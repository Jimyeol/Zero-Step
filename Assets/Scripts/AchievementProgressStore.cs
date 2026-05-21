using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AchievementProgressRecord
{
    public string id;
    public int progress;
    public int target;
    public bool achieved;
    public bool pendingSync;
    public long achievedUnix;
    public long syncedUnix;
}

[Serializable]
internal class AchievementProgressData
{
    public int schemaVersion = 1;
    public long createdUnix;
    public long updatedUnix;
    public int highestClearedStage;
    public int uniqueStageClearCount;
    public string clearedStagesCsv;
    public int firstTryClearCount;
    public int noHintClearCount;
    public int currentClearStreak;
    public int bestClearStreak;
    public int currentNoResetClearStreak;
    public int bestNoResetClearStreak;
    public int totalRetries;
    public int totalGameOvers;
    public int heartDepletedCount;
    public long totalPlaySeconds;
    public int currentStageIndex;
    public long currentStageStartUnix;
    public int currentStageAttempts;
    public bool currentStageHintUsed;
    public bool currentStageResetUsed;
    public AchievementProgressRecord[] achievements;
}

public static class AchievementProgressStore
{
    public const string ClearStage10 = "clear_stage_10";
    public const string ClearStage100 = "clear_stage_100";
    public const string ClearStage500 = "clear_stage_500";
    public const string ClearStage1000 = "clear_stage_1000";
    public const string NoHintClear10 = "no_hint_clear_10";
    public const string NoHintClear100 = "no_hint_clear_100";
    public const string FirstTryClear10 = "first_try_clear_10";
    public const string FirstTryClear100 = "first_try_clear_100";
    public const string ClearStreak10 = "clear_streak_10";
    public const string ClearStreak50 = "clear_streak_50";
    public const string NoResetClearStreak10 = "no_reset_clear_streak_10";
    public const string NoResetClearStreak50 = "no_reset_clear_streak_50";
    public const string TotalPlayTime1Hour = "total_play_time_1h";
    public const string TotalPlayTime10Hours = "total_play_time_10h";
    public const string TotalRetries50 = "total_retries_50";
    public const string TotalGameOvers50 = "total_gameovers_50";
    public const string HeartDepleted1 = "heart_depleted_1";

    private const string SaveKeyAchievementProgressJson = "AchievementProgressJson";
    private const int CurrentSchemaVersion = 1;
    private const int UnknownStage = 0;
    private const int OneHourSeconds = 3600;

    private static readonly AchievementDefinition[] Definitions =
    {
        new AchievementDefinition(ClearStage10, AchievementMetric.HighestClearedStage, 10),
        new AchievementDefinition(ClearStage100, AchievementMetric.HighestClearedStage, 100),
        new AchievementDefinition(ClearStage500, AchievementMetric.HighestClearedStage, 500),
        new AchievementDefinition(ClearStage1000, AchievementMetric.HighestClearedStage, FinalEndingProgressStore.FinalStageIndex),
        new AchievementDefinition(NoHintClear10, AchievementMetric.NoHintClearCount, 10),
        new AchievementDefinition(NoHintClear100, AchievementMetric.NoHintClearCount, 100),
        new AchievementDefinition(FirstTryClear10, AchievementMetric.FirstTryClearCount, 10),
        new AchievementDefinition(FirstTryClear100, AchievementMetric.FirstTryClearCount, 100),
        new AchievementDefinition(ClearStreak10, AchievementMetric.BestClearStreak, 10),
        new AchievementDefinition(ClearStreak50, AchievementMetric.BestClearStreak, 50),
        new AchievementDefinition(NoResetClearStreak10, AchievementMetric.BestNoResetClearStreak, 10),
        new AchievementDefinition(NoResetClearStreak50, AchievementMetric.BestNoResetClearStreak, 50),
        new AchievementDefinition(TotalPlayTime1Hour, AchievementMetric.TotalPlaySeconds, OneHourSeconds),
        new AchievementDefinition(TotalPlayTime10Hours, AchievementMetric.TotalPlaySeconds, OneHourSeconds * 10),
        new AchievementDefinition(TotalRetries50, AchievementMetric.TotalRetries, 50),
        new AchievementDefinition(TotalGameOvers50, AchievementMetric.TotalGameOvers, 50),
        new AchievementDefinition(HeartDepleted1, AchievementMetric.HeartDepletedCount, 1)
    };

