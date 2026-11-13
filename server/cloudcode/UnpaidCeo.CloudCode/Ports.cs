using System.Threading.Tasks;

namespace UnpaidCeo.CloudCode
{
    /// <summary>Protected player data: players can read, only the server writes (first seen, attempts, duel ids).</summary>
    public interface IPlayerStore
    {
        Task<string> Get(string playerId, string key);
        Task Set(string playerId, string key, string value);
    }

    /// <summary>
    /// Duel records (Cloud Save custom items, one entity per duel). Each side's score is its own write, so two players
    /// finishing at the same moment never overwrite each other.
    /// </summary>
    public interface IDuelStore
    {
        Task<DuelRecord> Get(string duelId);
        /// <summary>Stores a new duel (no scores yet).</summary>
        Task Create(DuelRecord duel);
        Task SetScore(string duelId, bool playerA, long netWorthCents);
    }

    /// <summary>Leaderboard writes with the service token; clients are denied writes by policy.</summary>
    public interface IBoardWriter
    {
        Task Submit(string boardId, string playerId, double score);
        /// <summary>1-based rank, or -1 when unknown.</summary>
        Task<int> RankOf(string boardId, string playerId);
    }

    public interface IClock
    {
        long NowUnix { get; }
    }

    /// <summary>A duel as stored on the server.</summary>
    public sealed class DuelRecord
    {
        public string Id { get; set; }
        public string A { get; set; }
        public string B { get; set; }
        public string IndustryId { get; set; }
        public long CreatedUnix { get; set; }
        public long? ScoreA { get; set; }
        public long? ScoreB { get; set; }
    }
}
