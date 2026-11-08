using System;
using System.Collections;
using UnityEngine;
using UnpaidCeo.Core.Platform;

namespace UnpaidCeo.Game.Graphics
{
    /// <summary>
    /// First launch: renders the scene in Optimal for ~3 s, picks a mode with <see cref="GraphicsModeChooser"/>
    /// and saves it. Later launches apply the saved choice (the player can change it in Settings).
    /// GPU frame time from FrameTimingManager is preferred, because a vsync-capped frame delta hides spare headroom.
    /// </summary>
    public sealed class DeviceBenchmark : MonoBehaviour
    {
        public const float WarmupSeconds = 0.5f;
        public const float MeasureSeconds = 3f;

        public event Action<GraphicsMode> Finished;
        public float LastAverageMs { get; private set; } = float.NaN;

        readonly FrameTiming[] _timings = new FrameTiming[1];

        IEnumerator Start()
        {
            if (GraphicsModeService.TryLoadSaved(out var saved))
            {
                GraphicsModeService.Apply(saved);
                Finished?.Invoke(saved);
                yield break;
            }

            GraphicsModeService.Apply(GraphicsMode.Optimal);
            var sampler = new FrameSampler(WarmupSeconds);
            while (sampler.Elapsed < WarmupSeconds + MeasureSeconds)
            {
                yield return null;
                sampler.Add(Time.unscaledDeltaTime, FrameMs());
            }

            LastAverageMs = sampler.AverageMs;
            var mode = GraphicsModeChooser.Choose(LastAverageMs, Application.isMobilePlatform, SystemInfo.graphicsMemorySize);
            GraphicsModeService.Save(mode);
            GraphicsModeService.Apply(mode);
            Finished?.Invoke(mode);
        }

        float FrameMs()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.IsFeatureEnabled() && FrameTimingManager.GetLatestTimings(1, _timings) > 0)
            {
                var t = _timings[0];
                double ms = Math.Max(t.cpuMainThreadFrameTime, t.gpuFrameTime);
                if (ms > 0) return (float)ms;
            }
            return Time.unscaledDeltaTime * 1000f;
        }
    }
}
