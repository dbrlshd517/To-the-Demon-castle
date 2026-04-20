using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using DeckRoguelike.Combat;

namespace DeckRoguelike.Core
{
    /// <summary>
    /// Resources/Enemies 폴더의 EnemyData를 자동 로드하여 enemyId로 조회.
    /// </summary>
    public static class EnemyRegistry
    {
        private static Dictionary<int, EnemyData> lookup;

        private static void EnsureLoaded()
        {
            if (lookup != null) return;

            lookup = new Dictionary<int, EnemyData>();
            var loaded = Addressables.LoadAssetsAsync<EnemyData>("Enemies", null).WaitForCompletion();
            foreach (var enemy in loaded)
            {
                if (enemy == null || enemy.enemyId == 0) continue;
                if (lookup.ContainsKey(enemy.enemyId))
                {
                    Debug.LogWarning($"[EnemyRegistry] 중복 enemyId: {enemy.enemyId} ({enemy.enemyName})");
                    continue;
                }
                lookup[enemy.enemyId] = enemy;
            }

            Debug.Log($"[EnemyRegistry] 적 {lookup.Count}개 로드 완료");
        }

        public static EnemyData GetEnemy(int code)
        {
            EnsureLoaded();
            if (lookup.TryGetValue(code, out var enemy)) return enemy;
            Debug.LogWarning($"[EnemyRegistry] enemyId {code}를 찾을 수 없습니다.");
            return null;
        }

        public static void Reset() => lookup = null;
    }
}
