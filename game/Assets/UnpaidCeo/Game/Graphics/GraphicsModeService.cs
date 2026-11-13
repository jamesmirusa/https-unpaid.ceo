using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnpaidCeo.Core.Platform;

namespace UnpaidCeo.Game.Graphics
{
    /// <summary>
    /// Applies a graphics mode: the matching quality level (which carries its URP asset, shadows and texture size),
    /// the frame-rate cap and post-processing on every camera. The player's choice is stored in PlayerPrefs.
    /// </summary>
    public static class GraphicsModeService
    {
        public const string PrefsKey = "unpaidceo.graphics.mode";

        public static GraphicsMode Current { get; private set; } = GraphicsMode.Optimal;
        public static event Action<GraphicsMode> Changed;

        public static string QualityName(GraphicsMode m) => m.ToString();
        public static int TargetFrameRate(GraphicsMode m) => m == GraphicsMode.Eco ? 30 : 60;
        public static bool PostProcessing(GraphicsMode m) => m != GraphicsMode.Eco;

        public static void Apply(GraphicsMode m)
        {
            int index = Array.IndexOf(QualitySettings.names, QualityName(m));
            if (index >= 0) QualitySettings.SetQualityLevel(index, true);
            else Debug.LogWarning($"Quality level '{QualityName(m)}' is missing");
            Application.targetFrameRate = TargetFrameRate(m);
            foreach (var cam in Camera.allCameras) ApplyTo(cam, m);
            Current = m;
            Changed?.Invoke(m);
        }

        /// <summary>Cameras created after <see cref="Apply"/> call this to pick up the current mode.</summary>
        public static void ApplyTo(Camera cam, GraphicsMode m)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = PostProcessing(m);
        }

        public static bool TryLoadSaved(out GraphicsMode mode)
        {
            mode = GraphicsMode.Optimal;
            if (!PlayerPrefs.HasKey(PrefsKey)) return false;
            int v = PlayerPrefs.GetInt(PrefsKey);
            if (!Enum.IsDefined(typeof(GraphicsMode), v)) return false;
            mode = (GraphicsMode)v;
            return true;
        }

        public static void Save(GraphicsMode mode)
        {
            PlayerPrefs.SetInt(PrefsKey, (int)mode);
            PlayerPrefs.Save();
        }
    }
}
