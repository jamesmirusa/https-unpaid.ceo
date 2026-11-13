using System.Linq;
using NUnit.Framework;
using static UnpaidCeo.Core.Tests.Game.Stage2Content;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>Opening and closing units, hiring and firing (stage 2A Task 3).</summary>
    public class ExpansionTests
    {
        [Test]
        public void Opening_a_unit_pays_capex_from_cash()
        {
            var (e, s) = Chapter2(300000);
            var r = e.EndMonth(s, Plan(Lever("shop", "units", 1)));
            Assert.AreEqual(1, s.Businesses.Find(b => b.Id == "shop").Units);
            Assert.AreEqual(100000, r.Capex.Cents);
            Assert.IsEmpty(r.RejectedLevers);
        }

        [Test]
        public void Opening_a_unit_without_cash_is_rejected()
        {
            var (e, s) = Chapter2(50000);
            var r = e.EndMonth(s, Plan(Lever("shop", "units", 1)));
            Assert.AreEqual(0, s.Businesses.Find(b => b.Id == "shop").Units);
            CollectionAssert.Contains(r.RejectedLevers, "units:shop");
            Assert.AreEqual(0, r.Capex.Cents);
        }

        [Test]
        public void Closing_a_unit_refunds_half_capex()
        {
            var (e, s) = Chapter2(500000);
            e.EndMonth(s, Plan(Lever("shop", "units", 2)));
            var r = e.EndMonth(s, Plan(Lever("shop", "units", 1)));
            Assert.AreEqual(1, s.Businesses.Find(b => b.Id == "shop").Units);
            Assert.AreEqual(50000, r.AssetSales.Cents);
        }

        [Test]
        public void Staff_target_hires_and_fires()
        {
            var (e, s) = Chapter2(500000, "truck");
            s.Cash = UnpaidCeo.Core.Primitives.Money.FromCents(5_000_000);
            e.EndMonth(s, Plan(Lever("truck", "units", 1), Lever("truck", "staff", 3)));
            var truck = s.Businesses.Find(b => b.Id == "truck");
            Assert.AreEqual(3, truck.Employees);
            e.EndMonth(s, Plan(Lever("truck", "staff", 9)));
            Assert.AreEqual(4, truck.Employees, "clamped to units x staffPerUnit x 2");
            e.EndMonth(s, Plan(Lever("truck", "staff", 0)));
            Assert.AreEqual(0, truck.Employees);
        }

        [Test]
        public void First_hire_and_second_unit_set_facts()
        {
            var (e, s) = Chapter2(500000);
            e.EndMonth(s, Plan(Lever("shop", "units", 1), Lever("shop", "staff", 1)));
            Assert.IsTrue(s.Facts.Has("first_hire"));
            Assert.IsFalse(s.Facts.Has("second_unit"));
            e.EndMonth(s, Plan(Lever("shop", "units", 2)));
            Assert.IsTrue(s.Facts.Has("second_unit"));
            Assert.IsTrue(s.Facts.Has("first_payroll"));
        }

        [Test]
        public void Gig_levers_ignore_units_and_staff()
        {
            var (e, s) = Chapter2(500000);
            e.EndMonth(s, Plan(Lever("gig_test", "units", 3), Lever("gig_test", "staff", 2)));
            var gig = s.Businesses.Find(b => b.Id == "gig_test");
            Assert.AreEqual(0, gig.Units);
            Assert.AreEqual(0, gig.Employees);
        }

        [Test]
        public void Price_change_and_marketing_set_facts_once()
        {
            var (e, s) = Chapter2(500000);
            e.EndMonth(s, Plan(Lever("shop", "units", 1), Lever("shop", "price", 1200), Lever("shop", "marketing", 5000)));
            Assert.AreEqual(1, s.Facts.MonthSet("price_changed"));
            Assert.AreEqual(1, s.Facts.MonthSet("marketing_started"));
            e.EndMonth(s, Plan(Lever("shop", "price", 900)));
            Assert.AreEqual(1, s.Facts.MonthSet("price_changed"));
        }

        [Test]
        public void Buying_a_unit_is_an_investment_not_a_first_loss()
        {
            var (e, s) = Chapter2(300000, noPersonalCosts: true);
            e.EndMonth(s, Plan(Lever("shop", "units", 1)));
            Assert.IsFalse(s.Facts.Has("first_loss"), "capex lowers cash but is not a loss");
        }
    
        [Test]
        public void Month_report_keeps_each_business_result()
        {
            var (e, s) = Stage2Content.Chapter2(1_000_000, noPersonalCosts: true);
            s.Businesses.Single(b => b.IndustryId == "shop").Units = 1;
            var r = e.EndMonth(s, Stage2Content.Plan());
            Assert.IsTrue(r.BusinessResults.TryGetValue("shop", out var shop));
            Assert.AreEqual(100, shop.UnitsSold);
            Assert.AreEqual(100_000, shop.Revenue.Cents);
        }

        [Test]
        public void Gigs_stop_paying_once_a_business_unit_opens()
        {
            var (e, s) = Stage2Content.Chapter2(1_000_000, noPersonalCosts: true);
            var before = e.EndMonth(s, Stage2Content.Plan());
            Assert.Greater(before.BusinessResults["gig_test"].Revenue.Cents, 0, "no unit yet: the gig still pays");
            s.Businesses.Single(b => b.IndustryId == "shop").Units = 1;
            var after = e.EndMonth(s, Stage2Content.Plan());
            Assert.AreEqual(0, after.BusinessResults["gig_test"].Revenue.Cents, "the owner now works the business");
        }

        [Test]
        public void Owner_staffs_the_business_with_their_work_time()
        {
            var (e, s) = Stage2Content.Chapter2(1_000_000, noPersonalCosts: true);
            s.Businesses.Single(b => b.IndustryId == "shop").Units = 1;
            var half = TestContent.Decisions(30, 25, 35, 10);   // work 30 of a full-time 60
            Assert.AreEqual(50, e.EndMonth(s, half).BusinessResults["shop"].UnitsSold);
            Assert.AreEqual(100, e.EndMonth(s, Stage2Content.Plan()).BusinessResults["shop"].UnitsSold);
        }
}
}
