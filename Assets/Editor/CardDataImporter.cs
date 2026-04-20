using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using DeckRoguelike.Cards;
using DeckRoguelike.Core;
using LocalizationDatabase = DeckRoguelike.Core.LocalizationDatabase;

/// <summary>
/// CSV → CardData ScriptableObject 자동 생성 툴
/// 메뉴: Tools > Card Data > Import from CSV
///
/// [CSV 포맷]
/// effects  : 각 효과를 | 로 구분
///            효과 포맷: effectId:value:targeting:useAbsoluteCoords:rangeOffsets
///              - effectId          : EffectType enum 이름, 커스텀 ID, 또는 summon_{allyCode}
///                                    summon_30100 → SummonAlly + AllyData 자동 참조
///                                    Strength → Custom + customEffectId="Strength"
///                                    Damage / Block / Move 등 → 해당 EffectType
///              - useAbsoluteCoords : true / false (생략 시 false)
///              - rangeOffsets      : col.row 형식, 여러 개면 / 로 구분  예) 1.0/2.0/3.0
///            예) Damage:6:EnemyOnly:false:1.0/2.0/3.0
///                Block:5::::                              (targeting 빈칸 = None)
///                Move:1:AnyCell:false:0.1/0.-1/-1.0
///                Strength:2::::
///                summon_30100:0:AnyCell:false:1.0/2.0/3.0
/// bool 필드 : true / false (대소문자 무관)
/// </summary>
public class CardDataImporter : EditorWindow
{
    private const string DefaultTemplatePath = "Assets/Editor/CardDataTemplate.csv";
    private string csvPath      = DefaultTemplatePath;
    private string outputFolder = "Assets/GameResources/Data/Cards";

    [MenuItem("Tools/Card Data/Import from CSV")]
    public static void ShowWindow()
    {
        GetWindow<CardDataImporter>("Card Data Importer");
    }

    private void OnEnable()
    {
        if (File.Exists(DefaultTemplatePath))
            csvPath = DefaultTemplatePath;
    }

