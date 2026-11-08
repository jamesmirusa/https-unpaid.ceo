using System.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Game
{
    public class GameEngineTests
    {
        [Test]
        public void New_game_starts_in_month_1_with_chapter_gigs()
        {
            var e = new GameEngine(TestContent.Db());
            var s = e.NewGame(42, TestContent.Hero);
            Assert.AreEqual(1, s.Month);
            Assert.AreEqual(1, s.Chapter);
            Assert.AreEqual(0, s.Cash.Cents);
            Assert.AreEqual("gig_test", s.Businesses.Single().IndustryId);
            Assert.AreEqual("raccoon", s.Hero.Species);
        }

        [Test]
        public void Bills_start_after_100_lifetime()
        {
            // $1 per unit, Work 60 × 1.5 = 90 units → $90 lifetime → no bills.
            var e1 = new GameEngine(TestContent.Db(capacityPerWorkPoint: 1.5));
            var s1 = e1.NewGame(1, TestContent.Hero);
            var r1 = e1.EndMonth(s1, TestContent.Decisions(60, 15, 25, 0));
            Assert.AreEqual(9_000, r1.Revenue.Cents);
            Assert.AreEqual(0, r1.PersonalCosts.Cents);
            Assert.AreEqual(9_000 - 2_250, s1.Cash.Cents);   // revenue − 25% tax reserve

            // Work 60 × 2 = 120 units → $120 lifetime → full bills ($1,500).
            var e2 = new GameEngine(TestContent.Db(capacityPerWorkPoint: 2.0));
            var s2 = e2.NewGame(1, TestContent.Hero);
            var r2 = e2.EndMonth(s2, TestContent.Decisions(60, 15, 25, 0));
            Assert.AreEqual(150_000, r2.PersonalCosts.Cents);
            Assert.AreEqual(12_000 - 3_000 - 150_000, s2.Cash.Cents);
        }

        [Test]
        public void Tutorial_gig_pays_once_and_counts_as_earnings()
        {
            var e = new GameEngine(TestContent.Db());
            var s = e.NewGame(1, TestContent.Hero);
            e.TutorialGig(s);
            e.TutorialGig(s);
            Assert.AreEqual(4_000, s.Cash.Cents);
            Assert.AreEqual(4_000, s.LifetimeEarnings.Cents);
        }

        [Test]
        public void Negative_cash_sets_in_debt_and_bankrupt_after_two_months()
        {
            var e = new GameEngine(TestContent.Db());
            var s = e.NewGame(1, TestContent.Hero);
            s.Cash = Money.FromDollars(-600);
            s.LifetimeEarnings = Money.FromDollars(500);
            var idle = TestContent.Decisions(0, 40, 40, 20);
            var r1 = e.EndMonth(s, idle);
            Assert.IsTrue(s.Facts.Has("in_debt"));
            Assert.IsFalse(s.Facts.Has("bankrupt"));
            var r2 = e.EndMonth(s, idle);
            Assert.IsTrue(s.Facts.Has("bankrupt"));
            Assert.Less(r2.CashEnd.Cents, 0);
            Assert.IsTrue(s.Journal.Any(j => j.TextKey == "journal.bankrupt"));
        }

        [Test]
        public void Chapter1_goal_requires_cash_and_reserve()
        {
            var e = new GameEngine(TestContent.Db());
            var s = e.NewGame(1, TestContent.Hero);
            s.Cash = Money.FromDollars(1_000);
            Assert.IsFalse(e.Chapter(s).GoalMet);
            s.Reserve = Money.FromDollars(1_500);   // one month of personal costs in TestContent
            Assert.IsTrue(e.Chapter(s).GoalMet);
            e.EndMonth(s, TestContent.Decisions(0, 40, 40, 20));   // lifetime 0 → no bills, cash stays
            Assert.IsTrue(s.Facts.Has("chapter1_done"));
        }

        [Test]
        public void Storylet_choice_applies_effects_and_journal()
        {
            var e = new GameEngine(TestContent.Db());
            var s = e.NewGame(1, TestContent.Hero);
            var picked = e.BeginMonth(s);
            Assert.AreEqual(1, picked.Count);
            var id = picked[0].Id;
            var choice = picked[0].Choices.Keys.First();
            e.ChooseStoryletOption(s, id, choice);
            Assert.AreEqual(0, s.PendingStorylets.Count);
            Assert.IsTrue(s.Journal.Any(j => j.Kind == "decision" && j.Args["storylet"] == id));
            if (id == "tony_intro") { Assert.AreEqual(15, s.Npcs.Get("tony")); Assert.IsTrue(s.Facts.Has("helped_tony")); }
        }

        [Test]
        public void Choosing_an_unknown_option_is_rejected_without_changes()
        {
            var e = new GameEngine(TestContent.Db());
            var s = e.NewGame(1, TestContent.Hero);
            var id = e.BeginMonth(s)[0].Id;
            Assert.Throws<System.ArgumentException>(() => e.ChooseStoryletOption(s, id, "no_such_choice"));
            Assert.Throws<System.ArgumentException>(() => e.ChooseStoryletOption(s, "not_pending", "help"));
            Assert.AreEqual(1, s.PendingStorylets.Count);
        }

        [Test]
        public void Free_actions_limited_by_action_points()
        {
            var e = new GameEngine(TestContent.Db());
            var s = e.NewGame(1, TestContent.Hero);
            s.Life.Energy = 70;
            var r = e.EndMonth(s, TestContent.Decisions(0, 40, 40, 20, "overtime", "network"));
            CollectionAssert.AreEqual(new[] { "network" }, r.RejectedActions);
            Assert.AreEqual(0, s.Life.Connections - 20);   // network not applied (start 20)
            Assert.AreEqual(5_000, s.Cash.Cents);          // overtime cash effect
        }

        [Test]
        public void Reserve_effect_moves_only_available_cash()
        {
            var e = new GameEngine(TestContent.Db());
            var s = e.NewGame(1, TestContent.Hero);
            s.Cash = Money.FromDollars(300);
            e.EndMonth(s, TestContent.Decisions(0, 40, 40, 20, "save"));   // asks to move $500
            Assert.AreEqual(30_000, s.Reserve.Cents);
            Assert.AreEqual(0, s.Cash.Cents);
        }

        [Test]
        public void Burnout_mistake_cuts_revenue_10pct()
        {
            var e = new GameEngine(TestContent.Db(capacityPerWorkPoint: 1.0, errorChance: 1.0));
            var s = e.NewGame(1, TestContent.Hero);
            s.Life.Energy = 10;   // below threshold 30
            var r = e.EndMonth(s, TestContent.Decisions(100, 0, 0, 0));
            Assert.IsTrue(r.BurnoutMistake);
            Assert.AreEqual(10_000, r.Revenue.Cents);
            Assert.AreEqual(9_000, r.OperatingProfit.Cents);
            Assert.IsTrue(s.Journal.Any(j => j.TextKey == "journal.burnout_mistake"));
        }

        [Test]
        public void Invalid_time_allocation_is_rejected()
        {
            var e = new GameEngine(TestContent.Db());
            var s = e.NewGame(1, TestContent.Hero);
            Assert.Throws<System.ArgumentException>(() => e.EndMonth(s, TestContent.Decisions(60, 30, 30, 0)));
            Assert.AreEqual(1, s.Month);
        }
    }
}
