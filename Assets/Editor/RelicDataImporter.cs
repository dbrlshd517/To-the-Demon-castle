using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using DeckRoguelike.Relic;

/// <summary>
/// CSV → RelicDatabase ScriptableObject 임포터
/// 메뉴: Tools > Relic Data > Import from CSV
///
/// [CSV 컬럼 순서]
///   relicCode   : 유물 코드 (RelicLibrary.effectFactories 키와 일치해야 함)
///   relicName   : 표시 이름
///   description : 설명 텍스트
///   iconPath    : Sprite 에셋 경로 (Assets/ 기준, 비워두면 null)
///                 예) Assets/Sprites/Relics/blood_vial.png
///
/// 출력: Assets/GameResources/Data/RelicDatabase.asset
/// </summary>
public class RelicDataImporter : EditorWindow
{
    // CSV 컬럼: relicCode, relicName, description, iconPath
    private const string DefaultTemplatePath = "Assets/Editor/RelicDataTemplate.csv";
    private const string OutputPath          = "Assets/GameResources/Data/RelicDatabase.asset";

    private string csvPath = DefaultTemplatePath;

    [MenuItem("Tools/Relic Data/Import from CSV")]
    public static void ShowWindow()
    {
        GetWindow<RelicDataImporter>("Relic Data Importer");
    }

    private void OnEnable()
    {
        if (File.Exists(DefaultTemplatePath))
            csvPath = DefaultTemplatePath;
    }

    private void OnGUI()
    {
        GUILayout.Label("CSV → RelicDatabase 임포터", EditorStyles.boldLabel);
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

        // 현재 DB 미리보기
        var db = AssetDatabase.LoadAssetAtPath<RelicDatabase>(OutputPath);
        if (db != null)
        {
            EditorGUILayout.LabelField($"현재 DB: 유물 {db.relics?.Count ?? 0}개 ({OutputPath})",
                EditorStyles.miniLabel);
        }
    }

    // ─────────────────────────────────────────────
    // 템플릿 CSV 생성
    // ─────────────────────────────────────────────
    private void CreateTemplate()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("relicCode,relicName,description,iconPath");
        sb.AppendLine("101,피의 약병,전투 시작 시 HP를 4 회복합니다.,");
        sb.AppendLine("102,고대의 방패,전투 중 첫 번째 피해를 1회 완전히 무효화합니다.,");
        sb.AppendLine("103,불타는 심장,전투 시작 시 에너지 최대치가 1 증가합니다.,");
        sb.AppendLine("201,영혼의 보석,적을 처치할 때마다 HP를 1 회복합니다.,");
        sb.AppendLine("202,어둠의 왕관,매 턴 시작 시 에너지 +1을 얻지만 HP 1을 잃습니다.,");
        sb.AppendLine("901,악마의 핵,최대 HP가 20 증가합니다. 전투 시작 시 에너지 +1을 얻습니다.,");

        File.WriteAllText(DefaultTemplatePath, sb.ToString(),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        AssetDatabase.Refresh();
        csvPath = DefaultTemplatePath;
        EditorUtility.RevealInFinder(DefaultTemplatePath);
        Debug.Log($"[RelicDataImporter] 템플릿 생성 완료: {DefaultTemplatePath}");
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

        // 헤더 파싱
        string[] headers = ParseLine(lines[0]);
        var col = new Dictionary<string, int>();
        for (int i = 0; i < headers.Length; i++)
            col[headers[i].Trim()] = i;

        // RelicDatabase 로드 or 생성
        EnsureFolderExists("Assets/GameResources/Data");
        var db = AssetDatabase.LoadAssetAtPath<RelicDatabase>(OutputPath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<RelicDatabase>();
            AssetDatabase.CreateAsset(db, OutputPath);
        }

        // 기존 항목 보존 딕셔너리 (icon 등 수동 설정 값 유지)
        var existing = new Dictionary<int, RelicData>();
        if (db.relics != null)
            foreach (var r in db.relics)
                if (r != null && r.relicCode != 0)
                    existing[r.relicCode] = r;

        var newList = new List<RelicData>();
        int created = 0, updated = 0, skipped = 0;

        for (int row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;

            string[] f = ParseLine(lines[row]);
            int rc = int.TryParse(GetField(f, col, "relicCode"), out int parsedCode) ? parsedCode : 0;
            if (rc == 0) { skipped++; continue; }

            // 기존 항목 재사용 (icon 보존) or 새 항목
            bool isNew = !existing.TryGetValue(rc, out var data);
            if (isNew) data = new RelicData();

            data.relicCode   = rc;
            data.relicName   = GetField(f, col, "relicName");
            data.description = GetField(f, col, "description");

            // iconPath가 있으면 Sprite 로드 시도
            string iconPath = GetField(f, col, "iconPath");
            if (!string.IsNullOrEmpty(iconPath))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (sprite != null)
                    data.icon = sprite;
                else
                    Debug.LogWarning($"[RelicDataImporter] Sprite를 찾을 수 없습니다: {iconPath}");
            }

            newList.Add(data);
            if (isNew) created++; else updated++;
        }

        db.relics = newList;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"생성: {created}개\n업데이트: {updated}개\n건너뜀: {skipped}개";
        EditorUtility.DisplayDialog("임포트 완료", msg, "확인");
        Debug.Log($"[RelicDataImporter] {msg} → {OutputPath}");
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
