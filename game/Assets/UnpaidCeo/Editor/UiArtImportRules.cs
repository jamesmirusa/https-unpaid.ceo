using UnityEditor;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// Interface art (stage 5B): icons and the 9-slice shadow are flat UI images — no mipmaps, clamped, alpha as
    /// transparency, small. Service icons are white masks tinted in USS; game icons keep their colours.
    /// </summary>
    public sealed class UiArtImportRules : AssetPostprocessor
    {
        const string Root = "Assets/UnpaidCeo/Resources/UI/";

        void OnPreprocessTexture()
        {
            if (!(assetPath.StartsWith(Root + "Icons/") || assetPath.StartsWith(Root + "GameIcons/") || assetPath.StartsWith(Root + "Kit/"))) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.alphaIsTransparency = true;
            t.mipmapEnabled = false;
            t.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            t.npotScale = TextureImporterNPOTScale.None;
            t.maxTextureSize = 256;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
