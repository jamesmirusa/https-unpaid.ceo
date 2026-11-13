using System.IO;
using System.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>Three daily missions: deterministic per day, rewards outside the replayed state (stage 2A, Task 8).</summary>
    public class DailyMissionsTests
    {
        internal const string Missions = @"{ ""missions"": [
  { ""id"": ""months_3"", ""kind"": ""endMonth"", ""target"": 3, ""skill"": ""operations"", ""xp"": 10, ""ceoPoints"": 5, ""titleKey"": ""mission.months_3"" },
  { ""id"": ""months_1"", ""kind"": ""endMonth"", ""target"": 1, ""skill"": ""sales"", ""xp"": 5, ""ceoPoints"": 3, ""titleKey"": ""mission.months_1"" },
  { ""id"": ""scenes_2"", ""kind"": ""storyletChoice"", ""target"": 2, ""skill"": ""leadership"", ""xp"": 10, ""ceoPoints"": 5, ""titleKey"": ""mission.scenes_2"" },
  { ""id"": ""quiz_3"", ""kind"": ""quizAnswer"", ""target"": 3, ""skill"": ""finance"", ""xp"": 10, ""ceoPoints"": 5, ""titleKey"": ""mission.quiz_3"" },
  { ""id"": ""actions_2"", ""kind"": ""freeAction"", ""target"": 2, ""skill"": ""marketing"", ""xp"": 8, ""ceoPoints"": 4, ""titleKey"": ""mission.actions_2"" },
  { ""id"": ""quiz_1"", ""kind"": ""quizAnswer"", ""target"": 1, ""skill"": ""finance"", ""xp"": 5, ""ceoPoints"": 3, ""titleKey"": ""mission.quiz_1"" }
] }";

        static ContentDb Db(string missions = Missions) => ContentDb.FromJson(new ContentFiles
        {
            Industries = TestContent.Industries(1.0), Balance = TestContent.Balance(), Missions = missions,
        });

        static string[] Ids(int day, ulong seed, ContentDb db) => DailyMissions.For(day, seed, db).Select(m => m.Id).ToArray();

        [Test]
        public void Same_day_same_missions()
        {
            var db = Db();
            var today = Ids(20000, 42, db);
            Assert.AreEqual(3, today.Length);
            Assert.AreEqual(3, today.Distinct().Count());
            CollectionAssert.AreEqual(today, Ids(20000, 42, db));
            Assert.IsTrue(Enumerable.Range(20001, 10).Any(d => !Ids(d, 42, db).SequenceEqual(today)), "other days bring other missions");
            Assert.IsTrue(Enumerable.Range(20000, 10).Any(d => !Ids(d, 43, db).SequenceEqual(Ids(d, 42, db))), "other players get other missions");
        }

        [Test]
        public void Missions_reward_xp_and_points_never_cash()
        {
            Assert.IsFalse(typeof(MissionDef).GetProperties().Any(p => p.PropertyType == typeof(Money)), "a mission cannot carry money");

            var db = Db();
            var e = new GameEngine(db);
            var s = e.NewGame(42, TestContent.Hero);
            var t = new TurnState();
            TurnBudget.Refresh(t, 20000 * 86400L + 9 * 3600, 0, db.Balance);
            var m = new MetaProgress();
            var cash = s.Cash;
            var hash = StateHash.Compute(s);

            var today = DailyMissions.For(t.Day, s.Seed, db);
            var done = today.Select(x => x.Kind).Distinct().SelectMany(k => DailyMissions.Record(m, t, s.Seed, db, k, 10)).ToList();
            CollectionAssert.AreEquivalent(today.Select(x => x.Id), done.Select(x => x.Id));
            Assert.AreEqual(today.Sum(x => x.CeoPoints), m.CeoPoints);
            foreach (var g in today.GroupBy(x => x.Skill)) Assert.AreEqual(g.Sum(x => x.Xp), m.SkillXp[g.Key], g.Key);
            Assert.AreEqual(cash, s.Cash, "missions never pay cash");
            Assert.AreEqual(hash, StateHash.Compute(s), "rewards live outside the replayed game state");
            Assert.AreEqual(1, t.Streak, "all three missions keep the streak");

            // A finished mission pays once.
            Assert.IsEmpty(DailyMissions.Record(m, t, s.Seed, db, today[0].Kind, 10));
            Assert.AreEqual(today.Sum(x => x.CeoPoints), m.CeoPoints);
        }

        [Test]
        public void Missions_count_manual_actions_from_the_log_and_skip_autopilot()
        {
            var db = ContentDb.FromJson(new ContentFiles
            {
                Industries = TestContent.Industries(1.0), Balance = TestContent.Balance(), FreeActions = TestContent.FreeActions,
                Storylets = TestContent.Storylets,
                Missions = @"{ ""missions"": [
  { ""id"": ""m1"", ""kind"": ""endMonth"", ""target"": 2, ""skill"": ""sales"", ""xp"": 5, ""ceoPoints"": 1, ""titleKey"": ""mission.m1"" },
  { ""id"": ""m2"", ""kind"": ""freeAction"", ""target"": 2, ""skill"": ""sales"", ""xp"": 5, ""ceoPoints"": 1, ""titleKey"": ""mission.m2"" },
  { ""id"": ""m3"", ""kind"": ""storyletChoice"", ""target"": 99, ""skill"": ""sales"", ""xp"": 5, ""ceoPoints"": 1, ""titleKey"": ""mission.m3"" } ] }",
            });
            var e = new GameEngine(db);
            var s = e.NewGame(42, TestContent.Hero);
            var t = new TurnState();
            long now = 20000 * 86400L + 9 * 3600;
            TurnBudget.Refresh(t, now, 0, db.Balance);
            var m = new MetaProgress();

            e.EndMonth(s, TestContent.Decisions(50, 20, 20, 10, "network"));
            DailyMissions.Sync(m, t, s, db);
            Assert.AreEqual(1, DailyMissions.Progress(m, t, "m1"));
            Assert.AreEqual(1, DailyMissions.Progress(m, t, "m2"));

            TurnBudget.OnLaunch(e, s, t, now + 9 * 3600, 0);   // two autopilot months
            DailyMissions.SkipLog(m, s);
            DailyMissions.Sync(m, t, s, db);
            Assert.AreEqual(1, DailyMissions.Progress(m, t, "m1"), "autopilot months are not the player's");

            e.EndMonth(s, TestContent.Decisions(50, 20, 20, 10, "save"));
            var done = DailyMissions.Sync(m, t, s, db);
            CollectionAssert.AreEquivalent(new[] { "m1", "m2" }, done.Select(x => x.Id));
            Assert.IsTrue(DailyMissions.IsDone(m, t, "m1"));
        }

        [Test]
        public void Quiz_practice_counts_each_question_once_a_day()
        {
            var db = Db(@"{ ""missions"": [
  { ""id"": ""q"", ""kind"": ""quizAnswer"", ""target"": 2, ""skill"": ""finance"", ""xp"": 5, ""ceoPoints"": 1, ""titleKey"": ""mission.q"" } ] }");
            var t = new TurnState();
            TurnBudget.Refresh(t, 20000 * 86400L, 0, db.Balance);
            var m = new MetaProgress();
            DailyMissions.RecordQuiz(m, t, 1, db, "reserve", 0);
            DailyMissions.RecordQuiz(m, t, 1, db, "reserve", 0);
            Assert.AreEqual(1, DailyMissions.Progress(m, t, "q"), "the same question twice counts once");
            Assert.AreEqual(1, DailyMissions.RecordQuiz(m, t, 1, db, "reserve", 1).Count);
            TurnBudget.Refresh(t, 20001 * 86400L, 0, db.Balance);
            DailyMissions.RecordQuiz(m, t, 1, db, "reserve", 0);
            Assert.AreEqual(1, DailyMissions.Progress(m, t, "q"), "a new day counts it again");
        }

        [Test]
        public void Bad_missions_fail_at_load()
        {
            var db = Db(@"{ ""missions"": [
  { ""id"": ""x"", ""kind"": ""buyCoins"", ""target"": 0, ""skill"": ""luck"", ""xp"": -1, ""ceoPoints"": 1, ""titleKey"": ""mission.x"" } ] }");
            var errors = db.Validate().Errors;
            Assert.That(errors, Has.Some.Contains("buyCoins"));
            Assert.That(errors, Has.Some.Contains("target"));
            Assert.That(errors, Has.Some.Contains("luck"));
            Assert.That(errors, Has.Some.Contains("xp"));
        }

        [Test]
        public void Shipped_missions_are_valid_and_reward_every_skill()
        {
            var path = Path.Combine(UnityEngine.Application.dataPath, "UnpaidCeo", "Content", "missions.json");
            var db = ContentDb.FromJson(new ContentFiles { Missions = File.ReadAllText(path) });
            Assert.IsEmpty(db.Validate().Errors.Where(x => x.Contains("mission")));
            Assert.GreaterOrEqual(db.Missions.Count, 9);
            CollectionAssert.IsSubsetOf(FounderSkills.All, db.Missions.Values.Select(x => x.Skill).Distinct().ToList());
        }
    }
}
