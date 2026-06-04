using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using DeckRoguelike.Combat;

namespace DeckRoguelike.Core
{
    /// <summary>
    /// Resources/Encounters 폴더의 EnemyEncounter를 자동 로드.
    /// GameManager의 encounterPool 배열 수동 등록 불필요.
    /// </summary>
    public static class EncounterRegistry
    {
        private static List<EnemyEncounterData> all;

        private static void EnsureLoaded()
        {
            if (all != null) return;

            all = new List<EnemyEncounterData>();
            var loaded = Addressables.LoadAssetsAsync<EnemyEncounterData>("Encounters", null).WaitForCompletion();
            if (loaded == null)
            {
                // "Encounters" 라벨 에셋이 하나도 없으면 LoadAssetsAsync는 null을 돌려준다 → 크래시 대신 빈 채로 둔다.
                Debug.LogWarning("[EncounterRegistry] \"Encounters\" 라벨 에셋을 찾을 수 없습니다. " +
                    "Tools > Addressables > Setup GameResources 를 실행해 라벨을 등록하세요.");
                return;
            }
            foreach (var enc in loaded)
            {
                if (enc != null) all.Add(enc);
            }

            Debug.Log($"[EncounterRegistry] 인카운터 {all.Count}개 로드 완료");
        }

        /// <summary>
        /// D/A/N 코드에 맞는 인카운터를 가중치 랜덤으로 선택.
        /// 정확한 N이 없으면 가장 가까운 N으로 fallback.
        /// </summary>
        public static EnemyEncounterData SelectEncounter(int gameDifficulty, int act, int actDifficulty,
                                                          SeededRandom rng = null)
        {
            EnsureLoaded();

            var candidates = FindCandidates(gameDifficulty, act, actDifficulty);

            if (candidates.Count == 0)
            {
                Debug.LogWarning($"[EncounterRegistry] D={gameDifficulty} A={act} N={actDifficulty} 인카운터 없음. 가까운 난이도로 fallback.");
                int bestN = FindClosestN(gameDifficulty, act, actDifficulty);
                if (bestN >= 0) candidates = FindCandidates(gameDifficulty, act, bestN);
            }

            if (candidates.Count == 0)
            {
                Debug.LogError($"[EncounterRegistry] D={gameDifficulty} A={act}에 맞는 인카운터를 찾을 수 없습니다.");
                return null;
            }

            int idx = rng != null
                ? rng.EncounterRange(candidates.Count)
                : Random.Range(0, candidates.Count);
            return candidates[idx];
        }

        private static List<EnemyEncounterData> FindCandidates(int d, int a, int n)
        {
            var result = new List<EnemyEncounterData>();
            foreach (var enc in all)
            {
                if (enc.GameDifficulty == d && enc.ActFromCode == a && enc.ActDifficulty == n)
                    result.Add(enc);
            }
            return result;
        }

        private static int FindClosestN(int d, int a, int target)
        {
            int best = -1;
            foreach (var enc in all)
            {
                if (enc.GameDifficulty != d || enc.ActFromCode != a) continue;
                int n = enc.ActDifficulty;
                if (best < 0 || Mathf.Abs(n - target) < Mathf.Abs(best - target))
                    best = n;
            }
            return best;
        }

        public static void Reset() => all = null;
    }
}
