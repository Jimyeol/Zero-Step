using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 네온 퍼즐 게임 매니저: JSON 스테이지 로드, 그리드 생성(count==0 스킵), 드래그 경로, Line Renderer, Stage Clear 시 다음 스테이지.
/// </summary>
public class GameManager : MonoBehaviour
{
    private const float TallStageBottomGameplayBoostMaxNormalized = 0.04f;
    private const float TallStageAspectBoostStart = 1.15f;
    private const float TallStageAspectBoostFull = 1.8f;
    private const float LargeStageBottomGameplayBoostMaxNormalized = 0.02f;
    private const float LargeStageBoostStartCells = 6f;
    private const float LargeStageBoostFullCells = 8f;

    [Header("그리드 설정")]
    [SerializeField] private float padding = 0.2f;
    [SerializeField] private float fitMargin = 1.05f;
    [Tooltip("화면 양 끝 패딩. 1.15 = 그리드 주변 15% 여백 (맵이 화면 끝에 붙지 않음)")]
    [SerializeField] private float screenEdgePadding = 1.15f;
    [Tooltip("스테이지 JSON 없을 때 폴백용")]
    [SerializeField] private int fallbackRows = 3;
    [SerializeField] private int fallbackCols = 3;
    [SerializeField] private int fallbackInitialNumber = 3;

    [Header("참조")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ProceduralGridBackground proceduralBackground;
    [Tooltip("TwinLink 타일 전기 효과용. Assets/LightningBolt/SimpleLightningBoltAnimatedPrefab 할당")]
    [SerializeField] private GameObject twinLinkLightningPrefab;

    [Header("TwinLink 전기 효과 (Inspector에서 여기서 조정)")]
    [Tooltip("테두리 반폭. 1이면 타일 가장자리에 딱 맞게")]
    [SerializeField] private float twinLinkBorderOffset = 0.98f;
    [Tooltip("번개 갱신 간격(초). 짧을수록 끊김 없이 계속 흐르는 느낌 (권장 0.03~0.06)")]
    [SerializeField] [Range(0.02f, 0.2f)] private float twinLinkBoltInterval = 0.04f;
    [Tooltip("전기 꺾임(0~0.5). 낮을수록 직선에 가깝고 끊김 없이 흐름")]
    [SerializeField] [Range(0f, 0.5f)] private float twinLinkChaosFactor = 0.03f;
    [Tooltip("번개 세부 분할. 낮을수록 단순한 선, 끊김 감소")]
    [SerializeField] [Range(2, 6)] private int twinLinkBoltGenerations = 3;
    [Tooltip("전기 두께 배율. 타일 크기 기준")]
    [SerializeField] private float twinLinkBoltWidthScale = 0.25f;
    [Tooltip("밟을 때 번쩍임 지속 시간")]
    [SerializeField] private float twinLinkFlashDuration = 0.2f;
    [Tooltip("밟을 때 흔들림 강도")]
    [SerializeField] private float twinLinkShakeStrength = 0.08f;
    [Tooltip("대기 중 전기 테두리 알파. 낮을수록 조용하게 보임")]
    [SerializeField] [Range(0.1f, 1f)] private float twinLinkIdleAlphaMultiplier = 0.34f;
    [Tooltip("대기 중 한 번에 갱신할 테두리 변 개수. 낮을수록 덜 산만함")]
    [SerializeField] [Range(1, 4)] private int twinLinkIdleEdgesPerPulse = 2;
    [Tooltip("발동 순간 같은 그룹을 잇는 연결선 두께 배율")]
    [SerializeField] private float twinLinkActivationLineWidthScale = 0.12f;
    [Tooltip("발동 연결선이 사라지는 시간(초)")]
    [SerializeField] private float twinLinkActivationLineDuration = 0.22f;
    [Tooltip("발동 연결선 최대 알파")]
    [SerializeField] [Range(0.1f, 1f)] private float twinLinkActivationLineAlpha = 0.9f;

    [Header("네온 트레일 (손가락 궤적)")]
    [SerializeField] private Color trailColor = new Color(1f, 0.4f, 1f, 1f);
    [Tooltip("트레일 잔상 유지 시간(초). 손 뗀 후 이 시간만큼 남았다가 사라짐")]
    [SerializeField] private float trailTime = 0.5f;
    [Tooltip("트레일 꼭지점 최소 간격. 작을수록 부드러운 선")]
    [SerializeField] private float trailMinVertexDistance = 0.1f;
    [Tooltip("속 빈 네온 윤곽선 트레일 셰이더. 비어 있으면 Resources/Shaders/HollowNeonTrail을 사용")]
    [SerializeField] private Shader hollowTrailShader;
    [Tooltip("트레일 폭 중 양쪽 윤곽선이 차지하는 비율")]
    [SerializeField] [Range(0.02f, 0.45f)] private float trailOutlineWidth = 0.18f;
    [Tooltip("윤곽선 가장자리 부드러움")]
    [SerializeField] [Range(0.005f, 0.2f)] private float trailOutlineSoftness = 0.04f;
    [Tooltip("Bloom에 걸리는 바깥 광원 폭")]
    [SerializeField] [Range(0.05f, 1f)] private float trailGlowWidth = 0.58f;
    [Tooltip("속 부분의 아주 약한 투명 잔광. 0이면 완전히 비어 보임")]
    [SerializeField] [Range(0f, 0.2f)] private float trailCenterAlpha = 0.015f;
    [Tooltip("손 뗀 후 경로(링크) 점등 해제까지 대기 시간(초)")]
    [SerializeField] private float pathLitClearDelay = 1f;

    [Header("힌트 미리보기")]
    [Tooltip("힌트 경로 밝기 pulse 반복 시간(초). 힌트는 유저가 다시 터치하기 전까지 유지됨")]
    [SerializeField] private float hintPreviewDuration = 2.6f;
    [Tooltip("타일 크기 대비 힌트 라인 두께")]
    [SerializeField] private float hintPreviewLineWidthScale = 0.12f;
    [Tooltip("힌트 라인의 최대 알파")]
    [SerializeField] [Range(0.1f, 1f)] private float hintPreviewMaxAlpha = 0.96f;
    [Tooltip("전체 힌트 경로를 흐리게 깔 때의 알파 배율")]
    [SerializeField] [Range(0.05f, 0.8f)] private float hintPreviewBaseAlphaMultiplier = 0.48f;
    [Tooltip("밝은 하이라이트가 힌트 경로 전체를 한 번 훑는 시간(초)")]
    [SerializeField] private float hintPreviewSweepDuration = 1.15f;
    [Tooltip("힌트 흐름 화살표 머리 길이. 타일 크기 기준 배율")]
    [SerializeField] private float hintPreviewArrowLengthScale = 0.22f;
    [Tooltip("힌트 흐름 화살표 머리 폭. 타일 크기 기준 배율")]
    [SerializeField] private float hintPreviewArrowWidthScale = 0.16f;
    [Tooltip("힌트가 없을 때 스낵바 표시 시간")]
    [SerializeField] private float hintSnackbarDuration = 1.6f;

    [Header("Multi-Color Neon Trail (그라데이션 순환 + 특수 타일 반응)")]
    [Tooltip("4가지 이상 네온 컬러. Cyan, Magenta, Purple, Electric Blue 등")]
    [SerializeField] private Color[] neonGradientColors = new Color[]
    {
        new Color(0f, 1f, 1f, 1f),   // Cyan
        new Color(1f, 0f, 1f, 1f),   // Magenta
        new Color(0.6f, 0.2f, 1f, 1f), // Purple
        new Color(0.2f, 0.5f, 1f, 1f)  // Electric Blue
    };
    [Tooltip("Bloom용 HDR 강도. 2 이상 권장")]
    [SerializeField] [Min(1f)] private float trailHdrIntensity = 2.2f;
    [Tooltip("그라데이션 색상이 흐르는 속도 (키 순환)")]
    [SerializeField] private float trailColorShiftSpeed = 1.5f;
    [Tooltip("네온 그라데이션 갱신 빈도(Hz). 너무 높으면 GC/CPU 부하가 커질 수 있음")]
    [SerializeField] [Range(10f, 120f)] private float trailGradientUpdateHz = 45f;
    [Tooltip("특수 타일 밟았을 때 해당 컬러로 0.2초간 Lerp 후 복귀")]
    [SerializeField] private float specialTileColorLerpDuration = 0.2f;

    [Header("스테이지")]
    [SerializeField] private int startStageIndex = 1;
    [SerializeField] private float nextStageDelay = 1.5f;


    [Header("사운드")]
    [Tooltip("블록 count가 -1 될 때 재생되는 음 볼륨")]
    [SerializeField] [Range(0f, 1f)] private float blockNoteVolume = 0.9f;
    [Tooltip("clear/fail/new stage 효과음 볼륨")]
    [SerializeField] [Range(0f, 1f)] private float eventSfxVolume = 1f;
    [Tooltip("같은 프레임에 여러 count 감소가 발생할 때 음 간격(초)")]
    [SerializeField] [Range(0.01f, 0.2f)] private float blockNoteInterval = 0.045f;

    [Header("블록 음계 (Resources/Sounds 자동 연결)")]
    [SerializeField] private AudioClip blockAClip;
    [SerializeField] private AudioClip blockAsClip;
    [SerializeField] private AudioClip blockBClip;
    [SerializeField] private AudioClip blockCClip;
    [SerializeField] private AudioClip blockCoClip;
    [SerializeField] private AudioClip blockCsClip;
    [SerializeField] private AudioClip blockDClip;
    [SerializeField] private AudioClip blockDsClip;
    [SerializeField] private AudioClip blockEClip;
    [SerializeField] private AudioClip blockFClip;
    [SerializeField] private AudioClip blockFsClip;
    [SerializeField] private AudioClip blockGClip;
    [SerializeField] private AudioClip blockGsClip;

    [Header("상태 효과음 (Resources/Sounds 자동 연결)")]
    [SerializeField] private AudioClip failClip;
    [SerializeField] private AudioClip newStageClip;
    [SerializeField] private AudioClip clearClip;
    [SerializeField] private AudioClip bombClip;
    [SerializeField] [Range(0f, 1f)] private float bombSfxVolumeMultiplier = 0.5f;

    /// <summary>Easy Save 3 진행도 저장 키. 앱 재실행 시 이 스테이지부터 시작.</summary>
    private const string SaveKeyStage = "StageProgress";
    private const string StageClearTypeAllTilesZero = "all_tiles_zero";
    private const string StageClearTypeLastTileRule = "last_tile_rule";
    private const string StageFailReasonNoLegalMove = "deadlock_no_legal_move";
    private const string StageFailReasonUnreachableRemainingTiles = "unreachable_remaining_tiles";
    private const string StageFailReasonHiddenUntriggerable = "hidden_untriggerable";
    private const string StageFailReasonShortCircuitBlocked = "short_circuit_blocked";
    private const string StageFailReasonTwinLinkUnsatisfiable = "twin_link_unsatisfiable";
    private const string StageFailReasonInvalidCurrentStart = "invalid_current_start";
    private const string StageFailReasonFixedKnotMissed = "fixed_knot_missed";
    private const int ManualRetryHeartPenaltyMoveThreshold = 2;
    private static readonly bool ManualRetryConsumesHeart = false;
    private const float BoardFailureSnackbarDuration = 2.2f;
    private const float FixedKnotMissedSnackbarDuration = 2.2f;
    private const int VerboseDebugStageIndex = 6;
    private const float SessionFreeHeartRefillFirstThresholdSeconds = 10f * 60f;
    private const float SessionFreeHeartRefillSecondThresholdSeconds = 20f * 60f;
    private const int BackgroundGridReferenceColumns = 2;
    private const int BackgroundGridReferenceRows = 2;
    public static bool VerboseStage6DebugEnabled { get; private set; }

    [Header("성능 (디바이스 최대 FPS)")]
    [Tooltip("실행 중 디바이스가 지원하는 최대 주사율을 목표 FPS로 사용합니다.")]
    [SerializeField] private bool useDeviceMaxFps = true;

    [Header("게임오버·리셋 연출")]
    [Tooltip("깜빡임 한 번당 간격(초). 0.1초에 2번 깜빡임 = 0.025")]
    [SerializeField] private float blinkInterval = 0.025f;
    [Tooltip("암전 후 리셋 전 대기 시간(초)")]
    [SerializeField] private float blackoutWait = 1.5f;
    [Tooltip("순차 등장 시 타일 간 간격(초). 작을수록 빠름")]
    [SerializeField] private float tileAppearInterval = 0.02f;

    // 실패 연출: "왜 졌는지 보여주고, 빠르게 재시작". 스트로브(깜빡임)는 광과민 위험으로 쓰지 않는다.
    private const float FailureExplainDuration = 0.4f;
    private const float FailureExplainSkipAfter = 0.2f;
    private const float FailureFadeToDarkDuration = 0.12f;
    private const float FailureDarkHoldDuration = 0.04f;
    private const float FailureOtherTilesDimFactor = 0.3f;
    private const float RestartCascadeMaxDuration = 0.22f;
    private const int FailureFocusPulseCount = 3;
    private readonly List<Tile> failureHighlightTiles = new List<Tile>();
    private readonly List<Tile> restartCascadeTiles = new List<Tile>();
    private readonly List<Color> failureFadeStartColors = new List<Color>();

    [Header("네온 보드 연출")]
    [SerializeField] [Range(0.1f, 0.7f)] private float stageStartCascadeDuration = 0.36f;
    [SerializeField] [Range(0.2f, 0.9f)] private float stageClearWaveDuration = 0.55f;
    [SerializeField] [Range(0.1f, 1f)] private float stageClearWaveAlpha = 0.72f;
    [SerializeField] [Range(0.01f, 0.12f)] private float stageClearWaveWidthScale = 0.04f;

    [Header("Blackout 물음표 전환 연출")]
    [Tooltip("한 줄당 Y축 한 바퀴 회전 시간(초). 50% 지점에서 ?로 전환")]
    [SerializeField] private float blackoutFlipDuration = 0.35f;
    [Tooltip("윗줄에서 아랫줄로 내려가는 줄 간격(초)")]
    [SerializeField] private float blackoutRowInterval = 0.05f;

    [Header("Spotlight 게임오버 Vignette")]
    [Tooltip("암전 시 Vignette 강한 농도. 비어 있으면 씬의 Volume 자동 탐색")]
    [SerializeField] private Volume postProcessVolume;
    [Tooltip("암전 시 Vignette 순식간에 올리는 시간(초)")]
    [SerializeField] private float vignetteRampUpDuration = 0.25f;
    [Tooltip("암전 후 기본 암막으로 돌아오는 시간(초)")]
    [SerializeField] private float vignetteReturnDuration = 0.6f;
    [Tooltip("암전 시 Vignette 최대 강도 (0~1)")]
    [SerializeField] [Range(0f, 1f)] private float vignetteMaxIntensity = 0.85f;
    [Tooltip("평소 Vignette 강도 (기본 암막 크기). 0이면 효과 없음")]
    [SerializeField] [Range(0f, 1f)] private float vignetteDefaultIntensity = 0f;

    [Header("카메라 UI 여백")]
    [Tooltip("상단 UI(TopBar) 높이에 대응하는 화면 비율 (0~0.4 정도 권장)")]
    [SerializeField] [Range(0f, 0.4f)] private float uiTopMarginNormalized = 0.22f;
    [Tooltip("하단 버튼 바(BottomBar) 높이에 대응하는 화면 비율 (0~0.4 정도 권장)")]
    [SerializeField] [Range(0f, 0.4f)] private float uiBottomMarginNormalized = 0.18f;
    [Tooltip("실제 하단 UI 위에 추가로 비워둘 게임 보드 여백. 태블릿에서 하단 UI와 보드가 붙는 것을 방지")]
    [SerializeField] [Range(0f, 0.15f)] private float uiBottomGameplayGapNormalized = 0.04f;

    private int currentStageIndex;
    private float tileWidth;
    private float tileHeight;
    private float totalGridWidth;
    private float totalGridHeight;
    private int stageWidth;
    private int stageHeight;
    private Tile[,] tiles;
    /// <summary>
    /// 현재 시작점(Current Start Point): 다음 드래그를 시작할 수 있는 타일.
    /// 스테이지 시작 시에는 JSON의 초기 시작점(Initial Start Point)이고,
    /// 플레이 중에는 손을 뗀 마지막 타일로 계속 갱신된다.
    /// </summary>
    private Tile currentStartTile;
    private Tile gameOverFocusTile;
    /// <summary>게임오버 리셋 시 시작점 복원용. tiles[row, col] 인덱스.</summary>
    private int initialStartTileRow;
    private int initialStartTileCol;
    private List<Tile> currentPath = new List<Tile>();
    /// <summary>마지막으로 타일을 경로에 추가한 프레임. 직선 드래그 시 되돌아가기 지터 무시용.</summary>
    private int lastStepFrame = -1;
    private float lastStepTime = -1f;
    /// <summary>지금까지 커밋된 스텝 수. 기어 숫자 이어가기.</summary>
    private int totalStepsCommitted;
    private bool isDragging;
    private bool stageCleared;
    /// <summary>손 뗀 후 경로 점등 해제용 코루틴.</summary>
    private Coroutine pathLitClearRoutine;
    /// <summary>트레일 터치 시작 시 1프레임 뒤 emitting 재개하는 코루틴.</summary>
    private Coroutine trailEmitDelayRoutine;
    private Coroutine blackoutQuestionFlipRoutine;
    private bool blackoutQuestionFlipPlayed;
    /// <summary>손가락 궤적 네온 트레일. 드래그 중에만 emitting, 위치는 포인터 월드 좌표.</summary>
    private TrailRenderer neonTrail;
    private Transform neonTrailTransform;
    /// <summary>특수 타일 밟았을 때 트레일이 이 색으로 0.2초간 Lerp. EndTime 초과 시 무시.</summary>
    private Color specialTileColor;
    private float specialTileColorLerpStartTime = -999f;
    /// <summary>게임오버·리셋 연출 진행 중이면 입력 차단.</summary>
    private bool isGameOverSequencePlaying;
    /// <summary>인접 타일 사이 Link 배치·경로/체인 점등.</summary>
    private LinkSystem linkSystem;
    /// <summary>TwinLink 타일: linkID별 그룹 (그리드 생성 후 파트너 등록용).</summary>
    private Dictionary<int, List<TwinLinkTile>> twinLinkGroups = new Dictionary<int, List<TwinLinkTile>>();
    private readonly Dictionary<int, Color> twinLinkAssignedColors = new Dictionary<int, Color>();
    private readonly Dictionary<int, string> twinLinkAssignedLabels = new Dictionary<int, string>();
    private readonly List<Color> twinLinkAvailablePalette = new List<Color>();
    private int nextTwinLinkLabelIndex;
    /// <summary>Hidden 타일: groupID별 그룹 (Igniter 트리거 시 활성화용).</summary>
    private Dictionary<string, List<HiddenTile>> hiddenGroups = new Dictionary<string, List<HiddenTile>>();
    /// <summary>CrossBlast/Hidden 릴레이처럼 보드 접근성이 지연 변경 중이면 실패 확정을 미룬다.</summary>
    private int pendingBoardMutationCount;
    private bool pendingBoardFailureRecheck;
    private Tile pendingBoardFailureClearReferenceTile;
    private Coroutine deferredBoardFailureCheckRoutine;
    private Coroutine postStageStartedFailureCheckRoutine;
    private const int HintPreviewMaxMoves = 8;
    private const int HintPreviewMaxPathTiles = HintPreviewMaxMoves + 1;
    private const int HintQuickSolverNodeLimit = 120000;
    private const float HintQuickSolverTimeLimitSeconds = 0.12f;
    private const int HintSolverNodeLimit = 3000000;
    private const float HintSolverTimeLimitSeconds = 4f;
    private const float HintLoadingMinimumVisibleSeconds = 0.45f;
    // 힌트 전체 탐색은 프레임마다 이 시간만큼만 계산해 화면·입력이 멈추지 않게 한다.
    private const float HintSolverSliceSecondsWithLoading = 0.010f;
    private const float HintSolverSliceSecondsSilent = 0.003f;
    private const float HintSilentSolverTimeLimitSeconds = 1.5f;
    private string silentHintExhaustedKey;
    private int boardVersion;
    private int cachedHintBoardVersion = -1;
    private Tile cachedHintStartTile;
    private Tile cachedHintPreviousTile;
    private int cachedHintNextStepNumber = -1;
    private bool cachedHintUnavailable;
    private readonly List<Tile> cachedHintPath = new List<Tile>(HintPreviewMaxPathTiles);
    private readonly List<Tile> cachedHintSolutionPath = new List<Tile>(64);
    private string cachedHintSolverStatus = "";
    private LineRenderer[] hintPreviewLines;
    private LineRenderer hintPreviewFlowLine;
    private LineRenderer hintPreviewArrowLeftLine;
    private LineRenderer hintPreviewArrowRightLine;
    private LineRenderer stageClearWaveLine;
    private Material hintPreviewMaterial;
    private Coroutine hintPreviewRoutine;
    private Coroutine hintPreviewRequestRoutine;
    private Coroutine stageStartVisualRoutine;
    private Coroutine stageClearWaveRoutine;
    private bool isHintPreviewRequestRunning;
    private int activeHintPreviewLineCount;
    private readonly List<Vector3> activeHintPreviewPoints = new List<Vector3>(HintPreviewMaxPathTiles);
    // 힌트를 따라 그리는 동안 남은 구간만 보여주기 위한 상태.
    private readonly List<Tile> activeHintPreviewTiles = new List<Tile>(HintPreviewMaxPathTiles);
    private int hintPreviewFollowIndex;
    private LineRenderer[] hintSegmentArrowLines;
    // 숫자 색(민트·하늘·분홍)·TwinLink 색과 겹치지 않도록 힌트는 흰색 계열로 고정한다.
    private static readonly Color HintLineColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color HintFlowColor = new Color(1.2f, 1.15f, 1f, 1f);
    private const float HintSweepSecondsPerSegment = 0.25f;
    private static readonly Color[] TwinLinkRandomPalette =
    {
        new Color(1f, 0.55f, 0.12f, 1f),
        new Color(1f, 0.86f, 0.2f, 1f),
        new Color(0.72f, 1f, 0.18f, 1f),
        new Color(0.66f, 0.44f, 1f, 1f),
        new Color(1f, 0.28f, 0.38f, 1f)
    };

    private AudioSource blockNoteAudioSource;
    private AudioSource eventAudioSource;
    private readonly Queue<AudioClip> pendingBlockNoteQueue = new Queue<AudioClip>();
    private readonly Queue<int> pendingSessionFreeHeartRefillMinutes = new Queue<int>();
    private readonly Dictionary<string, AudioClip> blockNoteClipMap = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string[]> sparseMelodyPool = new List<string[]>();
    private readonly List<string[]> mediumMelodyPool = new List<string[]>();
    private readonly List<string[]> denseMelodyPool = new List<string[]>();
    private string[] activeMelody = Array.Empty<string>();
    private int activeMelodyIndex;
    private string lastSelectedMelodySignature;
    private float nextQueuedBlockNoteTime;
    private float sessionPlaytimeSeconds;
    private float nextTrailGradientUpdateTime;
    private GradientColorKey[] reusableTrailColorKeys;
    private readonly Gradient reusableTrailGradient = new Gradient();
    private static readonly GradientAlphaKey[] reusableTrailAlphaKeys =
    {
        new GradientAlphaKey(0.9f, 0f),
        new GradientAlphaKey(0f, 1f)
    };
    private static readonly Color[] fallbackNeonColors =
    {
        new Color(0f, 1f, 1f, 1f),
        new Color(1f, 0f, 1f, 1f),
        new Color(0.6f, 0.2f, 1f, 1f),
        new Color(0.2f, 0.5f, 1f, 1f)
    };
    private static readonly int TrailTintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int TrailOutlineWidthId = Shader.PropertyToID("_EdgeWidth");
    private static readonly int TrailOutlineSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int TrailGlowWidthId = Shader.PropertyToID("_GlowWidth");
    private static readonly int TrailGlowAlphaId = Shader.PropertyToID("_GlowAlpha");
    private static readonly int TrailGlowIntensityId = Shader.PropertyToID("_GlowIntensity");
    private static readonly int TrailCenterAlphaId = Shader.PropertyToID("_CenterAlpha");

    [Header("UI Toolkit 상단 UI")]
    [Tooltip("GameMainUI.uxml을 사용하는 UIDocument가 있는 오브젝트에 붙은 컨트롤러")]
    [SerializeField] private GameMainUIController mainUI;
    private GameplayFeedback gameplayFeedback;
    private GameMainUIController subscribedGameplayLayoutUI;
    [SerializeField] [Range(0.8f, 3f)] private float gameplayRuleSnackbarDuration = 1.6f;
    /// <summary>각 스테이지 시작 시 전체 타일 카운트(합). 진행도 계산용.</summary>
    private int initialTileCountForUI;
    private string lastMoveRuleSnackbarId;
    private bool sessionFreeHeartRefillGrantedAt10Minutes;
    private bool sessionFreeHeartRefillGrantedAt20Minutes;
    private bool isApplicationPaused;
    private bool hasApplicationFocus = true;
    public static bool IsPerformanceOverlayOpen { get; private set; }

    /// <summary>CrossBlastTile·LinkSystem 등에서 그리드 크기 참조용.</summary>
    public int StageWidth => stageWidth;
    public int StageHeight => stageHeight;
    public LinkSystem GetLinkSystem() => linkSystem;
    /// <summary>Spotlight 모드: 현재 드래그 중인지.</summary>
    public bool IsDragging => isDragging;
    public bool CanCheckIdleHintBonus => !stageCleared && !isGameOverSequencePlaying && !isDragging && tiles != null && currentStartTile != null && !isHintPreviewRequestRunning;
    public string IdleHintBonusBlockState => $"cleared={stageCleared} gameOver={isGameOverSequencePlaying} dragging={isDragging} hasTiles={tiles != null} hasStart={currentStartTile != null} hintRequest={isHintPreviewRequestRunning}";
    public string LastHintSolverStatus => cachedHintSolverStatus;
    public bool WasLastHintSolverBudgetExhausted => IsHintSolverBudgetExhaustedStatus();
    public int PendingSessionFreeHeartRefillCount => pendingSessionFreeHeartRefillMinutes.Count;
    /// <summary>Spotlight 모드: 포인터(마우스/터치) 월드 좌표.</summary>
    public Vector2 GetPointerWorldPosition()
    {
        if (mainCamera == null) return Vector2.zero;
        Vector2 screen = GetPointerScreenPosition();
        float camZ = mainCamera.transform.position.z;
        Vector3 w = mainCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(camZ)));
        return new Vector2(w.x, w.y);
    }

    /// <summary>Spotlight 모드 컨트롤러. config.mode == "Spotlight"일 때만 사용.</summary>
    private SpotlightController spotlightController;

    private void Awake()
    {
        DeviceOrientationPolicy.ApplyPortrait();
        ConfigureDeviceMaxFrameRate();
    }

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (tilePrefab == null || mainCamera == null)
        {
            Debug.LogError("[GameManager] Tile 프리팹 또는 Main Camera가 할당되지 않았습니다.");
            return;
        }

        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = VisualTheme.IsSoftClay ? VisualTheme.Paper : Color.black;

        EnsureInputAndRaycaster();
        EnsureCameraPostProcessingAndHDR();
        CreateNeonTrail();
        CacheTileSizeFromPrefab();
        InitializeAudioSystem();
        CacheMainUIReference();
        gameplayFeedback = GetComponent<GameplayFeedback>();
        if (gameplayFeedback == null)
            gameplayFeedback = gameObject.AddComponent<GameplayFeedback>();
        gameplayFeedback.Configure(mainCamera, mainUI, EnsureProceduralBackground(), PlayFeedbackHaptic);

        if (FinalEndingProgressStore.TryLoadEndingProgress(out FinalEndingState endingState, out FinalEndingSnapshot endingSnapshot) &&
            endingState == FinalEndingState.PendingResume)
        {
            currentStageIndex = FinalEndingProgressStore.FinalStageIndex;
            if (mainUI != null)
            {
                mainUI.ShowFinalEndingFromResume(endingSnapshot, this);
            }
            else
            {
                Debug.LogWarning("[GameManager] Final ending resume requested, but GameMainUIController was not found. Falling back to stage 1.");
                FinalEndingProgressStore.MarkEndingCompletedReplayable();
                endingState = FinalEndingState.CompletedReplayable;
            }

            if (mainUI != null)
            {
                ConfigureDeviceMaxFrameRate();
                return;
            }
        }

        // 저장된 진행도가 있으면 해당 스테이지부터, 없으면 startStageIndex부터
        currentStageIndex = endingState == FinalEndingState.CompletedReplayable ? 1 : LoadSavedStageIndex();
        hasLoadedStageProgress = true;
        totalStepsCommitted = 0;
        StageData data = StageManager.LoadStage(currentStageIndex);
        UpdateVerboseStage6DebugState(data);
        if (data != null)
            CreateGridFromStageData(data);
        else
            CreateGridFallback();
        SetCurrentStartTileFromStageData(data);
        SetupSpotlight(data);
        AdjustCameraToFitGrid();

        RefreshMainUIForStage();
        RestoreMainUIHeartsForSavedSession();
        SetupMelodyForCurrentStage();
        PlayNewStageSfx();
        if (endingState == FinalEndingState.CompletedReplayable)
            SaveStageProgress();
        PrewarmUpcomingStages();
        IncrementBoardVersion("app_launch");
        NotifySplashStageBootstrapCompleted();
        HandleStageStarted("app_launch");
        ConfigureDeviceMaxFrameRate();
    }

    // 별점 계산용: 이번 스테이지에서 힌트를 봤는지, 실패·리셋했는지, 이번 시도에서 몇 번 손을 뗐는지.
    private bool stageHintUsed;
    private bool stageRetried;
    private int attemptStrokeCount;
    private bool attemptUsedUndo;

    private void HandleStageStarted(string entryType)
    {
        ResetUndoHistory();
        attemptMoves.Clear();
        bool isRestart = entryType == "auto_restart_after_fail" || entryType == "manual_reset";
        if (isRestart)
        {
            stageRetried = true;
        }
        else
        {
            stageHintUsed = false;
            stageRetried = false;
        }
        attemptStrokeCount = 0;
        attemptUsedUndo = false;
        gameplayFeedback?.BeginStage(Mathf.Min(tileWidth, tileHeight));
        SyncBackgroundGridReferenceCameraSize();
        RandomizeBackgroundGridFlow();
        if (ShouldPlayStageStartVisual(entryType))
            PlayStageStartVisuals();
        TrackStageStarted(entryType);
        SchedulePostStageStartedFailureCheck();
        ScheduleEarlyStageGuide();
    }

    // 첫 스테이지들에서는 잠시 멈춰 있으면 실제 정답 경로를 손가락 가이드처럼 자동으로 보여준다(힌트 충전 소모 없음).
    private const int EarlyStageGuideMaxStage = 2;
    private const float EarlyStageGuideIdleSeconds = 1.2f;
    private Coroutine earlyStageGuideRoutine;

    private void ScheduleEarlyStageGuide()
    {
        if (earlyStageGuideRoutine != null)
        {
            StopCoroutine(earlyStageGuideRoutine);
            earlyStageGuideRoutine = null;
        }
        if (currentStageIndex > EarlyStageGuideMaxStage)
            return;
        earlyStageGuideRoutine = StartCoroutine(EarlyStageGuideRoutine(currentStageIndex));
    }

    private IEnumerator EarlyStageGuideRoutine(int stageIndex)
    {
        float idle = 0f;
        while (idle < EarlyStageGuideIdleSeconds)
        {
            yield return null;
            if (stageIndex != currentStageIndex || stageCleared || isGameOverSequencePlaying || totalStepsCommitted > 0)
            {
                earlyStageGuideRoutine = null;
                yield break;
            }

            bool overlayOpen = mainUI != null && mainUI.IsModalOverlayOpen;
            idle = overlayOpen || isDragging || IsPointerHeld() ? 0f : idle + Time.unscaledDeltaTime;
        }

        if (activeHintPreviewLineCount <= 0 && TryGetHintPreviewPath(out List<Tile> path, useQuickBudget: true) && path != null && path.Count > 1)
            RenderHintPreview(path);
        earlyStageGuideRoutine = null;
    }

    private void RandomizeBackgroundGridFlow()
    {
        ProceduralGridBackground background = EnsureProceduralBackground();
        if (background != null)
            background.RandomizeGridFlowDirection(currentStageIndex);
    }

    private bool ShouldPlayStageStartVisual(string entryType)
    {
        return entryType == "app_launch" ||
               entryType == "auto_next_stage" ||
               entryType == "manual_skip" ||
               entryType == "debug_jump" ||
               entryType == "progress_reset";
    }

    private void SyncBackgroundGridReferenceCameraSize()
    {
        ProceduralGridBackground background = EnsureProceduralBackground();
        if (background == null || mainCamera == null || !mainCamera.orthographic)
            return;
        if (tileWidth <= 0f || tileHeight <= 0f)
            return;

        float referenceGridWidth = GetGridWorldSpan(BackgroundGridReferenceColumns, tileWidth);
        float referenceGridHeight = GetGridWorldSpan(BackgroundGridReferenceRows, tileHeight);
        float referenceOrthographicSize = CalculateCameraOrthographicSizeForGrid(referenceGridWidth, referenceGridHeight, GetCameraAspect());
        background.SetGridReferenceOrthographicSize(referenceOrthographicSize);
    }

    private ProceduralGridBackground EnsureProceduralBackground()
    {
        if (proceduralBackground == null)
            proceduralBackground = FindFirstObjectByType<ProceduralGridBackground>();

        return proceduralBackground;
    }

    private void TrackStageStarted(string entryType)
    {
        FinalEndingProgressStore.RecordStageStarted(currentStageIndex);
        AchievementProgressStore.RecordStageStarted(currentStageIndex);
        FirebaseBootstrap.LogEvent("stage_start", new Dictionary<string, object>
        {
            { "stage_index", currentStageIndex },
            { "entry_type", entryType },
            { "active_tiles", CountActiveTileCount() },
            { "remaining_count", GetTotalRemainingCount() }
        });
        FirebaseBootstrap.LogBreadcrumb($"stage_start:{currentStageIndex}:{entryType}");
    }

    private void TrackStageCleared(string clearType)
    {
        FinalEndingProgressStore.RecordStageCleared(currentStageIndex);
        AchievementProgressStore.RecordStageCleared(currentStageIndex);
        FirebaseBootstrap.LogEvent("stage_clear", new Dictionary<string, object>
        {
            { "stage_index", currentStageIndex },
            { "clear_type", clearType },
            { "steps", totalStepsCommitted },
            { "remaining_count", GetTotalRemainingCount() }
        });
        FirebaseBootstrap.LogBreadcrumb($"stage_clear:{currentStageIndex}:{clearType}");
    }

    private void TrackStageFailed(string reason)
    {
        FinalEndingProgressStore.RecordStageFailed(currentStageIndex);
        AchievementProgressStore.RecordStageFailed(currentStageIndex);
        FirebaseBootstrap.LogEvent("stage_fail", new Dictionary<string, object>
        {
            { "stage_index", currentStageIndex },
            { "reason", reason },
            { "steps", totalStepsCommitted },
            { "remaining_count", GetTotalRemainingCount() }
        });
        FirebaseBootstrap.LogBreadcrumb($"stage_fail:{currentStageIndex}:{reason}");
    }

    private void TrackStageReset(string resetType)
    {
        FinalEndingProgressStore.RecordStageReset(currentStageIndex);
        AchievementProgressStore.RecordStageReset(currentStageIndex);
        FirebaseBootstrap.LogEvent("stage_reset", new Dictionary<string, object>
        {
            { "stage_index", currentStageIndex },
            { "reset_type", resetType },
            { "steps", totalStepsCommitted }
        });
        FirebaseBootstrap.LogBreadcrumb($"stage_reset:{currentStageIndex}:{resetType}");
    }

    /// <summary>
    /// 다음 스테이지로 전환. 다음이 없으면 1스테이지로 반복.
    /// </summary>
    public void LoadNextStageImmediateFromUI()
    {
        LoadNextStageImmediate();
    }

    public bool IsOnFinalStage => FinalEndingProgressStore.IsFinalStageIndex(currentStageIndex);

    public bool LoadNextStageImmediate()
    {
        if (isGameOverSequencePlaying)
            return false;
        // 최종 스테이지는 스킵으로 넘길 수 없다. 엔딩은 정상 클리어 경로에서만 열린다.
        if (IsOnFinalStage)
            return false;

        int skippedStageIndex = currentStageIndex;
        PrepareForStageTransition();
        if (!TryAdvanceToNextStage())
            return false;

        FirebaseBootstrap.LogEvent("stage_skip", new Dictionary<string, object>
        {
            { "from_stage_index", skippedStageIndex },
            { "to_stage_index", currentStageIndex }
        });
        HandleStageStarted("manual_skip");
        return true;
    }

    public bool LoadStageImmediateForDebug(int stageIndex)
    {
        if (isGameOverSequencePlaying || stageIndex <= 0)
            return false;

        StageData data = StageManager.LoadStage(stageIndex);
        if (data == null)
            return false;

        int previousStageIndex = currentStageIndex;
        PrepareForStageTransition();
        currentStageIndex = stageIndex;
        ApplyLoadedStageData(data, "debug_stage_jump");

        FirebaseBootstrap.LogEvent("stage_debug_jump", new Dictionary<string, object>
        {
            { "from_stage_index", previousStageIndex },
            { "to_stage_index", currentStageIndex }
        });
        HandleStageStarted("debug_jump");
        return true;
    }

    /// <summary>데이터 초기화 직후 호출: 1스테이지로 즉시 복귀하고 진행도를 1로 저장.</summary>
    public void ResetProgressAndRestartToFirstStage()
    {
        CancelFever();
        if (isGameOverSequencePlaying)
            return;

        int previousStageIndex = currentStageIndex;
        if (pathLitClearRoutine != null)
        {
            StopCoroutine(pathLitClearRoutine);
            pathLitClearRoutine = null;
        }

        ResetTrail();
        ClearPendingBlockNoteQueue();
        linkSystem?.ClearPathLit();
        ClearHintPreview();
        currentPath.Clear();
        isDragging = false;
        stageCleared = false;
        totalStepsCommitted = 0;
        currentStageIndex = 1;

        StageData data = StageManager.LoadStage(1);
        UpdateVerboseStage6DebugState(data);
        ClearTiles();
        if (data != null)
        {
            CreateGridFromStageData(data);
            SetCurrentStartTileFromStageData(data);
            SetupSpotlight(data);
        }
        else
        {
            CreateGridFallback();
            SetCurrentStartTileFromStageData(null);
            SetupSpotlight(null);
        }

        AdjustCameraToFitGrid();
        RefreshMainUIForStage();
        ResetMainUIHeartsForNewStage();
        SetupMelodyForCurrentStage();
        PlayNewStageSfx();
        SaveStageProgress();
        IncrementBoardVersion("progress_reset");
        FirebaseBootstrap.LogEvent("progress_reset", new Dictionary<string, object>
        {
            { "from_stage_index", previousStageIndex },
            { "to_stage_index", currentStageIndex }
        });
        HandleStageStarted("progress_reset");
    }

    public bool ReturnToFirstStageFromFinalEnding()
    {
        if (isGameOverSequencePlaying)
            return false;

        int previousStageIndex = currentStageIndex;
        PrepareForStageTransition();
        currentPath.Clear();
        isDragging = false;
        stageCleared = false;
        totalStepsCommitted = 0;
        currentStageIndex = 1;

        StageData data = StageManager.LoadStage(1);
        if (data == null)
        {
            Debug.LogWarning("[GameManager] Stage 1 not found while returning from final ending.");
            return false;
        }

        ApplyLoadedStageData(data, "final_ending_main", resetHeartsForNewStage: false);
        FirebaseBootstrap.LogEvent("stage1000_credits_main", new Dictionary<string, object>
        {
            { "from_stage_index", previousStageIndex },
            { "to_stage_index", currentStageIndex },
            { "final_stage_index", FinalEndingProgressStore.FinalStageIndex }
        });
        HandleStageStarted("final_ending_main");
        return true;
    }

    private ClayStartMarker clayStartMarker;

    private void LateUpdate()
    {
        if (hintHaloPairs.Count > 0)
            SyncHintHalos();
        UpdateClayFocus();
    }

    /// <summary>점토 테마: 지금 시작할 칸에 외곽선·물결 표시를 붙이고, 짝꿍 연결 표시용 현재 칸을 알린다.</summary>
    private void UpdateClayFocus()
    {
        if (!VisualTheme.IsSoftClay)
            return;
        Tile head = isDragging && currentPath.Count > 0 ? currentPath[currentPath.Count - 1] : currentStartTile;
        VisualTheme.FocusTile = stageCleared || isGameOverSequencePlaying ? null : head;

        if (clayStartMarker == null)
            clayStartMarker = ClayStartMarker.Create(transform);
        bool idle = !isDragging && !stageCleared && !isGameOverSequencePlaying;
        clayStartMarker.SetTarget(idle ? currentStartTile : null);
    }

    private void Update()
    {
        UpdateAdaptiveFrameRate();
        CacheMainUIReference();
        bool overlayOpen = mainUI != null && mainUI.IsModalOverlayOpen;
        IsPerformanceOverlayOpen = overlayOpen;
        gameplayFeedback?.SetSuspended(overlayOpen);

        ProcessPendingBlockNoteQueue();
        UpdateSessionHeartRefillProgress(overlayOpen);
        UpdateFeverTimer(overlayOpen);
        if (stageCleared || isGameOverSequencePlaying)
            return;

        CaptureUndoSnapshotIfNeeded();

        if (overlayOpen)
        {
            ClearHintPreview();
            if (isDragging)
            {
                isDragging = false;
                currentPath.Clear();
                ResetTrail();
                linkSystem?.ClearPathLit();
                gameplayFeedback?.ClearPath();
            }
            return;
        }

        UpdateDragAndPath();
    }

    public bool TryPeekSessionFreeHeartRefill(out int thresholdMinutes)
    {
        if (pendingSessionFreeHeartRefillMinutes.Count > 0)
        {
            thresholdMinutes = pendingSessionFreeHeartRefillMinutes.Peek();
            return true;
        }

        thresholdMinutes = 0;
        return false;
    }

    public bool TryConsumeSessionFreeHeartRefill(out int thresholdMinutes)
    {
        if (pendingSessionFreeHeartRefillMinutes.Count > 0)
        {
            thresholdMinutes = pendingSessionFreeHeartRefillMinutes.Dequeue();
            return true;
        }

        thresholdMinutes = 0;
        return false;
    }

    private void UpdateSessionHeartRefillProgress(bool overlayOpen)
    {
        if (overlayOpen || stageCleared || isGameOverSequencePlaying || isApplicationPaused || !hasApplicationFocus)
            return;

        sessionPlaytimeSeconds += Time.unscaledDeltaTime;
        TryGrantSessionFreeHeartRefill(SessionFreeHeartRefillFirstThresholdSeconds, 10, ref sessionFreeHeartRefillGrantedAt10Minutes);
        TryGrantSessionFreeHeartRefill(SessionFreeHeartRefillSecondThresholdSeconds, 20, ref sessionFreeHeartRefillGrantedAt20Minutes);
    }

    private void TryGrantSessionFreeHeartRefill(float thresholdSeconds, int thresholdMinutes, ref bool alreadyGranted)
    {
        if (alreadyGranted || sessionPlaytimeSeconds < thresholdSeconds)
            return;

        alreadyGranted = true;
        pendingSessionFreeHeartRefillMinutes.Enqueue(thresholdMinutes);
        FirebaseBootstrap.LogEvent("session_free_heart_refill_granted", new Dictionary<string, object>
        {
            { "threshold_minutes", thresholdMinutes },
            { "session_play_seconds", Mathf.FloorToInt(sessionPlaytimeSeconds) },
            { "pending_free_refills", pendingSessionFreeHeartRefillMinutes.Count }
        });
        FirebaseBootstrap.LogBreadcrumb($"session_free_heart_refill_granted:{thresholdMinutes}m");
    }

    private void InitializeAudioSystem()
    {
        EnsureAudioSource(ref blockNoteAudioSource, "BlockNoteAudioSource");
        EnsureAudioSource(ref eventAudioSource, "EventSfxAudioSource");
        ResolveRuntimeAudioClips();
        BuildBlockNoteClipMap();
        BuildMelodyPoolsIfNeeded();
    }

    /// <summary>
    /// 씬에 저장된 참조가 없어도 기기 빌드에서 소리가 나도록 Resources에서 효과음을 불러온다.
    /// (에디터 OnValidate 자동 연결은 씬을 저장하지 않으면 빌드에 반영되지 않는다.)
    /// </summary>
    private void ResolveRuntimeAudioClips()
    {
        blockAClip = LoadClipIfMissing(blockAClip, "block_a");
        blockAsClip = LoadClipIfMissing(blockAsClip, "block_as");
        blockBClip = LoadClipIfMissing(blockBClip, "block_b");
        blockCClip = LoadClipIfMissing(blockCClip, "block_c");
        blockCoClip = LoadClipIfMissing(blockCoClip, "block_co");
        blockCsClip = LoadClipIfMissing(blockCsClip, "block_cs");
        blockDClip = LoadClipIfMissing(blockDClip, "block_d");
        blockDsClip = LoadClipIfMissing(blockDsClip, "block_ds");
        blockEClip = LoadClipIfMissing(blockEClip, "block_e");
        blockFClip = LoadClipIfMissing(blockFClip, "block_f");
        blockFsClip = LoadClipIfMissing(blockFsClip, "block_fs");
        blockGClip = LoadClipIfMissing(blockGClip, "block_g");
        blockGsClip = LoadClipIfMissing(blockGsClip, "block_gs");
        failClip = LoadClipIfMissing(failClip, "fail");
        newStageClip = LoadClipIfMissing(newStageClip, "new_stage");
        clearClip = LoadClipIfMissing(clearClip, "clear");
        bombClip = LoadClipIfMissing(bombClip, "bomb");
    }

    private static AudioClip LoadClipIfMissing(AudioClip current, string clipName)
    {
        if (current != null)
            return current;
        AudioClip loaded = Resources.Load<AudioClip>("Sounds/" + clipName);
        if (loaded == null)
            Debug.LogWarning($"[GameManager] Missing sound clip: Resources/Sounds/{clipName}");
        return loaded;
    }

    private void EnsureAudioSource(ref AudioSource audioSource, string childObjectName)
    {
        if (audioSource == null)
        {
            Transform existing = transform.Find(childObjectName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childObjectName);
            if (existing == null)
                child.transform.SetParent(transform, false);
            audioSource = child.GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = child.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.dopplerLevel = 0f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
    }

    private void BuildBlockNoteClipMap()
    {
        blockNoteClipMap.Clear();
        RegisterBlockNoteClip("a", blockAClip);
        RegisterBlockNoteClip("as", blockAsClip);
        RegisterBlockNoteClip("b", blockBClip);
        RegisterBlockNoteClip("c", blockCClip);
        RegisterBlockNoteClip("co", blockCoClip);
        RegisterBlockNoteClip("cs", blockCsClip);
        RegisterBlockNoteClip("d", blockDClip);
        RegisterBlockNoteClip("ds", blockDsClip);
        RegisterBlockNoteClip("e", blockEClip);
        RegisterBlockNoteClip("f", blockFClip);
        RegisterBlockNoteClip("fs", blockFsClip);
        RegisterBlockNoteClip("g", blockGClip);
        RegisterBlockNoteClip("gs", blockGsClip);
    }

    private void RegisterBlockNoteClip(string noteKey, AudioClip clip)
    {
        if (!string.IsNullOrEmpty(noteKey) && clip != null)
            blockNoteClipMap[noteKey] = clip;
    }

    private void BuildMelodyPoolsIfNeeded()
    {
        if (sparseMelodyPool.Count > 0 || mediumMelodyPool.Count > 0 || denseMelodyPool.Count > 0)
            return;

        sparseMelodyPool.Add(new[] { "c", "d", "e", "c", "e", "g", "e" }); // Airplane-like
        sparseMelodyPool.Add(new[] { "c", "c", "g", "g", "a", "a", "g" }); // Twinkle-like
        sparseMelodyPool.Add(new[] { "g", "g", "a", "a", "g", "g", "e" }); // School bell-like
        sparseMelodyPool.Add(new[] { "g", "e", "e", "f", "d", "d", "c" }); // Butterfly-like
        sparseMelodyPool.Add(new[] { "c", "e", "g", "e", "c", "d", "e" }); // Tiny march
        sparseMelodyPool.Add(new[] { "c", "d", "e", "d", "c", "e", "g" }); // Chick steps
        sparseMelodyPool.Add(new[] { "c", "g", "e", "g", "c", "g", "e" }); // Water drops
        sparseMelodyPool.Add(new[] { "c", "e", "g", "co", "g", "e", "c" }); // Music box
        sparseMelodyPool.Add(new[] { "e", "g", "a", "g", "e", "d", "c" }); // Forest walk
        sparseMelodyPool.Add(new[] { "c", "d", "e", "g", "co", "g", "e" }); // Short clear
        sparseMelodyPool.Add(new[] { "d", "e", "f", "d", "f", "a", "f" }); // Small lantern
        sparseMelodyPool.Add(new[] { "e", "f", "g", "e", "g", "b", "g" }); // Small bell
        sparseMelodyPool.Add(new[] { "c", "e", "f", "g", "f", "e", "c" }); // Soft arc
        sparseMelodyPool.Add(new[] { "g", "f", "e", "d", "c", "d", "e" }); // Falling steps
        sparseMelodyPool.Add(new[] { "a", "g", "e", "c", "e", "g", "a" }); // Bright hop
        sparseMelodyPool.Add(new[] { "c", "cs", "d", "e", "d", "cs", "c" }); // Tiny chromatic
        sparseMelodyPool.Add(new[] { "e", "ds", "e", "g", "e", "d", "c" }); // Little twist
        sparseMelodyPool.Add(new[] { "f", "g", "a", "f", "e", "d", "c" }); // Meadow step
        sparseMelodyPool.Add(new[] { "c", "g", "a", "g", "e", "d", "c" }); // Round drop
        sparseMelodyPool.Add(new[] { "d", "f", "a", "f", "d", "e", "f" }); // Toy flute
        sparseMelodyPool.Add(new[] { "g", "b", "co", "b", "g", "e", "d" }); // High sparkle
        sparseMelodyPool.Add(new[] { "c", "e", "d", "f", "e", "g", "f" }); // Zigzag
        sparseMelodyPool.Add(new[] { "a", "c", "e", "c", "a", "b", "c" }); // Cat steps
        sparseMelodyPool.Add(new[] { "e", "g", "co", "g", "e", "f", "g" }); // Lift
        sparseMelodyPool.Add(new[] { "c", "d", "f", "e", "c", "g", "e" }); // Side street
        sparseMelodyPool.Add(new[] { "g", "e", "c", "d", "e", "c", "g" }); // Echo
        sparseMelodyPool.Add(new[] { "f", "a", "g", "e", "d", "c", "d" }); // Warm fall
        sparseMelodyPool.Add(new[] { "c", "e", "g", "a", "g", "e", "c" }); // Small fanfare
        sparseMelodyPool.Add(new[] { "d", "e", "g", "e", "d", "c", "d" }); // Soft return
        sparseMelodyPool.Add(new[] { "c", "co", "g", "e", "c", "d", "c" }); // Tiny finale

        mediumMelodyPool.Add(new[] { "c", "d", "e", "c", "e", "g", "e", "d", "c", "e", "g" }); // Airplane variation
        mediumMelodyPool.Add(new[] { "c", "c", "g", "g", "a", "a", "g", "f", "f", "e", "e", "d" }); // Twinkle variation
        mediumMelodyPool.Add(new[] { "g", "a", "g", "f", "e", "f", "g", "d", "e", "f", "e" }); // Bridge-like
        mediumMelodyPool.Add(new[] { "e", "d", "c", "d", "e", "e", "e", "d", "d", "d", "e", "g", "g" }); // Nursery march
        mediumMelodyPool.Add(new[] { "c", "e", "g", "a", "g", "e", "d", "c", "d", "e" }); // Round moon
        mediumMelodyPool.Add(new[] { "c", "e", "g", "c", "d", "f", "a", "d", "e", "g" }); // Toy march
        mediumMelodyPool.Add(new[] { "f", "a", "co", "a", "f", "d", "f", "a", "g", "e", "d" }); // Carousel
        mediumMelodyPool.Add(new[] { "a", "c", "e", "c", "a", "b", "c", "e", "d", "c" }); // Cat steps
        mediumMelodyPool.Add(new[] { "c", "d", "e", "g", "a", "g", "e", "d", "c", "e" }); // Fairy village
        mediumMelodyPool.Add(new[] { "g", "a", "b", "co", "b", "a", "g", "e", "d", "c" }); // Bright stage
        mediumMelodyPool.Add(new[] { "c", "e", "f", "g", "a", "g", "f", "e", "d", "c" }); // Neon hill
        mediumMelodyPool.Add(new[] { "d", "f", "g", "a", "g", "f", "d", "e", "f", "d" }); // Firefly
        mediumMelodyPool.Add(new[] { "e", "g", "a", "b", "a", "g", "e", "f", "g", "e" }); // Glass stairs
        mediumMelodyPool.Add(new[] { "c", "cs", "d", "ds", "e", "f", "e", "d", "c" }); // Clock climb
        mediumMelodyPool.Add(new[] { "e", "ds", "e", "f", "g", "f", "e", "d", "c", "e" }); // Curious turn
        mediumMelodyPool.Add(new[] { "g", "e", "c", "e", "g", "a", "g", "e", "d", "c" }); // Open window
        mediumMelodyPool.Add(new[] { "a", "g", "f", "e", "d", "f", "a", "g", "e", "d" }); // Warm breeze
        mediumMelodyPool.Add(new[] { "c", "e", "g", "co", "b", "g", "e", "d", "c" }); // Music box climb
        mediumMelodyPool.Add(new[] { "d", "e", "f", "a", "g", "f", "e", "d", "c", "d" }); // Boardwalk
        mediumMelodyPool.Add(new[] { "f", "e", "d", "c", "d", "f", "a", "f", "d", "c" }); // Lantern fall
        mediumMelodyPool.Add(new[] { "c", "g", "c", "e", "g", "e", "c", "d", "e" }); // Pulse loop
        mediumMelodyPool.Add(new[] { "e", "g", "e", "c", "e", "g", "a", "g", "e" }); // Soft shimmer
        mediumMelodyPool.Add(new[] { "g", "b", "co", "b", "g", "f", "e", "d", "c" }); // High return
        mediumMelodyPool.Add(new[] { "a", "as", "b", "co", "b", "as", "a", "g", "e" }); // Prism turn
        mediumMelodyPool.Add(new[] { "c", "d", "e", "f", "g", "a", "g", "f", "e", "d" }); // Ascending rail
        mediumMelodyPool.Add(new[] { "c", "e", "ds", "e", "g", "e", "ds", "c", "d" }); // Spark flicker
        mediumMelodyPool.Add(new[] { "f", "g", "a", "co", "a", "g", "f", "e", "d" }); // Horizon
        mediumMelodyPool.Add(new[] { "d", "fs", "a", "fs", "d", "e", "fs", "g", "a" }); // Clear metal
        mediumMelodyPool.Add(new[] { "c", "e", "g", "b", "co", "b", "g", "e", "c" }); // Tall arch
        mediumMelodyPool.Add(new[] { "g", "fs", "g", "a", "b", "a", "g", "e", "d", "c" }); // Festival turn

        denseMelodyPool.Add(new[] { "c", "d", "e", "c", "e", "g", "e", "d", "c", "d", "e", "g", "a", "g", "e" }); // Airplane loop
        denseMelodyPool.Add(new[] { "c", "c", "g", "g", "a", "a", "g", "f", "f", "e", "e", "d", "d", "c" }); // Twinkle long
        denseMelodyPool.Add(new[] { "d", "a", "b", "fs", "g", "d", "g", "a", "d", "fs", "g", "a", "b", "a", "g" }); // Canon-like
        denseMelodyPool.Add(new[] { "e", "e", "fs", "g", "g", "fs", "e", "d", "c", "d", "e", "fs", "g", "a", "g", "e" }); // Four seasons-like
        denseMelodyPool.Add(new[] { "e", "e", "f", "g", "g", "f", "e", "d", "c", "c", "d", "e", "d", "c", "c" }); // Ode-like
        denseMelodyPool.Add(new[] { "e", "ds", "e", "ds", "e", "b", "d", "c", "a", "c", "e", "a", "b", "e" }); // Elise-like
        denseMelodyPool.Add(new[] { "a", "c", "ds", "e", "ds", "c", "a", "b", "c", "e", "g", "e", "ds", "c" }); // Mystery nursery
        denseMelodyPool.Add(new[] { "c", "cs", "d", "ds", "e", "f", "fs", "g", "gs", "a", "as", "b", "co", "b" }); // Boss climb
        denseMelodyPool.Add(new[] { "c", "d", "e", "g", "a", "b", "co", "b", "a", "g", "e", "d", "c", "e", "g", "co" }); // Puzzle finale
        denseMelodyPool.Add(new[] { "d", "fs", "a", "co", "b", "a", "g", "fs", "e", "d", "fs", "a", "g", "e", "d" }); // Ending festival
        denseMelodyPool.Add(new[] { "c", "e", "g", "co", "g", "e", "c", "d", "f", "a", "co", "a", "f", "d" }); // Wide music box
        denseMelodyPool.Add(new[] { "g", "a", "b", "co", "b", "a", "g", "f", "e", "d", "c", "d", "e", "g" }); // Long return
        denseMelodyPool.Add(new[] { "a", "g", "e", "c", "e", "g", "a", "b", "co", "b", "a", "g", "e", "c" }); // Bright valley
        denseMelodyPool.Add(new[] { "c", "e", "f", "g", "a", "co", "a", "g", "f", "e", "d", "c", "e", "g" }); // Crystal ridge
        denseMelodyPool.Add(new[] { "d", "e", "f", "g", "a", "g", "f", "e", "d", "c", "d", "f", "a", "g" }); // Rolling march
        denseMelodyPool.Add(new[] { "e", "g", "b", "co", "b", "g", "e", "f", "a", "co", "a", "f", "e" }); // Night lift
        denseMelodyPool.Add(new[] { "c", "d", "cs", "d", "e", "f", "e", "ds", "e", "g", "e", "c" }); // Circuit tick
        denseMelodyPool.Add(new[] { "f", "a", "co", "b", "a", "g", "f", "e", "d", "f", "a", "g", "f", "d" }); // Lantern bridge
        denseMelodyPool.Add(new[] { "g", "b", "co", "b", "g", "e", "d", "c", "e", "g", "a", "g", "e" }); // High echo
        denseMelodyPool.Add(new[] { "a", "c", "e", "g", "e", "c", "a", "b", "c", "e", "d", "c", "a" }); // Cat carousel
        denseMelodyPool.Add(new[] { "c", "co", "b", "a", "g", "e", "c", "d", "e", "g", "co", "g", "e" }); // Tall finale
        denseMelodyPool.Add(new[] { "d", "f", "a", "co", "a", "f", "d", "e", "g", "b", "g", "e", "d" }); // Prism stairs
        denseMelodyPool.Add(new[] { "e", "f", "fs", "g", "a", "b", "co", "b", "a", "g", "fs", "e", "d" }); // Gear climb
        denseMelodyPool.Add(new[] { "c", "e", "g", "e", "c", "g", "e", "c", "d", "f", "a", "f", "d" }); // Double echo
        denseMelodyPool.Add(new[] { "g", "e", "c", "e", "g", "b", "co", "b", "g", "e", "d", "c", "d" }); // Gate open
        denseMelodyPool.Add(new[] { "a", "as", "b", "co", "b", "a", "g", "fs", "g", "a", "b", "a", "g" }); // Neon prism
        denseMelodyPool.Add(new[] { "c", "d", "e", "f", "g", "a", "b", "co", "b", "a", "g", "f", "e", "d" }); // Full ladder
        denseMelodyPool.Add(new[] { "e", "g", "a", "b", "co", "b", "a", "g", "e", "f", "g", "a", "g", "e" }); // Star trail
        denseMelodyPool.Add(new[] { "d", "e", "g", "b", "co", "b", "g", "e", "d", "fs", "a", "g", "e" }); // Brass puzzle
        denseMelodyPool.Add(new[] { "c", "e", "g", "a", "co", "b", "g", "e", "f", "a", "co", "a", "g", "e", "c" }); // Last sparkle
    }

    private void SetupMelodyForCurrentStage()
    {
        BuildBlockNoteClipMap();
        BuildMelodyPoolsIfNeeded();

        int activeTileCount = CountActiveTileCount();
        int totalRemainingCount = GetTotalRemainingCount();

        List<string[]> selectedPool;
        if (activeTileCount <= 6 || totalRemainingCount <= 12)
            selectedPool = sparseMelodyPool;
        else if (activeTileCount >= 20 || totalRemainingCount >= 60)
            selectedPool = denseMelodyPool;
        else
            selectedPool = mediumMelodyPool;

        if (selectedPool == null || selectedPool.Count == 0)
        {
            activeMelody = Array.Empty<string>();
            activeMelodyIndex = 0;
            ClearPendingBlockNoteQueue();
            return;
        }

        int seed = Mathf.Abs((currentStageIndex * 92821) ^ (activeTileCount * 68917) ^ (totalRemainingCount * 31337));
        int melodyIndex = seed % selectedPool.Count;
        string selectedMelodySignature = null;
        for (int attempts = 0; attempts < selectedPool.Count; attempts++)
        {
            selectedMelodySignature = GetMelodySignature(selectedPool[melodyIndex]);
            if (selectedPool.Count == 1 || !string.Equals(selectedMelodySignature, lastSelectedMelodySignature, StringComparison.Ordinal))
                break;

            melodyIndex = (melodyIndex + 1) % selectedPool.Count;
        }

        activeMelody = selectedPool[melodyIndex];
        lastSelectedMelodySignature = selectedMelodySignature ?? GetMelodySignature(activeMelody);
        activeMelodyIndex = 0;
        ClearPendingBlockNoteQueue();
        ResetStrokeCombo();
    }

    private static string GetMelodySignature(string[] melody)
    {
        return melody == null || melody.Length == 0 ? string.Empty : string.Join(",", melody);
    }

    private int CountActiveTileCount()
    {
        if (tiles == null) return 0;
        int count = 0;
        for (int row = 0; row < stageHeight; row++)
            for (int col = 0; col < stageWidth; col++)
                if (tiles[row, col] != null && tiles[row, col].IsActive)
                    count++;
        return count;
    }

    // 콤보: 한 획(누르고 뗄 때까지, 뗀 직후 마지막 칸 소비 포함) 안에서 0이 되어 터진 칸 수.
    // 터질 때마다 멜로디를 반음씩 올려(최대 완전5도) 긴 한붓일수록 점점 신나게 들리게 한다.
    private int strokeComboPops;
    private const int ComboMaxSemitones = 7;

    private void RegisterComboPop(Tile tile)
    {
        strokeComboPops++;
        if (feverActive)
        {
            feverPops++;
            mainUI?.UpdateFeverHud(FeverSecondsLeft, feverPops, true);
        }
        if (blockNoteAudioSource != null)
            blockNoteAudioSource.pitch = Mathf.Pow(2f, Mathf.Min(strokeComboPops - 1, ComboMaxSemitones) / 12f);
        gameplayFeedback?.ComboPop(tile, strokeComboPops);
    }

    private void ResetStrokeCombo()
    {
        strokeComboPops = 0;
        if (blockNoteAudioSource != null)
            blockNoteAudioSource.pitch = 1f;
    }

    private void QueueNextMelodyBlockNote()
    {
        if (activeMelody == null || activeMelody.Length == 0)
            return;

        string nextNoteKey = activeMelody[activeMelodyIndex];
        activeMelodyIndex = (activeMelodyIndex + 1) % activeMelody.Length;

        if (blockNoteClipMap.TryGetValue(nextNoteKey, out AudioClip clip) && clip != null)
            pendingBlockNoteQueue.Enqueue(clip);
    }

    private void ProcessPendingBlockNoteQueue()
    {
        if (blockNoteAudioSource == null || pendingBlockNoteQueue.Count == 0)
            return;
        if (Time.unscaledTime < nextQueuedBlockNoteTime)
            return;

        while (pendingBlockNoteQueue.Count > 0)
        {
            AudioClip clip = pendingBlockNoteQueue.Dequeue();
            if (clip == null)
                continue;
            blockNoteAudioSource.PlayOneShot(clip, blockNoteVolume);
            nextQueuedBlockNoteTime = Time.unscaledTime + Mathf.Max(0.01f, blockNoteInterval);
            return;
        }
    }

    private void ClearPendingBlockNoteQueue()
    {
        pendingBlockNoteQueue.Clear();
        nextQueuedBlockNoteTime = 0f;
    }

    private void PlayEventSfx(AudioClip clip)
    {
        if (clip == null || eventAudioSource == null)
            return;
        eventAudioSource.PlayOneShot(clip, eventSfxVolume);
    }

    private void PlayEventSfx(AudioClip clip, float volumeMultiplier)
    {
        if (clip == null || eventAudioSource == null)
            return;
        eventAudioSource.PlayOneShot(clip, Mathf.Clamp01(eventSfxVolume * volumeMultiplier));
    }

    private void PlayFailSfx() => PlayEventSfx(failClip);
    private void PlayBombSfx() => PlayEventSfx(bombClip, bombSfxVolumeMultiplier);
    private void PlayNewStageSfx()
    {
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI != null && mainUI.IsSplashActive)
            return;
        PlayEventSfx(newStageClip);
    }
    private void PlayClearSfx() => PlayEventSfx(clearClip);
    private void PlayStageClearHaptic()
    {
        if (!GameMainUIController.IsVibrationEnabled)
            return;
        if (!Application.isMobilePlatform)
            return;
        // 예전 값(20ms, 세기 30/255)은 거의 느껴지지 않아 클리어를 가장 강한 피드백으로 올린다.
        PlayFeedbackHaptic(1f);
    }

    private void PlayFeedbackHaptic(float strength)
    {
        if (!GameMainUIController.IsVibrationEnabled || !Application.isMobilePlatform)
            return;
#if UNITY_ANDROID && !UNITY_EDITOR
        float s = Mathf.Clamp01(strength);
        PlayAndroidOneShotVibration(Mathf.RoundToInt(Mathf.Lerp(10f, 45f, s)), Mathf.RoundToInt(Mathf.Lerp(45f, 220f, s)));
#elif UNITY_IOS && !UNITY_EDITOR
        PlayIOSFeedbackImpact(Mathf.Clamp01(strength));
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal", EntryPoint = "ZeroStepFeedbackImpact")]
    private static extern void PlayIOSFeedbackImpact(float strength);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    // 이동마다 호출될 수 있으므로 JNI 객체는 한 번만 만들어 재사용한다.
    private AndroidJavaObject cachedAndroidVibrator;
    private AndroidJavaClass cachedVibrationEffectClass;
    private int cachedAndroidSdkInt = -1;
    private bool androidVibratorUnavailable;

    private void PlayAndroidOneShotVibration(int durationMs, int amplitude)
    {
        if (androidVibratorUnavailable)
            return;

        int safeDurationMs = Mathf.Clamp(durationMs, 1, 1000);
        int safeAmplitude = Mathf.Clamp(amplitude, 1, 255);
        try
        {
            if (cachedAndroidVibrator == null)
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaClass contextClass = new AndroidJavaClass("android.content.Context"))
                using (AndroidJavaClass versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    string vibratorService = contextClass.GetStatic<string>("VIBRATOR_SERVICE");
                    cachedAndroidVibrator = activity.Call<AndroidJavaObject>("getSystemService", vibratorService);
                    cachedAndroidSdkInt = versionClass.GetStatic<int>("SDK_INT");
                }

                if (cachedAndroidVibrator == null || !cachedAndroidVibrator.Call<bool>("hasVibrator"))
                {
                    androidVibratorUnavailable = true;
                    return;
                }

                if (cachedAndroidSdkInt >= 26)
                    cachedVibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
            }

            if (cachedAndroidSdkInt >= 26 && cachedVibrationEffectClass != null)
            {
                using (AndroidJavaObject oneShotEffect = cachedVibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)safeDurationMs, safeAmplitude))
                {
                    cachedAndroidVibrator.Call("vibrate", oneShotEffect);
                }
            }
            else
            {
                cachedAndroidVibrator.Call("vibrate", (long)safeDurationMs);
            }
        }
        catch (Exception ex)
        {
            androidVibratorUnavailable = true;
            Debug.LogWarning($"[GameManager] Haptic unavailable: {ex.Message}");
        }
    }
