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
    public IEnumerator LabVariantsStreakAndExit()
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
        var failures = new List<string>();
        PlayerPrefs.DeleteKey("ZeroStep.LabDay");
        PlayerPrefs.DeleteKey("ZeroStep.LabBest");
        yield return Settle(gm, ui);
        int stageBefore = gm.CurrentStageIndex;
        int frontierBefore = gm.StageFrontier;

        Check(failures, gm.StartLab() && gm.IsLabActive, "lab did not start");
        yield return Settle(gm, ui);

        // 0번 판: 기본 → 클리어하면 연속 1, 다음 판으로.
        Check(failures, gm.AutomationLabVariant == GameManager.LabVariant.Normal, $"board 0 variant {gm.AutomationLabVariant}");
        yield return PlayLabBoard(gm, failures);
        yield return WaitForLabBoard(gm, 1);
        Check(failures, gm.AutomationLabBoardIndex == 1 && gm.AutomationLabStreak == 1, $"after board 0: index {gm.AutomationLabBoardIndex} streak {gm.AutomationLabStreak}");

        // 1번 판: 한붓 → 두 칸 가다 손을 떼면 실패(같은 판 다시, 연속 0). 다시 한 획으로 깨면 통과.
        Check(failures, gm.AutomationLabVariant == GameManager.LabVariant.OneStroke, $"board 1 variant {gm.AutomationLabVariant}");
        List<Vector2Int> moves = gm.AutomationLabSolution;
        gm.AutomationBeginDrag();
        gm.AutomationStep(moves[0].x, moves[0].y);
        gm.AutomationStep(moves[1].x, moves[1].y);
        gm.AutomationEndDrag();
        Check(failures, gm.AutomationLabStreak == 0 && gm.AutomationGameOverPlaying, "lifting in one-stroke variant did not fail");
        yield return Wait(1.2f);
        Check(failures, gm.AutomationLabBoardIndex == 1 && !gm.AutomationGameOverPlaying, $"one-stroke retry: index {gm.AutomationLabBoardIndex}");
        yield return PlayLabBoard(gm, failures);
        yield return WaitForLabBoard(gm, 2);
        Check(failures, gm.AutomationLabStreak == 1, $"after one-stroke board: streak {gm.AutomationLabStreak}");

        // 2번 판: 무거운 판.
        Check(failures, gm.AutomationLabVariant == GameManager.LabVariant.Heavy, $"board 2 variant {gm.AutomationLabVariant}");
        yield return PlayLabBoard(gm, failures);
        yield return WaitForLabBoard(gm, 3);

        // 3번 판: 기억 → 잠시 뒤 숫자가 가려진다. 가려진 채로 정답을 따라가면 클리어.
        Check(failures, gm.AutomationLabVariant == GameManager.LabVariant.Memory, $"board 3 variant {gm.AutomationLabVariant}");
        Check(failures, !gm.AutomationLabMemoryHidden, "memory numbers hidden too early");
        yield return Wait(3.4f);
        Check(failures, gm.AutomationLabMemoryHidden, "memory numbers were not hidden");
        yield return PlayLabBoard(gm, failures);
        yield return WaitForLabBoard(gm, 4);
        Check(failures, gm.AutomationLabStreak == 3 && GameManager.LabBestStreak == 3, $"streak {gm.AutomationLabStreak} best {GameManager.LabBestStreak}");

        // 스킵 = 다음 판, 연속 0.
        Check(failures, gm.LabSkipBoard(), "lab skip refused");
        yield return Settle(gm, ui);
        Check(failures, gm.AutomationLabBoardIndex == 5 && gm.AutomationLabStreak == 0, $"after skip: index {gm.AutomationLabBoardIndex} streak {gm.AutomationLabStreak}");

        // 지도에서 하던 판을 고르면 연구소를 나온다. 스테이지 진행도는 그대로.
        Check(failures, gm.LoadStageFromMap(stageBefore), "leaving the lab through the map failed");
        yield return Settle(gm, ui);
        Check(failures, !gm.IsLabActive && gm.CurrentStageIndex == stageBefore && gm.StageFrontier == frontierBefore,
            $"after lab: lab={gm.IsLabActive} current {gm.CurrentStageIndex} frontier {gm.StageFrontier}");

        yield return new ExitPlayMode();
        Assert.IsEmpty(failures, string.Join("\n", failures));
    }

    private static IEnumerator PlayLabBoard(GameManager gm, List<string> failures)
    {
        List<Vector2Int> moves = gm.AutomationLabSolution;
        if (moves == null)
        {
            failures.Add("lab board has no solution");
            yield break;
        }
        int index = gm.AutomationLabBoardIndex;
        gm.AutomationBeginDrag();
        for (int i = 0; i < moves.Count && !gm.AutomationStageCleared; i++)
        {
            if (!gm.AutomationStep(moves[i].x, moves[i].y) && !gm.AutomationStageCleared)
            {
                failures.Add($"lab board {index}: step {i} refused");
                yield break;
            }
            float quiet = Time.realtimeSinceStartup;
            while (gm.AutomationHasPendingMutations && Time.realtimeSinceStartup - quiet < 1f)
                yield return null;
        }
        if (!gm.AutomationStageCleared)
            failures.Add($"lab board {index}: not cleared");
    }

    private static IEnumerator WaitForLabBoard(GameManager gm, int boardIndex)
    {
        float start = Time.realtimeSinceStartup;
        while ((gm.AutomationLabBoardIndex != boardIndex || gm.AutomationStageCleared || gm.AutomationGameOverPlaying) && Time.realtimeSinceStartup - start < 5f)
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
