using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Stage 4B on screen: the HUD counts weeks, the result says what was paid and what comes next, study every second week.</summary>
    public class WeekUiTests
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
                Storylets = Read("storylets_ch1.json"), Npcs = Read("npcs.json"), Lessons = Read("lessons.json"), Missions = Read("missions.json"),
            });
        }

        IEnumerator Boot(System.Action<GameState> setup = null)
        {
            yield return LocalizationSettings.InitializationOperation;
            _target = new RenderTexture(1080, 1920, 24);
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            _engine = new GameEngine(Content());
            _state = _engine.NewGame(5, new HeroAppearance());
            setup?.Invoke(_state);
            _ui.Initialize(_engine, _state, _target);
            yield return _ui.SetLanguage("en");
            for (int i = 0; i < 5; i++) yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            if (_target != null) Object.Destroy(_target);
        }

        static bool Shown(VisualElement e)
        {
            for (var v = e; v != null; v = v.parent)
                if (v.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        [UnityTest]
        public IEnumerator The_hud_counts_weeks_and_the_button_says_next_week()
        {
            yield return Boot();
            Assert.AreEqual("Week", _ui.Root.Q<Label>("hud-month-caption").text);
            Assert.AreEqual("1", _ui.Root.Q<Label>("hud-month").text);
            Assert.AreEqual("Next week", _ui.NextMonthButton.text);
            Assert.AreEqual("This week", _ui.Screen(UiRoot.Decisions).Q<Label>("decisions-title").text);
            _ui.SetBusy(true);
            yield return null;
            Assert.AreEqual("Week in progress…", _ui.NextMonthButton.text);
        }

        [UnityTest]
        public IEnumerator The_weeks_result_lists_what_was_paid_and_what_falls_due_next_week()
        {
            yield return Boot(s => s.LifetimeEarnings = Money.FromCents(1_000_000));
            var report = _engine.EndWeek(_state, _ui.CurrentDecisions());
            _ui.ShowMonthSummary(report);
            yield return null;
            Assert.AreEqual("Week", _ui.Summary.Root.Q<Label>(className: "section-title").text);
            Assert.AreEqual("1", _ui.Summary.Root.Q<Label>("summary-month").text);
            var paid = _ui.Summary.Root.Q("summary-paid");
            Assert.IsTrue(Shown(paid));
            var paidTexts = paid.Query<Label>().ToList().Select(l => l.text).ToList();
            CollectionAssert.Contains(paidTexts, "Food");
            CollectionAssert.Contains(paidTexts, "Transit");
            Assert.IsFalse(paidTexts.Contains("Rent"), "rent is collected on the last week of the month");
            Assert.IsFalse(paidTexts.Contains("Essentials"), "essentials are a week-2 bill");
            var due = _ui.Summary.Root.Q("summary-due");
            Assert.IsTrue(Shown(due));
            CollectionAssert.Contains(due.Query<Label>().ToList().Select(l => l.text).ToList(), "Essentials");
            var food = Money.FromCents(_engine.Content.Balance.PersonalCosts.Food.Cents / 4);
            CollectionAssert.Contains(paidTexts, MoneyFormat.Compact(food, "en"));
        }

        [UnityTest]
        public IEnumerator A_result_without_bills_hides_the_paid_list()
        {
            yield return Boot(s => s.Businesses.Clear());   // nothing earned yet: the city sends no bills
            _ui.ShowMonthSummary(_engine.EndWeek(_state, _ui.CurrentDecisions()));
            yield return null;
            Assert.IsFalse(Shown(_ui.Summary.Root.Q("summary-paid")));
        }

        [UnityTest]
        public IEnumerator Study_is_asked_every_second_week()
        {
            yield return Boot(s => { s.Week = 2; s.LessonsSeen["revenue_vs_profit"] = 1; });
            Assert.IsTrue(Shown(_ui.Root.Q("gate-study")), "week 2 asks for study");
            Assert.AreEqual("Before next week", _ui.Root.Q("turn-gate").Q<Label>(className: "gate__title").text);
            yield return Boot(s => { s.Week = 3; s.LessonsSeen["revenue_vs_profit"] = 1; });
            Assert.IsNull(_ui.Root.Q("gate-study"), "week 3 does not");
        }
    }
}
