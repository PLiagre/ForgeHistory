using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace VictoriaGame.Core
{
    /// <summary>
    /// Clés de domaine stables — jamais Entity.Index (artefact d'allocation ECS).
    /// Convention unique : <see cref="CountryData.CountryId"/> = rang 0-based dans countries.json.
    /// HashTag hashe les octets du Tag (pas string.GetHashCode) pour les preuves / utilitaires.
    /// </summary>
    public static class DomainKeys
    {
        /// <summary>
        /// Constante de régression : HashTag("FRA") doit rester égale à cette valeur
        /// entre exécutions et plateformes (preuve de stabilité du hash d'octets).
        /// </summary>
        public const uint ExpectedHashFra = 76402792u;

        /// <summary>
        /// Hash déterministe des octets du Tag (longueur + jusqu'à 12 premiers octets).
        /// Interdit : string.GetHashCode() — non garanti stable.
        /// </summary>
        public static uint HashTag(in FixedString32Bytes tag)
        {
            uint b0 = 0;
            uint b1 = 0;
            uint b2 = 0;
            var len = tag.Length;
            var n = len < 12 ? len : 12;
            for (var i = 0; i < n; i++)
            {
                var shift = (i % 4) * 8;
                var b = (uint)tag[i];
                if (i < 4)
                {
                    b0 |= b << shift;
                }
                else if (i < 8)
                {
                    b1 |= b << shift;
                }
                else
                {
                    b2 |= b << shift;
                }
            }

            return math.hash(new uint4(b0, b1, b2, (uint)len));
        }

        /// <summary>
        /// Clé de dictionnaire stable et injective pour (ZoneId, CountryId).
        /// Remplace ZoneId ^ Entity.Index (instable + collisions possibles).
        /// </summary>
        public static int HashZoneCountry(int zoneId, int countryId)
        {
            return (int)math.hash(new int2(zoneId, countryId));
        }

        /// <summary>
        /// Ordre total armées : CountryId, puis ProvinceId.
        /// Avec une armée par pays (MilitaryInit), CountryId seul est unique ;
        /// ProvinceId départage si plusieurs armées d'un même pays existent un jour.
        /// </summary>
        public static int CompareArmyKeys(
            int countryIdA, int provinceIdA,
            int countryIdB, int provinceIdB)
        {
            var c = countryIdA.CompareTo(countryIdB);
            if (c != 0)
            {
                return c;
            }

            return provinceIdA.CompareTo(provinceIdB);
        }

        /// <summary>
        /// Ordre total flottes : CountryId, puis SeaZoneId.
        /// Une flotte par pays (NavalRecruitment) ⇒ CountryId unique.
        /// </summary>
        public static int CompareNavyKeys(
            int countryIdA, int seaZoneIdA,
            int countryIdB, int seaZoneIdB)
        {
            var c = countryIdA.CompareTo(countryIdB);
            if (c != 0)
            {
                return c;
            }

            return seaZoneIdA.CompareTo(seaZoneIdB);
        }
    }
}
