# ZeroStep 프로젝트 진단 및 개선 로드맵

> 작성일: 2026-07-14  
> 대상: Unity 6 모바일 퍼즐 게임 `ZeroStep`  
> 분석 범위: 런타임 코드, UI Toolkit, 1,000개 스테이지 JSON, Android/iOS 설정, 광고, Firebase, 현지화, 저장, 테스트 및 저장소 운영 상태

## 1. 결론

현재 프로젝트는 게임 규칙, 1,000개 스테이지, 특수 타일, 힌트 솔버, 광고, Firebase 분석, 엔딩 및 업적까지 실제 제품에 필요한 기능이 상당 부분 구현되어 있다. 프로토타입 수준은 이미 넘었고, 콘텐츠 양과 기능 밀도도 충분하다.

다만 지금 가장 큰 위험은 “기능이 부족하다”기보다 “출시 가능한 상태임을 반복해서 증명할 장치가 부족하다”는 점이다. 특히 iOS 빌드, 스토어 정책 링크, 광고 동의 및 테스트 경로, 앱 아이콘과 회사 정보, 자동화 테스트가 아직 출시 기준을 충족했다고 보기 어렵다.

### 우선순위 종합

| 순위 | 핵심 문제 | 우선순위 | 확신 | 근거 구분 |
| --- | --- | --- | --- | --- |
| 1 | iOS 빌드 및 스토어 제출 준비가 완료되지 않음 | P0 | 높음 | 코드와 프로젝트 설정 |
| 2 | 개인정보·광고 동의·외부 정책 링크가 출시 상태가 아님 | P0 | 높음 | 코드와 광고 설정 |
| 3 | “광고 제거”, “평가하기” 등 노출된 버튼이 실제 기능과 연결되지 않음 | P0 | 높음 | 클릭 핸들러 |
| 4 | 테스트 광고를 안전하게 검증할 수 있는 빌드 경로가 사실상 막혀 있음 | P0 | 높음 | 광고 초기화 분기 |
| 5 | 테스트와 CI가 없어 1,000개 스테이지 및 플랫폼 회귀를 자동 방지하지 못함 | P1 | 높음 | 테스트 파일·CI 부재 |
| 6 | `GameManager`와 `GameMainUIController`에 책임이 과도하게 집중됨 | P1 | 높음 | 파일 크기와 메서드 수 |
| 7 | 스테이지 데이터의 실행 가능성과 메타데이터 일관성이 분리되어 있음 | P1 | 높음 | JSON 전체 검사 |
| 8 | 반응형 UI가 수동 수치와 런타임 측정에 의존하며 자동 시각 검증이 없음 | P1 | 높음 | 레이아웃 코드와 테스트 문서 |
| 9 | Unity Localization과 자체 현지화 시스템이 중복 구성됨 | P1 | 높음 | 패키지·설정·코드 |
| 10 | 성능·배터리·저장 마이그레이션 기준이 아직 제품 운영 수준으로 고정되지 않음 | P2 | 중간 | 프레임률·생성/파괴·저장 코드 |

## 2. 판정 기준

이 문서는 아래 세 가지를 구분한다.

- **Evidence(직접 근거)**: 저장소의 코드, 설정, 데이터 또는 실행 결과에서 직접 확인된 사실
- **Inference(추론)**: 직접 근거가 실제 출시나 운영에서 어떤 위험으로 이어질지에 대한 판단
- **Unknown(미확인)**: App Store Connect, Google Play Console, 실제 서명 인증서처럼 저장소만으로 확인할 수 없는 항목

우선순위 정의:

- **P0**: 출시 전에 반드시 해결하거나 기능을 숨겨야 하는 항목
- **P1**: 출시 전 강하게 권장하며, 미해결 시 회귀·장애 가능성이 큰 항목
- **P2**: 출시 직후 안정성과 개발 속도를 높이는 항목
- **P3**: 운영 기반이 안정된 뒤 추가할 제품 기능

## 3. 현재 프로젝트 스냅샷

| 항목 | 확인 결과 |
| --- | --- |
| Unity 버전 | `6000.3.5f2` |
| 빌드 씬 | `Assets/Scenes/SampleScene.unity` 1개 |
| 런타임 C# 파일 | 30개 |
| `GameManager.cs` | 6,095줄, 메서드 약 292개, 직렬화 필드 91개 |
| `GameMainUIController.cs` | 7,521줄, 메서드 약 352개, 직렬화 필드 25개 |
| UI 스타일 | USS 2,439줄, UXML 358줄, UXML 인라인 스타일 82개 |
| 스테이지 | `stage_1.json`부터 `stage_1000.json`까지 1,000개 |
| 스테이지 JSON 파싱 | 1,000개 모두 성공 |
| 기본 구조 검사 | 좌표 범위, 중복 좌표, 시작점, 특수타일 필수값 모두 통과 |
| 테스트 코드 | 프로젝트 전용 EditMode/PlayMode 테스트 및 테스트 asmdef 미발견 |
| CI | GitHub Actions 등 CI 설정 미발견 |
| Firebase 설정 파일 | 로컬에는 Android/iOS 파일 모두 존재, Git에서는 의도적으로 제외 |
| 현재 작업 트리 | 감사 시점에 수정·미추적 항목 13개 존재 |
| 정적 C# 컴파일 | 오류 없이 통과, 미사용 필드 등 경고 존재 |
| Unity 배치 검증 | 로컬 Unity 라이선스가 없어 실행 불가 |

