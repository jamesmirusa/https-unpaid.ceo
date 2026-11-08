using System.Collections.Generic;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Economy
{
    /// <summary>Mutable state of one gig or business the player runs.</summary>
    public sealed class BusinessState
    {
        public string Id { get; set; }
        public string IndustryId { get; set; }
        public string LotId { get; set; }
        public Money Price { get; set; }
        /// <summary>Owned units (carts, stores). Ignored for gigs.</summary>
        public int Units { get; set; }
        public Money Marketing { get; set; }
        /// <summary>Revenue booked but not yet paid, oldest first (B2B delay).</summary>
        public List<Money> Receivables { get; set; } = new List<Money>();
    }

    public readonly struct BusinessInputs
    {
        public readonly double ReputationFactor;
        public readonly double SeasonFactor;
        public readonly double EventFactor;
        /// <summary>Share of the player's Work time given to this gig, 0..100.</summary>
        public readonly int WorkShare;

        public BusinessInputs(double reputationFactor, double seasonFactor, double eventFactor, int workShare)
        {
            ReputationFactor = reputationFactor;
            SeasonFactor = seasonFactor;
            EventFactor = eventFactor;
            WorkShare = workShare;
        }
    }

    public sealed class BusinessMonthResult
    {
        public int UnitsSold { get; set; }
        public Money Revenue { get; set; }
        public Money Cogs { get; set; }
        public Money GrossProfit { get; set; }
        public Money Fixed { get; set; }
        public Money Marketing { get; set; }
        public Money OperatingProfit { get; set; }
        public Money CashCollected { get; set; }
    }
}
