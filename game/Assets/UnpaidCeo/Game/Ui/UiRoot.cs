using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Platform;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>One daily mission row: its title key, progress toward the target, done or not.</summary>
    public readonly struct MissionView
    {
        public readonly string TitleKey;
        public readonly int Progress;
        public readonly int Target;
        public readonly bool Done;
        public MissionView(string titleKey, int progress, int target, bool done) { TitleKey = titleKey; Progress = progress; Target = target; Done = done; }
    }

    /// <summary>
    /// Phone-first UI (spec §5): HUD (cash, month, energy), the screen area, the "Next month" button under the thumb,
    /// and the bottom nav City · Company · Decisions · Academy · Ranking. Journal and Settings open from the HUD.
    /// All words come from the "UI" / "Academy" string tables; numbers and names carry the .value class.
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        public const string Table = "UI";
        public const string ValueClass = "value";
        public const string LanguagePrefsKey = "unpaidceo.language";
        public static readonly string[] Languages = { "en", "ru" };

        public const string City = "city", Company = "company", Decisions = "decisions", Academy = "academy",
            Ranking = "ranking", Journal = "journal", Settings = "settings";
        public static readonly IReadOnlyList<string> Tabs = new[] { City, Company, Decisions, Academy, Ranking, Journal, Settings };
        static readonly string[] NavTabs = { City, Company, Decisions, Academy, Ranking };

        readonly Dictionary<string, VisualElement> _screens = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, Button> _nav = new Dictionary<string, Button>();
        UIDocument _document;
        GameEngine _engine;
        GameState _state;
        CompanyScreen _company;
        DecisionsScreen _decisions;
        JournalScreen _journal;
        SettingsScreen _settings;
        AcademyScreen _academy;
        Label _cash, _month;
        VisualElement _energyFill;
        Button _next;
        VisualElement _turnsBar, _waitRow, _missions;
        Label _turnsValue, _countdownValue;

        public VisualElement Root { get; private set; }
        public string CurrentTab { get; private set; }
        public string Language { get; private set; } = "en";
        public bool IsNeon { get; private set; }

        public Button NextMonthButton => _next;
        /// <summary>"left/per day" manual months.</summary>
        public Label TurnsValue => _turnsValue;
        /// <summary>Shown instead of a working button when today's months are used up.</summary>
        public VisualElement CountdownRow => _waitRow;
        public Label CountdownValue => _countdownValue;
        /// <summary>Today's missions on the City screen.</summary>
        public VisualElement MissionsPanel => _missions;

        public event Action NextMonthRequested;
        /// <summary>A quiz answer in the Academy (lesson id, question index), first try or practice.</summary>
        public event Action<string, int> QuizAnswered;
        public event Action<bool> ThemeChanged;
        public event Action ResetRequested;

        /// <summary>Yarn line id → text in the current language, for decisions in the journal.</summary>
        public Func<string, string> LineText
        {
            get => _journal?.LineText;
            set { if (_journal != null) { _journal.LineText = value; _journal.Refresh(); } }
        }

        public static string PickLanguage() =>
            LanguagePicker.Pick(PlayerPrefs.GetString(LanguagePrefsKey, null), Application.systemLanguage.ToString(), Languages);

        /// <param name="target">Render into a texture instead of the screen (tests, screenshots).</param>
        public void Initialize(GameEngine engine, GameState state, RenderTexture target = null)
        {
            _engine = engine;
            _state = state;

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "UnpaidCeo Panel";
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/UnpaidTheme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1080, 1920);
            panel.screenMatchMode = PanelScreenMatchMode.Expand;
            panel.targetTexture = target;
            panel.clearColor = target != null;
            panel.colorClearValue = new Color(0.55f, 0.72f, 0.87f, 1f);

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panel;
            if (_document.rootVisualElement == null) { _document.enabled = false; _document.enabled = true; }

            Root = new VisualElement { name = "app" };
            Root.AddToClassList("app");
            Root.AddToClassList("theme-day");
            Root.style.flexGrow = 1;
            _document.rootVisualElement.Add(Root);

            var hud = Clone("Hud");
            Root.Add(hud);
            UiText.Bind(hud.Q<Label>("hud-cash-caption"), "hud.cash");
            UiText.Bind(hud.Q<Label>("hud-month-caption"), "hud.month");
            UiText.Bind(hud.Q<Label>("hud-energy-caption"), "hud.energy");
            _cash = hud.Q<Label>("hud-cash");
            _month = hud.Q<Label>("hud-month");
            _energyFill = hud.Q("hud-energy-fill");
            var journalButton = UiText.Bind(hud.Q<Button>("hud-journal"), "hud.journal");
            journalButton.clicked += () => Open(Journal);
            var settingsButton = UiText.Bind(hud.Q<Button>("hud-settings"), "hud.settings");
            settingsButton.clicked += () => Open(Settings);

            var content = new VisualElement { name = "content" };
            content.AddToClassList("content");
            Root.Add(content);
            AddScreen(content, City, Clone("City"));
            AddScreen(content, Company, Clone("Company"));
            AddScreen(content, Decisions, Clone("Decisions"));
            AddScreen(content, Academy, Clone("Academy"));
            AddScreen(content, Ranking, Clone("Ranking"));
            AddScreen(content, Journal, Clone("Journal"));
            AddScreen(content, Settings, Clone("Settings"));

            UiText.Bind(_screens[City].Q<Label>("city-hint"), "city.hint");
            UiText.Bind(_screens[Ranking].Q<Label>("ranking-title"), "ranking.title");
            UiText.Bind(_screens[Ranking].Q<Label>("ranking-soon"), "ranking.soon");
            _company = new CompanyScreen(_screens[Company], this);
            _decisions = new DecisionsScreen(_screens[Decisions], engine, state);
            _academy = new AcademyScreen(_screens[Academy], engine, state);
            _academy.Answered += (lesson, q) => QuizAnswered?.Invoke(lesson, q);
            _missions = new VisualElement { name = "city-missions" };
            _missions.AddToClassList("missions");
            _missions.style.display = DisplayStyle.None;
            _screens[City].Add(_missions);
            _journal = new JournalScreen(_screens[Journal], engine, state);
            _settings = new SettingsScreen(_screens[Settings], this);
            _settings.ResetRequested += () => ResetRequested?.Invoke();

            _turnsBar = new VisualElement { name = "turns-bar" };
            _turnsBar.AddToClassList("turns-bar");
            _turnsBar.Add(UiText.Label("hud.turns_left", "turns-bar__caption"));
            _turnsValue = UiText.ValueLabel("", "turns-bar__value");
            _turnsValue.name = "turns-left";
            _turnsBar.Add(_turnsValue);
            _waitRow = new VisualElement { name = "next-wait" };
            _waitRow.AddToClassList("turns-bar__wait");
            _waitRow.Add(UiText.Label("hud.next_in", "turns-bar__caption"));
            _countdownValue = UiText.ValueLabel("", "turns-bar__value");
            _countdownValue.name = "next-countdown";
            _waitRow.Add(_countdownValue);
            _waitRow.style.display = DisplayStyle.None;
            _turnsBar.Add(_waitRow);
            _turnsBar.style.display = DisplayStyle.None;
            Root.Add(_turnsBar);

            _next = UiText.Bind(new Button(() => NextMonthRequested?.Invoke()) { name = "next-month" }, "hud.next_month");
            _next.AddToClassList("primary-button");
            Root.Add(_next);

            var nav = Clone("BottomNav");
            Root.Add(nav);
            foreach (var tab in NavTabs)
            {
                var t = tab;
                var b = UiText.Bind(nav.Q<Button>("nav-" + tab), "nav." + tab);
                b.clicked += () => Open(t);
                _nav[tab] = b;
            }

            Open(City);
            RefreshHud();
        }

        static VisualElement Clone(string name)
        {
            var asset = Resources.Load<VisualTreeAsset>("UI/" + name);
            if (asset == null) throw new InvalidOperationException($"UI/{name}.uxml is missing from Resources");
            var tree = asset.Instantiate();
            return tree.childCount == 1 ? tree[0] : tree;
        }

        void AddScreen(VisualElement content, string tab, VisualElement screen)
        {
            screen.name = tab;
            _screens[tab] = screen;
            content.Add(screen);
        }

        public VisualElement Screen(string tab) => _screens[tab];

        public void Open(string tab)
        {
            if (!_screens.ContainsKey(tab)) throw new ArgumentException("unknown tab " + tab, nameof(tab));
            foreach (var s in _screens) s.Value.style.display = s.Key == tab ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var b in _nav) b.Value.EnableInClassList("nav-button--active", b.Key == tab);
            CurrentTab = tab;
            if (tab == Academy) _academy.Refresh();
            if (tab == Journal) _journal.Refresh();
            if (tab == Settings) _settings.Refresh();
        }

        /// <summary>Call after each EndMonth.</summary>
        public void ShowReport(MonthReport report)
        {
            _company.Show(report);
            _decisions.Refresh();
            _journal.Refresh();
            _academy.Refresh();
            RefreshHud();
        }

        public void RefreshHud()
        {
            if (_state == null) return;
            _cash.text = MoneyFormat.Compact(_state.Cash, Language);
            _cash.EnableInClassList("value--negative", _state.Cash.Cents < 0);
            _month.text = _state.Month.ToString();
            _energyFill.style.width = Length.Percent(_state.Life.Energy);
        }

        public MonthDecisions CurrentDecisions() => _decisions.Build();

        /// <summary>Manual months left today; with none left the button is off and a countdown to the refill shows.</summary>
        public void SetTurns(int left, int perDay, long secondsToRefill)
        {
            _turnsBar.style.display = DisplayStyle.Flex;
            _turnsValue.text = $"{Math.Max(0, left)}/{perDay}";
            bool none = left <= 0;
            _next.SetEnabled(!none);
            _waitRow.style.display = none ? DisplayStyle.Flex : DisplayStyle.None;
            _countdownValue.text = Countdown(secondsToRefill);
        }

        public static string Countdown(long seconds)
        {
            if (seconds < 0) seconds = 0;
            return $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
        }

        /// <summary>Today's missions, the streak and CEO points on the City screen.</summary>
        public void SetMissions(IReadOnlyList<MissionView> missions, int streak, int ceoPoints)
        {
            _missions.Clear();
            _missions.Add(UiText.Label("missions.title", "missions__title"));
            foreach (var m in missions)
            {
                var row = new VisualElement();
                row.AddToClassList("row");
                row.AddToClassList("mission");
                row.EnableInClassList("mission--done", m.Done);
                row.Add(UiText.Label(m.TitleKey, "row__label"));
                row.Add(UiText.ValueLabel($"{Math.Min(m.Progress, m.Target)}/{m.Target}", "row__value"));
                _missions.Add(row);
            }
            var footer = new VisualElement();
            footer.AddToClassList("missions__footer");
            footer.Add(UiText.Label("missions.streak", "missions__caption"));
            footer.Add(UiText.ValueLabel(streak.ToString(), "missions__value"));
            footer.Add(UiText.Label("missions.points", "missions__caption"));
            footer.Add(UiText.ValueLabel(ceoPoints.ToString(), "missions__value"));
            _missions.Add(footer);
            _missions.style.display = missions.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public IEnumerator SetLanguage(string code)
        {
            if (Array.IndexOf(Languages, code) < 0) code = "en";
            var init = LocalizationSettings.InitializationOperation;
            if (!init.IsDone) yield return init;
            var locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));
            if (locale != null) LocalizationSettings.SelectedLocale = locale;
            else Debug.LogWarning($"Locale '{code}' is not configured");
            Language = code;
            PlayerPrefs.SetString(LanguagePrefsKey, code);
            RefreshHud();
            _company?.Refresh();
            _settings?.Refresh();
            yield return null;
        }

        public void SetTheme(bool neon)
        {
            IsNeon = neon;
            Root.EnableInClassList("theme-neon", neon);
            Root.EnableInClassList("theme-day", !neon);
            ThemeChanged?.Invoke(neon);
        }
    }
}
