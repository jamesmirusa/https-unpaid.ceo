using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Game.Ui;
using UnpaidCeo.Game.Ui.Kit;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>
    /// docs/AUTOPILOT.md §8 · the top of the screen must not hide the city: a thin bar by default (money, week, four need
    /// rings, one menu button, a goal line), a tap opens the full panel, moving around the city clears the cards, the
    /// places strip is gone (places open as a sheet), danger still shows when folded.
    /// </summary>
    public class CompactHudTests
    {
        GameObject _host;
        UiRoot _ui;
        RenderTexture _target;
        GameEngine _engine;
        GameState _state;
        readonly List<string> _sounds = new List<string>();

        static ContentDb Content()
        {
            string dir = Path.Combine(Application.dataPath, "UnpaidCeo/Content");
            string Read(string f) { var p = Path.Combine(dir, f); return File.Exists(p) ? File.ReadAllText(p) : null; }
            return ContentDb.FromJson(new ContentFiles
            {
                Industries = Read("industries.json"), Balance = Read("balance.json"), FreeActions = Read("free_actions.json"),
                Storylets = Read("storylets_ch1.json"), Npcs = Read("npcs.json"), Lessons = Read("lessons.json"),
                Missions = Read("missions.json"),
            });
        }

        IEnumerator Boot(System.Action<GameState> setup = null, int width = 750, int height = 1334)
        {
            yield return LocalizationSettings.InitializationOperation;
            PlayerPrefs.DeleteKey(HudView.ExpandedPrefsKey);
            _sounds.Clear();
            UiSound.Sink = _sounds.Add;
            Haptics.Sink = _ => { };
            _target = new RenderTexture(width, height, 24);   // iPhone SE / 8: 375 × 667 pt
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            _engine = new GameEngine(Content());
            _state = _engine.NewGame(5, new HeroAppearance());
            _state.Life.Energy = 70;
            setup?.Invoke(_state);
            _ui.Initialize(_engine, _state, _target);
            yield return _ui.SetLanguage("ru");
            for (int i = 0; i < 6; i++) yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            UiSound.Sink = null;
            Haptics.Sink = null;
            PlayerPrefs.DeleteKey(HudView.ExpandedPrefsKey);
            if (_host != null) Object.Destroy(_host);
            if (_target != null) Object.Destroy(_target);
        }

        static void Click(VisualElement e)
        {
            Assert.IsNotNull(e);
            using (var ev = ClickEvent.GetPooled()) { ev.target = e; e.SendEvent(ev); }
        }

        float PanelHeight => _ui.Root.panel.visualTree.layout.height;
        VisualElement Expanded => _ui.Root.Q("hud-expanded");

        [UnityTest]
        public IEnumerator Folded_the_top_takes_at_most_12_percent_of_a_375_point_iPhone()
        {
            yield return Boot();
            Assert.IsFalse(_ui.Hud.IsExpanded, "folded by default");
            float bottom = _ui.Root.Q("hud").worldBound.yMax;
            Assert.LessOrEqual(bottom, PanelHeight * 0.12f, $"the top ends at {bottom:0} of {PanelHeight:0}");
            Assert.IsNull(_ui.Root.Q("city-strip"), "the places strip left the map (the Places tab has them)");
            var stack = _ui.Root.Q("city-stack");
            Assert.Greater(stack.worldBound.yMin - bottom, PanelHeight * 0.25f, "the city shows between the bar and the cards");
        }

        [UnityTest]
        public IEnumerator The_bar_shows_money_the_week_four_need_rings_and_one_menu()
        {
            yield return Boot(s => { s.Life.Relationships = 40; s.Cash = Money.FromDollars(640); });
            var bar = _ui.Root.Q("hud-bar");
            Assert.IsNotNull(bar.Q<Label>("hud-cash"));
            Assert.IsNotNull(bar.Q<Label>("hud-month"));
            foreach (var id in HeroStateView.NeedIds) Assert.IsNotNull(bar.Q<NeedRing>("hud-ring-" + id), id + " ring");
            Assert.AreEqual(0.4f, bar.Q<NeedRing>("hud-ring-relationships").Value, 0.01f);
            Assert.IsNotNull(bar.Q<Button>("hud-menu"));
            Assert.AreEqual(DisplayStyle.None, Expanded.resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator A_tap_opens_the_panel_and_a_tap_folds_it()
        {
            yield return Boot();
            Click(_ui.Root.Q("hud-bar"));
            yield return null;
            Assert.IsTrue(_ui.Hud.IsExpanded);
            Assert.AreEqual(DisplayStyle.Flex, Expanded.resolvedStyle.display);
            Assert.IsNotNull(Expanded.Q("hud-stats"), "bars with numbers");
            Assert.IsNotNull(Expanded.Q("calendar"), "and the money calendar");
            Assert.IsNotNull(Expanded.Q<Button>("hud-journal"), "journal and settings live inside");
            Click(_ui.Root.Q("hud-bar"));
            yield return null;
            Assert.IsFalse(_ui.Hud.IsExpanded);
        }

        [UnityTest]
        public IEnumerator The_menu_button_opens_the_panel_too()
        {
            yield return Boot();
            using (var e = NavigationSubmitEvent.GetPooled()) { var b = _ui.Root.Q<Button>("hud-menu"); e.target = b; b.SendEvent(e); }
            Assert.IsTrue(_ui.Hud.IsExpanded);
        }

        [UnityTest]
        public IEnumerator The_panel_folds_by_itself_on_a_tap_on_the_city_and_on_a_swipe_up()
        {
            yield return Boot();
            _ui.Hud.SetExpanded(true);
            yield return new WaitForSecondsRealtime(HudView.AutoFoldSeconds + 0.6f);
            Assert.IsFalse(_ui.Hud.IsExpanded, "folds itself after 5 s");
            _ui.Hud.SetExpanded(true);
            Click(_ui.Screen(UiRoot.City));
            Assert.IsFalse(_ui.Hud.IsExpanded, "a tap on the city folds it");
            _ui.Hud.SetExpanded(true);
            _ui.Hud.Swipe(-80f);
            Assert.IsFalse(_ui.Hud.IsExpanded, "a swipe up folds it");
        }

        [UnityTest]
        public IEnumerator The_choice_is_remembered()
        {
            yield return Boot();
            _ui.Hud.SetExpanded(true);
            Object.Destroy(_host);
            yield return null;
            _host = new GameObject("ui2");
            _ui = _host.AddComponent<UiRoot>();
            _ui.Initialize(_engine, _state, _target);
            yield return null;
            Assert.IsTrue(_ui.Hud.IsExpanded);
        }

        [UnityTest]
        public IEnumerator Moving_around_the_city_clears_the_cards_and_they_come_back()
        {
            yield return Boot();
            _ui.NotifyCityMotion();
            Assert.IsTrue(_ui.Root.ClassListContains(UiRoot.CityMovingClass), "the extras step aside while the camera moves");
            yield return new WaitForSecondsRealtime(UiRoot.CityStillSeconds + 0.4f);
            Assert.IsFalse(_ui.Root.ClassListContains(UiRoot.CityMovingClass), "and come back 1.5 s after it stops");
        }

        [UnityTest]
        public IEnumerator Danger_shows_even_when_folded()
        {
            yield return Boot(s => s.Life.Energy = 12);
            var ring = _ui.Root.Q<NeedRing>("hud-ring-energy");
            Assert.IsTrue(ring.ClassListContains(HudView.AlarmClass), "a low need pulses");
            var toast = _ui.Root.Q<Label>("toast");
            Assert.IsNotNull(toast, "and says so");
            Assert.IsTrue(UiText.IsLocalized(toast));
        }

        [UnityTest]
        public IEnumerator A_money_date_this_week_pulses_the_week()
        {
            yield return Boot(s => { s.LifetimeEarnings = Money.FromDollars(100_000); s.Week = 4; s.Cash = Money.FromDollars(5_000); });
            Assert.IsTrue(_ui.Root.Q("hud-week-pill").ClassListContains(HudView.AlarmClass), "rent is due this week");
        }

        [UnityTest]
        public IEnumerator Places_open_as_a_sheet_from_the_dock()
        {
            yield return Boot();
            _ui.OpenPlaces();
            yield return null;
            Assert.IsTrue(_ui.Screen(UiRoot.PlacesTab).ClassListContains("screen--sheet"));
        }
    }
}
