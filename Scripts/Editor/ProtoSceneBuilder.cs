#if UNITY_EDITOR
using UnityEngine.Video;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Unity 메뉴 → Playgroumd → Build Scene
/// 14개 패널 (1면 7개 + 2면 7개) 자동 생성
/// 좌우 세로 계단식 레이아웃
/// </summary>
public class ProtoSceneBuilder : EditorWindow
{
    private static readonly string[] PanelNames =
    {
        "영아기", "전환①", "아동기", "전환②", "청소년기", "전환③", "성인기"
    };

    private static readonly bool[] IsTransition =
    {
        false, true, false, true, false, true, false
    };

    // ── 레이아웃 ───────────────────────────────────────────
    // 생애주기 패널: 200×120
    // 전환 패널: 200×120 (동일 크기로 통일)
    // 전환 패널 Z = 0.5 (생애주기 패널 뒤로)
    private static readonly Vector3[] Positions1F =
    {
        new Vector3(-360f, -210f,  0f),  // [0] 영아기
        new Vector3(-290f, -140f,  0.5f), // [1] 전환① (뒤로)
        new Vector3(-210f,  -70f,  0f),  // [2] 아동기
        new Vector3(-140f,    0f,  0.5f), // [3] 전환② (뒤로)
        new Vector3( -60f,   70f,  0f),  // [4] 청소년기
        new Vector3(   0f,  140f,  0.5f), // [5] 전환③ (뒤로)
        new Vector3(  60f,  210f,  0f),  // [6] 성인기
    };

    private static readonly Vector3[] Scales1F =
    {
        new Vector3(200f, 120f, 1f), // 영아기
        new Vector3(200f, 120f, 1f), // 전환① (생애주기와 동일)
        new Vector3(200f, 120f, 1f), // 아동기
        new Vector3(200f, 120f, 1f), // 전환②
        new Vector3(200f, 120f, 1f), // 청소년기
        new Vector3(200f, 120f, 1f), // 전환③
        new Vector3(200f, 120f, 1f), // 성인기
    };

    private static Vector3[] GetPositions2F()
    {
        var p = new Vector3[7];
        for (int i = 0; i < 7; i++)
            p[i] = new Vector3(-Positions1F[i].x, Positions1F[i].y, Positions1F[i].z);
        return p;
    }

    [MenuItem("Playgroumd/Build Scene")]
    public static void BuildScene()
    {
        DestroyIfExists("Panels_1F_과보호");
        DestroyIfExists("Panels_2F_자립");
        DestroyIfExists("Managers");

        SetupCamera();

        var root1F   = new GameObject("Panels_1F_과보호");
        var root2F   = new GameObject("Panels_2F_자립");
        var panels1F = new PanelPlayer[7];
        var panels2F = new PanelPlayer[7];
        var pos2F    = GetPositions2F();

        for (int i = 0; i < 7; i++)
        {
            panels1F[i] = CreatePanel(
                $"Panel_1F_{i}_{PanelNames[i]}",
                Positions1F[i], Scales1F[i],
                PanelNames[i], true, IsTransition[i],
                root1F.transform
            );
            panels2F[i] = CreatePanel(
                $"Panel_2F_{i}_{PanelNames[i]}",
                pos2F[i], Scales1F[i],
                PanelNames[i], false, IsTransition[i],
                root2F.transform
            );
        }

        CreateManagers(panels1F, panels2F);

        // 손전등 빛 생성 (생애주기 패널만)
        CreateFlashlights(panels1F, root1F.transform, true);
        CreateFlashlights(panels2F, root2F.transform, false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Playgroumd 씬 생성 완료",
            "총 14개 패널 + 손전등 8개 생성\n\n" +
            "각 패널 Inspector에서 영상 클립 연결 필요\n" +
            "RT → Material Base(RGB) 연결 필요",
            "확인"
        );
    }

