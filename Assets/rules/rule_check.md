# 규칙 검증 리포트

## 검증 기준

- 기준 문서: [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md)
- 실제 구현 기준: [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs), [Tile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/Tile.cs), 각 특수 타일 컴포넌트
- 이 리포트는 `TILE_RULES.md`에 적힌 규칙이 "현재 Unity 런타임"과 일치하는지 검증한 결과다.
- `TILE_RULES.md`는 문서 서두에서 시뮬레이터/검증기/생성기를 기준으로 작성되었다고 밝히고 있으므로, 런타임과 차이가 나는 경우에는 런타임 코드를 우선 진실값으로 봤다. 기준 문구는 [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L3) 에 있다.

## 판정 요약

| 항목 | 판정 | 요약 |
| --- | --- | --- |
| 이동 기본 규칙 | 부분 일치 | 인접 이동, 출발 시 감소, 마지막 타일 보존은 일치하지만 실제 처리 순서는 문서와 다르다. |
| 승리 조건 | 불일치 | 런타임에는 `all_tiles_zero` 외에 `last_tile_rule`이 추가로 존재한다. |
| 패배 조건 | 불일치 | 데드락 외에 `FixedKnot` 관련 실패 경로가 있다. |
| 타일 생성/소멸/음수 | 불일치 | `count <= 0` 생성 제외는 일치하지만, `count == 0` 목적지 진입 모드 분기는 없고 `-1` 상태도 유지되지 않는다. |
| `Normal` | 일치 | 일반 감소만 적용된다. |
| `CrossBlast` | 부분 일치 | 기본 광역 감소 규칙은 맞지만 `-1` 실패 규칙은 런타임에 없다. |
| `ShortCircuit` | 일치 | 출발 방향 제한 규칙은 문서와 일치한다. |
| `FixedKnot` | 부분 일치 | 지정 스텝 진입 규칙은 맞지만, 놓쳤을 때 즉시 실패하는 경로가 문서에 없다. |
| `TwinLink` | 불일치 | 문서의 "짝 타일도 직접 1 감소"가 아니라, 런타임은 stepped 타일의 현재 count로 동기화한다. |
| `Igniter` | 불일치 | 일반 타일과 동일하지 않고 `count = 1` 강제, 숫자 숨김, 떠날 때 소멸, 시작 시 자동 활성화도 없다. |
| `Hidden` | 부분 일치 | 잠금/해제 개념은 맞지만, 별도 진입 판정이 아니라 콜라이더 비활성으로 막고, 필드 누락 시 기본값도 허용한다. |
| `BlindCurtain` | 불일치 | 문서의 "시각 표시용"보다 강하게 동작하며, 밟는 순간 모든 타일 숫자를 `?`로 바꾼다. |
| `Blackout` | 부분 일치 | 이동 규칙은 일반 타일과 같지만, 런타임의 `?`/노이즈/피드백 정보는 문서에 빠져 있다. |

## 일치 항목

