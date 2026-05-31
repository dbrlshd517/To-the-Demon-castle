using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DeckRoguelike.Item;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 인게임 아이템 슬롯 하나에 붙는 컴포넌트.
    /// 클릭 시 InGameUIController를 통해 ConfirmUseItemPanel을 표시합니다.
    ///
    /// [프리팹 구조]
    /// ItemSlotPrefab (ItemSlotUI + Button + Image)
    ///  └─ IconImage (Image)  ← iconImage (선택)
    /// </summary>
    public class ItemSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public ItemData ItemData { get; private set; }

        private Button button;
        private bool isHovering;
        // MainMenu 슬롯과 동일한 button-like 동작 — PointerDown 시점엔 후보로 기록만,
        // PointerUp 시점에 down→up 거리가 임계값 이하면 click 인정.
        // 기존엔 PointerDown에서 즉시 OnSlotClicked()를 호출해 mousedown으로 ConfirmUseItemPanel이
        // 열려버리던 문제를 해소한다. Button.onClick은 PointerClick 경유라 작은 드래그에도 취소되므로
        // 자체 PointerDown/Up으로 거리 검사를 한다.
        //
        // ⚠ ItemSlotUI 자체가 IPointerDown/Up Handler를 구현하면 안 됨:
        // Itemprefab은 자식 'Image' GameObject에 Image + Button이 같이 붙어 있어
        // ExecuteHierarchy가 Selectable(IPointerDownHandler)에서 멈춰 root까지 도달하지 않음.
        // 따라서 EventTrigger를 Button.gameObject(=자식 'Image' GO)에 부착해 click을 받는다.
        private Vector2 _pointerDownPos;
        private bool _pendingClick;
        private const float ClickMovementThreshold = 20f;

        private void Awake()
        {
            button = GetComponentInChildren<Button>();
            // Button.onClick은 사용하지 않는다 — PointerDown/Up 자체 click 판별만 사용.
            // (이전 fallback 코드는 PointerClick 경유라 작은 드래그에도 묵살되는 한계가 있었음)
            if (button != null)
                button.onClick.RemoveAllListeners();

            GameObject clickTarget = button != null ? button.gameObject : this.gameObject;
            SetupClickHandlerOnTarget(clickTarget);
        }

        private void SetupClickHandlerOnTarget(GameObject target)
        {
            var trig = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
            trig.triggers.Clear();

            var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            downEntry.callback.AddListener(ev =>
            {
                if (ev is PointerEventData ped && ped.button == PointerEventData.InputButton.Left)
                {
                    _pointerDownPos = ped.position;
                    _pendingClick = true;
                }
            });
            trig.triggers.Add(downEntry);

            var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            upEntry.callback.AddListener(ev =>
            {
                if (!_pendingClick) return;
                if (!(ev is PointerEventData ped) || ped.button != PointerEventData.InputButton.Left) return;
                _pendingClick = false;
                float dist = Vector2.Distance(_pointerDownPos, ped.position);
                if (dist <= ClickMovementThreshold) OnSlotClicked();
            });
            trig.triggers.Add(upEntry);
        }

        /// <summary>슬롯을 빈 상태로 초기화합니다. 위치는 유지됩니다.</summary>
        public void SetEmpty()
        {
            ItemData = null;

            var images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                img.sprite = null;
                img.color = new Color(0, 0, 0, 0);
            }

            if (button == null) button = GetComponentInChildren<Button>();
            if (button != null) button.interactable = false;

            var trigger = GetComponent<TooltipTrigger>();
            if (trigger != null) trigger.ClearDynamicEntries();
        }

        /// <summary>아이템 데이터를 설정하고 아이콘을 표시합니다.</summary>
        public void Initialize(ItemData item)
        {
            ItemData = item;

            if (button == null) button = GetComponentInChildren<Button>();
            if (button != null) button.interactable = true;

            // 루트 또는 자식의 Image에 아이콘 적용
            var images = GetComponentsInChildren<Image>(true);
            Image iconImg = null;
            foreach (var img in images)
            {
                if (img.gameObject != gameObject) { iconImg = img; break; }
            }
            if (iconImg == null)
            {
                // 루트에만 Image가 있는 경우
                iconImg = GetComponent<Image>();
            }

            Sprite itemSprite = null;
            if (iconImg != null && item != null)
            {
                itemSprite = item.icon;
                if (itemSprite == null && item.itemCode > 0)
                    itemSprite = Addressables.LoadAssetAsync<Sprite>($"Sprites/Ingame/Item/{item.itemCode}").WaitForCompletion();

                if (itemSprite != null)
                {
                    iconImg.sprite = itemSprite;
                    iconImg.color  = Color.white;
                }
                else
                {
                    iconImg.color = new Color(0.3f, 0.7f, 0.3f, 1f);
                }
                iconImg.enabled = true;
            }

            // hover 시 툴팁 표시
            if (item != null)
            {
                var trigger = GetComponent<TooltipTrigger>();
                if (trigger == null) trigger = gameObject.AddComponent<TooltipTrigger>();
                trigger.SetDynamicEntries(new List<TooltipEntry>
                {
                    TooltipEntry.ObjectUI(item.itemName, item.description, itemSprite)
                });
            }
        }

        private void OnSlotClicked()
        {
            if (ItemData == null) return;
            // 카드 sticky 진행 중에는 아이템 슬롯 입력 차단
            if (BoardController.AnyCardStickyActive) return;

            var combat = InGameUIController.Instance?.GetCombatController();

            // 1) 아이템 타겟팅 모드(예: Use 버튼 후 적/셀 선택 대기) 중 같은 슬롯을 다시 누르면 취소.
            if (combat != null && combat.IsItemTargetingActive)
            {
                combat.CancelItemTargeting();
                InGameUIController.Instance?.ClearConfirmPanelForSlot(this);
                return;
            }

            // 2) 이 슬롯에 confirm 패널이 이미 열려있으면 → 클릭은 "닫기/취소" 의미
            //    슬롯 위에서 클릭한 것이므로(=hover 중) preview는 고정 해제만 → hover-exit 시 자연 클리어.
            if (InGameUIController.Instance?.HasActiveConfirmPanelFor(this) == true)
            {
                InGameUIController.Instance?.ClearConfirmPanelForSlot(this);
                if (combat != null) combat.UnpinItemAllTilePreview();
                return;
            }

            int code = ItemData.itemCode;
            // 3) 105/109/201/202/208/301/302: hover로만 보여지던 preview를 고정 토글하고 패널 표시
            if (combat != null && (code == 105 || code == 109 || code == 201 || code == 202 || code == 208 || code == 301 || code == 302))
            {
                // hover 상태였다면 이미 active일 수 있음 — pinned 부여하고 panel만 열어준다.
                combat.ToggleItemAllTilePreviewPinned(code);
                InGameUIController.Instance?.ShowConfirmUseItemPanel(ItemData, this);
                return;
            }

            InGameUIController.Instance?.ShowConfirmUseItemPanel(ItemData, this);
        }

        // 105/109/201/202/208/301/302: hover 시 임시로 사거리 미리보기 표시, 클릭(=pinned)일 때는 유지
        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
            if (ItemData == null) return;
            if (BoardController.AnyCardStickyActive) return; // 카드 sticky 중 아이템 hover 차단
            int c = ItemData.itemCode;
            if (c != 105 && c != 109 && c != 201 && c != 202 && c != 208 && c != 301 && c != 302) return;
            var combat = InGameUIController.Instance?.GetCombatController();
            // 이미 표시 중(pinned 포함)이면 추가 동작 불필요
            if (combat == null || combat.IsItemAllTilePreviewActive(c)) return;
            combat.ShowItemAllTilePreview(c);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
            if (ItemData == null) return;
            int c = ItemData.itemCode;
            if (c != 105 && c != 109 && c != 201 && c != 202 && c != 208 && c != 301 && c != 302) return;
            var combat = InGameUIController.Instance?.GetCombatController();
            if (combat == null) return;
            // 타겟팅 진행 중이면 유지
            if (combat.IsItemTargetingActive) return;
            // 클릭으로 고정된 상태면 유지, 아니면 제거
            combat.HideItemAllTilePreviewIfNotPinned();
        }
    }
}
