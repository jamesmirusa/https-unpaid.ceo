using System;
using System.Linq;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Core.Online
{
    /// <summary>The verified facts of a run, or why it was refused.</summary>
    public sealed class VerifyResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public GameState State { get; set; }
        public long NetWorthCents { get; set; }
        /// <summary>Months completed (GameState.Month − 1).</summary>
        public int Months { get; set; }
        /// <summary>The chapter-2+ business, or null before one is chosen.</summary>
        public string IndustryId { get; set; }
        public long LastMonthRevenueCents { get; set; }
    }

    /// <summary>
    /// Server-side check of a submitted run with the game's own core: same content version, every logged action replays
    /// strictly, and the replayed state hashes to what the client reported. Only then do its numbers count.
    /// </summary>
    public static class RunVerifier
    {
        public static VerifyResult Verify(ContentDb c, string serverContentVersion, RunSubmission s, Func<RunStart, ScenarioDef> scenarioFor)
        {
            if (s == null || s.Start == null) return new VerifyResult { Error = "bad_submission" };
            if (s.ContentVersion != serverContentVersion) return new VerifyResult { Error = "content_version" };
            var r = GameEngine.ReplayStrict(c, s.Start, scenarioFor, s.Log);
            if (!r.Ok) return new VerifyResult { Error = $"replay:{r.FailedAt}:{r.Error}" };
            if (Primitives.StateHash.Compute(r.State) != s.StateHash) return new VerifyResult { Error = "hash_mismatch" };

            var business = r.State.Businesses.FirstOrDefault(b => c.Industries.TryGetValue(b.IndustryId, out var d) && d.Kind == IndustryKind.Business);
            long revenue = 0;
            if (business != null && r.LastReport != null && r.LastReport.BusinessResults.TryGetValue(business.Id, out var month))
                revenue = month.Revenue.Cents;
            return new VerifyResult
            {
                Ok = true,
                State = r.State,
                NetWorthCents = Chapters.NetWorth(r.State, c).Cents,
                Months = r.State.Month - 1,
                IndustryId = business?.IndustryId,
                LastMonthRevenueCents = revenue,
            };
        }
    }
}
