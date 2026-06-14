using UnityEngine;
using UnityEditor;
using Klak.Syphon;

/// <summary>
/// 모든 PanelPlayer 오브젝트에 Syphon Server를 자동으로 추가하는 에디터 툴
/// 메뉴: Tools → Setup Syphon Senders
/// </summary>
public class SyphonSenderSetup : Editor
{
    [MenuItem("Tools/Setup Syphon Senders")]
    static void SetupSyphonSenders()
    {
        PanelPlayer[] panels = FindObjectsOfType<PanelPlayer>();
        int count = 0;

        foreach (PanelPlayer panel in panels)
        {
            // 이미 있으면 스킵
            SyphonServer existing = panel.GetComponent<SyphonServer>();
            if (existing != null) continue;

            // Render Texture 찾기 (VideoPlayer Target Texture)
            UnityEngine.Video.VideoPlayer vp =
                panel.GetComponent<UnityEngine.Video.VideoPlayer>();
            if (vp == null || vp.targetTexture == null) continue;

            // Syphon Server 추가
            SyphonServer server = panel.gameObject.AddComponent<SyphonServer>();

            // SerializedObject로 프로퍼티 탐색 (버전별 이름 다를 수 있음)
            SerializedObject so = new SerializedObject(server);

            // Source 타입 설정 — 가능한 프로퍼티 이름 순서대로 시도
            string[] sourceNames  = { "_source", "_sourceType", "sourceType", "m_Source" };
            string[] textureNames = { "_sourceTexture", "_texture", "sourceTexture", "m_SourceTexture" };

            SerializedProperty sourceProp = null;
            foreach (var name in sourceNames)
            {
                sourceProp = so.FindProperty(name);
                if (sourceProp != null) break;
            }

            SerializedProperty textureProp = null;
            foreach (var name in textureNames)
            {
                textureProp = so.FindProperty(name);
                if (textureProp != null) break;
            }

            if (sourceProp != null)
                sourceProp.enumValueIndex = 1; // Texture

            if (textureProp != null)
                textureProp.objectReferenceValue = vp.targetTexture;

            // 서버 이름을 패널 오브젝트 이름으로 설정
            string[] nameProps = { "_name", "_serverName", "serverName", "m_Name" };
            foreach (var n in nameProps)
            {
                var nameProp = so.FindProperty(n);
                if (nameProp != null)
                {
                    nameProp.stringValue = panel.gameObject.name;
                    break;
                }
            }

            so.ApplyModifiedProperties();

            // 프로퍼티를 못 찾은 경우 Inspector에서 수동 설정 안내
            if (sourceProp == null || textureProp == null)
                Debug.LogWarning($"[Setup] {panel.gameObject.name} — SyphonServer 프로퍼티를 찾지 못했습니다. Inspector에서 직접 RenderTexture를 연결해주세요.");
            else
                Debug.Log($"[Setup] Syphon Server 추가: {panel.gameObject.name} → {vp.targetTexture.name}");

            count++;
        }

        Debug.Log($"[Setup] 완료 — {count}개 패널에 Syphon Server 추가됨");
        EditorUtility.DisplayDialog("완료", $"{count}개 패널에 Syphon Server가 추가되었습니다.\n프로퍼티 경고가 있으면 Inspector에서 직접 RenderTexture를 연결해주세요.", "확인");
    }

    // ── 전환 패널 크기 조정 ────────────────────────────────