## 4. 잘 되어 있는 점

### 4.1 콘텐츠와 규칙 구현

- 1,000개 스테이지가 연속 파일로 존재한다.
- `Normal` 외에 `TwinLink`, `Hidden`, `ShortCircuit`, `Igniter`, `CrossBlast`, `BlindCurtain`, `FixedKnot`, `Blackout` 타일이 실제 데이터에 사용된다.
- 스테이지 JSON 1,000개가 모두 파싱되며 기본 구조 검사도 통과했다.
- 게임 안에 최대 300만 노드/5초 예산을 사용하는 힌트 솔버가 구현되어 있다.
- 1,000 스테이지 완료 엔딩, 여정 통계, 로컬 업적 데이터가 구현되어 있다.

### 4.2 운영 관측성

- Firebase Analytics, Crashlytics, Remote Config 초기화 코드가 있다.
- `stage_start`, `stage_clear`, `stage_fail`, `stage_reset` 등 핵심 이벤트가 이미 구조화되어 있다.
- 광고 노출 간격과 힌트 보너스 정책 일부를 Remote Config로 조절할 수 있다.
- 반응형 레이아웃은 프로필·margin·center band가 바뀔 때만 진단 로그를 출력하도록 되어 있다.

### 4.3 모바일 대응

- safe area, 배너 높이, 실제 UI bounds를 반영하는 레이아웃 계산이 있다.
- Android는 portrait 고정 및 진동·네트워크 권한이 선언되어 있다.
- iPhone·Android 세로형, 4:3 태블릿, compact height, large screen 프로필이 코드에 존재한다.

이 기반은 좋다. 다음 작업은 새 기능을 무작정 늘리기보다 이 기능들이 출시 빌드에서 확실하게 동작하도록 고정하는 쪽이 우선이다.

## 5. P0: 출시 전 반드시 정리할 항목

### P0-1. iOS 빌드 오류 제거와 플랫폼 API 분리

**Evidence**

- `GameManager.PlayStageClearHaptic()`는 Android가 아닌 모바일 플랫폼에서 `Handheld.Vibrate()`를 호출한다.
- 이전 iOS 빌드 로그에서 이 호출이 `CS0103` 오류를 발생시켰다.
- 현재 정적 Editor/Android 컴파일은 통과하지만 iOS Player 빌드는 별도 검증되지 않았다.

**Inference**

- 현재 상태에서는 iOS 아카이브 생성이 다시 실패할 가능성이 높다.
- 진동, 스토어 링크, 광고처럼 플랫폼별 API가 메인 게임 클래스 안에 계속 들어가면 유사한 오류가 반복될 가능성이 있다.

**권장 조치**

1. `IHapticService` 또는 작은 `PlatformHaptics` 클래스로 진동 호출을 분리한다.
2. Android, iOS, Editor 구현을 전처리 분기 안에 각각 둔다.
3. iOS용 구현이 준비되지 않았다면 iOS에서는 안전하게 no-op 처리한 뒤 별도 네이티브 진동을 도입한다.
4. Android와 iOS를 각각 Development/Release 조건으로 컴파일한다.

**완료 기준**

- Android ARM64 Player 빌드 성공
- iOS Xcode 프로젝트 생성 성공
- Xcode Archive 성공
- 실제 기기에서 진동 ON/OFF 설정 확인

### P0-2. 앱 식별 정보와 스토어 자산 완성

**Evidence**

- `companyName`이 `DefaultCompany`로 남아 있다.
- Android와 iOS 앱 아이콘 슬롯의 `m_Textures`가 비어 있다.
- iOS Launch Screen 이미지가 비어 있다.
- Android release keystore와 iOS Team/Provisioning 값은 저장소 설정에서 확인되지 않는다.

**Inference**

- 앱 아이콘과 회사 정보는 스토어 제출 품질에 직접 영향을 준다.
- 서명 정보는 저장소에 넣지 않는 것이 정상일 수 있으므로, “없다”가 아니라 “릴리스 빌드 절차에서 별도 확인이 필요하다”로 판단한다.

**권장 조치**

- 회사/개발자 이름을 실제 배포 주체로 변경한다.
- Android adaptive icon의 foreground/background와 legacy icon을 등록한다.
- iPhone, iPad, App Store 1024 아이콘을 등록한다.
- 브랜드가 적용된 launch screen을 준비한다.
- keystore, key alias, Apple Team ID, provisioning profile을 로컬 비밀 저장소 또는 CI secret으로 주입한다.
- 버전 이름과 Android version code/iOS build number 증가 규칙을 문서화한다.

**완료 기준**

- 설치 후 홈 화면, 설정 화면, 최근 앱 화면에서 올바른 앱 이름과 아이콘 표시
- AAB와 Xcode Archive가 배포 인증서로 서명됨
- 스토어 업로드 pre-check 통과

### P0-3. 개인정보처리방침과 이용약관 링크 교체

**Evidence**

- `PrivacyUrl`은 현재 `https://www.naver.com`이다.
- 개인정보처리방침 버튼과 이용약관 버튼이 모두 같은 `PrivacyUrl`을 연다.
- 별도의 `TermsUrl`이 없다.

**Inference**

- 현재 링크는 출시용 정책 문서가 아니다.
- 광고·Firebase·기기 식별자를 사용하는 앱이므로 정책 문서와 실제 수집 동작의 일치 여부가 특히 중요하다.

