using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// <summary>Stage 5B step 4: four need bars, the needs panel, mood badges and the week's wants.</summary>
    public class HeroStateUiTests
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

        IEnumerator Boot(System.Action<GameState> setup = null)
        {
            yield return LocalizationSettings.InitializationOperation;
            _sounds.Clear();
            UiSound.Sink = _sounds.Add;
            Haptics.Sink = _ => { };
            _target = new RenderTexture(1080, 1920, 24);
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            _engine = new GameEngine(Content());
            _state = _engine.NewGame(5, new HeroAppearance());
            _state.Life.Energy = 70;
            setup?.Invoke(_state);
            _ui.Initialize(_engine, _state, _target);
            yield return _ui.SetLanguage("en");
            for (int i = 0; i < 5; i++) yield return null;
            _sounds.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(HudView.ExpandedPrefsKey);
            UiSound.Sink = null;
            Haptics.Sink = null;
            if (_host != null) Object.Destroy(_host);
            if (_target != null) Object.Destroy(_target);
        }

        static void Click(VisualElement e)
        {
            Assert.IsNotNull(e);
            using (var ev = ClickEvent.GetPooled()) { ev.target = e; e.SendEvent(ev); }
        }

        [UnityTest]
        public IEnumerator The_hud_shows_four_needs()
        {
            yield return Boot(s => { s.Life.Relationships = 72; s.StudyXp = 34; s.Life.Reputation = 58; });
            Assert.AreEqual("72", _ui.Root.Q<Label>("hud-relationships-value").text);
            Assert.AreEqual("34", _ui.Root.Q<Label>("hud-knowledge-value").text);
            Assert.AreEqual("58", _ui.Root.Q<Label>("hud-reputation-value").text);
            Assert.IsNotNull(_ui.Root.Q("hud-stat-reputation").Q(className: "game-icon"));
        }

        [UnityTest]
        public IEnumerator A_need_that_changes_floats_its_change()
        {
            yield return Boot(s => s.Life.Relationships = 40);
            _state.Life.Relationships = 48;
            _ui.RefreshHud();
            var delta = _ui.Root.Q("hud-ring-relationships").Q<Label>("delta");
            Assert.IsNotNull(delta, "folded (§8): the change floats over the need's ring");
            Assert.AreEqual("+8", delta.text);
            _ui.Hud.SetExpanded(true);
            _state.Life.Relationships = 50;
            _ui.RefreshHud();
            Assert.AreEqual("+2", _ui.Root.Q("hud-stat-relationships").Q<Label>("delta").text, "open: over its bar");
        }

        [UnityTest]
        public IEnumerator Planning_a_deed_does_not_float_an_energy_change()
        {
            yield return Boot();
            _ui.DecisionsPanel.Plan("overtime");
            yield return null;
            Assert.IsNull(_ui.Root.Q("hud-stat-energy").Q<Label>("delta") ?? _ui.Root.Q("hud-ring-energy").Q<Label>("delta"), "the plan's preview reads \"70 → 60\"; only real changes float");
        }

        [UnityTest]
        public IEnumerator A_reload_with_a_want_done_plays_no_tick()
        {
            yield return LocalizationSettings.InitializationOperation;
            yield return Boot(s => { s.ActionLastWeek.Clear(); });
            _ui.DecisionsPanel.Plan("overtime");
            yield return null;
            var meta = new MetaProgress { WeekPlan = _ui.DecisionsPanel.Snapshot() };
            Object.Destroy(_host);
            yield return null;
            _sounds.Clear();
            _host = new GameObject("ui2");
            _ui = _host.AddComponent<UiRoot>();
            _ui.Initialize(_engine, _state, _target);
            _ui.Meta = meta;   // the game restores the week's plan from the meta save after the screens are built
            yield return null;
            CollectionAssert.DoesNotContain(_sounds, UiSound.Success, "a want already done on load is not news");
        }

        [UnityTest]
        public IEnumerator A_tap_on_the_needs_explains_each_one()
        {
            yield return Boot();
            Click(_ui.Root.Q("hud-stats"));
            yield return null;
            var panel = _ui.Root.Q("needs-panel");
            Assert.AreEqual(DisplayStyle.Flex, panel.resolvedStyle.display);
            Assert.AreEqual(4, panel.Query(className: "needs__row").ToList().Count);
            Assert.IsTrue(UiText.IsLocalized(panel.Q<Label>("need-energy-down")), "what happens when it falls");
        }

        [UnityTest]
        public IEnumerator Mood_badges_show_the_state_and_a_tap_says_one_line()
        {
            yield return Boot(s => s.Life.Energy = 10);
            var burnout = _ui.Root.Q<Button>("mood-burnout");
            Assert.IsNotNull(burnout);
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = burnout; burnout.SendEvent(e); }
            var toast = _ui.Root.Q<Label>("toast");
            Assert.IsNotNull(toast);
            Assert.IsTrue(UiText.IsLocalized(toast));
            Assert.LessOrEqual(_ui.Root.Query(className: "mood").ToList().Count, 4);
        }

        [UnityTest]
        public IEnumerator Wants_tick_when_the_week_takes_them()
        {
            yield return Boot();
            var count = _ui.Root.Q<Label>("wants-count");
            Assert.AreEqual("0/3", count.text);
            _ui.DecisionsPanel.Plan("overtime");
            yield return null;
            StringAssert.StartsWith("1/", count.text);
            CollectionAssert.Contains(_sounds, UiSound.Success, "a tick sounds");
        }
    }
}
