#nullable enable
using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace UnpaidCeo.Game.Services.Online
{
    /// <summary>
    /// Connects to Unity Gaming Services without ever holding the game up: an unlinked project, a failure, a timeout
    /// or Remote Config's <c>online_enabled</c> = false all give <see cref="OfflineServices"/>. Otherwise the player is
    /// signed in anonymously (reusing the cached session) and the flags are fetched.
    /// </summary>
    public static class OnlineBootstrap
    {
        struct NoAttributes { }

        /// <summary>
        /// Set to "" by the test assembly so no test ever talks to the real services (a linked project would otherwise
        /// sign in, sync cloud saves and call the server from inside the test run).
        /// </summary>
        public static string? CloudProjectIdOverride;

        /// <param name="cloudProjectId">null reads the override, then Application.cloudProjectId; "" means an unlinked project.</param>
        public static async Task<IOnlineServices> Start(float timeoutSeconds = 8, string? cloudProjectId = null)
        {
            var projectId = cloudProjectId ?? CloudProjectIdOverride ?? Application.cloudProjectId;
            if (string.IsNullOrEmpty(projectId)) return new OfflineServices();

            var connect = Connect();
            if (await Task.WhenAny(connect, Delay(timeoutSeconds)) != connect)
            {
                Debug.LogWarning($"[online] no answer within {timeoutSeconds}s; playing offline");
                return new OfflineServices();
            }
            return await connect;
        }

        /// <summary>A main-thread delay (Task.Delay needs threads, which the web player lacks).</summary>
        static async Task Delay(float seconds) => await Awaitable.WaitForSecondsAsync(seconds);

        static async Task<IOnlineServices> Connect()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                try { await AuthenticationService.Instance.GetPlayerInfoAsync(); }   // whether a username is linked
                catch (Exception e) { Debug.LogWarning("[online] player info: " + e.Message); }
                var flags = await Flags();
                return flags.Online ? new UgsOnlineServices(flags) : new OfflineServices(flags);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[online] unavailable, playing offline: " + e.Message);
                return new OfflineServices();
            }
        }

        /// <summary>Remote Config flags; if they cannot be fetched the defaults (everything on) apply.</summary>
        static async Task<OnlineFlags> Flags()
        {
            try
            {
                await RemoteConfigService.Instance.FetchConfigsAsync(new NoAttributes(), new NoAttributes());
                var c = RemoteConfigService.Instance.appConfig;
                return new OnlineFlags
                {
                    Online = c.GetBool("online_enabled", true),
                    Challenge = c.GetBool("challenge_enabled", true),
                    Duels = c.GetBool("duels_enabled", true),
                    MarketWars = c.GetBool("market_wars_enabled", true),
                };
            }
            catch (Exception e)
            {
                Debug.LogWarning("[online] flags: " + e.Message);
                return new OnlineFlags();
            }
        }
    }
}
