using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play-mode only visual QA driver for GameplayFeedback. It only invokes cosmetic feedback callbacks.
/// </summary>
public static class GameplayFeedbackPreview
{
    private const string MenuRoot = "Zero Step/Feedback/";
    private const string OutputDirectory = "docs/feedback-qa";
    private const string TemporarySizePrefix = "Feedback QA ";
    private const double CaptureDelaySeconds = 0.06d;

    private enum PreviewCue
    {
        StepZero,
        Specials,
        Clear
    }

    private struct ScheduledCue
    {
        public double time;
        public PreviewCue cue;
        public string screenshotName;
        public bool fired;
        public bool captured;
    }

    private struct GameViewState
    {
        public bool valid;
        public EditorWindow gameView;
        public object group;
        public int originalIndex;
        public int temporaryIndex;
        public string temporaryLabel;
        public int requestedWidth;
        public int requestedHeight;
    }

    private static readonly Tile[] CrossTargets = new Tile[4];

    private static ScheduledCue[] cues;
    private static string activeProfile;
    private static GameViewState gameViewState;
    private static GameManager gameManager;
    private static GameplayFeedback feedback;
    private static Camera previewCamera;
    private static bool gameManagerWasEnabled;
    private static bool runInBackgroundWasEnabled;
    private static Vector3 cameraPosition;
    private static Quaternion cameraRotation;
    private static float cameraOrthographicSize;
    private static Rect cameraRect;
    private static Tile sourceTile;
    private static Tile targetTile;
    private static Tile thirdTile;
    private static double startedAt;
    private static bool previewActive;

