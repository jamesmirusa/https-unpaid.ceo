#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using UnpaidCeo.Core.Online;

namespace UnpaidCeo.Game.Services.Online
{
    /// <summary>
    /// Everything online, behind one seam: Unity Gaming Services when the project is linked and reachable, an offline
    /// stand-in otherwise (and a fake in tests). No member throws; failures come back as Offline/empty results.
    /// </summary>
    public interface IOnlineServices
    {
        bool Available { get; }
        IOnlineAuth Auth { get; }
        ICloudSaves Saves { get; }
        IRunBackend Runs { get; }
        ILeaderboards Boards { get; }
        IFriends Friends { get; }
        OnlineFlags Flags { get; }
        IGameAnalytics Analytics { get; }
    }

    public enum AuthResult { Ok, InvalidCredentials, NameTaken, WeakPassword, Offline, Error }

    public interface IOnlineAuth
    {
        bool SignedIn { get; }
        /// <summary>Signed in anonymously (no username yet).</summary>
        bool IsGuest { get; }
        string? PlayerId { get; }
        string? PlayerName { get; }
        Task<AuthResult> SignInAnonymously();
        /// <summary>Creates an account; a signed-in guest keeps its player id (progress and rank stay).</summary>
        Task<AuthResult> SignUp(string user, string pass);
        /// <summary>Switches to an existing account.</summary>
        Task<AuthResult> SignIn(string user, string pass);
        Task<AuthResult> SetName(string name);
        /// <summary>Signs out and continues as a new guest; the device save is left alone.</summary>
        Task SignOut();
    }

    /// <summary>The player's save in Cloud Save (key <see cref="Key"/>).</summary>
    public sealed class CloudSaveEnvelope
    {
        public const string Key = "save_v2";
        public string? SaveJson { get; set; }
        public string? MetaJson { get; set; }
        /// <summary>Months played in the career, so the longer game wins a conflict.</summary>
        public int Months { get; set; }
        public long UpdatedUnix { get; set; }
    }

    /// <summary>
    /// A cloud-save read. "No save" and "could not reach the cloud" are different answers: only a reachable empty
    /// cloud may be filled from the device, so a network blip never uploads a newcomer over real progress.
    /// </summary>
    public sealed class CloudLoad
    {
        public static readonly CloudLoad Unreachable = new CloudLoad(false, null);
        public static CloudLoad Of(CloudSaveEnvelope? envelope) => new CloudLoad(true, envelope);

        CloudLoad(bool reachable, CloudSaveEnvelope? envelope) { Reachable = reachable; Envelope = envelope; }

        public bool Reachable { get; }
        /// <summary>The save, or null when the cloud has none (or was unreachable).</summary>
        public CloudSaveEnvelope? Envelope { get; }
    }

    public interface ICloudSaves
    {
        Task<CloudLoad> Load();
        Task<bool> Save(CloudSaveEnvelope e);
    }

    /// <summary>The Cloud Code module "UnpaidCeo" (server-validated runs).</summary>
    public interface IRunBackend
    {
        /// <summary>The server's clock and this week's challenge, or null offline.</summary>
        Task<HelloResult?> Hello();
        Task<SubmitResult> SubmitCareer(RunSubmission s);
        Task<SubmitResult> SubmitChallenge(RunSubmission s);
        Task<DuelView> CreateDuel(string opponentId, string industryId);
        Task<DuelView> SubmitDuel(string duelId, RunSubmission s);
        Task<DuelView[]> ListDuels();
    }

    public sealed class RankEntry
    {
        /// <summary>1-based.</summary>
        public int Rank { get; set; }
        public string? PlayerId { get; set; }
        public string? Name { get; set; }
        public double Score { get; set; }
        public string? Tier { get; set; }
    }

    public interface ILeaderboards
    {
        Task<RankEntry[]> Top(string board, int limit);
        /// <summary>The player's own entry, or null when they have no score there.</summary>
        Task<RankEntry?> Mine(string board);
    }

    public sealed class FriendView
    {
        public string? PlayerId { get; set; }
        public string? Name { get; set; }
    }

    public interface IFriends
    {
        Task<FriendView[]> List();
        Task<FriendView[]> Requests();
        Task<bool> AddByName(string name);
        Task<bool> Accept(string playerId);
        /// <summary>Turns down an incoming request.</summary>
        Task<bool> Decline(string playerId);
        Task<bool> Remove(string playerId);
    }

    /// <summary>Analytics events (spec §7.7) are recorded only after the player consents.</summary>
    public interface IGameAnalytics
    {
        bool Consented { get; }
        void SetConsent(bool on);
        void Record(string name, IDictionary<string, object>? args = null);
    }

    /// <summary>Remote Config flags (the only thing Remote Config carries; balance ships with builds).</summary>
    public sealed class OnlineFlags
    {
        public bool Online { get; set; } = true;
        public bool Challenge { get; set; } = true;
        public bool Duels { get; set; } = true;
        public bool MarketWars { get; set; } = true;
    }

    /// <summary>Refusal codes the client produces itself (the server's come back in Error fields).</summary>
    public static class OnlineErrors
    {
        public const string Offline = "offline";
        public const string Failed = "failed";
        /// <summary>The server was reached but failed on the request (a module error or a timeout on the run).</summary>
        public const string ServerError = "server_error";
    }
}
