using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Core.Tests.Game;
using UnpaidCeo.Core.Tests.Online;

namespace UnpaidCeo.CloudCode.Tests
{
    /// <summary>Honest runs played by the Unity test bots and packed as the client would submit them.</summary>
    static class ServerRuns
    {
        public const long Day = 86400;

        public static RunSubmission Career(GameState s) => RunSubmission.From(OnlineRuns.Career(s), s, ServerContent.Version);

        public static RunStart ChallengeStart(int week) =>
            new RunStart { Kind = RunStart.Challenge, ContextId = week.ToString(), Hero = TestContent.Hero };

        public static RunStart DuelStart(string duelId, string industryId) =>
            new RunStart { Kind = RunStart.Duel, ContextId = duelId, IndustryId = industryId, Hero = TestContent.Hero };

        /// <summary>
        /// Plays a challenge or duel scenario for <paramref name="months"/> months the way the client's RunController does:
        /// a business sprint of month plans (and an optional loan) — no scenes, no quizzes.
        /// </summary>
        public static (RunSubmission Sub, long NetWorthCents) PlayScenario(RunStart start, int months, IBot bot, bool withLoan = false)
        {
            var c = BotRunner.Content();
            var e = new GameEngine(c);
            var s = GameEngine.Start(c, start, r => Scenarios.For(r, c.Balance));
            if (withLoan && !e.TakeLoan(s, Money.FromCents(500_000), 12)) throw new System.InvalidOperationException("loan refused");
            while (s.Month <= months) e.EndMonth(s, bot.Decide(e, s));
            return (RunSubmission.From(start, s, ServerContent.Version), Chapters.NetWorth(s, c).Cents);
        }
    }

    /// <summary>A third honest player for attempt counting: the optimal plan in odd months, the casual one in even months.</summary>
    sealed class MixedBot : IBot
    {
        readonly OptimalBot _optimal = new OptimalBot();
        readonly CasualBot _casual = new CasualBot();
        IBot For(GameState s) => s.Month % 2 == 1 ? (IBot)_optimal : _casual;
        public MonthDecisions Decide(GameEngine e, GameState s) => For(s).Decide(e, s);
        public string PickChoice(GameEngine e, GameState s, StoryletDef scene) => For(s).PickChoice(e, s, scene);
        public void AfterMonth(GameEngine e, GameState s, MonthReport report) { }
        public void StartChapter2(GameEngine e, GameState s, string industry) => e.ChooseIndustry(s, industry);
    }

    /// <summary>A GameServer over in-memory ports, with a settable clock and predictable duel ids.</summary>
    sealed class Harness
    {
        public readonly FakePlayers Players = new FakePlayers();
        public readonly FakeDuels Duels = new FakeDuels();
        public readonly FakeBoards Boards = new FakeBoards();
        public readonly FakeClock Clock = new FakeClock();
        public readonly GameServer Server;
        int _ids;

        public Harness(long nowUnix)
        {
            Clock.NowUnix = nowUnix;
            Server = new GameServer(ServerContent.Db, ServerContent.Version, Players, Duels, Boards, Clock, () => "duel" + ++_ids);
        }
    }
}
