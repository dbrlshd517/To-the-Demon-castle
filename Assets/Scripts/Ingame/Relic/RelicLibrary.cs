using DeckRoguelike.Cards;
using DeckRoguelike.Combat;
using DeckRoguelike.Core;
using DeckRoguelike.UI;
using System.Collections.Generic;
using System.Linq;
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
        // relicCode → RelicEffect 생성 팩토리. CSV (Assets/Editor/RelicDataTemplate.csv) 기준.
        private static readonly Dictionary<int, System.Func<RelicEffect>> effectFactories =
            new Dictionary<int, System.Func<RelicEffect>>
            {
                // 직업 시작 유물
                {  70, () => new WarriorStartHealRelic() },      // 전사 시작
                {  80, () => new GunnerStartKillDrawRelic()  },      // 거너 시작
                {  90, () => new MageStartTeleportRelic() },     // 메이지 시작

                // 1xx 희귀 유물 (Uncommon)
                { 100, () => new PostRestStrengthRelic() },      // 휴식 후 전투 힘 +4
                { 101, () => new PostShopStrengthRelic() },      // 상점 후 전투 힘 +4
                { 102, () => new PostMysteryStrengthRelic() },   // 미지 후 전투 힘 +2
                { 103, () => new RestBonusRelic() },             // 휴식의 부적 — 휴식 HP +15
                { 104, () => new ShopBonusRelic() },             // 상점의 부적 — 상점 HP +15
                { 105, () => new MysteryBonusRelic() },          // 미지의 부적 — 미지 HP +7
                { 106, () => new MaxHPRelic() },                 // 최대 HP +7
                { 107, () => new BloodVialRelic() },             // 피의 약병 — 전투 시작 HP +3
                { 108, () => new TreasureMapRelic() },           // 금속 탐지기
                { 109, () => new BossStrengthRelic() },          // 보스 전투 힘 +3
                { 110, () => new StrengthRelic() },              // 힘 +1
                { 111, () => new ThornsRelic() },                // 공격받을 때 적 피해 3
                { 112, () => new BurningHeartRelic() },          // 불타는 심장 — 전투 시작 카드 +1
                { 113, () => new PotionSlotRelic() },            // 포션 상자 — 슬롯 +2
                { 114, () => new MapMoveGoldRelic() },           // 맵 이동 시 +25G (상점 방문 후 비활성화)
                { 115, () => new CardObtainGoldRelic() },        // 덱 카드 추가 시 +20G
                { 116, () => new RestHealBonusRelic() },         // 휴식 시 채력 전부 회복
                { 117, () => new NoActionNextDrawRelic() },      // 액션 미사용 → 다음턴 +1
                { 118, () => new NoMoveNextDrawRelic() },        // 이동 미사용 → 다음턴 +1
                { 119, () => new NoPowerNextDrawRelic() },       // 파워 미사용 → 다음턴 +1
                { 170, () => new LowHpStrengthRelic() },         // 채력 50%↓ 힘 +3
                { 190, () => new ExtraBurnRelic() },             // 화염 부여 시 +1 추가

                // 2xx 영웅 유물 (Rare)
                { 200, () => new HalfHpVictoryHealRelic() },     // 전투 종료 시 HP 50%↓ → HP +12
                { 201, () => new FirstActionBonusRelic() },      // 선제의 일격 — 첫 피해 +8
                { 202, () => new MapMoveUpgradeRelic() },        // 나침반 — 맵 이동 카드 강화
                { 203, () => new ReflectRelic() },               // 반사
                { 204, () => new CardRemoveOnObtainRelic() },    // 카드 1장 제거
                { 205, () => new EveryThreeTurnDrawRelic() },    // 3턴마다 카드 +1
                { 206, () => new ExtraCardRewardChoiceRelic() }, // 카드 보상 선택지 +1
                { 207, () => new ActionChoiceRelic() },          // 각인된 부적 (액션)
                { 208, () => new MoveChoiceRelic() },            // 각인된 부적 (이동)
                { 209, () => new PowerChoiceRelic() },           // 각인된 부적 (파워)
                { 210, () => new UpgradeStartingDeckRelic() },   // 시작 카드 전부 강화
                { 211, () => new ShuffleStrengthRelic() },       // 셔플마다 힘 +1
                { 212, () => new RandomUpgradeOnObtainRelic() }, // 획득 시 카드 2장 무작위 강화
                { 213, () => new ShopBetterDiscountRelic() },    // 상점 품절X + 30% 할인
                { 214, () => new DangerCombatEnemyHPRelic() },   // 위험 전투 적 HP 25%↓
                { 215, () => new RestFullAccessRelic() },        // 휴식+강화 둘 다
                { 216, () => new FirstHitDrawRelic() },          // 첫 공격받을때 카드 3장
                { 217, () => new ArtisanHammerRelic() },         // 장인의 망치 — 휴식 두 장 강화
                { 218, () => new AnyCardCountDrawRelic() },      // 수련의 일지 — 10장 사용마다 +1
                { 219, () => new KillHealRelic() },              // 적 처치 시 HP +2
                { 270, () => new DamageReductionRelic() },       // 피해 -1
                { 290, () => new AllyHpBoostRelic() },           // 소환수 체력 +5

                // 3xx 영웅 유물 (Rare)
                { 300, () => new UpgradedRewardRelic() },        // 카드 보상 강화 상태
                { 301, () => new FreeMapMoveRelic() },           // 자유의 발걸음
                { 302, () => new RandomSpawnRelic() },           // 예측 불허
                { 303, () => new DiscardStrengthRelic() },       // 매턴 버리는 카드 수만큼 힘
                { 304, () => new CombatStartFearRelic() },       // 공포의 위압
                { 305, () => new TurnEndRandomUpgradeRelic() },  // 턴 끝 무작위 카드 강화
                { 306, () => new ArmorBreakFearRelic() },        // 방어도 파괴 시 공포
                { 309, () => new PowerCardOnCombatStartRelic() },// 강화된 파워카드 생성
                { 310, () => new StunOnAttackedRelic() },        // 공격받으면 기절
                { 311, () => new DirectionalDamageRelic("위") },  // 위에서 2배
                { 312, () => new DirectionalDamageRelic("아래") },// 아래에서 2배
                { 313, () => new ExcessDamageTransferRelic() },  // 초과 피해 전이
                { 314, () => new DirectionalDamageRelic("뒤") },  // 뒤에서 3배
                { 315, () => new CombatStartAoeDamageRelic() },  // 전투 시작 시 적 전체 10 피해
                { 316, () => new EnemyDamagedThornsRelic() },    // 적이 HP 잃을 때마다 5 피해
                { 317, () => new PowerCountStrengthRelic() },    // 파워의 단련 — 파워 1장당 힘 +1
                { 318, () => new ActionCountStrengthRelic() },   // 액션의 단련 — 액션 3장마다 힘 +1
                { 319, () => new ReviveRelic() },                // 채력 0 → 부활

                { 370, () => new MoveCountStrengthRelic() },     // 이동의 단련
                { 380, () => new DogTagRelic() },                // 군번줄 — 회피 1
                { 390, () => new SummonCountStrengthRelic() },   // 소환 카드 사용 시 힘 +1

                // 4xx 상점 전용 일반
                { 400, () => new DrawOrderRelic() },             // 드로우 순서 표시
                { 401, () => new PotionFiveRelic() },            // 포션 5개 획득
                { 402, () => new AllClassRewardRelic() },        // 모든 직업 카드 보상
                { 403, () => new CurseStrengthRelic() },         // 저주 한 장당 힘 +2

                // 5xx 상점 전용 희귀
                { 500, () => new ShopDiscountRelic() },          // 50% 할인
                { 501, () => new CurseConsumableRelic() },       // 저주 사용 시 소멸
                { 502, () => new TripleDebuffRelic() },          // 매턴 1회 해로운 효과 3배
                { 503, () => new FullHealRelic(6) }, // HP +6, 전부 회복

                // 6xx 상점 전용 희귀 (계속)
                { 600, () => new CardCopyRelic() },              // 카드 복제
                { 601, () => new ToughnessTenTurnsRelic() },     // 10턴 강인함
                { 602, () => new ChooseAnyCardRelic() },         // 원하는 카드 1장
                { 603, () => new TwentyTurnSweepRelic() },       // 20턴 → 모든 적 처치

                // 9xx 보스 유물
                { 900, () => new PandoraBoxRelic() },            // 판도라의 상자
                { 901, () => new EliteOrBossDrawRelic() },       // 위기의 직감
                { 902, () => new HiddenInfoDrawRelic() },        // 어둠의 안개
                { 903, () => new KeepHandPlusDrawRelic() },      // 영원의 손
                { 904, () => new PotionDoubleRelic() },          // 연금술사의 손길 — 아이템 효과 2배
                { 905, () => new NoPotionDrawRelic() },          // 금욕의 서약 — 매턴 +1, 아이템 차단
                { 906, () => new RarityUpgradeRelic() },         // 대격변
                { 907, () => new CursedPowerRelic() },           // 저주받은 힘
                { 908, () => new NoUpgradeDrawRelic() },         // 망각의 인장 — 매턴 +1, 강화 차단
                { 909, () => new EmptyHandDrawRelic() },         // 마지막 한발
                { 910, () => new EnemyAttackBuffDrawRelic() },   // 분노한 적
                { 911, () => new NoRestHealDrawRelic() },        // 수면의 단절
                { 970, () => new WarriorBossHealRelic() },       // 전사 보스
                { 971, () => new HpLossDrawRelic() },            // HP 잃을 때 카드 +1
                { 972, () => new NoExhaustRelic() },             // 소멸 방지
                { 980, () => new GunnerBossDrawRelic() },        // 거너 보스
                { 981, () => new AreaDamageDoubleRelic() },      // 범위 피해 2배
                { 982, () => new MoveDrawRelic() },              // 이동 시 카드 +1
                { 990, () => new MageBossTeleportRelic() },      // 메이지 보스
                { 991, () => new SkeletonOnKillRelic() },        // 적 처치 해골 소환
                { 992, () => new BurnTransferOnKillRelic() },    // 적 처치 시 화상 전이
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

    // ── Common (1xx) ───────────────────────────────────────────────────────────

    /// <summary>피의 약병 (107): 전투 시작 시 HP +3 회복</summary>
    public class BloodVialRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            GameManager.Instance?.Heal(3);
            Debug.Log("[BloodVialRelic] 전투 시작: HP +3");
        }
    }

    /// <summary>반사 (102): 공격받은 만큼 피해를 공격한 적에게 되돌려줍니다.</summary>
    public class ReflectRelic : RelicEffect
    {
        public override void OnPlayerDamagedBy(RelicCombatContext ctx, int damage, EnemyInstance attacker)
        {
            if (attacker == null || attacker.CurrentHP <= 0) return;
            ctx.Board.DamageEnemy(attacker, damage);
            Debug.Log($"[ReflectRelic] {damage} 피해 반사 → {attacker.Name}");
        }
    }

    /// <summary>103: 휴식 장소에 진입하면 hp를 15 회복합니다.
    /// 휴식(회복) 카드 사용 여부와 무관하게, 상점 유물 104(OnShopOpen)와 대칭으로 진입 시 자동 발동합니다.</summary>
    public class RestBonusRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.NonCombat;
        public override void OnRestEnter(GameManager gm)
        {
            gm.Heal(15);
            Debug.Log("[RestBonusRelic] 휴식 진입: HP +15");
        }
    }

    /// <summary>104: 상점에서 hp를 15 획득합니다.</summary>
    public class ShopBonusRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.NonCombat;
        public override void OnShopOpen(GameManager gm)
        {
            gm.Heal(15);
            Debug.Log("[ShopBonusRelic] 상점 보너스: HP +15");
        }
    }

    /// <summary>힘 (105): 전투 시작 시 힘 +1</summary>
    public class StrengthRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx.Board.AddStrength(1);
            Debug.Log("[StrengthRelic] 전투 시작: 힘 +1");
        }
    }

    /// <summary>최대채력 (106): 획득 시 최대 HP +7</summary>
    public class MaxHPRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.Temporary;
        public override void OnRelicObtained(GameManager gm)
        {
            gm.ModifyMaxHP(7);
            Debug.Log("[MaxHPRelic] 획득: 최대 HP +7");
        }
    }

    /// <summary>107: 3턴마다 카드를 한 장 더 뽑습니다.</summary>
    public class EveryThreeTurnDrawRelic : RelicEffect
    {
        public override void OnPlayerTurnStart(RelicCombatContext ctx)
        {
            // OnPlayerTurnStart는 turnNumber++ 후, DrawCards 이전에 호출됨.
            // TurnNumber 3, 6, 9 ...에서 +1
            if (ctx.Board.TurnNumber > 0 && ctx.Board.TurnNumber % 3 == 0)
            {
                ctx.Board.AddThisTurnBonusDraw(1);
                Debug.Log($"[EveryThreeTurnDrawRelic] 턴 {ctx.Board.TurnNumber}: 카드 +1");
            }
        }
    }

    /// <summary>나침반(202) — 마커 유물. BoardController.CreateMapMoveCard()에서 보유 여부를 검사해
    /// 맵 이동 카드를 70000 대신 70001(강화)로 지급한다.</summary>
    public class MapMoveUpgradeRelic : RelicEffect { public override RelicCategory Category => RelicCategory.NonCombat; }

    /// <summary>보스 드로우 (109): 보스 전투 시 첫 턴 드로우 +2</summary>
    public class BossDrawRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsBossEncounter)
            {
                ctx.Board.AddFirstTurnBonusDraw(2);
                Debug.Log("[BossDrawRelic] 보스전 첫 턴 드로우 +2");
            }
        }
    }

    /// <summary>201 선제의 일격: 전투마다 처음으로 데미지를 주면 8 피해를 더 줍니다.</summary>
    public class FirstActionBonusRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx.Board.AddFirstActionBonusDamage(8);
            Debug.Log("[FirstActionBonusRelic] 첫 데미지 +8 피해");
        }
    }

    /// <summary>318: 액션 카드를 3장 사용할 때마다 힘 +1.</summary>
    public class ActionCountStrengthRelic : CardCountRelic
    {
        protected override CardType? FilterType => CardType.Action;
        protected override int       Threshold  => 3;
        protected override string    LogTag     => "[ActionCountStrengthRelic]";
        protected override void      OnThresholdReached(RelicCombatContext ctx) => ctx.Board.AddStrength(1);
    }

    /// <summary>112: 액션 카드를 10장 사용할 때마다 카드 1장 드로우.</summary>
    public class ActionCountDrawRelic : CardCountRelic
    {
        protected override CardType? FilterType => CardType.Action;
        protected override int       Threshold  => 10;
        protected override string    LogTag     => "[ActionCountDrawRelic]";
        protected override void      OnThresholdReached(RelicCombatContext ctx) => ctx.Board.DrawExtraCards(1);
    }

    // ── Uncommon (2xx) ─────────────────────────────────────────────────────────

    /// <summary>
    /// 207/208/209 각인된 부적: 획득 시 현재 덱에서 지정 타입(액션/이동/파워) 카드를 한 장 선택,
    /// 매 전투 시작 손패에 그 카드를 포함시킵니다.
    /// 백버튼으로 픽을 취소하면 GameManager에서 본 유물을 제거하고 BoardController가 60001
    /// (유물 보상 카드)를 손패에 복구하여 재시도 가능합니다.
    /// </summary>
    public abstract class StartingHandCardRelic : RelicEffect
    {
        protected abstract CardType TargetType { get; }

        // 선택된 카드를 런 동안 유지 (RelicEffect 인스턴스 = 런 동안 유효)
        private CardData _pickedCard;

        public override void OnRelicObtained(GameManager gm)
        {
            var dm = DeckRoguelike.Combat.DeckManager.Instance;
            var pool = dm != null
                ? dm.MasterDeck
                    .Where(c => c != null && c.CardTypeFromCode == TargetType)
                    .GroupBy(c => c.cardCode)
                    .Select(g => g.First())
                    .OrderBy(c => c.RarityDigit)
                    .ThenBy(c => c.cardName)
                    .ToList()
                : new List<CardData>();

            if (pool.Count == 0)
            {
                Debug.LogWarning($"[{GetType().Name}] 덱에 {TargetType} 카드가 없어 선택을 건너뜁니다.");
                return;
            }

            int relicCode = Data?.relicCode ?? 0;
            var relicData = Data;
            InGameUIController.Instance?.OpenCardPicker(pool, picked =>
            {
                if (picked == null)
                {
                    Debug.Log($"[{GetType().Name}] 선택 취소 — 유물 제거 + 보상 카드 복구");
                    if (relicCode > 0) GameManager.Instance?.RemoveRelic(relicCode);
                    var board = UnityEngine.Object.FindObjectOfType<BoardController>();
                    board?.RestoreCanceledRelicCard(relicData);
                    return;
                }
                _pickedCard = picked;
                Debug.Log($"[{GetType().Name}] 시작 손패 카드로 '{picked.cardName}' 선택됨");
            });
        }

        public override void OnCombatStart(RelicCombatContext ctx)
        {
            if (_pickedCard == null) return;
            ctx.Board.AddStartingDrawCard(_pickedCard);
        }
    }

    public class ActionChoiceRelic : StartingHandCardRelic { protected override CardType TargetType => CardType.Action; }
    public class MoveChoiceRelic   : StartingHandCardRelic { protected override CardType TargetType => CardType.Move;   }
    public class PowerChoiceRelic  : StartingHandCardRelic { protected override CardType TargetType => CardType.Power;  }
    /// <summary>205: 적의 방어도를 파괴하면 공포를 부여합니다.</summary>
    public class ArmorBreakFearRelic : RelicEffect
    {
        public override void OnEnemyArmorBroken(RelicCombatContext ctx, EnemyInstance enemy)
        {
            if (enemy == null || enemy.CurrentHP <= 0) return;
            ctx.Board.ApplyStatus(enemy, StatusEffectType.Fear, 1);
            Debug.Log($"[ArmorBreakFearRelic] {enemy.Name} 방어도 파괴 → 공포");
        }
    }

    /// <summary>불타는 심장 (206): 전투 시작 시 카드를 한 장 더 뽑습니다.</summary>
    public class BurningHeartRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx.Board.AddFirstTurnBonusDraw(1);
            Debug.Log("[BurningHeartRelic] 전투 시작 드로우 +1");
        }
    }

    /// <summary>208: 카드 보상 무작위 클래스 (CardRewardPanel에서 HasRelic 체크).</summary>
    public class AnyClassRewardRelic : RelicEffect { public override RelicCategory Category => RelicCategory.NonCombat; }

    /// <summary>209: 이동 카드 1장 사용마다 힘 +1.</summary>
    public class MoveCountStrengthRelic : CardCountRelic
    {
        protected override CardType? FilterType => CardType.Move;
        protected override int       Threshold  => 1;
        protected override string    LogTag     => "[MoveCountStrengthRelic]";
        protected override void      OnThresholdReached(RelicCombatContext ctx) => ctx.Board.AddStrength(1);
    }
    
    /// <summary>211: 파워 카드 사용마다 힘 +1.</summary>
    public class PowerCountStrengthRelic : CardCountRelic
    {
        protected override CardType? FilterType => CardType.Power;
        protected override int       Threshold  => 1;
        protected override bool      ShowCounter => false;
        protected override string    LogTag     => "[PowerCountStrengthRelic]";
        protected override void      OnThresholdReached(RelicCombatContext ctx) => ctx.Board.AddStrength(1);
    }

    /// <summary>
    /// 212~215 공통: 지정 타입 카드를 획득하면 그 카드를 강화된 버전으로 교체합니다.
    /// </summary>
    public class ObtainUpgradeRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.NonCombat;
        private readonly CardType _targetType;
        public ObtainUpgradeRelic(CardType targetType) { _targetType = targetType; }

        public override void OnCardObtained(CardData card, ref CardData replacement)
        {
            if (card == null) return;
            if (card.CardTypeFromCode != _targetType) return;
            if (card.IsUpgraded) return;

            var upgraded = CardRegistry.GetCard(card.UpgradedCode);
            if (upgraded == null) return;
            replacement = upgraded.Clone();
            Debug.Log($"[ObtainUpgradeRelic/{_targetType}] '{card.cardName}' → '{replacement.cardName}' 자동 강화");
        }
    }

    /// <summary>216 장인의 망치: 휴식 시 카드 두 장 강화 (CombatController.OnUpgradeClicked에서 HasRelic 체크).</summary>
    public class ArtisanHammerRelic : RelicEffect { public override RelicCategory Category => RelicCategory.NonCombat; }

    /// <summary>217: 카드를 10장 사용할 때마다 카드 +1 드로우.</summary>
    public class AnyCardCountDrawRelic : CardCountRelic
    {
        protected override CardType? FilterType => null; // 모든 카드
        protected override int       Threshold  => 10;
        protected override string    LogTag     => "[AnyCardCountDrawRelic]";
        protected override void      OnThresholdReached(RelicCombatContext ctx) => ctx.Board.DrawExtraCards(1);
    }

    // ── Rare (3xx) ─────────────────────────────────────────────────────────────

    /// <summary>301: 지도 자유 이동 (MapController에서 HasRelic 체크).</summary>
    public class FreeMapMoveRelic : RelicEffect { public override RelicCategory Category => RelicCategory.NonCombat; }

    /// <summary>302: 전투 시작 시 시작 위치 임의 (CombatController.StartNewCombat에서 HasRelic 체크).</summary>
    public class RandomSpawnRelic : RelicEffect { }


    /// <summary>304: 전투 시작 시 적 전체에게 공포를 부여합니다.</summary>
    public class CombatStartFearRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx.Board.ApplyStatusToAllEnemies(StatusEffectType.Fear, 1);
            Debug.Log("[CombatStartFearRelic] 전투 시작: 적 전체 공포");
        }
    }

    // ── Boss (9xx) ─────────────────────────────────────────────────────────────

    /// <summary>판도라의 상자 (900): 전투가 끝나고 유물 보상을 추가로 하나 더 얻습니다 (InGameUIController에서 HasRelic 체크).</summary>
    public class PandoraBoxRelic : RelicEffect { public override RelicCategory Category => RelicCategory.NonCombat; }

    /// <summary>901 위기의 직감: 위험/보스 전투에서 매 턴 카드 +1.</summary>
    public class EliteOrBossDrawRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            var gm = GameManager.Instance;
            if (gm != null && (gm.IsBossEncounter || gm.IsDangerCombatEncounter))
            {
                ctx.Board.AddBonusHandSize(1);
                Debug.Log("[EliteOrBossDrawRelic] 위험/보스 전투 — 매 턴 드로우 +1");
            }
        }
    }

    /// <summary>902 어둠의 안개: 매 턴 +1, 적 정보 숨김 (CombatController에서 HasRelic 체크).</summary>
    public class HiddenInfoDrawRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx.Board.AddBonusHandSize(1);
        }
    }

    /// <summary>903 영원의 손: 턴이 끝나도 카드를 버리지 않음 (BoardController.DiscardHandCards에서 HasRelic(903) 체크).</summary>
    public class KeepHandPlusDrawRelic : RelicEffect { }


    /// <summary>906 대전환의 의식: 카드 제거→강화→변화→보상 순차 진행.</summary>
    public class GreatTransitionRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.Temporary;
        public override void OnRelicObtained(GameManager gm)
        {
            Step1Remove();
        }

        private void Step1Remove()
        {
            var dm = DeckManager.Instance;
            if (dm == null) { Step2Upgrade(); return; }

            var cards = dm.MasterDeck.Where(c => !c.NonRemovable).ToList();
            if (cards.Count == 0) { Step2Upgrade(); return; }

            InGameUIController.Instance?.OpenCardPicker(cards, picked =>
            {
                if (picked != null)
                {
                    dm.RemoveCardFromDeck(picked);
                    Debug.Log($"[GreatTransitionRelic] 제거: {picked.cardName}");
                }
                Step2Upgrade();
            });
        }

        private void Step2Upgrade()
        {
            var dm = DeckManager.Instance;
            if (dm == null) { Step3Transform(); return; }

            var cards = dm.MasterDeck.Where(c => CardRegistry.GetCard(c.cardCode + 1) != null).ToList();
            if (cards.Count == 0) { Step3Transform(); return; }

            InGameUIController.Instance?.OpenCardPicker(cards, picked =>
            {
                if (picked != null)
                {
                    dm.UpgradeCard(picked);
                    Debug.Log($"[GreatTransitionRelic] 강화: {picked.cardName}");
                }
                Step3Transform();
            });
        }

        private void Step3Transform()
        {
            var dm = DeckManager.Instance;
            if (dm == null) { Step4Reward(); return; }

            var cards = new List<CardData>(dm.MasterDeck);
            if (cards.Count == 0) { Step4Reward(); return; }

            InGameUIController.Instance?.OpenCardPicker(cards, picked =>
            {
                if (picked != null) RarityUpgradeRelic.TransformCard(picked);
                Step4Reward();
            });
        }

        private void Step4Reward()
        {
            InGameUIController.Instance?.OpenStandaloneCardReward(picked =>
            {
                if (picked != null) DeckManager.Instance?.AddCardToDeck(picked);
                Debug.Log("[GreatTransitionRelic] 대전환의 의식 완료");
            });
        }
    }

    /// <summary>907 연금술사의 손길: 포션 효과 2배 (GameManager.UseItem에서 HasRelic 체크).</summary>
    public class PotionDoubleRelic : RelicEffect { }

    /// <summary>908 금욕의 서약: 매 턴 +1, 포션 사용 차단 (GameManager.UseItem에서 HasRelic 체크).</summary>
    public class NoPotionDrawRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx.Board.AddBonusHandSize(1);
        }
    }

    /// <summary>909 등급의 비약: 카드 3장을 더 높은 등급의 무작위 카드로 변화시킵니다.</summary>
    public class RarityUpgradeRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.Temporary;
        public override void OnRelicObtained(GameManager gm)
        {
            PickAndTransform(3);
        }

        private void PickAndTransform(int remaining)
        {
            if (remaining <= 0) return;
            var dm = DeckManager.Instance;
            if (dm == null) return;

            var cards = new List<CardData>(dm.MasterDeck);
            if (cards.Count == 0) return;

            InGameUIController.Instance?.OpenCardPicker(cards, picked =>
            {
                if (picked != null)
                {
                    TransformCard(picked);
                    PickAndTransform(remaining - 1);
                }
            });
        }

        /// <summary>대상 카드를 더 높은 등급의 무작위 카드로 교체합니다 (Rare는 그대로).</summary>
        public static void TransformCard(CardData target)
        {
            if (target == null) return;
            var dm = DeckManager.Instance;
            if (dm == null) return;

            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            var pool = CardRegistry.GetRewardPool(character);

            // 대상 등급 → 한 단계 위 (Common→Uncommon, Uncommon→Rare, Rare→Rare 유지)
            CardRarity targetRarity = target.Rarity switch
            {
                CardRarity.Common   => CardRarity.Uncommon,
                CardRarity.Uncommon => CardRarity.Rare,
                _                   => CardRarity.Rare,
            };

            var candidates = pool
                .Where(c => c.Rarity == targetRarity && c.cardCode != target.cardCode)
                .ToList();

            if (candidates.Count == 0)
            {
                Debug.LogWarning($"[RarityUpgradeRelic] '{target.cardName}' 대체 가능한 {targetRarity} 카드 없음");
                return;
            }

            var replacement = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            dm.RemoveCardFromDeck(target);
            dm.AddCardToDeck(replacement);
            Debug.Log($"[RarityUpgradeRelic] '{target.cardName}' → '{replacement.cardName}' ({targetRarity})");
        }
    }

    /// <summary>910 저주받은 힘: 매 턴 +1, 저주 카드 1장 획득 (제거 불가).</summary>
    public class CursedPowerRelic : RelicEffect
    {
        public override void OnRelicObtained(GameManager gm)
        {
            var curse = CreateCurseCard();
            DeckManager.Instance?.AddCardToDeck(curse);
            Debug.Log("[CursedPowerRelic] 저주 카드 1장 추가 (제거 불가)");
        }

        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx.Board.AddBonusHandSize(1);
        }

        /// <summary>저주 카드 인스턴스를 런타임에 생성합니다 (NonRemovable + Unplayable).
        /// 501 유물 보유 시: Unplayable 제거 + Exhausts 부여 (사용해 소멸 가능).</summary>
        public static CardData CreateCurseCard()
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.cardCode    = 99000; // CSV의 80001(저주)와 별개로, 동적 생성 저주는 99000으로 분리
            c.cardName    = "저주";
            c.effects     = new List<CardEffect>();
            c.name        = "Curse";

            bool consumable = GameManager.Instance != null && GameManager.Instance.HasRelic(501);
            if (consumable)
            {
                c.description = "사용하면 소멸합니다. 덱에서 제거할 수 없습니다.";
                c.keywords    = CardKeyword.Exhausts | CardKeyword.NonRemovable;
            }
            else
            {
                c.description = "사용할 수 없습니다. 덱에서 제거할 수 없습니다.";
                c.keywords    = CardKeyword.Unplayable | CardKeyword.NonRemovable;
            }
            return c;
        }
    }

    /// <summary>911 망각의 인장: 매 턴 +1, 휴식 강화 차단 (CombatController.SpawnRestButtons에서 HasRelic 체크).</summary>
    public class NoUpgradeDrawRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx.Board.AddBonusHandSize(1);
        }
    }

    /// <summary>912 청빈의 맹세: 매 턴 +1, 골드 획득 차단 (GameManager.ModifyGold에서 HasRelic 체크).</summary>
    public class NoGoldDrawRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx.Board.AddBonusHandSize(1);
        }
    }

    /// <summary>913 분노한 적: 매 턴 +1, 적 공격력 +5 (CombatController.SpawnFromEncounter에서 HasRelic 체크).</summary>
    public class EnemyAttackBuffDrawRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx.Board.AddBonusHandSize(1);
        }
    }

    /// <summary>914 수면의 단절: 매 턴 +1, 휴식 회복 차단 (CombatController.SpawnRestButtons에서 HasRelic 체크).</summary>
    public class NoRestHealDrawRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx.Board.AddBonusHandSize(1);
        }
    }

    /// <summary>970 피의 깨우침: HP를 잃을 때마다 카드 1장 즉시 드로우.</summary>
    public class HpLossDrawRelic : RelicEffect
    {
        public override void OnPlayerLostHp(RelicCombatContext ctx, int amount)
        {
            if (ctx?.Board == null || amount <= 0) return;
            ctx.Board.DrawExtraCards(1);
            Debug.Log("[HpLossDrawRelic] HP 손실 → 카드 +1");
        }
    }

    /// <summary>971 소멸의 공명: 카드가 소멸될 때마다 카드 1장 즉시 드로우.</summary>
    public class ExhaustDrawRelic : RelicEffect
    {
        public override void OnCardExhausted(RelicCombatContext ctx, CardData card)
        {
            if (ctx?.Board == null) return;
            ctx.Board.DrawExtraCards(1);
            Debug.Log("[ExhaustDrawRelic] 카드 소멸 → 카드 +1");
        }
    }

    /// <summary>207 예지의 책: 드로우 더미를 누르면 순서대로 표시 (CardListController에서 HasRelic 체크).</summary>
    public class DrawOrderRelic : RelicEffect { public override RelicCategory Category => RelicCategory.NonCombat; }

    // ── 직업 시작 유물 (070/080/090) ───────────────────────────────────────────

    /// <summary>70 피의 깨우침: 매 턴 시작 시 이동 카드(21000)를 한 장 생성하고
    /// 휘발성(Ethereal)과 소멸(Exhausts)을 부여합니다 (유물 설명과 일치).
    /// extraKeywords는 GrantClassRelicCard가 복제본에만 적용하므로 덱의 기본 21000은 영향받지 않습니다.</summary>
    public class WarriorStartHealRelic : RelicEffect
    {
        public override void OnPlayerTurnStart(RelicCombatContext ctx)
        {
            ctx?.Board?.GrantClassRelicCard(WarriorTurnMoveCardCode,
                CardKeyword.Ethereal | CardKeyword.Exhausts);
        }

        private const int WarriorTurnMoveCardCode = 21000;
    }

    /// <summary>80 거너 시작 — 피의 깨우침: 발사 카드를 두 번 사용할 때마다 이동 카드(31000)를 한 장 생성합니다.
    /// 발사 카드는 키워드가 아니라 customEffectId(BoardController.IsShootAttackEffect)로 식별합니다(메모리 참조).
    /// 생성된 카드는 휘발성(Ethereal)·소멸(Exhausts)이 부여되어 그 턴에 쓰지 않으면 사라집니다.
    /// 손패에 직접 추가되므로 startingHandSize 드로우 계산에 포함되지 않습니다.</summary>
    public class ShootMoveCardRelic : RelicEffect
    {
        private const int GunnerMoveCardCode = 31000; // 거너 이동 카드 (CSV: Exhausts|Ethereal)
        private const int Threshold = 2;
        private int _counter;

        public override string CounterText => $"{_counter % Threshold}/{Threshold}";

        public override void OnCombatStart(RelicCombatContext ctx)
        {
            _counter = 0;
            NotifyCounterChanged();
        }

        public override void OnCardPlayed(RelicCombatContext ctx, CardData card)
        {
            if (card?.effects == null || ctx?.Board == null) return;
            if (!card.effects.Any(e => BoardController.IsShootAttackEffect(e))) return;

            _counter++;
            NotifyCounterChanged();

            if (_counter % Threshold == 0)
            {
                ctx.Board.GrantClassRelicCard(GunnerMoveCardCode,
                    CardKeyword.Ethereal | CardKeyword.Exhausts);
                Debug.Log("[ShootMoveCardRelic] 발사 2회 — 이동 카드 생성");
            }
        }
    }

    /// <summary>320 거너 시작: 적 처치 시 카드 1장 드로우.</summary>
    public class GunnerStartKillDrawRelic : RelicEffect
    {
        public override void OnEnemyKilled(RelicCombatContext ctx, EnemyInstance enemy)
        {
            ctx?.Board?.DrawExtraCards(1);
        }
    }

    /// <summary>90 메이지 시작: 전투 시작 시 순간이동 카드(41000) 한 장을 손패에 추가합니다.
    /// Ethereal로 부여되어 사용/턴종료 시 소멸됩니다.</summary>
    public class MageStartTeleportRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx?.Board?.GrantClassRelicCard(MageTeleportCardCode, CardKeyword.Ethereal);
        }

        private const int MageTeleportCardCode = 41000; // 순간이동 (CSV)
    }

    // ── Common 신규 (1xx) ──────────────────────────────────────────────────────

    /// <summary>101: 상점을 거친 다음 전투에서 힘 +4 (GameManager.LastNodeWasShop 체크).</summary>
    public class PostShopStrengthRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            if (GameManager.Instance != null && GameManager.Instance.LastNodeWasShop)
            {
                ctx.Board.AddStrength(4);
                Debug.Log("[PostShopStrengthRelic] 상점 직후 전투 — 힘 +4");
            }
        }
    }

    /// <summary>100: 휴식을 거친 다음 전투에서 힘 +2 (GameManager.LastNodeWasRest 체크).</summary>
    public class PostRestStrengthRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            if (GameManager.Instance != null && GameManager.Instance.LastNodeWasRest)
            {
                ctx.Board.AddStrength(4);
                Debug.Log("[PostRestStrengthRelic] 휴식 직후 전투 — 힘 +4");
            }
        }
    }

    /// <summary>114: 맵에서 이동할 때마다 +25G. 단, 상점을 한 번이라도 방문한 뒤로는 비활성화됩니다.
    /// 상점 방문 여부는 저장/로드에 영속되는 ShopVisitCount로 판정합니다.</summary>
    public class MapMoveGoldRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.NonCombat;
        public override void OnMapMove(GameManager gm)
        {
            if (gm == null) return;
            if (gm.ShopVisitCount > 0) return; // 상점 방문 후 비활성화
            gm.ModifyGold(25);
            Debug.Log("[MapMoveGoldRelic] 맵 이동 — +25G");
        }
    }

    /// <summary>115: 덱에 카드를 추가할 때마다 +15G.</summary>
    public class CardObtainGoldRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.NonCombat;
        public override void OnCardObtained(CardData card, ref CardData replacement)
        {
            if (card == null) return;
            GameManager.Instance?.ModifyGold(20);
            Debug.Log("[CardObtainGoldRelic] 카드 획득 — +20G");
        }
    }

    /// <summary>116: 휴식할 때 채력을 전부 회복합니다.
    /// 회복 비율을 100%로 설정 — MaxHP × 1.0 회복량은 ModifyHP의 상한 클램프로 최대 HP까지 채워집니다.</summary>
    public class RestHealBonusRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.NonCombat;
        public override void OnBeforeRestHeal(ref float healPercent)
        {
            healPercent = 1f; // 채력 전부 회복
            Debug.Log("[RestHealBonusRelic] 휴식 회복 → 채력 전부 회복(100%)");
        }
    }

    /// <summary>117: 액션 카드 미사용 시 다음 턴 카드 +1.</summary>
    public class NoActionNextDrawRelic : NoTypeUsedNextDrawRelic
    {
        protected override CardType FilterType => CardType.Action;
        protected override string Tag => "[NoActionNextDrawRelic]";
    }

    /// <summary>118: 획득 시 카드 2장 무작위 강화. UI/선택 미구현으로 스텁.</summary>
    public class RandomUpgradeOnObtainRelic : RelicEffect { public override RelicCategory Category => RelicCategory.Temporary; }

    // ── 상점 전용 (4xx) ────────────────────────────────────────────────────────

    /// <summary>400: 상점 모든 상품 50% 할인 (ShopController에서 HasRelic(400) 체크).</summary>
    public class ShopDiscountRelic : RelicEffect { public override RelicCategory Category => RelicCategory.NonCombat; }

    /// <summary>503: 획득 시 최대 HP +bonus, 그 후 채력 전부 회복.</summary>
    public class FullHealRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.Temporary;
        private readonly int _maxHpBonus;
        public FullHealRelic(int maxHpBonus = 0) { _maxHpBonus = maxHpBonus; }
        public override void OnRelicObtained(GameManager gm)
        {
            if (gm == null) return;
            if (_maxHpBonus > 0) gm.ModifyMaxHP(_maxHpBonus);
            gm.FullHeal();
            Debug.Log($"[FullHealRelic] 최대 HP +{_maxHpBonus}, 채력 전부 회복");
        }
    }

    /// <summary>402: 카드 제거 비용 50G 고정 (GameManager.CardRemovalPrice에서 HasRelic(402) 체크).</summary>
    public class CardRemovalDiscountRelic : RelicEffect { public override RelicCategory Category => RelicCategory.NonCombat; }

    /// <summary>403/503/603: 획득 시 지정 등급의 카드 보상 패널을 엽니다 (전투 종료 보상과 동일).</summary>
    public class CardRewardOnObtainRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.NonCombat;
        private readonly CardRarity _rarity;
        private const int Choices = 3;

        public CardRewardOnObtainRelic(CardRarity rarity) { _rarity = rarity; }

        public override void OnRelicObtained(GameManager gm)
        {
            var pool = CardRegistry.GetRewardPool(gm.SelectedCharacter)
                .Where(c => c.Rarity == _rarity)
                .ToList();
            if (pool.Count == 0)
            {
                Debug.LogWarning($"[CardRewardOnObtainRelic] {_rarity} 등급 카드 풀이 비어있습니다.");
                return;
            }

            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            var picks = pool.Take(Mathf.Min(Choices, pool.Count)).ToList();

            InGameUIController.Instance?.OpenItemCardSelection(picks, picked =>
            {
                if (picked != null) DeckManager.Instance?.AddCardToDeck(picked);
            });
        }
    }

    /// <summary>덱이 셔플될 때마다 힘 +1. DeckManager.OnDeckShuffled 구독.</summary>
    public class ShuffleStrengthRelic : RelicEffect
    {
        private DeckRoguelike.Combat.DeckManager _subscribed;
        private RelicCombatContext _ctx;

        public override void OnCombatStart(RelicCombatContext ctx)
        {
            _ctx = ctx;
            var dm = DeckRoguelike.Combat.DeckManager.Instance;
            if (dm == null) return;
            if (_subscribed != null) _subscribed.OnDeckShuffled -= OnShuffle;
            dm.OnDeckShuffled += OnShuffle;
            _subscribed = dm;
        }

        private void OnShuffle()
        {
            if (_ctx?.Board == null) return;
            _ctx.Board.AddStrength(1);
            Debug.Log("[ShuffleStrengthRelic] 덱 셔플 — 힘 +1");
        }
    }

    /// <summary>405: 모든 직업/공통 카드 보상 (CardRewardPanel에서 HasRelic(405) 체크).</summary>
    public class AllClassRewardRelic : RelicEffect { public override RelicCategory Category => RelicCategory.NonCombat; }

    // ── Boss 신규 (9xx) ────────────────────────────────────────────────────────


    // ── 직업 보스 유물 ────────────────────────────────────────────────────────

    /// <summary>970 피의 깨우침(보스): 매 턴 시작 시 이동+ 카드(21001)를 한 장 생성하고
    /// 휘발성(Ethereal)과 소멸(Exhausts)을 부여합니다 (유물 설명과 일치).
    /// extraKeywords는 GrantClassRelicCard가 복제본에만 적용하므로 덱의 기본 21001은 영향받지 않습니다.</summary>
    public class WarriorBossHealRelic : RelicEffect
    {
        public override void OnPlayerTurnStart(RelicCombatContext ctx)
        {
            ctx?.Board?.GrantClassRelicCard(WarriorBossTurnMoveCardCode,
                CardKeyword.Ethereal | CardKeyword.Exhausts);
        }

        private const int WarriorBossTurnMoveCardCode = 21001;
    }

    /// <summary>972 전사 보스 — 더이상 카드 소멸 안 됨. 카드 효과 단계 개입 필요로 스텁.</summary>
    public class NoExhaustRelic : RelicEffect { }

    /// <summary>980 거너 보스: 적 처치 시 카드 +2.</summary>
    public class GunnerBossDrawRelic : RelicEffect
    {
        public override void OnEnemyKilled(RelicCombatContext ctx, EnemyInstance enemy)
        {
            ctx?.Board?.DrawExtraCards(2);
        }
    }

    /// <summary>981 거너 보스 — 범위 피해 2배. 데미지 처리 분기 필요로 스텁.</summary>
    public class AreaDamageDoubleRelic : RelicEffect { }

    /// <summary>982 거너 보스: 이동 시 카드 +1.</summary>
    public class MoveDrawRelic : RelicEffect
    {
        public override void OnPlayerMoved(RelicCombatContext ctx, int totalMoveCount)
        {
            ctx?.Board?.DrawExtraCards(1);
        }
    }

    /// <summary>990 메이지 보스(90 업그레이드): 전투 시작 시 순간이동(41000) 카드 두 장을 손패에 추가합니다.
    /// 강화 카드(41001)가 없으므로 일반 순간이동을 두 장 지급해 효과를 유지합니다.</summary>
    public class MageBossTeleportRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            if (ctx?.Board == null) return;
            ctx.Board.GrantClassRelicCard(MageTeleportCardCode, CardKeyword.Ethereal);
            ctx.Board.GrantClassRelicCard(MageTeleportCardCode, CardKeyword.Ethereal);
        }

        private const int MageTeleportCardCode = 41000; // 순간이동 (CSV)
    }

    /// <summary>991 메이지 보스 — 적 처치 시 해골병사 생성. 소환 시스템 연계 필요로 스텁.</summary>
    public class SkeletonOnKillRelic : RelicEffect { }

    /// <summary>992 메이지 보스 — 적 처치 시 화상 전이. 상태 전이 로직 필요로 스텁.</summary>
    public class BurnTransferOnKillRelic : RelicEffect { }

    // ── CSV 미등록 유물 추가 구현 ─────────────────────────────────────────────

    /// <summary>403: 덱의 저주 카드 한 장당 전투 시작 시 힘 +2.</summary>
    public class CurseStrengthRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            var dm = DeckManager.Instance;
            if (dm == null) return;
            int curseCount = dm.MasterDeck.Count(c => c != null
                && c.keywords.HasFlag(CardKeyword.NonRemovable)
                && (c.keywords.HasFlag(CardKeyword.Unplayable) || c.cardCode == 99000));
            if (curseCount > 0)
            {
                int bonus = curseCount * 2;
                ctx.Board.AddStrength(bonus);
                Debug.Log($"[CurseStrengthRelic] 저주 {curseCount}장 — 힘 +{bonus}");
            }
        }
    }

    /// <summary>109: 보스 전투 시작 시 힘 +3.</summary>
    public class BossStrengthRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsBossEncounter)
            {
                ctx.Board.AddStrength(3);
                Debug.Log("[BossStrengthRelic] 보스전 — 힘 +3");
            }
        }
    }

    /// <summary>270: 채력을 잃을 때마다 1 덜 잃습니다.</summary>
    public class DamageReductionRelic : RelicEffect
    {
        public override void OnPlayerDamaged(RelicCombatContext ctx, ref int damage)
        {
            if (damage > 0)
            {
                damage = Mathf.Max(0, damage - 1);
                Debug.Log("[DamageReductionRelic] 피해 -1");
            }
        }
    }

    /// <summary>200: 전투 종료 시 HP가 50% 이하라면 HP +12 회복.</summary>
    public class HalfHpVictoryHealRelic : RelicEffect
    {
        public override void OnCombatVictory(RelicCombatContext ctx)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.MaxHP <= 0) return;
            if (gm.CurrentHP * 2 <= gm.MaxHP)
            {
                gm.Heal(12);
                Debug.Log("[HalfHpVictoryHealRelic] HP 50% 이하 — HP +12");
            }
        }
    }

    /// <summary>220/320: 하트 포션 — 획득 시 최대 HP +amount.</summary>
    public class MaxHPPotionRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.Temporary;
        private readonly int _amount;
        public MaxHPPotionRelic(int amount) { _amount = amount; }
        public override void OnRelicObtained(GameManager gm)
        {
            gm?.ModifyMaxHP(_amount);
            Debug.Log($"[MaxHPPotionRelic] 최대 HP +{_amount}");
        }
    }

    /// <summary>305 마지막 한발: 매 턴 카드를 전부 사용하면(손패가 비면) 카드 1장 드로우 (턴당 1회).</summary>
    public class EmptyHandDrawRelic : RelicEffect
    {
        private bool _triggeredThisTurn;
        public override void OnCombatStart(RelicCombatContext ctx)    { _triggeredThisTurn = false; }
        public override void OnPlayerTurnStart(RelicCombatContext ctx) { _triggeredThisTurn = false; }
        public override void OnCardPlayed(RelicCombatContext ctx, CardData card)
        {
            if (_triggeredThisTurn || ctx?.Board == null) return;
            if (ctx.Board.HandCount == 0)
            {
                _triggeredThisTurn = true;
                ctx.Board.DrawExtraCards(1);
                Debug.Log("[EmptyHandDrawRelic] 손패 비움 — 카드 +1");
            }
        }
    }

    /// <summary>311: 획득 즉시 amount 골드를 얻습니다.</summary>
    public class GoldGainRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.Temporary;
        private readonly int _amount;
        public GoldGainRelic(int amount) { _amount = amount; }
        public override void OnRelicObtained(GameManager gm)
        {
            gm?.ModifyGold(_amount);
            Debug.Log($"[GoldGainRelic] +{_amount}G");
        }
    }

    /// <summary>601: 이동/액션/파워 카드 사용 시 플레이어의 해로운 효과를 모두 제거합니다.</summary>
    public class RemoveDebuffOnPlayRelic : RelicEffect
    {
        public override void OnCardPlayed(RelicCombatContext ctx, CardData card)
        {
            if (card == null || ctx?.Board == null) return;
            var t = card.CardTypeFromCode;
            if (t != CardType.Action && t != CardType.Move && t != CardType.Power) return;
            ctx.Board.RemovePlayerDebuffs();
            Debug.Log($"[RemoveDebuffOnPlayRelic] {t} 카드 사용 — 해로운 효과 제거");
        }
    }

    // ── 외부 시스템에서 HasRelic 체크용 마커 (NonCombat 스텁) ─────────────────

    /// <summary>108: 금속 탐지기 — 보물방 위치 표시 (MapController에서 HasRelic 체크).</summary>
    public class TreasureMapRelic : RelicEffect { public override RelicCategory Category => RelicCategory.NonCombat; }

    /// <summary>113 포션 상자 — 포션 슬롯 1열(2개) 추가. 획득 즉시 슬롯 그리드를 재구성합니다.</summary>
    public class PotionSlotRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.NonCombat;
        public override void OnRelicObtained(GameManager gm)
        {
            InGameUIController.Instance?.RebuildItemSlots();
            Debug.Log("[PotionSlotRelic] 포션 슬롯 +1열 재구성");
        }
    }

    /// <summary>102: 미지(Event)를 거친 다음 전투에서 힘 +4.</summary>
    public class PostMysteryStrengthRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            if (GameManager.Instance != null && GameManager.Instance.LastNodeWasMystery)
            {
                ctx.Board.AddStrength(2);
                Debug.Log("[PostMysteryStrengthRelic] 미지 직후 전투 — 힘 +2");
            }
        }
    }

    /// <summary>213: 상점 품절 없음 + 30% 할인 (ShopController에서 HasRelic 체크).</summary>
    public class ShopBetterDiscountRelic : RelicEffect { public override RelicCategory Category => RelicCategory.NonCombat; }

    /// <summary>214: 위험 전투 적 HP 25% 감소 (CombatController.SpawnFromEncounter에서 HasRelic 체크).</summary>
    public class DangerCombatEnemyHPRelic : RelicEffect { public override RelicCategory Category => RelicCategory.NonCombat; }

    /// <summary>215: 휴식 장소에서 휴식+강화 둘 다 사용 (CombatController.SpawnRestButtons에서 HasRelic 체크).</summary>
    public class RestFullAccessRelic : RelicEffect { public override RelicCategory Category => RelicCategory.NonCombat; }

    /// <summary>309: 전투 시작 시 강화된 무작위 파워카드 1장을 손패에 생성합니다.</summary>
    public class PowerCardOnCombatStartRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            if (ctx?.Board == null) return;
            var gm = GameManager.Instance;
            if (gm == null) return;

            var pool = CardRegistry.GetRewardPool(gm.SelectedCharacter)
                .Where(c => c != null && c.CardTypeFromCode == CardType.Power)
                .ToList();
            if (pool.Count == 0)
            {
                Debug.LogWarning("[PowerCardOnCombatStartRelic] 파워 카드 풀이 비어있음");
                return;
            }

            var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            var upgraded = CardRegistry.GetCard(pick.UpgradedCode) ?? pick;
            ctx.Board.AddStartingDrawCard(upgraded.Clone());
            Debug.Log($"[PowerCardOnCombatStartRelic] 강화된 파워카드 '{upgraded.cardName}' 생성");
        }
    }

    /// <summary>310: 매 턴마다 한 번, 공격받았을 때 공격한 적을 기절시킵니다.</summary>
    public class StunOnAttackedRelic : RelicEffect
    {
        private bool _triggeredThisTurn;
        public override void OnCombatStart(RelicCombatContext ctx)    { _triggeredThisTurn = false; }
        public override void OnPlayerTurnStart(RelicCombatContext ctx) { _triggeredThisTurn = false; }
        public override void OnPlayerDamagedBy(RelicCombatContext ctx, int damage, EnemyInstance attacker)
        {
            if (_triggeredThisTurn) return;
            if (attacker == null || attacker.CurrentHP <= 0 || ctx?.Board == null) return;
            _triggeredThisTurn = true;
            ctx.Board.ApplyStatus(attacker, StatusEffectType.Stun, 1);
            Debug.Log($"[StunOnAttackedRelic] {attacker.Name} 기절 (턴당 1회)");
        }
    }

    /// <summary>311/312/314: 특정 방향(위/아래/뒤)에서 공격 시 2배 피해 (피해 계산기에서 HasRelic 체크).</summary>
    public class DirectionalDamageRelic : RelicEffect
    {
        public string Direction { get; }
        public DirectionalDamageRelic(string direction) { Direction = direction; }
    }

    /// <summary>404/504/604: 획득 즉시 같은 등급의 무작위 유물을 하나 더 얻습니다.</summary>
    public abstract class RandomRelicGrantRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.Temporary;
        protected abstract int TargetRarityIndex { get; }  // 0=Common, 1=Uncommon, 2=Rare
        public override void OnRelicObtained(GameManager gm)
        {
            if (gm == null) return;
            var ownedCodes = new HashSet<int>();
            foreach (var r in gm.Relics)
                if (r?.Data != null) ownedCodes.Add(r.Data.relicCode);

            var pool = RelicRegistry.GetByType(bossRelic: false)
                .Where(d => d != null
                            && !ownedCodes.Contains(d.relicCode)
                            && d.RarityIndex == TargetRarityIndex
                            && !d.IsShopOnlyRelic
                            && !d.IsStartingRelic
                            && d.IsForCharacter(gm.SelectedCharacter))
                .ToList();
            if (pool.Count == 0)
            {
                Debug.LogWarning($"[RandomRelicGrantRelic] 풀 비어있음 (rarity {TargetRarityIndex})");
                return;
            }
            var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            gm.AddRelic(pick.relicCode);
            Debug.Log($"[RandomRelicGrantRelic] 무작위 유물 '{pick.relicName}' 획득");
        }
    }
    public class RandomCommonRelicRelic   : RandomRelicGrantRelic { protected override int TargetRarityIndex => 0; }
    public class RandomUncommonRelicRelic : RandomRelicGrantRelic { protected override int TargetRarityIndex => 1; }
    public class RandomRareRelicRelic     : RandomRelicGrantRelic { protected override int TargetRarityIndex => 2; }

    /// <summary>501: 저주 카드를 사용하여 소멸시킬 수 있습니다.
    /// 획득 즉시 덱/드로우/버린 더미의 모든 저주 카드 Unplayable→Exhausts 로 변환.
    /// 이후 생성되는 저주 카드는 CursedPowerRelic.CreateCurseCard()에서 직접 처리됩니다.</summary>
    public class CurseConsumableRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.NonCombat;
        public override void OnRelicObtained(GameManager gm)
        {
            var dm = DeckManager.Instance;
            if (dm == null) return;
            int converted = 0;
            foreach (var card in dm.MasterDeck)
            {
                if (card == null) continue;
                if (!card.keywords.HasFlag(CardKeyword.Unplayable)) continue;
                if (!card.keywords.HasFlag(CardKeyword.NonRemovable)) continue; // 저주만
                card.keywords &= ~CardKeyword.Unplayable;
                card.keywords |=  CardKeyword.Exhausts;
                card.description = "사용하면 소멸합니다. 덱에서 제거할 수 없습니다.";
                converted++;
            }
            Debug.Log($"[CurseConsumableRelic] 저주 {converted}장 사용 가능(소멸)으로 변환");
        }
    }

    /// <summary>502: 전투 시작 시 무작위 공통(ClassDigit==1) 카드 셋 중 하나를 골라 손으로 가져옵니다.</summary>
    public class CommonCardOnCombatStartRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            if (ctx?.Board == null) return;
            var gm = GameManager.Instance;
            if (gm == null) return;

            var pool = CardRegistry.GetRewardPool(gm.SelectedCharacter)
                .Where(c => c != null && !c.IsUpgraded && c.ClassDigit == 1)
                .ToList();
            if (pool.Count == 0)
            {
                Debug.LogWarning("[CommonCardOnCombatStartRelic] 공통 카드 풀이 비어있음");
                return;
            }

            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            var picks = pool.Take(Mathf.Min(3, pool.Count)).ToList();

            var board = ctx.Board;
            InGameUIController.Instance?.OpenCardPicker(picks, picked =>
            {
                if (picked == null) return;
                board.AddCardToHandFree(picked.Clone());
                Debug.Log($"[CommonCardOnCombatStartRelic] 공통 카드 '{picked.cardName}' 손에 추가");
            });
        }
    }

    /// <summary>600: 획득 시 덱의 카드 하나를 선택해 복사하여 추가합니다.</summary>
    public class CardCopyRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.Temporary;
        public override void OnRelicObtained(GameManager gm)
        {
            var dm = DeckManager.Instance;
            if (dm == null) return;
            var cards = new List<CardData>(dm.MasterDeck);
            if (cards.Count == 0) return;

            InGameUIController.Instance?.OpenCardPicker(cards, picked =>
            {
                if (picked == null) return;
                var copy = picked.Clone();
                dm.AddCardToDeck(copy);
                Debug.Log($"[CardCopyRelic] '{picked.cardName}' 복사하여 덱에 추가");
            });
        }
    }

    // ── 공통 유틸 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// "N장 사용마다 효과 발동" 패턴 공통 베이스.
    /// 111/112/209/210/211/217 에서 상속.
    /// FilterType == null이면 모든 카드, 그렇지 않으면 해당 타입만 카운트.
    /// </summary>
    public abstract class CardCountRelic : RelicEffect
    {
        private int _counter = 0;

        protected abstract CardType? FilterType  { get; }
        protected abstract int       Threshold   { get; }
        protected virtual  bool      ShowCounter => true;
        protected abstract string    LogTag      { get; }
        protected abstract void      OnThresholdReached(RelicCombatContext ctx);

        public override string CounterText =>
            (ShowCounter && Threshold > 1) ? $"{_counter % Threshold}/{Threshold}" : null;

        public override void OnCombatStart(RelicCombatContext ctx)
        {
            _counter = 0;
            NotifyCounterChanged();
        }

        public override void OnCardPlayed(RelicCombatContext ctx, CardData card)
        {
            if (card == null) return;
            if (FilterType.HasValue && card.CardTypeFromCode != FilterType.Value) return;

            _counter++;
            NotifyCounterChanged();

            if (_counter % Threshold == 0)
            {
                OnThresholdReached(ctx);
                Debug.Log($"{LogTag} 카운트 {_counter} — 효과 발동");
            }
        }
    }

    // ── CSV 추가 유물 (확장) ─────────────────────────────────────────────────────

    /// <summary>105 미지의 부적: 미지 노드 진입 시 HP +7. GameManager 미지 노드 진입 시 호출 필요.</summary>
    public class MysteryBonusRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.NonCombat;
        /// <summary>미지 진입 시 외부에서 호출하는 회복 진입 훅. GameManager.OnMysteryNodeEntered 등에서 사용.</summary>
        public void OnMysteryEntered(GameManager gm)
        {
            gm?.Heal(7);
            Debug.Log("[MysteryBonusRelic] 미지 진입 — HP +7");
        }
    }

    /// <summary>111 가시: 공격받을 때마다 공격한 적에게 3 피해.</summary>
    public class ThornsRelic : RelicEffect
    {
        public override void OnPlayerDamagedBy(RelicCombatContext ctx, int damage, EnemyInstance attacker)
        {
            if (attacker == null || attacker.CurrentHP <= 0 || ctx?.Board == null) return;
            ctx.Board.DamageEnemy(attacker, 3);
            Debug.Log($"[ThornsRelic] {attacker.Name}에게 3 가시 피해");
        }
    }

    /// <summary>118 이동 카드 미사용 시 다음 턴 카드 +1.</summary>
    public class NoMoveNextDrawRelic : NoTypeUsedNextDrawRelic
    {
        protected override CardType FilterType => CardType.Move;
        protected override string Tag => "[NoMoveNextDrawRelic]";
    }

    /// <summary>119 파워 카드 미사용 시 다음 턴 카드 +1.</summary>
    public class NoPowerNextDrawRelic : NoTypeUsedNextDrawRelic
    {
        protected override CardType FilterType => CardType.Power;
        protected override string Tag => "[NoPowerNextDrawRelic]";
    }

    /// <summary>117/118/119 공통 베이스: 지정 타입 카드를 이번 턴에 한 번도 쓰지 않으면 다음 턴 +1 드로우.</summary>
    public abstract class NoTypeUsedNextDrawRelic : RelicEffect
    {
        protected abstract CardType FilterType { get; }
        protected abstract string Tag { get; }

        private bool _usedThisTurn;
        public override void OnCombatStart(RelicCombatContext ctx) { _usedThisTurn = false; }
        public override void OnPlayerTurnStart(RelicCombatContext ctx) { _usedThisTurn = false; }
        public override void OnCardPlayed(RelicCombatContext ctx, CardData card)
        {
            if (card != null && card.CardTypeFromCode == FilterType) _usedThisTurn = true;
        }
        public override void OnPlayerTurnEnd(RelicCombatContext ctx)
        {
            if (!_usedThisTurn && ctx?.Board != null)
            {
                ctx.Board.AddTimedBonusDraw(1, 1);
                Debug.Log($"{Tag} {FilterType} 미사용 — 다음 턴 카드 +1");
            }
        }
    }

    /// <summary>170 채력 50% 이하일 때 전투 시작 시 힘 +3.</summary>
    public class LowHpStrengthRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.MaxHP <= 0) return;
            if (gm.CurrentHP * 2 <= gm.MaxHP)
            {
                ctx.Board.AddStrength(3);
                Debug.Log("[LowHpStrengthRelic] HP 50%↓ — 힘 +3");
            }
        }
    }

    /// <summary>190 화염을 부여할 때마다 화염을 1 추가로 부여합니다. (BoardController.ApplyStatus에서 HasRelic(190) 체크 — 스택 +1)</summary>
    public class ExtraBurnRelic : RelicEffect { }

    /// <summary>204 카드 1장 제거.</summary>
    public class CardRemoveOnObtainRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.Temporary;
        public override void OnRelicObtained(GameManager gm)
        {
            var dm = DeckManager.Instance;
            if (dm == null) return;
            var cards = dm.MasterDeck.Where(c => c != null && !c.NonRemovable).ToList();
            if (cards.Count == 0) return;
            InGameUIController.Instance?.OpenCardPicker(cards, picked =>
            {
                if (picked == null) return;
                dm.RemoveCardFromDeck(picked);
                Debug.Log($"[CardRemoveOnObtainRelic] '{picked.cardName}' 제거");
            });
        }
    }

    /// <summary>206 카드 보상 선택지 +1 (CardRewardPanel에서 HasRelic(206) 체크).</summary>
    public class ExtraCardRewardChoiceRelic : RelicEffect { public override RelicCategory Category => RelicCategory.NonCombat; }

    /// <summary>210 시작 카드들을 전부 강화.</summary>
    public class UpgradeStartingDeckRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.Temporary;
        public override void OnRelicObtained(GameManager gm)
        {
            var dm = DeckManager.Instance;
            if (dm == null) return;
            int upgraded = 0;
            foreach (var card in dm.MasterDeck)
            {
                if (card == null || card.IsUpgraded) continue;
                if (CardRegistry.GetCard(card.UpgradedCode) == null) continue;
                dm.UpgradeCard(card);
                upgraded++;
            }
            Debug.Log($"[UpgradeStartingDeckRelic] 시작 덱 {upgraded}장 강화");
        }
    }

    /// <summary>216 매 전투마다 처음 공격받았을때 카드를 3장 뽑습니다.</summary>
    public class FirstHitDrawRelic : RelicEffect
    {
        private bool _triggered;
        public override void OnCombatStart(RelicCombatContext ctx) { _triggered = false; }
        public override void OnPlayerDamagedBy(RelicCombatContext ctx, int damage, EnemyInstance attacker)
        {
            if (_triggered || damage <= 0 || ctx?.Board == null) return;
            _triggered = true;
            ctx.Board.DrawExtraCards(3);
            Debug.Log("[FirstHitDrawRelic] 첫 피격 — 카드 +3");
        }
    }

    /// <summary>219 적 처치 시 HP +2.</summary>
    public class KillHealRelic : RelicEffect
    {
        public override void OnEnemyKilled(RelicCombatContext ctx, EnemyInstance enemy)
        {
            GameManager.Instance?.Heal(2);
        }
    }

    /// <summary>290 소환수의 체력 +5. (BoardController.SummonAlly에서 HasRelic(290) 체크 — MaxHP/CurrentHP +5)</summary>
    public class AllyHpBoostRelic : RelicEffect { }

    /// <summary>300 카드 보상 카드들이 강화된 상태로 나옵니다. (CardRewardPanel에서 HasRelic(300) 체크)</summary>
    public class UpgradedRewardRelic : RelicEffect { public override RelicCategory Category => RelicCategory.NonCombat; }

    /// <summary>303 매턴 버린 카드 수만큼 힘 증가.</summary>
    public class DiscardStrengthRelic : RelicEffect
    {
        public override void OnHandDiscarded(RelicCombatContext ctx, int count)
        {
            if (count <= 0 || ctx?.Board == null) return;
            ctx.Board.AddStrength(count);
            Debug.Log($"[DiscardStrengthRelic] 버린 카드 {count}장 — 힘 +{count}");
        }
    }

    /// <summary>305 턴 종료 시 손패의 무작위 카드 1장을 그 턴 동안만 강화. (간이 구현: 손패 전체 임시 강화)</summary>
    public class TurnEndRandomUpgradeRelic : RelicEffect
    {
        public override void OnPlayerTurnEnd(RelicCombatContext ctx)
        {
            // 다음 턴 손패 전체를 그 턴만 강화 (의도: 매 턴 무작위 1장 강화. 간이로 핸드 일괄 강화)
            ctx?.Board?.UpgradeAllHandCardsTemporary();
        }
    }

    /// <summary>313 초과된 피해량이 가까운 적에게 전이됩니다. (BoardController.DamageEnemy 분기에서 HasRelic(313) 체크)</summary>
    public class ExcessDamageTransferRelic : RelicEffect { }

    /// <summary>315 전투가 시작될 때 적 전체에게 10 피해를 줍니다.</summary>
    public class CombatStartAoeDamageRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            if (ctx?.Board == null) return;
            ctx.Board.DealDamageToAllEnemies(10);
            Debug.Log("[CombatStartAoeDamageRelic] 전투 시작 — 적 전체 10 피해");
        }
    }

    /// <summary>316 적이 채력을 잃을 때마다 5 피해.</summary>
    public class EnemyDamagedThornsRelic : RelicEffect
    {
        public override void OnEnemyDamaged(RelicCombatContext ctx, EnemyInstance enemy, int amount)
        {
            if (enemy == null || enemy.CurrentHP <= 0 || amount <= 0 || ctx?.Board == null) return;
            ctx.Board.StartFollowUpDamage(enemy, 5, 0.05f);
            Debug.Log($"[EnemyDamagedThornsRelic] {enemy.Name} 피해 → 추가 5 피해");
        }
    }

    /// <summary>319 채력이 0이 됐을때 부활합니다. (GameManager에서 HasRelic(319) 체크하여 SetHP 회복)</summary>
    public class ReviveRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.NonCombat;
    }

    /// <summary>380 군번줄: 전투가 시작될 때 회피 +1.</summary>
    public class DogTagRelic : RelicEffect
    {
        public override void OnCombatStart(RelicCombatContext ctx)
        {
            ctx?.Board?.AddDodge(1);
            Debug.Log("[DogTagRelic] 전투 시작 — 회피 +1");
        }
    }

    /// <summary>390 소환 카드 사용 시 힘 +1.</summary>
    public class SummonCountStrengthRelic : RelicEffect
    {
        public override void OnCardPlayed(RelicCombatContext ctx, CardData card)
        {
            if (card?.effects == null || ctx?.Board == null) return;
            bool isSummon = card.effects.Any(e => e != null
                && e.effectType == EffectType.Custom
                && !string.IsNullOrEmpty(e.customEffectId)
                && e.customEffectId.StartsWith("summon_", System.StringComparison.OrdinalIgnoreCase));
            if (!isSummon) return;
            ctx.Board.AddStrength(1);
            Debug.Log("[SummonCountStrengthRelic] 소환 카드 — 힘 +1");
        }
    }

    /// <summary>401 포션 5개 획득 (현재 직업 풀에서 무작위로).</summary>
    public class PotionFiveRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.Temporary;
        public override void OnRelicObtained(GameManager gm)
        {
            if (gm == null) return;
            var pool = DeckRoguelike.Item.ItemRegistry.GetForCharacter(gm.SelectedCharacter, bossItem: false);
            if (pool == null || pool.Count == 0) return;
            int granted = 0;
            for (int i = 0; i < 5; i++)
            {
                var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                if (pick == null) continue;
                gm.AddItem(pick);
                granted++;
            }
            Debug.Log($"[PotionFiveRelic] 포션 {granted}개 획득");
        }
    }

    /// <summary>502 매 턴 한 번, 다음에 적에게 부여하는 해로운 효과 스택을 3배로 증가시킵니다.
    /// (BoardController.ApplyStatus에서 HasRelic(502) + 턴당 1회 플래그 체크)</summary>
    public class TripleDebuffRelic : RelicEffect
    {
        public bool ConsumedThisTurn { get; private set; }
        public override void OnCombatStart(RelicCombatContext ctx)    { ConsumedThisTurn = false; }
        public override void OnPlayerTurnStart(RelicCombatContext ctx) { ConsumedThisTurn = false; }
        public void MarkConsumed() { ConsumedThisTurn = true; }
    }

    /// <summary>601 전투가 시작하고 10턴 동안 강인함(피해 -1)을 얻습니다. (간이 구현: 첫 10턴 동안 피해 -1)</summary>
    public class ToughnessTenTurnsRelic : RelicEffect
    {
        public override void OnPlayerDamaged(RelicCombatContext ctx, ref int damage)
        {
            if (ctx?.Board == null) return;
            if (ctx.Board.TurnNumber <= 10 && damage > 0)
            {
                damage = Mathf.Max(0, damage - 1);
            }
        }
    }

    /// <summary>602 원하는 카드 한 장 얻음 (현재 직업의 보상 풀에서 자유 선택).</summary>
    public class ChooseAnyCardRelic : RelicEffect
    {
        public override RelicCategory Category => RelicCategory.Temporary;
        public override void OnRelicObtained(GameManager gm)
        {
            if (gm == null) return;
            var pool = CardRegistry.GetRewardPool(gm.SelectedCharacter)
                .OrderBy(c => c.RarityDigit)
                .ThenBy(c => c.cardName)
                .ToList();
            if (pool.Count == 0) return;
            InGameUIController.Instance?.OpenCardPicker(pool, picked =>
            {
                if (picked == null) return;
                DeckManager.Instance?.AddCardToDeck(picked.Clone());
                Debug.Log($"[ChooseAnyCardRelic] '{picked.cardName}' 덱에 추가");
            });
        }
    }

    /// <summary>603 20턴이 지나면 모든 적을 처치합니다.</summary>
    public class TwentyTurnSweepRelic : RelicEffect
    {
        public override void OnPlayerTurnStart(RelicCombatContext ctx)
        {
            if (ctx?.Board == null) return;
            if (ctx.Board.TurnNumber < 20) return;
            ctx.Board.DealDamageToAllEnemies(9999);
            Debug.Log("[TwentyTurnSweepRelic] 20턴 경과 — 모든 적 처치");
        }
    }
}
