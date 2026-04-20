using DeckRoguelike.Cards;
using DeckRoguelike.Combat;
using DeckRoguelike.Core;
using DeckRoguelike.UI;

namespace DeckRoguelike.Relic
{
    /// <summary>
    /// RelicRegistry에 등록된 함수에 전달되는 컨텍스트.
    /// </summary>
    public class RelicCombatContext
    {
        public CombatController Combat;
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
    ///   OnBeforeRestHeal     : 휴식 회복량 결정 직전 (healPercent 수정 가능)
    ///   OnEnemyKilled        : 적이 사망할 때마다
    ///   OnCombatVictory      : 전투 승리 시
    /// </summary>
    public abstract class RelicEffect
    {
        public RelicData Data { get; internal set; }

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

        /// <summary>적이 사망할 때마다 호출됩니다.</summary>
        public virtual void OnEnemyKilled(RelicCombatContext ctx, EnemyInstance enemy) { }

        /// <summary>전투에서 승리할 때 호출됩니다.</summary>
        public virtual void OnCombatVictory(RelicCombatContext ctx) { }
    }
}
