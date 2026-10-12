using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 스테이지 데이터 정적 검증기. 메뉴(ZeroStep/Validate Stage Data)로 실행하거나, 빌드 직전에 자동 실행된다.
/// 구조·규칙상 절대 성립할 수 없는 데이터(예: FixedKnot 홀짝 불일치)를 막는다.
/// 실제 풀이 가능 여부는 StageReplayTests(실제 엔진으로 정답 재생)로 확인한다.
/// </summary>
public sealed class StageDataValidator : IPreprocessBuildWithReport
{
    public const int FinalStageIndex = 1000;
    private const string StagesFolder = "Assets/Resources/Stages";
    private const string SolutionsPath = "Assets/Resources/Stages/solutions.txt";

    private static readonly HashSet<string> KnownTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Normal", "CrossBlast", "ShortCircuit", "FixedKnot", "TwinLink", "Hidden", "Igniter", "BlindCurtain", "Blackout", "Charge"
    };

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Result result = Validate();
        if (result.Errors.Count > 0)
            throw new BuildFailedException($"[StageDataValidator] 스테이지 데이터 오류 {result.Errors.Count}건. 첫 오류: {result.Errors[0]}");
        Debug.Log($"[StageDataValidator] OK stages={result.StageCount} warnings={result.Warnings.Count}");
    }

    [MenuItem("ZeroStep/Validate Stage Data")]
    public static void ValidateFromMenu()
    {
        Result result = Validate();
        var sb = new StringBuilder();
        sb.AppendLine($"Stages: {result.StageCount}, Errors: {result.Errors.Count}, Warnings: {result.Warnings.Count}");
        foreach (string e in result.Errors.Take(60)) sb.AppendLine("ERROR " + e);
        foreach (string w in result.Warnings.Take(40)) sb.AppendLine("WARN  " + w);
        if (result.Errors.Count > 0)
            Debug.LogError(sb.ToString());
        else
            Debug.Log(sb.ToString());
    }

    /// <summary>배치 모드용: -executeMethod StageDataValidator.ValidateBatch (오류가 있으면 종료 코드 1)</summary>
    public static void ValidateBatch()
    {
        Result result = Validate();
        foreach (string e in result.Errors) Debug.LogError("[StageDataValidator] " + e);
        foreach (string w in result.Warnings) Debug.LogWarning("[StageDataValidator] " + w);
        Debug.Log($"[StageDataValidator] done stages={result.StageCount} errors={result.Errors.Count} warnings={result.Warnings.Count}");
        EditorApplication.Exit(result.Errors.Count > 0 ? 1 : 0);
    }

    public sealed class Result
    {
        public int StageCount;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
    }

    public static Result Validate()
    {
        var result = new Result();
        Dictionary<int, string> solutionLines = LoadSolutionLines();
        if (solutionLines == null)
            result.Errors.Add($"{SolutionsPath} 없음 (힌트 즉시 표시·재생 검증에 필요)");

        for (int n = 1; n <= FinalStageIndex; n++)
        {
            string path = $"{StagesFolder}/stage_{n}.json";
            if (!File.Exists(path))
            {
                result.Errors.Add($"stage {n}: 파일 없음 ({path})");
                continue;
            }

            StageData data;
            try
            {
                data = JsonUtility.FromJson<StageData>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                result.Errors.Add($"stage {n}: JSON 파싱 실패 {ex.Message}");
                continue;
            }

            result.StageCount++;
            if (File.ReadAllText(path).Contains("\"walls\""))
                result.Errors.Add($"stage {n}: 벽(walls)은 더 이상 지원하지 않습니다");
            ValidateStage(n, data, result);
            if (solutionLines != null)
                ValidateSolution(n, data, solutionLines, result);
        }

        if (File.Exists($"{StagesFolder}/stage_{FinalStageIndex + 1}.json"))
            result.Warnings.Add($"stage {FinalStageIndex + 1} 파일이 있지만 최종 스테이지는 {FinalStageIndex}입니다.");
        return result;
    }

    private static void ValidateStage(int n, StageData d, Result r)
    {
        string p = $"stage {n}: ";
        if (d == null || d.cells == null || d.startPoint == null)
        {
            r.Errors.Add(p + "cells/startPoint 없음");
            return;
        }
        if (d.stageID != n)
            r.Warnings.Add(p + $"stageID({d.stageID})가 파일 번호와 다름");
        if (d.width < 1 || d.height < 1 || d.width > 10 || d.height > 10)
            r.Errors.Add(p + $"보드 크기 이상 {d.width}x{d.height}");

        var cells = new Dictionary<Vector2Int, CellData>();
        foreach (CellData c in d.cells)
        {
            if (c == null || c.count <= 0)
                continue;
            var pos = new Vector2Int(c.x, c.y);
            if (c.x < 0 || c.y < 0 || c.x >= d.width || c.y >= d.height)
                r.Errors.Add(p + $"칸 ({c.x},{c.y})이 보드 밖");
            if (cells.ContainsKey(pos))
                r.Errors.Add(p + $"칸 ({c.x},{c.y}) 중복");
            cells[pos] = c;
            string type = string.IsNullOrEmpty(c.type) ? "Normal" : c.type;
            if (!KnownTypes.Contains(type))
                r.Errors.Add(p + $"알 수 없는 타입 {type} ({c.x},{c.y})");
        }

        var start = new Vector2Int(d.startPoint.x, d.startPoint.y);
        if (!cells.TryGetValue(start, out CellData startCell))
            r.Errors.Add(p + "시작 칸에 타일이 없음");
        else if (startCell.type == "Hidden")
            r.Errors.Add(p + "시작 칸이 Hidden");

        var hiddenGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var igniterTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var twinGroups = new Dictionary<int, int>();
        foreach (CellData c in cells.Values)
        {
            var pos = new Vector2Int(c.x, c.y);
            switch (c.type)
            {
                case "ShortCircuit":
                {
                    Vector2Int dir = DirectionOf(c.direction, out bool ok);
                    if (!ok)
                        r.Errors.Add(p + $"ShortCircuit 방향 이상 '{c.direction}' ({c.x},{c.y})");
                    else if (!cells.ContainsKey(pos + dir) && c.count > 1)
                        r.Errors.Add(p + $"ShortCircuit ({c.x},{c.y}) 출구 칸이 비어 있는데 count>1 — 다시 나갈 수 없음");
                    break;
                }
                case "FixedKnot":
                {
                    if (c.targetOrder < 1)
                        r.Errors.Add(p + $"FixedKnot ({c.x},{c.y}) targetOrder < 1");
                    int distance = Mathf.Abs(c.x - start.x) + Mathf.Abs(c.y - start.y);
                    if ((distance & 1) != (c.targetOrder & 1))
                        r.Errors.Add(p + $"FixedKnot ({c.x},{c.y}) targetOrder {c.targetOrder} 홀짝 불일치(시작점 거리 {distance}) — 절대 진입 불가");
                    break;
                }
                case "TwinLink":
                    twinGroups[c.linkID] = twinGroups.TryGetValue(c.linkID, out int k) ? k + 1 : 1;
                    break;
                case "Hidden":
                    hiddenGroups.Add(string.IsNullOrEmpty(c.groupID) ? "default" : c.groupID);
                    break;
                case "Igniter":
                    if (string.IsNullOrEmpty(c.targetID))
                        r.Errors.Add(p + $"Igniter ({c.x},{c.y}) targetID 없음");
                    else
                        igniterTargets.Add(c.targetID);
                    break;
                case "Charge":
                    foreach (Vector2Int dir in Directions)
                    {
                        if (cells.TryGetValue(pos + dir, out CellData nb) && (nb.type == "FixedKnot" || nb.type == "Igniter" || nb.type == "Hidden"))
                            r.Warnings.Add(p + $"Charge ({c.x},{c.y}) 옆에 {nb.type} — 충전되지 않는 타일");
                        if (cells.TryGetValue(pos + dir, out CellData nc) && nc.type == "Charge")
                            r.Warnings.Add(p + $"Charge ({c.x},{c.y}) 옆에 다른 Charge — 서로 숫자를 계속 늘릴 수 있어 권장하지 않음");
                    }
                    break;
            }
        }

        foreach (string g in hiddenGroups)
            if (!igniterTargets.Contains(g))
                r.Errors.Add(p + $"Hidden 그룹 '{g}'를 여는 Igniter 없음");
        foreach (string t in igniterTargets)
            if (!hiddenGroups.Contains(t))
                r.Warnings.Add(p + $"Igniter target '{t}'에 해당하는 Hidden 없음");
        foreach (KeyValuePair<int, int> kv in twinGroups)
            if (kv.Value < 2)
                r.Warnings.Add(p + $"TwinLink linkID {kv.Key} 타일이 1개뿐");
    }

    private static void ValidateSolution(int n, StageData d, Dictionary<int, string> lines, Result r)
    {
        if (!lines.TryGetValue(n, out string text))
        {
            r.Errors.Add($"stage {n}: solutions.txt에 정답 없음");
            return;
        }

        var cells = new HashSet<Vector2Int>();
        foreach (CellData c in d.cells)
            if (c != null && c.count > 0)
                cells.Add(new Vector2Int(c.x, c.y));

        Vector2Int current = new Vector2Int(d.startPoint.x, d.startPoint.y);
        string[] strokes = text.Split('|');
        for (int s = 0; s < strokes.Length; s++)
        {
            string[] parts = strokes[s].Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string[] xy = parts[i].Split(',');
                if (xy.Length != 2 || !int.TryParse(xy[0], out int x) || !int.TryParse(xy[1], out int y))
                {
                    r.Errors.Add($"stage {n}: 정답 형식 오류 '{parts[i]}'");
                    return;
                }
                var cell = new Vector2Int(x, y);
                if (i == 0)
                {
                    if (cell != current)
                        r.Errors.Add($"stage {n}: 정답 {s}번째 드래그 시작 칸 불일치");
                    continue;
                }
                if (!cells.Contains(cell) || Mathf.Abs(cell.x - current.x) + Mathf.Abs(cell.y - current.y) != 1)
                {
                    r.Errors.Add($"stage {n}: 정답 이동 ({current.x},{current.y})→({x},{y})이 인접하지 않거나 보드 밖");
                    return;
                }
                current = cell;
            }
        }
    }

    private static Dictionary<int, string> LoadSolutionLines()
    {
        if (!File.Exists(SolutionsPath))
            return null;
        var result = new Dictionary<int, string>();
        foreach (string raw in File.ReadAllLines(SolutionsPath))
        {
            string line = raw.Trim();
            int bar = line.IndexOf('|');
            if (bar > 0 && int.TryParse(line.Substring(0, bar), out int n))
                result[n] = line.Substring(bar + 1);
        }
        return result;
    }

    private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    private static Vector2Int DirectionOf(string dir, out bool ok)
    {
        ok = true;
        switch ((dir ?? string.Empty).ToUpperInvariant())
        {
            case "UP": return Vector2Int.up;
            case "DOWN": return Vector2Int.down;
            case "LEFT": return Vector2Int.left;
            case "RIGHT": return Vector2Int.right;
            default:
                ok = false;
                return Vector2Int.zero;
        }
    }
}
