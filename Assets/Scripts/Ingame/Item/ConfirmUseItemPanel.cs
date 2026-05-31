using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using DeckRoguelike.Item;
using DeckRoguelike.Core;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 아이템 슬롯 클릭 시 생성되는 확인 패널.
    ///
    /// Blocker(전체화면 투명 오브젝트) 없이 동작합니다.
    /// Update()에서 패널 RectTransform 밖을 클릭했는지 좌표로만 판단하여 닫습니다.
    /// 덕분에 패널이 열린 채로 카드 hover, tooltip 등 다른 UI 상호작용이 모두 정상 동작합니다.
    ///
    /// [프리팹 구조 (예시)]
    /// ConfirmUseItemPrefab  (ConfirmUseItemPanel)
    ///  └─ Panel             (실제 패널 콘텐츠)       ← panel
    ///       ├─ ItemIcon     (Image)                  ← itemIcon
    ///       ├─ ItemName     (TextMeshProUGUI)        ← itemNameText
    ///       ├─ ItemDesc     (TextMeshProUGUI)        ← itemDescText
    ///       ├─ UseButton    (Button)                 ← useButton
    ///       └─ DiscardButton(Button)                 ← discardButton
    /// </summary>
    public class ConfirmUseItemPanel : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("실제 콘텐츠가 담긴 패널 오브젝트 (이 영역 밖 클릭 시 닫힘)")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private Image            itemIcon;
        [SerializeField] private TextMeshProUGUI  itemNameText;
        [SerializeField] private TextMeshProUGUI  itemDescText;
        [SerializeField] private Button useButton;
        [SerializeField] private Button discardButton;

        private ItemData currentItem;
        private ItemSlotUI sourceSlot;
        private Canvas parentCanvas;
        private bool isClosing = false;

        // 패널 생성 직후 같은 프레임의 PointerDown은 무시 (생성 클릭으로 즉시 닫히지 않도록)
        private bool readyToDetectOutsideClick = false;

        public ItemSlotUI SourceSlot => sourceSlot;

        private void Awake()
        {
            EnsureComponents();
            useButton?.onClick.AddListener(OnUseClicked);
            discardButton?.onClick.AddListener(OnDiscardClicked);
        }

        private void Start()
        {
            // Canvas 참조 (Screen Space Overlay면 null, Camera면 해당 카메라)
            parentCanvas = GetComponentInParent<Canvas>();
        }

        private void LateUpdate()
        {
            if (!readyToDetectOutsideClick) { readyToDetectOutsideClick = true; return; }
            if (isClosing) return;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            // 패널 내부에서 클릭 → 닫지 않음
            if (IsPointerInsidePanel()) return;
            // 소스 슬롯 위 클릭 → 슬롯 자체의 토글 로직이 닫기를 처리하므로 여기선 무시
            if (IsPointerInsideSourceSlot()) return;

            Close();
        }

        /// <summary>패널을 초기화하고 표시합니다.</summary>
        public void Initialize(ItemData item, ItemSlotUI slot)
        {
            currentItem = item;
            sourceSlot  = slot;

            if (itemIcon != null)
            {
                Sprite sprite = item.icon;
                if (sprite == null && item.itemCode > 0)
                    sprite = Addressables.LoadAssetAsync<Sprite>($"Sprites/Ingame/Item/{item.itemCode}").WaitForCompletion();

                itemIcon.sprite  = sprite;
                itemIcon.color   = Color.white;
                itemIcon.enabled = true;
            }

            if (itemNameText != null) itemNameText.text = item.itemName;
            if (itemDescText  != null) itemDescText.text  = item.description;

            // 308 소생의 팬던트: 패시브 아이템이므로 사용 버튼 비활성화 (전투 시작 시 자동 활성화됨)
            if (useButton != null)
                useButton.interactable = (item.itemCode != 308);
        }

        // ─────────────────────────────────────────────
        // 외부 클릭 감지
        // ─────────────────────────────────────────────

        /// <summary>
        /// 현재 마우스 커서가 패널 RectTransform 안에 있는지 확인합니다.
        /// 패널이 없으면 루트 RectTransform 기준으로 판단합니다.
        /// </summary>
        private bool IsPointerInsidePanel()
        {
            var rt = panel != null ? panel : GetComponent<RectTransform>();
            if (rt == null) return false;

            Camera cam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? parentCanvas.worldCamera
                : null;

            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            return RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos, cam);
        }

        private bool IsPointerInsideSourceSlot()
        {
            if (sourceSlot == null) return false;
            var rt = sourceSlot.transform as RectTransform;
            if (rt == null) return false;
            Camera cam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? parentCanvas.worldCamera : null;
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            return RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos, cam);
        }

        // ─────────────────────────────────────────────
        // 버튼 콜백
        // ─────────────────────────────────────────────

        private void OnUseClicked()
        {
            if (currentItem == null || GameManager.Instance == null) return;

            var combat = InGameUIController.Instance?.GetCombatController();

            // 105/109/201/202/208/301/302: 사용 버튼을 누른 직후 전체 타일에 사거리 스프라이트 표시
            int code = currentItem.itemCode;
            if (combat != null && (code == 105 || code == 109 || code == 201 || code == 202 || code == 208 || code == 301 || code == 302))
                combat.ShowItemAllTilePreview(code);

            bool used  = GameManager.Instance.UseItem(currentItem, combat);

            if (used)
            {
                // 타겟팅 모드 진입 시 슬롯 즉시 파괴 금지 — 취소/확정 시 CombatController가 처리
                if (combat != null && combat.IsItemTargetingActive)
                    combat.SetPendingItemSlot(sourceSlot);
                else
                {
                    // 305 무지개 포션처럼 효과에서 자기 슬롯에 새 아이템을 채워넣었다면 파괴하지 않는다.
                    bool slotReused = sourceSlot != null
                        && sourceSlot.ItemData != null
                        && GameManager.Instance != null
                        && GameManager.Instance.Items.Contains(sourceSlot.ItemData);
                    if (sourceSlot != null && !slotReused)
                        Destroy(sourceSlot.gameObject);
                    // 비-타겟팅 아이템(103/104 등)은 타겟팅 완료 콜백이 없으므로 미리보기 직접 정리
                    combat?.ClearItemAllTilePreview();
                }
            }
            else
            {
                Debug.Log("[ConfirmUseItemPanel] 아이템 사용 불가 (조건 미충족)");
            }

            Close();
        }

        private void OnDiscardClicked()
        {
            if (currentItem == null || GameManager.Instance == null) return;

            GameManager.Instance.RemoveItem(currentItem);
            InGameUIController.Instance?.ClearItemSlot(sourceSlot);

            Close();
        }

        // ─────────────────────────────────────────────
        // 열기 / 닫기
        // ─────────────────────────────────────────────

        public void Close()
        {
            if (isClosing) return;
            isClosing = true;

            // Sprite 정리 규칙:
            //   1) 아이템 타겟팅 모드가 시작됐으면(=Use 클릭 후) → 그대로 유지 (셀 hover 효과에 사용)
            //   2) 소스 슬롯 위에서 hover 중이면 → 고정 해제만 (hover-exit 시 자연 정리)
            //   3) 그 외 (화면 밖 클릭) → 즉시 제거
            var combat = InGameUIController.Instance?.GetCombatController();
            if (combat != null && !combat.IsItemTargetingActive)
            {
                if (IsPointerInsideSourceSlot()) combat.UnpinItemAllTilePreview();
                else                            combat.ClearItemAllTilePreview();
            }

            if (InGameUIController.Instance != null)
                InGameUIController.Instance.ClearConfirmPanel(this);

            Destroy(gameObject);
        }

        // ─────────────────────────────────────────────
        // 컴포넌트 자동 구성
        // ─────────────────────────────────────────────

        private void EnsureComponents()
        {
            // Panel 자동 탐색
            if (panel == null)
            {
                var t = transform.Find("Panel");
                if (t != null) panel = t.GetComponent<RectTransform>();
            }
            // Panel 없으면 루트 자체를 패널로 사용
            if (panel == null)
                panel = GetComponent<RectTransform>();

            // 루트 Image는 raycastTarget OFF (패널 밖 hover 통과용)
            var rootImg = GetComponent<Image>();
            if (rootImg != null) rootImg.raycastTarget = false;

            // Panel 내부 UI 자동 탐색
            Transform searchRoot = panel != null ? panel.transform : transform;

            if (itemIcon == null)
            {
                foreach (var img in searchRoot.GetComponentsInChildren<Image>(true))
                {
                    if (img.gameObject == searchRoot.gameObject) continue;
                    if (img.GetComponent<Button>() != null) continue; // 버튼 배경 Image 스킵
                    itemIcon = img;
                    break;
                }
            }

            if (itemNameText == null || itemDescText == null)
            {
                // 버튼 자식 텍스트(버튼 라벨)는 제외하고 탐색
                foreach (var tmp in searchRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (tmp.GetComponentInParent<Button>() != null) continue; // 버튼 라벨 스킵

                    if (itemNameText == null)      { itemNameText = tmp; continue; }
                    if (itemDescText  == null)     { itemDescText  = tmp; break; }
                }
            }

            if (useButton == null || discardButton == null)
            {
                // 인스펙터 미연결 시 순서로 탐색: 첫 번째 Button = Use, 두 번째 = Discard
                var btns = searchRoot.GetComponentsInChildren<Button>(true);
                foreach (var b in btns)
                {
                    if      (useButton     == null) useButton     = b;
                    else if (discardButton == null) { discardButton = b; break; }
                }
            }
        }
    }
}
