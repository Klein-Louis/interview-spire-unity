#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace InterviewSpire.Editor
{
    /// <summary>按 UPM Client API 安装/校验包；不直接手改 manifest.json。</summary>
    public static class PackageInstaller
    {
        private static readonly string[] PackagesToAdd = { "com.unity.textmeshpro", "com.unity.test-framework" };
        private const double TimeoutSeconds = 300;
        private static AddAndRemoveRequest request;
        private static double deadline;

        [MenuItem("Tools/Interview Spire/Ensure Packages")]
        public static void Install()
        {
            Debug.Log("[PackageInstaller] Adding: " + string.Join(", ", PackagesToAdd));
            request = Client.AddAndRemove(PackagesToAdd, Array.Empty<string>());
            deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (request == null) return;
            if (!request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup > deadline)
                {
                    EditorApplication.update -= Poll;
                    Debug.LogError("[PackageInstaller] Timed out waiting for UPM.");
                    EditorApplication.Exit(2);
                }
                return;
            }
            EditorApplication.update -= Poll;
            if (request.Status == StatusCode.Success)
            {
                Debug.Log("[PackageInstaller] Resolved: " +
                          string.Join(", ", request.Result.Select(p => p.name + "@" + p.version)));
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[PackageInstaller] Failed: " + request.Error?.message);
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
