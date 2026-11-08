using NUnit.Framework;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Economy
{
    public class EconomyFormulasTests
    {
        [Test]
        public void Demand_example_matches_hand_calculation()
        {
            double m = EconomyFormulas.MarketingFactor(Money.FromDollars(500), 0.25, Money.FromDollars(500)); // 1 + 0.25·ln2
            Assert.AreEqual(1.173287, m, 1e-6);
            Assert.AreEqual(1173, EconomyFormulas.Demand(1000, 1.0, m, 1.0, 1.0, 1.0));
            Assert.AreEqual(353, EconomyFormulas.Demand(1000,
                EconomyFormulas.PriceFactor(Money.FromCents(900), Money.FromCents(450), 1.5)));
        }

        [Test]
        public void Demand_does_not_lose_a_unit_to_float_error()
        {
            Assert.AreEqual(500, EconomyFormulas.Demand(1000, 0.5));
            Assert.AreEqual(300, EconomyFormulas.Demand(1000, 0.1, 3.0));
        }

        [Test]
        public void Units_sold_capped_by_capacity()
        {
            Assert.AreEqual(800, EconomyFormulas.UnitsSold(1173, 800));
            Assert.AreEqual(500, EconomyFormulas.UnitsSold(500, 800));
        }

        [Test]
        public void Margin_breakeven_tax()
        {
            Assert.AreEqual(0.7333, EconomyFormulas.GrossMargin(Money.FromCents(450), Money.FromCents(120)), 1e-4);
            Assert.AreEqual(304, EconomyFormulas.BreakEvenUnits(Money.FromDollars(1000), Money.FromCents(450), Money.FromCents(120)));
            Assert.AreEqual(int.MaxValue, EconomyFormulas.BreakEvenUnits(Money.FromDollars(1000), Money.FromCents(100), Money.FromCents(120)));
            Assert.AreEqual(28_500, EconomyFormulas.TaxReserve(Money.FromCents(114_000), 0.25).Cents);
            Assert.AreEqual(0, EconomyFormulas.TaxReserve(Money.FromCents(-5_000), 0.25).Cents);
        }

        [Test]
        public void Loan_payment_12pct_12m()
        {
            Assert.AreEqual(88_849, EconomyFormulas.LoanPayment(Money.FromDollars(10_000), 0.12, 12).Cents);
            Assert.AreEqual(50_000, EconomyFormulas.LoanPayment(Money.FromDollars(6_000), 0.0, 12).Cents);
        }
    }
}
