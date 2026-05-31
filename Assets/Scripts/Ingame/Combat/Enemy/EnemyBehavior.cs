using System;
using System.Collections.Generic;
using UnityEngine;
using DeckRoguelike.UI;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 적 유닛 행동의 추상 기반 클래스 + 헬퍼 라이브러리.
    ///
    /// [작성 방식]
    ///   1. 이 클래스를 상속받아 PlanTurn만 오버라이드하면 됩니다.
    ///   2. PlanTurn 안에서 Plan*** 헬퍼를 호출하면 ExecuteTurn은 자동으로 처리됩니다.
    ///   3. 한 턴에 공격/이동/기타(블록·버프·소환 등)를 모두 등록할 수 있습니다.
    ///      각 슬롯은 독립적이라 공격 + 이동을 둘 다 호출하면 둘 다 실행됩니다.
    ///   4. EnemyBehaviorRegistry.Register(enemyCode, () => new MyBehavior()) 로 등록.
    ///
    /// [슬롯 구조 — 한 턴 안에서 동시에 등록 가능]
    ///   ① 공격 슬롯 : PlanTargetAttack / PlanRangeAttack / PlanUnavoidableAttack
    ///                  / PlanAttackAndMoveToCell (공격 후 그 좌표로 이동까지 한 슬롯에서 처리)
    ///   ② 이동 슬롯 (모두 Phase 2 지연 — player 턴 종료 후 BoardController가 정렬·실행):
    ///       PlanMoveTowardNearest / PlanMoveTowardPlayer / PlanMoveTowardAlly  — BFS 최단경로
    ///       PlanMoveAwayFromPlayer  — greedy 후퇴
    ///       PlanMoveTo / PlanMoveRandomNearPlayer / PlanMoveRandomToOffset  — 텔레포트
    ///         의도(delegate)만 등록되고 실제 칸은 라운드마다 보드 상태 기반으로 계산됩니다.
    ///         가까운 적부터 이동해 셀을 비워주는 효과 + Manhattan/spread tiebreak로 둘러싸기 유도.
    ///   ③ 기타 슬롯 : PlanSetBlock / PlanApplyStatusToSelf
    ///                  / PlanTargetDebuff / PlanRangeDebuff / PlanUnavoidableDebuff
    ///                  / PlanSummon / PlanSelfDestruct / PlanCustom
    ///   같은 슬롯 안에서 마지막 호출만 살아남습니다 (덮어쓰기). 슬롯이 다르면 공존.
    ///   PlanWait는 모든 슬롯을 비웁니다.
    ///   Phase 1 (ExecuteTurn) — 기타 → 공격 (PlanAttackAndMoveToCell의 이동도 여기 포함)
    ///   Phase 2 — 지연 이동을 정렬해 라운드-로빈으로 실행 후 OnTurnFullyResolved로 정리.
    ///
    /// [예시 — 슬라임]
    ///   if (IsPlayerInRange(self, combat, AdjacentFour))
    ///       PlanTargetAttack(self, combat, AttackTarget.Player);
    ///   else
    ///       PlanMoveTowardPlayer(self, combat, 1);
    ///
    /// [예시 — 해골 전사 (공격하면 그 좌표로 이동)]
    ///   if (IsPlayerInRange(self, combat, AdjacentFour))
    ///       PlanAttackAndMoveToCell(self, combat, AttackTarget.Nearest);   // 결합 헬퍼
    ///   else
    ///       PlanMoveTowardPlayer(self, combat, 1);   // Phase 2 지연 이동
    ///
    /// [예시 — 페이즈 보스]
    ///   if (IsHpBelow(self, 0.5f)) {
    ///       PlanRangeAttack(self, combat, new[] { combat.PlayerSpawnCell }, extraDamage: 5, absolute: true);
    ///       PlanMoveTo(self, combat, PickRandomFreeCell(...));
    ///   }
    /// </summary>
    public abstract class EnemyBehavior
    {
        // ── 자주 쓰는 좌표 패턴 (PlanTurn에서 그대로 사용) ─────────────
        /// <summary>상하좌우 1칸 (4칸).</summary>
        public static readonly Vector2Int[] AdjacentFour =
        {
            new Vector2Int( 1,  0), new Vector2Int(-1,  0),
            new Vector2Int( 0,  1), new Vector2Int( 0, -1),
        };

        /// <summary>대각선 1칸 (4칸).</summary>
        public static readonly Vector2Int[] DiagonalFour =
        {
            new Vector2Int( 1,  1), new Vector2Int(-1, -1),
            new Vector2Int( 1, -1), new Vector2Int(-1,  1),
        };

        /// <summary>8방향 1칸 (8칸).</summary>
        public static readonly Vector2Int[] AdjacentEight =
        {
            new Vector2Int( 1,  0), new Vector2Int(-1,  0),
            new Vector2Int( 0,  1), new Vector2Int( 0, -1),
            new Vector2Int( 1,  1), new Vector2Int(-1, -1),
            new Vector2Int( 1, -1), new Vector2Int(-1,  1),
        };

        /// <summary>상하좌우 1~2칸 십자 (8칸).</summary>
        public static readonly Vector2Int[] Cross2 =
        {
            new Vector2Int( 1,  0), new Vector2Int( 2,  0),
            new Vector2Int(-1,  0), new Vector2Int(-2,  0),
            new Vector2Int( 0,  1), new Vector2Int( 0,  2),
            new Vector2Int( 0, -1), new Vector2Int( 0, -2),
        };

        /// <summary>나이트(체스) 이동 위치 8칸.</summary>
        public static readonly Vector2Int[] KnightJump =
        {
            new Vector2Int( 2,  1), new Vector2Int( 1,  2),
            new Vector2Int(-1,  2), new Vector2Int(-2,  1),
            new Vector2Int( 2, -1), new Vector2Int( 1, -2),
            new Vector2Int(-1, -2), new Vector2Int(-2, -1),
        };

        // ── 미리보기 좌표 (헬퍼가 자동 채움) ─────────────
        protected List<Vector2Int> plannedAttackPositions = new List<Vector2Int>();
        protected List<Vector2Int> plannedSkillPositions  = new List<Vector2Int>();
        // plannedMovePositions: 이동 step 수 카운팅용 — 멀티셀이어도 step 당 anchor 1개만.
        protected List<Vector2Int> plannedMovePositions   = new List<Vector2Int>();
        // plannedMoveSpritePositions: 화면에 표시할 이동 sprite 좌표 — 멀티셀이면 이동 후 footprint 전체.
        // (1x1이면 비워두고 GetMoveSpritePositions가 plannedMovePositions를 fallback으로 사용)
        protected List<Vector2Int> plannedMoveSpritePositions = new List<Vector2Int>();

        // ── 내부 슬롯 상태 (각 Plan* 헬퍼가 자기 슬롯만 갱신) ──────────────
        // 공격 슬롯
        private readonly List<Vector2Int> _attackOffsets        = new List<Vector2Int>();   // 적 기준 상대 좌표 (밀려도 추적)
        private readonly List<Vector2Int> _attackAbsoluteCells  = new List<Vector2Int>();   // 절대 좌표 (Plan*Cell류)
        private Action<EnemyInstance, BoardController> _attackAction;
        private string _attackIntent;

        // 이동 슬롯 (모두 Phase 2 — player 턴 종료 후 BoardController가 정렬·실행)
        // 등록 시 next-step delegate 만 저장, 실제 칸은 라운드마다 보드 상태 기반 계산.
        // (공격좌표이동 결합인 PlanAttackAndMoveToCell만 _attackAction에 묶여 Phase 1)
        private Func<EnemyInstance, BoardController, Vector2Int> _deferredNextStep;
        private Func<EnemyInstance, BoardController, Vector2Int> _deferredSortAnchor;
        private int _deferredMoveSteps = 0;
        private string _moveIntent;

        public bool HasDeferredMove => _deferredNextStep != null && _deferredMoveSteps > 0;
        public int DeferredMoveSteps => _deferredMoveSteps;

        /// <summary>모든 슬롯(공격/이동/기타)과 미리보기 좌표를 비웁니다.
        /// 재계산 시 이전 PlanTurn의 stale 슬롯이 다음 턴에 함께 실행되는 버그를 막기 위해 사용.</summary>
        public void ResetPlanState() => ClearAllSlots();

        // _seqCounter 스냅샷/복원 — 플레이어가 이동할 때마다 PlanTurn을 재호출해도
        // NextSequential 기반 패턴(예: 골블린 궁수의 충전→공격 사이클)이 이중 진행되지 않도록.
        // 핵심: 스냅샷은 턴 시작 시 BeginTurnSeqSnapshot에서 "초기 PlanTurn 호출 직전" 값을 잡아둔다.
        // RePlan은 PlanTurn 재호출 직전에 _seqCounter를 그 값으로 되돌리므로, 매 RePlan이 동일한
        // 시작점에서 패턴을 결정 → 동일한 카테고리가 다시 선택된다 (이전 구현은 초기 PlanTurn이
        // 증가시킨 값을 스냅으로 잡아 RePlan마다 반대 step으로 깜빡이는 버그가 있었음).
        public int SeqCounterSnapshot => _preTurnSeqCounter;
        public void RestoreSeqCounter(int v) => _seqCounter = v;
        public void BeginTurnSeqSnapshot() => _preTurnSeqCounter = _seqCounter;

        // 기타 슬롯 (블록/버프/소환/커스텀)
        private Action<EnemyInstance, BoardController> _extraAction;
        private string _extraIntent;

        // 패턴 카운터 (턴 사이 보존) — _preTurnSeqCounter는 이번 턴 시작 시 _seqCounter 값을 보관해
        // RePlan이 같은 시작점에서 PlanTurn을 다시 돌릴 수 있게 한다.
        private int _seqCounter;
        private int _preTurnSeqCounter;

        // ── 행동 카테고리 잠금 (한 플레이어 턴 동안 공격/이동 결정 고정) ──────────────
        // 슬라임·폭탄병류는 매 재계산(RePlan)마다 사거리 판정이 다시 내려져 공격↔이동 인텐트가
        // 깜빡인다. 첫 PlanTurn에서 정한 카테고리를 여기에 저장하고, 같은 턴의 재계산에서는 그
        // 카테고리를 유지한다. ResetPlanState(슬롯 비우기)는 이 값을 건드리지 않으므로 재계산을
        // 거쳐도 잠금이 살아남고, 적 턴 종료 시 OnTurnFullyResolved에서 자동 해제 → 다음 턴 새 결정.
        protected enum LockedCategory { None, Attack, Move }
        private LockedCategory _lockedCategory = LockedCategory.None;

        /// <summary>이번 플레이어 턴에 공격/이동 카테고리가 이미 확정됐는지.</summary>
        protected bool IsActionLocked => _lockedCategory != LockedCategory.None;
        /// <summary>확정된 카테고리(None/Attack/Move).</summary>
        protected LockedCategory ActionLockCategory => _lockedCategory;
        /// <summary>이번 턴 행동 카테고리를 확정. 이미 확정돼 있으면 무시.</summary>
        protected void LockActionCategory(LockedCategory c)
        {
            if (_lockedCategory == LockedCategory.None) _lockedCategory = c;
        }

        // ── 표준 hooks ────────────────────────────────────────────
        /// <summary>유닛이 전투에 소환될 때 1회 호출됩니다.</summary>
        public virtual void OnSpawn(EnemyInstance self, BoardController board) { }

        /// <summary>인카운터의 모든 적이 보드에 배치된 직후, 각 적마다 1회 호출됩니다.
        /// OnSpawn은 자기 차례에 호출되어 뒤에 나올 적들이 아직 배치 전이지만, 이 훅 시점에는
        /// 모든 적이 보드에 올라가 있어 군집 배치(예: 뱀 몸통을 머리에 이어붙이기)에 안전합니다.</summary>
        public virtual void OnAllEnemiesSpawned(EnemyInstance self, BoardController board) { }

        /// <summary>
        /// 플레이어 턴 시작 시 호출. 이번 적 턴 행동을 결정해 Plan* 헬퍼로 등록하세요.
        /// </summary>
        public virtual void PlanTurn(EnemyInstance self, BoardController board) { }

        /// <summary>
        /// 적 턴 Phase 1 — 기타·공격 슬롯 실행 (PlanAttackAndMoveToCell의 공격좌표이동도 여기 포함).
        /// 일반 이동은 모두 Phase 2(BoardController가 정렬·실행)로 미뤄집니다.
        /// 실행 순서: 기타 → 공격.
        /// </summary>
        public virtual void ExecuteTurn(EnemyInstance self, BoardController board)
        {
            _extraAction?.Invoke(self, board);
            _attackAction?.Invoke(self, board);
            // Phase 1 슬롯만 정리. 지연 이동은 Phase 2 종료 후 OnTurnFullyResolved에서 클리어.
            ClearAttackSlot();
            ClearExtraSlot();
            plannedSkillPositions.Clear();
            plannedAttackPositions.Clear();
        }

        /// <summary>Phase 2 종료 후 BoardController가 호출 — 지연 이동 슬롯 정리.</summary>
        public virtual void OnTurnFullyResolved(EnemyInstance self, BoardController board)
        {
            ClearMoveSlot();
            _lockedCategory = LockedCategory.None;   // 행동 잠금 해제 — 다음 턴에 새로 결정
        }

        /// <summary>유닛이 사망할 때 호출됩니다.</summary>
        public virtual void OnDeath(EnemyInstance self, BoardController board) { }

        /// <summary>유닛이 피해를 받은 직후(현재 HP 갱신 직후) 호출됩니다.
        /// HP 임계 반응(예: 대왕 슬라임의 즉시 자폭) 등에 사용. amount는 실제 적용된 데미지 양.</summary>
        public virtual void OnDamaged(EnemyInstance self, BoardController board, int amount) { }

        /// <summary>슬롯에 등록된 인텐트를 합쳐 표시.</summary>
        public virtual string GetIntentText(EnemyInstance self)
        {
            string a = _attackIntent;
            string m = _moveIntent;
            string e = _extraIntent;
            var parts = new List<string>(3);
            if (!string.IsNullOrEmpty(e)) parts.Add(e);
            if (!string.IsNullOrEmpty(a)) parts.Add(a);
            if (!string.IsNullOrEmpty(m)) parts.Add(m);
            return parts.Count == 0 ? "대기" : string.Join(" + ", parts);
        }

        /// <summary>공격 미리보기 — 상대 좌표(밀침 추적) + 절대 좌표 합산.</summary>
        public virtual List<Vector2Int> GetAttackPreviewPositions(EnemyInstance self)
        {
            plannedAttackPositions.Clear();
            foreach (var off in _attackOffsets)
                plannedAttackPositions.Add(self.GridPos + off);
            foreach (var pos in _attackAbsoluteCells)
                plannedAttackPositions.Add(pos);
            return plannedAttackPositions;
        }

        public virtual List<Vector2Int> GetSkillPreviewPositions(EnemyInstance self)
            => plannedSkillPositions;

        public virtual List<Vector2Int> GetMovePreviewPositions(EnemyInstance self)
            => plannedMovePositions;

        /// <summary>이동 sprite로 표시할 셀 좌표. 멀티셀 적은 이동 후 점유할 footprint 전체를 반환.
        /// 1x1 적은 plannedMovePositions(anchor)를 그대로 사용. RefreshEnemyActionIcons의 step 카운트는
        /// plannedMovePositions를 따로 사용하므로 sprite 좌표가 늘어나도 행동 아이콘 숫자는 변하지 않는다.</summary>
        public virtual List<Vector2Int> GetMoveSpritePositions(EnemyInstance self)
            => plannedMoveSpritePositions.Count > 0 ? plannedMoveSpritePositions : plannedMovePositions;

        /// <summary>
        /// Phase 2 지연 이동의 첫 step을 미리 계산해 plannedMovePositions에 담는다.
        /// 플레이어 턴 시작 시 / 플레이어 이동 시 호출되어 이동 미리보기 sprite의 좌표가 된다.
        /// 실제 이동은 여전히 Phase 2 (player 턴 종료 후)에서 ExecuteDeferredMoveStep으로 처리.
        /// 멀티셀 적은 plannedMoveSpritePositions에 "새로 점유될 셀"(new footprint − old footprint)만 채워
        /// 이동으로 실제 변하는 칸만 sprite로 표시. (기존 점유칸은 그대로 유지되므로 표시 X)
        /// </summary>
        public virtual void ComputeDeferredMovePreview(EnemyInstance self, BoardController board)
        {
            plannedMovePositions.Clear();
            plannedMoveSpritePositions.Clear();
            if (self.IsBound) return; // 속박: 이동 미리보기 표시 안 함 (공격 미리보기는 유지)
            if (!HasDeferredMove) return;
            Vector2Int next = _deferredNextStep(self, board);
            if (next == self.GridPos) return;
            if (!board.IsInBoard(next)) return;
            plannedMovePositions.Add(next);

            Vector2Int size = self.Size;
            if (size.x > 1 || size.y > 1)
            {
                // old footprint 집합 — 새 footprint와 겹치는 셀은 그대로 유지되므로 sprite 제외.
                var oldFootprint = new HashSet<Vector2Int>();
                foreach (var c in self.OccupiedCells) oldFootprint.Add(c);

                for (int dx = 0; dx < size.x; dx++)
                for (int dy = 0; dy < size.y; dy++)
                {
                    Vector2Int p = new Vector2Int(next.x + dx, next.y - dy);
                    if (!board.IsInBoard(p)) continue;
                    if (oldFootprint.Contains(p)) continue; // 이미 점유 중 → 변하지 않음
                    plannedMoveSpritePositions.Add(p);
                }
            }
        }

        // ──────────────────────────────────────────────────────────
        // 조건 검사 헬퍼
        // ──────────────────────────────────────────────────────────

        /// <summary>플레이어 또는 아군이 self의 footprint 중 한 셀 + offsets[i]에 위치하면 true.
        /// 역투영 알고리즘: 각 offset에 대해 (target - offset)이 footprint 사각형 안에 있는지 O(1)로 체크.
        /// → footprint 크기와 무관하게 O(offsets) 비용, gappy offset 패턴에도 정확.</summary>
        protected bool IsTargetInRange(EnemyInstance self, BoardController board, Vector2Int[] offsets)
        {
            if (offsets == null) return false;
            if (IsInRangeFromFootprint(self.GridPos, self.Size, board.PlayerSpawnCell, offsets)) return true;
            foreach (var ally in board.GetAllies())
                if (ally.CurrentHP > 0 &&
                    IsInRangeFromFootprint(self.GridPos, self.Size, ally.GridPos, offsets))
                    return true;
            return false;
        }

        /// <summary>플레이어만 offsets 안에 있는지 확인. IsTargetInRange의 플레이어 전용 버전.</summary>
        protected bool IsPlayerInRange(EnemyInstance self, BoardController board, Vector2Int[] offsets)
        {
            if (offsets == null) return false;
            return IsInRangeFromFootprint(self.GridPos, self.Size, board.PlayerSpawnCell, offsets);
        }

        /// <summary>역투영 InRange 체크: target에 닿으려면 footprint 어느 셀에서 출발해야 하는지 거꾸로 계산하고
        /// 그 셀이 footprint 사각형 안에 있는지만 본다. 모든 occupied cell을 enumerate하지 않으므로 footprint 크기와 무관.
        /// footprint 좌표: (anchor.x + dx, anchor.y - dy), dx∈[0,W-1], dy∈[0,H-1].</summary>
        private static bool IsInRangeFromFootprint(Vector2Int anchor, Vector2Int size, Vector2Int target, Vector2Int[] offsets)
        {
            foreach (var o in offsets)
            {
                int dx = (target.x - o.x) - anchor.x;       // 기대 범위 [0, W-1]
                int dy = anchor.y - (target.y - o.y);       // 기대 범위 [0, H-1] (y가 아래로 갈수록 증가)
                if (dx >= 0 && dx < size.x && dy >= 0 && dy < size.y) return true;
            }
            return false;
        }

        /// <summary>가장 가까운 적대 대상(플레이어 또는 아군) 위치를 반환합니다. 대상이 없으면 self.GridPos.
        /// 거리는 footprint 사각형에서 target까지의 최소 Manhattan 거리 (size ≥ 2 적도 정확).</summary>
        protected Vector2Int FindNearestTargetPos(EnemyInstance self, BoardController board)
        {
            Vector2Int best = board.PlayerSpawnCell;
            int bestDist = FootprintMinManhattan(self.GridPos, self.Size, best);
            foreach (var ally in board.GetAllies())
            {
                if (ally.CurrentHP <= 0) continue;
                int d = FootprintMinManhattan(self.GridPos, self.Size, ally.GridPos);
                if (d < bestDist) { bestDist = d; best = ally.GridPos; }
            }
            return best;
        }

        /// <summary>플레이어까지의 맨해튼 거리. footprint 사각형 기준 (점→사각형 최소 거리).</summary>
        protected int ManhattanToPlayer(EnemyInstance self, BoardController board)
            => FootprintMinManhattan(self.GridPos, self.Size, board.PlayerSpawnCell);

        /// <summary>가장 가까운 대상까지의 맨해튼 거리 (플레이어·아군 중 최소값). footprint 기준.</summary>
        protected int ManhattanToNearest(EnemyInstance self, BoardController board)
        {
            int d = ManhattanToPlayer(self, board);
            foreach (var ally in board.GetAllies())
                if (ally.CurrentHP > 0)
                    d = Mathf.Min(d, FootprintMinManhattan(self.GridPos, self.Size, ally.GridPos));
            return d;
        }

        /// <summary>플레이어까지의 체비쇼프 거리. footprint 사각형 기준 (점→사각형 최소 거리).</summary>
        protected int ChebyshevToPlayer(EnemyInstance self, BoardController board)
            => FootprintMinChebyshev(self.GridPos, self.Size, board.PlayerSpawnCell);

        /// <summary>가장 가까운 대상까지의 체비쇼프 거리 (플레이어·아군 중 최소값). footprint 기준.</summary>
        protected int ChebyshevToNearest(EnemyInstance self, BoardController board)
        {
            int d = ChebyshevToPlayer(self, board);
            foreach (var ally in board.GetAllies())
                if (ally.CurrentHP > 0)
                    d = Mathf.Min(d, FootprintMinChebyshev(self.GridPos, self.Size, ally.GridPos));
            return d;
        }

        /// <summary>(anchor, size) footprint에서 target까지의 최소 Chebyshev 거리 (max(|dx|,|dy|)).
        /// 점→사각형 공식 O(1).</summary>
        private static int FootprintMinChebyshev(Vector2Int anchor, Vector2Int size, Vector2Int target)
        {
            int xMin = anchor.x;
            int xMax = anchor.x + size.x - 1;
            int yMin = anchor.y - size.y + 1;
            int yMax = anchor.y;
            int dx = target.x < xMin ? xMin - target.x
                   : target.x > xMax ? target.x - xMax : 0;
            int dy = target.y < yMin ? yMin - target.y
                   : target.y > yMax ? target.y - yMax : 0;
            return Mathf.Max(dx, dy);
        }

        /// <summary>현재 HP가 MaxHP × ratio 이하면 true.</summary>
        protected bool IsHpBelow(EnemyInstance self, float ratio)
            => self != null && self.MaxHP > 0 && self.CurrentHP <= self.MaxHP * ratio;

        /// <summary>현재 HP가 MaxHP × ratio 초과면 true.</summary>
        protected bool IsHpAbove(EnemyInstance self, float ratio)
            => self != null && self.MaxHP > 0 && self.CurrentHP > self.MaxHP * ratio;

        // ──────────────────────────────────────────────────────────
        // 선택 패턴 헬퍼
        // ──────────────────────────────────────────────────────────

        /// <summary>호출할 때마다 0,1,...,modulo-1 순서를 반환한 뒤 카운터 +1.</summary>
        protected int NextSequential(int modulo)
        {
            if (modulo <= 0) return 0;
            int v = _seqCounter % modulo;
            _seqCounter++;
            return v;
        }

        /// <summary>0 ~ sides-1 사이 무작위 정수.</summary>
        protected int RollDice(int sides) => UnityEngine.Random.Range(0, Mathf.Max(1, sides));

        /// <summary>percent% 확률로 true (0~100).</summary>
        protected bool RollChance(int percent) => UnityEngine.Random.Range(0, 100) < percent;

        /// <summary>주어진 옵션 중 무작위 1개 반환.</summary>
        protected T PickRandom<T>(params T[] options)
        {
            if (options == null || options.Length == 0) return default;
            return options[UnityEngine.Random.Range(0, options.Length)];
        }

        // ──────────────────────────────────────────────────────────
        // 공격 슬롯 헬퍼 — 호출 시 공격 슬롯만 덮어씀
        // ──────────────────────────────────────────────────────────

        public enum AttackTarget { Player, Ally, Nearest }

        /// <summary>
        /// 지정 대상을 직접 공격. 사거리 체크 없음 — PlanTurn에서 IsPlayerInRange 등으로 미리 확인할 것.
        /// </summary>
        protected void PlanTargetAttack(EnemyInstance self, BoardController board,
                                        AttackTarget target, int extraDamage = 0)
        {
            ClearAttackSlot();
            int dmg = Mathf.Max(0, self.Damage + extraDamage);
            _attackIntent = $"공격 {dmg}";

            Vector2Int targetPos = ResolveTargetPos(self, board, target);
            Vector2Int offset = targetPos - self.GridPos;
            _attackOffsets.Add(offset);

            _attackAction = (s, c) =>
            {
                Vector2Int cell = s.GridPos + offset;
                c.ApplyDamageAtCell(dmg, cell, s);
            };
        }

        /// <summary>
        /// 좌표들에 광역 공격.
        /// absolute=false(기본): 적 기준 상대 좌표. absolute=true: 절대 좌표.
        /// </summary>
        protected void PlanRangeAttack(EnemyInstance self, BoardController board,
                                       Vector2Int[] offsets, int extraDamage = 0,
                                       bool absolute = false)
        {
            ClearAttackSlot();
            if (offsets == null || offsets.Length == 0) return;

            int dmg = Mathf.Max(0, self.Damage + extraDamage);
            _attackIntent = offsets.Length > 1 ? $"광역 {dmg}" : $"공격 {dmg}";
            var copy = (Vector2Int[])offsets.Clone();

            if (absolute)
            {
                foreach (var p in copy) _attackAbsoluteCells.Add(p);
                _attackAction = (s, c) =>
                {
                    foreach (var p in copy)
                        c.ApplyDamageAtCell(dmg, p, s);
                };
            }
            else
            {
                foreach (var o in copy) _attackOffsets.Add(o);
                _attackAction = (s, c) =>
                {
                    foreach (var o in copy)
                        c.ApplyDamageAtCell(dmg, s.GridPos + o, s);
                };
            }
        }

        /// <summary>
        /// 지정 대상에게 회피 불가 공격. 위치와 무관하게 직접 데미지 적용.
        /// target=Ally일 때 targetAlly를 지정하세요.
        /// </summary>
        protected void PlanUnavoidableAttack(EnemyInstance self, BoardController board,
                                             AttackTarget target, int extraDamage = 0,
                                             AllyInstance targetAlly = null)
        {
            ClearAttackSlot();
            int dmg = Mathf.Max(0, self.Damage + extraDamage);
            _attackIntent = $"공격 {dmg}";

            switch (target)
            {
                case AttackTarget.Player:
                    _attackAbsoluteCells.Add(board.PlayerSpawnCell);
                    _attackAction = (s, c) => c.ApplyDamageToPlayer(dmg, s);
                    break;
                case AttackTarget.Ally:
                    if (targetAlly == null || targetAlly.CurrentHP <= 0) return;
                    var a = targetAlly;
                    _attackAbsoluteCells.Add(a.GridPos);
                    _attackAction = (s, c) => ApplyDamageToAllyAt(c, a.GridPos, dmg);
                    break;
                default:
                    Vector2Int nearestPos = FindNearestTargetPos(self, board);
                    _attackAbsoluteCells.Add(nearestPos);
                    bool isPlayer = (nearestPos == board.PlayerSpawnCell);
                    _attackAction = (s, c) =>
                    {
                        if (isPlayer)
                            c.ApplyDamageToPlayer(dmg, s);
                        else
                            ApplyDamageToAllyAt(c, nearestPos, dmg);
                    };
                    break;
            }
        }

        /// <summary>
        /// 공격 + 공격한 좌표로 이동 결합. 공격 슬롯 안에서 데미지 후 즉시 TryMoveEnemy를 호출.
        /// Phase 1에서 한 번에 실행되어 사용자 요구상 무조건 다른 모든 이동보다 먼저 처리됨.
        /// 대상이 살아있어 셀이 점유돼 있으면 이동만 자동 실패. 공격으로 처치 시 그 빈 칸으로 진입.
        /// </summary>
        protected void PlanAttackAndMoveToCell(EnemyInstance self, BoardController board,
                                               AttackTarget target, int extraDamage = 0)
        {
            ClearAttackSlot();
            int dmg = Mathf.Max(0, self.Damage + extraDamage);
            Vector2Int targetPos = ResolveTargetPos(self, board, target);
            Vector2Int offset = targetPos - self.GridPos;
            _attackOffsets.Add(offset);
            _attackIntent = $"공격 {dmg}";

            _attackAction = (s, c) =>
            {
                Vector2Int cell = s.GridPos + offset;
                c.ApplyDamageAtCell(dmg, cell, s);
                c.TryMoveEnemy(s, cell);
            };
        }

        /// <summary>
        /// 절대 좌표 셀에 공격 + 그 셀로 이동. PlanAttackAndMoveToCell의 절대좌표 버전.
        /// 셀이 PlanTurn 시점에 고정되므로 RePlan에서 호출해도 처음 잡힌 셀만 노린다 →
        /// 플레이어가 그 셀을 벗어나면 빈 셀을 때리고 미스(회피 가능). 적이 밀려도 같은 셀 유지.
        /// 멜레 카테고리 잠금 후 RePlan에서 좌표를 따라가지 않게 하기 위해 사용.
        /// </summary>
        protected void PlanAttackAndMoveToCell(EnemyInstance self, BoardController board,
                                               Vector2Int absoluteCell, int extraDamage = 0)
        {
            ClearAttackSlot();
            int dmg = Mathf.Max(0, self.Damage + extraDamage);
            _attackAbsoluteCells.Add(absoluteCell);
            _attackIntent = $"공격 {dmg}";

            _attackAction = (s, c) =>
            {
                c.ApplyDamageAtCell(dmg, absoluteCell, s);
                c.TryMoveEnemy(s, absoluteCell);
            };
        }

        /// <summary>
        /// attackCell에 공격하면서 별도 moveCell로 이동. 공격 좌표와 이동 좌표가 다른 경우 사용
        /// (예: 12000 — 플레이어 절대좌표 공격 + 대각선 1칸 이동).
        /// 둘 다 PlanTurn 시점에 절대 좌표로 고정 — 회피 가능, 적이 밀려도 동일 셀 유지.
        /// </summary>
        protected void PlanAttackAtCellAndMoveTo(EnemyInstance self, BoardController board,
                                                  Vector2Int attackCell, Vector2Int moveCell,
                                                  int extraDamage = 0)
        {
            ClearAttackSlot();
            int dmg = Mathf.Max(0, self.Damage + extraDamage);
            _attackAbsoluteCells.Add(attackCell);
            _attackIntent = $"공격 {dmg}";

            _attackAction = (s, c) =>
            {
                c.ApplyDamageAtCell(dmg, attackCell, s);
                c.TryMoveEnemy(s, moveCell);
            };
        }

        private Vector2Int ResolveTargetPos(EnemyInstance self, BoardController board, AttackTarget target)
        {
            switch (target)
            {
                case AttackTarget.Player: return board.PlayerSpawnCell;
                case AttackTarget.Ally:
                    AllyInstance nearest = null;
                    int best = int.MaxValue;
                    foreach (var ally in board.GetAllies())
                    {
                        if (ally.CurrentHP <= 0) continue;
                        int d = FootprintMinManhattan(self.GridPos, self.Size, ally.GridPos);
                        if (d < best) { best = d; nearest = ally; }
                    }
                    return nearest != null ? nearest.GridPos : board.PlayerSpawnCell;
                default: return FindNearestTargetPos(self, board);
            }
        }

        private static int ApplyDamageToAllyAt(BoardController c, Vector2Int pos, int dmg)
        {
            foreach (var ally in c.GetAllies())
            {
                if (ally.CurrentHP > 0 && ally.GridPos == pos)
                {
                    int actual = Mathf.Max(0, dmg - ally.Block);
                    c.ApplyDamageAtCell(dmg, pos, null);
                    return actual;
                }
            }
            return 0;
        }

        // ──────────────────────────────────────────────────────────
        // 이동 슬롯 헬퍼 — 호출 시 이동 슬롯만 덮어씀
        // ──────────────────────────────────────────────────────────

        // ── 모든 Plan*Move 는 Phase 2 지연 등록 — 의도만 저장, 실행은 player 턴 종료 후 ──

        /// <summary>가장 가까운 대상(플레이어 또는 아군) 방향으로 BFS 최단경로 이동 (Phase 2).</summary>
        protected void PlanMoveTowardNearest(EnemyInstance self, BoardController board, int steps = 1)
        {
            SetDeferredMove(steps, "이동",
                nextStep:   (s, c) => ComputeBfsStepToward(s, c, FindNearestTargetPos(s, c)),
                sortAnchor: (s, c) => FindNearestTargetPos(s, c));
        }

        /// <summary>플레이어 방향으로 BFS 최단경로 이동 (Phase 2).</summary>
        protected void PlanMoveTowardPlayer(EnemyInstance self, BoardController board, int steps = 1)
        {
            SetDeferredMove(steps, "이동",
                nextStep:   (s, c) => ComputeBfsStepToward(s, c, c.PlayerSpawnCell),
                sortAnchor: (s, c) => c.PlayerSpawnCell);
        }

        /// <summary>가장 가까운 아군 방향으로 BFS 최단경로 이동 (Phase 2). 아군이 없으면 플레이어 fallback.</summary>
        protected void PlanMoveTowardAlly(EnemyInstance self, BoardController board, int steps = 1)
        {
            SetDeferredMove(steps, "이동",
                nextStep:   (s, c) => ComputeBfsStepToward(s, c, FindNearestAllyPos(s, c)),
                sortAnchor: (s, c) => FindNearestAllyPos(s, c));
        }

        /// <summary>플레이어 반대 방향으로 1~steps칸 후퇴 (Phase 2). 매 step 보드 상태 기반 greedy.</summary>
        protected void PlanMoveAwayFromPlayer(EnemyInstance self, BoardController board, int steps = 1)
        {
            SetDeferredMove(steps, "후퇴",
                nextStep:   (s, c) => ComputeStepAwayFrom(s, c, c.PlayerSpawnCell),
                sortAnchor: (s, c) => c.PlayerSpawnCell);
        }

        /// <summary>지정한 절대 셀로 한 번에 이동 (Phase 2 — 텔레포트). 셀이 점유돼있으면 자동 실패.</summary>
        protected void PlanMoveTo(EnemyInstance self, BoardController board, Vector2Int targetCell)
        {
            Vector2Int dest = targetCell;
            SetDeferredMove(1, "이동",
                nextStep:   (s, c) => (c.IsInBoard(dest) && c.IsCellFreeForEnemy(dest)) ? dest : s.GridPos,
                sortAnchor: (s, c) => dest);
        }

        /// <summary>플레이어로부터 맨해튼 거리 distance인 빈 셀 중 무작위로 순간이동 (Phase 2).
        /// 후보·빈 셀 판정은 실행 시점의 보드 상태로 매번 재계산.</summary>
        protected void PlanMoveRandomNearPlayer(EnemyInstance self, BoardController board, int distance)
        {
            int dist = distance;
            SetDeferredMove(1, "이동",
                nextStep: (s, c) =>
                {
                    Vector2Int pp = c.PlayerSpawnCell;
                    var free = new List<Vector2Int>();
                    for (int dx = -dist; dx <= dist; dx++)
                    {
                        int dy = dist - Mathf.Abs(dx);
                        Vector2Int p1 = pp + new Vector2Int(dx, dy);
                        if (c.IsInBoard(p1) && c.IsCellFreeForEnemy(p1)) free.Add(p1);
                        if (dy != 0)
                        {
                            Vector2Int p2 = pp + new Vector2Int(dx, -dy);
                            if (c.IsInBoard(p2) && c.IsCellFreeForEnemy(p2)) free.Add(p2);
                        }
                    }
                    return free.Count == 0 ? s.GridPos : free[UnityEngine.Random.Range(0, free.Count)];
                },
                sortAnchor: (s, c) => c.PlayerSpawnCell);
        }

        /// <summary>offsets 좌표 목록 중 빈 셀 하나를 무작위로 골라 순간이동 (Phase 2).
        /// absolute=false(기본): offsets를 plan 시점의 자신(self) 기준 상대좌표로 해석.
        /// absolute=true: offsets를 절대 좌표로 해석.</summary>
        protected void PlanMoveRandomToOffset(EnemyInstance self, BoardController board,
            IEnumerable<Vector2Int> offsets, bool absolute = false)
        {
            // origin은 plan 시점에 고정 (기존 동작 유지)
            Vector2Int origin = absolute ? Vector2Int.zero : self.GridPos;
            var captured = new List<Vector2Int>();
            foreach (var off in offsets) captured.Add(origin + off);
            SetDeferredMove(1, "이동",
                nextStep: (s, c) =>
                {
                    var free = new List<Vector2Int>();
                    foreach (var p in captured)
                        if (c.IsInBoard(p) && c.IsCellFreeForEnemy(p)) free.Add(p);
                    return free.Count == 0 ? s.GridPos : free[UnityEngine.Random.Range(0, free.Count)];
                },
                sortAnchor: (s, c) => c.PlayerSpawnCell);
        }

        /// <summary>지연 이동 슬롯 등록 — 모든 PlanMove* 헬퍼의 공통 진입점.
        /// 서브클래스가 board 상태에 따라 목적지가 달라지는 커스텀 지연 이동을 등록할 때도 사용
        /// (예: 도적 11040 — 공격 후 '노린 셀' 또는 '플레이어 인접 셀'로 이동).</summary>
        protected void SetDeferredMove(int steps, string intentLabel,
            Func<EnemyInstance, BoardController, Vector2Int> nextStep,
            Func<EnemyInstance, BoardController, Vector2Int> sortAnchor)
        {
            ClearMoveSlot();
            _deferredMoveSteps = Mathf.Max(1, steps);
            _deferredNextStep = nextStep;
            _deferredSortAnchor = sortAnchor;
            _moveIntent = _deferredMoveSteps > 1 ? $"{intentLabel} ×{_deferredMoveSteps}" : intentLabel;
        }

        /// <summary>가장 가까운 살아있는 아군 좌표 (없으면 플레이어 좌표). footprint 사각형 기준 최소 거리.</summary>
        private static Vector2Int FindNearestAllyPos(EnemyInstance self, BoardController board)
        {
            AllyInstance nearest = null;
            int best = int.MaxValue;
            foreach (var ally in board.GetAllies())
            {
                if (ally.CurrentHP <= 0) continue;
                int d = FootprintMinManhattan(self.GridPos, self.Size, ally.GridPos);
                if (d < best) { best = d; nearest = ally; }
            }
            return nearest != null ? nearest.GridPos : board.PlayerSpawnCell;
        }

        /// <summary>from에서 멀어지는 인접 빈 칸 1개 (greedy Manhattan). 없으면 self.GridPos.</summary>
        private static Vector2Int ComputeStepAwayFrom(EnemyInstance self, BoardController board, Vector2Int from)
        {
            int curDist = Mathf.Abs(self.GridPos.x - from.x) + Mathf.Abs(self.GridPos.y - from.y);
            Vector2Int best = self.GridPos;
            int bestDist = curDist;
            foreach (var dir in AdjacentFour)
            {
                Vector2Int nxt = self.GridPos + dir;
                if (!board.IsInBoard(nxt)) continue;
                if (!board.IsCellFreeForEnemy(nxt)) continue;
                int d = Mathf.Abs(nxt.x - from.x) + Mathf.Abs(nxt.y - from.y);
                if (d > bestDist) { bestDist = d; best = nxt; }
            }
            return best;
        }

        // ──────────────────────────────────────────────────────────
        // 기타 슬롯 헬퍼 — 블록/버프/디버프/커스텀
        // ──────────────────────────────────────────────────────────

        /// <summary>이번 턴에 자기 방어도(Block)를 amount 값으로 초기화 (덮어쓰기).</summary>
        protected void PlanSetBlock(EnemyInstance self, BoardController board, int amount)
        {
            ClearExtraSlot();
            int n = Mathf.Max(0, amount);
            _extraIntent = $"방어 {n}";
            _extraAction = (s, c) =>
            {
                s.Block = n;
                s.UI?.UpdateShield(s.Block, s.CurrentHP, s.MaxHP);
            };
        }

        /// <summary>자기 자신에게 버프 적용.</summary>
        protected void PlanApplyStatusToSelf(EnemyInstance self, BoardController board,
                                             StatusEffectType type, int stacks)
        {
            ClearExtraSlot();
            int n = Mathf.Max(0, stacks);
            _extraIntent = type == StatusEffectType.Strength ? $"힘 +{n}"
                         : type == StatusEffectType.HealHP   ? $"회복 {n}"
                         : $"버프 {type}";
            _extraAction = (me, c) => c.ApplyStatus(me, type, n);
        }

        /// <summary>
        /// 지정 대상에게 디버프 부여. 사거리 체크 없음 — PlanTurn에서 미리 확인할 것.
        /// target=Ally일 때 targetAlly를 지정하세요.
        /// </summary>
        protected void PlanTargetDebuff(EnemyInstance self, BoardController board,
                                        AttackTarget target, StatusEffectType type, int stacks,
                                        AllyInstance targetAlly = null)
        {
            ClearExtraSlot();
            int n = Mathf.Max(1, stacks);
            _extraIntent = $"디버프 {n}턴";

            switch (target)
            {
                case AttackTarget.Player:
                    _extraAction = (_, c) => c.ApplyStatusToPlayer(type, n);
                    break;
                case AttackTarget.Ally:
                    if (targetAlly == null) return;
                    var a = targetAlly;
                    _extraAction = (_, c) => c.ApplyStatusToAlly(a, type, n);
                    break;
                default:
                    Vector2Int nearestPos = FindNearestTargetPos(self, board);
                    bool isPlayer = (nearestPos == board.PlayerSpawnCell);
                    if (isPlayer)
                    {
                        _extraAction = (_, c) => c.ApplyStatusToPlayer(type, n);
                    }
                    else
                    {
                        AllyInstance foundAlly = null;
                        foreach (var ally in board.GetAllies())
                            if (ally.CurrentHP > 0 && ally.GridPos == nearestPos) { foundAlly = ally; break; }
                        if (foundAlly != null)
                        {
                            var fa = foundAlly;
                            _extraAction = (_, c) => c.ApplyStatusToAlly(fa, type, n);
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 좌표들에 있는 대상에게 광역 디버프.
        /// absolute=false(기본): 적 기준 상대 좌표. absolute=true: 절대 좌표.
        /// </summary>
        protected void PlanRangeDebuff(EnemyInstance self, BoardController board,
                                       Vector2Int[] offsets, StatusEffectType type, int stacks,
                                       bool absolute = false)
        {
            ClearExtraSlot();
            if (offsets == null || offsets.Length == 0) return;
            int n = Mathf.Max(1, stacks);
            _extraIntent = $"광역 디버프 {n}턴";
            var copy = (Vector2Int[])offsets.Clone();

            _extraAction = (s, c) =>
            {
                foreach (var o in copy)
                {
                    Vector2Int cell = absolute ? o : s.GridPos + o;
                    Vector2Int playerPos = c.PlayerSpawnCell;
                    if (cell == playerPos)
                        c.ApplyStatusToPlayer(type, n);
                    foreach (var ally in c.GetAllies())
                        if (ally.CurrentHP > 0 && ally.GridPos == cell)
                            c.ApplyStatusToAlly(ally, type, n);
                }
            };
        }

        /// <summary>
        /// 지정 대상에게 회피 불가 디버프. 위치와 무관하게 적용.
        /// target=Ally일 때 targetAlly를 지정하세요.
        /// </summary>
        protected void PlanUnavoidableDebuff(EnemyInstance self, BoardController board,
                                             AttackTarget target, StatusEffectType type, int stacks,
                                             AllyInstance targetAlly = null)
        {
            ClearExtraSlot();
            int n = Mathf.Max(1, stacks);
            _extraIntent = $"디버프 {n}턴";

            switch (target)
            {
                case AttackTarget.Player:
                    _extraAction = (_, c) => c.ApplyStatusToPlayer(type, n);
                    break;
                case AttackTarget.Ally:
                    if (targetAlly == null) return;
                    var a = targetAlly;
                    _extraAction = (_, c) => c.ApplyStatusToAlly(a, type, n);
                    break;
                default:
                    Vector2Int nearestPos = FindNearestTargetPos(self, board);
                    bool isPlayer = (nearestPos == board.PlayerSpawnCell);
                    if (isPlayer)
                    {
                        _extraAction = (_, c) => c.ApplyStatusToPlayer(type, n);
                    }
                    else
                    {
                        AllyInstance foundAlly = null;
                        foreach (var ally in board.GetAllies())
                            if (ally.CurrentHP > 0 && ally.GridPos == nearestPos) { foundAlly = ally; break; }
                        if (foundAlly != null)
                        {
                            var fa = foundAlly;
                            _extraAction = (_, c) => c.ApplyStatusToAlly(fa, type, n);
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 유닛을 소환합니다.
        /// absolute=false(기본): offsets를 자신 기준 상대좌표로 해석.
        /// absolute=true: offsets를 절대 좌표로 해석.
        /// 빈 셀만 소환되며, 점유된 셀은 무시됩니다.
        /// </summary>
        protected void PlanSummon(EnemyInstance self, BoardController board,
                                  int enemyId, Vector2Int[] offsets, bool absolute = false)
        {
            ClearExtraSlot();
            var copy = (Vector2Int[])offsets.Clone();
            _extraIntent = "소환";
            _extraAction = (s, c) =>
            {
                foreach (var o in copy)
                {
                    Vector2Int cell = absolute ? o : s.GridPos + o;
                    c.SummonEnemy(enemyId, cell);
                }
            };
        }

        /// <summary>
        /// 자폭: 자신을 제거하면서 효과를 발동합니다.
        /// onExplode 콜백으로 자폭 시 발동할 효과를 자유롭게 지정하세요.
        /// onExplode가 null이면 단순 자폭(효과 없이 제거).
        ///
        /// 실행 순서: ① 자기 footprint 비우기 + GameObject 제거 (승리 판정은 보류)
        ///           ② onExplode 실행 (비워진 자리에 소환 가능)
        ///           ③ 승리 판정
        /// — 순서를 분리하지 않으면 onExplode 안에서 같은 자리에 소환할 때 셀이 점유 상태라
        ///   소환이 실패하고, 곧장 RemoveEnemyFromCombat의 CheckVictory가 승리를 트리거함.
        /// </summary>
        protected void PlanSelfDestruct(EnemyInstance self, BoardController board,
                                        Action<EnemyInstance, BoardController> onExplode = null)
        {
            ClearExtraSlot();
            ClearAttackSlot();
            ClearMoveSlot();
            _extraIntent = "자폭";
            _extraAction = (s, c) =>
            {
                c.RemoveEnemyFromCombat(s, checkVictory: false);
                onExplode?.Invoke(s, c);
                c.TriggerVictoryCheck();
            };
        }

        /// <summary>완전 자유 액션 (소환/특수효과 등).</summary>
        protected void PlanCustom(string intentText, Action<EnemyInstance, BoardController> action)
        {
            ClearExtraSlot();
            _extraIntent = intentText ?? "행동";
            _extraAction = action;
        }

        // ──────────────────────────────────────────────────────────
        // 전체 클리어
        // ──────────────────────────────────────────────────────────

        /// <summary>이번 턴 모든 슬롯을 비워 아무것도 하지 않음.</summary>
        protected void PlanWait(string intentText = "대기")
        {
            ClearAllSlots();
            _extraIntent = intentText;
        }

        // ──────────────────────────────────────────────────────────
        // 내부 슬롯 클리어
        // ──────────────────────────────────────────────────────────

        private void ClearAttackSlot()
        {
            _attackOffsets.Clear();
            _attackAbsoluteCells.Clear();
            _attackAction = null;
            _attackIntent = null;
        }

        private void ClearMoveSlot()
        {
            plannedMovePositions.Clear();
            plannedMoveSpritePositions.Clear();
            _moveIntent = null;
            _deferredNextStep = null;
            _deferredSortAnchor = null;
            _deferredMoveSteps = 0;
        }

        private void ClearExtraSlot()
        {
            _extraAction = null;
            _extraIntent = null;
        }

        private void ClearAllSlots()
        {
            ClearAttackSlot();
            ClearMoveSlot();
            ClearExtraSlot();
            plannedSkillPositions.Clear();
            plannedAttackPositions.Clear();
        }

        // ──────────────────────────────────────────────────────────
        // 경로/셀 검사 유틸 (서브클래스에서 자작 헬퍼 만들 때 활용)
        // ──────────────────────────────────────────────────────────

        /// <summary>from에서 target 방향으로 한 칸. 막히면 4방향 중 가장 가까워지는 칸.</summary>
        protected Vector2Int CalcOneStepToward(Vector2Int from, Vector2Int target,
                                               BoardController board, HashSet<Vector2Int> reserved,
                                               HashSet<Vector2Int> vacating = null)
        {
            Vector2Int diff = target - from;
            if (diff == Vector2Int.zero) return from;

            Vector2Int primary = Mathf.Abs(diff.x) >= Mathf.Abs(diff.y)
                ? new Vector2Int((int)Mathf.Sign(diff.x), 0)
                : new Vector2Int(0, (int)Mathf.Sign(diff.y));
            Vector2Int secondary = Mathf.Abs(diff.x) >= Mathf.Abs(diff.y)
                ? new Vector2Int(0, (int)Mathf.Sign(diff.y))
                : new Vector2Int((int)Mathf.Sign(diff.x), 0);

            Vector2Int candidate = from + primary;
            if (primary != Vector2Int.zero && CanMoveInto(candidate, board, reserved, vacating))
                return candidate;
            candidate = from + secondary;
            if (secondary != Vector2Int.zero && CanMoveInto(candidate, board, reserved, vacating))
                return candidate;

            int currentDist = Mathf.Abs(from.x - target.x) + Mathf.Abs(from.y - target.y);
            Vector2Int best = from;
            int bestDist = currentDist;
            foreach (var dir in AdjacentFour)
            {
                Vector2Int step = from + dir;
                if (!CanMoveInto(step, board, reserved, vacating)) continue;
                int d = Mathf.Abs(step.x - target.x) + Mathf.Abs(step.y - target.y);
                if (d < bestDist) { bestDist = d; best = step; }
            }
            return best;
        }

        /// <summary>from에서 playerPos로부터 멀어지는 방향으로 한 칸.</summary>
        protected Vector2Int CalcOneStepAway(Vector2Int from, Vector2Int playerPos,
                                              BoardController board, HashSet<Vector2Int> reserved,
                                              HashSet<Vector2Int> vacating = null)
        {
            int currentDist = Mathf.Abs(from.x - playerPos.x) + Mathf.Abs(from.y - playerPos.y);
            Vector2Int best = from;
            int bestDist = currentDist;
            foreach (var dir in AdjacentFour)
            {
                Vector2Int step = from + dir;
                if (!CanMoveInto(step, board, reserved, vacating)) continue;
                int d = Mathf.Abs(step.x - playerPos.x) + Mathf.Abs(step.y - playerPos.y);
                if (d > bestDist) { bestDist = d; best = step; }
            }
            return best;
        }

        /// <summary>해당 셀이 이동 가능한지 (보드 안 + 비점유 + 미예약).
        /// vacating에 포함된 칸은 다른 적이 떠날 예정이므로 점유 검사를 건너뜁니다.</summary>
        protected bool CanMoveInto(Vector2Int pos, BoardController board, HashSet<Vector2Int> reserved,
                                   HashSet<Vector2Int> vacating = null)
        {
            if (!board.IsInBoard(pos)) return false;
            bool willBeVacated = vacating != null && vacating.Contains(pos);
            if (!willBeVacated && !board.IsCellFreeForEnemy(pos)) return false;
            if (reserved != null && reserved.Contains(pos)) return false;
            return true;
        }

        /// <summary>이번 턴에 다른 적들이 이미 예약한 이동 destination footprint 모음.
        /// 멀티셀 적이면 새 anchor 기준 footprint 전체 셀이 reserved에 들어간다 — 후순위 적이 같은 칸으로
        /// 동시에 이동하는 것을 막아 player를 둘러싸는 배치가 겹치지 않도록 한다.</summary>
        protected HashSet<Vector2Int> GatherReservedCells(EnemyInstance self, BoardController board)
        {
            var reserved = new HashSet<Vector2Int>();
            foreach (var other in board.GetAliveEnemies())
            {
                if (other == self) continue;
                if (other.Behavior == null) continue;
                var moves = other.Behavior.GetMovePreviewPositions(other);
                if (moves == null) continue;
                Vector2Int sz = other.Size;
                foreach (var anchor in moves)
                {
                    if (sz.x <= 1 && sz.y <= 1) { reserved.Add(anchor); continue; }
                    for (int dx = 0; dx < sz.x; dx++)
                    for (int dy = 0; dy < sz.y; dy++)
                        reserved.Add(new Vector2Int(anchor.x + dx, anchor.y - dy));
                }
            }
            return reserved;
        }

        /// <summary>이번 턴에 다른 적들이 떠날 예정인(비워질) 현재 footprint 셀 모음.
        /// 먼저 이동을 계획한 적의 현재 footprint를 후순위 적이 점유 가능한 후보로 사용해
        /// player 둘러싸기에 적의 빈자리 연쇄까지 활용한다.
        /// 멀티셀이 짧게 이동하면 old/new footprint가 겹칠 수 있으므로 실제로 비워지는 셀
        /// (old footprint − new footprint)만 vacating에 포함.</summary>
        protected HashSet<Vector2Int> GatherVacatingCells(EnemyInstance self, BoardController board)
        {
            var vacating = new HashSet<Vector2Int>();
            foreach (var other in board.GetAliveEnemies())
            {
                if (other == self) continue;
                if (other.Behavior == null) continue;
                var moves = other.Behavior.GetMovePreviewPositions(other);
                if (moves == null || moves.Count == 0) continue;
                Vector2Int newAnchor = moves[moves.Count - 1];
                if (newAnchor == other.GridPos) continue; // 실제로는 이동 안 함

                Vector2Int sz = other.Size;
                // new footprint 집합 — 1x1이면 anchor 1개.
                var newFootprint = new HashSet<Vector2Int>();
                for (int dx = 0; dx < sz.x; dx++)
                for (int dy = 0; dy < sz.y; dy++)
                    newFootprint.Add(new Vector2Int(newAnchor.x + dx, newAnchor.y - dy));

                // old footprint 중 new footprint에 없는 셀만 vacating.
                foreach (var c in other.OccupiedCells)
                    if (!newFootprint.Contains(c)) vacating.Add(c);
            }
            return vacating;
        }

        /// <summary>주어진 후보 좌표 중 보드 안 + 비점유 + 미예약인 셀들만 필터.</summary>
        protected List<Vector2Int> FilterFreeCells(IEnumerable<Vector2Int> candidates,
                                                   EnemyInstance self, BoardController board)
        {
            var reserved = GatherReservedCells(self, board);
            var vacating = GatherVacatingCells(self, board);
            var result = new List<Vector2Int>();
            if (candidates == null) return result;
            foreach (var p in candidates)
                if (CanMoveInto(p, board, reserved, vacating)) result.Add(p);
            return result;
        }

        // ──────────────────────────────────────────────────────────
        // Phase 2 — 지연 이동 실행 (BoardController가 정렬된 순서로 호출)
        // ──────────────────────────────────────────────────────────

        /// <summary>BoardController가 정렬에 사용하는 좌표 (각 PlanMove* 등록 시 지정한 sort anchor).</summary>
        public Vector2Int GetDeferredSortAnchor(EnemyInstance self, BoardController board)
            => _deferredSortAnchor != null ? _deferredSortAnchor(self, board) : board.PlayerSpawnCell;

        /// <summary>Phase 2 — 등록된 next-step delegate를 호출해 한 step 실행.
        /// delegate가 self.GridPos를 반환하거나 TryMoveEnemy가 실패하면 false. 성공 시 step 감소.</summary>
        public virtual bool ExecuteDeferredMoveStep(EnemyInstance self, BoardController board)
        {
            if (!HasDeferredMove) return false;
            Vector2Int next = _deferredNextStep(self, board);
            if (next == self.GridPos) return false;
            if (!board.IsInBoard(next)) return false;
            bool moved = board.TryMoveEnemy(self, next);
            if (moved) _deferredMoveSteps--;
            return moved;
        }

        /// <summary>BFS 최단경로 첫 step 계산. target 칸과 self 시작 칸은 통과 가능 처리.
        /// 멀티셀 적은 footprint 전체를 한 단위로 보고 anchor 칸을 이동시키며,
        ///   - BFS 경로 탐색: footprint가 target 셀과 겹쳐도 통과 허용(경로 계산용).
        ///   - 후보 선택: 실제로 안착 가능(IsFootprintFreeForEnemy)한 anchor만 후보.
        ///   - BFS가 self anchor에 닿지 못하면 greedy 폴백: footprint→target Manhattan을 가장 줄이는
        ///     인접 anchor 1칸을 선택. (대각선·코너 같은 막힘 상황에서 정지 버그 회피)
        /// 동거리 후보 여러 개면:
        ///   1차 tiebreaker — target에 대한 footprint Manhattan 거리가 작을수록 우선
        ///   2차 tiebreaker — 다른 적과의 min Chebyshev 거리가 클수록 우선 (둘러싸기 spread)
        /// 이동 불가면 self.GridPos 반환.</summary>
        protected Vector2Int ComputeBfsStepToward(EnemyInstance self, BoardController board, Vector2Int target)
        {
            if (!board.IsInBoard(target)) return self.GridPos;

            Vector2Int size = self.Size;
            bool isMultiCell = size.x > 1 || size.y > 1;

            // 다른 적의 예정 이동을 반영 — vacating(곧 비워질 칸)은 통과 허용,
            // reserved(다른 적이 안착 예정)는 후보에서 제외해 둘러싸기에서 같은 칸 충돌을 막는다.
            var vacating = GatherVacatingCells(self, board);
            var reserved = GatherReservedCells(self, board);

            var dist = new Dictionary<Vector2Int, int>();
            var queue = new Queue<Vector2Int>();
            dist[target] = 0;
            queue.Enqueue(target);

            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                int cd = dist[cur];
                foreach (var dir in AdjacentFour)
                {
                    Vector2Int nxt = cur + dir;
                    if (!board.IsInBoard(nxt)) continue;
                    if (dist.ContainsKey(nxt)) continue;
                    bool passable;
                    if (isMultiCell)
                    {
                        // 멀티셀: 앵커 nxt에서 footprint가 보드 안 + (target/자기 자신/vacating 제외) 빈 칸이면 통과.
                        passable = (nxt == self.GridPos)
                                || IsMultiCellAnchorPassable(board, nxt, size, self, target, vacating);
                    }
                    else
                    {
                        passable = (nxt == self.GridPos) || (nxt == target)
                                || vacating.Contains(nxt)
                                || board.IsCellFreeForEnemy(nxt);
                    }
                    if (!passable) continue;
                    dist[nxt] = cd + 1;
                    queue.Enqueue(nxt);
                }
            }

            // BFS가 self anchor에 도달하지 못했어도(예: 멀티셀이 코너/대각 위치에 끼인 경우),
            // 멀티셀은 greedy 폴백으로 footprint→target 거리를 줄일 수 있는 인접 anchor를 시도.
            bool reached = dist.TryGetValue(self.GridPos, out int curDist);
            if (!reached || curDist <= 1)
            {
                if (isMultiCell)
                    return GreedyMultiCellStepToward(self, board, target, vacating, reserved);
                return self.GridPos; // 도달 불가 또는 이미 인접 (1x1)
            }

            var candidates = new List<Vector2Int>();
            foreach (var dir in AdjacentFour)
            {
                Vector2Int nxt = self.GridPos + dir;
                if (!board.IsInBoard(nxt)) continue;
                if (nxt == target) continue;
                if (!dist.TryGetValue(nxt, out int nd) || nd != curDist - 1) continue;
                // 다른 적의 예정 도착지(reserved)와 충돌하지 않는 후보만.
                if (FootprintOverlapsReserved(nxt, size, reserved)) continue;
                if (isMultiCell)
                {
                    if (!IsFootprintFreeOrVacating(board, nxt, size, self, vacating)) continue;
                }
                else
                {
                    if (!board.IsCellFreeForEnemy(nxt) && !vacating.Contains(nxt)) continue;
                }
                candidates.Add(nxt);
            }

            // BFS 후보가 비면 멀티셀은 greedy 폴백 한 번 더 시도.
            if (candidates.Count == 0)
            {
                if (isMultiCell)
                    return GreedyMultiCellStepToward(self, board, target, vacating, reserved);
                return self.GridPos;
            }
            if (candidates.Count == 1) return candidates[0];

            Vector2Int best = candidates[0];
            int bestManhattan = isMultiCell
                ? FootprintMinManhattan(best, size, target)
                : ManhattanDist(best, target);
            int bestCornerScore = CountTargetEscapesCovered(best, target, board);
            int bestSpread = MinChebyshevToOtherEnemies(best, self, board);
            for (int i = 1; i < candidates.Count; i++)
            {
                int m = isMultiCell
                    ? FootprintMinManhattan(candidates[i], size, target)
                    : ManhattanDist(candidates[i], target);
                int cs = CountTargetEscapesCovered(candidates[i], target, board);
                int sp = MinChebyshevToOtherEnemies(candidates[i], self, board);
                // 1차: Manhattan 작을수록
                // 2차: corner score 클수록 — target의 인접 탈출 셀을 멜레 공격범위(Manhattan 1)로
                //      더 많이 덮는 후보 선호. 플레이어를 구석으로 모는 방향이 자연히 우선됨.
                // 3차: spread 클수록 — 여러 적이 같은 escape 셀로 몰려 동일 후보를 차지하는 충돌 완화.
                if (m < bestManhattan ||
                   (m == bestManhattan && cs > bestCornerScore) ||
                   (m == bestManhattan && cs == bestCornerScore && sp > bestSpread))
                {
                    bestManhattan = m;
                    bestCornerScore = cs;
                    bestSpread = sp;
                    best = candidates[i];
                }
            }
            return best;
        }

        /// <summary>후보 위치에 enemy가 안착한다고 가정할 때, target의 4방향 인접 탈출 셀 중
        /// 후보 위치에서 멜레 공격범위(Manhattan ≤ 1)로 덮는 셀 개수를 반환.
        /// 플레이어를 구석으로 모는 방향(=탈출구 차단) 선호용 tiebreaker 점수.
        /// 멜레 가정이라 원거리 적에게는 의미가 약하지만 단순 휴리스틱으로 충분.</summary>
        protected static int CountTargetEscapesCovered(Vector2Int candidate, Vector2Int target, BoardController board)
        {
            int count = 0;
            foreach (var dir in AdjacentFour)
            {
                Vector2Int escape = target + dir;
                if (!board.IsInBoard(escape)) continue;
                if (ManhattanDist(candidate, escape) <= 1) count++;
            }
            return count;
        }

        /// <summary>멀티셀 greedy 1-step — AdjacentFour anchor 후보 중 footprint→target Manhattan을
        /// 가장 줄이는 위치를 선택. 같으면 spread tiebreaker. 모든 후보가 현재보다 나쁘면 self.GridPos.
        /// BFS가 target과 footprint 겹침으로 self에 닿지 못한 경우를 처리하기 위한 폴백.
        /// vacating/reserved를 반영해 다른 적의 이동으로 비워질 칸은 통과, 안착 예정 칸은 회피.</summary>
        private Vector2Int GreedyMultiCellStepToward(EnemyInstance self, BoardController board, Vector2Int target,
                                                     HashSet<Vector2Int> vacating, HashSet<Vector2Int> reserved)
        {
            Vector2Int size = self.Size;
            int curDist = FootprintMinManhattan(self.GridPos, size, target);
            Vector2Int best = self.GridPos;
            int bestDist = curDist;
            int bestCornerScore = CountTargetEscapesCovered(self.GridPos, target, board);
            int bestSpread = MinChebyshevToOtherEnemies(self.GridPos, self, board);

            foreach (var dir in AdjacentFour)
            {
                Vector2Int nxt = self.GridPos + dir;
                if (!board.IsInBoard(nxt)) continue;
                if (FootprintOverlapsReserved(nxt, size, reserved)) continue;
                if (!IsFootprintFreeOrVacating(board, nxt, size, self, vacating)) continue;
                int d = FootprintMinManhattan(nxt, size, target);
                int cs = CountTargetEscapesCovered(nxt, target, board);
                int sp = MinChebyshevToOtherEnemies(nxt, self, board);
                // 1차: footprint→target Manhattan, 2차: corner score, 3차: spread.
                if (d < bestDist
                    || (d == bestDist && cs > bestCornerScore)
                    || (d == bestDist && cs == bestCornerScore && sp > bestSpread))
                {
                    bestDist = d;
                    bestCornerScore = cs;
                    bestSpread = sp;
                    best = nxt;
                }
            }
            return best;
        }

        /// <summary>멀티셀 anchor가 BFS 경로상 통과 가능한지 — footprint가 보드 안이며
        /// target/self-occupied/vacating 칸을 제외하고 모두 비어있어야 한다.</summary>
        private static bool IsMultiCellAnchorPassable(BoardController board, Vector2Int anchor, Vector2Int size,
                                                       EnemyInstance self, Vector2Int target,
                                                       HashSet<Vector2Int> vacating)
        {
            for (int dx = 0; dx < size.x; dx++)
            for (int dy = 0; dy < size.y; dy++)
            {
                Vector2Int p = new Vector2Int(anchor.x + dx, anchor.y - dy);
                if (!board.IsInBoard(p)) return false;
                if (p == target) continue;
                if (vacating != null && vacating.Contains(p)) continue;
                var occ = board.GetEnemyAt(p);
                if (occ == self) continue;
                if (!board.IsCellFreeForEnemy(p)) return false;
            }
            return true;
        }

        /// <summary>멀티셀 anchor의 footprint가 실제로 안착 가능한지 — vacating 칸은 허용.
        /// (target은 허용하지 않음 — 안착 시 target 위에 올라탈 수는 없다.)</summary>
        private static bool IsFootprintFreeOrVacating(BoardController board, Vector2Int anchor, Vector2Int size,
                                                       EnemyInstance self, HashSet<Vector2Int> vacating)
        {
            for (int dx = 0; dx < size.x; dx++)
            for (int dy = 0; dy < size.y; dy++)
            {
                Vector2Int p = new Vector2Int(anchor.x + dx, anchor.y - dy);
                if (!board.IsInBoard(p)) return false;
                if (vacating != null && vacating.Contains(p)) continue;
                var occ = board.GetEnemyAt(p);
                if (occ == self) continue;
                if (!board.IsCellFreeForEnemy(p)) return false;
            }
            return true;
        }

        /// <summary>이동 후보 footprint가 다른 적의 예정 도착지(reserved)와 겹치는지.</summary>
        private static bool FootprintOverlapsReserved(Vector2Int anchor, Vector2Int size,
                                                       HashSet<Vector2Int> reserved)
        {
            if (reserved == null || reserved.Count == 0) return false;
            for (int dx = 0; dx < size.x; dx++)
            for (int dy = 0; dy < size.y; dy++)
            {
                if (reserved.Contains(new Vector2Int(anchor.x + dx, anchor.y - dy))) return true;
            }
            return false;
        }

        /// <summary>(anchor, size) footprint에서 target까지의 최소 Manhattan 거리.
        /// footprint는 사각형 점 집합이므로 점→사각형 axis-aligned 거리 공식으로 O(1) 계산.</summary>
        private static int FootprintMinManhattan(Vector2Int anchor, Vector2Int size, Vector2Int target)
        {
            int xMin = anchor.x;
            int xMax = anchor.x + size.x - 1;
            int yMin = anchor.y - size.y + 1;
            int yMax = anchor.y;
            int dx = target.x < xMin ? xMin - target.x
                   : target.x > xMax ? target.x - xMax : 0;
            int dy = target.y < yMin ? yMin - target.y
                   : target.y > yMax ? target.y - yMax : 0;
            return dx + dy;
        }

        private static int ManhattanDist(Vector2Int a, Vector2Int b)
            => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        private int MinChebyshevToOtherEnemies(Vector2Int pos, EnemyInstance self, BoardController board)
        {
            int min = int.MaxValue;
            foreach (var other in board.GetAliveEnemies())
            {
                if (other == self) continue;
                int d = Mathf.Max(Mathf.Abs(pos.x - other.GridPos.x), Mathf.Abs(pos.y - other.GridPos.y));
                if (d < min) min = d;
            }
            return min == int.MaxValue ? 999 : min;
        }
    }
}
