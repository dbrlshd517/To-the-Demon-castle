using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// New Input System 환경에서 ScrollRect 마우스 휠 스크롤 지원
    /// ScrollView GameObject에 이 컴포넌트를 추가하세요
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollRectWheelFix : MonoBehaviour, IScrollHandler
    {
        [SerializeField] private float scrollSensitivity = 300f;

        private ScrollRect scrollRect;

        private void Awake()
        {
            scrollRect = GetComponent<ScrollRect>();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!scrollRect.vertical) return;

            float delta = eventData.scrollDelta.y * scrollSensitivity;
            float contentHeight = scrollRect.content.rect.height;
            float viewportHeight = scrollRect.viewport.rect.height;
            float scrollableHeight = contentHeight - viewportHeight;

            if (scrollableHeight <= 0f) return;

            Vector2 pos = scrollRect.content.anchoredPosition;
            pos.y -= delta;
            pos.y = Mathf.Clamp(pos.y, 0f, scrollableHeight);
            scrollRect.content.anchoredPosition = pos;
        }
    }
}
