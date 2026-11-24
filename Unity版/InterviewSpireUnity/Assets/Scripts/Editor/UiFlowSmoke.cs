#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace InterviewSpire.Editor
{
    /// <summary>验证 Unity UI 中 STAR 答题链路是否真实可达。</summary>
    public static class UiFlowSmoke
    {
        public static void Run()
        {
            GameDatabase.Load();
            GameDatabase.LoadImported();
            var go = new GameObject("UiFlowSmoke");
            var game = go.AddComponent<GameController>();
            var ui = go.AddComponent<GameUI>();
            ui.SendMessage("Start");

            // 开局界面设置按钮应能返回开局界面
            var startSettings = FindButton("设置");
            if (startSettings == null) throw new Exception("UI FLOW FAIL: 开局界面找不到设置按钮");
            startSettings.onClick.Invoke();
            var startSettingsClose = FindButton("关闭");
            if (startSettingsClose == null) throw new Exception("UI FLOW FAIL: 开局设置找不到关闭按钮");
            startSettingsClose.onClick.Invoke();
            if (FindButton("开始面试") == null)
                throw new Exception("UI FLOW FAIL: 关闭设置后未返回开局界面");
            Debug.Log("UI FLOW OK: 开局设置关闭后返回开局界面");

            // 点击开局按钮
            var startBtn = FindButton("开始面试");
            if (startBtn == null) throw new Exception("UI FLOW FAIL: 找不到开始面试按钮");
            startBtn.onClick.Invoke();

            // 关掉新手引导
            CloseModal(ui);
            CloseModal(ui);
            CloseModal(ui);

            // 验证设置面板打开/关闭后不残留遮罩
            var settingsBtn = FindButton("设置");
            if (settingsBtn == null) throw new Exception("UI FLOW FAIL: 找不到设置按钮");
            settingsBtn.onClick.Invoke();
            var settingsClose = FindButton("关闭");
            if (settingsClose == null) throw new Exception("UI FLOW FAIL: 找不到设置关闭按钮");
            settingsClose.onClick.Invoke();
            if (GameObject.Find("ModalBlocker") != null)
                throw new Exception("UI FLOW FAIL: 关闭设置后遮罩仍存在");
            Debug.Log("UI FLOW OK: 设置关闭后无遮罩残留");

            // 先验证不点 STAR 也能出牌（双击手牌）
            var handler = UnityEngine.Object.FindObjectsOfType<CardDragHandler>()
                .FirstOrDefault(h => game.State.hand.Any(c => c.uid == h.cardUid));
            if (handler == null) throw new Exception("UI FLOW FAIL: 找不到有效手牌交互组件");
            int beforeHand = game.State.hand.Count;
            var ped = new PointerEventData(EventSystem.current);
            handler.OnPointerClick(ped);
            handler.OnPointerClick(ped);
            Debug.Log("UI FLOW DEBUG hand=" + beforeHand + "->" + game.State.hand.Count +
                      " uid=" + handler.cardUid + " gameNull=" + (handler.game == null) +
                      " energy=" + game.State.energy + " phase=" + game.State.phase);
            if (game.State.hand.Count >= beforeHand)
                throw new Exception("UI FLOW FAIL: 未点 STAR 时双击手牌没有出牌");
            if (game.State.starUsedThisTurn)
                throw new Exception("UI FLOW FAIL: 出牌不应消耗 STAR");
            Debug.Log("UI FLOW OK: 未点 STAR 也能双击出牌");

            // 再验证上滑拖拽出牌
            handler = UnityEngine.Object.FindObjectsOfType<CardDragHandler>()
                .FirstOrDefault(h => game.State.hand.Any(c => c.uid == h.cardUid));
            if (handler == null) throw new Exception("UI FLOW FAIL: 找不到可拖拽手牌");
            int beforeDrag = game.State.hand.Count;
            game.State.energy = 4;
            var dragPed = new PointerEventData(EventSystem.current);
            handler.OnBeginDrag(dragPed);
            Vector3 world = handler.dropZone.TransformPoint(handler.dropZone.rect.center);
            dragPed.position = RectTransformUtility.WorldToScreenPoint(null, world);
            handler.OnDrag(dragPed);
            bool hitTest = RectTransformUtility.RectangleContainsScreenPoint(
                handler.dropZone, dragPed.position, null);
            Debug.Log("UI FLOW DRAG DEBUG screen=" + dragPed.position +
                      " zone=" + handler.dropZone.position + " hit=" + hitTest +
                      " screenSize=" + Screen.width + "x" + Screen.height);
            handler.OnEndDrag(dragPed);
            if (game.State.hand.Count >= beforeDrag)
                throw new Exception("UI FLOW FAIL: 拖拽到面试官区域没有出牌");
            Debug.Log("UI FLOW OK: 拖拽上滑出牌");

            var starBtn = FindButton("STAR");
            if (starBtn == null) throw new Exception("UI FLOW FAIL: 找不到 STAR 按钮");
            Debug.Log("UI FLOW: STAR button found, interactable=" + starBtn.interactable);
            starBtn.onClick.Invoke();

            var texts = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>();
            bool modalVisible = texts.Any(t => t.text != null && t.text.Contains("STAR · 亮点前置"));
            if (!modalVisible) throw new Exception("UI FLOW FAIL: 答题弹窗没有出现");
            var input = UnityEngine.Object.FindObjectOfType<TMP_InputField>();
            if (input == null) throw new Exception("UI FLOW FAIL: 答题输入框不存在");

            var submit = FindButton("提交回答");
            if (submit == null) throw new Exception("UI FLOW FAIL: 找不到提交回答按钮");
            input.text = "内存管理、内存分配、垃圾回收；我负责优化，最终耗时从200ms降到50ms。";
            submit.onClick.Invoke();

            if (!game.State.starUsedThisTurn) throw new Exception("UI FLOW FAIL: 提交后 starUsedThisTurn 仍为 false");
            if (game.LastStarReport == null) throw new Exception("UI FLOW FAIL: 没有评分报告");
            Debug.Log("UI FLOW OK: grade=" + game.LastStarReport.grade + " damage=" + game.LastStarReport.damage);

            // 验证：读档后敌人 0 血能补触发奖励，不卡死（截图反馈的 bug）
            if (game.State == null) throw new Exception("UI FLOW FAIL: 无对局状态可读档");
            var zeroHp = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(game.State));
            zeroHp.enemy.hp = 0;
            zeroHp.phase = 0;
            game.LoadState(zeroHp);
            if (game.State.phase != 2 && game.State.floor + 1 >= GameDatabase.Data.enemies.Count)
                throw new Exception("UI FLOW FAIL: 0血读档未进入通关结算");
            if (game.State.phase != 2)
                throw new Exception("UI FLOW FAIL: 0血读档未进入奖励阶段 phase=" + game.State.phase);
            Debug.Log("UI FLOW OK: 0血读档补触发奖励 phase=" + game.State.phase);

            UnityEngine.Object.DestroyImmediate(go);
        }

        private static Button FindButton(string keyword)
        {
            return UnityEngine.Object.FindObjectsOfType<Button>()
                .FirstOrDefault(b => b.gameObject.name.Contains(keyword));
        }

        private static void CloseModal(GameUI ui)
        {
            var m = typeof(GameUI).GetMethod("CloseModal", BindingFlags.NonPublic | BindingFlags.Instance);
            if (m != null) m.Invoke(ui, null);
        }
    }
}
#endif