**권장 조치**

- HTTPS로 공개된 실제 개인정보처리방침 URL을 만든다.
- 별도의 이용약관 URL을 만든다.
- 수집 SDK, 수집 목적, 보관 기간, 삭제 요청 방법, 광고 개인화 여부, 문의처를 실제 동작과 맞춘다.
- 앱 내부 링크, Google Play Data safety, App Store Privacy Nutrition Label의 내용이 서로 일치하는지 검토한다.

**완료 기준**

- 두 버튼이 서로 다른 올바른 문서를 연다.
- 오프라인 또는 URL 실패 시 사용자에게 피드백을 제공한다.
- 정책 문서의 앱 이름·개발자·문의 이메일이 실제 배포 정보와 일치한다.

### P0-4. 광고 및 분석 동의 흐름 마련

**Evidence**

- Firebase Analytics와 Crashlytics 수집 기본값이 `true`다.
- Firebase 사용자 ID로 `SystemInfo.deviceUniqueIdentifier`를 전달한다.
- 프로젝트 코드에서 UMP 동의 폼 또는 ATT 권한 요청 흐름을 찾지 못했다.
- Google Mobile Ads의 `userTrackingUsageDescription`이 비어 있다.

**Inference**

- 국가, 사용자 연령, 광고 개인화 설정, 스토어 정책에 따라 동의 또는 고지 흐름이 필요할 수 있다.
- 식별자를 그대로 Analytics/Crashlytics 사용자 ID로 쓰는 방식은 개인정보 정책 및 최소 수집 원칙 검토가 필요하다.
- 이는 법률 자문이 아니라 코드 기준 위험 표시이며, 실제 요구사항은 배포 지역과 광고 설정에 따라 확인해야 한다.

**권장 조치**

1. 앱 시작 시 광고 SDK보다 먼저 동의 상태를 확인하는 `ConsentCoordinator`를 둔다.
2. Google UMP를 사용해 GDPR/미국 주별 개인정보 선택을 처리한다.
3. iOS에서 개인화 광고가 필요할 때 ATT 설명 문구와 요청 시점을 설계한다.
4. 동의 전에는 Analytics/Crashlytics/광고 개인화 수집을 최소 상태로 유지한다.
5. 영구 기기 식별자 대신 앱 설치 단위의 임의 ID 또는 Firebase 기본 식별 체계를 검토한다.
6. 설정 화면에 개인정보 선택 재진입 경로를 제공한다.

**완료 기준**

- 신규 설치, 동의, 거부, 동의 철회 흐름이 Android/iOS에서 각각 검증됨
- 거부 상태에서도 게임 진행이 가능함
- 정책 문서와 SDK 수집 상태가 일치함

### P0-5. 노출된 미완성 버튼 구현 또는 숨김

**Evidence**

- “평가하기” 버튼은 로그만 출력한다.
- 설정의 “광고 제거” 버튼은 로그만 출력한다.
- 하단 `BlockAdsButton`도 로그만 출력한다.
- `IsRemoveAdsEntitled()`는 로컬 저장값을 읽지만, 구매를 시작하거나 영수증을 검증하는 코드는 없다.
- `com.unity.purchasing` 패키지도 없다.

**Inference**

- 사용자가 결제 또는 스토어 이동을 기대하는 버튼이 아무 동작도 하지 않으면 출시 품질과 신뢰를 해친다.
- 로컬 bool만으로 광고 제거 권리를 판단하면 구매 복원과 기기 변경을 처리할 수 없다.

**권장 조치**

- 1.0에서 광고 제거 판매를 하지 않을 경우 두 광고 제거 버튼을 완전히 숨긴다.
- 판매할 경우 Unity IAP 또는 선택한 스토어 결제 SDK를 도입하고 상품 ID, 구매, 실패, 보류, 복원, 영수증 검증을 구현한다.
- 광고 제거 권리는 구매 영수증에서 복원하고 로컬 저장값은 캐시로만 사용한다.
- “평가하기”는 Android Play Store와 iOS App Store의 실제 앱 페이지 또는 인앱 리뷰 API에 연결한다.

**완료 기준**

- 화면에 보이는 모든 버튼이 실제 동작하거나 명시적으로 비활성 상태를 설명함
- 구매 성공 후 모든 배너·전면 광고가 즉시 사라짐
- 앱 재설치 및 다른 기기에서 구매 복원 확인

### P0-6. 광고 테스트 환경 분리

**Evidence**

- Development Build에서는 `ShouldDisableAdsForDevelopmentBuild()`가 true가 되어 광고 초기화와 로드를 건너뛴다.
- 광고 단위 선택 코드는 Development Build에서 테스트 광고 ID를 고르도록 되어 있다.
- 결과적으로 테스트 광고 ID를 선택하는 빌드에서는 광고 자체가 로드되지 않고, 광고가 로드되는 non-development 빌드에서는 운영 광고 ID가 선택된다.

**Inference**

- 개발 빌드에서 실제 광고 흐름을 검증하기 어렵다.
- 출시 후보를 테스트하려고 운영 광고를 반복 노출하면 잘못된 트래픽 위험이 있다.

**권장 조치**