#endif

    private void DecreaseTileAndPlayBlockNote(Tile tile)
    {
        if (tile == null)
            return;
        int before = tile.CurrentNumber;
        tile.DecreaseNumber();
        if (before > tile.CurrentNumber)
        {
            gameplayFeedback?.TileDecreased(tile, before);
            if (tile.CurrentNumber == 0)
                RegisterComboPop(tile);
            var fixedKnot = tile.GetComponent<FixedKnotTile>();
            if (fixedKnot != null)
                fixedKnot.OnCountDecreased();
            QueueNextMelodyBlockNote();
            IncrementBoardVersion("tile_count_changed");
        }
    }

    /// <summary>
    /// 드래그 입력 처리: 누르면 경로 시작, 이동 시 인접 타일만 추가, 떼면 경로 적용 후 라인 갱신 및 승리 체크.
    /// </summary>
    private void UpdateDragAndPath()
    {
        Vector2 screenPoint = GetPointerScreenPosition();
        bool pointerDown = IsPointerDown();
        bool pointerUp = IsPointerUp();
        bool pointerHeld = IsPointerHeld();
        if (pointerDown || pointerHeld || pointerUp)
            NotifyMainUIPlayerActivity();

        if (pointerDown)
        {
            Tile hit = GetTileAtScreen(screenPoint);
            if (hit != currentStartTile || !CanEnterTile(hit))
                return;
            ClearMoveRuleSnackbarState();
            BeginFollowingHintPreview();
            if (ShouldLogVerboseStage6Debug())
            {
                Vector2 pointerWorld = ScreenToWorld2D(screenPoint);
                DevLog.Log($"[Stage6 드래그 시작] screen=({screenPoint.x:F1},{screenPoint.y:F1}) world=({pointerWorld.x:F3},{pointerWorld.y:F3}) hit={DescribeTileForDebug(hit)} currentStart={DescribeTileForDebug(currentStartTile)}");
            }
            currentStartTile.ClearScaleOverride();
            ResetStrokeCombo();
            isDragging = true;
            currentPath.Clear();
            currentPath.Add(hit);
            lastStepFrame = Time.frameCount;
            lastStepTime = Time.unscaledTime;
            NotifyTrailTileStepped(hit);
            OnDragStartTrail();
            gameplayFeedback?.DragStarted(hit);
            gameplayFeedback?.TrackFinger(ScreenToWorld2D(screenPoint));
            gameplayFeedback?.UpdatePath(currentPath);
            // 시작점 터치만으로는 기어 숫자 갱신 안 함 — 타일을 실제로 옮겼을 때만 -1
        }
        // pointerUp일 때는 타일 추가하지 않음 — 손 뗀 위치로 잘못 판정되어 터치만 해도 DecreaseNumber 되는 버그 방지
        else if (isDragging && pointerHeld)
        {
            // 파괴되거나 소진된 타일 참조 제거
            currentPath.RemoveAll(t => t == null);
            if (currentPath.Count == 0)
            {
                ClearMoveRuleSnackbarState();
                isDragging = false;
                return;
            }
            Tile lastForHit = currentPath[currentPath.Count - 1];
            Tile previousTile = currentPath.Count >= 2 ? currentPath[currentPath.Count - 2] : null;
            Vector2 pointerWorld = ScreenToWorld2D(screenPoint);
            gameplayFeedback?.TrackFinger(pointerWorld);
            Tile directHit = GetTileAtScreen(screenPoint);
            Tile hit = GetTileAtScreen(screenPoint, preferAdjacentTo: lastForHit);
            if (ShouldLogVerboseStage6Debug() && (directHit != hit || hit == null || hit == previousTile))
            {
                DevLog.Log($"[Stage6 입력 판정] screen=({screenPoint.x:F1},{screenPoint.y:F1}) world=({pointerWorld.x:F3},{pointerWorld.y:F3}) last={DescribeTileForDebug(lastForHit)} prev={DescribeTileForDebug(previousTile)} direct={DescribeTileForDebug(directHit)} picked={DescribeTileForDebug(hit)} path={DescribeCurrentPathForDebug()} candidates={DescribeTilePickCandidates(screenPoint, lastForHit)}");
            }
            // 숫자가 남아 있으면 이미 라인이 그려진 타일이라도 재방문(중복 밟기) 허용.
            // 숫자는 '들어갈 때'가 아니라 '지나쳐 나갈 때' 감소 → 멈춘 타일이 0이 되어 다음 드래그를 못 시작하는 문제 방지.
            if (CanEnterTile(hit))
            {
                Tile last = currentPath[currentPath.Count - 1];
                if (last == null)
                {
                    ClearMoveRuleSnackbarState();
                    isDragging = false;
                    currentPath.Clear();
                    return;
                }
                // 같은 타일 위에서만 막기(hit==last). 이미 경로에 있어도 다른 타일에서 인접 진입(재진입)은 허용 — ShortCircuit도 한 드래그에서 여러 번 밟기 가능(count 2→1→0)
                bool canStep = hit != last;
                // 직선 드래그 시 인접 타일이 오락가락하며 되돌아가기만 반복되는 지터 무시 (한 번에 -2 되는 버그 방지)
                if (canStep && currentPath.Count >= 2 && hit == currentPath[currentPath.Count - 2])
                {
                    int frameDelta = Time.frameCount - lastStepFrame;
                    bool pointerDirectlyOnBackTile = directHit == hit;
                    float backDist = Vector2.Distance(pointerWorld, (Vector2)hit.transform.position);
                    float currentDist = Vector2.Distance(pointerWorld, (Vector2)last.transform.position);
                    float tileSize = Mathf.Max(0.01f, Mathf.Min(tileWidth, tileHeight));
                    float backtrackMargin = tileSize * BacktrackDistanceMarginRatio;
                    bool stronglyInsideBackTile = (backDist + backtrackMargin) < currentDist;
                    bool withinJitterFrames = lastStepTime >= 0f && Time.unscaledTime - lastStepTime <= ImmediateBacktrackIgnoreSeconds;
                    if (!pointerDirectlyOnBackTile || withinJitterFrames || !stronglyInsideBackTile)
                    {
                        string reason = !pointerDirectlyOnBackTile
                            ? "포인터가 되돌아간 타일 위에 직접 올라오지 않음"
                            : (withinJitterFrames
                                ? "직선 드래그 오락가락 방지"
                                : $"되돌아간 타일 안쪽 진입 부족(backDist={backDist:F3}, currentDist={currentDist:F3}, margin={backtrackMargin:F3})");
                        if (ShouldLogVerboseStage6Debug())
                        {
                            DevLog.Log($"[지터 무시] 되돌아감 last=({last.X},{last.Y}) hit=({hit.X},{hit.Y}) frameDelta={frameDelta} ({reason}) direct={DescribeTileForDebug(directHit)} world=({pointerWorld.x:F3},{pointerWorld.y:F3}) path={DescribeCurrentPathForDebug()} candidates={DescribeTilePickCandidates(screenPoint, last)}");
                        }
                        else
                        {
                            DevLog.Log($"[지터 무시] 되돌아감 last=({last.X},{last.Y}) hit=({hit.X},{hit.Y}) frameDelta={frameDelta} ({reason})");
                        }
                        canStep = false;
                    }
                }
                if (canStep)
                    TryStepToTile(last, hit);
            }
            else if (ShouldLogVerboseStage6Debug())
            {
                DevLog.Log($"[Stage6 입력 후보 무효] last={DescribeTileForDebug(lastForHit)} prev={DescribeTileForDebug(previousTile)} direct={DescribeTileForDebug(directHit)} picked={DescribeTileForDebug(hit)} world=({pointerWorld.x:F3},{pointerWorld.y:F3}) path={DescribeCurrentPathForDebug()} candidates={DescribeTilePickCandidates(screenPoint, lastForHit)}");
            }
        }

        // 손 뗄 때는 항상 커밋(터치만 해도 하트비트·라인 갱신). 타일 추가는 pointerHeld일 때만 해서 터치만 할 때 DecreaseNumber 방지.
        if (pointerUp)
        {
            gameplayFeedback?.DragReleased(currentPath.Count > 0 ? currentPath[currentPath.Count - 1] : currentStartTile);
            gameplayFeedback?.ReleasePath();
            ClearMoveRuleSnackbarState();
            isDragging = false;
            ResetTrail();
            CommitPathAndSetCurrentPosition();
            CheckVictoryCondition(currentStartTile);
            // 클리어를 먼저 검사. 모든 타일 0이면 데드락 검사와 겹치므로 클리어 우선 (그렇지 않으면 게임오버로 잘못 처리됨)
            CheckStageClear();
            if (!stageCleared && CheckAndHandleBoardFailure(currentStartTile))
            { /* 실패 판정이면 GameOver 연출은 CheckAndHandleBoardFailure 내부에서 시작 */ }
        }

        if (isDragging)
        {
            UpdateNeonTrailPosition();
            linkSystem?.SetPathLit(currentPath, trailColor);
        }

        // Multi-Color Neon: 그라데이션 색상 순환 + 특수 타일 밟았을 때 0.2초 Lerp
        UpdateNeonTrailColor();
    }

    private Vector2 GetPointerScreenPosition()
    {
        if (Mouse.current != null && Mouse.current.position.IsActuated())
            return Mouse.current.position.ReadValue();
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return Touchscreen.current.primaryTouch.position.ReadValue();
        return Vector2.zero;
    }

    private bool IsPointerDown()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;
        return false;
    }

    private bool IsPointerUp()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
            return true;
        return false;
    }

    private bool IsPointerHeld()
    {
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return true;
        return false;
    }

    private bool CanEnterTile(Tile hit)
    {
        // Hidden은 잠겨 있을 때 collider/활성 상태로 입력 단계에서 막히므로 IsActive 검사로 함께 정리한다.
        return IsCurrentBoardLiveRemainingTile(hit);
    }

    private bool ValidateMoveRules(Tile last, Tile hit, int nextStepNumber, out FixedKnotTile fixedKnotHit)
    {
        Tile previousTile = currentPath != null && currentPath.Count >= 2 ? currentPath[currentPath.Count - 2] : null;
        var context = new TwinLinkLegalMoveContext
        {
            CandidateNextTile = hit,
            PreviousTile = previousTile,
            NextStepNumber = nextStepNumber,
            Snapshot = null
        };

        bool legalMove = CanLegallyMoveWithKernel(
            last,
            hit,
            context,
            tile => tile != null ? tile.CurrentNumber : 0,
            IsCurrentBoardLiveRemainingTile,
            out fixedKnotHit,
            out LegalMoveBlockReason blockReason);

        if (legalMove)
            return true;

        if (blockReason == LegalMoveBlockReason.NullOrInactive || blockReason == LegalMoveBlockReason.HiddenLocked)
        {
            if (ShouldLogVerboseStage6Debug())
                DevLog.Log($"[Stage6 이동 거부] reason={blockReason} last={DescribeTileForDebug(last)} hit={DescribeTileForDebug(hit)} nextStep={nextStepNumber} path={DescribeCurrentPathForDebug()}");
            return false;
        }

        var shortCircuitHit = hit != null ? hit.GetComponent<ShortCircuitTile>() : null;
        if (blockReason == LegalMoveBlockReason.ShortCircuitBlocked && shortCircuitHit != null && last != null && shortCircuitHit.IsBlockedEntryFrom(last.X, last.Y))
        {
            gameplayFeedback?.MoveDenied(hit);
            ShowMoveRuleSnackbar(
                $"short_circuit_entry:{hit.X}:{hit.Y}:{shortCircuitHit.DirectionLocalizationKey}",
                "snackbar_short_circuit_blocked_entry",
                ("direction", GetLocalizedDirectionLabel(shortCircuitHit.DirectionLocalizationKey)));
            if (ShouldLogVerboseStage6Debug())
                DevLog.Log($"[Stage6 이동 거부] reason=short_circuit_entry last={DescribeTileForDebug(last)} hit={DescribeTileForDebug(hit)} nextStep={nextStepNumber}");
            return false;
        }

        var shortCircuitLast = last != null ? last.GetComponent<ShortCircuitTile>() : null;
        if (blockReason == LegalMoveBlockReason.ShortCircuitBlocked && shortCircuitLast != null && hit != null && !shortCircuitLast.IsExitCell(hit.X, hit.Y))
        {
            gameplayFeedback?.MoveDenied(last);
            ShowMoveRuleSnackbar(
                $"short_circuit:{last.X}:{last.Y}:{shortCircuitLast.DirectionLocalizationKey}",
                "snackbar_short_circuit_only_direction",
                ("direction", GetLocalizedDirectionLabel(shortCircuitLast.DirectionLocalizationKey)));
            if (ShouldLogVerboseStage6Debug())
                DevLog.Log($"[Stage6 이동 거부] reason=short_circuit_exit last={DescribeTileForDebug(last)} hit={DescribeTileForDebug(hit)} nextStep={nextStepNumber}");
            return false;
        }

        if (blockReason == LegalMoveBlockReason.FixedKnotOrder && fixedKnotHit != null)
        {
            fixedKnotHit.PlayWrongOrderShake();
            ShowMoveRuleSnackbar(
                $"fixed_knot:{hit.X}:{hit.Y}:{fixedKnotHit.CurrentRequiredOrder}",
                "snackbar_fixed_knot_only_order",
                ("order", fixedKnotHit.CurrentRequiredOrder.ToString()));
            if (ShouldLogVerboseStage6Debug())
                DevLog.Log($"[Stage6 이동 거부] reason=fixed_knot_order last={DescribeTileForDebug(last)} hit={DescribeTileForDebug(hit)} nextStep={nextStepNumber}");
            return false;
        }

        if (ShouldLogVerboseStage6Debug())
            DevLog.Log($"[Stage6 이동 거부] reason={blockReason} last={DescribeTileForDebug(last)} hit={DescribeTileForDebug(hit)} nextStep={nextStepNumber}");
        return false;
    }

    private void ApplyLeaveTileEffects(Tile last, Tile hit)
    {
        OnLeaveTileForNext(last, hit);
        var twinLink = last.GetComponent<TwinLinkTile>();
        if (twinLink != null)
        {
            twinLink.ConsumePartners(DecreaseTileAndPlayBlockNote, partner => ShouldExcludeTwinLinkPartnerFromStep(twinLink, partner, hit));
            RefreshMainUIProgress();
        }
        NotifyFixedKnotLeft(last);

        var crossBlast = last.GetComponent<CrossBlastTile>();
        if (crossBlast != null)
        {
            PlayBombSfx();
            crossBlast.TriggerExplosion(this, hit);
        }

        var charge = last.GetComponent<ChargeTile>();
        if (charge != null)
        {
            int charged = ApplyChargeToNeighbors(last, hit);
            charge.PlayChargeFeedback(charged);
        }
    }

    /// <summary>Charge: 떠난 타일의 상하좌우 중 남아 있는 이웃(들어가는 칸 제외)을 1씩 늘린다.</summary>
    private int ApplyChargeToNeighbors(Tile chargeTile, Tile enteredTile)
    {
        int charged = 0;
        for (int i = 0; i < CrossBlastDx.Length; i++)
        {
            Tile neighbor = GetTileAtGrid(chargeTile.X + CrossBlastDx[i], chargeTile.Y + CrossBlastDy[i]);
            if (neighbor == null || neighbor == enteredTile || !neighbor.IsActive || !ChargeTile.CanBeCharged(neighbor))
                continue;
            neighbor.IncreaseNumber();
            gameplayFeedback?.Charged(chargeTile, neighbor);
            charged++;
        }

        if (charged > 0)
        {
            IncrementBoardVersion("charge");
            RefreshMainUIProgress();
        }
        return charged;
    }

    private bool ShouldExcludeTwinLinkPartnerFromStep(TwinLinkTile sourceTwinLink, Tile partnerTile, Tile enteredTile)
    {
        if (sourceTwinLink == null || partnerTile == null)
            return false;

        if (!sourceTwinLink.HasPartner(partnerTile))
            return false;

        if (partnerTile == enteredTile)
            return true;

        return IsImmediatelyPreviousPathTile(partnerTile);
    }

    private bool IsImmediatelyPreviousPathTile(Tile tile)
    {
        if (tile == null || currentPath == null || currentPath.Count < 2)
            return false;

        return currentPath[currentPath.Count - 2] == tile;
    }

    private void ApplyEnterTileEffects(Tile hit, FixedKnotTile fixedKnotHit, int totalPathCount)
    {
        LogSteppedOn(hit);
        var blackout = hit.GetComponent<BlackoutTile>();
        if (blackout != null)
        {
            blackout.OnStepped();
            if (!blackoutQuestionFlipPlayed)
                gameplayFeedback?.Blackout(hit);
            TriggerBlackoutQuestionFlip();
        }
        TryTriggerIgniter(hit);
        NotifyTrailTileStepped(hit);
        NotifyFixedKnotsUpdateVisual(totalPathCount);
        if (fixedKnotHit != null && fixedKnotHit.IsOrderConstraintActive)
            fixedKnotHit.OnSteppedCorrectly();
        if (CheckAndHandleMissedFixedKnot(totalPathCount))
            return;
        CheckVictoryCondition(hit);
    }

    private void FinalizeStep(Tile last, Tile hit, string stepLabel)
    {
        currentPath.Add(hit);
        lastStepFrame = Time.frameCount;
        lastStepTime = Time.unscaledTime;
        gameplayFeedback?.UpdatePath(currentPath);
        attemptMoves.Add(hit);
        AdvanceHintPreviewFollow(hit);
        DevLog.Log($"[스텝] {stepLabel} last=({last.X},{last.Y})→hit=({hit.X},{hit.Y}) pathLen={currentPath.Count}");
    }

    private string GetStepLabel(Tile last, Tile hit, FixedKnotTile fixedKnotHit)
    {
        if (fixedKnotHit != null && fixedKnotHit.IsOrderConstraintActive)
            return "FixedKnot";
        if (last != null && last.GetComponent<ShortCircuitTile>() != null)
            return "ShortCircuit(위)";
        if (hit != null && hit.GetComponent<ShortCircuitTile>() != null)
            return "ShortCircuit(진입)";
        return "일반";
    }

    private void TryStepToTile(Tile last, Tile hit)
    {
        int nextStepNumber = GetTotalPathCount() + 1;
        if (!ValidateMoveRules(last, hit, nextStepNumber, out var fixedKnotHit))
            return;

        ClearMoveRuleSnackbarState();
        gameplayFeedback?.BeginMove(last, hit);
        ApplyLeaveTileEffects(last, hit);
        FinalizeStep(last, hit, GetStepLabel(last, hit, fixedKnotHit));
        gameplayFeedback?.StepAccepted(last, hit);
        int totalPathCount = GetTotalPathCount();
        ApplyEnterTileEffects(hit, fixedKnotHit, totalPathCount);
    }

    private void ClearMoveRuleSnackbarState()
    {
        lastMoveRuleSnackbarId = null;
    }

    private void ShowMoveRuleSnackbar(string snackbarId, string localizationKey, params (string key, string value)[] replacements)
    {
        if (string.IsNullOrWhiteSpace(snackbarId) || lastMoveRuleSnackbarId == snackbarId)
            return;

        lastMoveRuleSnackbarId = snackbarId;

        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI == null)
            return;

        mainUI.ShowGameplaySnackbar(localizationKey, gameplayRuleSnackbarDuration, replacements);
    }

    private string GetLocalizedDirectionLabel(string directionLocalizationKey)
    {
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI == null)
            return directionLocalizationKey;

        return GameLocalization.Get(directionLocalizationKey, mainUI.ActiveLanguageCode);
    }

    /// <summary>드래그 중 인접 타일 검색 시 포인터 주변 반경(월드). count 1인 ShortCircuit 등이 놓치지 않도록.</summary>
    private const float TilePickRadius = 0.45f;
    /// <summary>직전 타일로 즉시 되돌아가는 입력은 N프레임 이내 지터로 간주해 무시.</summary>
    // 오락가락 지터 무시 시간. 프레임 수 기준이면 120Hz 기기에서 절반으로 줄어들어 시간 기준으로 둔다.
    private const float ImmediateBacktrackIgnoreSeconds = 0.2f;
    private readonly Collider2D[] tilePickResults = new Collider2D[16];
    private ContactFilter2D tilePickFilter = ContactFilter2D.noFilter;
    /// <summary>직전 타일로 되돌아가려면 현재 타일보다 이 거리만큼 더 가까워야 함(경계 오락가락 방지).</summary>
    private const float BacktrackDistanceMarginRatio = 0.12f;
    private static readonly int[] CrossBlastDx = { -1, 1, 0, 0 };
    private static readonly int[] CrossBlastDy = { 0, 0, -1, 1 };
    private readonly Tile[] crossBlastScratchTiles = new Tile[4];

    private Vector2 ScreenToWorld2D(Vector2 screenPoint)
    {
        if (mainCamera == null) return Vector2.zero;
        float camZ = mainCamera.transform.position.z;
        Vector3 world3 = mainCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, Mathf.Abs(camZ)));
        return new Vector2(world3.x, world3.y);
    }

    /// <summary>화면 좌표 아래 타일 반환. 드래그 중에는 작은 반경(OverlapCircle)으로 검사해 인접 타일 중 포인터에 가장 가까운 것 반환.</summary>
    private Tile GetTileAtScreen(Vector2 screenPoint, Tile preferAdjacentTo = null)
    {
        if (mainCamera == null) return null;
        Vector2 worldPoint = ScreenToWorld2D(screenPoint);
        // 드래그 중 매 프레임 호출되므로 결과 배열을 재사용해 GC 할당을 없앤다.
        Collider2D[] cols = tilePickResults;
        int hitCount = preferAdjacentTo != null
            ? Physics2D.OverlapCircle(worldPoint, TilePickRadius, tilePickFilter, cols)
            : Physics2D.OverlapPoint(worldPoint, tilePickFilter, cols);
        Tile first = null;
        Tile closestAdjacent = null;
        float closestSq = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            Tile t = cols[i].GetComponent<Tile>();
            if (t == null)
                t = cols[i].GetComponentInParent<Tile>();
            if (t == null) continue;
            if (first == null) first = t;
            if (preferAdjacentTo != null && t != preferAdjacentTo && IsAdjacent(preferAdjacentTo, t))
            {
                float sq = ((Vector2)t.transform.position - worldPoint).sqrMagnitude;
                if (sq < closestSq)
                {
                    closestSq = sq;
                    closestAdjacent = t;
                }
            }
        }
        return closestAdjacent != null ? closestAdjacent : first;
    }

    /// <summary>
    /// CrossBlast 폭발 시 인접(상하좌우) 타일 숫자 1씩 감소. excludeX, excludeY는 제외(밟고 이동한 다음 타일).
    /// </summary>
    public void DecreaseAdjacentTiles(int centerX, int centerY, int excludeX = -999, int excludeY = -999)
    {
        int affectedCount = GetCrossBlastAffectedTiles(centerX, centerY, excludeX, excludeY, crossBlastScratchTiles);
        DecreaseCrossBlastAffectedTiles(crossBlastScratchTiles, affectedCount);
    }

    /// <summary>
    /// CrossBlast가 실제로 영향을 줄 타일을 GameManager에서 단일 계산한다.
    /// </summary>
    public int GetCrossBlastAffectedTiles(Tile centerTile, Tile nextTile, Tile[] results)
    {
        if (centerTile == null)
        {
            ClearCrossBlastResultBuffer(results);
            return 0;
        }

        int excludeX = nextTile != null ? nextTile.X : -999;
        int excludeY = nextTile != null ? nextTile.Y : -999;
        return GetCrossBlastAffectedTiles(centerTile.X, centerTile.Y, excludeX, excludeY, results);
    }

    private int GetCrossBlastAffectedTiles(int centerX, int centerY, int excludeX, int excludeY, Tile[] results)
    {
        ClearCrossBlastResultBuffer(results);
        if (tiles == null || results == null || results.Length == 0)
            return 0;

        int count = 0;
        for (int i = 0; i < CrossBlastDx.Length && count < results.Length; i++)
        {
            int nx = centerX + CrossBlastDx[i];
            int ny = centerY + CrossBlastDy[i];
            if (nx == excludeX && ny == excludeY) continue; // 다음 타일은 CrossBlast 효과 제외
            if (ny >= 0 && ny < stageHeight && nx >= 0 && nx < stageWidth)
            {
                Tile t = tiles[ny, nx];
                if (t != null && t.IsActive)
                    results[count++] = t;
            }
        }

        return count;
    }

    private static void ClearCrossBlastResultBuffer(Tile[] results)
    {
        if (results == null) return;
        Array.Clear(results, 0, results.Length);
    }

    public void DecreaseCrossBlastAffectedTiles(Tile[] affectedTiles, int affectedCount)
    {
        if (affectedTiles == null)
            return;

        int count = Mathf.Min(affectedCount, affectedTiles.Length);
        for (int i = 0; i < count; i++)
        {
            Tile tile = affectedTiles[i];
            if (tile != null && tile.IsActive)
                DecreaseTileAndPlayBlockNote(tile);
        }

        // CrossBlast로 인한 주변 타일 감소 후 진행도 갱신
        RefreshMainUIProgress();
    }

    /// <summary>타일을 떠날 때(last) 그 타일이 FixedKnot이면 기어 사라짐 연출.</summary>
    private void NotifyFixedKnotLeft(Tile lastTile)
    {
        if (lastTile == null) return;
        var fk = lastTile.GetComponent<FixedKnotTile>();
        if (fk != null) fk.OnLeftByPlayer();
    }

    /// <summary>타일을 떠날 때 기본 감소를 적용한다.</summary>
    private void OnLeaveTileForNext(Tile last, Tile next)
    {
        if (last == null) return;
        DevLog.Log($"[타일 -1] 직전 타일 ({last.X},{last.Y}) 떠남 → 다음 ({next.X},{next.Y})으로 이동, 직전 타일 -1");
        int before = last.CurrentNumber;
        DecreaseTileAndPlayBlockNote(last);
        var igniter = last.GetComponent<IgniterTile>();
        if (igniter != null && last.CurrentNumber > 0)
            igniter.OnConsumed();
        DevLog.Log($"[타일 -1] ({last.X},{last.Y}) count {before} → {last.CurrentNumber}");
        if (last.CurrentNumber <= 0)
            DevLog.Log($"[타일 사라짐] ({last.X},{last.Y}) count 0");

        // 타일 숫자/소멸 변경 직후 진행도 갱신
        RefreshMainUIProgress();
    }

    /// <summary>현재 밟은 타일 로그 (디버그용).</summary>
    private void LogSteppedOn(Tile hit)
    {
        if (hit == null) return;
        DevLog.Log($"[현재 밟은 타일] ({hit.X},{hit.Y}) count={hit.CurrentNumber}");
    }

    /// <summary>타일을 밟은 직후: Igniter면 targetID에 해당하는 Hidden 그룹을 활성화한다.</summary>
    private void TryTriggerIgniter(Tile steppedTile, bool instant = false)
    {
        if (steppedTile == null || hiddenGroups == null) return;
        var igniter = steppedTile.GetComponent<IgniterTile>();
        if (igniter == null) return;
        List<HiddenTile> list = null;
        if (string.IsNullOrEmpty(igniter.TargetID) || !hiddenGroups.TryGetValue(igniter.TargetID, out list) || list == null) return;
        Vector3 igniterPos = steppedTile.transform.position;
        list.Sort((a, b) =>
        {
            if (a == null || b == null) return 0;
            float da = (a.transform.position - igniterPos).sqrMagnitude;
            float db = (b.transform.position - igniterPos).sqrMagnitude;
            return da.CompareTo(db);
        });
        float relayInterval = (list.Count > 0 && list[0] != null) ? list[0].RelayInterval : 0.08f;
        int pendingHiddenActivations = 0;
        if (!igniter.HasTriggered)
        {
            foreach (var hidden in list)
                if (hidden != null && !hidden.IsActivated)
                    pendingHiddenActivations++;
        }
        if (pendingHiddenActivations > 0)
            RegisterPendingBoardMutation("hidden_relay", pendingHiddenActivations);
        if (ShouldLogVerboseStage6Debug())
        {
            List<string> hiddenSummaries = new List<string>();
            foreach (var hidden in list)
            {
                if (hidden == null)
                {
                    hiddenSummaries.Add("null");
                    continue;
                }
                var hiddenTile = hidden.GetComponent<Tile>();
                hiddenSummaries.Add($"{DescribeTileForDebug(hiddenTile)} activated={hidden.IsActivated} collider={hidden.IsColliderEnabled}");
            }
            DevLog.Log($"[Stage6 Igniter 트리거] stepped={DescribeTileForDebug(steppedTile)} targetID={igniter.TargetID} instant={instant} relay={relayInterval:F3} targets=[{string.Join(" | ", hiddenSummaries)}]");
        }
        int scheduledHiddenActivations = igniter.TriggerHiddenTiles(list, instant, relayInterval, OnHiddenTileBecameLiveForFailureCheck);
        int unscheduledHiddenActivations = pendingHiddenActivations - scheduledHiddenActivations;
        for (int i = 0; i < unscheduledHiddenActivations; i++)
            CompletePendingBoardMutation("hidden_relay_unscheduled", steppedTile, false);
    }

    private void OnHiddenTileBecameLiveForFailureCheck(HiddenTile hidden)
    {
        IncrementBoardVersion("hidden_live");
        CompletePendingBoardMutation("hidden_relay", GetFailureCurrentTile());
    }

    private void ActivateStartIgniterIfNeeded()
    {
        if (currentStartTile == null)
            return;

        TryTriggerIgniter(currentStartTile, true);
        IncrementBoardVersion("start_igniter");
    }

    /// <summary>옮긴 횟수만 반환. 첫 구간·이어서 드래그 모두 currentPath.Count - 1 로 통일 (한 번 옮길 때마다 -1).</summary>
    private int GetTotalPathCount()
    {
        return totalStepsCommitted + Mathf.Max(0, currentPath.Count - 1);
    }

    /// <summary>맵 전체에서 count &gt; 0인 타일들의 카운트 합.</summary>
    private int GetTotalRemainingCount()
    {
        if (tiles == null) return 0;
        int sum = 0;
        for (int row = 0; row < stageHeight; row++)
            for (int col = 0; col < stageWidth; col++)
                if (tiles[row, col] != null)
                    sum += tiles[row, col].CurrentNumber;
        return sum;
    }

    private void IncrementBoardVersion(string reason)
    {
        boardVersion = boardVersion == int.MaxValue ? 1 : boardVersion + 1;
        InvalidateHintCache();
    }

    private void InvalidateHintCache()
    {
        cachedHintBoardVersion = -1;
        cachedHintStartTile = null;
        cachedHintPreviousTile = null;
        cachedHintNextStepNumber = -1;
        cachedHintUnavailable = false;
        cachedHintPath.Clear();
        cachedHintSolutionPath.Clear();
        cachedHintSolverStatus = "";
    }

    public bool HasAvailableHintPreview()
    {
        return TryGetHintPreviewPath(out _);
    }

    public bool IsHintPreviewRequestRunning => isHintPreviewRequestRunning;

    public void CheckHintPreviewAvailabilityWithLoading(Action<bool> onComplete)
    {
        if (isHintPreviewRequestRunning)
            return;

        hintPreviewRequestRoutine = StartCoroutine(CheckHintPreviewAvailabilityRoutine(onComplete, showFeedback: true));
    }

    public void CheckHintPreviewAvailabilitySilently(Action<bool> onComplete)
    {
        if (isHintPreviewRequestRunning)
        {
            onComplete?.Invoke(false);
            return;
        }

        hintPreviewRequestRoutine = StartCoroutine(CheckHintPreviewAvailabilityRoutine(onComplete, showFeedback: false));
    }

    private IEnumerator CheckHintPreviewAvailabilityRoutine(Action<bool> onComplete, bool showFeedback)
    {
        isHintPreviewRequestRunning = true;

        bool hasPath = false;
        bool loadingShown = false;
        bool aborted = false;
        float loadingStartedAt = 0f;
        try
        {
            hasPath = TryGetHintPreviewPath(out _, useQuickBudget: true);
            if (!hasPath && IsHintSolverBudgetExhaustedStatus())
            {
                string solveKey = BuildHintSolveKey();
                bool skipSilentRetry = !showFeedback && solveKey == silentHintExhaustedKey;
                HintSolveRequest request = skipSilentRetry
                    ? null
                    : BeginHintSolve(useQuickBudget: false, showFeedback ? HintSolverTimeLimitSeconds : HintSilentSolverTimeLimitSeconds, out hasPath);

                if (request != null)
                {
                    if (showFeedback)
                    {
                        SetHintLoadingVisible(true);
                        loadingShown = true;
                    }
                    loadingStartedAt = Time.realtimeSinceStartup;

                    int requestBoardVersion = boardVersion;
                    float slice = showFeedback ? HintSolverSliceSecondsWithLoading : HintSolverSliceSecondsSilent;
                    while (!request.Step(slice))
                    {
                        yield return null;
                        // 탐색 중 보드가 바뀌면(드래그 시작, 리셋, 클리어) 결과가 의미 없으므로 중단한다.
                        if (requestBoardVersion != boardVersion || isDragging || stageCleared || isGameOverSequencePlaying)
                        {
                            aborted = true;
                            break;
                        }
                    }

                    if (!aborted)
                    {
                        hasPath = CompleteHintSolve(request);
                        if (!hasPath && !showFeedback && IsHintSolverBudgetExhaustedStatus())
                            silentHintExhaustedKey = solveKey;
                    }

                    float remainingVisibleTime = showFeedback && !aborted ? HintLoadingMinimumVisibleSeconds - (Time.realtimeSinceStartup - loadingStartedAt) : 0f;
                    if (remainingVisibleTime > 0f)
                        yield return new WaitForSecondsRealtime(remainingVisibleTime);
                }
            }
        }
        finally
        {
            if (loadingShown)
                SetHintLoadingVisible(false);
            isHintPreviewRequestRunning = false;
            hintPreviewRequestRoutine = null;
        }

        if (aborted)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        if (!hasPath && showFeedback)
        {
            LogHintUnavailable();
            ShowHintSnackbar(GetHintUnavailableSnackbarKey());
            TrackHintEvent("hint_path_none", 0);
        }

        onComplete?.Invoke(hasPath);
    }

    public void ShowHintPreviewWithLoading(Action<bool> onComplete = null)
    {
        CheckHintPreviewAvailabilityWithLoading(hasPath =>
        {
            bool shown = hasPath && ShowHintPreview();
            onComplete?.Invoke(shown);
        });
    }

    public bool ShowHintPreview()
    {
        ClearHintPreview();

        if (!TryGetHintPreviewPath(out List<Tile> path))
        {
            LogHintUnavailable();
            ShowHintSnackbar(GetHintUnavailableSnackbarKey());
            TrackHintEvent("hint_path_none", 0);
            return false;
        }

        RenderHintPreview(path);
        LogHintPreviewPlan(path);
        TrackHintEvent("hint_use", path.Count - 1);
        stageHintUsed = true;
        return true;
    }

    public void CancelHintPreview()
    {
        if (hintPreviewRequestRoutine != null)
        {
            StopCoroutine(hintPreviewRequestRoutine);
            hintPreviewRequestRoutine = null;
        }

        isHintPreviewRequestRunning = false;
        SetHintLoadingVisible(false);
        ClearHintPreview();
    }

    private void SetHintLoadingVisible(bool visible)
    {
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI == null)
            return;

        if (visible)
            mainUI.ShowHintLoading(HintSolverTimeLimitSeconds * (1f / 60f) / HintSolverSliceSecondsWithLoading);
        else
            mainUI.HideHintLoading();
    }

    private void TrackHintEvent(string eventName, int moveCount)
    {
        FirebaseBootstrap.LogEvent(eventName, new Dictionary<string, object>
        {
            { "stage_index", currentStageIndex },
            { "board_version", boardVersion },
            { "moves", Mathf.Max(0, moveCount) }
        });
    }

    private void ShowHintSnackbar(string localizationKey)
    {
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI != null)
            mainUI.ShowGameplaySnackbar(localizationKey, hintSnackbarDuration);
    }

    private string GetHintUnavailableSnackbarKey()
    {
        if (string.IsNullOrEmpty(cachedHintSolverStatus))
            return "snackbar_hint_no_path";
        if (IsHintSolverBudgetExhaustedStatus())
            return "snackbar_hint_solver_timeout";

        return "snackbar_hint_no_solution_restart";
    }

    private bool IsHintSolverBudgetExhaustedStatus()
    {
        return cachedHintSolverStatus == "time_limit" || cachedHintSolverStatus == "node_limit";
    }

    private bool TryGetHintPreviewPath(out List<Tile> path, bool useQuickBudget = false)
    {
        path = cachedHintPath;
        float timeLimit = useQuickBudget ? HintQuickSolverTimeLimitSeconds : HintSolverTimeLimitSeconds;
        HintSolveRequest request = BeginHintSolve(useQuickBudget, timeLimit, out bool cachedResult);
        if (request == null)
            return cachedResult;

        request.RunToCompletion();
        return CompleteHintSolve(request);
    }

    private string BuildHintSolveKey()
    {
        Tile previousTile = GetHintPreviousTile();
        return $"{boardVersion}:{(currentStartTile != null ? currentStartTile.GetInstanceID() : 0)}:{(previousTile != null ? previousTile.GetInstanceID() : 0)}:{GetTotalPathCount() + 1}";
    }

    /// <summary>
    /// 캐시·전제조건을 확인하고 필요하면 새 탐색 요청을 만든다. 요청이 null이면 cachedResult가 최종 결과다.
    /// </summary>
    private HintSolveRequest BeginHintSolve(bool useQuickBudget, float timeLimitSeconds, out bool cachedResult)
    {
        cachedResult = false;
        if (stageCleared || isGameOverSequencePlaying || isDragging || tiles == null)
            return null;
        if (!IsCurrentBoardLiveRemainingTile(currentStartTile))
            return null;

        Tile previousTile = GetHintPreviousTile();
        int nextStepNumber = GetTotalPathCount() + 1;
        bool sameCacheKey = cachedHintBoardVersion == boardVersion &&
            cachedHintStartTile == currentStartTile &&
            cachedHintPreviousTile == previousTile &&
            cachedHintNextStepNumber == nextStepNumber;

        if (sameCacheKey && cachedHintPath.Count > 1)
        {
            cachedResult = true;
            return null;
        }

        if (sameCacheKey && cachedHintUnavailable)
            return null;

        cachedHintPath.Clear();
        cachedHintSolutionPath.Clear();
        cachedHintUnavailable = false;
        cachedHintSolverStatus = "";
        cachedHintBoardVersion = boardVersion;
        cachedHintStartTile = currentStartTile;
        cachedHintPreviousTile = previousTile;
        cachedHintNextStepNumber = nextStepNumber;

        HintSearchState initialState = new HintSearchState(this, currentStartTile, previousTile, nextStepNumber);
        int targetMoveCount = CalculateHintTargetMoveCount(initialState.TotalRemainingCount);
        if (targetMoveCount <= 0)
        {
            cachedHintUnavailable = true;
            cachedHintSolverStatus = "target_move_count_zero";
            return null;
        }

        // 플레이어가 아직 검증된 정답 경로 위에 있으면 탐색 없이 바로 힌트를 만든다.
        if (TryBuildHintFromStoredSolution(initialState, targetMoveCount))
        {
            cachedResult = true;
            return null;
        }

        int nodeLimit = useQuickBudget ? HintQuickSolverNodeLimit : HintSolverNodeLimit;
        HintSolverBudget budget = new HintSolverBudget(nodeLimit, timeLimitSeconds);
        return new HintSolveRequest(this, initialState, currentStartTile, targetMoveCount, budget);
    }

    // 이번 시도에서 실제로 들어간 칸 순서(되돌리기 시 함께 줄어든다). 저장된 정답 경로와 비교하는 데 쓴다.
    private readonly List<Tile> attemptMoves = new List<Tile>(64);

    private bool TryBuildHintFromStoredSolution(HintSearchState initialState, int targetMoveCount)
    {
        List<Vector2Int> solution = feverActive ? feverSolution : StageManager.LoadSolutionMoves(currentStageIndex);
        int done = attemptMoves.Count;
        if (solution == null || done >= solution.Count || isDragging)
            return false;

        for (int i = 0; i < done; i++)
        {
            Tile moved = attemptMoves[i];
            if (moved == null || moved.X != solution[i].x || moved.Y != solution[i].y)
                return false;
        }

        // 남은 정답을 힌트 규칙 모델로 끝까지 재생해 지금 보드에서도 정말 풀리는지 확인한 뒤에만 쓴다.
        var path = new List<Tile>(solution.Count - done + 1) { currentStartTile };
        var sim = new HintSearchState(initialState);
        for (int i = done; i < solution.Count; i++)
        {
            Tile next = GetTileAtGrid(solution[i].x, solution[i].y);
            if (next == null || IsHintSolvedState(sim))
                return false;

            bool legal = false;
            List<HintMoveCandidate> candidates = CollectSortedHintCandidates(sim);
            for (int c = 0; c < candidates.Count; c++)
            {
                if (candidates[c].Tile == next)
                {
                    legal = true;
                    break;
                }
            }
            if (!legal || !TryApplyHintStep(sim, next, null, out _) || sim.HasMissedFixedKnot(sim.NextStepNumber - 1))
                return false;
            path.Add(next);
        }

        if (!IsHintSolvedState(sim))
            return false;

        cachedHintSolutionPath.AddRange(path);
        if (!TryBuildCommitSafeHintPreviewPath(path, initialState, targetMoveCount, cachedHintPath))
        {
            cachedHintSolutionPath.Clear();
            cachedHintPath.Clear();
            return false;
        }

        cachedHintSolverStatus = $"stored_solution fullMoves={path.Count - 1} previewMoves={Mathf.Max(0, cachedHintPath.Count - 1)}";
        return true;
    }

    private bool CompleteHintSolve(HintSolveRequest request)
    {
        HintSolverBudget budget = request.Budget;
        List<Tile> solutionPath = request.SolutionPath;
        HintSearchState initialState = request.InitialState;

        if (!request.Solved || solutionPath.Count <= 1)
        {
            cachedHintSolverStatus = budget.IsExhausted ? budget.Status : "not_found";
            cachedHintUnavailable = !budget.IsExhausted;
            HintDebugLog($"[힌트 solver 실패] stage={currentStageIndex} boardVersion={boardVersion} start={FormatHintTile(currentStartTile)} remaining={initialState.TotalRemainingCount} nodes={budget.NodeCount} failedCache={request.FailedCacheCount} status={cachedHintSolverStatus}");
            return false;
        }

        cachedHintSolutionPath.AddRange(solutionPath);
        if (!TryBuildCommitSafeHintPreviewPath(solutionPath, initialState, request.TargetMoveCount, cachedHintPath))
        {
            cachedHintSolverStatus = "commit_safe_preview_not_found";
            cachedHintUnavailable = true;
            HintDebugLog($"[힌트 안전지점 없음] stage={currentStageIndex} boardVersion={boardVersion} start={FormatHintTile(currentStartTile)} remaining={initialState.TotalRemainingCount} fullMoves={solutionPath.Count - 1} path={FormatHintPath(solutionPath)}");
            return false;
        }

        int previewMoves = Mathf.Max(0, cachedHintPath.Count - 1);
        cachedHintSolverStatus = $"solved nodes={budget.NodeCount} failedCache={request.FailedCacheCount} fullMoves={solutionPath.Count - 1} previewMoves={previewMoves} targetMoves={request.TargetMoveCount}";
        return true;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private static void HintDebugLog(string message)
    {
        DevLog.Log(message);
    }

    /// <summary>
    /// 힌트 탐색을 재귀 대신 명시적 스택으로 수행해 여러 프레임에 나눠 실행할 수 있게 한 작업 단위.
    /// 탐색 순서·가지치기·실패 캐시는 기존 재귀 탐색(TrySolveHintPath)과 동일하다.
    /// </summary>
    private sealed class HintSolveRequest
    {
        private sealed class Frame
        {
            public HintSearchState State;
            public string Key;
            public List<HintMoveCandidate> Candidates;
            public int NextIndex;
        }

        private enum PushResult { Pushed, Rejected, Solved, Exhausted }

        private readonly GameManager owner;
        private readonly List<Frame> frames = new List<Frame>(64);
        private readonly List<Tile> workingPath;
        private readonly HashSet<string> failedCache = new HashSet<string>();
        private readonly System.Diagnostics.Stopwatch sliceWatch = new System.Diagnostics.Stopwatch();
        private bool started;

        public HintSearchState InitialState { get; }
        public int TargetMoveCount { get; }
        public HintSolverBudget Budget { get; }
        public List<Tile> SolutionPath { get; }
        public bool Finished { get; private set; }
        public bool Solved { get; private set; }
        public int FailedCacheCount => failedCache.Count;

        public HintSolveRequest(GameManager owner, HintSearchState initialState, Tile startTile, int targetMoveCount, HintSolverBudget budget)
        {
            this.owner = owner;
            InitialState = initialState;
            TargetMoveCount = targetMoveCount;
            Budget = budget;
            workingPath = new List<Tile>(Mathf.Max(HintPreviewMaxPathTiles, initialState.TotalRemainingCount + 1)) { startTile };
            SolutionPath = new List<Tile>(workingPath.Capacity);
        }

        public void RunToCompletion()
        {
            while (!Step(float.MaxValue)) { }
        }

        /// <summary>최대 maxSeconds 동안 탐색한다. 탐색이 끝나면 true.</summary>
        public bool Step(float maxSeconds)
        {
            if (Finished)
                return true;

            Budget.ResumeClock();
            sliceWatch.Restart();
            try
            {
                if (!started)
                {
                    started = true;
                    if (HandlePush(Push(InitialState)))
                        return true;
                }

                int iterations = 0;
                while (frames.Count > 0)
                {
                    if ((++iterations & 31) == 0 && sliceWatch.Elapsed.TotalSeconds >= maxSeconds)
                        return false;

                    if (Budget.IsExhausted)
                        return Finish(false);

                    Frame top = frames[frames.Count - 1];
                    if (top.NextIndex >= top.Candidates.Count)
                    {
                        if (!Budget.IsExhausted)
                            failedCache.Add(top.Key);
                        frames.RemoveAt(frames.Count - 1);
                        if (frames.Count > 0)
                            workingPath.RemoveAt(workingPath.Count - 1);
                        continue;
                    }

                    Tile candidate = top.Candidates[top.NextIndex++].Tile;
                    HintSearchState nextState = new HintSearchState(top.State);
                    if (!owner.TryApplyHintStep(nextState, candidate, null, out _))
                        continue;
                    if (nextState.HasMissedFixedKnot(nextState.NextStepNumber - 1))
                        continue;
                    if (nextState.TotalRemainingCount > 0 && nextState.GetRemainingCount(nextState.Current) <= 0)
                        continue;

                    workingPath.Add(candidate);
                    PushResult result = Push(nextState);
                    if (result == PushResult.Rejected)
                    {
                        workingPath.RemoveAt(workingPath.Count - 1);
                        continue;
                    }
                    if (HandlePush(result))
                        return true;
                }

                return Finish(false);
            }
            finally
            {
                sliceWatch.Stop();
                Budget.PauseClock();
            }
        }

        /// <summary>Solved/Exhausted면 탐색을 끝내고 true를 반환한다.</summary>
        private bool HandlePush(PushResult result)
        {
            if (result == PushResult.Solved)
                return Finish(true);
            if (result == PushResult.Exhausted)
                return Finish(false);
            if (result == PushResult.Rejected && frames.Count == 0)
                return Finish(false);
            return false;
        }

        private PushResult Push(HintSearchState state)
        {
            if (!Budget.TryEnterNode())
                return PushResult.Exhausted;

            if (owner.IsHintSolvedState(state))
            {
                SolutionPath.Clear();
                SolutionPath.AddRange(workingPath);
                return PushResult.Solved;
            }

            if (state.TotalRemainingCount > 0 && state.GetRemainingCount(state.Current) <= 0)
                return PushResult.Rejected;

            string key = state.BuildCacheKey();
            if (failedCache.Contains(key))
                return PushResult.Rejected;

            frames.Add(new Frame
            {
                State = state,
                Key = key,
                Candidates = owner.CollectSortedHintCandidates(state),
                NextIndex = 0
            });
            return PushResult.Pushed;
        }

        private bool Finish(bool solved)
        {
            Finished = true;
            Solved = solved;
            frames.Clear();
            return true;
        }
    }

    private bool TryBuildCommitSafeHintPreviewPath(List<Tile> solutionPath, HintSearchState initialState, int targetMoveCount, List<Tile> previewPath)
    {
        previewPath?.Clear();
        if (solutionPath == null || solutionPath.Count <= 1 || initialState == null || previewPath == null)
            return false;

        HintSearchState endpointState = new HintSearchState(initialState);
        int firstCheckIndex = Mathf.Clamp(targetMoveCount, 1, solutionPath.Count - 1);
        int selectedIndex = -1;

        for (int i = 1; i < solutionPath.Count; i++)
        {
            if (!TryApplyHintStep(endpointState, solutionPath[i], null, out _))
                return false;

            if (endpointState.HasMissedFixedKnot(endpointState.NextStepNumber - 1))
                return false;

            if (i < firstCheckIndex)
                continue;

            if (IsHintCommitSafeEndpoint(endpointState, solutionPath, i))
            {
                selectedIndex = i;
                break;
            }
        }

        if (selectedIndex < 0)
            return false;

        for (int i = 0; i <= selectedIndex; i++)
            previewPath.Add(solutionPath[i]);

        return previewPath.Count > 1;
    }

    private bool IsHintCommitSafeEndpoint(HintSearchState endpointState, List<Tile> solutionPath, int endpointIndex)
    {
        if (endpointState == null)
            return false;
        if (IsHintSolvedState(endpointState))
            return true;
        if (endpointState.Current == null || !endpointState.IsLiveRemainingTile(endpointState.Current))
            return false;
        if (solutionPath == null || endpointIndex >= solutionPath.Count - 1)
            return false;

        HintSearchState resumeState = new HintSearchState(endpointState)
        {
            Previous = null
        };

        if (!HasAnyLegalHintMove(resumeState))
            return false;

        for (int i = endpointIndex + 1; i < solutionPath.Count; i++)
        {
            Tile nextTile = solutionPath[i];
            if (!CanLegallyMoveForHint(resumeState, nextTile))
                return false;
            if (!TryApplyHintStep(resumeState, nextTile, null, out _))
                return false;
            if (resumeState.HasMissedFixedKnot(resumeState.NextStepNumber - 1))
                return false;
            if (resumeState.TotalRemainingCount > 0 && resumeState.GetRemainingCount(resumeState.Current) <= 0)
                return false;
        }

        return IsHintSolvedState(resumeState);
    }

    private void LogHintUnavailable()
    {
        DevLog.Log($"[힌트 없음] stage={currentStageIndex} boardVersion={boardVersion} start={DescribeTileForDebug(currentStartTile)} previous={DescribeTileForDebug(GetHintPreviousTile())} remaining={GetTotalRemainingCount()} dragging={isDragging} cleared={stageCleared} gameOver={isGameOverSequencePlaying} solver={cachedHintSolverStatus}");
    }

    private void LogHintPreviewPlan(List<Tile> path)
    {
        if (path == null || path.Count <= 1)
            return;

        Tile previousTile = GetHintPreviousTile();
        int nextStepNumber = GetTotalPathCount() + 1;
        var state = new HintSearchState(this, currentStartTile, previousTile, nextStepNumber);
        int beforeRemaining = state.TotalRemainingCount;
        List<string> stepSummaries = new List<string>(path.Count - 1);

        for (int i = 1; i < path.Count; i++)
        {
            Tile nextTile = path[i];
            Tile leavingTile = state.Current;
            List<string> effects = new List<string>();
            ApplyHintStep(state, nextTile, effects);
            string effectSummary = effects.Count > 0 ? string.Join(", ", effects) : "count 변화 없음";
            stepSummaries.Add($"{i}. {FormatHintTile(leavingTile)} -> {FormatHintTile(nextTile)} | {effectSummary} | remaining={state.TotalRemainingCount}");
        }

        string finishIntent = BuildHintPreviewFinishIntent(path, state);
        string solutionSummary = BuildHintSolutionSummary();

        DevLog.Log(
            $"[힌트 표시] stage={currentStageIndex} boardVersion={boardVersion} start={FormatHintTile(currentStartTile)} previous={FormatHintTile(previousTile)} nextStep={nextStepNumber} moves={path.Count - 1} remaining {beforeRemaining}->{state.TotalRemainingCount}\n" +
            $"[힌트 경로] {FormatHintPath(path)}\n" +
            $"[힌트 스텝]\n{string.Join("\n", stepSummaries)}\n" +
            $"{solutionSummary}\n" +
            $"[힌트 마무리 의도] {finishIntent}");
    }

    private string BuildHintPreviewFinishIntent(List<Tile> previewPath, HintSearchState previewEndState)
    {
        if (previewEndState == null)
            return "힌트 상태를 다시 계산할 수 없음";

        if (IsHintSolvedState(previewEndState))
            return DescribeHintSolvedState(previewEndState);

        if (cachedHintSolutionPath.Count > previewPath.Count)
        {
            Tile nextTile = cachedHintSolutionPath[previewPath.Count];
            int hiddenMoves = Mathf.Max(0, cachedHintSolutionPath.Count - previewPath.Count);
            return $"전체 클리어 해답의 앞 {previewPath.Count - 1}칸만 표시함. 이후 {FormatHintTile(nextTile)} 방향으로 {hiddenMoves}칸 더 이어가면 solver 해답을 따라감";
        }

        return "전체 클리어 해답을 찾았지만 표시 경로 이후 요약할 다음 칸이 없음";
    }

    private string BuildHintSolutionSummary()
    {
        if (cachedHintSolutionPath.Count <= 1)
            return $"[힌트 전체 해답] 없음 status={cachedHintSolverStatus}";

        Tile previousTile = GetHintPreviousTile();
        int nextStepNumber = GetTotalPathCount() + 1;
        var solutionState = new HintSearchState(this, currentStartTile, previousTile, nextStepNumber);
        int beforeRemaining = solutionState.TotalRemainingCount;
        for (int i = 1; i < cachedHintSolutionPath.Count; i++)
            ApplyHintStep(solutionState, cachedHintSolutionPath[i]);

        return $"[힌트 전체 해답] moves={cachedHintSolutionPath.Count - 1} remaining {beforeRemaining}->{solutionState.TotalRemainingCount} status={cachedHintSolverStatus} finish={DescribeHintSolvedState(solutionState)} path={FormatHintPath(cachedHintSolutionPath)}";
    }

    private static string FormatHintPath(List<Tile> path)
    {
        if (path == null || path.Count == 0)
            return "[]";

        List<string> parts = new List<string>(path.Count);
        for (int i = 0; i < path.Count; i++)
            parts.Add(FormatHintTile(path[i]));
        return string.Join(" -> ", parts);
    }

    private static string FormatHintTile(Tile tile)
    {
        if (tile == null)
            return "null";

        List<string> tags = new List<string>();
        if (tile.GetComponent<IgniterTile>() != null) tags.Add("Igniter");
        if (tile.GetComponent<HiddenTile>() != null) tags.Add("Hidden");
        if (tile.GetComponent<BlackoutTile>() != null) tags.Add("Blackout");
        if (tile.GetComponent<BlindCurtainTile>() != null) tags.Add("BlindCurtain");
        if (tile.GetComponent<ShortCircuitTile>() != null) tags.Add("ShortCircuit");
        var fixedKnot = tile.GetComponent<FixedKnotTile>();
        if (fixedKnot != null && fixedKnot.IsOrderConstraintActive) tags.Add("FixedKnot");
        if (tile.GetComponent<CrossBlastTile>() != null) tags.Add("CrossBlast");
        if (tile.GetComponent<TwinLinkTile>() != null) tags.Add("TwinLink");
        string typeSummary = tags.Count > 0 ? string.Join("+", tags) : "Normal";
        return $"({tile.X},{tile.Y})#{tile.CurrentNumber}/{typeSummary}";
    }

    private Tile GetHintPreviousTile()
    {
        if (currentPath != null && currentPath.Count >= 2)
            return currentPath[currentPath.Count - 2];
        return null;
    }

    private int CalculateHintTargetMoveCount(int remainingCount)
    {
        if (remainingCount <= 0)
            return 0;
        if (remainingCount <= 5)
            return Mathf.Min(HintPreviewMaxMoves, remainingCount);
        return Mathf.Clamp(Mathf.CeilToInt(remainingCount * 0.2f), 3, HintPreviewMaxMoves);
    }

    private List<HintMoveCandidate> CollectSortedHintCandidates(HintSearchState state)
    {
        List<HintMoveCandidate> candidates = new List<HintMoveCandidate>(CrossBlastDx.Length);
        if (state == null || state.Current == null)
            return candidates;

        for (int i = 0; i < CrossBlastDx.Length; i++)
        {
            Tile candidate = GetTileAtGrid(state.Current.X + CrossBlastDx[i], state.Current.Y + CrossBlastDy[i]);
            if (!CanLegallyMoveForHint(state, candidate))
                continue;

            candidates.Add(new HintMoveCandidate(candidate, ScoreHintCandidate(state, candidate)));
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        return candidates;
    }

    private int ScoreHintCandidate(HintSearchState state, Tile candidate)
    {
        if (state == null || candidate == null)
            return int.MinValue;

        int score = state.GetRemainingCount(candidate) * 100;
        score += CountLiveHintNeighbors(candidate, state) * 18;

        if (candidate.GetComponent<IgniterTile>() != null)
            score += 35;
        if (candidate.GetComponent<FixedKnotTile>() != null && !state.IsFixedKnotSolved(candidate))
            score += 25;
        if (candidate.GetComponent<CrossBlastTile>() != null)
            score += 12;
        if (candidate == state.Previous)
            score -= 25;

        return score;
    }

    private int CountLiveHintNeighbors(Tile tile, HintSearchState state)
    {
        if (tile == null || state == null)
            return 0;

        int count = 0;
        for (int i = 0; i < CrossBlastDx.Length; i++)
        {
            Tile neighbor = GetTileAtGrid(tile.X + CrossBlastDx[i], tile.Y + CrossBlastDy[i]);
            if (neighbor != null && state.IsLiveRemainingTile(neighbor))
                count++;
        }
        return count;
    }

    private bool IsHintSolvedState(HintSearchState state)
    {
        if (state == null)
            return false;
        if (state.TotalRemainingCount <= 0)
            return true;

        Tile current = state.Current;
        if (current == null || state.GetRemainingCount(current) != 1)
            return false;

        if (state.TotalRemainingCount == 1)
            return true;

        var twinLink = current.GetComponent<TwinLinkTile>();
        if (twinLink == null || twinLink.Partners == null || twinLink.Partners.Count == 0)
            return false;

        int groupRemaining = 1;
        foreach (TwinLinkTile partner in twinLink.Partners)
        {
            if (partner == null)
                continue;

            Tile partnerTile = partner.GetComponent<Tile>();
            if (partnerTile == null)
                continue;

            int partnerRemaining = state.GetRemainingCount(partnerTile);
            if (partnerRemaining <= 0)
                continue;
            if (partnerRemaining != 1)
                return false;

            groupRemaining += partnerRemaining;
        }

        return state.TotalRemainingCount == groupRemaining;
    }

    private string DescribeHintSolvedState(HintSearchState state)
    {
        if (state == null)
            return "solver 상태 없음";
        if (state.TotalRemainingCount <= 0)
            return "모든 count가 0이 되는 클리어 해답";
        if (state.TotalRemainingCount == 1 && state.GetRemainingCount(state.Current) == 1)
            return $"마지막 타일 규칙으로 {FormatHintTile(state.Current)}을 소비하면 클리어";
        return $"TwinLink 마지막 그룹 규칙으로 {FormatHintTile(state.Current)} 그룹을 소비하면 클리어";
    }

    private bool CanLegallyMoveForHint(HintSearchState state, Tile candidate)
    {
        if (state == null)
            return false;

        var context = new TwinLinkLegalMoveContext
        {
            CandidateNextTile = candidate,
            PreviousTile = state.Previous,
            NextStepNumber = state.NextStepNumber,
            Snapshot = null
        };

        return CanLegallyMoveWithKernel(
            state.Current,
            candidate,
            context,
            state.GetRemainingCount,
            state.IsLiveRemainingTile,
            out _,
            out _,
            state.IsFixedKnotSolved(candidate));
    }

    private bool HasAnyLegalHintMove(HintSearchState state)
    {
        if (state == null || state.Current == null)
            return false;

        for (int i = 0; i < CrossBlastDx.Length; i++)
        {
            Tile candidate = GetTileAtGrid(state.Current.X + CrossBlastDx[i], state.Current.Y + CrossBlastDy[i]);
            if (CanLegallyMoveForHint(state, candidate))
                return true;
        }
        return false;
    }

    private int ApplyHintStep(HintSearchState state, Tile nextTile, List<string> debugEffects = null)
    {
        return TryApplyHintStep(state, nextTile, debugEffects, out int score) ? score : int.MinValue;
    }

    private bool TryApplyHintStep(HintSearchState state, Tile nextTile, List<string> debugEffects, out int score)
    {
        if (state == null || nextTile == null)
        {
            score = 0;
            return false;
        }

        Tile leavingTile = state.Current;
        if (!state.TryDecrement(leavingTile, out int beforeLeaving, out int afterLeaving))
        {
            score = 0;
            return false;
        }
        if (debugEffects != null)
            debugEffects.Add($"떠난 타일 {FormatHintTile(leavingTile)} {beforeLeaving}->{afterLeaving}");
        score = 12;

        var twinLink = leavingTile.GetComponent<TwinLinkTile>();
        if (twinLink != null)
        {
            foreach (TwinLinkTile partner in twinLink.Partners)
            {
                if (partner == null)
                    continue;

                Tile partnerTile = partner.GetComponent<Tile>();
                if (partnerTile == null || partnerTile == nextTile || partnerTile == state.Previous)
                    continue;

                if (state.GetRemainingCount(partnerTile) <= 0)
                    continue;

                if (!state.TryDecrement(partnerTile, out int beforePartner, out int afterPartner))
                {
                    score = 0;
                    return false;
                }
                if (debugEffects != null)
                    debugEffects.Add($"TwinLink {FormatHintTile(partnerTile)} {beforePartner}->{afterPartner}");
                score += 10;
            }
        }

        if (leavingTile.GetComponent<CrossBlastTile>() != null)
        {
            int affected = CollectHintCrossBlastTargets(leavingTile, nextTile, state);
            for (int i = 0; i < affected; i++)
            {
                Tile affectedTile = crossBlastScratchTiles[i];
                if (!state.TryDecrement(affectedTile, out int beforeAffected, out int afterAffected))
                {
                    score = 0;
                    return false;
                }
                if (debugEffects != null)
                    debugEffects.Add($"CrossBlast {FormatHintTile(affectedTile)} {beforeAffected}->{afterAffected}");
                score += 9;
            }
            if (affected > 0)
                score += affected * 3;
        }

        if (leavingTile.GetComponent<ChargeTile>() != null)
        {
            for (int i = 0; i < CrossBlastDx.Length; i++)
            {
                Tile neighbor = GetTileAtGrid(leavingTile.X + CrossBlastDx[i], leavingTile.Y + CrossBlastDy[i]);
                if (neighbor == null || neighbor == nextTile || state.GetRemainingCount(neighbor) <= 0 || !ChargeTile.CanBeCharged(neighbor))
                    continue;
                state.Increment(neighbor);
                if (debugEffects != null)
                    debugEffects.Add($"Charge {FormatHintTile(neighbor)} +1");
                score -= 4;
            }
        }

        var igniter = nextTile.GetComponent<IgniterTile>();
        if (igniter != null && state.TryTriggerIgniter(igniter))
        {
            if (debugEffects != null)
                debugEffects.Add($"Igniter target={igniter.TargetID} Hidden 활성화");
            score += 18;
        }

        var fixedKnot = nextTile.GetComponent<FixedKnotTile>();
        if (fixedKnot != null && !state.IsFixedKnotSolved(nextTile))
        {
            state.MarkFixedKnotSolved(nextTile);
            if (debugEffects != null)
                debugEffects.Add($"FixedKnot 순서 타일 진입 {FormatHintTile(nextTile)}");
            score += 8;
        }
        if (leavingTile.GetComponent<TwinLinkTile>() != null || leavingTile.GetComponent<CrossBlastTile>() != null)
            score += 5;

        state.Previous = leavingTile;
        state.Current = nextTile;
        state.NextStepNumber++;
        return true;
    }

    private int CollectHintCrossBlastTargets(Tile centerTile, Tile nextTile, HintSearchState state)
    {
        ClearCrossBlastResultBuffer(crossBlastScratchTiles);
        if (centerTile == null || nextTile == null || state == null)
            return 0;

        int count = 0;
        for (int i = 0; i < CrossBlastDx.Length && count < crossBlastScratchTiles.Length; i++)
        {
            Tile candidate = GetTileAtGrid(centerTile.X + CrossBlastDx[i], centerTile.Y + CrossBlastDy[i]);
            if (candidate == null || candidate == nextTile)
                continue;
            // 실제 게임(GetCrossBlastAffectedTiles)과 동일하게, 아직 잠긴 Hidden 타일도 남은 숫자가 있으면 감소 대상이다.
            if (state.GetRemainingCount(candidate) <= 0)
                continue;
            crossBlastScratchTiles[count++] = candidate;
        }

        return count;
    }

    private bool IsCurrentBoardLiveRemainingTile(Tile tile)
    {
        if (tile == null || tile.CurrentNumber <= 0)
            return false;

        var hidden = tile.GetComponent<HiddenTile>();
        return hidden == null || hidden.IsLive;
    }

    private bool CanLegallyMoveWithKernel(
        Tile from,
        Tile to,
        TwinLinkLegalMoveContext context,
        Func<Tile, int> getRemainingCount,
        Func<Tile, bool> isLiveRemainingTile,
        out FixedKnotTile fixedKnotHit,
        out LegalMoveBlockReason blockReason,
        bool ignoreFixedKnotOrder = false,
        bool ignoreTwinLinkPartners = false)
    {
        fixedKnotHit = to != null ? to.GetComponent<FixedKnotTile>() : null;
        blockReason = LegalMoveBlockReason.None;
        if (from == null || to == null || getRemainingCount == null || isLiveRemainingTile == null)
        {
            blockReason = LegalMoveBlockReason.NullOrInactive;
            return false;
        }

        if (!isLiveRemainingTile(from) || !isLiveRemainingTile(to))
        {
            var hidden = to.GetComponent<HiddenTile>();
            blockReason = hidden != null ? LegalMoveBlockReason.HiddenLocked : LegalMoveBlockReason.NullOrInactive;
            return false;
        }

        if (!IsAdjacent(from, to))
        {
            blockReason = LegalMoveBlockReason.NullOrInactive;
            return false;
        }

        var shortCircuitHit = to.GetComponent<ShortCircuitTile>();
        if (shortCircuitHit != null && shortCircuitHit.IsBlockedEntryFrom(from.X, from.Y))
        {
            blockReason = LegalMoveBlockReason.ShortCircuitBlocked;
            return false;
        }

        var shortCircuitFrom = from.GetComponent<ShortCircuitTile>();
        if (shortCircuitFrom != null && !shortCircuitFrom.IsExitCell(to.X, to.Y))
        {
            blockReason = LegalMoveBlockReason.ShortCircuitBlocked;
            return false;
        }

        if (!ignoreFixedKnotOrder && fixedKnotHit != null && !fixedKnotHit.CanEnter(context.NextStepNumber))
        {
            blockReason = LegalMoveBlockReason.FixedKnotOrder;
            return false;
        }

        if (!ignoreTwinLinkPartners)
        {
            var twinLinkFrom = from.GetComponent<TwinLinkTile>();
            if (twinLinkFrom != null && !CanConsumeTwinLinkPartnersForMoveKernel(twinLinkFrom, context.CandidateNextTile, context.PreviousTile, getRemainingCount))
            {
                blockReason = LegalMoveBlockReason.TwinLinkPartner;
                return false;
            }
        }

        return true;
    }

    private bool CanConsumeTwinLinkPartnersForMoveKernel(TwinLinkTile sourceTwinLink, Tile candidateNextTile, Tile previousTile, Func<Tile, int> getRemainingCount)
    {
        if (sourceTwinLink == null || getRemainingCount == null)
            return true;

        foreach (TwinLinkTile partner in sourceTwinLink.Partners)
        {
            if (partner == null)
                continue;

            Tile partnerTile = partner.GetComponent<Tile>();
            if (partnerTile == null)
                continue;

            if (partnerTile == candidateNextTile || partnerTile == previousTile)
                continue;

            if (getRemainingCount(partnerTile) <= 0)
                continue;
        }

        return true;
    }

    private void RenderHintPreview(List<Tile> path)
    {
        if (path == null || path.Count <= 1)
            return;

        int segmentCount = path.Count - 1;
        EnsureHintPreviewPool(segmentCount);
        float width = Mathf.Max(0.05f, Mathf.Min(tileWidth, tileHeight) * Mathf.Max(0.01f, hintPreviewLineWidthScale));
        if (VisualTheme.IsSoftClay)
            width *= 1.3f;
        Color baseColor = GetHintPreviewColor(hintPreviewMaxAlpha * HintBaseAlphaMultiplier, false);

        activeHintPreviewPoints.Clear();
        activeHintPreviewTiles.Clear();
        hintPreviewFollowIndex = 0;
        for (int i = 0; i <= segmentCount; i++)
        {
            Vector3 point = path[i].transform.position;
            point.z = -0.45f;
            activeHintPreviewPoints.Add(point);
            activeHintPreviewTiles.Add(path[i]);
        }

        EnsureHintSegmentArrowPool(segmentCount);
        float tileSizeForOffset = Mathf.Max(0.01f, Mathf.Min(tileWidth, tileHeight));
        var edgeVisits = new Dictionary<long, int>();
        for (int i = 0; i < hintPreviewLines.Length; i++)
        {
            LineRenderer line = hintPreviewLines[i];
            bool active = i < segmentCount;
            line.enabled = active;
            SetHintSegmentArrowEnabled(i, active);
            if (!active)
                continue;

            // 같은 두 칸을 오가는 경로는 선이 겹쳐 안 보이므로, 다시 지나는 구간을 옆으로 살짝 띄운다.
            Vector3 a = activeHintPreviewPoints[i];
            Vector3 b = activeHintPreviewPoints[i + 1];
            long edgeKey = HintEdgeKey(path[i], path[i + 1]);
            edgeVisits.TryGetValue(edgeKey, out int visits);
            edgeVisits[edgeKey] = visits + 1;
            if (visits > 0)
            {
                Vector3 dir = (b - a).normalized;
                Vector3 side = new Vector3(-dir.y, dir.x, 0f);
                float offset = tileSizeForOffset * 0.13f * ((visits + 1) / 2) * (visits % 2 == 1 ? 1f : -1f);
                a += side * offset;
                b += side * offset;
            }

            if (VisualTheme.IsSoftClay)
                TrimHintSegment(ref a, ref b, tileSizeForOffset);
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            line.widthMultiplier = width;
            line.startColor = baseColor;
            line.endColor = baseColor;
            PlaceHintSegmentArrow(i, a, b, baseColor, width);
        }

        activeHintPreviewLineCount = segmentCount;
        ConfigureHintPreviewFlowLine(width);
        if (hintPreviewRoutine != null)
            StopCoroutine(hintPreviewRoutine);
        hintPreviewRoutine = StartCoroutine(HintPreviewLifetimeRoutine());
    }

    /// <summary>
    /// 점토 테마: 힌트 선을 칸 중심에서 중심까지가 아니라 칸 가장자리 사이(틈)만 잇게 줄여 숫자를 가리지 않는다.
    /// </summary>
    private static void TrimHintSegment(ref Vector3 a, ref Vector3 b, float tileSize)
    {
        Vector3 dir = b - a;
        float length = dir.magnitude;
        if (length < 0.0001f)
            return;
        dir /= length;
        float trim = Mathf.Min(tileSize * HintClayTrimRatio, length * 0.42f);
        a += dir * trim;
        b -= dir * trim;
    }

    private const float HintClayTrimRatio = 0.4f;
    // 점토 테마: 칸 사이 틈이 짧아 흐르는 화살촉도 작게 그린다.
    private const float HintClayFlowArrowLengthScale = 0.15f;
    private const float HintClayFlowArrowWidthScale = 0.11f;

    private float HintFlowArrowLength()
    {
        float tileSize = Mathf.Max(0.01f, Mathf.Min(tileWidth, tileHeight));
        return Mathf.Max(0.04f, tileSize * (VisualTheme.IsSoftClay ? HintClayFlowArrowLengthScale : Mathf.Max(0.01f, hintPreviewArrowLengthScale)));
    }

    private void EnsureHintPreviewPool(int requiredSegmentCount = HintPreviewMaxMoves)
    {
        requiredSegmentCount = Mathf.Max(HintPreviewMaxMoves, requiredSegmentCount);
        if (hintPreviewLines != null && hintPreviewLines.Length >= requiredSegmentCount)
        {
            EnsureHintPreviewFlowLine();
            return;
        }

        LineRenderer[] previousLines = hintPreviewLines;
        hintPreviewLines = new LineRenderer[requiredSegmentCount];
        int existingCount = previousLines != null ? previousLines.Length : 0;
        for (int i = 0; i < existingCount; i++)
            hintPreviewLines[i] = previousLines[i];

        for (int i = existingCount; i < hintPreviewLines.Length; i++)
        {
            hintPreviewLines[i] = CreateHintPreviewLine($"HintPreviewLine_{i}", 80);
        }
        EnsureHintPreviewFlowLine();
    }

    private void EnsureHintPreviewFlowLine()
    {
        if (hintPreviewFlowLine != null)
        {
            EnsureHintPreviewArrowLines();
            return;
        }

        hintPreviewFlowLine = CreateHintPreviewLine("HintPreviewFlowLine", 82);
        EnsureHintPreviewArrowLines();
    }

    private void EnsureHintPreviewArrowLines()
    {
        if (hintPreviewArrowLeftLine == null)
            hintPreviewArrowLeftLine = CreateHintPreviewLine("HintPreviewArrowLeftLine", 83);
        if (hintPreviewArrowRightLine == null)
            hintPreviewArrowRightLine = CreateHintPreviewLine("HintPreviewArrowRightLine", 83);
    }

    // 힌트 선은 하트비트로 커진 현재/시작 타일(정렬 +100)보다 항상 위에 그린다.
    private const int HintSortingLift = 320;
    private readonly List<KeyValuePair<LineRenderer, LineRenderer>> hintHaloPairs = new List<KeyValuePair<LineRenderer, LineRenderer>>();
    private readonly List<Vector3> hintHaloPositions = new List<Vector3>();

    /// <summary>점토 테마: 힌트 선 아래에 종이색 테두리(헤일로)를 같은 모양으로 맞춰 그려 타일 위에서도 잘 보이게 한다.</summary>
    private void SyncHintHalos()
    {
        for (int i = 0; i < hintHaloPairs.Count; i++)
        {
            LineRenderer line = hintHaloPairs[i].Key;
            LineRenderer halo = hintHaloPairs[i].Value;
            if (line == null || halo == null)
                continue;
            bool show = line.enabled && line.gameObject.activeInHierarchy;
            halo.enabled = show;
            if (!show)
                continue;
            int count = line.positionCount;
            halo.positionCount = count;
            for (int k = 0; k < count; k++)
                halo.SetPosition(k, line.GetPosition(k) + new Vector3(0f, 0f, 0.01f));
            halo.widthMultiplier = line.widthMultiplier * 1.75f;
            Color paper = VisualTheme.Paper;
            paper.a = Mathf.Clamp01(Mathf.Max(line.startColor.a, line.endColor.a) * 1.1f);
            halo.startColor = paper;
            halo.endColor = paper;
        }
    }

    private LineRenderer CreateHintPreviewLine(string lineName, int sortingOrder)
    {
        sortingOrder += HintSortingLift;
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);
        var line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.numCapVertices = 6;
        line.numCornerVertices = 4;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.material = ResolveHintPreviewMaterial();
        line.sortingOrder = sortingOrder;
        line.enabled = false;
        if (VisualTheme.IsSoftClay)
        {
            var haloObject = new GameObject(lineName + "_Halo");
            haloObject.transform.SetParent(transform, false);
            var halo = haloObject.AddComponent<LineRenderer>();
            halo.useWorldSpace = true;
            halo.positionCount = 2;
            halo.numCapVertices = 6;
            halo.numCornerVertices = 4;
            halo.alignment = LineAlignment.View;
            halo.material = line.material;
            halo.sortingOrder = sortingOrder - 10;
            halo.enabled = false;
            hintHaloPairs.Add(new KeyValuePair<LineRenderer, LineRenderer>(line, halo));
        }
        return line;
    }

    private void ConfigureHintPreviewFlowLine(float baseWidth)
    {
        if (hintPreviewFlowLine == null || activeHintPreviewLineCount <= 0 || activeHintPreviewPoints.Count <= 1)
            return;

        hintPreviewFlowLine.enabled = true;
        hintPreviewFlowLine.positionCount = 2;
        hintPreviewFlowLine.widthMultiplier = baseWidth * 1.75f;
        ConfigureHintPreviewArrowLine(hintPreviewArrowLeftLine, baseWidth);
        ConfigureHintPreviewArrowLine(hintPreviewArrowRightLine, baseWidth);
        UpdateHintPreviewFlow(0f, 1f);
    }

    private void ConfigureHintPreviewArrowLine(LineRenderer line, float baseWidth)
    {
        if (line == null)
            return;

        line.enabled = true;
        line.positionCount = 2;
        line.widthMultiplier = baseWidth * 1.35f;
    }

    private Material ResolveHintPreviewMaterial()
    {
        if (hintPreviewMaterial != null)
            return hintPreviewMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Default");
        if (shader == null)
        {
            Debug.LogWarning("[GameManager] Hint preview line shader not found.");
            return null;
        }

        hintPreviewMaterial = new Material(shader);
        return hintPreviewMaterial;
    }

    private IEnumerator HintPreviewLifetimeRoutine()
    {
        float pulseDuration = Mathf.Max(0.6f, hintPreviewDuration);
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            float pulse = 0.9f + 0.1f * GameplayFeedback.Intensity * Mathf.Sin((elapsed / pulseDuration) * Mathf.PI * 2f);
            SetHintPreviewAlpha(hintPreviewMaxAlpha * pulse);
            UpdateHintPreviewFlow(elapsed, 1f);
            yield return null;
        }
    }

    private void SetHintPreviewAlpha(float alpha)
    {
        Color baseColor = GetHintPreviewColor(Mathf.Clamp01(alpha * HintBaseAlphaMultiplier), false);
        if (hintPreviewLines == null)
            return;

        int count = Mathf.Min(activeHintPreviewLineCount, hintPreviewLines.Length);
        for (int i = 0; i < count; i++)
        {
            LineRenderer line = hintPreviewLines[i];
            if (line == null || !line.enabled)
                continue;
            line.startColor = baseColor;
            line.endColor = baseColor;
        }
    }

    private void UpdateHintPreviewFlow(float elapsed, float fade)
    {
        if (hintPreviewFlowLine == null || activeHintPreviewLineCount <= 0 || activeHintPreviewPoints.Count <= activeHintPreviewLineCount)
            return;

        int firstSegment = Mathf.Clamp(hintPreviewFollowIndex, 0, activeHintPreviewLineCount - 1);
        int remainingSegments = Mathf.Max(1, activeHintPreviewLineCount - firstSegment);
        // 구간당 0.25초로 천천히 흐르게 해 3~8수를 눈으로 따라갈 수 있게 한다.
        float sweepDuration = Mathf.Max(hintPreviewSweepDuration, HintSweepSecondsPerSegment * remainingSegments);
        float sweepProgress = Mathf.Repeat(elapsed, sweepDuration) / sweepDuration;
        float segmentProgress = firstSegment + sweepProgress * remainingSegments;
        int segmentIndex = Mathf.Clamp(Mathf.FloorToInt(segmentProgress), firstSegment, activeHintPreviewLineCount - 1);
        float t = Mathf.Clamp01(segmentProgress - segmentIndex);

        Vector3 start = activeHintPreviewPoints[segmentIndex];
        Vector3 end = activeHintPreviewPoints[segmentIndex + 1];
        float minT = 0.06f;
        if (VisualTheme.IsSoftClay)
        {
            TrimHintSegment(ref start, ref end, Mathf.Max(0.01f, Mathf.Min(tileWidth, tileHeight)));
            // 짧아진 구간에서 화살촉 날개가 뒤 칸 숫자로 넘어가지 않게 촉을 구간 안쪽에서 시작한다.
            float segmentLength = Vector3.Distance(start, end);
            if (segmentLength > 0.0001f)
                minT = Mathf.Clamp(HintFlowArrowLength() / segmentLength, 0.06f, 0.9f);
        }
        Vector3 head = Vector3.Lerp(start, end, Mathf.Max(t, minT));
        Vector3 direction = end - start;

        hintPreviewFlowLine.enabled = true;
        hintPreviewFlowLine.SetPosition(0, start);
        hintPreviewFlowLine.SetPosition(1, head);

        float headPulse = 0.8f + 0.2f * GameplayFeedback.Intensity * Mathf.Sin(Time.time * 18f);
        Color flowColor = GetHintPreviewColor(hintPreviewMaxAlpha * Mathf.Clamp01(fade) * headPulse, true);
        hintPreviewFlowLine.startColor = flowColor;
        hintPreviewFlowLine.endColor = flowColor;
        UpdateHintPreviewArrowHead(head, direction, flowColor);
    }

    private void UpdateHintPreviewArrowHead(Vector3 head, Vector3 direction, Color flowColor)
    {
        if (hintPreviewArrowLeftLine == null || hintPreviewArrowRightLine == null)
            return;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 dir = direction.normalized;
        Vector3 side = new Vector3(-dir.y, dir.x, 0f);
        float tileSize = Mathf.Max(0.01f, Mathf.Min(tileWidth, tileHeight));
        float arrowLength = HintFlowArrowLength();
        float arrowWidth = Mathf.Max(0.03f, tileSize * (VisualTheme.IsSoftClay ? HintClayFlowArrowWidthScale : Mathf.Max(0.01f, hintPreviewArrowWidthScale)));
        Vector3 back = head - dir * arrowLength;
        Vector3 left = back + side * arrowWidth;
        Vector3 right = back - side * arrowWidth;

        SetHintPreviewArrowLine(hintPreviewArrowLeftLine, head, left, flowColor);
        SetHintPreviewArrowLine(hintPreviewArrowRightLine, head, right, flowColor);
    }

    private void SetHintPreviewArrowLine(LineRenderer line, Vector3 head, Vector3 tail, Color color)
    {
        if (line == null)
            return;

        line.enabled = true;
        line.SetPosition(0, head);
        line.SetPosition(1, tail);
        line.startColor = color;
        line.endColor = color;
    }

    /// <summary>드래그를 시작해도 힌트를 지우지 않는다. 힌트대로 따라가면 지나간 구간만 지워진다.</summary>
    private void BeginFollowingHintPreview()
    {
        if (activeHintPreviewLineCount <= 0 || activeHintPreviewTiles.Count == 0)
            return;
        if (activeHintPreviewTiles[hintPreviewFollowIndex] != currentStartTile)
            ClearHintPreview();
    }

    private void AdvanceHintPreviewFollow(Tile enteredTile)
    {
        if (activeHintPreviewLineCount <= 0 || activeHintPreviewTiles.Count == 0)
            return;

        int nextIndex = hintPreviewFollowIndex + 1;
        if (nextIndex >= activeHintPreviewTiles.Count || activeHintPreviewTiles[nextIndex] != enteredTile)
        {
            // 힌트와 다른 길로 가면 힌트를 지운다.
            ClearHintPreview();
            return;
        }

        if (hintPreviewLines != null && hintPreviewFollowIndex < hintPreviewLines.Length && hintPreviewLines[hintPreviewFollowIndex] != null)
            hintPreviewLines[hintPreviewFollowIndex].enabled = false;
        SetHintSegmentArrowEnabled(hintPreviewFollowIndex, false);
        hintPreviewFollowIndex = nextIndex;
        if (hintPreviewFollowIndex >= activeHintPreviewLineCount)
            ClearHintPreview();
    }

    private static long HintEdgeKey(Tile a, Tile b)
    {
        int ia = a != null ? a.Y * 64 + a.X : 0;
        int ib = b != null ? b.Y * 64 + b.X : 0;
        return ia < ib ? ((long)ia << 32) | (uint)ib : ((long)ib << 32) | (uint)ia;
    }

    private void EnsureHintSegmentArrowPool(int segmentCount)
    {
        int required = Mathf.Max(HintPreviewMaxMoves, segmentCount) * 2;
        if (hintSegmentArrowLines != null && hintSegmentArrowLines.Length >= required)
            return;

        LineRenderer[] previous = hintSegmentArrowLines;
        hintSegmentArrowLines = new LineRenderer[required];
        int existing = previous != null ? previous.Length : 0;
        for (int i = 0; i < existing; i++)
            hintSegmentArrowLines[i] = previous[i];
        for (int i = existing; i < required; i++)
            hintSegmentArrowLines[i] = CreateHintPreviewLine($"HintSegmentArrow_{i}", 81);
    }

    private void SetHintSegmentArrowEnabled(int segmentIndex, bool enabled)
    {
        if (hintSegmentArrowLines == null)
            return;
        for (int k = 0; k < 2; k++)
        {
            int index = segmentIndex * 2 + k;
            if (index >= 0 && index < hintSegmentArrowLines.Length && hintSegmentArrowLines[index] != null)
                hintSegmentArrowLines[index].enabled = enabled;
        }
    }

    /// <summary>구간마다 가운데에 작은 화살촉을 그려 방향을 한눈에 알 수 있게 한다.</summary>
    private void PlaceHintSegmentArrow(int segmentIndex, Vector3 from, Vector3 to, Color color, float baseWidth)
    {
        if (hintSegmentArrowLines == null)
            return;
        Vector3 direction = to - from;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 dir = direction.normalized;
        Vector3 side = new Vector3(-dir.y, dir.x, 0f);
        float tileSize = Mathf.Max(0.01f, Mathf.Min(tileWidth, tileHeight));
        Vector3 head = Vector3.Lerp(from, to, VisualTheme.IsSoftClay ? 0.78f : 0.62f);
        Vector3 back = head - dir * tileSize * 0.14f;
        for (int k = 0; k < 2; k++)
        {
            int index = segmentIndex * 2 + k;
            if (index >= hintSegmentArrowLines.Length || hintSegmentArrowLines[index] == null)
                continue;
            LineRenderer line = hintSegmentArrowLines[index];
            line.enabled = true;
            line.positionCount = 2;
            line.widthMultiplier = baseWidth * 0.9f;
            line.SetPosition(0, head);
            line.SetPosition(1, back + side * tileSize * (k == 0 ? 0.1f : -0.1f));
            line.startColor = color;
            line.endColor = color;
        }
    }

    // 점토 테마 힌트: 진한 테라코타(바탕 선은 조금 더 진하게) + 종이색 헤일로.
    private static readonly Color HintClayLineColor = new Color32(0xB8, 0x4E, 0x2C, 0xFF);
    private static readonly Color HintClayFlowColor = new Color32(0xE0, 0x6A, 0x3E, 0xFF);
    private float HintBaseAlphaMultiplier => VisualTheme.IsSoftClay ? 0.9f : hintPreviewBaseAlphaMultiplier;

    private Color GetHintPreviewColor(float alpha, bool flow)
    {
        Color color = VisualTheme.IsSoftClay ? (flow ? HintClayFlowColor : HintClayLineColor) : (flow ? HintFlowColor : HintLineColor);
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private void ClearHintPreview(bool stopRoutine = true)
    {
        if (stopRoutine && hintPreviewRoutine != null)
        {
            StopCoroutine(hintPreviewRoutine);
            hintPreviewRoutine = null;
        }

        activeHintPreviewLineCount = 0;
        activeHintPreviewPoints.Clear();
        activeHintPreviewTiles.Clear();
        hintPreviewFollowIndex = 0;
        if (hintSegmentArrowLines != null)
        {
            for (int i = 0; i < hintSegmentArrowLines.Length; i++)
                if (hintSegmentArrowLines[i] != null)
                    hintSegmentArrowLines[i].enabled = false;
        }
        if (hintPreviewFlowLine != null)
            hintPreviewFlowLine.enabled = false;
        if (hintPreviewArrowLeftLine != null)
            hintPreviewArrowLeftLine.enabled = false;
        if (hintPreviewArrowRightLine != null)
            hintPreviewArrowRightLine.enabled = false;
        if (hintPreviewLines == null)
            return;

        for (int i = 0; i < hintPreviewLines.Length; i++)
        {
            if (hintPreviewLines[i] != null)
                hintPreviewLines[i].enabled = false;
        }
    }

    private struct HintMoveCandidate
    {
        public readonly Tile Tile;
        public readonly int Score;

        public HintMoveCandidate(Tile tile, int score)
        {
            Tile = tile;
            Score = score;
        }
    }

    private sealed class HintSolverBudget
    {
        private readonly int nodeLimit;
        private readonly float timeLimitSeconds;
        // 프레임을 나눠 탐색하므로 벽시계가 아니라 실제 계산 시간만 센다.
        private readonly System.Diagnostics.Stopwatch computeWatch = new System.Diagnostics.Stopwatch();
        private string status = "running";

        public int NodeCount { get; private set; }
        public bool IsExhausted { get; private set; }
        public string Status => IsExhausted ? status : "solved";

        public HintSolverBudget(int nodeLimit, float timeLimitSeconds)
        {
            this.nodeLimit = Mathf.Max(1, nodeLimit);
            this.timeLimitSeconds = Mathf.Max(0.01f, timeLimitSeconds);
        }

        public void ResumeClock() => computeWatch.Start();
        public void PauseClock() => computeWatch.Stop();

        public bool TryEnterNode()
        {
            if (IsExhausted)
                return false;

            NodeCount++;
            if (NodeCount > nodeLimit)
            {
                IsExhausted = true;
                status = "node_limit";
                return false;
            }

            if ((NodeCount & 63) == 0 && computeWatch.Elapsed.TotalSeconds > timeLimitSeconds)
            {
                IsExhausted = true;
                status = "time_limit";
                return false;
            }

            return true;
        }
    }

    private sealed class HintSearchState
    {
        private readonly GameManager owner;
        private readonly Dictionary<Tile, int> remainingCounts;
        private readonly HashSet<Tile> liveHiddenTiles;
        private readonly HashSet<string> triggeredIgniterTargets;
        private readonly HashSet<Tile> solvedFixedKnots;

        public Tile Current;
        public Tile Previous;
        public int NextStepNumber;
        public int TotalRemainingCount { get; private set; }

        public HintSearchState(GameManager owner, Tile current, Tile previous, int nextStepNumber)
        {
            this.owner = owner;
            Current = current;
            Previous = previous;
            NextStepNumber = nextStepNumber;
            remainingCounts = new Dictionary<Tile, int>();
            liveHiddenTiles = new HashSet<Tile>();
            triggeredIgniterTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            solvedFixedKnots = new HashSet<Tile>();
            CaptureCurrentBoard();
        }

        public HintSearchState(HintSearchState other)
        {
            owner = other.owner;
            Current = other.Current;
            Previous = other.Previous;
            NextStepNumber = other.NextStepNumber;
            TotalRemainingCount = other.TotalRemainingCount;
            remainingCounts = new Dictionary<Tile, int>(other.remainingCounts);
            liveHiddenTiles = new HashSet<Tile>(other.liveHiddenTiles);
            triggeredIgniterTargets = new HashSet<string>(other.triggeredIgniterTargets, StringComparer.OrdinalIgnoreCase);
            solvedFixedKnots = new HashSet<Tile>(other.solvedFixedKnots);
        }

        private void CaptureCurrentBoard()
        {
            if (owner == null || owner.tiles == null)
                return;

            for (int row = 0; row < owner.stageHeight; row++)
            {
                for (int col = 0; col < owner.stageWidth; col++)
                {
                    Tile tile = owner.tiles[row, col];
                    if (tile == null)
                        continue;

                    int count = Mathf.Max(0, tile.CurrentNumber);
                    remainingCounts[tile] = count;
                    TotalRemainingCount += count;

                    var hidden = tile.GetComponent<HiddenTile>();
                    if (hidden != null && hidden.IsLive)
                        liveHiddenTiles.Add(tile);

                    var igniter = tile.GetComponent<IgniterTile>();
                    if (igniter != null && igniter.HasTriggered)
                        triggeredIgniterTargets.Add(GetIgniterTargetID(igniter));

                    var fixedKnot = tile.GetComponent<FixedKnotTile>();
                    if (fixedKnot != null && fixedKnot.HasBeenSteppedCorrectly)
                        solvedFixedKnots.Add(tile);
                }
            }
        }

        public int GetRemainingCount(Tile tile)
        {
            if (tile == null)
                return 0;
            return remainingCounts.TryGetValue(tile, out int count) ? count : 0;
        }

        public bool IsLiveRemainingTile(Tile tile)
        {
            if (tile == null || GetRemainingCount(tile) <= 0)
                return false;

            var hidden = tile.GetComponent<HiddenTile>();
            return hidden == null || liveHiddenTiles.Contains(tile);
        }

        public void Increment(Tile tile)
        {
            if (tile == null || !remainingCounts.TryGetValue(tile, out int count) || count <= 0)
                return;
            remainingCounts[tile] = count + 1;
            TotalRemainingCount++;
        }

        public int Decrement(Tile tile)
        {
            return TryDecrement(tile, out _, out _) ? 1 : 0;
        }

        public bool TryDecrement(Tile tile, out int before, out int after)
        {
            before = 0;
            after = 0;
            if (tile == null)
                return false;

            if (!remainingCounts.TryGetValue(tile, out int count) || count <= 0)
            {
                before = count;
                after = count;
                return false;
            }

            before = count;
            after = count - 1;
            remainingCounts[tile] = after;
            TotalRemainingCount = Mathf.Max(0, TotalRemainingCount - 1);
            return true;
        }

        public bool TryTriggerIgniter(IgniterTile igniter)
        {
            if (owner == null || igniter == null)
                return false;

            string targetID = GetIgniterTargetID(igniter);
            if (!triggeredIgniterTargets.Add(targetID))
                return false;

            if (owner.hiddenGroups == null || !owner.hiddenGroups.TryGetValue(targetID, out List<HiddenTile> hiddenTiles) || hiddenTiles == null)
                return false;

            bool activatedAny = false;
            foreach (HiddenTile hidden in hiddenTiles)
            {
                if (hidden == null)
                    continue;

                Tile hiddenTile = hidden.GetComponent<Tile>();
                if (hiddenTile == null || GetRemainingCount(hiddenTile) <= 0)
                    continue;

                if (liveHiddenTiles.Add(hiddenTile))
                    activatedAny = true;
            }

            return activatedAny;
        }

        public void MarkFixedKnotSolved(Tile tile)
        {
            if (tile != null)
                solvedFixedKnots.Add(tile);
        }

        public bool IsFixedKnotSolved(Tile tile)
        {
            return tile != null && solvedFixedKnots.Contains(tile);
        }

        public bool HasMissedFixedKnot(int completedStepNumber)
        {
            if (owner == null || owner.tiles == null)
                return false;

            for (int row = 0; row < owner.stageHeight; row++)
            {
                for (int col = 0; col < owner.stageWidth; col++)
                {
                    Tile tile = owner.tiles[row, col];
                    if (tile == null || solvedFixedKnots.Contains(tile) || GetRemainingCount(tile) <= 0)
                        continue;

                    var fixedKnot = tile.GetComponent<FixedKnotTile>();
                    if (fixedKnot != null && completedStepNumber >= fixedKnot.TargetOrder)
                        return true;
                }
            }

            return false;
        }

        public string BuildCacheKey()
        {
            StringBuilder sb = new StringBuilder(256);
            AppendTileKey(sb, Current);
            sb.Append('|');
            AppendTileKey(sb, Previous);
            sb.Append('|').Append(NextStepNumber).Append('|');

            if (owner != null && owner.tiles != null)
            {
                for (int row = 0; row < owner.stageHeight; row++)
                {
                    for (int col = 0; col < owner.stageWidth; col++)
                    {
                        Tile tile = owner.tiles[row, col];
                        if (tile == null)
                        {
                            sb.Append("x,");
                            continue;
                        }

                        sb.Append(GetRemainingCount(tile)).Append(',');
                    }
                }

                sb.Append('|');
                for (int row = 0; row < owner.stageHeight; row++)
                {
                    for (int col = 0; col < owner.stageWidth; col++)
                    {
                        Tile tile = owner.tiles[row, col];
                        if (tile == null)
                            continue;

                        if (tile.GetComponent<HiddenTile>() != null)
                            sb.Append(liveHiddenTiles.Contains(tile) ? '1' : '0');
                    }
                }

                sb.Append('|');
                for (int row = 0; row < owner.stageHeight; row++)
                {
                    for (int col = 0; col < owner.stageWidth; col++)
                    {
                        Tile tile = owner.tiles[row, col];
                        if (tile == null)
                            continue;

                        var igniter = tile.GetComponent<IgniterTile>();
                        if (igniter != null)
                            sb.Append(triggeredIgniterTargets.Contains(GetIgniterTargetID(igniter)) ? '1' : '0');
                    }
                }

                sb.Append('|');
                for (int row = 0; row < owner.stageHeight; row++)
                {
                    for (int col = 0; col < owner.stageWidth; col++)
                    {
                        Tile tile = owner.tiles[row, col];
                        if (tile == null)
                            continue;

                        if (tile.GetComponent<FixedKnotTile>() != null)
                            sb.Append(solvedFixedKnots.Contains(tile) ? '1' : '0');
                    }
                }
            }

            return sb.ToString();
        }

        private static void AppendTileKey(StringBuilder sb, Tile tile)
        {
            if (tile == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append(tile.X).Append(',').Append(tile.Y);
        }
    }

    private void SetAllTilesDisplayAsQuestion(bool showAsQuestion)
    {
        if (tiles == null) return;
        for (int row = 0; row < stageHeight; row++)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                Tile tile = tiles[row, col];
                if (tile == null) continue;
                tile.SetDisplayAsQuestion(showAsQuestion);
                var blindCurtain = tile.GetComponent<BlindCurtainTile>();
                if (blindCurtain != null)
                    blindCurtain.RefreshVisualState();
            }
        }
    }

    private void ResetAllTileQuestionRotations()
    {
        if (tiles == null) return;
        for (int row = 0; row < stageHeight; row++)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                Tile tile = tiles[row, col];
                if (tile == null) continue;
                var numberText = tile.GetNumberText();
                if (numberText != null)
                    numberText.transform.localEulerAngles = Vector3.zero;
            }
        }
    }

    private void TriggerBlackoutQuestionFlip()
    {
        if (blackoutQuestionFlipPlayed)
        {
            if (blackoutQuestionFlipRoutine == null)
            {
                SetAllTilesDisplayAsQuestion(true);
                ResetAllTileQuestionRotations();
            }
            return;
        }

        blackoutQuestionFlipPlayed = true;
        if (blackoutQuestionFlipRoutine != null)
        {
            StopCoroutine(blackoutQuestionFlipRoutine);
            blackoutQuestionFlipRoutine = null;
        }
        ResetAllTileQuestionRotations();
        if (GameplayFeedback.Intensity <= 0f)
        {
            SetAllTilesDisplayAsQuestion(true);
            return;
        }
        blackoutQuestionFlipRoutine = StartCoroutine(ShowAllTilesAsQuestionWithAnimation());
    }

    private IEnumerator ShowAllTilesAsQuestionWithAnimation()
    {
        if (tiles == null) yield break;

        // 연출 도중 스테이지가 바뀌면(클리어 직후 다음 스테이지 로드 등) 보드 크기가 달라지므로,
        // 시작 시점의 보드를 붙잡아 두고 바뀌면 즉시 중단한다(예전에는 IndexOutOfRange 예외).
        Tile[,] boardTiles = tiles;
        int boardHeight = Mathf.Min(stageHeight, boardTiles.GetLength(0));
        int boardWidth = Mathf.Min(stageWidth, boardTiles.GetLength(1));
        float rowDuration = Mathf.Max(0.01f, blackoutFlipDuration);
        float rowDelay = Mathf.Max(0f, blackoutRowInterval);
        float totalDuration = Mathf.Max(rowDuration, (boardHeight - 1) * rowDelay + rowDuration);
        bool[] rowSwitched = new bool[boardHeight];
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            if (tiles != boardTiles)
            {
                blackoutQuestionFlipRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;

            for (int row = boardHeight - 1; row >= 0; row--)
            {
                float rowStart = (boardHeight - 1 - row) * rowDelay;
                float localElapsed = elapsed - rowStart;
                float progress = localElapsed / rowDuration;
                float yAngle = progress < 0f ? 0f : (progress > 1f ? 360f : 360f * progress);

                for (int col = 0; col < boardWidth; col++)
                {
                    Tile tile = boardTiles[row, col];
                    if (tile == null) continue;

                    var numberText = tile.GetNumberText();
                    if (numberText != null)
                        numberText.transform.localEulerAngles = new Vector3(0f, yAngle, 0f);

                    if (progress >= 0.5f && !rowSwitched[row])
                    {
                        tile.SetDisplayAsQuestion(true);
                        var blindCurtain = tile.GetComponent<BlindCurtainTile>();
                        if (blindCurtain != null)
                            blindCurtain.RefreshVisualState();
                    }
                }

                if (progress >= 0.5f)
                    rowSwitched[row] = true;
            }

            yield return null;
        }

        if (tiles == boardTiles)
        {
            SetAllTilesDisplayAsQuestion(true);
            ResetAllTileQuestionRotations();
        }
        blackoutQuestionFlipRoutine = null;
    }

    private void StopBlackoutQuestionFlip()
    {
        if (blackoutQuestionFlipRoutine != null)
        {
            StopCoroutine(blackoutQuestionFlipRoutine);
            blackoutQuestionFlipRoutine = null;
        }
        blackoutQuestionFlipPlayed = false;
    }

    /// <summary>현재 스테이지 기준으로 상단 UI(스테이지/진행도) 초기화.</summary>
    private void RefreshMainUIForStage()
    {
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI == null)
            return;

        int total = GetTotalRemainingCount();
        initialTileCountForUI = Mathf.Max(1, total);
        mainUI.SetupStage(currentStageIndex, initialTileCountForUI, total);
    }

    private void ResetMainUIHeartsForNewStage()
    {
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI != null)
            mainUI.ResetHeartsForNewStage();
    }

    private void RestoreMainUIHeartsForSavedSession()
    {
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI != null)
            mainUI.RestoreHeartsForSavedSession();
    }

    /// <summary>남은 타일 카운트 기준으로 상단 UI ProgressBar 갱신.</summary>
    private void RefreshMainUIProgress()
    {
        // 피버 중에는 진행 막대가 남은 시간을 보여준다.
        if (feverActive)
            return;
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI == null || initialTileCountForUI <= 0)
            return;

        int remaining = GetTotalRemainingCount();
        mainUI.UpdateProgress(remaining);
        gameplayFeedback?.Progress(remaining, initialTileCountForUI, currentPath.Count > 0 ? currentPath[currentPath.Count - 1] : currentStartTile);
    }

    /// <summary>남은 합이 1이고 현재 타일도 1이면 마지막 타일을 소비하고 즉시 클리어한다.</summary>
    private bool CheckVictoryCondition(Tile currentTile)
    {
        if (stageCleared || tiles == null || currentTile == null)
            return false;

        int totalRemaining = GetTotalRemainingCount();
        if (currentTile.CurrentNumber == 1)
        {
            if (totalRemaining == 1)
            {
                DecreaseTileAndPlayBlockNote(currentTile);
                RefreshMainUIProgress();
                BeginStageClear(StageClearTypeLastTileRule, currentTile);
                return true;
            }

            var twinLink = currentTile.GetComponent<TwinLinkTile>();
            if (twinLink != null && twinLink.AreAllPartnersAtCount(1))
            {
                int twinGroupRemaining = currentTile.CurrentNumber + twinLink.GetPartnerRemainingCount();
                if (totalRemaining == twinGroupRemaining)
                {
                    DecreaseTileAndPlayBlockNote(currentTile);
                    twinLink.ConsumePartners(DecreaseTileAndPlayBlockNote);
                    RefreshMainUIProgress();
                    BeginStageClear(StageClearTypeLastTileRule, currentTile);
                    return true;
                }
            }
        }

        return false;
    }

    private void BeginStageClear(string clearType, Tile referenceTile)
    {
        if (stageCleared)
            return;

        stageCleared = true;
        if (feverActive)
        {
            ResetActiveDragState("fever_clear");
            ClearHintPreview();
            PlayClearSfx();
            gameplayFeedback?.Cleared(referenceTile);
            OnFeverBoardFinished(true);
            return;
        }
        RecordStageStars();
        ResetActiveDragState("stage_clear");
        DevLog.Log("Clear");
        ClearHintPreview();
        PlayClearSfx();
        PlayStageClearHaptic();
        PlayStageClearVisuals(referenceTile);
        gameplayFeedback?.Cleared(referenceTile);
        PlayClearStarsToHearts(referenceTile);
        if (lastClearWasOneStroke && referenceTile != null)
            mainUI?.ShowPerfectPopup(referenceTile.transform.position, mainCamera != null ? mainCamera : Camera.main);
        TrackStageCleared(clearType);
        if (FinalEndingProgressStore.IsFinalStageIndex(currentStageIndex))
        {
            FinalEndingSnapshot snapshot = FinalEndingProgressStore.CreateFinalSnapshot();
            FinalEndingProgressStore.SavePendingEndingResume(snapshot);
            StartCoroutine(LoadFinalEndingAfterDelay(snapshot));
            return;
        }

        StartCoroutine(LoadNextStageAfterDelay());
    }

    private const int ClearStarSourceCount = 10;
    private bool lastClearWasOneStroke;

    /// <summary>점토 테마: 부서진 칸들 중 몇 곳(마지막 칸 포함)에서 별이 상단 하트로 날아가 하트를 다시 채운다.</summary>
    private void PlayClearStarsToHearts(Tile referenceTile)
    {
        if (!VisualTheme.IsSoftClay || tiles == null)
            return;
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        Camera cam = mainCamera != null ? mainCamera : Camera.main;
        if (mainUI == null || cam == null)
            return;

        var candidates = new List<Vector3>();
        for (int row = 0; row < stageHeight; row++)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                Tile tile = tiles[row, col];
                if (tile != null && tile != referenceTile)
                    candidates.Add(tile.transform.position);
            }
        }
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        var sources = new List<Vector3>(ClearStarSourceCount);
        if (referenceTile != null)
            sources.Add(referenceTile.transform.position);
        for (int i = 0; i < candidates.Count && sources.Count < ClearStarSourceCount; i++)
            sources.Add(candidates[i]);
        mainUI.PlayClearStarsToHearts(sources, cam);
    }

    private void RecordStageStars()
    {
        // 클리어가 드래그 도중에 일어나면 그 드래그도 한 획으로 센다.
        int strokes = attemptStrokeCount + (isDragging && currentPath.Count > 1 ? 1 : 0);
        bool oneStroke = strokes == 1 && !attemptUsedUndo;
        lastClearWasOneStroke = oneStroke;
        int stars = 1 + (stageHintUsed ? 0 : 1) + (!stageHintUsed && !stageRetried ? 1 : 0);
        bool improved = StageStarsStore.Record(currentStageIndex, stars, oneStroke);
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        mainUI?.ShowStageClearResult(stars, oneStroke, improved);
        FirebaseBootstrap.LogEvent("stage_stars", new Dictionary<string, object>
        {
            { "stage_index", currentStageIndex },
            { "stars", stars },
            { "one_stroke", oneStroke ? 1 : 0 }
        });
    }

    /// <summary>FixedKnot 화면 갱신. totalStepsCommitted + currentPath 기준 Count 전달.</summary>
    private void NotifyFixedKnotsUpdateVisual(int totalPathCount)
    {
        if (tiles == null) return;
        for (int row = 0; row < stageHeight; row++)
            for (int col = 0; col < stageWidth; col++)
                if (tiles[row, col] != null)
                {
                    var fk = tiles[row, col].GetComponent<FixedKnotTile>();
                    if (fk != null) fk.UpdateVisual(totalPathCount);
                }
    }

    private bool IsAdjacent(Tile a, Tile b)
    {
        int dx = Mathf.Abs(a.X - b.X);
        int dy = Mathf.Abs(a.Y - b.Y);
        return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
    }

    /// <summary>
    /// 손 떼면: 실제로 타일을 옮겼을 때만 경로를 누적·카운트다운 갱신. 마지막 타일을 새 시작점으로 설정하고 하트비트 유지.
    /// </summary>
    private void CommitPathAndSetCurrentPosition()
    {
        // 실제로 다른 타일로 이동했을 때만 totalStepsCommitted 갱신 (시작점만 터치한 경우 제외)
        bool actuallyMoved = currentPath.Count > 1;
        if (actuallyMoved)
        {
            // Spotlight Normal 모드: 커밋된 경로 타일 위치를 영구 밝힘 목록에 추가
            if (spotlightController != null)
            {
                foreach (Tile t in currentPath)
                {
                    if (t != null)
                        spotlightController.AddRevealedPosition(t.transform.position);
                }
            }
            // 기어 스텝 수: 옮긴 횟수만. 첫 구간도 이어서도 새로 밟은 타일 수 = currentPath.Count - 1 (시작점 제외)
            totalStepsCommitted += (currentPath.Count - 1);
            attemptStrokeCount++;
            NotifyFixedKnotsUpdateVisual(totalStepsCommitted);

            if (pathLitClearRoutine != null)
                StopCoroutine(pathLitClearRoutine);
            pathLitClearRoutine = StartCoroutine(PathLitClearAfterDelayRoutine(pathLitClearDelay));

            // 마지막 타일(손 뗀 위치)은 여기서 -1 하지 않음. "다음 타일을 밟아서 떠날 때"만 OnLeaveTileForNext에서 -1 되므로,
            // count 1인 타일에서 손을 떼도 그 타일이 사라지지 않고 다음 드래그의 시작점으로 유지됨.
        }

        if (currentPath.Count > 0)
        {
            Tile lastTile = currentPath[currentPath.Count - 1];
            if (lastTile != null)
            {
                // Spotlight Hard: 손 뗀 뒤 새 시작점(하트비트 나오는 타일)도 밝혀 두어 다음 드래그 시 보이게
                if (spotlightController != null)
                    spotlightController.AddRevealedPositionForNewStart(lastTile.transform.position);
                if (currentStartTile != null && currentStartTile != lastTile)
                    currentStartTile.ClearScaleOverride();
                currentStartTile = lastTile;
                currentStartTile.SetCurrentPositionMarker(true);
            }
        }
        currentPath.Clear();
        SetNeonTrailEmitting(false);

        RefreshMainUIProgress();
    }

    private IEnumerator PathLitClearAfterDelayRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        linkSystem?.ClearPathLit();
        pathLitClearRoutine = null;
    }

    /// <summary>터치 시작 시 트레일 초기화 시퀀스: emitting 중단 → Clear → 위치 이동 → 1프레임 뒤 time 복구·emitting 재개.</summary>
    private void OnDragStartTrail()
    {
        if (neonTrail == null || neonTrailTransform == null) return;
        if (trailEmitDelayRoutine != null)
        {
            StopCoroutine(trailEmitDelayRoutine);
            trailEmitDelayRoutine = null;
        }
        neonTrail.emitting = false;
        neonTrail.Clear();
        neonTrail.time = 0f;
        Vector2 w = GetPointerWorldPosition();
        neonTrailTransform.position = new Vector3(w.x, w.y, -0.5f);
        trailEmitDelayRoutine = StartCoroutine(TrailEmitDelayRoutine());
    }

    private IEnumerator TrailEmitDelayRoutine()
    {
        yield return new WaitForEndOfFrame();
        if (neonTrail != null)
        {
            neonTrail.time = trailTime;
            neonTrail.emitting = true;
        }
        trailEmitDelayRoutine = null;
    }

    /// <summary>드래그 중에는 현재 프레임 포인터 월드 좌표만 position에 대입. emitting은 OnDragStartTrail 코루틴에서 1프레임 뒤 켬.</summary>
    private void UpdateNeonTrailPosition()
    {
        if (neonTrail == null || neonTrailTransform == null) return;
        if (isDragging)
        {
            Vector2 w = GetPointerWorldPosition();
            neonTrailTransform.position = new Vector3(w.x, w.y, -0.5f);
        }
        else
        {
            if (neonTrail != null)
                neonTrail.emitting = false;
        }
    }

    private void SetNeonTrailEmitting(bool emitting)
    {
        // 점토 테마는 네온 꼬리 대신 GameplayFeedback의 잉크 경로선만 쓴다.
        if (neonTrail != null)
            neonTrail.emitting = emitting && !VisualTheme.IsSoftClay;
    }

    /// <summary>트레일 강제 초기화. 드래그 종료·게임오버·리셋·스테이지 전환 시 호출. Clear() 포함.</summary>
    private void ResetTrail()
    {
        if (trailEmitDelayRoutine != null)
        {
            StopCoroutine(trailEmitDelayRoutine);
            trailEmitDelayRoutine = null;
        }
        if (neonTrail != null)
        {
            neonTrail.Clear();
            neonTrail.emitting = false;
        }
    }

    private void ResetActiveDragState(string reason)
    {
        bool hadActiveDrag = isDragging || currentPath.Count > 0;
        ClearMoveRuleSnackbarState();
        isDragging = false;
        currentPath.Clear();
        ResetTrail();
        SetNeonTrailEmitting(false);
        gameplayFeedback?.ClearPath();

        if (hadActiveDrag)
            DevLog.Log($"[드래그 상태 리셋] reason={reason}");
    }

    /// <summary>특수 타일(TwinLink, Igniter 등)을 밟으면 트레일 메인 컬러가 해당 대표색으로 0.2초간 Lerp 후 복귀.</summary>
    private void NotifyTrailTileStepped(Tile tile)
    {
        if (tile == null) return;
        var twin = tile.GetComponent<TwinLinkTile>();
        if (twin != null)
        {
            specialTileColor = twin.GetLinkColor();
            specialTileColorLerpStartTime = Time.time;
            return;
        }
        var igniter = tile.GetComponent<IgniterTile>();
        if (igniter != null)
        {
            specialTileColor = igniter.GetAccentColor();
            specialTileColorLerpStartTime = Time.time;
        }
    }

    private enum BoardFailureReason
    {
        None,
        NoLegalMove,
        UnreachableRemainingTiles,
        HiddenUntriggerable,
        ShortCircuitBlocked,
        TwinLinkUnsatisfiable,
        InvalidCurrentStart
    }

    private enum HiddenSnapshotState
    {
        None,
        Locked,
        Pending,
        Live
    }

    private enum LegalMoveBlockReason
    {
        None,
        NullOrInactive,
        HiddenLocked,
        ShortCircuitBlocked,
        FixedKnotOrder,
        TwinLinkPartner
    }

    private struct BoardFailureAnalysisResult
    {
        public bool ShouldFail;
        public bool ShouldDefer;
        public BoardFailureReason Reason;
        public Tile FocusTile;

        public static BoardFailureAnalysisResult Safe()
        {
            return new BoardFailureAnalysisResult { Reason = BoardFailureReason.None };
        }

        public static BoardFailureAnalysisResult Defer()
        {
            return new BoardFailureAnalysisResult { ShouldDefer = true, Reason = BoardFailureReason.None };
        }

        public static BoardFailureAnalysisResult Fail(BoardFailureReason reason, Tile focusTile = null)
        {
            return new BoardFailureAnalysisResult
            {
                ShouldFail = true,
                Reason = reason,
                FocusTile = focusTile
            };
        }
    }

    private struct TwinLinkLegalMoveContext
    {
        public Tile CandidateNextTile;
        public Tile PreviousTile;
        public int NextStepNumber;
        public BoardFailureSnapshot Snapshot;

        public int GetRemainingCount(Tile tile)
        {
            return Snapshot != null ? Snapshot.GetRemainingCount(tile) : 0;
        }
    }

    private struct ReachabilityState : IEquatable<ReachabilityState>
    {
        public Tile Current;
        public Tile Previous;

        public bool Equals(ReachabilityState other)
        {
            return Current == other.Current && Previous == other.Previous;
        }

        public override bool Equals(object obj)
        {
            return obj is ReachabilityState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int currentHash = Current != null ? Current.GetHashCode() : 0;
                int previousHash = Previous != null ? Previous.GetHashCode() : 0;
                return (currentHash * 397) ^ previousHash;
            }
        }
    }

    private sealed class BoardFailureSnapshot
    {
        public readonly Tile CurrentTile;
        public readonly Tile PreviousTile;
        public readonly int NextStepNumber;
        public readonly List<Tile> AllTiles = new List<Tile>();
        public readonly Dictionary<string, List<Tile>> IgniterTilesByTarget = new Dictionary<string, List<Tile>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<Tile, int> remainingCounts = new Dictionary<Tile, int>();
        private readonly Dictionary<Tile, HiddenSnapshotState> hiddenStates = new Dictionary<Tile, HiddenSnapshotState>();

        public bool HasPendingHidden { get; private set; }

        public BoardFailureSnapshot(Tile[,] sourceTiles, int width, int height, Tile currentTile, Tile previousTile, int nextStepNumber)
        {
            CurrentTile = currentTile;
            PreviousTile = previousTile;
            NextStepNumber = nextStepNumber;

            if (sourceTiles == null)
                return;

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    Tile tile = sourceTiles[row, col];
                    if (tile == null)
                        continue;

                    AllTiles.Add(tile);
                    remainingCounts[tile] = tile.CurrentNumber;

                    var hidden = tile.GetComponent<HiddenTile>();
                    if (hidden != null)
                    {
                        HiddenSnapshotState state = ResolveHiddenState(tile, hidden);
                        hiddenStates[tile] = state;
                        HasPendingHidden |= state == HiddenSnapshotState.Pending;
                    }

                    var igniter = tile.GetComponent<IgniterTile>();
                    if (igniter != null)
                        AddTileByKey(IgniterTilesByTarget, ResolveIgniterTargetID(igniter), tile);
                }
            }
        }

        public int GetRemainingCount(Tile tile)
        {
            if (tile == null)
                return 0;
            return remainingCounts.TryGetValue(tile, out int value) ? value : tile.CurrentNumber;
        }

        public HiddenSnapshotState GetHiddenState(Tile tile)
        {
            if (tile == null)
                return HiddenSnapshotState.None;
            return hiddenStates.TryGetValue(tile, out HiddenSnapshotState state) ? state : HiddenSnapshotState.None;
        }

        private static HiddenSnapshotState ResolveHiddenState(Tile tile, HiddenTile hidden)
        {
            if (hidden == null)
                return HiddenSnapshotState.None;
            if (tile != null && !tile.IsActive)
                return HiddenSnapshotState.None;
            if (hidden.IsColliderEnabled && tile != null && tile.IsActive)
                return HiddenSnapshotState.Live;
            if (hidden.IsActivated)
                return HiddenSnapshotState.Pending;
            return HiddenSnapshotState.Locked;
        }

        private static string ResolveIgniterTargetID(IgniterTile igniter)
        {
            string id = igniter != null ? igniter.TargetID : "";
            return string.IsNullOrEmpty(id) ? "default" : id;
        }

        private static void AddTileByKey(Dictionary<string, List<Tile>> map, string key, Tile tile)
        {
            if (map == null || tile == null)
                return;

            string safeKey = string.IsNullOrEmpty(key) ? "default" : key;
            if (!map.TryGetValue(safeKey, out List<Tile> list))
            {
                list = new List<Tile>();
                map[safeKey] = list;
            }
            list.Add(tile);
        }
    }

    public void RegisterPendingBoardMutation(string source, int count = 1)
    {
        if (count <= 0)
            return;

        pendingBoardMutationCount += count;
        if (ShouldLogVerboseStage6Debug())
            DevLog.Log($"[실패 판정 지연] begin source={source} pending={pendingBoardMutationCount}");
    }

    public void CompletePendingBoardMutation(string source, Tile clearReferenceTile = null, bool requestFailureCheck = true)
    {
        if (pendingBoardMutationCount > 0)
            pendingBoardMutationCount--;

        if (ShouldLogVerboseStage6Debug())
            DevLog.Log($"[실패 판정 지연] end source={source} pending={pendingBoardMutationCount}");

        if (clearReferenceTile != null)
            pendingBoardFailureClearReferenceTile = clearReferenceTile;

        if (pendingBoardMutationCount <= 0 && deferredMissedFixedKnotStepCount >= 0)
        {
            int deferredSteps = Mathf.Max(deferredMissedFixedKnotStepCount, GetTotalPathCount());
            deferredMissedFixedKnotStepCount = -1;
            if (CheckAndHandleMissedFixedKnot(deferredSteps))
                return;
        }

        if (pendingBoardMutationCount <= 0 && (requestFailureCheck || pendingBoardFailureRecheck))
            ScheduleDeferredBoardFailureCheck(pendingBoardFailureClearReferenceTile);
    }

    private void ResetBoardFailureTracking()
    {
        deferredMissedFixedKnotStepCount = -1;
        pendingBoardMutationCount = 0;
        pendingBoardFailureRecheck = false;
        pendingBoardFailureClearReferenceTile = null;
        if (deferredBoardFailureCheckRoutine != null)
        {
            StopCoroutine(deferredBoardFailureCheckRoutine);
            deferredBoardFailureCheckRoutine = null;
        }
        if (postStageStartedFailureCheckRoutine != null)
        {
            StopCoroutine(postStageStartedFailureCheckRoutine);
            postStageStartedFailureCheckRoutine = null;
        }
    }

    private void ScheduleDeferredBoardFailureCheck(Tile clearReferenceTile = null)
    {
        if (stageCleared)
            return;

        pendingBoardFailureRecheck = true;
        if (clearReferenceTile != null)
            pendingBoardFailureClearReferenceTile = clearReferenceTile;

        if (pendingBoardMutationCount > 0 || deferredBoardFailureCheckRoutine != null)
            return;

        deferredBoardFailureCheckRoutine = StartCoroutine(DeferredBoardFailureCheckRoutine());
    }

    private IEnumerator DeferredBoardFailureCheckRoutine()
    {
        yield return null;

        deferredBoardFailureCheckRoutine = null;
        if (!pendingBoardFailureRecheck)
            yield break;
        if (isDragging)
            yield break;

        pendingBoardFailureRecheck = false;
        Tile clearReferenceTile = pendingBoardFailureClearReferenceTile;
        pendingBoardFailureClearReferenceTile = null;
        CheckAndHandleBoardFailure(clearReferenceTile);
    }

    private void SchedulePostStageStartedFailureCheck()
    {
        if (postStageStartedFailureCheckRoutine != null)
            StopCoroutine(postStageStartedFailureCheckRoutine);

        postStageStartedFailureCheckRoutine = StartCoroutine(PostStageStartedFailureCheckRoutine());
    }

    private IEnumerator PostStageStartedFailureCheckRoutine()
    {
        yield return null;
        yield return null;

        postStageStartedFailureCheckRoutine = null;
        CheckAndHandleBoardFailure(currentStartTile);
    }

    private bool CheckAndHandleBoardFailure(Tile clearReferenceTile = null)
    {
        if (stageCleared || isGameOverSequencePlaying || tiles == null)
            return false;
        if (isDragging)
        {
            pendingBoardFailureRecheck = true;
            if (clearReferenceTile != null)
                pendingBoardFailureClearReferenceTile = clearReferenceTile;
            return false;
        }

        if (RunClearChecksBeforeFailure(clearReferenceTile))
            return false;

        if (pendingBoardMutationCount > 0)
        {
            pendingBoardFailureRecheck = true;
            if (clearReferenceTile != null)
                pendingBoardFailureClearReferenceTile = clearReferenceTile;
            return false;
        }

        BoardFailureSnapshot snapshot = CreateBoardFailureSnapshot();
        BoardFailureAnalysisResult result = AnalyzeBoardFailure(snapshot);
        if (result.ShouldDefer)
        {
            ScheduleDeferredBoardFailureCheck(clearReferenceTile);
            return false;
        }

        if (!result.ShouldFail)
            return false;

        string reason = StageFailureReasonFromBoardFailure(result.Reason);
        CollectFailureHighlightTiles(result.Reason, result.FocusTile, snapshot);
        ShowBoardFailureSnackbar(result.Reason);
        PlayBoardFailureFocusEffect(result.Reason, result.FocusTile);
        DevLog.Log($"Game Over: {reason} focus={DescribeTileForDebug(result.FocusTile)}");
        return BeginGameOverSequence(reason, result.FocusTile);
    }

    private bool RunClearChecksBeforeFailure(Tile clearReferenceTile)
    {
        Tile currentTileForClear = clearReferenceTile != null ? clearReferenceTile : GetFailureCurrentTile();
        if (currentTileForClear != null && CheckVictoryCondition(currentTileForClear))
            return true;

        CheckStageClear();
        return stageCleared;
    }

    private BoardFailureSnapshot CreateBoardFailureSnapshot()
    {
        return new BoardFailureSnapshot(
            tiles,
            stageWidth,
            stageHeight,
            GetFailureCurrentTile(),
            GetFailurePreviousTile(),
            GetTotalPathCount() + 1);
    }

    private Tile GetFailureCurrentTile()
    {
        if (isDragging && currentPath != null && currentPath.Count > 0)
            return currentPath[currentPath.Count - 1];
        return currentStartTile;
    }

    private Tile GetFailurePreviousTile()
    {
        if (currentPath != null && currentPath.Count >= 2)
            return currentPath[currentPath.Count - 2];
        return null;
    }

    private BoardFailureAnalysisResult AnalyzeBoardFailure(BoardFailureSnapshot snapshot)
    {
        if (snapshot == null || tiles == null)
            return BoardFailureAnalysisResult.Safe();

        Tile currentTile = snapshot.CurrentTile;
        if (currentTile == null)
            return BoardFailureAnalysisResult.Fail(BoardFailureReason.InvalidCurrentStart);

        HiddenSnapshotState currentHiddenState = snapshot.GetHiddenState(currentTile);
        if (currentHiddenState == HiddenSnapshotState.Pending || snapshot.HasPendingHidden)
            return BoardFailureAnalysisResult.Defer();

        if (!IsLiveRemainingTile(currentTile, snapshot))
            return BoardFailureAnalysisResult.Fail(BoardFailureReason.InvalidCurrentStart, currentTile);

        LegalMoveBlockReason strongestBlockReason;
        int legalMoveCount = CountLegalMovesFrom(currentTile, snapshot, out strongestBlockReason);
        if (legalMoveCount == 0)
            return BoardFailureAnalysisResult.Fail(ResolveNoLegalMoveReason(currentTile, strongestBlockReason), currentTile);

        HashSet<Tile> reachableTiles = CollectReachableTiles(snapshot);
        if (TryFindUntriggerableHiddenTile(snapshot, reachableTiles, out Tile hiddenTile))
            return BoardFailureAnalysisResult.Fail(BoardFailureReason.HiddenUntriggerable, hiddenTile);

        if (TryFindUnreachableRemainingTile(snapshot, reachableTiles, out Tile unreachableTile))
            return BoardFailureAnalysisResult.Fail(BoardFailureReason.UnreachableRemainingTiles, unreachableTile);

        return BoardFailureAnalysisResult.Safe();
    }

    private int CountLegalMovesFrom(Tile from, BoardFailureSnapshot snapshot, out LegalMoveBlockReason strongestBlockReason)
    {
        strongestBlockReason = LegalMoveBlockReason.None;
        int count = 0;

        for (int i = 0; i < CrossBlastDx.Length; i++)
        {
            Tile candidate = GetTileAtGrid(from.X + CrossBlastDx[i], from.Y + CrossBlastDy[i]);
            var context = new TwinLinkLegalMoveContext
            {
                CandidateNextTile = candidate,
                PreviousTile = snapshot.PreviousTile,
                NextStepNumber = snapshot.NextStepNumber,
                Snapshot = snapshot
            };

            if (CanLegallyMoveForFailureCheck(from, candidate, context, out LegalMoveBlockReason blockReason))
            {
                count++;
                continue;
            }

            strongestBlockReason = PickMoreSpecificBlockReason(strongestBlockReason, blockReason);
        }

        return count;
    }

    private bool CanLegallyMoveForFailureCheck(
        Tile from,
        Tile to,
        TwinLinkLegalMoveContext context,
        out LegalMoveBlockReason blockReason,
        bool ignoreFixedKnotOrder = false,
        bool ignoreTwinLinkPartners = false)
    {
        BoardFailureSnapshot snapshot = context.Snapshot;
        if (snapshot == null)
        {
            blockReason = LegalMoveBlockReason.NullOrInactive;
            return false;
        }

        return CanLegallyMoveWithKernel(
            from,
            to,
            context,
            context.GetRemainingCount,
            tile => IsLiveRemainingTile(tile, snapshot),
            out _,
            out blockReason,
            ignoreFixedKnotOrder,
            ignoreTwinLinkPartners);
    }

    private bool CanConsumeTwinLinkPartnersForFailure(TwinLinkTile sourceTwinLink, TwinLinkLegalMoveContext context)
    {
        if (sourceTwinLink == null || context.Snapshot == null)
            return true;

        foreach (TwinLinkTile partner in sourceTwinLink.Partners)
        {
            if (partner == null)
                continue;

            Tile partnerTile = partner.GetComponent<Tile>();
            if (partnerTile == null)
                continue;

            if (partnerTile == context.CandidateNextTile || partnerTile == context.PreviousTile)
                continue;

            if (context.GetRemainingCount(partnerTile) <= 0)
                continue;
        }

        return true;
    }

    private HashSet<Tile> CollectReachableTiles(BoardFailureSnapshot snapshot)
    {
        HashSet<Tile> reachable = new HashSet<Tile>();
        if (snapshot == null || !IsLiveRemainingTile(snapshot.CurrentTile, snapshot))
            return reachable;

        Queue<ReachabilityState> queue = new Queue<ReachabilityState>();
        HashSet<ReachabilityState> visitedStates = new HashSet<ReachabilityState>();
        ReachabilityState initialState = new ReachabilityState
        {
            Current = snapshot.CurrentTile,
            Previous = snapshot.PreviousTile
        };
        visitedStates.Add(initialState);
        reachable.Add(snapshot.CurrentTile);
        queue.Enqueue(initialState);

        while (queue.Count > 0)
        {
            ReachabilityState state = queue.Dequeue();
            Tile from = state.Current;
            for (int i = 0; i < CrossBlastDx.Length; i++)
            {
                Tile candidate = GetTileAtGrid(from.X + CrossBlastDx[i], from.Y + CrossBlastDy[i]);
                var context = new TwinLinkLegalMoveContext
                {
                    CandidateNextTile = candidate,
                    PreviousTile = state.Previous,
                    NextStepNumber = snapshot.NextStepNumber,
                    Snapshot = snapshot
                };

                if (!CanLegallyMoveForFailureCheck(from, candidate, context, out _, true))
                    continue;

                reachable.Add(candidate);
                ReachabilityState nextState = new ReachabilityState
                {
                    Current = candidate,
                    Previous = from
                };
                if (visitedStates.Add(nextState))
                    queue.Enqueue(nextState);
            }
        }

        return reachable;
    }

    private bool TryFindUntriggerableHiddenTile(BoardFailureSnapshot snapshot, HashSet<Tile> reachableTiles, out Tile hiddenTile)
    {
        hiddenTile = null;
        HashSet<string> triggerableHiddenGroups = CollectTriggerableHiddenGroups(snapshot, reachableTiles);
        foreach (Tile tile in snapshot.AllTiles)
        {
            if (tile == null || snapshot.GetRemainingCount(tile) <= 0)
                continue;

            var hidden = tile.GetComponent<HiddenTile>();
            if (hidden == null)
                continue;

            HiddenSnapshotState state = snapshot.GetHiddenState(tile);
            if (state == HiddenSnapshotState.Live)
                continue;
            if (state == HiddenSnapshotState.Pending)
                return false;

            string groupID = GetHiddenGroupID(hidden);
            if (!triggerableHiddenGroups.Contains(groupID))
            {
                hiddenTile = tile;
                return true;
            }
        }

        return false;
    }

    private HashSet<string> CollectTriggerableHiddenGroups(BoardFailureSnapshot snapshot, HashSet<Tile> reachableTiles)
    {
        HashSet<string> triggerableGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (snapshot == null)
            return triggerableGroups;

        HashSet<Tile> potentialReachable = reachableTiles != null
            ? new HashSet<Tile>(reachableTiles)
            : new HashSet<Tile>();

        bool changed;
        do
        {
            changed = false;

            foreach (Tile tile in snapshot.AllTiles)
            {
                if (tile == null || !potentialReachable.Contains(tile))
                    continue;

                var igniter = tile.GetComponent<IgniterTile>();
                if (igniter == null || igniter.HasTriggered || !IsPotentiallyEnterableTile(tile, snapshot, triggerableGroups))
                    continue;

                string targetID = GetIgniterTargetID(igniter);
                if (triggerableGroups.Add(targetID))
                    changed = true;
            }

            foreach (Tile tile in snapshot.AllTiles)
            {
                if (tile == null || potentialReachable.Contains(tile) || snapshot.GetRemainingCount(tile) <= 0)
                    continue;

                var hidden = tile.GetComponent<HiddenTile>();
                if (hidden == null || !triggerableGroups.Contains(GetHiddenGroupID(hidden)))
                    continue;

                potentialReachable.Add(tile);
                changed = true;
            }

            changed |= ExpandPotentialReachability(snapshot, potentialReachable, triggerableGroups);
        }
        while (changed);

        return triggerableGroups;
    }

    private bool ExpandPotentialReachability(BoardFailureSnapshot snapshot, HashSet<Tile> potentialReachable, HashSet<string> triggerableGroups)
    {
        bool changed = false;
        Queue<Tile> queue = new Queue<Tile>(potentialReachable);

        while (queue.Count > 0)
        {
            Tile from = queue.Dequeue();
            for (int i = 0; i < CrossBlastDx.Length; i++)
            {
                Tile candidate = GetTileAtGrid(from.X + CrossBlastDx[i], from.Y + CrossBlastDy[i]);
                if (potentialReachable.Contains(candidate))
                    continue;
                if (!CanTraversePotentialReachability(from, candidate, snapshot, triggerableGroups))
                    continue;

                potentialReachable.Add(candidate);
                queue.Enqueue(candidate);
                changed = true;
            }
        }

        return changed;
    }

    private bool CanTraversePotentialReachability(Tile from, Tile to, BoardFailureSnapshot snapshot, HashSet<string> triggerableGroups)
    {
        if (from == null || to == null || snapshot == null)
            return false;
        if (!IsPotentiallyEnterableTile(from, snapshot, triggerableGroups) ||
            !IsPotentiallyEnterableTile(to, snapshot, triggerableGroups))
        {
            return false;
        }
        if (!IsAdjacent(from, to))
            return false;

        var shortCircuitHit = to.GetComponent<ShortCircuitTile>();
        if (shortCircuitHit != null && shortCircuitHit.IsBlockedEntryFrom(from.X, from.Y))
            return false;

        var shortCircuitFrom = from.GetComponent<ShortCircuitTile>();
        if (shortCircuitFrom != null && !shortCircuitFrom.IsExitCell(to.X, to.Y))
            return false;

        return true;
    }

    private bool IsPotentiallyEnterableTile(Tile tile, BoardFailureSnapshot snapshot, HashSet<string> triggerableGroups)
    {
        if (tile == null || snapshot == null || snapshot.GetRemainingCount(tile) <= 0)
            return false;

        var hidden = tile.GetComponent<HiddenTile>();
        if (hidden == null)
            return true;

        HiddenSnapshotState state = snapshot.GetHiddenState(tile);
        if (state == HiddenSnapshotState.Live)
            return true;
        if (state == HiddenSnapshotState.Pending)
            return false;

        return triggerableGroups != null && triggerableGroups.Contains(GetHiddenGroupID(hidden));
    }

    private static string GetHiddenGroupID(HiddenTile hidden)
    {
        string id = hidden != null ? hidden.GroupID : "";
        return string.IsNullOrEmpty(id) ? "default" : id;
    }

    private static string GetIgniterTargetID(IgniterTile igniter)
    {
        string id = igniter != null ? igniter.TargetID : "";
        return string.IsNullOrEmpty(id) ? "default" : id;
    }

    private bool TryFindUnreachableRemainingTile(BoardFailureSnapshot snapshot, HashSet<Tile> reachableTiles, out Tile unreachableTile)
    {
        unreachableTile = null;
        HashSet<string> triggerableHiddenGroups = CollectTriggerableHiddenGroups(snapshot, reachableTiles);
        HashSet<Tile> potentiallyReachableTiles = CollectPotentiallyReachableTiles(snapshot, reachableTiles, triggerableHiddenGroups);

        foreach (Tile tile in snapshot.AllTiles)
        {
            if (tile == null || snapshot.GetRemainingCount(tile) <= 0)
                continue;

            if (!IsLiveRemainingTile(tile, snapshot))
                continue;

            if (reachableTiles != null && reachableTiles.Contains(tile))
                continue;

            if (potentiallyReachableTiles.Contains(tile))
                continue;

            if (CanBeResolvedWithoutDirectReach(tile, snapshot, potentiallyReachableTiles))
                continue;

            unreachableTile = tile;
            return true;
        }

        return false;
    }

    private HashSet<Tile> CollectPotentiallyReachableTiles(BoardFailureSnapshot snapshot, HashSet<Tile> reachableTiles, HashSet<string> triggerableHiddenGroups)
    {
        HashSet<Tile> potentialReachable = reachableTiles != null
            ? new HashSet<Tile>(reachableTiles)
            : new HashSet<Tile>();
        if (snapshot == null)
            return potentialReachable;

        HashSet<string> groups = triggerableHiddenGroups ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool changed;
        do
        {
            changed = false;
            foreach (Tile tile in snapshot.AllTiles)
            {
                if (tile == null || potentialReachable.Contains(tile) || snapshot.GetRemainingCount(tile) <= 0)
                    continue;

                var hidden = tile.GetComponent<HiddenTile>();
                if (hidden == null || !groups.Contains(GetHiddenGroupID(hidden)))
                    continue;

                potentialReachable.Add(tile);
                changed = true;
            }

            changed |= ExpandPotentialReachability(snapshot, potentialReachable, groups);
        }
        while (changed);

        return potentialReachable;
    }

    private bool CanBeResolvedWithoutDirectReach(Tile target, BoardFailureSnapshot snapshot, HashSet<Tile> reachableTiles)
    {
        if (target == null || snapshot == null || reachableTiles == null)
            return false;

        if (HasReachableTwinLinkPartner(target, snapshot, reachableTiles))
            return true;

        if (HasReachableCrossBlastAffecting(target, snapshot, reachableTiles))
            return true;

        return false;
    }

    private bool HasReachableTwinLinkPartner(Tile target, BoardFailureSnapshot snapshot, HashSet<Tile> reachableTiles)
    {
        var targetTwinLink = target != null ? target.GetComponent<TwinLinkTile>() : null;
        if (targetTwinLink == null || snapshot == null || reachableTiles == null)
            return false;

        foreach (Tile reachable in reachableTiles)
        {
            if (reachable == null || reachable == target)
                continue;

            var reachableTwinLink = reachable.GetComponent<TwinLinkTile>();
            if (reachableTwinLink != null &&
                reachableTwinLink.LinkID == targetTwinLink.LinkID &&
                CanTwinLinkConsumeTargetForFailure(reachable, target, snapshot))
            {
                return true;
            }
        }

        return false;
    }

    private bool CanTwinLinkConsumeTargetForFailure(Tile sourceTile, Tile targetTile, BoardFailureSnapshot snapshot)
    {
        if (sourceTile == null || targetTile == null || snapshot == null)
            return false;

        for (int i = 0; i < CrossBlastDx.Length; i++)
        {
            Tile candidate = GetTileAtGrid(sourceTile.X + CrossBlastDx[i], sourceTile.Y + CrossBlastDy[i]);
            if (candidate == null || candidate == targetTile)
                continue;

            var context = new TwinLinkLegalMoveContext
            {
                CandidateNextTile = candidate,
                PreviousTile = null,
                NextStepNumber = snapshot.NextStepNumber,
                Snapshot = snapshot
            };

            if (CanLegallyMoveForFailureCheck(sourceTile, candidate, context, out _, true))
                return true;
        }

        return false;
    }

    private bool HasReachableCrossBlastAffecting(Tile target, BoardFailureSnapshot snapshot, HashSet<Tile> reachableTiles)
    {
        if (target == null || snapshot == null || reachableTiles == null)
            return false;

        foreach (Tile reachable in reachableTiles)
        {
            if (reachable == null || reachable.GetComponent<CrossBlastTile>() == null)
                continue;

            if (Mathf.Abs(reachable.X - target.X) + Mathf.Abs(reachable.Y - target.Y) != 1)
                continue;

            if (HasCrossBlastExitOtherThanTarget(reachable, target, snapshot))
                return true;
        }

        return false;
    }

    private bool HasCrossBlastExitOtherThanTarget(Tile crossBlastTile, Tile target, BoardFailureSnapshot snapshot)
    {
        for (int i = 0; i < CrossBlastDx.Length; i++)
        {
            Tile candidate = GetTileAtGrid(crossBlastTile.X + CrossBlastDx[i], crossBlastTile.Y + CrossBlastDy[i]);
            if (candidate == null || candidate == target)
                continue;

            var context = new TwinLinkLegalMoveContext
            {
                CandidateNextTile = candidate,
                PreviousTile = null,
                NextStepNumber = snapshot.NextStepNumber,
                Snapshot = snapshot
            };

            if (CanLegallyMoveForFailureCheck(crossBlastTile, candidate, context, out _, true, true))
                return true;
        }

        return false;
    }

    private bool IsLiveRemainingTile(Tile tile, BoardFailureSnapshot snapshot)
    {
        if (tile == null || snapshot == null || snapshot.GetRemainingCount(tile) <= 0)
            return false;

        HiddenSnapshotState hiddenState = snapshot.GetHiddenState(tile);
        return hiddenState == HiddenSnapshotState.None || hiddenState == HiddenSnapshotState.Live;
    }

    private Tile GetTileAtGrid(int x, int y)
    {
        if (tiles == null || x < 0 || x >= stageWidth || y < 0 || y >= stageHeight)
            return null;

        return tiles[y, x];
    }

    private LegalMoveBlockReason PickMoreSpecificBlockReason(LegalMoveBlockReason current, LegalMoveBlockReason candidate)
    {
        if (candidate == LegalMoveBlockReason.None)
            return current;
        if (current == LegalMoveBlockReason.None || current == LegalMoveBlockReason.NullOrInactive)
            return candidate;
        if (candidate == LegalMoveBlockReason.TwinLinkPartner || candidate == LegalMoveBlockReason.ShortCircuitBlocked)
            return candidate;
        return current;
    }

    private BoardFailureReason ResolveNoLegalMoveReason(Tile currentTile, LegalMoveBlockReason blockReason)
    {
        if (blockReason == LegalMoveBlockReason.TwinLinkPartner)
            return BoardFailureReason.TwinLinkUnsatisfiable;
        if (blockReason == LegalMoveBlockReason.ShortCircuitBlocked || (currentTile != null && currentTile.GetComponent<ShortCircuitTile>() != null))
            return BoardFailureReason.ShortCircuitBlocked;
        return BoardFailureReason.NoLegalMove;
    }

    private void PlayBoardFailureFocusEffect(BoardFailureReason reason, Tile focusTile)
    {
        Color focusColor = BoardFailureFocusColor(reason);
        if (reason == BoardFailureReason.TwinLinkUnsatisfiable)
        {
            PlayTwinLinkGroupFailurePulse(focusTile, focusColor);
            return;
        }

        if (focusTile != null)
            focusTile.PlayFailureFocusPulse(focusColor, FailureFocusPulseCount);
        for (int i = 0; i < failureHighlightTiles.Count; i++)
        {
            Tile tile = failureHighlightTiles[i];
            if (tile != null && tile != focusTile)
                tile.PlayFailureFocusPulse(focusColor, FailureFocusPulseCount);
        }
    }

    /// <summary>
    /// 실패 설명에 함께 강조할 타일을 모은다. 도달 불가 실패면 더 이상 닿을 수 없는 남은 타일을 모두 표시한다.
    /// </summary>
    private void CollectFailureHighlightTiles(BoardFailureReason reason, Tile focusTile, BoardFailureSnapshot snapshot)
    {
        failureHighlightTiles.Clear();
        if (focusTile != null)
            failureHighlightTiles.Add(focusTile);
        if (reason != BoardFailureReason.UnreachableRemainingTiles || snapshot == null)
            return;

        HashSet<Tile> reachableTiles = CollectReachableTiles(snapshot);
        HashSet<string> triggerableHiddenGroups = CollectTriggerableHiddenGroups(snapshot, reachableTiles);
        HashSet<Tile> potentiallyReachableTiles = CollectPotentiallyReachableTiles(snapshot, reachableTiles, triggerableHiddenGroups);
        foreach (Tile tile in snapshot.AllTiles)
        {
            if (tile == null || tile == focusTile || !IsLiveRemainingTile(tile, snapshot))
                continue;
            if (reachableTiles != null && reachableTiles.Contains(tile))
                continue;
            if (potentiallyReachableTiles.Contains(tile))
                continue;
            if (CanBeResolvedWithoutDirectReach(tile, snapshot, potentiallyReachableTiles))
                continue;
            failureHighlightTiles.Add(tile);
        }
    }

    private Color BoardFailureFocusColor(BoardFailureReason reason)
    {
        switch (reason)
        {
            case BoardFailureReason.HiddenUntriggerable:
                return new Color(0.45f, 0.95f, 1.35f, 1f);
            case BoardFailureReason.UnreachableRemainingTiles:
                return new Color(1.35f, 0.8f, 0.25f, 1f);
            case BoardFailureReason.ShortCircuitBlocked:
            case BoardFailureReason.TwinLinkUnsatisfiable:
                return new Color(1.3f, 0.42f, 0.5f, 1f);
            case BoardFailureReason.InvalidCurrentStart:
                return new Color(1.2f, 1.2f, 1.2f, 1f);
            case BoardFailureReason.NoLegalMove:
            default:
                return new Color(1.2f, 0.45f, 0.75f, 1f);
        }
    }

    private void PlayTwinLinkGroupFailurePulse(Tile focusTile, Color focusColor)
    {
        var sourceTwinLink = focusTile != null ? focusTile.GetComponent<TwinLinkTile>() : null;
        if (sourceTwinLink == null || tiles == null)
        {
            if (focusTile != null)
                focusTile.PlayFailureFocusPulse(focusColor, 2);
            return;
        }

        for (int row = 0; row < stageHeight; row++)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                Tile tile = tiles[row, col];
                if (tile == null)
                    continue;

                var twinLink = tile.GetComponent<TwinLinkTile>();
                if (twinLink != null && twinLink.LinkID == sourceTwinLink.LinkID)
                    tile.PlayFailureFocusPulse(focusColor, 2);
            }
        }
    }

    private string StageFailureReasonFromBoardFailure(BoardFailureReason reason)
    {
        switch (reason)
        {
            case BoardFailureReason.UnreachableRemainingTiles:
                return StageFailReasonUnreachableRemainingTiles;
            case BoardFailureReason.HiddenUntriggerable:
                return StageFailReasonHiddenUntriggerable;
            case BoardFailureReason.ShortCircuitBlocked:
                return StageFailReasonShortCircuitBlocked;
            case BoardFailureReason.TwinLinkUnsatisfiable:
                return StageFailReasonTwinLinkUnsatisfiable;
            case BoardFailureReason.InvalidCurrentStart:
                return StageFailReasonInvalidCurrentStart;
            case BoardFailureReason.NoLegalMove:
            default:
                return StageFailReasonNoLegalMove;
        }
    }

    private void ShowBoardFailureSnackbar(BoardFailureReason reason)
    {
        string localizationKey = BoardFailureSnackbarKey(reason);
        if (string.IsNullOrEmpty(localizationKey))
            return;

        ShowGameOverSnackbar(localizationKey, Mathf.Max(gameplayRuleSnackbarDuration, BoardFailureSnackbarDuration));
    }

    private string BoardFailureSnackbarKey(BoardFailureReason reason)
    {
        switch (reason)
        {
            case BoardFailureReason.UnreachableRemainingTiles:
                return "snackbar_gameover_unreachable_remaining_tiles";
            case BoardFailureReason.HiddenUntriggerable:
                return "snackbar_gameover_hidden_untriggerable";
            case BoardFailureReason.ShortCircuitBlocked:
                return "snackbar_gameover_short_circuit_blocked";
            case BoardFailureReason.TwinLinkUnsatisfiable:
                return "snackbar_gameover_twin_link_unsatisfiable";
            case BoardFailureReason.InvalidCurrentStart:
                return "snackbar_gameover_invalid_current_start";
            case BoardFailureReason.NoLegalMove:
                return "snackbar_gameover_no_legal_move";
            case BoardFailureReason.None:
            default:
                return null;
        }
    }

    private int deferredMissedFixedKnotStepCount = -1;

    private bool CheckAndHandleMissedFixedKnot(int totalPathCount)
    {
        if (stageCleared || isGameOverSequencePlaying || tiles == null)
            return false;

        // CrossBlast 감소는 연출 때문에 0.07초 늦게 적용된다. 그 감소로 순서 타일이 지워질 수 있으므로
        // 대기 중인 보드 변화가 끝난 뒤에 판정한다(규칙상 떠날 때의 효과는 같은 이동 안에서 먼저 적용된다).
        if (pendingBoardMutationCount > 0)
        {
            deferredMissedFixedKnotStepCount = Mathf.Max(deferredMissedFixedKnotStepCount, totalPathCount);
            return false;
        }

        for (int row = 0; row < stageHeight; row++)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                Tile tile = tiles[row, col];
                if (tile == null)
                    continue;

                var fixedKnot = tile.GetComponent<FixedKnotTile>();
                if (fixedKnot == null || !fixedKnot.IsMissedAtStepCount(totalPathCount))
                    continue;

                ShowFixedKnotMissedSnackbar(tile, fixedKnot);
                fixedKnot.PlayWrongOrderShake();
                DevLog.Log($"Game Over: FixedKnot missed at ({tile.X},{tile.Y}) targetOrder={fixedKnot.TargetOrder} totalPathCount={totalPathCount}");
                return BeginGameOverSequence(StageFailReasonFixedKnotMissed, tile);
            }
        }

        return false;
    }

    private void ShowFixedKnotMissedSnackbar(Tile tile, FixedKnotTile fixedKnot)
    {
        if (tile == null || fixedKnot == null)
            return;

        ShowGameOverSnackbar("snackbar_fixed_knot_missed", Mathf.Max(gameplayRuleSnackbarDuration, FixedKnotMissedSnackbarDuration));
    }

    private void ShowGameOverSnackbar(string localizationKey, float durationSeconds)
    {
        if (string.IsNullOrEmpty(localizationKey))
            return;

        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI == null)
            return;

        mainUI.ShowGameplaySnackbar(localizationKey, Mathf.Max(0.1f, durationSeconds));
    }

    private bool BeginGameOverSequence(string reason, Tile failFocusTile = null)
    {
        if (isGameOverSequencePlaying)
            return false;
        if (feverActive)
        {
            // 피버는 실패가 없다: 막히면 하트를 쓰지 않고 바로 새 판을 준다.
            ClearHintPreview();
            currentPath.Clear();
            isDragging = false;
            gameplayFeedback?.Failed(failFocusTile != null ? failFocusTile : currentStartTile);
            PlayFailSfx();
            OnFeverBoardFinished(false);
            return true;
        }

        DevLog.Log("Game Over");
        TrackStageFailed(reason);
        if (pathLitClearRoutine != null)
        {
            StopCoroutine(pathLitClearRoutine);
            pathLitClearRoutine = null;
        }

        gameOverFocusTile = failFocusTile;
        if (failFocusTile != null && !failureHighlightTiles.Contains(failFocusTile))
            failureHighlightTiles.Add(failFocusTile);
        ClearHintPreview();
        currentPath.Clear();
        isDragging = false;
        isGameOverSequencePlaying = true;
        StartCoroutine(GameOverAndResetSequence());
        return true;
    }

    /// <summary>
    /// 게임오버: 글리치(0.5초) → 암전 → 대기 → 리셋 → 순차 등장. Spotlight 모드면 실패 지점 Radar Pulse → Vignette → 완전 암흑 리셋.
    /// </summary>
    private IEnumerator GameOverAndResetSequence()
    {
        ResetBoardFailureTracking();
        StopBoardTransientVisuals();
        gameplayFeedback?.Failed(gameOverFocusTile != null ? gameOverFocusTile : currentStartTile);
        ClearHintPreview();
        totalStepsCommitted = 0;
        blackoutQuestionFlipPlayed = false;
        if (blackoutQuestionFlipRoutine != null)
        {
            StopCoroutine(blackoutQuestionFlipRoutine);
            blackoutQuestionFlipRoutine = null;
        }
        ResetAllTileQuestionRotations();
        ResetTrail();
        ClearPendingBlockNoteQueue();
        PlayFailSfx();
        linkSystem?.ClearLinks();

        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI != null)
            mainUI.ConsumeHeartOnGameOver();

        bool isSpotlight = spotlightController != null && spotlightController.IsSpotlightActive();

        if (isSpotlight)
        {
            // Spotlight: 전체 맵 밝히지 않음. 실패 지점(CurrentPosition)에서 Radar Pulse → Vignette 암전 → 완전 암흑 리셋
            Tile focusTile = gameOverFocusTile != null ? gameOverFocusTile : currentStartTile;
            Vector2 failPos = focusTile != null ? (Vector2)focusTile.transform.position : Vector2.zero;
            bool pulseDone = false;
            spotlightController.TriggerGameOverPulse(failPos, () => pulseDone = true);
            yield return new WaitUntil(() => pulseDone);

            // Vignette 순식간에 올렸다가 암전
            Volume vol = postProcessVolume != null ? postProcessVolume : FindFirstObjectByType<Volume>();
            if (vol != null && vol.profile != null && vol.profile.TryGet<Vignette>(out var vig))
            {
                vig.intensity.Override(vignetteDefaultIntensity);
                yield return DOTween.To(() => vig.intensity.value, x => vig.intensity.Override(x), vignetteMaxIntensity, vignetteRampUpDuration).SetEase(Ease.OutQuad).WaitForCompletion();
            }

            for (int row = 0; row < stageHeight; row++)
                for (int col = 0; col < stageWidth; col++)
                    if (tiles[row, col] != null)
                        tiles[row, col].SetBlackout(true);
            if (spotlightController != null)
                spotlightController.ClearAllRevealed();

            yield return new WaitForSeconds(blackoutWait);

            if (vol != null && vol.profile != null && vol.profile.TryGet<Vignette>(out vig))
                yield return DOTween.To(() => vig.intensity.value, x => vig.intensity.Override(x), vignetteDefaultIntensity, vignetteReturnDuration).SetEase(Ease.OutQuad).WaitForCompletion();
        }
        else
        {
            // 일반: 실패 원인 설명 → 한 번의 부드러운 암전 (깜빡임 없음)
            yield return PlayFailureExplanation();
            yield return FadeBoardToDark(FailureFadeToDarkDuration);
            if (FailureDarkHoldDuration > 0f)
                yield return new WaitForSeconds(FailureDarkHoldDuration);
        }

        if (mainUI != null && mainUI.IsWaitingForHeartRefill)
            yield return new WaitUntil(() => mainUI == null || !mainUI.IsWaitingForHeartRefill);

        for (int row = 0; row < stageHeight; row++)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                if (tiles[row, col] == null) continue;
                ResetTileForStageRestart(tiles[row, col]);
            }
        }

        yield return PlayRestartCascade();

        if (currentStartTile != null)
            currentStartTile.ClearScaleOverride();
        if (tiles != null && initialStartTileRow >= 0 && initialStartTileRow < stageHeight &&
            initialStartTileCol >= 0 && initialStartTileCol < stageWidth)
        {
            Tile initialStart = tiles[initialStartTileRow, initialStartTileCol];
            if (initialStart != null)
            {
                currentStartTile = initialStart;
                currentStartTile.SetInitialStartTile(true);
                ActivateStartIgniterIfNeeded();
                // 암막 모드 리셋 후에도 스테이지 새로 시작하듯 시작점만 밝혀서 다시 플레이 가능하게
                if (spotlightController != null)
                    spotlightController.ResetRevealedToStartOnly(initialStart.transform.position);
            }
        }

        if (linkSystem != null && tiles != null)
            linkSystem.CreateLinksForCrossBlastOnly(tiles, stageWidth, stageHeight);

        // 게임오버 후 리셋이 끝나면 진행도/스테이지 UI도 초기 상태로 복원
        RefreshMainUIForStage();
        SetupMelodyForCurrentStage();
        PlayNewStageSfx();
        IncrementBoardVersion("auto_restart_after_fail");
        HandleStageStarted("auto_restart_after_fail");

        gameOverFocusTile = null;
        isGameOverSequencePlaying = false;
    }

    /// <summary>
    /// 실패 원인 설명: 원인 타일은 맥동(CheckAndHandleBoardFailure에서 시작), 나머지 타일은 어둡게.
    /// FailureExplainSkipAfter 이후에는 탭하면 바로 넘어간다.
    /// </summary>
    private IEnumerator PlayFailureExplanation()
    {
        if (tiles == null)
            yield break;

        for (int row = 0; row < stageHeight; row++)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                Tile tile = tiles[row, col];
                if (tile == null || !tile.IsActive || failureHighlightTiles.Contains(tile))
                    continue;
                tile.SetFailureDim(FailureOtherTilesDimFactor);
            }
        }

        float elapsed = 0f;
        while (elapsed < FailureExplainDuration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= FailureExplainSkipAfter && IsPointerDown())
                break;
        }

        for (int i = 0; i < failureHighlightTiles.Count; i++)
            failureHighlightTiles[i]?.StopFailureFocusPulse();
    }

    /// <summary>보드 전체를 한 번에 부드럽게 어둡게 만든 뒤 암전 상태로 둔다.</summary>
    private IEnumerator FadeBoardToDark(float duration)
    {
        if (tiles == null)
            yield break;

        restartCascadeTiles.Clear();
        failureFadeStartColors.Clear();
        for (int row = 0; row < stageHeight; row++)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                Tile tile = tiles[row, col];
                if (tile == null)
                    continue;
                restartCascadeTiles.Add(tile);
                failureFadeStartColors.Add(tile.CurrentDisplayColor);
            }
        }

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = t * t * (3f - 2f * t);
            for (int i = 0; i < restartCascadeTiles.Count; i++)
            {
                Tile tile = restartCascadeTiles[i];
                if (tile != null && tile.IsActive)
                    tile.SetGlitchColor(Color.Lerp(failureFadeStartColors[i], VisualTheme.IsSoftClay ? VisualTheme.PaperShade : Color.black, eased));
            }
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        for (int i = 0; i < restartCascadeTiles.Count; i++)
            restartCascadeTiles[i]?.SetBlackout(true);
        restartCascadeTiles.Clear();
        failureFadeStartColors.Clear();
        failureHighlightTiles.Clear();
    }

    /// <summary>
    /// 재시작 타일 등장. 시작 타일을 먼저 띄우고, 타일 수와 무관하게 RestartCascadeMaxDuration 안에 끝낸다.
    /// (프레임 단위 WaitForSeconds 누적으로 큰 보드에서 길어지던 문제 해결)
    /// </summary>
    private IEnumerator PlayRestartCascade()
    {
        restartCascadeTiles.Clear();
        Tile startTile = null;
        if (tiles != null && initialStartTileRow >= 0 && initialStartTileRow < stageHeight &&
            initialStartTileCol >= 0 && initialStartTileCol < stageWidth)
            startTile = tiles[initialStartTileRow, initialStartTileCol];
        if (startTile != null && startTile.GetComponent<HiddenTile>() == null)
            restartCascadeTiles.Add(startTile);

        for (int row = stageHeight - 1; row >= 0; row--)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                Tile tile = tiles[row, col];
                if (tile == null || tile == startTile || tile.GetComponent<HiddenTile>() != null)
                    continue;
                restartCascadeTiles.Add(tile);
            }
        }

        int count = restartCascadeTiles.Count;
        float interval = count > 1
            ? Mathf.Min(Mathf.Max(0f, tileAppearInterval), RestartCascadeMaxDuration / (count - 1))
            : 0f;
        float elapsed = 0f;
        int next = 0;
        while (next < count)
        {
            while (next < count && elapsed >= next * interval)
            {
                Tile tile = restartCascadeTiles[next++];
                if (tile == null)
                    continue;
                tile.PlayBounceAppearance();
                RefreshSpecialTileVisualStateAfterAppearance(tile);
            }

            if (next < count)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        restartCascadeTiles.Clear();
    }

    // ===== 되돌리기(Undo) =====
    // 이동이 확정된 "조용한 시점"(대기 중인 CrossBlast·Hidden 연쇄가 없는 순간)마다 보드 상태를 저장해 두고,
    // 손을 뗀 상태에서 직전 이동을 한 칸씩 되돌린다. 하트는 쓰지 않는다.
    private sealed class UndoSnapshot
    {
        public int StepCount;
        public Tile Head;
        public int[] Counts;
        public bool[] HiddenActivated;
        public bool[] IgniterTriggered;
        public bool[] IgniterVisual;
        public FixedKnotTile.UndoState[] FixedKnots;
        public bool BlackoutPlayed;
    }

    private const int MaxUndoSnapshots = 256;
    private readonly List<UndoSnapshot> undoHistory = new List<UndoSnapshot>();
    private int lastUndoSnapshotStepCount = -1;

    public bool CanUndo =>
        !isDragging &&
        !stageCleared &&
        !isGameOverSequencePlaying &&
        pendingBoardMutationCount == 0 &&
        FindUndoTargetIndex() >= 0;

    private void ResetUndoHistory()
    {
        undoHistory.Clear();
        lastUndoSnapshotStepCount = -1;
    }

    private void CaptureUndoSnapshotIfNeeded()
    {
        if (tiles == null || stageCleared || isGameOverSequencePlaying || pendingBoardMutationCount > 0)
            return;

        int steps = GetTotalPathCount();
        if (steps == lastUndoSnapshotStepCount)
            return;

        Tile head = isDragging && currentPath.Count > 0 ? currentPath[currentPath.Count - 1] : currentStartTile;
        if (head == null)
            return;

        int cellCount = stageWidth * stageHeight;
        var snapshot = new UndoSnapshot
        {
            StepCount = steps,
            Head = head,
            Counts = new int[cellCount],
            HiddenActivated = new bool[cellCount],
            IgniterTriggered = new bool[cellCount],
            IgniterVisual = new bool[cellCount],
            FixedKnots = new FixedKnotTile.UndoState[cellCount],
            BlackoutPlayed = blackoutQuestionFlipPlayed
        };

        for (int row = 0; row < stageHeight; row++)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                Tile tile = tiles[row, col];
                if (tile == null)
                    continue;

                int index = row * stageWidth + col;
                snapshot.Counts[index] = tile.CurrentNumber;
                var hidden = tile.GetComponent<HiddenTile>();
                if (hidden != null)
                    snapshot.HiddenActivated[index] = hidden.IsActivated;
                var igniter = tile.GetComponent<IgniterTile>();
                if (igniter != null)
                {
                    snapshot.IgniterTriggered[index] = igniter.HasTriggered;
                    snapshot.IgniterVisual[index] = igniter.UsesIgniterVisual;
                }
                var fixedKnot = tile.GetComponent<FixedKnotTile>();
                if (fixedKnot != null)
                    snapshot.FixedKnots[index] = fixedKnot.CaptureUndoState();
            }
        }

        // 되돌린 뒤 새로 움직이면 그 이후 기록은 버린다.
        for (int i = undoHistory.Count - 1; i >= 0 && undoHistory[i].StepCount >= steps; i--)
            undoHistory.RemoveAt(i);
        undoHistory.Add(snapshot);
        if (undoHistory.Count > MaxUndoSnapshots)
            undoHistory.RemoveAt(0);
        lastUndoSnapshotStepCount = steps;
    }

    private int FindUndoTargetIndex()
    {
        int current = totalStepsCommitted;
        for (int i = undoHistory.Count - 1; i >= 0; i--)
        {
            if (undoHistory[i].StepCount < current)
                return i;
        }
        return -1;
    }

    /// <summary>직전 이동 하나를 되돌린다. 드래그 중이거나 연쇄 효과가 진행 중이면 false.</summary>
    public bool TryUndoLastMove()
    {
        if (!CanUndo)
            return false;

        int targetIndex = FindUndoTargetIndex();
        UndoSnapshot snapshot = undoHistory[targetIndex];
        int fromSteps = totalStepsCommitted;
        attemptUsedUndo = true;
        undoHistory.RemoveRange(targetIndex + 1, undoHistory.Count - targetIndex - 1);
        ApplyUndoSnapshot(snapshot);
        lastUndoSnapshotStepCount = snapshot.StepCount;

        FirebaseBootstrap.LogEvent("stage_undo", new Dictionary<string, object>
        {
            { "stage_index", currentStageIndex },
            { "from_step", fromSteps },
            { "to_step", snapshot.StepCount }
        });
        return true;
    }

    private void ApplyUndoSnapshot(UndoSnapshot snapshot)
    {
        ClearHintPreview();
        StopBoardTransientVisuals();
        ResetBoardFailureTracking();
        ClearMoveRuleSnackbarState();
        if (pathLitClearRoutine != null)
        {
            StopCoroutine(pathLitClearRoutine);
            pathLitClearRoutine = null;
        }
        linkSystem?.ClearPathLit();
        if (blackoutQuestionFlipRoutine != null)
        {
            StopCoroutine(blackoutQuestionFlipRoutine);
            blackoutQuestionFlipRoutine = null;
        }

        for (int row = 0; row < stageHeight; row++)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                Tile tile = tiles[row, col];
                if (tile == null)
                    continue;

                int index = row * stageWidth + col;
                tile.GetComponent<CrossBlastTile>()?.ResetTransientVisualState();
                tile.GetComponent<TwinLinkTile>()?.ResetTransientVisualState();
                tile.ClearScaleOverride();
                tile.RestoreCountForUndo(snapshot.Counts[index]);

                var hidden = tile.GetComponent<HiddenTile>();
                if (hidden != null)
                {
                    if (snapshot.HiddenActivated[index])
                        hidden.SetLiveImmediateForUndo();
                    else
                        hidden.ResetToHiddenState();
                }

                tile.GetComponent<IgniterTile>()?.RestoreForUndo(snapshot.IgniterTriggered[index], snapshot.IgniterVisual[index]);
                tile.GetComponent<FixedKnotTile>()?.RestoreUndoState(snapshot.FixedKnots[index]);
            }
        }

        blackoutQuestionFlipPlayed = snapshot.BlackoutPlayed;
        SetAllTilesDisplayAsQuestion(snapshot.BlackoutPlayed);
        ResetAllTileQuestionRotations();

        totalStepsCommitted = snapshot.StepCount;
        if (attemptMoves.Count > snapshot.StepCount)
            attemptMoves.RemoveRange(snapshot.StepCount, attemptMoves.Count - snapshot.StepCount);
        currentStartTile = snapshot.Head;
        bool isInitialStart = snapshot.StepCount == 0 &&
                              initialStartTileRow >= 0 && initialStartTileCol >= 0 &&
                              initialStartTileRow < stageHeight && initialStartTileCol < stageWidth &&
                              tiles[initialStartTileRow, initialStartTileCol] == currentStartTile;
        if (isInitialStart)
            currentStartTile.SetInitialStartTile(true);
        else
            currentStartTile.SetCurrentPositionMarker(true);

        IncrementBoardVersion("undo");
        RefreshMainUIProgress();
    }

    /// <summary>
    /// 현재 스테이지를 초기 상태로 리셋 (ResetButton 등에서 호출). 게임오버 연출 없이 타일 복원 + 순차 등장.
    /// </summary>
    public void ResetCurrentStage()
    {
        if (isGameOverSequencePlaying || tiles == null) return;
        bool consumedRetryHeart = ConsumeHeartForManualRetryIfNeeded();
        TrackStageReset(consumedRetryHeart ? "manual_reset_retry_penalty" : "manual_reset");
        isGameOverSequencePlaying = true;
        StartCoroutine(ResetCurrentStageRoutine());
    }

    private bool ConsumeHeartForManualRetryIfNeeded()
    {
        // 시행착오는 퍼즐의 핵심이므로 수동 리셋은 하트를 쓰지 않는다. 하트는 게임오버에서만 차감한다.
        if (!ManualRetryConsumesHeart)
            return false;

        int retryMoveCount = GetTotalPathCount();
        if (retryMoveCount < ManualRetryHeartPenaltyMoveThreshold)
            return false;

        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();

        return mainUI != null && mainUI.ConsumeHeartOnManualRetry(retryMoveCount);
    }

    private void ResetTileForStageRestart(Tile tile)
    {
        if (tile == null)
            return;

        tile.ResetTransientVisualState();

        var crossBlast = tile.GetComponent<CrossBlastTile>();
        if (crossBlast != null)
            crossBlast.ResetTransientVisualState();

        var twinLink = tile.GetComponent<TwinLinkTile>();
        if (twinLink != null)
            twinLink.ResetTransientVisualState();

        tile.ResetToInitial();
        ClearGameOverBlackoutVisualForRestart(tile);

        var hidden = tile.GetComponent<HiddenTile>();
        var igniter = tile.GetComponent<IgniterTile>();
        if (hidden != null)
            hidden.ResetToHiddenState();
        else if (igniter != null)
            igniter.ResetToInitialState();

        if (hidden == null)
            tile.SetScaleZero();

        var fixedKnot = tile.GetComponent<FixedKnotTile>();
        if (fixedKnot != null)
            fixedKnot.ResetGearVisibility();
    }

    private void ClearGameOverBlackoutVisualForRestart(Tile tile)
    {
        if (tile == null || !tile.IsGameOverBlackoutActive)
            return;

        tile.SetBlackout(false);
    }

    private IEnumerator ResetCurrentStageRoutine()
    {
        ResetBoardFailureTracking();
        StopBoardTransientVisuals();
        ClearHintPreview();
        totalStepsCommitted = 0;
        blackoutQuestionFlipPlayed = false;
        if (blackoutQuestionFlipRoutine != null)
        {
            StopCoroutine(blackoutQuestionFlipRoutine);
            blackoutQuestionFlipRoutine = null;
        }
        ResetAllTileQuestionRotations();
        ResetTrail();
        ClearPendingBlockNoteQueue();
        linkSystem?.ClearLinks();
        currentPath.Clear();
        isDragging = false;
        SetNeonTrailEmitting(false);

        for (int row = 0; row < stageHeight; row++)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                if (tiles[row, col] == null) continue;
                ResetTileForStageRestart(tiles[row, col]);
            }
        }

        yield return PlayRestartCascade();

        if (currentStartTile != null)
            currentStartTile.ClearScaleOverride();
        if (tiles != null && initialStartTileRow >= 0 && initialStartTileRow < stageHeight &&
            initialStartTileCol >= 0 && initialStartTileCol < stageWidth)
        {
            Tile initialStart = tiles[initialStartTileRow, initialStartTileCol];
            if (initialStart != null)
            {
                currentStartTile = initialStart;
                currentStartTile.SetInitialStartTile(true);
                ActivateStartIgniterIfNeeded();
                if (spotlightController != null)
                    spotlightController.ResetRevealedToStartOnly(initialStart.transform.position);
            }
        }

        if (linkSystem != null && tiles != null)
            linkSystem.CreateLinksForCrossBlastOnly(tiles, stageWidth, stageHeight);

        RefreshMainUIForStage();
        SetupMelodyForCurrentStage();
        PlayNewStageSfx();
        IncrementBoardVersion("manual_reset");
        HandleStageStarted("manual_reset");
        isGameOverSequencePlaying = false;
    }

    private void RefreshSpecialTileVisualStateAfterAppearance(Tile tile)
    {
        if (tile == null)
            return;

        var fixedKnot = tile.GetComponent<FixedKnotTile>();
        if (fixedKnot != null)
            fixedKnot.RefreshVisualState();

        var igniter = tile.GetComponent<IgniterTile>();
        if (igniter != null)
            igniter.RefreshVisualState();

        var blindCurtain = tile.GetComponent<BlindCurtainTile>();
        if (blindCurtain != null)
            blindCurtain.RefreshVisualState();
    }

    /// <summary>
    /// 모든 타일(특수 타일 포함)이 0이면 클리어. 로그 "Clear" 후 다음 스테이지.
    /// </summary>
    private void CheckStageClear()
    {
        if (stageCleared || tiles == null) return;
        Tile referenceTile = currentStartTile;
        for (int row = 0; row < stageHeight; row++)
            for (int col = 0; col < stageWidth; col++)
                if (tiles[row, col] != null && tiles[row, col].IsActive)
                    return;
        BeginStageClear(StageClearTypeAllTilesZero, referenceTile);
    }

    private const float StageClearTapToAdvanceAfterSeconds = 0.3f;

    private IEnumerator LoadNextStageAfterDelay()
    {
        // 클리어 연출을 기다리되, 0.3초 뒤부터는 화면을 탭하면 바로 다음 스테이지로 넘어간다.
        float totalDelay = Mathf.Max(0f, nextStageDelay);
        float elapsed = 0f;
        while (elapsed < totalDelay)
        {
            yield return null;
            elapsed += Time.deltaTime;
            if (elapsed >= StageClearTapToAdvanceAfterSeconds && IsPointerDown())
                break;
        }

        PrepareForStageTransition();

        int completedStageIndex = currentStageIndex;
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();

        if (mainUI != null)
        {
            mainUI.ShowStageTransitionInterstitialIfNeeded(completedStageIndex, () =>
            {
                if (this == null)
                    return;

                AdvanceAfterStageClear(completedStageIndex);
            });
        }
        else
        {
            AdvanceAfterStageClear(completedStageIndex);
        }
    }

    private void AdvanceAfterStageClear(int completedStageIndex)
    {
        if (ShouldStartFeverAfter(completedStageIndex))
        {
            StartFever(completedStageIndex);
            return;
        }
        if (TryAdvanceToNextStage())
            HandleStageStarted("auto_next_stage");
    }

    // ===== 피버 보드 =====
    // 10스테이지마다 20초짜리 보너스 판이 온다. 즉석 생성 보드(BoardGenerator)라 항상 풀리고, 막혀도 하트 없이 새 판이 나온다.
    // 진행도는 시작할 때 다음 스테이지로 미리 저장해 두므로 피버 중에 앱을 꺼도 다음 판부터 이어진다. 별·업적·스테이지 통계에는 들어가지 않는다.
    private const int FeverEveryStages = 10;
    private const float FeverDurationSeconds = 20f;
    private const float FeverBannerSeconds = 0.9f;
    private const float FeverNextBoardDelay = 0.35f;
    private const float FeverResultSeconds = 2.2f;
    private bool feverActive;
    private bool feverEnding;
    private float feverEndTime;
    private int feverPops;
    private int feverBoards;
    private int feverSeed;
    private int feverLastShownSeconds = -1;
    private List<Vector2Int> feverSolution;

    public bool IsFeverActive => feverActive;
    private int FeverSecondsLeft => Mathf.Max(0, Mathf.CeilToInt(feverEndTime - Time.unscaledTime));

    private bool ShouldStartFeverAfter(int completedStageIndex)
    {
        return VisualTheme.IsSoftClay
            && completedStageIndex >= FeverEveryStages
            && completedStageIndex % FeverEveryStages == 0
            && !FinalEndingProgressStore.IsFinalStageIndex(completedStageIndex)
            && StageManager.LoadStage(completedStageIndex + 1) != null
            && FirebaseBootstrap.GetRemoteBool(FirebaseBootstrap.RcFeverBoardEnabled, true);
    }

    private void StartFever(int completedStageIndex)
    {
        currentStageIndex = completedStageIndex + 1;
        SaveStageProgress();
        feverActive = true;
        feverEnding = false;
        feverPops = 0;
        feverBoards = 0;
        feverSeed = unchecked(completedStageIndex * 7349 + DateTime.Now.DayOfYear * 131);
        feverEndTime = Time.unscaledTime + FeverBannerSeconds + FeverDurationSeconds;
        feverLastShownSeconds = -1;
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        mainUI?.BeginFeverHud();
        FirebaseBootstrap.LogEvent("fever_start", new Dictionary<string, object> { { "after_stage", completedStageIndex } });
        LoadNextFeverBoard();
    }

    private void LoadNextFeverBoard()
    {
        BoardGenerator.Result result = BoardGenerator.Generate(BoardGenerator.Settings.Fever, feverSeed + feverBoards * 31);
        feverBoards++;
        if (result == null)
        {
            EndFever();
            return;
        }
        feverSolution = result.solution;
        StageData data = result.stage;

        ResetActiveDragState("fever_board");
        totalStepsCommitted = 0;
        ClearTiles();
        CreateGridFromStageData(data);
        SetCurrentStartTileFromStageData(data);
        SetupSpotlight(data);
        AdjustCameraToFitGrid();
        stageCleared = false;
        isGameOverSequencePlaying = false;

        int total = GetTotalRemainingCount();
        initialTileCountForUI = Mathf.Max(1, total);
        mainUI?.SetupFeverBoard(initialTileCountForUI, total);
        mainUI?.UpdateFeverHud(FeverSecondsLeft, feverPops, false);
        SetupMelodyForCurrentStage();
        PlayNewStageSfx();
        IncrementBoardVersion("fever_board");

        ResetUndoHistory();
        attemptMoves.Clear();
        attemptStrokeCount = 0;
        attemptUsedUndo = false;
        gameplayFeedback?.BeginStage(Mathf.Min(tileWidth, tileHeight));
        PlayStageStartVisuals();
    }

    private void OnFeverBoardFinished(bool cleared)
    {
        if (!feverActive || feverEnding)
            return;
        // 다음 판으로 넘어가는 동안 입력을 막는다.
        isGameOverSequencePlaying = true;
        StartCoroutine(FeverNextBoardRoutine(cleared ? FeverNextBoardDelay : FeverNextBoardDelay * 1.6f));
    }

    private IEnumerator FeverNextBoardRoutine(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (!feverActive || feverEnding)
            yield break;
        if (Time.unscaledTime >= feverEndTime - 0.5f)
            EndFever();
        else
            LoadNextFeverBoard();
    }

    /// <summary>스킵·디버그 이동·초기화처럼 다른 경로로 스테이지가 바뀔 때 피버를 조용히 끝낸다(보상 없음).</summary>
    private void CancelFever()
    {
        if (!feverActive)
            return;
        feverActive = false;
        feverEnding = false;
        feverSolution = null;
        isGameOverSequencePlaying = false;
        mainUI?.CancelFeverHud();
    }

    private void UpdateFeverTimer(bool overlayOpen)
    {
        if (!feverActive || feverEnding)
            return;
        // 설정 창 등을 여는 동안은 시간이 흐르지 않는다.
        if (overlayOpen)
        {
            feverEndTime += Time.unscaledDeltaTime;
            return;
        }
        mainUI?.SetFeverTimeFraction((feverEndTime - Time.unscaledTime) / FeverDurationSeconds);
        int seconds = FeverSecondsLeft;
        if (seconds != feverLastShownSeconds)
        {
            feverLastShownSeconds = seconds;
            mainUI?.UpdateFeverHud(seconds, feverPops, false);
        }
        if (Time.unscaledTime >= feverEndTime)
            EndFever();
    }

    private void EndFever()
    {
        if (!feverActive || feverEnding)
            return;
        feverEnding = true;
        stageCleared = true;
        ResetActiveDragState("fever_end");
        ClearHintPreview();
        int rewardHints = feverPops >= 25 ? 2 : 1;
        mainUI?.ShowFeverResult(feverPops, rewardHints);
        PlayClearSfx();
        FirebaseBootstrap.LogEvent("fever_end", new Dictionary<string, object>
        {
            { "pops", feverPops },
            { "boards", feverBoards },
            { "reward_hints", rewardHints }
        });
        StartCoroutine(FinishFeverRoutine());
    }

    private IEnumerator FinishFeverRoutine()
    {
        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < FeverResultSeconds)
        {
            yield return null;
            if (Time.unscaledTime - start >= 0.5f && IsPointerDown())
                break;
        }
        if (!feverActive || !feverEnding)
            yield break;
        feverActive = false;
        feverEnding = false;
        feverSolution = null;
        isGameOverSequencePlaying = false;
        StageData data = StageManager.LoadStage(currentStageIndex);
        if (data == null)
        {
            currentStageIndex = 1;
            data = StageManager.LoadStage(1);
        }
        ApplyLoadedStageData(data, "fever_end");
        HandleStageStarted("after_fever");
    }

    private IEnumerator LoadFinalEndingAfterDelay(FinalEndingSnapshot snapshot)
    {
        float visualDuration = Mathf.Min(Mathf.Max(0f, stageClearWaveDuration), Mathf.Max(0f, nextStageDelay));
        if (visualDuration > 0f)
            yield return new WaitForSeconds(visualDuration);

        ClearHintPreview();
        ResetActiveDragState("final_ending_transition");
        ClearPendingBlockNoteQueue();

        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();

        if (mainUI != null)
        {
            mainUI.ShowFinalEndingWithFinale(snapshot, this);
        }
        else
        {
            Debug.LogWarning("[GameManager] Final ending UI not found. Marking ending replayable and returning to stage 1.");
            FinalEndingProgressStore.MarkEndingCompletedReplayable();
            ReturnToFirstStageFromFinalEnding();
        }
    }

    private void PrepareForStageTransition()
    {
        CancelFever();
        StopBlackoutQuestionFlip();
        ResetBoardFailureTracking();
        StopBoardTransientVisuals();
        ClearHintPreview();
        if (pathLitClearRoutine != null)
        {
            StopCoroutine(pathLitClearRoutine);
            pathLitClearRoutine = null;
        }

        ResetActiveDragState("stage_transition");
        ClearPendingBlockNoteQueue();
    }

    private void StopBoardTransientVisuals()
    {
        gameplayFeedback?.ResetFeedback();
        if (stageStartVisualRoutine != null)
        {
            StopCoroutine(stageStartVisualRoutine);
            stageStartVisualRoutine = null;
        }

        if (stageClearWaveRoutine != null)
        {
            StopCoroutine(stageClearWaveRoutine);
            stageClearWaveRoutine = null;
        }

        if (stageClearWaveLine != null)
            stageClearWaveLine.enabled = false;

        if (mainUI != null)
            mainUI.ResetTransientStageClearVisuals();
    }

    private void PlayStageStartVisuals()
    {
        if (tiles == null)
            return;

        if (stageStartVisualRoutine != null)
            StopCoroutine(stageStartVisualRoutine);
        stageStartVisualRoutine = StartCoroutine(StageStartVisualRoutine());
    }

    private IEnumerator StageStartVisualRoutine()
    {
        List<Tile> visibleTiles = new List<Tile>(stageWidth * stageHeight);
        for (int row = stageHeight - 1; row >= 0; row--)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                Tile tile = tiles[row, col];
                if (!ShouldUseTileForStageStartVisual(tile))
                    continue;

                tile.SetScaleZero();
                visibleTiles.Add(tile);
            }
        }

        int count = Mathf.Max(1, visibleTiles.Count);
        float interval = Mathf.Min(tileAppearInterval, stageStartCascadeDuration / count);
        for (int i = 0; i < visibleTiles.Count; i++)
        {
            Tile tile = visibleTiles[i];
            if (tile == null)
                continue;

            tile.PlayBounceAppearance();
            RefreshSpecialTileVisualStateAfterAppearance(tile);
            if (interval > 0f)
                yield return new WaitForSeconds(interval);
        }

        RestoreCurrentStartMarkerVisual();
        stageStartVisualRoutine = null;
    }

    private bool ShouldUseTileForStageStartVisual(Tile tile)
    {
        if (tile == null || tile.CurrentNumber <= 0)
            return false;

        var hidden = tile.GetComponent<HiddenTile>();
        return hidden == null || hidden.IsLive;
    }

    private void RestoreCurrentStartMarkerVisual()
    {
        if (currentStartTile == null)
            return;

        currentStartTile.SetInitialStartTile(true);
    }

    private void PlayStageClearVisuals(Tile referenceTile)
    {
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI != null)
            mainUI.PlayStageClearGlow();

        if (stageClearWaveRoutine != null)
            StopCoroutine(stageClearWaveRoutine);
        if (GameplayFeedback.Intensity > 0f)
            stageClearWaveRoutine = StartCoroutine(StageClearWaveRoutine(referenceTile));
    }

    private IEnumerator StageClearWaveRoutine(Tile referenceTile)
    {
        LineRenderer line = EnsureStageClearWaveLine();
        if (line == null)
            yield break;

        Vector3 origin = referenceTile != null ? referenceTile.transform.position : GetBoardCenterWorld();
        origin.z = -0.48f;
        float tileSize = Mathf.Max(0.01f, Mathf.Min(tileWidth, tileHeight));
        float startRadius = tileSize * 0.35f;
        float endRadius = Mathf.Max(totalGridWidth, totalGridHeight) * 0.68f + tileSize;
        float duration = Mathf.Max(0.05f, stageClearWaveDuration);
        int segments = 64;
        float lineWidth = Mathf.Max(0.02f, tileSize * stageClearWaveWidthScale);

        line.enabled = true;
        line.positionCount = segments + 1;
        line.widthMultiplier = lineWidth;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);
            float radius = Mathf.Lerp(startRadius, endRadius, eased);
            float alpha = Mathf.Sin(t * Mathf.PI) * stageClearWaveAlpha * GameplayFeedback.Intensity;
            // 점토 테마: 클리어 물결은 테라코타(네온 시안 대신).
            Color color = VisualTheme.IsSoftClay
                ? new Color(VisualTheme.Accent.r, VisualTheme.Accent.g, VisualTheme.Accent.b, alpha * 0.55f)
                : new Color(0.7f, 1.25f, 1.25f, alpha);
            line.startColor = color;
            line.endColor = color;
            UpdateCircleLine(line, origin, radius, segments);
            yield return null;
        }

        line.enabled = false;
        stageClearWaveRoutine = null;
    }

    private LineRenderer EnsureStageClearWaveLine()
    {
        if (stageClearWaveLine != null)
            return stageClearWaveLine;

        stageClearWaveLine = CreateHintPreviewLine("StageClearWaveLine", 84);
        stageClearWaveLine.loop = false;
        stageClearWaveLine.numCapVertices = 0;
        stageClearWaveLine.numCornerVertices = 6;
        return stageClearWaveLine;
    }

    private static void UpdateCircleLine(LineRenderer line, Vector3 origin, float radius, int segments)
    {
        if (line == null)
            return;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;
            Vector3 point = origin + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            line.SetPosition(i, point);
        }
    }

    private Vector3 GetBoardCenterWorld()
    {
        return transform.position;
    }

    private bool TryAdvanceToNextStage()
    {
        if (IsOnFinalStage)
            return false;

        currentStageIndex++;
        StageData data = StageManager.LoadStage(currentStageIndex);
        if (data == null)
        {
            // 데이터 누락 시 안전장치. 최종 스테이지 이후로는 위에서 막는다.
            Debug.LogError($"[GameManager] Missing stage data for stage {currentStageIndex}. Falling back to stage 1.");
            currentStageIndex = 1;
            data = StageManager.LoadStage(1);
            if (data == null)
            {
                stageCleared = false;
                return false;
            }
        }

        ApplyLoadedStageData(data, "stage_loaded");
        return true;
    }

    private void ApplyLoadedStageData(StageData data, string boardVersionReason, bool resetHeartsForNewStage = true)
    {
        UpdateVerboseStage6DebugState(data);

        ResetActiveDragState(boardVersionReason);
        totalStepsCommitted = 0;
        ClearTiles();
        CreateGridFromStageData(data);
        SetCurrentStartTileFromStageData(data);
        SetupSpotlight(data);
        AdjustCameraToFitGrid();
        stageCleared = false;

        RefreshMainUIForStage();
        if (resetHeartsForNewStage)
            ResetMainUIHeartsForNewStage();
        SetupMelodyForCurrentStage();
        PlayNewStageSfx();
        SaveStageProgress();
        PrewarmUpcomingStages();
        IncrementBoardVersion(boardVersionReason);
    }

    private void PrewarmUpcomingStages()
    {
        const int warmupCount = 6;
        int warmed = StageManager.PrewarmStages(currentStageIndex, warmupCount);
        if (warmed > 0)
            DevLog.Log($"[GameManager] Stage prewarm complete. start={currentStageIndex}, warmed={warmed}");
    }

    private void NotifySplashStageBootstrapCompleted()
    {
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI != null)
            mainUI.NotifyStageBootstrapCompleted();
    }

    /// <summary>Easy Save 3로 현재 스테이지 인덱스 저장. 클리어 후·앱 종료/일시정지 시 호출.</summary>
    private void SaveStageProgress()
    {
        // 앱이 시작 직후(Start에서 진행도를 읽기 전) 일시정지되면 currentStageIndex가 아직 0이다.
        // 이때 저장하면 진행도가 0으로 덮어써져 다음 실행에 없는 스테이지(0)가 열린다.
        if (!hasLoadedStageProgress || currentStageIndex < 1)
            return;
        try
        {
            ES3.Save(SaveKeyStage, currentStageIndex);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameManager] 진행도 저장 실패: {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, "SaveStageProgress failed");
        }
    }

    /// <summary>저장된 스테이지 진행도 키를 삭제한다. (데이터 초기화용)</summary>
    public static void ClearSavedStageProgress()
    {
        try
        {
            if (ES3.KeyExists(SaveKeyStage))
                ES3.DeleteKey(SaveKeyStage);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameManager] 진행도 초기화 실패: {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, "ClearSavedStageProgress failed");
        }

        PlayerPrefs.DeleteKey(SaveKeyStage);
        PlayerPrefs.Save();
    }

    /// <summary>저장된 스테이지 인덱스 로드. 없으면 startStageIndex 반환.</summary>
    private bool hasLoadedStageProgress;

    private static bool IsValidStageIndex(int index) => index >= 1 && index <= FinalEndingProgressStore.FinalStageIndex;

    /// <summary>진행도 값이 망가졌을 때(0 등) 업적 기록에 남은 현재 스테이지로 되살린다.</summary>
    private static int RecoverStageIndexFromAchievementRecord()
    {
        try
        {
            if (!ES3.KeyExists("AchievementProgressJson"))
                return 0;
            string json = ES3.Load<string>("AchievementProgressJson");
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(json ?? string.Empty, "\"currentStageIndex\"\\s*:\\s*(\\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int index) && IsValidStageIndex(index))
                return index;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameManager] 진행도 복구 실패: {e.Message}");
        }
        return 0;
    }

    private int LoadSavedStageIndex()
    {
        try
        {
            if (ES3.KeyExists(SaveKeyStage))
            {
                int saved = ES3.Load<int>(SaveKeyStage);
                if (IsValidStageIndex(saved))
                    return saved;
                int recovered = RecoverStageIndexFromAchievementRecord();
                Debug.LogWarning($"[GameManager] 저장된 진행도({saved})가 올바르지 않아 {(recovered > 0 ? recovered.ToString() : "처음")}부터 시작합니다.");
                if (recovered > 0)
                    return recovered;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameManager] 진행도 로드 실패: {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, "LoadSavedStageIndex failed");
        }
        return startStageIndex;
    }

    private void OnApplicationPause(bool pause)
    {
        isApplicationPaused = pause;
        if (pause)
            SaveStageProgress();
        else
            ConfigureDeviceMaxFrameRate();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        hasApplicationFocus = hasFocus;
        if (hasFocus)
            ConfigureDeviceMaxFrameRate();
    }

    private void OnApplicationQuit()
    {
        SaveStageProgress();
        pendingSessionFreeHeartRefillMinutes.Clear();
        IsPerformanceOverlayOpen = false;
    }

    // 턴제 퍼즐이므로 평소에는 60fps로 두고, 손가락으로 그리는 동안(및 직후 연출)만 기기 최대 주사율을 쓴다.
    // 생각하는 동안 120~240fps로 블룸 화면을 계속 그리던 발열·배터리 소모를 줄인다.
    private const int BaselineFrameRate = 60;
    private const float FrameRateBoostHoldSeconds = 0.8f;
    private int deviceMaxFrameRate = BaselineFrameRate;
    private float frameRateBoostUntil;
    private int appliedTargetFrameRate = -1;

    private void ConfigureDeviceMaxFrameRate()
    {
        if (!useDeviceMaxFps)
            return;

        QualitySettings.vSyncCount = 0;
        deviceMaxFrameRate = GetDeviceMaxFrameRate();
        Time.maximumDeltaTime = 0.1f;
        appliedTargetFrameRate = -1;
        UpdateAdaptiveFrameRate();
    }

    private void UpdateAdaptiveFrameRate()
    {
        if (!useDeviceMaxFps)
            return;

        if (isDragging)
            frameRateBoostUntil = Time.unscaledTime + FrameRateBoostHoldSeconds;
        bool boost = Time.unscaledTime < frameRateBoostUntil;
        int desired = boost ? deviceMaxFrameRate : Mathf.Min(BaselineFrameRate, deviceMaxFrameRate);
        if (desired == appliedTargetFrameRate)
            return;

        appliedTargetFrameRate = desired;
        Application.targetFrameRate = desired;
    }

    private static int GetDeviceMaxFrameRate()
    {
        float maxRefreshRate = 0f;
        Resolution[] resolutions = Screen.resolutions;
        if (resolutions != null && resolutions.Length > 0)
        {
            for (int i = 0; i < resolutions.Length; i++)
            {
                float hz = GetResolutionRefreshRate(resolutions[i]);
                if (hz > maxRefreshRate)
                    maxRefreshRate = hz;
            }
        }

        if (maxRefreshRate <= 0f)
            maxRefreshRate = GetResolutionRefreshRate(Screen.currentResolution);
        if (maxRefreshRate <= 0f)
            maxRefreshRate = 60f;

        return Mathf.Clamp(Mathf.RoundToInt(maxRefreshRate), 30, 240);
    }

    private static float GetResolutionRefreshRate(Resolution resolution)
    {
#if UNITY_2022_2_OR_NEWER
        return (float)resolution.refreshRateRatio.value;
#else
        return resolution.refreshRate;
#endif
    }

    private void ClearTiles()
    {
        ResetBoardFailureTracking();
        StopBoardTransientVisuals();
        linkSystem?.ClearLinks();
        hiddenGroups.Clear();
        twinLinkGroups.Clear();
        if (tiles != null)
        {
            for (int row = 0; row < stageHeight; row++)
                for (int col = 0; col < stageWidth; col++)
                    if (tiles[row, col] != null)
                    {
                        ClearGameOverBlackoutVisualForRestart(tiles[row, col]);
                        Destroy(tiles[row, col].gameObject);
                        tiles[row, col] = null;
                    }
        }
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponent<Tile>() != null || child.name == ClaySlotContainerName)
                Destroy(child.gameObject);
        }
        claySlotContainer = null;
    }

    private const string ClaySlotContainerName = "ClaySlots";
    private Transform claySlotContainer;

    /// <summary>
    /// 점토 테마: 칸 아래에 음각 자리를 깔아 둔다. 타일이 0이 되어 사라지면 이 자리가 보인다.
    /// 숨은(Hidden) 칸에는 깔지 않는다(켜지기 전 위치가 드러나지 않게).
    /// </summary>
    private void CreateClaySlot(float wx, float wy, string cellType)
    {
        if (!VisualTheme.IsSoftClay || cellType == "Hidden")
            return;
        Sprite slot = VisualTheme.SlotSprite;
        if (slot == null)
            return;
        if (claySlotContainer == null)
        {
            claySlotContainer = new GameObject(ClaySlotContainerName).transform;
            claySlotContainer.SetParent(transform, false);
        }
        var go = new GameObject("Slot");
        go.transform.SetParent(claySlotContainer, false);
        go.transform.position = new Vector3(wx, wy, 0.05f);
        SpriteRenderer prefabRenderer = tilePrefab != null ? tilePrefab.GetComponentInChildren<SpriteRenderer>(true) : null;
        if (prefabRenderer != null && prefabRenderer.sprite != null)
        {
            // 프리팹 타일과 같은 크기로 맞춘다(그리드 간격은 프리팹 스프라이트 기준).
            Vector2 target = prefabRenderer.bounds.size;
            Vector2 size = slot.bounds.size;
            go.transform.localScale = new Vector3(target.x / size.x, target.y / size.y, 1f);
        }
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = slot;
        sr.color = VisualTheme.SlotTint;
        sr.sortingOrder = (prefabRenderer != null ? prefabRenderer.sortingOrder : 0) - 5;
    }

    /// <summary>Multi-Color Neon Trail: 속 빈 윤곽선 셰이더 + 4색 그라데이션, 시작 밝게·끝 투명.</summary>
    private void CreateNeonTrail()
    {
        GameObject trailGo = new GameObject("NeonTrail");
        trailGo.transform.SetParent(transform);
        trailGo.transform.position = new Vector3(0f, 0f, -0.5f);

        neonTrail = trailGo.AddComponent<TrailRenderer>();
        neonTrail.time = trailTime;
        neonTrail.minVertexDistance = trailMinVertexDistance;
        neonTrail.emitting = false;
        neonTrail.textureMode = LineTextureMode.Stretch;
        neonTrail.alignment = LineAlignment.View;
        neonTrail.numCapVertices = 6;
        neonTrail.numCornerVertices = 4;
        neonTrail.shadowCastingMode = ShadowCastingMode.Off;
        neonTrail.receiveShadows = false;

        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0f, 0.5f);
        widthCurve.AddKey(1f, 0f);
        neonTrail.widthCurve = widthCurve;

        Material trailMat = CreateNeonTrailMaterial();
        if (trailMat != null)
            neonTrail.material = trailMat;

        // 4색 이상 그라데이션: 시작(손가락)은 가장 밝게, 끝(꼬리)은 Alpha 0. 순환은 UpdateNeonTrailColor에서 처리
        ApplyNeonTrailGradient(0f);

        neonTrail.sortingOrder = 10;
        neonTrailTransform = trailGo.transform;
    }

    private Material CreateNeonTrailMaterial()
    {
        Shader shader = ResolveHollowTrailShader();
        bool useHollowShader = shader != null;
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("[GameManager] 네온 트레일 셰이더를 찾지 못했습니다. 기본 TrailRenderer 머티리얼을 사용합니다.");
            return null;
        }

        Material trailMat = new Material(shader)
        {
            name = useHollowShader ? "Hollow Neon Trail (Runtime)" : "Fallback Neon Trail (Runtime)"
        };

        if (trailMat.HasProperty(TrailTintColorId))
            trailMat.SetColor(TrailTintColorId, Color.white);
        else
            trailMat.color = Color.white;

        if (useHollowShader)
        {
            SetTrailMaterialFloat(trailMat, TrailOutlineWidthId, trailOutlineWidth);
            SetTrailMaterialFloat(trailMat, TrailOutlineSoftnessId, trailOutlineSoftness);
            SetTrailMaterialFloat(trailMat, TrailGlowWidthId, trailGlowWidth);
            SetTrailMaterialFloat(trailMat, TrailGlowAlphaId, 0.42f);
            SetTrailMaterialFloat(trailMat, TrailGlowIntensityId, 0.75f);
            SetTrailMaterialFloat(trailMat, TrailCenterAlphaId, trailCenterAlpha);
        }

        return trailMat;
    }

    private Shader ResolveHollowTrailShader()
    {
        if (hollowTrailShader != null)
            return hollowTrailShader;

        Shader shader = Resources.Load<Shader>("Shaders/HollowNeonTrail");
        if (shader != null)
            return shader;

        return Shader.Find("ZeroStep/HollowNeonTrail");
    }

    private static void SetTrailMaterialFloat(Material material, int propertyId, float value)
    {
        if (material != null && material.HasProperty(propertyId))
            material.SetFloat(propertyId, value);
    }

    /// <summary>Time.time 기반으로 그라데이션 색상 키를 순환(Shift)시키고, 특수 타일 Lerp 중이면 해당 컬러로 블렌드.</summary>
    private void UpdateNeonTrailColor()
    {
        if (neonTrail == null) return;

        float t = Time.time;
        bool inTileLerp = (t - specialTileColorLerpStartTime) < specialTileColorLerpDuration && specialTileColorLerpStartTime > -900f;
        if (!isDragging && !inTileLerp)
            return;

        float minUpdateInterval = 1f / Mathf.Max(10f, trailGradientUpdateHz);
        if (!inTileLerp && t < nextTrailGradientUpdateTime)
            return;
        nextTrailGradientUpdateTime = t + minUpdateInterval;

        float blend = 0f;
        if (inTileLerp)
        {
            float elapsed = t - specialTileColorLerpStartTime;
            if (elapsed < specialTileColorLerpDuration * 0.5f)
                blend = elapsed / (specialTileColorLerpDuration * 0.5f);
            else
                blend = 1f - (elapsed - specialTileColorLerpDuration * 0.5f) / (specialTileColorLerpDuration * 0.5f);
        }

        // 그라데이션 키 순환: phase에 따라 4색이 흐르는 느낌
        float phase = t * trailColorShiftSpeed;
        ApplyNeonTrailGradient(phase, inTileLerp ? blend : 0f, inTileLerp ? specialTileColor : default);
    }

    /// <summary>네온 그라데이션 적용. phase로 색상 키 순환, tileBlend &gt; 0이면 specialColor로 Lerp.</summary>
    private void ApplyNeonTrailGradient(float phase, float tileBlend = 0f, Color specialColor = default)
    {
        if (neonTrail == null) return;
        neonTrail.widthMultiplier = gameplayFeedback != null ? gameplayFeedback.TrailWidthMultiplier : 1f;
        Color[] baseColors = (neonGradientColors != null && neonGradientColors.Length >= 4) ? neonGradientColors : fallbackNeonColors;
        int n = baseColors.Length;
        EnsureTrailGradientBufferSize(n + 1);

        // HDR 강도 적용 (Bloom용)
        float intensity = trailHdrIntensity * (gameplayFeedback != null ? gameplayFeedback.TrailIntensityMultiplier : 1f);
        float step = 1f / Mathf.Max(1, n);
        for (int i = 0; i <= n; i++)
        {
            float keyTime = i * step;
            int idx = ((int)Mathf.Floor(phase) + i) % n;
            if (idx < 0) idx += n;
            Color c = baseColors[idx];
            c = new Color(c.r * intensity, c.g * intensity, c.b * intensity, c.a);
            if (tileBlend > 0.001f && specialColor.a > 0.001f)
            {
                Color sc = new Color(specialColor.r * intensity, specialColor.g * intensity, specialColor.b * intensity, specialColor.a);
                c = Color.Lerp(c, sc, tileBlend);
            }
            reusableTrailColorKeys[i] = new GradientColorKey(c, keyTime);
        }
        reusableTrailGradient.SetKeys(reusableTrailColorKeys, reusableTrailAlphaKeys);
        neonTrail.colorGradient = reusableTrailGradient;
    }

    private void EnsureTrailGradientBufferSize(int size)
    {
        if (reusableTrailColorKeys == null || reusableTrailColorKeys.Length != size)
            reusableTrailColorKeys = new GradientColorKey[size];
    }

    private void EnsureInputAndRaycaster()
    {
        EventSystem es = FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            var esGo = new GameObject("EventSystem");
            es = esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }
        else
        {
            var oldModule = es.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                Destroy(oldModule);
                if (es.GetComponent<InputSystemUIInputModule>() == null)
                    es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        if (mainCamera != null && mainCamera.GetComponent<Physics2DRaycaster>() == null)
            mainCamera.gameObject.AddComponent<Physics2DRaycaster>();
    }

    private void EnsureCameraPostProcessingAndHDR()
    {
        if (mainCamera == null) return;
        var camData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
        if (camData != null)
        {
            // 점토 테마는 밝은 종이 배경이라 블룸을 켜면 화면 전체가 뿌옇게 번진다.
            camData.renderPostProcessing = !VisualTheme.IsSoftClay;
            camData.allowHDROutput = !VisualTheme.IsSoftClay;
        }
    }

    private void CacheTileSizeFromPrefab()
    {
        var sr = tilePrefab.GetComponentInChildren<SpriteRenderer>(true);
        if (sr == null || sr.sprite == null)
        {
            tileWidth = 1f;
            tileHeight = 1f;
            return;
        }
        Bounds b = sr.sprite.bounds;
        Vector3 scale = tilePrefab.transform.lossyScale;
        tileWidth = b.size.x * scale.x;
        tileHeight = b.size.y * scale.y;
    }

    private void AdjustCameraToFitGrid()
    {
        if (mainCamera == null || !mainCamera.orthographic) return;
        gameplayFeedback?.RestoreCameraPose();

        GetGameplayMarginsNormalized(out float topMargin, out float bottomMargin);
        float orthographicSize = CalculateCameraOrthographicSizeForGrid(totalGridWidth, totalGridHeight, GetCameraAspect(), topMargin, bottomMargin);
        mainCamera.orthographicSize = orthographicSize;
        float yOffset = (topMargin - bottomMargin) * orthographicSize;
        mainCamera.transform.position = new Vector3(0f, yOffset, mainCamera.transform.position.z);
        mainCamera.rect = new Rect(0f, 0f, 1f, 1f);
    }

    private float CalculateCameraOrthographicSizeForGrid(float gridWidth, float gridHeight, float aspect)
    {
        GetGameplayMarginsNormalized(out float top, out float bottom);
        return CalculateCameraOrthographicSizeForGrid(gridWidth, gridHeight, aspect, top, bottom);
    }

    private float CalculateCameraOrthographicSizeForGrid(float gridWidth, float gridHeight, float aspect, float topMargin, float bottomMargin)
    {
        float safeAspect = Mathf.Max(0.01f, aspect);

        // 뷰포트를 자르지 않고 전체 화면을 써서 발광(블룸)이 경계에서 잘리지 않도록 함.
        // 대신 orthographicSize를 키워 그리드가 상·하단 UI 사이 '중앙 밴드'에만 들어가게 함.
        float top = Mathf.Clamp01(topMargin);
        float bottom = Mathf.Clamp01(bottomMargin);
        float centerBandHeight = Mathf.Clamp(1f - top - bottom, 0.2f, 1f);

        // 점토 테마는 발광(블룸)이 없어 가장자리 여유를 크게 둘 필요가 없다(네온: 1.05 × 1.15).
        float margin = VisualTheme.IsSoftClay ? ClayFitMargin : fitMargin;
        float edge = VisualTheme.IsSoftClay ? 1f : screenEdgePadding;
        float sizeByHeight = (gridHeight * 0.5f * margin) / centerBandHeight;
        // 점토 테마: 가로는 좌우에 여백(각 약 6%)을 두어 타일·시작점 물결이 화면 끝에 붙거나 잘리지 않게 한다.
        float widthMargin = VisualTheme.IsSoftClay ? ClayWidthFitMargin : margin;
        float sizeByWidth = (gridWidth * 0.5f) / safeAspect * widthMargin;
        float fitSize = Mathf.Max(sizeByHeight, sizeByWidth) * edge;
        if (VisualTheme.IsSoftClay && tileWidth > 0f)
        {
            // 칸이 적은 판에서 타일이 지나치게 커지지 않게 한 칸을 화면 폭의 ClayMaxTileScreenWidth 이하로 둔다.
            float minSizeForTileCap = tileWidth / (2f * ClayMaxTileScreenWidth * safeAspect);
            fitSize = Mathf.Max(fitSize, minSizeForTileCap);
        }
        return fitSize;
    }

    private void GetGameplayMarginsNormalized(out float top, out float bottom)
    {
        top = Mathf.Clamp01(uiTopMarginNormalized);
        bottom = Mathf.Clamp01(uiBottomMarginNormalized);

        if (mainUI != null && mainUI.HasResolvedGameplayLayout)
        {
            top = Mathf.Clamp01(mainUI.TopGameplayMarginNormalized);
            bottom = Mathf.Clamp01(mainUI.BottomGameplayMarginNormalized);
        }
        else
        {
            bottom = Mathf.Clamp01(bottom + uiBottomGameplayGapNormalized);
        }

        bottom = Mathf.Clamp01(bottom + CalculateStageBottomGameplayBoostNormalized());
        top = Mathf.Clamp01(top);
        bottom = Mathf.Clamp01(bottom);
    }

    private const float ClayFitMargin = 1.03f;
    private const float ClayWidthFitMargin = 1.14f;
    private const float ClayMaxTileScreenWidth = 0.24f;

    private float CalculateStageBottomGameplayBoostNormalized()
    {
        // 점토 테마는 실제 UI 크기로 여백을 잡으므로 세로로 긴 판에 아래 여백을 더 주지 않는다.
        if (VisualTheme.IsSoftClay)
            return 0f;
        if (stageWidth <= 0 || stageHeight <= 0)
            return 0f;

        float gridAspect = totalGridWidth > 0.0001f
            ? totalGridHeight / totalGridWidth
            : (float)stageHeight / Mathf.Max(1, stageWidth);
        float tallStageT = Mathf.InverseLerp(TallStageAspectBoostStart, TallStageAspectBoostFull, gridAspect);
        float largeStageT = Mathf.InverseLerp(LargeStageBoostStartCells, LargeStageBoostFullCells, Mathf.Max(stageWidth, stageHeight));
        float tallBoost = tallStageT * TallStageBottomGameplayBoostMaxNormalized;
        float largeBoost = largeStageT * LargeStageBottomGameplayBoostMaxNormalized;
        return Mathf.Max(tallBoost, largeBoost);
    }

    private void CacheMainUIReference()
    {
        GameMainUIController resolvedUI = mainUI != null ? mainUI : FindFirstObjectByType<GameMainUIController>();
        if (resolvedUI == mainUI && subscribedGameplayLayoutUI == mainUI)
            return;

        if (subscribedGameplayLayoutUI != null)
            subscribedGameplayLayoutUI.GameplayLayoutChanged -= HandleGameplayLayoutChanged;

        mainUI = resolvedUI;
        subscribedGameplayLayoutUI = mainUI;
        if (mainUI != null)
            mainUI.GameplayLayoutChanged += HandleGameplayLayoutChanged;
    }

    private void NotifyMainUIPlayerActivity()
    {
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI != null)
            mainUI.NotifyGameplayActivity();
    }

    private void HandleGameplayLayoutChanged()
    {
        if (!isActiveAndEnabled || mainCamera == null || tiles == null)
            return;

        AdjustCameraToFitGrid();
        SyncBackgroundGridReferenceCameraSize();
    }

    private void OnDestroy()
    {
        StopBoardTransientVisuals();
        ClearHintPreview();
        if (hintPreviewMaterial != null)
        {
            Destroy(hintPreviewMaterial);
            hintPreviewMaterial = null;
        }
        if (subscribedGameplayLayoutUI != null)
            subscribedGameplayLayoutUI.GameplayLayoutChanged -= HandleGameplayLayoutChanged;
        subscribedGameplayLayoutUI = null;
    }

    private float GetCameraAspect()
    {
        return ProceduralGridBackground.CalculateCameraAspect(mainCamera);
    }

    private float GetGridWorldSpan(int tileCount, float tileSize)
    {
        if (tileCount <= 0)
            return 0f;

        return tileCount * tileSize + (tileCount - 1) * padding;
    }

    /// <summary>
    /// JSON 스테이지 데이터로 그리드 생성. count가 0인 셀은 인스턴스화 건너뛰고, startPoint 타일은 시작점 표시.
    /// </summary>
    private void CreateGridFromStageData(StageData data)
    {
        if (data.cells == null || data.startPoint == null) return;
        blackoutQuestionFlipPlayed = false;
        UpdateVerboseStage6DebugState(data);
        LogVerboseStage6Summary(data);

        hiddenGroups.Clear();
        twinLinkGroups.Clear();
        ResetTwinLinkPaletteAssignments();
        stageWidth = data.width;
        stageHeight = data.height;
        totalGridWidth = data.width * tileWidth + (data.width - 1) * padding;
        totalGridHeight = data.height * tileHeight + (data.height - 1) * padding;

        tiles = new Tile[data.height, data.width];
        float startX = -totalGridWidth * 0.5f + tileWidth * 0.5f;
        float startY = -totalGridHeight * 0.5f + tileHeight * 0.5f;

        foreach (CellData cell in data.cells)
        {
            if (cell.count <= 0) continue;

            float wx = startX + cell.x * (tileWidth + padding);
            float wy = startY + cell.y * (tileHeight + padding);

            GameObject tileObj = Instantiate(tilePrefab, transform);
            tileObj.transform.position = new Vector3(wx, wy, 0f);
            tileObj.name = $"Tile_{cell.y}_{cell.x}";
            CreateClaySlot(wx, wy, cell.type);

            Tile tile = tileObj.GetComponent<Tile>();
            if (tile != null)
            {
                tile.SetGridPosition(cell.x, cell.y);
                tile.SetInitialNumber(cell.count);
                if (data.startPoint.x == cell.x && data.startPoint.y == cell.y)
                    tile.SetAsStartPoint(true);
                FixedKnotTile pendingFixedKnot = null;
                int pendingFixedKnotTargetOrder = 1;
                bool pendingFixedKnotIsAbsolute = false;
                // CrossBlast/Blackout는 SetNumber 전에 추가 (Blackout은 초기화 시 숫자 노출 방지)
                if (cell.type == "CrossBlast")
                {
                    var crossBlast = tileObj.AddComponent<CrossBlastTile>();
                    if (cell.properties != null)
                        crossBlast.SetProperties(cell.properties.pulseSpeed, cell.properties.pulseRange, cell.properties.beamColor);
                }
                if (cell.type == "Blackout")
                    tileObj.AddComponent<BlackoutTile>();
                if (cell.type == "BlindCurtain")
                {
                    tileObj.AddComponent<BlindCurtainTile>();
                }
                if (cell.type == "ShortCircuit")
                {
                    var shortCircuit = tileObj.AddComponent<ShortCircuitTile>();
                    shortCircuit.Setup(cell.direction, data.width, data.height, startX, startY, tileWidth, tileHeight, padding);
                }
                if (cell.type == "FixedKnot")
                {
                    pendingFixedKnot = tileObj.AddComponent<FixedKnotTile>();
                    pendingFixedKnotTargetOrder = cell.targetOrder > 0 ? cell.targetOrder : 1;
                    pendingFixedKnotIsAbsolute = cell.properties != null && cell.properties.isAbsolute;
                }
                if (cell.type == "TwinLink")
                {
                    var twinLink = tileObj.AddComponent<TwinLinkTile>();
                    int id = cell.linkID != 0 ? cell.linkID : 101;
                    Color assignedColor = GetOrAssignTwinLinkColor(id);
                    string assignedLabel = GetOrAssignTwinLinkLabel(id);
                    twinLink.Setup(id, assignedColor, twinLinkLightningPrefab, new TwinLinkTile.TwinLinkSettings
                    {
                        borderOffset = twinLinkBorderOffset,
                        boltInterval = twinLinkBoltInterval,
                        chaosFactor = twinLinkChaosFactor,
                        boltGenerations = twinLinkBoltGenerations,
                        boltWidthScale = twinLinkBoltWidthScale,
                        flashDuration = twinLinkFlashDuration,
                        shakeStrength = twinLinkShakeStrength,
                        idleAlphaMultiplier = twinLinkIdleAlphaMultiplier,
                        idleEdgesPerPulse = twinLinkIdleEdgesPerPulse,
                        activationLineWidthScale = twinLinkActivationLineWidthScale,
                        activationLineDuration = twinLinkActivationLineDuration,
                        activationLineAlpha = twinLinkActivationLineAlpha
                    }, assignedLabel);
                    if (!twinLinkGroups.ContainsKey(id))
                        twinLinkGroups[id] = new List<TwinLinkTile>();
                    twinLinkGroups[id].Add(twinLink);
                }
                if (cell.type == "Hidden")
                {
                    var hidden = tileObj.AddComponent<HiddenTile>();
                    string gid = !string.IsNullOrEmpty(cell.groupID) ? cell.groupID : "default";
                    hidden.Setup(gid);
                    if (!hiddenGroups.ContainsKey(gid))
                        hiddenGroups[gid] = new List<HiddenTile>();
                    hiddenGroups[gid].Add(hidden);
                }
                if (cell.type == "Igniter")
                {
                    var igniter = tileObj.AddComponent<IgniterTile>();
                    igniter.Setup(cell.targetID ?? "");
                }
                if (cell.type == "Charge")
                    tileObj.AddComponent<ChargeTile>();
                tile.SetNumber(cell.count);
                if (pendingFixedKnot != null)
                    pendingFixedKnot.Setup(pendingFixedKnotTargetOrder, pendingFixedKnotIsAbsolute);
                var placedIgniter = tileObj.GetComponent<IgniterTile>();
                if (placedIgniter != null)
                    placedIgniter.RefreshVisualState();
                var placedBlindCurtain = tileObj.GetComponent<BlindCurtainTile>();
                if (placedBlindCurtain != null)
                    placedBlindCurtain.RefreshVisualState();
                tiles[cell.y, cell.x] = tile;
                if (cell.type == "Hidden")
                {
                    var hidden = tileObj.GetComponent<HiddenTile>();
                    if (hidden != null) hidden.ResetToHiddenState();
                }
            }
        }

        if (linkSystem == null)
        {
            GameObject go = new GameObject("LinkSystem");
            go.transform.SetParent(transform);
            linkSystem = go.AddComponent<LinkSystem>();
        }
        linkSystem.CreateLinksForCrossBlastOnly(tiles, data.width, data.height);
        // TwinLink: 같은 linkID끼리 파트너 등록
        foreach (var list in twinLinkGroups.Values)
        {
            foreach (var twin in list)
                if (twin != null) twin.SetPartners(list);
        }
        twinLinkGroups.Clear();
        NotifyFixedKnotsUpdateVisual(0);
    }

    private void ResetTwinLinkPaletteAssignments()
    {
        twinLinkAssignedColors.Clear();
        twinLinkAssignedLabels.Clear();
        nextTwinLinkLabelIndex = 0;
        twinLinkAvailablePalette.Clear();
        twinLinkAvailablePalette.AddRange(TwinLinkRandomPalette);
        ShuffleTwinLinkPalette(twinLinkAvailablePalette);
    }

    private Color GetOrAssignTwinLinkColor(int linkId)
    {
        if (twinLinkAssignedColors.TryGetValue(linkId, out Color assignedColor))
            return assignedColor;

        if (twinLinkAvailablePalette.Count == 0)
        {
            twinLinkAvailablePalette.AddRange(TwinLinkRandomPalette);
            ShuffleTwinLinkPalette(twinLinkAvailablePalette);
        }

        assignedColor = twinLinkAvailablePalette[0];
        twinLinkAvailablePalette.RemoveAt(0);
        twinLinkAssignedColors[linkId] = assignedColor;
        return assignedColor;
    }

    private string GetOrAssignTwinLinkLabel(int linkId)
    {
        if (twinLinkAssignedLabels.TryGetValue(linkId, out string assignedLabel))
            return assignedLabel;

        assignedLabel = MakeTwinLinkGroupLabel(nextTwinLinkLabelIndex++);
        twinLinkAssignedLabels[linkId] = assignedLabel;
        return assignedLabel;
    }

    private static string MakeTwinLinkGroupLabel(int labelIndex)
    {
        int normalized = Mathf.Max(0, labelIndex);
        char letter = (char)('A' + (normalized % 26));
        int cycle = normalized / 26;
        return cycle == 0 ? letter.ToString() : $"{letter}{cycle + 1}";
    }

    private static void ShuffleTwinLinkPalette(List<Color> palette)
    {
        for (int i = palette.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            Color temp = palette[i];
            palette[i] = palette[swapIndex];
            palette[swapIndex] = temp;
        }
    }

    /// <summary>
    /// JSON startPoint 또는 폴백 첫 칸을 현재 시작 타일로 설정하고 1.2x 적용.
    /// </summary>
    private void SetCurrentStartTileFromStageData(StageData data)
    {
        if (data != null && data.startPoint != null && tiles != null)
        {
            int sx = data.startPoint.x;
            int sy = data.startPoint.y;
            if (sy >= 0 && sy < stageHeight && sx >= 0 && sx < stageWidth && tiles[sy, sx] != null)
            {
                initialStartTileRow = sy;
                initialStartTileCol = sx;
                currentStartTile = tiles[sy, sx];
                currentStartTile.SetInitialStartTile(true);
                ActivateStartIgniterIfNeeded();
                return;
            }
        }
        if (tiles != null && stageHeight > 0 && stageWidth > 0 && tiles[0, 0] != null)
        {
            initialStartTileRow = 0;
            initialStartTileCol = 0;
            currentStartTile = tiles[0, 0];
            currentStartTile.SetInitialStartTile(true);
            ActivateStartIgniterIfNeeded();
        }
    }

    /// <summary>
    /// config.mode == "Spotlight"일 때 포그 레이어·스포트라이트 설정. 아니면 비활성화.
    /// </summary>
    private void SetupSpotlight(StageData data)
    {
        if (spotlightController == null)
        {
            spotlightController = GetComponent<SpotlightController>();
            if (spotlightController == null)
                spotlightController = gameObject.AddComponent<SpotlightController>();
        }
        if (spotlightController == null) return;

        if (data?.config != null && data.config.mode != null && data.config.mode.Equals("Spotlight", System.StringComparison.OrdinalIgnoreCase))
        {
            Vector2 startWorld = currentStartTile != null ? (Vector2)currentStartTile.transform.position : Vector2.zero;
            float startRadius = data.config.spotlightRadius > 0f ? data.config.spotlightRadius : 2.5f;
            spotlightController.Setup(data.config, startWorld, startRadius);
            var cam = mainCamera != null ? mainCamera : Camera.main;
            if (cam != null) spotlightController.SetCamera(cam);
            spotlightController.SetGameManager(this);
        }
        else
            spotlightController.Disable();
    }

    /// <summary>
    /// 스테이지 JSON 없을 때 폴백: 동일 크기 그리드 전체 생성.
    /// </summary>
    private void CreateGridFallback()
    {
        UpdateVerboseStage6DebugState(null);
        stageWidth = fallbackCols;
        stageHeight = fallbackRows;
        totalGridWidth = fallbackCols * tileWidth + (fallbackCols - 1) * padding;
        totalGridHeight = fallbackRows * tileHeight + (fallbackRows - 1) * padding;

        tiles = new Tile[fallbackRows, fallbackCols];
        float startX = -totalGridWidth * 0.5f + tileWidth * 0.5f;
        float startY = -totalGridHeight * 0.5f + tileHeight * 0.5f;

        for (int row = 0; row < fallbackRows; row++)
        {
            for (int col = 0; col < fallbackCols; col++)
            {
                float x = startX + col * (tileWidth + padding);
                float y = startY + row * (tileHeight + padding);

                GameObject tileObj = Instantiate(tilePrefab, transform);
                tileObj.transform.position = new Vector3(x, y, 0f);
                tileObj.name = $"Tile_{row}_{col}";

                Tile tile = tileObj.GetComponent<Tile>();
                if (tile != null)
                {
                    tile.SetGridPosition(col, row);
                    tile.SetInitialNumber(fallbackInitialNumber);
                    tile.SetNumber(fallbackInitialNumber);
                    tiles[row, col] = tile;
                }
            }
        }
        if (currentStartTile == null && tiles != null && fallbackRows > 0 && fallbackCols > 0 && tiles[0, 0] != null)
        {
            initialStartTileRow = 0;
            initialStartTileCol = 0;
            currentStartTile = tiles[0, 0];
            currentStartTile.SetInitialStartTile(true);
        }
    }

    private void UpdateVerboseStage6DebugState(StageData data)
    {
        VerboseStage6DebugEnabled = currentStageIndex == VerboseDebugStageIndex || (data != null && data.stageID == VerboseDebugStageIndex);
    }

    private bool ShouldLogVerboseStage6Debug()
    {
        return VerboseStage6DebugEnabled;
    }

    private void LogVerboseStage6Summary(StageData data)
    {
        if (!ShouldLogVerboseStage6Debug() || data == null || data.cells == null)
            return;

        int igniterCount = 0;
        int hiddenCount = 0;
        int blackoutCount = 0;
        int blindCurtainCount = 0;
        int shortCircuitCount = 0;
        foreach (CellData cell in data.cells)
        {
            switch (cell.type)
            {
                case "Igniter": igniterCount++; break;
                case "Hidden": hiddenCount++; break;
                case "Blackout": blackoutCount++; break;
                case "BlindCurtain": blindCurtainCount++; break;
                case "ShortCircuit": shortCircuitCount++; break;
            }
        }

        DevLog.Log($"[Stage6 로드 요약] stageIndex={currentStageIndex} stageID={data.stageID} size={data.width}x{data.height} start=({data.startPoint.x},{data.startPoint.y}) igniter={igniterCount} hidden={hiddenCount} blackout={blackoutCount} blindCurtain={blindCurtainCount} shortCircuit={shortCircuitCount}");
    }

    public static string DescribeTileForDebug(Tile tile)
    {
        if (tile == null)
            return "null";

        List<string> tags = new List<string>();
        if (tile.GetComponent<IgniterTile>() != null) tags.Add("Igniter");
        if (tile.GetComponent<HiddenTile>() != null) tags.Add("Hidden");
        if (tile.GetComponent<BlackoutTile>() != null) tags.Add("Blackout");
        if (tile.GetComponent<BlindCurtainTile>() != null) tags.Add("BlindCurtain");
        if (tile.GetComponent<ShortCircuitTile>() != null) tags.Add("ShortCircuit");
        var fixedKnot = tile.GetComponent<FixedKnotTile>();
        if (fixedKnot != null && fixedKnot.IsOrderConstraintActive) tags.Add("FixedKnot");
        if (tile.GetComponent<CrossBlastTile>() != null) tags.Add("CrossBlast");
        if (tile.GetComponent<TwinLinkTile>() != null) tags.Add("TwinLink");
        string typeSummary = tags.Count > 0 ? string.Join("+", tags) : "Normal";
        return $"({tile.X},{tile.Y}) count={tile.CurrentNumber} active={tile.IsActive} type={typeSummary}";
    }

    private string DescribeCurrentPathForDebug()
    {
        if (currentPath == null || currentPath.Count == 0)
            return "[]";

        List<string> parts = new List<string>(currentPath.Count);
        foreach (Tile tile in currentPath)
            parts.Add(DescribeTileForDebug(tile));
        return "[" + string.Join(" -> ", parts) + "]";
    }

    private string DescribeTilePickCandidates(Vector2 screenPoint, Tile preferAdjacentTo = null)
    {
        if (mainCamera == null)
            return "camera=null";

        Vector2 worldPoint = ScreenToWorld2D(screenPoint);
        Collider2D[] cols = preferAdjacentTo != null
            ? Physics2D.OverlapCircleAll(worldPoint, TilePickRadius)
            : Physics2D.OverlapPointAll(worldPoint);
        if (cols == null || cols.Length == 0)
            return $"world=({worldPoint.x:F3},{worldPoint.y:F3}) candidates=[]";

        HashSet<Tile> seen = new HashSet<Tile>();
        List<string> parts = new List<string>();
        foreach (Collider2D col in cols)
        {
            Tile tile = col.GetComponent<Tile>();
            if (tile == null)
                tile = col.GetComponentInParent<Tile>();
            if (tile == null || !seen.Add(tile))
                continue;

            bool adjacent = preferAdjacentTo != null && IsAdjacent(preferAdjacentTo, tile);
            parts.Add($"{DescribeTileForDebug(tile)}, adjacent={adjacent}");
        }

        return $"world=({worldPoint.x:F3},{worldPoint.y:F3}) candidates=[{string.Join(" | ", parts)}]";
    }

