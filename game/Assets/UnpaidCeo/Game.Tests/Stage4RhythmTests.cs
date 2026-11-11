using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Core.Persistence;
using UnpaidCeo.Game.Narrative;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>
    /// Stage 4 rhythm: the CEO plans a day in hours ("My day · 24 h"), a checklist of requirements sits before
    /// "Next month" (plan reviewed, a place visited, every second month three academy answers), and a scene shows
    /// what a choice did right after it is made.
    /// </summary>
    public class Stage4RhythmTests
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

        // ---------- My day · 24 hours ----------

        [UnityTest]
        public IEnumerator The_day_has_24_hours_and_12_of_them_are_the_ceos_to_give()
        {
            yield return Boot();
            _ui.Open(UiRoot.Decisions);
            yield return null;
            var day = _ui.Root.Q<DayPlanControl>();
            Assert.IsNotNull(day, "the plan screen shows the day planner");
            Assert.IsNull(_ui.Root.Q("time-allocation"), "percent sliders are gone");
            Assert.AreEqual(DayPlanControl.FreeHours, day.Hours.Sum());
            Assert.AreEqual(24, DayPlanControl.Sleep + DayPlanControl.Travel + DayPlanControl.Chores + DayPlanControl.FreeHours);
            foreach (var key in DayPlanControl.Keys)
                Assert.IsTrue(Shown(_ui.Root.Q("day-hours-" + key)), key + " shows its hours");
            Assert.IsTrue(Shown(_ui.Root.Q("day-bar")), "the segmented day bar is shown");
        }

        [UnityTest]
        public IEnumerator An_hour_more_at_work_comes_out_of_my_time_and_the_plan_follows()
        {
            yield return Boot();
            _ui.Open(UiRoot.Decisions);
            yield return null;
            var day = _ui.Root.Q<DayPlanControl>();
            int work = day.Hours[DayPlanControl.Work], me = day.Hours[DayPlanControl.MyTime];
            Tap(_ui.Root.Q<Button>("day-plus-work"));
            Assert.AreEqual(work + 1, day.Hours[DayPlanControl.Work]);
            Assert.AreEqual(me - 1, day.Hours[DayPlanControl.MyTime]);
            Assert.AreEqual(DayPlanControl.FreeHours, day.Hours.Sum());
            var d = _ui.CurrentDecisions();
            Assert.AreEqual(100, d.Work + d.Family + d.Rest + d.Study);
            Assert.AreEqual(DayPlanControl.SharesFromHours(day.Hours)[DayPlanControl.Work], d.Work);
            Assert.AreEqual((work + 1).ToString(), _ui.Root.Q<Label>("day-hours-work").text);
            Assert.AreEqual("h", _ui.Root.Q("day-row-work").Q<Label>(className: "day-row__unit").text, "the unit is its own localized word");
        }

        [UnityTest]
        public IEnumerator Hours_never_go_below_zero_and_a_taken_hour_goes_back_to_my_time()
        {
            yield return Boot();
            _ui.Open(UiRoot.Decisions);
            yield return null;
            var day = _ui.Root.Q<DayPlanControl>();
            int me = day.Hours[DayPlanControl.MyTime];
            int study = day.Hours[DayPlanControl.Study];
            for (int i = 0; i < 20; i++) day.Remove(DayPlanControl.Study);
            Assert.AreEqual(0, day.Hours[DayPlanControl.Study]);
            Assert.AreEqual(me + study, day.Hours[DayPlanControl.MyTime]);
            Assert.IsFalse(_ui.Root.Q<Button>("day-minus-study").enabledSelf, "nothing left to take");
            for (int i = 0; i < 20; i++) day.Add(DayPlanControl.Work);
            Assert.AreEqual(DayPlanControl.FreeHours, day.Hours.Sum());
            Assert.IsTrue(day.Hours.All(h => h >= 0));
        }

        [Test]
        public void Shares_and_hours_round_trip_without_losing_the_total()
        {
            var hours = DayPlanControl.HoursFromShares(new[] { 55, 15, 20, 10 });
            Assert.AreEqual(DayPlanControl.FreeHours, hours.Sum());
            Assert.AreEqual(7, hours[DayPlanControl.Work], "55% of 12 hours is about 7");
            var shares = DayPlanControl.SharesFromHours(hours);
            Assert.AreEqual(100, shares.Sum());
            Assert.AreEqual(hours, DayPlanControl.HoursFromShares(shares), "a saved plan comes back as the same hours");
            CollectionAssert.AreEqual(new[] { 100, 0, 0, 0 }, DayPlanControl.SharesFromHours(new[] { 12, 0, 0, 0 }));
        }

        // ---------- Requirements before "Next month" ----------

        [UnityTest]
        public IEnumerator Next_month_waits_until_the_day_is_planned_and_a_place_was_visited()
        {
            yield return Boot();
            Assert.IsTrue(Shown(_ui.Root.Q("turn-gate")), "the checklist sits on the main screen");
            Assert.IsFalse(_ui.NextMonthButton.enabledSelf, "nothing done yet: Next month is locked");
            Assert.IsTrue(Shown(_ui.Root.Q("gate-plan")));
            Assert.IsTrue(Shown(_ui.Root.Q("gate-place")));
            Assert.IsNull(_ui.Root.Q("gate-study"), "month 1 asks no study");

            Tap(_ui.Root.Q<Button>("gate-plan"));
            Assert.AreEqual(UiRoot.Decisions, _ui.CurrentTab, "the item opens the plan as a popup");
            _ui.Open(UiRoot.City);
            Assert.IsTrue(_ui.Gate.PlanReviewed);
            Assert.IsFalse(_ui.NextMonthButton.enabledSelf);

            Tap(_ui.Root.Q<Button>("gate-place"));
            Assert.AreEqual(UiRoot.PlacesTab, _ui.CurrentTab);
            _ui.OpenPlace("bodega");
            _ui.Open(UiRoot.City);
            Assert.IsTrue(_ui.Gate.AllMet);
            Assert.IsTrue(_ui.NextMonthButton.enabledSelf, "all requirements met: Next month opens");
            Assert.IsTrue(_ui.Root.Q("gate-plan").ClassListContains("gate__item--done"));
        }

        [UnityTest]
        public IEnumerator Every_second_month_asks_for_three_new_academy_answers()
        {
            yield return Boot(s => { s.Month = 2; s.LessonsSeen["revenue_vs_profit"] = 1; });
            Assert.IsTrue(Shown(_ui.Root.Q("gate-study")), "month 2 asks for study");
            Assert.AreEqual(3, _ui.Gate.AnswersNeeded);
            _ui.Open(UiRoot.Decisions); _ui.OpenPlace("bodega"); _ui.Open(UiRoot.City);
            Assert.IsFalse(_ui.NextMonthButton.enabledSelf, "the plan and the visit are not enough on a study month");

            var lesson = _engine.Content.Lessons["revenue_vs_profit"];
            _ui.AcademyPanel.Answer(lesson.Id, 0, (lesson.Quiz[0].CorrectIndex + 1) % lesson.Quiz[0].OptionKeys.Count);
            Assert.AreEqual(1, _ui.Gate.AnswersGiven, "the game records one attempt per question: a wrong answer spends it and shows the right one");
            _ui.AcademyPanel.Answer(lesson.Id, 0, lesson.Quiz[0].CorrectIndex);
            Assert.AreEqual(1, _ui.Gate.AnswersGiven, "the same question counts once");
            Assert.AreEqual("1 / 3", _ui.Root.Q("gate-study").Q<Label>(className: "gate__count").text);
            for (int q = 1; q < 3; q++) _ui.AcademyPanel.Answer(lesson.Id, q, lesson.Quiz[q].CorrectIndex);
            Assert.AreEqual(3, _ui.Gate.AnswersGiven);
            Assert.IsTrue(_ui.Gate.AllMet);
            Assert.IsTrue(_ui.NextMonthButton.enabledSelf);
        }

        [UnityTest]
        public IEnumerator A_study_month_with_fewer_new_questions_asks_only_for_those()
        {
            yield return Boot(s =>
            {
                s.Month = 4;
                s.LessonsSeen["revenue_vs_profit"] = 1;
                var l = Content().Lessons["revenue_vs_profit"];
                s.QuizAnswers[l.Id + "/0"] = l.Quiz[0].CorrectIndex;
                s.QuizAnswers[l.Id + "/1"] = (l.Quiz[1].CorrectIndex + 1) % l.Quiz[1].OptionKeys.Count;
            });
            Assert.AreEqual(1, _ui.Gate.AnswersNeeded, "two of three questions were already answered (one of them wrong: it is spent)");
            yield return Boot(s => s.Month = 2);
            Assert.IsNull(_ui.Root.Q("gate-study"), "no lesson open yet: nothing to study, no lock");
        }

        [Test]
        public void The_checklist_survives_a_reload_and_resets_with_the_month()
        {
            var gate = new TurnGate();
            gate.StartMonth(2);
            gate.PlanReviewed = true;
            gate.RecordAnswer("revenue_vs_profit/1");
            gate.RecordAnswer("revenue_vs_profit/1");
            Assert.AreEqual(1, gate.AnswersGiven, "the same question counts once");
            var meta = new MetaProgress();
            gate.CopyTo(meta);
            var back = SaveCodec.DecodeMeta(SaveCodec.EncodeMeta(new PlayerMeta { Meta = meta })).Meta;
            var loaded = TurnGate.From(back);
            Assert.AreEqual(2, loaded.Month);
            Assert.IsTrue(loaded.PlanReviewed);
            Assert.IsFalse(loaded.PlaceVisited);
            Assert.AreEqual(1, loaded.AnswersGiven);
            loaded.StartMonth(3);
            Assert.IsFalse(loaded.PlanReviewed);
            Assert.AreEqual(0, loaded.AnswersGiven);
            loaded.StartMonth(3);
            loaded.PlanReviewed = true;
            loaded.StartMonth(3);
            Assert.IsTrue(loaded.PlanReviewed, "the same month again keeps what was done");
        }

        // ---------- Signs over the buildings ----------

        [UnityTest]
        public IEnumerator A_sign_over_a_building_shows_the_places_picture_and_name()
        {
            yield return Boot();
            var city = _ui.Screen(UiRoot.City);
            _ui.PlaceMarker("bank", new Vector2(city.worldBound.center.x, city.worldBound.center.y));
            yield return null;
            var sign = city.Q<Button>("marker-bank");
            Assert.IsTrue(Shown(sign));
            Assert.IsNotNull(sign.Q("marker-pic-bank"), "the sign carries the place's picture");
            Assert.AreEqual("Bank", sign.Q<Label>("marker-title-bank").text);
        }

        // ---------- A living scene ----------

        [Test]
        public void A_choice_describes_what_it_did_from_its_effects()
        {
            var content = Content();
            var chips = EffectChips.Describe(content.Storylets.First(x => x.Id == "ch1_mentor_in_debt").Choices["mom"], "en");
            CollectionAssert.AreEqual(new[] { "+" + UnpaidCeo.Core.Format.MoneyFormat.Compact(UnpaidCeo.Core.Primitives.Money.FromCents(20000), "en"), "npc.mom", "stat.relationships" }, chips.Select(c => c.CaptionKey ?? c.Value).ToArray());
            Assert.IsTrue(chips[0].Good);
            Assert.AreEqual("+3", chips[1].Value);
            Assert.AreEqual("−2", chips[2].Value);
            Assert.IsFalse(chips[2].Good);
            Assert.IsEmpty(EffectChips.Describe(content.Storylets.First(x => x.Id == "ch1_mentor_welcome").Choices["solo"], "en"), "a fact alone is nothing to show");
        }

        [UnityTest]
        public IEnumerator The_scene_shows_the_consequences_of_a_choice_until_it_ends()
        {
            yield return LocalizationSettings.InitializationOperation;
            _host = new GameObject("dialogue");
            var doc = _host.AddComponent<UIDocument>();
            doc.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            var presenter = _host.AddComponent<UitkDialoguePresenter>();
            yield return null;
            var box = doc.rootVisualElement.Q("dialogue-consequences");
            Assert.IsNotNull(box);
            Assert.AreEqual(DisplayStyle.None, box.resolvedStyle.display, "nothing chosen yet");
            presenter.ShowConsequences(Content().Storylets.First(x => x.Id == "ch1_mentor_in_debt").Choices["mom"], "en");
            yield return null;
            Assert.AreEqual(3, box.Query(className: "chip").ToList().Count);
            Assert.AreEqual(DisplayStyle.Flex, box.resolvedStyle.display);
            presenter.OnDialogueCompleteAsync();
            yield return null;
            Assert.AreEqual(0, box.childCount, "a new scene starts clean");
        }
    }
}
