#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Analytics;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Friends;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Exceptions;
using UnityEngine;
using UnpaidCeo.Core.Online;
using LeaderboardEntry = Unity.Services.Leaderboards.Models.LeaderboardEntry;

namespace UnpaidCeo.Game.Services.Online
{
    /// <summary>
    /// Unity Gaming Services behind <see cref="IOnlineServices"/>. Created by <see cref="OnlineBootstrap"/> once the
    /// services are initialized and a player is signed in. Every call catches its own failures.
    /// </summary>
    public sealed class UgsOnlineServices : IOnlineServices, IOnlineAuth, ICloudSaves, IRunBackend, ILeaderboards, IFriends, IGameAnalytics
    {
        public const string Module = "UnpaidCeo";

        static IAuthenticationService A => AuthenticationService.Instance;

        bool _hasAccount;
        bool _friendsReady;

        public UgsOnlineServices(OnlineFlags flags)
        {
            Flags = flags;
            _hasAccount = !string.IsNullOrEmpty(A.PlayerInfo?.Username);
            Consented = AnalyticsConsent.Granted;
            if (Consented) Try(() => AnalyticsService.Instance.StartDataCollection());
        }

        public bool Available => true;
        public IOnlineAuth Auth => this;
        public ICloudSaves Saves => this;
        public IRunBackend Runs => this;
        public ILeaderboards Boards => this;
        public IFriends Friends => this;
        public OnlineFlags Flags { get; }
        public IGameAnalytics Analytics => this;

        // ---- Auth ----

        public bool SignedIn => A.IsSignedIn;
        public bool IsGuest => !_hasAccount;
        public string? PlayerId => A.IsSignedIn ? A.PlayerId : null;
        public string? PlayerName => A.IsSignedIn ? A.PlayerName : null;

        public async Task<AuthResult> SignInAnonymously()
        {
            try
            {
                if (!A.IsSignedIn) await A.SignInAnonymouslyAsync();
                return AuthResult.Ok;
            }
            catch (Exception e) { return Map(e, signUp: false); }
        }

        public async Task<AuthResult> SignUp(string user, string pass)
        {
            if (!AccountRules.UsernameOk(user)) return AuthResult.InvalidCredentials;
            if (!AccountRules.PasswordOk(pass)) return AuthResult.WeakPassword;
            try
            {
                if (A.IsSignedIn) await A.AddUsernamePasswordAsync(user, pass);   // the guest becomes the account: same player id
                else await A.SignUpWithUsernamePasswordAsync(user, pass);
                _hasAccount = true;
                return AuthResult.Ok;
            }
            catch (Exception e) { return Map(e, signUp: true); }
        }

        public async Task<AuthResult> SignIn(string user, string pass)
        {
            if (!AccountRules.UsernameOk(user) || string.IsNullOrEmpty(pass)) return AuthResult.InvalidCredentials;
            try
            {
                // Keep the cached session: if the account sign-in fails, the guest comes back from it.
                if (A.IsSignedIn) A.SignOut(clearCredentials: false);
                await A.SignInWithUsernamePasswordAsync(user, pass);
                _hasAccount = true;
                _friendsReady = false;
                return AuthResult.Ok;
            }
            catch (Exception e)
            {
                var result = Map(e, signUp: false);
                if (!A.IsSignedIn) await SignInAnonymously();
                return result;
            }
        }

        public async Task<AuthResult> SetName(string name)
        {
            try { await A.UpdatePlayerNameAsync(name); return AuthResult.Ok; }
            catch (Exception e) { return Map(e, signUp: false); }
        }

        public async Task SignOut()
        {
            try
            {
                A.SignOut(clearCredentials: true);
                _hasAccount = false;
                _friendsReady = false;
                await A.SignInAnonymouslyAsync();
            }
            catch (Exception e) { Warn(e); }
        }

        /// <summary>Network trouble reads as Offline; a taken name or bad credentials as such; anything else as Error.</summary>
        static AuthResult Map(Exception e, bool signUp)
        {
            Warn(e);
            if (!(e is RequestFailedException r)) return AuthResult.Error;
            int code = r.ErrorCode;
            if (code == CommonErrorCodes.TransportError || code == CommonErrorCodes.Timeout || code == CommonErrorCodes.ServiceUnavailable)
                return AuthResult.Offline;
            if (signUp)
                return code == CommonErrorCodes.Conflict || code == AuthenticationErrorCodes.AccountAlreadyLinked ? AuthResult.NameTaken : AuthResult.Error;
            return code == CommonErrorCodes.InvalidRequest || code == CommonErrorCodes.NotFound || code == CommonErrorCodes.Forbidden
                || code == AuthenticationErrorCodes.InvalidParameters ? AuthResult.InvalidCredentials : AuthResult.Error;
        }

        // ---- Cloud save ----

        public async Task<CloudLoad> Load()
        {
            try
            {
                var items = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { CloudSaveEnvelope.Key });
                return CloudLoad.Of(items.TryGetValue(CloudSaveEnvelope.Key, out var item)
                    ? OnlineJson.Deserialize<CloudSaveEnvelope>(item.Value.GetAsString())
                    : null);
            }
            catch (Exception e) { Warn(e); return CloudLoad.Unreachable; }
        }

