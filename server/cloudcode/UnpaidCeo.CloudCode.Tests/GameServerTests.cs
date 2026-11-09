using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Core.Tests.Game;
using UnpaidCeo.Core.Tests.Online;
using Xunit;
using static UnpaidCeo.CloudCode.Tests.ServerRuns;

namespace UnpaidCeo.CloudCode.Tests
{
    /// <summary>The Cloud Code rules (stage 2B, Task 4): nothing reaches a leaderboard unless the core replays it.</summary>
    public class GameServerTests
    {
        const int Week = 2950;
        static readonly long Wednesday = WeeklyChallenge.WeekStartUnix(Week) + 2 * Day + 12 * 3600;

        [Fact]
        public async Task Hello_records_first_seen_once_and_names_this_weeks_challenge()
        {
            var h = new Harness(Wednesday);
            var hello = await h.Server.Hello("p1");
            Assert.Equal(Wednesday, hello.ServerUnix);
            Assert.Equal(Week, hello.Week);
            Assert.Equal(WeeklyChallenge.For(Week, ServerContent.Db.Balance).IndustryId, hello.Challenge.IndustryId);
            h.Clock.NowUnix += Day;
            await h.Server.Hello("p1");
            Assert.Equal(Wednesday.ToString(), await h.Players.Get("p1", "first_seen"));
        }

        [Fact]
        public async Task Honest_career_writes_capital_and_market_boards()
        {
            var h = new Harness(Wednesday);
            await h.Server.Hello("p1");
            h.Clock.NowUnix += 7 * Day;   // a week of play, plus a day of slack, allows 2 + 8 × 9 = 74 months
            var (e, s, last) = OnlineRuns.PlayCareer(41, 40);

            var r = await h.Server.SubmitCareer("p1", Career(s));

            Assert.True(r.Ok, r.Error);
            Assert.False(r.Paced);
            long netWorth = Chapters.NetWorth(s, e.Content).Cents;
            Assert.Equal(netWorth, r.NetWorthCents);
            Assert.Equal(Leagues.For(netWorth), r.League);
            Assert.Equal(1, r.Rank);
            Assert.Equal(netWorth / 100.0, h.Boards.Score("capital", "p1"));
            Assert.Equal(last.BusinessResults["coffee_cart"].Revenue.Cents / 100.0, h.Boards.Score("market_coffee_cart", "p1"));
        }

        [Fact]
        public async Task Tampered_runs_are_rejected_and_boards_untouched()
        {
            var h = new Harness(Wednesday);
            await h.Server.Hello("p1");
            h.Clock.NowUnix += 7 * Day;
            var (_, s, _) = OnlineRuns.PlayCareer(42, 12);

            var editedCash = Career(s);
            s.Cash += Money.FromCents(100_000_000);
            editedCash.StateHash = StateHash.Compute(s);   // a client that edits its save and re-hashes it
            s.Cash -= Money.FromCents(100_000_000);
            var failingAction = Career(s);
            failingAction.Log.Insert(0, new DecisionLogEntry { Month = 1, Kind = DecisionLogEntry.AdvanceChapter });
            var wrongHash = Career(s);
            wrongHash.StateHash = new string('0', 64);
            var otherContent = Career(s);
            otherContent.ContentVersion = "other";
            var notACareer = Career(s);
            notACareer.Start.Kind = RunStart.Challenge;

            var cases = new (string Name, RunSubmission Sub, string Error)[]
            {
                ("edited cash", editedCash, "hash_mismatch"),
                ("failing action", failingAction, "replay:0:refused:advanceChapter"),
                ("wrong hash", wrongHash, "hash_mismatch"),
                ("other content version", otherContent, "content_version"),
                ("challenge sent as a career", notACareer, "wrong_kind"),
            };
            foreach (var c in cases)
            {
                var r = await h.Server.SubmitCareer("p1", c.Sub);
                Assert.False(r.Ok, c.Name);
                Assert.Equal(c.Error, r.Error);
            }
            Assert.Empty(h.Boards.Submissions);
        }

