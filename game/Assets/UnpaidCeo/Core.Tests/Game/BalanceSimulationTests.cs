using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>
    /// The pacing gate (plan 2A): an optimal bot needs at least 10 / 24 / 36 months for chapters 1–3, a casual bot still
    /// finishes chapter 2 within 60 months with every business, no business dominates, and optimal play never goes bankrupt.
    /// 5 seeds × 6 industries, shipped content. A table of the runs is written to TestResults/balance.txt.
    /// </summary>
    public class BalanceSimulationTests
    {
        static readonly ulong[] Seeds = { 11, 23, 37, 41, 59 };
        static string[] Industries => BotRunner.Content().Balance.Chapter2Industries.ToArray();

        static Dictionary<(string, ulong), RunResult> _optimal, _casual;

        static Dictionary<(string, ulong), RunResult> Optimal() => _optimal ?? (_optimal = RunAll(new OptimalBot(), 3));
        static Dictionary<(string, ulong), RunResult> Casual() => _casual ?? (_casual = RunAll(new CasualBot(), 2));

        static Dictionary<(string, ulong), RunResult> RunAll(IBot bot, int stopAfter)
        {
            var db = BotRunner.Content();
            var runs = new Dictionary<(string, ulong), RunResult>();
            foreach (var industry in Industries)
                foreach (var seed in Seeds)
                    runs[(industry, seed)] = BotRunner.Run(db, seed, industry, bot, 400, stopAfter);
            return runs;
        }

        [OneTimeTearDown]
        public void WriteTable()
        {
            if (_optimal == null && _casual == null) return;
            var sb = new StringBuilder("industry\tseed\topt ch1\topt ch2\topt ch3\topt bankrupt\tcasual ch2\n");
            foreach (var industry in Industries)
                foreach (var seed in Seeds)
                {
                    var o = _optimal?[(industry, seed)];
                    var c = _casual?[(industry, seed)];
                    sb.AppendLine($"{industry}\t{seed}\t{o?.ChapterMonths[1]}\t{o?.ChapterMonths[2]}\t{o?.ChapterMonths[3]}\t{o?.Bankrupt}\t{c?.ChapterMonths[2]}");
                }
            var dir = Path.Combine(UnityEngine.Application.dataPath, "..", "TestResults");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "balance.txt"), sb.ToString());
        }

        [Test]
        public void Optimal_play_takes_at_least_10_24_36_months_per_chapter()
        {
            var bad = new List<string>();
            foreach (var kv in Optimal())
            {
                var r = kv.Value;
                if (!r.Finished) { bad.Add($"{kv.Key}: did not finish chapter 3 (reached {r.ReachedChapter})"); continue; }
                if (r.ChapterMonths[1] < 10) bad.Add($"{kv.Key}: chapter 1 in {r.ChapterMonths[1]} months");
                if (r.ChapterMonths[2] < 24) bad.Add($"{kv.Key}: chapter 2 in {r.ChapterMonths[2]} months");
                if (r.ChapterMonths[3] < 36) bad.Add($"{kv.Key}: chapter 3 in {r.ChapterMonths[3]} months");
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void Casual_play_finishes_chapter2_within_60_months_with_every_industry()
        {
            var bad = Casual().Where(kv => !kv.Value.Finished || kv.Value.ChapterMonths[2] > 60)
                .Select(kv => $"{kv.Key}: chapter 2 in {(kv.Value.Finished ? kv.Value.ChapterMonths[2].ToString() : "never")} (reached {kv.Value.ReachedChapter})").ToList();
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void No_industry_dominates()
        {
            var median = Industries.ToDictionary(i => i, i =>
            {
                var months = Seeds.Select(s => Optimal()[(i, s)]).Where(r => r.Finished).Select(r => r.ChapterMonths[3]).OrderBy(x => x).ToList();
                return months.Count == 0 ? int.MaxValue : months[months.Count / 2];
            });
            var bad = new List<string>();
            foreach (var i in Industries)
            {
                var others = Industries.Where(x => x != i).Select(x => median[x]).OrderBy(x => x).ToList();
                double otherMedian = others[others.Count / 2];
                if (median[i] < 0.6 * otherMedian) bad.Add($"{i}: chapter 3 in {median[i]} months vs {otherMedian} for the others");
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void Optimal_play_never_goes_bankrupt()
        {
            var bad = Optimal().Where(kv => kv.Value.Bankrupt).Select(kv => kv.Key.ToString()).ToList();
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }
    }
}
