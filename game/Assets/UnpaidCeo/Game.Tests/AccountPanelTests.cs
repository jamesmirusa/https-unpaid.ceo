#nullable enable
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Game.Services;
using UnpaidCeo.Game.Services.Online;
using UnpaidCeo.Game.Tests.Fakes;
using UnpaidCeo.Game.Ui;
using static UnpaidCeo.Game.Tests.OnlineServicesTests;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Settings → Account (stage 2B, Task 8).</summary>
    public class AccountPanelTests
    {
        const string Strong = "Str0ng!pass";
        GameObject? _host;
        RenderTexture? _target;
        UiRoot _ui = null!;
        GameState _state = null!;
        string? _savedSave;

        [SetUp]
        public void KeepDeveloperSave() { _savedSave = PlayerPrefs.GetString(SaveService.Key, null); SaveService.Delete(); }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            if (_target != null) Object.Destroy(_target);
            SaveService.Delete();
            if (!string.IsNullOrEmpty(_savedSave)) PlayerPrefs.SetString(SaveService.Key, _savedSave);
        }

        static ContentDb Content()
        {
            string dir = Path.Combine(Application.dataPath, "UnpaidCeo/Content");
            string Read(string f) => File.ReadAllText(Path.Combine(dir, f));
            return ContentDb.FromJson(new ContentFiles
            {
                Industries = Read("industries.json"), Balance = Read("balance.json"), FreeActions = Read("free_actions.json"),
                Storylets = Read("storylets_ch1.json"), Npcs = Read("npcs.json"), Lessons = Read("lessons.json"), Missions = Read("missions.json"),
            });
        }

        IEnumerator Boot(IOnlineServices online)
        {
            yield return LocalizationSettings.InitializationOperation;
            _target = new RenderTexture(1080, 1920, 24);
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            var engine = new GameEngine(Content());
            _state = engine.NewGame(9, new HeroAppearance());
            _ui.Initialize(engine, _state, _target);
            _ui.Online = online;
            yield return _ui.SetLanguage("en");
            _ui.Open(UiRoot.Settings);
            for (int i = 0; i < 3; i++) yield return null;
        }

        static FakeOnlineServices Guest()
        {
            var f = new FakeOnlineServices();
            f.SignInAnonymously().Wait();
            return f;
        }

        void AssertMessage(string key)
        {
            Assert.AreEqual(key, _ui.Account.MessageKey);
            Assert.IsTrue(UiText.IsLocalized(_ui.Account.Message), "the message comes from the string table");
        }

        [UnityTest]
        public IEnumerator Signing_up_keeps_the_guest_progress()
        {
            var fake = Guest();
            var guest = fake.Auth.PlayerId!;
            yield return Boot(fake);
            AccountChange? change = null;
            _ui.AccountChanged += c => change = c;
            yield return Run(async () =>
            {
                var sync = new CloudSync(fake.Saves, () => 1_000, () => fake.Auth.PlayerId);
                await sync.Pull(new CloudSaveEnvelope { SaveJson = "{}", Months = 3, UpdatedUnix = 1_000 });   // the guest's 3 months are in the cloud

                _ui.Account.User.value = "ceo_one";
                _ui.Account.Password.value = Strong;
                await _ui.Account.SignUp();

                AssertMessage("account.created");
                Assert.AreEqual(guest, fake.Auth.PlayerId, "same player id");
                Assert.AreEqual(3, fake.CloudSaveOf(guest)!.Months, "the cloud save still holds the guest's months");
                Assert.AreEqual(AccountChange.SignedUp, change);
                Assert.IsFalse(_ui.Account.IsGuestFormShown, "an account sees its name and Sign out");
                Assert.AreEqual("", _ui.Account.Password.value, "the password is not kept on screen");
            });
        }

        [UnityTest]
        public IEnumerator Weak_password_is_explained_before_sending()
        {
            var fake = Guest();
            yield return Boot(fake);
            yield return Run(async () =>
            {
                _ui.Account.User.value = "ceo_one";
                _ui.Account.Password.value = "weakpass";
                await _ui.Account.SignUp();
                AssertMessage("account.weak");
                Assert.AreEqual(0, fake.SignUpCalls, "nothing was sent");

                _ui.Account.User.value = "a b";
                _ui.Account.Password.value = Strong;
                await _ui.Account.SignUp();
                AssertMessage("account.bad_user");
                Assert.AreEqual(0, fake.SignUpCalls);
            });
        }

        [UnityTest]
        public IEnumerator Name_taken_shows_its_message()
        {
            var fake = Guest();
            fake.SignUp("ceo_one", Strong).Wait();
            fake.SignOut().Wait();   // a new guest on this device
            yield return Boot(fake);
            yield return Run(async () =>
            {
                _ui.Account.User.value = "ceo_one";
                _ui.Account.Password.value = Strong;
                await _ui.Account.SignUp();
                AssertMessage("account.taken");
                Assert.IsTrue(fake.Auth.IsGuest);

                _ui.Account.Password.value = "Wr0ng!pass";
                await _ui.Account.SignIn();
                AssertMessage("account.invalid");
            });
        }

        [UnityTest]
        public IEnumerator Signing_out_returns_to_a_new_guest_and_keeps_the_device_save()
        {
            var fake = Guest();
            fake.SignUp("ceo_one", Strong).Wait();
            var account = fake.Auth.PlayerId;
            yield return Boot(fake);
            _state.Month = 5;
            SaveService.Save(_state);
            AccountChange? change = null;
            _ui.AccountChanged += c => change = c;
            yield return Run(async () =>
            {
                await _ui.Account.SignOut();
                AssertMessage("account.signed_out");
                Assert.IsTrue(fake.Auth.IsGuest);
                Assert.AreNotEqual(account, fake.Auth.PlayerId);
                Assert.AreEqual(5, SaveService.Load()!.State!.Month, "the device save is untouched");
                Assert.AreEqual(AccountChange.SignedOut, change);
                Assert.IsTrue(_ui.Account.IsGuestFormShown);
            });
        }

        [UnityTest]
        public IEnumerator Offline_account_panel_says_so()
        {
            yield return Boot(new OfflineServices());
            AssertMessage("account.offline");
            Assert.IsFalse(_ui.Account.SignUpButton.enabledSelf);
            Assert.IsFalse(_ui.Account.SignInButton.enabledSelf);
            Assert.IsFalse(string.IsNullOrEmpty(_ui.Account.Message.text));
        }
    }
}
