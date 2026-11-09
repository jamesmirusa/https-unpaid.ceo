#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Game.Services.Online;

namespace UnpaidCeo.Game.Tests.Fakes
{
    /// <summary>
    /// In-memory online services for the stage-2B tests: accounts, cloud saves, boards, friends and duels. Turn
    /// <see cref="Online"/> off and every call answers like <see cref="OfflineServices"/>. Friends added by name become
    /// friends at once (the real service sends a request first); incoming requests are queued with QueueRequest.
    /// </summary>
    public sealed class FakeOnlineServices : IOnlineServices, IOnlineAuth, ICloudSaves, IRunBackend, ILeaderboards, IFriends, IGameAnalytics
    {
        readonly Dictionary<string, (string Pass, string PlayerId)> _accounts = new Dictionary<string, (string, string)>();
        readonly Dictionary<string, string> _names = new Dictionary<string, string>();
        readonly Dictionary<string, string> _saves = new Dictionary<string, string>();
        readonly Dictionary<string, Dictionary<string, RankEntry>> _boards = new Dictionary<string, Dictionary<string, RankEntry>>();
        readonly Dictionary<string, HashSet<string>> _friends = new Dictionary<string, HashSet<string>>();
        readonly Dictionary<string, List<string>> _requests = new Dictionary<string, List<string>>();
        readonly List<DuelView> _duels = new List<DuelView>();
        int _ids;
        string? _player;
        bool _guest = true;

        public bool Online { get; set; } = true;
        public long NowUnix { get; set; } = 1_800_000_000;
        public readonly List<RunSubmission> Careers = new List<RunSubmission>();
        public readonly List<RunSubmission> Challenges = new List<RunSubmission>();
        public readonly List<(string Name, IDictionary<string, object>? Args)> Events = new List<(string, IDictionary<string, object>?)>();
        public Func<RunSubmission, SubmitResult> CareerResult = s => new SubmitResult { Ok = true, League = Leagues.All[0], Rank = 1 };
        public Func<RunSubmission, SubmitResult> ChallengeResult = s => new SubmitResult { Ok = true, League = Leagues.All[0], Rank = 1 };
        public Func<RunSubmission, long> DuelNetWorth = s => 0;

        public bool Available => Online;
        public IOnlineAuth Auth => this;
        public ICloudSaves Saves => this;
        public IRunBackend Runs => this;
        public ILeaderboards Boards => this;
        public IFriends Friends => this;
        public OnlineFlags Flags { get; } = new OnlineFlags();
        public IGameAnalytics Analytics => this;

        // Test setup
        public void AddPlayer(string playerId, string name) => _names[playerId] = name;
        public void SetScore(string board, string playerId, double score, string? tier = null)
        {
            if (!_boards.TryGetValue(board, out var b)) _boards[board] = b = new Dictionary<string, RankEntry>();
            b[playerId] = new RankEntry { PlayerId = playerId, Name = _names.TryGetValue(playerId, out var n) ? n : playerId, Score = score, Tier = tier };
        }
        public void QueueRequest(string fromPlayerId) { if (_player != null) Requests(_player).Add(fromPlayerId); }
        public CloudSaveEnvelope? CloudSaveOf(string playerId) =>
            _saves.TryGetValue(playerId, out var json) ? OnlineJson.Deserialize<CloudSaveEnvelope>(json) : null;
        public void PutCloudSave(string playerId, CloudSaveEnvelope e) => _saves[playerId] = OnlineJson.Serialize(e);

        // Auth
        public bool SignedIn => Online && _player != null;
        public bool IsGuest => _guest;
        public string? PlayerId => _player;
        public string? PlayerName => _player != null && _names.TryGetValue(_player, out var n) ? n : null;

        public Task<AuthResult> SignInAnonymously()
        {
            if (!Online) return Task.FromResult(AuthResult.Offline);
            if (_player == null) { _player = "guest" + ++_ids; _guest = true; }
            return Task.FromResult(AuthResult.Ok);
        }

        /// <summary>Sign-up requests that reached the "server".</summary>
        public int SignUpCalls { get; private set; }

        public Task<AuthResult> SignUp(string user, string pass)
        {
            SignUpCalls++;
            if (!Online) return Task.FromResult(AuthResult.Offline);
            if (!AccountRules.UsernameOk(user)) return Task.FromResult(AuthResult.InvalidCredentials);
            if (!AccountRules.PasswordOk(pass)) return Task.FromResult(AuthResult.WeakPassword);
            if (_accounts.ContainsKey(user.ToLowerInvariant())) return Task.FromResult(AuthResult.NameTaken);
            _player ??= "player" + ++_ids;
            _accounts[user.ToLowerInvariant()] = (pass, _player);
            _names[_player] = user;
            _guest = false;
            return Task.FromResult(AuthResult.Ok);
        }

        public Task<AuthResult> SignIn(string user, string pass)
        {
            if (!Online) return Task.FromResult(AuthResult.Offline);
            if (!_accounts.TryGetValue(user.ToLowerInvariant(), out var a) || a.Pass != pass) return Task.FromResult(AuthResult.InvalidCredentials);
            _player = a.PlayerId;
            _guest = false;
            return Task.FromResult(AuthResult.Ok);
        }

        public Task<AuthResult> SetName(string name)
        {
            if (!Online || _player == null) return Task.FromResult(AuthResult.Offline);
            _names[_player] = name;
            return Task.FromResult(AuthResult.Ok);
        }

