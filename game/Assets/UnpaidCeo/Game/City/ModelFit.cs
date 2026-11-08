using UnityEngine;

namespace UnpaidCeo.Game.City
{
    /// <summary>Generated models come in arbitrary units; this scales an instance to a footprint or height and rests it on the ground.</summary>
    public static class ModelFit
    {
        public static Bounds RendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Uniformly scales so the larger horizontal side equals <paramref name="footprint"/> metres.</summary>
        public static void ToFootprint(GameObject go, float footprint) => Scale(go, b => Mathf.Max(b.size.x, b.size.z), footprint);

        /// <summary>Uniformly scales so the height equals <paramref name="height"/> metres.</summary>
        public static void ToHeight(GameObject go, float height) => Scale(go, b => b.size.y, height);

        static void Scale(GameObject go, System.Func<Bounds, float> measure, float target)
        {
            var b = RendererBounds(go);
            float m = measure(b);
            if (m > 1e-4f) go.transform.localScale *= target / m;
            Ground(go);
        }

        /// <summary>Moves the instance so its lowest point sits at its parent's ground height (y of its pivot).</summary>
        public static void Ground(GameObject go)
        {
            var b = RendererBounds(go);
            var p = go.transform.position;
            float groundY = go.transform.parent != null ? go.transform.parent.position.y : 0f;
            go.transform.position = new Vector3(p.x, p.y + (groundY - b.min.y), p.z);
        }
    }
}
