using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DeckRoguelike.Core;

namespace DeckRoguelike.Editor
{
    public class LocalizationBindingTool : EditorWindow
    {
        private const string CSV_PATH  = "Assets/Editor/Localization.csv";
        private const string DB_PATH   = "Assets/GameResources/Data/LocalizationDatabase.asset";

        // ── 탭 ──────────────────────────────────────────────
        private enum Tab { Import, Bind }
        private Tab currentTab = Tab.Import;

        // ── Import 탭 ────────────────────────────────────────
        private string importLog = "";

        // ── Bind 탭 ──────────────────────────────────────────
        private Vector2 scroll;
        private List<BindingResult> results = new List<BindingResult>();
        private bool scanned = false;
        private HashSet<string> validCodes = new HashSet<string>();

        private struct BindingResult
        {
            public string scenePath;
            public string sceneName;
            public string objectFullPath;
            public string objectName;
            public string matchedCode;
            public bool   hasExistingBinder;
        }

        // ────────────────────────────────────────────────────
        [MenuItem("Tools/Localization/Binding Tool")]
        public static void OpenWindow()
            => GetWindow<LocalizationBindingTool>("Localization Binding Tool");

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            currentTab = (Tab)GUILayout.Toolbar((int)currentTab,
                new[] { "① CSV → Import", "② Scene Bind" });
            EditorGUILayout.Space(6);

            if (currentTab == Tab.Import) DrawImportTab();
            else                          DrawBindTab();
        }

        // ═══════════════════════════════════════════════════
        // Import 탭
        // ═══════════════════════════════════════════════════
        private void DrawImportTab()
        {
            EditorGUILayout.LabelField("CSV → LocalizationDatabase", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"CSV:  {CSV_PATH}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"출력: {DB_PATH}", EditorStyles.miniLabel);
            EditorGUILayout.Space(6);

            if (GUILayout.Button("Import CSV → LocalizationDatabase", GUILayout.Height(32)))
                ImportCSV();

            if (GUILayout.Button("CSV 파일 열기", GUILayout.Height(24)))
                System.Diagnostics.Process.Start(Path.GetFullPath(CSV_PATH));

            if (!string.IsNullOrEmpty(importLog))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(importLog, MessageType.Info);
            }
        }

        private void ImportCSV()
        {
            if (!File.Exists(CSV_PATH))
            {
                importLog = $"CSV 없음: {CSV_PATH}";
                return;
            }

            // ScriptableObject 로드 or 생성
            var db = AssetDatabase.LoadAssetAtPath<LocalizationDatabase>(DB_PATH);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<LocalizationDatabase>();
                AssetDatabase.CreateAsset(db, DB_PATH);
            }

            Undo.RecordObject(db, "Import Localization CSV");
            db.entries.Clear();

            var lines = File.ReadAllLines(CSV_PATH, Encoding.UTF8);
            if (lines.Length < 2) { importLog = "CSV에 데이터가 없습니다."; return; }

            // 헤더 파싱
            var headers = ParseLine(lines[0]);

            int count = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                var cells = ParseLine(lines[i]);
                if (cells.Count == 0 || string.IsNullOrWhiteSpace(cells[0])) continue;

