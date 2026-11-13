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
    /// <summary>
    /// One main screen: the city stays on screen, every other screen is a popup over it, a guide card says what to do
    /// now, a month ends in a result popup, and the dock replaces the tab bar.
    /// </summary>
    public class MainScreenTests
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

        IEnumerator Boot(System.Action<GameState> setup = null, int width = 1080, int height = 1920)
        {
            yield return LocalizationSettings.InitializationOperation;
            _target = new RenderTexture(width, height, 24);
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            _engine = new GameEngine(Content());
            _state = _engine.NewGame(5, new HeroAppearance());
            setup?.Invoke(_state);
            _ui.Initialize(_engine, _state, _target);
            yield return _ui.SetLanguage("en");
            for (int i = 0; i < 5; i++) yield return null;
        }

        static bool Shown(VisualElement e)
        {
            for (var v = e; v != null; v = v.parent)
                if (v.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

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
        public IEnumerator Other_screens_open_as_popups_over_the_city()
        {
            yield return Boot();
            Assert.IsTrue(Shown(_ui.Screen(UiRoot.City)));
            Assert.IsFalse(Shown(_ui.PopupBackdrop), "no popup at the start");

            _ui.Open(UiRoot.Company);
            yield return null;
            Assert.IsTrue(Shown(_ui.Screen(UiRoot.City)), "the city stays on screen under a popup");
            Assert.IsTrue(Shown(_ui.Screen(UiRoot.Company)));
            Assert.IsTrue(Shown(_ui.PopupBackdrop), "the popup dims the city");
            Assert.IsTrue(UiText.IsLocalized(_ui.PopupClose), "the close button is a word from the string table");

            Tap(_ui.PopupClose);
            yield return null;
            Assert.AreEqual(UiRoot.City, _ui.CurrentTab);
            Assert.IsFalse(Shown(_ui.Screen(UiRoot.Company)));
            Assert.IsFalse(Shown(_ui.PopupBackdrop));
        }

        [UnityTest]
        public IEnumerator The_dock_opens_the_plan_the_report_lessons_and_ranking()
        {
            yield return Boot();
            foreach (var tab in new[] { UiRoot.Decisions, UiRoot.Company, UiRoot.Academy, UiRoot.Ranking })
            {
                Tap(_ui.Root.Q<Button>("nav-" + tab));
                yield return null;
                Assert.AreEqual(tab, _ui.CurrentTab);
                Tap(_ui.PopupClose);
                yield return null;
                Assert.AreEqual(UiRoot.City, _ui.CurrentTab);
            }
            Assert.IsNull(_ui.Root.Q<Button>("nav-city"), "the city is the main screen, not a tab");
        }

        [UnityTest]
        public IEnumerator A_month_result_pops_up_on_the_main_screen()
        {
            yield return Boot();
            var report = _engine.EndMonth(_state, _ui.CurrentDecisions());
            _ui.ShowReport(report);
            _ui.ShowMonthSummary(report);
            yield return null;

            Assert.IsTrue(_ui.MonthSummaryOpen);
            Assert.AreEqual(UiRoot.City, _ui.CurrentTab, "the player stays on the main screen");
            var s = _ui.Summary.Root;
            Assert.IsTrue(Shown(s));
            Assert.AreEqual(MoneyFormat.Compact(report.Revenue, "en"), s.Q<Label>("summary-revenue").text);
            Assert.AreEqual(MoneyFormat.Compact(report.NetProfit, "en"), s.Q<Label>("summary-profit").text);
            Assert.AreEqual(MoneyFormat.Compact(report.PersonalCosts, "en"), s.Q<Label>("summary-personal").text);
            Assert.AreEqual(MoneyFormat.Compact(report.CashEnd, "en"), s.Q<Label>("summary-cash").text);
            var change = report.CashEnd - report.CashStart;
            Assert.AreEqual((change > Money.Zero ? "+" : "") + MoneyFormat.Compact(change, "en"), s.Q<Label>("summary-change").text,
                "the change over the month carries its sign");
            var tip = s.Q<Label>("summary-tip");
            Assert.IsTrue(UiText.IsLocalized(tip) && !string.IsNullOrEmpty(tip.text) && !tip.text.StartsWith("summary."), "one line teaches what the numbers mean");

            Tap(s.Q<Button>("month-summary-ok"));
            yield return null;
            Assert.IsFalse(_ui.MonthSummaryOpen);
            Assert.IsFalse(Shown(s));
        }

        [UnityTest]
        public IEnumerator The_month_result_leads_to_the_full_report()
        {
            yield return Boot();
            var report = _engine.EndMonth(_state, _ui.CurrentDecisions());
            _ui.ShowReport(report);
            _ui.ShowMonthSummary(report);
            yield return null;
            Tap(_ui.Summary.Root.Q<Button>("month-summary-details"));
            yield return null;
            Assert.IsFalse(_ui.MonthSummaryOpen);
            Assert.AreEqual(UiRoot.Company, _ui.CurrentTab);
        }

        [UnityTest]
        public IEnumerator The_month_result_adds_up()
        {
            yield return Boot();
            Money M(long dollars) => Money.FromCents(dollars * 100);
            var report = new MonthReport { Month = 3, Revenue = M(2470), NetProfit = M(1746), PersonalCosts = M(1714), CashStart = M(103), CashEnd = M(195) };
            _ui.ShowMonthSummary(report);
            yield return null;
            var other = _ui.Summary.Root.Q<Label>("summary-other");
            Assert.IsNotNull(other, "profit minus living costs is +$32 but cash grew by $92: the popup must say where the other $60 came from");
            Assert.IsTrue(Shown(other));
            Assert.AreEqual("+" + MoneyFormat.Compact(M(60), "en"), other.text);

            report.CashEnd = M(135);   // exactly profit minus living costs
            _ui.ShowMonthSummary(report);
            yield return null;
            Assert.IsFalse(Shown(other), "nothing else moved the cash: no empty row");
        }

        [Test]
        public void The_month_result_picks_the_lesson_the_numbers_call_for()
        {
            Money M(long dollars) => Money.FromCents(dollars * 100);
            Assert.AreEqual("summary.tip.no_revenue", MonthSummaryDialog.TipKey(new MonthReport { Revenue = M(0), NetProfit = M(-900), CashStart = M(1000), CashEnd = M(100) }));
            Assert.AreEqual("summary.tip.loss", MonthSummaryDialog.TipKey(new MonthReport { Revenue = M(500), NetProfit = M(-200), CashStart = M(1000), CashEnd = M(800) }));
            Assert.AreEqual("summary.tip.cash_down", MonthSummaryDialog.TipKey(new MonthReport { Revenue = M(500), NetProfit = M(200), CashStart = M(1000), CashEnd = M(700) }));
            Assert.AreEqual("summary.tip.profit", MonthSummaryDialog.TipKey(new MonthReport { Revenue = M(500), NetProfit = M(200), CashStart = M(1000), CashEnd = M(1100) }));
        }

        [UnityTest]
        public IEnumerator The_guide_says_what_to_do_now()
        {
            yield return Boot();
            _ui.SetTurns(3, 6, 0);
            yield return null;
            Assert.IsTrue(Shown(_ui.Guide), "a new player always sees the next step");
            Assert.AreEqual("guide.plan", _ui.GuideKey);
            Assert.IsTrue(UiText.IsLocalized(_ui.GuideText) && !_ui.GuideText.text.StartsWith("guide."));
            Tap(_ui.GuideAction);
            yield return null;
            Assert.AreEqual(UiRoot.Decisions, _ui.CurrentTab, "the action opens the month plan");

            _ui.Open(UiRoot.City);
            _ui.SetTurns(0, 6, 3600);
            yield return null;
            Assert.AreEqual("guide.no_turns", _ui.GuideKey, "no months left today: the guide says so and offers a lesson");
            Tap(_ui.GuideAction);
            yield return null;
            Assert.AreEqual(UiRoot.Academy, _ui.CurrentTab);
        }

        [UnityTest]
        public IEnumerator The_guide_sends_an_owner_without_a_business_to_the_choice()
        {
            yield return Boot(s => { s.Chapter = 2; s.Cash = Money.FromCents(100_000); });
            _ui.Open(UiRoot.City);
            yield return null;
            Assert.AreEqual("guide.choose_business", _ui.GuideKey);
            Tap(_ui.GuideAction);
            yield return null;
            Assert.AreEqual(UiRoot.Industry, _ui.CurrentTab);
        }

        [UnityTest]
        public IEnumerator The_guide_offers_the_next_chapter_once_the_goal_is_met()
        {
            yield return Boot(s => { s.Cash = Money.FromCents(100_000_00); s.Reserve = Money.FromCents(100_000_00); });
            Assert.IsTrue(_engine.Chapter(_state).GoalMet, "test setup: the chapter 1 goal is met");
            Assert.AreEqual("guide.next_chapter", _ui.GuideKey);
        }

        [UnityTest]
        public IEnumerator The_guide_points_at_a_new_lesson_after_a_month_that_taught_one()
        {
            yield return Boot();
            _ui.SetTurns(3, 6, 0);
            var report = new MonthReport { Month = 1 };
            report.Lessons.Add("revenue_vs_profit");
            _ui.ShowReport(report);
            yield return null;
            Assert.AreEqual("guide.lesson", _ui.GuideKey);
            Tap(_ui.GuideAction);
            yield return null;
            Assert.AreEqual(UiRoot.Academy, _ui.CurrentTab);
            _ui.Open(UiRoot.City);
            yield return null;
            Assert.AreEqual("guide.plan", _ui.GuideKey, "the lesson was opened: back to the month plan");
        }

        [UnityTest]
        public IEnumerator The_guide_can_step_aside()
        {
            yield return Boot();
            _ui.GuideEnabled = false;   // onboarding has its own coach
            yield return null;
            Assert.IsFalse(Shown(_ui.Guide));
            _ui.GuideEnabled = true;
            yield return null;
            Assert.IsTrue(Shown(_ui.Guide));
        }

        [UnityTest]
        public IEnumerator Next_month_is_off_while_the_game_is_busy()
        {
            yield return Boot();
            TestGate.Satisfy(_ui, _engine.Content, _state);
            _ui.SetTurns(3, 6, 0);
            string idle = _ui.NextMonthButton.text;
            _ui.SetBusy(true);
            yield return null;
            Assert.IsFalse(_ui.NextMonthButton.enabledSelf, "a month is running: the button shows it cannot be pressed");
            Assert.AreEqual("Week in progress…", _ui.NextMonthButton.text, "and says why");
            _ui.SetBusy(false);
            yield return null;
            Assert.IsTrue(_ui.NextMonthButton.enabledSelf);
            Assert.AreEqual(idle, _ui.NextMonthButton.text);
            _ui.SetTurns(0, 6, 60);
            _ui.SetBusy(true);
            _ui.SetBusy(false);
            Assert.IsFalse(_ui.NextMonthButton.enabledSelf, "no turns left: still off after the month");
        }

        [UnityTest]
        public IEnumerator Missions_are_a_compact_widget_that_opens_on_tap()
        {
            yield return Boot();
            _ui.SetMissions(new[]
            {
                new MissionView("mission.months_3", 1, 3, false),
                new MissionView("mission.quiz_1", 1, 1, true),
            }, streak: 2, ceoPoints: 5);
            for (int i = 0; i < 3; i++) yield return null;

            var toggle = _ui.MissionsPanel.Q<Button>("missions-toggle");
            Assert.IsNotNull(toggle);
            Assert.IsTrue(toggle.Query<Label>().ToList().Any(l => l.text == "1/2"), "the chip counts done missions");
            var row = _ui.MissionsPanel.Query(className: "mission").First();
            Assert.IsFalse(Shown(row), "collapsed: the list does not cover the city");
            Tap(toggle);
            yield return null;
            Assert.IsTrue(Shown(row), "a tap opens the list");
            Tap(toggle);
            yield return null;
            Assert.IsFalse(Shown(row));
        }

        [UnityTest]
        public IEnumerator The_chapter_bar_names_the_goal()
        {
            yield return Boot();
            var goal = _ui.ChapterBar.Q<Label>("chapter-goal");
            Assert.IsNotNull(goal, "the bar says what the goal is, not just 'chapter goal'");
            Assert.IsTrue(UiText.IsLocalized(goal) && !string.IsNullOrEmpty(goal.text) && !goal.text.StartsWith("goal."));
            Assert.AreEqual(MoneyFormat.Compact(_engine.Content.Balance.Chapter1CashGoal, "en"), _ui.ChapterBar.Q<Label>("chapter-goal-value").text);
        }

        [UnityTest]
        public IEnumerator On_a_wide_screen_the_ui_is_a_phone_wide_column_in_the_middle()
        {
            yield return Boot(width: 1920, height: 1080);   // a desktop browser window
            var screen = _ui.Root.panel.visualTree.worldBound;
            Assert.Greater(screen.width, 3000f, "test setup: a landscape panel in reference units");
            Assert.LessOrEqual(_ui.Root.worldBound.width, 1081f, "the phone layout is not stretched across a desktop window");
            Assert.AreEqual(screen.center.x, _ui.Root.worldBound.center.x, 1f, "and sits in the middle");

            var report = _engine.EndMonth(_state, _ui.CurrentDecisions());
            _ui.ShowReport(report);
            _ui.ShowMonthSummary(report);
            yield return null;
            var modal = _ui.Summary.Root;
            Assert.LessOrEqual(modal.worldBound.xMin, screen.xMin + 1f, "a modal dims the whole screen, not only the column");
            Assert.GreaterOrEqual(modal.worldBound.xMax, screen.xMax - 1f);
            Assert.LessOrEqual(modal.Q<Button>("month-summary-ok").worldBound.width, 1081f, "its panel stays phone-wide");
        }

        static void AssertColour(Color32 expected, Color actual, string why)
        {
            Color32 a = actual;
            Assert.IsTrue(Mathf.Abs(a.r - expected.r) <= 2 && Mathf.Abs(a.g - expected.g) <= 2 && Mathf.Abs(a.b - expected.b) <= 2, $"{why}: expected {expected}, was {a}");
        }

        [UnityTest]
        public IEnumerator Colours_carry_meaning()
        {
            yield return Boot(s => s.Cash = Money.FromCents(-5_000));
            var cash = _ui.Root.Q<Label>("hud-cash");
            AssertColour(new Color32(0xD9, 0x48, 0x3B, 255), cash.resolvedStyle.color, "cash below zero is red");
            AssertColour(new Color32(0xA9, 0xB4, 0xCC, 255), _ui.Root.Q<Label>("hud-cash-caption").resolvedStyle.color, "captions are dimmer than values on the navy bar");

            _state.Cash = Money.FromCents(5_000);
            _ui.RefreshHud();
            yield return null;
            AssertColour(new Color32(0x6F, 0xD6, 0x9A, 255), cash.resolvedStyle.color, "money is green (the light green that reads on navy)");

            _ui.Transition.Play(2);
            yield return null;
            var title = _ui.Transition.Root.Q<Label>(className: "chapter-card__title");
            AssertColour(new Color32(255, 255, 255, 255), title.resolvedStyle.color, "the chapter card is white on dark");
            _ui.Transition.Hide();
        }

        [UnityTest]
        public IEnumerator The_month_plan_explains_itself()
        {
            yield return Boot();
            _ui.Open(UiRoot.Decisions);
            yield return null;
            var hint = _ui.Screen(UiRoot.Decisions).Q<Label>("decisions-hint");
            Assert.IsNotNull(hint, "a new player is told how the week is decided");
            Assert.IsTrue(Shown(hint) && UiText.IsLocalized(hint) && hint.text.Length > 40 && !hint.text.StartsWith("decisions."));
            Assert.AreEqual("This week", _ui.Screen(UiRoot.Decisions).Q<Label>("decisions-title").text, "the popup is named like the button that opens it (Week)");
            _ui.Open(UiRoot.Company);
            yield return null;
            Assert.AreEqual("Report", _ui.Screen(UiRoot.Company).Q<Label>("company-title").text);
        }

        [UnityTest]
        public IEnumerator The_hero_tag_follows_the_hero_on_the_city()
        {
            yield return Boot();
            _ui.PlaceHeroTag(new Vector2(400, 500));
            yield return null;
            Assert.IsTrue(Shown(_ui.HeroTag));
            Assert.IsTrue(UiText.IsLocalized(_ui.HeroTag));
            Assert.AreEqual(_ui.Screen(UiRoot.City), _ui.HeroTag.parent, "the tag belongs to the city, so popups cover it");
            _ui.PlaceHeroTag(null);
            yield return null;
            Assert.IsFalse(Shown(_ui.HeroTag), "no hero on screen: no tag");

            _ui.PlaceTag("mentor", "city.mentor", new Vector2(300, 400));
            yield return null;
            var mentor = _ui.Screen(UiRoot.City).Q<Label>("tag-mentor");
            Assert.IsNotNull(mentor, "the people of the block carry name tags too");
            Assert.IsTrue(Shown(mentor) && UiText.IsLocalized(mentor) && mentor.text == "Mentor");
            _ui.PlaceTag("mentor", "city.mentor", null);
            yield return null;
            Assert.IsFalse(Shown(mentor));
        }
    }
}
