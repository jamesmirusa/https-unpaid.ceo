using System.Linq;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Online;
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

        /// <summary>Plays a challenge or duel scenario for <paramref name="months"/> months.</summary>
        public static (RunSubmission Sub, long NetWorthCents) PlayScenario(RunStart start, int months, IBot bot)
        {
            var c = BotRunner.Content();
            var e = new GameEngine(c);
            var s = GameEngine.Start(c, start, r => Scenarios.For(r, c.Balance));
            while (s.Month <= months)
            {
                foreach (var scene in e.BeginMonth(s).ToList())
                    e.ChooseStoryletOption(s, scene.Id, bot.PickChoice(e, s, scene));
                var report = e.EndMonth(s, bot.Decide(e, s));
                bot.AfterMonth(e, s, report);
            }
            return (RunSubmission.From(start, s, ServerContent.Version), Chapters.NetWorth(s, c).Cents);
        }
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
