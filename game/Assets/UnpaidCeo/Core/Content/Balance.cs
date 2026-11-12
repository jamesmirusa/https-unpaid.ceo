using System.Collections.Generic;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Content
{
    public sealed class PersonalCosts
    {
        public Money Room { get; set; }
        public Money Food { get; set; }
        public Money Transit { get; set; }
        public Money Essentials { get; set; }
        public Money Total => Room + Food + Transit + Essentials;

        /// <summary>
        /// The bills of one week of the month: food and transit every week (a quarter each, the last week takes the odd
        /// cents), essentials on week 2, rent on the last week (the landlord collects at month end, so the first pay comes
        /// before the first rent). Four weeks add up to <see cref="Total"/> exactly.
        /// </summary>
        public List<Game.LineItem> ForWeek(int weekOfMonth)
        {
            var items = new List<Game.LineItem>();
            int n = Game.GameState.WeeksPerMonth;
            items.Add(new Game.LineItem("bill.food", Quarter(Food, weekOfMonth, n)));
            items.Add(new Game.LineItem("bill.transit", Quarter(Transit, weekOfMonth, n)));
            if (weekOfMonth == 2) items.Add(new Game.LineItem("bill.essentials", Essentials));
            if (weekOfMonth == n) items.Add(new Game.LineItem("bill.room", Room));
            return items;
        }

        static Money Quarter(Money m, int week, int n)
        {
            long part = m.Cents / n;
            return Money.FromCents(week == n ? m.Cents - part * (n - 1) : part);
        }
    }

    public sealed class SourceRef
    {
        public string Field { get; set; }
        public string Url { get; set; }
        public string CheckedOn { get; set; }
        public string Note { get; set; }
    }

    /// <summary>Bank loan rules (chapters 2–3).</summary>
    public sealed class LoanBalance
    {
        public double BaseApr { get; set; } = 0.12;
        /// <summary>APR added per reputation point below 50.</summary>
        public double AprPerMissingReputation { get; set; } = 0.001;
        public double LowHonestyAprAdd { get; set; } = 0.03;
        public int LowHonestyBelow { get; set; } = 40;
        public Money MaxBase { get; set; } = Money.FromCents(1_000_000);
        public Money MaxPerReputation { get; set; } = Money.FromCents(50_000);
        public int[] TermsMonths { get; set; } = { 12, 24, 36 };
    }

    public sealed class MultipleStep
    {
        /// <summary>Yearly profit below which this multiple applies.</summary>
        public Money Below { get; set; }
        public double Multiple { get; set; }
    }

    /// <summary>Company valuation rules (chapters 4–5): see docs/superpowers/specs/2026-11-10-stage3-zero-to-billionaire-design.md.</summary>
    public sealed class ValuationBalance
    {
        /// <summary>Multiples of yearly profit by size, smallest first; profits past the last step use TopMultiple.</summary>
        public List<MultipleStep> Ladder { get; set; } = new List<MultipleStep>();
        public double TopMultiple { get; set; } = 8.0;
        public double PublicMultiple { get; set; } = 15.0;
        /// <summary>Share of the company sold in one investment round.</summary>
        public double RoundDilution { get; set; } = 0.20;
    }

    /// <summary>Global balance constants (balance.json). Values in cents where money.</summary>
    public sealed class Balance
    {
        public double TaxRate { get; set; }
        public Money BillsStartAfter { get; set; }
        /// <summary>Chapter-1 living costs (a shared room in the Bronx).</summary>
        public PersonalCosts PersonalCosts { get; set; } = new PersonalCosts();
        /// <summary>Living costs from a chapter on (housing follows the district): chapter → costs.</summary>
        public Dictionary<int, PersonalCosts> PersonalCostsByChapter { get; set; } = new Dictionary<int, PersonalCosts>();

        /// <summary>The costs of the latest listed chapter not after this one; chapter 1 (or none listed) uses PersonalCosts.</summary>
        public PersonalCosts PersonalCostsFor(int chapter)
        {
            var best = PersonalCosts;
            int bestChapter = 1;
            if (PersonalCostsByChapter != null)
                foreach (var kv in PersonalCostsByChapter)
                    if (kv.Key > bestChapter && kv.Key <= chapter && kv.Value != null) { best = kv.Value; bestChapter = kv.Key; }
            return best;
        }
        public Money Chapter1CashGoal { get; set; }
        public Money DebtBankruptLine { get; set; }
        public int[] EventsPerMonthWeights { get; set; } = { 1, 2, 1 };
        /// <summary>Scenes rolled per week (stage 4B): weights for 0, 1, … scenes; the default is about one a month.</summary>
        public int[] EventsPerWeekWeights { get; set; } = { 3, 1 };
        /// <summary>
        /// The week's modes (stage 5A): mode id → work, family, rest, study shares summing to 100. "balance" is the
        /// default plan; the order on screen is <see cref="Game.WeekPlanning.ModeOrder"/>.
        /// </summary>
        public Dictionary<string, int[]> WeekModes { get; set; } = new Dictionary<string, int[]>
        {
            ["hustle"] = new[] { 70, 10, 15, 5 },
            ["balance"] = new[] { 55, 15, 20, 10 },
            ["study"] = new[] { 45, 10, 20, 25 },
            ["family"] = new[] { 45, 25, 25, 5 },
        };
        public int AutopilotHoursPerMonth { get; set; } = 4;
        public int AutopilotMaxMonths { get; set; } = 2;
        public Money TutorialGigPay { get; set; }
        public List<string> Chapter1Gigs { get; set; } = new List<string>();
        public List<string> Chapter2Industries { get; set; } = new List<string>();
        /// <summary>Demand multiplier per district (bronx, brooklyn, queens).</summary>
        public Dictionary<string, double> DistrictDemand { get; set; } = new Dictionary<string, double>();
        public Money Chapter2CapitalGoal { get; set; }
        public Money Chapter3CapitalGoal { get; set; }
        public int Chapter3EmployeesGoal { get; set; }
        /// <summary>Month ends in a row the chapter-3 team must be kept: a team hired for one month is not a team.</summary>
        public int Chapter3TeamMonths { get; set; }
        public int Chapter3UnitsGoal { get; set; }
        /// <summary>Share of capex a unit is worth when sold (and in net worth).</summary>
        public double AssetResaleRate { get; set; }
        /// <summary>Employer costs on top of salaries (payroll taxes, insurance).</summary>
        public double PayrollOverhead { get; set; }
        public LoanBalance Loans { get; set; } = new LoanBalance();
        /// <summary>Months a player can play by hand per local calendar day.</summary>
        public int DailyManualMonths { get; set; } = 6;
        public LifeBalance Life { get; set; } = new LifeBalance();
        public ValuationBalance Valuation { get; set; } = new ValuationBalance();
        public List<SourceRef> Sources { get; set; } = new List<SourceRef>();
    }
}
