using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DeckRoguelike.Cards;
using DeckRoguelike.Core;
using DeckRoguelike.Relic;
using DeckRoguelike.Item;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 메인 메뉴 화면 UI 컨트롤러
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject characterSelectPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject LeaderboardPanel;
        [Tooltip("도감 진입 패널 — cardList/relicList/potionList 3개 진입 버튼을 가진 컨테이너")]
        [SerializeField] private GameObject dictionaryPanel;

        [Header("Dictionary Sub-Panels")]
        [Tooltip("도감 패널의 카드 도감 진입 버튼")]
        [SerializeField] private Button openCardListButton;
        [Tooltip("도감 패널의 유물 도감 진입 버튼")]
        [SerializeField] private Button openRelicListButton;
        [Tooltip("도감 패널의 포션(아이템) 도감 진입 버튼")]
        [SerializeField] private Button openPotionListButton;
        [SerializeField] private GameObject cardListPanel;
        [SerializeField] private GameObject relicListPanel;
        [SerializeField] private GameObject potionListPanel;

        [Header("Dictionary Detail Panel")]
        [Tooltip("유물 도감에서 슬롯 클릭 시 열리는 RelicInfoPanelController. " +
                 "씬 시작 시 강제 비활성화되며 RelicListPanel에서 이 패널을 조회한다.")]
        [SerializeField] private DeckRoguelike.UI.RelicInfoController relicInfo;
        [Tooltip("카드 도감/덱 보기에서 카드 클릭 시 열리는 CardInfoController. " +
                 "씬 시작 시 강제 비활성화되며 CardListController가 Instance.ShowCardInfo로 호출한다.")]
        [SerializeField] private DeckRoguelike.UI.CardInfoController cardInfo;

        // RelicListPanel / CardListController가 클릭 시 조회할 수 있도록 정적 접근자 제공.
        private static MainMenuController _instance;
        public static MainMenuController Instance => _instance;
        public static DeckRoguelike.UI.RelicInfoController GetRelicInfoPanel()
            => _instance != null ? _instance.relicInfo : null;

        [Header("Main Menu Buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button rankingButton;
        [SerializeField] private Button dictionaryButton;
        [SerializeField] private Button quitButton;

        [Header("Character Select")]
        [SerializeField] private Button[] characterButtons;
        [SerializeField] private TextMeshProUGUI characterNameText;
        [SerializeField] private TextMeshProUGUI characterDescText;
        [SerializeField] private TextMeshProUGUI characterStatsText;
        [SerializeField] private Image characterPortrait;
        [SerializeField] private Button startRunButton;
        [SerializeField] private Button backtomainmenuButton;

        [Header("Starting Item Override")]
        [Tooltip("캐릭터 선택 화면에서 직접 시작 아이템 코드를 입력하는 InputField (선택 사항)")]
        [SerializeField] private TMP_InputField startingItemCodeInputField;
        [Tooltip("입력된 아이템 코드 미리보기 텍스트")]
        [SerializeField] private TextMeshProUGUI itemPreviewText;

        [Header("Character Data")]
        [SerializeField] private CharacterData[] characters;

        [Header("Settings")]
        [SerializeField] private SettingsMenuController settingsMenuController;

        [Header("Animation")]
        [SerializeField] private float panelFadeDuration = 0.3f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Audio")]
        [SerializeField] private AudioClip buttonClickSound;
        [SerializeField] private AudioClip buttonHoverSound;
        [SerializeField] private AudioClip menuMusic;

        private CharacterType selectedCharacter;
        private AudioSource audioSource;
        private CanvasGroup currentPanelCanvasGroup;
        private int? overrideItemCode = null;

        // ── 패널 스택 (InGameUIController 패턴과 동일) ─────────────
        // mainMenuPanel은 스택에 들어가지 않는 '루트' 패널. 스택이 비면 자동으로 보이고,
        // 무엇이든 push되면 자동으로 숨겨진다. InfoPanel(relicInfoPanel 등)은 스택 미사용.
        private struct PanelEntry { public string id; public System.Action close; }
        private readonly System.Collections.Generic.LinkedList<PanelEntry> panelStack
            = new System.Collections.Generic.LinkedList<PanelEntry>();
        private const int MaxPanelStack = 5;

        private void Awake()
        {
            _instance = this;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            // RelicInfoPanel/CardInfoPanel이 씬 시작 시 활성 상태로 남아있으면 강제 비활성화.
            // (편집 중 켜둔 채 저장된 경우 대비 — Awake가 일찍 호출되어야 함)
            if (relicInfo != null && relicInfo.gameObject.activeSelf)
                relicInfo.gameObject.SetActive(false);
            if (cardInfo != null && cardInfo.gameObject.activeSelf)
                cardInfo.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            InitializeUI();
            SetupButtonListeners();
            ApplyButtonLocalization();
            PreWarmDictionaryPanels();
            // 사전 인스턴스화로 인해 자식 RelicInfoPanel/CardInfoPanel이 켜졌을 가능성이 있으므로 한 번 더 확실히 끔.
            if (relicInfo != null) relicInfo.gameObject.SetActive(false);
            if (cardInfo != null) cardInfo.gameObject.SetActive(false);
            if (settingsMenuController != null)
                // Settings 패널이 자체적으로 닫히면 스택에서 제거(close 액션 재실행 X).
                settingsMenuController.OnClosed += () => UnregisterPanel("Settings");
            ShowMainMenu();
            PlayBackgroundMusic();
        }

        /// <summary>도감 하위 패널들(카드/유물/포션)을 씬 시작 시 미리 한 번 활성화→비활성화하여
        /// Addressables 카드/스프라이트 로드와 슬롯 인스턴스화가 사전에 수행되도록 한다.
        /// 첫 클릭 시 발생하던 동기 로드 렉을 제거. 각 패널의 OnEnable이 한 번 발화하여 빌드 후
        /// _built 플래그로 다음 활성화부터는 즉시 표시된다.</summary>
        private void PreWarmDictionaryPanels()
        {
            PreWarmPanel(cardListPanel);
            PreWarmPanel(relicListPanel);
            PreWarmPanel(potionListPanel);
        }

        private static void PreWarmPanel(GameObject panel)
        {
            if (panel == null) return;
            bool wasActive = panel.activeSelf;
            if (!wasActive) panel.SetActive(true);
            // 활성화로 Awake + OnEnable이 즉시 실행되어 그리드 빌드가 끝난다.
            // 빌드된 슬롯은 OnDisable에서 파괴하지 않으므로 보존된다.
            if (!wasActive) panel.SetActive(false);
        }

        #region Panel Stack API

        /// <summary>패널을 스택에 등록하고 활성화. 같은 id가 이미 있으면 기존 항목 제거 후 맨 위에 재등록.
        /// closeAction은 해당 패널이 pop될 때 실행될 정리 함수(주로 SetActive(false)).</summary>
        public void PushPanel(string panelId, System.Action closeAction)
        {
            RemoveFromStack(panelId);
            if (panelStack.Count >= MaxPanelStack)
            {
                var oldest = panelStack.First;
                panelStack.RemoveFirst();
                oldest.Value.close?.Invoke();
            }
            panelStack.AddLast(new PanelEntry { id = panelId, close = closeAction });
            UpdateBackButton();
            UpdateMainMenuVisibility();
        }

        /// <summary>스택에서 패널을 제거 (close 액션은 실행하지 않음).
        /// 외부 컴포넌트가 자체적으로 닫힐 때 스택을 동기화하기 위해 사용.</summary>
        public void UnregisterPanel(string panelId)
        {
            RemoveFromStack(panelId);
            UpdateBackButton();
            UpdateMainMenuVisibility();
        }

        /// <summary>스택의 모든 패널을 pop하고 close 액션 실행. 메인 메뉴로 돌아갈 때 사용.</summary>
        public void ClearStack()
        {
            while (panelStack.Count > 0)
            {
                var top = panelStack.Last.Value;
                panelStack.RemoveLast();
                top.close?.Invoke();
            }
            UpdateBackButton();
            UpdateMainMenuVisibility();
        }

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

        /// <summary>backButton 클릭 — 스택 최상단 패널을 닫음. 빈 스택이면 무시.</summary>
        public void OnBackClicked()
        {
            PlayButtonSound();
            if (panelStack.Count == 0) return;
            var top = panelStack.Last.Value;
            panelStack.RemoveLast();   // 먼저 제거 → close에서 UnregisterPanel을 호출해도 no-op
            top.close?.Invoke();
            UpdateBackButton();
            UpdateMainMenuVisibility();
        }

        private void UpdateBackButton()
        {
            if (backtomainmenuButton == null) return;
            backtomainmenuButton.gameObject.SetActive(panelStack.Count > 0);
        }

        /// <summary>메인 메뉴 패널은 스택이 비어있을 때만 보이게.</summary>
        private void UpdateMainMenuVisibility()
        {
            if (mainMenuPanel == null) return;
            mainMenuPanel.SetActive(panelStack.Count == 0);
        }

        /// <summary>CardListController(Dictionary/Deck 모드)가 카드 클릭 시 호출.
        /// 부모 체인이 비활성이면 강제로 활성화하고, 좌/우 nav를 위해 리스트 전체를 넘긴다.
        /// InGameUIController.ShowRangeInfo와 동일한 패턴.</summary>
        public void ShowCardInfo(IList<CardData> cards, int startIndex)
        {
            if (cardInfo == null || cards == null) return;
            var t = cardInfo.transform;
            while (t != null) { if (!t.gameObject.activeSelf) t.gameObject.SetActive(true); t = t.parent; }
            cardInfo.transform.SetAsLastSibling();
            cardInfo.ShowWithList(cards, startIndex);
        }

        #endregion

        /// <summary>
        /// 메인 메뉴 버튼들의 라벨 TextMeshPro에 LocalizationBinder를 강제로 부착/갱신해
        /// continue → newGame → compendium(Dictionary) → ranking → settings → exit 순으로
        /// 모든 언어에서 올바른 키가 대응되도록 보장한다. Inspector 설정과 무관하게 적용된다.
        /// </summary>
        private void ApplyButtonLocalization()
        {
            BindButtonText(continueButton,  "main_continue");
            BindButtonText(newGameButton,   "main_newgame");
            BindButtonText(dictionaryButton,"main_compendium");
            BindButtonText(rankingButton,   "main_ranking");
            BindButtonText(settingsButton,  "public_settings");
            BindButtonText(quitButton,      "main_exit");
        }

        private static void BindButtonText(Button button, string stringCode)
        {
            if (button == null) return;
            var tmp = button.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            if (tmp == null) return;
            var binder = tmp.GetComponent<LocalizationBinder>();
            if (binder == null) binder = tmp.gameObject.AddComponent<LocalizationBinder>();
            binder.SetCode(stringCode);
        }

        private void InitializeUI()
        {
            RefreshContinueButton();

            // 모든 sub-panel 비활성화. mainMenuPanel은 ShowMainMenu가 UpdateMainMenuVisibility로 처리.
            SetPanelActive(mainMenuPanel, false);
            DeactivateAllSubPanels();
            // back 버튼은 스택이 비어 있을 때 숨겨야 함.
            if (backtomainmenuButton != null) backtomainmenuButton.gameObject.SetActive(false);
        }

        private void RefreshContinueButton()
        {
            if (continueButton == null)
            {
                Debug.LogWarning("[MainMenu] continueButton 미할당 — Inspector에서 슬롯을 확인하세요");
                return;
            }

            bool hasSaveData = GameManager.HasSavedRun();
            continueButton.interactable = hasSaveData;
            Debug.Log($"[MainMenu] Continue 버튼 활성 상태: {hasSaveData} (PlayerPrefs 키 존재 여부)");
        }

        private void SetupButtonListeners()
        {
            // 메인 메뉴 버튼
            newGameButton?.onClick.AddListener(OnNewGameClicked);
            continueButton?.onClick.AddListener(OnContinueClicked);
            settingsButton?.onClick.AddListener(OnSettingsClicked);
            rankingButton?.onClick.AddListener(OnRankingClicked);
            dictionaryButton?.onClick.AddListener(OnDictionaryClicked);
            quitButton?.onClick.AddListener(OnQuitClicked);

            // 캐릭터 선택 버튼
            for (int i = 0; i < characterButtons.Length; i++)
            {
                int index = i;
                characterButtons[i]?.onClick.AddListener(() => OnCharacterSelected(index));
            }

            // 도감 진입 버튼
            openCardListButton?.onClick.AddListener(OnOpenCardListClicked);
            openRelicListButton?.onClick.AddListener(OnOpenRelicListClicked);
            openPotionListButton?.onClick.AddListener(OnOpenPotionListClicked);

            startRunButton?.onClick.AddListener(OnStartRunClicked);
            // 범용 뒤로가기 — 스택 최상단 패널을 닫음. 빈 스택일 때는 OnBackClicked 자체가 no-op.
            backtomainmenuButton?.onClick.AddListener(OnBackClicked);

            // 시작 아이템 코드 입력 필드
            if (startingItemCodeInputField != null)
            {
                startingItemCodeInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
                startingItemCodeInputField.onValueChanged.AddListener(OnItemCodeInputChanged);
            }

            // 버튼 호버 효과 추가
            AddHoverEffect(newGameButton);
            AddHoverEffect(continueButton);
            AddHoverEffect(settingsButton);
            AddHoverEffect(rankingButton);
            AddHoverEffect(dictionaryButton);
            AddHoverEffect(quitButton);
            AddHoverEffect(startRunButton);
        }

        private void AddHoverEffect(Button button)
        {
            if (button == null) return;

            var eventTrigger = button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (eventTrigger == null)
            {
                eventTrigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            }

            // Hover Enter
            var entryEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
            entryEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            entryEnter.callback.AddListener((data) => OnButtonHover(button));
            eventTrigger.triggers.Add(entryEnter);
        }

        private void OnButtonHover(Button button)
        {
            if (buttonHoverSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(buttonHoverSound, 0.5f);
            }

            // 버튼 스케일 애니메이션
            StartCoroutine(ScaleButton(button.transform, 1.05f, 0.1f));
        }

        private IEnumerator ScaleButton(Transform target, float scale, float duration)
        {
            Vector3 originalScale = Vector3.one;
            Vector3 targetScale = Vector3.one * scale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                target.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / duration);
                yield return null;
            }

            // 원래 크기로 복구
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                target.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / duration);
                yield return null;
            }

            target.localScale = originalScale;
        }

        private void PlayButtonSound()
        {
            if (buttonClickSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(buttonClickSound);
            }
        }

        private void PlayBackgroundMusic()
        {
            if (menuMusic != null && audioSource != null)
            {
                audioSource.clip = menuMusic;
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        #region Panel Management

        /// <summary>스택을 비우고 메인 메뉴로 복귀. 모든 sub-panel close 액션이 실행되어 정리됨.
        /// UpdateMainMenuVisibility가 mainMenuPanel을 자동으로 활성화.</summary>
        private void ShowMainMenu()
        {
            ClearStack();
            // mainMenuPanel은 UpdateMainMenuVisibility(스택 비었으니 활성)가 처리.
            // FadeInPanel은 시각 효과용으로 그대로 호출 (이미 활성 상태에서 alpha만 보간).
            if (mainMenuPanel != null) StartCoroutine(FadeInPanel(mainMenuPanel));
            RefreshContinueButton();
        }

        private void ShowCharacterSelect()
        {
            // 캐릭터 선택은 mainMenu에서 시작하는 패널 — 스택에 push.
            PushPanel("CharacterSelect", () =>
            {
                if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
            });
            if (characterSelectPanel != null)
            {
                characterSelectPanel.SetActive(true);
                StartCoroutine(FadeInPanel(characterSelectPanel));
            }

            // 아이템 입력 필드 초기화
            overrideItemCode = null;
            if (startingItemCodeInputField != null) startingItemCodeInputField.text = "";
            if (itemPreviewText != null) itemPreviewText.text = "";

            // 첫 번째 캐릭터 기본 선택
            if (characters != null && characters.Length > 0)
            {
                OnCharacterSelected(0);
            }
        }

        /// <summary>씬 초기화용 — 모든 sub-panel을 비활성화하되 스택은 건드리지 않음.
        /// InitializeUI에서만 호출.</summary>
        private void DeactivateAllSubPanels()
        {
            SetPanelActive(characterSelectPanel, false);
            SetPanelActive(settingsPanel, false);
            SetPanelActive(LeaderboardPanel, false);
            SetPanelActive(dictionaryPanel, false);
            SetPanelActive(cardListPanel, false);
            SetPanelActive(relicListPanel, false);
            SetPanelActive(potionListPanel, false);
        }

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }

        private IEnumerator FadeInPanel(GameObject panel)
        {
            if (panel == null) yield break;

            panel.SetActive(true);
            // backtomainmenuButton 활성화는 UpdateBackButton(스택 기반)이 담당하므로 여기서 만지지 않음.
            CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panel.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 0f;
            float elapsed = 0f;

            while (elapsed < panelFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = fadeCurve.Evaluate(elapsed / panelFadeDuration);
                canvasGroup.alpha = t;
                yield return null;
            }

            canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOutPanel(GameObject panel, System.Action onComplete = null)
        {
            if (panel == null) yield break;

            CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                panel.SetActive(false);
                onComplete?.Invoke();
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < panelFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - fadeCurve.Evaluate(elapsed / panelFadeDuration);
                canvasGroup.alpha = t;
                yield return null;
            }

            canvasGroup.alpha = 0f;
            panel.SetActive(false);
            onComplete?.Invoke();
        }

        #endregion

        #region Button Callbacks

        private void OnNewGameClicked()
        {
            PlayButtonSound();
            ShowCharacterSelect();
        }

        private void OnContinueClicked()
        {
            PlayButtonSound();
            RelicLibrary.RegisterAll();
            ItemLibrary.RegisterAll();

            if (GameManager.Instance == null || !GameManager.Instance.LoadRun())
            {
                Debug.LogError("[MainMenu] 세이브 로드 실패");
                return;
            }

            GameManager.Instance.EnterPlaying();
            SceneLoader.Instance?.LoadScene("InGame");
        }

        private void OnSettingsClicked()
        {
            PlayButtonSound();
            PushPanel("Settings", () =>
            {
                if (settingsPanel != null) settingsPanel.SetActive(false);
            });
            SetPanelActive(settingsPanel, true);

            // 인스펙터 미연결 시 자동 탐색
            if (settingsMenuController == null && settingsPanel != null)
                settingsMenuController = settingsPanel.GetComponentInChildren<SettingsMenuController>(true);

            if (settingsMenuController != null)
                settingsMenuController.Open();
        }

        private void OnCreditsClicked()
        {
            PlayButtonSound();
            PushPanel("Credits", () =>
            {
                if (LeaderboardPanel != null) LeaderboardPanel.SetActive(false);
            });
            StartCoroutine(FadeInPanel(LeaderboardPanel));
        }

        private void OnRankingClicked()
        {
            PlayButtonSound();
            Debug.Log("[MainMenu] 랭킹 열기");
        }

        // 도감 진입 — 메인 메뉴 위에 dictionaryPanel을 오버레이.
        private void OnDictionaryClicked()
        {
            PlayButtonSound();
            PushPanel("Dictionary", () =>
            {
                if (dictionaryPanel != null) dictionaryPanel.SetActive(false);
            });
            StartCoroutine(FadeInPanel(dictionaryPanel));
        }

        // 도감 하위 패널 — dictionaryPanel을 끄지 않고 그 위에 push.
        // back으로 list를 닫으면 dictionaryPanel이 그대로 남아 있다.
        private void OnOpenCardListClicked()
        {
            PlayButtonSound();
            PushPanel("CardList", () =>
            {
                if (cardListPanel != null) cardListPanel.SetActive(false);
            });
            StartCoroutine(FadeInPanel(cardListPanel));
        }

        private void OnOpenRelicListClicked()
        {
            PlayButtonSound();
            PushPanel("RelicList", () =>
            {
                if (relicListPanel != null) relicListPanel.SetActive(false);
            });
            StartCoroutine(FadeInPanel(relicListPanel));
        }

        private void OnOpenPotionListClicked()
        {
            PlayButtonSound();
            PushPanel("PotionList", () =>
            {
                if (potionListPanel != null) potionListPanel.SetActive(false);
            });
            StartCoroutine(FadeInPanel(potionListPanel));
        }

        private void OnQuitClicked()
        {
            PlayButtonSound();
            Debug.Log("[MainMenu] 게임 종료");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnCharacterSelected(int index)
        {
            PlayButtonSound();

            if (characters == null || index >= characters.Length) return;

            CharacterData character = characters[index];
            selectedCharacter = character.characterType;

            // UI 업데이트
            if (characterNameText != null)
                characterNameText.text = character.characterName;

            if (characterDescText != null)
                characterDescText.text = character.description;

            if (characterStatsText != null)
                characterStatsText.text = $"HP: {character.baseHP}";

            if (characterPortrait != null && character.portrait != null)
                characterPortrait.sprite = character.portrait;

            // 선택된 버튼 하이라이트
            for (int i = 0; i < characterButtons.Length; i++)
            {
                var colors = characterButtons[i].colors;
                Color baseColor = (i == index) ? Color.yellow : Color.white;
                colors.normalColor = baseColor;
                colors.highlightedColor = baseColor;
                colors.selectedColor = baseColor;
                characterButtons[i].colors = colors;
            }

            startRunButton.interactable = true;
        }

        private void OnItemCodeInputChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                overrideItemCode = null;
                if (itemPreviewText != null) itemPreviewText.text = "";
                return;
            }

            if (int.TryParse(value, out int code))
            {
                overrideItemCode = code;

                // 아이템 이름 미리보기
                if (itemPreviewText != null)
                {
                    ItemLibrary.RegisterAll();
                    var itemData = ItemRegistry.GetAll().Find(i => i.itemCode == code);
                    itemPreviewText.text = itemData != null
                        ? $"아이템: {itemData.itemName}"
                        : $"코드 {code}: 알 수 없는 아이템";
                }
            }
            else
            {
                overrideItemCode = null;
                if (itemPreviewText != null) itemPreviewText.text = "올바른 숫자를 입력하세요";
            }
        }

        private void OnStartRunClicked()
        {
            PlayButtonSound();
            Debug.Log($"[MainMenu] 새 런 시작 - 캐릭터: {selectedCharacter}");

            CharacterData charData = characters != null ? System.Array.Find(characters, c => c.characterType == selectedCharacter) : null;
            int    hp       = charData != null ? charData.baseHP           : 75;
            int    gold     = charData != null ? charData.startingGold     : 99;
            int[]  deckCodes = charData?.startingDeckCodes;

            GameManager.Instance?.StartNewRun(selectedCharacter, hp, gold, deckCodes);

            RelicLibrary.RegisterAll();
            ItemLibrary.RegisterAll();

            // 캐릭터 기본 시작 유물 지급
            if (charData?.startingRelicCodes != null && charData.startingRelicCodes.Length > 0)
            {
                foreach (var relicCode in charData.startingRelicCodes)
                    GameManager.Instance?.AddRelic(relicCode);
            }

            // 캐릭터 기본 시작 아이템 지급
            if (charData?.startingItemCodes != null && charData.startingItemCodes.Length > 0)
            {
                foreach (var itemCode in charData.startingItemCodes)
                {
                    var itemData = ItemRegistry.GetAll().Find(i => i.itemCode == itemCode);
                    if (itemData != null)
                        GameManager.Instance?.AddItem(itemData);
                    else
                        Debug.LogWarning($"[MainMenu] 시작 아이템 코드 {itemCode} 를 찾을 수 없음");
                }
            }

            // UI 입력 오버라이드 아이템 추가 지급
            if (overrideItemCode.HasValue)
            {
                var itemData = ItemRegistry.GetAll().Find(i => i.itemCode == overrideItemCode.Value);
                if (itemData != null)
                {
                    Debug.Log($"[MainMenu] 오버라이드 시작 아이템 코드 {overrideItemCode.Value} 지급: {itemData.itemName}");
                    GameManager.Instance?.AddItem(itemData);
                }
                else
                {
                    Debug.LogWarning($"[MainMenu] 오버라이드 아이템 코드 {overrideItemCode.Value} 를 찾을 수 없음");
                }
            }

            GameManager.Instance?.EnterPlaying();
            SceneLoader.Instance?.LoadScene("InGame");
        }

        // 외부 UI(예: 캐릭터 선택 패널 내부의 다른 버튼)에서 메인 메뉴로 강제 복귀하고 싶을 때 호출.
        // backButton 자체는 SetupButtonListeners에서 OnBackClicked로 연결됨.
        public void OnBackToMainMenu()
        {
            PlayButtonSound();
            ShowMainMenu();
        }

        #endregion
    }

    #region Character Data

    
    [System.Serializable]
    public class CharacterData
    {
        public string characterName;
        public CharacterType characterType;
        public Sprite portrait;
        [TextArea(3, 5)]
        public string description;
        public int baseHP;
        public int startingGold = 99;
        public Color themeColor;
        [Tooltip("런 시작 시 지급되는 유물 코드 목록 (RelicDataTemplate.csv의 relicCode 참조)")]
        public int[] startingRelicCodes;
        [Tooltip("런 시작 시 지급되는 아이템 코드 목록 (ItemDataTemplate.csv의 itemCode 참조)")]
        public int[] startingItemCodes;
        [Tooltip("스타팅 덱 카드 코드 목록. 비어있으면 DeckManager 기본값 사용.")]
        public int[] startingDeckCodes;
    }

    #endregion
}
