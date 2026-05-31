using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DeckRoguelike.Core;
using DeckRoguelike.Relic;
using Loc = DeckRoguelike.Core.LocalizationManager;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 유물 상세 패널 컨트롤러.
    /// 보유 유물을 좌/우 버튼으로 하나씩 탐색합니다.
    /// closeOverlay(전체화면 투명 버튼)를 클릭하면 닫힙니다.
    ///
    /// [Unity 설정]
    /// 1. relicPanel 오브젝트에 이 스크립트를 붙입니다.
    /// 2. closeOverlay: relicPanel 직속 자식으로 전체화면 투명 Button 오브젝트 (Image alpha=0, Raycast On)
    /// 3. 콘텐츠 오브젝트(relicImage, texts, nav buttons)는 closeOverlay보다 높은 sibling index에 위치
    /// </summary>
    public class RelicInfoController : MonoBehaviour
    {
        [Header("=== 유물 표시 ===")]
        [SerializeField] private Image relicImage;
        [SerializeField] private TextMeshProUGUI relicNameText;
        [SerializeField] private TextMeshProUGUI relicRarityText;
        [SerializeField] private TextMeshProUGUI relicDescText;

        [Header("=== 내비게이션 ===")]
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;

        [Header("=== 외부 클릭 닫기 ===")]
        [SerializeField] private Button closeOverlay; // 패널 뒤 전체화면 투명 버튼

        private int currentIndex = 0;
        // 도감 모드: GameManager.Relics 대신 외부에서 주입된 RelicData 리스트를 사용한다.
        private List<DeckRoguelike.Relic.RelicData> overrideRelics;
        // Open/OpenWithList 호출로 명시적으로 열린 경우에만 true.
        // 씬에 활성으로 저장됐거나 부모 활성화로 의도치 않게 켜진 경우를 막기 위한 가드.
        private bool _explicitlyOpened;

        private void Awake()
        {
            leftButton?.onClick.AddListener(NavigateLeft);
            rightButton?.onClick.AddListener(NavigateRight);
            closeOverlay?.onClick.AddListener(CloseAndForwardClick);
            gameObject.SetActive(false);
        }

        /// <summary>closeOverlay 클릭 시 호출 — 패널을 닫는다.
        /// 클릭 대상이 다른 유물 슬롯(RelicListSlot 마커가 있는 오브젝트)인 경우에만
        /// EventSystem으로 클릭을 전파하여 즉시 새 유물 정보로 전환되도록 한다.
        /// 도감 탭/필터 버튼 등 마커가 없는 UI에는 클릭이 전파되지 않으므로
        /// 패널이 열린 상태에서 외곽 클릭이 다른 버튼을 발화하는 버그가 발생하지 않는다.</summary>
        private void CloseAndForwardClick()
        {
            Vector2 cursor = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

            // 패널을 닫기 전에 cursor 아래에 슬롯 마커가 있는지 미리 조사.
            // 닫은 뒤에 raycast 하면 EventSystem 상태나 활성 패널 변화로 hits가 달라질 수 있어,
            // 닫기 전에 결정해 두는 편이 안정적이다.
            GameObject slotTarget = FindRelicSlotAt(cursor);

            Close();

            if (slotTarget != null)
                ForwardClickTo(slotTarget, cursor);
        }

        /// <summary>screenPos 좌표에서 raycast하여 RelicListSlot 마커가 있는 가장 위쪽 오브젝트를 반환.
        /// 자기 자신(이 InfoPanel)의 자식은 무시한다.</summary>
        private GameObject FindRelicSlotAt(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return null;

            var ped = new PointerEventData(es)
            {
                position = screenPos,
                button   = PointerEventData.InputButton.Left,
            };
            var hits = new List<RaycastResult>();
            es.RaycastAll(ped, hits);

            foreach (var h in hits)
            {
                if (h.gameObject == null) continue;
                if (h.gameObject.transform.IsChildOf(transform)) continue;

                // h.gameObject 자신부터 부모로 거슬러 올라가며 RelicListSlot 마커를 찾는다 —
                // 보통 슬롯의 Button은 자식 Image이고 마커는 슬롯 루트에 부착되기 때문.
                if (h.gameObject.GetComponentInParent<RelicListSlot>(true) != null)
                    return h.gameObject;
            }
            return null;
        }

        /// <summary>지정한 GameObject로 좌클릭 이벤트를 전파한다.
        /// PointerClick(=IPointerClickHandler)뿐 아니라 PointerDown→Up도 같은 좌표로 발화 —
        /// RelicListPanel 슬롯이 button-like 자체 click 판별(EventTrigger PointerDown/Up)을 쓰기 때문에
        /// PointerClick만으로는 새 슬롯에 클릭이 전달되지 않는다.</summary>
        private static void ForwardClickTo(GameObject target, Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null || target == null) return;

            var ped = new PointerEventData(es)
            {
                position = screenPos,
                button   = PointerEventData.InputButton.Left,
            };

            // 슬롯의 SetupSlotClickHandler가 EventTrigger PointerDown/Up으로 click을 판별하므로
            // 같은 좌표로 두 이벤트를 모두 호출. 거리=0이라 임계값 이하 → click 인정.
            ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.pointerUpHandler);

            // 폴백: IPointerClickHandler / ISubmitHandler 경로도 시도 (기존 Button 슬롯 대비)
            var clicked = ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.pointerClickHandler);
            if (clicked != null) return;

            ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.submitHandler);
        }

        /// <summary>씬 시작 시 패널이 활성 상태로 저장돼 있어도 무조건 비활성화.
        /// Awake에서의 SetActive(false)가 어떤 이유로 무시되거나 다른 코드가 활성화한 경우 대비.</summary>
        private void Start()
        {
            if (!_explicitlyOpened && gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += DisplayCurrent;
            // 명시적 Open 호출이 아닌데 활성화됐다면 즉시 닫는다.
            if (!_explicitlyOpened)
            {
                gameObject.SetActive(false);
                return;
            }
        }
        private void OnDisable() => LocalizationManager.OnLanguageChanged -= DisplayCurrent;

        // ──────────────────────────────────────────────
        #region Public API

        public void Toggle()
        {
            if (gameObject.activeSelf) Close();
            else Open();
        }

        public void Open()
        {
            overrideRelics = null;
            var relics = GetRelics();
            if (relics.Count == 0) return;

            // 가장 최근에 추가된 유물부터 표시
            currentIndex = relics.Count - 1;
            _explicitlyOpened = true;
            gameObject.SetActive(true);
            DisplayCurrent();
        }

        /// <summary>도감용 — 외부에서 주입한 RelicData 리스트와 시작 인덱스로 패널을 연다.
        /// GameManager.Relics에 의존하지 않으므로 메인 메뉴에서도 사용 가능.</summary>
        public void OpenWithList(List<DeckRoguelike.Relic.RelicData> dataList, int startIndex = 0)
        {
            if (dataList == null || dataList.Count == 0) return;
            overrideRelics = dataList;
            currentIndex = Mathf.Clamp(startIndex, 0, dataList.Count - 1);
            _explicitlyOpened = true;
            gameObject.SetActive(true);
            DisplayCurrent();
        }

        public void Close()
        {
            overrideRelics = null;
            _explicitlyOpened = false;
            gameObject.SetActive(false);
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Navigation

        private void NavigateLeft()
        {
            currentIndex = Mathf.Max(0, currentIndex - 1);
            DisplayCurrent();
        }

        private void NavigateRight()
        {
            int total = overrideRelics != null ? overrideRelics.Count : GetRelics().Count;
            currentIndex = Mathf.Min(total - 1, currentIndex + 1);
            DisplayCurrent();
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Display

        private void DisplayCurrent()
        {
            // 도감 모드면 overrideRelics 사용, 아니면 GameManager 보유 유물 사용.
            DeckRoguelike.Relic.RelicData data;
            int total;
            if (overrideRelics != null)
            {
                if (overrideRelics.Count == 0) { Close(); return; }
                currentIndex = Mathf.Clamp(currentIndex, 0, overrideRelics.Count - 1);
                data = overrideRelics[currentIndex];
                total = overrideRelics.Count;
            }
            else
            {
                var owned = GetRelics();
                if (owned.Count == 0) { Close(); return; }
                currentIndex = Mathf.Clamp(currentIndex, 0, owned.Count - 1);
                data = owned[currentIndex]?.Data;
                total = owned.Count;
            }
            if (data == null) return;

            if (relicImage != null)
            {
                Sprite icon = data.icon;
                if (icon == null && data.relicCode > 0)
                    icon = TryLoadRelicSprite(data.relicCode);
                relicImage.sprite  = icon;
                relicImage.enabled = icon != null;
            }

            if (relicNameText != null)
                relicNameText.text = Loc.Get($"relic_name_{data.relicCode}");

            if (relicRarityText != null)
                relicRarityText.text = Loc.Get(data.IsBossRelic ? "relic_type_boss" : "relic_type_normal");

            if (relicDescText != null)
                relicDescText.text = data.description;

            RefreshNavButtons(total);
        }

        private void RefreshNavButtons(int total)
        {
            // 첫 번째 유물이면 왼쪽 버튼 숨김
            if (leftButton != null)
            {
                bool canLeft = currentIndex > 0;
                leftButton.interactable = canLeft;
                leftButton.gameObject.SetActive(canLeft);
            }

            // 마지막 유물이면 오른쪽 버튼 숨김
            if (rightButton != null)
            {
                bool canRight = currentIndex < total - 1;
                rightButton.interactable = canRight;
                rightButton.gameObject.SetActive(canRight);
            }
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Helpers

        private IReadOnlyList<RelicEffect> GetRelics()
        {
            return GameManager.Instance?.Relics ?? new System.Collections.Generic.List<RelicEffect>();
        }

        /// <summary>해당 relicCode의 sprite를 Addressables에서 로드. 키가 없으면 null 반환(InvalidKeyException 등 모두 흡수).</summary>
        private static Sprite TryLoadRelicSprite(int relicCode)
        {
            string address = $"Sprites/Ingame/Relic/{relicCode}";
            try { return Addressables.LoadAssetAsync<Sprite>(address).WaitForCompletion(); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RelicInfoController] 스프라이트 로드 실패 '{address}': {e.Message}");
                return null;
            }
        }

        #endregion
    }
}
