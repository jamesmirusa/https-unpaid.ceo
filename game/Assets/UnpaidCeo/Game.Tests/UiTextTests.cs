using System.Collections;
using NUnit.Framework;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>
    /// Localised text must arrive without UI Toolkit's data-binding pass: in the first web build every
    /// bound label stayed blank while directly set values rendered.
    /// </summary>
    public class UiTextTests
    {
        [UnityTest]
        public IEnumerator Bound_label_gets_its_text_from_the_string_table_without_a_panel()
        {
            yield return LocalizationSettings.InitializationOperation;
            yield return LocalizationSettings.SelectedLocaleAsync;
            var en = LocalizationSettings.AvailableLocales.GetLocale("en");
            LocalizationSettings.SelectedLocale = en;
            var label = UiText.Bind(new Label(), "hud.next_month");
            for (int i = 0; i < 10 && string.IsNullOrEmpty(label.text); i++) yield return null;
            Assert.AreEqual("Next month", label.text);
            Assert.IsTrue(UiText.IsLocalized(label));
        }

        [UnityTest]
        public IEnumerator Rebinding_switches_to_the_new_key()
        {
            yield return LocalizationSettings.InitializationOperation;
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
            var button = UiText.Bind(new Button(), "onboarding.gig");
            UiText.Bind(button, "onboarding.continue");
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual("Got it", button.text, "the old key must not overwrite the new one");
        }
    }
}
