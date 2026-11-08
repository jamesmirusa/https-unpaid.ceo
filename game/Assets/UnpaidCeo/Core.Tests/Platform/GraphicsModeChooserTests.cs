using NUnit.Framework;
using UnpaidCeo.Core.Platform;

namespace UnpaidCeo.Core.Tests.Platform
{
    public class GraphicsModeChooserTests
    {
        [TestCase(40f, true, 4096, GraphicsMode.Eco)]
        [TestCase(34f, false, 8192, GraphicsMode.Eco)]
        [TestCase(20f, false, 4096, GraphicsMode.Optimal)]
        [TestCase(10f, false, 4096, GraphicsMode.Max)]
        [TestCase(10f, false, 2048, GraphicsMode.Max)]
        [TestCase(10f, false, 1024, GraphicsMode.Optimal)]
        [TestCase(10f, true, 4096, GraphicsMode.Optimal)]
        [TestCase(33f, true, 0, GraphicsMode.Optimal)]
        [TestCase(12f, false, 4096, GraphicsMode.Optimal)]
        public void Choose_table(float avgFrameMs, bool isMobile, int gpuMemMb, GraphicsMode expected) =>
            Assert.AreEqual(expected, GraphicsModeChooser.Choose(avgFrameMs, isMobile, gpuMemMb));

        [Test]
        public void Frame_sampler_skips_warmup_and_averages()
        {
            var s = new FrameSampler(warmupSeconds: 0.5f);
            s.Add(0.3f, 100f);   // warm-up frames are ignored
            s.Add(0.3f, 100f);
            s.Add(0.1f, 10f);
            s.Add(0.1f, 30f);
            Assert.AreEqual(20f, s.AverageMs, 1e-4f);
            Assert.AreEqual(2, s.Count);
            Assert.AreEqual(0.8f, s.Elapsed, 1e-4f);
        }

        [Test]
        public void Empty_sampler_reports_no_average() => Assert.IsNaN(new FrameSampler(0.5f).AverageMs);
    }
}
