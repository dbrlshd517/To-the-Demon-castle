using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using DeckRoguelike.Core;
using DeckRoguelike.Cards;
using DeckRoguelike.Combat;
using DeckRoguelike.Relic;
using DeckRoguelike.Item;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 전투 UI 및 로직 컨트롤러
    ///
    /// [그리드 타겟팅 흐름]
    /// 1. 카드 클릭 → TryPlayCard()
    /// 2. 첫 번째 효과의 targeting이 Enemy/Any/Ally → EnterTargetingMode() : 범위 하이라이트
    ///    그 외 (Self/Random/All) → ExecuteCard() 즉시 실행
    /// 3. 플레이어가 셀 클릭 → HandleCellClicked() → ExecuteCard(selectedPos)
    /// 4. 턴 종료 / ESC 클릭 → CancelTargeting()
    ///
    /// [효과 처리]
    /// - 각 효과는 자신의 targeting/rangeOffsets로 대상을 결정
    /// - 셀 선택 UI는 effects[0]의 targeting 기준
    /// - 선택된 좌표(selectedPos)는 모든 효과에 공유됨
    /// - rangeOffsets 비어있으면 무제한 사거리 (적용 가능한 대상에만 하이라이트)
    /// </summary>
    public class CombatController : MonoBehaviour
    {
        [Header("=== Combat Settings ===")]
        [SerializeField] private int startingHandSize = 5;
        [SerializeField] private int baseEnergy = 3;
        [SerializeField] private float enemyTurnDelay = 1f;

        [Header("=== Range Preview Size ===")]
        [SerializeField] private float playerRangePreviewScale = 1f;
        [SerializeField] private float enemyRangePreviewScale  = 1f;

        [Header("=== Energy UI ===")]
        [SerializeField] private TextMeshProUGUI energyText;
        [SerializeField] private Image energyOrb;

        [Header("=== Turn UI ===")]
        [SerializeField] private Button endTurnButton;
        [SerializeField] private TextMeshProUGUI turnIndicatorText;
        [SerializeField] private GameObject turnIndicator;

        [Header("=== Deck ===")]
        [SerializeField] private DeckManager deckManager;

        [Header("=== Hand Area ===")]
        [SerializeField] private Transform handContainer;
        [SerializeField] private GameObject cardPrefab;
        [Tooltip("카드 간 가로 간격 (px). 카드 수에 관계없이 고정 간격으로 배치됩니다.")]
        [SerializeField] private float handCardSpacing = 120f;
        [Tooltip("가장자리 카드가 중앙보다 내려가는 양 (px). 클수록 부채꼴이 뚜렷해짐")]
        [SerializeField] private float handArcDrop = 60f;
        [Tooltip("손패 카드 생성 기준 높이. 화면 하단(0)에서 위로 올리는 픽셀 수. 0=카드 바닥이 화면 최하단.")]
        [SerializeField] private float handYOffset = 0f;
        [Tooltip("sticky 상태 카드가 이 화면 Y 픽셀 이상으로 올라가면 카드가 작아집니다. 보드 셀 하단 높이와 맞추세요.")]
        [SerializeField] private float cardShrinkBoardScreenY = 300f;

        [Header("=== Deck Pile Buttons ===")]
        [SerializeField] private Button drawPileButton;
        [SerializeField] private TextMeshProUGUI drawPileCount;
        [SerializeField] private Button discardPileButton;
        [SerializeField] private TextMeshProUGUI discardPileCount;

        [Header("=== Combat Board (4열 x 3행) ===")]
        [Tooltip("combatBoard의 부모 패널 — 비전투 플레이어 이미지를 여기에 생성")]
        [SerializeField] private RectTransform combatPanel;
        [SerializeField] private RectTransform combatBoard;
        [SerializeField] private GameObject cellPrefab;          // CombatBoardCell 프리팹 (없으면 자동 생성)
        [SerializeField] private int boardCols = 4;
        [SerializeField] private int boardRows = 3;
        [SerializeField] private float cellWidth  = 150f;
        [SerializeField] private float cellHeight = 150f;
        [Tooltip("그리드 세로 위치. X는 무시되며 가로는 combatBoard 기준 자동 중앙 정렬됩니다.\nY: 화면 좌하단(0,0) 기준 픽셀 좌표로 그리드 하단 위치를 설정합니다.")]
        [SerializeField] private Vector2 boardOffset = Vector2.zero;
        [SerializeField] private Vector2Int playerSpawnCell = new Vector2Int(0, 1); // 전투마다 초기화되는 현재 위치
        [SerializeField] private Vector2Int playerInitialCell = new Vector2Int(0, 1); // 고정 스폰 위치 (Inspector에서 설정)
        [SerializeField] private Vector2Int[] enemySpawnCells =
        {
            new Vector2Int(3, 0),
            new Vector2Int(3, 1),
            new Vector2Int(3, 2),
        };

        [Header("=== Prefabs ===")]
        [Tooltip("플레이어·적·아군 공통 유닛 프리팹. AllyData.allyPrefab이 있으면 아군은 그것을 우선 사용합니다.")]
        [SerializeField] private GameObject unitPrefab;
        [Tooltip("플레이어를 셀 중앙에서 얼마나 오프셋할지 (예: (0, 20) 으로 위로 올림)")]
        [SerializeField] private Vector2 playerCellOffset = new Vector2(0f, 20f);

        [Header("=== Combat HUD (전투 종료 시 비활성화) ===")]
        [Tooltip("전투 승리 후 숨길 UI 요소들 (전투판 제외). 예: 손패, 에너지, 턴종료버튼 등")]
        [SerializeField] private GameObject[] combatHudElements;

        [Header("=== Shop / Rest 공통 ===")]
        [Tooltip("상점/휴식 상태에서만 표시되는 떠나기 버튼 (맵 패널 열기)")]
        [SerializeField] private Button leaveButton;

        [Header("=== Merchant Shop ===")]
        [Tooltip("상인 캐릭터 이미지 스프라이트 — 클릭 시 ShopPanel을 엽니다")]
        [SerializeField] private Sprite merchantSprite;
        [Tooltip("상인 이미지 위치 (combatPanel 기준 anchoredPosition)")]
        [SerializeField] private Vector2 merchantPos  = new Vector2(75f, 0f);
        [Tooltip("상인 이미지 크기")]
        [SerializeField] private Vector2 merchantSize = new Vector2(112f, 112f);

        [Header("=== Rest Area ===")]
        [Tooltip("강화 확인 패널")]
        [SerializeField] private CardConfirmPanel cardConfirmPanel;
        [Tooltip("최대 HP 대비 회복 비율 (0.3 = 30%)")]
        [SerializeField] private float restHealPercent = 0.3f;
        [Tooltip("휴식 버튼 공통 프리팹 (Button + Image 포함)")]
        [SerializeField] private GameObject restButtonPrefab;
        [Tooltip("휴식 버튼 스프라이트 (0:휴식 1:강화 2:제거 3:발굴)")]
        [SerializeField] private Sprite[] restButtonSprites = new Sprite[4];
        [Tooltip("버튼 위치 — 0:휴식 1:강화 2:제거 3:발굴 (combatPanel 기준)")]
        [SerializeField] private Vector2[] restButtonPositions = new Vector2[]
        {
            new Vector2(-150f,  60f),
            new Vector2( -50f,  60f),
            new Vector2(  50f,  60f),
            new Vector2( 150f,  60f),
        };
        [Tooltip("버튼 크기")]
        [SerializeField] private Vector2 restButtonSize = new Vector2(90f, 90f);

        [Header("=== 비전투 플레이어 이미지 ===")]
        [Tooltip("플레이어 이미지 anchoredPosition (격자 1,1 기준: -75, 0)")]
        [SerializeField] private Vector2 playerNonCombatPos  = new Vector2(-75f, 0f);
        [Tooltip("플레이어 이미지 크기")]
        [SerializeField] private Vector2 playerNonCombatSize = new Vector2(112f, 112f);

        [Header("=== Effect Icon Sprites ===")]
        [Tooltip("힘(Strength) 버프 아이콘")]
        [SerializeField] private Sprite strengthIcon;
        [Tooltip("민첩(Dexterity) 버프 아이콘")]
        [SerializeField] private Sprite dexterityIcon;

        [Header("=== Action Icon Sprites (Enemy/Ally 행동 아이콘) ===")]
        [Tooltip("공격 행동 아이콘 (actionIconPrefabs[0] 에 표시)")]
        [SerializeField] private Sprite attackActionIcon;
        [Tooltip("이동 행동 아이콘 (actionIconPrefabs[1] 에 표시)")]
        [SerializeField] private Sprite moveActionIcon;
        [Tooltip("스킬 행동 아이콘 (actionIconPrefabs[2] 에 표시)")]
        [SerializeField] private Sprite skillActionIcon;

        [Header("=== Status Effect Icons ===")]
        [SerializeField] private Sprite fireIcon;
        [SerializeField] private Sprite stunIcon;
        [SerializeField] private Sprite freezeIcon;

        // 플레이어 프리팹의 PlayerController (생성 후 자동 참조)
        private DeckRoguelike.Combat.UnitUI unitUI;

        [Header("=== Audio ===")]
        [SerializeField] private AudioClip attackSound;
        [SerializeField] private AudioClip blockSound;
        [SerializeField] private AudioClip drawCardSound;
        [SerializeField] private AudioClip endTurnSound;
        [SerializeField] private AudioClip victorySound;
        [SerializeField] private AudioClip defeatSound;

        [Header("=== Card Animation ===")]
        [SerializeField] private float cardDrawDuration = 0.3f;
        [SerializeField] private float cardDiscardDuration = 0.25f;
        [SerializeField] private float drawStaggerDelay = 0.1f;

        // Combat State
        private CombatState combatState = CombatState.NotStarted;
        private int currentEnergy;
        private int maxEnergy;
        private int _playingHandIndex = -1; // ApplyCardEffects 실행 중 현재 플레이 카드 인덱스
        private int playerBlock;
        private int playerStrength;
        private int playerDexterity;
        private int turnNumber;
        private bool isPaused;

        // 카드 타입 봉인: CardType → 남은 턴 수
        private Dictionary<CardType, int> cardTypeRestrictions = new Dictionary<CardType, int>();

        // ── 유물 관련 런타임 상태 ─────────────────────────────────────────
        private int  _playerMoveCount   = 0; // 현재 전투 누적 이동 횟수 (107 이동 유물)
        private int  _bonusHandSize     = 0; // 턴 시작 추가 드로우 (902 유물)
        private int  _cardCostReduction = 0; // 이번 턴 카드 비용 감소 (107 유물)
        private int  _upgradesRemaining = 0; // 휴식 강화 남은 횟수 (301 유물)

        // ── 파워 카드 지속 효과 ────────────────────────────────────────────
        private readonly List<CombatPowerEffect> _activePowers = new List<CombatPowerEffect>();
        private bool _noCardHpLoss = false; // no_loseHp: 카드 효과로 HP를 잃지 않음
        private bool _noExhaust    = false; // no_Exhausts: 카드가 소멸되지 않고 버림 더미로

        // Deck
        private List<CardData> hand = new List<CardData>();
        private List<GameObject> handCardObjects = new List<GameObject>();
        private List<GameObject> hoverPreviewObjects = new List<GameObject>();
        private List<GameObject> unitPreviewObjects = new List<GameObject>();

        // Grid
        private CombatBoardCell[,] grid;
        private GameObject playerObject;
        private GameObject nonCombatPlayerObj;

        // Player direction (true = right, false = left)
        private bool facingRight = true;
        public bool FacingRight => facingRight;

        // Enemies
        private List<EnemyInstance> enemies = new List<EnemyInstance>();
        private EnemyEncounterData currentEncounterData;

        // Allies
        private List<AllyInstance> allies = new List<AllyInstance>();

        // Targeting
        private CardData pendingCard;
        private int pendingCardIndex = -1;

        private AudioSource audioSource;

        // ── 런타임 로드 스프라이트 (Addressables) — 플레이어·적·아군 공통 ──
        private Sprite attackRangeSprite;
        private Sprite moveRangeSprite;
        private Sprite skillRangeSprite;
        private Sprite skillCollapsedRangeSprite;

        // ── 아이템 효과 상태 ──────────────────────────────────────────────
        private List<StatusEntry> playerStatusEffects  = new List<StatusEntry>();
        private int    nextAttackDamageMultiplier = 1;    // 분노의 포션
        private bool   nextCardDoublePlay         = false; // 재사용 포션
        private float  outgoingDamageMultiplier   = 1f;   // 철가면
        private float  incomingDamageMultiplier   = 1f;   // 철가면
        private int    tempStrengthFromItem       = 0;    // 초코바 (턴 종료 시 회수)
        private int    dodgeCharges               = 0;    // 군번줄
        private ItemData revivePassiveItem        = null;  // 소생의 팬던트
        private float  reviveHealPercent          = 0.3f;
        // 아이템 이동/순간이동 모드
        private bool   isItemMoveMode             = false;
        private bool   isItemTeleportMode         = false;
        // 아이템 타겟팅 모드 (적 선택 / 영역 선택)
        private bool   isItemEnemyTargetingMode   = false;
        private bool   isItemAreaTargetingMode    = false;
        private System.Action<EnemyInstance>  _itemEnemyCallback;
        private System.Action<Vector2Int>     _itemAreaCallback;
        private System.Action                 _itemTargetCancelCallback;
        private System.Action                 _itemConfirmedUseCallback;
        private ItemSlotUI                    _pendingItemSlot;
        private List<GameObject>              itemHoverPreviewObjects = new List<GameObject>();

        public event Action<bool> OnCombatEnded;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnHPChanged += OnPlayerHPChanged;
        }

        private void Update()
        {
            // 아이템 타겟팅 중 우클릭 → 취소
            if (IsItemTargetingActive)
            {
                if (UnityEngine.InputSystem.Mouse.current != null &&
                    UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame)
                {
                    CancelItemTargeting();
                }
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnHPChanged -= OnPlayerHPChanged;
        }

        private void OnPlayerHPChanged(int current, int max)
        {
            unitUI?.UpdateHP(current, max);
        }

        private void Start()
        {
            endTurnButton?.onClick.AddListener(OnEndTurnClicked);
            drawPileButton?.onClick.AddListener(ShowDrawPile);
            discardPileButton?.onClick.AddListener(ShowDiscardPile);

            // 전투 HUD 버튼 — hover 시 아이템 타겟팅 취소
            AddItemCancelOnHover(endTurnButton);
            AddItemCancelOnHover(drawPileButton);
            AddItemCancelOnHover(discardPileButton);

            CardEffectLibrary.RegisterAll();
            EnemyBehaviorLibrary.RegisterAll();
            AllyBehaviorLibrary.RegisterAll();
            RelicLibrary.RegisterAll();
            ItemLibrary.RegisterAll();

            LoadRangePreviewSprites();
        }

        private void LoadRangePreviewSprites()
        {
            // GameResources/Sprites/Combat/Rangepreview/ 폴더의 1·2·3 순서: attack·move·skill
            attackRangeSprite        = LoadSpriteSafe("Sprites/Combat/Rangepreview/1");
            moveRangeSprite          = LoadSpriteSafe("Sprites/Combat/Rangepreview/2");
            skillRangeSprite         = LoadSpriteSafe("Sprites/Combat/Rangepreview/3");
            skillCollapsedRangeSprite = LoadSpriteSafe("Sprites/Combat/Rangepreview/4");
        }

        private static Sprite LoadSpriteSafe(string address)
        {
            try { return Addressables.LoadAssetAsync<Sprite>(address).WaitForCompletion(); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CombatController] 스프라이트 로드 실패 '{address}': {e.Message}");
                return null;
            }
        }


        #region Combat Flow

        /// <summary>전투 시작 전 인카운터 설정 (GameManager.SelectEncounter() 결과를 전달)</summary>
        public void SetEncounter(EnemyEncounterData encounterData)
        {
            currentEncounterData = encounterData;
        }

        public void StartNewCombat()
        {
            Debug.Log("[CombatController] 전투 시작");
            CleanupNonCombatVisuals();

            combatState    = CombatState.Starting;
            turnNumber     = 0;
            playerBlock    = 0;
            playerStrength  = 0;
            playerDexterity = 0;
            _playerMoveCount   = 0;
            _bonusHandSize     = 0;
            _cardCostReduction = 0;
            _activePowers.Clear();
            _noCardHpLoss = false;
            _noExhaust    = false;
            maxEnergy      = GameManager.Instance != null ? GameManager.Instance.BaseEnergy : baseEnergy;
            currentEnergy  = maxEnergy;
            pendingCard     = null;
            pendingCardIndex = -1;
            allies.Clear();
            facingRight  = true;

            // 아이템 상태 초기화
            playerStatusEffects.Clear();
            nextAttackDamageMultiplier = 1;
            nextCardDoublePlay         = false;
            outgoingDamageMultiplier   = 1f;
            incomingDamageMultiplier   = 1f;
            tempStrengthFromItem       = 0;
            dodgeCharges               = 0;
            revivePassiveItem          = null;
            isItemMoveMode             = false;
            isItemTeleportMode         = false;
            isItemEnemyTargetingMode   = false;
            isItemAreaTargetingMode    = false;
            _itemEnemyCallback         = null;
            _itemAreaCallback          = null;
            _itemTargetCancelCallback  = null;
            _itemConfirmedUseCallback  = null;
            _pendingItemSlot           = null;
            ClearItemHoverPreview();

            int spawnX = (boardCols % 2 == 1) ? boardCols / 2 : boardCols / 2 - 1;
            playerSpawnCell = new Vector2Int(spawnX, playerInitialCell.y);

            SetCombatHudActive(true);
            SetLeaveButtonActive(false); // 전투 중 떠나기 버튼 비활성화
            InitializeDeck();
            InitializeBoard();
            PlacePlayer();
            unitUI?.ClearEffects(); // 이전 전투 이펙트 아이콘 초기화
            SpawnEnemies();

            FireRelicHook((r, ctx) => r.OnCombatStart(ctx));

            UpdateEnergyDisplay();
            UpdateBlockDisplay();
            UpdatePileCounters();

            StartPlayerTurn();
        }

        private void InitializeDeck()
        {
            hand.Clear();
            ClearHandVisuals();
            if (deckManager != null)
                deckManager.ResetForNewCombat();
            else
                Debug.LogWarning("[CombatController] DeckManager 참조가 없습니다.");
        }

        /// <summary>4x3 그리드 셀 생성</summary>
        private void InitializeBoard()
        {
            if (combatBoard == null) return;

            foreach (Transform child in combatBoard)
                Destroy(child.gameObject);

            Canvas.ForceUpdateCanvases(); // rect.width 등 레이아웃 값이 유효하도록 강제 갱신
            grid = new CombatBoardCell[boardCols, boardRows];

            for (int col = 0; col < boardCols; col++)
            {
                for (int row = 0; row < boardRows; row++)
                {
                    GameObject cellObj = CreateCellObject(col, row);

                    RectTransform rt = cellObj.GetComponent<RectTransform>();
                    rt.anchoredPosition = GetCellPosition(col, row);
                    rt.sizeDelta        = new Vector2(cellWidth, cellHeight);

                    CombatBoardCell cell = cellObj.GetComponent<CombatBoardCell>();
                    if (cell == null) cell = cellObj.AddComponent<CombatBoardCell>();
                    cell.Initialize(new Vector2Int(col, row));
                    cell.OnCellClicked    += HandleCellClicked;
                    cell.OnCellHoverEnter += OnEnemyCellHoverEnter;
                    cell.OnCellHoverExit  += OnEnemyCellHoverExit;
                    grid[col, row] = cell;
                }
            }
        }

        private GameObject CreateCellObject(int col, int row)
        {
            if (cellPrefab != null)
                return Instantiate(cellPrefab, combatBoard);

            // 프리팹 없으면 기본 UI 오브젝트 생성
            var obj = new GameObject($"Cell_{col}_{row}",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(CombatBoardCell));
            obj.transform.SetParent(combatBoard, false);
            return obj;
        }

        /// <summary>
        /// 가로: 전체 컬럼을 combatBoard 기준 중앙 정렬.
        /// 세로: boardOffset.y(화면 좌하단 기준 픽셀)를 combatBoard 로컬 좌표로 변환해 그리드 하단 기준점으로 사용.
        /// </summary>
        private Vector2 GetCellPosition(int col, int row)
        {
            float totalWidth = boardCols * cellWidth;
            float centerX = (0.5f - combatBoard.pivot.x) * combatBoard.rect.width;
            float x = centerX - totalWidth / 2f + (col + 0.5f) * cellWidth;

            // 세로: boardOffset.y(화면 좌하단 기준 픽셀) → combatBoard 로컬 Y 좌표
            var canvas = combatBoard.GetComponentInParent<Canvas>();
            Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera : null;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                combatBoard, new Vector2(0f, boardOffset.y), uiCam, out Vector2 localOrigin);

            float y = localOrigin.y + (row + 0.5f) * cellHeight;
            return new Vector2(x, y);
        }

        private void PlacePlayer()
        {
            if (playerObject != null) Destroy(playerObject);
            if (grid == null) return;

            CombatBoardCell cell = grid[playerSpawnCell.x, playerSpawnCell.y];
            cell.IsPlayerHere = true;
            cell.SetState(CellState.PlayerOccupied);

            if (unitPrefab != null)
            {
                // CombatBoardCell 자식이 아닌 combatBoard 직접 자식으로 생성
                // → RangePreview보다 항상 위(높은 sibling index)에 렌더링
                playerObject = Instantiate(unitPrefab, combatBoard);
                RectTransform rt = playerObject.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = GetCellPosition(playerSpawnCell.x, playerSpawnCell.y) + playerCellOffset;

                playerObject.transform.SetAsLastSibling();
                foreach (var g in playerObject.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                    g.raycastTarget = false;

                unitUI = playerObject.GetComponentInChildren<DeckRoguelike.Combat.UnitUI>(true);
                if (unitUI != null && GameManager.Instance != null)
                    unitUI.UpdateHP(GameManager.Instance.CurrentHP, GameManager.Instance.MaxHP);
            }
        }

        private void SpawnEnemies()
        {
            foreach (var e in enemies)
                if (e.GameObject != null) Destroy(e.GameObject);
            enemies.Clear();

            if (grid == null) return;

            if (currentEncounterData == null || currentEncounterData.enemies == null || currentEncounterData.enemies.Length == 0)
            {
                Debug.LogError("[CombatController] 인카운터 데이터가 없습니다. SetEncounter()를 먼저 호출하세요.");
                return;
            }
            SpawnFromEncounter();
        }

        private void SpawnFromEncounter()
        {
            foreach (var slot in currentEncounterData.enemies)
            {
                if (slot.enemyData == null) continue;

                int col = slot.col;
                int row = slot.row;

                if (col < 0 || col >= boardCols || row < 0 || row >= boardRows)
                {
                    Debug.LogWarning($"[CombatController] 인카운터 '{currentEncounterData.encounterName}': ({col},{row})은 그리드 범위 초과 - 스킵");
                    continue;
                }

                EnemyData data = slot.enemyData;
                EnemyInstance enemy = new EnemyInstance
                {
                    Name      = data.enemyName,
                    MaxHP     = data.maxHP,
                    Damage    = data.baseDamage,
                    GridPos   = new Vector2Int(col, row),
                    Data      = data,
                };
                enemy.CurrentHP = enemy.MaxHP;
                enemy.Behavior  = EnemyBehaviorRegistry.Create(data.enemyId);

                PlaceEnemyOnCell(enemy, new Vector2Int(col, row));
                enemy.Behavior?.OnSpawn(enemy, this);
                enemies.Add(enemy);
            }

            Debug.Log($"[CombatController] '{currentEncounterData.encounterName}' 인카운터 - 적 {enemies.Count}마리 생성");
        }

/// <summary>EnemyInstance를 지정 셀에 배치 (SpawnEnemies + Summon 공용)</summary>
        private void PlaceEnemyOnCell(EnemyInstance enemy, Vector2Int pos)
        {
            CombatBoardCell cell = grid[pos.x, pos.y];
            cell.OccupyingEnemy = enemy;
            cell.SetState(CellState.EnemyOccupied);

            if (unitPrefab != null)
            {
                // combatBoard 직접 자식으로 생성 → RangePreview보다 항상 위에 렌더링
                enemy.GameObject = Instantiate(unitPrefab, combatBoard);
                RectTransform rt = enemy.GameObject.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = GetCellPosition(pos.x, pos.y) + playerCellOffset;
                enemy.GameObject.transform.SetAsLastSibling();
                enemy.UI = enemy.GameObject.GetComponent<UnitUI>();
                Sprite enemySprite = enemy.Data?.enemySprite;
                if (enemySprite == null && enemy.Data?.enemyId != 0)
                    enemySprite = Addressables.LoadAssetAsync<Sprite>($"Sprites/EnemySprites/{enemy.Data.enemyId}").WaitForCompletion();
                enemy.UI?.Initialize(enemy.MaxHP, enemySprite);
                RefreshEnemyActionIcons(enemy);   // 스폰 직후 기본 행동 아이콘 표시
                foreach (var g in enemy.GameObject.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                    g.raycastTarget = false;
            }
        }

        #endregion

        #region Turn Management

        private void StartPlayerTurn()
        {
            turnNumber++;
            combatState = CombatState.PlayerTurn;
            Debug.Log($"[CombatController] 플레이어 턴 {turnNumber}");

            FireRelicHook((r, ctx) => r.OnPlayerTurnStart(ctx));
            foreach (var p in _activePowers) p.OnTurnStart(this);

            // 카드 타입 봉인 턴 감소
            var expiredTypes = new List<CardType>();
            foreach (var key in new List<CardType>(cardTypeRestrictions.Keys))
            {
                cardTypeRestrictions[key]--;
                if (cardTypeRestrictions[key] <= 0)
                    expiredTypes.Add(key);
            }
            foreach (var type in expiredTypes)
            {
                cardTypeRestrictions.Remove(type);
                Debug.Log($"[CombatController] {type} 카드 봉인 해제");
            }

            // 이전 턴 임시 효과 초기화 (철가면, 초코바)
            outgoingDamageMultiplier = 1f;
            incomingDamageMultiplier = 1f;
            if (tempStrengthFromItem > 0)
            {
                playerStrength   -= tempStrengthFromItem;
                tempStrengthFromItem = 0;
                RefreshHandDisplay();
            }

            // 플레이어 화염 피해 처리
            ProcessPlayerFireStatus();

            _cardCostReduction = 0;
            currentEnergy = maxEnergy;
            UpdateEnergyDisplay();

            playerBlock = 0;
            UpdateBlockDisplay();

            DrawCards(startingHandSize + _bonusHandSize);
            UpdateTurnIndicator("내 턴");
            endTurnButton.interactable = true;

            foreach (var enemy in enemies)
            {
                if (enemy.CurrentHP <= 0) continue;
                enemy.Behavior?.PlanTurn(enemy, this);
                RefreshEnemyActionIcons(enemy);
            }
            foreach (var ally in allies)
            {
                ally.Behavior?.PlanTurn(ally, this);
                RefreshAllyActionIcons(ally);
            }

        }

        private void OnEndTurnClicked()
        {
            if (combatState != CombatState.PlayerTurn) return;
            CancelTargeting();
            PlaySound(endTurnSound);
            EndPlayerTurn();
        }

        private void EndPlayerTurn()
        {
            Debug.Log("[CombatController] 플레이어 턴 종료");
            FireRelicHook((r, ctx) => r.OnPlayerTurnEnd(ctx));
            endTurnButton.interactable = false;
            DiscardHand();
            StartCoroutine(EnemyTurnRoutine());
        }

        private IEnumerator EnemyTurnRoutine()
        {
            combatState = CombatState.EnemyTurn;
            UpdateTurnIndicator("적 턴");
            ClearUnitAttackPreviews();

            yield return new WaitForSeconds(0.5f);

            // 적 턴 시작 시 방어도 초기화 (플레이어와 동일 규칙)
            foreach (var enemy in enemies)
            {
                if (enemy.CurrentHP <= 0) continue;
                enemy.Block = 0;
                enemy.UI?.UpdateShield(0, enemy.CurrentHP, enemy.MaxHP);
            }

            foreach (var enemy in enemies)
            {
                if (enemy.CurrentHP <= 0) continue;

                // 화염 피해 처리
                ProcessEnemyFireStatus(enemy);
                if (enemy.CurrentHP <= 0) { CheckVictory(); continue; }

                // 기절·빙결: 이번 턴 스킵
                if (ConsumeEnemyStun(enemy)) continue;

                if (enemy.Behavior != null)
                {
                    enemy.Behavior.ExecuteTurn(enemy, this);
                }
                else
                {
                    // Behavior 없음: 기본 단순 공격 (fallback)
                    int actualDamage = ApplyDamageToPlayer(enemy.Damage, enemy);
                    if (actualDamage > 0)
                    {
                        PlaySound(attackSound);
                        Debug.Log($"[CombatController] {enemy.Name}의 공격! {enemy.Damage} 데미지");
                    }
                }

                if (GameManager.Instance != null && GameManager.Instance.CurrentHP <= 0)
                {
                    combatState = CombatState.Defeat;
                    PlaySound(defeatSound);
                    OnCombatEnded?.Invoke(false);
                    yield break;
                }

                yield return new WaitForSeconds(enemyTurnDelay);
            }

            StartPlayerTurn();
        }

        #endregion

        #region Card Management

        private void DrawCards(int count)
        {
            StartCoroutine(DrawCardsCoroutine(count));
        }

        private IEnumerator DrawCardsCoroutine(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (hand.Count >= 10) break;
                CardData card = deckManager != null ? deckManager.DrawCard() : null;
                if (card == null) break;

                hand.Add(card);
                PlaySound(drawCardSound);

                // 카드 오브젝트 생성 후 손패 레이아웃 계산 (기존 카드 부드럽게 재배치)
                GameObject cardObj = CreateSingleCardObject(card);
                handCardObjects.Add(cardObj);
                RepositionHandCards();

                // 새 카드 드로우 애니메이션: 덱 위치 → 손패 위치
                CardUI cardUI = cardObj.GetComponent<CardUI>();
                if (cardUI != null && drawPileButton != null)
                    cardUI.StartDrawAnimation(drawPileButton.transform.position, cardDrawDuration);

                UpdatePileCounters();
                RefreshCardPlayability(); // 새 카드의 플레이 가능 여부 즉시 반영
                yield return new WaitForSeconds(drawStaggerDelay);
            }
        }

        private GameObject CreateSingleCardObject(CardData card)
        {
            if (cardPrefab == null || handContainer == null) return null;

            GameObject cardObj = Instantiate(cardPrefab, handContainer);
            CardUI cardUI = cardObj.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.Initialize(card, playerStrength, playerDexterity, _cardCostReduction, currentEnergy);
                cardUI.SetAsHandCard(); // 손패 전용: LayoutGroup 간섭 차단 및 크기 고정
                // IndexOf로 동적 인덱스 조회 → 카드 제거 후에도 정확한 인덱스 사용
                CardData capturedCard = card;
                var capturedUI = cardUI;
                cardUI.OnCardPlayed         += (_)    => TryPlayCard(hand.IndexOf(capturedCard));
                // range 카드: hover/선택 시 range 스프라이트 표시. non-range(Skill/Power)는 collapse 이벤트로만 표시
                cardUI.OnCardDown           += (_)    => { int idx = hand.IndexOf(capturedCard); pendingCardIndex = idx; capturedUI.SetLocked(true); SetCombatButtonsInteractable(false); if (!capturedUI.IsRangeCard && !capturedUI.IsSelfPlayCard) EnterTargetingMode(idx); };
                cardUI.OnCardHoverEnter     += (_)    => { if (capturedUI.IsRangeCard || capturedUI.IsSelfPlayCard) ShowCardRangePreview(capturedCard); };
                cardUI.OnCardHoverExit      += (_)    => ClearCardRangePreview();
                cardUI.OnCardCollapseEnter  += (_)    =>
                {
                    if (capturedUI.IsSelfPlayCard)
                    {
                        if (hoverPreviewObjects.Count == 0) ShowCardRangePreview(capturedCard);
                        SetRangePreviewSprite(skillCollapsedRangeSprite);
                    }
                };
                cardUI.OnCardCollapseExit   += (_)    =>
                {
                    if (capturedUI.IsSelfPlayCard)
                        SetRangePreviewSprite(skillRangeSprite);
                };
                cardUI.OnTargetingCancelled += (_)    => CancelTargeting();
            }
            return cardObj;
        }

        private void RepositionHandCards()
        {
            int count = handCardObjects.Count;
            if (count == 0) return;

            float spacing = handCardSpacing;
            float totalWidth = count > 1 ? spacing * (count - 1) : 0f;
            float startX = -totalWidth / 2f;
            float rotationRange = 15f;

            // handContainer 중앙을 기준 높이에 배치 → isSticky 카드가 올바른 위치에 닿을 수 있도록
            SnapHandContainerToScreenBottom();
            // sticky 카드 축소 기준 Y를 CardUI에 전달 (Inspector에서 조정 가능)
            CardUI.BoardCollapseScreenY = cardShrinkBoardScreenY;

            // 카드 바닥이 화면 바닥에 밀착되도록 기준 Y 계산
            float cardHeight = GetHandCardHeight();
            float handBaseY  = CalculateHandBaseY(cardHeight);

            for (int i = 0; i < count; i++)
            {
                if (handCardObjects[i] == null) continue;
                CardUI cardUI = handCardObjects[i].GetComponent<CardUI>();
                if (cardUI == null) continue;

                float xPos = count > 1 ? startX + i * spacing : 0f;
                float rotZ = count > 1
                    ? Mathf.Lerp(rotationRange, -rotationRange, (float)i / (count - 1))
                    : 0f;

                // 가장자리로 갈수록 아래로: 포물선 아크 (중앙=0, 가장자리=-arcDrop)
                float t    = count > 1 ? (float)i / (count - 1) - 0.5f : 0f; // -0.5 ~ 0.5
                float arcY = -(t * t * 4f) * handArcDrop;
                float yPos = handBaseY + arcY;

                cardUI.SetHandPosition(new Vector3(xPos, yPos, 0f), Quaternion.Euler(0f, 0f, rotZ));
            }

            RefreshCardPlayability();
        }

        /// <summary>
        /// handContainer의 anchoredPosition을 조정해 컨테이너 중앙이 화면 하단에 오도록 합니다.
        /// 이렇게 하면 isSticky 카드(카드 바닥 = 화면 바닥)가 커서를 따라 화면 하단까지 이동할 수 있습니다.
        /// </summary>
        private void SnapHandContainerToScreenBottom()
        {
            var handRT = handContainer as RectTransform;
            if (handRT == null) return;

            var parentRT = handRT.parent as RectTransform;
            if (parentRT == null) return;

            Canvas canvas = handContainer.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;

            // 화면 하단 중앙(+handYOffset)을 부모(CombatPanel) 로컬 좌표로 변환
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT, new Vector2(Screen.width * 0.5f, handYOffset), cam, out Vector2 localBottom);

            // handContainer 중앙이 기준 위치에 오도록 anchoredPosition의 Y만 조정
            handRT.anchoredPosition = new Vector2(handRT.anchoredPosition.x, localBottom.y);
        }

        /// <summary>카드 프리팹의 세로 크기를 반환합니다.</summary>
        private float GetHandCardHeight()
        {
            if (cardPrefab == null) return 280f;
            var rt = cardPrefab.GetComponent<RectTransform>();
            return rt != null ? rt.sizeDelta.y : 280f;
        }

        /// <summary>
        /// handContainer 로컬 좌표 기준으로, 중앙 카드의 바닥이 화면 바닥에
        /// 딱 맞도록 카드 중심 Y를 계산합니다.
        /// </summary>
        private float CalculateHandBaseY(float cardHeight)
        {
            var handRT = handContainer as RectTransform;
            if (handRT == null) return 0f;

            Canvas canvas = handContainer.GetComponentInParent<Canvas>();
            if (canvas == null) return 0f;

            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;

            // 기준 높이(화면 하단 + handYOffset)를 handContainer 로컬 좌표로 변환
            Vector2 screenBaseCenter = new Vector2(Screen.width * 0.5f, handYOffset);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                handRT, screenBaseCenter, cam, out Vector2 localBottom);

            // 카드 중심 Y = 기준 Y + 카드 절반 높이 (카드 바닥이 기준 높이에 밀착)
            return localBottom.y + cardHeight * 0.5f;
        }

        private IEnumerator DiscardAndDestroy(CardUI cardUI, Vector3 discardWorldPos)
        {
            yield return cardUI.AnimateDiscardOut(discardWorldPos, cardDiscardDuration);
            if (cardUI != null && cardUI.gameObject != null)
                Destroy(cardUI.gameObject);
        }

        private void DiscardHand()
        {
            // 데이터 즉시 처리
            if (deckManager != null)
                foreach (var card in hand)
                    deckManager.AddToDiscardPile(card);
            hand.Clear();

            // 비주얼 애니메이션 후 파괴 (fire-and-forget)
            var cardsToDiscard = new List<GameObject>(handCardObjects);
            handCardObjects.Clear();
            UpdatePileCounters();

            if (discardPileButton != null)
            {
                Vector3 discardPos = discardPileButton.transform.position;
                foreach (var obj in cardsToDiscard)
                {
                    if (obj == null) continue;
                    CardUI cardUI = obj.GetComponent<CardUI>();
                    if (cardUI != null)
                        StartCoroutine(DiscardAndDestroy(cardUI, discardPos));
                    else
                        Destroy(obj);
                }
            }
            else
            {
                foreach (var obj in cardsToDiscard)
                    if (obj != null) Destroy(obj);
            }
        }

        private void ClearHandVisuals()
        {
            foreach (var obj in handCardObjects)
                if (obj != null) Destroy(obj);
            handCardObjects.Clear();

            // handCardObjects에 추적되지 않은 카드(애니메이션 중 제거된 것 등)도 정리
            if (handContainer != null)
                foreach (Transform child in handContainer)
                    Destroy(child.gameObject);
        }

        /// <summary>
        /// 현재 게임 상태 기준으로 카드가 사용 가능한지 판단.
        /// 상황별 조건을 추가하려면 여기에 넣으면 됩니다.
        /// </summary>
        private bool IsCardPlayableNow(CardData card)
        {
            if (card.IsUnplayable) return false;
            if (card.IsXCost)
            {
                if (currentEnergy <= 0) return false;
            }
            else
            {
                int effectiveCost = Mathf.Max(0, card.EnergyCost - _cardCostReduction);
                if (currentEnergy < effectiveCost) return false;
            }
            if (cardTypeRestrictions.TryGetValue(card.CardTypeFromCode, out int remaining) && remaining > 0) return false;
            return true;
        }

        /// <summary>
        /// 적 효과 등에서 호출: 특정 카드 타입을 N턴 동안 사용 봉인.
        /// 이미 봉인 중이면 남은 턴 중 더 긴 쪽으로 덮어씁니다.
        /// </summary>
        public void AddCardTypeRestriction(CardType type, int turns)
        {
            if (cardTypeRestrictions.TryGetValue(type, out int current))
                cardTypeRestrictions[type] = Mathf.Max(current, turns);
            else
                cardTypeRestrictions[type] = turns;

            Debug.Log($"[CombatController] {type} 카드 {turns}턴 봉인");
            RefreshCardPlayability();
        }

        /// <summary>
        /// 손패의 모든 카드 UI 플레이 가능 상태를 현재 게임 상태에 맞게 갱신
        /// </summary>
        private void RefreshCardPlayability()
        {
            for (int i = 0; i < handCardObjects.Count && i < hand.Count; i++)
            {
                if (handCardObjects[i] == null) continue;
                var cardUI = handCardObjects[i].GetComponent<CardUI>();
                cardUI?.SetPlayable(IsCardPlayableNow(hand[i]));
            }
        }

        private void TryPlayCard(int handIndex)
        {
            if (combatState != CombatState.PlayerTurn) return;
            if (handIndex < 0 || handIndex >= hand.Count) return;

            CardData card = hand[handIndex];

            if (!IsCardPlayableNow(card))
            {
                Debug.Log("[CombatController] 카드를 사용할 수 없습니다.");
                return;
            }

            // 이미 다른 카드 타겟팅 중이면 취소 (실행 직전이므로 손패로 복귀 애니메이션 생략)
            if (pendingCard != null)
                CancelTargeting(returnToHand: false);

            switch (card.PrimaryTargeting)
            {
                case TargetType.Enemy:
                case TargetType.Any:
                case TargetType.Ally:
                    EnterTargetingMode(handIndex);
                    break;
                default: // Self, Random, All
                    SetCombatButtonsInteractable(true);
                    ExecuteCard(handIndex, null);
                    break;
            }
        }

        /// <summary>
        /// 카드 실행 공통 처리: 에너지 소모 → 효과 적용 → 더미 이동 → UI 갱신
        /// selectedPos: 플레이어가 선택한 셀 위치 (effects[0]의 targeting이 Enemy/Any/Ally인 경우)
        /// </summary>
        private void ExecuteCard(int handIndex, Vector2Int? selectedPos)
        {
            CardData card = hand[handIndex];
            Debug.Log($"[CombatController] 카드 사용: {card.CardName}");

            ClearCardRangePreview(); // 카드 실행 시 hover 미리보기 즉시 제거

            // 플레이된 카드 오브젝트 먼저 참조 (제거 전)
            GameObject playedObj = handIndex < handCardObjects.Count ? handCardObjects[handIndex] : null;

            int xValue = 0;
            if (card.IsXCost)
            {
                xValue = currentEnergy;
                currentEnergy = 0;
            }
            else
            {
                currentEnergy -= Mathf.Max(0, card.EnergyCost - _cardCostReduction);
            }
            UpdateEnergyDisplay();

            _playingHandIndex = handIndex;
            ApplyCardEffects(card, selectedPos, xValue);
            _playingHandIndex = -1;
            FireRelicHook((r, ctx) => r.OnCardPlayed(ctx, card));

            // 재사용 포션: 다음 카드를 2번 실행 (1회 소모)
            if (nextCardDoublePlay)
            {
                nextCardDoublePlay = false;
                _playingHandIndex = hand.IndexOf(card);
                ApplyCardEffects(card, selectedPos, xValue);
                _playingHandIndex = -1;
                Debug.Log("[재사용 포션] 카드 효과 2회 실행!");
            }

            // 효과로 hand가 변경될 수 있으므로 현재 위치를 재탐색
            int actualIdx = hand.IndexOf(card);
            if (actualIdx >= 0)
            {
                hand.RemoveAt(actualIdx);
                if (actualIdx < handCardObjects.Count)
                    handCardObjects.RemoveAt(actualIdx);
            }

            bool isPower = card.CardTypeFromCode == DeckRoguelike.Cards.CardType.Power;
            if (isPower && !_noExhaust)
            {
                // 파워 카드: 소멸더미로 이동하지만 소멸 상호작용은 발동하지 않음
                deckManager?.ExhaustCard(card);
            }
            else if (card.Exhaust && !_noExhaust)
            {
                deckManager?.ExhaustCard(card);
                foreach (var p in _activePowers) p.OnCardExhausted(this);
            }
            else
            {
                deckManager?.AddToDiscardPile(card);
            }

            GameManager.Instance?.AddCardPlayed();

            // 나머지 카드 부드럽게 재배치
            RepositionHandCards();
            UpdatePileCounters();

            // 플레이된 카드 버림더미로 날리기 (CheckVictory 전에 코루틴 시작해야 함)
            // CheckVictory가 SetCombatHudActive(false)를 호출하면 GameObject가 비활성화돼
            // StartCoroutine이 실패하기 때문
            if (playedObj != null)
            {
                CardUI playedUI = playedObj.GetComponent<CardUI>();
                if (playedUI != null)
                {
                    playedUI.MarkAsPlayed();
                    if (discardPileButton != null)
                        StartCoroutine(DiscardAndDestroy(playedUI, discardPileButton.transform.position));
                    else
                        Destroy(playedObj);
                }
                else
                    Destroy(playedObj);
            }

            CheckVictory();
        }

        /// <summary>
        /// 카드의 모든 효과를 순서대로 적용.
        /// 각 효과는 자신의 targeting/rangeOffsets로 독립적으로 대상을 결정한다.
        /// selectedPos는 Enemy/Any/Ally 효과에서 플레이어가 선택한 좌표.
        /// </summary>
        private void ApplyCardEffects(CardData card, Vector2Int? selectedPos, int xValue = 0)
        {
            Debug.Log($"[ApplyCardEffects] '{card.CardName}' Effects 수={card.Effects?.Count ?? -1} selectedPos={selectedPos}");
            if (card.Effects == null) return;

            foreach (var effect in card.Effects)
            {
                Debug.Log($"[ApplyCardEffects] effect.effectType={effect.effectType} value={effect.value}");
                switch (effect.effectType)
                {
                    case EffectType.Damage:
                    {
                        int baseDmg = effect.value + playerStrength;
                        float mult  = outgoingDamageMultiplier;
                        // 분노의 포션: 다음 공격 카드 3배 (1회 소모)
                        if (card.CardTypeFromCode == DeckRoguelike.Cards.CardType.Attack
                            && nextAttackDamageMultiplier > 1)
                        {
                            mult *= nextAttackDamageMultiplier;
                            nextAttackDamageMultiplier = 1;
                        }
                        int finalDmg = Mathf.RoundToInt(baseDmg * mult);
                        foreach (var t in ResolveEffectTargets(effect, selectedPos))
                            DamageEnemy(t, finalDmg);
                        break;
                    }

                    case EffectType.Block:
                        playerBlock += effect.value + playerDexterity;
                        UpdateBlockDisplay();
                        PlaySound(blockSound);
                        Debug.Log($"[CombatController] 방어도 +{effect.value}");
                        break;

                    case EffectType.Draw:
                        DrawCards(effect.value);
                        break;

                    case EffectType.Energy:
                        currentEnergy += effect.value;
                        UpdateEnergyDisplay();
                        break;

                    case EffectType.Heal:
                        GameManager.Instance?.Heal(effect.value);
                        break;

                    case EffectType.Move:
                        if (selectedPos.HasValue)
                            MovePlayer(selectedPos.Value);
                        break;

                    case EffectType.SummonAlly:
                        if (selectedPos.HasValue && int.TryParse(effect.customEffectId, out int allyCode))
                        {
                            var allyData = AllyRegistry.GetAlly(allyCode);
                            if (allyData != null) SummonAlly(allyData, selectedPos.Value);
                        }
                        break;

                    case EffectType.Custom:
                        CardEffectRegistry.Execute(effect.customEffectId, new CardEffectContext
                        {
                            Combat      = this,
                            Value       = effect.value,
                            SelectedPos = selectedPos,
                            XValue      = xValue,
                        });
                        break;
                }
            }
        }

        /// <summary>
        /// 효과의 targeting 타입에 따라 실제 피해 대상 목록을 반환한다.
        /// - Enemy  : selectedPos 셀의 적
        /// - All    : 효과의 rangeOffsets 기준 범위 내 모든 적
        /// - Random : 살아있는 적 중 무작위 1명
        /// - Any / Self / Ally : 빈 목록 (Custom 함수로 처리)
        /// </summary>
        private List<EnemyInstance> ResolveEffectTargets(CardEffect effect, Vector2Int? selectedPos)
        {
            var result = new List<EnemyInstance>();

            switch (effect.targeting)
            {
                case TargetType.Enemy:
                    if (selectedPos.HasValue && IsInBoard(selectedPos.Value))
                    {
                        var e = grid[selectedPos.Value.x, selectedPos.Value.y].OccupyingEnemy;
                        if (e != null && e.CurrentHP > 0) result.Add(e);
                    }
                    break;

                case TargetType.Any:
                    // 범위 공격은 Custom 함수로 처리. 여기서는 빈 목록 반환.
                    break;

                case TargetType.All:
                    if (effect.rangeOffsets != null && effect.rangeOffsets.Length > 0)
                    {
                        foreach (var offset in effect.rangeOffsets)
                        {
                            Vector2Int pos = effect.useAbsoluteCoords
                                ? offset.ToVector2Int()
                                : playerSpawnCell + offset.ToVector2Int();
                            if (!IsInBoard(pos)) continue;
                            var e = grid[pos.x, pos.y].OccupyingEnemy;
                            if (e != null && e.CurrentHP > 0) result.Add(e);
                        }
                    }
                    else
                    {
                        // 무제한 범위: 살아있는 모든 적
                        foreach (var e in enemies) if (e.CurrentHP > 0) result.Add(e);
                    }
                    break;

                case TargetType.Random:
                    var alive = new List<EnemyInstance>();
                    foreach (var e in enemies) if (e.CurrentHP > 0) alive.Add(e);
                    if (alive.Count > 0)
                        result.Add(alive[UnityEngine.Random.Range(0, alive.Count)]);
                    break;
            }

            return result;
        }

        /// <summary>플레이어를 newPos로 이동</summary>
        private void MovePlayer(Vector2Int newPos)
        {
            if (!IsInBoard(newPos)) return;
            if (grid[newPos.x, newPos.y].OccupyingEnemy != null)
            {
                Debug.LogWarning($"[MovePlayer] ({newPos})에 적이 있어 이동 불가");
                return;
            }

            // 수평 이동 방향으로 바라보는 방향 갱신
            if (newPos.x > playerSpawnCell.x) SetFacingRight(true);
            else if (newPos.x < playerSpawnCell.x) SetFacingRight(false);

            // 현재 셀 초기화
            CombatBoardCell oldCell = grid[playerSpawnCell.x, playerSpawnCell.y];
            oldCell.IsPlayerHere = false;
            oldCell.ClearHighlight();

            // 새 셀로 이동 (parent는 combatBoard 유지, anchoredPosition만 변경)
            if (playerObject != null)
            {
                RectTransform rt = playerObject.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = GetCellPosition(newPos.x, newPos.y) + playerCellOffset;
                playerObject.transform.SetAsLastSibling();
            }

            CombatBoardCell newCell = grid[newPos.x, newPos.y];
            newCell.IsPlayerHere = true;
            newCell.SetState(CellState.PlayerOccupied);
            playerSpawnCell = newPos;
            Debug.Log($"[CombatController] 플레이어 이동 → {newPos}");

            _playerMoveCount++;
            FireRelicHook((r, ctx) => r.OnPlayerMoved(ctx, _playerMoveCount));
        }

        private void SetFacingRight(bool right)
        {
            if (facingRight == right) return;
            facingRight = right;
            if (playerObject != null)
            {
                Vector3 scale = playerObject.transform.localScale;
                scale.x = right ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
                playerObject.transform.localScale = scale;
            }
        }

        /// <summary>
        /// 적 유닛을 targetPos로 이동합니다. 이동에 성공하면 true, 실패하면 false를 반환합니다.
        /// 대상 셀에 플레이어나 다른 적이 있으면 이동 불가합니다.
        /// </summary>
        public bool TryMoveEnemy(EnemyInstance enemy, Vector2Int targetPos)
        {
            if (!IsInBoard(targetPos)) return false;

            CombatBoardCell targetCell = grid[targetPos.x, targetPos.y];
            if (targetCell.IsPlayerHere || targetCell.OccupyingEnemy != null || targetCell.OccupyingAlly != null) return false;

            // 기존 셀 초기화
            CombatBoardCell oldCell = grid[enemy.GridPos.x, enemy.GridPos.y];
            oldCell.OccupyingEnemy = null;
            oldCell.SetState(CellState.Empty);

            // 새 셀로 이동
            enemy.GridPos = targetPos;
            targetCell.OccupyingEnemy = enemy;
            targetCell.SetState(CellState.EnemyOccupied);

            if (enemy.GameObject != null)
            {
                RectTransform rt = enemy.GameObject.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = GetCellPosition(targetPos.x, targetPos.y) + playerCellOffset;
                enemy.GameObject.transform.SetAsLastSibling();
            }

            return true;
        }

        /// <summary>
        /// SummonAlly 효과: AllyData 기반으로 지정 위치에 아군 유닛 소환.
        /// 프리팹은 AllyData.allyPrefab을 사용하며, 행동 알고리즘은 behaviorId로 등록된 함수가 처리.
        /// 이미 유닛이 있는 셀이면 무시.
        /// </summary>
        private void SummonAlly(AllyData data, Vector2Int pos)
        {
            if (!IsInBoard(pos)) return;
            if (grid[pos.x, pos.y].OccupyingEnemy != null || grid[pos.x, pos.y].IsPlayerHere) return;

            GameObject prefabToUse = data.allyPrefab != null ? data.allyPrefab : unitPrefab;
            if (prefabToUse == null)
            {
                Debug.LogWarning($"[CombatController] {data.allyName}: allyPrefab도 unitPrefab도 없습니다.");
                return;
            }

            // combatBoard 직접 자식으로 생성 → RangePreview보다 항상 위에 렌더링
            GameObject allyObj = Instantiate(prefabToUse, combatBoard);
            RectTransform rt = allyObj.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = GetCellPosition(pos.x, pos.y) + playerCellOffset;
            allyObj.transform.SetAsLastSibling();

            // 아군 스프라이트: allySprite 필드 우선, 없으면 Resources/AllySprites/{allyCode} 로드
            var allySR = allyObj.GetComponentInChildren<SpriteRenderer>();
            if (allySR != null)
            {
                Sprite allySprite = data.allySprite;
                if (allySprite == null && data.allyCode > 0)
                    allySprite = Addressables.LoadAssetAsync<Sprite>($"Sprites/AllySprites/{data.allyCode}").WaitForCompletion();
                if (allySprite != null)
                    allySR.sprite = allySprite;
            }

            var instance = new AllyInstance
            {
                Name           = data.allyName,
                MaxHP          = data.maxHP,
                CurrentHP      = data.maxHP,
                Damage         = data.baseDamage,
                TurnsRemaining = data.durationTurns,
                GridPos        = pos,
                GameObject     = allyObj,
                Data           = data,
            };

            instance.UI = allyObj.GetComponent<UnitUI>();
            Sprite allyUISpr = data.allySprite;
            if (allyUISpr == null && data.allyCode > 0)
                allyUISpr = Addressables.LoadAssetAsync<Sprite>($"Sprites/AllySprites/{data.allyCode}").WaitForCompletion();
            instance.UI?.Initialize(data.maxHP, allyUISpr);
            RefreshAllyActionIcons(instance);   // 스폰 직후 기본 행동 아이콘 표시

            instance.Behavior = AllyBehaviorRegistry.Create(data.allyCode);

            allies.Add(instance);
            grid[pos.x, pos.y].OccupyingAlly = instance;
            grid[pos.x, pos.y].SetState(CellState.AllyOccupied);

            Debug.Log($"[CombatController] {data.allyName} 소환! 위치: {pos}");
        }

        public void DamageEnemy(EnemyInstance enemy, int amount)
        {
            // 방어도(Block) 먼저 흡수
            if (enemy.Block > 0)
            {
                int absorbed = Mathf.Min(enemy.Block, amount);
                enemy.Block -= absorbed;
                amount -= absorbed;
                enemy.UI?.UpdateShield(enemy.Block, enemy.CurrentHP, enemy.MaxHP);
            }

            if (amount <= 0)
            {
                Debug.Log($"[CombatController] {enemy.Name}의 방어도가 {(enemy.Block == 0 ? "모두 " : "")}흡수했습니다.");
                return;
            }

            enemy.CurrentHP -= amount;
            GameManager.Instance?.AddDamageDealt(amount);
            PlaySound(attackSound);
            Debug.Log($"[CombatController] {enemy.Name}에게 {amount} 데미지!");

            enemy.UI?.UpdateHP(enemy.CurrentHP, enemy.MaxHP);

            if (enemy.CurrentHP <= 0)
            {
                // 셀 비우기
                if (grid != null)
                    grid[enemy.GridPos.x, enemy.GridPos.y].ClearUnit();

                if (enemy.GameObject != null)
                    Destroy(enemy.GameObject);

                Debug.Log($"[CombatController] {enemy.Name} 사망!");
                FireRelicHook((r, ctx) => r.OnEnemyKilled(ctx, enemy));
                foreach (var p in _activePowers) p.OnEnemyKilled(this);
                CheckVictory();
            }
        }

        #endregion

        #region Targeting

        /// <summary>타겟 선택 모드 진입: 첫 번째 효과의 rangeOffsets 기준으로 셀 하이라이트</summary>
        /// <summary>드래그로 셀 위에서 놓았을 때 유효성 검사 후 카드 실행</summary>
        private void TryExecuteCardOnDragDrop(int handIndex, CombatBoardCell cell)
        {
            if (combatState != CombatState.PlayerTurn) return;
            if (handIndex < 0 || handIndex >= hand.Count) return;
            CardData card = hand[handIndex];
            if (!IsCardPlayableNow(card)) return;

            var firstEffect = card.Effects?[0];
            if (firstEffect == null) return;

            var pt = firstEffect.targeting;
            if (pt != TargetType.Enemy && pt != TargetType.Any && pt != TargetType.Ally) return;

            // 범위 체크 (rangeOffsets 있을 때만)
            if (firstEffect.rangeOffsets != null && firstEffect.rangeOffsets.Length > 0)
            {
                bool inRange = false;
                foreach (var offset in firstEffect.rangeOffsets)
                {
                    Vector2Int pos = firstEffect.useAbsoluteCoords
                        ? offset.ToVector2Int()
                        : playerSpawnCell + offset.ToVector2Int();
                    if (pos == cell.GridPos) { inRange = true; break; }
                }
                if (!inRange) return;
            }

            // Enemy: 적 있어야 함 / Ally: 자신/아군 셀이어야 함
            if (pt == TargetType.Enemy &&
                (cell.OccupyingEnemy == null || cell.OccupyingEnemy.CurrentHP <= 0)) return;
            if (pt == TargetType.Ally &&
                !cell.IsPlayerHere && cell.OccupyingAlly == null) return;

            if (pendingCard != null) CancelTargeting();
            ExecuteCard(handIndex, cell.GridPos);
        }

        private void EnterTargetingMode(int cardIndex)
        {
            pendingCardIndex = cardIndex;
            pendingCard      = hand[cardIndex];

            ClearAllHighlights();
            HighlightRange(pendingCard);

            Debug.Log($"[CombatController] 타겟 선택: {pendingCard.CardName}");
        }

        /// <summary>첫 번째 효과의 rangeOffsets/targeting 기준으로 셀 하이라이트</summary>
        private void HighlightRange(CardData card)
        {
            if (grid == null || card.Effects == null || card.Effects.Count == 0) return;

            var firstEffect = card.Effects[0];

            // rangeOffsets 없음 = 무제한 사거리 → 적용 가능한 대상 셀만 하이라이트
            if (firstEffect.rangeOffsets == null || firstEffect.rangeOffsets.Length == 0)
            {
                switch (firstEffect.targeting)
                {
                    case TargetType.Enemy:
                        foreach (var e in enemies)
                            if (e.CurrentHP > 0 && IsInBoard(e.GridPos))
                                grid[e.GridPos.x, e.GridPos.y].SetState(CellState.TargetableEnemy);
                        break;
                    case TargetType.All:
                        for (int col = 0; col < boardCols; col++)
                            for (int row = 0; row < boardRows; row++)
                                grid[col, row].SetState(CellState.AreaSelectable);
                        break;
                    case TargetType.Ally:
                        grid[playerSpawnCell.x, playerSpawnCell.y].SetState(CellState.TargetableAlly);
                        foreach (var a in allies)
                            if (IsInBoard(a.GridPos))
                                grid[a.GridPos.x, a.GridPos.y].SetState(CellState.TargetableAlly);
                        break;
                    case TargetType.Self:
                    case TargetType.Random:
                        grid[playerSpawnCell.x, playerSpawnCell.y].SetState(CellState.AreaSelectable);
                        break;
                }
                return;
            }

            foreach (var offset in firstEffect.rangeOffsets)
            {
                Vector2Int pos = firstEffect.useAbsoluteCoords
                    ? offset.ToVector2Int()
                    : playerSpawnCell + offset.ToVector2Int();
                if (!IsInBoard(pos)) continue;

                CombatBoardCell cell = grid[pos.x, pos.y];
                cell.SetState(GetHighlightState(cell, firstEffect.targeting));
            }
        }

        /// <summary>
        /// 카드 hover 시 effects[0] 범위에 sprite 오브젝트를 생성해 미리보기.
        /// OnCardHoverExit에 연결하지 않으므로 커서가 카드 밖으로 나가도 유지된다.
        /// </summary>
        private void ShowCardRangePreview(CardData card)
        {
            if (pendingCard != null) return;
            if (combatBoard == null || card.Effects == null || card.Effects.Count == 0) return;

            Debug.Log($"[RangePreview] '{card.CardName}' CardType={card.CardTypeFromCode} TypeDigit={card.TypeDigit}");
            var firstEffectForSprite = card.Effects[0];
            Sprite previewSprite = firstEffectForSprite.targeting == TargetType.Random
                ? attackRangeSprite
                : card.CardTypeFromCode switch
                {
                    CardType.Attack => attackRangeSprite,
                    CardType.Move   => moveRangeSprite,
                    CardType.Skill  => skillRangeSprite,
                    CardType.Power  => skillRangeSprite,
                    _               => null,
                };
            if (previewSprite == null) return;

            foreach (var old in hoverPreviewObjects)
                if (old != null) Destroy(old);
            hoverPreviewObjects.Clear();

            var firstEffect = card.Effects[0];
            bool hasRange = firstEffect.rangeOffsets != null && firstEffect.rangeOffsets.Length > 0;

            if (hasRange)
            {
                // range가 있는 카드: rangeOffsets 위치에 스프라이트
                bool isMoveCard = card.CardTypeFromCode == CardType.Move;
                foreach (var offset in firstEffect.rangeOffsets)
                {
                    Vector2Int pos = firstEffect.useAbsoluteCoords
                        ? offset.ToVector2Int()
                        : playerSpawnCell + offset.ToVector2Int();
                    if (!IsInBoard(pos)) continue;
                    if (grid == null) continue;
                    if (grid[pos.x, pos.y].IsPlayerHere) continue;
                    if (isMoveCard && grid[pos.x, pos.y].OccupyingEnemy != null) continue;
                    AddPreviewObject(pos, previewSprite);
                }
            }
            else
            {
                // 무제한 사거리: 적용 가능한 대상에만 스프라이트 표시
                switch (firstEffect.targeting)
                {
                    case TargetType.Enemy:
                    case TargetType.All:
                        foreach (var e in enemies)
                            if (e.CurrentHP > 0 && IsInBoard(e.GridPos))
                                AddPreviewObject(e.GridPos, previewSprite);
                        break;
                    case TargetType.Ally:
                        AddPreviewObject(playerSpawnCell, previewSprite);
                        foreach (var a in allies)
                            if (IsInBoard(a.GridPos))
                                AddPreviewObject(a.GridPos, previewSprite);
                        break;
                    default:
                        // Self 등: 플레이어 위치에 표시
                        AddPreviewObject(playerSpawnCell, previewSprite);
                        break;
                }
            }

        }

        private void AddPreviewObject(Vector2Int pos, Sprite sprite)
            => AddPreviewObject(pos, sprite, hoverPreviewObjects);

        private void AddPreviewObject(Vector2Int pos, Sprite sprite, List<GameObject> targetList)
        {
            var obj = new GameObject("RangePreview", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(combatBoard, false);

            var rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = GetCellPosition(pos.x, pos.y);
            rt.sizeDelta = new Vector2(cellWidth * playerRangePreviewScale, cellHeight * playerRangePreviewScale);

            var img = obj.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;

            // unitPreview 위, unit 프리팹(SetAsLastSibling) 아래에 위치
            obj.transform.SetSiblingIndex(boardCols * boardRows + unitPreviewObjects.Count + hoverPreviewObjects.Count);
            targetList.Add(obj);
        }

        /// <summary>플레이어 턴 시작 시 적·아군의 예정 행동 좌표를 보드에 표시합니다.</summary>
        private void ShowUnitAttackPreviews()
        {
            ClearUnitAttackPreviews();
            if (combatBoard == null) return;

            foreach (var enemy in enemies)
            {
                if (enemy.CurrentHP <= 0 || enemy.Behavior == null) continue;
                AddUnitPreviews(enemy.Behavior.GetAttackPreviewPositions(enemy), attackRangeSprite);
                AddUnitPreviews(enemy.Behavior.GetSkillPreviewPositions(enemy),  skillRangeSprite);
                AddUnitPreviews(enemy.Behavior.GetMovePreviewPositions(enemy),   moveRangeSprite);
            }

            foreach (var ally in allies)
            {
                if (ally.Behavior == null) continue;
                AddUnitPreviews(ally.Behavior.GetAttackPreviewPositions(ally), attackRangeSprite);
                AddUnitPreviews(ally.Behavior.GetSkillPreviewPositions(ally),  skillRangeSprite);
                AddUnitPreviews(ally.Behavior.GetMovePreviewPositions(ally),   moveRangeSprite);
            }
        }

        private void AddUnitPreviews(List<Vector2Int> positions, Sprite sprite)
        {
            if (positions == null || sprite == null) return;
            foreach (var pos in positions)
            {
                if (!IsInBoard(pos)) continue;
                var obj = new GameObject("UnitPreview", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                obj.transform.SetParent(combatBoard, false);
                var rt = obj.GetComponent<RectTransform>();
                rt.anchoredPosition = GetCellPosition(pos.x, pos.y);
                rt.sizeDelta = new Vector2(cellWidth * enemyRangePreviewScale, cellHeight * enemyRangePreviewScale);
                var img = obj.GetComponent<UnityEngine.UI.Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                obj.transform.SetSiblingIndex(boardCols * boardRows);
                unitPreviewObjects.Add(obj);
            }
        }

        private void ClearUnitAttackPreviews()
        {
            foreach (var obj in unitPreviewObjects)
                if (obj != null) Destroy(obj);
            unitPreviewObjects.Clear();
            _hoveredEnemy = null;
        }

        // ── Enemy / Ally 행동 아이콘 갱신 ────────────────────────────────

        /// <summary>
        /// EnemyBehavior의 계획된 위치 목록을 읽어 행동 아이콘을 갱신합니다.
        /// PlanTurn 이전에 호출되면 기본 공격 아이콘만 표시합니다.
        /// </summary>
        private void RefreshEnemyActionIcons(EnemyInstance enemy)
        {
            if (enemy == null) return;
            var actions = new List<UnitActionEntry>();

            if (enemy.Behavior != null)
            {
                var atkPos   = enemy.Behavior.GetAttackPreviewPositions(enemy);
                var movePos  = enemy.Behavior.GetMovePreviewPositions(enemy);
                var skillPos = enemy.Behavior.GetSkillPreviewPositions(enemy);

                if (atkPos?.Count   > 0) actions.Add(new UnitActionEntry { Type = UnitActionType.Attack, Value = enemy.Damage, Icon = attackActionIcon });
                if (movePos?.Count  > 0) actions.Add(new UnitActionEntry { Type = UnitActionType.Move,   Value = movePos.Count, Icon = moveActionIcon });
                if (skillPos?.Count > 0) actions.Add(new UnitActionEntry { Type = UnitActionType.Skill,  Value = 0,             Icon = skillActionIcon });
            }

            // 계획된 행동이 없으면 기본 공격 아이콘 표시
            if (actions.Count == 0)
                actions.Add(new UnitActionEntry { Type = UnitActionType.Attack, Value = enemy.Damage, Icon = attackActionIcon });

            enemy.PlannedActions = actions;
            enemy.UI?.RefreshActions(actions);
        }

        /// <summary>AllyBehavior의 계획된 위치 목록을 읽어 행동 아이콘을 갱신합니다.</summary>
        private void RefreshAllyActionIcons(AllyInstance ally)
        {
            if (ally == null) return;
            var actions = new List<UnitActionEntry>();

            if (ally.Behavior != null)
            {
                var atkPos   = ally.Behavior.GetAttackPreviewPositions(ally);
                var movePos  = ally.Behavior.GetMovePreviewPositions(ally);
                var skillPos = ally.Behavior.GetSkillPreviewPositions(ally);

                if (atkPos?.Count   > 0) actions.Add(new UnitActionEntry { Type = UnitActionType.Attack, Value = ally.Damage, Icon = attackActionIcon });
                if (movePos?.Count  > 0) actions.Add(new UnitActionEntry { Type = UnitActionType.Move,   Value = movePos.Count, Icon = moveActionIcon });
                if (skillPos?.Count > 0) actions.Add(new UnitActionEntry { Type = UnitActionType.Skill,  Value = 0,            Icon = skillActionIcon });
            }

            if (actions.Count == 0)
                actions.Add(new UnitActionEntry { Type = UnitActionType.Attack, Value = ally.Damage, Icon = attackActionIcon });

            ally.PlannedActions = actions;
            ally.UI?.RefreshActions(actions);
        }

        // ── Enemy 셀 Hover → 해당 적 범위 미리보기 ──────────────────────

        private EnemyInstance _hoveredEnemy;

        private void OnEnemyCellHoverEnter(CombatBoardCell cell)
        {
            // 카드 선택/타겟팅 중 또는 아이템 모드 중에는 적 hover 미리보기 비활성
            if (pendingCard != null || pendingCardIndex >= 0 || IsItemTargetingActive) return;

            var enemy = cell.OccupyingEnemy;
            if (enemy == null || enemy.CurrentHP <= 0) return;
            if (_hoveredEnemy == enemy) return;

            ClearUnitAttackPreviews();
            _hoveredEnemy = enemy;

            if (enemy.Behavior == null) return;

            AddUnitPreviews(enemy.Behavior.GetAttackPreviewPositions(enemy), attackRangeSprite);
            AddUnitPreviews(enemy.Behavior.GetSkillPreviewPositions(enemy),  skillRangeSprite);
            AddUnitPreviews(enemy.Behavior.GetMovePreviewPositions(enemy),   moveRangeSprite);
        }

        private void OnEnemyCellHoverExit(CombatBoardCell cell)
        {
            if (pendingCard != null || pendingCardIndex >= 0 || IsItemTargetingActive) return;
            if (_hoveredEnemy != null && cell.OccupyingEnemy == _hoveredEnemy)
            {
                ClearUnitAttackPreviews();
                _hoveredEnemy = null;
            }
        }

        /// <summary>카드 hover sprite 미리보기 제거. targeting 모드 중에는 셀 상태를 건드리지 않는다.</summary>
        private void ClearCardRangePreview()
        {
            foreach (var obj in hoverPreviewObjects)
                if (obj != null) Destroy(obj);
            hoverPreviewObjects.Clear();
        }

        /// <summary>현재 hoverPreviewObjects의 스프라이트를 교체합니다 (Skill/Power collapse 전환용).</summary>
        private void SetRangePreviewSprite(Sprite sprite)
        {
            if (sprite == null) return;
            foreach (var obj in hoverPreviewObjects)
            {
                if (obj == null) continue;
                var img = obj.GetComponent<Image>();
                if (img != null) img.sprite = sprite;
            }
        }

        /// <summary>
        /// TargetType에 따라 셀 하이라이트 상태 결정
        /// Enemy: 적 있는 셀만 클릭 가능, 빈 셀은 InRange 표시만
        /// Any:   모든 셀 클릭 가능 (AreaSelectable)
        /// All:   하이라이트만 (클릭 불가, 자동 실행)
        /// Ally:  자신/아군 셀만 클릭 가능
        /// </summary>
        private CellState GetHighlightState(CombatBoardCell cell, TargetType targetType)
        {
            bool hasEnemy = cell.OccupyingEnemy != null && cell.OccupyingEnemy.CurrentHP > 0;
            bool hasAlly  = cell.OccupyingAlly != null || cell.IsPlayerHere;
            return targetType switch
            {
                TargetType.Enemy => hasEnemy ? CellState.TargetableEnemy : CellState.InRange,
                TargetType.All   => CellState.AreaSelectable,
                TargetType.Any   => hasEnemy ? CellState.InRange : CellState.AreaSelectable, // 적 있는 셀은 이동 불가
                TargetType.Ally  => hasAlly  ? CellState.TargetableAlly  : CellState.InRange,
                _                => CellState.InRange,
            };
        }

        private void ClearAllHighlights()
        {
            if (grid == null) return;
            for (int col = 0; col < boardCols; col++)
                for (int row = 0; row < boardRows; row++)
                    grid[col, row].ClearHighlight();
        }

        /// <param name="returnToHand">true=취소(손패 복귀), false=카드 실행 직전(ReturnToHand 생략)</param>
        private void CancelTargeting(bool returnToHand = true)
        {
            if (pendingCardIndex >= 0 && pendingCardIndex < handCardObjects.Count)
            {
                var cardUI = handCardObjects[pendingCardIndex]?.GetComponent<DeckRoguelike.Cards.CardUI>();
                if (cardUI != null)
                {
                    cardUI.SetLocked(false);
                    if (returnToHand) cardUI.ReturnToHand();
                }
            }

            pendingCard      = null;
            pendingCardIndex = -1;
            SetCombatButtonsInteractable(true);
            ClearCardRangePreview();
            ClearAllHighlights();
        }

        private void SetCombatButtonsInteractable(bool interactable)
        {
            SetButtonRaycast(endTurnButton,      interactable);
            SetButtonRaycast(drawPileButton,     interactable);
            SetButtonRaycast(discardPileButton,  interactable);
        }

        private void SetButtonRaycast(Button btn, bool enable)
        {
            if (btn == null) return;
            var cg = btn.GetComponent<CanvasGroup>();
            if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = enable;
        }

        /// <summary>
        /// 셀 클릭 처리.
        /// - 첫 번째 효과가 Enemy → 적이 있는 셀만 허용
        /// - Any → 범위 내 모든 셀 허용
        /// - Ally → 자신/아군 셀만 허용
        /// 선택된 셀 좌표(selectedPos)를 ExecuteCard에 전달하면
        /// 각 효과가 자신의 targeting으로 대상을 결정한다.
        /// </summary>
        private void HandleCellClicked(CombatBoardCell cell)
        {
            // 아이템 타겟팅 모드: 카드 타겟팅보다 우선
            if (isItemMoveMode)            { HandleItemMoveCellClicked(cell);     return; }
            if (isItemTeleportMode)        { HandleItemTeleportCellClicked(cell); return; }
            if (isItemEnemyTargetingMode)  { HandleItemEnemyCellClicked(cell);    return; }
            if (isItemAreaTargetingMode)   { HandleItemAreaCellClicked(cell);     return; }

            if (pendingCard == null || pendingCard.Effects == null || pendingCard.Effects.Count == 0) return;

            var firstEffect = pendingCard.Effects[0];

            // Self/Random/All 카드: 셀 위치 무관하게 즉시 실행
            if (firstEffect.targeting == TargetType.Self ||
                firstEffect.targeting == TargetType.Random ||
                firstEffect.targeting == TargetType.All)
            {
                int noRangeIndex = pendingCardIndex;
                CancelTargeting(returnToHand: false);
                ExecuteCard(noRangeIndex, null);
                return;
            }

            // effects[0]의 rangeOffsets 기준으로 클릭 가능 범위 제한 (빈 배열은 범위 제한 없음)
            if (firstEffect.rangeOffsets != null && firstEffect.rangeOffsets.Length > 0)
            {
                bool inRange = false;
                foreach (var offset in firstEffect.rangeOffsets)
                {
                    Vector2Int pos = firstEffect.useAbsoluteCoords
                        ? offset.ToVector2Int()
                        : playerSpawnCell + offset.ToVector2Int();
                    if (pos == cell.GridPos) { inRange = true; break; }
                }
                if (!inRange) return;
            }

            // Enemy: 적 있는 셀만 선택 가능 / Ally: 자신/아군 셀만 선택 가능
            if (firstEffect.targeting == TargetType.Enemy)
            {
                if (cell.OccupyingEnemy == null || cell.OccupyingEnemy.CurrentHP <= 0) return;
            }
            else if (firstEffect.targeting == TargetType.Ally)
            {
                if (!cell.IsPlayerHere && cell.OccupyingAlly == null) return;
            }

            int savedIndex         = pendingCardIndex;
            Vector2Int selectedPos = cell.GridPos;
            CancelTargeting(returnToHand: false); // 카드 실행 → ReturnToHand 불필요, DiscardAndDestroy가 처리
            ExecuteCard(savedIndex, selectedPos);
        }

        public bool IsInBoard(Vector2Int pos)
            => pos.x >= 0 && pos.x < boardCols && pos.y >= 0 && pos.y < boardRows;

        /// <summary>해당 셀이 보드 내에 있고 적이 이동 가능한 빈 칸인지 확인합니다 (플레이어·아군·적 없음).</summary>
        public bool IsCellFreeForEnemy(Vector2Int pos)
        {
            if (!IsInBoard(pos)) return false;
            CombatBoardCell cell = grid[pos.x, pos.y];
            return !cell.IsPlayerHere && cell.OccupyingEnemy == null && cell.OccupyingAlly == null;
        }

        #endregion

        #region Victory / Defeat

        private void CheckVictory()
        {
            foreach (var enemy in enemies)
                if (enemy.CurrentHP > 0) return;

            combatState = CombatState.Victory;
            PlaySound(victorySound);
            Debug.Log("[CombatController] 전투 승리!");
            SetCombatHudActive(false);
            FireRelicHook((r, ctx) => r.OnCombatVictory(ctx));
            OnCombatEnded?.Invoke(true);
        }

        /// <summary>combatHudElements를 일괄 활성/비활성화합니다.</summary>
        private void SetCombatHudActive(bool active)
        {
            foreach (var obj in combatHudElements)
                if (obj != null) obj.SetActive(active);
        }

        /// <summary>
        /// 공통 떠나기 버튼을 활성/비활성화합니다.
        /// active=true 시 기존 리스너를 제거하고 onClick을 새로 등록합니다.
        /// </summary>
        private void SetLeaveButtonActive(bool active, System.Action onClick = null)
        {
            if (leaveButton == null) return;
            leaveButton.gameObject.SetActive(active);
            if (active && onClick != null)
            {
                leaveButton.onClick.RemoveAllListeners();
                leaveButton.onClick.AddListener(() => onClick());
            }
        }

        #endregion

        #region Relic Hooks

        /// <summary>플레이어 현재 그리드 위치 (EnemyBehavior에서 인접 판별 용도)</summary>
        public Vector2Int PlayerSpawnCell  => playerSpawnCell;
        public int        PlayerStrength   => playerStrength;

        /// <summary>해당 좌표에 있는 적을 반환합니다. 없으면 null.</summary>
        public EnemyInstance GetEnemyAt(Vector2Int pos)
            => IsInBoard(pos) ? grid[pos.x, pos.y].OccupyingEnemy : null;

        /// <summary>플레이어를 지정 위치로 이동합니다. 보드 밖이거나 막힌 경우 무시됩니다.</summary>
        public void TryMovePlayerTo(Vector2Int pos) => MovePlayer(pos);

        /// <summary>보유 중인 모든 유물의 훅을 순서대로 호출합니다.</summary>
        private void FireRelicHook(System.Action<RelicEffect, RelicCombatContext> hook)
        {
            if (GameManager.Instance == null) return;
            var ctx = new RelicCombatContext { Combat = this };
            foreach (var relic in GameManager.Instance.Relics)
                hook(relic, ctx);
        }

        #endregion

        #region Player Stat Modifiers

        /// <summary>힘 수치를 증가시킵니다. Damage 효과에 자동 반영됩니다.</summary>
        public void AddStrength(int amount)
        {
            playerStrength += amount;
            RefreshHandDisplay();
            unitUI?.SetEffect("strength", strengthIcon, playerStrength);
        }

        /// <summary>민첩 수치를 증가시킵니다. Block 효과에 자동 반영됩니다.</summary>
        public void AddDexterity(int amount)
        {
            playerDexterity += amount;
            RefreshHandDisplay();
            unitUI?.SetEffect("dexterity", dexterityIcon, playerDexterity);
        }

        /// <summary>
        /// 플레이어에게 피해를 줍니다.
        /// 유물 OnPlayerDamaged 훅 → 방어도 차감 → GameManager.TakeDamage 순서로 처리합니다.
        /// 실제로 받은 피해량을 반환합니다.
        /// </summary>
        /// <summary>
        /// 특정 좌표에 플레이어가 있을 때만 피해를 줍니다.
        /// 플레이어가 해당 위치에 없으면 공격이 빗나가 0을 반환합니다.
        /// </summary>
        public int ApplyDamageToPlayerAt(int rawDamage, Vector2Int attackPos, EnemyInstance attacker = null)
        {
            if (attackPos != PlayerSpawnCell)
            {
                Debug.Log($"[CombatController] 공격 좌표 {attackPos}에 플레이어 없음 → 공격 빗나감");
                return 0;
            }
            return ApplyDamageToPlayer(rawDamage, attacker);
        }

        public int ApplyDamageToPlayer(int rawDamage, EnemyInstance attacker = null)
        {
            // 회피 (군번줄): 공격 무효화
            if (dodgeCharges > 0)
            {
                Debug.Log("[CombatController] 회피! 공격 무효화");
                return 0;
            }

            // 유물 훅: 피해량 수정 가능
            if (GameManager.Instance != null)
            {
                var ctx = new RelicCombatContext { Combat = this };
                foreach (var relic in GameManager.Instance.Relics)
                    relic.OnPlayerDamaged(ctx, ref rawDamage);
            }

            // 받는 피해 배율 (철가면)
            rawDamage = Mathf.RoundToInt(rawDamage * incomingDamageMultiplier);
            rawDamage = Mathf.Max(0, rawDamage);

            int blockedDamage = Mathf.Min(playerBlock, rawDamage);
            int actualDamage  = rawDamage - blockedDamage;
            playerBlock -= blockedDamage;
            UpdateBlockDisplay();

            if (actualDamage > 0)
            {
                // 소생의 팬던트: 사망할 경우 발동
                if (revivePassiveItem != null && GameManager.Instance != null
                    && GameManager.Instance.CurrentHP - actualDamage <= 0)
                {
                    var revive = revivePassiveItem;
                    revivePassiveItem = null;
                    GameManager.Instance.RemoveItem(revive);
                    GameManager.Instance.SetHP(1);
                    int healAmt = Mathf.RoundToInt(GameManager.Instance.MaxHP * reviveHealPercent);
                    GameManager.Instance.Heal(healAmt);
                    Debug.Log($"[소생의 팬던트] 발동! HP {healAmt} 회복");
                    return actualDamage;
                }

                GameManager.Instance?.TakeDamage(actualDamage);
            }

            // 파워 훅: HP 손실
            if (actualDamage > 0)
                foreach (var p in _activePowers) p.OnPlayerLostHp(this, actualDamage);

            // 반사 유물 훅: 확정 피해량 + 공격한 적 정보 전달
            if (actualDamage > 0 && attacker != null && GameManager.Instance != null)
            {
                var reflectCtx = new RelicCombatContext { Combat = this };
                foreach (var relic in GameManager.Instance.Relics)
                    relic.OnPlayerDamagedBy(reflectCtx, actualDamage, attacker);
            }

            return actualDamage;
        }

        // ── 파워 카드 시스템 ──────────────────────────────────────────────────

        /// <summary>파워 카드 지속 효과를 전투에 등록합니다.</summary>
        public void RegisterPower(CombatPowerEffect power) => _activePowers.Add(power);

        /// <summary>카드 효과로 HP를 잃지 않는 상태 여부</summary>
        public bool NoCardHpLoss => _noCardHpLoss;

        /// <summary>카드 효과의 HP 손실 면역을 설정합니다.</summary>
        public void SetNoCardHpLoss(bool value) => _noCardHpLoss = value;

        /// <summary>카드 소멸 면역을 설정합니다.</summary>
        public void SetNoExhaust(bool value) => _noExhaust = value;

        /// <summary>카드 효과로 HP 손실 시 파워 훅을 발동합니다.</summary>
        public void FirePowerOnPlayerLostHp(int amount)
        {
            foreach (var p in _activePowers) p.OnPlayerLostHp(this, amount);
        }

        /// <summary>현재 에너지를 증가시킵니다. 최대 에너지를 초과하지 않습니다.</summary>
        public void AddEnergy(int amount)
        {
            currentEnergy += amount;
            UpdateEnergyDisplay();
        }

        /// <summary>최대 에너지를 증가시킵니다. 현재 에너지도 같이 올라갑니다.</summary>
        public void AddMaxEnergy(int amount)
        {
            maxEnergy     += amount;
            currentEnergy += amount;
            UpdateEnergyDisplay();
        }

        /// <summary>즉시 방어도(Block)를 증가시킵니다. 아이템 효과에서 사용합니다.</summary>
        public void AddBlock(int amount)
        {
            playerBlock += amount;
            UpdateBlockDisplay();
        }

        /// <summary>즉시 카드를 추가로 드로우합니다. 아이템 효과에서 사용합니다.</summary>
        public void DrawExtraCards(int count)
        {
            DrawCards(count);
        }

        /// <summary>턴 시작 드로우 수를 증가시킵니다. 유물 OnCombatStart에서 설정합니다.</summary>
        public void AddBonusHandSize(int amount)
        {
            _bonusHandSize += amount;
            Debug.Log($"[CombatController] 턴 시작 추가 드로우 +{amount} (총 보너스: {_bonusHandSize})");
        }

        /// <summary>이번 턴 카드 비용을 amount만큼 낮춥니다. 유물/아이템 효과에서 사용합니다.</summary>
        public void AddCardCostReduction(int amount)
        {
            _cardCostReduction += amount;
            RefreshCardPlayability();
            RefreshHandDisplay();
            Debug.Log($"[CombatController] 카드 비용 -{amount} 적용 (총 감소: {_cardCostReduction})");
        }

        private void RefreshHandDisplay()
        {
            for (int i = 0; i < handCardObjects.Count; i++)
            {
                var cardUI = handCardObjects[i]?.GetComponent<CardUI>();
                if (cardUI != null && i < hand.Count)
                    cardUI.Initialize(hand[i], playerStrength, playerDexterity, _cardCostReduction);
            }
        }

        // ── 아이템 전용 전투 API ─────────────────────────────────────────

        /// <summary>범위 내 모든 적에게 피해를 줍니다 (폭탄 등).</summary>
        public void DealDamageInArea(Vector2Int center, int halfRange, int damage)
        {
            for (int dc = -halfRange; dc <= halfRange; dc++)
                for (int dr = -halfRange; dr <= halfRange; dr++)
                {
                    var pos = center + new Vector2Int(dc, dr);
                    if (!IsInBoard(pos)) continue;
                    var e = grid[pos.x, pos.y].OccupyingEnemy;
                    if (e != null && e.CurrentHP > 0) DamageEnemy(e, damage);
                }
            CheckVictory();
        }

        /// <summary>살아있는 모든 적에게 피해를 줍니다.</summary>
        public void DealDamageToAllEnemies(int damage)
        {
            foreach (var e in enemies)
                if (e.CurrentHP > 0) DamageEnemy(e, damage);
            CheckVictory();
        }

        /// <summary>무작위 적 1명에게 피해를 줍니다.</summary>
        public void DealDamageToRandomEnemy(int damage)
        {
            var alive = enemies.FindAll(e => e.CurrentHP > 0);
            if (alive.Count == 0) return;
            DamageEnemy(alive[UnityEngine.Random.Range(0, alive.Count)], damage);
            CheckVictory();
        }

        /// <summary>현재 손패를 읽기 전용으로 반환합니다 (카드 포션용).</summary>
        public IReadOnlyList<DeckRoguelike.Cards.CardData> GetHand() => hand;

        /// <summary>손패에서 무작위로 count장을 소멸시킵니다 (영혼빼기 등).</summary>
        public void ExhaustRandomHandCard(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (hand.Count == 0) break;
                int idx = UnityEngine.Random.Range(0, hand.Count);
                var card = hand[idx];
                hand.RemoveAt(idx);
                if (idx < handCardObjects.Count)
                {
                    var obj = handCardObjects[idx];
                    handCardObjects.RemoveAt(idx);
                    if (obj != null) Destroy(obj);
                }
                if (_noExhaust)
                    deckManager?.AddToDiscardPile(card);
                else
                {
                    deckManager?.ExhaustCard(card);
                    foreach (var p in _activePowers) p.OnCardExhausted(this);
                }
            }
            RepositionHandCards();
            UpdatePileCounters();
        }

        /// <summary>
        /// 손패의 카드를 전부 버림 더미로 보냅니다.
        /// 현재 플레이 중인 카드(_playingHandIndex)는 건너뛰고 버린 카드 수를 반환합니다.
        /// </summary>
        public int DiscardHandCards()
        {
            int skipIdx = _playingHandIndex;
            int count = 0;
            var objsToDiscard = new List<GameObject>();

            for (int i = hand.Count - 1; i >= 0; i--)
            {
                if (i == skipIdx) continue;
                deckManager?.AddToDiscardPile(hand[i]);
                hand.RemoveAt(i);
                if (i < handCardObjects.Count)
                {
                    objsToDiscard.Add(handCardObjects[i]);
                    handCardObjects.RemoveAt(i);
                }
                count++;
            }

            // 역순 제거 후 플레이 카드만 남으면 인덱스는 항상 0
            if (skipIdx >= 0 && hand.Count > 0)
                _playingHandIndex = 0;

            if (discardPileButton != null)
            {
                Vector3 discardPos = discardPileButton.transform.position;
                foreach (var obj in objsToDiscard)
                {
                    if (obj == null) continue;
                    var ui = obj.GetComponent<CardUI>();
                    if (ui != null) StartCoroutine(DiscardAndDestroy(ui, discardPos));
                    else Destroy(obj);
                }
            }
            else
            {
                foreach (var obj in objsToDiscard)
                    if (obj != null) Destroy(obj);
            }

            UpdatePileCounters();
            RepositionHandCards();
            return count;
        }

        /// <summary>적에게 dmgPerHit 피해를 times번, delay초 간격으로 순차 적용합니다.</summary>
        public void StartSequentialDamage(EnemyInstance enemy, int dmgPerHit, int times, float delay = 0.15f)
        {
            StartCoroutine(SequentialDamageCoroutine(enemy, dmgPerHit, times, delay));
        }

        private IEnumerator SequentialDamageCoroutine(EnemyInstance enemy, int dmgPerHit, int times, float delay)
        {
            for (int i = 0; i < times; i++)
            {
                if (enemy.CurrentHP <= 0) yield break;
                DamageEnemy(enemy, dmgPerHit);
                if (i < times - 1)
                    yield return new WaitForSeconds(delay);
            }
        }

        /// <summary>
        /// 손패 카드를 하나씩 버리면서 매번 적에게 dmgPerDiscard 피해를 줍니다.
        /// 플레이 중인 카드(_playingHandIndex)는 건너뜁니다.
        /// </summary>
        public void StartDiscardAndDamageSequence(EnemyInstance enemy, int dmgPerDiscard, float delay = 0.2f)
        {
            int skipIdx = _playingHandIndex;

            // 버릴 카드 목록 캡처 (순서 유지)
            var toDiscard = new List<(CardData card, GameObject obj)>();
            for (int i = 0; i < hand.Count; i++)
            {
                if (i == skipIdx) continue;
                var obj = i < handCardObjects.Count ? handCardObjects[i] : null;
                toDiscard.Add((hand[i], obj));
            }

            // 데이터 즉시 제거 (역순으로 인덱스 안전하게)
            for (int i = hand.Count - 1; i >= 0; i--)
            {
                if (i == skipIdx) continue;
                deckManager?.AddToDiscardPile(hand[i]);
                hand.RemoveAt(i);
                if (i < handCardObjects.Count)
                    handCardObjects.RemoveAt(i);
            }

            if (skipIdx >= 0 && hand.Count > 0)
                _playingHandIndex = 0;

            UpdatePileCounters();
            RepositionHandCards();

            StartCoroutine(DiscardAndDamageCoroutine(enemy, dmgPerDiscard, toDiscard, delay));
        }

        private IEnumerator DiscardAndDamageCoroutine(EnemyInstance enemy, int dmgPerDiscard,
            List<(CardData card, GameObject obj)> cards, float delay)
        {
            Vector3 discardPos = discardPileButton != null
                ? discardPileButton.transform.position
                : Vector3.zero;

            foreach (var (_, obj) in cards)
            {
                // 카드 버리기 애니메이션
                if (obj != null)
                {
                    var ui = obj.GetComponent<CardUI>();
                    if (ui != null && discardPileButton != null)
                        StartCoroutine(DiscardAndDestroy(ui, discardPos));
                    else
                        Destroy(obj);
                }

                // 버린 직후 데미지
                if (enemy.CurrentHP > 0)
                    DamageEnemy(enemy, dmgPerDiscard);

                yield return new WaitForSeconds(delay);
            }
        }

        /// <summary>카드를 손패에 추가합니다. 비용이 0인 복사본으로 추가됩니다.</summary>
        public void AddCardToHandFree(DeckRoguelike.Cards.CardData card)
        {
            if (card == null || hand.Count >= 10) return;
            var free = card.Clone();
            free.energyCost = 0;
            hand.Add(free);
            var obj = CreateSingleCardObject(free);
            if (obj != null) handCardObjects.Add(obj);
            RepositionHandCards();
            UpdatePileCounters();
        }

        /// <summary>손패에 있는 카드의 비용을 0으로 만듭니다 (에이스 카드용).</summary>
        public void SetHandCardFree(DeckRoguelike.Cards.CardData card)
        {
            int idx = hand.IndexOf(card);
            if (idx < 0) return;
            var free = card.Clone();
            free.energyCost = 0;
            hand[idx] = free;
            if (idx < handCardObjects.Count)
                handCardObjects[idx]?.GetComponent<DeckRoguelike.Cards.CardUI>()
                    ?.Initialize(free, playerStrength, playerDexterity, _cardCostReduction);
            RefreshCardPlayability();
        }

        /// <summary>손패의 모든 카드를 영구 강화합니다.</summary>
        public void UpgradeAllHandCards()
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i].IsUpgraded) continue;
                var upgraded = DeckRoguelike.Core.CardRegistry.GetCard(hand[i].UpgradedCode);
                if (upgraded == null) continue;
                var clone = upgraded.Clone();
                clone.energyCost = hand[i].energyCost;
                deckManager?.UpgradeCard(hand[i]);
                hand[i] = clone;
                if (i < handCardObjects.Count)
                    handCardObjects[i]?.GetComponent<DeckRoguelike.Cards.CardUI>()
                        ?.Initialize(clone, playerStrength, playerDexterity, _cardCostReduction);
            }
            RefreshCardPlayability();
        }

        /// <summary>손패가 꽉 찰 때까지 카드를 드로우합니다.</summary>
        public void DrawUntilFull()
        {
            int count = 10 - hand.Count;
            if (count > 0) DrawCards(count);
        }

        /// <summary>손패 모든 카드의 비용을 0~3으로 무작위 변경합니다.</summary>
        public void RandomizeAllHandCosts()
        {
            for (int i = 0; i < hand.Count; i++)
            {
                var clone = hand[i].Clone();
                clone.energyCost = UnityEngine.Random.Range(0, 4);
                hand[i] = clone;
                if (i < handCardObjects.Count)
                    handCardObjects[i]?.GetComponent<DeckRoguelike.Cards.CardUI>()
                        ?.Initialize(clone, playerStrength, playerDexterity, _cardCostReduction);
            }
            RefreshCardPlayability();
        }

        /// <summary>드로우 더미 맨 위부터 count장을 자동으로 실행합니다 (돌림판).</summary>
        public void PlayTopCardsFromDraw(int count)
        {
            StartCoroutine(PlayTopCardsCoroutine(count));
        }

        private System.Collections.IEnumerator PlayTopCardsCoroutine(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var card = deckManager?.DrawCard();
                if (card == null) break;
                ApplyCardEffects(card, null);
                if (!card.Exhaust || _noExhaust)
                    deckManager?.AddToDiscardPile(card);
                else
                {
                    deckManager?.ExhaustCard(card);
                    foreach (var p in _activePowers) p.OnCardExhausted(this);
                }
                CheckVictory();
                if (combatState != CombatState.PlayerTurn) yield break;
                yield return new WaitForSeconds(0.25f);
            }
            UpdatePileCounters();
        }

        /// <summary>다음에 사용하는 공격 카드의 피해를 multiplier배로 만듭니다 (분노의 포션).</summary>
        public void SetNextAttackMultiplier(int multiplier)
        {
            nextAttackDamageMultiplier = multiplier;
        }

        /// <summary>다음 카드를 2회 실행하도록 설정합니다 (재사용 포션).</summary>
        public void SetNextCardDoublePlay()
        {
            nextCardDoublePlay = true;
        }

        /// <summary>현재 방어도를 3배로 늘립니다 (강철화 포션).</summary>
        public void TripleCurrentBlock()
        {
            playerBlock *= 3;
            UpdateBlockDisplay();
        }

        /// <summary>이번 턴 데미지 배율을 설정합니다 (철가면). 다음 플레이어 턴 시작 시 초기화됩니다.</summary>
        public void SetTurnDamageMultipliers(float outgoing, float incoming)
        {
            outgoingDamageMultiplier = outgoing;
            incomingDamageMultiplier = incoming;
        }

        /// <summary>이번 턴에만 힘을 추가합니다. 턴 종료 시 자동 회수됩니다 (초코바).</summary>
        public void AddTempStrength(int amount)
        {
            playerStrength       += amount;
            tempStrengthFromItem += amount;
            RefreshHandDisplay();
            unitUI?.SetEffect("strength", strengthIcon, playerStrength);
        }

        /// <summary>회피 횟수를 추가합니다 (군번줄). 공격 1회를 무효화합니다.</summary>
        public void AddDodge(int charges)
        {
            dodgeCharges += charges;
        }

        /// <summary>소생의 팬던트를 패시브로 등록합니다. 사망 시 자동 발동됩니다.</summary>
        public void RegisterRevivePassive(ItemData item, float healPercent)
        {
            revivePassiveItem = item;
            reviveHealPercent = healPercent;
        }

        /// <summary>플레이어 자신의 모든 해로운 상태이상과 카드 제한을 제거합니다 (성수).</summary>
        public void RemovePlayerDebuffs()
        {
            playerStatusEffects.RemoveAll(s =>
                s.Type == StatusEffectType.Fire ||
                s.Type == StatusEffectType.Stun ||
                s.Type == StatusEffectType.Freeze);
            cardTypeRestrictions.Clear();
            unitUI?.RemoveEffect("fire");
            unitUI?.RemoveEffect("stun");
            unitUI?.RemoveEffect("freeze");
            Debug.Log("[CombatController] 플레이어 디버프 모두 제거");
        }

        /// <summary>보스가 아닌 전투에서 도망칩니다 (연막탄).</summary>
        public bool EscapeCombat()
        {
            if (GameManager.Instance?.IsBossEncounter == true)
            {
                Debug.Log("[연막탄] 보스전에서는 사용 불가");
                return false;
            }
            combatState = CombatState.Victory;
            SetCombatHudActive(false);
            InGameUIController.Instance?.OpenMap();
            Debug.Log("[연막탄] 전투에서 도망!");
            return true;
        }

        /// <summary>살아있는 적 목록을 반환합니다 (아이템 효과용).</summary>
        public List<EnemyInstance> GetAliveEnemies()
        {
            return enemies.FindAll(e => e.CurrentHP > 0);
        }

        /// <summary>아군 목록을 반환합니다 (뼈피리 등).</summary>
        public IReadOnlyList<AllyInstance> GetAllies() => allies;

        /// <summary>적에게 상태이상을 부여합니다. Fire는 스택이 누적됩니다.</summary>
        public void ApplyStatus(EnemyInstance enemy, StatusEffectType type, int stacks)
        {
            if (enemy == null || enemy.CurrentHP <= 0) return;
            var existing = enemy.StatusEffects.Find(s => s.Type == type);
            if (existing != null)
                existing.Stacks += stacks;
            else
                enemy.StatusEffects.Add(new StatusEntry { Type = type, Stacks = stacks });

            var entry = enemy.StatusEffects.Find(s => s.Type == type);
            string key   = type switch { StatusEffectType.Fire => "fire", StatusEffectType.Stun => "stun", _ => "freeze" };
            Sprite icon  = type switch { StatusEffectType.Fire => fireIcon, StatusEffectType.Stun => stunIcon, _ => freezeIcon };
            enemy.UI?.SetEffect(key, icon, entry.Stacks);
        }

        /// <summary>살아있는 모든 적에게 상태이상을 부여합니다.</summary>
        public void ApplyStatusToAllEnemies(StatusEffectType type, int stacks)
        {
            foreach (var e in enemies)
                if (e.CurrentHP > 0) ApplyStatus(e, type, stacks);
        }

        /// <summary>
        /// 아이템 포션 카드 선택 UI를 엽니다 (CardRewardPanel 재활용).
        /// 선택 후 또는 닫힌 후 callback이 호출됩니다.
        /// </summary>
        public void OpenItemCardSelection(List<DeckRoguelike.Cards.CardData> cards,
                                          System.Action<DeckRoguelike.Cards.CardData> callback)
        {
            if (cards == null || cards.Count == 0) { callback?.Invoke(null); return; }
            InGameUIController.Instance?.OpenItemCardSelection(cards, callback);
        }

        /// <summary>이동 포션: 인접 1칸 이동 셀 선택 모드로 진입합니다.</summary>
        public void EnterItemMoveMode()
        {
            if (combatState != CombatState.PlayerTurn || grid == null) return;
            if (pendingCard != null) CancelTargeting(returnToHand: true);
            isItemMoveMode    = true;
            isItemTeleportMode = false;
            isItemEnemyTargetingMode = false;
            isItemAreaTargetingMode  = false;
            ClearAllHighlights();
            ClearUnitAttackPreviews();
            ClearItemHoverPreview();

            int[] dx = { -1, 1, 0, 0 };
            int[] dy = {  0, 0,-1, 1 };
            for (int i = 0; i < 4; i++)
            {
                var pos = playerSpawnCell + new Vector2Int(dx[i], dy[i]);
                if (!IsInBoard(pos)) continue;
                if (grid[pos.x, pos.y].OccupyingEnemy != null) continue;
                if (grid[pos.x, pos.y].OccupyingAlly  != null) continue;
                grid[pos.x, pos.y].SetState(DeckRoguelike.UI.CellState.AreaSelectable);
                // 이동 범위 스프라이트 미리보기
                if (moveRangeSprite != null)
                    AddPreviewObject(pos, moveRangeSprite, itemHoverPreviewObjects);
            }
        }

        /// <summary>순간이동 포션: 보드 내 빈 칸 어디로든 이동 모드로 진입합니다.</summary>
        public void EnterItemTeleportMode()
        {
            if (combatState != CombatState.PlayerTurn || grid == null) return;
            if (pendingCard != null) CancelTargeting(returnToHand: true);
            isItemMoveMode    = false;
            isItemTeleportMode = true;
            ClearAllHighlights();
            ClearUnitAttackPreviews();

            for (int col = 0; col < boardCols; col++)
                for (int row = 0; row < boardRows; row++)
                {
                    var cell = grid[col, row];
                    if (!cell.IsPlayerHere && cell.OccupyingEnemy == null && cell.OccupyingAlly == null)
                        cell.SetState(DeckRoguelike.UI.CellState.AreaSelectable);
                }
        }

        public void SetItemMoveModeConfirmCallback(System.Action onConfirmedUse)
            => _itemConfirmedUseCallback = onConfirmedUse;

        private void HandleItemMoveCellClicked(CombatBoardCell cell)
        {
            var pos = cell.GridPos;
            int dx = Mathf.Abs(pos.x - playerSpawnCell.x);
            int dy = Mathf.Abs(pos.y - playerSpawnCell.y);
            if (dx + dy != 1) return;                        // 인접 1칸만 허용
            if (cell.OccupyingEnemy != null) return;
            if (cell.OccupyingAlly  != null) return;

            isItemMoveMode = false;
            ClearItemHoverPreview();
            ClearAllHighlights();

            _itemConfirmedUseCallback?.Invoke();
            FinishItemSlot();
            _itemConfirmedUseCallback = null;

            MovePlayer(pos);
            grid[playerSpawnCell.x, playerSpawnCell.y].SetState(CellState.PlayerOccupied);

        }

        private void HandleItemTeleportCellClicked(CombatBoardCell cell)
        {
            if (cell.IsPlayerHere || cell.OccupyingEnemy != null || cell.OccupyingAlly != null) return;

            isItemTeleportMode = false;
            ClearAllHighlights();
            MovePlayer(cell.GridPos);
            grid[playerSpawnCell.x, playerSpawnCell.y].SetState(CellState.PlayerOccupied);

        }

        // ── 아이템 타겟팅 모드 ────────────────────────────────────────────

        /// <summary>현재 어떤 아이템 타겟팅 모드라도 활성화 중인지 여부 (ConfirmUseItemPanel에서 참조).</summary>
        public bool IsItemTargetingActive =>
            isItemMoveMode || isItemTeleportMode || isItemEnemyTargetingMode || isItemAreaTargetingMode;

        /// <summary>타겟팅이 시작된 아이템 슬롯을 등록합니다. 슬롯은 숨기지 않고 클릭 시 취소 가능 상태로 유지됩니다.</summary>
        public void SetPendingItemSlot(ItemSlotUI slot)
        {
            _pendingItemSlot = slot;
            InGameUIController.Instance?.SetItemTargetingBlocker(true);
        }

        /// <summary>적 선택 아이템 타겟팅 모드 진입 (화살·화염병·섬광탄·드라이아이스).</summary>
        public void EnterItemEnemyTargetingMode(System.Action<EnemyInstance> onSelected,
                                                System.Action<Vector2Int> _,
                                                System.Action onCancel,
                                                System.Action onConfirmedUse)
        {
            if (combatState != CombatState.PlayerTurn || grid == null) return;
            if (pendingCard != null) CancelTargeting(returnToHand: true);
            isItemMoveMode           = false;
            isItemTeleportMode       = false;
            isItemEnemyTargetingMode = true;
            isItemAreaTargetingMode  = false;
            _itemEnemyCallback        = onSelected;
            _itemTargetCancelCallback = onCancel;
            _itemConfirmedUseCallback = onConfirmedUse;
            ClearAllHighlights();
            ClearUnitAttackPreviews();
            ClearItemHoverPreview();

            // 살아있는 적 셀 하이라이트 (TargetableEnemy)
            foreach (var e in enemies)
                if (e.CurrentHP > 0 && IsInBoard(e.GridPos))
                    grid[e.GridPos.x, e.GridPos.y].SetState(CellState.TargetableEnemy);

            // 셀 hover 이벤트 구독
            SubscribeCellHoverForItemTargeting();
        }

        /// <summary>영역 선택 아이템 타겟팅 모드 진입 (폭탄).</summary>
        public void EnterItemAreaTargetingMode(System.Action<Vector2Int> onSelected,
                                               System.Action onCancel,
                                               System.Action onConfirmedUse)
        {
            if (combatState != CombatState.PlayerTurn || grid == null) return;
            if (pendingCard != null) CancelTargeting(returnToHand: true);
            isItemMoveMode           = false;
            isItemTeleportMode       = false;
            isItemEnemyTargetingMode = false;
            isItemAreaTargetingMode  = true;
            _itemAreaCallback         = onSelected;
            _itemTargetCancelCallback = onCancel;
            _itemConfirmedUseCallback = onConfirmedUse;
            ClearAllHighlights();
            ClearUnitAttackPreviews();
            ClearItemHoverPreview();

            // 보드 전체 셀 AreaSelectable
            for (int col = 0; col < boardCols; col++)
                for (int row = 0; row < boardRows; row++)
                    grid[col, row].SetState(CellState.AreaSelectable);

            SubscribeCellHoverForItemTargeting();
        }

        /// <summary>버튼에 PointerEnter 이벤트 추가 — hover 시 아이템 타겟팅 활성 중이면 취소합니다.</summary>
        private void AddItemCancelOnHover(Button btn)
        {
            if (btn == null) return;
            var trigger = btn.GetComponent<UnityEngine.EventSystems.EventTrigger>()
                       ?? btn.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var entry = new UnityEngine.EventSystems.EventTrigger.Entry
                { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => { if (IsItemTargetingActive) CancelItemTargeting(); });
            trigger.triggers.Add(entry);
        }

        /// <summary>아이템 타겟팅 취소 (우클릭 또는 버튼 hover).</summary>
        public void CancelItemTargeting()
        {
            bool wasActive = IsItemTargetingActive;
            isItemMoveMode           = false;
            isItemTeleportMode       = false;
            isItemEnemyTargetingMode = false;
            isItemAreaTargetingMode  = false;

            UnsubscribeCellHoverForItemTargeting();
            ClearItemHoverPreview();
            ClearAllHighlights();


            _pendingItemSlot = null;
            InGameUIController.Instance?.SetItemTargetingBlocker(false);

            if (wasActive) _itemTargetCancelCallback?.Invoke();
            _itemEnemyCallback        = null;
            _itemAreaCallback         = null;
            _itemTargetCancelCallback = null;
            _itemConfirmedUseCallback = null;
        }

        private void HandleItemEnemyCellClicked(CombatBoardCell cell)
        {
            if (cell.OccupyingEnemy == null || cell.OccupyingEnemy.CurrentHP <= 0) return;

            var target   = cell.OccupyingEnemy;
            var callback = _itemEnemyCallback;
            var confirm  = _itemConfirmedUseCallback;

            isItemEnemyTargetingMode = false;
            UnsubscribeCellHoverForItemTargeting();
            ClearItemHoverPreview();
            ClearAllHighlights();

            confirm?.Invoke();          // 아이템 소모
            FinishItemSlot();           // 슬롯 파괴
            callback?.Invoke(target);  // 실제 효과 실행

            _itemEnemyCallback        = null;
            _itemTargetCancelCallback = null;
            _itemConfirmedUseCallback = null;

        }

        private void HandleItemAreaCellClicked(CombatBoardCell cell)
        {
            var pos      = cell.GridPos;
            var callback = _itemAreaCallback;
            var confirm  = _itemConfirmedUseCallback;

            isItemAreaTargetingMode = false;
            UnsubscribeCellHoverForItemTargeting();
            ClearItemHoverPreview();
            ClearAllHighlights();

            confirm?.Invoke();
            FinishItemSlot();
            callback?.Invoke(pos);

            _itemAreaCallback         = null;
            _itemTargetCancelCallback = null;
            _itemConfirmedUseCallback = null;

        }

        private void FinishItemSlot()
        {
            InGameUIController.Instance?.SetItemTargetingBlocker(false);
            if (_pendingItemSlot != null)
            {
                UnityEngine.Object.Destroy(_pendingItemSlot.gameObject);
                _pendingItemSlot = null;
            }
        }

        // ── 아이템 타겟팅 hover 이벤트 ────────────────────────────────────

        private void SubscribeCellHoverForItemTargeting()
        {
            if (grid == null) return;
            for (int col = 0; col < boardCols; col++)
                for (int row = 0; row < boardRows; row++)
                {
                    grid[col, row].OnCellHoverEnter += OnItemTargetCellHoverEnter;
                    grid[col, row].OnCellHoverExit  += OnItemTargetCellHoverExit;
                }
        }

        private void UnsubscribeCellHoverForItemTargeting()
        {
            if (grid == null) return;
            for (int col = 0; col < boardCols; col++)
                for (int row = 0; row < boardRows; row++)
                {
                    grid[col, row].OnCellHoverEnter -= OnItemTargetCellHoverEnter;
                    grid[col, row].OnCellHoverExit  -= OnItemTargetCellHoverExit;
                }
        }

        private void OnItemTargetCellHoverEnter(CombatBoardCell cell)
        {
            ClearItemHoverPreview();
            if (isItemEnemyTargetingMode)
            {
                // 적 선택: hover된 적 셀에 attack 스프라이트
                if (cell.OccupyingEnemy != null && attackRangeSprite != null)
                    AddPreviewObject(cell.GridPos, attackRangeSprite, itemHoverPreviewObjects);
            }
            else if (isItemAreaTargetingMode)
            {
                // 폭탄: hover된 셀 기준 3×3 attack 스프라이트
                if (attackRangeSprite != null)
                {
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            var pos = cell.GridPos + new Vector2Int(dx, dy);
                            if (IsInBoard(pos))
                                AddPreviewObject(pos, attackRangeSprite, itemHoverPreviewObjects);
                        }
                }
            }
        }

        private void OnItemTargetCellHoverExit(CombatBoardCell cell)
            => ClearItemHoverPreview();

        private void ClearItemHoverPreview()
        {
            foreach (var obj in itemHoverPreviewObjects)
                if (obj != null) Destroy(obj);
            itemHoverPreviewObjects.Clear();
        }

        // ── 상태이상 내부 처리 ───────────────────────────────────────────

        private void ProcessPlayerFireStatus()
        {
            var fire = playerStatusEffects.Find(s => s.Type == StatusEffectType.Fire);
            if (fire == null || fire.Stacks <= 0) return;
            ApplyDamageToPlayer(fire.Stacks);
            unitUI?.SetEffect("fire", fireIcon, fire.Stacks);
            Debug.Log($"[CombatController] 플레이어 화염 피해: {fire.Stacks}");
        }

        private void ProcessEnemyFireStatus(EnemyInstance enemy)
        {
            var fire = enemy.StatusEffects.Find(s => s.Type == StatusEffectType.Fire);
            if (fire == null || fire.Stacks <= 0) return;
            DamageEnemy(enemy, fire.Stacks);
            if (enemy.CurrentHP > 0)
                enemy.UI?.SetEffect("fire", fireIcon, fire.Stacks);
        }

        /// <summary>기절·빙결 처리. 해당 상태면 true 반환 (이번 턴 스킵).</summary>
        private bool ConsumeEnemyStun(EnemyInstance enemy)
        {
            for (int i = enemy.StatusEffects.Count - 1; i >= 0; i--)
            {
                var s = enemy.StatusEffects[i];
                if (s.Type != StatusEffectType.Stun && s.Type != StatusEffectType.Freeze) continue;
                s.Stacks--;
                string key = s.Type == StatusEffectType.Stun ? "stun" : "freeze";
                if (s.Stacks <= 0)
                {
                    enemy.StatusEffects.RemoveAt(i);
                    enemy.UI?.RemoveEffect(key);
                }
                else
                {
                    Sprite icon = s.Type == StatusEffectType.Stun ? stunIcon : freezeIcon;
                    enemy.UI?.SetEffect(key, icon, s.Stacks);
                }
                Debug.Log($"[CombatController] {enemy.Name} 기절/빙결으로 턴 스킵");
                return true;
            }
            return false;
        }

        #endregion

        #region UI Updates

        private void UpdateEnergyDisplay()
        {
            if (energyText != null)
                energyText.text = $"{currentEnergy}/{maxEnergy}";

            RefreshHandXPlaceholders();
            RefreshCardPlayability();
        }

        private void RefreshHandXPlaceholders()
        {
            for (int i = 0; i < handCardObjects.Count; i++)
            {
                if (handCardObjects[i] == null) continue;
                var cardUI = handCardObjects[i].GetComponent<CardUI>();
                if (cardUI == null || i >= hand.Count) continue;
                string descKey = $"card_desc_{hand[i].cardCode}";
                string locDesc = LocalizationManager.Get(descKey);
                string rawDesc = locDesc == descKey ? (hand[i].description ?? "") : locDesc;
                if (!rawDesc.Contains("{X}")) continue;
                cardUI.Initialize(hand[i], playerStrength, playerDexterity, _cardCostReduction, currentEnergy);
            }
        }

        private void UpdateBlockDisplay()
        {
            int currentHP = GameManager.Instance != null ? GameManager.Instance.CurrentHP : 1;
            int maxHP     = GameManager.Instance != null ? GameManager.Instance.MaxHP     : 1;
            unitUI?.UpdateShield(playerBlock, currentHP, maxHP);
        }

        private void UpdatePileCounters()
        {
            if (drawPileCount != null)
                drawPileCount.text   = (deckManager != null ? deckManager.DrawPileCount   : 0).ToString();
            if (discardPileCount != null)
                discardPileCount.text = (deckManager != null ? deckManager.DiscardPileCount : 0).ToString();
        }

        private void UpdateTurnIndicator(string text)
        {
            if (turnIndicatorText != null)
                turnIndicatorText.text = text;
            if (turnIndicator != null)
                turnIndicator.SetActive(true);
        }

        private void ShowDrawPile()
        {
            if (deckManager != null && deckManager.DrawPileCount == 0) return;
            InGameUIController.Instance?.ToggleDrawPanel();
        }

        private void ShowDiscardPile()
        {
            if (deckManager != null && deckManager.DiscardPileCount == 0) return;
            InGameUIController.Instance?.ToggleDiscardPanel();
        }

        #endregion

        #region Rest Area

        private readonly System.Collections.Generic.List<GameObject> _restButtonObjs =
            new System.Collections.Generic.List<GameObject>();

        public void SpawnRestArea()
        {
            CleanupNonCombatVisuals();
            combatState = CombatState.Rest;
            SetCombatHudActive(false);
            cardConfirmPanel?.Close();
            SetLeaveButtonActive(true, OnRestLeave);
            SpawnRestButtons();
            SpawnPlayerNonCombat();
        }

        private void SpawnRestButtons()
        {
            foreach (var o in _restButtonObjs) if (o != null) Destroy(o);
            _restButtonObjs.Clear();

            // 담배(201) 유물 보유 시 제거 버튼 활성, 삽(202) 유물 보유 시 발굴 버튼 활성
            bool canRemove    = HasRelic(201);
            bool canExcavate  = HasRelic(202);

            System.Action[] actions = {
                OnRestClicked,
                OnUpgradeClicked,
                OnRemoveClicked,
                OnExcavateClicked,
            };
            bool[] interactable = { true, true, canRemove, canExcavate };

            var parent = combatPanel != null ? (Transform)combatPanel : combatBoard;
            if (parent == null) return;

            for (int i = 0; i < 4; i++)
            {
                GameObject obj;
                if (restButtonPrefab != null)
                {
                    obj = Instantiate(restButtonPrefab, parent);
                }
                else
                {
                    obj = new GameObject($"RestButton_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                    obj.transform.SetParent(parent, false);
                }
                var rt = obj.GetComponent<RectTransform>();
                if (restButtonPositions != null && i < restButtonPositions.Length)
                    rt.anchoredPosition = restButtonPositions[i];
                rt.sizeDelta = restButtonSize;

                var img = obj.GetComponent<Image>();
                if (img != null && restButtonSprites != null && i < restButtonSprites.Length && restButtonSprites[i] != null)
                    img.sprite = restButtonSprites[i];

                var btn = obj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.interactable = interactable[i];
                    int idx = i;
                    btn.onClick.AddListener(() => actions[idx]?.Invoke());
                }

                _restButtonObjs.Add(obj);
            }
        }

        private void OnRestLeave()
        {
            Debug.Log("Sdsf");
            InGameUIController.Instance?.OpenMap();
        }

        private void SpawnPlayerNonCombat()
        {
            var parent = combatPanel != null ? combatPanel : combatBoard;
            if (parent == null) return;
            if (nonCombatPlayerObj != null) { Destroy(nonCombatPlayerObj); nonCombatPlayerObj = null; }

            // unitPrefab에서 sprite만 추출
            Sprite playerSprite = null;
            if (unitPrefab != null)
            {
                var srcImg = unitPrefab.GetComponent<Image>()
                          ?? unitPrefab.GetComponentInChildren<Image>(true);
                playerSprite = srcImg?.sprite;
            }

            nonCombatPlayerObj = new GameObject("PlayerImage", typeof(RectTransform), typeof(Image));
            nonCombatPlayerObj.transform.SetParent(parent, false);

            var img = nonCombatPlayerObj.GetComponent<Image>();
            img.sprite = playerSprite;
            img.raycastTarget = false;

            var rt = nonCombatPlayerObj.GetComponent<RectTransform>();
            rt.anchoredPosition = playerNonCombatPos;
            rt.sizeDelta        = playerNonCombatSize;
        }

        private void OnRestClicked()
        {
            if (GameManager.Instance != null)
            {
                float pct = restHealPercent;
                foreach (var relic in GameManager.Instance.Relics)
                    relic.OnBeforeRestHeal(ref pct);
                int healAmount = Mathf.RoundToInt(GameManager.Instance.MaxHP * pct);
                GameManager.Instance.Heal(healAmount);
            }
            CleanupRestArea();
            InGameUIController.Instance?.OnNodeComplete();
        }

        private void OnUpgradeClicked()
        {
            // 장인의 망치(301): 두 장 강화, 없으면 한 장
            _upgradesRemaining = HasRelic(301) ? 2 : 1;
            DoNextUpgrade();
        }

        private void DoNextUpgrade()
        {
            if (_upgradesRemaining <= 0)
            {
                CleanupRestArea();
                InGameUIController.Instance?.OnNodeComplete();
                return;
            }
            _upgradesRemaining--;
            InGameUIController.Instance?.OpenRestCardList(RestCardMode.Upgrade, cardConfirmPanel, DoNextUpgrade);
        }

        private void OnRemoveClicked()  => InGameUIController.Instance?.OpenRestCardList(RestCardMode.Remove,  cardConfirmPanel);

        private void OnExcavateClicked()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            RelicLibrary.RegisterAll();
            ItemLibrary.RegisterAll();
            var pool = RelicRegistry.GetByType(bossRelic: false);
            var ownedCodes = new System.Collections.Generic.HashSet<int>();
            foreach (var r in gm.Relics)
                if (r?.Data != null) ownedCodes.Add(r.Data.relicCode);
            pool.RemoveAll(r => ownedCodes.Contains(r.relicCode));

            if (pool.Count > 0)
            {
                var picked = pool[UnityEngine.Random.Range(0, pool.Count)];
                gm.AddRelic(picked.relicCode);
                Debug.Log($"[CombatController] 발굴: {picked.relicName} 획득");
            }

            CleanupRestArea();
            InGameUIController.Instance?.OnNodeComplete();
        }

        public void CleanupRestArea()
        {
            combatState = CombatState.NotStarted;
            SetLeaveButtonActive(false);
            cardConfirmPanel?.Close();

            foreach (var o in _restButtonObjs) if (o != null) Destroy(o);
            _restButtonObjs.Clear();

            if (nonCombatPlayerObj != null) { Destroy(nonCombatPlayerObj); nonCombatPlayerObj = null; }
        }

        private static bool HasRelic(int relicCode)
        {
            var relics = GameManager.Instance?.Relics;
            if (relics == null) return false;
            foreach (var r in relics)
                if (r?.Data?.relicCode == relicCode) return true;
            return false;
        }

        #endregion

        #region Merchant Shop

        private GameObject _merchantObj;

        /// <summary>
        /// 상점 노드 진입 시 호출. 상인 이미지를 동적으로 생성하고 클릭 시 ShopPanel을 엽니다.
        /// </summary>
        public void SpawnMerchant()
        {
            CleanupNonCombatVisuals();
            combatState = CombatState.Shop;
            SetCombatHudActive(false);
            SetLeaveButtonActive(true, OnMerchantLeave);
            SpawnPlayerNonCombat();
            SpawnMerchantImage();
        }

        private void OnMerchantLeave()
        {
            InGameUIController.Instance?.OpenMap();
        }

        private void SpawnMerchantImage()
        {
            var parent = combatPanel != null ? (Transform)combatPanel : combatBoard;
            if (parent == null) return;

            if (_merchantObj != null) { Destroy(_merchantObj); _merchantObj = null; }

            _merchantObj = new GameObject("MerchantImage",
                typeof(RectTransform), typeof(Image), typeof(Button));
            _merchantObj.transform.SetParent(parent, false);

            var rt = _merchantObj.GetComponent<RectTransform>();
            rt.anchoredPosition = merchantPos;
            rt.sizeDelta        = merchantSize;

            var img = _merchantObj.GetComponent<Image>();
            if (merchantSprite != null)
                img.sprite = merchantSprite;
            else
                img.color = new Color(0.6f, 0.4f, 0.1f, 1f);

            var btn = _merchantObj.GetComponent<Button>();
            btn.onClick.AddListener(() => InGameUIController.Instance?.OpenShopPanel());
        }

        /// <summary>상점 떠나기 후 상인 이미지를 제거하고 맵으로 이동합니다.</summary>
        public void CleanupMerchant()
        {
            combatState = CombatState.NotStarted;
            SetLeaveButtonActive(false);

            if (_merchantObj != null) { Destroy(_merchantObj); _merchantObj = null; }
            if (nonCombatPlayerObj != null) { Destroy(nonCombatPlayerObj); nonCombatPlayerObj = null; }

            InGameUIController.Instance?.OpenMap();
        }

        /// <summary>비전투 씬 오브젝트(휴식 버튼, 상인 이미지, 비전투 플레이어 이미지)를 일괄 제거합니다.</summary>
        private void CleanupNonCombatVisuals()
        {
            foreach (var o in _restButtonObjs) if (o != null) Destroy(o);
            _restButtonObjs.Clear();

            if (_merchantObj != null) { Destroy(_merchantObj); _merchantObj = null; }
            if (nonCombatPlayerObj != null) { Destroy(nonCombatPlayerObj); nonCombatPlayerObj = null; }
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        #endregion

        #region Pause

        public void PauseCombat()  { isPaused = true; }
        public void ResumeCombat() { isPaused = false; }

        #endregion

        #region Audio

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
                audioSource.PlayOneShot(clip);
        }

        #endregion
    }

    #region Support Classes

    public enum CombatState
    {
        NotStarted,
        Starting,
        PlayerTurn,
        EnemyTurn,
        Victory,
        Defeat,
        Rest,
        Shop
    }

    #endregion
}
