using System.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Life
{
    public class TimeRebalanceTests
    {
        [Test]
        public void Changing_one_share_rescales_the_others_to_keep_100()
        {
            var r = TimeAllocation.Rebalance(new[] { 55, 15, 20, 10 }, 0, 70);
            Assert.AreEqual(70, r[0]);
            Assert.AreEqual(100, r.Sum());
            Assert.IsTrue(r[1] <= 15 && r[2] <= 20 && r[3] <= 10, "others only shrink when one grows");
            Assert.IsTrue(r[2] >= r[1] && r[1] >= r[3], "proportions are kept");
        }

        [Test]
        public void Others_at_zero_share_the_remainder_equally()
        {
            CollectionAssert.AreEqual(new[] { 34, 33, 33, 0 }, TimeAllocation.Rebalance(new[] { 0, 0, 0, 100 }, 3, 0));
        }

        [Test]
        public void Value_is_clamped_and_random_edits_always_sum_100()
        {
            CollectionAssert.AreEqual(new[] { 100, 0, 0, 0 }, TimeAllocation.Rebalance(new[] { 25, 25, 25, 25 }, 0, 140));
            var rng = new Pcg32(9, 9);
            var v = new[] { 55, 15, 20, 10 };
            for (int i = 0; i < 500; i++)
            {
                v = TimeAllocation.Rebalance(v, rng.NextInt(4), rng.NextInt(121) - 10);
                Assert.AreEqual(100, v.Sum());
                Assert.IsTrue(v.All(x => x >= 0 && x <= 100));
            }
        }
    }
}
