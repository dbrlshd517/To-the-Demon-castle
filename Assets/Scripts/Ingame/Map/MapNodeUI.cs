using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace DeckRoguelike.UI
{
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(Image))]
    public class MapNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("=== Node Icons ===")]
        [SerializeField] private Sprite startIcon;
        [SerializeField] private Sprite combatIcon;
        [SerializeField] private Sprite eliteIcon;
        [SerializeField] private Sprite bossIcon;
        [SerializeField] private Sprite restIcon;
        [SerializeField] private Sprite shopIcon;
        [SerializeField] private Sprite treasureIcon;
        [SerializeField] private Sprite eventIcon;
        [SerializeField] private Sprite visitedIcon;

        [Header("=== Colors ===")]
        [SerializeField] private Color normalColor = new Color(0.25f, 0.25f, 0.35f);
        [SerializeField] private Color accessibleColor = Color.white;
        [SerializeField] private Color visitedColor = new Color(0.4f, 0.4f, 0.4f);
        [SerializeField] private Color hoverColor = new Color(1f, 0.9f, 0.5f);

        [Header("=== Animation ===")]
        [SerializeField] private float hoverScale = 1.15f;
        [SerializeField] private float pulseSpeed = 2f;

        private MapNodeData nodeData;
        private Button button;
        private Image image;
        private Vector3 originalScale;

        public event Action OnClicked;

        private void Awake()
        {
            button = GetComponent<Button>();
            image = GetComponent<Image>();
            originalScale = transform.localScale;
            button.onClick.AddListener(HandleClick);
        }

        private void Update()
        {
            if (nodeData != null && nodeData.IsAccessible && !nodeData.IsVisited)
            {
                float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
                image.color = Color.Lerp(accessibleColor, hoverColor, t);
            }
        }

        public void Initialize(MapNodeData data)
        {
            nodeData = data;
            UpdateVisual();
        }

        public void UpdateVisual()
        {
            if (nodeData == null) return;

            if (nodeData.IsVisited && visitedIcon != null)
                image.sprite = visitedIcon;
            else if (nodeData.IsStart)
                image.sprite = startIcon;
            else
                image.sprite = GetIcon(nodeData.Type);

            if (nodeData.IsVisited)
                image.color = visitedColor;
            else if (nodeData.IsAccessible)
                image.color = accessibleColor;
            else
                image.color = normalColor;

            transform.localScale = originalScale;
            button.interactable = nodeData.IsAccessible && !nodeData.IsVisited;
        }

        private Sprite GetIcon(NodeType type)
        {
            return type switch
            {
                NodeType.Combat => combatIcon,
                NodeType.Elite => eliteIcon,
                NodeType.Boss => bossIcon,
                NodeType.Rest => restIcon,
                NodeType.Shop => shopIcon,
                NodeType.Treasure => treasureIcon,
                NodeType.Event => eventIcon,
                _ => null
            };
        }

        private void HandleClick()
        {
            if (nodeData == null || !nodeData.IsAccessible || nodeData.IsVisited) return;
            OnClicked?.Invoke();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (nodeData == null || nodeData.IsVisited || !nodeData.IsAccessible) return;
            transform.localScale = originalScale * hoverScale;
            image.color = hoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = originalScale;
            UpdateVisual();
        }

        private void OnDestroy()
        {
            button?.onClick.RemoveListener(HandleClick);
        }
    }
}
