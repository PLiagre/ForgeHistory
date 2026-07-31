using Unity.Collections;
using Unity.Entities;
using VictoriaGame.Core;
using VictoriaGame.Population;
using VictoriaGame.World;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Coût fiscal lisible à côté du levier (v1_086) : satisfaction et affamés
    /// du pays visualisé. Seuil affamé = NeedsSatisfaction &lt; 0,5 (même que
    /// les mesures v1_075 / v1_086).
    /// </summary>
    public static class TaxCostSnapshot
    {
        public const float HungryThreshold = 0.5f;

        /// <summary>
        /// Sat moyenne pondérée Size, pops affamées, provinces distinctes affamées
        /// pour le pays <paramref name="countryId"/>. Si countryId &lt; 0 : monde entier.
        /// </summary>
        public static void Capture(
            EntityManager em,
            int countryId,
            out float satAvg,
            out int hungryPops,
            out int hungryProvinces)
        {
            satAvg = 0f;
            hungryPops = 0;
            hungryProvinces = 0;

            var filterCountry = countryId >= 0;
            double weighted = 0.0;
            double totalSize = 0.0;
            var hungryProvIds = new NativeHashSet<int>(64, Allocator.Temp);

            using (var pq = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>()))
            using (var pops = pq.ToComponentDataArray<PopData>(Allocator.Temp))
            {
                for (var i = 0; i < pops.Length; i++)
                {
                    var p = pops[i];
                    if (filterCountry)
                    {
                        if (p.Country == Entity.Null || !em.Exists(p.Country))
                            continue;
                        if (!em.HasComponent<CountryData>(p.Country))
                            continue;
                        if (em.GetComponentData<CountryData>(p.Country).CountryId != countryId)
                            continue;
                    }

                    weighted += p.NeedsSatisfaction * p.Size;
                    totalSize += p.Size;
                    if (p.NeedsSatisfaction < HungryThreshold)
                    {
                        hungryPops++;
                        var pid = -1;
                        if (p.Province != Entity.Null &&
                            em.Exists(p.Province) &&
                            em.HasComponent<ProvinceData>(p.Province))
                        {
                            pid = em.GetComponentData<ProvinceData>(p.Province).ProvinceId;
                        }

                        if (pid >= 0)
                            hungryProvIds.Add(pid);
                    }
                }
            }

            hungryProvinces = hungryProvIds.Count;
            satAvg = totalSize > 0.0 ? (float)(weighted / totalSize) : 0f;
            hungryProvIds.Dispose();
        }

        /// <summary>Ligne HUD (sat + affamés) — même format que InGameHud.</summary>
        public static string FormatHudLine(float sat, int hungryPops, int hungryProvinces)
        {
            return "Sat " + HudValueFormatter.FormatNumber(sat, "0.000") +
                   "  ·  Affamés " + hungryPops.ToString() +
                   "  ·  Prov. affamées " + hungryProvinces.ToString();
        }
    }
}
