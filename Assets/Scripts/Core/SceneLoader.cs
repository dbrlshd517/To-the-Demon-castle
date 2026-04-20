using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

namespace DeckRoguelike.Core
{
    /// <summary>
    /// 씬 전환 및 로딩 화면을 관리하는 매니저
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float minimumLoadTime = 0.5f;
        [SerializeField] private CanvasGroup loadingScreen;
        [SerializeField] private UnityEngine.UI.Slider progressBar;

        public event Action<string> OnSceneLoadStarted;
        public event Action<string> OnSceneLoadCompleted;
        public event Action<float> OnLoadProgressChanged;

        public bool IsLoading { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void LoadScene(string sceneName)
        {
            if (IsLoading) return;
            StartCoroutine(LoadSceneAsync(sceneName));
        }

        public void LoadScene(int sceneIndex)
        {
            if (IsLoading) return;
            string sceneName = SceneManager.GetSceneByBuildIndex(sceneIndex).name;
            StartCoroutine(LoadSceneAsync(sceneName));
        }

        private IEnumerator LoadSceneAsync(string sceneName)
        {
            IsLoading = true;
            OnSceneLoadStarted?.Invoke(sceneName);

            // 로딩 화면 페이드 인
            if (loadingScreen != null)
            {
                yield return StartCoroutine(FadeLoadingScreen(true));
            }

            float startTime = Time.realtimeSinceStartup;

            // 비동기 씬 로드
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            asyncLoad.allowSceneActivation = false;

            while (!asyncLoad.isDone)
            {
                float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
                OnLoadProgressChanged?.Invoke(progress);

                if (progressBar != null)
                {
                    progressBar.value = progress;
                }

                // 로딩 완료 체크
                if (asyncLoad.progress >= 0.9f)
                {
                    // 최소 로딩 시간 보장
                    float elapsedTime = Time.realtimeSinceStartup - startTime;
                    if (elapsedTime < minimumLoadTime)
                    {
                        yield return new WaitForSecondsRealtime(minimumLoadTime - elapsedTime);
                    }

                    asyncLoad.allowSceneActivation = true;
                }

                yield return null;
            }

            // 로딩 화면 페이드 아웃
            if (loadingScreen != null)
            {
                yield return StartCoroutine(FadeLoadingScreen(false));
            }

            IsLoading = false;
            OnSceneLoadCompleted?.Invoke(sceneName);
        }

        private IEnumerator FadeLoadingScreen(bool fadeIn)
        {
            float targetAlpha = fadeIn ? 1f : 0f;
            float startAlpha = loadingScreen.alpha;
            float duration = 0.3f;
            float elapsed = 0f;

            loadingScreen.gameObject.SetActive(true);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                loadingScreen.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            loadingScreen.alpha = targetAlpha;

            if (!fadeIn)
            {
                loadingScreen.gameObject.SetActive(false);
            }
        }

        public void ReloadCurrentScene()
        {
            LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
