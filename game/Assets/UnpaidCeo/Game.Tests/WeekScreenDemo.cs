using System.Collections;
using System.IO;
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
    /// <summary>
    /// Not a check: renders the Week screen at 1080×1920 in the states a player meets (a fresh week, a week decided, the
    /// next week reopened, an owner's levers) to TestResults/demo/*.png, for review. Run by name:
    /// tools/unity.sh test-playmode WeekScreenDemo
    /// </summary>
    [Explicit, Category("Demo")]
    public class WeekScreenDemo
    {
        GameObject _host;
        UiRoot _ui;
        RenderTexture _target;
        GameEngine _engine;
        GameState _state;

        static string Dir => Path.Combine(Application.dataPath, "../TestResults/demo");

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

        IEnumerator Boot(string lang, System.Action<GameState> setup)
        {
            yield return LocalizationSettings.InitializationOperation;
            Directory.CreateDirectory(Dir);
            _target = new RenderTexture(1080, 1920, 24);
            _engine = new GameEngine(Content());
            _state = _engine.NewGame(5, new HeroAppearance());
            setup(_state);
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            _ui.Initialize(_engine, _state, _target);
            yield return _ui.SetLanguage(lang);
            _ui.Open(UiRoot.Decisions);
            for (int i = 0; i < 8; i++) yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            if (_target != null) Object.Destroy(_target);
        }

        VisualElement Screen => _ui.Screen(UiRoot.Decisions);

        static void Tap(Button b)
        {
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        IEnumerator ScrollTo(string name)
        {
            var scroll = Screen.Q<ScrollView>("decisions");
            var target = Screen.Q(name);
            for (int i = 0; i < 3; i++) yield return null;
            scroll.scrollOffset = new Vector2(0, Mathf.Max(0, target.layout.y - 12));
            for (int i = 0; i < 4; i++) yield return null;
        }

        IEnumerator ScrollToEnd()
        {
            var scroll = Screen.Q<ScrollView>("decisions");
            for (int i = 0; i < 3; i++) yield return null;
            scroll.scrollOffset = new Vector2(0, 100_000);
            for (int i = 0; i < 4; i++) yield return null;
        }

        IEnumerator Shot(string file)
        {
            for (int i = 0; i < 4; i++) yield return null;
            var prev = RenderTexture.active;
            RenderTexture.active = _target;
            var tex = new Texture2D(_target.width, _target.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(Dir, file + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        static void Gigger(GameState s)
        {
            s.Week = 6;
            s.Life.Energy = 78;
            s.StudyXp = 14;
            s.Cash = Money.FromDollars(640);
        }

        [UnityTest]
        public IEnumerator A_week_from_start_to_the_next([Values("ru", "en")] string lang)
        {
            yield return Boot(lang, Gigger);
            yield return Shot($"{lang}_1_new_week");

            Tap(Screen.Q<Button>("mode-study"));
            Tap(Screen.Q<Button>("focus-even"));
            Tap(Screen.Q<Button>("action-reserve_transfer"));
            Tap(Screen.Q<Button>("action-overtime"));
            Tap(Screen.Q<Button>("action-rest"));
            yield return Shot($"{lang}_2_decided_top");
            yield return ScrollTo("actions-title");
            yield return Shot($"{lang}_3_decided_deeds");
            yield return ScrollToEnd();
            yield return Shot($"{lang}_3b_decided_deeds_end");

            _ui.ShowReport(_engine.EndWeek(_state, _ui.CurrentDecisions()));
            _ui.Open(UiRoot.Decisions);
            for (int i = 0; i < 6; i++) yield return null;
            Screen.Q<ScrollView>("decisions").scrollOffset = Vector2.zero;
            yield return Shot($"{lang}_4_next_week_top");
            yield return ScrollToEnd();
            yield return Shot($"{lang}_5_next_week_deeds_end");
        }

        [UnityTest]
        public IEnumerator An_owner_steps_the_levers([Values("ru", "en")] string lang)
        {
            yield return Boot(lang, s =>
            {
                Gigger(s);
                s.Chapter = 2;
                s.Cash = Money.FromDollars(9_000);
                s.Businesses.Add(new BusinessState { Id = "coffee_cart", IndustryId = "coffee_cart", Price = Money.FromCents(400), Units = 1, Employees = 1 });
            });
            Tap(Screen.Q<Button>("mode-hustle"));
            Tap(Screen.Q<Button>("price-plus-coffee_cart"));
            Tap(Screen.Q<Button>("staff-plus-coffee_cart"));
            yield return Shot($"{lang}_6_owner_top");
            yield return ScrollTo("business-title");
            yield return Shot($"{lang}_7_owner_levers");
        }
    }
}
