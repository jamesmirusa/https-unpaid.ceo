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
        public int Chapter3UnitsGoal { get; set; }
        /// <summary>Share of capex a unit is worth when sold (and in net worth).</summary>
        public double AssetResaleRate { get; set; }
        /// <summary>Employer costs on top of salaries (payroll taxes, insurance).</summary>
        public double PayrollOverhead { get; set; }
        public LoanBalance Loans { get; set; } = new LoanBalance();
        /// <summary>Months a player can play by hand per local calendar day.</summary>
        public int DailyManualMonths { get; set; } = 6;
        public LifeBalance Life { get; set; } = new LifeBalance();
        public List<SourceRef> Sources { get; set; } = new List<SourceRef>();
    }
}
