using Unity.Entities;
using System;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Politics;
using VictoriaGame.World;

namespace VictoriaGame.Economy
{
    /// <summary>
    /// Couture fiscale → marchandises (v1_065 / v1_075 / v1_086) : la part taxée
    /// de la production est RETIRÉE des stocks physiques localisés ET, depuis
    /// v1_075, de l'offre du marché abstrait qui alimente lodSat dans
    /// <see cref="VictoriaGame.Population.PopConsumptionSystem"/>.
    /// <para>
    /// Deux coefficients réversibles indépendants. Défauts compilés = 0/0
    /// (réversibilité bit-identique via c=0 ; ancre digest : voir ParityAnchors —
    /// v1_090 a rebasé l'empreinte car CountryData.Population est devenue vivante).
    /// Adoption v1_086 via JSON :
    /// <list type="bullet">
    /// <item><see cref="WithdrawalCoefficient"/> (cPhys) — ProvinceStock + ledger.
    /// Non monotone seul (Δsat≈0) : laissé à 0, déclaré inutile.</item>
    /// <item><see cref="AbstractWithdrawalCoefficient"/> (cAbs) — offre LOD.
    /// ADOPTÉ à 0,5 (monotone, porte tout l'effet sat).</item>
    /// </list>
    /// À coefficient nul : no-op strict sur la couche concernée (bit-identique).
    /// PhysicalBlendWeight (w=0,25 adopté v1_022) n'est PAS modifié ici.
    /// </para>
    /// Quantité demandée = LastOutput × rate × yield × coefficient.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicalProductionSystem))]
    [UpdateBefore(typeof(PhysicalStockSystem))]
    public partial struct TaxPhysicalWithdrawalSystem : ISystem
    {
        /// <summary>Défaut compilé = 0 (réversibilité / parité v1_009).</summary>
        public const float DefaultWithdrawalCoefficient = 0f;

        /// <summary>Défaut compilé = 0 (réversibilité / parité v1_009).</summary>
        public const float DefaultAbstractWithdrawalCoefficient = 0f;

        /// <summary>v1_086 — cPhys non adopté (inutile, non monotone).</summary>
        public const float AdoptedWithdrawalCoefficient = 0f;

        /// <summary>v1_086 — cAbs adopté (canal monotone mesuré).</summary>
        public const float AdoptedAbstractWithdrawalCoefficient = 0.5f;

        /// <summary>
        /// [0..1] retrait physique. 0 = aucun retrait (no-op) ; 1 = part taxée retirée.
        /// Mutable pour les harnais de mesure.
        /// </summary>
        public static float WithdrawalCoefficient = DefaultWithdrawalCoefficient;

        /// <summary>
        /// [0..1] retrait sur l'offre LOD (lodSat). 0 = no-op bit-identique.
        /// Lu par <see cref="VictoriaGame.Population.PopConsumptionSystem"/>.
        /// </summary>
        public static float AbstractWithdrawalCoefficient = DefaultAbstractWithdrawalCoefficient;

        static bool _harnessLocked;
        static bool _jsonApplied;

        /// <summary>Quantité physique demandée ce tick (avant plafond stock).</summary>
        public static double LastTickRequested;

        /// <summary>Quantité physique effectivement retirée ce tick.</summary>
        public static double LastTickWithdrawn;

        /// <summary>Quantité abstraite demandée ce tick ( LOD ).</summary>
        public static double LastTickAbstractRequested;

        /// <summary>Quantité abstraite effectivement retirée de l'offre LOD ce tick.</summary>
        public static double LastTickAbstractWithdrawn;

        /// <summary>Cumul session physique — remis à 0 par ResetSessionTotals.</summary>
        public static double SessionRequested;

        /// <summary>Cumul session physique retiré.</summary>
        public static double SessionWithdrawn;

        /// <summary>Cumul session abstrait demandé.</summary>
        public static double SessionAbstractRequested;

        /// <summary>Cumul session abstrait retiré de l'offre LOD.</summary>
        public static double SessionAbstractWithdrawn;

        public static double LastTickCpuMs;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
            ApplyJsonDefaultIfUnlocked();
        }

