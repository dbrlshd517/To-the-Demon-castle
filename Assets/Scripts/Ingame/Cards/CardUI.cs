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
        [SerializeField] private GameObject rarityGlow;
        [SerializeField] private GameObject upgradeIndicator;

        [Header("Rarity Colors")]
        [SerializeField] private Color commonRarityColor = Color.white;
        [SerializeField] private Color uncommonRarityColor = new Color(0.3f, 0.5f, 1f);
        [SerializeField] private Color rareRarityColor = new Color(1f, 0.8f, 0.2f);

        [Header("Animation Settings")]
        [SerializeField] private float hoverScale = 1.2f;
        [SerializeField] private float hoverYOffset = 50f;
        [SerializeField] private float animationSpeed = 10f;
        [SerializeField] private float dragCollapseScale = 1f;
        [Tooltip("카드가 보드 위로 올라가 축소될 때 추가로 내려가는 로컬 Y 오프셋")]
        [SerializeField] private float dragCollapseYOffset = 0f;
        [Tooltip("isSticky 상태에서 마우스가 이 화면 Y(px) 이하로 내려가면 자동 deselect. 0이면 비활성")]
        [SerializeField] private float stickyDeactivateScreenY = 0f;

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
        private int cachedEnergy;
        public int HandIndex { get; set; }
        public bool IsPlayable { get; set; } = true;
        public bool IsRuntimeUnplayable { get; set; }
        /// <summary>셀 선택이 필요한 카드(Enemy/Any/Ally). true이면 클릭 시 즉시 targeting 진입.</summary>
        public bool IsRangeCard { get; private set; }
        /// <summary>Skill(TypeDigit=3) 또는 Power(TypeDigit=4) 카드. BoardCollapseScreenY 충족 시 즉시 실행.</summary>
        public bool IsSelfPlayCard => CardData != null &&
            (CardData.CardTypeFromCode == CardType.Skill || CardData.CardTypeFromCode == CardType.Power);

        // State
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Vector3 originalScale;
        private int originalSiblingIndex;
        private bool isDragging;       // 마우스 버튼 누른 채 이동 중
        private bool isHovering;
        private bool isAnimating;
        private bool isSticky;        // 선택 상태 (클릭/드래그 공통) - 카드가 커서를 따라다님
        private bool isPlayed;        // 이미 실행되어 더 이상 처리 불필요
        private bool isLocked;        // 타겟팅 모드 중 이동 잠금 (range 카드 전용)
        private Vector3 stickyOffset; // 선택 시 커서와 카드 중심 간 오프셋
        private Vector2 mouseDownPos; // 마우스 누른 위치 (드래그/클릭 판별용)
        private float _collapsedEntryWorldY = float.MinValue; // 보드 경계 진입 순간 기록한 카드 world Y
        private float _collapseClampWorldY;                   // 실제 lerp 중인 카드 Y (world 단위)
        /// <summary>sticky 중 BoardCollapseScreenY 이상으로 올라간 적이 있는지 추적.
        /// stickyDeactivateScreenY는 이 플래그가 true일 때만 동작합니다.</summary>
        private bool _stickyCollapsedOnce;
        private bool _wasCollapsed;

        // 프리팹 원본 사이즈 (인스턴스화 직후 Awake에서 캡처)
        private Vector2 _prefabSizeDelta;

        // true일 때만 ignoreLayout 및 크기 강제를 적용 (전투 손패 전용)
        private bool _isHandCard = false;

        // 드래그/sticky 중인 카드가 있으면 다른 카드 hover 차단
        private static CardUI interactingCard;
        private Canvas canvas;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private AudioSource audioSource;
        private Coroutine hoverCoroutine;
        private Coroutine repositionCoroutine;

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
            if (!isSticky || isAnimating) return;

            // 오른쪽 마우스 버튼 클릭 시 선택 해제
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                Deselect();
                return;
            }

            Vector2 mousePos = Mouse.current.position.ReadValue();

            // isLocked(타겟팅 중)이면 커서 추적 안 함 (카드 고정)
            bool collapsed = mousePos.y >= BoardCollapseScreenY;

            // BoardCollapseScreenY 이상으로 한 번이라도 진입했으면 플래그 세팅
            if (collapsed) _stickyCollapsedOnce = true;

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

            // sticky 하단 임계값: BoardCollapseScreenY(보드 위 축소) 적용 이후에만 동작
            if (_stickyCollapsedOnce && stickyDeactivateScreenY > 0f && mousePos.y < stickyDeactivateScreenY)
            {
                Deselect();
                return;
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

            // Skill/Power: locked + collapsed 상태에서 클릭 → PlayCard (카드가 커서 아래 없을 수 있으므로 Update에서 직접 감지)
            if (isLocked && IsSelfPlayCard && collapsed && Mouse.current.leftButton.wasPressedThisFrame)
            {
                PlayCard();
                return;
            }

            // 축소 및 Y 이동: isLocked 여부와 무관하게 collapsed 조건만으로 동작
            if (collapsed)
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

            // 마우스가 보드 셀 높이 위로 올라가면 카드를 원래 크기로 부드럽게 축소
            {
                float targetScaleMult = collapsed ? dragCollapseScale : hoverScale;
                float curMult = originalScale.x > 0f
                    ? transform.localScale.x / originalScale.x
                    : hoverScale;
                float newMult = Mathf.Lerp(curMult, targetScaleMult, Time.deltaTime * 12f);
                transform.localScale = originalScale * newMult;
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

        private void RefreshText()
        {
            if (CardData == null) return;
            cardNameText.text    = ResolveLocalized($"card_name_{CardData.cardCode}", CardData.cardName);
            string rawDesc       = ResolveLocalized($"card_desc_{CardData.cardCode}", CardData.description);
            string desc          = CardData.GetFormattedDescription(rawDesc, cachedStrength, cachedDexterity, cachedEnergy);
            descriptionText.text = CardKeywordHelper.BuildDescriptionWithKeywords(desc, CardData.keywords);
            if (cardTypeText != null)
                cardTypeText.text = Loc.Get($"card_type_{CardData.CardTypeFromCode.ToString().ToLower()}");
        }

        private string ResolveLocalized(string key, string fallback)
        {
            string result = Loc.Get(key);
            return result == key ? (fallback ?? "") : result;
        }

        /// <summary>
        /// 카드 데이터로 UI 초기화
        /// </summary>
        public void Initialize(CardData data, int strength = 0, int dexterity = 0, int costReduction = 0, int currentEnergy = 0)
        {
            CardData = data;
            cachedStrength   = strength;
            cachedDexterity  = dexterity;
            cachedEnergy     = currentEnergy;

            if (data == null)
            {
                Debug.LogWarning("[CardUI] 카드 데이터가 null입니다.");
                return;
            }

            IsRangeCard = data.PrimaryTargeting == TargetType.Enemy ||
                          data.PrimaryTargeting == TargetType.Any   ||
                          data.PrimaryTargeting == TargetType.Ally;

            // 기본 정보
            cardNameText.text    = ResolveLocalized($"card_name_{data.cardCode}", data.cardName);
            string rawDesc       = data.GetFormattedDescription(
                ResolveLocalized($"card_desc_{data.cardCode}", data.description), strength, dexterity, currentEnergy);
            descriptionText.text = CardKeywordHelper.BuildDescriptionWithKeywords(rawDesc, data.keywords);
            costText.text = data.IsXCost ? "X" : Mathf.Max(0, data.energyCost - costReduction).ToString();
            if (cardTypeText != null)
                cardTypeText.text = Loc.Get($"card_type_{data.CardTypeFromCode.ToString().ToLower()}");

            // 키워드 툴팁 설정
            CardKeywordHelper.SetupTooltip(gameObject, data.keywords);

            // 카드 아트: cardArt 필드 우선, 없으면 Resources/CardArt/{cardCode} 로드
            if (cardArt != null)
            {
                Sprite art = data.cardArt;
                if (art == null && data.cardCode > 0)
                    art = Addressables.LoadAssetAsync<Sprite>($"Sprites/CardArt/{data.cardCode}").WaitForCompletion();
                if (art != null)
                    cardArt.sprite = art;
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
                // Skill/Power: blocksRaycasts 유지 → PointerDown 계속 수신 (보드 셀 클릭 불필요)
                // Attack/Move: false → 보드 셀이 raycast 받음
                if (!IsSelfPlayCard)
                    canvasGroup.blocksRaycasts = false;
                if (hoverCoroutine != null) { StopCoroutine(hoverCoroutine); hoverCoroutine = null; }
            }
            else
            {
                isSticky = false;
                canvasGroup.blocksRaycasts = true;
                if (interactingCard == this) interactingCard = null;
            }
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
            if (IsRuntimeUnplayable) return; // 뷰어 모드에서 hover 비활성화
            if (isAnimating || isSticky || isPlayed) return;
            if (interactingCard != null && interactingCard != this) return; // 다른 카드 상호작용 중

            isHovering = true;
            // originalPosition은 SetHandPosition이 올바른 손패 위치를 이미 설정함
            // 여기서 덮어쓰면 RepositionCoroutine 도중 hover 시 중간 좌표가 저장되는 버그 발생
            originalSiblingIndex = transform.GetSiblingIndex();

            transform.SetAsLastSibling();

            PlaySound(hoverSound);
            OnCardHoverEnter?.Invoke(this);

            if (hoverCoroutine != null) StopCoroutine(hoverCoroutine);
            if (repositionCoroutine != null) StopCoroutine(repositionCoroutine);
            hoverCoroutine = StartCoroutine(AnimateHover(true));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (IsRuntimeUnplayable) return; // 뷰어 모드에서 hover 비활성화
            if (isAnimating || isSticky || isPlayed || isLocked) return; // isSticky/isLocked: 선택/타겟팅 중 커서가 벗어나도 유지
            if (interactingCard != null && interactingCard != this) return; // 다른 카드 드래그/sticky 중 → preview 꺼짐 방지
            if (!isHovering) return; // hover 중이 아닌 카드의 Exit는 무시

            isHovering = false;
            transform.SetSiblingIndex(originalSiblingIndex);

            OnCardHoverExit?.Invoke(this);

            if (hoverCoroutine != null) StopCoroutine(hoverCoroutine);
            hoverCoroutine = StartCoroutine(AnimateHover(false));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (IsClickDisabled) return;
            OnCardViewClicked?.Invoke(this); // 뷰어 구독자용: 항상 발화

            if (isAnimating || isPlayed) return;

            // Skill/Power: 이미 sticky 상태에서 보드 위 클릭 → 즉시 실행
            if (isSticky && IsSelfPlayCard && Mouse.current.position.ReadValue().y >= BoardCollapseScreenY)
            {
                PlayCard();
                return;
            }

            if (isSticky) return;

            // 다른 카드가 선택 중이면 먼저 해제
            if (interactingCard != null && interactingCard != this)
                interactingCard.Deselect();

            if (isLocked || !IsPlayable || IsRuntimeUnplayable) return;

            // 선택 상태 진입 - isSticky와 isDragging 즉시 true (이동 감지 불필요)
            if (!isHovering)
            {
                originalPosition = transform.localPosition;
                originalSiblingIndex = transform.GetSiblingIndex();
                // hover 없이 바로 클릭한 경우: AnimateHover와 동일한 고정 Y 즉시 적용
                transform.localPosition = new Vector3(originalPosition.x, hoverYOffset, originalPosition.z);
                transform.localRotation = Quaternion.identity;
            }
            if (hoverCoroutine != null) { StopCoroutine(hoverCoroutine); hoverCoroutine = null; }
            if (repositionCoroutine != null) { StopCoroutine(repositionCoroutine); repositionCoroutine = null; }
            transform.localScale = originalScale * hoverScale; // 호버 애니메이션 중 선택 시 즉시 완성 크기로 스냅
            isSticky   = true;
            isDragging = true;
            isHovering = false;
            _stickyCollapsedOnce = false; // 새 선택 세션 시작 — collapse 진입 전까지 deactivate 비활성
            transform.localRotation = Quaternion.identity; // 선택 시 수직 정렬
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
            if (!isSticky || isPlayed) return;

            // ── Skill/Power 전용 경로 ─────────────────────────────────
            if (IsSelfPlayCard)
            {
                bool collapsed = Mouse.current.position.ReadValue().y >= BoardCollapseScreenY;
                if (collapsed)
                    PlayCard(); // 보드 위에서 손 떼면 즉시 실행
                else
                    isDragging = false; // 보드 아래: sticky 유지
                return;
            }

            // ── Attack/Move 기존 로직 ────────────────────────────────
            if (isLocked && !isDragging) return; // 타겟팅 모드 중 재클릭 무시

            if (!isDragging)
            {
                if (IsRangeCard) PlayCard();
                else             Deselect();
                return;
            }

            isDragging = false;

            if (IsRangeCard)
            {
                PlayCard();
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, results);
                foreach (var r in results)
                {
                    var cell = r.gameObject.GetComponent<CombatBoardCell>();
                    if (cell != null) { cell.TriggerClick(); break; }
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
            if (isLocked)
            {
                OnCardHoverExit?.Invoke(this);      // range preview 스프라이트 제거
                OnTargetingCancelled?.Invoke(this); // CombatController → CancelTargeting
                return;
            }
            isSticky   = false;
            isDragging = false;
            isHovering = false;
            _collapsedEntryWorldY = float.MinValue;
            _wasCollapsed = false;
            canvasGroup.blocksRaycasts = true;
            if (interactingCard == this) interactingCard = null;
            if (hoverCoroutine != null) { StopCoroutine(hoverCoroutine); hoverCoroutine = null; }
            OnCardHoverExit?.Invoke(this);
            ReturnToHand();
        }

        private void PlayCard()
        {
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

        public void ReturnToHand()
        {
            isAnimating = false; // StopAllCoroutines로 인한 isAnimating 고착 방지
            StopAllCoroutines();

            // 부모(handContainer 등)가 비활성이면 StartCoroutine이 실패하므로 즉시 위치 스냅으로 대체
            if (!gameObject.activeInHierarchy)
            {
                transform.localPosition = originalPosition;
                transform.localRotation = originalRotation;
                transform.localScale    = originalScale;
                return;
            }

            StartCoroutine(AnimateReturn());
        }

        private System.Collections.IEnumerator AnimateReturn()
        {
            isAnimating = true;
            transform.SetSiblingIndex(originalSiblingIndex);
            float duration = 0.2f;
            float elapsed = 0f;
            Vector3 startPos = transform.localPosition;
            Quaternion startRot = transform.localRotation;
            Vector3 targetPos = originalPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                transform.localRotation = Quaternion.Lerp(startRot, originalRotation, t);
                transform.localScale = Vector3.Lerp(transform.localScale, originalScale, t);

                yield return null;
            }

            transform.localPosition = targetPos;
            transform.localRotation = originalRotation;
            transform.localScale = originalScale;
            isAnimating = false;

            // 카드가 마우스 아래로 돌아왔을 때 OnPointerEnter 수동 재발화
            // (Unity EventSystem은 오브젝트가 마우스 밑으로 이동해도 Enter를 자동 재발화하지 않음)
            if (isPlayed || isSticky) yield break;
            if (interactingCard != null && interactingCard != this) yield break;

            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    rectTransform, Mouse.current.position.ReadValue(), cam))
            {
                isHovering = true;
                originalPosition = transform.localPosition;
                originalSiblingIndex = transform.GetSiblingIndex();
                transform.SetAsLastSibling();
                OnCardHoverEnter?.Invoke(this);
                if (hoverCoroutine != null) StopCoroutine(hoverCoroutine);
                hoverCoroutine = StartCoroutine(AnimateHover(true));
            }
        }

        private System.Collections.IEnumerator AnimateHover(bool enter)
        {
            Vector3 targetScale = enter ? originalScale * hoverScale : originalScale;
            // 가장자리 카드는 originalPosition.y가 낮아서 고정 offset만 더하면 center보다 낮게 멈춤.
            // center(y=0) 기준 hover 높이보다 낮아지지 않도록 Mathf.Max로 보정.
            Vector3 targetPos = enter ?
                new Vector3(originalPosition.x, hoverYOffset, originalPosition.z) :
                originalPosition;
            Quaternion targetRot = enter ? Quaternion.identity : originalRotation;

            transform.localScale = targetScale;
            transform.localPosition = targetPos;
            transform.localRotation = targetRot;
            yield break;
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

            if (!isSticky && !isHovering && !isAnimating)
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

            // 덱 위치에서 시작
            if (transform.parent != null)
                transform.localPosition = transform.parent.InverseTransformPoint(drawWorldPos);
            else
                transform.position = drawWorldPos;

            transform.localScale = originalScale * 0.4f;
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;

            StartCoroutine(DrawInCoroutine(duration));
        }

        private System.Collections.IEnumerator DrawInCoroutine(float duration)
        {
            isAnimating = true;

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
    }
}
