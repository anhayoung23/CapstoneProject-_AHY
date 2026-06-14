using UnityEngine;

/// <summary>
/// 맵핑 씬 전용 — Play 시작하면 모든 PanelPlayer를 자동으로 프리뷰 재생
/// Managers 비활성화 + 이 스크립트 활성화 상태로 Play
/// </summary>
public class MappingPreview : MonoBehaviour
{
    [Tooltip("몇 초 뒤에 프리뷰 시작할지 (VideoPlayer 준비 시간)")]
    [SerializeField] private float startDelay = 0.5f;

    void Start()
    {
        Invoke(nameof(StartPreview), startDelay);
    }

    void StartPreview()
    {
        PanelPlayer[] panels = FindObjectsOfType<PanelPlayer>(true);
        int count = 0;
        foreach (PanelPlayer p in panels)
        {
            if (!p.gameObject.activeInHierarchy)
                p.gameObject.SetActive(true);
            p.ShowStatic();
            count++;
        }
        Debug.Log($"[MappingPreview] {count}개 패널 정적 표시");
    }
}
