using NUnit.Framework;

namespace UnpaidCeo.Core.Tests
{
    public class SmokeTests
    {
        [Test]
        public void Core_assembly_loads()
        {
            Assert.AreEqual("UnpaidCeo.Core", typeof(UnpaidCeo.Core.CoreAssembly).Assembly.GetName().Name);
        }
    }
}
