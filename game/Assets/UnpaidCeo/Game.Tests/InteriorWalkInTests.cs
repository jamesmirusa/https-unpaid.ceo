using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Platform;
using UnpaidCeo.Game.Bootstrap;
using UnpaidCeo.Game.City;
using UnpaidCeo.Game.Graphics;
using UnpaidCeo.Game.Services;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>The whole game: a sign on the street walks the hero into the place's room, and leaving puts them back outside.</summary>
    public class InteriorWalkInTests
    {
        static readonly string[] DistrictScenes = { "Bronx", "Brooklyn", "Queens" };
        string _savedSave, _savedMeta;

        static ContentDb Content()
        {
            string dir = Path.Combine(Application.dataPath, "UnpaidCeo/Content");
            string Read(string f) { var p = Path.Combine(dir, f); return File.Exists(p) ? File.ReadAllText(p) : null; }
            return ContentDb.FromJson(new ContentFiles
            {
                Industries = Read("industries.json"), Balance = Read("balance.json"), FreeActions = Read("free_actions.json"),
                Storylets = Read("storylets_ch1.json"), Npcs = Read("npcs.json"), Lessons = Read("lessons.json"), Missions = Read("missions.json"),
            });
        }

        [SetUp]
        public void KeepDeveloperSave()
        {
            _savedSave = PlayerPrefs.GetString(SaveService.Key, null);
            _savedMeta = PlayerPrefs.GetString(SaveService.MetaKey, null);
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            if (boot != null) Object.Destroy(boot.gameObject);
            yield return null;
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if ((DistrictScenes.Contains(scene.name) || scene.name == "Boot") && SceneManager.sceneCount > 1) yield return SceneManager.UnloadSceneAsync(scene);
            }
            Shader.SetGlobalFloat(DayNeonController.NightProperty, 0f);
            GraphicsModeService.Apply(GraphicsMode.Optimal);
            CameraRig.InputBlocked = false;
            Restore(SaveService.Key, _savedSave);
            Restore(SaveService.MetaKey, _savedMeta);
        }

        static void Restore(string key, string value)
        {
            if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey(key); else PlayerPrefs.SetString(key, value);
        }

        static bool Shown(VisualElement e)
        {
            for (var v = e; v != null; v = v.parent)
                if (v.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        static IEnumerator Boot(GameState s)
        {
            SaveService.Save(s);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/UnpaidCeo/Scenes/Boot.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            for (float t = 0f; t < 30f; t += Time.unscaledDeltaTime)
            {
                var boot = Object.FindFirstObjectByType<GameBootstrap>();
                if (boot != null && boot.Current != GameBootstrap.Phase.Loading) break;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator A_sign_takes_the_hero_into_the_room_to_the_host_and_leaving_puts_them_outside_it()
        {
            yield return Boot(new GameEngine(Content()).NewGame(11, new UnpaidCeo.Core.Game.HeroAppearance()));
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            Assert.AreEqual(GameBootstrap.Phase.Playing, boot.Current, "a saved player resumes");
            Assert.IsTrue(boot.Ui.Enter3D, "in the career a sign walks the hero in");
            var cityCam = Object.FindFirstObjectByType<CameraRig>().GetComponent<Camera>();
            for (float t = 0f; t < 10f && boot.Hero.Model == null; t += Time.unscaledDeltaTime) yield return null;

            yield return boot.EnterPlace("bodega");
            var stage = Object.FindFirstObjectByType<InteriorStage>();
            Assert.IsTrue(stage != null && stage.Showing, "the bodega's room is up");
            Assert.IsNotNull(stage.Host, "Tony stands in his shop");
            Assert.AreEqual("npc_tony_bulldog", stage.Host.name);
            Assert.IsNotNull(stage.Hero, "the hero stands with him");
            Assert.IsNotNull(stage.Hero.GetComponentInChildren<UnpaidCeo.Game.Hero.CharacterAnimator>(), "and can still move");
            Assert.IsFalse(cityCam.enabled, "the street is not drawn behind the room");
            Assert.IsTrue(Shown(boot.Ui.Root.Q("visit")), "the conversation runs under them");
            Assert.IsTrue(CameraRig.InputBlocked, "dragging does not move the street camera while inside");

            boot.Ui.Visit.Close();
            for (float t = 0f; t < 5f && stage.Showing; t += Time.unscaledDeltaTime) yield return null;
            Assert.IsFalse(stage.Showing, "leaving closes the room");
            Assert.IsTrue(cityCam.enabled, "the street is back");
            Assert.AreEqual(boot.Ui.PopupOpen, CameraRig.InputBlocked, "the street camera moves again (unless a popup is up)");
            var door = boot.PlaceSpots["bodega"];
            var at = boot.Hero.transform.position;
            Assert.Less(Vector2.Distance(new Vector2(at.x, at.z), new Vector2(door.x, door.z)), 8f, "the hero comes out at the bodega's door");
        }

        [UnityTest]
        public IEnumerator Ms_Lee_receives_the_hero_in_the_bank()
        {
            yield return Boot(new GameEngine(Content()).NewGame(12, new UnpaidCeo.Core.Game.HeroAppearance()));
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            yield return boot.EnterPlace("bank");
            var stage = Object.FindFirstObjectByType<InteriorStage>();
            Assert.IsTrue(stage != null && stage.Showing, "the bank's room is up");
            Assert.AreEqual("npc_ms_lee_owl", stage.Host?.name, "the banker stands behind her bank");
            Assert.IsNotNull(stage.Host.GetComponentInChildren<UnpaidCeo.Game.Hero.CharacterAnimator>(), "and moves");
            boot.Ui.Visit.Close();
            for (float t = 0f; t < 5f && stage.Showing; t += Time.unscaledDeltaTime) yield return null;
            Assert.IsFalse(stage.Showing);
        }
    }
}
