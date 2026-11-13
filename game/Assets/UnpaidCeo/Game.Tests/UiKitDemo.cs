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
using UnpaidCeo.Game.Ui.Kit;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>
    /// Not a check: renders the Week screen at 1080×1920 in the states a player meets (a fresh week, a week decided, the
    /// result) to TestResults/demo/kit_*.png, for review. Run by name:
    /// tools/unity.sh test-playmode UiKitDemo
    /// </summary>
    [Explicit, Category("Demo")]
    public class UiKitDemo
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
        public IEnumerator Radial_badges_calendar([Values("ru", "en")] string lang)
        {
            UiSound.Sink = _ => { };
            yield return Boot(lang, s => { Gigger(s); s.LifetimeEarnings = Money.FromDollars(100_000); s.Cash = Money.FromDollars(420); });
            _ui.PlaceTag("npc_tony_bulldog", "city.tony", new Vector2(600, 760));
            _ui.PlaceMarker("academy", new Vector2(250, 820));
            _ui.PlaceHeroTag(new Vector2(860, 800));
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot($"step2_{lang}_1_city_badges");
            _ui.OpenRadial("resident:npc_tony_bulldog", _ui.Root.Q("tag-npc_tony_bulldog"));
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot($"step2_{lang}_2_radial_tony");
            _ui.Radial.Close();
            _ui.OpenRadial("hero", _ui.HeroTag);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot($"step2_{lang}_3_radial_hero");
            _ui.Radial.Close();
            Tap((Button)_ui.Root.Q("calendar-cell-4"));
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot($"step2_{lang}_4_calendar_tip");
            UiSound.Sink = null;
        }

        [UnityTest]
        public IEnumerator Conversation([Values("ru", "en")] string lang)
        {
            UiSound.Sink = _ => { };
            yield return Boot(lang, s => { Gigger(s); s.Npcs.Add("tony", 28); s.StudyXp = 14; });
            _ui.Visit.Show("bodega");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot($"step3_{lang}_1_room_hearts");
            // the scene's panel as the dialogue draws it (same classes), with Tony's markup answers
            var back = new VisualElement();
            back.AddToClassList("dialogue-backdrop");
            var panel = new VisualElement();
            panel.AddToClassList("dialogue");
            panel.AddToClassList("theme-day");
            var plate = new VisualElement();
            plate.AddToClassList("dialogue__nameplate");
            var speaker = new Label(lang == "ru" ? "Тони" : "Tony");
            speaker.AddToClassList("dialogue__speaker");
            plate.Add(speaker);
            var hearts = new VisualElement();
            hearts.AddToClassList("dialogue__hearts");
            hearts.Add(Icons.Game("trust", "dialogue__heart-icon"));
            var hv = new Label("2");
            hv.AddToClassList("dialogue__hearts-value");
            hearts.Add(hv);
            plate.Add(hearts);
            panel.Add(plate);
            var text = new Label(lang == "ru" ? "Поставщик опять поднял цену. Сорок центов за пачку. А продаю я по доллару двадцать пять."
                                              : "Supplier raised me again. Forty cents a bag. I sell 'em for a buck twenty-five.");
            text.AddToClassList("dialogue__text");
            panel.Add(text);
            var options = new VisualElement();
            options.AddToClassList("dialogue__options");
            panel.Add(options);
            back.Add(panel);
            _ui.Root.Add(back);
            DialogueOptionsView.Build(options, new System.Collections.Generic.List<DialogueOptionsView.Option>
            {
                new DialogueOptionsView.Option { Text = lang == "ru" ? "Тяжёлая неделя. Помочь с коробками?" : "Rough week. Want a hand with the boxes?", Tone = "friendly",
                    Effects = new System.Collections.Generic.List<UnpaidCeo.Core.Narrative.Effect> { new UnpaidCeo.Core.Narrative.StatEffect("energy", -3) } },
                new DialogueOptionsView.Option { Text = lang == "ru" ? "Какая у тебя наценка после новой цены?" : "What's your markup after the new price?", Tone = "business", Locked = true, RequiredKnowledge = 30 },
                new DialogueOptionsView.Option { Text = lang == "ru" ? "Продавай по два доллара. Назови «ремесленные»." : "Charge two bucks. Call it artisanal.", Tone = "joke" },
            }, _ => { }, lang);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot($"step3_{lang}_2_answers");
            back.RemoveFromHierarchy();
            _ui.ShowLevelUp("tony", 3, "bonus.tony.3");
            yield return new WaitForSecondsRealtime(0.45f);
            yield return Shot($"step3_{lang}_3_level_up");
            UiSound.Sink = null;
        }

        [UnityTest]
        public IEnumerator The_new_look([Values("ru", "en")] string lang)
        {
            UiSound.Sink = _ => { };
            yield return Boot(lang, Gigger);
            yield return Shot($"kit_{lang}_1_city");

            _ui.Open(UiRoot.Decisions);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot($"kit_{lang}_2_week");
            Tap(Screen.Q<Button>("mode-study"));
            Tap(Screen.Q<Button>("action-overtime"));
            Tap(Screen.Q<Button>("action-loan_shark_loan"));
            yield return ScrollTo("actions-title");
            yield return Shot($"kit_{lang}_3_deeds");

            _ui.Open(UiRoot.City);
            _ui.Visit.Show("bodega");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot($"kit_{lang}_3b_room");
            _ui.Visit.Close();
            var report = _engine.EndWeek(_state, _ui.CurrentDecisions());
            _ui.ShowReport(report);
            _ui.ShowMonthSummary(report);
            yield return new WaitForSecondsRealtime(0.15f);
            yield return Shot($"kit_{lang}_4_result_moving");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot($"kit_{lang}_5_result");
            _ui.Open(UiRoot.Decisions);
            _ui.SetTheme(true);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot($"kit_{lang}_6_neon_week");
            UiSound.Sink = null;
        }
    }
}
