using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using DeckRoguelike.Combat;

/// <summary>
/// CSV -> EnemyEncounter ScriptableObject 자동 생성 툴
/// 메뉴: Tools > Enemy Data > Import Encounters from CSV
///
/// [encounterCode 5자리 구조]  D A N X X
///   D (1자리) : 게임 난이도   1=easy  2=normal  3=hard  4=hell
///   A (2자리) : 액트 번호     1~9
///   N (3자리) : 액트 내 난이도  1~8=일반  9=보스
///   XX(4-5자리): 개별 번호    00~99
///
///   예) 11100 = easy / 액트1 / 난이도1 / 00번
///       11900 = easy / 액트1 / 보스    / 00번
///       21200 = normal / 액트1 / 난이도2 / 00번
///
///   ※ 액트 난이도: 전투 2회 클리어마다 +1 (1부터 시작), 다음 액트 진입 시 초기화
///   ※ type은 코드에서 자동 추론됨 (N자리 9 = Boss, 나머지 = Normal)
///
/// [CSV 포맷]
/// encounterCode    : 위 규칙의 5자리 코드 (파일명으로 사용)
/// enemies          : enemyCode:col.row 형식, 여러 적은 / 로 구분
///                    예) 10100:1.0/10200:2.1/10100:3.-1
///                    col: 그리드 열, row: 그리드 행 (음수 허용)
/// spawnWeight      : 0.0 ~ 1.0 (높을수록 자주 등장)
/// goldMin/goldMax  : 전투 보상 골드 범위
/// cardRewardChance : 카드 보상 확률 0.0 ~ 1.0
///
/// ※ EnemyData 에셋이 먼저 임포트되어 있어야 합니다 (Tools > Enemy Data > Import from CSV)
/// ※ enemyCode 로 Assets/ScriptableObjects/Enemies/{code}.asset 을 자동 참조합니다
/// </summary>
public class EnemyEncounterImporter : EditorWindow
{
    private const string DefaultTemplatePath  = "Assets/Editor/EnemyEncounterTemplate.csv";
    private const string EnemyAssetFolder     = "Assets/GameResources/Data/Enemies";
    private string csvPath      = DefaultTemplatePath;
    private string outputFolder = "Assets/GameResources/Data/Encounters";

    [MenuItem("Tools/Enemy Data/Import Encounters from CSV")]
    public static void ShowWindow()
    {
        GetWindow<EnemyEncounterImporter>("Encounter Importer");
    }

    private void OnEnable()
    {
        if (File.Exists(DefaultTemplatePath))
            csvPath = DefaultTemplatePath;
    }

    private void OnGUI()
    {
        GUILayout.Label("CSV -> EnemyEncounter 임포터", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "enemies 포맷: enemyCode:col.row / enemyCode:col.row\n" +
            "예) 10100:1.0/10200:2.1  →  코드 10100은 (col=1,row=0), 10200은 (col=2,row=1)\n" +
            "EnemyData 에셋을 먼저 임포트해야 참조가 연결됩니다.",
            MessageType.Info);

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

        if (GUILayout.Button("템플릿 CSV 생성 (Assets/Editor/)"))
            CreateTemplate();

        EditorGUILayout.Space();

        bool canImport = !string.IsNullOrEmpty(csvPath) && File.Exists(csvPath);
        GUI.enabled = canImport;
        if (GUILayout.Button("CSV 임포트", GUILayout.Height(35)))
            ImportCSV();
        GUI.enabled = true;

        if (!canImport && !string.IsNullOrEmpty(csvPath))
            EditorGUILayout.HelpBox("CSV 파일을 찾을 수 없습니다.", MessageType.Error);
    }

    private void CreateTemplate()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("encounterCode,encounterName,enemies");
        sb.AppendLine("11100,슬라임 무리,10100:1.0/10100:2.0/10100:3.0");
        sb.AppendLine("11200,해골 군단,10200:1.0/10200:3.0");
        sb.AppendLine("11800,석상의 시험,10300:2.0");
        sb.AppendLine("11900,악마 군주,10400:2.0");

