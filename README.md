# ZeroStep

Unity 기반 모바일 퍼즐 게임 프로젝트입니다.  
플레이어는 그리드 타일 위를 드래그로 이동하며 타일 숫자를 0으로 만들어 스테이지를 클리어합니다.  
타일 숫자는 "그 타일을 몇 번 거쳐 가야 하는지"를 뜻하므로, 방문 횟수 제약이 있는 경로 찾기 퍼즐에 가깝습니다.

## 1. 게임 개요

- 장르: 터치/드래그 기반 숫자 경로 퍼즐
- 타겟: 모바일 Android / iOS (세로 고정)
- 엔진: Unity 6 (`6000.3.5f2`), URP, UI Toolkit, New Input System
- 앱 식별자: `com.crewoong.zerostep`, 버전 `1.0`
- 씬: `Assets/Scenes/SampleScene.unity` 1개
- 스테이지: `Assets/Resources/Stages/stage_1.json` ~ `stage_1000.json` (총 1,000개, 50개씩 20챕터)
- 시작 시 코드로 그리는 약 1.9초 네온 인트로(`NeonIntroElement`)가 나오며, 두 번째 실행부터는 탭으로 건너뛸 수 있습니다.
- 1,000 스테이지 클리어 시 엔딩 크레딧과 여정 통계가 표시됩니다.

## 2. 현재 구현 기준 플레이 방식

이 섹션은 "현재 코드 동작" 기준입니다. 규칙의 기준 문서는 `Assets/rules/TILE_RULES.md`입니다.

### 2.1 기본 조작

1. 시작 타일(스테이지 JSON의 `startPoint`)에서 터치를 시작합니다.
2. 손가락(또는 마우스) 드래그로 인접 타일로 이동합니다. 지나온 경로는 타일 중심을 잇는 선으로 표시됩니다.
3. 손을 떼면 현재 위치가 다음 드래그 시작점이 됩니다.
4. 마지막 남은 타일(숫자 1)에 도착해 보드 전체 합이 1이 되면 클리어합니다. 클리어 0.3초 뒤부터는 탭하면 바로 다음 스테이지로 넘어갑니다.

### 2.2 이동 규칙

- 이동은 상하좌우 인접 타일 기준입니다(대각선 불가).
- `count > 0` 인 활성 타일만 이동 대상으로 인정됩니다.
- 시작은 반드시 현재 시작 타일에서만 가능합니다.

### 2.3 숫자 감소 규칙(핵심)

- 숫자는 "들어갈 때"가 아니라 "해당 타일을 떠날 때" 감소합니다.
- 즉, `A -> B` 이동 시 `A`가 1 감소합니다.
- 손을 떼고 멈춘 마지막 타일은 즉시 감소하지 않고, 다음 이동 때 떠나면서 감소합니다.

### 2.4 게임오버, 하트, 되돌리기

- 현재 위치에서 이동 가능한 인접 활성 타일이 없거나, 남은 타일 중 다시는 닿을 수 없는 타일이 생기면 실패합니다.
- 실패하면 원인 타일을 약 0.4초 강조하고(나머지는 어둡게) 깜빡임 없이 암전한 뒤 빠르게 재시작합니다(약 0.7~0.8초, 0.2초 뒤부터 탭으로 건너뛰기).
- 하트는 스테이지마다 5개로 다시 채워지며, **실패할 때만** 1개씩 줄어듭니다. 수동 리셋과 되돌리기는 무료입니다.
- 하단 바의 **되돌리기** 버튼으로 직전 이동을 한 칸씩 되돌릴 수 있습니다.
- 하트를 모두 잃으면 보상형 광고로 즉시, 또는 3분을 기다려 무료로 5개를 회복합니다(대기 타이머는 앱을 꺼도 유지).  
  광고를 불러오지 못하면 5·15·45초 간격으로 자동 재시도하고, 오프라인이면 안내 문구를 보여줍니다.  
  한 세션에서 10분, 20분을 플레이하면 광고 없이 무료로 회복할 기회도 한 번씩 주어집니다.

