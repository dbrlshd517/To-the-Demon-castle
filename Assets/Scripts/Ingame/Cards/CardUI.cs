using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
using DeckRoguelike.Core;
using DeckRoguelike.UI;
using Loc = DeckRoguelike.Core.LocalizationManager;

namespace DeckRoguelike.Cards
{
    /// <summary>
    /// 개별 카드 UI 및 인터랙션 처리
    /// </summary>
    public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                          IPointerDownHandler, IPointerUpHandler,
                          IBeginDragHandler, IDragHandler, IEndDragHandler,
                          IInitializePotentialDragHandler
    {
        [Header("UI References")]
        [SerializeField] private Image cardBackground;
        [SerializeField] private Image cardArt;
        [SerializeField] private Image cardFrame;
        [SerializeField] private TextMeshProUGUI cardTypeText;
        [SerializeField] private TextMeshProUGUI cardNameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Image costBackground;
        [SerializeField] private Image cardIcon;
        [SerializeField] private GameObject rarityGlow;
        [SerializeField] private GameObject upgradeIndicator;
        [SerializeField] private TextMeshProUGUI goldText;

        [Header("Rarity Colors")]
        [SerializeField] private Color commonRarityColor = Color.white;
        [SerializeField] private Color uncommonRarityColor = new Color(0.3f, 0.5f, 1f);
        [SerializeField] private Color rareRarityColor = new Color(1f, 0.8f, 0.2f);

        [Header("Animation Settings")]
        [SerializeField] private float hoverScale = 1.2f;
        [SerializeField] private float hoverYOffset = 50f;
        [SerializeField] private float animationSpeed = 10f;
        [Tooltip("hover 진입/이탈 lerp 시간(초)")]
        [SerializeField] private float hoverAnimDuration = 0.12f;
        [Tooltip("sticky 해제 후 손패 복귀 lerp 시간(초). 이 시간 동안 같은 카드의 hover 가 차단된다.")]
        [SerializeField] private float stickyReleaseAnimDuration = 0.18f;
        [SerializeField] private float dragCollapseScale = 1f;
        [Tooltip("카드가 보드 위로 올라가 축소될 때 추가로 내려가는 로컬 Y 오프셋")]
        [SerializeField] private float dragCollapseYOffset = 0f;

        /// <summary>
        /// sticky 상태 카드가 이 화면 Y(px) 이상으로 올라가면 원래 크기로 축소됩니다.
        /// CombatController가 RepositionHandCards()마다 갱신합니다.
        /// Inspector의 cardShrinkBoardScreenY로 조정하세요.
        /// </summary>
        public static float BoardCollapseScreenY { get; set; } = 400f;

        [Header("Audio")]
        [SerializeField] private AudioClip hoverSound;
        [SerializeField] private AudioClip pickupSound;
        [SerializeField] private AudioClip playSound;

        // Data
        public CardData CardData { get; private set; }
        private int cachedStrength;
        private int cachedDexterity;
        public int HandIndex { get; set; }
        public int DrawOrder { get; set; }
        public bool IsPlayable { get; set; } = true;
        public bool IsRuntimeUnplayable { get; set; }
        /// <summary>셀 선택이 필요한 카드(Enemy/Any/Ally). true이면 클릭 시 즉시 targeting 진입.</summary>
        public bool IsRangeCard { get; private set; }
        /// <summary>범위 자동 액션 카드(All/Random) — 셀 선택은 없지만 hover 시 미리보기를 표시해야 함.</summary>
        public bool IsAreaCard { get; private set; }
        public bool IsSelfPlayCard => CardData != null &&
            (CardData.CardTypeFromCode == CardType.Power || IsAreaCard
             || CardData.PrimaryTargeting == TargetType.Self);

        /// <summary>상점 카드 모드. true면 Power 카드와 동일한 sticky/collapse 흐름을 따르며,
        /// 보드 위에서 release 시에만 PlayCard(=OnCardPlayed)가 발화됨.</summary>
        public bool IsShopMode { get; set; }
        /// <summary>BoardCollapseScreenY 진입 시 조건 검사를 통과해 mousedown/up으로 PlayCard 발화가 허용된 상태.</summary>
        public bool CanPlayWhileSticky { get; set; }
        /// <summary>외부(BoardController)가 sticky 진입 가능 여부를 결정하기 위해 설정하는 콜백. null이면 항상 허용.</summary>
        public System.Func<CardUI, bool> StickyAllowedCheck;
        private bool UsesSelfPlayFlow => IsSelfPlayCard || IsShopMode;

        // State
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Vector3 originalScale;
        private int originalSiblingIndex;
        private bool isDragging;       // 마우스 버튼 누른 채 이동 중
        private bool isHovering;
        private bool isAnimating;
        // 드로우 애니메이션 전용 플래그/코루틴 — 사용자가 드로우 중 카드를 클릭/호버해도
        // 첫 입력이 isAnimating 차단으로 무시되지 않도록 즉시 finalize 처리.
        private bool _isInDrawAnimation;
        private Coroutine _drawCoroutine;
        private bool isSticky;        // 선택 상태 (클릭/드래그 공통) - 카드가 커서를 따라다님
        private bool isPlayed;        // 이미 실행되어 더 이상 처리 불필요
        private bool isLocked;        // 타겟팅 모드 중 이동 잠금 (range 카드 전용)
        public bool IsLocked => isLocked;
        private Vector3 stickyOffset; // 선택 시 커서와 카드 중심 간 오프셋
        private Vector2 mouseDownPos; // 마우스 누른 위치 (드래그/클릭 판별용)
        // OnCardViewClicked를 PointerUp에서 발화하기 위한 후보 플래그.
        // PointerDown 시점에 click 자격(click disabled/포션픽/선택패널 등 제외)을 통과하면 true,
        // PointerUp 시점에 down→up 거리가 ViewClickMovementThreshold 이하면 이벤트 발화.
        // Button과 달리 작은 드래그를 click으로 허용 → ScrollRect 안에서 미세 이동해도 클릭 인정.
        private bool _pendingViewClick;
        private const float ViewClickMovementThreshold = 20f;
        // 103/104 포션 픽 후보 플래그 — PointerDown 시점에 후보 등록, PointerUp 시점에 down→up 거리가
        // ViewClickMovementThreshold 이하면 OnPotionPick 발화. mousedown 즉시 발화가 아니라 button-like
        // click으로 동작시켜 drawPanel/discardPanel ScrollRect 드래그(스크롤)와 카드 선택을 구분한다.
        private bool _pendingPotionPick;
        private float _collapsedEntryWorldY = float.MinValue; // 보드 경계 진입 순간 기록한 카드 world Y
        private float _collapseClampWorldY;                   // 실제 lerp 중인 카드 Y (world 단위)
        private bool _wasCollapsed;
        // Update 경로(locked+collapsed)에서 mousedown 즉시 발화가 아니라 click(=press+release) 으로 발화하기 위한 후보 플래그.
        // sticky-not-drag 상태에서 마우스 클릭으로 카드를 실행하려는 요구사항에 맞춰 도입.
        private bool _updateClickPending;
        // sticky가 아닌데 마우스가 collapse(보드) 영역으로 올라가 hover를 강제 취소한 상태인지.
        // true인 동안 마우스가 영역 아래로 내려오면 커서가 카드 위일 때 hover를 복귀시킨다.
        private bool _hoverSuppressedByCollapse;
        // sticky 카드(축소 상태)를 누르고 있는 동안 확대+위로 올려 내용을 확인하는 시각 래치.
        // 누르는 순간 켜지고 버튼을 떼면 꺼진다(카드가 올라가 커서를 벗어나도 유지 → 깜빡임 방지). Update가 관리.
        private bool _inspectingSticky;
        // sticky 카드 재클릭(inspect)으로 시작한 press인지 — release 시 SelfPlay 플레이를 차단하는 용도.
        // OnPointerDown(이미 sticky인 카드)에서 켜고 OnPointerUp에서 끄며 early-return한다.
        private bool _inspectSuppressPlay;
        // sticky 진입 프레임 — 픽업 프레임의 mousedown을 inspect로 오인하지 않기 위한 가드.
        private int _stickyEntryFrame = -1;

        // 프리팹 원본 사이즈 (인스턴스화 직후 Awake에서 캡처)
        private Vector2 _prefabSizeDelta;

        // true일 때만 ignoreLayout 및 크기 강제를 적용 (전투 손패 전용)
        private bool _isHandCard = false;

        // 드래그/sticky 중인 카드가 있으면 다른 카드 hover 차단
        private static CardUI interactingCard;
        // sticky가 해제된 직후 같은 클릭 사이클에서 즉시 다른 카드가 sticky되는 것을 막는 가드.
        // Deselect가 호출되면 _suppressStickyUntilFrame을 다음 프레임으로 세팅 — OnPointerDown에서 검사.
        // collapseScreenY를 충족하지 못한 채 sticky가 풀린 경우 다른 카드로 sticky가 연쇄되는 것을 방지.
        private static int _suppressStickyUntilFrame = -1;
        private Canvas canvas;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private AudioSource audioSource;
        private Coroutine repositionCoroutine;
        // hover enter/exit lerp 코루틴 — 새로 시작할 때 이전 것을 중지하여 깜빡임 방지.
        private Coroutine _hoverAnimCoroutine;
        // sticky 해제 → 손패 복귀 lerp 코루틴. != null 동안 같은 카드의 hover 시각은 차단되고 코루틴 종료 시점에 재적용된다.
        private Coroutine _stickyReleaseCoroutine;

        // sticky 상태 진입 시 Canvas 직하로 올려 hand 컨테이너 마스크 클리핑 방지
        private Transform _handParent;

        // Events
        public event System.Action<CardUI> OnCardPlayed;
        public event System.Action<CardUI> OnCardHoverEnter;
        public event System.Action<CardUI> OnCardHoverExit;
        /// <summary>타겟팅 중 선택 해제(우클릭 등)로 타겟팅을 취소해야 할 때 발화</summary>
        public event System.Action<CardUI> OnTargetingCancelled;
        /// <summary>뷰어 전용: 플레이 여부/상태와 무관하게 좌클릭 시 항상 발화</summary>
        public event System.Action<CardUI> OnCardViewClicked;
        /// <summary>카드 선택 진입 시 발화 (PointerDown → sticky 상태 진입 직후)</summary>
        public event System.Action<CardUI> OnCardDown;
        /// <summary>sticky 중 카드가 BoardCollapseScreenY 위로 처음 진입할 때 발화</summary>
        public event System.Action<CardUI> OnCardCollapseEnter;
        /// <summary>sticky 중 카드가 BoardCollapseScreenY 아래로 내려갈 때 발화</summary>
        public event System.Action<CardUI> OnCardCollapseExit;

        private void Update()
        {
            if (isAnimating) return;
            // CardInfoController가 떠 있는 동안에는 collapse/sticky/hover 트래킹 모두 동결
            if (DeckRoguelike.UI.CardInfoController.IsAnyOpen) return;

            // sticky가 아닐 때: 마우스가 collapse(보드) 영역으로 올라가면 hover 시각을 취소하고,
            // 다시 영역 아래로 내려오면 커서가 카드 위일 때 hover를 복귀시킨다.
            if (!isSticky)
            {
                UpdateNonStickyHoverCollapse();
                return;
            }

            // 오른쪽 마우스 버튼 클릭 시 선택 해제
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                Debug.Log($"[CardUI:{name}] 우클릭 감지 → Deselect 호출 — isLocked={isLocked} isSticky={isSticky}");
                Deselect();
                Debug.Log($"[CardUI:{name}] Deselect 후 — isLocked={isLocked} isSticky={isSticky}");
                return;
            }

            Vector2 mousePos = Mouse.current.position.ReadValue();

            // isLocked(타겟팅 중)이면 커서 추적 안 함 (카드 고정)
            bool collapsed = mousePos.y >= BoardCollapseScreenY;

            // collapsed 진입/이탈 이벤트
            if (collapsed && !_wasCollapsed)
            {
                _wasCollapsed = true;
                OnCardCollapseEnter?.Invoke(this);
            }
            else if (!collapsed && _wasCollapsed)
            {
                _wasCollapsed = false;
                OnCardCollapseExit?.Invoke(this);
            }

            if (!isLocked)
            {
                Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    canvas.GetComponent<RectTransform>(), mousePos, cam, out Vector3 worldPos
                );
                Vector3 targetPos = worldPos + stickyOffset;
                if (!collapsed)
                    _collapsedEntryWorldY = float.MinValue; // 경계 이탈 시 리셋
                transform.position = targetPos;
            }

