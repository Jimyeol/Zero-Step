using System;
using UnityEngine;

public enum FinalEndingState
{
    None,
    PendingResume,
    CompletedReplayable
}

[Serializable]
public class FinalEndingSnapshot
{
    public int finalStageIndex = FinalEndingProgressStore.FinalStageIndex;
    public long firstPlayedUnix;
    public long stage1000ClearUnix;
    public long totalPlaySeconds;
    public int uniquePlayDays;
    public long daysToStage1000;
    public int fastestStageNumber;
    public int fastestStageSeconds;
    public int longestStageNumber;
    public int longestStageSeconds;
    public int firstTryClearCount;
    public int noHintClearCount;
    public int bestClearStreak;
    public int bestNoResetStreak;
    public int mostRetriedStageNumber;
    public int mostRetriedStageCount;
    public int totalRetries;
    public int totalGameOvers;
    public int heartDepletedCount;
    public string titleKey = FinalEndingProgressStore.TitleNeonMasterKey;
    public bool firstPlayedKnown;
    public bool stage1000ClearKnown;
}

[Serializable]
internal class JourneyStatsData
{
    public long firstPlayedUnix;
    public long totalPlaySeconds;
    public int uniquePlayDays;
    public int lastPlayDayNumber;
    public int currentStageIndex;
    public long currentStageStartUnix;
    public int currentStageAttempts;
    public bool currentStageHintUsed;
    public bool currentStageResetUsed;
    public int currentClearStreak;
    public int currentNoResetClearStreak;
    public int bestClearStreak;
    public int bestNoResetStreak;
    public int firstTryClearCount;
    public int noHintClearCount;
    public int fastestStageNumber;
    public int fastestStageSeconds;
    public int longestStageNumber;
    public int longestStageSeconds;
    public int mostRetriedStageNumber;
    public int mostRetriedStageCount;
    public int totalRetries;
    public int totalGameOvers;
    public int heartDepletedCount;
    public long stage1000ClearUnix;
}

public static class FinalEndingProgressStore
{
    public const int FinalStageIndex = 1000;
    public const string TitleNeonMasterKey = "final_credits_title_neon_master";
    public const string TitleIntuitionKey = "final_credits_title_intuition";
    public const string TitleSprinterKey = "final_credits_title_sprinter";
    public const string TitleIndomitableKey = "final_credits_title_indomitable";
    public const string TitleCalmSolverKey = "final_credits_title_calm_solver";

    private const string SaveKeyEndingState = "FinalEndingState";
    private const string SaveKeyEndingSnapshotJson = "FinalEndingSnapshotJson";
    private const string SaveKeyJourneyStatsJson = "FinalEndingJourneyStatsJson";
    private const string StateNone = "none";
    private const string StatePendingResume = "pending_resume";
    private const string StateCompletedReplayable = "completed_replayable";
    private const int UnknownStage = 0;

    public static bool IsFinalStageIndex(int stageIndex)
    {
        return stageIndex == FinalStageIndex;
    }

    public static FinalEndingState LoadEndingState()
    {
        string rawState = LoadString(SaveKeyEndingState, StateNone);
        if (string.Equals(rawState, StatePendingResume, StringComparison.Ordinal))
            return FinalEndingState.PendingResume;
        if (string.Equals(rawState, StateCompletedReplayable, StringComparison.Ordinal))
            return FinalEndingState.CompletedReplayable;
        return FinalEndingState.None;
    }

    public static bool TryLoadEndingProgress(out FinalEndingState state, out FinalEndingSnapshot snapshot)
    {
        state = LoadEndingState();
        snapshot = LoadSnapshot();
        if (state == FinalEndingState.None)
            return true;

        if (snapshot != null)
            return true;

        snapshot = CreateFallbackSnapshot();
        FirebaseBootstrap.LogEvent("journey_stats_missing_field", new System.Collections.Generic.Dictionary<string, object>
        {
            { "field", "snapshot" },
            { "ending_state", ToPersistedState(state) }
        });
        return true;
    }

    public static FinalEndingSnapshot CreateFinalSnapshot()
    {
        JourneyStatsData stats = LoadJourneyStats();
        long now = GetUnixNowSeconds();
        EnsureFirstPlayed(stats, now);
        CommitCurrentStageElapsed(stats, now);
        stats.stage1000ClearUnix = now;
        SaveJourneyStats(stats);

        return BuildSnapshot(stats, now);
    }

