# CDP2 Layer Playground — 전시 시스템 문서

> Unity + Syphon/Spout + TouchDesigner 기반 프로젝션 맵핑 인터랙티브 전시
> 현재 버전: **Mac OS (Syphon)** — Windows 전환 방법은 [OS별 차이점](#os별-차이점) 참고

---

## 목차

1. [프로젝트 개요](#프로젝트-개요)
2. [OS별 차이점](#os별-차이점)
3. [시스템 구조](#시스템-구조)
4. [Unity 개발 과정](#unity-개발-과정)
5. [TouchDesigner 작업 과정](#touchdesigner-작업-과정)
6. [파일 구조](#파일-구조)
7. [주요 스크립트 설명](#주요-스크립트-설명)

---

## 프로젝트 개요

CDP2 Layer Playground는 생애주기(영아기 → 아동기 → 청소년기 → 성인기)를 주제로 한 인터랙티브 전시 시스템입니다.

관람객이 레버를 돌리면 다음 단계로 영상이 진행되며, 손전등(Flashlight) 빛이 신호로 색상이 변합니다.

**전시 구성**
- 2개 면(1면: 과보호 / 2면: 자립)에 각 7개 패널 = 총 14개 패널
- 생애주기 패널 8개 + 전환 패널 6개
- Flashlight 8개 (생애주기 패널 아래 각 1개)
- 프로젝터 2대 (면당 1대)

**기술 스택**
- Unity 2022.3 (VideoPlayer, MaterialPropertyBlock)
- KlakSyphon (Mac) / KlakSpout (Windows) — Unity → TD 텍스처 송출
- TouchDesigner (Composite, Kantan Mapper)
- Arduino (레버 인터랙션 감지)

---

## OS별 차이점

Unity와 TouchDesigner 간 텍스처 공유 프로토콜이 OS에 따라 다릅니다. **그 외 모든 설정은 동일**합니다.

| 항목 | Mac OS | Windows |
|------|--------|---------|
| 텍스처 공유 프로토콜 | Syphon | Spout |
| Unity 패키지 | KlakSyphon | KlakSpout |
| Unity 컴포넌트 | `SyphonServer` | `SpoutSender` |
| TD 수신 노드 | `SyphonSpoutIn` | `SyphonSpoutIn` (동일) |

### Mac → Windows 전환 방법

1. **패키지 교체**
   Package Manager → KlakSyphon 제거 후 KlakSpout 설치
   ```
   https://github.com/keijiro/KlakSpout.git
   ```

2. **컴포넌트 교체**
   카메라 6대에서 `SyphonServer` 컴포넌트 제거 → `SpoutSender` 추가
   설정값(Source Texture, Server Name)은 동일하게 유지

3. **TouchDesigner는 변경 없음**
   `SyphonSpoutIn` 노드가 Syphon/Spout 모두 자동 감지함

---

## 시스템 구조

```
[Unity Scene]
    ├── StoryPanels Layer      → Camera_Story_1F/2F → Syphon "Story_1F/2F"
    ├── TransitionPanels Layer → Camera_Trans_1F/2F → Syphon "Trans_1F/2F"
    └── Flashlights Layer      → Camera_Flash_1F/2F → Syphon "Flash_1F/2F"

[TouchDesigner]
    ├── SyphonSpoutIn × 6
    ├── Composite TOP (Over) × 2  ← 면당 1개, Story + Trans + Flash 합성
    └── Kantan Mapper × 2         → 프로젝터 출력

[프로젝터]
    ├── 프로젝터 1 → 1면 (과보호)
    └── 프로젝터 2 → 2면 (자립)
```

---

## Unity 개발 과정

### 1. 레이어 분리 방식 채택 배경

초기에는 패널마다 개별 Syphon 스트림을 붙이는 방식(14개 스트림)을 사용했으나 두 가지 문제가 발생했습니다.

**문제 1 — 투명 처리 불가**
개별 카메라가 검은 배경으로 찍혀 패널끼리 겹치면 검정이 앞 패널을 덮어버림. 전환 패널이 스토리 패널 위에 겹쳐 보이는 현상 발생.

**문제 2 — TD 복잡도**
SyphonSpoutIn 14개 + kantanMapper Rectangle 14개를 각각 위치 맞춰야 하며, 패널 위치가 바뀌면 TD에서 14개를 모두 다시 조정해야 하는 번거로움.

**해결 — 6카메라 레이어 방식**
Unity의 Layer + Culling Mask를 활용해 카메라 1대가 한 레이어 전체를 찍도록 분리. Syphon 스트림 6개로 줄이고, TD에서 Over 합성으로 투명 처리를 해결.

---

### 2. FlashlightBeam 구현

손전등 빛 효과를 코드로 생성하는 컴포넌트입니다.

**셰이더 선택 과정**

| 시도 | 결과 |
|------|------|
| `Unlit/Transparent` | `_Color` 속성 미지원 → 색상 변경 불가 |
| `Sprites/Default` | `_Color` 지원 → 정상 동작 ✅ |

**텍스처 생성 방식**
텍스처의 RGB는 모두 흰색(Color.white)으로 채우고, 알파 채널에만 빛 모양(그라디언트)을 담습니다. 실제 색상은 `MaterialPropertyBlock`으로 런타임에 제어합니다.

```csharp
// 텍스처: 모양(알파)만 담음
Color col = Color.white;
col.a = alpha;  // 그라디언트 알파

// 색상 변경: MaterialPropertyBlock 사용
_propBlock.SetColor("_Color", signalColor);
_renderer.SetPropertyBlock(_propBlock);
```

**인터랙션 신호 색상**
레버를 돌려야 할 때 Flashlight 색상이 빨간색으로 변하고, 레버 감지 후 원래 색으로 복귀합니다.

```csharp
// ExhibitionManager.cs
SetFlashlightSignal(stageIdx, true);   // 빨간색으로
yield return new WaitUntil(() => ReelActioned);
SetFlashlightSignal(stageIdx, false);  // 원래 색으로
```

---

### 3. PanelPlayer — 인터랙션 구간 루프

레버 대기 중 특정 구간을 반복 재생하는 기능입니다.

**Inspector 설정값**

| 항목 | 설명 |
|------|------|
| `Interaction Start` | 인터랙션 구간 시작 시간 (초) |
| `Loop At Interaction` | true = 구간 루프 / false = 정지 |
| `Interaction Loop End` | 루프 끝 시간 (초) |

**재생 흐름 (loopAtInteraction = true)**

```
Phase 1: 영상 시작 → interactionStart 도달까지 재생
Phase 2: 구간 루프 (레버 대기)
          _vp.isLooping = true
          time >= interactionLoopEnd → interactionStart로 seek
          ReelHandled == true → 루프 탈출
Phase 3: 레버 감지 → 이어서 재생 → 영상 끝
```

**구현 시 주의사항**
- `_vp.isLooping = true` 없이 seek만 하면 클립 끝에서 멈춤
- `t < interactionStart - 1f` 조건으로 클립 루프 후 처음으로 돌아가는 엣지케이스 처리

---

### 4. 6카메라 레이어 분리

**Unity Layer 생성**
`Edit → Project Settings → Tags and Layers`에 3개 레이어 추가:
- `StoryPanels`
- `TransitionPanels`
- `Flashlights`

**자동 레이어 지정**
`Tools → Assign Layers (Story · Trans · Flash)` 메뉴 실행으로 전체 패널/Flashlight에 레이어 자동 지정. 내부적으로 `PanelPlayer.isTransition` 값을 읽어 분류.

**카메라 6대 설정**

| 항목 | 값 |
|------|-----|
| Projection | Orthographic |
| Orthographic Size | 270 (Main Camera와 동일) |
| Clear Flags | Solid Color |
| Background | 검정, **Alpha = 0** (투명 합성 필수) |
| Culling Mask | 해당 레이어만 단독 체크 |
| Target Texture | 개별 RenderTexture 연결 |

> **Target Texture가 반드시 필요한 이유**
> 없으면 카메라가 스크린 전체에 렌더링하고 Syphon이 스크린 합성본을 잡아 레이어 분리가 불가능합니다.

---

### 5. RenderTexture 해상도

영상 원본 해상도에 맞춰 RT를 설정해야 화질 손실이 없습니다.

| 패널 종류 | RT 해상도 | 영상 비율 |
|-----------|-----------|-----------|
| Story (생애주기) | 1920×1080 | 16:9 가로형 |
| Transition (전환) | 1080×1620 | 2:3 세로형 |
| Flashlights | 1920×1080 | 16:9 |

`Tools → Resize Transition Panels (2:3)` 메뉴로 전환 패널 Scale과 RT를 한 번에 변경 가능.

---

### 6. 맵핑 프리뷰

**MappingScene 전용 씬** 사용:

1. `Managers` 오브젝트 비활성화
2. `MappingPreview` 컴포넌트가 붙은 빈 GameObject 배치
3. Play → 0.5초 후 모든 패널 첫 프레임 고정 표시

에디터 메뉴 `Tools → Mapping Preview → Start All Panels`로도 실행 가능 (Play 중).

---

## TouchDesigner 작업 과정

### 1. Syphon 스트림 수신

Unity Play 모드 상태에서 TD에 SyphonSpoutIn TOP 6개 생성 후 연결합니다.

| 노드 이름 | Sender Name |
|-----------|-------------|
| Story_1F | Unity:Story_1F |
| Trans_1F | Unity:Trans_1F |
| Flash_1F | Unity:Flash_1F |
| Story_2F | Unity:Story_2F |
| Trans_2F | Unity:Trans_2F |
| Flash_2F | Unity:Flash_2F |

> 노란 경고(⚠) 표시는 Unity가 Play 중이 아닐 때 나타남. Play 모드 진입 후 사라집니다.

---

### 2. Composite (레이어 합성)

면당 Composite TOP 1개 생성 후 3개 SyphonSpoutIn을 연결합니다.

```
[1면 comp1]             [2면 comp2]
Story_1F → Input 0     Story_2F → Input 0
Trans_1F → Input 1     Trans_2F → Input 1
Flash_1F → Input 2     Flash_2F → Input 2
Operation: Over         Operation: Over
```

**Over 합성 원리**
알파=0(투명) 영역은 아래 레이어가 그대로 보입니다. 활성화된 패널만 불투명하게 나타나므로 레이어 순서가 자동으로 유지됩니다.

---

### 3. Kantan Mapper 맵핑

1. kantanMapper COMP 파라미터 → `Open Kantan Window` 클릭
2. Kantan Window 내 우클릭 → `Add Shape → Rectangle`
3. Rectangle 파라미터 → Texture 항목에 `/project1/comp1` 입력
4. 프로젝터를 켜고 물리 스크린에 투사한 상태에서 Rectangle 4개 코너 드래그 조정
5. `Save Project`로 맵핑 저장

---

## 파일 구조

```
Assets/
├── Scripts/
│   ├── ExhibitionManager.cs       # 전시 흐름 총괄 (레버 감지, 페이드, 신호 색상)
│   ├── PanelPlayer.cs             # 패널별 영상 재생 (루프, 정지, 페이드)
│   ├── FlashlightBeam.cs          # 손전등 빛 효과 (그라디언트 텍스처, 색상 제어)
│   ├── ArduinoManager.cs          # Arduino 레버 인터랙션 감지
│   ├── MappingPreview.cs          # 맵핑 씬 전용 프리뷰 자동 실행
│   └── Editor/
│       ├── ProtoSceneBuilder.cs   # 씬 자동 생성 (패널, RT, 레이아웃)
│       └── SyphonSenderSetup.cs   # 에디터 유틸리티 모음
├── Materials/Panels/              # 패널별 Unlit/Transparent Material
├── RenderTextures/                # 카메라별 RenderTexture
├── Scenes/
│   ├── SampleScene                # 본 전시 씬
│   └── MappingScene               # 맵핑 작업 전용 씬
└── Videos/                        # 영상 클립 (.mp4)
```

---

## 주요 스크립트 설명

### ExhibitionManager.cs

| 주요 필드 | 설명 |
|-----------|------|
| `panels1F / panels2F` | 7개 패널 배열 (1면 / 2면) |
| `flashlights` | Flashlight 배열 |
| `signalColor` | 인터랙션 신호 색상 (기본 빨간색) |
| `closingHoldTime` | 마지막 영상 유지 시간 |

### PanelPlayer.cs

| 메서드 | 용도 |
|--------|------|
| `PlayMain()` | 본편 재생 (인터랙션 포함) |
| `PlayAuto()` | 자동 재생 (전환 패널용) |
| `PlayPreview()` | 루프 프리뷰 (맵핑용) |
| `ShowStatic()` | 첫 프레임 고정 (맵핑 정렬용) |
| `NotifyReel()` | 레버 감지 신호 수신 |

### SyphonSenderSetup.cs (Tools 메뉴)

| 메뉴 항목 | 기능 |
|-----------|------|
| Setup Syphon Senders | 패널에 SyphonServer 자동 추가 |
| Assign Layers | 레이어 자동 지정 |
| Resize Transition Panels (2:3) | 전환 패널 Scale + RT 해상도 변경 |
| Mapping Preview → Start | 모든 패널 프리뷰 시작 |
| Mapping Preview → Stop | 모든 패널 리셋 |
