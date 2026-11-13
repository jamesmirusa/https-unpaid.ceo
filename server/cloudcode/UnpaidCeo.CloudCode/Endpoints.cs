using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using UnpaidCeo.Core.Online;

namespace UnpaidCeo.CloudCode
{
    /// <summary>
    /// The module's functions, called by signed-in players. Submissions arrive and results leave as OnlineJson text, so
    /// the game's own types (money, decision logs) cross the wire exactly; every rule lives in <see cref="GameServer"/>.
    /// </summary>
    public sealed class Endpoints
    {
        readonly IGameApiClient _api;

        public Endpoints(IGameApiClient api) { _api = api; }

        GameServer Server(IExecutionContext ctx) => new GameServer(ServerContent.Db, ServerContent.Version,
            new UgsPlayerStore(_api, ctx), new UgsDuelStore(_api, ctx), new UgsBoards(_api, ctx), SystemClock.Instance);

        static string Out(object result) => OnlineJson.Serialize(result);

        static bool NoPlayer(IExecutionContext ctx) => string.IsNullOrEmpty(ctx.PlayerId);

        static readonly string NoPlayerResult = Out(new SubmitResult { Ok = false, Error = "no_player" });

        [CloudCodeFunction("Hello")]
        public async Task<string> Hello(IExecutionContext context) =>
            NoPlayer(context) ? NoPlayerResult : Out(await Server(context).Hello(context.PlayerId));

        [CloudCodeFunction("SubmitCareer")]
        public async Task<string> SubmitCareer(IExecutionContext context, string submission) =>
            NoPlayer(context) ? NoPlayerResult
                : Out(await Server(context).SubmitCareer(context.PlayerId, OnlineJson.Deserialize<RunSubmission>(submission)));

        [CloudCodeFunction("SubmitChallenge")]
        public async Task<string> SubmitChallenge(IExecutionContext context, string submission) =>
            NoPlayer(context) ? NoPlayerResult
                : Out(await Server(context).SubmitChallenge(context.PlayerId, OnlineJson.Deserialize<RunSubmission>(submission)));

        [CloudCodeFunction("CreateDuel")]
        public async Task<string> CreateDuel(IExecutionContext context, string opponentId, string industryId) =>
            NoPlayer(context) ? Out(new DuelView { Error = "no_player" })
                : Out(await Server(context).CreateDuel(context.PlayerId, opponentId, industryId));

        [CloudCodeFunction("SubmitDuel")]
        public async Task<string> SubmitDuel(IExecutionContext context, string duelId, string submission) =>
            NoPlayer(context) ? Out(new DuelView { Id = duelId, Error = "no_player" })
                : Out(await Server(context).SubmitDuel(context.PlayerId, duelId, OnlineJson.Deserialize<RunSubmission>(submission)));

        [CloudCodeFunction("ListDuels")]
        public async Task<string> ListDuels(IExecutionContext context) =>
            NoPlayer(context) ? "[]" : Out(await Server(context).ListDuels(context.PlayerId));
    }
}
