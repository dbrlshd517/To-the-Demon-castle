using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using DeckRoguelike.Combat;

namespace DeckRoguelike.Core
{
    /// <summary>
    /// Resources/Allies 폴더의 AllyData를 자동 로드하여 allyCode로 조회.
    /// </summary>
    public static class AllyRegistry
    {
        private static Dictionary<int, AllyData> lookup;

        private static void EnsureLoaded()
        {
            if (lookup != null) return;

            lookup = new Dictionary<int, AllyData>();
            var loaded = Addressables.LoadAssetsAsync<AllyData>("Allies", null).WaitForCompletion();
            if (loaded == null)
            {
                // "Allies" 라벨 에셋이 하나도 없으면 LoadAssetsAsync는 null을 돌려준다 → 크래시 대신 빈 채로 둔다.
                Debug.LogWarning("[AllyRegistry] \"Allies\" 라벨 에셋을 찾을 수 없습니다. " +
                    "Tools > Addressables > Setup GameResources 를 실행해 라벨을 등록하세요.");
                return;
            }
            foreach (var ally in loaded)
            {
                if (ally == null) continue;
                if (lookup.ContainsKey(ally.allyCode))
                {
                    Debug.LogWarning($"[AllyRegistry] 중복 allyCode: {ally.allyCode} ({ally.allyName})");
                    continue;
                }
                lookup[ally.allyCode] = ally;
            }

            Debug.Log($"[AllyRegistry] 아군 {lookup.Count}개 로드 완료");
        }

        public static AllyData GetAlly(int code)
        {
            EnsureLoaded();
            if (lookup.TryGetValue(code, out var ally)) return ally;
            Debug.LogWarning($"[AllyRegistry] allyCode {code}를 찾을 수 없습니다.");
            return null;
        }

        public static void Reset() => lookup = null;
    }
}
