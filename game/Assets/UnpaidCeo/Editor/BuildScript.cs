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
            // Diagnostics: UC_DIAG=1 builds to Builds/WebDiag with wasm function names, so a browser stack trace is readable.
            bool diag = Environment.GetEnvironmentVariable("UC_DIAG") == "1";
            PlayerSettings.WebGL.emscriptenArgs = diag ? "--profiling-funcs" : "";
            string output = diag ? "Builds/WebDiag" : WebOutput;

            AddressableAssetSettings.CleanPlayerContent();
            AddressableAssetSettings.BuildPlayerContent(out var content);
            if (!string.IsNullOrEmpty(content.Error)) Fail("Addressables build failed: " + content.Error);
            Debug.Log("[build] addressables ok in " + content.Duration.ToString("0.0") + " s");

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) Fail("no scenes in build settings (run BootSceneBuilder.BuildAll)");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
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
            // Hashed file names: a returning player's browser cache can never pair an old data file with new code
            // (that mismatch crashed the game at startup with "Maximum call stack size exceeded").
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            SetWasmOptimization("DiskSizeLTO");
        }

        public const string IosOutput = "Builds/iOS", IosSimOutput = "Builds/iOSSim";
        public const string BundleId = "ceo.unpaid.game";
        public const string DevelopmentTeam = "5MT347728K";

        /// <summary>
        /// Stage 6 · iPhone: an Xcode project from the same game (IL2CPP, ARM64, Metal), Addressables built for iOS first.
        /// UC_IOS_SIM=1 targets the Simulator SDK (Builds/iOSSim). Run: tools/unity.sh build-ios
        /// Signing stays with the owner: automatic signing on, team chosen in Xcode.
        /// </summary>
        public static void BuildIos()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            bool sim = Environment.GetEnvironmentVariable("UC_IOS_SIM") == "1";
            EditorUserBuildSettings.development = false;   // no on-screen development console in what players (and the owner) see
            ConfigureIos(sim);

            AddressableAssetSettings.CleanPlayerContent();
            AddressableAssetSettings.BuildPlayerContent(out var content);
            if (!string.IsNullOrEmpty(content.Error)) Fail("Addressables build failed: " + content.Error);
            Debug.Log("[build] addressables (iOS) ok in " + content.Duration.ToString("0.0") + " s");

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) Fail("no scenes in build settings (run BootSceneBuilder.BuildAll)");
            // A fresh folder each time: replacing an existing Xcode project makes Unity ask Xcode (scripting bridge) whether
            // it is open, and that query crashes the editor with Xcode 27 ("-[SBApplication workspaceDocuments]").
            string output = sim ? IosSimOutput : IosOutput;
            if (System.IO.Directory.Exists(output)) System.IO.Directory.Delete(output, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            });
            Debug.Log("[build] iOS " + (sim ? "simulator " : "") + report.summary.result + ", " + report.summary.totalSize / 1024 / 1024 + " MB, "
                      + report.summary.totalTime.TotalSeconds.ToString("0") + " s");
            if (report.summary.result != BuildResult.Succeeded) Fail("player build " + report.summary.result);
        }

        public static void ConfigureIos(bool simulator)
        {
            PlayerSettings.companyName = "Unpaid CEO";
            PlayerSettings.productName = "Unpaid CEO";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.bundleVersion = "0.6.0";
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.iOS.targetOSVersionString = "16.0";
            PlayerSettings.iOS.sdkVersion = simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;   // iPad layouts are not designed or tested yet
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = false;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.appleDeveloperTeamID = DevelopmentTeam;   // the owner's Apple Developer team (not a secret: it is in every signed app)
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1);   // ARM64
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Medium);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, true);   // Metal
            Debug.Log("[build] iOS settings: " + BundleId + ", iOS 16+, IL2CPP ARM64, " + (simulator ? "Simulator SDK" : "Device SDK"));
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
