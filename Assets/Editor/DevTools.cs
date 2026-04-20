using UnityEngine;
using UnityEditor;

/// <summary>
/// 개발/테스트용 에디터 유틸리티
/// 메뉴: Tools > Dev Tools > ...
/// </summary>
public static class DevTools
{
    [MenuItem("Tools/Dev Tools/PlayerPrefs 전체 초기화")]
    private static void ClearAllPlayerPrefs()
    {
        if (EditorUtility.DisplayDialog("PlayerPrefs 초기화",
            "저장된 모든 데이터(설정, 세이브 등)를 삭제합니다.\n계속하시겠습니까?", "초기화", "취소"))
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[DevTools] PlayerPrefs 전체 초기화 완료");
        }
    }

    [MenuItem("Tools/Dev Tools/설정(Settings)만 초기화")]
    private static void ClearSettings()
    {
        PlayerPrefs.DeleteKey("GameSettings");
        PlayerPrefs.Save();
        Debug.Log("[DevTools] Settings 초기화 완료 → 다음 실행 시 기본값 적용");
    }
}
