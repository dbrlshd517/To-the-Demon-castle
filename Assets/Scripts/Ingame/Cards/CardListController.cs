using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using DeckRoguelike.Cards;
using DeckRoguelike.Combat;
using DeckRoguelike.Core;

namespace DeckRoguelike.UI
{
    public enum CardListMode { Draw, Discard, Deck, Sub }
    public enum DeckSortMode { Acquired, Type, Cost, Name }
    public enum RestCardMode  { Upgrade, Remove }

    public class CardListController : MonoBehaviour
    {
        [Header("=== 패널 설정 ===")]
        [SerializeField] private CardListMode mode = CardListMode.Draw;

        [Header("=== 그리드 ===")]
        [SerializeField] private ScrollRect  scrollRect;
        [SerializeField] private Transform   cardGridContainer;
        [SerializeField] private GameObject  cardPrefab;
        [SerializeField] private Vector2     cardCellSize     = new Vector2(130f, 190f);
        [SerializeField] private float       cardSpacingX     = 100f;
        [SerializeField] private float       cardSpacingY     = 100f;
        [SerializeField] private int         paddingTopBottom = 500;
        [SerializeField] private int         paddingLeftRight = 0;

        [Header("=== 헤더 ===")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI countText;

        [Header("=== 호버 효과 ===")]
        [SerializeField] private float listHoverScale = 1.3f;

        [Header("=== 닫기 버튼 (Draw / Discard 모드) ===")]
        [SerializeField] private Button closeButton;

        [Header("=== 정렬 버튼 (Deck 모드만) ===")]
        [SerializeField] private GameObject sortButtonGroup;
        [SerializeField] private Button     sortAcquiredButton;
        [SerializeField] private Button     sortTypeButton;
        [SerializeField] private Button     sortCostButton;
        [SerializeField] private Button     sortNameButton;

        [Header("=== Sub 모드 전용 ===")]
        [SerializeField] private CardConfirmPanel cardConfirmPanel;

        // ── 런타임 상태 ──────────────────────────
        private DeckSortMode   currentSort  = DeckSortMode.Acquired;
        private List<CardData> currentCards = new List<CardData>();

        // Sub 전용
        private RestCardMode  currentRestMode;
        private System.Action onFinishCallback;

        // ──────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
            SetupScrollView();
            SetupGridLayout();

            if (mode == CardListMode.Sub) return;

            closeButton?.onClick.AddListener(() =>
            {
                if (mode == CardListMode.Draw)
                    InGameUIController.Instance?.ToggleDrawPanel();
                else if (mode == CardListMode.Discard)
                    InGameUIController.Instance?.ToggleDiscardPanel();
            });

            sortAcquiredButton?.onClick.AddListener(() => SetSort(DeckSortMode.Acquired));
            sortTypeButton?.onClick.AddListener(() => SetSort(DeckSortMode.Type));
            sortCostButton?.onClick.AddListener(() => SetSort(DeckSortMode.Cost));
            sortNameButton?.onClick.AddListener(() => SetSort(DeckSortMode.Name));

            if (sortButtonGroup != null)
                sortButtonGroup.SetActive(mode == CardListMode.Deck);
        }

        private void OnEnable()
        {
            if (mode == CardListMode.Sub) return;

            Refresh();
            if (scrollRect != null)
                scrollRect.normalizedPosition = new Vector2(0f, 1f);
        }

