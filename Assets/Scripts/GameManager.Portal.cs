using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 포탈 판(2페이즈 스테이지). 규칙은 Assets/rules/TILE_RULES.md "포탈" 절.
/// - 1페이즈 보드에는 포탈 칸(숫자 1) 하나가 처음부터 보인다. 다른 칸을 모두 지운 뒤 마지막 이동으로만 들어갈 수 있다.
/// - 포탈에 들어가면 클리어 대신 제로가 빨려 들어가고, 같은 자리에 2페이즈 보드가 나온다. 2페이즈를 깨야 스테이지 클리어.
/// - 2페이즈에서 실패하거나 리셋하면 1페이즈부터 다시 한다. 되돌리기는 페이즈를 넘어가지 못한다.
/// </summary>
public partial class GameManager
{
    private StageData portalStageData;      // 포탈 판이면 1페이즈 데이터(2페이즈는 여기서 만든다)
    private int portalPhase;                // 0 = 포탈 판 아님, 1, 2
    private bool portalTransitioning;
    private bool portalPhaseOneOneStroke = true;
    private PortalTile activePortal;


    public int CurrentPortalPhase => portalPhase;

    /// <summary>스테이지(또는 즉석 보드)를 새로 올릴 때 포탈 상태를 정한다. 진행 중인 전환은 취소된다.</summary>
    private void SetupPortalForLoadedStage(StageData data)
    {
        portalStageData = data != null && data.HasPhase2 ? data : null;
        portalPhase = portalStageData != null ? 1 : 0;
        portalTransitioning = false;
        portalPhaseOneOneStroke = true;
        clayZero?.CancelSuck();
    }

