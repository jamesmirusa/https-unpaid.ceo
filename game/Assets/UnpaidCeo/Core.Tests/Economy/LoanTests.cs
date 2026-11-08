using System.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Core.Tests.Game;
using static UnpaidCeo.Core.Tests.Game.Stage2Content;

namespace UnpaidCeo.Core.Tests.Economy
{
    /// <summary>Bank loans with annuity payments (stage 2A Task 4).</summary>
    public class LoanTests
    {
        [Test]
        public void Payment_matches_the_annuity()
        {
            Assert.AreEqual(47073, EconomyFormulas.LoanPayment(Money.FromCents(1_000_000), 0.12, 24).Cents);
            var (e, s) = Chapter2(0);
            Assert.IsTrue(e.TakeLoan(s, Money.FromCents(1_000_000), 24));
            Assert.AreEqual(47073, s.Loans.Single().Payment.Cents);
            Assert.AreEqual(1_000_000, s.Cash.Cents);
            Assert.IsTrue(s.Facts.Has("first_loan"));
        }

        [Test]
        public void Balance_reaches_zero_at_the_end_of_the_term()
        {
            var (e, s) = Chapter2(5_000_000, noPersonalCosts: true);
            Assert.IsTrue(e.TakeLoan(s, Money.FromCents(1_000_000), 12));
            long paid = 0;
            for (int m = 0; m < 12; m++) paid += e.EndMonth(s, Plan()).LoanPayments.Cents;
            Assert.IsEmpty(s.Loans);
            // 12 annuity payments of 88849 cents, the last one trimmed to what is owed.
            Assert.AreEqual(12 * EconomyFormulas.LoanPayment(Money.FromCents(1_000_000), 0.12, 12).Cents, paid, 12);
        }

        [Test]
        public void Loan_above_the_limit_is_rejected()
        {
            var (e, s) = Chapter2(0);
            Assert.AreEqual(3_500_000, e.MaxLoan(s).Cents, "1,000,000 + reputation 50 x 50,000");
            Assert.IsFalse(e.TakeLoan(s, Money.FromCents(3_500_001), 24));
            Assert.IsTrue(e.TakeLoan(s, Money.FromCents(2_000_000), 24));
            Assert.AreEqual(1_500_000, e.MaxLoan(s).Cents, "existing balances count against the limit");
        }

        [Test]
        public void Unknown_term_or_empty_principal_is_rejected()
        {
            var (e, s) = Chapter2(0);
            Assert.IsFalse(e.TakeLoan(s, Money.FromCents(100_000), 18));
            Assert.IsFalse(e.TakeLoan(s, Money.Zero, 12));
            Assert.IsEmpty(s.Loans);
            Assert.AreEqual(0, s.Log.Count(l => l.Kind == UnpaidCeo.Core.Game.DecisionLogEntry.TakeLoan));
        }

        [Test]
        public void Low_reputation_and_honesty_raise_apr()
        {
            var (e, s) = Chapter2(0);
            Assert.AreEqual(0.12, e.LoanApr(s), 1e-9);
            s.Life.Reputation = 30;
            Assert.AreEqual(0.14, e.LoanApr(s), 1e-9);
            s.Life.Honesty = 30;
            Assert.AreEqual(0.17, e.LoanApr(s), 1e-9);
        }

        [Test]
        public void Payments_continue_when_cash_is_negative()
        {
            var (e, s) = Chapter2(0);
            var payment = EconomyFormulas.LoanPayment(Money.FromCents(30_000_000), 0.12, 36);
            s.Loans.Add(new LoanState { Id = "big", Principal = Money.FromCents(30_000_000), Balance = Money.FromCents(30_000_000), Apr = 0.12, MonthsLeft = 36, Payment = payment });
            s.Cash = Money.FromCents(-200_000);
            var r = e.EndMonth(s, Plan());
            Assert.IsTrue(s.Facts.Has("in_debt"));
            var loan = s.Loans.Single();
            Assert.AreEqual(30_000_000 - (payment.Cents - 300_000), loan.Balance.Cents, "interest 1% of $300,000 is exactly 300,000 cents");
            Assert.AreEqual(35, loan.MonthsLeft);
            Assert.AreEqual(payment.Cents, r.LoanPayments.Cents);
            Assert.AreEqual(300_000, r.Interest.Cents);
        }

        [Test]
        public void Interest_is_deducted_before_the_tax_reserve()
        {
            var (e, s) = Chapter2(300_000, noPersonalCosts: true);
            e.EndMonth(s, Plan(Lever("shop", "units", 1)));
            Assert.IsTrue(e.TakeLoan(s, Money.FromCents(1_000_000), 12));
            var r = e.EndMonth(s, Plan());
            Assert.AreEqual(10_000, r.Interest.Cents);
            Assert.AreEqual((r.OperatingProfit - r.Interest).MulRound(0.25).Cents, r.TaxReserve.Cents);
            Assert.AreEqual((r.OperatingProfit - r.Interest - r.TaxReserve).Cents, r.NetProfit.Cents);
        }
    }
}
