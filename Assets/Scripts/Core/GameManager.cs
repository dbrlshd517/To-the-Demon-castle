using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using DeckRoguelike.Cards;
using DeckRoguelike.Combat;
using DeckRoguelike.Relic;
using DeckRoguelike.Item;
using DeckRoguelike.UI;

namespace DeckRoguelike.Core
{
    /// <summary>
    /// 게임 전체 상태를 관리하는 싱글톤 매니저
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game State")]
        [SerializeField] private int currentFloor = 1;
        [SerializeField] private int maxFloors = 50;
        [SerializeField] private int currentAct = 1;

        [Header("Character Data")]

        [Header("Player Run Data")]
        [SerializeField] private CharacterType selectedCharacter;
        [SerializeField] private int gold = 99;
        [SerializeField] private int currentHP;
        [SerializeField] private int maxHP;
        [SerializeField] private int runSeed;
        private SeededRandom seededRandom;
        private int[] startingDeckCodes;

        [Header("Game Difficulty")]
        [Tooltip("런 시작 시 결정. 1=normal 2=hard 3=hell")]
        [SerializeField] private int gameDifficulty = 1;

        // 현재 노드 타입
        private NodeType currentNodeType = NodeType.Combat;

        // 진행 중 이벤트 baseCode — Event/Treasure 노드 재진입 시 사용
        private int currentEventCode;
        public int CurrentEventCode => currentEventCode;
        public void SetCurrentEventCode(int code) { currentEventCode = code; }

        // 현재 Event 노드의 sub-type — BoardController가 dispatch 직전에 기록한다.
        // Continue 시에는 mapSeed로 맵이 결정론적으로 재생성되므로 별도 save 필드 불필요
        // (단, 진입한 직후 재진입 케이스를 위해 currentEventCode 기반으로 sub-type을 역추정 가능).
        private DeckRoguelike.UI.EventSubType currentEventSubType = DeckRoguelike.UI.EventSubType.None;
        public DeckRoguelike.UI.EventSubType CurrentEventSubType => currentEventSubType;
        public void SetCurrentEventSubType(DeckRoguelike.UI.EventSubType subType) { currentEventSubType = subType; }

        // 101/102 유물 — 직전 노드 추적 (전투 시작 시 첫 턴 카드 +2)
        private bool lastNodeWasShop;
        private bool lastNodeWasRest;
        private bool lastNodeWasMystery;

        [Header("Statistics")]
        [SerializeField] private int enemiesDefeated = 0;
        [SerializeField] private int cardsPlayed = 0;
        [SerializeField] private int totalDamageDealt = 0;
        [SerializeField] private float runTime = 0f;

        // Events
        public event Action<int> OnFloorChanged;
        public event Action<int> OnGoldChanged;
        public event Action<int, int> OnHPChanged;
        public event Action OnRunStarted;
        public event Action<bool> OnRunEnded; // true = victory
        public event Action<RelicEffect> OnRelicAdded;
        /// <summary>유물이 업그레이드 유물로 교체될 때 발생합니다 (예: 70→970). 슬롯/아이콘 위치를 보존하기 위해 UI에서 인플레이스 갱신에 사용합니다.</summary>
        public event Action<RelicEffect, RelicEffect> OnRelicReplaced;
        /// <summary>유물 제거 시 발생 (207/208/209 각인된 부적의 픽 취소 등 — UI에서 아이콘 제거).</summary>
        public event Action<RelicEffect> OnRelicRemoved;
        public event Action<ItemData>   OnItemAdded;
        public event Action<ItemData>   OnItemRemoved;

        // 액트 난이도 (전투 2회마다 +1, 액트 전환 시 초기화)
        private int actDifficulty = 1;
        private int battlesCompletedInAct = 0;

        // 카드 보상 희귀도 확률 (런 내 누적, 일반카드 보상 때마다 희귀 +1%)
        private int rareChanceBonus = 0;  // 0 ~ 60 (일반 확률 한계)

        // 상점 카드 제거 가격 (첫 이용 75G, 이후 25G씩 증가)
        private const int CardRemovalBasePrice = 75;
        private const int CardRemovalPriceStep = 25;
        private int cardRemovalUseCount = 0;

        // 상점 결정론 — 런 시작 시 시드 풀을 미리 생성, 방문마다 1개씩 소비
        private const int ShopSeedPoolSize = 10;
        private List<int> shopSeeds = new List<int>();
        private int shopVisitCount = 0;
        public int ShopVisitCount => shopVisitCount;

        // 유물
        private readonly List<RelicEffect> relics = new List<RelicEffect>();

