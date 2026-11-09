using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Core.Tests.Game;

namespace UnpaidCeo.Core.Tests.Online
{
    /// <summary>Strict replay (server validation) and scenario starts for challenge and duel runs (stage 2B, Task 1).</summary>
    public class StrictReplayTests
    {
        static ScenarioDef NoScenario(RunStart r) => null;

        /// <summary>An honest career: the optimal bot plays through GameEngine, so every action is logged.</summary>
        static (GameEngine e, GameState s) PlayCareer(ulong seed, int months)
        {
            var e = new GameEngine(BotRunner.Content());
            var s = e.NewGame(seed, TestContent.Hero);
            var bot = new OptimalBot();
            e.TutorialGig(s);
            while (s.Month <= months)
            {
                foreach (var scene in e.BeginMonth(s).ToList())
                    e.ChooseStoryletOption(s, scene.Id, bot.PickChoice(e, s, scene));
                var report = e.EndMonth(s, bot.Decide(e, s));
                bot.AfterMonth(e, s, report);
                if (e.Chapter(s).GoalMet && s.Chapter < Chapters.LastChapter && e.AdvanceChapter(s) && s.Chapter == 2)
                    bot.StartChapter2(e, s, "coffee_cart");
            }
            return (e, s);
        }

        static RunStart Career(GameState s) => new RunStart { Kind = RunStart.Career, Seed = s.Seed, Hero = s.Hero };

        [Test]
        public void Strict_replay_matches_the_live_game()
        {
            var (e, s) = PlayCareer(21, 40);
            Assert.IsTrue(s.Log.Any(l => l.Kind == DecisionLogEntry.ChooseIndustry), "the run reaches chapter 2");
            var r = GameEngine.ReplayStrict(e.Content, Career(s), NoScenario, s.Log);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(StateHash.Compute(s), StateHash.Compute(r.State));
            Assert.IsNotNull(r.LastReport, "the last month's report comes back for revenue checks");
        }

        [Test]
        public void Strict_replay_stops_at_the_first_refused_action()
        {
            var (e, s) = PlayCareer(22, 3);
            var log = s.Log.ToList();
            log.Add(new DecisionLogEntry
            {
                Month = s.Month, Kind = DecisionLogEntry.TakeLoan,
                Payload = CanonicalJson.Serialize(new JObject { ["principal"] = 999_999_999, ["months"] = 36 }),
            });
            log.Add(log[log.Count - 2]);   // anything after the refusal is never applied
            var r = GameEngine.ReplayStrict(e.Content, Career(s), NoScenario, log);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(log.Count - 2, r.FailedAt);
            Assert.AreEqual("refused:takeLoan", r.Error);
        }

        [Test]
        public void Strict_replay_reports_a_bad_payload()
        {
            var (e, s) = PlayCareer(23, 3);
            var log = s.Log.ToList();
            int i = log.FindIndex(l => l.Kind == DecisionLogEntry.EndMonth);
            log[i] = new DecisionLogEntry { Month = log[i].Month, Kind = DecisionLogEntry.EndMonth, Payload = "{not json" };
            var r = GameEngine.ReplayStrict(e.Content, Career(s), NoScenario, log);
            Assert.AreEqual(i, r.FailedAt);
            StringAssert.StartsWith("bad_payload", r.Error);
        }

        [Test]
        public void A_second_tutorial_gig_is_refused()
        {
            var (e, s) = PlayCareer(24, 1);
            var log = s.Log.ToList();
            log.Add(new DecisionLogEntry { Month = s.Month, Kind = DecisionLogEntry.TutorialGig });
            var r = GameEngine.ReplayStrict(e.Content, Career(s), NoScenario, log);
            Assert.AreEqual(log.Count - 1, r.FailedAt);
            Assert.AreEqual("refused:tutorialGig", r.Error);
        }

        [Test]
        public void Loans_need_chapter_2()
        {
            var e = new GameEngine(BotRunner.Content());
            var s = e.NewGame(3, TestContent.Hero);
            Assert.IsFalse(e.TakeLoan(s, Money.FromCents(100_000), 12));
            Assert.IsEmpty(s.Loans);
            Assert.IsEmpty(s.Log);
        }

        [Test]
        public void Scenario_start_is_chapter_2_with_the_chosen_industry()
        {
            var db = BotRunner.Content();
            var e = new GameEngine(db);
            var def = new ScenarioDef { Seed = 11, IndustryId = "coffee_cart", StartCash = Money.FromCents(1_000_000), Months = 12 };
            var s = e.StartScenario(def, TestContent.Hero);
            Assert.AreEqual(2, s.Chapter);
            Assert.AreEqual("brooklyn", s.District);
            Assert.AreEqual(1_000_000, s.Cash.Cents);
            var business = s.Businesses.Where(b => db.Industries[b.IndustryId].Kind == IndustryKind.Business).ToList();
            Assert.AreEqual(1, business.Count);
            Assert.AreEqual("coffee_cart", business[0].IndustryId);
            Assert.AreEqual(0, business[0].Units);
            Assert.IsEmpty(s.Log, "the start is not part of the log: RunStart rebuilds it");

            var chapter1 = new HashSet<string>(JObject.Parse(File.ReadAllText(Path.Combine(
                UnityEngine.Application.dataPath, "UnpaidCeo", "Content", "storylets_ch1.json")))["storylets"].Select(t => (string)t["id"]));
            for (int m = 0; m < 6; m++)
            {
                var scenes = e.BeginMonth(s).ToList();
                Assert.IsFalse(scenes.Any(st => chapter1.Contains(st.Id)), $"month {s.Month}: no chapter-1 scene");
                foreach (var st in scenes) e.ChooseStoryletOption(s, st.Id, st.Choices.Keys.OrderBy(k => k).First());
                e.EndMonth(s, Stage2Content.Plan());
            }

            // The same RunStart replays the scenario run exactly.
            var start = new RunStart { Kind = RunStart.Challenge, ContextId = "w", IndustryId = "coffee_cart", Hero = TestContent.Hero };
            var r = GameEngine.ReplayStrict(db, start, _ => def, s.Log);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(StateHash.Compute(s), StateHash.Compute(r.State));
        }
    }
}
