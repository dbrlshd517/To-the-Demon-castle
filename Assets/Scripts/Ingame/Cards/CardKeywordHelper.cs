using System.Collections.Generic;
using UnityEngine;
using DeckRoguelike.UI;

namespace DeckRoguelike.Cards
{
    /// <summary>
    /// CardKeyword 플래그에 대한 표시 이름·설명·색상을 관리합니다.
    ///
    /// [역할]
    ///  1. ColorizeKeywords() : description 텍스트 안에 키워드 이름이 있으면 TMP 색상 태그로 감쌉니다.
    ///  2. SetupTooltip()     : CardUI GameObject에 TooltipTrigger를 추가/갱신하고
    ///                          활성 키워드 설명을 엔트리로 등록합니다.
    /// </summary>
    public static class CardKeywordHelper
    {
        // ── 키워드 정의 ──────────────────────────────────────────────────
        // (displayName, tooltipDesc, highlightColor)
        private static readonly Dictionary<CardKeyword, KeywordInfo> Data =
            new Dictionary<CardKeyword, KeywordInfo>
        {
            {
                CardKeyword.Exhausts,
                new KeywordInfo(
                    "소멸",
                    "사용 후 덱에서 완전히 제거됩니다.",
                    new Color(1f, 0.55f, 0.2f))   // 주황
            },
            {
                CardKeyword.Ethereal,
                new KeywordInfo(
                    "소각",
                    "턴 종료 시 손패에서 버려지면 덱으로 돌아가지 않고 소멸합니다.",
                    new Color(0.75f, 0.4f, 1f))   // 보라
            },
            {
                CardKeyword.Innate,
                new KeywordInfo(
                    "선천성",
                    "매 전투 시작 시 반드시 초기 손패에 포함됩니다.",
                    new Color(1f, 0.85f, 0.2f))   // 금색
            },
            {
                CardKeyword.Retain,
                new KeywordInfo(
                    "보존",
                    "턴이 끝나도 손패에서 버려지지 않고 유지됩니다.",
                    new Color(0.35f, 0.8f, 1f))   // 하늘
            },
            {
                CardKeyword.Unplayable,
                new KeywordInfo(
                    "사용 불가",
                    "이 카드는 손패에 있어도 플레이할 수 없습니다.",
                    new Color(0.55f, 0.55f, 0.55f)) // 회색
            },
        };

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// description 문자열 안에서 활성 키워드의 표시 이름을 찾아
        /// TMP rich text 색상 태그로 감쌉니다.
        /// </summary>
        public static string ColorizeKeywords(string description, CardKeyword keywords)
        {
            if (string.IsNullOrEmpty(description) || keywords == CardKeyword.None)
                return description;

            string result = description;
            foreach (var kv in Data)
            {
                if (!keywords.HasFlag(kv.Key)) continue;

                string name = kv.Value.DisplayName;
                string hex  = ColorUtility.ToHtmlStringRGB(kv.Value.Color);
                string tagged = $"<color=#{hex}>{name}</color>";
                if (!result.Contains(tagged))
                    result = result.Replace(name, tagged);
            }
            return result;
        }

        /// <summary>
        /// description 끝에 활성 키워드 이름을 줄바꿈 후 append하고 색상 태그를 적용합니다.
        /// </summary>
        public static string BuildDescriptionWithKeywords(string description, CardKeyword keywords)
        {
            if (keywords == CardKeyword.None)
                return ColorizeKeywords(description ?? "", keywords);

            var sb = new System.Text.StringBuilder(description ?? "");
            foreach (var kv in Data)
            {
                if (!keywords.HasFlag(kv.Key)) continue;
                if (sb.Length > 0) sb.Append("\n");
                sb.Append(kv.Value.DisplayName);
            }
            return ColorizeKeywords(sb.ToString(), keywords);
        }

        /// <summary>
        /// target GameObject에 TooltipTrigger를 추가(이미 있으면 재사용)하고
        /// 활성 키워드 엔트리를 등록합니다. 키워드가 없으면 아무것도 하지 않습니다.
        /// </summary>
        public static void SetupTooltip(GameObject target, CardKeyword keywords)
        {
            if (keywords == CardKeyword.None) return;

            var entries = new List<TooltipEntry>();
            foreach (CardKeyword flag in System.Enum.GetValues(typeof(CardKeyword)))
            {
                if (flag == CardKeyword.None)      continue;
                if (!keywords.HasFlag(flag))       continue;
                if (!Data.TryGetValue(flag, out var info)) continue;

                entries.Add(TooltipEntry.ObjectUI(info.DisplayName, info.TooltipDesc));
            }

            if (entries.Count == 0) return;

            var trigger = target.GetComponent<TooltipTrigger>()
                       ?? target.AddComponent<TooltipTrigger>();
            trigger.SetDynamicEntries(entries);
        }

        // ── 내부 데이터 클래스 ────────────────────────────────────────────

        private class KeywordInfo
        {
            public readonly string DisplayName;
            public readonly string TooltipDesc;
            public readonly Color  Color;

            public KeywordInfo(string displayName, string tooltipDesc, Color color)
            {
                DisplayName = displayName;
                TooltipDesc = tooltipDesc;
                Color       = color;
            }
        }
    }
}
