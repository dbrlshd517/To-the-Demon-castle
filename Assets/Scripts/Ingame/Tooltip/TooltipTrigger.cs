using System.Collections.Generic;
using DeckRoguelike.Core;
using DeckRoguelike.Relic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 툴팁이 필요한 UI GameObject에 이 컴포넌트를 추가합니다.
    /// TooltipUI 싱글톤이 없어도 FindObjectOfType으로 자동 탐색합니다.
    ///
    /// [입력 방법 — 우선순위 순]
    ///   1. SetDynamicEntries()  : 코드에서 동적으로 설정 (카드, 적, 플레이어 등 가변 툴팁)
    ///   2. relicCode            : RelicDatabase에서 이름·설명·아이콘 자동 조회
    ///   3. tooltipCodes         : TooltipDatabase에서 코드로 조회
    ///   4. staticEntries        : Inspector에서 직접 입력
    ///   (2·3·4는 동시에 사용 가능, 순서대로 합쳐져 표시)
    ///
    /// [위치 설정]
    ///   followMouse = true  → 마우스 커서를 따라다님
    ///   followMouse = false → 이 GameObject 중심 기준으로 offset(픽셀) 위치에 고정
    /// </summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("위치")]
        [SerializeField] private bool    followMouse = true;
        [SerializeField] private Vector2 offset      = Vector2.zero;

        [Header("툴팁 내용 — 방법 1: 직접 입력")]
        [SerializeField] private List<TooltipEntry> staticEntries = new List<TooltipEntry>();

        [Header("툴팁 내용 — 방법 2: CSV 코드 조회")]
        [SerializeField] private List<int> tooltipCodes = new List<int>();

        [Header("툴팁 내용 — 방법 3: 유물 코드 (Inspector 또는 런타임 설정)")]
        [SerializeField] private int relicCode = 0;

        // 동적 엔트리 (SetDynamicEntries로 설정, 최우선)
        private List<TooltipEntry> _dynamicEntries;
        private RelicData          _cachedRelicData;
        private TooltipDatabase    _tooltipDb;

        // ── 동적 API ───────────────────────────────────────────────────

        public void SetDynamicEntries(List<TooltipEntry> entries)
            => _dynamicEntries = entries;

        public void ClearDynamicEntries()
            => _dynamicEntries = null;

        public void SetRelicCode(int code)
        {
            relicCode        = code;
            _cachedRelicData = null;
        }

        public void SetPosition(bool follow, Vector2 pixelOffset)
        {
            followMouse = follow;
            offset      = pixelOffset;
        }

        // ── Pointer 이벤트 ─────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            var entries = _dynamicEntries != null && _dynamicEntries.Count > 0
                ? _dynamicEntries
                : CompileEntries();

            if (entries.Count == 0) return;
            GetTooltipUI()?.Show(entries, followMouse, offset, GetComponent<RectTransform>());
        }

        public void OnPointerExit(PointerEventData eventData)
            => GetTooltipUI()?.Hide();

        // ── 내부 ───────────────────────────────────────────────────────

        private TooltipUI GetTooltipUI()
        {
            if (TooltipUI.Instance != null) return TooltipUI.Instance;
            return FindObjectOfType<TooltipUI>();
        }

        private List<TooltipEntry> CompileEntries()
        {
            var result = new List<TooltipEntry>();

            // 유물 코드 → RelicData (LocalizationManager 실패 시 RelicData 필드 직접 사용)
            if (relicCode > 0)
            {
                var relic = GetRelicData();
                if (relic != null)
                {
                    string name = LocalizationManager.Get($"relic_name_{relic.relicCode}");
                    if (name == $"relic_name_{relic.relicCode}") name = relic.relicName;

                    string desc = LocalizationManager.Get($"relic_desc_{relic.relicCode}");
                    if (desc == $"relic_desc_{relic.relicCode}") desc = relic.description;

                    result.Add(TooltipEntry.ObjectUI(name, desc, relic.icon));
                }
            }

            // TooltipDatabase 직접 조회 (TooltipUI 불필요)
            if (tooltipCodes.Count > 0)
            {
                var db = GetTooltipDatabase();
                foreach (var code in tooltipCodes)
                {
                    var entry = db?.GetByCode(code);
                    if (entry == null) continue;
                    result.Add(new TooltipEntry
                    {
                        code        = entry.code,
                        uiType      = entry.uiType,
                        name        = entry.name,
                        description = entry.description,
                        icon        = entry.icon,
                    });
                }
            }

            // 직접 입력
            result.AddRange(staticEntries);

            return result;
        }

        private RelicData GetRelicData()
        {
            if (_cachedRelicData != null) return _cachedRelicData;
            var db = Addressables.LoadAssetAsync<RelicDatabase>("Data/RelicDatabase").WaitForCompletion();
            if (db == null) return null;
            _cachedRelicData = db.relics.Find(r => r.relicCode == relicCode);
            if (_cachedRelicData != null && _cachedRelicData.icon == null)
            {
                var sprite = Addressables.LoadAssetAsync<Sprite>($"Sprites/Relic/{relicCode}").WaitForCompletion();
                if (sprite != null) _cachedRelicData.icon = sprite;
            }
            return _cachedRelicData;
        }

        private TooltipDatabase GetTooltipDatabase()
        {
            if (_tooltipDb != null) return _tooltipDb;
            _tooltipDb = Addressables.LoadAssetAsync<TooltipDatabase>("TooltipDatabase").WaitForCompletion();
            return _tooltipDb;
        }
    }
}