    public static void RecordStageStarted(int stageIndex)
    {
        if (stageIndex <= 0)
            return;

        AchievementProgressData data = LoadProgress();
        long now = GetUnixNowSeconds();
        EnsureCreated(data, now);

        if (data.currentStageIndex != stageIndex)
        {
            CommitCurrentStageElapsed(data, now);
            data.currentStageIndex = stageIndex;
            data.currentStageAttempts = 1;
            data.currentStageHintUsed = false;
            data.currentStageResetUsed = false;
        }
        else if (data.currentStageAttempts <= 0)
        {
            data.currentStageAttempts = 1;
        }

        data.currentStageStartUnix = now;
        SaveProgress(data, now);
    }

    public static void RecordStageCleared(int stageIndex)
    {
        if (stageIndex <= 0)
            return;

        AchievementProgressData data = LoadProgress();
        long now = GetUnixNowSeconds();
        EnsureCurrentStage(data, stageIndex, now);
        CommitCurrentStageElapsed(data, now);

        bool firstRecordedClearForStage = AddClearedStage(data, stageIndex);
        data.highestClearedStage = Mathf.Max(data.highestClearedStage, stageIndex);
        data.currentClearStreak++;
        data.bestClearStreak = Mathf.Max(data.bestClearStreak, data.currentClearStreak);
        if (data.currentStageResetUsed)
        {
            data.currentNoResetClearStreak = 0;
        }
        else
        {
            data.currentNoResetClearStreak++;
            data.bestNoResetClearStreak = Mathf.Max(data.bestNoResetClearStreak, data.currentNoResetClearStreak);
        }

        if (firstRecordedClearForStage)
        {
            if (data.currentStageAttempts <= 1)
                data.firstTryClearCount++;
            if (!data.currentStageHintUsed)
                data.noHintClearCount++;
        }

        data.currentStageIndex = UnknownStage;
        data.currentStageStartUnix = 0L;
        data.currentStageAttempts = 0;
        data.currentStageHintUsed = false;
        data.currentStageResetUsed = false;
        EvaluateAchievements(data, now);
        SaveProgress(data, now);
    }

    public static void RecordStageFailed(int stageIndex)
    {
        if (stageIndex <= 0)
            return;

        AchievementProgressData data = LoadProgress();
        long now = GetUnixNowSeconds();
        EnsureCurrentStage(data, stageIndex, now);
        CommitCurrentStageElapsed(data, now);
        data.totalGameOvers++;
        data.currentStageAttempts++;
        data.currentClearStreak = 0;
        data.currentNoResetClearStreak = 0;
        data.currentStageStartUnix = now;
        EvaluateAchievements(data, now);
        SaveProgress(data, now);
    }

    public static void RecordStageReset(int stageIndex)
    {
        if (stageIndex <= 0)
            return;

        AchievementProgressData data = LoadProgress();
        long now = GetUnixNowSeconds();
        EnsureCurrentStage(data, stageIndex, now);
        CommitCurrentStageElapsed(data, now);
        data.totalRetries++;
        data.currentStageAttempts++;
        data.currentStageResetUsed = true;
        data.currentClearStreak = 0;
        data.currentNoResetClearStreak = 0;
        data.currentStageStartUnix = now;
        EvaluateAchievements(data, now);
        SaveProgress(data, now);
    }

    public static void RecordHintShown(int stageIndex)
    {
        if (stageIndex <= 0)
            return;

        AchievementProgressData data = LoadProgress();
        long now = GetUnixNowSeconds();
        EnsureCurrentStage(data, stageIndex, now);
        data.currentStageHintUsed = true;
        SaveProgress(data, now);
    }

    public static void RecordHeartDepleted()
    {
        AchievementProgressData data = LoadProgress();
        long now = GetUnixNowSeconds();
        EnsureCreated(data, now);
        data.heartDepletedCount++;
        EvaluateAchievements(data, now);
        SaveProgress(data, now);
    }

    public static AchievementProgressRecord[] GetAchievementRecords()
    {
        AchievementProgressData data = LoadProgress();
        EnsureRecords(data);
        AchievementProgressRecord[] records = new AchievementProgressRecord[data.achievements.Length];
        for (int i = 0; i < data.achievements.Length; i++)
            records[i] = CloneRecord(data.achievements[i]);
        return records;
    }

