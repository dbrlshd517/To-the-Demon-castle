using System;
using System.Collections.Generic;
using UnityEngine;
using DeckRoguelike.UI;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 아군 유닛 행동의 추상 기반 클래스 + 헬퍼 라이브러리.
    /// EnemyBehavior와 동일한 구조 — 단, 타겟이 플레이어가 아닌 "적(Enemy)"입니다.
    ///
    /// [작성 방식]
    ///   1. 이 클래스를 상속받아 PlanTurn만 오버라이드.
    ///   2. PlanTurn 안에서 Plan*** 헬퍼를 호출하면 ExecuteTurn이 자동 처리.
    ///   3. 한 턴에 공격/이동/기타(블록·버프·소환 등)를 모두 등록 가능.
    ///   4. AllyBehaviorRegistry.Register(allyCode, () => new MyBehavior())로 등록.
    ///
    /// [슬롯 구조]
    ///   ① 공격 슬롯 : PlanTargetAttack / PlanRangeAttack / PlanUnavoidableAttack
    ///   ② 이동 슬롯 : PlanMoveTowardEnemy / PlanMoveAwayFromEnemy / PlanMoveTo / PlanMoveRandomNearEnemy / PlanMoveRandomToOffset
    ///   ③ 기타 슬롯 : PlanSetBlock / PlanApplyStatusToSelf / PlanTargetDebuff / PlanRangeDebuff / PlanUnavoidableDebuff
    ///                  / PlanSummon / PlanSelfDestruct / PlanCustom
    ///   같은 슬롯은 마지막 호출만 살아남음. 슬롯이 다르면 공존. PlanWait는 전 슬롯을 비움.
    ///   ExecuteTurn 실행 순서: 기타 → 공격 → 이동
    /// </summary>
    public abstract class AllyBehavior
    {
        // ── 자주 쓰는 좌표 패턴 ─────────────────────────────────────
        public static readonly Vector2Int[] AdjacentFour =
        {
            new Vector2Int( 1,  0), new Vector2Int(-1,  0),
            new Vector2Int( 0,  1), new Vector2Int( 0, -1),
        };
        public static readonly Vector2Int[] DiagonalFour =
        {
            new Vector2Int( 1,  1), new Vector2Int(-1, -1),
            new Vector2Int( 1, -1), new Vector2Int(-1,  1),
        };
        public static readonly Vector2Int[] AdjacentEight =
        {
            new Vector2Int( 1,  0), new Vector2Int(-1,  0),
            new Vector2Int( 0,  1), new Vector2Int( 0, -1),
            new Vector2Int( 1,  1), new Vector2Int(-1, -1),
            new Vector2Int( 1, -1), new Vector2Int(-1,  1),
        };
        public static readonly Vector2Int[] Cross2 =
        {
            new Vector2Int( 1,  0), new Vector2Int( 2,  0),
            new Vector2Int(-1,  0), new Vector2Int(-2,  0),
            new Vector2Int( 0,  1), new Vector2Int( 0,  2),
            new Vector2Int( 0, -1), new Vector2Int( 0, -2),
        };
        public static readonly Vector2Int[] KnightJump =
        {
            new Vector2Int( 2,  1), new Vector2Int( 1,  2),
            new Vector2Int(-1,  2), new Vector2Int(-2,  1),
            new Vector2Int( 2, -1), new Vector2Int( 1, -2),
            new Vector2Int(-1, -2), new Vector2Int(-2, -1),
        };

        // ── 미리보기 좌표 ─────────────────────────────────────
        protected List<Vector2Int> plannedAttackPositions = new List<Vector2Int>();
        protected List<Vector2Int> plannedSkillPositions  = new List<Vector2Int>();
        protected List<Vector2Int> plannedMovePositions   = new List<Vector2Int>();

        // ── 슬롯 상태 ─────────────────────────────────────
        private readonly List<Vector2Int> _attackOffsets       = new List<Vector2Int>();
        private readonly List<Vector2Int> _attackAbsoluteCells = new List<Vector2Int>();
        private Action<AllyInstance, BoardController> _attackAction;
        private string _attackIntent;

        private Action<AllyInstance, BoardController> _moveAction;
        private string _moveIntent;

        private Action<AllyInstance, BoardController> _extraAction;
        private string _extraIntent;

        private int _seqCounter;

        // ── 주 타겟 선택 상태 (이동·공격이 공유하는 "가장 가까운 적") ──────────────
        // EnemyBehavior와 동형 — 최근접 후보 집합이 그대로면 같은 적 유지, 집합이 바뀌거나 더 가까운
        // 적이 생기면 후보 중 랜덤 재선택. 턴 종료(ExecuteTurn 끝)에서 초기화.
        private const int NoTargetId = int.MinValue;
        private readonly HashSet<int> _lastNearestIds = new HashSet<int>();
        private int _currentTargetId = NoTargetId;

        // ── 표준 hooks ────────────────────────────────────────────
        public virtual void OnSpawn(AllyInstance self, BoardController board) { }

        public virtual void PlanTurn(AllyInstance self, BoardController board) { }

        public virtual void ExecuteTurn(AllyInstance self, BoardController board)
        {
            _extraAction?.Invoke(self, board);
            _attackAction?.Invoke(self, board);
            _moveAction?.Invoke(self, board);
            ClearAllSlots();
            // 주 타겟 초기화 — 다음 턴에 최근접 적을 새로 선택.
            _lastNearestIds.Clear();
            _currentTargetId = NoTargetId;
        }

        public virtual void OnDeath(AllyInstance self, BoardController board) { }

        public virtual string GetIntentText(AllyInstance self)
        {
            var parts = new List<string>(3);
            if (!string.IsNullOrEmpty(_extraIntent))  parts.Add(_extraIntent);
            if (!string.IsNullOrEmpty(_attackIntent)) parts.Add(_attackIntent);
            if (!string.IsNullOrEmpty(_moveIntent))   parts.Add(_moveIntent);
            return parts.Count == 0 ? "대기" : string.Join(" + ", parts);
        }

        public virtual List<Vector2Int> GetAttackPreviewPositions(AllyInstance self)
        {
            plannedAttackPositions.Clear();
            foreach (var off in _attackOffsets)       plannedAttackPositions.Add(self.GridPos + off);
            foreach (var pos in _attackAbsoluteCells) plannedAttackPositions.Add(pos);
            return plannedAttackPositions;
        }

        public virtual List<Vector2Int> GetSkillPreviewPositions(AllyInstance self) => plannedSkillPositions;
        public virtual List<Vector2Int> GetMovePreviewPositions(AllyInstance self)  => plannedMovePositions;

        // ──────────────────────────────────────────────────────────
        // 조건 검사 헬퍼
        // ──────────────────────────────────────────────────────────

        /// <summary>self.GridPos + offsets[i] 중 적이 한 명이라도 있으면 true.</summary>
        protected bool IsEnemyInRange(AllyInstance self, BoardController board, Vector2Int[] offsets)
        {
            if (offsets == null) return false;
            foreach (var o in offsets)
            {
                Vector2Int cell = self.GridPos + o;
                foreach (var e in board.GetAliveEnemies())
                    if (e.GridPos == cell) return true;
            }
            return false;
        }

        /// <summary>주 타겟(가장 가까운 적) 위치. 적이 없으면 self.GridPos.</summary>
        protected Vector2Int FindNearestEnemyPos(AllyInstance self, BoardController board)
        {
            var e = PrimaryEnemyTarget(self, board);
            return e != null ? e.GridPos : self.GridPos;
        }

        /// <summary>주 타겟(가장 가까운 적) 인스턴스. 없으면 null.</summary>
        protected EnemyInstance FindNearestEnemy(AllyInstance self, BoardController board)
            => PrimaryEnemyTarget(self, board);

        /// <summary>
        /// 이동·공격이 공유하는 "주 타겟" 적. 자신에게서 가장 가까운 적을 고른다. 같은 최소 거리가
        /// 여럿이면 랜덤. 재계산 시 최근접 후보 집합이 그대로면 같은 적을 유지하고, 집합이 바뀌거나
        /// 더 가까운 적이 생기면 후보 중 랜덤으로 다시 선택. 적이 없으면 null.
        /// </summary>
        protected EnemyInstance PrimaryEnemyTarget(AllyInstance self, BoardController board)
        {
            // 1) 최소 거리.
            int minDist = int.MaxValue;
            foreach (var e in board.GetAliveEnemies())
            {
                int d = Mathf.Abs(self.GridPos.x - e.GridPos.x) + Mathf.Abs(self.GridPos.y - e.GridPos.y);
                if (d < minDist) minDist = d;
            }
            if (minDist == int.MaxValue)
            {
                _lastNearestIds.Clear();
                _currentTargetId = NoTargetId;
                return null;
            }

            // 2) 최소 거리 후보 수집.
            var ids = new List<int>();
            var objs = new List<EnemyInstance>();
            foreach (var e in board.GetAliveEnemies())
            {
                int d = Mathf.Abs(self.GridPos.x - e.GridPos.x) + Mathf.Abs(self.GridPos.y - e.GridPos.y);
                if (d != minDist) continue;
                // EnemyInstance는 UnityEngine.Object가 아닌 일반 클래스 → 참조 기반 GetHashCode()로 식별.
                ids.Add(e.GetHashCode());
                objs.Add(e);
            }

            // 3) 집합 그대로면 유지, 아니면 랜덤 재선택.
            var nearestIds = new HashSet<int>(ids);
            int chosen;
            if (nearestIds.SetEquals(_lastNearestIds) && nearestIds.Contains(_currentTargetId))
                chosen = ids.IndexOf(_currentTargetId);
            else
                chosen = UnityEngine.Random.Range(0, ids.Count);

            _lastNearestIds.Clear();
            foreach (var id in ids) _lastNearestIds.Add(id);
            _currentTargetId = ids[chosen];
            return objs[chosen];
        }

        protected int ManhattanToEnemy(AllyInstance self, BoardController board)
        {
            int best = int.MaxValue;
            foreach (var e in board.GetAliveEnemies())
            {
                int d = Mathf.Abs(self.GridPos.x - e.GridPos.x) + Mathf.Abs(self.GridPos.y - e.GridPos.y);
                if (d < best) best = d;
            }
            return best == int.MaxValue ? 0 : best;
        }

        protected int ChebyshevToEnemy(AllyInstance self, BoardController board)
        {
            int best = int.MaxValue;
            foreach (var e in board.GetAliveEnemies())
            {
                int d = Mathf.Max(Mathf.Abs(self.GridPos.x - e.GridPos.x), Mathf.Abs(self.GridPos.y - e.GridPos.y));
                if (d < best) best = d;
            }
            return best == int.MaxValue ? 0 : best;
        }

        protected bool IsHpBelow(AllyInstance self, float ratio)
            => self != null && self.MaxHP > 0 && self.CurrentHP <= self.MaxHP * ratio;

        protected bool IsHpAbove(AllyInstance self, float ratio)
            => self != null && self.MaxHP > 0 && self.CurrentHP > self.MaxHP * ratio;

        // ──────────────────────────────────────────────────────────
        // 선택 패턴 헬퍼
        // ──────────────────────────────────────────────────────────

        protected int NextSequential(int modulo)
        {
            if (modulo <= 0) return 0;
            int v = _seqCounter % modulo;
            _seqCounter++;
            return v;
        }

        protected int RollDice(int sides) => UnityEngine.Random.Range(0, Mathf.Max(1, sides));
        protected bool RollChance(int percent) => UnityEngine.Random.Range(0, 100) < percent;

        protected T PickRandom<T>(params T[] options)
        {
            if (options == null || options.Length == 0) return default;
            return options[UnityEngine.Random.Range(0, options.Length)];
        }

        // ──────────────────────────────────────────────────────────
        // 공격 슬롯 헬퍼
        // ──────────────────────────────────────────────────────────

        public enum AttackTarget { Nearest, Specific }

        /// <summary>가장 가까운 적을 직접 공격 (사거리 체크 없음).</summary>
        protected void PlanTargetAttack(AllyInstance self, BoardController board, int extraDamage = 0)
        {
            ClearAttackSlot();
            int dmg = Mathf.Max(0, self.Damage + extraDamage);
            _attackIntent = $"공격 {dmg}";

            Vector2Int targetPos = FindNearestEnemyPos(self, board);
            Vector2Int offset = targetPos - self.GridPos;
            _attackOffsets.Add(offset);

            _attackAction = (s, c) =>
            {
                Vector2Int cell = s.GridPos + offset;
                var enemy = c.GetEnemyAt(cell);
                if (enemy != null) c.DamageEnemy(enemy, dmg);
            };
        }

        /// <summary>특정 적 인스턴스를 추적 공격 (사거리 무시).</summary>
        protected void PlanTargetAttackSpecific(AllyInstance self, BoardController board,
                                                EnemyInstance target, int extraDamage = 0)
        {
            ClearAttackSlot();
            if (target == null) return;
            int dmg = Mathf.Max(0, self.Damage + extraDamage);
            _attackIntent = $"공격 {dmg}";
            _attackAbsoluteCells.Add(target.GridPos);
            var t = target;
            _attackAction = (s, c) =>
            {
                if (t.CurrentHP > 0) c.DamageEnemy(t, dmg);
            };
        }

        /// <summary>
        /// 좌표들에 광역 공격. 적이 있는 셀에만 데미지.
        /// absolute=false(기본): 자신 기준 상대 좌표. absolute=true: 절대 좌표.
        /// </summary>
        protected void PlanRangeAttack(AllyInstance self, BoardController board,
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
                _attackAction = (s, c) => c.DealDamageAtCells(copy, dmg);
            }
            else
            {
                foreach (var o in copy) _attackOffsets.Add(o);
                _attackAction = (s, c) =>
                {
                    var cells = new List<Vector2Int>(copy.Length);
                    foreach (var o in copy) cells.Add(s.GridPos + o);
                    c.DealDamageAtCells(cells, dmg);
                };
            }
        }

        /// <summary>가장 가까운 적에게 회피 불가 직접 공격 (위치 무관).</summary>
        protected void PlanUnavoidableAttack(AllyInstance self, BoardController board, int extraDamage = 0)
        {
            ClearAttackSlot();
            int dmg = Mathf.Max(0, self.Damage + extraDamage);
            _attackIntent = $"공격 {dmg}";

            var target = FindNearestEnemy(self, board);
            if (target == null) return;
            _attackAbsoluteCells.Add(target.GridPos);
            var t = target;
            _attackAction = (s, c) =>
            {
                if (t.CurrentHP > 0) c.DamageEnemy(t, dmg);
            };
        }

        // ──────────────────────────────────────────────────────────
        // 이동 슬롯 헬퍼
        // ──────────────────────────────────────────────────────────

        protected void PlanMoveTowardEnemy(AllyInstance self, BoardController board, int steps = 1)
        {
            Vector2Int nearest = FindNearestEnemyPos(self, board);
            PlanMoveTowardTarget(self, board, nearest, steps);
        }

        private void PlanMoveTowardTarget(AllyInstance self, BoardController board,
                                          Vector2Int targetPos, int steps)
        {
            ClearMoveSlot();
            steps = Mathf.Max(1, steps);
            HashSet<Vector2Int> reserved = GatherReservedCells(self, board);

            Vector2Int pos = self.GridPos;
            var path = new List<Vector2Int>();
            for (int i = 0; i < steps; i++)
            {
                Vector2Int next = CalcOneStepToward(pos, targetPos, board, reserved);
                if (next == pos) break;
                if (next == targetPos) break;
                path.Add(next);
                reserved.Add(next);
                pos = next;
            }
            if (path.Count == 0) return;

            plannedMovePositions.Add(path[path.Count - 1]);
            _moveIntent = path.Count > 1 ? $"이동 ×{path.Count}" : "이동";

            var pathCopy = path.ToArray();
            _moveAction = (s, c) =>
            {
                foreach (var step in pathCopy)
                    if (!c.TryMoveAlly(s, step)) break;
            };
        }

        protected void PlanMoveAwayFromEnemy(AllyInstance self, BoardController board, int steps = 1)
        {
            ClearMoveSlot();
            steps = Mathf.Max(1, steps);

            Vector2Int enemyPos = FindNearestEnemyPos(self, board);
            HashSet<Vector2Int> reserved = GatherReservedCells(self, board);

            Vector2Int pos = self.GridPos;
            var path = new List<Vector2Int>();
            for (int i = 0; i < steps; i++)
            {
                Vector2Int next = CalcOneStepAway(pos, enemyPos, board, reserved);
                if (next == pos) break;
                path.Add(next);
                reserved.Add(next);
                pos = next;
            }
            if (path.Count == 0) return;

            plannedMovePositions.Add(path[path.Count - 1]);
            _moveIntent = "후퇴";

            var pathCopy = path.ToArray();
            _moveAction = (s, c) =>
            {
                foreach (var step in pathCopy)
                    if (!c.TryMoveAlly(s, step)) break;
            };
        }

        /// <summary>지정한 절대 셀로 한 번에 이동 (점프).</summary>
        protected void PlanMoveTo(AllyInstance self, BoardController board, Vector2Int targetCell)
        {
            ClearMoveSlot();
            plannedMovePositions.Add(targetCell);
            _moveIntent = "이동";
            _moveAction = (s, c) => c.TryMoveAlly(s, targetCell);
        }

        /// <summary>가장 가까운 적으로부터 맨해튼 거리 distance인 빈 셀 중 무작위 순간이동.</summary>
        protected void PlanMoveRandomNearEnemy(AllyInstance self, BoardController board, int distance)
        {
            ClearMoveSlot();
            Vector2Int enemyPos = FindNearestEnemyPos(self, board);
            var candidates = new List<Vector2Int>();
            for (int dx = -distance; dx <= distance; dx++)
            {
                int dy = distance - Mathf.Abs(dx);
                candidates.Add(enemyPos + new Vector2Int(dx, dy));
                if (dy != 0) candidates.Add(enemyPos + new Vector2Int(dx, -dy));
            }
            var freeCells = FilterFreeCells(candidates, self, board);
            if (freeCells.Count == 0) return;

            Vector2Int dest = freeCells[UnityEngine.Random.Range(0, freeCells.Count)];
            plannedMovePositions.Add(dest);
            _moveIntent = "이동";
            _moveAction = (s, c) => c.TryMoveAlly(s, dest);
        }

        /// <summary>offsets 좌표 후보 중 빈 셀을 무작위로 골라 순간이동.</summary>
        protected void PlanMoveRandomToOffset(AllyInstance self, BoardController board,
            IEnumerable<Vector2Int> offsets, bool absolute = false)
        {
            ClearMoveSlot();
            Vector2Int origin = absolute ? Vector2Int.zero : self.GridPos;
            var candidates = new List<Vector2Int>();
            foreach (var off in offsets) candidates.Add(origin + off);
            var freeCells = FilterFreeCells(candidates, self, board);
            if (freeCells.Count == 0) return;

            Vector2Int dest = freeCells[UnityEngine.Random.Range(0, freeCells.Count)];
            plannedMovePositions.Add(dest);
            _moveIntent = "이동";
            _moveAction = (s, c) => c.TryMoveAlly(s, dest);
        }

        // ──────────────────────────────────────────────────────────
        // 기타 슬롯 헬퍼
        // ──────────────────────────────────────────────────────────

        protected void PlanSetBlock(AllyInstance self, BoardController board, int amount)
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

        protected void PlanApplyStatusToSelf(AllyInstance self, BoardController board,
                                             StatusEffectType type, int stacks)
        {
            ClearExtraSlot();
            int n = Mathf.Max(0, stacks);
            _extraIntent = type == StatusEffectType.Strength ? $"힘 +{n}"
                         : type == StatusEffectType.HealHP   ? $"회복 {n}"
                         : $"버프 {type}";
            _extraAction = (me, c) => c.ApplyStatusToAlly(me, type, n);
        }

        /// <summary>가장 가까운 적에게 디버프 부여.</summary>
        protected void PlanTargetDebuff(AllyInstance self, BoardController board,
                                        StatusEffectType type, int stacks)
        {
            ClearExtraSlot();
            int n = Mathf.Max(1, stacks);
            _extraIntent = $"디버프 {n}턴";

            var target = FindNearestEnemy(self, board);
            if (target == null) return;
            var t = target;
            _extraAction = (_, c) => { if (t.CurrentHP > 0) c.ApplyStatus(t, type, n); };
        }

        /// <summary>좌표 안의 적에게 광역 디버프.</summary>
        protected void PlanRangeDebuff(AllyInstance self, BoardController board,
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
                    var e = c.GetEnemyAt(cell);
                    if (e != null && e.CurrentHP > 0) c.ApplyStatus(e, type, n);
                }
            };
        }

        /// <summary>가장 가까운 적에게 회피 불가 디버프.</summary>
        protected void PlanUnavoidableDebuff(AllyInstance self, BoardController board,
                                             StatusEffectType type, int stacks)
            => PlanTargetDebuff(self, board, type, stacks);

        /// <summary>아군 소환 (offsets 좌표에 allyCode 소환).</summary>
        protected void PlanSummon(AllyInstance self, BoardController board,
                                  int allyCode, Vector2Int[] offsets, bool absolute = false)
        {
            ClearExtraSlot();
            if (offsets == null || offsets.Length == 0) return;
            var copy = (Vector2Int[])offsets.Clone();
            _extraIntent = "소환";
            _extraAction = (s, c) =>
            {
                foreach (var o in copy)
                {
                    Vector2Int cell = absolute ? o : s.GridPos + o;
                    if (c.IsCellFreeForAlly(cell))
                        c.SummonAllyAt(allyCode, cell);
                }
            };
        }

        /// <summary>자폭: 자신 제거 + onExplode 콜백.</summary>
        protected void PlanSelfDestruct(AllyInstance self, BoardController board,
                                        Action<AllyInstance, BoardController> onExplode = null)
        {
            ClearExtraSlot();
            ClearAttackSlot();
            ClearMoveSlot();
            _extraIntent = "자폭";
            _extraAction = (s, c) =>
            {
                onExplode?.Invoke(s, c);
                c.RemoveAllyFromCombat(s);
            };
        }

        /// <summary>완전 자유 액션.</summary>
        protected void PlanCustom(string intentText, Action<AllyInstance, BoardController> action)
        {
            ClearExtraSlot();
            _extraIntent = intentText ?? "행동";
            _extraAction = action;
        }

        protected void PlanWait(string intentText = "대기")
        {
            ClearAllSlots();
            _extraIntent = intentText;
        }

        // ──────────────────────────────────────────────────────────
        // 슬롯 클리어
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
            _moveAction = null;
            _moveIntent = null;
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
        // 경로/셀 검사 유틸
        // ──────────────────────────────────────────────────────────

        protected Vector2Int CalcOneStepToward(Vector2Int from, Vector2Int target,
                                               BoardController board, HashSet<Vector2Int> reserved)
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
            if (primary != Vector2Int.zero && CanMoveInto(candidate, board, reserved))
                return candidate;
            candidate = from + secondary;
            if (secondary != Vector2Int.zero && CanMoveInto(candidate, board, reserved))
                return candidate;

            int currentDist = Mathf.Abs(from.x - target.x) + Mathf.Abs(from.y - target.y);
            Vector2Int best = from;
            int bestDist = currentDist;
            foreach (var dir in AdjacentFour)
            {
                Vector2Int step = from + dir;
                if (!CanMoveInto(step, board, reserved)) continue;
                int d = Mathf.Abs(step.x - target.x) + Mathf.Abs(step.y - target.y);
                if (d < bestDist) { bestDist = d; best = step; }
            }
            return best;
        }

        protected Vector2Int CalcOneStepAway(Vector2Int from, Vector2Int enemyPos,
                                              BoardController board, HashSet<Vector2Int> reserved)
        {
            int currentDist = Mathf.Abs(from.x - enemyPos.x) + Mathf.Abs(from.y - enemyPos.y);
            Vector2Int best = from;
            int bestDist = currentDist;
            foreach (var dir in AdjacentFour)
            {
                Vector2Int step = from + dir;
                if (!CanMoveInto(step, board, reserved)) continue;
                int d = Mathf.Abs(step.x - enemyPos.x) + Mathf.Abs(step.y - enemyPos.y);
                if (d > bestDist) { bestDist = d; best = step; }
            }
            return best;
        }

        protected bool CanMoveInto(Vector2Int pos, BoardController board, HashSet<Vector2Int> reserved)
        {
            if (!board.IsInBoard(pos)) return false;
            if (!board.IsCellFreeForAlly(pos)) return false;
            if (reserved != null && reserved.Contains(pos)) return false;
            return true;
        }

        protected HashSet<Vector2Int> GatherReservedCells(AllyInstance self, BoardController board)
        {
            var reserved = new HashSet<Vector2Int>();
            foreach (var other in board.GetAllies())
            {
                if (other == self) continue;
                if (other.CurrentHP <= 0) continue;
                if (other.Behavior == null) continue;
                var moves = other.Behavior.GetMovePreviewPositions(other);
                if (moves == null) continue;
                foreach (var p in moves) reserved.Add(p);
            }
            return reserved;
        }

        protected List<Vector2Int> FilterFreeCells(IEnumerable<Vector2Int> candidates,
                                                   AllyInstance self, BoardController board)
        {
            var reserved = GatherReservedCells(self, board);
            var result = new List<Vector2Int>();
            if (candidates == null) return result;
            foreach (var p in candidates)
                if (CanMoveInto(p, board, reserved)) result.Add(p);
            return result;
        }
    }
}
