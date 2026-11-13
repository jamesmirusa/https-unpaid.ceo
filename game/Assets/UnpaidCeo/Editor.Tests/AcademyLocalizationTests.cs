using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using UnpaidCeo.Core.Content;

namespace UnpaidCeo.Editor.Tests
{
    /// <summary>Every Academy lesson key must have English and Russian text in the "Academy" string table.</summary>
    public class AcademyLocalizationTests
    {
        static ContentDb Content()
        {
            var path = Path.Combine(UnityEngine.Application.dataPath, "UnpaidCeo/Content/lessons.json");
            Assert.IsTrue(File.Exists(path), "lessons.json is missing");
            return ContentDb.FromJson(new ContentFiles { Lessons = File.ReadAllText(path) });
        }

        static IEnumerable<string> Keys(LessonDef l)
        {
            yield return l.TitleKey;
            yield return l.BodyKey;
            foreach (var q in l.Quiz)
            {
                yield return q.QuestionKey;
                foreach (var o in q.OptionKeys) yield return o;
            }
        }

        [Test]
        public void Chapter1_has_the_five_lessons()
        {
            var db = Content();
            // Chapters 2–3 add their lessons (stage 2A); the five chapter-1 lessons stay.
            CollectionAssert.IsSubsetOf(new[] { "revenue_vs_profit", "reserve", "first_loss", "cash_gap", "break_even" }, db.Lessons.Keys.ToList());
            Assert.IsTrue(db.Validate().IsValid, string.Join("\n", db.Validate().Errors));
        }

        [Test]
        public void Lesson_keys_exist_in_en_and_ru()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("Academy");
            Assert.IsNotNull(collection, "string table collection 'Academy' is missing");
            var missing = new List<string>();
            foreach (var code in new[] { "en", "ru" })
            {
                var table = collection.GetTable(code) as StringTable;
                Assert.IsNotNull(table, $"Academy has no '{code}' table");
                foreach (var key in Content().Lessons.Values.SelectMany(Keys))
                {
                    var entry = table.GetEntry(key);
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Value)) missing.Add(code + ": " + key);
                }
            }
            Assert.IsEmpty(missing, string.Join("\n", missing));
        }
    }
}
