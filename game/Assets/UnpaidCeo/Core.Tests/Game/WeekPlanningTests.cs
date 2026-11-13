using System;
using System.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>Stage 5A: the week is planned with one tap — a mode for the hero's time, a focus for the gigs.</summary>
    public class WeekPlanningTests
    {
        static GameEngine Engine() => new GameEngine(TestContent.Db(capacityPerWorkPoint: 2.0));

        [Test]
        public void Every_mode_sums_to_100_and_balance_is_the_default()
        {
            var b = Engine().Content.Balance;
            CollectionAssert.AreEquivalent(WeekPlanning.ModeOrder, b.WeekModes.Keys);
            foreach (var kv in b.WeekModes) Assert.AreEqual(100, kv.Value.Sum(), kv.Key);
            var d = new MonthDecisions();
            WeekPlanning.ApplyMode(d, "balance", b);
            var def = MonthDecisions.Default(null);
            Assert.AreEqual((def.Work, def.Family, def.Rest, def.Study), (d.Work, d.Family, d.Rest, d.Study));
        }

        [Test]
        public void Hustle_earns_more_and_tires_more_than_balance()
        {
            var e = Engine();
            (long revenue, int energy) Play(string mode)
            {
                var s = e.NewGame(1, TestContent.Hero);
                var d = TestContent.Decisions(55, 15, 20, 10);
                WeekPlanning.ApplyMode(d, mode, e.Content.Balance);
                var r = e.EndWeek(s, d);
                return (r.Revenue.Cents, s.Life.Energy);
            }
            var hustle = Play("hustle");
            var balance = Play("balance");
            Assert.Greater(hustle.revenue, balance.revenue);
            Assert.Less(hustle.energy, balance.energy);
        }

        [Test]
        public void Focus_puts_all_work_into_one_gig()
        {
            var gigs = new[] { "a", "b", "c" };
            var d = new MonthDecisions();
            WeekPlanning.ApplyFocus(d, gigs, "b");
            Assert.AreEqual(100, d.WorkSplit["b"]);
            Assert.AreEqual(0, d.WorkSplit["a"]);
            Assert.AreEqual(0, d.WorkSplit["c"]);
            Assert.AreEqual("b", WeekPlanning.FocusOf(d, gigs));
        }

        [Test]
        public void Even_focus_splits_work_evenly()
        {
            var gigs = new[] { "a", "b", "c" };
            var d = new MonthDecisions();
            WeekPlanning.ApplyFocus(d, gigs, WeekPlanning.EvenFocus);
            Assert.AreEqual(100, gigs.Sum(g => d.WorkSplit[g]));
            Assert.IsTrue(gigs.All(g => d.WorkSplit[g] == 33 || d.WorkSplit[g] == 34));
            Assert.AreEqual(WeekPlanning.EvenFocus, WeekPlanning.FocusOf(d, gigs));
        }

        [Test]
        public void ModeOf_recognises_a_mode_and_returns_null_for_custom_shares()
        {
            var b = Engine().Content.Balance;
            var d = new MonthDecisions();
            WeekPlanning.ApplyMode(d, "study", b);
            Assert.AreEqual("study", WeekPlanning.ModeOf(d, b));
            Assert.IsNull(WeekPlanning.ModeOf(new MonthDecisions { Work = 61, Family = 13, Rest = 21, Study = 5 }, b));
            Assert.Throws<ArgumentException>(() => WeekPlanning.ApplyMode(d, "party", b));
        }

        [Test]
        public void The_week_plan_round_trips_through_the_meta_save()
        {
            var meta = new PlayerMeta();
            meta.Meta.WeekPlan = new WeekPlanRecord { Week = 7, Mode = "study", ModeChosen = true, Focus = "even", FocusChosen = false };
            meta.Meta.WeekPlan.Actions.Add("overtime");
            meta.Meta.WeekPlan.Levers.Add(new LeverChange { BusinessId = "coffee_cart", Lever = "price", ValueCents = 420 });
            var back = UnpaidCeo.Core.Persistence.SaveCodec.DecodeMeta(UnpaidCeo.Core.Persistence.SaveCodec.EncodeMeta(meta)).Meta.WeekPlan;
            Assert.AreEqual(7, back.Week);
            Assert.AreEqual("study", back.Mode);
            Assert.IsTrue(back.ModeChosen);
            CollectionAssert.AreEqual(new[] { "overtime" }, back.Actions);
            Assert.AreEqual(420, back.Levers.Single().ValueCents);
            Assert.IsNull(UnpaidCeo.Core.Persistence.SaveCodec.DecodeMeta("{}").Meta.WeekPlan, "an old meta save has no week plan");
        }

        [Test]
        public void A_mode_that_does_not_sum_to_100_is_reported()
        {
            var db = TestContent.Db();
            db.Balance.WeekModes["hustle"] = new[] { 50, 10, 10, 10 };
            Assert.IsTrue(ContentValidator.Validate(db).Errors.Any(x => x.StartsWith("balance.weekModes")));
        }
    }
}
