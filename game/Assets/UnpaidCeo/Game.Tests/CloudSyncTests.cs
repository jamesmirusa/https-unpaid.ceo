#nullable enable
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Services.Core;
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
            return (f, new CloudSync(f.Saves, () => _now, () => f.Auth.PlayerId));
        }

        static RunSubmission Challenge(string hash) =>
            new RunSubmission { Start = new RunStart { Kind = RunStart.Challenge, ContextId = "2950" }, StateHash = hash };

        [UnityTest]
        public IEnumerator Writes_stop_when_the_player_changes() => Run(async () =>
        {
            var f = new FakeOnlineServices();
            await f.SignInAnonymously();
            await f.SignUp("alice", "Secret#123");
            string alice = f.Auth.PlayerId!;
            f.PutCloudSave(alice, Save(40, 500));   // alice's long game
            await f.SignOut();                       // a new guest plays on this device
            var sync = new CloudSync(f.Saves, () => _now, () => f.Auth.PlayerId);
            Assert.IsNull(await sync.Pull(Save(3, _now)));

            await f.SignIn("alice", "Secret#123");   // e.g. from Settings during a run: no new pull yet
            _now += CloudSync.DebounceSeconds;
            Assert.IsFalse(await sync.Push(Save(4, _now)), "the guest's game is not written into alice's cloud");
            Assert.IsFalse(await sync.FlushPending());
            Assert.AreEqual(40, f.CloudSaveOf(alice)!.Months);
        });

        [UnityTest]
        public IEnumerator After_the_cloud_wins_nothing_is_written_until_a_new_pull() => Run(async () =>
        {
            var (f, sync) = Online();
            f.PutCloudSave(f.Auth.PlayerId!, Save(39, 500));
            Assert.IsNotNull(await sync.Pull(Save(2, 900)), "the device is about to restart on the cloud game");

            _now += CloudSync.DebounceSeconds;
            Assert.IsFalse(await sync.Push(Save(3, _now)), "a month finished before the restart (or a failed adopt) is not pushed");
            Assert.IsFalse(await sync.FlushPending());
            Assert.AreEqual(39, f.CloudSaveOf(f.Auth.PlayerId!)!.Months);
        });

        [UnityTest]
        public IEnumerator A_push_never_replaces_a_longer_cloud_game() => Run(async () =>
        {
            var (f, sync) = Online();
            Assert.IsNull(await sync.Pull(Save(5, _now)));
            f.PutCloudSave(f.Auth.PlayerId!, Save(30, _now + 1));   // another device played on meanwhile

            _now += CloudSync.DebounceSeconds;
            Assert.IsFalse(await sync.Push(Save(6, _now)));
            Assert.AreEqual(30, f.CloudSaveOf(f.Auth.PlayerId!)!.Months);
        });

        [UnityTest]
        public IEnumerator A_failing_run_is_retried_without_blocking_the_others() => Run(async () =>
        {
            var f = new FakeOnlineServices { ChallengeResult = _ => new SubmitResult { Ok = false, Error = OnlineErrors.ServerError } };
            await f.SignInAnonymously();
            string me = f.Auth.PlayerId!;
            var q = new SubmissionQueue(QueueKey);
            q.EnqueueChallenge(Challenge("c"), me);
            q.EnqueueCareer(Career("k"), me);

            await q.Flush(f.Runs, me);
            Assert.AreEqual(1, f.Careers.Count, "the career behind the failing run is sent");
            Assert.AreEqual("c", q.Entries.Single().Submission!.StateHash, "the failing run waits for another try");

            for (int i = 1; i < SubmissionQueue.MaxFailures; i++) await q.Flush(f.Runs, me);
            Assert.AreEqual(SubmissionQueue.MaxFailures, f.Challenges.Count);
            Assert.IsEmpty(q.Entries, "…and is given up after the last try");
        });

        [UnityTest]
        public IEnumerator Entries_wait_for_the_player_who_played_them() => Run(async () =>
        {
            var f = new FakeOnlineServices();
            await f.SignInAnonymously();
            string first = f.Auth.PlayerId!;
            var q = new SubmissionQueue(QueueKey);
            q.EnqueueChallenge(Challenge("mine"), first);
            await f.SignOut();
            string second = f.Auth.PlayerId!;

            await q.Flush(f.Runs, second);
            Assert.IsEmpty(f.Challenges, "a run is never sent under another player's account");
            q.EnqueueChallenge(Challenge("theirs"), second);
            await q.Flush(f.Runs, second);
            Assert.AreEqual("theirs", f.Challenges.Single().StateHash);
            Assert.AreEqual("mine", q.Entries.Single().Submission!.StateHash, "kept for its player");
        });

        [Test]
        public void Server_failures_are_told_apart_from_being_offline()
        {
            Assert.AreEqual(OnlineErrors.Offline, UgsOnlineServices.ErrorFor(new RequestFailedException(CommonErrorCodes.TransportError, "no network")));
            Assert.AreEqual(OnlineErrors.Offline, UgsOnlineServices.ErrorFor(new RequestFailedException(CommonErrorCodes.Timeout, "slow")));
            Assert.AreEqual(OnlineErrors.Offline, UgsOnlineServices.ErrorFor(new RequestFailedException(CommonErrorCodes.ServiceUnavailable, "down")));
            Assert.AreEqual(OnlineErrors.Offline, UgsOnlineServices.ErrorFor(new RequestFailedException(CommonErrorCodes.TooManyRequests, "later")));
            Assert.AreEqual(OnlineErrors.Offline, UgsOnlineServices.ErrorFor(new System.InvalidOperationException("not initialized")));
            Assert.AreEqual(OnlineErrors.ServerError, UgsOnlineServices.ErrorFor(new RequestFailedException(9009, "module threw")));
            Assert.AreEqual(OnlineErrors.ServerError, UgsOnlineServices.ErrorFor(new RequestFailedException(CommonErrorCodes.InvalidRequest, "bad")));
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
            q.EnqueueCareer(Career("h5"), null);

            await q.Flush(f.Runs, null);
            Assert.AreEqual(1, q.Entries.Count, "kept while offline");
            Assert.AreEqual(0, f.Careers.Count);

            f.Online = true;
            await q.Flush(f.Runs, "p1");
            Assert.AreEqual(1, f.Careers.Count, "played before anyone signed in: sent by the player who comes online");
            Assert.IsEmpty(q.Entries);
            await q.Flush(f.Runs, "p1");
            Assert.AreEqual(1, f.Careers.Count, "flushed once");
            Assert.IsEmpty(new SubmissionQueue(QueueKey).Entries, "the emptied queue was saved");
        });

        [Test]
        public void Only_the_latest_career_submission_is_kept()
        {
            var q = new SubmissionQueue(QueueKey);
            q.EnqueueCareer(Career("a"), "p1");
            q.EnqueueChallenge(Challenge("c"), "p1");
            q.EnqueueCareer(Career("b"), "p1");

            foreach (var queue in new[] { q, new SubmissionQueue(QueueKey) })
            {
                Assert.AreEqual(2, queue.Entries.Count);
                Assert.AreEqual("b", queue.Entries.Single(e => e.Kind == RunStart.Career).Submission!.StateHash);
                Assert.AreEqual("c", queue.Entries.Single(e => e.Kind == RunStart.Challenge).Submission!.StateHash);
            }
        }
    }
}
