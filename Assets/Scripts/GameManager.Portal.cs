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

    private const float PortalEmergeDelay = 0.1f;

    public int CurrentPortalPhase => portalPhase;

    /// <summary>스테이지(또는 즉석 보드)를 새로 올릴 때 포탈 상태를 정한다. 진행 중인 전환은 취소된다.</summary>
    private void SetupPortalForLoadedStage(StageData data)
    {
        portalStageData = data != null && data.HasPhase2 ? data : null;
        portalPhase = portalStageData != null ? 1 : 0;
        portalTransitioning = false;
        portalPhaseOneOneStroke = true;
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

    private IEnumerator PortalTransitionRoutine(Tile portalTile, StageData stageData)
    {
        portalTile.GetComponent<PortalTile>()?.PlaySuck();
        clayZero?.SuckInto(portalTile);
        gameplayFeedback?.PortalEntered(portalTile);
        PlayEventSfx(portalSuckClip, 0.85f);
        DevLog.Log($"[포탈] stage={currentStageIndex} 1페이즈 완료 → 2페이즈");

        yield return new WaitForSecondsRealtime(PortalTile.SuckSeconds + PortalEmergeDelay);
        // 그 사이 스킵·스테이지 이동 등으로 다른 판이 올라왔으면 아무것도 하지 않는다.
        if (!portalTransitioning || portalStageData != stageData)
            yield break;

        LoadPortalBoard(stageData.CreatePhase2Stage(), 2, "portal_phase2");
        PlayStageStartVisuals();
        PlayEventSfx(portalEmergeClip, 0.85f);
        clayZero?.EmergeAt(currentStartTile);
        if (mainUI == null)
            mainUI = FindFirstObjectByType<GameMainUIController>();
        mainUI?.SetupPortalPhase(initialTileCountForUI, GetTotalRemainingCount(), 2);
        portalTransitioning = false;
        SchedulePostStageStartedFailureCheck();
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
        LoadPortalBoard(portalStageData, 1, "portal_phase1_restore");
        portalPhaseOneOneStroke = true;
        clayZero?.EmergeAt(currentStartTile);
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
