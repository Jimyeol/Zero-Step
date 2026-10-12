# Stage Pipeline (스테이지 생성·검증 도구)

1,000개 스테이지를 만들고 검증하는 데 쓴 파이썬 도구입니다. Unity 프로젝트 밖(`Tools/`)에 있어 빌드에는 포함되지 않습니다.
규칙의 기준은 `Assets/rules/TILE_RULES.md`이며, 이 도구의 솔버(`zs.py`)는 실제 게임(`GameManager.cs`)과 같은 규칙을 구현합니다.

## 빠른 사용법

```bash
# 프로젝트에 들어 있는 스테이지 데이터 검증(전체: 풀이 가능·난이도·메커닉 의미 여부)
python3 -I Tools/StagePipeline/check_project.py --jobs 8

# 빠른 검증(구조 + 정답 재생만)
python3 -I Tools/StagePipeline/check_project.py --quick
```

`check_project.py`는 `Assets/Resources/Stages`, `solutions.txt`, `Tutorials`로 임시 폴더를 만든 뒤 `validate.py`를 실행합니다.

## 파일

| 파일 | 내용 |
| --- | --- |
| `zs.py` | 규칙 엔진 + 참조 솔버(파이썬). Charge 포함(벽 기능은 제거되어 스테이지에 쓰지 않음) |
| `zsolve.c`, `zs_c.py` | 같은 알고리즘의 C 구현과 파이썬 래퍼(약 100배 빠름). `cc -O2 -o zsolve zsolve.c`로 빌드 |
| `validate.py` | 출력 폴더 전체 검증(31개 항목). `python3 -I validate.py <폴더> [--jobs N] [--quick] [--no-ablation]` |
| `mapping.csv` | 현재 1,000개 스테이지의 지표(챕터, 출처, 무작위 클리어 확률, 결정적 수, 해답 수, 상태 수 등) |
| `CONTENT_REPORT.md` | 이번 재구성 보고서(챕터 구성, 도입 일정, 제외한 스테이지, 검증 결과) |
| `generator/` | 생성·배치 스크립트(참고용). 후보 풀(약 40MB)은 포함하지 않아 재실행하려면 `genpool.py`로 다시 만들어야 합니다 |

## 함께 쓰는 Unity 쪽 검증

- 정적 검증: Unity 메뉴 `ZeroStep/Validate Stage Data` — 빌드 직전에 자동 실행되며 오류가 있으면 빌드가 멈춥니다.
- 엔진 재생 검증: `Assets/Scripts/Editor/Tests/StageReplayTests.cs` — 실제 게임 코드로 1,000개 정답을 재생합니다.

```bash
ZEROSTEP_SOLUTIONS=Assets/Resources/Stages/solutions.txt ZEROSTEP_STAGES=1-1000 \
  /Applications/Unity/Hub/Editor/6000.3.5f2/Unity.app/Contents/MacOS/Unity -batchmode -nographics \
  -projectPath . -runTests -testPlatform EditMode -testFilter StageReplayTests -testResults replay.xml
```

> 엔진 테스트는 에디터의 로컬 저장 데이터(진행도·설정)를 바꿉니다. 실행 전후로 저장 데이터를 백업·복원하세요.

## 데이터 형식 요약

- `"type": "Charge"` — 떠날 때 이웃 +1(들어가는 칸, FixedKnot·Igniter·Hidden 제외). Charge끼리는 붙이지 않습니다.
- `solutions.txt` 한 줄: `N|x,y;x,y;...` (`|`로 나뉜 구간 = 손을 떼지 않는 한 번의 드래그)
