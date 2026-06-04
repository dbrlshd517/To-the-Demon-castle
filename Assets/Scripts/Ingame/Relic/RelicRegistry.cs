using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckRoguelike.Relic
{
    /// <summary>
    /// 유물 데이터와 생성 팩토리를 ID로 등록하고 조회하는 정적 레지스트리.
    ///
    /// 사용법:
    ///   1. RelicRegistry.Register(data, () => new MyRelic());
    ///   2. RelicRegistry.Create("my_relic_id") 로 인스턴스 생성
    ///   3. RelicRegistry.GetByType(isBoss: false) 으로 보상 풀 조회
    /// </summary>
    public static class RelicRegistry
    {
        private static readonly Dictionary<int, (RelicData data, Func<RelicEffect> factory)> entries =
            new Dictionary<int, (RelicData, Func<RelicEffect>)>();

        /// <summary>유물 데이터와 팩토리를 등록합니다. 같은 코드면 덮어씁니다.</summary>
        public static void Register(RelicData data, Func<RelicEffect> factory)
        {
            if (data == null || data.relicCode == 0)
            {
                Debug.LogWarning("[RelicRegistry] 유효하지 않은 RelicData 등록 시도 무시됨");
                return;
            }
            entries[data.relicCode] = (data, factory);
        }

        /// <summary>
        /// 등록된 팩토리로 새 유물 인스턴스를 생성합니다.
        /// Data 필드를 자동으로 할당합니다.
        /// 코드가 없으면 경고 후 null 반환.
        /// </summary>
        public static RelicEffect Create(int relicCode)
        {
            if (entries.TryGetValue(relicCode, out var entry))
            {
                var relic = entry.factory();
                relic.Data = entry.data;
                return relic;
            }

            Debug.LogWarning($"[RelicRegistry] 등록되지 않은 유물 코드: {relicCode}");
            return null;
        }

        /// <summary>일반 유물(1xx) 또는 보스 유물(9xx) 목록을 반환합니다.
        /// 7xx 미사용 유물(IsUnused)은 풀에서 제외됩니다.</summary>
        public static List<RelicData> GetByType(bool bossRelic)
        {
            var result = new List<RelicData>();
            foreach (var entry in entries.Values)
                if (entry.data.IsBossRelic == bossRelic && !entry.data.IsUnused)
                    result.Add(entry.data);
            return result;
        }

        /// <summary>등록된 모든 RelicData 목록을 반환합니다.
        /// 7xx 미사용 유물(IsUnused)은 도감 등 열거에서 제외됩니다.</summary>
        public static List<RelicData> GetAll()
        {
            var result = new List<RelicData>();
            foreach (var entry in entries.Values)
                if (!entry.data.IsUnused)
                    result.Add(entry.data);
            return result;
        }

        /// <summary>모든 등록을 초기화합니다.</summary>
        public static void Clear() => entries.Clear();
    }
}
