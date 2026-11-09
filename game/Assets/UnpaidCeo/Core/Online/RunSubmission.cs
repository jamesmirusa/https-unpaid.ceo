using System.Collections.Generic;
using System.Linq;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Online
{
    /// <summary>What a client sends to be ranked: how the run began, its decision log, the hash it reached, its content.</summary>
    public sealed class RunSubmission
    {
        public RunStart Start { get; set; }
        public List<DecisionLogEntry> Log { get; set; } = new List<DecisionLogEntry>();
        public string StateHash { get; set; }
        public string ContentVersion { get; set; }

        public static RunSubmission From(RunStart start, GameState s, string contentVersion) => new RunSubmission
        {
            Start = start,
            Log = s.Log.ToList(),
            StateHash = Primitives.StateHash.Compute(s),
            ContentVersion = contentVersion,
        };
    }

    /// <summary>The server's answer to a submission.</summary>
    public sealed class SubmitResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public long NetWorthCents { get; set; }
        public string League { get; set; }
        public int Rank { get; set; } = -1;
        /// <summary>The career is further ahead than real time allows; only its first allowed months were ranked.</summary>
        public bool Paced { get; set; }
    }
}
