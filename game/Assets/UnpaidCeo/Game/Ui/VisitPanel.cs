using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// The conversation inside a place: the host's portrait and name, what they say, the place's actions as choices
    /// (each with what it pays and costs), "talk" for the host's next line and "leave". A plan choice goes into this
    /// week's plan (the same free action the plan screen lists) and the host answers it; a choice that opens a screen
    /// (the plan, the report, lessons, a loan) leaves the room first.
    /// </summary>
    public sealed class VisitPanel
    {
        readonly UiRoot _ui;
        readonly GameEngine _engine;
        readonly GameState _state;
        readonly DecisionsScreen _plan;
        readonly VisualElement _portrait, _choices, _consequences;
        readonly Label _speaker, _place, _line;
        readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        int _lineIndex;

        public VisualElement Root { get; }
        /// <summary>The place being visited, or null.</summary>
        public string CurrentPlace { get; private set; }
        public bool IsOpen => CurrentPlace != null;

        /// <summary>A choice was made (its action id); the stage plays the host's answer.</summary>
        public event Action<string> ActionChosen;
        /// <summary>"Talk" was tapped: the host says their next line.</summary>
        public event Action Talked;
        /// <summary>The player left the room (leave, or a choice that opens another screen).</summary>
        public event Action Exited;

        public VisitPanel(UiRoot ui, GameEngine engine, GameState state, DecisionsScreen plan)
        {
            _ui = ui;
            _engine = engine;
            _state = state;
            _plan = plan;
            Root = new VisualElement { name = "visit" };
            Root.AddToClassList("visit");
            var head = new VisualElement();
            head.AddToClassList("visit__head");
            _portrait = new VisualElement { name = "visit-portrait" };
            _portrait.AddToClassList("visit__portrait");
            head.Add(_portrait);
            var names = new VisualElement();
            names.AddToClassList("visit__names");
            _speaker = new Label { name = "visit-speaker" };
            _speaker.AddToClassList("visit__speaker");
            _place = new Label { name = "visit-place" };
            _place.AddToClassList("visit__place");
            names.Add(_speaker);
            names.Add(_place);
            head.Add(names);
            Root.Add(head);
            _line = new Label { name = "visit-line" };
            _line.AddToClassList("visit__line");
            Root.Add(_line);
            _consequences = new VisualElement { name = "visit-consequences" };
            _consequences.AddToClassList("visit__consequences");
            Root.Add(_consequences);
            _choices = new VisualElement { name = "visit-choices" };
            _choices.AddToClassList("visit__choices");
            Root.Add(_choices);
            var row = new VisualElement();
            row.AddToClassList("visit__row");
            var talk = UiText.Bind(new Button(Talk) { name = "visit-talk" }, "visit.talk");
            talk.AddToClassList("visit__talk");
            var exit = UiText.Bind(new Button(Close) { name = "visit-exit" }, "visit.exit");
            exit.AddToClassList("visit__exit");
            row.Add(talk);
            row.Add(exit);
            Root.Add(row);
            Root.style.display = DisplayStyle.None;
            _plan.PlanChanged += RefreshChoices;
        }

        /// <summary>Walks into a place: the host greets with this week's line and the place's actions become choices.</summary>
        public void Show(string id)
        {
            var place = Places.Find(id) ?? throw new ArgumentException("unknown place " + id, nameof(id));
            CurrentPlace = id;
            UiText.Bind(_speaker, "npc." + place.Host);
            UiText.Bind(_place, $"place.{id}.title");
            _portrait.style.backgroundImage = StyleKeyword.None;
            PlacesScreen.LoadThumb(id, _portrait);
            _lineIndex = (Math.Max(1, _state.Month) - 1) % Places.LinesPerPlace;
            Say($"place.{id}.line.{_lineIndex + 1}");
            _consequences.Clear();
            _consequences.style.display = DisplayStyle.None;

            _choices.Clear();
            _buttons.Clear();
            foreach (var action in place.Actions)
            {
                if (_state.Chapter < action.MinChapter) continue;
                var a = action;
                var b = new Button(() => Choose(a)) { name = "visit-action-" + a.Id };
                b.AddToClassList("visit__choice");
                b.Add(UiText.Label("place.action." + a.Id, "visit__choice-label"));
                if (a.Kind == PlaceActionKind.Plan && _engine.Content.FreeActions.TryGetValue(a.FreeAction, out var free))
                {
                    var chips = new VisualElement();
                    chips.AddToClassList("visit__choice-chips");
                    if (free.Cost.Cents > 0) chips.Add(EffectChips.Chip(null, EffectChips.Minus + MoneyFormat.Compact(free.Cost, _ui.Language), false));
                    foreach (var c in EffectChips.Describe(free.Effects, _ui.Language)) chips.Add(EffectChips.Chip(c.CaptionKey, c.Value, c.Good));
                    b.Add(chips);
                }
                _buttons[a.Id] = b;
                _choices.Add(b);
            }
            RefreshChoices();
            Root.style.display = DisplayStyle.Flex;
            Root.BringToFront();
            _ui.SetVisiting(true);
        }

        void Say(string key) => UiText.Bind(_line, key);

        void Choose(PlaceAction a)
        {
            switch (a.Kind)
            {
                case PlaceActionKind.Plan:
                    bool planned = !_plan.IsPlanned(a.FreeAction);
                    _plan.SetPlanned(a.FreeAction, planned);
                    planned = _plan.IsPlanned(a.FreeAction);
                    Say(planned ? "place.react." + a.FreeAction : "place.react.unplanned");
                    if (planned && _engine.Content.FreeActions.TryGetValue(a.FreeAction, out var free))
                    {
                        int n = EffectChips.Fill(_consequences, free.Effects, _ui.Language);
                        _consequences.style.display = n > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                    else _consequences.style.display = DisplayStyle.None;
                    RefreshChoices();
                    ActionChosen?.Invoke(a.Id);
                    break;
                case PlaceActionKind.Open:
                    ActionChosen?.Invoke(a.Id);
                    Close();
                    _ui.Open(a.Tab);
                    break;
                case PlaceActionKind.Loan:
                    ActionChosen?.Invoke(a.Id);
                    Close();
                    _ui.Loan.Open(Money.Zero);
                    break;
            }
        }

        void Talk()
        {
            if (CurrentPlace == null) return;
            _lineIndex = (_lineIndex + 1) % Places.LinesPerPlace;
            Say($"place.{CurrentPlace}.line.{_lineIndex + 1}");
            _consequences.style.display = DisplayStyle.None;
            Talked?.Invoke();
        }

        void RefreshChoices()
        {
            var place = CurrentPlace != null ? Places.Find(CurrentPlace) : null;
            if (place == null) return;
            foreach (var a in place.Actions)
            {
                if (a.Kind != PlaceActionKind.Plan || !_buttons.TryGetValue(a.Id, out var b)) continue;
                b.EnableInClassList("visit__choice--planned", _plan.IsPlanned(a.FreeAction));
                b.SetEnabled(_plan.CanPlan(a.FreeAction));
            }
        }

        /// <summary>Leaves the room.</summary>
        public void Close()
        {
            if (CurrentPlace == null) return;
            CurrentPlace = null;
            Root.style.display = DisplayStyle.None;
            _ui.SetVisiting(false);
            Exited?.Invoke();
        }
    }
}
