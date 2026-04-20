using UnityEngine;
using DeckRoguelike.UI;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 기본 제공 아군 행동 클래스들을 AllyBehaviorRegistry에 등록합니다.
    /// CombatController.Start()에서 AllyBehaviorLibrary.RegisterAll()을 호출하세요.
    ///
    /// 새 아군을 추가하려면:
    ///   1. 아래에 AllyBehavior 상속 클래스를 작성합니다.
    ///   2. RegisterAll()에 AllyData.allyCode와 함께 Register 한 줄을 추가합니다.
    /// </summary>
    public static class AllyBehaviorLibrary
    {
        public static void RegisterAll()
        {
            AllyBehaviorRegistry.Register(30100, () => new ShieldSoldierBehavior());
            AllyBehaviorRegistry.Register(30200, () => new ArcherBehavior());
            AllyBehaviorRegistry.Register(30300, () => new HealerBehavior());
            AllyBehaviorRegistry.Register(30400, () => new BerserkerBehavior());
        }
    }

    // ── 행동 구현 ─────────────────────────────────────────────────────

    /// <summary>
    /// 방패 병사: 인접한 적에게 공격. 피격 시 Block 획득 (OnDamaged 콜백 필요).
    /// </summary>
    public class ShieldSoldierBehavior : AllyBehavior
    {
        public override void ExecuteTurn(AllyInstance self, CombatController combat)
        {
            // TODO: 인접 적 공격
            Debug.Log($"[ShieldSoldierBehavior] 방패 병사 행동 @ {self.GridPos}");
        }

        public override string GetIntentText(AllyInstance self) => "공격";
    }

    /// <summary>
    /// 궁수: 가장 먼 적을 원거리 공격. 장애물 무시.
    /// </summary>
    public class ArcherBehavior : AllyBehavior
    {
        public override void ExecuteTurn(AllyInstance self, CombatController combat)
        {
            // TODO: 가장 먼 적 탐색 → 원거리 공격
            Debug.Log($"[ArcherBehavior] 궁수 행동 @ {self.GridPos}");
        }

        public override string GetIntentText(AllyInstance self) => "원거리 공격";
    }

    /// <summary>
    /// 치유사: HP가 가장 낮은 아군(또는 플레이어)을 회복.
    /// </summary>
    public class HealerBehavior : AllyBehavior
    {
        public override void ExecuteTurn(AllyInstance self, CombatController combat)
        {
            // TODO: 가장 HP가 낮은 아군 탐색 → 회복
            Debug.Log($"[HealerBehavior] 치유사 행동 @ {self.GridPos}");
        }

        public override string GetIntentText(AllyInstance self) => "회복";
    }

    /// <summary>
    /// 광전사: 자신 HP가 낮을수록 공격력 증가 (HP 비율에 반비례).
    /// </summary>
    public class BerserkerBehavior : AllyBehavior
    {
        public override void ExecuteTurn(AllyInstance self, CombatController combat)
        {
            float hpRatio   = (float)self.CurrentHP / self.MaxHP;
            int   bonusDmg  = Mathf.RoundToInt(self.Damage * (1f - hpRatio));
            int   totalDmg  = self.Damage + bonusDmg;

            // TODO: 인접 적에게 totalDmg 공격
            Debug.Log($"[BerserkerBehavior] 광전사 행동 @ {self.GridPos} | 공격력 {totalDmg} (보너스 +{bonusDmg})");
        }

        public override string GetIntentText(AllyInstance self) => "분노 공격";
    }
}
