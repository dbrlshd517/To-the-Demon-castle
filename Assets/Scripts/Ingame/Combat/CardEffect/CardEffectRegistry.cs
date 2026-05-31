using System;
using System.Collections.Generic;
using UnityEngine;
using DeckRoguelike.Cards;
using DeckRoguelike.UI;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// CustomEffectRegistry에 등록된 함수에 전달되는 컨텍스트.
    /// </summary>
    public class CardEffectContext
    {
        /// <summary>현재 전투를 관리하는 컨트롤러</summary>
        public BoardController Board;

        /// <summary>카드 효과에 지정된 수치 (value 필드)</summary>
        public int Value;

        /// <summary>CSV value 토큰 원본 (예: "6.20"). 다중 값 핸들러가 '.'로 split.</summary>
        public string ValueRaw;

        /// <summary>플레이어가 선택한 셀 좌표 (타겟이 없는 효과면 null)</summary>
        public Vector2Int? SelectedPos;

        /// <summary>X 코스트 카드가 소모한 에너지량. 일반 카드는 0.</summary>
        public int XValue;

        /// <summary>현재 처리 중인 카드 효과 (rangeOffsets/targeting 접근용)</summary>
        public CardEffect Effect;

        /// <summary>현재 사용 중인 카드 (handler가 카드 컨텍스트 필요할 때)</summary>
        public CardData Card;
    }

    /// <summary>
    /// 커스텀 효과 함수를 ID로 등록하고 조회하는 정적 레지스트리.
    ///
    /// 사용법:
    ///   1. CustomEffectRegistry.Register("my_effect", ctx => { ... });
    ///   2. CardEffect.customEffectId = "my_effect" 로 카드에 연결
    /// </summary>
    public static class CardEffectRegistry
    {
        private static readonly Dictionary<string, Action<CardEffectContext>> effects =
            new Dictionary<string, Action<CardEffectContext>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>커스텀 효과를 ID와 함께 등록합니다. 같은 ID면 덮어씁니다.</summary>
        public static void Register(string id, Action<CardEffectContext> handler)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[CustomEffectRegistry] 빈 ID로 등록 시도 무시됨");
                return;
            }
            effects[id] = handler;
        }

        /// <summary>등록된 효과를 실행합니다. ID가 없으면 경고 후 false 반환.</summary>
        public static bool Execute(string id, CardEffectContext ctx)
        {
            if (effects.TryGetValue(id, out var handler))
            {
                handler(ctx);
                return true;
            }
            Debug.LogWarning($"[CustomEffectRegistry] 등록되지 않은 효과 ID: '{id}'");
            return false;
        }

        /// <summary>모든 등록된 효과를 초기화합니다 (씬 전환 등에서 필요시 사용).</summary>
        public static void Clear() => effects.Clear();
    }
}
