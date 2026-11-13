using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnpaidCeo.Core.Platform;
using UnpaidCeo.Game.Graphics;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Draw-call budgets (spec §5.5): ≤ 100 Eco, ≤ 250 Optimal, ≤ 600 Max, on a 12-building block.</summary>
    public class GraphicsBudgetTests
    {
        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var o in _created) if (o != null) Object.Destroy(o);
            _created.Clear();
            GraphicsModeService.Apply(GraphicsMode.Optimal);
        }

        /// <summary>Stand-in for the Bronx block until Task 14 replaces it with the real scene: 12 buildings of 3 parts plus 30 props.</summary>
        Camera _camera;

        void BuildBlock()
        {
            var shader = Shader.Find("UnpaidCeo/Stylized");
            var brick = new Material(shader) { color = new Color(0.71f, 0.34f, 0.24f) };
            var trim = new Material(shader) { color = new Color(0.2f, 0.55f, 0.34f) };
            _created.Add(brick);
            _created.Add(trim);
            for (int i = 0; i < 12; i++)
            {
                var root = new GameObject("building_" + i);
                _created.Add(root);
                root.transform.position = new Vector3((i % 4) * 12f, 0, (i / 4) * 12f);
                Part(root, PrimitiveType.Cube, new Vector3(0, 6, 0), new Vector3(8, 12, 8), brick);
                Part(root, PrimitiveType.Cube, new Vector3(0, 12.5f, 0), new Vector3(8.5f, 1, 8.5f), trim);
                Part(root, PrimitiveType.Cube, new Vector3(0, 2, 4.4f), new Vector3(6, 0.4f, 1.5f), trim);
            }
            for (int i = 0; i < 30; i++)
            {
                var prop = Part(null, PrimitiveType.Cylinder, new Vector3((i % 10) * 4.5f - 2, 0.5f, (i / 10) * 12f + 6), new Vector3(0.6f, 0.5f, 0.6f), trim);
                _created.Add(prop);
            }
            var sun = new GameObject("sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            _created.Add(sun.gameObject);

            var cam = new GameObject("camera").AddComponent<Camera>();
            cam.transform.position = new Vector3(18, 45, -30);
            cam.transform.LookAt(new Vector3(18, 0, 12));
            cam.farClipPlane = 300;
            _created.Add(cam.gameObject);
            _camera = cam;
        }

        static GameObject Part(GameObject parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            if (parent != null) go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        [UnityTest]
        public IEnumerator DrawCalls_within_budget_per_mode()
        {
            BuildBlock();
            var budgets = new Dictionary<GraphicsMode, int> { [GraphicsMode.Eco] = 100, [GraphicsMode.Optimal] = 250, [GraphicsMode.Max] = 600 };
            int previous = 0;
            foreach (var pair in budgets)
            {
                GraphicsModeService.Apply(pair.Key);
                yield return null;
                var estimate = DrawCallEstimator.Estimate(_camera);
                Debug.Log($"[graphics] {pair.Key}: ~{estimate.Total} draw calls (forward {estimate.Forward}, depth {estimate.Depth}, shadows {estimate.Shadows}); budget {pair.Value}");
                Assert.Greater(estimate.Forward, 40, "the block should be visible to the camera");
                Assert.LessOrEqual(estimate.Total, pair.Value, $"{pair.Key} exceeds its draw-call budget");
                Assert.GreaterOrEqual(estimate.Total, previous, "higher modes never draw less");
                previous = estimate.Total;
            }
        }

        [UnityTest]
        public IEnumerator Eco_casts_no_realtime_shadows()
        {
            BuildBlock();
            GraphicsModeService.Apply(GraphicsMode.Eco);
            yield return null;
            Assert.AreEqual(0, DrawCallEstimator.Estimate(_camera).Shadows);
        }
    }
}
