#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnpaidCeo.Core.Online;

namespace UnpaidCeo.Game.Services.Online
{
    /// <summary>
    /// The game without the internet (unlinked project, no network, UGS down, or the online flag off): every call
    /// completes at once with an Offline/empty result, so the game plays exactly as it does offline.
    /// </summary>
    public sealed class OfflineServices : IOnlineServices, IOnlineAuth, ICloudSaves, IRunBackend, ILeaderboards, IFriends, IGameAnalytics
    {
        public OfflineServices(OnlineFlags? flags = null) { Flags = flags ?? new OnlineFlags { Online = false }; }

        public bool Available => false;
        public IOnlineAuth Auth => this;
        public ICloudSaves Saves => this;
        public IRunBackend Runs => this;
        public ILeaderboards Boards => this;
        public IFriends Friends => this;
        public OnlineFlags Flags { get; }
        public IGameAnalytics Analytics => this;

        // Auth
        public bool SignedIn => false;
        public bool IsGuest => true;
        public string? PlayerId => null;
        public string? PlayerName => null;
        public Task<AuthResult> SignInAnonymously() => Task.FromResult(AuthResult.Offline);
        public Task<AuthResult> SignUp(string user, string pass) => Task.FromResult(AuthResult.Offline);
        public Task<AuthResult> SignIn(string user, string pass) => Task.FromResult(AuthResult.Offline);
        public Task<AuthResult> SetName(string name) => Task.FromResult(AuthResult.Offline);
        public Task SignOut() => Task.CompletedTask;

        // Saves
        public Task<CloudLoad> Load() => Task.FromResult(CloudLoad.Unreachable);
        public Task<bool> Save(CloudSaveEnvelope e) => Task.FromResult(false);

        // Runs
        public Task<HelloResult?> Hello() => Task.FromResult<HelloResult?>(null);
        public Task<SubmitResult> SubmitCareer(RunSubmission s) => Task.FromResult(Refused());
        public Task<SubmitResult> SubmitChallenge(RunSubmission s) => Task.FromResult(Refused());
        public Task<DuelView> CreateDuel(string opponentId, string industryId) => Task.FromResult(new DuelView { Error = OnlineErrors.Offline });
        public Task<DuelView> SubmitDuel(string duelId, RunSubmission s) => Task.FromResult(new DuelView { Id = duelId, Error = OnlineErrors.Offline });
        public Task<DuelView[]> ListDuels() => Task.FromResult(Array.Empty<DuelView>());

        // Boards
        public Task<RankEntry[]> Top(string board, int limit) => Task.FromResult(Array.Empty<RankEntry>());
        public Task<RankEntry?> Mine(string board) => Task.FromResult<RankEntry?>(null);

        // Friends
        public Task<FriendView[]> List() => Task.FromResult(Array.Empty<FriendView>());
        public Task<FriendView[]> Requests() => Task.FromResult(Array.Empty<FriendView>());
        public Task<bool> AddByName(string name) => Task.FromResult(false);
        public Task<bool> Accept(string playerId) => Task.FromResult(false);
        public Task<bool> Decline(string playerId) => Task.FromResult(false);
        public Task<bool> Remove(string playerId) => Task.FromResult(false);

        // Analytics: the choice is kept for the session; nothing is recorded offline.
        public bool Consented { get; private set; }
        public void SetConsent(bool on) => Consented = on;
        public void Record(string name, IDictionary<string, object>? args = null) { }

        static SubmitResult Refused() => new SubmitResult { Ok = false, Error = OnlineErrors.Offline };
    }
}
