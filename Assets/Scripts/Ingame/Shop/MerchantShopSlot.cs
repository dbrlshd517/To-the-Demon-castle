using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DeckRoguelike.Core;
using DeckRoguelike.Cards;
using DeckRoguelike.Relic;
using DeckRoguelike.Item;
using DeckRoguelike.Combat;
using static DeckRoguelike.Core.LocalizationManager;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 상인 주변 8칸 중 하나에 배치되는 상점 슬롯 프리팹 컨트롤러.
    ///
    /// [Unity 구조]
    /// MerchantShopSlot (MerchantShopSlot + Button + TooltipTrigger)
    ///  ├─ IconImage      (Image)              ← iconImage
    ///  ├─ PriceText      (TextMeshProUGUI)    ← priceText
    ///  └─ SoldOutOverlay (GameObject)         ← soldOutOverlay
    /// </summary>
    public class MerchantShopSlot : MonoBehaviour
    {
        public enum SlotType { ClassCard, NeutralCard, Item, Relic }

        [SerializeField] private Image           iconImage;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private GameObject      soldOutOverlay;

        private static readonly Color NormalPriceColor   = Color.white;
        private static readonly Color DiscountPriceColor = Color.red;

        private SlotType  slotType;
        private int       price;

        private CardData  cardData;
        private ItemData  itemData;
        private RelicData relicData;

        // ─────────────────────────────────────────────
        // 자동 컴포넌트 탐색 (SerializeField 미연결 시 대비)
        // ─────────────────────────────────────────────
        private void Awake()
        {
            EnsureComponents();
        }

        /// <summary>필수 컴포넌트가 없으면 직접 생성합니다.</summary>
        private void EnsureComponents()
        {
            // 루트 배경 Image
            var bgImg = GetComponent<Image>();
            if (bgImg == null)
                bgImg = gameObject.AddComponent<Image>();
            if (bgImg.color == Color.white)
                bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            // 루트 Button
            var btn = GetComponent<Button>();
            if (btn == null) btn = gameObject.AddComponent<Button>();

            // IconImage — 루트 제외 첫 번째 Image 탐색, 없으면 생성
            if (iconImage == null)
            {
                foreach (var img in GetComponentsInChildren<Image>(true))
                {
                    if (img.gameObject != gameObject) { iconImage = img; break; }
                }
            }
            if (iconImage == null)
            {
                var go = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.1f, 0.3f);
                rt.anchorMax = new Vector2(0.9f, 1f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                iconImage = go.GetComponent<Image>();
                iconImage.raycastTarget = false;
            }

            // PriceText — TextMeshProUGUI 탐색, 없으면 생성
            if (priceText == null)
                priceText = GetComponentInChildren<TextMeshProUGUI>(true);

            if (priceText == null)
            {
                var go = new GameObject("PriceText", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0.3f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                priceText = go.GetComponent<TextMeshProUGUI>();
                priceText.alignment   = TMPro.TextAlignmentOptions.Center;
                priceText.fontSize    = 14f;
                priceText.raycastTarget = false;
            }

            // SoldOutOverlay — 없으면 생성
            if (soldOutOverlay == null)
            {
                var t = transform.Find("SoldOutOverlay");
                soldOutOverlay = t != null ? t.gameObject : null;
            }
            if (soldOutOverlay == null)
            {
                soldOutOverlay = new GameObject("SoldOutOverlay", typeof(RectTransform), typeof(Image));
                soldOutOverlay.transform.SetParent(transform, false);
                var rt = soldOutOverlay.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var img = soldOutOverlay.GetComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.6f);
                img.raycastTarget = false;
                soldOutOverlay.SetActive(false);
            }
        }

        // ─────────────────────────────────────────────
        // 초기화
        // ─────────────────────────────────────────────

        public void InitializeClassCard(CardData card, int finalPrice, bool discounted)
        {
            slotType = SlotType.ClassCard;
            cardData = card;
            SetupUI(card?.cardArt, finalPrice, discounted);
            if (card != null)
                SetTooltip(card.cardName, card.description);
        }

        public void InitializeNeutralCard(CardData card, int finalPrice, bool discounted)
        {
            slotType = SlotType.NeutralCard;
            cardData = card;
            SetupUI(card?.cardArt, finalPrice, discounted);
            if (card != null)
                SetTooltip(card.cardName, card.description);
        }

        public void InitializeItem(ItemData item, int finalPrice, bool discounted)
        {
            slotType = SlotType.Item;
            itemData = item;
            SetupUI(item?.icon, finalPrice, discounted);
            if (item != null)
                SetTooltip(item.itemName, item.description);
        }

        public void InitializeRelic(RelicData relic, int finalPrice, bool discounted)
        {
            slotType  = SlotType.Relic;
            relicData = relic;
            SetupUI(relic?.icon, finalPrice, discounted);
            if (relic != null)
                SetTooltip(relic.relicName, relic.description);
        }

        private void SetupUI(Sprite icon, int finalPrice, bool discounted)
        {
            price = finalPrice;

            // 자식 오브젝트 활성화 (프리팹에서 비활성으로 저장됐을 경우 대비)
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(true);
                iconImage.sprite  = icon != null ? icon : null;
                iconImage.color   = icon != null ? Color.white : Color.gray;
                iconImage.enabled = true;
            }

            if (priceText != null)
            {
                priceText.gameObject.SetActive(true);
                priceText.text  = $"{finalPrice}G";
                priceText.color = discounted ? DiscountPriceColor : NormalPriceColor;
            }

            if (soldOutOverlay != null) soldOutOverlay.SetActive(false);

            // 버튼 리스너 (중복 방지)
            var btn = GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = true;
                btn.onClick.RemoveListener(OnBuyClicked);
                btn.onClick.AddListener(OnBuyClicked);
            }
        }

        private void SetTooltip(string title, string desc)
        {
            var trigger = GetComponent<TooltipTrigger>() ?? gameObject.AddComponent<TooltipTrigger>();
            trigger.SetDynamicEntries(new List<TooltipEntry> { TooltipEntry.ObjectUI(title, desc) });
        }

        // ─────────────────────────────────────────────
        // 구매
        // ─────────────────────────────────────────────

        private void OnBuyClicked()
        {
            if (GameManager.Instance == null) return;
            if (!GameManager.Instance.SpendGold(price))
            {
                Debug.Log("[MerchantShopSlot] 골드 부족");
                return;
            }

            switch (slotType)
            {
                case SlotType.ClassCard:
                case SlotType.NeutralCard:
                    if (cardData != null)
                    {
                        var captured = cardData;
                        // CardRewardPanel에 구매한 카드 1장 표시 후 덱에 추가
                        InGameUIController.Instance?.OpenCardRewardWithCard(captured, _ =>
                        {
                            DeckManager.Instance?.AddCardToDeck(captured);
                            Debug.Log($"[MerchantShopSlot] 카드 획득: {captured.cardName} (-{price}G)");
                        });
                    }
                    break;

                case SlotType.Item:
                    if (itemData != null)
                    {
                        GameManager.Instance.AddItem(itemData);
                        Debug.Log($"[MerchantShopSlot] 아이템 구매: {itemData.itemName} (-{price}G)");
                    }
                    break;

                case SlotType.Relic:
                    if (relicData != null)
                    {
                        GameManager.Instance.AddRelic(relicData.relicCode);
                        Debug.Log($"[MerchantShopSlot] 유물 구매: {relicData.relicName} (-{price}G)");
                    }
                    break;
            }

            // 카드는 CardRewardPanel 닫힌 후 슬롯이 사라지도록 콜백에서 처리하지 않고 즉시 제거
            // (패널이 열린 동안 슬롯이 보이지 않아도 무방)
            gameObject.SetActive(false);
        }
    }
}