        private void OnDisable()
        {
            if (mode == CardListMode.Draw || mode == CardListMode.Discard)
                InGameUIController.Instance?.SetTopBarButtonsInteractable(true);
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Public API — Sub 모드

        public bool IsConfirmOpen => cardConfirmPanel != null && cardConfirmPanel.gameObject.activeSelf;
        public void CloseConfirm() => cardConfirmPanel?.Close();

        /// <summary>InGameUIController / ShopController에서 호출. SetActive(true) 이후에 호출할 것.</summary>
        public void Setup(RestCardMode restMode, CardConfirmPanel confirmPanel = null, System.Action onFinish = null)
        {
            currentRestMode  = restMode;
            onFinishCallback = onFinish;
            if (confirmPanel != null) cardConfirmPanel = confirmPanel;
            RebuildGrid();
            if (scrollRect != null)
                scrollRect.normalizedPosition = new Vector2(0f, 1f);
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Public API — Draw / Discard / Deck 모드

        public void Refresh()
        {
            currentCards = GetCards();
            RebuildGrid();
            UpdateHeader();
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Card Data

        private List<CardData> GetCards()
        {
            var dm = DeckManager.Instance;
            if (dm == null) return new List<CardData>();

            return mode switch
            {
                CardListMode.Draw    => SortByRarity(dm.GetDrawPileForView()),
                CardListMode.Discard => SortByRarity(dm.GetDiscardPileForView()),
                CardListMode.Deck    => ApplySort(dm.MasterDeck, currentSort),
                _                    => new List<CardData>()
            };
        }

        private static List<CardData> SortByRarity(List<CardData> cards) =>
            cards.OrderByDescending(c => c.RarityDigit).ThenBy(c => c.cardName).ToList();

        private static List<CardData> ApplySort(List<CardData> cards, DeckSortMode sort) =>
            sort switch
            {
                DeckSortMode.Type => cards.OrderBy(c => c.TypeDigit).ThenBy(c => c.cardName).ToList(),
                DeckSortMode.Cost => cards.OrderBy(c => c.energyCost).ThenBy(c => c.cardName).ToList(),
                DeckSortMode.Name => cards.OrderBy(c => c.cardName).ToList(),
                _                 => new List<CardData>(cards)
            };

        private void SetSort(DeckSortMode sort)
        {
            currentSort = sort;
            currentCards = GetCards();
            RebuildGrid();
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Grid

        private void RebuildGrid()
        {
            if (cardGridContainer == null || cardPrefab == null) return;

            var toDestroy = new List<GameObject>();
            foreach (Transform child in cardGridContainer) toDestroy.Add(child.gameObject);
            foreach (var obj in toDestroy) { obj.transform.SetParent(null); Destroy(obj); }

            if (mode == CardListMode.Sub)
            {
                RebuildSubGrid();
                return;
            }

            for (int i = 0; i < currentCards.Count; i++)
            {
                int capturedIndex = i;
                var item   = Instantiate(cardPrefab, cardGridContainer);
                var cardUI = item.GetComponent<CardUI>();
                if (cardUI == null) continue;

                cardUI.Initialize(currentCards[i]);
                cardUI.IsRuntimeUnplayable = true;

                if (mode == CardListMode.Deck)
                {
                    cardUI.SetClickable(true);
                    cardUI.OnCardViewClicked += _ => OpenCardInfo(capturedIndex);
                }
                else
                {
                    cardUI.SetClickable(false);
                }

                AddListHoverEffect(item);
            }

            if (cardGridContainer is RectTransform rt)
            {
                var grid = cardGridContainer.GetComponent<GridLayoutGroup>();
                if (grid != null) StartCoroutine(ApplyCenteredPadding(grid));
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }

        private void RebuildSubGrid()
        {
            var dm = DeckManager.Instance;
            if (dm == null) return;

            var cards = currentRestMode == RestCardMode.Upgrade
                ? dm.MasterDeck.Where(c => CardRegistry.GetCard(c.cardCode + 1) != null).ToList()
                : new List<CardData>(dm.MasterDeck);

            foreach (var card in cards)
            {
                var captured = card;
                var item   = Instantiate(cardPrefab, cardGridContainer);
                var cardUI = item.GetComponent<CardUI>();
                if (cardUI == null) continue;

                cardUI.Initialize(card);
                cardUI.IsRuntimeUnplayable = true;
                cardUI.SetClickable(true);
                cardUI.OnCardViewClicked += _ => OnSubCardClicked(captured);
                AddListHoverEffect(item);
            }

            if (cardGridContainer is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private void OnSubCardClicked(CardData card)
        {
            cardConfirmPanel?.Open(card, currentRestMode, OnSubConfirmed);
        }

        private void OnSubConfirmed(CardData card)
        {
            if (currentRestMode == RestCardMode.Upgrade)
                DeckManager.Instance?.UpgradeCard(card);
            else
                DeckManager.Instance?.RemoveCardFromDeck(card);

            InGameUIController.Instance?.CloseRestCardList();

            if (onFinishCallback != null)
                onFinishCallback.Invoke();
            else
                FinishRestAction();
        }

        private void FinishRestAction()
        {
            FindObjectOfType<CombatController>()?.CleanupRestArea();
            InGameUIController.Instance?.OnNodeComplete();
        }

        private void OpenCardInfo(int index)
        {
            CardInfoController.Instance?.Open(currentCards, index);
        }

        private void AddListHoverEffect(GameObject cardObj)
        {
            var trigger = cardObj.GetComponent<EventTrigger>() ?? cardObj.AddComponent<EventTrigger>();

            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener(_ => cardObj.transform.localScale = Vector3.one * listHoverScale);
            trigger.triggers.Add(enterEntry);

            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener(_ => cardObj.transform.localScale = Vector3.one);
            trigger.triggers.Add(exitEntry);
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Header

        private void UpdateHeader()
        {
            if (titleText != null)
                titleText.text = mode switch
                {
                    CardListMode.Draw    => "드로우 더미",
                    CardListMode.Discard => "버린 카드",
                    CardListMode.Deck    => "덱 보기",
                    _                    => string.Empty
                };

            if (countText != null)
                countText.text = $"{currentCards.Count}";
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Setup

        private void SetupScrollView()
        {
            if (scrollRect == null && cardGridContainer != null)
                scrollRect = cardGridContainer.GetComponentInParent<ScrollRect>();

            if (scrollRect == null) return;

            scrollRect.horizontal        = false;
            scrollRect.vertical          = true;
            scrollRect.movementType      = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 30f;

            if (cardGridContainer is RectTransform content)
            {
                content.anchorMin        = new Vector2(0f, 1f);
                content.anchorMax        = new Vector2(1f, 1f);
                content.pivot            = new Vector2(0.5f, 1f);
                content.anchoredPosition = Vector2.zero;
                content.sizeDelta        = new Vector2(0f, content.sizeDelta.y);
            }
        }

        private void SetupGridLayout()
        {
            if (cardGridContainer == null) return;

            var grid = cardGridContainer.GetComponent<GridLayoutGroup>()
                    ?? cardGridContainer.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.cellSize        = cardCellSize;
            grid.spacing         = new Vector2(cardSpacingX, cardSpacingY);
            grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis       = GridLayoutGroup.Axis.Horizontal;

            var fitter = cardGridContainer.GetComponent<ContentSizeFitter>()
                      ?? cardGridContainer.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            if (mode == CardListMode.Sub)
            {
                grid.childAlignment = TextAnchor.UpperCenter;
                grid.padding        = new RectOffset(0, 0, paddingTopBottom, paddingTopBottom);
            }
            else
            {
                grid.childAlignment = TextAnchor.UpperLeft;
                StartCoroutine(ApplyCenteredPadding(grid));
            }
        }

        private System.Collections.IEnumerator ApplyCenteredPadding(GridLayoutGroup grid)
        {
            yield return null;

            if (cardGridContainer is not RectTransform rt) yield break;

            float containerWidth = rt.rect.width;
            float gridWidth      = 5 * cardCellSize.x + 4 * cardSpacingX;
            int   autoPad        = Mathf.Max(0, Mathf.RoundToInt((containerWidth - gridWidth) / 2f));

            grid.padding = new RectOffset(autoPad + paddingLeftRight, autoPad + paddingLeftRight,
                                          paddingTopBottom, paddingTopBottom);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        #endregion
    }
}