        public async Task<bool> Save(CloudSaveEnvelope e)
        {
            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { CloudSaveEnvelope.Key, OnlineJson.Serialize(e) } });
                return true;
            }
            catch (Exception ex) { Warn(ex); return false; }
        }

        // ---- Runs (Cloud Code module; arguments and results are OnlineJson text) ----

        public async Task<HelloResult?> Hello() => await Call<HelloResult>("Hello", new Dictionary<string, object>());

        public async Task<SubmitResult> SubmitCareer(RunSubmission s) =>
            await Call<SubmitResult>("SubmitCareer", Args(("submission", OnlineJson.Serialize(s)))) ?? Refused();

        public async Task<SubmitResult> SubmitChallenge(RunSubmission s) =>
            await Call<SubmitResult>("SubmitChallenge", Args(("submission", OnlineJson.Serialize(s)))) ?? Refused();

        public async Task<DuelView> CreateDuel(string opponentId, string industryId) =>
            await Call<DuelView>("CreateDuel", Args(("opponentId", opponentId), ("industryId", industryId)))
            ?? new DuelView { Error = OnlineErrors.Offline };

        public async Task<DuelView> SubmitDuel(string duelId, RunSubmission s) =>
            await Call<DuelView>("SubmitDuel", Args(("duelId", duelId), ("submission", OnlineJson.Serialize(s))))
            ?? new DuelView { Id = duelId, Error = OnlineErrors.Offline };

        public async Task<DuelView[]> ListDuels() =>
            await Call<DuelView[]>("ListDuels", new Dictionary<string, object>()) ?? Array.Empty<DuelView>();

        static async Task<T?> Call<T>(string function, Dictionary<string, object> args) where T : class
        {
            try { return OnlineJson.Deserialize<T>(await CloudCodeService.Instance.CallModuleEndpointAsync<string>(Module, function, args)); }
            catch (Exception e) { Warn(e); return null; }
        }

        static Dictionary<string, object> Args(params (string Key, object Value)[] pairs) => pairs.ToDictionary(p => p.Key, p => p.Value);

        static SubmitResult Refused() => new SubmitResult { Ok = false, Error = OnlineErrors.Offline };

        // ---- Leaderboards (the API ranks from 0; the game shows ranks from 1) ----

        public async Task<RankEntry[]> Top(string board, int limit)
        {
            try
            {
                var page = await LeaderboardsService.Instance.GetScoresAsync(board, new GetScoresOptions { Limit = limit });
                return page.Results.Select(ToRank).ToArray();
            }
            catch (Exception e) { Warn(e); return Array.Empty<RankEntry>(); }
        }

        public async Task<RankEntry?> Mine(string board)
        {
            try { return ToRank(await LeaderboardsService.Instance.GetPlayerScoreAsync(board)); }
            catch (LeaderboardsException e) when (e.Reason == LeaderboardsExceptionReason.EntryNotFound) { return null; }
            catch (Exception e) { Warn(e); return null; }
        }

        static RankEntry ToRank(LeaderboardEntry e) =>
            new RankEntry { Rank = e.Rank + 1, PlayerId = e.PlayerId, Name = e.PlayerName, Score = e.Score, Tier = e.Tier };

        // ---- Friends (initialized on first use, again after an account switch) ----

        async Task<bool> FriendsReady()
        {
            if (_friendsReady) return true;
            try { await FriendsService.Instance.InitializeAsync(); _friendsReady = true; }
            catch (Exception e) { Warn(e); }
            return _friendsReady;
        }

        public async Task<FriendView[]> List() => await FriendsReady()
            ? FriendsService.Instance.Friends.Select(r => new FriendView { PlayerId = r.Member.Id, Name = r.Member.Profile?.Name }).ToArray()
            : Array.Empty<FriendView>();

        public async Task<FriendView[]> Requests() => await FriendsReady()
            ? FriendsService.Instance.IncomingFriendRequests.Select(r => new FriendView { PlayerId = r.Member.Id, Name = r.Member.Profile?.Name }).ToArray()
            : Array.Empty<FriendView>();

        public async Task<bool> AddByName(string name)
        {
            if (!await FriendsReady()) return false;
            try { await FriendsService.Instance.AddFriendByNameAsync(name); return true; }
            catch (Exception e) { Warn(e); return false; }
        }

        public async Task<bool> Accept(string playerId)
        {
            if (!await FriendsReady()) return false;
            try { await FriendsService.Instance.AddFriendAsync(playerId); return true; }
            catch (Exception e) { Warn(e); return false; }
        }

        public async Task<bool> Decline(string playerId)
        {
            if (!await FriendsReady()) return false;
            try { await FriendsService.Instance.DeleteIncomingFriendRequestAsync(playerId); return true; }
            catch (Exception e) { Warn(e); return false; }
        }

        public async Task<bool> Remove(string playerId)
        {
            if (!await FriendsReady()) return false;
            try { await FriendsService.Instance.DeleteFriendAsync(playerId); return true; }
            catch (Exception e) { Warn(e); return false; }
        }

        // ---- Analytics: nothing is collected until the player consents ----

        public bool Consented { get; private set; }

        public void SetConsent(bool on)
        {
            Consented = on;
            AnalyticsConsent.Set(on);
            Try(() => { if (on) AnalyticsService.Instance.StartDataCollection(); else AnalyticsService.Instance.StopDataCollection(); });
        }

        public void Record(string name, IDictionary<string, object>? args = null)
        {
            if (!Consented) return;
            Try(() =>
            {
                var e = new CustomEvent(name);
                if (args != null) foreach (var kv in args) e.Add(kv.Key, kv.Value);
                AnalyticsService.Instance.RecordEvent(e);
            });
        }

        static void Try(Action a) { try { a(); } catch (Exception e) { Warn(e); } }

        static void Warn(Exception e) => Debug.LogWarning("[online] " + e.GetType().Name + ": " + e.Message);
    }
}
