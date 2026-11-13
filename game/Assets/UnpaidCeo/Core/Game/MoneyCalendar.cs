using System.Collections.Generic;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Game
{
    /// <summary>One week of the month in the calendar: what money leaves that week and whether the cash covers it.</summary>
    public sealed class CalendarWeek
    {
        /// <summary>1..4 within the month.</summary>
        public int WeekOfMonth { get; set; }
        /// <summary>The absolute week number.</summary>
        public int Week { get; set; }
        public bool Past { get; set; }
        public bool Now { get; set; }
        public List<LineItem> Items { get; } = new List<LineItem>();
        public Money Total { get; set; }
        /// <summary>The item the cell is named after (rent, essentials, loan, food), null for a free week.</summary>
        public string Headline { get; set; }
        /// <summary>True when today's cash, minus everything due from now through this week, stays at zero or above.</summary>
        public bool Covered { get; set; } = true;
    }

    /// <summary>
    /// Stage 5B · the money dates of the current month, from the same rules <see cref="GameEngine.EndWeek"/> pays by:
    /// personal costs by week (<see cref="PersonalCosts.ForWeek"/>, once bills have started) and loan payments at the
    /// month's end (<see cref="LoanDue"/>). Pure: it explains, it never changes the game.
    /// </summary>
    public static class MoneyCalendar
    {
        /// <summary>What the loans take at the end of this month: the payment, or the whole balance with a month's interest on the last one.</summary>
        public static Money LoanDue(GameState s)
        {
            var due = Money.Zero;
            foreach (var loan in s.Loans)
                due += loan.MonthsLeft <= 1 ? loan.Balance + loan.Balance.MulRound(loan.Apr / 12.0) : loan.Payment;
            return due;
        }

        /// <param name="spentNow">Money this week's plan already takes at the week's end (the price of deeds tapped this week).</param>
        public static List<CalendarWeek> Month(GameState s, Balance b, Money spentNow = default)
        {
            int now = s.WeekOfMonth, n = GameState.WeeksPerMonth;
            int first = (s.Week <= 0 ? 1 : s.Week) - (now - 1);
            bool bills = s.LifetimeEarnings >= b.BillsStartAfter;
            var costs = b.PersonalCostsFor(s.Chapter);
            var loan = LoanDue(s);
            var weeks = new List<CalendarWeek>();
            var left = s.Cash;
            for (int w = 1; w <= n; w++)
            {
                var cw = new CalendarWeek { WeekOfMonth = w, Week = first + w - 1, Past = w < now, Now = w == now };
                if (bills) cw.Items.AddRange(costs.ForWeek(w));
                if (w == n && loan.Cents > 0) cw.Items.Add(new LineItem("loan.pay", loan));
                var total = Money.Zero;
                foreach (var i in cw.Items) total += i.Amount;
                cw.Total = total;
                cw.Headline = Headline(cw.Items);
                if (!cw.Past)
                {
                    left -= total;
                    if (cw.Now) left -= spentNow;
                    cw.Covered = left.Cents >= 0;
                }
                weeks.Add(cw);
            }
            return weeks;
        }

        static string Headline(List<LineItem> items)
        {
            if (items.Count == 0) return null;
            foreach (var key in new[] { "bill.room", "loan.pay", "bill.essentials", "bill.food" })
                foreach (var i in items) if (i.Key == key) return key;
            return items[0].Key;
        }
    }
}
