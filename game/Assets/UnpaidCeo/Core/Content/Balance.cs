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

    /// <summary>Global balance constants (balance.json). Values in cents where money.</summary>
    public sealed class Balance
    {
        public double TaxRate { get; set; }
        public Money BillsStartAfter { get; set; }
        public PersonalCosts PersonalCosts { get; set; } = new PersonalCosts();
        public Money Chapter1CashGoal { get; set; }
        public Money DebtBankruptLine { get; set; }
        public int[] EventsPerMonthWeights { get; set; } = { 1, 2, 1 };
        public int AutopilotHoursPerMonth { get; set; } = 4;
        public int AutopilotMaxMonths { get; set; } = 2;
        public Money TutorialGigPay { get; set; }
        public List<string> Chapter1Gigs { get; set; } = new List<string>();
        public LifeBalance Life { get; set; } = new LifeBalance();
        public List<SourceRef> Sources { get; set; } = new List<SourceRef>();
    }
}
