using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

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

    /// <summary>CrossBlastTile·LinkSystem 등에서 그리드 크기 참조용.</summary>
    public int StageWidth => stageWidth;
    public int StageHeight => stageHeight;
    public LinkSystem GetLinkSystem() => linkSystem;

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
                // 이미 이번 드래그에서 밟은 타일로는 다시 들어가지 않음 → 타일 한 칸 옮길 때마다 정확히 -1만 적용 (흔들림/재진입 시 -2 방지)
                if (hit != last && !currentPath.Contains(hit))
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
                    last.DecreaseNumber();
                    NotifyFixedKnotLeft(last);
                    var crossBlast = last.GetComponent<CrossBlastTile>();
                    if (crossBlast != null) crossBlast.TriggerExplosion(this, hit);
                    var blackout = last.GetComponent<BlackoutTile>();
                    if (blackout != null) blackout.OnStepped();
                    currentPath.Add(hit);
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
                            last.DecreaseNumber();
                            NotifyFixedKnotLeft(last);
                            currentPath.Add(hit);
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
                        last.DecreaseNumber();
                        NotifyFixedKnotLeft(last);
                        var crossBlast = last.GetComponent<CrossBlastTile>();
                        if (crossBlast != null)
                            crossBlast.TriggerExplosion(this, hit);
                        var blackout = last.GetComponent<BlackoutTile>();
                        if (blackout != null)
                            blackout.OnStepped();
                        currentPath.Add(hit);
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
                        last.DecreaseNumber(); // 떠나는 타일에서 숫자 감소
                        NotifyFixedKnotLeft(last);
                        var crossBlast = last.GetComponent<CrossBlastTile>();
                        if (crossBlast != null)
                            crossBlast.TriggerExplosion(this, hit); // hit = 다음 타일(밟고 이동한 타일) → 효과 제외
                        var blackout = last.GetComponent<BlackoutTile>();
                        if (blackout != null)
                            blackout.OnStepped(); // Blackout 타일 밟을 때 Punch Scale·탁해짐 피드백
                        currentPath.Add(hit);
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
            if (!CheckAndHandleDeadlock())
                CheckStageClear();
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
                    t.DecreaseNumber();
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
        }

        if (currentPath.Count > 0)
        {
            Tile lastTile = currentPath[currentPath.Count - 1];
            if (lastTile != null)
            {
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
    /// 게임오버: 글리치(0.5초) → 암전 → 1.5초 대기 → 리셋: 숫자·스케일0 → 순차 등장(위→아래 0.1초 간격, Bounce). 라인 즉시 삭제.
    /// </summary>
    private IEnumerator GameOverAndResetSequence()
    {
        linePoints.Clear();
        totalStepsCommitted = 0;
        UpdateLineRendererPositions();

        // 리셋 시 link.png도 같이 리셋 (기존 링크 제거 → 타일 재등장 후 다시 생성)
        linkSystem?.ClearLinks();

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

        for (int row = 0; row < stageHeight; row++)
            for (int col = 0; col < stageWidth; col++)
                if (tiles[row, col] != null)
                {
                    tiles[row, col].ResetToInitial();
                    var fixedKnot = tiles[row, col].GetComponent<FixedKnotTile>();
                    if (fixedKnot != null) fixedKnot.ResetGearVisibility();
                    tiles[row, col].SetScaleZero();
                }

        for (int row = stageHeight - 1; row >= 0; row--)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                if (tiles[row, col] != null)
                {
                    yield return new WaitForSeconds(tileAppearInterval);
                    tiles[row, col].PlayBounceAppearance();
                }
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
            }
        }

        // 타일들이 다시 생성(등장)된 뒤 CrossBlast 기준 link.png 다시 생성
        if (linkSystem != null && tiles != null)
            linkSystem.CreateLinksForCrossBlastOnly(tiles, stageWidth, stageHeight);

        isGameOverSequencePlaying = false;
    }

    /// <summary>
    /// 모든 타일 숫자가 0이면 Stage Clear 로그.
    /// </summary>
    private void CheckStageClear()
    {
        if (tiles == null) return;
        for (int row = 0; row < stageHeight; row++)
            for (int col = 0; col < stageWidth; col++)
                if (tiles[row, col] != null && tiles[row, col].IsActive)
                    return;
        stageCleared = true;
        Debug.Log("Stage Clear");
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
                tile.SetNumber(cell.type == "BlindCurtain" ? 1 : cell.count);
                tiles[cell.y, cell.x] = tile;
            }
        }

        if (linkSystem == null)
        {
            GameObject go = new GameObject("LinkSystem");
            go.transform.SetParent(transform);
            linkSystem = go.AddComponent<LinkSystem>();
        }
        linkSystem.CreateLinksForCrossBlastOnly(tiles, data.width, data.height);
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
