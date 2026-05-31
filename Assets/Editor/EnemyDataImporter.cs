using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using DeckRoguelike.Combat;

/// <summary>
/// CSV -> EnemyData ScriptableObject 자동 생성 툴
/// 메뉴: Tools > Enemy Data > Import from CSV
///
/// [CSV 포맷]
/// enemyCode  : 숫자 코드 (파일명으로 사용)
/// behaviorId : EnemyBehaviorRegistry에 등록된 함수 ID (예: enemy_slime)
///
/// ※ 스프라이트/프리팹/사운드는 CSV 설정 불가 → Inspector에서 직접 연결
/// </summary>
public class EnemyDataImporter : EditorWindow
{
    private const string DefaultTemplatePath = "Assets/Editor/EnemyDataTemplate.csv";
    private string csvPath      = DefaultTemplatePath;
    private string outputFolder = "Assets/GameResources/Data/Enemies";

    [MenuItem("Tools/Enemy Data/Import from CSV")]
    public static void ShowWindow()
    {
        GetWindow<EnemyDataImporter>("Enemy Data Importer");
    }

    private void OnEnable()
    {
        if (File.Exists(DefaultTemplatePath))
            csvPath = DefaultTemplatePath;
    }

    private void OnGUI()
    {
        GUILayout.Label("CSV -> EnemyData 임포터", EditorStyles.boldLabel);
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
        sb.AppendLine("enemyCode,enemyName,hp,damage,behaviorId");
        sb.AppendLine("10100,슬라임,30,5,enemy_slime");
        sb.AppendLine("10200,해골 전사,50,8,enemy_skeleton_warrior");
        sb.AppendLine("10300,석상 수호자,80,6,enemy_stone_guardian");
        sb.AppendLine("10400,악마 군주,200,12,enemy_demon_lord");

        File.WriteAllText(DefaultTemplatePath, sb.ToString(), new System.Text.UTF8Encoding(true));
        AssetDatabase.Refresh();
        csvPath = DefaultTemplatePath;
        EditorUtility.RevealInFinder(DefaultTemplatePath);
        Debug.Log($"[EnemyDataImporter] 템플릿 생성 완료: {DefaultTemplatePath}");
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

        int created = 0, updated = 0, skipped = 0;
        var csvCodes = new HashSet<int>();

        for (int row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;

            string[] f = ParseLine(lines[row]);

            string code = GetField(f, col, "enemyCode");
            if (string.IsNullOrEmpty(code)) { skipped++; continue; }

            string assetPath = $"{outputFolder}/{code}.asset";
            EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(assetPath);
            bool isNew = data == null;
            if (isNew) data = ScriptableObject.CreateInstance<EnemyData>();

            data.enemyId       = ParseInt(code, 0);
            data.enemyName     = GetField(f, col, "enemyName");
            data.maxHP         = ParseInt(GetField(f, col, "hp"), 1);
            data.baseDamage    = ParseInt(GetField(f, col, "damage"), 0);
            data.durationTurns = ParseInt(GetField(f, col, "duration"), 0);
            data.gridSize      = ParseSize(GetField(f, col, "size"));

            if (isNew) { AssetDatabase.CreateAsset(data, assetPath); created++; }
            else       { EditorUtility.SetDirty(data); updated++; }

            csvCodes.Add(data.enemyId);
        }

        int deleted = DeleteAssetsNotInCsv(csvCodes);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"생성: {created}개\n업데이트: {updated}개\n건너뜀: {skipped}개\n삭제: {deleted}개";
        EditorUtility.DisplayDialog("임포트 완료", msg, "확인");
        Debug.Log($"[EnemyDataImporter] {msg}");
    }

    /// <summary>
    /// outputFolder의 EnemyData.asset 중 enemyId가 csvCodes에 없는 항목을 삭제합니다.
    /// 사용자에게 미리 대상 목록을 보여주고 승인받은 뒤 실제로 삭제합니다.
    /// </summary>
    private int DeleteAssetsNotInCsv(HashSet<int> csvCodes)
    {
        if (!AssetDatabase.IsValidFolder(outputFolder)) return 0;

        string[] guids = AssetDatabase.FindAssets("t:EnemyData", new[] { outputFolder });
        var orphans = new List<(string path, int code, string name)>();
        foreach (var guid in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var d = AssetDatabase.LoadAssetAtPath<EnemyData>(p);
            if (d == null) continue;
            if (csvCodes.Contains(d.enemyId)) continue;
            orphans.Add((p, d.enemyId, d.enemyName));
        }

        if (orphans.Count == 0) return 0;

        const int previewMax = 15;
        var preview = new System.Text.StringBuilder();
        for (int i = 0; i < Mathf.Min(previewMax, orphans.Count); i++)
            preview.AppendLine($"  {orphans[i].code} {orphans[i].name}");
        if (orphans.Count > previewMax)
            preview.AppendLine($"  ... 외 {orphans.Count - previewMax}개");

        bool confirm = EditorUtility.DisplayDialog(
            "CSV에 없는 적 삭제",
            $"CSV에 없는 적 {orphans.Count}개를 삭제합니다.\n\n{preview}\n계속할까요?",
            "삭제",
            "취소");
        if (!confirm) return 0;

        int deleted = 0;
        foreach (var o in orphans)
        {
            if (AssetDatabase.DeleteAsset(o.path)) deleted++;
            else Debug.LogWarning($"[EnemyDataImporter] 삭제 실패: {o.path}");
        }
        return deleted;
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

    private static int ParseInt(string value, int fallback)
        => int.TryParse(value, out int r) ? r : fallback;

    private static float ParseFloat(string value, float fallback)
        => float.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float r) ? r : fallback;

    /// <summary>
    /// size 컬럼 파싱. "W.H" / "WxH" / "W,H" / "N"(= NxN) 형식 모두 지원.
    /// 빈 값이면 (1,1).
    /// 예: "2.2" → (2,2),  "3x1" → (3,1),  "2" → (2,2),  "" → (1,1)
    /// </summary>
    private static Vector2Int ParseSize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new Vector2Int(1, 1);
        string v = value.Trim();
        char[] seps = new[] { '.', 'x', 'X', ',', ':', '*' };
        int sepIdx = v.IndexOfAny(seps);
        if (sepIdx < 0)
        {
            int n = ParseInt(v, 1);
            n = Mathf.Max(1, n);
            return new Vector2Int(n, n);
        }
        string ws = v.Substring(0, sepIdx);
        string hs = v.Substring(sepIdx + 1);
        int w = Mathf.Max(1, ParseInt(ws, 1));
        int h = Mathf.Max(1, ParseInt(hs, 1));
        return new Vector2Int(w, h);
    }
}
