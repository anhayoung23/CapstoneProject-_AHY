// ─────────────────────────────────────────────
// Playgroumd — Arduino Uno 펌웨어
// Unity ↔ Arduino 양방향 통신 버전
//
// 하드웨어:
//   스텝모터 STEP핀 → 3번
//   스텝모터 DIR핀  → 2번
//   버튼(레버)      → 10번 (INPUT_PULLUP)
//
// Unity → Arduino:
//   "RESET"  → releaseCount 초기화, 잠금 해제
//
// Arduino → Unity:
//   "REEL"   → 버튼 눌릴 때마다 전송 (Unity가 영상 재개)
// ─────────────────────────────────────────────

// 스텝모터 핀
const int stepPin = 3;
const int dirPin  = 2;

// 버튼 핀
const int PinSW = 10;

// 스텝 설정
const int stepsPerRelease  = 800;   // 일반 해제 1회 스텝 수
const int maxNormalRelease = 4;     // 일반 해제 최대 횟수
const int finalReleaseSteps = 3000; // 최종 해제 스텝 수

int  releaseCount      = 0;
bool interactionLocked = false;
int  lastButtonState   = HIGH;

// ─────────────────────────────────────────────
void setup() {
  pinMode(stepPin, OUTPUT);
  pinMode(dirPin,  OUTPUT);
  pinMode(PinSW,   INPUT_PULLUP);

  digitalWrite(dirPin, HIGH); // 줄 풀리는 방향

  Serial.begin(9600);
  Serial.println("Start");
}

// ─────────────────────────────────────────────
void loop() {

  // ── Unity로부터 명령 수신 ──────────────────
  if (Serial.available() > 0) {
    String cmd = Serial.readStringUntil('\n');
    cmd.trim();

    if (cmd == "RESET") {
      // 루프 재시작 시 Unity가 보내는 초기화 신호
      releaseCount      = 0;
      interactionLocked = false;
      Serial.println("RESET_OK");
    }
    // 향후 STAGE_ON/OFF 등 추가 가능
  }

  // ── 버튼(레버) 감지 ───────────────────────
  int buttonState = digitalRead(PinSW);

  if (lastButtonState == HIGH && buttonState == LOW) {

    if (interactionLocked) {
      Serial.println("Locked");
      delay(200);
      lastButtonState = buttonState;
      return;
    }

    releaseCount++;

    // Unity에 레버 회전 신호 전송 → 영상 재개
    Serial.println("REEL");

    if (releaseCount <= maxNormalRelease) {
      Serial.print("Release ");
      Serial.println(releaseCount);
      moveStepper(stepsPerRelease);
    }
    else if (releaseCount == maxNormalRelease + 1) {
      Serial.println("Final Release");
      moveStepper(finalReleaseSteps);
      interactionLocked = true;
      Serial.println("Interaction Locked");
    }

    delay(200);
  }

  lastButtonState = buttonState;
}

// ─────────────────────────────────────────────
void moveStepper(int steps) {
  digitalWrite(dirPin, HIGH);
  for (int x = 0; x < steps; x++) {
    digitalWrite(stepPin, HIGH);
    delayMicroseconds(500);
    digitalWrite(stepPin, LOW);
    delayMicroseconds(500);
  }
}
