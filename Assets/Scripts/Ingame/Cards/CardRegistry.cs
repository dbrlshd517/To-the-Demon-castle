using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using DeckRoguelike.Cards;

namespace DeckRoguelike.Core
{
    /// <summary>
    /// Resources/Cards 폴더의 CardData를 자동 로드하여 code로 조회.
    /// 수동으로 CardDatabase에 드래그할 필요 없음.
    /// </summary>
    public static class CardRegistry
    {
        private static Dictionary<int, CardData> lookup;
        private static List<CardData> allCards;

        private static void EnsureLoaded()
        {
            if (lookup != null) return;

            lookup = new Dictionary<int, CardData>();
            allCards = new List<CardData>();

            var loaded = Addressables.LoadAssetsAsync<CardData>("Cards", null).WaitForCompletion();
            foreach (var card in loaded)
            {
                if (card == null) continue;
                if (lookup.ContainsKey(card.cardCode))
                {
                    Debug.LogWarning($"[CardRegistry] 중복 cardCode: {card.cardCode} ({card.cardName})");
                    continue;
                }
                lookup[card.cardCode] = card;
                allCards.Add(card);
            }

            Debug.Log($"[CardRegistry] 카드 {allCards.Count}장 로드 완료");
        }

        public static CardData GetCard(int code)
        {
            EnsureLoaded();
            if (lookup.TryGetValue(code, out var card)) return card;
            Debug.LogWarning($"[CardRegistry] cardCode {code}를 찾을 수 없습니다.");
            return null;
        }

        public static List<CardData> GetCards(List<int> codes)
        {
            EnsureLoaded();
            var result = new List<CardData>();
            foreach (int code in codes)
            {
                var card = GetCard(code);
                if (card != null) result.Add(card);
            }
            return result;
        }

        /// <summary>
        /// 직업별 보상 카드 풀. 중립(1xxx) + 해당 직업 카드 중 강화 전 카드만 반환.
        /// </summary>
        public static List<CardData> GetRewardPool(CharacterType character, List<int> overrideCodes = null)
        {
            EnsureLoaded();

            if (overrideCodes != null && overrideCodes.Count > 0)
                return GetCards(overrideCodes);

            var result = new List<CardData>();
            foreach (var card in allCards)
            {
                if (card == null) continue;
                if (!card.IsUpgraded && !card.IsStarter && card.BelongsToClass(character))
                    result.Add(card);
            }
            return result;
        }

        /// <summary>에디터/테스트용: 캐시 초기화</summary>
        public static void Reset() => lookup = null;
    }
}
