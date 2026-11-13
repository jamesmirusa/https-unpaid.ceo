using System.Collections.Generic;
using System.Linq;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Core.Narrative
{
    /// <summary>Stage 5B · trust (−100…100) as players read it: hearts 0–10 and a tier for everyday lines.</summary>
    public static class Relationships
    {
        public const string Cold = "cold", Neutral = "neutral", Warm = "warm", Close = "close";

        /// <summary>Hearts: trust ÷ 10 for positive trust, 0 otherwise (negative trust reads as "cold").</summary>
        public static int Level(int trust) => trust <= 0 ? 0 : System.Math.Min(10, trust / 10);

        public static string Tier(int trust) => trust < 0 ? Cold : trust < 20 ? Neutral : trust < 50 ? Warm : Close;
    }

    /// <summary>Stage 5B · the bonuses relationship levels bring (balance.json → levelBonuses).</summary>
    public static class LevelBonuses
    {
        /// <summary>The bonus for the highest level of <paramref name="npc"/> passed when trust went from level <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static LevelBonusDef Reached(Balance b, string npc, int from, int to) =>
            b.LevelBonuses.Where(x => x.Npc == npc && x.Level > from && x.Level <= to).OrderByDescending(x => x.Level).FirstOrDefault();

        public static LevelBonusDef At(Balance b, string npc, int level) => b.LevelBonuses.FirstOrDefault(x => x.Npc == npc && x.Level == level);
    }

    /// <summary>
    /// Stage 5B · scenes that wait at their host. Which one waits is decided without dice: reactions to past decisions
    /// first, then the next step of the host's line; one story scene per host a week.
    /// </summary>
    public static class HostScenes
    {
        public static bool CanTalk(GameState s, string host) => !(s.HostTalkWeek.TryGetValue(host, out var w) && w == s.Week);

        public static StoryletDef Waiting(GameState s, ContentDb c, string host)
        {
            var ctx = GameEngine.ContextOf(s);
            var seen = s.StoryletsSeen.Keys.ToList();
            return c.Storylets
                .Where(x => x.Host == host && !s.PendingStorylets.Contains(x.Id) && StoryletSelector.IsEligible(x, ctx, s.StoryletLastMonth, seen))
                .OrderBy(x => x.Reaction ? 0 : 1).ThenBy(x => x.Step).ThenBy(x => x.Id, System.StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>A story scene waits and the host has not given one this week ("!" over them).</summary>
        public static bool HasScene(GameState s, ContentDb c, string host) => CanTalk(s, host) && Waiting(s, c, host) != null;

        /// <summary>The answer needs more knowledge than the hero has.</summary>
        public static bool Locked(StoryletDef st, string choice, GameState s) =>
            st.Requires != null && st.Requires.TryGetValue(choice, out var need) && s.StudyXp < need;

        public static IEnumerable<string> Hosts(ContentDb c) => c.Storylets.Where(x => x.Host != null).Select(x => x.Host).Distinct();
    }
}
