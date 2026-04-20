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
                { 101, () => new BombItem() },             // 폭탄
                { 102, () => new ShieldPotionItem() },     // 방어 포션
                { 103, () => new SpeedPotionItem() },      // 신속 포션
                { 104, () => new AttackPotionItem() },     // 공격 포션
                { 105, () => new ColorlessPotionItem() },  // 무색 포션
                { 106, () => new SkillPotionItem() },      // 스킬 포션
                { 107, () => new ArrowItem() },            // 화살
                { 109, () => new EnergyPotionItem() },     // 에너지 포션
                { 119, () => new MovePotionItem() },       // 이동 포션

                // Common - 직업 전용
                { 170, () => new AirStrikeItem() },        // 공중 포격 (Warrior)
                { 180, () => new AdrenalineItem() },       // 아드레날린 (Gunner)
                { 190, () => new MolotovItem() },          // 화염병 (Mage)

                // ── Uncommon (2xx) ──────────────────────────────────────
                { 201, () => new AceCardItem() },          // 에이스 카드
                { 202, () => new StrengthPotionItem() },   // 힘 포션
                { 203, () => new ChocolateBarItem() },     // 초코바
                { 204, () => new UpgradePotionItem() },    // 강화 포션
                { 205, () => new SmokeBombItem() },        // 연막탄
                { 206, () => new HealthPotionItem() },     // 체력 포션
                { 207, () => new RecycledPotionItem() },   // 재활용된 포션
                { 208, () => new OmenBookItem() },         // 예지의 고서
                { 209, () => new ReusePotionItem() },      // 재사용 포션

                // Uncommon - 직업 전용
                { 270, () => new SteelPotionItem() },      // 강철화 포션 (Warrior)
                { 280, () => new AmmoItem() },             // 탄약 (Gunner)
                { 290, () => new BoneFluteItem() },        // 뼈피리 (Mage)

                // ── Rare (3xx) ──────────────────────────────────────────
                { 301, () => new RagePotionItem() },       // 분노의 포션
                { 302, () => new PhantomDustItem() },      // 환상의 가루
                { 303, () => new RouletteItem() },         // 돌림판
                { 304, () => new HolyWaterItem() },        // 성수
                { 305, () => new LuckyDiceItem() },        // 사기 주사위
                { 306, () => new RainbowPotionItem() },    // 무지개 포션
                { 307, () => new TeleportPotionItem() },   // 순간이동 포션
                { 308, () => new FlashbangItem() },        // 섬광탄
                { 309, () => new RevivePendantItem() },    // 소생의 팬던트

                // Rare - 직업 전용
                { 370, () => new IronMaskItem() },         // 철가면 (Warrior)
                { 380, () => new DogTagItem() },           // 군번줄 (Gunner)
                { 390, () => new DryIceItem() },           // 드라이아이스 (Mage)
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

        private static List<CardData> GetCardsByType(System.Func<CardData, bool> filter, int maxCount)
        {
            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            var pool = CardRegistry.GetRewardPool(character)
                .Where(c => !c.IsUpgraded && filter(c))
                .ToList();
            Shuffle(pool);
            return pool.Take(maxCount).ToList();
        }

        private static void Shuffle<T>(List<T> list)
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

    /// <summary>101 폭탄 - 선택한 셀 기준 3×3 범위에 피해 10</summary>
    public class BombItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override bool NeedsTargetingMode => true;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.EnterItemAreaTargetingMode(
                pos =>
                {
                    ctx.Combat.DealDamageInArea(pos, 1, 10);
                    Debug.Log($"[BombItem] 폭탄 폭발! {pos} 기준 3×3 범위 10 피해");
                },
                onCancel: null,
                onConfirmedUse: ctx.OnConfirmedUse
            );
        }
    }

    /// <summary>102 방어 포션 - 방어도 12 획득</summary>
    public class ShieldPotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.AddBlock(12);
            Debug.Log("[ShieldPotionItem] 방어도 +12");
        }
    }

    /// <summary>103 신속 포션 - 카드 3장 드로우</summary>
    public class SpeedPotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.DrawExtraCards(3);
            Debug.Log("[SpeedPotionItem] 카드 3장 드로우");
        }
    }

    /// <summary>104 공격 포션 - 공격 카드 3장 중 1장 선택, 비용 0</summary>
    public class AttackPotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            var cards = GetAttackCards(3);
            if (cards.Count == 0) return;
            ctx.Combat.OpenItemCardSelection(cards, picked =>
            {
                if (picked != null) ctx.Combat.AddCardToHandFree(picked);
            });
        }

        private static List<CardData> GetAttackCards(int count)
        {
            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            var pool = CardRegistry.GetRewardPool(character)
                .Where(c => !c.IsUpgraded && c.CardTypeFromCode == CardType.Attack)
                .ToList();
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            return pool.Take(count).ToList();
        }
    }

    /// <summary>105 무색 포션 - 중립(ClassDigit==1) 카드 3장 중 1장 선택, 비용 0</summary>
    public class ColorlessPotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            var pool = CardRegistry.GetRewardPool(CharacterType.Warrior) // neutral = class 1
                .Where(c => !c.IsUpgraded && c.ClassDigit == 1)
                .ToList();
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            var cards = pool.Take(3).ToList();
            if (cards.Count == 0) return;
            ctx.Combat.OpenItemCardSelection(cards, picked =>
            {
                if (picked != null) ctx.Combat.AddCardToHandFree(picked);
            });
        }
    }

    /// <summary>106 스킬 포션 - 스킬 카드 3장 중 1장 선택, 비용 0</summary>
    public class SkillPotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            var pool = CardRegistry.GetRewardPool(character)
                .Where(c => !c.IsUpgraded && c.CardTypeFromCode == CardType.Skill)
                .ToList();
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            var cards = pool.Take(3).ToList();
            if (cards.Count == 0) return;
            ctx.Combat.OpenItemCardSelection(cards, picked =>
            {
                if (picked != null) ctx.Combat.AddCardToHandFree(picked);
            });
        }
    }

    /// <summary>107 화살 - 선택한 적에게 피해 20</summary>
    public class ArrowItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override bool NeedsTargetingMode => true;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.EnterItemEnemyTargetingMode(
                enemy =>
                {
                    ctx.Combat.DamageEnemy(enemy, 20);
                    Debug.Log($"[ArrowItem] 화살 발사! {enemy.Name} 20 피해");
                },
                null, onCancel: null,
                onConfirmedUse: ctx.OnConfirmedUse
            );
        }
    }

    /// <summary>109 에너지 포션 - 에너지 2 획득</summary>
    public class EnergyPotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.AddEnergy(2);
            Debug.Log("[EnergyPotionItem] 에너지 +2");
        }
    }

    /// <summary>119 이동 포션 - 인접 1칸 이동 (우클릭 취소 가능)</summary>
    public class MovePotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override bool NeedsTargetingMode => true;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.SetItemMoveModeConfirmCallback(ctx.OnConfirmedUse);
            ctx.Combat.EnterItemMoveMode();
            Debug.Log("[MovePotionItem] 이동 포션 발동 - 이동할 칸을 선택하세요");
        }
    }

    // ── Common 직업 전용 ─────────────────────────────────────────────────

    /// <summary>170 공중 포격 (Warrior) - 모든 타일에 피해 10</summary>
    public class AirStrikeItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.DealDamageToAllEnemies(10);
            Debug.Log("[AirStrikeItem] 공중 포격! 전체 10 피해");
        }
    }

    /// <summary>180 아드레날린 (Gunner) - 힘 +3, 1씩 3번 피해</summary>
    public class AdrenalineItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.AddStrength(3);
            for (int i = 0; i < 3; i++)
                ctx.Combat.ApplyDamageToPlayer(1);
            Debug.Log("[AdrenalineItem] 힘 +3, 피해 1×3 수령");
        }
    }

    /// <summary>190 화염병 (Mage) - 선택한 적에게 화염 5 부여</summary>
    public class MolotovItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override bool NeedsTargetingMode => true;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.EnterItemEnemyTargetingMode(
                enemy =>
                {
                    ctx.Combat.ApplyStatus(enemy, StatusEffectType.Fire, 5);
                    Debug.Log($"[MolotovItem] {enemy.Name}에게 화염 5 부여");
                },
                null, onCancel: null,
                onConfirmedUse: ctx.OnConfirmedUse
            );
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Uncommon (2xx) — 공용
    // ════════════════════════════════════════════════════════════════════

    /// <summary>201 에이스 카드 - 손패에서 1장 선택, 이번 전투 비용 0</summary>
    public class AceCardItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            var hand = new List<CardData>(ctx.Combat.GetHand());
            if (hand.Count == 0) return;
            ctx.Combat.OpenItemCardSelection(hand, picked =>
            {
                if (picked != null) ctx.Combat.SetHandCardFree(picked);
            });
        }
    }

    /// <summary>202 힘 포션 - 힘 +3</summary>
    public class StrengthPotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.AddStrength(3);
            Debug.Log("[StrengthPotionItem] 힘 +3");
        }
    }

    /// <summary>203 초코바 - 이번 턴 힘 +8 (턴 종료 시 사라짐)</summary>
    public class ChocolateBarItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.AddTempStrength(8);
            Debug.Log("[ChocolateBarItem] 이번 턴 힘 +8");
        }
    }

    /// <summary>204 강화 포션 - 손패 모든 카드 강화 (영구)</summary>
    public class UpgradePotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.UpgradeAllHandCards();
            Debug.Log("[UpgradePotionItem] 손패 모든 카드 강화");
        }
    }

    /// <summary>205 연막탄 - 보스 아닌 전투에서 도망</summary>
    public class SmokeBombItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx)
            => ctx.InCombat && GameManager.Instance?.IsBossEncounter == false;

        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.EscapeCombat();
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

    /// <summary>207 재활용된 포션 - 버린 더미에서 1장 선택, 비용 0</summary>
    public class RecycledPotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            var discardView = DeckManager.Instance?.GetDiscardPileForView();
            if (discardView == null || discardView.Count == 0) return;

            // 최대 6장까지 표시
            var show = discardView.Take(Mathf.Min(6, discardView.Count)).ToList();
            ctx.Combat.OpenItemCardSelection(show, picked =>
            {
                if (picked == null) return;
                // 버린 더미에서 제거 후 손패에 비용 0으로 추가
                DeckManager.Instance?.GetDiscardPileForView(); // 뷰는 복사본이므로 실 제거는 아래
                ctx.Combat.AddCardToHandFree(picked);
                Debug.Log($"[RecycledPotionItem] {picked.cardName} 재활용");
            });
        }
    }

    /// <summary>208 예지의 고서 - 드로우 더미에서 1장 선택, 비용 0</summary>
    public class OmenBookItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            var drawView = DeckManager.Instance?.GetDrawPileForView();
            if (drawView == null || drawView.Count == 0) return;

            var show = drawView.Take(Mathf.Min(6, drawView.Count)).ToList();
            ctx.Combat.OpenItemCardSelection(show, picked =>
            {
                if (picked == null) return;
                ctx.Combat.AddCardToHandFree(picked);
                Debug.Log($"[OmenBookItem] {picked.cardName} 드로우 더미에서 가져옴");
            });
        }
    }

    /// <summary>209 재사용 포션 - 다음 카드를 2번 사용</summary>
    public class ReusePotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.SetNextCardDoublePlay();
            Debug.Log("[ReusePotionItem] 다음 카드 2회 실행 준비");
        }
    }

    // ── Uncommon 직업 전용 ────────────────────────────────────────────

    /// <summary>270 강철화 포션 (Warrior) - 방어도 3배</summary>
    public class SteelPotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.TripleCurrentBlock();
            Debug.Log("[SteelPotionItem] 방어도 3배!");
        }
    }

    /// <summary>280 탄약 (Gunner) - 발사(cardCode 31100) 3장, 비용 0</summary>
    public class AmmoItem : ItemEffect
    {
        private const int ShotCardCode = 31100;

        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            var shotCard = CardRegistry.GetCard(ShotCardCode);
            if (shotCard == null)
            {
                Debug.LogWarning("[AmmoItem] 발사 카드(31100)를 찾을 수 없습니다.");
                return;
            }
            for (int i = 0; i < 3; i++)
                ctx.Combat.AddCardToHandFree(shotCard);
            Debug.Log("[AmmoItem] 발사 카드 3장 추가 (비용 0)");
        }
    }

    /// <summary>290 뼈피리 (Mage) - 아군 스켈레톤의 최대 HP +5</summary>
    public class BoneFluteItem : ItemEffect
    {
        public override void OnItemUsed(ItemUseContext ctx)
        {
            if (!ctx.InCombat) return;
            int buffed = 0;
            foreach (var ally in ctx.Combat.GetAllies())
            {
                if (ally.Name != "스켈레톤") continue;
                ally.MaxHP     += 5;
                ally.CurrentHP += 5;
                ally.UI?.UpdateHP(ally.CurrentHP, ally.MaxHP);
                buffed++;
            }
            Debug.Log($"[BoneFluteItem] 스켈레톤 {buffed}마리 최대 HP +5");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Rare (3xx) — 공용
    // ════════════════════════════════════════════════════════════════════

    /// <summary>301 분노의 포션 - 다음 공격 카드 피해 3배</summary>
    public class RagePotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.SetNextAttackMultiplier(3);
            Debug.Log("[RagePotionItem] 다음 공격 카드 3배 준비");
        }
    }

    /// <summary>302 환상의 가루 - 손 가득 드로우 후 모든 카드 비용 무작위화</summary>
    public class PhantomDustItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.DrawUntilFull();
            ctx.Combat.RandomizeAllHandCosts();
            Debug.Log("[PhantomDustItem] 손 가득 드로우 + 비용 무작위화");
        }
    }

    /// <summary>303 돌림판 - 드로우 더미 맨 위 3장 자동 실행</summary>
    public class RouletteItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.PlayTopCardsFromDraw(3);
            Debug.Log("[RouletteItem] 드로우 더미 상위 3장 자동 실행");
        }
    }

    /// <summary>304 성수 - 자신에게 걸린 모든 해로운 효과 제거</summary>
    public class HolyWaterItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.RemovePlayerDebuffs();
            Debug.Log("[HolyWaterItem] 모든 디버프 제거");
        }
    }

    /// <summary>305 사기 주사위 - 공격·스킬·파워 카드 각 1장씩 손패에 추가 (비용 0)</summary>
    public class LuckyDiceItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            var pool = CardRegistry.GetRewardPool(character)
                .Where(c => !c.IsUpgraded).ToList();

            AddRandomOfType(ctx, pool, CardType.Attack);
            AddRandomOfType(ctx, pool, CardType.Skill);
            AddRandomOfType(ctx, pool, CardType.Power);
            Debug.Log("[LuckyDiceItem] 공격·스킬·파워 카드 각 1장 추가");
        }

        private static void AddRandomOfType(ItemUseContext ctx, List<CardData> pool, CardType type)
        {
            var candidates = pool.Where(c => c.CardTypeFromCode == type).ToList();
            if (candidates.Count == 0) return;
            var picked = candidates[Random.Range(0, candidates.Count)];
            ctx.Combat.AddCardToHandFree(picked);
        }
    }

    /// <summary>306 무지개 포션 - 무작위 아이템 3개를 인벤토리에 추가</summary>
    public class RainbowPotionItem : ItemEffect
    {
        public override void OnItemUsed(ItemUseContext ctx)
        {
            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            var pool = ItemRegistry.GetForCharacter(character, bossItem: false);
            // 이미 보유 중인 동일 코드 제외
            var owned = new System.Collections.Generic.HashSet<int>(
                ctx.GM.Items.Select(i => i.itemCode));
            pool.RemoveAll(i => owned.Contains(i.itemCode));

            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            int count = Mathf.Min(3, pool.Count);
            for (int i = 0; i < count; i++)
                ctx.GM.AddItem(pool[i]);
            Debug.Log($"[RainbowPotionItem] 무작위 아이템 {count}개 추가");
        }
    }

    /// <summary>307 순간이동 포션 - 보드 내 빈 칸으로 순간이동 (취소 불가)</summary>
    public class TeleportPotionItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.EnterItemTeleportMode();
            Debug.Log("[TeleportPotionItem] 순간이동 포션 발동 - 이동할 칸을 선택하세요");
        }
    }

    /// <summary>308 섬광탄 - 선택한 적 기절 (1턴)</summary>
    public class FlashbangItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override bool NeedsTargetingMode => true;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.EnterItemEnemyTargetingMode(
                enemy =>
                {
                    ctx.Combat.ApplyStatus(enemy, StatusEffectType.Stun, 1);
                    Debug.Log($"[FlashbangItem] {enemy.Name} 기절 1턴");
                },
                null, onCancel: null,
                onConfirmedUse: ctx.OnConfirmedUse
            );
        }
    }

    /// <summary>309 소생의 팬던트 - 사망 시 자동 발동, 최대 HP 30% 회복 (패시브)</summary>
    public class RevivePendantItem : ItemEffect
    {
        public override void OnItemUsed(ItemUseContext ctx)
        {
            if (!ctx.InCombat) return;
            ctx.Combat.RegisterRevivePassive(Data, 0.3f);
            Debug.Log("[RevivePendantItem] 소생의 팬던트 패시브 활성화");
        }
    }

    // ── Rare 직업 전용 ────────────────────────────────────────────────

    /// <summary>370 철가면 (Warrior) - 이번 턴 데미지 2배, 받는 피해 2배</summary>
    public class IronMaskItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.SetTurnDamageMultipliers(2f, 2f);
            Debug.Log("[IronMaskItem] 이번 턴 데미지 2배 / 받는 피해 2배");
        }
    }

    /// <summary>380 군번줄 (Gunner) - 회피 1회 획득</summary>
    public class DogTagItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.AddDodge(1);
            Debug.Log("[DogTagItem] 회피 1회 획득");
        }
    }

    /// <summary>390 드라이아이스 (Mage) - 선택한 적 빙결 (1턴)</summary>
    public class DryIceItem : ItemEffect
    {
        public override bool CanUse(ItemUseContext ctx) => ctx.InCombat;
        public override bool NeedsTargetingMode => true;
        public override void OnItemUsed(ItemUseContext ctx)
        {
            ctx.Combat.EnterItemEnemyTargetingMode(
                enemy =>
                {
                    ctx.Combat.ApplyStatus(enemy, StatusEffectType.Freeze, 1);
                    Debug.Log($"[DryIceItem] {enemy.Name} 빙결 1턴");
                },
                null, onCancel: null,
                onConfirmedUse: ctx.OnConfirmedUse
            );
        }
    }
}
