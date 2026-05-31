using System.Collections.Generic;
using UnityEngine;
using DeckRoguelike.UI;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 기본 제공 적 행동 클래스들을 EnemyBehaviorRegistry에 등록합니다.
    /// CombatController.Start()에서 EnemyBehaviorLibrary.RegisterAll()을 호출하세요.
    ///
    /// 새 적 추가 절차:
    ///   1. 아래에 EnemyBehavior 상속 클래스를 작성 (PlanTurn만 오버라이드)
    ///   2. RegisterAll()에 EnemyData.enemyCode와 함께 한 줄 추가
    ///   3. PlanTurn 안에서 헬퍼 호출 — 한 턴에 공격·이동·기타 슬롯을 자유 조합 가능
    ///
    /// 사용 가능한 헬퍼:
    ///   조건:
    ///     IsTargetInRange(self, combat, offsets)  — 플레이어 또는 아군이 offsets 안에 있으면 true
    ///     IsPlayerInRange(self, combat, offsets)  — 플레이어만 offsets 안에 있으면 true
    ///     ManhattanToNearest / ChebyshevToNearest — 가장 가까운 대상(플레이어·아군)까지 거리
    ///     ManhattanToPlayer / ChebyshevToPlayer   — 플레이어까지 거리
    ///     IsHpBelow(self, ratio) / IsHpAbove(self, ratio)
    ///
    ///   선택:    NextSequential / RollDice / RollChance / PickRandom
    ///
    ///   공격:
    ///     PlanTargetAttack(self, combat, target, extraDamage=0)
    ///       — 지정 대상(Player/Ally/Nearest) 직접 공격. 사거리 체크 없음.
    ///     PlanRangeAttack(self, combat, offsets, extraDamage=0, absolute=false)
    ///       — 좌표들에 광역 피해. absolute=true면 절대 좌표.
    ///     PlanUnavoidableAttack(self, combat, target, extraDamage=0, targetAlly=null)
    ///       — 지정 대상에게 회피 불가 직접 공격.
    ///     PlanAttackAndMoveToCell(self, combat, target, extraDamage=0)
    ///       — 공격 + 그 대상의 좌표로 즉시 이동 결합. Phase 1에서 실행 (지연 X).
    ///         대상이 살아있어 점유돼있으면 이동만 자동 실패. 처치 시 그 빈 칸으로 진입.
    ///
    ///   이동 (모두 Phase 2 지연 실행 — player 턴 종료 후 BoardController가 정렬·실행):
    ///     PlanMoveTowardNearest(self, combat, steps=1)
    ///       — 가장 가까운 대상(플레이어 또는 아군) 방향으로 BFS 최단경로 steps칸.
    ///     PlanMoveTowardPlayer(self, combat, steps=1)
    ///       — 플레이어 방향으로 BFS 최단경로 steps칸.
    ///     PlanMoveTowardAlly(self, combat, steps=1)
    ///       — 가장 가까운 아군 방향으로 BFS 최단경로. 아군이 없으면 플레이어로 fallback.
    ///     PlanMoveAwayFromPlayer(self, combat, steps=1)
    ///       — 플레이어 반대 방향으로 steps칸 후퇴 (greedy).
    ///     PlanMoveTo(self, combat, targetCell)
    ///       — 절대 좌표 targetCell로 텔레포트 (경로 무시). 점유 시 자동 실패.
    ///     PlanMoveRandomNearPlayer(self, combat, 2)
    ///       — 플레이어에서 n칸 거리의 좌표 중 무작위 빈 칸으로 텔레포트.
    ///     ※ 모든 이동은 가까운 적 먼저 정렬되어 셀 비우기 효과로 후순위 적 BFS 단축.
    ///     ※ PlanAttackAndMoveToCell만 Phase 1에서 공격과 함께 즉시 실행.
    ///
    ///   버프 디버프:
    ///     PlanSetBlock(self, combat, amount)
    ///       — 방어도를 amount로 초기화 (+=가 아니라 = 대입).
    ///     PlanDebuff(self, combat, target, type, stacks, targetAlly=null)
    ///       — 지정 대상(Player/Ally/Nearest)에게 디��프 부여.
    ///     PlanCustom(intentText, action)
    ///       — 자유 액션 등록. action = (EnemyInstance s, CombatController c) => { ... }.
    ///
    ///   소환/자폭:
    ///     PlanSummon(self, combat, enemyId, offsets, absolute=false)
    ///       — offsets 좌표에 적 소환. absolute=true면 절대 좌표.
    ///     PlanSelfDestruct(self, combat, onExplode=null)
    ///       — 자폭하여 자신 제거. onExplode로 자폭 시 효과(광역 피해/디버프 등) 지정.
    ///
    ///   전체:
    ///     PlanWait(intentText="대기")
    ///       — 아무 행동도 하지 않음. 인텐트 텍스트만 표시.
    ///
    ///   유틸:
    ///     FindNearestTargetPos(self, combat) → Vector2Int  — 가장 가까운 대상 좌표
    ///     FilterFreeCells(candidates, self, combat) → List<Vector2Int>
    ///       — candidates 목록에서 그리드 범위 내에 있고 빈 칸인 셀만 반환.
    ///
    ///   좌표상수 (Vector2Int[], 자기 기준 상대 오프셋):
    ///     AdjacentFour  — 상하좌우 1칸 (4개)
    ///     DiagonalFour  — 대각선 4방향 1칸 (4개)
    ///     AdjacentEight — 8방향 1칸 (8개)
    ///     Cross2        — 상하좌우 1~2칸 (8개)
    ///     KnightJump    — 체스 나이트 점프 8칸
    /// </summary>
    public static class EnemyBehaviorLibrary
    {
        public static void RegisterAll()
        {
            EnemyBehaviorRegistry.Register(11000, () => new SlimeBehavior());
            EnemyBehaviorRegistry.Register(11001, () => new LavaSlimeBehavior());
            EnemyBehaviorRegistry.Register(11002, () => new ChillSlimeBehavior());
            EnemyBehaviorRegistry.Register(11006, () => new KingSlimeBehavior());
            EnemyBehaviorRegistry.Register(11007, () => new KingBomberSlimeBehavior());
            EnemyBehaviorRegistry.Register(11020, () => new GoblineWarriorBehavior());
            EnemyBehaviorRegistry.Register(11021, () => new GoblinearchorBehavior());
            EnemyBehaviorRegistry.Register(11022, () => new GoblinBomberBehavior());
            EnemyBehaviorRegistry.Register(11030, () => new StoneGuardianBehavior());
            EnemyBehaviorRegistry.Register(11040, () => new ThiefBehavior());
            EnemyBehaviorRegistry.Register(11050, () => new BatBehavior());
            EnemyBehaviorRegistry.Register(11060, () => new SpiderBehavior());
            EnemyBehaviorRegistry.Register(11070, () => new PoisonMushroom1Behavior());
            EnemyBehaviorRegistry.Register(11071, () => new PoisonMushroom2Behavior());
            
            EnemyBehaviorRegistry.Register(12000, () => new LaserStatueChaserBehavior());
            EnemyBehaviorRegistry.Register(12010, () => new LaserStatueCrossBehavior());
            EnemyBehaviorRegistry.Register(12011, () => new LaserStatueDiagonalBehavior());

            EnemyBehaviorRegistry.Register(11900, () => new DemonLordBehavior());
            EnemyBehaviorRegistry.Register(11901, () => new SnakeBehavior());
            EnemyBehaviorRegistry.Register(11910, () => new KnightBehavior());
            EnemyBehaviorRegistry.Register(11911, () => new RookBehavior());
            EnemyBehaviorRegistry.Register(11912, () => new BishopBehavior());
        }
    }

    // ── 행동 구현 ─────────────────────────────────────────────────────

    /// <summary>슬라임 (11000): 2턴에 1번 — 인접 시 공격, 아니면 플레이어 방향 1칸 이동 (재계산 시 공격↔이동 자유 전환).</summary>
    public class SlimeBehavior : EnemyBehavior
    {
        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            // 슬라임 계열은 2턴에 한 번만 행동 — 0번째 턴 행동, 1번째 턴 휴식.
            if (NextSequential(2) == 1) { PlanWait("휴식"); return; }
            if (IsPlayerInRange(self, board, AdjacentFour))
                PlanTargetAttack(self, board, AttackTarget.Nearest);
            else
                PlanMoveTowardNearest(self, board, 1);
        }
    }

    /// <summary>죽으면 점유 셀마다 지대(Zone)를 남기는 슬라임 계열 베이스. 이동/공격 알고리즘은 기본 슬라임(11000)과 동일(2턴에 1번, 공격↔이동 자유 전환).</summary>
    public abstract class ZoneSlimeBehavior : SlimeBehavior
    {
        /// <summary>사망 시 점유 셀마다 깔아둘 Zone 위해 코드.</summary>
        protected abstract int DeathZoneHazardCode { get; }

        public override void OnDeath(EnemyInstance self, BoardController board)
        {
            if (self == null || board == null) return;
            var data = DeckRoguelike.Core.HazardRegistry.GetHazard(DeathZoneHazardCode);
            if (data == null) return;
            // 죽은 셀(footprint)마다 지대 생성. OnDeath 시점엔 그리드 점유는 이미 비워졌지만
            // GridPos는 그대로라 OccupiedCells가 죽은 자리를 그대로 가리킨다.
            foreach (var cell in self.OccupiedCells)
                board.EmplaceHazard(data, cell, data.value);
        }
    }

    /// <summary>화염 슬라임 (11001): 슬라임과 동일하게 행동하되, 죽으면 점유 셀에 용암지대(32140)를 남긴다.</summary>
    public class LavaSlimeBehavior : ZoneSlimeBehavior
    {
        protected override int DeathZoneHazardCode => 32140;
    }

    /// <summary>얼음 슬라임 (11002): 슬라임과 동일하게 행동하되, 죽으면 점유 셀에 빙하지대(32242)를 남긴다.</summary>
    public class ChillSlimeBehavior : ZoneSlimeBehavior
    {
        protected override int DeathZoneHazardCode => 32242;
    }

    /// <summary>대왕 슬라임 (11006): 2턴에 1번 — 사거리 1 광역 공격 또는 가장 가까운 대상 방향 1칸 이동. HP 임계 도달 시 다음 턴 분열(점유 셀마다 11000 소환). 재계산 시 공격↔이동 자유 전환.</summary>
    public class KingSlimeBehavior : EnemyBehavior
    {
        private const int   SpawnEnemyId    = 11000;
        private const float ExplodeHpRatio  = 0.5f;  // MaxHP 대비 비율 — 미만으로 떨어지면 다음 턴에 분열
        private const int   AttackRange     = 1;

        // _splitArmed: HP가 임계 아래로 내려간 적이 한 번이라도 있어 다음 턴에 분열할 예정.
        // _exploded: 이미 분열을 수행해서 더 이상 행동할 수 없는 상태(객체는 곧 제거됨).
        private bool _splitArmed;
        private bool _exploded;

        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            if (_exploded) { PlanWait("..."); return; }

            // HP 임계 진입 → 분열은 휴식 턴보다 우선. 공격·이동 슬롯은 PlanSelfDestruct가 비운다.
            if (_splitArmed || IsBelowExplodeRatio(self))
            {
                _splitArmed = true;
                _exploded   = true;
                PlanSplitIntoSlimes(self, board);
                return;
            }

            // 슬라임 계열은 2턴에 한 번만 행동 — 0번째 턴 행동, 1번째 턴 휴식.
            if (NextSequential(2) == 1) { PlanWait("휴식"); return; }

            if (TryBuildRangeAttackTowardNearest(self, board, AttackRange, out var attackCells))
            {
                RegisterRangeAttack(self, board, attackCells);
                return;
            }

            PlanMoveTowardNearest(self, board, 1);
        }

        /// <summary>절대 좌표 ring 셀을 self.GridPos 기준 상대 좌표로 변환해 등록 — 돌진 등으로
        /// 슬라임이 밀려도 공격 좌표가 footprint를 따라 이동하도록.</summary>
        private void RegisterRangeAttack(EnemyInstance self, BoardController board, Vector2Int[] attackCells)
        {
            var offsets = new Vector2Int[attackCells.Length];
            for (int i = 0; i < attackCells.Length; i++)
                offsets[i] = attackCells[i] - self.GridPos;
            PlanRangeAttack(self, board, offsets, absolute: false);
        }

        /// <summary>피해를 받은 직후 HP가 임계 미만이면 다음 적 턴에 분열하도록 예약.
        /// 즉시 자폭하지 않고 곧 다가올 적 턴 한 턴을 분열에 사용. 기존에 등록된 이번 턴 공격·이동은
        /// 취소하고 PlanTurn을 다시 호출해 분열을 등록 — 행동 변경 가드를 우회하기 위한 명시적 재계획.</summary>
        public override void OnDamaged(EnemyInstance self, BoardController board, int amount)
        {
            if (_exploded) return;
            if (_splitArmed) return;
            if (!IsBelowExplodeRatio(self)) return;

            _splitArmed = true;
            // 이번 턴(아직 적 턴 시작 전)에 등록된 공격/이동을 비우고, _splitArmed 기반의 새 plan을 등록.
            // 적 턴 실행 시 PlanSelfDestruct가 한 턴을 통째로 소모해 분열을 수행.
            board?.RePlanEnemyNow(self);
        }

        private static bool IsBelowExplodeRatio(EnemyInstance self)
            => self != null && self.MaxHP > 0 && self.CurrentHP < self.MaxHP * ExplodeHpRatio;

        // ── 분열 ──────────────────────────────────────────────────
        /// <summary>한 턴짜리 분열을 PlanCustom으로 등록.
        /// PlanSelfDestruct를 쓰지 않는 이유 — PlanSelfDestruct의 onExplode 콜백은 RemoveEnemyFromCombat
        /// 이후에 호출되므로 s.CurrentHP가 이미 0이고, plan 시점의 hp를 closure로 잡으면 plan~execute 사이에
        /// 슬라임이 추가 데미지를 받았을 때 분열 시점의 실제 HP가 반영되지 않는다.
        /// 여기선 execute 시점에 직접 s.CurrentHP / s.OccupiedCells를 읽은 뒤 RemoveEnemyFromCombat을
        /// 호출 → SummonEnemy로 점유 셀마다 일반 슬라임을 그 HP로 스폰 → 승리 판정.</summary>
        private void PlanSplitIntoSlimes(EnemyInstance self, BoardController board)
        {
            PlanCustom("자폭", (s, c) =>
            {
                // 실행 시점의 실제 값으로 캡처 — plan 이후 추가 데미지/푸시도 반영.
                int hp = Mathf.Max(1, s.CurrentHP);
                var occupied = new List<Vector2Int>(s.OccupiedCells);

                // PlanSelfDestruct와 동일한 순서: 먼저 footprint 비우기 → 같은 자리에 소환 → 승리 판정.
                c.RemoveEnemyFromCombat(s, checkVictory: false);

                foreach (var cell in occupied)
                {
                    var spawned = c.SummonEnemy(SpawnEnemyId, cell);
                    if (spawned == null) continue;
                    spawned.MaxHP     = hp;
                    spawned.CurrentHP = hp;
                    spawned.UI?.UpdateHP(hp, hp);
                }

                c.TriggerVictoryCheck();
            });
        }

        // ── 공격 좌표 계산 ────────────────────────────────────────

        /// <summary>
        /// Footprint와 정확히 range만큼 떨어진 BFS ring 셀 중 가장 가까운 대상이 있는 변(side)의
        /// 셀들만 골라 절대 좌표 배열로 반환. ring 안에 대상이 없으면 false.
        /// </summary>
        private static bool TryBuildRangeAttackTowardNearest(EnemyInstance self, BoardController board,
                                                              int range, out Vector2Int[] attackCells)
        {
            attackCells = null;
            var ring = ComputeFootprintRing(self, range);

            if (!TryFindTargetInCells(ring, board, out Vector2Int targetCell)) return false;

            var filtered = FilterCellsTowardTarget(ring, self, targetCell);
            if (filtered.Count == 0) return false;

            attackCells = filtered.ToArray();
            return true;
        }

        /// <summary>
        /// 점유 셀 전체에서 4-인접 BFS를 돌려 정확히 `range` 맨해튼 거리에 있는 셀들의 절대 좌표 모음.
        /// Footprint 크기·모양에 무관하게 한 번의 BFS로 ring을 만들어내므로 size가 바뀌어도 동작.
        /// </summary>
        private static List<Vector2Int> ComputeFootprintRing(EnemyInstance self, int range)
        {
            var dist  = new Dictionary<Vector2Int, int>();
            var queue = new Queue<Vector2Int>();
            foreach (var c in self.OccupiedCells) { dist[c] = 0; queue.Enqueue(c); }

            var ring = new List<Vector2Int>();
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                int d = dist[cur];
                if (d == range) { ring.Add(cur); continue; }
                foreach (var dir in AdjacentFour)
                {
                    var nxt = cur + dir;
                    if (dist.ContainsKey(nxt)) continue;
                    dist[nxt] = d + 1;
                    queue.Enqueue(nxt);
                }
            }
            return ring;
        }

        /// <summary>
        /// Footprint 바운딩박스 기준으로 `target`과 같은 변(side)에 있는 ring 셀만 반환.
        /// 대각 위치(예: 우상)는 X축을 우선해 좌/우 한 변만 채택.
        /// </summary>
        private static List<Vector2Int> FilterCellsTowardTarget(List<Vector2Int> ring,
                                                                EnemyInstance self, Vector2Int target)
        {
            Vector2Int anchor = self.GridPos;
            Vector2Int size   = self.Size;
            int xMin = anchor.x, xMax = anchor.x + size.x - 1;
            int yMin = anchor.y - size.y + 1, yMax = anchor.y;

            int sx = target.x < xMin ? -1 : target.x > xMax ? 1 : 0;
            int sy = target.y < yMin ? -1 : target.y > yMax ? 1 : 0;

            var result = new List<Vector2Int>();
            if (sx != 0)
            {
                foreach (var c in ring)
                    if ((sx < 0 && c.x < xMin) || (sx > 0 && c.x > xMax)) result.Add(c);
            }
            else if (sy != 0)
            {
                foreach (var c in ring)
                    if ((sy < 0 && c.y < yMin) || (sy > 0 && c.y > yMax)) result.Add(c);
            }
            return result;
        }

        private static bool TryFindTargetInCells(List<Vector2Int> cells, BoardController board, out Vector2Int found)
        {
            Vector2Int p = board.PlayerSpawnCell;
            foreach (var c in cells)
            {
                if (!board.IsInBoard(c)) continue;
                if (c == p) { found = c; return true; }
                foreach (var ally in board.GetAllies())
                    if (ally.CurrentHP > 0 && ally.GridPos == c) { found = c; return true; }
            }
            found = default;
            return false;
        }
    }

    /// <summary>자폭 슬라임 계열 (11007 베이스): 2턴에 1번 — 인접 시 자폭(점유+BFS-1 광역+선택적 소환), 아니면 가장 가까운 대상 방향 1칸 이동. 재계산 시 자폭↔이동 자유 전환.</summary>
    public class BomberSlimeBehavior : EnemyBehavior
    {
        /// <summary>자폭 시 점유 셀마다 소환할 적 ID. 0이면 소환 없음.</summary>
        protected virtual int SpawnEnemyIdOnExplode => 0;

        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            // 슬라임 계열은 2턴에 한 번만 행동 — 0번째 턴 행동, 1번째 턴 휴식.
            if (NextSequential(2) == 1) { PlanWait("휴식"); return; }
            if (IsTargetInRange(self, board, AdjacentFour))
            {
                PlanBombSelfDestruct(self, board);
                return;
            }
            PlanMoveTowardNearest(self, board, 1);
        }

        /// <summary>
        /// 자폭 시 동작:
        ///   1) 자기 점유 셀들 + 인접 4방향(BFS-1)에 광역 피해
        ///   2) SpawnEnemyIdOnExplode > 0 이면 점유 셀마다 해당 적을 자폭 직전 HP로 소환
        /// </summary>
        protected void PlanBombSelfDestruct(EnemyInstance self, BoardController board)
        {
            int dmg     = Mathf.Max(0, self.Damage);
            int spawnId = SpawnEnemyIdOnExplode;
            int spawnHp = Mathf.Max(1, self.CurrentHP);
            var occupied = new List<Vector2Int>(self.OccupiedCells);

            PlanSelfDestruct(self, board, (s, c) =>
            {
                // 1) 광역 피해 — 점유 셀 + 4방향 인접(BFS-1)
                var hit = new HashSet<Vector2Int>();
                foreach (var cell in occupied)
                {
                    hit.Add(cell);
                    foreach (var dir in AdjacentFour)
                        hit.Add(cell + dir);
                }
                foreach (var p in hit)
                {
                    if (!c.IsInBoard(p)) continue;
                    c.ApplyDamageAtCell(dmg, p, s);
                }

                // 2) 자폭 후 점유 셀마다 소환 (옵션)
                if (spawnId > 0)
                {
                    foreach (var cell in occupied)
                    {
                        var spawned = c.SummonEnemy(spawnId, cell);
                        if (spawned == null) continue;
                        spawned.CurrentHP = spawnHp;
                        spawned.MaxHP    = Mathf.Max(spawned.MaxHP, spawnHp);
                        spawned.UI?.UpdateHP(spawned.CurrentHP, spawned.MaxHP);
                    }
                }
            });
        }
    }

    /// <summary>
    /// 자폭 대왕 슬라임 (11007):
    ///   - BomberSlimeBehavior와 동일한 사거리/자폭 패턴.
    ///   - 자폭 시 KingSlimeBehavior(11006)와 같은 방식으로 점유 셀마다 후속 유닛을 소환하되,
    ///     11000 대신 11001(자폭 슬라임)을 소환한다.
    /// </summary>
    public class KingBomberSlimeBehavior : BomberSlimeBehavior
    {
        protected override int SpawnEnemyIdOnExplode => 11001;
    }

    /// <summary>고블린 전사 (11020): 인접 시 그 자리에서 공격(이동 없음), 아니면 가장 가까운 대상 방향 1칸 이동.
    /// 잠금 없음 — 플레이어가 움직여 RePlan될 때마다 사거리를 다시 판정해 공격↔이동을 자유롭게 전환한다.
    /// 공격은 상대 좌표(가장 가까운 대상)라 RePlan마다 대상을 재조준 → 인접해 있는 한 따라가며 때린다.</summary>
    public class GoblineWarriorBehavior : EnemyBehavior
    {
        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            // 잠금 없음 — 매 재계산마다 사거리를 새로 판정해 공격↔이동을 자유롭게 전환.
            if (IsTargetInRange(self, board, AdjacentFour))
                PlanTargetAttack(self, board, AttackTarget.Nearest);
            else
                PlanMoveTowardNearest(self, board, 1);
        }
    }
    /// <summary>
    /// 고블린 궁수 (11021): 2턴 주기 — 한 턴 충전, 다음 턴 발사.
    ///   - 발사 턴에는 가장 가까운 대상을 **상대 좌표**(적 기준 offset)로 노린다. 좌표를 고정하지 않으므로
    ///     플레이어 이동마다 PlanTurn이 재호출될 때 offset이 현재 대상 위치로 다시 계산된다 →
    ///     공격 셀(텔레그래프 스프라이트)이 대상을 졸졸 따라가, 칸을 벗어나도 빗나가지 않는다(**회피 불가**).
    ///   - 상대 좌표라 적이 밀려도(TryMoveEnemy로 재계산) 다시 대상을 향해 재조준한다.
    ///   - (대조: 도적 11040·박쥐 11050은 턴 시작 좌표를 절대 좌표로 한 번만 고정 → 칸을 벗어나면 회피 가능.)
    /// </summary>
    public class GoblinearchorBehavior : EnemyBehavior
    {
        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            // 0번째 턴: 충전 / 1번째 턴: 발사 — 교대 (재계산 시 seq 스냅샷/복원으로 안정).
            if (NextSequential(2) == 1)
            {
                // 발사 턴 — 상대 좌표로 현재 대상을 노린다. 좌표를 잠그지 않아 재계산마다 offset이
                // 갱신되므로 대상을 추적한다(회피 불가). 텔레그래프 스프라이트도 대상을 따라간다.
                Vector2Int target = FindNearestTargetPos(self, board);
                PlanRangeAttack(self, board, new[] { target - self.GridPos }, absolute: false);
            }
            else
            {
                PlanWait("충전");
            }
        }
    }
    /// <summary>고블린 폭탄병 (11022): 8방향 1칸 안에 대상 있으면 자폭(점유 기준 5x5 광역), 아니면 플레이어 방향 1칸 이동 (재계산 시 자폭↔이동 자유 전환).</summary>
    public class GoblinBomberBehavior : EnemyBehavior
    {
        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            // 잠금 없음 — 매 재계산마다 사거리를 새로 판정해 자폭↔이동을 자유롭게 전환.
            if (IsTargetInRange(self, board, AdjacentEight))
            {
                PlanExplode(self, board);
                return;
            }
            PlanMoveTowardPlayer(self, board, 1);
        }

        /// <summary>자폭 — 점유 셀 기준 사거리 2(체비쇼프 거리 ≤ 2 = 5x5) 광역 피해를 입히고 자신 제거.</summary>
        private void PlanExplode(EnemyInstance self, BoardController board)
        {
            int dmg = Mathf.Max(0, self.Damage);
            var occupied = new List<Vector2Int>(self.OccupiedCells);

            PlanSelfDestruct(self, board, (s, c) =>
            {
                var hit = new HashSet<Vector2Int>();
                foreach (var cell in occupied)
                {
                    // 점유 셀 기준 사거리 2 — 체비쇼프 거리 ≤ 2 (5x5, 중심 포함).
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dy = -2; dy <= 2; dy++)
                            hit.Add(cell + new Vector2Int(dx, dy));
                }
                foreach (var p in hit)
                {
                    if (!c.IsInBoard(p)) continue;
                    c.ApplyDamageAtCell(dmg, p, s);
                }
            });
        }
    }
    /// <summary>
    /// 석상 수호자: 매 턴 방어도(Block)를 일정 값으로 초기화.
    /// </summary>
    public class StoneGuardianBehavior : EnemyBehavior
    {
        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            PlanSetBlock(self, board, amount: 5);
        }
    }

    /// <summary>
    /// 레이저 석상-추격형 (12000):
    ///   - 평소: 플레이어 방향 1칸 BFS 이동 (PlanMoveTowardPlayer).
    ///   - 플레이어가 Manhattan 1(4방향 인접)에 들어오면: 대각선 1칸 이동 + **플레이어 절대 좌표** 공격.
    ///     대각선 방향은 corner pressure 점수(플레이어 escape 셀 덮기) 최대인 셀 우선,
    ///     동점이면 플레이어 최근접 코너에 더 가까운 셀(플레이어를 코너로 밀어붙임).
    ///   - 카테고리 + 공격/이동 셀 모두 PlanTurn 첫 호출에 lock → RePlan에서 좌표 추적 X (회피 가능).
    ///     plan된 이동 셀이 같은 턴에 다른 적이 점유 예정(reserved)이면 후보에서 제외 →
    ///     여러 석상이 둘러쌀 때 자연스럽게 서로 다른 대각선 선택.
    /// </summary>
    public class LaserStatueChaserBehavior : EnemyBehavior
    {
        private static readonly Vector2Int[] ChaseDiagonals =
        {
            new Vector2Int( 1,  1), new Vector2Int(-1,  1),
            new Vector2Int( 1, -1), new Vector2Int(-1, -1),
        };

        private Vector2Int _chaseAttackCell;
        private Vector2Int _chaseMoveCell;
        private bool _chaseAttackHasMove;  // 잠긴 공격이 이동 동반인지 (대각선 후보 있을 때만)

        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            // 같은 턴 RePlan: 이미 잠긴 카테고리/셀 유지.
            if (IsActionLocked)
            {
                if (ActionLockCategory == LockedCategory.Attack)
                {
                    if (_chaseAttackHasMove)
                        PlanAttackAtCellAndMoveTo(self, board, _chaseAttackCell, _chaseMoveCell);
                    else
                        PlanRangeAttack(self, board, new[] { _chaseAttackCell }, absolute: true);
                }
                else
                {
                    PlanMoveTowardPlayer(self, board, 1);
                }
                return;
            }

            // 첫 PlanTurn — 사거리 판정 후 카테고리/셀 lock.
            if (ManhattanToPlayer(self, board) <= 1)
            {
                _chaseAttackCell = board.PlayerSpawnCell;
                Vector2Int diag = PickCorneringDiagonal(self, board, _chaseAttackCell);
                _chaseAttackHasMove = (diag != self.GridPos);
                if (_chaseAttackHasMove)
                {
                    _chaseMoveCell = diag;
                    PlanAttackAtCellAndMoveTo(self, board, _chaseAttackCell, _chaseMoveCell);
                }
                else
                {
                    PlanRangeAttack(self, board, new[] { _chaseAttackCell }, absolute: true);
                }
                LockActionCategory(LockedCategory.Attack);
            }
            else
            {
                PlanMoveTowardPlayer(self, board, 1);
                LockActionCategory(LockedCategory.Move);
            }
        }

        /// <summary>4대각선 후보 중 플레이어 escape 셀을 가장 많이 덮는 셀을 선택.
        /// 동점이면 플레이어의 최근접 코너에 더 가까운 셀 우선 (코너로 밀어붙임).
        /// reserved(같은 턴 다른 적의 안착 예정 셀) 회피 → 다중 석상 둘러싸기에서 셀 겹침 방지.
        /// 마땅한 후보 없으면 self.GridPos 반환 (이동 없이 공격만).</summary>
        private Vector2Int PickCorneringDiagonal(EnemyInstance self, BoardController board, Vector2Int playerPos)
        {
            var reserved = GatherReservedCells(self, board);
            Vector2Int playerCorner = NearestCorner(playerPos, board);

            Vector2Int best = self.GridPos;
            int bestScore = -1;
            int bestCornerDist = int.MaxValue;

            foreach (var d in ChaseDiagonals)
            {
                Vector2Int cand = self.GridPos + d;
                if (!board.IsInBoard(cand)) continue;
                if (cand == playerPos) continue;
                if (reserved.Contains(cand)) continue;
                if (!board.IsCellFreeForEnemy(cand)) continue;

                int score = CountTargetEscapesCovered(cand, playerPos, board);
                int cdist = Mathf.Abs(cand.x - playerCorner.x) + Mathf.Abs(cand.y - playerCorner.y);
                if (score > bestScore || (score == bestScore && cdist < bestCornerDist))
                {
                    bestScore = score;
                    bestCornerDist = cdist;
                    best = cand;
                }
            }
            return best;
        }

        private static Vector2Int NearestCorner(Vector2Int pos, BoardController board)
        {
            int cols = board.BoardColumns;
            int rows = board.BoardRows;
            var corners = new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(cols - 1, 0),
                new Vector2Int(0, rows - 1),
                new Vector2Int(cols - 1, rows - 1),
            };
            Vector2Int best = corners[0];
            int bestDist = Mathf.Abs(pos.x - best.x) + Mathf.Abs(pos.y - best.y);
            for (int i = 1; i < corners.Length; i++)
            {
                int d = Mathf.Abs(pos.x - corners[i].x) + Mathf.Abs(pos.y - corners[i].y);
                if (d < bestDist) { bestDist = d; best = corners[i]; }
            }
            return best;
        }
    }
    /// <summary>
    /// 레이저 석상-십자형 (12010): 2턴마다 한 번 상하좌우 직선(십자) 좌표 공격, 그 외 턴은 충전.
    /// (NextSequential(2) 사이클 — 재계획 시 자동 스냅샷/복원으로 안정.)
    /// </summary>
    public class LaserStatueCrossBehavior : EnemyBehavior
    {
        // 자기 기준 상하좌우 1~4칸 (16칸)
        private static readonly Vector2Int[] CrossLine4 =
        {
            new Vector2Int( 1,  0), new Vector2Int( 2,  0), new Vector2Int( 3,  0), new Vector2Int( 4,  0),
            new Vector2Int(-1,  0), new Vector2Int(-2,  0), new Vector2Int(-3,  0), new Vector2Int(-4,  0),
            new Vector2Int( 0,  1), new Vector2Int( 0,  2), new Vector2Int( 0,  3), new Vector2Int( 0,  4),
            new Vector2Int( 0, -1), new Vector2Int( 0, -2), new Vector2Int( 0, -3), new Vector2Int( 0, -4),
        };

        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            // 0번째 턴: 공격 / 1번째 턴: 휴식 — 교대
            if (NextSequential(2) == 0)
                PlanRangeAttack(self, board, CrossLine4);
            else
                PlanWait("충전");
        }
    }

    /// <summary>
    /// 레이저 석상-대각선형 (12011): 2턴마다 한 번 4대각선 직선 좌표 공격, 그 외 턴은 충전.
    /// (NextSequential(2) 사이클 — 재계획 시 자동 스냅샷/복원으로 안정.)
    /// </summary>
    public class LaserStatueDiagonalBehavior : EnemyBehavior
    {
        // 자기 기준 4대각선 1~4칸 (16칸)
        private static readonly Vector2Int[] DiagonalLine4 =
        {
            new Vector2Int( 1,  1), new Vector2Int( 2,  2), new Vector2Int( 3,  3), new Vector2Int( 4,  4),
            new Vector2Int(-1,  1), new Vector2Int(-2,  2), new Vector2Int(-3,  3), new Vector2Int(-4,  4),
            new Vector2Int( 1, -1), new Vector2Int( 2, -2), new Vector2Int( 3, -3), new Vector2Int( 4, -4),
            new Vector2Int(-1, -1), new Vector2Int(-2, -2), new Vector2Int(-3, -3), new Vector2Int(-4, -4),
        };

        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            // 0번째 턴: 공격 / 1번째 턴: 휴식 — 교대
            if (NextSequential(2) == 0)
                PlanRangeAttack(self, board, DiagonalLine4);
            else
                PlanWait("충전");
        }
    }

    /// <summary>
    /// 독버섯1 (12070):
    ///   - 평소엔 대기. 플레이어가 8방향 인접(AdjacentEight) 안에 들어오면 그 자리에 정지한 채
    ///     다음 턴에 8방향 광역 공격을 예약.
    ///   - 광역 공격을 끝낸 다음 턴은 다시 평소 상태로 돌아가 재탐지.
    ///
    /// _armedThisTurn은 OnTurnFullyResolved에서만 갱신 — PlanTurn이 플레이어 이동 시
    /// 여러 번 재호출되어도 사이클이 이중 진행되지 않도록.
    /// </summary>
    public class PoisonMushroom1Behavior : EnemyBehavior
    {
        private bool _armedThisTurn;      // 이번 턴에 광역 공격할 차례인지
        private bool _willArmNextTurn;    // 이번 턴 결과로 다음 턴에 공격할지 (PlanTurn마다 재평가)

        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            _willArmNextTurn = false;

            if (_armedThisTurn)
            {
                PlanRangeAttack(self, board, AdjacentEight);
                return;
            }

            if (IsPlayerInRange(self, board, AdjacentEight))
            {
                _willArmNextTurn = true;
                PlanWait("준비");
            }
            else
            {
                PlanWait("대기");
            }
        }

        public override void OnTurnFullyResolved(EnemyInstance self, BoardController board)
        {
            base.OnTurnFullyResolved(self, board);
            // 이번 턴이 공격이었으면 disarm, 이번 턴에 접근했으면 다음 턴 공격으로 전환.
            _armedThisTurn = _willArmNextTurn;
            _willArmNextTurn = false;
        }
    }

    /// <summary>
    /// 독버섯2 (12071): 2턴마다 한 번 8방향 광역 좌표 공격, 그 외 턴은 충전.
    /// (LaserStatue 계열과 같은 NextSequential(2) 사이클 — 재계획 시 자동 스냅샷/복원으로 안정.)
    /// </summary>
    public class PoisonMushroom2Behavior : EnemyBehavior
    {
        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            if (NextSequential(2) == 0)
                PlanRangeAttack(self, board, AdjacentEight);
            else
                PlanWait("충전");
        }
    }

    /// <summary>
    /// 도적 (11040):
    ///   - 공격 턴: 플레이어의 (턴 시작 시점) 좌표를 노리는 회피 가능한 공격. 노린 좌표는 그 턴 동안
    ///     고정되어, 플레이어가 그 칸을 벗어나면 빗나간다(피할 수 있음). 명중하면 플레이어가 골드 20 손실.
    ///   - 여러 도적이 동시에 공격할 때 같은 셀을 중복으로 노리지 않는다: 한 도적이 플레이어 셀을
    ///     노리면 나머지 도적은 플레이어에서 가장 가까운, 아직 아무도 노리지 않은 셀을 분배받는다.
    ///   - 공격 후 이동(Phase 2): 노린 셀이 비어 있으면(플레이어가 벗어남) 그 셀로 파고들고,
    ///     플레이어가 그대로면(명중 예정 — 그 셀엔 못 올라가니) 플레이어에 가장 가까운 빈 셀로 이동.
    ///     → 공격 텔레그래프와 이동 텔레그래프 sprite가 함께 표시된다.
    ///   - 후퇴 턴: 플레이어로부터 2칸 떨어진 무작위 빈 칸으로 순간이동.
    ///   - 공격 ↔ 후퇴를 번갈아 반복하되, 플레이어를 한 번이라도 맞히면 다음 턴 후퇴를 마친 뒤
    ///     "도망"(자폭처럼 전투에서 이탈)한다.
    /// </summary>
    public class ThiefBehavior : EnemyBehavior
    {
        private const int GoldLossOnHit = 20;

        // 셀 분배/이동 후보 탐색 시 플레이어 주변을 훑는 최대 맨해튼 반경 (보드보다 넉넉히).
        private const int SearchRadius = 12;

        // 공격/후퇴 토글. true면 이번 턴은 공격, false면 후퇴. 토글은 OnTurnFullyResolved(적 턴당
        // 1회)에서만 — PlanTurn은 재계산으로 한 턴에 여러 번 불리므로, 토글을 여기서 하면 재계산마다
        // 공격↔후퇴가 깜빡인다. NextSequential은 턴 시작 호출과 재계산 호출의 카운터가 어긋날 수 있어
        // 사용하지 않는다.
        private bool _attackTurn = true;

        // 도망 시퀀스 단계: 0 = 평소 공격/후퇴 반복, 1 = (피격 후) 후퇴, 2 = 도망.
        private int _fleeStage;
        private bool _hitThisTurn;            // 이번 적 턴 공격이 플레이어를 맞혔는지 (ExecuteTurn에서 set)

        // 공격 좌표 고정 — 공격 턴 동안 한 번만 캡처해 재계산 시에도 같은 칸을 노린다(회피 가능).
        private bool _attackCellCaptured;
        private Vector2Int _attackCell;

        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            // 도망 시퀀스 — 피격 후: 후퇴(1) → 도망(2).
            if (_fleeStage == 2)
            {
                PlanCustom("도망!", (s, c) => c.RemoveEnemyFromCombat(s));
                return;
            }
            if (_fleeStage == 1)
            {
                PlanMoveRandomNearPlayer(self, board, 2);
                return;
            }

            // 평소: 공격 ↔ 후퇴 반복.
            if (_attackTurn)
                PlanThiefAttack(self, board);
            else
                PlanMoveRandomNearPlayer(self, board, 2);
        }

        /// <summary>다른 도적과 겹치지 않는 좌표(공격 턴 시작 시 고정)를 노리는 회피 가능 공격.
        /// 명중(플레이어가 그 칸에 그대로 있음) 시 데미지 + 골드 20 손실 + 도망 시퀀스 발동.
        /// 공격 후 Phase 2에서 노린 셀(또는 플레이어 인접 셀)로 파고든다.</summary>
        private void PlanThiefAttack(EnemyInstance self, BoardController board)
        {
            if (!_attackCellCaptured)
            {
                // 셀 캡처는 턴 시작 시 enemies 순서대로 1회만 — 먼저 계획된 도적이 우선 셀을 차지하고
                // 뒤 도적은 남은 셀 중 플레이어에 가장 가까운 셀을 받는다. RePlan에서는 캡처를 건너뛰어
                // (이미 _attackCellCaptured) 같은 셀을 유지하므로 분배가 턴 내내 안정적이다.
                _attackCell = PickThiefAttackCell(self, board);
                _attackCellCaptured = true;
            }
            Vector2Int cell = _attackCell;

            // 공격 슬롯 — 고정된 절대 좌표에 데미지(미리보기/회피 가능 텔레그래프 포함).
            PlanRangeAttack(self, board, new[] { cell }, absolute: true);

            // 기타 슬롯 — 명중 판정 + 골드 손실. 빈 인텐트("")로 등록해 인텐트 표시는 공격만 노출.
            // ExecuteTurn은 기타 → 공격 순이라 데미지 적용 전에 플레이어 위치로 명중을 판정한다.
            PlanCustom("", (s, c) =>
            {
                if (c.PlayerSpawnCell != cell) return;   // 플레이어가 칸을 벗어남 → 빗나감
                _hitThisTurn = true;
                var gm = DeckRoguelike.Core.GameManager.Instance;
                if (gm != null)
                {
                    int loss = Mathf.Min(GoldLossOnHit, gm.Gold);
                    if (loss > 0) gm.ModifyGold(-loss);
                }
            });

            // 이동 슬롯 (Phase 2 — 공격 실행 후) — 노린 셀이 비었으면 그 셀로, 아니면(명중 예정)
            // 플레이어에 가장 가까운 빈 셀로. nextStep은 미리보기·실행 시점마다 보드 상태로 재계산되어
            // 이동 텔레그래프 sprite가 항상 최신 목적지를 가리킨다.
            SetDeferredMove(1, "이동",
                nextStep:   (s, c) => ComputeThiefMoveStep(s, c, cell),
                sortAnchor: (s, c) => cell);
        }

        /// <summary>공격 후 이동 목적지 1칸 계산.
        ///   · 노린 셀에서 플레이어가 벗어나 비어 있으면 → 그 셀로 진입(파고들기).
        ///   · 플레이어가 그대로면(명중 예정, 그 셀은 점유) → 플레이어에 가장 가까운 빈 셀로.
        /// 다른 적의 예정 이동(reserved/vacating)을 반영해 같은 칸 충돌을 피한다.</summary>
        private Vector2Int ComputeThiefMoveStep(EnemyInstance self, BoardController board, Vector2Int attackCell)
        {
            var reserved = GatherReservedCells(self, board);
            var vacating = GatherVacatingCells(self, board);

            if (board.PlayerSpawnCell != attackCell
                && CanMoveInto(attackCell, board, reserved, vacating))
                return attackCell;

            return FindNearestFreeCellToPlayer(self, board, reserved, vacating);
        }

        /// <summary>플레이어 셀이 아직 비어 있으면(아무 도적도 안 노림) 그 셀, 아니면 플레이어에서 가장
        /// 가까운(맨해튼) 보드 안 셀 중 다른 도적이 노리지 않은 셀을 반환. 동거리면 자신에 더 가까운 셀.</summary>
        private Vector2Int PickThiefAttackCell(EnemyInstance self, BoardController board)
        {
            Vector2Int player = board.PlayerSpawnCell;

            // 같은 턴 다른 도적들이 이미 캡처한 공격 셀 — 중복 공격 방지.
            var taken = new HashSet<Vector2Int>();
            foreach (var other in board.GetAliveEnemies())
            {
                if (other == self) continue;
                if (other.Behavior is ThiefBehavior tb && tb.TryGetCapturedAttackCell(out var c))
                    taken.Add(c);
            }

            if (!taken.Contains(player)) return player;

            Vector2Int best = player;
            int bestPlayerDist = int.MaxValue;
            int bestSelfDist = int.MaxValue;
            for (int dx = -SearchRadius; dx <= SearchRadius; dx++)
            for (int dy = -SearchRadius; dy <= SearchRadius; dy++)
            {
                Vector2Int p = new Vector2Int(player.x + dx, player.y + dy);
                if (!board.IsInBoard(p)) continue;
                if (taken.Contains(p)) continue;
                int pd = Mathf.Abs(dx) + Mathf.Abs(dy);
                int sd = Mathf.Abs(p.x - self.GridPos.x) + Mathf.Abs(p.y - self.GridPos.y);
                if (pd < bestPlayerDist || (pd == bestPlayerDist && sd < bestSelfDist))
                {
                    bestPlayerDist = pd; bestSelfDist = sd; best = p;
                }
            }
            return best;
        }

        /// <summary>플레이어에 가장 가까운(맨해튼) 이동 가능 빈 셀. 동거리면 자신에 더 가까운 셀.
        /// 후보가 없으면 제자리(self.GridPos).</summary>
        private Vector2Int FindNearestFreeCellToPlayer(EnemyInstance self, BoardController board,
            HashSet<Vector2Int> reserved, HashSet<Vector2Int> vacating)
        {
            Vector2Int player = board.PlayerSpawnCell;
            Vector2Int best = self.GridPos;
            int bestPlayerDist = int.MaxValue;
            int bestSelfDist = int.MaxValue;
            for (int dx = -SearchRadius; dx <= SearchRadius; dx++)
            for (int dy = -SearchRadius; dy <= SearchRadius; dy++)
            {
                Vector2Int p = new Vector2Int(player.x + dx, player.y + dy);
                if (p == player) continue;
                if (!CanMoveInto(p, board, reserved, vacating)) continue;
                int pd = Mathf.Abs(dx) + Mathf.Abs(dy);
                int sd = Mathf.Abs(p.x - self.GridPos.x) + Mathf.Abs(p.y - self.GridPos.y);
                if (pd < bestPlayerDist || (pd == bestPlayerDist && sd < bestSelfDist))
                {
                    bestPlayerDist = pd; bestSelfDist = sd; best = p;
                }
            }
            return best;
        }

        /// <summary>이번 턴 이 도적이 공격 셀을 확정했으면 그 셀을 반환(true). 공격 턴이 아니거나
        /// 아직 캡처 전이면 false. 다른 도적의 셀 분배(PickThiefAttackCell)에서 사용.</summary>
        public bool TryGetCapturedAttackCell(out Vector2Int cell)
        {
            cell = _attackCell;
            return _attackCellCaptured;
        }

        public override void OnTurnFullyResolved(EnemyInstance self, BoardController board)
        {
            base.OnTurnFullyResolved(self, board);

            // 도망 시퀀스 진행 (턴당 1회).
            if (_fleeStage == 1)
            {
                _fleeStage = 2;                  // 후퇴 완료 → 다음 턴 도망
            }
            else if (_fleeStage == 0)
            {
                if (_hitThisTurn) _fleeStage = 1;        // 이번 턴 명중 → 다음 턴 후퇴 후 도망
                else _attackTurn = !_attackTurn;         // 평소엔 공격 ↔ 후퇴 토글
            }

            _hitThisTurn = false;
            _attackCellCaptured = false;                  // 다음 공격 턴에 새 좌표 캡처
        }
    }

    /// <summary>
    /// 박쥐 (11050): 평소엔 1칸 무작위 배회.
    ///   - 플레이어가 인접 1칸(AdjacentFour) 안에 들어오면 공격 모드로 전환 (이후 유지).
    ///   - 공격 모드에서는 플레이어 턴 시작 시점의 플레이어 좌표를 한 번만 고정해 그 절대
    ///     좌표를 공격한다. 유도 공격이 아니므로 플레이어가 그 칸에서 벗어나면 피할 수 있다.
    ///
    /// PlanTurn은 플레이어 이동마다 재호출(재계획)되지만, 고정된 좌표(_lockedTarget)를 그대로
    /// 재등록하므로 좌표가 플레이어를 따라가지 않는다. 적 턴이 끝나면(OnTurnFullyResolved)
    /// 잠금을 풀어 다음 플레이어 턴 시작 시 좌표를 다시 고정한다.
    /// </summary>
    public class BatBehavior : EnemyBehavior
    {
        private bool _attackMode;
        private bool _targetLocked;
        private Vector2Int _lockedTarget;

        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            if (!_attackMode && IsPlayerInRange(self, board, AdjacentFour))
                _attackMode = true;

            if (_attackMode)
            {
                if (!_targetLocked)
                {
                    _lockedTarget = board.PlayerSpawnCell;
                    _targetLocked = true;
                }
                PlanRangeAttack(self, board, new[] { _lockedTarget }, absolute: true);
            }
            else
            {
                PlanMoveRandomToOffset(self, board, AdjacentFour);
            }
        }

        public override void OnTurnFullyResolved(EnemyInstance self, BoardController board)
        {
            base.OnTurnFullyResolved(self, board);
            // 적 턴 종료 — 다음 플레이어 턴 시작 시 좌표를 새로 고정하도록 잠금 해제.
            _targetLocked = false;
        }
    }

    /// <summary>
    /// 거미 (11060): 대각선 전용 행동.
    ///   - 대각선 1칸(DiagonalFour)에 플레이어가 있으면 대각선 공격.
    ///   - 아니면 플레이어와의 맨해튼 거리가 가장 줄어드는 대각선 한 칸으로 이동.
    ///   - 어떤 대각선도 비어있지 않으면 대기.
    /// </summary>
    public class SpiderBehavior : EnemyBehavior
    {
        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            if (IsPlayerInRange(self, board, DiagonalFour))
            {
                PlanTargetAttack(self, board, AttackTarget.Player);
                return;
            }

            Vector2Int dest = ChooseDiagonalStepTowardPlayer(self, board);
            if (dest != self.GridPos)
                PlanMoveTo(self, board, dest);
            else
                PlanWait("대기");
        }

        private static Vector2Int ChooseDiagonalStepTowardPlayer(EnemyInstance self, BoardController board)
        {
            Vector2Int p = board.PlayerSpawnCell;
            Vector2Int cur = self.GridPos;
            int curDist = Mathf.Abs(cur.x - p.x) + Mathf.Abs(cur.y - p.y);

            Vector2Int best = cur;
            int bestDist = curDist;
            foreach (var d in DiagonalFour)
            {
                Vector2Int nxt = cur + d;
                if (!board.IsInBoard(nxt)) continue;
                if (!board.IsCellFreeForEnemy(nxt)) continue;
                int dist = Mathf.Abs(nxt.x - p.x) + Mathf.Abs(nxt.y - p.y);
                if (dist < bestDist) { bestDist = dist; best = nxt; }
            }
            return best;
        }
    }

    /// <summary>
    /// 악마 군주: 페이즈 보스.
    ///   페이즈 1 (HP 50% 초과): 이동 → 강공격 → 강화 순환
    ///   페이즈 2 (HP 50% 이하): 매 턴 — 플레이어 좌표 직접 공격 + 플레이어 ±2칸으로 무작위 순간이동
    /// </summary>
    public class DemonLordBehavior : EnemyBehavior
    {
        public override void OnSpawn(EnemyInstance self, BoardController board)
        {
            Debug.Log($"[DemonLord] 등장 @ {self.GridPos}");
        }

        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            // 페이즈 2: HP 50% 이하 — 공격 + 이동을 두 슬롯으로 분리 등록
            if (IsHpBelow(self, 0.5f))
            {
                PhaseTwo(self, board);
                return;
            }

            // 페이즈 1: 이동 → 강공격 → 강화 순환
            PhaseOne(self, board);
        }

        // ── 페이즈 2: 공격 + 이동 분리 ──────────────────────────────
        private void PhaseTwo(EnemyInstance self, BoardController board)
        {
            Vector2Int playerPos = board.PlayerSpawnCell;

            // 공격 슬롯 — 플레이어 좌표에 직접 데미지
            PlanRangeAttack(self, board, new[] { playerPos }, extraDamage: 5, absolute: true);

            // 이동 슬롯 — 플레이어 맨해튼 거리 2칸 빈 셀 중 무작위
            PlanMoveRandomNearPlayer(self, board, 2);
        }

        // ── 페이즈 1: 3턴 순환 ──────────────────────────────────
        private void PhaseOne(EnemyInstance self, BoardController board)
        {
            switch (NextSequential(3))
            {
                case 0:
                    PlanMoveTowardPlayer(self, board, steps: 1);
                    break;
                case 1:
                    PlanTargetAttack(self, board, AttackTarget.Player, extraDamage: 5);
                    break;
                case 2:
                    PlanCustom("강화", (s, c) =>
                    {
                        // TODO: 자기 강화 효과
                    });
                    break;
            }
        }
    }

    /// <summary>
    /// 뱀 (11901): 같은 소환 호출(SpawnGroupId가 같음)에서 나온 11901들이 한 마리의 뱀 체인을 이룬다.
    ///   - CSV "11901:5.random:3" 한 엔트리 = 5칸짜리 뱀 1마리. "11901:5.random:3/11901:5.random:3"이면
    ///     5칸짜리 뱀 2마리(서로 독립). 런타임 SummonEnemy도 호출 1회마다 별개 뱀.
    ///   - 첫 번째로 소환된 객체가 "머리"(체인 0번). 모든 적이 배치된 뒤(OnAllEnemiesSpawned) 이후
    ///     세그먼트를 직전 세그먼트의 상하좌우 1칸 빈 칸으로 끌어와 머리→꼬리로 끊김 없이 이어붙인다.
    ///   - 매 턴 머리만 PlanTurn 로직을 실행 — PlanCustom("이동")으로 체인 전체를 머리→꼬리 순서로
    ///     한 칸씩 끌고 간다(스네이크 게임 이동). 각 세그먼트는 한 step 전 앞 세그먼트가 있던 칸으로
    ///     이동(SlideSnake가 새 위치를 배열에 먼저 캡처해 머리→꼬리로 적용). 자기 몸·벽·판 밖으로는 못 간다.
    ///
    /// [두 가지 모드 — SnakeState로 그룹마다 보존]
    ///   ① 랜덤 모드(기본): 머리가 인접 4칸 중 갈 수 있는 빈 칸 하나를 **랜덤**으로 고른다.
    ///      이 셀은 이번 플레이어 턴 동안 잠궈(hasPlannedStep) 재계획해도 흔들리지 않는다 → 미리보기 일치.
    ///   ② 추격 모드: 머리가 플레이어 쪽으로 한 칸씩 다가간다(인접하면 제자리 공격). 한 번 켜지면 전투 끝까지.
    ///   전환 트리거(둘 다 "이번 턴 종료 시" 승격 → 미리보기와 실행이 어긋나지 않음):
    ///     · 잠근 랜덤 이동 셀에 플레이어가 들어오면(= 뱀이 가려던 칸에 플레이어 도달), 또는
    ///     · 뱀이 피해를 입으면(OnDamaged) → attackModeArmed 예약 → OnTurnFullyResolved에서 attackMode=true.
    ///   - 공격(사거리 1): 모드와 무관하게 이동 전·후로 머리가 플레이어와 인접하면 같은 턴에 절대 좌표
    ///     공격을 등록(공격 range 스프라이트 표시). extra(SlideSnake)가 머리를 옮긴 뒤에도 적중.
    ///   - 이동 미리보기는 ComputeDeferredMovePreview 오버라이드가 머리의 다음 셀만 채운다(기본 구현은
    ///     PlanCustom 기반 뱀의 미리보기를 지워버리므로 반드시 오버라이드).
    ///   - 허리(중간 몸통)가 처치되면 그 지점부터 꼬리까지의 세그먼트를 한 번에 제거(뱀이 잘림).
    ///     머리 앞쪽 세그먼트는 더 짧은 뱀으로 살아남는다. 머리가 죽으면 몸 전체가 사라진다.
    /// 머리가 갈 칸이 없으면 그 뱀 전체가 정지. 몸통/꼬리는 PlanWait로 머리 호출만 기다린다.
    /// </summary>
    public class SnakeBehavior : EnemyBehavior
    {
        // SpawnGroupId → 그 그룹에 속한 살아있는 세그먼트(소환 순서). 0번이 머리.
        private static readonly Dictionary<int, List<EnemyInstance>> chains
            = new Dictionary<int, List<EnemyInstance>>();
        // SpawnGroupId → 그 뱀의 모드/이번 턴 계획 상태.
        private static readonly Dictionary<int, SnakeState> states
            = new Dictionary<int, SnakeState>();
        // 보드 인스턴스가 바뀌면(새 전투) 모든 체인 리셋.
        private static BoardController boundBoard;

        /// <summary>한 마리 뱀(그룹)의 런타임 상태. chain과 1:1로 gid 키 공유.</summary>
        private class SnakeState
        {
            public bool attackMode;       // 추격 모드 — 한 번 켜지면 전투 끝까지 유지.
            public bool attackModeArmed;  // 이번 플레이어 턴 종료 시 attackMode로 승격될 예약.
            public bool hasPlannedStep;   // 이번 플레이어 턴의 머리 이동 셀을 이미 골라 잠갔는지.
            public Vector2Int plannedStep; // 이번 턴 머리가 이동할 셀(랜덤 또는 추격). 이동 안 하면 머리 현재 위치.
            public bool willMove;          // plannedStep != 머리 현재 위치.
        }

        // SpawnGroupId 0(미지정) — 안전망: 각자 자기만의 그룹을 갖도록 인스턴스 해시로 분리.
        // 그래도 뱀 1칸짜리로 동작 (혼자 짧은 뱀).
        private static int GroupId(EnemyInstance self)
            => self.SpawnGroupId != 0 ? self.SpawnGroupId : self.GetHashCode();

        private static SnakeState StateOf(int gid)
        {
            if (!states.TryGetValue(gid, out var st)) { st = new SnakeState(); states[gid] = st; }
            return st;
        }

        public override void OnSpawn(EnemyInstance self, BoardController board)
        {
            if (boundBoard != board)
            {
                chains.Clear();
                states.Clear();
                boundBoard = board;
            }
            int gid = GroupId(self);
            if (!chains.TryGetValue(gid, out var list))
            {
                list = new List<EnemyInstance>();
                chains[gid] = list;
            }
            list.RemoveAll(e => e == null || e.CurrentHP <= 0);
            if (!list.Contains(self)) list.Add(self);
        }

        public override void OnAllEnemiesSpawned(EnemyInstance self, BoardController board)
        {
            // 머리(체인 0번)일 때만 1회 — 이 시점엔 모든 적이 보드에 있어 몸통 부착이 안전하다.
            int gid = GroupId(self);
            if (!chains.TryGetValue(gid, out var list)) return;
            list.RemoveAll(e => e == null || e.CurrentHP <= 0);
            if (list.Count == 0 || list[0] != self) return;

            // 머리는 배치기가 정한 위치 그대로. i번 세그먼트는 직전 세그먼트의 상하좌우 1칸 빈 칸으로
            // 끌어와 머리→꼬리로 이어붙인다(이미 인접하면 그대로). 빈 칸이 없으면 제자리(드문 폴백).
            for (int i = 1; i < list.Count; i++)
            {
                EnemyInstance prev = list[i - 1];
                EnemyInstance seg = list[i];
                if (prev == null || seg == null) continue;
                Vector2Int pp = prev.GridPos;
                if (Mathf.Abs(seg.GridPos.x - pp.x) + Mathf.Abs(seg.GridPos.y - pp.y) == 1)
                    continue;
                Vector2Int? target = FindAttachCell(list, i, board);
                if (target.HasValue && target.Value != seg.GridPos)
                    board.TryMoveEnemy(seg, target.Value, forced: true);
            }
        }

        /// <summary>피격 시 추격 모드를 예약 — 이번 플레이어 턴이 끝나면 attackMode로 승격된다.
        /// (몸통 어느 세그먼트가 맞아도 같은 그룹이므로 뱀 전체가 추격 모드로 전환.)</summary>
        public override void OnDamaged(EnemyInstance self, BoardController board, int amount)
        {
            if (amount <= 0) return;
            StateOf(GroupId(self)).attackModeArmed = true;
        }

        /// <summary>적 턴 종료 정리. 예약된 추격 모드를 이 시점에 승격시켜, 미리보기(랜덤 이동)와
        /// 실제 실행이 어긋나지 않게 한다 — 다음 플레이어 턴부터 추격 미리보기가 그대로 표시·실행된다.
        /// 또 이번 턴 잠근 랜덤 step을 풀어 다음 턴에 새 방향을 고르게 한다.</summary>
        public override void OnTurnFullyResolved(EnemyInstance self, BoardController board)
        {
            base.OnTurnFullyResolved(self, board);
            if (!states.TryGetValue(GroupId(self), out var st)) return;
            if (st.attackModeArmed) st.attackMode = true;
            st.attackModeArmed = false;
            st.hasPlannedStep = false;
        }

        public override void OnDeath(EnemyInstance self, BoardController board)
        {
            int gid = GroupId(self);
            if (!chains.TryGetValue(gid, out var list)) return;

            int idx = list.IndexOf(self);
            if (idx < 0)
            {
                list.Remove(self);
                if (list.Count == 0) chains.Remove(gid);
                return;
            }

            // 잘린 지점(self) 뒤쪽(꼬리 방향) 세그먼트를 먼저 스냅샷.
            var tail = new List<EnemyInstance>();
            for (int i = idx + 1; i < list.Count; i++)
                if (list[i] != null) tail.Add(list[i]);

            // ★ 체인 리스트를 먼저 정리(self + 꼬리 제거)한 뒤에 RemoveEnemyFromCombat을 호출한다.
            //   RemoveEnemyFromCombat → RePlanAllEnemyTurns → 머리 PlanTurn → chain.RemoveAll 이
            //   같은 list를 재진입 변형하므로, 인덱스 기반 제거를 먼저 끝내 두지 않으면
            //   뒤따르는 RemoveAt이 범위를 벗어나 예외(→ 카드가 버려지지 않는 버그)가 난다.
            list.RemoveRange(idx, list.Count - idx);
            if (list.Count == 0) chains.Remove(gid);

            // self 자신은 데미지 처리 블록이 이미 셀·GO를 정리했으므로 꼬리만 전투에서 제거.
            foreach (var seg in tail)
                if (seg.CurrentHP > 0)
                    board.RemoveEnemyFromCombat(seg, checkVictory: false);
        }

        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            int gid = GroupId(self);
            if (!chains.TryGetValue(gid, out var chain))
            {
                PlanWait("");
                return;
            }
            chain.RemoveAll(e => e == null || e.CurrentHP <= 0);

            // 머리(체인 0번)만 행동. 나머지는 머리의 SlideSnake에 의해 한꺼번에 끌려간다.
            if (chain.Count == 0 || chain[0] != self)
            {
                PlanWait("");
                return;
            }

            var st = StateOf(gid);
            Vector2Int cur = self.GridPos;
            Vector2Int player = board.PlayerSpawnCell;

            Vector2Int nextHead;
            if (st.attackMode)
            {
                // 추격 모드: 플레이어 쪽으로 한 칸. 이미 인접하면 제자리에서 공격만.
                nextHead = ComputeChaseStep(self, board, player);
            }
            else
            {
                // 랜덤 모드: 이번 턴의 머리 이동 셀을 한 번만 골라 잠근다(재계획해도 흔들리지 않음).
                if (!st.hasPlannedStep)
                {
                    st.plannedStep = ComputeRandomHeadStep(self, board);
                    st.hasPlannedStep = true;
                }
                nextHead = st.plannedStep;
                // 트리거: 잠근 랜덤 셀에 플레이어가 들어오면 이번 턴 종료 시 추격 모드로 전환(예약).
                if (player == nextHead) st.attackModeArmed = true;
            }

            // 속박(중력): 자발적 이동 차단 — 공격은 유지. ([[project_bondage_system]])
            bool canMove = !self.IsBound && nextHead != cur;
            st.plannedStep = nextHead;
            st.willMove = canMove;

            // 사거리 1: 이동 전·후 어느 쪽에서든 플레이어와 인접하면 같은 턴에 공격(이동과 동시 공격).
            bool adjacentNow = Mathf.Abs(cur.x - player.x) + Mathf.Abs(cur.y - player.y) == 1;
            bool adjacentAfter = Mathf.Abs(nextHead.x - player.x) + Mathf.Abs(nextHead.y - player.y) == 1;
            if (adjacentNow || adjacentAfter)
            {
                // 절대 좌표로 공격 등록 — extra(SlideSnake)가 머리를 옮긴 뒤에도 정확히 플레이어 셀을 친다.
                PlanRangeAttack(self, board, new[] { player }, absolute: true);
            }

            if (canMove)
            {
                // 머리 이동 미리보기는 ComputeDeferredMovePreview 오버라이드가 채운다(몸통은 미표시).
                int capturedGid = gid;
                Vector2Int capturedHead = nextHead;
                PlanCustom("이동", (s, c) => SlideSnake(capturedGid, c, capturedHead));
            }
        }

        /// <summary>이동 미리보기 — 머리만 다음 이동 셀(plannedStep)을 표시한다.
        /// 뱀은 일반 지연 이동 슬롯 대신 PlanCustom(SlideSnake)으로 움직이므로, 기본
        /// ComputeDeferredMovePreview(HasDeferredMove==false → plannedMovePositions를 비우고 반환)가
        /// 미리보기를 지워버린다. 그래서 여기서 직접 채워 move sprite/행동 아이콘이 정상 표시되게 한다.</summary>
        public override void ComputeDeferredMovePreview(EnemyInstance self, BoardController board)
        {
            plannedMovePositions.Clear();
            plannedMoveSpritePositions.Clear();
            if (self.IsBound) return; // 속박: 이동 미리보기 표시 안 함 (공격 미리보기는 유지)

            int gid = GroupId(self);
            if (!chains.TryGetValue(gid, out var chain) || chain.Count == 0 || chain[0] != self) return;
            if (!states.TryGetValue(gid, out var st) || !st.willMove) return;
            if (!board.IsInBoard(st.plannedStep)) return;
            plannedMovePositions.Add(st.plannedStep);
        }

        /// <summary>추격 한 칸: 이미 플레이어와 인접하면 제자리(머리 위치) 유지(공격만).
        /// 아니면 갈 수 있는 인접 빈 칸 중 플레이어와 맨해튼 거리가 가장 짧아지는 칸으로(동률은 랜덤).
        /// 자기 몸·다른 유닛·플레이어 칸·벽은 IsCellFreeForEnemy/IsInBoard가 걸러낸다.</summary>
        private Vector2Int ComputeChaseStep(EnemyInstance head, BoardController board, Vector2Int player)
        {
            Vector2Int cur = head.GridPos;
            if (Mathf.Abs(cur.x - player.x) + Mathf.Abs(cur.y - player.y) == 1) return cur;

            var best = new List<Vector2Int>(4);
            int bestDist = int.MaxValue;
            foreach (var dir in AdjacentFour)
            {
                Vector2Int n = cur + dir;
                if (!board.IsInBoard(n)) continue;
                if (!board.IsCellFreeForEnemy(n)) continue;
                int d = Mathf.Abs(n.x - player.x) + Mathf.Abs(n.y - player.y);
                if (d < bestDist) { bestDist = d; best.Clear(); best.Add(n); }
                else if (d == bestDist) best.Add(n);
            }
            if (best.Count == 0) return cur;
            return best[Random.Range(0, best.Count)];
        }

        /// <summary>인접 4칸 중 갈 수 있는 빈 칸(보드 안·비점유)을 모아 그중 하나를 랜덤 선택.
        /// 플레이어 칸·다른 유닛·자기 몸은 IsCellFreeForEnemy가 걸러내므로 자기 몸으로 들어가거나
        /// 역주행하지 않는다. 갈 칸이 하나도 없으면 현재 위치(뱀 전체 정지).</summary>
        private Vector2Int ComputeRandomHeadStep(EnemyInstance head, BoardController board)
        {
            Vector2Int cur = head.GridPos;
            var free = new List<Vector2Int>(4);
            foreach (var dir in AdjacentFour)
            {
                Vector2Int n = cur + dir;
                if (!board.IsInBoard(n)) continue;
                if (!board.IsCellFreeForEnemy(n)) continue;
                free.Add(n);
            }
            if (free.Count == 0) return cur;
            return free[Random.Range(0, free.Count)];
        }

        /// <summary>list[idx] 세그먼트를 이어붙일 빈 칸을 찾는다. 직전 세그먼트의 상하좌우 1칸 중
        /// (가능하면 몸통이 펴지는 직선 방향을 우선) 빈 칸을 고른다. 직전 세그먼트가 꽉 막혀 있으면
        /// 앞 세그먼트들을 차례로 거슬러 올라가며 빈 인접 칸을 탐색. 끝내 없으면 null.</summary>
        private Vector2Int? FindAttachCell(List<EnemyInstance> list, int idx, BoardController board)
        {
            for (int b = idx - 1; b >= 0; b--)
            {
                EnemyInstance anchor = list[b];
                if (anchor == null) continue;
                Vector2Int ap = anchor.GridPos;

                // 직선 우선: 앞 두 세그먼트의 진행 방향으로 한 칸 더 뻗기.
                if (b >= 1 && list[b - 1] != null)
                {
                    Vector2Int straight = ap + (ap - list[b - 1].GridPos);
                    if (board.IsInBoard(straight) && board.IsCellFreeForEnemy(straight))
                        return straight;
                }

                var free = new List<Vector2Int>(4);
                foreach (var dir in AdjacentFour)
                {
                    Vector2Int n = ap + dir;
                    if (!board.IsInBoard(n)) continue;
                    if (!board.IsCellFreeForEnemy(n)) continue;
                    free.Add(n);
                }
                if (free.Count > 0) return free[Random.Range(0, free.Count)];
            }
            return null;
        }

        /// <summary>한 그룹의 체인 전체를 1칸 슬라이드. 각 세그먼트의 새 위치(앞 세그먼트의 이동 전 위치)를
        /// 먼저 캡처한 뒤 머리→꼬리 순으로 TryMoveEnemy. 머리가 먼저 비우면 그 자리에 뒤따르는 세그먼트가
        /// 차례로 안착해 빈 칸 충돌이 없다.</summary>
        private static void SlideSnake(int gid, BoardController board, Vector2Int newHeadPos)
        {
            if (!chains.TryGetValue(gid, out var chain)) return;

            // 1) 살아있는 세그먼트만 모은다(인덱스 0 = 머리, 마지막 = 꼬리).
            var alive = new List<EnemyInstance>();
            foreach (var seg in chain)
                if (seg != null && seg.CurrentHP > 0) alive.Add(seg);
            if (alive.Count == 0) return;
            if (newHeadPos == alive[0].GridPos) return;

            // 2) 슬라이드 전에 모든 세그먼트의 현재 좌표를 스냅샷.
            //    뱀 게임 규칙: 머리는 newHeadPos로, 몸통[i]는 몸통[i-1]의 옛 좌표로 이동.
            //    꼬리(alive[last])의 옛 좌표는 비워지는 칸이 된다.
            var oldPositions = new List<Vector2Int>(alive.Count);
            for (int i = 0; i < alive.Count; i++)
                oldPositions.Add(alive[i].GridPos);

            // 3) 머리부터 이동 — 머리가 새 좌표로 비켜야 그 옛 좌표를 몸통[0]이 차지할 수 있다.
            //    머리가 막혀(점유/보드 밖) 못 가면 슬라이드 자체를 포기(뱀 전체 정지).
            //    그렇지 않으면 몸통이 머리 옛 좌표(아직 머리가 있는 칸)로 들어가지 못해 체인이 끊긴다.
            if (!board.TryMoveEnemy(alive[0], newHeadPos)) return;

            // 4) 몸통[i] → 몸통[i-1]의 옛 좌표(머리→꼬리 순). "끌려가는" 이동이므로
            //    IsBound(속박) 체크를 우회(forced=true). 순서대로 처리하면 각 단계에서
            //    목적 칸이 바로 직전 이동으로 비어있어 점유 충돌이 발생하지 않는다.
            for (int i = 1; i < alive.Count; i++)
                board.TryMoveEnemy(alive[i], oldPositions[i - 1], forced: true);
        }
    }

    /// <summary>
    /// 나이트 (11910): 체스 나이트처럼 점프 이동, 인접하면 공격.
    ///   - 플레이어가 AdjacentFour(상하좌우 1칸) 안에 있으면 사거리 1 공격.
    ///   - 아니면 8가지 나이트 점프 좌표 중 플레이어 인접(AdjacentFour) 위치로 우선 점프.
    ///     그런 점프가 없으면 플레이어와 가장 가까워지는 점프를 선택.
    ///   - 모든 점프가 막혀있으면 대기.
    /// </summary>
    public class KnightBehavior : EnemyBehavior
    {
        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            if (IsPlayerInRange(self, board, AdjacentFour))
            {
                PlanTargetAttack(self, board, AttackTarget.Player);
                return;
            }

            Vector2Int playerPos = board.PlayerSpawnCell;
            Vector2Int dest = ChooseKnightJump(self, board, playerPos);
            if (dest != self.GridPos)
                PlanMoveTo(self, board, dest);
            else
                PlanWait("대기");
        }

        /// <summary>플레이어 인접 가능 점프를 우선 선택, 없으면 플레이어와 맨해튼 최단인 점프를 선택.</summary>
        private Vector2Int ChooseKnightJump(EnemyInstance self, BoardController board, Vector2Int player)
        {
            var reserved = GatherReservedCells(self, board);
            var vacating = GatherVacatingCells(self, board);

            Vector2Int best = self.GridPos;
            int bestDist = int.MaxValue;
            bool bestCanAttack = false;

            foreach (var jump in KnightJump)
            {
                Vector2Int candidate = self.GridPos + jump;
                if (!CanMoveInto(candidate, board, reserved, vacating)) continue;

                int dist = Mathf.Abs(candidate.x - player.x) + Mathf.Abs(candidate.y - player.y);
                bool canAttack = (dist == 1);

                if (canAttack && !bestCanAttack)
                {
                    best = candidate; bestDist = dist; bestCanAttack = true;
                }
                else if (canAttack == bestCanAttack && dist < bestDist)
                {
                    best = candidate; bestDist = dist;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// 룩 (11911): 체스 룩처럼 직선으로 돌진 공격. 이동 반경 = 공격 사거리 = 직선 무제한.
    ///   - 플레이어와 같은 행/열에 있으면 그 방향으로 돌진:
    ///       공격: self+dir*1 ~ player 좌표 모든 셀 (사거리 무제한, 절대 좌표)
    ///       이동: player - dir 셀(플레이어 바로 앞 한 칸)로 이동
    ///   - 그 외에는 룩 이동(4방향 직선 무제한)으로 플레이어와 같은 행/열인 칸으로 한 번에 이동.
    ///     도달 가능한 직선 칸 중 (1) 플레이어와 같은 행/열인 칸을 우선, 그 중 플레이어와
    ///     맨해튼 거리가 짧은 칸을 선택. 같은 행/열이 모두 막혀있으면 도달 가능한 칸 중
    ///     플레이어와 가장 가까워지는 칸으로 이동, 그것도 없으면 대기.
    /// 공격 슬롯과 기타(이동) 슬롯을 함께 등록 — 기타→공격 순으로 Phase 1에서 처리되어
    /// 룩이 먼저 정지 위치로 이동한 뒤 절대 좌표 공격이 발사됨.
    ///
    /// [Sprite 표시 — 돌진 시] path 셀(self+dir*1 ~ player-dir)은 move sprite로, player 셀만
    /// attack sprite로 표시. PlanRangeAttack은 데미지 계산용 절대 좌표 전체를 등록하지만
    /// GetAttackPreviewPositions/GetMoveSpritePositions를 override해 시각 표시만 분리.
    /// (보드 상태가 바뀔 때마다 RePlanAllEnemyTurns가 PlanTurn을 다시 호출하므로 player가
    ///  사거리에 들고 나는 것에 따라 attack/move 표시가 매번 다시 결정된다.)
    /// </summary>
    public class RookBehavior : EnemyBehavior
    {
        // 돌진 시점에 채워지는 시각 표시 상태 (GetAttackPreviewPositions / GetMoveSpritePositions override에서 사용).
        // 정렬 안 된 턴(룩 align 이동) 또는 대기 턴에는 비어있음 → 부모 구현으로 fallback.
        private readonly List<Vector2Int> _dashPathCells = new List<Vector2Int>();
        private Vector2Int? _dashAttackCell;
        private Vector2Int? _dashStopPos;

        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            _dashPathCells.Clear();
            _dashAttackCell = null;
            _dashStopPos = null;

            Vector2Int player = board.PlayerSpawnCell;
            Vector2Int diff = player - self.GridPos;

            bool sameRow = diff.y == 0 && diff.x != 0;
            bool sameCol = diff.x == 0 && diff.y != 0;
            if (sameRow || sameCol)
            {
                Vector2Int dir = new Vector2Int(
                    diff.x == 0 ? 0 : (diff.x > 0 ? 1 : -1),
                    diff.y == 0 ? 0 : (diff.y > 0 ? 1 : -1));
                int distance = Mathf.Abs(diff.x) + Mathf.Abs(diff.y);

                // 공격 — 자기 셀 다음 칸부터 플레이어 셀까지 일직선 모든 셀(절대 좌표).
                // 데미지는 모든 path 셀에 적용되지만, 시각 표시는 player 셀에만 attack sprite.
                var cells = new Vector2Int[distance];
                for (int i = 0; i < distance; i++)
                    cells[i] = self.GridPos + dir * (i + 1);
                PlanRangeAttack(self, board, cells, absolute: true);

                // 시각 표시 상태:
                //   path(중간 셀들) → move sprite
                //   player(마지막 셀) → attack sprite
                for (int i = 0; i < distance - 1; i++)
                    _dashPathCells.Add(cells[i]);
                _dashAttackCell = cells[distance - 1];

                // 이동 — 플레이어 한 칸 앞 (player - dir). 거리 1이면 이미 인접이라 이동 불필요.
                if (distance < 2) return;

                Vector2Int stopPos = player - dir;
                if (!board.IsInBoard(stopPos)) return;
                _dashStopPos = stopPos;

                PlanCustom("돌진", (s, c) => c.TryMoveEnemy(s, stopPos));
                return;
            }

            // 정렬 안 됨 — 룩 이동(직선 무제한)으로 플레이어와 같은 행/열 칸으로 이동.
            Vector2Int dest = FindBestRookAlignCell(self.GridPos, board, player);
            if (dest != self.GridPos)
                PlanMoveTo(self, board, dest);
            else
                PlanWait("대기");
        }

        /// <summary>4방향 직선으로 걸어가며 통과 가능한 칸들을 enumerate, 그 중
        /// (1) 플레이어와 같은 행/열인 칸을 우선, 그 중 플레이어와 맨해튼 거리가 가장 짧은 칸 선택.
        /// (2) 정렬 가능한 칸이 없으면 도달 칸 중 플레이어와 가장 가까운 칸.
        /// 어디로도 더 가까워질 수 없으면 origin 반환(이동 안 함).
        /// 직선 경로는 첫 점유 셀에서 끊긴다(룩은 점프 불가).</summary>
        private static Vector2Int FindBestRookAlignCell(Vector2Int origin, BoardController board, Vector2Int player)
        {
            bool foundAligned = false;
            Vector2Int bestAligned = origin;
            int bestAlignedDist = int.MaxValue;

            Vector2Int bestAny = origin;
            int bestAnyDist = Mathf.Abs(origin.x - player.x) + Mathf.Abs(origin.y - player.y);

            foreach (var dir in AdjacentFour)
            {
                Vector2Int cur = origin;
                while (true)
                {
                    Vector2Int nxt = cur + dir;
                    if (!board.IsInBoard(nxt)) break;
                    if (!board.IsCellFreeForEnemy(nxt)) break;
                    cur = nxt;

                    int d = Mathf.Abs(cur.x - player.x) + Mathf.Abs(cur.y - player.y);
                    bool isAligned = (cur.x == player.x) || (cur.y == player.y);

                    if (isAligned && (!foundAligned || d < bestAlignedDist))
                    {
                        bestAlignedDist = d;
                        bestAligned = cur;
                        foundAligned = true;
                    }
                    if (d < bestAnyDist)
                    {
                        bestAnyDist = d;
                        bestAny = cur;
                    }
                }
            }

            return foundAligned ? bestAligned : bestAny;
        }

        /// <summary>공격 미리보기 — 돌진 시 player 셀만 attack sprite. (path 셀들은 move sprite로 표시되므로 제외.)
        /// 돌진이 아닌 턴(정렬 안 됨/대기)에는 부모 구현으로 fallback. PlanRangeAttack이 등록한 절대 좌표
        /// 전체는 데미지 계산용으로만 사용되고, 시각 표시는 여기서 _dashAttackCell만 노출.</summary>
        public override List<Vector2Int> GetAttackPreviewPositions(EnemyInstance self)
        {
            plannedAttackPositions.Clear();
            if (_dashAttackCell.HasValue)
                plannedAttackPositions.Add(_dashAttackCell.Value);
            return plannedAttackPositions;
        }

        /// <summary>이동 sprite 미리보기 — 돌진 시 path 셀 전체(self+dir*1 ~ player-dir)에 move sprite 표시.
        /// 정렬 안 된 align 이동 턴에는 부모 구현(PlanMoveTo의 dest 1개) 사용.</summary>
        public override List<Vector2Int> GetMoveSpritePositions(EnemyInstance self)
        {
            if (_dashPathCells.Count > 0)
                return _dashPathCells;
            return base.GetMoveSpritePositions(self);
        }

        /// <summary>이동 step 카운트/다른 적의 예약(reserved)·비움(vacating) 계산용 — 돌진 시 실제로
        /// 안착할 stopPos 1개만 반환. path 셀들은 시각 표시일 뿐 룩이 점유하지 않는다.</summary>
        public override List<Vector2Int> GetMovePreviewPositions(EnemyInstance self)
        {
            if (_dashStopPos.HasValue)
            {
                plannedMovePositions.Clear();
                plannedMovePositions.Add(_dashStopPos.Value);
                return plannedMovePositions;
            }
            return base.GetMovePreviewPositions(self);
        }
    }

    /// <summary>
    /// 비숍 (11912): 체스 비숍처럼 대각선으로 사거리 무제한 공격.
    ///   - 플레이어와 대각선 정렬(|dx|==|dy|, dx!=0)이면 대각선 방향으로 self 다음 칸부터
    ///     플레이어 셀까지 모든 대각선 셀에 광역 공격.
    ///   - 정렬 안 되어 있으면 대각선 1칸 이동으로 정렬 차이(||dx|-|dy||)를 가장 줄이는
    ///     칸 선택. 동률이면 플레이어와 맨해튼 가까운 칸 선택. 모든 대각선이 막혀있으면 대기.
    /// </summary>
    public class BishopBehavior : EnemyBehavior
    {
        public override void PlanTurn(EnemyInstance self, BoardController board)
        {
            Vector2Int player = board.PlayerSpawnCell;
            Vector2Int diff = player - self.GridPos;

            if (diff.x != 0 && Mathf.Abs(diff.x) == Mathf.Abs(diff.y))
            {
                int steps = Mathf.Abs(diff.x);
                Vector2Int dir = new Vector2Int(diff.x > 0 ? 1 : -1, diff.y > 0 ? 1 : -1);
                var cells = new Vector2Int[steps];
                for (int i = 0; i < steps; i++)
                    cells[i] = self.GridPos + dir * (i + 1);
                PlanRangeAttack(self, board, cells, absolute: true);
                return;
            }

            Vector2Int dest = ChooseDiagonalAlignStep(self, board, player);
            if (dest != self.GridPos)
                PlanMoveTo(self, board, dest);
            else
                PlanWait("대기");
        }

        /// <summary>대각선 1칸 이동 후보 중 ||dx|-|dy|| 가장 작은 칸을 우선.
        /// 동률이면 플레이어 맨해튼 거리가 더 작은 쪽 선택. 후보가 없으면 self.GridPos.</summary>
        private Vector2Int ChooseDiagonalAlignStep(EnemyInstance self, BoardController board, Vector2Int player)
        {
            var reserved = GatherReservedCells(self, board);
            var vacating = GatherVacatingCells(self, board);

            Vector2Int best = self.GridPos;
            int bestAlign = AlignDiff(self.GridPos, player);
            int bestManhattan = Mathf.Abs(self.GridPos.x - player.x) + Mathf.Abs(self.GridPos.y - player.y);

            foreach (var d in DiagonalFour)
            {
                Vector2Int candidate = self.GridPos + d;
                if (!CanMoveInto(candidate, board, reserved, vacating)) continue;

                int align = AlignDiff(candidate, player);
                int manhattan = Mathf.Abs(candidate.x - player.x) + Mathf.Abs(candidate.y - player.y);

                if (align < bestAlign ||
                    (align == bestAlign && manhattan < bestManhattan))
                {
                    bestAlign = align;
                    bestManhattan = manhattan;
                    best = candidate;
                }
            }
            return best;
        }

        private static int AlignDiff(Vector2Int from, Vector2Int target)
        {
            int dx = Mathf.Abs(target.x - from.x);
            int dy = Mathf.Abs(target.y - from.y);
            return Mathf.Abs(dx - dy);
        }
    }
}
