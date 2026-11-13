#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnpaidCeo.Core.Online;

namespace UnpaidCeo.Game.Services.Online
{
    /// <summary>
    /// Runs waiting for the server, kept across launches (PlayerPrefs <see cref="PrefsKey"/>), each with the player who
    /// played it. Only the latest career submission is kept (it contains every earlier month); challenge and duel runs
    /// are each kept. Entries leave the queue once the server answers — accepted or refused — or after
    /// <see cref="MaxFailures"/> server failures, and stay while it is unreachable or answers "too_early".
    /// </summary>
    public sealed class SubmissionQueue
    {
        public const string PrefsKey = "unpaidceo.queue.v1";
        /// <summary>Server failures on one entry before it is given up.</summary>
        public const int MaxFailures = 5;
        const string TooEarly = "too_early";

        public sealed class Entry
        {
            public string Kind { get; set; } = RunStart.Career;
            public string? DuelId { get; set; }
            public RunSubmission? Submission { get; set; }
            /// <summary>The player who played it; null when nobody was signed in (any player of this device may send it).</summary>
            public string? PlayerId { get; set; }
            public int Failures { get; set; }
        }

        readonly string _key;
        readonly List<Entry> _entries;
        bool _flushing;

        public SubmissionQueue(string prefsKey = PrefsKey)
        {
            _key = prefsKey;
            _entries = OnlineJson.Deserialize<List<Entry>>(PlayerPrefs.GetString(_key, null)) ?? new List<Entry>();
        }

        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>A career or challenge run the server answered (accepted or refused).</summary>
        public event Action<Entry, SubmitResult>? Submitted;
        public event Action<Entry, DuelView>? DuelSubmitted;

        public void EnqueueCareer(RunSubmission s, string? playerId)
        {
            _entries.RemoveAll(e => e.Kind == RunStart.Career);
            _entries.Add(new Entry { Kind = RunStart.Career, Submission = s, PlayerId = playerId });
            Save();
        }

        public void EnqueueChallenge(RunSubmission s, string? playerId)
        {
            _entries.Add(new Entry { Kind = RunStart.Challenge, Submission = s, PlayerId = playerId });
            Save();
        }

        public void EnqueueDuel(string duelId, RunSubmission s, string? playerId)
        {
            _entries.Add(new Entry { Kind = RunStart.Duel, DuelId = duelId, Submission = s, PlayerId = playerId });
            Save();
        }

        /// <summary>
        /// Sends what <paramref name="playerId"/> may send (their runs and runs played signed out), oldest first. Stops at the
        /// first sign of being offline; an entry the server fails on is tried again on later flushes, up to
        /// <see cref="MaxFailures"/> times, and never holds back the entries behind it.
        /// </summary>
        public async Task Flush(IRunBackend backend, string? playerId)
        {
            if (_flushing) return;
            _flushing = true;
            try
            {
                foreach (var entry in _entries.ToList())
                {
                    if (entry.PlayerId != null && entry.PlayerId != playerId) continue;
                    string? error;
                    DuelView? view = null;
                    SubmitResult? result = null;
                    var s = entry.Submission ?? new RunSubmission();
                    if (entry.Kind == RunStart.Duel)
                    {
                        view = await backend.SubmitDuel(entry.DuelId ?? "", s);
                        error = view.Error;
                    }
                    else
                    {
                        result = entry.Kind == RunStart.Challenge ? await backend.SubmitChallenge(s) : await backend.SubmitCareer(s);
                        error = result.Ok ? null : result.Error;
                    }
                    if (error == OnlineErrors.Offline) break;
                    if (error == TooEarly) continue;
                    if (error == OnlineErrors.ServerError && ++entry.Failures < MaxFailures) { Save(); continue; }

                    _entries.Remove(entry);
                    Save();
                    if (view != null) DuelSubmitted?.Invoke(entry, view);
                    if (result != null) Submitted?.Invoke(entry, result);
                }
            }
            finally { _flushing = false; }
        }

        void Save()
        {
            PlayerPrefs.SetString(_key, OnlineJson.Serialize(_entries));
            PlayerPrefs.Save();
        }
    }
}