    public static string[] GetPendingSyncAchievementIds()
    {
        AchievementProgressData data = LoadProgress();
        EnsureRecords(data);
        List<string> pending = new List<string>();
        for (int i = 0; i < data.achievements.Length; i++)
        {
            AchievementProgressRecord record = data.achievements[i];
            if (record != null && record.achieved && record.pendingSync && !string.IsNullOrEmpty(record.id))
                pending.Add(record.id);
        }

        return pending.ToArray();
    }

    public static void MarkAchievementSynced(string achievementId)
    {
        if (string.IsNullOrEmpty(achievementId))
            return;

        AchievementProgressData data = LoadProgress();
        EnsureRecords(data);
        AchievementProgressRecord record = FindRecord(data, achievementId);
        if (record == null || !record.achieved)
            return;

        long now = GetUnixNowSeconds();
        record.pendingSync = false;
        record.syncedUnix = now;
        SaveProgress(data, now);
    }

    public static void MarkAllPendingAchievementsSynced()
    {
        AchievementProgressData data = LoadProgress();
        EnsureRecords(data);
        long now = GetUnixNowSeconds();
        bool changed = false;
        for (int i = 0; i < data.achievements.Length; i++)
        {
            AchievementProgressRecord record = data.achievements[i];
            if (record == null || !record.achieved || !record.pendingSync)
                continue;

            record.pendingSync = false;
            record.syncedUnix = now;
            changed = true;
        }

        if (changed)
            SaveProgress(data, now);
    }

    public static void ClearProgress()
    {
        DeleteKey(SaveKeyAchievementProgressJson);
    }

    private static void EnsureCurrentStage(AchievementProgressData data, int stageIndex, long now)
    {
        EnsureCreated(data, now);
        if (data.currentStageIndex != stageIndex)
        {
            data.currentStageIndex = stageIndex;
            data.currentStageAttempts = 1;
            data.currentStageHintUsed = false;
            data.currentStageResetUsed = false;
            data.currentStageStartUnix = now;
        }
        else if (data.currentStageStartUnix <= 0L)
        {
            data.currentStageStartUnix = now;
        }
    }

    private static void EnsureCreated(AchievementProgressData data, long now)
    {
        data.schemaVersion = CurrentSchemaVersion;
        if (data.createdUnix <= 0L)
            data.createdUnix = now;
        EnsureRecords(data);
    }

    private static int CommitCurrentStageElapsed(AchievementProgressData data, long now)
    {
        if (data.currentStageStartUnix <= 0L || now < data.currentStageStartUnix)
            return 0;

        int elapsed = Mathf.Max(0, (int)(now - data.currentStageStartUnix));
        data.totalPlaySeconds += elapsed;
        return elapsed;
    }

    private static bool AddClearedStage(AchievementProgressData data, int stageIndex)
    {
        if (ContainsStage(data.clearedStagesCsv, stageIndex))
            return false;

        data.clearedStagesCsv = AppendStage(data.clearedStagesCsv, stageIndex);
        data.uniqueStageClearCount++;
        return true;
    }

