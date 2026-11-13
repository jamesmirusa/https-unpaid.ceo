using System;
using System.Collections.Generic;
using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// What has to be done before "Next week" (stage 4): the day planned, a place in the city visited and, every second
    /// week, the new academy questions answered. Lives outside the replayed game (saved with MetaProgress), so
    /// the server's replay never sees it.
    /// </summary>
    public sealed class TurnGate
    {
        public const int StudyEvery = 2, DefaultAnswers = 3;
        public const string Plan = "plan", Place = "place", StudyItem = "study";

        readonly HashSet<string> _answered = new HashSet<string>();
        bool _plan, _place;

        /// <summary>The turn (week) this checklist is for.</summary>
        public int Turn { get; private set; }
        public bool PlanReviewed { get => _plan; set { if (_plan == value) return; _plan = value; Changed?.Invoke(); } }
        public bool PlaceVisited { get => _place; set { if (_place == value) return; _place = value; Changed?.Invoke(); } }
        /// <summary>New questions to answer this turn (0 = no study this week). Fixed when the turn starts.</summary>
        public int AnswersNeeded { get; set; }
        public int AnswersGiven => _answered.Count;
        public bool StudyRequired => AnswersNeeded > 0;
        public bool AllMet => PlanReviewed && PlaceVisited && AnswersGiven >= AnswersNeeded;
        public IReadOnlyCollection<string> Answered => _answered;

        public event Action Changed;

        public static bool IsStudyTurn(int turn) => turn % StudyEvery == 0;

        /// <summary>A new turn starts clean; the same turn again keeps what was done.</summary>
        public void StartTurn(int turn)
        {
            if (turn == Turn) return;
            Turn = turn;
            _plan = _place = false;
            _answered.Clear();
            Changed?.Invoke();
        }

        /// <summary>
        /// A new academy question answered this turn. Right or wrong counts: the game records one attempt per question
        /// and shows the right answer, so the question is spent either way (a right answer is what pays skill XP).
        /// </summary>
        public void RecordAnswer(string key)
        {
            if (!_answered.Add(key)) return;
            Changed?.Invoke();
        }

        public void CopyTo(MetaProgress meta)
        {
            meta.GateTurn = Turn;
            meta.GatePlan = PlanReviewed;
            meta.GatePlace = PlaceVisited;
            meta.GateAnswered = new List<string>(_answered);
        }

        /// <summary>Takes the saved month's progress over when it is this month's; an older record is left behind.</summary>
        public void Load(MetaProgress meta)
        {
            if (meta == null || meta.GateTurn != Turn) return;
            _plan = meta.GatePlan;
            _place = meta.GatePlace;
            _answered.Clear();
            if (meta.GateAnswered != null) foreach (var k in meta.GateAnswered) _answered.Add(k);
            Changed?.Invoke();
        }

        public static TurnGate From(MetaProgress meta)
        {
            var gate = new TurnGate();
            gate.StartTurn(meta.GateTurn);
            gate.Load(meta);
            return gate;
        }
    }
}
