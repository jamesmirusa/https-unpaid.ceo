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
