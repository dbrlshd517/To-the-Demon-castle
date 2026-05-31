using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DeckRoguelike.Cards;
using DeckRoguelike.Core;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 덱, 버린 카드 더미, 소멸 카드 더미를 관리
    /// </summary>
    public class DeckManager : MonoBehaviour
    {
        public static DeckManager Instance { get; private set; }

        [Header("Starting Deck")]
        [Tooltip("CharacterData가 없을 때 사용하는 폴백 덱")]
        [SerializeField] private List<CardData> fallbackStarterDeck;

        [Header("UI References")]
        [SerializeField] private TMPro.TextMeshProUGUI drawPileCountText;
        [SerializeField] private TMPro.TextMeshProUGUI discardPileCountText;
        [SerializeField] private TMPro.TextMeshProUGUI exhaustPileCountText;

        // Card Piles
        private List<CardData> masterDeck = new List<CardData>();     // 전체 덱 (런 진행 중 변하지 않음)
        private List<CardData> drawPile = new List<CardData>();       // 드로우 더미
        private List<CardData> discardPile = new List<CardData>();    // 버린 카드 더미
        private List<CardData> exhaustPile = new List<CardData>();    // 소멸 카드 더미

        // Events
        public event System.Action<int> OnDrawPileChanged;
        public event System.Action<int> OnDiscardPileChanged;
        public event System.Action<int> OnExhaustPileChanged;
        public event System.Action OnDeckShuffled;

        // Properties
        public int DrawPileCount => drawPile.Count;
        public int DiscardPileCount => discardPile.Count;
        public int ExhaustPileCount => exhaustPile.Count;
        public int MasterDeckCount => masterDeck.Count;
        public List<CardData> MasterDeck => new List<CardData>(masterDeck);

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            InitializeDeck();
        }

        /// <summary>
        /// 덱 초기화
        /// </summary>
        public void InitializeDeck()
        {
            masterDeck.Clear();
            drawPile.Clear();
            discardPile.Clear();
            exhaustPile.Clear();

            List<CardData> source = null;

            // 캐릭터별 커스텀 스타팅 덱 코드가 있으면 우선 사용
            var customCodes = GameManager.Instance?.StartingDeckCodes;
            if (customCodes != null && customCodes.Length > 0)
            {
                source = CardRegistry.GetCards(new List<int>(customCodes));
            }
            else
            {
                var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
                int c = character switch
                {
                    CharacterType.Warrior => 2,
                    CharacterType.Gunner  => 3,
                    CharacterType.Mage    => 4,
                    _                     => 1,
                };
                var codes = new List<int>();
                for (int i = 0; i < 2; i++) codes.Add(c * 10000 + 1100); // 액션 첫번째 카드 2장
                codes.Add(c * 10000 + 2100);                              // 이동 첫번째 카드 1장
                source = CardRegistry.GetCards(codes);
            }

            if (source == null || source.Count == 0)
            {
                Debug.LogWarning("[DeckManager] 스타터 덱 없음. fallbackStarterDeck 사용.");
                source = fallbackStarterDeck;
            }

            foreach (var card in source)
            {
                if (card != null)
                {
                    CardData cardCopy = card.Clone();
                    masterDeck.Add(cardCopy);
                    drawPile.Add(cardCopy);
                }
            }

            UpdateUI();
            Debug.Log($"[DeckManager] 덱 초기화 완료 - {masterDeck.Count}장");
        }

        /// <summary>
        /// 덱 셔플
        /// </summary>
        public void ShuffleDeck()
        {
            var rng = DeckRoguelike.Core.GameManager.Instance?.Rng;
            // Fisher-Yates 셔플
            for (int i = drawPile.Count - 1; i > 0; i--)
            {
                int randomIndex = rng != null
                    ? rng.CardShuffleRange(i + 1)
                    : Random.Range(0, i + 1);
                CardData temp = drawPile[i];
                drawPile[i] = drawPile[randomIndex];
                drawPile[randomIndex] = temp;
            }

            OnDeckShuffled?.Invoke();
            Debug.Log("[DeckManager] 덱 셔플 완료");
        }

        /// <summary>
        /// 카드 드로우
        /// </summary>
        public CardData DrawCard()
        {
            // 드로우 더미가 비어있으면 버린 카드 더미를 섞어서 가져옴
            if (drawPile.Count == 0)
            {
                if (discardPile.Count == 0)
                {
                    Debug.Log("[DeckManager] 드로우할 카드가 없습니다.");
                    return null;
                }

                ReshuffleDiscardIntoDraw();
            }

            if (drawPile.Count == 0) return null;

            CardData drawnCard = drawPile[0];
            drawPile.RemoveAt(0);

            UpdateUI();
            OnDrawPileChanged?.Invoke(drawPile.Count);

            return drawnCard;
        }

        /// <summary>
        /// 버린 카드 더미를 드로우 더미로 리셔플
        /// </summary>
        private void ReshuffleDiscardIntoDraw()
        {
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            ShuffleDeck();

            UpdateUI();
            OnDiscardPileChanged?.Invoke(discardPile.Count);
            OnDrawPileChanged?.Invoke(drawPile.Count);

            Debug.Log("[DeckManager] 버린 카드 더미를 드로우 더미로 리셔플");
        }

        /// <summary>
        /// 버린 카드 더미에 추가
        /// </summary>
        public void AddToDiscardPile(CardData card)
        {
            if (card == null) return;

            discardPile.Add(card);
            UpdateUI();
            OnDiscardPileChanged?.Invoke(discardPile.Count);
        }

        /// <summary>
        /// 카드 소멸
        /// </summary>
        public void ExhaustCard(CardData card)
        {
            if (card == null) return;

            exhaustPile.Add(card);
            UpdateUI();
            OnExhaustPileChanged?.Invoke(exhaustPile.Count);

            // 971 유물 등 — 카드 소멸 시 유물 훅 발화
            if (GameManager.Instance != null)
            {
                var combat = FindObjectOfType<DeckRoguelike.UI.BoardController>();
                var ctx = new DeckRoguelike.Relic.RelicCombatContext { Board = combat };
                foreach (var relic in GameManager.Instance.Relics)
                    relic.OnCardExhausted(ctx, card);
            }

            Debug.Log($"[DeckManager] 카드 소멸: {card.cardName}");
        }

        /// <summary>
        /// 덱에 카드 추가 (보상 등)
        /// </summary>
        public void AddCardToDeck(CardData card)
        {
            if (card == null) return;

            CardData cardCopy = card.Clone();

            // 212~215 유물 - 획득 카드 자동 강화 훅
            if (GameManager.Instance != null)
            {
                CardData replacement = null;
                foreach (var relic in GameManager.Instance.Relics)
                {
                    relic.OnCardObtained(cardCopy, ref replacement);
                    if (replacement != null)
                    {
                        cardCopy = replacement;
                        replacement = null;
                    }
                }
            }

            masterDeck.Add(cardCopy);
            discardPile.Add(cardCopy);
            DeckRoguelike.Core.DiscoveryManager.DiscoverCard(cardCopy.cardCode);

            UpdateUI();
            OnDiscardPileChanged?.Invoke(discardPile.Count);

            Debug.Log($"[DeckManager] 덱에 카드 추가: {cardCopy.cardName}");
        }

        /// <summary>
        /// 덱에서 카드 제거
        /// </summary>
        public void RemoveCardFromDeck(CardData card)
        {
            if (card == null) return;

            // 마스터 덱에서 제거
            CardData toRemove = masterDeck.FirstOrDefault(c => c.cardCode == card.cardCode);
            if (toRemove != null)
            {
                masterDeck.Remove(toRemove);

                // 드로우 더미에서도 제거
                drawPile.RemoveAll(c => c.cardCode == card.cardCode);
                // 버린 카드 더미에서도 제거
                discardPile.RemoveAll(c => c.cardCode == card.cardCode);

                UpdateUI();
                Debug.Log($"[DeckManager] 덱에서 카드 제거: {card.cardName}");
            }
        }

        /// <summary>
        /// 카드 업그레이드
        /// </summary>
        public void UpgradeCard(CardData card)
        {
            if (card == null || card.IsUpgraded) return;

            int upgradedCode = card.UpgradedCode;
            CardData upgraded = CardRegistry.GetCard(upgradedCode);
            if (upgraded == null)
            {
                Debug.LogWarning($"[DeckManager] 업그레이드 카드({upgradedCode})를 찾을 수 없습니다.");
                return;
            }

            int index = masterDeck.FindIndex(c => c.cardCode == card.cardCode);
            if (index >= 0)
            {
                CardData upgradedCopy = upgraded.Clone();
                masterDeck[index] = upgradedCopy;

                int drawIndex = drawPile.FindIndex(c => c.cardCode == card.cardCode);
                if (drawIndex >= 0) drawPile[drawIndex] = upgradedCopy;

                int discardIndex = discardPile.FindIndex(c => c.cardCode == card.cardCode);
                if (discardIndex >= 0) discardPile[discardIndex] = upgradedCopy;

                Debug.Log($"[DeckManager] 카드 업그레이드: {card.cardName}({card.cardCode}) → {upgradedCopy.cardName}({upgradedCode})");
            }
        }

        /// <summary>
        /// 임시 강화된 카드를 원래(강화 전) 카드로 되돌립니다.
        /// upgradedCard 인스턴스를 master/draw/discard/exhaust 더미에서 찾아 originalDef.Clone()으로 교체.
        /// 인스턴스 단위 비교로 이미 영구 강화된 동일 코드 카드는 건드리지 않습니다.
        /// </summary>
        public void DowngradeCard(CardData upgradedCard, CardData originalDef)
        {
            if (upgradedCard == null || originalDef == null) return;

            CardData replacement = originalDef.Clone();
            bool replaced = false;

            int mIdx = masterDeck.IndexOf(upgradedCard);
            if (mIdx >= 0) { masterDeck[mIdx] = replacement; replaced = true; }

            int dIdx = drawPile.IndexOf(upgradedCard);
            if (dIdx >= 0) { drawPile[dIdx] = replacement; replaced = true; }

            int discIdx = discardPile.IndexOf(upgradedCard);
            if (discIdx >= 0) { discardPile[discIdx] = replacement; replaced = true; }

            int eIdx = exhaustPile.IndexOf(upgradedCard);
            if (eIdx >= 0) { exhaustPile[eIdx] = replacement; replaced = true; }

            if (replaced)
                Debug.Log($"[DeckManager] 카드 다운그레이드: {upgradedCard.cardName}({upgradedCard.cardCode}) → {replacement.cardName}({replacement.cardCode})");
        }

        /// <summary>
        /// 드로우 더미 맨 위에 카드 추가
        /// </summary>
        public void AddToTopOfDrawPile(CardData card)
        {
            if (card == null) return;

            drawPile.Insert(0, card);
            UpdateUI();
            OnDrawPileChanged?.Invoke(drawPile.Count);
        }

        /// <summary>
        /// 드로우 더미에서 특정 카드 검색
        /// </summary>
        public CardData SearchDrawPile(System.Predicate<CardData> predicate)
        {
            return drawPile.Find(predicate);
        }

        /// <summary>
        /// 버린 카드 더미에서 무작위 카드 가져오기
        /// </summary>
        public CardData GetRandomFromDiscard()
        {
            if (discardPile.Count == 0) return null;

            int randomIndex = Random.Range(0, discardPile.Count);
            CardData card = discardPile[randomIndex];
            discardPile.RemoveAt(randomIndex);

            UpdateUI();
            OnDiscardPileChanged?.Invoke(discardPile.Count);

            return card;
        }

        /// <summary>
        /// 전투 종료 시 덱 리셋
        /// </summary>
        public void ResetForNewCombat()
        {
            drawPile.Clear();
            discardPile.Clear();
            exhaustPile.Clear();

            // 마스터 덱에서 복사
            foreach (var card in masterDeck)
            {
                drawPile.Add(card);
            }

            ShuffleDeck();

            // innate 카드를 드로우 더미 맨 앞으로 이동 → 첫 손패에 반드시 포함
            var innateCards = drawPile.Where(c => c.Innate).ToList();
            foreach (var card in innateCards)
                drawPile.Remove(card);
            for (int i = innateCards.Count - 1; i >= 0; i--)
                drawPile.Insert(0, innateCards[i]);

            UpdateUI();

            Debug.Log("[DeckManager] 전투용 덱 리셋 완료");
        }

        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            if (drawPileCountText != null)
                drawPileCountText.text = drawPile.Count.ToString();

            if (discardPileCountText != null)
                discardPileCountText.text = discardPile.Count.ToString();

            if (exhaustPileCountText != null)
                exhaustPileCountText.text = exhaustPile.Count.ToString();
        }

        /// <summary>
        /// 덱 뷰어용 카드 리스트 반환
        /// </summary>
        public List<CardData> GetDrawPileForView()
        {
            // 정렬된 복사본 반환 (실제 순서 노출 방지)
            return drawPile.OrderBy(c => c.cardName).ToList();
        }

        /// <summary>
        /// 207 유물 전용: 드로우 더미를 실제 순서대로 반환합니다 (다음에 뽑힐 카드가 0번 인덱스).
        /// </summary>
        public List<CardData> GetDrawPileInOrder()
        {
            return new List<CardData>(drawPile);
        }

        public List<CardData> GetDiscardPileForView()
        {
            return new List<CardData>(discardPile);
        }

        /// <summary>207/208 포션: 드로우 더미에서 지정 카드를 제거합니다.</summary>
        public bool RemoveFromDrawPile(CardData card)
        {
            if (card == null) return false;
            bool ok = drawPile.Remove(card);
            if (ok) OnDrawPileChanged?.Invoke(drawPile.Count);
            return ok;
        }

        /// <summary>207/208 포션: 버린 더미에서 지정 카드를 제거합니다.</summary>
        public bool RemoveFromDiscardPile(CardData card)
        {
            if (card == null) return false;
            bool ok = discardPile.Remove(card);
            if (ok) OnDiscardPileChanged?.Invoke(discardPile.Count);
            return ok;
        }

        public void MoveDiscardToDrawPile()
        {
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            OnDiscardPileChanged?.Invoke(0);
            OnDrawPileChanged?.Invoke(drawPile.Count);
        }

        public List<CardData> GetExhaustPileForView()
        {
            return new List<CardData>(exhaustPile);
        }
    }
}
