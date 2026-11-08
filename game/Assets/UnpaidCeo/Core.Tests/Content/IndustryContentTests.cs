using System.IO;
using System.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Economy;

namespace UnpaidCeo.Core.Tests.Content
{
    /// <summary>Chapter 2–3 industries and balance (stage 2A, Task 1).</summary>
    public class IndustryContentTests
    {
        static string ContentDir => Path.Combine(UnityEngine.Application.dataPath, "UnpaidCeo", "Content");

        static ContentDb Db(string industries = null)
        {
            string Read(string f) { var p = Path.Combine(ContentDir, f); return File.Exists(p) ? File.ReadAllText(p) : null; }
            return ContentDb.FromJson(new ContentFiles { Industries = industries ?? Read("industries.json"), Balance = Read("balance.json") });
        }

        static readonly string[] Chapter2 = { "coffee_cart", "resale_store", "food_truck", "ai_agency", "cleaning", "logistics_box_truck" };

        [Test]
        public void Six_chapter2_industries_load()
        {
            var db = Db();
            CollectionAssert.AreEquivalent(Chapter2, db.Balance.Chapter2Industries);
            foreach (var id in Chapter2)
            {
                Assert.IsTrue(db.Industries.TryGetValue(id, out var d), id);
                Assert.AreEqual(IndustryKind.Business, d.Kind, id);
                Assert.AreEqual(2, d.Chapter, id);
                Assert.AreEqual(12, d.Seasonality.Length, id);
                Assert.AreEqual(3, d.ScaleNameKeys.Count, id);
            }
            Assert.AreEqual(1, db.Industries["gig_delivery"].Chapter);
            Assert.IsTrue(db.Validate().IsValid, string.Join("\n", db.Validate().Errors));
        }

        [Test]
        public void Businesses_have_positive_margin_and_capex()
        {
            foreach (var id in Chapter2)
            {
                var d = Db().Industries[id];
                Assert.Greater(d.Price.Cents, d.UnitCost.Cents, id);
                Assert.Greater(d.Capex.Cents, 0, id);
                Assert.GreaterOrEqual(d.StaffPerUnit, 1, id);
                Assert.Greater(d.Salary.Cents, 0, id);
            }
        }

        [Test]
        public void Break_even_fits_in_one_staffed_unit()
        {
            var db = Db();
            foreach (var id in Chapter2)
            {
                var d = db.Industries[id];
                var fixedWithStaff = d.FixedMonthly + d.Salary.MulRound((d.StaffPerUnit - 1) * (1 + db.Balance.PayrollOverhead));
                Assert.LessOrEqual(EconomyFormulas.BreakEvenUnits(fixedWithStaff, d.Price, d.UnitCost), d.CapacityPerUnit, id);
            }
        }

        [Test]
        public void Balance_has_chapter_goals_loans_and_pacing()
        {
            var b = Db().Balance;
            Assert.AreEqual(2_500_000, b.Chapter2CapitalGoal.Cents);
            Assert.AreEqual(25_000_000, b.Chapter3CapitalGoal.Cents);
            Assert.AreEqual(10, b.Chapter3EmployeesGoal);
            Assert.AreEqual(2, b.Chapter3UnitsGoal);
            Assert.AreEqual(0.5, b.AssetResaleRate, 1e-9);
            Assert.AreEqual(0.10, b.PayrollOverhead, 1e-9);
            Assert.AreEqual(1.15, b.DistrictDemand["queens"], 1e-9);
            Assert.AreEqual(0.12, b.Loans.BaseApr, 1e-9);
            CollectionAssert.AreEqual(new[] { 12, 24, 36 }, b.Loans.TermsMonths);
            Assert.AreEqual(6, b.DailyManualMonths);
        }

        const string BrokenBusiness = @"{ ""industries"": [ { ""id"": ""x"", ""kind"": ""Business"", ""chapter"": 2, ""price"": 500, ""refPrice"": 500,
  ""unitCost"": 100, ""baseDemand"": 10, ""elasticity"": 1, ""marketingK"": 1000, ""capacityPerUnit"": 10, ""staffPerUnit"": 1,
  ""salary"": 1000, ""scaleNameKeys"": [""a"", ""b"", ""c""] } ] }";

        [Test]
        public void Validator_rejects_business_without_capex()
        {
            var r = Db(BrokenBusiness).Validate();
            Assert.That(r.Errors, Has.Some.Contains("capex"));
        }

        [Test]
        public void Validator_rejects_seasonality_not_12_long()
        {
            var json = BrokenBusiness.Replace(@"""salary"": 1000", @"""salary"": 1000, ""capex"": 100, ""seasonality"": [1, 1]");
            var r = Db(json).Validate();
            Assert.That(r.Errors, Has.Some.Contains("seasonality"));
            Assert.That(r.Errors, Has.None.Contains("capex"));
        }
    }
}