### 2.5 보조 기능 (힌트 / 스킵)

| 기능 | 충전 | 충전이 없을 때 |
| --- | --- | --- |
| 힌트 | 최대 3회, 15분마다 1회 충전 | 보상형 광고 시청 |
| 스킵 | 최대 1회, 30분마다 1회 충전 | 보상형 광고 시청 |

- 힌트는 흰색 경로와 구간별 화살표로 다음 몇 수를 보여줍니다. 힌트대로 그리면 지나간 구간만 지워지고, 다른 길로 가면 사라집니다.
- 플레이어가 아직 검증된 정답 경로(`Resources/Stages/solutions.txt`) 위에 있으면 힌트가 즉시 나옵니다. 벗어난 경우에만 게임 내 솔버가 화면을 멈추지 않고 여러 프레임에 나눠 탐색합니다.
- 1~2스테이지에서는 잠시 멈춰 있으면 정답 경로 가이드가 자동으로 표시됩니다(충전 소모 없음).
- 한 스테이지에서 일정 시간(기본 40초) 막혀 있으면 무료 힌트 보너스 1회가 주어집니다.
- 스킵은 한 번 더 눌러야 실행되며(오조작 방지), 최종 스테이지(1000)에서는 스킵할 수 없습니다.
- 광고가 준비되지 않았으면 다음 무료 충전까지 남은 시간을 안내합니다.
- Development Build와 에디터에서는 광고 없이 바로 보상이 지급됩니다.

### 2.6 별점과 한붓 보너스

- 클리어하면 별을 보여주고 스테이지별 최고 기록을 저장합니다(`StageStarsStore`).
  - ★ 클리어, ★★ 힌트 없이, ★★★ 힌트·실패·리셋 없이
  - **한붓 보너스**: 손가락을 한 번도 떼지 않고(되돌리기 없이) 클리어

## 3. 스테이지 진행과 저장

- 스테이지는 `StageManager`가 `Resources/Stages/stage_{번호}.json`을 파일명 번호 기준으로 로드합니다.
- 현재 스테이지 진행도는 Easy Save 3(ES3)로, 설정·하트·충전·별점 등은 PlayerPrefs로 저장됩니다.
- 앱 재실행 시 저장된 스테이지부터 시작합니다.
- 최종 스테이지는 1,000(`FinalEndingProgressStore.FinalStageIndex`)이며, 클리어하면 엔딩 크레딧으로 넘어갑니다.
- 업적 진행도(클리어 수, 연속 클리어, 노힌트 클리어, 플레이 시간 등)는 `AchievementProgressStore`가 로컬에 먼저 기록합니다.

## 4. 스테이지 데이터

1,000개 스테이지는 `Tools/StagePipeline`으로 재구성했습니다(상세: `Tools/StagePipeline/CONTENT_REPORT.md`).

- 50개씩 20챕터. 챕터마다 쉬운 워밍업 3개 → 점점 어려워지는 구간(쉬어가는 판 포함) → 50번째 보스(챕터에서 가장 어려운 판).
- 챕터가 진행될수록 난이도가 오릅니다(챕터 중앙값 기준 순위 상관 1.0, 스테이지 단위 0.93).
- 보드 크기: 가로 2~7, 세로 1~8. 100번 이후 98%가 직사각형이 아닌 모양(구멍, 고리, 글자·아이콘 모양 등). 회전·대칭 중복 없음.
- 모든 특수 타일 배치는 실제로 풀이에 영향을 주도록 검증했습니다.
- 피날레(1000)는 번개 모양 7×8 보드로 8종 장치를 모두 사용합니다.
- `stageID`는 파일 번호와 같습니다. `Stages/index.json`은 1,000개 전체를 나열합니다(런타임 로딩에는 사용하지 않음).
- `Resources/Stages/solutions.txt`에 스테이지별 검증된 정답이 들어 있습니다(힌트 즉시 표시, 엔진 재생 검증에 사용).

