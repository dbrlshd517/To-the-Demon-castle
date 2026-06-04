using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using DeckRoguelike.Cards;
using DeckRoguelike.Combat;
using DeckRoguelike.Core;

namespace DeckRoguelike.UI
{
    public enum CardListMode { Draw, Discard, Deck, Sub, Dictionary }
    public enum DeckSortMode { Acquired, Type, Name, Rarity }
    public enum RestCardMode  { Upgrade, Remove }

    public class CardListController : MonoBehaviour
    {
        [Header("=== 패널 설정 ===")]
        [SerializeField] private CardListMode mode = CardListMode.Draw;

        [Header("=== 그리드 ===")]
        [SerializeField] private ScrollRect  scrollRect;
        [SerializeField] private Transform   cardGridContainer;
        [SerializeField] private GameObject  cardPrefab;
        [SerializeField] private Vector2     cardCellSize     = new Vector2(130f, 190f);
        [SerializeField] private float       cardSpacingX     = 100f;
        [SerializeField] private float       cardSpacingY     = 100f;
        [SerializeField] private int         paddingTopBottom = 500;
        [SerializeField] private int         paddingLeftRight = 0;

        [Header("=== 헤더 ===")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI countText;

        [Header("=== 호버 효과 ===")]
        [SerializeField] private float listHoverScale = 1.3f;

        [Header("=== 닫기 버튼 (Draw / Discard 모드) ===")]
        [SerializeField] private Button closeButton;

        [Header("=== 정렬 버튼 (Deck / Dictionary 모드) ===")]
        [SerializeField] private GameObject sortButtonGroup;
        [Tooltip("Deck 모드에서는 획득순, Dictionary 모드에서는 희귀도순으로 동작 (라벨도 자동 전환)")]
        [SerializeField] private Button     sortAcquiredButton;
        [SerializeField] private Button     sortTypeButton;
        [SerializeField] private Button     sortNameButton;

        // 스프라이트는 기본 오름차순 모양(▲)으로 그리고, 내림차순일 때 transform.localScale.y의 부호를 뒤집어 상하 반전한다.
        [Header("=== 정렬 방향 화살표 sprite ===")]
        [Tooltip("획득(또는 Dictionary 모드의 희귀도) 정렬 버튼의 방향 화살표 Image. 오름차순 ▲ 모양으로 그려두면 내림차순일 때 자동 반전됨.")]
        [SerializeField] private UnityEngine.UI.Image sortAcquiredArrow;
        [Tooltip("종류 정렬 버튼의 방향 화살표 Image")]
        [SerializeField] private UnityEngine.UI.Image sortTypeArrow;
        [Tooltip("이름 정렬 버튼의 방향 화살표 Image")]
        [SerializeField] private UnityEngine.UI.Image sortNameArrow;

        [Header("=== Dictionary 모드 전용 — 직업 필터 ===")]
        [Tooltip("[테스트용] true면 발견 여부와 무관하게 모든 카드를 발견한 것처럼 표시.\n" +
                 "80002(???) 플레이스홀더 대신 실제 카드 아이콘/이름이 노출됨. DiscoveryManager 상태는 변경하지 않음.")]
        [SerializeField] private bool showAllAsDiscovered = false;
        [Tooltip("Dictionary 모드에서만 표시될 직업 버튼 그룹")]
        [SerializeField] private GameObject classButtonGroup;
        [SerializeField] private Button     warriorButton;
        [SerializeField] private Button     gunnerButton;
        [SerializeField] private Button     mageButton;
        [Tooltip("공통 카드(ClassDigit=1) + 80001 카드를 보여주는 '기타' 버튼")]
        [SerializeField] private Button     miscButton;
        [Tooltip("선택된 직업 버튼의 색상")]
        [SerializeField] private Color      selectedClassColor   = Color.yellow;
        [Tooltip("선택되지 않은 직업 버튼의 색상")]
        [SerializeField] private Color      unselectedClassColor = Color.white;

        [Header("=== Sub 모드 전용 ===")]
        [SerializeField] private CardConfirmPanel cardConfirmPanel;

        [Header("=== 휠 스크롤 ===")]
        [Tooltip("관성 휠 스크롤 튜닝값을 담은 ScriptableObject (Assets/.../WheelScrollTuning*.asset).\n" +
                 "여러 패널이 같은 asset 을 참조하면 일괄 적용. 미할당 시 휠 핸들러 부착 생략.")]
        [SerializeField] private DeckRoguelike.UI.WheelScrollTuning wheelTuning;

        // ── 런타임 상태 ──────────────────────────
        private DeckSortMode      currentSort  = DeckSortMode.Acquired;
        private List<CardData>    currentCards = new List<CardData>();
        // Dictionary 모드 — 현재 선택된 필터(직업 또는 기타).
        private DictionaryFilter  selectedFilter = DictionaryFilter.Warrior;

        // 정렬 방향은 "현재 활성 정렬 하나에 대해서만" 유지한다.
        // 시작 시 전체가 오름차순으로 보이고, 클릭한 버튼만 내림차순(스프라이트 상하 반전)으로 표시된다.
        // 비활성 정렬 버튼은 항상 오름차순 화살표(반전 없음)를 유지한다.
        private bool _currentSortAsc = true;

        // Dictionary 모드 필터 — 3 직업 + 기타(공통 + 80001).
        private enum DictionaryFilter { Warrior, Gunner, Mage, Misc }
        // 기타 필터에 강제로 포함되는 추가 카드 코드.
        private const int MiscIncludedCardCode = 80001;

        // Sub 전용
        private RestCardMode  currentRestMode;
        private System.Action onFinishCallback;

        // Sub Custom Pick 전용 (유물 201~204 카드 선택)
        private List<CardData>             _customPickPool;
        private System.Action<CardData>    _onCustomPick;

        // Sub 다중 선택 전용 (906/910 대격변 — 지정 수만큼 토글 선택하면 즉시 처리)
        private bool                            _multiSelectActive;
        private int                             _multiSelectRequired;
        private System.Action<List<CardData>>   _onMultiSelectComplete;
        private readonly List<CardUI>           _multiSelectedUIs = new List<CardUI>();

        // ──────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
            SetupScrollView();
            SetupGridLayout();

            if (mode == CardListMode.Sub) return;

            closeButton?.onClick.AddListener(() =>
            {
                if (mode == CardListMode.Draw)
                    InGameUIController.Instance?.ToggleDrawPanel();
                else if (mode == CardListMode.Discard)
                    InGameUIController.Instance?.ToggleDiscardPanel();
            });

            // Dictionary 모드에서는 sortAcquiredButton이 희귀도순 정렬로 동작한다.
            DeckSortMode firstButtonSort = (mode == CardListMode.Dictionary)
                ? DeckSortMode.Rarity
                : DeckSortMode.Acquired;
            sortAcquiredButton?.onClick.AddListener(() => SetSort(firstButtonSort));
            sortTypeButton   ?.onClick.AddListener(() => SetSort(DeckSortMode.Type));
            sortNameButton   ?.onClick.AddListener(() => SetSort(DeckSortMode.Name));

            // 정렬 버튼 라벨을 다국어로 바인딩. Dictionary 모드의 첫 버튼은 "희귀도순"으로 표시.
            BindButtonLabel(sortAcquiredButton, firstButtonSort == DeckSortMode.Rarity ? "sort_rarity" : "sort_acquired");
            BindButtonLabel(sortTypeButton,    "sort_type");
            BindButtonLabel(sortNameButton,    "sort_name");

            // Dictionary 모드 — 직업/기타 필터 버튼 활성화 및 핸들러 연결.
            warriorButton?.onClick.AddListener(() => SetSelectedFilter(DictionaryFilter.Warrior));
            gunnerButton ?.onClick.AddListener(() => SetSelectedFilter(DictionaryFilter.Gunner));
            mageButton   ?.onClick.AddListener(() => SetSelectedFilter(DictionaryFilter.Mage));
            miscButton   ?.onClick.AddListener(() => SetSelectedFilter(DictionaryFilter.Misc));

            // 기타 버튼 라벨 다국어 (등록되지 않은 키면 원본 텍스트 유지)
            if (miscButton != null)
                BindButtonLabel(miscButton, "dictionary_misc");

            // sortButtonGroup/classButtonGroup 가시성을 모드 기준으로 적용.
            ApplyModeDependentUI();

            if (mode == CardListMode.Dictionary)
            {
                // Dictionary 기본 정렬: 희귀도순.
                currentSort = DeckSortMode.Rarity;
                ApplyClassButtonHighlight();
            }
        }

