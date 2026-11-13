using System;
using System.Collections.Generic;
using System.Linq;
using UnpaidCeo.Core.Content;

namespace UnpaidCeo.Core.Game
{
    /// <summary>
    /// The week planned with one tap (stage 5A): a mode is a ready split of the hero's free time (balance.json → weekModes),
    /// a focus puts the work into one gig or splits it evenly. Both only fill the plan's shares, so the decision log and
    /// the server replay keep their format.
    /// </summary>
    public static class WeekPlanning
    {
        /// <summary>The order the modes are shown in.</summary>
        public static readonly string[] ModeOrder = { "hustle", "balance", "study", "family" };

        public const string EvenFocus = "even";

        /// <summary>Sets the plan's work, family, rest and study shares to the mode's.</summary>
        public static void ApplyMode(MonthDecisions d, string mode, Balance b)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            if (mode == null || !b.WeekModes.TryGetValue(mode, out var parts) || parts == null || parts.Length != 4)
                throw new ArgumentException($"unknown week mode '{mode}'", nameof(mode));
            d.Work = parts[0];
            d.Family = parts[1];
            d.Rest = parts[2];
            d.Study = parts[3];
        }

        /// <summary>The mode whose shares the plan has, or null for any other split.</summary>
        public static string ModeOf(MonthDecisions d, Balance b)
        {
            foreach (var mode in ModeOrder)
            {
                if (!b.WeekModes.TryGetValue(mode, out var p) || p == null || p.Length != 4) continue;
                if (d.Work == p[0] && d.Family == p[1] && d.Rest == p[2] && d.Study == p[3]) return mode;
            }
            return null;
        }

        /// <summary>A gig id puts all the work time there; <see cref="EvenFocus"/> splits it evenly (largest remainder first).</summary>
        public static void ApplyFocus(MonthDecisions d, IReadOnlyList<string> gigIds, string focus)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            if (gigIds == null || gigIds.Count == 0) return;
            if (focus == EvenFocus)
            {
                int share = 100 / gigIds.Count, extra = 100 % gigIds.Count;
                for (int i = 0; i < gigIds.Count; i++) d.WorkSplit[gigIds[i]] = share + (i < extra ? 1 : 0);
                return;
            }
            if (!gigIds.Contains(focus)) throw new ArgumentException($"unknown gig '{focus}'", nameof(focus));
            foreach (var id in gigIds) d.WorkSplit[id] = id == focus ? 100 : 0;
        }

        /// <summary>The gig all the work goes to, <see cref="EvenFocus"/> for an even split, or null for any other split.</summary>
        public static string FocusOf(MonthDecisions d, IReadOnlyList<string> gigIds)
        {
            if (gigIds == null || gigIds.Count == 0) return null;
            int Share(string id) => d.WorkSplit != null && d.WorkSplit.TryGetValue(id, out var v) ? v : 0;
            foreach (var id in gigIds)
                if (Share(id) == 100 && gigIds.All(o => o == id || Share(o) == 0)) return id;
            var shares = gigIds.Select(Share).ToList();
            return shares.Sum() == 100 && shares.Max() - shares.Min() <= 1 ? EvenFocus : null;
        }
    }
}
