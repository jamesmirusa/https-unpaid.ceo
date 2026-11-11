using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Game.Bootstrap;
using UnpaidCeo.Game.Services;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>The real entry point: Boot scene → content, Bronx, UI, dialogue → onboarding for a new player.</summary>
    public class BootSmokeTests
    {
        string _savedSave, _savedMeta;
        int _savedConsent;

        [SetUp]
        public void KeepDeveloperSave()
        {
            _savedSave = PlayerPrefs.GetString(SaveService.Key, null);
            _savedMeta = PlayerPrefs.GetString(SaveService.MetaKey, null);
            _savedConsent = PlayerPrefs.GetInt(UnpaidCeo.Game.Services.Online.AnalyticsConsent.Key, -1);
            SaveService.Delete();
            PlayerPrefs.DeleteKey(SaveService.MetaKey);
            PlayerPrefs.DeleteKey(UnpaidCeo.Game.Services.Online.AnalyticsConsent.Key);   // a new player has not been asked yet
        }

        [UnityTearDown]
        public IEnumerator UnloadAndRestore()
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == "Boot" || scene.name == "Bronx") yield return SceneManager.UnloadSceneAsync(scene);
            }
            SaveService.Delete();
            if (!string.IsNullOrEmpty(_savedSave)) PlayerPrefs.SetString(SaveService.Key, _savedSave);
            if (string.IsNullOrEmpty(_savedMeta)) PlayerPrefs.DeleteKey(SaveService.MetaKey); else PlayerPrefs.SetString(SaveService.MetaKey, _savedMeta);
            if (_savedConsent < 0) PlayerPrefs.DeleteKey(UnpaidCeo.Game.Services.Online.AnalyticsConsent.Key);
            else PlayerPrefs.SetInt(UnpaidCeo.Game.Services.Online.AnalyticsConsent.Key, _savedConsent);
        }

        static bool Shown(VisualElement e)
        {
            for (var v = e; v != null; v = v.parent)
                if (v.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        static void Tap(Button b)
        {
            Assert.IsNotNull(b, "button missing");
            Assert.IsTrue(b.enabledInHierarchy && Shown(b), b.name + " must be visible and enabled");
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = b;
                b.SendEvent(e);
            }
        }

        static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (!done() && Time.realtimeSinceStartup - start < seconds) yield return null;
        }

        /// <summary>Plays the scenes on screen like a player: Continue, or the first choice, until the dialogue closes.</summary>
        static IEnumerator PlayScenes(VisualElement dialogueRoot)
        {
            var backdrop = dialogueRoot.Q("dialogue-backdrop");
            int quiet = 0;
            float start = Time.realtimeSinceStartup;
            while (quiet < 45 && Time.realtimeSinceStartup - start < 60f)
            {
                yield return null;
                if (!Shown(backdrop)) { quiet++; continue; }
                quiet = 0;
                var option = dialogueRoot.Q<Button>(className: "dialogue__option");
                var go = dialogueRoot.Q<Button>("dialogue-continue");
                if (option != null) Tap(option);
                else if (Shown(go)) Tap(go);
            }
            var stuckOn = dialogueRoot.Q<Label>("dialogue-text");
            Assert.IsFalse(Shown(backdrop), $"the scenes never ended; stuck on '{stuckOn?.text}', options: {dialogueRoot.Query<Button>(className: "dialogue__option").ToList().Count}, "
                + $"continue shown: {Shown(dialogueRoot.Q<Button>("dialogue-continue"))}, "
                + $"storylet: {Object.FindFirstObjectByType<UnpaidCeo.Game.Narrative.YarnBridge>()?.CurrentStorylet}, running: {Object.FindFirstObjectByType<UnpaidCeo.Game.Narrative.YarnBridge>()?.IsPlaying}");
        }

        static UnpaidCeo.Core.Content.ContentDb Content()
        {
            string dir = System.IO.Path.Combine(Application.dataPath, "UnpaidCeo/Content");
            string Read(string f) { var p = System.IO.Path.Combine(dir, f); return System.IO.File.Exists(p) ? System.IO.File.ReadAllText(p) : null; }
            return UnpaidCeo.Core.Content.ContentDb.FromJson(new UnpaidCeo.Core.Content.ContentFiles
            {
                Industries = Read("industries.json"), Balance = Read("balance.json"), FreeActions = Read("free_actions.json"),
                Storylets = Read("storylets_ch1.json"), Npcs = Read("npcs.json"), Lessons = Read("lessons.json"),
                Missions = Read("missions.json"),
            });
        }

        /// <summary>
        /// Two scenes in one month (the dice give that often): the second must start after the first, and after both the
        /// game must be free again. The second used to be started from inside the first one's completion, where the
        /// dialogue runner drops it: the panel stayed up with a dead button and "Next month" never worked again.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator A_month_with_two_scenes_plays_both_and_frees_the_game()
        {
            var state = new UnpaidCeo.Core.Game.GameEngine(Content()).NewGame(10, new UnpaidCeo.Core.Game.HeroAppearance());
            state.PendingStorylets.Add("ch1_mentor_welcome");
            state.PendingStorylets.Add("ch1_tony_intro");
            SaveService.Save(state);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/UnpaidCeo/Scenes/Boot.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            GameBootstrap boot = null;
            yield return Until(() => (boot = Object.FindFirstObjectByType<GameBootstrap>()) != null && boot.Current != GameBootstrap.Phase.Loading, 30f);
            Assert.AreEqual(GameBootstrap.Phase.Playing, boot.Current, "a saved game resumes");
            var ui = boot.Ui;
            var dialogue = Object.FindFirstObjectByType<UnpaidCeo.Game.Narrative.UitkDialoguePresenter>().GetComponent<UIDocument>().rootVisualElement;
            yield return Until(() => Shown(dialogue.Q("dialogue-backdrop")), 10f);
            Assert.IsTrue(Shown(dialogue.Q("dialogue-backdrop")), "the month's scenes open");

            yield return PlayScenes(dialogue);
            yield return Until(() => SaveService.Load().State.PendingStorylets.Count == 0, 5f);
            var saved = SaveService.Load().State;
            Assert.AreEqual(0, saved.PendingStorylets.Count, "both scenes were played to their choice");
            Assert.AreEqual(2, saved.Journal.Count(j => j.Kind == "decision"), "and both choices are in the journal");

            if (Shown(ui.Consent.Root)) Tap(ui.Root.Q<Button>("consent-deny"));
            yield return null;
            Tap(ui.NextMonthButton);
            yield return Until(() => ui.MonthSummaryOpen, 20f);
            Assert.IsTrue(ui.MonthSummaryOpen, "after the scenes Next month works");
            Tap(ui.Summary.Root.Q<Button>("month-summary-ok"));
            yield return PlayScenes(dialogue);
        }

        /// <summary>
        /// What the first player on the live site could not do: get past month 1. Hero, the mentor's three steps, the
        /// month result, the scenes, the question about statistics, and then a second month from the main screen.
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator A_new_player_gets_through_the_first_month_and_plays_the_second()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/UnpaidCeo/Scenes/Boot.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            GameBootstrap boot = null;
            yield return Until(() => (boot = Object.FindFirstObjectByType<GameBootstrap>()) != null && boot.Current != GameBootstrap.Phase.Loading, 30f);
            Assert.AreEqual(GameBootstrap.Phase.Onboarding, boot.Current);
            var ui = boot.Ui;
            var dialogue = Object.FindFirstObjectByType<UnpaidCeo.Game.Narrative.UitkDialoguePresenter>().GetComponent<UIDocument>().rootVisualElement;

            Tap(ui.Root.Q<Button>("hero-done"));
            yield return null;
            Tap(ui.Root.Q<Button>(OnboardingFlow.ActionButton));
            yield return null;
            Tap(ui.Root.Q<Button>(OnboardingFlow.ActionButton));
            yield return null;
            Tap(ui.NextMonthButton);
            yield return null;

            Assert.IsTrue(ui.MonthSummaryOpen, "month 1 ends in its result on the main screen");
            Assert.IsFalse(Shown(ui.Consent.Root), "one thing at a time: the statistics question waits");
            Assert.IsFalse(Shown(dialogue.Q("dialogue-backdrop")), "and so do the month's scenes");
            Tap(ui.Summary.Root.Q<Button>("month-summary-ok"));

            yield return PlayScenes(dialogue);
            yield return Until(() => Shown(ui.Consent.Root), 5f);
            Assert.IsTrue(Shown(ui.Consent.Root), "after the scenes the player is asked about statistics, once");
            Tap(ui.Root.Q<Button>("consent-deny"));
            yield return null;

            Assert.AreEqual(UnpaidCeo.Game.Ui.UiRoot.City, ui.CurrentTab, "the player is on the main screen");
            Assert.IsTrue(Shown(ui.Guide), "with the guide saying what to do next");
            Assert.AreEqual("2", ui.Root.Q<Label>("hud-month").text, "month 1 is done: the HUD shows the month being planned");

            Tap(ui.NextMonthButton);
            yield return null;
            Assert.IsFalse(ui.NextMonthButton.enabledSelf, "while the month plays the button shows it is busy");
            yield return Until(() => ui.MonthSummaryOpen, 20f);
            Assert.IsTrue(ui.MonthSummaryOpen, "month 2 was played and its result is on screen");
            Assert.AreEqual("3", ui.Root.Q<Label>("hud-month").text, "the calendar moved on");
            Tap(ui.Summary.Root.Q<Button>("month-summary-ok"));
            yield return PlayScenes(dialogue);
            yield return Until(() => ui.NextMonthButton.enabledSelf, 5f);
            Assert.IsTrue(ui.NextMonthButton.enabledSelf, "and the game is ready for month 3");
        }

        [UnityTest]
        public IEnumerator Boot_scene_starts_onboarding_for_a_new_player()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/UnpaidCeo/Scenes/Boot.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            GameBootstrap boot = null;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 30f)
            {
                boot = Object.FindFirstObjectByType<GameBootstrap>();
                if (boot != null && boot.Current != GameBootstrap.Phase.Loading) break;
                yield return null;
            }
            Assert.IsNotNull(boot, "Boot scene has no GameBootstrap");
            Assert.AreEqual(GameBootstrap.Phase.Onboarding, boot.Current, "a new player lands in onboarding");
            var done = boot.Ui.Root.Q<Button>("hero-done");
            Assert.IsNotNull(done, "hero creator is on screen");
            Assert.IsNotNull(Object.FindFirstObjectByType<Yarn.Unity.DialogueRunner>(), "dialogue is wired");
            var dialogue = Object.FindFirstObjectByType<UnpaidCeo.Game.Narrative.UitkDialoguePresenter>()
                .GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement.Q("dialogue");
            Assert.IsTrue(dialogue.ClassListContains("theme-day"), "the dialogue panel carries the theme, or it has no background and accent");
            Assert.AreEqual("dialogue-backdrop", dialogue.parent.name, "a scene is a modal: it covers the game, so no button under it looks alive");
            var go = dialogue.Q<Button>("dialogue-continue");
            Assert.IsTrue(UnpaidCeo.Game.Ui.UiText.IsLocalized(go), "Continue is a word, not a symbol the font may lack");

            var hero = Object.FindFirstObjectByType<UnpaidCeo.Game.Hero.HeroView>();
            var city = Object.FindFirstObjectByType<UnpaidCeo.Game.City.CityView>();
            Assert.Greater(Vector3.Distance(hero.transform.position, city.HomeAnchor.position), 4f, "the hero stands outside, not inside the home building");
            var rig = Object.FindFirstObjectByType<UnpaidCeo.Game.City.CameraRig>();
            Assert.Less(Vector3.Distance(rig.Focus, hero.transform.position), 8f, "the camera opens on the hero");
            float heroOnScreen = rig.GetComponent<Camera>().WorldToViewportPoint(hero.transform.position).y;
            Assert.That(heroOnScreen, Is.InRange(0.56f, 0.75f), "the hero shows above the middle: the guide and the buttons cover the bottom of the screen");
            var life = Object.FindFirstObjectByType<UnpaidCeo.Game.City.StreetLife>();
            Assert.IsNotNull(life, "the district has street life");
            Assert.GreaterOrEqual(life.Walkers.Count, 8, "people walk the block");
            Assert.GreaterOrEqual(Object.FindObjectsByType<UnpaidCeo.Game.City.StreetCar>(FindObjectsSortMode.None).Length, 2, "cars drive the avenue");

            Assert.IsNotNull(boot.Ui.Online, "screens always have online services (offline until UGS answers)");
            if (string.IsNullOrEmpty(Application.cloudProjectId))
            {
                yield return null;
                yield return null;
                Assert.IsFalse(boot.Online.Available, "an unlinked project plays offline");
                Assert.AreSame(boot.Online, boot.Ui.Online);
            }
        }
    }
}
