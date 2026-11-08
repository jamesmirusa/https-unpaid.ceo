using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Narrative
{
    public class NarrativeTests
    {
        static NarrativeContext Ctx(int month = 5, long cashCents = 10_000)
        {
            var facts = new FactStore();
            facts.Set("helped_tony", 2);
            var npcs = new NpcRelations();
            npcs.Add("tony", 25);
            return new NarrativeContext
            {
                Month = month, Chapter = 1, Cash = Money.FromCents(cashCents),
                Life = new LifeStats { Energy = 45, Honesty = 70 },
                Facts = facts, Npcs = npcs, IndustryIds = new[] { "gig_resale" },
            };
        }

        static bool Eval(string json, NarrativeContext c) => Condition.Parse(JObject.Parse(json)).Evaluate(c);

        [Test]
        public void Conditions_parse_and_evaluate()
        {
            var c = Ctx();
            Assert.IsTrue(Eval(@"{""fact"":""helped_tony""}", c));
            Assert.IsFalse(Eval(@"{""fact"":""cheated_supplier""}", c));
            Assert.IsTrue(Eval(@"{""notFact"":""cheated_supplier""}", c));
            Assert.IsTrue(Eval(@"{""stat"":""energy"",""op"":"">="",""value"":40}", c));
            Assert.IsFalse(Eval(@"{""stat"":""energy"",""op"":""<"",""value"":40}", c));
            Assert.IsTrue(Eval(@"{""npc"":""tony"",""op"":"">="",""value"":20}", c));
            Assert.IsFalse(Eval(@"{""npc"":""loan_shark"",""op"":"">"",""value"":0}", c));
            Assert.IsFalse(Eval(@"{""cash"":{""op"":""<"",""value"":0}}", c));
            Assert.IsTrue(Eval(@"{""cash"":{""op"":""<"",""value"":0}}", Ctx(cashCents: -100)));
            Assert.IsTrue(Eval(@"{""chapter"":1}", c));
            Assert.IsTrue(Eval(@"{""industry"":""gig_resale""}", c));
            Assert.IsFalse(Eval(@"{""industry"":""gig_delivery""}", c));
        }

        [Test]
        public void Unknown_condition_or_stat_is_rejected()
        {
            Assert.Throws<System.FormatException>(() => Condition.Parse(JObject.Parse(@"{""weather"":""rain""}")));
            Assert.Throws<System.FormatException>(() => Condition.Parse(JObject.Parse(@"{""stat"":""luck"",""op"":"">"",""value"":1}")));
            Assert.Throws<System.FormatException>(() => Effect.Parse(JObject.Parse(@"{""teleport"":1}")));
        }

        [Test]
        public void SinceFact_window()
        {
            const string w = @"{""sinceFact"":""helped_tony"",""min"":3,""max"":12}";
            Assert.IsFalse(Eval(w, Ctx(month: 4)));
            Assert.IsTrue(Eval(w, Ctx(month: 5)));
            Assert.IsTrue(Eval(w, Ctx(month: 14)));
            Assert.IsFalse(Eval(w, Ctx(month: 15)));
            Assert.IsFalse(Eval(@"{""sinceFact"":""never_set"",""min"":0}", Ctx()));
        }

        [Test]
        public void Effects_parse_to_typed_effects()
        {
            Assert.IsInstanceOf<CashEffect>(Effect.Parse(JObject.Parse(@"{""cash"":-5000}")));
            var stat = (StatEffect)Effect.Parse(JObject.Parse(@"{""stat"":""honesty"",""delta"":-10}"));
            Assert.AreEqual("honesty", stat.Stat);
            Assert.AreEqual(-10, stat.Delta);
            Assert.IsInstanceOf<NpcEffect>(Effect.Parse(JObject.Parse(@"{""npc"":""tony"",""delta"":15}")));
            Assert.IsInstanceOf<SetFactEffect>(Effect.Parse(JObject.Parse(@"{""setFact"":""x""}")));
            Assert.IsInstanceOf<ClearFactEffect>(Effect.Parse(JObject.Parse(@"{""clearFact"":""x""}")));
            Assert.AreEqual(10_000, ((ReserveEffect)Effect.Parse(JObject.Parse(@"{""reserve"":10000}"))).Amount.Cents);
            Assert.AreEqual("revenue_vs_profit", ((LessonEffect)Effect.Parse(JObject.Parse(@"{""lesson"":""revenue_vs_profit""}"))).LessonId);
        }

        [Test]
        public void Npc_clamped()
        {
            var n = new NpcRelations();
            n.Add("tony", 150);
            Assert.AreEqual(100, n.Get("tony"));
            n.Add("tony", -500);
            Assert.AreEqual(-100, n.Get("tony"));
            Assert.AreEqual(0, n.Get("nobody"));
        }

        static StoryletDef S(string id, int weight = 1, bool once = false, int cooldown = 0, string when = null) =>
            StoryletDef.FromJson(JObject.Parse($@"{{ ""id"": ""{id}"", ""yarnNode"": ""{id}"", ""weight"": {weight},
                ""once"": {(once ? "true" : "false")}, ""cooldownMonths"": {cooldown},
                ""when"": [{when ?? ""}], ""choices"": {{ ""ok"": [] }} }}"));

        static readonly int[] AlwaysOne = { 0, 1, 0 };

        [Test]
        public void No_eligible_storylets_returns_empty()
        {
            var all = new List<StoryletDef> { S("needs_fact", when: @"{""fact"":""never""}") };
            var picks = StoryletSelector.Pick(all, Ctx(), new Dictionary<string, int>(), new HashSet<string>(), new Pcg32(1, 1), AlwaysOne);
            Assert.AreEqual(0, picks.Count);
            Assert.AreEqual(0, StoryletSelector.Pick(new List<StoryletDef>(), Ctx(), new Dictionary<string, int>(), new HashSet<string>(), new Pcg32(1, 1), new[] { 0, 0, 1 }).Count);
        }

        [Test]
        public void Once_never_repeats_over_100_months()
        {
            var all = new List<StoryletDef> { S("once_a", once: true), S("filler", weight: 1) };
            var last = new Dictionary<string, int>();
            var seen = new HashSet<string>();
            var rng = new Pcg32(3, 7);
            int shown = 0;
            for (int m = 1; m <= 100; m++)
            {
                foreach (var p in StoryletSelector.Pick(all, Ctx(month: m), last, seen, rng, new[] { 0, 0, 1 }))
                {
                    last[p.Id] = m;
                    if (p.Once) seen.Add(p.Id);
                    if (p.Id == "once_a") shown++;
                }
            }
            Assert.AreEqual(1, shown);
        }

        [Test]
        public void Cooldown_respected()
        {
            var all = new List<StoryletDef> { S("cd", cooldown: 3) };
            var last = new Dictionary<string, int> { ["cd"] = 1 };
            var seen = new HashSet<string>();
            Assert.AreEqual(0, StoryletSelector.Pick(all, Ctx(month: 2), last, seen, new Pcg32(1, 1), AlwaysOne).Count);
            Assert.AreEqual(0, StoryletSelector.Pick(all, Ctx(month: 3), last, seen, new Pcg32(1, 1), AlwaysOne).Count);
            Assert.AreEqual(1, StoryletSelector.Pick(all, Ctx(month: 4), last, seen, new Pcg32(1, 1), AlwaysOne).Count);
        }

        [Test]
        public void Never_picks_the_same_storylet_twice_in_a_month()
        {
            var all = new List<StoryletDef> { S("only") };
            var picks = StoryletSelector.Pick(all, Ctx(), new Dictionary<string, int>(), new HashSet<string>(), new Pcg32(1, 1), new[] { 0, 0, 1 });
            Assert.AreEqual(1, picks.Count);
        }

        [Test]
        public void Same_seed_same_picks()
        {
            var all = Enumerable.Range(0, 8).Select(i => S("s" + i, weight: i + 1)).ToList();
            string Run()
            {
                var rng = new Pcg32(7, 1);
                var last = new Dictionary<string, int>();
                var ids = new List<string>();
                for (int m = 1; m <= 24; m++)
                    foreach (var p in StoryletSelector.Pick(all, Ctx(month: m), last, new HashSet<string>(), rng, new[] { 1, 2, 1 }))
                    { last[p.Id] = m; ids.Add(m + ":" + p.Id); }
                return string.Join(",", ids);
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void StudyXp_effect_parses_and_adds_study_xp()
        {
            var e = Effect.Parse(JObject.Parse("{\"studyXp\": 10}"));
            var s = new GameState { StudyXp = 5 };
            EffectApplier.Apply(e, s);
            Assert.AreEqual(15, s.StudyXp);
        }
    
        // ---- Stage 2A Task 5: demand modifiers ----

        static (UnpaidCeo.Core.Game.GameEngine e, UnpaidCeo.Core.Game.GameState s) DemandLimitedShop()
        {
            var industries = UnpaidCeo.Core.Tests.Game.Stage2Content.Industries.Replace(
                "\"baseDemand\": 1000000, \"elasticity\": 1.0, \"marketingA\": 0.0, \"marketingK\": 10000, \"capacityPerUnit\": 100",
                "\"baseDemand\": 50, \"elasticity\": 1.0, \"marketingA\": 0.0, \"marketingK\": 10000, \"capacityPerUnit\": 100");
            var db = ContentDb.FromJson(new ContentFiles { Industries = industries, Balance = UnpaidCeo.Core.Tests.Game.Stage2Content.Balance(true) });
            var e = new UnpaidCeo.Core.Game.GameEngine(db);
            var s = e.NewGame(7, new UnpaidCeo.Core.Game.HeroAppearance());
            s.Chapter = 2;
            s.Life.Reputation = 50; // reputation factor 0.8 + 0.004 × 50 = 1.0
            s.Businesses.RemoveAll(b => b.IndustryId == "gig_test"); // only the shop earns, so revenue counts its sales
            s.Cash = UnpaidCeo.Core.Primitives.Money.FromCents(1_000_000);
            s.Businesses.Add(new UnpaidCeo.Core.Economy.BusinessState { Id = "shop", IndustryId = "shop", Price = db.Industries["shop"].Price, Units = 1 });
            return (e, s);
        }

        static int ShopUnits(UnpaidCeo.Core.Game.GameEngine e, UnpaidCeo.Core.Game.GameState s)
        {
            var r = e.EndMonth(s, UnpaidCeo.Core.Tests.Game.Stage2Content.Plan());
            return (int)(r.Revenue.Cents / 1000); // the shop sells at $10

        }

        static Effect Demand(double factor, int months, string industry = "current") =>
            Effect.Parse(Newtonsoft.Json.Linq.JObject.Parse($"{{ \"demand\": {{ \"factor\": {factor.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"months\": {months}, \"industry\": \"{industry}\" }} }}"));

        [Test]
        public void Demand_effect_scales_demand_for_its_months()
        {
            var (e, s) = DemandLimitedShop();
            Assert.AreEqual(50, ShopUnits(e, s));
            EffectApplier.Apply(Demand(0.8, 2), s);
            Assert.AreEqual(40, ShopUnits(e, s));
            Assert.AreEqual(40, ShopUnits(e, s));
            Assert.AreEqual(50, ShopUnits(e, s));
        }

        [Test]
        public void Modifiers_expire()
        {
            var (e, s) = DemandLimitedShop();
            EffectApplier.Apply(Demand(0.8, 3), s);
            Assert.AreEqual(1, s.Modifiers.Count);
            for (int i = 0; i < 3; i++) ShopUnits(e, s);
            Assert.IsEmpty(s.Modifiers);
        }

        [Test]
        public void Two_modifiers_multiply()
        {
            var (e, s) = DemandLimitedShop();
            EffectApplier.Apply(Demand(0.8, 1), s);
            EffectApplier.Apply(Demand(0.5, 1, "shop"), s);
            Assert.AreEqual(20, ShopUnits(e, s));
        }

        [Test]
        public void Unknown_industry_in_demand_effect_fails_at_load()
        {
            const string storylets = @"{ ""storylets"": [ { ""id"": ""war"", ""yarnNode"": ""war"",
  ""choices"": { ""ignore"": [ { ""demand"": { ""factor"": 0.8, ""months"": 3, ""industry"": ""nope"" } } ] } } ] }";
            var db = ContentDb.FromJson(new ContentFiles { Industries = UnpaidCeo.Core.Tests.Game.Stage2Content.Industries,
                Balance = UnpaidCeo.Core.Tests.Game.Stage2Content.Balance(), Storylets = storylets });
            Assert.That(db.Validate().Errors, Has.Some.Contains("nope"));
        }
    }
}
