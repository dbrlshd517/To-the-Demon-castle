using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using DeckRoguelike.UI;

/// <summary>
/// CSV → TooltipDatabase ScriptableObject 임포터
/// 메뉴: Tools > Tooltip Data > Import from CSV
///
/// [CSV 컬럼]
///   code        : 3자리 정수 (1xx=Object UI, 2xx=Card UI, 3xx=Menu UI)
///   name        : 표시 이름
///   description : 설명 텍스트
///   iconPath    : Sprite 에셋 경로 (1xx만 사용, 비워두면 null)
///
/// 출력: Assets/GameResources/TooltipDatabase.asset
/// </summary>
public class TooltipDataImporter : EditorWindow
{
    private const string DefaultTemplatePath = "Assets/Editor/TooltipDataTemplate.csv";
    private const string OutputPath          = "Assets/GameResources/TooltipDatabase.asset";

    private string csvPath = DefaultTemplatePath;

    [MenuItem("Tools/Tooltip Data/Import from CSV")]
    public static void ShowWindow() => GetWindow<TooltipDataImporter>("Tooltip Data Importer");

    private void OnEnable()
    {
        if (File.Exists(DefaultTemplatePath)) csvPath = DefaultTemplatePath;
    }

    private void OnGUI()
    {
        GUILayout.Label("CSV → TooltipDatabase 임포터", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        bool exists = File.Exists(DefaultTemplatePath);
        EditorGUILayout.HelpBox(
            exists ? $"템플릿 감지됨: {DefaultTemplatePath}" : "기본 템플릿 없음.",
            exists ? MessageType.Info : MessageType.Warning);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        csvPath = EditorGUILayout.TextField("CSV 파일", csvPath);
        if (GUILayout.Button("찾아보기", GUILayout.Width(70)))
        {
            string s = EditorUtility.OpenFilePanel("CSV 선택", Application.dataPath, "csv");
            if (!string.IsNullOrEmpty(s))
                csvPath = "Assets" + s.Substring(Application.dataPath.Length);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        if (GUILayout.Button("📄 템플릿 CSV 생성"))
            CreateTemplate();

        EditorGUILayout.Space();
        bool canImport = !string.IsNullOrEmpty(csvPath) && File.Exists(csvPath);
        GUI.enabled = canImport;
        if (GUILayout.Button("⬇ CSV 임포트", GUILayout.Height(35)))
            ImportCSV();
        GUI.enabled = true;

        var db = AssetDatabase.LoadAssetAtPath<TooltipDatabase>(OutputPath);
        if (db != null)
            EditorGUILayout.LabelField($"현재 DB: {db.entries?.Count ?? 0}개", EditorStyles.miniLabel);
    }

    private void CreateTemplate()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("code,name,description,iconPath");
        sb.AppendLine("101,방어도,이번 턴에 받는 피해를 먼저 막아줍니다.,");
        sb.AppendLine("102,힘,공격 데미지가 힘 수치만큼 증가합니다.,");
        sb.AppendLine("103,민첩,방어 카드 사용 시 방어도를 민첩 수치만큼 추가로 획득합니다.,");
        sb.AppendLine("201,강화,이 카드의 강화 버전입니다. 효과가 향상됩니다.,");
        sb.AppendLine("301,덱 보기,현재 덱에 있는 모든 카드를 확인합니다.,");
        sb.AppendLine("302,버리기 더미,이번 전투에서 사용된 카드 목록입니다.,");

        Directory.CreateDirectory(Path.GetDirectoryName(DefaultTemplatePath));
        File.WriteAllText(DefaultTemplatePath, sb.ToString(),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        AssetDatabase.Refresh();
        csvPath = DefaultTemplatePath;
        Debug.Log($"[TooltipDataImporter] 템플릿 생성: {DefaultTemplatePath}");
    }

    private void ImportCSV()
    {
        string[] lines = File.ReadAllLines(csvPath, System.Text.Encoding.UTF8);
        if (lines.Length < 2) { EditorUtility.DisplayDialog("오류", "데이터가 없습니다.", "확인"); return; }

        string[] headers = ParseLine(lines[0]);
        var col = new Dictionary<string, int>();
        for (int i = 0; i < headers.Length; i++) col[headers[i].Trim()] = i;

        EnsureFolder("Assets/GameResources");
        var db = AssetDatabase.LoadAssetAtPath<TooltipDatabase>(OutputPath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<TooltipDatabase>();
            AssetDatabase.CreateAsset(db, OutputPath);
        }

        var list = new List<TooltipEntry>();
        int created = 0, skipped = 0;

        for (int row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;
            string[] f = ParseLine(lines[row]);

            if (!int.TryParse(Get(f, col, "code"), out int code) || code <= 0) { skipped++; continue; }

            var entry = new TooltipEntry
            {
                code        = code,
                name        = Get(f, col, "name"),
                description = Get(f, col, "description"),
            };

            // icon (1xx = Object UI)
            string iconPath = Get(f, col, "iconPath");
            if (!string.IsNullOrEmpty(iconPath))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (sprite != null) entry.icon = sprite;
                else Debug.LogWarning($"[TooltipDataImporter] Sprite 없음: {iconPath}");
            }

            list.Add(entry);
            created++;
        }

        db.entries = list;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("임포트 완료", $"생성: {created}개\n건너뜀: {skipped}개", "확인");
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.TrimEnd('/').Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    private static string[] ParseLine(string line)
    {
        var res = new List<string>();
        var cur = new System.Text.StringBuilder();
        bool inQ = false;
        foreach (char c in line)
        {
            if (c == '"') { inQ = !inQ; continue; }
            if (c == ',' && !inQ) { res.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        res.Add(cur.ToString());
        return res.ToArray();
    }

    private static string Get(string[] f, Dictionary<string, int> col, string key)
    {
        if (!col.TryGetValue(key, out int i) || i >= f.Length) return string.Empty;
        return f[i].Trim();
    }
}
