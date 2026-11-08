using NUnit.Framework;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Economy
{
    public class BusinessSimulatorTests
    {
        static IndustryDef Cart(int receivableDelay = 0) => new IndustryDef
        {
            Id = "coffee_cart", Kind = IndustryKind.Business,
            Price = Money.FromCents(450), RefPrice = Money.FromCents(450), UnitCost = Money.FromCents(120),
            BaseDemand = 1000, Elasticity = 1.5, MarketingA = 0.25, MarketingK = Money.FromDollars(500),
            CapacityPerUnit = 800, FixedMonthly = Money.FromDollars(1000), ReceivableDelayMonths = receivableDelay,
        };

        static BusinessState CartState() => new BusinessState
        {
            Id = "b1", IndustryId = "coffee_cart", LotId = "bronx_01",
            Price = Money.FromCents(450), Units = 1, Marketing = Money.FromDollars(500),
        };

        static readonly BusinessInputs Neutral = new BusinessInputs(1.0, 1.0, 1.0, 0);

        [Test]
        public void Cart_month_matches_hand_calculation()
        {
            var r = BusinessSimulator.Simulate(CartState(), Cart(), Neutral);
            Assert.AreEqual(800, r.UnitsSold);
            Assert.AreEqual(360_000, r.Revenue.Cents);
            Assert.AreEqual(96_000, r.Cogs.Cents);
            Assert.AreEqual(264_000, r.GrossProfit.Cents);
            Assert.AreEqual(100_000, r.Fixed.Cents);
            Assert.AreEqual(50_000, r.Marketing.Cents);
            Assert.AreEqual(114_000, r.OperatingProfit.Cents);
            Assert.AreEqual(360_000, r.CashCollected.Cents);
        }

        [Test]
        public void B2B_receivables_arrive_after_delay()
        {
            var state = CartState();
            var month1 = BusinessSimulator.Simulate(state, Cart(receivableDelay: 1), Neutral);
            Assert.AreEqual(0, month1.CashCollected.Cents);
            var month2 = BusinessSimulator.Simulate(state, Cart(receivableDelay: 1), Neutral);
            Assert.AreEqual(month1.Revenue.Cents, month2.CashCollected.Cents);
            Assert.AreEqual(1, state.Receivables.Count);
        }

        [Test]
        public void Gig_capacity_scales_with_work_share()
        {
            var gig = new IndustryDef
            {
                Id = "gig_delivery", Kind = IndustryKind.Gig,
                Price = Money.FromCents(2600), RefPrice = Money.FromCents(2600), UnitCost = Money.FromCents(150),
                BaseDemand = 100_000, Elasticity = 0.5, MarketingK = Money.FromDollars(100), CapacityPerWorkPoint = 1.0,
            };
            var state = new BusinessState { Id = "g1", IndustryId = "gig_delivery", Price = Money.FromCents(2600) };
            Assert.AreEqual(100, BusinessSimulator.Simulate(state, gig, new BusinessInputs(1, 1, 1, 100)).UnitsSold);
            Assert.AreEqual(50, BusinessSimulator.Simulate(state, gig, new BusinessInputs(1, 1, 1, 50)).UnitsSold);
            Assert.AreEqual(0, BusinessSimulator.Simulate(state, gig, new BusinessInputs(1, 1, 1, 0)).UnitsSold);
        }
    }
}
