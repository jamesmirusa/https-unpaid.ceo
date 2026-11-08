using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Yarn.Unity.Editor;

namespace UnpaidCeo.Editor.Tests
{
    /// <summary>The chapter 1 Yarn project must compile, use explicit line ids, and ship a Russian string table.</summary>
    public class YarnProjectTests
    {
        const string ProjectPath = "Assets/UnpaidCeo/Yarn/Chapter1.yarnproject";

        static YarnProjectImporter Importer()
        {
            var importer = AssetImporter.GetAtPath(ProjectPath) as YarnProjectImporter;
            Assert.IsNotNull(importer, ProjectPath + " is missing or not imported as a Yarn project");
            Assert.IsNotNull(importer.ImportData, "Yarn project has no import data");
            return importer;
        }

        [Test]
        public void Yarn_project_compiles_without_errors()
        {
            var data = Importer().ImportData;
            Assert.IsTrue(data.yarnFiles.Count >= 5, $"expected the chapter 1 scripts, found {data.yarnFiles.Count}");
            var errors = data.diagnostics.SelectMany(d => d.errorMessages.Select(m => (d.yarnFile != null ? d.yarnFile.name : "?") + ": " + m));
            Assert.IsFalse(data.HasCompileErrors, string.Join("\n", errors));
            Assert.IsFalse(data.containsImplicitLineIDs, "every line needs an explicit #line: id");
        }

        [Test]
        public void Yarn_project_has_russian_string_table()
        {
            var data = Importer().ImportData;
            Assert.AreEqual("en", data.baseLanguageName);
            Assert.IsTrue(data.TryGetLocalizationEntry("ru", out var ru), "no ru localisation in the Yarn project");
            Assert.IsNotNull(ru.stringsFile, "ru localisation has no strings file");
        }
    }
}
