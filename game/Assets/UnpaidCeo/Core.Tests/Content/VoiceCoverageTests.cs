using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace UnpaidCeo.Core.Tests.Content
{
    /// <summary>
    /// Chapter 1 voice-over: every spoken Yarn line (dialogue and narration) and every Academy lesson body
    /// ships an English and a Russian clip. Player options ("->") are not voiced: the hero is made by the
    /// player and has no fixed voice. Clips are named by line id (Yarn voice-over convention).
    /// </summary>
    public class VoiceCoverageTests
    {
        static string Root => Path.Combine(UnityEngine.Application.dataPath, "UnpaidCeo");
        static string YarnDir => Path.Combine(Root, "Yarn");
        static string YarnVoiceDir => Path.Combine(YarnDir, "Voice");
        static string LessonVoiceDir => Path.Combine(Root, "Audio", "Lessons");
        static readonly string[] Languages = { "en", "ru" };
        static readonly Regex LineTag = new Regex(@"#line:(\S+)");
        static readonly Regex Speaker = new Regex(@"^([^:]{1,30}):\s");
        static readonly Regex LessonId = new Regex("\"id\"\\s*:\\s*\"([^\"]+)\"");

        static List<(string id, string speaker)> SpokenLines()
        {
            var lines = new List<(string, string)>();
            foreach (var file in Directory.GetFiles(YarnDir, "*.yarn").OrderBy(f => f))
            foreach (var raw in File.ReadLines(file))
            {
                var m = LineTag.Match(raw);
                if (!m.Success) continue;
                var text = raw.Substring(0, m.Index).Trim();
                if (text.StartsWith("->")) continue;
                var s = Speaker.Match(text);
                lines.Add((m.Groups[1].Value, s.Success ? s.Groups[1].Value : "Narrator"));
            }
            return lines;
        }

        static List<string> LessonIds() =>
            LessonId.Matches(File.ReadAllText(Path.Combine(Root, "Content", "lessons.json")))
                .Cast<Match>().Select(m => m.Groups[1].Value).ToList();

        static List<string> Missing(IEnumerable<string> ids, string dir) =>
            (from lang in Languages
             from id in ids
             where !File.Exists(Path.Combine(dir, lang, id + ".mp3"))
             select lang + "/" + id).ToList();

        static void AssertNoneMissing(List<string> missing, int total) =>
            Assert.IsEmpty(missing, $"{missing.Count} of {total * Languages.Length} clips missing, e.g. " +
                                    string.Join(", ", missing.Take(8)));

        [Test]
        public void Every_mentor_line_has_voice_in_en_and_ru()
        {
            var ids = SpokenLines().Where(l => l.speaker == "Unpaid CEO").Select(l => l.id).ToList();
            Assert.That(ids.Count, Is.GreaterThanOrEqualTo(30), "mentor lines not found");
            AssertNoneMissing(Missing(ids, YarnVoiceDir), ids.Count);
        }

        [Test]
        public void Every_spoken_line_has_voice_in_en_and_ru()
        {
            var ids = SpokenLines().Select(l => l.id).ToList();
            Assert.That(ids.Count, Is.GreaterThanOrEqualTo(150), "spoken lines not found");
            AssertNoneMissing(Missing(ids, YarnVoiceDir), ids.Count);
        }

        [Test]
        public void Every_lesson_body_has_voice_in_en_and_ru()
        {
            var ids = LessonIds();
            Assert.That(ids.Count, Is.EqualTo(5), "chapter 1 has five lessons");
            AssertNoneMissing(Missing(ids, LessonVoiceDir), ids.Count);
        }

        [Test]
        public void Voice_folders_hold_no_orphan_clips()
        {
            var spoken = new HashSet<string>(SpokenLines().Select(l => l.id));
            var orphans = (from lang in Languages
                           let dir = Path.Combine(YarnVoiceDir, lang)
                           where Directory.Exists(dir)
                           from f in Directory.GetFiles(dir, "*.mp3")
                           where !spoken.Contains(Path.GetFileNameWithoutExtension(f))
                           select lang + "/" + Path.GetFileName(f)).ToList();
            Assert.IsEmpty(orphans, "clips without a spoken line (renamed id or voiced option?): " + string.Join(", ", orphans.Take(8)));
        }
    }
}
