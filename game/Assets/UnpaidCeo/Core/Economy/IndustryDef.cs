using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Economy
{
    public enum IndustryKind { Gig, Business }

    /// <summary>Static definition of a gig or business type (content data, money in cents).</summary>
    public sealed class IndustryDef
    {
        public string Id { get; set; }
        public IndustryKind Kind { get; set; }
        public Money Price { get; set; }
        public Money RefPrice { get; set; }
        public Money UnitCost { get; set; }
        public int BaseDemand { get; set; }
        public double Elasticity { get; set; }
        public double MarketingA { get; set; }
        public Money MarketingK { get; set; }
        /// <summary>Business: units sold per owned unit (cart, store) per month.</summary>
        public int CapacityPerUnit { get; set; }
        /// <summary>Gig: units (hours, items, tasks) per percentage point of Work time per month.</summary>
        public double CapacityPerWorkPoint { get; set; }
        public Money FixedMonthly { get; set; }
        public Money Capex { get; set; }
        public int ReceivableDelayMonths { get; set; }
        /// <summary>Study XP needed before this industry unlocks.</summary>
        public int RequiredStudy { get; set; }
    }
}
