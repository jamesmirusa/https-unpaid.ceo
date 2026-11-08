using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Persistence;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Core.Tests.Game;

namespace UnpaidCeo.Core.Tests.Persistence
{
    public class SaveCodecTests
    {
        static GameState Played()
        {
            var e = new GameEngine(TestContent.Db(capacityPerWorkPoint: 3.0));
            var s = e.NewGame(77, TestContent.Hero);
            e.TutorialGig(s);
            for (int m = 0; m < 4; m++)
            {
                foreach (var st in e.BeginMonth(s).ToList())
                    e.ChooseStoryletOption(s, st.Id, st.Choices.Keys.OrderBy(k => k).First());
                e.EndMonth(s, TestContent.Decisions(55, 15, 20, 10, "network"));
            }
            return s;
        }

        [Test]
        public void Roundtrip_preserves_hash()
        {
            var s = Played();
            var r = SaveCodec.Decode(SaveCodec.Encode(s));
            Assert.AreEqual(LoadStatus.Ok, r.Status);
            Assert.AreEqual(StateHash.Compute(s), StateHash.Compute(r.State));
        }

        [Test]
        public void Garbage_returns_CorruptReset_with_backup()
        {
            var r = SaveCodec.Decode("{not json");
            Assert.AreEqual(LoadStatus.CorruptReset, r.Status);
            Assert.AreEqual("{not json", r.Backup);
            Assert.IsNull(r.State);
            Assert.AreEqual(LoadStatus.CorruptReset, SaveCodec.Decode("").Status);
            Assert.AreEqual(LoadStatus.CorruptReset, SaveCodec.Decode("[1,2,3]").Status);
        }

        [Test]
        public void Future_version_is_not_loaded()
        {
            var j = JObject.Parse(SaveCodec.Encode(Played()));
            j["Version"] = 99;
            var r = SaveCodec.Decode(j.ToString());
            Assert.AreEqual(LoadStatus.CorruptReset, r.Status);
            Assert.IsNull(r.State);
        }

        [Test]
        public void Wrong_typed_field_is_CorruptReset_not_exception()
        {
            var j = JObject.Parse(SaveCodec.Encode(Played()));
            j["Month"] = "not a number";
            Assert.AreEqual(LoadStatus.CorruptReset, SaveCodec.Decode(j.ToString()).Status);
        }

        [Test]
        public void Missing_optional_fields_migrate()
        {
            var j = JObject.Parse(SaveCodec.Encode(Played()));
            j.Remove("Journal");
            var r = SaveCodec.Decode(j.ToString());
            Assert.AreEqual(LoadStatus.Migrated, r.Status);
            Assert.IsNotNull(r.State.Journal);
            Assert.AreEqual(0, r.State.Journal.Count);
        }

        [Test]
        public void Save_from_before_skills_migrates_with_empty_skills()
        {
            var j = JObject.Parse(SaveCodec.Encode(Played()));
            j.Remove("Skills");
            j.Remove("QuizAnswers");
            var r = SaveCodec.Decode(j.ToString());
            Assert.AreEqual(LoadStatus.Migrated, r.Status);
            Assert.IsNotNull(r.State.Skills);
            Assert.IsNotNull(r.State.QuizAnswers);
        }
    }
}
