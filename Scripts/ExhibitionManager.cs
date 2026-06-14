using System.Collections;
using UnityEngine;

/// <summary>
/// Playgroumd 전시 메인 매니저
/// ─────────────────────────────────────────────────────────
/// 전시 전개:
///  1. 입장        — 1·2면 패널 전체 Freeze 상태로 표시  (2회차~)
///  2. 안내        — 전체 검은 화면 + TD 안내 텍스트     (2회차~)
///  3. 1면 전개    — 영아기→성인기 (인터랙션 포함)
///  4. 2면 전개    — 영아기→성인기 (자동 재생)
///  4.5 마무리     — 1·2면 단계 패널 8개 + 조명 동시 등장
///                   → 터치 입력이 있을 때까지 유지
///  5. 작품 설명   — TD 설명 화면
///  6. 퇴장        — 페이드 아웃 → 루프 재시작
/// ─────────────────────────────────────────────────────────
/// ※ 첫 루프는 검은 화면 없이 바로 1면 전개로 시작
/// </summary>
public class ExhibitionManager : MonoBehaviour
{
    public static ExhibitionManager Instance { get; private set; }

    public enum State
    {
        Idle,
        Introducing,        // 1. 입장 — 패널 전체 보임
        Guiding,            // 2. 안내 — 검은 화면 + 1면 안내
        Playing,
        WaitingInteraction,
        Transitioning,
        Face2Playing,       // 4. 2면 전개
        AllPanels,          // 4.5 마무리 — 모든 단계 패널 + 조명 등장
        Explaining,         // 5. 작품 설명
        FadingOut           // 6. 퇴장
    }

    public State CurrentState { get; private set; }
    public int   PanelIndex   { get; private set; }
    public bool  ReelActioned { get; private set; }

    [Header("1면 패널 — 과보호")]
    [SerializeField] private PanelPlayer[] panels1F = new PanelPlayer[7];

    [Header("2면 패널 — 자립")]
    [SerializeField] private PanelPlayer[] panels2F = new PanelPlayer[7];

    [Header("타이밍 (Inspector에서 조정 가능)")]
    [Tooltip("1. 입장 화면 유지 시간 (초)")]
    [SerializeField] private float introduceHoldTime = 5f;
    [Tooltip("2. 안내 검은 화면 시간 (초)")]
    [SerializeField] private float guideHoldTime     = 12f;
    [Tooltip("안내 후 1면 시작 전 대기 (초)")]
    [SerializeField] private float idleBeforeLoop    = 3f;
    [Tooltip("4.5 마무리 최소 유지 시간 — 이후 터치 대기")]
    [SerializeField] private float allPanelHoldTime  = 5f;
    [Tooltip("5. 작품 설명 유지 시간 (초)")]
    [SerializeField] private float explainHoldTime   = 10f;
    [Tooltip("페이드 아웃 / 디졸브 길이 (초)")]
    [SerializeField] private float fadeOutDuration   = 2f;
    [SerializeField] private float face2LightDelay   = 0.5f;

    [Header("인터랙션 신호 색상")]
    [Tooltip("레버를 돌려야 할 때 손전등이 바뀌는 색상")]
    [SerializeField] private Color signalColor = new Color(1f, 0.15f, 0.05f, 1f); // 붉은색

    private static readonly bool[] HasInteraction =
        { true, false, true, false, true, false, true };

    private static readonly string[] PanelNames =
        { "영아기", "전환①", "아동기", "전환②", "청소년기", "전환③", "성인기" };

    private static readonly int[] StageIndices = { 0, 2, 4, 6 };

    [Header("1면 손전등 (영아기·아동기·청소년기·성인기 순서)")]
    [SerializeField] private FlashlightBeam[] flashlights = new FlashlightBeam[4];

    [Header("2면 손전등 — 데칼코마니 (영아기·아동기·청소년기·성인기 순서)")]
    [SerializeField] private FlashlightBeam[] flashlights2F = new FlashlightBeam[4];

