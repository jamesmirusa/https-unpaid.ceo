using System.Collections.Generic;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Game
{
    /// <summary>The month's P&L and side effects, shown to the player as the waterfall.</summary>
    public sealed class MonthReport
    {
        public int Month { get; set; }
        public Money Revenue { get; set; }
        public Money Cogs { get; set; }
        public Money GrossProfit { get; set; }
        public Money Fixed { get; set; }
        public Money Marketing { get; set; }
        public Money MistakeLoss { get; set; }
        public Money OperatingProfit { get; set; }
        public Money TaxReserve { get; set; }
        public Money NetProfit { get; set; }
        public Money PersonalCosts { get; set; }
        public Money CashStart { get; set; }
        public Money CashEnd { get; set; }
        public List<string> Storylets { get; set; } = new List<string>();
        public LifeDelta Life { get; set; }
        public List<string> Lessons { get; set; } = new List<string>();
        public bool BurnoutMistake { get; set; }
        public List<string> RejectedActions { get; set; } = new List<string>();
        /// <summary>Levers that could not be applied, as "lever:businessId" (e.g. "units:shop" when cash was short).</summary>
        public List<string> RejectedLevers { get; set; } = new List<string>();
        /// <summary>Paid this month for new units (investment, not an expense).</summary>
        public Money Capex { get; set; }
        /// <summary>Received this month for units sold off.</summary>
        public Money AssetSales { get; set; }
        /// <summary>Loan payments made this month (interest + principal).</summary>
        public Money LoanPayments { get; set; }
        /// <summary>Interest part of the loan payments (an expense before the tax reserve).</summary>
        public Money Interest { get; set; }
    }

    public readonly struct ChapterStatus
    {
        public readonly int Chapter;
        public readonly bool GoalMet;
        public ChapterStatus(int chapter, bool goalMet) { Chapter = chapter; GoalMet = goalMet; }
    }
}
