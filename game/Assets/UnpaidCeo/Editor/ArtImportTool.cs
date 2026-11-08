using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// Builds game prefabs from generated GLBs (Art/Imported/&lt;Kind&gt;/*.glb), validates them against the art budget,
    /// renders a preview with the house shader and writes a markdown report. Used for the pilot and every batch.
    /// </summary>
    public static class ArtImportTool
    {
        public const string ImportedRoot = "Assets/UnpaidCeo/Art/Imported";

        [MenuItem("Unpaid CEO/Art/Import all GLBs")]
        public static void ImportAll()
        {
            AssetDatabase.Refresh();
            var report = new StringBuilder("# Art import report\n\n");
            foreach (var (folder, kind) in new[] { ("Buildings", AssetKind.Building), ("Props", AssetKind.Prop), ("Characters", AssetKind.Character) })
                report.Append(ImportFolder($"{ImportedRoot}/{folder}", kind, null));
            File.WriteAllText("../tools/art/import-report.md", report.ToString());
        }

        /// <summary>Batchmode entry for the pilot: Art/Imported/Pilot as buildings, previews and report in tools/art/pilot.</summary>
        public static void PilotReport()
        {
            AssetDatabase.Refresh();
            var md = "# Pilot: bodega walk-up\n\n" + ImportFolder($"{ImportedRoot}/Pilot", AssetKind.Building, "../tools/art/pilot");
            File.WriteAllText("../tools/art/pilot/report.md", md);
            Debug.Log("[art] " + md);
        }

        public static string ImportFolder(string folder, AssetKind kind, string previewDir)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return "";
            var sb = new StringBuilder($"## {Path.GetFileName(folder)} ({kind})\n\n| asset | file | LOD0 tris | LOD levels | materials | textures | budget |\n|---|---|---|---|---|---|---|\n");
            foreach (var guid in AssetDatabase.FindAssets("", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".glb")) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                long bytes = new FileInfo(path).Length;
                string prefabPath;
                try { prefabPath = GlbPrefabBuilder.BuildPrefab(path, kind); }
                catch (System.Exception e) { sb.Append($"| {name} | {bytes / 1024} KB | — | — | — | — | import failed: {e.Message} |\n"); continue; }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var meshes = prefab.GetComponentsInChildren<MeshFilter>(true).Select(m => m.sharedMesh).Where(m => m != null)
                    .Concat(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => s.sharedMesh).Where(m => m != null)).ToList();
                int tris = meshes.Sum(AssetBudgetValidator.TrianglesAtLod0);
                int lods = meshes.Count == 0 ? 0 : meshes.Min(m => m.lodCount);
                var mats = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToList();
                var textures = mats.Select(m => m.GetTexture("_BaseMap")).Where(t => t != null).Distinct().Select(t => $"{t.width}×{t.height}");
                var errors = AssetBudgetValidator.Check(prefab, kind);
                sb.Append($"| {name} | {bytes / 1024} KB | {tris} | {lods} | {mats.Count} | {string.Join(", ", textures)} | {(errors.Count == 0 ? "OK" : string.Join("; ", errors))} |\n");
                if (previewDir != null) RenderPreview(prefab, $"{previewDir}/{name}.png");
            }
            return sb.Append('\n').ToString();
        }

        /// <summary>Renders the prefab in day light from the game's isometric angle into a PNG.</summary>
        public static void RenderPreview(GameObject prefab, string pngPath, int size = 768)
        {
            var created = new List<Object>();
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                created.Add(go);
                var bounds = new Bounds(go.transform.position, Vector3.zero);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);

                var sun = new GameObject("preview-sun").AddComponent<Light>();
                created.Add(sun.gameObject);
                sun.type = LightType.Directional;
                sun.intensity = 1.3f;
                sun.color = new Color(1f, 0.95f, 0.86f);
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(48, -35, 0);

                var cam = new GameObject("preview-camera").AddComponent<Camera>();
                created.Add(cam.gameObject);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.86f, 0.88f, 0.9f);
                cam.fieldOfView = 30;
                float radius = bounds.extents.magnitude;
                cam.transform.position = bounds.center + Quaternion.Euler(30, 45, 0) * Vector3.back * (radius / Mathf.Sin(15 * Mathf.Deg2Rad));
                cam.transform.LookAt(bounds.center);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = radius * 10;

                var rt = new RenderTexture(size, size, 24) { antiAliasing = 4 };
                created.Add(rt);
                cam.targetTexture = rt;
                var request = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                else cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                created.Add(tex);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
                File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            }
            finally
            {
                foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            }
        }
    }
}
