using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// Pictures of the places (Art/Places/place_&lt;id&gt;.jpg, 4K, 3:2): import settings and Addressables. Each picture is
    /// its own bundle in the "places" group, so the web build downloads a place's picture when the player walks in.
    ///   tools/unity-run.sh UnpaidCeo.Editor.PlaceArtImporter.ImportAll
    /// </summary>
    public static class PlaceArtImporter
    {
        public const string Folder = "Assets/UnpaidCeo/Art/Places";
        public const string Group = "places";

        [MenuItem("Unpaid CEO/Art/Import place pictures")]
        public static void ImportAll()
        {
            AssetDatabase.Refresh();
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(Group) ?? settings.CreateGroup(Group, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 4096;   // the owner asked for 4K art; each picture is its own on-demand bundle
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                var entry = settings.CreateOrMoveEntry(guid, group);
                entry.address = Path.GetFileNameWithoutExtension(path);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[places] {count} pictures in group '{Group}'");
        }
    }
}
