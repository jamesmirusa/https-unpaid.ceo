using System;
using UnpaidCeo.Core.Content;

namespace UnpaidCeo.Core.Online
{
    /// <summary>
    /// The most career months real time allows (2A pacing): two onboarding months plus (manual + autopilot) months for
    /// every day since the server first saw the player, today included. The server refuses careers longer than this.
    /// </summary>
    public static class Pacing
    {
        public static int MaxCareerMonths(long firstSeenUnix, long nowUnix, Balance b)
        {
            long days = Math.Max(0, (nowUnix - firstSeenUnix) / 86400);
            return (int)Math.Min(int.MaxValue, 2 + (long)(b.DailyManualMonths + b.AutopilotMaxMonths) * (days + 1));
        }
    }
}
