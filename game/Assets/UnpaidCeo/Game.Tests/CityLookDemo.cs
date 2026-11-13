using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnpaidCeo.Core.Platform;
using UnpaidCeo.Game.City;
using UnpaidCeo.Game.Graphics;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>
    /// Not a check: renders the Bronx block through its own camera at 1080×1920 by time of day and graphics mode to
    /// TestResults/demo/city_*.png, for the stage 5B look review (the 6-axis board). Run by name:
    /// tools/unity.sh test-playmode CityLookDemo
    /// </summary>
    [Explicit, Category("Demo")]
    public class CityLookDemo
    {
        AsyncOperationHandle<SceneInstance> _scene;
        static string Dir => Path.Combine(Application.dataPath, "../TestResults/demo");

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (_scene.IsValid()) yield return Addressables.UnloadSceneAsync(_scene);
            Shader.SetGlobalFloat(DayNeonController.NightProperty, 0f);
            GraphicsModeService.Apply(GraphicsMode.Optimal);
        }

        static void Shot(Camera cam, string file)
        {
            var rt = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = prev;
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(Path.Combine(Dir, file + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            Object.Destroy(rt);
        }

        [UnityTest]
        public IEnumerator The_block_by_time_and_mode([Values("before", "after")] string tag)
        {
            _scene = Addressables.LoadSceneAsync(CityView.BronxAddress, LoadSceneMode.Additive);
            yield return _scene;
            var rig = Object.FindFirstObjectByType<CameraRig>();
            var cam = rig.GetComponent<Camera>();
            var light = Object.FindFirstObjectByType<DayNeonController>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            rig.Frame(new Vector3(8f, 0f, 4f), 52f);
            rig.Snap();
            foreach (var mode in new[] { GraphicsMode.Eco, GraphicsMode.Optimal, GraphicsMode.Max })
            {
                GraphicsModeService.Apply(mode);
                foreach (var (name, blend) in new[] { ("day", 0f), ("evening", 0.5f), ("night", 1f) })
                {
                    light.SetBlend(blend);
                    for (int i = 0; i < 3; i++) yield return null;
                    Shot(cam, $"city_{tag}_{mode.ToString().ToLowerInvariant()}_{name}");
                }
            }
        }
    
        [UnityTest]
        public IEnumerator A_resident_close_up()
        {
            _scene = Addressables.LoadSceneAsync(CityView.BronxAddress, LoadSceneMode.Additive);
            yield return _scene;
            var rig = Object.FindFirstObjectByType<CameraRig>();
            var cam = rig.GetComponent<Camera>();
            var light = Object.FindFirstObjectByType<DayNeonController>();
            light.SetBlend(0.15f);
            var spot = new Vector3(21.5f, 0.2f, 7.4f);
            var tony = Addressables.InstantiateAsync("npc_tony_bulldog", spot, Quaternion.Euler(0f, 180f, 0f));
            yield return tony;
            var mentor = Addressables.InstantiateAsync("npc_mentor_raccoon", spot + new Vector3(2.2f, 0f, 0.4f), Quaternion.Euler(0f, 200f, 0f));
            yield return mentor;
            foreach (var go in new[] { tony.Result, mentor.Result }) go.transform.localScale *= 1.6f / Mathf.Max(0.01f, go.GetComponentInChildren<Renderer>().bounds.size.y);
            rig.Frame(spot + new Vector3(1f, 0f, 0f), 24f, raise: 1f);
            rig.Snap();
            GraphicsModeService.Apply(GraphicsMode.Optimal);
            for (int i = 0; i < 4; i++) yield return null;
            Shot(cam, "city_closeup_residents");
            Addressables.ReleaseInstance(tony.Result);
            Addressables.ReleaseInstance(mentor.Result);
        }
    }
}
