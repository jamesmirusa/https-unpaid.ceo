using NUnit.Framework;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Primitives
{
    public class Pcg32Tests
    {
        [Test]
        public void Pcg32_matches_reference_vector()
        {
            // pcg32 reference demo: pcg32_srandom_r(&rng, 42u, 54u)
            var r = new Pcg32(42, 54);
            uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };
            foreach (var e in expected) Assert.AreEqual(e, r.NextUInt());
        }

        [Test]
        public void Pcg32_state_roundtrip_continues_sequence()
        {
            var a = new Pcg32(7, 1);
            for (int i = 0; i < 10; i++) a.NextUInt();
            var b = Pcg32.FromState(a.State);
            for (int i = 0; i < 5; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void NextInt_and_NextDouble_stay_in_range()
        {
            var r = new Pcg32(1, 2);
            for (int i = 0; i < 10000; i++)
            {
                int n = r.NextInt(7);
                Assert.That(n, Is.InRange(0, 6));
                double d = r.NextDouble();
                Assert.That(d, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
            }
        }
    }
}
