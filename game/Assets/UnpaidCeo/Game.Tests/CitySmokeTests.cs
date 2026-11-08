using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Platform;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Game.City;
using UnpaidCeo.Game.Graphics;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>The Bronx block: Addressables loading, buildings on lots, the month timelapse, day/neon and draw-call budgets.</summary>
    public class CitySmokeTests
    {
        AsyncOperationHandle<SceneInstance> _scene;
        CityView _city;

        IEnumerator LoadBronx()
        {
            _scene = Addressables.LoadSceneAsync(CityView.BronxAddress, LoadSceneMode.Additive);
            yield return _scene;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, _scene.Status, "district_bronx failed to load");
            _city = Object.FindFirstObjectByType<CityView>();
            Assert.IsNotNull(_city, "Bronx scene has no CityView");
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (_scene.IsValid()) yield return Addressables.UnloadSceneAsync(_scene);
            Shader.SetGlobalFloat(DayNeonController.NightProperty, 0f);
        }

        [UnityTest]
        public IEnumerator Bronx_scene_loads_via_addressables()
        {
            yield return LoadBronx();
            Assert.GreaterOrEqual(_city.Lots.Count, 4, "the block needs lots for the player's businesses");
            Assert.GreaterOrEqual(Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r => r.name.StartsWith("building_")), 10,
                "the block should show the generated Bronx buildings");
            Assert.IsNotNull(Object.FindFirstObjectByType<DayNeonController>());
            Assert.IsNotNull(Object.FindFirstObjectByType<CameraRig>());
            Assert.IsNotNull(Object.FindFirstObjectByType<TimelapseDirector>());
        }

        static GameState StateWith(params (string id, string industry, string lot)[] businesses)
        {
            var s = new GameState { Month = 3 };
            foreach (var b in businesses) s.Businesses.Add(new BusinessState { Id = b.id, IndustryId = b.industry, LotId = b.lot });
            return s;
        }

        [UnityTest]
        public IEnumerator Opening_business_spawns_building_on_lot()
        {
            yield return LoadBronx();
            var lot = _city.Lots[0];
            _city.Sync(StateWith(("gig", "gig_delivery", null)));
            Assert.IsNull(lot.Building, "a gig has no building");

            _city.Sync(StateWith(("gig", "gig_delivery", null), ("shop", "resale_shop", lot.LotId)));
            yield return null;
            Assert.IsNotNull(lot.Building, "opening a business on a lot must spawn its building");
            Assert.IsTrue(lot.Building.transform.IsChildOf(lot.transform));
            Assert.AreEqual("shop", lot.BusinessId);

            _city.Sync(StateWith(("gig", "gig_delivery", null)));
            yield return null;
            Assert.IsNull(lot.Building, "closing the business clears the lot");
        }

        [UnityTest]
        public IEnumerator Timelapse_finishes_within_8_seconds()
        {
            yield return LoadBronx();
            var director = Object.FindFirstObjectByType<TimelapseDirector>();
            var report = new MonthReport { Month = 1, Revenue = Money.FromDollars(2470), NetProfit = Money.FromDollars(1745) };
            float start = Time.realtimeSinceStartup;
            director.Play(report);
            Assert.IsTrue(director.IsPlaying);
            while (director.IsPlaying && Time.realtimeSinceStartup - start < 10f) yield return null;
            float elapsed = Time.realtimeSinceStartup - start;
            Assert.IsFalse(director.IsPlaying, "timelapse never finished");
            Assert.That(elapsed, Is.InRange(5f, 8f), $"timelapse took {elapsed:0.00} s");
            Assert.Greater(director.FloatingTextsShown, 0, "revenue should float above the block");
        }

        [UnityTest]
        public IEnumerator Night_turns_on_the_neon()
        {
            yield return LoadBronx();
            var dn = Object.FindFirstObjectByType<DayNeonController>();
            dn.Set(TimeOfDay.Night);
            yield return null;
            Assert.AreEqual(1f, Shader.GetGlobalFloat(DayNeonController.NightProperty), 1e-3f);
            dn.Set(TimeOfDay.Day);
            yield return null;
            Assert.AreEqual(0f, Shader.GetGlobalFloat(DayNeonController.NightProperty), 1e-3f);
        }

        [UnityTest]
        public IEnumerator Bronx_draw_calls_within_budget_per_mode()
        {
            yield return LoadBronx();
            var cam = Object.FindFirstObjectByType<CameraRig>().GetComponent<Camera>();
            foreach (var (mode, budget) in new[] { (GraphicsMode.Eco, 100), (GraphicsMode.Optimal, 250), (GraphicsMode.Max, 600) })
            {
                GraphicsModeService.Apply(mode);
                yield return null;
                var e = DrawCallEstimator.Estimate(cam);
                Debug.Log($"[graphics] Bronx {mode}: ~{e.Total} draw calls (forward {e.Forward}, depth {e.Depth}, shadows {e.Shadows}); budget {budget}");
                Assert.LessOrEqual(e.Total, budget, $"{mode} exceeds its draw-call budget on the Bronx block");
            }
            GraphicsModeService.Apply(GraphicsMode.Optimal);
        }
    }
}
