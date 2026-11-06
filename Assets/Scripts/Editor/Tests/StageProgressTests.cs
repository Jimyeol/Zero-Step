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
