using System;
using System.Collections.Generic;
using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// What has to be done before "Next month" (stage 4): the day planned, a place in the city visited and, every second
    /// month, the new academy questions answered right. Lives outside the replayed game (saved with MetaProgress), so
    /// the server's replay never sees it.
    /// </summary>
    public sealed class TurnGate
    {
        public const int StudyEvery = 2, DefaultAnswers = 3;
        public const string Plan = "plan", Place = "place", StudyItem = "study";

        readonly HashSet<string> _answered = new HashSet<string>();
        bool _plan, _place;

        public int Month { get; private set; }
        public bool PlanReviewed { get => _plan; set { if (_plan == value) return; _plan = value; Changed?.Invoke(); } }
        public bool PlaceVisited { get => _place; set { if (_place == value) return; _place = value; Changed?.Invoke(); } }
        /// <summary>New questions to answer this month (0 = no study this month). Fixed when the month starts.</summary>
        public int AnswersNeeded { get; set; }
        public int AnswersGiven => _answered.Count;
        public bool StudyRequired => AnswersNeeded > 0;
        public bool AllMet => PlanReviewed && PlaceVisited && AnswersGiven >= AnswersNeeded;
        public IReadOnlyCollection<string> Answered => _answered;

        public event Action Changed;

        public static bool IsStudyMonth(int month) => month % StudyEvery == 0;

        /// <summary>A new month starts clean; the same month again keeps what was done.</summary>
        public void StartMonth(int month)
        {
            if (month == Month) return;
            Month = month;
            _plan = _place = false;
            _answered.Clear();
            Changed?.Invoke();
        }

        /// <summary>
        /// A new academy question answered this month. Right or wrong counts: the game records one attempt per question
        /// and shows the right answer, so the question is spent either way (a right answer is what pays skill XP).
        /// </summary>
        public void RecordAnswer(string key)
        {
            if (!_answered.Add(key)) return;
            Changed?.Invoke();
        }

        public void CopyTo(MetaProgress meta)
        {
            meta.GateMonth = Month;
            meta.GatePlan = PlanReviewed;
            meta.GatePlace = PlaceVisited;
            meta.GateAnswered = new List<string>(_answered);
        }

        /// <summary>Takes the saved month's progress over when it is this month's; an older record is left behind.</summary>
        public void Load(MetaProgress meta)
        {
            if (meta == null || meta.GateMonth != Month) return;
            _plan = meta.GatePlan;
            _place = meta.GatePlace;
            _answered.Clear();
            if (meta.GateAnswered != null) foreach (var k in meta.GateAnswered) _answered.Add(k);
            Changed?.Invoke();
        }

        public static TurnGate From(MetaProgress meta)
        {
            var gate = new TurnGate();
            gate.StartMonth(meta.GateMonth);
            gate.Load(meta);
            return gate;
        }
    }
}
