using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Game.Ui.Kit;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// Stage 5B · the hero's state in a second: the HUD's need bars (each floats its change; a tap opens the needs panel
    /// with what raises each and what a fall costs), mood badges under the HUD (a tap says one line from life) and the
    /// week's three wants (ticked with a sound when the week takes them). Everything is read from Core's HeroView.
    /// </summary>
    public sealed class HeroStateView
    {
        public static readonly string[] NeedIds = { "energy", "relationships", "knowledge", "reputation" };
        static readonly Dictionary<string, string> NeedIcon = new Dictionary<string, string>
        {
            ["energy"] = "energy", ["relationships"] = "close_ones", ["knowledge"] = "knowledge", ["reputation"] = "reputation",
        };
        static readonly Dictionary<string, string> MoodIcon = new Dictionary<string, string>
        {
            ["burnout"] = "mood_burnout", ["shark_debt"] = "mood_loan_shark", ["in_debt"] = "loan", ["lonely"] = "trust_cold",
            ["paycheck"] = "salary", ["rested"] = "mood_slept", ["cushion"] = "reserve",
        };
        static readonly Dictionary<string, string> WantIcon = new Dictionary<string, string>
        {
            ["talk"] = "emote_scene", ["reserve"] = "reserve", ["lesson"] = "lesson", ["rest"] = "mood_slept", ["earn"] = "emote_work", ["study"] = "knowledge",
        };

        readonly UiRoot _ui;
        readonly Dictionary<string, int> _shown = new Dictionary<string, int>();
        readonly VisualElement _needsPanel, _moods, _wants, _wantsBody;
        readonly Label _wantsCount;
        readonly HashSet<string> _ticked = new HashSet<string>();
        readonly HashSet<string> _lessonsThisWeek = new HashSet<string>();
        bool _wantsOpen;
        int _wantsWeek = -1;

        public VisualElement NeedsPanel => _needsPanel;
        public VisualElement Moods => _moods;
        public VisualElement Wants => _wants;

        public HeroStateView(UiRoot ui, VisualElement hud)
        {
            _ui = ui;
            foreach (var id in NeedIds.Skip(1))
            {
                var stat = hud.Q("hud-stat-" + id);
                stat.Q(className: "hud__caption-row").Insert(0, Icons.Game(NeedIcon[id], "hud__stat-icon"));
                UiText.Bind(stat.Q<Label>("hud-" + id + "-caption"), "need." + id);
            }
            var stats = hud.Q("hud-stats");
            stats.RegisterCallback<ClickEvent>(_ => OpenNeeds());

            _needsPanel = BuildNeedsPanel();
            _moods = new VisualElement { name = "moods" };
            _moods.AddToClassList("moods");
            _wants = new VisualElement { name = "wants" };
            _wants.AddToClassList("wants");
            var head = new Button(() => { _wantsOpen = !_wantsOpen; _wantsBody.style.display = _wantsOpen ? DisplayStyle.Flex : DisplayStyle.None; }) { name = "wants-toggle" };
            head.AddToClassList("wants__head");
            head.Add(Icons.Game("advice", "wants__icon"));
            head.Add(UiText.Label("wants.title", "wants__title"));
            _wantsCount = UiText.ValueLabel("", "wants__count");
            _wantsCount.name = "wants-count";
            head.Add(_wantsCount);
            _wants.Add(head);
            _wantsBody = new VisualElement { name = "wants-body" };
            _wantsBody.style.display = DisplayStyle.None;
            _wants.Add(_wantsBody);
        }

        VisualElement BuildNeedsPanel()
        {
            var modal = new VisualElement { name = "needs-panel" };
            modal.AddToClassList("modal");
            modal.RegisterCallback<ClickEvent>(e => { if (e.target == modal) Close(); });
            var panel = new VisualElement { name = "needs-panel-body" };
            panel.AddToClassList("modal__panel");
            panel.Add(UiText.Label("needs.title", "section-title"));
            foreach (var id in NeedIds)
            {
                var row = new VisualElement { name = "need-row-" + id };
                row.AddToClassList("needs__row");
                row.Add(Icons.Game(NeedIcon[id], "needs__icon"));
                var text = new VisualElement();
                text.AddToClassList("needs__text");
                var name = UiText.Label("need." + id, "needs__name");
                text.Add(name);
                var up = UiText.Label("need." + id + ".up", "needs__line");
                up.name = "need-" + id + "-up";
                text.Add(up);
                var down = UiText.Label("need." + id + ".down", "needs__line");
                down.name = "need-" + id + "-down";
                down.AddToClassList("needs__line--down");
                text.Add(down);
                row.Add(text);
                panel.Add(row);
            }
            var ok = UiText.Bind(new Button(Close) { name = "needs-ok" }, "summary.ok");
            ok.AddToClassList("primary-button");
            panel.Add(ok);
            modal.Add(panel);
            modal.style.display = DisplayStyle.None;
            return modal;
        }

        /// <summary>The next refresh is a load (a plan restored), not news: no tick sound, no floating changes.</summary>
        public void Silence() { _wantsWeek = -1; _shown.Clear(); }

        public void OpenNeeds()
        {
            _needsPanel.style.display = DisplayStyle.Flex;
            _needsPanel.BringToFront();
            PopIn.Play(_needsPanel.Q("needs-panel-body"));
        }

        public void Close() => _needsPanel.style.display = DisplayStyle.None;

        /// <summary>Values, floating changes, badges and wants from the state; <paramref name="energyShown"/> is the energy the plan leaves.</summary>
        public void Refresh(GameState s, GameEngine engine, MonthReport last, IEnumerable<string> planned, int energyShown)
        {
            if (s == null || engine == null) return;
            var hud = _ui.Root.Q("hud");
            foreach (var n in HeroView.Needs(s, engine.Content.Balance))
            {
                int value = n.Id == "energy" ? energyShown : n.Value;
                var stat = hud.Q("hud-stat-" + n.Id);
                if (n.Id != "energy")
                {
                    stat.Q<Label>("hud-" + n.Id + "-value").text = value.ToString();
                    stat.Q("hud-" + n.Id + "-fill").style.width = UnityEngine.UIElements.Length.Percent(n.Bar);
                }
                stat.EnableInClassList("hud__stat--low", n.Low);
                // only real changes float (the week's result, a scene); the plan's energy preview reads "70 → 60" already
                if (_shown.TryGetValue(n.Id, out var before) && before != n.Value)
                    FloatingDelta.Show(stat, EffectChips.Signed(n.Value - before), n.Value > before);
                _shown[n.Id] = n.Value;
            }
            RefreshMoods(s, engine, last);
            RefreshWants(s, engine, planned);
        }

        void RefreshMoods(GameState s, GameEngine engine, MonthReport last)
        {
            _moods.Clear();
            foreach (var m in HeroView.Moods(s, engine.Content.Balance, last))
            {
                var id = m.Id;
                var chip = new Button(() => Toasts.Show(_ui.Root, "mood." + id + ".line", MoodIcon[id], 3500)) { name = "mood-" + id };
                chip.AddToClassList("mood");
                chip.AddToClassList(m.Good ? "mood--good" : "mood--bad");
                chip.Add(Icons.Game(MoodIcon[id], "mood__icon"));
                chip.Add(UiText.Label("mood." + id, "mood__name"));
                chip.Add(UiText.ValueLabel(m.Good ? "+" : EffectChips.Minus, "mood__sign"));
                _moods.Add(chip);
            }
            _moods.style.display = _moods.childCount > 0 && !_ui.RunMode ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void RefreshWants(GameState s, GameEngine engine, IEnumerable<string> planned)
        {
            bool first = _wantsWeek == -1;
            if (s.Week != _wantsWeek) { _wantsWeek = s.Week; _ticked.Clear(); _lessonsThisWeek.Clear(); }
            var wants = HeroView.Wants(s, engine.Content, planned);
            // a lesson finished this week stays as a ticked want (Core lists only open lessons)
            foreach (var l in wants.Where(w => w.Id == "lesson")) _lessonsThisWeek.Add(l.Arg);
            foreach (var arg in _lessonsThisWeek)
                if (!wants.Any(w => w.Id == "lesson" && w.Arg == arg) && wants.Count < HeroView.MaxWants)
                    wants.Add(new Want { Id = "lesson", Arg = arg, Done = true });
            _wantsBody.Clear();
            int done = 0;
            foreach (var w in wants)
            {
                string key = w.Id + ":" + w.Arg;
                var row = new VisualElement { name = "want-" + w.Id };
                row.AddToClassList("want");
                row.EnableInClassList("want--done", w.Done);
                var box = new VisualElement();
                box.AddToClassList("want__box");
                if (w.Done) box.Add(Icons.Service("check", "want__check"));
                row.Add(box);
                row.Add(Icons.Game(WantIcon[w.Id], "want__icon"));
                var text = UiText.Label(WantKey(w), "want__text");
                row.Add(text);
                if (w.Id == "study") row.Add(UiText.ValueLabel(w.Arg, "want__arg"));
                _wantsBody.Add(row);
                if (w.Done)
                {
                    done++;
                    if (_ticked.Add(key) && !first)   // a want already done on load is not news
                    {
                        UiSound.Play(UiSound.Success);
                        Toasts.Show(_ui.Root, "wants.done", WantIcon[w.Id], Toasts.ShowMs, sound: false);
                    }
                }
            }
            _wantsCount.text = $"{done}/{wants.Count}";
            _wants.style.display = wants.Count > 0 && !_ui.RunMode ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static string WantKey(Want w) => w.Id switch
        {
            "talk" => "want.talk." + w.Arg,
            "lesson" => "want.lesson",
            _ => "want." + w.Id,
        };
    }
}
