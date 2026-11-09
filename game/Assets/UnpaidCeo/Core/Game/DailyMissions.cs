using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Game
{
    /// <summary>A daily mission (missions.json): do something Target times today. Rewards founder-skill XP and CEO points, never cash.</summary>
    public sealed class MissionDef
    {
        /// <summary>Free actions taken in manual months (counted from the month's decisions).</summary>
        public const string FreeActionKind = "freeAction";
        /// <summary>What a mission can count: manual months, scene choices, quiz answers, free actions.</summary>
        public static readonly IReadOnlyList<string> Kinds = new[]
            { DecisionLogEntry.EndMonth, DecisionLogEntry.StoryletChoice, DecisionLogEntry.QuizAnswer, FreeActionKind };

        public string Id { get; set; }
        public string Kind { get; set; }
        public int Target { get; set; } = 1;
        public string Skill { get; set; }
        public int Xp { get; set; }
        public int CeoPoints { get; set; }
        public string TitleKey { get; set; }
    }

    /// <summary>Progress outside the replayed game: today's missions, CEO points (future cosmetics) and mission XP.</summary>
    public sealed class MetaProgress
    {
        public int MissionDay { get; set; }
        public Dictionary<string, int> MissionProgress { get; set; } = new Dictionary<string, int>();
        public List<string> MissionsDone { get; set; } = new List<string>();
        /// <summary>Quiz questions ("lesson/question") already counted today: practice counts once per question a day.</summary>
        public List<string> QuizKeysToday { get; set; } = new List<string>();
        public int CeoPoints { get; set; }
        /// <summary>Founder-skill XP from missions (shown with the quiz XP in GameState.Skills).</summary>
        public Dictionary<string, int> SkillXp { get; set; } = new Dictionary<string, int>();
        /// <summary>How many GameState.Log entries were already counted, for the game with <see cref="LogSeed"/>.</summary>
        public int LogCursor { get; set; }
        public ulong LogSeed { get; set; }
        /// <summary>Achievement badges earned (Core.Online.Achievements).</summary>
        public List<string> Badges { get; set; } = new List<string>();
        /// <summary>Month ends in a row with cash ≥ 0 since the first unit opened.</summary>
        public int CleanCashMonths { get; set; }
    }

    /// <summary>Turn budget, streak and mission progress, saved next to the game save.</summary>
    public sealed class PlayerMeta
    {
        public int Version { get; set; } = 1;
        public TurnState Turns { get; set; } = new TurnState();
        public MetaProgress Meta { get; set; } = new MetaProgress();
    }

    public static class DailyMissions
    {
        public const int PerDay = 3;

        /// <summary>Today's three missions: the same for the same player and day on every device.</summary>
        public static IReadOnlyList<MissionDef> For(int day, ulong seed, ContentDb c)
        {
            var pool = c.MissionList;
            var picked = new List<MissionDef>();
            if (pool == null || pool.Count == 0) return picked;
            var rng = new Pcg32(seed ^ 0x9E3779B97F4A7C15UL, unchecked((ulong)(uint)day));
            var left = Enumerable.Range(0, pool.Count).ToList();
            while (picked.Count < PerDay && left.Count > 0)
            {
                int i = rng.NextInt(left.Count);
                picked.Add(pool[left[i]]);
                left.RemoveAt(i);
            }
            return picked;
        }

        /// <summary>Counts <paramref name="count"/> actions of a kind toward today's missions; returns the missions this completes.</summary>
        public static IReadOnlyList<MissionDef> Record(MetaProgress m, TurnState t, ulong seed, ContentDb c, string kind, int count)
        {
            Roll(m, t);
            var newly = new List<MissionDef>();
            if (count <= 0) return newly;
            var today = For(t.Day, seed, c);
            foreach (var md in today)
            {
                if (md.Kind != kind || m.MissionsDone.Contains(md.Id)) continue;
                int target = Math.Max(1, md.Target);
                int p = Math.Min(target, (m.MissionProgress.TryGetValue(md.Id, out var v) ? v : 0) + count);
                m.MissionProgress[md.Id] = p;
                if (p < target) continue;
                m.MissionsDone.Add(md.Id);
                m.CeoPoints += md.CeoPoints;
                if (!string.IsNullOrEmpty(md.Skill)) m.SkillXp[md.Skill] = (m.SkillXp.TryGetValue(md.Skill, out var xp) ? xp : 0) + md.Xp;
                newly.Add(md);
            }
            if (newly.Count > 0 && today.All(x => m.MissionsDone.Contains(x.Id))) TurnBudget.CompleteDay(t);
            return newly;
        }

        /// <summary>Counts the game's log entries added since the last sync (manual play only: skip autopilot with <see cref="SkipLog"/>).</summary>
        public static IReadOnlyList<MissionDef> Sync(MetaProgress m, TurnState t, GameState s, ContentDb c)
        {
            if (m.LogSeed != s.Seed) { m.LogSeed = s.Seed; m.LogCursor = 0; }
            if (m.LogCursor > s.Log.Count) m.LogCursor = s.Log.Count;
            var counts = new Dictionary<string, int>();
            void Add(string k, int n) => counts[k] = (counts.TryGetValue(k, out var v) ? v : 0) + n;
            for (int i = m.LogCursor; i < s.Log.Count; i++)
            {
                var e = s.Log[i];
                // Quiz answers come from RecordQuiz (practice counts too), not from the log.
                if (e.Kind != DecisionLogEntry.QuizAnswer) Add(e.Kind, 1);
                if (e.Kind == DecisionLogEntry.EndMonth && e.Payload != null)
                    Add(MissionDef.FreeActionKind, (JObject.Parse(e.Payload)["FreeActions"] as JArray)?.Count ?? 0);
            }
            m.LogCursor = s.Log.Count;
            var done = new List<MissionDef>();
            foreach (var kind in MissionDef.Kinds)
                if (counts.TryGetValue(kind, out var n)) done.AddRange(Record(m, t, s.Seed, c, kind, n));
            return done;
        }

        /// <summary>A quiz answer in the Academy, first try or practice: each question counts once a day.</summary>
        public static IReadOnlyList<MissionDef> RecordQuiz(MetaProgress m, TurnState t, ulong seed, ContentDb c, string lessonId, int question)
        {
            Roll(m, t);
            var key = lessonId + "/" + question;
            if (m.QuizKeysToday.Contains(key)) return Array.Empty<MissionDef>();
            m.QuizKeysToday.Add(key);
            return Record(m, t, seed, c, DecisionLogEntry.QuizAnswer, 1);
        }

        /// <summary>Marks the log as counted without crediting it (autopilot months are not the player's).</summary>
        public static void SkipLog(MetaProgress m, GameState s)
        {
            m.LogSeed = s.Seed;
            m.LogCursor = s.Log.Count;
        }

        public static int Progress(MetaProgress m, TurnState t, string missionId) =>
            m.MissionDay == t.Day && m.MissionProgress.TryGetValue(missionId, out var p) ? p : 0;

        public static bool IsDone(MetaProgress m, TurnState t, string missionId) =>
            m.MissionDay == t.Day && m.MissionsDone.Contains(missionId);

        static void Roll(MetaProgress m, TurnState t)
        {
            if (m.MissionDay == t.Day) return;
            m.MissionDay = t.Day;
            m.MissionProgress.Clear();
            m.MissionsDone.Clear();
            m.QuizKeysToday.Clear();
        }
    }
}
