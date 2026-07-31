using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using NUnit.Framework;
using VictoriaGame.Core;
using VictoriaGame.World;
using VictoriaGame.Economy;
using VictoriaGame.Population;
using VictoriaGame.Politics;
using VictoriaGame.Military;
using VictoriaGame.Navy;

namespace VictoriaGame.Tests
{
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.DeterminismBatchRunner.Run</summary>
    public static class DeterminismBatchRunner
    {
        public static void Run()
        {
            var tests = new DeterminismTests();
            tests.Determinism_SameSeedTwice_IdenticalFinalState();
            tests.Determinism_DifferentSeeds_DivergentFinalState();
            tests.Determinism_SeedOverride_ReachesWorldState();
            UnityEngine.Debug.Log("DeterminismBatchRunner: 3/3 PASS");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
            #endif
        }
    }

    /// <summary>
    /// Empreinte FNV-1a 64 bits de l'état du monde.
    ///
    /// Les floats sont hachés sur leurs bits bruts (<see cref="math.asuint"/>), sans arrondi
    /// ni tolérance : deux runs déterministes produisent les mêmes bits, pas « presque » les
    /// mêmes. Aucun float n'est additionné — une somme de floats dépend de l'ordre des termes,
    /// ce qui ferait échouer le test pour une raison qui n'a rien à voir avec la simulation.
    /// </summary>
    struct StateHash
    {
        const ulong FnvOffsetBasis = 14695981039346656037UL;
        const ulong FnvPrime = 1099511628211UL;

        ulong _hash;

        public static StateHash New() => new StateHash { _hash = FnvOffsetBasis };

        public ulong Value => _hash;

        public void Byte(byte b)
        {
            _hash ^= b;
            _hash *= FnvPrime;
        }

        public void U64(ulong value)
        {
            for (var i = 0; i < 8; i++)
                Byte((byte)(value >> (i * 8)));
        }

        public void Int(int value) => U64((uint)value);

        public void Bool(bool value) => Byte(value ? (byte)1 : (byte)0);

        /// <summary>Bits bruts du float — pas de quantification, pas de tolérance.</summary>
        public void Float(float value) => U64(math.asuint(value));

        /// <summary>Bits bruts du double (stocks physiques v1_025).</summary>
        public void Double(double value) => U64(math.asulong(value));

        /// <summary>
        /// Hache le contenu de la chaîne, jamais son GetHashCode() : celui-ci n'est pas
        /// garanti stable d'une exécution du process à l'autre.
        /// </summary>
        public void Text(string value)
        {
            Int(value.Length);
            foreach (var c in value)
                U64(c);
        }
    }

    /// <summary>
    /// Calcule une empreinte de l'état du monde, indépendante de l'ordre des entités.
    ///
    /// L'ordre d'itération des entités dans les chunks DOTS n'est pas garanti stable, donc
    /// toute collection est triée sur une clé métier (ProvinceId, tag pays, ZoneId) avant
    /// d'être hachée. Les clés de tri incluent la charge utile en départage : deux entités
    /// que la clé métier ne sépare pas (deux pops de même type dans la même province) sont
    /// alors ordonnées par leurs valeurs — et si elles sont identiques jusque dans leurs
    /// valeurs, leur ordre relatif ne change pas l'empreinte.
    /// </summary>
    static class WorldDigest
    {
        const string NoCountry = "none";

        public static ulong Compute(EntityManager em) =>
            Compute(em, includeCountryPopulation: true);

        /// <summary>
        /// Empreinte complète. <paramref name="includeCountryPopulation"/> = false
        /// exclut CountryData.Population du digest (preuve v1_090 : seul ce champ change).
        /// </summary>
        public static ulong Compute(EntityManager em, bool includeCountryPopulation)
        {
            var hash = StateHash.New();

            var countryTags = MapCountryTags(em);
            var provinceIds = MapProvinceIds(em);

            HashWorldState(em, ref hash);
            HashCountries(em, countryTags, ref hash, includeCountryPopulation);
            HashProvinces(em, countryTags, ref hash);
            HashPops(em, countryTags, provinceIds, ref hash);
            HashArmies(em, countryTags, ref hash);
            HashNavies(em, countryTags, ref hash);

            return hash.Value;
        }

        static Dictionary<Entity, string> MapCountryTags(EntityManager em)
        {
            var tags = new Dictionary<Entity, string>();

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
                tags[entity] = em.GetComponentData<CountryData>(entity).Tag.ToString();

            return tags;
        }

        static Dictionary<Entity, int> MapProvinceIds(EntityManager em)
        {
            var ids = new Dictionary<Entity, int>();

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceData>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
                ids[entity] = em.GetComponentData<ProvinceData>(entity).ProvinceId;

            return ids;
        }

        /// <summary>
        /// Un pays est désigné par son tag, jamais par son Entity : l'index d'une Entity est
        /// un détail d'allocation, pas une identité métier.
        /// </summary>
        static string TagOf(Dictionary<Entity, string> tags, Entity entity)
            => tags.TryGetValue(entity, out var tag) ? tag : NoCountry;

