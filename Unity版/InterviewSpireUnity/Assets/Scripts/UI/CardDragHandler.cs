using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InterviewSpire
{
    /// <summary>
    /// 手牌交互：拖动时卡牌跟随鼠标，拖到面试官区域才打出；双击也可打出。
    /// 单击不出牌，避免误触。
    /// </summary>
    public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public GameController game;
        public RectTransform dropZone;
        public UnityEngine.UI.Image[] dropBorderSides;
        public Canvas canvas;
        public int cardUid;

        private Vector2 home;
        private bool homeReady;
        private bool dragging;
        private int lastClickTime;
        private int lastDragEndMs;

        public void Init(Vector2 basePosition)
        {
            home = basePosition;
            homeReady = true;
        }

        private void Start()
        {
            if (!homeReady)
            {
                home = ((RectTransform)transform).anchoredPosition;
                homeReady = true;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (dragging) return;
            transform.SetAsLastSibling();
            transform.localScale = Vector3.one * 1.06f;
            ((RectTransform)transform).anchoredPosition = home + new Vector2(0f, 14f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (dragging) return;
            transform.localScale = Vector3.one;
            ((RectTransform)transform).anchoredPosition = home;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!homeReady) home = ((RectTransform)transform).anchoredPosition;
            dragging = true;
            transform.SetAsLastSibling();
            transform.localScale = Vector3.one * 1.06f;
            SetDropHighlight(true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            var rt = (RectTransform)transform;
            var parent = (RectTransform)rt.parent;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, eventData.position, eventData.pressEventCamera, out local))
                rt.anchoredPosition = local;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            dragging = false;
            lastDragEndMs = Environment.TickCount;
            transform.localScale = Vector3.one;
            SetDropHighlight(false);

            bool hit = dropZone != null && RectTransformUtility.RectangleContainsScreenPoint(
                dropZone, eventData.position, eventData.pressEventCamera);
            if (hit && game != null) game.PlayCard(cardUid);
            else ((RectTransform)transform).anchoredPosition = home;
        }

        private void SetDropHighlight(bool on)
        {
            if (dropBorderSides == null) return;
            foreach (var side in dropBorderSides)
                if (side != null) side.gameObject.SetActive(on);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 拖动结束后的那次点击不算双击
            int now = Environment.TickCount;
            if (now - lastDragEndMs < 150) return;
            if (now - lastClickTime < 500)
            {
                lastClickTime = 0;
                if (game != null) game.PlayCard(cardUid);
            }
            else lastClickTime = now;
        }
    }
}
