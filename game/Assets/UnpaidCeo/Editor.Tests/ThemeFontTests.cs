using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TextCore.Text;

namespace UnpaidCeo.Editor.Tests
{
    /// <summary>
    /// Runtime panels have no OS fonts on the web, so the theme must ship its own font that covers
    /// English and Russian (the default runtime font left Cyrillic labels blank in the first web build).
    /// </summary>
    public class ThemeFontTests
    {
        [Test]
        public void Ui_font_covers_english_and_russian()
        {
            var font = AssetDatabase.LoadAssetAtPath<FontAsset>(FontBuilder.RegularPath);
            Assert.IsNotNull(font, FontBuilder.RegularPath + " is missing (run FontBuilder.Build)");
            Assert.IsNotNull(font.sourceFontFile, "the font file must ship with the asset so glyphs can be added at runtime");
            Assert.IsTrue(font.TryAddCharacters("Создай героя. Следующий месяц: 1 234,56 $ — «Ёлка» Hello, CEO!", out string missing),
                          "glyphs the font cannot draw: " + missing);
        }

        [Test]
        public void Theme_sets_the_ui_font_for_runtime_panels()
        {
            var uss = File.ReadAllText("Assets/UnpaidCeo/Resources/UI/Tokens.uss");
            StringAssert.Contains("-unity-font-definition: url(\"Fonts/" + Path.GetFileName(FontBuilder.RegularPath) + "\")", uss);
            CollectionAssert.Contains(AssetDatabase.GetDependencies("Assets/UnpaidCeo/Resources/UI/Tokens.uss", false), FontBuilder.RegularPath,
                "the imported stylesheet does not reference the font (reimport Tokens.uss after building the font)");
        }
    }
}
