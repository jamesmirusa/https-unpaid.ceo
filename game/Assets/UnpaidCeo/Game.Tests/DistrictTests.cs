using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Platform;
using UnpaidCeo.Game.Audio;
using UnpaidCeo.Game.Bootstrap;
using UnpaidCeo.Game.City;
using UnpaidCeo.Game.Graphics;
using UnpaidCeo.Game.Services;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Brooklyn (chapter 2) and Queens (chapter 3): loading, budgets, buildings that grow with units, and the boot/advance swap.</summary>
    public class DistrictTests
    {
        static readonly string[] Districts = { "district_brooklyn", "district_queens" };
        static readonly string[] DistrictScenes = { "Boot", "Bronx", "Brooklyn", "Queens", "Room" };

        AsyncOperationHandle<SceneInstance> _scene;
        CityView _city;
        string _savedSave, _savedMeta;

        [SetUp]
        public void KeepDeveloperSave()
        {
            _savedSave = PlayerPrefs.GetString(SaveService.Key, null);
            _savedMeta = PlayerPrefs.GetString(SaveService.MetaKey, null);
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (_scene.IsValid()) yield return Addressables.UnloadSceneAsync(_scene);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (DistrictScenes.Contains(scene.name) && SceneManager.sceneCount > 1) yield return SceneManager.UnloadSceneAsync(scene);
            }
            Shader.SetGlobalFloat(DayNeonController.NightProperty, 0f);
            GraphicsModeService.Apply(GraphicsMode.Optimal);
            Restore(SaveService.Key, _savedSave);
            Restore(SaveService.MetaKey, _savedMeta);
        }

        static void Restore(string key, string value)
        {
            if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey(key); else PlayerPrefs.SetString(key, value);
        }

        IEnumerator Load(string address)
        {
            if (_scene.IsValid()) yield return Addressables.UnloadSceneAsync(_scene);
            _scene = Addressables.LoadSceneAsync(address, LoadSceneMode.Additive);
            yield return _scene;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, _scene.Status, address + " failed to load");
            _city = Object.FindFirstObjectByType<CityView>();
            Assert.IsNotNull(_city, address + " has no CityView");
        }

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

        [UnityTest]
        public IEnumerator Brooklyn_and_Queens_load()
        {
            foreach (var address in Districts)
            {
                yield return Load(address);
                Assert.AreEqual(address == "district_brooklyn" ? "Brooklyn" : "Queens", _scene.Result.Scene.name);
                Assert.GreaterOrEqual(_city.Lots.Count, 6, address + " needs lots for the player's units");
                Assert.IsNotNull(_city.HomeAnchor, address + " needs a home for the hero");
                Assert.GreaterOrEqual(Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r => r.name.StartsWith("building_")), 10,
                    address + " should show its buildings");
                Assert.IsNotNull(Object.FindFirstObjectByType<DayNeonController>());
                Assert.IsNotNull(Object.FindFirstObjectByType<CameraRig>());
                Assert.IsNotNull(Object.FindFirstObjectByType<TimelapseDirector>());
                var cars = Object.FindObjectsByType<StreetCar>(FindObjectsSortMode.None);
                Assert.GreaterOrEqual(cars.Length, 2, address + " needs cars on the avenue");
                Assert.IsTrue(cars.All(c => !c.gameObject.isStatic), address + ": a static car is batched into the scene and cannot move");
            }
        }

        [UnityTest]
        public IEnumerator Districts_stay_within_draw_call_budgets_per_mode()
        {
            foreach (var address in Districts)
            {
                yield return Load(address);
                var cam = Object.FindFirstObjectByType<CameraRig>().GetComponent<Camera>();
                foreach (var (mode, budget) in new[] { (GraphicsMode.Eco, 100), (GraphicsMode.Optimal, 250), (GraphicsMode.Max, 600) })
                {
                    GraphicsModeService.Apply(mode);
                    yield return null;
                    var e = DrawCallEstimator.Estimate(cam);
                    Debug.Log($"[graphics] {address} {mode}: ~{e.Total} draw calls (forward {e.Forward}, depth {e.Depth}, shadows {e.Shadows}); budget {budget}");
                    Assert.Greater(e.Forward, 30, address + " should be visible to its camera");
                    Assert.LessOrEqual(e.Total, budget, $"{mode} exceeds its draw-call budget in {address}");
                }
            }
        }

        [UnityTest]
        public IEnumerator Every_chapter_2_industry_has_its_building()
        {
            yield return Load("district_brooklyn");
            var c = _city.Catalog;
            Assert.IsNotNull(c);
            void Expect(string industry, int units, string prefab) => Assert.AreEqual(prefab, c.BuildingFor(industry, units)?.name, $"{industry} × {units}");
            Expect("coffee_cart", 1, "prop_coffee_cart");
            Expect("coffee_cart", 2, "building_coffee_shop");
            Expect("resale_store", 1, "building_pawn_shop");
            Expect("food_truck", 2, "prop_food_truck");
            Expect("food_truck", 3, "building_pizza");
            Expect("ai_agency", 1, "building_office");
            Expect("cleaning", 1, "building_cleaning_co");
            Expect("logistics_box_truck", 1, "prop_box_truck");
            Expect("logistics_box_truck", 2, "building_warehouse");
        }

        static GameState StateWith(params (string id, string industry, int units)[] businesses)
        {
            var s = new GameState { Month = 20, Chapter = 2 };
            foreach (var b in businesses) s.Businesses.Add(new BusinessState { Id = b.id, IndustryId = b.industry, Units = b.units });
            return s;
        }

        [UnityTest]
        public IEnumerator Opening_units_upgrades_the_building_on_the_lot()
        {
            yield return Load("district_brooklyn");
            var lot = _city.Lots[0];

            _city.Sync(StateWith(("gig", "gig_delivery", 0), ("cafe", "coffee_cart", 0)));
            Assert.IsTrue(_city.Lots.All(l => l.Building == null), "a chosen industry with no unit open has nothing on a lot yet");

            _city.Sync(StateWith(("gig", "gig_delivery", 0), ("cafe", "coffee_cart", 1)));
            yield return null;
            Assert.IsNotNull(lot.Building, "the first unit takes the first lot");
            Assert.AreEqual("prop_coffee_cart", lot.Building.name);
            Assert.AreEqual("cafe", lot.BusinessId);
            float cart = ModelFit.RendererBounds(lot.Building).size.magnitude;

            _city.Sync(StateWith(("gig", "gig_delivery", 0), ("cafe", "coffee_cart", 2)));
            yield return null;
            Assert.AreEqual("building_coffee_shop", lot.Building.name, "the second unit upgrades the cart to a shop");
            Assert.Less(cart * 1.5f, ModelFit.RendererBounds(lot.Building).size.magnitude, "a cart stands at cart size, not at building size");
            Assert.AreEqual(1, _city.Lots.Count(l => l.Building != null));
        }

        IEnumerator BootWithSave(GameState s)
        {
            SaveService.Save(s);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/UnpaidCeo/Scenes/Boot.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 30f)
            {
                var boot = Object.FindFirstObjectByType<GameBootstrap>();
                if (boot != null && boot.Current != GameBootstrap.Phase.Loading) break;
                yield return null;
            }
            // A chapter-2 save opens a voiced scene. Yarn Spinner runs a line whose clip finishes loading after
            // the runner is gone and throws, so let the first line reach the screen before the test moves on.
            var audio = Object.FindFirstObjectByType<AudioDirector>();
            for (float t = 0f; audio != null && !audio.VoicePlaying && t < 5f; t += Time.unscaledDeltaTime) yield return null;
        }

        static bool Loaded(string scene) => SceneManager.GetSceneByName(scene).isLoaded;

        [UnityTest]
        public IEnumerator Bootstrap_loads_the_district_for_the_saved_chapter()
        {
            Assert.AreEqual("district_bronx", GameBootstrap.DistrictAddress(1));
            Assert.AreEqual("district_brooklyn", GameBootstrap.DistrictAddress(2));
            Assert.AreEqual("district_queens", GameBootstrap.DistrictAddress(3));

            var state = new GameEngine(Content()).NewGame(7, new HeroAppearance());
            state.Chapter = 2;
            yield return BootWithSave(state);
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            Assert.AreEqual(GameBootstrap.Phase.Playing, boot.Current, "a saved player resumes");
            Assert.IsTrue(Loaded("Brooklyn"), "a chapter-2 save opens Brooklyn");
            Assert.IsFalse(Loaded("Bronx"), "the Bronx is not downloaded for a chapter-2 save");
        }

        [UnityTest]
        public IEnumerator Advancing_swaps_to_the_next_district()
        {
            var state = new GameEngine(Content()).NewGame(8, new HeroAppearance());
            state.Chapter = 2;
            yield return BootWithSave(state);
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            var brooklyn = Object.FindFirstObjectByType<CityView>();
            yield return boot.SwapDistrict(3);
            Assert.IsTrue(Loaded("Queens"));
            Assert.IsFalse(Loaded("Brooklyn"), "the old district is unloaded");
            var city = Object.FindFirstObjectByType<CityView>();
            Assert.IsNotNull(city);
            Assert.AreNotSame(brooklyn, city);
        }

        [UnityTest]
        public IEnumerator District_music_follows_the_chapter()
        {
            var state = new GameEngine(Content()).NewGame(9, new HeroAppearance());
            state.Chapter = 2;
            yield return BootWithSave(state);
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            var audio = Object.FindFirstObjectByType<AudioDirector>();
            Assert.IsNotNull(audio);
            yield return WaitForTheme(audio, "music_brooklyn");
            Assert.AreEqual("music_brooklyn", audio.DayTheme, "a chapter-2 save plays the Brooklyn theme");
            Assert.AreEqual(AudioAddresses.MusicNeon, audio.NightTheme);

            yield return boot.SwapDistrict(3);
            yield return WaitForTheme(audio, "music_queens");
            Assert.AreEqual("music_queens", audio.DayTheme, "moving to Queens switches the theme");
            Assert.IsTrue(audio.MusicPlaying);
        }

        [UnityTest]
        public IEnumerator Destroying_the_game_mid_scene_ends_the_scene_loop()
        {
            var state = new GameEngine(Content()).NewGame(10, new HeroAppearance());
            state.Chapter = 2;
            yield return BootWithSave(state);
            Assert.IsTrue(Object.FindFirstObjectByType<AudioDirector>().VoicePlaying, "a chapter-2 save opens a voiced scene");

            // Reset (and closing the game) destroys the bootstrap while a scene is on screen: the month's other
            // pending scenes must not start on the destroyed dialogue runner.
            Object.Destroy(Object.FindFirstObjectByType<GameBootstrap>().gameObject);
            for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime) yield return null; // an exception here fails the test
        }

        static IEnumerator WaitForTheme(AudioDirector audio, string theme)
        {
            for (float t = 0f; t < 10f && audio.DayTheme != theme; t += Time.unscaledDeltaTime) yield return null;
        }
    }
}
