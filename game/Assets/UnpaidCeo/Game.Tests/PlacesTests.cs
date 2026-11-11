using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
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
    /// Places of the block: Home, Tony's bodega, the pizza bar, the laundromat office, the pawnshop, the bank and the
    /// academy. Each has a host from the cast and things to do there that go into this month's plan.
    /// </summary>
    public class PlacesTests
    {
        GameObject _host;
        UiRoot _ui;
        RenderTexture _target;
        GameEngine _engine;
        GameState _state;
        readonly List<GameObject> _made = new List<GameObject>();

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
            foreach (var go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        VisualElement Panel => _ui.Screen(UiRoot.PlacesTab);

        [Test]
        public void Every_place_has_a_host_from_the_cast_and_things_the_engine_can_do()
        {
            var content = Content();
            Assert.GreaterOrEqual(Places.All.Count, 7);
            var offered = new HashSet<string>();
            foreach (var place in Places.All)
            {
                Assert.IsTrue(content.Npcs.ContainsKey(place.Host), $"{place.Id}: host '{place.Host}' is not in npcs.json");
                Assert.IsNotEmpty(place.Actions, place.Id + " has nothing to do");
                foreach (var a in place.Actions.Where(a => a.Kind == PlaceActionKind.Plan))
                {
                    Assert.IsTrue(content.FreeActions.ContainsKey(a.FreeAction), $"{place.Id}/{a.Id}: no free action '{a.FreeAction}'");
                    offered.Add(a.FreeAction);
                }
            }
            CollectionAssert.AreEquivalent(content.FreeActions.Keys, offered, "every free action of the month has its place in the city");
        }

        [Test]
        public void Places_sit_at_their_buildings()
        {
            Transform Building(string name, float x, float z)
            {
                var go = new GameObject(name);
                _made.Add(go);
                go.transform.position = new Vector3(x, 0f, z);
                return go.transform;
            }
            var home = Building("building_walkup", 11f, 13.5f);
            var all = new[] { home, Building("building_bodega", 21f, 13.5f), Building("building_pawn_shop(Clone)", -21f, 13.5f), Building("prop_taxi", 0f, 2f) };
            var spots = Places.FindSpots(all, home);

            Assert.IsTrue(spots.ContainsKey(Places.Home) && spots.ContainsKey("bodega") && spots.ContainsKey("pawnshop"));
            Assert.IsFalse(spots.ContainsKey("bank"), "no bank building in this district: no marker (the list of places still has it)");
            Assert.AreEqual(21f, spots["bodega"].x, 0.01f);
            Assert.That(spots["bodega"].z, Is.InRange(7.5f, 9.5f), "the marker hangs on the shop front, on the street side, where the camera sees it");
            Assert.That(spots["bodega"].y, Is.InRange(3.8f, 5.2f),
                "at shop-sign height: higher, the phone camera puts it under the HUD; lower, it covers the hero's and the mentor's tags");
        }

        [UnityTest]
        public IEnumerator The_dock_opens_the_block_and_a_card_opens_the_place()
        {
            yield return Boot();
            Tap(_ui.Root.Q<Button>("nav-places"));
            yield return null;
            Assert.AreEqual(UiRoot.PlacesTab, _ui.CurrentTab);
            Assert.AreEqual(Places.All.Count, Panel.Query<Button>(className: "place-card").ToList().Count(Shown), "one card per place");

            Tap(Panel.Q<Button>("place-card-bank"));
            yield return null;
            Assert.AreEqual("bank", _ui.PlacesPanel.CurrentPlace);
            Assert.AreEqual("Bank", Panel.Q<Label>("place-title").text);
            Assert.IsTrue(Panel.Q<Label>("place-host").text.Contains("Ms. Lee"), "the host is named");
            var line = Panel.Q<Label>("place-line");
            Assert.IsTrue(UiText.IsLocalized(line) && line.text.Length > 20 && !line.text.StartsWith("place."), "the host says something worth hearing");

            Tap(Panel.Q<Button>("places-back"));
            yield return null;
            Assert.IsNull(_ui.PlacesPanel.CurrentPlace);
            Assert.IsTrue(Shown(Panel.Q<Button>("place-card-home")));
        }

        [UnityTest]
        public IEnumerator A_marker_on_the_map_opens_its_place()
        {
            yield return Boot();
            _ui.PlaceMarker("bodega", new Vector2(500, 700));
            yield return null;
            var marker = _ui.Screen(UiRoot.City).Q<Button>("marker-bodega");
            Assert.IsNotNull(marker);
            Assert.IsTrue(Shown(marker) && UiText.IsLocalized(marker) && marker.text == "Tony's Bodega");

            Tap(marker);
            yield return null;
            Assert.AreEqual(UiRoot.PlacesTab, _ui.CurrentTab);
            Assert.AreEqual("bodega", _ui.PlacesPanel.CurrentPlace);

            _ui.Open(UiRoot.City);
            _ui.PlaceMarker("bodega", null);
            yield return null;
            Assert.IsFalse(Shown(marker), "off screen: no marker");
        }

        [UnityTest]
        public IEnumerator What_you_do_at_a_place_goes_into_this_months_plan()
        {
            yield return Boot();
            _ui.OpenPlace("bodega");
            yield return null;
            var overtime = Panel.Q<Button>("place-action-overtime");
            var action = _engine.Content.FreeActions["overtime"];
            var cash = action.Effects.OfType<UnpaidCeo.Core.Narrative.CashEffect>().Single().Amount;
            Assert.IsTrue(overtime.Query<Label>().ToList().Any(l => l.text == "+" + MoneyFormat.Compact(cash, "en")), "the button says what the work pays");
            Assert.IsTrue(overtime.Query<Label>().ToList().Any(l => l.text == "−10"), "and what it costs in energy");

            Tap(overtime);
            yield return null;
            CollectionAssert.Contains(_ui.CurrentDecisions().FreeActions, "overtime");
            Assert.IsTrue(overtime.ClassListContains("place-action--planned"));
            Assert.AreEqual($"{_ui.DecisionsPanel.PointsLeft} / {_ui.DecisionsPanel.PointsTotal}", Panel.Q<Label>("place-points").text);
            Assert.AreEqual(_ui.DecisionsPanel.PointsTotal - 1, _ui.DecisionsPanel.PointsLeft);

            Tap(overtime);
            yield return null;
            CollectionAssert.DoesNotContain(_ui.CurrentDecisions().FreeActions, "overtime", "a second tap takes it back out");
            Assert.IsFalse(overtime.ClassListContains("place-action--planned"));
        }

        [UnityTest]
        public IEnumerator Actions_stop_when_the_points_run_out()
        {
            yield return Boot();
            int total = _ui.DecisionsPanel.PointsTotal;
            Assert.GreaterOrEqual(total, 1);
            var costly = Places.All.SelectMany(p => p.Actions.Select(a => (place: p.Id, action: a)))
                .Where(x => x.action.Kind == PlaceActionKind.Plan && _engine.Content.FreeActions[x.action.FreeAction].ActionPoints == 1).ToList();
            Assert.Greater(costly.Count, total, "test setup: more one-point actions than points");
            for (int i = 0; i < total; i++)
            {
                _ui.OpenPlace(costly[i].place);
                yield return null;
                Tap(Panel.Q<Button>("place-action-" + costly[i].action.Id));
                yield return null;
            }
            _ui.OpenPlace(costly[total].place);
            yield return null;
            Assert.IsFalse(Panel.Q<Button>("place-action-" + costly[total].action.Id).enabledSelf, "no points left: the next action is off");
        }

        [UnityTest]
        public IEnumerator The_bank_lends_from_chapter_two()
        {
            yield return Boot();
            _ui.OpenPlace("bank");
            yield return null;
            Assert.IsFalse(Shown(Panel.Q<Button>("place-action-loan")), "chapter 1: the bank does not lend to a courier yet");
            TearDown();

            yield return Boot(s => { s.Chapter = 2; s.Cash = Money.FromCents(500_000); });
            _ui.OpenPlace("bank");
            yield return null;
            Tap(Panel.Q<Button>("place-action-loan"));
            yield return null;
            Assert.IsTrue(_ui.Loan.IsOpen, "chapter 2: the loan dialog opens");
        }

        [UnityTest]
        public IEnumerator The_office_leads_to_the_plan_and_the_academy_to_lessons()
        {
            yield return Boot();
            _ui.OpenPlace("office");
            yield return null;
            Tap(Panel.Q<Button>("place-action-plan"));
            yield return null;
            Assert.AreEqual(UiRoot.Decisions, _ui.CurrentTab);

            _ui.OpenPlace("academy");
            yield return null;
            Tap(Panel.Q<Button>("place-action-lessons"));
            yield return null;
            Assert.AreEqual(UiRoot.Academy, _ui.CurrentTab);
        }

        [UnityTest]
        public IEnumerator Every_place_has_its_picture()
        {
            foreach (var place in Places.All)
            {
                var find = Addressables.LoadResourceLocationsAsync(Places.ArtAddress(place.Id), typeof(Texture2D));
                yield return find;
                Assert.IsNotEmpty(find.Result, $"no picture for '{place.Id}' ({Places.ArtAddress(place.Id)})");
                Addressables.Release(find);
            }
        }

        /// <summary>
        /// The list of places shows seven cards: they get small thumbnails. The 4K picture (several MB each) is loaded
        /// only when the player walks in, or opening the list would download all seven on a phone.
        /// </summary>
        [UnityTest]
        public IEnumerator Cards_use_light_thumbnails_and_the_4k_picture_waits_for_the_visit()
        {
            Assert.AreNotEqual(Places.ArtAddress("bank"), Places.ThumbAddress("bank"));
            foreach (var place in Places.All)
            {
                var load = Addressables.LoadAssetAsync<Texture2D>(Places.ThumbAddress(place.Id));
                yield return load;
                Assert.AreEqual(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded, load.Status, "no thumbnail for " + place.Id);
                Assert.LessOrEqual(Mathf.Max(load.Result.width, load.Result.height), 1024, place.Id + ": a card thumbnail stays small");
                Addressables.Release(load);
            }
            var full = Addressables.LoadAssetAsync<Texture2D>(Places.ArtAddress("bank"));
            yield return full;
            Assert.GreaterOrEqual(full.Result.width, 4000, "inside a place the picture is 4K");
            Addressables.Release(full);
        }
    }
}
