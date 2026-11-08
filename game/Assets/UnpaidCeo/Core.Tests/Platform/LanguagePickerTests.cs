using NUnit.Framework;
using UnpaidCeo.Core.Platform;

namespace UnpaidCeo.Core.Tests.Platform
{
    public class LanguagePickerTests
    {
        static readonly string[] Available = { "en", "ru" };

        [TestCase(null, "ru-RU", "ru")]
        [TestCase(null, "Russian", "ru")]
        [TestCase(null, "en-US", "en")]
        [TestCase(null, "de-DE", "en")]
        [TestCase(null, "", "en")]
        [TestCase("ru", "en-US", "ru")]
        [TestCase("xx", "ru-RU", "ru")]
        [TestCase(null, "RU", "ru")]
        public void Saved_choice_wins_then_device_language_then_english(string saved, string device, string expected) =>
            Assert.AreEqual(expected, LanguagePicker.Pick(saved, device, Available));
    }
}
