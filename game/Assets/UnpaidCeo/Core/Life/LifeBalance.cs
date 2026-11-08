namespace UnpaidCeo.Core.Life
{
    /// <summary>Tunable coefficients of the life model (balance.json → life).</summary>
    public sealed class LifeBalance
    {
        public int RestBaseline { get; set; }
        public double RestCoef { get; set; }
        public int WorkSafe { get; set; }
        public double BurnCoef { get; set; }
        public int FamilyBaseline { get; set; }
        public double FamilyCoef { get; set; }
        public double StudyCoef { get; set; }
        public int BaseActionPoints { get; set; } = 3;
        public int BurnoutThreshold { get; set; }
        public double BurnoutErrorChance { get; set; }
    }
}