- 상하좌우 인접 이동만 허용하고 대각선 이동은 허용하지 않는다. 인접 판정은 [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1378) 에서 처리한다. 문서 기준은 [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L19) 이다.
- 타일 감소는 목적지 진입 시가 아니라 출발 타일을 떠날 때 일어난다. 실제 감소는 [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1228) 에서 처리하고, 손을 뗀 마지막 타일을 바로 줄이지 않는 규칙은 [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1411) 에 명시돼 있다. 문서 기준은 [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L21) 이다.
- 스테이지 JSON에서 `count <= 0` 인 셀은 생성하지 않는다. 런타임 구현은 [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L2238) 바로 앞의 `if (cell.count <= 0) continue;` 흐름으로 동작한다. 문서 기준은 [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L33) 이다.
- `count == 0` 이 된 타일은 비활성화되어 더 이상 시작점이나 목적지가 될 수 없다. 비활성화 시 렌더러/콜라이더/숫자를 끄는 처리는 [Tile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/Tile.cs#L568) 에 있다. 문서 기준은 [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L34) 이다.
- 데드락 게임오버는 실제로 구현되어 있고, 손을 뗀 뒤 클리어를 먼저 검사한 다음 데드락을 검사한다. 관련 구현은 [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1013), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1526), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1566) 에 있다. 문서 기준은 [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L27) 과 [README.md](/Users/economy/Unity/ZeroStep/README.md#L39) 이다.
- `Normal` 타일은 별도 효과 없이 떠날 때 자신의 count만 줄어드는 기본 타일로 동작한다. 실제 기본 감소는 [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1228) 과 [Tile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/Tile.cs#L416) 에 있다. 문서 기준은 [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L71) 이다.
- `CrossBlast`는 출발 타일 기본 감소 후, 목적지를 제외한 상하좌우 인접 타일을 감소시키는 핵심 규칙이 런타임에 존재한다. 실제 효과는 [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L990), [CrossBlastTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/CrossBlastTile.cs#L49), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1114) 에 있다. 문서 기준은 [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L82) 이다.
- `ShortCircuit`는 출발 시 지정 방향으로만 나갈 수 있다. 실제 출구 판정은 [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L879) 와 [ShortCircuitTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/ShortCircuitTile.cs#L72) 에 있다. 문서 기준은 [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L91) 이다.
- `FixedKnot`는 지정 이동 순번에만 진입 가능하고, 맞는 순서로 진입한 뒤에는 다음에 떠날 때 감소한다. 실제 진입 판정은 [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L842), [FixedKnotTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/FixedKnotTile.cs#L159), 감소/해제는 [FixedKnotTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/FixedKnotTile.cs#L171) 와 [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1211) 에 있다. 문서 기준은 [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L101) 이다.
- `Hidden`은 초기에는 진입 불가이고, `Igniter`가 활성화되면 등장한 뒤 일반 타일처럼 이동할 수 있다. 실제 구현은 [HiddenTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/HiddenTile.cs#L46), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1261) 에 있다. 문서 기준은 [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L134) 이다.
- `Blackout`은 이동/감소 규칙 자체는 일반 타일과 동일하다. 런타임에서도 이동 제약을 추가하지 않고, 밟을 때 시각 피드백만 준다. 관련 구현은 [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L993) 와 [BlackoutTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/BlackoutTile.cs#L69) 이다. 문서 기준은 [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L152) 이다.

## 불일치 항목

### 1. 승리 조건이 문서보다 하나 더 있다

- 문서 내용: 모든 활성 타일의 count가 `0`이 되면 클리어라고 설명한다. [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L25)
- 실제 구현: 런타임에는 두 가지 승리 경로가 있다.
- 첫 번째는 문서와 같은 `all_tiles_zero` 규칙이다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1810)
- 두 번째는 남은 count 합이 `1`이고 현재 밟은 타일의 count도 `1`일 때, 그 현재 타일을 즉시 `0`으로 만들고 클리어하는 `last_tile_rule`이다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1332)
- 결론: 문서는 일반 클리어 조건만 설명하고 있고, 런타임의 마지막 타일 특례 승리 규칙이 빠져 있다.

### 2. 패배 조건이 데드락만이 아니다

- 문서 내용: 핵심 규칙 파트에서는 데드락과 `count < 0` 비정상 상태를 중심으로 설명하고 있다. [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L39)
- 실제 구현: 런타임은 데드락 외에도 `FixedKnot` 실패를 게임오버로 처리한다.
- `isAbsolute == true` 인 `FixedKnot`를 잘못된 순서로 밟으면 즉시 게임오버 시퀀스를 시작한다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L843)
- `FixedKnot`를 제때 밟지 못하고 스텝이 지나가도 게임오버 시퀀스를 시작한다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L899), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L934), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L974), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1363)
- 결론: 현재 런타임의 패배 조건은 데드락보다 넓다.

### 3. 이동 판정 순서가 문서의 9단계와 정확히 일치하지 않는다

- 문서 내용: `Hidden` 판정 -> `ShortCircuit` 판정 -> `FixedKnot` 판정 -> 필요 시 목적지 `count > 0` 확인 -> 출발 타일 감소 -> 특수 효과 -> `Igniter` 활성화 -> 전체 합 검사 순서로 적혀 있다. [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L48)
- 실제 구현: 분기마다 순서가 다르고, 특히 일반 이동과 `ShortCircuit` 분기에서는 `currentPath.Add(hit)`와 `TryTriggerIgniter(hit)`가 출발 타일 감소보다 먼저 일어난다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L893), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L896), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L911), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L968), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L971), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L986)
- `FixedKnot` 정순서 분기에서는 반대로 출발 타일 감소가 `currentPath.Add(hit)`보다 먼저 일어난다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L857)
- 결론: 문서의 "안전한 해석 순서"는 현재 런타임의 단일 공통 순서가 아니다.

