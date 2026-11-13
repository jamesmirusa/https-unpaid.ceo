using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Online;

namespace UnpaidCeo.CloudCode
{
    /// <summary>
    /// The rules of every endpoint, free of Cloud Code types: runs are replayed with the game's core before any number
    /// reaches a leaderboard; careers are capped by real time; challenges by week and attempts; duels by their two players.
    /// </summary>
    public sealed class GameServer
    {
        const string FirstSeenKey = "first_seen", DuelsKey = "duels";
        const int MaxListedDuels = 20, MaxDuelsPerDay = 10;
        const long Day = 86400;
        /// <summary>UGS player ids are short URL-safe strings; anything else is not a player.</summary>
        static readonly Regex PlayerIdPattern = new Regex(@"^[A-Za-z0-9_-]{1,64}\z", RegexOptions.CultureInvariant);

        readonly ContentDb _c;
        readonly string _version;
        readonly IPlayerStore _players;
        readonly IDuelStore _duels;
        readonly IBoardWriter _boards;
        readonly IClock _clock;
        readonly Func<string> _newId;

        public GameServer(ContentDb content, string contentVersion, IPlayerStore players, IDuelStore duels, IBoardWriter boards,
            IClock clock, Func<string> newId = null)
        {
            _c = content;
            _version = contentVersion;
            _players = players;
            _duels = duels;
            _boards = boards;
            _clock = clock;
            _newId = newId ?? (() => Guid.NewGuid().ToString("N"));
        }

        ScenarioDef ScenarioFor(RunStart r) => Scenarios.For(r, _c.Balance);

        public async Task<HelloResult> Hello(string playerId)
        {
            await FirstSeen(playerId);
            long now = _clock.NowUnix;
            int week = WeeklyChallenge.WeekIndex(now);
            int used = (await ChallengeRuns(playerId, week)).Count;
            return new HelloResult { ServerUnix = now, Week = week, Challenge = WeeklyChallenge.For(week, _c.Balance), ChallengeAttemptsUsed = used };
        }

        public async Task<SubmitResult> SubmitCareer(string playerId, RunSubmission s)
        {
            if (s?.Start == null) return Refused("bad_submission");
            if (s.Start.Kind != RunStart.Career) return Refused("wrong_kind");
            // A day of slack: first_seen starts at the first online visit, which can follow a day of offline play.
            long firstSeen = await FirstSeen(playerId);
            int limit = Pacing.MaxCareerMonths(firstSeen - Day, _clock.NowUnix, _c.Balance);
            var v = RunVerifier.Verify(_c, _version, s, ScenarioFor, limit);
            if (!v.Ok) return Refused(v.Error);

            // A career ahead of real time still ranks — as far as real time allows — and its market share waits.
            long netWorth = v.PacedNetWorthCents;
            await _boards.Submit(GameBoards.Capital, playerId, netWorth / 100.0);
            if (!v.Paced && v.IndustryId != null && v.Months > 0)
                await _boards.Submit(GameBoards.Market(v.IndustryId), playerId, v.LastMonthRevenueCents / 100.0);
            return new SubmitResult
            {
                Ok = true, NetWorthCents = netWorth, League = Leagues.For(netWorth), Paced = v.Paced,
                Rank = await _boards.RankOf(GameBoards.Capital, playerId),
            };
        }

        /// <summary>
        /// The board resets on Monday 01:00 UTC (one hour after the week turns): a run of last week is accepted until then,
        /// a run of this week from then on ("too_early" before it, and the client's queue retries).
        /// </summary>
        public async Task<SubmitResult> SubmitChallenge(string playerId, RunSubmission s)
        {
            if (s?.Start == null) return Refused("bad_submission");
            if (s.Start.Kind != RunStart.Challenge) return Refused("wrong_kind");
            if (!int.TryParse(s.Start.ContextId, out int week)) return Refused("bad_submission");
            long now = _clock.NowUnix;
            int current = WeeklyChallenge.WeekIndex(now);
            bool inGrace = now < WeeklyChallenge.WeekStartUnix(current) + WeeklyChallenge.GraceSeconds;
            if (week == current && inGrace) return Refused("too_early");
            if (!(week == current || (week == current - 1 && inGrace))) return Refused("week_ended");

            var v = RunVerifier.Verify(_c, _version, s, ScenarioFor);
            if (!v.Ok) return Refused(v.Error);
            int months = WeeklyChallenge.For(week, _c.Balance).Months;
            if (v.Months < months) return Refused("not_finished");
            if (v.Months > months) return Refused("too_long");

            // An attempt is a distinct finished run (by state hash): a run the queue resends after a lost answer is the same
            // attempt, and an attempt is only spent once its score is on the board.
            var counted = await ChallengeRuns(playerId, week);
            if (!counted.Contains(s.StateHash))
            {
                if (counted.Count >= WeeklyChallenge.AttemptsPerWeek) return Refused("attempts_used");
                await _boards.Submit(GameBoards.WeeklyChallenge, playerId, v.NetWorthCents / 100.0);
                counted.Add(s.StateHash);
                await _players.Set(playerId, ChallengeRunsKey(week), JsonConvert.SerializeObject(counted));
            }
            return new SubmitResult
            {
                Ok = true, NetWorthCents = v.NetWorthCents, League = Leagues.For(v.NetWorthCents),
                Rank = await _boards.RankOf(GameBoards.WeeklyChallenge, playerId),
            };
        }

