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
///   D (1자리) : 게임 난이도  1=normal  2=hard  3=hell
///   A (2자리) : 액트 번호     1~9
///   N (3자리) : 액트 내 난이도  1~8=일반  9=보스
///   XX(4-5자리): 개별 번호    00~99

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
            "enemies 포맷:\n" +
            "  랜덤: 11000:3.random:3 → 11000 유닛 3개, 플레이어 맨해튼 거리 1~3 안의 모든 셀에서 랜덤\n" +
            "  사거리: 11000:3.range:3 → 11000 유닛 3개, 플레이어 맨해튼 거리가 정확히 3인 셀(고리)에서 랜덤\n" +
            "  고정: 11000:2.1 → 11000 유닛 1개, (col=2,row=1)\n" +
            "  반대편: 11000:3.opposite:1.2.3 → 11000 유닛 3개를 1/2/3번 셀에 배치.\n" +
            "          번호는 플레이어 사분면 반대 코너에서 시작해 행→열 순(플레이어 쪽으로) 1~N.\n" +
            "여러 그룹은 / 로 구분. EnemyData 에셋을 먼저 임포트해야 참조가 연결됩니다.",
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
        sb.AppendLine("11100,슬라임 무리,10100:3.random:3");
        sb.AppendLine("11200,해골 군단,10200:2.random:3");
        sb.AppendLine("11800,석상의 시험,10300:1.random:3");
        sb.AppendLine("11900,악마 군주,10400:1.random:3");

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

        // 임포트 직전에 AssetDatabase를 강제로 갱신 — 외부에서 새로 추가된 EnemyData .asset 파일이
        // 아직 AssetDatabase에 등록되지 않아 LoadAssetAtPath가 null을 반환하는 사고를 막는다.
        AssetDatabase.Refresh();

        EnsureFolderExists(outputFolder);

        string[] headers = ParseLine(lines[0]);
        var col = new Dictionary<string, int>();
        for (int i = 0; i < headers.Length; i++)
            col[headers[i].Trim()] = i;

        // 기존 에셋 중 CSV에 없는 것 삭제를 위해 코드 수집
        var validCodes = new HashSet<string>();
        for (int row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;
            string[] tmp = ParseLine(lines[row]);
            string code = GetField(tmp, col, "encounterCode");
            if (!string.IsNullOrEmpty(code)) validCodes.Add(code);
        }

        // CSV에 없는 기존 에셋 삭제
        int deleted = 0;
        string[] existingAssets = Directory.GetFiles(outputFolder, "*.asset");
        foreach (string path in existingAssets)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (!validCodes.Contains(fileName))
            {
                string assetPath = path.Replace("\\", "/");
                AssetDatabase.DeleteAsset(assetPath);
                deleted++;
                Debug.Log($"[EnemyEncounterImporter] CSV에 없는 에셋 삭제: {fileName}");
            }
        }

        int created = 0, updated = 0, skipped = 0, missingEnemy = 0, outOfBounds = 0;

        // 씬에서 boardCols/boardRows 자동 추출 — 못 찾으면 검증 생략
        bool hasBoardSize = TryLoadBoardSize(out int boardCols, out int boardRows, out string sceneSource);
        if (hasBoardSize)
            Debug.Log($"[EnemyEncounterImporter] 보드 크기 자동 감지: {boardCols} x {boardRows} (출처: {sceneSource})");
        else
            Debug.LogWarning("[EnemyEncounterImporter] 씬에서 boardCols/boardRows를 찾지 못해 좌표 범위 검증을 건너뜁니다.");

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
            var slots = ParseEnemySlots(enemiesRaw, code, hasBoardSize, boardCols, boardRows, ref missingEnemy, ref outOfBounds);
            enc.enemies = slots;

            if (isNew) { AssetDatabase.CreateAsset(enc, assetPath); created++; }
            else       { EditorUtility.SetDirty(enc); updated++; }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"생성: {created}개\n업데이트: {updated}개\n삭제: {deleted}개\n건너뜀: {skipped}개";
        if (missingEnemy > 0)
            msg += $"\n\n경고: EnemyData 못 찾음 {missingEnemy}개 (Console 확인)";
        if (outOfBounds > 0)
            msg += $"\n경고: 보드 범위 밖 고정 좌표 {outOfBounds}개 (Console 확인)";
        EditorUtility.DisplayDialog("임포트 완료", msg, "확인");
        Debug.Log($"[EnemyEncounterImporter] {msg}");
    }

    /// <summary>
    /// Assets/Scenes 의 .unity 파일에서 BoardController의 boardCols/boardRows를 찾아 반환.
    /// 여러 씬에 있을 수 있으므로 첫 번째 매칭을 사용하며, 우선순위로 Ingame.unity가 있으면 그것을 사용.
    /// </summary>
    private static bool TryLoadBoardSize(out int boardCols, out int boardRows, out string sceneSource)
    {
        boardCols = 0;
        boardRows = 0;
        sceneSource = null;

        const string ScenesFolder = "Assets/Scenes";
        if (!Directory.Exists(ScenesFolder)) return false;

        var scenePaths = new List<string>(Directory.GetFiles(ScenesFolder, "*.unity", SearchOption.AllDirectories));
        // Ingame.unity 우선
        scenePaths.Sort((a, b) =>
        {
            bool aIngame = Path.GetFileName(a).Equals("Ingame.unity", System.StringComparison.OrdinalIgnoreCase);
            bool bIngame = Path.GetFileName(b).Equals("Ingame.unity", System.StringComparison.OrdinalIgnoreCase);
            if (aIngame && !bIngame) return -1;
            if (!aIngame && bIngame) return 1;
            return string.Compare(a, b, System.StringComparison.OrdinalIgnoreCase);
        });

        var colsRegex = new System.Text.RegularExpressions.Regex(@"^\s*boardCols:\s*(\d+)\s*$", System.Text.RegularExpressions.RegexOptions.Multiline);
        var rowsRegex = new System.Text.RegularExpressions.Regex(@"^\s*boardRows:\s*(\d+)\s*$", System.Text.RegularExpressions.RegexOptions.Multiline);

        foreach (string path in scenePaths)
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch { continue; }

            var colMatch = colsRegex.Match(text);
            var rowMatch = rowsRegex.Match(text);
            if (!colMatch.Success || !rowMatch.Success) continue;

            if (int.TryParse(colMatch.Groups[1].Value, out int c) &&
                int.TryParse(rowMatch.Groups[1].Value, out int r) &&
                c > 0 && r > 0)
            {
                boardCols = c;
                boardRows = r;
                sceneSource = path.Replace("\\", "/");
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// enemies 파싱 -> EnemySlot[]
    /// 랜덤: 11000:3.random:3 → 11000 유닛 3개, 거리 1~3 안의 모든 셀에서 랜덤
    /// 사거리: 11000:3.range:3 → 11000 유닛 3개, 거리가 정확히 3인 셀(고리)에서 랜덤
    /// 고정: 11000:2.1 → 11000 유닛 1개, (col=2, row=1)
    /// 여러 그룹은 / 로 구분
    /// boardCols/boardRows가 주어지면 고정 좌표가 범위 밖일 때 경고를 찍는다.
    /// </summary>
    private static EnemySlot[] ParseEnemySlots(
        string raw,
        string encounterCode,
        bool hasBoardSize,
        int boardCols,
        int boardRows,
        ref int missingCount,
        ref int outOfBoundsCount)
    {
        var list = new List<EnemySlot>();
        if (string.IsNullOrEmpty(raw)) return list.ToArray();

        // CSV 엔트리(예: "11901:5.random:3")마다 1, 2, 3 ... 증가하는 그룹 ID 부여.
        // 같은 엔트리에서 펼쳐진 슬롯은 모두 같은 spawnGroupId — SnakeBehavior 등이
        // "한 번의 소환 호출 = 한 그룹"으로 묶는 근거로 사용.
        int groupId = 0;

        foreach (var entry in raw.Split('/'))
        {
            string trimmed = entry.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            int firstColon = trimmed.IndexOf(':');
            if (firstColon < 0) continue;

            string enemyCodeStr = trimmed.Substring(0, firstColon).Trim();
            string rest = trimmed.Substring(firstColon + 1).Trim();

            string enemyAssetPath = $"{EnemyAssetFolder}/{enemyCodeStr}.asset";
            EnemyData enemyData = AssetDatabase.LoadAssetAtPath<EnemyData>(enemyAssetPath);
            if (enemyData == null)
            {
                Debug.LogWarning(
                    $"[EnemyEncounterImporter] 인카운터 {encounterCode}: EnemyData 없음 → {enemyAssetPath}. " +
                    $"EnemyData CSV를 먼저 임포트했는지 확인하세요 (Tools > Enemy Data > Import from CSV).");
                missingCount++;
            }
            else if (enemyData.enemyId.ToString() != enemyCodeStr)
            {
                // 파일명과 내부 enemyId 불일치 — 잘못 복사된 에셋이거나 AssetDatabase 캐시 문제일 수 있음
                Debug.LogWarning(
                    $"[EnemyEncounterImporter] 인카운터 {encounterCode}: {enemyAssetPath} 의 내부 enemyId({enemyData.enemyId})가 " +
                    $"파일명({enemyCodeStr})과 다릅니다. EnemyData 임포트를 다시 실행해 동기화하세요.");
            }

            int dotIdx = rest.IndexOf('.');
            if (dotIdx < 0) continue;

            string beforeDot = rest.Substring(0, dotIdx);
            string afterDot = rest.Substring(dotIdx + 1);

            groupId++;

            if (afterDot.StartsWith("random:"))
            {
                int count = ParseInt(beforeDot, 1);
                int range = ParseInt(afterDot.Substring(7), 3);
                for (int i = 0; i < count; i++)
                    list.Add(new EnemySlot
                    {
                        enemyData = enemyData,
                        placementType = PlacementType.Random,
                        placementRange = range,
                        spawnGroupId = groupId,
                    });
            }
            else if (afterDot.StartsWith("range:"))
            {
                int count = ParseInt(beforeDot, 1);
                int range = ParseInt(afterDot.Substring(6), 3);
                for (int i = 0; i < count; i++)
                    list.Add(new EnemySlot
                    {
                        enemyData = enemyData,
                        placementType = PlacementType.Range,
                        placementRange = range,
                        spawnGroupId = groupId,
                    });
            }
            else if (afterDot.StartsWith("opposite:"))
            {
                int count = ParseInt(beforeDot, 1);
                string posListRaw = afterDot.Substring(9);
                var posTokens = posListRaw.Split('.');
                var positions = new List<int>(posTokens.Length);
                foreach (var t in posTokens)
                {
                    string trimmedTok = t.Trim();
                    if (string.IsNullOrEmpty(trimmedTok)) continue;
                    positions.Add(ParseInt(trimmedTok, 0));
                }

                if (positions.Count != count)
                {
                    Debug.LogWarning(
                        $"[EnemyEncounterImporter] 인카운터 {encounterCode}: opposite 개수({count})와 " +
                        $"위치 수({positions.Count})가 다릅니다. 위치 수에 맞춰 생성합니다.");
                }

                foreach (int p in positions)
                {
                    list.Add(new EnemySlot
                    {
                        enemyData = enemyData,
                        placementType = PlacementType.Opposite,
                        oppositePosition = p,
                        spawnGroupId = groupId,
                    });
                }
            }
            else
            {
                int slotCol = ParseInt(beforeDot, 0);
                int slotRow = ParseInt(afterDot, 0);

                if (hasBoardSize &&
                    (slotCol < 0 || slotCol >= boardCols || slotRow < 0 || slotRow >= boardRows))
                {
                    Debug.LogWarning(
                        $"[EnemyEncounterImporter] 인카운터 {encounterCode}: 고정 좌표 ({slotCol},{slotRow}) " +
                        $"가 보드 범위 (0..{boardCols - 1}, 0..{boardRows - 1}) 밖입니다 — 런타임에 -1로 처리되어 적이 생성되지 않습니다.");
                    outOfBoundsCount++;
                }

                list.Add(new EnemySlot
                {
                    enemyData = enemyData,
                    placementType = PlacementType.Fixed,
                    col = slotCol,
                    row = slotRow,
                    spawnGroupId = groupId,
                });
            }
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