        [Fact]
        public async Task Careers_ahead_of_real_time_rank_their_paced_months()
        {
            var h = new Harness(Wednesday);   // first seen just now; a day of slack allows 2 + 8 × 2 = 18 months
            var (e, s, _) = OnlineRuns.PlayCareer(41, 40);
            var (_, s18, _) = OnlineRuns.PlayCareer(41, 18);   // the same career, stopped where real time allows
            long paced = Chapters.NetWorth(s18, e.Content).Cents;
            Assert.NotEqual(Chapters.NetWorth(s, e.Content).Cents, paced);

            var r = await h.Server.SubmitCareer("p1", Career(s));

            Assert.True(r.Ok, r.Error);
            Assert.True(r.Paced);
            Assert.Equal(paced, r.NetWorthCents);
            Assert.Equal(Leagues.For(paced), r.League);
            Assert.Equal(paced / 100.0, h.Boards.Score("capital", "p1"));
            Assert.Equal(new[] { "capital" }, h.Boards.Submissions.Select(x => x.Board).ToArray());   // no market share ahead of time
        }

        [Fact]
        public async Task Challenge_counts_three_attempts_a_week_and_keeps_the_best()
        {
            var h = new Harness(Wednesday);
            var start = ChallengeStart(Week);
            var casual = PlayScenario(start, 12, new CasualBot());
            var optimal = PlayScenario(start, 12, new OptimalBot());
            Assert.NotEqual(casual.NetWorthCents, optimal.NetWorthCents);

            var tampered = PlayScenario(start, 12, new CasualBot()).Sub;
            tampered.StateHash = new string('0', 64);
            Assert.Equal("hash_mismatch", (await h.Server.SubmitChallenge("p1", tampered)).Error);
            Assert.Equal("not_finished", (await h.Server.SubmitChallenge("p1", PlayScenario(start, 6, new CasualBot()).Sub)).Error);

            Assert.True((await h.Server.SubmitChallenge("p1", casual.Sub)).Ok, "attempt 1");
            var second = await h.Server.SubmitChallenge("p1", optimal.Sub);
            Assert.True(second.Ok, second.Error);
            Assert.Equal(optimal.NetWorthCents, second.NetWorthCents);
            Assert.True((await h.Server.SubmitChallenge("p1", PlayScenario(start, 12, new MixedBot()).Sub)).Ok, "attempt 3");
            Assert.Equal("attempts_used", (await h.Server.SubmitChallenge("p1", PlayScenario(start, 12, new CasualBot(), withLoan: true).Sub)).Error);

            Assert.Equal(3, (await h.Server.Hello("p1")).ChallengeAttemptsUsed);
            Assert.Equal(0, (await h.Server.Hello("p2")).ChallengeAttemptsUsed);
            Assert.Equal(System.Math.Max(casual.NetWorthCents, optimal.NetWorthCents) / 100.0, h.Boards.Score("weekly_challenge", "p1"));
            Assert.Equal(3, h.Boards.Submissions.Count);
        }

        [Fact]
        public async Task Scenario_runs_accept_only_month_plans_and_loans()
        {
            var h = new Harness(Wednesday);
            var honest = PlayScenario(ChallengeStart(Week), 12, new OptimalBot()).Sub;
            var actions = new (string Kind, string Payload)[]
            {
                (DecisionLogEntry.BeginMonth, null),   // a modified client rolling scenes and keeping the lucky ones
                (DecisionLogEntry.StoryletChoice, "{\"storylet\":\"x\",\"choice\":\"y\"}"),
                (DecisionLogEntry.QuizAnswer, "{\"lesson\":\"x\",\"question\":0,\"option\":0}"),
                (DecisionLogEntry.TutorialGig, null),
                (DecisionLogEntry.ChooseIndustry, "{\"industry\":\"food_truck\"}"),
                (DecisionLogEntry.AdvanceChapter, null),
            };
            foreach (var a in actions)
            {
                var sub = OnlineJson.Deserialize<RunSubmission>(OnlineJson.Serialize(honest));
                sub.Log.Insert(0, new DecisionLogEntry { Month = 1, Kind = a.Kind, Payload = a.Payload });
                Assert.Equal("replay:0:refused:" + a.Kind, (await h.Server.SubmitChallenge("p1", sub)).Error);
            }
            Assert.Empty(h.Boards.Submissions);
            Assert.True((await h.Server.SubmitChallenge("p1", honest)).Ok);
        }