        File.WriteAllText(DefaultTemplatePath, sb.ToString(), new System.Text.UTF8Encoding(true));
        AssetDatabase.Refresh();
        csvPath = DefaultTemplatePath;
        EditorUtility.RevealInFinder(DefaultTemplatePath);
        Debug.Log($"[EnemyEncounterImporter] 템플릿 생성 완료: {DefaultTemplatePath}");
    }

    private void ImportCSV()
    {
        string[] lines = File.ReadAllLines(csvPath, System.Text.Encoding.UTF8);
        if (lines.Length < 2)
        {
            EditorUtility.DisplayDialog("오류", "데이터가 없습니다 (헤더만 있거나 빈 파일).", "확인");
            return;
        }

        EnsureFolderExists(outputFolder);

        string[] headers = ParseLine(lines[0]);
        var col = new Dictionary<string, int>();
        for (int i = 0; i < headers.Length; i++)
            col[headers[i].Trim()] = i;

        int created = 0, updated = 0, skipped = 0, missingEnemy = 0;

        for (int row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;

            string[] f = ParseLine(lines[row]);

            string code = GetField(f, col, "encounterCode");
            if (string.IsNullOrEmpty(code)) { skipped++; continue; }

            string assetPath = $"{outputFolder}/{code}.asset";
            EnemyEncounterData enc = AssetDatabase.LoadAssetAtPath<EnemyEncounterData>(assetPath);
            bool isNew = enc == null;
            if (isNew) enc = ScriptableObject.CreateInstance<EnemyEncounterData>();

            enc.encounterId   = code;
            enc.encounterName = GetField(f, col, "encounterName");
            // encounterType: 코드 3번째 자리가 9이면 Boss, 나머지는 Normal
            enc.encounterType   = (code.Length >= 3 && code[2] == '9') ? EncounterType.Boss : EncounterType.Normal;
            enc.isBossEncounter = enc.encounterType == EncounterType.Boss;

            // enemies: "enemyCode:col.row / enemyCode:col.row"
            string enemiesRaw = GetField(f, col, "enemies");
            var slots = ParseEnemySlots(enemiesRaw, ref missingEnemy);
            enc.enemies = slots;

            if (isNew) { AssetDatabase.CreateAsset(enc, assetPath); created++; }
            else       { EditorUtility.SetDirty(enc); updated++; }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"생성: {created}개\n업데이트: {updated}개\n건너뜀: {skipped}개";
        if (missingEnemy > 0)
            msg += $"\n\n경고: EnemyData 못 찾음 {missingEnemy}개 (Console 확인)";
        EditorUtility.DisplayDialog("임포트 완료", msg, "확인");
        Debug.Log($"[EnemyEncounterImporter] {msg}");
    }

    /// <summary>
    /// "enemyCode:col.row/enemyCode:col.row" 파싱 -> EnemySlot[]
    /// col.row 에서 마지막 '.' 기준으로 col / row 분리 (음수 row 지원)
    /// </summary>
    private static EnemySlot[] ParseEnemySlots(string raw, ref int missingCount)
    {
        var list = new List<EnemySlot>();
        if (string.IsNullOrEmpty(raw)) return list.ToArray();

        foreach (var entry in raw.Split('/'))
        {
            string trimmed = entry.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // enemyCode:col.row
            int colonIdx = trimmed.IndexOf(':');
            if (colonIdx < 0) continue;

            string enemyCodeStr = trimmed.Substring(0, colonIdx).Trim();
            string posStr       = trimmed.Substring(colonIdx + 1).Trim();

            // col.row 분리
            int dotIdx = posStr.LastIndexOf('.');
            if (dotIdx <= 0) continue;
            int slotCol = ParseInt(posStr.Substring(0, dotIdx), 0);
            int slotRow = ParseInt(posStr.Substring(dotIdx + 1), 0);

            string enemyAssetPath = $"{EnemyAssetFolder}/{enemyCodeStr}.asset";
            EnemyData enemyData = AssetDatabase.LoadAssetAtPath<EnemyData>(enemyAssetPath);
            if (enemyData == null)
            {
                Debug.LogWarning($"[EnemyEncounterImporter] EnemyData 없음: {enemyAssetPath}");
                missingCount++;
            }

            list.Add(new EnemySlot { enemyData = enemyData, col = slotCol, row = slotRow });
        }
        return list.ToArray();
    }

    // ─── 유틸리티 ───────────────────────────────────

    private static void EnsureFolderExists(string path)
    {
        string[] parts = path.TrimEnd('/').Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

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

    private static T ParseEnum<T>(string value, T fallback) where T : struct, System.Enum
    {
        if (System.Enum.TryParse<T>(value, true, out T result)) return result;
        Debug.LogWarning($"[EnemyEncounterImporter] 알 수 없는 값: '{value}' ({typeof(T).Name}) -> {fallback} 사용");
        return fallback;
    }

    private static int ParseInt(string value, int fallback)
        => int.TryParse(value, out int r) ? r : fallback;

    private static float ParseFloat(string value, float fallback)
        => float.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float r) ? r : fallback;
}