    private static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var obj = new GameObject("Main Camera");
            obj.tag = "MainCamera";
            cam     = obj.AddComponent<Camera>();
        }
        cam.orthographic       = true;
        cam.orthographicSize   = 270f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.backgroundColor    = Color.black;
        cam.clearFlags         = CameraClearFlags.SolidColor;
    }

    private static PanelPlayer CreatePanel(
        string    objName,
        Vector3   position,
        Vector3   scale,
        string    panelName,
        bool      isScreen1,
        bool      isTransition,
        Transform parent)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = objName;
        quad.transform.SetParent(parent);
        quad.transform.position   = position;
        quad.transform.localScale = scale;
        Object.DestroyImmediate(quad.GetComponent<MeshCollider>());

        // Material — Unlit/Transparent
        var mat  = new Material(Shader.Find("Unlit/Transparent"));
        mat.name = $"Mat_{objName}";
        mat.color = new Color(1f, 1f, 1f, 0f); // 초기 투명
        System.IO.Directory.CreateDirectory("Assets/Materials/Panels");
        string path = $"Assets/Materials/Panels/{mat.name}.mat";
        if (!System.IO.File.Exists(path))
            AssetDatabase.CreateAsset(mat, path);
        quad.GetComponent<Renderer>().sharedMaterial = mat;

        // VideoPlayer
        var vp               = quad.AddComponent<VideoPlayer>();
        vp.renderMode        = VideoRenderMode.RenderTexture;
        vp.playOnAwake       = false;
        vp.waitForFirstFrame = true;
        vp.isLooping         = false;
        vp.skipOnDrop        = false;

        // RenderTexture
        var rt = new RenderTexture(1920, 1080, 24);
        rt.name = $"RT_{objName}";
        System.IO.Directory.CreateDirectory("Assets/RenderTextures");
        string rtPath = $"Assets/RenderTextures/{rt.name}.renderTexture";
        if (!System.IO.File.Exists(rtPath))
            AssetDatabase.CreateAsset(rt, rtPath);
        vp.targetTexture = rt;

        // PanelPlayer
        var panel = quad.AddComponent<PanelPlayer>();
        var so    = new SerializedObject(panel);
        so.FindProperty("panelName").stringValue              = panelName;
        so.FindProperty("isScreen1").boolValue                = isScreen1;
        so.FindProperty("isTransition").boolValue             = isTransition;
        so.FindProperty("interactionStart").floatValue        = 19f;
        so.FindProperty("interactionLoopEnd").floatValue       = 24f;
        so.FindProperty("renderTexture").objectReferenceValue = rt;
        so.FindProperty("quadRenderer").objectReferenceValue  = quad.GetComponent<Renderer>();
        so.ApplyModifiedProperties();

        return panel;
    }

    private static void CreateFlashlights(PanelPlayer[] panels, Transform parent, bool isScreen1)
    {
        int[] stageIndices = { 0, 2, 4, 6 };

        foreach (int idx in stageIndices)
        {
            if (idx >= panels.Length) continue;

            var panelPos   = panels[idx].gameObject.transform.position;
            var panelScale = panels[idx].gameObject.transform.localScale;

            var beam     = GameObject.CreatePrimitive(PrimitiveType.Quad);
            beam.name    = $"Flashlight_{(isScreen1 ? "1F" : "2F")}_{idx}";
            beam.transform.SetParent(parent);

            float beamH = panelScale.y * 0.9f;
            float beamW = panelScale.x * 0.8f;

            beam.transform.position   = new Vector3(
                panelPos.x,
                panelPos.y - panelScale.y * 0.5f - beamH * 0.5f,
                -0.1f // 패널보다 앞에 (빛이 위에 보여야 함)
            );
            beam.transform.localScale = new Vector3(beamW, beamH, 1f);

            Object.DestroyImmediate(beam.GetComponent<MeshCollider>());
            beam.AddComponent<FlashlightBeam>();
        }
    }

    private static void CreateManagers(PanelPlayer[] panels1F, PanelPlayer[] panels2F)
    {
        var mgr  = new GameObject("Managers");
        var em   = mgr.AddComponent<ExhibitionManager>();
        var emSo = new SerializedObject(em);

        var p1F = emSo.FindProperty("panels1F");
        var p2F = emSo.FindProperty("panels2F");
        p1F.arraySize = 7;
        p2F.arraySize = 7;
        for (int i = 0; i < 7; i++)
        {
            p1F.GetArrayElementAtIndex(i).objectReferenceValue = panels1F[i];
            p2F.GetArrayElementAtIndex(i).objectReferenceValue = panels2F[i];
        }
        emSo.FindProperty("closingHoldTime").floatValue = 8f;
        emSo.FindProperty("fadeOutDuration").floatValue = 2f;
        emSo.FindProperty("idleBeforeLoop").floatValue  = 3f;
        emSo.ApplyModifiedProperties();

        var arduino   = mgr.AddComponent<ArduinoManager>();
        var arduinoSo = new SerializedObject(arduino);
        arduinoSo.FindProperty("useSerial").boolValue = false;
        arduinoSo.ApplyModifiedProperties();

        // TimerInteraction, TDConnectionManager 제거됨 (OSC 미사용)
    }

    private static void DestroyIfExists(string name)
    {
        var obj = GameObject.Find(name);
        if (obj) Object.DestroyImmediate(obj);
    }
}
#endif
