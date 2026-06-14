#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 메뉴: Playgroumd → Create Flashlight Video Panel
/// Flashlight_1F_6 자리에 F1_Stage4_Light 영상 패널 자동 생성
/// ExhibitionManager.lastStageFlashlightPanel 슬롯에 자동 연결
/// </summary>
public class CreateFlashlightVideoPanel
{
    [MenuItem("Playgroumd/Create Flashlight Video Panel")]
    public static void Create()
    {
        // ── 1. Flashlight_1F_6 찾기 ───────────────────────
        var flashlight = GameObject.Find("Flashlight_1F_6");
        if (flashlight == null)
        {
            EditorUtility.DisplayDialog("오류", "Flashlight_1F_6 오브젝트를 씬에서 찾을 수 없습니다.", "확인");
            return;
        }

        // ── 2. F1_Stage4_Light 영상 클립 찾기 ────────────
        var clipGuids = AssetDatabase.FindAssets("F1_Stage4_Light t:VideoClip", new[] { "Assets/Videos" });
        VideoClip clip = null;
        if (clipGuids.Length > 0)
            clip = AssetDatabase.LoadAssetAtPath<VideoClip>(AssetDatabase.GUIDToAssetPath(clipGuids[0]));

        if (clip == null)
        {
            EditorUtility.DisplayDialog("오류", "F1_Stage4_Light 영상 클립을 찾을 수 없습니다.\nAssets/Videos 폴더 확인 후 다시 실행하세요.", "확인");
            return;
        }

        // ── 3. 기존 패널 있으면 제거 ──────────────────────
        var existing = GameObject.Find("FlashlightVideoPanel_1F_6");
        if (existing != null) Object.DestroyImmediate(existing);

        // ── 4. Quad 생성 (Flashlight와 동일 위치·크기) ────
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "FlashlightVideoPanel_1F_6";
        quad.transform.SetParent(flashlight.transform.parent);

        var flPos   = flashlight.transform.position;
        var flScale = flashlight.transform.localScale;

        quad.transform.position   = new Vector3(flPos.x, flPos.y, flPos.z - 0.2f); // 조명보다 앞
        quad.transform.localScale = flScale;
        Object.DestroyImmediate(quad.GetComponent<MeshCollider>());

        // ── 5. Material 생성 ──────────────────────────────
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.name = "Mat_FlashlightVideoPanel_1F_6";
        System.IO.Directory.CreateDirectory("Assets/Materials/Panels");
        string matPath = "Assets/Materials/Panels/Mat_FlashlightVideoPanel_1F_6.mat";
        AssetDatabase.CreateAsset(mat, matPath);
        quad.GetComponent<Renderer>().sharedMaterial = mat;

        // ── 6. RenderTexture 생성 ─────────────────────────
        var rt = new RenderTexture(1920, 1080, 24);
        rt.name = "RT_FlashlightVideoPanel_1F_6";
        System.IO.Directory.CreateDirectory("Assets/RenderTextures");
        string rtPath = "Assets/RenderTextures/RT_FlashlightVideoPanel_1F_6.renderTexture";
        AssetDatabase.CreateAsset(rt, rtPath);

        // ── 7. VideoPlayer 설정 ───────────────────────────
        var vp               = quad.AddComponent<VideoPlayer>();
        vp.renderMode        = VideoRenderMode.RenderTexture;
        vp.targetTexture     = rt;
        vp.playOnAwake       = false;
        vp.waitForFirstFrame = true;
        vp.isLooping         = false;
        vp.skipOnDrop        = false;

        // ── 8. PanelPlayer 설정 ───────────────────────────
        var panel = quad.AddComponent<PanelPlayer>();
        var so    = new SerializedObject(panel);
        so.FindProperty("panelName").stringValue              = "성인기_조명영상";
        so.FindProperty("isTransition").boolValue             = true; // 자동 재생
        so.FindProperty("mainClip").objectReferenceValue      = clip;
        so.FindProperty("renderTexture").objectReferenceValue = rt;
        so.FindProperty("quadRenderer").objectReferenceValue  = quad.GetComponent<Renderer>();
        so.ApplyModifiedProperties();

        // Material에 RT 연결
        mat.mainTexture = rt;
        EditorUtility.SetDirty(mat);

        // ── 9. ExhibitionManager 슬롯 자동 연결 ──────────
        var managers = GameObject.Find("Managers");
        if (managers != null)
        {
            var em   = managers.GetComponent<ExhibitionManager>();
            var emSo = new SerializedObject(em);
            var prop = emSo.FindProperty("lastStageFlashlightPanel");
            if (prop != null)
            {
                prop.objectReferenceValue = panel;
                emSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(em);
                Debug.Log("[CreateFlashlightVideoPanel] ExhibitionManager 슬롯 연결 완료");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 씬 저장 표시
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "FlashlightVideoPanel 생성 완료",
            "FlashlightVideoPanel_1F_6 생성 완료!\n\n" +
            "· Flashlight_1F_6 동일 위치·크기\n" +
            "· F1_Stage4_Light 영상 연결\n" +
            "· ExhibitionManager 슬롯 자동 연결\n\n" +
            "Ctrl+S로 저장하세요.",
            "확인"
        );
    }
}
#endif
