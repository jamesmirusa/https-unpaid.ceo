using System.IO;
using NUnit.Framework;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Economy;

namespace UnpaidCeo.Core.Tests.Content
{
    public class ContentDbTests
    {
        internal const string IndustriesJson = @"{ ""industries"": [
  { ""id"": ""gig_delivery"", ""kind"": ""Gig"", ""price"": 2600, ""refPrice"": 2600, ""unitCost"": 150,
    ""baseDemand"": 100000, ""elasticity"": 0.5, ""marketingA"": 0.0, ""marketingK"": 10000, ""capacityPerWorkPoint"": 1.73 },
  { ""id"": ""gig_resale"", ""kind"": ""Gig"", ""price"": 4500, ""refPrice"": 4500, ""unitCost"": 2400,
    ""baseDemand"": 40, ""elasticity"": 1.8, ""marketingA"": 0.3, ""marketingK"": 10000, ""capacityPerWorkPoint"": 0.5 },
  { ""id"": ""gig_ai_freelance"", ""kind"": ""Gig"", ""price"": 4500, ""refPrice"": 4500, ""unitCost"": 300,
    ""baseDemand"": 15, ""elasticity"": 1.2, ""marketingA"": 0.4, ""marketingK"": 10000, ""capacityPerWorkPoint"": 0.35, ""requiredStudy"": 30 }
] }";

        internal const string BalanceJson = @"{
  ""taxRate"": 0.25, ""billsStartAfter"": 10000,
  ""personalCosts"": { ""room"": 110000, ""food"": 31200, ""transit"": 15200, ""essentials"": 15000 },
  ""chapter1CashGoal"": 100000, ""debtBankruptLine"": -50000, ""eventsPerMonthWeights"": [1, 2, 1],
  ""autopilotHoursPerMonth"": 4, ""autopilotMaxMonths"": 2, ""tutorialGigPay"": 4000,
  ""chapter1Gigs"": [""gig_delivery"", ""gig_resale"", ""gig_ai_freelance""],
  ""life"": { ""restBaseline"": 25, ""restCoef"": 0.4, ""workSafe"": 60, ""burnCoef"": 0.5, ""familyBaseline"": 15,
              ""familyCoef"": 0.5, ""studyCoef"": 1.0, ""baseActionPoints"": 3, ""burnoutThreshold"": 30, ""burnoutErrorChance"": 0.25 },
  ""sources"": []
}";

        static ContentDb Load(string industries = IndustriesJson, string balance = BalanceJson, string freeActions = null) =>
            ContentDb.FromJson(new ContentFiles { Industries = industries, Balance = balance, FreeActions = freeActions });

        [Test]
        public void Loads_fixture_content()
        {
            var db = Load();
            Assert.AreEqual(3, db.Industries.Count);
            Assert.AreEqual(IndustryKind.Gig, db.Industries["gig_delivery"].Kind);
            Assert.AreEqual(2600, db.Industries["gig_delivery"].Price.Cents);
            Assert.AreEqual(30, db.Industries["gig_ai_freelance"].RequiredStudy);
            Assert.AreEqual(0.25, db.Balance.TaxRate);
            Assert.AreEqual(10_000, db.Balance.BillsStartAfter.Cents);
            Assert.AreEqual(171_400, db.Balance.PersonalCosts.Total.Cents);
            Assert.AreEqual(3, db.Balance.Life.BaseActionPoints);
            Assert.AreEqual(0, db.FreeActions.Count);
            Assert.IsTrue(db.Validate().IsValid, string.Join("; ", db.Validate().Errors));
        }

        [Test]
        public void Validate_reports_unknown_industry_and_negative_price()
        {
            var badIndustries = IndustriesJson.Replace("\"price\": 4500, \"refPrice\": 4500, \"unitCost\": 2400", "\"price\": -1, \"refPrice\": 4500, \"unitCost\": 2400");
            var badBalance = BalanceJson.Replace("\"gig_resale\", \"gig_ai_freelance\"]", "\"gig_resale\", \"gig_ghost\"]");
            var result = Load(badIndustries, badBalance).Validate();
            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("gig_resale", string.Join("\n", result.Errors));
            StringAssert.Contains("gig_ghost", string.Join("\n", result.Errors));
        }

        [Test]
        public void Free_actions_load_with_raw_effects()
        {
            var db = Load(freeActions: @"{ ""freeActions"": [ { ""id"": ""rest"", ""actionPoints"": 1, ""cost"": 0,
                ""effects"": [ { ""stat"": ""energy"", ""delta"": 15 } ], ""journalKey"": ""journal.rest"" } ] }");
            Assert.AreEqual(1, db.FreeActions.Count);
            Assert.AreEqual(1, db.FreeActions["rest"].ActionPoints);
            Assert.AreEqual(15, (int)db.FreeActions["rest"].Effects[0]["delta"]);
        }

        [Test]
        public void Real_content_is_valid()
        {
            string dir = Path.Combine(UnityEngine.Application.dataPath, "UnpaidCeo/Content");
            string Read(string f) { var p = Path.Combine(dir, f); return File.Exists(p) ? File.ReadAllText(p) : null; }
            var db = ContentDb.FromJson(new ContentFiles
            {
                Industries = Read("industries.json"),
                Balance = Read("balance.json"),
                FreeActions = Read("free_actions.json"),
            });
            var result = db.Validate();
            Assert.IsTrue(result.IsValid, string.Join("\n", result.Errors));
            Assert.IsTrue(db.Balance.Sources.Count >= 4, "balance.json must cite its sources");
        }
    }
}
