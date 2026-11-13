using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnpaidCeo.Editor;

namespace UnpaidCeo.Editor.Tests
{
    /// <summary>
    /// Max-quality art: textures ship at full size (2048) and keep their normal maps in the store apps, while the
    /// web build keeps its download budget through WebGL texture overrides.
    /// </summary>
    public class MaxQualityArtTests
    {
        const string TempDir = "Assets/UnpaidCeo/Editor.Tests/TempMaxQuality";

        [TearDown]
        public void Cleanup() => AssetDatabase.DeleteAsset(TempDir);

        static TextureImporter TempTexture(string name)
        {
            Directory.CreateDirectory(TempDir);
            var path = $"{TempDir}/{name}.png";
            File.WriteAllBytes(path, new Texture2D(64, 64).EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            return (TextureImporter)AssetImporter.GetAtPath(path);
        }

        [TestCase(AssetKind.Building, 2048, 1024)]
        [TestCase(AssetKind.Character, 2048, 1024)]
        [TestCase(AssetKind.Prop, 1024, 512)]
        public void Textures_ship_full_size_in_apps_and_capped_on_the_web(AssetKind kind, int apps, int web)
        {
            var importer = TempTexture("base_" + kind);
            GlbPrefabBuilder.ConfigureTexture(importer, kind, normalMap: false);

            Assert.AreEqual(apps, importer.maxTextureSize);
            var webgl = importer.GetPlatformTextureSettings("WebGL");
            Assert.IsTrue(webgl.overridden);
            Assert.AreEqual(web, webgl.maxTextureSize);
            Assert.IsTrue(importer.sRGBTexture);
            Assert.AreEqual(web, GlbPrefabBuilder.MaxTextureSize(kind));
        }

        [Test]
        public void Normal_maps_are_imported_as_normal_maps_and_smaller_on_the_web()
        {
            var importer = TempTexture("normal");
            GlbPrefabBuilder.ConfigureTexture(importer, AssetKind.Building, normalMap: true);

            Assert.AreEqual(TextureImporterType.NormalMap, importer.textureType);
            Assert.AreEqual(2048, importer.maxTextureSize);
            Assert.AreEqual(GlbPrefabBuilder.WebNormalMapSize, importer.GetPlatformTextureSettings("WebGL").maxTextureSize);
        }

        [Test]
        public void Stylized_shader_supports_normal_maps()
        {
            var shader = Shader.Find(AssetBudgetValidator.StylizedShaderName);
            Assert.GreaterOrEqual(shader.FindPropertyIndex("_BumpMap"), 0);
            Assert.IsTrue(shader.keywordSpace.FindKeyword("_NORMALMAP").isValid);
        }

        [Test]
        public void Stylize_keeps_the_normal_map_and_turns_it_on()
        {
            var source = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var normal = new Texture2D(8, 8, TextureFormat.RGBA32, true, true);
            source.SetTexture("_BumpMap", normal);

            var m = GlbPrefabBuilder.Stylize(source, Shader.Find(AssetBudgetValidator.StylizedShaderName), 1024);

            Assert.AreSame(normal, m.GetTexture("_BumpMap"));
            Assert.IsTrue(m.IsKeywordEnabled("_NORMALMAP"));
        }

        [Test]
        public void Materials_without_a_normal_map_leave_it_off()
        {
            var source = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var m = GlbPrefabBuilder.Stylize(source, Shader.Find(AssetBudgetValidator.StylizedShaderName), 1024);
            Assert.IsFalse(m.IsKeywordEnabled("_NORMALMAP"));
        }
    }
}
