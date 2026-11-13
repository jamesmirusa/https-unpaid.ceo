using System;
using System.Collections.Generic;
using UnpaidCeo.Core.Content;

namespace UnpaidCeo.Core.Game
{
    /// <summary>
    /// The player's daily turn budget and streak. Kept beside the game save, never inside GameState:
    /// it is not replayed or hashed, so it cannot change rating results.
    /// </summary>
    public sealed class TurnState
    {
        /// <summary>Local day number of the last refill, counted in <see cref="OffsetMinutes"/>.</summary>
        public int Day { get; set; }
        public int ManualLeft { get; set; }
        /// <summary>The latest device time seen; it never moves back.</summary>
        public long LastSeenUnix { get; set; }
        /// <summary>Consecutive days with all daily missions done.</summary>
        public int Streak { get; set; }
        /// <summary>The last day whose missions were all done.</summary>
        public int LastPlayedDay { get; set; }
        /// <summary>The UTC offset the current day started in.</summary>
        public int OffsetMinutes { get; set; }
        public bool Started { get; set; }
        /// <summary>Local day (in <see cref="OffsetMinutes"/>) of <see cref="AutopilotUsed"/>.</summary>
        public int AutopilotDay { get; set; }
        /// <summary>Autopilot months already granted on <see cref="AutopilotDay"/>.</summary>
        public int AutopilotUsed { get; set; }
    }

    /// <summary>
    /// Pacing (plan 2A): DailyManualMonths manual months per local calendar day, plus offline autopilot.
    /// A clock set back grants nothing, and a time-zone change cannot bring a second refill the same day.
    /// </summary>
    public static class TurnBudget
    {
        public const long SecondsPerDay = 86400;

        public static int LocalDay(long unixSeconds, int utcOffsetMinutes)
        {
            long t = unixSeconds + utcOffsetMinutes * 60L;
            long d = t / SecondsPerDay;
            if (t % SecondsPerDay < 0) d--;
            return (int)d;
        }

        public static void Refresh(TurnState t, long nowUnix, int utcOffsetMinutes, Balance b)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (!t.Started)
            {
                t.Started = true;
                t.OffsetMinutes = utcOffsetMinutes;
                t.Day = LocalDay(nowUnix, utcOffsetMinutes);
                t.ManualLeft = b.DailyManualMonths;
                t.LastSeenUnix = nowUnix;
                return;
            }
            if (nowUnix < t.LastSeenUnix) return;   // the device clock went back
            t.LastSeenUnix = nowUnix;

            // A new day is judged in the zone the current day started in, so moving zones cannot refill early.
            int there = LocalDay(nowUnix, t.OffsetMinutes);
            if (there <= t.Day) return;
            // Adopt the device's zone unless it would put the day back (flying west); then keep counting where we were.
            int here = LocalDay(nowUnix, utcOffsetMinutes);
            if (here >= there) { t.OffsetMinutes = utcOffsetMinutes; t.Day = here; }
            else t.Day = there;
            t.ManualLeft = b.DailyManualMonths;
            if (t.Day - t.LastPlayedDay > 1) t.Streak = 0;
        }

        public static bool TryConsume(TurnState t)
        {
            if (t.ManualLeft <= 0) return false;
            t.ManualLeft--;
            return true;
        }

        /// <summary>Seconds until the next refill: the next local midnight in the zone the current day started in.</summary>
        public static long SecondsToRefill(TurnState t, long nowUnix, int utcOffsetMinutes)
        {
            if (!t.Started) return 0;
            long next = (t.Day + 1L) * SecondsPerDay - t.OffsetMinutes * 60L;
            return Math.Max(0, next - nowUnix);
        }

        /// <summary>All of today's missions are done: the streak grows if yesterday was done too.</summary>
        public static void CompleteDay(TurnState t)
        {
            if (t.LastPlayedDay == t.Day) return;
            t.Streak = t.LastPlayedDay == t.Day - 1 ? t.Streak + 1 : 1;
            t.LastPlayedDay = t.Day;
        }

        /// <summary>
        /// On launch: offline autopilot months since the last seen time (they use no manual turns), at most
        /// AutopilotMaxMonths per local day however often the game is opened, then today's refill.
        /// Nothing runs on the very first launch.
        /// </summary>
        public static IReadOnlyList<MonthReport> OnLaunch(GameEngine e, GameState s, TurnState t, long nowUnix, int utcOffsetMinutes,
            MonthDecisions thisWeek = null)
        {
            IReadOnlyList<MonthReport> reports = Array.Empty<MonthReport>();
            if (t.Started && t.LastSeenUnix > 0)
            {
                var b = e.Content.Balance;
                int months = Autopilot.MonthsToRun(t.LastSeenUnix, nowUnix, b);
                int today = LocalDay(nowUnix, t.OffsetMinutes);
                if (t.AutopilotDay != today) { t.AutopilotDay = today; t.AutopilotUsed = 0; }
                months = Math.Min(months, Math.Max(0, b.AutopilotMaxMonths - t.AutopilotUsed));
                t.AutopilotUsed += months;
                reports = Autopilot.Run(e, s, months, thisWeek);
            }
            Refresh(t, nowUnix, utcOffsetMinutes, e.Content.Balance);
            return reports;
        }
    }
}
