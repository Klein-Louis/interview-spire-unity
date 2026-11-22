using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace InterviewSpire
{
    [Serializable] public class RunRecord
    {
        public string job, diff, time;
        public bool win;
        public int floor, turns, damage, cards;
    }

    [Serializable] public class StatsData
    {
        public List<RunRecord> runs = new List<RunRecord>();
    }

    /// <summary>跨局统计：只保存匿名汇总，不保存回答文本。</summary>
    public static class StatsSystem
    {
        public static string PathOverride;

        public static string Path
        {
            get { return PathOverride ?? System.IO.Path.Combine(Application.persistentDataPath, "stats.json"); }
        }

        public static StatsData Load()
        {
            try
            {
                if (!File.Exists(Path)) return new StatsData();
                return JsonUtility.FromJson<StatsData>(File.ReadAllText(Path, Encoding.UTF8)) ?? new StatsData();
            }
            catch { return new StatsData(); }
        }

        public static void Record(GameState s, bool won)
        {
            if (s == null) return;
            var d = Load();
            d.runs.Add(new RunRecord
            {
                job = s.job, diff = s.diff, time = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                win = won, floor = s.floor + 1, turns = s.totalTurns,
                damage = s.totalDamage, cards = s.totalCards
            });
            if (d.runs.Count > 100) d.runs.RemoveRange(0, d.runs.Count - 100);
            try { File.WriteAllText(Path, JsonUtility.ToJson(d, true), Encoding.UTF8); }
            catch (Exception ex) { Debug.LogWarning("统计写入失败：" + ex.Message); }
        }
    }
}
