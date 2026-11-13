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
        static readonly string[] NormalTextureProperties = { "_BumpMap", "normalTexture" };
        public static readonly string[] ClipNames = { "walk", "cheer", "think" };
        public const string ClipsFolder = "Clips";

        /// <summary>Largest texture in the web build per kind: the web build has a 20 MB budget (spec §6), quality levels go lower still.</summary>
        public static int MaxTextureSize(AssetKind kind) => kind == AssetKind.Prop ? 512 : kind == AssetKind.Interior ? 2048 : 1024;

        /// <summary>Largest texture in the store apps and desktop builds, where the download budget does not apply.</summary>
        public static int AppTextureSize(AssetKind kind) => kind == AssetKind.Prop ? 1024 : 2048;

        /// <summary>The house shader's most light bands: a room's pale surfaces shade smoothly instead of in steps.</summary>
        public const float InteriorBands = 6f;

        /// <summary>
        /// Normal maps in the web build: enough for surface detail at the isometric camera distance; a room fills a phone
        /// screen during a visit (downloaded only then), so its maps are larger.
        /// </summary>
        public static int WebNormalSize(AssetKind kind) => kind == AssetKind.Interior ? 1024 : WebNormalMapSize;

        /// <summary>Normal maps in the web build: enough for surface detail at the isometric camera distance.</summary>
        public const int WebNormalMapSize = 512;

        /// <summary>Full size by default, capped through a WebGL override; normal maps are linear and smaller on the web.</summary>
        public static void ConfigureTexture(TextureImporter importer, AssetKind kind, bool normalMap)
        {
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.maxTextureSize = AppTextureSize(kind);
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = true;
            var web = importer.GetPlatformTextureSettings("WebGL");
            web.overridden = true;
            web.maxTextureSize = normalMap ? WebNormalSize(kind) : MaxTextureSize(kind);
            importer.SetPlatformTextureSettings(web);
            importer.SaveAndReimport();
        }

        /// <summary>Returns a prepared copy of <paramref name="source"/>; the source is left untouched.</summary>
        public static GameObject Prepare(GameObject source, AssetKind kind)
        {
            var copy = Object.Instantiate(source);
            copy.name = source.name;
            var shader = Shader.Find(AssetBudgetValidator.StylizedShaderName);

            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = Stylize(mats[i], shader, AppTextureSize(kind));
                    if (kind == AssetKind.Interior) LightSoftly(mats[i]);
                }
                r.sharedMaterials = mats;
                EnsureTangents(r, mats);
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

        /// <summary>
        /// A room seen from inside: no warm rim (it lit every floor seen at a glancing angle, adding a third to pale marble)
        /// and the most light bands, so normal-mapped furniture shades smoothly instead of in chrome-like steps. A lighter
        /// shade tint, because the room's own key light already leaves its far side in shadow.
        /// </summary>
        public static void LightSoftly(Material m)
        {
            m.SetColor("_RimColor", Color.black);
            m.SetFloat("_Bands", InteriorBands);
            m.SetColor("_ShadeColor", new Color(0.7f, 0.72f, 0.82f, 1f));
        }

        /// <summary>Normal mapping needs tangents; generated meshes sometimes ship without them.</summary>
        static void EnsureTangents(Renderer r, Material[] mats)
        {
            if (!System.Array.Exists(mats, m => m != null && m.IsKeywordEnabled("_NORMALMAP"))) return;
            var filter = r.GetComponent<MeshFilter>();
            var skinned = r as SkinnedMeshRenderer;
            var mesh = filter != null ? filter.sharedMesh : skinned != null ? skinned.sharedMesh : null;
            if (mesh == null || mesh.tangents.Length == mesh.vertexCount) return;
            var copy = Object.Instantiate(mesh);
            copy.name = mesh.name;
            copy.RecalculateTangents();
            if (filter != null) filter.sharedMesh = copy;
            else skinned!.sharedMesh = copy;
        }

        /// <summary>
        /// A character GLB is its idle take; its other takes (same rig, one clip each) sit in Clips/&lt;name&gt;_&lt;clip&gt;.glb.
        /// The prefab gets a CharacterAnimator that plays them.
        /// </summary>
        static void AddClips(GameObject prepared, string glbPath)
        {
            var dir = Path.GetDirectoryName(glbPath)!.Replace('\\', '/');
            var name = Path.GetFileNameWithoutExtension(glbPath);
            AnimationClip? FirstClip(string path) =>
                System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.OfType<AnimationClip>(AssetDatabase.LoadAllAssetsAtPath(path)),
                    c => !c.name.StartsWith("__preview__"));
            var idle = FirstClip(glbPath);
            var others = new AnimationClip?[ClipNames.Length];
            for (int i = 0; i < ClipNames.Length; i++) others[i] = FirstClip($"{dir}/{ClipsFolder}/{name}_{ClipNames[i]}.glb");
            if (idle == null && System.Array.TrueForAll(others, c => c == null)) return;
            var animator = prepared.GetComponent<UnpaidCeo.Game.Hero.CharacterAnimator>() ?? prepared.AddComponent<UnpaidCeo.Game.Hero.CharacterAnimator>();
            animator.SetClips(idle, others[0], others[1], others[2]);
            var unityAnimator = prepared.GetComponent<Animator>();
            if (unityAnimator != null) unityAnimator.runtimeAnimatorController = null;
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
                if (kind == AssetKind.Character)
                {
                    AddClips(prepared, glbPath);
                    var (sole, top) = CharacterPoseBaker.Measure(prepared);
                    (prepared.GetComponent<UnpaidCeo.Game.Hero.StandingPose>() ?? prepared.AddComponent<UnpaidCeo.Game.Hero.StandingPose>()).Set(sole, top);
                }
                SaveTextures(prepared, $"{root}/Textures/{name}", kind);
                int n = 0;
                foreach (var mf in prepared.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && !AssetDatabase.Contains(mf.sharedMesh))
                        AssetDatabase.CreateAsset(mf.sharedMesh, $"{root}/Meshes/{name}_{n++}.asset");
                foreach (var sm in prepared.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (sm.sharedMesh != null && !AssetDatabase.Contains(sm.sharedMesh))
                        AssetDatabase.CreateAsset(sm.sharedMesh, $"{root}/Meshes/{name}_{n++}.asset");
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
        static void SaveTextures(GameObject prepared, string pathPrefix, AssetKind kind)
        {
            int n = 0;
            int maxSize = AppTextureSize(kind);
            var saved = new System.Collections.Generic.Dictionary<Texture, Texture2D>();
            foreach (var r in prepared.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    foreach (var prop in new[] { "_BaseMap", "_EmissionMap", "_BumpMap" })
                    {
                        var tex = m != null ? m.GetTexture(prop) : null;
                        if (tex == null) continue;
                        bool normal = prop == "_BumpMap";
                        if (!saved.TryGetValue(tex, out var asset))
                        {
                            var readable = tex as Texture2D;
                            if (readable == null || !readable.isReadable || Mathf.Max(tex.width, tex.height) > maxSize)
                                readable = Copy(tex, Mathf.Min(maxSize, Mathf.Max(tex.width, tex.height)), !normal);
                            // Colour maps as JPEG (a quarter of the PNG size in the repo; the GPU format is the same),
                            // normal maps as lossless PNG.
                            var path = $"{pathPrefix}_{n++}{(normal ? ".png" : ".jpg")}";
                            File.WriteAllBytes(path, normal ? readable.EncodeToPNG() : readable.EncodeToJPG(92));
                            AssetDatabase.ImportAsset(path);
                            ConfigureTexture((TextureImporter)AssetImporter.GetAtPath(path), kind, normal);
                            asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                            saved[tex] = asset;
                        }
                        m.SetTexture(prop, asset);
                    }
        }

        /// <summary>A house-shader material carrying the source's base colour, emission and normal map.</summary>
        public static Material Stylize(Material source, Shader shader, int maxSize)
        {
            var m = new Material(shader) { name = source != null ? source.name : "stylized" };
            if (source == null) return m;
            foreach (var p in BaseTextureProperties)
                if (source.HasProperty(p) && source.GetTexture(p) != null) { m.SetTexture("_BaseMap", Fit(source.GetTexture(p), maxSize, true)); break; }
            foreach (var p in EmissionTextureProperties)
                if (source.HasProperty(p) && source.GetTexture(p) != null) { m.SetTexture("_EmissionMap", Fit(source.GetTexture(p), maxSize, true)); break; }
            foreach (var p in NormalTextureProperties)
                if (source.HasProperty(p) && source.GetTexture(p) != null)
                {
                    m.SetTexture("_BumpMap", source.GetTexture(p));
                    m.EnableKeyword("_NORMALMAP");
                    break;
                }
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
