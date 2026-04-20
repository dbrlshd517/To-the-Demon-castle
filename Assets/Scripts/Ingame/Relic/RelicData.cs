using System;
using UnityEngine;

namespace DeckRoguelike.Relic
{
    /// <summary>
    /// 유물의 정적 데이터.
    ///
    /// [relicCode 규칙 — 3자리]
    ///   1xx = 일반(Common) 유물
    ///   2xx = 고급(Uncommon) 유물
    ///   3xx = 희귀(Rare) 유물
    ///   9xx = 보스 유물
    ///
    /// [필드 순서] relicCode, relicName, description, icon
    /// </summary>
    [Serializable]
    public class RelicData
    {
        public int    relicCode;    // 3자리 코드 (첫째 자리: 1=일반, 2=고급, 3=희귀, 9=보스)
        public string relicName;
        [TextArea]
        public string description;
        public Sprite icon;

        public bool IsBossRelic => relicCode / 100 == 9;

        /// <summary>코드 첫째 자리(1/2/3)를 희귀도 인덱스(0/1/2)로 반환합니다.</summary>
        public int RarityIndex => Mathf.Clamp(relicCode / 100 - 1, 0, 2);
    }
}
