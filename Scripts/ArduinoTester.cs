using System.IO.Ports;
using UnityEngine;

/// <summary>
/// 아두이노 단독 연결 테스트
/// ─────────────────────────────────────────────
/// ExhibitionManager 없이 아두이노만 독립적으로 테스트
/// 빈 씬에 빈 GameObject 만들고 이 컴포넌트 추가해서 사용
///
/// 테스트 키:
///   R → RESET 전송
///   1~4 → STAGE1_ON ~ STAGE4_ON 전송
///   Q → STAGE1_OFF ~ STAGE4_OFF 전송 (전체)
///   Space → 수동으로 REEL 신호 시뮬레이션
/// ─────────────────────────────────────────────
/// </summary>
public class ArduinoTester : MonoBehaviour
{
    [Header("Serial 설정")]
    [SerializeField] private string portName = "/dev/cu.usbmodem14101";
    [SerializeField] private int    baudRate = 9600;

    [Header("테스트 키")]
    [SerializeField] private KeyCode resetKey   = KeyCode.R;
    [SerializeField] private KeyCode stage1Key  = KeyCode.Alpha1;
    [SerializeField] private KeyCode stage2Key  = KeyCode.Alpha2;
    [SerializeField] private KeyCode stage3Key  = KeyCode.Alpha3;
    [SerializeField] private KeyCode stage4Key  = KeyCode.Alpha4;
    [SerializeField] private KeyCode allOffKey  = KeyCode.Q;

    private SerialPort _port;
    private bool       _connected;

    // ─────────────────────────────────────────
    void Start()
    {
        Connect();
    }

    void Update()
    {
        // 수신 처리
        if (_connected && _port.BytesToRead > 0)
        {
            try
            {
                string msg = _port.ReadLine().Trim();
                if (!string.IsNullOrEmpty(msg))
                    Debug.Log($"[ArduinoTester] ← 수신: {msg}");
            }
            catch { }
        }

        // 키 입력 테스트
        if (Input.GetKeyDown(resetKey))       Send("RESET");
        if (Input.GetKeyDown(stage1Key))      Send("STAGE1_ON");
        if (Input.GetKeyDown(stage2Key))      Send("STAGE2_ON");
        if (Input.GetKeyDown(stage3Key))      Send("STAGE3_ON");
        if (Input.GetKeyDown(stage4Key))      Send("STAGE4_ON");
        if (Input.GetKeyDown(allOffKey))
        {
            Send("STAGE1_OFF");
            Send("STAGE2_OFF");
            Send("STAGE3_OFF");
            Send("STAGE4_OFF");
        }
    }

    void OnDestroy() => Disconnect();

    // ─────────────────────────────────────────
    private void Connect()
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
            _connected = true;
            Debug.Log($"[ArduinoTester] ✓ 연결 성공 → {portName}");
        }
        catch (System.Exception e)
        {
            _connected = false;
            Debug.LogError($"[ArduinoTester] ✕ 연결 실패: {e.Message}\n포트명 확인: 터미널에서 'ls /dev/cu.*' 실행");
        }
    }

    private void Disconnect()
    {
        if (_connected && _port != null && _port.IsOpen)
            _port.Close();
        _connected = false;
    }

    private void Send(string msg)
    {
        if (!_connected || _port == null || !_port.IsOpen)
        {
            Debug.LogWarning("[ArduinoTester] 연결 안 됨");
            return;
        }
        try
        {
            _port.WriteLine(msg);
            Debug.Log($"[ArduinoTester] → 전송: {msg}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ArduinoTester] 전송 실패: {e.Message}");
        }
    }

    // ─────────────────────────────────────────
    // 에디터 화면에 상태 표시
    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.box) { fontSize = 13 };

        GUI.backgroundColor = _connected
            ? new Color(0.1f, 0.6f, 0.1f, 0.9f)
            : new Color(0.6f, 0.1f, 0.1f, 0.9f);

        GUI.Box(new Rect(10, 10, 320, 30),
            _connected ? $"● Arduino 연결됨  [{portName}]"
                       : "✕ Arduino 연결 안 됨", style);

        GUI.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.85f);
        GUI.Box(new Rect(10, 45, 320, 120),
            "[ 테스트 키 ]\n" +
            "R        → RESET\n" +
            "1/2/3/4  → STAGE1~4_ON\n" +
            "Q        → 전체 OFF\n", style);

        GUI.backgroundColor = Color.white;
    }
}
