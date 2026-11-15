using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 진행 위치(frontier)·갈래 길·지도 이동·도전 목표 별을 실제 GameManager로 검증한다.
/// 배치 모드: Unity -batchmode -nographics -runTests -testPlatform EditMode -testFilter StageProgressTests
/// (별 기록·진행도를 바꾸므로 Tools/DevScripts/clone_run.sh처럼 저장 데이터를 되돌리는 러너로 돌린다)
/// </summary>
public class StageProgressTests
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [UnityTest, Timeout(600000)]
    public IEnumerator FrontierWindowMapAndChallengeStars()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
        }
        var gm = Object.FindFirstObjectByType<GameManager>();
        var ui = Object.FindFirstObjectByType<GameMainUIController>();
        Assert.IsNotNull(gm);
        yield return Wait(2f);
        StageStarsStore.Clear();
        var failures = new List<string>();

        // 디버그 이동은 진행 위치를 앞으로만 옮긴다. 그 뒤 세 판(21·22·23)만 열린다.
        gm.AutomationSetFrontier(1);
        gm.LoadStageImmediateForDebug(21);
        yield return Settle(gm, ui);
        Check(failures, gm.StageFrontier == 21, $"frontier after jump = {gm.StageFrontier}");
        Check(failures, gm.IsStageUnlocked(1) && gm.IsStageUnlocked(21) && gm.IsStageUnlocked(23), "window should open 21..23");
        Check(failures, !gm.IsStageUnlocked(24), "24 must stay locked");
        Check(failures, !gm.IsStageUnlocked(FinalEndingProgressStore.FinalStageIndex), "final stage must stay locked");

        // 앞질러 23을 깨면: 24는 아직 잠겨 있으므로 맨 앞(21)으로 돌아온다. 진행 위치는 그대로.
        Check(failures, gm.LoadStageFromMap(23), "map select 23 refused");
        yield return Settle(gm, ui);
        yield return PlaySolution(gm, 23, failures);
        yield return WaitForStage(gm, 21);
        Check(failures, gm.CurrentStageIndex == 21 && gm.StageFrontier == 21, $"after clearing 23: current {gm.CurrentStageIndex} frontier {gm.StageFrontier}");
        // 23 = 한 획 도전(23 % 3 == 2). 정답은 한 획이므로 별 3개.
        Check(failures, StageStarsStore.GetStars(23) == 3, $"stage 23 stars {StageStarsStore.GetStars(23)} (expected 3: one-stroke challenge)");

        // 21을 깨면 22로, 22를 깨면 이미 깬 23을 건너뛰어 24로.
        yield return Settle(gm, ui);
        yield return PlaySolution(gm, 21, failures);
        yield return WaitForStage(gm, 22);
        Check(failures, gm.CurrentStageIndex == 22 && gm.StageFrontier == 22, $"after clearing 21: current {gm.CurrentStageIndex} frontier {gm.StageFrontier}");
        // 21 = 별 칸에서 끝내기(21 % 3 == 0). 정답의 마지막 칸이 별 칸이다.
        Check(failures, StageChallenge.KindFor(21) == StageChallengeKind.EndOnMarked && StageStarsStore.GetStars(21) == 3, $"stage 21 stars {StageStarsStore.GetStars(21)}");
        yield return Settle(gm, ui);
        yield return PlaySolution(gm, 22, failures);
        yield return WaitForStage(gm, 24);
        Check(failures, gm.CurrentStageIndex == 24 && gm.StageFrontier == 24, $"after clearing 22: current {gm.CurrentStageIndex} frontier {gm.StageFrontier}");

        // 지나온 판을 다시 하면 바로 다음 판으로 이어지고, 진행 위치는 줄지 않는다.
        yield return Settle(gm, ui);
        Check(failures, gm.LoadStageFromMap(3), "map select 3 refused");
        yield return Settle(gm, ui);
        yield return PlaySolution(gm, 3, failures);
        yield return WaitForStage(gm, 4);
        Check(failures, gm.CurrentStageIndex == 4 && gm.StageFrontier == 24, $"after replaying 3: current {gm.CurrentStageIndex} frontier {gm.StageFrontier}");

        // 스킵도 진행 위치를 민다(맨 앞 판에서만).
        yield return Settle(gm, ui);
        Check(failures, gm.LoadStageFromMap(24), "map select 24 refused");
        yield return Settle(gm, ui);
        Check(failures, gm.LoadNextStageImmediate(), "skip refused");
        yield return Settle(gm, ui);
        Check(failures, gm.CurrentStageIndex == 25 && gm.StageFrontier == 25, $"after skip: current {gm.CurrentStageIndex} frontier {gm.StageFrontier}");

        // 잠긴 판은 지도에서도 못 연다.
        Check(failures, !gm.LoadStageFromMap(40), "locked stage 40 was loaded from the map");

        // 지도 열기/닫기: 열려 있는 동안은 모달.
        if (ui != null)
        {
            Check(failures, ui.OpenStageMap() && ui.IsModalOverlayOpen, "map did not open");
            ui.CloseStageMap();
            Check(failures, !ui.IsStageMapOpen, "map did not close");
        }

        yield return new ExitPlayMode();
        Assert.IsEmpty(failures, string.Join("\n", failures));
    }

    [UnityTest, Timeout(600000)]
    public IEnumerator EndlessLabUnlockNumberingHeartsAndVariants()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
        }
        var gm = Object.FindFirstObjectByType<GameManager>();
        var ui = Object.FindFirstObjectByType<GameMainUIController>();
        Assert.IsNotNull(gm);
        Assert.IsNotNull(ui);
        yield return Wait(2f);
        var failures = new List<string>();
        PlayerPrefs.DeleteKey("ZeroStep.LabStage");
        yield return Settle(gm, ui);
        ui.ResetHeartsForNewStage();
        int stageBefore = gm.CurrentStageIndex;
        int frontierBefore = gm.StageFrontier;

        // 판 종류는 번호로 정해진다: 같은 번호면 같은 결과, 변주는 5판에 한 번쯤, 크기는 세 가지가 섞여 나온다.
        int variants = 0, small = 0, medium = 0, large = 0;
        int oneStrokeStage = 0, memoryStage = 0;
        for (int n = 1; n <= 1000; n++)
        {
            GameManager.ResolveLabBoardKind(n, out GameManager.LabVariant variant, out BoardGenerator.Settings settings);
            GameManager.ResolveLabBoardKind(n, out GameManager.LabVariant again, out BoardGenerator.Settings settingsAgain);
            if (variant != again || settings.totalCount != settingsAgain.totalCount)
                failures.Add($"lab stage {n}: board kind is not deterministic");
            if (variant != GameManager.LabVariant.Normal)
                variants++;
            if (variant == GameManager.LabVariant.OneStroke && oneStrokeStage == 0)
                oneStrokeStage = n;
            if (variant == GameManager.LabVariant.Memory)
            {
                if (memoryStage == 0)
                    memoryStage = n;
                if (settings.totalCount != BoardGenerator.Settings.Small.totalCount)
                    failures.Add($"lab stage {n}: memory variant on a board that is not small");
            }
            if (variant == GameManager.LabVariant.Normal)
            {
                if (settings.totalCount == BoardGenerator.Settings.Small.totalCount) small++;
                else if (settings.totalCount == BoardGenerator.Settings.Medium.totalCount) medium++;
                else large++;
            }
            if (settings.totalCount > BoardGenerator.Settings.Large.totalCount)
                failures.Add($"lab stage {n}: board bigger than the large cap");
        }
        GameManager.ResolveLabBoardKind(1, out GameManager.LabVariant firstVariant, out _);
        Check(failures, firstVariant == GameManager.LabVariant.Normal, "lab stage 1 must be a classic board");
        Check(failures, variants >= 130 && variants <= 270, $"variants in 1000 boards = {variants} (expected about 200)");
        Check(failures, small > 150 && medium > 250 && large > 80, $"size mix small {small} medium {medium} large {large}");
        Check(failures, oneStrokeStage > 0 && memoryStage > 0, "no one-stroke / memory board found");

        // 엔딩 전에는 잠겨 있다.
        GameManager.AutomationSetLabUnlocked(false);
        Check(failures, !GameManager.IsLabUnlocked && !gm.StartLab() && !gm.IsLabActive, "lab opened while locked");
        GameManager.AutomationSetLabUnlocked(true);
        Check(failures, gm.StartLab() && gm.IsLabActive, "lab did not start when unlocked");
        yield return Settle(gm, ui);

        // 1번 판을 깨면 2번 판이 바로 만들어지고, 번호가 저장된다.
        Check(failures, gm.AutomationLabStage == 1 && gm.AutomationLabVariant == GameManager.LabVariant.Normal, $"first board: stage {gm.AutomationLabStage} variant {gm.AutomationLabVariant}");
        yield return PlayLabBoard(gm, failures);
        yield return WaitForLabStage(gm, 2);
        Check(failures, gm.AutomationLabStage == 2 && GameManager.SavedLabStage == 2, $"after board 1: stage {gm.AutomationLabStage} saved {GameManager.SavedLabStage}");

        // 스킵은 다음 판으로(충전·광고는 UI가 처리).
        Check(failures, gm.LoadNextStageImmediate() && gm.AutomationLabStage == 3, $"skip: stage {gm.AutomationLabStage}");
        yield return Settle(gm, ui);

        // 한붓 변주 판: 두 칸 가다 손을 떼면 실패 → 하트 1개가 줄고 같은 판을 다시 한다. 한 획으로 깨면 하트가 다시 찬다.
        yield return EnterLabAt(gm, ui, stageBefore, oneStrokeStage, failures);
        Check(failures, gm.AutomationLabVariant == GameManager.LabVariant.OneStroke, $"stage {oneStrokeStage} variant {gm.AutomationLabVariant}");
        int heartsBefore = GetHearts(ui);
        List<Vector2Int> moves = gm.AutomationLabSolution;
        gm.AutomationBeginDrag();
        gm.AutomationStep(moves[0].x, moves[0].y);
        gm.AutomationStep(moves[1].x, moves[1].y);
        gm.AutomationEndDrag();
        Check(failures, gm.AutomationGameOverPlaying, "lifting in the one-stroke variant did not fail");
        float failWait = Time.realtimeSinceStartup;
        while (gm.AutomationGameOverPlaying && Time.realtimeSinceStartup - failWait < 8f)
            yield return null;
        Check(failures, !gm.AutomationGameOverPlaying, "fail sequence did not finish");
        Check(failures, GetHearts(ui) == heartsBefore - 1, $"hearts {heartsBefore} -> {GetHearts(ui)} after a lab fail (expected -1)");
        Check(failures, gm.IsLabActive && gm.AutomationLabStage == oneStrokeStage, $"after fail: lab {gm.IsLabActive} stage {gm.AutomationLabStage}");
        yield return Settle(gm, ui);
        yield return PlayLabBoard(gm, failures);
        yield return WaitForLabStage(gm, oneStrokeStage + 1);
        Check(failures, gm.AutomationLabStage == oneStrokeStage + 1, $"one-stroke board not cleared: stage {gm.AutomationLabStage}");
        Check(failures, GetHearts(ui) == 5, $"hearts after a lab clear = {GetHearts(ui)} (expected full)");

        // 기억 변주 판: 잠시 뒤 숫자가 가려지고, 가려진 채로 정답을 따라가면 클리어.
        yield return EnterLabAt(gm, ui, stageBefore, memoryStage, failures);
        Check(failures, gm.AutomationLabVariant == GameManager.LabVariant.Memory && !gm.AutomationLabMemoryHidden, $"stage {memoryStage} variant {gm.AutomationLabVariant}");
        yield return Wait(3.4f);
        Check(failures, gm.AutomationLabMemoryHidden, "memory numbers were not hidden");
        yield return PlayLabBoard(gm, failures);
        yield return WaitForLabStage(gm, memoryStage + 1);
        Check(failures, gm.AutomationLabStage == memoryStage + 1, $"memory board not cleared: stage {gm.AutomationLabStage}");

        // 지도에서 하던 판을 고르면 연구소를 나온다. 스테이지 진행도는 그대로이고 연구소 번호는 남는다.
        yield return Settle(gm, ui);
        Check(failures, gm.LoadStageFromMap(stageBefore), "leaving the lab through the map failed");
        yield return Settle(gm, ui);
        Check(failures, !gm.IsLabActive && gm.CurrentStageIndex == stageBefore && gm.StageFrontier == frontierBefore,
            $"after lab: lab={gm.IsLabActive} current {gm.CurrentStageIndex} frontier {gm.StageFrontier}");
        Check(failures, GameManager.SavedLabStage == memoryStage + 1, $"saved lab stage {GameManager.SavedLabStage}");

        // 홈 화면: 열리면 모달, 닫으면 풀린다.
        Check(failures, ui.ShowHomeScreen(false) && ui.IsModalOverlayOpen, "home screen did not open");
        ui.CloseHomeScreen();
        Check(failures, !ui.IsHomeScreenOpen, "home screen did not close");

        GameManager.AutomationSetLabUnlocked(null);
        yield return new ExitPlayMode();
        Assert.IsEmpty(failures, string.Join("\n", failures));
    }

    [UnityTest, Timeout(600000)]
    public IEnumerator StuckHelpGivesFreeHintThenFreeSkip()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
        }
        var gm = Object.FindFirstObjectByType<GameManager>();
        var ui = Object.FindFirstObjectByType<GameMainUIController>();
        Assert.IsNotNull(gm);
        Assert.IsNotNull(ui);
        yield return Wait(2f);
        var failures = new List<string>();
        gm.AutomationSetFrontier(1);
        gm.LoadStageImmediateForDebug(330);
        yield return Settle(gm, ui);
        ui.ResetHeartsForNewStage();
        const int stage = 330;

        for (int fail = 1; fail <= 4; fail++)
        {
            Check(failures, gm.AutomationForceGameOver(), $"fail {fail}: game over did not start");
            float wait = Time.realtimeSinceStartup;
            while (gm.AutomationGameOverPlaying && Time.realtimeSinceStartup - wait < 8f)
                yield return null;
            yield return Wait(0.2f);
            Check(failures, gm.StageFailStreak == fail, $"fail streak {gm.StageFailStreak} (expected {fail})");
            bool hint = ui.HasStuckHintForStage(stage);
            bool skip = ui.HasFreeSkipForStage(stage);
            if (fail == 1)
                Check(failures, !hint && !skip, "help given after a single fail");
            if (fail >= 2)
                Check(failures, hint, $"free hint missing after fail {fail}");
            if (fail == 3)
                Check(failures, !skip, "free skip given before the 4th fail");
            if (fail == 4)
                Check(failures, skip, "free skip missing after the 4th fail");
        }

        // 무료 스킵: 확인을 위해 두 번 누르면 충전을 쓰지 않고 다음 판으로.
        var skipCharges = typeof(GameMainUIController).GetField("skipCharges", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        int chargesBefore = (int)skipCharges.GetValue(ui);
        var onSkip = typeof(GameMainUIController).GetMethod("OnSkipClicked", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        onSkip.Invoke(ui, null);
        onSkip.Invoke(ui, null);
        yield return Settle(gm, ui);
        Check(failures, gm.CurrentStageIndex == stage + 1, $"free skip did not advance (stage {gm.CurrentStageIndex})");
        Check(failures, (int)skipCharges.GetValue(ui) == chargesBefore, "free skip used a skip charge");
        Check(failures, gm.StageFailStreak == 0 && !ui.HasFreeSkipForStage(stage + 1) && !ui.HasStuckHintForStage(stage + 1), "help carried over to the next stage");

        yield return new ExitPlayMode();
        Assert.IsEmpty(failures, string.Join("\n", failures));
    }

    /// <summary>
    /// 포탈 판(451): 일찍 들어가면 막힘 / 마지막에 들어가면 2페이즈 등장(클리어 아님) / 되돌리기가 페이즈를 못 넘음 /
    /// 2페이즈 힌트 / 2페이즈 실패 시 1페이즈로 / 두 페이즈를 다 풀면 클리어.
    /// </summary>
    [UnityTest, Timeout(600000)]
    public IEnumerator PortalStagePhases()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
        }
        var gm = Object.FindFirstObjectByType<GameManager>();
        var ui = Object.FindFirstObjectByType<GameMainUIController>();
        Assert.IsNotNull(gm);
        Assert.IsNotNull(ui);
        yield return Wait(2f);
        var failures = new List<string>();
        const int stage = 451;
        StageStarsStore.Clear();
        gm.AutomationSetFrontier(1);
        StageData data = StageManager.LoadStage(stage);
        Assert.IsTrue(data != null && data.HasPhase2, "stage 451 is not a portal stage");
        Check(failures, StageChallenge.KindFor(stage) == StageChallengeKind.Flawless, "portal stage challenge is not Flawless");
        List<Vector2Int> phase1 = StageManager.LoadSolutionMoves(stage, 1);
        List<Vector2Int> phase2 = StageManager.LoadSolutionMoves(stage, 2);
        Assert.IsNotNull(phase1);
        Assert.IsNotNull(phase2);
        Vector2Int portal = phase1[phase1.Count - 1];
        var start1 = new Vector2Int(data.startPoint.x, data.startPoint.y);
        var start2 = new Vector2Int(data.phase2.startPoint.x, data.phase2.startPoint.y);

        // 1) 다른 칸이 남아 있으면 포탈에 못 들어간다.
        gm.LoadStageImmediateForDebug(stage);
        yield return Settle(gm, ui);
        Check(failures, gm.AutomationPortalPhase == 1, $"phase {gm.AutomationPortalPhase} at start");
        bool early = false;
        gm.AutomationBeginDrag();
        Vector2Int at = start1;
        // 포탈 옆 칸으로 먼저 간다.
        var firstSteps = new List<Vector2Int>();
        foreach (Vector2Int n in NeighborsOf(at))
        {
            if (Mathf.Abs(n.x - portal.x) + Mathf.Abs(n.y - portal.y) == 1)
                firstSteps.Insert(0, n);
            else
                firstSteps.Add(n);
        }
        foreach (Vector2Int next in firstSteps)
        {
            if (gm.AutomationGetCount(next.x, next.y) <= 0 || next == portal)
                continue;
            if (!gm.AutomationStep(next.x, next.y))
                continue;
            at = next;
            break;
        }
        if (Mathf.Abs(at.x - portal.x) + Mathf.Abs(at.y - portal.y) == 1)
        {
            early = gm.AutomationStep(portal.x, portal.y);
            Check(failures, !early && gm.AutomationPortalPhase == 1 && !gm.AutomationPortalTransitioning, "entered the portal before clearing the other tiles");
        }
        else
        {
            failures.Add($"test setup: could not reach a tile next to the portal (at {at})");
        }
        gm.AutomationEndDrag();

        // 2) 정답대로 마지막에 들어가면 클리어가 아니라 2페이즈가 나온다. 2페이즈 시작 칸은 포탈이 있던 화면 자리에 나타난다.
        gm.LoadStageImmediateForDebug(stage);
        yield return Settle(gm, ui);
        Camera cam = Camera.main;
        Vector3? portalWorld = gm.AutomationTileWorldPosition(portal.x, portal.y);
        Vector3 portalScreen = cam != null && portalWorld.HasValue ? cam.WorldToScreenPoint(portalWorld.Value) : Vector3.zero;
        yield return PlayMoves(gm, phase1, "phase 1", failures);
        Check(failures, !gm.AutomationStageCleared, "portal entry cleared the stage");
        float wait = Time.realtimeSinceStartup;
        while (gm.AutomationPortalPhase != 2 && Time.realtimeSinceStartup - wait < 4f)
            yield return null;
        Vector3? startWorld = gm.AutomationTileWorldPosition(start2.x, start2.y);
        if (cam != null && startWorld.HasValue)
        {
            Vector3 startScreen = cam.WorldToScreenPoint(startWorld.Value);
            float gap = Vector2.Distance(startScreen, portalScreen);
            Check(failures, gap < 3f, $"phase 2 start appeared {gap:F1}px away from the portal");
        }
        while (gm.AutomationPortalTransitioning && Time.realtimeSinceStartup - wait < 6f)
            yield return null;
        Check(failures, !gm.AutomationPortalTransitioning, "portal transition did not finish");
        yield return Wait(0.3f);
        if (cam != null)
        {
            foreach (Vector3 p in gm.AutomationAllTileWorldPositions())
            {
                Vector3 v = cam.WorldToViewportPoint(p);
                if (v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f)
                {
                    failures.Add($"phase 2 tile at {p} is off screen after the camera glide (viewport {v})");
                    break;
                }
            }
        }
        Check(failures, gm.AutomationPortalPhase == 2, $"phase 2 did not appear (phase {gm.AutomationPortalPhase})");
        Check(failures, gm.CurrentStageIndex == stage, $"stage changed to {gm.CurrentStageIndex}");
        Check(failures, gm.AutomationCurrentCell == start2, $"phase 2 start {gm.AutomationCurrentCell} (expected {start2})");
        Check(failures, gm.AutomationHintAvailableNow(), "no hint in phase 2");

        // 3) 되돌리기는 1페이즈로 넘어가지 못한다.
        bool undone = gm.TryUndoLastMove();
        Check(failures, !undone && gm.AutomationPortalPhase == 2, "undo went back across the portal");

        // 4) 2페이즈에서 실패하면 1페이즈부터 다시 한다.
        Check(failures, gm.AutomationForceGameOver(), "phase 2 game over did not start");
        wait = Time.realtimeSinceStartup;
        while (gm.AutomationGameOverPlaying && Time.realtimeSinceStartup - wait < 8f)
            yield return null;
        yield return Wait(0.3f);
        Check(failures, gm.AutomationPortalPhase == 1, $"after a phase-2 fail the phase is {gm.AutomationPortalPhase}");
        Check(failures, gm.AutomationCurrentCell == start1, $"after a phase-2 fail the start is {gm.AutomationCurrentCell} (expected {start1})");
        Check(failures, gm.AutomationGetCount(portal.x, portal.y) == 1, "portal tile missing after the restart");

        // 5) 두 페이즈를 다 풀면 스테이지 클리어.
        yield return PlayMoves(gm, phase1, "phase 1 (again)", failures);
        wait = Time.realtimeSinceStartup;
        while ((gm.AutomationPortalTransitioning || gm.AutomationPortalPhase != 2) && Time.realtimeSinceStartup - wait < 4f)
            yield return null;
        yield return Wait(0.3f);
        yield return PlayMoves(gm, phase2, "phase 2", failures);
        wait = Time.realtimeSinceStartup;
        while (!gm.AutomationStageCleared && Time.realtimeSinceStartup - wait < 3f)
            yield return null;
        Check(failures, gm.AutomationStageCleared, "stage not cleared after phase 2");
        Check(failures, StageStarsStore.GetStars(stage) >= 1, $"stars {StageStarsStore.GetStars(stage)} after clearing the portal stage");
        yield return WaitForStage(gm, stage + 1);

        yield return new ExitPlayMode();
        Assert.IsEmpty(failures, string.Join("\n", failures));
    }

    private static IEnumerable<Vector2Int> NeighborsOf(Vector2Int c)
    {
        yield return c + Vector2Int.up;
        yield return c + Vector2Int.down;
        yield return c + Vector2Int.left;
        yield return c + Vector2Int.right;
    }

    /// <summary>한 번의 드래그로 moves를 따라간다(포탈 진입·클리어가 일어나면 거기서 멈춘다).</summary>
    private static IEnumerator PlayMoves(GameManager gm, List<Vector2Int> moves, string label, List<string> failures)
    {
        if (!gm.AutomationBeginDrag())
        {
            failures.Add($"{label}: drag refused at {gm.AutomationCurrentCell}");
            yield break;
        }
        for (int i = 0; i < moves.Count; i++)
        {
            // 포탈 진입·클리어 순간에는 드래그가 끝나므로 이동이 거부된 것처럼 보일 수 있다: 상태로 먼저 판단한다.
            bool stepped = gm.AutomationStep(moves[i].x, moves[i].y);
            if (!stepped && !gm.AutomationStageCleared && !gm.AutomationPortalTransitioning)
            {
                failures.Add($"{label}: step {i} to {moves[i]} refused");
                yield break;
            }
            if (gm.AutomationStageCleared || gm.AutomationPortalTransitioning)
            {
                if (i != moves.Count - 1)
                    failures.Add($"{label}: ended early at step {i}");
                yield break;
            }
            float quiet = Time.realtimeSinceStartup;
            while (gm.AutomationHasPendingMutations && Time.realtimeSinceStartup - quiet < 1f)
                yield return null;
        }
        gm.AutomationEndDrag();
    }

    /// <summary>연구소를 나갔다가 저장된 판 번호를 labStage로 바꿔 다시 들어간다.</summary>
    private static IEnumerator EnterLabAt(GameManager gm, GameMainUIController ui, int stageToReturnTo, int labStage, List<string> failures)
    {
        yield return Settle(gm, ui);
        if (gm.IsLabActive && !gm.LoadStageFromMap(stageToReturnTo))
            failures.Add("could not leave the lab");
        yield return Settle(gm, ui);
        PlayerPrefs.SetInt("ZeroStep.LabStage", labStage);
        if (!gm.StartLab() || gm.AutomationLabStage != labStage)
            failures.Add($"could not enter the lab at stage {labStage} (got {gm.AutomationLabStage})");
        yield return Settle(gm, ui);
    }

    private static int GetHearts(GameMainUIController ui)
    {
        var field = typeof(GameMainUIController).GetField("currentHearts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field != null ? (int)field.GetValue(ui) : -1;
    }

    private static IEnumerator PlayLabBoard(GameManager gm, List<string> failures)
    {
        List<Vector2Int> moves = gm.AutomationLabSolution;
        if (moves == null)
        {
            failures.Add("lab board has no solution");
            yield break;
        }
        int stage = gm.AutomationLabStage;
        gm.AutomationBeginDrag();
        for (int i = 0; i < moves.Count && !gm.AutomationStageCleared; i++)
        {
            if (!gm.AutomationStep(moves[i].x, moves[i].y) && !gm.AutomationStageCleared)
            {
                failures.Add($"lab stage {stage}: step {i} refused");
                yield break;
            }
            float quiet = Time.realtimeSinceStartup;
            while (gm.AutomationHasPendingMutations && Time.realtimeSinceStartup - quiet < 1f)
                yield return null;
        }
        if (!gm.AutomationStageCleared)
            failures.Add($"lab stage {stage}: not cleared");
    }

    private static IEnumerator WaitForLabStage(GameManager gm, int labStage)
    {
        float start = Time.realtimeSinceStartup;
        while ((gm.AutomationLabStage != labStage || gm.AutomationStageCleared || gm.AutomationGameOverPlaying) && Time.realtimeSinceStartup - start < 6f)
            yield return null;
    }

    private static void Check(List<string> failures, bool condition, string message)
    {
        if (!condition)
            failures.Add(message);
    }

    private static IEnumerator PlaySolution(GameManager gm, int stage, List<string> failures)
    {
        if (gm.CurrentStageIndex != stage)
        {
            failures.Add($"expected to be on stage {stage} but on {gm.CurrentStageIndex}");
            yield break;
        }
        List<Vector2Int> moves = StageManager.LoadSolutionMoves(stage);
        gm.AutomationBeginDrag();
        for (int i = 0; i < moves.Count && !gm.AutomationStageCleared; i++)
        {
            if (!gm.AutomationStep(moves[i].x, moves[i].y) && !gm.AutomationStageCleared)
            {
                failures.Add($"stage {stage}: step {i} refused");
                yield break;
            }
            float quiet = Time.realtimeSinceStartup;
            while (gm.AutomationHasPendingMutations && Time.realtimeSinceStartup - quiet < 1f)
                yield return null;
        }
        float wait = Time.realtimeSinceStartup;
        while (!gm.AutomationStageCleared && Time.realtimeSinceStartup - wait < 3f)
            yield return null;
        if (!gm.AutomationStageCleared)
            failures.Add($"stage {stage}: not cleared");
    }

    private static IEnumerator WaitForStage(GameManager gm, int stage)
    {
        float start = Time.realtimeSinceStartup;
        while ((gm.CurrentStageIndex != stage || gm.AutomationStageCleared || gm.AutomationFeverActive) && Time.realtimeSinceStartup - start < 6f)
            yield return null;
    }

    private static IEnumerator Settle(GameManager gm, GameMainUIController ui)
    {
        yield return Wait(0.4f);
        float start = Time.realtimeSinceStartup;
        while (gm.AutomationGameOverPlaying && Time.realtimeSinceStartup - start < 3f)
            yield return null;
        if (ui != null && ui.IsTutorialPopupOpen)
        {
            var close = typeof(GameMainUIController).GetMethod("CloseTutorialPopup", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            close?.Invoke(ui, null);
        }
        yield return Wait(0.2f);
    }

    private static IEnumerator Wait(float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds)
            yield return null;
    }
}
