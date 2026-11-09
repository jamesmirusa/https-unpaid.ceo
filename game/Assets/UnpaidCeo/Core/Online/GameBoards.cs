namespace UnpaidCeo.Core.Online
{
    /// <summary>Leaderboard ids, shared by the game, the Cloud Code module and the UGS config check.</summary>
    public static class GameBoards
    {
        public const string Capital = "capital", WeeklyChallenge = "weekly_challenge", MarketPrefix = "market_";

        /// <summary>The weekly "market wars" board of one chapter-2 industry.</summary>
        public static string Market(string industryId) => MarketPrefix + industryId;
    }
}
