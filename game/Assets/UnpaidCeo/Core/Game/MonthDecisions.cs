using System.Collections.Generic;
using UnpaidCeo.Core.Life;

namespace UnpaidCeo.Core.Game
{
    public sealed class LeverChange
    {
        public string BusinessId { get; set; }
        /// <summary>"price", "marketing" (cents), "units" (target owned units) or "staff" (target employees).</summary>
        public string Lever { get; set; }
        public long ValueCents { get; set; }
    }

    /// <summary>The player's plan for one month. Plain fields so it round-trips through the decision log.</summary>
    public sealed class MonthDecisions
    {
        public int Work { get; set; }
        public int Family { get; set; }
        public int Rest { get; set; }
        public int Study { get; set; }
        /// <summary>Business id → percent of Work time spent there (gigs).</summary>
        public Dictionary<string, int> WorkSplit { get; set; } = new Dictionary<string, int>();
        public List<LeverChange> Levers { get; set; } = new List<LeverChange>();
        public List<string> FreeActions { get; set; } = new List<string>();

        public bool TryGetAllocation(out TimeAllocation t) => TimeAllocation.TryCreate(Work, Family, Rest, Study, out t);

        /// <summary>The routine without one-off decisions (levers, free actions): what autopilot repeats.</summary>
        public MonthDecisions Routine() => new MonthDecisions
        {
            Work = Work, Family = Family, Rest = Rest, Study = Study,
            WorkSplit = new Dictionary<string, int>(WorkSplit ?? new Dictionary<string, int>()),
        };

        public static MonthDecisions Default(string firstBusinessId)
        {
            var d = new MonthDecisions { Work = 55, Family = 15, Rest = 20, Study = 10 };
            if (!string.IsNullOrEmpty(firstBusinessId)) d.WorkSplit[firstBusinessId] = 100;
            return d;
        }
    }
}