### 4. 목적지 `count == 0` 진입 허용/strict 모드 분기는 런타임에 없다

- 문서 내용: 시뮬레이터 기본 모드에서는 `count == 0` 목적지 진입을 허용할 수 있고, `strict` 모드에서는 금지한다고 적혀 있다. [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L36)
- 실제 구현: 런타임은 `hit != null && hit.IsActive` 인 경우만 다음 타일 후보로 처리한다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L789)
- 그리고 `count == 0` 이 되면 콜라이더 자체를 꺼서 입력 대상으로도 잡히지 않게 만든다. [Tile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/Tile.cs#L568)
- 결론: 현재 Unity 런타임에는 문서가 말하는 `strict`/비-`strict` 목적지 해석 분기가 존재하지 않는다.

### 5. `count < 0` 실패 상태는 런타임에서 유지되지 않는다

- 문서 내용: `-1`은 비정상 상태이며 즉시 실패 처리해야 한다고 적혀 있다. [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L39)
- 실제 구현: `Tile.SetNumber()`가 모든 값을 `Mathf.Max(0, value)`로 clamp 한다. [Tile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/Tile.cs#L403)
- `Tile.OnStep()`도 현재 count가 `0` 이하면 그냥 리턴한다. [Tile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/Tile.cs#L416)
- `CrossBlast`는 인접 타일이 `IsActive` 인 경우에만 감소시키고, [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1126) `TwinLink`도 파트너의 값을 직접 `-1` 하지 않고 stepped 타일의 현재 count로 동기화한다. [TwinLinkTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/TwinLinkTile.cs#L252)
- 결론: 런타임에는 `-1` 상태를 중간 상태로 유지하거나 그것만으로 즉시 실패 처리하는 구조가 없다.

### 6. `TwinLink`는 "짝 타일도 1 감소"가 아니라 "값 동기화"다

- 문서 내용: 출발 타일을 떠날 때 짝 타일도 1 감소한다고 적혀 있다. [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L111)
- 실제 구현: `NotifyTwinLinkStepped()`가 `TwinLinkTile.OnSteppedSyncPartners()`를 호출하고, 이 메서드는 파트너 타일의 count를 stepped 타일의 현재 count로 그대로 맞춘다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1219), [TwinLinkTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/TwinLinkTile.cs#L252)
- 결론: 문서의 감소 모델과 런타임의 동기화 모델이 다르다.

### 7. `Igniter`는 일반 타일과 동일하게 취급되지 않는다

- 문서 내용: 이동/감소 규칙은 일반 타일과 같고, 진입 시 `Hidden`을 활성화한다고 적혀 있다. 시작 타일이 `Igniter` 인 경우 그룹을 시작부터 켜는 구현이 안전하다는 보충 규칙도 있다. [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L123)
- 실제 구현: 런타임은 `Igniter`의 initial/runtime count를 강제로 `1`로 설정한다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L2300), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L2306)
- `Igniter`는 숫자를 숨기고 스위치 스프라이트를 사용한다. [IgniterTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/IgniterTile.cs#L39), [IgniterTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/IgniterTile.cs#L111)
- 떠날 때는 일반 감소 대신 소멸 연출 후 `0`으로 만든다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1227), [IgniterTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/IgniterTile.cs#L75)
- 시작 타일이 `Igniter`일 때 자동으로 그룹을 켜는 초기화 코드는 없다. `TryTriggerIgniter()`는 이동 중 진입한 타일에 대해서만 호출된다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L871), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L896), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L931), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L971), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1261)
- 결론: `Igniter`는 런타임에서 "일반 타일 + 트리거"보다 더 특수하게 구현돼 있고, 시작 시 자동 활성화 규칙도 없다.

### 8. `BlindCurtain`은 시각 전용 타일이 아니다

