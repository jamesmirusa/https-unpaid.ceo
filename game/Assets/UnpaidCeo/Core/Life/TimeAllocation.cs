namespace UnpaidCeo.Core.Life
{
    /// <summary>How the month's CEO time splits; the four parts always sum to 100.</summary>
    public readonly struct TimeAllocation
    {
        public readonly int Work;
        public readonly int Family;
        public readonly int Rest;
        public readonly int Study;

        TimeAllocation(int work, int family, int rest, int study)
        {
            Work = work; Family = family; Rest = rest; Study = study;
        }

        public static bool TryCreate(int work, int family, int rest, int study, out TimeAllocation t)
        {
            t = default;
            if (work < 0 || family < 0 || rest < 0 || study < 0) return false;
            if (work + family + rest + study != 100) return false;
            t = new TimeAllocation(work, family, rest, study);
            return true;
        }

        public static TimeAllocation Default => new TimeAllocation(55, 15, 20, 10);

        /// <summary>
        /// Sets one share (clamped to 0–100) and rescales the others proportionally so the total stays 100.
        /// Rounding uses largest remainders; if the others are all zero they split the rest equally.
        /// Works for any number of shares (time split, work split between gigs).
        /// </summary>
        public static int[] Rebalance(int[] values, int index, int newValue)
        {
            int n = values.Length;
            var result = new int[n];
            int v = System.Math.Max(0, System.Math.Min(100, newValue));
            result[index] = v;
            int remaining = 100 - v;
            var others = new System.Collections.Generic.List<int>();
            for (int i = 0; i < n; i++) if (i != index) others.Add(i);
            if (others.Count == 0) { result[index] = 100; return result; }

            long sum = 0;
            foreach (var i in others) sum += System.Math.Max(0, values[i]);
            if (sum == 0)
            {
                for (int k = 0; k < others.Count; k++)
                    result[others[k]] = remaining / others.Count + (k < remaining % others.Count ? 1 : 0);
                return result;
            }

            var fractions = new System.Collections.Generic.List<(int index, double frac)>();
            int assigned = 0;
            foreach (var i in others)
            {
                double exact = (double)System.Math.Max(0, values[i]) * remaining / sum;
                int floor = (int)System.Math.Floor(exact);
                result[i] = floor;
                assigned += floor;
                fractions.Add((i, exact - floor));
            }
            fractions.Sort((a, b) => b.frac != a.frac ? b.frac.CompareTo(a.frac) : a.index.CompareTo(b.index));
            for (int k = 0, left = remaining - assigned; left > 0; k = (k + 1) % fractions.Count, left--)
                result[fractions[k].index]++;
            return result;
        }
    }

    /// <summary>Change applied to life scales in one month (after clamping).</summary>
    public readonly struct LifeDelta
    {
        public readonly int Energy;
        public readonly int Relationships;
        public readonly int StudyXp;

        public LifeDelta(int energy, int relationships, int studyXp)
        {
            Energy = energy; Relationships = relationships; StudyXp = studyXp;
        }
    }
}
