using UnityEngine;
using UnityEngine.AddressableAssets;
using System;

namespace DeckRoguelike.Core
{
    public enum Language
    {
        en, pt_BR, zh_CN, zh_TW, nl, eo, fi, fr, de, id, it, ja, ko,
        pl, ru, sr, sr_Latn, es, th, tr, uk, vi
    }

    public static class LocalizationManager
    {
        private static Language currentLanguage;
        private static LocalizationDatabase db;
        private static bool initialized;

        public static event Action OnLanguageChanged;

        public static Language CurrentLanguage
        {
            get
            {
                EnsureInitialized();
                return currentLanguage;
            }
            set
            {
                EnsureInitialized();
                if (currentLanguage == value) return;
                currentLanguage = value;
                PlayerPrefs.SetString("language", value.ToString());
                OnLanguageChanged?.Invoke();
            }
        }

        private static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            db = Addressables.LoadAssetAsync<LocalizationDatabase>("Data/LocalizationDatabase").WaitForCompletion();
            if (db == null)
                Debug.LogWarning("[Localization] Data/LocalizationDatabase 를 찾을 수 없습니다. Addressables 주소를 확인하세요.");

            string savedLang = PlayerPrefs.GetString("language", "ko");
            if (Enum.TryParse<Language>(savedLang, out var lang))
                currentLanguage = lang;
        }

        public static string Get(string code)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(code)) return code;
            if (db == null)
            {
                Debug.LogWarning("[Localization] db가 null입니다. LocalizationDatabase 에셋을 확인하세요.");
                return code;
            }
            string result = db.GetText(code, currentLanguage);
            if (result == code)
                Debug.LogWarning($"[Localization] 키를 찾을 수 없음: '{code}' (언어: {currentLanguage})");
            return result;
        }

        /// <summary>경고 없이 조회. 키가 없으면 null 반환.</summary>
        public static string GetOrNull(string code)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(code) || db == null) return null;
            string result = db.GetText(code, currentLanguage);
            return result == code ? null : result;
        }
    }
}
