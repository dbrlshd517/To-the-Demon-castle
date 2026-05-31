using UnityEngine;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 적 캐릭터 데이터
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "Deck Roguelike/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Basic Info")]
        public int    enemyId;
        public string enemyName;
        [TextArea(2, 4)]
        public string description;
        public Sprite enemySprite;

        [Header("Stats")]
        public int maxHP;
        public int baseDamage;

        [Header("Grid Size")]
        [Tooltip("그리드에서 차지하는 셀 크기 (가로, 세로). 기본 1x1. " +
                 "예: (2,2)면 4칸 차지. 앵커(GridPos)는 항상 왼쪽 위 칸 — " +
                 "footprint = (anchor.x+dx, anchor.y-dy), dx∈[0,W-1], dy∈[0,H-1].")]
        public Vector2Int gridSize = new Vector2Int(1, 1);

        [Header("Duration")]
        [Tooltip("0 = 전투 종료까지 유지.")]
        public int durationTurns = 0;

        [Header("Special Abilities")]
        public bool canMultiAttack;
        public int multiAttackCount = 2;
        public bool canSummon;
        public EnemyData summonedEnemy;
        public bool hasThorns;
        public int thornsDamage;

        [Header("Audio/Visual")]
        public AudioClip attackSound;
        public AudioClip deathSound;
        public GameObject hitVFX;
        public RuntimeAnimatorController animatorController;
    }

}
