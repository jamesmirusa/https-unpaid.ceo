using System;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace UnpaidCeo.Game.Services
{
    /// <summary>Device time for the daily turn budget: Unix seconds and the local UTC offset (from the browser on Web).</summary>
    public static class DeviceClock
    {
        public static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int UnpaidCeo_UtcOffsetMinutes();
        public static int UtcOffsetMinutes() => UnpaidCeo_UtcOffsetMinutes();
#else
        public static int UtcOffsetMinutes() => (int)Math.Round(TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes);
#endif
    }
}
