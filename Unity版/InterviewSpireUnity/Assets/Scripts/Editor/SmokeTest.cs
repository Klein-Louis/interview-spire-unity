#if UNITY_EDITOR
using UnityEngine;

namespace InterviewSpire.Editor
{
    /// <summary>命令行冒烟测试：用团结引擎跑一遍新局与出牌，不需进入 Play 模式。</summary>
    public static class SmokeTest
    {
        public static void Run()
        {
            GameDatabase.Load();
            var go = new GameObject("SmokeTest");
            var game = go.AddComponent<GameController>();
            game.Job = "软件开发";
            game.Difficulty = "标准";
            game.NewGame();
            Debug.Log("SMOKE newgame hand=" + game.State.hand.Count +
                      " enemyHP=" + game.State.enemy.hp +
                      " question=" + game.State.question.type);
            if (game.State.hand.Count > 0)
            {
                int uid = game.State.hand[0].uid;
                game.PlayCard(uid);
                Debug.Log("SMOKE playcard energy=" + game.State.energy +
                          " enemyHP=" + game.State.enemy.hp + " mood=" + game.EnemyMood);
            }
            Object.DestroyImmediate(go);
            Debug.Log("SMOKE OK");
        }
    }
}
#endif
