using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// Builds the UI font asset from Inter (SIL OFL, Art/Fonts/Inter-OFL.txt): dynamic SDF atlas with the
    /// font file included, prewarmed with Latin and Cyrillic, so every runtime panel renders English and
    /// Russian on the web where no OS fonts exist. Run: -executeMethod UnpaidCeo.Editor.FontBuilder.Build
    /// </summary>
    public static class FontBuilder
    {
        public const string SourcePath = "Assets/UnpaidCeo/Art/Fonts/Inter-Regular.ttf";
        public const string RegularPath = "Assets/UnpaidCeo/Resources/UI/Fonts/Inter-Regular-SDF.asset";

        const string Prewarm =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
            "«»—–…№€₽·•− ";

        [MenuItem("Unpaid CEO/Build UI Font")]
        public static void Build()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (font == null) throw new FileNotFoundException(SourcePath);
            Directory.CreateDirectory(Path.GetDirectoryName(RegularPath));
            AssetDatabase.DeleteAsset(RegularPath);

            var asset = FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = Path.GetFileNameWithoutExtension(RegularPath);
            AssetDatabase.CreateAsset(asset, RegularPath);
            foreach (var texture in asset.atlasTextures)
            {
                texture.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(texture, asset);
            }
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.TryAddCharacters(Prewarm, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[font] Inter lacks: " + missing);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            // Stylesheets resolve url() at import time: reimport so they pick up the (re)created font asset.
            foreach (var sheet in new[] { "Assets/UnpaidCeo/Resources/UI/Tokens.uss", "Assets/UnpaidCeo/Resources/UI/UnpaidTheme.tss" })
                AssetDatabase.ImportAsset(sheet, ImportAssetOptions.ForceUpdate);
            Debug.Log("[font] built " + RegularPath + " with " + asset.characterTable.Count + " characters");
        }
    }
}
