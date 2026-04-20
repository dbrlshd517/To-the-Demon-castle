using System.Collections.Generic;
using UnityEngine;

namespace DeckRoguelike.Relic
{
    /// <summary>
    /// RelicImporter가 CSV로부터 생성하는 런타임 유물 데이터베이스.
    /// Assets/Resources/RelicDatabase.asset 에 저장됩니다.
    /// RelicLibrary.RegisterAll()이 이 에셋을 로드하여 사용합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "DeckRoguelike/Relic Database", fileName = "RelicDatabase")]
    public class RelicDatabase : ScriptableObject
    {
        public List<RelicData> relics = new List<RelicData>();
    }
}
