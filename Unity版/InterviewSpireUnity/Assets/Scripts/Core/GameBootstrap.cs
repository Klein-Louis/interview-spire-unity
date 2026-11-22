using UnityEngine;

namespace InterviewSpire
{
    /// <summary>任意空场景按 Play 都会自动创建游戏对象，无需手工挂脚本。</summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindObjectOfType<GameController>() != null) return;
            var go = new GameObject("InterviewSpire");
            go.AddComponent<GameController>();
            go.AddComponent<GameUI>();
        }
    }
}