새 장치 도입 시점(튜토리얼은 해당 스테이지, 예고 스낵바는 2스테이지 전):

| 장치 | 도입 스테이지 | 사용 스테이지 수 |
| --- | --- | --- |
| `ShortCircuit` | 21 | 448 |
| `CrossBlast` | 51 | 335 |
| `FixedKnot` | 101 | 428 |
| `TwinLink` | 151 | 394 |
| `Igniter` / `Hidden` | 201 | 343 |
| `BlindCurtain` | 251 | 218 |
| `Blackout` | 301 | 119 |
| `Charge` | 401 | 299 |

451 이후에는 대부분(96.7%)의 스테이지가 2~3종 장치를 조합하며, 후반 챕터는 장치 조합 테마로 구성됩니다.

## 5. 특수 타일 정리 (코드 기준)

아래는 `cells[].type` 기준 특수 타일 목록입니다.

### 5.1 특수 타일 한글 이름

게임 안에서 초등학생도 바로 규칙을 떠올릴 수 있도록 아래 이름을 기준으로 사용합니다.

| 코드 타입 | 한글 이름 |
| --- | --- |
| `ShortCircuit` | 한방향 타일 |
| `CrossBlast` | 십자폭발 타일 |
| `FixedKnot` | 순서 타일 |
| `TwinLink` | 짝꿍 타일 |
| `Igniter` | 켜기 타일 |
| `BlindCurtain` | 가림 타일 |
| `Blackout` | 깜깜 타일 |
| `Charge` | 충전 타일 |

### 5.2 특수 타일 동작

- `Normal`
  - 기본 타일입니다.
  - `type`을 생략하거나 `"Normal"`로 두면 동일하게 처리됩니다.

- `CrossBlast`
  - 밟고 떠날 때 상하좌우 인접 타일을 추가로 감소시킵니다.
  - 선택 필드: `properties.beamColor` (예: `"#00FFFF"`).

- `ShortCircuit`
  - 화살표 방향 셀로만 나갈 수 있는 강제 방향 타일입니다.
  - 권장 필드: `direction` (`"Up"`, `"Down"`, `"Left"`, `"Right"`).

- `FixedKnot`
  - 처음 진입은 `targetOrder`번째 이동에서만 가능한 타일입니다.
  - 필수 필드: `targetOrder` (1 이상 권장).
  - 해당 순서가 지나도록 한 번도 밟지 않으면 게임오버 처리됩니다.
  - 순서 조건을 만족한 뒤에는 일반 타일처럼 동작합니다.
  - `properties.isAbsolute`는 데이터에서 읽어 저장하지만 현재 규칙 판정에는 사용되지 않습니다.
  - 주의: 체커보드 특성상 시작점에서 타일까지의 거리와 `targetOrder`의 홀짝이 다르면 절대 진입할 수 없습니다.

- `TwinLink`
  - 같은 `linkID` 그룹끼리 count를 동기화하는 타일입니다.
  - 권장 필드: `linkID`.
  - 선택 필드: `color` (전기/숫자 발광색, 예: `"#00FBFF"`).

- `Hidden`
  - 시작 시 숨겨져 있고 밟을 수 없으며(자리는 희미한 테두리로 표시), Igniter를 밟는 즉시 들어갈 수 있게 됩니다.
  - 등장 연출만 0.08초 간격으로 순서대로 보입니다.
  - 선택 필드: `groupID` (없으면 `"default"`).

- `Igniter`
  - 밟는 순간 `targetID`와 같은 `groupID`의 Hidden 타일을 릴레이로 활성화합니다.
  - 시작 타일이 Igniter이면 시작과 동시에 발동합니다.
  - 필수 필드: `targetID`.
  - count는 JSON 값을 그대로 사용합니다.

- `BlindCurtain`
  - 규칙은 일반 타일과 같고, 자기 숫자만 `?`로 가려서 표시합니다.
  - 추가 필드 없음. count는 JSON 값을 그대로 사용합니다.

