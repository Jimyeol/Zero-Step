using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 피버 보드·로그라이트 런용 즉석 보드 생성기.
/// 무작위 경로를 먼저 걷고, 각 칸을 지난 횟수를 그 칸의 숫자로 적는다. 규칙상 "떠날 때 1 감소, 마지막 칸은 1인 채로 들어가면 클리어"이므로
/// 이렇게 만든 보드는 그 경로 자체가 정답이라 항상 풀린다(기본 타일만 사용, TILE_RULES.md 1·2절).
/// 같은 시드면 항상 같은 보드가 나온다(데일리·공유용).
/// </summary>
public static class BoardGenerator
{
    public struct Settings
    {
        /// <summary>보드 최대 가로·세로(경로가 실제로 쓴 영역만 남긴다).</summary>
        public int maxWidth;
        public int maxHeight;
        /// <summary>숫자 합(= 정답 이동 수 + 1) 목표.</summary>
        public int totalCount;
        /// <summary>한 칸 숫자 상한.</summary>
        public int maxCount;
        /// <summary>최소 칸 수. 못 채우면 다른 시드로 다시 걷는다.</summary>
        public int minCells;

        public static Settings Small => new Settings { maxWidth = 4, maxHeight = 4, totalCount = 12, maxCount = 2, minCells = 7 };
        public static Settings Medium => new Settings { maxWidth = 5, maxHeight = 6, totalCount = 22, maxCount = 3, minCells = 12 };
        public static Settings Large => new Settings { maxWidth = 6, maxHeight = 7, totalCount = 34, maxCount = 3, minCells = 18 };
        /// <summary>피버 보드: 넓고 숫자가 낮아 한 획으로 시원하게 쓸어 담기 좋다.</summary>
        public static Settings Fever => new Settings { maxWidth = 6, maxHeight = 7, totalCount = 40, maxCount = 2, minCells = 26 };
    }

    public sealed class Result
    {
        public StageData stage;
        /// <summary>시작 칸 다음부터 끝까지의 정답 이동(StageManager.LoadSolutionMoves와 같은 형식).</summary>
        public List<Vector2Int> solution;
        public int seed;
    }

    private const int MaxAttempts = 40;
    private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    public static Result Generate(Settings settings, int seed)
    {
        Result best = null;
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            int attemptSeed = unchecked(seed * 7919 + attempt * 104729);
            Result result = TryWalk(settings, attemptSeed);
            if (result == null)
                continue;
            result.seed = seed;
            if (CountCells(result.stage) >= settings.minCells)
                return result;
            if (best == null || CountCells(result.stage) > CountCells(best.stage))
                best = result;
        }
        return best;
    }

    private static Result TryWalk(Settings s, int seed)
    {
        int w = Mathf.Max(2, s.maxWidth);
        int h = Mathf.Max(2, s.maxHeight);
        int maxCount = Mathf.Max(1, s.maxCount);
        var random = new System.Random(seed);
        var counts = new int[w, h];
        var path = new List<Vector2Int>(s.totalCount);

        Vector2Int pos = new Vector2Int(random.Next(w), random.Next(h));
        counts[pos.x, pos.y] = 1;
        path.Add(pos);

        var candidates = new List<Vector2Int>(4);
        var weights = new List<float>(4);
        while (path.Count < s.totalCount)
        {
            candidates.Clear();
            weights.Clear();
            Vector2Int previous = path.Count >= 2 ? path[path.Count - 2] : new Vector2Int(-1, -1);
            for (int d = 0; d < Directions.Length; d++)
            {
                Vector2Int next = pos + Directions[d];
                if (next.x < 0 || next.y < 0 || next.x >= w || next.y >= h)
                    continue;
                int c = counts[next.x, next.y];
                if (c >= maxCount)
                    continue;
                // 새 칸을 좋아하고, 막 떠난 칸으로 바로 돌아가는 건 드물게.
                float weight = c == 0 ? 3f : 1f;
                if (next == previous)
                    weight *= 0.25f;
                candidates.Add(next);
                weights.Add(weight);
            }
            if (candidates.Count == 0)
                break;

            pos = candidates[Pick(weights, random)];
            counts[pos.x, pos.y]++;
            path.Add(pos);
        }

        if (path.Count < 2)
            return null;
        return Build(counts, w, h, path);
    }

    private static int Pick(List<float> weights, System.Random random)
    {
        float total = 0f;
        for (int i = 0; i < weights.Count; i++)
            total += weights[i];
        float roll = (float)random.NextDouble() * total;
        for (int i = 0; i < weights.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0f)
                return i;
        }
        return weights.Count - 1;
    }

    /// <summary>경로가 쓴 영역만 잘라 (0,0) 기준 보드로 만든다.</summary>
    private static Result Build(int[,] counts, int w, int h, List<Vector2Int> path)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (Vector2Int p in path)
        {
            minX = Mathf.Min(minX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxX = Mathf.Max(maxX, p.x);
            maxY = Mathf.Max(maxY, p.y);
        }

        var cells = new List<CellData>();
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (counts[x, y] > 0)
                    cells.Add(new CellData { x = x - minX, y = y - minY, count = counts[x, y], type = "Normal" });
            }
        }

        var solution = new List<Vector2Int>(path.Count - 1);
        for (int i = 1; i < path.Count; i++)
            solution.Add(new Vector2Int(path[i].x - minX, path[i].y - minY));

        var stage = new StageData
        {
            stageID = 0,
            width = maxX - minX + 1,
            height = maxY - minY + 1,
            startPoint = new StartPointData { x = path[0].x - minX, y = path[0].y - minY },
            cells = cells.ToArray()
        };
        return new Result { stage = stage, solution = solution };
    }

    private static int CountCells(StageData stage) => stage?.cells?.Length ?? 0;

    /// <summary>
    /// 기본 타일 규칙으로 정답을 재생해 확인한다: 들어가는 칸은 숫자가 남아 있어야 하고, 떠날 때 1 감소,
    /// 마지막 칸은 숫자 1·전체 합 1인 상태로 들어가야 한다.
    /// </summary>
    public static bool Verify(StageData stage, IReadOnlyList<Vector2Int> solution, out string error)
    {
        error = null;
        var counts = new Dictionary<Vector2Int, int>();
        int total = 0;
        foreach (CellData cell in stage.cells)
        {
            counts[new Vector2Int(cell.x, cell.y)] = cell.count;
            total += cell.count;
        }
        Vector2Int pos = new Vector2Int(stage.startPoint.x, stage.startPoint.y);
        if (!counts.TryGetValue(pos, out int startCount) || startCount <= 0)
        {
            error = "start cell missing";
            return false;
        }
        for (int i = 0; i < solution.Count; i++)
        {
            Vector2Int next = solution[i];
            if (Mathf.Abs(next.x - pos.x) + Mathf.Abs(next.y - pos.y) != 1)
            {
                error = $"step {i} not adjacent";
                return false;
            }
            if (!counts.TryGetValue(next, out int c) || c <= 0)
            {
                error = $"step {i} enters empty cell";
                return false;
            }
            counts[pos]--;
            total--;
            pos = next;
        }
        if (counts[pos] != 1 || total != 1)
        {
            error = $"ends with count {counts[pos]} total {total}";
            return false;
        }
        return true;
    }
}
