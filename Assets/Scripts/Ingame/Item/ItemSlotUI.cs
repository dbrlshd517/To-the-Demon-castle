using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using DeckRoguelike.Item;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 인게임 아이템 슬롯 하나에 붙는 컴포넌트.
    /// 클릭 시 InGameUIController를 통해 ConfirmUseItemPanel을 표시합니다.
    ///
    /// [프리팹 구조]
    /// ItemSlotPrefab (ItemSlotUI + Button + Image)
    ///  └─ IconImage (Image)  ← iconImage (선택)
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ItemSlotUI : MonoBehaviour
    {
        public ItemData ItemData { get; private set; }

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnSlotClicked);
        }

        /// <summary>아이템 데이터를 설정하고 아이콘을 표시합니다.</summary>
        public void Initialize(ItemData item)
        {
            ItemData = item;

            // 루트 또는 자식의 Image에 아이콘 적용
            var images = GetComponentsInChildren<Image>(true);
            Image iconImg = null;
            foreach (var img in images)
            {
                if (img.gameObject != gameObject) { iconImg = img; break; }
            }
            if (iconImg == null)
            {
                // 루트에만 Image가 있는 경우
                iconImg = GetComponent<Image>();
            }

            Sprite itemSprite = null;
            if (iconImg != null && item != null)
            {
                itemSprite = item.icon;
                if (itemSprite == null && item.itemCode > 0)
                    itemSprite = Addressables.LoadAssetAsync<Sprite>($"Sprites/Ingame/Item/{item.itemCode}").WaitForCompletion();

                if (itemSprite != null)
                {
                    iconImg.sprite = itemSprite;
                    iconImg.color  = Color.white;
                }
                else
                {
                    iconImg.color = new Color(0.3f, 0.7f, 0.3f, 1f);
                }
                iconImg.enabled = true;
            }

            // hover 시 툴팁 표시
            if (item != null)
            {
                var trigger = GetComponent<TooltipTrigger>() ?? gameObject.AddComponent<TooltipTrigger>();
                trigger.SetDynamicEntries(new List<TooltipEntry>
                {
                    TooltipEntry.ObjectUI(item.itemName, item.description, itemSprite)
                });
            }
        }

        private void OnSlotClicked()
        {
            if (ItemData == null) return;

            var combat = InGameUIController.Instance?.GetCombatController();

            // 아이템 타겟팅 모드 중 슬롯 클릭은 무시 (취소 조건에서 제외)
            if (combat != null && combat.IsItemTargetingActive) return;

            // 이 슬롯에 대한 패널이 이미 열려있으면 무시 (깜빡임 방지)
            if (InGameUIController.Instance?.HasActiveConfirmPanelFor(this) == true) return;

            InGameUIController.Instance?.ShowConfirmUseItemPanel(ItemData, this);
        }
    }
}
