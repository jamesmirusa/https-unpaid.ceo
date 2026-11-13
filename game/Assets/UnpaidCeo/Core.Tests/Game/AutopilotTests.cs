using System.Linq;
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
        public void Autopilot_runs_weeks_without_storylets()
        {
            var e = new GameEngine(TestContent.Db());
            var s = e.NewGame(1, TestContent.Hero);
            s.LastDecisions = TestContent.Decisions(50, 15, 25, 10);
            var reports = Autopilot.Run(e, s, 2);
            Assert.AreEqual(2, reports.Count);
            Assert.AreEqual(3, s.Week, "a turn is a week");
            Assert.AreEqual(0, s.StoryletLastMonth.Count);
        }

        [Test]
        public void Autopilot_plays_the_week_the_player_already_decided_then_the_routine()
        {
            // The player tapped a saving this week and left for hours: the week they decided is the one played.
            var e = new GameEngine(TestContent.Db());
            var s = e.NewGame(1, TestContent.Hero);
            s.Cash = UnpaidCeo.Core.Primitives.Money.FromDollars(500);
            s.LastDecisions = TestContent.Decisions(55, 15, 20, 10);
            var thisWeek = s.LastDecisions.Routine();
            thisWeek.FreeActions.Add("save");
            long reserve = s.Reserve.Cents;
            var reports = Autopilot.Run(e, s, 2, thisWeek);
            Assert.AreEqual(2, reports.Count);
            Assert.IsEmpty(reports[0].RejectedActions);
            Assert.AreEqual(reserve + e.Content.FreeActions["save"].Effects
                .OfType<UnpaidCeo.Core.Narrative.ReserveEffect>().Sum(r => r.Amount.Cents), s.Reserve.Cents,
                "the tapped saving happened once: the second week is the routine");
        }

        [Test]
        public void Autopilot_does_not_repeat_last_months_levers_or_free_actions()
        {
            var (e, s) = Stage2Content.Chapter2(10_000_000, noPersonalCosts: true);
            var shop = s.Businesses.Single(b => b.IndustryId == "shop");
            shop.Units = 2;
            var last = Stage2Content.Plan(Stage2Content.Lever("shop", "staff", 4));
            last.FreeActions.Add(e.Content.FreeActions.Keys.First());
            s.LastDecisions = last;
            shop.Employees = 2;   // after last month a scene let two people go
            Autopilot.Run(e, s, 1);
            Assert.AreEqual(2, shop.Employees, "autopilot keeps what the scene left; nobody is re-hired unasked");
            Assert.IsEmpty(s.LastDecisions.Levers, "levers are one-off decisions");
            Assert.IsEmpty(s.LastDecisions.FreeActions, "free actions are one-off decisions");
        }
    }
}
