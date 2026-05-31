using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using DeckRoguelike.Cards;

namespace DeckRoguelike.Core
{
    /// <summary>
    /// GameResources/Data/Cards 폴더 ("Cards" 라벨) — 일반 카드
    /// GameResources/Data/Events 폴더 ("Events" 라벨) — 이벤트 카드 (entry + 선택지)
    ///
    /// 두 풀은 cardCode가 겹쳐도 분리 보관됩니다.
    /// 일반 카드 풀에는 이벤트 카드가 섞이지 않습니다.
    /// </summary>
    public static class CardRegistry
    {
        private static Dictionary<int, CardData> lookup;
        private static List<CardData> allCards;

        private static Dictionary<int, CardData> eventLookup;
        private static List<CardData> allEventCards;

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

        private static void EnsureEventsLoaded()
        {
            if (eventLookup != null) return;

            eventLookup = new Dictionary<int, CardData>();
            allEventCards = new List<CardData>();

            try
            {
                var loaded = Addressables.LoadAssetsAsync<CardData>("Events", null).WaitForCompletion();
                if (loaded != null)
                {
                    foreach (var card in loaded)
                    {
                        if (card == null) continue;
                        if (eventLookup.ContainsKey(card.cardCode))
                        {
                            Debug.LogWarning($"[CardRegistry] 중복 이벤트 cardCode: {card.cardCode} ({card.cardName})");
                            continue;
                        }
                        eventLookup[card.cardCode] = card;
                        allEventCards.Add(card);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CardRegistry] Events 라벨 로드 실패 (라벨이 없을 수 있음): {e.Message}");
            }

            Debug.Log($"[CardRegistry] 이벤트 카드 {allEventCards.Count}장 로드 완료");
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
        /// 직업별 보상 카드 풀. 공통(1xxx) + 해당 직업 카드 중 강화 전 카드만 반환.
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
                if (card.IsUpgraded || card.IsStarter) continue;
                if (card.ClassDigit == 1 || card.BelongsToClass(character))
                    result.Add(card);
            }
            return result;
        }

        // ───────────────────────────────────────────────
        // Event 카드 풀 (Addressables "Events" 라벨)
        // ───────────────────────────────────────────────

        /// <summary>이벤트 카드 (entry 또는 선택지)를 cardCode로 조회.</summary>
        public static CardData GetEventCard(int code)
        {
            EnsureEventsLoaded();
            if (eventLookup.TryGetValue(code, out var card)) return card;
            Debug.LogWarning($"[CardRegistry] 이벤트 cardCode {code}를 찾을 수 없습니다.");
            return null;
        }

        /// <summary>
        /// (LEGACY) 액트에 해당하는 entry 이벤트 카드 풀.
        /// 규칙: cardCode 첫째자리 = act, 마지막자리 = 0.
        /// 신규 코드는 <see cref="GetEventEntriesForActAndType"/>를 사용하세요.
        /// </summary>
        public static List<CardData> GetEventEntriesForAct(int act)
        {
            EnsureEventsLoaded();
            var result = new List<CardData>();
            foreach (var card in allEventCards)
            {
                if (card == null) continue;
                int first = card.cardCode;
                while (first >= 10) first /= 10;
                int last = card.cardCode % 10;
                if (first == act && last == 0)
                    result.Add(card);
            }
            return result;
        }

        /// <summary>
        /// 새 cardCode 규칙 기반 entry 이벤트 카드 풀.
        ///   첫째자리: act(1~3) 또는 4 = 공통(모든 act 풀에 포함)
        ///   둘째자리: 1 = ChoiceEvent / 2 = ShopEvent
        ///   마지막자리: 0 = entry (1~9 = choice)
        /// 3·4번째 자리는 슬롯 구분용으로 의미 없음 (entry 단위 카운터).
        /// </summary>
        public static List<CardData> GetEventEntriesForActAndType(int act, DeckRoguelike.UI.EventSubType subType)
        {
            EnsureEventsLoaded();
            int typeDigit = subType switch
            {
                DeckRoguelike.UI.EventSubType.ChoiceEvent => 1,
                DeckRoguelike.UI.EventSubType.ShopEvent   => 2,
                _ => 0,
            };
            if (typeDigit == 0) return new List<CardData>();

            var result = new List<CardData>();
            foreach (var card in allEventCards)
            {
                if (card == null) continue;
                int code = card.cardCode;
                if (code < 10000) continue;       // 최소 5자리 (전형적 코드 길이 — 안전망)
                if (code % 10 != 0) continue;     // entry only

                int firstDigit = code;
                while (firstDigit >= 10) firstDigit /= 10;
                if (!(firstDigit == act || firstDigit == 4)) continue; // act 매칭 or 공통

                // 둘째 자리 — 5자리 코드 기준 / 10000
                int secondDigit = (code / 1000) % 10;
                if (secondDigit != typeDigit) continue;

                result.Add(card);
            }
            return result;
        }

        /// <summary>이벤트 카드 전체 (디버그/툴용)</summary>
        public static List<CardData> GetAllEventCards()
        {
            EnsureEventsLoaded();
            return new List<CardData>(allEventCards);
        }

        /// <summary>에디터/테스트용: 캐시 초기화</summary>
        public static void Reset()
        {
            lookup = null;
            eventLookup = null;
        }
    }
}
