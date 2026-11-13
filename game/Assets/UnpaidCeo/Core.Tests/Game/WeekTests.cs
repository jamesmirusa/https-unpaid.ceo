using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Persistence;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>Stage 4B: the turn is a week and money moves when it really moves.</summary>
    public class WeekTests
    {
        static GameEngine Engine(double capacity = 2.0, string weekWeights = "[0, 1]") => new GameEngine(TestContent.Db(capacityPerWorkPoint: capacity, weekWeights: weekWeights));

        static GameState Earning(GameEngine e)
        {
            var s = e.NewGame(1, TestContent.Hero);
            s.LifetimeEarnings = Money.FromCents(10_000);   // bills already started
            return s;
        }

        [Test]
        public void A_new_game_starts_in_week_1_of_month_1_and_four_weeks_make_a_month()
        {
            var e = Engine();
            var s = e.NewGame(1, TestContent.Hero);
            Assert.AreEqual(1, s.Week);
            Assert.AreEqual(1, s.WeekOfMonth);
            Assert.AreEqual(1, s.Month);
            for (int i = 0; i < 4; i++) e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0));
            Assert.AreEqual(5, s.Week);
            Assert.AreEqual(1, s.WeekOfMonth);
            Assert.AreEqual(2, s.Month, "the stored month follows the week");
        }

        [Test]
        public void Gig_pay_arrives_every_week_as_a_quarter_of_the_months_work()
        {
            var e = Engine(capacity: 2.0);   // Work 60 × 2 = 120 units a month at $1
            var s = Earning(e);
            var r = e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0));
            Assert.AreEqual(3_000, r.Revenue.Cents, "30 units this week");
            Assert.AreEqual(1, r.Week);
            Assert.AreEqual(1, r.WeekOfMonth);
        }

        [Test]
        public void Food_and_transit_fall_every_week_essentials_on_week_2_rent_on_the_last_week()
        {
            var e = Engine();
            var s = Earning(e);
            var d = TestContent.Decisions(60, 15, 25, 0);
            var r1 = e.EndWeek(s, d); var r2 = e.EndWeek(s, d); var r3 = e.EndWeek(s, d); var r4 = e.EndWeek(s, d);
            // room 1000 on the last week, food 300 + transit 150 = 450 a month → 112.50 a week, essentials 50 on week 2
            Assert.AreEqual(11_250, r1.PersonalCosts.Cents, "the first pay comes before the first rent");
            Assert.AreEqual(11_250 + 5_000, r2.PersonalCosts.Cents);
            Assert.AreEqual(11_250, r3.PersonalCosts.Cents);
            Assert.AreEqual(100_000 + 11_250, r4.PersonalCosts.Cents);
            Assert.AreEqual(150_000, r1.PersonalCosts.Cents + r2.PersonalCosts.Cents + r3.PersonalCosts.Cents + r4.PersonalCosts.Cents, "four weeks are exactly the month's bills");
            Assert.IsTrue(r4.Paid.Any(p => p.Key == "bill.room" && p.Amount.Cents == 100_000), "the week's result names the rent");
            Assert.IsFalse(r1.Paid.Any(p => p.Key == "bill.room"));
            Assert.IsTrue(r1.DueNext.Any(p => p.Key == "bill.essentials" && p.Amount.Cents == 5_000), "and says what falls due next week");
            Assert.IsTrue(r3.DueNext.Any(p => p.Key == "bill.room"), "week 3 warns about the rent");
            Assert.IsFalse(r4.DueNext.Any(p => p.Key == "bill.room"));
        }

        [Test]
        public void Odd_cents_of_a_weekly_bill_land_in_the_last_week()
        {
            var e = new GameEngine(TestContent.Db(capacityPerWorkPoint: 2.0, food: 30_001));
            var s = Earning(e);
            var d = TestContent.Decisions(60, 15, 25, 0);
            long sum = 0;
            for (int w = 0; w < 4; w++) sum += e.EndWeek(s, d).PersonalCosts.Cents;
            Assert.AreEqual(100_000 + 30_001 + 15_000 + 5_000, sum);
        }

        [Test]
        public void Loans_are_paid_on_the_last_week_of_the_month()
        {
            var e = Engine();
            var s = Earning(e);
            s.Cash = Money.FromDollars(5000);
            s.Loans.Add(new LoanState { Id = "l", Principal = Money.FromCents(100_000), Balance = Money.FromCents(100_000), Apr = 0.12, MonthsLeft = 12, Payment = Money.FromCents(10_000) });
            var d = TestContent.Decisions(60, 15, 25, 0);
            Assert.AreEqual(0, e.EndWeek(s, d).LoanPayments.Cents);
            Assert.AreEqual(0, e.EndWeek(s, d).LoanPayments.Cents);
            Assert.AreEqual(0, e.EndWeek(s, d).LoanPayments.Cents);
            var r4 = e.EndWeek(s, d);
            Assert.AreEqual(10_000, r4.LoanPayments.Cents);
            Assert.AreEqual(1_000, r4.Interest.Cents, "a month of interest");
            Assert.AreEqual(11, s.Loans[0].MonthsLeft);
        }

        [Test]
        public void Each_week_refills_its_two_points_and_an_action_opens_again_next_week()
        {
            var e = Engine();
            var s = e.NewGame(1, TestContent.Hero);
            Assert.IsEmpty(e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0, "overtime")).RejectedActions);   // 2 of 2 points
            Assert.AreEqual(0, s.ActionPointsLeft, "two points a week");
            Assert.IsEmpty(e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0, "overtime")).RejectedActions, "a new week, a new pool");
        }

        [Test]
        public void A_burnt_out_hero_has_one_point_a_week()
        {
            var e = Engine();
            var s = e.NewGame(1, TestContent.Hero);
            s.Life.Energy = 20;
            Assert.AreEqual(1, LifeSimulator.WeeklyActionPoints(s.Life, e.Content.Balance.Life));
            CollectionAssert.Contains(e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0, "overtime")).RejectedActions, "overtime",
                "overtime takes two points");
        }

        /// <summary>Guard: challenge and duel scenarios still play whole months with the monthly pool.</summary>
        [Test]
        public void A_legacy_month_still_gets_the_monthly_pool()
        {
            var e = Engine();
            var s = e.NewGame(1, TestContent.Hero);
            Assert.IsEmpty(e.EndMonth(s, TestContent.Decisions(60, 15, 25, 0, "overtime")).RejectedActions);
            Assert.AreEqual(1, s.ActionPointsLeft, "three a month, two spent");
        }

        [Test]
        public void The_buttons_offer_a_legacy_months_pool_on_a_legacy_month()
        {
            // Challenge and duel runs play whole months: their buttons must offer what EndMonth accepts.
            var e = Engine();
            var s = e.NewGame(1, TestContent.Hero);
            Assert.AreEqual(UnpaidCeo.Core.Life.LifeSimulator.ActionPoints(s.Life, 0, e.Content.Balance.Life),
                ActionChoices.PointsTotal(s, e.Content, weekly: false));
            Assert.AreEqual(UnpaidCeo.Core.Life.LifeSimulator.WeeklyActionPoints(s.Life, e.Content.Balance.Life),
                ActionChoices.PointsTotal(s, e.Content));
        }

        [Test]
        public void An_old_save_from_the_middle_of_a_month_opens_with_a_fresh_week()
        {
            var e = Engine();
            var obj = JObject.Parse(SaveCodec.Encode(e.NewGame(1, TestContent.Hero)));
            obj["Week"] = 6;
            obj["Month"] = 2;
            obj["ActionsThisMonth"] = new JArray("overtime");
            obj["ActionPointsLeft"] = 0;
            var s = SaveCodec.Decode(obj.ToString()).State;
            Assert.AreEqual(ChoiceState.Open, ActionChoices.State(s, e.Content, new string[0], "overtime"));
            Assert.AreEqual(2, ActionChoices.PointsTotal(s, e.Content));
        }

        [Test]
        public void Life_moves_by_a_quarter_a_week()
        {
            var e = Engine();
            var s = e.NewGame(1, TestContent.Hero);
            int energy = s.Life.Energy;
            // rest 45: 0.4 × (45 − 25) = +8 a month → +2 a week
            var r = e.EndWeek(s, TestContent.Decisions(40, 15, 45, 0));
            Assert.AreEqual(2, r.Life.Energy);
            Assert.AreEqual(energy + 2, s.Life.Energy);
        }

        [Test]
        public void Scenes_roll_each_week_at_about_one_a_month()
        {
            var e = Engine(weekWeights: "[3, 1]");
            var s = e.NewGame(9, TestContent.Hero);
            int scenes = 0;
            for (int w = 0; w < 80; w++)
            {
                var picked = e.BeginWeek(s);
                Assert.LessOrEqual(picked.Count, 1);
                scenes += picked.Count;
                foreach (var st in picked) e.ChooseStoryletOption(s, st.Id, st.Choices.Keys.OrderBy(k => k).First());
                e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0));
            }
            Assert.That(scenes, Is.InRange(8, 36), "about 20 scenes in 20 months");
        }

        [Test]
        public void Old_month_logs_replay_as_before_and_new_weeks_follow_them()
        {
            var e = Engine();
            var s = e.NewGame(5, TestContent.Hero);
            e.EndMonth(s, TestContent.Decisions(60, 15, 25, 0));
            Assert.AreEqual(5, s.Week, "a legacy month is four weeks");
            e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0));
            var replayed = GameEngine.Replay(TestContent.Db(capacityPerWorkPoint: 2.0), 5, TestContent.Hero, s.Log);
            Assert.AreEqual(StateHash.Compute(s), StateHash.Compute(replayed));
            Assert.AreEqual(6, replayed.Week);
        }

        [Test]
        public void A_version_2_save_opens_at_the_first_week_of_its_month_with_receivables_split_by_week()
        {
            var e = Engine();
            var s = e.NewGame(3, TestContent.Hero);
            s.Businesses[0].Receivables.Add(Money.FromCents(40_000));
            var json = JObject.Parse(SaveCodec.Encode(s));
            json["Version"] = 2;
            json["Month"] = 3;
            json.Remove("Week");
            var r = SaveCodec.Decode(json.ToString());
            Assert.AreEqual(LoadStatus.Migrated, r.Status);
            Assert.AreEqual(9, r.State.Week);
            Assert.AreEqual(3, r.State.Month);
            Assert.AreEqual(3, r.State.Version);
            CollectionAssert.AreEqual(new[] { 10_000L, 10_000L, 10_000L, 10_000L }, r.State.Businesses[0].Receivables.Select(m => m.Cents));
        }
    }
}
