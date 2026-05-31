using DeckRoguelike.Core;
using DeckRoguelike.UI;

namespace DeckRoguelike.Item
{
    /// <summary>
    /// ItemEffect.OnItemUsed()에 전달되는 컨텍스트.
    /// 전투 중 사용 시 Combat이 채워지고, 전투 밖에서 사용 시 null입니다.
    /// </summary>
    public class ItemUseContext
    {
        public GameManager GM;
        public BoardController Board;

        /// <summary>전투 중에 사용되었으면 true.</summary>
        public bool InCombat => Board != null;

        /// <summary>
        /// NeedsTargetingMode 아이템 전용.
        /// 타겟 확정 시 CombatController가 이 콜백을 호출해 아이템을 인벤토리에서 소모합니다.
        /// </summary>
        public System.Action OnConfirmedUse;
    }

    /// <summary>
    /// 아이템 효과의 추상 기반 클래스.
    /// 아이템은 일회용이므로 OnItemUsed 호출 후 인벤토리에서 자동으로 제거됩니다.
    ///
    /// [사용법]
    ///   1. 이 클래스를 상속해 구체 아이템 클래스를 작성합니다.
    ///   2. ItemLibrary의 effectFactories 딕셔너리에 등록합니다.
    ///   3. GameManager.UseItem(itemCode, combat)으로 사용합니다.
    ///
    /// [CanUse]
    ///   전투 전용 포션처럼 특정 조건에서만 사용 가능한 아이템은
    ///   CanUse를 오버라이드해 false를 반환하세요.
    ///   UI에서 사용 불가 표시 및 사용 차단에 활용합니다.
    /// </summary>
    public abstract class ItemEffect
    {
        public ItemData Data { get; internal set; }

        /// <summary>
        /// 아이템을 사용할 때 1회 호출됩니다.
        /// 호출이 끝나면 인벤토리에서 자동 제거됩니다.
        /// </summary>
        public abstract void OnItemUsed(ItemUseContext ctx);

        /// <summary>
        /// 현재 컨텍스트에서 이 아이템을 사용할 수 있는지 여부.
        /// 전투 전용 아이템이라면 ctx.InCombat을 확인하세요.
        /// 기본값은 항상 true입니다.
        /// </summary>
        public virtual bool CanUse(ItemUseContext ctx) => true;

        /// <summary>
        /// true이면 사용 즉시 소모되지 않고, 타겟 선택 후 ctx.OnConfirmedUse()로 소모됩니다.
        /// 화살·폭탄·화염병 등 타겟팅이 필요한 아이템에서 true를 반환합니다.
        /// </summary>
        public virtual bool NeedsTargetingMode => false;
    }
}
