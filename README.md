# ZeroStep

Unity 기반 모바일 퍼즐 게임 프로젝트입니다.  
플레이어는 그리드 타일 위를 드래그로 이동하며 타일 숫자를 0으로 만들어 스테이지를 클리어합니다.

## 1. 게임 개요

- 장르: 터치/드래그 기반 숫자 퍼즐
- 타겟: 모바일(현재 Android 설정 중심)
- 엔진: Unity 6 (`6000.3.5f2`)
- 현재 씬: `Assets/Scenes/SampleScene.unity`
- 현재 스테이지 데이터: `Assets/Resources/Stages/stage_1.json` ~ `stage_35.json` (총 35개)

## 2. 현재 구현 기준 플레이 방식

이 섹션은 "현재 코드 동작" 기준입니다.

### 2.1 기본 조작

1. 시작 타일(스테이지 JSON의 `startPoint`)에서 터치를 시작합니다.
2. 손가락(또는 마우스) 드래그로 인접 타일로 이동합니다.
3. 손을 떼면 현재 위치가 다음 드래그 시작점이 됩니다.
4. 모든 활성 타일의 숫자가 0이 되면 클리어 후 다음 스테이지로 이동합니다.

### 2.2 이동 규칙

- 이동은 상하좌우 인접 타일 기준입니다(대각선 불가).
- `count > 0` 인 활성 타일만 이동 대상으로 인정됩니다.
- 시작은 반드시 현재 시작 타일에서만 가능합니다.

### 2.3 숫자 감소 규칙(핵심)

- 숫자는 "들어갈 때"가 아니라 "해당 타일을 떠날 때" 감소합니다.
- 즉, `A -> B` 이동 시 `A`가 1 감소합니다.
- 손을 떼고 멈춘 마지막 타일은 즉시 감소하지 않고, 다음 이동 때 떠나면서 감소합니다.

### 2.4 게임오버

- 현재 위치에서 이동 가능한 인접 활성 타일이 없으면 데드락(Game Over) 처리됩니다.
- 게임오버 시 글리치/암전 연출 후 현재 스테이지 초기 상태로 리셋됩니다.

## 3. 스테이지 진행과 저장

- 스테이지는 `StageManager`가 `Resources/Stages/stage_{번호}.json`을 로드합니다.
- 진행도는 Easy Save 3(ES3)로 저장/복원됩니다.
- 앱 재실행 시 저장된 스테이지부터 시작합니다.
- 마지막 스테이지 다음 번호 파일이 없으면 1스테이지로 순환합니다.

## 4. 현재 스테이지 데이터 상태

현재 JSON 데이터(`stage_1`~`stage_35`) 기준:

- `config.mode`는 모두 `Normal`
- 셀 `type`은 사실상 모두 `Normal`
- 즉, 코드에 구현된 특수 타일 시스템은 현재 스테이지 데이터에서는 거의 사용되지 않습니다.

참고:

- 일부 파일은 파일명 번호와 JSON 내부 `stageID` 값이 다릅니다.
- 실제 로딩은 파일명(`stage_{번호}.json`) 기준이라 플레이에는 큰 문제 없이 동작합니다.

## 5. 특수 타일 정리 (코드 기준)

아래는 `cells[].type` 기준 특수 타일 목록입니다.

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
  - 특정 스텝 순서에서만 진입 가능한 타일입니다.
  - 필수 필드: `targetOrder` (1 이상 권장).
  - 선택 필드: `properties.isAbsolute` (`true`면 순서 위반 즉시 게임오버 처리).

- `TwinLink`
  - 같은 `linkID` 그룹끼리 count를 동기화하는 타일입니다.
  - 권장 필드: `linkID`.
  - 선택 필드: `color` (전기/숫자 발광색, 예: `"#00FBFF"`).

- `Hidden`
  - 시작 시 숨겨져 있고 밟을 수 없으며, Igniter 트리거 시 나타납니다.
  - 선택 필드: `groupID` (없으면 `"default"`).

- `Igniter`
  - 밟는 순간 `targetID`와 같은 `groupID`의 Hidden 타일을 릴레이로 활성화합니다.
  - 필수 필드: `targetID`.
  - 주의: 코드에서 count는 1로 고정 처리됩니다.

