using Unity.Entities;
using Unity.Collections;

namespace VictoriaGame.Core
{
    /// <summary>
    /// Singleton : pays contrôlé par le joueur (CountryId = rang countries.json).
    /// Défaut FRA = 0. Les intentions visant un autre pays sont refusées.
    /// </summary>
    public struct PlayerControl : IComponentData
    {
        /// <summary>FRA = rang 0 dans countries.json (ancre UI + tests).</summary>
        public const int DefaultControlledCountryId = 0;

        public int ControlledCountryId;
    }

    /// <summary>Marqueur singleton de la file d'intentions joueur.</summary>
    public struct PlayerIntentionQueueTag : IComponentData
    {
    }

    /// <summary>
    /// Kinds d'intention — extensible (construire, recruter, guerre…).
    /// Forme stable : Kind + CountryId + Value (+ ValueB réservé).
    /// </summary>
    public enum PlayerIntentionKind : byte
    {
        None = 0,
        SetProductionTaxRate = 1,
        /// <summary>Value = CityId, ValueB = BuildingType (cast float).</summary>
        StartBuildingConstruction = 2,
        /// <summary>
        /// Investissement développement provincial (v1_087).
        /// Value = ProvinceId, ValueB = axis (0=Tax, 1=Production, 2=Manpower).
        /// </summary>
        InvestProvinceDevelopment = 3,
        /// <summary>
        /// Déclaration de guerre (v1_088). Value = CountryId cible.
        /// </summary>
        DeclareWar = 4,
        /// <summary>
        /// Proposition de paix blanche (v1_088). Value = CountryId cible.
        /// </summary>
        ProposePeace = 5,
        /// <summary>
        /// Promulgation d'une loi (v1_089). TargetId = LawId (laws.json).
        /// Une loi en vigueur par catégorie — remplace si même catégorie.
        /// </summary>
        EnactLaw = 6
    }

    /// <summary>
    /// Élément de file : intention soumise par l'UI ou un test EditMode.
    /// L'interface n'écrit JAMAIS TaxPolicy / TreasuryData — elle enqueue ici.
    /// </summary>
    public struct PlayerIntention : IBufferElementData
    {
        public PlayerIntentionKind Kind;
        public int CountryId;
        public float Value;
        public float ValueB;
        public int SubmittedTick;
        /// <summary>Identifiant de domaine (LawId pour EnactLaw). Vide sinon.</summary>
        public FixedString32Bytes TargetId;
    }

    /// <summary>Dernier résultat d'application (lisible UI / tests, une seule entrée).</summary>
    public struct PlayerIntentionReceipt : IComponentData
    {
        public PlayerIntentionKind Kind;
        public int CountryId;
        public float Value;
        public int AppliedTick;
        public byte Accepted; // 1 = ok, 0 = refusé
        public FixedString64Bytes Reason;
        /// <summary>LawId (ou autre clé) de l'intention appliquée.</summary>
        public FixedString32Bytes TargetId;
    }

    /// <summary>
    /// API de soumission testable SANS interface.
    /// Déterministe : l'ordre d'enqueue est l'ordre d'application au prochain tick.
    /// </summary>
    public static class PlayerIntentionSubmit
    {
        public static readonly FixedString64Bytes ReasonAccepted = "accepted";
        public static readonly FixedString64Bytes ReasonRateOutOfBounds = "rate_out_of_bounds";
        public static readonly FixedString64Bytes ReasonCountryNotFound = "country_not_found";
        public static readonly FixedString64Bytes ReasonCountryNotControlled = "country_not_controlled";
        public static readonly FixedString64Bytes ReasonUnknownKind = "unknown_kind";
        public static readonly FixedString64Bytes ReasonNoQueue = "no_intention_queue";
        public static readonly FixedString64Bytes ReasonTargetUnknown = "target_unknown";
        public static readonly FixedString64Bytes ReasonTypeUnknown = "type_unknown";
        public static readonly FixedString64Bytes ReasonInsufficientTreasury = "insufficient_treasury";
        public static readonly FixedString64Bytes ReasonSlotOccupied = "slot_occupied";
        public static readonly FixedString64Bytes ReasonProvinceNotOwned = "province_not_owned";
        public static readonly FixedString64Bytes ReasonLevelAtCeiling = "level_at_ceiling";
        public static readonly FixedString64Bytes ReasonAxisUnknown = "axis_unknown";
        public static readonly FixedString64Bytes ReasonProvinceNotFound = "province_not_found";
        public static readonly FixedString64Bytes ReasonTargetIsSelf = "target_is_self";
        public static readonly FixedString64Bytes ReasonAlreadyAtWar = "already_at_war";
        public static readonly FixedString64Bytes ReasonNoActiveWar = "no_active_war";
        public static readonly FixedString64Bytes ReasonLawNotFound = "law_not_found";
        public static readonly FixedString64Bytes ReasonGovernmentTypeInsufficient = "government_type_insufficient";
        public static readonly FixedString64Bytes ReasonLawNotAvailable = "law_not_available";
        public static readonly FixedString64Bytes ReasonLawAlreadyEnacted = "law_already_enacted";

