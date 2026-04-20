using DeckRoguelike.UI;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 파워 카드로 등록되는 전투 지속 효과의 베이스 클래스.
    /// 카드가 플레이될 때 CombatController.RegisterPower()로 등록되고
    /// 전투가 끝나면 자동으로 해제됩니다.
    /// </summary>
    public abstract class CombatPowerEffect
    {
        public int Value { get; protected set; }

        protected CombatPowerEffect(int value) { Value = value; }

        /// <summary>플레이어 턴 시작 시 호출</summary>
        public virtual void OnTurnStart(CombatController combat) {}

        /// <summary>적이 사망할 때마다 호출</summary>
        public virtual void OnEnemyKilled(CombatController combat) {}

        /// <summary>플레이어가 HP를 잃을 때마다 호출 (카드 효과 포함)</summary>
        public virtual void OnPlayerLostHp(CombatController combat, int amount) {}

        /// <summary>카드가 소멸(Exhaust)될 때마다 호출</summary>
        public virtual void OnCardExhausted(CombatController combat) {}
    }

    // ── 구체 구현 ────────────────────────────────────────────────────────────

    /// <summary>매 턴 시작 시 힘 +Value</summary>
    public class EveryTurnStrengthPower : CombatPowerEffect
    {
        public EveryTurnStrengthPower(int value) : base(value) {}
        public override void OnTurnStart(CombatController c) => c.AddStrength(Value);
    }

    /// <summary>매 턴 시작 시 에너지 +Value</summary>
    public class EveryTurnEnergyPower : CombatPowerEffect
    {
        public EveryTurnEnergyPower(int value) : base(value) {}
        public override void OnTurnStart(CombatController c) => c.AddEnergy(Value);
    }

    /// <summary>적 처치 시 힘 +Value</summary>
    public class EveryKillStrengthPower : CombatPowerEffect
    {
        public EveryKillStrengthPower(int value) : base(value) {}
        public override void OnEnemyKilled(CombatController c) => c.AddStrength(Value);
    }

    /// <summary>HP를 잃을 때마다 힘 +Value</summary>
    public class EveryLoseHpStrengthPower : CombatPowerEffect
    {
        public EveryLoseHpStrengthPower(int value) : base(value) {}
        public override void OnPlayerLostHp(CombatController c, int amount) => c.AddStrength(Value);
    }

    /// <summary>카드 소멸 시 힘 +Value</summary>
    public class EveryExhaustsStrengthPower : CombatPowerEffect
    {
        public EveryExhaustsStrengthPower(int value) : base(value) {}
        public override void OnCardExhausted(CombatController c) => c.AddStrength(Value);
    }
}
