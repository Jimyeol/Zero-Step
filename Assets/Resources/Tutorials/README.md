# Tutorials JSON 운영 가이드

이 문서는 아래 2개 JSON 파일을 어떻게 정리하고 사용하는지 설명합니다.

- `Assets/Resources/Tutorials/help_tutorial_schedule.json`
- `Assets/Resources/Tutorials/stage_snackbar_schedule.json`

## 1) 파일 구조와 역할

`help_tutorial_schedule.json`
- 스테이지 진입 시 자동으로 뜨는 도움말 팝업 스케줄
- 설정창의 도움말 버튼에서도 사용

`stage_snackbar_schedule.json`
- 스테이지 진입 시 하단에 짧게 뜨는 스낵바 스케줄
- "몇 스테이지 남음" 같은 안내 문구용

## 2) 코드에서 로드되는 위치

`GameMainUIController`에서 `Resources.Load<TextAsset>()`로 로드합니다.

- 도움말: `Tutorials/help_tutorial_schedule`
- 스낵바: `Tutorials/stage_snackbar_schedule`

스테이지 시작 시점(`SetupStage`)에 아래가 자동 실행됩니다.

- `TryShowScheduledTutorialForStage(currentStageIndexForUI)`
- `TryShowScheduledSnackbarForStage(currentStageIndexForUI)`

## 3) help_tutorial_schedule.json 스키마

기본 형태:

```json
{
  "entries": [
    {
      "id": "basic_stage_1",
      "stageIndex": 1,
      "tutorialType": "BasicPath",
      "titleKey": "tutorial_basic_title",
      "descriptionKey": "tutorial_basic_description",
      "closeButtonTextKey": "help_close_button",
      "title": "기본 플레이 방법",
      "description": "설명",
      "closeButtonText": "확인"
    }
  ]
}
```

필드 설명:

- `id`: 튜토리얼 고유 ID
- `stageIndex`: 자동 노출할 스테이지 번호
- `tutorialType`: 현재는 `BasicPath` 사용
- `titleKey`: (선택) 다국어 키. 있으면 `title`보다 우선
- `title`: 팝업 제목
- `descriptionKey`: (선택) 다국어 키. 있으면 `description`보다 우선
- `description`: 팝업 본문
- `closeButtonTextKey`: (선택) 다국어 키. 있으면 `closeButtonText`보다 우선
- `closeButtonText`: 닫기 버튼 문구

동작 규칙:

- 같은 `id` 튜토리얼을 닫으면 `TutorialDismissed_{id}` 키로 저장되어 자동 노출은 다시 안 뜹니다.
- 설정창 도움말 버튼으로 열 때는 저장 여부를 무시하고 강제로 열 수 있습니다.

## 4) stage_snackbar_schedule.json 스키마

기본 형태:

```json
{
  "entries": [
    {
      "id": "tile_unlock_preview_3_to_5",
      "stageIndex": 3,
      "targetStageIndex": 5,
      "messageKey": "snackbar_default_new_tile_unlock",
      "message": "새로운 타입의 타일이 열립니다! {remainingStages}스테이지 남았습니다.",
      "duration": 2.8
    }
  ]
}
```

필드 설명:

- `id`: 스낵바 고유 ID
- `stageIndex`: 스낵바를 띄울 스테이지 번호
- `targetStageIndex`: 목표 스테이지(남은 스테이지 계산 기준)
- `messageKey`: (선택) 다국어 키. 있으면 `message`보다 우선
- `message`: 표시 문구
- `duration`: 노출 시간(초)

메시지 치환 토큰:

- `{currentStage}`: 현재 스테이지
- `{targetStage}`: 목표 스테이지
- `{remainingStages}`: `targetStageIndex - currentStage` (최소 0)

동작 규칙:

- 같은 `id` 스낵바는 앱 실행 1회(세션) 동안 1번만 표시됩니다.
- 설정/튜토리얼/하트 리필 팝업이 열려 있으면 해당 시점 스낵바는 표시하지 않습니다.
- `duration <= 0`이면 기본값(코드 기본 2.8초)을 사용합니다.

## 5) 다국어 우선순위

- 도움말: `titleKey/descriptionKey/closeButtonTextKey` -> 일반 텍스트(`title/description/closeButtonText`) 순으로 사용
- 스낵바: `messageKey` -> `message` 순으로 사용
- 다국어 키는 `Assets/Scripts/GameLocalization.cs`에 정의되어 있습니다.

## 6) 작성 규칙(권장)

- `id`는 반드시 고유하게 유지
- `stageIndex`는 1 이상의 실제 존재 스테이지 번호 사용
- "N스테이지 뒤에 열림" 안내는 `targetStageIndex`를 같이 지정
- 문구는 가능한 한 짧게(1~2문장) 작성

권장 ID 패턴:

- 도움말: `tutorial_stage_{stage}_{topic}`
- 스낵바: `snackbar_stage_{stage}_{topic}`

## 7) 실전 예시

```json
{
  "entries": [
    {
      "id": "snackbar_stage_7_igniter_unlock",
      "stageIndex": 7,
      "targetStageIndex": 10,
      "messageKey": "snackbar_default_new_tile_unlock",
      "message": "점화 타일이 곧 열립니다! {remainingStages}스테이지 남았습니다.",
      "duration": 3.0
    },
    {
      "id": "snackbar_stage_12_twin_link_hint",
      "stageIndex": 12,
      "targetStageIndex": 12,
      "message": "쌍둥이 링크 타일 등장! 지금 바로 확인하세요.",
      "duration": 2.4
    }
  ]
}
```

## 8) 수정 후 체크리스트

- JSON 문법 에러가 없는지 확인(쉼표/괄호)
- `entries` 배열 안에 오브젝트가 들어있는지 확인
- 스테이지 번호가 실제 게임 진행 스테이지와 맞는지 확인
- 같은 `id`를 중복 사용하지 않았는지 확인

표시가 안 될 때 점검:

- 파일 경로가 `Assets/Resources/Tutorials/`가 맞는지
- 파일명이 코드 상수와 정확히 같은지
- 스낵바는 같은 세션에서 이미 같은 `id`를 보여준 상태인지
- 스테이지 진입 타이밍에 다른 팝업이 열려 있지 않았는지
