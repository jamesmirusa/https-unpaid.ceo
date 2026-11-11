using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Core.Persistence
{
    public enum LoadStatus { Ok, Migrated, CorruptReset }

    public sealed class LoadResult
    {
        public GameState State { get; set; }
        public LoadStatus Status { get; set; }
        /// <summary>The original text when the save could not be loaded, kept so nothing is lost.</summary>
        public string Backup { get; set; }
    }

    /// <summary>Versioned JSON saves. Never throws on bad input: unreadable saves come back as CorruptReset with a backup.</summary>
    public static class SaveCodec
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        // Fields added after version 1 shipped (or optional ones): their absence means an older save to migrate.
        static readonly string[] OptionalFields =
        {
            nameof(GameState.Journal), nameof(GameState.Log), nameof(GameState.StoryletsSeen),
            nameof(GameState.PendingStorylets), nameof(GameState.PendingLessons), nameof(GameState.LessonsSeen),
            nameof(GameState.LastDecisions), nameof(GameState.Skills), nameof(GameState.QuizAnswers),
            nameof(GameState.Loans), nameof(GameState.Modifiers),
        };

        public static string Encode(GameState s) => JsonConvert.SerializeObject(s, Formatting.None, Settings);

        public static LoadResult Decode(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return Corrupt(json);
                if (!(JToken.Parse(json) is JObject obj)) return Corrupt(json);

                var version = obj[nameof(GameState.Version)];
                if (version == null || version.Type != JTokenType.Integer) return Corrupt(json);
                int v = version.Value<int>();
                if (v < 1 || v > GameState.CurrentVersion) return Corrupt(json);

                // Version 1 (stage 1) saves gain loans, demand modifiers and staff, all empty by default.
                bool migrated = v < GameState.CurrentVersion;
                foreach (var f in OptionalFields)
                    if (obj[f] == null || obj[f].Type == JTokenType.Null) migrated = true;

                var state = obj.ToObject<GameState>(JsonSerializer.Create(Settings));
                if (state == null || state.Month < 1 || state.Life == null || state.Hero == null) return Corrupt(json);
                FillNulls(state);
                if (v < 3 || state.Week < 1) MigrateToWeeks(state);
                state.Version = GameState.CurrentVersion;
                return new LoadResult { State = state, Status = migrated ? LoadStatus.Migrated : LoadStatus.Ok };
            }
            catch (Exception e) when (e is JsonException || e is FormatException || e is InvalidCastException || e is ArgumentException || e is OverflowException)
            {
                return Corrupt(json);
            }
        }

        /// <summary>
        /// Version 3 (stage 4B): the turn is a week. An old save opens at the first week of its month; its monthly
        /// receivables are split into four weekly ones; the action-point pool refills on that first week.
        /// </summary>
        static void MigrateToWeeks(GameState s)
        {
            s.Week = GameState.FirstWeekOf(s.Month);
            s.ActionsThisMonth = s.ActionsThisMonth ?? new System.Collections.Generic.List<string>();
            s.ActionsThisMonth.Clear();
            s.ActionPointsLeft = 0;
            foreach (var b in s.Businesses)
            {
                if (b.Receivables == null || b.Receivables.Count == 0) continue;
                var weekly = new System.Collections.Generic.List<UnpaidCeo.Core.Primitives.Money>();
                foreach (var m in b.Receivables)
                {
                    long part = m.Cents / GameState.WeeksPerMonth;
                    for (int w = 1; w < GameState.WeeksPerMonth; w++) weekly.Add(UnpaidCeo.Core.Primitives.Money.FromCents(part));
                    weekly.Add(UnpaidCeo.Core.Primitives.Money.FromCents(m.Cents - part * (GameState.WeeksPerMonth - 1)));
                }
                b.Receivables = weekly;
            }
        }

        static void FillNulls(GameState s)
        {
            s.Journal = s.Journal ?? new System.Collections.Generic.List<JournalEntry>();
            s.Log = s.Log ?? new System.Collections.Generic.List<DecisionLogEntry>();
            s.StoryletsSeen = s.StoryletsSeen ?? new System.Collections.Generic.Dictionary<string, int>();
            s.StoryletLastMonth = s.StoryletLastMonth ?? new System.Collections.Generic.Dictionary<string, int>();
            s.PendingStorylets = s.PendingStorylets ?? new System.Collections.Generic.List<string>();
            s.PendingLessons = s.PendingLessons ?? new System.Collections.Generic.List<string>();
            s.LessonsSeen = s.LessonsSeen ?? new System.Collections.Generic.Dictionary<string, int>();
            s.Skills = s.Skills ?? new System.Collections.Generic.Dictionary<string, int>();
            s.ActionsThisMonth = s.ActionsThisMonth ?? new System.Collections.Generic.List<string>();
            s.QuizAnswers = s.QuizAnswers ?? new System.Collections.Generic.Dictionary<string, int>();
            s.Businesses = s.Businesses ?? new System.Collections.Generic.List<Economy.BusinessState>();
            s.Loans = s.Loans ?? new System.Collections.Generic.List<Economy.LoanState>();
            s.Modifiers = s.Modifiers ?? new System.Collections.Generic.List<Economy.DemandModifier>();
            s.Loans.RemoveAll(l => l == null);
            s.Modifiers.RemoveAll(m => m == null);
            s.Facts = s.Facts ?? new Narrative.FactStore();
            s.Npcs = s.Npcs ?? new Narrative.NpcRelations();
            // Repair nested state that would otherwise throw in the month loop on every launch.
            s.Facts.Items = s.Facts.Items ?? new System.Collections.Generic.Dictionary<string, int>();
            s.Npcs.Values = s.Npcs.Values ?? new System.Collections.Generic.Dictionary<string, int>();
            s.Businesses.RemoveAll(b => b == null || string.IsNullOrEmpty(b.Id) || string.IsNullOrEmpty(b.IndustryId));
            if (s.LastDecisions != null)
            {
                s.LastDecisions.WorkSplit = s.LastDecisions.WorkSplit ?? new System.Collections.Generic.Dictionary<string, int>();
                s.LastDecisions.Levers = s.LastDecisions.Levers ?? new System.Collections.Generic.List<LeverChange>();
                s.LastDecisions.FreeActions = s.LastDecisions.FreeActions ?? new System.Collections.Generic.List<string>();
            }
            if (s.LastDecisions == null || !s.LastDecisions.TryGetAllocation(out _))
                s.LastDecisions = MonthDecisions.Default(s.Businesses.Count > 0 ? s.Businesses[0].Id : null);
        }

        public static string EncodeMeta(PlayerMeta m) => JsonConvert.SerializeObject(m, Formatting.None, Settings);

        /// <summary>Turn budget, streak and missions; unreadable text gives a fresh record (never throws).</summary>
        public static PlayerMeta DecodeMeta(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return new PlayerMeta();
                var m = JsonConvert.DeserializeObject<PlayerMeta>(json, Settings) ?? new PlayerMeta();
                m.Turns = m.Turns ?? new TurnState();
                m.Meta = m.Meta ?? new MetaProgress();
                m.Meta.MissionProgress = m.Meta.MissionProgress ?? new System.Collections.Generic.Dictionary<string, int>();
                m.Meta.MissionsDone = m.Meta.MissionsDone ?? new System.Collections.Generic.List<string>();
                m.Meta.SkillXp = m.Meta.SkillXp ?? new System.Collections.Generic.Dictionary<string, int>();
                m.Meta.QuizKeysToday = m.Meta.QuizKeysToday ?? new System.Collections.Generic.List<string>();
                return m;
            }
            catch (Exception e) when (e is JsonException || e is FormatException || e is InvalidCastException || e is ArgumentException || e is OverflowException)
            {
                return new PlayerMeta();
            }
        }

        static LoadResult Corrupt(string json) => new LoadResult { Status = LoadStatus.CorruptReset, Backup = json };
    }
}
