using UnityEngine;
using System.Collections.Generic;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 전투 중 소환된 아군 유닛의 런타임 상태.
    /// AllyData는 원본 정적 데이터, AllyInstance는 전투 중 변화하는 값을 담습니다.
    /// </summary>
    public class AllyInstance
    {
        public string        Name;
        public int           MaxHP;
        public int           CurrentHP;
        public int           Damage;
        public int           Block;
        public int           TurnsRemaining;  // 0 = 전투 종료까지 유지 (durationTurns에서 초기화)
        public Vector2Int    GridPos;
        public GameObject    GameObject;
        public AllyData      Data;            // 원본 AllyData 참조

        /// <summary>그리드에서 차지하는 크기 (가로, 세로). Data.gridSize 미설정 시 (1,1).</summary>
        public Vector2Int Size =>
            (Data != null && Data.gridSize.x > 0 && Data.gridSize.y > 0)
                ? Data.gridSize
                : Vector2Int.one;

        /// <summary>
        /// 이 아군이 점유하는 모든 셀 좌표 (앵커=왼쪽 위 기준).
        /// footprint = (GridPos.x+dx, GridPos.y-dy), dx∈[0,W-1], dy∈[0,H-1].
        /// </summary>
        public IEnumerable<Vector2Int> OccupiedCells
        {
            get
            {
                Vector2Int s = Size;
                for (int dx = 0; dx < s.x; dx++)
                    for (int dy = 0; dy < s.y; dy++)
                        yield return new Vector2Int(GridPos.x + dx, GridPos.y - dy);
            }
        }
        public UnitUI        UI;              // 전투 중 HP/쉴드/이펙트 표시
        public AllyBehavior  Behavior;        // 이 유닛 전용 행동 인스턴스

        /// <summary>true=오른쪽 보는 sprite(미반전), false=왼쪽(좌우반전).</summary>
        public bool FacingRight = true;

        /// <summary>화염·기절·빙결 등 상태이상 목록</summary>
        public List<StatusEntry> StatusEffects = new List<StatusEntry>();

        /// <summary>이번 턴 예정 행동 목록. PlanTurn 후 갱신되며 conditionContainer 행동 아이콘에 반영됩니다.</summary>
        public List<UnitActionEntry> PlannedActions = new List<UnitActionEntry>();
    }
}
