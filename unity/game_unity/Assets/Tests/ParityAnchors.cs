namespace VictoriaGame.Tests
{
    /// <summary>
    /// Ancres de parité v1_009. v1_090 rebase <see cref="Expected"/> parce que
    /// CountryData.Population (haché) passe de 0 gelé à Σ PopData.Size vivant.
    /// L'ancienne empreinte reste <see cref="PreV1090FrozenPopulation"/> pour la
    /// réversibilité (CountryPopulationAggregationInterval = 0).
    /// </summary>
    public static class ParityAnchors
    {
        /// <summary>
        /// Digest t100 seed 42195 avec Population gelée à 0 (v1_009 → v1_089).
        /// Conservé pour prouver la réversibilité interval=0.
        /// </summary>
        public const ulong PreV1090FrozenPopulation = 0x4ED26CB61DE7B2B2UL;

        /// <summary>
        /// Digest t100 seed 42195 après v1_090 (Population = Σ PopData.Size chaque tick).
        /// Mesuré 2026-07-28 : seule CountryData.Population change dans le digest
        /// (variant sans Population = 0xDDFD98DA72F5E312 bit-identique avant/après).
        /// Ne PAS inventer — rejouer V1090BatchRunner.MeasureDigests.
        /// </summary>
        public const ulong Expected = 0xA6D63D33280D5778UL;
    }
}
