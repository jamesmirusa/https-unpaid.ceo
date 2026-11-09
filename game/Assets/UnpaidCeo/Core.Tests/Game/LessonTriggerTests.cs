using System;
using System.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Game
{
    public class LessonTriggerTests
    {
        static string Quiz(string id) => string.Join(",", Enumerable.Range(0, 3).Select(q =>
            $@"{{ ""questionKey"": ""lesson.{id}.q{q}"", ""optionKeys"": [""lesson.{id}.q{q}.a0"", ""lesson.{id}.q{q}.a1"", ""lesson.{id}.q{q}.a2""], ""correctIndex"": 1 }}"));

        static string Lesson(string id, string fact, string skill) =>
            $@"{{ ""id"": ""{id}"", ""triggerFact"": ""{fact}"", ""titleKey"": ""lesson.{id}.title"", ""bodyKey"": ""lesson.{id}.body"",
                 ""quiz"": [ {Quiz(id)} ], ""skillReward"": {{ ""skill"": ""{skill}"", ""xp"": 5 }} }}";

        internal static readonly string Lessons = "{ \"lessons\": [ " + string.Join(",",
            Lesson("revenue_vs_profit", "first_income", "finance"),
            Lesson("reserve", "first_bills", "finance"),
            Lesson("first_loss", "first_loss", "finance"),
            Lesson("cash_gap", "in_debt", "operations"),
            Lesson("break_even", "chapter1_done", "sales")) + " ] }";

        static ContentDb Db() => TestContent.Db(lessons: Lessons);
        static MonthDecisions Month() => TestContent.Decisions(55, 15, 20, 10);

        [Test]
        public void Lesson_fires_once_on_trigger()
        {
            var e = new GameEngine(Db());
            var s = e.NewGame(1, TestContent.Hero);
            s.Facts.Set("first_loss", 1);

            var first = e.EndMonth(s, Month());
            CollectionAssert.Contains(first.Lessons, "first_loss");
            Assert.AreEqual(1, s.LessonsSeen["first_loss"]);

            var second = e.EndMonth(s, Month());
            CollectionAssert.DoesNotContain(second.Lessons, "first_loss");
            Assert.AreEqual(1, first.Lessons.Count(l => l == "first_loss"));
        }

        [Test]
        public void First_income_and_first_bills_unlock_their_lessons()
        {
            var e = new GameEngine(Db());
            var s = e.NewGame(2, TestContent.Hero);

            var m1 = e.EndMonth(s, Month());
            Assert.IsTrue(m1.Revenue.Cents > 0);
            Assert.IsTrue(s.Facts.Has("first_income"));
            CollectionAssert.Contains(m1.Lessons, "revenue_vs_profit");

            MonthReport billed = null;
            for (int i = 0; i < 6 && billed == null; i++)
            {
                var r = e.EndMonth(s, Month());
                if (r.PersonalCosts.Cents > 0) billed = r;
            }
            Assert.IsNotNull(billed, "personal costs never started");
            Assert.IsTrue(s.Facts.Has("first_bills"));
            CollectionAssert.Contains(billed.Lessons, "reserve");
        }

        [Test]
        public void Quiz_correct_answer_grants_skill_xp()
        {
            var e = new GameEngine(Db());
            var s = e.NewGame(3, TestContent.Hero);
            Assert.Throws<ArgumentException>(() => e.AnswerQuiz(s, "first_loss", 0, 1), "locked lesson");

            s.Facts.Set("first_loss", 1);
            e.EndMonth(s, Month());

            Assert.IsTrue(e.AnswerQuiz(s, "first_loss", 0, 1));
            Assert.AreEqual(5, s.Skills["finance"]);

            Assert.IsTrue(e.AnswerQuiz(s, "first_loss", 0, 1), "a repeated answer is still checked");
            Assert.AreEqual(5, s.Skills["finance"], "only the first answer to a question earns xp");

            Assert.IsFalse(e.AnswerQuiz(s, "first_loss", 1, 0));
            Assert.IsTrue(e.AnswerQuiz(s, "first_loss", 1, 1));
            Assert.AreEqual(5, s.Skills["finance"], "a correct retry after a wrong answer earns nothing");

            Assert.Throws<ArgumentException>(() => e.AnswerQuiz(s, "first_loss", 3, 0));
            Assert.Throws<ArgumentException>(() => e.AnswerQuiz(s, "first_loss", 0, 3));
            Assert.Throws<ArgumentException>(() => e.AnswerQuiz(s, "no_such_lesson", 0, 0));
        }

        static void PlayMonth(GameEngine e, GameState s)
        {
            foreach (var st in e.BeginMonth(s).ToList()) e.ChooseStoryletOption(s, st.Id, st.Choices.Keys.First());
            e.EndMonth(s, Month());
        }

        [Test]
        public void Quiz_answers_replay_to_the_same_state()
        {
            var db = Db();
            var e = new GameEngine(db);
            var s = e.NewGame(4, TestContent.Hero);
            PlayMonth(e, s);
            Assert.IsTrue(s.LessonsSeen.ContainsKey("revenue_vs_profit"));
            e.AnswerQuiz(s, "revenue_vs_profit", 0, 1);
            e.AnswerQuiz(s, "revenue_vs_profit", 2, 0);
            PlayMonth(e, s);

            var replayed = GameEngine.Replay(db, 4, TestContent.Hero, s.Log);
            Assert.AreEqual(2, s.Log.Count(l => l.Kind == DecisionLogEntry.QuizAnswer));
            Assert.AreEqual(5, replayed.Skills["finance"]);
            Assert.AreEqual(StateHash.Compute(s), StateHash.Compute(replayed));
        }

        [Test]
        public void Lesson_validation_reports_bad_quiz_and_unknown_skill()
        {
            var bad = @"{ ""lessons"": [
  { ""id"": ""x"", ""triggerFact"": ""first_loss"", ""titleKey"": ""t"", ""bodyKey"": ""b"",
    ""quiz"": [ { ""questionKey"": ""q"", ""optionKeys"": [""a"", ""b""], ""correctIndex"": 2 } ],
    ""skillReward"": { ""skill"": ""juggling"", ""xp"": 5 } },
  { ""id"": ""x"", ""triggerFact"": """", ""titleKey"": ""t"", ""bodyKey"": ""b"", ""quiz"": [], ""skillReward"": { ""skill"": ""finance"", ""xp"": 5 } }
] }";
            var errors = TestContent.Db(lessons: bad).Validate().Errors;
            Assert.That(errors, Has.Some.Contains("lesson x: quiz needs 3 questions"));
            Assert.That(errors, Has.Some.Contains("lesson x: question 0 needs 3 options"));
            Assert.That(errors, Has.Some.Contains("lesson x: question 0 correctIndex out of range"));
            Assert.That(errors, Has.Some.Contains("lesson x: unknown skill 'juggling'"));
            Assert.That(errors, Has.Some.Contains("lesson x: duplicate id"));
            Assert.That(errors, Has.Some.Contains("lesson x: missing triggerFact"));
        }
    
        // Stage 2A (Task 9): chapter 2–3 lessons, their trigger facts and skills.
        static readonly (string id, string fact, string skill)[] Stage2Lessons =
        {
            ("unit_economics", "industry_chosen", "finance"),
            ("pricing", "price_changed", "sales"),
            ("estimated_taxes", "break_even", "finance"),
            ("llc", "chapter2_start", "finance"),
            ("hiring", "first_hire", "leadership"),
            ("payroll", "first_payroll", "operations"),
            ("cash_gap_b2b", "receivable_gap", "finance"),
            ("cac_ltv", "marketing_started", "marketing"),
            ("loans", "first_loan", "finance"),
        };

        static ContentDb ShippedLessons() => TestContent.Db(lessons: System.IO.File.ReadAllText(
            System.IO.Path.Combine(UnityEngine.Application.dataPath, "UnpaidCeo", "Content", "lessons.json")));

        [Test]
        public void Each_stage2_lesson_is_queued_by_its_fact()
        {
            var db = ShippedLessons();
            Assert.IsEmpty(db.Validate().Errors.Where(x => x.Contains("lesson")));
            foreach (var (id, fact, skill) in Stage2Lessons)
            {
                Assert.IsTrue(db.Lessons.TryGetValue(id, out var lesson), id);
                Assert.AreEqual(fact, lesson.TriggerFact, id);
                Assert.AreEqual(skill, lesson.SkillReward.Skill, id);
                Assert.AreEqual(3, lesson.Quiz.Count, id);

                var e = new GameEngine(db);
                var s = e.NewGame(1, TestContent.Hero);
                s.Facts.Set(fact, 1);
                var report = e.EndMonth(s, Month());
                CollectionAssert.Contains(report.Lessons, id, $"{fact} queues {id}");
            }
        }

        [Test]
        public void Receivable_gap_fact_is_set_when_cash_is_negative_with_receivables()
        {
            // A B2B truck: clients pay a month later, costs go out now.
            var (e, s) = Stage2Content.Chapter2(50_000, "truck", noPersonalCosts: true);
            s.Businesses.Single(b => b.IndustryId == "truck").Units = 1;
            e.EndMonth(s, Stage2Content.Plan());
            Assert.Less(s.Cash.Cents, 0);
            Assert.IsTrue(s.Facts.Has("receivable_gap"));

            // In debt with nobody owing you money is an ordinary cash problem, not a receivable gap.
            var (e2, s2) = Stage2Content.Chapter2(-100_000, "shop", noPersonalCosts: true);
            e2.EndMonth(s2, Stage2Content.Plan());
            Assert.Less(s2.Cash.Cents, 0);
            Assert.IsFalse(s2.Facts.Has("receivable_gap"));
        }
}
}
