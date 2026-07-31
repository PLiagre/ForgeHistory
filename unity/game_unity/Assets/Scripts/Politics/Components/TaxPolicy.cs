using Unity.Entities;

namespace VictoriaGame.Politics
{
    /// <summary>
    /// Politique fiscale par pays (v1_035) — taux d'impôt sur la production.
    /// Remplace la constante de compilation <c>TaxSystem.ProductionTaxRate</c> :
    /// chaque pays porte son propre taux, réglable via intention joueur.
    ///
    /// BIT-IDENTITÉ : à <see cref="DefaultProductionTaxRate"/> le monde doit reproduire
    /// exactement l'ancien taux fixe 0.00002f (parité v1_009, digests inchangés).
    ///
    /// IA (EXPLICITE) : les pays non contrôlés par le joueur CONSERVENT le taux par défaut
    /// pour toute la partie (<see cref="TaxAiPolicy.HoldDefault"/>). Aucun ajustement
    /// automatique — un comportement implicite est interdit par le brief.
    ///
    /// BORNES (justifiées par mesure seed 42195, Logs/v1_035_tax_sweep.log) :
    /// - Min = 0 : à 0× le revenu de production disparaît → dette / banqueroutes ↑.
    /// - Max = 10× défaut (0.0002) : enveloppe d'exploration ; au-delà le trésor
    ///   mondial explose et la stabilité long-horizon se dégrade (seuil affiné
    ///   dans le log de balayage, sans retoucher la calibration v1_015→v1_018).
    /// </summary>
    public struct TaxPolicy : IComponentData
    {
        /// <summary>Taux sur priceEff × LastOutput (même unité que l'ex-constante TaxSystem).</summary>
        public float ProductionTaxRate;
    }

    /// <summary>Bornes et défaut de la politique fiscale — source unique pour simu + UI + tests.</summary>
    public static class TaxPolicyLimits
    {
        /// <summary>Valeur historique (constante TaxSystem pré-v1_035) — ancre bit-identique.</summary>
        public const float DefaultProductionTaxRate = 0.00002f;

        /// <summary>
        /// Plancher mesuré (seed 42195, t3000, Logs/v1_035_tax_sweep.log) :
        /// à 0× → debt=13205, bankrupt=15, army=194 (effondrement).
        /// À 0.5× le monde reste vivable (army≈17k, bankrupt=3).
        /// </summary>
        public const float MinProductionTaxRate = 0f;

        /// <summary>
        /// Plafond = 10 × défaut. Mesure t3000 : ×10 reste vivable
        /// (army≈10.9k, zombie=0, debt=450, bankrupt=1) — enveloppe entière OK.
        /// Au-delà la simulation refuse (intention rate_out_of_bounds).
        /// </summary>
        public const float MaxProductionTaxRate = DefaultProductionTaxRate * 10f;

        /// <summary>Pas UI / intention discrète (±1 cran = 0.5× défaut).</summary>
        public const float UiStep = DefaultProductionTaxRate * 0.5f;

        public static bool IsInBounds(float rate) =>
            rate >= MinProductionTaxRate && rate <= MaxProductionTaxRate;

        public static float Clamp(float rate)
        {
            if (rate < MinProductionTaxRate)
                return MinProductionTaxRate;
            if (rate > MaxProductionTaxRate)
                return MaxProductionTaxRate;
            return rate;
        }

        public static TaxPolicy Default() => new TaxPolicy
        {
            ProductionTaxRate = DefaultProductionTaxRate
        };
    }

    /// <summary>
    /// Politique IA fiscale — EXPLICITE. HoldDefault = les IA ne touchent jamais au taux.
    /// </summary>
    public enum TaxAiPolicy : byte
    {
        /// <summary>Conserve <see cref="TaxPolicyLimits.DefaultProductionTaxRate"/> indéfiniment.</summary>
        HoldDefault = 0
    }

    /// <summary>Constantes documentaires de la politique IA (pas d'état mutable).</summary>
    public static class TaxAiPolicyConfig
    {
        public const TaxAiPolicy Mode = TaxAiPolicy.HoldDefault;
    }
}
