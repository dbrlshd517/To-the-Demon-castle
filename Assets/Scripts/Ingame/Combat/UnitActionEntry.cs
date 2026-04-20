using UnityEngine;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 행동 아이콘 타입.
    /// 배열 인덱스와 일치합니다 — UnitUI.actionIconPrefabs[0]=Attack, [1]=Move, [2+]=Skill 등.
    /// </summary>
    public enum UnitActionType
    {
        Attack = 0,
        Move   = 1,
        Skill  = 2,
    }

    /// <summary>
    /// Enemy / Ally 가 한 턴에 수행할 행동 하나를 나타냅니다.
    /// EnemyInstance.PlannedActions 배열에 저장됩니다.
    /// </summary>
    public class UnitActionEntry
    {
        /// <summary>행동 종류 (배열 인덱스와 대응)</summary>
        public UnitActionType Type;
        /// <summary>Attack = 데미지, Move = 이동 칸 수, Skill = 0 또는 수치</summary>
        public int Value;
        /// <summary>null이면 Value.ToString() 표시</summary>
        public string Label;
        /// <summary>conditionContainer에 표시할 아이콘 스프라이트</summary>
        public Sprite Icon;
    }
}
