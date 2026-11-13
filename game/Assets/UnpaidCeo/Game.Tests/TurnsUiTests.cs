using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Daily turns in the HUD and the daily missions panel on the City screen (stage 2A, Task 8).</summary>
    public class TurnsUiTests
    {
        GameObject _host;
        UiRoot _ui;
        RenderTexture _target;
        GameEngine _engine;
        GameState _state;

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
            _target = new RenderTexture(1080, 1920, 24);
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            _engine = new GameEngine(Content());
            _state = _engine.NewGame(5, new HeroAppearance());
            _ui.Initialize(_engine, _state, _target);
            yield return _ui.SetLanguage("en");
            for (int i = 0; i < 5; i++) yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            if (_target != null) Object.Destroy(_target);
        }

        [UnityTest]
        public IEnumerator Next_month_is_disabled_with_a_countdown_when_no_turns_left()
        {
            yield return Boot();
            TestGate.Satisfy(_ui, _engine.Content, _state);
            _ui.SetTurns(0, 6, 3723);
            yield return null;
            Assert.IsFalse(_ui.NextMonthButton.enabledSelf, "no turns: the button is off");
            Assert.AreEqual(DisplayStyle.Flex, _ui.CountdownRow.resolvedStyle.display);
            Assert.AreEqual("01:02:03", _ui.CountdownValue.text);
            Assert.AreEqual("0/6", _ui.TurnsValue.text);

            _ui.SetTurns(4, 6, 0);
            yield return null;
            Assert.IsTrue(_ui.NextMonthButton.enabledSelf);
            Assert.AreEqual(DisplayStyle.None, _ui.CountdownRow.resolvedStyle.display);
            Assert.AreEqual("4/6", _ui.TurnsValue.text);
        }

        [UnityTest]
        public IEnumerator Missions_step_aside_while_a_scene_is_on_screen()
        {
            yield return Boot();
            _ui.SetMissions(new[]
            {
                new MissionView("mission.months_3", 1, 3, false),
                new MissionView("mission.quiz_1", 1, 1, true),
                new MissionView("mission.scenes_1", 0, 1, false),
            }, streak: 4, ceoPoints: 12);
            _ui.Open(UiRoot.City);
            Assert.AreEqual(DisplayStyle.Flex, _ui.MissionsPanel.style.display.value);

            _ui.SetSceneOpen(true);   // the dialogue is drawn where the missions are
            Assert.AreEqual(DisplayStyle.None, _ui.MissionsPanel.style.display.value);
            _ui.SetMissions(new[]
            {
                new MissionView("mission.months_3", 1, 3, false),
                new MissionView("mission.quiz_1", 1, 1, true),
                new MissionView("mission.scenes_1", 0, 1, false),
            }, streak: 4, ceoPoints: 12);
            Assert.AreEqual(DisplayStyle.None, _ui.MissionsPanel.style.display.value, "a refresh during the scene keeps them hidden");

            _ui.SetSceneOpen(false);
            Assert.AreEqual(DisplayStyle.Flex, _ui.MissionsPanel.style.display.value);
        }

        [UnityTest]
        public IEnumerator City_shows_todays_missions_with_progress_and_streak()
        {
            yield return Boot();
            _ui.SetMissions(new[]
            {
                new MissionView("mission.months_3", 1, 3, false),
                new MissionView("mission.quiz_1", 1, 1, true),
                new MissionView("mission.scenes_1", 0, 1, false),
            }, streak: 4, ceoPoints: 12);
            for (int i = 0; i < 3; i++) yield return null;

            _ui.Open(UiRoot.City);
            var rows = _ui.MissionsPanel.Query(className: "mission").ToList();
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual(1, rows.Count(r => r.ClassListContains("mission--done")));
            var labels = _ui.MissionsPanel.Query<Label>().ToList();
            Assert.IsTrue(labels.Any(l => l.text == "1/3"));
            Assert.IsTrue(labels.Any(l => l.text == "4"), "streak");
            Assert.IsTrue(labels.Where(l => UiText.IsLocalized(l)).All(l => !string.IsNullOrEmpty(l.text) && !l.text.StartsWith("mission.")),
                "every mission title comes from the string table");
        }
    }
}
