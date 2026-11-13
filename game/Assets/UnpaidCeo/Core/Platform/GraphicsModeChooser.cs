namespace UnpaidCeo.Core.Platform
{
    public enum GraphicsMode { Eco, Optimal, Max }

    /// <summary>First-launch choice of graphics mode from a short benchmark (spec §5.5).</summary>
    public static class GraphicsModeChooser
    {
        public const float EcoAboveMs = 33f;
        public const float MaxBelowMs = 12f;
        public const int MaxMinGpuMemoryMb = 2048;

        public static GraphicsMode Choose(float avgFrameMs, bool isMobile, int gpuMemMb)
        {
            if (avgFrameMs > EcoAboveMs) return GraphicsMode.Eco;
            if (avgFrameMs < MaxBelowMs && !isMobile && gpuMemMb >= MaxMinGpuMemoryMb) return GraphicsMode.Max;
            return GraphicsMode.Optimal;
        }
    }

    /// <summary>Averages frame times after a warm-up period (frames that start inside the warm-up are ignored).</summary>
    public sealed class FrameSampler
    {
        readonly float _warmup;
        double _sumMs;

        public FrameSampler(float warmupSeconds) { _warmup = warmupSeconds; }

        public float Elapsed { get; private set; }
        public int Count { get; private set; }
        public float AverageMs => Count == 0 ? float.NaN : (float)(_sumMs / Count);

        public void Add(float deltaSeconds, float frameMs)
        {
            if (Elapsed >= _warmup) { _sumMs += frameMs; Count++; }
            Elapsed += deltaSeconds;
        }
    }
}
