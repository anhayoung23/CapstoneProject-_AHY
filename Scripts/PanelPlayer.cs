using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 패널 하나의 VideoPlayer 재생 담당
/// ─────────────────────────────────────────────────────────
/// · 본편 패널: 영상 재생 → interactionStart초에 일시정지 (1회만) → 재개 → 끝
/// · 전환 패널: 처음부터 끝까지 자동 재생
/// · 디졸브: Sprites/Default 셰이더로 FadeIn 지원
/// ─────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class PanelPlayer : MonoBehaviour
{
    [Header("패널 정보")]
    [SerializeField] private string panelName    = "영아기";
    [SerializeField] private bool   isScreen1    = true;
    [SerializeField] private bool   isTransition = false;

    [Header("영상 클립")]
    [SerializeField] private VideoClip mainClip;

    public bool HasMainClip => mainClip != null;

    [Header("인터랙션 일시정지 시점 (본편 패널만)")]
    [SerializeField] private float interactionStart     = 19f;
    [Tooltip("true면 interactionStart~interactionLoopEnd 구간을 루프. false면 기존처럼 정지")]
    [SerializeField] private bool  loopAtInteraction = false;
    [Tooltip("루프 끝 시점 — 여기서 interactionStart로 돌아감 (loopAtInteraction=true일 때만 사용)")]
    [SerializeField] private float interactionLoopEnd = 24f;

    [Header("출력")]
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private Renderer      quadRenderer;

    [Header("디졸브")]
    [SerializeField] private float fadeDuration = 0.8f;

    // ── 공개 상태 ──────────────────────────────────────────
    public bool IsAtInteractionPoint { get; private set; }
    public bool IsFinished           { get; private set; }
    public bool IsFrozen             { get; private set; }
    public bool ReelHandled          { get; private set; }
    public bool IsPausedAtInteraction{ get; private set; }

    // ── 내부 ───────────────────────────────────────────────
    private VideoPlayer _vp;
    private Material    _mat;
    private Coroutine   _routine;
    private bool        _waitingResume;
    private bool        _interactionDone; // 단계당 1회만 인터랙션

    // ───────────────────────────────────────────────────────
    void Awake()
    {
        _vp = GetComponent<VideoPlayer>();
        _vp.renderMode        = VideoRenderMode.RenderTexture;
        _vp.targetTexture     = renderTexture;
        _vp.playOnAwake       = false;
        _vp.waitForFirstFrame = true;
        _vp.isLooping         = false;
        _vp.skipOnDrop        = false;

        if (quadRenderer != null)
            _mat = quadRenderer.material;

        Hide();
    }

    // ── 표시 / 숨김 ───────────────────────────────────────
    private void Show() { if (quadRenderer) quadRenderer.enabled = true; }
    private void Hide() { if (quadRenderer) quadRenderer.enabled = false; }

    public void SetAlpha(float alpha)
    {
        if (_mat == null) return;
        _mat.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
    }

    // ───────────────────────────────────────────────────────
    #region 공개 API

    public void PlayMain()
    {
        if (_routine != null) StopCoroutine(_routine);
        ResetState();
        Show();
        SetAlpha(0f);
        _routine = StartCoroutine(isTransition ? AutoRoutine() : StageRoutine());
    }

    public void PlayAuto()
    {
        if (_routine != null) StopCoroutine(_routine);
        ResetState();
        Show();
        SetAlpha(0f);
        _routine = StartCoroutine(AutoRoutine());
    }

    /// <summary>매핑 프리뷰용 — 클립을 루프로 즉시 재생, alpha 1 고정</summary>
    public void PlayPreview()
    {
        if (mainClip == null) return;
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        _vp.clip      = mainClip;
        _vp.isLooping = true;
        Show();
        SetAlpha(1f);
        _vp.Play();
    }

    /// <summary>맵핑 정렬용 — 첫 프레임을 RenderTexture에 올린 뒤 정지, 사라지지 않음</summary>
    public void ShowStatic()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        _vp.Stop();
        if (mainClip == null) { Show(); SetAlpha(1f); return; }
        _routine = StartCoroutine(ShowStaticRoutine());
    }

    private IEnumerator ShowStaticRoutine()
    {
        _vp.clip      = mainClip;
        _vp.isLooping = false;
        _vp.time      = 0;
        _vp.Prepare();

        // 준비 완료 대기
        yield return new WaitUntil(() => _vp.isPrepared);

        Show();
        SetAlpha(1f);

        // 첫 프레임을 RenderTexture에 올리고 즉시 정지
        _vp.Play();
        yield return null; // 한 프레임 대기
        yield return null;
        _vp.Pause();

        _routine = null;
    }

    public void Freeze()
    {
        if (_routine != null) StopCoroutine(_routine);
        IsFrozen = true;
        _vp.Pause();
        Show();
    }

    public void ResetPanel()
    {
        if (_routine != null) StopCoroutine(_routine);
        _vp.Stop();
        ResetState();
        Hide();
    }

    /// <summary>마지막 프레임을 유지한 채 투명하게 숨김 — 나중에 다시 보여줄 수 있음</summary>
    public IEnumerator FadeOutAndFreeze()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        if (_vp.isPlaying) _vp.Pause(); // 마지막 프레임 유지
        IsFrozen = true;

        float t = fadeDuration;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            SetAlpha(Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        SetAlpha(0f);
        Show(); // renderer 활성 유지 (투명 상태) → 나중에 SetAlpha(1)로 복원 가능
    }

    /// <summary>Freeze 패널을 외부에서 다시 표시할 때 사용 (alpha 0으로 초기화)</summary>
    public void ShowPanel()
    {
        Show();
        SetAlpha(0f);
    }

    public void NotifyReel()
    {
        if (!IsAtInteractionPoint) return;
        ReelHandled = true;
    }

    /// <summary>레버 회전 완료 → 루프 종료 신호</summary>
    public void SetInteractionDone()
    {
        _interactionDone = true;
    }

    public void ResumeVideo()
    {
        if (!IsPausedAtInteraction) return;
        _waitingResume        = false;
        IsPausedAtInteraction = false;
        _vp.Play();
        Debug.Log($"[PanelPlayer] ▶ 영상 재개 — {panelName}");
    }

    #endregion

    // ───────────────────────────────────────────────────────
    #region 재생 루틴

    private IEnumerator StageRoutine()
    {
        if (mainClip == null)
        {
            Debug.LogWarning($"[PanelPlayer] mainClip 없음 — {panelName}");
            IsFinished = true; Hide(); yield break;
        }

        _vp.clip = mainClip;
        _vp.Prepare();
        yield return new WaitUntil(() => _vp.isPrepared);

        _vp.Play();
        yield return StartCoroutine(FadeIn());

        // ── Phase 1: interactionStart 도달까지 재생 ──────
        yield return new WaitUntil(() =>
            !_vp.isPlaying || (float)_vp.time >= interactionStart);

        if (!_vp.isPlaying) { IsFinished = true; yield break; }

        // ── Phase 2: 인터랙션 구간 ────────────────────────
        IsAtInteractionPoint  = true;

        if (loopAtInteraction)
        {
            // 구간 루프: interactionStart ~ interactionLoopEnd 반복
            Debug.Log($"[PanelPlayer] ↻ 구간 루프 대기 ({interactionStart}s~{interactionLoopEnd}s) — {panelName}");
            _vp.isLooping = true; // 클립 끝에서 멈추지 않도록
            while (!ReelHandled)
            {
                float t = (float)_vp.time;
                // 루프 끝점 초과 또는 클립 루프로 처음으로 돌아간 경우 → 시작점으로 seek
                if (t >= interactionLoopEnd || t < interactionStart - 1f)
                    _vp.time = interactionStart;
                yield return null;
            }
            _vp.isLooping = false;
        }
        else
        {
            // 기존: 정지 후 대기
            _vp.Pause();
            IsPausedAtInteraction = true;
            Debug.Log($"[PanelPlayer] ■ 레버 대기 중 ({interactionStart}s) — {panelName}");
            yield return new WaitUntil(() => ReelHandled);
            _vp.Play();
        }

        // 레버 감지 → 이어서 재생
        Debug.Log($"[PanelPlayer] ▶ 영상 재개 — {panelName}");

        IsAtInteractionPoint  = false;
        IsPausedAtInteraction = false;
        _interactionDone      = true;

        // ── Phase 3: 영상 끝까지 재생 ────────────────────
        // 재개 직후 isPlaying 안정화 대기
        float grace = 0f;
        while (!_vp.isPlaying && grace < 0.5f)
        {
            grace += Time.deltaTime;
            yield return null;
        }
        yield return new WaitUntil(() => !_vp.isPlaying);

        IsFinished = true;
        Debug.Log($"[PanelPlayer] ✓ 본편 완료 — {panelName}");
    }

    private IEnumerator AutoRoutine()
    {
        if (mainClip == null)
        {
            Debug.LogWarning($"[PanelPlayer] mainClip 없음 — {panelName}(자동)");
            IsFinished = true; Hide(); yield break;
        }

        _vp.clip = mainClip;
        _vp.Prepare();
        yield return new WaitUntil(() => _vp.isPrepared);

        _vp.Play();
        yield return StartCoroutine(FadeIn());

        // isPlaying 시작 대기 (최대 1초)
        float waited = 0f;
        while (!_vp.isPlaying && waited < 1f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        yield return new WaitUntil(() => !_vp.isPlaying);

        IsFinished = true;
        Debug.Log($"[PanelPlayer] ✓ 자동 완료 — {panelName}");
    }

    #endregion

    // ───────────────────────────────────────────────────────
    #region 디졸브 페이드

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        SetAlpha(1f);
    }

    public IEnumerator FadeOutAndReset()
    {
        float t = fadeDuration;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            SetAlpha(Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        SetAlpha(0f);
        ResetPanel();
    }

    #endregion

    // ───────────────────────────────────────────────────────
    private void ResetState()
    {
        IsAtInteractionPoint  = false;
        IsFinished            = false;
        IsFrozen              = false;
        ReelHandled           = false;
        IsPausedAtInteraction = false;
        _waitingResume        = false;
        _interactionDone      = false;
    }
}
