using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using NUnit.Framework;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Military;
using VictoriaGame.World;

namespace VictoriaGame.Tests
{
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.Mil023BatchRunner.Run</summary>
    public static class Mil023BatchRunner
    {
        public static void Run()
        {
            Mil023DiagnosticTests.RunDiagnosticsAndWriteLog();
            UnityEngine.Debug.Log("Mil023BatchRunner: DONE");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
            #endif
        }
    }

    [TestFixture]
    public class Mil023DiagnosticTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 200, 500, 1000 };

        enum AblationKind : byte
        {
            Production = 0,
            NoSolvencyGates = 1,
            NoRecruitCost = 2,
            NoDecisiveWars = 3,
            NoTerritorialAdmin = 4,
            AllOff = 5
        }

        struct AblationConfig
        {
            public AblationKind Kind;
            public string Label;
        }

        static readonly AblationConfig[] Configs =
        {
            new AblationConfig
            {
                Kind = AblationKind.Production,
                Label = "A — PRODUCTION (GateMode=FluxCommitted, RecruitCostScale=0.05, OccupationScoreRate=0.5, CostMode=PerProvince 0.10)"
            },
            new AblationConfig
            {
                Kind = AblationKind.NoSolvencyGates,
                Label = "B — SANS désarmement/gates solvabilité (GateMode=Disabled)"
            },
            new AblationConfig
            {
                Kind = AblationKind.NoRecruitCost,
                Label = "C — SANS coût de recrutement (RecruitCostScale=0)"
            },
            new AblationConfig
            {
                Kind = AblationKind.NoDecisiveWars,
                Label = "D — SANS guerres décisives (OccupationScoreRate=0)"
            },
            new AblationConfig
            {
                Kind = AblationKind.NoTerritorialAdmin,
                Label = "E — SANS admin territorial (CostMode=FlatBaseline)"
            },
            new AblationConfig
            {
                Kind = AblationKind.AllOff,
                Label = "F — TOUT ÉTEINT (B+C+D+E)"
            }
        };

        [Test]
        public void Mil023_AblationDemilitarizationDiagnostic() => RunDiagnosticsAndWriteLog();

        public static void RunDiagnosticsAndWriteLog()
        {
            var prevGate = ArmyDisbandmentSystem.GateMode;
            var prevRecruit = TemplateRecruitSystem.RecruitCostScale;
            var prevOcc = OccupationScoreSystem.OccupationScoreRate;
            var prevCostMode = MilitaryUpkeepSystem.CostMode;
            var prevAdmin = MilitaryUpkeepSystem.AdminCostPerProvince;

            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "mil_023_diagnostic.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(
                    $"=== mil_023 DIAGNOSTIC seed={Seed} — ablation démilitarisation worldArmyStr ===");
                sb.AppendLine(
                    "Mesure PURE : aucun système modifié. Leviers = statics mutables déjà existants.");
                sb.AppendLine(
                    "Hypothèses : H1=sain (moins de pays → moins d'armées) ; " +
                    "H2=pathologique (survivants aussi affaiblis).");
                sb.AppendLine();

                // worldArmyStr[configIndex][tickIndex]
                var armyByConfig = new float[Configs.Length][];
                var landByConfig = new int[Configs.Length][];

                for (var c = 0; c < Configs.Length; c++)
                {
                    armyByConfig[c] = new float[SnapshotTicks.Length];
                    landByConfig[c] = new int[SnapshotTicks.Length];
                    AppendAblationScenario(sb, Configs[c], armyByConfig[c], landByConfig[c]);
                }

                AppendDecomposition(sb);
                AppendVerdict(sb, armyByConfig, landByConfig);

                File.WriteAllText(logPath, sb.ToString());
                UnityEngine.Debug.Log(sb.ToString());
            }
            finally
            {
                ArmyDisbandmentSystem.GateMode = prevGate;
                TemplateRecruitSystem.RecruitCostScale = prevRecruit;
                OccupationScoreSystem.OccupationScoreRate = prevOcc;
                MilitaryUpkeepSystem.CostMode = prevCostMode;
                MilitaryUpkeepSystem.AdminCostPerProvince = prevAdmin;
            }
        }

        static void ApplyConfig(AblationKind kind)
        {
            // Toujours repartir des valeurs de production, puis éteindre le levier demandé.
            ArmyDisbandmentSystem.GateMode = ArmySolvencyGateMode.FluxCommitted;
            TemplateRecruitSystem.RecruitCostScale = TemplateRecruitSystem.DefaultRecruitCostScale;
            OccupationScoreSystem.OccupationScoreRate = OccupationScoreSystem.DefaultOccupationScoreRate;
            MilitaryUpkeepSystem.CostMode = AdminCostMode.PerProvince;
            MilitaryUpkeepSystem.AdminCostPerProvince = MilitaryUpkeepSystem.DefaultAdminCostPerProvince;

            switch (kind)
            {
                case AblationKind.NoSolvencyGates:
                    ArmyDisbandmentSystem.GateMode = ArmySolvencyGateMode.Disabled;
                    break;
                case AblationKind.NoRecruitCost:
                    TemplateRecruitSystem.RecruitCostScale = 0f;
                    break;
                case AblationKind.NoDecisiveWars:
                    OccupationScoreSystem.OccupationScoreRate = 0f;
                    break;
                case AblationKind.NoTerritorialAdmin:
                    MilitaryUpkeepSystem.CostMode = AdminCostMode.FlatBaseline;
                    break;
                case AblationKind.AllOff:
                    ArmyDisbandmentSystem.GateMode = ArmySolvencyGateMode.Disabled;
                    TemplateRecruitSystem.RecruitCostScale = 0f;
                    OccupationScoreSystem.OccupationScoreRate = 0f;
                    MilitaryUpkeepSystem.CostMode = AdminCostMode.FlatBaseline;
                    break;
            }
        }

        static void AppendAblationScenario(
            StringBuilder sb,
            AblationConfig config,
            float[] armyOut,
            int[] landOut)
        {
            ApplyConfig(config.Kind);
            sb.AppendLine($"=== {config.Label} ===");

            for (var i = 0; i < SnapshotTicks.Length; i++)
            {
                var tick = SnapshotTicks[i];
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(tick);
                var snap = CaptureWorldArmy(harness.EntityManager);
                armyOut[i] = snap.WorldArmyStr;
                landOut[i] = snap.CountriesWithLand;

                sb.AppendLine(
                    $"tick{tick}: worldArmyStr={snap.WorldArmyStr.ToString("F0", CultureInfo.InvariantCulture)} " +
                    $"countriesWithLand={snap.CountriesWithLand} livingArmies={snap.LivingArmies} " +
                    $"regiments={snap.TotalRegiments}");
            }

            sb.AppendLine();
        }

        static void AppendDecomposition(StringBuilder sb)
        {
            ApplyConfig(AblationKind.Production);
            sb.AppendLine("=== DÉCOMPOSITION t1000 — config PRODUCTION (A) ===");

            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(1000);
            var em = harness.EntityManager;
            var decomp = CaptureDecomposition(em);

            sb.AppendLine(
                $"livingArmies={decomp.LivingArmies} countriesWithLand={decomp.CountriesWithLand} " +
                $"worldArmyStr={decomp.WorldArmyStr.ToString("F0", CultureInfo.InvariantCulture)}");
            sb.AppendLine(
                $"avgStrPerArmy={decomp.AvgStrPerArmy.ToString("F1", CultureInfo.InvariantCulture)} " +
                $"avgStrPerSurvivingCountry={decomp.AvgStrPerSurvivingCountry.ToString("F1", CultureInfo.InvariantCulture)} " +
                $"(armyStrSurvivors={decomp.ArmyStrSurvivors.ToString("F0", CultureInfo.InvariantCulture)})");
            sb.AppendLine(
                $"totalRegiments={decomp.TotalRegiments} " +
                $"avgStrPerRegiment={decomp.AvgStrPerRegiment.ToString("F1", CultureInfo.InvariantCulture)}");
            sb.AppendLine(
                $"insolventGatedCountries={decomp.InsolventGated} " +
                $"(eco_027 FluxCommitted : !CanAffordRecruit)");
            sb.AppendLine(
                $"zombieArmyStrLandless={decomp.ZombieArmyStr.ToString("F0", CultureInfo.InvariantCulture)} " +
                $"landlessCountries={decomp.LandlessCountries}");

            sb.Append("top3:");
            for (var i = 0; i < decomp.Top.Count; i++)
            {
                var e = decomp.Top[i];
                sb.Append(
                    $" {e.Tag}={e.ArmyStr.ToString("F0", CultureInfo.InvariantCulture)}(land={e.HasLand})");
            }

            sb.AppendLine();
            sb.Append("bottom3:");
            for (var i = 0; i < decomp.Bottom.Count; i++)
            {
                var e = decomp.Bottom[i];
                sb.Append(
                    $" {e.Tag}={e.ArmyStr.ToString("F0", CultureInfo.InvariantCulture)}(land={e.HasLand})");
            }

            sb.AppendLine();
            sb.AppendLine();
        }

        static void AppendVerdict(StringBuilder sb, float[][] armyByConfig, int[][] landByConfig)
        {
            sb.AppendLine("=== VERDICT mil_023 ===");

            var tick1000 = SnapshotTicks.Length - 1;
            var prod = armyByConfig[0][tick1000];
            sb.AppendLine(
                $"Référence A (PRODUCTION) t1000: worldArmyStr={prod.ToString("F0", CultureInfo.InvariantCulture)} " +
                $"countriesWithLand={landByConfig[0][tick1000]}");

            // Deltas d'ablation : extinction qui remonte le plus = principal responsable.
            var deltas = new List<(string Label, float Delta, float Absolute)>();
            for (var c = 1; c < Configs.Length; c++)
            {
                var abs = armyByConfig[c][tick1000];
                var delta = abs - prod;
                var shortLabel = Configs[c].Kind switch
                {
                    AblationKind.NoSolvencyGates => "B gates/désarmement (eco_026/027)",
                    AblationKind.NoRecruitCost => "C coût recrutement (eco_031)",
                    AblationKind.NoDecisiveWars => "D guerres décisives (dip_005)",
                    AblationKind.NoTerritorialAdmin => "E admin territorial (eco_032)",
                    AblationKind.AllOff => "F tout éteint (plafond)",
                    _ => Configs[c].Label
                };
                deltas.Add((shortLabel, delta, abs));
                sb.AppendLine(
                    $"  {shortLabel}: worldArmyStr={abs.ToString("F0", CultureInfo.InvariantCulture)} " +
                    $"deltaVsA={FormatSigned(delta)} " +
                    $"countriesWithLand={landByConfig[c][tick1000]}");
            }

            // Classement hors F (plafond) : B,C,D,E par delta décroissant.
            var ranked = new List<(string Label, float Delta, float Absolute)>();
            for (var i = 0; i < deltas.Count; i++)
            {
                if (deltas[i].Label.StartsWith("F "))
                    continue;
                ranked.Add(deltas[i]);
            }

            ranked.Sort((a, b) => b.Delta.CompareTo(a.Delta));

            sb.AppendLine();
            sb.AppendLine("(1) Classement des mécanismes (delta worldArmyStr t1000 vs A, extinction → remontée) :");
            for (var i = 0; i < ranked.Count; i++)
            {
                sb.AppendLine(
                    $"  #{i + 1} {ranked[i].Label}: delta={FormatSigned(ranked[i].Delta)} " +
                    $"(abs={ranked[i].Absolute.ToString("F0", CultureInfo.InvariantCulture)})");
            }

            var topCause = ranked.Count > 0 ? ranked[0] : default;
            if (ranked.Count > 0 && topCause.Delta > 1f)
            {
                sb.AppendLine(
                    $"  → Principal responsable : {topCause.Label} " +
                    $"(+{topCause.Delta.ToString("F0", CultureInfo.InvariantCulture)} vs production).");
            }
            else if (ranked.Count > 0)
            {
                sb.AppendLine(
                    "  → Aucune extinction unitaire ne remonte nettement worldArmyStr : " +
                    "effet dominant = couplage / consolidation, pas un levier isolé.");
            }

            // H1 vs H2 : re-mesure décomposition A + comparaison F (monde « avant » approx).
            ApplyConfig(AblationKind.Production);
            DecompSnap prodDecomp;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(1000);
                prodDecomp = CaptureDecomposition(h.EntityManager);
            }

            ApplyConfig(AblationKind.AllOff);
            DecompSnap allOffDecomp;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(1000);
                allOffDecomp = CaptureDecomposition(h.EntityManager);
            }

            sb.AppendLine();
            sb.AppendLine("(2) Test H1 vs H2 — force moyenne PAR PAYS SURVIVANT (terre) :");
            sb.AppendLine(
                $"  A production: avgStrPerSurvivingCountry=" +
                $"{prodDecomp.AvgStrPerSurvivingCountry.ToString("F1", CultureInfo.InvariantCulture)} " +
                $"countriesWithLand={prodDecomp.CountriesWithLand} " +
                $"worldArmyStr={prodDecomp.WorldArmyStr.ToString("F0", CultureInfo.InvariantCulture)} " +
                $"zombie={prodDecomp.ZombieArmyStr.ToString("F0", CultureInfo.InvariantCulture)}");
            sb.AppendLine(
                $"  F tout éteint: avgStrPerSurvivingCountry=" +
                $"{allOffDecomp.AvgStrPerSurvivingCountry.ToString("F1", CultureInfo.InvariantCulture)} " +
                $"countriesWithLand={allOffDecomp.CountriesWithLand} " +
                $"worldArmyStr={allOffDecomp.WorldArmyStr.ToString("F0", CultureInfo.InvariantCulture)} " +
                $"zombie={allOffDecomp.ZombieArmyStr.ToString("F0", CultureInfo.InvariantCulture)}");

            var avgDown = prodDecomp.AvgStrPerSurvivingCountry + 50f < allOffDecomp.AvgStrPerSurvivingCountry;
            var totalDown = prodDecomp.WorldArmyStr + 500f < allOffDecomp.WorldArmyStr;
            var fewerCountries = prodDecomp.CountriesWithLand < allOffDecomp.CountriesWithLand;

            string hypothesis;
            string justification;
            if (totalDown && !avgDown && fewerCountries)
            {
                hypothesis = "H1 (SAIN)";
                justification =
                    "le total baisse surtout parce qu'il y a moins de pays survivants ; " +
                    "la force moyenne par pays encore vivant tient (ou ne s'effondre pas).";
            }
            else if (avgDown)
            {
                hypothesis = "H2 (PATHOLOGIQUE)";
                justification =
                    "la force moyenne par pays survivant a aussi baissé : même les survivants " +
                    "n'entretiennent plus une armée au niveau du plafond F.";
            }
            else if (totalDown && fewerCountries)
            {
                hypothesis = "H1 (SAIN, avec nuances)";
                justification =
                    "moins de pays et total en baisse, sans effondrement clair de la moyenne survivante.";
            }
            else
            {
                hypothesis = "INCONCLUSIF";
                justification =
                    "les écarts A vs F ne tranchent pas net entre consolidation et affaiblissement.";
            }

            sb.AppendLine($"  → VERDICT HYPOTHÈSE : {hypothesis} — {justification}");

            sb.AppendLine();
            sb.AppendLine("(3) Piste de correctif (SI H2 seulement — NON IMPLÉMENTÉE) :");
            if (hypothesis.StartsWith("H2"))
            {
                var tip = ranked.Count > 0 ? ranked[0].Label : "mécanisme dominant";
                sb.AppendLine(
                    $"  La piste la plus prometteuse suit le classement d'ablation : {tip}. " +
                    "Recalibrer ce levier (ou son interaction budget) avant de toucher les autres. " +
                    "Ne pas cumuler plusieurs correctifs à l'aveugle.");
            }
            else if (hypothesis.StartsWith("H1"))
            {
                sb.AppendLine(
                    "  Aucun correctif requis : la démilitarisation mesurée est cohérente avec la " +
                    "consolidation du monde (moins de pays → moins d'armées). Sujet clos côté mil_023.");
            }
            else
            {
                sb.AppendLine(
                    "  Pas de correctif tant que H1/H2 n'est pas tranché ; rejouer avec plus de seeds si besoin.");
            }

            sb.AppendLine();
            sb.AppendLine(
                "Note: deltas t200/t500 disponibles dans les blocs A–F ci-dessus ; " +
                "classement officiel sur t1000.");
        }

        static string FormatSigned(float v) =>
            (v >= 0f ? "+" : "") + v.ToString("F0", CultureInfo.InvariantCulture);

        struct WorldArmySnap
        {
            public float WorldArmyStr;
            public int CountriesWithLand;
            public int LivingArmies;
            public int TotalRegiments;
        }

        struct CountryArmyEntry
        {
            public FixedString32Bytes Tag;
            public float ArmyStr;
            public bool HasLand;
        }

        struct DecompSnap
        {
            public float WorldArmyStr;
            public int LivingArmies;
            public int CountriesWithLand;
            public int LandlessCountries;
            public float ArmyStrSurvivors;
            public float AvgStrPerArmy;
            public float AvgStrPerSurvivingCountry;
            public int TotalRegiments;
            public float AvgStrPerRegiment;
            public int InsolventGated;
            public float ZombieArmyStr;
            public List<CountryArmyEntry> Top;
            public List<CountryArmyEntry> Bottom;
        }

        static WorldArmySnap CaptureWorldArmy(EntityManager em)
        {
            var snap = new WorldArmySnap();
            using var armyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = armyQuery.ToComponentDataArray<ArmyData>(Allocator.Temp);
            snap.LivingArmies = armies.Length;
            for (var i = 0; i < armies.Length; i++)
                snap.WorldArmyStr += armies[i].Strength;

            using var regQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<ArmyData>(),
                ComponentType.ReadOnly<RegimentSlot>());
            using var armyEntities = regQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < armyEntities.Length; i++)
                snap.TotalRegiments += em.GetBuffer<RegimentSlot>(armyEntities[i]).Length;

            snap.CountriesWithLand = CountCountriesWithLand(em);
            return snap;
        }

        static DecompSnap CaptureDecomposition(EntityManager em)
        {
            var decomp = new DecompSnap
            {
                Top = new List<CountryArmyEntry>(3),
                Bottom = new List<CountryArmyEntry>(3)
            };

            var provinceCounts = CountProvincesByOwner(em);
            var armyByCountry = SumArmyByCountry(em);
            var regsByCountry = CountRegsByCountry(em);

            using var armyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = armyQuery.ToComponentDataArray<ArmyData>(Allocator.Temp);
            decomp.LivingArmies = armies.Length;
            for (var i = 0; i < armies.Length; i++)
                decomp.WorldArmyStr += armies[i].Strength;

            var regimentStrengthSum = 0f;
            using var regQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<ArmyData>(),
                ComponentType.ReadOnly<RegimentSlot>());
            using var armyEntities = regQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < armyEntities.Length; i++)
            {
                var slots = em.GetBuffer<RegimentSlot>(armyEntities[i]);
                decomp.TotalRegiments += slots.Length;
                for (var s = 0; s < slots.Length; s++)
                    regimentStrengthSum += slots[s].Strength;
            }

            decomp.AvgStrPerArmy = decomp.LivingArmies > 0
                ? decomp.WorldArmyStr / decomp.LivingArmies
                : 0f;
            decomp.AvgStrPerRegiment = decomp.TotalRegiments > 0
                ? regimentStrengthSum / decomp.TotalRegiments
                : 0f;

            using var countryQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var countries = countryQuery.ToEntityArray(Allocator.Temp);
            using var countryData = countryQuery.ToComponentDataArray<CountryData>(Allocator.Temp);
            using var treasuries = countryQuery.ToComponentDataArray<TreasuryData>(Allocator.Temp);

            var entries = new List<CountryArmyEntry>(countries.Length);

            for (var i = 0; i < countries.Length; i++)
            {
                var entity = countries[i];
                provinceCounts.TryGetValue(entity, out var prov);
                armyByCountry.TryGetValue(entity, out var armyStr);
                regsByCountry.TryGetValue(entity, out var regCount);
                var hasLand = prov > 0;

                if (hasLand)
                {
                    decomp.CountriesWithLand++;
                    decomp.ArmyStrSurvivors += armyStr;
                }
                else
                {
                    decomp.LandlessCountries++;
                    decomp.ZombieArmyStr += armyStr;
                }

                // Insolvabilité eco_027 = gate recrutement (FluxCommitted).
                if (!ArmyDisbandmentSystem.CanAffordRecruit(
                        treasuries[i], regCount, armyStr, ArmySolvencyGateMode.FluxCommitted))
                {
                    decomp.InsolventGated++;
                }

                entries.Add(new CountryArmyEntry
                {
                    Tag = countryData[i].Tag,
                    ArmyStr = armyStr,
                    HasLand = hasLand
                });
            }

            decomp.AvgStrPerSurvivingCountry = decomp.CountriesWithLand > 0
                ? decomp.ArmyStrSurvivors / decomp.CountriesWithLand
                : 0f;

            entries.Sort((a, b) => b.ArmyStr.CompareTo(a.ArmyStr));
            for (var i = 0; i < entries.Count && i < 3; i++)
                decomp.Top.Add(entries[i]);
            for (var i = entries.Count - 1; i >= 0 && decomp.Bottom.Count < 3; i--)
                decomp.Bottom.Add(entries[i]);

            provinceCounts.Dispose();
            armyByCountry.Dispose();
            regsByCountry.Dispose();
            return decomp;
        }

        static int CountCountriesWithLand(EntityManager em)
        {
            var owners = new HashSet<Entity>();
            using var ownQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceOwnership>());
            using var ownerships = ownQuery.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp);
            for (var i = 0; i < ownerships.Length; i++)
            {
                var o = ownerships[i];
                if (o.Owner != Entity.Null)
                    owners.Add(o.Owner);
            }

            return owners.Count;
        }

        static NativeHashMap<Entity, int> CountProvincesByOwner(EntityManager em)
        {
            var map = new NativeHashMap<Entity, int>(32, Allocator.Temp);
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceOwnership>());
            using var ownerships = query.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp);
            for (var i = 0; i < ownerships.Length; i++)
            {
                var owner = ownerships[i].Owner;
                if (owner == Entity.Null)
                    continue;
                map.TryGetValue(owner, out var current);
                map[owner] = current + 1;
            }

            return map;
        }

        static NativeHashMap<Entity, float> SumArmyByCountry(EntityManager em)
        {
            var map = new NativeHashMap<Entity, float>(32, Allocator.Temp);
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = query.ToComponentDataArray<ArmyData>(Allocator.Temp);
            for (var i = 0; i < armies.Length; i++)
            {
                var c = armies[i].Country;
                if (c == Entity.Null)
                    continue;
                map.TryGetValue(c, out var cur);
                map[c] = cur + armies[i].Strength;
            }

            return map;
        }

        static NativeHashMap<Entity, int> CountRegsByCountry(EntityManager em)
        {
            var map = new NativeHashMap<Entity, int>(32, Allocator.Temp);
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ArmyData>(),
                ComponentType.ReadOnly<RegimentSlot>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var armies = query.ToComponentDataArray<ArmyData>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var c = armies[i].Country;
                if (c == Entity.Null)
                    continue;
                var slots = em.GetBuffer<RegimentSlot>(entities[i]);
                map.TryGetValue(c, out var cur);
                map[c] = cur + slots.Length;
            }

            return map;
        }
    }
}
