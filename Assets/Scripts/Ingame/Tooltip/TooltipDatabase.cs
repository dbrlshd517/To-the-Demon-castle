using System.Collections.Generic;
using UnityEngine;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// Tools > Tooltip Data > Import from CSV 로 생성되는 툴팁 데이터베이스.
    /// Assets/Resources/TooltipDatabase.asset 에 저장됩니다.
    /// </summary>
    [CreateAssetMenu(menuName = "DeckRoguelike/Tooltip Database", fileName = "TooltipDatabase")]
    public class TooltipDatabase : ScriptableObject
    {
        public List<TooltipEntry> entries = new List<TooltipEntry>();

        private Dictionary<int, TooltipEntry> _cache;

        public TooltipEntry GetByCode(int code)
        {
            if (_cache == null) BuildCache();
            return _cache.TryGetValue(code, out var e) ? e : null;
        }

        private void BuildCache()
        {
            _cache = new Dictionary<int, TooltipEntry>();
            foreach (var e in entries)
                if (e.code > 0) _cache[e.code] = e;
        }
    }
}
