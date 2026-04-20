using DeckRoguelike.Cards;
using DeckRoguelike.Combat;
using DeckRoguelike.Core;
using DeckRoguelike.UI;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine;

namespace DeckRoguelike.Relic
{
    /// <summary>
    /// RelicDatabase ScriptableObject를 로드하여 RelicRegistry에 등록합니다.
    /// RelicDatabase는 Editor의 Tools > Relic Data > Import from CSV 로 생성합니다.
    ///
    /// 새 유물을 추가하려면:
    ///   1. Assets/Editor/RelicDataTemplate.csv 에 행을 추가합니다.
    ///   2. Tools > Relic Data > Import from CSV 를 실행합니다.
    ///   3. 이 파일 하단에 RelicEffect 상속 클래스를 작성합니다.
    ///   4. effectFactories 딕셔너리에 항목을 추가합니다.
    /// </summary>
    public static class RelicLibrary
    {
        // relicCode → RelicEffect 생성 팩토리
        private static readonly Dictionary<int, System.Func<RelicEffect>> effectFactories =
            new Dictionary<int, System.Func<RelicEffect>>
            {
                // ── Common ──────────────────────────────────────────────────
                { 101, () => new BloodVialRelic()      },  // 피의 약병 : 전투 시작 HP +4
                { 102, () => new AncientShieldRelic()  },  // 고대의 방패: 첫 피해 1회 무효
                { 103, () => new BurningHeartRelic()   },  // 불타는 심장: 전투 시작 최대 에너지 +1
                { 104, () => new SoulGemRelic()        },  // 영혼의 보석: 적 처치 시 HP +1
                { 105, () => new StrengthRelic()       },  // 힘           : 힘 +1 (전투 시작마다)
                { 106, () => new MaxHPRelic()          },  // 최대채력     : 최대 HP +7 (획득 시)
                { 107, () => new MoveRelic()           },  // 이동         : 3번째 이동 시 카드 비용 -1
                // ── Uncommon ─────────────────────────────────────────────
                { 201, () => new CigaretteRelic()      },  // 담배         : 휴식 장소에서 제거 가능
                { 202, () => new ShovelRelic()         },  // 삽           : 휴식 장소에서 발굴 가능
                { 203, () => new DarkCrownRelic()      },  // 어둠의 왕관  : 매 턴 에너지 +1, HP -1
                // ── Rare ─────────────────────────────────────────────────
                { 301, () => new ArtisanHammerRelic()  },  // 장인의 망치  : 휴식 시 카드 2장 강화
                { 302, () => new LuxuryPillowRelic()   },  // 고급 베개    : 휴식 회복 +30%
                { 303, () => new ReflectRelic()        },  // 반사         : 받은 피해를 공격한 적에게 반사
                // ── Boss ─────────────────────────────────────────────────
                { 901, () => new SoulOrbRelic()        },  // 영혼이 담긴 구슬: 기본 에너지 +1
                { 902, () => new SoulCardRelic()       },  // 영혼이 담긴 카드: 매 턴 드로우 +1
            };

        /// <summary>
        /// RelicDatabase를 로드하여 RelicRegistry에 등록합니다. 중복 호출해도 안전합니다.
        /// </summary>
        public static void RegisterAll()
        {
            RelicRegistry.Clear();

            var db = Addressables.LoadAssetAsync<RelicDatabase>("Data/RelicDatabase").WaitForCompletion();
            if (db == null)
            {
                Debug.LogError("[RelicLibrary] RelicDatabase.asset 을 찾을 수 없습니다. Addressables 주소를 확인하세요.");
                return;
            }

            int count = 0;
            foreach (var data in db.relics)
            {
                if (data == null || data.relicCode == 0) continue;

                if (!effectFactories.TryGetValue(data.relicCode, out var factory))
                {
                    Debug.LogWarning($"[RelicLibrary] 코드 {data.relicCode} 에 대응하는 RelicEffect 구현이 없습니다. " +
                                     "effectFactories에 추가하세요.");
                    continue;
                }

                RelicRegistry.Register(data, factory);
                count++;
            }

            Debug.Log($"[RelicLibrary] RelicDatabase에서 유물 {count}개 등록 완료");
        }
    }

    // ── 유물 효과 구현 ──────────────────────────────────────────────────────────

    // ── Common ─────────────────────────────────────────────────────────────────

