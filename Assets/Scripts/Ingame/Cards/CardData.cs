using UnityEngine;
using System.Collections.Generic;
using DeckRoguelike.Combat;

namespace DeckRoguelike.Cards
{
    [CreateAssetMenu(fileName = "NewCard", menuName = "Deck Roguelike/Card Data")]
    public class CardData : ScriptableObject
    {
        [Header("Basic Info")]
        public int cardCode;
        public string cardName;
        [TextArea(2, 4)]
        public string description;
        public Sprite cardArt;

        [Header("Cost")]
        public int energyCost;

        [Header("Effects")]
        [Tooltip("효과 목록. 각 효과는 자체 targeting/rangeOffsets를 가집니다.\n" +
                 "첫 번째 효과의 targeting이 셀 선택 UI 기준이 됩니다.")]
        public List<CardEffect> effects;

        [Header("Keywords")]
        public CardKeyword keywords;

        [Header("Audio/Visual")]
        public AudioClip playSFX;
        public GameObject playVFX;
        public Color cardGlow;

        #region Properties

        public string CardName    => cardName;
        public int    CardCode    => cardCode;
        public string Description => description;
        public Sprite CardArt     => cardArt;

        public int  EnergyCost   => energyCost;
        public bool IsXCost      => energyCost < 0;

        public bool IsUnplayable => keywords.HasFlag(CardKeyword.Unplayable);
        public bool Exhaust      => keywords.HasFlag(CardKeyword.Exhausts);
        public bool Ethereal     => keywords.HasFlag(CardKeyword.Ethereal);
        public bool Innate       => keywords.HasFlag(CardKeyword.Innate);
        public bool Retain       => keywords.HasFlag(CardKeyword.Retain);

        // ── cardCode 파싱 프로퍼티 ────────────────────────────
        // [구조] C T R N O
        //   C(1): 클래스  1=중립 2=전사 3=거너 4=메이지
        //   T(2): 타입    1=공격 2=이동 3=스킬 4=파워
        //   R(3): 희귀도  1=일반 2=고급 3=희귀 4=전설
        //   N(4): 번호
        //   O(5): 강화    짝수=강화전 홀수=강화후

        public int ClassDigit  => cardCode / 10000;
        public int TypeDigit   => (cardCode / 1000) % 10;
        public int RarityDigit => (cardCode / 100) % 10;
        public bool IsStarter  => RarityDigit == 0;

        public CardRarity Rarity => RarityDigit switch
        {
            1 => CardRarity.Common,
            2 => CardRarity.Uncommon,
            3 => CardRarity.Rare,
            4 => CardRarity.Legendary,
            _ => CardRarity.Common,
        };

        public CardType CardTypeFromCode => TypeDigit switch
        {
            1 => CardType.Attack,
            2 => CardType.Move,
            3 => CardType.Skill,
            4 => CardType.Power,
            _ => CardType.Status,
        };

        public bool IsUpgraded   => cardCode % 2 == 1;
        public int  UpgradedCode => IsUpgraded ? cardCode : cardCode + 1;

        public bool BelongsToClass(DeckRoguelike.Core.CharacterType character)
        {
            int classDigit = character switch
            {
                DeckRoguelike.Core.CharacterType.Warrior => 2,
                DeckRoguelike.Core.CharacterType.Gunner  => 3,
                DeckRoguelike.Core.CharacterType.Mage    => 4,
                _                                        => 1,
            };
            return ClassDigit == 1 || ClassDigit == classDigit;
        }

        public List<CardEffect> Effects => effects;

        /// <summary>UI 셀 선택 기준: 첫 번째 효과의 targeting</summary>
        public TargetType PrimaryTargeting =>
            (effects != null && effects.Count > 0) ? effects[0].targeting : TargetType.Self;

        #endregion

        /// <summary>
        /// 설명 텍스트 내 플레이스홀더를 실제 수치로 치환
        /// {D} = 첫 번째 Damage 효과값 + strength
        /// {B} = 첫 번째 Block 효과값 + dexterity
        /// {Draw} / {E} / {Heal} = 해당 타입 첫 번째 효과값
        /// {X} = 현재 에너지
        /// </summary>
        public string GetFormattedDescription(int strength = 0, int dexterity = 0, int currentEnergy = 0)
            => GetFormattedDescription(description, strength, dexterity, currentEnergy);

