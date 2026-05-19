using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.RemoteConfig;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Firebase Analytics/Crashlytics 런타임 부트스트랩.
/// 앱 시작 시 자동으로 생성되어 Firebase 초기화 및 공용 로깅 API를 제공한다.
/// </summary>
public sealed class FirebaseBootstrap : MonoBehaviour
{
    private sealed class QueuedAnalyticsEvent
    {
        public string Name;
        public Dictionary<string, object> Parameters;
    }

    private const string AnalyticsCollectionKey = "FirebaseAnalyticsCollectionEnabled";
    private const string CrashlyticsCollectionKey = "FirebaseCrashlyticsCollectionEnabled";
    private const int MaxQueuedEvents = 128;
    private const long DefaultRemoteConfigFetchIntervalSeconds = 12L * 60L * 60L;
    public const string RcStageInterstitialFirstEligibleStage = "stage_interstitial_first_eligible_stage";
    public const string RcStageInterstitialCooldownSeconds = "stage_interstitial_cooldown_seconds";
    public const string RcStageInterstitialMinStageGap = "stage_interstitial_min_stage_gap";
    public const string RcIdleHintBonusEnabled = "idle_hint_bonus_enabled";
    public const string RcIdleHintBonusDelaySeconds = "idle_hint_bonus_delay_seconds";
    public const string RcDailyChallengeEnabled = "daily_challenge_enabled";
    public const string RcWeeklyStageEnabled = "weekly_stage_enabled";
    public const string RcInfiniteModeEnabled = "infinite_mode_enabled";
    public const string RcLeaderboardEnabled = "leaderboard_enabled";

    private static FirebaseBootstrap instance;
    private static bool isInitializing;
    private static bool isInitialized;
    private static bool remoteConfigInitialized;
    private static bool analyticsCollectionEnabled = true;
    private static bool crashlyticsCollectionEnabled = true;
    private static readonly Queue<QueuedAnalyticsEvent> pendingEvents = new Queue<QueuedAnalyticsEvent>();
    private static readonly Dictionary<string, object> remoteConfigDefaults = CreateRemoteConfigDefaults();

    public static bool IsInitialized => isInitialized;
    public static bool IsRemoteConfigInitialized => remoteConfigInitialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("FirebaseBootstrap");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<FirebaseBootstrap>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void Start()
    {
        if (!isInitializing && !isInitialized)
            StartCoroutine(InitializeFirebaseRoutine());
    }

    private IEnumerator InitializeFirebaseRoutine()
    {
        isInitializing = true;

#if UNITY_EDITOR
        isInitializing = false;
        Debug.Log("[FirebaseBootstrap] Firebase initialization is skipped in Unity Editor. Validate on iOS/Android device build.");
        yield break;
#endif

        var dependencyTask = FirebaseApp.CheckAndFixDependenciesAsync();
        yield return new WaitUntil(() => dependencyTask.IsCompleted);

        if (dependencyTask.IsFaulted)
        {
            isInitializing = false;
            Exception ex = dependencyTask.Exception ?? new Exception("Firebase dependency check failed.");
            Debug.LogError($"[FirebaseBootstrap] Firebase dependency check failed: {ex}");
            yield break;
        }

        DependencyStatus status = dependencyTask.Result;
        if (status != DependencyStatus.Available)
        {
            isInitializing = false;
            Debug.LogError($"[FirebaseBootstrap] Firebase dependencies unavailable: {status}");
            yield break;
        }

        try
        {
            _ = FirebaseApp.DefaultInstance;

            analyticsCollectionEnabled = LoadCollectionFlag(AnalyticsCollectionKey, true);
            crashlyticsCollectionEnabled = LoadCollectionFlag(CrashlyticsCollectionKey, true);

            FirebaseAnalytics.SetAnalyticsCollectionEnabled(analyticsCollectionEnabled);

            Crashlytics.ReportUncaughtExceptionsAsFatal = true;
            ApplyCrashlyticsCollectionSetting(crashlyticsCollectionEnabled);
            Crashlytics.SetCustomKey("app_version", Application.version);
            Crashlytics.SetCustomKey("unity_version", Application.unityVersion);
            Crashlytics.SetCustomKey("platform", Application.platform.ToString());
            Crashlytics.SetUserId(SystemInfo.deviceUniqueIdentifier);
            Crashlytics.Log("Firebase initialized.");

            FirebaseAnalytics.SetUserId(SystemInfo.deviceUniqueIdentifier);
            FirebaseAnalytics.SetUserProperty("platform", Application.platform.ToString());
            FirebaseAnalytics.SetUserProperty("system_language", Application.systemLanguage.ToString());
            FirebaseAnalytics.SetUserProperty("app_version", Application.version);
            StartCoroutine(InitializeRemoteConfigRoutine());

            isInitialized = true;
            isInitializing = false;

            LogEventInternal("app_open", new Dictionary<string, object>
            {
                { "scene_name", SceneManager.GetActiveScene().name },
                { "platform", Application.platform.ToString() }
            }, queueIfNotReady: false);

            FlushPendingEvents();
            Debug.Log("[FirebaseBootstrap] Firebase initialized successfully.");
        }
        catch (Exception ex)
        {
            isInitializing = false;
            Debug.LogError($"[FirebaseBootstrap] Initialization exception: {ex}");
        }
    }

