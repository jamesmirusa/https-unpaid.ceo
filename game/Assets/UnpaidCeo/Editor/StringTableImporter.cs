using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// Localization/Source/&lt;Table&gt;.tsv (key, then one column per locale code) is the source of truth for UI and
    /// Academy strings. This writes it into the matching Unity Localization string table collection, creating the
    /// collection and missing locales if needed. Run from the menu or in batchmode with -executeMethod.
    /// </summary>
    public static class StringTableImporter
    {
        public const string SourceDir = "Assets/UnpaidCeo/Localization/Source";
        public const string TablesDir = "Assets/UnpaidCeo/Localization";

        [MenuItem("Unpaid CEO/Import string tables")]
        public static void ImportAll()
        {
            foreach (var path in Directory.GetFiles(SourceDir, "*.tsv")) Import(Path.GetFileNameWithoutExtension(path), path);
            AssetDatabase.SaveAssets();
        }

        public static Dictionary<string, Dictionary<string, string>> Read(string tsvPath)
        {
            var lines = File.ReadAllLines(tsvPath);
            var header = lines[0].Split('\t');
            if (header[0] != "key") throw new FormatException(tsvPath + ": first column must be 'key'");
            var byLocale = new Dictionary<string, Dictionary<string, string>>();
            for (int c = 1; c < header.Length; c++) byLocale[header[c]] = new Dictionary<string, string>();
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cells = lines[i].Split('\t');
                for (int c = 1; c < header.Length && c < cells.Length; c++) byLocale[header[c]][cells[0]] = cells[c];
            }
            return byLocale;
        }

        public static void Import(string tableName, string tsvPath)
        {
            var source = Read(tsvPath);
            foreach (var code in source.Keys)
                if (LocalizationEditorSettings.GetLocale(new LocaleIdentifier(code)) == null)
                {
                    var locale = Locale.CreateLocale(new LocaleIdentifier(code));
                    Directory.CreateDirectory(TablesDir + "/Locales");
                    AssetDatabase.CreateAsset(locale, $"{TablesDir}/Locales/{code}.asset");
                    LocalizationEditorSettings.AddLocale(locale, false);
                }

            var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
            if (collection == null)
            {
                Directory.CreateDirectory($"{TablesDir}/{tableName}");
                collection = LocalizationEditorSettings.CreateStringTableCollection(tableName, $"{TablesDir}/{tableName}");
            }

            foreach (var pair in source)
            {
                var table = collection.GetTable(pair.Key) as StringTable;
                if (table == null) { Debug.LogError($"{tableName}: no '{pair.Key}' table"); continue; }
                foreach (var entry in pair.Value)
                {
                    var existing = table.GetEntry(entry.Key);
                    if (existing != null) existing.Value = entry.Value;
                    else table.AddEntry(entry.Key, entry.Value);
                }
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData);
            Debug.Log($"[strings] {tableName}: {string.Join(", ", source.Keys)} imported");
        }
    }
}
