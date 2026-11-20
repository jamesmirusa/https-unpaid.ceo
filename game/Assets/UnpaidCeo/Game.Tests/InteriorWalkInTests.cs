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

        static void Submit(Button b)
        {
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        /// <summary>
        /// Owner, 2026-11-19: no sign on the street could be tapped. Walk mode (§8) hid the signs and the cards while "the
        /// hero moves" — and the hero's idle stroll in front of home never stops, so they were hidden all the time.
        /// </summary>
        [UnityTest]
        public IEnumerator Standing_on_the_street_keeps_the_signs_and_cards()
        {
            yield return BootPlaying(14);
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            int moving = 0, frames = 0;
            for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
            {
                yield return null;
                frames++;
                if (t > 1.6f && boot.Ui.Root.ClassListContains(UnpaidCeo.Game.Ui.UiRoot.CityMovingClass)) moving++;
            }
            Assert.AreEqual(0, moving, $"nobody touches anything, yet the city counts as moving on {moving} of {frames} frames");

            var rig = Object.FindFirstObjectByType<CameraRig>();
            rig.Frame(boot.Hero.transform.position + new Vector3(8f, 0f, 0f), 40f);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(boot.Ui.Root.ClassListContains(UnpaidCeo.Game.Ui.UiRoot.CityMovingClass), "moving the camera still clears the extras");
        }

        /// <summary>A saved player on the street, done with the month's scenes and the statistics question, as a player would be.</summary>
        static IEnumerator BootPlaying(ulong seed)
        {
            yield return Boot(new GameEngine(Content()).NewGame(seed, new UnpaidCeo.Core.Game.HeroAppearance()));
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            Assert.AreEqual(GameBootstrap.Phase.Playing, boot.Current, "a saved player resumes");
            for (float t = 0f; t < 10f && boot.Hero.Model == null; t += Time.unscaledDeltaTime) yield return null;
            var dialogue = Object.FindFirstObjectByType<UnpaidCeo.Game.Narrative.UitkDialoguePresenter>().GetComponent<UIDocument>().rootVisualElement;
            int quiet = 0;
            for (float t = 0f; quiet < 45 && t < 60f; t += Time.unscaledDeltaTime)
            {
                yield return null;
                if (!CameraRig.SceneOpen) { quiet++; continue; }
                quiet = 0;
                var option = dialogue.Q<Button>(className: "dialogue__option");
                var next = dialogue.Q<Button>("dialogue-continue");
                if (option != null) Submit(option);
                else if (next != null && Shown(next)) Submit(next);
            }
            Assert.IsFalse(CameraRig.SceneOpen, "the month's scenes are over");
            for (float t = 0f; t < 5f && !Shown(boot.Ui.Consent.Root); t += Time.unscaledDeltaTime) yield return null;
            var deny = boot.Ui.Root.Q<Button>("consent-deny");
            if (deny != null && Shown(deny)) Submit(deny);
            for (int i = 0; i < 5; i++) yield return null;
        }

        /// <summary>Owner, 2026-11-19: "a building on the map can't be tapped to go in" — the whole game, a tap on the bodega.</summary>
        [UnityTest]
        public IEnumerator A_tap_on_the_bodegas_building_walks_the_hero_in()
        {
            yield return BootPlaying(13);
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            var bodega = boot.PlaceBuildings.First(p => p.Value == "bodega").Key;
            var wall = bodega.GetComponentInChildren<Collider>();
            Assert.IsNotNull(wall, "the bodega can be hit");
            var rig = Object.FindFirstObjectByType<CameraRig>();
            rig.Frame(wall.bounds.center, 46f);
            for (int i = 0; i < 3; i++) yield return null;
            var at = rig.GetComponent<Camera>().WorldToScreenPoint(wall.bounds.center);
            Assert.AreEqual("bodega", boot.TapCity(new Vector2(at.x, at.y)), "the tap lands on the bodega");
            yield return null;

            var enter = boot.Ui.Root.Q<Button>("radial-enter");
            Assert.IsNotNull(enter, "its menu offers to walk in");
            Submit(enter);
            InteriorStage stage = null;
            for (float t = 0f; t < 15f && (stage == null || !stage.Showing); t += Time.unscaledDeltaTime)
            {
                stage = Object.FindFirstObjectByType<InteriorStage>();
                yield return null;
            }
            Assert.IsTrue(stage != null && stage.Showing, "the bodega's room is up");
            Assert.AreEqual("npc_tony_bulldog", stage.Host?.name, "with Tony in it");
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
