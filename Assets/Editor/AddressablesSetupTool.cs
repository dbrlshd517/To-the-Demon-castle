using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Assets/GameResources 안의 모든 에셋을 Addressables에 자동 등록합니다.
///
/// 주소 규칙: Assets/GameResources/ 접두사와 확장자를 제거한 경로
///   예) Assets/GameResources/Data/ItemDatabase.asset  →  "Data/ItemDatabase"
///       Assets/GameResources/Sprites/relic/101.png   →  "Sprites/relic/101"
///
/// 라벨 규칙 (LoadAssetsAsync에서 사용):
///   Data/Cards/      →  "Cards"
///   Data/Allies/     →  "Allies"
///   Data/Enemies/    →  "Enemies"
///   Data/Encounters/ →  "Encounters"
///
/// 메뉴: Tools > Addressables > Setup GameResources
/// </summary>
public static class AddressablesSetupTool
{
    private const string GameResourcesRoot = "Assets/GameResources";

    private static readonly Dictionary<string, string> FolderLabels = new Dictionary<string, string>
    {
        { "Data/Cards/",      "Cards"      },
        { "Data/Allies/",     "Allies"     },
        { "Data/Enemies/",    "Enemies"    },
        { "Data/Encounters/", "Encounters" },
    };

    [MenuItem("Tools/Addressables/Setup GameResources")]
    public static void SetupAll()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AddressablesSetup] Addressable Asset Settings를 찾을 수 없습니다. " +
                           "Window > Asset Management > Addressables > Groups 에서 초기화하세요.");
            return;
        }

        // 기본 그룹 가져오기
        var group = settings.DefaultGroup;
        if (group == null)
        {
            Debug.LogError("[AddressablesSetup] Default Group이 없습니다.");
            return;
        }

        // 라벨이 없으면 미리 등록
        foreach (var label in FolderLabels.Values)
        {
            if (!settings.GetLabels().Contains(label))
                settings.AddLabel(label);
        }

        // GameResources 안의 모든 에셋 순회 (폴더 제외)
        string[] guids = AssetDatabase.FindAssets("", new[] { GameResourcesRoot });

        int registered = 0, skipped = 0;

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // 폴더는 건너뜀
            if (AssetDatabase.IsValidFolder(assetPath)) { skipped++; continue; }
            // .meta 파일은 AssetDatabase에서 나오지 않지만 혹시 모르니 필터
            if (assetPath.EndsWith(".meta"))            { skipped++; continue; }

            // 주소 계산: "Assets/GameResources/" 제거 + 확장자 제거
            string address = assetPath;
            if (address.StartsWith(GameResourcesRoot + "/"))
                address = address.Substring((GameResourcesRoot + "/").Length);
            address = Path.ChangeExtension(address, null).TrimEnd('.');

            // Addressables에 등록 (이미 있으면 주소만 업데이트)
            var entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
            entry.address = address;

            // 라벨 초기화 후 해당 폴더 라벨 부여
            entry.labels.Clear();
            foreach (var kv in FolderLabels)
            {
                if (address.StartsWith(kv.Key) || address.StartsWith(kv.Key.TrimEnd('/')))
                {
                    entry.SetLabel(kv.Value, enable: true, force: true, postEvent: false);
                }
            }

            registered++;
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
        AssetDatabase.SaveAssets();

        string summary = $"등록: {registered}개 / 건너뜀: {skipped}개";
        EditorUtility.DisplayDialog("Addressables 설정 완료", summary, "확인");
        Debug.Log($"[AddressablesSetup] {summary}");
    }

    [MenuItem("Tools/Addressables/Print Registered Addresses")]
    public static void PrintAddresses()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) { Debug.LogError("Settings 없음"); return; }

        foreach (var group in settings.groups)
        {
            if (group == null) continue;
            foreach (var entry in group.entries)
                Debug.Log($"  [{group.Name}] {entry.address}  labels={string.Join(",", entry.labels)}");
        }
    }
}
