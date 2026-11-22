#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using TMPro;
using UnityEngine;

namespace InterviewSpire.Editor
{
    /// <summary>非交互导入 TMP Essential Resources，并等待资源真正落盘。</summary>
    public static class TmpResourcesSetup
    {
        private static double deadline;

        [MenuItem("Tools/Interview Spire/Import TMP Essentials")]
        public static void Import()
        {
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            string dir = "Assets/TextMesh Pro/Shaders";
            bool ready = Directory.Exists(dir) && Directory.GetFiles(dir, "*.shader").Length > 0;
            if (!ready && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            AssetDatabase.Refresh();
            if (ready)
            {
                Debug.Log("TMP ESSENTIALS IMPORTED");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("TMP ESSENTIALS IMPORT TIMEOUT");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
