using UnityEditor;
using UnityEngine;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// Import settings by folder: voice and lessons are light mono Vorbis loaded on demand, music streams,
    /// UI effects decompress on load so they fire without delay.
    /// </summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        const string Root = "Assets/UnpaidCeo/";

        void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;
            var s = importer.defaultSampleSettings;
            if (assetPath.StartsWith(Root + "Yarn/Voice/") || assetPath.StartsWith(Root + "Audio/Lessons/"))
            {
                importer.forceToMono = true;
                importer.loadInBackground = true;
                s.loadType = AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.35f;
                s.preloadAudioData = false;
                s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                s.sampleRateOverride = 22050;
            }
            else if (assetPath.StartsWith(Root + "Audio/Music/"))
            {
                importer.loadInBackground = true;
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.5f;
                s.preloadAudioData = false;
            }
            else if (assetPath.StartsWith(Root + "Resources/Audio/Sfx/"))
            {
                importer.forceToMono = true;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.ADPCM;
                s.preloadAudioData = true;
            }
            else return;
            importer.defaultSampleSettings = s;
        }
    }
}
