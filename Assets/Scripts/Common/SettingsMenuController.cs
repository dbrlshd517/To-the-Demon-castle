using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
using DeckRoguelike.Core;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 설정 화면 UI 컨트롤러
    /// 
    /// [사용법]
    /// 1. SettingsCanvas 프리팹 만들기
    /// 2. Inspector에서 UI 연결
    /// 3. 각 씬에 프리팹 배치
    /// 
    /// [구조]
    /// SettingsCanvas (프리팹)
    /// ├── SettingsPanel          ← UI
    /// │   ├── AudioTabButton
    /// │   └── ...
    /// └── SettingsController     ← 이 스크립트
    /// </summary>
    public class SettingsMenuController : MonoBehaviour
    {
        [Header("=== Panel ===")]
        [SerializeField] private GameObject settingsPanel;

        [Header("=== Tabs ===")]
        [SerializeField] private Button audioTabButton;
        [SerializeField] private Button displayTabButton;
        [SerializeField] private Button gameplayTabButton;
        [SerializeField] private GameObject audioPanel;
        [SerializeField] private GameObject displayPanel;
        [SerializeField] private GameObject gameplayPanel;

        [Header("=== Audio Settings ===")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [Tooltip("마스터 볼륨 슬라이더 옆 퍼센트 값 표시")]
        [SerializeField] private TextMeshProUGUI masterVolumePercentText;
        [Tooltip("음악 볼륨 슬라이더 옆 퍼센트 값 표시")]
        [SerializeField] private TextMeshProUGUI musicVolumePercentText;
        [Tooltip("효과음 볼륨 슬라이더 옆 퍼센트 값 표시")]
        [SerializeField] private TextMeshProUGUI sfxVolumePercentText;
        [SerializeField] private Toggle muteToggle;
        [SerializeField] private Toggle muteInBackgroundToggle;

        [Header("=== Display Settings ===")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Toggle vsyncToggle;
        [SerializeField] private TMP_Dropdown frameRateDropdown;

        [Header("=== Gameplay Settings ===")]
        [SerializeField] private Toggle screenShakeToggle;
        [SerializeField] private Toggle autoEndTurnToggle;
        [SerializeField] private Toggle fastModeToggle;
        [SerializeField] private Toggle cardConfirmationToggle;
        [SerializeField] private Toggle damageNumbersToggle;
        [SerializeField] private TMP_Dropdown languageDropdown;

        [Header("=== Buttons ===")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button backButton;

        [Header("=== Game Buttons ===")]
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button abandonRunButton;
        [SerializeField] private Button restartCombatButton;

        [Header("=== Localized Labels ===")]
        [Tooltip("마스터 볼륨")]
        [SerializeField] private TextMeshProUGUI masterVolumeText;
        [Tooltip("효과 볼륨")]
        [SerializeField] private TextMeshProUGUI sfxVolumeText;
        [Tooltip("음악 볼륨")]
        [SerializeField] private TextMeshProUGUI musicVolumeText;
        [Tooltip("전투 재시작 버튼 라벨 (setting_combatrestart)")]
        [SerializeField] private TextMeshProUGUI combatRestartText;
        [Tooltip("런 포기 버튼 라벨 (setting_abandonrun)")]
        [SerializeField] private TextMeshProUGUI abandonRunText;
        [Tooltip("메인메뉴 버튼 라벨 — MainMenu 씬에서는 main_exit, 그 외에서는 setting_mainmenubutton")]
        [SerializeField] private TextMeshProUGUI mainMenuButtonText;
        [Tooltip("언어 선택 라벨 (setting_language)")]
        [SerializeField] private TextMeshProUGUI languageText;
        [Tooltip("전체화면 토글 라벨 (setting_fullscreen)")]
        [SerializeField] private TextMeshProUGUI fullscreenText;
        [Tooltip("화면 흔들림 토글 라벨 (setting_screenshake)")]
        [SerializeField] private TextMeshProUGUI screenShakeText;
        [Tooltip("VSync 토글 라벨 (setting_vsync)")]
        [SerializeField] private TextMeshProUGUI vSyncText;
        [Tooltip("카드 확인 토글 라벨 (setting_cardconfirmation)")]
        [SerializeField] private TextMeshProUGUI cardConfirmationText;
        [Tooltip("빠른 모드 토글 라벨 (setting_fastmode)")]
        [SerializeField] private TextMeshProUGUI fastModeText;
        [Tooltip("백그라운드 음소거 토글 라벨 (setting_muteinbackground)")]
        [SerializeField] private TextMeshProUGUI muteInBackgroundText;

        [Header("=== Audio ===")]
        [SerializeField] private AudioClip buttonClickSound;

        public event System.Action OnClosed;

        private AudioSource audioSource;
        private Resolution[] availableResolutions;
        private List<Vector2Int> resolutionList = new List<Vector2Int>(); // 드롭다운 index → (width, height)
        private GameSettings tempSettings;
        private int currentTab = 0;
        // Open() 호출로 명시적으로 열린 경우에만 true.
        // 첫 활성화 시 Start의 자동 Close가 Open() 호출 직후 실행돼 패널이 사라지는 버그 방지용.
        private bool _explicitlyOpened;

        // CSV 헤더 순서와 동일: en,pt_BR,zh_CN,zh_TW,nl,eo,fi,fr,de,id,it,ja,ko,pl,ru,sr,sr_Latn,es,th,tr,uk,vi
        private static readonly string[] LanguageCodes =
        {
            "en", "pt_BR", "zh_CN", "zh_TW", "nl", "eo", "fi", "fr", "de", "id", "it",
            "ja", "ko", "pl", "ru", "sr", "sr_Latn", "es", "th", "tr", "uk", "vi"
        };

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
            SetupResolutions();
            SetupDropdowns();
            InitializeSlidersFromSettings();
            SetupListeners();
            RefreshLocalizedLabels();
            LocalizationManager.OnLanguageChanged += RefreshLocalizedLabels;
            // 명시적 Open() 호출 없이 활성 상태로 도달한 경우에만 닫는다.
            // (씬 저장이 활성으로 돼 있을 때만 닫고, Open() 직후 Start가 실행되는 경우는 건드리지 않음)
            if (!_explicitlyOpened) Close();
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedLabels;
        }

        private void Update()
        {
            if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;

            if (IsOpen)
                OnBackClicked();
            else
                Open();
        }

        #region Public Methods

        public void Open()
        {
            if (settingsPanel == null) return;

            _explicitlyOpened = true;
            settingsPanel.SetActive(true);
            LoadCurrentSettings();
            ShowTab(0);
            UpdateGameButtons();
        }

        public void Close()
        {
            if (settingsPanel == null) return;
            _explicitlyOpened = false;
            ApplyTempSettings();
            settingsPanel.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        public bool IsOpen => settingsPanel != null && settingsPanel.activeSelf;

        #endregion

        #region Setup

        private void SetupResolutions()
        {
            if (resolutionDropdown == null) return;

            availableResolutions = Screen.resolutions;
            resolutionDropdown.ClearOptions();
            resolutionList.Clear();

            List<string> options = new List<string>();
            int currentResolutionIndex = 0;

            for (int i = 0; i < availableResolutions.Length; i++)
            {
                int w = availableResolutions[i].width;
                int h = availableResolutions[i].height;
                string option = $"{w} x {h}";

                if (!options.Contains(option))
                {
                    options.Add(option);
                    resolutionList.Add(new Vector2Int(w, h));
                }

                if (w == Screen.currentResolution.width && h == Screen.currentResolution.height)
                    currentResolutionIndex = options.Count - 1;
            }

            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = currentResolutionIndex;
            resolutionDropdown.RefreshShownValue();
        }

        private void SetupDropdowns()
        {
            if (frameRateDropdown != null)
            {
                frameRateDropdown.ClearOptions();
                frameRateDropdown.AddOptions(new List<string> { "30 FPS", "60 FPS", "120 FPS", "무제한" });
            }

            RefreshLanguageDropdownLabels();
        }

        /// <summary>
        /// 언어 드롭다운 옵션을 현재 언어 기준으로 채워 넣습니다.
        /// LocalizationManager.Get("lang_<code>")로 각 언어 이름을 가져오므로 언어 변경 시마다 새로 호출되어야 합니다.
        /// </summary>
        private void RefreshLanguageDropdownLabels()
        {
            if (languageDropdown == null) return;

            int prevValue = languageDropdown.value;
            languageDropdown.ClearOptions();
            var options = new List<string>(LanguageCodes.Length);
            foreach (var code in LanguageCodes)
                options.Add(LocalizationManager.Get($"lang_{code}"));
            languageDropdown.AddOptions(options);
            languageDropdown.SetValueWithoutNotify(Mathf.Clamp(prevValue, 0, options.Count - 1));
            languageDropdown.RefreshShownValue();
        }

        private void SetupListeners()
        {
            // 탭 버튼
            if (audioTabButton != null) audioTabButton.onClick.AddListener(() => ShowTab(0));
            if (displayTabButton != null) displayTabButton.onClick.AddListener(() => ShowTab(1));
            if (gameplayTabButton != null) gameplayTabButton.onClick.AddListener(() => ShowTab(2));

            // 오디오
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            if (muteToggle != null) muteToggle.onValueChanged.AddListener(OnMuteChanged);
            if (muteInBackgroundToggle != null) muteInBackgroundToggle.onValueChanged.AddListener(OnMuteInBackgroundChanged);

            // 디스플레이
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            if (vsyncToggle != null) vsyncToggle.onValueChanged.AddListener(OnVSyncChanged);
            if (frameRateDropdown != null) frameRateDropdown.onValueChanged.AddListener(OnFrameRateChanged);

            // 게임플레이
            if (screenShakeToggle != null) screenShakeToggle.onValueChanged.AddListener(OnScreenShakeChanged);
            if (autoEndTurnToggle != null) autoEndTurnToggle.onValueChanged.AddListener(OnAutoEndTurnChanged);
            if (fastModeToggle != null) fastModeToggle.onValueChanged.AddListener(OnFastModeChanged);
            if (cardConfirmationToggle != null) cardConfirmationToggle.onValueChanged.AddListener(OnCardConfirmationChanged);
            if (damageNumbersToggle != null) damageNumbersToggle.onValueChanged.AddListener(OnDamageNumbersChanged);
            if (languageDropdown != null) languageDropdown.onValueChanged.AddListener(OnLanguageChanged);

            // 버튼
            if (applyButton != null) applyButton.onClick.AddListener(OnApplyClicked);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
            if (backButton != null) backButton.onClick.AddListener(OnBackClicked);

            // 게임 버튼
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            if (abandonRunButton != null) abandonRunButton.onClick.AddListener(OnAbandonRunClicked);
            if (restartCombatButton != null) restartCombatButton.onClick.AddListener(OnRestartCombatClicked);
        }

        #endregion

        #region Load Settings

        /// <summary>
        /// 씬 시작 시 이벤트 발화 없이 슬라이더를 SettingsManager 값으로 초기화.
        /// SetupListeners() 전에 호출해야 한다.
        /// </summary>
        private void InitializeSlidersFromSettings()
        {
            GameSettings settings = SettingsManager.Instance != null
                ? SettingsManager.Instance.CurrentSettings
                : GameSettings.GetDefault();

            if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(settings.masterVolume);
            if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(settings.musicVolume);
            if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(settings.sfxVolume);
            if (muteToggle != null) muteToggle.SetIsOnWithoutNotify(settings.muted);
            if (muteInBackgroundToggle != null) muteInBackgroundToggle.SetIsOnWithoutNotify(settings.muteInBackground);
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(settings.fullscreen);
            if (vsyncToggle != null) vsyncToggle.SetIsOnWithoutNotify(settings.vsync);
            if (screenShakeToggle != null) screenShakeToggle.SetIsOnWithoutNotify(settings.screenShake);
            if (autoEndTurnToggle != null) autoEndTurnToggle.SetIsOnWithoutNotify(settings.autoEndTurn);
            if (fastModeToggle != null) fastModeToggle.SetIsOnWithoutNotify(settings.gameSpeed != GameSpeed.Normal);
            if (cardConfirmationToggle != null) cardConfirmationToggle.SetIsOnWithoutNotify(settings.cardConfirmation);
            if (damageNumbersToggle != null) damageNumbersToggle.SetIsOnWithoutNotify(settings.showDamageNumbers);
            UpdateVolumeTexts(settings);
        }

        /// <summary>
        /// 패널을 열 때 SettingsManager 값을 UI에 반영.
        /// SetValueWithoutNotify 사용으로 onValueChanged 발화 없음 → 저장 루프 방지.
        /// </summary>
        private void LoadCurrentSettings()
        {
            GameSettings settings = SettingsManager.Instance != null
                ? SettingsManager.Instance.CurrentSettings
                : GameSettings.GetDefault();
            tempSettings = settings.Clone();

            // 오디오 — 이벤트 없이 세팅
            if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(settings.masterVolume);
            if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(settings.musicVolume);
            if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(settings.sfxVolume);
            if (muteToggle != null) muteToggle.SetIsOnWithoutNotify(settings.muted);
            if (muteInBackgroundToggle != null) muteInBackgroundToggle.SetIsOnWithoutNotify(settings.muteInBackground);
            UpdateVolumeTexts(settings);

            // 디스플레이
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(settings.fullscreen);
            if (vsyncToggle != null) vsyncToggle.SetIsOnWithoutNotify(settings.vsync);
            if (resolutionDropdown != null && resolutionList.Count > 0)
            {
                int resIdx = resolutionList.FindIndex(r => r.x == settings.resolutionWidth && r.y == settings.resolutionHeight);
                resolutionDropdown.SetValueWithoutNotify(resIdx >= 0 ? resIdx : 0);
                resolutionDropdown.RefreshShownValue();
            }
            if (frameRateDropdown != null)
            {
                frameRateDropdown.SetValueWithoutNotify(settings.targetFrameRate switch
                {
                    30 => 0, 60 => 1, 120 => 2, _ => 3
                });
                frameRateDropdown.RefreshShownValue();
            }

            // 게임플레이
            if (screenShakeToggle != null) screenShakeToggle.SetIsOnWithoutNotify(settings.screenShake);
            if (autoEndTurnToggle != null) autoEndTurnToggle.SetIsOnWithoutNotify(settings.autoEndTurn);
            if (fastModeToggle != null) fastModeToggle.SetIsOnWithoutNotify(settings.gameSpeed != GameSpeed.Normal);
            if (cardConfirmationToggle != null) cardConfirmationToggle.SetIsOnWithoutNotify(settings.cardConfirmation);
            if (damageNumbersToggle != null) damageNumbersToggle.SetIsOnWithoutNotify(settings.showDamageNumbers);
            if (languageDropdown != null)
            {
                int langIdx = System.Array.IndexOf(LanguageCodes, settings.language);
                languageDropdown.SetValueWithoutNotify(langIdx >= 0 ? langIdx : 0);
                languageDropdown.RefreshShownValue();
            }
        }

        private void UpdateVolumeTexts(GameSettings settings = null)
        {
            float master = settings?.masterVolume ?? (masterVolumeSlider != null ? masterVolumeSlider.value : 1f);
            float music  = settings?.musicVolume  ?? (musicVolumeSlider  != null ? musicVolumeSlider.value  : 1f);
            float sfx    = settings?.sfxVolume    ?? (sfxVolumeSlider    != null ? sfxVolumeSlider.value    : 1f);

            if (masterVolumePercentText != null) masterVolumePercentText.text = $"{Mathf.RoundToInt(master * 100)}%";
            if (musicVolumePercentText  != null) musicVolumePercentText.text  = $"{Mathf.RoundToInt(music  * 100)}%";
            if (sfxVolumePercentText    != null) sfxVolumePercentText.text    = $"{Mathf.RoundToInt(sfx    * 100)}%";
        }

        #endregion

        #region Tab Management

        private void ShowTab(int tabIndex)
        {
            currentTab = tabIndex;
            PlaySound(buttonClickSound);

            if (audioPanel != null) audioPanel.SetActive(tabIndex == 0);
            if (displayPanel != null) displayPanel.SetActive(tabIndex == 1);
            if (gameplayPanel != null) gameplayPanel.SetActive(tabIndex == 2);

            UpdateTabButtonStyle(audioTabButton, tabIndex == 0);
            UpdateTabButtonStyle(displayTabButton, tabIndex == 1);
            UpdateTabButtonStyle(gameplayTabButton, tabIndex == 2);
        }

        private void UpdateTabButtonStyle(Button button, bool isActive)
        {
            if (button == null) return;

            ColorBlock colors = button.colors;
            colors.normalColor = isActive ? new Color(0.3f, 0.6f, 1f) : Color.white;
            button.colors = colors;
        }

        #endregion

        #region Callbacks

        private void OnMasterVolumeChanged(float value)
        {
            if (tempSettings != null) tempSettings.masterVolume = value;
            SettingsManager.Instance?.SetMasterVolume(value);
            UpdateVolumeTexts();
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (tempSettings != null) tempSettings.musicVolume = value;
            SettingsManager.Instance?.SetMusicVolume(value);
            UpdateVolumeTexts();
        }

        private void OnSFXVolumeChanged(float value)
        {
            if (tempSettings != null) tempSettings.sfxVolume = value;
            SettingsManager.Instance?.SetSFXVolume(value);
            UpdateVolumeTexts();
        }

        private void OnMuteChanged(bool muted)
        {
            if (tempSettings != null) tempSettings.muted = muted;
            SettingsManager.Instance?.SetMute(muted);
        }

        private void OnMuteInBackgroundChanged(bool enabled)
        {
            if (tempSettings != null) tempSettings.muteInBackground = enabled;
            SettingsManager.Instance?.SetMuteInBackground(enabled);
        }

        private void OnResolutionChanged(int index)
        {
            if (index < 0 || index >= resolutionList.Count) return;
            Vector2Int res = resolutionList[index];
            if (tempSettings != null) { tempSettings.resolutionWidth = res.x; tempSettings.resolutionHeight = res.y; }
            SettingsManager.Instance?.SetResolution(res.x, res.y);
        }

        private void OnFullscreenChanged(bool isFullscreen)
        {
            if (tempSettings != null) tempSettings.fullscreen = isFullscreen;
            SettingsManager.Instance?.SetFullscreen(isFullscreen);
        }

        private void OnVSyncChanged(bool enabled)
        {
            if (tempSettings != null) tempSettings.vsync = enabled;
            SettingsManager.Instance?.SetVSync(enabled);
        }

        private void OnFrameRateChanged(int index)
        {
            int fps = index switch { 0 => 30, 1 => 60, 2 => 120, _ => -1 };
            if (tempSettings != null) tempSettings.targetFrameRate = fps;
            SettingsManager.Instance?.SetTargetFrameRate(fps);
        }

        private void OnScreenShakeChanged(bool enabled)
        {
            if (tempSettings != null) tempSettings.screenShake = enabled;
            SettingsManager.Instance?.SetScreenShake(enabled);
        }

        private void OnAutoEndTurnChanged(bool enabled)
        {
            if (tempSettings != null) tempSettings.autoEndTurn = enabled;
            SettingsManager.Instance?.SetAutoEndTurn(enabled);
        }

        private void OnFastModeChanged(bool enabled)
        {
            GameSpeed speed = enabled ? GameSpeed.Fast : GameSpeed.Normal;
            if (tempSettings != null) tempSettings.gameSpeed = speed;
            SettingsManager.Instance?.SetGameSpeed(speed);
        }

        private void OnCardConfirmationChanged(bool enabled)
        {
            if (tempSettings != null) tempSettings.cardConfirmation = enabled;
            SettingsManager.Instance?.SetCardConfirmation(enabled);
        }

        private void OnDamageNumbersChanged(bool enabled)
        {
            if (tempSettings != null) tempSettings.showDamageNumbers = enabled;
            SettingsManager.Instance?.SetShowDamageNumbers(enabled);
        }

        private void OnLanguageChanged(int index)
        {
            string lang = index >= 0 && index < LanguageCodes.Length ? LanguageCodes[index] : "ko";
            if (tempSettings != null) tempSettings.language = lang;
            SettingsManager.Instance?.SetLanguage(lang);
        }

        private void ApplyTempSettings()
        {
            if (SettingsManager.Instance == null || tempSettings == null) return;
            SettingsManager.Instance.SetResolution(tempSettings.resolutionWidth, tempSettings.resolutionHeight);
            SettingsManager.Instance.SetFullscreen(tempSettings.fullscreen);
            SettingsManager.Instance.SetVSync(tempSettings.vsync);
            SettingsManager.Instance.SetTargetFrameRate(tempSettings.targetFrameRate);
            SettingsManager.Instance.SetScreenShake(tempSettings.screenShake);
            SettingsManager.Instance.SetAutoEndTurn(tempSettings.autoEndTurn);
            SettingsManager.Instance.SetGameSpeed(tempSettings.gameSpeed);
            SettingsManager.Instance.SetCardConfirmation(tempSettings.cardConfirmation);
            SettingsManager.Instance.SetShowDamageNumbers(tempSettings.showDamageNumbers);
            SettingsManager.Instance.SetLanguage(tempSettings.language);
            SettingsManager.Instance.SaveSettings();
        }

        private void OnApplyClicked()
        {
            PlaySound(buttonClickSound);
            ApplyTempSettings();
        }

        private void OnResetClicked()
        {
            PlaySound(buttonClickSound);
            SettingsManager.Instance?.ResetToDefault();
            LoadCurrentSettings();
        }

        private void OnBackClicked()
        {
            PlaySound(buttonClickSound);
            Close(); // ApplyTempSettings는 Close() 내부에서 호출
            OnClosed?.Invoke();
        }

        private void OnMainMenuClicked()
        {
            PlaySound(buttonClickSound);

            // MainMenu 씬에서는 이 버튼이 Exit로 동작 (이미 메인 메뉴이므로 이동 불필요)
            if (IsInMainMenuScene())
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                return;
            }

            Close();
            Time.timeScale = 1f;
            GameManager.Instance?.EnterMainMenu();
            DeckRoguelike.Core.SceneLoader.Instance?.LoadScene("MainMenu");
        }

        private static bool IsInMainMenuScene() =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu";

        /// <summary>
        /// Inspector에 연결된 라벨 텍스트를 현재 언어로 갱신합니다.
        /// Start와 LocalizationManager.OnLanguageChanged에서 호출됩니다.
        /// </summary>
        private void RefreshLocalizedLabels()
        {
            RefreshLanguageDropdownLabels();

            SetLocalized(masterVolumeText, "setting_mastervolume");
            SetLocalized(sfxVolumeText,    "setting_sfxvolume");
            SetLocalized(musicVolumeText,  "setting_musicvolume");
            SetLocalized(combatRestartText,       "setting_combatrestart");
            SetLocalized(abandonRunText,          "setting_abandonrun");
            SetLocalized(languageText,            "setting_language");
            SetLocalized(fullscreenText,          "setting_fullscreen");
            SetLocalized(screenShakeText,         "setting_screenshake");
            SetLocalized(vSyncText,               "setting_vsync");
            SetLocalized(cardConfirmationText,    "setting_cardconfirmation");
            SetLocalized(fastModeText,            "setting_fastmode");
            SetLocalized(muteInBackgroundText,    "setting_muteinbackground");

            // MainMenu 씬: Exit 라벨, 그 외 씬: Main Menu 라벨
            SetLocalized(mainMenuButtonText, IsInMainMenuScene() ? "main_exit" : "setting_mainmenubutton");
        }

        private static void SetLocalized(TextMeshProUGUI text, string code)
        {
            if (text == null || string.IsNullOrEmpty(code)) return;
            text.text = LocalizationManager.Get(code);
        }

        private void OnAbandonRunClicked()
        {
            PlaySound(buttonClickSound);
            Close();
            Time.timeScale = 1f;
            GameManager.Instance?.AbandonRun();
        }

        private void OnRestartCombatClicked()
        {
            PlaySound(buttonClickSound);
            Close();
            Time.timeScale = 1f;
            var inGameUI = FindObjectOfType<DeckRoguelike.UI.InGameUIController>();
            inGameUI?.RestartCombat();
        }

        #endregion

        private void UpdateGameButtons()
        {
            var gm = GameManager.Instance;
            bool inRun = gm != null && gm.IsPlaying;
            bool inCombat = gm != null && gm.IsInCombat;

            if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(true);
            if (abandonRunButton != null) abandonRunButton.gameObject.SetActive(inRun);
            if (restartCombatButton != null) restartCombatButton.gameObject.SetActive(inCombat);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}