    public static void SavePendingEndingResume(FinalEndingSnapshot snapshot)
    {
        if (snapshot == null)
            snapshot = CreateFallbackSnapshot();

        SaveString(SaveKeyEndingSnapshotJson, JsonUtility.ToJson(snapshot));
        SaveString(SaveKeyEndingState, StatePendingResume);
        FirebaseBootstrap.LogEvent("stage1000_finale_started", new System.Collections.Generic.Dictionary<string, object>
        {
            { "final_stage_index", FinalStageIndex },
            { "ending_state", StatePendingResume }
        });
    }

    public static void MarkEndingCompletedReplayable()
    {
        SaveString(SaveKeyEndingState, StateCompletedReplayable);
        FirebaseBootstrap.LogEvent("stage1000_credits_completed", new System.Collections.Generic.Dictionary<string, object>
        {
            { "final_stage_index", FinalStageIndex },
            { "ending_state", StateCompletedReplayable }
        });
    }

    public static void ClearEndingProgress()
    {
        DeleteKey(SaveKeyEndingState);
        DeleteKey(SaveKeyEndingSnapshotJson);
        DeleteKey(SaveKeyJourneyStatsJson);
    }

    public static void RecordStageStarted(int stageIndex)
    {
        if (stageIndex <= 0)
            return;

        JourneyStatsData stats = LoadJourneyStats();
        long now = GetUnixNowSeconds();
        EnsureFirstPlayed(stats, now);
        RegisterPlayDay(stats, now);

        if (stats.currentStageIndex != stageIndex)
        {
            stats.currentStageIndex = stageIndex;
            stats.currentStageAttempts = 1;
            stats.currentStageHintUsed = false;
            stats.currentStageResetUsed = false;
        }
        else if (stats.currentStageAttempts <= 0)
        {
            stats.currentStageAttempts = 1;
        }

        stats.currentStageStartUnix = now;
        SaveJourneyStats(stats);
    }

    public static void RecordStageCleared(int stageIndex)
    {
        if (stageIndex <= 0)
            return;

        JourneyStatsData stats = LoadJourneyStats();
        long now = GetUnixNowSeconds();
        EnsureCurrentStage(stats, stageIndex, now);
        int elapsedSeconds = CommitCurrentStageElapsed(stats, now);

        if (stats.currentStageAttempts <= 1)
            stats.firstTryClearCount++;
        if (!stats.currentStageHintUsed)
            stats.noHintClearCount++;

        stats.currentClearStreak++;
        stats.bestClearStreak = Mathf.Max(stats.bestClearStreak, stats.currentClearStreak);
        if (!stats.currentStageResetUsed)
        {
            stats.currentNoResetClearStreak++;
            stats.bestNoResetStreak = Mathf.Max(stats.bestNoResetStreak, stats.currentNoResetClearStreak);
        }
        else
        {
            stats.currentNoResetClearStreak = 0;
        }

        if (elapsedSeconds > 0)
        {
            if (stats.fastestStageNumber <= 0 || elapsedSeconds < stats.fastestStageSeconds)
            {
                stats.fastestStageNumber = stageIndex;
                stats.fastestStageSeconds = elapsedSeconds;
            }

            if (elapsedSeconds > stats.longestStageSeconds)
            {
                stats.longestStageNumber = stageIndex;
                stats.longestStageSeconds = elapsedSeconds;
            }
        }

        stats.currentStageIndex = UnknownStage;
        stats.currentStageAttempts = 0;
        stats.currentStageHintUsed = false;
        stats.currentStageResetUsed = false;
        stats.currentStageStartUnix = 0L;
        SaveJourneyStats(stats);
    }

    public static void RecordStageFailed(int stageIndex)
    {
        if (stageIndex <= 0)
            return;

        JourneyStatsData stats = LoadJourneyStats();
        long now = GetUnixNowSeconds();
        EnsureCurrentStage(stats, stageIndex, now);
        CommitCurrentStageElapsed(stats, now);
        stats.totalGameOvers++;
        stats.currentStageAttempts++;
        stats.currentClearStreak = 0;
        stats.currentNoResetClearStreak = 0;
        UpdateMostRetried(stats, stageIndex);
        stats.currentStageStartUnix = now;
        SaveJourneyStats(stats);
    }

