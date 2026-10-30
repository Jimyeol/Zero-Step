using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 즉석 보드 생성기 검증.
/// 1) 시드 수천 개로 만든 보드가 기본 규칙 모델로 항상 풀리는지, 같은 시드면 같은 보드인지.
/// 2) 실제 GameManager 규칙 코드로 피버 보드 정답을 재생해 매번 클리어되어 다음 판으로 넘어가는지.
/// 배치 모드: Unity -batchmode -runTests -testPlatform EditMode -testFilter BoardGeneratorTests
/// </summary>
public class BoardGeneratorTests
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [Test]
    public void GeneratedBoardsAreAlwaysSolvable()
    {
        var presets = new[] { BoardGenerator.Settings.Small, BoardGenerator.Settings.Medium, BoardGenerator.Settings.Large, BoardGenerator.Settings.Fever };
        var failures = new List<string>();
        int underfilled = 0;
        for (int p = 0; p < presets.Length; p++)
        {
            BoardGenerator.Settings settings = presets[p];
            for (int seed = 1; seed <= 750; seed++)
            {
                BoardGenerator.Result result = BoardGenerator.Generate(settings, seed);
                if (result == null)
                {
                    failures.Add($"preset {p} seed {seed}: no board");
                    continue;
                }
                if (!BoardGenerator.Verify(result.stage, result.solution, out string error))
                    failures.Add($"preset {p} seed {seed}: {error}");
                if (result.stage.width > settings.maxWidth || result.stage.height > settings.maxHeight)
                    failures.Add($"preset {p} seed {seed}: board {result.stage.width}x{result.stage.height} too large");
                foreach (CellData cell in result.stage.cells)
                {
                    if (cell.count < 1 || cell.count > settings.maxCount)
                        failures.Add($"preset {p} seed {seed}: cell count {cell.count}");
                }
                if (result.stage.cells.Length < settings.minCells)
                    underfilled++;
            }
        }
        Debug.Log($"[BoardGenerator] underfilled boards: {underfilled} / 3000");
        Assert.IsEmpty(failures, string.Join("\n", failures.GetRange(0, Mathf.Min(20, failures.Count))));
        Assert.Less(underfilled, 30, "too many boards below minCells");
    }

    [Test]
    public void SameSeedGivesSameBoard()
    {
        BoardGenerator.Result a = BoardGenerator.Generate(BoardGenerator.Settings.Medium, 4242);
        BoardGenerator.Result b = BoardGenerator.Generate(BoardGenerator.Settings.Medium, 4242);
        Assert.AreEqual(JsonUtility.ToJson(a.stage), JsonUtility.ToJson(b.stage));
        Assert.AreEqual(a.solution, b.solution);
    }

    [UnityTest, Timeout(600000)]
    public IEnumerator FeverBoardsClearInEngine()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
        }
        var gm = Object.FindFirstObjectByType<GameManager>();
        Assert.IsNotNull(gm);
        float wait = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - wait < 2f)
            yield return null;

        const int boardsToPlay = 12;
        gm.AutomationStartFever(10, 600f);
        var failures = new List<string>();
        for (int board = 0; board < boardsToPlay; board++)
        {
            int boardsBefore = gm.AutomationFeverBoards;
            List<Vector2Int> moves = gm.AutomationFeverSolution;
            Assert.IsNotNull(moves);
            int popsBefore = gm.AutomationFeverPops;
            gm.AutomationBeginDrag();
            for (int i = 0; i < moves.Count && !gm.AutomationStageCleared; i++)
            {
                if (!gm.AutomationStep(moves[i].x, moves[i].y) && !gm.AutomationStageCleared)
                {
                    failures.Add($"board {board}: step {i} refused");
                    break;
                }
                float quiet = Time.realtimeSinceStartup;
                while (gm.AutomationHasPendingMutations && Time.realtimeSinceStartup - quiet < 1f)
                    yield return null;
            }
            float clearWait = Time.realtimeSinceStartup;
            while (gm.AutomationFeverBoards == boardsBefore && Time.realtimeSinceStartup - clearWait < 3f)
                yield return null;
            if (gm.AutomationFeverBoards == boardsBefore)
            {
                failures.Add($"board {board}: did not clear into the next board");
                break;
            }
            if (gm.AutomationFeverPops <= popsBefore)
                failures.Add($"board {board}: no pops counted");
            float settle = Time.realtimeSinceStartup;
            while (gm.AutomationGameOverPlaying && Time.realtimeSinceStartup - settle < 2f)
                yield return null;
        }
        Debug.Log($"[BoardGenerator] engine fever boards={gm.AutomationFeverBoards} pops={gm.AutomationFeverPops}");
        Assert.IsTrue(gm.AutomationFeverActive, "fever ended early");
        yield return new ExitPlayMode();
        Assert.IsEmpty(failures, string.Join("\n", failures));
    }
}
