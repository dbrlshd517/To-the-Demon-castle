using System;
using UnityEngine;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 툴팁 패널 한 블록의 데이터.
    ///
    /// [UIType 규칙 (code 첫 자리)]
    ///   1xx = Object UI     → 이름 + 설명 (아이콘 없음)
    ///   2xx = Card UI       → 이름 + 설명 (아이콘 없음)
    ///   3xx = Menu UI       → 이름 + 설명 (아이콘 없음)
    ///   4xx = Character UI  → 아이콘 + 이름 + 설명 (플레이어·아군·적 특성/행동 전용)
    ///   code = 0 → 수동 입력, uiType 필드로 직접 지정
    /// </summary>
    [Serializable]
    public class TooltipEntry
    {
        public int    code;            // 0 = 수동, 100이상 = CSV 코드
        public int    uiType;          // code=0일 때 직접 지정 (1/2/3)
        public string name;
        [TextArea(1, 5)]
        public string description;
        public Sprite icon;            // UIType=1(Object)일 때 표시

        public int  UIType   => code > 0 ? code / 100 : uiType;
        public bool ShowIcon => UIType == 4 && icon != null;

        // ── 생성 헬퍼 ─────────────────────────────────────────────────

        public static TooltipEntry Manual(string name, string description, int uiType = 2, Sprite icon = null)
            => new TooltipEntry { name = name, description = description, uiType = uiType, icon = icon };

        public static TooltipEntry ObjectUI(string name, string description, Sprite icon = null)
            => new TooltipEntry { name = name, description = description, uiType = 1, icon = icon };

        public static TooltipEntry CardUI(string name, string description)
            => new TooltipEntry { name = name, description = description, uiType = 2 };

        public static TooltipEntry MenuUI(string name, string description)
            => new TooltipEntry { name = name, description = description, uiType = 3 };

        /// <summary>플레이어·아군·적의 특성 또는 행동 툴팁 (아이콘 표시)</summary>
        public static TooltipEntry CharacterUI(string name, string description, Sprite icon = null)
            => new TooltipEntry { name = name, description = description, uiType = 4, icon = icon };
    }
}