- 문서 내용: 현재는 시각 표시용이며, 게임 규칙 측면에서는 일반 타일처럼 취급해도 된다고 적혀 있다. [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L143)
- 실제 구현: `BlindCurtain`은 생성 시 initial count를 강제로 `1`로 맞춘다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L2256), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L2306)
- 그리고 밟는 순간 모든 타일 숫자 표시를 `?`로 바꾸는 전역 효과를 발생시킨다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1142), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1155)
- 타일 자체도 숫자 대신 아이콘을 띄운다. [BlindCurtainTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/BlindCurtainTile.cs#L23)
- 결론: 런타임 기준 `BlindCurtain`은 단순 시각 장식이 아니라 정보 은닉 효과를 가진 특수 타일이다.

### 9. 필수 필드 누락을 런타임은 "데이터 오류"로 막지 않는다

- 문서 내용: 특수 타일 필수 필드가 없으면 스테이지 데이터 오류로 취급하는 것이 맞다고 적혀 있다. [TILE_RULES.md](/Users/economy/Unity/ZeroStep/Assets/rules/TILE_RULES.md#L64)
- 실제 구현: 런타임은 여러 필드에 기본값을 넣고 계속 진행한다.
- `ShortCircuit.direction` 이 없으면 `Down`으로 처리한다. [ShortCircuitTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/ShortCircuitTile.cs#L39), [ShortCircuitTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/ShortCircuitTile.cs#L60)
- `FixedKnot.targetOrder <= 0` 이면 `1`로 처리한다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L2266)
- `TwinLink.linkID == 0` 이면 `101`로 처리한다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L2272)
- `Hidden.groupID` 가 비어 있으면 `"default"`로 처리한다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L2291)
- `Igniter.targetID` 가 비어 있으면 빈 문자열로 처리하고, 해당 경우 활성화 동작은 그냥 아무 일도 하지 않는다. [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L2300), [IgniterTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/IgniterTile.cs#L39), [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L1268)
- 결론: 문서는 "오류 처리"를 말하고 있지만, 런타임은 대부분 "기본값 보정"으로 흘려보낸다.

## 보완 필요 항목

- `Hidden` 문서는 "진입 가능 여부 확인"이라는 규칙 설명을 두고 있지만, 실제 구현은 별도 규칙 함수보다 콜라이더/렌더러 비활성 상태에 더 의존한다. 따라서 문서도 "입력 단계에서 막힘"이라고 명시하는 편이 런타임과 더 가깝다. 관련 구현은 [HiddenTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/HiddenTile.cs#L46) 와 [Tile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/Tile.cs#L568) 이다.
- `Blackout`은 문서상 "시각 표시용"으로 요약되어 있지만, 실제로는 숫자를 항상 `?`로 바꾸고 노이즈/플리커/펀치 스케일 피드백을 가진다. 이동 규칙은 일치하지만 표현 규칙은 문서보다 구체적이다. 관련 구현은 [BlackoutTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/BlackoutTile.cs#L54) 와 [Tile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/Tile.cs#L444) 이다.
- `ShortCircuit`는 문서 설명대로 출발 방향 제약은 맞지만, 코드 내부에는 `EntryCell`/`IsValidEntryFrom()`가 정의돼 있음에도 현재 이동 판정에서는 사용하지 않는다. 즉 런타임은 "진입은 어느 방향에서든 허용, 출발만 제한"으로 굳어져 있다. 관련 구현은 [ShortCircuitTile.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/ShortCircuitTile.cs#L78) 와 [GameManager.cs](/Users/economy/Unity/ZeroStep/Assets/Scripts/GameManager.cs#L923) 이다.

## 최종 결론

- 현재 `TILE_RULES.md`는 "시뮬레이터/생성기/검증기 관점 문서"로서는 일관성이 있지만, Unity 런타임과는 중요한 차이가 있다.
- 가장 큰 차이는 승리 조건 2종, `FixedKnot` 기반 패배 조건, `TwinLink` 동기화 방식, `Igniter`/`BlindCurtain`의 실제 특수 동작, 그리고 `-1` 상태 부재다.
- 따라서 이 문서를 "현재 게임 런타임 규칙 문서"로 그대로 사용하면 오해가 생길 가능성이 높다.
