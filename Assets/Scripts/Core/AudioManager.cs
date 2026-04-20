using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DeckRoguelike.Core;

namespace DeckRoguelike.Audio
{
    /// <summary>
    /// 게임 전체 오디오를 관리하는 매니저
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource ambienceSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource uiSource;

        [Header("Music Tracks")]
        [SerializeField] private AudioClip mainMenuMusic;
        [SerializeField] private AudioClip mapMusic;
        [SerializeField] private AudioClip combatMusic;
        [SerializeField] private AudioClip bossMusic;
        [SerializeField] private AudioClip shopMusic;
        [SerializeField] private AudioClip victoryMusic;
        [SerializeField] private AudioClip defeatMusic;

        [Header("Settings")]
        [SerializeField] private float musicFadeDuration = 1f;
        [SerializeField] private int sfxPoolSize = 10;

        private List<AudioSource> sfxPool = new List<AudioSource>();
        private Coroutine musicFadeCoroutine;
        private float currentMusicVolume = 1f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeAudioSources();
                CreateSFXPool();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 설정 이벤트 구독
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnMasterVolumeChanged += OnMasterVolumeChanged;
                SettingsManager.Instance.OnMusicVolumeChanged += OnMusicVolumeChanged;
                SettingsManager.Instance.OnSFXVolumeChanged += OnSFXVolumeChanged;

