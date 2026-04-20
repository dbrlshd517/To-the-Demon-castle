using System;
using System.Collections.Generic;
using UnityEngine;
using DeckRoguelike.UI;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// CustomEffectRegistry에 등록된 함수에 전달되는 컨텍스트.
    /// </summary>
    public class CardEffectContext
    {
        /// <summary>현재 전투를 관리하는 컨트롤러</summary>
        public CombatController Combat;

        /// <summary>카드 효과에 지정된 수치 (value 필드)</summary>
        public int Value;

        /// <summary>플레이어가 선택한 셀 좌표 (타겟이 없는 효과면 null)</summary>
        public Vector2Int? SelectedPos;

        /// <summary>X 코스트 카드가 소모한 에너지량. 일반 카드는 0.</summary>
        public int XValue;
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
            new Dictionary<string, Action<CardEffectContext>>();

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
