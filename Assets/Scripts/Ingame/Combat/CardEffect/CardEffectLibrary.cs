using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DeckRoguelike.Cards;
using DeckRoguelike.Core;
using DeckRoguelike.UI;

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
            CardEffectRegistry.Register("Hp",             HealHp);
            CardEffectRegistry.Register("Gold",           GainGold);
            CardEffectRegistry.Register("area_damage",    AreaDamage);
            CardEffectRegistry.Register("All_Damage",     AllDamage);

            // ── 파워 카드: 전투 지속 효과 ──────────────────────────
            CardEffectRegistry.Register("everyturn_Strength",         EveryturnStrength);
            CardEffectRegistry.Register("everyKill_Strength",         EveryKillStrength);
            CardEffectRegistry.Register("everyloseHp_Strength",       EveryLoseHpStrength);
            CardEffectRegistry.Register("everyExhausts_Strength",     EveryExhaustsStrength);
            CardEffectRegistry.Register("Dameged_moreDamage",         DamegedMoreDamage);
            CardEffectRegistry.Register("everyturn_createknife",      EveryturnCreateKnife);
            CardEffectRegistry.Register("everylosehp_AllrangeDamage", EveryLoseHpAllrangeDamage);
            CardEffectRegistry.Register("everyloseHp_Draw",           EveryLoseHpDraw);
            CardEffectRegistry.Register("ExhaustsCard_Strength",      ExhaustsCardStrength);
            CardEffectRegistry.Register("ExhaustsCard_Draw",          ExhaustsCardDraw);
            CardEffectRegistry.Register("MoveCard_Strength",          MoveCardStrength);
            CardEffectRegistry.Register("PowerCard_Strength",         PowerCardStrength);

            // ── 연속 공격 ─────────────────────────────────────────────────
            CardEffectRegistry.Register("double_Damage",      DoubleDamage);
            CardEffectRegistry.Register("4times_Damage",      FourTimesDamage);
            CardEffectRegistry.Register("5times_Damage",      FiveTimesDamage);
            CardEffectRegistry.Register("6times_Damage",      SixTimesDamage);
            CardEffectRegistry.Register("allExhausts_Damage", AllExhaustsDamage);
            CardEffectRegistry.Register("Random_Damage",      RandomDamage);
            CardEffectRegistry.Register("Damage_5RandomDamage", DamageThen5Random);
            CardEffectRegistry.Register("Untilkill_losehp",   UntilKillLoseHp);

            // ── 이동/적 제어 효과 ─────────────────────────────────────────
            CardEffectRegistry.Register("pushEnmemy",             PushEnemy);
            CardEffectRegistry.Register("losehp_Damage",          LoseHpDamage);
            CardEffectRegistry.Register("losehp_AllDamage",       LoseHpAllDamage);
            CardEffectRegistry.Register("MovebehindEnemy_Damage", MoveBehindEnemyDamage);
            CardEffectRegistry.Register("Movediagonal_Damage",    MoveDiagonalDamage);
            CardEffectRegistry.Register("Allrangefear",           AllRangeFear);
            CardEffectRegistry.Register("All_bondage",            AllBondage);
            CardEffectRegistry.Register("All_chill",              AllChill);
            CardEffectRegistry.Register("teleportation",          Teleportation);
            CardEffectRegistry.Register("Change_Move",            ChangeMove);
            CardEffectRegistry.Register("endturn_ExhaustsMove",   EndTurnExhaustsMove);

            // ── 처치 보상 (valueRaw "X.Y" 사용) ───────────────────────────
            CardEffectRegistry.Register("Damage_killedGold",   DamageKilledGold);
            CardEffectRegistry.Register("Damage_killedHp",     DamageKilledHp);
            CardEffectRegistry.Register("Damage_killedfullHp", DamageKilledFullHp);
            CardEffectRegistry.Register("Damage_killedMove",   DamageKilledMove);

            // ── 손패 진입 시 카드 변환 (드로우 훅에서 처리되며, 효과 적용 시점엔 NoOp) ──
            CardEffectRegistry.Register("Random_AttackCard", NoOp);
            CardEffectRegistry.Register("Random_MoveCard",   NoOp);
            CardEffectRegistry.Register("Random_PowerCard",  NoOp);

            // ── 카드 강화 (전투 종료 시 자동 복원) ─────────────────────────
            CardEffectRegistry.Register("Card_Upgrade", CardUpgrade);

            // ── 기타 효과 ─────────────────────────────────────────────────
            CardEffectRegistry.Register("random_weapon",     RandomWeapon);
            CardEffectRegistry.Register("thisturn_Strength", ThisTurnStrength);
            CardEffectRegistry.Register("thisturn_Strenght", ThisTurnStrength); // 과거 오타 호환
            CardEffectRegistry.Register("multiple_Strength", MultiplyStrength);
            CardEffectRegistry.Register("multiple_attack",   MultiplyAttack);

            // ── 보상 카드 (전투 승리 후 드로우) ──────────────────────────
            CardEffectRegistry.Register("Reward_Gold",  RewardGold);
            CardEffectRegistry.Register("Reward_Relic", RewardRelic);
            CardEffectRegistry.Register("Reward_Item",  RewardItem);
            CardEffectRegistry.Register("Reward_Card",  RewardCard);
            CardEffectRegistry.Register("Reward_Skip",  RewardSkip);
            CardEffectRegistry.Register("chooserelic",  ChooseRelic);   // 60010 보스 유물 선택 (3장 중 1장)
            CardEffectRegistry.Register("nextstage",    NextStage);     // 60011 다음 스테이지(Act +1) 이동
            CardEffectRegistry.Register("combat_restart", CombatRestart); // 60008 전투재시작 (패배 후 부활)
            CardEffectRegistry.Register("mainmenu",       ReturnMainMenu); // 60009 메인메뉴로 복귀 (패배 후 종료)

            // ── 추가 효과 ───────────────────────────────────────────────
            CardEffectRegistry.Register("Damage_killeddamage", DamageKilledDamage);
            CardEffectRegistry.Register("ataacked_Damage",     AttackedDamage);
            CardEffectRegistry.Register("3range_Damage",       ThreeRangeDamage);
            CardEffectRegistry.Register("create_DamageCard",   CreateDamageCard);
            CardEffectRegistry.Register("reflection_Damage",   ReflectionDamage);
            CardEffectRegistry.Register("BehindEnemy_Move",    BehindEnemyMove);
            CardEffectRegistry.Register("Hook",                Hook);
            CardEffectRegistry.Register("Hook_Damage",         HookDamage);
            CardEffectRegistry.Register("nextuse_copyCard",    NextUseCopyCard);
            CardEffectRegistry.Register("Range_Increase",      RangeIncrease);

            // ── Shoot 시스템 ────────────────────────────────────────────
            // 발사 카드는 customEffectId로 식별 (키워드 사용 X).
            // 데미지 = effect.value + ShootDamage + Strength, 사거리는 전역 ShootRange.
            CardEffectRegistry.Register("Shoot",               Shoot);
            CardEffectRegistry.Register("Allrange_Shoot",      AllrangeShoot);
            CardEffectRegistry.Register("6times_Shoot",        SixTimesShoot);
            CardEffectRegistry.Register("ShootRange",          ShootRange);
            CardEffectRegistry.Register("ShootDamage",         ShootDamage);
            CardEffectRegistry.Register("Dodge",               Dodge);
            CardEffectRegistry.Register("ReLoad",              ReLoad);
            CardEffectRegistry.Register("everyShoot_ricochet", EveryShootRicochet);
            CardEffectRegistry.Register("everyShoot_Strength", EveryShootStrength);
            CardEffectRegistry.Register("Shoot_MoveCard",      ShootMoveCard);
            CardEffectRegistry.Register("Shoot_ShootDamage",   ShootSnipe);
            CardEffectRegistry.Register("Shoot_areaattack",    ShootAreaAttack);
            CardEffectRegistry.Register("everytrap_Strength",  EveryTrapStrength);
            CardEffectRegistry.Register("everytrap_ShootCard", EveryTrapShootCard);
            CardEffectRegistry.Register("Allrange_trap_32300", AllrangeTrap);
            CardEffectRegistry.Register("Allrange_trap_32301", AllrangeTrap);
            CardEffectRegistry.Register("Allrange_trap_32122", AllrangeTrap);
            CardEffectRegistry.Register("Allrange_trap_32123", AllrangeTrap);

            // ── Zone 생성 (메이지: 용암지대/빙하지대) ─────────────────────
            // Generate_4214x → 용암 지대(32140/32141), Generate_4224x → 빙하 지대(32242/32243)
            CardEffectRegistry.Register("Generate_42140", GenerateLavaZone);
            CardEffectRegistry.Register("Generate_42141", GenerateLavaZone);
            CardEffectRegistry.Register("Generate_42240", GenerateGlacierZone);
            CardEffectRegistry.Register("Generate_42241", GenerateGlacierZone);

            // ── 휴식·제거 카드는 전용 흐름에서 직접 처리되므로 NoOp 등록 ──
            CardEffectRegistry.Register("Rest_Heal",    NoOp);
            CardEffectRegistry.Register("Rest_Upgrade", NoOp);
            CardEffectRegistry.Register("remove_Card",  NoOp);

            // ── 맵 이동 카드는 BoardController.HandleMapMoveCardPlay에서 직접 처리되므로 NoOp ──
            CardEffectRegistry.Register("map_Move", NoOp);
        }

        // ── 추가 효과 구현 ──────────────────────────────────────────────────

        /// <summary>피해 + 처치 시 카드 자체 데미지 영구 증가. valueRaw "X.Y" → X 데미지 / Y 증가량.</summary>
        private static void DamageKilledDamage(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;

            int baseDmg = ctx.Value;
            int bonus   = 0;
            if (!string.IsNullOrEmpty(ctx.ValueRaw))
            {
                var parts = ctx.ValueRaw.Split('.');
                if (parts.Length > 0 && int.TryParse(parts[0], out var d)) baseDmg = d;
                if (parts.Length > 1 && int.TryParse(parts[1], out var b)) bonus   = b;
            }

            int dmg = baseDmg + ctx.Board.PlayerStrength;
            int hpBefore = enemy.CurrentHP;
            ctx.Board.DamageEnemy(enemy, dmg);
            // 처치 시: 이 효과 자체의 시작 데미지(value)와 valueRaw의 baseDmg 부분을 영구적으로 bonus만큼 증가.
            // {D} 치환은 effect.value를 읽으므로 다음 사용 시 증가량이 반영됨.
            if (hpBefore > 0 && enemy.CurrentHP <= 0 && bonus > 0 && ctx.Effect != null)
            {
                ctx.Effect.value += bonus;
                ctx.Effect.valueRaw = $"{ctx.Effect.value}.{bonus}";
            }
        }

        /// <summary>가시갑옷: 공격을 당할 때마다 공격자에게 value 피해.</summary>
        private static void AttackedDamage(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new AttackedDamagePower(ctx.Value));

        /// <summary>이번 턴 동안 받는 공격을 반사합니다. valueRaw "X.Y" → X 피해 / Y 턴.</summary>
        private static void ReflectionDamage(CardEffectContext ctx)
        {
            int dmg   = ctx.Value;
            int turns = 1;
            if (!string.IsNullOrEmpty(ctx.ValueRaw))
            {
                var parts = ctx.ValueRaw.Split('.');
                if (parts.Length > 0 && int.TryParse(parts[0], out var d)) dmg   = d;
                if (parts.Length > 1 && int.TryParse(parts[1], out var t)) turns = t;
            }
            RegisterPowerWithIcon(ctx, new ReflectionDamagePower(dmg, turns));
        }

        /// <summary>휩쓸기: rangeOffsets를 시계방향으로 정렬한 뒤, 선택한 칸과 그 양옆(±1) 총 3칸에 피해.</summary>
        private static void ThreeRangeDamage(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            if (ctx.Effect?.rangeOffsets == null || ctx.Effect.rangeOffsets.Length == 0) return;

            var playerPos = ctx.Board.PlayerSpawnCell;
            var selectedOffset = ctx.SelectedPos.Value - playerPos;

            var sorted = ctx.Effect.rangeOffsets
                .Select(o => o.ToVector2Int())
                .OrderBy(v => Mathf.Atan2(v.x, v.y))
                .ToList();

            int idx = sorted.FindIndex(v => v == selectedOffset);
            if (idx < 0) return;

            int n = sorted.Count;
            int dmg = ctx.Value + ctx.Board.PlayerStrength;
            var cells = new List<Vector2Int>(3);
            for (int k = -1; k <= 1; k++)
            {
                var off = sorted[((idx + k) % n + n) % n];
                cells.Add(playerPos + off);
            }
            ctx.Board.DealDamageAtCells(cells, dmg);
        }

        /// <summary>무작위 액션 카드를 (value+1)장 손에 추가합니다.</summary>
        private static void CreateDamageCard(CardEffectContext ctx)
        {
            int count = Mathf.Max(1, ctx.Value + 1);
            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            var pool = CardRegistry.GetRewardPool(character)
                .Where(c => !c.IsUpgraded && c.CardTypeFromCode == CardType.Action)
                .ToList();
            if (pool.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                var picked = pool[Random.Range(0, pool.Count)];
                ctx.Board.AddCardToHandFree(picked);
            }
        }

        /// <summary>고기 갈고리: 선택한 적을 플레이어 정면 칸으로 끌어옵니다. 정면이 막혀있으면 카드 자체가 사용 불가.
        /// 끌어온 후 적이 반드시 플레이어를 마주보도록 facing을 강제 설정합니다 (수직 이동이라 TryMoveEnemy의 자동 facing이 어긋나는 경우 보정).</summary>
        private static void Hook(CardEffectContext ctx)
        {
            PullEnemyToFront(ctx);
        }

        /// <summary>고기 갈고리(22210): 적을 플레이어 정면으로 끌어당기고 {D}(value+힘) 피해를 줍니다.</summary>
        private static void HookDamage(CardEffectContext ctx)
        {
            var enemy = PullEnemyToFront(ctx);
            if (enemy == null) return;
            int dmg = ctx.Value + ctx.Board.PlayerStrength;
            ctx.Board.DamageEnemy(enemy, dmg);
        }

        /// <summary>선택한 적을 플레이어 정면 칸으로 끌어당기고(속박 무시) 플레이어를 마주보게 한다.
        /// 정면 칸이 보드 밖·막힘이거나 적이 이미 정면이면 이동만 생략하고, 선택된 적 인스턴스를 반환한다.
        /// (사용 가능 판정 IsCardPlayableNow가 정면 칸이 비어있을 때만 카드를 허용하므로, 실제 사용 시엔 항상 끌어당겨진다.)</summary>
        private static EnemyInstance PullEnemyToFront(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return null;
            var enemy = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return null;

            var playerPos = ctx.Board.PlayerSpawnCell;
            int dirX = ctx.Board.FacingRight ? 1 : -1;
            var front = new Vector2Int(playerPos.x + dirX, playerPos.y);
            if (ctx.Board.IsInBoard(front) && enemy.GridPos != front && ctx.Board.IsCellEmpty(front))
            {
                ctx.Board.TryMoveEnemy(enemy, front, forced: true); // 끌어당김 — 속박 무시
                // 끌어온 적이 항상 플레이어를 마주보도록 강제: enemy.FacingRight = !player.FacingRight
                ctx.Board.SetEnemyFacingRight(enemy, !ctx.Board.FacingRight);
            }
            return enemy;
        }

        /// <summary>순간이동 마커: 카드가 실제 이동(Move 효과)을 수행한 후 사용 카운터를 1 증가시킵니다.
        /// IsCardPlayableNow는 이 마커를 가진 카드를 전투당 act수만큼만 사용 가능하게 제한합니다.</summary>
        private static void Teleportation(CardEffectContext ctx)
        {
            ctx.Board.RegisterTeleportUse();
        }

        /// <summary>위치변경술: 선택한 적과 플레이어의 위치를 서로 교환합니다.</summary>
        private static void ChangeMove(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;
            ctx.Board.SwapPlayerEnemy(enemy);
        }

        /// <summary>이동 + 턴 종료 시 자동 소멸. 휘발성(Ethereal) 키워드 없이 카드 효과 자체로
        /// "내 턴이 끝나면 소멸" 동작을 제공한다 (70/970 유물의 매턴 이동 카드 등).
        /// 사용 시점에는 일반 Move와 동일하게 SelectedPos로 이동만 수행하며,
        /// 손에 남아 턴이 끝나는 경우 DiscardHand가 이 effectId를 감지해 소멸시킨다.</summary>
        private static void EndTurnExhaustsMove(CardEffectContext ctx)
        {
            if (ctx.SelectedPos.HasValue)
                ctx.Board.TryMovePlayerTo(ctx.SelectedPos.Value);
        }

        /// <summary>벽력일섬: 선택 적의 바라보는 방향 반대(등 뒤) 칸으로 이동 (피해 없음).</summary>
        private static void BehindEnemyMove(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;

            int behindCol = enemy.FacingRight ? enemy.GridPos.x - 1 : enemy.GridPos.x + 1;
            ctx.Board.TryMovePlayerTo(new Vector2Int(behindCol, enemy.GridPos.y));
        }

        /// <summary>다음에 사용되는 액션카드를 value장 복사하고 소멸을 부여합니다.</summary>
        private static void NextUseCopyCard(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new NextUseCopyCardPower(ctx.Value));

        /// <summary>
        /// (구) Range_Increase — 33100/33101의 구 ID. 현재는 ShootRange 핸들러와 동일 동작.
        /// 키워드가 아닌 customEffectId(IsShootAttackEffect)로 발사 공격 카드를 판별한다.
        /// </summary>
        private static void RangeIncrease(CardEffectContext ctx)
        {
            ctx.Board.IncreaseShootCardRange(ctx.Value);
        }

        private static void RewardGold(CardEffectContext ctx)
        {
            int gold = ctx.Value > 0 ? ctx.Value : Random.Range(10, 21);
            GameManager.Instance?.ModifyGold(gold);
            Debug.Log($"[Reward] 골드 +{gold}");
        }

        private static void RewardRelic(CardEffectContext ctx)
        {
            ctx.Board.ClaimPendingRewardRelic();
        }

        private static void RewardItem(CardEffectContext ctx)
        {
            ctx.Board.ClaimPendingRewardItem();
        }

        private static void RewardCard(CardEffectContext ctx)
        {
            // 패널을 열지 않고 픽 모드(보상 카드 3장 + 60004 넘기기 카드 드로우) 진입을 예약.
            ctx.Board.RequestRewardCardPick();
        }

        /// <summary>60004 넘기기 카드 — 카드 보상 픽 모드 종료를 예약한다.</summary>
        private static void RewardSkip(CardEffectContext ctx)
        {
            ctx.Board.RequestRewardCardPickEnd();
        }

        /// <summary>60010 유물선택 — 보스 유물 3장 중 1장을 고르는 픽 모드 진입을 예약한다 (넘기기 없음).</summary>
        private static void ChooseRelic(CardEffectContext ctx)
        {
            ctx.Board.RequestRelicPick();
        }

        /// <summary>60011 다음 스테이지 — Act +1 후 맵 재생성을 예약한다.</summary>
        private static void NextStage(CardEffectContext ctx)
        {
            ctx.Board.RequestNextStage();
        }

        /// <summary>60008 전투재시작 — 패배 직전 전투를 다시 시작하도록 예약한다.</summary>
        private static void CombatRestart(CardEffectContext ctx)
        {
            ctx.Board.RequestCombatRestart();
        }

        /// <summary>60009 메인메뉴 — 메인메뉴 씬으로 복귀하도록 예약한다.</summary>
        private static void ReturnMainMenu(CardEffectContext ctx)
        {
            ctx.Board.RequestReturnToMainMenu();
        }

        // ── 즉발 효과 구현 ───────────────────────────────────────────────────

        /// <summary>아무 동작도 하지 않는 핸들러 (드로우 시점에서 효과가 처리되는 카드용)</summary>
        private static void NoOp(CardEffectContext ctx) { }

        /// <summary>힘 +value</summary>
        private static void GainStrength(CardEffectContext ctx)
        {
            ctx.Board.AddStrength(ctx.Value);
        }

        /// <summary>HP +value (음수면 손실, no_loseHp 플래그 체크)</summary>
        private static void HealHp(CardEffectContext ctx)
        {
            if (ctx.Value < 0 && ctx.Board.NoCardHpLoss) return;

            GameManager.Instance?.Heal(ctx.Value);

            if (ctx.Value < 0)
                ctx.Board.FirePowerOnPlayerLostHp(-ctx.Value);
        }

        /// <summary>골드 +value</summary>
        private static void GainGold(CardEffectContext ctx)
        {
            GameManager.Instance?.ModifyGold(ctx.Value);
        }

        /// <summary>선택한 셀을 중심으로 3×3에 피해 (플레이어 포함)</summary>
        private static void AreaDamage(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;

            var center = ctx.SelectedPos.Value;
            int dmg = ctx.Value + ctx.Board.PlayerStrength;
            bool hitPlayer = false;

            var cells = new List<Vector2Int>(9);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var pos = new Vector2Int(center.x + dx, center.y + dy);
                if (!ctx.Board.IsInBoard(pos)) continue;
                cells.Add(pos);
                if (pos == ctx.Board.PlayerSpawnCell) hitPlayer = true;
            }
            ctx.Board.DealDamageAtCells(cells, dmg);

            if (hitPlayer)
                GameManager.Instance?.TakeDamage(dmg);
        }

        /// <summary>모든 적에게 피해 (플레이어는 제외)</summary>
        private static void AllDamage(CardEffectContext ctx)
        {
            int dmg = ctx.Value + ctx.Board.PlayerStrength;
            ctx.Board.DealDamageToAllEnemies(dmg);
        }

        // ── 파워 카드 효과 구현 ─────────────────────────────────────────────

        private static void RegisterPowerWithIcon(CardEffectContext ctx, CombatPowerEffect power)
        {
            string name = ctx.Card?.CardName ?? power.GetType().Name;
            string desc = ctx.Card?.Description ?? "";
            UnityEngine.Sprite icon = ctx.Card?.cardArt;
            UnityEngine.Vector2Int[] offsets = null;
            if (ctx.Effect?.rangeOffsets != null && ctx.Effect.rangeOffsets.Length > 0)
            {
                offsets = new UnityEngine.Vector2Int[ctx.Effect.rangeOffsets.Length];
                for (int i = 0; i < ctx.Effect.rangeOffsets.Length; i++)
                    offsets[i] = ctx.Effect.rangeOffsets[i].ToVector2Int();
            }
            ctx.Board.RegisterPower(power, name, desc, icon, offsets);
        }

        /// <summary>매 턴 시작 시 힘 +value</summary>
        private static void EveryturnStrength(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new EveryTurnStrengthPower(ctx.Value));

        /// <summary>적 처치 시 힘 +value</summary>
        private static void EveryKillStrength(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new EveryKillStrengthPower(ctx.Value));

        /// <summary>HP를 잃을 때마다 힘 +value</summary>
        private static void EveryLoseHpStrength(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new EveryLoseHpStrengthPower(ctx.Value));

        /// <summary>카드 소멸 시 힘 +value</summary>
        private static void EveryExhaustsStrength(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new EveryExhaustsStrengthPower(ctx.Value));

        /// <summary>적이 데미지를 입을 때마다 약간의 텀 후 +value 추가 데미지</summary>
        private static void DamegedMoreDamage(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new DamegedMoreDamagePower(ctx.Value));

        /// <summary>매 턴 단검 카드 한 장 생성 (value 0=22002, 1=22003)</summary>
        private static void EveryturnCreateKnife(CardEffectContext ctx)
        {
            string key = $"power_createknife_{ctx.Value}";
            string name = ctx.Card?.CardName ?? "단검생성";
            string desc = ctx.Card?.Description ?? "";
            UnityEngine.Sprite icon = ctx.Card?.cardArt;
            ctx.Board.RegisterPowerNoIcon(new EveryTurnCreateKnifePower(ctx.Value));
            DeckRoguelike.UI.InGameUIController.Instance?.AddPlayerEffectValue(key, icon, 1, name, desc);
        }

        /// <summary>매 턴 채력 1 손실 + effect 좌표의 적에게 value 피해 (혈사포)</summary>
        private static void EveryLoseHpAllrangeDamage(CardEffectContext ctx)
        {
            var offsets = ctx.Effect?.rangeOffsets;
            UnityEngine.Vector2Int[] cells;
            if (offsets != null && offsets.Length > 0)
            {
                cells = new UnityEngine.Vector2Int[offsets.Length];
                for (int i = 0; i < offsets.Length; i++)
                    cells[i] = offsets[i].ToVector2Int();
            }
            else
            {
                cells = new[]
                {
                    new UnityEngine.Vector2Int(0, 1), new UnityEngine.Vector2Int(0, -1),
                    new UnityEngine.Vector2Int(-1, 0), new UnityEngine.Vector2Int(1, 0),
                };
            }
            string name = ctx.Card?.CardName ?? "혈사포";
            string desc = ctx.Card?.Description ?? "";
            UnityEngine.Sprite icon = ctx.Card?.cardArt;
            var power = new EveryLoseHpAllRangeDamagePower(ctx.Value, cells);
            ctx.Board.RegisterPower(power, name, desc, icon, cells, true);
        }

        /// <summary>매 턴 채력 X 손실 + 카드 Y장 드로우. valueRaw "X.Y"</summary>
        private static void EveryLoseHpDraw(CardEffectContext ctx)
        {
            int hpLoss = 1;
            int draw   = ctx.Value;
            if (!string.IsNullOrEmpty(ctx.ValueRaw))
            {
                var parts = ctx.ValueRaw.Split('.');
                if (parts.Length > 0 && int.TryParse(parts[0], out var h)) hpLoss = h;
                if (parts.Length > 1 && int.TryParse(parts[1], out var d)) draw   = d;
            }
            string name = ctx.Card?.CardName ?? "광폭화";
            string desc = ctx.Card?.Description ?? "";
            UnityEngine.Sprite icon = ctx.Card?.cardArt;
            ctx.Board.RegisterPowerNoIcon(new EveryLoseHpDrawPower(hpLoss, draw));
            DeckRoguelike.UI.InGameUIController.Instance?.AddPlayerEffectValue("power_loseHpDraw", icon, draw, name, desc);
        }

        /// <summary>소멸 카드 사용 시 힘 +value</summary>
        private static void ExhaustsCardStrength(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new ExhaustsCardStrengthPower(ctx.Value));

        /// <summary>소멸 카드 사용 시 카드 value장 드로우</summary>
        private static void ExhaustsCardDraw(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new ExhaustsCardDrawPower(ctx.Value));

        /// <summary>이동 카드 사용 시 힘 +value</summary>
        private static void MoveCardStrength(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new MoveCardStrengthPower(ctx.Value));

        /// <summary>파워 카드 사용 시 힘 +value</summary>
        private static void PowerCardStrength(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new PowerCardStrengthPower(ctx.Value));

        // ── 연속 공격 효과 구현 ─────────────────────────────────────────────────

        /// <summary>선택한 적에게 value 피해를 2번 줍니다 (0.15초 텀)</summary>
        private static void DoubleDamage(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;
            int dmg = ctx.Value + ctx.Board.PlayerStrength;
            ctx.Board.StartSequentialDamage(enemy, dmg, 2);
        }

        /// <summary>선택한 적에게 value 피해를 5번 줍니다</summary>
        private static void FiveTimesDamage(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;
            int dmg = ctx.Value + ctx.Board.PlayerStrength;
            ctx.Board.StartSequentialDamage(enemy, dmg, 5);
        }

        /// <summary>선택한 적에게 value 피해를 4번 줍니다 (연속배기)</summary>
        private static void FourTimesDamage(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;
            int dmg = ctx.Value + ctx.Board.PlayerStrength;
            ctx.Board.StartSequentialDamage(enemy, dmg, 4);
        }

        /// <summary>선택한 적에게 value 피해를 6번 줍니다.</summary>
        private static void SixTimesDamage(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;
            int dmg = ctx.Value + ctx.Board.PlayerStrength;
            ctx.Board.StartSequentialDamage(enemy, dmg, 6);
        }

        /// <summary>무작위 적에게 value 피해를 3번 줍니다 (칼던지기)</summary>
        private static void RandomDamage(CardEffectContext ctx)
        {
            int dmg = ctx.Value + ctx.Board.PlayerStrength;
            ctx.Board.StartRandomEnemyDamage(dmg, 3);
        }

        /// <summary>
        /// 검의 왈츠(22302/22303): 선택한 적에게 value 피해를 1번 준 뒤, 무작위 적에게 value 피해를 5번 입힙니다.
        /// 선택한 적이 첫 타격에 죽더라도 무작위 5타격은 살아있는 다른 적을 대상으로 계속 진행됩니다.
        /// </summary>
        private static void DamageThen5Random(CardEffectContext ctx)
        {
            int dmg = ctx.Value + ctx.Board.PlayerStrength;
            if (ctx.SelectedPos.HasValue)
            {
                var enemy = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
                if (enemy != null && enemy.CurrentHP > 0)
                    ctx.Board.DamageEnemy(enemy, dmg);
            }
            ctx.Board.StartRandomEnemyDamage(dmg, 5);
        }

        /// <summary>적이 처치될 때까지 X 피해 + 채력 Y 손실 반복 (치킨게임). valueRaw "X.Y"</summary>
        private static void UntilKillLoseHp(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;

            int baseDmg = ctx.Value;
            int loss    = 1;
            if (!string.IsNullOrEmpty(ctx.ValueRaw))
            {
                var parts = ctx.ValueRaw.Split('.');
                if (parts.Length > 0 && int.TryParse(parts[0], out var d)) baseDmg = d;
                if (parts.Length > 1 && int.TryParse(parts[1], out var l)) loss    = l;
            }
            int finalDmg = baseDmg + ctx.Board.PlayerStrength;
            ctx.Board.StartUntilKillLoseHp(enemy, finalDmg, loss);
        }

        /// <summary>
        /// 손패의 카드를 전부 소멸시키고, 소멸시킨 카드 1장당 피해를 두배로 늘려서 한 번에 적중.
        /// 최종 피해 = (value + 힘) × 2^N  (N = 소멸된 카드 수)
        /// 발사체 스프라이트는 ctx.Card.imagePath에서 로드 (없으면 인스펙터 기본).
        /// </summary>
        private static void AllExhaustsDamage(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;
            int baseDmg = ctx.Value + ctx.Board.PlayerStrength;
            ctx.Board.StartExhaustAllAndDamage(enemy, baseDmg, ctx.Card);
        }

        // ── 처치 보상 효과 구현 ─────────────────────────────────────────────

        /// <summary>피해 + 처치 시 골드. valueRaw "X.Y" → X 데미지 / Y 골드.</summary>
        private static void DamageKilledGold(CardEffectContext ctx)
            => DamageKilledReward(ctx, KillReward.Gold);

        /// <summary>피해 + 처치 시 체력. valueRaw "X.Y" → X 데미지 / Y 체력.</summary>
        private static void DamageKilledHp(CardEffectContext ctx)
            => DamageKilledReward(ctx, KillReward.Hp);

        /// <summary>피해 + 처치 시 최대 체력. valueRaw "X.Y" → X 데미지 / Y 최대체력.</summary>
        private static void DamageKilledFullHp(CardEffectContext ctx)
            => DamageKilledReward(ctx, KillReward.MaxHp);

        /// <summary>숙명: 피해를 주고 적이 처치되면 그 위치로 이동.</summary>
        private static void DamageKilledMove(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;

            var enemyPos = enemy.GridPos;
            int dmg = ctx.Value + ctx.Board.PlayerStrength;
            int hpBefore = enemy.CurrentHP;
            ctx.Board.DamageEnemy(enemy, dmg);
            if (hpBefore > 0 && enemy.CurrentHP <= 0)
                ctx.Board.TryMovePlayerTo(enemyPos);
        }

        private enum KillReward { Gold, Hp, MaxHp }

        private static void DamageKilledReward(CardEffectContext ctx, KillReward kind)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;

            int baseDmg = ctx.Value;
            int reward  = 0;
            if (!string.IsNullOrEmpty(ctx.ValueRaw))
            {
                var parts = ctx.ValueRaw.Split('.');
                if (parts.Length > 0 && int.TryParse(parts[0], out var d)) baseDmg = d;
                if (parts.Length > 1 && int.TryParse(parts[1], out var r)) reward  = r;
            }

            int dmg = baseDmg + ctx.Board.PlayerStrength;
            int hpBefore = enemy.CurrentHP;
            ctx.Board.DamageEnemy(enemy, dmg);
            if (hpBefore > 0 && enemy.CurrentHP <= 0 && reward > 0)
            {
                switch (kind)
                {
                    case KillReward.Gold:  GameManager.Instance?.ModifyGold(reward);  break;
                    case KillReward.Hp:    GameManager.Instance?.Heal(reward);        break;
                    case KillReward.MaxHp: GameManager.Instance?.ModifyMaxHP(reward); break;
                }
            }
        }

        // ── 카드 강화 ───────────────────────────────────────────────────────

        /// <summary>value 0 = 손패 모든 카드 강화, 1 = 덱 모든 카드 강화. 전투 종료 시 자동 복원.</summary>
        private static void CardUpgrade(CardEffectContext ctx)
        {
            if (ctx.Value == 1) ctx.Board.UpgradeAllDeckCardsTemporary();
            else                ctx.Board.UpgradeAllHandCardsTemporary();
        }

        // ── 이동/적 제어 효과 구현 ──────────────────────────────────────────

        /// <summary>
        /// 돌진: selectedPos에 적이 있으면 같은 방향으로 1칸 밀치고 {D} 피해를 입힌 뒤 플레이어가 그 자리로 들어갑니다.
        /// 적이 없으면 그냥 이동합니다. 적 뒤가 막혀 밀 수 없는 상황은 HighlightRange/IsValidPushTarget이
        /// 사전에 차단하므로, 여기서는 방어적으로 한번 더 검증합니다.
        /// </summary>
        private static void PushEnemy(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            if (!ctx.Board.IsValidPushTarget(ctx.SelectedPos.Value)) return;

            var targetPos = ctx.SelectedPos.Value;
            var playerPos = ctx.Board.PlayerSpawnCell;

            int dx = targetPos.x - playerPos.x;
            int dy = targetPos.y - playerPos.y;
            int stepX = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
            int stepY = dy == 0 ? 0 : (dy > 0 ? 1 : -1);

            var enemy = ctx.Board.GetEnemyAt(targetPos);
            if (enemy != null)
            {
                int dmg = ctx.Value + ctx.Board.PlayerStrength;
                ctx.Board.DamageEnemy(enemy, dmg);
                if (enemy.CurrentHP > 0)
                {
                    // 멀티셀 적도 footprint anchor 기준으로 1칸 밀어내야 한다
                    // (이전: targetPos+step 사용 → 멀티셀일 때 너무 멀리·필요 공간 과다로 밀림 실패)
                    ctx.Board.TryMoveEnemy(enemy, enemy.GridPos + new Vector2Int(stepX, stepY), forced: true); // 밀침 — 속박 무시
                }
            }

            ctx.Board.TryMovePlayerTo(targetPos);
        }

        /// <summary>
        /// 채력을 먼저 잃고 SelectedPos의 단일 적에게 공격합니다. valueRaw "X.Y" → X 데미지 / Y HP 손실.
        /// valueRaw가 비어있으면 ctx.Value를 데미지로 사용하고 HP 손실은 0.
        /// </summary>
        private static void LoseHpDamage(CardEffectContext ctx)
        {
            ParseLoseHpDamage(ctx, out int dmg, out int hpLoss);
            ApplyHpLoss(ctx, hpLoss);

            if (!ctx.SelectedPos.HasValue) return;
            int finalDmg = dmg + ctx.Board.PlayerStrength;
            var e = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
            if (e != null) ctx.Board.DamageEnemy(e, finalDmg);
        }

        /// <summary>
        /// 채력을 먼저 잃고 rangeOffsets 내 모든 적에게 공격합니다. valueRaw "X.Y" → X 데미지 / Y HP 손실.
        /// rangeOffsets가 비어있으면 살아있는 모든 적에게 피해.
        /// </summary>
        private static void LoseHpAllDamage(CardEffectContext ctx)
        {
            ParseLoseHpDamage(ctx, out int dmg, out int hpLoss);
            ApplyHpLoss(ctx, hpLoss);

            int finalDmg = dmg + ctx.Board.PlayerStrength;
            var effect = ctx.Effect;
            if (effect?.rangeOffsets != null && effect.rangeOffsets.Length > 0)
            {
                var cells = new List<Vector2Int>(effect.rangeOffsets.Length);
                foreach (var offset in effect.rangeOffsets)
                {
                    Vector2Int pos = effect.useAbsoluteCoords
                        ? offset.ToVector2Int()
                        : ctx.Board.PlayerSpawnCell + offset.ToVector2Int();
                    cells.Add(pos);
                }
                ctx.Board.DealDamageAtCells(cells, finalDmg);
            }
            else
            {
                ctx.Board.DealDamageToAllEnemies(finalDmg);
            }
        }

        private static void ParseLoseHpDamage(CardEffectContext ctx, out int dmg, out int hpLoss)
        {
            dmg    = ctx.Value;
            hpLoss = 0;
            if (string.IsNullOrEmpty(ctx.ValueRaw)) return;
            var parts = ctx.ValueRaw.Split('.');
            if (parts.Length > 0 && int.TryParse(parts[0], out var d)) dmg    = d;
            if (parts.Length > 1 && int.TryParse(parts[1], out var h)) hpLoss = h;
        }

        private static void ApplyHpLoss(CardEffectContext ctx, int hpLoss)
        {
            if (hpLoss <= 0 || ctx.Board.NoCardHpLoss) return;
            GameManager.Instance?.Heal(-hpLoss);
            ctx.Board.FirePowerOnPlayerLostHp(hpLoss);
        }

        /// <summary>
        /// 비껴치기: 대각선 1칸으로 이동한 뒤, 도착 위치 기준 상하좌우 1칸의 적을 공격합니다.
        /// </summary>
        private static void MoveDiagonalDamage(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;

            var target    = ctx.SelectedPos.Value;
            var playerPos = ctx.Board.PlayerSpawnCell;

            int dx = target.x - playerPos.x;
            int dy = target.y - playerPos.y;
            if (Mathf.Abs(dx) != 1 || Mathf.Abs(dy) != 1) return;

            ctx.Board.TryMovePlayerTo(target);

            int dmg = ctx.Value + ctx.Board.PlayerStrength;

            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            var cells = new List<Vector2Int>(dirs.Length);
            foreach (var dir in dirs) cells.Add(target + dir);
            ctx.Board.DealDamageAtCells(cells, dmg);
        }

        /// <summary>
        /// 카드의 rangeOffsets 범위 내 살아있는 모든 적에게 공포(Fear) 1스택을 부여합니다 (치킨게임 대체 효과).
        /// rangeOffsets가 비어있으면 보드 전체 적에게 적용.
        /// </summary>
        private static void AllRangeFear(CardEffectContext ctx)
        {
            var effect = ctx.Effect;
            if (effect == null) return;

            if (effect.rangeOffsets == null || effect.rangeOffsets.Length == 0)
            {
                ctx.Board.ApplyStatusToAllEnemies(StatusEffectType.Fear, 1);
                return;
            }

            foreach (var offset in effect.rangeOffsets)
            {
                Vector2Int pos = effect.useAbsoluteCoords
                    ? offset.ToVector2Int()
                    : ctx.Board.PlayerSpawnCell + offset.ToVector2Int();
                var enemy = ctx.Board.GetEnemyAt(pos);
                if (enemy != null && enemy.CurrentHP > 0)
                    ctx.Board.ApplyStatus(enemy, StatusEffectType.Fear, 1);
            }
        }

        /// <summary>
        /// 중력(43200/43201): 살아있는 모든 적에게 속박(Bondage)을 value턴 부여합니다.
        /// 속박된 적은 그 동안 "이동"만 못하며 공격·기타 행동은 정상 수행합니다.
        /// (적 턴 종료마다 1턴씩 감소 — BoardController.DecrementEnemyBondage)
        /// </summary>
        private static void AllBondage(CardEffectContext ctx)
        {
            int turns = UnityEngine.Mathf.Max(1, ctx.Value);
            ctx.Board.ApplyStatusToAllEnemies(StatusEffectType.Bondage, turns);
        }

        /// <summary>
        /// 한파(43300/43301): value턴 주기로 모든 적에게 냉기를 부여하는 파워를 등록합니다.
        /// value=1 매 턴 / value=2 2턴마다. 냉기 3 누적 시 해당 적은 빙결로 전환.
        /// </summary>
        private static void AllChill(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new EveryNTurnsChillPower(ctx.Value));

        /// <summary>선택한 적의 뒤 칸으로 이동 후 그 적에게 (value + 힘) 피해를 줍니다.</summary>
        private static void MoveBehindEnemyDamage(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;

            var enemyPos  = ctx.SelectedPos.Value;
            var playerPos = ctx.Board.PlayerSpawnCell;

            int behindCol = (enemyPos.x >= playerPos.x) ? enemyPos.x + 1 : enemyPos.x - 1;
            var target = new Vector2Int(behindCol, enemyPos.y);
            ctx.Board.TryMovePlayerTo(target);

            int dmg = ctx.Value + ctx.Board.PlayerStrength;
            if (enemy.CurrentHP > 0) ctx.Board.DamageEnemy(enemy, dmg);
        }

        // ── 기타 효과 ────────────────────────────────────────────────────────

        /// <summary>
        /// 무작위 액션 카드 1장을 손패에 비용 0으로 추가합니다.
        /// value는 최대 희귀도 (CardRarity enum 기준: Common=0, Uncommon=1, Rare=2, Legendary=3).
        /// </summary>
        private static void RandomWeapon(CardEffectContext ctx)
        {
            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            var pool = CardRegistry.GetRewardPool(character)
                .Where(c => !c.IsUpgraded && c.CardTypeFromCode == CardType.Action)
                .Where(c => (int)c.Rarity <= ctx.Value)
                .ToList();

            if (pool.Count == 0)
            {
                Debug.LogWarning("[RandomWeapon] 조건에 맞는 액션 카드가 없습니다.");
                return;
            }

            var picked = pool[Random.Range(0, pool.Count)];
            ctx.Board.AddCardToHandFree(picked);
        }

        /// <summary>이번 턴에만 힘 +value, 턴 종료 시 자동 회수</summary>
        private static void ThisTurnStrength(CardEffectContext ctx)
        {
            ctx.Board.AddTempStrength(ctx.Value);
        }

        /// <summary>현재 힘을 value배로 만듭니다 (한계돌파)</summary>
        private static void MultiplyStrength(CardEffectContext ctx)
        {
            int current = ctx.Board.PlayerStrength;
            if (current > 0)
                ctx.Board.AddStrength(current * (ctx.Value - 1));
        }

        // ── Shoot 시스템 효과 구현 ───────────────────────────────────────

        /// <summary>32000/32001 발사: 단일 적에게 (value + ShootDamage + Strength) 피해.</summary>
        private static void Shoot(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            var enemy = ctx.Board.GetEnemyAt(ctx.SelectedPos.Value);
            if (enemy == null) return;
            ctx.Board.FireShoot(enemy, ctx.Value);
        }

        /// <summary>
        /// 32300/32301 석양이 진다: BFS(ShootRange) 사거리 내 모든 적에게 value회 발사.
        /// 한 발 = 6(기본/강화시 12) + ShootDamage + Strength.
        /// </summary>
        private static void AllrangeShoot(CardEffectContext ctx)
        {
            int repeats = UnityEngine.Mathf.Max(1, ctx.Value);
            int baseHit = (ctx.Card != null && ctx.Card.IsUpgraded) ? 12 : 6;
            var targets = new List<EnemyInstance>();
            foreach (var pos in BfsCells(ctx, BoardController.CurrentShootRange))
            {
                var e = ctx.Board.GetEnemyAt(pos);
                if (e != null && e.CurrentHP > 0) targets.Add(e);
            }
            for (int r = 0; r < repeats; r++)
                foreach (var t in targets)
                    if (t.CurrentHP > 0) ctx.Board.FireShoot(t, baseHit);
        }

        /// <summary>
        /// 32302/32303 난사: BFS(ShootRange) 사거리 내 무작위 적에게 (6 × value)회 발사.
        /// 한 발 = 6(강화시 12) + ShootDamage + Strength.
        /// </summary>
        private static void SixTimesShoot(CardEffectContext ctx)
        {
            int perTarget = UnityEngine.Mathf.Max(1, ctx.Value);
            int totalShots = 6 * perTarget;
            int baseHit = (ctx.Card != null && ctx.Card.IsUpgraded) ? 12 : 6;
            for (int i = 0; i < totalShots; i++)
            {
                var alive = new List<EnemyInstance>();
                foreach (var pos in BfsCells(ctx, BoardController.CurrentShootRange))
                {
                    var e = ctx.Board.GetEnemyAt(pos);
                    if (e != null && e.CurrentHP > 0) alive.Add(e);
                }
                if (alive.Count == 0) return;
                var pick = alive[UnityEngine.Random.Range(0, alive.Count)];
                ctx.Board.FireShoot(pick, baseHit);
            }
        }

        /// <summary>33100/33101 정확도 향상: 발사 사거리 +value (전역 누적).</summary>
        private static void ShootRange(CardEffectContext ctx)
        {
            ctx.Board.IncreaseShootCardRange(ctx.Value);
        }

        /// <summary>33102/33103 탄속 증가: 발사 데미지 +value (전역 누적).</summary>
        private static void ShootDamage(CardEffectContext ctx)
        {
            ctx.Board.AddShootDamage(ctx.Value);
        }

        /// <summary>33200/33201 회피: Dodge +value.</summary>
        private static void Dodge(CardEffectContext ctx)
        {
            ctx.Board.AddDodge(UnityEngine.Mathf.Max(1, ctx.Value));
            var icon = ctx.Card?.cardArt;
            DeckRoguelike.UI.InGameUIController.Instance?.AddPlayerEffectValue(
                "dodge", icon, UnityEngine.Mathf.Max(1, ctx.Value),
                ctx.Card?.CardName ?? "회피",
                ctx.Card?.Description ?? "다음 공격을 무효화합니다.");
        }

        /// <summary>32200/32201 재장전: value장의 32002(또는 32003) 카드를 손패에 추가.</summary>
        private static void ReLoad(CardEffectContext ctx)
        {
            int count = UnityEngine.Mathf.Max(1, ctx.Value);
            int code = ctx.Card != null && ctx.Card.IsUpgraded ? 32003 : 32002;
            var card = DeckRoguelike.Core.CardRegistry.GetCard(code);
            if (card == null) return;
            for (int i = 0; i < count; i++) ctx.Board.AddCardToHandFree(card);
        }

        /// <summary>33202/33203 도탄: 발사 N회마다 무작위 적에게 추가 발사.</summary>
        private static void EveryShootRicochet(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new EveryShootRicochetPower(ctx.Value));

        /// <summary>33302/33303 무기 숙련: 발사할 때마다 힘 +value.</summary>
        private static void EveryShootStrength(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new EveryShootStrengthPower(ctx.Value));

        /// <summary>33300/33301: 발사 N회마다 이동 카드 1장 획득.</summary>
        private static void ShootMoveCard(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new ShootMoveCardPower(ctx.Value));

        /// <summary>33304/33305 저격: 적과의 거리당 발사 데미지 ×value.</summary>
        private static void ShootSnipe(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new ShootSnipePower(ctx.Value));

        /// <summary>33306/33307 유탄발사: 발사가 도착 칸 + 4방향에 추가 피해.</summary>
        private static void ShootAreaAttack(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new ShootAreaAttackPower(ctx.Value));

        /// <summary>33104/33105 설치가속: 트랩을 설치할 때마다 힘 +value.</summary>
        private static void EveryTrapStrength(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new EveryTrapStrengthPower(ctx.Value));

        /// <summary>33302/33303 변형: 트랩을 설치할 때마다 발사 카드 1장 획득.</summary>
        private static void EveryTrapShootCard(CardEffectContext ctx)
            => RegisterPowerWithIcon(ctx, new EveryTrapShootCardPower(ctx.Value));

        /// <summary>32306/32307: 보드 상의 모든 빈 칸에 지뢰 또는 트랩 설치.</summary>
        private static void AllrangeTrap(CardEffectContext ctx)
        {
            string id = ctx.Effect?.customEffectId ?? string.Empty;
            int hazardCode = 32122; // 기본 지뢰
            int suffixStart = id.LastIndexOf('_');
            if (suffixStart >= 0 && int.TryParse(id.Substring(suffixStart + 1), out var parsed))
                hazardCode = parsed;

            var hazardData = DeckRoguelike.Core.HazardRegistry.GetHazard(hazardCode);
            if (hazardData == null) return;

            for (int c = 0; c < ctx.Board.BoardColumns; c++)
                for (int r = 0; r < ctx.Board.BoardRows; r++)
                {
                    var pos = new UnityEngine.Vector2Int(c, r);
                    if (!ctx.Board.IsCellEmpty(pos)) continue;
                    ctx.Board.EmplaceHazard(hazardData, pos, hazardData.value);
                }
        }

        /// <summary>42140/42141 용암 지대: 선택 셀에 LavaZone 위해(32140/강화시 32141) 설치.</summary>
        private static void GenerateLavaZone(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            int hazardCode = (ctx.Card != null && ctx.Card.IsUpgraded) ? 32141 : 32140;
            var data = DeckRoguelike.Core.HazardRegistry.GetHazard(hazardCode);
            if (data == null) return;
            ctx.Board.EmplaceHazard(data, ctx.SelectedPos.Value, data.value);
        }

        /// <summary>42240/42241 빙하 지대: 선택 셀에 GlacierZone 위해(32242/강화시 32243) 설치.</summary>
        private static void GenerateGlacierZone(CardEffectContext ctx)
        {
            if (!ctx.SelectedPos.HasValue) return;
            int hazardCode = (ctx.Card != null && ctx.Card.IsUpgraded) ? 32243 : 32242;
            var data = DeckRoguelike.Core.HazardRegistry.GetHazard(hazardCode);
            if (data == null) return;
            ctx.Board.EmplaceHazard(data, ctx.SelectedPos.Value, data.value);
        }

        /// <summary>플레이어 중심 BFS 셀 좌표 열거 (Manhattan ≤ n, 중심 포함).</summary>
        private static IEnumerable<UnityEngine.Vector2Int> BfsCells(CardEffectContext ctx, int n)
        {
            var center = ctx.Board.PlayerSpawnCell;
            for (int dc = -n; dc <= n; dc++)
                for (int dr = -n; dr <= n; dr++)
                {
                    if (UnityEngine.Mathf.Abs(dc) + UnityEngine.Mathf.Abs(dr) > n) continue;
                    var pos = new UnityEngine.Vector2Int(center.x + dc, center.y + dr);
                    if (ctx.Board.IsInBoard(pos)) yield return pos;
                }
        }

        /// <summary>이번 턴 가하는 피해와 받는 피해를 value배로 만듭니다 (최후의 공격)</summary>
        private static void MultiplyAttack(CardEffectContext ctx)
        {
            var ui = DeckRoguelike.UI.InGameUIController.Instance;
            bool stacked = ui != null && ui.HasPlayerEffect("multiple_attack");

            if (stacked)
                ctx.Board.MultiplyTurnDamageMultipliers(ctx.Value, ctx.Value);
            else
                ctx.Board.SetTurnDamageMultipliers(ctx.Value, ctx.Value);

            int displayValue = UnityEngine.Mathf.RoundToInt(ctx.Board.OutgoingDamageMultiplier);
            string name = ctx.Card?.CardName ?? "최후의 공격";
            string desc = ctx.Card?.Description ?? "";
            UnityEngine.Sprite icon = ctx.Card?.cardArt;
            ui?.SetPlayerEffect("multiple_attack", icon, displayValue, name, desc);
        }
    }
}