        /// <summary>현재 mode에 맞춰 sortButtonGroup / classButtonGroup의 가시성을 갱신.
        /// Awake, OnEnable, SetMode에서 호출돼 런타임 모드 변경에도 UI가 동기화되도록 한다.</summary>
        private void ApplyModeDependentUI()
        {
            if (sortButtonGroup != null)
                sortButtonGroup.SetActive(mode == CardListMode.Deck || mode == CardListMode.Dictionary);
            if (classButtonGroup != null)
                classButtonGroup.SetActive(mode == CardListMode.Dictionary);
        }

        /// <summary>버튼의 자식 TextMeshProUGUI에 LocalizationBinder를 강제로 부착/갱신.</summary>
        private static void BindButtonLabel(Button button, string stringCode)
        {
            if (button == null) return;
            var tmp = button.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            if (tmp == null) return;
            var binder = tmp.GetComponent<DeckRoguelike.Core.LocalizationBinder>();
            if (binder == null) binder = tmp.gameObject.AddComponent<DeckRoguelike.Core.LocalizationBinder>();
            binder.SetCode(stringCode);
        }

        private void OnEnable()
        {
            if (mode == CardListMode.Sub) return;

            // SetMode로 런타임에 mode가 바뀌었을 수 있으니 패널이 다시 켜질 때마다 UI 가시성을 재적용.
            ApplyModeDependentUI();

            if (mode == CardListMode.Dictionary)
                ApplyClassButtonHighlight();

            Refresh();
            UpdateSortButtonArrows();
            // ScrollRect.content가 Inspector에서 미할당이면 normalizedPosition 접근이 NullReference로 터진다.
            // content가 연결된 경우에만 스크롤 위치를 맨 위로 복원.
            if (scrollRect != null && scrollRect.content != null)
                scrollRect.normalizedPosition = new Vector2(0f, 1f);
        }

