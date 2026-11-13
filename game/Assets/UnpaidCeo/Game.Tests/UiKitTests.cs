using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Game.Ui;
using UnpaidCeo.Game.Ui.Kit;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>
    /// Stage 5B step 1 · the UI kit and game feel: icons on the buttons, a tap that answers in the same frame (sinking
    /// button, sound, haptic), numbers that roll with a floating change, bouncy popups, toasts, a counter on the dock.
    /// </summary>
    public class UiKitTests
    {
        GameObject _host;
        UiRoot _ui;
        RenderTexture _target;
        GameEngine _engine;
        GameState _state;
        readonly List<string> _sounds = new List<string>();
        readonly List<Haptics.Kind> _haptics = new List<Haptics.Kind>();

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

        IEnumerator Boot()
        {
            yield return LocalizationSettings.InitializationOperation;
            _sounds.Clear();
            _haptics.Clear();
            UiSound.Sink = _sounds.Add;
            Haptics.Sink = _haptics.Add;
            _target = new RenderTexture(1080, 1920, 24);
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            _engine = new GameEngine(Content());
            _state = _engine.NewGame(5, new HeroAppearance());
            _ui.Initialize(_engine, _state, _target);
            yield return _ui.SetLanguage("en");
            for (int i = 0; i < 5; i++) yield return null;
            _sounds.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            UiSound.Sink = null;
            Haptics.Sink = null;
            if (_host != null) Object.Destroy(_host);
            if (_target != null) Object.Destroy(_target);
        }

        static void PointerDown(VisualElement e)
        {
            using (var ev = PointerDownEvent.GetPooled()) { ev.target = e; e.SendEvent(ev); }
        }

        static void PointerUp(VisualElement e)
        {
            using (var ev = PointerUpEvent.GetPooled()) { ev.target = e; e.SendEvent(ev); }
        }

        [Test]
        public void Every_icon_the_ui_names_exists()
        {
            foreach (var n in Icons.ServiceNames) Assert.IsNotNull(Resources.Load<Texture2D>(Icons.ServiceFolder + n), "service icon " + n);
            foreach (var n in Icons.GameNames) Assert.IsNotNull(Resources.Load<Texture2D>(Icons.GameFolder + n), "game icon " + n);
        }

        [Test]
        public void Every_ui_sound_exists()
        {
            foreach (var s in UiSound.All) Assert.IsNotNull(UiSound.Clip(s), "sound " + s);
        }

        [UnityTest]
        public IEnumerator A_press_sinks_the_button_and_answers_at_once()
        {
            yield return Boot();
            var b = _ui.Root.Q<Button>("nav-decisions");
            PointerDown(b);
            Assert.IsTrue(b.ClassListContains(Feel.PressedClass), "the button sinks in the same frame");
            CollectionAssert.Contains(_sounds, UiSound.Tap);
            CollectionAssert.Contains(_haptics, Haptics.Kind.Light);
            PointerUp(b);
            Assert.IsFalse(b.ClassListContains(Feel.PressedClass), "and comes back up on release");
        }

        [UnityTest]
        public IEnumerator A_closed_button_answers_with_the_closed_sound_not_a_tap()
        {
            yield return Boot();
            var b = new Button { name = "closed-test" };
            b.SetEnabled(false);
            _ui.Root.Add(b);
            yield return null;
            PointerDown(b);
            CollectionAssert.Contains(_sounds, UiSound.Closed);
            CollectionAssert.DoesNotContain(_sounds, UiSound.Tap);
            Assert.IsFalse(b.ClassListContains(Feel.PressedClass));
        }

        [UnityTest]
        public IEnumerator Cash_rolls_to_its_new_value_and_the_change_floats_up()
        {
            yield return Boot();
            var cash = _ui.Root.Q<Label>("hud-cash");
            _state.Cash += Money.FromDollars(51);
            _ui.RefreshHud();
            var delta = _ui.Root.Q<Label>("delta");
            Assert.IsNotNull(delta, "the change floats next to the cash");
            Assert.AreEqual("+" + MoneyFormat.Compact(Money.FromDollars(51), "en"), delta.text);
            Assert.IsTrue(delta.ClassListContains(FloatingDelta.Positive));
            CollectionAssert.Contains(_sounds, UiSound.Coin, "money coming in chimes");
            yield return new WaitForSecondsRealtime(NumberRoll.Seconds + 0.2f);
            Assert.AreEqual(MoneyFormat.Compact(_state.Cash, "en"), cash.text, "the roll ends exactly on the new value");
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsNull(_ui.Root.Q<Label>("delta"), "the floating change goes away by itself");

            _ui.RefreshHud();
            Assert.IsNull(_ui.Root.Q<Label>("delta"), "no change, nothing floats");
        }

        [UnityTest]
        public IEnumerator Hud_buttons_and_the_dock_carry_icons()
        {
            yield return Boot();
            foreach (var n in new[] { "hud-journal", "hud-settings", "nav-places", "nav-decisions", "nav-company", "nav-academy", "nav-ranking", "next-month" })
                Assert.IsNotNull(_ui.Root.Q(n).Q(className: "icon-slot"), n + " has an icon");
            Assert.IsNotNull(_ui.Root.Q("hud").Q(className: "game-icon"), "the cash has its coin");
        }

        [UnityTest]
        public IEnumerator A_popup_opens_with_a_bounce()
        {
            yield return Boot();
            _ui.Open(UiRoot.Decisions);
            yield return null;
            yield return null;
            Assert.IsTrue(_ui.Screen(UiRoot.Decisions).ClassListContains("popup--in"));
            CollectionAssert.Contains(_sounds, UiSound.Pop);
            _ui.Open(UiRoot.City);
            Assert.IsFalse(_ui.Screen(UiRoot.Decisions).ClassListContains("popup--in"));
        }

        [UnityTest]
        public IEnumerator A_toast_goes_away_by_itself()
        {
            yield return Boot();
            Toasts.Show(_ui.Root, "toast.new_lesson");
            Assert.IsNotNull(_ui.Root.Q<Label>("toast"));
            yield return new WaitForSecondsRealtime(Toasts.ShowMs / 1000f + 0.6f);
            Assert.IsNull(_ui.Root.Q<Label>("toast"));
        }

        [UnityTest]
        public IEnumerator Settings_can_turn_the_sounds_off_and_on()
        {
            yield return Boot();
            bool was = UiSound.Muted;
            try
            {
                _ui.Open(UiRoot.Settings);
                yield return null;
                var off = _ui.Root.Q<Button>("settings-sound-off");
                var on = _ui.Root.Q<Button>("settings-sound-on");
                using (var e = NavigationSubmitEvent.GetPooled()) { e.target = off; off.SendEvent(e); }
                Assert.IsTrue(UiSound.Muted);
                Assert.IsTrue(off.ClassListContains("choice-button--selected"));
                using (var e = NavigationSubmitEvent.GetPooled()) { e.target = on; on.SendEvent(e); }
                Assert.IsFalse(UiSound.Muted);
                Assert.IsTrue(on.ClassListContains("choice-button--selected"));
            }
            finally { UiSound.Muted = was; }
        }

        [UnityTest]
        public IEnumerator The_week_tab_counts_the_actions_left()
        {
            yield return Boot();
            var badge = _ui.Root.Q<Label>("nav-badge-decisions");
            Assert.IsNotNull(badge);
            Assert.AreEqual("2", badge.text);
            Assert.AreEqual(DisplayStyle.Flex, badge.resolvedStyle.display);
        }
    }
}
