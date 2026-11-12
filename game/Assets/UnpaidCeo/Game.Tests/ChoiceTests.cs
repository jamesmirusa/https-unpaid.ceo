using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>
    /// Choices in a room, the New2NYC way: each says whether it can be taken this week and why not, the room shows
    /// the energy the week's plan leaves and the actions left, and the HUD's energy drops the moment a choice is made.
    /// </summary>
    public class ChoiceTests
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

        Button Choice(string id) => _ui.Root.Q<Button>("visit-action-" + id);
        Label Status(string id) => Named("visit-status-" + id);

        Label Named(string name)
        {
            var l = _ui.Root.Q<Label>(name);
            Assert.IsNotNull(l, name + " missing");
            return l;
        }
        static string Localized(string key) => LocalizationSettings.StringDatabase.GetLocalizedString(UiRoot.Table, key);

        [UnityTest]
        public IEnumerator A_tired_hero_cannot_take_the_night_work_and_is_told_to_rest_which_is_recommended_at_home()
        {
            yield return Boot(s => s.Life.Energy = 5);
            _ui.Visit.Show("bodega");
            yield return null;
            Assert.IsFalse(Choice("overtime").enabledSelf, "10 energy for the truck, 5 left");
            Assert.IsTrue(Shown(Status("overtime")));
            Assert.AreEqual(Localized("choice.no_energy"), Status("overtime").text);
            _ui.Visit.Close();

            _ui.Visit.Show("home");
            yield return null;
            Assert.IsTrue(Choice("rest").enabledSelf);
            Assert.IsTrue(Shown(Status("rest")));
            Assert.AreEqual(Localized("choice.recommended"), Status("rest").text);
            Assert.IsTrue(Choice("rest").ClassListContains("visit__choice--recommended"));
        }

        [UnityTest]
        public IEnumerator The_room_and_the_hud_show_the_energy_the_plan_leaves_the_moment_a_choice_is_made()
        {
            yield return Boot(s => s.Life.Energy = 70);
            _ui.Visit.Show("bodega");
            yield return null;
            int points = _ui.DecisionsPanel.PointsTotal;
            Assert.AreEqual("70", Named("visit-energy").text);
            Assert.AreEqual($"{points} / {points}", Named("visit-points").text);
            Assert.AreEqual("70", Named("hud-energy-value").text);
            Assert.IsFalse(Shown(Status("overtime")), "an ordinary open choice needs no note");

            Tap(Choice("overtime"));
            yield return null;
            Assert.AreEqual("70 → 60", Named("visit-energy").text);
            Assert.AreEqual($"{points - 1} / {points}", Named("visit-points").text);
            Assert.AreEqual("70 → 60", Named("hud-energy-value").text, "the hud follows the plan at once");
            Assert.AreEqual(Localized("choice.planned"), Status("overtime").text);

            Tap(Choice("overtime"));
            yield return null;
            Assert.AreEqual("70", Named("hud-energy-value").text, "taking it back gives the energy back");
        }

        [UnityTest]
        public IEnumerator A_choice_done_last_week_is_open_again_and_the_week_has_its_own_points()
        {
            yield return Boot(s =>
            {
                s.Week = 2;   // the second week of month 1
                s.ActionsThisTurn.Add("overtime");   // taken last week
                s.ActionPointsLeft = 0;
            });
            Assert.AreEqual(2, _ui.DecisionsPanel.PointsTotal, "a fresh pool every week");
            _ui.Visit.Show("bodega");
            yield return null;
            Assert.IsTrue(Choice("overtime").enabledSelf, "last week's choice is open again");
        }

        /// <summary>
        /// New2NYC's activities brought to the block: each place gets one more trade-off, every one priced in energy,
        /// with its words in English and Russian (the button, the host's answer, the journal line, the plan's row).
        /// </summary>
        [UnityTest]
        public IEnumerator The_block_offers_the_new_choices_each_priced_in_energy()
        {
            yield return LocalizationSettings.InitializationOperation;
            var content = Content();
            var added = new[] { ("bodega", "night_shift"), ("bar", "pitch_night"), ("bank", "budget_review"), ("academy", "certificate_course") };
            foreach (var (place, id) in added)
            {
                Assert.IsTrue(System.Linq.Enumerable.Any(Places.Find(place).Actions, a => a.Kind == PlaceActionKind.Plan && a.FreeAction == id), $"{place} offers {id}");
                Assert.IsTrue(content.FreeActions.TryGetValue(id, out var fa), id + " in free_actions.json");
                Assert.AreEqual(1, fa.ActionPoints, id);
                Assert.Greater(ActionChoices.EnergyCost(fa), 0, id + " takes energy");
            }
            long Pay(string id) => System.Linq.Enumerable.Sum(System.Linq.Enumerable.OfType<UnpaidCeo.Core.Narrative.CashEffect>(content.FreeActions[id].Effects), c => c.Amount.Cents);
            Assert.Greater(Pay("night_shift"), Pay("overtime"), "the night shift pays more than the truck");
            Assert.Greater(ActionChoices.EnergyCost(content.FreeActions["night_shift"]), ActionChoices.EnergyCost(content.FreeActions["overtime"]), "and takes more out of you");

            foreach (var lang in new[] { "en", "ru" })
            {
                yield return LocalizationSettings.SelectedLocaleAsync;
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(lang);
                yield return LocalizationSettings.InitializationOperation;
                foreach (var (_, id) in added)
                    foreach (var key in new[] { "place.action." + id, "place.react." + id, "action." + id, content.FreeActions[id].JournalKey })
                    {
                        var text = Localized(key);
                        Assert.IsFalse(string.IsNullOrEmpty(text) || text.StartsWith("No translation"), $"{lang}: {key}");
                    }
            }
        }

        [UnityTest]
        public IEnumerator After_a_hard_night_shift_is_planned_home_recommends_rest()
        {
            yield return Boot(s => s.Life.Energy = 45);
            _ui.Visit.Show("home");
            yield return null;
            Assert.IsFalse(Shown(Status("rest")), "45 energy: no nudge yet");
            _ui.Visit.Close();

            _ui.Visit.Show("bodega");
            yield return null;
            Tap(Choice("night_shift"));
            yield return null;
            _ui.Visit.Close();

            _ui.Visit.Show("home");
            yield return null;
            Assert.IsTrue(Shown(Status("rest")), "45 − 16 = 29 after the plan");
            Assert.AreEqual(Localized("choice.recommended"), Status("rest").text);
        }

        [UnityTest]
        public IEnumerator A_paid_choice_is_closed_without_the_money()
        {
            yield return Boot(s => s.Cash = Money.Zero);
            _ui.Visit.Show("bar");
            yield return null;
            Assert.IsFalse(Choice("networking").enabledSelf, "pizza for the right people costs money");
            Assert.AreEqual(Localized("choice.no_money"), Status("networking").text);
        }
    }
}
