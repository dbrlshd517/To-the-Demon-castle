using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 적 유닛 행동 클래스를 ID로 등록하고 인스턴스를 생성하는 정적 레지스트리.
    ///
    /// 사용법:
    ///   1. EnemyBehaviorRegistry.Register(1001, () => new SlimeBehavior());
    ///   2. 소환 시 EnemyBehaviorRegistry.Create(data.enemyCode) 로 인스턴스 생성
    ///
    /// 행동 객체는 유닛마다 개별 생성되므로 내부 상태(페이즈 등)를 멤버 변수로 관리 가능합니다.
    /// </summary>
    public static class EnemyBehaviorRegistry
    {
        private static readonly Dictionary<int, Func<EnemyBehavior>> factories =
            new Dictionary<int, Func<EnemyBehavior>>();

        /// <summary>행동 팩토리를 enemyCode와 함께 등록합니다. 같은 코드면 덮어씁니다.</summary>
        public static void Register(int code, Func<EnemyBehavior> factory)
        {
            factories[code] = factory;
        }

        /// <summary>
        /// 등록된 팩토리로 새 행동 인스턴스를 생성합니다.
        /// 코드가 없으면 경고 후 null 반환.
        /// </summary>
        public static EnemyBehavior Create(int code)
        {
            if (factories.TryGetValue(code, out var factory))
                return factory();

            Debug.LogWarning($"[EnemyBehaviorRegistry] 등록되지 않은 enemyCode: {code}");
            return null;
        }

        /// <summary>모든 등록을 초기화합니다.</summary>
        public static void Clear() => factories.Clear();
    }
}
