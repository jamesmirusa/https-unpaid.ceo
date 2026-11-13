#nullable enable
using NUnit.Framework;
using UnpaidCeo.Game.Services.Online;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Every game test runs as an unlinked project: tests never reach the real Unity Gaming Services.</summary>
    [SetUpFixture]
    public class OfflineTestRun
    {
        [OneTimeSetUp] public void Unlink() => OnlineBootstrap.CloudProjectIdOverride = "";
        [OneTimeTearDown] public void Relink() => OnlineBootstrap.CloudProjectIdOverride = null;
    }
}