    private void OnGUI()
    {
        GUILayout.Label("CSV → CardData 임포터", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        bool templateExists = File.Exists(DefaultTemplatePath);
        if (templateExists)
            EditorGUILayout.HelpBox($"템플릿 감지됨: {DefaultTemplatePath}", MessageType.Info);
        else
            EditorGUILayout.HelpBox("기본 템플릿 없음. 아래에서 생성하거나 파일을 직접 선택하세요.", MessageType.Warning);

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        csvPath = EditorGUILayout.TextField("CSV 파일", csvPath);
        if (GUILayout.Button("찾아보기", GUILayout.Width(70)))
        {
            string selected = EditorUtility.OpenFilePanel("CSV 선택", Application.dataPath, "csv");
            if (!string.IsNullOrEmpty(selected)) csvPath = selected;
        }
        EditorGUILayout.EndHorizontal();

        outputFolder = EditorGUILayout.TextField("출력 폴더", outputFolder);

        EditorGUILayout.Space();

        if (GUILayout.Button("📄 템플릿 CSV 생성 (Assets/Editor/)"))
            CreateTemplate();

        EditorGUILayout.Space();

        bool canImport = !string.IsNullOrEmpty(csvPath) && File.Exists(csvPath);
        GUI.enabled = canImport;
        if (GUILayout.Button("⬇ CSV 임포트", GUILayout.Height(35)))
            ImportCSV();
        GUI.enabled = true;

        if (!canImport && !string.IsNullOrEmpty(csvPath))
            EditorGUILayout.HelpBox("CSV 파일을 찾을 수 없습니다.", MessageType.Error);
    }

    // ─────────────────────────────────────────────
    // 템플릿 CSV 생성
    // ─────────────────────────────────────────────
    private void CreateTemplate()
    {
        string path = DefaultTemplatePath;

        var sb = new System.Text.StringBuilder();

        // 헤더 (keywords: 파이프 구분 플래그, 예) Exhausts|Innate)
        sb.AppendLine(
            "cardCode," +
            "cardName," +
            "energyCost," +
            "description," +
            "effects," +
            "keywords"
        );

        // 효과 포맷: EffectType:value:targeting:rangeOffsets
        // rangeOffsets: col.row / col.row  (생략 시 targeting 기준 모든 유효 셀 자동 허용)

        // 포맷: cardCode(5자리),cardName,energyCost,description,effects,keywords
        // cardType·rarity는 cardCode에서 자동 파생 (CardCodeHelper)
        // C=클래스 T=타입(1공격2이동3스킬4파워) R=희귀도(1일반2고급3희귀4전설) N=번호 O=강화(짝수=전,홀수=후)
        // keywords: CardKeyword 플래그를 | 로 구분  예) Exhausts  /  Exhausts|Innate  / 없으면 빈칸

        // 전사 공격 카드
        sb.AppendLine("21100,일격,1,{D} 피해를 입힙니다.,"
            + "Damage:6:Enemy:1.0/2.0/3.0" + ",");
        sb.AppendLine("21101,일격+,1,{D} 피해를 입힙니다.,"
            + "Damage:9:Enemy:1.0/2.0/3.0" + ",");
        sb.AppendLine("21120,강타,2,{D} 피해를 입히고 {B} 방어도를 얻습니다.,"
            + "Damage:8:Enemy:1.0/2.0/3.0|Block:3" + ",");
        sb.AppendLine("21140,휩쓸기,1,범위 내 모든 적에게 {D} 피해.,"
            + "Damage:8:All:1.0/2.0/3.0/1.1/2.1/3.1/1.-1/2.-1/3.-1" + ",");
        sb.AppendLine("21200,돌진,2,뒤로 이동 후 앞 범위 공격.,"
            + "Move:1:Any:-1.0/-1.1/-1.-1|Damage:10:AllInRange:1.0/2.0/3.0" + ",");
        sb.AppendLine("21300,수류탄,2,선택한 셀 주변에 {D} 피해.,"
            + "area_damage:6:Any:1.0/2.0/3.0" + ",Exhausts");

        // 전사 이동 카드
        sb.AppendLine("22100,스텝,1,범위 내 칸으로 이동합니다.,"
            + "Move:1:Any:0.1/0.-1/-1.0/-1.1/-1.-1" + ",");

        // 전사 스킬 카드
        sb.AppendLine("23100,수비,1,{B} 방어도를 획득합니다.,"
            + "Block:5" + ",");
        sb.AppendLine("23101,수비+,1,{B} 방어도를 획득합니다.,"
            + "Block:8" + ",");
        sb.AppendLine("23120,아군 소환,2,빈 칸에 아군을 소환합니다.,"
            + "summon_30100:0:Any:1.0/2.0/3.0/1.1/2.1/3.1/1.-1/2.-1/3.-1" + ",Exhausts");
        sb.AppendLine("23140,발놀림,1,민첩을 2 증가시킵니다.,"
            + "gain_dexterity:2" + ",Exhausts");

        // 전사 파워 카드
        sb.AppendLine("24100,근력 강화,1,힘을 2 증가시킵니다.,"
            + "Strength:2" + ",Exhausts");
        sb.AppendLine("24200,전투 황홀경,1,힘을 3 증가하고 카드 1장을 드로우합니다.,"
            + "Strength:3|Draw:1" + ",Exhausts");

        // 중립 스킬 카드
        sb.AppendLine("13100,속사,1,카드를 {Draw}장 드로우합니다.,"
            + "Draw:2" + ",");
        sb.AppendLine("13200,재충전,0,에너지를 {E} 획득합니다.,"
            + "Energy:1" + ",Exhausts");

        File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        AssetDatabase.Refresh();
        csvPath = path;
        EditorUtility.RevealInFinder(path);
        Debug.Log($"[CardDataImporter] 템플릿 생성 완료: {path}");
    }

    // ─────────────────────────────────────────────
    // CSV 임포트
    // ─────────────────────────────────────────────
    private void ImportCSV()
    {
        string[] lines = File.ReadAllLines(csvPath, System.Text.Encoding.UTF8);
        if (lines.Length < 2)
        {
            EditorUtility.DisplayDialog("오류", "데이터가 없습니다 (헤더만 있거나 빈 파일).", "확인");
            return;
        }

        EnsureFolderExists(outputFolder);

        // LocalizationDatabase 로드
        var locDb = AssetDatabase.LoadAssetAtPath<LocalizationDatabase>("Assets/GameResources/Data/LocalizationDatabase.asset");
        if (locDb == null)
            Debug.LogWarning("[CardDataImporter] LocalizationDatabase를 찾을 수 없습니다. 로컬라이즈 항목이 저장되지 않습니다.");

        string[] headers = ParseLine(lines[0]);
        var col = new Dictionary<string, int>();
        for (int i = 0; i < headers.Length; i++)
            col[headers[i].Trim()] = i;

        int created = 0, updated = 0, skipped = 0;

        for (int row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;

            string[] f = ParseLine(lines[row]);

            string cardId = GetField(f, col, "cardCode");
            if (string.IsNullOrEmpty(cardId)) { skipped++; continue; }

            string assetPath = $"{outputFolder}/{cardId}.asset";
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(assetPath);
            bool isNew = card == null;

            if (isNew)
                card = ScriptableObject.CreateInstance<CardData>();

            // ── 기본 정보 (cardType·rarity는 cardCode에서 자동 파생)
            card.cardCode    = ParseInt(GetField(f, col, "cardCode"), 0);
            card.cardName    = GetField(f, col, "cardName");
            card.description = GetField(f, col, "description");

            // ── 코스트 ("x" → -1, X 코스트 카드)
            string costRaw = GetField(f, col, "energyCost").Trim().ToLower();
            card.energyCost = costRaw == "x" ? -1 : ParseInt(costRaw, 1);

            // ── 효과
            card.effects = ParseEffects(GetField(f, col, "effects"));

            // ── 키워드 (파이프 구분 플래그, 예) Exhausts|Innate)
            card.keywords = ParseKeywords(GetField(f, col, "keywords"));

            if (isNew) { AssetDatabase.CreateAsset(card, assetPath); created++; }
            else       { EditorUtility.SetDirty(card); updated++; }

            // ── LocalizationDatabase에 card_name / card_desc 항목 갱신
            if (locDb != null)
            {
                SetLocEntry(locDb, $"card_name_{card.cardCode}", card.cardName);
                SetLocEntry(locDb, $"card_desc_{card.cardCode}", card.description);
            }
        }

        if (locDb != null)
        {
            EditorUtility.SetDirty(locDb);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"생성: {created}개\n업데이트: {updated}개\n건너뜀: {skipped}개";
        EditorUtility.DisplayDialog("임포트 완료", msg, "확인");
        Debug.Log($"[CardDataImporter] {msg}");
    }

    /// <summary>LocalizationDatabase에서 해당 code 항목을 찾아 ko 필드를 갱신하거나 새 항목을 추가합니다.</summary>
    private static void SetLocEntry(LocalizationDatabase db, string code, string koValue)
    {
        if (string.IsNullOrEmpty(koValue)) return;

        var entry = db.entries.Find(e => e.code == code);
        if (entry == null)
        {
            entry = new LocalizationDatabase.Entry { code = code };
            db.entries.Add(entry);
        }
        entry.ko = koValue;
    }

    // ─────────────────────────────────────────────
    // 파서 유틸리티
    // ─────────────────────────────────────────────

    /// <summary>Assets/Foo/Bar 형태의 경로를 단계별로 생성</summary>
    private static void EnsureFolderExists(string path)
    {
        string[] parts = path.TrimEnd('/').Split('/');
        string current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    /// <summary>RFC 4180 기준 CSV 한 줄 파싱 (따옴표 필드 지원)</summary>
    private static string[] ParseLine(string line)
    {
        var result  = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                { current.Append('"'); i++; }
                else
                    inQuote = !inQuote;
            }
            else if (c == ',' && !inQuote)
            { result.Add(current.ToString()); current.Clear(); }
            else
                current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    private static string GetField(string[] fields, Dictionary<string, int> colMap, string key)
    {
        if (!colMap.TryGetValue(key, out int idx)) return "";
        if (idx >= fields.Length) return "";
        return fields[idx].Trim();
    }

    /// <summary>
    /// Effects 파싱
    /// 포맷: EffectType:value:targeting:rangeOffsets
    ///   rangeOffsets      : col.row/col.row (/ 구분, . 이 col·row 구분자)
    ///                       생략 시 무제한 사거리 (targeting 기준으로 모든 유효 셀 자동 허용)
    ///   customEffectId    : effectType=Custom 전용
    ///   allyId            : effectType=SummonAlly 전용
    /// 여러 효과는 | 로 구분
    /// </summary>
    private static List<CardEffect> ParseEffects(string raw)
    {
        var list = new List<CardEffect>();
        if (string.IsNullOrEmpty(raw)) return list;

        foreach (var part in raw.Split('|'))
        {
            string trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            string[] tokens = trimmed.Split(':');
            if (tokens.Length < 2) continue;

            // 토큰 0 파싱:
            //   summon_30100  → SummonAlly, allyCode=30100
            //   Strength → Custom, customEffectId="Strength"
            //   Damage 등     → 해당 EffectType
            var effect = new CardEffect { value = ParseInt(tokens[1], 0) };
            string typeToken = tokens[0].Trim();

            if (typeToken.StartsWith("summon_", System.StringComparison.OrdinalIgnoreCase))
            {
                effect.effectType     = EffectType.SummonAlly;
                effect.customEffectId = typeToken.Substring("summon_".Length); // allyCode 저장
            }
            else if (System.Enum.TryParse<EffectType>(typeToken, true, out var parsedType))
            {
                effect.effectType = parsedType;
            }
            else
            {
                effect.effectType     = EffectType.Custom;
                effect.customEffectId = typeToken;
            }

            // targeting (토큰 2)
            if (tokens.Length >= 3)
                effect.targeting = ParseEnum(tokens[2], TargetType.Self);

            // rangeOffsets (토큰 3) — 비어있으면 무제한 사거리
            if (tokens.Length >= 4 && !string.IsNullOrEmpty(tokens[3]))
                effect.rangeOffsets = ParseDotOffsets(tokens[3]);

            list.Add(effect);
        }
        return list;
    }

    /// <summary>
    /// "col.row/col.row" 형식의 오프셋 배열 파싱
    /// 예) "1.0/2.0/-1.1/0.-1"
    /// </summary>
    private static RangeOffset[] ParseDotOffsets(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return new RangeOffset[0];

        var list = new List<RangeOffset>();
        foreach (var entry in raw.Split('/'))
        {
            string trimmed = entry.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // col.row 에서 마지막 '.' 기준으로 분리 (음수 row 지원)
            int dotIdx = trimmed.LastIndexOf('.');
            if (dotIdx <= 0) continue;

            string colStr = trimmed.Substring(0, dotIdx);
            string rowStr = trimmed.Substring(dotIdx + 1);

            list.Add(new RangeOffset
            {
                col = ParseInt(colStr, 0),
                row = ParseInt(rowStr, 0),
            });
        }
        return list.ToArray();
    }

    private static T ParseEnum<T>(string value, T fallback) where T : struct, System.Enum
    {
        if (System.Enum.TryParse<T>(value, true, out T result)) return result;
        Debug.LogWarning($"[CardDataImporter] 알 수 없는 enum 값: '{value}' ({typeof(T).Name}) → {fallback} 사용");
        return fallback;
    }

    private static int ParseInt(string value, int fallback)
        => int.TryParse(value, out int r) ? r : fallback;

    private static bool ParseBool(string value)
        => value.Trim().ToLower() == "true";

    private static CardKeyword ParseKeywords(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return CardKeyword.None;
        var result = CardKeyword.None;
        foreach (var part in raw.Split('|'))
        {
            if (System.Enum.TryParse<CardKeyword>(part.Trim(), true, out var kw))
                result |= kw;
        }
        return result;
    }
}