- `Blackout`
  - 자기 숫자를 `?`로 표시하고 글리치/플리커 연출이 있는 타일입니다.
  - 밟는 순간 보드 전체 타일 숫자가 `?`로 뒤집힙니다(규칙은 바뀌지 않음).
  - 추가 필드 없음.

- `Charge` (신규)
  - 떠날 때 자기 숫자는 1 줄고, 상하좌우 이웃 중 숫자가 남은 타일(들어가는 칸 제외)을 1씩 늘립니다.
  - FixedKnot·Igniter·Hidden은 영향을 받지 않고, 0인 타일은 되살리지 않으며, 효과는 연쇄되지 않습니다.
  - 타일 오른쪽 위에 초록색 `+` 표시가 붙습니다. Charge끼리는 붙여 배치하지 않습니다.

참고:

- `Spotlight`는 타일 타입이 아니라 `config.mode`로 켜는 스테이지 모드입니다.

## 6. 프로젝트 구조

### 6.1 핵심 스크립트

- `Assets/Scripts/GameManager.cs`  
  게임 메인 루프(입력, 경로, 규칙, 승리/패배, 힌트 솔버, 스테이지 전환, 리셋, 카메라 맞춤)

- `Assets/Scripts/GameMainUIController.cs`  
  UI Toolkit 상단/하단 UI, 설정, 튜토리얼, 하트·힌트·스킵, 광고, 엔딩 크레딧, 반응형 레이아웃

- `Assets/Scripts/Tile.cs` 및 특수 타일 스크립트  
  `CrossBlastTile`, `ShortCircuitTile`, `FixedKnotTile`, `TwinLinkTile`, `HiddenTile`, `IgniterTile`, `BlindCurtainTile`, `BlackoutTile`, `ChargeTile`(신규)  

- `Assets/Scripts/StageManager.cs`, `StageData.cs`  
  JSON 스테이지 로더와 직렬화 구조(`StageData`, `CellData`, `StageConfig`), 정답 경로(`solutions.txt`) 로더

- `Assets/Scripts/NeonIntroElement.cs`  
  앱 시작 네온 인트로 모션그래픽(영상 대신 UI Toolkit 벡터 드로잉)

- `Assets/Scripts/StageStarsStore.cs`  
  스테이지별 별점·한붓 클리어 최고 기록

- `Assets/Scripts/DevLog.cs`  
  에디터·개발 빌드에서만 남는 로그(릴리스 빌드에서는 호출 자체가 제거됨)

- `Assets/Scripts/GameplayFeedback.cs`  
  이동·감소·클리어 시 시각/햅틱 피드백(효과 강도 Full / Reduced / Minimal 설정 지원)

- `Assets/Scripts/GameLocalization.cs`  
  게임 내 텍스트 현지화(현재 한국어·영어만 노출. 나머지 10개 언어 테이블은 번역 완료 전까지 비노출)

- `Assets/Scripts/AchievementProgressStore.cs`, `FinalEndingProgressStore.cs`  
  업적 진행도와 1,000 스테이지 엔딩/여정 통계

- `Assets/Scripts/Firebase/FirebaseBootstrap.cs`  
  Firebase Analytics, Crashlytics, Remote Config 초기화와 이벤트 로깅

- `Assets/Scripts/DeviceOrientationPolicy.cs`  
  스플래시 이전에 세로 방향 고정

- `Assets/Scripts/Editor/`  
  프리팹 생성 도구, Android 방향 manifest 후처리, 피드백 미리보기/검증 도구,  
  `StageDataValidator`(스테이지 정적 검증, 빌드 직전 자동 실행), `Tests/`(엔진 재생·회귀·화면 캡처 테스트)

- `Tools/StagePipeline/`  
  스테이지 생성·검증 파이썬 도구(빌드에 포함되지 않음). 사용법은 해당 폴더 README 참고

### 6.2 주요 리소스 경로

