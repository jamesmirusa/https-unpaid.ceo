using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Core.Online
{
    /// <summary>The outcome of a strict replay: the rebuilt state, the last month's report, or where and why it stopped.</summary>
    public sealed class ReplayResult
    {
        public GameState State { get; set; }
        /// <summary>The report of the last replayed endMonth (GameState keeps no report history).</summary>
        public MonthReport LastReport { get; set; }
        public int FailedAt { get; set; } = -1;
        public string Error { get; set; }
        public bool Ok => FailedAt < 0;
    }
}
