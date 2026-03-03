using System;
using System.Collections;
using System.Collections.Generic;
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

    [Header("네온 트레일 (손가락 궤적)")]
    [SerializeField] private Color trailColor = new Color(1f, 0.4f, 1f, 1f);
    [Tooltip("트레일 잔상 유지 시간(초). 손 뗀 후 이 시간만큼 남았다가 사라짐")]
    [SerializeField] private float trailTime = 0.5f;
    [Tooltip("트레일 꼭지점 최소 간격. 작을수록 부드러운 선")]
    [SerializeField] private float trailMinVertexDistance = 0.1f;
    [Tooltip("손 뗀 후 경로(링크) 점등 해제까지 대기 시간(초)")]
    [SerializeField] private float pathLitClearDelay = 1f;

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
    private const float SessionFreeHeartRefillFirstThresholdSeconds = 10f * 60f;
    private const float SessionFreeHeartRefillSecondThresholdSeconds = 20f * 60f;

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
    /// <summary>Hidden 타일: groupID별 그룹 (Igniter 트리거 시 활성화용).</summary>
    private Dictionary<string, List<HiddenTile>> hiddenGroups = new Dictionary<string, List<HiddenTile>>();

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

    [Header("UI Toolkit 상단 UI")]
    [Tooltip("GameMainUI.uxml을 사용하는 UIDocument가 있는 오브젝트에 붙은 컨트롤러")]
    [SerializeField] private GameMainUIController mainUI;
    /// <summary>각 스테이지 시작 시 전체 타일 카운트(합). 진행도 계산용.</summary>
    private int initialTileCountForUI;
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

        // 저장된 진행도가 있으면 해당 스테이지부터, 없으면 startStageIndex부터
        currentStageIndex = LoadSavedStageIndex();
        totalStepsCommitted = 0;
        StageData data = StageManager.LoadStage(currentStageIndex);
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
        NotifySplashStageBootstrapCompleted();
        TrackStageStarted("app_launch");
        ConfigureDeviceMaxFrameRate();
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
    public void LoadNextStageImmediate()
    {
        if (isGameOverSequencePlaying)
            return;

        int skippedStageIndex = currentStageIndex;
        PrepareForStageTransition();
        if (!TryAdvanceToNextStage())
            return;

        FirebaseBootstrap.LogEvent("stage_skip", new Dictionary<string, object>
        {
            { "from_stage_index", skippedStageIndex },
            { "to_stage_index", currentStageIndex }
        });
        TrackStageStarted("manual_skip");
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
        currentPath.Clear();
        isDragging = false;
        stageCleared = false;
        totalStepsCommitted = 0;
        currentStageIndex = 1;

        StageData data = StageManager.LoadStage(1);
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
        FirebaseBootstrap.LogEvent("progress_reset", new Dictionary<string, object>
        {
            { "from_stage_index", previousStageIndex },
            { "to_stage_index", currentStageIndex }
        });
        TrackStageStarted("progress_reset");
    }

    private void Update()
    {
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        bool overlayOpen = mainUI != null && (mainUI.IsSettingPopupOpen || mainUI.IsTutorialPopupOpen || mainUI.IsWaitingForHeartRefill || mainUI.IsSplashActive);
        IsPerformanceOverlayOpen = overlayOpen;

        ProcessPendingBlockNoteQueue();
        UpdateSessionHeartRefillProgress(overlayOpen);
        if (stageCleared || isGameOverSequencePlaying)
            return;

        if (overlayOpen)
        {
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

        sparseMelodyPool.Add(new[] { "c", "e", "g", "co", "g", "e", "c", "e" });
        sparseMelodyPool.Add(new[] { "a", "c", "e", "co", "e", "c", "a", "c" });
        sparseMelodyPool.Add(new[] { "d", "fs", "a", "co", "a", "fs", "d", "a" });
        sparseMelodyPool.Add(new[] { "g", "b", "d", "co", "d", "b", "g", "d" });
        sparseMelodyPool.Add(new[] { "c", "d", "e", "g", "a", "g", "e", "d" });

        mediumMelodyPool.Add(new[] { "e", "g", "a", "b", "a", "g", "e", "d", "c", "d" });
        mediumMelodyPool.Add(new[] { "c", "e", "g", "b", "g", "e", "d", "c", "d", "e" });
        mediumMelodyPool.Add(new[] { "a", "b", "co", "b", "a", "g", "e", "d", "e", "g" });
        mediumMelodyPool.Add(new[] { "d", "e", "fs", "a", "fs", "e", "d", "c", "d", "e" });
        mediumMelodyPool.Add(new[] { "f", "a", "as", "co", "as", "a", "f", "d", "f", "a" });

        denseMelodyPool.Add(new[] { "d", "a", "b", "fs", "g", "d", "g", "a" }); // Canon motif
        denseMelodyPool.Add(new[] { "fs", "d", "g", "a", "b", "g", "a", "fs" }); // Canon variation
        denseMelodyPool.Add(new[] { "e", "e", "fs", "g", "g", "fs", "e", "d", "c", "d", "e", "fs", "g", "a" }); // Vivaldi-like
        denseMelodyPool.Add(new[] { "a", "a", "g", "fs", "e", "fs", "g", "a", "co", "b", "a", "g", "fs", "e", "d" }); // Four seasons-like
        denseMelodyPool.Add(new[] { "e", "ds", "e", "ds", "e", "b", "d", "c", "a", "c", "e", "a" }); // Fur Elise-like
        denseMelodyPool.Add(new[] { "e", "e", "f", "g", "g", "f", "e", "d", "c", "c", "d", "e", "d", "c", "c" }); // Ode to Joy-like
        denseMelodyPool.Add(new[] { "cs", "e", "gs", "cs", "e", "gs", "b", "gs", "e", "cs", "e", "gs" }); // Moonlight-like
        denseMelodyPool.Add(new[] { "b", "a", "gs", "a", "c", "b", "a", "g", "fs", "e", "fs", "g", "a" }); // Turkish-like
        denseMelodyPool.Add(new[] { "g", "a", "b", "c", "d", "e", "fs", "g", "a", "b", "co", "b", "a", "g" }); // Minuet-like
        denseMelodyPool.Add(new[] { "d", "fs", "g", "a", "d", "a", "b", "fs", "g", "d", "g", "a" }); // Canon cadence
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
        activeMelody = selectedPool[seed % selectedPool.Count];
        activeMelodyIndex = 0;
        ClearPendingBlockNoteQueue();
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
            QueueNextMelodyBlockNote();
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
            if (hit != currentStartTile || hit == null || !hit.IsActive)
                return;
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
            // 파괴된 타일 참조 제거 (Igniter 소멸 등으로 타일이 비활성화/제거된 경우)
            currentPath.RemoveAll(t => t == null);
            if (currentPath.Count == 0)
            {
                isDragging = false;
                return;
            }
            Tile lastForHit = currentPath[currentPath.Count - 1];
            Tile hit = GetTileAtScreen(screenPoint, preferAdjacentTo: lastForHit);
            // 숫자가 남아 있으면 이미 라인이 그려진 타일이라도 재방문(중복 밟기) 허용.
            // 숫자는 '들어갈 때'가 아니라 '지나쳐 나갈 때' 감소 → 멈춘 타일이 0이 되어 다음 드래그를 못 시작하는 문제 방지.
            if (hit != null && hit.IsActive)
            {
                Tile last = currentPath[currentPath.Count - 1];
                if (last == null)
                {
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
                    if (frameDelta >= 0 && frameDelta <= 3)
                    {
                        Debug.Log($"[지터 무시] 되돌아감 last=({last.X},{last.Y}) hit=({hit.X},{hit.Y}) frameDelta={frameDelta} (직선 드래그 오락가락 방지)");
                        canStep = false;
                    }
                }
                // Igniter 포함 이후에는 해당 지점 이전으로 백트래킹(되돌리기) 차단
                if (canStep && currentPath.Contains(hit))
                {
                    int igniterIdx = -1;
                    for (int i = 0; i < currentPath.Count; i++)
                    {
                        Tile pt = currentPath[i];
                        if (pt == null) continue;
                        if (pt.GetComponent<IgniterTile>() != null) { igniterIdx = i; break; }
                    }
                    if (igniterIdx >= 0 && currentPath.IndexOf(hit) < igniterIdx)
                        canStep = false;
                }
                if (canStep)
                {
                int nextStepNumber = GetTotalPathCount() + 1;
                var fixedKnotHit = hit.GetComponent<FixedKnotTile>();
                var shortCircuitLast = last.GetComponent<ShortCircuitTile>();
                var shortCircuitHit = hit.GetComponent<ShortCircuitTile>();

                // FixedKnot: targetOrder 번째 스텝에만 진입 가능. 잘못된 순서면 경로에 넣지 않고, isAbsolute면 암전·재시작
                bool fixedKnotWrongOrder = fixedKnotHit != null && IsAdjacent(last, hit) && !fixedKnotHit.CanEnter(nextStepNumber);
                if (fixedKnotWrongOrder)
                {
                    fixedKnotHit.PlayWrongOrderShake();
                    if (fixedKnotHit.IsAbsolute)
                    {
                        isDragging = false;
                        currentPath.Clear();
                        SetNeonTrailEmitting(false);
                        linkSystem?.ClearPathLit();
                        isGameOverSequencePlaying = true;
                        StartCoroutine(GameOverAndResetSequence());
                    }
                }
                else if (fixedKnotHit != null && IsAdjacent(last, hit))
                {
                    // FixedKnot 정확한 순서로만 진입 허용 (기어는 다음 타일 밟을 때 사라짐)
                    OnLeaveTileForNext(last, hit);
                    NotifyTwinLinkStepped(last, hit);
                    NotifyFixedKnotLeft(last);
                    var crossBlast = last.GetComponent<CrossBlastTile>();
                    if (crossBlast != null) crossBlast.TriggerExplosion(this, hit);
                    var blackout = last.GetComponent<BlackoutTile>();
                    if (blackout != null) blackout.OnStepped();
                    currentPath.Add(hit);
                    lastStepFrame = Time.frameCount;
                    Debug.Log($"[스텝] FixedKnot last=({last.X},{last.Y})→hit=({hit.X},{hit.Y}) pathLen={currentPath.Count}");
                    LogSteppedOn(hit);
                    TryTriggerIgniter(hit);
                    NotifyTrailTileStepped(hit);
                    int cFk = GetTotalPathCount();
                    NotifyFixedKnotsUpdateVisual(cFk);
                    fixedKnotHit.OnSteppedCorrectly();
                    TryTriggerBlindCurtain(hit);
                    CheckVictoryCondition(hit);
                }
                else if (shortCircuitLast != null)
                {
                    // ShortCircuit 위: 화살표 방향(출구) 셀로만 이동 가능
                    if (!IsAdjacent(last, hit)) { /* 다른 타일 아님 */ }
                    else if (!shortCircuitLast.IsExitCell(hit.X, hit.Y))
                    {
                        // 방향 위반 — 이동 불가, 경로에 추가하지 않음
                    }
                    else
                    {
                        // FixedKnot이면 반드시 올바른 순서에서만 추가 (다른 분기에서 실수로 추가 방지)
                        if (fixedKnotHit != null && !fixedKnotHit.CanEnter(nextStepNumber)) { /* 진입 불가 */ }
                        else
                        {
                            currentPath.Add(hit);
                            Debug.Log($"[스텝] ShortCircuit(위) last=({last.X},{last.Y})→hit=({hit.X},{hit.Y}) pathLen={currentPath.Count}");
                            LogSteppedOn(hit);
                            TryTriggerIgniter(hit);
                            NotifyTrailTileStepped(hit);
                            int totalPathCount = GetTotalPathCount();
                            if (fixedKnotHit == null && CheckFixedKnotMissed(totalPathCount))
                            {
                                currentPath.RemoveAt(currentPath.Count - 1);
                                isDragging = false;
                                currentPath.Clear();
                                UpdateNeonTrailPosition();
                                linkSystem?.ClearPathLit();
                                isGameOverSequencePlaying = true;
                                StartCoroutine(GameOverAndResetSequence());
                            }
                            else
                            {
                                lastStepFrame = Time.frameCount;
                                OnLeaveTileForNext(last, hit);
                                NotifyTwinLinkStepped(last, hit);
                                NotifyFixedKnotLeft(last);
                                NotifyFixedKnotsUpdateVisual(totalPathCount);
                                if (fixedKnotHit != null) fixedKnotHit.OnSteppedCorrectly();
                                TryTriggerBlindCurtain(hit);
                                CheckVictoryCondition(hit);
                            }
                        }
                    }
                }
                else if (shortCircuitHit != null)
                {
                    // ShortCircuit으로 들어감: 인접하면 어느 방향에서든 진입 가능 (제한은 나갈 때만). FixedKnot이면 올바른 순서에서만 추가
                    if (IsAdjacent(last, hit) && (fixedKnotHit == null || fixedKnotHit.CanEnter(nextStepNumber)))
                    {
                        currentPath.Add(hit);
                        Debug.Log($"[스텝] ShortCircuit(진입) last=({last.X},{last.Y})→hit=({hit.X},{hit.Y}) pathLen={currentPath.Count}");
                        LogSteppedOn(hit);
                        TryTriggerIgniter(hit);
                        NotifyTrailTileStepped(hit);
                        int totalPathCountSc = GetTotalPathCount();
                        if (fixedKnotHit == null && CheckFixedKnotMissed(totalPathCountSc))
                        {
                            currentPath.RemoveAt(currentPath.Count - 1);
                            isDragging = false;
                            currentPath.Clear();
                            SetNeonTrailEmitting(false);
                            linkSystem?.ClearPathLit();
                            isGameOverSequencePlaying = true;
                            StartCoroutine(GameOverAndResetSequence());
                        }
                        else
                        {
                            lastStepFrame = Time.frameCount;
                            OnLeaveTileForNext(last, hit);
                            NotifyTwinLinkStepped(last, hit);
                            NotifyFixedKnotLeft(last);
                            var crossBlast = last.GetComponent<CrossBlastTile>();
                            if (crossBlast != null)
                                crossBlast.TriggerExplosion(this, hit);
                            var blackout = last.GetComponent<BlackoutTile>();
                            if (blackout != null)
                                blackout.OnStepped();
                            NotifyFixedKnotsUpdateVisual(totalPathCountSc);
                            if (fixedKnotHit != null) fixedKnotHit.OnSteppedCorrectly();
                            TryTriggerBlindCurtain(hit);
                            CheckVictoryCondition(hit);
                        }
                    }
                }
                else
                {
                    // 일반 타일 이동 (FixedKnot이면 올바른 순서에서만 추가)
                    if (IsAdjacent(last, hit) && (fixedKnotHit == null || fixedKnotHit.CanEnter(nextStepNumber)))
                    {
                        currentPath.Add(hit);
                        Debug.Log($"[스텝] 일반 last=({last.X},{last.Y})→hit=({hit.X},{hit.Y}) pathLen={currentPath.Count}");
                        LogSteppedOn(hit);
                        TryTriggerIgniter(hit);
                        NotifyTrailTileStepped(hit);
                        int totalPathCountEl = GetTotalPathCount();
                        if (fixedKnotHit == null && CheckFixedKnotMissed(totalPathCountEl))
                        {
                            currentPath.RemoveAt(currentPath.Count - 1);
                            isDragging = false;
                            currentPath.Clear();
                            SetNeonTrailEmitting(false);
                            linkSystem?.ClearPathLit();
                            isGameOverSequencePlaying = true;
                            StartCoroutine(GameOverAndResetSequence());
                        }
                        else
                        {
                            lastStepFrame = Time.frameCount;
                            OnLeaveTileForNext(last, hit); // 떠나는 타일: Igniter면 소멸 연출, 아니면 숫자 감소
                            NotifyTwinLinkStepped(last, hit);
                            NotifyFixedKnotLeft(last);
                            var crossBlast = last.GetComponent<CrossBlastTile>();
                            if (crossBlast != null)
                                crossBlast.TriggerExplosion(this, hit); // hit = 다음 타일(밟고 이동한 타일) → 효과 제외
                            var blackout = last.GetComponent<BlackoutTile>();
                            if (blackout != null)
                                blackout.OnStepped(); // Blackout 타일 밟을 때 Punch Scale·탁해짐 피드백
                            NotifyFixedKnotsUpdateVisual(totalPathCountEl);
                            if (fixedKnotHit != null) fixedKnotHit.OnSteppedCorrectly();
                            TryTriggerBlindCurtain(hit);
                            CheckVictoryCondition(hit);
                        }
                    }
                }
                }
            }
        }

        // 손 뗄 때는 항상 커밋(터치만 해도 하트비트·라인 갱신). 타일 추가는 pointerHeld일 때만 해서 터치만 할 때 DecreaseNumber 방지.
        if (pointerUp)
        {
            isDragging = false;
            ResetTrail();
            CommitPathAndSetCurrentPosition();
            // 클리어를 먼저 검사. 모든 타일 0이면 데드락 검사와 겹치므로 클리어 우선 (그렇지 않으면 게임오버로 잘못 처리됨)
            CheckStageClear();
            if (!stageCleared && CheckAndHandleDeadlock())
            { /* 데드락이면 GameOver 연출은 CheckAndHandleDeadlock 내부에서 시작 */ }
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

    /// <summary>드래그 중 인접 타일 검색 시 포인터 주변 반경(월드). count 1인 ShortCircuit 등이 놓치지 않도록.</summary>
    private const float TilePickRadius = 0.45f;

    /// <summary>화면 좌표 아래 타일 반환. 드래그 중에는 작은 반경(OverlapCircle)으로 검사해 인접 타일 중 포인터에 가장 가까운 것 반환.</summary>
    private Tile GetTileAtScreen(Vector2 screenPoint, Tile preferAdjacentTo = null)
    {
        if (mainCamera == null) return null;
        float camZ = mainCamera.transform.position.z;
        Vector3 world3 = mainCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, Mathf.Abs(camZ)));
        Vector2 worldPoint = new Vector2(world3.x, world3.y);
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
        if (tiles == null) return;
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };
        for (int i = 0; i < 4; i++)
        {
            int nx = centerX + dx[i];
            int ny = centerY + dy[i];
            if (nx == excludeX && ny == excludeY) continue; // 다음 타일은 CrossBlast 효과 제외
            if (ny >= 0 && ny < stageHeight && nx >= 0 && nx < stageWidth)
            {
                Tile t = tiles[ny, nx];
                if (t != null && t.IsActive)
                {
                    DecreaseTileAndPlayBlockNote(t);
                    NotifyTwinLinkStepped(t);
                }
            }
        }

        // CrossBlast로 인한 주변 타일 감소 후 진행도 갱신
        RefreshMainUIProgress();
    }

    /// <summary>
    /// BlindCurtain 타일을 밟으면 즉시 모든 타일 숫자를 ?로 표시.
    /// </summary>
    private void TryTriggerBlindCurtain(Tile steppedTile)
    {
        if (steppedTile == null || steppedTile.GetComponent<BlindCurtainTile>() == null) return;
        StartCoroutine(HideAllTilesNumbersWithAnimation());
    }

    [Header("BlindCurtain 물음표 전환 연출")]
    [Tooltip("한 줄당 Y축 한 바퀴 회전 시간(초). 50% 지점에서 ?로 전환")]
    [SerializeField] private float blindCurtainFlipDuration = 0.35f;
    [Tooltip("윗줄→아랫줄 순서로 줄 간격(초)")]
    [SerializeField] private float blindCurtainRowInterval = 0.05f;

    /// <summary>윗줄부터 아랫줄까지 0.05초 간격으로 Y축 한 바퀴 회전, 50% 지점에서 ?로 전환.</summary>
    private IEnumerator HideAllTilesNumbersWithAnimation()
    {
        if (tiles == null) yield break;
        float rowDuration = Mathf.Max(0.01f, blindCurtainFlipDuration);
        float rowDelay = Mathf.Max(0f, blindCurtainRowInterval);
        float totalDuration = (stageHeight - 1) * rowDelay + rowDuration;
        bool[] rowSwitched = new bool[stageHeight];
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            for (int r = stageHeight - 1; r >= 0; r--)
            {
                float rowStart = (stageHeight - 1 - r) * rowDelay;
                float localElapsed = elapsed - rowStart;
                float progress = localElapsed / rowDuration;

                float yAngle = progress < 0f ? 0f : (progress > 1f ? 360f : 360f * progress);

                for (int col = 0; col < stageWidth; col++)
                {
                    Tile t = tiles[r, col];
                    if (t == null) continue;
                    var numberText = t.GetNumberText();
                    if (numberText != null)
                        numberText.transform.localEulerAngles = new Vector3(0f, yAngle, 0f);
                    if (progress >= 0.5f && !rowSwitched[r])
                    {
                        rowSwitched[r] = true;
                        t.SetDisplayAsQuestion(true);
                    }
                }
            }

            yield return null;
        }

        for (int row = 0; row < stageHeight; row++)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                Tile t = tiles[row, col];
                if (t == null) continue;
                t.SetDisplayAsQuestion(true);
                var numberText = t.GetNumberText();
                if (numberText != null)
                    numberText.transform.localEulerAngles = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// 상하좌우 인접 여부 (대각선 불가).
    /// </summary>
    /// <summary>타일을 떠날 때(last) 그 타일이 FixedKnot이면 기어 사라짐 연출.</summary>
    private void NotifyFixedKnotLeft(Tile lastTile)
    {
        if (lastTile == null) return;
        var fk = lastTile.GetComponent<FixedKnotTile>();
        if (fk != null) fk.OnLeftByPlayer();
    }

    /// <summary>TwinLink 타일이 밟린 직후: 짝꿍 count 동기화·전기 테두리 번쩍임·DOShakePosition. excludeFromSync=지금 밟은 타일이면 동기화 제외(한 번에 -2 방지).</summary>
    private void NotifyTwinLinkStepped(Tile tile, Tile excludeFromSync = null)
    {
        if (tile == null) return;
        var twin = tile.GetComponent<TwinLinkTile>();
        if (twin != null) twin.OnSteppedSyncPartners(excludeFromSync);
    }

    /// <summary>타일을 떠날 때: Igniter면 소멸 연출 후 0, 아니면 DecreaseNumber.</summary>
    private void OnLeaveTileForNext(Tile last, Tile next)
    {
        if (last == null) return;
        Debug.Log($"[타일 -1] 직전 타일 ({last.X},{last.Y}) 떠남 → 다음 ({next.X},{next.Y})으로 이동, 직전 타일 -1");
        var igniter = last.GetComponent<IgniterTile>();
        if (igniter != null)
        {
            int beforeIgniter = last.CurrentNumber;
            igniter.OnLeftThenVanish();
            if (beforeIgniter > 0)
                QueueNextMelodyBlockNote();
            Debug.Log($"[타일 사라짐] Igniter ({last.X},{last.Y}) 소멸");
        }
        else
        {
            int before = last.CurrentNumber;
            DecreaseTileAndPlayBlockNote(last);
            Debug.Log($"[타일 -1] ({last.X},{last.Y}) count {before} → {last.CurrentNumber}");
            if (last.CurrentNumber <= 0)
                Debug.Log($"[타일 사라짐] ({last.X},{last.Y}) count 0");
        }

        // 타일 숫자/소멸 변경 직후 진행도 갱신
        RefreshMainUIProgress();
    }

    /// <summary>현재 밟은 타일 로그 (디버그용).</summary>
    private void LogSteppedOn(Tile hit)
    {
        if (hit == null) return;
        Debug.Log($"[현재 밟은 타일] ({hit.X},{hit.Y}) count={hit.CurrentNumber}");
    }

    /// <summary>타일을 밟은 직후: Igniter면 targetID에 해당하는 Hidden 그룹 릴레이 활성화 (Igniter에서 가까운 순).</summary>
    private void TryTriggerIgniter(Tile steppedTile)
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
        igniter.TriggerHiddenTiles(list, igniterPos, relayInterval);
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

    /// <summary>승리 조건: 남은 합이 1이고, 그 1이 현재 밟은 타일(B)의 카운트면 즉시 클리어. 해당 타일 0으로 만든 뒤 승리 연출.</summary>
    private bool CheckVictoryCondition(Tile currentTile)
    {
        if (stageCleared || tiles == null || currentTile == null) return false;
        int totalRemaining = GetTotalRemainingCount();
        if (totalRemaining != 1 || currentTile.CurrentNumber != 1) return false;
        DecreaseTileAndPlayBlockNote(currentTile);
        // 마지막 타일 감소도 진행도에 반영
        RefreshMainUIProgress();
        stageCleared = true;
        Debug.Log("Clear");
        PlayClearSfx();
        PlayStageClearHaptic();
        TrackStageCleared("last_tile_rule");
        StartCoroutine(LoadNextStageAfterDelay());
        return true;
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

    /// <summary>totalPathCount가 targetOrder를 넘어갔는데 아직 밟지 않은 FixedKnot이 있으면 true → 게임오버.</summary>
    private bool CheckFixedKnotMissed(int totalPathCount)
    {
        if (tiles == null) return false;
        for (int row = 0; row < stageHeight; row++)
            for (int col = 0; col < stageWidth; col++)
                if (tiles[row, col] != null)
                {
                    var fk = tiles[row, col].GetComponent<FixedKnotTile>();
                    if (fk != null && fk.IsMissedAtStepCount(totalPathCount))
                        return true;
                }
        return false;
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

    /// <summary>
    /// 데드락(게임오버) 여부: 현재 위치에서 인접(상하좌우) 중 이동 가능(숫자 1 이상) 타일이 하나도 없으면 true.
    /// ShortCircuit: 화살표 방향(출구) 셀만 검사.
    /// </summary>
    private bool IsDeadlock()
    {
        if (currentStartTile == null || tiles == null) return false;
        var shortCircuit = currentStartTile.GetComponent<ShortCircuitTile>();
        if (shortCircuit != null)
        {
            (int ex, int ey) = shortCircuit.ExitCell;
            if (ey >= 0 && ey < stageHeight && ex >= 0 && ex < stageWidth)
            {
                Tile t = tiles[ey, ex];
                if (t != null && t.IsActive)
                    return false;
            }
            return true;
        }
        int x = currentStartTile.X;
        int y = currentStartTile.Y;
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };
        for (int i = 0; i < 4; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];
            if (ny >= 0 && ny < stageHeight && nx >= 0 && nx < stageWidth)
            {
                Tile t = tiles[ny, nx];
                if (t != null && t.IsActive)
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 데드락이면 Game Over 로그 후 글리치·암전·리셋 연출 코루틴 시작. true 반환(Stage Clear 검사 생략).
    /// </summary>
    private bool CheckAndHandleDeadlock()
    {
        if (!IsDeadlock()) return false;

        Debug.Log("Game Over");
        TrackStageFailed("deadlock");
        if (pathLitClearRoutine != null)
        {
            StopCoroutine(pathLitClearRoutine);
            pathLitClearRoutine = null;
        }
        isGameOverSequencePlaying = true;
        StartCoroutine(GameOverAndResetSequence());
        return true;
    }

    /// <summary>
    /// 게임오버: 글리치(0.5초) → 암전 → 대기 → 리셋 → 순차 등장. Spotlight 모드면 실패 지점 Radar Pulse → Vignette → 완전 암흑 리셋.
    /// </summary>
    private IEnumerator GameOverAndResetSequence()
    {
        totalStepsCommitted = 0;
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
            Vector2 failPos = currentStartTile != null ? (Vector2)currentStartTile.transform.position : Vector2.zero;
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
                Tile t = tiles[row, col];
                t.ResetToInitial();
                var hidden = t.GetComponent<HiddenTile>();
                var igniter = t.GetComponent<IgniterTile>();
                if (hidden != null)
                    hidden.ResetToHiddenState();
                else if (igniter != null)
                    igniter.ResetToInitialState();
                if (hidden == null)
                    t.SetScaleZero();
                var fixedKnot = t.GetComponent<FixedKnotTile>();
                if (fixedKnot != null) fixedKnot.ResetGearVisibility();
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
                tiles[row, col].PlayBounceAppearance();
                var igniter = tiles[row, col].GetComponent<IgniterTile>();
                if (igniter != null)
                    igniter.EnsureNumberHidden();
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
        TrackStageStarted("auto_restart_after_fail");

        isGameOverSequencePlaying = false;
    }

    /// <summary>
    /// 현재 스테이지를 초기 상태로 리셋 (ResetButton 등에서 호출). 게임오버 연출 없이 타일 복원 + 순차 등장.
    /// </summary>
    public void ResetCurrentStage()
    {
        if (isGameOverSequencePlaying || tiles == null) return;
        TrackStageReset("manual_reset");
        isGameOverSequencePlaying = true;
        StartCoroutine(ResetCurrentStageRoutine());
    }

    private IEnumerator ResetCurrentStageRoutine()
    {
        totalStepsCommitted = 0;
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
                Tile t = tiles[row, col];
                t.ResetToInitial();
                var hidden = t.GetComponent<HiddenTile>();
                var igniter = t.GetComponent<IgniterTile>();
                if (hidden != null)
                    hidden.ResetToHiddenState();
                else if (igniter != null)
                    igniter.ResetToInitialState();
                if (hidden == null)
                    t.SetScaleZero();
                var fixedKnot = t.GetComponent<FixedKnotTile>();
                if (fixedKnot != null) fixedKnot.ResetGearVisibility();
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
                tiles[row, col].PlayBounceAppearance();
                var igniter = tiles[row, col].GetComponent<IgniterTile>();
                if (igniter != null)
                    igniter.EnsureNumberHidden();
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
                if (spotlightController != null)
                    spotlightController.ResetRevealedToStartOnly(initialStart.transform.position);
            }
        }

        if (linkSystem != null && tiles != null)
            linkSystem.CreateLinksForCrossBlastOnly(tiles, stageWidth, stageHeight);

        RefreshMainUIForStage();
        SetupMelodyForCurrentStage();
        PlayNewStageSfx();
        TrackStageStarted("manual_reset");
        isGameOverSequencePlaying = false;
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
        PlayClearSfx();
        PlayStageClearHaptic();
        TrackStageCleared("all_tiles_zero");
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
                    TrackStageStarted("auto_next_stage");
            });
        }
        else
        {
            if (TryAdvanceToNextStage())
                TrackStageStarted("auto_next_stage");
        }
    }

    private void PrepareForStageTransition()
    {
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
        linkSystem?.ClearLinks();
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

    /// <summary>Multi-Color Neon Trail: 4색+ 그라데이션, HDR 머티리얼(강도 2+), 시작 밝게·끝 투명.</summary>
    private void CreateNeonTrail()
    {
        GameObject trailGo = new GameObject("NeonTrail");
        trailGo.transform.SetParent(transform);
        trailGo.transform.position = new Vector3(0f, 0f, -0.5f);

        neonTrail = trailGo.AddComponent<TrailRenderer>();
        neonTrail.time = trailTime;
        neonTrail.minVertexDistance = trailMinVertexDistance;
        neonTrail.emitting = false;

        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0f, 0.5f);
        widthCurve.AddKey(1f, 0f);
        neonTrail.widthCurve = widthCurve;

        // Bloom 극대화: 그라데이션 색상에 HDR 강도(trailHdrIntensity) 적용. 머티리얼은 Additive로 블룸 노출
        Shader additiveShader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        Material trailMat = new Material(additiveShader);
        if (additiveShader != null && additiveShader.name.Contains("Additive"))
            trailMat.SetColor("_TintColor", Color.white);
        else
            trailMat.color = Color.white;
        neonTrail.material = trailMat;

        // 4색 이상 그라데이션: 시작(손가락)은 가장 밝게, 끝(꼬리)은 Alpha 0. 순환은 UpdateNeonTrailColor에서 처리
        ApplyNeonTrailGradient(0f);

        neonTrail.sortingOrder = 10;
        neonTrailTransform = trailGo.transform;
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
        float aspect = (float)Screen.width / Screen.height;

        // 뷰포트를 자르지 않고 전체 화면을 써서 발광(블룸)이 경계에서 잘리지 않도록 함.
        // 대신 orthographicSize를 키워 그리드가 상·하단 UI 사이 '중앙 밴드'에만 들어가게 함.
        float top = Mathf.Clamp01(uiTopMarginNormalized);
        float bottom = Mathf.Clamp01(uiBottomMarginNormalized);
        float centerBandHeight = Mathf.Clamp(1f - top - bottom, 0.2f, 1f);

        float sizeByHeight = (totalGridHeight * 0.5f * fitMargin) / centerBandHeight;
        float sizeByWidth = (totalGridWidth * 0.5f) / aspect * fitMargin;
        float fitSize = Mathf.Max(sizeByHeight, sizeByWidth);
        mainCamera.orthographicSize = fitSize * screenEdgePadding;
        mainCamera.transform.position = new Vector3(0f, 0f, mainCamera.transform.position.z);
        mainCamera.rect = new Rect(0f, 0f, 1f, 1f);
    }

    /// <summary>
    /// JSON 스테이지 데이터로 그리드 생성. count가 0인 셀은 인스턴스화 건너뛰고, startPoint 타일은 시작점 표시.
    /// </summary>
    private void CreateGridFromStageData(StageData data)
    {
        if (data.cells == null || data.startPoint == null) return;

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
                    tile.SetInitialNumber(1);
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
                    string colorHex = !string.IsNullOrEmpty(cell.color) ? cell.color : "#00FBFF";
                    twinLink.Setup(id, colorHex, twinLinkLightningPrefab, new TwinLinkTile.TwinLinkSettings
                    {
                        borderOffset = twinLinkBorderOffset,
                        boltInterval = twinLinkBoltInterval,
                        chaosFactor = twinLinkChaosFactor,
                        boltGenerations = twinLinkBoltGenerations,
                        boltWidthScale = twinLinkBoltWidthScale,
                        flashDuration = twinLinkFlashDuration,
                        shakeStrength = twinLinkShakeStrength
                    });
                    if (!twinLinkGroups.ContainsKey(id))
                        twinLinkGroups[id] = new List<TwinLinkTile>();
                    twinLinkGroups[id].Add(twinLink);
                }
                if (cell.type == "Hidden")
                {
                    var hidden = tileObj.AddComponent<HiddenTile>();
                    hidden.Setup();
                    string gid = !string.IsNullOrEmpty(cell.groupID) ? cell.groupID : "default";
                    if (!hiddenGroups.ContainsKey(gid))
                        hiddenGroups[gid] = new List<HiddenTile>();
                    hiddenGroups[gid].Add(hidden);
                }
                if (cell.type == "Igniter")
                {
                    tile.SetInitialNumber(1);
                    var igniter = tileObj.AddComponent<IgniterTile>();
                    igniter.Setup(cell.targetID ?? "");
                }
                tile.SetNumber(cell.type == "BlindCurtain" ? 1 : (cell.type == "Igniter" ? 1 : cell.count));
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
                return;
            }
        }
        if (tiles != null && stageHeight > 0 && stageWidth > 0 && tiles[0, 0] != null)
        {
            initialStartTileRow = 0;
            initialStartTileCol = 0;
            currentStartTile = tiles[0, 0];
            currentStartTile.SetInitialStartTile(true);
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
