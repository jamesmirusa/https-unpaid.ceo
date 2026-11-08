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
    }

    public readonly struct ChapterStatus
    {
        public readonly int Chapter;
        public readonly bool GoalMet;
        public ChapterStatus(int chapter, bool goalMet) { Chapter = chapter; GoalMet = goalMet; }
    }
}
