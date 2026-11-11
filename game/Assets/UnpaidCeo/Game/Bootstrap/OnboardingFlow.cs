#nullable enable
using System;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Game.Hero;
using UnpaidCeo.Game.Ui;

using UnpaidCeo.Game.Services.Online;

namespace UnpaidCeo.Game.Bootstrap
{
    /// <summary>
    /// The first 60 seconds of a new game, as coach marks over the real UI: pick a hero (one tap, a random
    /// preset is ready), the mentor offers a $40 delivery gig, explains revenue vs profit, highlights
    /// Next month, and the month 1 result pops up on the main screen. Mentor lines come from Yarn (ch1_onboarding.yarn) so they
    /// are localised and voiced like the rest of the chapter.
    /// </summary>
    public sealed class OnboardingFlow
    {
        public enum Step { Hero, Gig, Lesson, NextMonth, Done }

        public const string ActionButton = "coach-action";
        public const string HighlightClass = "coach-target";
        public const string Speaker = "Unpaid CEO";
        public const string LineGig = "line:ch1_onboarding_01";
        public const string LineRevenue = "line:ch1_onboarding_02";
        public const string LineNextMonth = "line:ch1_onboarding_03";

        readonly GameEngine _engine;
        readonly GameState _state;
        readonly UiRoot _ui;
        readonly HeroView _view;
        readonly Func<string, string?> _lineText;
        readonly int _seed;

        VisualElement? _heroOverlay;
        VisualElement? _coach;
        Label? _text;
        Button? _action;
        Button? _next;

        public Step Current { get; private set; } = Step.Hero;
        public MonthReport? Report { get; private set; }

        /// <summary>A mentor line appeared (Yarn line id) — the bootstrap plays its voice clip.</summary>
        public event Action<string>? LineShown;
        public event Action<HeroAppearance>? HeroChosen;
        public event Action<MonthReport>? Completed;

        public OnboardingFlow(GameEngine engine, GameState state, UiRoot ui, HeroView view, Func<string, string?> lineText, int seed)
        {
            _engine = engine;
            _state = state;
            _ui = ui;
            _view = view;
            _lineText = lineText;
            _seed = seed;
        }

        public void Start()
        {
            _ui.GuideEnabled = false;   // one voice at a time: the mentor's coach marks first
            _heroOverlay = new VisualElement { name = "onboarding-hero" };
            _heroOverlay.AddToClassList("onboarding-overlay");
            _ui.Root.Add(_heroOverlay);
            var creator = new HeroCreator(_heroOverlay, _view, _seed);
            creator.Confirmed += OnHeroChosen;
            AnalyticsEvents.TutorialStep(_ui.Online.Analytics, (int)Step.Hero);
        }

        void OnHeroChosen(HeroAppearance hero)
        {
            if (Current != Step.Hero) return;
            _state.Hero = hero;
            _heroOverlay?.RemoveFromHierarchy();
            HeroChosen?.Invoke(hero);
            BuildCoach();
            Show(Step.Gig, LineGig, "onboarding.gig");
        }

        void BuildCoach()
        {
            _coach = new VisualElement { name = "coach" };
            _coach.AddToClassList("coach");
            var speaker = new Label(Speaker) { name = "coach-speaker" };
            speaker.AddToClassList("coach__speaker");
            _text = new Label { name = "coach-text" };
            _text.AddToClassList("coach__text");
            _action = new Button(OnAction) { name = ActionButton };
            _action.AddToClassList("primary-button");
            _coach.Add(speaker);
            _coach.Add(_text);
            _coach.Add(_action);
            _ui.Root.Add(_coach);
            _next = _ui.Root.Q<Button>("next-month");
        }

        void Show(Step step, string lineId, string? actionKey)
        {
            Current = step;
            AnalyticsEvents.TutorialStep(_ui.Online.Analytics, (int)step);
            _text!.text = _lineText(lineId) ?? string.Empty;
            if (actionKey != null) UiText.Bind(_action!, actionKey);
            _action!.style.display = actionKey != null ? DisplayStyle.Flex : DisplayStyle.None;
            LineShown?.Invoke(lineId);
        }

        void OnAction()
        {
            switch (Current)
            {
                case Step.Gig:
                    _engine.TutorialGig(_state);
                    _ui.RefreshHud();
                    Show(Step.Lesson, LineRevenue, "onboarding.continue");
                    break;
                case Step.Lesson:
                    Show(Step.NextMonth, LineNextMonth, null);
                    _next?.AddToClassList(HighlightClass);
                    _ui.NextMonthRequested += OnNextMonth;
                    break;
            }
        }

        void OnNextMonth()
        {
            if (Current != Step.NextMonth) return;
            _ui.NextMonthRequested -= OnNextMonth;
            _next?.RemoveFromClassList(HighlightClass);
            _coach?.RemoveFromHierarchy();
            Report = _engine.EndMonth(_state, _ui.CurrentDecisions());
            _ui.ShowReport(Report);
            _ui.GuideEnabled = true;
            _ui.ShowMonthSummary(Report);
            Current = Step.Done;
            AnalyticsEvents.TutorialStep(_ui.Online.Analytics, (int)Step.Done);
            Completed?.Invoke(Report);
        }
    }
}
