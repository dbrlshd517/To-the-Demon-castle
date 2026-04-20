using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using DeckRoguelike.Combat;
using DeckRoguelike.Relic;
using DeckRoguelike.Item;

namespace DeckRoguelike.Core
{
    /// <summary>
    /// 게임 전체 상태를 관리하는 싱글톤 매니저
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game State")]
        [SerializeField] private GameState currentState = GameState.MainMenu;
        [SerializeField] private int currentFloor = 1;
        [SerializeField] private int maxFloors = 50;
        [SerializeField] private int currentAct = 1;

        [Header("Character Data")]

        [Header("Player Run Data")]
        [SerializeField] private CharacterType selectedCharacter;
        [SerializeField] private int gold = 99;
        [SerializeField] private int currentHP;
        [SerializeField] private int maxHP;
        [SerializeField] private int baseEnergy = 3;
        [SerializeField] private int runSeed;
        private int[] startingDeckCodes;

        [Header("Game Difficulty")]
        [Tooltip("런 시작 시 결정. 1=easy 2=normal 3=hard 4=hell")]
        [SerializeField] private int gameDifficulty = 1;

        [Header("Current Encounter")]
        [SerializeField] private bool isEliteEncounter;
        [SerializeField] private bool isBossEncounter;

        [Header("Statistics")]
        [SerializeField] private int enemiesDefeated = 0;
        [SerializeField] private int cardsPlayed = 0;
        [SerializeField] private int totalDamageDealt = 0;
        [SerializeField] private float runTime = 0f;

        // Events
        public event Action<GameState> OnGameStateChanged;
        public event Action<int> OnFloorChanged;
        public event Action<int> OnGoldChanged;
        public event Action<int, int> OnHPChanged;
        public event Action OnRunStarted;
        public event Action<bool> OnRunEnded; // true = victory
        public event Action<RelicEffect> OnRelicAdded;
        public event Action<ItemData>   OnItemAdded;

        // 액트 난이도 (전투 2회마다 +1, 액트 전환 시 초기화)
        private int actDifficulty = 1;
        private int battlesCompletedInAct = 0;

        // 카드 보상 희귀도 확률 (런 내 누적, 일반카드 보상 때마다 희귀 +1%)
        private int rareChanceBonus = 0;  // 0 ~ 60 (일반 확률 한계)

        // 상점 카드 제거 가격 (첫 이용 75G, 이후 25G씩 증가)
        private const int CardRemovalBasePrice = 75;
        private const int CardRemovalPriceStep = 25;
        private int cardRemovalUseCount = 0;

        // 유물
        private readonly List<RelicEffect> relics = new List<RelicEffect>();

        // 아이템
        private readonly List<ItemData> items = new List<ItemData>();

