namespace UnpaidCeo.Core.Online
{
    /// <summary>The server's greeting at sign-in: its clock, this week's challenge.</summary>
    public sealed class HelloResult
    {
        public long ServerUnix { get; set; }
        public int Week { get; set; }
        public ScenarioDef Challenge { get; set; }
        /// <summary>Verified attempts this week (of WeeklyChallenge.AttemptsPerWeek).</summary>
        public int ChallengeAttemptsUsed { get; set; }
    }

    /// <summary>One duel seen by one of its two players.</summary>
    public sealed class DuelView
    {
        public const string WaitingMe = "waiting_me", WaitingThem = "waiting_them", Won = "won", Lost = "lost", Draw = "draw", Expired = "expired";

        public string Id { get; set; }
        public string Opponent { get; set; }
        /// <summary>Filled by the client from its friends list (the server keeps no names).</summary>
        public string OpponentName { get; set; }
        public string IndustryId { get; set; }
        public long CreatedUnix { get; set; }
        public long? MyNetWorthCents { get; set; }
        public long? TheirNetWorthCents { get; set; }
        public string State { get; set; }
        /// <summary>Why a create or submit was refused; null when it went through.</summary>
        public string Error { get; set; }
    }
}