- 스테이지 JSON: `Assets/Resources/Stages/`
- 튜토리얼/스낵바 일정: `Assets/Resources/Tutorials/`
- UI UXML/USS: `Assets/Resources/GameMainUI.uxml`, `GameMainUI.uss`
- 아이콘/스프라이트: `Assets/Resources/Sprites/` (되돌리기 아이콘 `undo.png` 추가)
- 사운드: `Assets/Resources/Sounds/` (게임 효과음 전체. 런타임에 Resources에서 불러와 기기 빌드에서도 소리가 납니다)
- 셰이더: `Assets/Shaders/SpotlightFog.shader`, `Assets/Resources/Shaders/GameplayFeedback.shader`
- iOS 햅틱 네이티브 플러그인: `Assets/Plugins/iOS/ZeroStepFeedback.mm`
- 씬: `Assets/Scenes/SampleScene.unity`

### 6.3 주요 외부 의존성

- Google Mobile Ads (배너, 전면, 보상형 광고)
- Firebase Analytics / Crashlytics / Remote Config 13.8.0
- Easy Save 3, DOTween
- Unity Localization, Addressables, URP, Visual Effect Graph

### 6.4 씬 구성(현재)

- `GameManager` 오브젝트(핵심 런타임)
- `Main Camera` (URP)
- `Global Volume`
- `UI` (`UIDocument` + `GameMainUIController`)

## 7. UI와 운영 기능 상태

### 7.1 버튼 연결 상태

| 위치 | 버튼 | 상태 |
| --- | --- | --- |
| 하단 | `Hint` | 연결됨 (충전 / 보상형 광고) |
| 하단 | `Undo` | 연결됨 (직전 이동 되돌리기, 무료) |
| 하단 | `Reset` | 연결됨 (무료) |
| 하단 | `Skip` | 연결됨 (두 번 눌러 실행, 충전 / 보상형 광고) |
| 하단 | `Block Ads` | **미구현** (로그만 출력) |
| 설정 | 사운드 / 진동 / 효과 강도 / 언어 / 도움말 | 연결됨 |
| 설정 | 문의 메일 / 데이터 초기화 | 연결됨 |
| 설정 | 개인정보처리방침 / 이용약관 | 연결됨, 단 **두 버튼 모두 임시 URL(`https://www.naver.com`)을 엶** |
| 설정 | 평가하기 / 광고 제거 | **미구현** (로그만 출력) |

### 7.2 광고

- 배너 광고가 하단에 노출되며, 레이아웃은 배너 높이를 반영해 보드 크기를 조정합니다.
  - 배너 높이·safe area는 화면 픽셀이므로 UI Toolkit 패널 단위로 변환해 적용하고, 배너와 하단 버튼 사이 최소 16dp 간격을 보장합니다.
  - 스플래시와 튜토리얼 화면 위에는 배너를 띄우지 않으며, 로드 실패 시 30초 간격으로 재시도합니다(실패 문구는 개발 빌드에서만 표시).
- 스테이지 전환 전면 광고는 Remote Config 기본값 기준 12스테이지부터, 최소 180초 간격·5스테이지 간격으로 노출됩니다.
- Development Build에서는 광고 초기화를 건너뜁니다.
- 광고 제거 권한은 로컬 저장값(`RemoveAdsEntitled`)만 확인하며, 실제 인앱 결제는 아직 연결되지 않았습니다.

### 7.3 Firebase

- `stage_start`, `stage_clear`, `stage_fail`, `stage_reset`, 버튼 클릭, 광고 보상 등 이벤트를 기록합니다.
- Remote Config로 전면 광고 시점과 유휴 힌트 보너스를 조절합니다.  
  일일 챌린지, 주간 스테이지, 무한 모드, 리더보드 플래그도 정의되어 있지만 기본값은 모두 꺼져 있습니다.
- Firebase 설정 파일(`google-services.json`, `GoogleService-Info.plist`)은 Git에서 제외되어 있습니다. 콘솔 설정 절차는 `docs/FirebaseLaunchSetup.md`를 참고하세요.

### 7.4 현지화