        [Fact]
        public async Task A_resent_challenge_run_counts_once()
        {
            var h = new Harness(Wednesday);
            var start = ChallengeStart(Week);
            var runs = new[] { new OptimalBot(), new CasualBot(), (IBot)new MixedBot() }
                .Select(bot => PlayScenario(start, 12, bot).Sub).ToArray();

            var first = await h.Server.SubmitChallenge("p1", runs[0]);
            var resent = await h.Server.SubmitChallenge("p1", runs[0]);   // the answer was lost and the queue sent it again
            Assert.True(first.Ok, first.Error);
            Assert.True(resent.Ok, resent.Error);
            Assert.Equal(first.NetWorthCents, resent.NetWorthCents);
            Assert.Equal(1, (await h.Server.Hello("p1")).ChallengeAttemptsUsed);

            Assert.True((await h.Server.SubmitChallenge("p1", runs[1])).Ok);
            Assert.True((await h.Server.SubmitChallenge("p1", runs[2])).Ok);
            var lastResent = await h.Server.SubmitChallenge("p1", runs[2]);
            Assert.True(lastResent.Ok, lastResent.Error);   // the third attempt resent is still that attempt, not a fourth
            Assert.Equal(3, (await h.Server.Hello("p1")).ChallengeAttemptsUsed);
        }

        [Fact]
        public async Task A_failed_board_write_does_not_spend_an_attempt()
        {
            var h = new Harness(Wednesday);
            var run = PlayScenario(ChallengeStart(Week), 12, new OptimalBot()).Sub;

            h.Boards.FailNext = true;
            await Assert.ThrowsAnyAsync<System.Exception>(() => h.Server.SubmitChallenge("p1", run));
            Assert.Equal(0, (await h.Server.Hello("p1")).ChallengeAttemptsUsed);

            var retried = await h.Server.SubmitChallenge("p1", run);
            Assert.True(retried.Ok, retried.Error);
            Assert.Equal(1, (await h.Server.Hello("p1")).ChallengeAttemptsUsed);
        }

        [Fact]
        public async Task Duel_creation_is_capped_per_day()
        {
            var h = new Harness(Wednesday);
            for (int i = 0; i < 10; i++)
                Assert.Null((await h.Server.CreateDuel("a", "b" + i, "coffee_cart")).Error);
            Assert.Equal("duel_limit", (await h.Server.CreateDuel("a", "z", "coffee_cart")).Error);
            Assert.Empty(await h.Server.ListDuels("z"));
            Assert.Null((await h.Server.CreateDuel("b0", "a", "coffee_cart")).Error);   // a's cap is a's alone

            h.Clock.NowUnix += Day;
            Assert.Null((await h.Server.CreateDuel("a", "z", "coffee_cart")).Error);

            Assert.Equal("bad_opponent", (await h.Server.CreateDuel("a", "../duels", "coffee_cart")).Error);
            Assert.Equal("bad_opponent", (await h.Server.CreateDuel("a", new string('x', 65), "coffee_cart")).Error);
        }

