#nullable enable
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Game.Services.Online;
using UnpaidCeo.Game.Tests.Fakes;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>The online seam (stage 2B, Task 6): offline never throws, an unlinked project starts offline, the fake works.</summary>
    public class OnlineServicesTests
    {
        /// <summary>Runs an async test body on the main thread, failing on timeout or exception.</summary>
        public static IEnumerator Run(System.Func<Task> body, float timeoutSeconds = 10f)
        {
            var t = body();
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!t.IsCompleted && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(t.IsCompleted, $"did not finish within {timeoutSeconds}s");
            if (t.IsFaulted) throw t.Exception!.GetBaseException();
        }

        [UnityTest]
        public IEnumerator Offline_services_never_throw_and_report_unavailable() => Run(async () =>
        {
            IOnlineServices o = new OfflineServices();
            Assert.IsFalse(o.Available);
            Assert.IsFalse(o.Flags.Online);
            Assert.IsFalse(o.Auth.SignedIn);
            Assert.IsNull(o.Auth.PlayerId);

            Assert.AreEqual(AuthResult.Offline, await o.Auth.SignInAnonymously());
            Assert.AreEqual(AuthResult.Offline, await o.Auth.SignUp("ceo_one", "Str0ng!pass"));
            Assert.AreEqual(AuthResult.Offline, await o.Auth.SignIn("ceo_one", "Str0ng!pass"));
            Assert.AreEqual(AuthResult.Offline, await o.Auth.SetName("CEO"));
            await o.Auth.SignOut();

            var load = await o.Saves.Load();
            Assert.IsFalse(load.Reachable, "offline is not the same as an empty cloud");
            Assert.IsNull(load.Envelope);
            Assert.IsFalse(await o.Saves.Save(new CloudSaveEnvelope { SaveJson = "{}" }));

            Assert.IsNull(await o.Runs.Hello());
            var career = await o.Runs.SubmitCareer(new RunSubmission());
            Assert.IsFalse(career.Ok);
            Assert.AreEqual(OnlineErrors.Offline, career.Error);
            Assert.AreEqual(OnlineErrors.Offline, (await o.Runs.SubmitChallenge(new RunSubmission())).Error);
            Assert.AreEqual(OnlineErrors.Offline, (await o.Runs.CreateDuel("p2", "coffee_cart")).Error);
            Assert.AreEqual(OnlineErrors.Offline, (await o.Runs.SubmitDuel("d1", new RunSubmission())).Error);
            Assert.IsEmpty(await o.Runs.ListDuels());

            Assert.IsEmpty(await o.Boards.Top("capital", 10));
            Assert.IsNull(await o.Boards.Mine("capital"));

            Assert.IsEmpty(await o.Friends.List());
            Assert.IsEmpty(await o.Friends.Requests());
            Assert.IsFalse(await o.Friends.AddByName("Rival#1234"));
            Assert.IsFalse(await o.Friends.Accept("p2"));
            Assert.IsFalse(await o.Friends.Remove("p2"));

            o.Analytics.Record("month_completed");
            o.Analytics.SetConsent(true);
            Assert.IsTrue(o.Analytics.Consented);
        });

        [UnityTest]
        public IEnumerator Unlinked_project_starts_offline() => Run(async () =>
        {
            float started = Time.realtimeSinceStartup;
            var online = await OnlineBootstrap.Start(timeoutSeconds: 2f, cloudProjectId: "");
            Assert.IsFalse(online.Available);
            Assert.Less(Time.realtimeSinceStartup - started, 2.5f, "an unlinked project does not wait for the timeout");
        }, 5f);

        [Test]
        public void Account_rules_follow_the_username_and_password_service()
        {
            Assert.IsTrue(AccountRules.UsernameOk("ceo_one"));
            Assert.IsTrue(AccountRules.UsernameOk("a.b-c@d"));
            Assert.IsFalse(AccountRules.UsernameOk("ab"), "3–20 characters");
            Assert.IsFalse(AccountRules.UsernameOk(new string('a', 21)));
            Assert.IsFalse(AccountRules.UsernameOk("no spaces"));
            Assert.IsTrue(AccountRules.PasswordOk("Str0ng!pass"));
            Assert.IsFalse(AccountRules.PasswordOk("Sh0rt!"), "8–30 characters");
            Assert.IsFalse(AccountRules.PasswordOk("nouppercase1!"));
            Assert.IsFalse(AccountRules.PasswordOk("NOLOWERCASE1!"));
            Assert.IsFalse(AccountRules.PasswordOk("NoDigits!!"));
            Assert.IsFalse(AccountRules.PasswordOk("NoSymbol123"));
        }

        [UnityTest]
        public IEnumerator Fake_services_round_trip() => Run(async () =>
        {
            var f = new FakeOnlineServices();
            Assert.AreEqual(AuthResult.Ok, await f.Auth.SignInAnonymously());
            var guest = f.Auth.PlayerId!;
            Assert.IsTrue(f.Auth.IsGuest);
            Assert.AreEqual(AuthResult.WeakPassword, await f.Auth.SignUp("ceo_one", "weakpass"));
            Assert.AreEqual(AuthResult.Ok, await f.Auth.SignUp("ceo_one", "Str0ng!pass"));
            Assert.AreEqual(guest, f.Auth.PlayerId, "the guest keeps its player id");
            Assert.IsFalse(f.Auth.IsGuest);

            var empty = await f.Saves.Load();
            Assert.IsTrue(empty.Reachable);
            Assert.IsNull(empty.Envelope, "a new account has no cloud save yet");
            Assert.IsTrue(await f.Saves.Save(new CloudSaveEnvelope { SaveJson = "{}", Months = 7, UpdatedUnix = 5 }));
            Assert.AreEqual(7, (await f.Saves.Load()).Envelope!.Months);

            f.SetScore("capital", "other", 50_000, "hustler");
            f.SetScore("capital", guest, 120_000, "boss");
            var top = await f.Boards.Top("capital", 10);
            CollectionAssert.AreEqual(new[] { guest, "other" }, top.Select(e => e.PlayerId).ToArray());
            Assert.AreEqual(1, (await f.Boards.Mine("capital"))!.Rank);

            f.AddPlayer("rival-id", "Rival#1234");
            Assert.IsTrue(await f.Friends.AddByName("Rival#1234"));
            Assert.AreEqual("rival-id", (await f.Friends.List()).Single().PlayerId);

            var duel = await f.Runs.CreateDuel("rival-id", "coffee_cart");
            Assert.AreEqual(DuelView.WaitingMe, duel.State);
            f.DuelNetWorth = _ => 1_234_500;
            var after = await f.Runs.SubmitDuel(duel.Id, new RunSubmission());
            Assert.AreEqual(DuelView.WaitingThem, after.State);
            Assert.AreEqual(1_234_500, after.MyNetWorthCents);
            Assert.AreEqual(1, (await f.Runs.ListDuels()).Length);

            f.Online = false;
            Assert.IsFalse((await f.Saves.Load()).Reachable);
            Assert.AreEqual(OnlineErrors.Offline, (await f.Runs.SubmitCareer(new RunSubmission())).Error);
            f.Online = true;

            await f.Auth.SignOut();
            Assert.IsTrue(f.Auth.IsGuest);
            Assert.AreNotEqual(guest, f.Auth.PlayerId, "signing out continues as a new guest");
            Assert.AreEqual(AuthResult.InvalidCredentials, await f.Auth.SignIn("ceo_one", "Wr0ng!pass"));
            Assert.AreEqual(AuthResult.Ok, await f.Auth.SignIn("ceo_one", "Str0ng!pass"));
            Assert.AreEqual(guest, f.Auth.PlayerId);
            Assert.AreEqual(7, (await f.Saves.Load()).Envelope!.Months, "the account's cloud save is still there");
        });
    }
}
