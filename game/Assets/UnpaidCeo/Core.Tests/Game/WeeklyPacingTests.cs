using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>
    /// Chapter 1 played the way careers are played now — a week at a time (plan 5A). The table of the runs goes to
    /// TestResults/weekly_balance.txt; the baseline it measured before the weekly pool lives in the 5A ledger.
    /// </summary>
    public class WeeklyPacingTests
    {
        static readonly ulong[] Seeds = { 11, 23, 37, 41, 59 };

        /// <summary>Optimal chapter 1 in weeks before the weekly pool (Task 1 of plan 5A, monthly pool on weekly turns).</summary>
        const int BaselineOptimalMedianWeeks = 57;

        [Test]
        public void Optimal_weekly_play_takes_40_weeks_or_more_for_chapter1_and_stays_near_the_baseline()
        {
            var db = BotRunner.Content();
            var weeks = new List<int>();
            var bad = new List<string>();
            foreach (var seed in Seeds)
            {
                var o = BotRunner.RunWeekly(db, seed, "coffee_cart", new OptimalBot(weekly: true));
                if (!o.Finished) { bad.Add($"{seed}: did not finish chapter 1"); continue; }
                if (o.ChapterWeeks[1] < 40) bad.Add($"{seed}: chapter 1 in {o.ChapterWeeks[1]} weeks");
                weeks.Add(o.ChapterWeeks[1]);
            }
            weeks.Sort();
            if (weeks.Count > 0)
            {
                int median = weeks[weeks.Count / 2];
                if (median < BaselineOptimalMedianWeeks * 0.85 || median > BaselineOptimalMedianWeeks * 1.15)
                    bad.Add($"median {median} weeks vs baseline {BaselineOptimalMedianWeeks} (±15%)");
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void Weekly_chapter1_pacing_is_recorded()
        {
            var db = BotRunner.Content();
            var sb = new StringBuilder("seed\toptimal ch1 weeks\tcasual ch1 weeks\n");
            var bad = new List<string>();
            foreach (var seed in Seeds)
            {
                var o = BotRunner.RunWeekly(db, seed, "coffee_cart", new OptimalBot(weekly: true));
                var c = BotRunner.RunWeekly(db, seed, "coffee_cart", new CasualBot(weekly: true));
                if (!o.Finished) bad.Add($"{seed}: optimal play did not finish chapter 1 (week cap)");
                sb.AppendLine($"{seed}\t{o.ChapterWeeks[1]}\t{(c.Finished ? c.ChapterWeeks[1].ToString() : "never")}");
            }
            var dir = Path.Combine(UnityEngine.Application.dataPath, "..", "TestResults");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "weekly_balance.txt"), sb.ToString());
            UnityEngine.Debug.Log("[weekly-pacing]\n" + sb);
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }
    }
}
