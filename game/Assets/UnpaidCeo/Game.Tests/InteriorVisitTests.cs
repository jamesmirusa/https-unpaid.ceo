using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Game.City;
using UnpaidCeo.Game.Hero;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>
    /// Walking into a place: a tap on its sign takes the hero inside a 3D room where the host stands facing them, and a
    /// conversation with choices runs at the bottom of the screen (the host's line, the place's actions, talk, leave).
    /// </summary>
    public class InteriorVisitTests
    {
        GameObject _host;
        UiRoot _ui;
        RenderTexture _target;
        GameEngine _engine;
        GameState _state;
        readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

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
            foreach (var go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
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

        VisualElement Visit => _ui.Root.Q("visit");

        // ---------- what each place is made of ----------

        [Test]
        public void Every_place_has_a_3D_room_and_hosts_with_a_model_use_it()
        {
            string prefabs = Path.Combine(Application.dataPath, "UnpaidCeo/Art/Prefabs/Interiors");
            foreach (var place in Places.All)
            {
                Assert.AreEqual("interior_" + place.Id, Interiors.RoomAddress(place.Id));
                Assert.IsTrue(File.Exists(Path.Combine(prefabs, Interiors.RoomAddress(place.Id) + ".prefab")), place.Id + " has no room prefab");
                var model = Interiors.HostModel(place.Host);
                if (model != null)
                    Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "UnpaidCeo/Art/Prefabs", model + ".prefab")), $"{place.Host}: no prefab {model}");
            }
            Assert.AreEqual("npc_tony_bulldog", Interiors.HostModel("tony"));
            Assert.AreEqual("npc_mr_fins_shark", Interiors.HostModel("loan_shark"));
            Assert.AreEqual("npc_jay_otter", Interiors.HostModel("best_friend"));
            Assert.AreEqual("npc_mentor_raccoon", Interiors.HostModel("mentor"));
        }

        [Test]
        public void The_layout_faces_host_and_hero_and_the_camera_sees_both_above_the_dialogue()
        {
            var cam = new GameObject("cam").AddComponent<Camera>();
            _made.Add(cam.gameObject);
            cam.aspect = 1080f / 1920f;
            var layout = InteriorLayout.For(new Bounds(new Vector3(0f, 2f, 0f), new Vector3(10f, 4f, 10f)));
            cam.fieldOfView = layout.FieldOfView;
            cam.transform.position = layout.CameraPosition;
            cam.transform.LookAt(layout.LookAt);

            Assert.Greater(Vector3.Dot(layout.HeroFacing * Vector3.forward, (layout.HostSpot - layout.HeroSpot).normalized), 0.5f, "the hero looks at the host");
            Assert.Greater(Vector3.Dot(layout.HostFacing * Vector3.forward, (layout.HeroSpot - layout.HostSpot).normalized), 0.5f, "the host looks at the hero");
            foreach (var spot in new[] { layout.HeroSpot, layout.HostSpot })
            {
                Assert.AreEqual(0f, spot.y, 1e-4f, "both stand on the floor");
                foreach (var h in new[] { 0.1f, HeroView.Height })
                {
                    var v = cam.WorldToViewportPoint(spot + Vector3.up * h);
                    Assert.That(v.x, Is.InRange(0.08f, 0.92f), "inside the screen's width");
                    Assert.That(v.y, Is.InRange(0.42f, 0.95f), "above the conversation panel at the bottom");
                    Assert.Greater(v.z, 0f);
                }
            }
        }

        [UnityTest]
        public IEnumerator The_stage_puts_both_on_the_floor_far_from_the_city_and_gives_the_city_camera_back()
        {
            var city = new GameObject("city cam").AddComponent<Camera>();
            _made.Add(city.gameObject);
            var room = GameObject.CreatePrimitive(PrimitiveType.Cube);
            room.transform.localScale = new Vector3(10f, 0.2f, 10f);
            var hero = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var stageGo = new GameObject("stage");
            _made.Add(stageGo);
            var stage = stageGo.AddComponent<InteriorStage>();
            stage.Show(room, hero, npc, city);
            yield return null;

            Assert.IsFalse(city.enabled, "inside, the city camera rests");
            Assert.IsTrue(stage.Camera.enabled);
            Assert.Less(stage.transform.position.y, -100f, "the room is built far from the street");
            Assert.AreEqual(stage.transform.position.y, ModelFit.RendererBounds(hero).min.y, 0.02f, "the hero stands on the floor");
            Assert.AreEqual(stage.transform.position.y, ModelFit.RendererBounds(npc).min.y, 0.02f, "the host stands on the floor");
            Assert.AreEqual(HeroView.Height, ModelFit.RendererBounds(npc).size.y, 0.05f, "people keep the street's scale");
            Assert.Greater(Vector3.Dot(npc.transform.forward, (hero.transform.position - npc.transform.position).normalized), 0.5f);

            stage.Clear();
            yield return null;
            Assert.IsTrue(city.enabled, "outside again, the city camera is back");
            Assert.IsTrue(room == null && hero == null && npc == null, "the room and its people are gone");
        }

        [UnityTest]
        public IEnumerator The_two_stand_at_the_rooms_talk_spot_when_it_has_one()
        {
            var room = GameObject.CreatePrimitive(PrimitiveType.Cube);
            room.transform.localScale = new Vector3(10f, 0.2f, 10f);
            var root = new GameObject("room");
            room.transform.SetParent(root.transform, false);
            var spot = new GameObject(Interiors.TalkSpot).transform;
            spot.SetParent(root.transform, false);
            spot.localPosition = new Vector3(-2f, 0f, 1.5f);   // where the builder found clear floor
            var stageGo = new GameObject("stage");
            _made.Add(stageGo);
            var stage = stageGo.AddComponent<InteriorStage>();
            stage.Show(root, GameObject.CreatePrimitive(PrimitiveType.Capsule), GameObject.CreatePrimitive(PrimitiveType.Capsule), null);
            yield return null;
            var between = (stage.Hero.transform.position + stage.Host.transform.position) * 0.5f;
            Assert.AreEqual(spot.position.x, between.x, 0.05f, "they meet at the talk spot");
            Assert.AreEqual(spot.position.z, between.z, 0.05f);
            var expected = InteriorLayout.At(new Vector3(-2f, 0f, 1.5f));
            Assert.AreEqual(expected.CameraPosition.x, stage.Layout.CameraPosition.x, 1e-3f, "and the camera frames them there");
        }

        [UnityTest]
        public IEnumerator Inside_the_streets_tilt_shift_blur_is_off_and_it_comes_back_outside()
        {
            var stageGo = new GameObject("stage");
            _made.Add(stageGo);
            var stage = stageGo.AddComponent<InteriorStage>();
            stage.Show(GameObject.CreatePrimitive(PrimitiveType.Cube), null, null, null);
            yield return null;
            var volume = stage.GetComponentInChildren<Volume>();
            Assert.IsNotNull(volume, "the room brings its own look");
            Assert.IsTrue(volume.isGlobal && volume.enabled && volume.priority > 10f, "over the district's day and night volumes");
            Assert.IsTrue(volume.profile.TryGet<DepthOfField>(out var dof) && dof.mode.overrideState && dof.mode.value == DepthOfFieldMode.Off,
                "the street's blur is tuned for a camera 40 m up and smears a room seen from 18 m");
            stage.Clear();
            yield return null;
            Assert.IsFalse(volume.enabled, "outside, the street looks as before");
        }

        // ---------- walking in ----------

        [UnityTest]
        public IEnumerator A_sign_walks_into_the_building_when_3D_visits_are_on()
        {
            yield return Boot();
            string entered = null;
            _ui.PlaceEnterRequested += id => entered = id;
            _ui.Enter3D = true;
            _ui.PlaceMarker("bank", new Vector2(500, 900));
            yield return null;
            Tap(_ui.Root.Q<Button>("marker-bank"));
            Assert.AreEqual("bank", entered, "the sign asks the city to walk the hero in");
            Assert.AreEqual(UiRoot.City, _ui.CurrentTab, "no flat popup: the visit happens in 3D");

            _ui.Enter3D = false;
            Tap(_ui.Root.Q<Button>("marker-bank"));
            Assert.AreEqual(UiRoot.PlacesTab, _ui.CurrentTab, "without 3D the place opens as before");
        }

        // ---------- the conversation ----------

        [UnityTest]
        public IEnumerator The_visit_shows_the_host_line_and_the_places_actions_as_choices()
        {
            yield return Boot();
            _ui.Visit.Show("bodega");
            yield return null;
            Assert.IsTrue(Shown(Visit), "the conversation panel is up");
            Assert.IsTrue(_ui.Root.ClassListContains("visiting"), "the street's widgets step aside");
            Assert.AreEqual("Tony", _ui.Root.Q<Label>("visit-speaker").text);
            var line = _ui.Root.Q<Label>("visit-line");
            Assert.IsTrue(UiText.IsLocalized(line) && line.text.Length > 10 && !line.text.StartsWith("place."));
            var overtime = _ui.Root.Q<Button>("visit-action-overtime");
            Assert.IsTrue(Shown(overtime));
            Assert.IsTrue(overtime.Query(className: "chip").ToList().Count >= 2, "the choice says what it pays and costs");
            Assert.IsTrue(Shown(_ui.Root.Q<Button>("visit-talk")));
            Assert.IsTrue(Shown(_ui.Root.Q<Button>("visit-exit")));
            Assert.IsTrue(_ui.Gate.PlaceVisited, "walking in counts as dropping by");
        }

        [UnityTest]
        public IEnumerator A_choice_plans_the_action_and_the_host_answers()
        {
            yield return Boot();
            string chosen = null;
            _ui.Visit.ActionChosen += a => chosen = a;
            _ui.Visit.Show("bodega");
            yield return null;
            Tap(_ui.Root.Q<Button>("visit-action-overtime"));
            yield return null;
            Assert.AreEqual("overtime", chosen);
            CollectionAssert.Contains(_ui.CurrentDecisions().FreeActions, "overtime");
            var line = _ui.Root.Q<Label>("visit-line");
            Assert.IsTrue(UiText.IsLocalized(line) && !line.text.StartsWith("place."), "the host answers the choice");
            Assert.IsTrue(_ui.Root.Q<Button>("visit-action-overtime").ClassListContains("visit__choice--planned"));
            Assert.IsTrue(Shown(_ui.Root.Q("visit-consequences")), "and what it brings is shown");

            Tap(_ui.Root.Q<Button>("visit-action-overtime"));
            yield return null;
            CollectionAssert.DoesNotContain(_ui.CurrentDecisions().FreeActions, "overtime", "a second tap takes it back");
        }

        [UnityTest]
        public IEnumerator Talk_moves_through_the_hosts_lines()
        {
            yield return Boot();
            _ui.Visit.Show("bank");
            yield return null;
            var line = _ui.Root.Q<Label>("visit-line");
            string first = line.text;
            Tap(_ui.Root.Q<Button>("visit-talk"));
            yield return null;
            Assert.AreNotEqual(first, line.text, "talking brings the next line");
        }

        [UnityTest]
        public IEnumerator Leaving_closes_the_visit_and_an_open_action_leaves_for_its_screen()
        {
            yield return Boot();
            int left = 0;
            _ui.Visit.Exited += () => left++;
            _ui.Visit.Show("bodega");
            yield return null;
            Tap(_ui.Root.Q<Button>("visit-exit"));
            yield return null;
            Assert.AreEqual(1, left);
            Assert.IsFalse(Shown(Visit));
            Assert.IsFalse(_ui.Root.ClassListContains("visiting"));

            _ui.Visit.Show("office");
            yield return null;
            Tap(_ui.Root.Q<Button>("visit-action-plan"));
            yield return null;
            Assert.AreEqual(2, left, "an action that opens a screen leaves the room first");
            Assert.AreEqual(UiRoot.Decisions, _ui.CurrentTab);
        }
    }
}