        public Task SignOut()
        {
            if (Online) { _player = "guest" + ++_ids; _guest = true; }
            return Task.CompletedTask;
        }

        // Saves (stored as text, so callers never share objects with the "cloud")
        public Task<CloudLoad> Load() =>
            Task.FromResult(Online && _player != null ? CloudLoad.Of(CloudSaveOf(_player)) : CloudLoad.Unreachable);

        public Task<bool> Save(CloudSaveEnvelope e)
        {
            if (!Online || _player == null) return Task.FromResult(false);
            PutCloudSave(_player, e);
            return Task.FromResult(true);
        }

        // Runs
        public Task<HelloResult?> Hello() => Task.FromResult(Online
            ? new HelloResult { ServerUnix = NowUnix, Week = WeeklyChallenge.WeekIndex(NowUnix) }
            : null);

        public Task<SubmitResult> SubmitCareer(RunSubmission s)
        {
            if (!Online) return Task.FromResult(Refused());
            Careers.Add(s);
            return Task.FromResult(CareerResult(s));
        }

        public Task<SubmitResult> SubmitChallenge(RunSubmission s)
        {
            if (!Online) return Task.FromResult(Refused());
            Challenges.Add(s);
            return Task.FromResult(ChallengeResult(s));
        }

        public Task<DuelView> CreateDuel(string opponentId, string industryId)
        {
            if (!Online) return Task.FromResult(new DuelView { Error = OnlineErrors.Offline });
            var d = new DuelView { Id = "duel" + ++_ids, Opponent = opponentId, IndustryId = industryId, CreatedUnix = NowUnix, State = DuelView.WaitingMe };
            _duels.Add(d);
            return Task.FromResult(Copy(d));
        }

        public Task<DuelView> SubmitDuel(string duelId, RunSubmission s)
        {
            if (!Online) return Task.FromResult(new DuelView { Id = duelId, Error = OnlineErrors.Offline });
            var d = _duels.FirstOrDefault(x => x.Id == duelId);
            if (d == null) return Task.FromResult(new DuelView { Id = duelId, Error = "not_found" });
            d.MyNetWorthCents = DuelNetWorth(s);
            d.State = DuelView.WaitingThem;
            return Task.FromResult(Copy(d));
        }

        public Task<DuelView[]> ListDuels() => Task.FromResult(Online
            ? _duels.OrderByDescending(d => d.CreatedUnix).Select(Copy).ToArray()
            : Array.Empty<DuelView>());

        // Boards
        public Task<RankEntry[]> Top(string board, int limit) => Task.FromResult(Online ? Ranked(board).Take(limit).ToArray() : Array.Empty<RankEntry>());
        public Task<RankEntry?> Mine(string board) => Task.FromResult(Online ? Ranked(board).FirstOrDefault(e => e.PlayerId == _player) : null);

        IEnumerable<RankEntry> Ranked(string board) =>
            !_boards.TryGetValue(board, out var b) ? Enumerable.Empty<RankEntry>()
                : b.Values.OrderByDescending(e => e.Score).Select((e, i) => new RankEntry { Rank = i + 1, PlayerId = e.PlayerId, Name = e.Name, Score = e.Score, Tier = e.Tier });

        // Friends
        public Task<FriendView[]> List() => Task.FromResult(Online && _player != null
            ? FriendsOf(_player).Select(View).ToArray() : Array.Empty<FriendView>());
        public Task<FriendView[]> Requests() => Task.FromResult(Online && _player != null
            ? Requests(_player).Select(View).ToArray() : Array.Empty<FriendView>());

        public Task<bool> AddByName(string name)
        {
            var id = _names.FirstOrDefault(kv => kv.Value == name).Key;
            if (!Online || _player == null || id == null || id == _player) return Task.FromResult(false);
            FriendsOf(_player).Add(id);
            FriendsOf(id).Add(_player);
            return Task.FromResult(true);
        }

        public Task<bool> Accept(string playerId)
        {
            if (!Online || _player == null || !Requests(_player).Remove(playerId)) return Task.FromResult(false);
            FriendsOf(_player).Add(playerId);
            FriendsOf(playerId).Add(_player);
            return Task.FromResult(true);
        }

        public Task<bool> Remove(string playerId)
        {
            if (!Online || _player == null || !FriendsOf(_player).Remove(playerId)) return Task.FromResult(false);
            FriendsOf(playerId).Remove(_player);
            return Task.FromResult(true);
        }

        HashSet<string> FriendsOf(string id) => _friends.TryGetValue(id, out var f) ? f : _friends[id] = new HashSet<string>();
        List<string> Requests(string id) => _requests.TryGetValue(id, out var r) ? r : _requests[id] = new List<string>();
        FriendView View(string id) => new FriendView { PlayerId = id, Name = _names.TryGetValue(id, out var n) ? n : null };

        // Analytics
        public bool Consented { get; private set; }
        public void SetConsent(bool on) => Consented = on;
        public void Record(string name, IDictionary<string, object>? args = null) { if (Consented && Online) Events.Add((name, args)); }

        static SubmitResult Refused() => new SubmitResult { Ok = false, Error = OnlineErrors.Offline };
        static DuelView Copy(DuelView d) => OnlineJson.Deserialize<DuelView>(OnlineJson.Serialize(d))!;
    }
}
