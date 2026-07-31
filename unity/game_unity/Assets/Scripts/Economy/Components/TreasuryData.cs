using Unity.Entities;

namespace VictoriaGame.Economy
{
    [System.Serializable]
    public struct TreasuryData : IComponentData
    {
        public float Balance;
        public float Income;
        public float Expenses;
        public float Debt;
        public float DebtInterestRate;
        public int BankruptcyTick;
        public int BankruptcyCount;
    }
}
