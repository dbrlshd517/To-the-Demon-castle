using System;
using UnityEngine;
using DeckRoguelike.Core;

namespace DeckRoguelike.Relic
{
    /// <summary>
    /// 유물의 정적 데이터.
    ///
    /// [relicCode 규칙]
    ///   1xx = 희귀(Uncommon) 유물
    ///   2xx = 영웅(Rare) 유물
    ///   3xx = 영웅(Rare) 유물
    ///   4xx = 상점 전용 유물 (상점에서만 구매 가능, 희귀=Uncommon 등급)
    ///   5xx/6xx = 상점 전용 영웅(Rare) 유물
    ///   7xx = 미사용(폐기) 유물 — 도감·인게임 풀 어디에도 생성되지 않음
    ///   9xx = 보스 유물
    ///   ※ 유물은 일반(Common) 등급 없음 — 희귀(Uncommon)/영웅(Rare) 2등급
    ///
    /// [둘째 자리 — 직업 제약]
    ///   둘째 자리(tens digit)가 6이면 메이지 획득 불가
    ///   7/8/9 이면 직업 한정 유물
    ///   7=전사 / 8=거너 / 9=메이지
    ///   예) 70/80/90 = 직업별 시작 유물 (상점·위험 보상에서 제외)
    ///       970/980/990 = 직업별 보스 유물
    ///
    /// [필드 순서] relicCode, relicName, description, icon
    /// </summary>
    [Serializable]
    public class RelicData
    {
        public int    relicCode;
        public string relicName;
        [TextArea]
        public string description;
        public Sprite icon;

        public bool IsBossRelic     => relicCode / 100 == 9;
        public bool IsShopOnlyRelic => relicCode / 100 >= 4 && relicCode / 100 <= 6;

        /// <summary>7xx — 미사용(폐기) 유물. 도감 표시와 인게임 보상/상점 풀 모두에서 제외됩니다.
        /// (시작 유물 70/80/90은 두 자리 코드라 relicCode/100==0 이므로 여기 해당되지 않음.)</summary>
        public bool IsUnused        => relicCode / 100 == 7;

        /// <summary>70/80/90 — 직업 시작 유물. 상점·위험 보상에서 제외됩니다.</summary>
        public bool IsStartingRelic => relicCode == 70 || relicCode == 80 || relicCode == 90;

        /// <summary>둘째 자리에 따라 직업 제약을 반환합니다 (없으면 null).</summary>
        public CharacterType? RestrictedCharacter
        {
            get
            {
                int tens = (relicCode / 10) % 10;
                return tens switch
                {
                    7 => CharacterType.Warrior,
                    8 => CharacterType.Gunner,
                    9 => CharacterType.Mage,
                    _ => (CharacterType?)null,
                };
            }
        }

        /// <summary>둘째 자리가 6이면 Mage는 획득 불가.</summary>
        public CharacterType? ExcludedCharacter
        {
            get
            {
                int tens = (relicCode / 10) % 10;
                return tens == 6 ? CharacterType.Mage : null;
            }
        }

        public bool IsForCharacter(CharacterType character)
        {
            if (ExcludedCharacter == character) return false;
            var req = RestrictedCharacter;
            return req == null || req == character;
        }

        /// <summary>코드 첫째 자리를 희귀도 인덱스(1~2)로 반환합니다.
        /// 유물 등급은 희귀(Uncommon) / 영웅(Rare) 2단계만 존재합니다 (일반 폐지).
        /// 1xx=희귀/Uncommon(1), 2xx=영웅/Rare(2), 3xx=영웅/Rare(2),
        /// 4xx=희귀/Uncommon(1), 5xx=영웅/Rare(2), 6xx=영웅/Rare(2).</summary>
        public int RarityIndex
        {
            get
            {
                int hundreds = relicCode / 100;
                if (hundreds <= 1) return 1;             // 1xx → Uncommon
                if (hundreds == 4) return 1;             // 4xx → Shop Uncommon
                return 2;                                // 2xx/3xx/5xx/6xx → Rare
            }
        }
    }
}
