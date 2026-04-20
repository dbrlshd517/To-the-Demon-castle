using UnityEngine;
using System;
using System.Collections.Generic;

namespace DeckRoguelike.Core
{
    [CreateAssetMenu(fileName = "LocalizationDatabase", menuName = "DeckRoguelike/Localization Database")]
    public class LocalizationDatabase : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string code;
            public string en;
            public string pt_BR;
            public string zh_CN;
            public string zh_TW;
            public string nl;
            public string eo;
            public string fi;
            public string fr;
            public string de;
            public string id;
            public string it;
            public string ja;
            public string ko;
            public string pl;
            public string ru;
            public string sr;
            public string sr_Latn;
            public string es;
            public string th;
            public string tr;
            public string uk;
            public string vi;
            public string description;
        }

        public List<Entry> entries = new List<Entry>();

        private Dictionary<string, Entry> _cache;

        private void BuildCache()
        {
            _cache = new Dictionary<string, Entry>(entries.Count);
            foreach (var e in entries)
                if (!string.IsNullOrEmpty(e.code)) _cache[e.code] = e;
        }

        /// <summary>에디터에서 entries가 변경된 후 캐시를 수동 갱신합니다.</summary>
        public void InvalidateCache() => _cache = null;

        public string GetText(string code, Language language)
        {
            if (_cache == null) BuildCache();

            if (!_cache.TryGetValue(code, out var e)) return code;

            string text = language switch
            {
                Language.en      => e.en,
                Language.pt_BR   => e.pt_BR,
                Language.zh_CN   => e.zh_CN,
                Language.zh_TW   => e.zh_TW,
                Language.nl      => e.nl,
                Language.eo      => e.eo,
                Language.fi      => e.fi,
                Language.fr      => e.fr,
                Language.de      => e.de,
                Language.id      => e.id,
                Language.it      => e.it,
                Language.ja      => e.ja,
                Language.ko      => e.ko,
                Language.pl      => e.pl,
                Language.ru      => e.ru,
                Language.sr      => e.sr,
                Language.sr_Latn => e.sr_Latn,
                Language.es      => e.es,
                Language.th      => e.th,
                Language.tr      => e.tr,
                Language.uk      => e.uk,
                Language.vi      => e.vi,
                _                => string.Empty,
            };

            if (!string.IsNullOrEmpty(text)) return text;
            return string.IsNullOrEmpty(e.en) ? code : e.en; // fallback: 영어
        }
    }
}
