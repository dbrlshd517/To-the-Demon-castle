using UnityEngine;
using UnityEngine.AddressableAssets;
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
        [SerializeField] private CardRewardPanel cardRewardPanel;
        [SerializeField] private CombatRewardPanel combatRewardPanel;

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
        [SerializeField] private MapController mapController;
        [SerializeField] private CombatController combatController;
        [SerializeField] private SettingsMenuController settingsMenuController;
        [SerializeField] private ShopController shopController;
        [SerializeField] private CardListController subCardListController;

        [Header("=== Audio ===")]
        [SerializeField] private AudioClip buttonClickSound;
        [SerializeField] private AudioClip openMapSound;
        [SerializeField] private AudioClip closeMapSound;

        [Header("=== Relic UI ===")]
        [SerializeField] private Transform relicIconContainer;
        [SerializeField] private GameObject relicIconPrefab;
        [SerializeField] private RelicPanelController relicPanelController;
        [Tooltip("유물 페이지 전환 버튼 (클릭마다 다음 페이지로 순환). 페이지가 2개 이상일 때만 표시됩니다.")]
        [SerializeField] private Button relicNavButton;

        [Header("=== Item UI ===")]
        [Tooltip("아이템 슬롯 컨테이너 (VerticalLayoutGroup — 위→아래 배치, relic과 동일)")]
        [SerializeField] private Transform itemSlotContainer;
        [SerializeField] private GameObject itemSlotPrefab;
        [Tooltip("아이템 클릭 시 표시할 Use/Discard 확인 패널 프리팹")]
        [SerializeField] private GameObject confirmUseItemPrefab;
        [Tooltip("아이템 타겟팅 중 CombatBoardCell 외 UI 입력 차단용 투명 패널 (Canvas 자식, CombatBoard보다 낮은 sibling 순서)")]
        [SerializeField] private GameObject itemTargetingBlocker;

        private ConfirmUseItemPanel activeConfirmPanel;
        private ItemSlotUI activeConfirmSlot;


        private AudioSource audioSource;
        private InGameState currentState = InGameState.Map;
        private bool isMapOpen = false;
        private bool isPaused = false;

        // ── 패널 스택 ──────────────────────────────────────────────
        private struct PanelEntry { public string id; public System.Action close; }
        private readonly LinkedList<PanelEntry> panelStack = new LinkedList<PanelEntry>();
        private const int MaxPanelStack = 3;

        // Events
        public event Action<InGameState> OnStateChanged;

        // Properties
        public InGameState CurrentState => currentState;
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
            RefreshRelicIcons();

            // 게임 시작 시 맵 표시
            ChangeState(InGameState.Map);
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            HandleInput();
        }

        #region Initialization

        private void InitializeUI()
        {
            HideAllPanels();
            UpdatePlayerInfo();

            // itemSlotContainer 레이아웃 설정 — relic과 동일: 컨테이너 중앙 기준, 위→아래 배치
            if (itemSlotContainer != null)
            {
                // HorizontalLayoutGroup이 남아 있으면 제거
                var old = itemSlotContainer.GetComponent<HorizontalLayoutGroup>();
                if (old != null) Destroy(old);

                var vlg = itemSlotContainer.GetComponent<VerticalLayoutGroup>()
                       ?? itemSlotContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment         = TextAnchor.UpperCenter;
                vlg.spacing                = 4f;
                vlg.childForceExpandWidth  = false;
                vlg.childForceExpandHeight = false;
                vlg.childControlWidth      = false;
                vlg.childControlHeight     = false;
            }

            // 씬 로드 시 이미 보유한 아이템 복원
            if (GameManager.Instance != null)
                foreach (var item in GameManager.Instance.Items)
                    AddItemSlot(item);
        }

        private void SetupButtonListeners()
        {
            mapButton?.onClick.AddListener(ToggleMap);
            deckButton?.onClick.AddListener(ToggleDeckViewer);
            settingsButton?.onClick.AddListener(ToggleSettings);
            backButton?.onClick.AddListener(OnBackClicked);
            relicNavButton?.onClick.AddListener(OnRelicNavClicked);
            if (relicNavButton != null) relicNavButton.gameObject.SetActive(false);

            // 모든 TopBar 버튼 — hover 시 아이템 타겟팅 취소
            AddItemCancelOnHover(mapButton);
            AddItemCancelOnHover(deckButton);
            AddItemCancelOnHover(settingsButton);
            AddItemCancelOnHover(backButton);
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
                if (combatController != null && combatController.IsItemTargetingActive)
                    combatController.CancelItemTargeting();
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
                GameManager.Instance.OnItemAdded  += AddItemSlot;
            }

            if (mapController != null)
            {
                mapController.OnNodeSelected += HandleNodeSelected;
            }

            if (combatController != null)
            {
                combatController.OnCombatEnded += HandleCombatEnded;
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
                GameManager.Instance.OnItemAdded  -= AddItemSlot;
            }

            if (mapController != null)
            {
                mapController.OnNodeSelected -= HandleNodeSelected;
            }

            if (combatController != null)
            {
                combatController.OnCombatEnded -= HandleCombatEnded;
            }
        }

        #endregion

        #region Input Handling

        private void HandleInput()
        {
            // ESC - 일시정지
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (isMapOpen && currentState != InGameState.Map)
                {
                    CloseMap();
                }
                else
                {
                    TogglePause();
                }
            }

            // M / Tab - 맵 토글
            if (Keyboard.current.mKey.wasPressedThisFrame ||
                Keyboard.current.tabKey.wasPressedThisFrame)
            {
                if (!isPaused)
                {
                    ToggleMap();
                }
            }

            // D - 덱 뷰어
            if (Keyboard.current.dKey.wasPressedThisFrame)
            {
                if (!isPaused)
                {
                    ToggleDeckViewer();
                }
            }
        }

        #endregion

        #region State Management

        public void ChangeState(InGameState newState)
        {
            if (currentState == newState && newState != InGameState.Map) return;

            InGameState previousState = currentState;
            currentState = newState;

            Debug.Log($"[InGameUI] 상태 변경: {previousState} -> {newState}");

            HideAllPanels();
            CloseMap();

            switch (newState)
            {
                case InGameState.Map:
                    ShowMapState();
                    break;
                case InGameState.Combat:
                    ShowCombatState();
                    break;
                case InGameState.Shop:
                    ShowShopState();
                    break;
                case InGameState.Rest:
                    ShowRestState();
                    break;
                case InGameState.Event:
                    ShowEventState();
                    break;
                case InGameState.Reward:
                    ShowRewardState();
                    break;
                case InGameState.GameOver:
                    ShowGameOverState();
                    break;
                case InGameState.Victory:
                    ShowVictoryState();
                    break;
            }

            UpdateBackButton();
            OnStateChanged?.Invoke(newState);
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
            if (cardRewardPanel != null) cardRewardPanel.gameObject.SetActive(false);
            if (combatRewardPanel != null) combatRewardPanel.gameObject.SetActive(false);
            if (shopController != null) shopController.gameObject.SetActive(false);
            if (subCardListController != null) subCardListController.gameObject.SetActive(false);
        }

        private void ShowMapState()
        {
            if (mapOverlay != null) mapOverlay.SetActive(true);
            isMapOpen = true;
            mapController?.ShowMap();
            GameManager.Instance?.ChangeState(GameState.Map);
            PushPanel("Map", CloseMap);
        }

        private void ShowCombatState()
        {
            if (combatUI != null) combatUI.SetActive(true);
            cardRewardPanel?.ResetForNewCombat(); // 이전 전투의 카드 목록 초기화
            GameManager.Instance?.ChangeState(GameState.Combat);
        }

        private void ShowShopState()
        {
            if (combatUI != null) combatUI.SetActive(true);
            combatController?.SpawnMerchant();
            GameManager.Instance?.ChangeState(GameState.Shop);
        }

        private void ShowRestState()
        {
            if (combatUI != null) combatUI.SetActive(true);
            combatController?.SpawnRestArea();
            GameManager.Instance?.ChangeState(GameState.Rest);
        }

        private void ShowEventState()
        {
            if (eventPanel != null) eventPanel.SetActive(true);
            GameManager.Instance?.ChangeState(GameState.Event);
        }

        private void ShowRewardState()
        {
            backButton?.gameObject.SetActive(false);
            GameManager.Instance?.ChangeState(GameState.Reward);
        }

        private void ShowGameOverState()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            GameManager.Instance?.ChangeState(GameState.GameOver);
        }

        private void ShowVictoryState()
        {
            if (victoryPanel != null) victoryPanel.SetActive(true);
            GameManager.Instance?.ChangeState(GameState.Victory);
        }

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
            if (isPaused) return;

            CloseSettings();
            if (deckViewerPanel != null) deckViewerPanel.SetActive(false);

            PlaySound(openMapSound);
            if (mapOverlay != null) mapOverlay.SetActive(true);
            isMapOpen = true;
            mapController?.ShowMap();
            PushPanel("Map", CloseMap);

            if (currentState == InGameState.Combat)
                combatController?.PauseCombat();
        }

        public void CloseMap()
        {
            PlaySound(closeMapSound);
            if (mapOverlay != null) mapOverlay.SetActive(false);
            isMapOpen = false;
            RemoveFromStack("Map");
            UpdateBackButton();

            if (currentState == InGameState.Combat)
                combatController?.ResumeCombat();
        }

        #endregion

        #region Node Selection Handler

        private void HandleNodeSelected(NodeType nodeType, int floor)
        {
            Debug.Log($"[InGameUI] 노드 선택: {nodeType} (층: {floor})");

            GameManager.Instance?.SetFloor(floor + 1);
            UpdateFloorDisplay(floor + 1);

            switch (nodeType)
            {
                case NodeType.Combat:
                    GameManager.Instance?.SetCurrentEncounterElite(false);
                    StartCombat();
                    break;

                case NodeType.Elite:
                    GameManager.Instance?.SetCurrentEncounterElite(true);
                    StartCombat();
                    break;

                case NodeType.Boss:
                    GameManager.Instance?.SetCurrentEncounterBoss(true);
                    StartCombat();
                    break;

                case NodeType.Rest:
                    ChangeState(InGameState.Rest);
                    break;

                case NodeType.Shop:
                    ChangeState(InGameState.Shop);
                    break;

                case NodeType.Treasure:
                    HandleTreasure();
                    break;

                case NodeType.Event:
                    ChangeState(InGameState.Event);
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
            combatController?.SetEncounter(encounter);
            ChangeState(InGameState.Combat);
            combatController?.StartNewCombat();
        }

        private void HandleTreasure()
        {
            // 보물 획득 (유물 시스템 구현 전에는 골드 지급)
            int goldAmount = UnityEngine.Random.Range(50, 100);
            GameManager.Instance?.ModifyGold(goldAmount);
            Debug.Log($"[InGameUI] 보물 발견! +{goldAmount} 골드");

            // 맵으로 돌아가기
            ChangeState(InGameState.Map);
        }

        #endregion

        #region Combat End Handler

        private void HandleCombatEnded(bool victory)
        {
            if (victory)
            {
                int goldReward = CalculateGoldReward();
                GameManager.Instance?.AddEnemyDefeated();
                Debug.Log($"[InGameUI] 전투 승리! 골드 보상 대기 중: +{goldReward}");

                RelicData bonusRelic = null;
                if (GameManager.Instance != null)
                {
                    if (GameManager.Instance.IsBossEncounter)
                        bonusRelic = PickBossRelicReward();
                    else if (GameManager.Instance.GameDifficulty == 4)
                        bonusRelic = PickRelicReward();
                }

                OpenCombatReward(goldReward, offerCard: true, bonusRelic);
            }
            else
            {
                Debug.Log("[InGameUI] 전투 패배!");
                ChangeState(InGameState.GameOver);
            }
        }

        private void OpenCombatReward(int gold, bool offerCard, RelicData bonusRelic)
        {
            if (combatRewardPanel != null)
            {
                ChangeState(InGameState.Reward);
                combatRewardPanel.Open(gold, offerCard, bonusRelic);
            }
            else
            {
                Debug.LogWarning("[InGameUI] CombatRewardPanel 미연결 — 맵으로 이동");
                OpenMap();
            }
        }

        /// <summary>보스 처치 보상 유물을 RelicRegistry에서 무작위로 선택합니다.</summary>
        private RelicData PickBossRelicReward()
        {
            RelicLibrary.RegisterAll();
            ItemLibrary.RegisterAll();
            var pool = RelicRegistry.GetByType(bossRelic: true);

            // 이미 보유한 유물 제외
            if (GameManager.Instance != null)
            {
                var owned = new System.Collections.Generic.HashSet<int>();
                foreach (var r in GameManager.Instance.Relics)
                    if (r?.Data != null) owned.Add(r.Data.relicCode);
                pool.RemoveAll(r => owned.Contains(r.relicCode));
            }

            if (pool.Count == 0) return null;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        private int CalculateGoldReward()
        {
            if (GameManager.Instance == null) return UnityEngine.Random.Range(10, 21);
            return GameManager.Instance.GameDifficulty switch
            {
                1 => UnityEngine.Random.Range(10, 21),
                2 => UnityEngine.Random.Range(20, 31),
                3 => UnityEngine.Random.Range(40, 51),
                _ => 0
            };
        }

        private RelicData PickRelicReward()
        {
            RelicLibrary.RegisterAll();
            var pool = RelicRegistry.GetByType(bossRelic: false);
            if (GameManager.Instance != null)
            {
                var owned = new System.Collections.Generic.HashSet<int>();
                foreach (var r in GameManager.Instance.Relics)
                    if (r?.Data != null) owned.Add(r.Data.relicCode);
                pool.RemoveAll(r => owned.Contains(r.relicCode));
            }
            if (pool.Count == 0) return null;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        #endregion

        #region Pause

        public void ToggleSettings()
        {
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

            if (panelStack.Count >= MaxPanelStack)
            {
                var oldest = panelStack.First;
                panelStack.RemoveFirst();
                oldest.Value.close?.Invoke(); // 가장 오래된 패널 닫기
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

        // 열린 패널이 있을 때만 backButton 활성화
        private void UpdateBackButton()
        {
            if (backButton == null) return;
            backButton.gameObject.SetActive(panelStack.Count > 0);
        }

        public void TogglePause()
        {
            PlaySound(buttonClickSound);

            isPaused = !isPaused;

            if (isPaused)
            {
                Time.timeScale = 0f;
                GameManager.Instance?.ChangeState(GameState.Paused);
            }
            else
            {
                Time.timeScale = 1f;
                // 이전 상태로 복귀
                GameManager.Instance?.ChangeState(currentState switch
                {
                    InGameState.Combat => GameState.Combat,
                    InGameState.Map => GameState.Map,
                    InGameState.Shop => GameState.Shop,
                    _ => GameState.InGame
                });
            }
        }

        public void ResumeGame()
        {
            if (isPaused)
            {
                TogglePause();
            }
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
            GameManager.Instance?.ChangeState(GameState.MainMenu);
            SceneLoader.Instance?.LoadScene("MainMenu");
        }

        #endregion

        #region Deck Viewer

        public void ToggleDeckViewer()
        {
            PlaySound(buttonClickSound);
            if (IsSettingsOpen)
            {
                CloseSettings();
                if (deckViewerPanel != null && !deckViewerPanel.activeSelf)
                {
                    if (isMapOpen) CloseMap();
                    deckViewerPanel.SetActive(true);
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
                PushPanel("DeckViewer", CloseDeckViewer);
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
        private const int RelicsPerCol  = 6;   // 열당 최대 유물 수
        private const int RelicsPerPage = 12;  // 페이지당 최대 유물 수 (2열 × 6행)

        // ── 페이지 상태 ────────────────────────────────────────────
        private readonly List<GameObject> _relicPageObjs = new List<GameObject>();
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

        /// <summary>유물 획득 시 2열 페이지 레이아웃에 아이콘을 추가합니다.
        /// 1~6번째: 1열 / 7~12번째: 2열 / 13번째~: 새 페이지 생성 후 반복</summary>
        private void AddRelicIcon(RelicEffect relic)
        {
            if (relicIconContainer == null || relicIconPrefab == null || relic?.Data == null) return;

            EnsureRelicLayout();

            int pageIndex  = _totalRelicCount / RelicsPerPage;
            int slotInPage = _totalRelicCount % RelicsPerPage;
            int colIndex   = slotInPage / RelicsPerCol; // 0: 왼쪽 열, 1: 오른쪽 열

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
                    sprite = Addressables.LoadAssetAsync<Sprite>($"Sprites/Relic/{relic.Data.relicCode}").WaitForCompletion();
                if (sprite != null)
                    img.sprite = sprite;
                else
                    img.color = new Color(0.6f, 0.4f, 0.8f, 1f);
                img.enabled = true;
            }

            var btn = iconObj.GetComponent<Button>();
            btn?.onClick.AddListener(() => relicPanelController?.Toggle());

            var trigger = iconObj.GetComponent<DeckRoguelike.UI.TooltipTrigger>();
            trigger?.SetRelicCode(relic.Data.relicCode);

            // prefab의 모든 TMP 텍스트를 기본 비활성화
            var allTexts = iconObj.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            foreach (var t in allTexts)
                t.gameObject.SetActive(false);

            // 카운터가 있는 유물(MoveRelic 등)만 텍스트 활성화
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

        /// <summary>HorizontalLayoutGroup + 2열 컨테이너로 구성된 페이지 GO를 생성합니다.</summary>
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

            // 아이콘 프리팹 너비 (HLG가 처음부터 올바른 X에 배치하도록 sizeDelta 선설정)
            float iconW = 0f;
            if (relicIconPrefab != null)
            {
                var prefabRect = relicIconPrefab.GetComponent<RectTransform>();
                if (prefabRect != null)
                    iconW = prefabRect.sizeDelta.x > 0f ? prefabRect.sizeDelta.x : prefabRect.rect.width;
            }

            // 2개 열 컨테이너 생성
            for (int c = 0; c < 2; c++)
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

        /// <summary>네비게이션 버튼의 표시 여부를 갱신합니다. (페이지 2개 이상일 때만 표시)</summary>
        private void RefreshRelicNavButtons()
        {
            if (relicNavButton != null)
            {
                relicNavButton.gameObject.SetActive(_relicPageObjs.Count > 1);
                if (_relicPageObjs.Count > 1)
                    StartCoroutine(AlignRelicNavButtonX());
            }
        }

        /// <summary>Col_0과 Col_1의 월드 X 중앙값을 relicNavButton의 anchoredPosition.x에 적용합니다.</summary>
        private IEnumerator AlignRelicNavButtonX()
        {
            if (relicNavButton == null || _relicPageObjs.Count == 0) yield break;

            yield return null;
            Canvas.ForceUpdateCanvases();

            var page = _relicPageObjs[_relicCurrentPage];
            if (page == null || page.transform.childCount < 2) yield break;

            var col0Rect = page.transform.GetChild(0).GetComponent<RectTransform>();
            var col1Rect = page.transform.GetChild(1).GetComponent<RectTransform>();
            if (col0Rect == null || col1Rect == null) yield break;

            // 각 열의 월드 X 중앙값
            float midWorldX = (col0Rect.position.x + col1Rect.position.x) * 0.5f;

            var navRect    = relicNavButton.GetComponent<RectTransform>();
            var parentRect = navRect.parent as RectTransform;
            if (parentRect == null) yield break;

            var canvas = navRect.GetComponentInParent<Canvas>();
            Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                           ? canvas.worldCamera : null;

            // 월드 X → 스크린 → navButton 부모 로컬
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCam,
                new Vector3(midWorldX, col0Rect.position.y, col0Rect.position.z));

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, screenPoint, uiCam, out Vector2 localPos))
            {
                navRect.anchoredPosition = new Vector2(localPos.x, navRect.anchoredPosition.y);
            }
        }

        /// <summary>아이템 획득 시 아이템 슬롯 컨테이너에 아이콘 슬롯을 추가합니다.</summary>
        private void AddItemSlot(DeckRoguelike.Item.ItemData item)
        {
            if (itemSlotContainer == null || itemSlotPrefab == null || item == null) return;

            var slotObj = Instantiate(itemSlotPrefab, itemSlotContainer);
            slotObj.SetActive(true);
            slotObj.name = $"ItemSlot_{item.itemCode}_{slotObj.GetInstanceID()}";

            // ItemSlotUI 컴포넌트 부착 및 초기화
            var slotUI = slotObj.GetComponent<ItemSlotUI>();
            if (slotUI == null) slotUI = slotObj.AddComponent<ItemSlotUI>();
            slotUI.Initialize(item);

            // Layout 강제 갱신 (여러 슬롯이 올바르게 배치되도록)
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(
                itemSlotContainer as RectTransform ?? itemSlotContainer.GetComponent<RectTransform>());
        }

        /// <summary>아이템 슬롯 클릭 시 Use/Discard 확인 패널을 표시합니다.</summary>
        /// <summary>해당 슬롯에 대한 패널이 이미 열려있으면 true.</summary>
        public bool HasActiveConfirmPanelFor(ItemSlotUI slot) => activeConfirmPanel != null && activeConfirmSlot == slot;

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
        public CombatController GetCombatController() => combatController;

        /// <summary>아이템 타겟팅 blocker를 활성/비활성합니다. CombatController에서 호출합니다.</summary>
        public void SetItemTargetingBlocker(bool active)
        {
            if (itemTargetingBlocker != null)
                itemTargetingBlocker.SetActive(active);
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

            // 난이도 아이콘 (1=easy 2=normal 3=hard 4=hell)
            if (levelIcon != null)
            {
                string[] levelNames = { "Level_easy", "Level_nomal", "Level_hard", "Level_hell" };
                int idx = Mathf.Clamp(GameManager.Instance.GameDifficulty - 1, 0, levelNames.Length - 1);
                Sprite sp = Addressables.LoadAssetAsync<Sprite>($"Sprites/Common/Topbar/{levelNames[idx]}").WaitForCompletion();
                if (sp != null) levelIcon.sprite = sp;
            }

            // 액트 아이콘 (act_1, act_2, ...)
            if (actIcon != null)
            {
                Sprite sp = Addressables.LoadAssetAsync<Sprite>($"Sprites/Common/Topbar/act_{GameManager.Instance.CurrentAct}").WaitForCompletion();
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
        /// 상점 패널 열기. 상인 클릭 시 CombatController에서 호출됩니다.
        /// onLeave : Leave 버튼 클릭 시 실행될 콜백 (보드 정리 + OnNodeComplete)
        /// </summary>
        public void OpenShopPanel(System.Action onLeave = null)
        {
            if (shopController == null) return;
            shopController.gameObject.SetActive(true);
            shopController.OpenShop(onLeave);
            UpdateBackButton();
        }

        /// <summary>
        /// 카드 보상 선택 패널 열기. 어디서든 호출 가능.
        /// </summary>
        public void OpenCardReward(System.Action<CardData> callback)
        {
            cardRewardPanel?.Open(callback);
        }

        /// <summary>
        /// 상점에서 카드 구매 시 사용. 구매한 카드 1장을 CardRewardPanel에 표시합니다.
        /// </summary>
        public void OpenCardRewardWithCard(CardData card, System.Action<CardData> callback)
        {
            cardRewardPanel?.OpenWithSpecificCard(card, callback);
        }

        public void CloseCardReward()
        {
            if (cardRewardPanel != null) cardRewardPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// 아이템 효과에서 지정 카드 목록을 띄워 1장 선택하게 합니다.
        /// 선택 완료 또는 취소 시 callback이 호출되고 패널이 자동으로 닫힙니다.
        /// </summary>
        public void OpenItemCardSelection(List<CardData> cards, System.Action<CardData> callback)
        {
            if (cardRewardPanel == null) { callback?.Invoke(null); return; }
            cardRewardPanel.OpenWithCards(cards, picked =>
            {
                cardRewardPanel.gameObject.SetActive(false);
                // 스킵 버튼 복원 (다음 정상 Open을 위해)
                callback?.Invoke(picked);
            });
        }

        public void OpenRestCardList(RestCardMode mode, CardConfirmPanel confirmPanel, System.Action onFinish = null)
        {
            if (subCardListController == null) { onFinish?.Invoke(); return; }

            subCardListController.gameObject.SetActive(true);
            subCardListController.Setup(mode, confirmPanel, onFinish);
            PushPanel("RestCardList", CloseRestCardList);
        }

        public void CloseRestCardList()
        {
            if (subCardListController != null) subCardListController.gameObject.SetActive(false);
            RemoveFromStack("RestCardList");
            UpdateBackButton();
        }

        /// <summary>
        /// 보상 선택 완료 후 맵으로 돌아가기
        /// </summary>
        public void OnRewardComplete()
        {
            ChangeState(InGameState.Map);
        }

        /// <summary>
        /// 휴식/상점/이벤트 완료 후 맵으로 돌아가기
        /// </summary>
        public void OnNodeComplete()
        {
            ChangeState(InGameState.Map);
        }

        /// <summary>
        /// 보스 클리어 시 승리
        /// </summary>
        public void OnBossDefeated()
        {
            // 다음 Act로 가거나 게임 클리어
            if (GameManager.Instance != null && GameManager.Instance.CurrentAct >= 3)
            {
                ChangeState(InGameState.Victory);
            }
            else
            {
                // 다음 Act
                GameManager.Instance?.AdvanceFloor();
                mapController?.GenerateNewMap();
                ChangeState(InGameState.Map);
            }
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

    /// <summary>
    /// InGame 씬 내부 상태
    /// </summary>
    public enum InGameState
    {
        Map,
        Combat,
        Shop,
        Rest,
        Event,
        Reward,
        GameOver,
        Victory
    }
}
