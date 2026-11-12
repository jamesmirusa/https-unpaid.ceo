using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnpaidCeo.Editor;

namespace UnpaidCeo.Editor.Tests
{
    /// <summary>Budgets for generated art: buildings 2–8k triangles at LOD0 plus two Mesh LOD levels; characters 15–20k.</summary>
    public class AssetBudgetTests
    {
        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        /// <summary>A bumpy grid with roughly the requested triangle count (2 per cell).</summary>
        Mesh Grid(int triangles)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(triangles / 2f)));
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int y = 0; y <= n; y++)
                for (int x = 0; x <= n; x++)
                {
                    float h = Mathf.PerlinNoise(x * 0.37f, y * 0.29f) * 0.6f;
                    verts.Add(new Vector3(x / (float)n, h, y / (float)n));
                    uvs.Add(new Vector2(x / (float)n, y / (float)n));
                }
            var tris = new List<int>();
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = y * (n + 1) + x;
                    tris.AddRange(new[] { i, i + n + 1, i + 1, i + 1, i + n + 1, i + n + 2 });
                }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            _created.Add(mesh);
            return mesh;
        }

        GameObject Building(Mesh mesh, Material material, bool collider = true)
        {
            var go = new GameObject("building");
            _created.Add(go);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        Material Stylized()
        {
            var m = new Material(Shader.Find(AssetBudgetValidator.StylizedShaderName));
            _created.Add(m);
            return m;
        }

        [Test]
        public void Stylized_shader_compiles()
        {
            var shader = Shader.Find(AssetBudgetValidator.StylizedShaderName);
            Assert.IsNotNull(shader, AssetBudgetValidator.StylizedShaderName + " not found");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), string.Join("\n", System.Array.ConvertAll(ShaderUtil.GetShaderMessages(shader), m => m.message)));
        }

        [Test]
        public void Building_within_budget_with_mesh_lods_passes()
        {
            var mesh = Grid(5000);
            MeshLodUtility.GenerateMeshLods(mesh, 2);
            Assert.GreaterOrEqual(mesh.lodCount, 3, "test mesh did not get two extra LOD levels");
            CollectionAssert.IsEmpty(AssetBudgetValidator.Check(Building(mesh, Stylized()), AssetKind.Building));
        }

        [Test]
        public void Building_over_budget_or_without_lods_is_reported()
        {
            var errors = AssetBudgetValidator.Check(Building(Grid(12000), Stylized()), AssetKind.Building);
            Assert.That(errors, Has.Some.Contains("LOD0"));
            Assert.That(errors, Has.Some.Contains("LOD levels"));
        }

        [Test]
        public void A_room_fills_the_screen_so_its_budget_is_above_a_buildings()
        {
            var room = Grid(20000);
            MeshLodUtility.GenerateMeshLods(room, 2);
            CollectionAssert.IsEmpty(AssetBudgetValidator.Check(Building(room, Stylized()), AssetKind.Interior));
            Assert.That(AssetBudgetValidator.Check(Building(room, Stylized()), AssetKind.Building), Has.Some.Contains("LOD0"));
            var heavy = Grid(40000);
            MeshLodUtility.GenerateMeshLods(heavy, 2);
            Assert.That(AssetBudgetValidator.Check(Building(heavy, Stylized()), AssetKind.Interior), Has.Some.Contains("30000"));
            Assert.AreEqual(GlbPrefabBuilder.MaxTextureSize(AssetKind.Building), GlbPrefabBuilder.MaxTextureSize(AssetKind.Interior));
        }

        [Test]
        public void Wrong_material_and_missing_collider_are_reported()
        {
            var mesh = Grid(4000);
            MeshLodUtility.GenerateMeshLods(mesh, 2);
            var other = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _created.Add(other);
            var errors = AssetBudgetValidator.Check(Building(mesh, other, collider: false), AssetKind.Building);
            Assert.That(errors, Has.Some.Contains(AssetBudgetValidator.StylizedShaderName));
            Assert.That(errors, Has.Some.Contains("collider"));
        }

        [Test]
        public void Character_outside_15k_to_22k_is_reported()
        {
            var low = new GameObject("character");
            _created.Add(low);
            var smr = low.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = Grid(6000);
            smr.sharedMaterial = Stylized();
            Assert.That(AssetBudgetValidator.Check(low, AssetKind.Character), Has.Some.Contains("15000"));

            smr.sharedMesh = Grid(17000);
            CollectionAssert.IsEmpty(AssetBudgetValidator.Check(low, AssetKind.Character));

            // A rigged max-quality take lands a little above 20k (Meshy ultra at 19.5k + rigging).
            smr.sharedMesh = Grid(21000);
            CollectionAssert.IsEmpty(AssetBudgetValidator.Check(low, AssetKind.Character));

            smr.sharedMesh = Grid(23000);
            Assert.That(AssetBudgetValidator.Check(low, AssetKind.Character), Has.Some.Contains("22000"));
        }

        [Test]
        public void Prefab_builder_applies_stylized_material_lods_and_collider()
        {
            var tex = new Texture2D(4, 4);
            _created.Add(tex);
            var src = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            src.SetTexture("_BaseMap", tex);
            _created.Add(src);
            var source = Building(Grid(5000), src, collider: false);

            var built = GlbPrefabBuilder.Prepare(source, AssetKind.Building);
            _created.Add(built);
            var renderer = built.GetComponentInChildren<MeshRenderer>();
            Assert.AreEqual(AssetBudgetValidator.StylizedShaderName, renderer.sharedMaterial.shader.name);
            Assert.AreSame(tex, renderer.sharedMaterial.GetTexture("_BaseMap"));
            Assert.GreaterOrEqual(built.GetComponentInChildren<MeshFilter>().sharedMesh.lodCount, 3);
            CollectionAssert.IsEmpty(AssetBudgetValidator.Check(built, AssetKind.Building));
        }

        // Prepare keeps the app-size texture; the web cap comes from the WebGL import override (MaxQualityArtTests).
        [TestCase(AssetKind.Building, 2048, 1024)]
        [TestCase(AssetKind.Prop, 1024, 512)]
        [TestCase(AssetKind.Character, 2048, 1024)]
        public void Prefab_builder_keeps_textures_at_app_size(AssetKind kind, int max, int web)
        {
            var big = new Texture2D(2048, 2048, TextureFormat.RGBA32, false);
            var pixels = new Color32[2048 * 2048];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(200, 80, 60, 255);
            big.SetPixels32(pixels);
            big.Apply();
            _created.Add(big);
            var src = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            src.SetTexture("_BaseMap", big);
            _created.Add(src);
            var source = Building(Grid(kind == AssetKind.Prop ? 1000 : 5000), src, collider: false);

            var built = GlbPrefabBuilder.Prepare(source, kind);
            _created.Add(built);
            var tex = built.GetComponentInChildren<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap");
            Assert.AreEqual(web, GlbPrefabBuilder.MaxTextureSize(kind));
            Assert.AreEqual(max, GlbPrefabBuilder.AppTextureSize(kind));
            Assert.AreEqual(max, tex.width);
            Assert.AreEqual(max, tex.height);
            var c = ((Texture2D)tex).GetPixel(max / 2, max / 2);
            Assert.AreEqual(200f / 255f, c.r, 0.03f, "colour must survive the downscale");
        }
    }
}
