#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 메뉴: Playgroumd → Rename RenderTextures to English
/// RT 파일명의 한글을 영문으로 변경 (GUID 유지)
/// </summary>
public class RenameRenderTextures
{
    // 한글 → 영문 매핑
    private static readonly (string from, string to)[] RenameMap =
    {
        ("RT_Panel_1F_0_영아기",    "RT_Panel_1F_0_Infant"),
        ("RT_Panel_1F_1_전환①",    "RT_Panel_1F_1_Transition1"),
        ("RT_Panel_1F_2_아동기",    "RT_Panel_1F_2_Child"),
        ("RT_Panel_1F_3_전환②",    "RT_Panel_1F_3_Transition2"),
        ("RT_Panel_1F_4_청소년기",  "RT_Panel_1F_4_Teen"),
        ("RT_Panel_1F_5_전환③",    "RT_Panel_1F_5_Transition3"),
        ("RT_Panel_1F_6_성인기",    "RT_Panel_1F_6_Adult"),
        ("RT_Panel_2F_0_영아기",    "RT_Panel_2F_0_Infant"),
        ("RT_Panel_2F_1_전환①",    "RT_Panel_2F_1_Transition1"),
        ("RT_Panel_2F_2_아동기",    "RT_Panel_2F_2_Child"),
        ("RT_Panel_2F_3_전환②",    "RT_Panel_2F_3_Transition2"),
        ("RT_Panel_2F_4_청소년기",  "RT_Panel_2F_4_Teen"),
        ("RT_Panel_2F_5_전환③",    "RT_Panel_2F_5_Transition3"),
        ("RT_Panel_2F_6_성인기",    "RT_Panel_2F_6_Adult"),
        // 혹시 RT_1F_0 형식으로 생성된 경우도 대응
        ("RT_1F_0_영아기",   "RT_1F_0_Infant"),
        ("RT_1F_0_Panel",    "RT_1F_0_Infant"),
    };

    [MenuItem("Playgroumd/Rename RenderTextures to English")]
    public static void Rename()
    {
        int count = 0;
        var guids = AssetDatabase.FindAssets("t:RenderTexture", new[] { "Assets/RenderTextures" });

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
                        Debug.Log($"[Rename] {from} → {to}");
                        count++;
                    }
                    else
                    {
                        Debug.LogWarning($"[Rename] 실패: {from} — {error}");
                    }
                    break;
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Rename 완료",
            $"{count}개 RenderTexture 파일명 영문으로 변경 완료\n참조(GUID)는 그대로 유지됩니다.",
            "확인"
        );
    }
}
#endif
