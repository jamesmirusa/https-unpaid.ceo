using NUnit.Framework;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Primitives
{
    public class StateHashTests
    {
        [Test]
        public void StateHash_is_stable_and_sensitive()
        {
            Assert.AreEqual(StateHash.Compute(new { b = 1, a = 2 }), StateHash.Compute(new { a = 2, b = 1 }));
            Assert.AreNotEqual(StateHash.Compute(new { a = 2 }), StateHash.Compute(new { a = 3 }));
            Assert.AreEqual(64, StateHash.Compute(new { a = 1 }).Length);
        }

        [Test]
        public void CanonicalJson_sorts_keys_and_has_no_whitespace()
        {
            Assert.AreEqual("{\"a\":2,\"b\":1.5}", CanonicalJson.Serialize(new { b = 1.5, a = 2 }));
        }
    }
}
