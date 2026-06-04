using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DeckRoguelike.Cards;
using DeckRoguelike.Combat;
using DeckRoguelike.Core;

namespace DeckRoguelike.Item
{
    /// <summary>
    /// 모든 아이템 효과를 등록하고 구현합니다.
    ///
    /// [itemCode 규칙]
    ///   X (백의 자리): 1=Common 2=Uncommon 3=Rare 9=Boss
    ///   Y (십의 자리): 7=Warrior전용 8=Gunner전용 9=Mage전용 그외=공용
    ///   Z (일의 자리): 개별 번호
    /// </summary>
    public static class ItemLibrary
    {
        private static readonly Dictionary<int, System.Func<ItemEffect>> effectFactories =
            new Dictionary<int, System.Func<ItemEffect>>
            {
                // ── Common (1xx) ────────────────────────────────────────
                

                { 100, () => new ActionPotionItem() },  // 액션 포션
                { 102, () => new PowerPotionItem() },      // 파워 포션
                { 103, () => new RecyclePotionItem() },    // 재활용 포션 (버린 더미에서 1장)
                { 104, () => new ForesightPotionItem() },  // 예지 포션 (뽑을 더미에서 1장)
                { 105, () => new ArrowItem() },            // 화살
                { 106, () => new ChocolateBarItem() },     // 초코바
                { 107, () => new StrengthPotionItem() },   // 힘 포션
                { 108, () => new ReusePotionItem() },      // 재사용 포션
                { 109, () => new FlashbangItem() },        // 섬광탄

                // Common - 직업 전용

                { 170, () => new ScissorsItem() },         // 가위 (Warrior)
                { 180, () => new MagazineItem() },         // 탄알집 (Gunner)
                { 190, () => new MolotovItem() },          // 화염병 (Mage)

                // ── Uncommon (2xx) ──────────────────────────────────────





                // Uncommon - 직업 전용



                // ── Rare (3xx) ──────────────────────────────────────────
                { 200, () => new HealthPotionItem() },     // 체력 포션
                { 201, () => new GhostPotionItem() },      // 유령 포션
                { 202, () => new BombItem() },             // 폭탄
                { 204, () => new LuckyDiceItem() },        // 사기 주사위
                { 205, () => new RainbowPotionItem() },    // 무지개 포션
                { 206, () => new RouletteItem() },         // 돌림판
                { 207, () => new DrawPileItem() },         // 카드더미
                { 208, () => new PortalGunItem() },        // 포탈건 (적과 위치 교환)
                { 209, () => new UpgradePotionItem() }, 
                // Rare - 직업 전용
                { 270, () => new IronMaskItem() },         // 철가면 (Warrior)
                { 280, () => new DogTagItem() },           // 군번줄 (Gunner)
                { 290, () => new BoneFluteItem() },        // 뼈피리 (Mage)
                //안쓰는 아이템
                { 700, () => new HolyWaterItem() },        // 성수
            };

        public static void RegisterAll()
        {
            ItemEffectRegistry.Clear();

            var db = Addressables.LoadAssetAsync<ItemDatabase>("Data/ItemDatabase").WaitForCompletion();
            if (db == null)
            {
                Debug.LogError("[ItemLibrary] ItemDatabase.asset 을 찾을 수 없습니다. Addressables 주소를 확인하세요.");
                return;
            }

            int count = 0;
            foreach (var data in db.items)
            {
                if (data == null || data.itemCode == 0) continue;
                if (!effectFactories.TryGetValue(data.itemCode, out var factory))
                {
                    Debug.LogWarning($"[ItemLibrary] 코드 {data.itemCode}에 대응하는 ItemEffect 구현이 없습니다.");
                    continue;
                }
                ItemEffectRegistry.Register(data, factory);
                count++;
            }
            Debug.Log($"[ItemLibrary] {count}개 아이템 등록 완료");
        }

        // ── 공통 헬퍼 ───────────────────────────────────────────────────