- 노출 언어: 한국어, 영어(시스템 언어가 한국어면 한국어, 그 외 영어)
- 일본어·중국어 등 10개 언어는 문구의 약 8%만 번역되어 있어 번역 완료 전까지 선택 목록에서 숨겼습니다.
- 설정의 언어 버튼으로 변경할 수 있습니다.

## 8. 실행 방법

1. Unity `6000.3.5f2`로 프로젝트를 엽니다.
2. `Assets/Scenes/SampleScene.unity`를 엽니다.
3. Play 실행.

입력 시스템:

- 프로젝트는 New Input System 기반(`activeInputHandler: 1`)으로 동작합니다.
- 에디터에서는 마우스, 디바이스에서는 터치 입력으로 플레이 가능합니다.
- 에디터에서는 광고 대신 보상이 즉시 지급되어 힌트/스킵/하트 흐름을 바로 확인할 수 있습니다.

### 8.1 검증

- 스테이지 정적 검증: Unity 메뉴 `ZeroStep/Validate Stage Data` (빌드 직전 자동 실행)
- 엔진 재생 검증: `StageReplayTests` — 실제 게임 코드로 1,000개 정답을 재생해 클리어·힌트를 확인
- 회귀 테스트: `GameplayRegressionTests` — 되돌리기 복원, 실패 후 복귀 시간, 리셋 하트 규칙
- 화면 캡처: `VisualCaptureTests` — 인트로 프레임과 보드 화면을 PNG로 저장(그래픽 켠 배치 모드)
- 파이썬 검증: `python3 -I Tools/StagePipeline/check_project.py`

> 엔진 테스트는 에디터의 로컬 저장 데이터를 바꾸므로 실행 전후로 저장 데이터를 백업·복원하세요.

## 9. 스테이지 JSON 작성 가이드 (특수 타일 포함)

### 9.1 파일 위치/이름 규칙

- 경로: `Assets/Resources/Stages/`
- 파일명: `stage_{번호}.json` (예: `stage_12.json`)
- 로딩 기준은 파일명 번호이며, JSON 내부 `stageID` 값은 로딩 키로 쓰지 않습니다.

### 9.2 기본 구조

```json
{
  "stageID": 12,
  "width": 5,
  "height": 5,
  "startPoint": { "x": 0, "y": 0 },
  "config": {
    "mode": "Normal",
    "difficulty": "Normal",
    "spotlightRadius": 2.5,
    "showGridLines": false
  },
  "cells": [
    { "x": 0, "y": 0, "type": "Normal", "count": 2 }
  ]
}
```

### 9.3 `cells` 필드 규칙

- 공통 필드
  - `x`, `y`: 그리드 좌표
  - `count`: 타일 숫자
  - `type`: 타일 타입 문자열

- 공통 주의점
  - `count <= 0`이면 해당 좌표는 생성되지 않습니다.
  - `type` 미기입/빈 문자열은 사실상 `Normal`로 동작합니다.
  - 타입 문자열은 코드 기준으로 `Normal`, `CrossBlast`, `ShortCircuit`, `FixedKnot`, `TwinLink`, `Hidden`, `Igniter`, `BlindCurtain`, `Blackout`, `Charge` 사용을 권장합니다.

- 타입별 추가 필드
  - `ShortCircuit`: `direction`
  - `FixedKnot`: `targetOrder`, `properties.isAbsolute`
  - `TwinLink`: `linkID`, `color`
  - `Hidden`: `groupID`
  - `Igniter`: `targetID`
  - `CrossBlast`: `properties.beamColor` (선택)

- 스테이지 루트 추가 필드

- 스테이지를 추가·수정하면 `Resources/Stages/solutions.txt`에 검증된 정답도 갱신해야 합니다(검증기가 확인).

### 9.4 예시 1: 기본 퍼즐

