using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnpaidCeo.Game.Hero;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// Measures each character's idle pose (soles and head top, from the posed skinned mesh) into its
    /// <see cref="StandingPose"/>, so the game stands people on the floor instead of by their oversized bounding box.
    ///   tools/unity-run.sh UnpaidCeo.Editor.CharacterPoseBaker.BakeAll
    /// </summary>
    public static class CharacterPoseBaker
    {
        const string Prefabs = "Assets/UnpaidCeo/Art/Prefabs";

        [MenuItem("Unpaid CEO/Art/Bake character standing poses")]
        public static void BakeAll()
        {
            var report = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith("hero_") && !name.StartsWith("npc_")) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var (sole, top) = Measure(prefab);
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var pose = contents.GetComponent<StandingPose>() ?? contents.AddComponent<StandingPose>();
                    pose.Set(sole, top);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                report.Add($"{name}: sole {sole:0.000} top {top:0.000}");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[pose] " + string.Join("; ", report));
        }

        /// <summary>Soles and head top of the model in its idle pose at time 0, in the root's own (unscaled) units.</summary>
        public static (float sole, float top) Measure(GameObject source)
        {
            var go = Object.Instantiate(source);
            try
            {
                var t = go.transform;
                t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var idle = go.GetComponent<CharacterAnimator>()?.Clip(CharacterAnimator.State.Idle);
                if (idle != null) idle.SampleAnimation(go, 0f);
                var (min, max) = StandingPose.PosedHeightRange(go);
                float s = t.lossyScale.y;
                return min > max ? (0f, 0f) : (min / s, max / s);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
