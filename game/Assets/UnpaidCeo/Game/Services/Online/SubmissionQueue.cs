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
    /// Runs waiting for the server, kept across launches (PlayerPrefs <see cref="PrefsKey"/>). Only the latest career
    /// submission is kept (it contains every earlier month); challenge and duel runs are each kept. Entries leave the
    /// queue once the server answers — accepted or refused — and stay while it is unreachable or answers "too_early".
    /// </summary>
    public sealed class SubmissionQueue
    {
        public const string PrefsKey = "unpaidceo.queue.v1";
        const string TooEarly = "too_early";

        public sealed class Entry
        {
            public string Kind { get; set; } = RunStart.Career;
            public string? DuelId { get; set; }
            public RunSubmission? Submission { get; set; }
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

        public void EnqueueCareer(RunSubmission s)
        {
            _entries.RemoveAll(e => e.Kind == RunStart.Career);
            _entries.Add(new Entry { Kind = RunStart.Career, Submission = s });
            Save();
        }

        public void EnqueueChallenge(RunSubmission s) { _entries.Add(new Entry { Kind = RunStart.Challenge, Submission = s }); Save(); }

        public void EnqueueDuel(string duelId, RunSubmission s) { _entries.Add(new Entry { Kind = RunStart.Duel, DuelId = duelId, Submission = s }); Save(); }

        /// <summary>Sends what is waiting, oldest first; stops at the first sign of being offline.</summary>
        public async Task Flush(IRunBackend backend)
        {
            if (_flushing) return;
            _flushing = true;
            try
            {
                foreach (var entry in _entries.ToList())
                {
                    string? error;
                    if (entry.Kind == RunStart.Duel)
                    {
                        var view = await backend.SubmitDuel(entry.DuelId ?? "", entry.Submission ?? new RunSubmission());
                        error = view.Error;
                        if (!Retry(error)) DuelSubmitted?.Invoke(entry, view);
                    }
                    else
                    {
                        var s = entry.Submission ?? new RunSubmission();
                        var result = entry.Kind == RunStart.Challenge ? await backend.SubmitChallenge(s) : await backend.SubmitCareer(s);
                        error = result.Ok ? null : result.Error;
                        if (!Retry(error)) Submitted?.Invoke(entry, result);
                    }
                    if (error == OnlineErrors.Offline) break;
                    if (!Retry(error)) { _entries.Remove(entry); Save(); }
                }
            }
            finally { _flushing = false; }
        }

        static bool Retry(string? error) => error == OnlineErrors.Offline || error == TooEarly;

        void Save()
        {
            PlayerPrefs.SetString(_key, OnlineJson.Serialize(_entries));
            PlayerPrefs.Save();
        }
    }
}
