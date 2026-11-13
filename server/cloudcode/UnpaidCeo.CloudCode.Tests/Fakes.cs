using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace UnpaidCeo.CloudCode.Tests
{
    sealed class FakePlayers : IPlayerStore
    {
        readonly Dictionary<string, string> _data = new Dictionary<string, string>();
        public Task<string> Get(string playerId, string key) =>
            Task.FromResult(_data.TryGetValue(playerId + "/" + key, out var v) ? v : null);
        public Task Set(string playerId, string key, string value) { _data[playerId + "/" + key] = value; return Task.CompletedTask; }
    }

    /// <summary>Stores copies, as Cloud Save would.</summary>
    sealed class FakeDuels : IDuelStore
    {
        readonly Dictionary<string, string> _data = new Dictionary<string, string>();
        public Task<DuelRecord> Get(string duelId) =>
            Task.FromResult(_data.TryGetValue(duelId, out var json) ? JsonConvert.DeserializeObject<DuelRecord>(json) : null);
        public Task Create(DuelRecord duel) { _data[duel.Id] = JsonConvert.SerializeObject(duel); return Task.CompletedTask; }

        public Task SetScore(string duelId, bool playerA, long netWorthCents)
        {
            var d = JsonConvert.DeserializeObject<DuelRecord>(_data[duelId]);
            if (playerA) d.ScoreA = netWorthCents; else d.ScoreB = netWorthCents;
            _data[duelId] = JsonConvert.SerializeObject(d);
            return Task.CompletedTask;
        }
    }

    /// <summary>Keeps each player's best score per board, like the leaderboards configured "keepBest".</summary>
    sealed class FakeBoards : IBoardWriter
    {
        readonly Dictionary<string, Dictionary<string, double>> _best = new Dictionary<string, Dictionary<string, double>>();
        public readonly List<(string Board, string Player, double Score)> Submissions = new List<(string, string, double)>();
        /// <summary>The next Submit throws, as a Leaderboards call that times out would.</summary>
        public bool FailNext;

        public Task Submit(string boardId, string playerId, double score)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new System.InvalidOperationException("leaderboards unavailable");
            }
            Submissions.Add((boardId, playerId, score));
            if (!_best.TryGetValue(boardId, out var board)) _best[boardId] = board = new Dictionary<string, double>();
            if (!board.TryGetValue(playerId, out var old) || score > old) board[playerId] = score;
            return Task.CompletedTask;
        }

        public Task<int> RankOf(string boardId, string playerId)
        {
            if (!_best.TryGetValue(boardId, out var board) || !board.TryGetValue(playerId, out var mine)) return Task.FromResult(-1);
            return Task.FromResult(1 + board.Values.Count(v => v > mine));
        }

        /// <summary>The kept score, or NaN when the player has none on that board.</summary>
        public double Score(string boardId, string playerId) =>
            _best.TryGetValue(boardId, out var board) && board.TryGetValue(playerId, out var v) ? v : double.NaN;
    }

    sealed class FakeClock : IClock
    {
        public long NowUnix { get; set; }
    }
}
