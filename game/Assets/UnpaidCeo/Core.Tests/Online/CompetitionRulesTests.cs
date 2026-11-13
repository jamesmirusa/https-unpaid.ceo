using System.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Core.Tests.Game;

namespace UnpaidCeo.Core.Tests.Online
{
    /// <summary>Leagues, the pacing limit, weekly challenge, duels and badges (stage 2B, Task 3).</summary>
    public class CompetitionRulesTests
    {
        const long Day = 86400;

        [TestCase(0L, "rookie")]
        [TestCase(999_999L, "rookie")]
        [TestCase(1_000_000L, "hustler")]
        [TestCase(10_000_000L, "boss")]
        [TestCase(100_000_000L, "tycoon")]
        [TestCase(1_000_000_000L, "legend")]
        [TestCase(-500_000L, "rookie")]
        public void League_thresholds(long netWorthCents, string league)
        {
            Assert.AreEqual(league, Leagues.For(netWorthCents));
        }

        [Test]
        public void Next_league_threshold()
        {
            Assert.AreEqual(1_000_000L, Leagues.Next(0));
            Assert.AreEqual(10_000_000L, Leagues.Next(1_000_000));
            Assert.IsNull(Leagues.Next(2_000_000_000), "Legend is the top");
            CollectionAssert.AreEqual(new[] { "rookie", "hustler", "boss", "tycoon", "legend" }, Leagues.All);
        }

        [Test]
        public void Pacing_allows_eight_months_a_day_plus_onboarding()
        {
            var b = Stage2Content.Db().Balance;
            Assert.AreEqual(6, b.DailyManualMonths);
            Assert.AreEqual(2, b.AutopilotMaxMonths);
            const long first = 1_800_000_000;
            Assert.AreEqual(10, Pacing.MaxCareerMonths(first, first + 3600, b), "the first day");
            Assert.AreEqual(26, Pacing.MaxCareerMonths(first, first + 2 * Day, b), "two days later");
            Assert.AreEqual(10, Pacing.MaxCareerMonths(first, first - 5 * Day, b), "a clock before first seen grants nothing extra");
        }

        [Test]
        public void Week_index_turns_on_monday_utc()
        {
            const long mondayNov9 = 1_794_182_400;   // 2026-11-09 00:00:00 UTC, a Monday
            int week = WeeklyChallenge.WeekIndex(mondayNov9);
            Assert.AreEqual(week - 1, WeeklyChallenge.WeekIndex(mondayNov9 - 1), "Sunday 23:59:59 is last week");
            Assert.AreEqual(week, WeeklyChallenge.WeekIndex(mondayNov9 + 7 * Day - 1), "Sunday night is still this week");
            Assert.AreEqual(mondayNov9, WeeklyChallenge.WeekStartUnix(week));
        }

        [Test]
        public void Weekly_challenge_is_the_same_for_everyone_and_rotates_industries()
        {
            var b = BotRunner.Content().Balance;
            var a = WeeklyChallenge.For(2860, b);
            var again = WeeklyChallenge.For(2860, b);
            Assert.AreEqual(a.Seed, again.Seed);
            Assert.AreEqual(a.IndustryId, again.IndustryId);
            Assert.AreEqual(1_000_000, a.StartCash.Cents, "$10,000");
            Assert.AreEqual(12, a.Months);
            var next = WeeklyChallenge.For(2861, b);
            Assert.AreNotEqual(a.Seed, next.Seed);
            Assert.AreNotEqual(a.IndustryId, next.IndustryId);
            var industries = Enumerable.Range(2860, b.Chapter2Industries.Count).Select(w => WeeklyChallenge.For(w, b).IndustryId).ToList();
            CollectionAssert.AreEquivalent(b.Chapter2Industries, industries, "every industry comes up in turn");
            Assert.AreEqual(3, WeeklyChallenge.AttemptsPerWeek);
            Assert.AreEqual(3600, WeeklyChallenge.GraceSeconds);
        }

