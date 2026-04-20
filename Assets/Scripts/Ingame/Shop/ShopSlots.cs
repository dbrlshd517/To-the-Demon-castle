using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeckRoguelike.Cards;
using DeckRoguelike.Relic;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 상점 카드 슬롯.
    /// CardUI를 cardContainer에 생성하고, 클릭 시 구매 콜백을 호출합니다.
    ///
    /// [프리팹 구조]
    /// ShopCardSlot
    ///  ├─ CardContainer  (Transform)         ← cardContainer
    ///  ├─ PriceText      (TextMeshProUGUI)   ← priceText
    ///  └─ SoldOutOverlay (GameObject)        ← soldOutOverlay
    /// </summary>
    public class ShopCardSlot : MonoBehaviour
    {
        [SerializeField] private Transform  cardContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private GameObject soldOutOverlay;

        private bool isSold;
        private CardData cardData;
        private int price;
        private System.Action<CardData, int, ShopCardSlot> onBuy;

        public void Initialize(CardData card, int cardPrice,
                               System.Action<CardData, int, ShopCardSlot> buyCallback)
        {
            cardData = card;
            price    = cardPrice;
            onBuy    = buyCallback;
            isSold   = false;

            if (priceText != null) priceText.text = $"{price}G";
            soldOutOverlay?.SetActive(false);

            if (cardPrefab != null && cardContainer != null)
            {
                var obj    = Instantiate(cardPrefab, cardContainer);
                var cardUI = obj.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    cardUI.Initialize(card);
                    cardUI.IsRuntimeUnplayable = true;
                    cardUI.OnCardViewClicked += _ => TryBuy();
                }
            }
        }

        public void SetSoldOut()
        {
            isSold = true;
            soldOutOverlay?.SetActive(true);
        }

        private void TryBuy()
        {
            if (isSold) return;
            onBuy?.Invoke(cardData, price, this);
        }
    }

    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 상점 유물 슬롯.
    /// 유물 아이콘, 이름, 가격을 표시하고 BuyButton으로 구매합니다.
    ///
    /// [프리팹 구조]
    /// ShopRelicSlot
    ///  ├─ RelicIcon      (Image)             ← relicIcon
    ///  ├─ RelicName      (TextMeshProUGUI)   ← relicNameText
    ///  ├─ PriceText      (TextMeshProUGUI)   ← priceText
    ///  ├─ BuyButton      (Button)            ← buyButton
    ///  └─ SoldOutOverlay (GameObject)        ← soldOutOverlay
    /// </summary>
    public class ShopRelicSlot : MonoBehaviour
    {
        [SerializeField] private Image relicIcon;
        [SerializeField] private TextMeshProUGUI relicNameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private Button buyButton;
        [SerializeField] private GameObject soldOutOverlay;

        private bool isSold;
        private RelicData relicData;
        private int price;
        private System.Action<RelicData, int, ShopRelicSlot> onBuy;

        private void Awake()
        {
            buyButton?.onClick.AddListener(TryBuy);
        }

        public void Initialize(RelicData data, int relicPrice,
                               System.Action<RelicData, int, ShopRelicSlot> buyCallback)
        {
            relicData = data;
            price     = relicPrice;
            onBuy     = buyCallback;
            isSold    = false;

            if (relicIcon != null)
            {
                relicIcon.sprite  = data.icon;
                relicIcon.enabled = data.icon != null;
            }
            if (relicNameText != null) relicNameText.text = data.relicName;
            if (priceText != null)     priceText.text     = $"{price}G";
            soldOutOverlay?.SetActive(false);
        }

        public void SetSoldOut()
        {
            isSold = true;
            soldOutOverlay?.SetActive(true);
        }

        private void TryBuy()
        {
            if (isSold) return;
            onBuy?.Invoke(relicData, price, this);
        }
    }
}
