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
    [Tooltip("번개 갱신 간격(초). 낮을수록 더 부드럽게 흐름 (권장 0.025~0.04)")]
    [SerializeField] [Range(0.015f, 0.06f)] private float twinLinkBoltInterval = 0.03f;
    [Tooltip("전기 꺾임(0~0.5). 낮을수록 직선에 가깝고 끊김 없이 흐름")]
    [SerializeField] [Range(0f, 0.5f)] private float twinLinkChaosFactor = 0.025f;
    [Tooltip("번개 세부 분할. 높을수록 부드럽고 촘촘한 전기 라인")]
    [SerializeField] [Range(2, 6)] private int twinLinkBoltGenerations = 4;
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
    [Tooltip("힌트 기본 경로 색. 어두운 보드와 네온 타일 위에서 보이도록 라임-시안 계열 사용")]
    [SerializeField] private Color hintPreviewColor = new Color(0.58f, 1f, 0.08f, 1f);
    [Tooltip("경로를 훑는 밝은 하이라이트 색")]
    [SerializeField] private Color hintPreviewFlowColor = new Color(0.93f, 1f, 0.45f, 1f);
    [Tooltip("밝은 하이라이트가 힌트 경로 전체를 한 번 훑는 시간(초)")]
    [SerializeField] private float hintPreviewSweepDuration = 1.15f;
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

    [Header("진동")]
    [Tooltip("스테이지 클리어 진동 길이(ms). Android 전용 미세 조정")]
    [SerializeField] [Range(10, 120)] private int stageClearHapticDurationMs = 20;
    [Tooltip("스테이지 클리어 진동 강도(1~255). Android 전용 미세 조정")]
    [SerializeField] [Range(1, 255)] private int stageClearHapticAmplitude = 30;

    [Header("사운드")]
    [Tooltip("블록 count가 -1 될 때 재생되는 음 볼륨")]
    [SerializeField] [Range(0f, 1f)] private float blockNoteVolume = 0.9f;
    [Tooltip("clear/fail/new stage 효과음 볼륨")]
    [SerializeField] [Range(0f, 1f)] private float eventSfxVolume = 1f;
    [Tooltip("같은 프레임에 여러 count 감소가 발생할 때 음 간격(초)")]
    [SerializeField] [Range(0.01f, 0.2f)] private float blockNoteInterval = 0.045f;

    [Header("블록 음계 (Assets/Sounds 자동 연결)")]
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

    [Header("상태 효과음 (Assets/Sounds 자동 연결)")]
    [SerializeField] private AudioClip failClip;
    [SerializeField] private AudioClip newStageClip;
    [SerializeField] private AudioClip clearClip;

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
    private string pendingRestartGameOverSnackbarKey;
    private float pendingRestartGameOverSnackbarDuration;
    /// <summary>게임오버 리셋 시 시작점 복원용. tiles[row, col] 인덱스.</summary>
    private int initialStartTileRow;
    private int initialStartTileCol;
    private List<Tile> currentPath = new List<Tile>();
    /// <summary>마지막으로 타일을 경로에 추가한 프레임. 직선 드래그 시 되돌아가기 지터 무시용.</summary>
    private int lastStepFrame = -1;
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
    private readonly List<Color> twinLinkAvailablePalette = new List<Color>();
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
    private const float HintSolverTimeLimitSeconds = 3f;
    private const float HintLoadingMinimumVisibleSeconds = 0.45f;
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
    private Material hintPreviewMaterial;
    private Coroutine hintPreviewRoutine;
    private Coroutine hintPreviewRequestRoutine;
    private bool isHintPreviewRequestRunning;
    private int activeHintPreviewLineCount;
    private readonly List<Vector3> activeHintPreviewPoints = new List<Vector3>(HintPreviewMaxPathTiles);
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
        mainCamera.backgroundColor = Color.black;

        EnsureInputAndRaycaster();
        EnsureCameraPostProcessingAndHDR();
        CreateNeonTrail();
        CacheTileSizeFromPrefab();
        InitializeAudioSystem();
        CacheMainUIReference();

        // 저장된 진행도가 있으면 해당 스테이지부터, 없으면 startStageIndex부터
        currentStageIndex = LoadSavedStageIndex();
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
        ResetMainUIHeartsForNewStage();
        SetupMelodyForCurrentStage();
        PlayNewStageSfx();
        PrewarmUpcomingStages();
        IncrementBoardVersion("app_launch");
        NotifySplashStageBootstrapCompleted();
        HandleStageStarted("app_launch");
        ConfigureDeviceMaxFrameRate();
    }

    private void HandleStageStarted(string entryType)
    {
        SyncBackgroundGridReferenceCameraSize();
        RandomizeBackgroundGridFlow();
        TrackStageStarted(entryType);
        SchedulePostStageStartedFailureCheck();
    }

    private void RandomizeBackgroundGridFlow()
    {
        ProceduralGridBackground background = EnsureProceduralBackground();
        if (background != null)
            background.RandomizeGridFlowDirection(currentStageIndex);
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

    public bool LoadNextStageImmediate()
    {
        if (isGameOverSequencePlaying)
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

    /// <summary>데이터 초기화 직후 호출: 1스테이지로 즉시 복귀하고 진행도를 1로 저장.</summary>
    public void ResetProgressAndRestartToFirstStage()
    {
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

    private void Update()
    {
        CacheMainUIReference();
        bool overlayOpen = mainUI != null && (mainUI.IsSettingPopupOpen || mainUI.IsTutorialPopupOpen || mainUI.IsWaitingForHeartRefill || mainUI.IsSplashActive);
        IsPerformanceOverlayOpen = overlayOpen;

        ProcessPendingBlockNoteQueue();
        UpdateSessionHeartRefillProgress(overlayOpen);
        if (stageCleared || isGameOverSequencePlaying)
            return;

        if (overlayOpen)
        {
            ClearHintPreview();
            if (isDragging)
            {
                isDragging = false;
                currentPath.Clear();
                ResetTrail();
                linkSystem?.ClearPathLit();
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
        BuildBlockNoteClipMap();
        BuildMelodyPoolsIfNeeded();
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

    private void PlayFailSfx() => PlayEventSfx(failClip);
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
#if UNITY_ANDROID && !UNITY_EDITOR
        PlayAndroidOneShotVibration(stageClearHapticDurationMs, stageClearHapticAmplitude);
#else
        Handheld.Vibrate();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void PlayAndroidOneShotVibration(int durationMs, int amplitude)
    {
        int safeDurationMs = Mathf.Clamp(durationMs, 1, 1000);
        int safeAmplitude = Mathf.Clamp(amplitude, 1, 255);
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaClass contextClass = new AndroidJavaClass("android.content.Context"))
            {
                string vibratorService = contextClass.GetStatic<string>("VIBRATOR_SERVICE");
                using (AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", vibratorService))
                {
                    if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                        return;

                    using (AndroidJavaClass versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        int sdkInt = versionClass.GetStatic<int>("SDK_INT");
                        if (sdkInt >= 26)
                        {
                            using (AndroidJavaClass vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                            using (AndroidJavaObject oneShotEffect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)safeDurationMs, safeAmplitude))
                            {
                                vibrator.Call("vibrate", oneShotEffect);
                            }
                        }
                        else
                        {
                            vibrator.Call("vibrate", (long)safeDurationMs);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameManager] Stage clear haptic fallback: {ex.Message}");
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

        if (pointerDown)
        {
            Tile hit = GetTileAtScreen(screenPoint);
            if (hit != currentStartTile || !CanEnterTile(hit))
                return;
            ClearMoveRuleSnackbarState();
            ClearHintPreview();
            if (ShouldLogVerboseStage6Debug())
            {
                Vector2 pointerWorld = ScreenToWorld2D(screenPoint);
                Debug.Log($"[Stage6 드래그 시작] screen=({screenPoint.x:F1},{screenPoint.y:F1}) world=({pointerWorld.x:F3},{pointerWorld.y:F3}) hit={DescribeTileForDebug(hit)} currentStart={DescribeTileForDebug(currentStartTile)}");
            }
            currentStartTile.ClearScaleOverride();
            isDragging = true;
            currentPath.Clear();
            currentPath.Add(hit);
            lastStepFrame = Time.frameCount;
            NotifyTrailTileStepped(hit);
            OnDragStartTrail();
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
            Tile directHit = GetTileAtScreen(screenPoint);
            Tile hit = GetTileAtScreen(screenPoint, preferAdjacentTo: lastForHit);
            if (ShouldLogVerboseStage6Debug() && (directHit != hit || hit == null || hit == previousTile))
            {
                Debug.Log($"[Stage6 입력 판정] screen=({screenPoint.x:F1},{screenPoint.y:F1}) world=({pointerWorld.x:F3},{pointerWorld.y:F3}) last={DescribeTileForDebug(lastForHit)} prev={DescribeTileForDebug(previousTile)} direct={DescribeTileForDebug(directHit)} picked={DescribeTileForDebug(hit)} path={DescribeCurrentPathForDebug()} candidates={DescribeTilePickCandidates(screenPoint, lastForHit)}");
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
                    bool withinJitterFrames = frameDelta >= 0 && frameDelta <= ImmediateBacktrackIgnoreFrames;
                    if (!pointerDirectlyOnBackTile || withinJitterFrames || !stronglyInsideBackTile)
                    {
                        string reason = !pointerDirectlyOnBackTile
                            ? "포인터가 되돌아간 타일 위에 직접 올라오지 않음"
                            : (withinJitterFrames
                                ? "직선 드래그 오락가락 방지"
                                : $"되돌아간 타일 안쪽 진입 부족(backDist={backDist:F3}, currentDist={currentDist:F3}, margin={backtrackMargin:F3})");
                        if (ShouldLogVerboseStage6Debug())
                        {
                            Debug.Log($"[지터 무시] 되돌아감 last=({last.X},{last.Y}) hit=({hit.X},{hit.Y}) frameDelta={frameDelta} ({reason}) direct={DescribeTileForDebug(directHit)} world=({pointerWorld.x:F3},{pointerWorld.y:F3}) path={DescribeCurrentPathForDebug()} candidates={DescribeTilePickCandidates(screenPoint, last)}");
                        }
                        else
                        {
                            Debug.Log($"[지터 무시] 되돌아감 last=({last.X},{last.Y}) hit=({hit.X},{hit.Y}) frameDelta={frameDelta} ({reason})");
                        }
                        canStep = false;
                    }
                }
                if (canStep)
                    TryStepToTile(last, hit);
            }
            else if (ShouldLogVerboseStage6Debug())
            {
                Debug.Log($"[Stage6 입력 후보 무효] last={DescribeTileForDebug(lastForHit)} prev={DescribeTileForDebug(previousTile)} direct={DescribeTileForDebug(directHit)} picked={DescribeTileForDebug(hit)} world=({pointerWorld.x:F3},{pointerWorld.y:F3}) path={DescribeCurrentPathForDebug()} candidates={DescribeTilePickCandidates(screenPoint, lastForHit)}");
            }
        }

        // 손 뗄 때는 항상 커밋(터치만 해도 하트비트·라인 갱신). 타일 추가는 pointerHeld일 때만 해서 터치만 할 때 DecreaseNumber 방지.
        if (pointerUp)
        {
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
                Debug.Log($"[Stage6 이동 거부] reason={blockReason} last={DescribeTileForDebug(last)} hit={DescribeTileForDebug(hit)} nextStep={nextStepNumber} path={DescribeCurrentPathForDebug()}");
            return false;
        }

        var shortCircuitHit = hit != null ? hit.GetComponent<ShortCircuitTile>() : null;
        if (blockReason == LegalMoveBlockReason.ShortCircuitBlocked && shortCircuitHit != null && last != null && shortCircuitHit.IsBlockedEntryFrom(last.X, last.Y))
        {
            ShowMoveRuleSnackbar(
                $"short_circuit_entry:{hit.X}:{hit.Y}:{shortCircuitHit.DirectionLocalizationKey}",
                "snackbar_short_circuit_blocked_entry",
                ("direction", GetLocalizedDirectionLabel(shortCircuitHit.DirectionLocalizationKey)));
            if (ShouldLogVerboseStage6Debug())
                Debug.Log($"[Stage6 이동 거부] reason=short_circuit_entry last={DescribeTileForDebug(last)} hit={DescribeTileForDebug(hit)} nextStep={nextStepNumber}");
            return false;
        }

        var shortCircuitLast = last != null ? last.GetComponent<ShortCircuitTile>() : null;
        if (blockReason == LegalMoveBlockReason.ShortCircuitBlocked && shortCircuitLast != null && hit != null && !shortCircuitLast.IsExitCell(hit.X, hit.Y))
        {
            ShowMoveRuleSnackbar(
                $"short_circuit:{last.X}:{last.Y}:{shortCircuitLast.DirectionLocalizationKey}",
                "snackbar_short_circuit_only_direction",
                ("direction", GetLocalizedDirectionLabel(shortCircuitLast.DirectionLocalizationKey)));
            if (ShouldLogVerboseStage6Debug())
                Debug.Log($"[Stage6 이동 거부] reason=short_circuit_exit last={DescribeTileForDebug(last)} hit={DescribeTileForDebug(hit)} nextStep={nextStepNumber}");
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
                Debug.Log($"[Stage6 이동 거부] reason=fixed_knot_order last={DescribeTileForDebug(last)} hit={DescribeTileForDebug(hit)} nextStep={nextStepNumber}");
            return false;
        }

        if (ShouldLogVerboseStage6Debug())
            Debug.Log($"[Stage6 이동 거부] reason={blockReason} last={DescribeTileForDebug(last)} hit={DescribeTileForDebug(hit)} nextStep={nextStepNumber}");
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
            crossBlast.TriggerExplosion(this, hit);
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
            TriggerBlackoutQuestionFlip();
        }
        TryTriggerIgniter(hit);
        NotifyTrailTileStepped(hit);
        NotifyFixedKnotsUpdateVisual(totalPathCount);
        if (fixedKnotHit != null)
            fixedKnotHit.OnSteppedCorrectly();
        if (CheckAndHandleMissedFixedKnot(totalPathCount))
            return;
        CheckVictoryCondition(hit);
    }

    private void FinalizeStep(Tile last, Tile hit, string stepLabel)
    {
        currentPath.Add(hit);
        lastStepFrame = Time.frameCount;
        Debug.Log($"[스텝] {stepLabel} last=({last.X},{last.Y})→hit=({hit.X},{hit.Y}) pathLen={currentPath.Count}");
    }

    private string GetStepLabel(Tile last, Tile hit, FixedKnotTile fixedKnotHit)
    {
        if (fixedKnotHit != null)
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
        ApplyLeaveTileEffects(last, hit);
        FinalizeStep(last, hit, GetStepLabel(last, hit, fixedKnotHit));
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
    private const int ImmediateBacktrackIgnoreFrames = 15;
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
        Collider2D[] cols = preferAdjacentTo != null
            ? Physics2D.OverlapCircleAll(worldPoint, TilePickRadius)
            : Physics2D.OverlapPointAll(worldPoint);
        Tile first = null;
        Tile closestAdjacent = null;
        float closestSq = float.MaxValue;
        for (int i = 0; i < cols.Length; i++)
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
        Debug.Log($"[타일 -1] 직전 타일 ({last.X},{last.Y}) 떠남 → 다음 ({next.X},{next.Y})으로 이동, 직전 타일 -1");
        int before = last.CurrentNumber;
        DecreaseTileAndPlayBlockNote(last);
        var igniter = last.GetComponent<IgniterTile>();
        if (igniter != null && last.CurrentNumber > 0)
            igniter.OnConsumed();
        Debug.Log($"[타일 -1] ({last.X},{last.Y}) count {before} → {last.CurrentNumber}");
        if (last.CurrentNumber <= 0)
            Debug.Log($"[타일 사라짐] ({last.X},{last.Y}) count 0");

        // 타일 숫자/소멸 변경 직후 진행도 갱신
        RefreshMainUIProgress();
    }

    /// <summary>현재 밟은 타일 로그 (디버그용).</summary>
    private void LogSteppedOn(Tile hit)
    {
        if (hit == null) return;
        Debug.Log($"[현재 밟은 타일] ({hit.X},{hit.Y}) count={hit.CurrentNumber}");
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
            Debug.Log($"[Stage6 Igniter 트리거] stepped={DescribeTileForDebug(steppedTile)} targetID={igniter.TargetID} instant={instant} relay={relayInterval:F3} targets=[{string.Join(" | ", hiddenSummaries)}]");
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

        hintPreviewRequestRoutine = StartCoroutine(CheckHintPreviewAvailabilityRoutine(onComplete));
    }

    private IEnumerator CheckHintPreviewAvailabilityRoutine(Action<bool> onComplete)
    {
        isHintPreviewRequestRunning = true;

        bool hasPath = false;
        bool loadingShown = false;
        float loadingStartedAt = 0f;
        try
        {
            hasPath = TryGetHintPreviewPath(out _, useQuickBudget: true);
            if (!hasPath && IsHintSolverBudgetExhaustedStatus())
            {
                SetHintLoadingVisible(true);
                loadingShown = true;
                loadingStartedAt = Time.realtimeSinceStartup;
                yield return null;

                hasPath = TryGetHintPreviewPath(out _, useQuickBudget: false);

                float remainingVisibleTime = HintLoadingMinimumVisibleSeconds - (Time.realtimeSinceStartup - loadingStartedAt);
                if (remainingVisibleTime > 0f)
                    yield return new WaitForSecondsRealtime(remainingVisibleTime);
            }
        }
        finally
        {
            if (loadingShown)
                SetHintLoadingVisible(false);
            isHintPreviewRequestRunning = false;
            hintPreviewRequestRoutine = null;
        }

        if (!hasPath)
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
            mainUI.ShowHintLoading(HintSolverTimeLimitSeconds);
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
        if (stageCleared || isGameOverSequencePlaying || isDragging || tiles == null)
            return false;
        if (!IsCurrentBoardLiveRemainingTile(currentStartTile))
            return false;

        Tile previousTile = GetHintPreviousTile();
        int nextStepNumber = GetTotalPathCount() + 1;
        bool sameCacheKey = cachedHintBoardVersion == boardVersion &&
            cachedHintStartTile == currentStartTile &&
            cachedHintPreviousTile == previousTile &&
            cachedHintNextStepNumber == nextStepNumber;

        if (sameCacheKey && cachedHintPath.Count > 1)
        {
            return true;
        }

        if (sameCacheKey && cachedHintUnavailable)
            return false;

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
            return false;
        }

        List<Tile> workingPath = new List<Tile>(Mathf.Max(HintPreviewMaxPathTiles, initialState.TotalRemainingCount + 1)) { currentStartTile };
        List<Tile> solutionPath = new List<Tile>(workingPath.Capacity);
        HashSet<string> failedCache = new HashSet<string>();
        int nodeLimit = useQuickBudget ? HintQuickSolverNodeLimit : HintSolverNodeLimit;
        float timeLimitSeconds = useQuickBudget ? HintQuickSolverTimeLimitSeconds : HintSolverTimeLimitSeconds;
        HintSolverBudget budget = new HintSolverBudget(nodeLimit, timeLimitSeconds);
        bool solved = TrySolveHintPath(initialState, workingPath, failedCache, budget, solutionPath);

        if (!solved || solutionPath.Count <= 1)
        {
            cachedHintSolverStatus = budget.IsExhausted ? budget.Status : "not_found";
            cachedHintUnavailable = !budget.IsExhausted;
            Debug.Log($"[힌트 solver 실패] stage={currentStageIndex} boardVersion={boardVersion} start={FormatHintTile(currentStartTile)} remaining={initialState.TotalRemainingCount} nodes={budget.NodeCount} failedCache={failedCache.Count} status={cachedHintSolverStatus}");
            return false;
        }

        cachedHintSolutionPath.AddRange(solutionPath);
        if (!TryBuildCommitSafeHintPreviewPath(solutionPath, initialState, targetMoveCount, cachedHintPath))
        {
            cachedHintSolverStatus = "commit_safe_preview_not_found";
            cachedHintUnavailable = true;
            Debug.Log($"[힌트 안전지점 없음] stage={currentStageIndex} boardVersion={boardVersion} start={FormatHintTile(currentStartTile)} remaining={initialState.TotalRemainingCount} fullMoves={solutionPath.Count - 1} path={FormatHintPath(solutionPath)}");
            return false;
        }

        int previewMoves = Mathf.Max(0, cachedHintPath.Count - 1);
        cachedHintSolverStatus = $"solved nodes={budget.NodeCount} failedCache={failedCache.Count} fullMoves={solutionPath.Count - 1} previewMoves={previewMoves} targetMoves={targetMoveCount}";
        return true;
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
        Debug.Log($"[힌트 없음] stage={currentStageIndex} boardVersion={boardVersion} start={DescribeTileForDebug(currentStartTile)} previous={DescribeTileForDebug(GetHintPreviousTile())} remaining={GetTotalRemainingCount()} dragging={isDragging} cleared={stageCleared} gameOver={isGameOverSequencePlaying} solver={cachedHintSolverStatus}");
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

        Debug.Log(
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
        if (tile.GetComponent<FixedKnotTile>() != null) tags.Add("FixedKnot");
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

    private bool TrySolveHintPath(
        HintSearchState state,
        List<Tile> workingPath,
        HashSet<string> failedCache,
        HintSolverBudget budget,
        List<Tile> solutionPath)
    {
        if (state == null || workingPath == null || failedCache == null || budget == null || solutionPath == null)
            return false;
        if (!budget.TryEnterNode())
            return false;

        if (IsHintSolvedState(state))
        {
            solutionPath.Clear();
            solutionPath.AddRange(workingPath);
            return true;
        }

        if (state.TotalRemainingCount > 0 && state.GetRemainingCount(state.Current) <= 0)
            return false;

        string stateKey = state.BuildCacheKey();
        if (failedCache.Contains(stateKey))
            return false;

        List<HintMoveCandidate> candidates = CollectSortedHintCandidates(state);
        for (int i = 0; i < candidates.Count; i++)
        {
            if (budget.IsExhausted)
                return false;

            Tile candidate = candidates[i].Tile;
            HintSearchState nextState = new HintSearchState(state);
            if (!TryApplyHintStep(nextState, candidate, null, out _))
                continue;

            if (nextState.HasMissedFixedKnot(nextState.NextStepNumber - 1))
                continue;
            if (nextState.TotalRemainingCount > 0 && nextState.GetRemainingCount(nextState.Current) <= 0)
                continue;

            workingPath.Add(candidate);
            if (TrySolveHintPath(nextState, workingPath, failedCache, budget, solutionPath))
                return true;
            workingPath.RemoveAt(workingPath.Count - 1);
        }

        if (!budget.IsExhausted)
            failedCache.Add(stateKey);
        return false;
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
        if (candidate.GetComponent<FixedKnotTile>() != null)
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
                return false;

            Tile partnerTile = partner.GetComponent<Tile>();
            if (partnerTile == null || state.GetRemainingCount(partnerTile) != 1)
                return false;

            groupRemaining++;
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
            out _);
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

        var igniter = nextTile.GetComponent<IgniterTile>();
        if (igniter != null && state.TryTriggerIgniter(igniter))
        {
            if (debugEffects != null)
                debugEffects.Add($"Igniter target={igniter.TargetID} Hidden 활성화");
            score += 18;
        }

        if (nextTile.GetComponent<FixedKnotTile>() != null)
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
            if (!state.IsLiveRemainingTile(candidate))
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
                return false;
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
        Color baseColor = GetHintPreviewColor(hintPreviewMaxAlpha * hintPreviewBaseAlphaMultiplier, false);

        activeHintPreviewPoints.Clear();
        for (int i = 0; i <= segmentCount; i++)
        {
            Vector3 point = path[i].transform.position;
            point.z = -0.45f;
            activeHintPreviewPoints.Add(point);
        }

        for (int i = 0; i < hintPreviewLines.Length; i++)
        {
            LineRenderer line = hintPreviewLines[i];
            bool active = i < segmentCount;
            line.enabled = active;
            if (!active)
                continue;

            line.positionCount = 2;
            line.SetPosition(0, activeHintPreviewPoints[i]);
            line.SetPosition(1, activeHintPreviewPoints[i + 1]);
            line.widthMultiplier = width;
            line.startColor = baseColor;
            line.endColor = baseColor;
        }

        activeHintPreviewLineCount = segmentCount;
        ConfigureHintPreviewFlowLine(width);
        if (hintPreviewRoutine != null)
            StopCoroutine(hintPreviewRoutine);
        hintPreviewRoutine = StartCoroutine(HintPreviewLifetimeRoutine());
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
            return;

        hintPreviewFlowLine = CreateHintPreviewLine("HintPreviewFlowLine", 82);
    }

    private LineRenderer CreateHintPreviewLine(string lineName, int sortingOrder)
    {
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
        return line;
    }

    private void ConfigureHintPreviewFlowLine(float baseWidth)
    {
        if (hintPreviewFlowLine == null || activeHintPreviewLineCount <= 0 || activeHintPreviewPoints.Count <= 1)
            return;

        hintPreviewFlowLine.enabled = true;
        hintPreviewFlowLine.positionCount = 2;
        hintPreviewFlowLine.widthMultiplier = baseWidth * 1.75f;
        UpdateHintPreviewFlow(0f, 1f);
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
            float pulse = 0.9f + 0.1f * Mathf.Sin((elapsed / pulseDuration) * Mathf.PI * 2f);
            SetHintPreviewAlpha(hintPreviewMaxAlpha * pulse);
            UpdateHintPreviewFlow(elapsed, 1f);
            yield return null;
        }
    }

    private void SetHintPreviewAlpha(float alpha)
    {
        Color baseColor = GetHintPreviewColor(Mathf.Clamp01(alpha * hintPreviewBaseAlphaMultiplier), false);
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

        float sweepDuration = Mathf.Max(0.25f, hintPreviewSweepDuration);
        float sweepProgress = Mathf.Repeat(elapsed, sweepDuration) / sweepDuration;
        float segmentProgress = sweepProgress * activeHintPreviewLineCount;
        int segmentIndex = Mathf.Clamp(Mathf.FloorToInt(segmentProgress), 0, activeHintPreviewLineCount - 1);
        float t = Mathf.Clamp01(segmentProgress - segmentIndex);

        Vector3 start = activeHintPreviewPoints[segmentIndex];
        Vector3 end = activeHintPreviewPoints[segmentIndex + 1];
        Vector3 head = Vector3.Lerp(start, end, Mathf.Max(t, 0.06f));

        hintPreviewFlowLine.enabled = true;
        hintPreviewFlowLine.SetPosition(0, start);
        hintPreviewFlowLine.SetPosition(1, head);

        float headPulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 18f);
        Color flowColor = GetHintPreviewColor(hintPreviewMaxAlpha * Mathf.Clamp01(fade) * headPulse, true);
        hintPreviewFlowLine.startColor = flowColor;
        hintPreviewFlowLine.endColor = flowColor;
    }

    private Color GetHintPreviewColor(float alpha, bool flow)
    {
        Color color = flow ? hintPreviewFlowColor : hintPreviewColor;
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
        if (hintPreviewFlowLine != null)
            hintPreviewFlowLine.enabled = false;
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
        private readonly float startTime;
        private string status = "running";

        public int NodeCount { get; private set; }
        public bool IsExhausted { get; private set; }
        public string Status => IsExhausted ? status : "solved";

        public HintSolverBudget(int nodeLimit, float timeLimitSeconds)
        {
            this.nodeLimit = Mathf.Max(1, nodeLimit);
            this.timeLimitSeconds = Mathf.Max(0.01f, timeLimitSeconds);
            startTime = Time.realtimeSinceStartup;
        }

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

            if (Time.realtimeSinceStartup - startTime > timeLimitSeconds)
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
        blackoutQuestionFlipRoutine = StartCoroutine(ShowAllTilesAsQuestionWithAnimation());
    }

    private IEnumerator ShowAllTilesAsQuestionWithAnimation()
    {
        if (tiles == null) yield break;

        float rowDuration = Mathf.Max(0.01f, blackoutFlipDuration);
        float rowDelay = Mathf.Max(0f, blackoutRowInterval);
        float totalDuration = Mathf.Max(rowDuration, (stageHeight - 1) * rowDelay + rowDuration);
        bool[] rowSwitched = new bool[stageHeight];
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            for (int row = stageHeight - 1; row >= 0; row--)
            {
                float rowStart = (stageHeight - 1 - row) * rowDelay;
                float localElapsed = elapsed - rowStart;
                float progress = localElapsed / rowDuration;
                float yAngle = progress < 0f ? 0f : (progress > 1f ? 360f : 360f * progress);

                for (int col = 0; col < stageWidth; col++)
                {
                    Tile tile = tiles[row, col];
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

        SetAllTilesDisplayAsQuestion(true);
        ResetAllTileQuestionRotations();
        blackoutQuestionFlipRoutine = null;
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

    /// <summary>남은 타일 카운트 기준으로 상단 UI ProgressBar 갱신.</summary>
    private void RefreshMainUIProgress()
    {
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI == null || initialTileCountForUI <= 0)
            return;

        int remaining = GetTotalRemainingCount();
        mainUI.UpdateProgress(remaining);
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
                stageCleared = true;
                ClearHintPreview();
                Debug.Log("Clear");
                PlayClearSfx();
                PlayStageClearHaptic();
                TrackStageCleared(StageClearTypeLastTileRule);
                StartCoroutine(LoadNextStageAfterDelay());
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
                    stageCleared = true;
                    ClearHintPreview();
                    Debug.Log("Clear");
                    PlayClearSfx();
                    PlayStageClearHaptic();
                    TrackStageCleared(StageClearTypeLastTileRule);
                    StartCoroutine(LoadNextStageAfterDelay());
                    return true;
                }
            }
        }

        return false;
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
        if (neonTrail != null)
            neonTrail.emitting = emitting;
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
            Debug.Log($"[실패 판정 지연] begin source={source} pending={pendingBoardMutationCount}");
    }

    public void CompletePendingBoardMutation(string source, Tile clearReferenceTile = null, bool requestFailureCheck = true)
    {
        if (pendingBoardMutationCount > 0)
            pendingBoardMutationCount--;

        if (ShouldLogVerboseStage6Debug())
            Debug.Log($"[실패 판정 지연] end source={source} pending={pendingBoardMutationCount}");

        if (clearReferenceTile != null)
            pendingBoardFailureClearReferenceTile = clearReferenceTile;

        if (pendingBoardMutationCount <= 0 && (requestFailureCheck || pendingBoardFailureRecheck))
            ScheduleDeferredBoardFailureCheck(pendingBoardFailureClearReferenceTile);
    }

    private void ResetBoardFailureTracking()
    {
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
        ShowBoardFailureSnackbar(result.Reason);
        Debug.Log($"Game Over: {reason} focus={DescribeTileForDebug(result.FocusTile)}");
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
                return false;
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

    private bool CheckAndHandleMissedFixedKnot(int totalPathCount)
    {
        if (stageCleared || isGameOverSequencePlaying || tiles == null)
            return false;

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
                Debug.Log($"Game Over: FixedKnot missed at ({tile.X},{tile.Y}) targetOrder={fixedKnot.TargetOrder} totalPathCount={totalPathCount}");
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

        pendingRestartGameOverSnackbarKey = localizationKey;
        pendingRestartGameOverSnackbarDuration = Mathf.Max(0.1f, durationSeconds);

        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        if (mainUI == null)
            return;

        mainUI.ShowGameplaySnackbar(localizationKey, pendingRestartGameOverSnackbarDuration);
    }

    private void ReplayPendingGameOverSnackbarAfterRestart()
    {
        string localizationKey = pendingRestartGameOverSnackbarKey;
        float durationSeconds = pendingRestartGameOverSnackbarDuration;
        pendingRestartGameOverSnackbarKey = null;
        pendingRestartGameOverSnackbarDuration = 0f;

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

        Debug.Log("Game Over");
        TrackStageFailed(reason);
        if (pathLitClearRoutine != null)
        {
            StopCoroutine(pathLitClearRoutine);
            pathLitClearRoutine = null;
        }

        gameOverFocusTile = failFocusTile;
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
            // 일반: 글리치 → 암전
            for (int row = 0; row < stageHeight; row++)
                for (int col = 0; col < stageWidth; col++)
                    if (tiles[row, col] != null)
                        tiles[row, col].SetGlitchColor(Color.black);
            yield return new WaitForSeconds(blinkInterval);
            for (int row = 0; row < stageHeight; row++)
                for (int col = 0; col < stageWidth; col++)
                    if (tiles[row, col] != null)
                        tiles[row, col].RestoreNeonColor();
            yield return new WaitForSeconds(blinkInterval);
            for (int row = 0; row < stageHeight; row++)
                for (int col = 0; col < stageWidth; col++)
                    if (tiles[row, col] != null)
                        tiles[row, col].SetGlitchColor(Color.black);
            yield return new WaitForSeconds(blinkInterval);
            for (int row = 0; row < stageHeight; row++)
                for (int col = 0; col < stageWidth; col++)
                    if (tiles[row, col] != null)
                        tiles[row, col].RestoreNeonColor();
            yield return new WaitForSeconds(blinkInterval);

            for (int row = 0; row < stageHeight; row++)
                for (int col = 0; col < stageWidth; col++)
                    if (tiles[row, col] != null)
                        tiles[row, col].SetBlackout(true);

            yield return new WaitForSeconds(blackoutWait);
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

        for (int row = stageHeight - 1; row >= 0; row--)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                if (tiles[row, col] == null) continue;
                var hidden = tiles[row, col].GetComponent<HiddenTile>();
                if (hidden != null) continue;
                yield return new WaitForSeconds(tileAppearInterval);
                Tile tile = tiles[row, col];
                tile.PlayBounceAppearance();
                RefreshSpecialTileVisualStateAfterAppearance(tile);
            }
        }

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
        ReplayPendingGameOverSnackbarAfterRestart();
        SetupMelodyForCurrentStage();
        PlayNewStageSfx();
        IncrementBoardVersion("auto_restart_after_fail");
        HandleStageStarted("auto_restart_after_fail");

        gameOverFocusTile = null;
        isGameOverSequencePlaying = false;
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

        var crossBlast = tile.GetComponent<CrossBlastTile>();
        if (crossBlast != null)
            crossBlast.ResetTransientVisualState();

        var twinLink = tile.GetComponent<TwinLinkTile>();
        if (twinLink != null)
            twinLink.ResetTransientVisualState();

        tile.ResetToInitial();

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

    private IEnumerator ResetCurrentStageRoutine()
    {
        ResetBoardFailureTracking();
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

        for (int row = stageHeight - 1; row >= 0; row--)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                if (tiles[row, col] == null) continue;
                var hidden = tiles[row, col].GetComponent<HiddenTile>();
                if (hidden != null) continue;
                yield return new WaitForSeconds(tileAppearInterval);
                Tile tile = tiles[row, col];
                tile.PlayBounceAppearance();
                RefreshSpecialTileVisualStateAfterAppearance(tile);
            }
        }

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
        for (int row = 0; row < stageHeight; row++)
            for (int col = 0; col < stageWidth; col++)
                if (tiles[row, col] != null && tiles[row, col].IsActive)
                    return;
        stageCleared = true;
        Debug.Log("Clear");
        ClearHintPreview();
        PlayClearSfx();
        PlayStageClearHaptic();
        TrackStageCleared(StageClearTypeAllTilesZero);
        StartCoroutine(LoadNextStageAfterDelay());
    }

    private IEnumerator LoadNextStageAfterDelay()
    {
        yield return new WaitForSeconds(nextStageDelay);
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

                if (TryAdvanceToNextStage())
                    HandleStageStarted("auto_next_stage");
            });
        }
        else
        {
            if (TryAdvanceToNextStage())
                HandleStageStarted("auto_next_stage");
        }
    }

    private void PrepareForStageTransition()
    {
        ResetBoardFailureTracking();
        ClearHintPreview();
        if (pathLitClearRoutine != null)
        {
            StopCoroutine(pathLitClearRoutine);
            pathLitClearRoutine = null;
        }

        ResetTrail();
        ClearPendingBlockNoteQueue();
    }

    private bool TryAdvanceToNextStage()
    {
        currentStageIndex++;
        StageData data = StageManager.LoadStage(currentStageIndex);
        if (data == null)
        {
            currentStageIndex = 1;
            data = StageManager.LoadStage(1);
            if (data == null)
            {
                stageCleared = false;
                return false;
            }
        }
        UpdateVerboseStage6DebugState(data);

        totalStepsCommitted = 0;
        ClearTiles();
        CreateGridFromStageData(data);
        SetCurrentStartTileFromStageData(data);
        SetupSpotlight(data);
        AdjustCameraToFitGrid();
        stageCleared = false;

        RefreshMainUIForStage();
        ResetMainUIHeartsForNewStage();
        SetupMelodyForCurrentStage();
        PlayNewStageSfx();
        SaveStageProgress();
        PrewarmUpcomingStages();
        IncrementBoardVersion("stage_loaded");
        return true;
    }

    private void PrewarmUpcomingStages()
    {
        const int warmupCount = 6;
        int warmed = StageManager.PrewarmStages(currentStageIndex, warmupCount);
        if (warmed > 0)
            Debug.Log($"[GameManager] Stage prewarm complete. start={currentStageIndex}, warmed={warmed}");
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
    private int LoadSavedStageIndex()
    {
        try
        {
            if (ES3.KeyExists(SaveKeyStage))
                return ES3.Load<int>(SaveKeyStage);
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

    private void ConfigureDeviceMaxFrameRate()
    {
        if (!useDeviceMaxFps)
            return;

        QualitySettings.vSyncCount = 0;
        int maxFps = GetDeviceMaxFrameRate();
        Application.targetFrameRate = maxFps;
        Time.maximumDeltaTime = 0.1f;
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
        linkSystem?.ClearLinks();
        hiddenGroups.Clear();
        twinLinkGroups.Clear();
        if (tiles != null)
        {
            for (int row = 0; row < stageHeight; row++)
                for (int col = 0; col < stageWidth; col++)
                    if (tiles[row, col] != null)
                    {
                        Destroy(tiles[row, col].gameObject);
                        tiles[row, col] = null;
                    }
        }
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponent<Tile>() != null)
                Destroy(child.gameObject);
        }
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
        Color[] baseColors = (neonGradientColors != null && neonGradientColors.Length >= 4) ? neonGradientColors : fallbackNeonColors;
        int n = baseColors.Length;
        EnsureTrailGradientBufferSize(n + 1);

        // HDR 강도 적용 (Bloom용)
        float intensity = trailHdrIntensity;
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
            camData.renderPostProcessing = true;
            camData.allowHDROutput = true;
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

        float sizeByHeight = (gridHeight * 0.5f * fitMargin) / centerBandHeight;
        float sizeByWidth = (gridWidth * 0.5f) / safeAspect * fitMargin;
        float fitSize = Mathf.Max(sizeByHeight, sizeByWidth);
        return fitSize * screenEdgePadding;
    }

    private void GetGameplayMarginsNormalized(out float top, out float bottom)
    {
        top = Mathf.Clamp01(uiTopMarginNormalized);
        bottom = Mathf.Clamp01(uiBottomMarginNormalized);

        if (mainUI != null && mainUI.HasResolvedGameplayLayout)
        {
            top = Mathf.Max(top, mainUI.TopGameplayMarginNormalized);
            bottom = Mathf.Max(bottom, mainUI.BottomGameplayMarginNormalized + uiBottomGameplayGapNormalized);
        }

        top = Mathf.Clamp01(top);
        bottom = Mathf.Clamp01(bottom);
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

    private void HandleGameplayLayoutChanged()
    {
        if (!isActiveAndEnabled || mainCamera == null || tiles == null)
            return;

        AdjustCameraToFitGrid();
        SyncBackgroundGridReferenceCameraSize();
    }

    private void OnDestroy()
    {
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

            Tile tile = tileObj.GetComponent<Tile>();
            if (tile != null)
            {
                tile.SetGridPosition(cell.x, cell.y);
                tile.SetInitialNumber(cell.count);
                if (data.startPoint.x == cell.x && data.startPoint.y == cell.y)
                    tile.SetAsStartPoint(true);
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
                    var fixedKnot = tileObj.AddComponent<FixedKnotTile>();
                    bool isAbsolute = cell.properties != null && cell.properties.isAbsolute;
                    fixedKnot.Setup(cell.targetOrder > 0 ? cell.targetOrder : 1, isAbsolute);
                }
                if (cell.type == "TwinLink")
                {
                    var twinLink = tileObj.AddComponent<TwinLinkTile>();
                    int id = cell.linkID != 0 ? cell.linkID : 101;
                    Color assignedColor = GetOrAssignTwinLinkColor(id);
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
                    });
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
                tile.SetNumber(cell.count);
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

        Debug.Log($"[Stage6 로드 요약] stageIndex={currentStageIndex} stageID={data.stageID} size={data.width}x{data.height} start=({data.startPoint.x},{data.startPoint.y}) igniter={igniterCount} hidden={hiddenCount} blackout={blackoutCount} blindCurtain={blindCurtainCount} shortCircuit={shortCircuitCount}");
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
        if (tile.GetComponent<FixedKnotTile>() != null) tags.Add("FixedKnot");
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
        AssignClipIfMissing(ref blockAClip, "Assets/Sounds/block_a.wav");
        AssignClipIfMissing(ref blockAsClip, "Assets/Sounds/block_as.wav");
        AssignClipIfMissing(ref blockBClip, "Assets/Sounds/block_b.wav");
        AssignClipIfMissing(ref blockCClip, "Assets/Sounds/block_c.wav");
        AssignClipIfMissing(ref blockCoClip, "Assets/Sounds/block_co.wav");
        AssignClipIfMissing(ref blockCsClip, "Assets/Sounds/block_cs.wav");
        AssignClipIfMissing(ref blockDClip, "Assets/Sounds/block_d.wav");
        AssignClipIfMissing(ref blockDsClip, "Assets/Sounds/block_ds.wav");
        AssignClipIfMissing(ref blockEClip, "Assets/Sounds/block_e.wav");
        AssignClipIfMissing(ref blockFClip, "Assets/Sounds/block_f.wav");
        AssignClipIfMissing(ref blockFsClip, "Assets/Sounds/block_fs.wav");
        AssignClipIfMissing(ref blockGClip, "Assets/Sounds/block_g.wav");
        AssignClipIfMissing(ref blockGsClip, "Assets/Sounds/block_gs.wav");
        AssignClipIfMissing(ref failClip, "Assets/Sounds/fail.wav");
        AssignClipIfMissing(ref newStageClip, "Assets/Sounds/new_stage.wav");
        AssignClipIfMissing(ref clearClip, "Assets/Sounds/clear.wav");
    }

    private static void AssignClipIfMissing(ref AudioClip target, string assetPath)
    {
        if (target != null)
            return;
        target = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
    }
#endif
}
