using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DeckRoguelike.UI
{
    public class RewardRowUI : MonoBehaviour
    {
        [SerializeField] private Button           claimButton;
        [SerializeField] private Image            iconImage;
        [SerializeField] private TextMeshProUGUI  labelText;

        private void Awake()
        {
            if (claimButton == null)
                claimButton = GetComponent<Button>() ?? GetComponentInChildren<Button>(true);

            if (claimButton == null) return;

            var btnT = claimButton.transform;

            if (iconImage == null)
            {
                var t = btnT.Find("Icon");
                iconImage = t != null ? t.GetComponent<Image>() : null;
            }

            if (labelText == null)
            {
                var t = btnT.Find("Label");
                labelText = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
            }
        }

        public void SetIcon(Sprite sprite)
        {
            if (iconImage == null || sprite == null) return;
            iconImage.sprite  = sprite;
            iconImage.enabled = true;
        }

        public void SetText(string text)
        {
            if (labelText != null) labelText.text = text;
        }

        public void AddClaimListener(UnityEngine.Events.UnityAction callback)
        {
            if (claimButton != null)
                claimButton.onClick.AddListener(callback);
        }

        public void SetButtonInteractable(bool interactable)
        {
            if (claimButton != null) claimButton.interactable = interactable;
        }

        public void SetClaimed()
        {
            if (claimButton != null) claimButton.interactable = false;
            if (labelText   != null) labelText.text = "획득 완료";
        }

        public void DestroySelf() => Destroy(gameObject);
    }
}
