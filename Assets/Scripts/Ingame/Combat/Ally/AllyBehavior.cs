using System.Collections.Generic;
using UnityEngine;
using DeckRoguelike.UI;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 아군 유닛 행동의 추상 기반 클래스.
    ///
    /// [사용법]
    ///   1. 이 클래스를 상속받아 구체 행동 클래스를 작성합니다.
    ///   2. AllyBehaviorRegistry.Register("ally_archer", () => new ArcherBehavior()) 로 등록합니다.
    ///   3. AllyData.behaviorId 에 같은 ID 문자열을 설정합니다.
    ///
    /// [인스턴스 방식]
    ///   행동 객체는 유닛 소환 시 유닛마다 개별 생성됩니다 (AllyInstance.Behavior).
    ///   따라서 내부 상태를 멤버 변수로 안전하게 관리할 수 있습니다.
    /// </summary>
    public abstract class AllyBehavior
    {
        /// <summary>
        /// PlanTurn에서 행동할 좌표를 타입별로 여기에 저장하세요.
        /// GetXxxPreviewPositions가 이 목록을 자동으로 반환합니다.
        /// </summary>
        protected List<Vector2Int> plannedAttackPositions = new List<Vector2Int>();
        protected List<Vector2Int> plannedSkillPositions  = new List<Vector2Int>();
        protected List<Vector2Int> plannedMovePositions   = new List<Vector2Int>();

        /// <summary>유닛이 전투에 소환될 때 1회 호출됩니다.</summary>
        public virtual void OnSpawn(AllyInstance self, CombatController combat) { }

        /// <summary>
        /// 플레이어 턴 시작 시 호출됩니다. 이번 아군 턴에 할 행동을 미리 결정하고 내부에 저장하세요.
        /// 공격 좌표는 plannedAttackPositions에 저장하면 자동으로 프리뷰에 반영됩니다.
        /// </summary>
        public virtual void PlanTurn(AllyInstance self, CombatController combat) { }

        /// <summary>이 유닛의 턴마다 호출됩니다.</summary>
        public abstract void ExecuteTurn(AllyInstance self, CombatController combat);

        /// <summary>유닛이 사망할 때 호출됩니다.</summary>
        public virtual void OnDeath(AllyInstance self, CombatController combat) { }

        /// <summary>UI에 표시할 다음 행동 설명 텍스트를 반환합니다.</summary>
        public virtual string GetIntentText(AllyInstance self) => "공격";

        /// <summary>PlanTurn에서 plannedAttackPositions에 저장한 좌표를 반환합니다.</summary>
        public virtual List<Vector2Int> GetAttackPreviewPositions(AllyInstance self)
            => plannedAttackPositions;

        public virtual List<Vector2Int> GetSkillPreviewPositions(AllyInstance self)
            => plannedSkillPositions;

        public virtual List<Vector2Int> GetMovePreviewPositions(AllyInstance self)
            => plannedMovePositions;
    }
}
