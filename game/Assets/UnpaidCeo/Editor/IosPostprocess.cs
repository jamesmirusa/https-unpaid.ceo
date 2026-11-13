#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// Stage 6 · Info.plist after an iOS build: the game uses only HTTPS (exempt encryption), so App Store Connect
    /// does not ask the export-compliance question on every upload.
    /// </summary>
    public static class IosPostprocess
    {
        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
