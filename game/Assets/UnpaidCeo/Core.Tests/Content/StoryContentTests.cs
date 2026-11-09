using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Content
{
    /// <summary>Story content (chapters 1–3): storylets JSON, Yarn scripts and the RU string table must agree.</summary>
    public class StoryContentTests
    {
        static string Root => Path.Combine(UnityEngine.Application.dataPath, "UnpaidCeo");
        static string ContentDir => Path.Combine(Root, "Content");
        static string YarnDir => Path.Combine(Root, "Yarn");

        static ContentDb Db()
        {
            string Read(string f) { var p = Path.Combine(ContentDir, f); return File.Exists(p) ? File.ReadAllText(p) : null; }
            return ContentDb.FromJson(new ContentFiles
            {
                Industries = Read("industries.json"),
                Balance = Read("balance.json"),
                FreeActions = Read("free_actions.json"),
                Storylets = Read("storylets_ch1.json"),
                MoreStorylets = new[] { Read("storylets_ch2.json"), Read("storylets_ch3.json") },
                Npcs = Read("npcs.json"),
                Lessons = Read("lessons.json"),
            });
        }

        sealed class YarnNode
        {
            public string Title;
            public readonly HashSet<string> Chooses = new HashSet<string>();
            public readonly List<string> ContentLines = new List<string>();
        }

        static readonly Regex Choose = new Regex("<<choose\\s+\"([^\"]+)\"\\s*>>");
        static readonly Regex LineTag = new Regex("#line:(\\S+)");

        static Dictionary<string, YarnNode> YarnNodes()
        {
            var nodes = new Dictionary<string, YarnNode>();
            Assert.IsTrue(Directory.Exists(YarnDir), "Yarn folder is missing");
            foreach (var file in Directory.GetFiles(YarnDir, "*.yarn", SearchOption.AllDirectories))
            {
                YarnNode cur = null;
                bool body = false;
                foreach (var raw in File.ReadAllLines(file))
                {
                    var t = raw.Trim();
                    if (!body)
                    {
                        if (t.StartsWith("title:")) { cur = new YarnNode { Title = t.Substring(6).Trim() }; nodes[cur.Title] = cur; }
                        else if (t == "---") body = true;
                        continue;
                    }
                    if (t == "===") { body = false; cur = null; continue; }
                    foreach (Match m in Choose.Matches(t)) cur.Chooses.Add(m.Groups[1].Value);
                    if (t.Length == 0 || t.StartsWith("<<") || t.StartsWith("//")) continue;
                    cur.ContentLines.Add(t);
                }
            }
            return nodes;
        }

        [Test]
        public void Every_storylet_has_yarn_node()
        {
            var nodes = YarnNodes();
            var db = Db();
            Assert.IsTrue(db.Storylets.Count >= 36, $"chapter 1 needs ~40 storylets, has {db.Storylets.Count}");
            var missing = db.Storylets.Where(s => !nodes.ContainsKey(s.YarnNode)).Select(s => s.Id + " → " + s.YarnNode).ToList();
            Assert.IsEmpty(missing, "storylets without a Yarn node:\n" + string.Join("\n", missing));
        }

        [Test]
        public void Yarn_choose_ids_match_json_choices()
        {
            var nodes = YarnNodes();
            var bad = new List<string>();
            foreach (var s in Db().Storylets)
            {
                if (!nodes.TryGetValue(s.YarnNode, out var n)) continue;
                var json = new HashSet<string>(s.Choices.Keys);
                if (!json.SetEquals(n.Chooses))
                    bad.Add($"{s.Id}: json [{string.Join(",", json.OrderBy(x => x))}] vs yarn [{string.Join(",", n.Chooses.OrderBy(x => x))}]");
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void All_effects_and_conditions_parse()
        {
            var db = Db(); // parsing throws FormatException on any unknown effect, condition, stat or operator
            var result = db.Validate();
            Assert.IsTrue(result.IsValid, string.Join("\n", result.Errors));
            Assert.IsTrue(db.FreeActions.Count >= 9, "chapter 1 free actions are missing");
            foreach (var arc in new[] { "tony", "loan_shark", "best_friend" })
                Assert.IsTrue(db.Storylets.Count(s => s.Arc == arc) >= 4, $"arc '{arc}' needs at least 4 scenes");
        }

        [Test]
        public void Npc_ids_exist()
        {
            var db = Db();
            Assert.AreEqual(10, db.Npcs.Count, "6 chapter-1 NPCs and 4 dialogue-only NPCs from chapters 2–3");
            foreach (var id in new[] { "banker_lee", "supplier_rosa", "journalist_mia", "cofounder_sam" })
                Assert.IsTrue(db.Npcs.ContainsKey(id), id);
            var used = new List<(string where, string npc)>();
            foreach (var s in db.Storylets)
            {
                foreach (var w in s.When)
                    if (w.Raw["npc"] != null) used.Add((s.Id, (string)w.Raw["npc"]));
                foreach (var c in s.Choices)
                    foreach (var e in c.Value.OfType<NpcEffect>()) used.Add((s.Id + "/" + c.Key, e.Npc));
            }
            foreach (var a in db.FreeActions.Values)
                foreach (var e in a.Effects.OfType<NpcEffect>()) used.Add(("freeAction " + a.Id, e.Npc));
            var unknown = used.Where(u => !db.Npcs.ContainsKey(u.npc)).Select(u => u.where + ": " + u.npc).ToList();
            Assert.IsEmpty(unknown, string.Join("\n", unknown));
        }

        [Test]
        public void Every_line_has_ru_translation()
        {
            var nodes = YarnNodes();
            var untagged = nodes.Values.SelectMany(n => n.ContentLines.Where(l => !LineTag.IsMatch(l)).Select(l => n.Title + ": " + l)).ToList();
            Assert.IsEmpty(untagged, "every Yarn line and option needs an explicit #line: id:\n" + string.Join("\n", untagged));

            var ids = nodes.Values.SelectMany(n => n.ContentLines).Select(l => LineTag.Match(l).Groups[1].Value).ToList();
            var dupes = ids.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.IsEmpty(dupes, "duplicate line ids: " + string.Join(", ", dupes));

            var csvPath = Path.Combine(YarnDir, "Story-ru.csv");
            Assert.IsTrue(File.Exists(csvPath), "Story-ru.csv is missing");
            var ru = ReadStringTable(File.ReadAllText(csvPath, Encoding.UTF8));
            var missing = ids.Where(id => !ru.TryGetValue("line:" + id, out var text) || string.IsNullOrWhiteSpace(text)).ToList();
            Assert.IsEmpty(missing, "lines without a Russian translation:\n" + string.Join("\n", missing));
        }

        static readonly string[] Chapter2Industries = { "coffee_cart", "resale_store", "food_truck", "ai_agency", "cleaning", "logistics_box_truck" };

        static string IndustryOf(StoryletDef s) => s.When.Select(w => (string)w.Raw["industry"]).FirstOrDefault(x => x != null);

        [Test]
        public void Every_industry_has_at_least_six_scenes()
        {
            var db = Db();
            foreach (var id in Chapter2Industries)
                Assert.GreaterOrEqual(db.Storylets.Count(s => IndustryOf(s) == id), 6, id);
            Assert.GreaterOrEqual(db.Storylets.Count(s => s.Id.StartsWith("ch2_") || s.Id.StartsWith("ch3_") || s.Id.StartsWith("boss_")), 48);
            Assert.GreaterOrEqual(db.Storylets.Count(s => s.Id.StartsWith("ch2_") && IndustryOf(s) == null), 6, "generic Brooklyn scenes");
            Assert.GreaterOrEqual(db.Storylets.Count(s => s.Id.StartsWith("ch3_")), 6, "generic Queens scenes");
            foreach (var arc in new[] { "banker", "supplier", "journalist", "cofounder" })
                Assert.GreaterOrEqual(db.Storylets.Count(s => s.Arc == arc), 2, $"arc '{arc}' starts in Brooklyn and continues in Queens");
        }

        [Test]
        public void Every_industry_has_a_scene_that_teaches()
        {
            var db = Db();
            foreach (var id in Chapter2Industries)
            {
                var lessons = db.Storylets.Where(s => IndustryOf(s) == id)
                    .SelectMany(s => s.Choices.Values.SelectMany(c => c.OfType<LessonEffect>())).Select(e => e.LessonId).ToList();
                Assert.IsNotEmpty(lessons, $"{id} has no scene that leads to a lesson");
                Assert.IsTrue(lessons.All(l => db.Lessons.ContainsKey(l)), $"{id}: unknown lesson in [{string.Join(",", lessons)}]");
            }
        }

        [Test]
        public void Every_new_choice_has_a_visible_effect()
        {
            var bad = (from s in Db().Storylets
                       where !s.Id.StartsWith("ch1_")
                       from c in s.Choices
                       where !c.Value.Any(e => e is CashEffect || e is StatEffect || e is NpcEffect || e is DemandModifierEffect
                                              || e is StaffEffect || e is ReserveEffect || e is LessonEffect)
                       select s.Id + "/" + c.Key).ToList();
            Assert.IsEmpty(bad, "choices the player cannot feel:\n" + string.Join("\n", bad));
        }

        [Test]
        public void Chapter_end_rival_scene_fires_once_when_the_goal_is_met()
        {
            var db = Db();
            foreach (var (id, chapter, fact) in new[] { ("boss_fatcat_price_war", 2, "chapter2_done"), ("boss_fatcat_poach", 3, "chapter3_done") })
            {
                var s = db.Storylets.Single(x => x.Id == id);
                Assert.IsTrue(s.Once, id);
                Assert.GreaterOrEqual(s.Weight, 50, $"{id} should come up right away");
                var facts = new FactStore();
                NarrativeContext Ctx() => new NarrativeContext
                {
                    Month = 30, Chapter = chapter, Cash = Money.FromCents(1_000_000),
                    Life = new LifeStats { Energy = 70, Relationships = 60, Reputation = 50, Honesty = 70, Connections = 20 },
                    Facts = facts, Npcs = new NpcRelations(), IndustryIds = new[] { "coffee_cart" },
                };
                var none = new Dictionary<string, int>();
                Assert.IsFalse(StoryletSelector.IsEligible(s, Ctx(), none, new HashSet<string>()), $"{id} before the goal");
                facts.Set(fact, 29);
                Assert.IsTrue(StoryletSelector.IsEligible(s, Ctx(), none, new HashSet<string>()), $"{id} once the goal is met");
                Assert.IsFalse(StoryletSelector.IsEligible(s, Ctx(), none, new HashSet<string> { id }), $"{id} only once");
            }
            var war = db.Storylets.Single(x => x.Id == "boss_fatcat_price_war").Choices["ignore"].OfType<DemandModifierEffect>().Single();
            Assert.AreEqual(0.8, war.Factor, 1e-9);
            Assert.AreEqual(3, war.Months);
            var poach = db.Storylets.Single(x => x.Id == "boss_fatcat_poach").Choices["refuse"].OfType<StaffEffect>().Single();
            Assert.AreEqual(-2, poach.Delta);
        }

        [Test]
        public void Scene_choice_lines_resolve()
        {
            var lineIds = new HashSet<string>(YarnNodes().Values.SelectMany(n => n.ContentLines)
                .Select(l => LineTag.Match(l)).Where(m => m.Success).Select(m => "line:" + m.Groups[1].Value));
            var bad = new List<string>();
            foreach (var s in Db().Storylets)
            {
                if (s.ChoiceLines.Count == 0) continue;   // the journal falls back to the choice id
                if (!new HashSet<string>(s.ChoiceLines.Keys).SetEquals(s.Choices.Keys)) bad.Add(s.Id + ": choiceLines keys differ from choices");
                bad.AddRange(s.ChoiceLines.Where(kv => !lineIds.Contains(kv.Value)).Select(kv => $"{s.Id}/{kv.Key}: {kv.Value} is not a Yarn line"));
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void Bankrupt_comeback_reachable()
        {
            var facts = new FactStore();
            facts.Set("met_mentor", 1);
            facts.Set("in_debt", 3);
            facts.Set("bankrupt", 5);
            var ctx = new NarrativeContext
            {
                Month = 6, Chapter = 1, Cash = Money.FromCents(-60_000),
                Life = new LifeStats { Energy = 40, Relationships = 40, Reputation = 40, Honesty = 60, Connections = 20 },
                Facts = facts, Npcs = new NpcRelations(), IndustryIds = new[] { "gig_delivery" },
            };
            var comeback = Db().Storylets.Where(s =>
                s.When.Any(w => (string)w.Raw["fact"] == "bankrupt") &&
                StoryletSelector.IsEligible(s, ctx, new Dictionary<string, int>(), new HashSet<string>())).ToList();
            Assert.IsNotEmpty(comeback, "a bankrupt player must be offered a comeback scene");
            Assert.IsTrue(comeback.Any(s => s.Choices.Values.All(effects => effects.OfType<ClearFactEffect>().Any(e => e.FactId == "bankrupt"))),
                "every choice of the comeback scene must clear the bankrupt fact");
        }

        /// <summary>Minimal RFC 4180 reader for Yarn Spinner string tables: returns id → text.</summary>
        static Dictionary<string, string> ReadStringTable(string csv)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else field.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                    row.Add(field.ToString()); field.Clear();
                    if (row.Count > 1 || row[0].Length > 0) rows.Add(row);
                    row = new List<string>();
                }
                else field.Append(c);
            }
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }

            var header = rows[0];
            int idCol = header.IndexOf("id"), textCol = header.IndexOf("text");
            Assert.IsTrue(idCol >= 0 && textCol >= 0, "string table needs id and text columns");
            var d = new Dictionary<string, string>();
            foreach (var r in rows.Skip(1)) d[r[idCol]] = r[textCol];
            return d;
        }
    }
}