- `Disabled / Test / Production` 세 가지 광고 환경을 명시적으로 둔다.
- Editor 기본값은 Disabled, Development Build 기본값은 Test, Store Release만 Production으로 강제한다.
- Production 광고 ID를 사용하는 Development Build는 build preflight에서 실패시킨다.
- 보상형 광고의 성공, 닫기, 로드 실패, 보상 미수령, 앱 백그라운드 전환을 실제 테스트 광고로 검증한다.

**완료 기준**

- Development Build에서 Google 공식 테스트 광고가 표시됨
- Release 후보의 환경 값이 빌드 로그에 한 번 명확히 출력됨
- 운영 광고 ID가 테스트 빌드에서 사용되지 않음

### P0-7. 실제 플랫폼 클린 빌드 기준 확정

**Evidence**

- 정적 C# 컴파일은 오류 없이 통과했다.
- Unity 배치 실행은 현재 로컬 라이선스 문제로 완료하지 못했다.
- Firebase 설정 파일은 현재 로컬에는 있지만 Git에서 제외되어 있다.
- 이전 빌드에서는 Firebase 설정 누락, iOS Localization App Info, Addressables 그룹 경고가 발생한 이력이 있다.

**권장 조치**

- “새로 clone한 환경”을 기준으로 Android/iOS 빌드 절차를 작성한다.
- Firebase 설정 파일은 CI secret 또는 안전한 아티팩트 저장소에서 주입한다.
- Addressables, Localization, Firebase, AdMob의 사전 검사 결과를 한 개의 build preflight 로그로 모은다.
- 경고를 “무시 가능”, “출시 차단”으로 분류하고 허용 목록을 최소화한다.

**완료 기준**

- 빈 `Library`에서 Android/iOS 클린 빌드 성공
- 빌드 로그에 프로젝트 코드 오류 0개
- Firebase와 AdMob 초기화 성공을 실제 기기 로그에서 확인

## 6. P1: 출시 안정성을 위해 우선 추가할 항목

### P1-1. 프로젝트 전용 자동화 테스트

**Evidence**

- Unity Test Framework 패키지는 설치되어 있다.
- 프로젝트 전용 `*Tests.cs`, Test asmdef, CI 설정은 발견되지 않았다.
- 핵심 규칙, 힌트 솔버, 저장, 레이아웃이 모두 대형 MonoBehaviour 내부에 있어 단위 테스트 진입점이 적다.

**필수 테스트 묶음**

| 테스트 영역 | 최소 테스트 |
| --- | --- |
| 스테이지 데이터 | 1~1000 파일 존재, JSON 파싱, 좌표 범위, 중복, 시작점, 타입별 필수값 |
| 진행 | 1→2, 999→1000, 1000 엔딩, 없는 스테이지 fallback |
| 규칙 | 일반 이동, 역방향 차단, CrossBlast, FixedKnot, TwinLink, Igniter/Hidden |
| 힌트 | 대표 스테이지 해답 존재, 예산 초과, 캐시 무효화, 안전한 preview |
| 저장 | 신규 설치, 정상 로드, 손상 데이터, 이전 버전 마이그레이션, 초기화 |
| 광고 | 보상 성공/실패/미수령, interstitial cooldown, remove-ads 상태 |
| UI | 레이아웃 프로필 판정, margin clamp, safe area, 배너 reserve |
| 현지화 | 12개 언어 key parity, fallback, 치환 토큰, 긴 텍스트 |

첫 목표는 높은 커버리지 숫자가 아니라 “출시를 깨뜨리는 핵심 경로” 20~30개를 고정하는 것이다.

### P1-2. 스테이지 검증 도구와 생성 파이프라인

**Evidence**

- 1,000개 JSON은 파싱과 기본 구조 검사를 통과했다.
- 1,000개 모두 파일 번호와 내부 `stageID`가 다르다.
- 그중 963개는 `stageID = 0`이다.
- `index.json`은 197개 항목만 포함하며 `stage_55.json`, `stage_56.json`, `stage_200.json` 이후가 빠져 있다.
- 현재 런타임 `StageManager`는 파일명을 직접 로드하므로 `stageID`와 `index.json` 불일치가 즉시 플레이를 막지는 않는다.

**Inference**

- 현재 불일치는 런타임보다 제작·분석·향후 Addressables 전환에서 문제를 만들 가능성이 크다.
- 파일명, 내부 ID, 인덱스 중 어느 값이 정답인지 명확한 단일 소스가 없다.

**추가할 도구**

- `StageValidator`: 모든 파일을 검사하고 실패 시 빌드를 중단
- `StageIndexGenerator`: 파일 목록에서 `index.json` 자동 생성
- `StageIdNormalizer`: 파일 번호와 내부 ID를 일치시키는 일회성 도구
- `StageBalanceReport`: 크기, 총 count, 특수타일 수, 해답 길이, solver node 수를 CSV/JSON으로 출력
- `StageSmokeRunner`: 대표 스테이지를 로드하고 힌트 솔버로 제한 시간 안에 검사

**완료 기준**

- 스테이지 파일 추가/수정 시 검증기가 자동 실행됨
- `stageID == 파일 번호` 규칙이 1,000개 모두 통과함
- `index.json`이 1,000개 파일과 정확히 일치함

### P1-3. 반응형 게임 화면을 순수 계산 로직으로 분리하고 자동 검증

**Evidence**

