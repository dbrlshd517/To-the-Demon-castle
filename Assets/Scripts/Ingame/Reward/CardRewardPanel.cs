using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using DeckRoguelike.Cards;
using DeckRoguelike.Core;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 전투 보상 카드 선택 패널.
    /// 캐릭터 클래스에 맞는 카드를 N장 표시하고 하나를 선택합니다.
    ///
    /// • 스킵 시 카드 목록을 유지해 상자를 다시 클릭하면 동일 카드가 표시됩니다.
    /// • 카드 선택 시 카드 목록을 초기화하고 다음 전투를 대비합니다.
    /// • ResetForNewCombat()을 전투 시작 시 호출하면 카드 목록이 완전히 초기화됩니다.
    ///
    /// [Unity 구조]
    /// CardRewardPanel
    ///  ├─ CardContainer (HorizontalLayoutGroup) ← cardContainer
    ///  └─ SkipButton    (Button)               ← skipButton
    /// </summary>
    public class CardRewardPanel : MonoBehaviour
    {
        [Header("=== UI ===")]
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private Button        skipButton;
        [SerializeField] private GameObject    cardPrefab;

        [Header("=== 설정 ===")]
        [SerializeField] private int     cardChoices = 3;
        [SerializeField] private Vector2 cardSize    = new Vector2(180f, 260f);
        [SerializeField] private float   cardSpacing = 30f;
        /// <summary>패널 중앙 기준으로 카드 컨테이너를 위로 올리는 오프셋 (양수 = 위)</summary>
        [SerializeField] private float   cardOffsetY = 80f;

        private Action<CardData> onSelected;
        /// <summary>카드가 이미 생성되어 스킵으로 보존 중인지 여부.</summary>
        private bool cardsPending = false;

        private void Awake()
        {
            skipButton?.onClick.AddListener(OnSkipClicked);
            SetupLayout();
        }

        // ──────────────────────────────────────────────

        /// <summary>
        /// 패널을 엽니다.
        /// cardsPending이 true(스킵 후 재오픈)이면 기존 카드를 그대로 유지합니다.
        /// </summary>
        public void Open(Action<CardData> callback)
        {
            onSelected = callback;
            gameObject.SetActive(true);

            if (!cardsPending)
            {
                BuildCards();
                cardsPending = true;
            }
        }

        /// <summary>
        /// 상점 구매 시 사용. 이미 구매된 카드 1장을 보여주고 클릭하면 callback 호출.
        /// 스킵 버튼은 숨깁니다 (골드 이미 소비됨).
        /// </summary>
        public void OpenWithSpecificCard(CardData card, Action<CardData> callback)
        {
            onSelected   = callback;
            cardsPending = false;
            gameObject.SetActive(true);

            if (skipButton != null) skipButton.gameObject.SetActive(false);

            if (cardContainer != null)
                foreach (Transform child in cardContainer) Destroy(child.gameObject);

            if (cardPrefab != null && card != null)
            {
                var obj = Instantiate(cardPrefab, cardContainer);
                var le  = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
                le.preferredWidth  = cardSize.x;
                le.preferredHeight = cardSize.y;
                le.minWidth        = cardSize.x;
                le.minHeight       = cardSize.y;

                var cardUI = obj.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    cardUI.Initialize(card);
                    cardUI.IsRuntimeUnplayable = true;
                    cardUI.OnCardViewClicked += _ => OnCardChosen(card);
                }
                if (cardContainer != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(cardContainer);
            }
        }

        /// <summary>
        /// 아이템 효과 등에서 특정 카드 목록을 직접 제공해 선택하게 합니다.
        /// 선택 후 callback(card), 취소 시 callback(null).
        /// 스킵 버튼은 표시하지 않습니다.
        /// </summary>
        public void OpenWithCards(List<CardData> cards, Action<CardData> callback)
        {
            onSelected   = callback;
            cardsPending = false;
            gameObject.SetActive(true);

            if (skipButton != null) skipButton.gameObject.SetActive(false);

            if (cardContainer != null)
                foreach (Transform child in cardContainer) Destroy(child.gameObject);

            if (cardPrefab == null || cards == null) return;

            foreach (var card in cards)
            {
                var captured = card;
                var obj = Instantiate(cardPrefab, cardContainer);

                var le = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
                le.preferredWidth  = cardSize.x;
                le.preferredHeight = cardSize.y;
                le.minWidth        = cardSize.x;
                le.minHeight       = cardSize.y;

                var cardUI = obj.GetComponent<CardUI>();
                if (cardUI == null) continue;
                cardUI.Initialize(captured);
                cardUI.IsRuntimeUnplayable  = true;
                cardUI.OnCardViewClicked   += _ => OnCardChosen(captured);
            }

            if (cardContainer != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(cardContainer);
        }

        /// <summary>새 전투 시작 시 호출해 카드 목록을 초기화합니다.</summary>
        public void ResetForNewCombat()
        {
            cardsPending = false;
            if (cardContainer != null)
                foreach (Transform child in cardContainer)
                    Destroy(child.gameObject);
        }

        // ──────────────────────────────────────────────

        private void SetupLayout()
        {
            if (cardContainer != null)
            {
                cardContainer.anchorMin        = new Vector2(0.5f, 0.5f);
                cardContainer.anchorMax        = new Vector2(0.5f, 0.5f);
                cardContainer.pivot            = new Vector2(0.5f, 0.5f);
                cardContainer.anchoredPosition = new Vector2(0f, cardOffsetY);

                var hlg = cardContainer.GetComponent<HorizontalLayoutGroup>()
                       ?? cardContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing                = cardSpacing;
                hlg.childAlignment         = TextAnchor.MiddleCenter;
                hlg.childControlWidth      = true;
                hlg.childControlHeight     = true;
                hlg.childForceExpandWidth  = false;
                hlg.childForceExpandHeight = false;

                var fitter = cardContainer.GetComponent<ContentSizeFitter>()
                          ?? cardContainer.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            }

            if (skipButton != null)
            {
                var rt = skipButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin        = new Vector2(0.5f, 0.5f);
                    rt.anchorMax        = new Vector2(0.5f, 0.5f);
                    rt.pivot            = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0f, cardOffsetY - cardSize.y - 50f);
                }
            }
        }

        private void BuildCards()
        {
            if (cardContainer == null || cardPrefab == null) return;

            foreach (Transform child in cardContainer)
                Destroy(child.gameObject);

            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            var pool      = CardRegistry.GetRewardPool(character);
            Shuffle(pool);

            int count = Mathf.Min(cardChoices, pool.Count);
            for (int i = 0; i < count; i++)
            {
                var captured = pool[i];
                var obj      = Instantiate(cardPrefab, cardContainer);

                var le = obj.GetComponent<LayoutElement>()
                      ?? obj.AddComponent<LayoutElement>();
                le.preferredWidth  = cardSize.x;
                le.preferredHeight = cardSize.y;
                le.minWidth        = cardSize.x;
                le.minHeight       = cardSize.y;

                var cardUI = obj.GetComponent<CardUI>();
                if (cardUI == null) continue;

                cardUI.Initialize(captured);
                cardUI.IsRuntimeUnplayable = true;
                cardUI.OnCardViewClicked   += _ => OnCardChosen(captured);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(cardContainer);
        }

        private void OnCardChosen(CardData card)
        {
            cardsPending = false; // 카드 선택 → 다음 오픈 시 새로 생성
            // OnPointerDown 중 SetActive 호출 시 EventSystem이 깨지므로 1프레임 뒤 실행
            StartCoroutine(InvokeNextFrame(card));
        }

        private System.Collections.IEnumerator InvokeNextFrame(CardData card)
        {
            yield return null;
            // skipButton이 숨겨졌을 수 있으므로 복원
            if (skipButton != null) skipButton.gameObject.SetActive(true);
            onSelected?.Invoke(card);
        }

        private void OnSkipClicked()
        {
            // cardsPending 유지 → 상자 재클릭 시 동일 카드 표시
            onSelected?.Invoke(null);
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
