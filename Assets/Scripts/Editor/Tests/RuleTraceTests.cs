using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 규칙 비교용 추적: ZEROSTEP_TRACE_PATH="N|x,y;x,y;..." 경로를 한 번의 드래그로 재생하며
/// 이동마다 수락 여부와 보드 전체 숫자를 로그로 남긴다(파이썬 규칙 모델과 비교하는 데 사용).
/// </summary>
public class RuleTraceTests
{
    [UnityTest, Timeout(600000)]
    public IEnumerator TracePath()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ZEROSTEP_TRACE_PATH")))
            Assert.Ignore("ZEROSTEP_TRACE_PATH not set");
        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            yield return new EnterPlayMode();
        }
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        string spec = Environment.GetEnvironmentVariable("ZEROSTEP_TRACE_PATH");
        GameManager gm = null;
        while (gm == null) { gm = UnityEngine.Object.FindFirstObjectByType<GameManager>(); yield return null; }
        var ui = UnityEngine.Object.FindFirstObjectByType<GameMainUIController>();
        while (ui != null && ui.IsSplashActive) yield return null;

        foreach (string line in spec.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split('|');
            int stage = int.Parse(parts[0]);
            gm.LoadStageImmediateForDebug(stage);
            float t = Time.realtimeSinceStartup;
            MethodInfo close = typeof(GameMainUIController).GetMethod("CloseTutorialPopup", BindingFlags.Instance | BindingFlags.NonPublic);
            while (Time.realtimeSinceStartup - t < 1.2f || (ui != null && ui.IsModalOverlayOpen))
            {
                if (ui != null && ui.IsTutorialPopupOpen) close?.Invoke(ui, null);
                yield return null;
            }
            StageData data = StageManager.LoadStage(stage);
            string[] cells = parts[1].Split(';');
            Debug.Log($"[RuleTrace] stage={stage} step=0 {Counts(gm, data)}");
            gm.AutomationBeginDrag();
            for (int i = 1; i < cells.Length; i++)
            {
                string[] xy = cells[i].Split(',');
                bool ok = gm.AutomationStep(int.Parse(xy[0]), int.Parse(xy[1]));
                for (int f = 0; f < 6; f++) yield return null;
                float w = Time.realtimeSinceStartup;
                while (gm.AutomationHasPendingMutations && Time.realtimeSinceStartup - w < 2f) yield return null;
                Debug.Log($"[RuleTrace] stage={stage} step={i} to={cells[i]} accepted={ok} gameOver={gm.AutomationGameOverPlaying} {Counts(gm, data)}");
                if (gm.AutomationGameOverPlaying) break;
            }
            if (!gm.AutomationGameOverPlaying)
            {
                gm.AutomationEndDrag();
                for (int f = 0; f < 10; f++) yield return null;
                Debug.Log($"[RuleTrace] stage={stage} released gameOver={gm.AutomationGameOverPlaying} cur={gm.AutomationCurrentCell}");
            }
            float g = Time.realtimeSinceStartup;
            while (gm.AutomationGameOverPlaying && Time.realtimeSinceStartup - g < 5f) yield return null;
        }
        yield return new ExitPlayMode();
    }

    private static string Counts(GameManager gm, StageData data)
    {
        var sb = new StringBuilder();
        foreach (CellData c in data.cells)
            if (c != null && c.count > 0)
                sb.Append($"{c.x},{c.y}={gm.AutomationGetCount(c.x, c.y)} ");
        return sb.ToString();
    }
}
