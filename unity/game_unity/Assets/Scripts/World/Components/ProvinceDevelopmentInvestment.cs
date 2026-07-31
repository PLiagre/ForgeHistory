using Unity.Entities;
using Unity.Mathematics;

namespace VictoriaGame.World
{
    /// <summary>
    /// Contrat d'investissement provincial (v1_087) — coût, bornes, axes.
    /// À investissement nul (aucune intention acceptée), la simulation reste
    /// bit-identique à la parité v1_009 : le mécanisme est réversible par construction.
    /// </summary>
    public static class ProvinceDevelopmentInvestment
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 30;

        /// <summary>Coût monétaire = BaseMoneyCost × niveau courant (avant bump).</summary>
        public const float BaseMoneyCost = 50f;

        public const byte AxisTax = 0;
        public const byte AxisProduction = 1;
        public const byte AxisManpower = 2;

        public static bool IsValidAxis(byte axis) =>
            axis == AxisTax || axis == AxisProduction || axis == AxisManpower;

        public static float CostForLevel(int currentLevel) =>
            BaseMoneyCost * math.max(1, currentLevel);

        public static int ReadAxis(in ProvinceDevelopment dev, byte axis)
        {
            if (axis == AxisTax) return dev.Tax;
            if (axis == AxisProduction) return dev.Production;
            if (axis == AxisManpower) return dev.Manpower;
            return 0;
        }

        public static void WriteAxis(ref ProvinceDevelopment dev, byte axis, int value)
        {
            if (axis == AxisTax) dev.Tax = value;
            else if (axis == AxisProduction) dev.Production = value;
            else if (axis == AxisManpower) dev.Manpower = value;
        }

        public static float DevScore(in ProvinceDevelopment d)
        {
            var avg = (d.Tax + d.Production + d.Manpower) / 3f;
            return avg < 1f ? 1f : avg;
        }
    }
}
