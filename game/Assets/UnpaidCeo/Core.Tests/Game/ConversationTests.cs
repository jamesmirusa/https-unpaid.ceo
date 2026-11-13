using System;
using System.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>
    /// Stage 5B step 3: scenes wait at their host and Core picks them without dice; one story scene per host a week;
    /// answers have tones and knowledge locks; trust reads as levels; a level brings a real bonus from Core.
    /// </summary>
    public class ConversationTests
    {
        const string Storylets = @"{ ""storylets"": [
  { ""id"": ""tony_a"", ""yarnNode"": ""tony_a"", ""host"": ""tony"", ""step"": 1, ""once"": true,
    ""when"": [ { ""notFact"": ""met_tony"" } ],
    ""tones"": { ""help"": ""friendly"", ""busy"": ""business"" },
    ""choices"": { ""help"": [ { ""npc"": ""tony"", ""delta"": 25 }, { ""setFact"": ""met_tony"" } ], ""busy"": [ { ""setFact"": ""met_tony"" } ] } },
  { ""id"": ""tony_b"", ""yarnNode"": ""tony_b"", ""host"": ""tony"", ""step"": 2, ""once"": true,
    ""when"": [ { ""fact"": ""met_tony"" } ],
    ""tones"": { ""ask"": ""business"", ""joke"": ""joke"" },
    ""requires"": { ""ask"": 30 },
    ""choices"": { ""ask"": [ { ""npc"": ""tony"", ""delta"": 5 } ], ""joke"": [ { ""npc"": ""tony"", ""delta"": 2 } ] } },
  { ""id"": ""tony_react"", ""yarnNode"": ""tony_react"", ""host"": ""tony"", ""reaction"": true, ""once"": true,
    ""when"": [ { ""fact"": ""sold_expired"" } ],
    ""choices"": { ""sorry"": [ { ""clearFact"": ""sold_expired"" } ] } },
  { ""id"": ""rain"", ""yarnNode"": ""rain"",
    ""choices"": { ""stay"": [ { ""stat"": ""energy"", ""delta"": 1 } ] } }
] }";

        const string FreeActions = @"{ ""freeActions"": [
  { ""id"": ""visit_tony"", ""actionPoints"": 1, ""cost"": 0, ""effects"": [ { ""npc"": ""tony"", ""delta"": 10 } ] },
  { ""id"": ""weekend_shift"", ""actionPoints"": 1, ""cost"": 0, ""requiresFact"": ""tony_weekend_shift"",
    ""effects"": [ { ""cash"": 13600 }, { ""stat"": ""energy"", ""delta"": -14 } ] }
] }";

        static ContentDb Db()
        {
            var balance = TestContent.Balance(weekWeights: "[0, 1]").Replace(@"""sources"": []",
                @"""levelBonuses"": [ { ""npc"": ""tony"", ""level"": 3, ""fact"": ""tony_weekend_shift"", ""key"": ""bonus.tony.3"" } ], ""sources"": []");
            return ContentDb.FromJson(new ContentFiles
            {
                Industries = TestContent.Industries(1.0), Balance = balance, Storylets = Storylets, FreeActions = FreeActions,
            });
        }

        static (GameEngine e, GameState s) New()
        {
            var e = new GameEngine(Db());
            var s = e.NewGame(3, TestContent.Hero);
            return (e, s);
        }

        [Test]
        public void The_scene_waiting_at_a_host_is_chosen_without_dice_reactions_first()
        {
            var (e, s) = New();
            Assert.AreEqual("tony_a", HostScenes.Waiting(s, e.Content, "tony")?.Id);
            s.Facts.Set("met_tony", 1);
            Assert.AreEqual("tony_b", HostScenes.Waiting(s, e.Content, "tony")?.Id);
            s.Facts.Set("sold_expired", 1);
            Assert.AreEqual("tony_react", HostScenes.Waiting(s, e.Content, "tony")?.Id, "a reaction to a past decision comes first");
            Assert.IsNull(HostScenes.Waiting(s, e.Content, "mom"));
        }

        [Test]
        public void Talking_makes_the_scene_pending_and_a_host_gives_one_story_scene_a_week()
        {
            var (e, s) = New();
            var scene = e.TalkTo(s, "tony");
            Assert.AreEqual("tony_a", scene?.Id);
            CollectionAssert.Contains(s.PendingStorylets, "tony_a");
            e.ChooseStoryletOption(s, "tony_a", "busy");
            Assert.IsFalse(HostScenes.CanTalk(s, "tony"), "one story scene per host per week");
            Assert.IsNull(e.TalkTo(s, "tony"), "after that, only everyday lines");
            e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0));
            Assert.AreEqual("tony_b", e.TalkTo(s, "tony")?.Id, "next week the line goes on");
        }

        [Test]
        public void Weekly_random_scenes_never_take_a_hosts_scene()
        {
            var (e, s) = New();
            for (int w = 0; w < 12; w++)
            {
                var picks = e.BeginWeek(s);
                Assert.IsTrue(picks.All(p => p.Host == null), "host scenes wait at their host");
                foreach (var p in picks) e.ChooseStoryletOption(s, p.Id, p.Choices.Keys.First());
                e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0));
            }
        }

        [Test]
        public void Talking_is_logged_and_replayed()
        {
            var (e, s) = New();
            e.TalkTo(s, "tony");
            e.ChooseStoryletOption(s, "tony_a", "help");
            e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0));
            Assert.IsTrue(s.Log.Any(l => l.Kind == DecisionLogEntry.TalkTo));
            var r = GameEngine.Replay(e.Content, 3, TestContent.Hero, s.Log);
            Assert.AreEqual(s.Npcs.Get("tony"), r.Npcs.Get("tony"));
            Assert.IsTrue(r.Facts.Has("met_tony"));
        }

        [Test]
        public void A_strict_replay_refuses_a_talk_when_nothing_waits()
        {
            var (e, s) = New();
            var start = new RunStart { Kind = RunStart.Career, Seed = 3, Hero = TestContent.Hero };
            var log = new[] { new DecisionLogEntry { Kind = DecisionLogEntry.TalkTo, Payload = "{\"host\":\"mom\"}" } };
            var res = GameEngine.ReplayStrict(e.Content, start, _ => null, log);
            Assert.AreEqual("refused:" + DecisionLogEntry.TalkTo, res.Error);
        }

        [Test]
        public void An_answer_that_needs_knowledge_is_refused_without_it()
        {
            var (e, s) = New();
            s.Facts.Set("met_tony", 1);
            e.TalkTo(s, "tony");
            s.StudyXp = 10;
            Assert.Throws<ArgumentException>(() => e.ChooseStoryletOption(s, "tony_b", "ask"));
            Assert.IsTrue(HostScenes.Locked(e.Content.Storylets.First(x => x.Id == "tony_b"), "ask", s));
            s.StudyXp = 30;
            Assert.IsFalse(HostScenes.Locked(e.Content.Storylets.First(x => x.Id == "tony_b"), "ask", s));
            e.ChooseStoryletOption(s, "tony_b", "ask");
            Assert.AreEqual(5, s.Npcs.Get("tony"));
        }

        [Test]
        public void Tones_are_read_from_the_scene()
        {
            var st = Db().Storylets.First(x => x.Id == "tony_b");
            Assert.AreEqual("business", st.Tones["ask"]);
            Assert.AreEqual(30, st.Requires["ask"]);
        }

        [TestCase(0, 0)] [TestCase(9, 0)] [TestCase(10, 1)] [TestCase(35, 3)] [TestCase(100, 10)] [TestCase(-20, 0)]
        public void Trust_reads_as_a_level_from_0_to_10(int trust, int level) => Assert.AreEqual(level, Relationships.Level(trust));

        [TestCase(-1, "cold")] [TestCase(0, "neutral")] [TestCase(19, "neutral")] [TestCase(20, "warm")] [TestCase(50, "close")]
        public void Trust_falls_into_a_tier(int trust, string tier) => Assert.AreEqual(tier, Relationships.Tier(trust));

        [Test]
        public void Reaching_a_level_grants_its_bonus_and_unlocks_a_deed()
        {
            var (e, s) = New();
            Assert.AreEqual(ChoiceState.Locked, ActionChoices.State(s, e.Content, new string[0], "weekend_shift"));
            e.TalkTo(s, "tony");
            e.ChooseStoryletOption(s, "tony_a", "help");   // trust 25: level 2
            Assert.IsFalse(s.Facts.Has("tony_weekend_shift"));
            var r = e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0, "visit_tony"));   // +10: level 3
            Assert.IsTrue(s.Facts.Has("tony_weekend_shift"), "the bonus is a Core fact the server replays");
            Assert.IsTrue(r.LevelUps.Any(l => l.Npc == "tony" && l.Level == 3 && l.Key == "bonus.tony.3"));
            Assert.AreEqual(ChoiceState.Open, ActionChoices.State(s, e.Content, new string[0], "weekend_shift"));
            Assert.AreEqual(1, s.Journal.Count(j => j.TextKey == "journal.bonus.tony.3"), "journaled once");
        }

        [Test]
        public void A_locked_deed_is_refused_by_the_engine()
        {
            var (e, s) = New();
            var r = e.EndWeek(s, TestContent.Decisions(60, 15, 25, 0, "weekend_shift"));
            CollectionAssert.Contains(r.RejectedActions, "weekend_shift");
        }

        [Test]
        public void A_bonus_reached_in_a_scene_is_granted_at_once()
        {
            var (e, s) = New();
            s.Npcs.Add("tony", 10);
            e.TalkTo(s, "tony");
            e.ChooseStoryletOption(s, "tony_a", "help");   // 35: level 3
            Assert.IsTrue(s.Facts.Has("tony_weekend_shift"));
            Assert.AreEqual("bonus.tony.3", LevelBonuses.Reached(e.Content.Balance, "tony", 2, 3)?.Key);
        }
    }
}
