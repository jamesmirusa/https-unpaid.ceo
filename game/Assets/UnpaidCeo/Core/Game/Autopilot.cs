using System;
using System.Collections.Generic;
using UnpaidCeo.Core.Content;

namespace UnpaidCeo.Core.Game
{
    /// <summary>Offline progress: one month per AutopilotHoursPerMonth real hours, capped, no storylets.</summary>
    public static class Autopilot
    {
        public static int MonthsToRun(long lastSeenUnix, long nowUnix, Balance b)
        {
            long elapsed = nowUnix - lastSeenUnix;
            if (elapsed <= 0 || b.AutopilotHoursPerMonth <= 0) return 0;
            long months = elapsed / (b.AutopilotHoursPerMonth * 3600L);
            return (int)Math.Min(months, Math.Max(0, b.AutopilotMaxMonths));
        }

        public static IReadOnlyList<MonthReport> Run(GameEngine e, GameState s, int months)
        {
            var reports = new List<MonthReport>();
            for (int i = 0; i < months; i++)
            {
                var d = s.LastDecisions ?? MonthDecisions.Default(s.Businesses.Count > 0 ? s.Businesses[0].Id : null);
                reports.Add(e.EndMonth(s, d));
            }
            return reports;
        }
    }
}