        [Test]
        public void Duel_seed_comes_from_the_duel_id()
        {
            var a = Duels.For("duel-abc", "cleaning");
            Assert.AreEqual(a.Seed, Duels.For("duel-abc", "cleaning").Seed);
            Assert.AreNotEqual(a.Seed, Duels.For("duel-abd", "cleaning").Seed);
            Assert.AreEqual("cleaning", a.IndustryId);
            Assert.AreEqual(1_000_000, a.StartCash.Cents);
            Assert.AreEqual(6, a.Months);
            Assert.AreEqual(7, Duels.ExpiryDays);
        }

        [Test]
        public void Scenarios_dispatch_by_run_kind()
        {
            var b = BotRunner.Content().Balance;
            Assert.AreEqual(WeeklyChallenge.For(2860, b).Seed,
                Scenarios.For(new RunStart { Kind = RunStart.Challenge, ContextId = "2860" }, b).Seed);
            Assert.AreEqual(Duels.For("d1", "cleaning").Seed,
                Scenarios.For(new RunStart { Kind = RunStart.Duel, ContextId = "d1", IndustryId = "cleaning" }, b).Seed);
            Assert.IsNull(Scenarios.For(new RunStart { Kind = RunStart.Career }, b));
            Assert.IsNull(Scenarios.For(new RunStart { Kind = RunStart.Challenge, ContextId = "soon" }, b), "a bad week id");
            Assert.IsNull(Scenarios.For(new RunStart { Kind = RunStart.Duel, ContextId = "d1", IndustryId = "gig_delivery" }, b),
                "duels are played with a chapter-2 business");
        }

        [Test]
        public void Break_even_within_three_months_earns_the_badge_once()
        {
            var (_, s) = Stage2Content.Chapter2(0);
            var m = new MetaProgress();
            s.Facts.Set("industry_chosen", 5);
            s.Facts.Set("break_even", 8);
            CollectionAssert.AreEqual(new[] { Achievements.BreakEven3 }, Achievements.Update(m, s, new MonthReport()));
            Assert.IsEmpty(Achievements.Update(m, s, new MonthReport()), "once");
            Assert.AreEqual(1, m.Badges.Count(x => x == Achievements.BreakEven3));

            var (_, late) = Stage2Content.Chapter2(0);
            late.Facts.Set("industry_chosen", 5);
            late.Facts.Set("break_even", 9);
            Assert.IsEmpty(Achievements.Update(new MetaProgress(), late, new MonthReport()), "four months is too slow");
        }

        [Test]
        public void Twelve_clean_months_earn_the_badge_and_a_negative_month_resets()
        {
            var (_, s) = Stage2Content.Chapter2(100_000);
            var m = new MetaProgress();
            for (int i = 0; i < 3; i++) Assert.IsEmpty(Achievements.Update(m, s, new MonthReport()));
            Assert.AreEqual(0, m.CleanCashMonths, "the count starts with the first unit");

            s.Businesses.Single(b => b.IndustryId == "shop").Units = 1;
            for (int i = 0; i < 5; i++) Achievements.Update(m, s, new MonthReport());
            s.Cash = UnpaidCeo.Core.Primitives.Money.FromCents(-1);
            Achievements.Update(m, s, new MonthReport());
            Assert.AreEqual(0, m.CleanCashMonths, "a negative month restarts the count");
            s.Cash = UnpaidCeo.Core.Primitives.Money.FromCents(100_000);
            for (int i = 0; i < 11; i++) Assert.IsEmpty(Achievements.Update(m, s, new MonthReport()));
            CollectionAssert.AreEqual(new[] { Achievements.CleanCash12 }, Achievements.Update(m, s, new MonthReport()));
        }

        [Test]
        public void Granted_badges_are_kept_once()
        {
            var m = new MetaProgress();
            Assert.IsTrue(Achievements.Grant(m, Achievements.DuelWinner));
            Assert.IsFalse(Achievements.Grant(m, Achievements.DuelWinner));
            CollectionAssert.AreEqual(new[] { Achievements.DuelWinner }, m.Badges);
        }
    }
}
