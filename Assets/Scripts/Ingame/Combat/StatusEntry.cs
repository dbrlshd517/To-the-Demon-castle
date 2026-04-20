using System.Collections.Generic;

namespace DeckRoguelike.Combat
{
    public enum StatusEffectType
    {
        Fire,    // 매 턴 Stacks만큼 데미지. 중복 가능 (스택 누적)
        Stun,    // Stacks턴 동안 행동 불가
        Freeze,  // Stun과 동일 (빙결)
    }

    /// <summary>
    /// 하나의 상태이상 항목. 유닛당 List&lt;StatusEntry&gt;로 보유합니다.
    /// </summary>
    public class StatusEntry
    {
        public StatusEffectType Type;
        /// <summary>Fire: 턴당 데미지 / Stun·Freeze: 남은 턴 수</summary>
        public int Stacks;
    }
}
