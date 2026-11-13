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
using UnpaidCeo.Game.Narrative;
using UnpaidCeo.Game.Ui;
using UnpaidCeo.Game.Ui.Kit;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>
    /// Stage 5B step 3 · conversations: "Talk" asks Core for the scene waiting at the host (else an everyday line by
    /// trust), "!" shows over a host while a scene waits, answers carry tone icons in fixed places, their price and a
    /// knowledge lock, hearts show trust, and reaching a level is a short skippable moment.
    /// </summary>
    public class ConversationUiTests
    {
        GameObject _host;
        UiRoot _ui;
        RenderTexture _target;
        GameEngine _engine;
        GameState _state;
        readonly List<string> _scenes = new List<string>();

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
            UiSound.Sink = _ => { };
            Haptics.Sink = _ => { };
            _scenes.Clear();
            _target = new RenderTexture(1080, 1920, 24);
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            _engine = new GameEngine(Content());
            _state = _engine.NewGame(5, new HeroAppearance());
            _state.Facts.Set("met_mentor", 1);
            setup?.Invoke(_state);
            _ui.Initialize(_engine, _state, _target);
            _ui.SceneRequested += st => _scenes.Add(st.Id);
            yield return _ui.SetLanguage("en");
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

        static string Text(Button b) => b.Q<Label>("option-text").text;

        static void Tap(Button b)
        {
            Assert.IsNotNull(b, "button missing");
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        [UnityTest]
        public IEnumerator Talking_to_Tony_starts_the_scene_waiting_at_him()
        {
            yield return Boot();
            _ui.Visit.Show("bodega");
            yield return null;
            Tap(_ui.Visit.Root.Q<Button>("visit-talk"));
            CollectionAssert.AreEqual(new[] { "ch1_tony_intro" }, _scenes);
            CollectionAssert.Contains(_state.PendingStorylets, "ch1_tony_intro", "Core made it pending (and logged it)");
        }

        [UnityTest]
        public IEnumerator After_his_scene_of_the_week_Tony_says_an_everyday_line_by_trust()
        {
            yield return Boot();
            _ui.Visit.Show("bodega");
            yield return null;
            Tap(_ui.Visit.Root.Q<Button>("visit-talk"));
            _engine.ChooseStoryletOption(_state, "ch1_tony_intro", "help");
            Tap(_ui.Visit.Root.Q<Button>("visit-talk"));
            Assert.AreEqual(1, _scenes.Count, "one story scene a week");
            StringAssert.StartsWith("live.tony." + Relationships.Tier(_state.Npcs.Get("tony")) + ".", _ui.Visit.LineKey);
        }

        [UnityTest]
        public IEnumerator An_exclamation_floats_over_Tony_while_a_scene_waits()
        {
            yield return Boot();
            _ui.PlaceTag("npc_tony_bulldog", "city.tony", new Vector2(500, 1100));
            yield return null;
            var badge = _ui.Root.Q("tag-box-npc_tony_bulldog").Q("emote");
            Assert.IsTrue(badge.ClassListContains("emote--emote_scene"), "\"!\" — a scene waits");
            _ui.Talk("tony");
            _engine.ChooseStoryletOption(_state, "ch1_tony_intro", "help");
            _ui.RefreshHud();
            Assert.IsFalse(badge.ClassListContains("emote--emote_scene"));
        }

        [UnityTest]
        public IEnumerator The_hero_can_call_Mom_from_their_menu()
        {
            yield return Boot();
            _ui.PlaceHeroTag(new Vector2(500, 1100));
            yield return null;
            _ui.OpenRadial("hero", _ui.HeroTag);
            var call = _ui.Root.Q<Button>("radial-call_mom");
            Assert.IsNotNull(call);
            Tap(call);
            CollectionAssert.AreEqual(new[] { "ch1_mom_call" }, _scenes);
        }

        [UnityTest]
        public IEnumerator The_room_shows_the_hosts_hearts()
        {
            yield return Boot(s => s.Npcs.Add("tony", 35));
            _ui.Visit.Show("bodega");
            yield return null;
            var hearts = _ui.Visit.Root.Q<Label>("visit-hearts");
            Assert.AreEqual("3", hearts.text);
            Assert.AreEqual(DisplayStyle.Flex, hearts.parent.resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator Answers_carry_tone_icons_in_fixed_places_prices_and_knowledge_locks()
        {
            yield return Boot();
            var box = new VisualElement();
            _ui.Root.Add(box);
            int picked = -1;
            var options = new List<DialogueOptionsView.Option>
            {
                new DialogueOptionsView.Option { Text = "Charge two bucks.", Tone = "joke" },
                new DialogueOptionsView.Option { Text = "What's your markup?", Tone = "business", RequiredKnowledge = 30, Locked = true },
                new DialogueOptionsView.Option { Text = "Want a hand?", Tone = "friendly", Effects = new List<Effect> { new StatEffect("energy", -3) } },
            };
            DialogueOptionsView.Build(box, options, i => picked = i, "en");
            var buttons = box.Query<Button>(className: "dialogue__option").ToList();
            Assert.AreEqual(3, buttons.Count);
            Assert.IsTrue(buttons.Single(b => Text(b) == "Want a hand?").ClassListContains("dialogue__option--friendly"));
            Assert.AreEqual("friendly", DialogueOptionsView.ToneOrder[0], "friendly top-left");
            Assert.AreEqual("business", DialogueOptionsView.ToneOrder[1], "business top-right");
            Assert.AreEqual("joke", DialogueOptionsView.ToneOrder[2], "joke bottom-left");
            Assert.AreEqual("firm", DialogueOptionsView.ToneOrder[3], "firm bottom-right");
            Assert.AreEqual("Want a hand?", Text(buttons[0]), "answers sit in their tone's place");
            var locked = buttons.Single(b => Text(b) == "What's your markup?");
            Assert.IsFalse(locked.enabledSelf, "a knowledge answer is shown, locked");
            Assert.IsNotNull(locked.Q(name: "option-requires"), "with its requirement");
            Assert.IsNotNull(buttons.Single(b => Text(b) == "Want a hand?").Q(className: "chip"), "the price is on the button");
            foreach (var b in buttons) Assert.IsNotNull(b.Q(className: "game-icon"), "each answer has its tone icon");
            Tap(buttons.Single(b => Text(b) == "Charge two bucks."));
            Assert.AreEqual(0, picked, "the pick reports the option's original index");
        }

        [UnityTest]
        public IEnumerator Reaching_a_level_is_a_short_moment_a_tap_skips()
        {
            yield return Boot();
            _ui.ShowLevelUp("tony", 3, "bonus.tony.3");
            var moment = _ui.Root.Q("level-up");
            Assert.AreEqual(DisplayStyle.Flex, moment.resolvedStyle.display);
            Assert.AreEqual("3", moment.Q<Label>("level-up-level").text);
            Assert.IsTrue(UiText.IsLocalized(moment.Q<Label>("level-up-bonus")));
            using (var e = ClickEvent.GetPooled()) { e.target = moment; moment.SendEvent(e); }
            yield return null;
            Assert.AreEqual(DisplayStyle.None, moment.resolvedStyle.display, "a tap skips it");

            _ui.ShowLevelUp("mom", 3, "bonus.mom.3");
            yield return new WaitForSecondsRealtime(LevelUpMoment.Seconds + 0.3f);
            Assert.AreEqual(DisplayStyle.None, moment.resolvedStyle.display, "it leaves by itself in 1.5 s");
        }

        [UnityTest]
        public IEnumerator A_week_that_reaches_a_level_plays_the_moment()
        {
            yield return Boot();
            var r = new MonthReport { IsWeek = true, Week = 1 };
            r.LevelUps.Add(new LevelUp { Npc = "tony", Level = 3, Key = "bonus.tony.3" });
            _ui.ShowReport(r);
            _ui.ShowMonthSummary(r);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(DisplayStyle.None, _ui.Root.Q("level-up").resolvedStyle.display, "it waits until the week's result is read");
            Tap(_ui.Root.Q<Button>("month-summary-ok"));
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(DisplayStyle.Flex, _ui.Root.Q("level-up").resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator A_level_reached_in_a_scene_waits_for_the_scene_to_close()
        {
            yield return Boot();
            _ui.SetSceneOpen(true);
            _ui.ShowLevelUp("tony", 3, "bonus.tony.3");
            yield return null;
            Assert.AreEqual(DisplayStyle.None, _ui.Root.Q("level-up").resolvedStyle.display, "never behind the dialogue");
            _ui.SetSceneOpen(false);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(DisplayStyle.Flex, _ui.Root.Q("level-up").resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator Nobody_talks_in_a_challenge_run()
        {
            yield return Boot();
            _ui.RunMode = true;
            Assert.IsFalse(_ui.Talk("tony"));
            Assert.IsFalse(_state.Log.Any(l => l.Kind == DecisionLogEntry.TalkTo), "a talk would make the server refuse the run");
        }
    }
}
