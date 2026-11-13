using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Online
{
    /// <summary>
    /// The weekly challenge (spec §3.7): everyone plays the same scenario and seed — one chapter-2 industry in turn,
    /// $10,000, 12 months — and is ranked by net worth. Weeks start on Monday 00:00 UTC.
    /// </summary>
    public static class WeeklyChallenge
    {
        public const int AttemptsPerWeek = 3;
        /// <summary>A run started last week still counts this long after the reset.</summary>
        public const int GraceSeconds = 3600;
        const long Week = 7 * 86400, MondayOffset = 3 * 86400;   // 1970-01-01 was a Thursday

        public static int WeekIndex(long unixSeconds)
        {
            long t = unixSeconds + MondayOffset;
            long w = t / Week;
            if (t % Week < 0) w--;
            return (int)w;
        }

        public static long WeekStartUnix(int week) => week * Week - MondayOffset;

        public static ScenarioDef For(int week, Balance b)
        {
            int n = b.Chapter2Industries.Count;
            int i = ((week % n) + n) % n;
            return new ScenarioDef
            {
                Seed = Hashes.SplitMix64((ulong)week ^ 0x5EED_2BUL),
                IndustryId = b.Chapter2Industries[i],
                StartCash = Money.FromCents(1_000_000),
                Months = 12,
            };
        }
    }
}
