#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InterviewSpire.Editor
{
    /// <summary>首次打开工程时自动创建 Main.unity 并加入构建列表。</summary>
    [InitializeOnLoad]
    public static class ProjectBootstrap
    {
        static ProjectBootstrap()
        {
            EditorApplication.delayCall += EnsureScene;
        }

        public static void EnsureScene()
        {
            const string path = "Assets/Scenes/Main.unity";
            if (!File.Exists(path))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, path);
                AssetDatabase.Refresh();
                Debug.Log("已自动创建场景：" + path);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        }
    }
}
#endif
