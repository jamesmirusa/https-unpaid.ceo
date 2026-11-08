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
    /// <summary>Chapter 1 content: storylets JSON, Yarn scripts and the RU string table must agree.</summary>
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
                Npcs = Read("npcs.json"),
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
            Assert.AreEqual(6, db.Npcs.Count, "chapter 1 has 6 NPCs");
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

            var csvPath = Path.Combine(YarnDir, "Chapter1-ru.csv");
            Assert.IsTrue(File.Exists(csvPath), "Chapter1-ru.csv is missing");
            var ru = ReadStringTable(File.ReadAllText(csvPath, Encoding.UTF8));
            var missing = ids.Where(id => !ru.TryGetValue("line:" + id, out var text) || string.IsNullOrWhiteSpace(text)).ToList();
            Assert.IsEmpty(missing, "lines without a Russian translation:\n" + string.Join("\n", missing));
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
