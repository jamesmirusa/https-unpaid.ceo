using System.IO;
using NUnit.Framework;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using UnpaidCeo.Editor;

namespace UnpaidCeo.Editor.Tests
{
    /// <summary>Every row of Localization/Source/*.tsv must be in the string table with the same text (run the importer after editing a TSV).</summary>
    public class StringTableSourceTests
    {
        [Test]
        public void String_tables_match_their_sources()
        {
            var files = Directory.GetFiles(StringTableImporter.SourceDir, "*.tsv");
            Assert.IsNotEmpty(files);
            var stale = new System.Collections.Generic.List<string>();
            foreach (var path in files)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var collection = LocalizationEditorSettings.GetStringTableCollection(name);
                Assert.IsNotNull(collection, $"table '{name}' is missing — run Unpaid CEO/Import string tables");
                foreach (var locale in StringTableImporter.Read(path))
                {
                    var table = collection.GetTable(locale.Key) as StringTable;
                    Assert.IsNotNull(table, $"{name} has no '{locale.Key}' table");
                    foreach (var row in locale.Value)
                    {
                        Assert.IsFalse(string.IsNullOrWhiteSpace(row.Value), $"{name}/{locale.Key}/{row.Key} is empty in the TSV");
                        if (table.GetEntry(row.Key)?.Value != row.Value) stale.Add($"{name}/{locale.Key}/{row.Key}");
                    }
                }
            }
            Assert.IsEmpty(stale, "tables out of date — run Unpaid CEO/Import string tables:\n" + string.Join("\n", stale));
        }
    }
}
