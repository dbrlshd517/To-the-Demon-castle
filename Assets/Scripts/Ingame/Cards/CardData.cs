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

        [Tooltip("카드 효과 전용 이미지 주소 (소멸배기 칼 등). 비어 있으면 Inspector 기본값 사용.\n" +
                 "값에 '/'이 없으면 'Sprites/Combat/Player/{값}' 으로 해석.\n" +
                 "예) Exhaustsknife → Sprites/Combat/Player/Exhaustsknife")]
        public string imagePath;

        [Header("Effects")]
        [Tooltip("효과 목록. 각 효과는 자체 targeting/rangeOffsets를 가집니다.\n" +
                 "첫 번째 효과의 targeting이 셀 선택 UI 기준이 됩니다.")]
        public List<CardEffect> effects;

        [Header("Keywords")]
        public CardKeyword keywords;

        [Header("Icon")]
        [Tooltip("카드 아이콘 코드. 보상카드의 경우 유물/아이템 코드, 일반카드는 0(cardCode 사용)")]
        [HideInInspector] public int iconCode;

        [Header("Audio/Visual")]
        public AudioClip playSFX;
        public GameObject playVFX;
        public Color cardGlow;

        #region Properties

        public string CardName    => cardName;
        public int    CardCode    => cardCode;
        public string Description => description;
        public Sprite CardArt     => cardArt;

        public bool IsUnplayable => keywords.HasFlag(CardKeyword.Unplayable);
        public bool Exhaust      => keywords.HasFlag(CardKeyword.Exhausts);
        public bool Ethereal     => keywords.HasFlag(CardKeyword.Ethereal);
        public bool Innate       => keywords.HasFlag(CardKeyword.Innate);
        public bool Retain       => keywords.HasFlag(CardKeyword.Retain);
        public bool NonRemovable => keywords.HasFlag(CardKeyword.NonRemovable);

        // ── cardCode 파싱 프로퍼티 ────────────────────────────
        // [구조] C T R N O
        //   C(1): 클래스  1=공통 2=전사 3=거너 4=메이지
        //   T(2): 타입    1=이동 2=액션 3=파워
        //   R(3): 희귀도  1=일반 2=희귀 3=영웅 4=전설
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

        // 카드 분류 (ClassDigit):
        //   1~4 : 전투 카드 — TypeDigit으로 Move/Action/Power 결정.
        //   6,7 : 비전투 사용 카드 — CardType.None.
        //         6xxxx = 보상/시스템 카드(골드/유물/아이템/카드보상/넘기기/휴식/강화/카드제거/전투재시작/메인메뉴).
        //         7xxxx = 맵 이동, 강화 등.
        //   8   : 사용 불가능 카드 — CardType.None.
        //
        // 비전투 카드를 None으로 두는 이유: combat type filter(`cardTypeRestrictions[Action/Move/Power]` 봉인,
        // relic 트리거 "다음 X 카드", `OnCardPlayed`의 type 매칭으로 strength 가산, type별 카운트, "type X 카드 모두 버리기" 등)에
        // 잘못 매칭되는 것을 원천 차단. 6/7 카드는 모두 자체 dispatch 경로(`HandleShopCardPlay`/`HandleRestCardPlay`/
        // `HandleMapMoveCardPlay`/`ExecuteCard` for _rewardCards)로 실행되므로 generic type 기반 실행 흐름이 필요 없음.
        // IsSelfPlayCard는 `PrimaryTargeting == TargetType.Self`로 여전히 true가 되므로 SelfPlay UI 흐름 유지됨.
        public CardType CardTypeFromCode => (ClassDigit == 6 || ClassDigit == 7 || ClassDigit == 8)
            ? CardType.None
            : TypeDigit switch
            {
                1 => CardType.Move,
                2 => CardType.Action,
                3 => CardType.Power,
                _ => CardType.Action,
            };

        // 강화 여부는 정규 카드(ClassDigit 1~4)에만 적용. 보상/특수 템플릿(5xxxx, 6xxxx, 7xxxx, 8xxxx)은
        // cardCode 마지막 자리가 강화 플래그가 아니므로 IsUpgraded가 항상 false 여야 한다.
        public bool IsUpgraded   => ClassDigit >= 1 && ClassDigit <= 4 && cardCode % 2 == 1;
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
        /// {Draw} / {Heal} = 해당 타입 첫 번째 효과값
        /// </summary>
        public string GetFormattedDescription(int strength = 0, int dexterity = 0)
            => GetFormattedDescription(description, strength, dexterity);

        public string GetFormattedDescription(string baseDesc, int strength = 0, int dexterity = 0)
        {
            string desc = baseDesc.Replace("\\n", "\n");

            // {T} = 현재 Act 번호 (순간이동 카드 사용 가능 횟수)
            if (desc.Contains("{T}"))
                desc = desc.Replace("{T}", (DeckRoguelike.Core.GameManager.Instance?.CurrentAct ?? 1).ToString());

            if (Effects == null) return desc;

            foreach (var effect in Effects)
            {
                int val = effect.value;
                string key = effect.effectType switch
                {
                    EffectType.Damage => "{D}",
                    EffectType.Block  => "{B}",
                    EffectType.Draw   => "{Draw}",
                    EffectType.Heal   => "{Heal}",
                    _                 => null
                };

                // Shoot 공격 효과 (Shoot/Allrange_Shoot/6times_Shoot): {D} = value + ShootDamage + Strength
                bool isShootAttack = key == null
                    && DeckRoguelike.UI.BoardController.IsShootAttackEffect(effect);

                // Custom 데미지 효과: ID에 "damage"가 포함되거나, "damage"가 없지만 {D}(피해)를 쓰는 알려진 효과(돌진/치킨게임 등).
                // Shoot 공격은 별도 처리하므로 여기서 제외한다.
                bool isCustomDamage = key == null && !isShootAttack &&
                    effect.effectType == EffectType.Custom &&
                    !string.IsNullOrEmpty(effect.customEffectId) &&
                    (effect.customEffectId.IndexOf("damage", System.StringComparison.OrdinalIgnoreCase) >= 0
                     || IsKnownCustomDamageId(effect.customEffectId));

                if (isCustomDamage) key = "{D}";
                else if (isShootAttack) key = "{D}";

                // 골드 보상 카드: {G}를 미리 결정된 골드량으로 치환
                if (key == null &&
                    effect.effectType == EffectType.Custom &&
                    string.Equals(effect.customEffectId, "Reward_Gold", System.StringComparison.OrdinalIgnoreCase))
                    key = "{G}";

                if (key == null || !desc.Contains(key)) continue;

                if (effect.effectType == EffectType.Damage || isCustomDamage)
                    val = ApplyOutgoingMultiplierForDisplay(val + strength);
                else if (isShootAttack)
                    val = ApplyOutgoingMultiplierForDisplay(val + strength + DeckRoguelike.UI.BoardController.CurrentShootDamage);
                else if (effect.effectType == EffectType.Block) val += dexterity;

                desc = desc.Replace(key, val.ToString());
            }

            return desc;
        }

        /// <summary>customEffectId에 "damage" 문자열은 없지만 {D}(피해)를 사용하는 커스텀 데미지 효과 판별.</summary>
        private static bool IsKnownCustomDamageId(string id) =>
            id.Equals("pushEnmemy",       System.StringComparison.OrdinalIgnoreCase) ||  // 돌진
            id.Equals("Whenkill_losehp",  System.StringComparison.OrdinalIgnoreCase) ||  // 치킨게임 (카드 에셋 ID)
            id.Equals("Untilkill_losehp", System.StringComparison.OrdinalIgnoreCase);    // 치킨게임 (레지스트리 등록 ID)

        /// <summary>표시용 {D} 값에 이번 턴 데미지 배율(23302 최후의 공격 등)을 반영합니다.</summary>
        private static int ApplyOutgoingMultiplierForDisplay(int dmg)
        {
            float m = DeckRoguelike.UI.BoardController.CurrentOutgoingDamageMultiplier;
            return m != 1f ? Mathf.RoundToInt(dmg * m) : dmg;
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
        [Tooltip("CSV value 토큰 원본 (예: \"6.20\"). 다중 값 핸들러가 '.'로 split하여 사용.")]
        public string valueRaw;

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
        None         = 0,
        Unplayable   = 1 << 0,  // 손에 있어도 사용 불가 (저주/상태이상 카드)
        Exhausts     = 1 << 1,  // 사용 후 소멸
        Ethereal     = 1 << 2,  // 버리면 소멸
        Innate       = 1 << 3,  // 항상 첫 손패에 포함
        Retain       = 1 << 4,  // 턴 넘겨도 유지
        NonRemovable = 1 << 5,  // 덱에서 제거할 수 없음 (저주 카드 등)
        Shoot        = 1 << 6,  // (구) 발사형 카드 식별 키워드. 현재는 customEffectId 기반(BoardController.IsShootAttackEffect)으로 판별. 직렬화 호환을 위해 유지.
    }

    public enum CardType
    {
        Action,  // 액션 카드 (빨간색)   코드 T자리: 2
        Move,    // 이동 카드 (초록색)   코드 T자리: 1
        Power,   // 파워 카드 (노란색)   코드 T자리: 3
        Status,  // 상태이상 (회색)
        Curse,   // 저주 카드 (검은색)   직접 지정
        None,    // 타입 없음 — 전투 중에 사용되지 않는 시스템/맵 카드 (예: 70xxx 맵 이동). cardTypeRestrictions에 영향받지 않음.
    }

    public enum CardRarity
    {
        Common,    // 일반  코드 3자리: 0~2
        Uncommon,  // 희귀  코드 3자리: 3~5  (표시명: 희귀)
        Rare,      // 영웅  코드 3자리: 6~8  (표시명: 영웅)
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
        Damage   = 0, // 적에게 피해
        Block    = 1, // 방어도 획득
        Draw     = 2, // 카드 드로우
        Heal     = 3, // 체력 회복
        Move     = 4, // 플레이어 이동
        // 5 = (구) SummonAlly — 직렬화 호환을 위해 자리 비움. 이제 Custom + customEffectId="summon_{code}" 로 처리.
        Custom   = 6, // CustomEffectRegistry에 등록된 커스텀 함수 호출 (summon_{allyCode} 포함)
        Exhausts = 7, // 손패에서 카드를 직접 선택해 소멸 (value = 최대 선택 수)
        Discard  = 8, // 손패에서 카드를 직접 선택해 버리기 (value = 최대 선택 수)
    }

    #endregion
}
