using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Platform;
using UnpaidCeo.Game.Services.Online;

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
    /// Phone-first UI on one main screen: the HUD (cash, month, energy), the chapter goal, the city with the guide card
    /// ("what to do now") and today's missions, the "Next month" button under the thumb, and a dock that opens the month
    /// plan, the report, lessons and ranking as popups over the city. Journal and Settings open from the HUD the same way.
    /// All words come from the "UI" / "Academy" string tables; numbers and names carry the .value class.
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        public const string Table = "UI";
        public const string ValueClass = "value";
        public const string LanguagePrefsKey = "unpaidceo.language";
        public static readonly string[] Languages = { "en", "ru" };

        public const string City = "city", Company = "company", Decisions = "decisions", Academy = "academy",
            Ranking = "ranking", Journal = "journal", Settings = "settings", Industry = "industry", PlacesTab = "places";
        public static readonly IReadOnlyList<string> Tabs = new[] { City, Company, Decisions, Academy, Ranking, Journal, Settings, Industry, PlacesTab };
        static readonly string[] NavTabs = { PlacesTab, Decisions, Company, Academy, Ranking };

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
        IndustryScreen _industry;
        LoanDialog _loan;
        ChapterTransition _transition;
        VisualElement _chapterBar, _chapterFill;
        Button _advance;
        Label _cash, _month;
        VisualElement _energyFill;
        Button _next;
        VisualElement _turnsBar, _waitRow, _missions;
        Label _turnsValue, _countdownValue;

        public VisualElement Root { get; private set; }
        public string CurrentTab { get; private set; }
        public string Language { get; private set; } = "en";
        public bool IsNeon { get; private set; }

        IOnlineServices _online = new OfflineServices();
        /// <summary>Online services for the screens: offline until the background connection answers.</summary>
        public IOnlineServices Online
        {
            get => _online;
            set { _online = value ?? new OfflineServices(); OnlineChanged?.Invoke(_online); }
        }
        public event Action<IOnlineServices> OnlineChanged;

        AccountPanel _account;
        /// <summary>Settings → Account.</summary>
        public AccountPanel Account => _account;
        RankingScreen _ranking;
        public RankingScreen RankingPanel => _ranking;
        /// <summary>Cross-run progress (badges); set by the game after loading the meta record.</summary>
        MetaProgress _meta;
        /// <summary>Progress outside the game (missions, badges, the turn checklist); the checklist picks up this month's record.</summary>
        public MetaProgress Meta
        {
            get => _meta;
            set { _meta = value; if (value != null) _gate.Load(value); }
        }
        /// <summary>The screens show a challenge or duel run: no daily turns, no chapter bar. Set before Initialize.</summary>
        bool _runMode;
        public bool RunMode { get => _runMode; set { _runMode = value; RefreshGate(); } }
        ConsentDialog _consent;
        /// <summary>The one-time analytics question.</summary>
        public ConsentDialog Consent => _consent;
        /// <summary>Asks for analytics consent unless the player already answered on this device.</summary>
        public void AskConsent() => _consent?.Ask();
        public SettingsScreen SettingsPanel => _settings;
        HashSet<string> _opened = new HashSet<string>();
        bool _wasBankrupt;
        VisualElement _duelCard;
        /// <summary>City card "a duel is waiting for you".</summary>
        public VisualElement DuelCard => _duelCard;
        public void ShowDuelWaiting(bool on) { if (_duelCard != null) _duelCard.style.display = on && !RunMode ? DisplayStyle.Flex : DisplayStyle.None; }
        /// <summary>The player signed up, in or out, or renamed: the game re-syncs its cloud save.</summary>
        public event Action<AccountChange> AccountChanged;

        public Button NextMonthButton => _next;
        /// <summary>Chapter 2's business choice (also a tab: <see cref="Industry"/>).</summary>
        public IndustryScreen IndustryChoice => _industry;
        public LoanDialog Loan => _loan;
        public ChapterTransition Transition => _transition;
        public DecisionsScreen DecisionsPanel => _decisions;
        /// <summary>HUD progress toward the chapter goal, with the button to move on once it is met.</summary>
        public VisualElement ChapterBar => _chapterBar;
        public VisualElement ChapterFill => _chapterFill;
        public Button AdvanceButton => _advance;
        Button _chooseBusiness;
        /// <summary>HUD button back to the industry choice while a chapter-2+ player has no business.</summary>
        public Button ChooseBusinessButton => _chooseBusiness;

        /// <summary>Chapter 2 or later and no business chosen yet: the chapter goal cannot move until one is.</summary>
        public bool NeedsBusiness => _engine != null && _state != null && _state.Chapter >= 2
            && !_state.Businesses.Any(b => _engine.Content.Industries.TryGetValue(b.IndustryId, out var d) && d.Kind == UnpaidCeo.Core.Economy.IndustryKind.Business);

        /// <summary>Where the game is after a month: the industry choice until there is a business, else the main screen.</summary>
        public string HomeTab => NeedsBusiness ? Industry : City;

        public void OpenBusinessChoice() => Open(Industry);

        /// <summary>After the month's scenes: their effects (staff, cash) show in the steppers and screens.</summary>
        /// <summary>A scene's dialogue is on screen (it is drawn over the City screen's lower part).</summary>
        public void SetSceneOpen(bool open)
        {
            _sceneOpen = open;
            _missions.style.display = !open && _missions.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
        bool _sceneOpen;

        public void AfterScenes()
        {
            _decisions.Refresh();
            _company.Refresh();
            RefreshHud();
        }

        /// <summary>The player moved to the next chapter (and district).</summary>
        public event Action<int> ChapterAdvanced;
        public event Action<string> IndustryChosen;
        public event Action LoanTaken;
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

        VisualElement _popupBackdrop, _guide, _missionsBody;
        Button _popupClose, _guideAction;
        Label _guideText, _heroTag, _goal, _goalValue;
        MonthSummaryDialog _summary;
        string _guideKey = "";
        Action _guideRun;
        bool _guideEnabled = true, _noTurns, _busy, _newLesson, _missionsOpen;
        int _goalChapter;

        /// <summary>Dims the city while another screen is open over it; a tap on it closes that screen.</summary>
        public VisualElement PopupBackdrop => _popupBackdrop;
        public Button PopupClose => _popupClose;
        /// <summary>A screen other than the city is open (as a popup over it).</summary>
        public bool PopupOpen => CurrentTab != City;
        /// <summary>The month's result, shown over the main screen after each month.</summary>
        public MonthSummaryDialog Summary => _summary;
        /// <summary>What has to be done before "Next month" this month (plan, a visit, study every second month).</summary>
        public TurnGate Gate => _gate;
        /// <summary>The checklist changed (the bootstrap saves the meta record).</summary>
        public event Action GateChanged;
        public AcademyScreen AcademyPanel => _academy;
        public bool MonthSummaryOpen => _summary != null && _summary.IsOpen;
        public void ShowMonthSummary(MonthReport report) => _summary.Show(report, Language);

        /// <summary>"What to do now": one line and one button on the main screen.</summary>
        public VisualElement Guide => _guide;
        public Label GuideText => _guideText;
        public Button GuideAction => _guideAction;
        /// <summary>String-table key of the guide's current advice ("" while it is hidden).</summary>
        public string GuideKey => _guideKey;
        /// <summary>Off while the onboarding mentor is talking.</summary>
        public bool GuideEnabled
        {
            get => _guideEnabled;
            set { _guideEnabled = value; RefreshGuide(); RefreshGate(); }
        }

        /// <summary>A month is being played: "Next month" shows it cannot be pressed.</summary>
        public void SetBusy(bool busy)
        {
            if (busy != _busy && _next != null) UiText.Bind(_next, busy ? "hud.week_running" : "hud.next_week");
            _busy = busy;
            ApplyNext();
        }

        void ApplyNext() => _next?.SetEnabled(!_noTurns && !_busy && !_nextLocked && !_gateLocked);

        readonly TurnGate _gate = new TurnGate();
        VisualElement _gateBox, _gateItems;
        bool _gateLocked;

        /// <summary>Academy questions still open (unlocked, never answered); the gate asks for at most three of them.</summary>
        readonly HashSet<string> _answeredAtMonthStart = new HashSet<string>();
        int StudyNeeded()
        {
            _answeredAtMonthStart.Clear();
            if (_state == null || _engine == null) return 0;
            foreach (var k in _state.QuizAnswers.Keys) _answeredAtMonthStart.Add(k);
            if (!TurnGate.IsStudyTurn(_state.Week)) return 0;
            int open = 0;
            foreach (var lesson in _engine.Content.LessonsInOrder)
            {
                if (!_state.LessonsSeen.ContainsKey(lesson.Id)) continue;
                for (int q = 0; q < lesson.Quiz.Count; q++)
                    if (!_state.QuizAnswers.ContainsKey(lesson.Id + "/" + q)) open++;
            }
            return Math.Min(TurnGate.DefaultAnswers, open);
        }

        /// <summary>A week began (boot or after a result): the checklist starts over for it.</summary>
        void StartGateMonth()
        {
            if (_state == null) return;
            _gate.StartTurn(_state.Week);
            _gate.AnswersNeeded = StudyNeeded();
            if (_meta != null) _gate.Load(_meta);
            RefreshGate();
        }

        void RefreshGate()
        {
            if (_gateBox == null) return;
            bool show = !RunMode && _state != null && _guideEnabled;   // the onboarding mentor speaks alone
            _gateBox.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            _gateLocked = show && !_gate.AllMet;
            ApplyNext();
            _gateItems.Clear();
            if (!show) return;
            GateItem(TurnGate.Plan, _gate.PlanReviewed, null, () => Open(Decisions));
            GateItem(TurnGate.Place, _gate.PlaceVisited, null, OpenPlaces);
            if (_gate.StudyRequired)
                GateItem(TurnGate.StudyItem, _gate.AnswersGiven >= _gate.AnswersNeeded,
                    Math.Min(_gate.AnswersGiven, _gate.AnswersNeeded) + " / " + _gate.AnswersNeeded, () => Open(Academy));
            if (_meta != null) _gate.CopyTo(_meta);
            GateChanged?.Invoke();
        }

        void GateItem(string id, bool done, string count, Action open)
        {
            var item = new Button(open) { name = "gate-" + id };
            item.AddToClassList("gate__item");
            item.EnableInClassList("gate__item--done", done);
            var check = new Label(done ? "\u2713" : "") { name = "gate-check-" + id };
            check.AddToClassList("gate__check");
            check.AddToClassList("value");
            item.Add(check);
            item.Add(UiText.Label("gate." + id, "gate__text"));
            if (count != null) item.Add(UiText.ValueLabel(count, "gate__count"));
            _gateItems.Add(item);
        }

        PlacesScreen _places;
        /// <summary>The block and its places (Home, the bodega, the bank…).</summary>
        public PlacesScreen PlacesPanel => _places;

        /// <summary>The list of places.</summary>
        public void OpenPlaces()
        {
            _places.ShowList();
            Open(PlacesTab);
        }

        /// <summary>Walks into a place.</summary>
        public void OpenPlace(string id)
        {
            _places.Show(id);
            Open(PlacesTab);
            _gate.PlaceVisited = true;
        }

        readonly Dictionary<string, Button> _markers = new Dictionary<string, Button>();

        /// <summary>A place's marker over its building in the city: a tap walks in. Null hides it.</summary>
        public void PlaceMarker(string id, Vector2? panelPosition)
        {
            if (_heroTag == null || RunMode) return;
            if (!_markers.TryGetValue(id, out var marker))
            {
                if (panelPosition == null) return;
                // A shop sign over the building: the place's picture with its name under it; a tap walks in.
                // (A Button draws its own text under its children, so the name is a Label of its own.)
                marker = new Button(() => OpenPlace(id)) { name = "marker-" + id };
                marker.AddToClassList("place-marker");
                var pic = new VisualElement { name = "marker-pic-" + id };
                pic.AddToClassList("place-marker__pic");
                marker.Add(pic);
                var title = UiText.Label($"place.{id}.title", "place-marker__title");
                title.name = "marker-title-" + id;
                marker.Add(title);
                PlacesScreen.LoadThumb(id, pic);
                var city = _screens[City];
                city.Insert(city.IndexOf(_heroTag), marker);
                _markers[id] = marker;
            }
            if (panelPosition == null) { marker.style.display = DisplayStyle.None; return; }
            var cityArea = _screens[City];
            var local = cityArea.WorldToLocal(panelPosition.Value);
            // The sign hangs above its point (translate -50% -100%): it must clear the city hint at the top and stay inside
            // the city's sides, or it is drawn over the HUD and the chapter bar.
            float width = cityArea.layout.width;
            bool fits = local.y >= MarkerClearTop && local.x >= MarkerHalfWidth && (float.IsNaN(width) || local.x <= width - MarkerHalfWidth);
            if (!fits) { marker.style.display = DisplayStyle.None; return; }
            marker.style.left = local.x;
            marker.style.top = local.y;
            marker.style.display = DisplayStyle.Flex;
        }

        /// <summary>A sign's height: one hanging higher than this would be drawn over the HUD and the chapter bar.</summary>
        const float MarkerClearTop = 210f;
        /// <summary>Half of a long marker's width ("Laundromat Office").</summary>
        const float MarkerHalfWidth = 110f;

        readonly Dictionary<string, Label> _tags = new Dictionary<string, Label>();

        /// <summary>A name tag above someone in the city (the mentor, Tony); null hides it.</summary>
        public void PlaceTag(string id, string key, Vector2? panelPosition)
        {
            if (_heroTag == null) return;
            if (!_tags.TryGetValue(id, out var tag))
            {
                if (panelPosition == null) return;
                tag = UiText.Label(key, "npc-tag");
                tag.name = "tag-" + id;
                var city = _screens[City];
                city.Insert(city.IndexOf(_heroTag), tag);   // under the hero's tag and the widgets
                _tags[id] = tag;
            }
            if (panelPosition == null) { tag.style.display = DisplayStyle.None; return; }
            var local = _screens[City].WorldToLocal(panelPosition.Value);
            tag.style.left = local.x;
            tag.style.top = local.y;
            tag.style.display = DisplayStyle.Flex;
        }

        bool _nextLocked;
        /// <summary>Onboarding: "Next month" is off until the mentor asks for it (a button that does nothing must not look alive).</summary>
        public bool NextLocked
        {
            get => _nextLocked;
            set { _nextLocked = value; ApplyNext(); }
        }

        /// <summary>"You" above the hero in the city.</summary>
        public Label HeroTag => _heroTag;

        /// <param name="panelPosition">Where the hero's head is on the panel; null hides the tag.</param>
        public void PlaceHeroTag(Vector2? panelPosition)
        {
            if (_heroTag == null) return;
            if (panelPosition == null) { _heroTag.style.display = DisplayStyle.None; return; }
            var local = _screens[City].WorldToLocal(panelPosition.Value);
            _heroTag.style.left = local.x;
            _heroTag.style.top = local.y;
            _heroTag.style.display = DisplayStyle.Flex;
        }

        void RefreshGuide()
        {
            if (_guide == null) return;
            if (_engine == null || _state == null || RunMode || !_guideEnabled)
            {
                _guide.style.display = DisplayStyle.None;
                _guideKey = "";
                return;
            }
            string key;
            if (_engine.Chapter(_state).GoalMet && _state.Chapter < Chapters.LastChapter) { key = "guide.next_chapter"; _guideRun = Advance; }
            else if (NeedsBusiness) { key = "guide.choose_business"; _guideRun = OpenBusinessChoice; }
            else if (_newLesson) { key = "guide.lesson"; _guideRun = () => Open(Academy); }
            else if (_noTurns) { key = "guide.no_turns"; _guideRun = () => Open(Academy); }
            else if (_state.Cash.Cents < 0) { key = "guide.broke"; _guideRun = () => Open(Decisions); }
            else { key = "guide.plan"; _guideRun = () => Open(Decisions); }
            _guide.style.display = DisplayStyle.Flex;
            if (key == _guideKey) return;
            _guideKey = key;
            UiText.Bind(_guideText, key);
            UiText.Bind(_guideAction, key + ".action");
        }

        void OnDestroy() => UnpaidCeo.Game.City.CameraRig.InputBlocked = false;

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
            UiText.Bind(hud.Q<Label>("hud-month-caption"), "hud.week");
            UiText.Bind(hud.Q<Label>("hud-energy-caption"), "hud.energy");
            _cash = hud.Q<Label>("hud-cash");
            _month = hud.Q<Label>("hud-month");
            _energyFill = hud.Q("hud-energy-fill");
            var journalButton = UiText.Bind(hud.Q<Button>("hud-journal"), "hud.journal");
            journalButton.clicked += () => Open(Journal);
            var settingsButton = UiText.Bind(hud.Q<Button>("hud-settings"), "hud.settings");
            settingsButton.clicked += () => Open(Settings);

            _chapterBar = new VisualElement { name = "chapter-bar" };
            _chapterBar.AddToClassList("chapter-bar");
            var goalRow = new VisualElement();
            goalRow.AddToClassList("chapter-bar__goal");
            _goal = UiText.Label("goal.1", "chapter-bar__caption");
            _goal.name = "chapter-goal";
            _goalValue = UiText.ValueLabel("", "chapter-bar__goal-value");
            _goalValue.name = "chapter-goal-value";
            goalRow.Add(_goal);
            goalRow.Add(_goalValue);
            _chapterBar.Add(goalRow);
            var progressRow = new VisualElement();
            progressRow.AddToClassList("chapter-bar__row");
            var track = new VisualElement();
            track.AddToClassList("chapter-bar__track");
            _chapterFill = new VisualElement { name = "chapter-fill" };
            _chapterFill.AddToClassList("chapter-bar__fill");
            track.Add(_chapterFill);
            progressRow.Add(track);
            _advance = UiText.Bind(new Button(Advance) { name = "chapter-advance" }, "hud.advance");
            _advance.AddToClassList("chapter-bar__advance");
            progressRow.Add(_advance);
            _chooseBusiness = UiText.Bind(new Button(OpenBusinessChoice) { name = "chapter-business" }, "industry.title");
            _chooseBusiness.AddToClassList("chapter-bar__advance");
            progressRow.Add(_chooseBusiness);
            _chapterBar.Add(progressRow);
            Root.Add(_chapterBar);
            _loan = new LoanDialog(this, engine, state);
            _loan.Taken += (_, __) => { RefreshHud(); _company?.Refresh(); LoanTaken?.Invoke(); };
            _transition = new ChapterTransition();

            var content = new VisualElement { name = "content" };
            content.AddToClassList("content");
            Root.Add(content);
            AddScreen(content, City, Clone("City"));
            _popupBackdrop = new VisualElement { name = "popup-backdrop" };
            _popupBackdrop.AddToClassList("popup-backdrop");
            _popupBackdrop.RegisterCallback<ClickEvent>(_ => Open(City));
            content.Add(_popupBackdrop);
            AddScreen(content, Company, Clone("Company"));
            AddScreen(content, Decisions, Clone("Decisions"));
            AddScreen(content, Academy, Clone("Academy"));
            AddScreen(content, Ranking, Clone("Ranking"));
            AddScreen(content, Journal, Clone("Journal"));
            AddScreen(content, Settings, Clone("Settings"));
            AddScreen(content, Industry, Clone("Industry"));
            AddScreen(content, PlacesTab, Clone("Places"));
            _popupClose = UiText.Bind(new Button(() => Open(City)) { name = "popup-close" }, "popup.close");
            _popupClose.AddToClassList("popup-close");
            content.Add(_popupClose);

            // The hint band is gone: the checklist ("drop by a place in the city") says it, and the signs need the room.
            _screens[City].Q<Label>("city-hint").style.display = DisplayStyle.None;
            _ranking = new RankingScreen(_screens[Ranking], this, engine, state);
            _company = new CompanyScreen(_screens[Company], this, engine, state);
            _decisions = new DecisionsScreen(_screens[Decisions], this, engine, state);
            _places = new PlacesScreen(_screens[PlacesTab], this, engine, state, _decisions);
            _industry = new IndustryScreen(_screens[Industry], this, engine, state);
            _industry.Chosen += id => { _decisions.Refresh(); RefreshHud(); AnalyticsEvents.IndustryChosen(Online.Analytics, id); IndustryChosen?.Invoke(id); };
            _academy = new AcademyScreen(_screens[Academy], engine, state);
            _academy.Answered += (lesson, q) =>
            {
                if (!_answeredAtMonthStart.Contains(lesson + "/" + q)) _gate.RecordAnswer(lesson + "/" + q);
                QuizAnswered?.Invoke(lesson, q);
            };
            _academy.LessonViewed += id => AnalyticsEvents.LessonViewed(Online.Analytics, id);
            _academy.QuizPassed += id => AnalyticsEvents.QuizPassed(Online.Analytics, id);
            _opened = new HashSet<string>(OpenBusinesses());
            _wasBankrupt = state.Facts.Has("bankrupt");
            _heroTag = UiText.Label("city.you", "hero-tag");
            _heroTag.name = "hero-tag";
            _heroTag.style.display = DisplayStyle.None;
            _screens[City].Add(_heroTag);
            var stack = new VisualElement { name = "city-stack" };
            stack.AddToClassList("city-stack");
            _screens[City].Add(stack);
            _missions = new VisualElement { name = "city-missions" };
            _missions.AddToClassList("missions");
            _missions.style.display = DisplayStyle.None;
            _duelCard = new VisualElement { name = "city-duel" };
            _duelCard.AddToClassList("missions");
            _duelCard.Add(UiText.Label("duel.waiting", "missions__title"));
            var openDuels = UiText.Bind(new Button(() => Open(Ranking)) { name = "city-duel-open" }, "duel.open");
            openDuels.AddToClassList("choice-button");
            _duelCard.Add(openDuels);
            _duelCard.style.display = DisplayStyle.None;
            stack.Add(_duelCard);
            stack.Add(_missions);
            _gateBox = new VisualElement { name = "turn-gate" };
            _gateBox.AddToClassList("gate");
            _gateBox.Add(UiText.Label("gate.title", "gate__title"));
            _gateItems = new VisualElement { name = "gate-items" };
            _gateBox.Add(_gateItems);
            stack.Add(_gateBox);
            _gate.Changed += RefreshGate;
            _guide = new VisualElement { name = "guide" };
            _guide.AddToClassList("guide");
            _guideText = new Label { name = "guide-text" };
            _guideText.AddToClassList("guide__text");
            _guideAction = new Button(() => _guideRun?.Invoke()) { name = "guide-action" };
            _guideAction.AddToClassList("guide__action");
            _guide.Add(_guideText);
            _guide.Add(_guideAction);
            stack.Add(_guide);
            _journal = new JournalScreen(_screens[Journal], engine, state);
            _settings = new SettingsScreen(_screens[Settings], this);
            _settings.ResetRequested += () => ResetRequested?.Invoke();
            _account = new AccountPanel(_screens[Settings], this);
            _account.Changed += change => AccountChanged?.Invoke(change);
            _consent = new ConsentDialog(this);
            Root.Add(_consent.Root);
            _summary = new MonthSummaryDialog();
            _summary.DetailsRequested += () => Open(Company);

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

            _next = UiText.Bind(new Button(() => NextMonthRequested?.Invoke()) { name = "next-month" }, "hud.next_week");
            _next.AddToClassList("primary-button");
            Root.Add(_next);

            var nav = Clone("BottomNav");
            Root.Add(nav);
            foreach (var tab in NavTabs)
            {
                var t = tab;
                var b = UiText.Bind(nav.Q<Button>("nav-" + tab), "dock." + tab);
                b.clicked += () => { if (t == PlacesTab) OpenPlaces(); else Open(t); };
                _nav[tab] = b;
            }

            Root.Add(_summary.Root);
            Root.Add(_loan.Root);
            Root.Add(_transition.Root);

            Open(NeedsBusiness ? Industry : City);
            RefreshHud();
            StartGateMonth();
        }

        /// <summary>Moves to the next chapter when its goal is met: title card, then (chapter 2) the business choice.</summary>
        public void Advance()
        {
            if (_engine == null || !_engine.AdvanceChapter(_state)) return;
            _transition.Play(_state.Chapter);
            _decisions.Refresh();
            _company.Refresh();
            RefreshHud();
            AnalyticsEvents.ChapterUnlocked(Online.Analytics, _state.Chapter);
            ChapterAdvanced?.Invoke(_state.Chapter);
            if (NeedsBusiness) Open(Industry);
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
            bool popup = tab != City;
            foreach (var s in _screens) s.Value.style.display = s.Key == tab || s.Key == City ? DisplayStyle.Flex : DisplayStyle.None;
            _popupBackdrop.style.display = _popupClose.style.display = popup ? DisplayStyle.Flex : DisplayStyle.None;
            UnpaidCeo.Game.City.CameraRig.InputBlocked = popup;   // drags belong to the popup, not to the city under it
            foreach (var b in _nav) b.Value.EnableInClassList("nav-button--active", b.Key == tab);
            CurrentTab = tab;
            if (tab == Decisions) _gate.PlanReviewed = true;
            if (tab == Academy) { _newLesson = false; _academy.Refresh(); }
            if (tab == Journal) _journal.Refresh();
            if (tab == Settings) { _settings.Refresh(); _account.Refresh(); }
            if (tab == Ranking) _ranking.RefreshSoon();
            if (tab == Industry) _industry.Show(_engine, _state);
            if (tab == Company) _company.Refresh();
            if (tab == PlacesTab) { _places.LoadThumbs(); _places.Refresh(); }
            RefreshGuide();
        }

        /// <summary>Call after each EndMonth.</summary>
        public void ShowReport(MonthReport report)
        {
            if (!RunMode) RecordMonth(report);
            if (report.Lessons.Count > 0) _newLesson = true;
            _company.Show(report);
            _decisions.Refresh();
            _decisions.ShowRejections(report);
            _places.Refresh();
            _journal.Refresh();
            _academy.Refresh();
            RefreshHud();
            StartGateMonth();
        }

        public void RefreshHud()
        {
            if (_state == null) return;
            _cash.text = MoneyFormat.Compact(_state.Cash, Language);
            _cash.EnableInClassList("value--negative", _state.Cash.Cents < 0);
            _month.text = _state.Week.ToString();
            _energyFill.style.width = Length.Percent(_state.Life.Energy);
            if (_chapterBar != null) _chapterBar.style.display = RunMode ? DisplayStyle.None : DisplayStyle.Flex;
            if (_engine != null && _chapterBar != null && !RunMode)
            {
                var chapter = _engine.Chapter(_state);
                _chapterFill.style.width = Length.Percent((float)(chapter.Progress * 100));
                bool canMove = chapter.GoalMet && _state.Chapter < Chapters.LastChapter;
                _advance.style.display = canMove ? DisplayStyle.Flex : DisplayStyle.None;
                int shown = Mathf.Clamp(_state.Chapter, 1, Chapters.LastChapter);
                if (shown != _goalChapter)
                {
                    _goalChapter = shown;
                    UiText.Bind(_goal, "goal." + shown);
                }
                var balance = _engine.Content.Balance;
                _goalValue.text = MoneyFormat.Compact(shown == 1 ? balance.Chapter1CashGoal : shown == 2 ? balance.Chapter2CapitalGoal : balance.Chapter3CapitalGoal, Language);
            }
            if (_chooseBusiness != null) _chooseBusiness.style.display = NeedsBusiness ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshGuide();
        }

        public MonthDecisions CurrentDecisions() => _decisions.Build();

        IEnumerable<string> OpenBusinesses() => _state.Businesses
            .Where(b => b.Units > 0 && _engine.Content.Industries.TryGetValue(b.IndustryId, out var d) && d.Kind == UnpaidCeo.Core.Economy.IndustryKind.Business)
            .Select(b => b.IndustryId);

        /// <summary>Analytics for a finished career month: the month, a first business unit, going bankrupt.</summary>
        void RecordMonth(MonthReport report)
        {
            var a = Online.Analytics;
            AnalyticsEvents.MonthCompleted(a, report.Month, _state.Chapter);
            foreach (var id in OpenBusinesses().ToList())
                if (_opened.Add(id)) AnalyticsEvents.BusinessOpened(a, id, _state.Businesses.First(b => b.IndustryId == id).Units);
            bool bankrupt = _state.Facts.Has("bankrupt");
            if (bankrupt && !_wasBankrupt) AnalyticsEvents.Bankrupt(a, report.Month);
            _wasBankrupt = bankrupt;
        }

        /// <summary>Manual months left today; with none left the button is off and a countdown to the refill shows.</summary>
        public void SetTurns(int left, int perDay, long secondsToRefill)
        {
            if (RunMode) return;   // runs use no daily turns
            _turnsBar.style.display = DisplayStyle.Flex;
            _turnsValue.text = $"{Math.Max(0, left)}/{perDay}";
            bool none = left <= 0;
            bool changed = none != _noTurns;
            _noTurns = none;
            ApplyNext();
            _waitRow.style.display = none ? DisplayStyle.Flex : DisplayStyle.None;
            _countdownValue.text = Countdown(secondsToRefill);
            if (changed) RefreshGuide();
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
            var toggle = new Button(() =>
            {
                _missionsOpen = !_missionsOpen;
                _missionsBody.style.display = _missionsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            }) { name = "missions-toggle" };
            toggle.AddToClassList("missions__toggle");
            toggle.Add(UiText.Label("missions.title", "missions__title"));
            toggle.Add(UiText.ValueLabel($"{missions.Count(m => m.Done)}/{missions.Count}", "missions__count"));
            _missions.Add(toggle);
            _missionsBody = new VisualElement { name = "missions-body" };
            _missionsBody.style.display = _missionsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            _missions.Add(_missionsBody);
            foreach (var m in missions)
            {
                var row = new VisualElement();
                row.AddToClassList("row");
                row.AddToClassList("mission");
                row.EnableInClassList("mission--done", m.Done);
                row.Add(UiText.Label(m.TitleKey, "row__label"));
                row.Add(UiText.ValueLabel($"{Math.Min(m.Progress, m.Target)}/{m.Target}", "row__value"));
                _missionsBody.Add(row);
            }
            var footer = new VisualElement();
            footer.AddToClassList("missions__footer");
            footer.Add(UiText.Label("missions.streak", "missions__caption"));
            footer.Add(UiText.ValueLabel(streak.ToString(), "missions__value"));
            footer.Add(UiText.Label("missions.points", "missions__caption"));
            footer.Add(UiText.ValueLabel(ceoPoints.ToString(), "missions__value"));
            _missionsBody.Add(footer);
            _missions.style.display = missions.Count > 0 && !_sceneOpen ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public IEnumerator SetLanguage(string code)
        {
            if (Array.IndexOf(Languages, code) < 0) code = "en";
            var init = LocalizationSettings.InitializationOperation;
            if (!init.IsDone) yield return init;
            var locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));
            if (locale != null) LocalizationSettings.SelectedLocale = locale;
            else Debug.LogWarning($"Locale '{code}' is not configured");
            // Load the language's tables before returning, so screens don't show the previous language while they stream in.
            foreach (var table in new[] { Table, AcademyScreen.Table })
            {
                var load = LocalizationSettings.StringDatabase.GetTableAsync(table);
                if (!load.IsDone) yield return load;
            }
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
