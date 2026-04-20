using UnityEngine;
using TMPro;
using DeckRoguelike.Core;

namespace DeckRoguelike.Core
{
    /// <summary>
    /// TextMeshProUGUI에 stringcode를 바인딩해 언어 변경 시 자동 갱신.
    /// stringcode가 비어있으면 GameObject 이름을 코드로 사용.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizationBinder : MonoBehaviour
    {
        [SerializeField] private string stringCode;

        private TextMeshProUGUI label;

        private void Awake()
        {
            label = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            ApplyText();
            LocalizationManager.OnLanguageChanged += ApplyText;
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= ApplyText;
        }

        public void ApplyText()
        {
            string code = string.IsNullOrEmpty(stringCode) ? gameObject.name : stringCode;
            if (label == null) label = GetComponent<TextMeshProUGUI>();
            label.text = LocalizationManager.Get(code);
        }

        /// <summary>stringcode를 설정하고 즉시 텍스트를 갱신합니다.</summary>
        public void SetCode(string code)
        {
            stringCode = code;
            if (Application.isPlaying) ApplyText();
        }

        public string StringCode => string.IsNullOrEmpty(stringCode) ? gameObject.name : stringCode;
    }
}