        [Fact]
        public async Task Challenge_finished_after_the_reset_scores_within_grace_only()
        {
            long monday = WeeklyChallenge.WeekStartUnix(Week + 1);
            var h = new Harness(monday + 30 * 60);
            var lastWeek = PlayScenario(ChallengeStart(Week), 12, new OptimalBot());
            var thisWeek = PlayScenario(ChallengeStart(Week + 1), 12, new OptimalBot());

            var late = await h.Server.SubmitChallenge("p1", lastWeek.Sub);
            Assert.True(late.Ok, late.Error);
            Assert.Equal(lastWeek.NetWorthCents / 100.0, h.Boards.Score("weekly_challenge", "p1"));
            Assert.Equal("too_early", (await h.Server.SubmitChallenge("p2", thisWeek.Sub)).Error);

            h.Clock.NowUnix = monday + 2 * 3600;
            Assert.Equal("week_ended", (await h.Server.SubmitChallenge("p3", lastWeek.Sub)).Error);
            Assert.True((await h.Server.SubmitChallenge("p2", thisWeek.Sub)).Ok, "this week's run after the reset");
            Assert.True(double.IsNaN(h.Boards.Score("weekly_challenge", "p3")));
        }

        [Fact]
        public async Task Duel_resolves_when_both_finished()
        {
            var h = new Harness(Wednesday);
            var duel = await h.Server.CreateDuel("a", "b", "coffee_cart");
            Assert.Null(duel.Error);
            Assert.Equal("b", duel.Opponent);
            Assert.Equal(DuelView.WaitingMe, duel.State);

            var strong = PlayScenario(DuelStart(duel.Id, "coffee_cart"), 6, new OptimalBot());
            var weak = PlayScenario(DuelStart(duel.Id, "coffee_cart"), 6, new CasualBot());
            Assert.NotEqual(strong.NetWorthCents, weak.NetWorthCents);

            var afterA = await h.Server.SubmitDuel("a", duel.Id, strong.Sub);
            Assert.Null(afterA.Error);
            Assert.Equal(DuelView.WaitingThem, afterA.State);
            Assert.Equal(DuelView.WaitingMe, (await h.Server.ListDuels("b")).Single().State);

            var afterB = await h.Server.SubmitDuel("b", duel.Id, weak.Sub);
            bool aWins = strong.NetWorthCents > weak.NetWorthCents;
            Assert.Equal(aWins ? DuelView.Lost : DuelView.Won, afterB.State);
            var forA = (await h.Server.ListDuels("a")).Single();
            Assert.Equal(aWins ? DuelView.Won : DuelView.Lost, forA.State);
            Assert.Equal(strong.NetWorthCents, forA.MyNetWorthCents);
            Assert.Equal(weak.NetWorthCents, forA.TheirNetWorthCents);

            h.Clock.NowUnix += 60;
            var rematch = await h.Server.CreateDuel("b", "a", "food_truck");
            var same = PlayScenario(DuelStart(rematch.Id, "food_truck"), 6, new OptimalBot());
            await h.Server.SubmitDuel("b", rematch.Id, same.Sub);
            Assert.Equal(DuelView.Draw, (await h.Server.SubmitDuel("a", rematch.Id, same.Sub)).State);
            var list = await h.Server.ListDuels("a");
            Assert.Equal(new[] { rematch.Id, duel.Id }, list.Select(d => d.Id).ToArray());   // newest first
        }

        [Fact]
        public async Task Duel_expires_after_seven_days()
        {
            var h = new Harness(Wednesday);
            var duel = await h.Server.CreateDuel("a", "b", "coffee_cart");
            var run = PlayScenario(DuelStart(duel.Id, "coffee_cart"), 6, new OptimalBot());
            Assert.Equal(DuelView.WaitingThem, (await h.Server.SubmitDuel("a", duel.Id, run.Sub)).State);

            h.Clock.NowUnix += 7 * Day + 1;
            var late = await h.Server.SubmitDuel("b", duel.Id, run.Sub);
            Assert.Equal("duel_expired", late.Error);
            Assert.Equal(DuelView.Expired, (await h.Server.ListDuels("a")).Single().State);
            Assert.Equal(DuelView.Expired, (await h.Server.ListDuels("b")).Single().State);
        }

