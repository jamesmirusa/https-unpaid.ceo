using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnpaidCeo.Game.City;
using UnpaidCeo.Game.Hero;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>
    /// People stand with their soles on the floor while they play their idle clip: not hovering over it (which reads as
    /// floating in the close-up of a room) and not sunk into it.
    /// </summary>
    public class CharacterGroundingTests
    {
        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        static readonly string[] People =
        {
            "hero_raccoon_hoodie", "hero_fox_blazer", "hero_cat_hoodie", "npc_tony_bulldog", "npc_mr_fins_shark",
            "npc_ms_lee_owl", "npc_sam_beaver", "npc_mentor_raccoon", "npc_jay_otter",
        };

        /// <summary>The lowest point of what is drawn this frame (skinned meshes posed by their clip), in world space.</summary>
        static float SoleHeight(GameObject model) => StandingPose.PosedHeightRange(model).min;

        [UnityTest]
        public IEnumerator Each_person_stands_on_the_floor_in_their_idle_pose()
        {
#if UNITY_EDITOR
            var report = new List<string>();
            var failures = new List<string>();
            foreach (var name in People)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/UnpaidCeo/Art/Prefabs/{name}.prefab");
                Assert.IsNotNull(prefab, name);
                var spot = new GameObject("spot").transform;
                spot.position = new Vector3(0f, -400f, 0f);
                _made.Add(spot.gameObject);
                var model = Object.Instantiate(prefab, spot);
                model.transform.localPosition = Vector3.zero;
                ModelFit.Stand(model, HeroView.Height);
                for (int i = 0; i < 4; i++) yield return null;   // the idle clip has posed the skeleton
                ModelFit.Stand(model, HeroView.Height);
                yield return null;
                float gap = SoleHeight(model) - spot.position.y;
                report.Add($"{name} {gap:+0.000;-0.000} (bounds {ModelFit.RendererBounds(model).min.y - spot.position.y:+0.000;-0.000})");
                if (Mathf.Abs(gap) >= 0.03f) failures.Add(name);
            }
            Assert.IsEmpty(failures, "hover or sink: " + string.Join(", ", report));
#else
            Assert.Ignore("needs the editor's asset database");
            yield break;
#endif
        }
    }
}
