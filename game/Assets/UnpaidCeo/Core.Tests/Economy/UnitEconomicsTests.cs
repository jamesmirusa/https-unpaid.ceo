using NUnit.Framework;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Economy
{
    /// <summary>The numbers the company screen teaches with: margin, break-even, CAC, LTV (stage 2A, Task 7).</summary>
    public class UnitEconomicsTests
    {
        // The real coffee cart: $4 a cup, $1.20 to make, $1,200 fixed a month, $2,800 per barista, 20 cups per customer.
        static IndustryDef Cart() => new IndustryDef
        {
            Id = "coffee_cart", Kind = IndustryKind.Business,
            Price = Money.FromCents(400), RefPrice = Money.FromCents(400), UnitCost = Money.FromCents(120),
            CapacityPerUnit = 3000, FixedMonthly = Money.FromCents(120_000), StaffPerUnit = 1,
            Salary = Money.FromCents(280_000), RepeatPurchases = 20,
        };

        static BusinessState CartState(int employees = 0, long marketingCents = 0) => new BusinessState
        {
            Id = "coffee_cart", IndustryId = "coffee_cart", Price = Money.FromCents(400), Units = 1,
            Employees = employees, Marketing = Money.FromCents(marketingCents),
        };

        static BusinessMonthResult Sold(int units, long marketingCents) => new BusinessMonthResult
        {
            UnitsSold = units, Marketing = Money.FromCents(marketingCents),
        };

        [Test]
        public void Coffee_cart_margin_and_break_even()
        {
            var v = UnitEconomics.Compute(CartState(), Cart(), Sold(0, 0), 0.10);
            Assert.AreEqual(0.70, v.GrossMargin, 1e-9);
            Assert.AreEqual(429, v.BreakEvenUnits);   // $1,200 / $2.80 = 428.6 → 429 cups

            // Break-even counts salaries: one barista with 10% overhead adds $3,080 a month.
            var staffed = UnitEconomics.Compute(CartState(employees: 1), Cart(), Sold(0, 0), 0.10);
            Assert.AreEqual(1529, staffed.BreakEvenUnits);   // $4,280 / $2.80 = 1528.6 → 1529

            // Selling below cost never breaks even.
            var cheap = CartState();
            cheap.Price = Money.FromCents(100);
            Assert.AreEqual(UnitEconomicsView.NeverBreaksEven, UnitEconomics.Compute(cheap, Cart(), Sold(0, 0), 0.10).BreakEvenUnits);
        }

        [Test]
        public void Ltv_is_margin_times_repeat_purchases()
        {
            var v = UnitEconomics.Compute(CartState(), Cart(), Sold(1000, 0), 0.10);
            Assert.AreEqual(5600, v.Ltv.Cents);   // ($4.00 − $1.20) × 20
        }

        [Test]
        public void Cac_is_marketing_per_new_customer()
        {
            // 2,000 cups at 20 cups per customer ≈ 100 new customers; $500 of marketing → $5 each.
            var v = UnitEconomics.Compute(CartState(marketingCents: 50_000), Cart(), Sold(2000, 50_000), 0.10);
            Assert.AreEqual(500, v.Cac.Cents);
            Assert.AreEqual(11.2, v.LtvToCac, 1e-9);

            // No sales yet: all the marketing falls on one customer, not a division by zero.
            var none = UnitEconomics.Compute(CartState(marketingCents: 50_000), Cart(), Sold(0, 50_000), 0.10);
            Assert.AreEqual(50_000, none.Cac.Cents);
        }

        [Test]
        public void No_marketing_means_infinite_ltv_to_cac()
        {
            var v = UnitEconomics.Compute(CartState(), Cart(), Sold(2000, 0), 0.10);
            Assert.AreEqual(0, v.Cac.Cents);
            Assert.IsTrue(double.IsPositiveInfinity(v.LtvToCac));
        }
    }
}
