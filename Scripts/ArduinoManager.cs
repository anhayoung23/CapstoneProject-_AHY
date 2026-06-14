using System.IO.Ports;
using UnityEngine;

/// <summary>
/// Arduino 양방향 통신
/// Unity → Arduino : 서보 제어 ("STAGE1_ON" 등)
/// Arduino → Unity : 릴 감김 ("REEL" 또는 "NEXT_VIDEO_READY*")
/// useSerial = false → Space 키로 디버그
/// </summary>
public class ArduinoManager : MonoBehaviour
{
    public static ArduinoManager Instance { get; private set; }

    [Header("Serial 설정")]
    [SerializeField] private string portName  = "/dev/cu.usbmodem14101";
    [SerializeField] private int    baudRate  = 9600;
    [SerializeField] private bool   useSerial = true;

    [Header("디버그 키")]
    [SerializeField] private KeyCode reelKey = KeyCode.Space;

    public int ActiveStage { get; private set; }

    private SerialPort _port;
    private bool       _portOpen;

    private const string REEL_SIGNAL        = "REEL";
    private const string SERVO_ON_FMT       = "SERVO{0}_ON";
    private const string SERVO_OFF_FMT      = "SERVO{0}_OFF";
    private const string NEXT_VIDEO_SIGNAL  = "NEXT_VIDEO_READY";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (useSerial) OpenPort();
    }

    void Update()
    {
        // 키보드 입력은 LateUpdate에서 처리 (코루틴 상태 업데이트 이후)
        if (!_portOpen || _port.BytesToRead <= 0) return;
        try
        {
            while (_port.BytesToRead > 0)
            {
                string msg = _port.ReadLine().Trim();

                if (string.IsNullOrEmpty(msg)) return;

                Debug.Log($"[Arduino 수신] {msg}");

                // REEL 또는 NEXT_VIDEO_READY_* 수신 시 릴 입력으로 처리
                if (msg == REEL_SIGNAL || msg.StartsWith(NEXT_VIDEO_SIGNAL))
                {
                    NotifyReel();
                    ExhibitionManager.Instance?.OnAnyTouch();
                }
            }
        }
        catch (System.TimeoutException)
        {
            // 읽을 데이터 없을 때 무시
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Arduino] 수신 오류: {e.Message}");
        }
    }

    void LateUpdate()
    {
        // 코루틴(WaitUntil) 실행 후 감지 → 1프레임 타이밍 문제 해결
        if (Input.GetKeyDown(reelKey))
        {
            NotifyReel();
            ExhibitionManager.Instance?.OnAnyTouch();
        }
    }

    void OnDestroy() => ClosePort();

    // ── 공개 API ──────────────────────────────────────────

    public void SetActiveStage(int stage)
    {
        ActiveStage = stage;
        Debug.Log($"[Arduino] 활성 단계 → Stage {stage}");
    }

    public void ServoOn(int stage)
    {
        string signal = $"STAGE{stage + 1}_ON";
        SendSerial(signal);
        Debug.Log($"[Arduino] {signal} 전송");
    }

    public void ServoOff(int stage)
    {
        string signal = $"STAGE{stage + 1}_OFF";
        SendSerial(signal);
        Debug.Log($"[Arduino] {signal} 전송");
    }

    public void ReleaseRope()
    {
        SendSerial("RELEASE");
        Debug.Log("[Arduino] RELEASE 전송 → 줄 풀기");
    }

    public void FinalReleaseRope()
    {
        SendSerial("FINAL_RELEASE");
        Debug.Log("[Arduino] FINAL_RELEASE 전송 → 최종 줄 풀기");
    }

    public void ResetArduinoInteraction()
    {
        SendSerial("RESET");
        Debug.Log("[Arduino] RESET 전송 → Arduino 상태 초기화");
    }

    public void AllServoOff()
    {
        for (int i = 0; i < 4; i++) ServoOff(i);
    }

    public void SendCommand(string command)
    {
        SendSerial(command);
    }

    // ── 내부 ──────────────────────────────────────────────

    private void NotifyReel()
    {
        Debug.Log("[Arduino] 레버 회전 기준 도달 감지");
        ExhibitionManager.Instance?.OnReelInteraction();
    }

    private void SendSerial(string message)
    {
        if (_portOpen && _port != null && _port.IsOpen)
        {
            try
            {
                _port.WriteLine(message);
                Debug.Log($"[Arduino] 실제 전송: {message}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Arduino] 전송 실패: {e.Message}");
            }
        }
        else if (useSerial)
        {
            // useSerial=true인데 포트가 열리지 않은 경우만 경고
            Debug.LogWarning($"[Arduino] 포트가 열려있지 않음. 전송 실패: {message}");
        }
        // useSerial=false 시 시리얼 전송 없이 조용히 무시
    }

    private void OpenPort()
    {
        try
        {
            _port = new SerialPort(portName, baudRate)
            {
                ReadTimeout  = 20,
                WriteTimeout = 20,
                NewLine      = "\n"
            };
            _port.Open();
            _portOpen = true;
            Debug.Log($"[Arduino] 포트 열림: {portName}");
        }
        catch (System.Exception e)
        {
            _portOpen = false;
            Debug.LogWarning($"[Arduino] 포트 실패: {e.Message} → 키보드 테스트 모드 사용 가능");
        }
    }

    private void ClosePort()
    {
        if (_portOpen && _port != null && _port.IsOpen)
            _port.Close();
        _portOpen = false;
    }
}
