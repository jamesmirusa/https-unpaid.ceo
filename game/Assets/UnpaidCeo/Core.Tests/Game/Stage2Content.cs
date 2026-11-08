using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>
    /// Fixture for chapter 2–3 engine tests: the chapter-1 test gig plus two round-number businesses
    /// ("shop": $1,000 capex, $10 price, $4 cost; "truck": B2B, two staff per unit). Demand never caps sales.
    /// </summary>
    internal static class Stage2Content
    {
        public const string Industries = @"{ ""industries"": [
  { ""id"": ""gig_test"", ""kind"": ""Gig"", ""price"": 100, ""refPrice"": 100, ""unitCost"": 0,
    ""baseDemand"": 1000000, ""elasticity"": 1.0, ""marketingA"": 0.0, ""marketingK"": 10000, ""capacityPerWorkPoint"": 1.0 },
  { ""id"": ""shop"", ""kind"": ""Business"", ""chapter"": 2, ""capex"": 100000, ""price"": 1000, ""refPrice"": 1000, ""unitCost"": 400,
    ""baseDemand"": 1000000, ""elasticity"": 1.0, ""marketingA"": 0.0, ""marketingK"": 10000, ""capacityPerUnit"": 100,
    ""fixedMonthly"": 20000, ""staffPerUnit"": 1, ""salary"": 50000, ""repeatPurchases"": 5,
    ""scaleNameKeys"": [""s0"", ""s1"", ""s2""], ""lessonId"": ""unit_economics"" },
  { ""id"": ""truck"", ""kind"": ""Business"", ""chapter"": 2, ""capex"": 2000000, ""price"": 50000, ""refPrice"": 50000, ""unitCost"": 10000,
    ""baseDemand"": 1000000, ""elasticity"": 1.0, ""marketingA"": 0.0, ""marketingK"": 10000, ""capacityPerUnit"": 20,
    ""fixedMonthly"": 100000, ""staffPerUnit"": 2, ""salary"": 300000, ""receivableDelayMonths"": 1, ""repeatPurchases"": 10,
    ""scaleNameKeys"": [""t0"", ""t1"", ""t2""], ""lessonId"": ""cash_gap_b2b"" }
] }";

        public static string Balance(bool noPersonalCosts = false) => (noPersonalCosts
            ? TestContent.Balance().Replace("\"room\": 100000, \"food\": 30000, \"transit\": 15000, \"essentials\": 5000",
                                            "\"room\": 0, \"food\": 0, \"transit\": 0, \"essentials\": 0")
            : TestContent.Balance()).Replace(@"""sources"": []", @"""chapter2Industries"": [""shop"", ""truck""],
  ""districtDemand"": { ""bronx"": 1.0, ""brooklyn"": 1.0, ""queens"": 1.15 },
  ""chapter2CapitalGoal"": 2500000, ""chapter3CapitalGoal"": 25000000, ""chapter3EmployeesGoal"": 10, ""chapter3UnitsGoal"": 2,
  ""assetResaleRate"": 0.5, ""payrollOverhead"": 0.10,
  ""loans"": { ""baseApr"": 0.12, ""aprPerMissingReputation"": 0.001, ""lowHonestyAprAdd"": 0.03, ""lowHonestyBelow"": 40,
               ""maxBase"": 1000000, ""maxPerReputation"": 50000, ""termsMonths"": [12, 24, 36] },
  ""dailyManualMonths"": 6,
  ""sources"": []");

        public static ContentDb Db(string lessons = null, string storylets = null, bool noPersonalCosts = false) => ContentDb.FromJson(new ContentFiles
        {
            Industries = Industries,
            Balance = Balance(noPersonalCosts),
            Storylets = storylets ?? TestContent.Storylets,
            FreeActions = TestContent.FreeActions,
            Lessons = lessons,
        });

        /// <summary>A chapter-2 game with the given cash and a "shop" (or "truck") business with no units yet.</summary>
        public static (GameEngine engine, GameState state) Chapter2(long cashCents, string industry = "shop", bool noPersonalCosts = false)
        {
            var e = new GameEngine(Db(noPersonalCosts: noPersonalCosts));
            var s = e.NewGame(42, new HeroAppearance());
            s.Chapter = 2;
            s.Cash = Money.FromCents(cashCents);
            var def = e.Content.Industries[industry];
            s.Businesses.Add(new BusinessState { Id = industry, IndustryId = industry, Price = def.Price });
            return (e, s);
        }

        public static MonthDecisions Plan(params LeverChange[] levers)
        {
            var d = TestContent.Decisions(50, 20, 20, 10);
            d.Levers.AddRange(levers);
            return d;
        }

        public static LeverChange Lever(string business, string lever, long value) =>
            new LeverChange { BusinessId = business, Lever = lever, ValueCents = value };
    }
}
