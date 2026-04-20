using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 이펙트 아이콘 한 개 (아이콘 Image + 수치 TMP).
    /// effectIconPrefab에 붙여서 사용합니다.
    /// </summary>
    public class EffectIconEntry : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI valueText;

        public string Key { get; private set; }

        private bool _showPlusSign = true;

        /// <summary>
        /// 아이콘 슬롯을 초기화합니다.
        /// showPlusSign = false 이면 양수 값도 '+' 없이 숫자만 표시합니다 (공격 슬롯 전용).
        /// </summary>
        public void Initialize(string key, Sprite sprite, int value, bool showPlusSign = true)
        {
            Key = key;
            _showPlusSign = showPlusSign;
            if (iconImage != null) iconImage.sprite = sprite;
            UpdateValue(value);
        }

        public void UpdateValue(int value)
        {
            if (valueText != null)
                valueText.text = (value > 0 && _showPlusSign) ? $"+{value}" : value.ToString();
        }
    }
}
