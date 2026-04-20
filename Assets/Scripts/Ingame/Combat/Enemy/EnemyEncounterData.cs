using UnityEngine;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 전투 조우 데이터 (적 그룹 구성)
    ///
    /// [encounterId 5자리 구조]  D A N X X
    ///   D (1자리) : 게임 난이도   1=easy  2=normal  3=hard  4=hell
    ///   A (2자리) : 액트 번호     1~9
    ///   N (3자리) : 액트 내 난이도  1~8=일반  9=보스
    ///   XX(4-5자리): 개별 번호    00~99
    ///
    ///   예) 11100 = easy / 액트1 / 난이도1 / 00번
    ///       11900 = easy / 액트1 / 보스    / 00번
    ///
    ///   ※ 액트 난이도: 전투 2회 클리어마다 +1 (1부터 시작), 다음 액트 진입 시 초기화
    /// </summary>
    [CreateAssetMenu(fileName = "NewEncounter", menuName = "Deck Roguelike/Enemy Encounter")]
    public class EnemyEncounterData : ScriptableObject
    {
        [Header("Encounter Info")]
        public string encounterId;
        public string encounterName;
        public EncounterType encounterType;

        // ── encounterId 코드 파싱 헬퍼 ─────────────────────
        private int CodeDigit(int pos) =>
            encounterId != null && encounterId.Length > pos && char.IsDigit(encounterId[pos])
                ? encounterId[pos] - '0' : 0;

        /// <summary>게임 난이도 자리 (1=easy 2=normal 3=hard 4=hell)</summary>
        public int GameDifficulty => CodeDigit(0);
        /// <summary>액트 번호 자리 (1~9)</summary>
        public int ActFromCode => CodeDigit(1);
        /// <summary>액트 내 난이도 자리 (1~8=일반, 9=보스)</summary>
        public int ActDifficulty => CodeDigit(2);

        [Header("Enemies")]
        public EnemySlot[] enemies;

        [Header("Special")]
        public bool isBossEncounter;
        public AudioClip encounterMusic;
        public Sprite backgroundOverride;
    }

    /// <summary>
    /// 전투에 배치되는 적 한 명의 종류와 그리드 위치.
    /// </summary>
    [System.Serializable]
    public class EnemySlot
    {
        public EnemyData enemyData;
        [Tooltip("그리드 열 (0 = 플레이어 쪽 기준 첫 번째 열)")]
        public int col;
        [Tooltip("그리드 행 (0 = 중앙)")]
        public int row;
    }

    public enum EncounterType
    {
        Normal,
        Elite,
        Boss,
        Event
    }
}
