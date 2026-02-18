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

## 5. 특수 타일/모드 구현 현황

코드에는 아래 기능이 구현되어 있습니다.

- `CrossBlastTile`: 십자 인접 타일 감소
- `ShortCircuitTile`: 화살표 방향 강제 이동
- `FixedKnotTile`: 특정 순서 스텝 진입 제약
- `TwinLinkTile`: 같은 ID 타일 동기화
- `IgniterTile` + `HiddenTile`: 트리거/릴레이 활성화
- `BlindCurtainTile`: 숫자 물음표 처리
- `BlackoutTile`: 글리치/물음표 연출
- `SpotlightController`: 스포트라이트(Fog of War) 모드

단, 현재 스테이지 JSON에서는 이 특수 타입을 거의 쓰지 않으므로 기본 퍼즐 플레이가 중심입니다.

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

## 9. 스테이지 JSON 포맷 예시

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
    { "x": 1, "y": 0, "type": "CrossBlast", "count": 1 },
    { "x": 2, "y": 0, "type": "FixedKnot", "count": 1, "targetOrder": 3 }
  ]
}
```

`cells`에서 `count <= 0`이면 해당 좌표 타일은 생성하지 않습니다.

## 10. 참고

이 README는 현재 저장소의 실제 코드/데이터 상태에 맞춰 업데이트된 문서입니다.  
기존 소개 문구(예: 500 스테이지, 특정 이동 제약 등)와 실제 동작이 다른 부분은 현재 구현 기준으로 정리했습니다.