        private void OnDisable()
        {
            if (mode == CardListMode.Draw || mode == CardListMode.Discard)
            {
                InGameUIController.Instance?.SetTopBarButtonsInteractable(true);
                // 103/104 포션 픽 모드: 패널이 카드 선택 없이 닫혔다면 cancel을 발화.
                // 성공 핸들러는 패널 닫기 전 PotionPickModeActive=false로 만들어 이 분기를 건너뛴다.
                if (CardUI.PotionPickModeActive)
                    CardUI.RaisePotionPickCancelled();
            }
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Public API — Sub 모드

        public bool IsConfirmOpen => cardConfirmPanel != null && cardConfirmPanel.gameObject.activeSelf;
        public void CloseConfirm() => cardConfirmPanel?.Close();

        /// <summary>InGameUIController / ShopController에서 호출. SetActive(true) 이후에 호출할 것.</summary>
        public void Setup(RestCardMode restMode, CardConfirmPanel confirmPanel = null, System.Action onFinish = null)
        {
            currentRestMode  = restMode;
            onFinishCallback = onFinish;
            _customPickPool  = null;
            _onCustomPick    = null;
            ResetMultiSelectState();
            if (confirmPanel != null) cardConfirmPanel = confirmPanel;
            RebuildGrid();
            if (scrollRect != null && scrollRect.content != null)
                scrollRect.normalizedPosition = new Vector2(0f, 1f);
        }

        /// <summary>
        /// 유물 201~204 등에서 사용: 임의의 카드 풀을 띄우고 1장 선택받습니다.
        /// 선택 시 onPicked(card) 호출 후 패널 자동 닫힘.
        /// </summary>
        public void SetupCardPicker(List<CardData> cards, CardConfirmPanel confirmPanel,
                                    System.Action<CardData> onPicked)
        {
            _customPickPool   = cards ?? new List<CardData>();
            _onCustomPick     = onPicked;
            ResetMultiSelectState();
            currentRestMode   = RestCardMode.Remove; // confirm 패널은 단일 카드 표시
            onFinishCallback  = null;
            if (confirmPanel != null) cardConfirmPanel = confirmPanel;
            RebuildGrid();
            if (scrollRect != null && scrollRect.content != null)
                scrollRect.normalizedPosition = new Vector2(0f, 1f);
        }

        /// <summary>
        /// 906/910 대격변 등 — 카드 풀에서 정확히 count장을 토글 선택받습니다.
        /// 카드를 클릭하면 토글되어 살짝 확대된 채 "선택됨"으로 유지되고, 다시 클릭하면 해제됩니다.
        /// count장이 모두 선택되면 confirm 패널 없이 즉시 onComplete(선택된 카드 목록)이 호출되고 패널이 닫힙니다.
        /// </summary>
        public void SetupMultiCardPicker(List<CardData> cards, int count,
                                         System.Action<List<CardData>> onComplete)
        {
            _customPickPool        = cards ?? new List<CardData>();
            _onCustomPick          = null;
            _multiSelectActive     = true;
            _multiSelectRequired   = Mathf.Max(1, count);
            _onMultiSelectComplete = onComplete;
            _multiSelectedUIs.Clear();
            currentRestMode        = RestCardMode.Remove;
            onFinishCallback       = null;
            RebuildGrid();
            if (scrollRect != null && scrollRect.content != null)
                scrollRect.normalizedPosition = new Vector2(0f, 1f);
        }

        private void ResetMultiSelectState()
        {
            _multiSelectActive     = false;
            _multiSelectRequired   = 0;
            _onMultiSelectComplete = null;
            _multiSelectedUIs.Clear();
        }

        /// <summary>
        /// 사용자가 픽을 확정하지 않고 패널을 닫았을 때(백버튼 등) 호출됩니다.
        /// 단일 픽/다중 선택 콜백이 아직 살아있으면 취소로 처리하고 상태를 비웁니다.
        /// </summary>
        public void CancelPickerIfPending()
        {
            if (_onMultiSelectComplete != null)
            {
                var mcb = _onMultiSelectComplete;
                _onMultiSelectComplete = null;
                _multiSelectActive     = false;
                _multiSelectedUIs.Clear();
                _customPickPool        = null;
                mcb.Invoke(new List<CardData>()); // 확정 없이 닫힘 → 빈 목록(취소)
                return;
            }
            if (_onCustomPick == null) return;
            var cb = _onCustomPick;
            _onCustomPick   = null;
            _customPickPool = null;
            cb.Invoke(null);
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Public API — Draw / Discard / Deck 모드

        public void Refresh()
        {
            currentCards = GetCards();
            if (mode == CardListMode.Deck && currentCards.Count == 0)
            {
                var dm = DeckManager.Instance;
                Debug.LogWarning($"[CardListController] Deck 모드 — 표시할 카드 0장. " +
                    $"DeckManager.Instance={(dm != null ? "있음" : "null")}, " +
                    $"MasterDeckCount={(dm != null ? dm.MasterDeckCount : -1)}, " +
                    $"cardGridContainer={(cardGridContainer != null ? "할당" : "null")}, " +
                    $"cardPrefab={(cardPrefab != null ? "할당" : "null")}");
            }
            RebuildGrid();
            UpdateHeader();
        }

        /// <summary>외부에서 모드를 강제로 설정. 다음 Refresh부터 새 모드 기준으로 카드 목록을 표시한다.
        /// 주로 InGameUIController가 deckViewerPanel을 열 때 Deck 모드를 보장하기 위해 사용.
        /// 모드 의존 UI(sortButtonGroup/classButtonGroup)도 함께 갱신해 인스펙터의 초기 mode와
        /// 실제 사용 모드가 달라도 정렬/필터 버튼이 정확히 표시되도록 한다.</summary>
        public void SetMode(CardListMode newMode)
        {
            if (mode == newMode) return;
            mode = newMode;
            ApplyModeDependentUI();
            // Dictionary↔Deck 전환 시 sortAcquiredButton의 매핑(Rarity/Acquired)이 바뀌므로 화살표도 재평가.
            UpdateSortButtonArrows();
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Card Data

        private List<CardData> GetCards()
        {
            // Dictionary 모드는 DeckManager 없이도 동작 (메인 메뉴에서 사용).
            if (mode == CardListMode.Dictionary)
                return ApplySort(BuildDictionaryPoolForFilter(selectedFilter, showAllAsDiscovered),
                                 currentSort, _currentSortAsc);

            var dm = DeckManager.Instance;
            if (dm == null) return new List<CardData>();

            // 400 유물: 드로우 순서대로 (위→아래, 1행씩 — 첫 카드가 다음에 뽑힐 카드)
            bool drawOrderRelic = GameManager.Instance != null && GameManager.Instance.HasRelic(400);

            return mode switch
            {
                CardListMode.Draw    => drawOrderRelic ? dm.GetDrawPileInOrder() : SortByRarity(dm.GetDrawPileForView()),
                CardListMode.Discard => SortByRarity(dm.GetDiscardPileForView()),
                CardListMode.Deck    => ApplySort(dm.MasterDeck, currentSort, _currentSortAsc),
                _                    => new List<CardData>()
            };
        }

        /// <summary>Dictionary 모드용 — 선택된 필터에 맞는 카드 풀을 반환.
        /// showAllAsDiscovered=true면 발견 여부와 무관하게 모든 카드를 실제 데이터로 표시.</summary>
        private static List<CardData> BuildDictionaryPoolForFilter(DictionaryFilter filter, bool showAllAsDiscovered)
        {
            if (filter == DictionaryFilter.Misc)
                return BuildMiscPool(showAllAsDiscovered);
            CharacterType character = filter switch
            {
                DictionaryFilter.Warrior => CharacterType.Warrior,
                DictionaryFilter.Gunner  => CharacterType.Gunner,
                DictionaryFilter.Mage    => CharacterType.Mage,
                _                        => CharacterType.Warrior,
            };
            return BuildDictionaryPool(character, showAllAsDiscovered);
        }

        /// <summary>Dictionary 모드용 — 선택 직업이 사용 가능한 카드(공통+직업, 강화 전).
        /// 미발견 카드는 80002(미지) 플레이스홀더로 대체. showAllAsDiscovered=true면 모두 실제 카드로 표시.</summary>
        private static List<CardData> BuildDictionaryPool(CharacterType character, bool showAllAsDiscovered)
        {
            var pool = CardRegistry.GetRewardPool(character);
            if (pool == null || pool.Count == 0)
                Debug.LogWarning($"[CardListController] Dictionary {character} 풀이 비어있음. " +
                    "CardRegistry Addressables 'Cards' 라벨이 비어있거나 직업 카드가 임포트되지 않았을 수 있음.");

            var placeholder = CardRegistry.GetCard(DiscoveryManager.PlaceholderCardCode);

            var result = new List<CardData>();
            foreach (var c in pool)
            {
                if (c == null) continue;
                if (c.IsUpgraded) continue;
                // 공통 카드(ClassDigit==1)는 기타 탭 전용이므로 직업 탭에서 제외.
                if (c.ClassDigit == 1) continue;
                bool discovered = showAllAsDiscovered || DiscoveryManager.IsCardDiscovered(c.cardCode);
                result.Add(discovered ? c : (placeholder != null ? placeholder : c));
            }
            result.Sort((a, b) => a.cardCode.CompareTo(b.cardCode));
            return result;
        }

        /// <summary>기타 필터 — 모든 공통 카드(ClassDigit==1, 강화 전) + 80001 카드.
        /// 미발견 카드는 80002(미지) 플레이스홀더로 대체. showAllAsDiscovered=true면 모두 실제 카드로 표시.</summary>
        private static List<CardData> BuildMiscPool(bool showAllAsDiscovered)
        {
            // GetRewardPool은 직업 카드도 같이 포함하므로 직접 전체 카드를 순회.
            // 공통 카드만 따로 가져오는 API가 없어 Warrior 풀을 가져와도 ClassDigit==1을 필터링하면 동일하지만,
            // 확실하게 하기 위해 GetCard로 80001을 별도 추가.
            var basePool = CardRegistry.GetRewardPool(CharacterType.Warrior);
            var placeholder = CardRegistry.GetCard(DiscoveryManager.PlaceholderCardCode);
            var added = new HashSet<int>();
            var result = new List<CardData>();

            foreach (var c in basePool)
            {
                if (c == null) continue;
                if (c.IsUpgraded) continue;
                if (c.ClassDigit != 1) continue; // 공통 카드만
                if (!added.Add(c.cardCode)) continue;
                bool discovered = showAllAsDiscovered || DiscoveryManager.IsCardDiscovered(c.cardCode);
                result.Add(discovered ? c : (placeholder != null ? placeholder : c));
            }

            // 80001 카드는 ClassDigit가 8이라 공통 풀에 안 포함됨 → 별도 추가.
            if (added.Add(MiscIncludedCardCode))
            {
                var extra = CardRegistry.GetCard(MiscIncludedCardCode);
                if (extra != null)
                {
                    bool discovered = showAllAsDiscovered || DiscoveryManager.IsCardDiscovered(extra.cardCode);
                    result.Add(discovered ? extra : (placeholder != null ? placeholder : extra));
                }
            }

            if (result.Count == 0)
                Debug.LogWarning("[CardListController] Dictionary 기타 풀이 비어있음. " +
                    "공통 카드/80001 카드 임포트 상태 확인 필요.");

            result.Sort((a, b) => a.cardCode.CompareTo(b.cardCode));
            return result;
        }

        private void SetSelectedFilter(DictionaryFilter filter)
        {
            selectedFilter = filter;
            ApplyClassButtonHighlight();
            Refresh();
            if (scrollRect != null && scrollRect.content != null)
                scrollRect.normalizedPosition = new Vector2(0f, 1f);
        }

        private void ApplyClassButtonHighlight()
        {
            SetClassButtonColor(warriorButton, selectedFilter == DictionaryFilter.Warrior);
            SetClassButtonColor(gunnerButton,  selectedFilter == DictionaryFilter.Gunner);
            SetClassButtonColor(mageButton,    selectedFilter == DictionaryFilter.Mage);
            SetClassButtonColor(miscButton,    selectedFilter == DictionaryFilter.Misc);
        }

        private void SetClassButtonColor(Button button, bool selected)
        {
            if (button == null) return;
            var colors = button.colors;
            Color c = selected ? selectedClassColor : unselectedClassColor;
            colors.normalColor      = c;
            colors.highlightedColor = c;
            colors.selectedColor    = c;
            button.colors = colors;
        }

        private static List<CardData> SortByRarity(List<CardData> cards)
        {
            var nameComparer = GetLocalizedNameComparer();
            return cards.OrderByDescending(c => c.RarityDigit)
                        .ThenBy(GetLocalizedCardName, nameComparer)
                        .ToList();
        }

        private static List<CardData> ApplySort(List<CardData> cards, DeckSortMode sort, bool ascending)
        {
            var nameComparer = GetLocalizedNameComparer();
            List<CardData> ordered = sort switch
            {
                // 종류 정렬: cardCode T자리(1이동/2액션/3파워) → 같은 종류 내에서는 현지화 이름.
                DeckSortMode.Type   => cards.OrderBy(c => c.TypeDigit)
                                            .ThenBy(GetLocalizedCardName, nameComparer)
                                            .ToList(),
                // 이름 정렬: 현지화된 이름을 현재 언어 CultureInfo 기준으로 자연 정렬.
                DeckSortMode.Name   => cards.OrderBy(GetLocalizedCardName, nameComparer).ToList(),
                // 희귀도 정렬: 오름차순일 땐 Common→Uncommon→Rare (RarityDigit 오름차순) — 같은 등급 내에서는 이름순.
                DeckSortMode.Rarity => cards.OrderBy(c => c.RarityDigit)
                                            .ThenBy(GetLocalizedCardName, nameComparer)
                                            .ToList(),
                _                   => new List<CardData>(cards) // Acquired: 원본 순서 유지
            };
            // 내림차순일 땐 전체 리스트를 뒤집어 적용 (primary/secondary 동시에 역순).
            if (!ascending) ordered.Reverse();
            return ordered;
        }

        /// <summary>card_name_{cardCode} 로컬라이즈 키 조회. 없으면 cardName 원본(한국어) fallback.</summary>
        private static string GetLocalizedCardName(CardData card)
        {
            if (card == null) return string.Empty;
            return DeckRoguelike.Core.LocalizationManager.GetOrNull($"card_name_{card.cardCode}")
                   ?? card.cardName ?? string.Empty;
        }

        /// <summary>현재 언어의 CultureInfo로 StringComparer를 생성. 매핑 실패 시 InvariantCulture로 폴백.
        /// Language enum 이름(en, ko, zh_CN, pt_BR, sr_Latn 등)을 BCP-47 형식(en, ko, zh-CN ...)으로 변환.</summary>
        private static System.Collections.Generic.IComparer<string> GetLocalizedNameComparer()
        {
            string langCode = DeckRoguelike.Core.LocalizationManager.CurrentLanguage.ToString().Replace('_', '-');
            try
            {
                var culture = System.Globalization.CultureInfo.GetCultureInfo(langCode);
                return System.StringComparer.Create(culture, ignoreCase: true);
            }
            catch (System.Globalization.CultureNotFoundException)
            {
                return System.StringComparer.Create(System.Globalization.CultureInfo.InvariantCulture, ignoreCase: true);
            }
        }

        private void SetSort(DeckSortMode sort)
        {
            // 단일 활성 정렬 모델:
            //  - 같은 정렬 버튼을 다시 누르면 방향 토글 (오름차순 ↔ 내림차순)
            //  - 다른 정렬 버튼을 누르면 그 버튼이 활성으로 전환되고 내림차순으로 시작
            //  - 비활성 정렬 버튼들은 항상 오름차순 상태로 표시됨
            // → 사용자 요구: "전부 오름차순 시작 → 한 버튼 클릭 시 그 버튼만 내림차순, 나머지는 오름차순 유지"
            if (currentSort == sort)
            {
                _currentSortAsc = !_currentSortAsc;
            }
            else
            {
                currentSort = sort;
                _currentSortAsc = false; // 새 정렬 항목 클릭 시 내림차순으로 시작
            }
            currentCards = GetCards();
            RebuildGrid();
            UpdateSortButtonArrows();
        }

        /// <summary>각 정렬 버튼의 화살표 sprite 방향을 갱신.
        /// 활성 정렬 버튼은 _currentSortAsc 값에 따라 ▲(localScale.y=1) 또는 ▼(localScale.y=-1)로 표시되고,
        /// 비활성 버튼은 항상 ▲ 방향(localScale.y=1)으로 유지된다. sprite 자체는 오름차순(▲) 모양으로 그려져야 함.</summary>
        private void UpdateSortButtonArrows()
        {
            // sortAcquiredButton은 Dictionary 모드에선 Rarity, 그 외(Deck)에선 Acquired로 동작.
            DeckSortMode firstButtonSort = (mode == CardListMode.Dictionary)
                ? DeckSortMode.Rarity
                : DeckSortMode.Acquired;
            UpdateSortArrowImage(sortAcquiredArrow, firstButtonSort);
            UpdateSortArrowImage(sortTypeArrow,    DeckSortMode.Type);
            UpdateSortArrowImage(sortNameArrow,    DeckSortMode.Name);
        }

        private void UpdateSortArrowImage(UnityEngine.UI.Image arrow, DeckSortMode sortMode)
        {
            if (arrow == null) return;
            // 비활성 버튼은 오름차순(원래 sprite 방향) 유지. 활성 버튼만 _currentSortAsc=false 시 상하 반전.
            bool ascending = (currentSort != sortMode) || _currentSortAsc;
            var rt = arrow.transform;
            var s  = rt.localScale;
            s.y = ascending ? Mathf.Abs(s.y) : -Mathf.Abs(s.y);
            rt.localScale = s;
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Grid

        private void RebuildGrid()
        {
            if (cardGridContainer == null)
            {
                Debug.LogWarning($"[CardListController] cardGridContainer 미할당 (mode={mode}). Inspector 확인 필요.");
                return;
            }
            if (cardPrefab == null)
            {
                Debug.LogWarning($"[CardListController] cardPrefab 미할당 (mode={mode}). Inspector 확인 필요.");
                return;
            }

            var toDestroy = new List<GameObject>();
            foreach (Transform child in cardGridContainer) toDestroy.Add(child.gameObject);
            foreach (var obj in toDestroy) { obj.transform.SetParent(null); Destroy(obj); }

            if (mode == CardListMode.Sub)
            {
                RebuildSubGrid();
                return;
            }

            if (mode == CardListMode.Dictionary && currentCards.Count == 0)
                Debug.LogWarning($"[CardListController] Dictionary {selectedFilter} — 표시할 카드가 0장. " +
                    "CardRegistry 로드 상태/필터 풀/임포트된 카드 자산 확인 필요.");

            for (int i = 0; i < currentCards.Count; i++)
            {
                var capturedCard = currentCards[i];
                var item   = Instantiate(cardPrefab, cardGridContainer);
                var cardUI = item.GetComponent<CardUI>();
                if (cardUI == null) continue;

                cardUI.Initialize(capturedCard);
                cardUI.IsRuntimeUnplayable = true;

                if (mode == CardListMode.Deck || mode == CardListMode.Dictionary)
                {
                    cardUI.SetClickable(true);
                    int capturedIndex = i;
                    cardUI.OnCardViewClicked += _ => OpenRangeInfo(capturedCard, capturedIndex);
                }
                else
                {
                    cardUI.SetClickable(false);
                }

                AddListHoverEffect(item);
            }

            if (cardGridContainer is RectTransform rt)
            {
                // ContentSizeFitter가 새 카드 수에 맞는 preferredHeight를 sizeDelta.y에 반영해야
                // ScrollRect의 scrollable 범위(content.rect.height - viewport.rect.height)가 갱신된다.
                // ForceRebuildLayoutImmediate만으로는 RectTransform.rect 캐시가 즉시 갱신되지 않는
                // 케이스가 있어, Canvas.ForceUpdateCanvases로 한 번 더 flush해 WheelScrollHandler가
                // 새 카드 행까지 스크롤 가능하도록 보장.
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                Canvas.ForceUpdateCanvases();
            }
        }

        private void RebuildSubGrid()
        {
            if (_multiSelectActive)
            {
                RebuildMultiSelectGrid();
                return;
            }

            List<CardData> cards;
            if (_customPickPool != null)
            {
                cards = _customPickPool;
            }
            else
            {
                var dm = DeckManager.Instance;
                if (dm == null) return;

                cards = currentRestMode == RestCardMode.Upgrade
                    ? dm.MasterDeck.Where(c => !c.IsUpgraded && CardRegistry.GetCard(c.cardCode + 1) != null).ToList()
                    : dm.MasterDeck.Where(c => !c.NonRemovable).ToList();
            }

            foreach (var card in cards)
            {
                var captured = card;
                var item   = Instantiate(cardPrefab, cardGridContainer);
                var cardUI = item.GetComponent<CardUI>();
                if (cardUI == null) continue;

                cardUI.Initialize(card);
                cardUI.IsRuntimeUnplayable = true;
                cardUI.SetClickable(true);
                cardUI.OnCardViewClicked += _ => OnSubCardClicked(captured);
                AddListHoverEffect(item);
            }

            if (cardGridContainer is RectTransform rt)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                Canvas.ForceUpdateCanvases();
            }
        }

        /// <summary>다중 선택 모드 그리드. 카드 클릭 시 토글 선택되며, confirm 패널을 거치지 않는다.</summary>
        private void RebuildMultiSelectGrid()
        {
            _multiSelectedUIs.Clear();

            foreach (var card in _customPickPool)
            {
                var item   = Instantiate(cardPrefab, cardGridContainer);
                var cardUI = item.GetComponent<CardUI>();
                if (cardUI == null) continue;

                cardUI.Initialize(card);
                cardUI.IsRuntimeUnplayable = true;
                cardUI.SetClickable(true);
                // 뷰어 카드의 click(=PointerUp + 짧은 이동)이 OnCardViewClicked로 발화된다 → 토글로 사용.
                cardUI.OnCardViewClicked += ToggleMultiSelect;
                AddListHoverEffect(item);
            }

            if (cardGridContainer is RectTransform rt)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                Canvas.ForceUpdateCanvases();
            }
        }

        private void ToggleMultiSelect(CardUI ui)
        {
            if (ui == null || !_multiSelectActive) return;

            if (_multiSelectedUIs.Contains(ui))
            {
                _multiSelectedUIs.Remove(ui);
                ui.SetMultiSelected(false);
                return;
            }

            _multiSelectedUIs.Add(ui);
            ui.SetMultiSelected(true);

            if (_multiSelectedUIs.Count >= _multiSelectRequired)
                CompleteMultiSelect();
        }

        private void CompleteMultiSelect()
        {
            var picked = _multiSelectedUIs
                .Select(u => u.CardData)
                .Where(c => c != null)
                .ToList();

            var cb = _onMultiSelectComplete;
            _onMultiSelectComplete = null;
            _multiSelectActive     = false;
            _multiSelectedUIs.Clear();
            _customPickPool        = null;

            // confirm 패널 없이 즉시 닫고 콜백 실행. (CloseRestCardList → CancelPickerIfPending는
            // _onMultiSelectComplete가 이미 null이라 취소 콜백을 발화하지 않는다.)
            InGameUIController.Instance?.CloseRestCardList();
            cb?.Invoke(picked);
        }

        private void OnSubCardClicked(CardData card)
        {
            cardConfirmPanel?.Open(card, currentRestMode, OnSubConfirmed);
        }

        private void OnSubConfirmed(CardData card)
        {
            // 커스텀 피커 모드: 콜백만 호출하고 종료 (덱 변경 없음)
            if (_onCustomPick != null)
            {
                var cb = _onCustomPick;
                _onCustomPick   = null;
                _customPickPool = null;
                InGameUIController.Instance?.CloseRestCardList();
                cb.Invoke(card);
                return;
            }

            if (currentRestMode == RestCardMode.Upgrade)
                DeckManager.Instance?.UpgradeCard(card);
            else
                DeckManager.Instance?.RemoveCardFromDeck(card);

            InGameUIController.Instance?.ClearRestCancelCallback();
            InGameUIController.Instance?.CloseRestCardList();

            if (onFinishCallback != null)
                onFinishCallback.Invoke();
            else
                FinishRestAction();
        }

        private void FinishRestAction()
        {
            FindObjectOfType<BoardController>()?.ExhaustRewardCards();
        }

        /// <summary>Deck/Dictionary 모드 — 카드 클릭 시 CardInfoController를 열어 사거리/효과를 표시.
        /// 메인 메뉴 씬에서는 MainMenuController가, 인게임 씬에서는 InGameUIController가 패널을 관리.
        /// currentCards 전체 리스트를 같이 넘겨 좌/우 nav 버튼으로 인접 카드를 탐색할 수 있게 한다.
        ///
        /// ⚠ InGame도 단일 카드(ShowRangeInfo)가 아닌 리스트(ShowCardInfoList)로 호출해야 함 —
        /// 단일 카드로 열면 CardInfoController.navList=null이 되어 좌/우 nav 버튼이 비활성화됨.</summary>
        private void OpenRangeInfo(CardData card, int index)
        {
            if (card == null) return;

            var menu = MainMenuController.Instance;
            if (menu != null)
            {
                menu.ShowCardInfo(currentCards, index);
                return;
            }

            InGameUIController.Instance?.ShowCardInfoList(currentCards, index);
        }

        private void AddListHoverEffect(GameObject cardObj)
        {
            // EventTrigger를 쓰면 안 됨 — EventTrigger는 IScrollHandler 등 모든 핸들러 인터페이스를
            // 동시에 구현하기 때문에, 카드 위에 마우스를 올린 상태에서 휠을 굴리면
            // EventSystem이 OnScroll을 EventTrigger에서 소비해버리고 부모 ScrollRect로 전달되지 않음.
            // → IPointerEnter/Exit만 구현한 전용 헬퍼를 사용.
            var scaler = cardObj.GetComponent<CardHoverScaler>() ?? cardObj.AddComponent<CardHoverScaler>();
            scaler.hoverScale = listHoverScale;
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Header

        private void UpdateHeader()
        {
            if (titleText != null)
                titleText.text = mode switch
                {
                    CardListMode.Draw       => "드로우 더미",
                    CardListMode.Discard    => "버린 카드",
                    CardListMode.Deck       => "덱 보기",
                    CardListMode.Dictionary => "카드 도감",
                    _                       => string.Empty
                };

            if (countText != null)
                countText.text = $"{currentCards.Count}";
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Setup

        private void SetupScrollView()
        {
            if (scrollRect == null && cardGridContainer != null)
                scrollRect = cardGridContainer.GetComponentInParent<ScrollRect>();

            if (scrollRect == null) return;

            scrollRect.horizontal        = false;
            scrollRect.vertical          = true;
            // Clamped — Elastic의 경계 스프링이 매 프레임 SmoothDamp 보정을 돌려서
            // 우리의 클램프와 겹쳐 경계 부근에서 떨림(zitter)이 발생. 우리가 이미 idealY를
            // [0, scrollableHeight]로 강하게 클램프하므로 Elastic 효과는 불필요.
            scrollRect.movementType      = ScrollRect.MovementType.Clamped;

            // 관성 기반 휠 스크롤 — 공용 WheelScrollHandler 를 ScrollRect 에 부착하고 튜닝 asset 을 주입.
            // WheelScrollHandler.Awake 가 scrollSensitivity=0, inertia=false 로 잠그므로
            // 여기서 sensitivity 를 별도로 건드리지 않는다. (휠 이중 처리로 "점프 → 되돌아옴" 버그 차단)
            if (wheelTuning != null)
                DeckRoguelike.UI.WheelScrollHandler.AttachTo(scrollRect, wheelTuning);

            if (cardGridContainer is RectTransform content)
            {
                // ScrollRect.content가 Inspector에서 비어 있으면(예: Ingame씬 deckViewerPanel) 휠/드래그
                // 입력에 반응하지 못해 카드 보상으로 행이 추가됐을 때 아래로 스크롤이 안 되는 버그가 생긴다.
                // cardGridContainer가 그 자체로 스크롤 대상이므로 런타임에 강제로 연결.
                if (scrollRect.content == null)
                    scrollRect.content = content;

                content.anchorMin        = new Vector2(0f, 1f);
                content.anchorMax        = new Vector2(1f, 1f);
                content.pivot            = new Vector2(0.5f, 1f);
                content.anchoredPosition = Vector2.zero;
                content.sizeDelta        = new Vector2(0f, content.sizeDelta.y);
            }
        }

        private void SetupGridLayout()
        {
            if (cardGridContainer == null) return;

            var grid = cardGridContainer.GetComponent<GridLayoutGroup>()
                    ?? cardGridContainer.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.cellSize        = cardCellSize;
            grid.spacing         = new Vector2(cardSpacingX, cardSpacingY);
            grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis       = GridLayoutGroup.Axis.Horizontal;

            var fitter = cardGridContainer.GetComponent<ContentSizeFitter>()
                      ?? cardGridContainer.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            grid.childAlignment = TextAnchor.UpperCenter;
            grid.padding = new RectOffset(paddingLeftRight, paddingLeftRight,
                                          paddingTopBottom, paddingTopBottom);
        }


        #endregion

        // ──────────────────────────────────────────────
        #region 카드 호버 스케일러

        /// <summary>리스트 카드 hover 시 scale을 키우는 전용 컴포넌트.
        /// IPointerEnter/Exit만 구현 — EventTrigger처럼 IScrollHandler까지 같이 구현하지 않으므로
        /// 카드 위에 마우스를 올린 상태로 휠을 굴려도 OnScroll이 부모 ScrollRect로 그대로 전달된다.</summary>
        private class CardHoverScaler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public float hoverScale = 1.3f;
            private DeckRoguelike.Cards.CardUI _cardUI;
            private void Awake() => _cardUI = GetComponent<DeckRoguelike.Cards.CardUI>();
            public void OnPointerEnter(PointerEventData _) => transform.localScale = Vector3.one * hoverScale;
            // 다중 선택 패널에서 '선택됨' 카드는 hover가 풀려도 살짝 확대된 baseline을 유지한다.
            public void OnPointerExit (PointerEventData _) =>
                transform.localScale = Vector3.one *
                    (_cardUI != null && _cardUI.IsMultiSelected ? _cardUI.MultiSelectScale : 1f);
        }

        #endregion

        // ──────────────────────────────────────────────
        #region 휠 스크롤 내부 헬퍼 (legacy block removed)

        // (관성 휠 스크롤 본체는 DeckRoguelike.UI.WheelScrollHandler 로 분리됨)

        #endregion
    }
}
