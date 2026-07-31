using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Modes de l'État croupion (dip_007) : empêche l'annexion totale d'un belligérant
    /// qui possède encore au moins une province avant le traité.
    /// </summary>
    public enum RumpStateMode : byte
    {
        /// <summary>Comportement pré-dip_007 (uti possidetis intégral) — ancrage dip_006.</summary>
        Disabled = 0,
        /// <summary>Un vaincu conserve toujours au moins une province.</summary>
        Always = 1,
        /// <summary>
        /// Croupion sauf si la capitale du vaincu est occupée par l'ennemi :
        /// l'annexion totale est alors autorisée.
        /// </summary>
        UnlessCapitalOccupied = 2
    }

    /// <summary>
    /// Conclut les guerres par victoire (annexion uti possidetis) ou par épuisement (paix blanche,
    /// restitution des territoires occupés) et désengage les armées des belligérants.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SiegeSystem))]
    public partial struct PeaceSystem : ISystem
    {
        private const float PEACE_THRESHOLD = 60f;
        private const int WAR_EXHAUSTION_TICKS = 150;

        /// <summary>Mode retenu en production (calibré dip_007, seed 42195).</summary>
        public const RumpStateMode DefaultRumpStateMode = RumpStateMode.UnlessCapitalOccupied;

        /// <summary>Mutable pour mesures A/B ; production = <see cref="DefaultRumpStateMode"/>.</summary>
        public static RumpStateMode Mode = DefaultRumpStateMode;

        /// <summary>Compteur mesure : pays sauvés de l'élimination (ConcludePeace).</summary>
        public static int RumpStatesCreated;

        /// <summary>
        /// Compteur mesure / QA : province épargnée encore Controller != Owner après libération
        /// (doit rester 0).
        /// </summary>
        public static int SparedProvincesLeftOccupied;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<WorldState>())
            {
                return;
            }

            var worldState = SystemAPI.GetSingleton<WorldState>();
            if (worldState.IsPaused)
            {
                return;
            }

            var currentTick = worldState.CurrentTick;
            var warsForVictory = new NativeList<Entity>(4, Allocator.Temp);
            var warsForWhitePeace = new NativeList<Entity>(4, Allocator.Temp);

            foreach (var (war, warEntity) in SystemAPI.Query<RefRO<WarData>>().WithEntityAccess())
            {
                if (!war.ValueRO.IsActive)
                {
                    continue;
                }

                if (math.abs(war.ValueRO.WarScore) >= PEACE_THRESHOLD)
                {
                    warsForVictory.Add(warEntity);
                    continue;
                }

                if (currentTick - war.ValueRO.StartTick > WAR_EXHAUSTION_TICKS)
                {
                    warsForWhitePeace.Add(warEntity);
                }
            }

            for (var i = 0; i < warsForVictory.Length; i++)
            {
                ConcludePeace(ref state, warsForVictory[i]);
            }

            for (var i = 0; i < warsForWhitePeace.Length; i++)
            {
                ConcludeWhitePeace(ref state, warsForWhitePeace[i]);
            }

            warsForVictory.Dispose();
            warsForWhitePeace.Dispose();

            // dip_008 / world_013 : intégration post-paix via helper statique (pas d'ISystem).
            ProvinceIntegration.IntegrateProvinces(
                state.EntityManager,
                currentTick);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        /// <summary>
        /// v1_088 — paix blanche proposée par le joueur. Même chemin que
        /// <see cref="ConcludeWhitePeace"/> (IsActive=false, EndTick, restitution
        /// Controller→Owner, désengagement armées). Aucun seuil modifié.
        /// </summary>
        public static bool TryConcludePlayerWhitePeace(
            EntityManager em,
            Entity warEntity,
            int currentTick)
        {
            if (warEntity == Entity.Null || !em.Exists(warEntity) || !em.HasComponent<WarData>(warEntity))
                return false;

            var war = em.GetComponentData<WarData>(warEntity);
            if (!war.IsActive)
                return false;

            var attacker = war.Attacker;
            var defender = war.Defender;
            war.IsActive = false;
            war.EndTick = currentTick;
            em.SetComponentData(warEntity, war);

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadWrite<ProvinceOwnership>()))
            using (var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var ownership = em.GetComponentData<ProvinceOwnership>(entities[i]);
                    if (ownership.Controller == ownership.Owner)
                        continue;
                    if (ownership.Owner != attacker && ownership.Owner != defender)
                        continue;

                    ownership.Controller = ownership.Owner;
                    em.SetComponentData(entities[i], ownership);
                    RestoreFortsInProvinceStatic(em, pdata[i].ProvinceId, ownership.Owner);
                }
            }

            DisengageBelligerentArmiesStatic(em, attacker, defender);
            return true;
        }

        static void RestoreFortsInProvinceStatic(EntityManager em, int provinceId, Entity ownerCountry)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadWrite<FortData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var fort = em.GetComponentData<FortData>(entities[i]);
                if (fort.ProvinceId != provinceId)
                    continue;
                fort.OwnerCountry = ownerCountry;
                fort.IsUnderSiege = false;
                fort.SiegeProgress = 0f;
                em.SetComponentData(entities[i], fort);
            }
        }

        static void DisengageBelligerentArmiesStatic(EntityManager em, Entity attacker, Entity defender)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadWrite<ArmyData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var army = em.GetComponentData<ArmyData>(entities[i]);
                if (!army.IsEngaged)
                    continue;
                if (army.Country != attacker && army.Country != defender)
                    continue;
                army.IsEngaged = false;
                em.SetComponentData(entities[i], army);
            }
        }

        private void ConcludePeace(ref SystemState state, Entity warEntity)
        {
            var war = state.EntityManager.GetComponentData<WarData>(warEntity);
            var attacker = war.Attacker;
            var defender = war.Defender;

            var worldState = SystemAPI.GetSingleton<WorldState>();
            war.IsActive = false;
            war.EndTick = worldState.CurrentTick;
            state.EntityManager.SetComponentData(warEntity, war);

            var mode = Mode;
            var spareAttacker = -1;
            var spareDefender = -1;
            if (mode != RumpStateMode.Disabled)
            {
                spareAttacker = ChooseSpareProvinceId(ref state, attacker, defender, mode);
                spareDefender = ChooseSpareProvinceId(ref state, defender, attacker, mode);
            }

            foreach (var (provinceData, ownershipRef) in
                     SystemAPI.Query<RefRO<ProvinceData>, RefRW<ProvinceOwnership>>())
            {
                var ownership = ownershipRef.ValueRO;
                if (ownership.Controller == ownership.Owner)
                {
                    continue;
                }

                if (ownership.Owner != attacker && ownership.Owner != defender)
                {
                    continue;
                }

                var provinceId = provinceData.ValueRO.ProvinceId;
                var isSpared =
                    (ownership.Owner == attacker && provinceId == spareAttacker) ||
                    (ownership.Owner == defender && provinceId == spareDefender);

                if (isSpared)
                {
                    // Libérer l'occupation : sinon ré-annexion au traité suivant + WarScore dip_005.
                    ownership.Controller = ownership.Owner;
                    ownershipRef.ValueRW = ownership;
                    if (ownershipRef.ValueRO.Controller != ownershipRef.ValueRO.Owner)
                    {
                        SparedProvincesLeftOccupied++;
                    }

                    continue;
                }

                // Uti possidetis : Owner suit le Controller ; horodatage pour dip_008.
                ownership.Owner = ownership.Controller;
                ownership.OwnerChangedTick = worldState.CurrentTick;
                ownershipRef.ValueRW = ownership;
            }

            DisengageBelligerentArmies(ref state, attacker, defender);
        }

        /// <summary>
        /// Province épargnée déterministe si le pays tomberait à zéro après uti possidetis.
        /// -1 = aucune (Disabled, déjà à zéro, resterait &gt;0, ou capitale occupée en mode Unless).
        /// Non-static : SystemAPI.Query exige l'instance du système (source generator DOTS).
        /// </summary>
        private int ChooseSpareProvinceId(
            ref SystemState state,
            Entity country,
            Entity enemy,
            RumpStateMode mode)
        {
            if (country == Entity.Null || mode == RumpStateMode.Disabled)
            {
                return -1;
            }

            var capitalId = CountryInitSystem.InvalidCapitalProvinceId;
            if (state.EntityManager.Exists(country) &&
                state.EntityManager.HasComponent<CountryData>(country))
            {
                capitalId = state.EntityManager.GetComponentData<CountryData>(country).CapitalProvinceId;
            }

            var ownedBefore = 0;
            var remainingAfter = 0;
            var capitalOccupiedByEnemy = false;
            var lostProvinceIds = new NativeList<int>(8, Allocator.Temp);

            foreach (var (provinceData, ownership) in
                     SystemAPI.Query<RefRO<ProvinceData>, RefRO<ProvinceOwnership>>())
            {
                var owner = ownership.ValueRO.Owner;
                var controller = ownership.ValueRO.Controller;
                var provinceId = provinceData.ValueRO.ProvinceId;

                if (owner == country)
                {
                    ownedBefore++;
                    if (controller == country)
                    {
                        remainingAfter++;
                    }
                    else
                    {
                        lostProvinceIds.Add(provinceId);
                        if (controller == enemy && provinceId == capitalId)
                        {
                            capitalOccupiedByEnemy = true;
                        }
                    }
                }
                else if (owner == enemy && controller == country)
                {
                    // Annexion reçue via uti possidetis.
                    remainingAfter++;
                }
            }

            if (ownedBefore <= 0 || remainingAfter > 0 || lostProvinceIds.Length == 0)
            {
                lostProvinceIds.Dispose();
                return -1;
            }

            if (mode == RumpStateMode.UnlessCapitalOccupied && capitalOccupiedByEnemy)
            {
                lostProvinceIds.Dispose();
                return -1;
            }

            // Préférence déterministe : (1) capitale si perdue, (2) plus petit ProvinceId.
            var spareId = -1;
            if (capitalId > 0)
            {
                for (var i = 0; i < lostProvinceIds.Length; i++)
                {
                    if (lostProvinceIds[i] == capitalId)
                    {
                        spareId = capitalId;
                        break;
                    }
                }
            }

            if (spareId < 0)
            {
                spareId = lostProvinceIds[0];
                for (var i = 1; i < lostProvinceIds.Length; i++)
                {
                    if (lostProvinceIds[i] < spareId)
                    {
                        spareId = lostProvinceIds[i];
                    }
                }
            }

            lostProvinceIds.Dispose();
            RumpStatesCreated++;
            return spareId;
        }

        private void ConcludeWhitePeace(ref SystemState state, Entity warEntity)
        {
            var war = state.EntityManager.GetComponentData<WarData>(warEntity);
            var attacker = war.Attacker;
            var defender = war.Defender;

            var worldState = SystemAPI.GetSingleton<WorldState>();
            war.IsActive = false;
            war.EndTick = worldState.CurrentTick;
            state.EntityManager.SetComponentData(warEntity, war);

            foreach (var (provinceData, ownershipRef) in SystemAPI.Query<RefRO<ProvinceData>, RefRW<ProvinceOwnership>>())
            {
                var ownership = ownershipRef.ValueRO;
                if (ownership.Controller == ownership.Owner)
                {
                    continue;
                }

                if (ownership.Owner != attacker && ownership.Owner != defender)
                {
                    continue;
                }

                ownership.Controller = ownership.Owner;
                ownershipRef.ValueRW = ownership;

                RestoreFortsInProvince(ref state, provinceData.ValueRO.ProvinceId, ownership.Owner);
            }

            DisengageBelligerentArmies(ref state, attacker, defender);
        }

        private void RestoreFortsInProvince(ref SystemState state, int provinceId, Entity ownerCountry)
        {
            foreach (var fortRef in SystemAPI.Query<RefRW<FortData>>())
            {
                var fort = fortRef.ValueRO;
                if (fort.ProvinceId != provinceId)
                {
                    continue;
                }

                fort.OwnerCountry = ownerCountry;
                fort.IsUnderSiege = false;
                fort.SiegeProgress = 0f;
                fortRef.ValueRW = fort;
            }
        }

        private void DisengageBelligerentArmies(ref SystemState state, Entity attacker, Entity defender)
        {
            foreach (var armyRef in SystemAPI.Query<RefRW<ArmyData>>())
            {
                var army = armyRef.ValueRO;
                if (!army.IsEngaged)
                {
                    continue;
                }

                if (army.Country == attacker || army.Country == defender)
                {
                    army.IsEngaged = false;
                    armyRef.ValueRW = army;
                }
            }
        }
    }
}