- 레이아웃 프로필은 `Screen.width/height`, safe area, 배너 reserve, UI bounds를 사용한다.
- 프로필 판정에는 720, 1,400, 1,700, 1,900 같은 고정 pixel threshold가 있다.
- UI가 계산한 top/bottom margin에 스테이지 세로 비율 기반 bottom boost가 추가된다.
- UXML에 82개의 인라인 스타일과 많은 고정 px 크기가 있다.
- 반응형 테스트 사양 문서는 존재하지만 실제 screenshot 결과나 자동 테스트는 저장소에서 확인되지 않았다.

**Inference**

- 패널 스케일, Device Simulator 해상도, 실제 기기 렌더링 배율에 따라 같은 물리적 화면이 다른 프로필로 분류될 수 있다.
- 고정 px 인라인 스타일은 USS 프로필 override를 어렵게 해 작은 높이·긴 스테이지에서 간격 문제가 다시 생길 수 있다.
- 현재 보고된 “높이가 낮은 폰/태블릿에서 긴 스테이지와 하단 UI가 붙는 문제”는 viewport 프로필과 stage shape 보정이 서로 다른 단계에서 적용되고, 최종 보드 bounds를 직접 검증하지 않는 구조와 관련될 가능성이 높다.

**권장 구조**

1. `ResponsiveLayoutPolicy`를 순수 C# 계산 클래스로 분리한다.
2. 입력은 viewport, safe area, panel scale, top/bottom 실제 bounds, banner height, board aspect로 제한한다.
3. 출력은 profile, top/bottom reserved ratio, 최소 visual gap, board max size로 만든다.
4. 보드를 배치한 뒤 실제 screen-space bounds와 UI bounds 사이의 gap을 마지막으로 검증한다.
5. gap이 기준보다 작으면 카메라 orthographic size를 한 번 더 늘리는 최종 제약을 둔다.
6. UXML 인라인 크기를 클래스 기반 USS로 옮겨 profile override가 가능하게 한다.

**필수 viewport**

| 구분 | 예시 | 확인 포인트 |
| --- | --- | --- |
| Tall phone | iPhone 12 portrait | 기존 보드 크기 유지 |
| Tall Android | Galaxy S25 유사 portrait | cutout, nav bar, banner |
| 4:3 tablet | iPad Pro 12.9 2018 portrait | 상단·보드·하단 gap |
| Compact height | 낮은 해상도 또는 reduced-height | 버튼 축소와 최소 gap |
| Large tablet | 12~13인치 Android tablet | 보드 무한 확대 방지 |
| Multi-window | Android resizeable activity | 낮은 height와 focus 전환 |
| Extreme stage | 10x7, 10x3, 4x8, 6x8 | 긴/넓은 보드 bounds |

### P1-4. 대형 컨트롤러 책임 분리

**Evidence**

- `GameManager` 한 파일에 입력, 보드 생성, 규칙, 힌트 솔버, 카메라, 사운드, 진동, 실패 분석, 저장, 분석 이벤트가 함께 있다.
- `GameMainUIController` 한 파일에 UI 바인딩, 설정, 튜토리얼, 현지화, 광고, 하트, 힌트/스킵, 엔딩 크레딧, 반응형 레이아웃이 함께 있다.
- 두 파일만 합쳐 13,616줄이고 메서드는 약 644개다.

**Inference**

- 한 기능 수정이 다른 기능의 직렬화 필드, 초기화 순서, 전처리 분기에 영향을 줄 가능성이 높다.
- 테스트가 어려운 가장 큰 이유도 책임이 MonoBehaviour에 결합되어 있기 때문이다.

**점진적 분리 순서**

| 순서 | 추출 대상 | 이유 |
| --- | --- | --- |
| 1 | `PlatformHaptics` | iOS 빌드 오류와 플랫폼 분기 격리 |
| 2 | `AdService` + `ConsentCoordinator` | 정책·테스트·수명주기 격리 |
| 3 | `ResponsiveLayoutPolicy` + `GameplayCameraFitter` | 현재 디바이스 회귀를 테스트 가능하게 함 |
| 4 | `StageRepository` + `StageValidator` | 1,000개 콘텐츠의 단일 진입점 |
| 5 | `SaveRepository` | 키, 버전, fallback, reset 통합 |
| 6 | `HintSolver`와 순수 board state | GameObject 없이 규칙 테스트 가능 |
| 7 | 설정/튜토리얼/엔딩 UI presenter | UI 컨트롤러 축소 |

한 번에 전면 재작성하지 말고, 기존 public 동작을 유지하면서 한 책임씩 추출해야 한다.

### P1-5. 현지화 체계 하나로 통일

**Evidence**

- 게임 텍스트는 `GameLocalization.cs`의 12개 언어 Dictionary를 사용한다.
- 동시에 `com.unity.localization`이 설치되어 있고 `Localization Settings.asset`이 preloaded asset으로 등록되어 있다.
- Unity Localization의 locale/table은 실질적으로 비어 있고 iOS App Info metadata도 없다.
- Addressables 기본 그룹도 entry가 0개다.

**Inference**

- 현재는 자체 현지화와 Unity Localization 두 체계가 중복되어 빌드 경고와 관리 비용만 만든다.

**선택지**

- 단기 출시 우선: 자체 `GameLocalization`을 유지하고 사용하지 않는 Unity Localization preloaded 설정과 패키지 필요성을 검토한다.
- 장기 운영 우선: 문자열을 String Table로 이전하고 locale, plural, asset localization, iOS App Info metadata까지 Unity Localization으로 통합한다.

