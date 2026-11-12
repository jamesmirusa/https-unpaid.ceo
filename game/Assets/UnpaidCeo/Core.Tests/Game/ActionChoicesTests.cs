using System.Collections.Generic;
using NUnit.Framework;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>
    /// The New2NYC way of choosing: every button says what it costs in energy and money, a choice the hero cannot
    /// afford is closed and says why, and a choice that answers the hero's need is recommended. The engine closes
    /// exactly what the buttons close, so a planned choice is never silently dropped at the end of the week.
    /// </summary>
    public class ActionChoicesTests
    {
        const string Actions = @"{ ""freeActions"": [
  { ""id"": ""rest"", ""actionPoints"": 1, ""cost"": 0, ""effects"": [ { ""stat"": ""energy"", ""delta"": 12 } ] },
  { ""id"": ""overtime"", ""actionPoints"": 1, ""cost"": 0, ""effects"": [ { ""cash"": 6000 }, { ""stat"": ""energy"", ""delta"": -10 } ] },
  { ""id"": ""study"", ""actionPoints"": 1, ""cost"": 0, ""effects"": [ { ""studyXp"": 10 }, { ""stat"": ""energy"", ""delta"": -3 } ] },
  { ""id"": ""course"", ""actionPoints"": 1, ""cost"": 15000, ""effects"": [ { ""studyXp"": 25 }, { ""stat"": ""energy"", ""delta"": -8 } ] },
  { ""id"": ""family_time"", ""actionPoints"": 1, ""cost"": 0, ""effects"": [ { ""stat"": ""relationships"", ""delta"": 8 } ] },
  { ""id"": ""networking"", ""actionPoints"": 1, ""cost"": 2000, ""effects"": [ { ""stat"": ""connections"", ""delta"": 4 } ] },
  { ""id"": ""scheme"", ""actionPoints"": 1, ""cost"": 0, ""effects"": [ { ""cash"": 15000 }, { ""setFact"": ""scheme"" } ] },
  { ""id"": ""save"", ""actionPoints"": 0, ""cost"": 0, ""effects"": [ { ""reserve"": 10000 } ] }
] }";

        static GameEngine Engine() => new GameEngine(ContentDb.FromJson(new ContentFiles
        {
            Industries = TestContent.Industries(1.0),
            Balance = TestContent.Balance(),
            Storylets = TestContent.Storylets,
            FreeActions = Actions,
        }));

        static GameState Game(GameEngine e, int energy = 70, long cashCents = 100_000)
        {
            var s = e.NewGame(1, TestContent.Hero);
            s.Life.Energy = energy;
            s.Cash = Money.FromCents(cashCents);
            return s;
        }

        static ChoiceState State(GameEngine e, GameState s, string id, params string[] planned) =>
            ActionChoices.State(s, e.Content, planned, id);

        [Test]
        public void The_first_week_refills_the_months_points_and_later_weeks_spend_what_is_left()
        {
            var e = Engine();
            var s = Game(e);
            Assert.AreEqual(3, ActionChoices.PointsTotal(s, e.Content));
            e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0, "overtime", "study"));
            Assert.AreEqual(1, ActionChoices.PointsTotal(s, e.Content), "two of the month's three points are gone");
            Assert.AreEqual(0, ActionChoices.PointsLeft(s, e.Content, new[] { "rest" }));
            Assert.AreEqual(ChoiceState.NoPoints, State(e, s, "family_time", "rest"));
        }

        [Test]
        public void A_choice_made_earlier_this_month_waits_for_the_next_month()
        {
            var e = Engine();
            var s = Game(e);
            e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0, "overtime"));
            Assert.AreEqual(ChoiceState.DoneThisMonth, State(e, s, "overtime"));
            for (int i = 0; i < 3; i++) e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0));
            Assert.AreEqual(1, s.WeekOfMonth);
            Assert.AreEqual(ChoiceState.Open, State(e, s, "overtime"), "a new month, a new chance");
        }

        [Test]
        public void A_choice_that_would_take_more_energy_than_is_left_is_closed_until_the_hero_rests()
        {
            var e = Engine();
            var s = Game(e, energy: 8);
            Assert.AreEqual(ChoiceState.NoEnergy, State(e, s, "overtime"));
            Assert.AreEqual(ChoiceState.Open, State(e, s, "overtime", "rest"), "rest comes first: 8 + 12 − 10");
            Assert.AreEqual(ChoiceState.Open, State(e, s, "study"), "3 is still there");
        }

        [Test]
        public void A_choice_that_would_drain_the_energy_a_planned_one_needs_is_closed_too()
        {
            var e = Engine();
            var s = Game(e, energy: 12);
            // overtime runs before study: 12 − 10 = 2, and study then lacks its 3.
            Assert.AreEqual(ChoiceState.NoEnergy, State(e, s, "overtime", "study"));
        }

        [Test]
        public void A_choice_the_hero_cannot_pay_for_is_closed()
        {
            var e = Engine();
            var s = Game(e, cashCents: 10_000);
            Assert.AreEqual(ChoiceState.NoMoney, State(e, s, "course"), "$100 on hand, the course is $150");
            Assert.AreEqual(ChoiceState.Open, State(e, s, "course", "overtime"), "overtime pays first: 100 + 60");
        }

        [Test]
        public void A_scheme_still_open_waits_and_a_planned_choice_says_so()
        {
            var e = Engine();
            var s = Game(e);
            s.Facts.Set("scheme", s.Month);
            Assert.AreEqual(ChoiceState.Waiting, State(e, s, "scheme"));
            Assert.AreEqual(ChoiceState.Planned, State(e, s, "rest", "rest"));
        }

        [Test]
        public void The_plan_shows_the_energy_it_leaves()
        {
            var e = Engine();
            var s = Game(e, energy: 70);
            Assert.AreEqual(70, ActionChoices.EnergyAfter(s, e.Content, new string[0]));
            Assert.AreEqual(57, ActionChoices.EnergyAfter(s, e.Content, new[] { "overtime", "study" }));
            Assert.AreEqual(100, ActionChoices.EnergyAfter(Game(e, energy: 95), e.Content, new[] { "rest" }), "energy tops out at 100");
            Assert.AreEqual(10, ActionChoices.EnergyCost(e.Content.FreeActions["overtime"]));
            Assert.AreEqual(0, ActionChoices.EnergyCost(e.Content.FreeActions["rest"]));
        }

        [Test]
        public void The_engine_drops_exactly_what_the_buttons_close()
        {
            var e = Engine();
            var s = Game(e, energy: 8, cashCents: 10_000);
            int points = ActionChoices.PointsTotal(s, e.Content);   // a burnt-out hero has one point fewer
            var r = e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0, "overtime", "course"));
            CollectionAssert.AreEquivalent(new[] { "overtime", "course" }, r.RejectedActions);
            Assert.IsFalse(s.ActionsThisMonth.Contains("overtime"));
            Assert.AreEqual(points, s.ActionPointsLeft, "nothing was spent");
        }

        [Test]
        public void Rest_is_recommended_when_energy_runs_low_and_family_when_close_ones_drift()
        {
            var e = Engine();
            var tired = Game(e, energy: 35);
            Assert.IsTrue(ActionChoices.Recommended(tired, e.Content, "rest"));
            Assert.IsFalse(ActionChoices.Recommended(tired, e.Content, "overtime"));
            Assert.IsFalse(ActionChoices.Recommended(Game(e, energy: 80), e.Content, "rest"), "a rested hero needs no nudge");

            var lonely = Game(e);
            lonely.Life.Relationships = 30;
            Assert.IsTrue(ActionChoices.Recommended(lonely, e.Content, "family_time"));

            var unknown = Game(e);
            unknown.Life.Connections = 5;
            Assert.IsTrue(ActionChoices.Recommended(unknown, e.Content, "networking"));
            Assert.IsFalse(ActionChoices.Recommended(unknown, e.Content, "rest"));
        }

        [Test]
        public void A_plan_that_wears_the_hero_out_brings_rest_up_as_recommended()
        {
            var e = Engine();
            var s = Game(e, energy: 50);
            Assert.IsFalse(ActionChoices.Recommended(s, e.Content, "rest"));
            Assert.IsFalse(ActionChoices.Recommended(s, e.Content, "rest", new[] { "overtime" }), "50 − 10 = 40: just enough");
            Assert.IsTrue(ActionChoices.Recommended(s, e.Content, "rest", new[] { "overtime", "course" }), "50 − 10 − 8 = 32");
        }
    }
}
