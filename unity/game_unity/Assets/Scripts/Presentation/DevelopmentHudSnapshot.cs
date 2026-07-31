using Unity.Collections;
using Unity.Entities;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Lecture HUD du développement provincial (v1_087) — aucune écriture monde.
    /// </summary>
    public static class DevelopmentHudSnapshot
    {
        public static bool TryCapture(
            EntityManager em,
            int provinceId,
            out ProvinceDevelopment dev,
            out float costTax,
            out float costProd,
            out float costMan,
            out bool ownedByPlayer)
        {
            dev = default;
            costTax = costProd = costMan = 0f;
            ownedByPlayer = false;

            if (provinceId < 0 || !em.World.IsCreated)
                return false;

            Entity provinceEntity = Entity.Null;
            ProvinceOwnership ownership = default;
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceDevelopment>(),
                       ComponentType.ReadOnly<ProvinceOwnership>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            using (var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            using (var devs = q.ToComponentDataArray<ProvinceDevelopment>(Allocator.Temp))
            using (var owns = q.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp))
            {
                for (var i = 0; i < pdata.Length; i++)
                {
                    if (pdata[i].ProvinceId != provinceId)
                        continue;
                    provinceEntity = entities[i];
                    dev = devs[i];
                    ownership = owns[i];
                    break;
                }
            }

            if (provinceEntity == Entity.Null)
                return false;

            ownedByPlayer = IsOwnedByControlled(em, ownership.Owner);
            costTax = ProvinceDevelopmentInvestment.CostForLevel(dev.Tax);
            costProd = ProvinceDevelopmentInvestment.CostForLevel(dev.Production);
            costMan = ProvinceDevelopmentInvestment.CostForLevel(dev.Manpower);
            return true;
        }

        public static string FormatHudLine(in ProvinceDevelopment dev) =>
            "DEV T" + dev.Tax + " P" + dev.Production + " M" + dev.Manpower +
            "  score=" + ProvinceDevelopmentInvestment.DevScore(in dev).ToString("0.##");

        static bool IsOwnedByControlled(EntityManager em, Entity owner)
        {
            if (owner == Entity.Null || !em.HasComponent<CountryData>(owner))
                return false;

            var controlledId = PlayerControl.DefaultControlledCountryId;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerControl>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                    controlledId = q.GetSingleton<PlayerControl>().ControlledCountryId;
            }

            return em.GetComponentData<CountryData>(owner).CountryId == controlledId;
        }
    }
}