    public static void RecordStageReset(int stageIndex)
    {
        if (stageIndex <= 0)
            return;

        JourneyStatsData stats = LoadJourneyStats();
        long now = GetUnixNowSeconds();
        EnsureCurrentStage(stats, stageIndex, now);
        CommitCurrentStageElapsed(stats, now);
        stats.totalRetries++;
        stats.currentStageAttempts++;
        stats.currentStageResetUsed = true;
        stats.currentNoResetClearStreak = 0;
        UpdateMostRetried(stats, stageIndex);
        stats.currentStageStartUnix = now;
        SaveJourneyStats(stats);
    }

    public static void RecordHintShown(int stageIndex)
    {
        if (stageIndex <= 0)
            return;

        JourneyStatsData stats = LoadJourneyStats();
        long now = GetUnixNowSeconds();
        EnsureCurrentStage(stats, stageIndex, now);
        stats.currentStageHintUsed = true;
        SaveJourneyStats(stats);
    }

    public static void RecordHeartDepleted()
    {
        JourneyStatsData stats = LoadJourneyStats();
        EnsureFirstPlayed(stats, GetUnixNowSeconds());
        stats.heartDepletedCount++;
        SaveJourneyStats(stats);
    }

    public static FinalEndingSnapshot TryGetReplaySnapshot()
    {
        return LoadSnapshot();
    }