        internal static List<CardData> GetRandomCardsOfType(CardType type, int maxCount)
        {
            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            var pool = CardRegistry.GetRewardPool(character)
                .Where(c => !c.IsUpgraded && c.ClassDigit != 1 && c.CardTypeFromCode == type)
                .ToList();
            Shuffle(pool);
            return pool.Take(maxCount).ToList();
        }

        internal static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Common (1xx) — 공용
    // ════════════════════════════════════════════════════════════════════

    /// <summary>100 액션 포션 - 무작위 액션 카드 1장을 손으로 가져옵니다</summary>
    public class ActionPotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            var cards = ItemLibrary.GetRandomCardsOfType(CardType.Action, 1);
            if (cards.Count == 0) return;
            ctx.Board.AddCardToHandFree(cards[0]);
        }
    }


    /// <summary>102 파워 포션 - 무작위 파워 카드 1장을 손으로 가져옵니다</summary>
    public class PowerPotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            var cards = ItemLibrary.GetRandomCardsOfType(CardType.Power, 1);
            if (cards.Count == 0) return;
            ctx.Board.AddCardToHandFree(cards[0]);
        }
    }

    /// <summary>103 재활용 포션 - 버린 카드 더미에서 카드 1장 선택해 손으로 가져옵니다</summary>
    public class RecyclePotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) =>
            ctx.InCombat && DeckManager.Instance != null && DeckManager.Instance.DiscardPileCount > 0;
        public override bool NeedsTargetingMode => true;
        public override void OnItemUsed(ItemUseContext ctx) =>
            PotionPickHelper.BeginPick(ctx, fromDiscard: true);
    }

    /// <summary>104 예지 포션 - 뽑을 카드 더미에서 카드 1장 선택해 손으로 가져옵니다</summary>
    public class ForesightPotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) =>
            ctx.InCombat && DeckManager.Instance != null && DeckManager.Instance.DrawPileCount > 0;
        public override bool NeedsTargetingMode => true;
        public override void OnItemUsed(ItemUseContext ctx) =>
            PotionPickHelper.BeginPick(ctx, fromDiscard: false);
    }

    /// <summary>
    /// 103/104 포션 공용 — drawPanel/discardPanel을 열고 CardUI.OnPotionPick으로 카드 선택을 받습니다.
    /// 카드 선택 성공 시 OnConfirmedUse로 아이템을 소모하고, 패널이 그냥 닫히면 아이템을 유지합니다.
    /// </summary>
    internal static class PotionPickHelper
    {
        public static void BeginPick(ItemUseContext ctx, bool fromDiscard)
        {
            var ui = DeckRoguelike.UI.InGameUIController.Instance;
            var dm = DeckManager.Instance;
            if (ui == null || dm == null || ctx.Board == null) return;
            // 이미 다른 포션 픽 모드 진행 중이면 중복 진입 차단 (아이템은 미소모로 유지)
            if (DeckRoguelike.Cards.CardUI.PotionPickModeActive) return;

            System.Action<DeckRoguelike.Cards.CardData> pickHandler = null;
            System.Action cancelHandler = null;

            void Cleanup()
            {
                DeckRoguelike.Cards.CardUI.PotionPickModeActive = false;
                DeckRoguelike.Cards.CardUI.OnPotionPick         -= pickHandler;
                DeckRoguelike.Cards.CardUI.OnPotionPickCancelled -= cancelHandler;
            }

            pickHandler = (picked) =>
            {
                if (picked == null) return;
                // 성공 분기: PotionPickModeActive=false 먼저 설정 — 패널 닫기로 인한 cancelHandler 발화 방지.
                Cleanup();

                bool removed = fromDiscard
                    ? dm.RemoveFromDiscardPile(picked)
                    : dm.RemoveFromDrawPile(picked);
                if (removed)
                    ctx.Board.AddCardToHandFree(picked);

                ctx.OnConfirmedUse?.Invoke();

                if (fromDiscard) ui.ToggleDiscardPanel();
                else             ui.ToggleDrawPanel();
            };

            cancelHandler = () =>
            {
                // 사용자가 카드 선택 없이 패널을 닫음 — 아이템 미소모 (OnConfirmedUse 호출 안 함)
                Cleanup();
            };

            DeckRoguelike.Cards.CardUI.OnPotionPick         += pickHandler;
            DeckRoguelike.Cards.CardUI.OnPotionPickCancelled += cancelHandler;
            DeckRoguelike.Cards.CardUI.PotionPickModeActive  = true;

            if (fromDiscard) ui.ShowDiscardPanel();
            else             ui.ShowDrawPanel();
        }
    }

    /// <summary>106 화살 - 선택한 적에게 피해 10</summary>
    public class ArrowItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override bool NeedsTargetingMode => true;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.EnterItemEnemyTargetingMode(
                enemy =>
                {
                    ctx.Board.DamageEnemy(enemy, 20);
                    Debug.Log($"[ArrowItem] 화살 발사! {enemy.Name} 20 피해");
                },
                null, onCancel: null,
                onConfirmedUse: ctx.OnConfirmedUse
            );
        }
    }

    /// <summary>107 초코바 - 이번 턴 힘 +5 (턴 종료 시 사라짐)</summary>
    public class ChocolateBarItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.AddTempStrength(5);
            Debug.Log("[ChocolateBarItem] 이번 턴 힘 +5");
        }
    }

    /// <summary>108 돌림판 - 드로우 더미 맨 위 3장 자동 실행 (무작위 대상)</summary>
    public class RouletteItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.PlayTopCardsFromDraw(3);
            Debug.Log("[RouletteItem] 드로우 더미 상위 3장 자동 실행");
        }
    }

    /// <summary>108 재사용 포션 - 다음에 사용하는 카드를 복사하고 복사본에 소멸을 부여합니다</summary>
    public class ReusePotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.SetNextCardCopyExhaust();
            Debug.Log("[ReusePotionItem] 다음에 사용하는 카드를 복사하고 소멸을 부여합니다");
        }
    }

    // ── Common 직업 전용 ─────────────────────────────────────────────────

    /// <summary>170 가위 (Warrior) - 이번 전투 힘 +3, 체력을 1씩 3번 잃습니다</summary>
    public class ScissorsItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.AddStrength(3);
            for (int i = 0; i < 3; i++)
                ctx.GM.TakeDamage(1);
            Debug.Log("[ScissorsItem] 힘 +3, 체력 -1 × 3");
        }
    }

    /// <summary>180 탄알집 (Gunner) - 발사 카드 2장을 손패에 추가합니다 (비용 0)</summary>
    public class MagazineItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            // 발사 카드 풀: 키워드가 아닌 customEffectId로 식별 (BoardController.IsShootAttackEffect).
            var shootPool = CardRegistry.GetRewardPool(character)
                .Where(c => !c.IsUpgraded && c.effects != null &&
                            c.effects.Any(fx => DeckRoguelike.UI.BoardController.IsShootAttackEffect(fx)))
                .ToList();

            if (shootPool.Count == 0)
            {
                Debug.LogWarning("[MagazineItem] 발사 카드를 찾을 수 없습니다");
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                var picked = shootPool[Random.Range(0, shootPool.Count)];
                ctx.Board.AddCardToHandFree(picked);
            }
            Debug.Log("[MagazineItem] 발사 카드 2장 추가");
        }
    }

    /// <summary>190 화염병 (Mage) - 선택한 적에게 화염 4 부여</summary>
    public class MolotovItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override bool NeedsTargetingMode => true;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.EnterItemEnemyTargetingMode(
                enemy =>
                {
                    ctx.Board.ApplyStatus(enemy, StatusEffectType.Fire, 4);
                    Debug.Log($"[MolotovItem] {enemy.Name}에게 화염 4 부여");
                },
                null, onCancel: null,
                onConfirmedUse: ctx.OnConfirmedUse
            );
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Uncommon (2xx) — 공용
    // ════════════════════════════════════════════════════════════════════

    /// <summary>201 카드더미 - 카드 2장 드로우</summary>
    public class DrawPileItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.DrawExtraCards(2);
            Debug.Log("[DrawPileItem] 카드 2장 드로우");
        }
    }

    /// <summary>202 힘 포션 - 이번 전투 힘 +3</summary>
    public class StrengthPotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.AddStrength(2);
            Debug.Log("[StrengthPotionItem] 힘 +2");
        }
    }

    /// <summary>204 강화 포션 - 손패 모든 카드 강화 (영구)</summary>
    public class UpgradePotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.UpgradeAllHandCards();
            Debug.Log("[UpgradePotionItem] 손패 모든 카드 강화");
        }
    }

    /// <summary>206 체력 포션 - 최대 HP의 20% 회복</summary>
    public class HealthPotionItem : ItemEffect
    {
        public override void OnItemUsed(ItemUseContext ctx)
        {
            int amount = Mathf.RoundToInt(ctx.GM.MaxHP * 0.2f);
            ctx.GM.Heal(amount);
            Debug.Log($"[HealthPotionItem] HP +{amount} (20%)");
        }
    }

    // ── Uncommon 직업 전용 ────────────────────────────────────────────

    /// <summary>290 뼈피리 (Mage) - 자신을 제외한 모든 아군 유닛의 체력 +10</summary>
    public class BoneFluteItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            int buffed = 0;
            foreach (var ally in ctx.Board.GetAllies())
            {
                ally.MaxHP     += 10;
                ally.CurrentHP += 10;
                ally.UI?.UpdateHP(ally.CurrentHP, ally.MaxHP);
                buffed++;
            }
            Debug.Log($"[BoneFluteItem] 아군 {buffed}마리 체력 +10");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Rare (3xx) — 공용
    // ════════════════════════════════════════════════════════════════════

    public class GhostPotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override bool NeedsTargetingMode => true;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.EnterItemEnemyTargetingMode(
                enemy =>
                {
                    ctx.Board.ApplyStatus(enemy, StatusEffectType.Fear, 1);
                    Debug.Log($"[GhostPotionItem] {enemy.Name}에게 공포 부여");
                },
                null, onCancel: null,
                onConfirmedUse: ctx.OnConfirmedUse
            );
        }
    }

    /// <summary>109 섬광탄 - 선택한 적 기절 (1턴)</summary>
    public class FlashbangItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override bool NeedsTargetingMode => true;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.EnterItemEnemyTargetingMode(
                enemy =>
                {
                    ctx.Board.ApplyStatus(enemy, StatusEffectType.Stun, 1);
                    Debug.Log($"[FlashbangItem] {enemy.Name} 기절 1턴");
                },
                null, onCancel: null,
                onConfirmedUse: ctx.OnConfirmedUse
            );
        }
    }

    /// <summary>302 폭탄 - 선택한 셀 기준 3×3 범위에 피해 10</summary>
    public class BombItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override bool NeedsTargetingMode => true;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.EnterItemAreaTargetingMode(
                pos =>
                {
                    ctx.Board.DealDamageInArea(pos, 1, 10);
                    Debug.Log($"[BombItem] 폭탄 폭발! {pos} 기준 3×3 범위 10 피해");
                },
                onCancel: null,
                onConfirmedUse: ctx.OnConfirmedUse
            );
        }
    }

    /// <summary>303 성수 - 자신에게 걸린 모든 해로운 효과 제거</summary>
    public class HolyWaterItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.RemovePlayerDebuffs();
            Debug.Log("[HolyWaterItem] 모든 디버프 제거");
        }
    }

    /// <summary>304 사기 주사위 - 액션·파워·이동 카드 각 1장씩 손패에 추가 (비용 0)</summary>
    public class LuckyDiceItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            var pool = CardRegistry.GetRewardPool(character)
                .Where(c => !c.IsUpgraded && c.ClassDigit != 1).ToList();

            AddRandomOfType(ctx, pool, CardType.Action);
            AddRandomOfType(ctx, pool, CardType.Power);
            AddRandomOfType(ctx, pool, CardType.Move);
            Debug.Log("[LuckyDiceItem] 액션·파워·이동 카드 각 1장 추가");
        }

        private static void AddRandomOfType(ItemUseContext ctx, List<CardData> pool, CardType type)
        {
            var candidates = pool.Where(c => c.CardTypeFromCode == type).ToList();
            if (candidates.Count == 0) return;
            var picked = candidates[Random.Range(0, candidates.Count)];
            ctx.Board.AddCardToHandFree(picked);
        }
    }

    /// <summary>305 무지개 포션 - 자기 슬롯을 비운 뒤 빈 슬롯을 무작위 포션으로 채웁니다</summary>
    public class RainbowPotionItem : ItemEffect
    {
        private const int TotalItemSlots = 10;  // InGameUIController: ItemCols(5) * ItemsPerCol(2)

        public override void OnItemUsed(ItemUseContext ctx)
        {
            // GameManager.UseItem은 items 리스트에서만 제거하고 슬롯 UI는 ConfirmUseItemPanel이 나중에 파괴한다.
            // 이 시점에 자기 슬롯 UI는 여전히 ItemData=305를 들고 있어 AddItem이 그 자리를 건너뛰면 1칸이 빈다.
            // → 먼저 자기 슬롯을 비워 AddItem이 사용할 수 있도록 한다.
            var ui = DeckRoguelike.UI.InGameUIController.Instance;
            var srcSlot = ui?.GetActiveConfirmSlot();
            if (srcSlot != null) ui.ClearItemSlot(srcSlot);

            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            var pool = ItemRegistry.GetForCharacter(character, bossItem: false);
            // 무지개 포션 자체는 제외하여 무한 루프 방지
            pool.RemoveAll(i => i.itemCode == 305);

            ItemLibrary.Shuffle(pool);

            int emptySlots = Mathf.Max(0, TotalItemSlots - ctx.GM.Items.Count);
            int count = Mathf.Min(emptySlots, pool.Count);
            for (int i = 0; i < count; i++)
                ctx.GM.AddItem(pool[i]);
            Debug.Log($"[RainbowPotionItem] 빈 슬롯 {emptySlots}개 중 {count}개를 무작위 포션으로 채움");
        }
    }

    /// <summary>208 포탈건 - 선택한 적과 플레이어의 위치를 맞바꿉니다 (카드 11301 위치변경술과 동일).</summary>
    public class PortalGunItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override bool NeedsTargetingMode => true;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.EnterItemEnemyTargetingMode(
                enemy =>
                {
                    bool swapped = ctx.Board.SwapPlayerEnemy(enemy);
                    Debug.Log(swapped
                        ? $"[PortalGunItem] {enemy.Name}와 위치 교환"
                        : $"[PortalGunItem] {enemy.Name}와 위치 교환 실패 (멀티셀 적 등)");
                },
                null, onCancel: null,
                onConfirmedUse: ctx.OnConfirmedUse
            );
        }
    }

    /// <summary>308 소생의 팬던트 - 사용 시 패시브 등록, 사망 시 진짜 소모되며 최대 HP 30% 회복</summary>
    public class RevivePendantItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            // GameManager.UseItem이 이미 인벤토리에서 제거했으므로 사망 시까지 유지되도록 다시 추가한다.
            // (사망 시 BoardController가 RemoveItem으로 진짜 소모)
            ctx.GM.AddItem(Data);
            ctx.Board.RegisterRevivePassive(Data, 0.3f);
            Debug.Log("[RevivePendantItem] 소생의 팬던트 패시브 활성화 (사망 시 소모)");
        }
    }

    // ── Rare 직업 전용 ────────────────────────────────────────────────

    /// <summary>370 철가면 (Warrior) - 이번 턴 데미지 2배, 받는 피해 2배</summary>
    public class IronMaskItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.SetTurnDamageMultipliers(2f, 2f);
            DeckRoguelike.UI.InGameUIController.Instance?.SetPlayerEffect(
                "iron_mask",
                Data?.icon,
                2,
                Data?.itemName ?? "철가면",
                Data?.description ?? "이번 턴 데미지·받는 피해 2배");
            Debug.Log("[IronMaskItem] 이번 턴 데미지 2배 / 받는 피해 2배");
        }
    }

    /// <summary>280 군번줄 (Gunner) - 회피 1회 획득</summary>
    public class DogTagItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Board.AddDodge(1);
            Debug.Log("[DogTagItem] 회피 1회 획득");
        }
    }

}
