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
///            효과 포맷: effectId:value:targeting:rangeOffsets:useAbsoluteCoords
///              - effectId          : EffectType enum 이름, 커스텀 ID, 또는 summon_{allyCode}
///                                    summon_30100 → Custom (customEffectId="summon_30100"), AllyData 자동 참조
///                                    Strength → Custom + customEffectId="Strength"
///                                    Damage / Block / Move 등 → 해당 EffectType
///              - rangeOffsets      : col.row 형식, 여러 개면 / 로 구분  예) 1.0/2.0/3.0
///              - useAbsoluteCoords : true / false (생략 시 false, 마지막 필드)
///            예) Damage:6:EnemyOnly:1.0/2.0/3.0
///                Block:5::::                              (targeting 빈칸 = None)
///                Move:1:AnyCell:0.1/0.-1/-1.0
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
        // imagePath: 카드 효과 전용 이미지 (소멸배기 칼 등). '/'없으면 Sprites/Combat/Player/{값} 으로 해석.
        sb.AppendLine(
            "cardCode," +
            "cardName," +
            "description," +
            "effects," +
            "keywords," +
            "imagePath"
        );

        // 효과 포맷: EffectType:value:targeting:rangeOffsets:useAbsoluteCoords
        // rangeOffsets: col.row / col.row  (생략 시 targeting 기준 모든 유효 셀 자동 허용)
        // useAbsoluteCoords: true/false (생략 시 false)

        // 포맷: cardCode(5자리),cardName,description,effects,keywords
        // cardType·rarity는 cardCode에서 자동 파생 (CardData.CardTypeFromCode)
        // C=클래스(1공통/2전사/3거너/4메이지) T=타입(1이동/2액션/3파워) R=희귀도(1일반/2고급/3희귀/4전설) N=번호 O=강화(짝수=전/홀수=후)
        // keywords: CardKeyword 플래그를 | 로 구분  예) Exhausts  /  Exhausts|Innate  / 없으면 빈칸

        // 전사 액션 카드 (T=2)
        sb.AppendLine("22100,일격,1,{D} 피해를 입힙니다.,"
            + "Damage:6:Enemy:1.0/2.0/3.0" + ",");
        sb.AppendLine("22101,일격+,1,{D} 피해를 입힙니다.,"
            + "Damage:9:Enemy:1.0/2.0/3.0" + ",");
        sb.AppendLine("22120,강타,2,{D} 피해를 입히고 {B} 방어도를 얻습니다.,"
            + "Damage:8:Enemy:1.0/2.0/3.0|Block:3" + ",");
        sb.AppendLine("22140,휩쓸기,1,범위 내 모든 적에게 {D} 피해.,"
            + "Damage:8:All:1.0/2.0/3.0/1.1/2.1/3.1/1.-1/2.-1/3.-1" + ",");
        sb.AppendLine("22200,돌진,2,뒤로 이동 후 앞 범위 공격.,"
            + "Move:1:Any:-1.0/-1.1/-1.-1|Damage:10:AllInRange:1.0/2.0/3.0" + ",");
        sb.AppendLine("22300,수류탄,2,선택한 셀 주변에 {D} 피해.,"
            + "area_damage:6:Any:1.0/2.0/3.0" + ",Exhausts");

        // 전사 이동 카드 (T=1)
        sb.AppendLine("21100,스텝,1,범위 내 칸으로 이동합니다.,"
            + "Move:1:Any:0.1/0.-1/-1.0/-1.1/-1.-1" + ",");

        // 전사 파워 카드 (T=3)
        sb.AppendLine("23100,수비,1,{B} 방어도를 획득합니다.,"
            + "Block:5" + ",");
        sb.AppendLine("23101,수비+,1,{B} 방어도를 획득합니다.,"
            + "Block:8" + ",");
        sb.AppendLine("23140,발놀림,1,민첩을 2 증가시킵니다.,"
            + "gain_dexterity:2" + ",Exhausts");
        sb.AppendLine("23200,근력 강화,1,힘을 2 증가시킵니다.,"
            + "Strength:2" + ",Exhausts");
        sb.AppendLine("23300,전투 황홀경,1,힘을 3 증가하고 카드 1장을 드로우합니다.,"
            + "Strength:3|Draw:1" + ",Exhausts");
        // 아군 소환 효과는 파워 카드(T=3)에 Custom 효과(summon_{code})로 사용 가능
        sb.AppendLine("23400,아군 소환,1,빈 칸에 아군을 소환합니다.,"
            + "summon_30100:0:Any:1.0/2.0/3.0/1.1/2.1/3.1/1.-1/2.-1/3.-1" + ",Exhausts");

        // 공통 파워 카드 (T=3)
        sb.AppendLine("13100,속사,1,카드를 {Draw}장 드로우합니다.,"
            + "Draw:2" + ",");

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
        var csvCardCodes = new HashSet<int>();

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
            card.description = GetField(f, col, "description").Replace("\\n", "\n");

            // ── 효과
            card.effects = ParseEffects(GetField(f, col, "effects"));

            // ── 키워드 (파이프 구분 플래그, 예) Exhausts|Innate)
            card.keywords = ParseKeywords(GetField(f, col, "keywords"));

            // ── 이미지 주소 (소멸배기 칼 등 효과 전용 스프라이트)
            card.imagePath = GetField(f, col, "imagePath");

            if (isNew) { AssetDatabase.CreateAsset(card, assetPath); created++; }
            else       { EditorUtility.SetDirty(card); updated++; }

            csvCardCodes.Add(card.cardCode);

            // ── LocalizationDatabase에 card_name / card_desc 항목 갱신
            if (locDb != null)
            {
                SetLocEntry(locDb, $"card_name_{card.cardCode}", card.cardName);
                SetLocEntry(locDb, $"card_desc_{card.cardCode}", card.description);
            }
        }

        // ── CSV에 없는 카드 에셋 삭제 (확인창에서 최종 승인)
        int deleted = DeleteAssetsNotInCsv(csvCardCodes, locDb);

        if (locDb != null)
        {
            EditorUtility.SetDirty(locDb);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"생성: {created}개\n업데이트: {updated}개\n건너뜀: {skipped}개\n삭제: {deleted}개";
        EditorUtility.DisplayDialog("임포트 완료", msg, "확인");
        Debug.Log($"[CardDataImporter] {msg}");
    }

    /// <summary>
    /// outputFolder의 CardData.asset 중 cardCode가 csvCardCodes에 없는 항목을 삭제합니다.
    /// 사용자에게 미리 대상 목록을 보여주고 승인받은 뒤 실제로 삭제합니다.
    /// LocalizationDatabase의 card_name_/card_desc_ 항목도 함께 제거합니다.
    /// </summary>
    private int DeleteAssetsNotInCsv(HashSet<int> csvCardCodes, LocalizationDatabase locDb)
    {
        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            Debug.LogWarning($"[CardDataImporter] 출력 폴더가 존재하지 않습니다: {outputFolder}");
            return 0;
        }

        string[] guids = AssetDatabase.FindAssets("t:CardData", new[] { outputFolder });
        var orphans = new List<(string path, int code, string name)>();
        foreach (var guid in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var c = AssetDatabase.LoadAssetAtPath<CardData>(p);
            if (c == null) continue;
            if (csvCardCodes.Contains(c.cardCode)) continue;
            orphans.Add((p, c.cardCode, c.cardName));
        }

        if (orphans.Count == 0) return 0;

        const int previewMax = 15;
        var preview = new System.Text.StringBuilder();
        for (int i = 0; i < Mathf.Min(previewMax, orphans.Count); i++)
            preview.AppendLine($"  {orphans[i].code} {orphans[i].name}");
        if (orphans.Count > previewMax)
            preview.AppendLine($"  ... 외 {orphans.Count - previewMax}개");

        bool confirm = EditorUtility.DisplayDialog(
            "CSV에 없는 카드 삭제",
            $"CSV에 없는 카드 {orphans.Count}개를 삭제합니다.\n\n{preview}\n계속할까요?",
            "삭제",
            "취소");
        if (!confirm) return 0;

        int deleted = 0;
        foreach (var o in orphans)
        {
            if (AssetDatabase.DeleteAsset(o.path))
            {
                if (locDb != null)
                {
                    locDb.entries.RemoveAll(e =>
                        e != null && (e.code == $"card_name_{o.code}" || e.code == $"card_desc_{o.code}"));
                }
                deleted++;
            }
            else
            {
                Debug.LogWarning($"[CardDataImporter] 삭제 실패: {o.path}");
            }
        }
        return deleted;
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
    /// 포맷: EffectType:value:targeting:rangeOffsets:useAbsoluteCoords
    ///   rangeOffsets      : col.row/col.row (/ 구분, . 이 col·row 구분자)
    ///   useAbsoluteCoords : true/false (생략 시 false)
    ///                       생략 시 무제한 사거리 (targeting 기준으로 모든 유효 셀 자동 허용)
    ///   customEffectId    : effectType=Custom 전용
    ///   allyId            : summon_{allyCode} 커스텀 효과 전용
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
            //   summon_30100  → Custom, customEffectId="summon_30100" (BoardController가 prefix로 분기)
            //   Strength → Custom, customEffectId="Strength"
            //   Damage 등     → 해당 EffectType
            // value 토큰은 "6.20" 같은 다중값을 위해 원본도 valueRaw에 보관
            string valueToken = tokens[1];
            int parsedValue = ParseInt(valueToken.Contains(".") ? valueToken.Split('.')[0] : valueToken, 0);
            var effect = new CardEffect { value = parsedValue, valueRaw = valueToken };
            string typeToken = tokens[0].Trim();

            if (System.Enum.TryParse<EffectType>(typeToken, true, out var parsedType))
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

            // useAbsoluteCoords (토큰 4) — 생략 시 false
            if (tokens.Length >= 5 && !string.IsNullOrEmpty(tokens[4]))
                effect.useAbsoluteCoords = tokens[4].Trim().Equals("true", System.StringComparison.OrdinalIgnoreCase);

            list.Add(effect);
        }
        return list;
    }

    /// <summary>
    /// "col.row/col.row" 형식의 오프셋 배열 파싱
    /// 예) "1.0/2.0/-1.1/0.-1"
    ///
    /// BFS 약식: '.' 없이 순수 정수 N 만 적으면 Manhattan 거리 ≤ N 인 모든 셀 (중심 제외) 로 확장합니다.
    /// 예) "1" → 0.1/0.-1/-1.0/1.0  (4셀)
    ///     "2" → BFS-2 (12셀)
    /// </summary>
    private static RangeOffset[] ParseDotOffsets(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return new RangeOffset[0];

        // BFS 약식: 점이 없는 양의 정수 → Manhattan 거리 ≤ N 셀
        string trimmedRaw = raw.Trim();
        if (!trimmedRaw.Contains('.') && !trimmedRaw.Contains('/') &&
            int.TryParse(trimmedRaw, out int bfsN) && bfsN > 0)
            return BuildBfsRange(bfsN);

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

    /// <summary>
    /// Manhattan 거리 ≤ n 의 모든 셀 (중심 0,0 제외) 을 RangeOffset 배열로 반환.
    /// </summary>
    private static RangeOffset[] BuildBfsRange(int n)
    {
        var list = new List<RangeOffset>();
        for (int c = -n; c <= n; c++)
        for (int r = -n; r <= n; r++)
        {
            if (c == 0 && r == 0) continue;
            if (System.Math.Abs(c) + System.Math.Abs(r) <= n)
                list.Add(new RangeOffset { col = c, row = r });
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