        [Fact]
        public async Task Only_the_two_players_can_submit_to_a_duel()
        {
            var h = new Harness(Wednesday);
            var duel = await h.Server.CreateDuel("a", "b", "coffee_cart");
            var run = PlayScenario(DuelStart(duel.Id, "coffee_cart"), 6, new OptimalBot());

            Assert.Equal("not_in_duel", (await h.Server.SubmitDuel("c", duel.Id, run.Sub)).Error);
            Assert.Equal("not_found", (await h.Server.SubmitDuel("a", "no-such-duel", run.Sub)).Error);
            Assert.Empty(await h.Server.ListDuels("c"));
            Assert.Equal(DuelView.WaitingMe, (await h.Server.ListDuels("a")).Single().State);
        }

        [Fact]
        public async Task Duel_refuses_bad_creates_and_mismatched_runs()
        {
            var h = new Harness(Wednesday);
            Assert.Equal("bad_opponent", (await h.Server.CreateDuel("a", "a", "coffee_cart")).Error);
            Assert.Equal("bad_opponent", (await h.Server.CreateDuel("a", "", "coffee_cart")).Error);
            Assert.Equal("bad_industry", (await h.Server.CreateDuel("a", "b", "gig_delivery")).Error);

            var duel = await h.Server.CreateDuel("a", "b", "coffee_cart");
            var other = await h.Server.CreateDuel("a", "b", "coffee_cart");
            var forOther = PlayScenario(DuelStart(other.Id, "coffee_cart"), 6, new OptimalBot());
            Assert.Equal("wrong_kind", (await h.Server.SubmitDuel("a", duel.Id, forOther.Sub)).Error);
            Assert.Equal("not_finished", (await h.Server.SubmitDuel("a", duel.Id, PlayScenario(DuelStart(duel.Id, "coffee_cart"), 3, new OptimalBot()).Sub)).Error);

            var run = PlayScenario(DuelStart(duel.Id, "coffee_cart"), 6, new OptimalBot());
            Assert.Null((await h.Server.SubmitDuel("a", duel.Id, run.Sub)).Error);
            Assert.Equal("already_submitted", (await h.Server.SubmitDuel("a", duel.Id, run.Sub)).Error);
        }

        [Fact]
        public async Task A_submission_sent_as_json_still_verifies()
        {
            var h = new Harness(Wednesday);
            await h.Server.Hello("p1");
            h.Clock.NowUnix += 7 * Day;
            var (_, s, _) = OnlineRuns.PlayCareer(43, 24);
            var wire = OnlineJson.Serialize(Career(s));

            var r = await h.Server.SubmitCareer("p1", OnlineJson.Deserialize<RunSubmission>(wire));

            Assert.True(r.Ok, r.Error);
            var back = OnlineJson.Deserialize<SubmitResult>(OnlineJson.Serialize(r));
            Assert.Equal(r.NetWorthCents, back.NetWorthCents);
            Assert.Equal(r.League, back.League);
        }

        [Fact]
        public async Task Unreadable_submissions_are_refused()
        {
            var h = new Harness(Wednesday);
            Assert.Null(OnlineJson.Deserialize<RunSubmission>("{not json"));
            Assert.Equal("bad_submission", (await h.Server.SubmitCareer("p1", null)).Error);
            Assert.Equal("bad_submission", (await h.Server.SubmitChallenge("p1", null)).Error);
            var duel = await h.Server.CreateDuel("a", "b", "coffee_cart");
            Assert.Equal("bad_submission", (await h.Server.SubmitDuel("a", duel.Id, null)).Error);
            Assert.Empty(h.Boards.Submissions);
        }

        [Fact]
        public void Server_and_client_content_versions_match()
        {
            var dir = Path.Combine(UnityEngine.Application.dataPath, "UnpaidCeo", "Content");
            var clientTexts = ContentPack.Files.Select(f => File.ReadAllText(Path.Combine(dir, f + ".json"))).ToList();
            Assert.Equal(ContentVersion.Of(clientTexts), ServerContent.Version);
            Assert.Equal(BotRunner.Content().Industries.Count, ServerContent.Db.Industries.Count);
            Assert.Equal(64, ServerContent.Version.Length);
        }
    }
}