    private IEnumerator InitializeRemoteConfigRoutine()
    {
        var remoteConfig = FirebaseRemoteConfig.DefaultInstance;
        var setDefaultsTask = remoteConfig.SetDefaultsAsync(remoteConfigDefaults);
        yield return new WaitUntil(() => setDefaultsTask.IsCompleted);

        if (setDefaultsTask.IsFaulted)
        {
            Exception ex = setDefaultsTask.Exception ?? new Exception("Remote Config SetDefaultsAsync failed.");
            Debug.LogWarning($"[FirebaseBootstrap] Remote Config defaults failed: {ex.Message}");
            LogNonFatalException(ex, "Remote Config defaults failed");
            yield break;
        }

        remoteConfigInitialized = true;
        long minimumFetchIntervalSeconds = GetRemoteLong("remote_config_min_fetch_interval_seconds", DefaultRemoteConfigFetchIntervalSeconds);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        minimumFetchIntervalSeconds = 0L;
#endif

        var fetchTask = remoteConfig.FetchAsync(TimeSpan.FromSeconds(Math.Max(0L, minimumFetchIntervalSeconds)));
        yield return new WaitUntil(() => fetchTask.IsCompleted);

        if (fetchTask.IsFaulted)
        {
            Exception ex = fetchTask.Exception ?? new Exception("Remote Config FetchAsync failed.");
            Debug.LogWarning($"[FirebaseBootstrap] Remote Config fetch failed: {ex.Message}");
            LogEvent("remote_config_fetch_failed", new Dictionary<string, object>
            {
                { "minimum_fetch_interval_seconds", minimumFetchIntervalSeconds }
            });
            LogNonFatalException(ex, "Remote Config fetch failed");
            yield break;
        }

        var activateTask = remoteConfig.ActivateAsync();
        yield return new WaitUntil(() => activateTask.IsCompleted);

        if (activateTask.IsFaulted)
        {
            Exception ex = activateTask.Exception ?? new Exception("Remote Config ActivateAsync failed.");
            Debug.LogWarning($"[FirebaseBootstrap] Remote Config activate failed: {ex.Message}");
            LogNonFatalException(ex, "Remote Config activate failed");
            yield break;
        }

        bool activated = activateTask.Result;
        LogEvent("remote_config_fetch_complete", new Dictionary<string, object>
        {
            { "activated", activated },
            { "minimum_fetch_interval_seconds", minimumFetchIntervalSeconds }
        });
        Debug.Log($"[FirebaseBootstrap] Remote Config ready. activated={activated}");
    }

    private void OnActiveSceneChanged(Scene from, Scene to)
    {
        LogEvent("scene_change", new Dictionary<string, object>
        {
            { "from_scene", string.IsNullOrEmpty(from.name) ? "unknown" : from.name },
            { "to_scene", string.IsNullOrEmpty(to.name) ? "unknown" : to.name }
        });
    }

    public static void LogEvent(string eventName)
    {
        LogEventInternal(eventName, null, queueIfNotReady: true);
    }

