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
    
        // ---- Stage 2A Task 2: staffing, salaries, seasonality, district ----

        static IndustryDef Coffee() => new IndustryDef
        {
            Id = "coffee_cart", Kind = IndustryKind.Business, Chapter = 2,
            Price = Money.FromCents(400), RefPrice = Money.FromCents(400), UnitCost = Money.FromCents(120),
            BaseDemand = 100000, Elasticity = 1.4, MarketingA = 0.35, MarketingK = Money.FromCents(50000),
            CapacityPerUnit = 3000, FixedMonthly = Money.FromCents(120000), StaffPerUnit = 1, Salary = Money.FromCents(280000),
        };

        static IndustryDef Truck() => new IndustryDef
        {
            Id = "food_truck", Kind = IndustryKind.Business, Chapter = 2,
            Price = Money.FromCents(1200), RefPrice = Money.FromCents(1200), UnitCost = Money.FromCents(400),
            BaseDemand = 1800, Elasticity = 1.2, MarketingA = 0.3, MarketingK = Money.FromCents(50000),
            CapacityPerUnit = 2500, FixedMonthly = Money.FromCents(350000), StaffPerUnit = 2, Salary = Money.FromCents(300000),
        };

        static BusinessState State(IndustryDef d, int units, int employees) => new BusinessState
        {
            Id = d.Id, IndustryId = d.Id, Price = d.Price, Units = units, Employees = employees,
        };

        [Test]
        public void Owner_alone_runs_one_coffee_cart_at_full_capacity()
        {
            var r = BusinessSimulator.Simulate(State(Coffee(), 1, 0), Coffee(), Neutral);
            Assert.AreEqual(3000, r.UnitsSold);
        }

        [Test]
        public void Food_truck_needs_a_hire_for_full_capacity()
        {
            var big = new BusinessInputs(1.0, 1.0, 1.0, 0, districtFactor: 10.0);
            Assert.AreEqual(1250, BusinessSimulator.Simulate(State(Truck(), 1, 0), Truck(), big).UnitsSold);
            Assert.AreEqual(2500, BusinessSimulator.Simulate(State(Truck(), 1, 1), Truck(), big).UnitsSold);
        }

        [Test]
        public void Salaries_with_payroll_overhead_are_fixed_costs()
        {
            var r = BusinessSimulator.Simulate(State(Truck(), 1, 2), Truck(), new BusinessInputs(1.0, 1.0, 1.0, 0, payrollOverhead: 0.10));
            Assert.AreEqual(350000 + 660000, r.Fixed.Cents);
        }

        [Test]
        public void Units_multiply_capacity_and_fixed_costs()
        {
            var r = BusinessSimulator.Simulate(State(Coffee(), 2, 1), Coffee(), Neutral);
            Assert.AreEqual(6000, r.UnitsSold);
            Assert.AreEqual(2 * 120000 + 280000, r.Fixed.Cents);
        }

        [Test]
        public void Seasonality_and_district_multiply_demand()
        {
            // July food truck (season 1.4) in Queens (1.15): floor(1800 x 1.4 x 1.15) = 2898, capped by capacity 2500 with one hire.
            var july = new BusinessInputs(1.0, 1.4, 1.0, 0, districtFactor: 1.15);
            Assert.AreEqual(2500, BusinessSimulator.Simulate(State(Truck(), 1, 1), Truck(), july).UnitsSold);
            var truck = Truck();
            truck.BaseDemand = 1000;
            Assert.AreEqual(1610, BusinessSimulator.Simulate(State(truck, 1, 1), truck, july).UnitsSold);
        }

        [Test]
        public void Closed_business_sells_nothing_but_still_pays_staff()
        {
            var r = BusinessSimulator.Simulate(State(Truck(), 0, 2), Truck(), new BusinessInputs(1.0, 1.0, 1.0, 0, payrollOverhead: 0.10));
            Assert.AreEqual(0, r.UnitsSold);
            Assert.AreEqual(660000, r.Fixed.Cents);
        }
    }
}
