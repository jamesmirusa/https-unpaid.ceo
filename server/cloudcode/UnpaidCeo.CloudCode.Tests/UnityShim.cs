using System;
using System.IO;

namespace UnityEngine
{
    /// <summary>Test-only stand-in so the linked Unity bot helpers find the content: dataPath is the repo's game/Assets.</summary>
    internal static class Application
    {
        public static string dataPath { get; } = Find();

        static string Find()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var assets = Path.Combine(dir.FullName, "game", "Assets");
                if (Directory.Exists(Path.Combine(assets, "UnpaidCeo", "Content"))) return assets;
            }
            throw new DirectoryNotFoundException("game/Assets not found above " + AppContext.BaseDirectory);
        }
    }
}
