#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 메뉴: Playgroumd → Assign Video Clips
/// 영상 파일을 패널에 자동 연결
/// F1_Stage1 → Panel_1F_0, F1_Trans1 → Panel_1F_1 ...
/// </summary>
public class AssignVideoClips
{
    // (영상 파일명, 패널 오브젝트명) 매핑
    private static readonly (string clip, string panel)[] ClipMap =
    {
        // ── 1면 ──────────────────────────────────────
        ("F1_Stage1",       "Panel_1F_0_영아기"),
        ("F1_Trans1",       "Panel_1F_1_전환①"),
        ("F1_Stage2",       "Panel_1F_2_아동기"),
        ("F1_Trans2",       "Panel_1F_3_전환②"),
        ("F1_Stage3",       "Panel_1F_4_청소년기"),
        ("F1_Trans3",       "Panel_1F_5_전환③"),
        ("F1_Stage4_Panel", "Panel_1F_6_성인기"),

        // ── 2면 ──────────────────────────────────────
        ("F2_Stage1",       "Panel_2F_0_영아기"),
        ("F2_Trans1",       "Panel_2F_1_전환①"),
        ("F2_Stage2",       "Panel_2F_2_아동기"),
        ("F2_Trans2",       "Panel_2F_3_전환②"),
        ("F2_Stage3",       "Panel_2F_4_청소년기"),
        ("F2_Trans3",       "Panel_2F_5_전환③"),
        ("F2_Stage4",       "Panel_2F_6_성인기"),
    };

    [MenuItem("Playgroumd/Assign Video Clips")]
    public static void Assign()
    {
        int count = 0;

        foreach (var (clipName, panelName) in ClipMap)
        {
            // 영상 클립 찾기
            var guids = AssetDatabase.FindAssets($"{clipName} t:VideoClip", new[] { "Assets/Videos" });
            if (guids.Length == 0)
            {
                Debug.LogWarning($"[AssignClips] 영상 못 찾음: {clipName}");
                continue;
            }

            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(
                AssetDatabase.GUIDToAssetPath(guids[0]));

            // 패널 오브젝트 찾기
            var panelGO = GameObject.Find(panelName);
            if (panelGO == null)
            {
                Debug.LogWarning($"[AssignClips] 패널 못 찾음: {panelName}");
                continue;
            }

            var panel = panelGO.GetComponent<PanelPlayer>();
            if (panel == null)
            {
                Debug.LogWarning($"[AssignClips] PanelPlayer 없음: {panelName}");
                continue;
            }

            // SerializedObject로 mainClip 연결
            var so = new SerializedObject(panel);
            so.FindProperty("mainClip").objectReferenceValue = clip;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(panel);

            Debug.Log($"[AssignClips] ✓ {panelName} ← {clipName}");
            count++;
        }

        // 씬 저장
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Assign Video Clips 완료",
            $"{count}개 패널에 영상 자동 연결 완료\nCtrl+S로 저장하세요.",
            "확인"
        );
    }
}
#endif
