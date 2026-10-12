using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

/// <summary>
/// 실제 게임 규칙 코드(GameManager)로 스테이지 해답을 재생해 클리어 여부와 힌트 가능 여부를 검증한다.
/// 배치 모드 실행 예:
///   Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter StageReplayTests
///   환경 변수 ZEROSTEP_SOLUTIONS(해답 파일), ZEROSTEP_REPORT(결과 CSV), ZEROSTEP_STAGES("1-1000"), ZEROSTEP_HINT_CHECK(1/0)
/// 해답 파일 한 줄 형식: N|x,y;x,y;...|x,y;...  ('|'로 나뉜 각 구간 = 손가락을 떼지 않는 한 번의 드래그)
/// </summary>
public class StageReplayTests
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const float StepRetrySeconds = 1.5f;
    private const float ClearWaitSeconds = 3f;

    [UnityTest, Timeout(14400000)]
    public IEnumerator ReplayStageSolutionsInEngine()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ZEROSTEP_SOLUTIONS")))
            Assert.Ignore("ZEROSTEP_SOLUTIONS not set");

        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
        }

        // 플레이 모드 진입 시 도메인이 다시 로드되므로 설정은 진입 후에 읽는다.
        string solutionsPath = Environment.GetEnvironmentVariable("ZEROSTEP_SOLUTIONS");
        Assert.IsTrue(File.Exists(solutionsPath), $"solutions file not found: {solutionsPath}");
        Dictionary<int, List<List<Vector2Int>>> solutions = LoadSolutions(solutionsPath);
        ParseRange(Environment.GetEnvironmentVariable("ZEROSTEP_STAGES"), out int first, out int last);
        bool hintCheck = Environment.GetEnvironmentVariable("ZEROSTEP_HINT_CHECK") != "0";
        string reportPath = Environment.GetEnvironmentVariable("ZEROSTEP_REPORT");
        if (string.IsNullOrEmpty(reportPath))
            reportPath = Path.Combine(Application.temporaryCachePath, "zerostep_replay_report.csv");

        // 개발 로그마다 스택 트레이스를 만드는 비용이 커서 테스트 동안에는 끈다.
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        GameManager gm = null;
        float waitStart = Time.realtimeSinceStartup;
        while (gm == null && Time.realtimeSinceStartup - waitStart < 30f)
        {
            gm = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            yield return null;
        }
        Assert.IsNotNull(gm, "GameManager not found");
        yield return WaitRealtime(1.0f);

        var report = new StringBuilder("stage,result,detail,hint,hint_ms\n");
        int failures = 0;
        int hintMisses = 0;
        for (int stage = first; stage <= last; stage++)
        {
            if (!solutions.TryGetValue(stage, out List<List<Vector2Int>> strokes))
            {
                report.AppendLine($"{stage},skip,no_solution,,");
                continue;
            }

            float loadStart = Time.realtimeSinceStartup;
            while (gm.AutomationGameOverPlaying && Time.realtimeSinceStartup - loadStart < 10f)
                yield return null;
            if (!gm.LoadStageImmediateForDebug(stage))
            {
                report.AppendLine($"{stage},fail,load_failed,,");
                failures++;
                continue;
            }
            yield return WaitRealtime(0.15f);
            yield return WaitForQuiet(gm);
            yield return CloseBlockingOverlays();

            string hintResult = "";
            long hintMs = 0;
            if (hintCheck)
            {
                var sw = Stopwatch.StartNew();
                bool hint = gm.AutomationHintAvailableNow();
                hintMs = sw.ElapsedMilliseconds;
                hintResult = hint ? "ok" : "none";
                if (!hint)
                    hintMisses++;
            }

            string detail = "";
            bool ok = true;
            for (int s = 0; s < strokes.Count && ok; s++)
            {
                List<Vector2Int> stroke = strokes[s];
                if (stroke.Count == 0)
                    continue;
                if (gm.AutomationCurrentCell != stroke[0])
                {
                    detail = $"stroke{s}_start_mismatch:{gm.AutomationCurrentCell.x},{gm.AutomationCurrentCell.y}";
                    ok = false;
                    break;
                }
                if (!gm.AutomationBeginDrag())
                {
                    detail = $"stroke{s}_begin_refused";
                    ok = false;
                    break;
                }

                for (int i = 1; i < stroke.Count; i++)
                {
                    Vector2Int cell = stroke[i];
                    bool stepped = gm.AutomationStep(cell.x, cell.y);
                    float retryStart = Time.realtimeSinceStartup;
                    while (!stepped && !gm.AutomationStageCleared && !gm.AutomationGameOverPlaying &&
                           Time.realtimeSinceStartup - retryStart < StepRetrySeconds)
                    {
                        yield return null;
                        stepped = gm.AutomationStep(cell.x, cell.y);
                    }
                    if (gm.AutomationStageCleared)
                    {
                        if (i != stroke.Count - 1 || s != strokes.Count - 1)
                            detail = $"cleared_early_at_move{i}";
                        break;
                    }
                    if (gm.AutomationGameOverPlaying)
                    {
                        detail = $"game_over_at_stroke{s}_move{i}";
                        ok = false;
                        break;
                    }
                    if (!stepped)
                    {
                        detail = $"step_refused_at_stroke{s}_move{i}:{cell.x},{cell.y}";
                        ok = false;
                        break;
                    }
                    yield return WaitForQuiet(gm);
                    if (VerboseTrace)
                        Debug.Log($"[StageReplayTrace] stage={stage} move={i} to={cell.x},{cell.y} remaining={gm.AutomationTotalRemaining}");
                }

                if (!ok || gm.AutomationStageCleared)
                    break;
                gm.AutomationEndDrag();
                yield return WaitForQuiet(gm);
            }

            if (ok)
            {
                float clearWait = Time.realtimeSinceStartup;
                while (!gm.AutomationStageCleared && !gm.AutomationGameOverPlaying && Time.realtimeSinceStartup - clearWait < ClearWaitSeconds)
                    yield return null;
                if (!gm.AutomationStageCleared)
                {
                    ok = false;
                    detail = gm.AutomationGameOverPlaying ? "game_over_after_last_move" : $"not_cleared_remaining={gm.AutomationTotalRemaining}";
                }
            }

            if (!ok)
                failures++;
            report.AppendLine($"{stage},{(ok ? "clear" : "fail")},{detail},{hintResult},{hintMs}");

            // 클리어 후 자동 다음 스테이지 전환이 끝날 때까지 기다린다(최종 스테이지는 엔딩으로 넘어가므로 중단).
            if (ok && stage < last)
            {
                float advanceWait = Time.realtimeSinceStartup;
                while ((gm.AutomationStageCleared || gm.AutomationStageIndex == stage) && Time.realtimeSinceStartup - advanceWait < 5f)
                    yield return null;
            }

            if (stage % 50 == 0)
            {
                File.WriteAllText(reportPath, report.ToString());
                Debug.Log($"[StageReplay] progress stage={stage} failures={failures} hintMisses={hintMisses}");
            }
        }

        File.WriteAllText(reportPath, report.ToString());
        Debug.Log($"[StageReplay] done range={first}-{last} failures={failures} hintMisses={hintMisses} report={reportPath}");
        yield return new ExitPlayMode();
        Assert.AreEqual(0, failures, $"{failures} stage(s) failed to clear. See {reportPath}");
    }

    /// <summary>튜토리얼 등 모달이 열리면 게임이 드래그를 취소하므로, 실제 플레이어처럼 닫고 진행한다.</summary>
    private static IEnumerator CloseBlockingOverlays()
    {
        var ui = UnityEngine.Object.FindFirstObjectByType<GameMainUIController>();
        if (ui == null)
            yield break;

        System.Reflection.MethodInfo closeTutorial = typeof(GameMainUIController).GetMethod(
            "CloseTutorialPopup", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        float start = Time.realtimeSinceStartup;
        // 튜토리얼은 스테이지 시작 직후 약간 늦게 뜰 수 있어 잠시 지켜본다.
        while (Time.realtimeSinceStartup - start < 0.6f || ui.IsModalOverlayOpen)
        {
            if (ui.IsTutorialPopupOpen && closeTutorial != null)
                closeTutorial.Invoke(ui, null);
            if (Time.realtimeSinceStartup - start > 10f)
                break;
            yield return null;
        }
    }

    private static IEnumerator WaitForQuiet(GameManager gm)
    {
        float start = Time.realtimeSinceStartup;
        int quietFrames = 0;
        int frames = 0;
        while (Time.realtimeSinceStartup - start < 2f)
        {
            frames++;
            quietFrames = gm.AutomationHasPendingMutations ? 0 : quietFrames + 1;
            if (quietFrames >= 2)
                break;
            yield return null;
        }
        if (VerboseTrace)
            Debug.Log($"[StageReplayTrace] quiet after {frames} frames {(Time.realtimeSinceStartup - start) * 1000f:F0}ms pending={gm.AutomationHasPendingMutations} t={Time.time:F3} ts={Time.timeScale}");
    }

    private static bool VerboseTrace => Environment.GetEnvironmentVariable("ZEROSTEP_TRACE") == "1";

    private static IEnumerator WaitRealtime(float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds)
            yield return null;
    }

    private static void ParseRange(string text, out int first, out int last)
    {
        first = 1;
        last = 1000;
        if (string.IsNullOrEmpty(text))
            return;
        string[] parts = text.Split('-');
        int.TryParse(parts[0], out first);
        last = parts.Length > 1 && int.TryParse(parts[1], out int l) ? l : first;
    }

    private static Dictionary<int, List<List<Vector2Int>>> LoadSolutions(string path)
    {
        var result = new Dictionary<int, List<List<Vector2Int>>>();
        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;
            string[] segments = line.Split('|');
            if (!int.TryParse(segments[0], out int stage))
                continue;
            var strokes = new List<List<Vector2Int>>();
            for (int i = 1; i < segments.Length; i++)
            {
                var stroke = new List<Vector2Int>();
                foreach (string cell in segments[i].Split(';'))
                {
                    string[] xy = cell.Split(',');
                    if (xy.Length == 2 && int.TryParse(xy[0], out int x) && int.TryParse(xy[1], out int y))
                        stroke.Add(new Vector2Int(x, y));
                }
                strokes.Add(stroke);
            }
            result[stage] = strokes;
        }
        return result;
    }
}
