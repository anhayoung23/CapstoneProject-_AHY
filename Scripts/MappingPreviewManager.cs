using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 프로젝션 매핑 세팅용 프리뷰 씬 매니저
/// ─────────────────────────────────────────────────────────
/// · 모든 패널 영상을 루프로 재생
/// · 모든 손전등 ON
/// · 인터랙션 없음 — 위치 확인 전용
/// </summary>
public class MappingPreviewManager : MonoBehaviour
{
    [Header("1면 패널")]
    [SerializeField] private PanelPlayer[] panels1F = new PanelPlayer[7];

    [Header("2면 패널")]
    [SerializeField] private PanelPlayer[] panels2F = new PanelPlayer[7];

    [Header("1면 손전등")]
    [SerializeField] private FlashlightBeam[] flashlights1F = new FlashlightBeam[4];

    [Header("2면 손전등")]
    [SerializeField] private FlashlightBeam[] flashlights2F = new FlashlightBeam[4];

    [Header("1면 조명 자리 영상")]
    [SerializeField] private PanelPlayer lastStageFlashlightPanel;

    void Start()
    {
        // 모든 패널 루프 재생
        foreach (var p in panels1F)  p?.PlayPreview();
        foreach (var p in panels2F)  p?.PlayPreview();
        lastStageFlashlightPanel?.PlayPreview();

        // 모든 손전등 ON
        foreach (var f in flashlights1F)  f?.SetActive(true);
        foreach (var f in flashlights2F)  f?.SetActive(true);

        Debug.Log("[MappingPreview] 모든 패널 + 손전등 표시 완료");
    }
}
