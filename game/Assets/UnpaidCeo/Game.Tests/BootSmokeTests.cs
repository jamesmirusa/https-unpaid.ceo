using System.Collections;
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
        string _savedSave;

        [SetUp]
        public void KeepDeveloperSave()
        {
            _savedSave = PlayerPrefs.GetString(SaveService.Key, null);
            SaveService.Delete();
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
