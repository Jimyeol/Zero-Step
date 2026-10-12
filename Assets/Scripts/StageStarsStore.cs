using UnityEngine;

/// <summary>
/// 스테이지별 최고 기록(별 0~3개 + 한붓 클리어 여부)을 한 줄 문자열로 저장한다.
/// 별: 1 = 클리어, 2 = 힌트 없이, 3 = 힌트·실패·리셋 없이. 한붓 = 손가락을 한 번도 떼지 않고 클리어.
/// </summary>
public static class StageStarsStore
{
    private const string PreferenceKey = "ZeroStep.StageStars";
    private const int OneStrokeFlag = 4;
    private static char[] cache;

    public static int GetStars(int stageIndex) => GetRaw(stageIndex) & 3;
    public static bool GetOneStroke(int stageIndex) => (GetRaw(stageIndex) & OneStrokeFlag) != 0;

    /// <summary>기록을 갱신하고, 별 수나 한붓 기록이 새로 좋아졌으면 true.</summary>
    public static bool Record(int stageIndex, int stars, bool oneStroke)
    {
        if (stageIndex <= 0)
            return false;

        EnsureLoaded(stageIndex);
        int previous = GetRaw(stageIndex);
        int bestStars = Mathf.Max(previous & 3, Mathf.Clamp(stars, 0, 3));
        bool bestOneStroke = (previous & OneStrokeFlag) != 0 || oneStroke;
        int next = bestStars | (bestOneStroke ? OneStrokeFlag : 0);
        if (next == previous)
            return false;

        cache[stageIndex - 1] = (char)('0' + next);
        PlayerPrefs.SetString(PreferenceKey, new string(cache));
        return true;
    }

    public static int TotalStars()
    {
        EnsureLoaded(1);
        int total = 0;
        for (int i = 0; i < cache.Length; i++)
            total += (cache[i] - '0') & 3;
        return total;
    }

    public static void Clear()
    {
        cache = null;
        PlayerPrefs.DeleteKey(PreferenceKey);
    }

    private static int GetRaw(int stageIndex)
    {
        if (stageIndex <= 0)
            return 0;
        EnsureLoaded(stageIndex);
        int value = cache[stageIndex - 1] - '0';
        return value >= 0 && value <= 7 ? value : 0;
    }

    private static void EnsureLoaded(int minimumLength)
    {
        if (cache == null)
            cache = PlayerPrefs.GetString(PreferenceKey, string.Empty).ToCharArray();
        if (cache.Length < minimumLength)
        {
            var grown = new char[Mathf.Max(minimumLength, FinalEndingProgressStore.FinalStageIndex)];
            for (int i = 0; i < grown.Length; i++)
                grown[i] = i < cache.Length ? cache[i] : '0';
            cache = grown;
        }
    }
}
