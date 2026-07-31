using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using VictoriaGame.Core;
using VictoriaGame.Politics;
using VictoriaGame.World;

namespace VictoriaGame.Population
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(MapInitSystem))]
    public partial struct PopInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (data, dev, ownership, province) in
                     SystemAPI.Query<RefRO<ProvinceData>, RefRO<ProvinceDevelopment>, RefRO<ProvinceOwnership>>()
                         .WithEntityAccess())
            {
                var provinceData = data.ValueRO;
                var development = dev.ValueRO;
                var provinceOwnership = ownership.ValueRO;
                int basePopSize = development.Manpower * 1000;

                var peasant = ecb.CreateEntity();
                ecb.AddComponent(peasant, new PopData
                {
                    Type = PopType.Peasant,
                    Size = (int)(basePopSize * 0.80f),
                    Province = province,
                    Country = provinceOwnership.Owner,
                    CultureTag = provinceData.CultureTag,
                    ReligionTag = provinceData.ReligionTag,
                    Literacy = 0.05f,
                    NeedsSatisfaction = 0.5f,
                    PoliticalRadicalism = 0f,
                    BirthTick = 0,
                });
                ecb.AddComponent(peasant, new PopNeeds
                {
                    FoodNeed = basePopSize * 0.80f * 0.001f,
                    ClothNeed = basePopSize * 0.80f * 0.0002f,
                    LuxuryNeed = 0f,
                });
                ecb.AddComponent(peasant, new PopPolitics
                {
                    Ideology = IdeologyType.Conservatism,
                    Radicalism = 0f,
                    Loyalty = 0.5f,
                    PoliticalPower = 0f,
                    LastUnrestTick = 0,
                });

                var noble = ecb.CreateEntity();
                ecb.AddComponent(noble, new PopData
                {
                    Type = PopType.Noble,
                    Size = (int)(basePopSize * 0.05f),
                    Province = province,
                    Country = provinceOwnership.Owner,
                    CultureTag = provinceData.CultureTag,
                    ReligionTag = provinceData.ReligionTag,
                    Literacy = 0.5f,
                    NeedsSatisfaction = 0.7f,
                    PoliticalRadicalism = 0f,
                    BirthTick = 0,
                });
                ecb.AddComponent(noble, new PopNeeds
                {
                    FoodNeed = basePopSize * 0.05f * 0.001f,
                    ClothNeed = basePopSize * 0.05f * 0.0005f,
                    LuxuryNeed = basePopSize * 0.05f * 0.0003f,
                });
                ecb.AddComponent(noble, new PopPolitics
                {
                    Ideology = IdeologyType.Conservatism,
                    Radicalism = 0f,
                    Loyalty = 0.5f,
                    PoliticalPower = 0f,
                    LastUnrestTick = 0,
                });

                if (provinceData.IsCoastal || development.Production >= 3)
                {
                    var artisan = ecb.CreateEntity();
                    ecb.AddComponent(artisan, new PopData
                    {
                        Type = PopType.Artisan,
                        Size = (int)(basePopSize * 0.15f),
                        Province = province,
                        Country = provinceOwnership.Owner,
                        CultureTag = provinceData.CultureTag,
                        ReligionTag = provinceData.ReligionTag,
                        Literacy = 0.2f,
                        NeedsSatisfaction = 0.5f,
                        PoliticalRadicalism = 0f,
                        BirthTick = 0,
                    });
                    ecb.AddComponent(artisan, new PopNeeds
                    {
                        FoodNeed = basePopSize * 0.15f * 0.001f,
                        ClothNeed = basePopSize * 0.15f * 0.0003f,
                        LuxuryNeed = basePopSize * 0.15f * 0.0001f,
                    });
                    ecb.AddComponent(artisan, new PopPolitics
                    {
                        Ideology = IdeologyType.Conservatism,
                        Radicalism = 0f,
                        Loyalty = 0.5f,
                        PoliticalPower = 0f,
                        LastUnrestTick = 0,
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
