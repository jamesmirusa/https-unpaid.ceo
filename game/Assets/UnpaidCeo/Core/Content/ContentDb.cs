using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Content
{
    /// <summary>Raw JSON text of each content file; a null entry means the file is absent (empty list).</summary>
    public sealed class ContentFiles
    {
        public string Industries { get; set; }
        public string Balance { get; set; }
        public string FreeActions { get; set; }
        public string Storylets { get; set; }
        public string Npcs { get; set; }
        public string Lessons { get; set; }
    }

    /// <summary>A named character; Name maps a language code to the display name.</summary>
    public sealed class NpcDef
    {
        public string Id { get; set; }
        public string Species { get; set; }
        public string Role { get; set; }
        public Dictionary<string, string> Name { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>A player action available in any month.</summary>
    public sealed class FreeActionDef
    {
        public string Id { get; set; }
        public int ActionPoints { get; set; }
        public Money Cost { get; set; }
        public List<Effect> Effects { get; set; } = new List<Effect>();
        public string JournalKey { get; set; }
    }

    public sealed class ContentDb
    {
        public IReadOnlyDictionary<string, IndustryDef> Industries { get; private set; }
        public Balance Balance { get; private set; }
        public IReadOnlyDictionary<string, FreeActionDef> FreeActions { get; private set; }
        public IReadOnlyList<StoryletDef> Storylets { get; private set; }
        public IReadOnlyDictionary<string, NpcDef> Npcs { get; private set; }
        public IReadOnlyDictionary<string, LessonDef> Lessons { get; private set; }
        /// <summary>Lessons in file order: the order they unlock in when several trigger in one month.</summary>
        public IReadOnlyList<LessonDef> LessonsInOrder => LessonList;

        internal List<IndustryDef> IndustryList { get; private set; }
        internal List<FreeActionDef> FreeActionList { get; private set; }
        internal List<NpcDef> NpcList { get; private set; }
        internal List<LessonDef> LessonList { get; private set; }

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        class IndustriesFile { public List<IndustryDef> Industries { get; set; } = new List<IndustryDef>(); }
        class FreeActionsFile { public List<FreeActionDef> FreeActions { get; set; } = new List<FreeActionDef>(); }
        class StoryletsFile { public List<StoryletDef> Storylets { get; set; } = new List<StoryletDef>(); }
        class NpcsFile { public List<NpcDef> Npcs { get; set; } = new List<NpcDef>(); }
        class LessonsFile { public List<LessonDef> Lessons { get; set; } = new List<LessonDef>(); }

        public static ContentDb FromJson(ContentFiles files)
        {
            var industries = Parse<IndustriesFile>(files.Industries)?.Industries ?? new List<IndustryDef>();
            var actions = Parse<FreeActionsFile>(files.FreeActions)?.FreeActions ?? new List<FreeActionDef>();
            var storylets = Parse<StoryletsFile>(files.Storylets)?.Storylets ?? new List<StoryletDef>();
            var npcs = Parse<NpcsFile>(files.Npcs)?.Npcs ?? new List<NpcDef>();
            var lessons = Parse<LessonsFile>(files.Lessons)?.Lessons ?? new List<LessonDef>();
            return new ContentDb
            {
                IndustryList = industries,
                FreeActionList = actions,
                Industries = ToDict(industries, i => i.Id),
                FreeActions = ToDict(actions, a => a.Id),
                Storylets = storylets,
                NpcList = npcs,
                Npcs = ToDict(npcs, n => n.Id),
                LessonList = lessons,
                Lessons = ToDict(lessons, l => l.Id),
                Balance = Parse<Balance>(files.Balance) ?? new Balance(),
            };
        }

        public ValidationResult Validate() => ContentValidator.Validate(this);

        static T Parse<T>(string json) where T : class =>
            string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<T>(json, Settings);

        // Duplicate ids keep the first entry; the validator reports the duplicate.
        static IReadOnlyDictionary<string, T> ToDict<T>(IEnumerable<T> items, System.Func<T, string> key)
        {
            var d = new Dictionary<string, T>();
            foreach (var it in items)
            {
                var k = key(it) ?? "";
                if (!d.ContainsKey(k)) d.Add(k, it);
            }
            return d;
        }
    }
}
