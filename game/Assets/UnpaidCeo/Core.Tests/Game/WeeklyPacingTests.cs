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
