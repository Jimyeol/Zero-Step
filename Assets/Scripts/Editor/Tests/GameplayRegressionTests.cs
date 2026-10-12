using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 실제 GameManager로 되돌리기·실패 복귀·리셋 하트 규칙을 확인하는 회귀 테스트.
///   ZEROSTEP_UNDO_STAGES : 되돌리기 검증 스테이지(쉼표), 기본 "101,116,141,171,231,702"
///   ZEROSTEP_LOSING_PATHS : 지는 경로 파일(N|x,y;...)
/// </summary>
public class GameplayRegressionTests
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [UnityTest, Timeout(1800000)]
    public IEnumerator UndoFailureAndResetRules()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
        }

        // 개발 로그마다 스택 트레이스를 만드는 비용이 커서 테스트 동안에는 끈다.
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        GameManager gm = null;
        float waitStart = Time.realtimeSinceStartup;
        while (gm == null && Time.realtimeSinceStartup - waitStart < 30f)
        {
            gm = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            yield return null;
        }
        Assert.IsNotNull(gm);
        var ui = UnityEngine.Object.FindFirstObjectByType<GameMainUIController>();
        float splashWait = Time.realtimeSinceStartup;
        while (ui != null && ui.IsSplashActive && Time.realtimeSinceStartup - splashWait < 15f)
            yield return null;

        var failures = new List<string>();

        // 1) 되돌리기: 몇 수 둔 뒤 끝까지 되돌리면 처음 상태와 같아야 하고, 그 뒤 정답으로 클리어돼야 한다.
        string undoStages = Environment.GetEnvironmentVariable("ZEROSTEP_UNDO_STAGES");
        if (string.IsNullOrEmpty(undoStages))
            undoStages = "101,116,141,171,231,702";
        foreach (string token in undoStages.Split(','))
        {
            if (!int.TryParse(token.Trim(), out int stage))
                continue;
            yield return LoadStage(gm, ui, stage);
            List<Vector2Int> moves = StageManager.LoadSolutionMoves(stage);
            if (moves == null || moves.Count < 2)
            {
                failures.Add($"undo stage {stage}: no solution");
                continue;
            }

            Dictionary<Vector2Int, int> initial = ReadCounts(gm, stage);
            Vector2Int startCell = gm.AutomationCurrentCell;
            int prefix = Mathf.Min(6, moves.Count - 1);
            gm.AutomationBeginDrag();
            for (int i = 0; i < prefix; i++)
            {
                bool stepped = gm.AutomationStep(moves[i].x, moves[i].y);
                yield return WaitQuiet(gm);
                if (!stepped)
                {
                    failures.Add($"undo stage {stage}: prefix step {i} refused");
                    break;
                }
            }
            gm.AutomationEndDrag();
            yield return WaitQuiet(gm);

            int undone = 0;
            while (gm.CanUndo && undone < 64)
            {
                Assert.IsTrue(gm.TryUndoLastMove());
                undone++;
                yield return WaitQuiet(gm);
            }

            Dictionary<Vector2Int, int> after = ReadCounts(gm, stage);
            foreach (KeyValuePair<Vector2Int, int> kv in initial)
            {
                if (!after.TryGetValue(kv.Key, out int now) || now != kv.Value)
                    failures.Add($"undo stage {stage}: cell {kv.Key} expected {kv.Value} got {now}");
            }
            if (gm.AutomationCurrentCell != startCell)
                failures.Add($"undo stage {stage}: head {gm.AutomationCurrentCell} expected {startCell}");
            if (undone != prefix)
                failures.Add($"undo stage {stage}: undone {undone} steps, expected {prefix}");

            // 되돌린 뒤 정답 전체 재생 → 클리어. 최종 스테이지는 클리어하면 엔딩 화면이 열려 뒤 테스트를 막으므로 생략한다.
            if (FinalEndingProgressStore.IsFinalStageIndex(stage))
                continue;
            gm.AutomationBeginDrag();
            for (int i = 0; i < moves.Count && !gm.AutomationStageCleared; i++)
            {
                // 마지막 이동으로 클리어되면 경로가 바로 비워져 false가 나올 수 있으므로 클리어 여부로 구분한다.
                if (!gm.AutomationStep(moves[i].x, moves[i].y) && !gm.AutomationStageCleared)
                {
                    failures.Add($"undo stage {stage}: replay step {i} refused after undo");
                    break;
                }
                yield return WaitQuiet(gm);
            }
            float clearWait = Time.realtimeSinceStartup;
            while (!gm.AutomationStageCleared && Time.realtimeSinceStartup - clearWait < 3f)
                yield return null;
            if (!gm.AutomationStageCleared)
                failures.Add($"undo stage {stage}: not cleared after undo + replay");
            yield return WaitRealtime(1.2f);
        }

        // 2) 실패 후 다시 플레이 가능해질 때까지의 시간, 하트 차감
        string losingPath = Environment.GetEnvironmentVariable("ZEROSTEP_LOSING_PATHS");
        if (!string.IsNullOrEmpty(losingPath) && File.Exists(losingPath))
        {
            foreach (string line in File.ReadAllLines(losingPath))
            {
                string[] parts = line.Trim().Split('|');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int stage))
                    continue;
                yield return LoadStage(gm, ui, stage);
                int heartsBefore = GetHearts(ui);
                string[] cells = parts[1].Split(';');
                gm.AutomationBeginDrag();
                for (int i = 1; i < cells.Length && !gm.AutomationGameOverPlaying; i++)
                {
                    string[] xy = cells[i].Split(',');
                    gm.AutomationStep(int.Parse(xy[0]), int.Parse(xy[1]));
                    yield return WaitQuiet(gm);
                }
                if (!gm.AutomationGameOverPlaying)
                    gm.AutomationEndDrag();

                float detect = Time.realtimeSinceStartup;
                while (!gm.AutomationGameOverPlaying && Time.realtimeSinceStartup - detect < 2f)
                    yield return null;
                if (!gm.AutomationGameOverPlaying)
                {
                    failures.Add($"failure stage {stage}: game over not triggered");
                    continue;
                }

                float start = Time.realtimeSinceStartup;
                while (gm.AutomationGameOverPlaying && Time.realtimeSinceStartup - start < 5f)
                    yield return null;
                float seconds = Time.realtimeSinceStartup - start;
                Debug.Log($"[Regression] failure stage {stage} recovery {seconds:F2}s");
                if (seconds > 1.3f)
                    failures.Add($"failure stage {stage}: recovery took {seconds:F2}s (> 1.3s)");
                int heartsAfter = GetHearts(ui);
                if (heartsAfter != heartsBefore - 1)
                    failures.Add($"failure stage {stage}: hearts {heartsBefore} -> {heartsAfter} (expected -1)");
            }
        }

        // 3) 수동 리셋은 하트를 쓰지 않는다
        yield return LoadStage(gm, ui, 30);
        int heartsBeforeReset = GetHearts(ui);
        List<Vector2Int> resetMoves = StageManager.LoadSolutionMoves(30);
        gm.AutomationBeginDrag();
        for (int i = 0; resetMoves != null && i < Mathf.Min(3, resetMoves.Count - 1); i++)
        {
            gm.AutomationStep(resetMoves[i].x, resetMoves[i].y);
            yield return WaitQuiet(gm);
        }
        gm.AutomationEndDrag();
        yield return WaitQuiet(gm);
        gm.ResetCurrentStage();
        float resetWait = Time.realtimeSinceStartup;
        while (gm.AutomationGameOverPlaying && Time.realtimeSinceStartup - resetWait < 3f)
            yield return null;
        if (GetHearts(ui) != heartsBeforeReset)
            failures.Add($"manual reset consumed a heart ({heartsBeforeReset} -> {GetHearts(ui)})");

        yield return new ExitPlayMode();
        Assert.IsEmpty(failures, string.Join("\n", failures));
    }

    private static IEnumerator LoadStage(GameManager gm, GameMainUIController ui, int stage)
    {
        float wait = Time.realtimeSinceStartup;
        while (gm.AutomationGameOverPlaying && Time.realtimeSinceStartup - wait < 10f)
            yield return null;
        gm.LoadStageImmediateForDebug(stage);
        yield return WaitRealtime(0.4f);
        float start = Time.realtimeSinceStartup;
        MethodInfo close = typeof(GameMainUIController).GetMethod("CloseTutorialPopup", BindingFlags.Instance | BindingFlags.NonPublic);
        while (Time.realtimeSinceStartup - start < 0.6f || (ui != null && ui.IsModalOverlayOpen))
        {
            if (ui != null && ui.IsTutorialPopupOpen)
                close?.Invoke(ui, null);
            if (Time.realtimeSinceStartup - start > 8f)
                break;
            yield return null;
        }
        yield return WaitQuiet(gm);
    }

    private static Dictionary<Vector2Int, int> ReadCounts(GameManager gm, int stage)
    {
        var result = new Dictionary<Vector2Int, int>();
        StageData data = StageManager.LoadStage(stage);
        foreach (CellData c in data.cells)
        {
            if (c == null || c.count <= 0)
                continue;
            result[new Vector2Int(c.x, c.y)] = gm.AutomationGetCount(c.x, c.y);
        }
        return result;
    }

    private static int GetHearts(GameMainUIController ui)
    {
        FieldInfo field = typeof(GameMainUIController).GetField("currentHearts", BindingFlags.Instance | BindingFlags.NonPublic);
        return ui != null && field != null ? (int)field.GetValue(ui) : -1;
    }

    private static IEnumerator WaitQuiet(GameManager gm)
    {
        float start = Time.realtimeSinceStartup;
        int quiet = 0;
        while (Time.realtimeSinceStartup - start < 2f)
        {
            quiet = gm.AutomationHasPendingMutations ? 0 : quiet + 1;
            if (quiet >= 3)
                yield break;
            yield return null;
        }
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds)
            yield return null;
    }
}
