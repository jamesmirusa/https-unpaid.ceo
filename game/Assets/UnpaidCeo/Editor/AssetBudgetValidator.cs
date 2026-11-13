using System.Collections.Generic;
using UnityEngine;

namespace UnpaidCeo.Editor
{
    public enum AssetKind { Building, Prop, Character }

    /// <summary>
    /// Checks a generated asset against the stage-1 art budget (spec §6): triangle range at LOD0, Mesh LOD levels
    /// for static meshes, the house shader on every material, and a collider on buildings and props.
    /// </summary>
    public static class AssetBudgetValidator
    {
        public const string StylizedShaderName = "UnpaidCeo/Stylized";
        public const int RequiredLodLevels = 3;

        public static (int min, int max) TriangleBudget(AssetKind kind)
        {
            switch (kind)
            {
                case AssetKind.Building: return (2000, 8000);
                case AssetKind.Prop: return (200, 3000);
                default: return (15000, 22000);
            }
        }

        public static List<string> Check(GameObject prefab, AssetKind kind)
        {
            var errors = new List<string>();
            var meshes = new List<Mesh>();
            var renderers = new List<Renderer>();
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) meshes.Add(mf.sharedMesh);
            foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) meshes.Add(smr.sharedMesh);
            renderers.AddRange(prefab.GetComponentsInChildren<Renderer>(true));

            if (meshes.Count == 0) { errors.Add($"{prefab.name}: no mesh"); return errors; }

            int tris = 0;
            foreach (var m in meshes) tris += TrianglesAtLod0(m);
            var (min, max) = TriangleBudget(kind);
            if (tris < min || tris > max)
                errors.Add($"{prefab.name}: {tris} triangles at LOD0, budget for {kind} is {min}–{max}");

            if (kind != AssetKind.Character)
            {
                foreach (var m in meshes)
                    if (m.lodCount < RequiredLodLevels)
                        errors.Add($"{prefab.name}: mesh '{m.name}' has {m.lodCount} LOD levels, needs {RequiredLodLevels}");
                if (prefab.GetComponentInChildren<Collider>(true) == null)
                    errors.Add($"{prefab.name}: no collider");
            }

            foreach (var r in renderers)
                foreach (var mat in r.sharedMaterials)
                    if (mat == null || mat.shader == null || mat.shader.name != StylizedShaderName)
                        errors.Add($"{prefab.name}: material '{(mat != null ? mat.name : "null")}' must use {StylizedShaderName}");

            return errors;
        }

        public static int TrianglesAtLod0(Mesh m)
        {
            long indices = 0;
            for (int s = 0; s < m.subMeshCount; s++)
                indices += m.lodCount > 1 ? m.GetIndexCount(s, 0) : m.GetIndexCount(s);
            return (int)(indices / 3);
        }
    }
}
