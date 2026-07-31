using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Military;
using VictoriaGame.World;

namespace VictoriaGame.Politics
{
    /// <summary>
    /// Applique les intentions joueur AVANT la fiscalité / trésorerie / construction.
    /// Valide (bornes, pays, contrôle, cible, type, trésorerie, emplacement) puis écrit.
    /// L'UI n'écrit jamais l'état du monde — uniquement la file d'intentions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(VictoriaGame.Economy.TaxSystem))]
    [UpdateBefore(typeof(BuildingConstructionSystem))]
    public partial struct ApplyPlayerIntentionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
            state.RequireForUpdate<PlayerIntentionQueueTag>();
            state.RequireForUpdate<PlayerControl>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<WorldState>())
                return;

            var worldState = SystemAPI.GetSingleton<WorldState>();
            if (worldState.IsPaused)
                return;

            var queueEntity = SystemAPI.GetSingletonEntity<PlayerIntentionQueueTag>();
            var buffer = SystemAPI.GetBuffer<PlayerIntention>(queueEntity);
            if (buffer.Length == 0)
                return;

            // Copier AVANT toute création d'entité (chantier / guerre) — CreateEntity invalide le buffer.
            var pending = new NativeList<PlayerIntention>(buffer.Length, Allocator.Temp);
            for (var i = 0; i < buffer.Length; i++)
                pending.Add(buffer[i]);
            buffer.Clear();

            var control = SystemAPI.GetSingleton<PlayerControl>();
            var em = state.EntityManager;
            var tick = worldState.CurrentTick;

            var countryMap = new NativeHashMap<int, Entity>(64, Allocator.Temp);
            foreach (var (cd, entity) in SystemAPI.Query<RefRO<CountryData>>().WithEntityAccess())
            {
                countryMap.TryAdd(cd.ValueRO.CountryId, entity);
            }

            PlayerIntentionReceipt lastReceipt = default;
            var hasReceipt = false;

            for (var i = 0; i < pending.Length; i++)
            {
                var intention = pending[i];
                lastReceipt = ApplyOne(em, in intention, in control, tick, countryMap);
                hasReceipt = true;
            }

            pending.Dispose();
            countryMap.Dispose();

            if (hasReceipt && em.HasComponent<PlayerIntentionReceipt>(queueEntity))
            {
                em.SetComponentData(queueEntity, lastReceipt);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        static PlayerIntentionReceipt ApplyOne(
            EntityManager em,
            in PlayerIntention intention,
            in PlayerControl control,
            int tick,
            NativeHashMap<int, Entity> countryMap)
        {
            var receipt = new PlayerIntentionReceipt
            {
                Kind = intention.Kind,
                CountryId = intention.CountryId,
                Value = intention.Value,
                AppliedTick = tick,
                Accepted = 0,
                Reason = PlayerIntentionSubmit.ReasonUnknownKind,
                TargetId = intention.TargetId
            };

            if (intention.Kind == PlayerIntentionKind.SetProductionTaxRate)
                return ApplyTax(em, in intention, in control, countryMap, ref receipt);

            if (intention.Kind == PlayerIntentionKind.StartBuildingConstruction)
                return ApplyBuild(em, in intention, in control, countryMap, ref receipt);

            if (intention.Kind == PlayerIntentionKind.InvestProvinceDevelopment)
                return ApplyInvest(em, in intention, in control, countryMap, ref receipt);

            if (intention.Kind == PlayerIntentionKind.DeclareWar)
                return ApplyDeclareWar(em, in intention, in control, tick, countryMap, ref receipt);

            if (intention.Kind == PlayerIntentionKind.ProposePeace)
                return ApplyProposePeace(em, in intention, in control, tick, countryMap, ref receipt);

            if (intention.Kind == PlayerIntentionKind.EnactLaw)
                return ApplyEnactLaw(em, in intention, in control, tick, countryMap, ref receipt);

            receipt.Reason = PlayerIntentionSubmit.ReasonUnknownKind;
            return receipt;
        }

        /// <summary>
        /// v1_089 — EnactLaw : écrit EnactedLaw (une loi par catégorie, remplace).
        /// Refus nommés : country_not_controlled, law_not_found, government_type_insufficient,
        /// law_not_available, law_already_enacted.
        /// </summary>
        static PlayerIntentionReceipt ApplyEnactLaw(
            EntityManager em,
            in PlayerIntention intention,
            in PlayerControl control,
            int tick,
            NativeHashMap<int, Entity> countryMap,
            ref PlayerIntentionReceipt receipt)
        {
            if (intention.CountryId != control.ControlledCountryId)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonCountryNotControlled;
                return receipt;
            }

            if (!countryMap.TryGetValue(intention.CountryId, out var countryEntity) ||
                countryEntity == Entity.Null)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonCountryNotFound;
                return receipt;
            }

            if (intention.TargetId.IsEmpty)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonLawNotFound;
                return receipt;
            }

            if (!TryFindLaw(em, intention.TargetId, out var law))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonLawNotFound;
                return receipt;
            }

            if (!em.HasComponent<GovernmentData>(countryEntity) ||
                !em.HasBuffer<EnactedLaw>(countryEntity))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonCountryNotFound;
                return receipt;
            }

            var gov = em.GetComponentData<GovernmentData>(countryEntity);
            if ((byte)gov.Type < law.MinGovernmentTypeByte)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonGovernmentTypeInsufficient;
                return receipt;
            }

            if (tick < law.AvailableFromTick)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonLawNotAvailable;
                return receipt;
            }

            var buffer = em.GetBuffer<EnactedLaw>(countryEntity);
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].LawId.Equals(law.LawId))
                {
                    receipt.Reason = PlayerIntentionSubmit.ReasonLawAlreadyEnacted;
                    return receipt;
                }
            }

            // Une loi par catégorie : remplacer si même catégorie, sinon ajouter.
            var replaced = false;
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Category != law.Category)
                    continue;
                buffer[i] = new EnactedLaw
                {
                    LawId = law.LawId,
                    Category = law.Category,
                    EnactedTick = tick
                };
                replaced = true;
                break;
            }

            if (!replaced)
            {
                buffer.Add(new EnactedLaw
                {
                    LawId = law.LawId,
                    Category = law.Category,
                    EnactedTick = tick
                });
            }

            // Recalcule Σ tax_mod (clés LawId, jamais Entity.Index).
            var taxModSum = 0f;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<LawData>()))
            using (var laws = q.ToComponentDataArray<LawData>(Allocator.Temp))
            {
                for (var i = 0; i < buffer.Length; i++)
                {
                    var id = buffer[i].LawId;
                    for (var j = 0; j < laws.Length; j++)
                    {
                        if (!laws[j].LawId.Equals(id))
                            continue;
                        taxModSum += laws[j].TaxMod;
                        break;
                    }
                }
            }

            if (em.HasComponent<LawTaxMods>(countryEntity))
                em.SetComponentData(countryEntity, new LawTaxMods { TaxModSum = taxModSum });
            else
                em.AddComponentData(countryEntity, new LawTaxMods { TaxModSum = taxModSum });

            receipt.Accepted = 1;
            receipt.Reason = PlayerIntentionSubmit.ReasonAccepted;
            receipt.TargetId = law.LawId;
            return receipt;
        }

        static bool TryFindLaw(EntityManager em, FixedString32Bytes lawId, out LawData law)
        {
            law = default;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<LawData>());
            using var arr = q.ToComponentDataArray<LawData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                if (!arr[i].LawId.Equals(lawId))
                    continue;
                law = arr[i];
                return true;
            }

            return false;
        }

        static PlayerIntentionReceipt ApplyTax(
            EntityManager em,
            in PlayerIntention intention,
            in PlayerControl control,
            NativeHashMap<int, Entity> countryMap,
            ref PlayerIntentionReceipt receipt)
        {
            if (intention.CountryId != control.ControlledCountryId)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonCountryNotControlled;
                return receipt;
            }

            if (!countryMap.TryGetValue(intention.CountryId, out var countryEntity) ||
                countryEntity == Entity.Null)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonCountryNotFound;
                return receipt;
            }

            if (!TaxPolicyLimits.IsInBounds(intention.Value))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonRateOutOfBounds;
                return receipt;
            }

            if (!em.HasComponent<TaxPolicy>(countryEntity))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonCountryNotFound;
                return receipt;
            }

            em.SetComponentData(countryEntity, new TaxPolicy
            {
                ProductionTaxRate = intention.Value
            });

            receipt.Accepted = 1;
            receipt.Reason = PlayerIntentionSubmit.ReasonAccepted;
            return receipt;
        }

        static PlayerIntentionReceipt ApplyBuild(
            EntityManager em,
            in PlayerIntention intention,
            in PlayerControl control,
            NativeHashMap<int, Entity> countryMap,
            ref PlayerIntentionReceipt receipt)
        {
            // v1_039 : la porte d'autorisation pour StartBuildingConstruction est la
            // possession de la province (Owner == pays de l'intention) + trésorerie +
            // type connu — PAS PlayerControl. L'UI n'enqueue que le pays contrôlé ; l'IA
            // enqueue les autres. Les deux subissent la MÊME validation économique.
            // (SetProductionTaxRate garde le filtre PlayerControl.)
            _ = control;

            if (!countryMap.TryGetValue(intention.CountryId, out var countryEntity) ||
                countryEntity == Entity.Null)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonCountryNotFound;
                return receipt;
            }

            var cityId = (int)math.round(intention.Value);
            var typeInt = (int)math.round(intention.ValueB);
            if (typeInt < 0 || typeInt > (int)BuildingType.Factory)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonTypeUnknown;
                return receipt;
            }

            var type = (BuildingType)typeInt;
            if (!BuildingConstructionSystem.IsConstructibleType(type) ||
                !BuildingConstructionSystem.TryGetCatalogEntry(em, type, out var cat))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonTypeUnknown;
                return receipt;
            }

            if (!TryFindCity(em, cityId, out var city))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonTargetUnknown;
                return receipt;
            }

            if (BuildingConstructionSystem.CityHasActiveConstruction(em, cityId))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonSlotOccupied;
                return receipt;
            }

            // Province doit appartenir au pays contrôlé.
            if (city.Province == Entity.Null ||
                !em.HasComponent<ProvinceOwnership>(city.Province))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonProvinceNotOwned;
                return receipt;
            }

            var owner = em.GetComponentData<ProvinceOwnership>(city.Province).Owner;
            if (owner != countryEntity)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonProvinceNotOwned;
                return receipt;
            }

            if (!em.HasComponent<TreasuryData>(countryEntity))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonCountryNotFound;
                return receipt;
            }

            var treasury = em.GetComponentData<TreasuryData>(countryEntity);
            if (treasury.Balance + 1e-4f < cat.MoneyCost)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonInsufficientTreasury;
                return receipt;
            }

            // Output = bien provincial si site présent, sinon défaut catalogue.
            var outputGoodId = cat.DefaultOutputGoodId;
            if (em.HasComponent<ProductionSite>(city.Province))
            {
                var site = em.GetComponentData<ProductionSite>(city.Province);
                if (site.GoodId > 0)
                    outputGoodId = site.GoodId;
            }

            treasury.Balance -= cat.MoneyCost;
            treasury.Expenses += cat.MoneyCost;
            em.SetComponentData(countryEntity, treasury);

            var buildingId = BuildingConstructionSystem.NextBuildingId(em);
            if (!BuildingConstructionSystem.TryCreateConstructionSite(
                    em, buildingId, type, cityId, city.ProvinceId,
                    intention.CountryId, outputGoodId, cat.MoneyCost, out _))
            {
                // Rembourse si création échoue (ne devrait pas arriver après catalogue OK).
                treasury.Balance += cat.MoneyCost;
                treasury.Expenses -= cat.MoneyCost;
                em.SetComponentData(countryEntity, treasury);
                receipt.Reason = PlayerIntentionSubmit.ReasonTypeUnknown;
                return receipt;
            }

            if (TryGetBuildingSingleton(em, out var singleton))
            {
                var metrics = em.GetComponentData<BuildingEconomyMetrics>(singleton);
                metrics.MoneySpent += cat.MoneyCost;
                em.SetComponentData(singleton, metrics);
            }

            receipt.Accepted = 1;
            receipt.Reason = PlayerIntentionSubmit.ReasonAccepted;
            return receipt;
        }

        /// <summary>
        /// v1_087 — investissement ProvinceDevelopment : +1 sur un axe, coût trésorerie,
        /// bornes [1..30]. Verbe joueur uniquement (PlayerControl) + possession province.
        /// </summary>
        static PlayerIntentionReceipt ApplyInvest(
            EntityManager em,
            in PlayerIntention intention,
            in PlayerControl control,
            NativeHashMap<int, Entity> countryMap,
            ref PlayerIntentionReceipt receipt)
        {
            if (intention.CountryId != control.ControlledCountryId)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonCountryNotControlled;
                return receipt;
            }

            if (!countryMap.TryGetValue(intention.CountryId, out var countryEntity) ||
                countryEntity == Entity.Null)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonCountryNotFound;
                return receipt;
            }

            var axis = (byte)math.round(intention.ValueB);
            if (!ProvinceDevelopmentInvestment.IsValidAxis(axis))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonAxisUnknown;
                return receipt;
            }

            var provinceId = (int)math.round(intention.Value);
            if (!TryFindProvince(em, provinceId, out var provinceEntity))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonProvinceNotFound;
                return receipt;
            }

            if (!em.HasComponent<ProvinceOwnership>(provinceEntity) ||
                !em.HasComponent<ProvinceDevelopment>(provinceEntity))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonProvinceNotFound;
                return receipt;
            }

            var owner = em.GetComponentData<ProvinceOwnership>(provinceEntity).Owner;
            if (owner != countryEntity)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonProvinceNotOwned;
                return receipt;
            }

            var dev = em.GetComponentData<ProvinceDevelopment>(provinceEntity);
            var current = ProvinceDevelopmentInvestment.ReadAxis(in dev, axis);
            if (current >= ProvinceDevelopmentInvestment.MaxLevel)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonLevelAtCeiling;
                return receipt;
            }

            if (!em.HasComponent<TreasuryData>(countryEntity))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonCountryNotFound;
                return receipt;
            }

            var cost = ProvinceDevelopmentInvestment.CostForLevel(current);
            var treasury = em.GetComponentData<TreasuryData>(countryEntity);
            if (treasury.Balance + 1e-4f < cost)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonInsufficientTreasury;
                return receipt;
            }

            treasury.Balance -= cost;
            treasury.Expenses += cost;
            em.SetComponentData(countryEntity, treasury);

            ProvinceDevelopmentInvestment.WriteAxis(ref dev, axis, current + 1);
            em.SetComponentData(provinceEntity, dev);

            receipt.Accepted = 1;
            receipt.Reason = PlayerIntentionSubmit.ReasonAccepted;
            return receipt;
        }

        /// <summary>
        /// v1_088 — DeclareWar : crée une WarData via WarData.Create (même fabrique que
        /// WarDeclarationSystem). Clés de domaine = CountryId uniquement.
        /// </summary>
        static PlayerIntentionReceipt ApplyDeclareWar(
            EntityManager em,
            in PlayerIntention intention,
            in PlayerControl control,
            int tick,
            NativeHashMap<int, Entity> countryMap,
            ref PlayerIntentionReceipt receipt)
        {
            if (intention.CountryId != control.ControlledCountryId)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonCountryNotControlled;
                return receipt;
            }

            if (!countryMap.TryGetValue(intention.CountryId, out var attackerEntity) ||
                attackerEntity == Entity.Null)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonCountryNotFound;
                return receipt;
            }

            var targetId = (int)math.round(intention.Value);
            if (targetId == intention.CountryId)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonTargetIsSelf;
                return receipt;
            }

            if (!countryMap.TryGetValue(targetId, out var defenderEntity) ||
                defenderEntity == Entity.Null)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonTargetUnknown;
                return receipt;
            }

            if (TryFindActiveWar(em, attackerEntity, defenderEntity, out _))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonAlreadyAtWar;
                return receipt;
            }

            var warEntity = em.CreateEntity();
            em.AddComponentData(
                warEntity,
                WarData.Create(attackerEntity, defenderEntity, CasusBelli.Conquest, tick));

            receipt.Accepted = 1;
            receipt.Reason = PlayerIntentionSubmit.ReasonAccepted;
            return receipt;
        }

        /// <summary>
        /// v1_088 — ProposePeace : termine une guerre active via le chemin paix blanche
        /// de PeaceSystem (IsActive=false, EndTick, restitution occupations, désengagement).
        /// </summary>
        static PlayerIntentionReceipt ApplyProposePeace(
            EntityManager em,
            in PlayerIntention intention,
            in PlayerControl control,
            int tick,
            NativeHashMap<int, Entity> countryMap,
            ref PlayerIntentionReceipt receipt)
        {
            if (intention.CountryId != control.ControlledCountryId)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonCountryNotControlled;
                return receipt;
            }

            if (!countryMap.TryGetValue(intention.CountryId, out var selfEntity) ||
                selfEntity == Entity.Null)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonCountryNotFound;
                return receipt;
            }

            var targetId = (int)math.round(intention.Value);
            if (targetId == intention.CountryId)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonTargetIsSelf;
                return receipt;
            }

            if (!countryMap.TryGetValue(targetId, out var otherEntity) ||
                otherEntity == Entity.Null)
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonTargetUnknown;
                return receipt;
            }

            if (!TryFindActiveWar(em, selfEntity, otherEntity, out var warEntity))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonNoActiveWar;
                return receipt;
            }

            if (!PeaceSystem.TryConcludePlayerWhitePeace(em, warEntity, tick))
            {
                receipt.Reason = PlayerIntentionSubmit.ReasonNoActiveWar;
                return receipt;
            }

            receipt.Accepted = 1;
            receipt.Reason = PlayerIntentionSubmit.ReasonAccepted;
            return receipt;
        }

        /// <summary>
        /// Guerre active entre deux pays (ordre attaquant/défenseur indifférent).
        /// </summary>
        static bool TryFindActiveWar(
            EntityManager em,
            Entity a,
            Entity b,
            out Entity warEntity)
        {
            warEntity = Entity.Null;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var wars = q.ToComponentDataArray<WarData>(Allocator.Temp);
            for (var i = 0; i < wars.Length; i++)
            {
                if (!wars[i].IsActive)
                    continue;
                if ((wars[i].Attacker == a && wars[i].Defender == b) ||
                    (wars[i].Attacker == b && wars[i].Defender == a))
                {
                    warEntity = entities[i];
                    return true;
                }
            }

            return false;
        }

        static bool TryFindProvince(EntityManager em, int provinceId, out Entity provinceEntity)
        {
            provinceEntity = Entity.Null;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var arr = q.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].ProvinceId != provinceId)
                    continue;
                provinceEntity = entities[i];
                return true;
            }

            return false;
        }

        static bool TryFindCity(EntityManager em, int cityId, out CityData city)
        {
            city = default;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>());
            using var arr = q.ToComponentDataArray<CityData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].CityId != cityId)
                    continue;
                city = arr[i];
                return true;
            }

            return false;
        }

        static bool TryGetBuildingSingleton(EntityManager em, out Entity entity)
        {
            entity = Entity.Null;
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<BuildingEconomySingleton>(),
                ComponentType.ReadOnly<BuildingEconomyMetrics>());
            if (q.IsEmptyIgnoreFilter)
                return false;
            entity = q.GetSingletonEntity();
            return true;
        }
    }
}