```json
{
  "stageID": 1,
  "width": 3,
  "height": 3,
  "startPoint": { "x": 0, "y": 0 },
  "config": {
    "mode": "Normal",
    "difficulty": "Normal",
    "spotlightRadius": 2.5,
    "showGridLines": false
  },
  "cells": [
    { "x": 0, "y": 0, "type": "Normal", "count": 2 },
    { "x": 1, "y": 0, "type": "Normal", "count": 2 },
    { "x": 2, "y": 0, "type": "Normal", "count": 1 },
    { "x": 0, "y": 1, "type": "Normal", "count": 1 },
    { "x": 1, "y": 1, "type": "Normal", "count": 3 },
    { "x": 2, "y": 1, "type": "Normal", "count": 2 },
    { "x": 0, "y": 2, "type": "Normal", "count": 1 },
    { "x": 1, "y": 2, "type": "Normal", "count": 2 },
    { "x": 2, "y": 2, "type": "Normal", "count": 1 }
  ]
}
```

### 9.5 예시 2: 특수 타일 혼합 스테이지

```json
{
  "stageID": 99,
  "width": 5,
  "height": 5,
  "startPoint": { "x": 0, "y": 0 },
  "config": {
    "mode": "Normal",
    "difficulty": "Normal",
    "spotlightRadius": 2.5,
    "showGridLines": false
  },
  "cells": [
    { "x": 0, "y": 0, "type": "Normal", "count": 2 },
    { "x": 1, "y": 0, "type": "ShortCircuit", "count": 2, "direction": "Right" },
    { "x": 2, "y": 0, "type": "CrossBlast", "count": 2, "properties": { "beamColor": "#00FFFF" } },
    { "x": 3, "y": 0, "type": "TwinLink", "count": 3, "linkID": 7, "color": "#00FBFF" },
    { "x": 4, "y": 0, "type": "TwinLink", "count": 3, "linkID": 7, "color": "#00FBFF" },

    { "x": 0, "y": 1, "type": "Igniter", "count": 1, "targetID": "A" },
    { "x": 2, "y": 1, "type": "Hidden", "count": 2, "groupID": "A" },
    { "x": 3, "y": 1, "type": "Hidden", "count": 1, "groupID": "A" },
    { "x": 4, "y": 1, "type": "BlindCurtain", "count": 1 },

    { "x": 1, "y": 2, "type": "FixedKnot", "count": 1, "targetOrder": 4, "properties": { "isAbsolute": true } },
    { "x": 2, "y": 2, "type": "Blackout", "count": 2 },
    { "x": 3, "y": 2, "type": "Normal", "count": 1 },

    { "x": 0, "y": 3, "type": "Normal", "count": 2 },
    { "x": 1, "y": 3, "type": "Normal", "count": 2 },
    { "x": 2, "y": 3, "type": "Normal", "count": 1 }
  ]
}
```

운영 팁:

- `TwinLink`는 같은 `linkID`를 최소 2개 이상 배치해야 체감이 좋습니다.
- `Igniter.targetID`와 `Hidden.groupID` 문자열이 정확히 일치해야 Hidden이 활성화됩니다.
- `FixedKnot.targetOrder`는 플레이 경로 스텝 수(1-based) 기준입니다.

## 10. 디자인 컨셉과 비주얼 구성

### 10.1 디자인 컨셉

이 프로젝트의 비주얼 컨셉은 다음 키워드로 요약됩니다.

- `Neon Puzzle`: 어두운 배경 위에 네온 발광 요소(타일, 트레일, 특수 타일 효과)를 올려 퍼즐 상태를 즉각적으로 읽히게 함
- `Minimal HUD`: 화면을 복잡하게 채우지 않고, 상/하단 UI를 반투명·아웃라인 중심으로 구성해 플레이 영역 집중 유지
- `Readable Feedback`: 이동/감소/실패/클리어를 색상, 스케일, 글로우, 흔들림 등 짧은 피드백으로 일관되게 전달

### 10.2 핵심 시각 요소

