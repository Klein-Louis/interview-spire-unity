#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace InterviewSpire.Editor
{
    /// <summary>阶段三：用简单策略批量自动对局，输出通关率与节奏数据。</summary>
    public static class BalanceSimulator
    {
        private enum Policy { Good, Average, Skip }

        public static void Run()
        {
            GameDatabase.Load();
            GameDatabase.LoadImported();
            StatsSystem.PathOverride = Path.Combine(Path.GetTempPath(), "interviewspire_sim_stats.json");
            SaveSystem.SaveDirOverride = Path.Combine(Path.GetTempPath(), "interviewspire_sim_saves");

            foreach (Policy p in Enum.GetValues(typeof(Policy)))
                Simulate(p, 100);

            Debug.Log("BALANCE SIM DONE");
        }

        private static void Simulate(Policy policy, int runs)
        {
            int wins = 0, totalTurns = 0, totalDamage = 0, lowScores = 0, starUses = 0;
            for (int r = 0; r < runs; r++)
            {
                var go = new GameObject("Sim_" + policy + "_" + r);
                var game = go.AddComponent<GameController>();
                game.Job = "软件开发";
                game.Difficulty = "标准";
                game.NewGame();
                int guard = 0;
                while (game.State.phase != 3 && guard++ < 500)
                {
                    var s = game.State;
                    if (s.phase == 0)
                    {
                        foreach (int uid in s.hand.Select(c => c.uid).ToList())
                        {
                            if (s.phase != 0) break;
                            game.PlayCard(uid);
                        }
                        if (s.phase != 0) continue;
                        if (!s.starUsedThisTurn)
                        {
                            if (policy == Policy.Skip) game.SkipStar();
                            else
                            {
                                var report = game.UseStar(BuildAnswer(policy, s.currentQuestionId));
                                if (report != null)
                                {
                                    starUses++;
                                    if (report.score < 60) lowScores++;
                                }
                            }
                        }
                        if (s.phase == 0) game.EndTurn();
                    }
                    else if (s.phase == 1) game.SimulateEnemyAct();
                    else if (s.phase == 2)
                    {
                        var keys = game.RandomRewardKeys(3);
                        game.TakeReward(keys.Count > 0 ? keys[0] : "");
                    }
                    else break;
                }
                var records = StatsSystem.Load().runs;
                if (records.Count > 0 && records[records.Count - 1].win) wins++;
                totalTurns += game.State.totalTurns;
                totalDamage += game.State.totalDamage;
                UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log(string.Format(
                "SIM {0,-7} win={1}/{2} ({3:F0}%) avgTurns={4:F1} avgDamage={5:F1} lowScorePerRun={6:F2}",
                policy, wins, runs, wins * 100f / runs, totalTurns / (float)runs,
                totalDamage / (float)runs, lowScores / (float)runs));
        }

        private static string BuildAnswer(Policy policy, string questionId)
        {
            var meta = GameDatabase.Data.question_meta.Find(m => m.id == questionId);
            if (meta == null || meta.keyPoints.Count == 0) return "当时的情况是我负责处理，最终完成并提升了效率。";
            if (policy == Policy.Good)
            {
                return "当时的背景是项目优化，我的目标是提升效果。我负责核心方案，" +
                       string.Join("和", meta.keyPoints.Take(3).Select(k => k.term)) +
                       "都做了验证，最终上线后耗时从200ms降到50ms，提升30%。";
            }
            return "当时的情况是" + string.Join("和", meta.keyPoints.Take(2).Select(k => k.term)) +
                   "，用小时级数据做了说明，效果还可以。";
        }
    }
}
#endif
