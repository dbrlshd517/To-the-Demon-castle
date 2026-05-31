using DeckRoguelike.Cards;
using DeckRoguelike.Combat;
using DeckRoguelike.Core;
using DeckRoguelike.UI;

namespace DeckRoguelike.Relic
{
    public enum RelicCategory
    {
        Combat,
        NonCombat,
        Temporary
    }

    /// <summary>
    /// RelicRegistry에 등록된 함수에 전달되는 컨텍스트.
    /// </summary>
    public class RelicCombatContext
    {
        public BoardController Board;
    }

    /// <summary>
    /// 유물 효과의 추상 기반 클래스.
    ///
    /// [사용법]
    ///   1. 이 클래스를 상속해 구체 유물 클래스를 작성합니다.
    ///   2. RelicLibrary.RegisterAll()에서 RelicRegistry.Register(data, () => new MyRelic())로 등록합니다.
    ///
    /// [훅 호출 시점]
    ///   OnRelicObtained      : 유물 획득 시 1회
    ///   OnCombatStart        : 전투 시작 시
    ///   OnPlayerTurnStart    : 플레이어 턴 시작마다
    ///   OnPlayerTurnEnd      : 플레이어 턴 종료마다
    ///   OnCardPlayed         : 카드 사용 시마다
    ///   OnPlayerDamaged      : 플레이어가 피해를 받기 직전 (damage를 수정 가능)
    ///   OnPlayerDamagedBy    : 피해 확정 후, 공격한 적 정보 포함 (반사 등)
    ///   OnPlayerMoved        : 플레이어가 이동할 때마다 (누적 이동 횟수 포함)
    ///   OnBeforeRestHeal     : 휴식 회복량 결정 직전 (healPercent 수정 가능 - 116 유물)
    ///   OnAfterRestHeal      : 휴식 회복 처리 직후 (추가 flat 회복용)
    ///   OnRestEnter          : 휴식 노드 진입 시 1회 (103 유물 - 자동 HP +15)
    ///   OnShopOpen           : 상점 진입 시 1회 (104 유물)
    ///   OnMapMove            : 맵 이동이 확정될 때마다 (114 유물 - 이동마다 골드)
    ///   OnCardObtained       : 덱에 새 카드가 추가될 때 (212~215 유물 강화 등)
    ///   OnEnemyKilled        : 적이 사망할 때마다
    ///   OnEnemyArmorBroken   : 적 방어도가 0으로 깨질 때마다 (205 유물)
    ///   OnCombatVictory      : 전투 승리 시
    ///   OnPlayerLostHp       : 플레이어 HP가 실제로 감소했을 때 (970 유물)
    ///   OnCardExhausted      : 카드가 소멸될 때마다 (971 유물)
    ///   OnCombatRewardGenerated: 전투 보상이 생성될 때 — 추가 보상 유물(900) 등에서 사용
    /// </summary>
    public abstract class RelicEffect
    {
        public RelicData Data { get; internal set; }

        /// <summary>유물 분류: Combat(전투), NonCombat(비전투), Temporary(일시적).</summary>
        public virtual RelicCategory Category => RelicCategory.Combat;

        /// <summary>아이콘 위에 표시할 카운터 텍스트. null이면 숨깁니다.</summary>
        public virtual string CounterText => null;

        /// <summary>카운터 텍스트가 바뀔 때 발생합니다. UI에서 구독하여 갱신합니다.</summary>
        public event System.Action OnCounterChanged;

        /// <summary>파생 클래스에서 카운터가 변경될 때 호출합니다.</summary>
        protected void NotifyCounterChanged() => OnCounterChanged?.Invoke();

        /// <summary>유물을 처음 획득할 때 1회 호출됩니다.</summary>
        public virtual void OnRelicObtained(GameManager gm) { }

        /// <summary>전투가 시작될 때 호출됩니다.</summary>
        public virtual void OnCombatStart(RelicCombatContext ctx) { }

        /// <summary>플레이어 턴이 시작될 때마다 호출됩니다.</summary>
        public virtual void OnPlayerTurnStart(RelicCombatContext ctx) { }

        /// <summary>플레이어 턴이 종료될 때마다 호출됩니다.</summary>
        public virtual void OnPlayerTurnEnd(RelicCombatContext ctx) { }

        /// <summary>카드를 사용할 때마다 호출됩니다.</summary>
        public virtual void OnCardPlayed(RelicCombatContext ctx, CardData card) { }