    public static void LogEvent(string eventName, Dictionary<string, object> parameters)
    {
        LogEventInternal(eventName, parameters, queueIfNotReady: true);
    }

    public static void LogBreadcrumb(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (!isInitialized || !crashlyticsCollectionEnabled)
            return;

        try
        {
            Crashlytics.Log(message);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FirebaseBootstrap] Crashlytics breadcrumb failed: {ex.Message}");
        }
    }

    public static void LogNonFatalException(Exception exception, string context)
    {
        if (exception == null)
            return;

        if (!isInitialized || !crashlyticsCollectionEnabled)
            return;

        try
        {
            if (!string.IsNullOrWhiteSpace(context))
                Crashlytics.Log(context);

            Crashlytics.LogException(exception);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FirebaseBootstrap] Crashlytics non-fatal failed: {ex.Message}");
        }
    }

    public static bool GetRemoteBool(string key, bool defaultValue)
    {
        if (TryGetRemoteConfigValue(key, out ConfigValue value))
            return value.BooleanValue;

        object fallback = GetRemoteDefault(key);
        return fallback is bool boolValue ? boolValue : defaultValue;
    }

    public static long GetRemoteLong(string key, long defaultValue)
    {
        if (TryGetRemoteConfigValue(key, out ConfigValue value))
            return value.LongValue;

        object fallback = GetRemoteDefault(key);
        if (fallback is int intValue)
            return intValue;
        if (fallback is long longValue)
            return longValue;
        if (fallback is float floatValue)
            return Mathf.RoundToInt(floatValue);
        if (fallback is double doubleValue)
            return (long)Math.Round(doubleValue);
        return defaultValue;
    }

    public static double GetRemoteDouble(string key, double defaultValue)
    {
        if (TryGetRemoteConfigValue(key, out ConfigValue value))
            return value.DoubleValue;

        object fallback = GetRemoteDefault(key);
        if (fallback is double doubleValue)
            return doubleValue;
        if (fallback is float floatValue)
            return floatValue;
        if (fallback is int intValue)
            return intValue;
        if (fallback is long longValue)
            return longValue;
        return defaultValue;
    }

    public static string GetRemoteString(string key, string defaultValue)
    {
        if (TryGetRemoteConfigValue(key, out ConfigValue value))
            return value.StringValue;

        object fallback = GetRemoteDefault(key);
        return fallback != null ? fallback.ToString() : defaultValue;
    }

    [ContextMenu("Firebase/Log Test Non-Fatal")]
    private void DebugLogNonFatal()
    {
        LogNonFatalException(new Exception("Firebase test non-fatal exception"), "Debug non-fatal from context menu");
        LogEvent("firebase_test_nonfatal");
    }

    [ContextMenu("Firebase/Force Crash")]
    private void DebugForceCrash()
    {
        ForceCrashForTest();
    }

    public static void ForceCrashForTest()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("[FirebaseBootstrap] Firebase not initialized yet. Crash test skipped.");
            return;
        }

        try
        {
            Crashlytics.Log("Manual crash test requested.");

            MethodInfo method = typeof(Crashlytics).GetMethod("CrashApplication", BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                method.Invoke(null, null);
                return;
            }

            throw new Exception("Manual crash test fallback exception");
        }
        catch (Exception ex)
        {
            LogNonFatalException(ex, "ForceCrashForTest fallback path");
            throw;
        }
    }

    private static void LogEventInternal(string eventName, Dictionary<string, object> parameters, bool queueIfNotReady)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            return;

        if (!isInitialized)
        {
            if (queueIfNotReady)
                EnqueueEvent(eventName, parameters);
            return;
        }

        if (!analyticsCollectionEnabled)
            return;

        try
        {
            if (parameters == null || parameters.Count == 0)
            {
                FirebaseAnalytics.LogEvent(eventName);
                return;
            }

            List<Parameter> firebaseParams = new List<Parameter>(parameters.Count);
            foreach (KeyValuePair<string, object> pair in parameters)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                    continue;

                switch (pair.Value)
                {
                    case string valueString:
                        firebaseParams.Add(new Parameter(pair.Key, valueString));
                        break;
                    case int valueInt:
                        firebaseParams.Add(new Parameter(pair.Key, valueInt));
                        break;
                    case long valueLong:
                        firebaseParams.Add(new Parameter(pair.Key, valueLong));
                        break;
                    case bool valueBool:
                        firebaseParams.Add(new Parameter(pair.Key, valueBool ? 1L : 0L));
                        break;
                    case float valueFloat:
                        firebaseParams.Add(new Parameter(pair.Key, Convert.ToDouble(valueFloat)));
                        break;
                    case double valueDouble:
                        firebaseParams.Add(new Parameter(pair.Key, valueDouble));
                        break;
                    default:
                        firebaseParams.Add(new Parameter(pair.Key, pair.Value.ToString()));
                        break;
                }
            }

            if (firebaseParams.Count == 0)
                FirebaseAnalytics.LogEvent(eventName);
            else
                FirebaseAnalytics.LogEvent(eventName, firebaseParams.ToArray());
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FirebaseBootstrap] Analytics event failed ({eventName}): {ex.Message}");
        }
    }

    private static void EnqueueEvent(string eventName, Dictionary<string, object> parameters)
    {
        if (pendingEvents.Count >= MaxQueuedEvents)
            pendingEvents.Dequeue();

        pendingEvents.Enqueue(new QueuedAnalyticsEvent
        {
            Name = eventName,
            Parameters = CloneParameters(parameters)
        });
    }

    private static Dictionary<string, object> CloneParameters(Dictionary<string, object> source)
    {
        if (source == null || source.Count == 0)
            return null;

        Dictionary<string, object> copy = new Dictionary<string, object>(source.Count);
        foreach (KeyValuePair<string, object> pair in source)
            copy[pair.Key] = pair.Value;
        return copy;
    }

    private static void FlushPendingEvents()
    {
        while (pendingEvents.Count > 0)
        {
            QueuedAnalyticsEvent queued = pendingEvents.Dequeue();
            LogEventInternal(queued.Name, queued.Parameters, queueIfNotReady: false);
        }
    }

    private static Dictionary<string, object> CreateRemoteConfigDefaults()
    {
        return new Dictionary<string, object>
        {
            { RcStageInterstitialFirstEligibleStage, 12L },
            { RcStageInterstitialCooldownSeconds, 180.0 },
            { RcStageInterstitialMinStageGap, 5L },
            { RcIdleHintBonusEnabled, true },
            { RcIdleHintBonusDelaySeconds, 40.0 },
            { RcDailyChallengeEnabled, false },
            { RcWeeklyStageEnabled, false },
            { RcInfiniteModeEnabled, false },
            { RcLeaderboardEnabled, false },
            { "remote_config_min_fetch_interval_seconds", DefaultRemoteConfigFetchIntervalSeconds }
        };
    }

    private static bool TryGetRemoteConfigValue(string key, out ConfigValue value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(key) || !remoteConfigInitialized)
            return false;

        try
        {
            value = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FirebaseBootstrap] Remote Config read failed ({key}): {ex.Message}");
            return false;
        }
    }

    private static object GetRemoteDefault(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return remoteConfigDefaults.TryGetValue(key, out object value) ? value : null;
    }

    private static bool LoadCollectionFlag(string key, bool defaultValue)
    {
        if (!PlayerPrefs.HasKey(key))
            return defaultValue;

        return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
    }

    private static void ApplyCrashlyticsCollectionSetting(bool enabled)
    {
        try
        {
            MethodInfo setMethod = typeof(Crashlytics).GetMethod("SetCrashlyticsCollectionEnabled", BindingFlags.Public | BindingFlags.Static);
            if (setMethod != null)
            {
                setMethod.Invoke(null, new object[] { enabled });
                return;
            }

            PropertyInfo collectionProperty = typeof(Crashlytics).GetProperty("IsCrashlyticsCollectionEnabled", BindingFlags.Public | BindingFlags.Static);
            if (collectionProperty != null && collectionProperty.CanWrite)
                collectionProperty.SetValue(null, enabled, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FirebaseBootstrap] Crashlytics collection toggle failed: {ex.Message}");
        }
    }
}
