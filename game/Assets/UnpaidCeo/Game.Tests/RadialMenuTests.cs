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
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Game.Ui;
using UnpaidCeo.Game.Ui.Kit;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>
    /// Stage 5B step 2: tap a person, a place sign or the hero → one radial menu (≤ 6 items, no nesting, closed items
    /// say why, irreversible ones ask twice); badges over heads; the month's money dates in the HUD.
    /// </summary>
    public class RadialMenuTests
    {
        GameObject _host;
        UiRoot _ui;
        RenderTexture _target;
        GameEngine _engine;
        GameState _state;
        readonly List<string> _sounds = new List<string>();

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

        IEnumerator Boot(System.Action<GameState> setup = null, string lang = "en")
        {
            yield return LocalizationSettings.InitializationOperation;
            _sounds.Clear();
            UiSound.Sink = _sounds.Add;
            Haptics.Sink = _ => { };
            _target = new RenderTexture(1080, 1920, 24);
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            _engine = new GameEngine(Content());
            _state = _engine.NewGame(5, new HeroAppearance());
            _state.Life.Energy = 80;
            setup?.Invoke(_state);
            _ui.Initialize(_engine, _state, _target);
            yield return _ui.SetLanguage(lang);
            for (int i = 0; i < 5; i++) yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            UiSound.Sink = null;
            Haptics.Sink = null;
            if (_host != null) Object.Destroy(_host);
            if (_target != null) Object.Destroy(_target);
        }

        static void Click(VisualElement e)
        {
            Assert.IsNotNull(e);
            using (var ev = ClickEvent.GetPooled()) { ev.target = e; e.SendEvent(ev); }
        }

        static void Tap(Button b)
        {
            Assert.IsNotNull(b, "button missing");
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        static void PointerDown(VisualElement e)
        {
            using (var ev = PointerDownEvent.GetPooled()) { ev.target = e; e.SendEvent(ev); }
        }

        VisualElement Menu => _ui.Root.Q("radial");
        List<Button> Petals => Menu.Query<Button>(className: "radial__petal").ToList();
        Button Petal(string id) => Menu.Q<Button>("radial-" + id);
        Label Chip => Menu.Q<Label>("radial-chip");

        IEnumerator OpenTony()
        {
            _ui.PlaceTag("npc_tony_bulldog", "city.tony", new Vector2(500, 1000));
            yield return null;
            Click(_ui.Root.Q<Label>("tag-npc_tony_bulldog"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tapping_Tony_opens_one_radial_menu_with_talk_first_and_his_deeds()
        {
            yield return Boot();
            yield return OpenTony();
            Assert.AreEqual(DisplayStyle.Flex, Menu.resolvedStyle.display);
            var petals = Petals;
            Assert.LessOrEqual(petals.Count, RadialMenu.MaxItems, "never more than six");
            Assert.AreEqual("radial-talk", petals[0].name, "what waits comes first");
            Assert.IsNotNull(Petal("overtime"));
            Assert.IsNotNull(Petal("night_shift"));
            foreach (var p in petals) Assert.IsNotNull(p.Q(className: "game-icon") ?? p.Q(className: "icon"), p.name + " has an icon");
            Assert.AreEqual(1, _ui.Root.Query(className: "radial").ToList().Count, "one menu");
            foreach (var p in petals) Assert.AreEqual(0, p.Query(className: "radial__petal").ToList().Count(x => x != p), "no nested petals");
        }

        [UnityTest]
        public IEnumerator An_open_deed_is_taken_in_one_tap_and_the_menu_closes()
        {
            yield return Boot();
            yield return OpenTony();
            Tap(Petal("overtime"));
            yield return null;
            Assert.IsTrue(_ui.DecisionsPanel.IsPlanned("overtime"), "the same plan as the cards (one ChoiceState)");
            Assert.AreEqual(DisplayStyle.None, Menu.resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator A_closed_item_says_why_and_changes_nothing()
        {
            yield return Boot(s => s.Life.Energy = 5);   // too tired for a night shift
            yield return OpenTony();
            var night = Petal("night_shift");
            Assert.IsFalse(night.enabledSelf);
            PointerDown(night);
            Assert.AreEqual(ChoiceCard.StatusKey(_ui.DecisionsPanel.State("night_shift")) != null, Chip.text.Length > 0);
            Assert.IsTrue(Chip.ClassListContains("radial__chip--closed"), "the chip shows the reason");
            Assert.IsFalse(_ui.DecisionsPanel.IsPlanned("night_shift"));
            CollectionAssert.Contains(_sounds, UiSound.Closed);
        }

        [UnityTest]
        public IEnumerator Pressing_an_open_item_shows_its_price_and_effect_first()
        {
            yield return Boot();
            yield return OpenTony();
            PointerDown(Petal("overtime"));
            var chips = Menu.Q("radial-chip-effects");
            Assert.Greater(chips.childCount, 0, "money and energy show before the tap lands");
        }

        [UnityTest]
        public IEnumerator The_loan_shark_asks_twice()
        {
            yield return Boot();
            _ui.PlaceMarker("pawnshop", new Vector2(500, 1000));
            yield return null;
            Tap(_ui.Root.Q<Button>("marker-pawnshop"));
            yield return null;
            Assert.AreEqual("radial-enter", Petals[0].name, "a place sign offers to walk in first");
            var loan = Petal("loan_shark_loan");
            Tap(loan);
            Assert.IsFalse(_ui.DecisionsPanel.IsPlanned("loan_shark_loan"), "the first tap only asks");
            Assert.IsTrue(loan.ClassListContains("radial__petal--armed"));
            Tap(loan);
            Assert.IsTrue(_ui.DecisionsPanel.IsPlanned("loan_shark_loan"));
        }

        [UnityTest]
        public IEnumerator Walking_in_from_a_sign_opens_the_place()
        {
            yield return Boot();
            _ui.PlaceMarker("bodega", new Vector2(500, 1000));
            yield return null;
            Tap(_ui.Root.Q<Button>("marker-bodega"));
            yield return null;
            Tap(Petal("enter"));
            yield return null;
            Assert.AreEqual(UiRoot.PlacesTab, _ui.CurrentTab);
            Assert.AreEqual("bodega", _ui.PlacesPanel.CurrentPlace);
        }

        [UnityTest]
        public IEnumerator The_hero_offers_things_for_themselves()
        {
            yield return Boot();
            _ui.PlaceHeroTag(new Vector2(500, 1100));
            yield return null;
            Click(_ui.HeroTag);
            yield return null;
            Assert.IsNotNull(Petal("rest"));
            Assert.IsNotNull(Petal("study"));
            Assert.IsNotNull(Petal("family_time"));
            Assert.LessOrEqual(Petals.Count, RadialMenu.MaxItems);
        }

        [UnityTest]
        public IEnumerator The_host_in_a_room_opens_the_rooms_menu()
        {
            yield return Boot();
            _ui.Visit.Show("bodega");
            yield return null;
            Click(_ui.Visit.Root.Q("visit-portrait"));
            yield return null;
            Assert.AreEqual(DisplayStyle.Flex, Menu.resolvedStyle.display);
            Assert.IsNotNull(Petal("overtime"));
            Tap(Petal("overtime"));
            yield return null;
            Assert.IsTrue(_ui.DecisionsPanel.IsPlanned("overtime"));
            Assert.IsFalse(_ui.Visit.Root.Q<Button>("visit-action-overtime").enabledSelf, "the room's card shows it is done");
        }

        [UnityTest]
        public IEnumerator Tapping_beside_the_menu_closes_it()
        {
            yield return Boot();
            yield return OpenTony();
            Click(Menu.Q("radial-backdrop"));
            yield return null;
            Assert.AreEqual(DisplayStyle.None, Menu.resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator A_work_badge_floats_over_Tony_while_his_work_is_open()
        {
            yield return Boot();
            _ui.PlaceTag("npc_tony_bulldog", "city.tony", new Vector2(500, 1000));
            yield return null;
            var badge = _ui.Root.Q("tag-box-npc_tony_bulldog").Q("emote");
            Assert.IsNotNull(badge);
            Assert.AreEqual(DisplayStyle.Flex, badge.resolvedStyle.display, "\"$\": there is work");
            _ui.DecisionsPanel.Plan("overtime");
            _ui.DecisionsPanel.Plan("rest");   // the week's two points are spent
            yield return null;
            Assert.AreEqual(DisplayStyle.None, badge.resolvedStyle.display, "no points left, no work badge");
        }

        // ---------- the money calendar ----------

        [UnityTest]
        public IEnumerator The_hud_shows_the_months_four_weeks_with_their_money_dates()
        {
            yield return Boot(s => { s.LifetimeEarnings = Money.FromDollars(100_000); s.Week = 2; s.Cash = Money.FromDollars(5_000); });
            var cells = _ui.Root.Query(className: "calendar__cell").ToList();
            Assert.AreEqual(4, cells.Count);
            Assert.IsTrue(cells[1].ClassListContains("calendar__cell--now"));
            Assert.IsTrue(cells[0].ClassListContains("calendar__cell--past"));
            var cal = MoneyCalendar.Month(_state, _engine.Content.Balance);
            Assert.AreEqual(EffectChips.Minus + MoneyFormat.Compact(cal[3].Total, "en"), cells[3].Q<Label>("calendar-amount-4").text);
            Assert.IsTrue(cells[3].ClassListContains("calendar__cell--ok"));
        }

        [UnityTest]
        public IEnumerator A_week_the_cash_cannot_cover_is_red_and_a_tap_explains_the_cash_gap()
        {
            yield return Boot(s => { s.LifetimeEarnings = Money.FromDollars(100_000); s.Week = 1; s.Cash = Money.FromDollars(50); });
            var rent = _ui.Root.Q("calendar-cell-4");
            Assert.IsTrue(rent.ClassListContains("calendar__cell--gap"));
            Tap((Button)rent);
            var toast = _ui.Root.Q<Label>("toast");
            Assert.IsNotNull(toast, "one line about the cash gap");
            Assert.IsTrue(UiText.IsLocalized(toast));
        }
    }
}