    private static bool ContainsStage(string csv, int stageIndex)
    {
        if (string.IsNullOrEmpty(csv))
            return false;

        string needle = stageIndex.ToString();
        string[] parts = csv.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.Equals(parts[i], needle, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string AppendStage(string csv, int stageIndex)
    {
        if (string.IsNullOrEmpty(csv))
            return stageIndex.ToString();
        return csv + "," + stageIndex;
    }

    private static void EvaluateAchievements(AchievementProgressData data, long now)
    {
        EnsureRecords(data);
        for (int i = 0; i < Definitions.Length; i++)
        {
            AchievementDefinition definition = Definitions[i];
            AchievementProgressRecord record = FindRecord(data, definition.id);
            if (record == null)
                continue;

            int progress = Mathf.Max(0, GetMetricValue(data, definition.metric));
            record.target = definition.target;
            record.progress = Mathf.Min(progress, definition.target);

            if (record.achieved || progress < definition.target)
                continue;

            record.achieved = true;
            record.pendingSync = true;
            record.achievedUnix = now;
            record.syncedUnix = 0L;
        }
    }

    private static int GetMetricValue(AchievementProgressData data, AchievementMetric metric)
    {
        switch (metric)
        {
            case AchievementMetric.HighestClearedStage:
                return data.highestClearedStage;
            case AchievementMetric.NoHintClearCount:
                return data.noHintClearCount;
            case AchievementMetric.FirstTryClearCount:
                return data.firstTryClearCount;
            case AchievementMetric.BestClearStreak:
                return data.bestClearStreak;
            case AchievementMetric.BestNoResetClearStreak:
                return data.bestNoResetClearStreak;
            case AchievementMetric.TotalPlaySeconds:
                return data.totalPlaySeconds > int.MaxValue ? int.MaxValue : (int)data.totalPlaySeconds;
            case AchievementMetric.TotalRetries:
                return data.totalRetries;
            case AchievementMetric.TotalGameOvers:
                return data.totalGameOvers;
            case AchievementMetric.HeartDepletedCount:
                return data.heartDepletedCount;
            default:
                return 0;
        }
    }

    private static void EnsureRecords(AchievementProgressData data)
    {
        List<AchievementProgressRecord> records = new List<AchievementProgressRecord>();
        if (data.achievements != null)
        {
            for (int i = 0; i < data.achievements.Length; i++)
            {
                AchievementProgressRecord record = data.achievements[i];
                if (record == null || string.IsNullOrEmpty(record.id) || ContainsRecord(records, record.id))
                    continue;

                records.Add(record);
            }
        }

        for (int i = 0; i < Definitions.Length; i++)
        {
            AchievementDefinition definition = Definitions[i];
            AchievementProgressRecord record = FindRecord(records, definition.id);
            if (record == null)
            {
                record = new AchievementProgressRecord { id = definition.id };
                records.Add(record);
            }

            record.target = definition.target;
        }

        data.achievements = records.ToArray();
    }

    private static bool ContainsRecord(List<AchievementProgressRecord> records, string achievementId)
    {
        return FindRecord(records, achievementId) != null;
    }

    private static AchievementProgressRecord FindRecord(AchievementProgressData data, string achievementId)
    {
        if (data == null || data.achievements == null)
            return null;

        for (int i = 0; i < data.achievements.Length; i++)
        {
            AchievementProgressRecord record = data.achievements[i];
            if (record != null && string.Equals(record.id, achievementId, StringComparison.Ordinal))
                return record;
        }

        return null;
    }

    private static AchievementProgressRecord FindRecord(List<AchievementProgressRecord> records, string achievementId)
    {
        for (int i = 0; i < records.Count; i++)
        {
            AchievementProgressRecord record = records[i];
            if (record != null && string.Equals(record.id, achievementId, StringComparison.Ordinal))
                return record;
        }

        return null;
    }

    private static AchievementProgressRecord CloneRecord(AchievementProgressRecord record)
    {
        if (record == null)
            return null;

        return new AchievementProgressRecord
        {
            id = record.id,
            progress = record.progress,
            target = record.target,
            achieved = record.achieved,
            pendingSync = record.pendingSync,
            achievedUnix = record.achievedUnix,
            syncedUnix = record.syncedUnix
        };
    }

    private static AchievementProgressData LoadProgress()
    {
        string json = LoadString(SaveKeyAchievementProgressJson, string.Empty);
        if (string.IsNullOrEmpty(json))
            return new AchievementProgressData();

        try
        {
            AchievementProgressData data = JsonUtility.FromJson<AchievementProgressData>(json);
            return data ?? new AchievementProgressData();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AchievementProgressStore] progress load failed: {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, "AchievementProgressStore load failed");
            return new AchievementProgressData();
        }
    }

    private static void SaveProgress(AchievementProgressData data, long now)
    {
        data.updatedUnix = now;
        SaveString(SaveKeyAchievementProgressJson, JsonUtility.ToJson(data ?? new AchievementProgressData()));
    }

    private static long GetUnixNowSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
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
            Debug.LogWarning($"[AchievementProgressStore] load failed({key}): {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, $"AchievementProgressStore load failed: {key}");
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
            Debug.LogWarning($"[AchievementProgressStore] save failed({key}): {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, $"AchievementProgressStore save failed: {key}");
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
            Debug.LogWarning($"[AchievementProgressStore] delete failed({key}): {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, $"AchievementProgressStore delete failed: {key}");
        }

        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }

    private enum AchievementMetric
    {
        HighestClearedStage,
        NoHintClearCount,
        FirstTryClearCount,
        BestClearStreak,
        BestNoResetClearStreak,
        TotalPlaySeconds,
        TotalRetries,
        TotalGameOvers,
        HeartDepletedCount
    }

    private class AchievementDefinition
    {
        public readonly string id;
        public readonly AchievementMetric metric;
        public readonly int target;

        public AchievementDefinition(string id, AchievementMetric metric, int target)
        {
            this.id = id;
            this.metric = metric;
            this.target = target;
        }
    }
}
