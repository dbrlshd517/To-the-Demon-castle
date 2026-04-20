using UnityEngine;
using System.Collections.Generic;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 전투 중 생성된 적 유닛의 런타임 상태.
    /// EnemyData는 원본 정적 데이터, EnemyInstance는 전투 중 변화하는 값을 담습니다.
    /// </summary>
    public class EnemyInstance
    {
        public string         Name;
        public int            MaxHP;
        public int            CurrentHP;
        public int            Damage;
        public int            Block;
        public Vector2Int     GridPos;
        public GameObject     GameObject;
        public UnitUI         UI;
        public EnemyData      Data;
        public EnemyBehavior  Behavior;   // 이 유닛 전용 행동 인스턴스

        /// <summary>화염·기절·빙결 등 상태이상 목록</summary>
        public List<StatusEntry> StatusEffects = new List<StatusEntry>();

        /// <summary>이번 턴 예정 행동 목록. PlanTurn 후 갱신되며 conditionContainer 행동 아이콘에 반영됩니다.</summary>
        public List<UnitActionEntry> PlannedActions = new List<UnitActionEntry>();

        /// <summary>기절 또는 빙결 상태면 true</summary>
        public bool IsIncapacitated =>
            StatusEffects.Exists(s =>
                (s.Type == StatusEffectType.Stun || s.Type == StatusEffectType.Freeze)
                && s.Stacks > 0);
    }
}
