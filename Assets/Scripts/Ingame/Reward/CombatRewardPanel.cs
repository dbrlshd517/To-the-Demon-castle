using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using System.Collections.Generic;
using DeckRoguelike.Cards;
using DeckRoguelike.Combat;
using DeckRoguelike.Core;
using DeckRoguelike.Relic;
using DeckRoguelike.Item;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// Slay the Spire 스타일 전투 보상 패널.
    ///
    /// rewardContainer를 기준으로 위에서부터 아래 순서로 프리팹을 생성합니다:
    ///   골드 → 유물 → 아이템 → 카드
    ///
    /// 보상 획득 시 해당 행이 제거되고 VerticalLayoutGroup이 나머지 행을 위로 올립니다.
    ///
    /// [Inspector 연결]
    ///   rewardContainer  : 행이 생성될 부모 Transform (VerticalLayoutGroup 필요)
    ///   rewardRowPrefab  : RewardRowUI 컴포넌트가 있는 행 프리팹
    ///   goldIcon         : 골드 전용 아이콘 스프라이트
    ///   cardIcon         : 카드 보상 전용 아이콘 스프라이트
    ///   leaveButton      : 맵으로 돌아가는 버튼
    ///   cardRewardPanel  : 카드 선택 서브패널
    /// </summary>
    public class CombatRewardPanel : MonoBehaviour
    {
        [Header("=== 컨테이너 / 프리팹 ===")]
        [Tooltip("행이 생성될 부모 Transform — VerticalLayoutGroup을 붙여두세요")]
        [SerializeField] private Transform       rewardContainer;
        [SerializeField] private GameObject      rewardRowPrefab;

        [Header("=== 고정 아이콘 ===")]
        [Tooltip("골드 행에 표시할 아이콘 (없으면 Sprites/UI/coin 자동 로드)")]
        [SerializeField] private Sprite          goldIcon;
        [Tooltip("카드 보상 행에 표시할 아이콘 (없으면 Sprites/UI/rewardbox1 자동 로드)")]
        [SerializeField] private Sprite          cardIcon;

        [Header("=== 간격 ===")]
        [Tooltip("행과 행 사이 간격 (px)")]
        [SerializeField] private float rowSpacing = 12f;

        [Header("=== 버튼 ===")]
        [SerializeField] private Button          leaveButton;

        [Header("=== 서브패널 ===")]
        [SerializeField] private CardRewardPanel cardRewardPanel;

        // 스프라이트 폴더 경로
        private const string RelicSpritePath = "Sprites/Relic/";
        private const string ItemSpritePath  = "Sprites/Ingame/Item/";
        private const string GoldSpritePath  = "Sprites/UI/coin";
        private const string CardSpritePath  = "Sprites/UI/rewardbox1";

        // ──────────────────────────────────────────────────────────────────
        private void Awake()
        {
            leaveButton?.onClick.AddListener(OnLeaveClicked);
            gameObject.SetActive(false);
        }

        // ──────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// 보상 패널을 엽니다. 순서: 골드 → 유물 → 아이템 → 카드
        /// goldAmount : 이미 GameManager에 지급된 골드 (표시 전용)
        /// offerCard  : 카드 선택 행 표시 여부
        /// bonusRelic : 보스/엘리트 보상 유물 (없으면 null)
        /// bonusItem  : 아이템 보상 (없으면 null)
        /// </summary>
        public void Open(int goldAmount, bool offerCard,
                         RelicData bonusRelic = null, ItemData bonusItem = null)
        {
            ClearRows();
            gameObject.SetActive(true);
            leaveButton?.gameObject.SetActive(true);

            if (rewardContainer != null)
            {
                var vlg = rewardContainer.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
                if (vlg != null) vlg.spacing = rowSpacing;
            }

            if (goldAmount > 0)     SpawnGoldRow(goldAmount);
            if (bonusRelic != null) SpawnRelicRow(bonusRelic);
            if (bonusItem  != null) SpawnItemRow(bonusItem);
            if (offerCard)          SpawnCardRow();
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────
        #region Row Spawners

        private void SpawnGoldRow(int amount)
        {
            var row = CreateRow();
            if (row == null) return;

            string goldLabel = LocalizationManager.Get("playerinfo_gold");
            row.SetText($"{goldLabel}  +{amount}");
            row.SetIcon(goldIcon != null ? goldIcon : Addressables.LoadAssetAsync<Sprite>(GoldSpritePath).WaitForCompletion());

            row.AddClaimListener(() =>
            {
                GameManager.Instance?.ModifyGold(amount);
                row.DestroySelf();
            });
        }

        private void SpawnRelicRow(RelicData relic)
        {
            var row = CreateRow();
            if (row == null) return;

            string label = LocalizationManager.Get($"relic_{relic.relicCode}");
            if (label == $"relic_{relic.relicCode}") label = relic.relicName;
            row.SetText(label);

            Sprite icon = relic.icon != null
                ? relic.icon
                : Addressables.LoadAssetAsync<Sprite>($"{RelicSpritePath}{relic.relicCode}").WaitForCompletion();
            row.SetIcon(icon);

            row.AddClaimListener(() =>
            {
                GameManager.Instance?.AddRelic(relic.relicCode);
                row.DestroySelf();
            });
        }

        private void SpawnItemRow(ItemData item)
        {
            var row = CreateRow();
            if (row == null) return;

            string label = LocalizationManager.Get($"item_{item.itemCode}");
            if (label == $"item_{item.itemCode}") label = item.itemName;
            row.SetText(label);

            Sprite icon = item.icon != null
                ? item.icon
                : Addressables.LoadAssetAsync<Sprite>($"{ItemSpritePath}{item.itemCode}").WaitForCompletion();
            row.SetIcon(icon);

            row.AddClaimListener(() =>
            {
                GameManager.Instance?.AddItem(item);
                row.DestroySelf();
            });
        }

        private void SpawnCardRow()
        {
            var row = CreateRow();
            if (row == null) return;

            row.SetText(LocalizationManager.Get("reward_card"));
            row.SetIcon(cardIcon != null ? cardIcon : Addressables.LoadAssetAsync<Sprite>(CardSpritePath).WaitForCompletion());

            row.AddClaimListener(() =>
            {
                row.SetButtonInteractable(false);
                OpenCardSelection(row);
            });
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────
        #region Card Selection

        private void OpenCardSelection(RewardRowUI cardRow)
        {
            if (cardRewardPanel == null) return;

            cardRewardPanel.Open(card =>
            {
                if (card != null)
                {
                    DeckManager.Instance?.AddCardToDeck(card);
                    cardRow.DestroySelf();
                }
                else
                {
                    // 스킵: 버튼 다시 활성화, 카드 목록은 CardRewardPanel에 보존됨
                    cardRow.SetButtonInteractable(true);
                }
                InGameUIController.Instance?.CloseCardReward();
            });
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────
        #region Helpers

        private RewardRowUI CreateRow()
        {
            if (rewardRowPrefab == null || rewardContainer == null) return null;

            var obj = Instantiate(rewardRowPrefab, rewardContainer);
            obj.SetActive(true);
            return obj.GetComponent<RewardRowUI>();
        }

        private void ClearRows()
        {
            if (rewardContainer == null) return;
            foreach (Transform child in rewardContainer)
                Destroy(child.gameObject);
        }

        private void OnLeaveClicked()
        {
            InGameUIController.Instance?.OpenMap();
        }

        #endregion
    }
}
