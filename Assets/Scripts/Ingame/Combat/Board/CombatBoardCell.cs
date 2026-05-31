using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using DeckRoguelike.Combat;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 전투 보드의 개별 칸
    ///
    /// [프리팹 구조]
    /// CombatBoardCell (Button + CombatBoardCell 스크립트)
    ///  ├─ Background (Image) ← backgroundImage: 원하는 스프라이트 설정
    ///  └─ Overlay    (Image) ← overlayImage: 반투명 상태 색깔 (스프라이트 위에 덮임)
    ///
    /// [상태별 오버레이 색깔]
    /// Empty           → 투명 (스프라이트만 보임)
    /// PlayerOccupied  → 파란색 반투명
    /// EnemyOccupied   → 빨간색 반투명
    /// InRange         → 노란색 반투명 (사거리 안, 적 없음)
    /// TargetableEnemy → 주황-빨간색 (사거리 안, 적 있음 → 클릭 가능)
    ///
    /// [overlayImage 없을 때]
    /// backgroundImage의 color를 직접 바꾸는 폴백 동작
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class CombatBoardCell : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        [Header("References")]
        [Tooltip("셀 배경 스프라이트 Image. 프리팹에서 원하는 Sprite를 설정하세요.")]
        [SerializeField] private Image backgroundImage;
        [Tooltip("상태 색깔을 표시하는 반투명 오버레이 Image. 없으면 backgroundImage 색을 직접 변경합니다.")]
        [SerializeField] private Image overlayImage;

        [Header("Overlay Colors")]
        [SerializeField] private Color emptyColor         = new Color(0f,    0f,    0f,    0f);    // 투명
        [SerializeField] private Color playerColor        = new Color(0.2f,  0.4f,  0.85f, 0.5f);
        [SerializeField] private Color enemyColor         = new Color(0.55f, 0.15f, 0.15f, 0.5f);
        [SerializeField] private Color inRangeColor       = new Color(0.85f, 0.75f, 0.1f,  0.5f);
        [SerializeField] private Color targetableColor    = new Color(1f,    0.3f,  0.1f,  0.75f);
        [SerializeField] private Color areaSelectColor    = new Color(0.2f,  0.85f, 0.4f,  0.55f); // AreaSelect 선택 가능 셀 (녹색)
        [SerializeField] private Color allyOccupiedColor  = new Color(0.2f,  0.7f,  0.9f,  0.5f);  // 아군 점유 (하늘색)
        [SerializeField] private Color targetableAllyColor = new Color(0.1f,  0.9f,  0.9f,  0.75f); // 아군 선택 가능 (청록색)

        [Header("Fallback Colors (overlayImage 없을 때)")]
        [SerializeField] private Color fallbackEmpty        = new Color(0.15f, 0.15f, 0.2f,  1f);
        [SerializeField] private Color fallbackPlayer       = new Color(0.2f,  0.4f,  0.85f, 1f);
        [SerializeField] private Color fallbackEnemy        = new Color(0.55f, 0.15f, 0.15f, 1f);
        [SerializeField] private Color fallbackInRange      = new Color(0.85f, 0.75f, 0.1f,  1f);
        [SerializeField] private Color fallbackTargetable   = new Color(1f,    0.3f,  0.1f,  1f);
        [SerializeField] private Color fallbackAreaSelect   = new Color(0.2f,  0.85f, 0.4f,  1f);
        [SerializeField] private Color fallbackAllyOccupied = new Color(0.2f,  0.7f,  0.9f,  1f);
        [SerializeField] private Color fallbackTargetAlly   = new Color(0.1f,  0.9f,  0.9f,  1f);

        private Button btn;
        private CellState state;
        private bool isMapMode;
        /// <summary>true면 state와 무관하게 빈 셀 클릭도 OnCellClicked 발화. Shop 자유 이동에 사용.</summary>
        public static bool ShopFreeClickEnabled;

        public Vector2Int    GridPos         { get; private set; }
        public EnemyInstance OccupyingEnemy  { get; set; }
        public AllyInstance  OccupyingAlly   { get; set; }
        /// <summary>설치(Trap kind) 위해 — 진입 즉시 1회 발동 후 제거. Zone·아군과 한 셀에 공존 가능.</summary>
        public HazardInstance OccupyingTrap { get; set; }
        /// <summary>생성(Zone kind) 위해 — 적 턴 종료 시 반복 발동, 사라지지 않음. Trap·아군과 한 셀에 공존 가능.</summary>
        public HazardInstance OccupyingZone { get; set; }
        public bool          IsPlayerHere    { get; set; }
        public CellState     CurrentState    => state;
        public MapNodeData   MapNode         { get; set; }

        public event Action<CombatBoardCell> OnCellClicked;
        public event Action<CombatBoardCell> OnCellHoverEnter;
        public event Action<CombatBoardCell> OnCellHoverExit;
        /// <summary>좌클릭 down 즉시 발화 — sprite swap 등 down-trigger 처리용.
        /// (sticky/타게팅 sprite의 unactive→active 전환은 hover가 아니라 이 down 이벤트 기준)</summary>
        public event Action<CombatBoardCell> OnCellPointerDown;

        private void Awake()
        {
            btn = GetComponent<Button>();
            btn.interactable = false;
            btn.transition = Selectable.Transition.None; // hover/press 시 Button 자동 색상 변경 차단

            // backgroundImage, overlayImage가 Inspector에서 연결 안 됐으면 자동 탐색
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
        }

        public void Initialize(Vector2Int pos)
        {
            GridPos        = pos;
            OccupyingEnemy = null;
            IsPlayerHere   = false;
            SetState(CellState.Empty);
        }

        public void SetState(CellState s)
        {
            state = s;
            ApplyColor(s);
            RefreshInteractable();
        }

        /// <summary>현재 state와 ShopFreeClickEnabled 기준으로 button.interactable 재평가.</summary>
        public void RefreshInteractable()
        {
            if (btn == null) return;
            btn.interactable = ShopFreeClickEnabled || (state == CellState.TargetableEnemy || state == CellState.AreaSelectable || state == CellState.TargetableAlly);
        }

        // mousedown 즉시가 아니라 click(=down+up at same target) 으로 발화한다.
        // sticky 카드가 떠 있는 상태에서 mousedown으로 카드가 즉시 실행되던 문제를 막기 위함.
        public void OnPointerClick(PointerEventData eventData)
        {
            UnityEngine.Debug.Log($"[CombatBoardCell] OnPointerClick {GridPos} state={state} ShopFreeClickEnabled={ShopFreeClickEnabled} btn.interactable={btn?.interactable} button={eventData.button}");
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (isMapMode)
            {
                if (btn.interactable) OnCellClicked?.Invoke(this);
                return;
            }
            if (state != CellState.TargetableEnemy && state != CellState.AreaSelectable && state != CellState.TargetableAlly)
            {
                if (!ShopFreeClickEnabled)
                {
                    UnityEngine.Debug.Log($"[CombatBoardCell] {GridPos} 클릭 차단 — state={state}, ShopFreeClickEnabled=false");
                    return;
                }
                UnityEngine.Debug.Log($"[CombatBoardCell] {GridPos} ShopFreeClick 통과 → OnCellClicked 발화");
            }
            OnCellClicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData) => OnCellHoverEnter?.Invoke(this);
        public void OnPointerExit(PointerEventData eventData)  => OnCellHoverExit?.Invoke(this);

        // 좌클릭 down에서 sprite swap 등 즉시 트리거. (마우스 우클릭은 무시)
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            OnCellPointerDown?.Invoke(this);
        }

        /// <summary>드래그/sticky에서 직접 발동 — 활성화된 셀(TargetableEnemy/AreaSelectable/TargetableAlly)만 허용</summary>
        public void TriggerClick()
        {
            if (state != CellState.TargetableEnemy && state != CellState.AreaSelectable && state != CellState.TargetableAlly) return;
            OnCellClicked?.Invoke(this);
        }

        public void SetMapMode(bool mapMode) { isMapMode = mapMode; }

        public void SetMapVisual(Sprite icon, Color color, bool interactable)
        {
            if (backgroundImage != null)
            {
                if (icon != null) backgroundImage.sprite = icon;
                backgroundImage.color = color;
            }
            if (overlayImage != null)
                overlayImage.color = emptyColor;
            btn.interactable = interactable;
        }

        private void ApplyColor(CellState s)
        {
            if (overlayImage != null)
            {
                // overlayImage에 반투명 색 적용 → 스프라이트가 아래로 비쳐 보임
                overlayImage.color = s switch
                {
                    CellState.PlayerOccupied  => playerColor,
                    CellState.EnemyOccupied   => enemyColor,
                    CellState.InRange         => inRangeColor,
                    CellState.TargetableEnemy => targetableColor,
                    CellState.AreaSelectable  => areaSelectColor,
                    CellState.AllyOccupied    => allyOccupiedColor,
                    CellState.TargetableAlly  => targetableAllyColor,
                    _                         => emptyColor,
                };
            }
            else if (backgroundImage != null)
            {
                // overlayImage 없으면 backgroundImage 색 직접 변경 (폴백)
                backgroundImage.color = s switch
                {
                    CellState.PlayerOccupied  => fallbackPlayer,
                    CellState.EnemyOccupied   => fallbackEnemy,
                    CellState.InRange         => fallbackInRange,
                    CellState.TargetableEnemy => fallbackTargetable,
                    CellState.AreaSelectable  => fallbackAreaSelect,
                    CellState.AllyOccupied    => fallbackAllyOccupied,
                    CellState.TargetableAlly  => fallbackTargetAlly,
                    _                         => fallbackEmpty,
                };
            }
        }

        /// <summary>카드 hover 미리보기 색 표시 (클릭 불가 유지)</summary>
        public void ShowHoverPreview(Color color)
        {
            if (overlayImage != null)
                overlayImage.color = color;
            else if (backgroundImage != null)
                backgroundImage.color = color;
        }

        /// <summary>hover 미리보기 제거 → 현재 state 색으로 복원</summary>
        public void ClearHoverPreview()
        {
            ApplyColor(state);
        }

        /// <summary>사거리 하이라이트만 제거, 유닛 상태 유지</summary>
        public void ClearHighlight()
        {
            if      (state == CellState.InRange)         SetState(CellState.Empty);
            else if (state == CellState.TargetableEnemy) SetState(CellState.EnemyOccupied);
            else if (state == CellState.TargetableAlly)  SetState(OccupyingAlly != null ? CellState.AllyOccupied : IsPlayerHere ? CellState.PlayerOccupied : CellState.Empty);
            else if (state == CellState.AreaSelectable)  SetState(OccupyingEnemy != null ? CellState.EnemyOccupied : CellState.Empty);
        }

        // ── 지대(Zone) 타일 스프라이트 ──────────────────────────────
        // 용암/빙하지대 등 Zone 위해가 깔리면 배경 타일 스프라이트를 교체한다.
        // 최초 교체 시 원본 배경 스프라이트를 보존했다가 Zone 제거 시 복원.
        private Sprite _baseBackgroundSprite;
        private bool   _zoneSpriteApplied;

        /// <summary>지대 타일 스프라이트로 배경을 교체. 원본은 보존했다가 ClearZoneSprite로 복원.</summary>
        public void SetZoneSprite(Sprite sprite)
        {
            if (backgroundImage == null || sprite == null) return;
            if (!_zoneSpriteApplied)
            {
                _baseBackgroundSprite = backgroundImage.sprite;
                _zoneSpriteApplied    = true;
            }
            backgroundImage.sprite = sprite;
        }

        /// <summary>지대 타일 스프라이트 제거 → 원본 배경 스프라이트로 복원.</summary>
        public void ClearZoneSprite()
        {
            if (backgroundImage == null || !_zoneSpriteApplied) return;
            backgroundImage.sprite = _baseBackgroundSprite;
            _zoneSpriteApplied     = false;
        }

        /// <summary>유닛 사망/이동 시 셀 초기화 (함정은 유지 — 별도 BoardController에서 관리)</summary>
        public void ClearUnit()
        {
            OccupyingEnemy = null;
            OccupyingAlly  = null;
            IsPlayerHere   = false;
            SetState(CellState.Empty);
        }
    }

    public enum CellState
    {
        Empty,
        PlayerOccupied,
        EnemyOccupied,
        InRange,          // 사거리 안, 빈 셀 (클릭 불가)
        TargetableEnemy,  // 사거리 안, 적 있음 → 클릭 가능
        AreaSelectable,   // AreaSelect 모드: 적 유무와 무관하게 클릭 가능한 조준 셀
        AllyOccupied,     // 아군 점유 셀
        TargetableAlly,   // Ally 타겟팅 모드: 자신/아군 셀 클릭 가능
    }
}