어느 쪽이든 두 시스템을 동시에 반쯤 유지하지 않는 것이 중요하다.

### P1-6. 저장 포맷 버전과 마이그레이션

**Evidence**

- ES3와 PlayerPrefs가 혼합되어 있다.
- 일부 데이터는 ES3 우선 + PlayerPrefs fallback이며, 스테이지 진행은 ES3만 저장한다.
- 설정, 하트, 힌트/스킵 charge, 업적, 엔딩 통계가 서로 다른 키와 클래스에 분산되어 있다.
- 데이터 초기화는 주요 진행·업적·엔딩은 지우지만 튜토리얼 dismiss key와 힌트/스킵 charge 정책은 한눈에 확인하기 어렵다.

**권장 조치**

- `SaveSchemaVersion`을 추가한다.
- 전체 저장 키 목록과 reset 유지/삭제 정책을 한 파일에 모은다.
- 이전 버전 → 현재 버전 migration을 순차 함수로 만든다.
- 손상 데이터는 기본값 복구 후 Crashlytics non-fatal로 기록한다.
- remove-ads 구매 권한은 일반 데이터 초기화에서 삭제하지 않는다는 규칙을 테스트한다.

### P1-7. 성능과 배터리 예산 설정

**Evidence**

- 기기의 최대 주사율을 30~240 FPS 범위에서 그대로 목표 FPS로 사용한다.
- 스테이지 전환 때 타일 GameObject를 전부 Destroy하고 다시 Instantiate한다.
- 프로젝트 코드에는 ProfilerMarker가 없다.
- VFX Graph, URP 2D, bloom/후처리, 여러 발광 이펙트를 모바일에서 사용한다.

**Inference**

- 120/144Hz 기기에서 퍼즐 게임이 항상 최대 FPS로 동작하면 배터리·발열 비용이 커질 수 있다.
- 반복 생성/파괴는 스테이지 전환 시 GC spike와 프레임 끊김을 만들 가능성이 있다.

**권장 조치**

- 기본 60 FPS, 선택적 120 FPS 또는 배터리 절약 모드를 제공한다.
- 앱 백그라운드, 설정/크레딧 overlay, 저전력 모드에서 FPS를 낮춘다.
- 타일과 반복 VFX를 pool 또는 재사용한다.
- 보드 생성, 솔버, 스테이지 전환, UI 재배치에 ProfilerMarker를 추가한다.
- 저사양 Android 기준 CPU frame, GPU frame, GC alloc, 메모리, 발열을 기록한다.

## 7. P2: 출시 직후 개선할 항목

### P2-1. CI와 Build Preflight

최소 파이프라인:

1. JSON/메타데이터 검증
2. EditMode 테스트
3. PlayMode smoke test
4. Android/iOS 스크립트 컴파일
5. Development AAB 또는 APK 빌드
6. 빌드 로그 오류·경고 정책 검사
7. 버전·앱 ID·광고 환경·정책 URL·아이콘 preflight
8. 빌드 아티팩트와 테스트 리포트 보관

### P2-2. 저장소와 패키지 정리

**Evidence**

- 감사 시점에 생성형 Firebase meta, Package manifest/lock, ProjectSettings, VS Code, 성능 테스트 JSON, `.slnx`가 수정 또는 미추적 상태다.
- `com.coplaydev.unity-mcp`는 GitHub `main` 브랜치를 직접 참조한다.
- 에셋 안에 TMP, DOTween Pro, LightningBolt 등의 예제 콘텐츠가 다수 존재한다.

**권장 조치**

- `.vscode/`, `*.slnx`, 성능 테스트 생성 파일의 추적 정책을 결정한다.
- Unity MCP 의존성은 특정 commit/tag로 고정하거나 개발 전용 설치 절차로 분리한다.
- 실제 빌드에 필요 없는 예제 scene/prefab/font를 제거하되 라이선스 파일은 보존한다.
- 패키지 추가/삭제는 클린 빌드와 build report 크기 비교 후 수행한다.

### P2-3. Resources 사용 전략 정리

**Evidence**

- 런타임 스크립트에서 `Resources.Load`가 약 30회 사용된다.
- 1,000개 스테이지가 모두 Resources 아래에 있다.
- Addressables 그룹은 비어 있고 런타임 Addressables API 사용은 없다.

**권장 조치**

- 1.0에서는 현재 Resources 방식으로 출시해도 되지만, 모든 스테이지가 빌드에 포함된다는 점을 build report로 확인한다.
- 향후 콘텐츠 업데이트가 필요하면 StageRepository 뒤에서 Addressables 또는 원격 catalog로 교체한다.
- 현재 사용하지 않는 빈 Addressables 파이프라인은 Localization 선택과 함께 정리한다.

### P2-4. 로그 정책

**Evidence**

- 프로젝트 스크립트에서 Debug Log/Warning/Error 호출이 약 150개다.
- 핵심 두 컨트롤러에 일반 Debug.Log가 약 50개 있다.

**권장 조치**

- 반복 플레이 로그는 `DEVELOPMENT_BUILD` 또는 로깅 레벨 뒤로 이동한다.
- 에러·복구·분석 breadcrumb는 유지한다.
- 스테이지 번호, 광고 환경, 레이아웃 profile 같은 진단값은 변경 시에만 출력한다.

### P2-5. 문서 최신화