                var entry = new LocalizationDatabase.Entry();
                for (int c = 0; c < headers.Count && c < cells.Count; c++)
                {
                    string val = cells[c];
                    switch (headers[c])
                    {
                        case "stringcode":  entry.code        = val; break;
                        case "en":          entry.en          = val; break;
                        case "pt_BR":       entry.pt_BR       = val; break;
                        case "zh_CN":       entry.zh_CN       = val; break;
                        case "zh_TW":       entry.zh_TW       = val; break;
                        case "nl":          entry.nl          = val; break;
                        case "eo":          entry.eo          = val; break;
                        case "fi":          entry.fi          = val; break;
                        case "fr":          entry.fr          = val; break;
                        case "de":          entry.de          = val; break;
                        case "id":          entry.id          = val; break;
                        case "it":          entry.it          = val; break;
                        case "ja":          entry.ja          = val; break;
                        case "ko":          entry.ko          = val; break;
                        case "pl":          entry.pl          = val; break;
                        case "ru":          entry.ru          = val; break;
                        case "sr":          entry.sr          = val; break;
                        case "sr_Latn":     entry.sr_Latn     = val; break;
                        case "es":          entry.es          = val; break;
                        case "th":          entry.th          = val; break;
                        case "tr":          entry.tr          = val; break;
                        case "uk":          entry.uk          = val; break;
                        case "vi":          entry.vi          = val; break;
                        case "description": entry.description = val; break;
                    }
                }
                db.entries.Add(entry);
                count++;
            }

            db.InvalidateCache();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            importLog = $"✓ {count}개 항목 임포트 완료 → {DB_PATH}";
        }

        // ═══════════════════════════════════════════════════
        // Bind 탭
        // ═══════════════════════════════════════════════════
        private void DrawBindTab()
        {
            EditorGUILayout.LabelField("전체 씬 스캔 → LocalizationBinder 추가", EditorStyles.boldLabel);

            var buildScenes = UnityEditor.EditorBuildSettings.scenes;
            EditorGUILayout.LabelField($"빌드 씬 수: {buildScenes.Length}개", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(
                "현재 열린 씬의 변경사항을 저장한 뒤 스캔하세요.\n" +
                "스캔은 빌드 세팅에 등록된 모든 씬을 순서대로 열어 확인합니다.",
                MessageType.Info);
            EditorGUILayout.Space(4);

            if (GUILayout.Button("Scan All Scenes", GUILayout.Height(30)))
                ScanAllScenes();

            if (scanned)
            {
                EditorGUILayout.Space(4);
                int matched = 0;
                foreach (var r in results) if (r.matchedCode != null) matched++;
                EditorGUILayout.LabelField(
                    $"총 {results.Count}개 TMP 발견  |  매칭: {matched}개  |  미매칭: {results.Count - matched}개",
                    EditorStyles.boldLabel);

                EditorGUILayout.Space(4);
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(320));

                string lastScene = null;
                foreach (var r in results)
                {
                    if (r.sceneName != lastScene)
                    {
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField($"▶ {r.sceneName}", EditorStyles.boldLabel);
                        lastScene = r.sceneName;
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(
                        r.matchedCode != null ? "✓" : "—", GUILayout.Width(14));
                    EditorGUILayout.LabelField(r.objectName, GUILayout.Width(180));
                    EditorGUILayout.LabelField(
                        r.matchedCode ?? "(코드 없음)",
                        r.matchedCode != null ? EditorStyles.label : EditorStyles.miniLabel,
                        GUILayout.Width(180));
                    if (r.hasExistingBinder)
                        EditorGUILayout.LabelField("[Binder]", EditorStyles.miniLabel, GUILayout.Width(55));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(4);
                if (GUILayout.Button("Apply Binders to All Scenes", GUILayout.Height(30)))
                    ApplyBindersToAllScenes();
            }
        }

        private void ScanAllScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            LoadValidCodes();
            results.Clear();

            var originalScene = SceneManager.GetActiveScene().path;
            var buildScenes   = UnityEditor.EditorBuildSettings.scenes;

            foreach (var bs in buildScenes)
            {
                if (!bs.enabled || !File.Exists(bs.path)) continue;

                var scene = EditorSceneManager.OpenScene(bs.path, OpenSceneMode.Single);
                string sceneName = Path.GetFileNameWithoutExtension(bs.path);

                var labels = GameObject.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
                foreach (var label in labels)
                {
                    string name    = label.gameObject.name;
                    string matched = validCodes.Contains(name) ? name : null;
                    bool   hasBind = label.GetComponent<LocalizationBinder>() != null;

                    results.Add(new BindingResult
                    {
                        scenePath         = bs.path,
                        sceneName         = sceneName,
                        objectFullPath    = GetPath(label.transform),
                        objectName        = name,
                        matchedCode       = matched,
                        hasExistingBinder = hasBind,
                    });
                }
            }

            // 원래 씬으로 복귀
            if (!string.IsNullOrEmpty(originalScene) && File.Exists(originalScene))
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

            scanned = true;
            Repaint();
        }

        private void ApplyBindersToAllScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            LoadValidCodes();
            var originalScene = SceneManager.GetActiveScene().path;
            var scenePaths    = new HashSet<string>();
            foreach (var r in results)
                if (r.matchedCode != null) scenePaths.Add(r.scenePath);

            foreach (var scenePath in scenePaths)
            {
                if (!File.Exists(scenePath)) continue;
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                int added = 0, updated = 0;
                var labels = GameObject.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
                foreach (var label in labels)
                {
                    string name = label.gameObject.name;
                    if (!validCodes.Contains(name)) continue;

                    var binder = label.GetComponent<LocalizationBinder>();
                    if (binder == null)
                    {
                        binder = Undo.AddComponent<LocalizationBinder>(label.gameObject);
                        added++;
                    }
                    else updated++;

                    binder.SetCode(name);
                    EditorUtility.SetDirty(label.gameObject);
                }

                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[LocalizationBinder] {Path.GetFileNameWithoutExtension(scenePath)}: 추가 {added}, 업데이트 {updated}");
            }

            if (!string.IsNullOrEmpty(originalScene) && File.Exists(originalScene))
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

            ScanAllScenes();
        }

        private void LoadValidCodes()
        {
            validCodes.Clear();
            if (!File.Exists(CSV_PATH)) return;
            var lines = File.ReadAllLines(CSV_PATH, Encoding.UTF8);
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                int comma = line.IndexOf(',');
                string code = comma > 0 ? line.Substring(0, comma).Trim() : line.Trim();
                if (!string.IsNullOrEmpty(code)) validCodes.Add(code);
            }
        }

        private string GetPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }

        private List<string> ParseLine(string line)
        {
            var result  = new List<string>();
            bool inQ    = false;
            var  cur    = new StringBuilder();
            foreach (char c in line)
            {
                if (c == '"')      { inQ = !inQ; }
                else if (c == ',' && !inQ) { result.Add(cur.ToString()); cur.Clear(); }
                else               { cur.Append(c); }
            }
            result.Add(cur.ToString());
            return result;
        }
    }
}
