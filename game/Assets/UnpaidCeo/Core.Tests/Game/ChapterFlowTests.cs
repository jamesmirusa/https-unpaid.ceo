using System.IO;
using System.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>Chapter flow: moving district, choosing an industry, chapter goals, net worth (stage 2A, Task 6).</summary>
    public class ChapterFlowTests
    {
        static GameEngine Engine(bool noPersonalCosts = false) => new GameEngine(Stage2Content.Db(noPersonalCosts: noPersonalCosts));

        static string ContentDir => Path.Combine(UnityEngine.Application.dataPath, "UnpaidCeo", "Content");

        static ContentDb RealContent()
        {
            string Read(string f) { var p = Path.Combine(ContentDir, f); return File.Exists(p) ? File.ReadAllText(p) : null; }
            return ContentDb.FromJson(new ContentFiles { Industries = Read("industries.json"), Balance = Read("balance.json") });
        }

        static GameState Chapter1AtGoal(GameEngine e)
        {
            var s = e.NewGame(5, TestContent.Hero);
            s.Cash = e.Content.Balance.Chapter1CashGoal;
            s.Reserve = e.Content.Balance.PersonalCosts.Total;
            return s;
        }

        static void PlayMonth(GameEngine e, GameState s, MonthDecisions d)
        {
            foreach (var st in e.BeginMonth(s).ToList())
                e.ChooseStoryletOption(s, st.Id, st.Choices.Keys.OrderBy(k => k).First());
            e.EndMonth(s, d);
        }

        [Test]
        public void Chapter1_goal_lets_the_player_move_to_brooklyn()
        {
            var e = Engine();
            var s = Chapter1AtGoal(e);
            Assert.AreEqual("bronx", s.District);
            Assert.IsTrue(e.AdvanceChapter(s));
            Assert.AreEqual(2, s.Chapter);
            Assert.AreEqual("brooklyn", s.District);
            Assert.IsTrue(s.Facts.Has("chapter2_start"));
            Assert.AreEqual(DecisionLogEntry.AdvanceChapter, s.Log.Last().Kind);
        }

        [Test]
        public void Cannot_advance_before_the_goal()
        {
            var e = Engine();
            var s = e.NewGame(5, TestContent.Hero);
            Assert.IsFalse(e.AdvanceChapter(s));
            Assert.AreEqual(1, s.Chapter);
            Assert.IsFalse(s.Facts.Has("chapter2_start"));
            Assert.IsEmpty(s.Log);

            // Chapter 3 is the last chapter of this stage: its goal met still does not advance.
            var (e3, s3) = Stage2Content.Chapter2(30_000_000);
            s3.Chapter = 3;
            s3.Businesses.Single(b => b.IndustryId == "shop").Units = 2;
            Assert.IsTrue(e3.Chapter(s3).GoalMet);
            Assert.IsFalse(e3.AdvanceChapter(s3));
            Assert.AreEqual(3, s3.Chapter);
        }

        [Test]
        public void Choosing_an_industry_adds_a_business_with_no_units()
        {
            var e = Engine();
            var s = e.NewGame(5, TestContent.Hero);
            Assert.IsFalse(e.ChooseIndustry(s, "shop"), "chapter 1 has no business yet");
            s.Chapter = 2;
            Assert.IsFalse(e.ChooseIndustry(s, "gig_test"), "a gig is not a chapter-2 industry");
            Assert.IsFalse(e.ChooseIndustry(s, "nope"));
            int before = s.Businesses.Count;
            Assert.IsTrue(e.ChooseIndustry(s, "shop"));
            Assert.AreEqual(before + 1, s.Businesses.Count);
            var shop = s.Businesses.Single(b => b.IndustryId == "shop");
            Assert.AreEqual("shop", shop.Id);
            Assert.AreEqual(0, shop.Units);
            Assert.AreEqual(e.Content.Industries["shop"].Price, shop.Price);
            Assert.IsTrue(s.Facts.Has("industry_chosen"));
            Assert.AreEqual(DecisionLogEntry.ChooseIndustry, s.Log.Last().Kind);
        }

        [Test]
        public void Second_industry_choice_is_rejected()
        {
            var e = Engine();
            var s = e.NewGame(5, TestContent.Hero);
            s.Chapter = 2;
            Assert.IsTrue(e.ChooseIndustry(s, "shop"));
            Assert.IsFalse(e.ChooseIndustry(s, "truck"));
            Assert.IsFalse(e.ChooseIndustry(s, "shop"));
            Assert.AreEqual(1, s.Businesses.Count(b => b.IndustryId == "shop" || b.IndustryId == "truck"));
        }

        [Test]
        public void Every_industry_can_be_started_from_the_chapter1_goal()
        {
            var db = RealContent();
            foreach (var id in db.Balance.Chapter2Industries)
            {
                var e = new GameEngine(db);
                var s = Chapter1AtGoal(e);
                s.Life.Reputation = 50;
                Assert.IsTrue(e.AdvanceChapter(s), id);
                Assert.IsTrue(e.ChooseIndustry(s, id), id);
                var capex = db.Industries[id].Capex;
                if (capex > s.Cash)
                    Assert.IsTrue(e.TakeLoan(s, capex, 36), $"{id}: a {capex.Cents} cent loan at reputation 50");
                var d = TestContent.Decisions(50, 20, 20, 10);
                d.Levers.Add(Stage2Content.Lever(id, "units", 1));
                var r = e.EndMonth(s, d);
                Assert.IsEmpty(r.RejectedLevers, id);
                Assert.AreEqual(1, s.Businesses.Single(b => b.IndustryId == id).Units, id);
            }
        }

        [Test]
        public void Chapter2_goal_needs_break_even_and_capital()
        {
            // Rich, but no unit has broken even yet.
            var (e, s) = Stage2Content.Chapter2(2_700_000, noPersonalCosts: true);
            Assert.IsFalse(e.Chapter(s).GoalMet);
            e.EndMonth(s, Stage2Content.Plan(Stage2Content.Lever("shop", "units", 1)));
            Assert.IsTrue(s.Facts.Has("break_even"), "a profitable first unit breaks even");
            Assert.GreaterOrEqual(Chapters.NetWorth(s, e.Content).Cents, e.Content.Balance.Chapter2CapitalGoal.Cents);
            Assert.IsTrue(e.Chapter(s).GoalMet);
            Assert.AreEqual(1.0, e.Chapter(s).Progress);
            Assert.IsTrue(s.Facts.Has("chapter2_done"));

            // Breaking even with little capital is not enough.
            var (e2, s2) = Stage2Content.Chapter2(200_000, noPersonalCosts: true);
            e2.EndMonth(s2, Stage2Content.Plan(Stage2Content.Lever("shop", "units", 1)));
            Assert.IsTrue(s2.Facts.Has("break_even"));
            Assert.IsFalse(e2.Chapter(s2).GoalMet);
            Assert.That(e2.Chapter(s2).Progress, Is.GreaterThan(0.0).And.LessThan(1.0));
            Assert.IsFalse(s2.Facts.Has("chapter2_done"));
        }

        [Test]
        public void Chapter3_goal_needs_a_second_unit_and_staff_or_capital()
        {
            var (e, s) = Stage2Content.Chapter2(0, noPersonalCosts: true);
            s.Chapter = 3;
            var shop = s.Businesses.Single(b => b.IndustryId == "shop");
            shop.Units = 1;
            shop.Employees = 10;
            Assert.IsFalse(e.Chapter(s).GoalMet, "one unit");
            shop.Units = 2;
            Assert.IsTrue(e.Chapter(s).GoalMet, "two units and ten staff");
            shop.Employees = 3;
            Assert.IsFalse(e.Chapter(s).GoalMet, "two units, few staff, little capital");
            Assert.That(e.Chapter(s).Progress, Is.GreaterThan(0.0).And.LessThan(1.0));
            s.Cash = e.Content.Balance.Chapter3CapitalGoal;
            Assert.IsTrue(e.Chapter(s).GoalMet, "two units and the capital goal");
        }

        [Test]
        public void Net_worth_counts_assets_at_resale_and_subtracts_loans()
        {
            var (e, s) = Stage2Content.Chapter2(1_000_000);
            s.Reserve = Money.FromCents(50_000);
            s.Businesses.Single(b => b.IndustryId == "shop").Units = 3;   // 3 × $1,000 capex × 50% resale = $1,500
            s.Loans.Add(new LoanState { Id = "l", Principal = Money.FromCents(500_000), Balance = Money.FromCents(400_000), Apr = 0.12, MonthsLeft = 12 });
            Assert.AreEqual(1_000_000 + 50_000 + 150_000 - 400_000, Chapters.NetWorth(s, e.Content).Cents);
        }

        [Test]
        public void Rent_follows_the_district()
        {
            var b = Stage2Content.Db().Balance;
            Assert.Greater(b.PersonalCostsFor(2).Room.Cents, b.PersonalCostsFor(1).Room.Cents);
            Assert.Greater(b.PersonalCostsFor(3).Room.Cents, b.PersonalCostsFor(2).Room.Cents);
            foreach (int chapter in new[] { 1, 2, 3 })
            {
                var e = Engine();
                var s = e.NewGame(5, TestContent.Hero);
                s.Chapter = chapter;
                s.LifetimeEarnings = Money.FromDollars(1_000_000);   // bills are on
                var r = e.EndMonth(s, TestContent.Decisions(50, 20, 20, 10));
                Assert.AreEqual(b.PersonalCostsFor(chapter).Total, r.PersonalCosts, $"chapter {chapter}");
            }

            // The real game: a Bronx room, a Brooklyn studio, a Queens apartment; living costs +10% a chapter.
            var real = RealContent().Balance;
            Assert.AreEqual(110_000, real.PersonalCostsFor(1).Room.Cents);
            Assert.AreEqual(190_000, real.PersonalCostsFor(2).Room.Cents);
            Assert.AreEqual(230_000, real.PersonalCostsFor(3).Room.Cents);
            Assert.AreEqual(real.PersonalCosts.Food.MulRound(1.1), real.PersonalCostsFor(2).Food);
            Assert.AreEqual(real.PersonalCostsFor(2).Transit.MulRound(1.1), real.PersonalCostsFor(3).Transit);
        }

        [Test]
        public void Replay_reproduces_every_action_kind()
        {
            var db = ContentDb.FromJson(new ContentFiles
            {
                Industries = Stage2Content.Industries.Replace("\"capacityPerWorkPoint\": 1.0", "\"capacityPerWorkPoint\": 60.0"),
                Balance = Stage2Content.Balance(noPersonalCosts: true),
                Storylets = TestContent.Storylets,
                FreeActions = TestContent.FreeActions,
                Lessons = LessonTriggerTests.Lessons,
            });
            var e = new GameEngine(db);
            var s = e.NewGame(2027, TestContent.Hero);
            e.TutorialGig(s);
            PlayMonth(e, s, Stage2Content.Plan());
            Assert.IsTrue(e.AnswerQuiz(s, "revenue_vs_profit", 0, 1));
            Assert.IsTrue(e.AdvanceChapter(s), "one strong gig month reaches the chapter-1 goal");
            Assert.IsTrue(e.ChooseIndustry(s, "shop"));
            Assert.IsTrue(e.TakeLoan(s, Money.FromCents(100_000), 12));
            PlayMonth(e, s, Stage2Content.Plan(Stage2Content.Lever("shop", "units", 1), Stage2Content.Lever("shop", "staff", 1)));
            PlayMonth(e, s, Stage2Content.Plan());

            var shop = s.Businesses.Single(b => b.IndustryId == "shop");
            Assert.AreEqual(1, shop.Units);
            Assert.AreEqual(1, shop.Employees);
            var replayed = GameEngine.Replay(db, 2027, TestContent.Hero, s.Log);
            Assert.AreEqual(StateHash.Compute(s), StateHash.Compute(replayed));
            Assert.AreEqual(2, replayed.Chapter);
        }
    }
}