        // 아이템
        private readonly List<ItemData> items = new List<ItemData>();

        // Properties
        public bool IsPlaying { get; private set; }
        public bool IsInCombat { get; set; }
        public int CurrentFloor => currentFloor;
        public int CurrentAct => currentAct;
        public int GameDifficulty => gameDifficulty;
        public int ActDifficulty => actDifficulty;
        public CharacterType SelectedCharacter => selectedCharacter;
        public int Gold => gold;
        public int CurrentHP => currentHP;
        public int MaxHP => maxHP;
        public int RunSeed => runSeed;
        public SeededRandom Rng => seededRandom;
        public int[] StartingDeckCodes => startingDeckCodes;
        public NodeType CurrentNodeType => currentNodeType;
        public bool IsDangerCombatEncounter => currentNodeType == NodeType.DangerCombat;
        public bool IsBossEncounter => currentNodeType == NodeType.Boss;
        public int RareChanceBonus    => rareChanceBonus;
        // 402 유물 — 카드 제거 비용 50G 고정
        public int CardRemovalPrice   => HasRelic(402)
            ? 50
            : CardRemovalBasePrice + cardRemovalUseCount * CardRemovalPriceStep;
        public bool LastNodeWasShop => lastNodeWasShop;
        public bool LastNodeWasRest => lastNodeWasRest;
        public bool LastNodeWasMystery => lastNodeWasMystery;
        public int EnemiesDefeated => enemiesDefeated;
        public float RunTime => runTime;
        public IReadOnlyList<RelicEffect> Relics => relics;
        public IReadOnlyList<ItemData>   Items  => items;

