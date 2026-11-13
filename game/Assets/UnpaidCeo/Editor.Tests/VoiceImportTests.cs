using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnpaidCeo.Game.Audio;
using Yarn.Unity;
using Yarn.Unity.Editor;

namespace UnpaidCeo.Editor.Tests
{
    /// <summary>
    /// Voice-over wiring: Yarn finds each language's clips by line id, the clips load on demand through
    /// Addressables (so the web build's first download stays small), and they import as light mono audio.
    /// </summary>
    public class VoiceImportTests
    {
        const string ProjectPath = "Assets/UnpaidCeo/Yarn/Chapter1.yarnproject";
        const string VoiceRoot = "Assets/UnpaidCeo/Yarn/Voice";
        const string LessonRoot = "Assets/UnpaidCeo/Audio/Lessons";
        static readonly string[] Languages = { "en", "ru" };

        static YarnProjectImporter Importer() => (YarnProjectImporter)AssetImporter.GetAtPath(ProjectPath);

        [Test]
        public void Yarn_project_takes_voice_from_each_language_folder()
        {
            var importer = Importer();
            foreach (var lang in Languages)
            {
                var loc = importer.ImportData.localizations.FirstOrDefault(l => l.languageID == lang);
                Assert.IsNotNull(loc, "no " + lang + " localisation");
                Assert.IsNotNull(loc.assetsFolder, lang + " localisation has no voice folder");
                Assert.AreEqual(VoiceRoot + "/" + lang, AssetDatabase.GetAssetPath(loc.assetsFolder));
            }
            Assert.IsTrue(importer.useAddressableAssets, "voice clips must load on demand to keep the web download small");
        }

        [Test]
        public void Voice_addresses_match_the_ones_yarn_spinner_looks_up()
        {
            var yarnAddress = typeof(Localization).GetMethod("GetAddressForLine", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(yarnAddress, "Yarn Spinner no longer exposes GetAddressForLine; re-check the address format");
            Assert.AreEqual(yarnAddress.Invoke(null, new object[] { "line:ch1_mentor_welcome_02", "ru" }),
                            VoiceAddressables.LineAddress("ch1_mentor_welcome_02", "ru"));
        }

        [Test]
        public void Every_voice_clip_is_addressable_in_its_language_group()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var wrong = new List<string>();
            foreach (var lang in Languages)
            foreach (var f in Directory.GetFiles(VoiceRoot + "/" + lang, "*.mp3"))
            {
                var id = Path.GetFileNameWithoutExtension(f);
                var e = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(f.Replace('\\', '/')));
                if (e == null || e.address != VoiceAddressables.LineAddress(id, lang) || e.parentGroup.Name != VoiceAddressables.VoiceGroup(lang))
                    wrong.Add(lang + "/" + id + " -> " + (e == null ? "not addressable" : e.parentGroup.Name + ":" + e.address));
            }
            Assert.IsEmpty(wrong, string.Join("\n", wrong.Take(8)));
        }

        [Test]
        public void Every_lesson_clip_and_music_theme_is_addressable()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var wrong = new List<string>();
            foreach (var lang in Languages)
            foreach (var f in Directory.GetFiles(LessonRoot + "/" + lang, "*.mp3"))
            {
                var id = Path.GetFileNameWithoutExtension(f);
                var e = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(f.Replace('\\', '/')));
                if (e == null || e.address != VoiceAddressables.LessonAddress(id, lang)) wrong.Add(lang + "/" + id);
            }
            foreach (var address in new[] { VoiceAddressables.MusicDay, VoiceAddressables.MusicNeon,
                                            AudioAddresses.DistrictMusic("brooklyn"), AudioAddresses.DistrictMusic("queens") })
                if (!settings.groups.Any(g => g.entries.Any(e => e.address == address))) wrong.Add(address);
            Assert.IsEmpty(wrong, string.Join(", ", wrong));
        }

        [Test]
        public void Voice_clips_import_as_light_mono_audio_loaded_on_demand()
        {
            var imp = (AudioImporter)AssetImporter.GetAtPath(VoiceRoot + "/en/ch1_mentor_welcome_02.mp3");
            Assert.IsNotNull(imp);
            Assert.IsTrue(imp.forceToMono);
            var s = imp.defaultSampleSettings;
            Assert.AreEqual(AudioCompressionFormat.Vorbis, s.compressionFormat);
            Assert.AreEqual(AudioClipLoadType.CompressedInMemory, s.loadType);
            Assert.That(s.quality, Is.LessThanOrEqualTo(0.5f));
            Assert.IsFalse(s.preloadAudioData);
        }
    }
}
