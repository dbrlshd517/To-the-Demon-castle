using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 툴팁 매니저 싱글톤. Canvas 하위에 1개만 배치합니다.
    ///
    /// [Hierarchy 예시]
    /// Canvas
    ///   └─ TooltipRoot           ← 이 컴포넌트
    ///        └─ Container        ← VerticalLayoutGroup + ContentSizeFitter
    ///             (panelBlockPrefab이 여기에 동적 생성됨)
    ///
    /// [PanelBlock 프리팹 구성]
    ///   PanelBlock  (Image + VerticalLayoutGroup + ContentSizeFitter)
    ///     ├─ IconRow  (HorizontalLayoutGroup)
    ///     │    ├─ Icon  (Image)          ← 이름 "Icon"
    ///     │    └─ Name  (TextMeshProUGUI) ← 이름 "Name"
    ///     └─ Description (TextMeshProUGUI) ← 이름 "Description"
    /// </summary>
    public class TooltipUI : MonoBehaviour
    {
        public static TooltipUI Instance { get; private set; }

        [SerializeField] private GameObject    panelBlockPrefab;
        [SerializeField] private RectTransform container;
        [SerializeField] private float         showDelay    = 0f;
        [SerializeField] private Vector2       mouseOffset  = new Vector2(16f, -16f);

        private Canvas              _canvas;
        private Coroutine           _showCoroutine;
        private List<GameObject>    _activePanels = new List<GameObject>();
        private TooltipDatabase     _db;

        // 현재 표시 설정
        private bool          _followMouse;
        private Vector2       _offset;
        private RectTransform _source;

        private void Awake()
        {
            Instance = this;
            _canvas  = GetComponentInParent<Canvas>();
            _db      = Addressables.LoadAssetAsync<TooltipDatabase>("TooltipDatabase").WaitForCompletion();
            container.gameObject.SetActive(false);
        }

        // ── 외부 API ───────────────────────────────────────────────────

        /// <summary>툴팁을 표시합니다.</summary>
        /// <param name="entries">표시할 패널 목록</param>
        /// <param name="followMouse">true=마우스 따라다님, false=source 기준 offset 고정</param>
        /// <param name="offset">followMouse=false일 때 source 중심으로부터의 픽셀 오프셋</param>
        /// <param name="source">위치 기준 RectTransform (followMouse=false일 때 사용)</param>
        public void Show(List<TooltipEntry> entries, bool followMouse, Vector2 offset, RectTransform source)
        {
            if (entries == null || entries.Count == 0) return;

            if (_showCoroutine != null) StopCoroutine(_showCoroutine);
            _showCoroutine = StartCoroutine(ShowRoutine(entries, followMouse, offset, source));
        }

        public void Hide()
        {
            if (_showCoroutine != null)
            {
                StopCoroutine(_showCoroutine);
                _showCoroutine = null;
            }
            ClearPanels();
            container.gameObject.SetActive(false);
        }

        /// <summary>code로 TooltipDatabase에서 entry를 조회합니다.</summary>
        public TooltipEntry GetEntry(int code) => _db?.GetByCode(code);

        // ── 내부 ───────────────────────────────────────────────────────

        private IEnumerator ShowRoutine(List<TooltipEntry> entries, bool followMouse, Vector2 offset, RectTransform source)
        {
            yield return new WaitForSeconds(showDelay);

            _followMouse = followMouse;
            _offset      = offset;
            _source      = source;

            ClearPanels();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.description)) continue;
                CreateBlock(entry);
            }

            if (_activePanels.Count == 0) yield break;

            // 활성화 전에 위치 먼저 설정해서 깜박임 방지
            UpdatePosition();
            container.gameObject.SetActive(true);

            // ContentSizeFitter 계산 후 화면 밖 클램프만
            yield return null;
            ClampToCanvas();
        }

        private void CreateBlock(TooltipEntry entry)
        {
            var block = Instantiate(panelBlockPrefab, container);

            // Icon (type 1 = Object UI일 때만)
            var iconTr = block.transform.Find("IconRow/Icon");
            if (iconTr != null)
            {
                iconTr.gameObject.SetActive(entry.ShowIcon);
                if (entry.ShowIcon)
                    iconTr.GetComponent<Image>().sprite = entry.icon;
            }

            // Name
            var nameTr = block.transform.Find("IconRow/Name");
            if (nameTr != null)
            {
                bool hasName = !string.IsNullOrEmpty(entry.name);
                nameTr.gameObject.SetActive(hasName);
                if (hasName)
                    nameTr.GetComponent<TextMeshProUGUI>().text = entry.name;
            }

            // IconRow (이름도 아이콘도 없으면 숨기기)
            var iconRowTr = block.transform.Find("IconRow");
            if (iconRowTr != null)
                iconRowTr.gameObject.SetActive(entry.ShowIcon || !string.IsNullOrEmpty(entry.name));

            // Description
            var descTr = block.transform.Find("Description");
            if (descTr != null)
                descTr.GetComponent<TextMeshProUGUI>().text = entry.description;

            _activePanels.Add(block);
        }

        private void ClearPanels()
        {
            foreach (var p in _activePanels)
                if (p != null) Destroy(p);
            _activePanels.Clear();
        }

        private void Update()
        {
            if (!container.gameObject.activeSelf) return;
            if (_followMouse) UpdatePosition();
        }

        private void UpdatePosition()
        {
            Vector2 canvasLocalPoint;

            if (_followMouse)
            {
                ToScreenToCanvas(Mouse.current.position.ReadValue(), out canvasLocalPoint);
                container.anchoredPosition = canvasLocalPoint + mouseOffset;
            }
            else
            {
                if (_source == null) return;
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(
                    _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
                    _source.position
                );
                ToScreenToCanvas(screenPos, out canvasLocalPoint);
                container.anchoredPosition = canvasLocalPoint + _offset;
            }

            ClampToCanvas();
        }

        private void ToScreenToCanvas(Vector2 screenPos, out Vector2 localPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.GetComponent<RectTransform>(),
                screenPos,
                _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
                out localPoint
            );
        }

        private void ClampToCanvas()
        {
            Vector3[] cc = new Vector3[4];
            Vector3[] cv = new Vector3[4];
            container.GetWorldCorners(cc);
            _canvas.GetComponent<RectTransform>().GetWorldCorners(cv);

            // GetWorldCorners는 스크린 픽셀 좌표 → anchoredPosition과 같은 단위로 변환
            float scale = _canvas.scaleFactor;
            Vector2 pos = container.anchoredPosition;

            if (cc[2].x > cv[2].x) pos.x -= (cc[2].x - cv[2].x) / scale;
            if (cc[0].x < cv[0].x) pos.x += (cv[0].x - cc[0].x) / scale;
            if (cc[2].y > cv[2].y) pos.y -= (cc[2].y - cv[2].y) / scale;
            if (cc[0].y < cv[0].y) pos.y += (cv[0].y - cc[0].y) / scale;

            container.anchoredPosition = pos;
        }
    }
}