    /// <summary>
    /// 전환 패널 Scale을 1080×1620 (2:3) 비율로 일괄 변경
    /// 메뉴: Tools → Resize Transition Panels (2:3)
    /// </summary>
    [MenuItem("Tools/Resize Transition Panels (2:3)")]
    static void ResizeTransitionPanels()
    {
        // 원본 영상 해상도 1080×1620 (2:3 세로형)
        int rtW = 1080;
        int rtH = 1620;

        // Quad Scale — 2:3 비율 유지 (스토리 패널 높이 120 기준)
        float quadW = 80f;
        float quadH = 120f;

        int count = 0;
        foreach (PanelPlayer panel in FindObjectsOfType<PanelPlayer>(true))
        {
            var so   = new SerializedObject(panel);
            bool isT = so.FindProperty("isTransition").boolValue;
            if (!isT) continue;

            // ① Quad Scale 변경
            Undo.RecordObject(panel.transform, "Resize Transition Panel");
            Vector3 s = panel.transform.localScale;
            panel.transform.localScale = new Vector3(quadW, quadH, s.z);

            // ② RenderTexture 해상도 변경 (VideoPlayer targetTexture)
            var vp = panel.GetComponent<UnityEngine.Video.VideoPlayer>();
            if (vp != null && vp.targetTexture != null)
            {
                var rt = vp.targetTexture;
                // RT 크기가 이미 맞으면 스킵
                if (rt.width != rtW || rt.height != rtH)
                {
                    rt.Release();
                    rt.width  = rtW;
                    rt.height = rtH;
                    rt.Create();
                    EditorUtility.SetDirty(rt);
                    Debug.Log($"[Resize] {panel.gameObject.name} RT → {rtW}×{rtH}");
                }
            }

            count++;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("완료",
            $"전환 패널 {count}개 완료\n" +
            $"· Quad Scale: {quadW}×{quadH} (2:3)\n" +
            $"· RenderTexture: {rtW}×{rtH}", "확인");
    }

    // ── 레이어 자동 지정 ───────────────────────────────────

    [MenuItem("Tools/Assign Layers (Story · Trans · Flash)")]
    static void AssignLayers()
    {
        int storyLayer = LayerMask.NameToLayer("StoryPanels");
        int transLayer = LayerMask.NameToLayer("TransitionPanels");
        int flashLayer = LayerMask.NameToLayer("Flashlights");

        if (storyLayer < 0 || transLayer < 0 || flashLayer < 0)
        {
            EditorUtility.DisplayDialog("오류",
                "레이어가 없습니다.\nProject Settings → Tags and Layers에\n" +
                "StoryPanels / TransitionPanels / Flashlights 를 먼저 추가해주세요.", "확인");
            return;
        }

        int story = 0, trans = 0, flash = 0;

        // PanelPlayer → Story / Transition 구분
        foreach (PanelPlayer panel in FindObjectsOfType<PanelPlayer>(true))
        {
            var so   = new SerializedObject(panel);
            bool isT = so.FindProperty("isTransition").boolValue;
            int  layer = isT ? transLayer : storyLayer;
            SetLayerRecursively(panel.gameObject, layer);
            if (isT) trans++; else story++;
        }

        // FlashlightBeam → Flashlights
        foreach (FlashlightBeam beam in FindObjectsOfType<FlashlightBeam>(true))
        {
            SetLayerRecursively(beam.gameObject, flashLayer);
            flash++;
        }

        EditorUtility.DisplayDialog("완료",
            $"StoryPanels: {story}개\nTransitionPanels: {trans}개\nFlashlights: {flash}개", "확인");
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    [MenuItem("Tools/Remove All Syphon Senders")]
    static void RemoveSyphonSenders()
    {
        SyphonServer[] servers = FindObjectsOfType<SyphonServer>();
        foreach (SyphonServer s in servers)
        {
            if (s.gameObject.name.StartsWith("SyphonSender")) continue;
            DestroyImmediate(s);
        }
        Debug.Log("[Setup] 모든 Syphon Server 제거 완료");
    }

    // ── 맵핑 프리뷰 ────────────────────────────────────────

    /// <summary>
    /// Play 모드에서 모든 패널을 루프 프리뷰로 강제 표시
    /// Managers 비활성화 후 이 메뉴 실행 → 맵핑 작업용
    /// 메뉴: Tools → Mapping Preview / Start
    /// </summary>
    [MenuItem("Tools/Mapping Preview/Start All Panels")]
    static void StartMappingPreview()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("알림", "Play 모드에서 실행해주세요.", "확인");
            return;
        }

        PanelPlayer[] panels = FindObjectsOfType<PanelPlayer>(true); // 비활성 포함
        int count = 0;
        foreach (PanelPlayer p in panels)
        {
            if (!p.gameObject.activeInHierarchy)
                p.gameObject.SetActive(true);
            p.PlayPreview();
            count++;
        }
        Debug.Log($"[Mapping Preview] {count}개 패널 프리뷰 시작");
    }

    /// <summary>
    /// 모든 패널 숨기기 (프리뷰 종료)
    /// 메뉴: Tools → Mapping Preview / Stop
    /// </summary>
    [MenuItem("Tools/Mapping Preview/Stop All Panels")]
    static void StopMappingPreview()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("알림", "Play 모드에서 실행해주세요.", "확인");
            return;
        }

        PanelPlayer[] panels = FindObjectsOfType<PanelPlayer>(true);
        foreach (PanelPlayer p in panels)
            p.ResetPanel();

        Debug.Log("[Mapping Preview] 모든 패널 리셋");
    }
}
