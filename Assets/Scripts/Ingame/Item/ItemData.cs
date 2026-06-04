using System;
using UnityEngine;
using DeckRoguelike.Core;

namespace DeckRoguelike.Item
{
    /// <summary>
    /// 아이템의 정적 데이터.
    ///
    /// [itemCode 규칙 — 3자리 XYZ]
    ///   X (백의 자리) = 희귀도 (아이템은 일반/희귀 2등급 — 영웅 등급 없음)
    ///     1xx = 일반(Common) 아이템
    ///     2xx = 희귀(Uncommon) 아이템
    ///     3xx = 희귀(Uncommon) 아이템 (상위 등급 폐지 — Uncommon=희귀에 통합)
    ///     7xx = 미사용(폐기) 아이템 — 도감·인게임 풀 어디에도 생성되지 않음
    ///     9xx = 보스 아이템
    ///
    ///   Y (십의 자리) = 캐릭터 전용 여부
    ///     _6_ = Mage 획득 불가
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

        /// <summary>7xx — 미사용(폐기) 아이템. 도감 표시와 인게임 보상/상점 풀 모두에서 제외됩니다.</summary>
        public bool IsUnused => itemCode / 100 == 7;

        /// <summary>코드 첫째 자리를 희귀도 인덱스로 반환합니다 (아이템은 일반/희귀 2등급).
        /// 1xx → 0(일반/Common), 2xx/3xx → 1(희귀/Uncommon).</summary>
        public int RarityIndex
        {
            get
            {
                int hundreds = itemCode / 100;
                if (hundreds <= 1) return 0; // Common
                return 1;                    // Uncommon (2xx/3xx 통합)
            }
        }

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
                    _ => null
                };
            }
        }

        /// <summary>둘째 자리가 6이면 Mage는 획득 불가.</summary>
        public CharacterType? ExcludedCharacter
        {
            get
            {
                int tens = (itemCode / 10) % 10;
                return tens == 6 ? CharacterType.Mage : null;
            }
        }

        /// <summary>지정한 캐릭터가 이 아이템을 사용할 수 있으면 true를 반환합니다.</summary>
        public bool IsForCharacter(CharacterType character)
        {
            if (ExcludedCharacter == character) return false;
            var req = RequiredCharacter;
            return req == null || req == character;
        }
    }
}