        static int ProvinceIdOf(Dictionary<Entity, int> ids, Entity entity)
            => ids.TryGetValue(entity, out var id) ? id : -1;

        static void HashWorldState(EntityManager em, ref StateHash hash)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<WorldState>());
            var world = query.GetSingleton<WorldState>();

            hash.Int(world.CurrentTick);
            hash.Int(world.Year);
            hash.Int(world.Month);
        }

        static void HashCountries(
            EntityManager em,
            Dictionary<Entity, string> tags,
            ref StateHash hash,
            bool includeCountryPopulation)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            var rows = new List<(string Tag, CountryData Country, Entity Entity)>(entities.Length);
            foreach (var entity in entities)
                rows.Add((TagOf(tags, entity), em.GetComponentData<CountryData>(entity), entity));

            rows.Sort((a, b) => string.CompareOrdinal(a.Tag, b.Tag));

            foreach (var row in rows)
            {
                hash.Text(row.Tag);
                // v1_090 : Population reste dans le digest de parité ; le variant de preuve
                // l'exclut pour montrer que RIEN d'autre n'a bougé.
                if (includeCountryPopulation)
                    hash.Int(row.Country.Population);
                hash.Float(row.Country.Prestige);
                hash.Float(row.Country.Industrialization);

                if (em.HasComponent<TreasuryData>(row.Entity))
                {
                    var treasury = em.GetComponentData<TreasuryData>(row.Entity);
                    hash.Float(treasury.Balance);
                    hash.Float(treasury.Income);
                    hash.Float(treasury.Expenses);
                    hash.Float(treasury.Debt);
                }

                if (em.HasComponent<GovernmentData>(row.Entity))
                {
                    var government = em.GetComponentData<GovernmentData>(row.Entity);
                    hash.Byte((byte)government.Type);
                    hash.Float(government.Legitimacy);
                    hash.Float(government.Stability);
                    hash.Float(government.Autonomy);
                }
            }
        }

        static void HashProvinces(EntityManager em, Dictionary<Entity, string> tags, ref StateHash hash)
        {
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceOwnership>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            var rows = new List<(int ProvinceId, string Owner, string Controller)>(entities.Length);
            foreach (var entity in entities)
            {
                var ownership = em.GetComponentData<ProvinceOwnership>(entity);
                rows.Add((
                    em.GetComponentData<ProvinceData>(entity).ProvinceId,
                    TagOf(tags, ownership.Owner),
                    TagOf(tags, ownership.Controller)));
            }

            rows.Sort((a, b) => a.ProvinceId.CompareTo(b.ProvinceId));

            foreach (var row in rows)
            {
                hash.Int(row.ProvinceId);
                hash.Text(row.Owner);
                hash.Text(row.Controller);
            }
        }

        static void HashPops(
            EntityManager em,
            Dictionary<Entity, string> tags,
            Dictionary<Entity, int> provinceIds,
            ref StateHash hash)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = query.ToComponentDataArray<PopData>(Allocator.Temp);

            var rows = new List<PopRow>(pops.Length);
            foreach (var pop in pops)
            {
                rows.Add(new PopRow
                {
                    ProvinceId = ProvinceIdOf(provinceIds, pop.Province),
                    CountryTag = TagOf(tags, pop.Country),
                    Type = (byte)pop.Type,
                    CultureTag = pop.CultureTag.ToString(),
                    Size = pop.Size,
                    NeedsSatisfaction = pop.NeedsSatisfaction,
                    Literacy = pop.Literacy,
                    Radicalism = pop.PoliticalRadicalism,
                });
            }

            rows.Sort(PopRow.Compare);

            foreach (var row in rows)
            {
                hash.Int(row.ProvinceId);
                hash.Text(row.CountryTag);
                hash.Byte(row.Type);
                hash.Text(row.CultureTag);
                hash.Int(row.Size);
                hash.Float(row.NeedsSatisfaction);
                hash.Float(row.Literacy);
                hash.Float(row.Radicalism);
            }
        }

        struct PopRow
        {
            public int ProvinceId;
            public string CountryTag;
            public byte Type;
            public string CultureTag;
            public int Size;
            public float NeedsSatisfaction;
            public float Literacy;
            public float Radicalism;

            public static int Compare(PopRow a, PopRow b)
            {
                var c = a.ProvinceId.CompareTo(b.ProvinceId);
                if (c != 0) return c;
                c = string.CompareOrdinal(a.CountryTag, b.CountryTag);
                if (c != 0) return c;
                c = a.Type.CompareTo(b.Type);
                if (c != 0) return c;
                c = string.CompareOrdinal(a.CultureTag, b.CultureTag);
                if (c != 0) return c;
                c = a.Size.CompareTo(b.Size);
                if (c != 0) return c;
                c = a.NeedsSatisfaction.CompareTo(b.NeedsSatisfaction);
                if (c != 0) return c;
                c = a.Literacy.CompareTo(b.Literacy);
                if (c != 0) return c;
                return a.Radicalism.CompareTo(b.Radicalism);
            }
        }

        static void HashArmies(EntityManager em, Dictionary<Entity, string> tags, ref StateHash hash)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = query.ToComponentDataArray<ArmyData>(Allocator.Temp);

            var rows = new List<(string Tag, ArmyData Army)>(armies.Length);
            foreach (var army in armies)
                rows.Add((TagOf(tags, army.Country), army));

            rows.Sort((a, b) =>
            {
                var c = string.CompareOrdinal(a.Tag, b.Tag);
                if (c != 0) return c;
                c = a.Army.ProvinceId.CompareTo(b.Army.ProvinceId);
                if (c != 0) return c;
                c = a.Army.Strength.CompareTo(b.Army.Strength);
                if (c != 0) return c;
                c = a.Army.Organization.CompareTo(b.Army.Organization);
                if (c != 0) return c;
                return a.Army.Morale.CompareTo(b.Army.Morale);
            });

            foreach (var row in rows)
            {
                hash.Text(row.Tag);
                hash.Int(row.Army.ProvinceId);
                hash.Float(row.Army.Strength);
                hash.Float(row.Army.Organization);
                hash.Float(row.Army.Morale);
                hash.Float(row.Army.SupplyLevel);
                hash.Bool(row.Army.IsEngaged);
            }
        }

        static void HashNavies(EntityManager em, Dictionary<Entity, string> tags, ref StateHash hash)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<NavyData>());
            using var navies = query.ToComponentDataArray<NavyData>(Allocator.Temp);

            var rows = new List<(string Tag, NavyData Navy)>(navies.Length);
            foreach (var navy in navies)
                rows.Add((TagOf(tags, navy.Country), navy));

            rows.Sort((a, b) =>
            {
                var c = string.CompareOrdinal(a.Tag, b.Tag);
                if (c != 0) return c;
                c = a.Navy.SeaZoneId.CompareTo(b.Navy.SeaZoneId);
                if (c != 0) return c;
                c = a.Navy.NavalStrength.CompareTo(b.Navy.NavalStrength);
                if (c != 0) return c;
                return a.Navy.NavalMorale.CompareTo(b.Navy.NavalMorale);
            });

            foreach (var row in rows)
            {
                hash.Text(row.Tag);
                hash.Int(row.Navy.SeaZoneId);
                hash.Float(row.Navy.NavalStrength);
                hash.Float(row.Navy.NavalMorale);
            }
        }
    }

    /// <summary>
    /// « Même seed → même résultat, toujours » (ARCHITECTURE.md). Ces deux tests sont
    /// indissociables : le premier prouve que la simulation est reproductible, le second
    /// qu'elle simule quelque chose. Un monde entièrement figé passerait le premier seul.
    /// </summary>
    [TestFixture]
    public class DeterminismTests
    {
        const int TickCount = 100;
        const uint SeedA = 42195u;
        const uint SeedB = 7919u;

        static ulong RunAndHash(uint seed, int ticks)
        {
            using var harness = new SimulationHarness(seed);
            harness.RunTicks(ticks);
            return WorldDigest.Compute(harness.EntityManager);
        }

        [Test]
        public void Determinism_SameSeedTwice_IdenticalFinalState()
        {
            var first = RunAndHash(SeedA, TickCount);
            var second = RunAndHash(SeedA, TickCount);

            Assert.AreEqual(first, second,
                $"Déterminisme rompu : deux runs de {TickCount} ticks avec la seed {SeedA} " +
                $"ont divergé (0x{first:X16} != 0x{second:X16}). Chercher une source d'aléa " +
                "non semée sur GlobalSeed, une agrégation dépendante de l'ordre des chunks, " +
                "ou un usage de UnityEngine.Random / DateTime.");
        }

        [Test]
        public void Determinism_DifferentSeeds_DivergentFinalState()
        {
            var withSeedA = RunAndHash(SeedA, TickCount);
            var withSeedB = RunAndHash(SeedB, TickCount);

            Assert.AreNotEqual(withSeedA, withSeedB,
                $"Les seeds {SeedA} et {SeedB} produisent le même état après {TickCount} ticks " +
                $"(0x{withSeedA:X16}). Soit l'aléa n'est pas semé sur GlobalSeed, soit aucun " +
                "système à aléa n'a d'effet observable — auquel cas le test de reproductibilité " +
                "ne prouve rien.");
        }

        [Test]
        public void Determinism_SeedOverride_ReachesWorldState()
        {
            using var harness = new SimulationHarness(SeedB);
            harness.RunTicks(0);

            using var query = harness.EntityManager.CreateEntityQuery(typeof(WorldState));
            Assert.AreEqual(SeedB, query.GetSingleton<WorldState>().GlobalSeed,
                "La seed forcée par le harnais doit atteindre WorldState.GlobalSeed, " +
                "sans quoi les deux tests ci-dessus compareraient deux runs de même seed.");
        }
    }
}
