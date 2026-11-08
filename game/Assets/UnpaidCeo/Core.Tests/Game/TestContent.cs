using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Life;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>Small deterministic content set for engine tests: one $1/unit gig, no demand cap.</summary>
    internal static class TestContent
    {
        public static string Industries(double capacityPerWorkPoint) => $@"{{ ""industries"": [
  {{ ""id"": ""gig_test"", ""kind"": ""Gig"", ""price"": 100, ""refPrice"": 100, ""unitCost"": 0,
     ""baseDemand"": 1000000, ""elasticity"": 1.0, ""marketingA"": 0.0, ""marketingK"": 10000, ""capacityPerWorkPoint"": {capacityPerWorkPoint.ToString(System.Globalization.CultureInfo.InvariantCulture)} }}
] }}";

        public static string Balance(double errorChance = 0.0, string weights = "[0, 1, 0]") => $@"{{
  ""taxRate"": 0.25, ""billsStartAfter"": 10000,
  ""personalCosts"": {{ ""room"": 100000, ""food"": 30000, ""transit"": 15000, ""essentials"": 5000 }},
  ""chapter1CashGoal"": 100000, ""debtBankruptLine"": -50000, ""eventsPerMonthWeights"": {weights},
  ""autopilotHoursPerMonth"": 4, ""autopilotMaxMonths"": 2, ""tutorialGigPay"": 4000,
  ""chapter1Gigs"": [""gig_test""],
  ""life"": {{ ""restBaseline"": 25, ""restCoef"": 0.4, ""workSafe"": 60, ""burnCoef"": 0.5, ""familyBaseline"": 15,
              ""familyCoef"": 0.5, ""studyCoef"": 1.0, ""baseActionPoints"": 3, ""burnoutThreshold"": 30,
              ""burnoutErrorChance"": {errorChance.ToString(System.Globalization.CultureInfo.InvariantCulture)} }},
  ""sources"": []
}}";

        public const string Storylets = @"{ ""storylets"": [
  { ""id"": ""tony_intro"", ""yarnNode"": ""tony_intro"", ""arc"": ""tony"", ""once"": true,
    ""choices"": { ""help"": [ { ""npc"": ""tony"", ""delta"": 15 }, { ""setFact"": ""helped_tony"" } ],
                   ""ignore"": [ { ""npc"": ""tony"", ""delta"": -5 } ] } },
  { ""id"": ""rainy_day"", ""yarnNode"": ""rainy_day"", ""cooldownMonths"": 2,
    ""choices"": { ""stay_in"": [ { ""stat"": ""energy"", ""delta"": 5 } ], ""work_anyway"": [ { ""cash"": 2000 } ] } },
  { ""id"": ""tony_returns"", ""yarnNode"": ""tony_returns"", ""once"": true,
    ""when"": [ { ""sinceFact"": ""helped_tony"", ""min"": 2 } ],
    ""choices"": { ""accept"": [ { ""cash"": 50000 }, { ""npc"": ""tony"", ""delta"": 10 } ] } }
] }";

        public const string FreeActions = @"{ ""freeActions"": [
  { ""id"": ""overtime"", ""actionPoints"": 2, ""cost"": 0, ""effects"": [ { ""stat"": ""energy"", ""delta"": -10 }, { ""cash"": 5000 } ], ""journalKey"": ""journal.overtime"" },
  { ""id"": ""network"", ""actionPoints"": 2, ""cost"": 0, ""effects"": [ { ""stat"": ""connections"", ""delta"": 10 } ], ""journalKey"": ""journal.network"" },
  { ""id"": ""save"", ""actionPoints"": 0, ""cost"": 0, ""effects"": [ { ""reserve"": 50000 } ], ""journalKey"": ""journal.save"" }
] }";

        public static ContentDb Db(double capacityPerWorkPoint = 1.0, double errorChance = 0.0, string weights = "[0, 1, 0]") =>
            ContentDb.FromJson(new ContentFiles
            {
                Industries = Industries(capacityPerWorkPoint),
                Balance = Balance(errorChance, weights),
                Storylets = Storylets,
                FreeActions = FreeActions,
            });

        public static MonthDecisions Decisions(int work, int family, int rest, int study, params string[] freeActions)
        {
            var d = new MonthDecisions { Work = work, Family = family, Rest = rest, Study = study };
            d.WorkSplit["gig_test"] = 100;
            d.FreeActions.AddRange(freeActions);
            return d;
        }

        public static HeroAppearance Hero => new HeroAppearance { Species = "raccoon", Outfit = 0, ColorIndex = 0 };
    }
}