    static GameplayFeedbackPreview()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem(MenuRoot + "Preview Tall Phone")]
    public static void PreviewTallPhone()
    {
        BeginPreview("tall-phone", 390, 844);
    }

    [MenuItem(MenuRoot + "Preview Tablet")]
    public static void PreviewTablet()
    {
        BeginPreview("tablet", 768, 1024);
    }

    [MenuItem(MenuRoot + "Preview Compact")]
    public static void PreviewCompact()
    {
        BeginPreview("compact", 640, 480);
    }

    [MenuItem(MenuRoot + "End Preview")]
    public static void EndPreviewMenu()
    {
        EndPreview("manual");
    }

    [MenuItem(MenuRoot + "Preview Tall Phone", true)]
    [MenuItem(MenuRoot + "Preview Tablet", true)]
    [MenuItem(MenuRoot + "Preview Compact", true)]
    private static bool ValidateBeginPreview()
    {
        return Application.isPlaying && !previewActive;
    }

    [MenuItem(MenuRoot + "End Preview", true)]
    private static bool ValidateEndPreview()
    {
        return previewActive;
    }

    private static void BeginPreview(string profile, int width, int height)
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[FeedbackPreview] FAIL playModeRequired");
            return;
        }

        GameMainUIController ui = UnityEngine.Object.FindFirstObjectByType<GameMainUIController>();
        if (ui != null && ui.IsModalOverlayOpen)
        {
            Debug.LogError("[FeedbackPreview] FAIL modalOverlayOpen");
            return;
        }

        feedback = GameplayFeedback.Instance != null
            ? GameplayFeedback.Instance
            : UnityEngine.Object.FindFirstObjectByType<GameplayFeedback>();
        if (feedback == null)
        {
            Debug.LogError("[FeedbackPreview] FAIL missingGameplayFeedback");
            return;
        }

        Tile[] liveTiles = UnityEngine.Object.FindObjectsByType<Tile>(FindObjectsSortMode.None);
        if (!ChooseTiles(liveTiles))
        {
            Debug.LogError("[FeedbackPreview] FAIL needAtLeastTwoLiveTiles");
            return;
        }

        gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        previewCamera = Camera.main;
        if (previewCamera == null && gameManager != null)
            previewCamera = gameManager.GetComponentInChildren<Camera>();
        if (previewCamera == null)
            previewCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();

        if (!SetGameViewSize(width, height, out gameViewState))
        {
            Debug.LogError($"[FeedbackPreview] FAIL gameViewSizeUnavailable profile={profile} size={width}x{height}");
            return;
        }

        Directory.CreateDirectory(OutputDirectory);

        activeProfile = profile;
        startedAt = EditorApplication.timeSinceStartup;
        cues = new[]
        {
            new ScheduledCue { time = 0.12d, cue = PreviewCue.StepZero, screenshotName = "step-zero" },
            new ScheduledCue { time = 0.92d, cue = PreviewCue.Specials, screenshotName = "specials" },
            new ScheduledCue { time = 1.78d, cue = PreviewCue.Clear, screenshotName = "clear" }
        };

        if (gameManager != null)
        {
            gameManagerWasEnabled = gameManager.enabled;
            gameManager.enabled = false;
        }

        if (previewCamera != null)
        {
            cameraPosition = previewCamera.transform.position;
            cameraRotation = previewCamera.transform.rotation;
            cameraOrthographicSize = previewCamera.orthographic ? previewCamera.orthographicSize : 0f;
            cameraRect = previewCamera.rect;
        }

        runInBackgroundWasEnabled = Application.runInBackground;
        Application.runInBackground = true;
        previewActive = true;
        feedback.ResetFeedback();
        EditorApplication.update += UpdatePreview;
        FocusGameView();
        Debug.Log($"[FeedbackPreview] START profile={profile} size={width}x{height}");
    }

    private static bool ChooseTiles(Tile[] tiles)
    {
        sourceTile = null;
        targetTile = null;
        thirdTile = null;
        Array.Clear(CrossTargets, 0, CrossTargets.Length);

        if (tiles == null)
            return false;

        int crossCount = 0;
        for (int i = 0; i < tiles.Length; i++)
        {
            Tile tile = tiles[i];
            if (tile == null || !tile.IsActive)
                continue;

            if (sourceTile == null)
            {
                sourceTile = tile;
                continue;
            }

            if (targetTile == null)
            {
                targetTile = tile;
                CrossTargets[crossCount++] = tile;
                continue;
            }

            if (thirdTile == null)
                thirdTile = tile;

            if (crossCount < CrossTargets.Length)
                CrossTargets[crossCount++] = tile;
        }

        if (thirdTile == null)
            thirdTile = targetTile;

        return sourceTile != null && targetTile != null;
    }

    private static void UpdatePreview()
    {
        if (!previewActive)
            return;

        if (!Application.isPlaying || feedback == null)
        {
            EndPreview("play-exit");
            return;
        }

        FocusGameView();
        double elapsed = EditorApplication.timeSinceStartup - startedAt;
        for (int i = 0; i < cues.Length; i++)
        {
            ScheduledCue cue = cues[i];
            if (!cue.fired && elapsed >= cue.time)
            {
                FireCue(cue.cue);
                cue.fired = true;
            }

            if (cue.fired && !cue.captured && elapsed >= cue.time + CaptureDelaySeconds)
            {
                Capture(cue.screenshotName);
                cue.captured = true;
            }

            cues[i] = cue;
        }

        if (elapsed >= 2.65d)
            EndPreview("complete");
    }

    private static void FireCue(PreviewCue cue)
    {
        if (feedback == null || sourceTile == null || targetTile == null)
            return;

        switch (cue)
        {
            case PreviewCue.StepZero:
                feedback.DragStarted(sourceTile);
                feedback.BeginMove(sourceTile, targetTile);
                feedback.StepAccepted(sourceTile, targetTile);
                feedback.TileDecreased(targetTile, targetTile.CurrentNumber + 1);
                break;

            case PreviewCue.Specials:
                feedback.TwinLink(sourceTile, targetTile);
                feedback.RevealLink(sourceTile, thirdTile, 0.16f);
                feedback.CrossBlast(sourceTile, CrossTargets, CrossTargets.Length);
                feedback.HiddenRevealed(thirdTile);
                break;

            case PreviewCue.Clear:
                feedback.Cleared(targetTile);
                break;
        }
    }

    private static void Capture(string cueName)
    {
        FocusGameView();
        string path = Path.Combine(OutputDirectory, activeProfile + "-" + cueName + ".png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[FeedbackPreview] CAPTURE {path} screenshotPixels={Screen.width}x{Screen.height} requested={gameViewState.requestedWidth}x{gameViewState.requestedHeight}");
    }

    private static void EndPreview(string reason)
    {
        if (!previewActive)
            return;

        EditorApplication.update -= UpdatePreview;
        previewActive = false;

        if (feedback != null)
            feedback.ResetFeedback();

        if (gameManager != null)
            gameManager.enabled = gameManagerWasEnabled;

        Application.runInBackground = runInBackgroundWasEnabled;

        if (previewCamera != null)
        {
            previewCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            if (previewCamera.orthographic)
                previewCamera.orthographicSize = cameraOrthographicSize;
            previewCamera.rect = cameraRect;
        }

        RestoreGameViewSize(gameViewState);
        Debug.Log($"[FeedbackPreview] END reason={reason}");

        activeProfile = null;
        cues = null;
        gameManager = null;
        feedback = null;
        previewCamera = null;
        sourceTile = null;
        targetTile = null;
        thirdTile = null;
        Array.Clear(CrossTargets, 0, CrossTargets.Length);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            EndPreview("play-mode-state-" + state);
    }

    private static bool SetGameViewSize(int width, int height, out GameViewState state)
    {
        state = default;
        try
        {
            object group = CurrentGameViewSizeGroup();
            EditorWindow gameView = ExistingOrNewGameView();
            if (group == null || gameView == null)
                return false;

            int originalIndex = GetSelectedSizeIndex(gameView, group);
            int index = FindSizeIndex(group, width, height);
            int temporaryIndex = -1;
            string temporaryLabel = null;
            bool addedTemporarySize = false;
            string ownedLabel = TemporarySizePrefix + width + "x" + height;
            if (index >= 0 && FindSizeIndex(group, width, height, ownedLabel) == index)
            {
                temporaryLabel = ownedLabel;
                temporaryIndex = index;
            }
            if (index < 0)
            {
                temporaryLabel = TemporarySizePrefix + width + "x" + height;
                AddCustomSize(group, width, height, temporaryLabel);
                addedTemporarySize = true;
                index = FindSizeIndex(group, width, height, temporaryLabel);
                temporaryIndex = index;
            }

            if (index < 0)
            {
                if (addedTemporarySize)
                {
                    int addedIndex = FindSizeIndex(group, 0, 0, temporaryLabel);
                    if (addedIndex >= 0)
                        RemoveCustomSize(group, addedIndex);
                }
                SetSelectedSizeIndex(gameView, group, originalIndex);
                ApplyGameViewSizeIndex(gameView, originalIndex);
                return false;
            }

            SetSelectedSizeIndex(gameView, group, index);
            ApplyGameViewSizeIndex(gameView, index);
            state = new GameViewState
            {
                valid = true,
                gameView = gameView,
                group = group,
                originalIndex = originalIndex,
                temporaryIndex = temporaryIndex,
                temporaryLabel = temporaryLabel,
                requestedWidth = width,
                requestedHeight = height
            };
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[FeedbackPreview] FAIL gameViewReflection " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    private static void RestoreGameViewSize(GameViewState state)
    {
        if (!state.valid || state.group == null)
            return;

        try
        {
            SetSelectedSizeIndex(state.gameView, state.group, state.originalIndex);
            ApplyGameViewSizeIndex(state.gameView, state.originalIndex);
            if (state.temporaryIndex >= 0)
            {
                int index = FindSizeIndex(state.group, 0, 0, state.temporaryLabel);
                if (index >= 0)
                    RemoveCustomSize(state.group, index);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[FeedbackPreview] GameView restore warning " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static object CurrentGameViewSizeGroup()
    {
        Type sizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
        Type groupType = Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor");
        if (sizesType == null || groupType == null)
            return null;

        object sizesInstance = sizesType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)?.GetValue(null, null);
        MethodInfo getGroup = sizesType.GetMethod("GetGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        object currentGroup = CurrentGameViewSizeGroupType(sizesType, groupType);
        return getGroup?.Invoke(sizesInstance, new[] { currentGroup });
    }

    private static object CurrentGameViewSizeGroupType(Type sizesType, Type groupType)
    {
        PropertyInfo sizesProperty = sizesType.GetProperty("currentGroupType", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
        if (sizesProperty != null)
        {
            MethodInfo getter = sizesProperty.GetGetMethod(true);
            object owner = getter != null && getter.IsStatic
                ? null
                : sizesType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)?.GetValue(null, null);
            return sizesProperty.GetValue(owner, null);
        }

        Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        EditorWindow gameView = ExistingOrNewGameView();
        PropertyInfo property = gameViewType?.GetProperty("currentSizeGroupType", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? gameViewType?.GetProperty("currentGameViewSizeGroupType", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
        {
            MethodInfo getter = property.GetGetMethod(true);
            return property.GetValue(getter != null && getter.IsStatic ? null : gameView, null);
        }

        if (Enum.IsDefined(groupType, "Android"))
            return Enum.Parse(groupType, "Android");

        return Enum.Parse(groupType, "Standalone");
    }

    private static int GetSelectedSizeIndex(EditorWindow gameView, object group)
    {
        object source = gameView != null ? (object)gameView : group;
        PropertyInfo property = source.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
            return (int)property.GetValue(source, null);

        FieldInfo field = source.GetType().GetField("m_SelectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
            return (int)field.GetValue(source);

        property = group.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
            return (int)property.GetValue(group, null);

        field = group.GetType().GetField("m_SelectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field != null ? (int)field.GetValue(group) : 0;
    }

    private static void SetSelectedSizeIndex(EditorWindow gameView, object group, int index)
    {
        object source = gameView != null ? (object)gameView : group;
        PropertyInfo property = source.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
        {
            property.SetValue(source, index, null);
            return;
        }

        FieldInfo field = source.GetType().GetField("m_SelectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(source, index);
            return;
        }

        property = group.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
        {
            property.SetValue(group, index, null);
            return;
        }

        field = group.GetType().GetField("m_SelectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        field?.SetValue(group, index);
    }

    private static void ApplyGameViewSizeIndex(EditorWindow gameView, int index)
    {
        Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType == null || gameView == null)
            return;

        MethodInfo callback = gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (callback != null)
            callback.Invoke(gameView, new object[] { index, null });
        else
            gameView.Repaint();
    }

    private static int FindSizeIndex(object group, int width, int height, string label = null)
    {
        int count = GetTotalSizeCount(group);
        MethodInfo getSize = group.GetType().GetMethod("GetGameViewSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < count; i++)
        {
            object size = getSize.Invoke(group, new object[] { i });
            int sizeWidth = GetSizeInt(size, "width");
            int sizeHeight = GetSizeInt(size, "height");
            string sizeText = GetSizeText(size);
            bool labelMatches = string.IsNullOrEmpty(label) || string.Equals(sizeText, label, StringComparison.Ordinal);
            bool dimensionsMatch = width <= 0 || (sizeWidth == width && sizeHeight == height);
            if (labelMatches && dimensionsMatch)
                return i;
        }

        return -1;
    }

    private static int GetTotalSizeCount(object group)
    {
        int builtin = InvokeInt(group, "GetBuiltinCount");
        int custom = InvokeInt(group, "GetCustomCount");
        return builtin + custom;
    }

    private static int InvokeInt(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return method != null ? (int)method.Invoke(target, null) : 0;
    }

    private static int GetSizeInt(object size, string propertyName)
    {
        PropertyInfo property = size.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
            return (int)property.GetValue(size, null);
        FieldInfo field = size.GetType().GetField("m_" + char.ToUpperInvariant(propertyName[0]) + propertyName.Substring(1), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field != null ? (int)field.GetValue(size) : -1;
    }

    private static string GetSizeText(object size)
    {
        PropertyInfo property = size.GetType().GetProperty("baseText", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
            return property.GetValue(size, null) as string;
        return size.ToString();
    }

    private static void AddCustomSize(object group, int width, int height, string label)
    {
        Type sizeType = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
        Type sizeKindType = Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
        object fixedResolution = Enum.Parse(sizeKindType, "FixedResolution");
        object size = Activator.CreateInstance(sizeType, fixedResolution, width, height, label);
        MethodInfo addCustomSize = group.GetType().GetMethod("AddCustomSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        addCustomSize.Invoke(group, new[] { size });
    }

    private static void RemoveCustomSize(object group, int index)
    {
        MethodInfo remove = group.GetType().GetMethod("RemoveCustomSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (index >= InvokeInt(group, "GetBuiltinCount"))
            remove?.Invoke(group, new object[] { index });
    }

    private static void FocusGameView()
    {
        EditorWindow gameView = ExistingOrNewGameView();
        if (gameView != null)
        {
            gameView.Focus();
        }
    }

    private static EditorWindow ExistingOrNewGameView()
    {
        Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType == null)
            return null;

        return FindExistingEditorWindow(gameViewType) ?? EditorWindow.GetWindow(gameViewType);
    }

    private static EditorWindow FindExistingEditorWindow(Type windowType)
    {
        UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(windowType);
        for (int i = 0; i < windows.Length; i++)
        {
            if (windows[i] is EditorWindow window)
                return window;
        }

        return null;
    }
}
