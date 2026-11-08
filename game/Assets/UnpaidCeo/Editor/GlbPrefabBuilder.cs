using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// Turns a generated model (a glTFast-imported GLB) into a game prefab: every material becomes the house
    /// shader with the original base-color texture, static meshes get Mesh LOD levels, buildings and props get a collider.
    /// </summary>
    public static class GlbPrefabBuilder
    {
        static readonly string[] BaseTextureProperties = { "_BaseMap", "baseColorTexture", "_MainTex" };
        static readonly string[] EmissionTextureProperties = { "_EmissionMap", "emissiveTexture" };

        /// <summary>Largest texture shipped per kind: the web build has a 20 MB budget (spec §6), quality levels go lower still.</summary>
        public static int MaxTextureSize(AssetKind kind) => kind == AssetKind.Prop ? 512 : 1024;

        /// <summary>Returns a prepared copy of <paramref name="source"/>; the source is left untouched.</summary>
        public static GameObject Prepare(GameObject source, AssetKind kind)
        {
            var copy = Object.Instantiate(source);
            copy.name = source.name;
            var shader = Shader.Find(AssetBudgetValidator.StylizedShaderName);

            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Stylize(mats[i], shader, MaxTextureSize(kind));
                r.sharedMaterials = mats;
            }

            if (kind != AssetKind.Character)
            {
                foreach (var mf in copy.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    var mesh = Object.Instantiate(mf.sharedMesh);
                    mesh.name = mf.sharedMesh.name;
                    MeshLodUtility.GenerateMeshLods(mesh, AssetBudgetValidator.RequiredLodLevels - 1);
                    mf.sharedMesh = mesh;
                }
                if (copy.GetComponentInChildren<Collider>(true) == null) copy.AddComponent<BoxCollider>().center = Bounds(copy).center - copy.transform.position;
                var box = copy.GetComponent<BoxCollider>();
                if (box != null) box.size = Bounds(copy).size;
            }
            return copy;
        }

        /// <summary>Prepares a GLB asset and saves it as a prefab; generated meshes and materials become their own assets first.</summary>
        public static string BuildPrefab(string glbPath, AssetKind kind, string root = "Assets/UnpaidCeo/Art")
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (source == null) throw new FileNotFoundException("GLB not imported as a GameObject", glbPath);
            var name = Path.GetFileNameWithoutExtension(glbPath);
            var prepared = Prepare(source, kind);
            try
            {
                foreach (var folder in new[] { "Prefabs", "Meshes", "Materials", "Textures" }) Directory.CreateDirectory($"{root}/{folder}");
                SaveTextures(prepared, $"{root}/Textures/{name}", MaxTextureSize(kind));
                int n = 0;
                foreach (var mf in prepared.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && !AssetDatabase.Contains(mf.sharedMesh))
                        AssetDatabase.CreateAsset(mf.sharedMesh, $"{root}/Meshes/{name}_{n++}.asset");
                n = 0;
                foreach (var r in prepared.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m != null && !AssetDatabase.Contains(m))
                            AssetDatabase.CreateAsset(m, $"{root}/Materials/{name}_{n++}.mat");
                var path = $"{root}/Prefabs/{name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(prepared, path);
                AssetDatabase.SaveAssets();
                return path;
            }
            finally
            {
                Object.DestroyImmediate(prepared);
            }
        }

        /// <summary>Writes generated (non-asset) textures as PNGs with a capped, compressed import, and points the materials at them.</summary>
        static void SaveTextures(GameObject prepared, string pathPrefix, int maxSize)
        {
            int n = 0;
            var saved = new System.Collections.Generic.Dictionary<Texture, Texture2D>();
            foreach (var r in prepared.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    foreach (var prop in new[] { "_BaseMap", "_EmissionMap" })
                    {
                        var tex = m != null ? m.GetTexture(prop) : null;
                        if (tex == null) continue;
                        if (!saved.TryGetValue(tex, out var asset))
                        {
                            var readable = tex as Texture2D;
                            if (readable == null || !readable.isReadable)
                                readable = Copy(tex, Mathf.Min(maxSize, Mathf.Max(tex.width, tex.height)), true);
                            var path = $"{pathPrefix}_{n++}.png";
                            File.WriteAllBytes(path, readable.EncodeToPNG());
                            AssetDatabase.ImportAsset(path);
                            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                            importer.maxTextureSize = maxSize;
                            importer.textureCompression = TextureImporterCompression.Compressed;
                            importer.mipmapEnabled = true;
                            importer.sRGBTexture = true;
                            importer.SaveAndReimport();
                            asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                            saved[tex] = asset;
                        }
                        m.SetTexture(prop, asset);
                    }
        }

        static Material Stylize(Material source, Shader shader, int maxSize)
        {
            var m = new Material(shader) { name = source != null ? source.name : "stylized" };
            if (source == null) return m;
            foreach (var p in BaseTextureProperties)
                if (source.HasProperty(p) && source.GetTexture(p) != null) { m.SetTexture("_BaseMap", Fit(source.GetTexture(p), maxSize, true)); break; }
            foreach (var p in EmissionTextureProperties)
                if (source.HasProperty(p) && source.GetTexture(p) != null) { m.SetTexture("_EmissionMap", Fit(source.GetTexture(p), maxSize, true)); break; }
            if (source.HasProperty("_BaseColor")) m.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            else if (source.HasProperty("baseColorFactor")) m.SetColor("_BaseColor", source.GetColor("baseColorFactor"));
            return m;
        }

        /// <summary>Returns the texture itself if it fits, otherwise a GPU-downscaled readable copy (works for non-readable imports).</summary>
        static Texture Fit(Texture source, int maxSize, bool srgb) =>
            source.width <= maxSize && source.height <= maxSize ? source : Copy(source, maxSize, srgb);

        /// <summary>Always returns a readable copy whose longest side is <paramref name="maxSize"/> (or smaller).</summary>
        static Texture2D Copy(Texture source, int maxSize, bool srgb)
        {
            float scale = (float)maxSize / Mathf.Max(source.width, source.height);
            int w = Mathf.Max(1, Mathf.RoundToInt(source.width * scale)), h = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, true, !srgb) { name = source.name };
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply(true);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
