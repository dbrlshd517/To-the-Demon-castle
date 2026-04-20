using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DeckRoguelike.Cards;
using DeckRoguelike.Core;
using Loc = DeckRoguelike.Core.LocalizationManager;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 카드 상세 정보 패널.
    /// DeckViewerPanel과 동일한 cardPrefab(CardUI)을 사용해 카드를 표시합니다.
    ///
    /// [Unity 설정]
    /// Panel  (CardInfoPanelController) ← 카드가 패널 중앙에 직접 인스턴스화됨
    ///  ├─ CloseOverlay   (Button, 전체화면 투명)  ← closeOverlay
    ///  ├─ UpgradeToggle  (Toggle)                ← upgradeToggle
    ///  │   └─ Label      (TextMeshProUGUI)        ← upgradeToggleLabel
    ///  ├─ LeftButton     (Button)                 ← leftButton
    ///  └─ RightButton    (Button)                 ← rightButton
    /// </summary>
    public class CardInfoController : MonoBehaviour
    {
        public static CardInfoController Instance { get; private set; }

        [Header("=== 카드 표시 ===")]
        [SerializeField] private GameObject cardPrefab;    // CardUI 가 붙은 카드 프리팹

        [Header("=== 강화 토글 ===")]
        [SerializeField] private Toggle upgradeToggle;
        [SerializeField] private TextMeshProUGUI upgradeToggleLabel;

        [Header("=== 내비게이션 ===")]
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;

        [Header("=== 외부 클릭 닫기 ===")]
        [SerializeField] private Button closeOverlay;

        private List<CardData> cards = new List<CardData>();
        private int currentIndex;
        private GameObject currentCardObj;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            leftButton?.onClick.AddListener(NavigateLeft);
            rightButton?.onClick.AddListener(NavigateRight);
            closeOverlay?.onClick.AddListener(Close);
            upgradeToggle?.onValueChanged.AddListener(_ => DisplayCurrent());
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()  => LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        private void OnDisable() => LocalizationManager.OnLanguageChanged -= OnLanguageChanged;

        private void OnLanguageChanged()
        {
            if (cards.Count > 0) RefreshUpgradeToggle(cards[currentIndex]);
        }

        // ──────────────────────────────────────────────
        #region Public API

        /// <summary>카드 목록과 시작 인덱스를 받아 패널을 엽니다.</summary>
        public void Open(List<CardData> cardList, int startIndex = 0)
        {
            if (cardList == null || cardList.Count == 0) return;
            cards = new List<CardData>(cardList);
            currentIndex = Mathf.Clamp(startIndex, 0, cards.Count - 1);

            // 새 카드 열 때 강화 토글 초기화
            upgradeToggle?.SetIsOnWithoutNotify(false);

            gameObject.SetActive(true);
            DisplayCurrent();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Navigation

        private void NavigateLeft()
        {
            currentIndex = Mathf.Max(0, currentIndex - 1);
            upgradeToggle?.SetIsOnWithoutNotify(false);
            DisplayCurrent();
        }

        private void NavigateRight()
        {
            currentIndex = Mathf.Min(cards.Count - 1, currentIndex + 1);
            upgradeToggle?.SetIsOnWithoutNotify(false);
            DisplayCurrent();
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Display

        private void DisplayCurrent()
        {
            if (cards.Count == 0) { Close(); return; }

            var baseCard = cards[currentIndex];

            // 강화 토글이 켜져 있으면 code+1 카드 조회
            CardData displayCard = baseCard;
            if (upgradeToggle != null && upgradeToggle.isOn)
            {
                var upgraded = CardRegistry.GetCard(baseCard.cardCode + 1);
                if (upgraded != null) displayCard = upgraded;
            }

            // 기존 카드 오브젝트 제거 후 새로 생성
            if (currentCardObj != null) Destroy(currentCardObj);

            if (cardPrefab != null)
            {
                currentCardObj = Instantiate(cardPrefab, transform);
                var rt = currentCardObj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot     = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                }
                var cardUI = currentCardObj.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    cardUI.Initialize(displayCard);
                    cardUI.IsRuntimeUnplayable = true;
                }
            }

            RefreshNavButtons();
            RefreshUpgradeToggle(baseCard);
        }

        private void RefreshNavButtons()
        {
            if (leftButton != null)
            {
                bool canLeft = currentIndex > 0;
                leftButton.interactable = canLeft;
                leftButton.gameObject.SetActive(canLeft);
            }
            if (rightButton != null)
            {
                bool canRight = currentIndex < cards.Count - 1;
                rightButton.interactable = canRight;
                rightButton.gameObject.SetActive(canRight);
            }
        }

        private void RefreshUpgradeToggle(CardData baseCard)
        {
            if (upgradeToggle == null) return;

            // code+1 카드가 존재할 때만 토글 표시
            bool hasUpgrade = CardRegistry.GetCard(baseCard.cardCode + 1) != null;
            upgradeToggle.gameObject.SetActive(hasUpgrade);

            if (upgradeToggleLabel != null)
                upgradeToggleLabel.text = Loc.Get(upgradeToggle.isOn ? "card_view_original" : "card_view_upgraded");
        }

        #endregion
    }
}