- 타이포그래피
  - 메인 UI 폰트는 `Orbitron-Medium` 기반으로 사용 (`Assets/Fonts/Orbitron-Medium.ttf`).
  - 스테이지/설정 패널 텍스트에 동일 계열 폰트를 유지해 SF/네온 톤을 통일.

- 컬러 시스템
  - 기본 배경은 블랙 중심.
  - 타일 숫자 상태 색상은 코드 기준으로 분기:
    - `4+`: 핑크 계열
    - `2~3`: 민트 계열
    - `1`: 스카이블루 계열
    - `0`: 어두운 회색(비활성)
  - 시작점/현재시작점은 별도 틴트 + 하트비트 펄스로 강조.

- 발광/트레일
  - 타일 색상은 HDR emission 배율을 사용해 네온 느낌을 강화.
  - 드래그 궤적은 멀티 컬러 네온 그라데이션(Cyan/Magenta/Purple/Electric Blue)과 Additive 머티리얼 기반.
  - 특수 타일을 밟을 때 트레일 색이 잠깐 해당 타일 컬러로 블렌딩되어 규칙 인지가 쉬움.

- UI 레이아웃/형태
  - 상단: 스테이지 정보 + 진행바 + 하트.
  - 하단: 원형 아웃라인 버튼(`Hint`, `Skip`, `Reset`, `Block Ads`) 중심 구조와 배너 광고 영역.
  - 팝업/다이얼로그는 반투명 다크 패널 + 밝은 외곽선으로 일관된 모달 언어 사용.

- 인터랙션 모션
  - 버튼은 `neon-press` 계열 상태 클래스로 짧은 스케일/색 반응.
  - 하트, 튜토리얼, 스낵바, 팝업은 모두 짧은 `opacity/scale` 전환으로 통일.
  - 타일은 감소 시 텐션 애니메이션(수축→복원), 시작점 계열은 반복 펄스 애니메이션 사용.

### 10.3 특수 타일별 디자인 아이덴티티

- `CrossBlast`: 밝은 플래시 + 확대 후 인접 타일 파급
- `ShortCircuit`: 방향 화살표 아이콘으로 이동 규칙을 즉시 시각화
- `FixedKnot`: 기어 아이콘/숫자와 색상(빨강↔초록)으로 타이밍 제약 표시
- `TwinLink`: 전기 테두리(번개)와 동기화 반응으로 연결 관계 강조
- `Igniter` + `Hidden`: 스위치 트리거와 릴레이 점등 연출로 퍼즐 흐름 표현
- `BlindCurtain`: 자기 숫자만 `?`로 가려 기억력 요소 추가
- `Blackout`: 밟으면 보드 전체가 `?`로 뒤집히는 연출로 긴장감 연출

### 10.4 공간/카메라 연출

- 그리드는 카메라 중앙에 배치하되, 상/하단 UI와 배너 공간, safe area를 고려해 orthographic size를 동적으로 맞춤.
- 세로형 폰, 4:3 태블릿, 낮은 높이 화면, 대형 화면별 레이아웃 프로필을 사용.
- `Spotlight` 모드에서는 Fog of War로 가시 영역을 제한해 동일 퍼즐도 다른 분위기와 난이도로 체험 가능.

## 11. 출시 전 남은 과제

자세한 진단과 우선순위는 `docs/PROJECT_REVIEW_AND_ROADMAP.md`, 개선 계획은 `docs/IMPROVEMENT_PLAN.md`를 참고하세요. 요약:

- 개인정보처리방침·이용약관 실제 URL 연결
- 평가하기, 광고 제거(인앱 결제) 구현 또는 버튼 숨김
- 광고/분석 동의 흐름(UMP, iOS ATT) 추가, 분석용 사용자 ID에서 기기 고유 ID 제거
- 앱 아이콘, 회사명(`DefaultCompany`), 서명 정보 등 스토어 제출 자산 정리
- 10개 언어 번역 완료 후 언어 목록 다시 열기
- 리텐션 시스템(레벨 선택, 일일 퍼즐, 알림, 업적 화면, 클라우드 저장) — 개선 계획 4단계(보류)
