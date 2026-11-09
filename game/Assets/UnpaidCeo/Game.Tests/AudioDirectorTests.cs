using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnpaidCeo.Game.Audio;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Music follows the day/neon light, and voice ducks the music while a line plays.</summary>
    public class AudioDirectorTests
    {
        AudioDirector _director;
        float _night;

        static AudioClip Tone(string name, float seconds) =>
            AudioClip.Create(name, Mathf.CeilToInt(44100 * seconds), 1, 44100, false);

        [SetUp]
        public void SetUp()
        {
            _night = 0f;
            _director = new GameObject("Audio").AddComponent<AudioDirector>();
            _director.Configure(Tone("day", 2), Tone("neon", 2), () => _night);
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_director.gameObject);

        [UnityTest]
        public IEnumerator Music_crossfades_to_neon_theme_when_night_falls()
        {
            yield return null;
            _director.Tick(0.1f);
            Assert.That(_director.DayVolume, Is.EqualTo(_director.MusicVolume).Within(0.01f), "day theme leads in daylight");
            Assert.That(_director.NightVolume, Is.EqualTo(0f).Within(0.01f));

            _night = 1f;
            _director.Tick(AudioDirector.CrossfadeSeconds / 2f);
            Assert.That(_director.NightVolume, Is.GreaterThan(0.2f * _director.MusicVolume), "crossfade under way");
            Assert.That(_director.DayVolume, Is.GreaterThan(0.2f * _director.MusicVolume), "no hard cut");

            _director.Tick(AudioDirector.CrossfadeSeconds);
            Assert.That(_director.NightVolume, Is.EqualTo(_director.MusicVolume).Within(0.01f));
            Assert.That(_director.DayVolume, Is.EqualTo(0f).Within(0.01f));
            Assert.IsTrue(_director.MusicPlaying, "both loops keep running so the switch is seamless");
        }

        [UnityTest]
        public IEnumerator Voice_ducks_music_and_stops_when_the_line_ends()
        {
            yield return null;
            _director.Tick(5f);
            var full = _director.DayVolume;

            _director.PlayVoice(Tone("line", 3));
            _director.Tick(1f);
            Assert.IsTrue(_director.VoicePlaying);
            Assert.That(_director.DayVolume, Is.EqualTo(full * AudioDirector.VoiceDuck).Within(0.01f));

            _director.StopVoice();
            _director.Tick(5f);
            Assert.IsFalse(_director.VoicePlaying);
            Assert.That(_director.DayVolume, Is.EqualTo(full).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Swapping_the_district_theme_keeps_the_neon_loop_running()
        {
            yield return null;
            _night = 1f;
            _director.Tick(AudioDirector.CrossfadeSeconds * 2f);
            var neonVolume = _director.NightVolume;

            _director.SetDayTheme(Tone("brooklyn", 2));
            yield return null;
            Assert.AreEqual("brooklyn", _director.DayTheme, "the district's own theme takes the day layer");
            Assert.AreEqual("neon", _director.NightTheme, "neon nights sound the same in every district");
            Assert.IsTrue(_director.MusicPlaying);
            Assert.That(_director.NightVolume, Is.EqualTo(neonVolume).Within(0.01f), "the swap does not touch the mix");

            _director.SetDayTheme(null);
            Assert.AreEqual("brooklyn", _director.DayTheme, "a missing clip keeps the current theme");
        }

        [Test]
        public void Each_district_has_its_day_theme()
        {
            Assert.AreEqual(AudioAddresses.MusicDay, AudioAddresses.DistrictMusic("bronx"), "the Bronx keeps the chapter-1 theme");
            Assert.AreEqual("music_brooklyn", AudioAddresses.DistrictMusic("brooklyn"));
            Assert.AreEqual("music_queens", AudioAddresses.DistrictMusic("queens"));
        }

        [UnityTest]
        public IEnumerator Null_voice_clip_is_ignored()
        {
            yield return null;
            _director.PlayVoice(null);
            Assert.IsFalse(_director.VoicePlaying, "lines without a clip (e.g. a language without voice) stay silent");
        }
    }
}
