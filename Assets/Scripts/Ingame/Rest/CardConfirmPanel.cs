using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DeckRoguelike.Cards;
using DeckRoguelike.Core;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 카드 강화/제거 확인 패널.
    ///
    /// Remove  : 선택 카드를 패널 중앙에 단독 표시
    /// Upgrade : 강화 전 카드(왼쪽)와 강화 후 카드(오른쪽)를 중앙 기준으로 배치
    ///
    /// [Unity 구조]
    /// CardConfirmPanel
    ///  ├─ CardArea      (Transform) ← cardArea   카드가 생성될 컨테이너
    ///  └─ ConfirmButton (Button)    ← confirmButton
    /// </summary>
    public class CardConfirmPanel : MonoBehaviour
    {
        [Header("=== 카드 표시 ===")]
        [SerializeField] private GameObject cardPrefab;
        [Tooltip("카드가 생성될 컨테이너 (없으면 이 패널 자체 사용)")]
        [SerializeField] private Transform  cardArea;

        [Header("=== 레이아웃 ===")]
        [Tooltip("Upgrade 모드: 중앙 기준 각 카드의 좌우 오프셋(px)")]
        [SerializeField] private float cardOffset = 220f;
        [Tooltip("카드 RectTransform 크기(px)")]
        [SerializeField] private Vector2 cardSize = new Vector2(120f, 180f);

        [Header("=== 버튼 ===")]
        [SerializeField] private Button confirmButton;

        private CardData                 targetCard;
        private System.Action<CardData>  onConfirm;
        private readonly List<GameObject> spawnedCards = new List<GameObject>();

        private void Awake()
        {
            confirmButton?.onClick.AddListener(OnConfirmClicked);
            gameObject.SetActive(false);
        }

        private void OnEnable()  => InGameUIController.Instance?.PushPanel("CardConfirm", Close);
        private void OnDisable() => InGameUIController.Instance?.UnregisterPanel("CardConfirm");

        // ──────────────────────────────────────────────

        public void Open(CardData card, RestCardMode mode, System.Action<CardData> confirmCallback)
        {
            targetCard = card;
            onConfirm  = confirmCallback;

            ClearCards();

            Transform parent = cardArea != null ? cardArea : transform;

            if (mode == RestCardMode.Remove)
            {
                SpawnCard(parent, card, Vector2.zero);
            }
            else // Upgrade
            {
                SpawnCard(parent, card, new Vector2(-cardOffset, 0f));

                var upgraded = CardRegistry.GetCard(card.cardCode + 1);
                SpawnCard(parent, upgraded != null ? upgraded : card, new Vector2(cardOffset, 0f));
            }

            gameObject.SetActive(true);
        }

        public void Close()
        {
            ClearCards();
            gameObject.SetActive(false);
        }

        // ──────────────────────────────────────────────

        private void OnConfirmClicked()
        {
            onConfirm?.Invoke(targetCard);
            Close();
        }

        private void SpawnCard(Transform parent, CardData card, Vector2 anchoredPos)
        {
            if (cardPrefab == null || card == null) return;

            var obj = Instantiate(cardPrefab, parent);
            spawnedCards.Add(obj);

            var rt = obj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin        = new Vector2(0.5f, 0.5f);
                rt.anchorMax        = new Vector2(0.5f, 0.5f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = anchoredPos;
                rt.sizeDelta        = cardSize;
            }

            var cardUI = obj.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.Initialize(card);
                cardUI.IsRuntimeUnplayable = true;
            }
        }

        private void ClearCards()
        {
            foreach (var obj in spawnedCards)
                if (obj != null) Destroy(obj);
            spawnedCards.Clear();
        }
    }
}