        public string GetFormattedDescription(string baseDesc, int strength = 0, int dexterity = 0, int currentEnergy = 0)
        {
            string desc = baseDesc.Replace("\\n", "\n");

            if (Effects == null) return desc;

            if (desc.Contains("{X}"))
                desc = desc.Replace("{X}", currentEnergy.ToString());

            foreach (var effect in Effects)
            {
                int val = effect.value;
                string key = effect.effectType switch
                {
                    EffectType.Damage => "{D}",
                    EffectType.Block  => "{B}",
                    EffectType.Draw   => "{Draw}",
                    EffectType.Energy => "{E}",
                    EffectType.Heal   => "{Heal}",
                    _                 => null
                };

                // Custom 데미지 효과 (ID에 "damage" 포함): {D} 치환 + 힘 반영
                bool isCustomDamage = key == null &&
                    effect.effectType == EffectType.Custom &&
                    effect.customEffectId != null &&
                    effect.customEffectId.IndexOf("damage", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (isCustomDamage) key = "{D}";

                if (key == null || !desc.Contains(key)) continue;

                if (effect.effectType == EffectType.Damage || isCustomDamage) val += strength;
                else if (effect.effectType == EffectType.Block) val += dexterity;

                desc = desc.Replace(key, val.ToString());
            }

            return desc;
        }

        public CardData Clone()
        {
            CardData clone = Instantiate(this);
            clone.name = this.name;
            return clone;
        }
    }

    #region CardEffect

    [System.Serializable]
    public class CardEffect
    {
        public EffectType effectType;
        public int value;

        [Header("Targeting")]
        [Tooltip("이 효과의 타겟 방식\n" +
                 "첫 번째 효과의 targeting이 셀 선택 UI 기준이 됩니다.")]
        public TargetType targeting;
        [Tooltip("효과 범위 셀 목록\n" +
                 "useAbsoluteCoords=false: 플레이어 위치 기준 상대 좌표\n" +
                 "useAbsoluteCoords=true:  보드 절대 좌표\n" +
                 "col: 오른쪽 +, 왼쪽 -  /  row: 위 +, 아래 -")]
        public RangeOffset[] rangeOffsets;
        [Tooltip("true면 rangeOffsets를 보드 절대 좌표로 해석")]
        public bool useAbsoluteCoords = false;
        [Header("Custom / Summon")]
        [Tooltip("effectType=Custom 전용: CustomEffectRegistry에 등록된 함수 ID\n예) Strength, gain_dexterity")]
        public string customEffectId;
    }

    #endregion

    #region RangeOffset

    [System.Serializable]
    public class RangeOffset
    {
        [Tooltip("열 (오른쪽 +, 왼쪽 -)")]
        public int col;
        [Tooltip("행 (위 +, 아래 -)")]
        public int row;

        public Vector2Int ToVector2Int() => new Vector2Int(col, row);
    }

    #endregion

    #region Enums

    [System.Flags]
    public enum CardKeyword
    {
        None       = 0,
        Unplayable = 1 << 0,  // 손에 있어도 사용 불가 (저주/상태이상 카드)
        Exhausts   = 1 << 1,  // 사용 후 소멸
        Ethereal   = 1 << 2,  // 버리면 소멸
        Innate     = 1 << 3,  // 항상 첫 손패에 포함
        Retain     = 1 << 4,  // 턴 넘겨도 유지
    }

    public enum CardType
    {
        Attack,  // 공격 카드 (빨간색)   코드 2자리: 0/1
        Move,    // 이동 카드 (초록색)   코드 2자리: 2/3
        Skill,   // 스킬 카드 (파란색)   코드 2자리: 4/5
        Power,   // 파워 카드 (노란색)   코드 2자리: 6/7
        Status,  // 예비/상태이상 (회색) 코드 2자리: 8/9
        Curse,   // 저주 카드 (검은색)   직접 지정
    }

    public enum CardRarity
    {
        Common,    // 일반  코드 3자리: 0~2
        Uncommon,  // 고급  코드 3자리: 3~5
        Rare,      // 희귀  코드 3자리: 6~8
        Legendary  // 전설  코드 3자리: 9
    }

    public enum TargetType
    {
        Self   = 0, // 자기 자신 (즉시 실행, 셀 선택 불필요) — Power 카드 고정
        Random = 4, // 무작위 적 (즉시 실행)
        Enemy  = 5, // 범위 내 적 셀 하나 선택
        Any    = 6, // 범위 내 아무 셀이나 선택
        All    = 7, // 범위 내 모든 적 자동 공격
        Ally   = 8, // 범위 내 자신/아군 선택
    }

    public enum EffectType
    {
        Damage,      // 적에게 피해
        Block,       // 방어도 획득
        Draw,        // 카드 드로우
        Energy,      // 에너지 획득
        Heal,        // 체력 회복
        Move,        // 플레이어 이동
        SummonAlly,  // 지정 위치에 아군 유닛 소환
        Custom,      // CustomEffectRegistry에 등록된 커스텀 함수 호출
    }

    #endregion
}
