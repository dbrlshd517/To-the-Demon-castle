using UnityEngine;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 플레이어가 소환하는 아군 유닛 데이터.
    /// 행동 알고리즘은 allyCode로 등록된 함수가 처리합니다.
    ///
    /// [allyCode 5자리 구조]
    ///   자리: A B C D O
    ///   A (1자리) : 종류 그룹  (예: 3 = 아군)
    ///   B (2자리) : 역할       (예: 0 = 공용)
    ///   C (3자리) : 개별 번호  (1~9)
    ///   D (4자리) : 서브 번호  (0~9, 규칙 없음)
    ///   O (5자리) : 강화       짝수 = 강화 전 / 홀수 = 강화 후
    ///
    /// [강화] 5번째 자리 +1  (30100 → 30101)
    /// </summary>
    [CreateAssetMenu(fileName = "NewAlly", menuName = "Deck Roguelike/Ally Data")]
    public class AllyData : ScriptableObject
    {
        [Header("Basic Info")]
        public int allyCode;
        public string allyName;
        [TextArea(2, 4)]
        public string description;
        public Sprite allySprite;

        [Header("Stats")]
        public int maxHP;
        public int baseDamage;

        [Header("Grid Size")]
        [Tooltip("그리드에서 차지하는 셀 크기 (가로, 세로). 기본 1x1. EnemyData.gridSize와 동일 규칙.")]
        public Vector2Int gridSize = new Vector2Int(1, 1);

        [Header("Duration")]
        [Tooltip("0 = 전투 종료까지 유지. behaviorId 함수에서 직접 참조.")]
        public int durationTurns = 0;

        // ── 강화 규칙 (5번째 자리: 짝수=강화 전 / 홀수=강화 후) ──

        /// <summary>강화된 아군인지 여부 (allyCode 5번째 자리가 홀수)</summary>
        public bool IsUpgraded => allyCode % 2 == 1;

        /// <summary>강화 후 코드 반환. 이미 강화된 경우 그대로 반환.</summary>
        public int GetUpgradedCode() => IsUpgraded ? allyCode : allyCode + 1;

        [Header("Prefab")]
        [Tooltip("null이면 CombatController의 unitPrefab을 사용합니다.")]
        public GameObject allyPrefab;

        [Header("Audio/Visual")]
        public AudioClip attackSound;
        public AudioClip deathSound;
        public GameObject hitVFX;
    }
}
