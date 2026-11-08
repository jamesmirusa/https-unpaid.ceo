using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnpaidCeo.Game.Audio;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// Puts every voice clip, lesson clip and music theme into Addressables so the web build streams them
    /// on demand. One group per voice language, each clip its own bundle, so a player downloads only the
    /// lines they actually hear. Run: -executeMethod UnpaidCeo.Editor.VoiceAddressables.Apply
    /// </summary>
    public static class VoiceAddressables
    {
        public const string VoiceRoot = "Assets/UnpaidCeo/Yarn/Voice";
        public const string LessonRoot = "Assets/UnpaidCeo/Audio/Lessons";
        public const string MusicRoot = "Assets/UnpaidCeo/Audio/Music";
        public const string MusicDay = AudioAddresses.MusicDay;
        public const string MusicNeon = AudioAddresses.MusicNeon;
        static readonly string[] Languages = { "en", "ru" };

        public static string LineAddress(string lineId, string lang) => AudioAddresses.Line(lineId, lang);
        public static string LessonAddress(string lessonId, string lang) => AudioAddresses.Lesson(lessonId, lang);
        public static string VoiceGroup(string lang) => "voice_" + lang;

        [MenuItem("Unpaid CEO/Audio/Make Audio Addressable")]
        public static void Apply()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            foreach (var lang in Languages)
            {
                var group = Group(settings, VoiceGroup(lang));
                foreach (var f in Clips(VoiceRoot + "/" + lang))
                    Add(settings, group, f, LineAddress(Path.GetFileNameWithoutExtension(f), lang));
                var lessons = Group(settings, "voice_lessons");
                foreach (var f in Clips(LessonRoot + "/" + lang))
                    Add(settings, lessons, f, LessonAddress(Path.GetFileNameWithoutExtension(f), lang));
            }
            var music = Group(settings, "music");
            Add(settings, music, MusicRoot + "/music_day.mp3", MusicDay);
            Add(settings, music, MusicRoot + "/music_neon.mp3", MusicNeon);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
        }

        static string[] Clips(string dir) =>
            Directory.Exists(dir) ? Directory.GetFiles(dir, "*.mp3") : new string[0];

        static void Add(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address)
        {
            var guid = AssetDatabase.AssetPathToGUID(path.Replace('\\', '/'));
            if (string.IsNullOrEmpty(guid)) throw new FileNotFoundException("audio asset not imported: " + path);
            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.SetAddress(address, false);
        }

        static AddressableAssetGroup Group(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name) ?? settings.CreateGroup(name, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.Uncompressed;
            return group;
        }
    }
}
