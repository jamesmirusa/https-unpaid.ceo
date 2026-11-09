#nullable enable
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Game.Bootstrap;
using UnpaidCeo.Game.Hero;
using UnpaidCeo.Game.Services.Online;
using UnpaidCeo.Game.Tests.Fakes;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Analytics events and consent (stage 2B, Task 12).</summary>
    public class AnalyticsTests
    {
        GameObject? _host;
        RenderTexture? _target;
        UiRoot _ui = null!;
        GameEngine _engine = null!;
        GameState _state = null!;
        string? _consent;

        [SetUp]
        public void Keep()
        {
            _consent = PlayerPrefs.HasKey(AnalyticsConsent.Key) ? PlayerPrefs.GetInt(AnalyticsConsent.Key).ToString() : null;
            PlayerPrefs.DeleteKey(AnalyticsConsent.Key);
        }

        [TearDown]
        public void Restore()
        {
            if (_host != null) Object.Destroy(_host);
            if (_target != null) Object.Destroy(_target);
            if (_consent == null) PlayerPrefs.DeleteKey(AnalyticsConsent.Key); else PlayerPrefs.SetInt(AnalyticsConsent.Key, int.Parse(_consent));
        }

        static string[] Texts()
        {
            string dir = Path.Combine(Application.dataPath, "UnpaidCeo/Content");
            return ContentPack.Files.Select(f => File.ReadAllText(Path.Combine(dir, f + ".json"))).ToArray();
        }

        static void Tap(Button b)
        {
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        IEnumerator Boot(IOnlineServices online)
        {
            yield return LocalizationSettings.InitializationOperation;
            _target = new RenderTexture(1080, 1920, 24);
            _host = new GameObject("game");
            _ui = _host.AddComponent<UiRoot>();
            _engine = new GameEngine(ContentPack.Load(Texts()));
            _state = _engine.NewGame(11, new HeroAppearance());
            _ui.Initialize(_engine, _state, _target);
            _ui.Online = online;
            yield return _ui.SetLanguage("en");
        }

        /// <summary>Onboarding to the month-1 report, tapped as a player would.</summary>
        IEnumerator PlayOnboarding()
        {
            var hero = new GameObject("hero");
            hero.transform.SetParent(_host!.transform);
            var flow = new OnboardingFlow(_engine, _state, _ui, hero.AddComponent<HeroView>(), id => id, seed: 11);
            flow.Start();
            for (int i = 0; i < 3; i++) yield return null;
            Tap(_ui.Root.Q<Button>("hero-done"));
            yield return null;
            Tap(_ui.Root.Q<Button>(OnboardingFlow.ActionButton));
            yield return null;
            Tap(_ui.Root.Q<Button>(OnboardingFlow.ActionButton));
            yield return null;
            Tap(_ui.Root.Q<Button>("next-month"));
            yield return null;
            Assert.AreEqual(OnboardingFlow.Step.Done, flow.Current);
        }

        [UnityTest]
        public IEnumerator Nothing_is_recorded_before_consent()
        {
            var fake = new FakeOnlineServices();
            yield return Boot(fake);
            yield return PlayOnboarding();
            AnalyticsEvents.MonthCompleted(fake, 2, 1);
            Assert.IsEmpty(fake.Events);
            Assert.IsFalse(AnalyticsConsent.Asked);
        }

        [UnityTest]
        public IEnumerator Each_event_fires_once_with_its_arguments()
        {
            var fake = new FakeOnlineServices();
            fake.SetConsent(true);
            yield return Boot(fake);
            yield return PlayOnboarding();

            var steps = fake.Events.Where(e => e.Name == AnalyticsEvents.TutorialStepEvent).Select(e => (int)e.Args!["step"]).ToArray();
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, steps, "one tutorial_step per onboarding step");
            var months = fake.Events.Where(e => e.Name == AnalyticsEvents.MonthCompletedEvent).ToList();
            Assert.AreEqual(1, months.Count, "month_completed once");
            Assert.AreEqual(1, months[0].Args!["month"]);
            Assert.AreEqual(1, months[0].Args!["chapter"]);
        }

        [UnityTest]
        public IEnumerator Consent_can_be_withdrawn_in_settings()
        {
            var fake = new FakeOnlineServices();
            yield return Boot(fake);
            _ui.Open(UiRoot.Settings);
            _ui.AskConsent();
            Assert.AreEqual(DisplayStyle.Flex, _ui.Consent.Root.style.display.value, "asked once after onboarding");
            _ui.Consent.Choose(true);
            Assert.IsTrue(fake.Consented);
            Assert.IsTrue(AnalyticsConsent.Asked && AnalyticsConsent.Granted);
            Assert.AreEqual(DisplayStyle.None, _ui.Consent.Root.style.display.value);
            _ui.AskConsent();
            Assert.AreEqual(DisplayStyle.None, _ui.Consent.Root.style.display.value, "never asked twice");

            _ui.SettingsPanel.SetAnalytics(false);
            Assert.IsFalse(fake.Consented);
            Assert.IsFalse(AnalyticsConsent.Granted);
            AnalyticsEvents.MonthCompleted(fake, 3, 1);
            Assert.IsEmpty(fake.Events);
        }

        [Test]
        public void All_spec_events_are_declared()
        {
            CollectionAssert.AreEquivalent(new[]
            {
                "tutorial_step", "month_completed", "chapter_unlocked", "industry_chosen", "business_opened", "bankrupt",
                "lesson_viewed", "quiz_passed", "signup", "purchase", "ad_rewarded", "graphics_mode", "language",
            }, AnalyticsEvents.All);
        }
    }
}
