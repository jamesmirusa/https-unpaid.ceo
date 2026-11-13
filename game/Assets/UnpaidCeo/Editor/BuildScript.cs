using System;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// Web build: Brotli with decompression fallback (GitHub Pages serves .br without Content-Encoding),
    /// "Disk Size with LTO", the UnpaidCeo page template, Addressables content built before the player.
    /// Output: game/Builds/Web. Run: tools/unity.sh build-web
    /// </summary>
    public static class BuildScript
    {
        public const string WebOutput = "Builds/Web";

        public static void BuildWeb()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            ConfigureWeb();

            AddressableAssetSettings.CleanPlayerContent();
            AddressableAssetSettings.BuildPlayerContent(out var content);
            if (!string.IsNullOrEmpty(content.Error)) Fail("Addressables build failed: " + content.Error);
            Debug.Log("[build] addressables ok in " + content.Duration.ToString("0.0") + " s");

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) Fail("no scenes in build settings (run BootSceneBuilder.BuildAll)");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = WebOutput,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            Debug.Log("[build] " + report.summary.result + ", " + report.summary.totalSize / 1024 / 1024 + " MB, "
                      + report.summary.totalTime.TotalSeconds.ToString("0") + " s");
            if (report.summary.result != BuildResult.Succeeded) Fail("player build " + report.summary.result);
        }

        public static void ConfigureWeb()
        {
            PlayerSettings.companyName = "Unpaid CEO";
            PlayerSettings.productName = "Unpaid CEO";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "PROJECT:UnpaidCeo";
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            SetWasmOptimization("DiskSizeLTO");
        }

        /// <summary>UnityEditor.WebGL.UserBuildSettings lives in the WebGL module; set it by name so the editor assembly needs no platform reference.</summary>
        static void SetWasmOptimization(string value)
        {
            var settings = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.WebGL.UserBuildSettings")).FirstOrDefault(t => t != null);
            var prop = settings?.GetProperty("codeOptimization");
            if (prop == null) { Debug.LogWarning("[build] WebGL code optimization setting not found; using the default"); return; }
            prop.SetValue(null, Enum.Parse(prop.PropertyType, value));
            Debug.Log("[build] code optimization = " + value);
        }

        static void Fail(string why)
        {
            Debug.LogError("[build] " + why);
            EditorApplication.Exit(1);
        }
    }
}
