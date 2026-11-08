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

        /// <summary>Returns a prepared copy of <paramref name="source"/>; the source is left untouched.</summary>
        public static GameObject Prepare(GameObject source, AssetKind kind)
        {
            var copy = Object.Instantiate(source);
            copy.name = source.name;
            var shader = Shader.Find(AssetBudgetValidator.StylizedShaderName);

            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Stylize(mats[i], shader);
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
                foreach (var folder in new[] { "Prefabs", "Meshes", "Materials" }) Directory.CreateDirectory($"{root}/{folder}");
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

        static Material Stylize(Material source, Shader shader)
        {
            var m = new Material(shader) { name = source != null ? source.name : "stylized" };
            if (source == null) return m;
            foreach (var p in BaseTextureProperties)
                if (source.HasProperty(p) && source.GetTexture(p) != null) { m.SetTexture("_BaseMap", source.GetTexture(p)); break; }
            foreach (var p in EmissionTextureProperties)
                if (source.HasProperty(p) && source.GetTexture(p) != null) { m.SetTexture("_EmissionMap", source.GetTexture(p)); break; }
            if (source.HasProperty("_BaseColor")) m.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            else if (source.HasProperty("baseColorFactor")) m.SetColor("_BaseColor", source.GetColor("baseColorFactor"));
            return m;
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
