using UnityEngine;
using DeckRoguelike.Core;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 기본 제공 커스텀 효과들을 CustomEffectRegistry에 등록합니다.
    /// CombatController.Start()에서 EffectLibrary.RegisterAll()을 호출하세요.
    ///
    /// 새 효과를 추가하려면 이 클래스에 메서드를 추가하고 RegisterAll에 등록하면 됩니다.
    /// </summary>
    public static class CardEffectLibrary
    {
        public static void RegisterAll()
        {
            // ── 즉발 효과 ──────────────────────────────────────────
            CardEffectRegistry.Register("Strength",       GainStrength);
            CardEffectRegistry.Register("gain_dexterity", GainDexterity);
            CardEffectRegistry.Register("Hp",             HealHp);
            CardEffectRegistry.Register("Gold",           GainGold);
            CardEffectRegistry.Register("area_damage",    AreaDamage);
            CardEffectRegistry.Register("behindEnemy_Move", BehindEnemyMove);

            // ── 파워 카드: 전투 지속 효과 ──────────────────────────
            CardEffectRegistry.Register("everyturn_Strength",    EveryturnStrength);
            CardEffectRegistry.Register("everyturn_Energy",      EveryturnEnergy);
            CardEffectRegistry.Register("everyKill_Strength",    EveryKillStrength);
            CardEffectRegistry.Register("everyloseHp_Strength",  EveryLoseHpStrength);
            CardEffectRegistry.Register("everyExhausts_Strength", EveryExhaustsStrength);
            CardEffectRegistry.Register("no_loseHp",             NoLoseHp);
            CardEffectRegistry.Register("no_Exhausts",           NoExhausts);

            // ── 연속 공격 ─────────────────────────────────────────────────
            CardEffectRegistry.Register("double_Damage",     DoubleDamage);
            CardEffectRegistry.Register("alldiscard_Damage", AlldiscardDamage);
            CardEffectRegistry.Register("x_Damage",          XDamage);

            // ── 미구현 효과 추가 ──────────────────────────────────────────
            CardEffectRegistry.Register("Exhausts",          ExhaustHandCard);
            CardEffectRegistry.Register("pushEnmemy",         PushEnemy);
            CardEffectRegistry.Register("thisturn_Strenght",  ThisTurnStrength);
            CardEffectRegistry.Register("multiple_Strength",  MultiplyStrength);
            CardEffectRegistry.Register("multiple_attack",    MultiplyAttack);
        }

        // ── 즉발 효과 구현 ───────────────────────────────────────────────────

        /// <summary>힘 +value</summary>
        private static void GainStrength(CardEffectContext ctx)
        {
            ctx.Combat.AddStrength(ctx.Value);
        }

        /// <summary>민첩 +value</summary>
        private static void GainDexterity(CardEffectContext ctx)
        {
            ctx.Combat.AddDexterity(ctx.Value);
        }

        /// <summary>HP +value (음수면 손실, no_loseHp 플래그 체크)</summary>
        private static void HealHp(CardEffectContext ctx)
        {
            if (ctx.Value < 0 && ctx.Combat.NoCardHpLoss) return;

            GameManager.Instance?.Heal(ctx.Value);

            if (ctx.Value < 0)
                ctx.Combat.FirePowerOnPlayerLostHp(-ctx.Value);
        }

        /// <summary>골드 +value</summary>
        private static void GainGold(CardEffectContext ctx)
        {
            GameManager.Instance?.ModifyGold(ctx.Value);
        }

        /// <summary>선택한 셀을 중심으로 인접 8칸 + 선택 셀에 피해</summary>
        private static void AreaDamage(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;

            var center = ctx.SelectedPos.Value;
            int dmg = ctx.Value + ctx.Combat.PlayerStrength;

            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var pos = new UnityEngine.Vector2Int(center.x + dx, center.y + dy);
                if (!ctx.Combat.IsInBoard(pos)) continue;
                var enemy = ctx.Combat.GetEnemyAt(pos);
                if (enemy != null) ctx.Combat.DamageEnemy(enemy, dmg);
            }
        }

        /// <summary>선택한 적의 뒤 칸으로 순간이동 (적 방향 반대)</summary>
        private static void BehindEnemyMove(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;

            var enemyPos   = ctx.SelectedPos.Value;
            var playerPos  = ctx.Combat.PlayerSpawnCell;

            // 적이 플레이어 기준 오른쪽 → 적의 오른쪽(+1)이 뒤, 왼쪽이면 적의 왼쪽(-1)이 뒤
            int behindCol = (enemyPos.x >= playerPos.x)
                ? enemyPos.x + 1
                : enemyPos.x - 1;

            var target = new UnityEngine.Vector2Int(behindCol, enemyPos.y);
            ctx.Combat.TryMovePlayerTo(target);
        }

        // ── 파워 카드 효과 구현 ─────────────────────────────────────────────

        /// <summary>매 턴 시작 시 힘 +value</summary>
        private static void EveryturnStrength(CardEffectContext ctx)
        {
            ctx.Combat.RegisterPower(new EveryTurnStrengthPower(ctx.Value));
        }

        /// <summary>매 턴 시작 시 에너지 +value</summary>
        private static void EveryturnEnergy(CardEffectContext ctx)
        {
            ctx.Combat.RegisterPower(new EveryTurnEnergyPower(ctx.Value));
        }

        /// <summary>적 처치 시 힘 +value</summary>
        private static void EveryKillStrength(CardEffectContext ctx)
        {
            ctx.Combat.RegisterPower(new EveryKillStrengthPower(ctx.Value));
        }

        /// <summary>HP를 잃을 때마다 힘 +value</summary>
        private static void EveryLoseHpStrength(CardEffectContext ctx)
        {
            ctx.Combat.RegisterPower(new EveryLoseHpStrengthPower(ctx.Value));
        }

        /// <summary>카드 소멸 시 힘 +value</summary>
        private static void EveryExhaustsStrength(CardEffectContext ctx)
        {
            ctx.Combat.RegisterPower(new EveryExhaustsStrengthPower(ctx.Value));
        }

        /// <summary>카드 효과로 HP를 잃지 않음</summary>
        private static void NoLoseHp(CardEffectContext ctx)
        {
            ctx.Combat.SetNoCardHpLoss(true);
        }

        /// <summary>카드가 소멸되지 않고 버림 더미로</summary>
        private static void NoExhausts(CardEffectContext ctx)
        {
            ctx.Combat.SetNoExhaust(true);
        }

        // ── 연속 공격 효과 구현 ─────────────────────────────────────────────────

        /// <summary>선택한 적에게 value 피해를 2번 줍니다 (0.15초 텀)</summary>
        private static void DoubleDamage(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Combat.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;
            int dmg = ctx.Value + ctx.Combat.PlayerStrength;
            ctx.Combat.StartSequentialDamage(enemy, dmg, 2);
        }

        /// <summary>손패를 한장씩 버리면서 버릴 때마다 value 피해를 줍니다</summary>
        private static void AlldiscardDamage(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Combat.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;
            int dmg = ctx.Value + ctx.Combat.PlayerStrength;
            ctx.Combat.StartDiscardAndDamageSequence(enemy, dmg);
        }

        /// <summary>X 에너지를 소모하고 value 피해를 X번 순차 적용합니다</summary>
        private static void XDamage(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Combat.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;
            int times = ctx.XValue > 0 ? ctx.XValue : 1;
            int dmg = ctx.Value + ctx.Combat.PlayerStrength;
            ctx.Combat.StartSequentialDamage(enemy, dmg, times);
        }

        // ── 추가 즉발/파워 효과 구현 ─────────────────────────────────────────────

        /// <summary>손패에서 무작위로 value장을 소멸시킵니다</summary>
        private static void ExhaustHandCard(CardEffectContext ctx)
        {
            ctx.Combat.ExhaustRandomHandCard(ctx.Value > 0 ? ctx.Value : 1);
        }

        /// <summary>
        /// selectedPos에 있는 적을 이동 방향으로 1칸 밀고, 플레이어를 그 자리로 이동합니다.
        /// Move 효과가 적 때문에 막혔을 때 실제 밀기 + 이동을 처리합니다.
        /// </summary>
        private static void PushEnemy(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;

            var targetPos = ctx.SelectedPos.Value;
            var playerPos = ctx.Combat.PlayerSpawnCell;

            int dx = targetPos.x - playerPos.x;
            int dy = targetPos.y - playerPos.y;
            int stepX = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
            int stepY = dy == 0 ? 0 : (dy > 0 ? 1 : -1);

            var enemy = ctx.Combat.GetEnemyAt(targetPos);
            if (enemy != null)
                ctx.Combat.TryMoveEnemy(enemy, new UnityEngine.Vector2Int(targetPos.x + stepX, targetPos.y + stepY));

            ctx.Combat.TryMovePlayerTo(targetPos);
        }

        /// <summary>이번 턴에만 힘 +value, 턴 종료 시 자동 회수</summary>
        private static void ThisTurnStrength(CardEffectContext ctx)
        {
            ctx.Combat.AddTempStrength(ctx.Value);
        }

        /// <summary>현재 힘을 value배로 만듭니다 (한계돌파)</summary>
        private static void MultiplyStrength(CardEffectContext ctx)
        {
            int current = ctx.Combat.PlayerStrength;
            if (current > 0)
                ctx.Combat.AddStrength(current * (ctx.Value - 1));
        }

        /// <summary>이번 턴 가하는 피해와 받는 피해를 value배로 만듭니다 (최후의 공격)</summary>
        private static void MultiplyAttack(CardEffectContext ctx)
        {
            ctx.Combat.SetTurnDamageMultipliers(ctx.Value, ctx.Value);
        }
    }
}
