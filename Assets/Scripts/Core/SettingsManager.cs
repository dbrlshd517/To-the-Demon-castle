using UnityEngine;
using UnityEngine.Audio;
using System;
using System.Linq;

namespace DeckRoguelike.Core
{
    /// <summary>
    /// 게임 설정을 관리하고 저장/로드하는 매니저
    /// AudioMixer를 통해 볼륨 제어
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer audioMixer;

        private GameSettings currentSettings;
        private bool isApplicationFocused = true;

        // AudioMixer Parameter Names
        private const string MASTER_VOLUME = "MasterVolume";
        private const string MUSIC_VOLUME = "MusicVolume";
        private const string SFX_VOLUME = "SFXVolume";

        // Events
        public event Action<GameSettings> OnSettingsChanged;
        public event Action<float> OnMasterVolumeChanged;
        public event Action<float> OnMusicVolumeChanged;
        public event Action<float> OnSFXVolumeChanged;

        // Properties
        public GameSettings CurrentSettings => currentSettings;
        public AudioMixer AudioMixer => audioMixer;

        private const string SETTINGS_KEY = "GameSettings";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadSettings();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #region Focus / Background Mute

        private void OnApplicationFocus(bool hasFocus)
        {
            isApplicationFocused = hasFocus;
            UpdateBackgroundMute();
        }

        private void OnApplicationPause(bool isPaused)
        {
            isApplicationFocused = !isPaused;
            UpdateBackgroundMute();
        }

        private void UpdateBackgroundMute()
        {
            if (currentSettings == null || audioMixer == null) return;

            if (currentSettings.muteInBackground && !isApplicationFocused)
            {
                // 백그라운드 → Master 음소거
                audioMixer.SetFloat(MASTER_VOLUME, -80f);
            }
            else
            {
                // 포커스 복귀 → 볼륨 복원
                ApplyMasterVolume();
            }
        }

        public void SetMuteInBackground(bool enabled)
        {
            currentSettings.muteInBackground = enabled;
            SaveSettings();
        }

        #endregion

        #region Settings Management

        public void LoadSettings()
        {
            if (PlayerPrefs.HasKey(SETTINGS_KEY))
            {
                string json = PlayerPrefs.GetString(SETTINGS_KEY);
                currentSettings = JsonUtility.FromJson<GameSettings>(json);
                Debug.Log("[SettingsManager] 저장된 설정 로드 완료");
            }
            else
            {
                // defaultSettings는 [Serializable] 클래스라 null이 아닌 것처럼 보이지만
                // masterVolume 등이 0일 수 있으므로 GetDefault()를 기준으로 삼음
                currentSettings = GameSettings.GetDefault();
                Debug.Log("[SettingsManager] 기본 설정 적용");
            }

            ApplySettings();
        }

        public void SaveSettings()
        {
            string json = JsonUtility.ToJson(currentSettings);
            PlayerPrefs.SetString(SETTINGS_KEY, json);
            PlayerPrefs.SetString("language", currentSettings.language);
            PlayerPrefs.Save();
            Debug.Log("[SettingsManager] 설정 저장 완료");
        }

        public void ApplySettings()
        {
            // 오디오 적용
            ApplyAllVolumes();

            // 해상도 적용
            Screen.SetResolution(
                currentSettings.resolutionWidth,
                currentSettings.resolutionHeight,
                currentSettings.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed
            );

            // VSync 적용
            QualitySettings.vSyncCount = currentSettings.vsync ? 1 : 0;

            // 프레임레이트 제한
            Application.targetFrameRate = currentSettings.targetFrameRate;

            // 언어 적용
            ApplyLanguage();

            OnSettingsChanged?.Invoke(currentSettings);
            SaveSettings();
        }

        private void ApplyLanguage()
        {
            if (Enum.TryParse<Language>(currentSettings.language, out var lang))
                LocalizationManager.CurrentLanguage = lang;
        }

        public void ResetToDefault()
        {
            currentSettings = GameSettings.GetDefault();
            ApplySettings();
        }

        #endregion

        #region Audio Settings

        /// <summary>
        /// 0~1 슬라이더 값을 dB로 변환 (-80 ~ 0)
        /// </summary>
        private float LinearToDecibel(float linear)
        {
            if (linear <= 0.0001f) return -80f;
            return Mathf.Log10(linear) * 20f;
        }

        /// <summary>
        /// 모든 볼륨 적용
        /// </summary>
        private void ApplyAllVolumes()
        {
            ApplyMasterVolume();
            ApplyMusicVolume();
            ApplySFXVolume();
        }

        private void ApplyMasterVolume()
        {
            if (audioMixer == null) return;

            float volume = currentSettings.muted ? 0f : currentSettings.masterVolume;
            audioMixer.SetFloat(MASTER_VOLUME, LinearToDecibel(volume));
        }

        private void ApplyMusicVolume()
        {
            if (audioMixer == null) return;
            audioMixer.SetFloat(MUSIC_VOLUME, LinearToDecibel(currentSettings.musicVolume));
        }

        private void ApplySFXVolume()
        {
            if (audioMixer == null) return;
            audioMixer.SetFloat(SFX_VOLUME, LinearToDecibel(currentSettings.sfxVolume));
        }

