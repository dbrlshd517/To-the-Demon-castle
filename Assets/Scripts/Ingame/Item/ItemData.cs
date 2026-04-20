using System;
using UnityEngine;
using DeckRoguelike.Core;

namespace DeckRoguelike.Item
{
    /// <summary>
    /// 아이템의 정적 데이터.
    ///
    /// [itemCode 규칙 — 3자리 XYZ]
    ///   X (백의 자리) = 희귀도
    ///     1xx = 일반(Common) 아이템
    ///     2xx = 고급(Uncommon) 아이템
    ///     3xx = 희귀(Rare) 아이템
    ///     9xx = 보스 아이템
    ///
    ///   Y (십의 자리) = 캐릭터 전용 여부
    ///     _7_ = Warrior 전용
    ///     _8_ = Gunner 전용
    ///     _9_ = Mage 전용
    ///     그 외 = 공용 (캐릭터 제한 없음)
    ///
    /// [필드 순서] itemCode, itemName, description, icon
    /// </summary>
    [Serializable]
    public class ItemData
    {
        public int    itemCode;
        public string itemName;
        [TextArea]
        public string description;
        public Sprite icon;

        public bool IsBossItem => itemCode / 100 == 9;

        /// <summary>코드 첫째 자리(1/2/3)를 희귀도 인덱스(0/1/2)로 반환합니다.</summary>
        public int RarityIndex => Mathf.Clamp(itemCode / 100 - 1, 0, 2);

        /// <summary>
        /// 십의 자리 숫자로 결정되는 캐릭터 전용 제한.
        /// 7=Warrior, 8=Gunner, 9=Mage, 그 외=null(공용).
        /// </summary>
        public CharacterType? RequiredCharacter
        {
            get
            {
                int tens = (itemCode / 10) % 10;
                return tens switch
                {
                    7 => CharacterType.Warrior,
                    8 => CharacterType.Gunner,
                    9 => CharacterType.Mage,
                    _ => (CharacterType?)null
                };
            }
        }

        /// <summary>지정한 캐릭터가 이 아이템을 사용할 수 있으면 true를 반환합니다.</summary>
        public bool IsForCharacter(CharacterType character)
        {
            var req = RequiredCharacter;
            return req == null || req == character;
        }
    }
}
