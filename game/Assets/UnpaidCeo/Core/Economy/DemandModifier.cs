namespace UnpaidCeo.Core.Economy
{
    /// <summary>A temporary demand multiplier from an event (a price war, a viral post) for some months.</summary>
    public sealed class DemandModifier
    {
        public const string CurrentBusiness = "current";

        public double Factor { get; set; } = 1.0;
        public int MonthsLeft { get; set; }
        /// <summary>An industry id, or "current" for the player's chapter-2 business.</summary>
        public string IndustryId { get; set; } = CurrentBusiness;

        public bool AppliesTo(IndustryDef d) =>
            IndustryId == d.Id || (IndustryId == CurrentBusiness && d.Kind == IndustryKind.Business);
    }
}
