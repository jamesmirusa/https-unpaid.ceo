using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnpaidCeo.Core.Platform;
using UnpaidCeo.Game.City;
using UnpaidCeo.Game.Graphics;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Stage 5B step 5: a warm evening between day and neon, windows that light up, a look by quality level.</summary>
    public class CityLookTests
    {
        AsyncOperationHandle<SceneInstance> _scene;

        IEnumerator LoadBronx()
        {
            _scene = Addressables.LoadSceneAsync(CityView.BronxAddress, LoadSceneMode.Additive);
            yield return _scene;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, _scene.Status);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (_scene.IsValid()) yield return Addressables.UnloadSceneAsync(_scene);
            Shader.SetGlobalFloat(DayNeonController.NightProperty, 0f);
            GraphicsModeService.Apply(GraphicsMode.Optimal);
        }

        [UnityTest]
        public IEnumerator The_evening_sun_is_warm_and_the_night_cool()
        {
            yield return LoadBronx();
            var dn = Object.FindFirstObjectByType<DayNeonController>();
            dn.Set(TimeOfDay.Evening);
            Assert.AreEqual(0.5f, dn.NightAmount, 1e-3f);
            var sun = dn.Sun;
            Assert.Greater(sun.color.r, sun.color.b + 0.3f, "late sun on brick: warm");
            dn.Set(TimeOfDay.Night);
            Assert.Greater(sun.color.b, sun.color.r, "night: cool");
        }

        [UnityTest]
        public IEnumerator Strong_devices_get_the_look_weak_ones_do_not()
        {
            yield return LoadBronx();
            var dn = Object.FindFirstObjectByType<DayNeonController>();
            GraphicsModeService.Apply(GraphicsMode.Optimal);
            yield return null;
            var look = dn.LookVolume;
            Assert.IsNotNull(look);
            Assert.IsTrue(look.sharedProfile.TryGet<Vignette>(out var v) && v.intensity.value > 0.1f, "a light vignette");
            Assert.IsTrue(look.sharedProfile.TryGet<SplitToning>(out _), "warm light, cool shade");
            Assert.AreEqual(1f, look.weight, 1e-3f);
            GraphicsModeService.Apply(GraphicsMode.Eco);
            yield return null;
            Assert.AreEqual(0f, look.weight, 1e-3f, "Eco keeps it plain");
        }

        [UnityTest]
        public IEnumerator Windows_glow_on_buildings_only()
        {
            yield return LoadBronx();
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            var building = renderers.First(r => r.name.StartsWith("building_") && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_WindowGlow"));
            Assert.AreEqual(1f, building.sharedMaterial.GetFloat("_WindowGlow"), 1e-3f);
            var prop = renderers.FirstOrDefault(r => r.name.StartsWith("prop_") && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_WindowGlow"));
            if (prop != null) Assert.AreEqual(0f, prop.sharedMaterial.GetFloat("_WindowGlow"), 1e-3f);
        }

        [Test]
        public void A_standing_resident_looks_around()
        {
            float a = StreetLife.LookYaw(0f, 0), b = StreetLife.LookYaw(2.5f, 0), c = StreetLife.LookYaw(2.5f, 1);
            Assert.Greater(Mathf.Abs(a - b), 1e-3f, "they turn over time");
            Assert.Greater(Mathf.Abs(b - c), 1e-3f, "each resident in their own rhythm");
            Assert.LessOrEqual(Mathf.Abs(b), StreetLife.LookAround + 1e-3f);
        }
    }
}
