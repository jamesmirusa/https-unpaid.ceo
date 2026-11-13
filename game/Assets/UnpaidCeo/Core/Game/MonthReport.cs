using System.Collections.Generic;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Game
{
    /// <summary>A relationship level reached, with the bonus it brought (its string key).</summary>
    public sealed class LevelUp
    {
        public string Npc { get; set; }
        public int Level { get; set; }
        public string Key { get; set; }
    }

    /// <summary>One named amount of a week's result: a bill paid, a loan payment, what falls due next week.</summary>
    public sealed class LineItem
    {
        public string Key { get; set; }
        public Money Amount { get; set; }
        public LineItem() { }
        public LineItem(string key, Money amount) { Key = key; Amount = amount; }
    }

    /// <summary>The turn's P&L and side effects (a week since stage 4B; a month in legacy logs), shown as the waterfall.</summary>
    public sealed class MonthReport
    {
        public int Month { get; set; }
        public int Week { get; set; }
        /// <summary>A week's result (stage 4B) rather than a legacy month's.</summary>
        public bool IsWeek { get; set; }
        /// <summary>1–4; a legacy month report is 1.</summary>
        public int WeekOfMonth { get; set; } = 1;
        /// <summary>Bills and loan payments made this turn, by name.</summary>
        public List<LineItem> Paid { get; set; } = new List<LineItem>();
        /// <summary>What falls due next week (bills, a loan payment), so the player can plan for it.</summary>
        public List<LineItem> DueNext { get; set; } = new List<LineItem>();
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
        /// <summary>Stage 5B · relationship levels reached this turn that brought a bonus.</summary>
        public List<LevelUp> LevelUps { get; set; } = new List<LevelUp>();
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
        /// <summary>Each gig's and business's own month, by business id (unit economics on the Company screen).</summary>
        public Dictionary<string, BusinessMonthResult> BusinessResults { get; set; } = new Dictionary<string, BusinessMonthResult>();
    }

    public readonly struct ChapterStatus
    {
        public readonly int Chapter;
        public readonly bool GoalMet;
        /// <summary>0..1 toward the chapter goal, for the HUD bar.</summary>
        public readonly double Progress;
        public ChapterStatus(int chapter, bool goalMet, double progress) { Chapter = chapter; GoalMet = goalMet; Progress = progress; }
    }
}
