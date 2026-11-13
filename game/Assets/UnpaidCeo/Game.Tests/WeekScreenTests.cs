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
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Persistence;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>
    /// The Week screen (stage 5A): no checkboxes, sliders or hour steppers — a mode, a focus, the deeds and the levers are
    /// cards taken with one tap and closed until next week; what was tapped survives a reload.
    /// </summary>
    public class WeekScreenTests
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

        IEnumerator Boot(System.Action<GameState> setup = null, bool runMode = false)
        {
            yield return LocalizationSettings.InitializationOperation;
            _target = _target ?? new RenderTexture(1080, 1920, 24);
            _engine = _engine ?? new GameEngine(Content());
            if (_state == null)
            {
                _state = _engine.NewGame(5, new HeroAppearance());
                setup?.Invoke(_state);
            }
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            _ui.RunMode = runMode;
            _ui.Initialize(_engine, _state, _target);
            yield return _ui.SetLanguage("en");
            _ui.Open(UiRoot.Decisions);
            for (int i = 0; i < 5; i++) yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            if (_target != null) Object.Destroy(_target);
            _state = null;
            _engine = null;
            _target = null;
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

        VisualElement Screen => _ui.Screen(UiRoot.Decisions);
        Button Mode(string id) => Screen.Q<Button>("mode-" + id);
        Button Focus(string id) => Screen.Q<Button>("focus-" + id);

        [UnityTest]
        public IEnumerator Week_screen_is_cards_only()
        {
            yield return Boot();
            Assert.AreEqual(0, Screen.Query<Toggle>().ToList().Count, "no checkboxes");
            // The ScrollView's own scrollbar is a Slider inside a Scroller: only sliders a player would set count.
            Assert.AreEqual(0, Screen.Query<Slider>().Where(s => s.GetFirstAncestorOfType<Scroller>() == null).ToList().Count
                + Screen.Query<SliderInt>().ToList().Count, "no sliders");
            Assert.IsNull(Screen.Q("day-plan"), "no hour steppers");
            foreach (var mode in WeekPlanning.ModeOrder) Assert.IsNotNull(Mode(mode), mode);
            Assert.IsNotNull(Screen.Q<Button>("action-overtime"));
            Assert.IsNotNull(Focus(WeekPlanning.EvenFocus));
        }

        [UnityTest]
        public IEnumerator Choosing_a_mode_locks_the_others_until_next_week()
        {
            yield return Boot();
            Tap(Mode("hustle"));
            yield return null;
            Assert.IsTrue(_ui.DecisionsPanel.ModeChosen);
            foreach (var other in WeekPlanning.ModeOrder.Where(m => m != "hustle"))
                Assert.IsFalse(Mode(other).enabledSelf, other + " is closed for the week");
            var parts = _engine.Content.Balance.WeekModes["hustle"];
            var d = _ui.CurrentDecisions();
            Assert.AreEqual((parts[0], parts[1], parts[2], parts[3]), (d.Work, d.Family, d.Rest, d.Study));

            _ui.ShowReport(_engine.EndWeek(_state, d));
            yield return null;
            Assert.IsTrue(Mode("balance").enabledSelf, "a new week, a new choice");
            Assert.IsFalse(_ui.DecisionsPanel.ModeChosen);
            Assert.AreEqual("hustle", _ui.DecisionsPanel.Mode, "last week's mode stays selected");
        }

        [UnityTest]
        public IEnumerator Tapping_the_current_mode_confirms_the_week_gate()
        {
            yield return Boot();
            Assert.IsFalse(_ui.Gate.PlanReviewed, "just looking does not count");
            Assert.AreEqual("balance", _ui.DecisionsPanel.Mode, "a new game starts on Balance");
            Tap(Mode("balance"));
            Assert.IsTrue(_ui.Gate.PlanReviewed);
        }

        [UnityTest]
        public IEnumerator Focus_card_sends_all_work_to_that_gig()
        {
            yield return Boot(s => s.StudyXp = 100);
            Tap(Focus("gig_resale"));
            yield return null;
            var d = _ui.CurrentDecisions();
            Assert.AreEqual(100, d.WorkSplit["gig_resale"]);
            Assert.AreEqual(0, d.WorkSplit["gig_delivery"]);
            Assert.IsFalse(Focus("gig_delivery").enabledSelf, "the focus is set for the week");
        }

        [UnityTest]
        public IEnumerator A_locked_gig_says_how_much_knowledge_it_needs()
        {
            yield return Boot(s => s.StudyXp = 0);
            var ai = Focus("gig_ai_freelance");
            Assert.IsFalse(ai.enabledSelf);
            int need = _engine.Content.Industries["gig_ai_freelance"].RequiredStudy;
            Assert.IsTrue(ai.Query<Label>().ToList().Any(l => Shown(l) && l.text == need.ToString()), "it names the knowledge it needs");
        }

        [UnityTest]
        public IEnumerator Evenly_splits_between_the_gigs_that_pay_and_skips_a_locked_one()
        {
            yield return Boot(s => s.StudyXp = 0);
            Tap(Focus(WeekPlanning.EvenFocus));
            yield return null;
            var d = _ui.CurrentDecisions();
            Assert.AreEqual(0, d.WorkSplit["gig_ai_freelance"], "time sent to a locked gig earns nothing");
            Assert.AreEqual(100, d.WorkSplit["gig_delivery"] + d.WorkSplit["gig_resale"]);
            Assert.LessOrEqual(System.Math.Abs(d.WorkSplit["gig_delivery"] - d.WorkSplit["gig_resale"]), 1);
            Assert.IsTrue(_ui.DecisionsPanel.FocusChosen);
        }

        [UnityTest]
        public IEnumerator Owner_with_a_unit_sees_no_focus_but_modes_and_levers()
        {
            yield return Boot(s =>
            {
                s.Chapter = 2;
                s.Cash = Money.FromDollars(10_000);
                s.Businesses.Add(new BusinessState { Id = "coffee_cart", IndustryId = "coffee_cart", Price = Money.FromCents(400), Units = 1 });
            });
            Assert.IsFalse(Shown(Screen.Q("focus-cards")), "gigs no longer pay: no focus");
            Assert.IsTrue(Shown(Screen.Q("owner-time")));
            Assert.IsTrue(Shown(Mode("hustle")));
            Tap(Screen.Q<Button>("price-plus-coffee_cart"));
            Assert.IsFalse(Screen.Q<Button>("price-plus-coffee_cart").enabledSelf, "one step a week");
            Assert.IsFalse(Screen.Q<Button>("price-minus-coffee_cart").enabledSelf);
            Assert.IsTrue(Screen.Q<Label>("price-value-coffee_cart").text.Contains("→"), "now → after");
        }

        static void Owner(GameState s)
        {
            s.Chapter = 2;
            s.Cash = Money.FromDollars(10_000);
            s.Businesses.Add(new BusinessState { Id = "coffee_cart", IndustryId = "coffee_cart", Price = Money.FromCents(400), Units = 1 });
        }

        static string Localized(string key) => LocalizationSettings.StringDatabase.GetLocalizedString(UiRoot.Table, key);

        [UnityTest]
        public IEnumerator A_monthly_habit_says_when_it_opens_the_moment_it_is_done()
        {
            yield return Boot(s => s.Cash = Money.FromDollars(640));
            Tap(Screen.Q<Button>("action-reserve_transfer"));
            yield return null;
            int weeks = _engine.Content.FreeActions["reserve_transfer"].CooldownWeeks;
            Assert.AreEqual(Localized("choice.done.cooldown." + weeks), Screen.Q<Label>("action-status-reserve_transfer").text,
                "a once-a-month habit does not promise next week");
            Assert.AreEqual(weeks, _ui.DecisionsPanel.WeeksUntilOpen("reserve_transfer"));
        }

        [UnityTest]
        public IEnumerator A_stepped_lever_shows_which_way_it_went()
        {
            yield return Boot(Owner);
            Tap(Screen.Q<Button>("price-plus-coffee_cart"));
            Assert.IsTrue(Screen.Q<Button>("price-plus-coffee_cart").ClassListContains("lever-button--taken"));
            Assert.IsFalse(Screen.Q<Button>("price-minus-coffee_cart").ClassListContains("lever-button--taken"));
        }

        [UnityTest]
        public IEnumerator A_lever_at_its_limit_cannot_be_stepped_further()
        {
            yield return Boot(Owner);
            Assert.IsFalse(Screen.Q<Button>("marketing-minus-coffee_cart").enabledSelf, "no ads yet: nothing to cut");
            Assert.IsTrue(Screen.Q<Button>("marketing-plus-coffee_cart").enabledSelf);
            Assert.IsFalse(Screen.Q<Button>("staff-minus-coffee_cart").enabledSelf, "nobody to let go");
            Assert.IsTrue(Screen.Q<Button>("staff-plus-coffee_cart").enabledSelf);
        }

        [UnityTest]
        public IEnumerator Money_on_the_levers_follows_the_language()
        {
            yield return Boot(Owner);
            yield return _ui.SetLanguage("ru");
            for (int i = 0; i < 3; i++) yield return null;
            var capex = _engine.Content.Industries["coffee_cart"].Capex;
            Assert.AreEqual(MoneyFormat.Compact(capex, "ru"), Screen.Q<Label>("unit-cost-coffee_cart").text);
            Assert.AreEqual(MoneyFormat.Full(Money.FromCents(400), "ru"), Screen.Q<Label>("price-value-coffee_cart").text);
        }

        static void OwnerOfTwo(GameState s)
        {
            Owner(s);
            var cart = s.Businesses.Single(b => b.Id == "coffee_cart");
            cart.Units = 2;
            cart.Employees = 1;
        }

        [UnityTest]
        public IEnumerator A_risky_deed_needs_a_second_tap()
        {
            yield return Boot(s => s.Cash = Money.FromDollars(640));
            var card = Screen.Q<Button>("action-loan_shark_loan");
            Tap(card);
            Assert.IsFalse(_ui.DecisionsPanel.IsPlanned("loan_shark_loan"), "one stray tap does not borrow from a loan shark");
            Assert.AreEqual(Localized("choice.confirm"), Screen.Q<Label>("action-status-loan_shark_loan").text);
            Tap(card);
            Assert.IsTrue(_ui.DecisionsPanel.IsPlanned("loan_shark_loan"));
        }

        [UnityTest]
        public IEnumerator Closing_a_location_or_letting_someone_go_needs_a_second_tap()
        {
            yield return Boot(OwnerOfTwo);
            var close = Screen.Q<Button>("units-minus-coffee_cart");
            Tap(close);
            Assert.IsFalse(_ui.CurrentDecisions().Levers.Any(l => l.Lever == "units"), "one stray tap does not close a location");
            Assert.AreEqual(Localized("lever.close_unit.confirm"), close.text);
            Tap(close);
            Assert.AreEqual(1, _ui.CurrentDecisions().Levers.Single(l => l.Lever == "units").ValueCents);

            var fire = Screen.Q<Button>("staff-minus-coffee_cart");
            Tap(fire);
            Assert.IsFalse(_ui.CurrentDecisions().Levers.Any(l => l.Lever == "staff"));
            Assert.AreEqual(Localized("lever.fire.confirm"), fire.text);
            Tap(fire);
            Assert.AreEqual(0, _ui.CurrentDecisions().Levers.Single(l => l.Lever == "staff").ValueCents);
        }

        [UnityTest]
        public IEnumerator Locked_modes_and_gigs_say_why()
        {
            yield return Boot(s => s.StudyXp = 100);
            Tap(Mode("hustle"));
            Tap(Focus("gig_delivery"));
            foreach (var name in new[] { "mode-status-balance", "focus-status-gig_resale" })
            {
                var status = Screen.Q<Label>(name);
                Assert.IsTrue(Shown(status), name);
                Assert.AreEqual(Localized("week.locked"), status.text, name);
            }
        }

        [UnityTest]
        public IEnumerator A_closed_lever_says_why()
        {
            yield return Boot(s => { Owner(s); s.Cash = Money.FromDollars(100); });
            Assert.AreEqual(Localized("lever.marketing_none"), Screen.Q<Label>("marketing-status-coffee_cart").text);
            Assert.AreEqual(Localized("lever.staff_none"), Screen.Q<Label>("staff-status-coffee_cart").text);
            Assert.IsFalse(Screen.Q<Button>("units-plus-coffee_cart").enabledSelf, "a new location costs more than the cash");
            Assert.AreEqual(Localized("lever.no_money"), Screen.Q<Label>("units-status-coffee_cart").text);
            Tap(Screen.Q<Button>("price-plus-coffee_cart"));
            Assert.AreEqual(Localized("lever.locked"), Screen.Q<Label>("price-status-coffee_cart").text);
        }

        [UnityTest]
        public IEnumerator A_challenge_month_offers_the_months_points_and_four_lever_steps()
        {
            yield return Boot(Owner, runMode: true);
            Assert.AreEqual(LifeSimulator.ActionPoints(_state.Life, 0, _engine.Content.Balance.Life), _ui.DecisionsPanel.PointsTotal,
                "a challenge plays whole months: the buttons offer what the month accepts");
            var plus = Screen.Q<Button>("price-plus-coffee_cart");
            for (int i = 0; i < GameState.WeeksPerMonth; i++)
            {
                Assert.IsTrue(plus.enabledSelf, "step " + (i + 1));
                Tap(plus);
            }
            Assert.IsFalse(plus.enabledSelf, "a month is four weeks: four steps");
        }

        [UnityTest]
        public IEnumerator A_career_week_plan_never_lands_on_a_challenge_run()
        {
            yield return Boot(null, runMode: true);
            var r = new WeekPlanRecord { Week = _state.Week, Seed = _state.Seed, Mode = "hustle", ModeChosen = true };
            r.Actions.Add("overtime");
            _ui.Meta = new MetaProgress { WeekPlan = r };
            Assert.IsFalse(_ui.DecisionsPanel.IsPlanned("overtime"));
            Assert.IsFalse(_ui.DecisionsPanel.ModeChosen);
        }

        [UnityTest]
        public IEnumerator A_plan_from_another_game_is_ignored()
        {
            yield return Boot();
            var r = new WeekPlanRecord { Week = _state.Week, Seed = _state.Seed + 1, Mode = "hustle", ModeChosen = true };
            r.Actions.Add("overtime");
            _ui.DecisionsPanel.Restore(r);
            Assert.IsFalse(_ui.DecisionsPanel.IsPlanned("overtime"), "a new game does not inherit the last game's taps");
            Assert.IsFalse(_ui.DecisionsPanel.ModeChosen);
        }

        /// <summary>A reload the way the game does it: the meta goes through the save codec, a new screen, then Meta.</summary>
        IEnumerator ReloadThroughTheMetaSave(PlayerMeta meta)
        {
            yield return null;
            string saved = SaveCodec.EncodeMeta(meta);
            Object.Destroy(_host);
            yield return null;
            yield return Boot();
            _ui.Meta = SaveCodec.DecodeMeta(saved).Meta;
            yield return null;
        }

        [UnityTest]
        public IEnumerator The_week_survives_leaving_the_game_through_the_meta_save()
        {
            var meta = new PlayerMeta();
            yield return Boot(s => { s.StudyXp = 100; s.Life.Energy = 70; });
            _ui.Meta = meta.Meta;   // the bootstrap's order: Initialize, then Meta
            Tap(Mode("study"));
            Tap(Focus("gig_resale"));
            Tap(Screen.Q<Button>("action-overtime"));
            int after = _ui.DecisionsPanel.EnergyAfterPlan;
            yield return ReloadThroughTheMetaSave(meta);
            Assert.AreEqual("study", _ui.DecisionsPanel.Mode);
            Assert.IsTrue(_ui.DecisionsPanel.ModeChosen);
            Assert.AreEqual("gig_resale", _ui.DecisionsPanel.Focus);
            Assert.IsTrue(_ui.DecisionsPanel.FocusChosen);
            Assert.IsTrue(_ui.DecisionsPanel.IsPlanned("overtime"));
            Assert.AreEqual(after, _ui.DecisionsPanel.EnergyAfterPlan);
            Assert.AreEqual(100, _ui.CurrentDecisions().WorkSplit["gig_resale"]);
        }

        [UnityTest]
        public IEnumerator A_stepped_lever_survives_leaving_the_game()
        {
            var meta = new PlayerMeta();
            yield return Boot(Owner);
            _ui.Meta = meta.Meta;
            Tap(Screen.Q<Button>("price-plus-coffee_cart"));
            long price = _ui.CurrentDecisions().Levers.Single(l => l.Lever == "price").ValueCents;
            yield return ReloadThroughTheMetaSave(meta);
            Assert.IsTrue(_ui.DecisionsPanel.LeverLocked("coffee_cart", "price"));
            Assert.IsTrue(Screen.Q<Button>("price-plus-coffee_cart").ClassListContains("lever-button--taken"));
            Assert.AreEqual(price, _ui.CurrentDecisions().Levers.Single(l => l.Lever == "price").ValueCents);
        }

        [UnityTest]
        public IEnumerator Plan_survives_a_reload_in_the_same_week()
        {
            yield return Boot(s => s.Life.Energy = 70);
            Tap(Mode("study"));
            Tap(Screen.Q<Button>("action-overtime"));
            yield return null;
            var snap = _ui.DecisionsPanel.Snapshot();
            int after = _ui.DecisionsPanel.EnergyAfterPlan;

            Object.Destroy(_host);   // a reload: a new screen over the same game
            yield return null;
            yield return Boot();
            _ui.DecisionsPanel.Restore(snap);
            yield return null;
            Assert.IsTrue(_ui.DecisionsPanel.IsPlanned("overtime"));
            Assert.AreEqual("study", _ui.DecisionsPanel.Mode);
            Assert.IsTrue(_ui.DecisionsPanel.ModeChosen);
            Assert.AreEqual(after, _ui.DecisionsPanel.EnergyAfterPlan, "the energy stays spent");
            Assert.IsFalse(Screen.Q<Button>("action-overtime").enabledSelf);
        }

        [UnityTest]
        public IEnumerator A_plan_from_another_week_is_ignored()
        {
            yield return Boot();
            var r = new WeekPlanRecord { Week = _state.Week + 1, Mode = "hustle", ModeChosen = true };
            r.Actions.Add("overtime");
            _ui.DecisionsPanel.Restore(r);
            Assert.IsFalse(_ui.DecisionsPanel.IsPlanned("overtime"));
            Assert.IsFalse(_ui.DecisionsPanel.ModeChosen);
        }
    }
}