        public void SetMasterVolume(float volume)
        {
            currentSettings.masterVolume = Mathf.Clamp01(volume);
            ApplyMasterVolume();
            SaveSettings();
            OnMasterVolumeChanged?.Invoke(currentSettings.masterVolume);
        }

        public void SetMusicVolume(float volume)
        {
            currentSettings.musicVolume = Mathf.Clamp01(volume);
            ApplyMusicVolume();
            SaveSettings();
            OnMusicVolumeChanged?.Invoke(currentSettings.musicVolume);
        }

        public void SetSFXVolume(float volume)
        {
            currentSettings.sfxVolume = Mathf.Clamp01(volume);
            ApplySFXVolume();
            SaveSettings();
            OnSFXVolumeChanged?.Invoke(currentSettings.sfxVolume);
        }

        public void SetMute(bool muted)
        {
            currentSettings.muted = muted;
            ApplyMasterVolume();
            SaveSettings();
        }

        #endregion

        #region Display Settings

        public void SetResolution(int width, int height)
        {
            currentSettings.resolutionWidth = width;
            currentSettings.resolutionHeight = height;
            SaveSettings(); // 저장만, 적용은 재시작 후
        }

        public void SetFullscreen(bool fullscreen)
        {
            currentSettings.fullscreen = fullscreen;
            ApplySettings();
        }

        public void SetVSync(bool enabled)
        {
            currentSettings.vsync = enabled;
            ApplySettings();
        }

        public void SetTargetFrameRate(int fps)
        {
            currentSettings.targetFrameRate = fps;
            ApplySettings();
        }

        #endregion

        #region Gameplay Settings

        public void SetScreenShake(bool enabled)
        {
            currentSettings.screenShake = enabled;
            SaveSettings();
        }

        public void SetAutoEndTurn(bool enabled)
        {
            currentSettings.autoEndTurn = enabled;
            SaveSettings();
        }

        public void SetGameSpeed(GameSpeed speed)
        {
            currentSettings.gameSpeed = speed;
            SaveSettings();
        }

        /// <summary>
        /// 현재 게임 속도 배율 반환 (1.0, 1.5, 2.0)
        /// </summary>
        public float GetGameSpeedMultiplier()
        {
            return currentSettings.gameSpeed switch
            {
                GameSpeed.Normal => 1.0f,
                GameSpeed.Fast => 1.5f,
                GameSpeed.VeryFast => 2.0f,
                _ => 1.0f
            };
        }

        public void SetCardConfirmation(bool enabled)
        {
            currentSettings.cardConfirmation = enabled;
            SaveSettings();
        }

        public void SetShowDamageNumbers(bool enabled)
        {
            currentSettings.showDamageNumbers = enabled;
            SaveSettings();
        }

        public void SetLanguage(string languageCode)
        {
            currentSettings.language = languageCode;
            SaveSettings(); // 저장만, 적용은 재시작 후
        }

        #endregion
    }

    #region Settings Data Classes

    [Serializable]
    public class GameSettings
    {
        [Header("Audio")]
        public float masterVolume = 1f;
        public float musicVolume = 0.7f;
        public float sfxVolume = 0.8f;
        public bool muted = false;
        public bool muteInBackground = true;

        [Header("Display")]
        public int resolutionWidth = 1920;
        public int resolutionHeight = 1080;
        public bool fullscreen = true;
        public bool vsync = true;
        public int targetFrameRate = 60;

        [Header("Gameplay")]
        public bool screenShake = true;
        public bool autoEndTurn = false;
        public GameSpeed gameSpeed = GameSpeed.Normal;
        public bool cardConfirmation = true;
        public bool showDamageNumbers = true;
        public string language = "ko";

        public GameSettings Clone()
        {
            return new GameSettings
            {
                masterVolume = this.masterVolume,
                musicVolume = this.musicVolume,
                sfxVolume = this.sfxVolume,
                muted = this.muted,
                muteInBackground = this.muteInBackground,
                resolutionWidth = this.resolutionWidth,
                resolutionHeight = this.resolutionHeight,
                fullscreen = this.fullscreen,
                vsync = this.vsync,
                targetFrameRate = this.targetFrameRate,
                screenShake = this.screenShake,
                autoEndTurn = this.autoEndTurn,
                gameSpeed = this.gameSpeed,
                cardConfirmation = this.cardConfirmation,
                showDamageNumbers = this.showDamageNumbers,
                language = this.language
            };
        }

        public static GameSettings GetDefault()
        {
            return new GameSettings
            {
                masterVolume      = 1f,
                musicVolume       = 1f,
                sfxVolume         = 1f,
                muted             = false,
                muteInBackground  = false,
                fullscreen        = false,
                vsync             = true,
                targetFrameRate   = 60,
                screenShake       = true,
                autoEndTurn       = false,
                gameSpeed         = GameSpeed.Normal,
                cardConfirmation  = true,
                showDamageNumbers = true,
                language          = "ko"
            };
        }
    }

    public enum GameSpeed
    {
        Normal = 0,   // 1x
        Fast = 1,     // 1.5x
        VeryFast = 2  // 2x
    }

    #endregion
}