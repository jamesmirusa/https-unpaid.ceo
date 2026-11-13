using System;
using System.Collections.Generic;
using UnpaidCeo.Core.Content;

namespace UnpaidCeo.Core.Game
{
    /// <summary>
    /// Offline progress: one turn (a week since stage 4B) per AutopilotHoursPerMonth real hours, capped per local day
    /// (TurnBudget), no storylets. It repeats the routine (time split), never the last turn's levers or free actions —
    /// except the week the player already decided before leaving, which is played as they decided it.
    /// </summary>
    public static class Autopilot
    {
        public static int MonthsToRun(long lastSeenUnix, long nowUnix, Balance b)
        {
            long elapsed = nowUnix - lastSeenUnix;
            if (elapsed <= 0 || b.AutopilotHoursPerMonth <= 0) return 0;
            long months = elapsed / (b.AutopilotHoursPerMonth * 3600L);
            return (int)Math.Min(months, Math.Max(0, b.AutopilotMaxMonths));
        }

        /// <param name="thisWeek">What the player decided for the current week before leaving (null: the routine).</param>
        public static IReadOnlyList<MonthReport> Run(GameEngine e, GameState s, int months, MonthDecisions thisWeek = null)
        {
            var reports = new List<MonthReport>();
            for (int i = 0; i < months; i++)
            {
                var d = i == 0 && thisWeek != null
                    ? thisWeek
                    : (s.LastDecisions ?? MonthDecisions.Default(s.Businesses.Count > 0 ? s.Businesses[0].Id : null)).Routine();
                reports.Add(e.EndWeek(s, d));
            }
            return reports;
        }
    }
}
