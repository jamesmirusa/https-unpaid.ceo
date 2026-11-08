using System;
using System.Collections.Generic;
using System.Linq;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Narrative
{
    /// <summary>Chooses this month's scenes: count from weights (0/1/2), then weighted picks without repeats.</summary>
    public static class StoryletSelector
    {
        public static IReadOnlyList<StoryletDef> Pick(
            IReadOnlyList<StoryletDef> all, NarrativeContext c,
            IReadOnlyDictionary<string, int> lastShown, IReadOnlyCollection<string> seenOnce,
            Pcg32 rng, int[] eventsPerMonthWeights)
        {
            int count = RollCount(rng, eventsPerMonthWeights);
            var eligible = all.Where(s => IsEligible(s, c, lastShown, seenOnce)).ToList();
            var picks = new List<StoryletDef>();
            for (int i = 0; i < count && eligible.Count > 0; i++)
            {
                int total = eligible.Sum(s => Math.Max(1, s.Weight));
                int r = rng.NextInt(total);
                int idx = 0;
                while (r >= Math.Max(1, eligible[idx].Weight)) { r -= Math.Max(1, eligible[idx].Weight); idx++; }
                picks.Add(eligible[idx]);
                eligible.RemoveAt(idx);
            }
            return picks;
        }

        public static bool IsEligible(StoryletDef s, NarrativeContext c,
            IReadOnlyDictionary<string, int> lastShown, IReadOnlyCollection<string> seenOnce)
        {
            if (s.Once && seenOnce.Contains(s.Id)) return false;
            if (s.CooldownMonths > 0 && lastShown.TryGetValue(s.Id, out var last) && c.Month - last < s.CooldownMonths) return false;
            return s.When.All(w => w.Evaluate(c));
        }

        static int RollCount(Pcg32 rng, int[] w)
        {
            if (w == null || w.Length == 0) return 0;
            int total = w.Sum(x => Math.Max(0, x));
            if (total <= 0) return 0;
            int r = rng.NextInt(total);
            for (int k = 0; k < w.Length; k++)
            {
                int wk = Math.Max(0, w[k]);
                if (r < wk) return k;
                r -= wk;
            }
            return 0;
        }
    }
}
