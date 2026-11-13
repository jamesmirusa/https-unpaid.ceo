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
        /// <summary>The run has more months than the pace limit allowed.</summary>
        public bool Paced { get; set; }
        /// <summary>Net worth at the start of the first month past the pace limit (equals NetWorthCents when not paced).</summary>
        public long PacedNetWorthCents { get; set; }
    }

    /// <summary>
    /// Server-side check of a submitted run with the game's own core: same content version, every logged action replays
    /// strictly, and the replayed state hashes to what the client reported. Only then do its numbers count.
    /// </summary>
    public static class RunVerifier
    {
        /// <param name="paceLimit">
        /// Turns (weeks) real time allows. The whole log is still verified, but a longer run also reports its net worth as it
        /// stood when turn <paramref name="paceLimit"/> + 1 began (before that turn's scenes or plan), so it can rank that far.
        /// </param>
        public static VerifyResult Verify(ContentDb c, string serverContentVersion, RunSubmission s, Func<RunStart, ScenarioDef> scenarioFor,
            int paceLimit = int.MaxValue)
        {
            if (s == null || s.Start == null) return new VerifyResult { Error = "bad_submission" };
            if (s.ContentVersion != serverContentVersion) return new VerifyResult { Error = "content_version" };
            long? atLimit = null;
            var r = GameEngine.ReplayStrict(c, s.Start, scenarioFor, s.Log, (state, entry) =>
            {
                // The limit counts turns (weeks since stage 4B; a legacy month is four of them).
                if (atLimit == null && state.Week > paceLimit
                    && (entry.Kind == DecisionLogEntry.BeginMonth || entry.Kind == DecisionLogEntry.EndMonth
                        || entry.Kind == DecisionLogEntry.BeginWeek || entry.Kind == DecisionLogEntry.EndWeek))
                    atLimit = Chapters.NetWorth(state, c).Cents;
            });
            if (!r.Ok) return new VerifyResult { Error = $"replay:{r.FailedAt}:{r.Error}" };
            if (Primitives.StateHash.Compute(r.State) != s.StateHash) return new VerifyResult { Error = "hash_mismatch" };

            var business = r.State.Businesses.FirstOrDefault(b => c.Industries.TryGetValue(b.IndustryId, out var d) && d.Kind == IndustryKind.Business);
            long revenue = 0;
            if (business != null && r.LastReport != null && r.LastReport.BusinessResults.TryGetValue(business.Id, out var month))
                revenue = r.LastReport.IsWeek ? month.Revenue.Cents * GameState.WeeksPerMonth : month.Revenue.Cents;   // a monthly rate either way
            long netWorth = Chapters.NetWorth(r.State, c).Cents;
            bool paced = r.State.Week - 1 > paceLimit;
            return new VerifyResult
            {
                Ok = true,
                State = r.State,
                NetWorthCents = netWorth,
                Months = r.State.Month - 1,
                IndustryId = business?.IndustryId,
                LastMonthRevenueCents = revenue,
                Paced = paced,
                PacedNetWorthCents = paced ? atLimit.Value : netWorth,
            };
        }
    }
}
