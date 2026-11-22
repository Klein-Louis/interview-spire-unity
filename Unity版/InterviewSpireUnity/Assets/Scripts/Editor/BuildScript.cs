#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace InterviewSpire.Editor
{
    /// <summary>命令行一键构建：先跑自动验证，再导出 Windows 64 位程序并整目录覆盖桌面运行目录。</summary>
    public static class BuildScript
    {
        public static string OutputDir
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/InterviewSpire")); }
        }

        public static string Output
        {
            get { return Path.Combine(OutputDir, "InterviewSpire.exe"); }
        }

        public static string DesktopDir
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../..")); }
        }

        [MenuItem("Tools/Interview Spire/Build With Checks")]
        public static void PerformBuild()
        {
            BalanceSmoke.Run();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled)
                .Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new System.Exception("没有启用任何场景");

            // 整目录重建，避免 Unity 复用旧 Data 资源
            if (Directory.Exists(OutputDir)) Directory.Delete(OutputDir, true);
            Directory.CreateDirectory(OutputDir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception("构建失败：" + report.summary.result);

            CopyDirectory(OutputDir, DesktopDir);
            Debug.Log("BUILD OK: " + Output);
            Debug.Log("DESKTOP PLAYER UPDATED: " + DesktopDir);
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            foreach (string dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }
    }
}
#endif
