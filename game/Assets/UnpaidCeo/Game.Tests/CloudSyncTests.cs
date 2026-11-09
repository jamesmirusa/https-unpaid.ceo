#nullable enable
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Game.Services.Online;
using UnpaidCeo.Game.Tests.Fakes;
using static UnpaidCeo.Game.Tests.OnlineServicesTests;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Cloud save sync and the submission queue (stage 2B, Task 7).</summary>
    public class CloudSyncTests
    {
        const string QueueKey = "unpaidceo.queue.test";
        long _now = 1_000;

        [SetUp]
        public void Clean() { PlayerPrefs.DeleteKey(QueueKey); PlayerPrefs.DeleteKey(CloudSync.ResetKey); }

        [TearDown]
        public void Restore() { PlayerPrefs.DeleteKey(QueueKey); PlayerPrefs.DeleteKey(CloudSync.ResetKey); }

        static CloudSaveEnvelope Save(int months, long updated) =>
            new CloudSaveEnvelope { SaveJson = "{\"month\":" + (months + 1) + "}", MetaJson = "{}", Months = months, UpdatedUnix = updated };

        static RunSubmission Career(string hash) => new RunSubmission { Start = new RunStart { Kind = RunStart.Career, Seed = 1 }, StateHash = hash };

        (FakeOnlineServices Fake, CloudSync Sync) Online()
        {
            var f = new FakeOnlineServices();
            f.SignInAnonymously().Wait();
            return (f, new CloudSync(f.Saves, () => _now));
        }

        [UnityTest]
        public IEnumerator Fresh_install_does_not_overwrite_cloud_progress() => Run(async () =>
        {
            var (f, sync) = Online();
            f.PutCloudSave(f.Auth.PlayerId!, Save(39, 500));
            var fresh = Save(0, 900);

            Assert.AreEqual(SyncChoice.UseCloud, CloudSync.Choose(fresh, Save(39, 500)));
            Assert.IsFalse(await sync.Push(fresh), "nothing is pushed before the cloud has been read");
            Assert.AreEqual(39, f.CloudSaveOf(f.Auth.PlayerId!)!.Months);

            var adopt = await sync.Pull(fresh);
            Assert.IsNotNull(adopt, "the longer cloud game replaces the fresh install");
            Assert.AreEqual(39, adopt!.Months);
            Assert.AreEqual(39, f.CloudSaveOf(f.Auth.PlayerId!)!.Months);
        });

        [Test]
        public void Newer_but_shorter_save_loses()
        {
            Assert.AreEqual(SyncChoice.UseCloud, CloudSync.Choose(Save(10, 2_000), Save(20, 1_000)));
            Assert.AreEqual(SyncChoice.UseLocal, CloudSync.Choose(Save(20, 1_000), Save(10, 2_000)));
            Assert.AreEqual(SyncChoice.UseLocal, CloudSync.Choose(Save(12, 2_000), Save(12, 1_000)), "same length: newer wins");
            Assert.AreEqual(SyncChoice.Equal, CloudSync.Choose(Save(12, 1_000), Save(12, 1_000)));
            Assert.AreEqual(SyncChoice.UseLocal, CloudSync.Choose(Save(3, 1_000), null), "an empty cloud is filled from the device");
        }

        [UnityTest]
        public IEnumerator Push_is_debounced() => Run(async () =>
        {
            var (f, sync) = Online();
            Assert.IsNull(await sync.Pull(Save(0, _now)), "an empty cloud keeps the device save");
            Assert.AreEqual(0, f.CloudSaveOf(f.Auth.PlayerId!)!.Months, "…and fills the cloud from it");

            _now += 5;
            Assert.IsFalse(await sync.Push(Save(1, _now)));
            _now += 5;
            Assert.IsFalse(await sync.Push(Save(2, _now)));
            Assert.AreEqual(0, f.CloudSaveOf(f.Auth.PlayerId!)!.Months, "one write per 20 s");

            _now += CloudSync.DebounceSeconds;
            Assert.IsTrue(await sync.Push(Save(3, _now)));
            Assert.AreEqual(3, f.CloudSaveOf(f.Auth.PlayerId!)!.Months);

            _now += 1;
            Assert.IsFalse(await sync.Push(Save(4, _now)));
            Assert.IsTrue(await sync.FlushPending(), "pausing the game writes what is waiting");
            Assert.AreEqual(4, f.CloudSaveOf(f.Auth.PlayerId!)!.Months);
        });

        [UnityTest]
        public IEnumerator A_deliberate_reset_is_not_undone_by_the_cloud() => Run(async () =>
        {
            var (f, sync) = Online();
            f.PutCloudSave(f.Auth.PlayerId!, Save(39, 500));
            CloudSync.MarkReset();

            Assert.IsNull(await sync.Pull(Save(0, 900)), "the new game stays");
            Assert.AreEqual(0, f.CloudSaveOf(f.Auth.PlayerId!)!.Months, "and replaces the old one in the cloud");
            Assert.IsFalse(PlayerPrefs.HasKey(CloudSync.ResetKey), "the reset mark is cleared once written");
        });

        [UnityTest]
        public IEnumerator Queued_submission_is_retried_when_online() => Run(async () =>
        {
            var f = new FakeOnlineServices { Online = false };
            var q = new SubmissionQueue(QueueKey);
            q.EnqueueCareer(Career("h5"));

            await q.Flush(f.Runs);
            Assert.AreEqual(1, q.Entries.Count, "kept while offline");
            Assert.AreEqual(0, f.Careers.Count);

            f.Online = true;
            await q.Flush(f.Runs);
            Assert.AreEqual(1, f.Careers.Count);
            Assert.IsEmpty(q.Entries);
            await q.Flush(f.Runs);
            Assert.AreEqual(1, f.Careers.Count, "flushed once");
            Assert.IsEmpty(new SubmissionQueue(QueueKey).Entries, "the emptied queue was saved");
        });

        [Test]
        public void Only_the_latest_career_submission_is_kept()
        {
            var q = new SubmissionQueue(QueueKey);
            q.EnqueueCareer(Career("a"));
            q.EnqueueChallenge(new RunSubmission { Start = new RunStart { Kind = RunStart.Challenge, ContextId = "2950" }, StateHash = "c" });
            q.EnqueueCareer(Career("b"));

            foreach (var queue in new[] { q, new SubmissionQueue(QueueKey) })
            {
                Assert.AreEqual(2, queue.Entries.Count);
                Assert.AreEqual("b", queue.Entries.Single(e => e.Kind == RunStart.Career).Submission!.StateHash);
                Assert.AreEqual("c", queue.Entries.Single(e => e.Kind == RunStart.Challenge).Submission!.StateHash);
            }
        }
    }
}
