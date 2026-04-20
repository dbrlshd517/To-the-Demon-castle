using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DeckRoguelike.Core;
using DeckRoguelike.Relic;
using Loc = DeckRoguelike.Core.LocalizationManager;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 유물 상세 패널 컨트롤러.
    /// 보유 유물을 좌/우 버튼으로 하나씩 탐색합니다.
    /// closeOverlay(전체화면 투명 버튼)를 클릭하면 닫힙니다.
    ///
    /// [Unity 설정]
    /// 1. relicPanel 오브젝트에 이 스크립트를 붙입니다.
    /// 2. closeOverlay: relicPanel 직속 자식으로 전체화면 투명 Button 오브젝트 (Image alpha=0, Raycast On)
    /// 3. 콘텐츠 오브젝트(relicImage, texts, nav buttons)는 closeOverlay보다 높은 sibling index에 위치
    /// </summary>
    public class RelicPanelController : MonoBehaviour
    {
        [Header("=== 유물 표시 ===")]
        [SerializeField] private Image relicImage;
        [SerializeField] private TextMeshProUGUI relicNameText;
        [SerializeField] private TextMeshProUGUI relicRarityText;
        [SerializeField] private TextMeshProUGUI relicDescText;

        [Header("=== 내비게이션 ===")]
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;

        [Header("=== 외부 클릭 닫기 ===")]
        [SerializeField] private Button closeOverlay; // 패널 뒤 전체화면 투명 버튼

        private int currentIndex = 0;

        private void Awake()
        {
            leftButton?.onClick.AddListener(NavigateLeft);
            rightButton?.onClick.AddListener(NavigateRight);
            closeOverlay?.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        private void OnEnable()  => LocalizationManager.OnLanguageChanged += DisplayCurrent;
        private void OnDisable() => LocalizationManager.OnLanguageChanged -= DisplayCurrent;

        // ──────────────────────────────────────────────
        #region Public API

        public void Toggle()
        {
            if (gameObject.activeSelf) Close();
            else Open();
        }

        public void Open()
        {
            var relics = GetRelics();
            if (relics.Count == 0) return;

            // 가장 최근에 추가된 유물부터 표시
            currentIndex = relics.Count - 1;
            gameObject.SetActive(true);
            DisplayCurrent();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Navigation

        private void NavigateLeft()
        {
            currentIndex = Mathf.Max(0, currentIndex - 1);
            DisplayCurrent();
        }

        private void NavigateRight()
        {
            currentIndex = Mathf.Min(GetRelics().Count - 1, currentIndex + 1);
            DisplayCurrent();
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Display

        private void DisplayCurrent()
        {
            var relics = GetRelics();
            if (relics.Count == 0)
            {
                Close();
                return;
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, relics.Count - 1);
            var relic = relics[currentIndex];
            if (relic?.Data == null) return;

            if (relicImage != null)
            {
                Sprite icon = relic.Data.icon;
                if (icon == null && relic.Data.relicCode > 0)
                    icon = Addressables.LoadAssetAsync<Sprite>($"Sprites/Relic/{relic.Data.relicCode}").WaitForCompletion();
                relicImage.sprite  = icon;
                relicImage.enabled = icon != null;
            }

            if (relicNameText != null)
                relicNameText.text = Loc.Get($"relic_name_{relic.Data.relicCode}");

            if (relicRarityText != null)
                relicRarityText.text = Loc.Get(relic.Data.IsBossRelic ? "relic_type_boss" : "relic_type_normal");

            if (relicDescText != null)
                relicDescText.text = relic.Data.description;

            RefreshNavButtons(relics.Count);
        }

        private void RefreshNavButtons(int total)
        {
            // 첫 번째 유물이면 왼쪽 버튼 숨김
            if (leftButton != null)
            {
                bool canLeft = currentIndex > 0;
                leftButton.interactable = canLeft;
                leftButton.gameObject.SetActive(canLeft);
            }

            // 마지막 유물이면 오른쪽 버튼 숨김
            if (rightButton != null)
            {
                bool canRight = currentIndex < total - 1;
                rightButton.interactable = canRight;
                rightButton.gameObject.SetActive(canRight);
            }
        }

        #endregion

        // ──────────────────────────────────────────────
        #region Helpers

        private IReadOnlyList<RelicEffect> GetRelics()
        {
            return GameManager.Instance?.Relics ?? new System.Collections.Generic.List<RelicEffect>();
        }


        #endregion
    }
}
