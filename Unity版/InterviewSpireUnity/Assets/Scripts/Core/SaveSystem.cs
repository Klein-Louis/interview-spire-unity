using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace InterviewSpire
{
    /// <summary>多存档：命名存档 + 时间戳 + 按 runId 清理本局存档。</summary>
    public static class SaveSystem
    {
        public static string SaveDirOverride;

        public static string SaveDir
        {
            get
            {
                string dir = SaveDirOverride ?? Path.Combine(Application.persistentDataPath, "saves");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static bool Write(GameState state, string name, out string message)
        {
            if (state == null) { message = "没有可保存的对局"; return false; }
            try
            {
                string label = string.IsNullOrWhiteSpace(name) ? "未命名存档" : name.Trim();
                label = label.Substring(0, Math.Min(24, label.Length));
                foreach (char c in Path.GetInvalidFileNameChars()) label = label.Replace(c, '_');
                bool auto = label == "退出自动存档";
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string path = auto ? Path.Combine(SaveDir, "退出自动存档.json")
                    : Path.Combine(SaveDir, label + "_" + stamp + "_" + UnityEngine.Random.Range(100, 999) + ".json");
                state.saveName = label;
                state.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                File.WriteAllText(path, JsonUtility.ToJson(state, true), Encoding.UTF8);
                if (!auto) Prune(10);   // 手动存档最多保留最近 10 个
                message = path;
                return true;
            }
            catch (Exception ex) { message = ex.Message; return false; }
        }

        public static GameState Read(string path, out string error)
        {
            error = "";
            try
            {
                if (!File.Exists(path)) { error = "存档不存在"; return null; }
                var s = JsonUtility.FromJson<GameState>(File.ReadAllText(path, Encoding.UTF8));
                if (s == null || s.enemy == null) { error = "存档格式不正确"; return null; }
                return s;
            }
            catch (Exception ex) { error = ex.Message; return null; }
        }

        public static List<SaveRecord> List()
        {
            var result = new List<SaveRecord>();
            try
            {
                foreach (string path in Directory.GetFiles(SaveDir, "*.json"))
                {
                    string error;
                    var s = Read(path, out error);
                    if (s == null) continue;
                    result.Add(new SaveRecord
                    {
                        path = path, name = s.saveName, time = s.savedAt,
                        job = s.job, diff = s.diff, floor = s.floor + 1, runId = s.runId
                    });
                }
            }
            catch (Exception ex) { Debug.LogWarning("读取存档列表失败：" + ex.Message); }
            result.Sort((a, b) => string.CompareOrdinal(b.time, a.time));
            return result;
        }

        public static bool Delete(string path)
        {
            try
            {
                if (File.Exists(path)) { File.Delete(path); return true; }
            }
            catch (Exception ex) { Debug.LogWarning("删除存档失败：" + ex.Message); }
            return false;
        }

        private static void Prune(int keep)
        {
            var saves = List();
            for (int i = keep; i < saves.Count; i++)
            {
                if (saves[i].path == Path.Combine(SaveDir, "退出自动存档.json")) continue;
                Delete(saves[i].path);
            }
        }

        public static int DeleteForRun(long runId)
        {
            if (runId == 0) return 0;
            int count = 0;
            try
            {
                foreach (string path in Directory.GetFiles(SaveDir, "*.json"))
                {
                    string error;
                    var s = Read(path, out error);
                    if (s != null && s.runId == runId) { File.Delete(path); count++; }
                }
            }
            catch (Exception ex) { Debug.LogWarning("清理存档失败：" + ex.Message); }
            return count;
        }
    }
}
