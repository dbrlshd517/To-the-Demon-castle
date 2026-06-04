using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using DeckRoguelike.Core;
using DeckRoguelike.Combat;
using DeckRoguelike.Cards;
using DeckRoguelike.Relic;
using DeckRoguelike.Item;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// InGame 씬의 모든 UI 상태를 관리하는 통합 컨트롤러
    /// 
    /// [구조]
    /// 맵, 전투, 상점, 휴식, 이벤트, 보상 UI를 모두 관리
    /// 씬 전환 없이 UI 패널만 전환
    /// 
    /// [게임 상태별 표시되는 UI]
    /// - Map: MapOverlay (+ TopBar, PlayerInfo)
    /// - Combat: CombatUI (+ TopBar, PlayerInfo, HandArea)
    /// - Shop: ShopPanel
    /// - Rest: RestPanel  
    /// - Event: EventPanel
    /// - Reward: RewardPanel
    /// </summary>
    public class InGameUIController : MonoBehaviour
    {
        public static InGameUIController Instance { get; private set; }

        [Header("=== 항상 표시되는 UI ===")]

        [Header("=== 상태별 UI 패널 ===")]
        [SerializeField] private GameObject mapOverlay;
        [SerializeField] private GameObject combatUI;
        [SerializeField] private GameObject eventPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject deckViewerPanel;
        [SerializeField] private GameObject drawPanel;
        [SerializeField] private GameObject discardPanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject victoryPanel;
        [FormerlySerializedAs("cardInfoPanel")]
        [SerializeField] private CardInfoController cardInfoPanel;

        [Header("=== InGame Info Panel ===")]
        [Tooltip("플레이어 상태별 이름/설명을 표시하는 패널. CombatState에서는 오른쪽으로 슬라이드되어 카메라 밖으로 숨겨진다.")]
        [SerializeField] private RectTransform inGameInfoPanel;
        [SerializeField] private UnityEngine.UI.Image inGameNameImage;
        [SerializeField] private TextMeshProUGUI inGameNameText;
        [SerializeField] private UnityEngine.UI.Image inGameDescriptionImage;
        [SerializeField] private TextMeshProUGUI inGameDescriptionText;
        [Tooltip("패널을 숨길 때 active anchoredPosition에 더할 오프셋 (오른쪽=+X). 카메라 밖으로 나가도록 충분히 크게 설정.")]
        [SerializeField] private Vector2 inGameInfoHiddenOffset = new Vector2(2400f, 0f);
        [SerializeField] private float inGameInfoSlideDuration = 0.3f;

        [System.Serializable]
        private class StateInfo
        {
            public string nameText;
            [TextArea] public string descriptionText;
            public Sprite nameSprite;
            public Sprite descriptionSprite;
        }

        [Header("=== InGame Info — 상태별 기본값 ===")]
        [Tooltip("EventState는 이벤트 카드의 cardName/description을 직접 사용한다. 다른 상태는 아래 값으로 채워진다.")]
        [SerializeField] private StateInfo mapStateInfo;
        [SerializeField] private StateInfo shopStateInfo;
        [SerializeField] private StateInfo restStateInfo;
        [SerializeField] private StateInfo treasureStateInfo;
        [SerializeField] private StateInfo rewardStateInfo;
        [SerializeField] private StateInfo gameOverStateInfo;
        [SerializeField] private StateInfo victoryStateInfo;

        [Header("=== Top Bar UI ===")]
        [SerializeField] private UnityEngine.UI.Image levelIcon;
        [SerializeField] private UnityEngine.UI.Image actIcon;
        [SerializeField] private TextMeshProUGUI actText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private Button mapButton;
        [SerializeField] private Button deckButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button backButton;

        [Header("=== 서브 컨트롤러 ===")]
        [SerializeField] private BoardController boardController;
        [SerializeField] private SettingsMenuController settingsMenuController;
        [SerializeField] private CardListController subCardListController;
        private System.Action _restCardListCancelCallback;

        [Header("=== Audio ===")]
        [SerializeField] private AudioClip buttonClickSound;
        [SerializeField] private AudioClip openMapSound;
        [SerializeField] private AudioClip closeMapSound;

        [Header("=== Relic UI ===")]
        [SerializeField] private Transform relicIconContainer;
        [SerializeField] private GameObject relicIconPrefab;
        [SerializeField] private RelicInfoController relicInfoController;
        [Tooltip("유물 페이지 전환 네비게이션 프리팹 (Button 포함). 페이지 초과 시 Col_1 6행에 생성됩니다.")]
        [SerializeField] private GameObject relicNavPrefab;

        [Header("=== Player Effect UI (TopBar) ===")]
        [Tooltip("플레이어 버프/상태이상/파워카드 효과 아이콘을 나열할 컨테이너 (TopBar 내 HorizontalLayoutGroup 권장)")]
        [SerializeField] private Transform playerEffectContainer;
        [Tooltip("EffectIconEntry 컴포넌트를 가진 아이콘 프리팹")]
        [SerializeField] private GameObject playerEffectIconPrefab;
        [Tooltip("이펙트 페이지 전환 네비게이션 프리팹 (Button 포함). 페이지 초과 시 Col_1 6행에 생성됩니다.")]
        [SerializeField] private GameObject effectNavPrefab;

        [Header("=== Item UI ===")]
        [Tooltip("아이템 슬롯 컨테이너 (HorizontalLayoutGroup — 왼쪽→오른쪽 배치)")]
        [SerializeField] private Transform itemSlotContainer;
        [SerializeField] private GameObject itemSlotPrefab;
        [Tooltip("아이템 클릭 시 표시할 Use/Discard 확인 패널 프리팹")]
        [SerializeField] private GameObject confirmUseItemPrefab;
        [Tooltip("아이템 타겟팅 중 CombatBoardCell 외 UI 입력 차단용 투명 패널 (Canvas 자식, CombatBoard보다 낮은 sibling 순서)")]
        [SerializeField] private GameObject itemTargetingBlocker;

        // ── 아이템 레이아웃 (113 유물: 포션 슬롯 +1열) ──
        private const int BaseItemCols = 5;
        private const int ItemsPerCol  = 2;
        private int ItemCols => BaseItemCols + (GameManager.Instance != null && GameManager.Instance.HasRelic(113) ? 1 : 0);
        private ItemSlotUI[,] _itemSlots = new ItemSlotUI[BaseItemCols, ItemsPerCol];

        private ConfirmUseItemPanel activeConfirmPanel;
        private ItemSlotUI activeConfirmSlot;


        private AudioSource audioSource;
        private bool isMapOpen = false;
        private int combatStartHP;
        private int pendingEventCode; // 이벤트 상태 진입 시 표시할 eventCode

        // ── InGame Info Panel — 슬라이드 위치 캐시 ─────────────────
        private Vector2 _inGameInfoActivePos;
        private bool _inGameInfoActivePosCaptured;
        private Coroutine _inGameInfoSlideCoroutine;

        // ── 패널 스택 ──────────────────────────────────────────────
        private struct PanelEntry { public string id; public System.Action close; }
        private readonly LinkedList<PanelEntry> panelStack = new LinkedList<PanelEntry>();
        private const int MaxPanelStack = 3;

        // Events

        // Properties
        public bool IsMapOpen => isMapOpen;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        private void Start()
        {
            InitializeUI();
            SetupButtonListeners();
            SubscribeToEvents();

            // 시작 유물이 추가되기 전에 첫 페이지를 미리 생성
            if (relicIconContainer != null)
            {
                EnsureRelicLayout();
                if (_relicPageObjs.Count == 0) CreateRelicPage();
            }

            RefreshRelicIcons();

            // 게임 시작 시 맵 표시
            TransitionToMap();

            // Continue 진입 시 마지막 노드가 Shop/Rest였다면 그 상태로 자동 복귀
            // (맵이 초기화된 다음 프레임에 실행해야 셀/플레이어 위치가 잡힌 상태가 된다)
            StartCoroutine(InvokeNextFrame(RestoreNodeStateIfNeeded));
        }

        private void RestoreNodeStateIfNeeded()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            switch (gm.CurrentNodeType)
            {
                case NodeType.Shop:
                    Debug.Log("[InGameUI] Continue 복원 — 상점 재진입");
                    CloseMap();
                    if (combatUI != null) combatUI.SetActive(true);
                    if (cardInfoPanel != null) cardInfoPanel.gameObject.SetActive(false);
                    ApplyShopStateInfo();
                    boardController?.SpawnMerchant();
                    break;
                case NodeType.Rest:
                    Debug.Log("[InGameUI] Continue 복원 — 휴식지 재진입");
                    CloseMap();
                    if (combatUI != null) combatUI.SetActive(true);
                    if (cardInfoPanel != null) cardInfoPanel.gameObject.SetActive(false);
                    ApplyRestStateInfo();
                    boardController?.SpawnRestArea();
                    break;
                case NodeType.Combat:
                case NodeType.DangerCombat:
                case NodeType.Boss:
                    Debug.Log($"[InGameUI] Continue 복원 — 전투 재시작 ({gm.CurrentNodeType})");
                    StartCombat();
                    break;
                case NodeType.Event:
                    Debug.Log($"[InGameUI] Continue 복원 — 이벤트 재표시 (code={gm.CurrentEventCode})");
                    pendingEventCode = gm.CurrentEventCode;
                    CloseMap();
                    if (pendingEventCode > 0) ApplyEventCardInfo(pendingEventCode);
                    // entry code의 둘째자리로 sub-type 판별. 1=Choice, 2=Shop.
                    int restoreSecondDigit = pendingEventCode > 0 ? (pendingEventCode / 1000) % 10 : 0;
                    if (restoreSecondDigit == 2)
                    {
                        if (combatUI != null) combatUI.SetActive(true);
                        if (cardInfoPanel != null) cardInfoPanel.gameObject.SetActive(false);
                        boardController?.SpawnShopEvent(pendingEventCode);
                    }
                    else
                    {
                        if (eventPanel != null) eventPanel.SetActive(true);
                        if (pendingEventCode > 0)
                        {
                            InvokeEventEntry(pendingEventCode);
                            var moveCard = DeckRoguelike.UI.BoardController.CreateMapMoveCard();
                            if (moveCard != null) boardController?.AddCardToHandFree(moveCard);
                        }
                    }
                    break;
                case NodeType.Treasure:
                    Debug.Log("[InGameUI] Continue 복원 — 보물방 재진입");
                    CloseMap();
                    if (combatUI != null) combatUI.SetActive(true);
                    if (cardInfoPanel != null) cardInfoPanel.gameObject.SetActive(false);
                    ApplyTreasureStateInfo();
                    boardController?.SpawnTreasureArea();
                    break;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            HandleInput();
            UpdateMapButtonInteractable();
        }

        // 매 프레임 boardController의 combatState를 검사해 mapButton의 interactable을 갱신한다.
        // mapButton은 전투 중(PlayerTurn / EnemyTurn)에만 hover로 맵 미리보기가 발화되므로
        // 그 외 상태(MapState/Shop/Rest/Treasure/Victory/Defeat 등)에선 클릭 불가로 표시한다.
        private CombatState _mapButtonLastState = (CombatState)(-1);
        private void UpdateMapButtonInteractable()
        {
            if (mapButton == null || boardController == null) return;
            var st = boardController.CurrentCombatState;
            if (st == _mapButtonLastState) return;
            _mapButtonLastState = st;
            mapButton.interactable = (st == CombatState.PlayerTurn || st == CombatState.EnemyTurn);
        }

        #region Initialization

        private void InitializeUI()
        {
            HideAllPanels();
            UpdatePlayerInfo();

            // playerEffectContainer 레이아웃은 EnsureEffectLayout()에서 설정됨

            // itemSlotContainer 레이아웃 설정
            BuildItemSlotGrid();

            // 씬 로드 시 이미 보유한 아이템 복원
            if (GameManager.Instance != null)
                foreach (var item in GameManager.Instance.Items)
                    AddItemSlot(item);
        }

        /// <summary>아이템 슬롯 그리드 생성 — 113 유물 보유 여부에 따라 열 수(5 또는 6)가 결정됩니다.</summary>
        private void BuildItemSlotGrid()
        {
            if (itemSlotContainer == null) return;

            // 113 유물 등에 따라 열 수가 달라질 수 있으므로 배열 재할당
            _itemSlots = new ItemSlotUI[ItemCols, ItemsPerCol];

            // 기존 자식 슬롯들 제거 (Rebuild 시)
            for (int i = itemSlotContainer.childCount - 1; i >= 0; i--)
            {
                var child = itemSlotContainer.GetChild(i);
                if (child != null) Destroy(child.gameObject);
            }

            var oldVlg = itemSlotContainer.GetComponent<VerticalLayoutGroup>();
            if (oldVlg != null) Destroy(oldVlg);
            var oldHlg = itemSlotContainer.GetComponent<HorizontalLayoutGroup>();
            if (oldHlg != null) Destroy(oldHlg);

            var hlg = itemSlotContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment         = TextAnchor.UpperLeft;
            hlg.spacing                = 4f;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = false;
            hlg.childControlHeight     = false;

            for (int c = 0; c < ItemCols; c++)
            {
                var colGO = new GameObject($"ItemCol_{c}", typeof(RectTransform));
                colGO.transform.SetParent(itemSlotContainer, false);

                float iconW = 0f;
                if (itemSlotPrefab != null)
                {
                    var prefabRect = itemSlotPrefab.GetComponent<RectTransform>();
                    if (prefabRect != null)
                        iconW = prefabRect.sizeDelta.x > 0f ? prefabRect.sizeDelta.x : prefabRect.rect.width;
                }
                var colRect = colGO.GetComponent<RectTransform>();
                if (iconW > 0f)
                    colRect.sizeDelta = new Vector2(iconW, colRect.sizeDelta.y);

                var colVlg = colGO.AddComponent<VerticalLayoutGroup>();
                colVlg.childAlignment         = TextAnchor.UpperCenter;
                colVlg.spacing                = 4f;
                colVlg.childForceExpandWidth  = false;
                colVlg.childForceExpandHeight = false;
                colVlg.childControlWidth      = false;
                colVlg.childControlHeight     = false;

                for (int r = 0; r < ItemsPerCol; r++)
                {
                    var slotObj = Instantiate(itemSlotPrefab, colGO.transform);
                    slotObj.name = $"ItemSlot_{c}_{r}";
                    slotObj.SetActive(true);

                    var slotUI = slotObj.GetComponent<ItemSlotUI>();
                    if (slotUI == null) slotUI = slotObj.AddComponent<ItemSlotUI>();
                    slotUI.SetEmpty();
                    _itemSlots[c, r] = slotUI;
                }
            }
        }

        /// <summary>113 유물 획득 시 호출 — 슬롯 그리드를 재구성하고 기존 아이템을 복원합니다.</summary>
        public void RebuildItemSlots()
        {
            BuildItemSlotGrid();
            if (GameManager.Instance != null)
                foreach (var item in GameManager.Instance.Items)
                    AddItemSlot(item);
        }

        private void SetupButtonListeners()
        {
            deckButton?.onClick.AddListener(ToggleDeckViewer);
            settingsButton?.onClick.AddListener(ToggleSettings);
            backButton?.onClick.AddListener(OnBackClicked);
            // nav prefab은 필요 시 Instantiate하므로 여기선 비활성화만
            if (relicNavPrefab != null) relicNavPrefab.SetActive(false);
            if (effectNavPrefab != null) effectNavPrefab.SetActive(false);

            backButton?.gameObject.SetActive(false);

            // mapButton — 클릭 없음, hover 시 맵 미리보기. 전투 중에만 interactable이 되도록 Update에서 갱신.
            // 시작 시점엔 MapState부터 시작하므로 비활성으로 셋업.
            if (mapButton != null) mapButton.interactable = false;
            SetupMapButtonHover();

            // 모든 TopBar 버튼 — hover 시 아이템 타겟팅 취소
            AddItemCancelOnHover(mapButton);
            AddItemCancelOnHover(deckButton);
            AddItemCancelOnHover(settingsButton);
            AddItemCancelOnHover(backButton);
        }

        private void SetupMapButtonHover()
        {
            if (mapButton == null) return;
            var trigger = mapButton.GetComponent<EventTrigger>()
                       ?? mapButton.gameObject.AddComponent<EventTrigger>();

            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener(_ =>
            {
                if (boardController == null) return;
                var st = boardController.CurrentCombatState;
                // combat 노드(전투 중)에서만 hover 미리보기 활성화. Shop/Rest/Map/Victory 등에선 비활성.
                if (st != CombatState.PlayerTurn && st != CombatState.EnemyTurn) return;
                boardController.ShowMapPreview();
            });
            trigger.triggers.Add(enterEntry);

            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener(_ =>
            {
                boardController?.HideMapPreview();
            });
            trigger.triggers.Add(exitEntry);
        }

        /// <summary>버튼에 PointerEnter 이벤트를 추가합니다. hover 시 아이템 타겟팅이 활성 중이면 취소합니다.</summary>
        private void AddItemCancelOnHover(Button btn)
        {
            if (btn == null) return;
            var trigger = btn.GetComponent<EventTrigger>()
                       ?? btn.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ =>
            {
                if (boardController != null && boardController.IsItemTargetingActive)
                    boardController.CancelItemTargeting();
            });
            trigger.triggers.Add(entry);
        }

        private void SubscribeToEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGoldChanged += UpdateGoldDisplay;
                GameManager.Instance.OnFloorChanged += UpdateFloorDisplay;
                GameManager.Instance.OnHPChanged += UpdateHPDisplay;
                GameManager.Instance.OnRelicAdded += AddRelicIcon;
                GameManager.Instance.OnRelicReplaced += ReplaceRelicIcon;
                GameManager.Instance.OnRelicRemoved += RemoveRelicIcon;
                GameManager.Instance.OnItemAdded   += AddItemSlot;
                GameManager.Instance.OnItemRemoved += OnItemRemoved;
            }

            if (boardController != null)
            {
                boardController.OnNodeSelected += HandleNodeSelected;
                boardController.OnCombatEnded += HandleBoardEnded;
            }

        }

        private void UnsubscribeFromEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGoldChanged -= UpdateGoldDisplay;
                GameManager.Instance.OnFloorChanged -= UpdateFloorDisplay;
                GameManager.Instance.OnHPChanged -= UpdateHPDisplay;
                GameManager.Instance.OnRelicAdded -= AddRelicIcon;
                GameManager.Instance.OnRelicReplaced -= ReplaceRelicIcon;
                GameManager.Instance.OnRelicRemoved -= RemoveRelicIcon;
                GameManager.Instance.OnItemAdded   -= AddItemSlot;
                GameManager.Instance.OnItemRemoved -= OnItemRemoved;
            }

            if (boardController != null)
            {
                boardController.OnNodeSelected -= HandleNodeSelected;
                boardController.OnCombatEnded -= HandleBoardEnded;
            }

        }

        #endregion

        #region Input Handling

        private void HandleInput()
        {
            // ESC - 맵 닫기
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (isMapOpen && boardController != null && boardController.CurrentCombatState != CombatState.MapState)
                {
                    CloseMap();
                }
            }

            // D - 덱 뷰어
            if (Keyboard.current.dKey.wasPressedThisFrame)
            {
                ToggleDeckViewer();
            }
        }

        #endregion

        #region State Management

        public void TransitionToMap()
        {
            HideAllPanels();
            CloseMap();
            ShowMapState();
            UpdateBackButton();
        }

        public void TransitionToCombat()
        {
            HideAllPanels();
            CloseMap();
            ShowCombatState();
            UpdateBackButton();
        }

        public void TransitionToGameOver()
        {
            HideAllPanels();
            CloseMap();
            ShowGameOverState();
            UpdateBackButton();
        }

        public void TransitionToVictory()
        {
            HideAllPanels();
            CloseMap();
            ShowVictoryState();
            UpdateBackButton();
        }

        public void TransitionToReward()
        {
            HideAllPanels();
            CloseMap();
            ShowRewardState();
            UpdateBackButton();
        }

        private void HideAllPanels()
        {
            panelStack.Clear();
            isMapOpen = false;
            if (mapOverlay != null) mapOverlay.SetActive(false);
            if (combatUI != null) combatUI.SetActive(false);
            if (eventPanel != null) eventPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (deckViewerPanel != null) deckViewerPanel.SetActive(false);
            if (drawPanel != null) drawPanel.SetActive(false);
            if (discardPanel != null) discardPanel.SetActive(false);
            if (cardInfoPanel != null) cardInfoPanel.gameObject.SetActive(false);
            if (subCardListController != null) subCardListController.gameObject.SetActive(false);
        }

        private void ShowMapState()
        {
            Debug.Log($"[InGameUI] ShowMapState — combatUI={combatUI != null}, boardController={boardController != null}");
            if (combatUI != null) combatUI.SetActive(true);
            isMapOpen = true;
            boardController?.ShowMap();
            RemoveFromStack("Map");
            panelStack.AddLast(new PanelEntry { id = "Map", close = CloseMap });

            // 맵 상태 — 전투 버튼은 카메라 밖으로 숨기고 InGameInfoPanel은 맵 정보로 표시
            // 이름/설명은 Localization CSV(start_name, start_desc)에서 직접 표시.
            boardController?.SetCombatActionButtonsVisible(false);
            ApplyInGameInfoForState(mapStateInfo, "start");
            ShowInGameInfoPanel();
        }

        private void ShowCombatState()
        {
            if (combatUI != null) combatUI.SetActive(true);
            if (GameManager.Instance != null) GameManager.Instance.IsInCombat = true;

            // 전투 진입 — InGameInfoPanel은 오른쪽으로 슬라이드되어 카메라 밖으로 사라진다
            HideInGameInfoPanel();
        }

        private void ShowRewardState()
        {
            backButton?.gameObject.SetActive(false);
            ApplyInGameInfoForState(rewardStateInfo);
            ShowInGameInfoPanel();
            boardController?.SetCombatActionButtonsVisible(false);
        }

        private void ShowGameOverState()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            GameManager.Instance?.EnterGameOver();
            // 이름/설명은 Localization CSV(defeat_name, defeat_desc)에서 직접 표시. {0} = 사망 원인 적/인카운터 이름.
            string killerName = boardController != null ? boardController.LastCombatEncounterName : null;
            ApplyInGameInfoForState(gameOverStateInfo, "defeat", killerName);
            ShowInGameInfoPanel();
            boardController?.SetCombatActionButtonsVisible(false);
        }

        private void ShowVictoryState()
        {
            if (victoryPanel != null) victoryPanel.SetActive(true);
            GameManager.Instance?.EnterVictory();
            // 이름/설명은 Localization CSV(victory_name, victory_desc)에서 직접 표시. {0} = 처치한 인카운터(보스) 이름.
            string defeatedName = boardController != null ? boardController.LastCombatEncounterName : null;
            ApplyInGameInfoForState(victoryStateInfo, "victory", defeatedName);
            ShowInGameInfoPanel();
            boardController?.SetCombatActionButtonsVisible(false);
        }

        #region InGame Info Panel

        /// <summary>InGameInfoPanel에 이름/설명/이미지를 설정합니다. 스프라이트는 null이면 변경 안 함.</summary>
        public void SetInGameInfo(string name, string description, Sprite nameSprite = null, Sprite descSprite = null)
        {
            if (inGameNameText != null) inGameNameText.text = name ?? string.Empty;
            if (inGameDescriptionText != null) inGameDescriptionText.text = description ?? string.Empty;
            if (nameSprite != null && inGameNameImage != null) inGameNameImage.sprite = nameSprite;
            if (descSprite != null && inGameDescriptionImage != null) inGameDescriptionImage.sprite = descSprite;
        }

        private void ApplyInGameInfoForState(StateInfo info, string locPrefix = null, string formatArg0 = null)
        {
            if (info == null && string.IsNullOrEmpty(locPrefix)) return;
            string name = info?.nameText;
            string desc = info?.descriptionText;
            if (!string.IsNullOrEmpty(locPrefix))
            {
                name = LocalizationManager.Get($"{locPrefix}_name");
                desc = LocalizationManager.Get($"{locPrefix}_desc");
                if (!string.IsNullOrEmpty(desc) && desc.Contains("{0}"))
                {
                    // null/empty arg는 빈 문자열로 치환해 placeholder가 그대로 노출되지 않게 한다.
                    try { desc = string.Format(desc, formatArg0 ?? string.Empty); }
                    catch { /* 포맷 실패 시 원문 유지 */ }
                }
            }
            SetInGameInfo(name, desc, info?.nameSprite, info?.descriptionSprite);
        }

        /// <summary>MapState로 복귀했을 때(또는 카드 hover 종료 시) 다시 맵 상태 정보를 표시한다.
        /// 보상 카드 hover 종료 시 카드별 name/desc → 맵 상태 정보로 되돌리는 경로에 사용.</summary>
        public void ApplyMapStateInfo() { ApplyInGameInfoForState(mapStateInfo, "start"); ShowInGameInfoPanel(); }

        /// <summary>Shop 상태 진입 시 호출 (BoardController.SpawnMerchant에서 사용). 이름/설명은 Localization CSV(shop_name, shop_desc)에서 직접 표시.</summary>
        public void ApplyShopStateInfo() { ApplyInGameInfoForState(shopStateInfo, "shop"); ShowInGameInfoPanel(); }

        /// <summary>Rest 상태 진입 시 호출. 이름/설명은 Localization CSV(rest_name, rest_desc)에서 직접 표시.</summary>
        public void ApplyRestStateInfo() { ApplyInGameInfoForState(restStateInfo, "rest"); ShowInGameInfoPanel(); }

        /// <summary>Treasure 상태 진입 시 호출. 이름/설명은 Localization CSV(treasure_name, treasure_desc)에서 직접 표시.</summary>
        public void ApplyTreasureStateInfo() { ApplyInGameInfoForState(treasureStateInfo, "treasure"); ShowInGameInfoPanel(); }

        public void ShowInGameInfoPanel()
        {
            if (inGameInfoPanel == null) return;
            EnsureInGameInfoActivePos();
            StartInGameInfoSlide(_inGameInfoActivePos);
        }

        public void HideInGameInfoPanel()
        {
            if (inGameInfoPanel == null) return;
            EnsureInGameInfoActivePos();
            StartInGameInfoSlide(_inGameInfoActivePos + inGameInfoHiddenOffset);
        }

        private void EnsureInGameInfoActivePos()
        {
            if (_inGameInfoActivePosCaptured || inGameInfoPanel == null) return;
            _inGameInfoActivePos = inGameInfoPanel.anchoredPosition;
            _inGameInfoActivePosCaptured = true;
        }

        private void StartInGameInfoSlide(Vector2 target)
        {
            if (inGameInfoPanel == null) return;
            if (_inGameInfoSlideCoroutine != null) StopCoroutine(_inGameInfoSlideCoroutine);
            _inGameInfoSlideCoroutine = StartCoroutine(SlideAnchored(inGameInfoPanel, target, inGameInfoSlideDuration));
        }

        private IEnumerator SlideAnchored(RectTransform rt, Vector2 target, float duration)
        {
            if (rt == null) yield break;
            Vector2 start = rt.anchoredPosition;
            if (duration <= 0f) { rt.anchoredPosition = target; yield break; }
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                // ease-out
                float ek = 1f - (1f - k) * (1f - k);
                rt.anchoredPosition = Vector2.Lerp(start, target, ek);
                yield return null;
            }
            rt.anchoredPosition = target;
        }

        #endregion

        #endregion

        #region Map Toggle

        public void ToggleMap()
        {
            if (IsSettingsOpen)
            {
                // 세팅 닫기, 맵은 열려있으면 유지 / 닫혀있으면 열기
                CloseSettings();
                if (!isMapOpen) OpenMap();
                return;
            }

            if (isMapOpen) CloseMap();
            else OpenMap();
        }

        public void OpenMap()
        {
            CloseSettings();
            if (deckViewerPanel != null) deckViewerPanel.SetActive(false);
            RemoveFromStack("DeckViewer");

            PlaySound(openMapSound);
            if (combatUI != null) combatUI.SetActive(true);
            isMapOpen = true;
            boardController?.ShowMap();

            RemoveFromStack("Map");
            panelStack.AddLast(new PanelEntry { id = "Map", close = CloseMap });
            UpdateBackButton();
        }

        public void CloseMap()
        {
            PlaySound(closeMapSound);
            if (mapOverlay != null) mapOverlay.SetActive(false);
            isMapOpen = false;
            RemoveFromStack("Map");
            UpdateBackButton();
        }

        #endregion

        #region Node Selection Handler

        private void HandleNodeSelected(NodeType nodeType, int floor)
        {
            Debug.Log($"[InGameUI] 노드 선택: {nodeType} (층: {floor})");

            GameManager.Instance?.SetFloor(floor + 1);
            UpdateFloorDisplay(floor + 1);

            GameManager.Instance?.SetCurrentNodeType(nodeType);

            switch (nodeType)
            {
                case NodeType.Combat:
                    StartCombat();
                    break;

                case NodeType.DangerCombat:
                    StartCombat();
                    break;

                case NodeType.Boss:
                    StartCombat();
                    break;

                case NodeType.Shop:
                    CloseMap();
                    if (combatUI != null) combatUI.SetActive(true);
                    if (cardInfoPanel != null) cardInfoPanel.gameObject.SetActive(false);
                    ApplyShopStateInfo();
                    // 같은 프레임에 InitializeBoard가 클릭한 셀을 DestroyImmediate하면
                    // 남은 pointer up 이벤트가 파괴된 RectTransform을 참조해 MissingReferenceException 발생.
                    // 다음 프레임으로 지연.
                    StartCoroutine(InvokeNextFrame(() => boardController?.SpawnMerchant()));
                    break;

                case NodeType.Rest:
                    GameManager.Instance?.MarkLastNodeRest();
                    CloseMap();
                    if (combatUI != null) combatUI.SetActive(true);
                    if (cardInfoPanel != null) cardInfoPanel.gameObject.SetActive(false);
                    ApplyRestStateInfo();
                    StartCoroutine(InvokeNextFrame(() => boardController?.SpawnRestArea()));
                    break;

                case NodeType.Treasure:
                    CloseMap();
                    if (combatUI != null) combatUI.SetActive(true);
                    if (cardInfoPanel != null) cardInfoPanel.gameObject.SetActive(false);
                    ApplyTreasureStateInfo();
                    StartCoroutine(InvokeNextFrame(() => boardController?.SpawnTreasureArea()));
                    break;

                case NodeType.Event:
                    GameManager.Instance?.MarkLastNodeMystery();
                    // 105 미지의 부적 — 미지 진입 시 HP +7
                    if (GameManager.Instance != null && GameManager.Instance.HasRelic(105))
                    {
                        GameManager.Instance.Heal(7);
                        Debug.Log("[InGameUIController] 105 유물 — 미지 진입 HP +7");
                    }

                    int act = GameManager.Instance?.CurrentAct ?? 1;
                    // BoardController가 dispatch 직전에 GameManager에 sub-type을 기록한다.
                    // 기본값(None)이면 안전망으로 ChoiceEvent로 폴백.
                    EventSubType sub = GameManager.Instance?.CurrentEventSubType ?? EventSubType.None;
                    if (sub == EventSubType.None) sub = EventSubType.ChoiceEvent;

                    int chosen = PickRandomEventEntry(act, sub);
                    pendingEventCode = chosen;

                    if (chosen <= 0)
                    {
                        Debug.LogWarning($"[InGameUI] Act {act} / {sub} 에 사용 가능한 이벤트가 없습니다.");
                        TransitionToMap();
                        break;
                    }

                    GameManager.Instance?.SetCurrentEventCode(chosen);

                    // 이벤트 entry 카드의 cardName/description을 InGameInfoPanel에 표시
                    ApplyEventCardInfo(chosen);

                    if (sub == EventSubType.ShopEvent)
                    {
                        // 상점형 이벤트 — eventPanel을 거치지 않고 SpawnShopEvent가 보드/손패를 구성.
                        // 맵 이동 카드 1장은 SpawnShopEvent 내부에서 자동 지급된다.
                        CloseMap();
                        if (combatUI != null) combatUI.SetActive(true);
                        if (cardInfoPanel != null) cardInfoPanel.gameObject.SetActive(false);
                        StartCoroutine(InvokeNextFrame(() => boardController?.SpawnShopEvent(chosen)));
                    }
                    else
                    {
                        // 선택형 이벤트 — eventPanel + entry 함수 호출 + 맵 이동 카드 1장 자동 지급.
                        // 플레이어는 선택지를 하나(또는 여러개) 사용한 뒤 맵 이동 카드로 떠난다.
                        CloseMap();
                        if (eventPanel != null) eventPanel.SetActive(true);
                        InvokeEventEntry(chosen);
                        var moveCard = DeckRoguelike.UI.BoardController.CreateMapMoveCard();
                        if (moveCard != null) boardController?.AddCardToHandFree(moveCard);
                    }
                    break;
            }
        }

        private void StartCombat()
        {
            var encounter = GameManager.Instance?.SelectEncounter();
            if (encounter == null)
            {
                Debug.LogError($"[StartCombat] 인카운터 null. GameManager={GameManager.Instance != null}, D={GameManager.Instance?.GameDifficulty}, A={GameManager.Instance?.CurrentAct}, N={GameManager.Instance?.ActDifficulty}");
                return;
            }
            boardController?.SetEncounter(encounter);
            TransitionToCombat();
            combatStartHP = GameManager.Instance?.CurrentHP ?? 0;
            boardController?.StartNewCombat();
        }

        /// <summary>
        /// 새 cardCode 규칙 기반 entry 선택:
        ///   첫째자리 ∈ {act, 4(공통)}, 둘째자리 = sub-type(1=Shop / 2=Choice), 마지막자리 = 0.
        /// </summary>
        private int PickRandomEventEntry(int act, EventSubType subType)
        {
            DeckRoguelike.Event.EventLibrary.RegisterAll();

            var pool = CardRegistry.GetEventEntriesForActAndType(act, subType);
            if (pool.Count == 0) return -1;
            var picked = pool[UnityEngine.Random.Range(0, pool.Count)];
            return picked.cardCode;
        }

        /// <summary>이벤트 entry 카드의 cardName/description을 InGameInfoPanel에 표시.</summary>
        public void ApplyEventCardInfo(int entryCardCode)
        {
            var card = CardRegistry.GetEventCard(entryCardCode);
            if (card == null) return;
            SetInGameInfo(card.cardName, card.description);
            ShowInGameInfoPanel();
        }

        /// <summary>
        /// Entry 이벤트 카드의 함수("event_{cardCode}")를 CardEffectRegistry로 실행.
        /// EventLibrary에 등록된 entry 함수가 본문 표시 + 선택지 카드를 손패에 추가합니다.
        /// </summary>
        private void InvokeEventEntry(int entryCardCode)
        {
            DeckRoguelike.Event.EventLibrary.RegisterAll();

            var ctx = new DeckRoguelike.Combat.CardEffectContext
            {
                Board = boardController,
                Card  = CardRegistry.GetEventCard(entryCardCode),
            };
            DeckRoguelike.Combat.CardEffectRegistry.Execute($"event_{entryCardCode}", ctx);
        }

        /// <summary>EventLibrary가 선택지 해결 후 호출. 맵으로 복귀.</summary>
        public void FinishEventToMap()
        {
            TransitionToMap();
        }

        /// <summary>맵 이동 카드를 사용하기 전에 호출 — 열려있는 eventPanel을 닫아
        /// 맵 셀이 보이고 클릭 가능해지도록 한다.</summary>
        public void HideEventPanelIfOpen()
        {
            if (eventPanel != null && eventPanel.activeSelf)
                eventPanel.SetActive(false);
        }

        #endregion

        #region Combat End Handler

        private void HandleBoardEnded(bool victory)
        {
            if (GameManager.Instance != null) GameManager.Instance.IsInCombat = false;

            ClearPlayerEffects();

            // 1) TransitionToMap 전에 mapPlayerPos를 전투 시작 좌표로 복원 →
            //    ShowMap → InitializeMapBoard → PlacePlayerOnMap이 올바른 좌표를 사용.
            boardController?.RestoreMapPlayerPosAfterCombat();

            if (victory)
            {
                GameManager.Instance?.AddEnemyDefeated();
                Debug.Log("[InGameUI] 전투 승리 → 맵 복귀");
                TransitionToMap();
                // 전투 승리 후 보상 상태 안내: InGameInfoPanel을 승리 메시지(victory_name/desc)로 갱신.
                // TransitionToMap이 ShowMapState 안에서 start_name/desc로 셋한 것을 덮어쓴다.
                ApplyInGameInfoForState(victoryStateInfo, "victory");
                ShowInGameInfoPanel();
            }
            else
            {
                Debug.Log("[InGameUI] 전투 패배 → 맵 복귀");
                TransitionToMap();
            }

            // 2) TransitionToMap 후에도 한 번 더 강제 재배치.
            //    Why: ShowMap → InitializeMapBoard 흐름에서 PlacePlayerOnMap이 실패하거나
            //         _pendingVictoryReward 처리 중 보드/플레이어 객체가 일시적으로 비활성화될 수 있다.
            //         최종적으로 플레이어 좌표가 전투 시작 셀에 있도록 보장한다.
            boardController?.RestoreMapPlayerPosAfterCombat();
        }

        /// <summary>BoardController에서 보상 유물을 요청할 때 호출</summary>
        public RelicData PickRewardRelic(bool isBoss)
        {
            return isBoss ? PickBossRelicReward()
                          : PickDangerCombatRelicReward(GameManager.Instance?.CurrentAct ?? 1);
        }

        /// <summary>보스 처치 보상 유물을 RelicRegistry에서 무작위로 선택합니다.</summary>
        private RelicData PickBossRelicReward()
        {
            RelicLibrary.RegisterAll();
            ItemLibrary.RegisterAll();
            var pool = RelicRegistry.GetByType(bossRelic: true);

            // 직업 한정 (둘째자리 7/8/9) 비호환 / 이미 보유 유물 제외
            if (GameManager.Instance != null)
            {
                var character = GameManager.Instance.SelectedCharacter;
                pool.RemoveAll(r => !r.IsForCharacter(character));

                var owned = new System.Collections.Generic.HashSet<int>();
                foreach (var r in GameManager.Instance.Relics)
                    if (r?.Data != null) owned.Add(r.Data.relicCode);
                pool.RemoveAll(r => owned.Contains(r.relicCode));
            }

            if (pool.Count == 0) return null;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        /// <summary>
        /// 보스 유물 풀에서 직업 한정(둘째자리 7/8/9)·보유 중 유물 규칙을 준수해
        /// 최대 count개의 서로 다른 보스 유물을 무작위로 뽑아 반환합니다 (60010 유물선택 카드용).
        /// 풀이 모자라면 가능한 만큼만 반환합니다.
        /// </summary>
        public List<RelicData> PickBossRelicChoices(int count)
        {
            RelicLibrary.RegisterAll();
            ItemLibrary.RegisterAll();
            var pool = RelicRegistry.GetByType(bossRelic: true);

            // 직업 한정 비호환 / 이미 보유 유물 제외 (PickBossRelicReward와 동일 규칙)
            if (GameManager.Instance != null)
            {
                var character = GameManager.Instance.SelectedCharacter;
                pool.RemoveAll(r => !r.IsForCharacter(character));

                var owned = new System.Collections.Generic.HashSet<int>();
                foreach (var r in GameManager.Instance.Relics)
                    if (r?.Data != null) owned.Add(r.Data.relicCode);
                pool.RemoveAll(r => owned.Contains(r.relicCode));
            }

            var result = new List<RelicData>();
            while (result.Count < count && pool.Count > 0)
            {
                int idx = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
            return result;
        }

        private int CalculateGoldReward(bool isDangerCombat)
        {
            return isDangerCombat
                ? UnityEngine.Random.Range(30, 41)
                : UnityEngine.Random.Range(10, 21);
        }

        /// <summary>
        /// 위험 전투 / 보물방 보상 유물: Act별 가중치로 등급(Common/Uncommon/Rare) 결정 후 무작위 1장.
        ///   Act1=75/25/0   Act2=35/50/15   Act3+=0/75/25
        /// 70/80/90 시작 유물, 4xx 상점 전용 유물, 직업 비호환 유물은 풀에서 제외.
        /// 해당 등급 풀이 비면 인접 등급으로 폴백.
        /// </summary>
        private RelicData PickDangerCombatRelicReward(int act)
        {
            RelicLibrary.RegisterAll();
            var allNonBoss = RelicRegistry.GetByType(bossRelic: false);

            // 시작 유물 / 상점 전용 / 직업 비호환 / 보유 중 유물 제외
            allNonBoss.RemoveAll(r => r.IsStartingRelic);
            allNonBoss.RemoveAll(r => r.IsShopOnlyRelic);
            if (GameManager.Instance != null)
            {
                var character = GameManager.Instance.SelectedCharacter;
                allNonBoss.RemoveAll(r => !r.IsForCharacter(character));

                var owned = new System.Collections.Generic.HashSet<int>();
                foreach (var r in GameManager.Instance.Relics)
                    if (r?.Data != null) owned.Add(r.Data.relicCode);
                allNonBoss.RemoveAll(r => owned.Contains(r.relicCode));
            }

            // Act별 가중치 (act는 1부터)
            (float c, float u, float ra) weights = act switch
            {
                <= 1 => (75f, 25f, 0f),
                2    => (35f, 50f, 15f),
                _    => (0f, 75f, 25f),
            };

            float total = weights.c + weights.u + weights.ra;
            float roll  = UnityEngine.Random.Range(0f, total);
            int targetRarity;
            if (roll < weights.c)               targetRarity = 1; // Common (1xx)
            else if (roll < weights.c + weights.u) targetRarity = 2; // Uncommon (2xx)
            else                                targetRarity = 3; // Rare (3xx)

            // 우선 targetRarity → 이웃 등급 → 전체 폴백
            int[] order = targetRarity switch
            {
                1 => new[] { 1, 2, 3 },
                2 => new[] { 2, 1, 3 },
                _ => new[] { 3, 2, 1 },
            };
            foreach (int hundreds in order)
            {
                var tier = allNonBoss.FindAll(r => r.relicCode / 100 == hundreds);
                if (tier.Count > 0)
                    return tier[UnityEngine.Random.Range(0, tier.Count)];
            }

            if (allNonBoss.Count == 0) return null;
            return allNonBoss[UnityEngine.Random.Range(0, allNonBoss.Count)];
        }

        #endregion

        #region Settings & Panels

        public void ToggleSettings()
        {
            // 카드 sticky 중에도 설정 버튼은 sticky를 해제하지 않고 그대로 동작한다.
            bool isOpen = settingsMenuController != null ? settingsMenuController.IsOpen
                        : (settingsPanel != null && settingsPanel.activeSelf);
            bool opening = !isOpen;
            if (opening)
            {
                if (drawPanel != null && drawPanel.activeSelf)
                {
                    drawPanel.SetActive(false);
                    RemoveFromStack("Draw");
                    SetTopBarButtonsInteractable(true);
                }
                if (discardPanel != null && discardPanel.activeSelf)
                {
                    discardPanel.SetActive(false);
                    RemoveFromStack("Discard");
                    SetTopBarButtonsInteractable(true);
                }
                if (settingsMenuController != null) settingsMenuController.Open();
                else if (settingsPanel != null) settingsPanel.SetActive(true);
                PushPanel("Settings", CloseSettings);
            }
            else
            {
                CloseSettings();
            }
        }

        private void CloseSettings()
        {
            if (settingsMenuController != null) settingsMenuController.Close();
            else if (settingsPanel != null) settingsPanel.SetActive(false);
            RemoveFromStack("Settings");
            UpdateBackButton();
        }

        private bool IsSettingsOpen =>
            settingsMenuController != null ? settingsMenuController.IsOpen
            : (settingsPanel != null && settingsPanel.activeSelf);

        // ── 패널 스택 API ──────────────────────────────────────────
        /// <summary>패널을 스택에 등록. 최대 3개 초과 시 가장 오래된 패널을 자동으로 닫음.</summary>
        public void PushPanel(string panelId, System.Action closeAction)
        {
            // 이미 스택에 있으면 기존 항목 제거 후 맨 위로 재등록
            RemoveFromStack(panelId);

            // MaxPanelStack 초과 시 가장 최근(top) 항목을 pop하며 close 발화
            if (panelStack.Count >= MaxPanelStack)
            {
                var top = panelStack.Last;
                panelStack.RemoveLast();
                top.Value.close?.Invoke();
            }

            panelStack.AddLast(new PanelEntry { id = panelId, close = closeAction });
            UpdateBackButton();
        }

        /// <summary>패널이 닫힐 때 스택에서 제거 (close 액션 실행 안 함).</summary>
        public void UnregisterPanel(string panelId)
        {
            RemoveFromStack(panelId);
            UpdateBackButton();
        }

        public void RefreshBackButton() => UpdateBackButton();

        private void RemoveFromStack(string panelId)
        {
            var node = panelStack.First;
            while (node != null)
            {
                var next = node.Next;
                if (node.Value.id == panelId) { panelStack.Remove(node); return; }
                node = next;
            }
        }

        // 범용 뒤로가기: 스택 TOP 패널을 닫음
        public void OnBackClicked()
        {
            PlaySound(buttonClickSound);
            if (panelStack.Count == 0) return;

            var top = panelStack.Last.Value;
            panelStack.RemoveLast();   // 먼저 제거 → close 에서 UnregisterPanel 호출돼도 no-op
            top.close?.Invoke();
            UpdateBackButton();
        }

        private void UpdateBackButton()
        {
            if (backButton == null) return;
            bool onlyMap = panelStack.Count == 1 && panelStack.Last.Value.id == "Map";
            backButton.gameObject.SetActive(panelStack.Count > 0 && !onlyMap);
        }

        public void AbandonRun()
        {
            PlaySound(buttonClickSound);
            Time.timeScale = 1f;
            GameManager.Instance?.AbandonRun();
        }

        public void ReturnToMainMenu()
        {
            PlaySound(buttonClickSound);
            Time.timeScale = 1f;
            GameManager.Instance?.EnterMainMenu();
            SceneLoader.Instance?.LoadScene("MainMenu");
        }

        public void RestartCombat()
        {
            if (boardController == null) return;
            Time.timeScale = 1f;
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.SetHP(combatStartHP > 0 ? combatStartHP : gm.MaxHP);
                gm.IsInCombat = true;
            }
            TransitionToCombat();
            boardController.StartNewCombat();
        }

        #endregion

        #region Deck Viewer

        public void ToggleDeckViewer()
        {
            // 카드 sticky 중에도 덱 뷰어 버튼은 sticky를 해제하지 않고 그대로 동작한다.
            PlaySound(buttonClickSound);
            if (IsSettingsOpen)
            {
                CloseSettings();
                if (deckViewerPanel != null && !deckViewerPanel.activeSelf)
                {
                    if (isMapOpen) CloseMap();
                    deckViewerPanel.SetActive(true);
                    EnsureDeckViewerPopulated();
                    PushPanel("DeckViewer", CloseDeckViewer);
                }
                return;
            }

            if (deckViewerPanel != null && deckViewerPanel.activeSelf)
            {
                deckViewerPanel.SetActive(false);
                RemoveFromStack("DeckViewer");
                UpdateBackButton();
            }
            else
            {
                if (isMapOpen) CloseMap();
                if (deckViewerPanel != null) deckViewerPanel.SetActive(true);
                EnsureDeckViewerPopulated();
                PushPanel("DeckViewer", CloseDeckViewer);
            }
        }

        /// <summary>덱 뷰어 패널이 열릴 때 내부 CardListController(들)을 강제로 Deck 모드로 맞추고
        /// Refresh를 한 번 더 호출한다. 패널의 자식이 비활성 상태로 저장돼 있어 OnEnable이 발화하지 않거나,
        /// Inspector에서 mode가 Deck으로 설정되지 않은 경우에도 카드 목록이 비는 버그를 방지한다.</summary>
        private void EnsureDeckViewerPopulated()
        {
            if (deckViewerPanel == null) return;
            var controllers = deckViewerPanel.GetComponentsInChildren<CardListController>(true);
            if (controllers.Length == 0)
            {
                Debug.LogWarning("[InGameUIController] deckViewerPanel 내부에 CardListController 없음 — Inspector 확인 필요");
                return;
            }
            foreach (var c in controllers)
            {
                if (!c.gameObject.activeSelf) c.gameObject.SetActive(true);
                c.SetMode(CardListMode.Deck);
                c.Refresh();
            }
        }

        public void ShowDrawPanel()
        {
            CloseSettings();
            if (deckViewerPanel != null) { deckViewerPanel.SetActive(false); RemoveFromStack("DeckViewer"); }
            if (discardPanel != null)    { discardPanel.SetActive(false);    RemoveFromStack("Discard"); }

            if (drawPanel != null) drawPanel.SetActive(true);
            SetTopBarButtonsInteractable(false);
            PushPanel("Draw", () => { drawPanel?.SetActive(false); SetTopBarButtonsInteractable(true); });
        }

        public void ToggleDrawPanel()
        {
            if (drawPanel != null && drawPanel.activeSelf)
            {
                drawPanel.SetActive(false);
                RemoveFromStack("Draw");
                SetTopBarButtonsInteractable(true);
                UpdateBackButton();
            }
            else
            {
                ShowDrawPanel();
            }
        }

        public void ShowDiscardPanel()
        {
            CloseSettings();
            if (deckViewerPanel != null) { deckViewerPanel.SetActive(false); RemoveFromStack("DeckViewer"); }
            if (drawPanel != null)       { drawPanel.SetActive(false);       RemoveFromStack("Draw"); }

            if (discardPanel != null) discardPanel.SetActive(true);
            SetTopBarButtonsInteractable(false);
            PushPanel("Discard", () => { discardPanel?.SetActive(false); SetTopBarButtonsInteractable(true); });
        }

        public void ToggleDiscardPanel()
        {
            if (discardPanel != null && discardPanel.activeSelf)
            {
                discardPanel.SetActive(false);
                RemoveFromStack("Discard");
                SetTopBarButtonsInteractable(true);
                UpdateBackButton();
            }
            else
            {
                ShowDiscardPanel();
            }
        }

        public void CloseDeckViewer()
        {
            if (deckViewerPanel != null) deckViewerPanel.SetActive(false);
            RemoveFromStack("DeckViewer");
            UpdateBackButton();
        }

        public void SetTopBarButtonsInteractable(bool interactable)
        {
            if (mapButton != null) mapButton.interactable = interactable;
            if (deckButton != null) deckButton.interactable = interactable;
        }

        #endregion

        #region Relic UI

        // ── 레이아웃 상수 ──────────────────────────────────────────
        private const int RelicCols     = 5;   // 페이지당 열 수
        private const int RelicsPerCol  = 5;   // 열당 최대 유물 수 (행 수)
        private const int RelicsPerPage = 25;  // 5×5 그리드 풀 사용, 25개 초과 시 다음 페이지 + nav 생성

        // ── 페이지 상태 ────────────────────────────────────────────
        private readonly List<GameObject> _relicPageObjs = new List<GameObject>();
        private readonly List<GameObject> _relicNavButtons = new List<GameObject>();
        private int  _relicCurrentPage = 0;
        private int  _totalRelicCount  = 0;
        private bool _relicLayoutReady = false;

        /// <summary>씬 로드 시 이미 보유한 유물 아이콘을 전부 복원합니다.</summary>
        private void RefreshRelicIcons()
        {
            if (GameManager.Instance == null) return;
            foreach (var relic in GameManager.Instance.Relics)
                AddRelicIcon(relic);
        }

        /// <summary>유물 획득 시 3열 5행 페이지 레이아웃에 아이콘을 추가합니다.</summary>
        private void AddRelicIcon(RelicEffect relic)
        {
            if (relicIconContainer == null || relicIconPrefab == null || relic?.Data == null) return;

            EnsureRelicLayout();

            int pageIndex  = _totalRelicCount / RelicsPerPage;
            int slotInPage = _totalRelicCount % RelicsPerPage;
            // 각 열 5개씩, 총 25개. nav button은 ignoreLayout으로 그리드 밖에 배치.
            int colIndex = slotInPage / RelicsPerCol;

            // 필요하면 새 페이지 생성
            while (_relicPageObjs.Count <= pageIndex)
                CreateRelicPage();

            // 새 페이지가 추가된 경우 해당 페이지로 이동
            if (pageIndex != _relicCurrentPage)
            {
                _relicCurrentPage = pageIndex;
                ShowRelicPage(_relicCurrentPage);
                RefreshRelicNavButtons();
            }

            // 해당 열 컨테이너에 아이콘 생성
            Transform colContainer = _relicPageObjs[pageIndex].transform.GetChild(colIndex);
            GameObject iconObj = Instantiate(relicIconPrefab, colContainer);
            iconObj.name = $"RelicIcon_{relic.Data.relicCode}";


            // 아이콘 이미지 설정: Data.icon 우선, 없으면 Addressables 로드
            var img = iconObj.GetComponentInChildren<Image>();
            if (img != null)
            {
                Sprite sprite = relic.Data.icon;
                if (sprite == null && relic.Data.relicCode > 0)
                    sprite = Addressables.LoadAssetAsync<Sprite>($"Sprites/Ingame/Relic/{relic.Data.relicCode}").WaitForCompletion();
                if (sprite != null)
                    img.sprite = sprite;
                else
                    img.color = new Color(0.6f, 0.4f, 0.8f, 1f);
                img.enabled = true;
            }

            var btn = iconObj.GetComponentInChildren<Button>();
            if (btn == null) btn = iconObj.AddComponent<Button>();
            if (btn.targetGraphic == null) btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                // 카드 sticky 중에는 유물 상호작용 차단.
                if (BoardController.AnyCardStickyActive) return;
                relicInfoController?.Toggle();
            });

            var trigger = iconObj.GetComponent<DeckRoguelike.UI.TooltipTrigger>();
            trigger?.SetRelicCode(relic.Data.relicCode);

            // prefab의 모든 TMP 텍스트를 기본 비활성화
            var allTexts = iconObj.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            foreach (var t in allTexts)
                t.gameObject.SetActive(false);

            // 카운터가 있는 유물(ActionCountStrengthRelic 등)만 텍스트 활성화
            if (relic.CounterText != null)
            {
                // prefab에 TMP가 있으면 재사용, 없으면 동적 생성
                TMPro.TextMeshProUGUI counterTmp = allTexts.Length > 0 ? allTexts[0] : null;

                if (counterTmp == null)
                {
                    var textGO = new GameObject("CounterText", typeof(RectTransform));
                    textGO.transform.SetParent(iconObj.transform, false);
                    var rt2 = textGO.GetComponent<RectTransform>();
                    rt2.anchorMin = new Vector2(0f, 0f);
                    rt2.anchorMax = new Vector2(1f, 0.45f);
                    rt2.offsetMin = Vector2.zero;
                    rt2.offsetMax = Vector2.zero;

                    counterTmp = textGO.AddComponent<TMPro.TextMeshProUGUI>();
                    counterTmp.fontSize  = 9f;
                    counterTmp.alignment = TMPro.TextAlignmentOptions.Center;
                    counterTmp.color     = Color.white;
                    counterTmp.fontStyle = TMPro.FontStyles.Bold;
                }

                counterTmp.gameObject.SetActive(true);
                counterTmp.text = relic.CounterText;

                relic.OnCounterChanged += () =>
                {
                    if (counterTmp != null) counterTmp.text = relic.CounterText ?? "";
                };
            }

            _totalRelicCount++;
        }

        /// <summary>
        /// 970/980/990 같은 업그레이드 유물이 베이스를 교체할 때, 기존 아이콘 GameObject를
        /// 같은 위치에서 새 유물 정보로 갱신합니다. 삭제 후 새 아이콘을 만들면 슬롯이
        /// 비어 보이거나 페이지 끝으로 이동하기 때문에 인플레이스 갱신을 사용합니다.
        /// </summary>
        private void ReplaceRelicIcon(RelicEffect oldRelic, RelicEffect newRelic)
        {
            if (relicIconContainer == null || oldRelic?.Data == null || newRelic?.Data == null) return;

            string oldName  = $"RelicIcon_{oldRelic.Data.relicCode}";
            GameObject iconObj = null;
            for (int p = 0; p < _relicPageObjs.Count && iconObj == null; p++)
            {
                var page = _relicPageObjs[p];
                if (page == null) continue;
                var found = page.transform.Find(oldName);
                if (found != null) { iconObj = found.gameObject; continue; }
                for (int c = 0; c < page.transform.childCount && iconObj == null; c++)
                {
                    var col = page.transform.GetChild(c);
                    var hit = col.Find(oldName);
                    if (hit != null) iconObj = hit.gameObject;
                }
            }

            if (iconObj == null)
            {
                Debug.LogWarning($"[InGameUIController] 교체할 유물 아이콘을 찾지 못함 ({oldName}) — fallback으로 신규 추가");
                AddRelicIcon(newRelic);
                return;
            }

            iconObj.name = $"RelicIcon_{newRelic.Data.relicCode}";

            // 아이콘 이미지 갱신
            var img = iconObj.GetComponentInChildren<Image>();
            if (img != null)
            {
                Sprite sprite = newRelic.Data.icon;
                if (sprite == null && newRelic.Data.relicCode > 0)
                    sprite = Addressables.LoadAssetAsync<Sprite>($"Sprites/Ingame/Relic/{newRelic.Data.relicCode}").WaitForCompletion();
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color  = Color.white;
                }
                else
                {
                    img.color = new Color(0.6f, 0.4f, 0.8f, 1f);
                }
                img.enabled = true;
            }

            // 툴팁 갱신
            var trigger = iconObj.GetComponent<DeckRoguelike.UI.TooltipTrigger>();
            trigger?.SetRelicCode(newRelic.Data.relicCode);

            // 카운터 텍스트 갱신 (구독 재연결)
            var allTexts = iconObj.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            foreach (var t in allTexts)
                t.gameObject.SetActive(false);

            if (newRelic.CounterText != null)
            {
                TMPro.TextMeshProUGUI counterTmp = allTexts.Length > 0 ? allTexts[0] : null;
                if (counterTmp == null)
                {
                    var textGO = new GameObject("CounterText", typeof(RectTransform));
                    textGO.transform.SetParent(iconObj.transform, false);
                    var rt2 = textGO.GetComponent<RectTransform>();
                    rt2.anchorMin = new Vector2(0f, 0f);
                    rt2.anchorMax = new Vector2(1f, 0.45f);
                    rt2.offsetMin = Vector2.zero;
                    rt2.offsetMax = Vector2.zero;

                    counterTmp = textGO.AddComponent<TMPro.TextMeshProUGUI>();
                    counterTmp.fontSize  = 9f;
                    counterTmp.alignment = TMPro.TextAlignmentOptions.Center;
                    counterTmp.color     = Color.white;
                    counterTmp.fontStyle = TMPro.FontStyles.Bold;
                }

                counterTmp.gameObject.SetActive(true);
                counterTmp.text = newRelic.CounterText;

                newRelic.OnCounterChanged += () =>
                {
                    if (counterTmp != null) counterTmp.text = newRelic.CounterText ?? "";
                };
            }
        }

        /// <summary>
        /// 유물 아이콘을 제거합니다 (207/208/209 각인된 부적의 픽 취소 시 호출).
        /// 페이지 슬롯 카운터(_totalRelicCount)를 감소시키고, 빈 페이지가 마지막에 남으면 제거합니다.
        /// </summary>
        private void RemoveRelicIcon(RelicEffect relic)
        {
            if (relicIconContainer == null || relic?.Data == null) return;

            string name = $"RelicIcon_{relic.Data.relicCode}";
            GameObject iconObj = null;
            for (int p = 0; p < _relicPageObjs.Count && iconObj == null; p++)
            {
                var page = _relicPageObjs[p];
                if (page == null) continue;
                var found = page.transform.Find(name);
                if (found != null) { iconObj = found.gameObject; continue; }
                for (int c = 0; c < page.transform.childCount && iconObj == null; c++)
                {
                    var col = page.transform.GetChild(c);
                    var hit = col.Find(name);
                    if (hit != null) iconObj = hit.gameObject;
                }
            }

            if (iconObj == null) return;
            Destroy(iconObj);
            if (_totalRelicCount > 0) _totalRelicCount--;
        }

        /// <summary>최초 1회 레이아웃 뼈대(root VLG)를 생성합니다.
        /// 씬에 배치된 placeholder 자식이 있으면 먼저 제거합니다.</summary>
        private void EnsureRelicLayout()
        {
            if (_relicLayoutReady) return;
            _relicLayoutReady = true;

            // 씬에 배치된 기존 자식(placeholder) 제거
            for (int i = relicIconContainer.childCount - 1; i >= 0; i--)
                Destroy(relicIconContainer.GetChild(i).gameObject);

            // root container: VerticalLayoutGroup → 페이지들을 위→아래 배치
            var vlg = relicIconContainer.GetComponent<VerticalLayoutGroup>()
                   ?? relicIconContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment        = TextAnchor.UpperCenter;
            vlg.spacing               = 0f;
            vlg.childForceExpandWidth  = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = false;
            vlg.childControlHeight     = false;

        }

        /// <summary>HorizontalLayoutGroup + 3열 컨테이너로 구성된 페이지 GO를 생성합니다.
        /// Col_1(2열)에 nav button을 6행째로 자동 배치합니다.</summary>
        private void CreateRelicPage()
        {
            int idx = _relicPageObjs.Count;

            var pageGO = new GameObject($"RelicPage_{idx}", typeof(RectTransform));
            pageGO.transform.SetParent(relicIconContainer, false);

            var hlg = pageGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.UpperCenter;
            hlg.spacing               = 4f;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = false;
            hlg.childControlHeight     = false;

            float iconW = 0f;
            float iconH = 0f;
            if (relicIconPrefab != null)
            {
                var prefabRect = relicIconPrefab.GetComponent<RectTransform>();
                if (prefabRect != null)
                {
                    iconW = prefabRect.sizeDelta.x > 0f ? prefabRect.sizeDelta.x : prefabRect.rect.width;
                    iconH = prefabRect.sizeDelta.y > 0f ? prefabRect.sizeDelta.y : prefabRect.rect.height;
                }
            }

            // 열 컨테이너 생성
            for (int c = 0; c < RelicCols; c++)
            {
                var colGO   = new GameObject($"Col_{c}", typeof(RectTransform));
                colGO.transform.SetParent(pageGO.transform, false);

                var colRect = colGO.GetComponent<RectTransform>();
                if (iconW > 0f)
                    colRect.sizeDelta = new Vector2(iconW, colRect.sizeDelta.y);

                var vlg = colGO.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment        = TextAnchor.UpperCenter;
                vlg.spacing               = 4f;
                vlg.childForceExpandWidth  = false;
                vlg.childForceExpandHeight = false;
                vlg.childControlWidth      = false;
                vlg.childControlHeight     = false;
            }

            // 첫 페이지만 표시, 나머지 숨김
            pageGO.SetActive(idx == 0);
            _relicPageObjs.Add(pageGO);
        }

        /// <summary>마지막 유물 아이콘을 제거합니다 (디버그용).</summary>
        public void DebugRemoveLastRelicIcon()
        {
            if (_totalRelicCount <= 0 || _relicPageObjs.Count == 0) return;

            _totalRelicCount--;
            int pageIndex  = _totalRelicCount / RelicsPerPage;
            int slotInPage = _totalRelicCount % RelicsPerPage;
            int colIndex = slotInPage / RelicsPerCol;

            if (pageIndex < _relicPageObjs.Count)
            {
                var colContainer = _relicPageObjs[pageIndex].transform.GetChild(colIndex);
                if (colContainer.childCount > 0)
                {
                    Destroy(colContainer.GetChild(colContainer.childCount - 1).gameObject);
                }

                // 빈 페이지 제거
                if (_totalRelicCount % RelicsPerPage == 0 && pageIndex > 0)
                {
                    // 해당 페이지의 nav button도 제거
                    if (pageIndex < _relicNavButtons.Count)
                    {
                        if (_relicNavButtons[pageIndex] != null) Destroy(_relicNavButtons[pageIndex]);
                        _relicNavButtons.RemoveAt(pageIndex);
                    }
                    Destroy(_relicPageObjs[pageIndex]);
                    _relicPageObjs.RemoveAt(pageIndex);
                    _relicCurrentPage = Mathf.Clamp(_relicCurrentPage, 0, _relicPageObjs.Count - 1);
                    ShowRelicPage(_relicCurrentPage);
                }
            }

            RefreshRelicNavButtons();
        }

        /// <summary>유물 네비게이션 버튼 클릭 시 다음 페이지로 순환합니다.</summary>
        private void OnRelicNavClicked()
        {
            if (_relicPageObjs.Count <= 1) return;
            _relicCurrentPage = (_relicCurrentPage + 1) % _relicPageObjs.Count;
            ShowRelicPage(_relicCurrentPage);
        }

        /// <summary>지정 페이지만 활성화하고 나머지는 비활성화합니다.</summary>
        private void ShowRelicPage(int page)
        {
            for (int i = 0; i < _relicPageObjs.Count; i++)
                _relicPageObjs[i].SetActive(i == page);
        }

        /// <summary>페이지가 2개 이상이면 nav button을 각 페이지에 생성/표시합니다.</summary>
        private void RefreshRelicNavButtons()
        {
            bool needNav = _relicPageObjs.Count > 1;

            if (!needNav)
            {
                // 페이지 1개 이하: 기존 nav button 모두 제거
                foreach (var navObj in _relicNavButtons)
                    if (navObj != null) Destroy(navObj);
                _relicNavButtons.Clear();
                return;
            }

            // 아직 생성 안 된 페이지에 nav button 추가
            for (int i = _relicNavButtons.Count; i < _relicPageObjs.Count; i++)
                CreateRelicNavButtonForPage(i);
        }

        /// <summary>relicNavPrefab을 Col_1의 자식으로 Instantiate하여 6행 좌표에 배치</summary>
        private void CreateRelicNavButtonForPage(int pageIndex)
        {
            if (relicNavPrefab == null || pageIndex >= _relicPageObjs.Count) return;

            var page = _relicPageObjs[pageIndex];
            if (page.transform.childCount < 5) return;

            // Col_4(5열)의 자식으로 생성
            Transform col4 = page.transform.GetChild(4);
            var navObj = Instantiate(relicNavPrefab, col4);
            navObj.name = $"RelicNavBtn_{pageIndex}";
            navObj.SetActive(true);

            var le = navObj.GetComponent<UnityEngine.UI.LayoutElement>()
                  ?? navObj.AddComponent<UnityEngine.UI.LayoutElement>();
            le.ignoreLayout = true;

            var navRect = navObj.GetComponent<RectTransform>();
            navRect.anchorMin = new Vector2(0.5f, 1f);
            navRect.anchorMax = new Vector2(0.5f, 1f);
            navRect.pivot     = new Vector2(0.5f, 0.5f);
            navRect.anchoredPosition = new Vector2(0f, -548f);

            // 자식에 있는 Button 찾아서 이벤트 연결
            var btn = navObj.GetComponentInChildren<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnRelicNavClicked);
            }

            _relicNavButtons.Add(navObj);
        }

        /// <summary>아이템 획득 시 아이템 슬롯 컨테이너에 아이콘 슬롯을 추가합니다.</summary>
        private void AddItemSlot(DeckRoguelike.Item.ItemData item)
        {
            if (item == null) return;

            // 빈 슬롯 찾기
            for (int c = 0; c < ItemCols; c++)
            {
                for (int r = 0; r < ItemsPerCol; r++)
                {
                    if (_itemSlots[c, r] != null && _itemSlots[c, r].ItemData == null)
                    {
                        _itemSlots[c, r].Initialize(item);
                        return;
                    }
                }
            }
        }

        public void ClearItemSlot(ItemSlotUI slot)
        {
            if (slot == null) return;
            slot.SetEmpty();
        }

        private void OnItemRemoved(DeckRoguelike.Item.ItemData item)
        {
            if (item == null) return;
            for (int c = 0; c < ItemCols; c++)
                for (int r = 0; r < ItemsPerCol; r++)
                    if (_itemSlots[c, r] != null && _itemSlots[c, r].ItemData == item)
                    {
                        _itemSlots[c, r].SetEmpty();
                        return;
                    }
        }

        /// <summary>아이템 슬롯 클릭 시 Use/Discard 확인 패널을 표시합니다.</summary>
        /// <summary>해당 슬롯에 대한 패널이 이미 열려있으면 true.</summary>
        public bool HasActiveConfirmPanelFor(ItemSlotUI slot) => activeConfirmPanel != null && activeConfirmSlot == slot;

        /// <summary>현재 ConfirmUseItemPanel을 띄운 슬롯을 반환합니다 (포션 효과에서 자기 슬롯 비우기 용도).</summary>
        public ItemSlotUI GetActiveConfirmSlot() => activeConfirmSlot;

        public void ShowConfirmUseItemPanel(DeckRoguelike.Item.ItemData item, ItemSlotUI slot)
        {
            // 같은 슬롯의 패널이 이미 열려있으면 무시 (깜빡임 방지)
            if (activeConfirmPanel != null && activeConfirmSlot == slot) return;

            // 다른 슬롯의 패널이 열려있으면 닫기
            if (activeConfirmPanel != null)
            {
                activeConfirmPanel.Close();
                activeConfirmPanel = null;
                activeConfirmSlot  = null;
            }

            if (confirmUseItemPrefab == null)
            {
                Debug.LogWarning("[InGameUIController] confirmUseItemPrefab이 연결되지 않았습니다.");
                return;
            }

            // Canvas 루트 아래에 생성 (다른 UI 위에 표시되도록)
            var canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            var panelObj = Instantiate(confirmUseItemPrefab, parent);
            panelObj.transform.SetAsLastSibling();

            var panel = panelObj.GetComponent<ConfirmUseItemPanel>();
            if (panel == null) panel = panelObj.AddComponent<ConfirmUseItemPanel>();

            panel.Initialize(item, slot);
            activeConfirmPanel = panel;
            activeConfirmSlot  = slot;
        }

        /// <summary>CombatController 참조를 외부에 제공합니다 (ConfirmUseItemPanel에서 사용).</summary>
        public BoardController GetCombatController() => boardController;

        /// <summary>아이템 타겟팅 blocker를 활성/비활성합니다. CombatController에서 호출합니다.</summary>
        public void SetItemTargetingBlocker(bool active)
        {
            if (itemTargetingBlocker != null)
                itemTargetingBlocker.SetActive(active);
        }

        /// <summary>BoardController에서 카드 sticky 중 사이드 버튼들을 제어합니다.
        /// map 버튼은 raycast 차단(상호작용 불가), settings/deck 버튼은 그대로 두되 클릭 시
        /// 핸들러(ToggleSettings/ToggleDeckViewer)에서 sticky를 먼저 해제하고 패널 열기는 생략한다.</summary>
        public void SetSideButtonsInteractable(bool interactable)
        {
            SetButtonRaycast(mapButton, interactable);
            // settings/deck은 sticky 중에도 누르면 sticky를 해제할 수 있어야 하므로 raycast 유지.
            // (ToggleSettings/ToggleDeckViewer가 BoardController.ReleaseStickyCardIfAny로 분기 처리)
        }

        private static void SetButtonRaycast(Button btn, bool enable)
        {
            if (btn == null) return;
            var cg = btn.GetComponent<CanvasGroup>();
            if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = enable;
        }

        /// <summary>ConfirmUseItemPanel이 닫힐 때 호출되어 activeConfirmPanel 참조를 클리어합니다.</summary>
        public void ClearConfirmPanel(ConfirmUseItemPanel panel)
        {
            if (activeConfirmPanel == panel)
            {
                activeConfirmPanel = null;
                activeConfirmSlot  = null;
            }
        }

        /// <summary>지정한 슬롯에 대해 열려있는 ConfirmUseItemPanel을 닫습니다.</summary>
        public void ClearConfirmPanelForSlot(ItemSlotUI slot)
        {
            if (activeConfirmPanel != null && activeConfirmSlot == slot)
            {
                activeConfirmPanel.Close();
                activeConfirmPanel = null;
                activeConfirmSlot  = null;
            }
        }

        #endregion

        #region UI Updates

        private void UpdatePlayerInfo()
        {
            if (GameManager.Instance == null) return;

            UpdateGoldDisplay(GameManager.Instance.Gold);
            UpdateFloorDisplay(GameManager.Instance.CurrentFloor);
            UpdateHPDisplay(GameManager.Instance.CurrentHP, GameManager.Instance.MaxHP);
        }

        private void UpdateHPDisplay(int current, int max)
        {
            if (hpText != null)
                hpText.text = $"{current}/{max}";
        }

        private void UpdateGoldDisplay(int gold)
        {
            if (goldText != null)
            {
                goldText.text = gold.ToString();
            }
        }

        private void UpdateFloorDisplay(int floor)
        {
            if (GameManager.Instance == null) return;

            // 난이도 아이콘 (1=normal 2=hard 3=hell)
            if (levelIcon != null)
            {
                string[] levelNames = { "Level_nomal", "Level_hard", "Level_hell" };
                int idx = Mathf.Clamp(GameManager.Instance.GameDifficulty - 1, 0, levelNames.Length - 1);
                Sprite sp = Addressables.LoadAssetAsync<Sprite>($"Sprites/Ingame/Common/Topbar/{levelNames[idx]}").WaitForCompletion();
                if (sp != null) levelIcon.sprite = sp;
            }

            // 액트 아이콘 (act_1, act_2, ...)
            if (actIcon != null)
            {
                Sprite sp = Addressables.LoadAssetAsync<Sprite>($"Sprites/Ingame/Common/Topbar/act_{GameManager.Instance.CurrentAct}").WaitForCompletion();
                if (sp != null) actIcon.sprite = sp;
            }

            if (actText != null)
            {
                actText.text = $"Act {GameManager.Instance.CurrentAct}";
            }
        }

        #endregion

        #region Public Methods for Sub-Controllers

        /// <summary>
        /// 아이템/유물 효과에서 지정 카드 목록을 띄워 1장 선택하게 합니다.
        /// subCardListController(SetupCardPicker)를 통해 표시되며, 선택/취소 시 callback이 호출됩니다.
        /// </summary>
        public void OpenItemCardSelection(List<CardData> cards, System.Action<CardData> callback)
        {
            OpenCardPicker(cards, callback);
        }

        private System.Collections.IEnumerator InvokeNextFrame(System.Action action)
        {
            yield return null;
            action?.Invoke();
        }

        public void ShowRangeInfo(CardData card)
        {
            Debug.Log($"[ShowRangeInfo] card={(card != null ? card.cardName : "null")}, cardInfoPanel={(cardInfoPanel != null)}");
            if (cardInfoPanel == null || card == null) return;
            // 부모 체인이 비활성이면 패널이 보이지 않음 — 강제 활성화
            var t = cardInfoPanel.transform;
            while (t != null) { if (!t.gameObject.activeSelf) t.gameObject.SetActive(true); t = t.parent; }
            cardInfoPanel.transform.SetAsLastSibling();
            cardInfoPanel.ShowRange(card);
        }

        /// <summary>덱뷰어/도감처럼 좌/우 nav를 지원하는 리스트 모드로 카드 정보 패널을 표시.
        /// currentCards 전체를 넘겨 인접 카드 탐색이 가능하도록 한다.
        /// MainMenuController.ShowCardInfo와 동일한 시그니처 — 단일 카드만 보여줄 땐 ShowRangeInfo(card) 사용.</summary>
        public void ShowCardInfoList(IList<CardData> cards, int startIndex)
        {
            if (cardInfoPanel == null || cards == null || cards.Count == 0) return;
            var t = cardInfoPanel.transform;
            while (t != null) { if (!t.gameObject.activeSelf) t.gameObject.SetActive(true); t = t.parent; }
            cardInfoPanel.transform.SetAsLastSibling();
            cardInfoPanel.ShowWithList(cards, startIndex);
        }

        public void OpenRestCardList(RestCardMode mode, CardConfirmPanel confirmPanel,
                                     System.Action onFinish = null, System.Action onCancel = null)
        {
            if (subCardListController == null) { onFinish?.Invoke(); return; }

            subCardListController.gameObject.SetActive(true);
            subCardListController.Setup(mode, confirmPanel, onFinish);
            SetTopBarButtonsInteractable(false);
            _restCardListCancelCallback = onCancel;
            PushPanel("RestCardList", CloseRestCardList);
        }

        /// <summary>
        /// 임의 카드 풀에서 1장 선택받습니다 (유물 201~204 등). subCardListController + 그 자체의
        /// CardConfirmPanel을 사용합니다. 선택 시 onPicked(card) 호출 후 패널 자동 닫힘.
        /// </summary>
        public void OpenCardPicker(List<CardData> cards, System.Action<CardData> onPicked)
        {
            if (subCardListController == null) { onPicked?.Invoke(null); return; }

            subCardListController.gameObject.SetActive(true);
            subCardListController.SetupCardPicker(cards, null, onPicked);
            SetTopBarButtonsInteractable(false);
            PushPanel("RestCardList", CloseRestCardList);
        }

        /// <summary>
        /// 906/910 대격변 등에서 사용: 카드 풀을 띄워 정확히 count장을 토글 선택받습니다.
        /// 카드 클릭 시 토글 선택(확대 표시)되고, count장이 모두 선택되면 confirm 없이 즉시
        /// onComplete(선택 목록)이 호출되고 패널이 닫힙니다. 확정 없이 닫히면 빈 목록으로 호출됩니다.
        /// </summary>
        public void OpenMultiCardPicker(List<CardData> cards, int count,
                                        System.Action<List<CardData>> onComplete)
        {
            if (subCardListController == null) { onComplete?.Invoke(new List<CardData>()); return; }

            subCardListController.gameObject.SetActive(true);
            subCardListController.SetupMultiCardPicker(cards, count, onComplete);
            SetTopBarButtonsInteractable(false);
            PushPanel("RestCardList", CloseRestCardList);
        }

        /// <summary>
        /// 906/909 유물 등에서 사용: BoardController.BuildRewardPickCards로 보상 풀을 만든 뒤
        /// subCardListController(SetupCardPicker)로 1장 선택받습니다.
        /// 선택 시 onPicked(card), 스킵/취소 시 onPicked(null) 호출.
        /// </summary>
        public void OpenStandaloneCardReward(System.Action<CardData> onPicked)
        {
            if (boardController == null) { onPicked?.Invoke(null); return; }
            var picks = boardController.BuildRewardPickCards();
            if (picks == null || picks.Count == 0) { onPicked?.Invoke(null); return; }
            OpenCardPicker(picks, onPicked);
        }

        public void ClearRestCancelCallback()
        {
            _restCardListCancelCallback = null;
        }

        public void CloseRestCardList()
        {
            // 픽 콜백이 아직 살아있으면(=확정 없이 닫히는 경우) null로 호출해 취소를 알린다.
            // 207/208/209 각인된 부적의 보상 카드 복구가 이 콜백 경로에 의존.
            subCardListController?.CancelPickerIfPending();

            if (subCardListController != null) subCardListController.gameObject.SetActive(false);
            RemoveFromStack("RestCardList");
            SetTopBarButtonsInteractable(true);
            UpdateBackButton();

            if (_restCardListCancelCallback != null)
            {
                var cb = _restCardListCancelCallback;
                _restCardListCancelCallback = null;
                cb.Invoke();
            }
        }

        /// <summary>
        /// 보상 선택 완료 후 맵으로 돌아가기
        /// </summary>
        public void OnRewardComplete()
        {
            TransitionToMap();
        }

        /// <summary>
        /// 휴식/상점/이벤트 완료 후 맵으로 돌아가기
        /// </summary>
        public void OnNodeComplete()
        {
            TransitionToMap();
        }

        /// <summary>
        /// 보스 클리어 시 승리
        /// </summary>
        public void OnBossDefeated()
        {
            // 다음 Act로 가거나 게임 클리어
            if (GameManager.Instance != null && GameManager.Instance.CurrentAct >= 3)
            {
                TransitionToVictory();
            }
            else
            {
                // 다음 Act
                GameManager.Instance?.AdvanceFloor();
                boardController?.GenerateNewMap();
                TransitionToMap();
            }
        }

        #endregion

        #region Player Effect Icons (TopBar)

        private const int EffectCols     = 5;   // 페이지당 열 수
        private const int EffectsPerCol  = 5;   // 열당 최대 이펙트 수 (행 수)
        private const int EffectsPerPage = 25;  // 5×5 그리드 풀 사용, 25개 초과 시 다음 페이지 + nav 생성
        private readonly List<GameObject> _playerEffectObjs = new List<GameObject>();
        private readonly List<GameObject> _effectPageObjs = new List<GameObject>();
        private readonly List<GameObject> _effectNavButtons = new List<GameObject>();
        private int _effectCurrentPage = 0;
        private bool _effectLayoutReady = false;

        private void EnsureEffectLayout()
        {
            if (_effectLayoutReady) return;
            _effectLayoutReady = true;

            for (int i = playerEffectContainer.childCount - 1; i >= 0; i--)
                Destroy(playerEffectContainer.GetChild(i).gameObject);

            // 기존 레이아웃 그룹 제거
            var oldHlg = playerEffectContainer.GetComponent<HorizontalLayoutGroup>();
            if (oldHlg != null) Destroy(oldHlg);
            var oldVlg = playerEffectContainer.GetComponent<VerticalLayoutGroup>();
            if (oldVlg != null) Destroy(oldVlg);

            // root VLG — 자식 page가 container 너비를 채우도록 childControlWidth = true
            var vlg = playerEffectContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment        = TextAnchor.UpperRight;
            vlg.spacing               = 0f;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;

        }

        /// <summary>이펙트 페이지 생성: relic과 동일 구조 (3열 5행).
        /// container X 좌표에서 시작하여 왼쪽으로 확장.
        /// Col_1(2열) 6행에 nav button 배치.</summary>
        private GameObject CreateEffectPage()
        {
            int idx = _effectPageObjs.Count;
            var pageGO = new GameObject($"EffectPage_{idx}", typeof(RectTransform));
            pageGO.transform.SetParent(playerEffectContainer, false);

            // page를 container 전체 너비로 stretch
            var pageRect = pageGO.GetComponent<RectTransform>();
            pageRect.anchorMin = new Vector2(0f, 1f);
            pageRect.anchorMax = new Vector2(1f, 1f);
            pageRect.pivot     = new Vector2(1f, 1f);
            pageRect.offsetMin = Vector2.zero;
            pageRect.offsetMax = Vector2.zero;

            // HLG: 오른쪽 정렬 — 첫 열이 container X(오른쪽 끝)에 위치
            var hlg = pageGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.UpperRight;
            hlg.spacing               = 4f;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = false;
            hlg.childControlHeight     = false;

            float iconW = 0f;
            float iconH = 0f;
            if (relicIconPrefab != null)
            {
                var prefabRect = relicIconPrefab.GetComponent<RectTransform>();
                if (prefabRect != null)
                {
                    iconW = prefabRect.sizeDelta.x > 0f ? prefabRect.sizeDelta.x : prefabRect.rect.width;
                    iconH = prefabRect.sizeDelta.y > 0f ? prefabRect.sizeDelta.y : prefabRect.rect.height;
                }
            }

            for (int c = 0; c < EffectCols; c++)
            {
                var colGO = new GameObject($"Col_{c}", typeof(RectTransform));
                colGO.transform.SetParent(pageGO.transform, false);

                var colRect = colGO.GetComponent<RectTransform>();
                if (iconW > 0f)
                    colRect.sizeDelta = new Vector2(iconW, iconH * EffectsPerCol + 4f * (EffectsPerCol - 1));

                var colVlg = colGO.AddComponent<VerticalLayoutGroup>();
                colVlg.childAlignment        = TextAnchor.UpperCenter;
                colVlg.spacing               = 4f;
                colVlg.childForceExpandWidth  = false;
                colVlg.childForceExpandHeight = false;
                colVlg.childControlWidth      = false;
                colVlg.childControlHeight     = false;
            }

            pageGO.SetActive(idx == 0);
            _effectPageObjs.Add(pageGO);
            return pageGO;
        }

        private void ShowEffectPage(int page)
        {
            for (int i = 0; i < _effectPageObjs.Count; i++)
                _effectPageObjs[i].SetActive(i == page);
        }

        private void OnEffectNavClicked()
        {
            if (_effectPageObjs.Count <= 1) return;
            _effectCurrentPage = (_effectCurrentPage + 1) % _effectPageObjs.Count;
            ShowEffectPage(_effectCurrentPage);
        }

        private void RefreshEffectNavButton()
        {
            bool needNav = _effectPageObjs.Count > 1;

            if (!needNav)
            {
                foreach (var navObj in _effectNavButtons)
                    if (navObj != null) Destroy(navObj);
                _effectNavButtons.Clear();
                return;
            }

            for (int i = _effectNavButtons.Count; i < _effectPageObjs.Count; i++)
                CreateEffectNavButtonForPage(i);
        }

        private void CreateEffectNavButtonForPage(int pageIndex)
        {
            if (effectNavPrefab == null || pageIndex >= _effectPageObjs.Count) return;

            var page = _effectPageObjs[pageIndex];
            if (page.transform.childCount < 1) return;

            // Col_0(1열)의 자식으로 생성
            Transform col0 = page.transform.GetChild(0);
            var navObj = Instantiate(effectNavPrefab, col0);
            navObj.name = $"EffectNavBtn_{pageIndex}";
            navObj.SetActive(true);

            var le = navObj.GetComponent<UnityEngine.UI.LayoutElement>()
                  ?? navObj.AddComponent<UnityEngine.UI.LayoutElement>();
            le.ignoreLayout = true;

            var navRect = navObj.GetComponent<RectTransform>();
            navRect.anchorMin = new Vector2(0.5f, 1f);
            navRect.anchorMax = new Vector2(0.5f, 1f);
            navRect.pivot     = new Vector2(0.5f, 0.5f);
            navRect.anchoredPosition = new Vector2(0f, -548f);

            var btn = navObj.GetComponentInChildren<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnEffectNavClicked);
            }

            _effectNavButtons.Add(navObj);
        }

        private Transform GetEffectPageContainer(int index)
        {
            int pageIndex = index / EffectsPerPage;
            int slotInPage = index % EffectsPerPage;
            // 오른쪽(Col_2)부터 채움: 0~4→Col_2, 5~9→Col_1, 10~14→Col_0
            int colIndex = (EffectCols - 1) - (slotInPage / EffectsPerCol);

            while (_effectPageObjs.Count <= pageIndex)
                CreateEffectPage();
            return _effectPageObjs[pageIndex].transform.GetChild(colIndex);
        }

        private void RebuildEffectPages()
        {
            var validObjs = new List<GameObject>();
            foreach (var obj in _playerEffectObjs)
                if (obj != null) validObjs.Add(obj);
            _playerEffectObjs.Clear();
            _playerEffectObjs.AddRange(validObjs);

            foreach (var page in _effectPageObjs)
                if (page != null) Destroy(page);
            _effectPageObjs.Clear();

            for (int i = 0; i < _playerEffectObjs.Count; i++)
            {
                var parent = GetEffectPageContainer(i);
                _playerEffectObjs[i].transform.SetParent(parent, false);
            }

            int lastPage = _effectPageObjs.Count > 0 ? _effectPageObjs.Count - 1 : 0;
            _effectCurrentPage = Mathf.Clamp(_effectCurrentPage, 0, Mathf.Max(0, lastPage));
            ShowEffectPage(_effectCurrentPage);
            RefreshEffectNavButton();
        }

        /// <summary>
        /// 플레이어 버프/상태이상/파워카드 효과 아이콘을 추가하거나 수치를 갱신합니다.
        /// key가 같은 아이콘이 이미 있으면 수치만 업데이트합니다.
        /// value = 0 이하이면 아이콘을 제거합니다.
        /// </summary>
        public void SetPlayerEffect(string key, Sprite icon, int value, string tooltipName = null, string tooltipDesc = null)
        {
            if (value <= 0)
            {
                RemovePlayerEffect(key);
                return;
            }

            foreach (var obj in _playerEffectObjs)
            {
                if (obj == null) continue;
                var entry = obj.GetComponent<EffectIconEntry>();
                if (entry != null && entry.Key == key)
                {
                    entry.UpdateValue(value);
                    UpdatePlayerEffectTooltip(obj, tooltipName, tooltipDesc, icon);
                    return;
                }
            }

            if (playerEffectIconPrefab == null || playerEffectContainer == null) return;

            EnsureEffectLayout();

            int newIndex = _playerEffectObjs.Count;
            var parent = GetEffectPageContainer(newIndex);

            var newObj = Instantiate(playerEffectIconPrefab, parent);
            ResetRectTransform(newObj);

            var newEntry = newObj.GetComponent<EffectIconEntry>();
            if (newEntry == null) newEntry = newObj.AddComponent<EffectIconEntry>();
            newEntry.Initialize(key, icon, value);

            var trigger = newObj.GetComponent<TooltipTrigger>();
            if (trigger == null) trigger = newObj.AddComponent<TooltipTrigger>();
            if (!string.IsNullOrEmpty(tooltipDesc))
            {
                trigger.SetDynamicEntries(new List<TooltipEntry>
                {
                    TooltipEntry.CharacterUI(tooltipName ?? key, tooltipDesc, icon)
                });
            }

            _playerEffectObjs.Add(newObj);

            int targetPage = newIndex / EffectsPerPage;
            if (targetPage != _effectCurrentPage)
            {
                _effectCurrentPage = targetPage;
                ShowEffectPage(_effectCurrentPage);
            }
            RefreshEffectNavButton();
        }

        /// <summary>기존 아이콘의 값에 delta를 더합니다. 아이콘이 없으면 새로 생성합니다.</summary>
        public void AddPlayerEffectValue(string key, Sprite icon, int delta, string tooltipName = null, string tooltipDesc = null)
        {
            foreach (var obj in _playerEffectObjs)
            {
                if (obj == null) continue;
                var entry = obj.GetComponent<EffectIconEntry>();
                if (entry != null && entry.Key == key)
                {
                    entry.UpdateValue(entry.CurrentValue + delta);
                    UpdatePlayerEffectTooltip(obj, tooltipName, tooltipDesc, icon);
                    return;
                }
            }
            SetPlayerEffect(key, icon, delta, tooltipName, tooltipDesc);
        }

        /// <summary>지정 key의 아이콘이 이미 존재하는지 확인합니다.</summary>
        public bool HasPlayerEffect(string key)
        {
            foreach (var obj in _playerEffectObjs)
            {
                if (obj == null) continue;
                var entry = obj.GetComponent<EffectIconEntry>();
                if (entry != null && entry.Key == key) return true;
            }
            return false;
        }

        /// <summary>파워카드 효과를 아이콘으로 표시합니다. 매턴 발동하는 지속 효과용.</summary>
        public void SetPowerCardEffect(string key, Sprite icon, string tooltipName, string tooltipDesc, Vector2Int[] rangeOffsets = null, bool hidePlayerSprite = false)
        {
            SetPlayerEffect(key, icon, 1, tooltipName, tooltipDesc);

            if (rangeOffsets != null && rangeOffsets.Length > 0)
            {
                var obj = _playerEffectObjs[_playerEffectObjs.Count - 1];
                AddRangePreviewHover(obj, rangeOffsets, hidePlayerSprite);
            }
        }

        private void AddRangePreviewHover(GameObject obj, Vector2Int[] offsets, bool hidePlayerSprite = false)
        {
            var trigger = obj.GetComponent<EventTrigger>() ?? obj.AddComponent<EventTrigger>();

            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener(_ =>
            {
                if (boardController != null && GameManager.Instance != null && GameManager.Instance.IsInCombat)
                    boardController.ShowPowerEffectRangePreview(offsets, hidePlayerSprite);
            });
            trigger.triggers.Add(enterEntry);

            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener(_ =>
            {
                if (boardController != null)
                    boardController.HidePowerEffectRangePreview();
            });
            trigger.triggers.Add(exitEntry);
        }

        public void RemovePlayerEffect(string key)
        {
            for (int i = _playerEffectObjs.Count - 1; i >= 0; i--)
            {
                if (_playerEffectObjs[i] == null) { _playerEffectObjs.RemoveAt(i); continue; }
                var entry = _playerEffectObjs[i].GetComponent<EffectIconEntry>();
                if (entry != null && entry.Key == key)
                {
                    Destroy(_playerEffectObjs[i]);
                    _playerEffectObjs.RemoveAt(i);
                    RebuildEffectPages();
                    return;
                }
            }
        }

        public void ClearPlayerEffects()
        {
            foreach (var obj in _playerEffectObjs)
                if (obj != null) Destroy(obj);
            _playerEffectObjs.Clear();
            foreach (var page in _effectPageObjs)
                if (page != null) Destroy(page);
            _effectPageObjs.Clear();
            _effectNavButtons.Clear();
            _effectCurrentPage = 0;
            RefreshEffectNavButton();
        }

        private void UpdatePlayerEffectTooltip(GameObject obj, string name, string desc, Sprite icon)
        {
            if (string.IsNullOrEmpty(desc)) return;
            var trigger = obj.GetComponent<TooltipTrigger>();
            if (trigger == null) return;
            trigger.SetDynamicEntries(new List<TooltipEntry>
            {
                TooltipEntry.CharacterUI(name ?? "", desc, icon)
            });
        }

        private static void ResetRectTransform(GameObject obj)
        {
            var rt = obj.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.localPosition    = Vector3.zero;
            rt.localRotation    = Quaternion.identity;
            rt.localScale       = Vector3.one;
            rt.anchoredPosition = Vector2.zero;
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
    }

}
