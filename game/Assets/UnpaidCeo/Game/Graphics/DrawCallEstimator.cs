using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnpaidCeo.Game.Graphics
{
    /// <summary>
    /// Deterministic upper estimate of draw calls for the active URP asset: one per visible renderer material
    /// in the forward pass, the same again for the depth pass when a depth texture is required, one per
    /// shadow-casting material within shadow distance for each cascade, plus a few full-screen passes.
    /// Used as the CI gate because batchmode does not populate Unity's render-stats counters;
    /// real counts are checked on devices with the development-build stats overlay.
    /// </summary>
    public static class DrawCallEstimator
    {
        public const int FullScreenPasses = 3;

        public readonly struct Result
        {
            public readonly int Forward, Depth, Shadows;
            public Result(int forward, int depth, int shadows) { Forward = forward; Depth = depth; Shadows = shadows; }
            public int Total => Forward + Depth + Shadows + FullScreenPasses;
        }

        public static Result Estimate(Camera camera)
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            bool shadows = asset != null && asset.supportsMainLightShadows;
            float shadowDistance = asset != null ? asset.shadowDistance : 0f;
            int cascades = asset != null ? Mathf.Max(1, asset.shadowCascadeCount) : 1;
            bool depth = asset != null && asset.supportsCameraDepthTexture;

            int forward = 0, casters = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || (camera.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                int materials = Mathf.Max(1, r.sharedMaterials.Length);
                if (GeometryUtility.TestPlanesAABB(planes, r.bounds)) forward += materials;
                if (shadows && r.shadowCastingMode != ShadowCastingMode.Off &&
                    Vector3.Distance(camera.transform.position, r.bounds.ClosestPoint(camera.transform.position)) <= shadowDistance + r.bounds.extents.magnitude)
                    casters += materials;
            }
            return new Result(forward, depth ? forward : 0, casters * cascades);
        }
    }
}
