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
        /// <summary>A legacy month's pool (challenge and duel scenarios play whole months).</summary>
        public int BaseActionPoints { get; set; } = 3;
        /// <summary>A week's pool (stage 5A): careers play weeks, and each week brings its own points.</summary>
        public int BaseWeeklyActionPoints { get; set; } = 2;
        public int BurnoutThreshold { get; set; }
        public double BurnoutErrorChance { get; set; }
    }
}
