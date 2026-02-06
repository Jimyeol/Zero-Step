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

/// <summary>
/// 네온 퍼즐 게임 매니저: JSON 스테이지 로드, 그리드 생성(count==0 스킵), 드래그 경로, Line Renderer, Stage Clear 시 다음 스테이지.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("그리드 설정")]
    [SerializeField] private float padding = 0.2f;
    [SerializeField] private float fitMargin = 1.05f;
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

    [Header("라인 렌더러")]
    [SerializeField] private float lineWidth = 0.15f;
    [SerializeField] private Color lineColor = new Color(1f, 0.5f, 1f, 1f);
    [Tooltip("손 뗀 후 그려진 라인이 사라지기까지 대기 시간(초)")]
    [SerializeField] private float lineClearDelay = 1f;

    [Header("스테이지")]
    [SerializeField] private int startStageIndex = 1;
    [SerializeField] private float nextStageDelay = 1.5f;

    [Header("성능 (모바일 FPS)")]
    [Tooltip("목표 FPS. 60 권장, -1이면 디바이스 기본값")]
    [SerializeField] private int targetFrameRate = 60;

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

    private int currentStageIndex;
    private float tileWidth;
    private float tileHeight;
    private float totalGridWidth;
    private float totalGridHeight;
    private int stageWidth;
    private int stageHeight;
    private Tile[,] tiles;
    /// <summary>드래그를 시작할 수 있는 타일(JSON 시작점 → 손 떼면 마지막 도달 타일).</summary>
    private Tile currentStartTile;
    /// <summary>게임오버 리셋 시 시작점 복원용. tiles[row, col] 인덱스.</summary>
    private int initialStartTileRow;
    private int initialStartTileCol;
    private List<Tile> currentPath = new List<Tile>();
    /// <summary>이전 이동 경로 포함 누적 라인 위치(네온 선 유지).</summary>
    private List<Vector3> linePoints = new List<Vector3>();
    /// <summary>지금까지 커밋된 스텝 수. 라인 시각만 지워도 이 값은 유지 → 기어 숫자 이어가기.</summary>
    private int totalStepsCommitted;
    private bool isDragging;
    private LineRenderer lineRenderer;
    private bool stageCleared;
    /// <summary>손 뗀 후 1초 뒤 라인 제거용 코루틴.</summary>
    private Coroutine lineClearRoutine;
    /// <summary>게임오버·리셋 연출 진행 중이면 입력 차단.</summary>
    private bool isGameOverSequencePlaying;
    /// <summary>인접 타일 사이 Link 배치·경로/체인 점등.</summary>
    private LinkSystem linkSystem;
    /// <summary>TwinLink 타일: linkID별 그룹 (그리드 생성 후 파트너 등록용).</summary>
    private Dictionary<int, List<TwinLinkTile>> twinLinkGroups = new Dictionary<int, List<TwinLinkTile>>();
    /// <summary>Hidden 타일: groupID별 그룹 (Igniter 트리거 시 활성화용).</summary>
    private Dictionary<string, List<HiddenTile>> hiddenGroups = new Dictionary<string, List<HiddenTile>>();

    /// <summary>CrossBlastTile·LinkSystem 등에서 그리드 크기 참조용.</summary>
    public int StageWidth => stageWidth;
    public int StageHeight => stageHeight;
    public LinkSystem GetLinkSystem() => linkSystem;
    /// <summary>Spotlight 모드: 현재 드래그 중인지.</summary>
    public bool IsDragging => isDragging;
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

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (tilePrefab == null || mainCamera == null)
        {
            Debug.LogError("[GameManager] Tile 프리팹 또는 Main Camera가 할당되지 않았습니다.");
            return;
        }

        EnsureInputAndRaycaster();
        EnsureCameraPostProcessingAndHDR();
        CreateLineRenderer();
        CacheTileSizeFromPrefab();

        currentStageIndex = startStageIndex;
        linePoints.Clear();
        totalStepsCommitted = 0;
        StageData data = StageManager.LoadStage(currentStageIndex);
        if (data != null)
            CreateGridFromStageData(data);
        else
            CreateGridFallback();
        SetCurrentStartTileFromStageData(data);
        SetupSpotlight(data);
        AdjustCameraToFitGrid();

        if (targetFrameRate > 0)
            Application.targetFrameRate = targetFrameRate;
    }

    /// <summary>
    /// 다음 스테이지로 전환. 다음이 없으면 1스테이지로 반복.
    /// </summary>
    public void LoadNextStageImmediate()
    {
        if (isGameOverSequencePlaying)
            return;
        if (lineClearRoutine != null)
        {
            StopCoroutine(lineClearRoutine);
            lineClearRoutine = null;
        }
        currentStageIndex++;
        StageData data = StageManager.LoadStage(currentStageIndex);
        if (data == null)
        {
            currentStageIndex = 1;
            data = StageManager.LoadStage(1);
            if (data == null)
                return;
        }
        linePoints.Clear();
        totalStepsCommitted = 0;
        UpdateLineRendererPositions();
        ClearTiles();
        CreateGridFromStageData(data);
        SetCurrentStartTileFromStageData(data);
        SetupSpotlight(data);
        AdjustCameraToFitGrid();
        stageCleared = false;
    }

    private void Update()
    {
        if (stageCleared || isGameOverSequencePlaying)
            return;

        UpdateDragAndPath();
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
            // 시작점 터치만으로는 기어 숫자 갱신 안 함 — 타일을 실제로 옮겼을 때만 -1
        }
        // pointerUp일 때는 타일 추가하지 않음 — 손 뗀 위치로 잘못 판정되어 터치만 해도 DecreaseNumber 되는 버그 방지
        else if (isDragging && pointerHeld)
        {
            Tile hit = GetTileAtScreen(screenPoint);
            // 숫자가 남아 있으면 이미 라인이 그려진 타일이라도 재방문(중복 밟기) 허용.
            // 숫자는 '들어갈 때'가 아니라 '지나쳐 나갈 때' 감소 → 멈춘 타일이 0이 되어 다음 드래그를 못 시작하는 문제 방지.
            if (hit != null && hit.IsActive)
            {
                Tile last = currentPath[currentPath.Count - 1];
                // 인접하고, (처음 밟는 타일이거나 / ShortCircuit만 재방문 불가, 나머지는 같은 드래그에서 다시 밟기 가능)
                bool canStep = hit != last && (!currentPath.Contains(hit) || hit.GetComponent<ShortCircuitTile>() == null);
                // Igniter 포함 이후에는 해당 지점 이전으로 백트래킹(되돌리기) 차단
                if (canStep && currentPath.Contains(hit))
                {
                    int igniterIdx = -1;
                    for (int i = 0; i < currentPath.Count; i++)
                    {
                        if (currentPath[i].GetComponent<IgniterTile>() != null) { igniterIdx = i; break; }
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
                        UpdateLineRendererPositions();
                        linkSystem?.ClearPathLit();
                        isGameOverSequencePlaying = true;
                        StartCoroutine(GameOverAndResetSequence());
                    }
                }
                else if (fixedKnotHit != null && IsAdjacent(last, hit))
                {
                    // FixedKnot 정확한 순서로만 진입 허용 (기어는 다음 타일 밟을 때 사라짐)
                    OnLeaveTileForNext(last, hit);
                    NotifyTwinLinkStepped(last);
                    NotifyFixedKnotLeft(last);
                    var crossBlast = last.GetComponent<CrossBlastTile>();
                    if (crossBlast != null) crossBlast.TriggerExplosion(this, hit);
                    var blackout = last.GetComponent<BlackoutTile>();
                    if (blackout != null) blackout.OnStepped();
                    currentPath.Add(hit);
                    TryTriggerIgniter(hit);
                    int cFk = GetTotalPathCount();
                    Debug.Log($"[GameManager] totalPathCount 갱신(기어 -1 효과): {cFk} — 원인: FixedKnot 타일 추가");
                    NotifyFixedKnotsUpdateVisual(cFk);
                    fixedKnotHit.OnSteppedCorrectly();
                    TryTriggerBlindCurtain(hit);
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
                            OnLeaveTileForNext(last, hit);
                            NotifyTwinLinkStepped(last);
                            NotifyFixedKnotLeft(last);
                            currentPath.Add(hit);
                            TryTriggerIgniter(hit);
                            int totalPathCount = GetTotalPathCount();
                            if (fixedKnotHit == null && CheckFixedKnotMissed(totalPathCount))
                            {
                                currentPath.RemoveAt(currentPath.Count - 1);
                                last.GetComponent<Tile>().SetNumber(last.CurrentNumber + 1);
                                isDragging = false;
                                currentPath.Clear();
                                UpdateLineRendererPositions();
                                linkSystem?.ClearPathLit();
                                isGameOverSequencePlaying = true;
                                StartCoroutine(GameOverAndResetSequence());
                            }
                            else
                            {
                                Debug.Log($"[GameManager] totalPathCount 갱신(기어 -1 효과): {totalPathCount} — 원인: ShortCircuit(출구) 타일 추가");
                                NotifyFixedKnotsUpdateVisual(totalPathCount);
                                if (fixedKnotHit != null) fixedKnotHit.OnSteppedCorrectly();
                                TryTriggerBlindCurtain(hit);
                            }
                        }
                    }
                }
                else if (shortCircuitHit != null)
                {
                    // ShortCircuit으로 들어감: 인접하면 어느 방향에서든 진입 가능 (제한은 나갈 때만). FixedKnot이면 올바른 순서에서만 추가
                    if (IsAdjacent(last, hit) && (fixedKnotHit == null || fixedKnotHit.CanEnter(nextStepNumber)))
                    {
                        OnLeaveTileForNext(last, hit);
                        NotifyTwinLinkStepped(last);
                        NotifyFixedKnotLeft(last);
                        var crossBlast = last.GetComponent<CrossBlastTile>();
                        if (crossBlast != null)
                            crossBlast.TriggerExplosion(this, hit);
                        var blackout = last.GetComponent<BlackoutTile>();
                        if (blackout != null)
                            blackout.OnStepped();
                        currentPath.Add(hit);
                        TryTriggerIgniter(hit);
                        int totalPathCountSc = GetTotalPathCount();
                        if (fixedKnotHit == null && CheckFixedKnotMissed(totalPathCountSc))
                        {
                            currentPath.RemoveAt(currentPath.Count - 1);
                            last.GetComponent<Tile>().SetNumber(last.CurrentNumber + 1);
                            isDragging = false;
                            currentPath.Clear();
                            UpdateLineRendererPositions();
                            linkSystem?.ClearPathLit();
                            isGameOverSequencePlaying = true;
                            StartCoroutine(GameOverAndResetSequence());
                        }
                        else
                        {
                            Debug.Log($"[GameManager] totalPathCount 갱신(기어 -1 효과): {totalPathCountSc} — 원인: ShortCircuit(진입) 타일 추가");
                            NotifyFixedKnotsUpdateVisual(totalPathCountSc);
                            if (fixedKnotHit != null) fixedKnotHit.OnSteppedCorrectly();
                            TryTriggerBlindCurtain(hit);
                        }
                    }
                }
                else
                {
                    // 일반 타일 이동 (FixedKnot이면 올바른 순서에서만 추가)
                    if (IsAdjacent(last, hit) && (fixedKnotHit == null || fixedKnotHit.CanEnter(nextStepNumber)))
                    {
                        OnLeaveTileForNext(last, hit); // 떠나는 타일: Igniter면 소멸 연출, 아니면 숫자 감소
                        NotifyTwinLinkStepped(last);
                        NotifyFixedKnotLeft(last);
                        var crossBlast = last.GetComponent<CrossBlastTile>();
                        if (crossBlast != null)
                            crossBlast.TriggerExplosion(this, hit); // hit = 다음 타일(밟고 이동한 타일) → 효과 제외
                        var blackout = last.GetComponent<BlackoutTile>();
                        if (blackout != null)
                            blackout.OnStepped(); // Blackout 타일 밟을 때 Punch Scale·탁해짐 피드백
                        currentPath.Add(hit);
                        TryTriggerIgniter(hit);
                        int totalPathCountEl = GetTotalPathCount();
                        if (fixedKnotHit == null && CheckFixedKnotMissed(totalPathCountEl))
                        {
                            currentPath.RemoveAt(currentPath.Count - 1);
                            last.GetComponent<Tile>().SetNumber(last.CurrentNumber + 1);
                            isDragging = false;
                            currentPath.Clear();
                            UpdateLineRendererPositions();
                            linkSystem?.ClearPathLit();
                            isGameOverSequencePlaying = true;
                            StartCoroutine(GameOverAndResetSequence());
                        }
                        else
                        {
                            Debug.Log($"[GameManager] totalPathCount 갱신(기어 -1 효과): {totalPathCountEl} — 원인: 일반 타일 추가");
                            NotifyFixedKnotsUpdateVisual(totalPathCountEl);
                            if (fixedKnotHit != null) fixedKnotHit.OnSteppedCorrectly();
                            TryTriggerBlindCurtain(hit);
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
            CommitPathAndSetCurrentPosition();
            // 클리어를 먼저 검사. 모든 타일 0이면 데드락 검사와 겹치므로 클리어 우선 (그렇지 않으면 게임오버로 잘못 처리됨)
            CheckStageClear();
            if (!stageCleared && CheckAndHandleDeadlock())
                ; // 데드락이면 GameOver 연출은 CheckAndHandleDeadlock 내부에서 시작
        }

        if (isDragging)
        {
            UpdateLineRendererPositions();
            linkSystem?.SetPathLit(currentPath, lineColor);
        }
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

    private Tile GetTileAtScreen(Vector2 screenPoint)
    {
        if (mainCamera == null) return null;
        float camZ = mainCamera.transform.position.z;
        Vector3 world3 = mainCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, Mathf.Abs(camZ)));
        Vector2 worldPoint = new Vector2(world3.x, world3.y);
        Collider2D col = Physics2D.OverlapPoint(worldPoint);
        return col != null ? col.GetComponent<Tile>() : null;
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
                    t.DecreaseNumber();
                    NotifyTwinLinkStepped(t);
                }
            }
        }
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

    /// <summary>TwinLink 타일이 밟린 직후: 짝꿍 count 동기화·전기 테두리 번쩍임·DOShakePosition.</summary>
    private void NotifyTwinLinkStepped(Tile tile)
    {
        if (tile == null) return;
        var twin = tile.GetComponent<TwinLinkTile>();
        if (twin != null) twin.OnSteppedSyncPartners();
    }

    /// <summary>타일을 떠날 때: Igniter면 소멸 연출 후 0, 아니면 DecreaseNumber.</summary>
    private void OnLeaveTileForNext(Tile last, Tile next)
    {
        if (last == null) return;
        var igniter = last.GetComponent<IgniterTile>();
        if (igniter != null)
            igniter.OnLeftThenVanish();
        else
            last.DecreaseNumber();
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

    /// <summary>FixedKnot 화면 갱신. 오직 CurrentPathList( linePoints + currentPath ) Count만 전달. 터치 떼기·백트래킹 시에도 이 값만으로 갱신.</summary>
    private void NotifyFixedKnotsUpdateVisual(int totalPathCount)
    {
        Debug.Log($"[GameManager] NotifyFixedKnotsUpdateVisual 호출 → totalPathCount={totalPathCount} (모든 FixedKnot에 표시 = targetOrder - 이 값)");
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
        // 실제로 다른 타일로 이동했을 때만 linePoints 추가 및 totalStepsCommitted 갱신 (시작점만 터치한 경우 제외)
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
            foreach (Tile t in currentPath)
            {
                if (t == null) continue;
                Vector3 p = t.transform.position;
                p.z = -0.5f;
                linePoints.Add(p);
            }
            // 기어 스텝 수: 옮긴 횟수만. 첫 구간도 이어서도 새로 밟은 타일 수 = currentPath.Count - 1 (시작점 제외)
            totalStepsCommitted += (currentPath.Count - 1);
            Debug.Log($"[GameManager] totalPathCount 갱신(기어 -1 효과): {totalStepsCommitted} — 원인: CommitPath (손 뗀 후 경로 커밋)");
            NotifyFixedKnotsUpdateVisual(totalStepsCommitted);

            if (lineClearRoutine != null)
                StopCoroutine(lineClearRoutine);
            lineClearRoutine = StartCoroutine(LineClearAfterDelayRoutine(lineClearDelay));

            // 마지막 타일은 "다음 타일로 이동"이 없어서 -1이 안 됨. 손 뗄 때(커밋) 마지막 타일 1 감소 → 0이면 클리어
            if (currentPath.Count > 0)
            {
                Tile lastTile = currentPath[currentPath.Count - 1];
                if (lastTile != null && lastTile.CurrentNumber > 0)
                {
                    var igniter = lastTile.GetComponent<IgniterTile>();
                    if (igniter != null)
                        igniter.OnLeftThenVanish();
                    else
                        lastTile.DecreaseNumber();
                    NotifyTwinLinkStepped(lastTile);
                }
            }
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
        UpdateLineRendererPositions();
    }

    private IEnumerator LineClearAfterDelayRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        linePoints.Clear();
        UpdateLineRendererPositions();
        linkSystem?.ClearPathLit();
        // totalStepsCommitted는 건드리지 않음 → 기어 숫자 그대로 이어감
        lineClearRoutine = null;
    }

    private void UpdateLineRendererPositions()
    {
        if (lineRenderer == null) return;

        // 파괴된 타일 참조 제거 (리셋 등으로 Tile이 파괴된 경우 MissingReferenceException 방지)
        for (int i = currentPath.Count - 1; i >= 0; i--)
        {
            if (currentPath[i] == null)
                currentPath.RemoveAt(i);
        }

        int total = linePoints.Count + currentPath.Count;
        if (total == 0)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        lineRenderer.positionCount = total;
        int idx = 0;
        foreach (Vector3 p in linePoints)
        {
            lineRenderer.SetPosition(idx++, p);
        }
        for (int i = 0; i < currentPath.Count; i++)
        {
            Tile t = currentPath[i];
            if (t == null) continue;
            Vector3 p = t.transform.position;
            p.z = -0.5f;
            lineRenderer.SetPosition(idx++, p);
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
        if (lineClearRoutine != null)
        {
            StopCoroutine(lineClearRoutine);
            lineClearRoutine = null;
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
        linePoints.Clear();
        totalStepsCommitted = 0;
        UpdateLineRendererPositions();
        linkSystem?.ClearLinks();

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

        isGameOverSequencePlaying = false;
    }

    /// <summary>
    /// 모든 타일(특수 타일 포함)이 0이면 클리어. 로그 "Clear" 후 다음 스테이지.
    /// </summary>
    private void CheckStageClear()
    {
        if (tiles == null) return;
        for (int row = 0; row < stageHeight; row++)
            for (int col = 0; col < stageWidth; col++)
                if (tiles[row, col] != null && tiles[row, col].IsActive)
                    return;
        stageCleared = true;
        Debug.Log("Clear");
        StartCoroutine(LoadNextStageAfterDelay());
    }

    private IEnumerator LoadNextStageAfterDelay()
    {
        yield return new WaitForSeconds(nextStageDelay);
        currentStageIndex++;
        StageData data = StageManager.LoadStage(currentStageIndex);
        if (data == null)
        {
            Debug.Log("All stages clear. 처음 스테이지부터 다시.");
            currentStageIndex = 1;
            data = StageManager.LoadStage(1);
            if (data == null)
            {
                stageCleared = false;
                yield break;
            }
        }
        linePoints.Clear();
        totalStepsCommitted = 0;
        ClearTiles();
        CreateGridFromStageData(data);
        SetCurrentStartTileFromStageData(data);
        SetupSpotlight(data);
        AdjustCameraToFitGrid();
        stageCleared = false;
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

    private void CreateLineRenderer()
    {
        GameObject lineGo = new GameObject("PathLine");
        lineGo.transform.SetParent(transform);

        lineRenderer = lineGo.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth * 0.6f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;
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
        float sizeByHeight = totalGridHeight * 0.5f * fitMargin;
        float sizeByWidth = (totalGridWidth * 0.5f) / aspect * fitMargin;
        mainCamera.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth);
        mainCamera.transform.position = new Vector3(0f, 0f, mainCamera.transform.position.z);
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
        Debug.Log($"[GameManager] totalPathCount 갱신(기어 초기화): 0 — 원인: 스테이지 로드");
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
}
