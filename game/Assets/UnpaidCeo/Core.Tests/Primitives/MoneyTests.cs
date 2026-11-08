using NUnit.Framework;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Primitives
{
    public class MoneyTests
    {
        [Test]
        public void MulRound_rounds_half_away_from_zero()
        {
            Assert.AreEqual(3, Money.FromCents(5).MulRound(0.5).Cents);
            Assert.AreEqual(-3, Money.FromCents(-5).MulRound(0.5).Cents);
            Assert.AreEqual(450, Money.FromDollars(4.50m).Cents);
        }

        [Test]
        public void Arithmetic_and_comparison()
        {
            var a = Money.FromCents(1000);
            var b = Money.FromCents(250);
            Assert.AreEqual(1250, (a + b).Cents);
            Assert.AreEqual(750, (a - b).Cents);
            Assert.AreEqual(-1000, (-a).Cents);
            Assert.IsTrue(b < a);
            Assert.IsTrue(a > b);
            Assert.AreEqual(Money.FromCents(250), b);
            Assert.AreEqual(0, Money.Zero.Cents);
        }
    }
}
