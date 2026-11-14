using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;
using UnityEngine;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// The Deployment window's Deploy, from a command: deploys everything in Assets/UnpaidCeo/Server/Deploy (the Cloud
    /// Code module reference, leaderboards, Remote Config) to the Editor's current environment, signed in with the owner's
    /// Unity account (the Editor handles the sign-in; nothing is typed or stored). Run: tools/server/deploy-from-editor.sh
    /// </summary>
    public static class ServerDeploy
    {
        public const string Folder = "Assets/UnpaidCeo/Server/Deploy";

        public static void DeployAll()
        {
            // an Editor started from the command line gets the Hub sign-in a little later: wait for it (only checks that
            // the account is there; the token itself is never read out or stored)
            double until = EditorApplication.timeSinceStartup + 90;
            void WaitSignIn()
            {
                bool signedIn = !string.IsNullOrEmpty(CloudProjectSettings.userId) && !string.IsNullOrEmpty(CloudProjectSettings.accessToken);
                if (!signedIn && EditorApplication.timeSinceStartup < until) return;
                EditorApplication.update -= WaitSignIn;
                if (!signedIn) { Debug.LogError("[deploy] not signed in: open the project from Unity Hub once, then retry"); EditorApplication.Exit(10); return; }
                Debug.Log("[deploy] signed in");
                PickEnvironment();
            }
            EditorApplication.update += WaitSignIn;
        }

        static void PickEnvironment()
        {
            // pick the environment (production unless UC_UGS_ENV says otherwise) the way the Environments selector does
            string env = System.Environment.GetEnvironmentVariable("UC_UGS_ENV");
            if (string.IsNullOrEmpty(env)) env = "production";
            var envs = Unity.Services.Core.Editor.Environments.EnvironmentsApi.Instance;
            var refresh = envs.RefreshAsync();
            void AfterRefresh()
            {
                if (!refresh.IsCompleted) return;
                EditorApplication.update -= AfterRefresh;
                if (refresh.IsFaulted) { Debug.LogError("[deploy] environments: " + refresh.Exception?.GetBaseException().Message); EditorApplication.Exit(8); return; }
                try { envs.SetActiveEnvironment(env); }
                catch (System.Exception e) { Debug.LogError("[deploy] environment '" + env + "': " + e.Message); EditorApplication.Exit(9); return; }
                Debug.Log("[deploy] environment set: " + envs.ActiveEnvironmentName);
                OpenAndRun();
            }
            EditorApplication.update += AfterRefresh;
        }

        static void OpenAndRun()
        {
            // the API works only while the Deployment window is open: open it, give it a few editor ticks, then deploy
            Deployments.Instance.DeploymentWindow?.OpenWindow();
            int ticks = 0;
            void Wait()
            {
                if (++ticks < 30) return;
                EditorApplication.update -= Wait;
                Run();
            }
            EditorApplication.update += Wait;
        }

        static void Run()
        {
            var window = Deployments.Instance.DeploymentWindow;
            if (window == null) { Debug.LogError("[deploy] the Deployment window API is not ready"); EditorApplication.Exit(2); return; }
            var paths = Directory.GetFiles(Folder).Where(f => !f.EndsWith(".meta")).Select(f => f.Replace('\\', '/')).ToList();
            List<IDeploymentItem> items;
            try { items = window.GetFromFiles(paths).Where(i => i != null).ToList(); }
            catch (System.Exception e) { Debug.LogError("[deploy] " + e.Message); EditorApplication.Exit(7); return; }
            Debug.Log($"[deploy] environment {Deployments.Instance.EnvironmentProvider?.Current}: {items.Count} items: " + string.Join(", ", items.Select(i => i.Name)));
            if (items.Count == 0) { Debug.LogError("[deploy] nothing to deploy"); EditorApplication.Exit(3); return; }
            var task = window.Deploy(items);
            double deadline = EditorApplication.timeSinceStartup + 600;
            void Poll()
            {
                if (!task.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
                EditorApplication.update -= Poll;
                if (!task.IsCompleted) { Debug.LogError("[deploy] timed out"); EditorApplication.Exit(4); return; }
                if (task.IsFaulted) { Debug.LogError("[deploy] failed: " + task.Exception?.GetBaseException().Message); EditorApplication.Exit(5); return; }
                bool bad = false;
                foreach (var i in items)
                {
                    Debug.Log($"[deploy] {i.Name}: {i.Status.Message}");
                    if (i.Status.MessageSeverity == SeverityLevel.Error) bad = true;
                }
                EditorApplication.Exit(bad ? 6 : 0);
            }
            EditorApplication.update += Poll;
        }
    }
}
