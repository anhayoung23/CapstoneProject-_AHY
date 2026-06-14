using UnityEngine;

/// <summary>
/// 손전등 빛 효과
/// ─────────────────────────────────────────────────────────
/// 패널 아래에 배치 → 위로 퍼지는 반원형 빛
/// 코드로 그라디언트 텍스처 생성
/// 텍스처는 알파(모양)만 담고, RGB 색상은 material.color로 제어
/// ─────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class FlashlightBeam : MonoBehaviour
{
    [Header("빛 설정")]
    [SerializeField] private Color  beamColor     = new Color(1f, 0.97f, 0.90f, 1f); // 따뜻한 흰색
    [SerializeField] private float  beamIntensity = 1f;    // 밝기
    [SerializeField] private int    textureSize   = 256;   // 텍스처 해상도

    [Header("애니메이션 (선택)")]
    [SerializeField] private bool  flicker       = false;  // 깜빡임 효과
    [SerializeField] private float flickerSpeed  = 3f;
    [SerializeField] private float flickerAmount = 0.08f;

    private MeshRenderer         _renderer;
    private Material             _mat;
    private Texture2D            _tex;
    private MaterialPropertyBlock _propBlock;

    // ───────────────────────────────────────────────────────
    void Awake()
    {
        _renderer  = GetComponent<MeshRenderer>();
        _propBlock = new MaterialPropertyBlock();
        GenerateBeamTexture();

        // 시작 시 꺼진 상태로 초기화
        _renderer.enabled = false;
    }

    void Update()
    {
        if (!flicker) return;

        // 살짝 깜빡이는 효과 (촛불/손전등 느낌)
        float noise  = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f);
        float alpha  = beamIntensity - flickerAmount + noise * flickerAmount * 2f;
        Color c      = _mat.color;
        c.a          = Mathf.Clamp01(alpha);
        _mat.color   = c;
    }

    // ───────────────────────────────────────────────────────
    #region 텍스처 생성

    private void GenerateBeamTexture()
    {
        _tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        _tex.wrapMode   = TextureWrapMode.Clamp;
        _tex.filterMode = FilterMode.Bilinear;

        int   size    = textureSize;
        float half    = size / 2f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // UV 좌표 (-1 ~ 1)
                float u = (x - half) / half; // 좌우 (-1~1)
                float v = y / (float)size;   // 아래→위 (0~1)

                // 반원형 빛 마스크
                // 아래(v=0)에서 시작해서 위(v=1)로 갈수록 퍼짐
                float spread     = Mathf.Lerp(0.1f, 1.2f, v); // 위로 갈수록 넓어짐
                float distToCenter = Mathf.Abs(u) / spread;

                // 원형 그라디언트
                float radialFade = 1f - Mathf.Clamp01(distToCenter);
                radialFade = Mathf.Pow(radialFade, 1.5f); // 부드럽게

                // 위로 갈수록 페이드 (맨 위는 투명)
                float vertFade = 1f - Mathf.Pow(v, 0.6f);

                // 아래쪽(손전등 위치)은 좁고 밝게
                float bottomFade = Mathf.Clamp01(v * 4f);

                float alpha = radialFade * vertFade * bottomFade * beamIntensity;
                alpha = Mathf.Clamp01(alpha);

                // 텍스처는 알파(모양)만 — RGB는 material.color가 제어
                Color col = Color.white;
                col.a = alpha;
                pixels[y * size + x] = col;
            }
        }

        _tex.SetPixels(pixels);
        _tex.Apply();

        // Material 생성 — Sprites/Default: _Color 색상 제어 확실히 지원
        _mat             = new Material(Shader.Find("Sprites/Default"));
        _mat.mainTexture = _tex;
        _mat.color       = beamColor;

        _renderer.material = _mat;
        _mat = _renderer.material;
    }

    #endregion

    // ───────────────────────────────────────────────────────
    #region 공개 API

    /// <summary>빛 켜기/끄기</summary>
    public void SetActive(bool on)
    {
        if (!gameObject.activeSelf && on)
            gameObject.SetActive(true);
        _renderer.enabled = on;
        if (!on)
            gameObject.SetActive(false);
    }

    /// <summary>빛 색상 변경 (알파는 beamIntensity 유지)</summary>
    public void SetBeamColor(Color c)
    {
        c.a = beamIntensity;
        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor("_Color", c);
        _renderer.SetPropertyBlock(_propBlock);
    }

    /// <summary>기본 색상(beamColor)으로 복구</summary>
    public void ResetBeamColor()
    {
        Color c = beamColor;
        c.a     = beamIntensity;
        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor("_Color", c);
        _renderer.SetPropertyBlock(_propBlock);
    }

    /// <summary>빛 밝기 조절 (0~1)</summary>
    public void SetIntensity(float intensity)
    {
        beamIntensity = intensity;
        Color c = _mat.color;
        c.a = intensity;
        _mat.color = c;
    }

    #endregion
}
