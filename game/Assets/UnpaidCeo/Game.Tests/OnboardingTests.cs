using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Game.Bootstrap;
using UnpaidCeo.Game.Hero;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>The first 60 seconds: hero → mentor → $40 gig → "revenue is not profit" → Next month → report.</summary>
    public class OnboardingTests
    {
        GameObject _host;
        RenderTexture _target;

        static ContentDb Content()
        {
            string dir = Path.Combine(Application.dataPath, "UnpaidCeo/Content");
            string Read(string f) { var p = Path.Combine(dir, f); return File.Exists(p) ? File.ReadAllText(p) : null; }
            return ContentDb.FromJson(new ContentFiles
            {
                Industries = Read("industries.json"), Balance = Read("balance.json"), FreeActions = Read("free_actions.json"),
                Storylets = Read("storylets_ch1.json"), Npcs = Read("npcs.json"), Lessons = Read("lessons.json"),
            });
        }

        static bool Shown(VisualElement e)
        {
            for (var v = e; v != null; v = v.parent)
                if (v.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        /// <summary>A real tap as the panel sees it (Button handles submit like a click).</summary>
        static void Tap(Button b)
        {
            Assert.IsNotNull(b, "button missing");
            Assert.IsTrue(b.enabledInHierarchy && Shown(b), b.name + " must be visible and enabled");
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = b;
                b.SendEvent(e);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            if (_target != null) Object.Destroy(_target);
        }

        [UnityTest]
        public IEnumerator New_player_reaches_month1_report_in_at_most_6_taps()
        {
            yield return LocalizationSettings.InitializationOperation;
            _target = new RenderTexture(1080, 1920, 24);
            _host = new GameObject("game");
            var ui = _host.AddComponent<UiRoot>();
            var engine = new GameEngine(Content());
            var state = engine.NewGame(11, new HeroAppearance());
            ui.Initialize(engine, state, _target);
            var hero = new GameObject("hero");
            hero.transform.SetParent(_host.transform);
            var view = hero.AddComponent<HeroView>();

            var lines = new List<string>();
            var flow = new OnboardingFlow(engine, state, ui, view, id => "text of " + id, seed: 11);
            flow.LineShown += lines.Add;
            flow.Start();
            for (int i = 0; i < 3; i++) yield return null;

            int taps = 0;
            Tap(ui.Root.Q<Button>("hero-done")); taps++;
            yield return null;
            Assert.AreEqual(OnboardingFlow.Step.Gig, flow.Current, "mentor offers the gig right after the hero");
            Assert.AreEqual(DisplayStyle.None, ui.Guide.resolvedStyle.display, "one voice at a time: the guide waits for the mentor");
            Assert.IsFalse(ui.NextMonthButton.enabledSelf, "Next month is off until the mentor asks for it: a button that does nothing must not look alive");

            Tap(ui.Root.Q<Button>(OnboardingFlow.ActionButton)); taps++;
            yield return null;
            Assert.AreEqual(4000L, state.Cash.Cents, "the delivery gig pays $40");
            Assert.AreEqual(OnboardingFlow.Step.Lesson, flow.Current);

            Tap(ui.Root.Q<Button>(OnboardingFlow.ActionButton)); taps++;
            yield return null;
            var next = ui.Root.Q<Button>("next-month");
            Assert.AreEqual(OnboardingFlow.Step.NextMonth, flow.Current);
            Assert.IsTrue(next.ClassListContains(OnboardingFlow.HighlightClass), "Next month is highlighted");
            Assert.IsTrue(next.enabledSelf, "and can be pressed now");

            Tap(next); taps++;
            yield return null;
            Assert.AreEqual(OnboardingFlow.Step.Done, flow.Current);
            Assert.IsNotNull(flow.Report);
            Assert.AreEqual(1, flow.Report.Month);
            Assert.AreEqual(UiRoot.City, ui.CurrentTab, "the player stays on the main screen");
            Assert.IsTrue(ui.MonthSummaryOpen, "the month 1 result pops up");
            Assert.AreEqual(DisplayStyle.Flex, ui.Guide.resolvedStyle.display, "onboarding is over: the guide takes it from here");
            Assert.IsFalse(next.ClassListContains(OnboardingFlow.HighlightClass));
            Assert.LessOrEqual(taps, 6);
            CollectionAssert.AreEqual(new[] { "line:ch1_onboarding_01", "line:ch1_onboarding_02", "line:ch1_onboarding_03" }, lines);
        }
    }
}
