using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
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
        [SerializeField] private GameObject creditsPanel;

        [Header("Main Menu Buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button patchButton;
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

        private void Awake()
        {
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
            if (settingsMenuController != null)
                settingsMenuController.OnClosed += ShowMainMenu;
            ShowMainMenu();
            PlayBackgroundMusic();
        }

        private void InitializeUI()
        {
            // Continue 버튼은 저장 데이터가 있을 때만 활성화
            if (continueButton != null)
            {
                bool hasSaveData = PlayerPrefs.HasKey("SaveData");
                continueButton.interactable = hasSaveData;
            }

            // 모든 패널 비활성화
            SetPanelActive(mainMenuPanel, false);
            SetPanelActive(characterSelectPanel, false);
            SetPanelActive(settingsPanel, false);
            SetPanelActive(creditsPanel, false);
        }

        private void SetupButtonListeners()
        {
            // 메인 메뉴 버튼
            newGameButton?.onClick.AddListener(OnNewGameClicked);
            continueButton?.onClick.AddListener(OnContinueClicked);
            settingsButton?.onClick.AddListener(OnSettingsClicked);
            patchButton?.onClick.AddListener(OnCreditsClicked);
            quitButton?.onClick.AddListener(OnQuitClicked);

            // 캐릭터 선택 버튼
            for (int i = 0; i < characterButtons.Length; i++)
            {
                int index = i;
                characterButtons[i]?.onClick.AddListener(() => OnCharacterSelected(index));
            }

            startRunButton?.onClick.AddListener(OnStartRunClicked);
            backtomainmenuButton?.onClick.AddListener(OnBackFromCharSelectClicked);

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
            AddHoverEffect(patchButton);
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

        private void ShowMainMenu()
        {
            HideAllPanels();
            StartCoroutine(FadeInPanel(mainMenuPanel));
            backtomainmenuButton.gameObject.SetActive(false);
        }

        private void ShowCharacterSelect()
        {
            HideAllPanels();
            StartCoroutine(FadeInPanel(characterSelectPanel));

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

        private void HideAllPanels()
        {
            SetPanelActive(mainMenuPanel, false);
            SetPanelActive(characterSelectPanel, false);
            SetPanelActive(settingsPanel, false);
            SetPanelActive(creditsPanel, false);
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
            backtomainmenuButton?.gameObject.SetActive(true);
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
            // TODO: 저장된 게임 로드
            Debug.Log("[MainMenu] 저장된 게임 로드");
        }

        private void OnSettingsClicked()
        {
            PlayButtonSound();
            HideAllPanels();
            SetPanelActive(settingsPanel, true);
            backtomainmenuButton?.gameObject.SetActive(true);

            // 인스펙터 미연결 시 자동 탐색
            if (settingsMenuController == null && settingsPanel != null)
                settingsMenuController = settingsPanel.GetComponentInChildren<SettingsMenuController>(true);

            if (settingsMenuController != null)
                settingsMenuController.Open();
        }

        private void OnCreditsClicked()
        {
            PlayButtonSound();
            HideAllPanels();
            StartCoroutine(FadeInPanel(creditsPanel));
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
                characterStatsText.text = $"HP: {character.baseHP}\n" +
                                         $"에너지: {character.baseEnergy}";

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
            int    energy   = charData != null ? charData.baseEnergy       : 3;
            int[]  deckCodes = charData?.startingDeckCodes;

            GameManager.Instance?.StartNewRun(selectedCharacter, hp, gold, energy, deckCodes);

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

            GameManager.Instance?.ChangeState(GameState.Map);
            SceneLoader.Instance?.LoadScene("InGame");
        }

        private void OnBackFromCharSelectClicked()
        {
            PlayButtonSound();
            ShowMainMenu();
        }

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
        public int baseEnergy;
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
