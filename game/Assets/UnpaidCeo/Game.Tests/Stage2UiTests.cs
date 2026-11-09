using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Chapter 2–3 screens: industry choice, loans, units and staff, unit economics, chapter progress (stage 2A, Task 11).</summary>
    public class Stage2UiTests
    {
        GameObject _host;
        UiRoot _ui;
        RenderTexture _target;
        GameEngine _engine;
        GameState _state;

        static ContentDb Content()
        {
            string dir = Path.Combine(Application.dataPath, "UnpaidCeo/Content");
            string Read(string f) { var p = Path.Combine(dir, f); return File.Exists(p) ? File.ReadAllText(p) : null; }
            return ContentDb.FromJson(new ContentFiles
            {
                Industries = Read("industries.json"), Balance = Read("balance.json"), FreeActions = Read("free_actions.json"),
                Storylets = Read("storylets_ch1.json"), Npcs = Read("npcs.json"), Lessons = Read("lessons.json"),
                Missions = Read("missions.json"),
            });
        }

        /// <param name="setup">Changes the state before the UI is built.</param>
        IEnumerator Boot(string language = "en", System.Action<GameState> setup = null)
        {
            yield return LocalizationSettings.InitializationOperation;
            _target = new RenderTexture(1080, 1920, 24);
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            _engine = new GameEngine(Content());
            _state = _engine.NewGame(5, new HeroAppearance());
            setup?.Invoke(_state);
            _ui.Initialize(_engine, _state, _target);
            yield return _ui.SetLanguage(language);
            for (int i = 0; i < 5; i++) yield return null;
        }

        static void Chapter2(GameState s, long cashCents)
        {
            s.Chapter = 2;
            s.Cash = Money.FromCents(cashCents);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            if (_target != null) Object.Destroy(_target);
        }

        [UnityTest]
        public IEnumerator Industry_screen_shows_six_cards_with_capex_and_break_even()
        {
            yield return Boot(setup: s => Chapter2(s, 100_000));
            _ui.Open(UiRoot.Industry);
            for (int i = 0; i < 3; i++) yield return null;
            var cards = _ui.Screen(UiRoot.Industry).Query(className: "industry-card").ToList();
            Assert.AreEqual(6, cards.Count);
            foreach (var card in cards)
            {
                var id = card.name.Substring("card-".Length);
                Assert.IsFalse(string.IsNullOrEmpty(card.Q<Label>("capex-" + id).text), id + " capex");
                Assert.IsFalse(string.IsNullOrEmpty(card.Q<Label>("break-even-" + id).text), id + " break-even");
                Assert.IsFalse(string.IsNullOrEmpty(card.Q<Label>("margin-" + id).text), id + " margin");
                Assert.IsTrue(card.Q<Button>("choose-" + id).enabledSelf, id + " can be chosen");
            }
        }

        [UnityTest]
        public IEnumerator Disclaimer_is_visible()
        {
            yield return Boot("ru", s => Chapter2(s, 100_000));
            _ui.Open(UiRoot.Industry);
            for (int i = 0; i < 3; i++) yield return null;
            var disclaimer = _ui.Screen(UiRoot.Industry).Q<Label>("industry-disclaimer");
            Assert.AreEqual(DisplayStyle.Flex, disclaimer.resolvedStyle.display);
            StringAssert.Contains("финансовый совет", disclaimer.text);
        }

        [UnityTest]
        public IEnumerator Choosing_a_card_adds_the_business()
        {
            yield return Boot(setup: s => Chapter2(s, 1_000_000));
            _ui.Open(UiRoot.Industry);
            yield return null;
            Assert.IsTrue(_ui.IndustryChoice.Choose("cleaning"));
            yield return null;
            Assert.IsTrue(_state.Businesses.Any(b => b.IndustryId == "cleaning" && b.Units == 0));
            Assert.IsFalse(_ui.Screen(UiRoot.Industry).Q<Button>("choose-coffee_cart").enabledSelf, "one business per chapter 2");
            Assert.IsFalse(_ui.Loan.IsOpen, "enough cash: no loan needed");
            Assert.AreEqual(DisplayStyle.Flex, _ui.Screen(UiRoot.Industry).Q("industry-offer").resolvedStyle.display, "offer to open the first unit");
        }

        [UnityTest]
        public IEnumerator Short_cash_opens_the_loan_dialog_with_terms()
        {
            yield return Boot(setup: s => Chapter2(s, 100_000));
            _ui.Open(UiRoot.Industry);
            yield return null;
            Assert.IsTrue(_ui.IndustryChoice.Choose("logistics_box_truck"));
            for (int i = 0; i < 2; i++) yield return null;
            Assert.IsTrue(_ui.Loan.IsOpen);
            Assert.AreEqual(_engine.Content.Balance.Loans.TermsMonths.Length, _ui.Loan.TermButtons.Count);
            Assert.LessOrEqual(_ui.Loan.MaxDollars * 100L, _engine.MaxLoan(_state).Cents);
            Assert.IsFalse(string.IsNullOrEmpty(_ui.Loan.AprText), "APR");
            Assert.IsFalse(string.IsNullOrEmpty(_ui.Loan.PaymentText), "monthly payment");
            Assert.IsTrue(_ui.Loan.Take());
            Assert.AreEqual(1, _state.Loans.Count);
            Assert.IsFalse(_ui.Loan.IsOpen);
        }

        [UnityTest]
        public IEnumerator Units_stepper_shows_why_it_was_rejected()
        {
            yield return Boot(setup: s =>
            {
                Chapter2(s, 0);
                s.Businesses.Add(new BusinessState { Id = "cleaning", IndustryId = "cleaning", Price = Money.FromCents(15000) });
            });
            _ui.Open(UiRoot.Decisions);
            yield return null;
            var plus = _ui.Screen(UiRoot.Decisions).Q<Button>("units-plus-cleaning");
            Assert.IsNotNull(plus, "units stepper");
            _ui.DecisionsPanel.StepUnits("cleaning", +1);
            var d = _ui.CurrentDecisions();
            Assert.IsTrue(d.Levers.Any(l => l.BusinessId == "cleaning" && l.Lever == "units" && l.ValueCents == 1));
            var report = _engine.EndMonth(_state, d);
            _ui.ShowReport(report);
            for (int i = 0; i < 2; i++) yield return null;
            var warning = _ui.Screen(UiRoot.Decisions).Q<Label>("units-rejected-cleaning");
            Assert.AreEqual(DisplayStyle.Flex, warning.resolvedStyle.display);
            Assert.IsFalse(string.IsNullOrEmpty(warning.text));
        }

        [UnityTest]
        public IEnumerator Company_shows_unit_economics_after_a_month()
        {
            yield return Boot(setup: s =>
            {
                Chapter2(s, 1_000_000);
                s.Businesses.Add(new BusinessState { Id = "coffee_cart", IndustryId = "coffee_cart", Price = Money.FromCents(400), Units = 1 });
            });
            var report = _engine.EndMonth(_state, _ui.CurrentDecisions());
            _ui.ShowReport(report);
            _ui.Open(UiRoot.Company);
            for (int i = 0; i < 3; i++) yield return null;
            var unit = _ui.Screen(UiRoot.Company).Q("unit-economics");
            Assert.AreEqual(DisplayStyle.Flex, unit.resolvedStyle.display);
            StringAssert.Contains("70", unit.Q<Label>("unit-margin").text);
            Assert.AreEqual("429", unit.Q<Label>("unit-break-even").text);
            Assert.IsFalse(string.IsNullOrEmpty(unit.Q<Label>("unit-ltv").text));
        }

        [UnityTest]
        public IEnumerator Hud_shows_chapter_progress()
        {
            yield return Boot(setup: s => s.Cash = Money.FromCents(50_000));
            yield return null;
            Assert.AreEqual(DisplayStyle.Flex, _ui.ChapterBar.resolvedStyle.display);
            Assert.AreEqual(_engine.Chapter(_state).Progress * 100, _ui.ChapterFill.style.width.value.value, 0.5);
            Assert.AreEqual(DisplayStyle.None, _ui.AdvanceButton.resolvedStyle.display);

            _state.Cash = _engine.Content.Balance.Chapter1CashGoal;
            _state.Reserve = _engine.Content.Balance.PersonalCosts.Total;
            _ui.RefreshHud();
            yield return null;
            Assert.AreEqual(DisplayStyle.Flex, _ui.AdvanceButton.resolvedStyle.display, "goal met: move on");
            int chapter = 0;
            _ui.ChapterAdvanced += c => chapter = c;
            _ui.Advance();
            yield return null;
            Assert.AreEqual(2, chapter);
            Assert.IsTrue(_ui.Transition.IsPlaying, "title card while Brooklyn loads");
            Assert.AreEqual(UiRoot.Industry, _ui.CurrentTab, "chapter 2 starts with the industry choice");
        }
    }
}
