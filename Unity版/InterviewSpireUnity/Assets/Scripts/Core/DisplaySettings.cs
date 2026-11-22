using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace InterviewSpire
{
    [Serializable]
    public class DisplayConfig
    {
        public bool fullScreen;
        public int width = 1240;
        public int height = 820;
    }

    /// <summary>窗口模式与分辨率持久化，路径在用户可写目录。</summary>
    public static class DisplaySettings
    {
        public static string Path
        {
            get { return System.IO.Path.Combine(Application.persistentDataPath, "display.json"); }
        }

        public static DisplayConfig Load()
        {
            try
            {
                if (!File.Exists(Path)) return new DisplayConfig();
                var c = JsonUtility.FromJson<DisplayConfig>(File.ReadAllText(Path, Encoding.UTF8));
                return c ?? new DisplayConfig();
            }
            catch { return new DisplayConfig(); }
        }

        public static void Apply(bool fullScreen, int width, int height, bool save = true)
        {
            if (width < 640) width = 1240;
            if (height < 480) height = 820;
            if (!Application.isEditor)
            {
                Screen.fullScreen = fullScreen;
                Screen.SetResolution(width, height, fullScreen);
            }
            if (save) Save(fullScreen, width, height);
        }

        public static void Save(bool fullScreen, int width, int height)
        {
            try
            {
                var c = new DisplayConfig { fullScreen = fullScreen, width = width, height = height };
                File.WriteAllText(Path, JsonUtility.ToJson(c, true), Encoding.UTF8);
            }
            catch (Exception ex) { Debug.LogWarning("显示设置保存失败：" + ex.Message); }
        }
    }
}
