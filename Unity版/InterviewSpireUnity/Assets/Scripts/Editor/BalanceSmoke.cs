#if UNITY_EDITOR
using System;
using System.Linq;
using System.IO;
using UnityEngine;

namespace InterviewSpire.Editor
{
    /// <summary>针对评审中 4 个 P0 的自动验证。</summary>
    public static class BalanceSmoke
    {
        private static void Check(bool ok, string label)
        {
            if (!ok) throw new Exception("BALANCE FAIL: " + label);
            Debug.Log("BALANCE OK: " + label);
        }

        public static void Run()
        {
            GameDatabase.Load();
            GameDatabase.LoadImported();
            var go = new GameObject("BalanceSmoke");
            var game = go.AddComponent<GameController>();
            game.Job = "软件开发";
            game.Difficulty = "标准";
            game.NewGame();
            var s = game.State;
            s.enemy.hp = 1000;
            s.enemy.maxHp = 1000;
            s.phase = 0;
            s.question = new QuestionRuntime { type = "通用", text = "测试" };
            s.confidence = 5;
            s.maxConfidence = 20;
            s.upgraded.Clear();
            s.uidSeq = 1000;

            // 1. dmg_if：低自信 +6 必须生效
            Hand(game, "perf");
            s.energy = 10;
            int hpBefore = s.enemy.hp;
            game.PlayCard(1001);
            int dealt = hpBefore - s.enemy.hp;
            Check(dealt >= 10, "dmg_if low_hp bonus (dealt=" + dealt + ")");

            // block_if：低自信 +4 必须生效
            Hand(game, "emotion_buffer");
            int blockBefore = s.block;
            game.PlayCard(1001);
            Check(s.block - blockBefore >= 12, "block_if low_hp bonus");

            // 2. exhaust 真正移出循环
            Hand(game, "time");
            s.discard.Clear();
            s.exhaust.Clear();
            game.PlayCard(1001);
            Check(s.exhaust.Any(c => c.key == "time") && !s.discard.Any(c => c.key == "time"),
                  "exhaust card removed from cycle");

            // 3. 强化只作用于玩家卡组内的卡
            s.upgraded.Clear();
            s.masterDeck.Clear();
            s.masterDeck.Add(new RuntimeCard { key = "perf", uid = 2001 });
            s.masterDeck.Add(new RuntimeCard { key = "basic", uid = 2002 });
            game.ApplyEvent("upgrade");
            Check(s.upgraded.Count == 1 && s.upgraded[0] == "perf", "upgrade from own deck only");

            // 4. 奖励三选一为随机且不存在重复
            var a = game.RandomRewardKeys(3);
            var b = game.RandomRewardKeys(3);
            Check(a.Count == 3 && a.Distinct().Count() == 3 &&
                  a.All(k => GameDatabase.Card(k) != null && GameDatabase.Card(k).rarity != "starter"),
                  "reward pool random and distinct");
            Debug.Log("BALANCE reward samples: " + string.Join(",", a) + " / " + string.Join(",", b));


            // 存档往返
            string saveMsg;
            Check(SaveSystem.Write(s, "回归测试存档", out saveMsg), "save write");
            string readErr;
            var loaded = SaveSystem.Read(saveMsg, out readErr);
            Check(loaded != null && loaded.hand.Count == s.hand.Count &&
                  loaded.masterDeck.Count == s.masterDeck.Count, "save roundtrip");
            SaveSystem.Delete(saveMsg);

            // 确定性随机：相同 seed + counter 得到相同结果
            var go2 = new GameObject("BalanceSmoke2");
            var game2 = go2.AddComponent<GameController>();
            game2.NewGame();
            s.seed = 12345; s.rngCounter = 0;
            game2.State.seed = 12345; game2.State.rngCounter = 0;
            var r1 = game.RandomRewardKeys(3);
            var r2 = game2.RandomRewardKeys(3);
            Check(r1.SequenceEqual(r2), "deterministic rng by seed+counter");
            UnityEngine.Object.DestroyImmediate(go2);

            // 自动打通一局（简单策略：能出就出，然后结束回合）
            string oldStatsPath = StatsSystem.PathOverride;
            string oldSaveDir = SaveSystem.SaveDirOverride;
            StatsSystem.PathOverride = Path.Combine(Path.GetTempPath(), "interviewspire_test_stats.json");
            SaveSystem.SaveDirOverride = Path.Combine(Path.GetTempPath(), "interviewspire_test_saves");
            try
            {
            game.NewGame();
            int guard = 0;
            while (game.State.phase != 3 && guard++ < 300)
            {
                if (game.State.phase == 0)
                {
                    var uids = game.State.hand.Select(c => c.uid).ToList();
                    foreach (int uid in uids)
                    {
                        if (game.State.phase != 0) break;
                        game.PlayCard(uid);
                    }
                    if (game.State.phase == 0) game.EndTurn();
                }
                else if (game.State.phase == 1) game.SimulateEnemyAct();
                else if (game.State.phase == 2) game.TakeReward("");
                else break;
            }
            Check(game.State.phase == 3, "auto-play reaches game over (guard=" + guard + ")");
            Check(StatsSystem.Load().runs.Count > 0, "stats persisted after run");
            }
            finally
            {
                StatsSystem.PathOverride = oldStatsPath;
                SaveSystem.SaveDirOverride = oldSaveDir;
            }
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("BALANCE SMOKE OK");
        }

        private static void Hand(GameController game, string key)
        {
            var s = game.State;
            s.hand.Clear();
            s.hand.Add(new RuntimeCard { key = key, uid = 1001 });
            s.phase = 0;
        }
    }
}
#endif
