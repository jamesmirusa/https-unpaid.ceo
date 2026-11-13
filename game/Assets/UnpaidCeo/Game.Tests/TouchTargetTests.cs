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
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Game.Ui;
using UnpaidCeo.Game.Ui.Kit;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>
    /// Stage 6: on an iPhone (1179×2556 px, 393 pt wide) every button the player taps is at least 44 pt on its short side
    /// — 121 panel px at the 1080-wide panel (1080 / 393 = 2.75 px per pt).
    /// </summary>
    public class TouchTargetTests
    {
        /// <summary>44 pt on the narrowest supported iPhone (375 pt: SE, mini) at the 1080-wide panel.</summary>
        public const float MinPanelPx = 44f * 1080f / 375f;

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

        IEnumerator Boot()
        {
            yield return LocalizationSettings.InitializationOperation;
            UiSound.Sink = _ => { };
            Haptics.Sink = _ => { };
            _target = new RenderTexture(1179, 2556, 24);
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            _engine = new GameEngine(Content());
            _state = _engine.NewGame(5, new HeroAppearance());
            _state.Facts.Set("met_mentor", 1);
            _state.Life.Energy = 30; _state.Facts.Set("shark_debt", 1); _state.LifetimeEarnings = Money.FromDollars(10_000);
            _ui.Initialize(_engine, _state, _target);
            yield return _ui.SetLanguage("ru");
            for (int i = 0; i < 6; i++) yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            UiSound.Sink = null;
            Haptics.Sink = null;
            if (_host != null) Object.Destroy(_host);
            if (_target != null) Object.Destroy(_target);
        }

        static bool Shown(VisualElement e)
        {
            for (var v = e; v != null; v = v.parent)
                if (v.resolvedStyle.display == DisplayStyle.None || v.resolvedStyle.visibility == Visibility.Hidden) return false;
            return true;
        }

        List<string> Small(VisualElement scope) =>
            scope.Query<Button>().ToList().Where(b => Shown(b) && b.worldBound.width > 1)
                .Where(b => Mathf.Min(b.layout.width, b.layout.height) < MinPanelPx - 0.5f)
                .Select(b => $"{(string.IsNullOrEmpty(b.name) ? string.Join(".", b.GetClasses()) : b.name)} {b.layout.width:0}×{b.layout.height:0}")
                .Distinct().ToList();

        IEnumerator Settle() { for (int i = 0; i < 6; i++) yield return null; }

        [UnityTest]
        public IEnumerator Panels_at_the_bottom_stay_above_the_home_bar()
        {
            yield return Boot();
            SafeArea.Apply(_ui.Root, new SafeArea.Edges { top = 140, bottom = 100 });
            _ui.Visit.Show("bodega");
            yield return Settle();
            float h = _ui.Root.panel.visualTree.layout.height;
            Assert.LessOrEqual(_ui.Visit.Root.worldBound.yMax, h - 100 + 1f, "the room's buttons clear the home bar");
            SafeArea.Apply(_ui.Root, new SafeArea.Edges { top = 140, bottom = 100 });   // applying twice adds nothing
            yield return Settle();
            Assert.LessOrEqual(_ui.Visit.Root.worldBound.yMax, h - 100 + 1f);
            Assert.Greater(_ui.Visit.Root.worldBound.yMax, h - 100 - 40f, "and do not float away");
        }

        [UnityTest]
        public IEnumerator Every_tap_on_the_main_screens_is_at_least_44_points()
        {
            yield return Boot();
            _ui.PlaceTag("npc_tony_bulldog", "city.tony", new Vector2(500, 1300));
            _ui.PlaceMarker("bodega", new Vector2(300, 1300));
            yield return Settle();
            var small = new List<string>();
            small.AddRange(Small(_ui.Root).Select(x => "city: " + x));
            _ui.Open(UiRoot.Decisions);
            yield return new WaitForSecondsRealtime(0.4f);
            small.AddRange(Small(_ui.Screen(UiRoot.Decisions)).Select(x => "week: " + x));
            _ui.Open(UiRoot.City);
            _ui.OpenRadial("resident:npc_tony_bulldog", _ui.Root.Q("tag-npc_tony_bulldog"));
            yield return Settle();
            small.AddRange(Small(_ui.Root.Q("radial")).Select(x => "radial: " + x));
            _ui.Radial.Close();
            _ui.Visit.Show("bodega");
            yield return Settle();
            small.AddRange(Small(_ui.Visit.Root).Select(x => "room: " + x));
            Assert.IsEmpty(small, "below 44 pt:\n" + string.Join("\n", small));
        }
    }
}
