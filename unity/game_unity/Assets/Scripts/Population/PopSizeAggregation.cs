using Unity.Entities;
using Unity.Collections;
using System.Collections.Generic;
using VictoriaGame.Core;

namespace VictoriaGame.Population
{
    /// <summary>
    /// Agrégation canonique de <see cref="PopData.Size"/> — source unique pour
    /// <see cref="VictoriaGame.Presentation.WorldMetrics"/> (population mondiale)
    /// et pour l'écriture de <see cref="CountryData.Population"/> (v1_090).
    /// Clés de domaine stables (<see cref="CountryData.CountryId"/>), jamais Entity.Index.
    /// </summary>
    public static class PopSizeAggregation
    {
        /// <summary>Σ PopData.Size sur toutes les pops (définition WorldMetrics).</summary>
        public static int SumAll(EntityManager em)
        {
            SumAllWithWeightedSatisfaction(em, out var totalPop, out _);
            return totalPop;
        }

        /// <summary>
        /// Même boucle que l'ancien WorldMetrics.CapturePopulation : total + Σ(sat×size).
        /// </summary>
        public static void SumAllWithWeightedSatisfaction(
            EntityManager em, out int totalPop, out double weightedSat)
        {
            totalPop = 0;
            weightedSat = 0.0;
            using var popQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = popQuery.ToComponentDataArray<PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                totalPop += pops[i].Size;
                weightedSat += pops[i].NeedsSatisfaction * pops[i].Size;
            }
        }

        /// <summary>
        /// Σ Size des pops dont Country est Null ou sans CountryData (écart vs SumAll).
        /// </summary>
        public static int SumOrphan(EntityManager em)
        {
            var orphan = 0;
            using var popQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = popQuery.ToComponentDataArray<PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                var country = pops[i].Country;
                if (country == Entity.Null || !em.HasComponent<CountryData>(country))
                    orphan += pops[i].Size;
            }

            return orphan;
        }

        /// <summary>Σ CountryData.Population (après écriture).</summary>
        public static int SumCountryFields(EntityManager em)
        {
            var total = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var countries = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < countries.Length; i++)
                total += countries[i].Population;
            return total;
        }

        /// <summary>
        /// Écrit <c>CountryData.Population = Σ PopData.Size</c> du pays.
        /// Accumulation par CountryId ; écriture en ordre CountryId croissant.
        /// </summary>
        public static void WriteCountryPopulations(EntityManager em)
        {
            var sums = new NativeHashMap<int, int>(64, Allocator.Temp);
            using (var popQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>()))
            using (var pops = popQuery.ToComponentDataArray<PopData>(Allocator.Temp))
            {
                for (var i = 0; i < pops.Length; i++)
                {
                    var country = pops[i].Country;
                    if (country == Entity.Null || !em.HasComponent<CountryData>(country))
                        continue;

                    var countryId = em.GetComponentData<CountryData>(country).CountryId;
                    sums.TryGetValue(countryId, out var current);
                    sums[countryId] = current + pops[i].Size;
                }
            }

            using var countryQuery = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var entities = countryQuery.ToEntityArray(Allocator.Temp);
            var rows = new List<(int CountryId, Entity Entity)>(entities.Length);
            for (var i = 0; i < entities.Length; i++)
            {
                var cd = em.GetComponentData<CountryData>(entities[i]);
                rows.Add((cd.CountryId, entities[i]));
            }

            rows.Sort((a, b) => a.CountryId.CompareTo(b.CountryId));

            for (var i = 0; i < rows.Count; i++)
            {
                var entity = rows[i].Entity;
                var cd = em.GetComponentData<CountryData>(entity);
                sums.TryGetValue(cd.CountryId, out var population);
                cd.Population = population;
                em.SetComponentData(entity, cd);
            }

            sums.Dispose();
        }
    }
}
