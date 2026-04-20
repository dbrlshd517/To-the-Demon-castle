using UnityEngine;
using System.Collections.Generic;
using DeckRoguelike.Cards;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 손패의 카드 배치와 관리를 담당
    /// CombatController와 이벤트로 통신
    /// </summary>
    public class HandManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DeckManager deckManager;
        [SerializeField] private Transform handContainer;
        [SerializeField] private GameObject cardPrefab;

        [Header("Hand Layout")]
        [SerializeField] private float cardSpacing = 120f;
        [SerializeField] private float maxHandWidth = 800f;
        [SerializeField] private float cardArcHeight = 30f;
        [SerializeField] private float cardRotationAngle = 5f;
        [SerializeField] private int maxHandSize = 10;
        [Header("Animation")]
        [SerializeField] private float drawAnimationDuration = 0.3f;
        [SerializeField] private Transform drawPosition;
        [SerializeField] private Transform discardPosition;

        private List<CardUI> handCards = new List<CardUI>();

        // Events

        public event System.Action<int> OnHandSizeChanged;
        public event System.Action<CardUI> OnCardDrawn;
        public event System.Action<CardUI> OnCardDiscarded;
        
        /// <summary>
        /// 카드 사용 시도 이벤트 - CombatController가 구독
        /// </summary>
        public event System.Action<CardData, int> OnCardPlayAttempted;

        public int HandSize => handCards.Count;
        public List<CardUI> HandCards => handCards;

        private void Start()
        {
            if (deckManager == null)
                deckManager = FindObjectOfType<DeckManager>();
        }

        /// <summary>
        /// 카드 드로우
        /// </summary>
        public void DrawCards(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (handCards.Count >= maxHandSize)
                {
                    Debug.Log("[HandManager] 손패가 가득 찼습니다.");
                    break;
                }

                CardData cardData = deckManager?.DrawCard();
                if (cardData == null)
                {
                    Debug.Log("[HandManager] 덱에 카드가 없습니다.");
                    break;
                }

                CreateCardInHand(cardData);
            }

            UpdateHandLayout();
            OnHandSizeChanged?.Invoke(handCards.Count);
        }

        /// <summary>
        /// 손패에 카드 생성
        /// </summary>
        private CardUI CreateCardInHand(CardData cardData)
        {
            if (cardPrefab == null || handContainer == null)
            {
                Debug.LogError("[HandManager] cardPrefab 또는 handContainer가 설정되지 않았습니다.");
                return null;
            }

            GameObject cardObj = Instantiate(cardPrefab, handContainer);
            CardUI cardUI = cardObj.GetComponent<CardUI>();

            if (cardUI != null)
            {
                cardUI.Initialize(cardData);
                cardUI.HandIndex = handCards.Count;
                cardUI.OnCardPlayed += HandleCardPlayed;

                // 드로우 위치에서 시작
                if (drawPosition != null)
                {
                    cardObj.transform.position = drawPosition.position;
                }

                handCards.Add(cardUI);
                OnCardDrawn?.Invoke(cardUI);
            }

            return cardUI;
        }

        /// <summary>
        /// 손패 레이아웃 업데이트 (부채꼴 배치)
        /// </summary>
        public void UpdateHandLayout()
        {
            int cardCount = handCards.Count;
            if (cardCount == 0) return;

            // 카드 간격 계산
            float totalWidth = Mathf.Min(cardSpacing * (cardCount - 1), maxHandWidth);
            float actualSpacing = cardCount > 1 ? totalWidth / (cardCount - 1) : 0;
            float startX = -totalWidth / 2f;

            for (int i = 0; i < cardCount; i++)
            {
                CardUI card = handCards[i];
                card.HandIndex = i;

                // X 위치
                float xPos = startX + (actualSpacing * i);

                // Y 위치 (포물선 아크: 중앙이 제일 높고 가장자리로 갈수록 내려감)
                float normalizedPos = cardCount > 1 ? (float)i / (cardCount - 1) : 0.5f;
                float t = normalizedPos - 0.5f; // -0.5 ~ 0.5
                float yPos = -(t * t * 4f) * cardArcHeight;

                // 회전 (부채꼴)
                float rotationZ = Mathf.Lerp(cardRotationAngle, -cardRotationAngle, normalizedPos);

                Vector3 targetPosition = new Vector3(xPos, yPos, 0);
                Quaternion targetRotation = Quaternion.Euler(0, 0, rotationZ);

                card.SetHandPosition(targetPosition, targetRotation);
            }
        }

        /// <summary>
        /// 카드 사용 처리 - 이벤트로 CombatController에 알림
        /// </summary>
        private void HandleCardPlayed(CardUI cardUI)
        {
            // CombatController에 카드 사용 시도 알림
            OnCardPlayAttempted?.Invoke(cardUI.CardData, cardUI.HandIndex);
        }

        /// <summary>
        /// 카드 사용 성공 시 호출 (CombatController에서 호출)
        /// </summary>
        public void OnCardPlaySuccess(int handIndex)
        {
            if (handIndex < 0 || handIndex >= handCards.Count) return;

            CardUI cardUI = handCards[handIndex];
            handCards.RemoveAt(handIndex);
            
            if (cardUI != null && cardUI.gameObject != null)
            {
                Destroy(cardUI.gameObject);
            }

            UpdateHandLayout();
            OnHandSizeChanged?.Invoke(handCards.Count);
        }

        /// <summary>
        /// 카드 사용 실패 시 호출 (CombatController에서 호출)
        /// </summary>
        public void OnCardPlayFailed(int handIndex)
        {
            if (handIndex < 0 || handIndex >= handCards.Count) return;

            CardUI cardUI = handCards[handIndex];
            cardUI?.ReturnToHand();
        }

        /// <summary>
        /// 손패 전체 버리기
        /// </summary>
        public void DiscardHand()
        {
            foreach (var card in handCards)
            {
                if (card.CardData.Retain) continue; // retain 카드는 유지

                // ethereal 카드는 버릴 때 소멸 더미로
                if (card.CardData.Ethereal)
                    deckManager?.ExhaustCard(card.CardData);
                else
                    deckManager?.AddToDiscardPile(card.CardData);

                OnCardDiscarded?.Invoke(card);

                if (card != null && card.gameObject != null)
                    Destroy(card.gameObject);
            }

            // retain 카드만 남기기
            handCards.RemoveAll(c => c == null || c.gameObject == null || !c.CardData.Retain);
            UpdateHandLayout();
            OnHandSizeChanged?.Invoke(handCards.Count);
        }

        /// <summary>
        /// 특정 카드 버리기
        /// </summary>
        public void DiscardCard(CardUI cardUI)
        {
            if (cardUI == null || !handCards.Contains(cardUI)) return;

            handCards.Remove(cardUI);

            // ethereal 카드는 버릴 때 소멸 더미로
            if (cardUI.CardData.Ethereal)
                deckManager?.ExhaustCard(cardUI.CardData);
            else
                deckManager?.AddToDiscardPile(cardUI.CardData);

            OnCardDiscarded?.Invoke(cardUI);

            Destroy(cardUI.gameObject);
            UpdateHandLayout();
            OnHandSizeChanged?.Invoke(handCards.Count);
        }

        /// <summary>
        /// 특정 카드 소멸
        /// </summary>
        public void ExhaustCard(CardUI cardUI)
        {
            if (cardUI == null || !handCards.Contains(cardUI)) return;

            handCards.Remove(cardUI);
            deckManager?.ExhaustCard(cardUI.CardData);

            Destroy(cardUI.gameObject);
            UpdateHandLayout();
            OnHandSizeChanged?.Invoke(handCards.Count);
        }

        /// <summary>
        /// 손패에 카드 직접 추가
        /// </summary>
        public void AddCardToHand(CardData cardData)
        {
            if (handCards.Count >= maxHandSize)
            {
                Debug.Log("[HandManager] 손패가 가득 찼습니다. 카드가 버려집니다.");
                deckManager?.AddToDiscardPile(cardData);
                return;
            }

            CreateCardInHand(cardData);
            UpdateHandLayout();
            OnHandSizeChanged?.Invoke(handCards.Count);
        }

        /// <summary>
        /// 손패 카드의 런타임 불가 상태 초기화 (턴 시작 시 호출)
        /// </summary>
        public void ResetRuntimeUnplayable()
        {
            foreach (var card in handCards)
            {
                card.IsRuntimeUnplayable = false;
            }
        }

        /// <summary>
        /// 카드 플레이 가능 상태 업데이트
        /// </summary>
        public void UpdateCardPlayability(int currentEnergy)
        {
            foreach (var card in handCards)
            {
                bool canPlay = currentEnergy >= card.CardData.energyCost && !card.CardData.IsUnplayable && !card.IsRuntimeUnplayable;
                card.SetPlayable(canPlay);
            }
        }

        /// <summary>
        /// 손패 초기화
        /// </summary>
        public void ClearHand()
        {
            foreach (var card in handCards)
            {
                if (card != null && card.gameObject != null)
                {
                    Destroy(card.gameObject);
                }
            }
            handCards.Clear();
            OnHandSizeChanged?.Invoke(0);
        }
    }
}