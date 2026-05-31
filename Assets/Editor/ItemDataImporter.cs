using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using DeckRoguelike.Item;

/// <summary>
/// CSV → ItemDatabase ScriptableObject 임포터
/// 메뉴: Tools > Item Data > Import from CSV
///
/// [CSV 컬럼 순서]
///   itemCode    : 아이템 코드 (2xx=소비, 3xx=장비, 9xx=보스)
///   itemName    : 표시 이름
///   description : 설명 텍스트
///   iconPath    : Sprite 에셋 경로 (Assets/ 기준, 비워두면 null)
///
/// 출력: Assets/GameResources/Data/ItemDatabase.asset
/// </summary>
public class ItemDataImporter : EditorWindow
{
    private const string DefaultTemplatePath = "Assets/Editor/ItemDataTemplate.csv";
    private const string OutputPath          = "Assets/GameResources/Data/ItemDatabase.asset";

    private string csvPath = DefaultTemplatePath;

    [MenuItem("Tools/Item Data/Import from CSV")]
    public static void ShowWindow()
    {
        GetWindow<ItemDataImporter>("Item Data Importer");
    }

    private void OnEnable()
    {
        if (File.Exists(DefaultTemplatePath))
            csvPath = DefaultTemplatePath;
    }

    private void OnGUI()
    {
        GUILayout.Label("CSV → ItemDatabase 임포터", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        bool templateExists = File.Exists(DefaultTemplatePath);
        EditorGUILayout.HelpBox(
            templateExists
                ? $"템플릿 감지됨: {DefaultTemplatePath}"
                : "기본 템플릿 없음. 아래에서 생성하거나 파일을 직접 선택하세요.",
            templateExists ? MessageType.Info : MessageType.Warning);

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        csvPath = EditorGUILayout.TextField("CSV 파일", csvPath);
        if (GUILayout.Button("찾아보기", GUILayout.Width(70)))
        {
            string selected = EditorUtility.OpenFilePanel("CSV 선택", Application.dataPath, "csv");
            if (!string.IsNullOrEmpty(selected))
                csvPath = "Assets" + selected.Substring(Application.dataPath.Length);
        }
        EditorGUILayout.EndHorizontal();

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

        EditorGUILayout.Space();

        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(OutputPath);
        if (db != null)
        {
            EditorGUILayout.LabelField($"현재 DB: 아이템 {db.items?.Count ?? 0}개 ({OutputPath})",
                EditorStyles.miniLabel);
        }
    }

    // ─────────────────────────────────────────────
    // 템플릿 CSV 생성
    // ─────────────────────────────────────────────
    private void CreateTemplate()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("itemCode,itemName,description,iconPath");
        sb.AppendLine("101,회복 포션,최대 체력의 20%를 회복합니다.,");
        sb.AppendLine("202,강화 포션,이번 전투에서 공격력이 2 증가합니다.,");
        sb.AppendLine("203,방어 포션,방어도가 12 증가합니다.,");
        sb.AppendLine("204,속도 포션,이번 턴에 카드를 2장 추가로 드로우합니다.,");
        sb.AppendLine("301,강철 반지,전투 시작 시 방어도 3을 얻습니다.,");
        sb.AppendLine("302,힘의 팔찌,전투 시작 시 힘이 1 증가합니다.,");

        File.WriteAllText(DefaultTemplatePath, sb.ToString(),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        AssetDatabase.Refresh();
        csvPath = DefaultTemplatePath;
        EditorUtility.RevealInFinder(DefaultTemplatePath);
        Debug.Log($"[ItemDataImporter] 템플릿 생성 완료: {DefaultTemplatePath}");
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

        string[] headers = ParseLine(lines[0]);
        var col = new Dictionary<string, int>();
        for (int i = 0; i < headers.Length; i++)
            col[headers[i].Trim()] = i;

        EnsureFolderExists("Assets/GameResources/Data");
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(OutputPath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<ItemDatabase>();
            AssetDatabase.CreateAsset(db, OutputPath);
        }

        var existing = new Dictionary<int, ItemData>();
        if (db.items != null)
            foreach (var it in db.items)
                if (it != null && it.itemCode != 0)
                    existing[it.itemCode] = it;

        var newList = new List<ItemData>();
        int created = 0, updated = 0, skipped = 0;

        for (int row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;

            string[] f = ParseLine(lines[row]);
            int ic = int.TryParse(GetField(f, col, "itemCode"), out int parsedCode) ? parsedCode : 0;
            if (ic == 0) { skipped++; continue; }

            bool isNew = !existing.TryGetValue(ic, out var data);
            if (isNew) data = new ItemData();

            data.itemCode    = ic;
            data.itemName    = GetField(f, col, "itemName");
            data.description = GetField(f, col, "description").Replace("\\n", "\n");

            string iconPath = GetField(f, col, "iconPath");
            if (!string.IsNullOrEmpty(iconPath))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (sprite != null)
                    data.icon = sprite;
                else
                    Debug.LogWarning($"[ItemDataImporter] Sprite를 찾을 수 없습니다: {iconPath}");
            }

            newList.Add(data);
            if (isNew) created++; else updated++;
        }

        db.items = newList;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"생성: {created}개\n업데이트: {updated}개\n건너뜀: {skipped}개";
        EditorUtility.DisplayDialog("임포트 완료", msg, "확인");
        Debug.Log($"[ItemDataImporter] {msg} → {OutputPath}");
    }

    // ─────────────────────────────────────────────
    // 파서 유틸리티
    // ─────────────────────────────────────────────

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
        if (!colMap.TryGetValue(key, out int idx)) return string.Empty;
        if (idx >= fields.Length) return string.Empty;
        return fields[idx].Trim();
    }
}
