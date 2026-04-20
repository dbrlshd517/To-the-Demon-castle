using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DeckRoguelike.Cards;
using DeckRoguelike.Core;
using DeckRoguelike.Relic;
using DeckRoguelike.Item;
using Random = UnityEngine.Random;
using DeckRoguelike.Combat;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 상점 패널 컨트롤러. Slay the Spire 스타일.
    /// 골드 표시는 Top Bar의 기존 goldText를 사용합니다.
    /// 떠나기는 Back Button으로 처리합니다 (InGameUIController.OnBackClicked).
    ///
    /// [레이아웃]
    ///  ┌──────────────────────────────────────────────────────┐
    ///  │  [중립1] [중립2] [중립3]   |   [직업1] [직업2] [직업3] │  ← neutralCardSlotsContainer + classCardSlotsContainer
    ///  │  [Relic1] [Relic2]  [Item1] [Item2]  [카드제거 XG]   │  ← relicSlotsContainer + itemSlotsContainer + removeCardButton
    ///  └──────────────────────────────────────────────────────┘
    ///
    /// [Unity 구조]
    /// ShopPanel  (ShopController)
    ///  ├─ CommonSlotsContainer (Transform, HorizontalLayoutGroup) ← commonCardSlotsContainer  (ClassDigit=1 중립 카드)
    ///  ├─ ClassCardSlotsContainer   (Transform, HorizontalLayoutGroup) ← classCardSlotsContainer   (ClassDigit=2/3/4 직업 카드)
    ///  ├─ BottomRow
    ///  │   ├─ RelicSlotsContainer  (Transform, HorizontalLayoutGroup)  ← relicSlotsContainer
    ///  │   ├─ ItemSlotsContainer   (Transform, HorizontalLayoutGroup)  ← itemSlotsContainer
    ///  │   └─ RemoveCardButton     (Button)                            ← removeCardButton
    ///  │       └─ PriceText        (TextMeshProUGUI)                   ← removeCardPriceText
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [Header("=== 카드 슬롯 컨테이너 ===")]
        [Tooltip("공통 카드 컨테이너 — ClassDigit=1(중립) 카드 전용. Unity 오브젝트 'CommonSlotsContainer'를 연결하세요.")]
        [SerializeField] private Transform commonCardSlotsContainer;
        [Tooltip("직업 카드 컨테이너 — ClassDigit=2/3/4(전사/거너/메이지) 카드 전용. Unity 오브젝트 'ClassCardSlotsContainer'를 연결하세요.")]
        [SerializeField] private Transform classCardSlotsContainer;

        [Header("=== 기타 슬롯 컨테이너 ===")]
        [SerializeField] private Transform relicSlotsContainer;
        [SerializeField] private Transform itemSlotsContainer;

        [Header("=== 프리팹 ===")]
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private GameObject relicShopSlotPrefab;
        [SerializeField] private GameObject itemShopSlotPrefab;

        [Header("=== 카드 제거 ===")]
        [Tooltip("카드 제거 버튼 (가격은 GameManager.CardRemovalPrice 기준)")]
        [SerializeField] private Button removeCardButton;
        [Tooltip("버튼 위에 표시되는 가격 텍스트")]
        [SerializeField] private TextMeshProUGUI removeCardPriceText;
        [Tooltip("CardListController (Sub 모드) 연결 (Remove 모드로 열림)")]
        [SerializeField] private CardListController subCardListController;
        [Tooltip("카드 제거 확인 패널")]
        [SerializeField] private CardConfirmPanel cardConfirmPanel;

        [Header("=== 설정 ===")]
        [SerializeField] private int commonCardCount = 3;
        [SerializeField] private int classCardCount   = 3;
        [SerializeField] private int relicCount       = 2;
        [SerializeField] private int itemCount        = 2;

        // 희귀도별 카드 기본 가격
        private static readonly int[] CardBasePrices = { 50, 75, 150, 300 }; // Common / Uncommon / Rare / Legendary

        // 희귀도별 유물 가격 범위 [min, max] — Common / Uncommon / Rare
        private static readonly (int min, int max)[] RelicPriceRanges =
        {
            (143, 157),   // Common
            (238, 262),   // Uncommon
            (285, 315),   // Rare
        };

        // 희귀도별 아이템 가격 범위 [min, max] — Common / Uncommon / Rare
        private static readonly (int min, int max)[] ItemPriceRanges =
        {
            ( 48,  52),   // Common
            ( 72,  78),   // Uncommon
            ( 95, 105),   // Rare
        };

        private System.Action onLeave;
        private int _pendingRemovalPrice;

        private void Start()
        {
            removeCardButton?.onClick.AddListener(OnRemoveCardClicked);
        }

        private void OnEnable()  => InGameUIController.Instance?.PushPanel("Shop", Close);
        private void OnDisable() => InGameUIController.Instance?.UnregisterPanel("Shop");

        // ─────────────────────────────────────────
        // 외부 호출 진입점
        // ─────────────────────────────────────────

        public void OpenShop(System.Action onLeaveCallback = null)
        {
            onLeave = onLeaveCallback;
            GenerateShopItems();
            RefreshRemoveCardButton();
        }

        // ─────────────────────────────────────────
        #region Generation

        private void GenerateShopItems()
        {
            GenerateCardSlots();
            GenerateRelicSlots();
            GenerateItemSlots();
        }

        /// <summary>
        /// 공통 카드(ClassDigit=1)와 직업 카드(ClassDigit=2/3/4)를 분리해 각각의 컨테이너에 생성합니다.
        /// CardData 규칙: ClassDigit 1=중립(공통), 2=전사, 3=거너, 4=메이지
        /// </summary>
        private void GenerateCardSlots()
        {
            if (commonCardSlotsContainer != null)
                foreach (Transform child in commonCardSlotsContainer) Destroy(child.gameObject);
            if (classCardSlotsContainer != null)
                foreach (Transform child in classCardSlotsContainer) Destroy(child.gameObject);

            var character = GameManager.Instance?.SelectedCharacter ?? CharacterType.Warrior;
            var pool      = CardRegistry.GetRewardPool(character);

            var commonPool = new List<CardData>();
            var classPool  = new List<CardData>();
            foreach (var card in pool)
            {
                if (card.ClassDigit == 1) commonPool.Add(card);  // 중립(공통) 카드
                else                      classPool.Add(card);   // 직업 전용 카드
            }

            Shuffle(commonPool);
            Shuffle(classPool);

            SpawnCardGroup(commonCardSlotsContainer, commonPool, commonCardCount);
            SpawnCardGroup(classCardSlotsContainer,  classPool,  classCardCount);
        }

        private void SpawnCardGroup(Transform container, List<CardData> pool, int maxCount)
        {
            if (container == null || cardPrefab == null) return;

            int discountIdx = UnityEngine.Random.Range(0, Mathf.Max(1, maxCount));
            int count       = Mathf.Min(maxCount, pool.Count);

            for (int i = 0; i < count; i++)
            {
                var  card       = pool[i];
                int  rarityIdx  = Mathf.Clamp((int)card.Rarity, 0, CardBasePrices.Length - 1);
                bool discounted = (i == discountIdx);
                int  price      = Mathf.Max(1, discounted
                    ? CardBasePrices[rarityIdx] / 2
                    : CardBasePrices[rarityIdx]);

                SpawnCardEntry(container, card, price, discounted);
            }
        }

        private void SpawnCardEntry(Transform container, CardData card, int price, bool discounted)
        {
            var wrapper = new GameObject("CardEntry", typeof(RectTransform));
            wrapper.transform.SetParent(container, false);

            var vlg = wrapper.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment         = TextAnchor.UpperCenter;
            vlg.spacing                = 6f;
            vlg.childForceExpandWidth  = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;

            var csf = wrapper.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            var cardObj = Instantiate(cardPrefab, wrapper.transform);
            var cardUI  = cardObj.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.Initialize(card);
                cardUI.IsRuntimeUnplayable = true;
            }
            var cardLE = cardObj.GetComponent<LayoutElement>() ?? cardObj.AddComponent<LayoutElement>();
            var cardRT = cardObj.GetComponent<RectTransform>();
            if (cardLE.preferredWidth  <= 0) cardLE.preferredWidth  = cardRT.sizeDelta.x > 0 ? cardRT.sizeDelta.x : 120f;
            if (cardLE.preferredHeight <= 0) cardLE.preferredHeight = cardRT.sizeDelta.y > 0 ? cardRT.sizeDelta.y : 280f;

            var overlay = new GameObject("SoldOut", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(cardObj.transform, false);
            var overlayRT = overlay.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = overlayRT.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            overlay.SetActive(false);

            var priceGo  = new GameObject("PriceText", typeof(RectTransform), typeof(TextMeshProUGUI));
            priceGo.transform.SetParent(wrapper.transform, false);
            var priceTMP = priceGo.GetComponent<TextMeshProUGUI>();
            priceTMP.text      = $"{price}G";
            priceTMP.color     = discounted ? new Color(1f, 0.35f, 0.35f) : Color.white;
            priceTMP.fontSize  = 16f;
            priceTMP.alignment = TextAlignmentOptions.Center;
            var priceLE = priceGo.AddComponent<LayoutElement>();
            priceLE.preferredWidth  = 120f;
            priceLE.preferredHeight = 24f;

            if (cardUI != null)
            {
                bool isSold = false;
                cardUI.OnCardViewClicked += _ =>
                {
                    if (isSold) return;
                    if (GameManager.Instance == null || !GameManager.Instance.SpendGold(price))
                    {
                        Debug.Log("[ShopController] 골드 부족");
                        return;
                    }
                    isSold = true;
                    overlay.SetActive(true);
                    DeckManager.Instance?.AddCardToDeck(card);
                    Debug.Log($"[ShopController] 카드 획득: {card.cardName} (-{price}G)");
                };
            }
        }

        private void GenerateRelicSlots()
        {
            if (relicSlotsContainer == null || relicShopSlotPrefab == null) return;
            foreach (Transform child in relicSlotsContainer) Destroy(child.gameObject);

            RelicLibrary.RegisterAll();
            ItemLibrary.RegisterAll();
            var pool = RelicRegistry.GetByType(bossRelic: false);

            if (GameManager.Instance != null)
            {
                var owned = new HashSet<int>();
                foreach (var r in GameManager.Instance.Relics)
                    if (r?.Data != null) owned.Add(r.Data.relicCode);
                pool.RemoveAll(r => owned.Contains(r.relicCode));
            }

            Shuffle(pool);
            int count = Mathf.Min(relicCount, pool.Count);
            for (int i = 0; i < count; i++)
            {
                var relic = pool[i];
                int price = RollRelicPrice(relic);
                SpawnRelicEntry(relic, price);
            }
        }

        private void SpawnRelicEntry(RelicData relic, int price)
        {
            var wrapper = new GameObject("RelicEntry", typeof(RectTransform));
            wrapper.transform.SetParent(relicSlotsContainer, false);
            var vlg = wrapper.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment         = TextAnchor.UpperCenter;
            vlg.spacing                = 6f;
            vlg.childForceExpandWidth  = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            var csf = wrapper.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            var slotObj = Instantiate(relicShopSlotPrefab, wrapper.transform);
            var slotLE  = slotObj.GetComponent<LayoutElement>() ?? slotObj.AddComponent<LayoutElement>();
            var slotRT  = slotObj.GetComponent<RectTransform>();
            if (slotLE.preferredWidth  <= 0) slotLE.preferredWidth  = slotRT.sizeDelta.x > 0 ? slotRT.sizeDelta.x : 100f;
            if (slotLE.preferredHeight <= 0) slotLE.preferredHeight = slotRT.sizeDelta.y > 0 ? slotRT.sizeDelta.y : 100f;

            var iconImg = FindChildImage(slotObj);
            if (iconImg != null) { iconImg.sprite = relic.icon; iconImg.enabled = true; }

            foreach (var tmp in slotObj.GetComponentsInChildren<TextMeshProUGUI>(true))
                tmp.gameObject.SetActive(false);

            var overlay = new GameObject("SoldOut", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(slotObj.transform, false);
            var overlayRT = overlay.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = overlayRT.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            overlay.SetActive(false);

            var priceGo  = new GameObject("PriceText", typeof(RectTransform), typeof(TextMeshProUGUI));
            priceGo.transform.SetParent(wrapper.transform, false);
            var priceTMP = priceGo.GetComponent<TextMeshProUGUI>();
            priceTMP.text          = $"{price}G";
            priceTMP.alignment     = TextAlignmentOptions.Center;
            priceTMP.fontSize      = 16f;
            priceTMP.color         = Color.white;
            priceTMP.raycastTarget = false;
            var priceLE = priceGo.AddComponent<LayoutElement>();
            priceLE.preferredWidth  = 120f;
            priceLE.preferredHeight = 24f;

            var trigger = slotObj.GetComponent<TooltipTrigger>() ?? slotObj.AddComponent<TooltipTrigger>();
            trigger.SetDynamicEntries(new List<TooltipEntry> { TooltipEntry.ObjectUI(relic.relicName, relic.description) });

            var btn     = slotObj.GetComponent<Button>() ?? slotObj.AddComponent<Button>();
            bool isSold = false;
            btn.onClick.AddListener(() =>
            {
                if (isSold) return;
                if (GameManager.Instance == null || !GameManager.Instance.SpendGold(price))
                {
                    Debug.Log("[ShopController] 골드 부족");
                    return;
                }
                isSold = true;
                overlay.SetActive(true);
                GameManager.Instance.AddRelic(relic.relicCode);
                Debug.Log($"[ShopController] 유물 구매: {relic.relicName} (-{price}G)");
            });
        }

        private void GenerateItemSlots()
        {
            if (itemSlotsContainer == null || itemShopSlotPrefab == null) return;
            foreach (Transform child in itemSlotsContainer) Destroy(child.gameObject);

            var character = DeckRoguelike.Core.GameManager.Instance != null
                ? DeckRoguelike.Core.GameManager.Instance.SelectedCharacter
                : DeckRoguelike.Core.CharacterType.Warrior;
            var pool = ItemRegistry.GetForCharacter(character, bossItem: false);
            Shuffle(pool);

            int count = Mathf.Min(itemCount, pool.Count);
            for (int i = 0; i < count; i++)
            {
                var item  = pool[i];
                int price = RollItemPrice(item);
                SpawnItemEntry(item, price);
            }
        }

        private void SpawnItemEntry(ItemData item, int price)
        {
            var wrapper = new GameObject("ItemEntry", typeof(RectTransform));
            wrapper.transform.SetParent(itemSlotsContainer, false);
            var vlg = wrapper.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment         = TextAnchor.UpperCenter;
            vlg.spacing                = 6f;
            vlg.childForceExpandWidth  = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            var csf = wrapper.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            var slotObj = Instantiate(itemShopSlotPrefab, wrapper.transform);
            var slotLE  = slotObj.GetComponent<LayoutElement>() ?? slotObj.AddComponent<LayoutElement>();
            var slotRT  = slotObj.GetComponent<RectTransform>();
            if (slotLE.preferredWidth  <= 0) slotLE.preferredWidth  = slotRT.sizeDelta.x > 0 ? slotRT.sizeDelta.x : 100f;
            if (slotLE.preferredHeight <= 0) slotLE.preferredHeight = slotRT.sizeDelta.y > 0 ? slotRT.sizeDelta.y : 100f;

            var iconImg = FindChildImage(slotObj);
            if (iconImg != null) { iconImg.sprite = item.icon; iconImg.enabled = true; }

            var overlay = new GameObject("SoldOut", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(slotObj.transform, false);
            var overlayRT = overlay.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = overlayRT.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            overlay.SetActive(false);

            var priceGo  = new GameObject("PriceText", typeof(RectTransform), typeof(TextMeshProUGUI));
            priceGo.transform.SetParent(wrapper.transform, false);
            var priceTMP = priceGo.GetComponent<TextMeshProUGUI>();
            priceTMP.text          = $"{price}G";
            priceTMP.alignment     = TextAlignmentOptions.Center;
            priceTMP.fontSize      = 16f;
            priceTMP.color         = Color.white;
            priceTMP.raycastTarget = false;
            var priceLE = priceGo.AddComponent<LayoutElement>();
            priceLE.preferredWidth  = 120f;
            priceLE.preferredHeight = 24f;

            var trigger = slotObj.GetComponent<TooltipTrigger>() ?? slotObj.AddComponent<TooltipTrigger>();
            trigger.SetDynamicEntries(new List<TooltipEntry> { TooltipEntry.ObjectUI(item.itemName, item.description) });

            var btn     = slotObj.GetComponent<Button>() ?? slotObj.AddComponent<Button>();
            bool isSold = false;
            btn.onClick.AddListener(() =>
            {
                if (isSold) return;
                if (GameManager.Instance == null || !GameManager.Instance.SpendGold(price))
                {
                    Debug.Log("[ShopController] 골드 부족");
                    return;
                }
                isSold = true;
                overlay.SetActive(true);
                GameManager.Instance.AddItem(item);
                Debug.Log($"[ShopController] 아이템 구매: {item.itemName} (-{price}G)");
            });
        }

        #endregion

        // ─────────────────────────────────────────
        #region Remove Card

        private void OnRemoveCardClicked()
        {
            if (GameManager.Instance == null || subCardListController == null) return;

            _pendingRemovalPrice = GameManager.Instance.CardRemovalPrice;

            if (GameManager.Instance.Gold < _pendingRemovalPrice)
            {
                Debug.Log("[ShopController] 골드 부족 (카드 제거)");
                return;
            }

            subCardListController.gameObject.SetActive(true);
            subCardListController.Setup(
                RestCardMode.Remove,
                cardConfirmPanel,
                onFinish: OnCardRemoveConfirmed
            );
        }

        private void OnCardRemoveConfirmed()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.SpendGold(_pendingRemovalPrice);
            GameManager.Instance.UseCardRemoval();
            Debug.Log($"[ShopController] 카드 제거 완료 (-{_pendingRemovalPrice}G)");
            RefreshRemoveCardButton();
        }

        private void RefreshRemoveCardButton()
        {
            if (GameManager.Instance == null) return;
            int price = GameManager.Instance.CardRemovalPrice;
            if (removeCardPriceText != null)
                removeCardPriceText.text = $"카드 제거\n{price}G";
            if (removeCardButton != null)
                removeCardButton.interactable = GameManager.Instance.Gold >= price;
        }

        #endregion

        // ─────────────────────────────────────────
        #region Helpers

        public void Close()
        {
            gameObject.SetActive(false);
            onLeave?.Invoke();
        }

        private static Image FindChildImage(GameObject root)
        {
            foreach (var img in root.GetComponentsInChildren<Image>(true))
                if (img.gameObject != root) return img;
            return root.GetComponent<Image>();
        }

        private static int RollRelicPrice(RelicData relic)
        {
            var r = RelicPriceRanges[relic.RarityIndex];
            return Random.Range(r.min, r.max + 1);
        }

        private static int RollItemPrice(ItemData item)
        {
            var r = ItemPriceRanges[item.RarityIndex];
            return Random.Range(r.min, r.max + 1);
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        #endregion
    }
}