#if UNITY_EDITOR
    private void Reset()
    {
        AutoAssignSoundClipsInEditor();
    }

    private void OnValidate()
    {
        AutoAssignSoundClipsInEditor();
    }

    private void AutoAssignSoundClipsInEditor()
    {
        AssignClipIfMissing(ref blockAClip, "Assets/Resources/Sounds/block_a.wav");
        AssignClipIfMissing(ref blockAsClip, "Assets/Resources/Sounds/block_as.wav");
        AssignClipIfMissing(ref blockBClip, "Assets/Resources/Sounds/block_b.wav");
        AssignClipIfMissing(ref blockCClip, "Assets/Resources/Sounds/block_c.wav");
        AssignClipIfMissing(ref blockCoClip, "Assets/Resources/Sounds/block_co.wav");
        AssignClipIfMissing(ref blockCsClip, "Assets/Resources/Sounds/block_cs.wav");
        AssignClipIfMissing(ref blockDClip, "Assets/Resources/Sounds/block_d.wav");
        AssignClipIfMissing(ref blockDsClip, "Assets/Resources/Sounds/block_ds.wav");
        AssignClipIfMissing(ref blockEClip, "Assets/Resources/Sounds/block_e.wav");
        AssignClipIfMissing(ref blockFClip, "Assets/Resources/Sounds/block_f.wav");
        AssignClipIfMissing(ref blockFsClip, "Assets/Resources/Sounds/block_fs.wav");
        AssignClipIfMissing(ref blockGClip, "Assets/Resources/Sounds/block_g.wav");
        AssignClipIfMissing(ref blockGsClip, "Assets/Resources/Sounds/block_gs.wav");
        AssignClipIfMissing(ref failClip, "Assets/Resources/Sounds/fail.wav");
        AssignClipIfMissing(ref newStageClip, "Assets/Resources/Sounds/new_stage.wav");
        AssignClipIfMissing(ref clearClip, "Assets/Resources/Sounds/clear.wav");
        AssignClipIfMissing(ref bombClip, "Assets/Resources/Sounds/bomb.wav");
    }

    private static void AssignClipIfMissing(ref AudioClip target, string assetPath)
    {
        if (target != null)
            return;
        target = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
    }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // ===== 자동 검증(PlayMode 테스트) 전용 API: 손가락 입력 없이 실제 규칙 코드로 드래그를 재현한다. =====
    public int AutomationStageIndex => currentStageIndex;
    public bool AutomationStageCleared => stageCleared;
    public bool AutomationGameOverPlaying => isGameOverSequencePlaying;
    public bool AutomationIsDragging => isDragging;
    public bool AutomationHasPendingMutations => pendingBoardMutationCount > 0;
    public bool AutomationFeverActive => feverActive;
    public int AutomationFeverBoards => feverBoards;
    public int AutomationFeverPops => feverPops;
    public List<Vector2Int> AutomationFeverSolution => feverSolution;

    /// <summary>테스트용: completedStageIndex를 깬 직후처럼 피버를 시작한다. durationSeconds로 피버 길이를 바꿀 수 있다.</summary>
    public void AutomationStartFever(int completedStageIndex, float durationSeconds = FeverDurationSeconds)
    {
        StartFever(completedStageIndex);
        feverEndTime = Time.unscaledTime + FeverBannerSeconds + durationSeconds;
    }
    public int AutomationTotalRemaining => GetTotalRemainingCount();
    public Vector2Int AutomationCurrentCell => currentStartTile != null ? new Vector2Int(currentStartTile.X, currentStartTile.Y) : new Vector2Int(-1, -1);

    public int AutomationGetCount(int x, int y)
    {
        Tile tile = GetTileAtGrid(x, y);
        return tile != null ? tile.CurrentNumber : -1;
    }

    public bool AutomationBeginDrag()
    {
        if (stageCleared || isGameOverSequencePlaying || currentStartTile == null || !CanEnterTile(currentStartTile))
            return false;

        ClearMoveRuleSnackbarState();
        BeginFollowingHintPreview();
        currentStartTile.ClearScaleOverride();
        ResetStrokeCombo();
        isDragging = true;
        currentPath.Clear();
        currentPath.Add(currentStartTile);
        lastStepFrame = Time.frameCount;
        lastStepTime = Time.unscaledTime;
        NotifyTrailTileStepped(currentStartTile);
        OnDragStartTrail();
        gameplayFeedback?.DragStarted(currentStartTile);
        gameplayFeedback?.UpdatePath(currentPath);
        return true;
    }

    /// <summary>드래그 중 (x,y)로 한 칸 이동을 시도한다. 규칙상 거부되면 false.</summary>
    public bool AutomationStep(int x, int y)
    {
        if (!isDragging || currentPath.Count == 0)
            return false;

        Tile last = currentPath[currentPath.Count - 1];
        Tile hit = GetTileAtGrid(x, y);
        if (last == null || hit == null || hit == last || !CanEnterTile(hit))
            return false;

        int before = currentPath.Count;
        TryStepToTile(last, hit);
        return currentPath.Count > before;
    }

    public void AutomationEndDrag()
    {
        gameplayFeedback?.DragReleased(currentPath.Count > 0 ? currentPath[currentPath.Count - 1] : currentStartTile);
        gameplayFeedback?.ReleasePath();
        ClearMoveRuleSnackbarState();
        isDragging = false;
        ResetTrail();
        CommitPathAndSetCurrentPosition();
        CheckVictoryCondition(currentStartTile);
        CheckStageClear();
        if (!stageCleared)
            CheckAndHandleBoardFailure(currentStartTile);
    }

    public bool AutomationHintAvailableNow()
    {
        return TryGetHintPreviewPath(out _, useQuickBudget: false);
    }
#endif
}
