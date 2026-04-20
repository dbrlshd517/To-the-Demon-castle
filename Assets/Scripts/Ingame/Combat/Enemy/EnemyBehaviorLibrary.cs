using System.Collections.Generic;
using UnityEngine;
using DeckRoguelike.UI;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 기본 제공 적 행동 클래스들을 EnemyBehaviorRegistry에 등록합니다.
    /// CombatController.Start()에서 EnemyBehaviorLibrary.RegisterAll()을 호출하세요.
    ///
    /// 새 적을 추가하려면:
    ///   1. 아래에 EnemyBehavior 상속 클래스를 작성합니다.
    ///   2. RegisterAll()에 EnemyData.enemyCode와 함께 Register 한 줄을 추가합니다.
    /// </summary>
    public static class EnemyBehaviorLibrary
    {
        public static void RegisterAll()
        {
            EnemyBehaviorRegistry.Register(10100, () => new SlimeBehavior());
            EnemyBehaviorRegistry.Register(10200, () => new SkeletonWarriorBehavior());
            EnemyBehaviorRegistry.Register(10300, () => new StoneGuardianBehavior());
            EnemyBehaviorRegistry.Register(10400, () => new DemonLordBehavior());
        }
    }

    // ── 행동 구현 ─────────────────────────────────────────────────────

    /// <summary>
    /// 슬라임: 1칸 내에 플레이어가 있으면 공격, 없으면 플레이어 방향으로 1칸 이동.
    /// 인접 = 상하좌우 1칸
    /// </summary>
    public class SlimeBehavior : EnemyBehavior
    {
        private static readonly Vector2Int[] Adjacent =
        {
            new Vector2Int( 1,  0),
            new Vector2Int(-1,  0),
            new Vector2Int( 0,  1),
            new Vector2Int( 0, -1),
        };

        private bool        _willAttack;
        private Vector2Int  _plannedMoveTarget;

        /// <summary>
        /// 플레이어 턴 시작 전에 호출. 공격/이동을 완전히 결정하고 프리뷰 목록에 저장.
        /// </summary>
        public override void PlanTurn(EnemyInstance self, CombatController combat)
        {
            plannedAttackPositions.Clear();
            plannedMovePositions.Clear();

            Vector2Int playerPos = combat.PlayerSpawnCell;

            bool adjacent = false;
            foreach (var dir in Adjacent)
            {
                if (self.GridPos + dir == playerPos) { adjacent = true; break; }
            }

            if (adjacent)
            {
                _willAttack = true;
                plannedAttackPositions.Add(playerPos);
            }
            else
            {
                _willAttack = false;
                _plannedMoveTarget = CalcMoveTarget(self, combat, playerPos);
                plannedMovePositions.Add(_plannedMoveTarget);
            }
        }

        /// <summary>
        /// 적 턴에 호출. PlanTurn에서 결정된 행동을 그대로 실행.
        /// </summary>
        public override void ExecuteTurn(EnemyInstance self, CombatController combat)
        {
            if (_willAttack)
            {
                Vector2Int attackPos = plannedAttackPositions.Count > 0
                    ? plannedAttackPositions[0]
                    : combat.PlayerSpawnCell;
                int dealt = combat.ApplyDamageToPlayerAt(self.Damage, attackPos, self);
                Debug.Log($"[SlimeBehavior] {self.Name} @ {self.GridPos} → 플레이어 공격 {self.Damage} (실제 {dealt})");
            }
            else
            {
                bool moved = combat.TryMoveEnemy(self, _plannedMoveTarget);
                Debug.Log($"[SlimeBehavior] {self.Name} @ {self.GridPos} → {(moved ? $"{_plannedMoveTarget}으로 이동" : "이동 불가, 대기")}");
            }
        }

        private Vector2Int CalcMoveTarget(EnemyInstance self, CombatController combat, Vector2Int playerPos)
        {
            // 플레이어 인접 빈 칸 중 맨해튼 거리가 가장 가까운 칸을 목표로 선택
            Vector2Int bestTarget = playerPos;
            int bestDist = int.MaxValue;
            bool foundFree = false;

            foreach (var dir in Adjacent)
            {
                Vector2Int surroundPos = playerPos + dir;
                if (!combat.IsInBoard(surroundPos)) continue;
                if (!combat.IsCellFreeForEnemy(surroundPos)) continue;

                int dist = Mathf.Abs(surroundPos.x - self.GridPos.x)
                         + Mathf.Abs(surroundPos.y - self.GridPos.y);
                if (dist < bestDist)
                {
                    bestDist   = dist;
                    bestTarget = surroundPos;
                    foundFree  = true;
                }
            }

            // 주변이 모두 막혀 있으면 플레이어 방향으로 직진
            if (!foundFree)
                bestTarget = playerPos;

            Vector2Int diff    = bestTarget - self.GridPos;
            Vector2Int moveDir = Mathf.Abs(diff.x) >= Mathf.Abs(diff.y)
                ? new Vector2Int((int)Mathf.Sign(diff.x), 0)
                : new Vector2Int(0, (int)Mathf.Sign(diff.y));
            return self.GridPos + moveDir;
        }

        public override string GetIntentText(EnemyInstance self)
            => _willAttack ? $"공격 {self.Damage}" : $"이동 → {_plannedMoveTarget}";
    }

    /// <summary>
    /// 해골 전사: 플레이어를 향해 1칸 이동 후 공격.
    /// </summary>
    public class SkeletonWarriorBehavior : EnemyBehavior
    {
        public override void ExecuteTurn(EnemyInstance self, CombatController combat)
        {
            // TODO: 플레이어 방향으로 이동 → 인접 시 공격
            Debug.Log($"[SkeletonWarriorBehavior] 해골 전사 행동 @ {self.GridPos}");
        }
    }

    /// <summary>
    /// 석상 수호자: 제자리 방어 자세. 매 턴 Block 획득, 도발 효과.
    /// </summary>
    public class StoneGuardianBehavior : EnemyBehavior
    {
        public override void OnSpawn(EnemyInstance self, CombatController combat)
        {
            // TODO: 도발 상태 적용
            Debug.Log($"[StoneGuardianBehavior] 석상 수호자 소환 @ {self.GridPos}");
        }

        public override void ExecuteTurn(EnemyInstance self, CombatController combat)
        {
            // TODO: Block 획득 + 도발 유지
            Debug.Log($"[StoneGuardianBehavior] 석상 수호자 행동 @ {self.GridPos}");
        }

        public override string GetIntentText(EnemyInstance self) => "방어";
    }

    /// <summary>
    /// 악마 군주: 페이즈 전환 보스.
    ///   페이즈 1 (HP 50% 이상): 이동 → 강공격 → 소환 순서로 순환
    ///   페이즈 2 (HP 50% 미만): 매 턴 전체 공격 + 소환
    /// </summary>
    public class DemonLordBehavior : EnemyBehavior
    {
        private int _phase = 1;
        private int _patternIndex = 0;  // 페이즈 1 패턴 순서

        public override void OnSpawn(EnemyInstance self, CombatController combat)
        {
            _phase = 1;
            _patternIndex = 0;
            Debug.Log($"[DemonLordBehavior] 악마 군주 등장 @ {self.GridPos}");
        }

        public override void ExecuteTurn(EnemyInstance self, CombatController combat)
        {
            // 페이즈 전환 체크
            if (_phase == 1 && self.CurrentHP <= self.MaxHP / 2)
            {
                _phase = 2;
                Debug.Log("[DemonLordBehavior] 페이즈 2 돌입!");
                // TODO: 페이즈 전환 연출
            }

            if (_phase == 1) Phase1(self, combat);
            else             Phase2(self, combat);
        }

        public override string GetIntentText(EnemyInstance self)
        {
            if (_phase == 2) return "분노";
            return _patternIndex switch { 0 => "이동", 1 => "강공격", 2 => "소환", _ => "공격" };
        }

        private void Phase1(EnemyInstance self, CombatController combat)
        {
            switch (_patternIndex)
            {
                case 0:
                    // TODO: 플레이어 방향으로 이동
                    Debug.Log("[DemonLordBehavior] 페이즈1 이동");
                    break;
                case 1:
                    // TODO: 강공격 (baseDamage * 2)
                    Debug.Log("[DemonLordBehavior] 페이즈1 강공격");
                    break;
                case 2:
                    // TODO: 소환
                    Debug.Log("[DemonLordBehavior] 페이즈1 소환");
                    break;
            }
            _patternIndex = (_patternIndex + 1) % 3;
        }

        private void Phase2(EnemyInstance self, CombatController combat)
        {
            // TODO: 전체 공격 + 소환
            Debug.Log("[DemonLordBehavior] 페이즈2 전체 공격 + 소환");
        }
    }
}
