namespace UnpaidCeo.Core.Online
{
    /// <summary>Leagues by capital (spec §3.7): Rookie → Hustler ($10k) → Boss ($100k) → Tycoon ($1M) → Legend ($10M).</summary>
    public static class Leagues
    {
        public static readonly string[] All = { "rookie", "hustler", "boss", "tycoon", "legend" };
        /// <summary>Net worth (cents) where each league starts, parallel to <see cref="All"/>.</summary>
        public static readonly long[] ThresholdsCents = { 0, 1_000_000, 10_000_000, 100_000_000, 1_000_000_000 };

        public static string For(long netWorthCents)
        {
            int i = ThresholdsCents.Length - 1;
            while (i > 0 && netWorthCents < ThresholdsCents[i]) i--;
            return All[i];
        }

        /// <summary>Where the next league starts, or null in the top league.</summary>
        public static long? Next(long netWorthCents)
        {
            foreach (var t in ThresholdsCents) if (t > netWorthCents) return t;
            return null;
        }
    }
}