                // 초기 볼륨 적용
                ApplyVolumeSettings();
            }
        }

        private void OnDestroy()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnMasterVolumeChanged -= OnMasterVolumeChanged;
                SettingsManager.Instance.OnMusicVolumeChanged -= OnMusicVolumeChanged;
                SettingsManager.Instance.OnSFXVolumeChanged -= OnSFXVolumeChanged;
            }
        }

        private void InitializeAudioSources()
        {
            if (musicSource == null)
            {
                musicSource = CreateAudioSource("MusicSource");
                musicSource.loop = true;
            }

            if (ambienceSource == null)
            {
                ambienceSource = CreateAudioSource("AmbienceSource");
                ambienceSource.loop = true;
            }

            if (sfxSource == null)
            {
                sfxSource = CreateAudioSource("SFXSource");
            }

            if (uiSource == null)
            {
                uiSource = CreateAudioSource("UISource");
            }
        }

        private AudioSource CreateAudioSource(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform);
            return go.AddComponent<AudioSource>();
        }

        private void CreateSFXPool()
        {
            for (int i = 0; i < sfxPoolSize; i++)
            {
                AudioSource source = CreateAudioSource($"SFXPool_{i}");
                sfxPool.Add(source);
            }
        }

        private void ApplyVolumeSettings()
        {
            if (SettingsManager.Instance == null) return;

            var settings = SettingsManager.Instance.CurrentSettings;
            float master = settings.masterVolume;

            musicSource.volume = settings.musicVolume * master;
            ambienceSource.volume = settings.musicVolume * master * 0.5f;
            sfxSource.volume = settings.sfxVolume * master;
            uiSource.volume = settings.sfxVolume * master;

            foreach (var source in sfxPool)
            {
                source.volume = settings.sfxVolume * master;
            }

            currentMusicVolume = settings.musicVolume * master;
        }

        #region Volume Callbacks

        private void OnMasterVolumeChanged(float volume)
        {
            ApplyVolumeSettings();
        }

        private void OnMusicVolumeChanged(float volume)
        {
            ApplyVolumeSettings();
        }

        private void OnSFXVolumeChanged(float volume)
        {
            ApplyVolumeSettings();
        }

        #endregion

        #region Music

        /// <summary>
        /// 음악 재생 (페이드 전환)
        /// </summary>
        public void PlayMusic(AudioClip clip, bool fade = true)
        {
            if (clip == null) return;

            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }

            if (fade && musicSource.isPlaying)
            {
                musicFadeCoroutine = StartCoroutine(FadeToNewMusic(clip));
            }
            else
            {
                musicSource.clip = clip;
                musicSource.volume = currentMusicVolume;
                musicSource.Play();
            }
        }

        private IEnumerator FadeToNewMusic(AudioClip newClip)
        {
            // 페이드 아웃
            float startVolume = musicSource.volume;
            float elapsed = 0f;

            while (elapsed < musicFadeDuration / 2f)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (musicFadeDuration / 2f));
                yield return null;
            }

            // 새 음악 설정
            musicSource.clip = newClip;
            musicSource.Play();

            // 페이드 인
            elapsed = 0f;
            while (elapsed < musicFadeDuration / 2f)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(0f, currentMusicVolume, elapsed / (musicFadeDuration / 2f));
                yield return null;
            }

            musicSource.volume = currentMusicVolume;
        }

        /// <summary>
        /// 게임 상태에 따른 음악 재생
        /// </summary>
        public void PlayMusicForState(GameState state)
        {
            AudioClip clip = state switch
            {
                GameState.MainMenu => mainMenuMusic,
                GameState.CharacterSelect => mainMenuMusic,
                GameState.Map => mapMusic,
                GameState.Combat => combatMusic,
                GameState.Shop => shopMusic,
                GameState.Victory => victoryMusic,
                GameState.GameOver => defeatMusic,
                _ => mapMusic
            };

            PlayMusic(clip);
        }

        public void PlayBossMusic()
        {
            PlayMusic(bossMusic);
        }

        public void StopMusic(bool fade = true)
        {
            if (fade)
            {
                StartCoroutine(FadeOutMusic());
            }
            else
            {
                musicSource.Stop();
            }
        }

        private IEnumerator FadeOutMusic()
        {
            float startVolume = musicSource.volume;
            float elapsed = 0f;

            while (elapsed < musicFadeDuration)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / musicFadeDuration);
                yield return null;
            }

            musicSource.Stop();
            musicSource.volume = currentMusicVolume;
        }

        #endregion

        #region SFX

        /// <summary>
        /// 효과음 재생
        /// </summary>
        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;

            AudioSource source = GetAvailableSFXSource();
            if (source != null)
            {
                source.PlayOneShot(clip, volumeScale);
            }
        }

        /// <summary>
        /// 위치 기반 효과음 재생
        /// </summary>
        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            if (clip == null) return;

            AudioSource source = GetAvailableSFXSource();
            if (source != null)
            {
                source.transform.position = position;
                source.spatialBlend = 1f;
                source.PlayOneShot(clip, volumeScale);
            }
        }

        /// <summary>
        /// UI 효과음 재생
        /// </summary>
        public void PlayUISound(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || uiSource == null) return;
            uiSource.PlayOneShot(clip, volumeScale);
        }

        private AudioSource GetAvailableSFXSource()
        {
            foreach (var source in sfxPool)
            {
                if (!source.isPlaying)
                {
                    return source;
                }
            }

            // 모든 소스가 사용 중이면 첫 번째 반환
            return sfxPool.Count > 0 ? sfxPool[0] : sfxSource;
        }

        #endregion

        #region Ambience

        /// <summary>
        /// 환경음 재생
        /// </summary>
        public void PlayAmbience(AudioClip clip, bool fade = true)
        {
            if (clip == null) return;

            if (fade && ambienceSource.isPlaying)
            {
                StartCoroutine(FadeToNewAmbience(clip));
            }
            else
            {
                ambienceSource.clip = clip;
                ambienceSource.Play();
            }
        }

        private IEnumerator FadeToNewAmbience(AudioClip newClip)
        {
            float startVolume = ambienceSource.volume;
            float targetVolume = startVolume;
            float elapsed = 0f;

            while (elapsed < musicFadeDuration / 2f)
            {
                elapsed += Time.deltaTime;
                ambienceSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (musicFadeDuration / 2f));
                yield return null;
            }

            ambienceSource.clip = newClip;
            ambienceSource.Play();

            elapsed = 0f;
            while (elapsed < musicFadeDuration / 2f)
            {
                elapsed += Time.deltaTime;
                ambienceSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / (musicFadeDuration / 2f));
                yield return null;
            }
        }

        public void StopAmbience()
        {
            ambienceSource.Stop();
        }

        #endregion
    }
}
