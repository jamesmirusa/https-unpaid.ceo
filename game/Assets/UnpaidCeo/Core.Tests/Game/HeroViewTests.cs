using System.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>Stage 5B step 4: the hero's needs, mood badges and wants are pure reads of the state.</summary>
    public class HeroViewTests
    {
        static GameEngine Engine() => new GameEngine(TestContent.Db());

        [Test]
        public void Four_needs_read_the_state()
        {
            var e = Engine();
            var s = e.NewGame(1, TestContent.Hero);
            s.Life.Energy = 46; s.Life.Relationships = 72; s.StudyXp = 134; s.Life.Reputation = 58;
            var n = HeroView.Needs(s);
            CollectionAssert.AreEqual(new[] { "energy", "relationships", "knowledge", "reputation" }, n.Select(x => x.Id).ToArray());
            Assert.AreEqual(46, n[0].Value);
            Assert.AreEqual(134, n[2].Value, "knowledge shows its real number");
            Assert.AreEqual(100, n[2].Bar, "the bar is capped");
            Assert.IsTrue(HeroView.Needs(new GameState { Life = new LifeStats { Energy = 20 } })[0].Low, "energy below the burnout line is low");
        }

        [Test]
        public void Moods_explain_the_state_bad_ones_first_and_at_most_four()
        {
            var e = Engine();
            var s = e.NewGame(1, TestContent.Hero);
            s.Life.Energy = 10;                       // burnout
            s.Facts.Set("shark_debt", 1);             // debt to the loan shark
            s.Cash = Money.FromDollars(-20);          // in debt
            s.Life.Relationships = 10;                // lonely
            s.Reserve = Money.FromDollars(100);       // a cushion
            var last = new MonthReport { Revenue = Money.FromDollars(40) };   // a paycheck
            var m = HeroView.Moods(s, e.Content.Balance, last);
            Assert.AreEqual(4, m.Count);
            Assert.IsTrue(m.Take(4).All(x => !x.Good), "bad news first");
            Assert.AreEqual("burnout", m[0].Id);
        }

        [Test]
        public void A_good_week_shows_good_moods_with_their_weeks()
        {
            var e = Engine();
            var s = e.NewGame(1, TestContent.Hero);
            s.Life.Energy = 90; s.Life.Relationships = 60;
            var m = HeroView.Moods(s, e.Content.Balance, new MonthReport { Revenue = Money.FromDollars(40) });
            Assert.IsTrue(m.Any(x => x.Id == "rested" && x.Good));
            var pay = m.Single(x => x.Id == "paycheck");
            Assert.AreEqual(1, pay.WeeksLeft);
        }

        [Test]
        public void Moods_never_touch_the_game()
        {
            var e = Engine();
            var s = e.NewGame(1, TestContent.Hero);
            s.Life.Energy = 10;
            var before = Newtonsoft.Json.JsonConvert.SerializeObject(s);
            HeroView.Moods(s, e.Content.Balance, null);
            HeroView.Wants(s, e.Content, new string[0]);
            Assert.AreEqual(before, Newtonsoft.Json.JsonConvert.SerializeObject(s));
        }

        [Test]
        public void Wants_are_the_next_real_steps_and_tick_with_the_plan()
        {
            var e = Engine();
            var s = e.NewGame(1, TestContent.Hero);
            s.Life.Energy = 30;   // tired: rest is a want
            var w = HeroView.Wants(s, e.Content, new string[0]);
            Assert.LessOrEqual(w.Count, 3);
            Assert.IsTrue(w.Any(x => x.Id == "earn"), "earn while it is open");
            Assert.IsTrue(w.All(x => !x.Done));
            var planned = HeroView.Wants(s, e.Content, new[] { "overtime" });
            Assert.IsTrue(planned.Single(x => x.Id == "earn").Done, "ticked once the week's plan has it");
        }
    }
}
