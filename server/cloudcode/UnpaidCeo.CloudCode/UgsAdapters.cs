using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;
using Unity.Services.Leaderboards.Model;

namespace UnpaidCeo.CloudCode
{
    /// <summary>Cloud Save protected player data, written with the service token (players can read, never write).</summary>
    sealed class UgsPlayerStore : IPlayerStore
    {
        readonly IGameApiClient _api;
        readonly IExecutionContext _ctx;

        public UgsPlayerStore(IGameApiClient api, IExecutionContext ctx) { _api = api; _ctx = ctx; }

        public async Task<string> Get(string playerId, string key)
        {
            try
            {
                var r = await _api.CloudSaveData.GetProtectedItemsAsync(_ctx, _ctx.ServiceToken, _ctx.ProjectId, playerId, new List<string> { key });
                return r.Data?.Results?.FirstOrDefault(i => i.Key == key)?.Value?.ToString();
            }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound) { return null; }
        }

        public Task Set(string playerId, string key, string value) =>
            _api.CloudSaveData.SetProtectedItemAsync(_ctx, _ctx.ServiceToken, _ctx.ProjectId, playerId, new SetItemBody(key, value));
    }

    /// <summary>
    /// One Cloud Save custom entity per duel ("duel-&lt;id&gt;"): the record under "duel", each side's score under its own
    /// key, so the two players' writes never collide. Players can read custom items; only the server writes them.
    /// </summary>
    sealed class UgsDuelStore : IDuelStore
    {
        const string RecordKey = "duel", ScoreAKey = "score_a", ScoreBKey = "score_b";
        readonly IGameApiClient _api;
        readonly IExecutionContext _ctx;

        public UgsDuelStore(IGameApiClient api, IExecutionContext ctx) { _api = api; _ctx = ctx; }

        static string Entity(string duelId) => "duel-" + duelId;

        public async Task<DuelRecord> Get(string duelId)
        {
            List<Item> items;
            try
            {
                var r = await _api.CloudSaveData.GetCustomItemsAsync(_ctx, _ctx.ServiceToken, _ctx.ProjectId, Entity(duelId),
                    new List<string> { RecordKey, ScoreAKey, ScoreBKey });
                items = r.Data?.Results ?? new List<Item>();
            }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound) { return null; }

            string Text(string key) => items.FirstOrDefault(i => i.Key == key)?.Value?.ToString();
            var record = Text(RecordKey);
            if (string.IsNullOrEmpty(record)) return null;
            var d = JsonConvert.DeserializeObject<DuelRecord>(record);
            d.ScoreA = long.TryParse(Text(ScoreAKey), out var a) ? a : (long?)null;
            d.ScoreB = long.TryParse(Text(ScoreBKey), out var b) ? b : (long?)null;
            return d;
        }

        public Task Create(DuelRecord duel)
        {
            var record = new DuelRecord { Id = duel.Id, A = duel.A, B = duel.B, IndustryId = duel.IndustryId, CreatedUnix = duel.CreatedUnix };
            return _api.CloudSaveData.SetCustomItemAsync(_ctx, _ctx.ServiceToken, _ctx.ProjectId, Entity(duel.Id),
                new SetItemBody(RecordKey, JsonConvert.SerializeObject(record)));
        }

        public Task SetScore(string duelId, bool playerA, long netWorthCents) =>
            _api.CloudSaveData.SetCustomItemAsync(_ctx, _ctx.ServiceToken, _ctx.ProjectId, Entity(duelId),
                new SetItemBody(playerA ? ScoreAKey : ScoreBKey, netWorthCents.ToString()));
    }

    /// <summary>Leaderboard scores written with the service token (the boards deny client writes).</summary>
    sealed class UgsBoards : IBoardWriter
    {
        readonly IGameApiClient _api;
        readonly IExecutionContext _ctx;

        public UgsBoards(IGameApiClient api, IExecutionContext ctx) { _api = api; _ctx = ctx; }

        public Task Submit(string boardId, string playerId, double score) =>
            _api.Leaderboards.AddLeaderboardPlayerScoreAsync(_ctx, _ctx.ServiceToken, Guid.Parse(_ctx.ProjectId), boardId, playerId,
                new AddLeaderboardScore(score));

        /// <summary>The Leaderboards API ranks from 0; the game shows ranks from 1.</summary>
        public async Task<int> RankOf(string boardId, string playerId)
        {
            try
            {
                var r = await _api.Leaderboards.GetLeaderboardPlayerScoreAsync(_ctx, _ctx.ServiceToken, Guid.Parse(_ctx.ProjectId), boardId, playerId);
                return r.Data == null ? -1 : r.Data.Rank + 1;
            }
            catch (ApiException) { return -1; }
        }
    }

    sealed class SystemClock : IClock
    {
        public static readonly SystemClock Instance = new SystemClock();
        public long NowUnix => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
