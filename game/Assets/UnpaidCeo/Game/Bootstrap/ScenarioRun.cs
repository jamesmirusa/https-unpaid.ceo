#nullable enable
using System;
using System.Threading.Tasks;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Core.Persistence;
using UnpaidCeo.Game.Services;
using UnpaidCeo.Game.Services.Online;

namespace UnpaidCeo.Game.Bootstrap
{
    /// <summary>A challenge or duel run: its own GameState, started from a scenario, played for a fixed number of months.</summary>
    public sealed class ScenarioRun
    {
        public RunStart Start { get; set; } = new RunStart();
        public GameState State { get; set; } = null!;
        public int Months { get; set; }
        public int MonthsDone => Math.Max(0, State.Month - 1);
        public bool Finished => MonthsDone >= Months;
    }

    /// <summary>What finishing a run produced: the server's answer, or Queued when it waits for the internet.</summary>
    public sealed class RunOutcome
    {
        public bool Queued { get; set; }
        public SubmitResult? Result { get; set; }
        public DuelView? Duel { get; set; }
    }

    /// <summary>
    /// Plays challenge and duel runs beside the career: months use no daily turns and no autopilot, the run is kept in its
    /// own save slot (leave and come back later), and the finished run is submitted once through the queue. Runs are a
    /// business sprint: no monthly scenes, so every player of a scenario meets the same market.
    /// </summary>
    public sealed class RunController
    {
        sealed class Slot
        {
            public RunStart? Start { get; set; }
            public int Months { get; set; }
            public string? State { get; set; }
        }

        readonly GameEngine _engine;
        readonly SubmissionQueue _queue;
        readonly Func<IOnlineServices> _online;
        readonly string _contentVersion;

        public RunController(GameEngine engine, SubmissionQueue queue, Func<IOnlineServices> online, string contentVersion)
        {
            _engine = engine;
            _queue = queue;
            _online = online;
            _contentVersion = contentVersion;
        }

        public ScenarioRun? Current { get; private set; }

        /// <summary>The run waiting in the save slot, if any (for "Continue").</summary>
        public static ScenarioRun? Saved()
        {
            var slot = OnlineJson.Deserialize<Slot>(SaveService.LoadRun());
            if (slot?.Start == null || slot.State == null) return null;
            var loaded = SaveCodec.Decode(slot.State);
            return loaded.State == null || loaded.Status == LoadStatus.CorruptReset ? null
                : new ScenarioRun { Start = slot.Start, State = loaded.State, Months = slot.Months };
        }

        /// <summary>Continues the saved run when it is the same scenario; otherwise starts this one (replacing it).</summary>
        public ScenarioRun Begin(RunStart start)
        {
            var saved = Saved();
            if (saved != null && saved.Start.Kind == start.Kind && saved.Start.ContextId == start.ContextId && saved.Start.IndustryId == start.IndustryId)
                return Current = saved;
            var def = Scenarios.For(start, _engine.Content.Balance) ?? throw new ArgumentException("no scenario for this run start", nameof(start));
            Current = new ScenarioRun { Start = start, State = GameEngine.Start(_engine.Content, start, r => Scenarios.For(r, _engine.Content.Balance)), Months = def.Months };
            Save();
            return Current;
        }

        public MonthReport PlayMonth(MonthDecisions decisions)
        {
            if (Current == null) throw new InvalidOperationException("no run in progress");
            if (Current.Finished) throw new InvalidOperationException("the run is finished");
            var report = _engine.EndMonth(Current.State, decisions);
            Save();
            return report;
        }

        /// <summary>Back to the career; the run stays in its slot.</summary>
        public void Leave()
        {
            if (Current != null) Save();
            Current = null;
        }

        /// <summary>Saves the run after a change outside a month (a loan).</summary>
        public void Save()
        {
            if (Current == null) return;
            SaveService.SaveRun(OnlineJson.Serialize(new Slot { Start = Current.Start, Months = Current.Months, State = SaveCodec.Encode(Current.State) }));
        }

        /// <summary>Submits a finished run once and returns to the career. Null when there is no finished run.</summary>
        public async Task<RunOutcome?> Finish()
        {
            var run = Current;
            if (run == null || !run.Finished) return null;
            var submission = RunSubmission.From(run.Start, run.State, _contentVersion);
            var outcome = new RunOutcome { Queued = true };
            void OnResult(SubmissionQueue.Entry e, SubmitResult r) { if (e.Submission == submission) { outcome.Result = r; outcome.Queued = false; } }
            void OnDuel(SubmissionQueue.Entry e, DuelView v) { if (e.Submission == submission) { outcome.Duel = v; outcome.Queued = false; } }
            _queue.Submitted += OnResult;
            _queue.DuelSubmitted += OnDuel;
            try
            {
                var online = _online();
                string? player = online.Auth.PlayerId;
                if (run.Start.Kind == RunStart.Duel) _queue.EnqueueDuel(run.Start.ContextId ?? "", submission, player);
                else _queue.EnqueueChallenge(submission, player);
                SaveService.DeleteRun();
                Current = null;
                if (online.Available) await _queue.Flush(online.Runs, player);
            }
            finally
            {
                _queue.Submitted -= OnResult;
                _queue.DuelSubmitted -= OnDuel;
            }
            return outcome;
        }
    }
}
