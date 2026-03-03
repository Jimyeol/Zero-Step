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
}