**Evidence**

- README는 스테이지가 35개이며 특수 타일이 거의 쓰이지 않는다고 설명한다.
- 실제로는 1,000개이며 특수 타일도 수백 개 사용된다.

**권장 문서**

- 최신 README
- Android/iOS 클린 빌드 가이드
- Release checklist
- Stage authoring/validation guide
- Firebase/AdMob/Consent 운영 가이드
- 저장 데이터 schema와 migration 기록
- QA device matrix와 known issues

## 8. P3: 기반 안정화 후 추가할 제품 기능

### 8.1 접근성

- 색각 이상 사용자를 위한 색상 외 모양·패턴 구분
- 발광/플래시 강도 감소 옵션
- 진동 세기 또는 진동 종류 설정
- 고대비 모드
- 큰 글자에서도 설정·튜토리얼이 잘리는지 검증
- VoiceOver/TalkBack에서 최소한의 메뉴 탐색 지원

### 8.2 플레이 편의

- 최근 행동 한 단계 되돌리기 또는 제한형 undo
- 힌트 사용 전 preview와 취소
- 스테이지 즐겨찾기/다시 플레이
- 어려운 스테이지 북마크
- FPS/배터리 절약 설정

### 8.3 라이브 콘텐츠

Firebase Remote Config에는 이미 daily, weekly, infinite, leaderboard flag가 있다. 다만 아래 기반을 먼저 만든 뒤 활성화하는 것이 좋다.

- 서버 기준 날짜와 부정행위 방지
- 콘텐츠 버전과 fallback
- 오프라인 정책
- 보상 중복 지급 방지
- 원격 기능 kill switch
- 이벤트 분석 대시보드

권장 순서:

1. Daily Challenge
2. Weekly curated stage
3. Cloud Save
4. Play Games/Game Center 업적 동기화
5. Leaderboard
6. Infinite/생성형 모드

### 8.4 운영 기능

- 앱 내부 버전·빌드 번호 표시
- 개인정보 선택 관리
- 데이터 삭제 요청 안내
- 공지/점검 메시지
- 최소 지원 버전과 강제 업데이트가 아닌 권장 업데이트
- 장애 시 광고·원격 기능을 끄는 kill switch

## 9. 권장 QA 매트릭스

### 9.1 디바이스

| 플랫폼 | 최소 대상 |
| --- | --- |
| iOS | iPhone 12, 최신 긴 화면 iPhone, iPad Pro 12.9 2018 |
| Android | Galaxy S25 유사, 60Hz 중급기, 저사양/작은 높이 기기, 4:3 또는 16:10 태블릿 |
| 화면 상태 | portrait, multi-window/reduced-height, safe area/cutout, 시스템 글자 확대 |

### 9.2 시나리오

- 첫 설치 → 동의 → 첫 스테이지 → 튜토리얼
- 동의 거부 상태에서 플레이
- 네트워크 없음, 느린 네트워크, 광고 no-fill
- 광고 도중 앱 background/foreground
- 보상 callback 전 광고 닫기
- 999 → 1000 → 엔딩 → 1 스테이지 복귀
- 저장 데이터 손상 및 이전 버전 업그레이드
- 기기 시간 앞으로/뒤로 변경 후 하트·힌트 recharge
- 한국어, 영어, 독일어, 러시아어, 태국어, 중국어의 긴 문구
- 10x7, 10x3, 4x8, 6x8 등 극단 스테이지

### 9.3 성능 예산 예시

| 항목 | 초기 목표 |
| --- | --- |
| 기본 FPS | 60 FPS 안정 |
| 고주사율 옵션 | 명시적으로 선택한 경우만 120 FPS |
| gameplay GC alloc | 평상시 프레임당 0 B에 가깝게 |
| stage transition | 체감 멈춤 없이 완료 |
| 메모리 | 저사양 Android에서 반복 50 stage 후 지속 증가 없음 |
| 발열 | 20분 플레이 후 성능 급락 없음 |

## 10. 단계별 실행 로드맵

### 0단계: 출시 차단 제거

- iOS 진동 컴파일 오류 수정
- 실제 정책 URL과 이용약관 URL 연결
- 미완성 버튼 구현 또는 숨김
- 앱 아이콘, 회사명, launch screen 설정
- 광고 동의와 Test/Production 환경 분리
- Android/iOS 실기기 클린 빌드

### 1단계: 회귀 방지

- StageValidator와 index generator 추가
- 핵심 EditMode 테스트 20~30개
- responsive policy 단위 테스트
- 주요 viewport screenshot baseline
- BuildPreflight와 CI 추가

### 2단계: 구조와 성능

- PlatformHaptics, AdService, ResponsiveLayoutPolicy 추출
- SaveRepository와 schema migration 추가
- 타일/VFX 재사용 검토
- FPS 정책과 profiler marker 추가
- Localization 체계 하나로 통일

### 3단계: 출시 후 확장

- crash-free rate, stage fail funnel, 광고 수익/이탈 데이터 확인
- 밸런스가 나쁜 스테이지 조정
- 접근성 옵션 추가
- daily/weekly → cloud save → leaderboard 순으로 확장

## 11. 출시 체크리스트

### 빌드