        /// <summary>
        /// 플레이어가 피해를 받기 직전 호출됩니다.
        /// damage 값을 직접 수정해 피해량을 줄이거나 늘릴 수 있습니다.
        /// </summary>
        public virtual void OnPlayerDamaged(RelicCombatContext ctx, ref int damage) { }

        /// <summary>
        /// 플레이어가 실제 피해를 받은 직후 호출됩니다.
        /// attacker가 null이 아니면 해당 적이 공격한 것입니다 (반사 등에 활용).
        /// </summary>
        public virtual void OnPlayerDamagedBy(RelicCombatContext ctx, int damage, EnemyInstance attacker) { }

        /// <summary>
        /// 플레이어가 이동할 때마다 호출됩니다.
        /// totalMoveCount는 현재 전투에서의 누적 이동 횟수입니다.
        /// </summary>
        public virtual void OnPlayerMoved(RelicCombatContext ctx, int totalMoveCount) { }

        /// <summary>
        /// 휴식 회복량을 결정하기 직전 호출됩니다.
        /// healPercent를 수정해 회복량을 늘릴 수 있습니다.
        /// </summary>
        public virtual void OnBeforeRestHeal(ref float healPercent) { }

        /// <summary>
        /// 휴식 회복 처리가 끝난 직후 호출됩니다 (103 유물 - 휴식 시 flat HP +15).
        /// </summary>
        public virtual void OnAfterRestHeal(GameManager gm) { }

        /// <summary>
        /// 상점에 진입할 때 호출됩니다 (104 유물 - 상점에서 HP +15).
        /// </summary>
        public virtual void OnShopOpen(GameManager gm) { }

        /// <summary>
        /// 휴식 노드에 진입할 때 호출됩니다 (103 유물 - 휴식 진입 시 HP +15).
        /// OnShopOpen(104)과 대칭되는 휴식 버전입니다.
        /// </summary>
        public virtual void OnRestEnter(GameManager gm) { }

        /// <summary>
        /// 맵에서 이동이 확정될 때마다 호출됩니다 (114 유물 - 이동마다 골드 획득).
        /// </summary>
        public virtual void OnMapMove(GameManager gm) { }

        /// <summary>
        /// 덱에 새 카드가 추가될 때 호출됩니다. card는 이미 복제된 인스턴스입니다.
        /// 강화된 카드로 대체하려면 replacement에 대체 CardData를 넣으면
        /// DeckManager가 교체해 넣습니다 (212~215 유물).
        /// </summary>
        public virtual void OnCardObtained(CardData card, ref CardData replacement) { }

        /// <summary>적이 사망할 때마다 호출됩니다.</summary>
        public virtual void OnEnemyKilled(RelicCombatContext ctx, EnemyInstance enemy) { }

        /// <summary>
        /// 적의 방어도가 0으로 떨어질 때 호출됩니다 (205 유물).
        /// DamageEnemy에서 Block 흡수 직후, Block이 막 0이 된 경우만 발동합니다.
        /// </summary>
        public virtual void OnEnemyArmorBroken(RelicCombatContext ctx, EnemyInstance enemy) { }

        /// <summary>전투에서 승리할 때 호출됩니다.</summary>
        public virtual void OnCombatVictory(RelicCombatContext ctx) { }

        /// <summary>
        /// 플레이어가 실제로 HP를 잃었을 때 호출됩니다 (방어도 차감 후 amount &gt; 0).
        /// 전투 중일 때만 ctx.Combat이 null이 아닙니다 (970 유물).
        /// </summary>
        public virtual void OnPlayerLostHp(RelicCombatContext ctx, int amount) { }

        /// <summary>카드가 소멸 더미로 이동할 때 호출됩니다 (971 유물).</summary>
        public virtual void OnCardExhausted(RelicCombatContext ctx, CardData card) { }

        /// <summary>적이 실제 피해를 받은 직후 호출됩니다 (Block 차감 후). (316 유물 — 적 피해 입을 때마다 5 피해)</summary>
        public virtual void OnEnemyDamaged(RelicCombatContext ctx, EnemyInstance enemy, int amount) { }

        /// <summary>플레이어 턴 종료 시 손패가 버려졌을 때 버린 카드 수와 함께 호출됩니다 (303 유물).</summary>
        public virtual void OnHandDiscarded(RelicCombatContext ctx, int count) { }
    }
}
