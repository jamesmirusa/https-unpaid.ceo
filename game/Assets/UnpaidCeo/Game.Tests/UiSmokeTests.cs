using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Stage-1 UI on a phone-sized panel (1080×1920): tabs, time sliders, RU overflow, disclaimer, table-driven text.</summary>
    public class UiSmokeTests
    {
        GameObject _host;
        UiRoot _ui;
        RenderTexture _target;

        static ContentDb Content()
        {
            string dir = Path.Combine(Application.dataPath, "UnpaidCeo/Content");
            string Read(string f) { var p = Path.Combine(dir, f); return File.Exists(p) ? File.ReadAllText(p) : null; }
            return ContentDb.FromJson(new ContentFiles
            {
                Industries = Read("industries.json"), Balance = Read("balance.json"), FreeActions = Read("free_actions.json"),
                Storylets = Read("storylets_ch1.json"), Npcs = Read("npcs.json"), Lessons = Read("lessons.json"),
            });
        }

        IEnumerator Boot(string language)
        {
            yield return LocalizationSettings.InitializationOperation;
            _target = new RenderTexture(1080, 1920, 24);
            _host = new GameObject("ui");
            _ui = _host.AddComponent<UiRoot>();
            var engine = new GameEngine(Content());
            var state = engine.NewGame(5, new HeroAppearance());
            engine.TutorialGig(state);
            var report = engine.EndMonth(state, MonthDecisions.Default(state.Businesses[0].Id));
            _ui.Initialize(engine, state, _target);
            _ui.ShowReport(report);
            yield return _ui.SetLanguage(language);
            for (int i = 0; i < 10; i++) yield return null;
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
                if (v.resolvedStyle.display == DisplayStyle.None || !v.visible) return false;
            return true;
        }

        /// <summary>Text elements actually on screen (no hidden ancestor).</summary>
        static IEnumerable<TextElement> Texts(VisualElement root) => root.Query<TextElement>().Where(Shown).ToList();

        [UnityTest]
        public IEnumerator All_tabs_open_without_errors([Values("en", "ru")] string language)
        {
            yield return Boot(language);
            foreach (var tab in UiRoot.Tabs)
            {
                _ui.Open(tab);
                for (int i = 0; i < 3; i++) yield return null;
                Assert.AreEqual(tab, _ui.CurrentTab);
                Assert.AreEqual(DisplayStyle.Flex, _ui.Screen(tab).resolvedStyle.display, tab + " is not shown");
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Time_sliders_always_sum_100()
        {
            yield return Boot("en");
            _ui.Open(UiRoot.Decisions);
            yield return null;
            var control = _ui.Root.Q<TimeAllocationControl>();
            Assert.IsNotNull(control);
            var rng = new System.Random(3);
            for (int i = 0; i < 200; i++)
            {
                control.SetShare(rng.Next(4), rng.Next(0, 101));
                Assert.AreEqual(100, control.Values.Sum());
            }
            var d = _ui.CurrentDecisions();
            Assert.AreEqual(100, d.Work + d.Family + d.Rest + d.Study);
        }

        [UnityTest]
        public IEnumerator No_text_overflow_in_ru()
        {
            yield return Boot("ru");
            var overflow = new List<string>();
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "../TestResults"));
            foreach (var tab in UiRoot.Tabs)
            {
                _ui.Open(tab);
                for (int i = 0; i < 5; i++) yield return null;
                foreach (var t in Texts(_ui.Root))
                {
                    if (string.IsNullOrEmpty(t.text) || t.resolvedStyle.whiteSpace == WhiteSpace.Normal) continue;
                    var size = t.MeasureTextSize(t.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                    if (size.x > t.contentRect.width + 1f)
                        overflow.Add($"{tab}/{t.name}: '{t.text}' needs {size.x:0} px, has {t.contentRect.width:0}");
                }
                SaveScreenshot(Path.Combine(Application.dataPath, $"../TestResults/ui_ru_{tab}.png"));
            }
            Assert.IsEmpty(overflow, string.Join("\n", overflow));
        }

        void SaveScreenshot(string path)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = _target;
            var tex = new Texture2D(_target.width, _target.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator Disclaimer_visible_on_company_screen()
        {
            yield return Boot("ru");
            _ui.Open(UiRoot.Company);
            for (int i = 0; i < 3; i++) yield return null;
            var disclaimer = _ui.Screen(UiRoot.Company).Q<Label>("company-disclaimer");
            Assert.IsNotNull(disclaimer);
            Assert.AreEqual(DisplayStyle.Flex, disclaimer.resolvedStyle.display);
            StringAssert.Contains("финансовый совет", disclaimer.text);
        }

        [UnityTest]
        public IEnumerator No_hardcoded_strings()
        {
            yield return Boot("en");
            var hardcoded = new List<string>();
            foreach (var tab in UiRoot.Tabs)
            {
                _ui.Open(tab);
                for (int i = 0; i < 3; i++) yield return null;
                foreach (var t in Texts(_ui.Root))
                {
                    if (string.IsNullOrEmpty(t.text) || t.ClassListContains(UiRoot.ValueClass)) continue;
                    if (!UiText.IsLocalized(t))
                        hardcoded.Add($"{tab}/{t.name} ({t.GetType().Name}): '{t.text}'");
                }
            }
            Assert.IsEmpty(hardcoded, "text must come from a LocalizedString binding or carry the ." + UiRoot.ValueClass + " class:\n" + string.Join("\n", hardcoded));
        }
    }
}