        private void Awake()
        {
            Debug.Log($"[GameManager] Awake START | name={name} | scene={gameObject.scene.name}");

            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Debug.Log($"[GameManager] After DDOL | scene={gameObject.scene.name}");
                Initialize();
            }
            else
            {
                Debug.Log($"[GameManager] Duplicate -> Destroy | name={name} | scene={gameObject.scene.name}");
                Destroy(gameObject);
            }
        }
        
        private void OnDestroy()
        {
          //  Debug.LogError($"[GameManager] OnDestroy | name={name} | scene={gameObject.scene.name}\n{Environment.StackTrace}");
        }
        
        private void Initialize()
        {
            Application.targetFrameRate = 60;
            Debug.Log("[GameManager] 덱빌딩 로그라이크 게임 초기화 완료");
        }

        private void Update()
        {
            if (IsPlaying)
            {
                runTime += Time.deltaTime;
            }
        }

        #region State Transitions

        public void EnterPlaying()
        {
            IsPlaying = true;
            Time.timeScale = 1f;
        }

        public void EnterMainMenu()
        {
            IsPlaying = false;
            IsInCombat = false;
            Time.timeScale = 1f;
        }

        public void EnterGameOver()
        {
            IsPlaying = false;
            IsInCombat = false;
            RunSaveSystem.Delete();
            OnRunEnded?.Invoke(false);
        }

        public void EnterVictory()
        {
            IsPlaying = false;
            IsInCombat = false;
            RunSaveSystem.Delete();
            OnRunEnded?.Invoke(true);
        }

        #endregion

        #region Run Management

        public void StartNewRun(CharacterType character, int startMaxHP = 75, int startGold = 99, int[] deckCodes = null)
        {
            selectedCharacter = character;
            currentFloor = 1;
            currentAct = 1;
            actDifficulty = 1;
            battlesCompletedInAct = 0;
            enemiesDefeated = 0;
            cardsPlayed = 0;
            totalDamageDealt = 0;
            runTime = 0f;
            currentNodeType = NodeType.Combat;
            rareChanceBonus    = 0;
            cardRemovalUseCount = 0;

            relics.Clear();
            items.Clear();

            // 랜덤 시드 생성
            runSeed = UnityEngine.Random.Range(0, 99999);
            seededRandom = new SeededRandom(runSeed);

            // 상점 시드 풀 미리 생성 — 재시작해도 N번째 상점은 동일한 진열
            shopSeeds.Clear();
            for (int i = 0; i < ShopSeedPoolSize; i++)
                shopSeeds.Add(UnityEngine.Random.Range(1, int.MaxValue));
            shopVisitCount = 0;

            // 노드 미진입 상태 — Continue 자동 복원 로직이 시작 셀에서 트리거되지 않도록
            currentNodeType = NodeType.Unknown;
            currentEventCode = 0;

            maxHP             = startMaxHP;
            gold              = startGold;
            startingDeckCodes = deckCodes;
            currentHP         = maxHP;

            // 시작 덱 카드를 도감 발견 처리 — DeckManager 인스턴스화 전이라 직접 등록.
            if (startingDeckCodes != null)
            {
                foreach (var code in startingDeckCodes)
                    DiscoveryManager.DiscoverCard(code);
            }

            Debug.Log($"[GameManager] 새 런 시작 - 캐릭터: {character}, 시드: {runSeed}");
            OnRunStarted?.Invoke();
            OnHPChanged?.Invoke(currentHP, maxHP);
            OnGoldChanged?.Invoke(gold);

            SaveRun();
        }

        public void AbandonRun()
        {
            RunSaveSystem.Delete();
            EnterMainMenu();
            SceneLoader.Instance?.LoadScene("MainMenu");
        }

        public void SaveRun()
        {
            var data = new RunSaveData
            {
                runSeed              = runSeed,
                characterType        = (int)selectedCharacter,
                currentFloor         = currentFloor,
                currentAct           = currentAct,
                actDifficulty        = actDifficulty,
                battlesCompletedInAct = battlesCompletedInAct,
                gameDifficulty       = gameDifficulty,
                currentHP            = currentHP,
                maxHP                = maxHP,
                gold                 = gold,
                enemiesDefeated      = enemiesDefeated,
                cardsPlayed          = cardsPlayed,
                totalDamageDealt     = totalDamageDealt,
                runTime              = runTime,
                cardRemovalUseCount  = cardRemovalUseCount,
                rareChanceBonus      = rareChanceBonus,
                currentNodeType      = (int)currentNodeType,
                currentEventCode     = currentEventCode,
                shopSeeds            = new List<int>(shopSeeds),
                shopVisitCount       = shopVisitCount,
            };

            // 덱
            var deck = DeckRoguelike.Combat.DeckManager.Instance;
            if (deck != null)
                foreach (var card in deck.MasterDeck)
                    if (card != null) data.deckCardCodes.Add(card.cardCode);

            // 유물
            foreach (var r in relics)
                if (r?.Data != null) data.relicCodes.Add(r.Data.relicCode);

            // 아이템
            foreach (var item in items)
                if (item != null) data.itemCodes.Add(item.itemCode);

            // 맵 상태 — BoardController 경유
            var boardController = UnityEngine.Object.FindObjectOfType<DeckRoguelike.UI.BoardController>();
            if (boardController != null)
                boardController.WriteMapSaveData(data);

            RunSaveSystem.Save(data);
        }

        public bool LoadRun()
        {
            var data = RunSaveSystem.Load();
            if (data == null) return false;

            selectedCharacter     = (CharacterType)data.characterType;
            runSeed               = data.runSeed;
            seededRandom          = new SeededRandom(runSeed);
            currentFloor          = data.currentFloor;
            currentAct            = data.currentAct;
            actDifficulty         = data.actDifficulty;
            battlesCompletedInAct = data.battlesCompletedInAct;
            gameDifficulty        = data.gameDifficulty;
            currentHP             = data.currentHP;
            maxHP                 = data.maxHP;
            gold                  = data.gold;
            enemiesDefeated       = data.enemiesDefeated;
            cardsPlayed           = data.cardsPlayed;
            totalDamageDealt      = data.totalDamageDealt;
            runTime               = data.runTime;
            cardRemovalUseCount   = data.cardRemovalUseCount;
            rareChanceBonus       = data.rareChanceBonus;
            startingDeckCodes     = data.deckCardCodes.ToArray();
            currentNodeType       = (NodeType)data.currentNodeType;
            currentEventCode      = data.currentEventCode;
            shopSeeds             = data.shopSeeds != null ? new List<int>(data.shopSeeds) : new List<int>();
            shopVisitCount        = data.shopVisitCount;

            // 구버전 세이브 호환 — 시드 풀이 비어 있으면 재생성
            if (shopSeeds.Count < ShopSeedPoolSize)
            {
                var prevState = UnityEngine.Random.state;
                UnityEngine.Random.InitState(runSeed);
                while (shopSeeds.Count < ShopSeedPoolSize)
                    shopSeeds.Add(UnityEngine.Random.Range(1, int.MaxValue));
                UnityEngine.Random.state = prevState;
            }

            // 유물 복원
            relics.Clear();
            foreach (int code in data.relicCodes)
            {
                var relic = DeckRoguelike.Relic.RelicRegistry.Create(code);
                if (relic != null) relics.Add(relic);
            }

            // 아이템 복원
            items.Clear();
            foreach (int code in data.itemCodes)
            {
                var item = DeckRoguelike.Item.ItemRegistry.GetItem(code);
                if (item != null) items.Add(item);
            }

            // 시드 RNG 상태를 현재 층까지 빠르게 감기
            // (encounterRng는 층마다 1회씩 소비되었을 것이므로 동일 횟수 소비)
            AdvanceSeededRngToFloor(data.currentFloor);

            Debug.Log($"[GameManager] 런 로드 완료 - 층: {currentFloor}, 시드: {runSeed}");
            OnHPChanged?.Invoke(currentHP, maxHP);
            OnGoldChanged?.Invoke(gold);
            OnFloorChanged?.Invoke(currentFloor);
            return true;
        }

        private void AdvanceSeededRngToFloor(int floor)
        {
            // 새 SeededRandom을 만들고 지나온 층 수만큼 소비하여 동기화
            seededRandom = new SeededRandom(runSeed);
            for (int i = 1; i < floor; i++)
                seededRandom.EncounterRange(100);
        }

        public static bool HasSavedRun() => RunSaveSystem.HasSave();

        /// <summary>현재 진입할 상점의 결정론 시드를 반환. 시드 풀을 다 소진했으면 fallback 시드를 즉석 생성.</summary>
        public int GetCurrentShopSeed()
        {
            if (shopSeeds == null || shopSeeds.Count == 0) return runSeed;
            int idx = Mathf.Clamp(shopVisitCount, 0, shopSeeds.Count - 1);
            return shopSeeds[idx];
        }

        /// <summary>상점에서 떠날 때 호출 — 다음 상점 방문이 다음 시드를 쓰도록 인덱스만 증가. 저장은 다음 노드 진입 시 함께 이뤄짐.</summary>
        public void IncrementShopVisit()
        {
            shopVisitCount++;
        }

        #endregion

        #region Player Stats

        public void ModifyGold(int amount)
        {
            gold = Mathf.Max(0, gold + amount);
            OnGoldChanged?.Invoke(gold);
        }

        public bool SpendGold(int amount)
        {
            if (gold >= amount)
            {
                gold -= amount;
                OnGoldChanged?.Invoke(gold);
                return true;
            }
            return false;
        }

        public void ModifyHP(int amount)
        {
            currentHP = Mathf.Clamp(currentHP + amount, 0, maxHP);
            OnHPChanged?.Invoke(currentHP, maxHP);

            if (currentHP <= 0)
            {
                if (TryRevive()) return;
                // 전투 중 사망은 BoardController가 패배 흐름으로 처리한다 — MapState 복귀 + 부활 카드(60008/60009).
                if (IsInCombat) return;
                EnterGameOver();
            }
        }

        public void TakeDamage(int damage)
        {
            ModifyHP(-damage);
        }

        /// <summary>319 유물(부활) 발동 가능 여부 — 런당 1회.</summary>
        private bool _reviveUsed;

        /// <summary>319 유물 부활 처리. HP가 0 이하로 떨어졌을 때 호출되어 true면 부활 처리됨.</summary>
        public bool TryRevive()
        {
            if (_reviveUsed) return false;
            if (!HasRelic(319)) return false;
            _reviveUsed = true;
            SetHP(Mathf.Max(1, maxHP / 2));
            Debug.Log("[GameManager] 319 유물 — 부활 (HP 50%)");
            return true;
        }

        public void Heal(int amount)
        {
            ModifyHP(amount);
            Debug.Log($"[GameManager] {amount} HP 회복 (현재: {currentHP}/{maxHP})");
        }

        public void ModifyMaxHP(int amount)
        {
            maxHP += amount;
            if (amount > 0)
            {
                currentHP += amount;
            }
            OnHPChanged?.Invoke(currentHP, maxHP);
        }

        public void FullHeal()
        {
            currentHP = maxHP;
            OnHPChanged?.Invoke(currentHP, maxHP);
        }

        #endregion

        #region Floor & Encounter Management

        public void SetFloor(int floor)
        {
            currentFloor = floor;
            
            // Act 계산 (15층마다)
            currentAct = ((floor - 1) / 15) + 1;
            
            OnFloorChanged?.Invoke(currentFloor);
        }

        public void AdvanceFloor()
        {
            currentFloor++;

            // Act 변경 체크 (15층마다)
            if (currentFloor > currentAct * 15)
            {
                currentAct++;
                actDifficulty = 1;
                battlesCompletedInAct = 0;
                Debug.Log($"[GameManager] Act {currentAct} 진입! 액트 난이도 초기화");
            }

            // 최종 보스 클리어 체크
            if (currentFloor > maxFloors)
            {
                EnterVictory();
                return;
            }

            OnFloorChanged?.Invoke(currentFloor);
            Debug.Log($"[GameManager] {currentFloor}층 도달");
            SaveRun();
        }

        /// <summary>
        /// 보스 클리어 후 60011(다음 스테이지) 카드 사용 시 호출 — Act를 +1 하고
        /// 액트 난이도/전투 카운터를 초기화한 뒤 새 액트의 첫 층으로 동기화합니다.
        /// 맵 재생성은 BoardController.AdvanceToNextStage가 담당합니다.
        /// </summary>
        public void AdvanceToNextAct()
        {
            currentAct++;
            actDifficulty = 1;
            battlesCompletedInAct = 0;
            currentFloor = (currentAct - 1) * 15 + 1; // SetFloor 공식과 일관되게 새 액트 첫 층
            Debug.Log($"[GameManager] 다음 스테이지 진입 — Act {currentAct}, {currentFloor}층");
            OnFloorChanged?.Invoke(currentFloor);
            SaveRun();
        }

        public EnemyEncounterData SelectEncounter()
        {
            return EncounterRegistry.SelectEncounter(gameDifficulty, currentAct, actDifficulty, seededRandom);
        }

        public void SetCurrentNodeType(NodeType nodeType)
        {
            currentNodeType = nodeType;
            Debug.Log($"[GameManager] 현재 노드: {nodeType}");
        }

        /// <summary>101/102 유물용 — 직전에 방문한 비전투 노드 종류를 기록합니다.</summary>
        public void MarkLastNodeShop()    { lastNodeWasShop = true;    lastNodeWasRest = false; lastNodeWasMystery = false; }
        public void MarkLastNodeRest()    { lastNodeWasRest = true;    lastNodeWasShop = false; lastNodeWasMystery = false; }
        public void MarkLastNodeMystery() { lastNodeWasMystery = true; lastNodeWasShop = false; lastNodeWasRest = false; }
        /// <summary>전투에 진입할 때 호출 — 직전 노드 플래그를 소비/리셋합니다.</summary>
        public void ClearLastNodeFlags() { lastNodeWasShop = false; lastNodeWasRest = false; lastNodeWasMystery = false; }

        public void OnCombatVictory()
        {
            AddEnemyDefeated();

            // 액트 난이도 갱신: 전투 2회마다 +1
            battlesCompletedInAct++;
            actDifficulty = (battlesCompletedInAct / 2) + 1;
            Debug.Log($"[GameManager] 액트 내 전투 {battlesCompletedInAct}회 → 액트 난이도 {actDifficulty}");

            // 전투 보상 계산
            int goldReward = CalculateGoldReward();
            ModifyGold(goldReward);

            Debug.Log($"[GameManager] 전투 승리! +{goldReward} 골드");

            // Map으로 복귀
            EnterPlaying();
            SceneLoader.Instance?.LoadScene("Map");
        }

        public void OnCombatDefeat()
        {
            Debug.Log("[GameManager] 전투 패배!");
            EnterGameOver();
        }

        private int CalculateGoldReward()
        {
            return IsDangerCombatEncounter
                ? UnityEngine.Random.Range(30, 41)
                : UnityEngine.Random.Range(10, 21);
        }

        #endregion

        #region Relic Management

        /// <summary>지정 코드의 유물을 보유 중인지 검사합니다.</summary>
        public bool HasRelic(int relicCode)
        {
            foreach (var r in relics)
                if (r?.Data?.relicCode == relicCode) return true;
            return false;
        }

        /// <summary>
        /// 970/980/990 보스 직업 유물은 각각 70/80/90 시작 유물의 업그레이드 버전입니다.
        /// 베이스 유물을 보유 중이면 동일 슬롯에서 교체하기 위해 매핑을 반환합니다.
        /// </summary>
        private static int GetBaseRelicForUpgrade(int upgradeCode)
        {
            switch (upgradeCode)
            {
                case 970: return 70;
                case 980: return 80;
                case 990: return 90;
                default:  return 0;
            }
        }

        /// <summary>
        /// 유물을 획득합니다. OnRelicObtained 훅을 1회 호출합니다.
        /// 970/980/990을 획득할 때 베이스(70/80/90)를 보유 중이라면 인플레이스로 교체합니다.
        /// </summary>
        public void AddRelic(int relicCode)
        {
            int baseCode = GetBaseRelicForUpgrade(relicCode);
            if (baseCode != 0)
            {
                int baseIndex = relics.FindIndex(r => r?.Data?.relicCode == baseCode);
                if (baseIndex >= 0)
                {
                    var upgraded = RelicRegistry.Create(relicCode);
                    if (upgraded == null) return;

                    var old = relics[baseIndex];
                    relics[baseIndex] = upgraded;
                    upgraded.OnRelicObtained(this);
                    OnRelicReplaced?.Invoke(old, upgraded);
                    Debug.Log($"[GameManager] 유물 업그레이드 교체: {old?.Data?.relicName}({baseCode}) → {upgraded.Data.relicName}({relicCode})");
                    return;
                }
            }

            if (relics.Exists(r => r?.Data?.relicCode == relicCode))
            {
                Debug.LogWarning($"[GameManager] 이미 보유 중인 유물 (code {relicCode}) — 중복 추가 무시됨");
                return;
            }

            var relic = RelicRegistry.Create(relicCode);
            if (relic == null) return;

            relics.Add(relic);
            relic.OnRelicObtained(this);
            OnRelicAdded?.Invoke(relic);
            DiscoveryManager.DiscoverRelic(relic.Data.relicCode);
            Debug.Log($"[GameManager] 유물 획득: {relic.Data.relicName}");
        }

        /// <summary>
        /// 유물을 인벤토리에서 제거합니다. OnRelicRemoved 훅을 호출해 UI 아이콘을 정리합니다.
        /// 207/208/209 각인된 부적의 픽 취소 흐름에서 사용 — 보상 카드 복구와 짝지어 호출.
        /// </summary>
        public bool RemoveRelic(int relicCode)
        {
            int idx = relics.FindIndex(r => r?.Data?.relicCode == relicCode);
            if (idx < 0) return false;

            var removed = relics[idx];
            relics.RemoveAt(idx);
            OnRelicRemoved?.Invoke(removed);
            Debug.Log($"[GameManager] 유물 제거: {removed.Data?.relicName} ({relicCode})");
            return true;
        }

        /// <summary>디버그용: UI 이벤트만 발생시켜 유물 아이콘을 추가합니다.</summary>
        public void DebugAddRelicIcon(RelicEffect relic)
        {
            OnRelicAdded?.Invoke(relic);
        }

        /// <summary>아이템을 인벤토리에 추가합니다.</summary>
        public void AddItem(ItemData item)
        {
            if (item == null) return;
            items.Add(item);
            OnItemAdded?.Invoke(item);
            DiscoveryManager.DiscoverItem(item.itemCode);
            Debug.Log($"[GameManager] 아이템 획득: {item.itemName}");
        }

        /// <summary>아이템을 인벤토리에서 제거합니다 (소생의 팬던트 등 패시브 소모용).</summary>
        public void RemoveItem(ItemData item)
        {
            if (item == null) return;
            items.Remove(item);
            OnItemRemoved?.Invoke(item);
            Debug.Log($"[GameManager] 아이템 소모: {item.itemName}");
        }

        /// <summary>HP를 지정값으로 강제 설정합니다. 게임오버 판정을 우회합니다 (소생의 팬던트 전용).</summary>
        public void SetHP(int value)
        {
            currentHP = Mathf.Clamp(value, 1, maxHP);
            OnHPChanged?.Invoke(currentHP, maxHP);
        }

        /// <summary>
        /// 아이템을 사용합니다. 성공 시 인벤토리에서 제거되고 true를 반환합니다.
        /// combat이 null이면 전투 외부에서 사용한 것으로 처리됩니다.
        /// CanUse() 조건을 충족하지 못하면 false를 반환하고 아이템을 소모하지 않습니다.
        /// </summary>
        public bool UseItem(ItemData itemData, DeckRoguelike.UI.BoardController board = null)
        {
            if (itemData == null) return false;
            if (!items.Contains(itemData))
            {
                Debug.LogWarning($"[GameManager] 인벤토리에 없는 아이템 사용 시도: {itemData.itemName}");
                return false;
            }

            // 905 유물 — 더 이상 포션의 효과를 사용할 수 없습니다
            if (HasRelic(905))
            {
                Debug.Log("[GameManager] 905 유물 — 아이템 사용 차단");
                return false;
            }

            var effect = DeckRoguelike.Item.ItemEffectRegistry.Create(itemData.itemCode);
            if (effect == null) return false;

            var ctx = new DeckRoguelike.Item.ItemUseContext { GM = this, Board = board };

            if (!effect.CanUse(ctx))
            {
                Debug.Log($"[GameManager] {itemData.itemName}: 현재 사용 불가 (전투 중에만 사용 가능)");
                return false;
            }

            // 904 유물 — 포션의 효과가 두 배 (논타겟팅 아이템에 한해 효과를 두 번 적용)
            bool doubleEffect = HasRelic(904) && !effect.NeedsTargetingMode;

            if (effect.NeedsTargetingMode)
            {
                // 타겟팅 아이템: 확정 시점에 소모 (ctx.OnConfirmedUse로 처리)
                ctx.OnConfirmedUse = () =>
                {
                    RemoveItem(itemData);
                    Debug.Log($"[GameManager] 아이템 소모(타겟 확정): {itemData.itemName}");
                };
                effect.OnItemUsed(ctx);
                Debug.Log($"[GameManager] 아이템 타겟팅 시작: {itemData.itemName}");
            }
            else
            {
                // 305 무지개 포션처럼 OnItemUsed가 새 아이템을 추가하는 경우, 본 아이템을 먼저 슬롯에서
                // 비워야 새 아이템이 올바른 슬롯에 들어간다. RemoveItem이 OnItemRemoved 이벤트를 발화한다.
                RemoveItem(itemData);
                effect.OnItemUsed(ctx);
                if (doubleEffect)
                {
                    // 907 유물 — 효과 한 번 더 적용 (소모는 1회)
                    var ctx2 = new DeckRoguelike.Item.ItemUseContext { GM = this, Board = board };
                    effect.OnItemUsed(ctx2);
                    Debug.Log($"[GameManager] 907 유물 — {itemData.itemName} 효과 2회 적용");
                }
                Debug.Log($"[GameManager] 아이템 사용: {itemData.itemName} (소모됨)");
            }
            return true;
        }

        /// <summary>
        /// 유물 보상 후보 목록을 생성합니다.
        ///   isBoss = true  → Boss 희귀도 유물 1개
        ///   isBoss = false → Uncommon/Rare 유물 중 count개 (중복 제외)
        /// </summary>
        public List<RelicData> GenerateRelicReward(bool isBoss = false, int count = 3)
        {
            if (isBoss)
            {
                var bossPool = RelicRegistry.GetByType(bossRelic: true);
                bossPool.RemoveAll(r => !r.IsForCharacter(selectedCharacter));
                bossPool.RemoveAll(r => relics.Exists(owned => owned.Data.relicCode == r.relicCode));
                return PickRelicsUnique(bossPool, 1);
            }

            // 일반 유물 보상: Act에 따라 Uncommon/Rare 가중치를 다르게 적용
            //   Act1: 60/40  Act2: 30/70  Act3: 10/90
            var pool = RelicRegistry.GetByType(bossRelic: false);
            pool.RemoveAll(r => r.IsShopOnlyRelic);   // 4xx 상점 전용 제외
            pool.RemoveAll(r => r.IsStartingRelic);   // 70/80/90 제외
            pool.RemoveAll(r => !r.IsForCharacter(selectedCharacter));
            pool.RemoveAll(r => relics.Exists(owned => owned.Data.relicCode == r.relicCode));

            // 등급별로 분리 (RelicData.RarityIndex: 1xx=Uncommon(1), 2xx/3xx=Rare(2))
            var byRarity = new Dictionary<int, List<RelicData>>
            {
                { 1, new List<RelicData>() }, // Uncommon (1xx)
                { 2, new List<RelicData>() }, // Rare     (2xx, 3xx)
            };
            foreach (var r in pool)
            {
                int idx = r.RarityIndex == 1 ? 1 : 2;
                byRarity[idx].Add(r);
            }

            // Act별 가중치 (act는 1부터 시작)
            (float u, float ra) weights = currentAct switch
            {
                <= 1 => (60f, 40f),
                2    => (30f, 70f),
                _    => (10f, 90f),
            };

            var result = new List<RelicData>();
            for (int i = 0; i < count; i++)
            {
                int rarityIdx = RollRelicRarity(weights.u, weights.ra);
                var picked = PickFromRarityWithFallback(byRarity, rarityIdx, result);
                if (picked != null) result.Add(picked);
            }
            return result;
        }

        /// <summary>가중치(uncommon/rare)를 받아 1(Uncommon) 또는 2(Rare) 등급 인덱스를 반환.</summary>
        private static int RollRelicRarity(float uncommonW, float rareW)
        {
            float total = uncommonW + rareW;
            if (total <= 0f) return 1;
            float roll = UnityEngine.Random.Range(0f, total);
            return roll < uncommonW ? 1 : 2;
        }

        /// <summary>지정 등급 풀이 비어있으면 다른 등급에서 폴백 선택. exclude는 결과에 이미 포함된 유물.</summary>
        private static RelicData PickFromRarityWithFallback(
            Dictionary<int, List<RelicData>> byRarity, int preferred, List<RelicData> exclude)
        {
            // 우선 preferred → 반대 등급 순서로 시도
            int[] order = preferred == 1 ? new[] { 1, 2 } : new[] { 2, 1 };
            foreach (int idx in order)
            {
                var available = byRarity[idx].FindAll(r => !exclude.Contains(r));
                if (available.Count == 0) continue;
                return available[UnityEngine.Random.Range(0, available.Count)];
            }
            return null;
        }

        private List<RelicData> PickRelicsUnique(List<RelicData> pool, int count)
        {
            var shuffled = new List<RelicData>(pool);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            var result = new List<RelicData>();
            for (int i = 0; i < Mathf.Min(count, shuffled.Count); i++)
                result.Add(shuffled[i]);
            return result;
        }

        #endregion

        #region Card Rewards

        /// <summary>
        /// 카드 보상 한 배치(3장 모두 같은 등급)의 등급을 결정합니다.
        ///   • 보스: 항상 Rare 고정
        ///   • 일반: 60-bonus / 37 / 3+bonus 확률 — Rare가 나오면 bonus 0으로 리셋, 그 외엔 bonus += 3
        /// 호출 시 rareChanceBonus가 갱신되므로 한 배치당 한 번만 호출하세요.
        /// </summary>
        public DeckRoguelike.Cards.CardRarity RollCardRewardRarity(bool isBoss)
        {
            if (isBoss)
            {
                rareChanceBonus = 0; // 보스 보상 후에도 누적 리셋
                return DeckRoguelike.Cards.CardRarity.Rare;
            }

            float bonus = Mathf.Clamp(rareChanceBonus, 0, 57); // 일반 확률 0%까지만
            float rarePct = 3f + bonus;
            // uncommon = 37% 고정, common = 60% - bonus

            float roll = seededRandom != null ? seededRandom.CardRewardRoll() : UnityEngine.Random.Range(0f, 100f);
            DeckRoguelike.Cards.CardRarity rarity;
            if (roll < rarePct)
                rarity = DeckRoguelike.Cards.CardRarity.Rare;
            else if (roll < rarePct + 37f)
                rarity = DeckRoguelike.Cards.CardRarity.Uncommon;
            else
                rarity = DeckRoguelike.Cards.CardRarity.Common;

            if (rarity == DeckRoguelike.Cards.CardRarity.Rare)
                rareChanceBonus = 0;
            else
                rareChanceBonus += 3;

            Debug.Log($"[GameManager] 카드 보상 등급: {rarity} (rare {rarePct:0}% / unc 37% / common {Mathf.Max(60f - bonus, 0f):0}%) → 다음 bonus={rareChanceBonus}");
            return rarity;
        }

        #endregion

        #region Card Removal

        /// <summary>상점에서 카드 제거 1회 사용. 이후 CardRemovalPrice가 25G 증가합니다.</summary>
        public void UseCardRemoval()
        {
            cardRemovalUseCount++;
            Debug.Log($"[GameManager] 카드 제거 사용 (다음 가격: {CardRemovalPrice}G)");
        }

        #endregion

        #region Statistics

        public void AddEnemyDefeated()
        {
            enemiesDefeated++;
        }

        public void AddCardPlayed()
        {
            cardsPlayed++;
        }

        public void AddDamageDealt(int damage)
        {
            totalDamageDealt += damage;
        }

        public RunStatistics GetRunStatistics()
        {
            return new RunStatistics
            {
                Character = selectedCharacter,
                FloorsCleared = currentFloor - 1,
                EnemiesDefeated = enemiesDefeated,
                CardsPlayed = cardsPlayed,
                TotalDamageDealt = totalDamageDealt,
                GoldEarned = gold,
                RunTime = runTime
            };
        }

        #endregion
    }

    #region Enums and Data Classes

    public enum CharacterType
    {
        Warrior,    // 전사 - 높은 HP, 액션 카드 특화
        Mage,       // 마법사 - 파워 카드 특화
        Gunner      // 거너 - 원거리 공격, 범위 특화
    }

    [Serializable]
    public class RunStatistics
    {
        public CharacterType Character;
        public int FloorsCleared;
        public int EnemiesDefeated;
        public int CardsPlayed;
        public int TotalDamageDealt;
        public int GoldEarned;
        public float RunTime;
    }

    #endregion
}
