using UnityEngine;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 전투 조우 데이터 (적 그룹 구성)
    ///
    /// [encounterId 5자리 구조]  D A N X X
    ///   D (1자리) : 게임 난이도   1=normal  2=hard  3=hell
    ///   A (2자리) : 액트 번호     1~9
    ///   N (3자리) : 액트 내 난이도  1~8=일반  9=보스
    ///   XX(4-5자리): 개별 번호    00~99
    ///
    ///   예) 11100 = normal / 액트1 / 난이도1 / 00번
    ///       11900 = normal / 액트1 / 보스    / 00번
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

        /// <summary>게임 난이도 자리 (1=normal 2=hard 3=hell)</summary>
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

    public enum PlacementType
    {
        Fixed,
        Random,
        Opposite,
    }

    [System.Serializable]
    public class EnemySlot
    {
        public EnemyData enemyData;
        public PlacementType placementType;
        [Tooltip("Fixed: 그리드 열, Random/Opposite: 사용 안 함")]
        public int col;
        [Tooltip("Fixed: 그리드 행, Random/Opposite: 사용 안 함")]
        public int row;
        [Tooltip("Random 배치 시 플레이어로부터의 맨해튼 거리 제한")]
        public int placementRange;
        [Tooltip("Opposite 배치 시 사용. 플레이어 사분면 반대 코너에서 시작해 행→열 순으로 매긴 1-based 번호. " +
                 "예: 1 = 반대 코너 셀, 행 다 채우면 한 행 플레이어 쪽으로 이동.")]
        public int oppositePosition;
        [Tooltip("같은 CSV 엔트리(예: 11901:5.random:3)에서 펼쳐진 슬롯에 부여되는 그룹 ID. " +
                 "SnakeBehavior 같은 군집 행동이 \"한 번의 소환 호출 = 한 그룹\"으로 묶을 때 사용. " +
                 "엔트리마다 1, 2, 3 ... 으로 증가. 0이면 미지정.")]
        public int spawnGroupId;
    }

    public enum EncounterType
    {
        Normal,
        DangerCombat,
        Boss,
        Event
    }
}