    /// <summary>
    /// 규칙 커널의 포탈 판정: 떠나는 칸이 1이고 남은 합이 (떠나는 칸 1 + 포탈) 뿐일 때만 들어갈 수 있다.
    /// 실제 플레이·힌트·실패 판정이 같은 판정을 쓴다(getRemainingCount가 각자의 상태를 준다).
    /// </summary>
    private bool CanEnterPortalKernel(Tile from, Tile portal, Func<Tile, int> getRemainingCount)
    {
        if (getRemainingCount(from) != 1 || tiles == null)
            return false;
        int total = 0;
        for (int row = 0; row < stageHeight; row++)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                Tile tile = tiles[row, col];
                if (tile != null)
                    total += Mathf.Max(0, getRemainingCount(tile));
            }
        }
        return total == 1 + getRemainingCount(portal);
    }

    /// <summary>포탈에 들어갔을 때(CheckVictoryCondition에서): 클리어 대신 2페이즈 전환을 시작한다.</summary>
    private bool TryBeginPortalTransition(Tile portalTile)
    {
        if (portalPhase != 1 || portalStageData == null || portalTransitioning || portalTile == null)
            return false;

        int strokes = attemptStrokeCount + (isDragging && currentPath.Count > 1 ? 1 : 0);
        portalPhaseOneOneStroke = strokes == 1 && !attemptUsedUndo;
        portalTransitioning = true;
        ResetActiveDragState("portal_enter");
        ClearHintPreview();
        currentStartTile = portalTile;
        StartCoroutine(PortalTransitionRoutine(portalTile, portalStageData));
        return true;
    }

    // 포탈 전환 연출 시간(초): 소용돌이가 빨라짐 → 제로가 빨려 듦 → 판 교체 → 칸이 쏟아져 나옴 → 제로 등장
    private const float PortalSpinUpSeconds = 0.35f;
    private const float PortalDiveSeconds = 0.25f;
    private const float PortalSpillRingDelay = 0.06f;
    private const float PortalTileFlightSeconds = 0.28f;
    private const float PortalCameraGlideSeconds = 0.5f;

    /// <summary>
    /// 포탈 전환: 2페이즈 시작 칸이 포탈이 있던 화면 자리에 그대로 나타나고, 나머지 칸이 거기서 쏟아져 나와 자리를 잡는다.
    /// 판 데이터는 그대로이고 카메라만 옮긴다(판은 늘 원점 가운데에 깔린다).
    /// </summary>
    private IEnumerator PortalTransitionRoutine(Tile portalTile, StageData stageData)
    {
        Camera cam = mainCamera != null ? mainCamera : Camera.main;
        portalTile.GetComponent<PortalTile>()?.PlaySuck();
        gameplayFeedback?.PortalEntered(portalTile);
        PlayEventSfx(portalSuckClip, 0.85f);
        StartCoroutine(FadeClaySlots(claySlotContainer, 1f, 0f, PortalSpinUpSeconds + PortalDiveSeconds));
        DevLog.Log($"[포탈] stage={currentStageIndex} 1페이즈 완료 → 2페이즈");

        yield return new WaitForSecondsRealtime(PortalSpinUpSeconds);
        if (!portalTransitioning || portalStageData != stageData)
            yield break;
        clayZero?.SuckInto(portalTile, PortalDiveSeconds);
        yield return new WaitForSecondsRealtime(PortalDiveSeconds);
        // 그 사이 스킵·스테이지 이동 등으로 다른 판이 올라왔으면 아무것도 하지 않는다.
        if (!portalTransitioning || portalStageData != stageData || portalTile == null)
            yield break;

        Vector3 portalWorld = portalTile.transform.position;
        PortalTile portalVisual = portalTile.GetComponent<PortalTile>();
        float portalHalf = portalVisual != null ? portalVisual.WorldHalfSize : tileWidth * 0.5f;
        gameplayFeedback?.RestoreCameraPose();
        Vector3 camFrom = cam != null ? cam.transform.position : Vector3.zero;
        float sizeFrom = cam != null ? cam.orthographicSize : 0f;

        LoadPortalBoard(stageData.CreatePhase2Stage(), 2, "portal_phase2");
        Tile start = currentStartTile;
        // 최종 화면(새 판 전체가 보이는 위치)은 LoadPortalBoard가 맞춰 둔 값이다. 시작은 시작 칸이 포탈 자리에 오도록 맞춘다.
        Vector3 camTo = cam != null ? cam.transform.position : Vector3.zero;
        float sizeTo = cam != null ? cam.orthographicSize : 0f;
        if (cam != null && start != null)
        {
            Vector3 shift = start.transform.position - portalWorld;
            cam.transform.position = new Vector3(camFrom.x + shift.x, camFrom.y + shift.y, camFrom.z);
            cam.orthographicSize = sizeFrom;
            camFrom = cam.transform.position;
            PortalTile.SpawnAfterglow(start.transform.position, portalHalf, start.GetComponent<SpriteRenderer>());
        }
        // 화면은 최소한만 움직인다: 시작 칸이 포탈 자리에 그대로 있도록 잡고, 새 판이 화면 밖으로 나가는 만큼만 옮긴다.
        if (cam != null && start != null && sizeFrom > 0f)
        {
            Vector3 relative = (portalWorld - camFrom) / sizeFrom;
            Vector3 keep = start.transform.position - relative * sizeTo;
            camTo = ClampCameraToShowBoard(new Vector3(keep.x, keep.y, camTo.z), sizeTo, camTo);
        }
        PlayEventSfx(portalEmergeClip, 0.85f);
        StartCoroutine(GlideCamera(cam, camFrom, sizeFrom, camTo, sizeTo, PortalCameraGlideSeconds, 0.08f));
        yield return SpillTilesFromStart(start);

        if (!portalTransitioning || portalStageData != stageData)
            yield break;
        clayZero?.EmergeAt(currentStartTile);
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        mainUI?.SetupPortalPhase(initialTileCountForUI, GetTotalRemainingCount(), 2);
        yield return new WaitForSecondsRealtime(0.08f);
        portalTransitioning = false;
        SchedulePostStageStartedFailureCheck();
    }

    /// <summary>
    /// 카메라 위치를 판(원점 가운데)이 상·하단 UI 사이 화면 안에 다 보이는 범위로 자른다.
    /// 어느 방향으로도 다 보일 수 없으면 그 축은 판에 맞춘 위치(fit)를 쓴다.
    /// </summary>
    private Vector3 ClampCameraToShowBoard(Vector3 desired, float size, Vector3 fit)
    {
        GetGameplayMarginsNormalized(out float top, out float bottom);
        float aspect = GetCameraAspect();
        float m = tileWidth * 0.15f;
        float halfW = totalGridWidth * 0.5f + m;
        float halfH = totalGridHeight * 0.5f + m;
        float minX = halfW - size * aspect;
        float maxX = size * aspect - halfW;
        float minY = halfH - size + 2f * size * top;
        float maxY = size - 2f * size * bottom - halfH;
        float x = minX <= maxX ? Mathf.Clamp(desired.x, minX, maxX) : fit.x;
        float y = minY <= maxY ? Mathf.Clamp(desired.y, minY, maxY) : fit.y;
        return new Vector3(x, y, desired.z);
    }

    /// <summary>2페이즈 칸들이 시작 칸 자리에서 튀어나와 제자리로 날아가 앉는다. 시작 칸에서 가까운 칸부터 물결처럼 퍼진다.</summary>
    private IEnumerator SpillTilesFromStart(Tile start)
    {
        if (tiles == null || start == null)
            yield break;
        var ring = new System.Collections.Generic.Dictionary<Tile, int>();
        var queue = new System.Collections.Generic.Queue<Tile>();
        ring[start] = 0;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Tile from = queue.Dequeue();
            for (int i = 0; i < CrossBlastDx.Length; i++)
            {
                Tile n = GetTileAtGrid(from.X + CrossBlastDx[i], from.Y + CrossBlastDy[i]);
                if (n == null || ring.ContainsKey(n))
                    continue;
                ring[n] = ring[from] + 1;
                queue.Enqueue(n);
            }
        }

        var slots = CollectClaySlotsByPosition();
        foreach (SpriteRenderer slot in slots.Values)
            SetSpriteAlpha(slot, 0f);

        Vector3 origin = start.transform.position;
        int maxRing = 0;
        var flights = new System.Collections.Generic.List<(Tile tile, Vector3 home, float delay)>();
        for (int row = 0; row < stageHeight; row++)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                Tile tile = tiles[row, col];
                if (tile == null)
                    continue;
                if (!ShouldUseTileForStageStartVisual(tile))
                    continue;
                int r = ring.TryGetValue(tile, out int d) ? d : 1;
                maxRing = Mathf.Max(maxRing, r);
                flights.Add((tile, tile.transform.position, r * PortalSpillRingDelay));
                tile.SetScaleZero();
                tile.transform.position = origin;
            }
        }

        float elapsed = 0f;
        float total = maxRing * PortalSpillRingDelay + PortalTileFlightSeconds;
        var launched = new System.Collections.Generic.HashSet<Tile>();
        while (elapsed < total + 0.02f)
        {
            foreach (var f in flights)
            {
                if (f.tile == null)
                    continue;
                float local = elapsed - f.delay;
                if (local < 0f)
                    continue;
                if (launched.Add(f.tile))
                {
                    f.tile.PlayBounceAppearance();
                    RefreshSpecialTileVisualStateAfterAppearance(f.tile);
                }
                float t = Mathf.Clamp01(local / PortalTileFlightSeconds);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                // 살짝 위로 포물선을 그리며 날아간다.
                Vector3 p = Vector3.Lerp(origin, f.home, eased);
                p.y += Mathf.Sin(t * Mathf.PI) * tileHeight * 0.25f * Mathf.Min(1f, Vector3.Distance(origin, f.home) / Mathf.Max(0.01f, tileWidth));
                f.tile.transform.position = p;
                if (slots.TryGetValue(SlotKey(f.home), out SpriteRenderer slot))
                    SetSpriteAlpha(slot, t);
            }
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        foreach (var f in flights)
        {
            if (f.tile != null)
                f.tile.transform.position = f.home;
        }
        foreach (SpriteRenderer slot in slots.Values)
            SetSpriteAlpha(slot, 1f);
        RestoreCurrentStartMarkerVisual();
    }

    private static Vector2Int SlotKey(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x * 100f), Mathf.RoundToInt(p.y * 100f));

    private System.Collections.Generic.Dictionary<Vector2Int, SpriteRenderer> CollectClaySlotsByPosition()
    {
        var map = new System.Collections.Generic.Dictionary<Vector2Int, SpriteRenderer>();
        if (claySlotContainer == null)
            return map;
        foreach (SpriteRenderer sr in claySlotContainer.GetComponentsInChildren<SpriteRenderer>(true))
            map[SlotKey(sr.transform.position)] = sr;
        return map;
    }

    private static void SetSpriteAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null)
            return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }

    /// <summary>점토 음각 자리들을 서서히 나타나게/사라지게 한다.</summary>
    private IEnumerator FadeClaySlots(Transform container, float from, float to, float duration)
    {
        if (container == null)
            yield break;
        SpriteRenderer[] slots = container.GetComponentsInChildren<SpriteRenderer>(true);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float a = Mathf.Lerp(from, to, elapsed / Mathf.Max(0.01f, duration));
            foreach (SpriteRenderer sr in slots)
                SetSpriteAlpha(sr, a);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        foreach (SpriteRenderer sr in slots)
            SetSpriteAlpha(sr, to);
    }

    /// <summary>카메라를 부드럽게 옮긴다(흔들림 연출의 오프셋은 매 프레임 지우고 잡는다).</summary>
    private IEnumerator GlideCamera(Camera cam, Vector3 fromPos, float fromSize, Vector3 toPos, float toSize, float duration, float delay)
    {
        if (cam == null)
            yield break;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            gameplayFeedback?.RestoreCameraPose();
            cam.transform.position = Vector3.Lerp(fromPos, toPos, eased);
            cam.orthographicSize = Mathf.Lerp(fromSize, toSize, eased);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        gameplayFeedback?.RestoreCameraPose();
        cam.transform.position = toPos;
        cam.orthographicSize = toSize;
    }

    /// <summary>페이즈 보드를 화면에 올린다. 스테이지 번호·별·통계는 그대로다(포탈 판은 두 페이즈가 한 스테이지).</summary>
    private void LoadPortalBoard(StageData board, int phase, string reason)
    {
        if (board == null)
            return;
        ResetActiveDragState(reason);
        ClearTiles();
        CreateGridFromStageData(board);
        SetCurrentStartTileFromStageData(board);
        SetupSpotlight(board);
        AdjustCameraToFitGrid();
        SyncBackgroundGridReferenceCameraSize();
        portalPhase = phase;
        totalStepsCommitted = 0;
        stageCleared = false;
        initialTileCountForUI = Mathf.Max(1, GetTotalRemainingCount());
        SetupMelodyForCurrentStage();
        IncrementBoardVersion(reason);
        // 되돌리기는 페이즈를 넘어가지 못한다. 한 획 판정은 페이즈마다 따로 센다(RecordStageStars).
        ResetUndoHistory();
        attemptMoves.Clear();
        attemptStrokeCount = 0;
        gameplayFeedback?.BeginStage(Mathf.Min(tileWidth, tileHeight));
    }

    /// <summary>2페이즈에서 실패·리셋했을 때: 1페이즈 보드를 다시 올린다(이어지는 리셋 루틴이 타일을 순차 등장시킨다).</summary>
    private void RestorePortalPhaseOneIfNeeded()
    {
        if (portalPhase != 2 || portalStageData == null)
            return;
        Camera cam = mainCamera != null ? mainCamera : Camera.main;
        gameplayFeedback?.RestoreCameraPose();
        Vector3 camFrom = cam != null ? cam.transform.position : Vector3.zero;
        float sizeFrom = cam != null ? cam.orthographicSize : 0f;
        LoadPortalBoard(portalStageData, 1, "portal_phase1_restore");
        portalPhaseOneOneStroke = true;
        clayZero?.EmergeAt(currentStartTile);
        // 화면도 1페이즈 자리로 부드럽게 돌아간다.
        if (cam != null)
        {
            Vector3 camTo = cam.transform.position;
            float sizeTo = cam.orthographicSize;
            cam.transform.position = camFrom;
            cam.orthographicSize = sizeFrom;
            StartCoroutine(GlideCamera(cam, camFrom, sizeFrom, camTo, sizeTo, 0.4f, 0f));
        }
    }

    /// <summary>포탈이 지금 열려 있는지(포탈 말고는 1칸만 남음) 소용돌이 표시에 알린다.</summary>
    private void UpdatePortalOpenState()
    {
        if (activePortal == null || portalPhase != 1)
            return;
        Tile portalTile = activePortal.GetComponent<Tile>();
        if (portalTile == null)
            return;
        int others = GetTotalRemainingCount() - portalTile.CurrentNumber;
        activePortal.IsOpen = portalTransitioning || others <= 1;
    }
}
