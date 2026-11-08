using NUnit.Framework;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>Daily manual turns, offline autopilot and the streak (stage 2A, Task 8).</summary>
    public class TurnBudgetTests
    {
        const int NewYork = -300, Tokyo = 540;

        static Balance B() => Stage2Content.Db().Balance;

        /// <summary>Unix time of a local clock reading on local day number <paramref name="day"/>.</summary>
        static long At(int day, int hour, int minute, int offsetMinutes) =>
            day * 86400L + hour * 3600L + minute * 60L - offsetMinutes * 60L;

        static void UseAll(TurnState t) { while (TurnBudget.TryConsume(t)) { } }

        [Test]
        public void Six_manual_months_per_local_day()
        {
            var b = B();
            Assert.AreEqual(6, b.DailyManualMonths);
            var t = new TurnState();
            TurnBudget.Refresh(t, At(20000, 9, 0, 0), 0, b);
            Assert.AreEqual(6, t.ManualLeft);
            for (int i = 0; i < 6; i++) Assert.IsTrue(TurnBudget.TryConsume(t), $"turn {i + 1}");
            Assert.IsFalse(TurnBudget.TryConsume(t));
            TurnBudget.Refresh(t, At(20000, 22, 0, 0), 0, b);
            Assert.AreEqual(0, t.ManualLeft, "no second refill the same day");
        }

        [Test]
        public void Refill_happens_at_local_midnight_with_offset()
        {
            var b = B();
            var t = new TurnState();
            TurnBudget.Refresh(t, At(20000, 1, 0, NewYork), NewYork, b);
            UseAll(t);
            // UTC midnight is 19:00 in New York: nothing yet.
            TurnBudget.Refresh(t, 20001 * 86400L, NewYork, b);
            Assert.AreEqual(0, t.ManualLeft);
            TurnBudget.Refresh(t, At(20000, 23, 59, NewYork), NewYork, b);
            Assert.AreEqual(0, t.ManualLeft);
            Assert.AreEqual(60, TurnBudget.SecondsToRefill(t, At(20000, 23, 59, NewYork), NewYork));
            TurnBudget.Refresh(t, At(20001, 0, 0, NewYork), NewYork, b);
            Assert.AreEqual(6, t.ManualLeft);
        }

        [Test]
        public void Clock_set_back_grants_nothing()
        {
            var b = B();
            var t = new TurnState();
            TurnBudget.Refresh(t, At(20000, 10, 0, 0), 0, b);
            UseAll(t);
            TurnBudget.Refresh(t, At(19998, 10, 0, 0), 0, b);
            Assert.AreEqual(0, t.ManualLeft, "two days back");
            Assert.AreEqual(At(20000, 10, 0, 0), t.LastSeenUnix, "the last-seen time never moves back");
            Assert.AreEqual(0, Autopilot.MonthsToRun(t.LastSeenUnix, At(19998, 10, 0, 0), b), "no autopilot from the past");
            TurnBudget.Refresh(t, At(20000, 11, 0, 0), 0, b);
            Assert.AreEqual(0, t.ManualLeft, "back to today");
        }

        [Test]
        public void Time_zone_change_does_not_double_refill()
        {
            var b = B();
            var t = new TurnState();
            TurnBudget.Refresh(t, At(20000, 23, 0, NewYork), NewYork, b);
            UseAll(t);
            // Fly to Tokyo: it is already tomorrow afternoon there, but the day started in New York.
            TurnBudget.Refresh(t, At(20000, 23, 30, NewYork), Tokyo, b);
            Assert.AreEqual(0, t.ManualLeft);
            // The next refill comes at New York midnight; from then on Tokyo's days count.
            TurnBudget.Refresh(t, At(20001, 0, 0, NewYork), Tokyo, b);
            Assert.AreEqual(6, t.ManualLeft);
            UseAll(t);
            TurnBudget.Refresh(t, At(20001, 23, 0, Tokyo), Tokyo, b);
            Assert.AreEqual(0, t.ManualLeft);
            TurnBudget.Refresh(t, At(20002, 0, 0, Tokyo), Tokyo, b);
            Assert.AreEqual(6, t.ManualLeft);
            UseAll(t);
            // Flying back west never rewinds the day for a second refill.
            TurnBudget.Refresh(t, At(20002, 0, 30, Tokyo), NewYork, b);
            Assert.AreEqual(0, t.ManualLeft);
            TurnBudget.Refresh(t, At(20002, 12, 0, Tokyo), NewYork, b);
            Assert.AreEqual(0, t.ManualLeft);
        }

        [Test]
        public void Streak_counts_consecutive_days_and_resets_after_a_gap()
        {
            var b = B();
            var t = new TurnState();
            TurnBudget.Refresh(t, At(20000, 9, 0, 0), 0, b);
            TurnBudget.CompleteDay(t);
            TurnBudget.CompleteDay(t);
            Assert.AreEqual(1, t.Streak, "once per day");
            TurnBudget.Refresh(t, At(20001, 9, 0, 0), 0, b);
            TurnBudget.CompleteDay(t);
            Assert.AreEqual(2, t.Streak);
            TurnBudget.Refresh(t, At(20002, 9, 0, 0), 0, b);   // played, but missions not finished
            Assert.AreEqual(2, t.Streak, "the streak survives until a day is missed");
            TurnBudget.Refresh(t, At(20003, 9, 0, 0), 0, b);
            Assert.AreEqual(0, t.Streak, "a missed day resets it");
            TurnBudget.CompleteDay(t);
            Assert.AreEqual(1, t.Streak);
        }

        [Test]
        public void Autopilot_months_do_not_use_manual_turns()
        {
            var e = new GameEngine(TestContent.Db());
            var s = e.NewGame(3, TestContent.Hero);
            var t = new TurnState();
            long launch = At(20000, 8, 0, 0);
            Assert.IsEmpty(TurnBudget.OnLaunch(e, s, t, launch, 0), "no autopilot on the first launch");
            Assert.IsTrue(TurnBudget.TryConsume(t));
            int month = s.Month;

            var reports = TurnBudget.OnLaunch(e, s, t, launch + 9 * 3600, 0);   // back after 9 hours
            Assert.AreEqual(2, reports.Count, "1 month per 4 hours, at most 2");
            Assert.AreEqual(month + 2, s.Month);
            Assert.AreEqual(5, t.ManualLeft);
        }
    }
}
