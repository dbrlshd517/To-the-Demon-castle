using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckRoguelike.Item
{
    /// <summary>
    /// 아이템 데이터와 생성 팩토리를 코드로 등록하고 조회하는 정적 레지스트리.
    ///
    /// 사용법:
    ///   1. ItemEffectRegistry.Register(data, () => new MyItem());
    ///   2. ItemEffectRegistry.Create(itemCode) 로 인스턴스 생성 (일회용)
    /// </summary>
    public static class ItemEffectRegistry
    {
        private static readonly Dictionary<int, (ItemData data, Func<ItemEffect> factory)> entries =
            new Dictionary<int, (ItemData, Func<ItemEffect>)>();

        /// <summary>아이템 데이터와 팩토리를 등록합니다. 같은 코드면 덮어씁니다.</summary>
        public static void Register(ItemData data, Func<ItemEffect> factory)
        {
            if (data == null || data.itemCode == 0)
            {
                Debug.LogWarning("[ItemEffectRegistry] 유효하지 않은 ItemData 등록 시도 무시됨");
                return;
            }
            entries[data.itemCode] = (data, factory);
        }

        /// <summary>
        /// 등록된 팩토리로 새 아이템 효과 인스턴스를 생성합니다.
        /// Data 필드를 자동으로 할당합니다.
        /// 코드가 없으면 경고 후 null 반환.
        /// </summary>
        public static ItemEffect Create(int itemCode)
        {
            if (entries.TryGetValue(itemCode, out var entry))
            {
                var effect = entry.factory();
                effect.Data = entry.data;
                return effect;
            }

            Debug.LogWarning($"[ItemEffectRegistry] 등록되지 않은 아이템 코드: {itemCode}");
            return null;
        }

        /// <summary>등록된 모든 ItemData 목록을 반환합니다.</summary>
        public static List<ItemData> GetAll()
        {
            var result = new List<ItemData>();
            foreach (var entry in entries.Values)
                result.Add(entry.data);
            return result;
        }

        /// <summary>모든 등록을 초기화합니다.</summary>
        public static void Clear() => entries.Clear();
    }
}