        // Properties
        public GameState CurrentState => currentState;
        public int CurrentFloor => currentFloor;
        public int CurrentAct => currentAct;
        public int GameDifficulty => gameDifficulty;
        public int ActDifficulty => actDifficulty;
        public CharacterType SelectedCharacter => selectedCharacter;
        public int Gold => gold;
        public int CurrentHP => currentHP;
        public int MaxHP => maxHP;
        public int BaseEnergy => baseEnergy;
        public int RunSeed => runSeed;
        public int[] StartingDeckCodes => startingDeckCodes;
        public bool IsEliteEncounter => isEliteEncounter;
        public bool IsBossEncounter => isBossEncounter;
        public int RareChanceBonus    => rareChanceBonus;
        public int CardRemovalPrice   => CardRemovalBasePrice + cardRemovalUseCount * CardRemovalPriceStep;
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
            if (currentState == GameState.InGame || 
                currentState == GameState.Combat || 
                currentState == GameState.Map)
            {
                runTime += Time.deltaTime;
            }
        }

        #region Game State Management

        public void ChangeState(GameState newState)
        {
            if (currentState == newState) return;

            GameState previousState = currentState;
            currentState = newState;

            Debug.Log($"[GameManager] 상태 변경: {previousState} -> {newState}");
            OnGameStateChanged?.Invoke(newState);

            HandleStateTransition(previousState, newState);
        }

        private void HandleStateTransition(GameState from, GameState to)
        {
            switch (to)
            {
                case GameState.MainMenu:
                    Time.timeScale = 1f;
                    break;
                case GameState.Map:
                    Time.timeScale = 1f;
                    break;
                case GameState.Combat:
                    Time.timeScale = 1f;
                    break;
                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;
                case GameState.GameOver:
                    OnRunEnded?.Invoke(false);
                    break;
                case GameState.Victory:
                    OnRunEnded?.Invoke(true);
                    break;
            }
        }

        #endregion

        #region Run Management

        public void StartNewRun(CharacterType character, int startMaxHP = 75, int startGold = 99, int startBaseEnergy = 3, int[] deckCodes = null)
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
            isEliteEncounter = false;
            isBossEncounter = false;
            rareChanceBonus    = 0;
            cardRemovalUseCount = 0;

            relics.Clear();
            items.Clear();

            // 랜덤 시드 생성
            runSeed = UnityEngine.Random.Range(0, 99999);

            maxHP             = startMaxHP;
            gold              = startGold;
            baseEnergy        = startBaseEnergy;
            startingDeckCodes = deckCodes;
            currentHP         = maxHP;

            Debug.Log($"[GameManager] 새 런 시작 - 캐릭터: {character}, 시드: {runSeed}");
            OnRunStarted?.Invoke();
            OnHPChanged?.Invoke(currentHP, maxHP);
            OnGoldChanged?.Invoke(gold);
        }

        public void AbandonRun()
        {
            ChangeState(GameState.MainMenu);
            SceneLoader.Instance?.LoadScene("MainMenu");
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
                ChangeState(GameState.GameOver);
            }
        }

        public void TakeDamage(int damage)
        {
            ModifyHP(-damage);
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

        /// <summary>런 내 기본 에너지를 영구적으로 수정합니다 (영혼이 담긴 구슬 등).</summary>
        public void ModifyBaseEnergy(int amount)
        {
            baseEnergy += amount;
            Debug.Log($"[GameManager] 기본 에너지 변경: {baseEnergy}");
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
                ChangeState(GameState.Victory);
                return;
            }

            OnFloorChanged?.Invoke(currentFloor);
            Debug.Log($"[GameManager] {currentFloor}층 도달");
        }

        public EnemyEncounterData SelectEncounter()
        {
            return EncounterRegistry.SelectEncounter(gameDifficulty, currentAct, actDifficulty);
        }

        public void SetCurrentEncounterElite(bool isElite)
        {
            isEliteEncounter = isElite;
            isBossEncounter = false;
            Debug.Log($"[GameManager] 엘리트 전투: {isElite}");
        }

        public void SetCurrentEncounterBoss(bool isBoss)
        {
            isBossEncounter = isBoss;
            isEliteEncounter = false;
            Debug.Log($"[GameManager] 보스 전투: {isBoss}");
        }

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
            ChangeState(GameState.Map);
            SceneLoader.Instance?.LoadScene("Map");
        }
        
        public void OnCombatDefeat()
        {
            Debug.Log("[GameManager] 전투 패배!");
            ChangeState(GameState.GameOver);
        }

        private int CalculateGoldReward()
        {
            return gameDifficulty switch
            {
                1 => UnityEngine.Random.Range(10, 21),
                2 => UnityEngine.Random.Range(20, 31),
                3 => UnityEngine.Random.Range(40, 51),
                _ => 0
            };
        }

        #endregion

        #region Relic Management

        /// <summary>
        /// 유물을 획득합니다. OnRelicObtained 훅을 1회 호출합니다.
        /// </summary>
        public void AddRelic(int relicCode)
        {
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
            Debug.Log($"[GameManager] 유물 획득: {relic.Data.relicName}");
        }

        /// <summary>아이템을 인벤토리에 추가합니다.</summary>
        public void AddItem(ItemData item)
        {
            if (item == null) return;
            items.Add(item);
            OnItemAdded?.Invoke(item);
            Debug.Log($"[GameManager] 아이템 획득: {item.itemName}");
        }

        /// <summary>아이템을 인벤토리에서 제거합니다 (소생의 팬던트 등 패시브 소모용).</summary>
        public void RemoveItem(ItemData item)
        {
            if (item == null) return;
            items.Remove(item);
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
        public bool UseItem(ItemData itemData, DeckRoguelike.UI.CombatController combat = null)
        {
            if (itemData == null) return false;
            if (!items.Contains(itemData))
            {
                Debug.LogWarning($"[GameManager] 인벤토리에 없는 아이템 사용 시도: {itemData.itemName}");
                return false;
            }

            var effect = DeckRoguelike.Item.ItemEffectRegistry.Create(itemData.itemCode);
            if (effect == null) return false;

            var ctx = new DeckRoguelike.Item.ItemUseContext { GM = this, Combat = combat };

            if (!effect.CanUse(ctx))
            {
                Debug.Log($"[GameManager] {itemData.itemName}: 현재 사용 불가 (전투 중에만 사용 가능)");
                return false;
            }

            if (effect.NeedsTargetingMode)
            {
                // 타겟팅 아이템: 확정 시점에 소모 (ctx.OnConfirmedUse로 처리)
                ctx.OnConfirmedUse = () =>
                {
                    items.Remove(itemData);
                    Debug.Log($"[GameManager] 아이템 소모(타겟 확정): {itemData.itemName}");
                };
                effect.OnItemUsed(ctx);
                Debug.Log($"[GameManager] 아이템 타겟팅 시작: {itemData.itemName}");
            }
            else
            {
                items.Remove(itemData);
                effect.OnItemUsed(ctx);
                Debug.Log($"[GameManager] 아이템 사용: {itemData.itemName} (소모됨)");
            }
            return true;
        }

        /// <summary>
        /// 유물 보상 후보 목록을 생성합니다.
        ///   isBoss = true  → Boss 희귀도 유물 1개
        ///   isBoss = false → Common/Uncommon 유물 중 count개 (중복 제외)
        /// </summary>
        public List<RelicData> GenerateRelicReward(bool isBoss = false, int count = 3)
        {
            List<RelicData> pool;

            if (isBoss)
            {
                pool = RelicRegistry.GetByType(bossRelic: true);
                count = 1;
            }
            else
            {
                pool = RelicRegistry.GetByType(bossRelic: false);
            }

            // 이미 보유 중인 유물 제외
            pool.RemoveAll(r => relics.Exists(owned => owned.Data.relicCode == r.relicCode));

            return PickRelicsUnique(pool, count);
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
        /// 전투 후 카드 보상 3장 생성.
        /// 보스: 희귀 카드 3장 고정.
        /// 일반: 일반(60%-bonus%) / 고급(40%) / 희귀(bonus%) 확률로 각 슬롯 결정.
        /// 일반 카드가 보상에 포함될 때마다 rareChanceBonus +1%.
        /// </summary>
        public List<DeckRoguelike.Cards.CardData> GenerateCardRewards()
        {
            var pool   = CardRegistry.GetRewardPool(selectedCharacter);
            var result = new List<DeckRoguelike.Cards.CardData>();

            if (isBossEncounter)
            {
                // 보스: 희귀 카드 3장
                var rarePool = pool.FindAll(c => c.Rarity == DeckRoguelike.Cards.CardRarity.Rare);
                result = PickUnique(rarePool, 3);
                result = result.ConvertAll(c => TryUpgrade(c));
            }
            else
            {
                // 일반 전투: 슬롯별 희귀도 결정
                var commonPool   = pool.FindAll(c => c.Rarity == DeckRoguelike.Cards.CardRarity.Common);
                var uncommonPool = pool.FindAll(c => c.Rarity == DeckRoguelike.Cards.CardRarity.Uncommon);
                var rarePool     = pool.FindAll(c => c.Rarity == DeckRoguelike.Cards.CardRarity.Rare);

                int commonCount = 0;
                for (int i = 0; i < 3; i++)
                {
                    float roll    = UnityEngine.Random.Range(0f, 100f);
                    float rarePct = Mathf.Min(rareChanceBonus, 60f);         // 최대 60%
                    float commonPct = Mathf.Max(60f - rarePct, 0f);          // 최소 0%
                    // uncommon은 나머지 40% 고정

                    DeckRoguelike.Cards.CardData picked = null;
                    if (roll < rarePct && rarePool.Count > 0)
                        picked = PickUniqueOne(rarePool, result);
                    else if (roll < rarePct + 40f && uncommonPool.Count > 0)
                        picked = PickUniqueOne(uncommonPool, result);
                    else if (commonPool.Count > 0)
                    {
                        picked = PickUniqueOne(commonPool, result);
                        commonCount++;
                    }

                    if (picked != null) result.Add(TryUpgrade(picked));
                }

                // 일반 카드가 나온 수만큼 희귀 확률 누적
                rareChanceBonus += commonCount;
                Debug.Log($"[GameManager] 카드 보상 생성 | 일반 {commonCount}장 → rareBonus={rareChanceBonus}%");
            }

            return result;
        }

        /// <summary>
        /// 액트마다 20%씩 업그레이드된 버전으로 교체.
        /// Act1=0% Act2=20% Act3=40% ...
        /// </summary>
        private DeckRoguelike.Cards.CardData TryUpgrade(DeckRoguelike.Cards.CardData card)
        {
            float upgradeChance = (currentAct - 1) * 0.2f;
            if (upgradeChance <= 0f) return card;
            if (UnityEngine.Random.value > upgradeChance) return card;

            int upgradedCode = card.UpgradedCode;
            var upgraded = CardRegistry.GetCard(upgradedCode);
            return upgraded != null ? upgraded : card;
        }

        private List<DeckRoguelike.Cards.CardData> PickUnique(List<DeckRoguelike.Cards.CardData> pool, int count)
        {
            var shuffled = new List<DeckRoguelike.Cards.CardData>(pool);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            var result = new List<DeckRoguelike.Cards.CardData>();
            for (int i = 0; i < Mathf.Min(count, shuffled.Count); i++)
                result.Add(shuffled[i]);
            return result;
        }

        private DeckRoguelike.Cards.CardData PickUniqueOne(
            List<DeckRoguelike.Cards.CardData> pool,
            List<DeckRoguelike.Cards.CardData> exclude)
        {
            var available = pool.FindAll(c => !exclude.Contains(c));
            if (available.Count == 0) return null;
            return available[UnityEngine.Random.Range(0, available.Count)];
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

    public enum GameState
    {
        MainMenu,
        CharacterSelect,
        Map,
        InGame,
        Combat,
        Shop,
        Rest,
        Event,
        Reward,
        Paused,
        Settings,
        GameOver,
        Victory
    }

    public enum CharacterType
    {
        Warrior,    // 전사 - 높은 HP, 공격 카드 특화
        Mage,       // 마법사 - 스킬/파워 카드 특화
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