            // Skill/Power/Shop: locked + collapsed 상태에서 마우스 클릭(=press+release) → PlayCard.
            // (drag가 아닌 sticky 상태에서는 mousedown 즉시 발화가 아니라 click으로 동작해야 함)
            // IsShopMode 포함 — shop의 Attack/Move 카드도 IsSelfPlayCard=false지만 동일 흐름이 필요.
            // Update는 raycast를 우회하므로 카드의 blocksRaycasts 상태와 무관하게 동작 → shop에서 raycast가
            // 어떤 이유로 차단되어도(SetLocked, StartDrawAnimation interrupt 등) 마우스 클릭으로 구매 가능.
            if (Mouse.current.leftButton.wasPressedThisFrame
                && isLocked && (IsSelfPlayCard || IsShopMode) && collapsed
                && !IsMouseInsideCardRect(Mouse.current.position.ReadValue()))
            {
                // 카드 위 mousedown은 OnPointerDown이 처리(=sticky 토글). Update 경로는 카드 밖 click 전용.
                _updateClickPending = true;
            }
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                bool wasPending = _updateClickPending;
                _updateClickPending = false;
                if (wasPending && isLocked && (IsSelfPlayCard || IsShopMode) && collapsed)
                {
                    Debug.Log($"[CardUI:{name}] Update — locked+collapsed+click(release) → PlayCard (IsShopMode={IsShopMode} IsSelfPlayCard={IsSelfPlayCard})");
                    PlayCard();
                    return;
                }
            }

            // inspect 래치: 축소된 sticky 카드를 누르고 있는 동안 확대 + 위로 올려 내용을 확인.
            // range 카드는 sticky 시 blocksRaycasts=false라 OnPointerDown이 안 오므로 raycast에 의존하지
            // 않고 Update에서 직접 감지한다 (모든 카드 타입에서 동작).
            //  - 시작: 이미 sticky인 카드(픽업 프레임 제외) 위에서 새 mousedown.
            //  - 유지: 버튼을 떼기 전까지 (카드가 위로 올라가 커서를 벗어나도 유지 → 깜빡임 방지).
            if (Mouse.current.leftButton.wasPressedThisFrame
                && Time.frameCount > _stickyEntryFrame
                && IsMouseInsideCardRect(Mouse.current.position.ReadValue()))
            {
                _inspectingSticky = true;
            }
            if (!Mouse.current.leftButton.isPressed)
                _inspectingSticky = false;
            bool inspecting = _inspectingSticky;

            // 축소 및 Y 이동: isLocked 여부와 무관하게 collapsed 조건만으로 동작.
            // sticky-not-drag 상태(=Update 실행 중 !isDragging)에서도 같은 Y offset을 적용해
            // 카드가 작아지면서 동시에 아래로 내려가도록 한다.
            bool shrinkY = collapsed || !isDragging;
            if (inspecting)
            {
                // inspect 중에는 sticky로 내려간 만큼만 다시 올려 collapse 진입 전(=hover) Y로 복귀시킨다.
                // (hoverScale과 합쳐져 비-sticky hover와 동일한 크기·위치가 됨)
                if (_collapsedEntryWorldY == float.MinValue)
                {
                    _collapsedEntryWorldY = transform.position.y;
                    _collapseClampWorldY  = transform.position.y;
                }
                float targetY = _collapsedEntryWorldY; // 내려가기 전(=hover) 위치
                _collapseClampWorldY = Mathf.Lerp(_collapseClampWorldY, targetY, Time.deltaTime * 12f);
                transform.position = new Vector3(transform.position.x, _collapseClampWorldY, transform.position.z);
            }
            else if (shrinkY)
            {
                if (_collapsedEntryWorldY == float.MinValue)
                {
                    // 경계 첫 진입: 현재 카드 Y 기록, lerp 시작점도 동일하게 설정
                    _collapsedEntryWorldY = transform.position.y;
                    _collapseClampWorldY  = transform.position.y;
                }
                float targetY = _collapsedEntryWorldY - dragCollapseYOffset * canvas.scaleFactor;
                _collapseClampWorldY = Mathf.Lerp(_collapseClampWorldY, targetY, Time.deltaTime * 12f);
                transform.position = new Vector3(transform.position.x, _collapseClampWorldY, transform.position.z);
            }
            else if (isLocked)
            {
                // 보드 아래로 내려오면 Y를 collapse 진입 전 위치로 lerp 복원
                if (_collapsedEntryWorldY != float.MinValue)
                {
                    _collapseClampWorldY = Mathf.Lerp(_collapseClampWorldY, _collapsedEntryWorldY, Time.deltaTime * 12f);
                    transform.position = new Vector3(transform.position.x, _collapseClampWorldY, transform.position.z);
                    if (Mathf.Abs(_collapseClampWorldY - _collapsedEntryWorldY) < 0.5f)
                        _collapsedEntryWorldY = float.MinValue; // 복원 완료 시 리셋
                }
            }

            // 스케일: shrinkY면 축소, 단 inspect 중에는 확대.
            {
                float targetScaleMult = (shrinkY && !inspecting) ? dragCollapseScale : hoverScale;
                float curMult = originalScale.x > 0f
                    ? transform.localScale.x / originalScale.x
                    : hoverScale;
                float newMult = Mathf.Lerp(curMult, targetScaleMult, Time.deltaTime * 12f);
                transform.localScale = originalScale * newMult;
            }
        }

        /// <summary>sticky가 아닐 때 마우스가 collapse(보드) 영역에 있으면 hover 시각을 취소하고,
        /// 영역 아래로 내려오면 커서가 카드 위일 때 hover를 복귀시킨다.</summary>
        private void UpdateNonStickyHoverCollapse()
        {
            if (isLocked || isPlayed) return;
            if (_stickyReleaseCoroutine != null) return;

            float mouseY = Mouse.current.position.ReadValue().y;
            if (mouseY >= BoardCollapseScreenY)
            {
                // 보드 영역으로 올라감 → hover 시각 취소 (OnPointerExit와 동일 처리)
                if (isHovering)
                {
                    isHovering = false;
                    _hoverSuppressedByCollapse = true;
                    transform.SetSiblingIndex(originalSiblingIndex);
                    OnCardHoverExit?.Invoke(this);
                    AnimateHover(false);
                }
            }
            else if (_hoverSuppressedByCollapse)
            {
                // 보드 아래로 내려옴 → 커서가 여전히 카드 위면 hover 복귀
                _hoverSuppressedByCollapse = false;
                if (IsMouseInsideCardRect(Mouse.current.position.ReadValue()))
                    TryActivateHoverFromCursor();
            }
        }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            originalScale = transform.localScale;

            // 프리팹 내 모든 RectTransform의 소수점 오차를 정수로 스냅
            foreach (var rt in GetComponentsInChildren<RectTransform>(true))
            {
                rt.sizeDelta = new Vector2(
                    Mathf.Round(rt.sizeDelta.x),
                    Mathf.Round(rt.sizeDelta.y)
                );
            }

            // 루트 크기를 저장 (손패 모드에서만 강제 적용)
            _prefabSizeDelta = rectTransform.sizeDelta;
        }

        /// <summary>
        /// 전투 손패 모드에서 레이아웃 그룹의 크기 강제를 차단합니다.
        /// 뷰어/상점 카드는 레이아웃 그룹이 자유롭게 크기를 지정하도록 허용합니다.
        /// </summary>
        private void OnRectTransformDimensionsChange()
        {
            if (!_isHandCard) return;
            if (rectTransform == null || _prefabSizeDelta == Vector2.zero) return;
            if (rectTransform.sizeDelta != _prefabSizeDelta)
                rectTransform.sizeDelta = _prefabSizeDelta;
        }

        /// <summary>
        /// 전투 손패 카드로 초기화합니다.
        /// LayoutGroup 간섭을 차단하고 크기를 프리팹 원본으로 고정합니다.
        /// CardListPanel/ShopPanel 카드는 이 메서드를 호출하지 않습니다.
        /// </summary>
        public void SetAsHandCard()
        {
            _isHandCard = true;
            var le = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += RefreshText;
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= RefreshText;
        }

        public void RefreshText()
        {
            if (CardData == null) return;
            cardNameText.text    = ResolveLocalized($"card_name_{CardData.cardCode}", CardData.cardName);
            string rawDesc       = ResolveLocalized($"card_desc_{CardData.cardCode}", CardData.description);
            string desc          = CardData.GetFormattedDescription(rawDesc, cachedStrength, cachedDexterity);
            descriptionText.text = CardKeywordHelper.BuildDescriptionWithKeywords(desc, CardData.keywords);
            if (cardTypeText != null)
                cardTypeText.text = GetCardTypeShortLabel(CardData.CardTypeFromCode);
        }

        private string ResolveLocalized(string key, string fallback)
        {
            string result = Loc.Get(key);
            return result == key ? (fallback ?? "") : result;
        }

        /// <summary>카드가 Random_Damage 등 무작위 적 공격 커스텀 효과를 갖는지 검사합니다 (hover preview용).</summary>
        private static bool HasRandomDamageEffect(CardData data)
        {
            if (data?.Effects == null) return false;
            foreach (var eff in data.Effects)
            {
                if (eff.effectType != EffectType.Custom) continue;
                if (string.IsNullOrEmpty(eff.customEffectId)) continue;
                if (eff.customEffectId.Equals("Random_Damage", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 카드 데이터로 UI 초기화
        /// </summary>
        public void Initialize(CardData data, int strength = 0, int dexterity = 0)
        {
            CardData = data;
            cachedStrength   = strength;
            cachedDexterity  = dexterity;

            if (data == null)
            {
                Debug.LogWarning("[CardUI] 카드 데이터가 null입니다.");
                return;
            }

            IsRangeCard = data.PrimaryTargeting == TargetType.Enemy ||
                          data.PrimaryTargeting == TargetType.Any   ||
                          data.PrimaryTargeting == TargetType.Ally;
            IsAreaCard  = data.PrimaryTargeting == TargetType.All ||
                          data.PrimaryTargeting == TargetType.Random ||
                          // 무작위 적 공격 (Random_Damage 등)은 targeting이 비어 있어도 area로 처리
                          HasRandomDamageEffect(data);

            // 기본 정보
            cardNameText.text    = ResolveLocalized($"card_name_{data.cardCode}", data.cardName);
            string rawDesc       = data.GetFormattedDescription(
                ResolveLocalized($"card_desc_{data.cardCode}", data.description), strength, dexterity);
            descriptionText.text = CardKeywordHelper.BuildDescriptionWithKeywords(rawDesc, data.keywords);
            if (costText != null) costText.text = "";
            if (cardTypeText != null)
                cardTypeText.text = GetCardTypeShortLabel(data.CardTypeFromCode);

            // 키워드 툴팁 설정
            CardKeywordHelper.SetupTooltip(gameObject, data.keywords);

            // 카드 아트: cardArt 필드 우선, 없으면 Resources/CardArt/{cardCode} 로드
            if (cardArt != null)
            {
                Sprite art = data.cardArt;
                if (art == null && data.cardCode > 0)
                    art = Addressables.LoadAssetAsync<Sprite>($"Sprites/Ingame/CardArt/{data.cardCode}").WaitForCompletion();
                if (art != null)
                    cardArt.sprite = art;
            }

            // 카드 아이콘
            if (cardIcon != null)
            {
                Sprite iconSpr = null;
                // 보상 템플릿: 60001(유물)/60002(아이템)/60003(카드보상). iconCode 기반으로 Relic/Item 폴더에서 로드.
                // 60004(넘기기), 60005~60009(휴식/강화/카드제거/전투재시작/메인메뉴)는 iconCode를 사용하지 않으므로 제외.
                bool isReward = (data.cardCode >= 60001 && data.cardCode <= 60003);
                if (isReward && data.iconCode > 0)
                {
                    string folder = (data.cardCode == 60001) ? "Relic" : "Item";
                    iconSpr = Addressables.LoadAssetAsync<Sprite>($"Sprites/Ingame/{folder}/{data.iconCode}").WaitForCompletion();
                }
                else if (!isReward && data.cardCode > 0)
                {
                    iconSpr = Addressables.LoadAssetAsync<Sprite>($"Sprites/Ingame/Cardicon/{data.cardCode}").WaitForCompletion();
                }
                cardIcon.gameObject.SetActive(iconSpr != null);
                if (iconSpr != null)
                    cardIcon.sprite = iconSpr;
            }

            // 레어리티
            if (rarityGlow != null)
            {
                rarityGlow.SetActive(true);
                var glowImage = rarityGlow.GetComponent<Image>();
                if (glowImage != null)
                    glowImage.color = GetRarityColor(data.Rarity);
            }

            // 업그레이드 표시
            if (upgradeIndicator != null)
                upgradeIndicator.SetActive(data.IsUpgraded);

            ClearShopPrice();

            // 플레이 불가 상태 처리
            if (data.IsUnplayable)
            {
                SetPlayable(false);
            }
        }

        /// <summary>
        /// 카드 플레이 가능 상태 설정
        /// </summary>
        /// <summary>카드가 실행됐음을 표시 - 이후 drag/click 이벤트 무시</summary>
        public void MarkAsPlayed()
        {
            isPlayed = true;
            isSticky = false;
            isDragging = false;
            isLocked = false; // 잔여 lock 상태 정리 — Update의 locked+collapsed+click → PlayCard 경로 차단.
            _handParent = null; // 카드 사용 → 손패 복귀 불필요, 추적 해제
            canvasGroup.blocksRaycasts = false;
            if (interactingCard == this) interactingCard = null;
        }

        public void SetPlayable(bool playable)
        {
            IsPlayable = playable;

            // 코스트 색상으로만 불가 여부 표시 (alpha 변경 없음)
            if (costText != null)
                costText.color = playable ? Color.white : Color.red;
        }

        /// <summary>클릭 활성 여부. Draw/Discard 뷰어처럼 클릭이 불필요한 경우 false.</summary>
        public void SetClickable(bool clickable) => IsClickDisabled = !clickable;
        public bool IsClickDisabled { get; private set; }

        /// <summary>카드 선택 패널 모드. true면 일반 드래그/플레이 대신 OnCardChoosePanelClick 발화.</summary>
        public bool IsChoosePanelMode { get; set; }
        /// <summary>chooseCardContainer 안에 있는 동안 true. hover 확대 억제에 사용.</summary>
        public bool IsInChooseContainer { get; set; }
        /// <summary>선택 패널 모드에서 PointerDown 시 발화</summary>
        public event System.Action<CardUI> OnCardChoosePanelClick;

        // ── 다중 선택 (906/910 대격변 등) ───────────────────────────
        // SubCardListPanel을 다중 선택 모드로 띄웠을 때, 카드를 클릭하면 토글되어
        // 살짝 확대된 채 "선택됨"으로 유지되고, 다시 클릭하면 해제된다.
        // 클릭 자체는 뷰어 카드(IsRuntimeUnplayable)의 OnCardViewClicked 경로로 발화되며,
        // hover 확대는 외부 CardHoverScaler가 처리한다. 이 baseline 스케일 위에 hover가 얹힌다.
        [Tooltip("다중 선택 패널에서 '선택됨' 표시로 적용할 확대 배율")]
        [SerializeField] private float multiSelectScale = 1.12f;

        /// <summary>다중 선택 패널에서 현재 선택(확대) 표시 상태인지.</summary>
        public bool IsMultiSelected { get; private set; }
        /// <summary>선택 시 적용되는 확대 배율 — 외부 hover 스케일러가 exit 시 baseline으로 참조.</summary>
        public float MultiSelectScale => multiSelectScale;

        /// <summary>다중 선택 패널에서 이 카드의 선택 표시를 설정합니다.
        /// 선택되면 살짝 확대된 채 유지되고, 해제되면 원래 크기로 돌아갑니다.</summary>
        public void SetMultiSelected(bool selected)
        {
            IsMultiSelected = selected;
            transform.localScale = originalScale * (selected ? multiSelectScale : 1f);
        }

        /// <summary>선택 컨테이너로 이동할 때 hover/sticky/애니메이션 상태를 초기화합니다.</summary>
        public void ResetVisualForChoosePanel()
        {
            isHovering = false;
            isSticky   = false;
            isDragging = false;
            _wasCollapsed = false;
            _collapsedEntryWorldY = float.MinValue;
            if (interactingCard == this) interactingCard = null;
            if (repositionCoroutine != null) { StopCoroutine(repositionCoroutine); repositionCoroutine = null; }
            transform.localRotation = Quaternion.identity;
            transform.localScale    = originalScale;
        }

        /// <summary>
        /// 이동 잠금. 타겟팅 모드 진입 시 카드가 보드를 가리지 않도록 고정.
        /// locked=true → sticky/drag 불가, 손패 위치로 복귀
        /// </summary>
        public void SetLocked(bool locked)
        {
            isLocked = locked;
            if (locked)
            {
                // isSticky 유지 → interactingCard 유지 → 타겟팅 중 다른 카드 hover 차단
                // isDragging 유지 → PointerUp에서 드래그/클릭 분기 올바르게 처리
                isHovering = false;
                // Skill/Power/Shop: blocksRaycasts 유지 → PointerDown 계속 수신 (보드 셀 클릭 불필요)
                // Attack/Move (non-shop): false → 보드 셀이 raycast 받음
                if (!IsSelfPlayCard && !IsShopMode)
                    canvasGroup.blocksRaycasts = false;
            }
            else
            {
                isSticky = false;
                canvasGroup.blocksRaycasts = true;
                if (interactingCard == this) interactingCard = null;
            }
        }

        /// <summary>sticky는 유지한 채 lock만 해제 (CardInfoController 닫힐 때 등 일시적 해제용).</summary>
        public void UnlockKeepSticky()
        {
            Debug.Log($"[CardUI:{name}] UnlockKeepSticky 호출 — 이전 isLocked={isLocked} isSticky={isSticky}");
            isLocked = false;
            canvasGroup.blocksRaycasts = true;
        }

        /// <summary>주어진 screen 좌표가 이 카드의 RectTransform 내부에 있는지 검사합니다.
        /// raycast 차단 여부와 무관 — 시각적 점유 여부만 본다 (locked range 카드처럼 raycast가 꺼진 케이스 대응).</summary>
        private bool IsMouseInsideCardRect(Vector2 screenPos)
        {
            if (rectTransform == null) return false;
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPos, cam);
        }

        private static string GetCardTypeShortLabel(CardType type)
        {
            return type switch
            {
                CardType.Move   => "M",
                CardType.Action => "A",
                CardType.Power  => "P",
                _               => "",
            };
        }

        private Color GetRarityColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common    => commonRarityColor,
                CardRarity.Uncommon  => uncommonRarityColor,
                CardRarity.Rare      => rareRarityColor,
                CardRarity.Legendary => new Color(1f, 0.5f, 0f),
                _                    => Color.white
            };
        }

        #region Pointer Events

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsRuntimeUnplayable && !IsShopMode) return; // 뷰어 모드에서 hover 비활성화 (단, 상점 카드는 허용)
            if (DeckRoguelike.UI.BoardController.AnyItemTargetingActive) return; // 아이템 사용 모드 중 카드 hover 차단
            // 드로우 애니메이션 중 첫 호버가 무시되는 버그 방지 — 즉시 finalize 후 정상 처리.
            if (_isInDrawAnimation) FinalizeDrawAnimation();
            // isAnimating은 의도적으로 제외: AnimateReturn(=sticky 해제 후 손패 복귀) 중에도
            // 카드 사용 조건과 무관하게 hover가 정상 발동해야 한다.
            // 다른 애니메이션(discard/knife)은 blocksRaycasts=false라 OnPointerEnter 자체가 들어오지 않음.
            if (isPlayed) return;
            if (interactingCard != null && interactingCard != this) return; // 다른 카드 상호작용 중
            // chooseCardContainer 안에 있는 카드는 hover 무시 (컨테이너가 레이아웃 소유)
            if (IsInChooseContainer) return;

            // sticky가 아닌데 커서가 collapse(보드) 영역이면 hover 시각/상태를 적용하지 않는다.
            // (커서가 영역 아래로 내려오면 UpdateNonStickyHoverCollapse가 hover를 복귀시킨다.)
            if (!isSticky && Mouse.current.position.ReadValue().y >= BoardCollapseScreenY)
            {
                _hoverSuppressedByCollapse = true;
                return;
            }

            // isHovering 은 sticky 도중에도 cursor 위치를 정확히 추적하기 위해 항상 업데이트한다.
            // 그래야 sticky 해제 시 cursor가 카드 위에 있으면 ReturnToHand 가 자동으로 hover 시각을 적용할 수 있다.
            isHovering = true;
            // sticky/locked 도중에는 visual 변경 금지 — sticky 시각이 우선.
            if (isSticky || isLocked) return;
            // sticky 해제 애니메이션 진행 중에는 같은 카드의 hover 시각을 차단 — 코루틴 종료 시점에 fallback 으로 적용된다.
            if (_stickyReleaseCoroutine != null) return;

            originalSiblingIndex = transform.GetSiblingIndex();
            transform.SetAsLastSibling();

            PlaySound(hoverSound);
            OnCardHoverEnter?.Invoke(this);

            if (repositionCoroutine != null) StopCoroutine(repositionCoroutine);
            AnimateHover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (IsRuntimeUnplayable && !IsShopMode) return; // 뷰어 모드에서 hover 비활성화 (단, 상점 카드는 허용)
            if (isPlayed) return;
            if (interactingCard != null && interactingCard != this) return; // 다른 카드 드래그/sticky 중 → preview 꺼짐 방지
            // 커서가 카드를 벗어나면 collapse-suppress 상태도 해제 (영역 복귀 시 잘못 재활성화 방지)
            _hoverSuppressedByCollapse = false;
            if (!isHovering) return; // hover 중이 아닌 카드의 Exit는 무시
            // chooseCardContainer 안에 있는 카드는 hover 무시 (originalPosition/Rotation은 손패 기준이라 적용 시 튐)
            if (IsInChooseContainer) return;

            // isHovering 은 sticky 도중에도 정확히 추적해야 한다. sticky 해제 시 ReturnToHand 가
            // mouse-over 체크 외에도 이 상태를 참고할 수 있도록 항상 업데이트.
            isHovering = false;
            // sticky/locked 도중에는 visual 변경 금지 — sticky 시각이 우선.
            if (isSticky || isLocked) return;
            // sticky 해제 애니메이션 진행 중에는 visual exit 도 차단 — 손패 복귀 lerp 가 transform 을 점유하므로 충돌 방지.
            if (_stickyReleaseCoroutine != null) return;

            transform.SetSiblingIndex(originalSiblingIndex);
            OnCardHoverExit?.Invoke(this);
            AnimateHover(false);
        }

        /// <summary>
        /// 포션 선택 모드 (103 재활용/104 예지): true면 drawPanel/discardPanel 안의 카드도 클릭 가능하게 처리.
        /// 클릭 시 OnPotionPick(CardData) 이벤트가 발화되고, ItemLibrary에서 손으로 가져온 뒤 패널을 닫습니다.
        /// 카드 선택 없이 패널이 닫히면 OnPotionPickCancelled가 발화되어 아이템 소모를 막습니다.
        /// </summary>
        public static bool PotionPickModeActive { get; set; }
        public static event System.Action<CardData> OnPotionPick;
        public static event System.Action OnPotionPickCancelled;
        internal static void RaisePotionPickCancelled() => OnPotionPickCancelled?.Invoke();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            // OnCardViewClicked(카드 정보 패널 열기)는 button-like 동작으로 PointerUp에서 발화한다.
            // PointerDown 시점엔 후보로 등록만 — 자격 미달 케이스는 아래에서 false로 되돌림.
            mouseDownPos = eventData.position;
            _pendingViewClick = true;

            // 103/104 포션 사용 중이라면 drawPanel/discardPanel의 카드 클릭을 가로채서
            // 손으로 가져오고 패널을 닫는 흐름으로 진입한다. (IsClickDisabled여도 발화)
            // mousedown 즉시 발화가 아니라 button-like click(PointerUp + 거리 임계값)으로 처리 —
            // CardListController의 OnCardViewClicked와 동일하게, ScrollRect 위 드래그(스크롤)는 픽으로
            // 인정하지 않고 짧은 클릭만 카드 선택으로 받는다. (sticky/play 흐름엔 진입하지 않음)
            if (PotionPickModeActive && CardData != null)
            {
                _pendingViewClick  = false;
                _pendingPotionPick = true;
                return;
            }
            Debug.Log($"[CardUI:{name}] OnPointerDown enter — isLocked={isLocked} isSticky={isSticky} CanPlayWhileSticky={CanPlayWhileSticky} IsShopMode={IsShopMode} CardInfoOpen={DeckRoguelike.UI.CardInfoController.IsAnyOpen}");
            if (IsClickDisabled) { _pendingViewClick = false; Debug.Log($"[CardUI:{name}] OnPointerDown blocked: IsClickDisabled"); return; }
            // CardInfoController가 떠 있어도 _pendingViewClick은 유지 — 다른 카드 클릭 시 PointerUp에서
            // OnCardViewClicked를 발화해 panel 표시 카드가 전환되도록 한다.
            if (DeckRoguelike.UI.CardInfoController.IsAnyOpen) { Debug.Log($"[CardUI:{name}] OnPointerDown blocked: CardInfoController.IsAnyOpen"); return; }
            // 아이템 타겟팅 모드 중에는 카드 입력 차단
            if (DeckRoguelike.UI.BoardController.AnyItemTargetingActive) { Debug.Log($"[CardUI:{name}] OnPointerDown blocked: AnyItemTargetingActive"); return; }

            // 드로우 애니메이션 중 첫 클릭이 무시되는 버그 방지 — 즉시 finalize 후 정상 처리 진행.
            if (_isInDrawAnimation) FinalizeDrawAnimation();

            if (isAnimating || isPlayed) { Debug.Log($"[CardUI:{name}] OnPointerDown blocked: isAnimating={isAnimating} isPlayed={isPlayed}"); return; }

            // 카드 선택 패널 모드: 일반 드래그/플레이 차단 후 패널 이벤트 발화
            if (IsChoosePanelMode)
            {
                Debug.Log($"[CardUI:{name}] OnPointerDown — IsChoosePanelMode → ChoosePanelClick");
                OnCardChoosePanelClick?.Invoke(this);
                return;
            }

            // sticky 카드는 축소돼 있으므로, 누르고 있는 동안 카드를 확대(+위로)해 내용을 확인할 수 있게 한다.
            // 시각 확대(_inspectingSticky)는 Update가 관리하고, 여기서는 release 시 SelfPlay 플레이를
            // 차단하는 플래그만 세팅한다. (range 카드는 blocksRaycasts=false라 여기 안 옴 → Update가 시각 처리)
            if (isSticky)
            {
                Debug.Log($"[CardUI:{name}] OnPointerDown — sticky 카드 inspect(확대) 시작");
                _pendingViewClick    = false; // 확대 확인은 카드 정보 패널 클릭으로 처리하지 않음
                _updateClickPending  = false;
                _inspectSuppressPlay = true;  // release 시 플레이 차단 (SelfPlay/Shop)
                PlaySound(hoverSound);
                return;
            }

            // sticky 해제 코루틴이 진행 중인 동안에는 같은 카드의 재 sticky 차단.
            // 코루틴이 transform 을 lerp 로 점유 중인데 sticky 진입이 끼어들면, sticky entry 의 hoverYOffset snap 직후
            // 코루틴이 한 프레임 더 돌면서 lerp-intermediate 값으로 transform 을 덮어써 Y 가 점진적으로 내려가는 현상이 발생.
            if (_stickyReleaseCoroutine != null)
            {
                Debug.Log($"[CardUI:{name}] OnPointerDown blocked: sticky-release 코루틴 진행 중");
                _pendingViewClick = false;
                return;
            }

            // sticky 해제 직후 같은 프레임/포인터 사이클에서 즉시 다른 카드 sticky를 막는다.
            // 직전 카드가 collapseScreenY 미충족으로 해제됐는데 이어서 다른 카드가 자동 sticky되는 연쇄를 방지.
            if (Time.frameCount <= _suppressStickyUntilFrame)
            {
                Debug.Log($"[CardUI:{name}] OnPointerDown blocked: suppress-sticky guard frame={Time.frameCount} until={_suppressStickyUntilFrame}");
                return;
            }

            // 다른 카드가 선택 중이면 먼저 해제
            if (interactingCard != null && interactingCard != this)
                interactingCard.Deselect();

            // 조건 확인: isLocked / IsRuntimeUnplayable / StickyAllowedCheck (외부 BoardController 결정)
            if (isLocked || IsRuntimeUnplayable) { Debug.Log($"[CardUI:{name}] sticky entry blocked: isLocked={isLocked} IsRuntimeUnplayable={IsRuntimeUnplayable}"); return; }
            if (StickyAllowedCheck != null && !StickyAllowedCheck(this)) { Debug.Log($"[CardUI:{name}] sticky entry blocked: StickyAllowedCheck=false"); return; }

            // 선택 상태 진입 - isSticky와 isDragging 즉시 true (이동 감지 불필요)
            // hover 가 막 활성화돼 lerp 가 완료되지 않은 상태(transform 이 hand 위치 ~ hoverYOffset 사이)에서도
            // sticky 진입은 카드를 hoverYOffset 으로 즉시 끌어올려야 한다. 그렇지 않으면 hand 위치 그대로 멈춰
            // 시각적으로 "반만 보이는" 상태가 된다. → snap 자체는 isHovering 과 무관하게 항상 실행.
            if (!isHovering)
            {
                originalSiblingIndex = transform.GetSiblingIndex();
            }
            if (repositionCoroutine != null) { StopCoroutine(repositionCoroutine); repositionCoroutine = null; }
            // 호버 lerp 진행 중 sticky 진입한 경우 — 코루틴이 계속 transform 을 덮어쓰지 않도록 중지.
            // (snap 보다 먼저 stop 해야 코루틴이 다음 프레임에 snap 값을 덮어쓰지 않음.)
            if (_hoverAnimCoroutine != null) { StopCoroutine(_hoverAnimCoroutine); _hoverAnimCoroutine = null; }
            transform.localPosition = new Vector3(originalPosition.x, hoverYOffset, originalPosition.z);
            transform.localRotation = Quaternion.identity;
            transform.localScale = originalScale * hoverScale; // 호버 애니메이션 중 선택 시 즉시 완성 크기로 스냅
            isSticky   = true;
            isDragging = true;
            // isHovering 은 일부러 건드리지 않는다 — sticky 진입 직전의 hover 상태를 보존해야
            // sticky 해제 시 cursor 위치가 여전히 카드 위면 자연스럽게 hover 시각으로 전환된다.
            CanPlayWhileSticky  = false; // 새 sticky 세션 — collapseY 진입 시 다시 부여
            _inspectingSticky   = false; // 새 sticky 세션 시작 — inspect 상태 초기화
            _inspectSuppressPlay = false;
            _stickyEntryFrame   = Time.frameCount; // 픽업 프레임 mousedown을 inspect로 오인하지 않도록 기록
            transform.localRotation = Quaternion.identity; // 선택 시 수직 정렬
            // 다른 카드가 해제(애니메이션) 중이어도 손패 겹침 순서를 DrawOrder대로 정규화한 뒤 이 카드를 맨 위로.
            // (해제 중 다른 카드를 sticky할 때 겹침 순서가 어긋나는 버그 방지)
            if (_isHandCard) RestoreHandSiblingOrder();
            transform.SetAsLastSibling();
            canvasGroup.blocksRaycasts = true;
            interactingCard = this;
            
            

            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                Mouse.current.position.ReadValue(), cam, out Vector3 cursorWorld
            );
            stickyOffset = transform.position - cursorWorld;

            _collapsedEntryWorldY = float.MinValue; // 새 선택 세션 — 경계 진입 시 재기록

            PlaySound(pickupSound);
            OnCardDown?.Invoke(this);       // range 카드: PointerDown 즉시 lock 처리용
            OnCardHoverEnter?.Invoke(this); // range 카드: range preview 스프라이트 표시
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            // sticky 카드 확대 확인(inspect)에서 손을 뗌 → 플레이/해제 없이 종료. (Update가 다시 축소)
            if (_inspectSuppressPlay)
            {
                _inspectSuppressPlay = false;
                return;
            }

            // 103/104 포션 픽: mousedown이 아니라 click으로 발화. down→up 거리가 임계값 이하일 때만 픽 인정 —
            // 패널 스크롤(드래그)로 손이 미끄러진 경우엔 픽하지 않는다. (OnPointerDown에서 sticky 진입을
            // 건너뛰었으므로 이후 sticky/play 로직은 실행할 필요 없이 여기서 종료.)
            if (_pendingPotionPick)
            {
                _pendingPotionPick = false;
                float pickDist = Vector2.Distance(mouseDownPos, eventData.position);
                if (pickDist <= ViewClickMovementThreshold && PotionPickModeActive && CardData != null)
                    OnPotionPick?.Invoke(CardData);
                return;
            }

            Debug.Log($"[CardUI:{name}] OnPointerUp enter — isSticky={isSticky} isLocked={isLocked} isDragging={isDragging} isPlayed={isPlayed} IsRangeCard={IsRangeCard} UsesSelfPlay={UsesSelfPlayFlow} CardInfoOpen={DeckRoguelike.UI.CardInfoController.IsAnyOpen}");

            // 뷰어 click 발화 (button-like) — IsAnyOpen 검사 전에 처리해 패널이 열린 상태에서
            // 다른 카드를 클릭하면 표시 카드가 전환되도록 한다. Down→Up 거리가 임계값 이하면 click 인정 —
            // Button과 달리 ScrollRect 위에서 미세하게 움직여도 클릭이 취소되지 않는다.
            if (_pendingViewClick)
            {
                _pendingViewClick = false;
                float dist = Vector2.Distance(mouseDownPos, eventData.position);
                if (dist <= ViewClickMovementThreshold)
                {
                    Debug.Log($"[CardUI:{name}] OnPointerUp → OnCardViewClicked 발화 (dist={dist:F1})");
                    OnCardViewClicked?.Invoke(this);
                }
                else
                {
                    Debug.Log($"[CardUI:{name}] OnPointerUp view-click 취소: 이동 거리={dist:F1} > 임계값={ViewClickMovementThreshold}");
                }
            }

            // mouseup이 mousedown한 카드 영역 안인지 (시각적 점유 기준; raycast 차단 여부와 무관).
            bool mouseOnCard = IsMouseInsideCardRect(eventData.position);

            // CardInfoController가 떠 있으면 카드 입력 전부 차단
            if (DeckRoguelike.UI.CardInfoController.IsAnyOpen) return;
            if (!isSticky || isPlayed) { Debug.Log($"[CardUI:{name}] OnPointerUp early return: !isSticky||isPlayed"); return; }

            // ── Skill/Power/Shop 전용 경로 ─────────────────────────────
            if (UsesSelfPlayFlow)
            {
                Vector2 mp = Mouse.current.position.ReadValue();
                bool collapsed = mp.y >= BoardCollapseScreenY;
                // Shop: sticky 진입한 상태에서 마우스가 collapse 영역(Y >= BoardCollapseScreenY)에 있으면 즉시 구매.
                //       CanPlayWhileSticky/CollapseEnter 이벤트 의존 없이 mouseY 직접 검사 — 골드 검사는 HandleShopCardPlay에서.
                //       이렇게 하면 shop의 모든 카드(power/attack/move)가 동일하게 "드래그→release 시 collapse 영역" 으로 구매됨.
                // Combat (Power/Skill): 기존대로 collapse 영역 + CanPlayWhileSticky 조건 모두 필요.
                bool shouldPlay = IsShopMode ? collapsed : (collapsed && CanPlayWhileSticky);
                if (shouldPlay)
                {
                    Debug.Log($"[CardUI:{name}] OnPointerUp — IsShopMode={IsShopMode} collapsed={collapsed} CanPlayWhileSticky={CanPlayWhileSticky} → PlayCard");
                    PlayCard(); // 보드 위에서 손 떼면 즉시 실행/구매
                }
                else if (mouseOnCard)
                {
                    // 카드 위에서 release → sticky-not-drag로 유지 (req: mouseup이 카드 안이어야 sticky 유지)
                    Debug.Log($"[CardUI:{name}] OnPointerUp SelfPlayFlow drag release on card → sticky-not-drag 유지");
                    isDragging = false;
                }
                else
                {
                    // 카드 밖 release → sticky 해제 (req 3)
                    Debug.Log($"[CardUI:{name}] OnPointerUp SelfPlayFlow drag release outside card → Deselect");
                    Deselect();
                }
                return;
            }

            // ── Attack/Move 기존 로직 ────────────────────────────────
            // MapState 맵 이동 카드 등 — OnCardCollapseEnter에서 CanPlayWhileSticky=true가 부여된 케이스 우선 처리.
            // 기존 IsRangeCard raycast 로직은 AreaSelectable 셀이 이미 존재해야 발동되는데, MapState 맵 이동 카드는
            // EnterMapMoveTargeting이 PlayCard 이후에 호출되므로 raycast가 빈 결과를 반환 → 영영 PlayCard가 안 됐던 문제 해결.
            {
                Vector2 mpCheck    = Mouse.current.position.ReadValue();
                bool collapsedCheck = mpCheck.y >= BoardCollapseScreenY;
                if (collapsedCheck && CanPlayWhileSticky)
                {
                    Debug.Log($"[CardUI:{name}] OnPointerUp non-SelfPlay collapsed+CanPlayWhileSticky → PlayCard");
                    PlayCard();
                    return;
                }
            }
            if (isLocked && !isDragging) { Debug.Log($"[CardUI:{name}] OnPointerUp: locked&&!dragging → return"); return; }

            if (!isDragging)
            {
                Debug.Log($"[CardUI:{name}] OnPointerUp: !dragging branch IsRangeCard={IsRangeCard}");
                if (IsRangeCard) PlayCard();
                else             Deselect();
                return;
            }

            isDragging = false;

            if (IsRangeCard)
            {
                // 타겟 가능 셀 위에서 release: 즉시 PlayCard + TriggerClick (드래그 드롭 실행)
                // 카드 영역 안에서 release: PlayCard (targeting 모드 유지)
                // 그 외 (카드 밖 + 비타겟): Deselect (req 3)
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, results);
                CombatBoardCell targetableCell = null;
                foreach (var r in results)
                {
                    // 셀의 자식(Overlay/Background image)가 raycast hit되는 경우도 있어 GetComponentInParent로 안전하게 찾는다.
                    var cell = r.gameObject.GetComponentInParent<CombatBoardCell>();
                    if (cell != null && (cell.CurrentState == CellState.TargetableEnemy
                                      || cell.CurrentState == CellState.AreaSelectable
                                      || cell.CurrentState == CellState.TargetableAlly))
                    {
                        targetableCell = cell;
                        break;
                    }
                }
                if (targetableCell != null)
                {
                    Debug.Log($"[CardUI:{name}] OnPointerUp Range drag release on targetable cell → PlayCard + TriggerClick");
                    PlayCard();
                    targetableCell.TriggerClick();
                }
                else if (mouseOnCard)
                {
                    Debug.Log($"[CardUI:{name}] OnPointerUp Range drag release on card area → PlayCard (targeting 유지)");
                    PlayCard();
                }
                else
                {
                    Debug.Log($"[CardUI:{name}] OnPointerUp Range drag release outside card & non-targetable → Deselect");
                    Deselect();
                }
            }
            else if (isLocked)
            {
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, results);
                foreach (var r in results)
                {
                    var cell = r.gameObject.GetComponent<CombatBoardCell>();
                    if (cell != null) { cell.TriggerClick(); break; }
                }
            }
        }

        #endregion

        #region Drag Events

        /// <summary>
        /// 뷰어 모드: pointerDrag는 카드 자신으로 유지 (변경하면 pointerPress≠pointerDrag → eligibleForClick=false → 클릭 불가)
        /// ScrollRect 내부 상태만 초기화해서 이후 드래그 이벤트를 전달받을 수 있게 준비.
        /// </summary>
        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (!IsRuntimeUnplayable) return;
            // pointerDrag는 건드리지 않음 — 카드를 drag handler로 유지해야 eligibleForClick이 살아있음
            GetComponentInParent<ScrollRect>()?.OnInitializePotentialDrag(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // 뷰어 모드에서만 ScrollRect로 전달 (카드 조작은 OnPointerDown/Up에서 처리)
            if (IsRuntimeUnplayable)
                GetComponentInParent<ScrollRect>()?.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (IsRuntimeUnplayable)
            {
                GetComponentInParent<ScrollRect>()?.OnDrag(eventData);
                return;
            }
            // Update()가 마우스 위치 추적 처리 → 별도 처리 없음
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (IsRuntimeUnplayable)
                GetComponentInParent<ScrollRect>()?.OnEndDrag(eventData);
        }

        #endregion

        #region Card Actions

        /// <summary>
        /// hand 컨테이너의 Mask/RectMask2D 클리핑을 피하기 위해
        /// 카드를 Canvas 직하 자식으로 올립니다. 월드 위치는 유지됩니다.
        /// </summary>
        private void DetachFromHand()
        {
            if (_handParent != null || canvas == null) return;
            _handParent = transform.parent;
            transform.SetParent(canvas.transform, worldPositionStays: true);
            transform.SetAsLastSibling();
        }

        /// <summary>
        /// Canvas에서 원래 hand 컨테이너로 복귀합니다.
        /// worldPositionStays=true 이므로 localPosition이 handContainer 기준으로 자동 재계산됩니다.
        /// </summary>
        private void ReattachToHand()
        {
            if (_handParent == null) return;
            transform.SetParent(_handParent, worldPositionStays: true);
            _handParent = null;
        }

        /// <summary>선택 상태 해제 (클릭 두 번째 / 드래그 마우스업 / 오른쪽 클릭 공통 처리)</summary>
        public void Deselect()
        {
            Debug.Log($"[CardUI:{name}] Deselect enter — isLocked={isLocked} isSticky={isSticky} isHovering={isHovering}");
            // 해제 직후 같은 클릭 사이클에서 다른 카드 sticky가 즉시 활성화되는 것을 차단 (1프레임 가드).
            _suppressStickyUntilFrame = Time.frameCount;
            if (isLocked)
            {
                Debug.Log($"[CardUI:{name}] Deselect locked branch — OnTargetingCancelled invoke (OnCardHoverExit 은 fire 하지 않는다 — sticky 해제 후 같은 카드 hover 시 range preview 깜빡임 방지)");
                // OnCardHoverExit 은 fire 하지 않는다. BoardController.CancelTargeting → ReturnToHand 흐름에서
                // isHovering=true 이면 ReturnToHand 가 OnCardHoverEnter 를 재발화해 range preview sprite 를 재활성화한다.
                OnTargetingCancelled?.Invoke(this); // CombatController → CancelTargeting
                Debug.Log($"[CardUI:{name}] Deselect locked branch end — isLocked={isLocked} isSticky={isSticky} isHovering={isHovering}");
                return;
            }
            isSticky   = false;
            isDragging = false;
            // isHovering 은 일부러 건드리지 않는다 — sticky 도중 OnPointerEnter/Exit 가 추적해놓은 정확한 값을 보존해서
            // ReturnToHand 가 자동으로 hover 시각을 재발화할 수 있도록.
            _collapsedEntryWorldY = float.MinValue;
            _wasCollapsed = false;
            canvasGroup.blocksRaycasts = true;
            if (interactingCard == this) interactingCard = null;
            // OnCardHoverExit 도 fire 하지 않는다 — 같은 카드 hover 가 ReturnToHand 에서 재발화될 때 range preview 가 깜빡이지 않도록.
            // cursor 가 카드 밖이면 ReturnToHand 가 isHovering=false 를 확인하고 hover 적용을 건너뛴다.
            ReturnToHand();
        }

        private void PlayCard()
        {
            if (isPlayed) return; // Update 경로와 OnPointerUp이 같은 프레임에서 동시 발화하더라도 중복 invoke 방지
            PlaySound(playSound);
            OnCardPlayed?.Invoke(this);
            // 시각 처리는 CombatController의 DiscardAndDestroy 또는 ReturnToHand가 담당
        }

        private System.Collections.IEnumerator PlayAnimation()
        {
            Vector3 targetPos = transform.localPosition + Vector3.up * 100f;
            Vector3 targetScale = originalScale * 0.5f;
            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, t);
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);
                canvasGroup.alpha = 1f - t;

                yield return null;
            }

            Destroy(gameObject);
        }

        private void RestoreHandSiblingOrder()
        {
            if (transform.parent == null) return;
            var siblings = new List<CardUI>();
            foreach (Transform child in transform.parent)
            {
                var card = child.GetComponent<CardUI>();
                if (card != null) siblings.Add(card);
            }
            siblings.Sort((a, b) => a.DrawOrder.CompareTo(b.DrawOrder));
            for (int i = 0; i < siblings.Count; i++)
                siblings[i].transform.SetSiblingIndex(i);
        }

        /// <summary>
        /// 손패 위치로 복귀 — sticky 해제 시 lerp 애니메이션으로 부드럽게 이동. 코루틴 종료 시점에
        /// cursor 위치를 재검사하여 hover 시각을 자동 적용한다. (Unity EventSystem 은 오브젝트가 마우스 밑으로
        /// 이동해도 Enter 를 자동 재발화하지 않으므로 fallback 이 필요.)
        /// </summary>
        public void ReturnToHand()
        {
            isAnimating = false;
            // 진행 중인 모든 코루틴 정리 — 직후 시작할 sticky-release 코루틴은 StartCoroutine 이후라 영향 없음.
            StopAllCoroutines();
            repositionCoroutine = null;
            _hoverAnimCoroutine = null;
            _stickyReleaseCoroutine = null;
            _drawCoroutine = null;

            ReattachToHand();

            if (_isHandCard) RestoreHandSiblingOrder();
            else transform.SetSiblingIndex(originalSiblingIndex);

            // 비활성 또는 isPlayed/isSticky 상태에서는 lerp 불가/불필요 → 즉시 스냅 후 종료.
            if (!gameObject.activeInHierarchy || isPlayed || isSticky)
            {
                transform.localPosition = originalPosition;
                transform.localRotation = originalRotation;
                transform.localScale    = originalScale;
                return;
            }

            _stickyReleaseCoroutine = StartCoroutine(StickyReleaseAnimCoroutine());

            // 코루틴 시작 직후 cursor 아래의 "다른" 카드는 즉시 hover 활성화 — 사용자가 sticky 해제 후
            // animation 이 끝날 때까지 기다리지 않고 바로 다른 카드를 hover 할 수 있어야 한다.
            // (Unity EventSystem 은 cursor 가 정지한 상태에서 조건이 바뀌어도 OnPointerEnter 를 재발화하지 않으므로 수동 트리거 필요.)
            // 같은 카드(this)는 TryActivateHoverFromCursor 의 _stickyReleaseCoroutine != null 가드에 막혀 자동 스킵된다.
            TryActivateHoverOnCardUnderCursor();
        }

        private System.Collections.IEnumerator StickyReleaseAnimCoroutine()
        {
            Vector3 startPos = transform.localPosition;
            Quaternion startRot = transform.localRotation;
            Vector3 startScale = transform.localScale;

            float duration = Mathf.Max(0.0001f, stickyReleaseAnimDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.localPosition = Vector3.Lerp(startPos, originalPosition, t);
                transform.localRotation = Quaternion.Slerp(startRot, originalRotation, t);
                transform.localScale    = Vector3.Lerp(startScale, originalScale, t);
                yield return null;
            }
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
            transform.localScale    = originalScale;
            _stickyReleaseCoroutine = null;

            // 애니메이션 완료 후 hover 재적용.
            if (isPlayed || isSticky) yield break;
            if (interactingCard != null && interactingCard != this) yield break;

            if (isHovering)
            {
                originalSiblingIndex = transform.GetSiblingIndex();
                transform.SetAsLastSibling();
                OnCardHoverEnter?.Invoke(this);
                AnimateHover(true);
                yield break;
            }

            // Fallback: isHovering=false 인데 cursor 가 이 카드 또는 다른 카드 rect 위에 있을 수 있음.
            // (sticky 도중 cursor 가 이미 다른 카드 위에 있던 경우 Unity 가 Enter 를 재발화하지 않으므로 필요.)
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            bool mouseOver = RectTransformUtility.RectangleContainsScreenPoint(
                rectTransform, Mouse.current.position.ReadValue(), cam);
            if (mouseOver)
            {
                isHovering = true;
                originalSiblingIndex = transform.GetSiblingIndex();
                transform.SetAsLastSibling();
                OnCardHoverEnter?.Invoke(this);
                AnimateHover(true);
                yield break;
            }

            TryActivateHoverOnCardUnderCursor();
        }

        private static readonly List<RaycastResult> _hoverRaycastBuffer = new List<RaycastResult>();
        private static void TryActivateHoverOnCardUnderCursor()
        {
            var es = EventSystem.current;
            if (es == null) return;
            var ped = new PointerEventData(es) { position = Mouse.current.position.ReadValue() };
            _hoverRaycastBuffer.Clear();
            es.RaycastAll(ped, _hoverRaycastBuffer);
            for (int i = 0; i < _hoverRaycastBuffer.Count; i++)
            {
                var card = _hoverRaycastBuffer[i].gameObject.GetComponentInParent<CardUI>();
                if (card == null) continue;
                // topmost 카드 한 장만 시도. 거부되더라도 뒤 카드로 fall-through 하지 않는다 —
                // 뒤 카드는 이 카드에 가려져 있으므로 사용자 입장에서 hover 가능해선 안 된다.
                card.TryActivateHoverFromCursor();
                return;
            }
        }

        private bool TryActivateHoverFromCursor()
        {
            if (!gameObject.activeInHierarchy) return false;
            // 뷰어/리스트 패널 카드(IsRuntimeUnplayable)는 수동 hover 트리거도 차단 — OnPointerEnter(648)와 동일 가드.
            // (Rest '카드 강화' 등에서 강화 카드 release 직후 sticky-release 코루틴이 끝날 때, 그 사이 열린
            //  CardListPanel의 카드가 커서 밑에 있으면 손패용 hover가 잘못 발동해 ypos가 hoverYOffset으로 튀던 버그.)
            if (IsRuntimeUnplayable && !IsShopMode) return false;
            if (isPlayed || isSticky || isLocked || isHovering) return false;
            if (IsInChooseContainer) return false;
            if (interactingCard != null && interactingCard != this) return false;
            // sticky 해제 애니메이션이 끝나기 전에는 같은 카드의 hover 활성화 금지.
            if (_stickyReleaseCoroutine != null) return false;

            isHovering = true;
            originalSiblingIndex = transform.GetSiblingIndex();
            transform.SetAsLastSibling();
            OnCardHoverEnter?.Invoke(this);
            AnimateHover(true);
            return true;
        }

        private void AnimateHover(bool enter)
        {
            if (_hoverAnimCoroutine != null) StopCoroutine(_hoverAnimCoroutine);
            if (!gameObject.activeInHierarchy)
            {
                // 비활성 상태에서는 코루틴 동작 불가 — 즉시 스냅.
                ApplyHoverTargets(enter);
                _hoverAnimCoroutine = null;
                return;
            }
            _hoverAnimCoroutine = StartCoroutine(HoverAnimCoroutine(enter));
        }

        private void ApplyHoverTargets(bool enter)
        {
            transform.localScale = enter ? originalScale * hoverScale : originalScale;
            transform.localPosition = enter
                ? new Vector3(originalPosition.x, hoverYOffset, originalPosition.z)
                : originalPosition;
            transform.localRotation = enter ? Quaternion.identity : originalRotation;
        }

        private System.Collections.IEnumerator HoverAnimCoroutine(bool enter)
        {
            Vector3 targetScale = enter ? originalScale * hoverScale : originalScale;
            Vector3 targetPos = enter
                ? new Vector3(originalPosition.x, hoverYOffset, originalPosition.z)
                : originalPosition;
            Quaternion targetRot = enter ? Quaternion.identity : originalRotation;

            Vector3 startScale = transform.localScale;
            Vector3 startPos = transform.localPosition;
            Quaternion startRot = transform.localRotation;

            float duration = Mathf.Max(0.0001f, hoverAnimDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                transform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }
            transform.localScale = targetScale;
            transform.localPosition = targetPos;
            transform.localRotation = targetRot;
            _hoverAnimCoroutine = null;
        }

        #endregion

        #region Audio

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        #endregion

        /// <summary>
        /// 손패 위치 설정 - 부드럽게 이동 (CombatController/HandManager에서 호출)
        /// </summary>
        public void SetHandPosition(Vector3 position, Quaternion rotation)
        {
            originalPosition = position;
            originalRotation = rotation;

            // sticky-release 코루틴이 transform 을 점유 중이면 reposition 을 시작하지 않는다 — 두 코루틴이
            // localPosition 을 동시에 쓰면 진동/끊김이 발생. sticky-release 는 originalPosition 을 매 프레임
            // 참조하므로 위에서 갱신된 새 좌표로 자연스럽게 수렴한다.
            if (!isSticky && !isHovering && !isAnimating && _stickyReleaseCoroutine == null)
            {
                if (repositionCoroutine != null) StopCoroutine(repositionCoroutine);
                repositionCoroutine = StartCoroutine(RepositionCoroutine(position, rotation));
            }
        }

        private System.Collections.IEnumerator RepositionCoroutine(Vector3 targetPos, Quaternion targetRot)
        {
            float duration = 0.15f;
            float elapsed = 0f;
            Vector3 startPos = transform.localPosition;
            Quaternion startRot = transform.localRotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                transform.localRotation = Quaternion.Lerp(startRot, targetRot, t);
                yield return null;
            }

            transform.localPosition = targetPos;
            transform.localRotation = targetRot;
        }

        /// <summary>
        /// 드로우 애니메이션 시작 (덱 위치 → 손패 위치)
        /// SetHandPosition으로 목표 위치를 먼저 설정한 뒤 호출해야 함
        /// </summary>
        public void StartDrawAnimation(Vector3 drawWorldPos, float duration)
        {
            if (repositionCoroutine != null) StopCoroutine(repositionCoroutine);
            if (_drawCoroutine != null) StopCoroutine(_drawCoroutine);

            // 덱 위치에서 시작
            if (transform.parent != null)
                transform.localPosition = transform.parent.InverseTransformPoint(drawWorldPos);
            else
                transform.position = drawWorldPos;

            transform.localScale = originalScale * 0.4f;
            canvasGroup.alpha = 0f;
            // blocksRaycasts를 끄지 않는다 — DrawInCoroutine이 중단되어도 raycast가 영구적으로 차단되지 않도록.
            // 카드가 alpha=0이라 시각적으로 안 보이는 동안 클릭은 가능하지만, 사용자가 보이지 않는 카드를 클릭할 확률은 낮음.

            _drawCoroutine = StartCoroutine(DrawInCoroutine(duration));
        }

        /// <summary>드로우 애니메이션 진행 중이면 즉시 최종 위치로 스냅하고 종료한다.
        /// 사용자가 드로우 중 카드를 클릭/호버할 때 첫 입력이 isAnimating 차단으로 무시되는 것을 방지.</summary>
        public void FinalizeDrawAnimation()
        {
            if (!_isInDrawAnimation) return;
            if (_drawCoroutine != null) { StopCoroutine(_drawCoroutine); _drawCoroutine = null; }
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
            transform.localScale = originalScale;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            isAnimating = false;
            _isInDrawAnimation = false;
        }

        private System.Collections.IEnumerator DrawInCoroutine(float duration)
        {
            isAnimating = true;
            _isInDrawAnimation = true;

            Vector3 startPos = transform.localPosition;
            Quaternion startRot = transform.localRotation;
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                // originalPosition/Rotation은 SetHandPosition이 갱신할 수 있으므로 동적으로 참조
                float arcOffset = Mathf.Sin(t * Mathf.PI) * 40f;
                Vector3 lerpPos = Vector3.Lerp(startPos, originalPosition, t);
                transform.localPosition = new Vector3(lerpPos.x, lerpPos.y + arcOffset, lerpPos.z);
                transform.localRotation = Quaternion.Lerp(startRot, originalRotation, t);
                transform.localScale = Vector3.Lerp(startScale, originalScale, t);
                canvasGroup.alpha = Mathf.Clamp01(t * 2f);

                yield return null;
            }

            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
            transform.localScale = originalScale;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            isAnimating = false;
            _isInDrawAnimation = false;
            _drawCoroutine = null;
        }

        /// <summary>
        /// 버림 더미 애니메이션 (손패 위치 → 버림더미 위치)
        /// yield return으로 대기 가능; 완료 후 호출자가 Destroy
        /// </summary>
        public System.Collections.IEnumerator AnimateDiscardOut(Vector3 discardWorldPos, float duration)
        {
            isAnimating = true;
            canvasGroup.blocksRaycasts = false;

            Vector3 startLocalPos = transform.localPosition;
            Vector3 discardLocalPos = transform.parent != null
                ? transform.parent.InverseTransformPoint(discardWorldPos)
                : discardWorldPos;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (this == null || gameObject == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.localPosition = Vector3.Lerp(startLocalPos, discardLocalPos, t);
                transform.localScale = Vector3.Lerp(originalScale, originalScale * 0.3f, t);
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }

            isAnimating = false;
        }

        /// <summary>
        /// 소멸배기용: 카드의 시각요소를 knifeSprite 단일 이미지로 즉시 교체합니다.
        /// 비행은 일으키지 않고 현재 위치/스케일은 유지 — 이후 FlyAsKnifeProjectile로 발사.
        /// </summary>
        public void TransformToKnifeProjectile(Sprite knifeSprite)
        {
            if (knifeSprite == null) return;

            isAnimating = true;
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

            // 텍스트/글로우는 항상 비활성화
            if (costText       != null) costText.enabled       = false;
            if (cardNameText   != null) cardNameText.enabled   = false;
            if (cardTypeText   != null) cardTypeText.enabled   = false;
            if (descriptionText!= null) descriptionText.enabled= false;
            if (rarityGlow     != null) rarityGlow.SetActive(false);
            if (upgradeIndicator != null) upgradeIndicator.SetActive(false);

            // 교체 대상 Image: cardArt 우선, 없으면 자식 Image들 중 하나로 자동 폴백
            // (Cardprefap이 cardArt 슬롯을 비워둔 경우 대응)
            Image target = cardArt;
            Image[] allImages = GetComponentsInChildren<Image>(true);
            if (target == null)
            {
                Image best = null;
                float bestArea = -1f;
                foreach (var img in allImages)
                {
                    if (img == null) continue;
                    var rt = img.rectTransform;
                    if (rt == null) continue;
                    float area = Mathf.Abs(rt.rect.width * rt.rect.height);
                    if (area > bestArea)
                    {
                        bestArea = area;
                        best = img;
                    }
                }
                target = best;
                if (target == null) return;
            }

            // target 외 모든 Image는 비활성화 (배경/프레임/코스트 배경 등이 위/아래로 덮이지 않게)
            foreach (var img in allImages)
            {
                if (img == null || img == target) continue;
                img.enabled = false;
            }

            target.enabled       = true;
            target.sprite        = knifeSprite;
            target.preserveAspect = true;
            target.color         = Color.white;
        }

        /// <summary>
        /// TransformToKnifeProjectile로 칼로 변신한 카드를 targetWorldPos까지 날립니다.
        /// yield 종료 후 호출자가 Destroy. 시작 시점 스케일에서 knifeScale로 점진 축소.
        /// </summary>
        public System.Collections.IEnumerator FlyAsKnifeProjectile(
            Vector3 targetWorldPos, float duration, float knifeScale = 1f)
        {
            Vector3 startLocalPos = transform.localPosition;
            Vector3 endLocalPos = transform.parent != null
                ? transform.parent.InverseTransformPoint(targetWorldPos)
                : targetWorldPos;

            // 날아가는 방향으로 카드 회전 (z축) — 카드의 윗변(+Y)이 진행 방향을 향하도록 -90 보정
            Vector3 dir = endLocalPos - startLocalPos;
            float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            Quaternion startRot = transform.localRotation;
            Quaternion endRot   = Quaternion.Euler(0f, 0f, baseAngle);
            Vector3 startScale  = transform.localScale;
            Vector3 endScale    = originalScale * knifeScale;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (this == null || gameObject == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = t * t; // ease-in (가속) 느낌
                transform.localPosition = Vector3.Lerp(startLocalPos, endLocalPos, ease);
                transform.localRotation = Quaternion.Slerp(startRot, endRot, Mathf.Min(1f, t * 2f));
                transform.localScale    = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }

            // 적 위치 도달 시 잠깐 깜빡 사라짐
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            isAnimating = false;
        }

        /// <summary>
        /// 변신과 비행을 한 번에 수행하는 호환 래퍼. 신규 코드는 Transform/Fly를 분리 사용 권장.
        /// </summary>
        public System.Collections.IEnumerator AnimateAsKnifeProjectile(
            Sprite knifeSprite, Vector3 targetWorldPos, float duration, float knifeScale = 1f)
        {
            TransformToKnifeProjectile(knifeSprite);
            yield return FlyAsKnifeProjectile(targetWorldPos, duration, knifeScale);
        }

        #region Shop Price

        public void SetShopPrice(int price, bool discounted = false)
        {
            if (goldText == null)
            {
                Debug.LogWarning($"[CardUI:{name}] SetShopPrice 호출됐으나 goldText 참조가 비어 있습니다. — 카드 프리팹의 goldText 필드 확인 필요");
                return;
            }
            // 자기 자신 + 부모 체인을 모두 활성화 (Initialize의 ClearShopPrice와 외부 비활성 부모 모두 대응)
            var t = goldText.transform;
            while (t != null && t != this.transform.parent)
            {
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                t = t.parent;
            }
            goldText.transform.SetAsLastSibling();
            goldText.text = $"{price}G";
            goldText.color = discounted ? new Color(1f, 0.35f, 0.35f) : Color.yellow;
            goldText.enabled = true;
            Debug.Log($"[CardUI:{name}] SetShopPrice {price}G discounted={discounted} active={goldText.gameObject.activeInHierarchy}");
        }

        public void ClearShopPrice()
        {
            if (goldText != null) goldText.gameObject.SetActive(false);
        }

        #endregion
    }
}