    /// <summary>피의 약병 (101): 전투 시작 시 HP +4 회복</summary>
    public class BloodVialRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            GameManager.Instance?.Heal(4);
            Debug.Log("[BloodVialRelic] 전투 시작: HP +4");
        }
    }

    /// <summary>고대의 방패 (102): 전투 내 첫 피해 1회 무효. 전투마다 리셋.</summary>
    public class AncientShieldRelic : RelicEffect
    {
        private bool _shieldActive;

        public override void OnCombatStart(RelicCombatContext ctx)
        {
            _shieldActive = true;
            Debug.Log("[AncientShieldRelic] 방패 활성화");
        }

        public override void OnPlayerDamaged(RelicCombatContext ctx, ref int damage)
        {
            if (!_shieldActive) return;
            _shieldActive = false;
            damage = 0;
            Debug.Log("[AncientShieldRelic] 피해 무효!");
        }
    }

    /// <summary>불타는 심장 (103): 전투 시작 시 최대 에너지 +1</summary>
    public class BurningHeartRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx.Combat.AddMaxEnergy(1);
            Debug.Log("[BurningHeartRelic] 전투 시작: 최대 에너지 +1");
        }
    }

    /// <summary>영혼의 보석 (104): 적 처치 시 HP +1 회복</summary>
    public class SoulGemRelic : RelicEffect
    {
        public override void OnEnemyKilled(RelicCombatContext ctx, EnemyInstance enemy)
        {
            GameManager.Instance?.Heal(1);
            Debug.Log($"[SoulGemRelic] {enemy.Name} 처치: HP +1");
        }
    }

    /// <summary>힘 (105): 전투 시작 시 힘 +1</summary>
    public class StrengthRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx.Combat.AddStrength(1);
            Debug.Log("[StrengthRelic] 전투 시작: 힘 +1");
        }
    }

    /// <summary>최대채력 (106): 획득 시 최대 HP +7</summary>
    public class MaxHPRelic : RelicEffect
    {
        public override void OnRelicObtained(GameManager gm)
        {
            gm.ModifyMaxHP(7);
            Debug.Log("[MaxHPRelic] 획득: 최대 HP +7");
        }
    }

    /// <summary>
    /// 이동 (107): 3번째 이동마다 이번 턴 카드 비용 -1.
    /// 아이콘 위에 "X/3" 카운터를 표시합니다.
    /// </summary>
    public class MoveRelic : RelicEffect
    {
        private int _moveCounter = 0;

        public override string CounterText => $"{_moveCounter % 3}/3";

        public override void OnCombatStart(RelicCombatContext ctx)
        {
            _moveCounter = 0;
            NotifyCounterChanged();
        }

        public override void OnPlayerMoved(RelicCombatContext ctx, int totalMoveCount)
        {
            _moveCounter++;
            NotifyCounterChanged();

            if (_moveCounter % 3 == 0)
            {
                ctx.Combat.AddCardCostReduction(1);
                Debug.Log($"[MoveRelic] {_moveCounter}번째 이동 → 카드 비용 -1");
            }
        }
    }

    // ── Uncommon ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 담배 (201): 휴식 장소에서 카드 제거 버튼을 활성화합니다.
    /// (CombatController.SpawnRestButtons 에서 HasRelic(201) 체크로 처리)
    /// </summary>
    public class CigaretteRelic : RelicEffect { }

    /// <summary>
    /// 삽 (202): 휴식 장소에서 발굴 버튼을 활성화합니다.
    /// (CombatController.SpawnRestButtons 에서 HasRelic(202) 체크로 처리)
    /// </summary>
    public class ShovelRelic : RelicEffect { }

    /// <summary>어둠의 왕관 (203): 매 턴 시작 에너지 +1, HP -1</summary>
    public class DarkCrownRelic : RelicEffect
    {
        public override void OnPlayerTurnStart(RelicCombatContext ctx)
        {
            ctx.Combat.AddEnergy(1);
            GameManager.Instance?.ModifyHP(-1);
            Debug.Log("[DarkCrownRelic] 턴 시작: 에너지 +1, HP -1");
        }
    }

    // ── Rare ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// 장인의 망치 (301): 휴식 장소에서 카드 2장을 강화할 수 있습니다.
    /// (CombatController.OnUpgradeClicked 에서 HasRelic(301) 체크로 처리)
    /// </summary>
    public class ArtisanHammerRelic : RelicEffect { }

    /// <summary>고급 베개 (302): 휴식 시 회복량 +30%p</summary>
    public class LuxuryPillowRelic : RelicEffect
    {
        public override void OnBeforeRestHeal(ref float healPercent)
        {
            healPercent += 0.3f;
            Debug.Log("[LuxuryPillowRelic] 휴식 회복 +30%p");
        }
    }

    /// <summary>반사 (303): 공격받은 만큼 피해를 공격한 적에게 되돌려줍니다.</summary>
    public class ReflectRelic : RelicEffect
    {
        public override void OnPlayerDamagedBy(RelicCombatContext ctx, int damage, EnemyInstance attacker)
        {
            if (attacker == null || attacker.CurrentHP <= 0) return;
            ctx.Combat.DamageEnemy(attacker, damage);
            Debug.Log($"[ReflectRelic] {damage} 피해 반사 → {attacker.Name}");
        }
    }

    // ── Boss ───────────────────────────────────────────────────────────────────

    /// <summary>영혼이 담긴 구슬 (901): 획득 시 기본 에너지 영구 +1</summary>
    public class SoulOrbRelic : RelicEffect
    {
        public override void OnRelicObtained(GameManager gm)
        {
            gm.ModifyBaseEnergy(1);
            Debug.Log("[SoulOrbRelic] 획득: 기본 에너지 +1");
        }
    }

    /// <summary>영혼이 담긴 카드 (902): 전투 중 매 턴 카드 1장 추가 드로우</summary>
    public class SoulCardRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx.Combat.AddBonusHandSize(1);
            Debug.Log("[SoulCardRelic] 전투 시작: 턴 드로우 +1");
        }
    }
}
