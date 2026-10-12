using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 스테이지 번호로 JSON 파일을 불러와 StageData로 파싱.
/// Resources/Stages/stage_{번호}.json 사용.
/// </summary>
public static class StageManager
{
    private const string StagesPath = "Stages/stage_";
    private static readonly Dictionary<int, StageData> stageCache = new Dictionary<int, StageData>();

    /// <summary>
    /// 스테이지 번호에 해당하는 JSON을 로드해 StageData 반환. 없으면 null.
    /// </summary>
    public static StageData LoadStage(int stageNumber)
    {
        return LoadStageInternal(stageNumber, logMissingWarning: true);
    }

    /// <summary>
    /// 지정 스테이지부터 연속해서 미리 로드한다. 존재하지 않는 번호를 만나면 중단.
    /// </summary>
    public static int PrewarmStages(int startStageNumber, int maxCount)
    {
        int warmed = 0;
        int safeCount = Mathf.Max(0, maxCount);
        int stageNumber = Mathf.Max(1, startStageNumber);

        for (int i = 0; i < safeCount; i++)
        {
            StageData data = LoadStageInternal(stageNumber + i, logMissingWarning: false);
            if (data == null)
                break;
            warmed++;
        }

        return warmed;
    }

    private static StageData LoadStageInternal(int stageNumber, bool logMissingWarning)
    {
        if (stageNumber <= 0)
            return null;

        if (stageCache.TryGetValue(stageNumber, out StageData cachedData) && cachedData != null)
            return cachedData;

        string path = StagesPath + stageNumber;
        TextAsset asset = Resources.Load<TextAsset>(path);
        if (asset == null)
        {
            if (logMissingWarning)
                Debug.LogWarning($"[StageManager] 스테이지 리소스 없음: {path}");
            return null;
        }

        StageData data = JsonUtility.FromJson<StageData>(asset.text);
        if (data == null || data.cells == null)
        {
            Debug.LogWarning($"[StageManager] 스테이지 파싱 실패: {path}");
            return null;
        }

        stageCache[stageNumber] = data;
        return data;
    }

    // ===== 검증된 정답 경로 =====
    // Resources/Stages/solutions.txt 한 줄 형식: N|x,y;x,y;...|x,y;...  ('|' 구간 = 손을 떼지 않는 한 번의 드래그)
    // 힌트가 정답 경로를 따라가는 동안에는 탐색 없이 즉시 힌트를 보여주는 데 쓴다.
    private const string SolutionsPath = "Stages/solutions";
    private static Dictionary<int, string> solutionLines;
    private static readonly Dictionary<int, List<Vector2Int>> solutionMoveCache = new Dictionary<int, List<Vector2Int>>();

    /// <summary>시작 칸 다음부터 끝까지 이어지는 정답 이동 칸 목록. 없으면 null.</summary>
    public static List<Vector2Int> LoadSolutionMoves(int stageNumber)
    {
        if (solutionMoveCache.TryGetValue(stageNumber, out List<Vector2Int> cached))
            return cached;

        EnsureSolutionLines();
        List<Vector2Int> moves = null;
        if (solutionLines != null && solutionLines.TryGetValue(stageNumber, out string line))
            moves = ParseSolutionMoves(line);
        solutionMoveCache[stageNumber] = moves;
        return moves;
    }

    private static void EnsureSolutionLines()
    {
        if (solutionLines != null)
            return;

        solutionLines = new Dictionary<int, string>();
        TextAsset asset = Resources.Load<TextAsset>(SolutionsPath);
        if (asset == null)
            return;

        foreach (string rawLine in asset.text.Split('\n'))
        {
            string line = rawLine.Trim();
            int bar = line.IndexOf('|');
            if (bar <= 0)
                continue;
            if (int.TryParse(line.Substring(0, bar), out int stage))
                solutionLines[stage] = line.Substring(bar + 1);
        }
        Resources.UnloadAsset(asset);
    }

    private static List<Vector2Int> ParseSolutionMoves(string strokesText)
    {
        var moves = new List<Vector2Int>();
        string[] strokes = strokesText.Split('|');
        for (int s = 0; s < strokes.Length; s++)
        {
            string[] cells = strokes[s].Split(';');
            // 각 드래그의 첫 칸은 현재 위치이므로 이동에서 제외한다.
            for (int i = 1; i < cells.Length; i++)
            {
                string[] xy = cells[i].Split(',');
                if (xy.Length == 2 && int.TryParse(xy[0], out int x) && int.TryParse(xy[1], out int y))
                    moves.Add(new Vector2Int(x, y));
            }
        }
        return moves.Count > 0 ? moves : null;
    }
}