        public async Task<DuelView> CreateDuel(string playerId, string opponentId, string industryId)
        {
            if (opponentId == null || opponentId == playerId || !PlayerIdPattern.IsMatch(opponentId)) return new DuelView { Error = "bad_opponent" };
            if (industryId == null || !_c.Balance.Chapter2Industries.Contains(industryId)) return new DuelView { Error = "bad_industry" };
            // Each duel lands in the opponent's list too, so one player cannot flood other lists: a few challenges a day.
            string createdKey = DuelsCreatedKey(_clock.NowUnix / Day);
            int.TryParse(await _players.Get(playerId, createdKey), out int created);
            if (created >= MaxDuelsPerDay) return new DuelView { Error = "duel_limit" };
            await _players.Set(playerId, createdKey, (created + 1).ToString());

            var d = new DuelRecord { Id = _newId(), A = playerId, B = opponentId, IndustryId = industryId, CreatedUnix = _clock.NowUnix };
            await _duels.Create(d);
            await AddDuelId(playerId, d.Id);
            await AddDuelId(opponentId, d.Id);
            return View(d, playerId);
        }

        public async Task<DuelView> SubmitDuel(string playerId, string duelId, RunSubmission s)
        {
            var d = string.IsNullOrEmpty(duelId) ? null : await _duels.Get(duelId);
            if (d == null) return new DuelView { Id = duelId, Error = "not_found" };
            bool isA = d.A == playerId;
            if (!isA && d.B != playerId) return new DuelView { Id = duelId, Error = "not_in_duel" };
            var view = View(d, playerId);
            if (view.State == DuelView.Expired) return Refuse(view, "duel_expired");
            if ((isA ? d.ScoreA : d.ScoreB).HasValue) return Refuse(view, "already_submitted");
            if (s?.Start == null) return Refuse(view, "bad_submission");
            if (s.Start.Kind != RunStart.Duel || s.Start.ContextId != d.Id || s.Start.IndustryId != d.IndustryId)
                return Refuse(view, "wrong_kind");
            var v = RunVerifier.Verify(_c, _version, s, ScenarioFor);
            if (!v.Ok) return Refuse(view, v.Error);
            int months = Duels.For(d.Id, d.IndustryId).Months;
            if (v.Months != months) return Refuse(view, v.Months < months ? "not_finished" : "too_long");

            await _duels.SetScore(d.Id, isA, v.NetWorthCents);
            if (isA) d.ScoreA = v.NetWorthCents; else d.ScoreB = v.NetWorthCents;
            return View(d, playerId);
        }

        public async Task<DuelView[]> ListDuels(string playerId)
        {
            var views = new List<DuelView>();
            foreach (var id in await DuelIds(playerId))
            {
                var d = await _duels.Get(id);
                if (d != null) views.Add(View(d, playerId));
            }
            return views.OrderByDescending(v => v.CreatedUnix).Take(MaxListedDuels).ToArray();
        }

        DuelView View(DuelRecord d, string me)
        {
            bool isA = d.A == me;
            long? mine = isA ? d.ScoreA : d.ScoreB, theirs = isA ? d.ScoreB : d.ScoreA;
            string state;
            if (mine.HasValue && theirs.HasValue) state = mine > theirs ? DuelView.Won : mine < theirs ? DuelView.Lost : DuelView.Draw;
            else if (_clock.NowUnix - d.CreatedUnix > Duels.ExpiryDays * 86400L) state = DuelView.Expired;
            else state = mine.HasValue ? DuelView.WaitingThem : DuelView.WaitingMe;
            return new DuelView
            {
                Id = d.Id, Opponent = isA ? d.B : d.A, IndustryId = d.IndustryId, CreatedUnix = d.CreatedUnix,
                MyNetWorthCents = mine, TheirNetWorthCents = theirs, State = state,
            };
        }

        static DuelView Refuse(DuelView v, string error) { v.Error = error; return v; }

        static SubmitResult Refused(string error) => new SubmitResult { Ok = false, Error = error };

        static string ChallengeRunsKey(int week) => "challenge_runs_" + week;
        static string DuelsCreatedKey(long day) => "duels_created_" + day;

        /// <summary>State hashes of this week's counted challenge runs; their count is the attempts used.</summary>
        async Task<List<string>> ChallengeRuns(string playerId, int week)
        {
            var json = await _players.Get(playerId, ChallengeRunsKey(week));
            return string.IsNullOrEmpty(json) ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
        }

        async Task<long> FirstSeen(string playerId)
        {
            if (long.TryParse(await _players.Get(playerId, FirstSeenKey), out long t)) return t;
            long now = _clock.NowUnix;
            await _players.Set(playerId, FirstSeenKey, now.ToString());
            return now;
        }

        async Task<List<string>> DuelIds(string playerId)
        {
            var json = await _players.Get(playerId, DuelsKey);
            return string.IsNullOrEmpty(json) ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
        }

        async Task AddDuelId(string playerId, string duelId)
        {
            var ids = await DuelIds(playerId);
            ids.Add(duelId);
            if (ids.Count > 50) ids.RemoveRange(0, ids.Count - 50);
            await _players.Set(playerId, DuelsKey, JsonConvert.SerializeObject(ids));
        }
    }
}
