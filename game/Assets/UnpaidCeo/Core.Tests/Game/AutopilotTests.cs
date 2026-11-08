using NUnit.Framework;
using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Core.Tests.Game
{
    public class AutopilotTests
    {
        const long H = 3600;

        [TestCase(3 * H + 59 * 60, 0)]
        [TestCase(4 * H, 1)]
        [TestCase(9 * H, 2)]
        [TestCase(100 * H, 2)]
        [TestCase(-5 * H, 0)]
        public void Autopilot_months(long elapsedSeconds, int expected)
        {
            var b = TestContent.Db().Balance;
            const long last = 1_800_000_000;
            Assert.AreEqual(expected, Autopilot.MonthsToRun(last, last + elapsedSeconds, b));
        }

        [Test]
        public void Autopilot_runs_months_without_storylets()
        {
            var e = new GameEngine(TestContent.Db());
            var s = e.NewGame(1, TestContent.Hero);
            s.LastDecisions = TestContent.Decisions(50, 15, 25, 10);
            var reports = Autopilot.Run(e, s, 2);
            Assert.AreEqual(2, reports.Count);
            Assert.AreEqual(3, s.Month);
            Assert.AreEqual(0, s.StoryletLastMonth.Count);
        }
    }
}
