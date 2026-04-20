using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DeckRoguelike.Core;

namespace DeckRoguelike.Item
{
    /// <summary>
    /// Resources/Data/ItemDatabase.asset 에서 아이템 데이터를 로드하여 조회하는 정적 레지스트리.
    /// </summary>
    public static class ItemRegistry
    {
        private static List<ItemData> items;

        private static void EnsureLoaded()
        {
            if (items != null) return;

            var db = Addressables.LoadAssetAsync<ItemDatabase>("Data/ItemDatabase").WaitForCompletion();
            if (db != null)
                items = new List<ItemData>(db.items);
            else
            {
                items = new List<ItemData>();
                Debug.LogWarning("[ItemRegistry] ItemDatabase를 찾을 수 없습니다. Addressables 주소를 확인하세요.");
            }
        }

        /// <summary>보스 아이템(9xx) 또는 일반 아이템 목록을 반환합니다.</summary>
        public static List<ItemData> GetByType(bool bossItem)
        {
            EnsureLoaded();
            var result = new List<ItemData>();
            foreach (var item in items)
                if (item != null && item.IsBossItem == bossItem)
                    result.Add(item);
            return result;
        }

        /// <summary>
        /// 지정한 캐릭터가 획득할 수 있는 일반/보스 아이템 목록을 반환합니다.
        /// 캐릭터 전용 아이템(십의 자리 7/8/9)은 해당 캐릭터만, 공용 아이템은 모두 포함됩니다.
        /// </summary>
        public static List<ItemData> GetForCharacter(CharacterType character, bool bossItem = false)
        {
            EnsureLoaded();
            var result = new List<ItemData>();
            foreach (var item in items)
                if (item != null && item.IsBossItem == bossItem && item.IsForCharacter(character))
                    result.Add(item);
            return result;
        }

        /// <summary>등록된 모든 ItemData 목록을 반환합니다.</summary>
        public static List<ItemData> GetAll()
        {
            EnsureLoaded();
            return new List<ItemData>(items);
        }

        /// <summary>캐시를 초기화합니다 (에디터/테스트용).</summary>
        public static void Reset() => items = null;
    }
}
