using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnpaidCeo.Core.Platform;
using UnpaidCeo.Game.Graphics;

namespace UnpaidCeo.Editor.Tests
{
    /// <summary>Spec §5.5: Eco / Optimal / Max are quality levels, each with its own URP asset.</summary>
    public class GraphicsSettingsTests
    {
        static UniversalRenderPipelineAsset Asset(GraphicsMode mode)
        {
            int index = Array.IndexOf(QualitySettings.names, GraphicsModeService.QualityName(mode));
            Assert.GreaterOrEqual(index, 0, $"quality level '{mode}' is missing");
            var asset = QualitySettings.GetRenderPipelineAssetAt(index) as UniversalRenderPipelineAsset;
            Assert.IsNotNull(asset, $"quality level '{mode}' has no URP asset");
            return asset;
        }

        [Test]
        public void Three_quality_levels_with_urp_assets()
        {
            CollectionAssert.AreEqual(new[] { "Eco", "Optimal", "Max" }, QualitySettings.names);
            Assert.IsInstanceOf<UniversalRenderPipelineAsset>(GraphicsSettings.defaultRenderPipeline, "no default URP asset");
        }

        [Test]
        public void Each_mode_matches_the_spec_table()
        {
            var eco = Asset(GraphicsMode.Eco);
            var optimal = Asset(GraphicsMode.Optimal);
            var max = Asset(GraphicsMode.Max);

            Assert.AreEqual(0.75f, eco.renderScale, 1e-4f);
            Assert.AreEqual(1f, optimal.renderScale, 1e-4f);
            Assert.AreEqual(1f, max.renderScale, 1e-4f);

            Assert.IsFalse(eco.supportsMainLightShadows, "Eco has no realtime shadows");
            Assert.IsTrue(optimal.supportsMainLightShadows && optimal.supportsSoftShadows);
            Assert.IsTrue(max.supportsMainLightShadows && max.supportsSoftShadows);
            Assert.Greater(max.shadowCascadeCount, optimal.shadowCascadeCount, "Max uses more shadow cascades");

            Assert.AreEqual(30, GraphicsModeService.TargetFrameRate(GraphicsMode.Eco));
            Assert.AreEqual(60, GraphicsModeService.TargetFrameRate(GraphicsMode.Optimal));
            Assert.AreEqual(60, GraphicsModeService.TargetFrameRate(GraphicsMode.Max));
            Assert.IsFalse(GraphicsModeService.PostProcessing(GraphicsMode.Eco));
            Assert.IsTrue(GraphicsModeService.PostProcessing(GraphicsMode.Optimal));
        }

        [Test]
        public void Texture_resolution_drops_per_mode()
        {
            // globalTextureMipmapLimit: 0 = full (2048), 1 = half (1024), 2 = quarter (512).
            int Limit(GraphicsMode m)
            {
                int current = QualitySettings.GetQualityLevel();
                QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, GraphicsModeService.QualityName(m)), false);
                int limit = QualitySettings.globalTextureMipmapLimit;
                QualitySettings.SetQualityLevel(current, false);
                return limit;
            }
            Assert.AreEqual(2, Limit(GraphicsMode.Eco));
            Assert.AreEqual(1, Limit(GraphicsMode.Optimal));
            Assert.AreEqual(0, Limit(GraphicsMode.Max));
        }
    }
}
