using NUnit.Framework;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Format
{
    public class MoneyFormatTests
    {
        [TestCase(123456789, "en", "$1,234,567.89")]
        [TestCase(123456789, "ru", "1 234 567,89 $")]
        [TestCase(-35000, "en", "−$350.00")]
        [TestCase(-35000, "ru", "−350,00 $")]
        [TestCase(0, "en", "$0.00")]
        [TestCase(5, "en", "$0.05")]
        [TestCase(-5, "ru", "−0,05 $")]
        public void Full(long cents, string lang, string expected) =>
            Assert.AreEqual(expected, MoneyFormat.Full(Money.FromCents(cents), lang));

        [TestCase(120000000, "en", "$1.2M")]
        [TestCase(120000000, "ru", "1,2 млн $")]
        [TestCase(1530000, "en", "$15.3K")]
        [TestCase(1530000, "ru", "15,3 тыс. $")]
        [TestCase(95000, "en", "$950")]
        [TestCase(950000, "en", "$9,500")]
        [TestCase(950000, "ru", "9 500 $")]
        [TestCase(100000000000, "en", "$1B")]
        [TestCase(100000000000, "ru", "1 млрд $")]
        [TestCase(-35000, "en", "−$350")]
        [TestCase(99995000, "en", "$1M")]
        [TestCase(0, "en", "$0")]
        public void Compact(long cents, string lang, string expected) =>
            Assert.AreEqual(expected, MoneyFormat.Compact(Money.FromCents(cents), lang));

        [Test]
        public void Unknown_language_falls_back_to_english() =>
            Assert.AreEqual("$1,000.00", MoneyFormat.Full(Money.FromDollars(1000), "xx"));
    }
}
