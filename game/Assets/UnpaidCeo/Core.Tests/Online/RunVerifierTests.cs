using NUnit.Framework;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Core.Primitives;
using static UnpaidCeo.Core.Tests.Online.OnlineRuns;

namespace UnpaidCeo.Core.Tests.Online
{
    /// <summary>The server's check of a submitted run: content version, strict replay, state hash (stage 2B, Task 2).</summary>
    public class RunVerifierTests
    {
        const string Version = "content-v1";

        [Test]
        public void Honest_run_verifies_with_net_worth_and_months()
        {
            var (e, s, _) = PlayCareer(31, 24);
            var sub = RunSubmission.From(Career(s), s, Version);
            var v = RunVerifier.Verify(e.Content, Version, sub, NoScenario);
            Assert.IsTrue(v.Ok, v.Error);
            Assert.AreEqual(Chapters.NetWorth(s, e.Content).Cents, v.NetWorthCents);
            Assert.AreEqual(s.Month - 1, v.Months, "months completed");
            Assert.AreEqual(StateHash.Compute(s), StateHash.Compute(v.State));
        }

        [Test]
        public void Edited_cash_fails_the_hash()
        {
            var (e, s, _) = PlayCareer(32, 6);
            var sub = RunSubmission.From(Career(s), s, Version);
            s.Cash += Money.FromCents(100_000_000);
            sub.StateHash = StateHash.Compute(s);   // a client that edits its save and re-hashes it
            var v = RunVerifier.Verify(e.Content, Version, sub, NoScenario);
            Assert.IsFalse(v.Ok);
            Assert.AreEqual("hash_mismatch", v.Error);
        }

        [Test]
        public void A_refused_action_names_its_index()
        {
            var (e, s, _) = PlayCareer(35, 4);
            var sub = RunSubmission.From(Career(s), s, Version);
            sub.Log.Insert(0, new DecisionLogEntry { Month = 1, Kind = DecisionLogEntry.AdvanceChapter });
            var v = RunVerifier.Verify(e.Content, Version, sub, NoScenario);
            Assert.AreEqual("replay:0:refused:advanceChapter", v.Error);
        }

        [Test]
        public void Other_content_version_is_refused()
        {
            var (e, s, _) = PlayCareer(33, 3);
            var v = RunVerifier.Verify(e.Content, "content-v2", RunSubmission.From(Career(s), s, Version), NoScenario);
            Assert.IsFalse(v.Ok);
            Assert.AreEqual("content_version", v.Error);
        }

        [Test]
        public void Revenue_of_the_last_month_is_reported()
        {
            var (e, s, last) = PlayCareer(34, 40);
            var v = RunVerifier.Verify(e.Content, Version, RunSubmission.From(Career(s), s, Version), NoScenario);
            Assert.IsTrue(v.Ok, v.Error);
            Assert.AreEqual("coffee_cart", v.IndustryId);
            Assert.IsTrue(last.BusinessResults.ContainsKey("coffee_cart"), "the cart is open by month 40");
            Assert.AreEqual(last.BusinessResults["coffee_cart"].Revenue.Cents, v.LastMonthRevenueCents);
            Assert.Greater(v.LastMonthRevenueCents, 0);
        }

        [Test]
        public void Content_version_is_stable_and_order_sensitive()
        {
            string a = ContentVersion.Of(new[] { "{\"a\":1,\"b\":[2,3]}", "[1]" });
            Assert.AreEqual(a, ContentVersion.Of(new[] { "{ \"b\": [2, 3], \"a\": 1 }", "[1]" }), "formatting and key order do not matter");
            Assert.AreNotEqual(a, ContentVersion.Of(new[] { "[1]", "{\"a\":1,\"b\":[2,3]}" }), "file order does");
            Assert.AreNotEqual(a, ContentVersion.Of(new[] { "{\"a\":1,\"b\":[2,4]}", "[1]" }));
            Assert.AreEqual(64, a.Length);
        }
    }
}
