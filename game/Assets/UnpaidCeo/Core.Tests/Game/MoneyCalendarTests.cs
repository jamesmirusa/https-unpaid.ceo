using System.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>Stage 5B · the week calendar: when money leaves this month and whether the cash covers it.</summary>
    public class MoneyCalendarTests
    {
        static GameEngine Engine() => new GameEngine(TestContent.Db());

        static GameState Earning(GameEngine e, int week = 1)
        {
            var s = e.NewGame(1, TestContent.Hero);
            s.LifetimeEarnings = Money.FromCents(10_000_000);   // bills already started
            s.Week = week;
            s.Cash = Money.FromDollars(5_000);
            return s;
        }

        [Test]
        public void The_month_has_four_weeks_and_marks_the_current_one()
        {
            var e = Engine();
            var s = Earning(e, week: 6);   // week 2 of month 2
            var m = MoneyCalendar.Month(s, e.Content.Balance);
            Assert.AreEqual(4, m.Count);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, m.Select(w => w.WeekOfMonth).ToArray());
            Assert.IsTrue(m[0].Past);
            Assert.IsTrue(m[1].Now);
            Assert.IsFalse(m[2].Past || m[2].Now);
            Assert.AreEqual(5, m[0].Week);
            Assert.AreEqual(8, m[3].Week);
        }

        [Test]
        public void The_weeks_carry_the_engines_bills_rent_on_the_last_week_and_essentials_on_the_second()
        {
            var e = Engine();
            var s = Earning(e);
            var b = e.Content.Balance;
            var costs = b.PersonalCostsFor(s.Chapter);
            var m = MoneyCalendar.Month(s, b);
            for (int w = 1; w <= 4; w++)
            {
                var expected = costs.ForWeek(w);
                Assert.AreEqual(expected.Sum(i => i.Amount.Cents), m[w - 1].Total.Cents, "week " + w);
            }
            Assert.AreEqual("bill.room", m[3].Headline);
            Assert.AreEqual("bill.essentials", m[1].Headline);
            Assert.AreEqual("bill.food", m[0].Headline);
        }

        [Test]
        public void A_loan_payment_falls_on_the_last_week()
        {
            var e = Engine();
            var s = Earning(e);
            s.Loans.Add(new LoanState { Id = "l", Principal = Money.FromCents(100_000), Balance = Money.FromCents(100_000), Apr = 0.12, MonthsLeft = 12, Payment = Money.FromCents(10_000) });
            var m = MoneyCalendar.Month(s, e.Content.Balance);
            Assert.IsTrue(m[3].Items.Any(i => i.Key == "loan.pay" && i.Amount.Cents == 10_000));
            Assert.IsFalse(m[1].Items.Any(i => i.Key == "loan.pay"));
            Assert.AreEqual(10_000, MoneyCalendar.LoanDue(s).Cents);
        }

        [Test]
        public void The_last_loan_payment_is_the_balance_with_its_interest()
        {
            var s = new GameState();
            s.Loans.Add(new LoanState { Id = "l", Balance = Money.FromCents(12_000), Apr = 0.12, MonthsLeft = 1, Payment = Money.FromCents(10_000) });
            Assert.AreEqual(12_120, MoneyCalendar.LoanDue(s).Cents);
        }

        [Test]
        public void A_week_the_cash_cannot_cover_is_marked_and_the_cover_counts_the_weeks_before_it()
        {
            var e = Engine();
            var s = Earning(e);
            var m0 = MoneyCalendar.Month(s, e.Content.Balance);
            // enough for weeks 1-3 but not for the rent on week 4
            s.Cash = m0[0].Total + m0[1].Total + m0[2].Total + Money.FromCents(100);
            var m = MoneyCalendar.Month(s, e.Content.Balance);
            Assert.IsTrue(m[0].Covered && m[1].Covered && m[2].Covered);
            Assert.IsFalse(m[3].Covered, "rent on week 4 is a cash gap");
        }

        [Test]
        public void Money_already_planned_this_week_counts_against_the_cover()
        {
            var e = Engine();
            var s = Earning(e);
            var m0 = MoneyCalendar.Month(s, e.Content.Balance);
            s.Cash = m0[0].Total + Money.FromDollars(100);
            Assert.IsTrue(MoneyCalendar.Month(s, e.Content.Balance)[0].Covered);
            var m = MoneyCalendar.Month(s, e.Content.Balance, spentNow: Money.FromDollars(250));
            Assert.IsFalse(m[0].Covered, "a $250 course planned this week leaves the bills short");
        }

        [Test]
        public void Before_the_bills_start_a_week_is_free()
        {
            var e = Engine();
            var s = e.NewGame(1, TestContent.Hero);
            s.LifetimeEarnings = Money.Zero;
            var m = MoneyCalendar.Month(s, e.Content.Balance);
            Assert.IsTrue(m.All(w => w.Items.Count == 0 && w.Headline == null && w.Covered));
        }

        [Test]
        public void Due_next_week_in_the_report_matches_the_calendar()
        {
            var e = Engine();
            var s = Earning(e, week: 3);
            s.Loans.Add(new LoanState { Id = "l", Principal = Money.FromCents(100_000), Balance = Money.FromCents(100_000), Apr = 0.12, MonthsLeft = 12, Payment = Money.FromCents(10_000) });
            e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0));
            var cal = MoneyCalendar.Month(s, e.Content.Balance);
            var now = cal.Single(w => w.Now);
            Assert.AreEqual(4, now.WeekOfMonth);
            var paid = e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0)).Paid;   // what week 4 really takes
            Assert.AreEqual(paid.Sum(i => i.Amount.Cents), now.Total.Cents);
        }
    }
}