        public static bool TryGetQueueEntity(EntityManager em, out Entity queueEntity)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerIntentionQueueTag>());
            if (q.IsEmptyIgnoreFilter)
            {
                queueEntity = Entity.Null;
                return false;
            }

            queueEntity = q.GetSingletonEntity();
            return true;
        }

        /// <summary>Enqueue SetProductionTaxRate pour le pays donné (validé au tick suivant).</summary>
        public static bool EnqueueSetProductionTaxRate(
            EntityManager em,
            int countryId,
            float rate,
            int submittedTick = -1)
        {
            if (!TryGetQueueEntity(em, out var queueEntity))
                return false;

            var buffer = em.GetBuffer<PlayerIntention>(queueEntity);
            buffer.Add(new PlayerIntention
            {
                Kind = PlayerIntentionKind.SetProductionTaxRate,
                CountryId = countryId,
                Value = rate,
                ValueB = 0f,
                SubmittedTick = submittedTick
            });
            return true;
        }

        /// <summary>
        /// Enqueue StartBuildingConstruction. Value=CityId, ValueB=(float)BuildingType.
        /// L'UI n'écrit JAMAIS TreasuryData / BuildingData — uniquement la file.
        /// </summary>
        public static bool EnqueueStartBuildingConstruction(
            EntityManager em,
            int countryId,
            int cityId,
            BuildingType buildingType,
            int submittedTick = -1)
        {
            if (!TryGetQueueEntity(em, out var queueEntity))
                return false;

            var buffer = em.GetBuffer<PlayerIntention>(queueEntity);
            buffer.Add(new PlayerIntention
            {
                Kind = PlayerIntentionKind.StartBuildingConstruction,
                CountryId = countryId,
                Value = cityId,
                ValueB = (float)(int)buildingType,
                SubmittedTick = submittedTick
            });
            return true;
        }

        /// <summary>
        /// Enqueue InvestProvinceDevelopment. Value=ProvinceId, ValueB=axis (0/1/2).
        /// L'UI n'écrit JAMAIS ProvinceDevelopment / TreasuryData — uniquement la file.
        /// </summary>
        public static bool EnqueueInvestProvinceDevelopment(
            EntityManager em,
            int countryId,
            int provinceId,
            byte axis,
            int submittedTick = -1)
        {
            if (!TryGetQueueEntity(em, out var queueEntity))
                return false;

            var buffer = em.GetBuffer<PlayerIntention>(queueEntity);
            buffer.Add(new PlayerIntention
            {
                Kind = PlayerIntentionKind.InvestProvinceDevelopment,
                CountryId = countryId,
                Value = provinceId,
                ValueB = axis,
                SubmittedTick = submittedTick
            });
            return true;
        }

        /// <summary>
        /// Enqueue DeclareWar. Value = CountryId cible.
        /// L'UI n'écrit JAMAIS WarData — uniquement la file.
        /// </summary>
        public static bool EnqueueDeclareWar(
            EntityManager em,
            int countryId,
            int targetCountryId,
            int submittedTick = -1)
        {
            if (!TryGetQueueEntity(em, out var queueEntity))
                return false;

            var buffer = em.GetBuffer<PlayerIntention>(queueEntity);
            buffer.Add(new PlayerIntention
            {
                Kind = PlayerIntentionKind.DeclareWar,
                CountryId = countryId,
                Value = targetCountryId,
                ValueB = 0f,
                SubmittedTick = submittedTick
            });
            return true;
        }

        /// <summary>
        /// Enqueue ProposePeace. Value = CountryId cible.
        /// L'UI n'écrit JAMAIS WarData — uniquement la file.
        /// </summary>
        public static bool EnqueueProposePeace(
            EntityManager em,
            int countryId,
            int targetCountryId,
            int submittedTick = -1)
        {
            if (!TryGetQueueEntity(em, out var queueEntity))
                return false;

            var buffer = em.GetBuffer<PlayerIntention>(queueEntity);
            buffer.Add(new PlayerIntention
            {
                Kind = PlayerIntentionKind.ProposePeace,
                CountryId = countryId,
                Value = targetCountryId,
                ValueB = 0f,
                SubmittedTick = submittedTick,
                TargetId = default
            });
            return true;
        }

        /// <summary>
        /// Enqueue EnactLaw. TargetId = LawId (laws.json).
        /// L'UI n'écrit JAMAIS EnactedLaw — uniquement la file.
        /// </summary>
        public static bool EnqueueEnactLaw(
            EntityManager em,
            int countryId,
            FixedString32Bytes lawId,
            int submittedTick = -1)
        {
            if (!TryGetQueueEntity(em, out var queueEntity))
                return false;

            var buffer = em.GetBuffer<PlayerIntention>(queueEntity);
            buffer.Add(new PlayerIntention
            {
                Kind = PlayerIntentionKind.EnactLaw,
                CountryId = countryId,
                Value = 0f,
                ValueB = 0f,
                SubmittedTick = submittedTick,
                TargetId = lawId
            });
            return true;
        }

        public static bool EnqueueEnactLaw(
            EntityManager em,
            int countryId,
            string lawId,
            int submittedTick = -1)
        {
            return EnqueueEnactLaw(
                em, countryId, new FixedString32Bytes(lawId ?? string.Empty), submittedTick);
        }
    }
}
