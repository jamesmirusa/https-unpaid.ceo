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
    /// (each with what it pays and costs), "talk" for the host's next line and "leave". A plan choice is done with one
    /// tap: it goes into this week's plan (the same free action the Week screen lists), the host answers it, and its card
    /// stays done until next week; a choice that opens a screen (the plan, the report, lessons, a loan) leaves the room first.
    /// </summary>
    public sealed class VisitPanel
    {
        readonly UiRoot _ui;
        readonly GameEngine _engine;
        readonly GameState _state;
        readonly DecisionsScreen _plan;
        readonly VisualElement _portrait, _choices, _consequences;
        readonly Label _speaker, _place, _line, _energy, _points;
        Label _hearts;
        VisualElement _heartIcon;

        /// <summary>Shows the host's hearts (level 0–10; the cold icon below zero trust).</summary>
        public void RefreshHearts()
        {
            var place = CurrentPlace != null ? Places.Find(CurrentPlace) : null;
            if (place == null) return;
            int trust = _state.Npcs.Get(place.Host);
            _hearts.text = UnpaidCeo.Core.Narrative.Relationships.Level(trust).ToString();
            _heartIcon.style.backgroundImage = new StyleBackground(Kit.Icons.Load(Kit.Icons.GameFolder + (trust < 0 ? "trust_cold" : "trust")));
        }
        readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        readonly Dictionary<string, ChoiceCard> _cards = new Dictionary<string, ChoiceCard>();
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
            Root.AddToClassList(Kit.SafeArea.BottomClass);   // above the home bar on a phone
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
            // the host's hearts: trust ÷ 10, or "cold" below zero
            var heartsRow = new VisualElement { name = "visit-hearts-row" };
            heartsRow.AddToClassList("visit__hearts");
            _heartIcon = Kit.Icons.Game("trust", "visit__heart-icon");
            heartsRow.Add(_heartIcon);
            _hearts = UiText.ValueLabel("", "visit__hearts-value");
            _hearts.name = "visit-hearts";
            heartsRow.Add(_hearts);
            names.Add(heartsRow);
            head.Add(names);
            Root.Add(head);
            _line = new Label { name = "visit-line" };
            _line.AddToClassList("visit__line");
            Root.Add(_line);
            _consequences = new VisualElement { name = "visit-consequences" };
            _consequences.AddToClassList("visit__consequences");
            Root.Add(_consequences);
            // What the week still allows, New2NYC-style: the energy the plan leaves and the actions left.
            var budget = new VisualElement { name = "visit-budget" };
            budget.AddToClassList("visit__budget");
            budget.Add(UiText.Label("stat.energy", "visit__budget-caption"));
            _energy = UiText.ValueLabel("", "visit__budget-value");
            _energy.name = "visit-energy";
            budget.Add(_energy);
            budget.Add(UiText.Label("decisions.ap", "visit__budget-caption"));
            _points = UiText.ValueLabel("", "visit__budget-value");
            _points.name = "visit-points";
            budget.Add(_points);
            Root.Add(budget);
            _choices = new VisualElement { name = "visit-choices" };
            _choices.AddToClassList("visit__choices");
            Root.Add(_choices);
            var row = new VisualElement();
            row.AddToClassList("visit__row");
            var talk = UiText.Bind(new Button(Talk) { name = "visit-talk" }, "visit.talk");
            talk.AddToClassList("visit__talk");
            Kit.Icons.Decorate(talk, Kit.Icons.Service("chat-circle-dots"), "icon--lead");
            var exit = UiText.Bind(new Button(Close) { name = "visit-exit" }, "visit.exit");
            exit.AddToClassList("visit__exit");
            Kit.Icons.Decorate(exit, Kit.Icons.Service("door-open"), "icon--lead");
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
            _cards.Clear();
            foreach (var action in place.Actions)
            {
                if (_state.Chapter < action.MinChapter) continue;
                var a = action;
                Button b;
                if (a.Kind == PlaceActionKind.Plan && _engine.Content.FreeActions.TryGetValue(a.FreeAction, out var free))
                {
                    var card = new ChoiceCard("visit-action-" + a.Id, "visit-status-" + a.Id, "place.action." + a.Id, () => Choose(a)).WithIcon(Kit.GameIconFor.Action(a.FreeAction));
                    card.SetChips(free.Cost, free.Effects, _ui.Language);
                    card.Confirm = free.Confirm;
                    _cards[a.Id] = card;
                    b = card;
                }
                else
                {
                    b = new Button(() => Choose(a)) { name = "visit-action-" + a.Id };
                    b.AddToClassList("visit__choice");
                    b.Add(UiText.Label("place.action." + a.Id, "visit__choice-label"));
                }
                _buttons[a.Id] = b;
                _choices.Add(b);
            }
            RefreshChoices();
            RefreshHearts();
            Root.style.display = DisplayStyle.Flex;
            Root.BringToFront();
            _ui.SetVisiting(true);
        }

        /// <summary>The key of the line the host says now.</summary>
        public string LineKey { get; private set; }

        public void Say(string key) { LineKey = key; UiText.Bind(_line, key); }

        /// <summary>The room's action by id, as if its card was tapped (the radial menu on the host uses it).</summary>
        public void Choose(string actionId)
        {
            var a = CurrentPlace != null ? System.Array.Find(Places.Find(CurrentPlace).Actions, x => x.Id == actionId) : null;
            if (a != null) Choose(a);
        }

        void Choose(PlaceAction a)
        {
            switch (a.Kind)
            {
                case PlaceActionKind.Plan:
                    // One tap and it is done: no second tap, no undo (stage 5A).
                    if (!_plan.Plan(a.FreeAction)) break;
                    Say("place.react." + a.FreeAction);
                    if (_engine.Content.FreeActions.TryGetValue(a.FreeAction, out var free))
                    {
                        int n = EffectChips.Fill(_consequences, free.Effects, _ui.Language);
                        _consequences.style.display = n > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                    }
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

        public void Talk()
        {
            if (CurrentPlace == null) return;
            // stage 5B: the host's waiting scene (Core decides) or an everyday line by trust; others keep the place's lines
            if (!_ui.Talk(Places.Find(CurrentPlace).Host))
            {
                _lineIndex = (_lineIndex + 1) % Places.LinesPerPlace;
                Say($"place.{CurrentPlace}.line.{_lineIndex + 1}");
            }
            _consequences.style.display = DisplayStyle.None;
            Talked?.Invoke();
        }

        /// <summary>
        /// Each choice says whether the week can take it and, if not, why (New2NYC's greyed-out buttons with a reason);
        /// an open choice that answers the hero's need says it is recommended. The budget row shows the energy the
        /// plan leaves and the actions left.
        /// </summary>
        void RefreshChoices()
        {
            var place = CurrentPlace != null ? Places.Find(CurrentPlace) : null;
            if (place == null) return;
            foreach (var a in place.Actions)
            {
                if (a.Kind != PlaceActionKind.Plan || !_cards.TryGetValue(a.Id, out var card)) continue;
                var state = _plan.State(a.FreeAction);
                card.Show(state, state == ChoiceState.Open && _plan.Recommended(a.FreeAction), _plan.WeeksUntilOpen(a.FreeAction));
            }
            int now = _state.Life.Energy, after = _plan.EnergyAfterPlan;
            _energy.text = EnergyText(now, after);
            _points.text = $"{_plan.PointsLeft} / {_plan.PointsTotal}";
        }

        /// <summary>"70", or "70 → 60" while the plan changes it.</summary>
        public static string EnergyText(int now, int after) => after == now ? now.ToString() : $"{now} → {after}";

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
