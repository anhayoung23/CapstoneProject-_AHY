#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 메뉴: Playgroumd → Rename Videos to English
/// 영상 파일명 한글 → 직관적 영문으로 일괄 변경
///
/// 변경 규칙:
///   F1 = 1면 (과보호)   F2 = 2면 (자립)
///   Stage = 본편 패널   Trans = 전환 패널
///   Light = 조명 자리 영상 (4단계-위)
/// </summary>
public class RenameVideos
{
    private static readonly (string from, string to)[] RenameMap =
    {
        // ── 1면 ──────────────────────────────────────
        ("3차 심사 1면 1단계",          "F1_Stage1"),
        ("3차 심사 1면 1단계 전환",      "F1_Trans1"),
        ("3차 심사 1면 2단계",          "F1_Stage2"),
        ("3차 심사 1면 2단계 전환",      "F1_Trans2"),
        ("3차 심사 1면 3단계",          "F1_Stage3"),
        ("3차 심사 1면 3단계 전환",      "F1_Trans3"),
        ("3차 심사 1면 4단계-아래",      "F1_Stage4_Panel"),
        ("3차심사 1면 4단계-위",         "F1_Stage4_Light"),

        // ── 2면 ──────────────────────────────────────
        ("3차심사 2면 1단계",            "F2_Stage1"),
        ("3차 심사 2면 1단계 전환",      "F2_Trans1"),
        ("3차 심사 2면 2단계",          "F2_Stage2"),
        ("3차 심사 2면 2단계 전환",      "F2_Trans2"),
        ("3차 심사 2면 3단계",          "F2_Stage3"),
        ("3차 심사 2면 3단계 전환",      "F2_Trans3"),
        ("3차 심사 2면 4단계",          "F2_Stage4"),
    };

    [MenuItem("Playgroumd/Rename Videos to English")]
    public static void Rename()
    {
        var guids = AssetDatabase.FindAssets("t:VideoClip", new[] { "Assets/Videos" });
        int count = 0;

        foreach (var guid in guids)
        {
            string path     = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);

            foreach (var (from, to) in RenameMap)
            {
                if (fileName == from)
                {
                    string error = AssetDatabase.RenameAsset(path, to);
                    if (string.IsNullOrEmpty(error))
                    {
                        Debug.Log($"[RenameVideos] {from} → {to}");
                        count++;
                    }
                    else
                    {
                        Debug.LogWarning($"[RenameVideos] 실패: {from} — {error}");
                    }
                    break;
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Rename Videos 완료",
            $"{count}개 영상 파일명 영문으로 변경 완료\n\n" +
            "1면: F1_Stage1~4, F1_Trans1~3, F1_Stage4_Light\n" +
            "2면: F2_Stage1~4, F2_Trans1~3",
            "확인"
        );
    }
}
#endif
