using System.Collections.Generic;
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

        /// <summary>Chapter the industry belongs to: 1 for the Bronx gigs, 2 for the businesses chosen in Brooklyn.</summary>
        public int Chapter { get; set; } = 1;
        /// <summary>Business: staff needed per unit for full capacity (the owner counts as one).</summary>
        public int StaffPerUnit { get; set; } = 1;
        /// <summary>Business: monthly salary per employee.</summary>
        public Money Salary { get; set; }
        /// <summary>Purchases per customer over their lifetime (LTV and CAC).</summary>
        public double RepeatPurchases { get; set; } = 1;
        /// <summary>Demand multiplier per calendar month, January first.</summary>
        public double[] Seasonality { get; set; } = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
        /// <summary>Localization keys for the scale path: 1 unit, 2–4 units, 5+ units.</summary>
        public List<string> ScaleNameKeys { get; set; } = new List<string>();
        /// <summary>The industry's main Academy lesson.</summary>
        public string LessonId { get; set; }

        public string ScaleNameKey(int units) =>
            ScaleNameKeys.Count < 3 ? null : units >= 5 ? ScaleNameKeys[2] : units >= 2 ? ScaleNameKeys[1] : ScaleNameKeys[0];
    }
}