    private static FinalEndingSnapshot LoadSnapshot()
    {
        string json = LoadString(SaveKeyEndingSnapshotJson, string.Empty);
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonUtility.FromJson<FinalEndingSnapshot>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FinalEndingProgressStore] snapshot load failed: {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, "FinalEndingSnapshot load failed");
            return null;
        }
    }

    private static FinalEndingSnapshot BuildSnapshot(JourneyStatsData stats, long clearUnix)
    {
        long firstPlayed = stats.firstPlayedUnix > 0L ? stats.firstPlayedUnix : clearUnix;
        long daysToClear = firstPlayed > 0L && clearUnix > firstPlayed
            ? Mathf.Max(0, Mathf.CeilToInt((clearUnix - firstPlayed) / 86400f))
            : 0L;

        return new FinalEndingSnapshot
        {
            finalStageIndex = FinalStageIndex,
            firstPlayedUnix = firstPlayed,
            stage1000ClearUnix = clearUnix,
            totalPlaySeconds = stats.totalPlaySeconds,
            uniquePlayDays = Mathf.Max(1, stats.uniquePlayDays),
            daysToStage1000 = daysToClear,
            fastestStageNumber = stats.fastestStageNumber,
            fastestStageSeconds = stats.fastestStageSeconds,
            longestStageNumber = stats.longestStageNumber,
            longestStageSeconds = stats.longestStageSeconds,
            firstTryClearCount = stats.firstTryClearCount,
            noHintClearCount = stats.noHintClearCount,
            bestClearStreak = stats.bestClearStreak,
            bestNoResetStreak = stats.bestNoResetStreak,
            mostRetriedStageNumber = stats.mostRetriedStageNumber,
            mostRetriedStageCount = stats.mostRetriedStageCount,
            totalRetries = stats.totalRetries,
            totalGameOvers = stats.totalGameOvers,
            heartDepletedCount = stats.heartDepletedCount,
            titleKey = SelectTitleKey(stats),
            firstPlayedKnown = stats.firstPlayedUnix > 0L,
            stage1000ClearKnown = clearUnix > 0L
        };
    }

    private static FinalEndingSnapshot CreateFallbackSnapshot()
    {
        long now = GetUnixNowSeconds();
        return new FinalEndingSnapshot
        {
            finalStageIndex = FinalStageIndex,
            firstPlayedUnix = now,
            stage1000ClearUnix = now,
            uniquePlayDays = 1,
            titleKey = TitleNeonMasterKey,
            firstPlayedKnown = false,
            stage1000ClearKnown = false
        };
    }

    private static string SelectTitleKey(JourneyStatsData stats)
    {
        if (stats.noHintClearCount >= 700)
            return TitleIntuitionKey;
        if (stats.fastestStageNumber > 0 && stats.fastestStageSeconds > 0 && stats.fastestStageSeconds <= 8)
            return TitleSprinterKey;
        if (stats.totalRetries + stats.totalGameOvers >= 250)
            return TitleIndomitableKey;
        if (stats.longestStageSeconds >= 900 && stats.totalGameOvers < 50)
            return TitleCalmSolverKey;
        return TitleNeonMasterKey;
    }

    private static JourneyStatsData LoadJourneyStats()
    {
        string json = LoadString(SaveKeyJourneyStatsJson, string.Empty);
        if (string.IsNullOrEmpty(json))
            return new JourneyStatsData();

        try
        {
            JourneyStatsData stats = JsonUtility.FromJson<JourneyStatsData>(json);
            return stats ?? new JourneyStatsData();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FinalEndingProgressStore] journey stats load failed: {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, "JourneyStats load failed");
            return new JourneyStatsData();
        }
    }

    private static void SaveJourneyStats(JourneyStatsData stats)
    {
        SaveString(SaveKeyJourneyStatsJson, JsonUtility.ToJson(stats ?? new JourneyStatsData()));
    }

    private static void EnsureCurrentStage(JourneyStatsData stats, int stageIndex, long now)
    {
        EnsureFirstPlayed(stats, now);
        RegisterPlayDay(stats, now);
        if (stats.currentStageIndex != stageIndex)
        {
            stats.currentStageIndex = stageIndex;
            stats.currentStageAttempts = Mathf.Max(1, stats.currentStageAttempts);
            stats.currentStageHintUsed = false;
            stats.currentStageResetUsed = false;
            stats.currentStageStartUnix = now;
        }
        else if (stats.currentStageStartUnix <= 0L)
        {
            stats.currentStageStartUnix = now;
        }
    }

    private static void EnsureFirstPlayed(JourneyStatsData stats, long now)
    {
        if (stats.firstPlayedUnix <= 0L)
            stats.firstPlayedUnix = now;
    }

    private static void RegisterPlayDay(JourneyStatsData stats, long unix)
    {
        int dayNumber = Mathf.FloorToInt(unix / 86400f);
        if (stats.lastPlayDayNumber == dayNumber)
            return;

        stats.lastPlayDayNumber = dayNumber;
        stats.uniquePlayDays = Mathf.Max(1, stats.uniquePlayDays + 1);
    }

    private static int CommitCurrentStageElapsed(JourneyStatsData stats, long now)
    {
        if (stats.currentStageStartUnix <= 0L || now < stats.currentStageStartUnix)
            return 0;

        int elapsed = Mathf.Max(0, (int)(now - stats.currentStageStartUnix));
        stats.totalPlaySeconds += elapsed;
        return elapsed;
    }

    private static void UpdateMostRetried(JourneyStatsData stats, int stageIndex)
    {
        int retries = Mathf.Max(0, stats.currentStageAttempts - 1);
        if (retries > stats.mostRetriedStageCount)
        {
            stats.mostRetriedStageNumber = stageIndex;
            stats.mostRetriedStageCount = retries;
        }
    }

    private static long GetUnixNowSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private static string ToPersistedState(FinalEndingState state)
    {
        switch (state)
        {
            case FinalEndingState.PendingResume:
                return StatePendingResume;
            case FinalEndingState.CompletedReplayable:
                return StateCompletedReplayable;
            default:
                return StateNone;
        }
    }

    private static string LoadString(string key, string defaultValue)
    {
        try
        {
            if (ES3.KeyExists(key))
                return ES3.Load<string>(key);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FinalEndingProgressStore] load failed({key}): {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, $"FinalEndingProgressStore load failed: {key}");
        }

        return PlayerPrefs.GetString(key, defaultValue);
    }

    private static void SaveString(string key, string value)
    {
        string safeValue = value ?? string.Empty;
        try
        {
            ES3.Save(key, safeValue);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FinalEndingProgressStore] save failed({key}): {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, $"FinalEndingProgressStore save failed: {key}");
        }

        PlayerPrefs.SetString(key, safeValue);
        PlayerPrefs.Save();
    }

    private static void DeleteKey(string key)
    {
        try
        {
            if (ES3.KeyExists(key))
                ES3.DeleteKey(key);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FinalEndingProgressStore] delete failed({key}): {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, $"FinalEndingProgressStore delete failed: {key}");
        }

        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
}
