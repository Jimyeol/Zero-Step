using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class GameplayFeedbackValidation
{
    private const string MenuPath = "Zero Step/Feedback/Run Checks";
    private const string ScreenshotPath = "docs/feedback-qa/current.png";
    private const string EffectLevelPreferenceKey = "ZeroStep.EffectLevel";
    private const int ExpectedPoolSize = 64;

    private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem(MenuPath)]
    public static void RunChecks()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[FeedbackQA] FAIL playModeRequired");
            return;
        }

        var results = new List<string>();
        bool allPassed = true;
        GameplayFeedback.EffectLevel originalLevel = GameplayFeedback.Level;
        bool hadEffectLevelPreference = PlayerPrefs.HasKey(EffectLevelPreferenceKey);
        int originalEffectLevelPreference = PlayerPrefs.GetInt(EffectLevelPreferenceKey, 0);
        object originalInstance = GetStaticField("Instance");
        object originalCachedLevel = GetStaticField("cachedLevel");
        GameObject temporaryFeedbackObject = null;
        GameObject temporaryCameraObject = null;
        GameObject tileAObject = null;
        GameObject tileBObject = null;
        Camera camera = null;
        Vector3 cameraPosition = Vector3.zero;
        Quaternion cameraRotation = Quaternion.identity;
        float cameraSize = 0f;
        List<BackgroundSnapshot> backgroundSnapshots = SnapshotBackgrounds();

        try
        {
            temporaryFeedbackObject = new GameObject("FeedbackQA_GameplayFeedback");
            temporaryFeedbackObject.hideFlags = HideFlags.HideAndDontSave;
            GameplayFeedback feedback = temporaryFeedbackObject.AddComponent<GameplayFeedback>();

            if (feedback == null)
                throw new InvalidOperationException("GameplayFeedback instance could not be created or found.");

            temporaryCameraObject = new GameObject("FeedbackQA_Camera");
            temporaryCameraObject.hideFlags = HideFlags.HideAndDontSave;
            camera = temporaryCameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            cameraPosition = camera.transform.position;
            cameraRotation = camera.transform.rotation;
            cameraSize = camera.orthographicSize;

            tileAObject = CreateTileObject("FeedbackQA_TileA", new Vector3(-0.5f, 0f, 0f), 2);
            tileBObject = CreateTileObject("FeedbackQA_TileB", new Vector3(0.5f, 0f, 0f), 2);
            Tile tileA = tileAObject.GetComponent<Tile>();
            Tile tileB = tileBObject.GetComponent<Tile>();
            BoxCollider2D colliderA = tileAObject.GetComponent<BoxCollider2D>();
            BoxCollider2D colliderB = tileBObject.GetComponent<BoxCollider2D>();
            Tile[] targets = { tileB };

            feedback.Configure(camera, null, null, null);
            feedback.BeginStage(1f);

            int countABefore = tileA.CurrentNumber;
            int countBBefore = tileB.CurrentNumber;
            bool colliderABefore = colliderA != null && colliderA.enabled;
            bool colliderBBefore = colliderB != null && colliderB.enabled;
            Vector3 cameraBeforeBasic = camera.transform.position;
            float cameraSizeBeforeBasic = camera.orthographicSize;

            feedback.DragStarted(tileA);
            feedback.BeginMove(tileA, tileB);
            feedback.StepAccepted(tileA, tileB);
            feedback.CrossBlast(tileA, targets, targets.Length);
            feedback.TwinLink(tileA, tileB);
            feedback.RevealLink(tileA, tileB, 0.15f);
            feedback.HiddenRevealed(tileB);
            feedback.Blackout(tileA);
            TickLateUpdate(feedback, 4);

            bool stateUnchanged =
                tileA.CurrentNumber == countABefore &&
                tileB.CurrentNumber == countBBefore &&
                colliderA != null && colliderA.enabled == colliderABefore &&
                colliderB != null && colliderB.enabled == colliderBBefore &&
                Approximately(camera.transform.position, cameraBeforeBasic) &&
                Mathf.Approximately(camera.orthographicSize, cameraSizeBeforeBasic);
            AddResult(results, ref allPassed, "stateUnchangedByCues", stateUnchanged);

            for (int i = 0; i < 140; i++)
            {
                feedback.CrossBlast(tileA, targets, targets.Length);
                feedback.Cleared(tileB);
            }
            TickLateUpdate(feedback, 6);
            AddResult(results, ref allPassed, "poolCapacity64", feedback.EffectCapacity == ExpectedPoolSize);
            AddResult(results, ref allPassed, "poolBoundedUnderBurstStorm", feedback.ActiveEffectCount <= ExpectedPoolSize);

            GameplayFeedback.Level = GameplayFeedback.EffectLevel.Full;
            feedback.BeginStage(1f);
            feedback.DragStarted(tileA);
            feedback.StepAccepted(tileA, tileB);
            TickLateUpdate(feedback, 1);
            bool fullHasIntensity = Mathf.Approximately(GameplayFeedback.Intensity, 1f) && feedback.ActiveEffectCount > 0;

            GameplayFeedback.Level = GameplayFeedback.EffectLevel.Reduced;
            feedback.BeginStage(1f);
            feedback.DragStarted(tileA);
            feedback.StepAccepted(tileA, tileB);
            TickLateUpdate(feedback, 1);
            bool reducedHasIntensity = Mathf.Approximately(GameplayFeedback.Intensity, 0.45f) && feedback.ActiveEffectCount > 0;

            GameplayFeedback.Level = GameplayFeedback.EffectLevel.Minimal;
            feedback.BeginStage(1f);
            feedback.DragStarted(tileA);
            feedback.StepAccepted(tileA, tileB);
            TickLateUpdate(feedback, 1);
            bool minimalSuppressesDecorativeEffects = Mathf.Approximately(GameplayFeedback.Intensity, 0f) && feedback.ActiveEffectCount == 0;

            AddResult(results, ref allPassed, "effectLevelTransitions", fullHasIntensity && reducedHasIntensity && minimalSuppressesDecorativeEffects);

            feedback.BeginStage(1f);
            feedback.Progress(1, 10, tileA);
            bool nearCompletionSet = GetInstanceBool(feedback, "nearCompletion");
            feedback.BeginStage(1f);
            bool nearCompletionReset = !GetInstanceBool(feedback, "nearCompletion");
            AddResult(results, ref allPassed, "beginStageResetsNearCompletion", nearCompletionSet && nearCompletionReset);

            GameplayFeedback.Level = GameplayFeedback.EffectLevel.Full;
            feedback.BeginStage(1f);
            HiddenTile hidden = tileBObject.AddComponent<HiddenTile>();
            hidden.Setup("feedback-qa");
            feedback.RevealLink(tileA, tileB, 0f);
            hidden.ActivateWithDelay(1f, null, tileA);
            TickLateUpdate(feedback, 1);
            bool hiddenStayedPrivate = !colliderB.enabled && feedback.ActiveEffectCount == 0;
            hidden.ResetToHiddenState();
            int liveCallbacks = 0;
            hidden.ActivateWithDelay(0f, _ => liveCallbacks++, tileA);
            TickLateUpdate(feedback, 1);
            AddResult(results, ref allPassed, "hiddenFeedbackOnlyWhenLive",
                hiddenStayedPrivate && colliderB.enabled && liveCallbacks == 1 && feedback.ActiveEffectCount > 0);

            GameplayFeedback.Level = GameplayFeedback.EffectLevel.Full;
            feedback.BeginStage(1f);
            camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            camera.orthographicSize = cameraSize;
            feedback.Cleared(tileA);
            InvokePrivate(feedback, "UpdateCameraImpulse", 0.05f);
            bool cameraMoved = !Approximately(camera.transform.position, cameraPosition) ||
                               !Mathf.Approximately(camera.orthographicSize, cameraSize);
            feedback.ResetFeedback();
            bool cameraRestored = Approximately(camera.transform.position, cameraPosition) &&
                                  Mathf.Approximately(camera.orthographicSize, cameraSize);
            AddResult(results, ref allPassed, "cameraImpulseRestore", cameraMoved && cameraRestored);

            AddResult(results, ref allPassed, "backgroundResourcesUnchanged", BackgroundsMatch(backgroundSnapshots));
            RunBackgroundFlowCheck(results, ref allPassed, backgroundSnapshots);

            TryCaptureScreenshot(results);
        }
        catch (Exception ex)
        {
            allPassed = false;
            results.Add("exception=" + ex.GetType().Name + ":" + ex.Message);
        }
        finally
        {
            GameplayFeedback.Level = originalLevel;
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                camera.orthographicSize = cameraSize;
            }

            DestroyTemporary(tileAObject);
            DestroyTemporary(tileBObject);
            DestroyTemporary(temporaryCameraObject);
            DestroyTemporary(temporaryFeedbackObject);
            SetStaticField("Instance", originalInstance);
            SetStaticField("cachedLevel", originalCachedLevel);
            if (hadEffectLevelPreference)
                PlayerPrefs.SetInt(EffectLevelPreferenceKey, originalEffectLevelPreference);
            else
                PlayerPrefs.DeleteKey(EffectLevelPreferenceKey);
            PlayerPrefs.Save();
        }

        Debug.Log("[FeedbackQA] " + (allPassed ? "PASS " : "FAIL ") + string.Join("; ", results));
    }

    private static GameObject CreateTileObject(string name, Vector3 position, int number)
    {
        GameObject go = new GameObject(name);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.position = position;
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        Tile tile = go.AddComponent<Tile>();
        tile.SetNumber(number);
        return go;
    }

    private static void TickLateUpdate(GameplayFeedback feedback, int frameCount)
    {
        MethodInfo lateUpdate = typeof(GameplayFeedback).GetMethod("LateUpdate", InstanceFlags);
        if (lateUpdate == null)
            throw new MissingMethodException(nameof(GameplayFeedback), "LateUpdate");

        for (int i = 0; i < Mathf.Max(1, frameCount); i++)
            lateUpdate.Invoke(feedback, null);
    }

    private static void InvokePrivate(GameplayFeedback feedback, string methodName, params object[] args)
    {
        MethodInfo method = typeof(GameplayFeedback).GetMethod(methodName, InstanceFlags);
        if (method == null)
            throw new MissingMethodException(nameof(GameplayFeedback), methodName);
        method.Invoke(feedback, args);
    }

    private static void InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        if (method == null)
            throw new MissingMethodException(target.GetType().Name, methodName);
        method.Invoke(target, args);
    }

    private static object GetStaticField(string fieldName)
    {
        FieldInfo field = typeof(GameplayFeedback).GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
            return field.GetValue(null);

        PropertyInfo property = typeof(GameplayFeedback).GetProperty(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
            return property.GetValue(null, null);

        FieldInfo backingField = typeof(GameplayFeedback).GetField("<" + fieldName + ">k__BackingField", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return backingField != null ? backingField.GetValue(null) : null;
    }

    private sealed class BackgroundSnapshot
    {
        public ProceduralGridBackground background;
        public object gridTexture;
        public object glowTexture;
        public object baseMaterial;
        public object gridMaterial;
        public object glowMaterial;
        public object coreGlowMaterial;
        public int textureRebuildCount;
        public int materialRebuildCount;
        public float feedbackEnergy;
        public float feedbackBlackoutTime;
        public Vector2 gridScrollOffset;
    }

    private static List<BackgroundSnapshot> SnapshotBackgrounds()
    {
        var snapshots = new List<BackgroundSnapshot>();
        ProceduralGridBackground[] backgrounds = UnityEngine.Object.FindObjectsByType<ProceduralGridBackground>(FindObjectsSortMode.None);
        for (int i = 0; i < backgrounds.Length; i++)
        {
            ProceduralGridBackground background = backgrounds[i];
            snapshots.Add(new BackgroundSnapshot
            {
                background = background,
                gridTexture = GetInstanceField(background, "gridTexture"),
                glowTexture = GetInstanceField(background, "glowTexture"),
                baseMaterial = GetInstanceField(background, "baseMaterial"),
                gridMaterial = GetInstanceField(background, "gridMaterial"),
                glowMaterial = GetInstanceField(background, "glowMaterial"),
                coreGlowMaterial = GetInstanceField(background, "coreGlowMaterial"),
                textureRebuildCount = background.TextureRebuildCount,
                materialRebuildCount = background.MaterialRebuildCount,
                feedbackEnergy = GetInstanceFloat(background, "feedbackEnergy"),
                feedbackBlackoutTime = GetInstanceFloat(background, "feedbackBlackoutTime"),
                gridScrollOffset = GetInstanceVector2(background, "gridScrollOffset")
            });
        }
        return snapshots;
    }

    private static bool BackgroundsMatch(List<BackgroundSnapshot> snapshots)
    {
        if (snapshots == null)
            return true;

        ProceduralGridBackground[] current = UnityEngine.Object.FindObjectsByType<ProceduralGridBackground>(FindObjectsSortMode.None);
        if (current.Length != snapshots.Count)
            return false;

        for (int i = 0; i < snapshots.Count; i++)
        {
            BackgroundSnapshot snapshot = snapshots[i];
            if (snapshot.background == null)
                return false;
            if (!ReferenceEquals(GetInstanceField(snapshot.background, "gridTexture"), snapshot.gridTexture))
                return false;
            if (!ReferenceEquals(GetInstanceField(snapshot.background, "glowTexture"), snapshot.glowTexture))
                return false;
            if (!ReferenceEquals(GetInstanceField(snapshot.background, "baseMaterial"), snapshot.baseMaterial))
                return false;
            if (!ReferenceEquals(GetInstanceField(snapshot.background, "gridMaterial"), snapshot.gridMaterial))
                return false;
            if (!ReferenceEquals(GetInstanceField(snapshot.background, "glowMaterial"), snapshot.glowMaterial))
                return false;
            if (!ReferenceEquals(GetInstanceField(snapshot.background, "coreGlowMaterial"), snapshot.coreGlowMaterial))
                return false;
            if (snapshot.background.TextureRebuildCount != snapshot.textureRebuildCount)
                return false;
            if (snapshot.background.MaterialRebuildCount != snapshot.materialRebuildCount)
                return false;
            if (!Mathf.Approximately(GetInstanceFloat(snapshot.background, "feedbackEnergy"), snapshot.feedbackEnergy))
                return false;
            if (!Mathf.Approximately(GetInstanceFloat(snapshot.background, "feedbackBlackoutTime"), snapshot.feedbackBlackoutTime))
                return false;
            if (!Approximately(GetInstanceVector2(snapshot.background, "gridScrollOffset"), snapshot.gridScrollOffset))
                return false;
        }
        return true;
    }

    private static void RunBackgroundFlowCheck(List<string> results, ref bool allPassed, List<BackgroundSnapshot> snapshots)
    {
        if (snapshots == null || snapshots.Count == 0)
        {
            results.Add("backgroundFlow=SKIP(noBackground)");
            return;
        }

        BackgroundSnapshot snapshot = snapshots[0];
        ProceduralGridBackground background = snapshot.background;
        if (background == null)
        {
            AddResult(results, ref allPassed, "backgroundFlow", false);
            return;
        }

        try
        {
            background.SetFeedbackEnergy(0.85f);
            background.PulseFeedbackBlackout();
            InvokePrivate(background, "AdvanceGridMotion");

            bool noRebuild =
                ReferenceEquals(GetInstanceField(background, "gridTexture"), snapshot.gridTexture) &&
                ReferenceEquals(GetInstanceField(background, "glowTexture"), snapshot.glowTexture) &&
                ReferenceEquals(GetInstanceField(background, "baseMaterial"), snapshot.baseMaterial) &&
                ReferenceEquals(GetInstanceField(background, "gridMaterial"), snapshot.gridMaterial) &&
                ReferenceEquals(GetInstanceField(background, "glowMaterial"), snapshot.glowMaterial) &&
                ReferenceEquals(GetInstanceField(background, "coreGlowMaterial"), snapshot.coreGlowMaterial) &&
                background.TextureRebuildCount == snapshot.textureRebuildCount &&
                background.MaterialRebuildCount == snapshot.materialRebuildCount;

            RestoreBackgroundMotion(snapshot);
            bool restored =
                Mathf.Approximately(GetInstanceFloat(background, "feedbackEnergy"), snapshot.feedbackEnergy) &&
                Mathf.Approximately(GetInstanceFloat(background, "feedbackBlackoutTime"), snapshot.feedbackBlackoutTime) &&
                Approximately(GetInstanceVector2(background, "gridScrollOffset"), snapshot.gridScrollOffset);

            AddResult(results, ref allPassed, "backgroundFlowNoRebuildAndRestored", noRebuild && restored);
        }
        catch (Exception ex)
        {
            RestoreBackgroundMotion(snapshot);
            allPassed = false;
            results.Add("backgroundFlow=FAIL(" + ex.GetType().Name + ")");
        }
    }

    private static void RestoreBackgroundMotion(BackgroundSnapshot snapshot)
    {
        if (snapshot == null || snapshot.background == null)
            return;

        SetInstanceField(snapshot.background, "feedbackEnergy", snapshot.feedbackEnergy);
        SetInstanceField(snapshot.background, "feedbackBlackoutTime", snapshot.feedbackBlackoutTime);
        SetInstanceField(snapshot.background, "gridScrollOffset", snapshot.gridScrollOffset);
    }

    private static object GetInstanceField(object target, string fieldName)
    {
        if (target == null)
            return null;
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        return field != null ? field.GetValue(target) : null;
    }

    private static float GetInstanceFloat(object target, string fieldName)
    {
        object value = GetInstanceField(target, fieldName);
        return value is float floatValue ? floatValue : 0f;
    }

    private static bool GetInstanceBool(object target, string fieldName)
    {
        object value = GetInstanceField(target, fieldName);
        return value is bool boolValue && boolValue;
    }

    private static Vector2 GetInstanceVector2(object target, string fieldName)
    {
        object value = GetInstanceField(target, fieldName);
        return value is Vector2 vector ? vector : Vector2.zero;
    }

    private static void SetInstanceField(object target, string fieldName, object value)
    {
        if (target == null)
            return;

        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        if (field != null)
            field.SetValue(target, value);
    }

    private static void SetStaticField(string fieldName, object value)
    {
        FieldInfo field = typeof(GameplayFeedback).GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(null, value);
            return;
        }

        FieldInfo backingField = typeof(GameplayFeedback).GetField("<" + fieldName + ">k__BackingField", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (backingField != null)
        {
            backingField.SetValue(null, value);
            return;
        }

        PropertyInfo property = typeof(GameplayFeedback).GetProperty(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.GetSetMethod(true) != null)
            property.SetValue(null, value, null);
    }

    private static void AddResult(List<string> results, ref bool allPassed, string name, bool passed)
    {
        if (!passed)
            allPassed = false;
        results.Add(name + "=" + (passed ? "PASS" : "FAIL"));
    }

    private static bool Approximately(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude <= 0.000001f;
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return (a - b).sqrMagnitude <= 0.000001f;
    }

    private static void TryCaptureScreenshot(List<string> results)
    {
        try
        {
            string directory = Path.GetDirectoryName(ScreenshotPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(ScreenshotPath);
            results.Add("screenshot=" + ScreenshotPath);
        }
        catch (Exception ex)
        {
            results.Add("screenshotSkipped=" + ex.GetType().Name);
        }
    }

    private static void DestroyTemporary(UnityEngine.Object obj)
    {
        if (obj == null)
            return;

        UnityEngine.Object.DestroyImmediate(obj);
    }
}