        // Pas de [BurstCompile] : lecture des coefficients static mutable — BC1040.
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<WorldState>())
            {
                return;
            }

            if (SystemAPI.GetSingleton<WorldState>().IsPaused)
            {
                return;
            }

            // Garde-fou bit-identique : à cPhys≤0 on ne touche RIEN côté physique.
            var coefficient = WithdrawalCoefficient;
            if (coefficient <= 0f)
            {
                LastTickRequested = 0.0;
                LastTickWithdrawn = 0.0;
                LastTickCpuMs = 0.0;
                return;
            }

            if (!SystemAPI.HasSingleton<PhysicalEconomySingleton>())
            {
                LastTickRequested = 0.0;
                LastTickWithdrawn = 0.0;
                LastTickCpuMs = 0.0;
                return;
            }

            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            coefficient = math.saturate(coefficient);
            state.Dependency.Complete();

            var em = state.EntityManager;
            var singletonEntity = SystemAPI.GetSingletonEntity<PhysicalEconomySingleton>();
            var ledger = em.GetBuffer<PhysicalLedgerEntry>(singletonEntity);

            var taxPolicyLookup = SystemAPI.GetComponentLookup<TaxPolicy>(true);
            taxPolicyLookup.Update(ref state);
            var lawTaxModsLookup = SystemAPI.GetComponentLookup<LawTaxMods>(true);
            lawTaxModsLookup.Update(ref state);

            var defaultRate = TaxSystem.ProductionTaxRate;
            var nonCoreYield = TaxSystem.NonCoreYieldFactor;

            // Collecte puis tri déterministe ProvinceId → GoodId (jamais Entity.Index).
            var rows = new NativeList<WithdrawalRow>(64, Allocator.TempJob);
            foreach (var (ownership, site, province) in SystemAPI
                         .Query<RefRO<ProvinceOwnership>, RefRO<ProductionSite>, RefRO<ProvinceData>>())
            {
                if (ownership.ValueRO.Owner == Entity.Null)
                {
                    continue;
                }

                if (site.ValueRO.GoodId <= 0 || site.ValueRO.LastOutput <= 0f)
                {
                    continue;
                }

                var rate = defaultRate;
                if (taxPolicyLookup.HasComponent(ownership.ValueRO.Owner))
                {
                    rate = taxPolicyLookup[ownership.ValueRO.Owner].ProductionTaxRate;
                }

                // Taux effectif lois (Σ=0 → inchangé, bit-identique).
                if (lawTaxModsLookup.HasComponent(ownership.ValueRO.Owner))
                {
                    var lawMod = lawTaxModsLookup[ownership.ValueRO.Owner].TaxModSum;
                    if (lawMod != 0f)
                        rate = LawTaxEffect.EffectiveProductionTaxRate(rate, lawMod);
                }

                if (rate <= 0f)
                {
                    continue;
                }

                var yield = ownership.ValueRO.Owner != ownership.ValueRO.Core
                    ? nonCoreYield
                    : 1f;
                var requested = (double)site.ValueRO.LastOutput * rate * yield * coefficient;
                if (requested <= 0.0)
                {
                    continue;
                }

                rows.Add(new WithdrawalRow
                {
                    ProvinceId = province.ValueRO.ProvinceId,
                    GoodId = site.ValueRO.GoodId,
                    Requested = requested
                });
            }

            rows.Sort(new WithdrawalRowComparer());

            var provinceById = new NativeHashMap<int, Entity>(64, Allocator.TempJob);
            foreach (var (prov, entity) in SystemAPI.Query<RefRO<ProvinceData>>().WithEntityAccess())
            {
                provinceById.TryAdd(prov.ValueRO.ProvinceId, entity);
            }

            double requestedTotal = 0.0;
            double withdrawnTotal = 0.0;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                requestedTotal += row.Requested;
                if (!provinceById.TryGetValue(row.ProvinceId, out var provEntity) ||
                    !em.HasBuffer<ProvinceStock>(provEntity))
                {
                    continue;
                }

                var stock = em.GetBuffer<ProvinceStock>(provEntity);
                var taken = PhysicalStockSystem.TryRemoveFromStock(stock, row.GoodId, row.Requested);
                if (taken > 0.0)
                {
                    PhysicalStockSystem.AddLedgerConsumptionPublic(ledger, row.GoodId, taken);
                    withdrawnTotal += taken;
                }
            }

            provinceById.Dispose();
            rows.Dispose();

            LastTickRequested = requestedTotal;
            LastTickWithdrawn = withdrawnTotal;
            SessionRequested += requestedTotal;
            SessionWithdrawn += withdrawnTotal;

            var end = System.Diagnostics.Stopwatch.GetTimestamp();
            LastTickCpuMs = (end - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        /// <summary>
        /// Offre LOD effective après retrait fiscal abstrait.
        /// À cAbs≤0 : retourne rawOutput inchangé (chemin bit-identique).
        /// <para>
        /// Part retirée = (rate / MaxProductionTaxRate) × yield × cAbs.
        /// Le taux monétaire (2e-5) est calibré pour le trésor, pas pour un flux
        /// LOD : l'appliquer tel quel laisse lodSat inerte (0,02 % de l'offre).
        /// Normaliser par le plafond convertit le levier fiscal en part de
        /// marchandises — c'est ce qui atteint les 75 % portés par lodSat.
        /// Couche physique : conserve LastOutput×rate×yield×cPhys (stocks).
        /// </para>
        /// </summary>
        public static float EffectiveAbstractSupply(
            float rawOutput, float taxRate, float yieldFactor)
        {
            var c = AbstractWithdrawalCoefficient;
            if (c <= 0f || rawOutput <= 0f || taxRate <= 0f)
            {
                return rawOutput;
            }

            c = math.saturate(c);
            var maxRate = TaxPolicyLimits.MaxProductionTaxRate;
            if (maxRate <= 0f)
            {
                return rawOutput;
            }

            var share = math.saturate(taxRate / maxRate) * yieldFactor * c;
            var withheld = rawOutput * share;
            if (withheld <= 0f)
            {
                return rawOutput;
            }

            if (withheld > rawOutput)
            {
                withheld = rawOutput;
            }

            return rawOutput - withheld;
        }

        /// <summary>
        /// Quantité abstraite retirée pour un site (conservation couche LOD).
        /// </summary>
        public static float AbstractWithheldAmount(
            float rawOutput, float taxRate, float yieldFactor)
        {
            return math.max(0f, rawOutput - EffectiveAbstractSupply(rawOutput, taxRate, yieldFactor));
        }

        /// <summary>
        /// Enregistre le bilan abstrait d'un tick (appelé par PopConsumptionSystem).
        /// </summary>
        public static void RecordAbstractTick(double requested, double withdrawn)
        {
            LastTickAbstractRequested = requested;
            LastTickAbstractWithdrawn = withdrawn;
            SessionAbstractRequested += requested;
            SessionAbstractWithdrawn += withdrawn;
        }

        /// <summary>Verrouille le coefficient physique pour un harnais.</summary>
        public static void LockCoefficient(float coefficient)
        {
            WithdrawalCoefficient = math.saturate(coefficient);
            _harnessLocked = true;
            _jsonApplied = true;
        }

        /// <summary>Verrouille le coefficient abstrait pour un harnais.</summary>
        public static void LockAbstractCoefficient(float coefficient)
        {
            AbstractWithdrawalCoefficient = math.saturate(coefficient);
            _harnessLocked = true;
            _jsonApplied = true;
        }

        /// <summary>Verrouille les deux coefficients d'un coup.</summary>
        public static void LockCoefficients(float physical, float abstractCoeff)
        {
            WithdrawalCoefficient = math.saturate(physical);
            AbstractWithdrawalCoefficient = math.saturate(abstractCoeff);
            _harnessLocked = true;
            _jsonApplied = true;
        }

        /// <summary>Relâche le verrou et recharge le défaut JSON / const.</summary>
        public static void UnlockCoefficient()
        {
            _harnessLocked = false;
            _jsonApplied = false;
            ApplyJsonDefaultIfUnlocked();
        }

        /// <summary>
        /// Pose les défauts compilés (0/0) et empêche la lecture JSON sans
        /// écraser un <see cref="LockCoefficients"/> déjà posé par un test.
        /// Appelé par SimulationHarness avant OnCreate.
        /// </summary>
        public static void EnsureParitySafeDefaults()
        {
            if (_harnessLocked)
            {
                return;
            }

            WithdrawalCoefficient = DefaultWithdrawalCoefficient;
            AbstractWithdrawalCoefficient = DefaultAbstractWithdrawalCoefficient;
            _jsonApplied = true;
        }

        /// <summary>Remet les statics au défaut compilé (sans relire le disque).</summary>
        public static void ResetToCompiledDefault()
        {
            WithdrawalCoefficient = DefaultWithdrawalCoefficient;
            AbstractWithdrawalCoefficient = DefaultAbstractWithdrawalCoefficient;
            _harnessLocked = false;
            _jsonApplied = false;
        }

        public static void ResetSessionTotals()
        {
            SessionRequested = 0.0;
            SessionWithdrawn = 0.0;
            SessionAbstractRequested = 0.0;
            SessionAbstractWithdrawn = 0.0;
            LastTickRequested = 0.0;
            LastTickWithdrawn = 0.0;
            LastTickAbstractRequested = 0.0;
            LastTickAbstractWithdrawn = 0.0;
        }

        static void ApplyJsonDefaultIfUnlocked()
        {
            if (_harnessLocked || _jsonApplied)
            {
                return;
            }

            _jsonApplied = true;
            var (phys, abs) = LoadCoefficientsFromJson(
                DefaultWithdrawalCoefficient, DefaultAbstractWithdrawalCoefficient);
            WithdrawalCoefficient = phys;
            AbstractWithdrawalCoefficient = abs;
        }

        static (float phys, float abs) LoadCoefficientsFromJson(float fallbackPhys, float fallbackAbs)
        {
            var path = Path.Combine(
                Application.streamingAssetsPath, "data", "tax_physical_withdrawal.json");

            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    "TaxPhysicalWithdrawalSystem: tax_physical_withdrawal.json introuvable — " +
                    $"défauts const phys={fallbackPhys} abs={fallbackAbs}");
                return (fallbackPhys, fallbackAbs);
            }

            var data = JsonUtility.FromJson<WithdrawalFile>(File.ReadAllText(path));
            var cPhys = data.withdrawal_coefficient;
            var cAbs = data.abstract_withdrawal_coefficient;
            if (cPhys < 0f || cPhys > 1f || float.IsNaN(cPhys) || float.IsInfinity(cPhys))
            {
                Debug.LogWarning(
                    $"TaxPhysicalWithdrawalSystem: coefficient physique JSON invalide ({cPhys}) — " +
                    $"défaut const={fallbackPhys}");
                cPhys = fallbackPhys;
            }

            if (cAbs < 0f || cAbs > 1f || float.IsNaN(cAbs) || float.IsInfinity(cAbs))
            {
                Debug.LogWarning(
                    $"TaxPhysicalWithdrawalSystem: coefficient abstrait JSON invalide ({cAbs}) — " +
                    $"défaut const={fallbackAbs}");
                cAbs = fallbackAbs;
            }

            Debug.Log(
                $"TaxPhysicalWithdrawalSystem: withdrawal_coefficient={cPhys} " +
                $"abstract_withdrawal_coefficient={cAbs} " +
                $"(depuis JSON; justification={data.coefficient_justification})");
            return (cPhys, cAbs);
        }

        struct WithdrawalRow
        {
            public int ProvinceId;
            public int GoodId;
            public double Requested;
        }

        struct WithdrawalRowComparer : System.Collections.Generic.IComparer<WithdrawalRow>
        {
            public int Compare(WithdrawalRow a, WithdrawalRow b)
            {
                var c = a.ProvinceId.CompareTo(b.ProvinceId);
                return c != 0 ? c : a.GoodId.CompareTo(b.GoodId);
            }
        }

        [Serializable]
        class WithdrawalFile
        {
            public float withdrawal_coefficient = DefaultWithdrawalCoefficient;
            public float abstract_withdrawal_coefficient = DefaultAbstractWithdrawalCoefficient;
            public string coefficient_justification = "";
        }
    }
}