- [ ] Android ARM64 Release AAB 빌드 성공
- [ ] iOS Xcode Archive 성공
- [ ] Development Build는 테스트 광고만 사용
- [ ] Store Release는 운영 광고만 사용
- [ ] Firebase 설정 파일 주입 자동화
- [ ] 프로젝트 코드 컴파일 오류 0개
- [ ] 허용되지 않은 빌드 경고 0개

### 스토어·정책

- [ ] 회사명과 앱 이름 확정
- [ ] Android/iOS 앱 아이콘 등록
- [ ] 개인정보처리방침 URL 연결
- [ ] 이용약관 URL 연결
- [ ] App Store Privacy / Play Data safety 작성
- [ ] 광고 동의, ATT 필요성 검토 및 구현
- [ ] 지원 이메일 수신 테스트
- [ ] 평가하기 링크 테스트

### 게임

- [ ] 1~1000 데이터 검증 통과
- [ ] 1, 999, 1000 스테이지 실플레이
- [ ] 모든 특수 타일 대표 스테이지 확인
- [ ] 힌트/스킵/하트/리셋 흐름 확인
- [ ] 1000 엔딩 후 재진입 확인
- [ ] 데이터 초기화 및 구매 복원 정책 확인

### UI

- [ ] iPhone 12 portrait
- [ ] Galaxy S25 유사 portrait
- [ ] iPad Pro 12.9 2018 portrait
- [ ] compact height
- [ ] Android multi-window
- [ ] 12개 언어 긴 문구 overflow
- [ ] 배너 있음/없음/no-fill 상태

### 운영

- [ ] Firebase Analytics 이벤트 수신 확인
- [ ] Crashlytics non-fatal 및 테스트 crash 확인
- [ ] Remote Config 기본값과 콘솔 값 일치
- [ ] 광고 보상 중복 지급 없음
- [ ] release dashboard와 알림 기준 설정

## 12. 근거 파일

- `Assets/Scripts/GameManager.cs`
  - 게임 흐름, 힌트 솔버, 저장, 카메라, 진동, 프레임률, 타일 생성/파괴
- `Assets/Scripts/GameMainUIController.cs`
  - 설정, 광고, 하트, 튜토리얼, 현지화, 엔딩, 반응형 레이아웃
- `Assets/Scripts/Firebase/FirebaseBootstrap.cs`
  - Analytics/Crashlytics 기본 수집, user ID, Remote Config
- `Assets/Scripts/StageManager.cs`
  - 파일명 기반 Resources 로딩과 cache
- `Assets/Scripts/StageData.cs`
  - 스테이지 JSON schema
- `Assets/Scripts/GameLocalization.cs`
  - 12개 언어 자체 Dictionary
- `Assets/Resources/GameMainUI.uxml`
  - 설정·튜토리얼 UI의 다수 인라인 px 스타일
- `Assets/Resources/GameMainUI.uss`
  - 반응형 profile별 스타일
- `Assets/Resources/Stages/index.json`
  - 197개 항목만 포함된 현재 index
- `ProjectSettings/ProjectSettings.asset`
  - 회사명, 아이콘, signing, orientation, FPS/빌드 설정
- `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`
  - AdMob app ID와 비어 있는 tracking usage description
- `Assets/Localization Settings.asset`
  - 비어 있는 metadata와 실사용되지 않는 Unity Localization 설정
- `Packages/manifest.json`
  - Localization/Test Framework 설치, IAP 미설치
- `README.md`
  - 35개 스테이지로 남아 있는 오래된 프로젝트 설명

## 13. 검증 결과와 한계

### 확인 완료

- 1,000개 스테이지 JSON 문법 검사 통과
- 1,000개 스테이지 기본 구조 검사 통과
- 특수 타일 필수값 기본 검사 통과
- 1,000개 파일의 `stageID` 불일치 확인
- `index.json` 197개 항목 확인
- 프로젝트 전용 테스트·CI 부재 확인
- C# 정적 컴파일 오류 0개 확인
- Android/iOS Firebase 설정 파일의 로컬 존재 확인

### 미확인

- 실제 App Store Connect / Google Play Console 설정
- 실제 배포 서명 인증서와 provisioning 상태
- Android/iOS Store Release 전체 빌드
- 실제 기기 screenshot 및 frame timing
- 1,000개 스테이지의 완전한 해답 존재 여부와 난이도 곡선
- 광고 콘솔의 consent, age rating, test device 설정

Unity 배치 실행은 로컬 환경에서 유효한 Unity Editor 라이선스를 찾지 못해 완료하지 못했다. 따라서 이 문서는 저장소 근거와 정적 검증을 중심으로 작성되었으며, 최종 출시 판정에는 Android/iOS 실기기 Release 빌드가 반드시 추가되어야 한다.

## 14. 최종 권고

가장 현실적인 다음 순서는 아래와 같다.

1. P0 항목을 해결해 “스토어에 올릴 수 있는 빌드”를 먼저 만든다.
2. StageValidator, 핵심 테스트, BuildPreflight를 추가해 그 상태가 다시 깨지지 않게 한다.
3. 반응형 레이아웃 계산과 플랫폼 서비스를 대형 컨트롤러에서 분리한다.
4. 실제 사용자 데이터가 쌓인 뒤 난이도 조정과 새 모드를 추가한다.

현재 프로젝트에는 추가 기능보다 출시 안전장치가 더 필요하다. 이 순서를 따르면 기존 1,000개 콘텐츠와 이미 구현된 시스템을 버리지 않고도 출시 위험을 크게 줄일 수 있다.
