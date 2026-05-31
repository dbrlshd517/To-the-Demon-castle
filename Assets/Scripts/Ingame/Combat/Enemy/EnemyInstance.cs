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

        /// <summary>
        /// 한 번의 소환 호출에서 같이 나온 적들을 묶는 식별자.
        /// SpawnFromEncounter: 같은 CSV 엔트리(예: 11901:5.random:3)에서 펼쳐진 슬롯끼리 같은 값.
        /// SummonEnemy(런타임): 호출 1회마다 새 고유값.
        /// 0이면 미지정. SnakeBehavior 같은 군집 행동이 같은 그룹끼리만 체인을 이룰 때 사용.
        /// </summary>
        public int            SpawnGroupId;

        /// <summary>그리드에서 차지하는 크기 (가로, 세로). Data.gridSize 미설정 시 (1,1).</summary>
        public Vector2Int Size =>
            (Data != null && Data.gridSize.x > 0 && Data.gridSize.y > 0)
                ? Data.gridSize
                : Vector2Int.one;

        /// <summary>
        /// 이 적이 점유하는 모든 셀 좌표 (앵커=왼쪽 위 기준).
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

        /// <summary>true=오른쪽 보는 sprite(미반전), false=왼쪽(좌우반전). 데미지 방향 보정에 사용.</summary>
        public bool FacingRight = false;

        /// <summary>화염·기절·빙결 등 상태이상 목록</summary>
        public List<StatusEntry> StatusEffects = new List<StatusEntry>();

        /// <summary>이번 턴 예정 행동 목록. PlanTurn 후 갱신되며 conditionContainer 행동 아이콘에 반영됩니다.</summary>
        public List<UnitActionEntry> PlannedActions = new List<UnitActionEntry>();

        /// <summary>기절·빙결·공포 상태면 true</summary>
        public bool IsIncapacitated =>
            StatusEffects.Exists(s =>
                (s.Type == StatusEffectType.Stun
                 || s.Type == StatusEffectType.Freeze
                 || s.Type == StatusEffectType.Fear)
                && s.Stacks > 0);

        /// <summary>속박 상태면 true — 자발적 이동만 불가하며 공격·기타 행동은 정상 수행한다.
        /// (밀침/끌어당김 등 플레이어·환경에 의한 강제 이동은 제외)</summary>
        public bool IsBound =>
            StatusEffects.Exists(s => s.Type == StatusEffectType.Bondage && s.Stacks > 0);
    }
}
