using System.Collections.Generic;
using UnityEngine;
using DeckRoguelike.Item;

namespace DeckRoguelike.Item
{
    /// <summary>
    /// ItemImporter가 CSV로부터 생성하는 런타임 아이템 데이터베이스.
    /// Assets/Resources/Data/ItemDatabase.asset 에 저장됩니다.
    /// </summary>
    [CreateAssetMenu(menuName = "DeckRoguelike/Item Database", fileName = "ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        public List<ItemData> items = new List<ItemData>();
    }
}