- `BlindCurtain`
  - 밟는 순간 전체 타일 숫자가 `?` 표시로 전환됩니다.
  - 추가 필드 없음.
  - 주의: 코드에서 count는 1로 고정 처리됩니다.

- `Blackout`
  - 숫자 대신 `?`를 표시하고 글리치/플리커 연출이 있는 타일입니다.
  - 추가 필드 없음.

참고:

- `Spotlight`는 타일 타입이 아니라 `config.mode`로 켜는 스테이지 모드입니다.

## 6. 프로젝트 구조

### 6.1 핵심 스크립트

- `Assets/Scripts/GameManager.cs`  
  게임 메인 루프(입력, 경로, 승리/패배, 스테이지 전환, 리셋)

- `Assets/Scripts/Tile.cs`  
  타일 숫자/색상/활성 상태/이펙트 처리

- `Assets/Scripts/StageManager.cs`  
  JSON 스테이지 로더

- `Assets/Scripts/StageData.cs`  
  JSON 직렬화 데이터 구조(`StageData`, `CellData`, `StageConfig`)

- `Assets/Scripts/GameMainUIController.cs`  
  UI Toolkit 상단/하단 UI 제어, 진행도 갱신

### 6.2 주요 리소스 경로

- 스테이지 JSON: `Assets/Resources/Stages/`
- UI UXML: `Assets/Resources/GameMainUI.uxml`
- 아이콘/스프라이트: `Assets/Resources/Sprites/`
- 셰이더: `Assets/Shaders/SpotlightFog.shader`
- 씬: `Assets/Scenes/SampleScene.unity`

### 6.3 씬 구성(현재)

- `GameManager` 오브젝트(핵심 런타임)
- `Main Camera` (URP)
- `Global Volume`
- `UI` (`UIDocument` + `GameMainUIController`)
- `Background_System` (VFX)

## 7. UI 동작 상태

현재 버튼 연결 상태:

- `Reset`: 현재 스테이지 리셋 동작 연결됨
- `Skip`: 현재 로그 출력만 구현
- `Setting`: 현재 로그 출력만 구현
- `Block Ads`: 현재 로그 출력만 구현

## 8. 실행 방법

1. Unity `6000.3.5f2`로 프로젝트를 엽니다.
2. `Assets/Scenes/SampleScene.unity`를 엽니다.
3. Play 실행.

입력 시스템:

- 프로젝트는 New Input System 기반(`activeInputHandler: 1`)으로 동작합니다.
- 에디터에서는 마우스, 디바이스에서는 터치 입력으로 플레이 가능합니다.

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
  - 타입 문자열은 코드 기준으로 `Normal`, `CrossBlast`, `ShortCircuit`, `FixedKnot`, `TwinLink`, `Hidden`, `Igniter`, `BlindCurtain`, `Blackout` 사용을 권장합니다.

- 타입별 추가 필드
  - `ShortCircuit`: `direction`
  - `FixedKnot`: `targetOrder`, `properties.isAbsolute`
  - `TwinLink`: `linkID`, `color`
  - `Hidden`: `groupID`
  - `Igniter`: `targetID`
  - `CrossBlast`: `properties.beamColor` (선택)

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
  - 하단: 원형 아웃라인 버튼(`Skip`, `Reset`, `Block Ads`) 중심 구조.
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
- `BlindCurtain`/`Blackout`: `?` 기반 시각 정보 제한으로 긴장감 연출

### 10.4 공간/카메라 연출

- 그리드는 카메라 중앙에 배치하되, 상/하단 UI 공간을 고려해 orthographic size를 동적으로 맞춤.
- `Spotlight` 모드에서는 Fog of War로 가시 영역을 제한해 동일 퍼즐도 다른 분위기와 난이도로 체험 가능.

## 11. 참고

이 README는 현재 저장소의 실제 코드/데이터 상태에 맞춰 업데이트된 문서입니다.  
기존 소개 문구(예: 500 스테이지, 특정 이동 제약 등)와 실제 동작이 다른 부분은 현재 구현 기준으로 정리했습니다.
