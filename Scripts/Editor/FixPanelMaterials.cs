#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Video;
using UnityEditor;

/// <summary>
/// Playgroumd → Fix Panel Materials
/// 모든 패널의 RT → VideoPlayer.targetTexture → PanelPlayer.renderTexture → Material.mainTexture 재연결
/// </summary>
public class FixPanelMaterials
{
    [MenuItem("Playgroumd/Fix Panel Materials")]
    public static void Fix()
    {
        var shader = Shader.Find("Sprites/Default"); // 알파 디졸브 지원
        if (shader == null)
        {
            EditorUtility.DisplayDialog("오류", "Unlit/Texture 셰이더를 찾을 수 없습니다.", "확인");
            return;
        }

        // Assets/RenderTextures 안의 모든 RT 로드
        var rtGUIDs = AssetDatabase.FindAssets("t:RenderTexture", new[] { "Assets/RenderTextures" });
        var rtList  = new System.Collections.Generic.List<RenderTexture>();
        foreach (var g in rtGUIDs)
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(AssetDatabase.GUIDToAssetPath(g));
            if (rt != null) rtList.Add(rt);
        }

        int fixedCount = 0;
        var panels = Object.FindObjectsOfType<PanelPlayer>();

        foreach (var panel in panels)
        {
            var vp  = panel.GetComponent<VideoPlayer>();
            var rnd = panel.GetComponent<Renderer>();
            if (vp == null || rnd == null) continue;

            // 패널 이름으로 대응하는 RT 찾기
            // 예: Panel_1F_0_영아기 → RT_Panel_1F_0_* 또는 RT_1F_0_*
            string panelObj = panel.gameObject.name; // 예) Panel_1F_0_영아기
            RenderTexture matched = FindMatchingRT(rtList, panelObj);

            if (matched == null)
            {
                Debug.LogWarning($"[Fix] RT 못 찾음 — {panelObj}");
                continue;
            }

            // 1. VideoPlayer.targetTexture 연결
            vp.targetTexture = matched;
            EditorUtility.SetDirty(vp);

            // 2. PanelPlayer.renderTexture (private 직렬화 필드) 연결
            var so = new SerializedObject(panel);
            var rtProp = so.FindProperty("renderTexture");
            if (rtProp != null)
            {
                rtProp.objectReferenceValue = matched;
                so.ApplyModifiedProperties();
            }

            // 3. Material 셰이더 + mainTexture 연결
            var mat = rnd.sharedMaterial;
            if (mat != null)
            {
                mat.shader      = shader;
                mat.mainTexture = matched;
                EditorUtility.SetDirty(mat);
            }

            Debug.Log($"[Fix] ✓ {panelObj} → {matched.name}");
            fixedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Fix 완료",
            $"{fixedCount}개 패널 RT 전체 재연결 완료\n" +
            "VideoPlayer / PanelPlayer / Material 모두 업데이트됨",
            "확인"
        );
    }

    /// <summary>
    /// 패널 오브젝트 이름에서 면(1F/2F)과 인덱스를 추출해 RT 매칭
    /// Panel_1F_0_영아기 → RT_Panel_1F_0_* 우선, 없으면 RT_1F_0_*
    /// </summary>
    private static RenderTexture FindMatchingRT(
        System.Collections.Generic.List<RenderTexture> rtList, string panelName)
    {
        // panelName 예: Panel_1F_0_영아기, Panel_2F_3_전환②
        // RT 이름 예: RT_Panel_1F_0_Infant, RT_Panel_2F_3_Transition2

        // "Panel_1F_0" 부분 추출
        string[] parts = panelName.Split('_');
        if (parts.Length < 3) return null;
        // parts[0]=Panel, parts[1]=1F, parts[2]=0, parts[3]=영아기
        string face  = parts.Length > 1 ? parts[1] : ""; // 1F or 2F
        string index = parts.Length > 2 ? parts[2] : ""; // 0~6

        // RT 이름에 face + index 가 포함된 것 찾기
        // RT_Panel_1F_0_Infant → contains "1F" and "_0_"
        foreach (var rt in rtList)
        {
            string rtName = rt.name;
            if (rtName.Contains(face) && rtName.Contains($"_{index}_"))
                return rt;
        }

        // fallback: index만 매칭
        foreach (var rt in rtList)
        {
            if (rt.name.Contains($"_{index}_") || rt.name.EndsWith($"_{index}"))
                return rt;
        }

        return null;
    }
}
#endif
