using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public class BoardController : MonoBehaviour
    {
        [Header("=== Combat Settings ===")]
        [SerializeField] private int startingHandSize = 5;
        [SerializeField] private float enemyTurnDelay = 1f;

        [Header("=== Range Preview Size ===")]
        [SerializeField] private float playerRangePreviewScale = 1f;
        [Tooltip("적/아군 공격·스킬 텔레그래프 sprite 크기 배율")]
        [SerializeField] private float enemyRangePreviewScale  = 1f;
        [Tooltip("적/아군 이동 텔레그래프 sprite 크기 배율 (공격 sprite와 따로 조절 — 보통 더 작게)")]
        [SerializeField] private float enemyMovePreviewScale   = 1f;

        [Header("=== Turn UI ===")]
        [SerializeField] private Button endTurnButton;
        [SerializeField] private TextMeshProUGUI turnIndicatorText;
        [SerializeField] private GameObject turnIndicator;

        [Header("=== Deck ===")]
        [SerializeField] private DeckManager deckManager;

        [Header("=== Hand Area ===")]
        [SerializeField] private Transform handContainer;
        [SerializeField] private GameObject cardPrefab;
        [Tooltip("카드 간 최대 가로 간격 (px). 카드 수가 적을 때 이 값까지 벌어집니다.")]
        [SerializeField] private float handCardSpacing = 120f;
        [Tooltip("손패 전체가 차지할 최대 가로 폭 (px). 카드가 늘어나면 이 폭 안에 들어가도록 간격이 줄어듭니다. 0 이하이면 항상 handCardSpacing 고정.")]
        [SerializeField] private float handMaxWidth = 800f;
        [Tooltip("손패 카드 생성 기준 높이. 화면 하단(0)에서 위로 올리는 픽셀 수. 0=카드 바닥이 화면 최하단.")]
        [SerializeField] private float handYOffset = 0f;
        [Tooltip("sticky 상태 카드가 이 화면 Y 픽셀 이상으로 올라가면 카드가 작아집니다. 보드 셀 하단 높이와 맞추세요.")]
        [SerializeField] private float cardCollapseBoardScreenY = 300f;

        [Header("=== Deck Pile Buttons ===")]
        [SerializeField] private Button drawPileButton;
        [SerializeField] private TextMeshProUGUI drawPileCount;
        [SerializeField] private Button discardPileButton;
        [SerializeField] private TextMeshProUGUI discardPileCount;

        [Header("=== Button Slide Offsets ===")]
        [Tooltip("endTurnButton을 숨길 때 active anchoredPosition에 더할 오프셋. 오른쪽으로 카메라 밖까지 이동(+X) → 보임 위치는 왼쪽.")]
        [SerializeField] private Vector2 endTurnHiddenOffset = new Vector2(900f, 0f);
        [Tooltip("drawPileButton / discardPileButton을 숨길 때 active anchoredPosition에 더할 오프셋. 아래로 내림(-Y).")]
        [SerializeField] private Vector2 deckPileHiddenOffset = new Vector2(0f, -600f);
        [SerializeField] private float buttonSlideDuration = 0.3f;

        [Header("=== Victory Card Drop Animation ===")]
        [Tooltip("전투 승리 시 손패 카드들이 아래로 떨어지는 거리(픽셀).")]
        [SerializeField] private float victoryCardDropOffset = 900f;
        [Tooltip("각 카드가 떨어지는데 걸리는 시간(초).")]
        [SerializeField] private float victoryCardDropDuration = 0.55f;
        [Tooltip("카드 간 떨어지기 시작하는 간격(초). 0이면 동시에 떨어짐.")]
        [SerializeField] private float victoryCardDropStagger = 0.06f;

        [Header("=== Combat Board (boardCols x boardRows) ===")]
        [Tooltip("combatBoard의 부모 패널 — 비전투 플레이어 이미지를 여기에 생성")]
        [SerializeField] private RectTransform combatPanel;
        [SerializeField] private RectTransform combatBoard;
        [SerializeField] private GameObject cellPrefab;          // CombatBoardCell 프리팹 (없으면 자동 생성)
        [Tooltip("전투 보드의 열 수 — 모든 좌표 검증·맵 노드 분포·플레이어 스폰이 이 값을 기준으로 동작합니다.")]
        [SerializeField] private int boardCols = 4;
        [Tooltip("전투 보드의 행 수 — 모든 좌표 검증·맵 노드 분포·플레이어 스폰이 이 값을 기준으로 동작합니다.")]
        [SerializeField] private int boardRows = 4;
        [SerializeField] private float cellWidth  = 150f;
        [SerializeField] private float cellHeight = 150f;
        [Tooltip("그리드 세로 위치. X는 무시되며 가로는 combatBoard 기준 자동 중앙 정렬됩니다.\nY: 화면 좌하단(0,0) 기준 픽셀 좌표로 그리드 하단 위치를 설정합니다.")]
        [SerializeField] private Vector2 boardOffset = Vector2.zero;
        [Tooltip("전투마다 초기화되는 현재 위치. Inspector 값은 첫 전투 진입 전 임시값.")]
        [SerializeField] private Vector2Int playerSpawnCell = new Vector2Int(1, 1);
        [Tooltip("고정 스폰 위치 — boardCols/boardRows 범위 내로 설정해야 합니다.")]
        [SerializeField] private Vector2Int playerInitialCell = new Vector2Int(1, 1);
        [Tooltip("랜덤 스폰 후보 셀 — 비어있으면 사용 안 함. boardCols/boardRows 범위 내 좌표만 유효.")]
        [SerializeField] private Vector2Int[] enemySpawnCells = new Vector2Int[0];

        [Header("=== Prefabs ===")]
        [Tooltip("플레이어·적·아군 공통 유닛 프리팹. AllyData.allyPrefab이 있으면 아군은 그것을 우선 사용합니다.")]
        [SerializeField] private GameObject unitPrefab;
        [Tooltip("플레이어를 셀 중앙에서 얼마나 오프셋할지 (예: (0, 20) 으로 위로 올림)")]
        [SerializeField] private Vector2 playerCellOffset = new Vector2(0f, 20f);

        [Header("=== Combat HUD (전투 종료 시 비활성화) ===")]
        [Tooltip("전투 승리 후 숨길 UI 요소들 (전투판 제외). 예: 손패, 에너지, 턴종료버튼 등")]
        [SerializeField] private GameObject[] combatHudElements;

        [Header("=== Merchant Shop ===")]
        [Tooltip("상점 카드 제거용 CardListController")]
        [SerializeField] private CardListController shopCardListController;
        [Tooltip("1page 직업 Common 카드 수")]
        [SerializeField] private int shopClassCommonCount   = 3;
        [Tooltip("1page 직업 Uncommon 카드 수")]
        [SerializeField] private int shopClassUncommonCount = 2;
        [Tooltip("1page 직업 Rare 카드 수")]
        [SerializeField] private int shopClassRareCount     = 1;
        [SerializeField] private int shopRelicCount         = 2;
        [SerializeField] private int shopItemCount          = 2;

        [Header("=== Choose Card Panel ===")]
        [Tooltip("Exhausts/Discard 이펙트에서 열리는 카드 선택 패널")]
        [SerializeField] private ChooseCardPanel chooseCardPanel;

        [Header("=== Rest Area ===")]
        [Tooltip("강화 확인 패널")]
        [SerializeField] private CardConfirmPanel cardConfirmPanel;
        [Tooltip("최대 HP 대비 회복 비율 (0.3 = 30%)")]
        [SerializeField] private float restHealPercent = 0.3f;

        [Header("=== Map Node Distribution (boardCols x boardRows 그리드) ===")]
        [SerializeField] private int mapDangerCombatCount = 1;
        // Event 노드는 act당 3개 — treasure / shopEvent / choiceEvent 가 각각 1개씩 숨겨진다.
        // GenerateMapGrid에서 자동으로 sub-type을 1:1로 배정한다.
        [SerializeField] private int mapEventCount   = 3;
        [SerializeField] private int mapRestCount    = 1;
        [SerializeField] private int mapShopCount    = 1;
        // 보물방은 Event 노드 sub-type(Treasure)으로 흡수되었다. 별도 카운트 사용 안 함.
        [SerializeField] private int mapTreasureCount = 0;

        // Map Node Icons (Addressables 런타임 로드)
        private Sprite startNodeIcon;
        private Sprite combatNodeIcon;
        private Sprite dangerCombatNodeIcon;
        private Sprite bossNodeIcon;
        private Sprite restNodeIcon;
        private Sprite shopNodeIcon;
        private Sprite eventNodeIcon;
        private Sprite visitedNodeIcon;
        private Sprite treasureNodeIcon;  // 108 유물 — 보물방 위치 공개용

        [Header("=== Map Colors ===")]
        [SerializeField] private Color mapNormalColor     = new Color(0.25f, 0.25f, 0.35f);
        [SerializeField] private Color mapAccessibleColor = Color.white;
        [SerializeField] private Color mapVisitedColor    = new Color(0.4f, 0.4f, 0.4f);


        // Effect / Action / Status Icons (아직 스프라이트 미생성 — 추후 Addressables 로드)
        private Sprite strengthIcon;
        private Sprite dexterityIcon;
        private Sprite attackActionIcon;
        private Sprite moveActionIcon;
        private Sprite skillActionIcon;
        private Sprite fireIcon;
        private Sprite stunIcon;
        private Sprite freezeIcon;
        private Sprite fearIcon;
        private Sprite unknownEnemyInfoIcon;

        [Header("=== Card Projectile (소멸배기) ===")]
        [Tooltip("소멸배기 카드 사용 시 손패 카드들이 변신할 칼 스프라이트")]
        [SerializeField] private Sprite knifeProjectileSprite;
        [Tooltip("칼이 적까지 날아가는 시간 (초)")]
        [SerializeField] private float knifeProjectileDuration = 0.45f;
        [Tooltip("칼로 변할 때 카드 크기 배수 (1=원래 크기)")]
        [SerializeField] private float knifeProjectileScale = 0.5f;
        [Tooltip("연속 발사 간 간격 (초)")]
        [SerializeField] private float knifeProjectileInterval = 0.08f;
        [Tooltip("손패 카드가 칼로 변신한 뒤 발사를 시작하기 전 대기 시간 (초). 0이면 변신 즉시 발사")]
        [SerializeField] private float knifeTransformDelay = 0.18f;

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
        public CombatState CurrentCombatState => combatState;
        private int _playingHandIndex = -1; // ApplyCardEffects 실행 중 현재 플레이 카드 인덱스
        private int playerBlock;
        private int playerStrength;
        private int playerDexterity;
        private int turnNumber;

        // 카드 타입 봉인: CardType → 남은 턴 수
        private Dictionary<CardType, int> cardTypeRestrictions = new Dictionary<CardType, int>();

        // ── 유물 관련 런타임 상태 ─────────────────────────────────────────
        private int  _playerMoveCount         = 0; // 현재 전투 누적 이동 횟수
        private int  _bonusHandSize           = 0; // 턴 시작 추가 드로우 (900 판도라)
        private int  _firstTurnBonusDraw      = 0; // 첫 턴에만 추가 드로우 (109 보스, 206 불타는 심장)
        private int  _thisTurnBonusDraw       = 0; // 이번 턴에만 추가 드로우 (107 - 매 3턴마다 +1)
        private int  _timedBonusDraw          = 0; // N턴 동안 추가 드로우 +1
        private int  _timedBonusDrawTurns     = 0; // 남은 턴 수
        private int  _cardCostReduction       = 0; // 이번 턴 카드 비용 감소
        private int  _upgradesRemaining       = 0; // 휴식 강화 남은 횟수 (216 장인의 망치)
        private int  _firstActionBonusDamage  = 0; // 첫 데미지 추가 피해 (201 유물)
        private bool _firstAttackUsedThisCombat = false;
        private bool _grantingCombatStartCards = false; // OnCombatStart 훅 실행 중 여부
        private int  _combatStartBonusCards    = 0; // OnCombatStart 훅이 생성한 보너스 카드 수 (90/990) — 첫 턴 startingHandSize에서 제외
        private int  _cardsPlayedThisTurn     = 0; // 904 유물 — 한 턴에 6장 제한 카운터
        private int  _teleportUsesThisCombat  = 0; // 순간이동 카드 — 전투당 act수만큼 사용 제한

        public int TurnNumber => turnNumber;

        // ── 파워 카드 지속 효과 ────────────────────────────────────────────
        private readonly List<CombatPowerEffect> _activePowers = new List<CombatPowerEffect>();
        private bool _noCardHpLoss = false; // no_loseHp: 카드 효과로 HP를 잃지 않음
        private bool _noExhaust    = false; // no_Exhausts: 카드가 소멸되지 않고 버림 더미로
        private bool _suppressOnEnemyDamaged = false; // 후속 데미지에서 OnEnemyDamaged 재진입 방지

        // ── Card_Upgrade 임시 강화 트래킹 (전투 종료 시 자동 복원) ────────
        private readonly List<CardData> _temporarilyUpgradedCards = new List<CardData>();

        // ── Range_Increase 임시 사거리 강화 트래킹 (전투 종료 시 자동 복원) ─
        // key: 수정된 CardEffect 인스턴스, value: 원본 rangeOffsets 배열
        private readonly Dictionary<CardEffect, RangeOffset[]> _shootRangeBackups = new Dictionary<CardEffect, RangeOffset[]>();

        // ── Shoot/Trap 카운터 (everyShoot_*, everytrap_* 파워에서 사용) ────
        private int _shootCount = 0;
        private int _trapCount  = 0;
        public  int ShootCount => _shootCount;
        public  int TrapCount  => _trapCount;

        // ── Random_*Card 변환 매핑 (변환된 카드 → 원본 Random 카드) ────────
        private readonly Dictionary<CardData, CardData> _randomCardOrigin = new Dictionary<CardData, CardData>();

        // ── 카드 선택 패널 ────────────────────────────────────────────────
        private bool _isChooseCardMode = false;
        private bool _pendingChoosePanelExhaust = false;
        private int  _pendingChoosePanelCount   = 0;
        private bool _hasPendingChoosePanel     = false;

        // ── Reward Cards ──────────────────────────────────────────────────
        private readonly List<CardData> _rewardCards = new List<CardData>();
        private Relic.RelicData _pendingRewardRelic;
        private Item.ItemData   _pendingRewardItem;
        private bool _pendingVictoryReward;
        private bool _pendingDefeatReward;

        // ── 카드 보상 픽 모드 (60003 → 보상 카드 3장 + 60004 넘기기 카드 드로우) ──
        public const int RewardSkipCardCode = 60004;
        private bool _rewardPickActive;
        private bool _rewardPickPending;          // Reward_Card 효과 실행 후 픽 모드 진입 예약 플래그
        private bool _rewardPickEndPending;       // Reward_Skip 효과 실행 후 픽 모드 종료 예약 플래그
        private readonly HashSet<CardData> _rewardPickCards = new HashSet<CardData>();
        // 픽 시작 시 손패에 남아있던 보상 카드들 (gold/relic/item/map_move 등) — 픽 종료 후 다시 드로우.
        private readonly List<CardData> _savedRewardCardsForPick = new List<CardData>();
        // 픽 모드에서 RangeView 대상 카드 — drawPileButton(shop/픽 모드에서 RangeIndicator로 동작) 클릭 시 사거리 표시.
        private CardData _rewardPickViewCard;

        // ── 보스 유물 선택 모드 (60010 → 보스 유물 3장 중 1장, 넘기기 없음) ──
        // 카드 보상 픽 흐름(_rewardPickActive)을 그대로 재사용하되, 아래 하위 플래그로
        // (a) 선택 시 덱 추가 대신 유물 획득, (b) discard/draw pile 버튼을 숨김(down), (c) 넘기기 비활성을 분기한다.
        private bool _relicPickActive;
        private bool _relicPickPending;           // chooserelic(60010) 효과 실행 후 유물 픽 모드 진입 예약
        private bool _nextStagePending;           // nextstage(60011) 효과 실행 후 Act 진행 예약
        private bool _restartCombatPending;       // combat_restart(60008) 효과 실행 후 전투 재시작 예약
        private bool _returnMainMenuPending;      // mainmenu(60009) 효과 실행 후 메인메뉴 복귀 예약
        private bool _grantMapMoveAfterRelicPick; // 보스 다음 Act 진입 시 유물 픽(60010)을 먼저 시키고, 픽 종료 후 맵 이동 카드를 지급하기 위한 예약
        private readonly List<Relic.RelicData> _pendingRelicChoices = new List<Relic.RelicData>();

        // ── Shop State ───────────────────────────────────────────────────
        private int _shopPage;
        private Coroutine _drawShopCardsCoroutine; // 진행 중인 shop 카드 드로우 코루틴 — 페이지 전환 시 중단용
        private readonly Dictionary<CardData, int> _shopCardPrices = new Dictionary<CardData, int>();
        private readonly Dictionary<CardData, bool> _shopCardDiscounted = new Dictionary<CardData, bool>();
        private readonly Dictionary<CardData, RelicData> _shopRelicMap = new Dictionary<CardData, RelicData>();
        private readonly Dictionary<CardData, ItemData> _shopItemMap = new Dictionary<CardData, ItemData>();
        private CardData _shopSelectedCard;

        // 같은 위치의 shop 재진입/페이지 전환 시 동일한 상품을 유지하기 위한 캐시
        private class ShopInventory
        {
            public List<CardData> page1Cards = new List<CardData>();
            public List<CardData> page2Cards = new List<CardData>();
            public Dictionary<CardData, int> prices = new Dictionary<CardData, int>();
            public Dictionary<CardData, bool> discounted = new Dictionary<CardData, bool>();
            public Dictionary<CardData, RelicData> relicMap = new Dictionary<CardData, RelicData>();
            public Dictionary<CardData, ItemData> itemMap = new Dictionary<CardData, ItemData>();
            // 구매한 카드는 이 set에 들어가 캐시 복원 시 제외됨
            public HashSet<CardData> consumed = new HashSet<CardData>();
            // 상점형 이벤트 — EventDataTemplate 카드를 상품으로 쓰는 경우 표식 (deck add 대신 effect 실행)
            public HashSet<CardData> eventOriginCards = new HashSet<CardData>();
        }
        // 현재 상점 인스턴스에서 EventDataTemplate 출신 상품 카드 set (HandleShopCardPlay에서 분기에 사용)
        private readonly HashSet<CardData> _shopEventOriginCards = new HashSet<CardData>();
        private readonly Dictionary<Vector2Int, ShopInventory> _shopInventoryCache = new Dictionary<Vector2Int, ShopInventory>();
        private Vector2Int _currentShopPos = new Vector2Int(-1, -1);
        // 카드 가격 — 희귀도별 (min, max) 랜덤 범위. 인덱스: 0=Common, 1=Uncommon, 2=Rare, 3=Legendary.
        private static readonly (int min, int max)[] ShopCardPriceRanges =
        {
            ( 45,  55),   // Common
            ( 68,  82),   // Uncommon
            (185, 215),   // Rare
            (300, 300),   // Legendary (현재 상점에선 사용 안 함 — 폴백)
        };
        // 유물은 Uncommon/Rare 2단계만 존재. 인덱스 0(Common)은 미사용 (RelicData.RarityIndex가 1/2만 반환).
        private static readonly (int min, int max)[] ShopRelicPriceRanges =
        {
            (143, 157),   // Common (현재 유물 시스템에선 미사용)
            (238, 262),   // Uncommon — 1xx/4xx 유물
            (450, 550),   // Rare — 2xx/3xx/5xx/6xx 유물
        };
        // 아이템: Rare 등급 폐지 — Common / Uncommon만 사용.
        private static readonly (int min, int max)[] ShopItemPriceRanges  =
        {
            ( 48,  52),   // Common
            ( 95, 105),   // Uncommon
        };

        /// <summary>희귀도 범위 (min, max)에서 가격을 한 번 굴린다. min==max면 고정값.</summary>
        private static int RollPriceInRange((int min, int max) range)
        {
            if (range.max <= range.min) return range.min;
            return UnityEngine.Random.Range(range.min, range.max + 1);
        }

        // ── Map State ──────────────────────────────────────────────────────
        private MapNodeData[,] mapGrid;        // [row, col]
        private Vector2Int mapPlayerPos = new Vector2Int(-1, -1); // (row, col)
        private Vector2Int savedMapPlayerPos;   // 전투 진입 시 저장
        // 전투 진입 시 mapPlayerPos가 set 됐는지 여부 — RestoreMapPlayerPosAfterCombat에서
        // (0,0) 같은 유효 좌표도 복원되도록 명시적 플래그를 사용한다.
        private bool _savedMapPlayerPosSet;
        private int mapSeed;
        private bool mapInitialized;

        public event Action<NodeType, int> OnNodeSelected;

        // Deck
        private int _drawOrderCounter = 0;
        private Coroutine _drawCardsCoroutine; // 진행 중인 일반 드로우(비주얼) 코루틴 — 턴 종료 시 중단용
        private List<CardData> hand = new List<CardData>();

        /// <summary>현재 손패에 있는 카드 수. 유물 효과 등에서 hand 상태를 읽을 때 사용.</summary>
        public int HandCount => hand?.Count ?? 0;
        private List<GameObject> handCardObjects = new List<GameObject>();
        private List<GameObject> hoverPreviewObjects = new List<GameObject>();
        // 현재 hoverPreviewObjects가 그려진 카드 — 같은 카드 hover/sticky 토글 시 destroy/recreate 대신 재사용해서 깜빡임 방지.
        private CardData _currentRangePreviewCard;
        private List<GameObject> unitPreviewObjects = new List<GameObject>();
        // 타입별 unit preview (카드 hover 시 타입별 표시/숨김 토글용)
        private List<GameObject> unitAttackPreviewObjects = new List<GameObject>();
        private List<GameObject> unitMovePreviewObjects   = new List<GameObject>();
        private List<GameObject> unitSkillPreviewObjects  = new List<GameObject>();
        // 타게팅 중 셀 hover 시 표시되는 보조 preview (pushEnmemy 밀리는 칸, Movediagonal_Damage 공격 칸)
        private List<GameObject> targetingHoverPreviewObjects = new List<GameObject>();

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

        /// <summary>현재(또는 가장 최근) 전투의 인카운터 이름. 승리/패배 패널의 {0} 치환에 사용.</summary>
        public string LastCombatEncounterName => currentEncounterData != null ? currentEncounterData.encounterName : null;

        // Allies
        private List<AllyInstance> allies = new List<AllyInstance>();

        // Targeting
        private CardData pendingCard;
        private int pendingCardIndex = -1;

        private AudioSource audioSource;

        // ── 런타임 로드 스프라이트 (Addressables) ──
        // 플레이어 (아군도 공유)
        private Sprite playerAttackUnactive, playerAttackActive;
        private Sprite playerMoveUnactive,   playerMoveActive;
        private Sprite playerPowerUnactive,  playerPowerActive;
        // 적
        private Sprite enemyAttackUnactive,  enemyAttackActive;
        private Sprite enemyMoveUnactive,    enemyMoveActive;
        // 지대(Zone) 타일 — 용암/빙하지대 셀 배경 교체용
        private Sprite lavaTileSprite, chillTileSprite;
        // 하위호환 별칭
        private Sprite attackRangeSprite;
        private Sprite moveRangeSprite;

        // ── RangeViewPanel용 공개 접근자 ──
        public GameObject CellPrefab => cellPrefab;
        public GameObject UnitPrefab => unitPrefab;
        public GameObject HandCardPrefab => cardPrefab;
        public Sprite PlayerAttackUnactive => playerAttackUnactive;
        public Sprite PlayerAttackActive   => playerAttackActive;
        public Sprite PlayerMoveUnactive   => playerMoveUnactive;
        public Sprite PlayerMoveActive     => playerMoveActive;
        public Sprite PlayerPowerUnactive  => playerPowerUnactive;
        public Sprite PlayerPowerActive    => playerPowerActive;

        // ── 아이템 효과 상태 ──────────────────────────────────────────────
        private List<StatusEntry> playerStatusEffects  = new List<StatusEntry>();
        private int    nextAttackDamageMultiplier = 1;    // 분노의 포션
        private bool   nextCardDoublePlay         = false; // (구) 재사용 포션
        private bool   nextCardKeepInHand         = false; // 재사용 포션 (203): 다음 카드는 버려지지 않음
        private bool   nextCardCopyExhaust        = false; // 재사용 포션 (108): 다음 카드를 복사하고 복사본에 소멸 부여
        private float  outgoingDamageMultiplier   = 1f;   // 철가면
        private float  incomingDamageMultiplier   = 1f;   // 철가면
        private int    tempStrengthFromItem       = 0;    // 초코바 (턴 종료 시 회수)
        private int    dodgeCharges               = 0;    // 군번줄
        private ItemData revivePassiveItem        = null;  // 소생의 팬던트
        private float  reviveHealPercent          = 0.3f;

        // ── 발사 시스템 (Shoot) — 전역 사거리/추가 데미지 ───────────────────
        // CSV의 customEffectId가 "Shoot" 계열인 카드들이 공유한다. 키워드 대신 함수 ID로 식별.
        // - playerShootRange : 기본 1. 33100/33101 ShootRange 카드로 누적 증가 (전투 종료 시 1로 복원).
        // - playerShootDamage: 기본 0. 33102/33103 ShootDamage 카드로 누적 증가 (전투 종료 시 0으로 복원).
        // - playerDodge      : 33200/33201 Dodge 카드 등에서 ControllerAddDodge로 적립되는 회피.
        private static int s_playerShootRange  = 1;
        private static int s_playerShootDamage = 0;
        public  static int CurrentShootRange   => s_playerShootRange;
        public  static int CurrentShootDamage  => s_playerShootDamage;
        // 이번 턴 데미지 배율(23302 최후의 공격 등)의 static 미러 — 카드 {D} 표시(CardData.GetFormattedDescription)에서 참조.
        // 인스턴스 outgoingDamageMultiplier가 변경/초기화될 때마다 동기화한다.
        private static float s_outgoingDamageMultiplier = 1f;
        public  static float CurrentOutgoingDamageMultiplier => s_outgoingDamageMultiplier;
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

        // 105/109/202/301/302 — hover/사용 시 유지되는 attack 미리보기 (targeting 진입시 일반 hover preview는
        // ClearItemHoverPreview에서 지워지므로 별도 리스트로 분리해야 use 이후에도 유지된다)
        private readonly List<GameObject> _itemAllTilePersistent = new List<GameObject>();
        private readonly Dictionary<Vector2Int, UnityEngine.UI.Image> _itemAllTileMap = new Dictionary<Vector2Int, UnityEngine.UI.Image>();
        private int  _itemAllTileCode = 0;
        private bool _itemAllTileHoverSubscribed = false;
        // true이면 hover-exit으로 자동 제거되지 않음(클릭으로 고정된 상태)
        private bool _itemAllTilePinned = false;

        public event Action<bool> OnCombatEnded;

        // ── Button slide-positioning state ─────────────────────
        private Vector2 _endTurnActivePos;
        private Vector2 _drawPileActivePos;
        private Vector2 _discardPileActivePos;
        private bool _buttonPositionsCaptured;
        private Coroutine _endTurnSlideCoroutine;
        private Coroutine _drawPileSlideCoroutine;
        private Coroutine _discardPileSlideCoroutine;

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

            // 맵 이동 카드 드래그 중 active sprite swap — Unity의 OnPointerEnter는 sticky+SetLocked로 인한
            // blocksRaycasts 토글 직후 hover 상태가 즉시 갱신되지 않아 누락되는 경우가 있다. 매 프레임 직접 raycast로
            // 커서 아래 셀을 찾아 sprite를 swap한다.
            UpdateMapMoveDragHover();

            // 맵 이동 카드 mouseup → 카드 fucntion 직접 실행 (CardUI의 IsRangeCard 경로가 AreaSelectable 셀을
            // 못 찾는 케이스 회피용 안전망). 커서 아래 셀이 있고, 그게 사거리 내 미방문 셀이면 즉시 이동.
            TryHandleMapMoveMouseUp();
        }

        private void TryHandleMapMoveMouseUp()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            if (!mouse.leftButton.wasReleasedThisFrame) return;
            if (pendingCard == null || !IsMapMoveCard(pendingCard)) return;
            if (combatState != CombatState.MapState
                && combatState != CombatState.Shop
                && combatState != CombatState.Rest
                && combatState != CombatState.Treasure) return;

            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return;

            var pd = new UnityEngine.EventSystems.PointerEventData(es)
            {
                position = mouse.position.ReadValue()
            };
            var results = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(pd, results);

            CombatBoardCell hoveredCell = null;
            foreach (var r in results)
            {
                if (r.gameObject == null) continue;
                var c = r.gameObject.GetComponentInParent<CombatBoardCell>();
                if (c != null) { hoveredCell = c; break; }
            }
            if (hoveredCell == null) return;

            // 카드의 사거리 내 미방문 셀인지 자체 검증 — CardUI의 IsRangeCard 경로를 우회하므로 여기서 직접 체크.
            var fx = pendingCard.Effects != null && pendingCard.Effects.Count > 0 ? pendingCard.Effects[0] : null;
            if (fx?.rangeOffsets == null) return;
            Vector2Int playerOnMap = new Vector2Int(mapPlayerPos.y, mapPlayerPos.x);
            bool inRange = false;
            foreach (var offset in fx.rangeOffsets)
                if (playerOnMap + offset.ToVector2Int() == hoveredCell.GridPos) { inRange = true; break; }
            if (!inRange) return;

            int col = hoveredCell.GridPos.x;
            int row = hoveredCell.GridPos.y;
            if (mapGrid == null || row < 0 || row >= boardRows || col < 0 || col >= boardCols) return;
            if (mapGrid[row, col].IsVisited || mapGrid[row, col].IsDisabled) return;

            Debug.Log($"[TryHandleMapMoveMouseUp] mouseup 검출 — {pendingCard.cardName} → cell {hoveredCell.GridPos}으로 이동 실행");

            // 손패에서 카드 제거 + 이동. PlayCard를 먼저 호출해 OnCardPlayed가 발화되어야 일관성 있게 카드 UI가 정리된다.
            int handIdx = hand.IndexOf(pendingCard);
            if (handIdx >= 0 && handIdx < handCardObjects.Count)
            {
                var cardObj = handCardObjects[handIdx];
                var cardUI = cardObj?.GetComponent<DeckRoguelike.Cards.CardUI>();
                // CardUI의 isPlayed=true로 마킹해 OnPointerUp 등에서 중복 처리 방지.
                cardUI?.MarkAsPlayed();
            }
            HandleMapMoveCellClicked(hoveredCell);
        }

        // 드래그 중 active로 swap된 셀 (이전 hover). 셀이 바뀌면 unactive로 복원하기 위해 추적한다.
        private CombatBoardCell _lastMapMoveHoveredCell;

        private void UpdateMapMoveDragHover()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool mousePressed = mouse != null && mouse.leftButton.isPressed;
            bool hasMapMovePending = pendingCard != null && IsMapMoveCard(pendingCard);
            bool stateOk = combatState == CombatState.MapState
                || combatState == CombatState.Shop
                || combatState == CombatState.Rest
                || combatState == CombatState.Treasure
                || combatState == CombatState.Victory;
            bool dragActive = mousePressed && hasMapMovePending && stateOk && hoverPreviewObjects.Count > 0;

            if (!dragActive)
            {
                // 드래그 종료/조건 미충족 — 마지막 active 셀을 unactive로 복원
                if (_lastMapMoveHoveredCell != null)
                {
                    if (playerMoveUnactive != null)
                        SwapPreviewSprite(_lastMapMoveHoveredCell.GridPos, playerMoveUnactive);
                    _lastMapMoveHoveredCell = null;
                }
                return;
            }

            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return;

            var pd = new UnityEngine.EventSystems.PointerEventData(es)
            {
                position = mouse.position.ReadValue()
            };
            var results = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(pd, results);

            // 첫 번째 CombatBoardCell — state 무관. preview가 없는 셀이면 SwapPreviewSprite가 no-op.
            // GetComponentInParent로 자식(overlay/background image)이 hit돼도 부모 셀을 찾는다.
            CombatBoardCell hoveredCell = null;
            foreach (var r in results)
            {
                if (r.gameObject == null) continue;
                var c = r.gameObject.GetComponentInParent<CombatBoardCell>();
                if (c != null) { hoveredCell = c; break; }
            }

            if (hoveredCell == _lastMapMoveHoveredCell) return;

            // 이전 셀 unactive 복원
            if (_lastMapMoveHoveredCell != null && playerMoveUnactive != null)
                SwapPreviewSprite(_lastMapMoveHoveredCell.GridPos, playerMoveUnactive);

            // 새 셀 active 적용
            if (hoveredCell != null && playerMoveActive != null)
                SwapPreviewSprite(hoveredCell.GridPos, playerMoveActive);

            _lastMapMoveHoveredCell = hoveredCell;
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

        private bool _initialized;

        public void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            endTurnButton?.onClick.AddListener(OnEndTurnClicked);
            // drawPile/discardPile은 SetPileButtonMode(Default)로 listener를 부여한다.
            SetPileButtonMode(PileButtonMode.Default);

            AddItemCancelOnHover(endTurnButton);
            AddItemCancelOnHover(drawPileButton);
            AddItemCancelOnHover(discardPileButton);

            CardEffectLibrary.RegisterAll();
            EnemyBehaviorLibrary.RegisterAll();
            AllyBehaviorLibrary.RegisterAll();
            HazardLibrary.RegisterAll();
            RelicLibrary.RegisterAll();
            ItemLibrary.RegisterAll();

            LoadRangePreviewSprites();
            LoadMapNodeSprites();
            EnsurePlayerObject();
        }

        private void EnsurePlayerObject()
        {
            if (playerObject != null || unitPrefab == null || combatBoard == null) return;

            playerObject = Instantiate(unitPrefab, combatBoard);
            foreach (var g in playerObject.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                g.raycastTarget = false;

            unitUI = playerObject.GetComponentInChildren<DeckRoguelike.Combat.UnitUI>(true);
            if (unitUI != null && GameManager.Instance != null)
                unitUI.UpdateHP(GameManager.Instance.CurrentHP, GameManager.Instance.MaxHP);

            // 맵 시작 좌표(0,0)로 이동 + 활성화
            RectTransform rt = playerObject.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = GetCellPosition(0, 0) + playerCellOffset;
            playerObject.SetActive(true);
        }

        private void Start()
        {
            EnsureInitialized();
            CaptureButtonActivePositions();
        }

        #region Button slide-positioning

        /// <summary>씬 로드 시 버튼들의 active anchoredPosition을 한 번 캡쳐. 이후 슬라이드의 기준점이 된다.</summary>
        private void CaptureButtonActivePositions()
        {
            if (_buttonPositionsCaptured) return;
            var endRT = endTurnButton != null ? endTurnButton.GetComponent<RectTransform>() : null;
            var drawRT = drawPileButton != null ? drawPileButton.GetComponent<RectTransform>() : null;
            var discardRT = discardPileButton != null ? discardPileButton.GetComponent<RectTransform>() : null;
            if (endRT != null) _endTurnActivePos = endRT.anchoredPosition;
            if (drawRT != null) _drawPileActivePos = drawRT.anchoredPosition;
            if (discardRT != null) _discardPileActivePos = discardRT.anchoredPosition;
            _buttonPositionsCaptured = true;
        }

        /// <summary>endTurnButton을 active 위치(왼쪽)로 슬라이드.</summary>
        public void ShowEndTurnButton()
        {
            if (endTurnButton == null) return;
            CaptureButtonActivePositions();
            var rt = endTurnButton.GetComponent<RectTransform>();
            if (rt == null) return;
            if (_endTurnSlideCoroutine != null) StopCoroutine(_endTurnSlideCoroutine);
            _endTurnSlideCoroutine = StartCoroutine(SlideRect(rt, _endTurnActivePos, buttonSlideDuration));
        }

        /// <summary>endTurnButton을 오른쪽 카메라 밖으로 슬라이드.</summary>
        public void HideEndTurnButton()
        {
            if (endTurnButton == null) return;
            CaptureButtonActivePositions();
            var rt = endTurnButton.GetComponent<RectTransform>();
            if (rt == null) return;
            if (_endTurnSlideCoroutine != null) StopCoroutine(_endTurnSlideCoroutine);
            _endTurnSlideCoroutine = StartCoroutine(SlideRect(rt, _endTurnActivePos + endTurnHiddenOffset, buttonSlideDuration));
        }

        /// <summary>draw/discardPile 버튼을 active 위치(위)로 슬라이드 — 전투 진입.</summary>
        public void ShowDeckPileButtons()
        {
            CaptureButtonActivePositions();
            SlidePileButton(drawPileButton,    _drawPileActivePos,    ref _drawPileSlideCoroutine);
            SlidePileButton(discardPileButton, _discardPileActivePos, ref _discardPileSlideCoroutine);
        }

        /// <summary>draw/discardPile 버튼을 아래(카메라 밖)으로 슬라이드 — 비활성화 대체.</summary>
        public void HideDeckPileButtons()
        {
            CaptureButtonActivePositions();
            SlidePileButton(drawPileButton,    _drawPileActivePos    + deckPileHiddenOffset, ref _drawPileSlideCoroutine);
            SlidePileButton(discardPileButton, _discardPileActivePos + deckPileHiddenOffset, ref _discardPileSlideCoroutine);
        }

        /// <summary>전투/상점 외 상태에서 InGameUIController가 호출 — 전투 액션 버튼 모두 카메라 밖으로.</summary>
        public void SetCombatActionButtonsVisible(bool visible)
        {
            if (visible)
            {
                ShowEndTurnButton();
                ShowDeckPileButtons();
            }
            else
            {
                HideEndTurnButton();
                HideDeckPileButtons();
            }
        }

        private void SlidePileButton(Button btn, Vector2 target, ref Coroutine slot)
        {
            if (btn == null) return;
            var rt = btn.GetComponent<RectTransform>();
            if (rt == null) return;
            if (slot != null) StopCoroutine(slot);
            slot = StartCoroutine(SlideRect(rt, target, buttonSlideDuration));
        }

        private IEnumerator SlideRect(RectTransform rt, Vector2 target, float duration)
        {
            if (rt == null) yield break;
            Vector2 start = rt.anchoredPosition;
            if (duration <= 0f) { rt.anchoredPosition = target; yield break; }
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float ek = 1f - (1f - k) * (1f - k); // ease-out
                rt.anchoredPosition = Vector2.Lerp(start, target, ek);
                yield return null;
            }
            rt.anchoredPosition = target;
        }

        #endregion

        private void LoadMapNodeSprites()
        {
            combatNodeIcon       = LoadSpriteSafe("Sprites/Ingame/map/1");
            dangerCombatNodeIcon = LoadSpriteSafe("Sprites/Ingame/map/2");
            eventNodeIcon        = LoadSpriteSafe("Sprites/Ingame/map/3");
            restNodeIcon         = LoadSpriteSafe("Sprites/Ingame/map/4");
            shopNodeIcon         = LoadSpriteSafe("Sprites/Ingame/map/5");
            bossNodeIcon         = LoadSpriteSafe("Sprites/Ingame/map/6");
            startNodeIcon        = combatNodeIcon;
            visitedNodeIcon      = LoadSpriteSafe("Sprites/Ingame/map/7");
            treasureNodeIcon     = LoadSpriteSafe("Sprites/Ingame/map/8");
        }

        private void LoadRangePreviewSprites()
        {
            // 플레이어 (아군도 공유)
            playerAttackUnactive = LoadSpriteSafe("Sprites/Ingame/Combat/Rangepreview/playerattack_unactive");
            playerAttackActive   = LoadSpriteSafe("Sprites/Ingame/Combat/Rangepreview/playerattack_active");
            playerMoveUnactive   = LoadSpriteSafe("Sprites/Ingame/Combat/Rangepreview/playermove_unacitive");
            playerMoveActive     = LoadSpriteSafe("Sprites/Ingame/Combat/Rangepreview/playmove_acitive ");
            playerPowerUnactive  = LoadSpriteSafe("Sprites/Ingame/Combat/Rangepreview/playerpower_unactive");
            playerPowerActive    = LoadSpriteSafe("Sprites/Ingame/Combat/Rangepreview/playerpower_active");
            // 적
            enemyAttackUnactive  = LoadSpriteSafe("Sprites/Ingame/Combat/Rangepreview/enemyattack_unactive");
            enemyAttackActive    = LoadSpriteSafe("Sprites/Ingame/Combat/Rangepreview/enemyattack_active");
            enemyMoveUnactive    = LoadSpriteSafe("Sprites/Ingame/Combat/Rangepreview/enemymove_unacitive");
            enemyMoveActive      = LoadSpriteSafe("Sprites/Ingame/Combat/Rangepreview/enemymove_acitive");
            // 지대(Zone) 타일
            lavaTileSprite       = LoadSpriteSafe("Sprites/Ingame/Combat/Tile/Common/lava");
            chillTileSprite      = LoadSpriteSafe("Sprites/Ingame/Combat/Tile/Common/chill");
            // 하위호환 별칭
            attackRangeSprite    = playerAttackUnactive;
            moveRangeSprite      = playerMoveUnactive;
        }

        /// <summary>Zone 위해 코드에 대응하는 셀 배경 타일 스프라이트. 없으면 null.</summary>
        private Sprite GetZoneTileSprite(int hazardCode)
        {
            switch (hazardCode)
            {
                case 32140: case 32141: return lavaTileSprite;   // 용암지대
                case 32242: case 32243: return chillTileSprite;  // 빙하지대
                default:                return null;
            }
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
            _playerMoveCount             = 0;
            _bonusHandSize               = 0;
            _firstTurnBonusDraw          = 0;
            _thisTurnBonusDraw           = 0;
            _timedBonusDraw              = 0;
            _timedBonusDrawTurns         = 0;
            _cardCostReduction           = 0;
            _firstActionBonusDamage      = 0;
            _firstAttackUsedThisCombat   = false;
            _grantingCombatStartCards    = false;
            _combatStartBonusCards       = 0;
            _teleportUsesThisCombat      = 0;
            _activePowers.Clear();
            _noCardHpLoss = false;
            _noExhaust    = false;
            _suppressOnEnemyDamaged = false;
            _temporarilyUpgradedCards.Clear();
            _shootRangeBackups.Clear();
            s_playerShootRange  = 1;
            s_playerShootDamage = 0;
            _shootCount         = 0;
            _trapCount          = 0;
            _randomCardOrigin.Clear();
            pendingCard     = null;
            pendingCardIndex = -1;
            allies.Clear();
            facingRight  = true;

            // 아이템 상태 초기화
            playerStatusEffects.Clear();
            nextAttackDamageMultiplier = 1;
            nextCardDoublePlay         = false;
            nextCardKeepInHand         = false;
            nextCardCopyExhaust        = false;
            outgoingDamageMultiplier   = 1f;
            incomingDamageMultiplier   = 1f;
            s_outgoingDamageMultiplier = 1f;
            tempStrengthFromItem       = 0;
            dodgeCharges               = 0;
            revivePassiveItem          = null;
            // 인벤토리에 소생의 팬던트(308)가 있으면 매 전투 시작 시 자동으로 패시브 활성화
            AutoRegisterRevivePendant();
            isItemMoveMode             = false;
            isItemTeleportMode         = false;
            isItemEnemyTargetingMode   = false;
            isItemAreaTargetingMode    = false;
            AnyItemTargetingActive     = false;
            _itemEnemyCallback         = null;
            _itemAreaCallback          = null;
            _itemTargetCancelCallback  = null;
            _itemConfirmedUseCallback  = null;
            _pendingItemSlot           = null;
            ClearItemHoverPreview();

            // 맵 위치를 전투 시작 좌표로 사용 + 전투 종료 시 복원에 사용 (RestoreMapPlayerPosAfterCombat).
            // 유효 좌표일 때만 saved 플래그를 set — 첫 전투 진입 전(mapPlayerPos<0) 호출되면 복원하지 않는다.
            savedMapPlayerPos = mapPlayerPos;
            _savedMapPlayerPosSet = (mapPlayerPos.x >= 0 && mapPlayerPos.y >= 0);
            Debug.Log($"[StartNewCombat] savedMapPlayerPos={savedMapPlayerPos} _savedMapPlayerPosSet={_savedMapPlayerPosSet}");
            if (mapInitialized && mapPlayerPos.x >= 0 && mapPlayerPos.y >= 0)
            {
                // mapPlayerPos는 (row, col), playerSpawnCell은 (col, row)
                playerSpawnCell = new Vector2Int(
                    Mathf.Clamp(mapPlayerPos.y, 0, boardCols - 1),
                    Mathf.Clamp(mapPlayerPos.x, 0, boardRows - 1));
            }
            else
            {
                int spawnX = (boardCols % 2 == 1) ? boardCols / 2 : boardCols / 2 - 1;
                playerSpawnCell = new Vector2Int(spawnX, playerInitialCell.y);
            }

            // 302 유물 — 전투 시작 위치를 임의로 설정
            if (GameManager.Instance != null && GameManager.Instance.HasRelic(302))
                playerSpawnCell = PickRandomPlayerSpawn();

            SetCombatHudActive(true);
            // 전투 진입 — endTurn은 왼쪽으로, draw/discard는 위로 슬라이드되어 active 위치로.
            ShowEndTurnButton();
            ShowDeckPileButtons();
            SetPileButtonMode(PileButtonMode.Default);
            InitializeDeck();
            InitializeBoard();
            PlacePlayer();
            DeckRoguelike.UI.InGameUIController.Instance?.ClearPlayerEffects();
            SpawnEnemies();

            // OnCombatStart 동안 생성되는 직업 유물 카드(90 순간이동/990 등)는 보너스로 취급한다.
            // _combatStartBonusCards에 누적해 첫 턴 startingHandSize 드로우 계산에서 제외(아래 StartPlayerTurn).
            _grantingCombatStartCards = true;
            FireRelicHook((r, ctx) => r.OnCombatStart(ctx));
            _grantingCombatStartCards = false;

            // 101/102 유물이 OnCombatStart에서 직전 노드 플래그를 읽었으므로 이제 소비/리셋
            GameManager.Instance?.ClearLastNodeFlags();

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

            // 전투 시작 시 모든 발사 공격 카드의 rangeOffsets를 BFS(1)로 채워둔다.
            // (CSV에는 rangeOffsets가 비어 있으므로, 함수에서 자체적으로 사거리 1을 부여.)
            RefreshShootCardRanges();
        }

        /// <summary>4x3 그리드 셀 생성</summary>
        private void InitializeBoard()
        {
            Debug.Log($"[BoardController] InitializeBoard 시작 — combatBoard={combatBoard != null}, active={combatBoard?.gameObject.activeInHierarchy}, cellPrefab={cellPrefab != null}, cols={boardCols}, rows={boardRows}");
            if (combatBoard == null) return;

            // EventSystem이 곧 파괴될 셀/카드를 selected/hovered로 잡고 있으면
            // ProcessPointerButton에서 MissingReferenceException 발생 → 사전에 해제
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null) es.SetSelectedGameObject(null);

            var toDestroy = new List<GameObject>();
            foreach (Transform child in combatBoard)
                if (child.gameObject != playerObject)
                    toDestroy.Add(child.gameObject);
            foreach (var go in toDestroy)
            {
                if (go == null) continue;
                go.SetActive(false);
                go.transform.SetParent(null, false);
                Destroy(go);
            }
            if (playerObject != null) playerObject.SetActive(false);

            Canvas.ForceUpdateCanvases();
            grid = new CombatBoardCell[boardCols, boardRows];

            int created = 0;
            for (int col = 0; col < boardCols; col++)
            {
                for (int row = 0; row < boardRows; row++)
                {
                    GameObject cellObj = CreateCellObject(col, row);
                    if (cellObj == null) { Debug.LogError($"[BoardController] CreateCellObject 반환 null ({col},{row})"); continue; }

                    RectTransform rt = cellObj.GetComponent<RectTransform>();
                    rt.anchoredPosition = GetCellPosition(col, row);
                    rt.sizeDelta        = new Vector2(cellWidth, cellHeight);

                    CombatBoardCell cell = cellObj.GetComponent<CombatBoardCell>();
                    if (cell == null) cell = cellObj.AddComponent<CombatBoardCell>();
                    cell.Initialize(new Vector2Int(col, row));
                    cell.OnCellClicked     += HandleCellClicked;
                    cell.OnCellHoverEnter  += OnEnemyCellHoverEnter;
                    cell.OnCellHoverExit   += OnEnemyCellHoverExit;
                    // 타게팅 sprite의 unactive→active 전환 조건:
                    //   • 셀에 진입(hover)하면 좌버튼 held 여부와 무관하게 즉시 swap → OnTargetingCellHoverEnter
                    //   • 셀 위에서 좌클릭 down 했을 때에도 swap (hover로 이미 swap됐어도 무해)  → OnCellPointerDown
                    // Exit(=active→unactive)는 hover-out 기준 — 마우스가 셀을 벗어나면 즉시 unactive 복원.
                    cell.OnCellPointerDown += OnTargetingCellHoverEnter;
                    cell.OnCellHoverEnter  += OnTargetingCellHoverEnter;
                    cell.OnCellHoverExit   += OnTargetingCellHoverExit;
                    grid[col, row] = cell;
                    created++;
                }
            }
            Debug.Log($"[BoardController] InitializeBoard 완료 — 셀 {created}개 생성, combatBoard 자식 수={combatBoard.childCount}");
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

        /// <summary>중앙 기준 셀 좌표 계산 (MapController와 동일 방식)</summary>
        private Vector2 GetCellPosition(int col, int row)
        {
            float xStart = -(boardCols - 1) / 2f * cellWidth;
            float yStart = -(boardRows - 1) / 2f * cellHeight;
            return new Vector2(xStart + col * cellWidth, yStart + row * cellHeight) + boardOffset;
        }

        private void PlacePlayer()
        {
            if (grid == null) return;

            EnsurePlayerObject();

            CombatBoardCell cell = grid[playerSpawnCell.x, playerSpawnCell.y];
            cell.IsPlayerHere = true;
            cell.SetState(CellState.PlayerOccupied);

            if (playerObject != null)
            {
                playerObject.SetActive(true);
                playerObject.transform.SetParent(combatBoard, false);
                RectTransform rt = playerObject.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = GetCellPosition(playerSpawnCell.x, playerSpawnCell.y) + playerCellOffset;
                playerObject.transform.SetAsLastSibling();

                if (unitUI != null && GameManager.Instance != null)
                    unitUI.UpdateHP(GameManager.Instance.CurrentHP, GameManager.Instance.MaxHP);
            }
        }

        /// <summary>302 유물: 적 위치와 겹치지 않는 임의의 셀을 반환합니다.</summary>
        private Vector2Int PickRandomPlayerSpawn()
        {
            var taken = new HashSet<Vector2Int>();
            if (currentEncounterData?.enemies != null)
            {
                foreach (var slot in currentEncounterData.enemies)
                    if (slot.enemyData != null)
                        taken.Add(new Vector2Int(slot.col, slot.row));
            }

            var candidates = new List<Vector2Int>();
            for (int c = 0; c < boardCols; c++)
                for (int r = 0; r < boardRows; r++)
                    if (!taken.Contains(new Vector2Int(c, r)))
                        candidates.Add(new Vector2Int(c, r));

            if (candidates.Count == 0)
                return playerSpawnCell;

            var picked = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            Debug.Log($"[CombatController] 302 유물 — 임의 시작 위치: {picked}");
            return picked;
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
            var positions = EnemyPlacementResolver.Resolve(
                currentEncounterData.enemies, playerSpawnCell, boardCols, boardRows, grid);

            for (int i = 0; i < currentEncounterData.enemies.Length; i++)
            {
                var slot = currentEncounterData.enemies[i];
                if (slot.enemyData == null) continue;

                Vector2Int pos = positions[i];
                if (pos.x < 0 || pos.x >= boardCols || pos.y < 0 || pos.y >= boardRows)
                {
                    Debug.LogWarning($"[CombatController] 인카운터 '{currentEncounterData.encounterName}': ({pos.x},{pos.y})은 배치 불가 - 스킵");
                    continue;
                }

                EnemyData data = slot.enemyData;
                int enemyDamage = data.baseDamage;
                // 910 유물 — 적들의 공격력이 2 증가
                if (GameManager.Instance != null && GameManager.Instance.HasRelic(910))
                    enemyDamage += 2;

                int enemyMaxHP = data.maxHP;
                // 214 유물 — 위험 전투에서 적 채력이 25% 감소
                if (GameManager.Instance != null
                    && GameManager.Instance.HasRelic(214)
                    && GameManager.Instance.IsDangerCombatEncounter)
                {
                    enemyMaxHP = Mathf.Max(1, Mathf.CeilToInt(enemyMaxHP * 0.75f));
                }

                EnemyInstance enemy = new EnemyInstance
                {
                    Name         = data.enemyName,
                    MaxHP        = enemyMaxHP,
                    Damage       = enemyDamage,
                    GridPos      = pos,
                    Data         = data,
                    SpawnGroupId = slot.spawnGroupId,
                };
                enemy.CurrentHP = enemy.MaxHP;
                enemy.Behavior  = EnemyBehaviorRegistry.Create(data.enemyId);

                PlaceEnemyOnCell(enemy, pos);
                enemy.Behavior?.OnSpawn(enemy, this);
                enemies.Add(enemy);
            }

            // 모든 적이 보드에 올라간 뒤 호출 — 군집 배치(뱀 몸통 부착 등)는 여기서 안전하게 수행.
            foreach (var enemy in enemies)
                enemy.Behavior?.OnAllEnemiesSpawned(enemy, this);

            Debug.Log($"[CombatController] '{currentEncounterData.encounterName}' 인카운터 - 적 {enemies.Count}마리 생성");
        }

/// <summary>EnemyInstance를 지정 셀에 배치 (SpawnEnemies + Summon 공용).
        /// 멀티셀 적은 footprint 전부에 OccupyingEnemy를 설정하고, 비주얼은 footprint 중앙에 W*H 크기로 배치.</summary>
        private void PlaceEnemyOnCell(EnemyInstance enemy, Vector2Int pos)
        {
            Vector2Int size = enemy.Size;
            SetEnemyFootprintCells(enemy, pos, size);

            if (unitPrefab != null)
            {
                // combatBoard 직접 자식으로 생성 → RangePreview보다 항상 위에 렌더링
                enemy.GameObject = Instantiate(unitPrefab, combatBoard);
                RectTransform rt = enemy.GameObject.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = GetEnemyVisualPosition(pos, size) + playerCellOffset;
                enemy.GameObject.transform.SetAsLastSibling();
                enemy.UI = enemy.GameObject.GetComponent<UnitUI>();
                // 멀티셀이면 스프라이트·HP바를 footprint 크기에 맞춰 확대/정렬 (1x1은 no-op)
                enemy.UI?.ApplyUnitSize(size, cellHeight);
                Sprite enemySprite = enemy.Data?.enemySprite;
                if (enemySprite == null && enemy.Data?.enemyId != 0)
                    enemySprite = Addressables.LoadAssetAsync<Sprite>($"Sprites/Ingame/EnemySprites/{enemy.Data.enemyId}").WaitForCompletion();
                enemy.UI?.Initialize(enemy.MaxHP, enemySprite);
                RefreshEnemyActionIcons(enemy);   // 스폰 직후 기본 행동 아이콘 표시
                foreach (var g in enemy.GameObject.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                    g.raycastTarget = false;
                // 스폰 시 플레이어 방향을 향하도록 초기 방향 설정
                bool facingRightInit = playerSpawnCell.x > pos.x;
                enemy.FacingRight = facingRightInit;
                enemy.UI?.SetFacingRight(facingRightInit);
            }
        }

        /// <summary>적의 footprint 셀들(W*H)을 모두 OccupyingEnemy로 표시.</summary>
        private void SetEnemyFootprintCells(EnemyInstance enemy, Vector2Int anchor, Vector2Int size)
        {
            for (int dx = 0; dx < size.x; dx++)
            for (int dy = 0; dy < size.y; dy++)
            {
                Vector2Int p = new Vector2Int(anchor.x + dx, anchor.y - dy);
                if (!IsInBoard(p)) continue;
                CombatBoardCell c = grid[p.x, p.y];
                c.OccupyingEnemy = enemy;
                c.SetState(CellState.EnemyOccupied);
            }
        }

        /// <summary>적의 footprint 셀들을 모두 비웁니다 (해당 적이 점유 중인 칸만 해제).</summary>
        private void ClearEnemyFootprintCells(EnemyInstance enemy)
        {
            if (grid == null || enemy == null) return;
            Vector2Int size = enemy.Size;
            for (int dx = 0; dx < size.x; dx++)
            for (int dy = 0; dy < size.y; dy++)
            {
                Vector2Int p = new Vector2Int(enemy.GridPos.x + dx, enemy.GridPos.y - dy);
                if (!IsInBoard(p)) continue;
                CombatBoardCell c = grid[p.x, p.y];
                if (c.OccupyingEnemy == enemy)
                    c.ClearUnit();
            }
        }

        /// <summary>
        /// 멀티셀 적의 비주얼 중앙 좌표 (앵커=왼쪽 위, 크기=W*H). 1x1은 GetCellPosition(anchor)와 동일.
        /// </summary>
        private Vector2 GetEnemyVisualPosition(Vector2Int anchor, Vector2Int size)
        {
            Vector2 tl = GetCellPosition(anchor.x, anchor.y);
            Vector2 br = GetCellPosition(anchor.x + size.x - 1, anchor.y - size.y + 1);
            return (tl + br) * 0.5f;
        }

        /// <summary>
        /// (anchor, size)의 footprint가 모두 보드 안이며 빈 칸이거나 무시 대상(ignoreEnemy)이 점유 중인지 확인.
        /// 멀티셀 적의 이동/배치 가능 여부 검사용. (1x1이면 IsCellFreeForEnemy와 동일한 결과)
        /// </summary>
        public bool IsFootprintFreeForEnemy(Vector2Int anchor, Vector2Int size, EnemyInstance ignoreEnemy = null)
        {
            for (int dx = 0; dx < size.x; dx++)
            for (int dy = 0; dy < size.y; dy++)
            {
                Vector2Int p = new Vector2Int(anchor.x + dx, anchor.y - dy);
                if (!IsInBoard(p)) return false;
                CombatBoardCell c = grid[p.x, p.y];
                if (c.IsPlayerHere) return false;
                if (c.OccupyingAlly != null) return false;
                if (c.OccupyingEnemy != null && c.OccupyingEnemy != ignoreEnemy) return false;
            }
            return true;
        }

        /// <summary>아군 footprint 셀들(W*H)을 모두 OccupyingAlly로 표시.</summary>
        private void SetAllyFootprintCells(AllyInstance ally, Vector2Int anchor, Vector2Int size)
        {
            for (int dx = 0; dx < size.x; dx++)
            for (int dy = 0; dy < size.y; dy++)
            {
                Vector2Int p = new Vector2Int(anchor.x + dx, anchor.y - dy);
                if (!IsInBoard(p)) continue;
                CombatBoardCell c = grid[p.x, p.y];
                c.OccupyingAlly = ally;
                c.SetState(CellState.AllyOccupied);
            }
        }

        /// <summary>아군 footprint 셀들을 모두 비웁니다 (해당 아군이 점유 중인 칸만 해제).</summary>
        private void ClearAllyFootprintCells(AllyInstance ally)
        {
            if (grid == null || ally == null) return;
            Vector2Int size = ally.Size;
            for (int dx = 0; dx < size.x; dx++)
            for (int dy = 0; dy < size.y; dy++)
            {
                Vector2Int p = new Vector2Int(ally.GridPos.x + dx, ally.GridPos.y - dy);
                if (!IsInBoard(p)) continue;
                CombatBoardCell c = grid[p.x, p.y];
                if (c.OccupyingAlly == ally)
                    c.ClearUnit();
            }
        }

        /// <summary>
        /// (anchor, size)의 footprint가 모두 보드 안이며 플레이어·적·다른 아군이 없는지 확인.
        /// 멀티셀 아군의 소환/이동 가능 여부 검사용. (1x1이면 단일 셀 검사와 동일)
        /// </summary>
        public bool IsFootprintFreeForAlly(Vector2Int anchor, Vector2Int size, AllyInstance ignoreAlly = null)
        {
            for (int dx = 0; dx < size.x; dx++)
            for (int dy = 0; dy < size.y; dy++)
            {
                Vector2Int p = new Vector2Int(anchor.x + dx, anchor.y - dy);
                if (!IsInBoard(p)) return false;
                CombatBoardCell c = grid[p.x, p.y];
                if (c.IsPlayerHere) return false;
                if (c.OccupyingEnemy != null) return false;
                if (c.OccupyingAlly != null && c.OccupyingAlly != ignoreAlly) return false;
            }
            return true;
        }

        /// <summary>
        /// 멀티셀 BFS 경로 탐색용 — 대상 셀(ignoreCell)을 통과 가능한 빈 칸으로 간주.
        /// 적은 실제로 footprint가 target 위에 안착할 수 없지만, target 주변으로 가는 경로를
        /// 탐색할 때 target 셀이 footprint를 막아 진입조차 못하는 문제를 회피하기 위해 사용.
        /// 실제 안착 가능 여부는 IsFootprintFreeForEnemy로 별도 확인할 것.
        /// </summary>
        public bool IsFootprintFreeForBfs(Vector2Int anchor, Vector2Int size, EnemyInstance ignoreEnemy, Vector2Int ignoreCell)
        {
            for (int dx = 0; dx < size.x; dx++)
            for (int dy = 0; dy < size.y; dy++)
            {
                Vector2Int p = new Vector2Int(anchor.x + dx, anchor.y - dy);
                if (!IsInBoard(p)) return false;
                if (p == ignoreCell) continue;
                CombatBoardCell c = grid[p.x, p.y];
                if (c.IsPlayerHere) return false;
                if (c.OccupyingAlly != null) return false;
                if (c.OccupyingEnemy != null && c.OccupyingEnemy != ignoreEnemy) return false;
            }
            return true;
        }

        /// <summary>
        /// 공격자가 피격자의 등 뒤에 있는지 판정합니다.
        /// 피격자 FacingRight=true(오른쪽) → 등은 왼쪽 → 공격자.x &lt; 피격자.x 시 뒷면.
        /// 피격자 FacingRight=false(왼쪽) → 등은 오른쪽 → 공격자.x &gt; 피격자.x 시 뒷면.
        /// 같은 x 좌표(위/아래)는 뒷면 아님.
        /// </summary>
        public static bool IsAttackFromBehind(Vector2Int defenderPos, bool defenderFacingRight, Vector2Int attackerPos)
        {
            if (attackerPos.x == defenderPos.x) return false;
            return defenderFacingRight ? attackerPos.x < defenderPos.x : attackerPos.x > defenderPos.x;
        }

        /// <summary>적의 바라보는 방향을 설정합니다. UnitUI와 EnemyInstance.FacingRight를 동기화.</summary>
        public void SetEnemyFacingRight(EnemyInstance enemy, bool right)
        {
            if (enemy == null) return;
            enemy.FacingRight = right;
            enemy.UI?.SetFacingRight(right);
        }

        /// <summary>아군의 바라보는 방향을 설정합니다.</summary>
        public void SetAllyFacingRight(AllyInstance ally, bool right)
        {
            if (ally == null) return;
            ally.FacingRight = right;
            ally.UI?.SetFacingRight(right);
        }

        /// <summary>
        /// 105/109/201/202/208/301/302 아이템 hover/사용 시 전체 타일에 사거리 스프라이트를 표시합니다.
        /// 폭탄(202/302)은 플레이어 셀에도 표시, 나머지는 플레이어 셀 제외.
        /// 208 포탈건은 위치 교환(이동) 아이템이라 attack 대신 move 스프라이트를 사용합니다.
        /// </summary>
        public void ShowItemAllTilePreview(int itemCode)
        {
            if (grid == null || combatState != CombatState.PlayerTurn) return;
            if (itemCode != 105 && itemCode != 109 && itemCode != 201 && itemCode != 202 && itemCode != 208 && itemCode != 301 && itemCode != 302) return;

            ClearItemAllTilePreview();

            // 208 포탈건은 move 스프라이트, 그 외는 attack 스프라이트.
            Sprite sprite = (itemCode == 208) ? playerMoveUnactive : playerAttackUnactive;
            if (sprite == null) return;

            _itemAllTileCode = itemCode;
            // 폭탄(202/302)은 클릭 셀 중심 3×3 전체 범위라 플레이어 셀도 포함.
            bool includePlayer = (itemCode == 202 || itemCode == 302);

            for (int col = 0; col < boardCols; col++)
            for (int row = 0; row < boardRows; row++)
            {
                var pos = new Vector2Int(col, row);
                if (!includePlayer && pos == playerSpawnCell) continue;

                // 카드 hover 미리보기와 동일한 크기/배치 규칙 사용 (playerRangePreviewScale 반영)
                var obj = new GameObject("ItemAllTilePreview", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                obj.transform.SetParent(combatBoard, false);
                var rt = obj.GetComponent<RectTransform>();
                rt.anchoredPosition = GetCellPosition(pos.x, pos.y);
                rt.sizeDelta = new Vector2(cellWidth * playerRangePreviewScale, cellHeight * playerRangePreviewScale);
                var img = obj.GetComponent<UnityEngine.UI.Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                obj.transform.SetAsLastSibling();
                _itemAllTilePersistent.Add(obj);
                _itemAllTileMap[pos] = img;
            }
            BringUnitsToFront();

            // hover 시 대상 셀(폭탄=3×3 전체 / 그 외=적이 있는 셀)을 active sprite로 교체하기 위한 셀 hover 구독
            SubscribeItemAllTileHover();
        }

        private void SubscribeItemAllTileHover()
        {
            if (_itemAllTileHoverSubscribed || grid == null) return;
            for (int col = 0; col < boardCols; col++)
                for (int row = 0; row < boardRows; row++)
                {
                    grid[col, row].OnCellHoverEnter += OnItemAllTileHoverEnter;
                    grid[col, row].OnCellHoverExit  += OnItemAllTileHoverExit;
                }
            _itemAllTileHoverSubscribed = true;
        }

        private void UnsubscribeItemAllTileHover()
        {
            if (!_itemAllTileHoverSubscribed || grid == null) return;
            for (int col = 0; col < boardCols; col++)
                for (int row = 0; row < boardRows; row++)
                {
                    grid[col, row].OnCellHoverEnter -= OnItemAllTileHoverEnter;
                    grid[col, row].OnCellHoverExit  -= OnItemAllTileHoverExit;
                }
            _itemAllTileHoverSubscribed = false;
        }

        private void OnItemAllTileHoverEnter(CombatBoardCell cell)
        {
            // 208 포탈건은 move active, 그 외는 attack active.
            Sprite activeSprite = (_itemAllTileCode == 208) ? playerMoveActive : playerAttackActive;
            if (activeSprite == null) return;
            // 폭탄(202/302): hover된 셀 중심 실제 공격범위 3×3을 active 스프라이트로 교체 (모든 좌표에서 반응)
            if (_itemAllTileCode == 202 || _itemAllTileCode == 302)
            {
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var p = cell.GridPos + new Vector2Int(dx, dy);
                        if (_itemAllTileMap.TryGetValue(p, out var img) && img != null)
                            img.sprite = activeSprite;
                    }
                return;
            }
            // 그 외 (105/109/208/301): 적이 있는 셀만 active 스프라이트로 교체
            if (cell.OccupyingEnemy == null || cell.OccupyingEnemy.CurrentHP <= 0) return;
            if (_itemAllTileMap.TryGetValue(cell.GridPos, out var img2) && img2 != null)
                img2.sprite = activeSprite;
        }

        private void OnItemAllTileHoverExit(CombatBoardCell cell)
        {
            // 208 포탈건은 move unactive, 그 외는 attack unactive.
            Sprite unactiveSprite = (_itemAllTileCode == 208) ? playerMoveUnactive : playerAttackUnactive;
            if (unactiveSprite == null) return;
            if (_itemAllTileCode == 202 || _itemAllTileCode == 302)
            {
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var p = cell.GridPos + new Vector2Int(dx, dy);
                        if (_itemAllTileMap.TryGetValue(p, out var img) && img != null)
                            img.sprite = unactiveSprite;
                    }
                return;
            }
            if (_itemAllTileMap.TryGetValue(cell.GridPos, out var img2) && img2 != null)
                img2.sprite = unactiveSprite;
        }

        /// <summary>위 아이템 hover 종료 시 호출.</summary>
        public void HideItemAllTilePreview()
        {
            ClearItemAllTilePreview();
        }

        /// <summary>지정 itemCode의 all-tile preview가 현재 표시 중인지 반환합니다.</summary>
        public bool IsItemAllTilePreviewActive(int itemCode)
            => _itemAllTileCode != 0 && _itemAllTileCode == itemCode && _itemAllTilePersistent.Count > 0;

        /// <summary>클릭으로 고정 토글 — 이미 고정된 같은 코드면 끄고, 아니면 켜고 고정.
        /// hover로만 표시된(미고정) 상태에서 클릭한 경우엔 고정만 부여하고 ON으로 반환한다
        /// (이 경우 confirm 패널이 열려야 하므로 끄지 않는다).</summary>
        public bool ToggleItemAllTilePreviewPinned(int itemCode)
        {
            if (IsItemAllTilePreviewActive(itemCode))
            {
                if (_itemAllTilePinned)
                {
                    ClearItemAllTilePreview();
                    return false;
                }
                // hover로 켜져 있던 상태 → 클릭은 "고정" 의미
                _itemAllTilePinned = true;
                return true;
            }
            ShowItemAllTilePreview(itemCode);
            _itemAllTilePinned = true;
            return true;
        }

        /// <summary>hover-exit 시 호출 — 고정 상태가 아닐 때만 제거.</summary>
        public void HideItemAllTilePreviewIfNotPinned()
        {
            if (_itemAllTilePinned) return;
            ClearItemAllTilePreview();
        }

        /// <summary>preview 고정 해제 — 표시 자체는 유지하되 다음 hover-exit에서 자연스럽게 클리어되게 한다.</summary>
        public void UnpinItemAllTilePreview()
        {
            _itemAllTilePinned = false;
        }

        /// <summary>persistent all-tile preview를 모두 제거합니다.</summary>
        public void ClearItemAllTilePreview()
        {
            UnsubscribeItemAllTileHover();
            foreach (var obj in _itemAllTilePersistent)
                if (obj != null) Destroy(obj);
            _itemAllTilePersistent.Clear();
            _itemAllTileMap.Clear();
            _itemAllTileCode = 0;
            _itemAllTilePinned = false;
        }

        #endregion

        #region Turn Management

        private void StartPlayerTurn()
        {
            turnNumber++;
            combatState = CombatState.PlayerTurn;
            Debug.Log($"[CombatController] 플레이어 턴 {turnNumber}");

            // 303 유물용 "잔류 카드만큼 덜 뽑기"는 이전 턴에서 남은 카드만 세야 한다.
            // 유물 70/970 등이 OnPlayerTurnStart에서 '생성'하는 카드는 추가분이므로 드로우를 깎으면 안 됨.
            // → 유물 훅이 카드를 생성하기 전의 손패 수를 스냅샷한다.
            // 또한 OnCombatStart에서 생성된 직업 유물 카드(90 순간이동/990 등)도 보너스이므로 제외한다
            // (첫 턴에만 _combatStartBonusCards > 0; 사용 후 0으로 초기화).
            int leftoverHandCount = Mathf.Max(0, hand.Count - _combatStartBonusCards);
            _combatStartBonusCards = 0;

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
            s_outgoingDamageMultiplier = 1f;
            DeckRoguelike.UI.InGameUIController.Instance?.RemovePlayerEffect("multiple_attack");
            DeckRoguelike.UI.InGameUIController.Instance?.RemovePlayerEffect("iron_mask");
            if (tempStrengthFromItem > 0)
            {
                playerStrength   -= tempStrengthFromItem;
                tempStrengthFromItem = 0;
                RefreshHandDisplay();
                DeckRoguelike.UI.InGameUIController.Instance?.SetPlayerEffect("strength", strengthIcon, playerStrength, "힘", "공격 시 추가 피해를 줍니다.");
            }

            // 플레이어 화염 피해 처리
            ProcessPlayerFireStatus();

            _cardCostReduction = 0;

            playerBlock = 0;
            UpdateBlockDisplay();

            int firstTurnExtra = (turnNumber == 1) ? _firstTurnBonusDraw : 0;
            int timedExtra = (_timedBonusDrawTurns > 0) ? _timedBonusDraw : 0;
            if (_timedBonusDrawTurns > 0) _timedBonusDrawTurns--;
            int drawTotal      = startingHandSize + _bonusHandSize + firstTurnExtra + _thisTurnBonusDraw + timedExtra;
            // 303 유물 — 이전 턴 카드가 손에 남아 있으면 그만큼 덜 뽑음
            // (이번 턴 유물 70/970 등이 생성한 카드는 제외 — leftoverHandCount = 훅 실행 전 손패 수)
            drawTotal = Mathf.Max(0, drawTotal - leftoverHandCount);
            DrawCards(drawTotal);
            _thisTurnBonusDraw = 0;
            _cardsPlayedThisTurn = 0; // 904 유물 — 턴마다 카운터 초기화

            // 직업별 매 턴 자동 생성 카드는 유물 70(WarriorStartHealRelic) /
            // 970(WarriorBossHealRelic)이 OnPlayerTurnStart 훅에서 직접 처리합니다.
            UpdateTurnIndicator("내 턴");
            endTurnButton.interactable = true;

            foreach (var enemy in enemies)
            {
                if (enemy.CurrentHP <= 0) continue;
                // RePlan이 패턴 카운터를 매번 동일 시작점으로 되돌릴 수 있도록 초기 PlanTurn 직전 값을 스냅.
                enemy.Behavior?.BeginTurnSeqSnapshot();
                enemy.Behavior?.PlanTurn(enemy, this);
                // 공격/스킬은 PlanTurn에서 좌표가 확정되지만, 이동은 지연(Phase 2) — 첫 step만 미리 계산해
                // plannedMovePositions에 담아 미리보기로 노출.
                enemy.Behavior?.ComputeDeferredMovePreview(enemy, this);
                RefreshEnemyActionIcons(enemy);
            }
            foreach (var ally in allies)
            {
                ally.Behavior?.PlanTurn(ally, this);
                RefreshAllyActionIcons(ally);
            }

            // 카드가 hover되지 않은 기본 상태에서도 적·아군 행동 미리보기를 표시한다
            ShowUnitAttackPreviews();
        }

        /// <summary>
        /// 살아있는 모든 적의 PlanTurn을 다시 호출해 행동을 완전히 재계산한다.
        ///
        /// 호출 시점 — 플레이어 턴 중 보드 상태가 바뀌는 모든 순간:
        ///   - 플레이어 이동 (MovePlayer)
        ///   - 적/아군의 위치 변경 (TryMoveEnemy·TryMoveAlly — 밀침/끌어당김/점프대 등)
        ///   - 적 처치·이탈로 셀이 비는 경우 (데미지 사망, RemoveEnemyFromCombat)
        ///   - 아군 사망·이탈 (RemoveAllyFromCombat, 데미지 사망)
        /// 적의 BFS 경로·타겟·사거리 판정은 다른 유닛의 위치와 빈 칸에 의존하므로, 이들이 바뀌면
        /// 재계산해 인텐트 미리보기를 최신 보드 상태에 맞춘다.
        ///
        /// ResetPlanState로 이전 슬롯을 모두 비운 뒤 PlanTurn을 다시 호출 → 적의 결정이 현재 보드
        /// 기준으로 처음부터 다시 내려진다:
        ///   - 사거리에 들어오면 이동→공격으로 전환  (예: 슬라임·대왕슬라임·박쥐)
        ///   - 사거리에서 벗어나면 공격→이동으로 전환
        ///   - 같은 모드 안에서는 공격 대상/이동 방향이 새 위치를 추적
        /// ResetPlanState가 이전 슬롯을 전부 비우므로 stale 슬롯에 의한 이중 실행(공격+이동 중복)은 없다.
        /// _seqCounter는 스냅샷/복원해 NextSequential 기반 패턴(궁수 충전→공격, 악마군주 phase1 등)이
        /// 이중 진행되지 않게 한다. KingSlime의 분열처럼 OnDamaged에서 세팅된 상태 플래그(_splitArmed 등)는
        /// PlanTurn이 다시 읽어 그대로 재등록하므로 재계산해도 유지된다.
        ///
        /// combatState != PlayerTurn이면 즉시 반환 — 적 턴 실행(Phase 1/2) 도중에는 재계획하지 않는다.
        /// 적 턴의 동적 이동은 Phase 2의 정렬·vacating 로직이 따로 처리하므로, 적 턴에 불리는
        /// 이동/제거 경로에서 이 메서드를 호출해도 안전하게 no-op이 된다.
        /// </summary>
        private void RePlanAllEnemyTurns()
        {
            if (combatState != CombatState.PlayerTurn) return;

            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.CurrentHP <= 0 || enemy.Behavior == null) continue;
                if (enemy.IsIncapacitated) continue;

                int snap = enemy.Behavior.SeqCounterSnapshot;
                enemy.Behavior.ResetPlanState();
                // PlanTurn 재호출 전에 turn 시작 시 값으로 되돌려, 매 RePlan이 같은 시작점에서 패턴을 결정.
                enemy.Behavior.RestoreSeqCounter(snap);
                enemy.Behavior.PlanTurn(enemy, this);
                enemy.Behavior.ComputeDeferredMovePreview(enemy, this);
                RefreshEnemyActionIcons(enemy);
            }

            ShowUnitAttackPreviews();
        }

        /// <summary>지정한 적 한 명의 plan을 강제로 재계산. OnDamaged 등에서 임계값 진입으로
        /// 행동을 즉시 교체해야 할 때 호출. ExecuteTurn 도중에는 호출하지 말 것 — slot 정리와 충돌함.</summary>
        public void RePlanEnemyNow(EnemyInstance enemy)
        {
            if (enemy == null || enemy.Behavior == null) return;
            if (enemy.CurrentHP <= 0) return;
            if (enemy.IsIncapacitated) return;

            int snap = enemy.Behavior.SeqCounterSnapshot;
            enemy.Behavior.ResetPlanState();
            // PlanTurn 재호출 전에 turn 시작 시 값으로 되돌려, 매 RePlan이 같은 시작점에서 패턴을 결정.
            enemy.Behavior.RestoreSeqCounter(snap);
            enemy.Behavior.PlanTurn(enemy, this);
            enemy.Behavior.ComputeDeferredMovePreview(enemy, this);
            RefreshEnemyActionIcons(enemy);

            if (combatState == CombatState.PlayerTurn)
                ShowUnitAttackPreviews();
        }

        private void OnEndTurnClicked()
        {
            if (combatState == CombatState.Shop)
            {
                // 상점형 이벤트는 페이지 시스템 없음 — endTurn 버튼은 숨겨져 있어야 하지만 안전망으로 무시.
                if (_currentShopEventCode > 0) return;
                OnShopPageSwitch();
                return;
            }
            if (combatState != CombatState.PlayerTurn) return;
            CancelTargeting();
            PlaySound(endTurnSound);
            EndPlayerTurn();
        }

        private void EndPlayerTurn()
        {
            Debug.Log("[CombatController] 플레이어 턴 종료");
            FireRelicHook((r, ctx) => r.OnPlayerTurnEnd(ctx));
            // 파워 OnTurnEnd 훅 발동 — 미소비된 1회성 파워(22212 NextUseCopy 등)는 여기서 만료된다.
            // UnregisterPower가 _activePowers를 수정하므로 스냅샷을 순회한다.
            var endTurnSnapshot = new List<CombatPowerEffect>(_activePowers);
            foreach (var p in endTurnSnapshot) p.OnTurnEnd(this);
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

            // ── Phase 1: 기타·공격·즉시이동 슬롯 실행 (등록된 PlanTurn 결과) ──
            // 자폭/분열로 SummonEnemy가 enemies 리스트에 새 적을 추가할 수 있어 foreach 도중
            // InvalidOperationException이 발생하면 coroutine이 깨져 player 턴으로 복귀하지 못한다.
            // 스냅샷을 떠서 안전하게 순회한다. 이번 턴에 새로 소환된 적은 다음 턴부터 행동.
            var phase1Snapshot = new List<EnemyInstance>(enemies);
            foreach (var enemy in phase1Snapshot)
            {
                if (enemy == null || enemy.CurrentHP <= 0) continue;

                // 화염 피해 처리
                ProcessEnemyFireStatus(enemy);
                if (enemy.CurrentHP <= 0) { CheckVictory(); continue; }

                // 기절·빙결: 이번 턴 스킵 — Phase 2도 건너뛰도록 deferred도 정리
                if (ConsumeEnemyStun(enemy))
                {
                    enemy.Behavior?.OnTurnFullyResolved(enemy, this);
                    continue;
                }

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
                    HandlePlayerDefeatInCombat();
                    yield break;
                }

                yield return new WaitForSeconds(enemyTurnDelay);
            }

            // ── Phase 2: 지연 이동(PlanMoveTowardPlayer/Nearest/Ally) BFS 실행 ──
            // 둘러싸기 전략: target까지 가까운 적부터 먼저 이동 → 그 칸이 비워져 후순위 적의 BFS가 더 짧아짐.
            yield return StartCoroutine(ResolveDeferredEnemyMovesRoutine());

            // 지연 이동 슬롯 정리
            foreach (var enemy in enemies)
            {
                if (enemy?.Behavior == null) continue;
                enemy.Behavior.OnTurnFullyResolved(enemy, this);
            }

            // ── Phase 3: 적 턴 종료 위해(Hazard) 처리 ──
            // 모든 적의 공격·이동이 끝난 후, 트랩(설치/Trap) 위에 서 있는 적에게 위해를 일괄 발동한다.
            // (적이 밟는 즉시가 아니라 이 단일 시점에만 발동 → 1회성 트랩은 발동 후 제거)
            ProcessTrapEndOfEnemyTurn();

            // Zone kind 위해(용암지대/빙하지대 등): Zone 셀 위에 머무는 유닛에게 1회 효과 부여.
            // Zone은 사라지지 않고 매 적턴 종료마다 반복 발동.
            ProcessZoneEndOfEnemyTurn();

            // 적 턴 종료 위해(트랩/Zone)로 마지막 적이 죽어 전투가 끝났다면 CheckVictory가 맵 전환을 수행해
            // combatState가 EnemyTurn이 아니게 된다. 이 경우 아래 StartPlayerTurn이 상태를 덮어쓰지 않도록 종료한다.
            if (combatState != CombatState.EnemyTurn)
                yield break;

            // 속박 지속시간 감소 — 이번 적 턴 동안 이동이 차단된 뒤 1턴씩 소모.
            DecrementEnemyBondage();

            if (GameManager.Instance != null && GameManager.Instance.CurrentHP <= 0)
            {
                HandlePlayerDefeatInCombat();
                yield break;
            }

            StartPlayerTurn();
        }

        /// <summary>Phase 2 — 각 적의 deferred 이동 step을 1회씩 라운드-로빈으로 실행.
        /// 매 라운드마다 target까지 거리 오름차순으로 다시 정렬해 가까운 적이 먼저 이동.
        /// </summary>
        private IEnumerator ResolveDeferredEnemyMovesRoutine()
        {
            // 안전을 위해 step 라운드 상한: 살아있는 적의 가장 큰 deferred steps × 적 수
            int maxRounds = 0;
            foreach (var e in enemies)
            {
                if (e == null || e.CurrentHP <= 0 || e.Behavior == null) continue;
                if (e.Behavior.HasDeferredMove)
                    maxRounds = Mathf.Max(maxRounds, e.Behavior.DeferredMoveSteps);
            }
            if (maxRounds <= 0) yield break;

            for (int round = 0; round < maxRounds; round++)
            {
                // 이번 라운드에 이동 가능한 적 모음 (deferred가 남아있는 적)
                var pending = new List<EnemyInstance>();
                foreach (var e in enemies)
                {
                    if (e == null || e.CurrentHP <= 0 || e.Behavior == null) continue;
                    if (e.IsIncapacitated) continue;
                    if (e.IsBound) continue; // 속박: 이동 불가 (공격은 Phase 1에서 이미 수행)
                    if (!e.Behavior.HasDeferredMove) continue;
                    pending.Add(e);
                }
                if (pending.Count == 0) yield break;

                // 각 적의 sort anchor까지 맨해튼 거리 오름차순 → 가까운 적부터 이동 (셀 비우기 효과)
                pending.Sort((a, b) =>
                {
                    Vector2Int ta = a.Behavior.GetDeferredSortAnchor(a, this);
                    Vector2Int tb = b.Behavior.GetDeferredSortAnchor(b, this);
                    int da = Mathf.Abs(a.GridPos.x - ta.x) + Mathf.Abs(a.GridPos.y - ta.y);
                    int db = Mathf.Abs(b.GridPos.x - tb.x) + Mathf.Abs(b.GridPos.y - tb.y);
                    return da.CompareTo(db);
                });

                bool anyMoved = false;
                foreach (var e in pending)
                {
                    if (e.CurrentHP <= 0) continue;
                    bool moved = e.Behavior.ExecuteDeferredMoveStep(e, this);
                    if (moved)
                    {
                        anyMoved = true;
                        yield return new WaitForSeconds(enemyTurnDelay * 0.5f);
                    }
                }
                // 어떤 적도 못 움직이면 더 진행해도 동일 → 종료
                if (!anyMoved) yield break;
            }
        }

        #endregion

        #region Card Management

        private void DrawCards(int count)
        {
            // 카드 데이터(hand)는 즉시 동기적으로 확정한다. 드로우 애니메이션이 staggered로 진행되는
            // 도중 턴 종료(EndTurn)를 눌러도 hand에 모든 카드가 들어가 있어 DiscardHand가 빠짐없이 버린다.
            // (기존엔 코루틴이 stagger마다 한 장씩 hand.Add → 도중 종료 시 아직 안 뽑힌 카드가 적 턴 동안
            //  손패에 추가되어 버려지지 않는 버그가 있었다.)
            var drawn = new List<CardData>();
            for (int i = 0; i < count; i++)
            {
                if (hand.Count >= 10) break;
                CardData card = deckManager != null ? deckManager.DrawCard() : null;
                if (card == null) break;

                // Random_*Card: 손패에 들어오는 시점에 무작위 카드로 치환 (원본은 손패를 떠날 때 복원)
                card = TryTransformRandomCard(card);

                hand.Add(card);
                drawn.Add(card);
            }

            UpdatePileCounters(); // 덱에서 카드가 빠졌으므로 카운터 즉시 갱신
            if (drawn.Count == 0) return;

            // 비주얼/애니메이션만 staggered로 처리. 이전 드로우 코루틴이 남아 있으면 중단.
            if (_drawCardsCoroutine != null) StopCoroutine(_drawCardsCoroutine);
            _drawCardsCoroutine = StartCoroutine(DrawCardsCoroutine(drawn));
        }

        private IEnumerator DrawCardsCoroutine(List<CardData> drawn)
        {
            foreach (var card in drawn)
            {
                PlaySound(drawCardSound);

                // 카드 오브젝트 생성 후 손패 레이아웃 계산 (기존 카드 부드럽게 재배치)
                GameObject cardObj = CreateSingleCardObject(card);
                handCardObjects.Add(cardObj);
                RepositionHandCards();

                // 새 카드 드로우 애니메이션: 덱 위치 → 손패 위치
                CardUI cardUI = cardObj.GetComponent<CardUI>();
                if (cardUI != null && drawPileButton != null)
                    cardUI.StartDrawAnimation(drawPileButton.transform.position, cardDrawDuration);

                RefreshCardPlayability(); // 새 카드의 플레이 가능 여부 즉시 반영
                yield return new WaitForSeconds(drawStaggerDelay);
            }
            _drawCardsCoroutine = null;
        }

        private GameObject CreateSingleCardObject(CardData card)
        {
            if (cardPrefab == null || handContainer == null) return null;

            GameObject cardObj = Instantiate(cardPrefab, handContainer);
            CardUI cardUI = cardObj.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.Initialize(card, playerStrength, playerDexterity);
                cardUI.SetAsHandCard(); // 손패 전용: LayoutGroup 간섭 차단 및 크기 고정
                // Shop: Power 카드와 동일한 sticky/collapse 흐름. 맵 이동 카드는 구매가 아니라 "이동"이므로 제외.
                if (combatState == CombatState.Shop && !IsMapMoveCard(card)) cardUI.IsShopMode = true;
                // 카드 보상 픽: shop과 동일한 sticky/collapse 흐름을 재사용 (Move/Attack 타입도 EnterTargetingMode 없이 동작). 맵 이동 카드 제외.
                if (_rewardPickActive && _rewardPickCards.Contains(card) && !IsMapMoveCard(card)) cardUI.IsShopMode = true;
                Debug.Log($"[CreateSingleCardObject] card={card.cardName}({card.cardCode}) combatState={combatState} IsShopMode={cardUI.IsShopMode} IsSelfPlayCard={cardUI.IsSelfPlayCard} IsRangeCard={cardUI.IsRangeCard}");
                cardUI.DrawOrder = _drawOrderCounter++;
                // IndexOf로 동적 인덱스 조회 → 카드 제거 후에도 정확한 인덱스 사용
                CardData capturedCard = card;
                var capturedUI = cardUI;
                cardUI.OnCardPlayed         += (_)    => TryPlayCard(hand.IndexOf(capturedCard));
                // sticky 진입 가능 여부 — Shop: IsCardPlayableNow(골드), Combat: IsCardPlayableNow(사용 조건) + HasAnyValidTargetForCard(보드 타겟)
                // 사용 조건(타입 봉인 / Discard 손패 수 부족 / Hook 칸 등)을 만족하지 못하면 보드 타겟이 있어도 sticky 자체를 막는다.
                cardUI.StickyAllowedCheck = (ui) =>
                {
                    if (combatState == CombatState.Shop) return IsCardPlayableNow(capturedCard);
                    if (combatState == CombatState.Rest) return true;
                    if (combatState == CombatState.Treasure) return true;
                    // 보상 픽: Shop과 동일 흐름 — collapse-release로 선택하므로 보드 타겟 검사 불필요.
                    // Victory 상태에선 적이 모두 죽어 있어 Action/Move 카드의 HasAnyValidTargetForCard가 false라
                    // 이 검사가 있으면 보상 픽 자체가 차단된다.
                    if (_rewardPickActive) return IsCardPlayableNow(capturedCard);
                    if (!IsCardPlayableNow(capturedCard)) return false;
                    return HasAnyValidTargetForCard(capturedCard);
                };
                // range 카드: hover/선택 시 range 스프라이트 표시. non-range(Skill/Power)는 collapse 이벤트로만 표시
                cardUI.OnCardDown           += (_)    => {
                    int idx = hand.IndexOf(capturedCard);
                    pendingCardIndex = idx;
                    if (idx >= 0 && idx < hand.Count) pendingCard = hand[idx]; // sticky 카드 추적 → boardcell 클릭 차단용
                    // 방어: Shop에서는 어떤 카드든 Power 카드와 동일한 sticky/collapse 흐름을 따른다.
                    // CreateSingleCardObject 시점에 IsShopMode를 못 잡은 경우(상태 누락/캐시 복원 등)에 대비해
                    // SetLocked 호출 전에 IsShopMode를 강제 설정한다 (Attack/Move 카드가 raycast 차단되는 것을 방지).
                    // 맵 이동 카드는 구매가 아니라 "이동"이므로 IsShopMode 적용 제외 — Update의 단축 분기가 첫 셀 클릭을 가로채는 문제 차단.
                    if (combatState == CombatState.Shop && !IsMapMoveCard(capturedCard)) capturedUI.IsShopMode = true;
                    Debug.Log($"[OnCardDown] card={capturedCard.cardName} idx={idx} combatState={combatState} IsShopMode={capturedUI.IsShopMode} IsSelfPlayCard={capturedUI.IsSelfPlayCard}");
                    capturedUI.SetLocked(true);
                    SetCombatButtonsInteractable(false);
                    if (combatState == CombatState.Shop)
                    {
                        // Shop: card sticky 시 선택 카드 기록. cardCode 첫째자리(ClassDigit) 1~4 (실제 카드)인 경우만 RangeIndicator 활성화
                        _shopSelectedCard = capturedCard;
                        int classDigit = capturedCard != null ? capturedCard.ClassDigit : 0;
                        if (drawPileButton != null) drawPileButton.interactable = (classDigit >= 1 && classDigit <= 4);
                        // RangeIndicator(=drawPileButton)는 sticky 중에도 클릭 가능해야 하므로 raycast 복구.
                        SetButtonRaycast(drawPileButton, true);
                    }
                    if (_rewardPickActive && _rewardPickCards.Contains(capturedCard))
                    {
                        // 카드 보상 픽: shop과 동일하게 sticky 시 RangeInfo 대상 기록 + 실제 카드(ClassDigit 1~4)만 RangeIndicator 활성화
                        _rewardPickViewCard = capturedCard;
                        int classDigit = capturedCard != null ? capturedCard.ClassDigit : 0;
                        if (drawPileButton != null) drawPileButton.interactable = (classDigit >= 1 && classDigit <= 4);
                        SetButtonRaycast(drawPileButton, true);
                    }
                    // Range 카드(Enemy/Any/Ally)는 sticky 진입 즉시 EnterTargetingMode로 셀 상태를 TargetableEnemy/AreaSelectable로 셋해야
                    // 셀 hover 시 OnTargetingCellHoverEnter의 sprite swap(unactive→active) 조건이 충족된다.
                    // 보상 픽 모드에서는 셀 타게팅 없이 collapse 영역으로 드래그하여 카드를 선택하므로 EnterTargetingMode 호출 차단.
                    // 맵 이동 카드는 mapPlayerPos 기준으로 셀을 highlight해야 drag-drop이 동작 — EnterTargetingMode는 combat playerSpawnCell 기준이라 좌표가 어긋남.
                    if (IsMapMoveCard(capturedCard) &&
                        (combatState == CombatState.MapState
                         || combatState == CombatState.Shop
                         || combatState == CombatState.Rest
                         || combatState == CombatState.Treasure
                         || combatState == CombatState.Victory))
                    {
                        Debug.Log($"[OnCardDown→MapMove] {capturedCard.cardName} combatState={combatState} idx={idx} → EnterMapMoveTargeting");
                        EnterMapMoveTargeting(idx);
                    }
                    else if (combatState != CombatState.Shop && combatState != CombatState.Rest && combatState != CombatState.Treasure && !_rewardPickActive && !capturedUI.IsSelfPlayCard)
                    {
                        EnterTargetingMode(idx);
                    }
                };
                cardUI.OnCardHoverEnter     += (_)    =>
                {
                    // 맵 이동 카드: MapState/Shop/Rest 어디서든 플레이어 맵 좌표 기준으로 이동 sprite 표시.
                    // ShowCardRangePreview는 combat playerSpawnCell 기준이라 비전투 상태에서 사용 시 위치가 어긋난다.
                    if (IsMapMoveCard(capturedCard) &&
                        (combatState == CombatState.MapState
                         || combatState == CombatState.Shop
                         || combatState == CombatState.Rest
                         || combatState == CombatState.Treasure))
                    {
                        ShowMapMoveRangePreview(capturedCard);
                        return;
                    }
                    if (combatState == CombatState.Shop) return; // Shop: 일반 카드 hover 미리보기 차단
                    if (combatState == CombatState.Rest) return; // Rest: range sprite 표시 안 함
                    if (combatState == CombatState.Treasure) return; // Treasure: range sprite 표시 안 함
                    if (_rewardPickActive) return; // 카드 보상 픽: powersprite / range preview 표시 안 함
                    if (!_isChooseCardMode && (capturedUI.IsRangeCard || capturedUI.IsSelfPlayCard || capturedUI.IsAreaCard))
                        ShowCardRangePreview(capturedCard);
                };
                cardUI.OnCardHoverExit      += (_)    =>
                {
                    // 맵 이동 카드는 hover 종료 시 sprite 정리 (lock 상태가 아니면).
                    if (IsMapMoveCard(capturedCard))
                    {
                        if (!capturedUI.IsLocked) ClearCardRangePreview();
                        return;
                    }
                    if (combatState == CombatState.Shop || combatState == CombatState.Rest || combatState == CombatState.Treasure) return;
                    if (_rewardPickActive) return;
                    if (!capturedUI.IsLocked)
                        ClearCardRangePreview();
                };
                cardUI.OnCardCollapseEnter  += (_)    =>
                {
                    // === collapseY 진입 시 조건 확인: 통과 → CanPlayWhileSticky=true, 실패 → DeselectPendingCard ===
                    if (combatState == CombatState.Shop)
                    {
                        bool canPlay = IsCardPlayableNow(capturedCard); // shop은 IsCardPlayableNow가 골드 검사 포함
                        Debug.Log($"[CollapseEnter:Shop] {capturedCard.cardName} canPlay={canPlay}");
                        if (canPlay) capturedUI.CanPlayWhileSticky = true;
                        else         DeselectPendingCard();
                        return;
                    }
                    if (combatState == CombatState.Rest)
                    {
                        capturedUI.CanPlayWhileSticky = true; // Rest 카드는 항상 사용 가능
                        return;
                    }
                    if (combatState == CombatState.Treasure)
                    {
                        capturedUI.CanPlayWhileSticky = true; // Treasure 카드는 항상 사용 가능
                        return;
                    }
                    if (_rewardPickActive)
                    {
                        // 카드 보상 픽: 보드 타겟 검사 없이 collapse 영역 release로 선택 가능
                        capturedUI.CanPlayWhileSticky = true;
                        return;
                    }
                    // MapState: 맵 이동 카드 또는 _rewardCards에 포함된 보상 카드만 collapse-release로 발동.
                    // Shop/Rest/Treasure처럼 explicit 분기가 없으면 아래 !IsSelfPlay 분기로 빠져 CanPlayWhileSticky가
                    // 부여되지 않아 release해도 PlayCard가 호출되지 않는 버그(맵 이동 카드 드래그 무반응)가 발생.
                    if (combatState == CombatState.MapState)
                    {
                        bool isMapMove = IsMapMoveCard(capturedCard);
                        bool isReward  = _rewardCards != null && _rewardCards.Contains(capturedCard);
                        if ((isMapMove || isReward) && IsCardPlayableNow(capturedCard))
                            capturedUI.CanPlayWhileSticky = true;
                        else
                            DeselectPendingCard();
                        return;
                    }
                    // 이동/공격(non-SelfPlay) 카드: 사용 조건 미충족이거나 유효 타겟 없으면 sticky 해제
                    if (!capturedUI.IsSelfPlayCard)
                    {
                        if (!IsCardPlayableNow(capturedCard))
                        {
                            Debug.Log($"[CollapseEnter] {capturedCard.cardName} 사용 조건 미충족 — sticky 해제");
                            DeselectPendingCard();
                            return;
                        }
                        if (!HasAnyValidTargetForCard(capturedCard))
                        {
                            Debug.Log($"[CollapseEnter] {capturedCard.cardName} 유효 타겟 없음 — sticky 해제");
                            DeselectPendingCard();
                        }
                        return;
                    }
                    // SelfPlay (Power/Area) 카드: 사용 조건 + 유효 타겟 모두 통과해야 CanPlayWhileSticky 부여
                    if (IsCardPlayableNow(capturedCard) && HasAnyValidTargetForCard(capturedCard))
                        capturedUI.CanPlayWhileSticky = true;
                    else
                    {
                        DeselectPendingCard();
                        return;
                    }
                    if (hoverPreviewObjects.Count == 0) ShowCardRangePreview(capturedCard);
                    if (HasRangeOffsets(capturedCard))
                    {
                        Sprite pwr = capturedCard.CardTypeFromCode == CardType.Power ? playerPowerActive : null;
                        Sprite atk = capturedCard.CardTypeFromCode != CardType.Power ? playerAttackActive : null;
                        SetRangePreviewSpriteByName(pwr, atk);
                    }
                    else
                        SetRangePreviewSprite(capturedCard.CardTypeFromCode == CardType.Power ? playerPowerActive : playerAttackActive);
                };
                cardUI.OnCardCollapseExit   += (_)    =>
                {
                    // Shop/Rest: 한 번 collapse 영역 진입(=buy commit)했으면 release 위치 무관하게 구매되도록 CanPlayWhileSticky를 유지한다.
                    // 그렇지 않으면 사용자가 마우스를 위로 올렸다가 살짝 내려서 떼면 구매가 안 되는 strict한 UX가 됨.
                    if (combatState == CombatState.Shop || combatState == CombatState.Rest || combatState == CombatState.Treasure) return;
                    if (_rewardPickActive) return;
                    // collapseY 아래로 내려가면 mousedown/up PlayCard 권한 회수 (combat 전용)
                    capturedUI.CanPlayWhileSticky = false;
                    if (!capturedUI.IsSelfPlayCard) return;
                    if (HasRangeOffsets(capturedCard))
                    {
                        Sprite pwr = capturedCard.CardTypeFromCode == CardType.Power ? playerPowerUnactive : null;
                        Sprite atk = capturedCard.CardTypeFromCode != CardType.Power ? playerAttackUnactive : null;
                        SetRangePreviewSpriteByName(pwr, atk);
                    }
                    else
                        SetRangePreviewSprite(capturedCard.CardTypeFromCode == CardType.Power ? playerPowerUnactive : playerAttackUnactive);
                };
                cardUI.OnTargetingCancelled += (_)    => {
                    Debug.LogWarning($"[OnTargetingCancelled] card={capturedCard?.cardName} combatState={combatState} stack=\n{System.Environment.StackTrace}");
                    CancelTargeting();
                    if (combatState == CombatState.Shop || combatState == CombatState.Rest || combatState == CombatState.Treasure || _rewardPickActive)
                    {
                        Debug.LogWarning($"[pendingCard CLEAR @ OnTargetingCancelled Shop/Rest/Treasure/RewardPick branch] card={capturedCard?.cardName}");
                        pendingCard = null;
                        pendingCardIndex = -1;
                        _shopSelectedCard = null;
                        _rewardPickViewCard = null;
                        if (drawPileButton != null) drawPileButton.interactable = false;
                    }
                };
            }
            return cardObj;
        }

        public void RepositionHandCards()
        {
            int count = handCardObjects.Count;
            if (count == 0) return;

            // chooseContainer에 있는 카드는 제외하고 실제 hand에 보이는 카드만 수집
            var visibleCards = new List<CardUI>();
            for (int i = 0; i < count; i++)
            {
                if (handCardObjects[i] == null) continue;
                CardUI cardUI = handCardObjects[i].GetComponent<CardUI>();
                if (cardUI == null) continue;
                if (cardUI.IsInChooseContainer) continue;
                visibleCards.Add(cardUI);
            }

            int visibleCount = visibleCards.Count;

            float spacing = handCardSpacing;
            if (handMaxWidth > 0f && visibleCount > 1)
            {
                float fitSpacing = handMaxWidth / (visibleCount - 1);
                if (fitSpacing < spacing) spacing = fitSpacing;
            }

            SnapHandContainerToScreenBottom();
            CardUI.BoardCollapseScreenY = cardCollapseBoardScreenY;

            float cardHeight = GetHandCardHeight();
            float handBaseY  = CalculateHandBaseY(cardHeight);

            float totalWidth = visibleCount > 1 ? spacing * (visibleCount - 1) : 0f;
            float startX = -totalWidth / 2f;

            for (int i = 0; i < visibleCount; i++)
            {
                float xPos = visibleCount > 1 ? startX + i * spacing : 0f;

                visibleCards[i].SetHandPosition(new Vector3(xPos, handBaseY, 0f), Quaternion.identity);
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
            // 카드 사용 시점에 ExecuteCard()가 이미 ClearCardRangePreview()를 호출함.
            // 여기서 다시 호출하면 애니메이션 동안 사용자가 다른 카드에 hover 하여 새로 만든
            // preview sprite까지 지워져, drag로 공격 카드 사용 후 이동 카드 hover 시
            // rangesprite가 보이지 않는 버그가 발생.
        }

        /// <summary>카드 효과 중 endturn_ExhaustsMove(Custom)가 포함되어 있는지 검사.
        /// 휘발성(Ethereal) 키워드 없이 카드 효과 자체로 "턴 종료 시 소멸" 동작을 제공한다.</summary>
        private static bool HasEndTurnExhaustEffect(DeckRoguelike.Cards.CardData card)
        {
            if (card?.Effects == null) return false;
            foreach (var eff in card.Effects)
            {
                if (eff == null) continue;
                if (eff.effectType != DeckRoguelike.Cards.EffectType.Custom) continue;
                if (string.Equals(eff.customEffectId, "endturn_ExhaustsMove",
                        System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void DiscardHand()
        {
            // 903 영원의 손 — 턴이 끝나도 손패를 버리지 않고 그대로 유지
            if (GameManager.Instance != null && GameManager.Instance.HasRelic(903))
            {
                Debug.Log("[CombatController] 903 유물 — 손패 유지");
                return;
            }

            // 드로우 애니메이션 코루틴이 아직 진행 중이면 중단한다. 카드 데이터는 이미 DrawCards에서
            // hand에 모두 들어가 있으므로 아래 루프가 빠짐없이 버린다. 중단하지 않으면 hand.Clear()
            // 이후 코루틴이 남은 카드의 비주얼을 다시 생성해 적 턴 동안 손패에 카드가 남는다.
            if (_drawCardsCoroutine != null) { StopCoroutine(_drawCardsCoroutine); _drawCardsCoroutine = null; }

            // 데이터 즉시 처리 (Random_*Card 변환 카드는 원본으로 복원하여 더미로 보냄)
            // Ethereal 카드 또는 endturn_ExhaustsMove 효과 카드는 버려질 때 소멸 더미로
            //   — 70/970 유물의 매턴 이동 카드 등. (Ethereal 키워드 없이 효과 자체로 처리)
            int discardedCount = hand.Count;
            if (deckManager != null)
                foreach (var card in hand)
                {
                    var resolved = ResolveOriginalCard(card);
                    if (resolved != null && (resolved.Ethereal || HasEndTurnExhaustEffect(resolved)))
                        deckManager.ExhaustCard(resolved);
                    else
                        deckManager.AddToDiscardPile(resolved);
                }
            hand.Clear();

            // 303 유물 — 매 턴 버린 카드 수만큼 힘 증가
            if (discardedCount > 0)
                FireRelicHook((r, ctx) => r.OnHandDiscarded(ctx, discardedCount));

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
            // 진행 중인 드로우 코루틴 중단 — 정리 후 비주얼이 다시 생성되는 것을 막는다.
            if (_drawCardsCoroutine != null) { StopCoroutine(_drawCardsCoroutine); _drawCardsCoroutine = null; }

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
            // 카드 보상 픽 모드: 픽 카드와 60004 넘기기 카드만 사용 가능.
            if (_rewardPickActive)
            {
                if (card.cardCode == RewardSkipCardCode) return true;
                return _rewardPickCards.Contains(card);
            }
            // 맵 이동 카드는 전투 PlayerTurn에서는 사용 불가 (맵 외부에선 의미 없음)
            if (combatState == CombatState.PlayerTurn && IsMapMoveCard(card)) return false;
            if (combatState == CombatState.Shop && _shopCardPrices.TryGetValue(card, out int shopPrice))
            {
                if (GameManager.Instance == null || GameManager.Instance.Gold < shopPrice) return false;
                return true;
            }
            if (cardTypeRestrictions.TryGetValue(card.CardTypeFromCode, out int remaining) && remaining > 0) return false;

            // 순간이동 카드: 전투당 act수만큼만 사용 가능
            if (HasTeleportationEffect(card) && TeleportUsesRemaining <= 0)
                return false;

            // Discard 효과 카드: 자신 외에 버릴 카드가 손패에 최소 1장 있어야 사용 가능
            if (card.Effects != null && card.Effects.Any(e => e.effectType == EffectType.Discard) && hand.Count <= 1)
                return false;

            // Hook/Hook_Damage(고기 갈고리): 플레이어 정면 칸이 보드 내에 존재하고 비어있어야 사용 가능
            if (card.Effects != null && card.Effects.Any(e =>
                    !string.IsNullOrEmpty(e.customEffectId) &&
                    e.customEffectId.StartsWith("Hook", System.StringComparison.OrdinalIgnoreCase)))
            {
                int dirX = facingRight ? 1 : -1;
                var front = new Vector2Int(playerSpawnCell.x + dirX, playerSpawnCell.y);
                if (!IsInBoard(front) || !IsCellEmpty(front)) return false;
            }

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
            if (_isChooseCardMode) return;
            if (combatState != CombatState.PlayerTurn && combatState != CombatState.Victory && combatState != CombatState.MapState && combatState != CombatState.Rest && combatState != CombatState.Shop && combatState != CombatState.Treasure) return;
            if (handIndex < 0 || handIndex >= hand.Count) return;

            // 카드 보상 픽 모드: 보상 픽 카드는 즉시 "선택"으로 처리. 60004 넘기기 카드는 Reward_Skip 효과로 처리.
            if (_rewardPickActive)
            {
                CardData rpCard = hand[handIndex];
                if (rpCard != null && rpCard.cardCode != RewardSkipCardCode && _rewardPickCards.Contains(rpCard))
                {
                    HandleRewardPickCardChosen(handIndex);
                    return;
                }
                if (rpCard != null && rpCard.cardCode == RewardSkipCardCode)
                {
                    // 넘기기 카드: ExecuteCard 흐름으로 Reward_Skip 효과 실행 (MapState 차단 우회).
                    SetCombatButtonsInteractable(true);
                    ExecuteCard(handIndex, null);
                    return;
                }
                Debug.Log("[CombatController] 카드보상 모드: 보상 카드 또는 넘기기 카드만 사용 가능");
                return;
            }

            // 맵 이동 카드는 모든 비전투 상태(Shop/Rest/MapState/Victory)에서 동일 흐름.
            // Shop/Rest에서 사용하면 OnMerchantLeave/OnRestLeave 후 MapState 타겟팅 진입.
            if (combatState != CombatState.PlayerTurn && IsMapMoveCard(hand[handIndex]))
            {
                HandleMapMoveCardPlay(handIndex);
                return;
            }

            if (combatState == CombatState.Shop)
            {
                HandleShopCardPlay(handIndex);
                return;
            }

            if (combatState == CombatState.Rest)
            {
                HandleRestCardPlay(handIndex);
                return;
            }

            if (combatState == CombatState.MapState)
            {
                // 맵 이동 카드 외에는 보상 카드(_rewardCards)만 사용 가능. 보상 카드는 타게팅 없이 즉시 실행.
                CardData mapStateCard = hand[handIndex];
                if (_rewardCards.Contains(mapStateCard))
                {
                    if (!IsCardPlayableNow(mapStateCard))
                    {
                        Debug.Log($"[CombatController] 보상 카드 사용 불가: {mapStateCard.cardName}");
                        return;
                    }
                    SetCombatButtonsInteractable(true);
                    ExecuteCard(handIndex, null);
                    return;
                }
                Debug.Log("[CombatController] MapState에서는 맵 이동 카드 또는 보상 카드만 사용 가능");
                return;
            }

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

            // 카드 사용 시점에 range preview sprite를 즉시 정리 (효과 적용 전)
            ClearCardRangePreview();

            // 플레이된 카드 오브젝트 먼저 참조 (제거 전)
            GameObject playedObj = handIndex < handCardObjects.Count ? handCardObjects[handIndex] : null;

            int powerCountBefore = _activePowers.Count;
            _playingHandIndex = handIndex;
            ApplyCardEffects(card, selectedPos);
            _playingHandIndex = -1;
            FireRelicHook((r, ctx) => r.OnCardPlayed(ctx, card));
            // 파워 효과(MoveCard_Strength / PowerCard_Strength 등) 훅 발동
            // ApplyCardEffects에서 새로 등록된 파워는 이번 카드에 반응하지 않도록 기존 파워만 호출
            for (int i = powerCountBefore - 1; i >= 0; i--)
                _activePowers[i].OnCardPlayed(this, card);

            // 재사용 포션: 다음 카드를 2번 실행 (1회 소모)
            if (nextCardDoublePlay)
            {
                nextCardDoublePlay = false;
                _playingHandIndex = hand.IndexOf(card);
                ApplyCardEffects(card, selectedPos);
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

            // 보상/휴식 카드 목록에서 제거 (이미 사용됨)
            _rewardCards.Remove(card);

            // 재사용 포션(108): 방금 사용한 카드를 복사하고 복사본에 소멸을 부여해 손에 추가합니다 (1회 소모).
            // 사용한 카드가 이미 손에서 제거된 시점이라 손패 한도(10장)에 여유가 있습니다.
            if (nextCardCopyExhaust)
            {
                nextCardCopyExhaust = false;
                DeckRoguelike.UI.InGameUIController.Instance?.RemovePlayerEffect("card_copy_exhaust");
                var copy = card.Clone();
                copy.keywords |= DeckRoguelike.Cards.CardKeyword.Exhausts; // 복사본에 소멸 부여
                AddCardToHandFree(copy);
                Debug.Log($"[재사용 포션] '{card.cardName}' 복사본(소멸 부여)을 손에 추가");
            }

            // Random_*Card 변환 카드는 손패를 떠날 때 원본 Random 카드로 복원해서 더미로 보냄
            CardData pileCard = ResolveOriginalCard(card);

            bool isPower = card.CardTypeFromCode == DeckRoguelike.Cards.CardType.Power;
            // 재사용 포션(203): 다음 카드는 버려지지 않고 손으로 되돌아옵니다.
            // 파워 카드와 소멸 카드는 원래 손으로 돌아오지 않으므로 예외 적용 불가합니다.
            bool keepInHand = nextCardKeepInHand && !isPower && !card.Exhaust;
            if (keepInHand)
            {
                nextCardKeepInHand = false;
                DeckRoguelike.UI.InGameUIController.Instance?.RemovePlayerEffect("card_keep_in_hand");
                AddCardToHandFree(pileCard);
                Debug.Log($"[재사용 포션] {pileCard.cardName} 손으로 되돌아옴");
            }
            else if (isPower && !_noExhaust)
            {
                // 파워 카드: 소멸더미로 이동하지만 소멸 상호작용은 발동하지 않음
                deckManager?.ExhaustCard(pileCard);
            }
            else if (card.Exhaust && !_noExhaust)
            {
                deckManager?.ExhaustCard(pileCard);
                for (int i = powerCountBefore - 1; i >= 0; i--)
                    _activePowers[i].OnCardExhausted(this);
            }
            else if (HasEndTurnExhaustEffect(pileCard) && !_noExhaust)
            {
                // endturn_ExhaustsMove(이동 카드 21000/21001 등): 사용 시점에도 소멸.
                // 손패에 남으면 DiscardHand가, 사용 시엔 여기서 소멸 처리하여 더미에 누적되지 않도록 함.
                deckManager?.ExhaustCard(pileCard);
            }
            else
            {
                deckManager?.AddToDiscardPile(pileCard);
            }

            GameManager.Instance?.AddCardPlayed();
            _cardsPlayedThisTurn++; // 904 유물 카운터

            // 나머지 카드 부드럽게 재배치
            RepositionHandCards();
            UpdatePileCounters();

            // 플레이된 카드 버림더미로 날리기 (CheckVictory 전에 코루틴 시작해야 함)
            // CheckVictory가 SetCombatHudActive(false)를 호출하면 GameObject가 비활성화돼
            // StartCoroutine이 실패하기 때문
            Debug.Log($"[ExecuteCard] cleanup card={card.cardName} playedObj={(playedObj != null ? playedObj.name : "null")} discardPileButton={(discardPileButton != null)}");
            if (playedObj != null)
            {
                CardUI playedUI = playedObj.GetComponent<CardUI>();
                if (playedUI != null)
                {
                    playedUI.MarkAsPlayed();
                    if (discardPileButton != null)
                    {
                        Debug.Log($"[ExecuteCard] StartCoroutine DiscardAndDestroy for {card.cardName} obj.activeInHierarchy={playedObj.activeInHierarchy}");
                        StartCoroutine(DiscardAndDestroy(playedUI, discardPileButton.transform.position));
                    }
                    else
                        Destroy(playedObj);
                }
                else
                    Destroy(playedObj);
            }
            else
            {
                Debug.LogWarning($"[ExecuteCard] playedObj null — card={card.cardName} handIndex={handIndex} handCardObjects.Count={handCardObjects.Count}");
            }

            CheckVictory();

            // Exhausts/Discard 효과로 인한 선택 패널 오픈 (카드가 손패에서 제거된 이후)
            if (_hasPendingChoosePanel && combatState == CombatState.PlayerTurn)
            {
                _hasPendingChoosePanel = false;
                OpenChooseCardPanel(_pendingChoosePanelCount, _pendingChoosePanelExhaust);
            }

            // Reward_Card / Reward_Skip 효과는 손패가 정리된 뒤 픽 모드를 시작/종료한다.
            if (_rewardPickEndPending)
            {
                _rewardPickEndPending = false;
                EndRewardPickFlow();
            }
            else if (_rewardPickPending)
            {
                _rewardPickPending = false;
                StartRewardPickFlow();
            }
            else if (_relicPickPending)
            {
                // 60010 유물선택: 손패 정리 후 보스 유물 3장 픽 모드 진입.
                _relicPickPending = false;
                StartRelicPickFlow();
            }

            // 60011 다음 스테이지: 손패가 정리된 뒤 Act +1 및 맵 재생성.
            if (_nextStagePending)
            {
                _nextStagePending = false;
                AdvanceToNextStage();
            }

            // 60008 전투재시작 / 60009 메인메뉴: 손패 정리 후 처리.
            if (_restartCombatPending)
            {
                _restartCombatPending = false;
                _returnMainMenuPending = false;
                _pendingDefeatReward = false;
                _rewardCards.Clear();
                DeckRoguelike.UI.InGameUIController.Instance?.RestartCombat();
            }
            else if (_returnMainMenuPending)
            {
                _returnMainMenuPending = false;
                _pendingDefeatReward = false;
                _rewardCards.Clear();
                DeckRoguelike.UI.InGameUIController.Instance?.ReturnToMainMenu();
            }

            // 카드 사용 후 상태에 맞춰 pile 버튼 위치 갱신.
            // 보상 카드를 마지막으로 소진해 MapState로 돌아가는 등의 경우 자동으로 아래로 내려간다.
            RefreshDeckPileButtonVisibility();
        }

        /// <summary>
        /// 카드의 모든 효과를 순서대로 적용.
        /// 각 효과는 자신의 targeting/rangeOffsets로 독립적으로 대상을 결정한다.
        /// selectedPos는 Enemy/Any/Ally 효과에서 플레이어가 선택한 좌표.
        /// </summary>
        private void ApplyCardEffects(CardData card, Vector2Int? selectedPos)
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
                        // 201 선제의 일격: 전투마다 처음으로 데미지를 주면 보너스 피해
                        if (!_firstAttackUsedThisCombat
                            && _firstActionBonusDamage > 0)
                        {
                            baseDmg += _firstActionBonusDamage;
                            _firstAttackUsedThisCombat = true;
                            Debug.Log($"[CombatController] 201 유물 — 첫 데미지 +{_firstActionBonusDamage} 피해");
                        }
                        // 이번 턴 데미지 배율(outgoingDamageMultiplier, 23302 등)은 DamageEnemy에서 일괄 적용하므로
                        // 여기서는 곱하지 않는다 (다회타격/커스텀 데미지 카드와 동일하게 한 곳에서만 적용해 이중 적용 방지).
                        float mult  = 1f;
                        // 분노의 포션: 다음 액션 카드 3배 (1회 소모)
                        if (card.CardTypeFromCode == DeckRoguelike.Cards.CardType.Action
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

                    case EffectType.Heal:
                        GameManager.Instance?.Heal(effect.value);
                        break;

                    case EffectType.Move:
                        if (selectedPos.HasValue)
                            MovePlayer(selectedPos.Value);
                        break;

                    case EffectType.Custom:
                        if (!string.IsNullOrEmpty(effect.customEffectId)
                            && effect.customEffectId.StartsWith("summon_", System.StringComparison.OrdinalIgnoreCase))
                        {
                            if (selectedPos.HasValue
                                && int.TryParse(effect.customEffectId.Substring("summon_".Length), out int allyCode))
                            {
                                var allyData = AllyRegistry.GetAlly(allyCode);
                                if (allyData != null) SummonAlly(allyData, selectedPos.Value);
                            }
                            break;
                        }
                        if (!string.IsNullOrEmpty(effect.customEffectId)
                            && effect.customEffectId.StartsWith("trap_", System.StringComparison.OrdinalIgnoreCase))
                        {
                            if (selectedPos.HasValue
                                && int.TryParse(effect.customEffectId.Substring("trap_".Length), out int hazardCode))
                            {
                                var hazardData = HazardRegistry.GetHazard(hazardCode);
                                if (hazardData != null) EmplaceHazard(hazardData, selectedPos.Value, hazardData.value);
                            }
                            break;
                        }
                        CardEffectRegistry.Execute(effect.customEffectId, new CardEffectContext
                        {
                            Board      = this,
                            Value       = effect.value,
                            ValueRaw    = effect.valueRaw,
                            SelectedPos = selectedPos,
                            XValue      = 0,
                            Effect      = effect,
                            Card        = card,
                        });
                        break;

                    case EffectType.Exhausts:
                        _hasPendingChoosePanel     = true;
                        _pendingChoosePanelExhaust = true;
                        _pendingChoosePanelCount   = effect.value > 0 ? effect.value : 1;
                        Debug.Log($"[CombatController] ChoosePanel(Exhaust) 예약: 최대 {_pendingChoosePanelCount}장");
                        break;

                    case EffectType.Discard:
                        _hasPendingChoosePanel     = true;
                        _pendingChoosePanelExhaust = false;
                        _pendingChoosePanelCount   = effect.value > 0 ? effect.value : 1;
                        Debug.Log($"[CombatController] ChoosePanel(Discard) 예약: 최대 {_pendingChoosePanelCount}장");
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
            Debug.Log($"[MovePlayer] 호출 newPos={newPos} from={playerSpawnCell} inBoard={IsInBoard(newPos)} gridNull={grid == null}");
            if (!IsInBoard(newPos)) { Debug.LogWarning($"[MovePlayer] {newPos} 보드 밖 — 이동 불가"); return; }
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

            // 함정 발동 — 플레이어가 함정 위에 진입한 경우
            TryTriggerHazard(newPos, HazardTriggerSource.Player);

            // 플레이어 이동 → 적의 공격 사거리·이동 좌표를 다시 계산하고 미리보기 sprite 갱신
            RePlanAllEnemyTurns();
        }

        private void SetFacingRight(bool right)
        {
            if (facingRight == right) return;
            facingRight = right;
            if (playerObject != null)
            {
                // 프리팹 전체가 아닌 UnitUI의 unitSpriteImage만 좌우반전한다.
                var ui = playerObject.GetComponent<UnitUI>();
                if (ui != null) ui.SetFacingRight(right);
            }
        }

        /// <summary>
        /// 적 유닛을 targetPos로 이동합니다. 이동에 성공하면 true, 실패하면 false를 반환합니다.
        /// 대상 셀에 플레이어나 다른 적이 있으면 이동 불가합니다.
        /// forced=false(기본)는 적의 자발적 이동 — 속박(Bondage) 상태면 차단됩니다.
        /// 밀침·끌어당김·점프대 등 플레이어/환경에 의한 강제 이동은 forced=true로 호출해 속박을 무시합니다.
        /// </summary>
        public bool TryMoveEnemy(EnemyInstance enemy, Vector2Int targetPos, bool forced = false)
        {
            if (enemy == null) return false;
            if (!forced && enemy.IsBound) return false; // 속박: 자발적 이동 차단 (공격은 별도 경로로 진행)
            if (!IsInBoard(targetPos)) return false;

            // 멀티셀이면 footprint 전체가 보드 안 + 비점유(자기 자신 제외)인지 검사
            Vector2Int size = enemy.Size;
            if (!IsFootprintFreeForEnemy(targetPos, size, ignoreEnemy: enemy)) return false;

            // 기존 footprint 셀들 해제
            ClearEnemyFootprintCells(enemy);

            // 수평 이동 시 방향 갱신
            if (targetPos.x > enemy.GridPos.x) SetEnemyFacingRight(enemy, true);
            else if (targetPos.x < enemy.GridPos.x) SetEnemyFacingRight(enemy, false);

            // 새 footprint 점유
            enemy.GridPos = targetPos;
            SetEnemyFootprintCells(enemy, targetPos, size);

            if (enemy.GameObject != null)
            {
                RectTransform rt = enemy.GameObject.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = GetEnemyVisualPosition(targetPos, size) + playerCellOffset;
                enemy.GameObject.transform.SetAsLastSibling();
            }

            // 적이 이동(밀림/끌림/점프대 등)했으니 보드 상태가 바뀐 것 — 플레이어 턴 중이면 모든 적의
            // 행동을 재계산한다. (적 턴 실행 중에는 RePlanAllEnemyTurns가 no-op이라 Phase 2 이동에는 영향 없음)
            RePlanAllEnemyTurns();

            // 함정(설치/Trap)은 적 진입 즉시 발동하지 않는다. 적의 자발적 이동이든 밀림/끌림 등 강제 이동이든,
            // 모든 적의 공격·이동이 끝난 뒤 적 턴 종료 시점(ProcessTrapEndOfEnemyTurn)에 트랩 위에 서 있는
            // 적에게 일괄 발동한다 — 발동 타이밍을 단일화해 플레이어가 예측하기 쉽게 한다.

            return true;
        }

        /// <summary>
        /// SummonAlly 효과: AllyData 기반으로 지정 위치에 아군 유닛 소환.
        /// 프리팹은 AllyData.allyPrefab을 사용하며, 행동 알고리즘은 behaviorId로 등록된 함수가 처리.
        /// 이미 유닛이 있는 셀이면 무시.
        /// </summary>
        /// <summary>외부 호출용 래퍼: allyCode로 AllyData를 조회해 지정 위치에 소환합니다.</summary>
        public void SummonAllyAt(int allyCode, Vector2Int pos)
        {
            var data = DeckRoguelike.Core.AllyRegistry.GetAlly(allyCode);
            if (data != null) SummonAlly(data, pos);
        }

        private void SummonAlly(AllyData data, Vector2Int pos)
        {
            if (!IsInBoard(pos)) return;

            // 멀티셀이면 footprint 전체가 보드 안 + 플레이어·적·다른 아군이 없어야 한다.
            Vector2Int size = (data.gridSize.x > 0 && data.gridSize.y > 0) ? data.gridSize : Vector2Int.one;
            if (!IsFootprintFreeForAlly(pos, size)) return;

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
                rt.anchoredPosition = GetEnemyVisualPosition(pos, size) + playerCellOffset;
            allyObj.transform.SetAsLastSibling();

            // 아군 스프라이트: allySprite 필드 우선, 없으면 Resources/AllySprites/{allyCode} 로드
            var allySR = allyObj.GetComponentInChildren<SpriteRenderer>();
            if (allySR != null)
            {
                Sprite allySprite = data.allySprite;
                if (allySprite == null && data.allyCode > 0)
                    allySprite = Addressables.LoadAssetAsync<Sprite>($"Sprites/Ingame/AllySprites/{data.allyCode}").WaitForCompletion();
                if (allySprite != null)
                    allySR.sprite = allySprite;
            }

            // 290 유물 — 소환수의 체력 +5
            int allyMaxHp = data.maxHP;
            if (GameManager.Instance != null && GameManager.Instance.HasRelic(290))
                allyMaxHp += 5;

            var instance = new AllyInstance
            {
                Name           = data.allyName,
                MaxHP          = allyMaxHp,
                CurrentHP      = allyMaxHp,
                Damage         = data.baseDamage,
                TurnsRemaining = data.durationTurns,
                GridPos        = pos,
                GameObject     = allyObj,
                Data           = data,
            };

            instance.UI = allyObj.GetComponent<UnitUI>();
            Sprite allyUISpr = data.allySprite;
            if (allyUISpr == null && data.allyCode > 0)
                allyUISpr = Addressables.LoadAssetAsync<Sprite>($"Sprites/Ingame/AllySprites/{data.allyCode}").WaitForCompletion();
            instance.UI?.Initialize(allyMaxHp, allyUISpr);
            // 멀티셀이면 스프라이트·HP바를 footprint 크기에 맞춰 확대/정렬 (1x1은 no-op)
            instance.UI?.ApplyUnitSize(size, cellHeight);
            RefreshAllyActionIcons(instance);   // 스폰 직후 기본 행동 아이콘 표시

            instance.Behavior = AllyBehaviorRegistry.Create(data.allyCode);

            allies.Add(instance);
            SetAllyFootprintCells(instance, pos, size);

            // 아군은 플레이어와 같은 방향을 바라봅니다.
            instance.FacingRight = facingRight;
            instance.UI?.SetFacingRight(facingRight);

            Debug.Log($"[CombatController] {data.allyName} 소환! 위치: {pos}");

            // 소환수가 설치(Trap) 위에 놓이면 즉시 발동 (생성/Zone은 적 턴 종료시 처리되므로 여기선 트랩만).
            TryTriggerHazard(pos, HazardTriggerSource.Ally, movingAlly: instance);

            // 새 아군이 등장해 적의 타겟 후보가 늘었으니 모든 적 행동을 재계산 (플레이어 턴 중이면 적용).
            RePlanAllEnemyTurns();
        }

        public void DamageEnemy(EnemyInstance enemy, int amount)
        {
            DamageEnemyFromPos(enemy, amount, playerSpawnCell);
        }

        /// <summary>지정 위치에서 적을 공격합니다. 적의 바라보는 방향의 반대편에서 공격 시 2배 데미지.</summary>
        public void DamageEnemyFromPos(EnemyInstance enemy, int amount, Vector2Int attackerPos)
        {
            if (enemy == null) return;

            // 23302(최후의 공격) 등 이번 턴 데미지 배율 — 다회타격/커스텀 데미지를 포함한 모든 직접 공격에 일괄 적용.
            // EffectType.Damage 경로는 ApplyCardEffects에서 더 이상 배율을 곱하지 않으므로 여기서만 한 번 적용된다.
            // 후속/전이 데미지(_suppressOnEnemyDamaged: 313 초과피해 전이, 소매치기 후속 등)는 이미 산출된 값이므로 제외.
            if (!_suppressOnEnemyDamaged && outgoingDamageMultiplier != 1f)
                amount = Mathf.RoundToInt(amount * outgoingDamageMultiplier);

            // 뒷면 공격(반대 방향) 보너스: 적 facingRight=true → 등은 왼쪽, attacker.x < enemy.x 시 2배
            if (IsAttackFromBehind(enemy.GridPos, enemy.FacingRight, attackerPos))
            {
                amount *= 2;
                Debug.Log($"[BackAttack] {enemy.Name} 뒷면 공격! 데미지 2배 → {amount}");
            }

            // 311/312/314 유물 — 방향별 2배 피해. 동일 위치는 제외, 가장 우선 한 번만 2배 적용.
            var gm = GameManager.Instance;
            if (gm != null)
            {
                bool fromAbove  = attackerPos.y > enemy.GridPos.y;
                bool fromBelow  = attackerPos.y < enemy.GridPos.y;
                bool fromBehind = IsAttackFromBehind(enemy.GridPos, enemy.FacingRight, attackerPos);

                if (gm.HasRelic(311) && fromAbove)
                {
                    amount *= 2;
                    Debug.Log($"[Relic311] 위에서 공격 — {enemy.Name} 데미지 2배 → {amount}");
                }
                else if (gm.HasRelic(312) && fromBelow)
                {
                    amount *= 2;
                    Debug.Log($"[Relic312] 아래에서 공격 — {enemy.Name} 데미지 2배 → {amount}");
                }
                else if (gm.HasRelic(314) && fromBehind)
                {
                    amount *= 2;
                    Debug.Log($"[Relic314] 뒤에서 공격 — {enemy.Name} 데미지 2배 → {amount}");
                }
            }

            // 빙결 상태인 유닛은 공격당할 때 3배 피해
            if (enemy.StatusEffects.Exists(s => s.Type == StatusEffectType.Freeze && s.Stacks > 0))
            {
                amount *= 3;
                Debug.Log($"[Freeze] {enemy.Name} 빙결 상태 — 피해 3배 → {amount}");
            }

            // 방어도(Block) 먼저 흡수
            bool armorWasUp = enemy.Block > 0;
            if (enemy.Block > 0)
            {
                int absorbed = Mathf.Min(enemy.Block, amount);
                enemy.Block -= absorbed;
                amount -= absorbed;
                enemy.UI?.UpdateShield(enemy.Block, enemy.CurrentHP, enemy.MaxHP);
            }
            // 205 유물 등: 방어도가 막 0으로 내려간 경우 훅 발동
            if (armorWasUp && enemy.Block <= 0)
                FireRelicHook((r, ctx) => r.OnEnemyArmorBroken(ctx, enemy));

            if (amount <= 0)
            {
                Debug.Log($"[CombatController] {enemy.Name}의 방어도가 {(enemy.Block == 0 ? "모두 " : "")}흡수했습니다.");
                return;
            }

            // 313 유물 — 초과 피해가 가장 가까운 다른 적에게 전이됩니다
            int overkill = 0;
            if (gm != null && gm.HasRelic(313) && amount > enemy.CurrentHP)
                overkill = amount - enemy.CurrentHP;

            enemy.CurrentHP -= amount;
            GameManager.Instance?.AddDamageDealt(amount);
            PlaySound(attackSound);
            Debug.Log($"[CombatController] {enemy.Name}에게 {amount} 데미지!");

            enemy.UI?.UpdateHP(enemy.CurrentHP, enemy.MaxHP);

            // 적 행동 훅 — HP 임계 즉시 반응(예: 대왕 슬라임 자폭) 등에 사용.
            // 행동이 RemoveEnemyFromCombat을 호출해도 아래 사망 처리 블록은 안전하게(idempotent) 통과한다.
            enemy.Behavior?.OnDamaged(enemy, this, amount);

            // 적이 피해를 입을 때마다 파워 훅 발동 (후속 데미지 자체는 재진입 방지)
            if (!_suppressOnEnemyDamaged)
            {
                foreach (var p in _activePowers) p.OnEnemyDamaged(this, enemy, amount);
                FireRelicHook((r, ctx) => r.OnEnemyDamaged(ctx, enemy, amount));
            }

            // 313 유물 — 가장 가까운 살아있는 적에게 전이
            if (overkill > 0 && !_suppressOnEnemyDamaged)
            {
                EnemyInstance closest = null;
                int bestDist = int.MaxValue;
                foreach (var e in enemies)
                {
                    if (e == null || e == enemy || e.CurrentHP <= 0) continue;
                    int d = Mathf.Abs(e.GridPos.x - enemy.GridPos.x) + Mathf.Abs(e.GridPos.y - enemy.GridPos.y);
                    if (d < bestDist) { bestDist = d; closest = e; }
                }
                if (closest != null)
                {
                    _suppressOnEnemyDamaged = true;
                    try { DamageEnemyFromPos(closest, overkill, attackerPos); }
                    finally { _suppressOnEnemyDamaged = false; }
                    Debug.Log($"[Relic313] {enemy.Name}의 초과 피해 {overkill} → {closest.Name}");
                }
            }

            if (enemy.CurrentHP <= 0)
            {
                // 셀 비우기 (멀티셀이면 footprint 전체)
                ClearEnemyFootprintCells(enemy);

                if (enemy.GameObject != null)
                    Destroy(enemy.GameObject);

                Debug.Log($"[CombatController] {enemy.Name} 사망!");
                enemy.Behavior?.OnDeath(enemy, this);
                FireRelicHook((r, ctx) => r.OnEnemyKilled(ctx, enemy));
                foreach (var p in _activePowers) p.OnEnemyKilled(this);
                CheckVictory();

                // 적이 죽어 셀이 비었으니 다른 적들의 BFS 경로·타겟이 달라진다 — 플레이어 턴 중이면
                // 모든 적을 재계산. (잔류 미리보기 스프라이트 정리도 RePlanAllEnemyTurns가 함께 처리)
                RePlanAllEnemyTurns();
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
                    Vector2Int offsetVec = offset.ToVector2Int();
                    Vector2Int pos = firstEffect.useAbsoluteCoords
                        ? offsetVec
                        : playerSpawnCell + offsetVec;
                    if (pos == cell.GridPos)
                    {
                        // 체스 piece 등 직선/대각선 Move 카드: 적이 길을 막으면 드래그-드롭도 실행 안 함
                        if (IsMoveLineBlockedByEnemy(card, firstEffect, offsetVec)) return;
                        inRange = true; break;
                    }
                }
                if (!inRange) return;
            }

            // Enemy: 적 있어야 함 / Ally: 자신/아군 셀이어야 함
            if (pt == TargetType.Enemy &&
                (cell.OccupyingEnemy == null || cell.OccupyingEnemy.CurrentHP <= 0)) return;
            if (pt == TargetType.Ally &&
                !cell.IsPlayerHere && cell.OccupyingAlly == null) return;
            if (pt == TargetType.Any)
            {
                bool isPushEnmemy = !string.IsNullOrEmpty(firstEffect.customEffectId)
                    && firstEffect.customEffectId.Equals("pushEnmemy", System.StringComparison.OrdinalIgnoreCase);
                // EffectType.Move 외에 endturn_ExhaustsMove(21000/21001 등) 같은 커스텀 이동 효과도 동일하게 취급.
                bool hasMoveEffect = card.Effects.Any(e =>
                    e.effectType == EffectType.Move ||
                    (e.effectType == EffectType.Custom
                        && !string.IsNullOrEmpty(e.customEffectId)
                        && e.customEffectId.Equals("endturn_ExhaustsMove", System.StringComparison.OrdinalIgnoreCase)));
                if (cell.OccupyingEnemy != null && !isPushEnmemy && hasMoveEffect) return;
                if (cell.OccupyingAlly  != null) return;
                if (cell.IsPlayerHere && hasMoveEffect) return;
            }

            // 커스텀 효과별 추가 유효성 (pushEnmemy: 적 뒤 공간 필요 / Movediagonal_Damage: 빈 칸)
            if (!IsValidCustomEffectTarget(card, cell.GridPos)) return;

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
                            if (e.CurrentHP > 0 && IsInBoard(e.GridPos)
                                && IsValidCustomEffectTarget(card, e.GridPos))
                                grid[e.GridPos.x, e.GridPos.y].SetState(CellState.TargetableEnemy);
                        break;
                    case TargetType.Any:
                        bool hlMoveCard = card.Effects.Any(e => e.effectType == EffectType.Move);
                        for (int col = 0; col < boardCols; col++)
                            for (int row = 0; row < boardRows; row++)
                            {
                                var anyPos = new Vector2Int(col, row);
                                if (hlMoveCard && !IsCellEmpty(anyPos)) continue;
                                if (!hlMoveCard && grid[col, row].OccupyingAlly != null) continue;
                                // 생성/소환은 위해가 설치된 셀을 선택 대상에서 제외 (설치는 trap_ → 제한 없음)
                                if (!IsValidCustomEffectTarget(card, anyPos)) continue;
                                if (grid[col, row].OccupyingEnemy != null && grid[col, row].OccupyingEnemy.CurrentHP > 0)
                                    grid[col, row].SetState(CellState.TargetableEnemy);
                                else
                                    grid[col, row].SetState(CellState.AreaSelectable);
                            }
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

            bool isPushEnmemy = !string.IsNullOrEmpty(firstEffect.customEffectId)
                && firstEffect.customEffectId.Equals("pushEnmemy", System.StringComparison.OrdinalIgnoreCase);

            foreach (var offset in firstEffect.rangeOffsets)
            {
                Vector2Int offsetVec = offset.ToVector2Int();
                Vector2Int pos = firstEffect.useAbsoluteCoords
                    ? offsetVec
                    : playerSpawnCell + offsetVec;
                if (!IsInBoard(pos)) continue;
                // 커스텀 효과별 사전 필터 (예: pushEnmemy는 적 뒤가 막혀 있으면 선택 불가)
                if (!IsValidCustomEffectTarget(card, pos)) continue;
                // 체스 piece 등 직선/대각선 Move 카드: 적이 길을 막으면 선택 불가
                if (IsMoveLineBlockedByEnemy(card, firstEffect, offsetVec)) continue;

                CombatBoardCell cell = grid[pos.x, pos.y];
                CellState state = GetHighlightState(cell, firstEffect.targeting, card.CardTypeFromCode);
                // 돌진(pushEnmemy): 적이 있는 셀도 클릭 가능해야 한다 (밀고 그 자리로 이동)
                if (isPushEnmemy && cell.OccupyingEnemy != null && cell.OccupyingEnemy.CurrentHP > 0)
                    state = CellState.TargetableEnemy;
                cell.SetState(state);
            }
        }

        /// <summary>
        /// 카드 hover 시 effects[0] 범위에 sprite 오브젝트를 생성해 미리보기.
        /// OnCardHoverExit에 연결하지 않으므로 커서가 카드 밖으로 나가도 유지된다.
        /// </summary>
        private void ShowCardRangePreview(CardData card)
        {
            // pendingCard가 다른 카드라면 사거리 미리보기를 띄우지 않는다.
            // 단, 같은 card라면 OnCardDown 직후의 OnCardHoverEnter에서 호출되는 케이스로,
            // 이전 카드 sticky에서 새 카드로 전환된 직후의 첫 표시도 통과시켜야 sprite가 갱신된다.
            if (pendingCard != null && pendingCard != card) return;
            if (combatBoard == null || card.Effects == null || card.Effects.Count == 0) return;

            // 이미 같은 카드의 preview가 그려져 있으면 destroy/recreate 없이 그대로 재사용 — 깜빡임 방지.
            // ClearCardRangePreview 가 sprite를 hide만 했다면 다시 활성화한다.
            if (_currentRangePreviewCard == card && hoverPreviewObjects.Count > 0)
            {
                foreach (var obj in hoverPreviewObjects)
                    if (obj != null && !obj.activeSelf) obj.SetActive(true);
                return;
            }

            Debug.Log($"[RangePreview] '{card.CardName}' CardType={card.CardTypeFromCode} TypeDigit={card.TypeDigit}");
            var firstEffectForSprite = card.Effects[0];
            // 사거리 없이 Self 타겟으로 즉시 발동하는 카드는 CardType과 무관하게 파워 스프라이트로 표시
            //   — 소매치기(Gold), 채력 회복(Hp) 등 Action 분류이지만 사용 방식은 파워 카드와 동일.
            bool selfPlayLikePower = firstEffectForSprite.targeting == TargetType.Self
                && (firstEffectForSprite.rangeOffsets == null || firstEffectForSprite.rangeOffsets.Length == 0);
            Sprite previewSprite = firstEffectForSprite.targeting == TargetType.Random
                ? playerAttackUnactive
                : selfPlayLikePower
                    ? playerPowerUnactive
                    : card.CardTypeFromCode switch
                    {
                        CardType.Action => playerAttackUnactive,
                        CardType.Move   => playerMoveUnactive,
                        CardType.Power  => playerPowerUnactive,
                        _               => null,
                    };
            if (previewSprite == null) return;

            // 다른 카드의 preview면 destroy 후 재생성
            foreach (var old in hoverPreviewObjects)
                if (old != null) Destroy(old);
            hoverPreviewObjects.Clear();
            _currentRangePreviewCard = card;

            var firstEffect = card.Effects[0];
            bool hasRange = firstEffect.rangeOffsets != null && firstEffect.rangeOffsets.Length > 0;

            // 무작위 적 공격(Random_Damage 등)은 targeting/rangeOffsets 미지정이라도 살아있는 적 위치에 표시
            bool isRandomDamage = !string.IsNullOrEmpty(firstEffect.customEffectId)
                && firstEffect.customEffectId.Equals("Random_Damage", System.StringComparison.OrdinalIgnoreCase);
            if (isRandomDamage)
            {
                foreach (var e in enemies)
                    if (e.CurrentHP > 0 && IsInBoard(e.GridPos))
                        AddPreviewObject(e.GridPos, previewSprite);
                return;
            }

            if (hasRange)
            {
                bool isSelfPlayWithRange = (card.CardTypeFromCode == CardType.Power || firstEffect.targeting == TargetType.All)
                    && firstEffect.rangeOffsets != null && firstEffect.rangeOffsets.Length > 0;
                if (isSelfPlayWithRange)
                {
                    // power+range만 player 셀에 power 스프라이트 표시 (휩쓸기는 player 셀 스프라이트 없음)
                    if (card.CardTypeFromCode == CardType.Power)
                    {
                        AddPreviewObject(playerSpawnCell, playerPowerUnactive);
                        hoverPreviewObjects[hoverPreviewObjects.Count - 1].name = "RangePreview_Power";
                    }
                    foreach (var offset in firstEffect.rangeOffsets)
                    {
                        Vector2Int pos = firstEffect.useAbsoluteCoords
                            ? offset.ToVector2Int()
                            : playerSpawnCell + offset.ToVector2Int();
                        if (!IsInBoard(pos)) continue;
                        if (grid != null && grid[pos.x, pos.y].IsPlayerHere) continue;
                        AddPreviewObject(pos, playerAttackUnactive);
                        hoverPreviewObjects[hoverPreviewObjects.Count - 1].name = "RangePreview_Attack";
                    }
                }
                else
                {
                    // 일반 range 카드 (돌진 등): 기존 로직 유지
                    bool isMoveCard = card.CardTypeFromCode == CardType.Move;
                    string fxId = firstEffect.customEffectId ?? string.Empty;
                    bool isPushEnmemy = fxId.Equals("pushEnmemy", System.StringComparison.OrdinalIgnoreCase);
                    foreach (var offset in firstEffect.rangeOffsets)
                    {
                        Vector2Int offsetVec = offset.ToVector2Int();
                        Vector2Int pos = firstEffect.useAbsoluteCoords
                            ? offsetVec
                            : playerSpawnCell + offsetVec;
                        if (!IsInBoard(pos)) continue;
                        if (grid == null) continue;
                        if (grid[pos.x, pos.y].IsPlayerHere && isMoveCard) continue;
                        if (!isPushEnmemy && isMoveCard && grid[pos.x, pos.y].OccupyingEnemy != null) continue;
                        // hover 미리보기는 카드의 사거리 자체를 보여주는 정보 표시이므로 사용 가능 여부(IsValidCustomEffectTarget)로
                        // 필터링하지 않는다. 실제 사용 가능 셀 제한은 HighlightRange/클릭 검증(IsValidCustomEffectTarget)에서 처리.
                        // 체스 piece 등 직선/대각선 Move 카드: 적이 길을 막으면 미리보기에서도 제외
                        if (IsMoveLineBlockedByEnemy(card, firstEffect, offsetVec)) continue;
                        AddPreviewObject(pos, previewSprite);
                    }
                }
            }
            else
            {
                // 무제한 사거리: 적용 가능한 대상에만 스프라이트 표시
                switch (firstEffect.targeting)
                {
                    case TargetType.Enemy:
                        // 사거리 미지정 + useAbsoluteCoords=false → player 셀 제외 모든 좌표 공격 가능
                        if (!firstEffect.useAbsoluteCoords)
                        {
                            // hover 미리보기는 사거리 정보 표시 — 사용 가능 여부(IsValidCustomEffectTarget)로 필터하지 않는다.
                            for (int col = 0; col < boardCols; col++)
                                for (int row = 0; row < boardRows; row++)
                                {
                                    if (grid != null && grid[col, row].IsPlayerHere) continue;
                                    var ePos = new Vector2Int(col, row);
                                    AddPreviewObject(ePos, previewSprite);
                                }
                        }
                        else
                        {
                            for (int col = 0; col < boardCols; col++)
                                for (int row = 0; row < boardRows; row++)
                                {
                                    var ePos = new Vector2Int(col, row);
                                    AddPreviewObject(ePos, previewSprite);
                                }
                        }
                        break;
                    case TargetType.All:
                        // All: 사거리 미지정이면 보드 전체 셀에 attack sprite 표시
                        // useAbsoluteCoords=false면 player 셀 제외 ("모든 좌표 = 내 위치 제외 전부")
                        for (int col = 0; col < boardCols; col++)
                            for (int row = 0; row < boardRows; row++)
                            {
                                var allPos = new Vector2Int(col, row);
                                if (!firstEffect.useAbsoluteCoords && grid != null && grid[col, row].IsPlayerHere) continue;
                                AddPreviewObject(allPos, previewSprite);
                            }
                        break;
                    case TargetType.Any:
                        bool isMoveCard = card.Effects.Any(e => e.effectType == EffectType.Move);
                        for (int col = 0; col < boardCols; col++)
                            for (int row = 0; row < boardRows; row++)
                            {
                                var anyPos = new Vector2Int(col, row);
                                if (isMoveCard && !IsCellEmpty(anyPos)) continue;
                                if (!isMoveCard && grid[col, row].OccupyingAlly != null) continue;
                                AddPreviewObject(anyPos, previewSprite);
                            }
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

            // playerRangePreview는 항상 모든 enemyRangePreview(unitPreview) 위에 위치해야 함.
            // 일단 맨 위로 올린 뒤, unit 프리팹(player/enemy/ally)만 다시 SetAsLastSibling 으로 끌어올린다.
            obj.transform.SetAsLastSibling();
            BringUnitsToFront();
            targetList.Add(obj);
        }

        /// <summary>player/enemy/ally 본체 GameObject를 항상 최상단(SetAsLastSibling)으로 유지합니다.</summary>
        private void BringUnitsToFront()
        {
            if (playerObject != null) playerObject.transform.SetAsLastSibling();
            foreach (var enemy in enemies)
                if (enemy?.GameObject != null) enemy.GameObject.transform.SetAsLastSibling();
            foreach (var ally in allies)
                if (ally?.GameObject != null) ally.GameObject.transform.SetAsLastSibling();
        }

        /// <summary>플레이어 턴 시작 시 적·아군의 예정 행동 좌표를 보드에 표시합니다.</summary>
        private void ShowUnitAttackPreviews()
        {
            ClearUnitAttackPreviews();
            if (combatBoard == null) return;

            // 적 범위(공격/이동) 미리보기는 플레이어 턴에만 표시한다. 적 턴 중 빙결·기절·공포·속박
            // 부여(ApplyStatus → 7452/7461)로 이 메서드가 호출돼도 미리보기를 다시 만들지 않는다 —
            // 적 턴 시작 시 지운 enemy range sprite가 적 턴 도중 되살아나 안 사라지는 버그 방지.
            if (combatState != CombatState.PlayerTurn) return;

            // 902 유물 — 적의 행동 미리보기 숨김
            bool hideEnemyInfo = GameManager.Instance != null && GameManager.Instance.HasRelic(902);

            foreach (var enemy in enemies)
            {
                if (enemy.CurrentHP <= 0 || enemy.Behavior == null) continue;
                if (hideEnemyInfo) continue;
                // 기절·빙결·공포 상태의 적은 행동을 못하므로 공격/이동 미리보기를 표시하지 않는다.
                if (enemy.IsIncapacitated) continue;
                AddUnitPreviews(enemy.Behavior.GetAttackPreviewPositions(enemy), enemyAttackActive, unitAttackPreviewObjects);
                AddUnitPreviews(enemy.Behavior.GetSkillPreviewPositions(enemy),  enemyAttackActive,  unitSkillPreviewObjects);
                // 적 이동은 player 턴 시작 시 / 플레이어 이동 시마다 ComputeDeferredMovePreview로 다시 계산되어
                // plannedMovePositions에 담긴다. 멀티셀 적은 이동 후 점유할 모든 footprint 셀에 sprite 표시.
                AddUnitPreviews(enemy.Behavior.GetMoveSpritePositions(enemy),    enemyMoveActive,    unitMovePreviewObjects, enemyMovePreviewScale);
            }

            foreach (var ally in allies)
            {
                if (ally.Behavior == null) continue;
                AddUnitPreviews(ally.Behavior.GetAttackPreviewPositions(ally), playerAttackUnactive, unitAttackPreviewObjects);
                AddUnitPreviews(ally.Behavior.GetSkillPreviewPositions(ally),  playerAttackUnactive,  unitSkillPreviewObjects);
                AddUnitPreviews(ally.Behavior.GetMovePreviewPositions(ally),   playerMoveUnactive,   unitMovePreviewObjects, enemyMovePreviewScale);
            }
        }

        private void AddUnitPreviews(List<Vector2Int> positions, Sprite sprite, List<GameObject> typedList,
                                     float scale = -1f)
        {
            if (positions == null || sprite == null) return;
            if (scale < 0f) scale = enemyRangePreviewScale;   // 미지정 시 공격 배율 fallback
            foreach (var pos in positions)
            {
                if (!IsInBoard(pos)) continue;
                var obj = new GameObject("UnitPreview", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                obj.transform.SetParent(combatBoard, false);
                var rt = obj.GetComponent<RectTransform>();
                rt.anchoredPosition = GetCellPosition(pos.x, pos.y);
                rt.sizeDelta = new Vector2(cellWidth * scale, cellHeight * scale);
                var img = obj.GetComponent<UnityEngine.UI.Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                // 항상 하단(셀 위)에 배치 → 이후 추가되는 player hover preview가 자연히 위에 오게 됨
                obj.transform.SetSiblingIndex(boardCols * boardRows);
                unitPreviewObjects.Add(obj);
                typedList?.Add(obj);
            }
        }

        private void ClearUnitAttackPreviews()
        {
            foreach (var obj in unitPreviewObjects)
                if (obj != null) Destroy(obj);
            unitPreviewObjects.Clear();
            unitAttackPreviewObjects.Clear();
            unitMovePreviewObjects.Clear();
            unitSkillPreviewObjects.Clear();
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

            // 902 유물 — 적의 정보를 보여주지 않습니다 (? 아이콘만 표시)
            if (GameManager.Instance != null && GameManager.Instance.HasRelic(902))
            {
                var hidden = unknownEnemyInfoIcon != null ? unknownEnemyInfoIcon : attackActionIcon;
                actions.Add(new UnitActionEntry { Type = UnitActionType.Attack, Value = 0, Icon = hidden });
                enemy.PlannedActions = actions;
                // 적 행동 아이콘은 데이터만 저장 (UI 표시 제거됨)
                return;
            }

            if (enemy.Behavior != null)
            {
                var atkPos   = enemy.Behavior.GetAttackPreviewPositions(enemy);
                var movePos  = enemy.Behavior.GetMovePreviewPositions(enemy);
                var skillPos = enemy.Behavior.GetSkillPreviewPositions(enemy);

                // 즉시 이동(plannedMovePositions) + 지연 이동(DeferredMoveSteps)을 합쳐 표시
                int moveCount = movePos?.Count ?? 0;
                if (enemy.Behavior.HasDeferredMove) moveCount = Mathf.Max(moveCount, enemy.Behavior.DeferredMoveSteps);
                if (enemy.IsBound) moveCount = 0; // 속박: 이동 행동 아이콘 숨김

                if (atkPos?.Count   > 0) actions.Add(new UnitActionEntry { Type = UnitActionType.Attack, Value = enemy.Damage, Icon = attackActionIcon });
                if (moveCount > 0)       actions.Add(new UnitActionEntry { Type = UnitActionType.Move,   Value = moveCount,    Icon = moveActionIcon });
                if (skillPos?.Count > 0) actions.Add(new UnitActionEntry { Type = UnitActionType.Skill,  Value = 0,            Icon = skillActionIcon });
            }

            // 계획된 행동이 없으면 기본 공격 아이콘 표시
            if (actions.Count == 0)
                actions.Add(new UnitActionEntry { Type = UnitActionType.Attack, Value = enemy.Damage, Icon = attackActionIcon });

            enemy.PlannedActions = actions;
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
            // 아군 행동 아이콘은 데이터만 저장 (UI 표시 제거됨)
        }

        // ── Enemy 셀 Hover → 해당 적 범위 미리보기 ──────────────────────

        private EnemyInstance _hoveredEnemy;

        private void OnEnemyCellHoverEnter(CombatBoardCell cell)
        {
            // 카드 선택/타겟팅 중에는 적 hover 미리보기 비활성 (아이템 모드 중에는 허용)
            if (pendingCard != null || pendingCardIndex >= 0) return;

            // 함정 hover 미리보기 — 적 hover와 독립적으로 동작
            TryShowHazardHoverPreview(cell);

            // 902 유물 — 적 호버 미리보기 차단
            if (GameManager.Instance != null && GameManager.Instance.HasRelic(902)) return;

            // 적 턴 중에는 적 호버 범위 미리보기를 표시하지 않는다 — 적 턴엔 인텐트 비표시 원칙.
            // (마우스를 적 위에 올려 생성된 range sprite가 적 턴 동안 남는 것을 방지)
            if (combatState != CombatState.PlayerTurn) return;

            var enemy = cell.OccupyingEnemy;
            if (enemy == null || enemy.CurrentHP <= 0) return;
            if (_hoveredEnemy == enemy) return;

            ClearUnitAttackPreviews();
            _hoveredEnemy = enemy;

            if (enemy.Behavior == null) return;
            // 기절/빙결/공포 상태이면 행동 미리보기를 표시하지 않는다.
            if (enemy.IsIncapacitated) return;

            // 호버된 적만 sprite를 유지 — 기본 combat state와 동일한 active sprite를 사용한다.
            // (이전에는 unactive로 바뀌어 비정상이었음)
            AddUnitPreviews(enemy.Behavior.GetAttackPreviewPositions(enemy), enemyAttackActive, unitAttackPreviewObjects);
            AddUnitPreviews(enemy.Behavior.GetSkillPreviewPositions(enemy),  enemyAttackActive, unitSkillPreviewObjects);
            AddUnitPreviews(enemy.Behavior.GetMoveSpritePositions(enemy),    enemyMoveActive,    unitMovePreviewObjects, enemyMovePreviewScale);
        }

        private void OnEnemyCellHoverExit(CombatBoardCell cell)
        {
            // 함정 hover 미리보기는 셀에서 벗어나면 항상 정리한다 (카드 조준 여부와 무관)
            if (_hoveredHazard != null && cell.OccupyingTrap == _hoveredHazard)
                ClearHazardHoverPreview();

            if (pendingCard != null || pendingCardIndex >= 0) return;
            if (_hoveredEnemy != null && cell.OccupyingEnemy == _hoveredEnemy)
            {
                // 단순 destroy가 아닌 전체 적·아군 preview 재생성으로 복귀
                ShowUnitAttackPreviews();
            }
        }

        // ── Trap 셀 Hover → 함정 효과 범위 미리보기 ──────────────────────
        // 적이 자신의 다음 행동을 미리 보여주는 OnEnemyCellHoverEnter 패턴과 동일한 방식으로
        // 설치된 함정에 마우스를 올리면 영향 범위를 playerAttackActive 스프라이트로 표시한다.

        private HazardInstance _hoveredHazard;
        private List<GameObject> hazardHoverPreviewObjects = new List<GameObject>();

        private void TryShowHazardHoverPreview(CombatBoardCell cell)
        {
            var hazard = cell.OccupyingTrap;
            if (hazard?.Data == null) { ClearHazardHoverPreview(); return; }
            if (_hoveredHazard == hazard) return;

            ClearHazardHoverPreview();
            _hoveredHazard = hazard;

            if (playerAttackActive == null) return;

            int code = hazard.Data.hazardCode;
            Vector2Int center = hazard.Position;

            // 피해형 함정 — 피해 범위를 표시
            if (code == 32122 || code == 32123)
            {
                // 지뢰: 함정 위치 1칸
                AddHazardHoverPreview(center);
            }
            else if (code == 32220 || code == 32221)
            {
                // 폭탄: BFS-1 십자 (중심 + 4방향)
                Vector2Int[] offsets =
                {
                    new Vector2Int( 0,  0),
                    new Vector2Int( 0,  1),
                    new Vector2Int( 0, -1),
                    new Vector2Int( 1,  0),
                    new Vector2Int(-1,  0),
                };
                foreach (var off in offsets)
                    AddHazardHoverPreview(center + off);
            }
            // 이동형 함정 — 이동 예상 지점(거리 = value 인 모든 셀) 표시
            else if (code == 32222 || code == 32223)
            {
                int dist = Mathf.Max(1, hazard.Value);
                for (int dx = -dist; dx <= dist; dx++)
                {
                    int absDy = dist - Mathf.Abs(dx);
                    foreach (int sgn in new[] { 1, -1 })
                    {
                        int dy = absDy * sgn;
                        if (sgn == -1 && absDy == 0) continue;
                        AddHazardHoverPreview(center + new Vector2Int(dx, dy));
                    }
                }
            }
            // 그 외(곰덫 등) — 효과 범위가 함정 셀 자신이므로 미리보기 생략
        }

        private void AddHazardHoverPreview(Vector2Int pos)
        {
            if (!IsInBoard(pos)) return;
            AddPreviewObject(pos, playerAttackActive, hazardHoverPreviewObjects);
        }

        private void ClearHazardHoverPreview()
        {
            foreach (var obj in hazardHoverPreviewObjects)
                if (obj != null) Destroy(obj);
            hazardHoverPreviewObjects.Clear();
            _hoveredHazard = null;
        }

        // ── 타게팅 중 셀 hover preview (pushEnmemy / Movediagonal_Damage) ─────
        /// <summary>현재 조준 중인 카드 (sticky 상태 또는 EnterTargetingMode 진입 상태) 반환.</summary>
        private CardData GetCurrentlyAimedCard()
        {
            if (pendingCard != null) return pendingCard;
            if (pendingCardIndex >= 0 && pendingCardIndex < hand.Count) return hand[pendingCardIndex];
            return null;
        }

        private void OnTargetingCellHoverEnter(CombatBoardCell cell)
        {
            if (IsItemTargetingActive) return;
            var card = GetCurrentlyAimedCard();
            if (card?.Effects == null || card.Effects.Count == 0) return;
            var fx = card.Effects[0];

            // 휩쓸기/파워+range 카드는 셀 hover로 sprite를 변경하지 않음 (collapse에서만 처리)
            bool isSelfPlayRange = HasRangeOffsets(card)
                && (card.CardTypeFromCode == CardType.Power || fx.targeting == TargetType.All);
            if (isSelfPlayRange) return;

            ClearTargetingHoverPreview();
            Vector2Int pos = cell.GridPos;

            // All 카드 (TargetType.All): hover/exit으로는 sprite 변환하지 않음. CollapseScreenY 경로에서만 토글.
            if (fx.targeting == TargetType.All) { /* no-op: CollapseScreenY controls active/unactive */ }
            // Move/Attack 카드 sticky 중 범위 내 hover → 해당 셀만 unactive→active
            else if ((card.CardTypeFromCode == CardType.Move || card.CardTypeFromCode == CardType.Action || card.CardTypeFromCode == CardType.Power)
                && (cell.CurrentState == CellState.AreaSelectable || cell.CurrentState == CellState.TargetableEnemy))
            {
                Sprite activeSprite = card.CardTypeFromCode == CardType.Move ? playerMoveActive
                    : card.CardTypeFromCode == CardType.Power ? playerPowerActive : playerAttackActive;
                if (activeSprite != null)
                    SwapPreviewSprite(pos, activeSprite);
            }

            if (string.IsNullOrEmpty(fx.customEffectId)) return;

            if (fx.customEffectId.Equals("Movediagonal_Damage", System.StringComparison.OrdinalIgnoreCase))
            {
                Vector2Int dir = pos - playerSpawnCell;
                if (Mathf.Abs(dir.x) != 1 || Mathf.Abs(dir.y) != 1) return;
                if (!IsCellEmpty(pos)) return;

                var rightCell = new Vector2Int(playerSpawnCell.x + dir.x, playerSpawnCell.y);
                var upCell    = new Vector2Int(playerSpawnCell.x,         playerSpawnCell.y + dir.y);
                if (IsInBoard(rightCell))
                    AddPreviewObject(rightCell, playerAttackActive, targetingHoverPreviewObjects);
                if (IsInBoard(upCell))
                    AddPreviewObject(upCell,    playerAttackActive, targetingHoverPreviewObjects);
            }
            else if (fx.customEffectId.Equals("area_Damage", System.StringComparison.OrdinalIgnoreCase))
            {
                // 수류탄: rangeOffsets 안의 셀(unactive preview 존재)을 hover했을 때만 3×3 active 표시
                if (playerAttackActive != null && HasPreviewAt(pos))
                {
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            var areaPos = pos + new Vector2Int(dx, dy);
                            if (IsInBoard(areaPos))
                                AddPreviewObject(areaPos, playerAttackActive, targetingHoverPreviewObjects);
                        }
                }
            }
            else if (fx.customEffectId.Equals("3range_Damage", System.StringComparison.OrdinalIgnoreCase))
            {
                // 휩쓸기: rangeOffsets를 시계방향 정렬한 뒤 hover한 offset의 ±1 위치도 함께 active 표시
                if (playerAttackActive == null || fx.rangeOffsets == null || fx.rangeOffsets.Length == 0) return;
                var sorted = fx.rangeOffsets
                    .Select(o => o.ToVector2Int())
                    .OrderBy(v => Mathf.Atan2(v.x, v.y))
                    .ToList();
                Vector2Int selectedOffset = pos - playerSpawnCell;
                int idx = sorted.FindIndex(v => v == selectedOffset);
                if (idx < 0) return;
                int n = sorted.Count;
                for (int k = -1; k <= 1; k++)
                {
                    if (k == 0) continue; // 자기 셀은 위 분기에서 이미 active로 swap됨
                    var off = sorted[((idx + k) % n + n) % n];
                    var p = playerSpawnCell + off;
                    if (IsInBoard(p)) SwapPreviewSprite(p, playerAttackActive);
                }
            }
        }

        private void OnTargetingCellHoverExit(CombatBoardCell cell)
        {
            ClearTargetingHoverPreview();
            var card = GetCurrentlyAimedCard();
            if (card == null) return;
            var fx = card.Effects != null && card.Effects.Count > 0 ? card.Effects[0] : null;

            // 휩쓸기/파워+range 카드는 셀 hover로 sprite를 변경하지 않음
            if (fx != null && HasRangeOffsets(card)
                && (card.CardTypeFromCode == CardType.Power || fx.targeting == TargetType.All))
                return;

            // All 카드: CellExit으로 전체를 unactive로 되돌리지 않음. CollapseScreenY 경로에서만 토글.
            if (fx != null && fx.targeting == TargetType.All) { /* no-op */ }
            else if (card.CardTypeFromCode == CardType.Move || card.CardTypeFromCode == CardType.Action || card.CardTypeFromCode == CardType.Power)
            {
                Sprite unactiveSprite = card.CardTypeFromCode == CardType.Move ? playerMoveUnactive
                    : card.CardTypeFromCode == CardType.Power ? playerPowerUnactive : playerAttackUnactive;
                if (unactiveSprite != null)
                    SwapPreviewSprite(cell.GridPos, unactiveSprite);

                // 휩쓸기(3range_Damage): hover-enter에서 active로 swap한 ±1 인접 셀을 unactive로 되돌림
                if (fx != null && !string.IsNullOrEmpty(fx.customEffectId)
                    && fx.customEffectId.Equals("3range_Damage", System.StringComparison.OrdinalIgnoreCase)
                    && fx.rangeOffsets != null && fx.rangeOffsets.Length > 0
                    && unactiveSprite != null)
                {
                    var sorted = fx.rangeOffsets
                        .Select(o => o.ToVector2Int())
                        .OrderBy(v => Mathf.Atan2(v.x, v.y))
                        .ToList();
                    Vector2Int selectedOffset = cell.GridPos - playerSpawnCell;
                    int idx = sorted.FindIndex(v => v == selectedOffset);
                    if (idx >= 0)
                    {
                        int n = sorted.Count;
                        for (int k = -1; k <= 1; k++)
                        {
                            if (k == 0) continue;
                            var off = sorted[((idx + k) % n + n) % n];
                            var p = playerSpawnCell + off;
                            if (IsInBoard(p)) SwapPreviewSprite(p, unactiveSprite);
                        }
                    }
                }
            }
        }

        private void SwapPreviewSprite(Vector2Int pos, Sprite sprite)
        {
            var targetPos = GetCellPosition(pos.x, pos.y);
            // ClearCardRangePreview는 list에서 제거하지 않고 SetActive(false)만 한다 — 같은 위치에 stale(비활성) 객체가
            // 먼저 iterate되면 활성 객체에 swap이 적용되지 않는 버그가 생긴다. activeSelf=true인 객체만 대상으로 한다.
            foreach (var obj in hoverPreviewObjects)
            {
                if (obj == null || !obj.activeSelf) continue;
                var rt = obj.GetComponent<RectTransform>();
                if (rt != null && rt.anchoredPosition == targetPos)
                {
                    var img = obj.GetComponent<Image>();
                    if (img != null) img.sprite = sprite;
                    break;
                }
            }
        }

        private void SwapAllPreviewSprites(Sprite sprite)
        {
            if (sprite == null) return;
            foreach (var obj in hoverPreviewObjects)
            {
                if (obj == null) continue;
                var img = obj.GetComponent<Image>();
                if (img != null) img.sprite = sprite;
            }
        }

        private bool HasPreviewAt(Vector2Int pos)
        {
            var targetPos = GetCellPosition(pos.x, pos.y);
            foreach (var obj in hoverPreviewObjects)
            {
                if (obj == null) continue;
                var rt = obj.GetComponent<RectTransform>();
                if (rt != null && rt.anchoredPosition == targetPos) return true;
            }
            return false;
        }

        private void ClearTargetingHoverPreview()
        {
            foreach (var obj in targetingHoverPreviewObjects)
                if (obj != null) Destroy(obj);
            targetingHoverPreviewObjects.Clear();
        }

        /// <summary>카드 hover sprite 미리보기 제거. targeting 모드 중에는 셀 상태를 건드리지 않는다.</summary>
        /// <summary>외부에서 지정 좌표에 attack rangePreview를 표시합니다 (파워카드 아이콘 hover용).</summary>
        public void ShowPowerEffectRangePreview(UnityEngine.Vector2Int[] offsets, bool hidePlayerSprite = false)
        {
            ClearCardRangePreview();
            if (combatBoard == null || offsets == null) return;
            if (!hidePlayerSprite)
                AddPreviewObject(playerSpawnCell, playerPowerUnactive);
            foreach (var off in offsets)
            {
                Vector2Int pos = playerSpawnCell + off;
                if (!IsInBoard(pos)) continue;
                if (grid != null && grid[pos.x, pos.y].IsPlayerHere) continue;
                AddPreviewObject(pos, playerAttackUnactive);
            }
        }

        /// <summary>외부에서 rangePreview를 제거합니다.</summary>
        public void HidePowerEffectRangePreview() => ClearCardRangePreview();

        // destroy 대신 hide(SetActive false) — sticky 해제와 동시에 같은 카드 hover가 발동되어도
        // movesprite가 깜빡이지 않도록 sprite를 재사용한다. 다른 카드 hover/사용 시 ShowCardRangePreview 가
        // destroy + 재생성으로 처리한다. 메모리 누수가 우려되면 DestroyCardRangePreview() 호출.
        private void ClearCardRangePreview()
        {
            foreach (var obj in hoverPreviewObjects)
                if (obj != null && obj.activeSelf) obj.SetActive(false);
            // hoverPreviewObjects 와 _currentRangePreviewCard 는 유지 — 다음 ShowCardRangePreview 가 same-card 면 재활성화.
        }

        /// <summary>실제로 hoverPreviewObjects 를 destroy 한다. 전투 종료 등 sprite 자체를 없애야 할 때 호출.</summary>
        private void DestroyCardRangePreview()
        {
            foreach (var obj in hoverPreviewObjects)
                if (obj != null) Destroy(obj);
            hoverPreviewObjects.Clear();
            _currentRangePreviewCard = null;
        }

        private bool IsPowerWithRange(CardData card)
        {
            if (card == null || card.CardTypeFromCode != CardType.Power) return false;
            if (card.Effects == null || card.Effects.Count == 0) return false;
            var fx = card.Effects[0];
            return fx.rangeOffsets != null && fx.rangeOffsets.Length > 0;
        }

        private bool HasRangeOffsets(CardData card)
        {
            if (card == null || card.Effects == null || card.Effects.Count == 0) return false;
            var fx = card.Effects[0];
            return fx.rangeOffsets != null && fx.rangeOffsets.Length > 0;
        }

        private void SetRangePreviewSpriteByName(Sprite powerSprite, Sprite attackSprite)
        {
            foreach (var obj in hoverPreviewObjects)
            {
                if (obj == null) continue;
                var img = obj.GetComponent<Image>();
                if (img == null) continue;
                if (obj.name == "RangePreview_Power" && powerSprite != null)
                    img.sprite = powerSprite;
                else if (obj.name == "RangePreview_Attack" && attackSprite != null)
                    img.sprite = attackSprite;
            }
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
        private CellState GetHighlightState(CombatBoardCell cell, TargetType targetType, CardType cardType = CardType.Action)
        {
            bool hasEnemy = cell.OccupyingEnemy != null && cell.OccupyingEnemy.CurrentHP > 0;
            bool hasAlly  = cell.OccupyingAlly != null || cell.IsPlayerHere;
            return targetType switch
            {
                TargetType.Enemy => hasEnemy ? CellState.TargetableEnemy : CellState.InRange,
                TargetType.All   => CellState.AreaSelectable,
                TargetType.Any   => (cardType == CardType.Move && hasEnemy) ? CellState.InRange : CellState.AreaSelectable,
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
            Debug.Log($"[CancelTargeting] returnToHand={returnToHand} pendingCardIndex={pendingCardIndex} handCardObjects.Count={handCardObjects.Count}");
            DeckRoguelike.Cards.CardUI cardUI = null;
            if (pendingCardIndex >= 0 && pendingCardIndex < handCardObjects.Count)
            {
                cardUI = handCardObjects[pendingCardIndex]?.GetComponent<DeckRoguelike.Cards.CardUI>();
                Debug.Log($"[CancelTargeting] cardUI at idx {pendingCardIndex}: {(cardUI != null ? cardUI.name : "null")}");
            }
            else
            {
                Debug.LogWarning($"[CancelTargeting] pendingCardIndex 유효하지 않음 — SetLocked(false) 호출 안 됨");
            }

            // ReturnToHand 보다 먼저 pendingCard / range sprite 를 정리한다.
            // ReturnToHand 가 cursor 아래 다른 카드의 hover 를 즉시 트리거하는데, ShowCardRangePreview 의
            // `pendingCard != null && pendingCard != card` 가드 때문에 pendingCard 가 아직 이전 카드면
            // 새 카드의 range sprite 가 표시되지 않는다.
            Debug.LogWarning($"[pendingCard CLEAR @ CancelTargeting] combatState={combatState} returnToHand={returnToHand} stack=\n{System.Environment.StackTrace}");
            pendingCard      = null;
            pendingCardIndex = -1;
            SetCombatButtonsInteractable(true);
            // execute 경로에선 sprite 유지 (DiscardAndDestroy 동안 보임)
            if (returnToHand) ClearCardRangePreview();
            ClearTargetingHoverPreview();
            ClearAllHighlights();

            // returnToHand=true (취소 경로)일 때만 lock 해제 + 손패 복귀
            // returnToHand=false (execute 경로)일 땐 sticky/lock 유지 — 카드는 곧 ExecuteCard → DiscardAndDestroy로 정리됨
            if (cardUI != null && returnToHand)
            {
                cardUI.SetLocked(false);
                cardUI.ReturnToHand();
            }
        }

        private void SetCombatButtonsInteractable(bool interactable)
        {
            SetButtonRaycast(endTurnButton,      interactable);
            SetButtonRaycast(drawPileButton,     interactable);
            SetButtonRaycast(discardPileButton,  interactable);
            // 카드 sticky 중에는 사이드 버튼(map/deck/settings)도 차단
            InGameUIController.Instance?.SetSideButtonsInteractable(interactable);
            // 아이템 슬롯 클릭 차단 — 정적 플래그로 ItemSlotUI에서 검사
            AnyCardStickyActive = !interactable;
        }

        /// <summary>카드 sticky 진행 중 여부 — ItemSlotUI 등 외부에서 입력 차단 판단용.</summary>
        public static bool AnyCardStickyActive { get; private set; }

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
            Debug.Log($"[HandleCellClicked] cell={cell.GridPos} state={cell.CurrentState} combatState={combatState} pendingCard={(pendingCard != null ? pendingCard.cardName : "null")} IsPlayerHere={cell.IsPlayerHere} enemy={cell.OccupyingEnemy != null} ally={cell.OccupyingAlly != null} itemModes(move={isItemMoveMode},teleport={isItemTeleportMode},enemy={isItemEnemyTargetingMode},area={isItemAreaTargetingMode})");

            // 아이템 타겟팅 모드: 카드 타겟팅보다 우선
            if (isItemMoveMode)            { HandleItemMoveCellClicked(cell);     return; }
            if (isItemTeleportMode)        { HandleItemTeleportCellClicked(cell); return; }
            if (isItemEnemyTargetingMode)  { HandleItemEnemyCellClicked(cell);    return; }
            if (isItemAreaTargetingMode)   { HandleItemAreaCellClicked(cell);     return; }

            // Rest/Shop 상태에서 셀 클릭으로 노드 이동하던 흐름은 폐지됨.
            // 맵 이동은 손패의 "맵 이동" 카드로만 가능하다. (Rest/Shop을 떠나려면
            // 휴식·구매 카드를 모두 사용하거나, 맵 이동 카드 사용 → OnRestLeave/OnMerchantLeave 경로로 처리)
            if (pendingCard == null && (combatState == CombatState.Rest || combatState == CombatState.Shop || combatState == CombatState.Treasure)) return;

            if (pendingCard == null || pendingCard.Effects == null || pendingCard.Effects.Count == 0) return;

            // Shop 상태: sticky 카드가 있어도 셀 클릭으로 combat 실행 경로(ExecuteCard)에 진입하지 않는다.
            // shop의 모든 카드(이동·공격·Power)는 sticky→collapseY→release 흐름(=Power 카드 흐름)으로만 구매.
            if (combatState == CombatState.Shop)
            {
                Debug.Log("[HandleCellClicked] Shop: sticky 카드 있음 — 셀 클릭 무시 (구매는 collapse 영역으로 드래그 후 release)");
                return;
            }

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
                    Vector2Int offsetVec = offset.ToVector2Int();
                    Vector2Int pos = firstEffect.useAbsoluteCoords
                        ? offsetVec
                        : playerSpawnCell + offsetVec;
                    if (pos == cell.GridPos)
                    {
                        // 체스 piece 등 직선/대각선 Move 카드: 적이 길을 막으면 클릭도 차단
                        if (IsMoveLineBlockedByEnemy(pendingCard, firstEffect, offsetVec)) return;
                        inRange = true; break;
                    }
                }
                if (!inRange) return;
            }

            // Enemy: 적 있는 셀만 선택 가능 / Ally: 자신/아군 셀만 선택 가능
            if (firstEffect.targeting == TargetType.Enemy)
            {
                if (cell.OccupyingEnemy == null || cell.OccupyingEnemy.CurrentHP <= 0) { DeselectPendingCard(); return; }
            }
            else if (firstEffect.targeting == TargetType.Ally)
            {
                if (!cell.IsPlayerHere && cell.OccupyingAlly == null) { DeselectPendingCard(); return; }
            }
            else if (firstEffect.targeting == TargetType.Any)
            {
                bool isPushEnmemy = !string.IsNullOrEmpty(firstEffect.customEffectId)
                    && firstEffect.customEffectId.Equals("pushEnmemy", System.StringComparison.OrdinalIgnoreCase);
                // EffectType.Move 외에 endturn_ExhaustsMove(21000/21001 등) 같은 커스텀 이동 효과도 동일하게 취급.
                bool hasMoveEffect = pendingCard.Effects.Any(e =>
                    e.effectType == EffectType.Move ||
                    (e.effectType == EffectType.Custom
                        && !string.IsNullOrEmpty(e.customEffectId)
                        && e.customEffectId.Equals("endturn_ExhaustsMove", System.StringComparison.OrdinalIgnoreCase)));
                if (cell.OccupyingEnemy != null && !isPushEnmemy && hasMoveEffect) { DeselectPendingCard(); return; }
                if (cell.OccupyingAlly  != null) { DeselectPendingCard(); return; }
                if (cell.IsPlayerHere && hasMoveEffect) { DeselectPendingCard(); return; }
            }

            // 커스텀 효과별 추가 유효성 (pushEnmemy: 적 뒤 공간 필요 / Movediagonal_Damage: 빈 칸)
            if (!IsValidCustomEffectTarget(pendingCard, cell.GridPos)) { DeselectPendingCard(); return; }

            int savedIndex         = pendingCardIndex;
            Vector2Int selectedPos = cell.GridPos;
            CancelTargeting(returnToHand: false); // 카드 실행 → ReturnToHand 불필요, DiscardAndDestroy가 처리
            ExecuteCard(savedIndex, selectedPos);
        }

        /// <summary>주어진 카드가 보드 위에서 사용 가능한 셀(타겟)을 하나라도 가졌는지 검사.</summary>
        private bool HasAnyValidTargetForCard(CardData card)
        {
            if (card?.Effects == null || card.Effects.Count == 0 || grid == null) return true;
            var fx = card.Effects[0];
            var t = fx.targeting;
            if (t != TargetType.Enemy && t != TargetType.Any && t != TargetType.Ally) return true;

            bool isPushEnmemy = !string.IsNullOrEmpty(fx.customEffectId)
                && fx.customEffectId.Equals("pushEnmemy", System.StringComparison.OrdinalIgnoreCase);
            bool hasMoveEffect = card.Effects.Any(e => e.effectType == EffectType.Move);

            IEnumerable<Vector2Int> candidates;
            if (fx.rangeOffsets != null && fx.rangeOffsets.Length > 0)
            {
                var list = new List<Vector2Int>();
                foreach (var o in fx.rangeOffsets)
                {
                    var ov = o.ToVector2Int();
                    var p = fx.useAbsoluteCoords ? ov : playerSpawnCell + ov;
                    if (!IsInBoard(p)) continue;
                    // 체스 piece 등: 적이 길을 막은 셀은 유효 타겟이 아님
                    if (IsMoveLineBlockedByEnemy(card, fx, ov)) continue;
                    list.Add(p);
                }
                candidates = list;
            }
            else
            {
                var list = new List<Vector2Int>();
                for (int c = 0; c < boardCols; c++)
                    for (int r = 0; r < boardRows; r++)
                        list.Add(new Vector2Int(c, r));
                candidates = list;
            }

            // 돌진(pushEnmemy): 빈 칸 OR 적이 있고 그 뒤가 빈 칸인 경우 → 유효
            if (isPushEnmemy)
            {
                foreach (var pos in candidates)
                    if (IsValidPushTarget(pos)) return true;
                return false;
            }

            // 일반 Move: 범위 내 빈 칸이 하나라도 있어야 유효
            if (hasMoveEffect && t == TargetType.Any)
            {
                foreach (var pos in candidates)
                    if (IsCellEmpty(pos)) return true;
                return false;
            }

            foreach (var pos in candidates)
            {
                var cell = grid[pos.x, pos.y];
                if (t == TargetType.Enemy)
                {
                    if (cell.OccupyingEnemy != null && cell.OccupyingEnemy.CurrentHP > 0
                        && IsValidCustomEffectTarget(card, pos)) return true;
                }
                else if (t == TargetType.Ally)
                {
                    if ((cell.IsPlayerHere || cell.OccupyingAlly != null)
                        && IsValidCustomEffectTarget(card, pos)) return true;
                }
                else if (t == TargetType.Any)
                {
                    if (cell.OccupyingAlly != null) continue;
                    if (IsValidCustomEffectTarget(card, pos)) return true;
                }
            }
            return false;
        }

        /// <summary>현재 sticky/lock된 pendingCard를 deselect하여 손패로 복귀시킨다.</summary>
        private void DeselectPendingCard()
        {
            Debug.LogWarning($"[DeselectPendingCard] 진입 — pendingCardIndex={pendingCardIndex} pendingCard={(pendingCard != null ? pendingCard.cardName : "null")} combatState={combatState} stack=\n{System.Environment.StackTrace}");
            if (pendingCardIndex < 0 || pendingCardIndex >= handCardObjects.Count) return;
            var ui = handCardObjects[pendingCardIndex]?.GetComponent<CardUI>();
            if (ui != null) { ui.SetLocked(false); ui.Deselect(); }
            CancelTargeting(returnToHand: true);
        }

        /// <summary>외부(사이드 버튼 등)에서 카드 sticky를 해제할 때 호출. sticky가 없으면 false.
        /// settings/deck 버튼이 sticky 도중 눌렸을 때 패널을 여는 대신 sticky만 해제하는 용도.</summary>
        public bool ReleaseStickyCardIfAny()
        {
            if (!AnyCardStickyActive && pendingCard == null) return false;
            DeselectPendingCard();
            return true;
        }

        public bool IsInBoard(Vector2Int pos)
            => pos.x >= 0 && pos.x < boardCols && pos.y >= 0 && pos.y < boardRows;

        /// <summary>해당 셀이 보드 내이고 플레이어/적/아군 누구도 점유하지 않은 빈 칸인지 확인합니다.</summary>
        public bool IsCellEmpty(Vector2Int pos)
        {
            if (!IsInBoard(pos)) return false;
            var c = grid[pos.x, pos.y];
            return !c.IsPlayerHere && c.OccupyingEnemy == null && c.OccupyingAlly == null;
        }

        /// <summary>
        /// 돌진(pushEnmemy) 카드가 해당 셀을 선택할 수 있는지 검사합니다.
        /// - 빈 칸이면 그냥 이동 가능 → true
        /// - 적이 있으면 이동 방향(player→pos)으로 적의 footprint를 1칸 옮긴 자리가 모두 비어있어야 true
        ///   (멀티셀 적 — 예: 2x2 대왕슬라임 — 도 뒤가 충분히 비어있다면 밀 수 있도록)
        /// </summary>
        public bool IsValidPushTarget(Vector2Int pos)
        {
            if (!IsInBoard(pos)) return false;
            var cell = grid[pos.x, pos.y];
            if (cell.IsPlayerHere || cell.OccupyingAlly != null) return false;
            if (cell.OccupyingEnemy == null) return true;

            Vector2Int dir = pos - playerSpawnCell;
            int sx = dir.x == 0 ? 0 : (dir.x > 0 ? 1 : -1);
            int sy = dir.y == 0 ? 0 : (dir.y > 0 ? 1 : -1);

            EnemyInstance enemy = cell.OccupyingEnemy;
            Vector2Int newAnchor = enemy.GridPos + new Vector2Int(sx, sy);
            return IsFootprintFreeForEnemy(newAnchor, enemy.Size, ignoreEnemy: enemy);
        }

        /// <summary>
        /// 이동 카드의 직선/대각선 사거리에서 LOS(line-of-sight) 차단 검사.
        /// 룩·비숍·퀸 같은 체스 piece는 적을 통과해서 이동할 수 없음 — "돌진해서 부딪힌다" 개념.
        /// - Move 카드만 적용. useAbsoluteCoords=true는 적용 안 함.
        /// - offset이 직선(dx==0 || dy==0) 또는 대각선(|dx|==|dy|)이고 거리가 2 이상이면 검사.
        /// - 사이에 살아있는 적이 있으면 true(차단).
        /// - 나이트(L자) / 1칸 이동(킹) / 무제한 사거리 카드는 항상 false.
        /// </summary>
        public bool IsMoveLineBlockedByEnemy(CardData card, CardEffect fx, Vector2Int offset)
        {
            if (card == null || fx == null) return false;
            if (card.CardTypeFromCode != CardType.Move) return false;
            if (fx.useAbsoluteCoords) return false;
            if (grid == null) return false;

            int dx = offset.x;
            int dy = offset.y;
            int adx = Mathf.Abs(dx);
            int ady = Mathf.Abs(dy);
            bool isStraight = (dx == 0) ^ (dy == 0); // 정확히 하나만 0
            bool isDiagonal = (adx == ady) && adx > 0;
            if (!isStraight && !isDiagonal) return false;

            int dist = Mathf.Max(adx, ady);
            if (dist <= 1) return false;

            int sx = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
            int sy = dy == 0 ? 0 : (dy > 0 ? 1 : -1);

            for (int i = 1; i < dist; i++)
            {
                var p = playerSpawnCell + new Vector2Int(sx * i, sy * i);
                if (!IsInBoard(p)) continue;
                var c = grid[p.x, p.y];
                if (c.OccupyingEnemy != null && c.OccupyingEnemy.CurrentHP > 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 카드 효과별 셀 유효성 검사. customEffectId에 따라 추가 제약을 적용합니다.
        /// 기본 카드는 true (기존 검사만 적용).
        /// </summary>
        public bool IsValidCustomEffectTarget(CardData card, Vector2Int pos)
        {
            if (card?.Effects == null || card.Effects.Count == 0) return true;
            var fx = card.Effects[0];
            if (string.IsNullOrEmpty(fx.customEffectId)) return true;

            if (fx.customEffectId.Equals("pushEnmemy", System.StringComparison.OrdinalIgnoreCase))
                return IsValidPushTarget(pos);
            if (fx.customEffectId.Equals("Movediagonal_Damage", System.StringComparison.OrdinalIgnoreCase))
                return IsCellEmpty(pos);
            if (fx.customEffectId.Equals("BehindEnemy_Move", System.StringComparison.OrdinalIgnoreCase))
                return IsValidBehindEnemyTarget(pos);

            // 생성(Generate_)은 이미 지대(Zone)가 있는 셀에 중복 생성 불가. 단 설치(Trap)/아군과는 공존 가능.
            // 설치(trap_)는 함정/지대/아군과 무관하게 항상 배치 가능(중복 허용 — 같은 칸 함정은 덮어씀)하고,
            // 소환(summon_)은 유닛 점유만 검사(Any 분기에서 처리)하므로 여기서 위해는 막지 않는다.
            if (fx.customEffectId.StartsWith("Generate_", System.StringComparison.OrdinalIgnoreCase))
            {
                if (IsInBoard(pos) && grid[pos.x, pos.y].OccupyingZone != null) return false;
            }
            return true;
        }

        /// <summary>
        /// 벽력일섬(BehindEnemy_Move) 셀 유효성:
        /// pos에 살아있는 적이 있고, 적이 바라보는 방향의 반대(=등 뒤) 칸이 보드 내·빈 칸이어야 true.
        /// </summary>
        public bool IsValidBehindEnemyTarget(Vector2Int pos)
        {
            if (!IsInBoard(pos)) return false;
            var cell = grid[pos.x, pos.y];
            var enemy = cell.OccupyingEnemy;
            if (enemy == null || enemy.CurrentHP <= 0) return false;

            // FacingRight=true → 등은 왼쪽(x-1), FacingRight=false → 등은 오른쪽(x+1)
            int behindCol = enemy.FacingRight ? enemy.GridPos.x - 1 : enemy.GridPos.x + 1;
            return IsCellEmpty(new Vector2Int(behindCol, enemy.GridPos.y));
        }

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
            if (combatState != CombatState.PlayerTurn && combatState != CombatState.EnemyTurn) return;
            foreach (var enemy in enemies)
                if (enemy.CurrentHP > 0) return;

            combatState = CombatState.Victory;
            PlaySound(victorySound);
            Debug.Log("[CombatController] 전투 승리!");
            FireRelicHook((r, ctx) => r.OnCombatVictory(ctx));
            RevertTemporaryUpgrades();
            RestoreShootCardRanges();

            // 전투 종료 — 활성 파워와 그에 묶인 UI 아이콘을 정리한다.
            // (예: NextUseCopyCardPower가 보상 카드(60xxx)에서 잘못 트리거되어 사본을 생성하는 문제 방지,
            //  사용되지 않은 1회성 파워의 잔류 아이콘 제거.)
            _activePowers.Clear();
            DeckRoguelike.UI.InGameUIController.Instance?.ClearPlayerEffects();

            // 맵 복귀 후 보상 카드 지급
            _pendingVictoryReward = true;
            OnCombatEnded?.Invoke(true);
        }

        // 패배 시 MapState 복귀와 함께 지급할 카드 코드 (60008 전투재시작, 60009 메인메뉴).
        public const int DefeatRewardCardCode1 = 60008;
        public const int DefeatRewardCardCode2 = 60009;

        /// <summary>전투 중 플레이어 사망 처리 — CheckVictory와 동일한 후처리 후 MapState로 복귀하여 60008/60009 카드를 지급한다.</summary>
        private void HandlePlayerDefeatInCombat()
        {
            combatState = CombatState.Defeat;
            PlaySound(defeatSound);
            Debug.Log("[CombatController] 전투 패배! 맵으로 복귀");
            RevertTemporaryUpgrades();
            RestoreShootCardRanges();

            // 보상 카드 시점과 동일하게 활성 파워와 UI 효과를 정리한다.
            _activePowers.Clear();
            DeckRoguelike.UI.InGameUIController.Instance?.ClearPlayerEffects();

            // 맵 복귀 후 패배 카드 지급
            _pendingDefeatReward = true;
            OnCombatEnded?.Invoke(false);
        }

        /// <summary>패배 시 MapState로 돌아온 직후 호출 — 60008/60009 카드를 _rewardCards에 채워 드로우한다.</summary>
        private void DistributeDefeatRewards()
        {
            // 1) 덱 정리 — DistributeVictoryRewards와 동일.
            if (deckManager != null)
            {
                var masterDeckRefs = new HashSet<CardData>(deckManager.MasterDeck);
                foreach (var card in hand)
                {
                    if (card == null) continue;
                    if (masterDeckRefs.Contains(card))
                        deckManager.AddToDiscardPile(card);
                }
                hand.Clear();
                ClearHandVisuals();
                deckManager.MoveDiscardToDrawPile();
            }

            // 2) 패배 카드(60008/60009) 생성
            _rewardCards.Clear();
            PrepareDefeatRewardCards();

            // 3) drawPile 맨 위에 올리고 그만큼 드로우. 보상 상황에서는 draw/discard pile 버튼을
            //    위로 올려 카드 정보/버린 카드 더미 확인이 가능하도록 한다.
            if (deckManager != null)
            {
                for (int i = _rewardCards.Count - 1; i >= 0; i--)
                    deckManager.AddToTopOfDrawPile(_rewardCards[i]);
            }

            DrawCards(_rewardCards.Count);

            SetPileButtonMode(PileButtonMode.Default);
            SetCombatButtonsInteractable(true);
            RefreshDeckPileButtonVisibility();
        }

        private void PrepareDefeatRewardCards()
        {
            AddDefeatRewardCard(DefeatRewardCardCode1);
            AddDefeatRewardCard(DefeatRewardCardCode2);
        }

        private void AddDefeatRewardCard(int code)
        {
            var template = CardRegistry.GetCard(code);
            if (template == null)
            {
                Debug.LogWarning($"[BoardController] 패배 보상 카드 템플릿 없음 (code={code})");
                return;
            }
            _rewardCards.Add(template.Clone());
        }

        private void DistributeVictoryRewards()
        {
            // 손패 카드를 아래로 떨어뜨리는 연출을 먼저 수행한 뒤 정리/보상 드로우를 진행한다.
            StartCoroutine(DistributeVictoryRewardsCoroutine());
        }

        private IEnumerator DistributeVictoryRewardsCoroutine()
        {
            // 0) 손패 카드들을 위에서 아래로 떨어뜨리는 연출 (카드 오브젝트 자체를 이동).
            yield return AnimateHandCardsDropDown();

            // 1) 덱 정리: 손패 버리기 + 버림 더미 → 뽑기 더미.
            //    23212(nextuse_copyCard)나 이벤트/아이템 효과로 손패에 들어온 임시 사본은 masterDeck에
            //    포함되지 않으므로 여기서 걸러내어 그대로 폐기한다(다음 전투에 남지 않도록).
            if (deckManager != null)
            {
                var masterDeckRefs = new HashSet<CardData>(deckManager.MasterDeck);
                foreach (var card in hand)
                {
                    if (card == null) continue;
                    if (masterDeckRefs.Contains(card))
                        deckManager.AddToDiscardPile(card);
                }
                hand.Clear();
                ClearHandVisuals();
                deckManager.MoveDiscardToDrawPile();
            }

            // 2) handContainer 재위치 — 다음 드로우가 정상 위치(화면 하단)에서 시작되도록.
            SnapHandContainerToScreenBottom();

            // 3) 보상 카드 생성
            _rewardCards.Clear();
            PrepareRewardCards();

            // 4) 보상 카드를 drawPile 맨 위에 넣기
            if (deckManager != null)
            {
                for (int i = _rewardCards.Count - 1; i >= 0; i--)
                    deckManager.AddToTopOfDrawPile(_rewardCards[i]);
            }

            // 5) 보상 카드 수만큼 드로우. 보상 상황에서는 draw/discard pile 버튼을 위로 올려
            //    카드 정보(드로우 더미) / 버린 카드 더미 확인이 가능하도록 한다.
            //    Why: OnCardDown에서 SetCombatButtonsInteractable(false)로 blocksRaycasts가
            //         꺼진 채 전투가 끝났을 수 있어 명시적으로 복구한다. 위치 슬라이드와 부모
            //         활성화는 RefreshDeckPileButtonVisibility가 담당.
            DrawCards(_rewardCards.Count);

            SetPileButtonMode(PileButtonMode.Default);
            SetCombatButtonsInteractable(true);
            RefreshDeckPileButtonVisibility();
        }

        /// <summary>현재 손패 카드 오브젝트들을 staggered 하게 아래로 떨어뜨린다. ClearHandVisuals 직전에 호출.</summary>
        private IEnumerator AnimateHandCardsDropDown()
        {
            var snapshot = new List<GameObject>(handCardObjects);
            if (snapshot.Count == 0) yield break;

            int idx = 0;
            foreach (var card in snapshot)
            {
                if (card == null) { idx++; continue; }
                var rt = card.GetComponent<RectTransform>();
                var cardUI = card.GetComponent<DeckRoguelike.Cards.CardUI>();
                // CardUI의 입력/locked 상태는 해제 — 떨어지는 동안 사용자가 만지지 못하도록
                if (cardUI != null) cardUI.SetLocked(true);
                if (rt != null)
                    StartCoroutine(DropSingleCard(rt, victoryCardDropDuration, victoryCardDropOffset, idx * victoryCardDropStagger));
                idx++;
            }

            float totalWait = victoryCardDropDuration + Mathf.Max(0, snapshot.Count - 1) * victoryCardDropStagger;
            yield return new WaitForSecondsRealtime(totalWait);
        }

        private IEnumerator DropSingleCard(RectTransform rt, float duration, float dropOffset, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (rt == null) yield break;

            Vector2 start = rt.anchoredPosition;
            Vector2 end = start + new Vector2(0f, -dropOffset);
            float t = 0f;
            while (t < duration && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                // 자유낙하 느낌의 가속 — quadratic ease-in
                float curve = k * k;
                rt.anchoredPosition = Vector2.Lerp(start, end, curve);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = end;
        }

        private void PrepareRewardCards()
        {
            bool isDanger = GameManager.Instance != null && GameManager.Instance.IsDangerCombatEncounter;
            bool isBoss   = GameManager.Instance != null && GameManager.Instance.IsBossEncounter;

            // 골드 보상 — 금액 미리 결정하여 description에 반영
            // cardCode 60000 = 골드(Reward_Gold). 50000은 저주 카드로 재배정되어 effects가 비어있음.
            var goldCard = CardRegistry.GetCard(60000);
            if (goldCard != null && goldCard.effects != null && goldCard.effects.Count > 0)
            {
                var gc = goldCard.Clone();
                int goldAmount = isDanger ? UnityEngine.Random.Range(30, 41) : UnityEngine.Random.Range(10, 21);
                gc.description = gc.description.Replace("{G}", goldAmount.ToString());
                gc.effects[0].value = goldAmount;
                _rewardCards.Add(gc);
            }

            // 유물 보상
            _pendingRewardRelic = null;
            if (isBoss)
            {
                // 보스: 유물선택 카드(60010)는 이 화면에서 주지 않는다.
                // 다음 스테이지 카드(60011)로 다음 Act에 진입한 뒤 AdvanceToNextStage에서 지급한다.
            }
            else if (isDanger)
            {
                _pendingRewardRelic = InGameUIController.Instance?.PickRewardRelic(false);
                if (_pendingRewardRelic != null)
                {
                    // 50001은 Addressables에 등록되어 있지 않음 — 60001 사용.
                    var relicCard = CardRegistry.GetCard(60001);
                    if (relicCard != null)
                    {
                        var rc = relicCard.Clone();
                        rc.cardName    = _pendingRewardRelic.relicName;
                        rc.description = _pendingRewardRelic.description;
                        rc.iconCode    = _pendingRewardRelic.relicCode;
                        _rewardCards.Add(rc);
                    }
                }
            }

            // 900 판도라 추가 유물
            if (GameManager.Instance != null && GameManager.Instance.HasRelic(900))
            {
                var extraRelic = InGameUIController.Instance?.PickRewardRelic(false);
                if (extraRelic != null && (extraRelic.relicCode != (_pendingRewardRelic?.relicCode ?? -1)))
                {
                    // 두 번째 유물은 _pendingRewardRelic에 추가 저장 불가 — 바로 지급
                    GameManager.Instance.AddRelic(extraRelic.relicCode);
                }
            }

            // 아이템 보상 (TODO: 조건에 따라)
            _pendingRewardItem = null;

            // 카드 보상 — 50003은 Addressables에 등록되어 있지 않음 — 60003 사용.
            var cardReward = CardRegistry.GetCard(60003);
            if (cardReward != null) _rewardCards.Add(cardReward.Clone());

            if (isBoss)
            {
                // 보스: 맵 이동 대신 다음 스테이지(60011) 카드 지급 — 사용 시 Act +1 후 맵 재생성.
                var nextStageCard = CardRegistry.GetCard(60011);
                if (nextStageCard != null) _rewardCards.Add(nextStageCard.Clone());
            }
            else
            {
                // 맵 이동 카드 한 장 — 다음 노드로 이동하기 위해 지급.
                AddMapMoveRewardCard();
            }
        }

        public void ClaimPendingRewardRelic()
        {
            if (_pendingRewardRelic == null) return;
            GameManager.Instance?.AddRelic(_pendingRewardRelic.relicCode);
            Debug.Log($"[Reward] 유물 획득: {_pendingRewardRelic.relicName}");
            _pendingRewardRelic = null;
        }

        /// <summary>보물방 등에서 Reward_Relic 카드가 ClaimPendingRewardRelic으로 지급할 유물을 미리 설정.</summary>
        public void SetPendingRewardRelic(Relic.RelicData relic)
        {
            _pendingRewardRelic = relic;
        }

        /// <summary>
        /// 207/208/209 각인된 부적 픽 취소 시 호출: 이미 소비된 60001(유물 보상) 카드를 손패에 복구합니다.
        /// _pendingRewardRelic도 함께 되돌려 다음 카드 사용으로 재시도가 가능하게 합니다.
        /// 손패가 가득 차 있으면(10장) 복구를 건너뜁니다.
        /// </summary>
        public void RestoreCanceledRelicCard(Relic.RelicData relic)
        {
            if (relic == null) return;
            var template = CardRegistry.GetCard(60001);
            if (template == null) return;

            var card = template.Clone();
            card.cardName    = relic.relicName;
            card.description = relic.description;
            card.iconCode    = relic.relicCode;

            _pendingRewardRelic = relic;
            AddRewardCardToHandFree(card);
            RefreshCardPlayability();
            Debug.Log($"[RestoreCanceledRelicCard] 유물 보상 카드 복구: {relic.relicName} ({relic.relicCode})");
        }

        public void ClaimPendingRewardItem()
        {
            if (_pendingRewardItem == null) return;
            GameManager.Instance?.AddItem(_pendingRewardItem);
            Debug.Log($"[Reward] 아이템 획득: {_pendingRewardItem.itemName}");
            _pendingRewardItem = null;
        }

        /// <summary>
        /// 60003(Reward_Card) 카드 효과에서 호출 — 카드 보상 픽 모드 진입을 예약한다.
        /// 실제 진입은 ExecuteCard가 종료된 직후 ApplyPendingRewardPick에서 처리.
        /// </summary>
        public void RequestRewardCardPick()
        {
            _rewardPickPending = true;
        }

        /// <summary>
        /// 60004(Reward_Skip) 카드 효과에서 호출 — 카드 보상 픽 모드 종료를 예약한다.
        /// </summary>
        public void RequestRewardCardPickEnd()
        {
            _rewardPickEndPending = true;
        }

        /// <summary>
        /// 60010(유물선택) 카드 효과에서 호출 — 보스 유물 픽 모드 진입을 예약한다.
        /// 실제 진입은 ExecuteCard 종료 직후 손패가 정리된 뒤 StartRelicPickFlow에서 처리.
        /// </summary>
        public void RequestRelicPick()
        {
            _relicPickPending = true;
        }

        /// <summary>
        /// 60011(다음 스테이지) 카드 효과에서 호출 — Act +1 및 맵 재생성을 예약한다.
        /// 실제 처리는 ExecuteCard 종료 직후 AdvanceToNextStage에서 수행.
        /// </summary>
        public void RequestNextStage()
        {
            _nextStagePending = true;
        }

        /// <summary>
        /// 60008(전투재시작) 카드 효과에서 호출 — 패배 직전 전투를 다시 시작하도록 예약한다.
        /// 실제 처리는 ExecuteCard 종료 직후 InGameUIController.RestartCombat에서 수행.
        /// </summary>
        public void RequestCombatRestart()
        {
            _restartCombatPending = true;
        }

        /// <summary>
        /// 60009(메인메뉴) 카드 효과에서 호출 — 메인메뉴로 복귀하도록 예약한다.
        /// 실제 처리는 ExecuteCard 종료 직후 InGameUIController.ReturnToMainMenu에서 수행.
        /// </summary>
        public void RequestReturnToMainMenu()
        {
            _returnMainMenuPending = true;
        }

        /// <summary>
        /// 카드 보상 픽 모드 진입:
        /// - 손패에 남아있는 다른 보상 카드(gold/relic/item/map_move)는 _savedRewardCardsForPick에 저장 후 손패에서 제거.
        /// - drawPileButton이 RangeIndicator 역할 수행.
        /// - discardPileButton이 Skip 버튼 역할 수행 (넘기기 카드 대체).
        /// - 직업 보상 풀에서 카드 3장을 drawPile 상단에 올린 뒤 드로우.
        /// </summary>
        private void StartRewardPickFlow()
        {
            // 1) 현재 손패에 남아있는 카드들 중 _rewardCards에 속한 것만 임시 보관 → 픽 종료 후 재드로우.
            //    그 외(이벤트·아이템·파워로 들어온 임시 사본)는 그대로 폐기해 다음 전투에 남지 않도록 한다.
            _savedRewardCardsForPick.Clear();
            for (int i = hand.Count - 1; i >= 0; i--)
            {
                var c = hand[i];
                if (c == null) continue;
                if (_rewardCards.Contains(c))
                {
                    _savedRewardCardsForPick.Insert(0, c);
                    _rewardCards.Remove(c);
                }
                hand.RemoveAt(i);
                if (i < handCardObjects.Count)
                {
                    var obj = handCardObjects[i];
                    handCardObjects.RemoveAt(i);
                    if (obj != null) Destroy(obj);
                }
            }
            RepositionHandCards();

            // 2) 픽 모드 플래그/UI 초기화. sticky 상태에서 막혔던 blocksRaycasts를 복구하고
            //    RefreshDeckPileButtonVisibility로 위치/부모 활성화를 한 번에 처리한다.
            _rewardPickActive = true;
            _rewardPickCards.Clear();
            _rewardPickViewCard = null;
            SetCombatButtonsInteractable(true);
            RefreshDeckPileButtonVisibility();
            SetPileButtonMode(PileButtonMode.RewardPick);

            // 3) 보상 카드 3장을 drawPile 상단에 추가 후 드로우. (Skip은 discardPileButton으로 처리)
            var picks = BuildRewardPickCards();

            int totalToDraw = picks.Count;
            if (deckManager != null)
            {
                for (int i = picks.Count - 1; i >= 0; i--)
                    deckManager.AddToTopOfDrawPile(picks[i]);
            }
            foreach (var p in picks) _rewardPickCards.Add(p);
            DrawCards(totalToDraw);
        }

        /// <summary>
        /// 보스 유물 선택 모드(60010) 진입:
        /// - 카드 보상 픽과 동일한 sticky/collapse 흐름(_rewardPickActive)을 재사용하되 _relicPickActive로 분기.
        /// - 보스 유물 풀에서 직업/보유 규칙을 준수해 서로 다른 3장을 뽑아 60001(유물 보상) 카드 비주얼로 표시.
        /// - 넘기기(60004)는 만들지 않으며 discard/draw pile 버튼은 숨긴(down) 상태로 둔다.
        /// - 한 장을 고르면 유물을 획득하고 EndRewardPickFlow로 이전 상태로 복귀한다.
        /// </summary>
        private void StartRelicPickFlow()
        {
            // 1) 손패에 남은 보상 카드는 _savedRewardCardsForPick에 보관 후 손패 비우기 (StartRewardPickFlow와 동일).
            _savedRewardCardsForPick.Clear();
            for (int i = hand.Count - 1; i >= 0; i--)
            {
                var c = hand[i];
                if (c == null) continue;
                if (_rewardCards.Contains(c))
                {
                    _savedRewardCardsForPick.Insert(0, c);
                    _rewardCards.Remove(c);
                }
                hand.RemoveAt(i);
                if (i < handCardObjects.Count)
                {
                    var obj = handCardObjects[i];
                    handCardObjects.RemoveAt(i);
                    if (obj != null) Destroy(obj);
                }
            }
            RepositionHandCards();

            // 2) 픽 모드 플래그 초기화. _rewardPickActive를 함께 켜서 기존 픽 흐름 special-casing을 재사용,
            //    _relicPickActive로 (선택=유물 획득 / pile 버튼 숨김)을 분기한다.
            _rewardPickActive = true;
            _relicPickActive  = true;
            _rewardPickCards.Clear();
            _rewardPickViewCard = null;
            _pendingRelicChoices.Clear();
            SetCombatButtonsInteractable(true);

            // 3) 보스 유물 3장 뽑기 — 직업/보유 규칙 준수.
            var relics = InGameUIController.Instance?.PickBossRelicChoices(3)
                         ?? new List<Relic.RelicData>();

            var picks = new List<CardData>();
            var template = CardRegistry.GetCard(60001);
            if (template != null)
            {
                foreach (var relic in relics)
                {
                    if (relic == null) continue;
                    var rc = template.Clone();
                    rc.cardName    = relic.relicName;
                    rc.description = relic.description;
                    rc.iconCode    = relic.relicCode;   // 선택 시 iconCode로 유물을 역참조.
                    picks.Add(rc);
                    _pendingRelicChoices.Add(relic);
                }
            }

            // 4) 유물 픽 모드: discard/draw pile 버튼은 숨김(down). 넘기기 listener도 두지 않는다.
            SetPileButtonMode(PileButtonMode.Default);
            HideDeckPileButtons();

            // 5) 뽑은 유물 카드를 drawPile 상단에 올린 뒤 드로우.
            if (picks.Count == 0)
            {
                // 줄 유물이 없으면(풀 고갈) 픽을 건너뛰고 즉시 복귀.
                Debug.LogWarning("[RelicPick] 선택 가능한 보스 유물이 없습니다 — 픽 건너뜀");
                EndRewardPickFlow();
                return;
            }
            if (deckManager != null)
            {
                for (int i = picks.Count - 1; i >= 0; i--)
                    deckManager.AddToTopOfDrawPile(picks[i]);
            }
            foreach (var p in picks) _rewardPickCards.Add(p);
            DrawCards(picks.Count);
        }

        /// <summary>
        /// 직업 보상 풀에서 픽 카드를 생성한다. 402(전체 풀), 206(+1장), 300(강화 상태) 유물 효과를 반영.
        /// 외부(906/909 유물 보상 등)에서도 동일한 풀 생성 로직을 재사용할 수 있도록 public.
        /// </summary>
        public List<CardData> BuildRewardPickCards()
        {
            var gm = GameManager.Instance;
            var character = gm?.SelectedCharacter ?? CharacterType.Warrior;

            // 402 유물: 직업/등급 무관 전체 풀
            List<CardData> pool;
            if (gm != null && gm.HasRelic(402))
            {
                pool = new List<CardData>();
                foreach (var ch in (CharacterType[])System.Enum.GetValues(typeof(CharacterType)))
                {
                    foreach (var card in CardRegistry.GetRewardPool(ch))
                        if (!pool.Contains(card)) pool.Add(card);
                }
            }
            else
            {
                pool = CardRegistry.GetRewardPool(character);
            }

            // 카드 보상은 공통(ClassDigit==1) 카드를 제외하고 직업 카드만 제시한다.
            // 402(모든 직업 보상)를 보유해도 공통 카드는 제외 — 직업 카드 전체 풀에서만 고른다.
            pool = pool.FindAll(c => c.ClassDigit != 1);

            int desired = 3;
            if (gm != null && gm.HasRelic(206)) desired += 1;
            bool upgradedReward = gm != null && gm.HasRelic(300);

            // 보스 처치 보상은 무조건 영웅(Rare) 카드만 제시 — 하위 등급 폴백 금지.
            // 일반 보상은 RollCardRewardRarity로 배치 단위 등급을 굴려 결정(3장 모두 같은 등급).
            bool bossReward = gm != null && gm.IsBossEncounter;
            CardRarity batchRarity = gm != null
                ? gm.RollCardRewardRarity(bossReward)
                : CardRarity.Common;

            var picked = new List<CardData>();
            var result = new List<CardData>();
            for (int i = 0; i < desired; i++)
            {
                var card = PickRewardPickRarityCard(pool, batchRarity, picked, forceRarity: bossReward);
                if (card == null) break;
                picked.Add(card);

                var presented = card;
                if (upgradedReward && !card.IsUpgraded)
                {
                    var up = CardRegistry.GetCard(card.UpgradedCode);
                    if (up != null) presented = up;
                }
                result.Add(presented.Clone());
            }
            return result;
        }

        /// <summary>희귀도 정확 매칭 → 인접 등급 폴백.
        /// forceRarity=true면(보스 보상 등) 인접 등급으로 폴백하지 않고 지정 등급만 사용하며,
        /// distinct가 고갈되면 같은 등급 내 중복을 허용해 반드시 해당 등급 카드를 반환한다.</summary>
        private static CardData PickRewardPickRarityCard(List<CardData> pool, CardRarity rarity, List<CardData> already, bool forceRarity = false)
        {
            var primary = new List<CardData>();
            foreach (var c in pool)
            {
                if (c.Rarity != rarity) continue;
                if (already.Contains(c)) continue;
                primary.Add(c);
            }
            if (primary.Count > 0)
                return primary[UnityEngine.Random.Range(0, primary.Count)];

            if (forceRarity)
            {
                // 보스 보상: 하위 등급으로 폴백하지 않는다. distinct 고갈 시 같은 등급 중복 허용.
                var sameRarity = pool.FindAll(c => c.Rarity == rarity);
                if (sameRarity.Count > 0)
                    return sameRarity[UnityEngine.Random.Range(0, sameRarity.Count)];
                return null;
            }

            CardRarity[] fallback = rarity switch
            {
                CardRarity.Rare     => new[] { CardRarity.Uncommon, CardRarity.Common },
                CardRarity.Uncommon => new[] { CardRarity.Common,   CardRarity.Rare   },
                _                   => new[] { CardRarity.Uncommon, CardRarity.Rare   },
            };
            foreach (var fr in fallback)
            {
                var alt = new List<CardData>();
                foreach (var c in pool)
                {
                    if (c.Rarity != fr) continue;
                    if (already.Contains(c)) continue;
                    alt.Add(c);
                }
                if (alt.Count > 0)
                    return alt[UnityEngine.Random.Range(0, alt.Count)];
            }
            return null;
        }

        /// <summary>
        /// 카드 보상 픽 모드에서 discardPileButton을 Skip 버튼으로 사용. 클릭 시 픽 모드 종료.
        /// 60004(Reward_Skip) 카드 사용과 동일한 효과.
        /// </summary>
        private void OnRewardPickSkipClicked()
        {
            if (!_rewardPickActive) return;
            EndRewardPickFlow();
        }

        /// <summary>60004(넘기기) 카드를 복제하여 반환. 등록 실패 시 null.</summary>
        private static CardData CreateRewardSkipCard()
        {
            var template = CardRegistry.GetCard(RewardSkipCardCode);
            if (template == null)
            {
                Debug.LogWarning($"[BoardController] 넘기기 카드 템플릿 없음 (code={RewardSkipCardCode})");
                return null;
            }
            return template.Clone();
        }

        /// <summary>
        /// 픽 모드에서 보상 카드 중 하나가 선택되었을 때 처리. 선택한 카드는 덱에 추가되고
        /// 나머지 픽/스킵 카드는 소멸된다. 이후 EndRewardPickFlow로 저장된 보상 카드를 재발급.
        /// </summary>
        private void HandleRewardPickCardChosen(int handIndex)
        {
            if (handIndex < 0 || handIndex >= hand.Count) return;
            CardData chosen = hand[handIndex];
            if (chosen == null) return;

            // 보스 유물 선택(60010): 덱 추가 대신 iconCode(=relicCode)로 유물을 역참조해 획득.
            if (_relicPickActive)
            {
                var relic = _pendingRelicChoices.Find(r => r != null && r.relicCode == chosen.iconCode);
                if (relic != null)
                {
                    GameManager.Instance?.AddRelic(relic.relicCode);
                    Debug.Log($"[RelicPick] 보스 유물 획득: {relic.relicName} ({relic.relicCode})");
                }
                EndRewardPickFlow();
                return;
            }

            Debug.Log($"[RewardPick] 카드 선택: {chosen.CardName}");
            DeckManager.Instance?.AddCardToDeck(chosen);

            EndRewardPickFlow();
        }

        /// <summary>
        /// 픽 모드 종료: 손패에 남아있는 픽/스킵 카드를 모두 소멸시키고,
        /// _savedRewardCardsForPick의 보상 카드를 drawPile 상단에 다시 올린 뒤 드로우.
        /// drawPileButton의 RangeIndicator 모드 해제.
        /// </summary>
        private void EndRewardPickFlow()
        {
            // 1) 손패의 픽/스킵 카드 모두 소멸.
            for (int i = hand.Count - 1; i >= 0; i--)
            {
                var c = hand[i];
                if (c == null) continue;
                if (!_rewardPickCards.Contains(c)) continue;
                hand.RemoveAt(i);
                if (i < handCardObjects.Count)
                {
                    var obj = handCardObjects[i];
                    handCardObjects.RemoveAt(i);
                    if (obj != null) Destroy(obj);
                }
                deckManager?.ExhaustCard(c);
            }
            _rewardPickCards.Clear();
            _rewardPickActive = false;
            _relicPickActive  = false;
            _pendingRelicChoices.Clear();
            _rewardPickViewCard = null;
            // 픽 종료 — sticky 카드의 잔여 포인터 상태 초기화 (이후 HandleCellClicked 등에서 stale pendingCard 사용 방지)
            pendingCard = null;
            pendingCardIndex = -1;

            // 2) pile 버튼 mode를 Default로 복귀 (라벨/listener/interactable 자동 갱신).
            SetPileButtonMode(PileButtonMode.Default);
            SetCombatButtonsInteractable(true);

            // 3) 저장해둔 보상 카드(gold/relic/item/map_move)를 drawPile 상단에 다시 올린 뒤 드로우.
            if (_savedRewardCardsForPick.Count > 0)
            {
                if (deckManager != null)
                {
                    for (int i = _savedRewardCardsForPick.Count - 1; i >= 0; i--)
                        deckManager.AddToTopOfDrawPile(_savedRewardCardsForPick[i]);
                }
                foreach (var c in _savedRewardCardsForPick)
                    if (!_rewardCards.Contains(c)) _rewardCards.Add(c);
                int drawCount = _savedRewardCardsForPick.Count;
                _savedRewardCardsForPick.Clear();
                DrawCards(drawCount);
            }

            RepositionHandCards();
            UpdatePileCounters();

            // 보스 다음 Act 진입 직후의 유물 픽이었다면, 픽이 끝난 지금 맵 이동 카드를 지급한다.
            // (픽 전에 지급하면 StartRelicPickFlow의 손패 정리에서 소멸되므로 이 시점까지 미뤘다.)
            if (_grantMapMoveAfterRelicPick)
            {
                _grantMapMoveAfterRelicPick = false;
                GrantInitialMapMoveCard();
            }

            // 픽 종료 후 남은 보상 카드 유무에 따라 pile 버튼 위치 결정 (있으면 위, 없으면 아래).
            RefreshDeckPileButtonVisibility();
        }

        /// <summary>맵 이동 시 사용하지 않은 보상 카드를 소멸시킵니다.</summary>
        public void ExhaustRewardCards()
        {
            if (_rewardCards.Count == 0) return;
            foreach (var rc in _rewardCards)
            {
                int idx = hand.IndexOf(rc);
                if (idx >= 0)
                {
                    hand.RemoveAt(idx);
                    if (idx < handCardObjects.Count)
                    {
                        if (handCardObjects[idx] != null) Destroy(handCardObjects[idx]);
                        handCardObjects.RemoveAt(idx);
                    }
                }
                deckManager?.ExhaustCard(rc);
            }
            _rewardCards.Clear();
            RepositionHandCards();
            UpdatePileCounters();
        }

        // ── Map Move 카드 ───────────────────────────────────────────────
        // 70000 = 맵 이동, 70001 = 맵 이동+ (202 나침반 보유 시).
        public const int MapMoveCardCode         = 70000;
        public const int MapMoveCardCodeUpgraded = 70001;

        /// <summary>현재 런 기준 맵 이동 카드를 한 장 복제하여 반환. 202(나침반) 보유 시 70001 사용.</summary>
        public static CardData CreateMapMoveCard()
        {
            int code = (GameManager.Instance != null && GameManager.Instance.HasRelic(202))
                ? MapMoveCardCodeUpgraded
                : MapMoveCardCode;
            var template = CardRegistry.GetCard(code);
            if (template == null)
            {
                Debug.LogWarning($"[BoardController] 맵 이동 카드 템플릿 없음 (code={code})");
                return null;
            }
            return template.Clone();
        }

        /// <summary>주어진 카드가 맵 이동 카드(map_Move 커스텀 효과 포함)인지 검사.</summary>
        public static bool IsMapMoveCard(CardData card)
        {
            if (card?.Effects == null) return false;
            foreach (var fx in card.Effects)
            {
                if (fx.effectType != EffectType.Custom) continue;
                if (string.IsNullOrEmpty(fx.customEffectId)) continue;
                if (fx.customEffectId.Equals("map_Move", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>맵 이동 보상 카드를 _rewardCards에 추가. null이면 무시.</summary>
        private void AddMapMoveRewardCard()
        {
            var card = CreateMapMoveCard();
            if (card != null) _rewardCards.Add(card);
        }

        /// <summary>맵 이동 카드 사용 처리. MapState/Shop/Rest 어디서든 그 자리에서 타겟팅 진입한다.
        /// Shop/Rest에서 호출돼도 leave는 하지 않는다 (다른 reward 카드가 즉시 사라지는 것을 방지).
        /// 실제 leave는 사용자가 셀을 클릭해 이동이 확정된 시점에 HandleMapMoveCellClicked에서 수행.</summary>
        private void HandleMapMoveCardPlay(int handIndex)
        {
            if (handIndex < 0 || handIndex >= hand.Count) return;
            CardData card = hand[handIndex];
            if (!IsMapMoveCard(card)) return;
            // 선택형 이벤트에서 사용된 경우 eventPanel을 닫아 셀이 보이도록 한다.
            InGameUIController.Instance?.HideEventPanelIfOpen();
            EnterMapMoveTargeting(handIndex);

            // 타겟팅 진입 후 카드 UI 상태를 정리한다.
            //
            // [중요] Shop(IsShopMode=true)에 한해서만 필요한 정리다.
            //   - Shop의 맵 이동 카드는 IsShopMode=true → CardUI.Update의 (isLocked + IsShopMode + collapsed + click)
            //     단축 분기가 사용자의 다음 셀 클릭을 PlayCard로 가로채기 때문에 sticky/locked를 풀어줘야 한다.
            //   - MapState/Rest/Treasure의 일반 맵 이동 카드는 IsShopMode=false라 위 단축 분기가 fire되지 않으므로
            //     cleanup이 불필요하고, 오히려 sticky를 부당하게 풀어버려 사용자 인지 상으로 "sticky 해제됐는데 pendingCard는 남음"
            //     이라는 버그가 발생한다. 전투 Move 카드와 동일하게 sticky를 유지하여 셀 클릭으로 이동을 확정한다.
            if (handIndex < handCardObjects.Count)
            {
                var cardObj = handCardObjects[handIndex];
                if (cardObj != null)
                {
                    var cardUI = cardObj.GetComponent<DeckRoguelike.Cards.CardUI>();
                    if (cardUI != null && cardUI.IsShopMode)
                    {
                        cardUI.SetLocked(false);
                        var cg = cardObj.GetComponent<UnityEngine.CanvasGroup>();
                        if (cg != null) cg.blocksRaycasts = false;
                        cardUI.ReturnToHand();
                    }
                }
            }

            // OnCardDown에서 비활성화된 endTurn/draw/discard + 사이드(map/deck/settings) 버튼 raycast를 복구.
            SetCombatButtonsInteractable(true);
        }

        /// <summary>맵 이동 카드를 타겟팅 모드로 진입 — 카드의 rangeOffsets 범위 내 셀을 highlight.</summary>
        private void EnterMapMoveTargeting(int handIndex)
        {
            Debug.Log($"[EnterMapMoveTargeting] enter — handIndex={handIndex} handCount={hand.Count} gridNull={grid==null} mapGridNull={mapGrid==null} mapPlayerPos={mapPlayerPos}");
            if (handIndex < 0 || handIndex >= hand.Count) { Debug.LogWarning($"[EnterMapMoveTargeting] invalid handIndex"); return; }
            if (grid == null || mapGrid == null) { Debug.LogWarning($"[EnterMapMoveTargeting] grid/mapGrid null — sticky만 진입하고 highlight 없음"); return; }

            CardData card = hand[handIndex];
            if (!IsMapMoveCard(card)) { Debug.LogWarning($"[EnterMapMoveTargeting] card is not map move: {card?.cardName}"); return; }

            pendingCardIndex = handIndex;
            pendingCard      = card;

            ClearAllHighlights();
            HighlightMapMoveRange(card);
            Debug.Log($"[EnterMapMoveTargeting] complete — pendingCard={pendingCard.cardName} hoverPreviewCount={hoverPreviewObjects.Count}");
        }

        /// <summary>맵 이동 카드 hover 시 플레이어 맵 좌표를 기준으로 이동 sprite(unactive)만 표시.
        /// 셀 상태는 변경하지 않으므로 클릭 불가 — 단순 미리보기.
        /// EnterMapMoveTargeting이 본격 클릭 가능 상태로 전환한다.</summary>
        public void ShowMapMoveRangePreview(CardData card)
        {
            EnumerateMapMoveCells(card, (col, row) =>
            {
                if (playerMoveUnactive != null)
                    AddPreviewObject(new Vector2Int(col, row), playerMoveUnactive);
            }, clearFirst: true);
        }

        /// <summary>맵 이동 카드의 rangeOffsets 기준으로 이동 가능 셀에 AreaSelectable 상태 + 이동 sprite를 표시.
        /// 카드의 offset은 (col, row) 형식이며, 항상 플레이어 현재 맵 좌표를 기준으로 적용된다.</summary>
        private void HighlightMapMoveRange(CardData card)
        {
            int highlighted = 0;
            EnumerateMapMoveCells(card, (col, row) =>
            {
                // MapState 셀은 SetMapVisual로 interactable=false 상태. AreaSelectable로 셋해 클릭 가능하게 한다.
                grid[col, row].SetState(CellState.AreaSelectable);
                // 이동 카드 unactive sprite를 셀 위에 표시. 셀 hover 시 active sprite로 교체.
                if (playerMoveUnactive != null)
                    AddPreviewObject(new Vector2Int(col, row), playerMoveUnactive);
                highlighted++;
            }, clearFirst: true);
            Debug.Log($"[HighlightMapMoveRange] {card.cardName} → {highlighted} cells highlighted as AreaSelectable. mapPlayerPos={mapPlayerPos}");
        }

        /// <summary>맵 이동 카드의 사거리 내 미방문 셀(col, row)을 콜백으로 열거.
        /// IsDisabled(보스 도달 불가) 셀은 제외 — 막다른 노드로 이동해 진행 불가가 되는 것을 방지.
        /// clearFirst=true면 기존 hover preview sprite를 미리 정리한다.</summary>
        private void EnumerateMapMoveCells(CardData card, System.Action<int, int> action, bool clearFirst)
        {
            if (grid == null || mapGrid == null) return;
            if (card?.Effects == null || card.Effects.Count == 0) return;
            var fx = card.Effects[0];
            if (fx.rangeOffsets == null) return;

            if (clearFirst) ClearCardRangePreview();

            // mapPlayerPos는 (row, col) 순서로 저장되어 있다. 카드의 offset은 (col, row) 형식.
            Vector2Int playerOnMap = new Vector2Int(mapPlayerPos.y, mapPlayerPos.x);

            foreach (var offset in fx.rangeOffsets)
            {
                var target = playerOnMap + offset.ToVector2Int();
                int col = target.x;
                int row = target.y;
                if (col < 0 || col >= boardCols || row < 0 || row >= boardRows) continue;
                var node = mapGrid[row, col];
                if (node.IsVisited) continue;
                // 보스에 도달할 수 없는 셀은 이동 후보에서 제외 (BFS는 RecomputeBossReachability에서 갱신됨).
                if (node.IsDisabled) continue;
                action(col, row);
            }
        }

        /// <summary>맵 이동 카드가 mouse-down + 드래그 중일 때만 sprite swap이 발동되어야 한다.
        /// 단순 카드 hover 상태(=마우스 미클릭)에선 unactive 그대로 유지.</summary>
        private bool IsMapMoveDragActive()
        {
            if (combatState != CombatState.MapState
                && combatState != CombatState.Shop
                && combatState != CombatState.Rest
                && combatState != CombatState.Treasure) return false;
            if (pendingCard == null || !IsMapMoveCard(pendingCard)) return false;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed) return false;
            return true;
        }

        /// <summary>맵 이동 카드 드래그 중 셀 hover 진입 → unactive → active.
        /// MapState/Shop/Rest/Treasure 모두 동작. mouse-down + drag중일 때만 swap.</summary>
        private void OnMapMoveCellHoverEnter(CombatBoardCell cell)
        {
            if (!IsMapMoveDragActive()) return;
            if (hoverPreviewObjects.Count == 0) return; // sprite preview 비활성
            if (playerMoveActive != null)
                SwapPreviewSprite(cell.GridPos, playerMoveActive);
        }

        /// <summary>맵 이동 카드 드래그 중 셀 hover 종료 → active → unactive 복원.</summary>
        private void OnMapMoveCellHoverExit(CombatBoardCell cell)
        {
            if (!IsMapMoveDragActive()) return;
            if (hoverPreviewObjects.Count == 0) return;
            if (playerMoveUnactive != null)
                SwapPreviewSprite(cell.GridPos, playerMoveUnactive);
        }

        /// <summary>맵 이동 카드 타겟팅 중 셀 클릭 처리 (MapState/Shop/Rest 공용).
        /// - 사거리 안의 미방문 셀이면 카드 소비 + (Shop/Rest이면 leave 수행) + 이동
        /// - 그 외엔 타겟팅 해제</summary>
        private void HandleMapMoveCellClicked(CombatBoardCell cell)
        {
            if (pendingCard == null) return;
            if (mapGrid == null || grid == null) return;
            if (combatState != CombatState.MapState
                && combatState != CombatState.Shop
                && combatState != CombatState.Rest
                && combatState != CombatState.Treasure) return;

            var fx = pendingCard.Effects != null && pendingCard.Effects.Count > 0 ? pendingCard.Effects[0] : null;
            if (fx?.rangeOffsets == null) { DeselectPendingCard(); return; }

            Vector2Int playerOnMap = new Vector2Int(mapPlayerPos.y, mapPlayerPos.x);
            bool inRange = false;
            foreach (var offset in fx.rangeOffsets)
                if (playerOnMap + offset.ToVector2Int() == cell.GridPos) { inRange = true; break; }
            if (!inRange) { DeselectPendingCard(); return; }

            int col = cell.GridPos.x;
            int row = cell.GridPos.y;
            if (row < 0 || row >= boardRows || col < 0 || col >= boardCols) { DeselectPendingCard(); return; }
            if (mapGrid[row, col].IsVisited) { DeselectPendingCard(); return; }
            // 보스에 도달할 수 없는 셀로의 이동 차단 (안전망 — EnumerateMapMoveCells에서 이미 highlight 제외).
            if (mapGrid[row, col].IsDisabled) { DeselectPendingCard(); return; }

            int savedIdx       = pendingCardIndex;
            CardData savedCard = pendingCard;
            Vector2Int target  = cell.GridPos;
            bool wasShop = combatState == CombatState.Shop;
            bool wasRest = combatState == CombatState.Rest;
            bool wasTreasure = combatState == CombatState.Treasure;

            // pending 해제 및 손패에서 카드 제거. 이동 sprite preview도 정리.
            CancelTargeting(returnToHand: false);
            ClearAllHighlights();
            ClearCardRangePreview();

            if (savedIdx >= 0 && savedIdx < hand.Count && hand[savedIdx] == savedCard)
            {
                hand.RemoveAt(savedIdx);
                if (savedIdx < handCardObjects.Count)
                {
                    var obj = handCardObjects[savedIdx];
                    handCardObjects.RemoveAt(savedIdx);
                    if (obj != null) Destroy(obj);
                }
            }
            _rewardCards.Remove(savedCard);
            RepositionHandCards();

            // Shop/Rest/Treasure를 떠나야 하면 여기서 leave (다른 reward 카드 exhaust + UI 정리).
            // 이전엔 HandleMapMoveCardPlay가 leave를 즉시 호출해 사용자가 다른 카드를 잃었음.
            // 이제 셀 클릭으로 이동이 확정되는 시점에만 leave가 발생한다.
            if (wasShop)
            {
                // 상점형 이벤트와 일반 상점은 다른 leave 경로 사용.
                if (_currentShopEventCode > 0) OnShopEventLeave();
                else OnMerchantLeave();
            }
            else if (wasRest) OnRestLeave();
            else if (wasTreasure) OnTreasureLeave();

            // 실제 맵 이동 — TryMapMoveToCell이 노드 효과(전투/이벤트/상점 등) 발화까지 처리.
            TryMapMoveToCell(target);
        }

        /// <summary>combatHudElements를 일괄 활성/비활성화합니다.</summary>
        private void SetCombatHudActive(bool active)
        {
            foreach (var obj in combatHudElements)
                if (obj != null) obj.SetActive(active);
        }

        /// <summary>
        /// 대상 GameObject와 모든 부모를 강제로 활성화한다.
        /// SetCombatHudActive(false)로 부모가 꺼져 있어도 Shop 버튼 등이 보이도록 보장.
        /// </summary>
        private void ForceActivateWithParents(GameObject go)
        {
            if (go == null) return;
            var t = go.transform;
            while (t != null)
            {
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                t = t.parent;
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

        /// <summary>
        /// 위치변경술: 플레이어와 적의 위치를 서로 교환합니다.
        /// 적이 null/사망 상태이거나 보드 밖이면 무시. 이동 카운트(OnPlayerMoved 훅)도 증가시킵니다.
        /// </summary>
        public bool SwapPlayerEnemy(EnemyInstance enemy)
        {
            if (enemy == null || enemy.CurrentHP <= 0) return false;
            // 멀티셀 적은 1셀 플레이어와 위치 교환 불가
            if (enemy.Size.x > 1 || enemy.Size.y > 1) return false;
            Vector2Int P = playerSpawnCell;
            Vector2Int E = enemy.GridPos;
            if (P == E) return false;
            if (!IsInBoard(P) || !IsInBoard(E)) return false;

            var cellP = grid[P.x, P.y];
            var cellE = grid[E.x, E.y];

            // 두 셀에서 기존 점유 정보 제거
            cellP.IsPlayerHere     = false;
            cellE.OccupyingEnemy   = null;
            cellP.ClearHighlight();
            cellE.ClearHighlight();

            // 수평 이동 방향으로 플레이어 facing 갱신
            if (E.x > P.x)      SetFacingRight(true);
            else if (E.x < P.x) SetFacingRight(false);

            // 플레이어 → E
            cellE.IsPlayerHere = true;
            cellE.SetState(CellState.PlayerOccupied);
            playerSpawnCell    = E;
            if (playerObject != null)
            {
                var rt = playerObject.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = GetCellPosition(E.x, E.y) + playerCellOffset;
                playerObject.transform.SetAsLastSibling();
            }

            // 적 → P
            if (P.x > E.x)      SetEnemyFacingRight(enemy, true);
            else if (P.x < E.x) SetEnemyFacingRight(enemy, false);

            enemy.GridPos          = P;
            cellP.OccupyingEnemy   = enemy;
            cellP.SetState(CellState.EnemyOccupied);
            if (enemy.GameObject != null)
            {
                var rt = enemy.GameObject.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = GetCellPosition(P.x, P.y) + playerCellOffset;
                enemy.GameObject.transform.SetAsLastSibling();
            }

            _playerMoveCount++;
            FireRelicHook((r, ctx) => r.OnPlayerMoved(ctx, _playerMoveCount));

            // 플레이어·적 위치가 모두 바뀌었으니 모든 적 행동을 재계산 (플레이어 턴 중이면 적용).
            RePlanAllEnemyTurns();

            Debug.Log($"[SwapPlayerEnemy] 플레이어 ↔ {enemy.Name} 위치 교환 ({P} ↔ {E})");
            return true;
        }

        /// <summary>보유 중인 모든 유물의 훅을 순서대로 호출합니다.</summary>
        private void FireRelicHook(System.Action<RelicEffect, RelicCombatContext> hook)
        {
            if (GameManager.Instance == null) return;
            var ctx = new RelicCombatContext { Board = this };
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
            DeckRoguelike.UI.InGameUIController.Instance?.SetPlayerEffect("strength", strengthIcon, playerStrength, "힘", "공격 시 추가 피해를 줍니다.");
        }

        /// <summary>민첩 수치를 증가시킵니다. Block 효과에 자동 반영됩니다.</summary>
        public void AddDexterity(int amount)
        {
            playerDexterity += amount;
            RefreshHandDisplay();
            DeckRoguelike.UI.InGameUIController.Instance?.SetPlayerEffect("dexterity", dexterityIcon, playerDexterity, "민첩", "방어 시 추가 방어도를 얻습니다.");
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
            // 뒷면 공격(반대 방향) 보너스: 공격자가 플레이어의 바라보는 방향 반대편에 있으면 2배
            if (attacker != null && IsAttackFromBehind(playerSpawnCell, facingRight, attacker.GridPos))
            {
                rawDamage *= 2;
                Debug.Log($"[BackAttack] 플레이어 뒷면 피격! 데미지 2배 → {rawDamage}");
            }

            // 회피 (군번줄): 공격 무효화
            if (dodgeCharges > 0)
            {
                Debug.Log("[CombatController] 회피! 공격 무효화");
                return 0;
            }

            // 유물 훅: 피해량 수정 가능
            if (GameManager.Instance != null)
            {
                var ctx = new RelicCombatContext { Board = this };
                foreach (var relic in GameManager.Instance.Relics)
                    relic.OnPlayerDamaged(ctx, ref rawDamage);
            }

            // 받는 피해 배율 (철가면)
            rawDamage = Mathf.RoundToInt(rawDamage * incomingDamageMultiplier);
            rawDamage = Mathf.Max(0, rawDamage);

            // 파워 훅: 공격 받기 직전 (가시갑옷, 방어태세)
            // 방어태세(반사)가 활성 중이면 공격자에게 피해를 그대로 돌려준 뒤 플레이어는 무적(피해 0)이 된다.
            bool reflectInvincible = false;
            if (attacker != null)
            {
                var attackedSnapshot = new System.Collections.Generic.List<CombatPowerEffect>(_activePowers);
                foreach (var p in attackedSnapshot)
                {
                    p.OnPlayerAttacked(this, attacker, rawDamage);
                    if (p is ReflectionDamagePower rp && rp.RemainingTurns > 0)
                        reflectInvincible = true;
                }
            }

            // 반사 중에는 방어도 소모/HP 손실 없이 공격을 완전히 무효화한다.
            if (reflectInvincible)
            {
                Debug.Log($"[방어태세] {attacker?.Name}의 공격 {rawDamage} 반사 — 플레이어 무적 (피해 0)");
                return 0;
            }

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

            // 970 유물 등 — HP 손실 시 유물 훅
            if (actualDamage > 0 && GameManager.Instance != null)
            {
                var lossCtx = new RelicCombatContext { Board = this };
                foreach (var relic in GameManager.Instance.Relics)
                    relic.OnPlayerLostHp(lossCtx, actualDamage);
            }

            // 반사 유물 훅: 확정 피해량 + 공격한 적 정보 전달
            if (actualDamage > 0 && attacker != null && GameManager.Instance != null)
            {
                var reflectCtx = new RelicCombatContext { Board = this };
                foreach (var relic in GameManager.Instance.Relics)
                    relic.OnPlayerDamagedBy(reflectCtx, actualDamage, attacker);
            }

            return actualDamage;
        }

        // ── 파워 카드 시스템 ──────────────────────────────────────────────────

        /// <summary>파워 카드 지속 효과를 전투에 등록합니다 (아이콘 표시 없음).</summary>
        public void RegisterPowerNoIcon(CombatPowerEffect power)
        {
            _activePowers.Add(power);
        }

        /// <summary>파워 카드 지속 효과를 전투에 등록합니다.</summary>
        public void RegisterPower(CombatPowerEffect power) => RegisterPower(power, null, null, null, null);

        /// <summary>파워 카드 지속 효과를 전투에 등록하고 TopBar에 아이콘을 표시합니다.</summary>
        public void RegisterPower(CombatPowerEffect power, string tooltipName, string tooltipDesc, UnityEngine.Sprite icon = null, Vector2Int[] rangeOffsets = null, bool hidePlayerSprite = false)
        {
            _activePowers.Add(power);
            string key = $"power_{power.GetType().Name}_{_activePowers.Count}";
            power.IconKey = key;
            DeckRoguelike.UI.InGameUIController.Instance?.SetPowerCardEffect(key, icon, tooltipName ?? power.GetType().Name, tooltipDesc ?? "", rangeOffsets, hidePlayerSprite);
        }

        /// <summary>1회성 파워(예: NextUseCopyCardPower)가 소비되었을 때 활성 파워 목록과 UI 아이콘을 같이 제거합니다.</summary>
        public void UnregisterPower(CombatPowerEffect power)
        {
            if (power == null) return;
            _activePowers.Remove(power);
            if (!string.IsNullOrEmpty(power.IconKey))
                DeckRoguelike.UI.InGameUIController.Instance?.RemovePlayerEffect(power.IconKey);
        }

        /// <summary>카드 효과로 HP를 잃지 않는 상태 여부</summary>
        public bool NoCardHpLoss => _noCardHpLoss;

        /// <summary>카드 효과의 HP 손실 면역을 설정합니다.</summary>
        public void SetNoCardHpLoss(bool value) => _noCardHpLoss = value;

        /// <summary>카드 소멸 면역을 설정합니다.</summary>
        public void SetNoExhaust(bool value) => _noExhaust = value;

        /// <summary>카드 효과로 HP 손실 시 파워/유물 훅을 발동합니다.</summary>
        public void FirePowerOnPlayerLostHp(int amount)
        {
            foreach (var p in _activePowers) p.OnPlayerLostHp(this, amount);

            if (amount > 0 && GameManager.Instance != null)
            {
                var ctx = new RelicCombatContext { Board = this };
                foreach (var relic in GameManager.Instance.Relics)
                    relic.OnPlayerLostHp(ctx, amount);
            }
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

        public void AddTimedBonusDraw(int amount, int turns)
        {
            _timedBonusDraw = amount;
            _timedBonusDrawTurns = turns;
        }

        /// <summary>첫 턴에만 추가로 드로우합니다 (109 보스, 206 불타는 심장).</summary>
        public void AddFirstTurnBonusDraw(int amount)
        {
            _firstTurnBonusDraw += amount;
            Debug.Log($"[CombatController] 첫 턴 추가 드로우 +{amount} (총: {_firstTurnBonusDraw})");
        }

        /// <summary>이번 턴 한정으로 추가 드로우 수를 누적합니다 (107 유물: 3턴마다 +1). OnPlayerTurnStart에서 호출.</summary>
        public void AddThisTurnBonusDraw(int amount)
        {
            _thisTurnBonusDraw += amount;
            Debug.Log($"[CombatController] 이번 턴 추가 드로우 +{amount}");
        }

        /// <summary>
        /// 201 유물: 전투마다 처음으로 데미지를 주는 효과에 +amount 피해.
        /// 카드 타입에 무관하게 첫 Damage 효과 1회에만 적용됩니다.
        /// 여러 유물이 동시에 호출하면 누적됩니다.
        /// </summary>
        public void AddFirstActionBonusDamage(int amount)
        {
            _firstActionBonusDamage += amount;
            Debug.Log($"[CombatController] 첫 데미지 추가 피해 +{amount} (총: {_firstActionBonusDamage})");
        }

        /// <summary>
        /// 이번 전투의 시작 시 뽑는 카드에 지정 카드를 끼워넣습니다 (유물 201~204).
        /// 드로우 더미 맨 위에 카드 클론을 삽입합니다 — 이번 전투에서만 유효하며,
        /// 다음 전투의 ResetForNewCombat에서 자동으로 사라집니다.
        /// 반드시 OnCombatStart 훅에서 호출하세요 (InitializeDeck 이후, StartPlayerTurn 이전).
        /// </summary>
        public void AddStartingDrawCard(DeckRoguelike.Cards.CardData card)
        {
            if (card == null || deckManager == null) return;
            deckManager.AddToTopOfDrawPile(card.Clone());
            Debug.Log($"[CombatController] 전투 시작 손패 카드 추가: {card.cardName}");
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
                    cardUI.Initialize(hand[i], playerStrength, playerDexterity);
            }
        }

        // ── 아이템 전용 전투 API ─────────────────────────────────────────

        /// <summary>범위 내 모든 적에게 피해를 줍니다 (폭탄 등).</summary>
        public void DealDamageInArea(Vector2Int center, int halfRange, int damage)
        {
            var hit = new HashSet<EnemyInstance>();
            for (int dc = -halfRange; dc <= halfRange; dc++)
                for (int dr = -halfRange; dr <= halfRange; dr++)
                {
                    var pos = center + new Vector2Int(dc, dr);
                    if (!IsInBoard(pos)) continue;
                    var e = grid[pos.x, pos.y].OccupyingEnemy;
                    if (e != null && e.CurrentHP > 0 && hit.Add(e))
                        DamageEnemy(e, damage);
                }
            CheckVictory();
        }

        /// <summary>지정한 셀 목록의 적들에게 피해를 줍니다. 멀티셀 적이 여러 셀에 걸쳐있어도
        /// EnemyInstance 단위로 중복을 제거해 한 번만 데미지를 적용합니다.</summary>
        public void DealDamageAtCells(IEnumerable<Vector2Int> cells, int damage)
        {
            if (cells == null) return;
            var hit = new HashSet<EnemyInstance>();
            foreach (var pos in cells)
            {
                if (!IsInBoard(pos)) continue;
                var e = grid[pos.x, pos.y].OccupyingEnemy;
                if (e != null && e.CurrentHP > 0 && hit.Add(e))
                    DamageEnemy(e, damage);
            }
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
                var card = ResolveOriginalCard(hand[idx]);
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

        /// <summary>손패에서 무작위로 count장을 버림 더미로 보냅니다.</summary>
        public void DiscardRandomHandCard(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (hand.Count == 0) break;
                int idx = UnityEngine.Random.Range(0, hand.Count);
                var card = ResolveOriginalCard(hand[idx]);
                hand.RemoveAt(idx);
                if (idx < handCardObjects.Count)
                {
                    var obj = handCardObjects[idx];
                    handCardObjects.RemoveAt(idx);
                    if (obj != null) Destroy(obj);
                }
                deckManager?.AddToDiscardPile(card);
            }
            RepositionHandCards();
            UpdatePileCounters();
        }

        /// <summary>ChooseCardPanel에서 선택된 카드에 소멸 키워드를 부여합니다 (카드는 손에 남음).</summary>
        public void MarkCardExhaust(DeckRoguelike.Cards.CardUI cardUI)
        {
            var cardData = cardUI.CardData;
            if (cardData != null)
            {
                cardData.keywords |= DeckRoguelike.Cards.CardKeyword.Exhausts;
                cardUI.RefreshText();
            }
        }

        /// <summary>ChooseCardPanel에서 선택된 카드를 소멸 처리합니다.</summary>
        public void ExhaustChosenCard(DeckRoguelike.Cards.CardUI cardUI)
        {
            var cardData = cardUI.CardData;
            int idx = handCardObjects.IndexOf(cardUI.gameObject);
            if (idx >= 0)
            {
                hand.RemoveAt(idx);
                handCardObjects.RemoveAt(idx);
            }
            else
            {
                int handIdx = cardData != null ? hand.IndexOf(cardData) : -1;
                if (handIdx >= 0)
                    hand.RemoveAt(handIdx);
            }

            if (cardData != null)
            {
                var original = ResolveOriginalCard(cardData);
                if (_noExhaust)
                    deckManager?.AddToDiscardPile(original);
                else
                {
                    deckManager?.ExhaustCard(original);
                    foreach (var p in _activePowers) p.OnCardExhausted(this);
                }
            }

            if (cardUI.gameObject != null) Destroy(cardUI.gameObject);
            UpdatePileCounters();
        }

        /// <summary>ChooseCardPanel에서 선택된 카드를 버림 더미로 처리합니다.</summary>
        public void DiscardChosenCard(DeckRoguelike.Cards.CardUI cardUI)
        {
            var cardData = cardUI.CardData;
            int idx = handCardObjects.IndexOf(cardUI.gameObject);
            if (idx >= 0)
            {
                hand.RemoveAt(idx);
                handCardObjects.RemoveAt(idx);
            }
            else
            {
                int handIdx = cardData != null ? hand.IndexOf(cardData) : -1;
                if (handIdx >= 0)
                    hand.RemoveAt(handIdx);
            }

            if (cardData != null)
            {
                var original = ResolveOriginalCard(cardData);
                deckManager?.AddToDiscardPile(original);
            }

            if (cardUI.gameObject != null) Destroy(cardUI.gameObject);
            UpdatePileCounters();
        }

        /// <summary>Exhausts/Discard 이펙트로 카드 선택 패널을 엽니다.</summary>
        public void OpenChooseCardPanel(int maxCount, bool isExhaust)
        {
            if (chooseCardPanel == null) return;

            // 남은 손패가 value 이하면 패널 없이 전부 처리
            if (handCardObjects.Count <= maxCount)
            {
                var all = new List<DeckRoguelike.Cards.CardUI>();
                foreach (var obj in handCardObjects)
                {
                    if (obj == null) continue;
                    var cui = obj.GetComponent<DeckRoguelike.Cards.CardUI>();
                    if (cui != null) all.Add(cui);
                }
                foreach (var cui in all)
                {
                    if (isExhaust) ExhaustChosenCard(cui);
                    else           DiscardChosenCard(cui);
                }
                return;
            }

            _isChooseCardMode = true;
            // endTurn만 비활성화, draw/discard 버튼은 유지
            SetButtonRaycast(endTurnButton, false);
            var mode = isExhaust ? ChoosePanelMode.Exhaust : ChoosePanelMode.Discard;
            chooseCardPanel.Open(this, handContainer, handCardObjects, maxCount, mode);
        }

        /// <summary>
        /// 가변 선택 수 버전의 Exhaust/Discard 패널. 원하는 수만큼(0~maxCount) 선택 가능.
        /// onConfirmed에 실제 처리된 카드 수가 전달됩니다 (가위 아이템 등).
        /// </summary>
        public void OpenChooseCardPanelWithCallback(int maxCount, bool isExhaust,
                                                    System.Action<int> onConfirmed)
        {
            if (chooseCardPanel == null) { onConfirmed?.Invoke(0); return; }
            if (handCardObjects.Count == 0) { onConfirmed?.Invoke(0); return; }

            _isChooseCardMode = true;
            SetButtonRaycast(endTurnButton, false);
            var mode = isExhaust ? ChoosePanelMode.Exhaust : ChoosePanelMode.Discard;
            int upper = Mathf.Min(maxCount, handCardObjects.Count);
            chooseCardPanel.Open(this, handContainer, handCardObjects, upper, mode,
                                 allowFewerSelection: true, onConfirmedCallback: onConfirmed);
        }

        /// <summary>원하는 만큼 손패 카드를 선택해 소멸 키워드를 부여합니다 (카드는 손에 남음).</summary>
        public void OpenChooseCardPanelForMarkExhaust(int maxCount, System.Action<int> onConfirmed)
        {
            if (chooseCardPanel == null) { onConfirmed?.Invoke(0); return; }
            if (handCardObjects.Count == 0) { onConfirmed?.Invoke(0); return; }

            _isChooseCardMode = true;
            SetButtonRaycast(endTurnButton, false);
            int upper = Mathf.Min(maxCount, handCardObjects.Count);
            chooseCardPanel.Open(this, handContainer, handCardObjects, upper,
                                 DeckRoguelike.UI.ChoosePanelMode.MarkExhaust,
                                 allowFewerSelection: true, onConfirmedCallback: onConfirmed);
        }

        public void MoveHandCardToLast(GameObject cardObj)
        {
            int idx = handCardObjects.IndexOf(cardObj);
            if (idx < 0 || idx >= handCardObjects.Count - 1) return;
            var cardData = idx < hand.Count ? hand[idx] : null;

            handCardObjects.RemoveAt(idx);
            handCardObjects.Add(cardObj);

            if (cardData != null && idx < hand.Count)
            {
                hand.RemoveAt(idx);
                hand.Add(cardData);
            }
        }

        /// <summary>ChooseCardPanel 확정/닫힘 시 호출됩니다.</summary>
        public void OnChooseCardPanelClosed()
        {
            _isChooseCardMode = false;
            SetCombatButtonsInteractable(true);
            RepositionHandCards();
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

        /// <summary>
        /// 짧은 텀 후 적에게 amount 피해를 한 번 적용합니다 (소매치기 등 OnEnemyDamaged 후속 피해 전용).
        /// 적용된 피해는 OnEnemyDamaged 훅을 다시 발동시키지 않습니다.
        /// </summary>
        public void StartFollowUpDamage(EnemyInstance enemy, int amount, float delay)
        {
            StartCoroutine(FollowUpDamageCoroutine(enemy, amount, delay));
        }

        private IEnumerator FollowUpDamageCoroutine(EnemyInstance enemy, int amount, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (enemy == null || enemy.CurrentHP <= 0 || amount <= 0) yield break;

            _suppressOnEnemyDamaged = true;
            try { DamageEnemy(enemy, amount); }
            finally { _suppressOnEnemyDamaged = false; }
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

        /// <summary>매 타격마다 살아있는 적 중 무작위 1명에게 dmgPerHit 피해를 times번 적용합니다 (칼던지기).</summary>
        public void StartRandomEnemyDamage(int dmgPerHit, int times, float delay = 0.15f)
        {
            StartCoroutine(RandomEnemyDamageCoroutine(dmgPerHit, times, delay));
        }

        private IEnumerator RandomEnemyDamageCoroutine(int dmgPerHit, int times, float delay)
        {
            for (int i = 0; i < times; i++)
            {
                var alive = new List<EnemyInstance>();
                foreach (var e in enemies) if (e.CurrentHP > 0) alive.Add(e);
                if (alive.Count == 0) yield break;
                var target = alive[UnityEngine.Random.Range(0, alive.Count)];
                DamageEnemy(target, dmgPerHit);
                if (i < times - 1)
                    yield return new WaitForSeconds(delay);
            }
        }

        /// <summary>
        /// 치킨게임: 적이 처치될 때까지 매 반복마다 dmgPerHit 피해를 주고 플레이어 채력 hpLoss를 잃습니다.
        /// 무한 루프 방지 — 최대 30회 반복 후 종료.
        /// 플레이어 채력이 1 이하가 되면 즉시 중단.
        /// </summary>
        public void StartUntilKillLoseHp(EnemyInstance enemy, int dmgPerHit, int hpLoss, float delay = 0.15f)
        {
            StartCoroutine(UntilKillLoseHpCoroutine(enemy, dmgPerHit, hpLoss, delay));
        }

        private IEnumerator UntilKillLoseHpCoroutine(EnemyInstance enemy, int dmgPerHit, int hpLoss, float delay)
        {
            const int safetyCap = 30;
            for (int i = 0; i < safetyCap; i++)
            {
                if (enemy == null || enemy.CurrentHP <= 0) yield break;

                DamageEnemy(enemy, dmgPerHit);

                if (hpLoss > 0 && DeckRoguelike.Core.GameManager.Instance != null)
                {
                    int curHp = DeckRoguelike.Core.GameManager.Instance.CurrentHP;
                    int actual = Mathf.Min(hpLoss, Mathf.Max(0, curHp - 1));
                    if (actual > 0)
                    {
                        DeckRoguelike.Core.GameManager.Instance.Heal(-actual);
                        FirePowerOnPlayerLostHp(actual);
                    }
                    if (DeckRoguelike.Core.GameManager.Instance.CurrentHP <= 1) yield break;
                }

                if (enemy.CurrentHP <= 0) yield break;
                yield return new WaitForSeconds(delay);
            }
        }

        /// <summary>
        /// CardData.imagePath 값을 Addressable Sprite로 해석합니다.
        /// '/'가 포함되어 있으면 그대로 키로 사용, 아니면 'Sprites/Combat/Player/{path}'.
        /// `[subSpriteName]` 표기로 서브스프라이트 직접 지정 가능.
        /// 단일 Sprite → 명시 서브스프라이트 → IList<Sprite> → '_0' 자동추정 순으로 폴백.
        /// 실패 시 null 반환.
        /// </summary>
        public Sprite ResolveCardImageSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // 'name[sub]' 표기 분리
            string subName = null;
            int br = path.IndexOf('[');
            if (br >= 0 && path.EndsWith("]"))
            {
                subName = path.Substring(br + 1, path.Length - br - 2);
                path = path.Substring(0, br);
            }

            string key = path.Contains("/") ? path : $"Sprites/Ingame/Combat/Player/{path}";

            // 1) 단일 Sprite 로드 시도 — multi-sprite면 null 반환되며, 예외 아닌 경우가 있음
            var s = TryLoadAddressableSprite(key);
            if (s != null) return s;

            // 2) 명시된 서브스프라이트명이 있으면 'key[name]' 키로 직접 시도
            if (!string.IsNullOrEmpty(subName))
            {
                s = TryLoadAddressableSprite($"{key}[{subName}]");
                if (s != null) return s;
            }

            // 3) Multi-sprite 시도 — 모든 서브스프라이트 로드 후 이름 매칭(없으면 첫 번째)
            try
            {
                var op = Addressables.LoadAssetAsync<IList<Sprite>>(key);
                var list = op.WaitForCompletion();
                if (list != null && list.Count > 0)
                {
                    if (!string.IsNullOrEmpty(subName))
                    {
                        for (int i = 0; i < list.Count; i++)
                            if (list[i] != null && list[i].name == subName) return list[i];
                    }
                    return list[0];
                }
            }
            catch { }

            // 4) 자동 슬라이스 기본 이름 '_0' 추정 시도
            string nameOnly = path.Contains("/") ? path.Substring(path.LastIndexOf('/') + 1) : path;
            return TryLoadAddressableSprite($"{key}[{nameOnly}_0]");
        }

        private static Sprite TryLoadAddressableSprite(string key)
        {
            try
            {
                var op = Addressables.LoadAssetAsync<Sprite>(key);
                return op.WaitForCompletion();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 손패의 카드(플레이 중인 카드 제외) 중 적을 처치하기에 필요한 만큼만(왼쪽부터) 칼로 변환해
        /// 한 발씩 적에게 발사하고, 나머지 카드는 일반 버림더미로 보냅니다.
        /// 칼 한 발의 피해 = baseDmg. 칼 수 = ceil(enemy.CurrentHP / baseDmg), 손패 카드 수로 클램프.
        /// </summary>
        public void StartExhaustAllAndDamage(EnemyInstance enemy, int baseDmg, CardData projectileCard = null, float delay = 0.12f)
        {
            int skipIdx = _playingHandIndex;

            // 손패 카드를 왼쪽→오른쪽 순서로 수집 (원본 인덱스 보존)
            var allCards = new List<(CardData card, GameObject obj, int origIdx)>();
            for (int i = 0; i < hand.Count; i++)
            {
                if (i == skipIdx) continue;
                var obj = i < handCardObjects.Count ? handCardObjects[i] : null;
                allCards.Add((hand[i], obj, i));
            }

            // 적 처치에 필요한 칼 수 — 손패 카드 수로 클램프
            int dmgPerKnife = Mathf.Max(1, baseDmg);
            int knivesNeeded;
            if (enemy == null || enemy.CurrentHP <= 0)
                knivesNeeded = 0;
            else
                knivesNeeded = Mathf.Min(allCards.Count, Mathf.CeilToInt(enemy.CurrentHP / (float)dmgPerKnife));

            // 왼쪽부터 knivesNeeded 만큼은 칼, 나머지는 버림
            var knifeCards = new List<(CardData card, GameObject obj, int origIdx)>(knivesNeeded);
            var discardCards = new List<(CardData card, GameObject obj, int origIdx)>(Mathf.Max(0, allCards.Count - knivesNeeded));
            for (int i = 0; i < allCards.Count; i++)
            {
                if (i < knivesNeeded) knifeCards.Add(allCards[i]);
                else discardCards.Add(allCards[i]);
            }

            // 데이터 즉시 처리 (역순) — 칼 카드는 소멸, 나머지는 버림더미로 (Random_*Card는 원본 복원)
            var knifeIdxSet = new HashSet<int>();
            foreach (var t in knifeCards) knifeIdxSet.Add(t.origIdx);

            for (int i = hand.Count - 1; i >= 0; i--)
            {
                if (i == skipIdx) continue;
                var card = ResolveOriginalCard(hand[i]);
                bool isKnife = knifeIdxSet.Contains(i);
                if (_noExhaust || !isKnife)
                {
                    deckManager?.AddToDiscardPile(card);
                }
                else
                {
                    deckManager?.ExhaustCard(card);
                    foreach (var p in _activePowers) p.OnCardExhausted(this);
                }
                hand.RemoveAt(i);
                if (i < handCardObjects.Count)
                    handCardObjects.RemoveAt(i);
            }

            if (skipIdx >= 0 && hand.Count > 0)
                _playingHandIndex = 0;

            UpdatePileCounters();
            RepositionHandCards();

            int finalDmg = dmgPerKnife * knifeCards.Count;

            // 카드의 imagePath가 우선, 없으면 인스펙터 기본 (knifeProjectileSprite)
            Sprite projectile = ResolveCardImageSprite(projectileCard?.imagePath) ?? knifeProjectileSprite;

            StartCoroutine(ExhaustAllAndDamageCoroutine(enemy, finalDmg, knifeCards, discardCards, delay, projectile));
        }

        /// <summary>
        /// 칼 카드는 변신·발사하며 한 발마다 자기 몫 데미지 적용, 그 외 카드는 일반 버림더미로 보냄.
        /// projectileSprite 미설정/적 부재 시 모두 일반 버림으로 폴백.
        /// </summary>
        private IEnumerator ExhaustAllAndDamageCoroutine(EnemyInstance enemy, int finalDmg,
            List<(CardData card, GameObject obj, int origIdx)> knifeCards,
            List<(CardData card, GameObject obj, int origIdx)> discardCards,
            float delay, Sprite projectileSprite)
        {
            // 스프라이트 미연결/적 부재 시 — 모두 일반 버림 처리
            if (projectileSprite == null || enemy?.GameObject == null)
            {
                Vector3 discardPos = discardPileButton != null
                    ? discardPileButton.transform.position
                    : Vector3.zero;

                void DiscardOne(GameObject obj)
                {
                    if (obj == null) return;
                    var ui = obj.GetComponent<CardUI>();
                    if (ui != null && discardPileButton != null)
                        StartCoroutine(DiscardAndDestroy(ui, discardPos));
                    else
                        Destroy(obj);
                }

                foreach (var t in knifeCards) { DiscardOne(t.obj); yield return new WaitForSeconds(delay); }
                foreach (var t in discardCards) { DiscardOne(t.obj); yield return new WaitForSeconds(delay); }

                if (enemy != null && enemy.CurrentHP > 0)
                    DamageEnemy(enemy, finalDmg);
                yield break;
            }

            // 칼이 아닌 카드들은 즉시 일반 버림 애니로 (칼 비행과 병렬 진행)
            if (discardPileButton != null)
            {
                Vector3 discardPos = discardPileButton.transform.position;
                foreach (var t in discardCards)
                {
                    if (t.obj == null) continue;
                    var ui = t.obj.GetComponent<CardUI>();
                    if (ui != null)
                        StartCoroutine(DiscardAndDestroy(ui, discardPos));
                    else
                        Destroy(t.obj);
                }
            }
            else
            {
                foreach (var t in discardCards)
                    if (t.obj != null) Destroy(t.obj);
            }

            // Phase 1: 칼이 될 카드들을 일괄적으로 칼 스프라이트로 변신
            var validUis = new List<CardUI>(knifeCards.Count);
            foreach (var t in knifeCards)
            {
                if (t.obj == null) continue;
                var ui = t.obj.GetComponent<CardUI>();
                if (ui == null)
                {
                    Destroy(t.obj);
                    continue;
                }
                ui.TransformToKnifeProjectile(projectileSprite);
                validUis.Add(ui);
            }

            int n = validUis.Count;
            if (n == 0)
            {
                if (enemy != null && enemy.CurrentHP > 0)
                    DamageEnemy(enemy, finalDmg);
                yield break;
            }

            // Phase 2: 변신이 끝난 칼이 손패에 머무는 짧은 대기 — 사용자에게 변신 시각 신호
            if (knifeTransformDelay > 0f)
                yield return new WaitForSeconds(knifeTransformDelay);

            // Phase 3: 한 발씩 발사 — 각 칼이 적에게 도달할 때마다 자기 몫 데미지를 적용
            int dmgPerHit = finalDmg / n;
            int remainder = finalDmg - dmgPerHit * n;
            float interval = Mathf.Max(0f, knifeProjectileInterval);

            for (int i = 0; i < n; i++)
            {
                var ui = validUis[i];
                if (ui == null)
                {
                    if (interval > 0f && i < n - 1) yield return new WaitForSeconds(interval);
                    continue;
                }

                Vector3 targetWorld = (enemy?.GameObject != null)
                    ? enemy.GameObject.transform.position
                    : (discardPileButton != null ? discardPileButton.transform.position : Vector3.zero);

                int thisDmg = (i == n - 1) ? dmgPerHit + remainder : dmgPerHit;
                StartCoroutine(KnifeFlyAndApplyDamage(ui, targetWorld, knifeProjectileDuration, knifeProjectileScale, enemy, thisDmg));
                if (interval > 0f && i < n - 1) yield return new WaitForSeconds(interval);
            }
        }

        /// <summary>변신된 카드를 적까지 날린 뒤 도달 시점에 dmg 만큼 피해 적용 후 파괴.</summary>
        private IEnumerator KnifeFlyAndApplyDamage(CardUI ui, Vector3 targetWorld, float duration, float scale,
            EnemyInstance enemy, int dmg)
        {
            yield return ui.FlyAsKnifeProjectile(targetWorld, duration, scale);
            if (dmg > 0 && enemy != null && enemy.CurrentHP > 0)
                DamageEnemy(enemy, dmg);
            if (ui != null && ui.gameObject != null)
                Destroy(ui.gameObject);
        }

        /// <summary>
        /// 직업 시작/보스 유물(70/970/90/990)이 호출하는 카드 지급 헬퍼.
        /// 지정 코드의 카드를 복제해 손패에 추가하고, extraKeywords 플래그를 추가로 부여합니다.
        /// </summary>
        public void GrantClassRelicCard(int cardCode, DeckRoguelike.Cards.CardKeyword extraKeywords = DeckRoguelike.Cards.CardKeyword.None)
        {
            if (cardCode == 0) return;

            var card = CardRegistry.GetCard(cardCode);
            if (card == null)
            {
                Debug.LogWarning($"[BoardController] 직업 유물 카드({cardCode})를 찾을 수 없습니다.");
                return;
            }

            if (extraKeywords == DeckRoguelike.Cards.CardKeyword.None)
            {
                int before = hand.Count;
                AddCardToHandFree(card);
                if (_grantingCombatStartCards && hand.Count > before) _combatStartBonusCards++;
                return;
            }

            if (hand.Count >= 10) return;
            var free = card.Clone();
            free.keywords |= extraKeywords;
            hand.Add(free);
            var obj = CreateSingleCardObject(free);
            if (obj != null) handCardObjects.Add(obj);
            RepositionHandCards();
            UpdatePileCounters();
            if (_grantingCombatStartCards) _combatStartBonusCards++;
        }

        /// <summary>카드를 손패에 추가합니다. 비용이 0인 복사본으로 추가됩니다.</summary>
        public void AddCardToHandFree(DeckRoguelike.Cards.CardData card)
        {
            if (card == null || hand.Count >= 10) return;
            // 포션 등으로 카드가 손패에 추가될 때 다른 카드가 sticky로 떠 있으면, sticky 상태/인덱스가
            // 꼬여 여러 카드가 동시에 sticky되는 버그가 발생한다. 추가 전에 깨끗이 해제한다.
            // (카드 효과 실행 중 AddCardToHandFree는 이미 CancelTargeting을 거쳐 AnyCardStickyActive=false이므로 영향 없음)
            if (AnyCardStickyActive) ReleaseStickyCardIfAny();
            var free = card.Clone();
            hand.Add(free);
            var obj = CreateSingleCardObject(free);
            if (obj != null) handCardObjects.Add(obj);
            RepositionHandCards();
            UpdatePileCounters();
        }

        /// <summary>선택형 이벤트의 선택지 카드를 손패에 추가합니다. 손패에 등록한 클론을 _rewardCards에도 넣어
        /// MapState/Shop/Rest/Treasure 등 비전투 상태의 OnHandCardClicked 분기가 ExecuteCard 흐름으로
        /// 진입할 수 있게 합니다. 사용 후 ExecuteCard가 _rewardCards에서 자동 제거합니다.</summary>
        public void AddRewardCardToHandFree(DeckRoguelike.Cards.CardData card)
        {
            if (card == null || hand.Count >= 10) return;
            var free = card.Clone();
            hand.Add(free);
            _rewardCards.Add(free);
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
            hand[idx] = free;
            if (idx < handCardObjects.Count)
                handCardObjects[idx]?.GetComponent<DeckRoguelike.Cards.CardUI>()
                    ?.Initialize(free, playerStrength, playerDexterity);
            RefreshCardPlayability();
        }

        /// <summary>손패의 모든 카드를 영구 강화합니다 (아이템: 붕대 등).</summary>
        public void UpgradeAllHandCards()
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i].IsUpgraded) continue;
                var upgraded = DeckRoguelike.Core.CardRegistry.GetCard(hand[i].UpgradedCode);
                if (upgraded == null) continue;
                var clone = upgraded.Clone();
                deckManager?.UpgradeCard(hand[i]);
                hand[i] = clone;
                if (i < handCardObjects.Count)
                    handCardObjects[i]?.GetComponent<DeckRoguelike.Cards.CardUI>()
                        ?.Initialize(clone, playerStrength, playerDexterity);
            }
            RefreshCardPlayability();
        }

        /// <summary>
        /// Card_Upgrade:0 (붕대) — 손패의 모든 카드를 임시 강화합니다.
        /// 전투 종료 시 RevertTemporaryUpgrades에서 자동으로 원래 카드로 복원됩니다.
        /// </summary>
        public void UpgradeAllHandCardsTemporary()
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i].IsUpgraded) continue;
                var upgradedDef = DeckRoguelike.Core.CardRegistry.GetCard(hand[i].UpgradedCode);
                if (upgradedDef == null) continue;
                var clone = upgradedDef.Clone();
                deckManager?.UpgradeCard(hand[i]);
                hand[i] = clone;
                _temporarilyUpgradedCards.Add(clone);
                if (i < handCardObjects.Count)
                    handCardObjects[i]?.GetComponent<DeckRoguelike.Cards.CardUI>()
                        ?.Initialize(clone, playerStrength, playerDexterity);
            }
            RefreshCardPlayability();
        }

        /// <summary>
        /// Card_Upgrade:1 (붕대+) — 덱(마스터/드로우/버림/소멸/손패) 모든 카드를 임시 강화합니다.
        /// 전투 종료 시 RevertTemporaryUpgrades에서 자동으로 원래 카드로 복원됩니다.
        /// </summary>
        public void UpgradeAllDeckCardsTemporary()
        {
            if (deckManager == null) return;

            // 손패 먼저 강화 (deckManager.UpgradeCard가 내부 더미만 갱신하므로 hand는 별도 처리 필요)
            UpgradeAllHandCardsTemporary();

            // 마스터 덱 스냅샷 후, 강화 안 된 모든 카드를 강화 (UpgradeCard는 master/draw/discard 동기화)
            var snapshot = deckManager.MasterDeck;
            foreach (var card in snapshot)
            {
                if (card.IsUpgraded) continue;
                var upgradedDef = DeckRoguelike.Core.CardRegistry.GetCard(card.UpgradedCode);
                if (upgradedDef == null) continue;
                int upgradedCode = card.UpgradedCode;
                deckManager.UpgradeCard(card);
                // UpgradeCard 내부에서 새로 만든 클론을 직접 받을 방법이 없어 마스터에서 재조회
                var newInst = deckManager.MasterDeck.FirstOrDefault(c => c.cardCode == upgradedCode);
                if (newInst != null && !_temporarilyUpgradedCards.Contains(newInst))
                    _temporarilyUpgradedCards.Add(newInst);
            }
        }

        // ── 위해(Hazard) 시스템 ────────────────────────────────────────────
        // CombatBoardCell의 OccupyingTrap(설치)/OccupyingZone(생성) 슬롯에 HazardInstance가 분리 저장되고,
        // Trap kind: 플레이어·아군이 밟으면 진입 즉시 TryTriggerHazard 호출 → HazardLibrary.Execute 발동 후 제거.
        //            적은 진입 즉시 발동하지 않고, 적 턴 종료시 ProcessTrapEndOfEnemyTurn 이 트랩 위의 적에게
        //            일괄 발동시킨 뒤 제거한다 (발동 타이밍 단일화).
        // Zone kind: 진입 시에는 발동하지 않고 적 턴 종료시 ProcessZoneEndOfEnemyTurn 이
        //            셀 위 유닛에게 효과를 반복 적용한다 (사라지지 않음).

        public int BoardColumns => boardCols;
        public int BoardRows    => boardRows;

        /// <summary>지정 셀에 위해를 배치합니다. kind에 따라 Trap/Zone 슬롯이 분리되어
        /// 한 셀에 설치(Trap)와 생성(Zone)이 공존할 수 있습니다. 같은 종류가 이미 있으면 새 것으로 교체.
        /// 설치(Trap)만 설치 카운터를 증가시키고 OnTrapPlaced 훅(설치가속/발사카드 파워)을 발동합니다.</summary>
        public void EmplaceHazard(HazardData data, Vector2Int pos, int value)
        {
            if (data == null) return;
            if (!IsInBoard(pos)) return;
            var cell = grid[pos.x, pos.y];
            var inst = new HazardInstance(data, pos, value);

            if (data.kind == HazardKind.Zone)
            {
                // 생성(Zone): 기존 지대가 있으면 시각 오브젝트 제거 후 교체
                if (cell.OccupyingZone?.VisualObject != null)
                    Destroy(cell.OccupyingZone.VisualObject);
                cell.OccupyingZone = inst;
                // 용암/빙하지대 등은 셀 배경 타일 스프라이트를 교체해 시각 표시.
                var zoneSprite = GetZoneTileSprite(data.hazardCode);
                if (zoneSprite != null) cell.SetZoneSprite(zoneSprite);
                Debug.Log($"[Hazard] 생성 — {data.hazardName}({data.hazardCode}) @ {pos} value={value}");
            }
            else
            {
                // 설치(Trap): 기존 함정이 있으면 시각 오브젝트 제거 후 교체 (중복 설치 = 덮어쓰기)
                if (cell.OccupyingTrap?.VisualObject != null)
                    Destroy(cell.OccupyingTrap.VisualObject);
                cell.OccupyingTrap = inst;
                Debug.Log($"[Hazard] 설치 — {data.hazardName}({data.hazardCode}) @ {pos} value={value}");

                // 설치 카운터 증가 → OnTrapPlaced 파워 훅 (설치가속/발사카드 등). 생성(Zone)은 제외.
                IncrementTrapCount();
            }
        }

        /// <summary>지정 셀의 설치(Trap) 위해를 강제 제거합니다 (발동 후 또는 셀 정리 시). 생성(Zone)은 유지.</summary>
        public void RemoveTrap(Vector2Int pos)
        {
            if (!IsInBoard(pos)) return;
            var cell = grid[pos.x, pos.y];
            if (cell.OccupyingTrap == null) return;
            if (cell.OccupyingTrap.VisualObject != null)
                Destroy(cell.OccupyingTrap.VisualObject);
            cell.OccupyingTrap = null;
        }

        /// <summary>지정 셀의 생성(Zone) 위해를 강제 제거합니다.</summary>
        public void RemoveZone(Vector2Int pos)
        {
            if (!IsInBoard(pos)) return;
            var cell = grid[pos.x, pos.y];
            if (cell.OccupyingZone == null) return;
            if (cell.OccupyingZone.VisualObject != null)
                Destroy(cell.OccupyingZone.VisualObject);
            cell.OccupyingZone = null;
            cell.ClearZoneSprite();   // 지대 제거 시 원본 배경 타일 복원
        }

        /// <summary>
        /// 셀의 Trap kind 위해를 진입한 유닛 진영과 무관하게 발동시키고 제거합니다.
        /// 플레이어·아군은 진입 즉시 이 메서드로 발동하지만, 적은 진입 즉시가 아니라
        /// 적 턴 종료시 ProcessTrapEndOfEnemyTurn 이 이 메서드를 호출해 일괄 발동시킨다.
        /// Zone kind는 진입 시 발동하지 않으며, 적 턴 종료시 ProcessZoneEndOfEnemyTurn 에서 처리합니다.
        /// 적/아군이 밟은 경우 movingEnemy/movingAlly를 넘기면 곰덫/점프대 등에서 사용됩니다.
        /// </summary>
        public void TryTriggerHazard(Vector2Int pos, HazardTriggerSource source,
                                   EnemyInstance movingEnemy = null,
                                   AllyInstance  movingAlly  = null)
        {
            if (!IsInBoard(pos)) return;
            var cell = grid[pos.x, pos.y];
            // 진입 시 발동은 설치(Trap)만. 생성(Zone)은 적 턴 종료시 ProcessZoneEndOfEnemyTurn에서 처리.
            var hazard = cell.OccupyingTrap;
            if (hazard?.Data == null) return;

            // hover 미리보기 정리 (발동 셀에 표시 중이던 미리보기 제거)
            if (_hoveredHazard == hazard) ClearHazardHoverPreview();

            var ctx = new HazardTriggerContext
            {
                Hazard       = hazard,
                Board        = this,
                Source       = source,
                TriggerEnemy = source == HazardTriggerSource.Enemy ? movingEnemy : null,
                TriggerAlly  = source == HazardTriggerSource.Ally  ? movingAlly  : null,
            };

            Debug.Log($"[Hazard] 발동 — {hazard.Data.hazardName}({hazard.Data.hazardCode}) by {source} @ {pos}");
            HazardLibrary.Execute(hazard.Data.hazardCode, ctx);

            // Trap kind: 발동 후 제거
            RemoveTrap(pos);
        }

        /// <summary>
        /// 적 턴 종료시 호출 — 설치(Trap) kind 위해가 깔린 모든 셀을 순회하며,
        /// 그 위에 적이 서 있으면 트랩을 발동하고 제거합니다(1회성).
        /// 적이 밟는 즉시가 아니라 모든 적의 공격·이동이 끝난 이 시점에만 발동시켜
        /// 발동 타이밍을 단일화합니다. (플레이어·아군이 밟는 트랩은 진입 즉시 발동 — 기존 경로 유지)
        /// </summary>
        public void ProcessTrapEndOfEnemyTurn()
        {
            if (grid == null) return;
            int w = grid.GetLength(0), h = grid.GetLength(1);

            // 발동 도중 트랩 제거(점프대 등)·적 이동/사망으로 그리드가 바뀔 수 있으므로
            // 트랩이 깔린 셀 좌표를 먼저 스냅샷한 뒤 순회한다.
            var trapCells = new List<Vector2Int>();
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (grid[x, y]?.OccupyingTrap?.Data != null)
                    trapCells.Add(new Vector2Int(x, y));
            }

            foreach (var pos in trapCells)
            {
                var cell = grid[pos.x, pos.y];
                var enemy = cell?.OccupyingEnemy;
                if (enemy == null || enemy.CurrentHP <= 0) continue;
                // TryTriggerHazard가 설치 위해를 발동하고 셀에서 제거한다.
                TryTriggerHazard(pos, HazardTriggerSource.Enemy, movingEnemy: enemy);
            }
        }

        /// <summary>
        /// 적 턴 종료시 호출 — Zone kind 위해가 설치된 모든 셀을 순회하며,
        /// 그 위에 있는 유닛(적/아군/플레이어)에게 효과를 1회 적용합니다.
        /// 한 셀에 한 유닛만 매핑되므로 적 > 아군 > 플레이어 우선순위로 발동.
        /// Zone은 사라지지 않고 다음 적턴 종료 때도 동일하게 다시 발동됩니다.
        /// </summary>
        public void ProcessZoneEndOfEnemyTurn()
        {
            if (grid == null) return;
            int w = grid.GetLength(0), h = grid.GetLength(1);
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var cell = grid[x, y];
                var hazard = cell?.OccupyingZone;
                if (hazard?.Data == null) continue;

                if (cell.OccupyingEnemy != null && cell.OccupyingEnemy.CurrentHP > 0)
                {
                    HazardLibrary.Execute(hazard.Data.hazardCode, new HazardTriggerContext
                    {
                        Hazard = hazard, Board = this,
                        Source = HazardTriggerSource.Enemy, TriggerEnemy = cell.OccupyingEnemy,
                    });
                }
                else if (cell.OccupyingAlly != null && cell.OccupyingAlly.CurrentHP > 0)
                {
                    HazardLibrary.Execute(hazard.Data.hazardCode, new HazardTriggerContext
                    {
                        Hazard = hazard, Board = this,
                        Source = HazardTriggerSource.Ally, TriggerAlly = cell.OccupyingAlly,
                    });
                }
                else if (cell.IsPlayerHere)
                {
                    HazardLibrary.Execute(hazard.Data.hazardCode, new HazardTriggerContext
                    {
                        Hazard = hazard, Board = this,
                        Source = HazardTriggerSource.Player,
                    });
                }
            }
        }

        /// <summary>HazardLibrary 등 외부에서 사용할 수 있도록 노출한 플레이어 텔레포트 진입점.</summary>
        public void TeleportPlayerPublic(Vector2Int newPos) => TeleportPlayer(newPos);

        /// <summary>
        /// ShootRange (33100/33101) — 전역 발사 사거리(s_playerShootRange)를 levels만큼 증가시키고,
        /// masterDeck 내 모든 발사 공격 카드(Shoot/Allrange_Shoot/6times_Shoot)의 rangeOffsets를
        /// BFS(s_playerShootRange) 셀로 갱신한다. 키워드가 아닌 customEffectId로 발사카드 판별.
        /// 전투 종료 시 RestoreShootCardRanges가 원본(=CSV의 빈 offsets)으로 복원.
        /// </summary>
        public void IncreaseShootCardRange(int levels)
        {
            if (levels <= 0) return;
            s_playerShootRange += levels;
            RefreshShootCardRanges();

            DeckRoguelike.UI.InGameUIController.Instance?.SetPlayerEffect(
                "shoot_range", strengthIcon, s_playerShootRange,
                "발사 사거리", "발사 카드의 사거리(BFS 거리)");
        }

        /// <summary>
        /// 마스터덱을 훑어 customEffectId가 "Shoot"/"Allrange_Shoot"/"6times_Shoot" 인 효과의
        /// rangeOffsets를 BFS(s_playerShootRange) 셀로 동기화한다.
        /// 최초 갱신 시 원본 offsets(빈 배열)를 _shootRangeBackups에 백업해 전투 종료 복원에 사용.
        /// </summary>
        public void RefreshShootCardRanges()
        {
            var deckMgr = DeckRoguelike.Combat.DeckManager.Instance;
            if (deckMgr == null) return;

            var newOffsets = BuildBfsRangeOffsets(Mathf.Max(1, s_playerShootRange));

            foreach (var card in deckMgr.MasterDeck)
            {
                if (card?.effects == null) continue;
                foreach (var effect in card.effects)
                {
                    if (!IsShootAttackEffect(effect)) continue;

                    if (!_shootRangeBackups.ContainsKey(effect))
                    {
                        var original = effect.rangeOffsets ?? new RangeOffset[0];
                        var copy = new RangeOffset[original.Length];
                        for (int i = 0; i < copy.Length; i++)
                            copy[i] = new RangeOffset { col = original[i].col, row = original[i].row };
                        _shootRangeBackups[effect] = copy;
                    }
                    effect.rangeOffsets = newOffsets;
                }
            }
        }

        /// <summary>
        /// ShootDamage (33102/33103) — 전역 발사 추가 데미지(s_playerShootDamage)를 amount만큼 증가시킨다.
        /// Shoot 효과의 데미지 = effect.value + s_playerShootDamage + playerStrength.
        /// PlayerEffectIcon에도 별도 항목("shoot_damage")으로 표시되어 힘과 구분.
        /// </summary>
        public void AddShootDamage(int amount)
        {
            if (amount == 0) return;
            s_playerShootDamage += amount;
            RefreshHandDisplay();
            DeckRoguelike.UI.InGameUIController.Instance?.SetPlayerEffect(
                "shoot_damage", strengthIcon, s_playerShootDamage,
                "발사 데미지", "발사 카드의 추가 피해");
        }

        /// <summary>발사 카드 사용 횟수를 1 증가시키고 활성 파워의 OnShoot 훅을 호출한다.</summary>
        public void IncrementShootCount()
        {
            _shootCount++;
            foreach (var p in _activePowers) p.OnShoot(this, _shootCount);
        }

        /// <summary>트랩 설치 횟수를 1 증가시키고 활성 파워의 OnTrapPlaced 훅을 호출한다.</summary>
        public void IncrementTrapCount()
        {
            _trapCount++;
            foreach (var p in _activePowers) p.OnTrapPlaced(this, _trapCount);
        }

        /// <summary>발사 공격 효과인지 식별 (키워드가 아닌 customEffectId 기반).</summary>
        public static bool IsShootAttackEffect(CardEffect fx)
        {
            if (fx == null || fx.effectType != EffectType.Custom) return false;
            string id = fx.customEffectId;
            if (string.IsNullOrEmpty(id)) return false;
            return id.Equals("Shoot",            System.StringComparison.OrdinalIgnoreCase)
                || id.Equals("Allrange_Shoot",   System.StringComparison.OrdinalIgnoreCase)
                || id.Equals("6times_Shoot",     System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 발사 카드(Shoot 효과) 1회의 데미지를 계산해 적에게 적용한다.
        /// damage = baseValue + s_playerShootDamage + playerStrength.
        /// 33304/33305 저격 파워가 활성이면 적과의 Manhattan 거리만큼 Multiplier배 곱연산.
        /// 33306/33307 유탄발사 파워가 활성이면 도착 칸 + 4방향에도 동일 피해를 입힌다.
        /// 발사 후 OnShoot 훅을 호출하므로 도탄/숙련 등 후속 파워가 자동 발동.
        /// </summary>
        public void FireShoot(EnemyInstance enemy, int baseValue, bool triggerOnShoot = true)
        {
            if (enemy == null || enemy.CurrentHP <= 0) return;
            int dmg = baseValue + s_playerShootDamage + playerStrength;

            // 저격: Manhattan 거리당 ×Multiplier (1칸 떨어질수록 ×N)
            var snipe = _activePowers.OfType<ShootSnipePower>().FirstOrDefault();
            if (snipe != null)
            {
                int dist = Mathf.Abs(enemy.GridPos.x - playerSpawnCell.x)
                         + Mathf.Abs(enemy.GridPos.y - playerSpawnCell.y);
                if (dist > 0) dmg *= (int)Mathf.Pow(snipe.Multiplier, dist);
            }

            DamageEnemy(enemy, dmg);

            // 유탄발사: 도착 칸 4방향에도 동일 데미지
            if (_activePowers.OfType<ShootAreaAttackPower>().Any())
            {
                Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                foreach (var d in dirs)
                {
                    var pos = enemy.GridPos + d;
                    var e = GetEnemyAt(pos);
                    if (e != null && e.CurrentHP > 0) DamageEnemy(e, dmg);
                }
            }

            if (triggerOnShoot) IncrementShootCount();
        }

        /// <summary>도탄: 살아있는 적 중 무작위 1체에게 추가 발사 (OnShoot 훅 재진입 방지).</summary>
        public void FireRicochetShot(int dmg)
        {
            var alive = new List<EnemyInstance>();
            foreach (var e in enemies)
                if (e.CurrentHP > 0) alive.Add(e);
            if (alive.Count == 0) return;
            var target = alive[UnityEngine.Random.Range(0, alive.Count)];
            DamageEnemy(target, dmg);
        }

        /// <summary>무작위 이동 카드를 손패에 비용 0으로 추가 (Shoot_MoveCard 파워).</summary>
        public void AddRandomMoveCardToHand()
        {
            var character = DeckRoguelike.Core.GameManager.Instance?.SelectedCharacter
                ?? DeckRoguelike.Core.CharacterType.Warrior;
            var pool = DeckRoguelike.Core.CardRegistry.GetRewardPool(character)
                .Where(c => !c.IsUpgraded && c.CardTypeFromCode == DeckRoguelike.Cards.CardType.Move)
                .ToList();
            if (pool.Count == 0) return;
            AddCardToHandFree(pool[UnityEngine.Random.Range(0, pool.Count)]);
        }

        /// <summary>전투 종료 시 Shoot 사거리 강화로 변경된 rangeOffsets를 원본으로 복원하고
        /// 전역 발사 스탯(s_playerShootRange/s_playerShootDamage)을 초기값으로 되돌린다.</summary>
        private void RestoreShootCardRanges()
        {
            foreach (var kvp in _shootRangeBackups)
            {
                if (kvp.Key == null) continue;
                kvp.Key.rangeOffsets = kvp.Value;
            }
            _shootRangeBackups.Clear();
            s_playerShootRange  = 1;
            s_playerShootDamage = 0;
        }

        /// <summary>Manhattan 거리 ≤ n 의 모든 셀 (중심 제외) RangeOffset 배열을 생성합니다.</summary>
        private static RangeOffset[] BuildBfsRangeOffsets(int n)
        {
            if (n <= 0) return new RangeOffset[0];
            var list = new List<RangeOffset>();
            for (int c = -n; c <= n; c++)
            for (int r = -n; r <= n; r++)
            {
                if (c == 0 && r == 0) continue;
                if (Mathf.Abs(c) + Mathf.Abs(r) <= n)
                    list.Add(new RangeOffset { col = c, row = r });
            }
            return list.ToArray();
        }

        /// <summary>
        /// Card_Upgrade로 임시 강화된 모든 카드를 원래(강화 전) 카드로 되돌립니다.
        /// 전투 종료(승리/패배) 시점에 호출됩니다.
        /// </summary>
        private void RevertTemporaryUpgrades()
        {
            if (deckManager == null || _temporarilyUpgradedCards.Count == 0)
            {
                _temporarilyUpgradedCards.Clear();
                return;
            }

            foreach (var upgradedCard in _temporarilyUpgradedCards)
            {
                if (upgradedCard == null) continue;
                int originalCode = upgradedCard.cardCode - 1;
                var originalDef  = DeckRoguelike.Core.CardRegistry.GetCard(originalCode);
                if (originalDef == null) continue;
                deckManager.DowngradeCard(upgradedCard, originalDef);
            }
            _temporarilyUpgradedCards.Clear();
        }

        // ── Random_*Card 변환 ─────────────────────────────────────────────

        /// <summary>
        /// 카드가 손패에 들어올 때 Random_AttackCard / Random_MoveCard / Random_PowerCard 효과를 가지면
        /// 같은 분류의 무작위 카드(value=0: 강화 전 / value=1: 강화 후)로 치환합니다.
        /// 변환 매핑은 기록되며, 카드가 손패를 떠날 때 ResolveOriginalCard로 원본을 되돌립니다.
        /// 매칭되는 효과가 없으면 입력 그대로 반환.
        /// </summary>
        private CardData TryTransformRandomCard(CardData card)
        {
            if (card?.Effects == null || card.Effects.Count == 0) return card;

            DeckRoguelike.Cards.CardType? targetType = null;
            int valueFlag = 0;
            foreach (var eff in card.Effects)
            {
                if (eff.effectType != DeckRoguelike.Cards.EffectType.Custom) continue;
                if (string.IsNullOrEmpty(eff.customEffectId)) continue;
                if (string.Equals(eff.customEffectId, "Random_AttackCard", System.StringComparison.OrdinalIgnoreCase))
                { targetType = DeckRoguelike.Cards.CardType.Action; valueFlag = eff.value; break; }
                if (string.Equals(eff.customEffectId, "Random_MoveCard", System.StringComparison.OrdinalIgnoreCase))
                { targetType = DeckRoguelike.Cards.CardType.Move; valueFlag = eff.value; break; }
                if (string.Equals(eff.customEffectId, "Random_PowerCard", System.StringComparison.OrdinalIgnoreCase))
                { targetType = DeckRoguelike.Cards.CardType.Power; valueFlag = eff.value; break; }
            }
            if (!targetType.HasValue) return card;

            var character = GameManager.Instance?.SelectedCharacter ?? DeckRoguelike.Core.CharacterType.Warrior;
            var pool = DeckRoguelike.Core.CardRegistry.GetRewardPool(character)
                .Where(c => c.CardTypeFromCode == targetType.Value)
                .Where(c => !HasRandomTransformEffect(c)) // Random_*Card 자기참조 방지
                .ToList();
            if (pool.Count == 0)
            {
                Debug.LogWarning($"[Random_*Card] {targetType} 풀이 비어있습니다 — 변환 생략");
                return card;
            }

            var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            // valueFlag == 1: 강화 버전 사용 (없으면 기본 사용)
            if (valueFlag >= 1)
            {
                var upgraded = DeckRoguelike.Core.CardRegistry.GetCard(pick.UpgradedCode);
                if (upgraded != null) pick = upgraded;
            }

            var clone = pick.Clone();
            _randomCardOrigin[clone] = card; // 원본 보관 → 손패를 떠날 때 복원
            return clone;
        }

        /// <summary>해당 카드가 Random_*Card 변환 효과를 갖는지 확인합니다 (자기참조 방지용).</summary>
        private static bool HasRandomTransformEffect(CardData card)
        {
            if (card?.Effects == null) return false;
            foreach (var eff in card.Effects)
            {
                if (eff.effectType != DeckRoguelike.Cards.EffectType.Custom) continue;
                if (string.IsNullOrEmpty(eff.customEffectId)) continue;
                if (eff.customEffectId.StartsWith("Random_", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Random_*Card로 변환된 카드라면 원본 Random 카드를 반환, 아니면 그대로 반환.
        /// 카드가 discard / exhaust 더미로 이동할 때 호출되어 매번 새로 변환되도록 합니다.
        /// </summary>
        private CardData ResolveOriginalCard(CardData card)
        {
            if (card == null) return null;
            if (_randomCardOrigin.TryGetValue(card, out var original))
            {
                _randomCardOrigin.Remove(card);
                return original;
            }
            return card;
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
                hand[i] = clone;
                if (i < handCardObjects.Count)
                    handCardObjects[i]?.GetComponent<DeckRoguelike.Cards.CardUI>()
                        ?.Initialize(clone, playerStrength, playerDexterity);
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
                // 돌림판으로 뽑힌 Random_*Card도 손패 진입과 동일하게 무작위 카드로 변환
                card = TryTransformRandomCard(card);

                // 돌림판: 카드의 효과 타입에 따라 무작위 타겟/위치를 미리 결정.
                Vector2Int? randomPos = ResolveRouletteSelectedPos(card);
                ApplyCardEffects(card, randomPos);

                // 돌림판: Discard 효과가 예약되어 있고 손패에 카드가 있으면 무작위 버림으로 자동 처리.
                if (_hasPendingChoosePanel && !_pendingChoosePanelExhaust)
                    AutoDiscardRandomFromHand(_pendingChoosePanelCount);
                _hasPendingChoosePanel = false;
                _pendingChoosePanelExhaust = false;
                _pendingChoosePanelCount = 0;

                CardData pileCard = ResolveOriginalCard(card);
                if (!card.Exhaust || _noExhaust)
                    deckManager?.AddToDiscardPile(pileCard);
                else
                {
                    deckManager?.ExhaustCard(pileCard);
                    foreach (var p in _activePowers) p.OnCardExhausted(this);
                }
                CheckVictory();
                if (combatState != CombatState.PlayerTurn) yield break;
                yield return new WaitForSeconds(0.25f);
            }
            UpdatePileCounters();
        }

        /// <summary>
        /// 돌림판(200) 전용: 카드의 첫 효과 종류를 보고 무작위 타겟/이동 위치를 결정한다.
        /// - Damage / TargetType.Enemy: rangeOffsets 내 살아있는 적 중 무작위
        /// - Move: 플레이어 인접 빈 칸 중 무작위
        /// 그 외(All/Random targeting, 위치 불필요)는 null 반환.
        /// </summary>
        private Vector2Int? ResolveRouletteSelectedPos(CardData card)
        {
            if (card?.Effects == null) return null;
            foreach (var effect in card.Effects)
            {
                if (effect.effectType == EffectType.Damage && effect.targeting == TargetType.Enemy)
                {
                    var inRange = new List<EnemyInstance>();
                    if (effect.rangeOffsets != null && effect.rangeOffsets.Length > 0)
                    {
                        foreach (var off in effect.rangeOffsets)
                        {
                            Vector2Int p = effect.useAbsoluteCoords
                                ? off.ToVector2Int()
                                : playerSpawnCell + off.ToVector2Int();
                            if (!IsInBoard(p)) continue;
                            var e = grid[p.x, p.y].OccupyingEnemy;
                            if (e != null && e.CurrentHP > 0) inRange.Add(e);
                        }
                    }
                    else
                    {
                        foreach (var e in enemies) if (e.CurrentHP > 0) inRange.Add(e);
                    }
                    if (inRange.Count == 0) return null;
                    return inRange[UnityEngine.Random.Range(0, inRange.Count)].GridPos;
                }

                if (effect.effectType == EffectType.Move)
                {
                    var candidates = new List<Vector2Int>();
                    int[] dx = { -1, 1, 0, 0 };
                    int[] dy = {  0, 0,-1, 1 };
                    for (int k = 0; k < 4; k++)
                    {
                        var p = playerSpawnCell + new Vector2Int(dx[k], dy[k]);
                        if (!IsInBoard(p)) continue;
                        var c = grid[p.x, p.y];
                        if (c.OccupyingEnemy != null || c.OccupyingAlly != null) continue;
                        candidates.Add(p);
                    }
                    if (candidates.Count == 0) return null;
                    return candidates[UnityEngine.Random.Range(0, candidates.Count)];
                }
            }
            return null;
        }

        /// <summary>돌림판: ChoosePanel 대신 손패에서 무작위로 count장 자동 버림.</summary>
        private void AutoDiscardRandomFromHand(int count)
        {
            int toDiscard = Mathf.Min(count, hand.Count);
            for (int i = 0; i < toDiscard; i++)
            {
                if (hand.Count == 0) break;
                int idx = UnityEngine.Random.Range(0, hand.Count);
                var c = hand[idx];
                hand.RemoveAt(idx);
                if (idx < handCardObjects.Count)
                {
                    if (handCardObjects[idx] != null) Destroy(handCardObjects[idx]);
                    handCardObjects.RemoveAt(idx);
                }
                deckManager?.AddToDiscardPile(ResolveOriginalCard(c));
            }
            if (toDiscard > 0)
            {
                RepositionHandCards();
                UpdatePileCounters();
            }
        }

        /// <summary>다음에 사용하는 액션 카드의 피해를 multiplier배로 만듭니다 (분노의 포션).</summary>
        public void SetNextAttackMultiplier(int multiplier)
        {
            nextAttackDamageMultiplier = multiplier;
        }

        /// <summary>다음 카드를 2회 실행하도록 설정합니다 (재사용 포션).</summary>
        /// <summary>다음에 사용하는 카드를 버리지 않고 손으로 되돌립니다 (203 재사용 포션).</summary>
        public void SetNextCardKeepInHand()
        {
            nextCardKeepInHand = true;
            DeckRoguelike.UI.InGameUIController.Instance?.SetPlayerEffect(
                "card_keep_in_hand", null, 1, "재사용 포션", "다음에 사용하는 카드는 버려지지 않습니다.");
        }

        public void SetNextCardDoublePlay()
        {
            nextCardDoublePlay = true;
        }

        /// <summary>다음에 사용하는 카드를 복사하고 복사본에 소멸을 부여합니다 (108 재사용 포션).</summary>
        public void SetNextCardCopyExhaust()
        {
            nextCardCopyExhaust = true;
            DeckRoguelike.UI.InGameUIController.Instance?.SetPlayerEffect(
                "card_copy_exhaust", null, 1, "재사용 포션", "다음에 사용하는 카드를 복사하고 복사본에 소멸을 부여합니다.");
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
            s_outgoingDamageMultiplier = outgoingDamageMultiplier;
            RefreshHandDisplay(); // {D} 표시 즉시 갱신
        }

        /// <summary>이번 턴 데미지 배율을 현재 값에 곱합니다 (최후의 공격 중복 사용 시).</summary>
        public void MultiplyTurnDamageMultipliers(float outgoing, float incoming)
        {
            outgoingDamageMultiplier *= outgoing;
            incomingDamageMultiplier *= incoming;
            s_outgoingDamageMultiplier = outgoingDamageMultiplier;
            RefreshHandDisplay(); // {D} 표시 즉시 갱신
        }

        public float OutgoingDamageMultiplier => outgoingDamageMultiplier;
        public float IncomingDamageMultiplier => incomingDamageMultiplier;

        /// <summary>이번 턴에만 힘을 추가합니다. 턴 종료 시 자동 회수됩니다 (초코바).</summary>
        public void AddTempStrength(int amount)
        {
            playerStrength       += amount;
            tempStrengthFromItem += amount;
            RefreshHandDisplay();
            DeckRoguelike.UI.InGameUIController.Instance?.SetPlayerEffect("strength", strengthIcon, playerStrength, "힘", "공격 시 추가 피해를 줍니다.");
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

        /// <summary>전투 시작 시 인벤토리의 308(소생의 팬던트)을 자동으로 패시브 등록합니다.</summary>
        private void AutoRegisterRevivePendant()
        {
            if (GameManager.Instance == null) return;
            foreach (var it in GameManager.Instance.Items)
            {
                if (it != null && it.itemCode == 308)
                {
                    RegisterRevivePassive(it, 0.3f);
                    Debug.Log("[AutoRegisterRevivePendant] 소생의 팬던트 자동 활성화");
                    break;
                }
            }
        }

        /// <summary>플레이어 자신의 모든 해로운 상태이상과 카드 제한을 제거합니다 (성수).</summary>
        public void RemovePlayerDebuffs()
        {
            playerStatusEffects.RemoveAll(s =>
                s.Type == StatusEffectType.Fire ||
                s.Type == StatusEffectType.Stun ||
                s.Type == StatusEffectType.Freeze ||
                s.Type == StatusEffectType.Fear ||
                s.Type == StatusEffectType.NoMoveCard ||
                s.Type == StatusEffectType.NoActionCard ||
                s.Type == StatusEffectType.NoPowerCard);
            cardTypeRestrictions.Clear();
            DeckRoguelike.UI.InGameUIController.Instance?.RemovePlayerEffect("fire");
            DeckRoguelike.UI.InGameUIController.Instance?.RemovePlayerEffect("stun");
            DeckRoguelike.UI.InGameUIController.Instance?.RemovePlayerEffect("freeze");
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
            InGameUIController.Instance?.ClearPlayerEffects();
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

        /// <summary>아군 이동. 빈 셀이면 이동 성공, 아니면 false.</summary>
        public bool TryMoveAlly(AllyInstance ally, Vector2Int targetPos)
        {
            if (ally == null) return false;
            if (!IsInBoard(targetPos)) return false;

            // 멀티셀이면 footprint 전체가 보드 안 + 비점유(자기 자신 제외)여야 한다.
            Vector2Int size = ally.Size;
            if (!IsFootprintFreeForAlly(targetPos, size, ignoreAlly: ally)) return false;

            // 기존 footprint 셀들 해제
            ClearAllyFootprintCells(ally);

            if (targetPos.x > ally.GridPos.x) SetAllyFacingRight(ally, true);
            else if (targetPos.x < ally.GridPos.x) SetAllyFacingRight(ally, false);

            // 새 footprint 점유
            ally.GridPos = targetPos;
            SetAllyFootprintCells(ally, targetPos, size);

            if (ally.GameObject != null)
            {
                RectTransform rt = ally.GameObject.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = GetEnemyVisualPosition(targetPos, size) + playerCellOffset;
                ally.GameObject.transform.SetAsLastSibling();
            }

            // 아군 위치가 바뀌면 그 아군을 노리던 적의 경로·타겟이 달라지므로 재계산.
            RePlanAllEnemyTurns();

            // 함정 발동 — 아군이 함정 위에 진입한 경우
            TryTriggerHazard(targetPos, HazardTriggerSource.Ally, movingAlly: ally);
            return true;
        }

        /// <summary>아군이 이동 가능한 빈 셀인지 확인 (플레이어·아군·적 없음).</summary>
        public bool IsCellFreeForAlly(Vector2Int pos) => IsCellFreeForEnemy(pos);

        /// <summary>아군을 전투에서 즉시 제거합니다. 사망 훅은 발동하지 않습니다.</summary>
        public void RemoveAllyFromCombat(AllyInstance ally)
        {
            if (ally == null) return;
            ally.CurrentHP = 0;
            ClearAllyFootprintCells(ally);   // 멀티셀이면 footprint 전체 해제
            if (ally.GameObject != null)
                Destroy(ally.GameObject);
            allies.Remove(ally);
            Debug.Log($"[CombatController] {ally.Name} 전투 이탈!");

            // 아군이 사라졌으니 그 아군을 노리던 적의 타겟·경로가 달라진다 — 플레이어 턴 중이면 재계산.
            RePlanAllEnemyTurns();
        }

        /// <summary>적을 전투에서 즉시 제거합니다 (도주 등). 사망 훅은 발동하지 않습니다.
        /// checkVictory=false면 승리 판정을 건너뛴다 — 자폭 후 같은 자리에 소환하는 흐름에서
        /// 셀을 먼저 비워주되 소환 전에 승리가 트리거되지 않도록 분리할 때 사용.</summary>
        public void RemoveEnemyFromCombat(EnemyInstance enemy, bool checkVictory = true)
        {
            if (enemy == null) return;
            enemy.CurrentHP = 0;
            ClearEnemyFootprintCells(enemy);
            if (enemy.GameObject != null)
                Destroy(enemy.GameObject);
            Debug.Log($"[CombatController] {enemy.Name} 전투에서 이탈!");
            if (checkVictory) CheckVictory();

            // 적이 이탈해 셀이 비었으니 다른 적들의 BFS 경로·타겟이 달라진다 — 플레이어 턴 중이면
            // 모든 적을 재계산. (잔류 미리보기 스프라이트 정리도 RePlanAllEnemyTurns가 함께 처리)
            RePlanAllEnemyTurns();
        }

        /// <summary>외부(적 행동 등)에서 자폭 후 소환 등 다단계 효과 종료 시 승리 판정을 수동 실행.</summary>
        public void TriggerVictoryCheck() => CheckVictory();

        // 런타임 소환의 SpawnGroupId 카운터. 인카운터 슬롯(작은 양수 1, 2, …)과 충돌하지 않도록
        // 큰 값에서 시작해 호출 1회마다 1씩 증가. 같은 호출에서 묶고 싶으면 SummonEnemy(...,spawnGroupId)
        // 오버로드를 사용해 공유 ID 전달.
        private int _runtimeSpawnGroupCounter = 1_000_000;

        /// <summary>전투 중 적을 소환합니다. footprint 전체가 보드 안+빈 칸이어야 소환 성공.
        /// 호출 1회마다 새 SpawnGroupId를 자동 부여 — SnakeBehavior 같은 군집 행동이 호출별로
        /// 별도 체인을 이루도록.</summary>
        public EnemyInstance SummonEnemy(int enemyId, Vector2Int pos)
            => SummonEnemy(enemyId, pos, _runtimeSpawnGroupCounter++);

        /// <summary>여러 적을 같은 그룹으로 묶어 소환하고 싶을 때 spawnGroupId를 명시.
        /// 호출자가 미리 한 번 _runtimeSpawnGroupCounter를 받아 같은 값으로 여러 번 호출하면 한 그룹.</summary>
        public EnemyInstance SummonEnemy(int enemyId, Vector2Int pos, int spawnGroupId)
        {
            EnemyData data = DeckRoguelike.Core.EnemyRegistry.GetEnemy(enemyId);
            if (data == null) return null;

            Vector2Int summonSize = (data.gridSize.x > 0 && data.gridSize.y > 0) ? data.gridSize : Vector2Int.one;
            if (!IsFootprintFreeForEnemy(pos, summonSize, ignoreEnemy: null))
            {
                Debug.LogWarning($"[CombatController] 소환 실패: ({pos.x},{pos.y}) ~ +{summonSize.x-1}/-{summonSize.y-1} footprint 사용 불가");
                return null;
            }

            int enemyDamage = data.baseDamage;
            if (GameManager.Instance != null && GameManager.Instance.HasRelic(910))
                enemyDamage += 2;

            EnemyInstance enemy = new EnemyInstance
            {
                Name         = data.enemyName,
                MaxHP        = data.maxHP,
                Damage       = enemyDamage,
                GridPos      = pos,
                Data         = data,
                SpawnGroupId = spawnGroupId,
            };
            enemy.CurrentHP = enemy.MaxHP;
            enemy.Behavior  = EnemyBehaviorRegistry.Create(data.enemyId);

            PlaceEnemyOnCell(enemy, pos);
            enemy.Behavior?.OnSpawn(enemy, this);
            enemies.Add(enemy);
            Debug.Log($"[CombatController] {enemy.Name} 소환 @ ({pos.x},{pos.y})");
            return enemy;
        }

        /// <summary>같은 그룹으로 여러 번 SummonEnemy를 호출하고 싶을 때 새 그룹 ID를 미리 받아둔다.</summary>
        public int AcquireSpawnGroupId() => _runtimeSpawnGroupCounter++;

        /// <summary>적에게 상태이상을 부여합니다. Fire는 스택이 누적됩니다.</summary>
        public void ApplyStatus(EnemyInstance enemy, StatusEffectType type, int stacks)
        {
            if (enemy == null || enemy.CurrentHP <= 0) return;

            // 즉시 적용 버프 — StatusEffects 리스트에 추가하지 않고 바로 처리
            if (type == StatusEffectType.Strength)
            {
                enemy.Damage = Mathf.Max(0, enemy.Damage + stacks);
                return;
            }
            if (type == StatusEffectType.HealHP)
            {
                enemy.CurrentHP = Mathf.Min(enemy.MaxHP, enemy.CurrentHP + stacks);
                enemy.UI?.UpdateHP(enemy.CurrentHP, enemy.MaxHP);
                return;
            }

            // 190 유물 — 화염 부여 시 +1 추가
            if (type == StatusEffectType.Fire
                && GameManager.Instance != null
                && GameManager.Instance.HasRelic(190))
            {
                stacks += 1;
            }

            // 502 유물 — 매 턴 1회, 적에게 부여하는 해로운 효과를 3배 증가
            bool isHarmful = type == StatusEffectType.Fire
                          || type == StatusEffectType.Stun
                          || type == StatusEffectType.Freeze
                          || type == StatusEffectType.Fear;
            if (isHarmful && stacks > 0 && GameManager.Instance != null && GameManager.Instance.HasRelic(502))
            {
                var trip = GameManager.Instance.Relics
                    .OfType<DeckRoguelike.Relic.TripleDebuffRelic>()
                    .FirstOrDefault();
                if (trip != null && !trip.ConsumedThisTurn)
                {
                    stacks *= 3;
                    trip.MarkConsumed();
                    Debug.Log($"[Relic502] {enemy.Name} {type} ×3 → {stacks}");
                }
            }

            var existing = enemy.StatusEffects.Find(s => s.Type == type);
            if (existing != null)
                existing.Stacks += stacks;
            else
                enemy.StatusEffects.Add(new StatusEntry { Type = type, Stacks = stacks });

            // 공포: 기절 효과 + 플레이어 위치에 따라 적의 바라보는 방향을 회전시킨다.
            // 플레이어가 적의 오른쪽이면 적은 왼쪽을 향하고(FacingRight=false),
            // 플레이어가 적의 왼쪽이면 적은 오른쪽을 향한다(FacingRight=true).
            // 같은 x(위/아래)면 방향 변경 없음 → 기절만.
            if (type == StatusEffectType.Fear)
            {
                if (playerSpawnCell.x > enemy.GridPos.x)
                    SetEnemyFacingRight(enemy, false);
                else if (playerSpawnCell.x < enemy.GridPos.x)
                    SetEnemyFacingRight(enemy, true);
            }

            // 기절/빙결/공포 부여 시 공격·이동 미리보기를 즉시 갱신해 해당 적의 sprite를 제거한다.
            // (combatState 가드 없음 — 부여 시점이 언제든 즉시 sprite 삭제 보장)
            if (type == StatusEffectType.Stun || type == StatusEffectType.Freeze || type == StatusEffectType.Fear)
            {
                ShowUnitAttackPreviews();
            }

            // 속박: 이동 미리보기/행동 아이콘을 즉시 갱신해 해당 적의 이동 sprite를 제거한다.
            // (공격 미리보기는 그대로 유지)
            if (type == StatusEffectType.Bondage)
            {
                enemy.Behavior?.ComputeDeferredMovePreview(enemy, this);
                RefreshEnemyActionIcons(enemy);
                ShowUnitAttackPreviews();
            }

            var entry = enemy.StatusEffects.Find(s => s.Type == type);
            string key   = type switch
            {
                StatusEffectType.Fire   => "fire",
                StatusEffectType.Stun   => "stun",
                StatusEffectType.Freeze => "freeze",
                StatusEffectType.Fear   => "fear",
                _                       => "freeze"
            };
            Sprite icon  = type switch
            {
                StatusEffectType.Fire   => fireIcon,
                StatusEffectType.Stun   => stunIcon,
                StatusEffectType.Freeze => freezeIcon,
                StatusEffectType.Fear   => (fearIcon != null ? fearIcon : stunIcon),
                _                       => freezeIcon
            };
            // 적 상태이상은 데이터만 저장
        }

        /// <summary>플레이어에게 카드 타입 봉인 디버프를 부여합니다.</summary>
        public void ApplyStatusToPlayer(StatusEffectType type, int stacks)
        {
            switch (type)
            {
                case StatusEffectType.NoMoveCard:
                    AddCardTypeRestriction(DeckRoguelike.Cards.CardType.Move, stacks);
                    break;
                case StatusEffectType.NoActionCard:
                    AddCardTypeRestriction(DeckRoguelike.Cards.CardType.Action, stacks);
                    break;
                case StatusEffectType.NoPowerCard:
                    AddCardTypeRestriction(DeckRoguelike.Cards.CardType.Power, stacks);
                    break;
            }
        }

        /// <summary>아군에게 디버프(이동/공격/능력치향상 봉인)를 부여합니다.</summary>
        public void ApplyStatusToAlly(AllyInstance ally, StatusEffectType type, int stacks)
        {
            if (ally == null || ally.CurrentHP <= 0) return;
            if (type == StatusEffectType.Strength)
            {
                if (HasAllyStatBuff(ally)) return;
                ally.Damage = Mathf.Max(0, ally.Damage + stacks);
                return;
            }
            if (type == StatusEffectType.HealHP)
            {
                ally.CurrentHP = Mathf.Min(ally.MaxHP, ally.CurrentHP + stacks);
                ally.UI?.UpdateHP(ally.CurrentHP, ally.MaxHP);
                return;
            }
            var existing = ally.StatusEffects.Find(s => s.Type == type);
            if (existing != null)
                existing.Stacks += stacks;
            else
                ally.StatusEffects.Add(new StatusEntry { Type = type, Stacks = stacks });
        }

        // ── Hazard Zone에서 사용하는 공개 진입점 ─────────────────────────

        /// <summary>플레이어에게 상태이상을 부여합니다 (Fire/Cold 스택 누적, 카드봉인 등 기존 항목 위임).</summary>
        public void ApplyStatusToPlayerPublic(StatusEffectType type, int stacks)
        {
            if (stacks <= 0) return;
            switch (type)
            {
                case StatusEffectType.NoMoveCard:
                case StatusEffectType.NoActionCard:
                case StatusEffectType.NoPowerCard:
                    ApplyStatusToPlayer(type, stacks);
                    return;
                default:
                {
                    var existing = playerStatusEffects.Find(s => s.Type == type);
                    if (existing != null) existing.Stacks += stacks;
                    else playerStatusEffects.Add(new StatusEntry { Type = type, Stacks = stacks });
                    if (type == StatusEffectType.Fire)
                        DeckRoguelike.UI.InGameUIController.Instance?.SetPlayerEffect("fire", fireIcon, existing != null ? existing.Stacks : stacks, "화염", "매 턴 시작 시 스택만큼 피해를 받습니다.");
                    break;
                }
            }
        }

        /// <summary>적에게 냉기 스택을 누적. 3 이상이 되면 냉기를 제거하고 빙결(=Stun과 동일) 1턴 부여.</summary>
        public void AddColdAndMaybeFreeze(EnemyInstance enemy, int stacks)
        {
            if (enemy == null || enemy.CurrentHP <= 0 || stacks <= 0) return;
            var entry = enemy.StatusEffects.Find(s => s.Type == StatusEffectType.Cold);
            if (entry == null)
            {
                entry = new StatusEntry { Type = StatusEffectType.Cold, Stacks = 0 };
                enemy.StatusEffects.Add(entry);
            }
            entry.Stacks += stacks;
            Debug.Log($"[Cold] {enemy.Name} 냉기 +{stacks} → {entry.Stacks}");
            if (entry.Stacks >= 3)
            {
                enemy.StatusEffects.RemoveAll(s => s.Type == StatusEffectType.Cold);
                ApplyStatus(enemy, StatusEffectType.Freeze, 1);
                Debug.Log($"[Cold] {enemy.Name} 냉기 3 누적 → 빙결 1턴");
            }
        }

        /// <summary>살아있는 모든 적에게 냉기 stacks를 누적합니다 (한파 카드용). 각 적이 3 누적 시 개별 빙결 전환.</summary>
        public void AddColdToAllEnemies(int stacks)
        {
            if (stacks <= 0) return;
            // AddColdAndMaybeFreeze가 3스택 도달 시 Freeze 부여 → ShowUnitAttackPreviews 호출 가능하므로
            // 순회 중 컬렉션 변경 위험은 없으나(소환 없음), 안전하게 스냅샷으로 순회.
            foreach (var enemy in new List<EnemyInstance>(enemies))
                if (enemy != null && enemy.CurrentHP > 0)
                    AddColdAndMaybeFreeze(enemy, stacks);
        }

        /// <summary>아군에게 냉기 스택을 누적. 3 이상이면 빙결(=Stun과 동일) 1턴 부여.</summary>
        public void AddColdAndMaybeFreezeAlly(AllyInstance ally, int stacks)
        {
            if (ally == null || ally.CurrentHP <= 0 || stacks <= 0) return;
            var entry = ally.StatusEffects.Find(s => s.Type == StatusEffectType.Cold);
            if (entry == null)
            {
                entry = new StatusEntry { Type = StatusEffectType.Cold, Stacks = 0 };
                ally.StatusEffects.Add(entry);
            }
            entry.Stacks += stacks;
            Debug.Log($"[Cold] 아군 {ally.Name} 냉기 +{stacks} → {entry.Stacks}");
            if (entry.Stacks >= 3)
            {
                ally.StatusEffects.RemoveAll(s => s.Type == StatusEffectType.Cold);
                ApplyStatusToAlly(ally, StatusEffectType.Freeze, 1);
            }
        }

        /// <summary>플레이어에게 냉기 스택을 누적. 3 이상이면 1턴 동안 이동/액션 카드 봉인(플레이어판 빙결).</summary>
        public void AddColdAndMaybeFreezePlayer(int stacks)
        {
            if (stacks <= 0) return;
            var entry = playerStatusEffects.Find(s => s.Type == StatusEffectType.Cold);
            if (entry == null)
            {
                entry = new StatusEntry { Type = StatusEffectType.Cold, Stacks = 0 };
                playerStatusEffects.Add(entry);
            }
            entry.Stacks += stacks;
            Debug.Log($"[Cold] 플레이어 냉기 +{stacks} → {entry.Stacks}");
            if (entry.Stacks >= 3)
            {
                playerStatusEffects.RemoveAll(s => s.Type == StatusEffectType.Cold);
                // 플레이어는 Freeze 직접 상태가 없으므로 행동/이동 카드 봉인 1턴으로 대체
                ApplyStatusToPlayer(StatusEffectType.NoActionCard, 1);
                ApplyStatusToPlayer(StatusEffectType.NoMoveCard,   1);
            }
        }

        /// <summary>아군이 NoStatBuff 상태인지 확인합니다.</summary>
        public bool HasAllyStatBuff(AllyInstance ally)
            => ally != null && ally.StatusEffects.Exists(s => s.Type == StatusEffectType.NoStatBuff && s.Stacks > 0);

        /// <summary>지정 셀에 있는 유닛(플레이어 또는 아군)에게 데미지를 줍니다.</summary>
        public void ApplyDamageAtCell(int rawDamage, Vector2Int cell, EnemyInstance attacker = null)
        {
            // 플레이어
            if (cell == PlayerSpawnCell)
            {
                ApplyDamageToPlayer(rawDamage, attacker);
                return;
            }
            // 아군 — 멀티셀이면 footprint 모든 셀에 OccupyingAlly가 설정돼 있으므로 grid 조회로 찾는다.
            if (!IsInBoard(cell)) return;
            AllyInstance ally = grid[cell.x, cell.y].OccupyingAlly;
            if (ally != null && ally.CurrentHP > 0)
            {
                // 빙결 상태인 아군은 공격당할 때 3배 피해
                if (ally.StatusEffects.Exists(s => s.Type == StatusEffectType.Freeze && s.Stacks > 0))
                {
                    rawDamage *= 3;
                    Debug.Log($"[Freeze] 아군 {ally.Name} 빙결 상태 — 피해 3배 → {rawDamage}");
                }
                int dmg = Mathf.Max(0, rawDamage - ally.Block);
                ally.Block = Mathf.Max(0, ally.Block - rawDamage);
                if (dmg > 0)
                {
                    ally.CurrentHP = Mathf.Max(0, ally.CurrentHP - dmg);
                    ally.UI?.UpdateHP(ally.CurrentHP, ally.MaxHP);
                    if (ally.CurrentHP <= 0)
                    {
                        ally.Behavior?.OnDeath(ally, this);
                        // 아군 사망으로 타겟이 사라졌으니 적 행동 재계산 (플레이어 턴 중이면 적용).
                        RePlanAllEnemyTurns();
                    }
                }
            }
        }

        /// <summary>지정 셀에 데미지를 주고 실제 피해량을 반환합니다.</summary>
        public int ApplyDamageAtCellWithReturn(int rawDamage, Vector2Int cell, EnemyInstance attacker = null)
        {
            if (cell == PlayerSpawnCell)
                return ApplyDamageToPlayer(rawDamage, attacker);
            // 아군 — 멀티셀이면 footprint 모든 셀에 OccupyingAlly가 설정돼 있으므로 grid 조회로 찾는다.
            if (!IsInBoard(cell)) return 0;
            AllyInstance ally = grid[cell.x, cell.y].OccupyingAlly;
            if (ally != null && ally.CurrentHP > 0)
            {
                // 빙결 상태인 아군은 공격당할 때 3배 피해
                if (ally.StatusEffects.Exists(s => s.Type == StatusEffectType.Freeze && s.Stacks > 0))
                {
                    rawDamage *= 3;
                    Debug.Log($"[Freeze] 아군 {ally.Name} 빙결 상태 — 피해 3배 → {rawDamage}");
                }
                int dmg = Mathf.Max(0, rawDamage - ally.Block);
                ally.Block = Mathf.Max(0, ally.Block - rawDamage);
                if (dmg > 0)
                {
                    ally.CurrentHP = Mathf.Max(0, ally.CurrentHP - dmg);
                    ally.UI?.UpdateHP(ally.CurrentHP, ally.MaxHP);
                    if (ally.CurrentHP <= 0)
                    {
                        ally.Behavior?.OnDeath(ally, this);
                        // 아군 사망으로 타겟이 사라졌으니 적 행동 재계산 (플레이어 턴 중이면 적용).
                        RePlanAllEnemyTurns();
                    }
                }
                return dmg;
            }
            return 0;
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
            AnyItemTargetingActive   = true;
            ClearAllHighlights();
            // 적 행동 미리보기(enemyRangePreview)는 유지
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
            AnyItemTargetingActive = true;
            ClearAllHighlights();
            // 적 행동 미리보기(enemyRangePreview)는 유지
            ClearItemHoverPreview();

            for (int col = 0; col < boardCols; col++)
                for (int row = 0; row < boardRows; row++)
                {
                    var cell = grid[col, row];
                    // 유닛(플레이어/적/아군)이 있는 셀에는 movesprite를 표시하지 않음
                    if (cell.IsPlayerHere || cell.OccupyingEnemy != null || cell.OccupyingAlly != null)
                        continue;
                    cell.SetState(DeckRoguelike.UI.CellState.AreaSelectable);
                    if (moveRangeSprite != null)
                        AddPreviewObject(cell.GridPos, moveRangeSprite, itemHoverPreviewObjects);
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
            AnyItemTargetingActive = false;
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
            ClearItemHoverPreview();
            ClearAllHighlights();

            // 텔레포트는 인접 제한이 없으므로 MovePlayer의 인접체크를 우회한다.
            TeleportPlayer(cell.GridPos);
            grid[playerSpawnCell.x, playerSpawnCell.y].SetState(CellState.PlayerOccupied);

            _itemConfirmedUseCallback?.Invoke();
            FinishItemSlot();
            _itemConfirmedUseCallback = null;
        }

        /// <summary>순간이동 카드 사용을 카운트합니다. 실제 이동은 카드의 Move 효과가 처리합니다.</summary>
        public void RegisterTeleportUse()
        {
            _teleportUsesThisCombat++;
            RefreshCardPlayability();
            Debug.Log($"[BoardController] 순간이동 사용 {_teleportUsesThisCombat}/{GameManager.Instance?.CurrentAct ?? 1}");
        }

        /// <summary>해당 카드가 순간이동(teleportation) 커스텀 효과를 가진 카드인지 판정.</summary>
        private static bool HasTeleportationEffect(CardData card)
        {
            if (card?.Effects == null) return false;
            foreach (var eff in card.Effects)
            {
                if (eff == null) continue;
                if (eff.effectType != EffectType.Custom) continue;
                if (string.Equals(eff.customEffectId, "teleportation", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>순간이동 카드의 전투당 잔여 사용 횟수 (act 수 기준).</summary>
        public int TeleportUsesRemaining
        {
            get
            {
                int cap = GameManager.Instance?.CurrentAct ?? 1;
                return Mathf.Max(0, cap - _teleportUsesThisCombat);
            }
        }

        /// <summary>플레이어를 어디로든 이동 (인접 제한 무시). 적이 있는 칸은 거부.</summary>
        private void TeleportPlayer(Vector2Int newPos)
        {
            if (!IsInBoard(newPos)) return;
            var target = grid[newPos.x, newPos.y];
            if (target.OccupyingEnemy != null || target.OccupyingAlly != null) return;

            if (newPos.x > playerSpawnCell.x) SetFacingRight(true);
            else if (newPos.x < playerSpawnCell.x) SetFacingRight(false);

            CombatBoardCell oldCell = grid[playerSpawnCell.x, playerSpawnCell.y];
            oldCell.IsPlayerHere = false;
            oldCell.ClearHighlight();

            if (playerObject != null)
            {
                RectTransform rt = playerObject.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = GetCellPosition(newPos.x, newPos.y) + playerCellOffset;
                playerObject.transform.SetAsLastSibling();
            }

            target.IsPlayerHere = true;
            target.SetState(CellState.PlayerOccupied);
            playerSpawnCell = newPos;
            Debug.Log($"[TeleportPlayer] 순간이동 → {newPos}");

            // 플레이어가 순간이동했으니 모든 적 행동을 재계산 (플레이어 턴 중이면 적용).
            RePlanAllEnemyTurns();
        }

        // ── 아이템 타겟팅 모드 ────────────────────────────────────────────

        /// <summary>현재 어떤 아이템 타겟팅 모드라도 활성화 중인지 여부 (ConfirmUseItemPanel에서 참조).</summary>
        public bool IsItemTargetingActive =>
            isItemMoveMode || isItemTeleportMode || isItemEnemyTargetingMode || isItemAreaTargetingMode;

        /// <summary>아무 BoardController에서든 아이템 타겟팅 모드가 활성 중인지 — CardUI 등에서 카드 상호작용 차단용.</summary>
        public static bool AnyItemTargetingActive { get; internal set; }

        /// <summary>타겟팅이 시작된 아이템 슬롯을 등록합니다. 슬롯은 숨기지 않고 클릭 시 취소 가능 상태로 유지됩니다.</summary>
        public void SetPendingItemSlot(ItemSlotUI slot)
        {
            _pendingItemSlot = slot;
            // itemTargetingBlocker는 보드 셀 hover까지 막아서 비활성화 — 카드 입력 차단은
            // CardUI의 AnyItemTargetingActive 검사로 대체됨.
            // InGameUIController.Instance?.SetItemTargetingBlocker(true);
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
            AnyItemTargetingActive   = true;
            _itemEnemyCallback        = onSelected;
            _itemTargetCancelCallback = onCancel;
            _itemConfirmedUseCallback = onConfirmedUse;
            ClearAllHighlights();
            // 적 행동 미리보기(enemyRangePreview)는 유지 — 아이템 사용 모드 중에도 보여야 한다.
            ClearItemHoverPreview();

            // 살아있는 적 셀 하이라이트 (TargetableEnemy)
            foreach (var e in enemies)
                if (e.CurrentHP > 0 && IsInBoard(e.GridPos))
                    grid[e.GridPos.x, e.GridPos.y].SetState(CellState.TargetableEnemy);

            // all-tile preview가 이미 hover 처리를 하고 있으면 구식 핸들러 구독 생략
            if (_itemAllTilePersistent.Count == 0)
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
            AnyItemTargetingActive   = true;
            _itemAreaCallback         = onSelected;
            _itemTargetCancelCallback = onCancel;
            _itemConfirmedUseCallback = onConfirmedUse;
            ClearAllHighlights();
            // 적 행동 미리보기(enemyRangePreview)는 유지
            ClearItemHoverPreview();

            // 보드 전체 셀 AreaSelectable
            for (int col = 0; col < boardCols; col++)
                for (int row = 0; row < boardRows; row++)
                    grid[col, row].SetState(CellState.AreaSelectable);

            // all-tile preview가 이미 hover 처리를 하고 있으면 구식 핸들러 구독 생략
            if (_itemAllTilePersistent.Count == 0)
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
            AnyItemTargetingActive   = false;

            UnsubscribeCellHoverForItemTargeting();
            ClearItemHoverPreview();
            ClearItemAllTilePreview();
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
            AnyItemTargetingActive   = false;
            UnsubscribeCellHoverForItemTargeting();
            ClearItemHoverPreview();
            ClearItemAllTilePreview();
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
            AnyItemTargetingActive  = false;
            UnsubscribeCellHoverForItemTargeting();
            ClearItemHoverPreview();
            ClearItemAllTilePreview();
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
            // 사용/취소가 끝났으니 attack 미리보기도 정리
            ClearItemAllTilePreview();
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
                if (cell.OccupyingEnemy != null && attackRangeSprite != null)
                    AddPreviewObject(cell.GridPos, attackRangeSprite, itemHoverPreviewObjects);
            }
            else if (isItemAreaTargetingMode)
            {
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
            DeckRoguelike.UI.InGameUIController.Instance?.SetPlayerEffect("fire", fireIcon, fire.Stacks, "화염", "매 턴 시작 시 스택만큼 피해를 받습니다.");
            Debug.Log($"[CombatController] 플레이어 화염 피해: {fire.Stacks}");
        }

        private void ProcessEnemyFireStatus(EnemyInstance enemy)
        {
            var fire = enemy.StatusEffects.Find(s => s.Type == StatusEffectType.Fire);
            if (fire == null || fire.Stacks <= 0) return;
            DamageEnemy(enemy, fire.Stacks);
        }

        /// <summary>기절·빙결·공포 처리. 해당 상태면 true 반환 (이번 턴 스킵).</summary>
        private bool ConsumeEnemyStun(EnemyInstance enemy)
        {
            for (int i = enemy.StatusEffects.Count - 1; i >= 0; i--)
            {
                var s = enemy.StatusEffects[i];
                if (s.Type != StatusEffectType.Stun
                    && s.Type != StatusEffectType.Freeze
                    && s.Type != StatusEffectType.Fear) continue;
                s.Stacks--;
                string key = s.Type switch
                {
                    StatusEffectType.Stun   => "stun",
                    StatusEffectType.Freeze => "freeze",
                    _                       => "fear"
                };
                if (s.Stacks <= 0)
                {
                    enemy.StatusEffects.RemoveAt(i);
                    // 적 상태이상 제거 (데이터만)
                }
                else
                {
                    Sprite icon = s.Type switch
                    {
                        StatusEffectType.Stun   => stunIcon,
                        StatusEffectType.Freeze => freezeIcon,
                        _                       => (fearIcon != null ? fearIcon : stunIcon)
                    };
                    // 적 상태이상 데이터만 저장
                }
                Debug.Log($"[CombatController] {enemy.Name} 기절/빙결/공포로 턴 스킵");
                return true;
            }
            return false;
        }

        /// <summary>모든 적의 속박(Bondage) 스택을 1씩 감소시키고 0이 되면 제거합니다.
        /// 적 턴 종료 시 1회 호출 — 속박은 이번 턴 이동을 막은 뒤 소모됩니다.</summary>
        private void DecrementEnemyBondage()
        {
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                for (int i = enemy.StatusEffects.Count - 1; i >= 0; i--)
                {
                    var s = enemy.StatusEffects[i];
                    if (s.Type != StatusEffectType.Bondage) continue;
                    s.Stacks--;
                    if (s.Stacks <= 0) enemy.StatusEffects.RemoveAt(i);
                }
            }
        }

        #endregion

        #region UI Updates

        private void UpdateBlockDisplay()
        {
            int currentHP = GameManager.Instance != null ? GameManager.Instance.CurrentHP : 1;
            int maxHP     = GameManager.Instance != null ? GameManager.Instance.MaxHP     : 1;
            unitUI?.UpdateShield(playerBlock, currentHP, maxHP);
        }

        private void UpdatePileCounters()
        {
            // Shop/픽 모드에서는 카운트 대신 RangeIndicator/페이지 전환 라벨이 표시된다.
            ApplyPileButtonLabels();
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

        // ── Pile button binding (Shop/RewardPick에서 역할 교체) ────────
        // Default      : drawPile = DrawPile 뷰어,        discardPile = DiscardPile 뷰어
        // Shop         : drawPile = RangeIndicator,       discardPile = 다음 페이지 (endButton 대체)
        // ShopEvent    : drawPile = RangeIndicator,       discardPile = DiscardPile 뷰어 (페이지 없음)
        // RewardPick   : drawPile = RangeIndicator,       discardPile = DiscardPile 뷰어
        private enum PileButtonMode { Default, Shop, ShopEvent, RewardPick }
        private PileButtonMode _pileButtonMode = PileButtonMode.Default;

        /// <summary>현재 상태에 맞춰 drawPile/discardPile 버튼의 onClick listener와 interactable, 라벨을 한번에 셋업.</summary>
        private void SetPileButtonMode(PileButtonMode mode)
        {
            _pileButtonMode = mode;
            if (drawPileButton != null)
            {
                drawPileButton.onClick.RemoveAllListeners();
                if (mode == PileButtonMode.Default)
                    drawPileButton.onClick.AddListener(ShowDrawPile);
                else
                    drawPileButton.onClick.AddListener(OnCombatViewClicked);
                // RangeIndicator 모드에서는 카드 sticky 전까지 비활성, 그 외엔 뷰어로 항상 활성.
                drawPileButton.interactable = (mode == PileButtonMode.Default);
            }
            if (discardPileButton != null)
            {
                discardPileButton.onClick.RemoveAllListeners();
                if (mode == PileButtonMode.Shop)
                    discardPileButton.onClick.AddListener(OnShopPageSwitch);
                else if (mode == PileButtonMode.RewardPick)
                    discardPileButton.onClick.AddListener(OnRewardPickSkipClicked);
                else
                    discardPileButton.onClick.AddListener(ShowDiscardPile);
                discardPileButton.interactable = true;
            }
            ApplyPileButtonLabels();
        }

        /// <summary>drawPile/discardPile 버튼 라벨을 현재 모드에 맞춰 갱신.
        /// Shop: drawPile="Range Indicator", discardPile=페이지 라벨(아이템,유물 보기 / 카드 보기).
        /// ShopEvent + RewardPick: drawPile="Range Indicator", discardPile=count.
        /// Default: 둘 다 count.</summary>
        private void ApplyPileButtonLabels()
        {
            if (drawPileCount != null)
            {
                if (_pileButtonMode != PileButtonMode.Default)
                    drawPileCount.text = Core.LocalizationManager.Get("ingame_range_indicator");
                else
                    drawPileCount.text = (deckManager != null ? deckManager.DrawPileCount : 0).ToString();
            }

            if (discardPileCount != null)
            {
                if (_pileButtonMode == PileButtonMode.Shop)
                {
                    string key = _shopPage == 0 ? "shop_view_item_relic" : "shop_view_card";
                    discardPileCount.text = Core.LocalizationManager.Get(key);
                }
                else if (_pileButtonMode == PileButtonMode.RewardPick)
                {
                    discardPileCount.text = Core.LocalizationManager.Get("ingame_skip");
                }
                else
                {
                    discardPileCount.text = (deckManager != null ? deckManager.DiscardPileCount : 0).ToString();
                }
            }
        }

        /// <summary>draw/discard pile 버튼을 강제로 활성화 (부모가 SetCombatHudActive(false)로 비활성화된 경우 대비).
        /// Shop/ShopEvent 진입 + 카드 보상 분배 시 사용.</summary>
        private void ForceActivateDeckPileButtons()
        {
            if (drawPileButton != null)    ForceActivateWithParents(drawPileButton.gameObject);
            if (discardPileButton != null) ForceActivateWithParents(discardPileButton.gameObject);
        }

        /// <summary>현재 상태에 따라 draw/discard pile 버튼을 위/아래로 슬라이드한다.
        /// 위(보임): Combat(PlayerTurn/EnemyTurn/Victory), Shop, 카드 보상 픽 모드(_rewardPickActive,
        ///          60003 사용 후 3장 중 선택하는 단계)만 해당.
        /// 아래(숨김): Rest/Treasure/MapState(전투 후 그냥 보상 받는 상태 포함) 등 그 외 전부.
        /// Note: "그냥 보상 상태"(gold/relic/item/cardreward 카드가 손패에 있을 뿐인 상태)는
        ///       drawPile/discardPile을 볼 이유가 없으므로 명시적으로 픽 모드만 위로 올린다.</summary>
        private void RefreshDeckPileButtonVisibility()
        {
            // 보스 유물 선택(_relicPickActive)은 넘기기가 없으므로 discard/draw pile 버튼을 숨긴(down) 상태로 둔다.
            bool shouldShow =
                (_rewardPickActive && !_relicPickActive) ||
                combatState == CombatState.PlayerTurn ||
                combatState == CombatState.EnemyTurn ||
                combatState == CombatState.Victory  ||
                combatState == CombatState.Shop;

            if (shouldShow)
            {
                ForceActivateDeckPileButtons();
                ShowDeckPileButtons();
            }
            else
            {
                HideDeckPileButtons();
            }
        }

        #endregion

        #region Rest Area

        private CardData _restHealCard;
        private CardData _restUpgradeCard;

        public void SpawnRestArea()
        {
            Debug.Log("[SpawnRestArea] 진입");
            CleanupNonCombatVisuals();
            pendingCard = null;
            pendingCardIndex = -1;
            combatState = CombatState.Rest;
            CombatBoardCell.ShopFreeClickEnabled = true;

            // 휴식 진입 시 유물 훅 (103 휴식의 부적 — HP +15). 상점 유물 104의 OnShopOpen과 대칭.
            if (GameManager.Instance != null)
                foreach (var relic in GameManager.Instance.Relics)
                    relic.OnRestEnter(GameManager.Instance);

            // Rest 노드: 보드를 재초기화하지 않고 기존 맵 보드/플레이어 위치를 그대로 유지.
            // (InitializeBoard + PlacePlayer를 호출하면 combat 보드 sprite로 바뀌고 player가 spawn 위치로 이동함)
            SetCombatHudActive(false);

            // Rest 노드: EndButton 카메라 밖, draw/discard도 카메라 밖
            HideEndTurnButton();
            HideDeckPileButtons();
            SetPileButtonMode(PileButtonMode.Default);

            _restHealCard = null;
            _restUpgradeCard = null;
            DistributeRestCards();
        }

        private void DistributeRestCards()
        {
            if (deckManager != null)
            {
                foreach (var card in hand) deckManager.AddToDiscardPile(card);
                hand.Clear();
                ClearHandVisuals();
                deckManager.MoveDiscardToDrawPile();
            }

            _rewardCards.Clear();

            var gm = GameManager.Instance;
            bool blockHeal = gm != null && gm.HasRelic(911);    // 911 수면의 단절 — 휴식 회복 차단
            bool blockUpgrade = gm != null && gm.HasRelic(908); // 908 망각의 인장 — 휴식 강화 차단

            if (!blockHeal)
            {
                float healPercent = restHealPercent;
                if (gm != null)
                    foreach (var relic in gm.Relics)
                        relic.OnBeforeRestHeal(ref healPercent);

                int healAmount = gm != null ? Mathf.CeilToInt(gm.MaxHP * healPercent) : 0;

                // 50003은 Addressables에 등록되어 있지 않음 — 60003 사용.
                var healTemplate = CardRegistry.GetCard(60003);
                if (healTemplate != null)
                {
                    var healCard = healTemplate.Clone();
                    healCard.cardCode = 0; // 로컬라이제이션 키 충돌 방지 — override한 cardName/description이 표시되도록
                    healCard.cardName = "휴식";
                    healCard.description = $"HP를 {healAmount} 회복합니다.";
                    _rewardCards.Add(healCard);
                    _restHealCard = healCard;
                }
            }

            if (!blockUpgrade)
            {
                // 50003은 Addressables에 등록되어 있지 않음 — 60003 사용.
                var upgradeTemplate = CardRegistry.GetCard(60003);
                if (upgradeTemplate != null)
                {
                    var upgradeCard = upgradeTemplate.Clone();
                    upgradeCard.cardCode = 0; // 로컬라이제이션 키 충돌 방지
                    upgradeCard.cardName = "카드 강화";
                    upgradeCard.description = "덱에서 카드 1장을 강화합니다.";
                    _rewardCards.Add(upgradeCard);
                    _restUpgradeCard = upgradeCard;
                }
            }

            // 맵 이동 카드 한 장 — 휴식을 마치고 다음 노드로 이동하기 위해 지급.
            AddMapMoveRewardCard();

            Debug.Log($"[DistributeRestCards] _rewardCards.Count={_rewardCards.Count} (heal={_restHealCard != null}, upgrade={_restUpgradeCard != null}), handContainerActive={(handContainer != null && handContainer.gameObject.activeInHierarchy)}");

            // handContainer가 비활성이면 새 카드 인스턴스도 inactive로 생성되어 보이지 않음 — 보장
            if (handContainer != null && !handContainer.gameObject.activeSelf)
                handContainer.gameObject.SetActive(true);

            // Rest 카드는 덱을 거치지 않고 손패에 직접 배치한다.
            // (덱→DrawShopCards 경로에서 cardCode가 동일한 두 클론이 한 장만 그려지는 이슈 우회)
            foreach (var card in _rewardCards)
            {
                if (hand.Count >= 10) break;
                hand.Add(card);
                GameObject cardObj = CreateSingleCardObject(card);
                if (cardObj == null)
                {
                    Debug.LogWarning($"[DistributeRestCards] CreateSingleCardObject 반환 null — cardPrefab/handContainer 확인 필요");
                    continue;
                }
                cardObj.SetActive(true);
                handCardObjects.Add(cardObj);
            }
            RepositionHandCards();
            UpdatePileCounters();
            RefreshCardPlayability();
            Debug.Log($"[DistributeRestCards] 종료 — hand.Count={hand.Count}, handCardObjects.Count={handCardObjects.Count}");
        }

        private void HandleRestCardPlay(int handIndex)
        {
            if (handIndex < 0 || handIndex >= hand.Count) return;

            CardData card = hand[handIndex];
            var gm = GameManager.Instance;

            if (card == _restHealCard)
            {
                float healPercent = restHealPercent;
                if (gm != null)
                    foreach (var relic in gm.Relics)
                        relic.OnBeforeRestHeal(ref healPercent);

                int healAmount = gm != null ? Mathf.CeilToInt(gm.MaxHP * healPercent) : 0;
                gm?.Heal(healAmount);

                if (gm != null)
                    foreach (var relic in gm.Relics)
                        relic.OnAfterRestHeal(gm);

                Debug.Log($"[Rest] HP {healAmount} 회복");

                // 215 유물 — 휴식과 강화를 둘 다 사용할 수 있도록.
                bool fullAccess = gm != null && gm.HasRelic(215);
                _restHealCard = null;
                if (handIndex >= 0 && handIndex < hand.Count) hand.RemoveAt(handIndex);
                if (handIndex >= 0 && handIndex < handCardObjects.Count)
                {
                    var obj = handCardObjects[handIndex];
                    // MarkAsPlayed: sticky 잔여 상태(interactingCard 등) 정리 — 다음 카드 클릭이 한 번에 동작하도록.
                    var ui = obj != null ? obj.GetComponent<CardUI>() : null;
                    ui?.MarkAsPlayed();
                    handCardObjects.RemoveAt(handIndex);
                    if (obj != null) Destroy(obj);
                }
                // 215 없으면 강화 카드도 함께 소진. 맵 이동 카드는 보존.
                // 215 있으면 강화 카드는 손패에 남겨 두 카드 모두 사용 가능 (필드 reference 유지).
                if (!fullAccess)
                {
                    ExhaustSpecificRewardCard(_restUpgradeCard);
                    _restUpgradeCard = null;
                }
                RepositionHandCards();
                // OnCardDown에서 SetCombatButtonsInteractable(false)로 막힌 버튼/사이드 입력 복원 + sticky 상태 정리.
                pendingCard = null;
                pendingCardIndex = -1;
                SetCombatButtonsInteractable(true);
                // 맵 이동 카드로만 Rest를 떠난다 — OnRestLeave 호출 안 함 (이전엔 맵 이동 카드까지 함께 사라졌음).
            }
            else if (card == _restUpgradeCard)
            {
                Debug.Log("[Rest] 카드 강화 선택");
                if (handIndex < handCardObjects.Count)
                {
                    var ui = handCardObjects[handIndex]?.GetComponent<CardUI>();
                    ui?.Deselect();
                }
                // 강화 패널을 여는 동안 sticky/버튼 상태 복원 — 이후 클릭이 한 번에 동작하도록.
                pendingCard = null;
                pendingCardIndex = -1;
                SetCombatButtonsInteractable(true);
                OpenRestCardUpgrade();
            }
        }

        /// <summary>특정 reward 카드 한 장을 손패에서 제거하고 소멸 더미로 보낸다. null/미존재면 무시.</summary>
        private void ExhaustSpecificRewardCard(CardData target)
        {
            if (target == null) return;
            int idx = hand.IndexOf(target);
            if (idx >= 0)
            {
                hand.RemoveAt(idx);
                if (idx < handCardObjects.Count)
                {
                    var obj = handCardObjects[idx];
                    var ui = obj != null ? obj.GetComponent<CardUI>() : null;
                    ui?.MarkAsPlayed();
                    handCardObjects.RemoveAt(idx);
                    if (obj != null) Destroy(obj);
                }
            }
            deckManager?.ExhaustCard(target);
            _rewardCards.Remove(target);
        }

        private void OpenRestCardUpgrade()
        {
            if (shopCardListController != null)
            {
                shopCardListController.gameObject.SetActive(true);
                shopCardListController.Setup(RestCardMode.Upgrade, cardConfirmPanel, onFinish: OnRestUpgradeConfirmed);
                // backButton이 활성화되도록 panelStack에 등록
                InGameUIController.Instance?.PushPanel("RestCardList", () =>
                {
                    if (shopCardListController != null) shopCardListController.gameObject.SetActive(false);
                    OnRestUpgradeCancelled();
                });
            }
            else
            {
                InGameUIController.Instance?.OpenRestCardList(RestCardMode.Upgrade, cardConfirmPanel, OnRestUpgradeConfirmed, OnRestUpgradeCancelled);
            }
        }

        private void OnRestUpgradeConfirmed()
        {
            Debug.Log("[Rest] 카드 강화 완료");
            InGameUIController.Instance?.UnregisterPanel("RestCardList");
            if (shopCardListController != null) shopCardListController.gameObject.SetActive(false);

            // 215 유물 — 휴식과 강화를 둘 다 사용할 수 있도록.
            var gm = GameManager.Instance;
            bool fullAccess = gm != null && gm.HasRelic(215);
            int upgradeIdx = _restUpgradeCard != null ? hand.IndexOf(_restUpgradeCard) : -1;
            CardData upgradeCard = _restUpgradeCard;
            _restUpgradeCard = null;
            if (upgradeIdx >= 0 && upgradeIdx < hand.Count) hand.RemoveAt(upgradeIdx);
            if (upgradeIdx >= 0 && upgradeIdx < handCardObjects.Count)
            {
                var obj = handCardObjects[upgradeIdx];
                var ui = obj != null ? obj.GetComponent<CardUI>() : null;
                ui?.MarkAsPlayed();
                handCardObjects.RemoveAt(upgradeIdx);
                if (obj != null) Destroy(obj);
            }
            _rewardCards.Remove(upgradeCard);
            // 215 없으면 휴식 카드도 함께 소진. 맵 이동 카드는 보존.
            // 215 있으면 휴식 카드는 손패에 남겨 두 카드 모두 사용 가능 (필드 reference 유지).
            if (!fullAccess)
            {
                ExhaustSpecificRewardCard(_restHealCard);
                _restHealCard = null;
            }
            RepositionHandCards();
            // sticky 잔여 상태 정리 — 강화 패널 종료 후 다음 카드 클릭이 한 번에 동작하도록.
            pendingCard = null;
            pendingCardIndex = -1;
            SetCombatButtonsInteractable(true);
            // 맵 이동 카드로만 Rest를 떠난다 — OnRestLeave 호출 안 함.
        }

        private void OnRestUpgradeCancelled()
        {
            Debug.Log("[Rest] 카드 강화 취소");
            InGameUIController.Instance?.UnregisterPanel("RestCardList");
            if (shopCardListController != null) shopCardListController.gameObject.SetActive(false);
        }

        private void OnRestLeave()
        {
            CombatBoardCell.ShopFreeClickEnabled = false;
            RefreshAllCellsInteractable();
            ExhaustRewardCards();
            _restHealCard = null;
            _restUpgradeCard = null;
            // sticky/pending 잔류 방지
            pendingCard = null;
            pendingCardIndex = -1;
            HideEndTurnButton();
            HideDeckPileButtons();
            InGameUIController.Instance?.OpenMap();
        }

        #endregion

        #region Treasure Area

        public void SpawnTreasureArea()
        {
            Debug.Log("[SpawnTreasureArea] 진입");
            CleanupNonCombatVisuals();
            pendingCard = null;
            pendingCardIndex = -1;
            combatState = CombatState.Treasure;
            CombatBoardCell.ShopFreeClickEnabled = true;
            SetCombatHudActive(false);

            HideEndTurnButton();
            HideDeckPileButtons();
            SetPileButtonMode(PileButtonMode.Default);

            DistributeTreasureCards();
        }

        private void DistributeTreasureCards()
        {
            if (deckManager != null)
            {
                foreach (var card in hand) deckManager.AddToDiscardPile(card);
                hand.Clear();
                ClearHandVisuals();
                deckManager.MoveDiscardToDrawPile();
            }

            _rewardCards.Clear();

            // 골드 카드 (60000) — 45~55 골드 baked-in
            var goldTemplate = CardRegistry.GetCard(60000);
            if (goldTemplate != null && goldTemplate.effects != null && goldTemplate.effects.Count > 0)
            {
                var gc = goldTemplate.Clone();
                int goldAmount = UnityEngine.Random.Range(45, 56);
                gc.description = gc.description.Replace("{G}", goldAmount.ToString());
                gc.effects[0].value = goldAmount;
                _rewardCards.Add(gc);
            }

            // 유물 카드 (60001) — Act별 가중치로 유물 선정 + _pendingRewardRelic 세팅
            var relic = InGameUIController.Instance?.PickRewardRelic(isBoss: false);
            if (relic != null)
            {
                var relicTemplate = CardRegistry.GetCard(60001);
                if (relicTemplate != null)
                {
                    var rc = relicTemplate.Clone();
                    rc.cardName    = relic.relicName;
                    rc.description = relic.description;
                    rc.iconCode    = relic.relicCode;
                    _pendingRewardRelic = relic;
                    _rewardCards.Add(rc);
                }
            }

            // 맵 이동 카드 — 다음 노드로 이동
            AddMapMoveRewardCard();

            if (handContainer != null && !handContainer.gameObject.activeSelf)
                handContainer.gameObject.SetActive(true);

            // Rest와 동일하게 덱을 거치지 않고 손패에 직접 배치
            foreach (var card in _rewardCards)
            {
                if (hand.Count >= 10) break;
                hand.Add(card);
                GameObject cardObj = CreateSingleCardObject(card);
                if (cardObj == null) continue;
                cardObj.SetActive(true);
                handCardObjects.Add(cardObj);
            }
            RepositionHandCards();
            UpdatePileCounters();
            RefreshCardPlayability();
            Debug.Log($"[DistributeTreasureCards] 종료 — hand.Count={hand.Count}");
        }

        private void OnTreasureLeave()
        {
            CombatBoardCell.ShopFreeClickEnabled = false;
            RefreshAllCellsInteractable();
            ExhaustRewardCards();
            pendingCard = null;
            pendingCardIndex = -1;
            HideEndTurnButton();
            HideDeckPileButtons();
            InGameUIController.Instance?.OpenMap();
        }

        #endregion

        #region Merchant Shop

        public void SpawnMerchant()
        {
            Debug.Log($"[SpawnMerchant] 진입 — gridNull={grid == null} playerSpawnCell={playerSpawnCell} playerObject={(playerObject != null ? playerObject.activeSelf.ToString() : "null")}");
            CleanupNonCombatVisuals();
            pendingCard = null;
            pendingCardIndex = -1;
            combatState = CombatState.Shop;
            CombatBoardCell.ShopFreeClickEnabled = true;
            _currentShopEventCode = 0; // 일반 상점 — 상점형 이벤트 상태 초기화 (안전망)
            _currentShopPos = mapPlayerPos; // shop 위치 기록 (캐시 키)
            // Shop 노드: 보드/player를 재초기화하지 않고 기존 맵 보드를 그대로 유지
            SetCombatHudActive(false);
            // SetCombatHudActive(false)가 handContainer를 가렸을 수 있음 — 카드가 보이도록 재활성화
            if (handContainer != null) ForceActivateWithParents(handContainer.gameObject);

            // Shop 진입 — endTurn은 카메라 밖, draw/discard 버튼이 RangeIndicator/페이지 전환 역할로 active 위치.
            HideEndTurnButton();
            ForceActivateDeckPileButtons();
            ShowDeckPileButtons();
            SetPileButtonMode(PileButtonMode.Shop);

            if (GameManager.Instance != null)
                foreach (var relic in GameManager.Instance.Relics)
                    relic.OnShopOpen(GameManager.Instance);

            _shopPage = 0;
            ClearShopState();

            // 결정론적 진열을 위해 상점 시드로 Random 재시드 (게임 다른 곳의 Random 흐름 보존을 위해 상태 push/pop)
            var prevRngState = UnityEngine.Random.state;
            int shopSeed = Core.GameManager.Instance?.GetCurrentShopSeed() ?? 0;
            UnityEngine.Random.InitState(shopSeed);
            try
            {
                DistributeShopPage1();
            }
            finally
            {
                UnityEngine.Random.state = prevRngState;
            }
        }

        private void OnMerchantLeave()
        {
            GameManager.Instance?.MarkLastNodeShop();
            // 상점을 떠나는 시점에 방문 카운트 증가 (다음 상점은 다음 시드 사용)
            GameManager.Instance?.IncrementShopVisit();
            CombatBoardCell.ShopFreeClickEnabled = false;
            RefreshAllCellsInteractable();
            if (_drawShopCardsCoroutine != null) { StopCoroutine(_drawShopCardsCoroutine); _drawShopCardsCoroutine = null; }
            ExhaustRewardCards();
            ClearShopState();
            // sticky/pending 상태 잔류 시 다음 전투에서 ShowCardRangePreview가 차단됨 — 초기화
            pendingCard = null;
            pendingCardIndex = -1;
            _shopSelectedCard = null;
            HideEndTurnButton();
            HideDeckPileButtons();
            SetPileButtonMode(PileButtonMode.Default);
            InGameUIController.Instance?.OpenMap();
        }

        private void OnShopPageSwitch()
        {
            Debug.Log($"[OnShopPageSwitch] 진입 — 현재 _shopPage={_shopPage}, hand.Count={hand.Count}, _rewardCards.Count={_rewardCards.Count} pendingCard={(pendingCard != null ? pendingCard.cardName : "null")} pendingCardIndex={pendingCardIndex}");
            // 페이지 전환 전에 sticky/pending 상태 명시적 정리 — OnCardCollapseEnter 가 다음 페이지 카드에서
            // 정상적으로 발화되도록 보장 (특히 _wasCollapsed/CanPlayWhileSticky는 per-CardUI이지만,
            // pendingCard/pendingCardIndex/_shopSelectedCard 는 BoardController 필드라서 잔류 시 다음 카드 흐름에 영향)
            if (pendingCardIndex >= 0 && pendingCardIndex < handCardObjects.Count)
            {
                var stuckUI = handCardObjects[pendingCardIndex]?.GetComponent<DeckRoguelike.Cards.CardUI>();
                if (stuckUI != null) { stuckUI.SetLocked(false); stuckUI.Deselect(); }
            }
            pendingCard = null;
            pendingCardIndex = -1;
            _shopSelectedCard = null;
            // sticky 해제 → drawPile(RangeIndicator) 비활성화. listener는 Shop mode 유지.
            if (drawPileButton != null) drawPileButton.interactable = false;

            ExhaustRewardCards();
            ClearShopState();
            // 같은 shop 안의 페이지 전환 — 캐시에서 재구성. Random 재시드 안 함.
            _shopPage++;
            if (_shopPage == 1)
            {
                Debug.Log("[OnShopPageSwitch] → DistributeShopPage2");
                DistributeShopPage2();
            }
            else
            {
                _shopPage = 0;
                Debug.Log("[OnShopPageSwitch] → DistributeShopPage1");
                DistributeShopPage1();
            }
            // 페이지 라벨 갱신 (discardPile = "아이템,유물 보기" / "카드 보기").
            ApplyPileButtonLabels();
        }

        private void ClearShopState()
        {
            _shopCardPrices.Clear();
            _shopCardDiscounted.Clear();
            _shopRelicMap.Clear();
            _shopItemMap.Clear();
            _shopEventOriginCards.Clear();
            _shopSelectedCard = null;
            if (drawPileButton != null) drawPileButton.interactable = false;
        }

        // ── Page 1: 공통 + 직업 카드 ─────────────────────────────────

        private void DistributeShopPage1()
        {
            Debug.Log($"[DistributeShopPage1] 진입 — hand.Count={hand.Count} _rewardCards.Count={_rewardCards.Count}");
            if (deckManager != null)
            {
                foreach (var card in hand) deckManager.AddToDiscardPile(card);
                hand.Clear();
                ClearHandVisuals();
                deckManager.MoveDiscardToDrawPile();
            }

            _rewardCards.Clear();

            // 캐시 확인 — 같은 shop 위치에 page1 캐시가 있으면 재사용
            ShopInventory cache = GetOrCreateShopInventory();
            if (cache.page1Cards.Count > 0)
            {
                RestoreShopPageFromCache(cache, cache.page1Cards);
                Debug.Log($"[DistributeShopPage1] 캐시 복원 — {cache.page1Cards.Count}장");
            }
            else
            {
                var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
                var pool = CardRegistry.GetRewardPool(character);

                // 공통 카드(ClassDigit==1)는 1page에서 제외. 직업 카드만 사용.
                var classCommon   = new List<CardData>();
                var classUncommon = new List<CardData>();
                var classRare     = new List<CardData>();
                foreach (var card in pool)
                {
                    if (card.ClassDigit == 1) continue;
                    switch (card.Rarity)
                    {
                        case CardRarity.Common:   classCommon.Add(card);   break;
                        case CardRarity.Uncommon: classUncommon.Add(card); break;
                        case CardRarity.Rare:     classRare.Add(card);     break;
                    }
                }
                Shuffle(classCommon);
                Shuffle(classUncommon);
                Shuffle(classRare);

                AddShopClassCards(classCommon,   shopClassCommonCount);
                AddShopClassCards(classUncommon, shopClassUncommonCount);
                AddShopClassCards(classRare,     shopClassRareCount);

                // page1 전체에서 1장만 50% 할인
                ApplyPage1SingleDiscount();

                // 맵 이동 카드 1장 무료 지급 (가격 0)
                AddShopMapMoveCard();

                // 캐시에 저장
                cache.page1Cards = new List<CardData>(_rewardCards);
                foreach (var c in _rewardCards)
                {
                    if (_shopCardPrices.TryGetValue(c, out var p))     cache.prices[c] = p;
                    if (_shopCardDiscounted.TryGetValue(c, out var d)) cache.discounted[c] = d;
                    if (_shopRelicMap.TryGetValue(c, out var r))       cache.relicMap[c] = r;
                }
                Debug.Log($"[DistributeShopPage1] 셔플 후 선정된 카드 [{string.Join(", ", _rewardCards.Select(c => c.cardName))}]");
            }

            if (deckManager != null)
                for (int i = _rewardCards.Count - 1; i >= 0; i--)
                    deckManager.AddToTopOfDrawPile(_rewardCards[i]);

            if (_drawShopCardsCoroutine != null) StopCoroutine(_drawShopCardsCoroutine);
            _drawShopCardsCoroutine = StartCoroutine(DrawShopCards(_rewardCards.Count));
        }

        private ShopInventory GetOrCreateShopInventory()
        {
            if (!_shopInventoryCache.TryGetValue(_currentShopPos, out var cache))
            {
                cache = new ShopInventory();
                _shopInventoryCache[_currentShopPos] = cache;
            }
            return cache;
        }

        /// <summary>캐시된 page 카드 목록을 _rewardCards/_shopCardPrices/_shopCardDiscounted/_shopRelicMap/_shopItemMap 에 복원.</summary>
        private void RestoreShopPageFromCache(ShopInventory cache, List<CardData> pageCards)
        {
            foreach (var c in pageCards)
            {
                if (cache.consumed.Contains(c)) continue; // 이미 구매한 카드는 제외
                _rewardCards.Add(c);
                if (cache.prices.TryGetValue(c, out var p))      _shopCardPrices[c] = p;
                if (cache.discounted.TryGetValue(c, out var d))  _shopCardDiscounted[c] = d;
                if (cache.relicMap.TryGetValue(c, out var r))    _shopRelicMap[c] = r;
                if (cache.itemMap.TryGetValue(c, out var it))    _shopItemMap[c] = it;
                if (cache.eventOriginCards.Contains(c))          _shopEventOriginCards.Add(c);
            }
        }

        private void AddShopClassCards(List<CardData> pool, int maxCount)
        {
            int count = Mathf.Min(maxCount, pool.Count);
            for (int i = 0; i < count; i++)
            {
                var card = pool[i].Clone();
                int rarityIdx = Mathf.Clamp((int)card.Rarity, 0, ShopCardPriceRanges.Length - 1);
                int price = ApplyShopDiscount(RollPriceInRange(ShopCardPriceRanges[rarityIdx]));

                _rewardCards.Add(card);
                _shopCardPrices[card] = price;
                _shopCardDiscounted[card] = false;
            }
        }

        // 상점에서 무료 지급되는 맵 이동 카드. _shopCardPrices에 등록하지 않아 "0G" 가격이 표시되지 않는다.
        // TryPlayCard가 IsMapMoveCard 분기로 HandleShopCardPlay를 우회하므로 가격 등록 없이도 사용 가능.
        private void AddShopMapMoveCard()
        {
            var card = CreateMapMoveCard();
            if (card == null) return;
            _rewardCards.Add(card);
        }

        // page1 전체 _rewardCards 중 1장만 50% 할인. 가격은 이미 ApplyShopDiscount가 적용된 상태이므로 그 위에 추가로 1/2.
        private void ApplyPage1SingleDiscount()
        {
            if (_rewardCards.Count == 0) return;
            int idx = UnityEngine.Random.Range(0, _rewardCards.Count);
            var card = _rewardCards[idx];
            if (_shopCardPrices.TryGetValue(card, out int p))
            {
                _shopCardPrices[card] = Mathf.Max(1, p / 2);
                _shopCardDiscounted[card] = true;
            }
        }

        // ── Page 2: 유물 + 아이템 + 카드 제거 ────────────────────────

        private void DistributeShopPage2()
        {
            Debug.Log($"[DistributeShopPage2] 진입 — hand.Count={hand.Count} _rewardCards.Count={_rewardCards.Count}");
            if (deckManager != null)
            {
                foreach (var card in hand) deckManager.AddToDiscardPile(card);
                hand.Clear();
                ClearHandVisuals();
                deckManager.MoveDiscardToDrawPile();
            }

            _rewardCards.Clear();

            ShopInventory cache = GetOrCreateShopInventory();
            if (cache.page2Cards.Count > 0)
            {
                RestoreShopPageFromCache(cache, cache.page2Cards);
                Debug.Log($"[DistributeShopPage2] 캐시 복원 — {cache.page2Cards.Count}장");
            }
            else
            {
                GenerateShopRelicCards();
                GenerateShopItemCards();
                // 맵 이동 카드 1장 무료 지급 (가격 0)
                AddShopMapMoveCard();
                cache.page2Cards = new List<CardData>(_rewardCards);
                foreach (var c in _rewardCards)
                {
                    if (_shopCardPrices.TryGetValue(c, out var p))     cache.prices[c] = p;
                    if (_shopCardDiscounted.TryGetValue(c, out var d)) cache.discounted[c] = d;
                    if (_shopRelicMap.TryGetValue(c, out var r))       cache.relicMap[c] = r;
                    if (_shopItemMap.TryGetValue(c, out var it))       cache.itemMap[c] = it;
                }
                Debug.Log($"[DistributeShopPage2] 생성된 상품 [{string.Join(", ", _rewardCards.Select(c => c.cardName))}]");
            }

            if (deckManager != null)
                for (int i = _rewardCards.Count - 1; i >= 0; i--)
                    deckManager.AddToTopOfDrawPile(_rewardCards[i]);

            if (_drawShopCardsCoroutine != null) StopCoroutine(_drawShopCardsCoroutine);
            _drawShopCardsCoroutine = StartCoroutine(DrawShopCards(_rewardCards.Count));
        }

        private void GenerateShopRelicCards()
        {
            RelicLibrary.RegisterAll();
            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;

            HashSet<int> owned = null;
            if (GameManager.Instance != null)
            {
                owned = new HashSet<int>();
                foreach (var r in GameManager.Instance.Relics)
                    if (r?.Data != null) owned.Add(r.Data.relicCode);
            }

            var normalPool = RelicRegistry.GetByType(bossRelic: false);
            normalPool.RemoveAll(r => r.IsStartingRelic || r.IsShopOnlyRelic);
            if (owned != null)
            {
                normalPool.RemoveAll(r => !r.IsForCharacter(character));
                normalPool.RemoveAll(r => owned.Contains(r.relicCode));
            }
            Shuffle(normalPool);

            var shopPool = RelicRegistry.GetByType(bossRelic: false);
            shopPool.RemoveAll(r => !r.IsShopOnlyRelic);
            if (owned != null)
            {
                shopPool.RemoveAll(r => !r.IsForCharacter(character));
                shopPool.RemoveAll(r => owned.Contains(r.relicCode));
            }
            Shuffle(shopPool);

            // shop-only 1장 + normal (shopRelicCount-1)장 구성. shop-only 풀이 비면 normal로 보충.
            bool shopOnlyAdded = false;
            if (shopPool.Count > 0)
            {
                AddShopRelicCard(shopPool[0]);
                shopOnlyAdded = true;
            }
            int normalTarget = shopOnlyAdded ? shopRelicCount - 1 : shopRelicCount;
            int normalCount = Mathf.Min(normalTarget, normalPool.Count);
            for (int i = 0; i < normalCount; i++)
                AddShopRelicCard(normalPool[i]);
            Debug.Log($"[GenerateShopRelicCards] shopRelicCount={shopRelicCount}, shopOnlyAdded={shopOnlyAdded}, normalCount={normalCount}, normalPool.Count={normalPool.Count}");
        }

        private void AddShopRelicCard(RelicData relic)
        {
            // 50001은 Addressables에 등록되어 있지 않음. 60001(Reward_Relic) 템플릿 사용.
            var template = CardRegistry.GetCard(60001);
            if (template == null) return;
            var card = template.Clone();
            card.cardName    = relic.relicName;
            card.description = relic.description;
            card.iconCode    = relic.relicCode;

            int price = ApplyShopDiscount(RollRelicPrice(relic));
            _rewardCards.Add(card);
            _shopCardPrices[card] = price;
            _shopCardDiscounted[card] = false;
            _shopRelicMap[card] = relic;
        }

        private void GenerateShopItemCards()
        {
            ItemLibrary.RegisterAll();
            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            var pool = ItemRegistry.GetForCharacter(character, bossItem: false);
            // 아이템 Rare(3xx, RarityIndex=2) 등급 폐지 — 상점 풀에서 제외.
            pool.RemoveAll(it => it == null || it.RarityIndex >= 2);
            Shuffle(pool);

            int count = Mathf.Min(shopItemCount, pool.Count);
            Debug.Log($"[GenerateShopItemCards] shopItemCount={shopItemCount}, pool.Count={pool.Count} → count={count}");
            for (int i = 0; i < count; i++)
            {
                var item = pool[i];
                // 50002는 Addressables에 없음. 60002(Reward_Item) 템플릿 사용.
                var template = CardRegistry.GetCard(60002);
                if (template == null) { Debug.LogWarning($"[GenerateShopItemCards] template 60002 null — i={i} skip"); continue; }
                var card = template.Clone();
                card.cardName    = item.itemName;
                card.description = item.description;
                card.iconCode    = item.itemCode;

                int price = ApplyShopDiscount(RollItemPrice(item));
                _rewardCards.Add(card);
                _shopCardPrices[card] = price;
                _shopCardDiscounted[card] = false;
                _shopItemMap[card] = item;
            }
        }

        // ── 상점 카드 드로우 (goldText 세팅 포함) ─────────────────────

        private IEnumerator DrawShopCards(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (hand.Count >= 10) break;
                CardData card = deckManager != null ? deckManager.DrawCard() : null;
                if (card == null) break;

                hand.Add(card);
                PlaySound(drawCardSound);

                GameObject cardObj = CreateSingleCardObject(card);
                handCardObjects.Add(cardObj);
                RepositionHandCards();

                CardUI cardUI = cardObj.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    if (_shopCardPrices.TryGetValue(card, out int price))
                    {
                        bool disc = _shopCardDiscounted.TryGetValue(card, out bool d) && d;
                        cardUI.SetShopPrice(price, disc);
                    }
                    if (drawPileButton != null)
                        cardUI.StartDrawAnimation(drawPileButton.transform.position, cardDrawDuration);
                }

                UpdatePileCounters();
                RefreshCardPlayability();
                yield return new WaitForSeconds(drawStaggerDelay);
            }
        }

        /// <summary>이벤트 카드 상품의 모든 effect를 CardEffectRegistry로 실행한다.
        /// (HandleShopCardPlay에서 deck add 대신 이 함수가 호출된다.)</summary>
        private void ExecuteEventOriginCardEffects(CardData card)
        {
            if (card?.Effects == null) return;
            var ctx = new DeckRoguelike.Combat.CardEffectContext
            {
                Board = this,
                Card  = card,
            };
            foreach (var fx in card.Effects)
            {
                if (fx == null) continue;
                if (string.IsNullOrEmpty(fx.customEffectId)) continue;
                DeckRoguelike.Combat.CardEffectRegistry.Execute(fx.customEffectId, ctx);
            }
        }

        // ── 상점 카드 플레이 처리 ─────────────────────────────────────

        private void HandleShopCardPlay(int handIndex)
        {
            Debug.Log($"[HandleShopCardPlay] 진입 handIndex={handIndex} hand.Count={hand.Count}");
            if (handIndex < 0 || handIndex >= hand.Count) return;

            CardData card = hand[handIndex];
            Debug.Log($"[HandleShopCardPlay] card={card.cardName} _shopCardPrices.Contains={_shopCardPrices.ContainsKey(card)} Gold={GameManager.Instance?.Gold}");

            if (!_shopCardPrices.TryGetValue(card, out int price))
            {
                Debug.LogWarning($"[Shop] 가격 정보 없음 — card={card.cardName} cardCode={card.cardCode}");
                return;
            }

            if (GameManager.Instance == null || !GameManager.Instance.SpendGold(price))
            {
                Debug.Log("[Shop] 골드 부족 — sticky 해제");
                DeselectPendingCard();
                return;
            }

            if (_shopEventOriginCards.Contains(card))
            {
                // 이벤트 카드 상품 (EventDataTemplate 출신) — deck에 추가하지 않고 customEffectId 실행.
                // 카드는 아래에서 강제 exhaust된다 (소멸 키워드 무관).
                Debug.Log($"[Shop] 이벤트 카드 효과 실행: {card.cardName} (-{price}G)");
                ExecuteEventOriginCardEffects(card);
            }
            else if (_shopRelicMap.TryGetValue(card, out var relic))
            {
                GameManager.Instance.AddRelic(relic.relicCode);
                Debug.Log($"[Shop] 유물 구매: {relic.relicName} (-{price}G)");
            }
            else if (_shopItemMap.TryGetValue(card, out var item))
            {
                GameManager.Instance.AddItem(item);
                Debug.Log($"[Shop] 아이템 구매: {item.itemName} (-{price}G)");
            }
            else
            {
                Debug.Log($"[Shop] 카드 구매: {card.cardName} (-{price}G)");
                // 403/503/603 유물이 있으면 전투 종료 보상과 동일하게 새 카드 3장 중 1장 선택
                bool hasCardRewardRelic = GameManager.Instance != null &&
                    (GameManager.Instance.HasRelic(403) || GameManager.Instance.HasRelic(503) || GameManager.Instance.HasRelic(603));

                if (hasCardRewardRelic)
                {
                    // 보상 풀에서 3장을 띄워 1장 선택 — subCardListController 기반.
                    var pickCards = BuildRewardPickCards();
                    if (pickCards != null && pickCards.Count > 0)
                        InGameUIController.Instance?.OpenCardPicker(pickCards, picked =>
                        {
                            if (picked != null) DeckManager.Instance?.AddCardToDeck(picked);
                        });
                    else
                        DeckManager.Instance?.AddCardToDeck(card);
                }
                else
                {
                    DeckManager.Instance?.AddCardToDeck(card);
                }
            }

            // 캐시에 구매됨으로 기록 — 같은 shop 재진입 시 이 카드는 다시 안 뜸
            if (_shopInventoryCache.TryGetValue(_currentShopPos, out var inv))
                inv.consumed.Add(card);
            // 상점형 이벤트 캐시에도 기록.
            if (_currentShopEventCode > 0 && _shopEventInventoryCache.TryGetValue(_currentShopEventCode, out var evInv))
                evInv.consumed.Add(card);

            // 손패에서 제거 및 시각 정리
            GameObject playedObj = (handIndex < handCardObjects.Count) ? handCardObjects[handIndex] : null;
            hand.RemoveAt(handIndex);
            if (handIndex < handCardObjects.Count) handCardObjects.RemoveAt(handIndex);
            _rewardCards.Remove(card);
            _shopCardPrices.Remove(card);
            _shopCardDiscounted.Remove(card);
            _shopRelicMap.Remove(card);
            _shopItemMap.Remove(card);
            _shopEventOriginCards.Remove(card);

            if (playedObj != null)
            {
                var ui = playedObj.GetComponent<CardUI>();
                ui?.MarkAsPlayed();
                Destroy(playedObj);
            }

            RepositionHandCards();
            RefreshCardPlayability();

            // 구매 완료 → sticky 상태/RangeIndicator 초기화
            // OnCardDown에서 SetCombatButtonsInteractable(false)로 endTurn/draw/discard 버튼의
            // blocksRaycasts를 끈 상태이므로 여기서 반드시 복구해야 한다.
            pendingCard = null;
            pendingCardIndex = -1;
            _shopSelectedCard = null;
            if (drawPileButton != null) drawPileButton.interactable = false;
            SetCombatButtonsInteractable(true);
        }

        /// <summary>CardInfoController 닫힐 때 호출 — lock은 유지(카드가 마우스 따라다니지 않게)하고 콤뱃 버튼 raycast만 복원.</summary>
        public void ReleaseShopCardLockIfAny()
        {
            Debug.Log($"[ReleaseShopCardLockIfAny] combatState={combatState} pendingCardIndex={pendingCardIndex} _rewardPickActive={_rewardPickActive}");
            // Shop / 카드 보상 픽 모두 sticky 카드 lock 유지, 콤뱃 버튼 raycast만 복원.
            if (combatState != CombatState.Shop && !_rewardPickActive) return;
            // lock은 풀지 않는다 — 풀면 Update의 mouse-tracking이 다시 켜져 카드가 포인터를 따라다님.
            // OnPointerDown의 PlayCard 분기는 isLocked를 체크하지 않으므로 mousedown으로 선택 가능.
            SetCombatButtonsInteractable(true);
        }

        private void OnCombatViewClicked()
        {
            // 카드 보상 픽 모드 우선 — sticky 픽 카드를 CardInfoController로 표시
            CardData target = (_rewardPickActive && _rewardPickViewCard != null)
                ? _rewardPickViewCard
                : _shopSelectedCard;
            Debug.Log($"[OnCombatViewClicked] target={(target != null ? target.cardName : "null")} _rewardPickActive={_rewardPickActive}");
            if (target == null) return;
            var ui = InGameUIController.Instance;
            if (ui == null)
            {
                Debug.LogWarning("[OnCombatViewClicked] InGameUIController.Instance == null");
                return;
            }
            ui.ShowRangeInfo(target);
        }

        // ── 상점 헬퍼 ─────────────────────────────────────────────────

        private static int ApplyShopDiscount(int basePrice)
        {
            if (GameManager.Instance != null && GameManager.Instance.HasRelic(400))
                return Mathf.Max(1, basePrice / 2);
            return basePrice;
        }

        private static int RollRelicPrice(RelicData relic)
        {
            int idx = Mathf.Clamp(relic.RarityIndex, 0, ShopRelicPriceRanges.Length - 1);
            return RollPriceInRange(ShopRelicPriceRanges[idx]);
        }

        private static int RollItemPrice(ItemData item)
        {
            int idx = Mathf.Clamp(item.RarityIndex, 0, ShopItemPriceRanges.Length - 1);
            return RollPriceInRange(ShopItemPriceRanges[idx]);
        }

        public void CleanupMerchant()
        {
            combatState = CombatState.NotStarted;
            ClearShopState();
            HideEndTurnButton();
            HideDeckPileButtons();
            SetPileButtonMode(PileButtonMode.Default);
            if (nonCombatPlayerObj != null) { Destroy(nonCombatPlayerObj); nonCombatPlayerObj = null; }

            InGameUIController.Instance?.OpenMap();
        }

        private void CleanupNonCombatVisuals()
        {
            if (nonCombatPlayerObj != null) { Destroy(nonCombatPlayerObj); nonCombatPlayerObj = null; }
        }

        private void RefreshAllCellsInteractable()
        {
            if (grid == null) return;
            for (int x = 0; x < grid.GetLength(0); x++)
                for (int y = 0; y < grid.GetLength(1); y++)
                    grid[x, y]?.RefreshInteractable();
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Shop Event (NodeType.Event sub-type=ShopEvent — 둘째자리=1)
        //
        // 일반 상점(SpawnMerchant)과 다른 점:
        //   - shopVisitCount/seed pool을 소비하지 않는다 (별도 진열).
        //   - 페이지 시스템 없음 — 한 번에 모든 상품 + 맵 이동 카드 1장.
        //   - 상품 구성은 EventLibrary 핸들러(event_{entryCode})에 위임 — 사용자가 이벤트마다 정의.
        //   - 떠나는 방법은 오로지 손패의 맵 이동 카드 사용.
        // ─────────────────────────────────────────────────────────────

        private int _currentShopEventCode = 0;
        public bool IsInShopEvent => _currentShopEventCode > 0;
        private readonly Dictionary<int, ShopInventory> _shopEventInventoryCache = new Dictionary<int, ShopInventory>();

        /// <summary>상점형 이벤트 진입 — combat state를 Shop으로 두고 EventLibrary 핸들러가 상품을 채운다.</summary>
        public void SpawnShopEvent(int entryCode)
        {
            Debug.Log($"[SpawnShopEvent] 진입 — entryCode={entryCode}");
            CleanupNonCombatVisuals();
            pendingCard = null;
            pendingCardIndex = -1;
            combatState = CombatState.Shop;
            CombatBoardCell.ShopFreeClickEnabled = true;
            _currentShopEventCode = entryCode;

            // 상점형 이벤트는 보드 재초기화 없이 맵 보드를 유지하고 HUD만 가린다.
            SetCombatHudActive(false);
            if (handContainer != null) ForceActivateWithParents(handContainer.gameObject);

            // 페이지 전환 없음 — endTurn은 카메라 밖으로. drawPile=RangeIndicator, discardPile=DiscardPile 뷰어.
            HideEndTurnButton();
            ForceActivateDeckPileButtons();
            ShowDeckPileButtons();
            SetPileButtonMode(PileButtonMode.ShopEvent);

            // 일반 상점 cache는 건드리지 않는다 — 별도 cache 사용.
            ClearShopState();

            // 결정론적 진열: entryCode + currentFloor 기반 seed.
            var prevRngState = UnityEngine.Random.state;
            int seed = entryCode * 31 + (Core.GameManager.Instance?.CurrentFloor ?? 0);
            UnityEngine.Random.InitState(seed);
            try
            {
                DistributeShopEventCards(entryCode);
            }
            finally
            {
                UnityEngine.Random.state = prevRngState;
            }
        }

        /// <summary>상점형 이벤트 떠나기 — shopVisitCount는 증가시키지 않으며 LastNodeShop 플래그도 set하지 않는다
        /// (미지 노드이므로 LastNodeMystery는 진입 시점에 이미 marked).</summary>
        private void OnShopEventLeave()
        {
            CombatBoardCell.ShopFreeClickEnabled = false;
            RefreshAllCellsInteractable();
            if (_drawShopCardsCoroutine != null) { StopCoroutine(_drawShopCardsCoroutine); _drawShopCardsCoroutine = null; }
            ExhaustRewardCards();
            ClearShopState();
            pendingCard = null;
            pendingCardIndex = -1;
            _shopSelectedCard = null;
            _currentShopEventCode = 0;
            HideEndTurnButton();
            HideDeckPileButtons();
            SetPileButtonMode(PileButtonMode.Default);
            InGameUIController.Instance?.OpenMap();
        }

        /// <summary>상점형 이벤트 카드 진열 — EventLibrary 핸들러에 위임 + 맵 이동 카드 1장.
        /// 캐시 hit 시 동일 진열 복원, miss 시 핸들러 실행.</summary>
        private void DistributeShopEventCards(int entryCode)
        {
            if (deckManager != null)
            {
                foreach (var card in hand) deckManager.AddToDiscardPile(card);
                hand.Clear();
                ClearHandVisuals();
                deckManager.MoveDiscardToDrawPile();
            }
            _rewardCards.Clear();

            if (!_shopEventInventoryCache.TryGetValue(entryCode, out var cache))
            {
                cache = new ShopInventory();
                _shopEventInventoryCache[entryCode] = cache;
            }

            if (cache.page1Cards.Count > 0)
            {
                RestoreShopPageFromCache(cache, cache.page1Cards);
                Debug.Log($"[DistributeShopEventCards] 캐시 복원 — {cache.page1Cards.Count}장");
            }
            else
            {
                // EventLibrary 핸들러 호출 — ctx.Board의 AddShopEventCardByCode 헬퍼로 상품을 추가하도록.
                var ctx = new DeckRoguelike.Combat.CardEffectContext
                {
                    Board = this,
                    Card  = DeckRoguelike.Core.CardRegistry.GetEventCard(entryCode),
                };
                DeckRoguelike.Combat.CardEffectRegistry.Execute($"event_{entryCode}", ctx);

                // 맵 이동 카드 1장 무조건 지급 (사용자 명세).
                AddShopMapMoveCard();

                cache.page1Cards = new List<CardData>(_rewardCards);
                foreach (var c in _rewardCards)
                {
                    if (_shopCardPrices.TryGetValue(c, out var p))     cache.prices[c] = p;
                    if (_shopCardDiscounted.TryGetValue(c, out var d)) cache.discounted[c] = d;
                    if (_shopEventOriginCards.Contains(c))             cache.eventOriginCards.Add(c);
                }
                Debug.Log($"[DistributeShopEventCards] 핸들러 생성 — {_rewardCards.Count}장: [{string.Join(", ", _rewardCards.Select(c => c.cardName))}]");
            }

            if (deckManager != null)
                for (int i = _rewardCards.Count - 1; i >= 0; i--)
                    deckManager.AddToTopOfDrawPile(_rewardCards[i]);

            if (_drawShopCardsCoroutine != null) StopCoroutine(_drawShopCardsCoroutine);
            _drawShopCardsCoroutine = StartCoroutine(DrawShopCards(_rewardCards.Count));
        }

        /// <summary>EventLibrary 핸들러에서 호출 — CardRegistry(일반 카드)에서 카드를 가져와 진열.
        /// 가격은 카드 Rarity에 따라 ShopCardPriceRanges에서 자동 산출 (일반 상점과 동일 규칙).
        /// 플레이 시 일반 상점처럼 deck에 추가된다.</summary>
        public void AddShopEventCardByCode(int cardCode)
        {
            var template = DeckRoguelike.Core.CardRegistry.GetCard(cardCode);
            if (template == null)
            {
                Debug.LogWarning($"[AddShopEventCardByCode] cardCode {cardCode} 카드를 찾을 수 없습니다.");
                return;
            }
            var card = template.Clone();
            int rarityIdx = Mathf.Clamp((int)card.Rarity, 0, ShopCardPriceRanges.Length - 1);
            int price = ApplyShopDiscount(RollPriceInRange(ShopCardPriceRanges[rarityIdx]));

            _rewardCards.Add(card);
            _shopCardPrices[card] = price;
            _shopCardDiscounted[card] = false;
        }

        /// <summary>EventLibrary 핸들러에서 호출 — EventRegistry(이벤트 카드)에서 카드를 가져와 진열.
        /// 가격은 카드 effects[0].value 에서 가져온다 (EventDataTemplate의 값 부분).
        /// 플레이 시 deck에 추가되지 않고 customEffectId 효과가 실행되며 강제 exhaust된다.</summary>
        public void AddShopEventEventCardByCode(int eventCardCode)
        {
            var template = DeckRoguelike.Core.CardRegistry.GetEventCard(eventCardCode);
            if (template == null)
            {
                Debug.LogWarning($"[AddShopEventEventCardByCode] event cardCode {eventCardCode} 카드를 찾을 수 없습니다.");
                return;
            }
            var card = template.Clone();
            int price = (card.Effects != null && card.Effects.Count > 0) ? card.Effects[0].value : 0;

            _rewardCards.Add(card);
            _shopEventOriginCards.Add(card);
            if (price > 0)
            {
                _shopCardPrices[card] = price;
                _shopCardDiscounted[card] = false;
            }
        }

        /// <summary>Rest의 카드 강화 picker와 동일 — shopCardListController를 Upgrade 모드로 띄운다.
        /// Rest 상태에 묶이지 않으므로 강화의 제단(42021) 같은 이벤트 카드에서 재사용 가능.
        /// onCancel: backButton으로 picker가 닫혔을 때 호출(강화 카드 재발급 등 후처리에 사용).</summary>
        public void OpenCardUpgradePickerStandalone(System.Action onCancel = null)
            => OpenStandaloneCardListPicker(RestCardMode.Upgrade, "StandaloneUpgrade", onCancel);

        /// <summary>제거의 제단(42041) 등에서 호출 — 덱에서 1장 제거 picker를 띄운다 (RestCardMode.Remove).</summary>
        public void OpenCardRemovePickerStandalone(System.Action onCancel = null)
            => OpenStandaloneCardListPicker(RestCardMode.Remove, "StandaloneRemove", onCancel);

        /// <summary>변화의 제단(42031) 등에서 호출 — 카드 변화(같은 등급의 다른 카드로 교체) picker.
        /// 현재는 전용 모드가 없어 Upgrade picker를 재사용한다. 변화 전용 로직은 후속 작업.</summary>
        public void OpenCardTransformPickerStandalone(System.Action onCancel = null)
        {
            // TODO: 카드 변화 전용 모드 (예: RestCardMode.Transform 추가 + CardListController 처리).
            //       지금은 Upgrade picker로 폴백.
            OpenStandaloneCardListPicker(RestCardMode.Upgrade, "StandaloneTransform", onCancel);
        }

        /// <summary>상점형 이벤트의 상품 카드(예: 12021 강화)를 손에 무료 복제본으로 다시 발급한다.
        /// HandleShopCardPlay 경로를 그대로 타도록 _shopCardPrices=0 / _shopEventOriginCards 등록.
        /// 사용 시 SpendGold(0) 통과 → ExecuteEventOriginCardEffects 실행 → picker 재오픈 흐름.</summary>
        public void ReissueShopEventCard(DeckRoguelike.Cards.CardData template)
        {
            if (template == null || hand.Count >= 10) return;
            var clone = template.Clone();
            hand.Add(clone);
            _rewardCards.Add(clone);
            _shopEventOriginCards.Add(clone);
            _shopCardPrices[clone]      = 0;
            _shopCardDiscounted[clone]  = false;
            var obj = CreateSingleCardObject(clone);
            if (obj != null) handCardObjects.Add(obj);
            RepositionHandCards();
            UpdatePileCounters();
        }

        /// <summary>현재 캐릭터의 카드 풀(공통+직업) 중 1장을 선택받아 덱에 추가한다.
        /// "원하는 카드 얻기" 이벤트(21011 등) 에서 호출.</summary>
        public void OpenClassCardGainPicker()
        {
            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            var pool = DeckRoguelike.Core.CardRegistry.GetRewardPool(character);
            if (pool == null || pool.Count == 0)
            {
                Debug.LogWarning("[OpenClassCardGainPicker] 카드 풀이 비어 있습니다.");
                return;
            }
            InGameUIController.Instance?.OpenCardPicker(pool, picked =>
            {
                if (picked != null) DeckRoguelike.Combat.DeckManager.Instance?.AddCardToDeck(picked);
            });
        }

        private void OpenStandaloneCardListPicker(RestCardMode mode, string panelId, System.Action onCancel = null)
        {
            if (shopCardListController != null)
            {
                shopCardListController.gameObject.SetActive(true);
                bool finished = false;
                shopCardListController.Setup(mode, cardConfirmPanel, onFinish: () =>
                {
                    finished = true;
                    OnStandalonePickerFinish(panelId);
                });
                InGameUIController.Instance?.PushPanel(panelId, () =>
                {
                    if (shopCardListController != null) shopCardListController.gameObject.SetActive(false);
                    if (!finished) onCancel?.Invoke();
                });
            }
            else
            {
                InGameUIController.Instance?.OpenRestCardList(mode, cardConfirmPanel,
                    () => OnStandalonePickerFinish(panelId),
                    () => { OnStandalonePickerFinish(panelId); onCancel?.Invoke(); });
            }
        }

        private void OnStandalonePickerFinish(string panelId)
        {
            InGameUIController.Instance?.UnregisterPanel(panelId);
            if (shopCardListController != null) shopCardListController.gameObject.SetActive(false);
        }

        #endregion

        #region Map State

        private static readonly Vector2Int[] MapOrthoDirections =
        {
            new Vector2Int(-1,  0), new Vector2Int( 1,  0),
            new Vector2Int( 0, -1), new Vector2Int( 0,  1),
        };

        private static readonly Vector2Int[] MapOrthoDiagDirections =
        {
            new Vector2Int(-1,  0), new Vector2Int( 1,  0),
            new Vector2Int( 0, -1), new Vector2Int( 0,  1),
            new Vector2Int(-1, -1), new Vector2Int(-1,  1),
            new Vector2Int( 1, -1), new Vector2Int( 1,  1),
        };

        /// <summary>108 유물 보유 시 대각선 이동 허용.</summary>
        private static Vector2Int[] MapDirections =>
            (GameManager.Instance != null && GameManager.Instance.HasRelic(108))
                ? MapOrthoDiagDirections : MapOrthoDirections;

        /// <summary>301 유물 보유 시 맵에서 어디든 이동 가능.</summary>
        private static bool MapFreeMovement =>
            GameManager.Instance != null && GameManager.Instance.HasRelic(301);

        public void ShowMap()
        {
            EnsureInitialized();
            Debug.Log($"[BoardController] ShowMap 호출 — mapInitialized={mapInitialized}, combatBoard={combatBoard != null}");
            bool wasUninitialized = !mapInitialized;
            bool isNewRun = false;
            if (!mapInitialized)
            {
                var save = Core.RunSaveSystem.Load();
                if (save != null && save.mapSeed != 0)
                {
                    LoadMapSaveData(save);
                }
                else
                {
                    GenerateNewMap(Core.GameManager.Instance?.RunSeed ?? -1);
                    isNewRun = true;
                    // 새 맵 생성 직후 저장 — 이후 Continue 시 같은 맵으로 복귀하기 위함
                    Core.GameManager.Instance?.SaveRun();
                }
            }
            else
            {
                InitializeMapBoard();
            }

            if (_pendingVictoryReward)
            {
                _pendingVictoryReward = false;
                DistributeVictoryRewards();
            }
            else if (_pendingDefeatReward)
            {
                _pendingDefeatReward = false;
                DistributeDefeatRewards();
            }
            else if (isNewRun)
            {
                // 새 런 시작 — 첫 맵 이동 카드를 손패에 지급한다.
                GrantInitialMapMoveCard();
            }
            // Continue로 복귀한 경우, 손패에 맵 이동 카드가 없고 _pendingVictoryReward도 없다면
            // 이동이 불가능해지므로 안전망으로 한 장 지급.
            else if (wasUninitialized)
            {
                bool hasMove = false;
                foreach (var c in hand) if (IsMapMoveCard(c)) { hasMove = true; break; }
                if (!hasMove) GrantInitialMapMoveCard();
            }
        }

        /// <summary>맵 이동 카드 한 장을 손패에 직접 지급 (덱/보상 더미를 거치지 않음).</summary>
        private void GrantInitialMapMoveCard()
        {
            var card = CreateMapMoveCard();
            if (card == null) return;

            if (handContainer != null && !handContainer.gameObject.activeSelf)
                handContainer.gameObject.SetActive(true);

            hand.Add(card);
            GameObject cardObj = CreateSingleCardObject(card);
            if (cardObj != null)
            {
                cardObj.SetActive(true);
                handCardObjects.Add(cardObj);
            }
            RepositionHandCards();
        }

        public void GenerateNewMap(int seed = -1)
        {
            mapSeed = seed >= 0 ? seed : UnityEngine.Random.Range(0, 99999);
            UnityEngine.Random.InitState(mapSeed);
            Debug.Log($"[BoardController] 맵 생성 (시드: {mapSeed})");

            GenerateMapGrid();
            Canvas.ForceUpdateCanvases();
            InitializeMapBoard();
            SetMapStartNode();
            mapInitialized = true;
        }

        /// <summary>
        /// 60011(다음 스테이지) 처리 — Act +1 후 인게임 시작과 동일하게 맵을 새로 생성하고,
        /// 플레이어를 시작 노드(0,0)에 배치한다.
        /// 새 Act에 진입하면 보스 유물 선택 카드(60010)를 먼저 지급하며, 맵 이동 카드는
        /// 유물 픽이 끝난 뒤(EndRewardPickFlow)에 지급해 플레이어가 보스 유물을 반드시 먼저 고르도록 한다.
        /// GenerateNewMap이 노드 규칙 배치 + 시작 노드 배치 + MapState 전환을 모두 수행한다.
        /// </summary>
        private void AdvanceToNextStage()
        {
            // 1) 손패에 남은 보상 카드(미사용 골드/카드보상 등) 정리.
            ExhaustRewardCards();

            // 2) Act +1 및 액트 난이도/전투 카운터 초기화.
            GameManager.Instance?.AdvanceToNextAct();

            // 3) 맵 재생성 — 규칙에 따라 노드 재배치 + 플레이어 시작 위치(0,0) 배치 + MapState 전환.
            mapInitialized = false;
            GenerateNewMap(UnityEngine.Random.Range(0, 99999));

            // 4) 보스 유물 선택 카드(60010) 지급 — 사용 시 보스 유물 3장 중 1장 선택.
            //    유물 픽이 끝나면 EndRewardPickFlow에서 맵 이동 카드를 지급한다.
            if (GrantBossRelicPickCard())
            {
                _grantMapMoveAfterRelicPick = true;
            }
            else
            {
                // 60010 템플릿이 없으면 픽을 건너뛰고 바로 맵 이동 카드를 지급해 진행이 막히지 않게 한다.
                GrantInitialMapMoveCard();
            }

            // 5) 새 맵 상태 저장 — Continue 시 같은 맵으로 복귀.
            GameManager.Instance?.SaveRun();
            Debug.Log("[BoardController] 다음 스테이지 진입 완료 — 맵 재생성 및 보스 유물 선택 카드 지급");
        }

        /// <summary>보스 유물 선택 카드(60010)를 손패에 직접 지급하고 _rewardCards에 등록한다
        /// (MapState에서 사용 가능하려면 _rewardCards에 있어야 함). 템플릿이 없으면 false.</summary>
        private bool GrantBossRelicPickCard()
        {
            var template = CardRegistry.GetCard(60010);
            if (template == null)
            {
                Debug.LogWarning("[BoardController] 보스 유물 선택 카드(60010) 템플릿 없음 — 지급 건너뜀");
                return false;
            }

            if (handContainer != null && !handContainer.gameObject.activeSelf)
                handContainer.gameObject.SetActive(true);

            var card = template.Clone();
            hand.Add(card);
            _rewardCards.Add(card);
            GameObject cardObj = CreateSingleCardObject(card);
            if (cardObj != null)
            {
                cardObj.SetActive(true);
                handCardObjects.Add(cardObj);
            }
            RepositionHandCards();
            return true;
        }

        private void GenerateMapGrid()
        {
            mapGrid = new MapNodeData[boardRows, boardCols];

            for (int row = 0; row < boardRows; row++)
                for (int col = 0; col < boardCols; col++)
                    mapGrid[row, col] = new MapNodeData { Type = NodeType.Combat, Row = row, Col = col };

            mapGrid[0, 0].Type    = NodeType.Combat;
            mapGrid[0, 0].IsStart = true;
            mapGrid[boardRows - 1, boardCols - 1].Type = NodeType.Boss;

            var freeNodes = new List<Vector2Int>();
            for (int row = 0; row < boardRows; row++)
                for (int col = 0; col < boardCols; col++)
                    if (!(row == 0 && col == 0) && !(row == boardRows - 1 && col == boardCols - 1))
                        freeNodes.Add(new Vector2Int(row, col));

            Shuffle(freeNodes);

            int idx = 0;
            void Assign(NodeType type, int count)
            {
                for (int i = 0; i < count && idx < freeNodes.Count; i++, idx++)
                    mapGrid[freeNodes[idx].x, freeNodes[idx].y].Type = type;
            }

            Assign(NodeType.DangerCombat, mapDangerCombatCount);

            // Event 노드 — sub-type 1+1+1 보장을 위해 별도 처리
            int eventStartIdx = idx;
            Assign(NodeType.Event, mapEventCount);
            int eventEndIdx = idx;
            AssignEventSubTypes(freeNodes, eventStartIdx, eventEndIdx);

            Assign(NodeType.Rest,     mapRestCount);
            Assign(NodeType.Shop,     mapShopCount);

            // 레거시 — Treasure는 Event sub-type으로 흡수됨. mapTreasureCount>0 이면 추가 Event로 보충.
            for (int i = 0; i < mapTreasureCount && idx < freeNodes.Count; i++, idx++)
            {
                var pos = freeNodes[idx];
                mapGrid[pos.x, pos.y].Type = NodeType.Event;
                mapGrid[pos.x, pos.y].EventSubType = EventSubType.Treasure;
                mapGrid[pos.x, pos.y].IsTreasure = true;
            }
        }

        /// <summary>Event 노드들에 sub-type을 1:1로 배정. [Treasure, ShopEvent, ChoiceEvent]을 셔플 후 순서대로 적용.
        /// Event 노드가 3개 미만이면 셔플된 앞부분만 사용, 3개 초과면 나머지는 ChoiceEvent로 채운다.</summary>
        private void AssignEventSubTypes(List<Vector2Int> freeNodes, int startIdx, int endIdx)
        {
            int count = endIdx - startIdx;
            if (count <= 0) return;

            var pool = new List<EventSubType>
            {
                EventSubType.Treasure,
                EventSubType.ShopEvent,
                EventSubType.ChoiceEvent,
            };
            Shuffle(pool);

            for (int i = 0; i < count; i++)
            {
                var pos = freeNodes[startIdx + i];
                var node = mapGrid[pos.x, pos.y];
                var sub = i < pool.Count ? pool[i] : EventSubType.ChoiceEvent;
                node.EventSubType = sub;
                node.IsTreasure = (sub == EventSubType.Treasure);
            }
        }

        private void InitializeMapBoard()
        {
            Debug.Log($"[BoardController] InitializeMapBoard 시작 — combatBoard={combatBoard != null}, active={combatBoard?.gameObject.activeInHierarchy}");
            if (combatBoard == null) return;

            CleanupNonCombatVisuals();
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null) es.SetSelectedGameObject(null);
            var toDestroy = new List<GameObject>();
            foreach (Transform child in combatBoard)
                if (child.gameObject != playerObject)
                    toDestroy.Add(child.gameObject);
            foreach (var go in toDestroy)
            {
                if (go == null) continue;
                go.SetActive(false);
                go.transform.SetParent(null, false);
                Destroy(go);
            }
            if (playerObject != null) playerObject.SetActive(false);

            Canvas.ForceUpdateCanvases();
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
                    cell.SetMapMode(true);

                    if (mapGrid != null)
                        cell.MapNode = mapGrid[row, col];

                    cell.OnCellClicked    += HandleMapCellClicked;
                    cell.OnCellHoverEnter += OnMapMoveCellHoverEnter;
                    cell.OnCellHoverExit  += OnMapMoveCellHoverExit;

                    grid[col, row] = cell;
                }
            }

            SetCombatHudActive(false);
            UpdateMapVisuals();
            PlacePlayerOnMap();

            combatState = CombatState.MapState;
        }

        private void SetMapStartNode()
        {
            if (mapGrid == null) return;
            mapGrid[0, 0].IsVisited = true;
            mapPlayerPos = new Vector2Int(0, 0);
            RecomputeBossReachability();
            MarkMapAccessibleFrom(0, 0);
            UpdateMapVisuals();
            PlacePlayerOnMap();
        }

        private void PlacePlayerOnMap()
        {
            if (grid == null || mapPlayerPos.x < 0) return;

            EnsurePlayerObject();

            int col = mapPlayerPos.y;
            int row = mapPlayerPos.x;
            if (col < 0 || col >= boardCols || row < 0 || row >= boardRows) return;

            CombatBoardCell cell = grid[col, row];
            cell.IsPlayerHere = true;

            if (playerObject != null)
            {
                playerObject.SetActive(true);
                playerObject.transform.SetParent(combatBoard, false);
                RectTransform rt = playerObject.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = GetCellPosition(col, row) + playerCellOffset;
                playerObject.transform.SetAsLastSibling();

                if (unitUI != null && GameManager.Instance != null)
                    unitUI.UpdateHP(GameManager.Instance.CurrentHP, GameManager.Instance.MaxHP);
            }
        }

        private void HandleMapCellClicked(CombatBoardCell cell)
        {
            // 맵 이동 카드 타겟팅 중이면 셀 클릭을 이동 시도로 처리.
            // MapState/Shop/Rest 모두 허용 — Shop/Rest에선 셀 클릭이 leave + 이동을 트리거.
            if (pendingCard != null && IsMapMoveCard(pendingCard) &&
                (combatState == CombatState.MapState
                 || combatState == CombatState.Shop
                 || combatState == CombatState.Rest
                 || combatState == CombatState.Treasure))
            {
                HandleMapMoveCellClicked(cell);
                return;
            }
            // 노드 클릭으로 맵을 이동하던 흐름은 폐지됨.
            if (combatState != CombatState.MapState) return;
            Debug.Log($"[HandleMapCellClicked] 무시 — 맵 이동은 카드(map_Move)로만 가능: cell={cell.GridPos}");
        }

        /// <summary>맵 이동 카드(customEffectId="map_Move")가 셀 좌표를 타깃으로 선택했을 때 호출된다.
        /// 카드의 rangeOffsets 안에 있어야만 이동이 성공한다.</summary>
        public bool TryMapMoveToCell(Vector2Int cellGridPos)
        {
            if (combatState != CombatState.MapState || mapGrid == null) return false;
            int col = cellGridPos.x;
            int row = cellGridPos.y;
            if (row < 0 || row >= boardRows || col < 0 || col >= boardCols) return false;

            var node = mapGrid[row, col];
            if (node.IsVisited) return false;

            ExhaustRewardCards();
            node.IsVisited = true;
            mapPlayerPos = new Vector2Int(row, col);

            // 맵 이동이 확정될 때마다 유물 훅 (114 — 이동마다 +25G, 상점 방문 후 비활성화).
            if (Core.GameManager.Instance != null)
                foreach (var relic in Core.GameManager.Instance.Relics)
                    relic.OnMapMove(Core.GameManager.Instance);

            ClearMapAccessibility();
            RecomputeBossReachability();
            if (node.Type != NodeType.Boss)
                MarkMapAccessibleFrom(row, col);

            UpdateMapVisuals();
            PlacePlayerOnMap();

            // Event 노드는 dispatch 직전에 sub-type을 GameManager에 기록 — InGameUIController가 분기에 사용.
            // Treasure sub-type만 NodeType.Treasure로 변환해 기존 SpawnTreasureArea 경로 재사용.
            if (node.Type == NodeType.Event)
                Core.GameManager.Instance?.SetCurrentEventSubType(node.EventSubType);

            var dispatchType = (node.Type == NodeType.Event && node.EventSubType == EventSubType.Treasure)
                ? NodeType.Treasure
                : (node.IsTreasure ? NodeType.Treasure : node.Type);
            OnNodeSelected?.Invoke(dispatchType, col);

            Core.GameManager.Instance?.SaveRun();
            return true;
        }

        private void MarkMapAccessibleFrom(int row, int col)
        {
            if (MapFreeMovement)
            {
                for (int r = 0; r < boardRows; r++)
                    for (int c = 0; c < boardCols; c++)
                    {
                        var n = mapGrid[r, c];
                        if (n.IsVisited || n.IsDisabled) continue;
                        if (r == row && c == col) continue;
                        n.IsAccessible = true;
                    }
                return;
            }

            foreach (var dir in MapDirections)
            {
                int nr = row + dir.x;
                int nc = col + dir.y;
                if (nr < 0 || nr >= boardRows || nc < 0 || nc >= boardCols) continue;
                var n = mapGrid[nr, nc];
                if (n.IsVisited || n.IsDisabled) continue;
                n.IsAccessible = true;
            }
        }

        private void ClearMapAccessibility()
        {
            if (mapGrid == null) return;
            for (int r = 0; r < boardRows; r++)
                for (int c = 0; c < boardCols; c++)
                    mapGrid[r, c].IsAccessible = false;
        }

        /// <summary>
        /// 보스 노드까지 (방문하지 않은 셀만 거쳐) 도달 가능한 셀들을 BFS로 찾아
        /// 그 외 비방문 셀은 IsDisabled = true 로 표시한다.
        /// MapFreeMovement(301)일 땐 모든 비방문 셀이 보스에 도달 가능한 것으로 간주.
        /// </summary>
        private void RecomputeBossReachability()
        {
            if (mapGrid == null) return;

            int bossR = boardRows - 1;
            int bossC = boardCols - 1;

            // 초기화: 모든 비방문 셀을 일단 disabled, 방문 셀은 disabled 해제
            for (int r = 0; r < boardRows; r++)
                for (int c = 0; c < boardCols; c++)
                    mapGrid[r, c].IsDisabled = !mapGrid[r, c].IsVisited;

            // 보스가 이미 방문됐다면 더 이상 길 찾기 불필요
            if (mapGrid[bossR, bossC].IsVisited) return;

            if (MapFreeMovement)
            {
                for (int r = 0; r < boardRows; r++)
                    for (int c = 0; c < boardCols; c++)
                        if (!mapGrid[r, c].IsVisited)
                            mapGrid[r, c].IsDisabled = false;
                return;
            }

            var dirs = MapDirections;
            var queue = new Queue<Vector2Int>();
            var visited = new bool[boardRows, boardCols];
            queue.Enqueue(new Vector2Int(bossR, bossC));
            visited[bossR, bossC] = true;
            mapGrid[bossR, bossC].IsDisabled = false;

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var d in dirs)
                {
                    int nr = cur.x + d.x;
                    int nc = cur.y + d.y;
                    if (nr < 0 || nr >= boardRows || nc < 0 || nc >= boardCols) continue;
                    if (visited[nr, nc]) continue;
                    var n = mapGrid[nr, nc];
                    if (n.IsVisited) continue; // 방문한 셀은 다시 못 지나감
                    visited[nr, nc] = true;
                    n.IsDisabled = false;
                    queue.Enqueue(new Vector2Int(nr, nc));
                }
            }
        }

        private void UpdateMapVisuals()
        {
            if (grid == null || mapGrid == null) return;

            for (int col = 0; col < boardCols; col++)
            {
                for (int row = 0; row < boardRows; row++)
                {
                    var cell = grid[col, row];
                    var node = mapGrid[row, col];

                    Sprite icon;
                    if (node.IsVisited && visitedNodeIcon != null)
                        icon = visitedNodeIcon;
                    else if (node.IsStart)
                        icon = startNodeIcon;
                    else if (node.IsTreasure
                             && treasureNodeIcon != null
                             && GameManager.Instance != null
                             && GameManager.Instance.HasRelic(108))
                        icon = treasureNodeIcon;  // 108 금속탐지기 — 보물방 공개
                    else
                        icon = GetMapNodeIcon(node.Type);

                    // 노드 색상: 방문 노드만 어둡게, 그 외는 항상 밝게(white).
                    // 이동은 맵 이동 카드를 사용해야 가능하므로 IsAccessible 색 표시를 하지 않는다.
                    // 방문 노드는 visitedNodeIcon으로 구분되며, 색은 항상 white로 유지(어두워지지 않음).
                    Color color = Color.white;

                    // 노드 클릭 이동을 폐지 — 셀은 카드 타겟팅 시에만 활성화된다.
                    cell.SetMapVisual(icon, color, false);
                }
            }
        }

        private Sprite GetMapNodeIcon(NodeType type)
        {
            return type switch
            {
                NodeType.Combat       => combatNodeIcon,
                NodeType.DangerCombat => dangerCombatNodeIcon,
                NodeType.Boss         => bossNodeIcon,
                NodeType.Rest         => restNodeIcon,
                NodeType.Shop         => shopNodeIcon,
                NodeType.Treasure     => eventNodeIcon,
                NodeType.Event        => eventNodeIcon,
                _                     => null
            };
        }

        public int GetCurrentFloor() => mapPlayerPos.y >= 0 ? mapPlayerPos.y : 0;

        public void WriteMapSaveData(DeckRoguelike.Core.RunSaveData data)
        {
            data.mapSeed = mapSeed;
            data.mapCurrentRow = mapPlayerPos.x;
            data.mapCurrentCol = mapPlayerPos.y;
            data.visitedRows.Clear();
            data.visitedCols.Clear();
            if (mapGrid == null) return;
            for (int r = 0; r < mapGrid.GetLength(0); r++)
                for (int c = 0; c < mapGrid.GetLength(1); c++)
                    if (mapGrid[r, c].IsVisited)
                    {
                        data.visitedRows.Add(r);
                        data.visitedCols.Add(c);
                    }
        }

        public void LoadMapSaveData(DeckRoguelike.Core.RunSaveData data)
        {
            GenerateNewMap(data.mapSeed);
            for (int i = 0; i < data.visitedRows.Count && i < data.visitedCols.Count; i++)
            {
                int r = data.visitedRows[i];
                int c = data.visitedCols[i];
                if (r >= 0 && r < boardRows && c >= 0 && c < boardCols)
                    mapGrid[r, c].IsVisited = true;
            }
            mapPlayerPos = new Vector2Int(data.mapCurrentRow, data.mapCurrentCol);
            ClearMapAccessibility();
            MarkMapAccessibleFrom(mapPlayerPos.x, mapPlayerPos.y);
            InitializeMapBoard();
        }

        /// <summary>전투 종료 후 저장된 맵 위치로 복귀합니다.</summary>
        public void ReturnToMapState()
        {
            mapPlayerPos = savedMapPlayerPos;
            InitializeMapBoard();
        }

        /// <summary>전투 종료 시점에 호출 — mapPlayerPos를 전투 시작 시점에 저장한 위치로 복원하고
        /// 보드/플레이어가 이미 표시되어 있다면 즉시 재배치한다.
        /// 전투 중 mapPlayerPos가 의도치 않게 변경되었거나, ShowMap 흐름 외에서 좌표를 동기화해야 할 때 사용.</summary>
        /// <summary>전투 종료 시 호출 — 전투 시작 시점에 저장한 좌표로 mapPlayerPos를 무조건 복원한다.
        /// _savedMapPlayerPosSet 플래그가 false면(=첫 전투 전) 복원하지 않는다.
        /// 보드/플레이어가 살아있으면 즉시 시각적으로도 재배치하고, 뒤따르는 ShowMap → InitializeMapBoard에서
        /// PlacePlayerOnMap이 같은 좌표로 한 번 더 적용한다 (이중 안전망).</summary>
        public void RestoreMapPlayerPosAfterCombat()
        {
            if (!_savedMapPlayerPosSet)
            {
                Debug.LogWarning("[RestoreMapPlayerPosAfterCombat] saved 플래그 미설정 — mapPlayerPos 복원 건너뜀");
                return;
            }
            mapPlayerPos = savedMapPlayerPos;
            // playerSpawnCell도 저장된 맵 좌표로 동기화 — 전투 후 보상/맵 상태에서 카드 hover의 range preview
            // (ShowCardRangePreview 등)가 playerSpawnCell을 anchor로 쓰기 때문에, 전투 중 이동한 좌표가 아닌
            // 전투 진입 직전 맵 좌표를 사용해야 power/range sprite가 올바른 위치에 표시된다.
            playerSpawnCell = new Vector2Int(
                Mathf.Clamp(mapPlayerPos.y, 0, boardCols - 1),
                Mathf.Clamp(mapPlayerPos.x, 0, boardRows - 1));
            Debug.Log($"[RestoreMapPlayerPosAfterCombat] mapPlayerPos -> {mapPlayerPos} playerSpawnCell -> {playerSpawnCell}");

            if (grid != null && playerObject != null)
                PlacePlayerOnMap();
        }

        // ── Map Preview (hover) ────────────────────────────────────────
        private bool _isMapPreviewActive;
        private readonly Dictionary<CombatBoardCell, (Sprite sprite, Color color)> _savedCellVisuals
            = new Dictionary<CombatBoardCell, (Sprite, Color)>();

        /// <summary>전투 중 맵 버튼 hover 시 셀에 맵 비주얼을 오버레이합니다.</summary>
        public void ShowMapPreview()
        {
            if (_isMapPreviewActive || combatState == CombatState.MapState) return;
            if (grid == null || mapGrid == null) return;
            _isMapPreviewActive = true;

            // 유닛 숨기기
            if (playerObject != null) playerObject.SetActive(false);
            foreach (var e in enemies)
                if (e?.GameObject != null) e.GameObject.SetActive(false);
            foreach (var a in allies)
                if (a?.GameObject != null) a.GameObject.SetActive(false);

            // range/preview 오브젝트 숨기기 — 손패(handCardObjects)는 유지해 카드가 계속 보이도록 한다.
            SetListActive(hoverPreviewObjects, false);
            SetListActive(unitPreviewObjects, false);
            SetListActive(unitAttackPreviewObjects, false);
            SetListActive(unitMovePreviewObjects, false);
            SetListActive(unitSkillPreviewObjects, false);
            SetListActive(targetingHoverPreviewObjects, false);

            // 셀 비주얼 저장 후 맵으로 교체
            _savedCellVisuals.Clear();
            for (int col = 0; col < boardCols; col++)
            {
                for (int row = 0; row < boardRows; row++)
                {
                    var cell = grid[col, row];
                    if (cell == null) continue;

                    var bg = cell.GetComponentInChildren<Image>();
                    if (bg != null)
                        _savedCellVisuals[cell] = (bg.sprite, bg.color);

                    var node = mapGrid[row, col];
                    bool revealTreasure = node.IsTreasure
                                          && treasureNodeIcon != null
                                          && GameManager.Instance != null
                                          && GameManager.Instance.HasRelic(108);
                    Sprite icon = node.IsVisited && visitedNodeIcon != null ? visitedNodeIcon
                                : node.IsStart ? startNodeIcon
                                : revealTreasure ? treasureNodeIcon
                                : GetMapNodeIcon(node.Type);

                    // 접근 가능 색상 표시는 폐지 — 방문/일반 두 가지만 사용. 일반은 항상 white(어둡게 안 함).
                    // 방문 노드는 visitedNodeIcon으로 구분되며, 색은 항상 white로 유지(어두워지지 않음).
                    Color color = Color.white;

                    cell.SetMapVisual(icon, color, false);
                }
            }
        }

        /// <summary>맵 미리보기 해제 → 전투 비주얼 복원</summary>
        public void HideMapPreview()
        {
            if (!_isMapPreviewActive) return;
            _isMapPreviewActive = false;

            // 셀 비주얼 복원
            foreach (var kv in _savedCellVisuals)
            {
                var cell = kv.Key;
                if (cell == null) continue;
                var bg = cell.GetComponentInChildren<Image>();
                if (bg != null)
                {
                    bg.sprite = kv.Value.sprite;
                    bg.color  = kv.Value.color;
                }
                cell.SetState(cell.CurrentState);
            }
            _savedCellVisuals.Clear();

            // 유닛 복원
            if (playerObject != null) playerObject.SetActive(true);
            foreach (var e in enemies)
                if (e?.GameObject != null) e.GameObject.SetActive(true);
            foreach (var a in allies)
                if (a?.GameObject != null) a.GameObject.SetActive(true);

            // range/preview 오브젝트 복원 — handCardObjects는 ShowMapPreview에서 끄지 않으므로 복원도 생략.
            SetListActive(hoverPreviewObjects, true);
            SetListActive(unitPreviewObjects, true);
            SetListActive(unitAttackPreviewObjects, true);
            SetListActive(unitMovePreviewObjects, true);
            SetListActive(unitSkillPreviewObjects, true);
            SetListActive(targetingHoverPreviewObjects, true);
        }

        private static void SetListActive(List<GameObject> list, bool active)
        {
            foreach (var obj in list)
                if (obj != null) obj.SetActive(active);
        }

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
        Shop,
        MapState,
        Treasure
    }

    #endregion

    #region Map Data Classes

    /// <summary>NodeType.Event 노드의 세부 종류. 모두 맵에서는 "?" 아이콘으로 표시되어
    /// 진입 전까지 플레이어가 구분할 수 없다.</summary>
    public enum EventSubType
    {
        None,
        Treasure,    // 보물방 (SpawnTreasureArea)
        ShopEvent,   // 상점형 이벤트 (SpawnShopEvent — 둘째자리=1)
        ChoiceEvent, // 선택형 이벤트 (entry+choice 카드 — 둘째자리=2)
    }

    [System.Serializable]
    public class MapNodeData
    {
        public NodeType Type;
        public int Row;
        public int Col;
        public bool IsVisited;
        public bool IsAccessible;
        public bool IsStart;
        public bool IsDisabled;
        // 호환 — EventSubType.Treasure와 동기화. 신규 코드는 EventSubType을 봐야 한다.
        public bool IsTreasure;
        public EventSubType EventSubType = EventSubType.None;
        public Vector2 Position;

        [System.NonSerialized]
        public GameObject NodeObject;
    }

    public enum NodeType
    {
        Combat,
        DangerCombat,
        Boss,
        Rest,
        Shop,
        Treasure,
        Event,
        Unknown
    }

    #endregion
}
