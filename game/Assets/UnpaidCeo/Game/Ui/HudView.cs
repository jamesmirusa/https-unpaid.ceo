using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Game.Ui.Kit;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// docs/AUTOPILOT.md §8 · the top of the screen as a thin bar by default: money (with its floating change), the week
    /// with its action dots, four need rings, one menu button and a goal line. A tap (or the menu) opens the full panel —
    /// need bars with numbers, the money calendar, the chapter goal, journal and settings — with a bounce; a tap on the
    /// bar or the city, a swipe up, or 5 s fold it again. Danger never hides: a low need's ring and a money week pulse,
    /// with a note once a week. The folded/open choice is remembered on this device.
    /// </summary>
    public sealed class HudView
    {
        public const string ExpandedPrefsKey = "unpaidceo.hud.expanded";
        public const string AlarmClass = "hud--alarm", ExpandedClass = "hud--expanded", BeatClass = "hud--beat", OpenClass = "hud-open";
        public const float AutoFoldSeconds = 5f, SwipeFold = -40f;

        static readonly Dictionary<string, (string icon, Color color)> Rings = new Dictionary<string, (string, Color)>
        {
            ["energy"] = ("energy", new Color(0.95f, 0.6f, 0.29f)),
            ["relationships"] = ("close_ones", new Color(1f, 0.56f, 0.66f)),
            ["knowledge"] = ("knowledge", new Color(0.73f, 0.64f, 1f)),
            ["reputation"] = ("reputation", new Color(1f, 0.82f, 0.48f)),
        };

        readonly UiRoot _ui;
        readonly VisualElement _hud, _expanded, _weekPill, _goalFill;
        readonly Dictionary<string, NeedRing> _rings = new Dictionary<string, NeedRing>();
        readonly HashSet<string> _warned = new HashSet<string>();
        IVisualElementScheduledItem _fold;
        int _warnedWeek = -1;
        float _downY;
        bool _swiped;
        IVisualElementScheduledItem _beat;

        public bool IsExpanded { get; private set; }
        public VisualElement Expanded => _expanded;
        public NeedRing Ring(string id) => _rings.TryGetValue(id, out var r) ? r : null;

        public HudView(UiRoot ui, VisualElement hud)
        {
            _ui = ui;
            _hud = hud;
            _expanded = hud.Q("hud-expanded");
            _weekPill = hud.Q("hud-week-pill");
            _goalFill = hud.Q("hud-goal-fill");
            var rings = hud.Q("hud-rings");
            foreach (var id in HeroStateView.NeedIds)
            {
                var r = new NeedRing(id, Rings[id].icon, Rings[id].color);
                _rings[id] = r;
                rings.Add(r);
            }
            var menu = hud.Q<Button>("hud-menu");
            menu.clicked += Toggle;
            menu.Add(Icons.Service("list"));   // icon only: the ≡ everyone reads as "more"
            var bar = hud.Q("hud-bar");
            bar.RegisterCallback<ClickEvent>(e => { if (Feel.ButtonOf(e.target as VisualElement) == null) Tapped(); });
            hud.Q("hud-goal").RegisterCallback<ClickEvent>(_ => Tapped());
            hud.RegisterCallback<PointerDownEvent>(e => { _downY = e.position.y; _swiped = false; }, TrickleDown.TrickleDown);
            hud.RegisterCallback<PointerUpEvent>(e => Swipe(e.position.y - _downY), TrickleDown.TrickleDown);
            _beat = hud.schedule.Execute(() => _hud.ToggleInClassList(BeatClass)).Every(600);   // the danger pulse
            _beat.Pause();
            bool remembered = PlayerPrefs.GetInt(ExpandedPrefsKey, 0) == 1;
            SetExpanded(remembered, remember: false);   // opened last time: open again, and fold by itself as usual
        }

        public void Toggle() => SetExpanded(!IsExpanded);

        /// <summary>A tap on the bar — unless that same touch was a swipe up that just folded it.</summary>
        void Tapped() { if (_swiped) { _swiped = false; return; } Toggle(); }

        /// <summary>A vertical drag on the panel: up (negative) past the threshold folds it.</summary>
        public void Swipe(float dy) { if (IsExpanded && dy < SwipeFold) { SetExpanded(false); _swiped = true; } }

        public void SetExpanded(bool on, bool remember = true, bool autoFold = true)
        {
            bool changed = on != IsExpanded;
            IsExpanded = on;
            _hud.EnableInClassList(ExpandedClass, on);
            _ui.Root?.EnableInClassList(OpenClass, on);   // the city's own buttons and cards step aside under the open panel
            _expanded.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            _fold?.Pause();
            if (on)
            {
                if (changed) PopIn.Play(_expanded, UiSound.Pop);
                if (autoFold) _fold = _hud.schedule.Execute(() => SetExpanded(false)).StartingIn((long)(AutoFoldSeconds * 1000));
            }
            if (remember)
            {
                PlayerPrefs.SetInt(ExpandedPrefsKey, on ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Rings, the goal line and the danger signals from the state.</summary>
        public void Refresh(GameState s, GameEngine engine, double goalProgress, int energyShown)
        {
            if (s == null || engine == null) return;
            if (s.Week != _warnedWeek) { _warnedWeek = s.Week; _warned.Clear(); }
            foreach (var n in HeroView.Needs(s, engine.Content.Balance))
            {
                var ring = _rings[n.Id];
                ring.Value = (n.Id == "energy" ? energyShown : n.Bar) / 100f;
                ring.EnableInClassList(AlarmClass, n.Low);
                if (n.Low && !_ui.RunMode && _warned.Add("need:" + n.Id)) Toasts.Show(_ui.Root, "need." + n.Id + ".low", Rings[n.Id].icon, 2000);
            }
            var now = MoneyCalendar.Month(s, engine.Content.Balance).Find(w => w.Now);
            bool moneyWeek = now != null && now.Headline != null && now.Headline != "bill.food";
            _weekPill.EnableInClassList(AlarmClass, moneyWeek && !_ui.RunMode);
            if (moneyWeek && !_ui.RunMode && _warned.Add("money")) Toasts.Show(_ui.Root, "toast.money_week", "money_date", 2000);
            bool danger = _weekPill.ClassListContains(AlarmClass);
            foreach (var r in _rings.Values) danger |= r.ClassListContains(AlarmClass);
            if (danger) _beat.Resume(); else { _beat.Pause(); _hud.RemoveFromClassList(BeatClass); }   // pulse only while in danger
            _goalFill.style.width = Length.Percent((float)(goalProgress * 100));
            _hud.Q("hud-goal").style.display = _ui.RunMode ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