    [Header("1면 마지막 단계(성인기) — 조명 자리 영상")]
    [SerializeField] private PanelPlayer lastStageFlashlightPanel;

    private ArduinoManager _arduino;
    private bool            _firstLoop         = true;
    private bool            _allPanelsContinue = false;
    private bool            _skipIntroducing   = false;
    private int             _runCount          = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _arduino = GetComponent<ArduinoManager>();
        StartCoroutine(RunExhibition());
    }

    #region 공개 API

    public void OnReelInteraction()
    {
        if (PanelIndex < 0 || PanelIndex >= panels1F.Length) return;
        if (panels1F[PanelIndex] == null || !panels1F[PanelIndex].IsAtInteractionPoint) return;

        ReelActioned = true;
        panels1F[PanelIndex].NotifyReel();
        _arduino?.ServoOff(GetStageIndex(PanelIndex));
        Debug.Log($"[Exhibition] 릴 감김 — {PanelNames[PanelIndex]}");
    }

    /// <summary>터치/레버 입력 시 호출</summary>
    public void OnAnyTouch()
    {
        if (CurrentState == State.AllPanels)
        {
            _allPanelsContinue = true;
            Debug.Log("[Exhibition] 터치 감지 → 마무리 단계 종료");
        }
        if (CurrentState == State.Introducing)
        {
            _skipIntroducing = true;
            Debug.Log("[Exhibition] 입장 중 입력 감지 → 바로 1면 전개");
        }
    }

    #endregion

    public void SetAlpha(float alpha) { }

    #region 전시 플로우

    private IEnumerator RunExhibition()
    {
        while (true)
        {
            // 1회차·2회차 모두 바로 1면 전개
            ResetAll();
            Debug.Log("[Exhibition] 바로 1면 전개");

            _firstLoop = false;

            // ══ 3. 1면 전개 ════════════════════════════════
            for (int i = 0; i < 7; i++)
            {
                PanelIndex   = i;
                ReelActioned = false;

                if (ShouldReleaseRopeOnStart(i))
                {
                    _arduino?.ReleaseRope();
                    Debug.Log($"[Exhibition] 줄 풀림 — {PanelNames[i]}");
                }

                if (HasInteraction[i])
                {
                    int stageIdx = GetStageIndex(i);
                    _arduino?.SetActiveStage(stageIdx);
                    SetFlashlight(stageIdx, true);

                    SetState(State.Playing);
                    panels1F[i].PlayMain();

                    yield return new WaitUntil(() => panels1F[i].IsAtInteractionPoint);

                    _arduino?.ServoOn(stageIdx);
                    SetState(State.WaitingInteraction);

                    // 인터랙션 신호 — 손전등을 붉은색으로
                    SetFlashlightSignal(stageIdx, true);

                    yield return new WaitUntil(() => ReelActioned);

                    // 레버 돌림 완료 — 원래 색상 복구
                    SetFlashlightSignal(stageIdx, false);

                    SetState(State.Transitioning);
                    yield return new WaitUntil(() => panels1F[i].IsFinished);
                }
                else
                {
                    SetState(State.Transitioning);
                    panels1F[i].PlayMain();
                    yield return new WaitUntil(() => panels1F[i].IsFinished);
                }

                // 패널 끝 → 마지막 프레임 유지 (마무리 단계에서 재사용)
                if (panels1F[i] != null)
                    yield return StartCoroutine(panels1F[i].FadeOutAndFreeze());

                if (HasInteraction[i])
                {
                    int stageIdx = GetStageIndex(i);

                    if (i == 6 && lastStageFlashlightPanel != null)
                    {
                        SetFlashlight(stageIdx, false);
                        lastStageFlashlightPanel.PlayAuto();
                        yield return new WaitUntil(() => lastStageFlashlightPanel.IsFinished);
                        yield return StartCoroutine(lastStageFlashlightPanel.FadeOutAndFreeze());
                    }
                    else
                    {
                        SetFlashlight(stageIdx, false);
                    }
                }

                yield return new WaitForSeconds(0.3f);
            }

            // ══ 4. 2면 전개 ════════════════════════════════
            SetState(State.Face2Playing);
            Debug.Log("[Exhibition] ▶ 2면 전개 시작");
            yield return StartCoroutine(RunFace2());

            // ══ 4.5 마무리 — 1회차만 표시 ═══════════════════
            if (_runCount == 0)
            {
                SetState(State.AllPanels);
                _allPanelsContinue = false;
                Debug.Log("[Exhibition] ✦ 마무리 — 단계 패널 전체 + 손전등 등장");
                yield return StartCoroutine(FadeInAllStagePanels());

                yield return new WaitForSeconds(allPanelHoldTime);
                Debug.Log("[Exhibition] ✦ 터치 대기 중...");
                yield return new WaitUntil(() => _allPanelsContinue);

                yield return StartCoroutine(FadeOutStagePanels());
            }

            _runCount++;

            // ══ 6. 퇴장 ════════════════════════════════════
            SetState(State.FadingOut);
            yield return StartCoroutine(FadeOutAll());
        }
    }

    private IEnumerator FadeOutAll()
    {
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
            for (int i = 0; i < panels1F.Length; i++)
            {
                panels1F[i]?.SetAlpha(alpha);
                panels2F[i]?.SetAlpha(alpha);
            }
            lastStageFlashlightPanel?.SetAlpha(alpha);
            yield return null;
        }
        Debug.Log("[Exhibition] 페이드 아웃 완료");
    }

    #endregion

    #region 2면 전개

    private IEnumerator RunFace2()
    {
        int flashIdx = 0;

        for (int i = 0; i < 7; i++)
        {
            if (panels2F[i] == null || !HasClip(panels2F[i]))
            {
                if (HasInteraction[i]) flashIdx++;
                continue;
            }

            if (HasInteraction[i])
            {
                Debug.Log($"[Exhibition] 2면 조명 켜짐 — {PanelNames[i]}");
                SetFlashlight2F(flashIdx, true);
                yield return new WaitForSeconds(face2LightDelay);

                panels2F[i].PlayAuto();
                yield return new WaitUntil(() => panels2F[i].IsFinished);

                yield return StartCoroutine(panels2F[i].FadeOutAndFreeze());
                SetFlashlight2F(flashIdx, false);
                flashIdx++;
            }
            else
            {
                panels2F[i].PlayAuto();
                yield return new WaitUntil(() => panels2F[i].IsFinished);
                yield return StartCoroutine(panels2F[i].FadeOutAndFreeze());
            }

            yield return new WaitForSeconds(0.3f);
        }

        Debug.Log("[Exhibition] 2면 순차 재생 완료");
    }

    #endregion

    #region 마무리 — 단계 패널 8개 + 손전등 동시 등장

    private IEnumerator FadeInAllStagePanels()
    {
        // 전환 패널 강제 숨김 (1,3,5번 인덱스)
        int[] transitionIdx = { 1, 3, 5 };
        foreach (int ti in transitionIdx)
        {
            panels1F[ti]?.ResetPanel();
            panels2F[ti]?.ResetPanel();
        }

        // renderer 활성화 (alpha 0 초기화)
        for (int i = 0; i < StageIndices.Length; i++)
        {
            panels1F[StageIndices[i]]?.ShowPanel();
            panels2F[StageIndices[i]]?.ShowPanel();
        }
        lastStageFlashlightPanel?.ShowPanel(); // 1면 4단계 조명 자리 영상

        // 손전등 전부 켜기
        for (int i = 0; i < flashlights.Length; i++)  SetFlashlight(i, true);
        for (int i = 0; i < flashlights2F.Length; i++) SetFlashlight2F(i, true);

        // 동시 페이드 인
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeOutDuration);
            for (int i = 0; i < StageIndices.Length; i++)
            {
                panels1F[StageIndices[i]]?.SetAlpha(alpha);
                panels2F[StageIndices[i]]?.SetAlpha(alpha);
            }
            lastStageFlashlightPanel?.SetAlpha(alpha);
            yield return null;
        }
        Debug.Log("[Exhibition] ✦ 8개 단계 패널 + 손전등 모두 표시됨");
    }

    private IEnumerator FadeOutStagePanels()
    {
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
            for (int i = 0; i < StageIndices.Length; i++)
            {
                panels1F[StageIndices[i]]?.SetAlpha(alpha);
                panels2F[StageIndices[i]]?.SetAlpha(alpha);
            }
            lastStageFlashlightPanel?.SetAlpha(alpha);
            yield return null;
        }
        AllFlashlightsOff();

        // AllPanels 종료 후 모든 패널 완전 숨김 (FadeOut 때 잔여 패널 안 보이게)
        for (int i = 0; i < panels1F.Length; i++)
        {
            panels1F[i]?.ResetPanel();
            panels2F[i]?.ResetPanel();
        }
        lastStageFlashlightPanel?.ResetPanel();
    }

    #endregion

    #region Arduino 줄 풀림 조건

    private bool ShouldReleaseRopeOnStart(int panelIndex)
    {
        return panelIndex == 0 || panelIndex == 1 || panelIndex == 3 || panelIndex == 5;
    }

    #endregion

    #region 유틸리티

    private void RestoreAllPanelAlpha()
    {
        for (int i = 0; i < panels1F.Length; i++)
        {
            panels1F[i]?.SetAlpha(1f);
            panels2F[i]?.SetAlpha(1f);
        }
        lastStageFlashlightPanel?.SetAlpha(1f);
    }

    private void SetFlashlight(int stageIdx, bool on)
    {
        if (stageIdx >= 0 && stageIdx < flashlights.Length)
        {
            if (flashlights[stageIdx] == null)
                Debug.LogWarning($"[Exhibition] flashlights[{stageIdx}] 가 Inspector에 연결되지 않았습니다!");
            flashlights[stageIdx]?.SetActive(on);
        }
    }

    private void SetFlashlight2F(int stageIdx, bool on)
    {
        if (stageIdx >= 0 && stageIdx < flashlights2F.Length)
        {
            if (flashlights2F[stageIdx] == null)
                Debug.LogWarning($"[Exhibition] flashlights2F[{stageIdx}] 가 Inspector에 연결되지 않았습니다!");
            flashlights2F[stageIdx]?.SetActive(on);
        }
    }

    private void SetFlashlightSignal(int stageIdx, bool signal)
    {
        if (stageIdx >= 0 && stageIdx < flashlights.Length)
        {
            if (signal) flashlights[stageIdx]?.SetBeamColor(signalColor);
            else        flashlights[stageIdx]?.ResetBeamColor();
        }
    }

    private void AllFlashlightsOff()
    {
        foreach (var f in flashlights)   f?.SetActive(false);
        foreach (var f in flashlights2F) f?.SetActive(false);
    }

    private void ResetAll()
    {
        for (int i = 0; i < panels1F.Length; i++)
        {
            panels1F[i]?.ResetPanel();
            panels2F[i]?.ResetPanel();
        }
        lastStageFlashlightPanel?.ResetPanel();
        _arduino?.AllServoOff();
        _arduino?.ResetArduinoInteraction();
        AllFlashlightsOff();
        PanelIndex   = 0;
        ReelActioned = false;
    }

    private void SetState(State s)
    {
        CurrentState = s;
        Debug.Log($"[Exhibition] ▶ {s} | Panel:{PanelIndex}");
    }

    private int GetStageIndex(int panelIndex)
    {
        for (int i = 0; i < StageIndices.Length; i++)
            if (StageIndices[i] == panelIndex) return i;
        return 0;
    }

    private bool HasClip(PanelPlayer panel)
    {
        if (panel == null) return false;
        return panel.HasMainClip;
    }

    #endregion
}
