using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
using GoogleMobileAds.Api;
#endif

/// <summary>
/// UI Toolkit 기반 게임 상단 UI 컨트롤러.
/// STAGE 텍스트와 진행도 ProgressBar를 GameManager와 연동한다.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameMainUIController : MonoBehaviour
{
    public static bool IsVibrationEnabled { get; private set; } = true;
    private const string SaveKeySoundOn = "SettingSoundOn";
    private const string SaveKeyVibrationOn = "SettingVibrationOn";
    private const string PrivacyUrl = "https://www.naver.com";
    private const string SupportEmailAddress = "crewoongcrewoong@gmail.com";
    private const string NeonPressBaseClass = "neon-press-button";
    private const string NeonPressWarmClass = "neon-press-button-warm";
    private const string NeonPressActiveClass = "neon-press-active";
    private const string AndroidReleaseBannerAdUnitId = "ca-app-pub-1863948941169747/1159516189";
    private const string IOSReleaseBannerAdUnitId = "ca-app-pub-1863948941169747/3645749158";
    private const string AndroidTestBannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";
    private const string IOSTestBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
    private const string AndroidReleaseRewardedAdUnitId = "ca-app-pub-1863948941169747/7021684124";
    private const string IOSReleaseRewardedAdUnitId = "ca-app-pub-1863948941169747/6383389878";
    private const string AndroidTestRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
    private const string IOSTestRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";
    private const int MaxHearts = 3;
    private const string TutorialScheduleResourcePath = "Tutorials/help_tutorial_schedule";
    private const string StageSnackbarScheduleResourcePath = "Tutorials/stage_snackbar_schedule";
    private const string TutorialDismissedKeyPrefix = "TutorialDismissed_";
    private const string TutorialTypeBasicPath = "BasicPath";

    [Serializable]
    private class HelpTutorialScheduleData
    {
        public HelpTutorialEntryData[] entries;
    }

    [Serializable]
    private class HelpTutorialEntryData
    {
        public string id;
        public int stageIndex;
        public string tutorialType = TutorialTypeBasicPath;
        public string title = "기본 플레이 방법";
        public string description = "왼쪽(1) → 중앙(2) → 오른쪽(1) → 중앙으로 이동하면 카운트가 줄어들며 클리어됩니다.";
        public string closeButtonText = "확인";
    }

    [Serializable]
    private class StageSnackbarScheduleData
    {
        public StageSnackbarEntryData[] entries;
    }

    [Serializable]
    private class StageSnackbarEntryData
    {
        public string id;
        public int stageIndex;
        public int targetStageIndex;
        public string message = "새로운 타입의 타일이 열립니다! {remainingStages}스테이지 남았습니다.";
        public float duration = 2.8f;
    }

    private enum HeartRefillMode
    {
        RewardedAd,
        SessionPlayReward
    }

    [Header("UI Toolkit 참조 (자동 캐싱)")]
    [SerializeField] private UIDocument uiDocument;
    [Header("배너 레이아웃")]
    [SerializeField] private float bottomBarExtraSpacing = 26f;
    [SerializeField] [Range(0.1f, 1f)] private float bannerHeightPollInterval = 0.25f;
    [Header("스테이지 스낵바")]
    [SerializeField] private float stageSnackbarExtraSpacing = 220f;
    [SerializeField] [Range(1f, 8f)] private float stageSnackbarDefaultDuration = 2.8f;

    private Label stageTitleLabel;
    private Label stageNumberLabel;
    private ProgressBar gameProgressBar;
    private Button settingButton;
    private Image settingIcon;

    private Button skipButton;
    private Image skipIcon;
    private Button resetButton;
    private Image resetIcon;
    private Button blockAdsButton;
    private Image blockIcon;
    private VisualElement settingPopupOverlay;
    private Button settingCloseButton;
    private Image settingCloseIcon;
    private bool isSettingPopupOpen;
    private VisualElement resetConfirmOverlay;
    private Button resetConfirmCancelButton;
    private Button resetConfirmOkButton;

    private Button soundSwitchButton;
    private Label soundSwitchLabel;
    private Button vibrationSwitchButton;
    private Label vibrationSwitchLabel;
    private Image soundIcon;
    private Image vibrationIcon;
    private Image helpIcon;
    private Image languageIcon;

    private Button helpButton;
    private Button languageButton;
    private Button rateButton;
    private Button removeAdsButton;
    private Button emailButton;
    private Button resetDataButton;
    private Button privacyPolicyButton;
    private Button termsButton;
    private VisualElement tutorialOverlay;
    private VisualElement tutorialDialog;
    private Label tutorialTitleLabel;
    private Label tutorialDescriptionLabel;
    private Label tutorialStepHintLabel;
    private Button tutorialCloseButton;
    private Image tutorialCloseIcon;
    private Button tutorialConfirmButton;
    private VisualElement tutorialTileLeft;
    private VisualElement tutorialTileCenter;
    private VisualElement tutorialTileRight;
    private Label tutorialTileLeftCount;
    private Label tutorialTileCenterCount;
    private Label tutorialTileRightCount;
    private Image tutorialHandImage;
    private Image heart1Image;
    private Image heart2Image;
    private Image heart3Image;
    private VisualElement heartDepletedOverlay;
    private VisualElement heartDepletedDialog;
    private Label heartDepletedTitleLabel;
    private Label heartDepletedMessageLabel;
    private Label heartRefillRewardHintLabel;
    private Button heartRefillAdButton;
    private Label heartRefillStatusLabel;
    private VisualElement bottomBar;
    private VisualElement stageSnackbar;
    private Label stageSnackbarLabel;
    private VisualElement bannerAdContainer;
    private Label bannerAdPlaceholder;
    private Sprite heartFilledSprite;
    private Sprite heartEmptySprite;
    private bool isSoundOn = true;
    private bool isVibrationOn = true;
    private bool isDebugBuildCached;
    private volatile bool pendingBannerLoadFromInitialize;
    private volatile bool pendingRewardedAdLoadFromInitialize;
    private volatile bool pendingShowRewardedAd;
    private float reservedBannerHeightPx;
    private float nextBannerHeightPollTime;
    private int cachedScreenWidth = -1;
    private int cachedScreenHeight = -1;
    private Rect cachedSafeArea;
    private int currentHearts = MaxHearts;
    private bool isWaitingForHeartRefill;
    private bool isTutorialPopupOpen;
    private int currentStageIndexForUI = 1;
    private Coroutine tutorialAnimationRoutine;
    private HelpTutorialEntryData activeTutorialEntry;
    private readonly List<HelpTutorialEntryData> helpTutorialEntries = new List<HelpTutorialEntryData>();
    private readonly Dictionary<int, List<HelpTutorialEntryData>> helpTutorialEntriesByStage = new Dictionary<int, List<HelpTutorialEntryData>>();
    private readonly Dictionary<int, List<StageSnackbarEntryData>> stageSnackbarEntriesByStage = new Dictionary<int, List<StageSnackbarEntryData>>();
    private readonly HashSet<string> shownStageSnackbarIdsThisSession = new HashSet<string>();
    private int tutorialAnimationVersion;
    private int stageSnackbarAnimationVersion;
    private HeartRefillMode currentHeartRefillMode = HeartRefillMode.RewardedAd;
    private int currentSessionPlayRewardMinutes;
    private Coroutine stageSnackbarRoutine;
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
    private BannerView bannerView;
    private RewardedAd rewardedAd;
    private bool isRewardedAdLoading;
    private bool rewardEarnedThisShow;
#endif
    [Header("ProgressBar Animation")]
    [SerializeField] private float progressAnimDuration = 0.25f;
    private float displayedProgressValue;
    private Coroutine progressAnimRoutine;
    private readonly Dictionary<VisualElement, int> buttonPressAnimationVersion = new Dictionary<VisualElement, int>();
    private readonly Dictionary<VisualElement, int> heartAnimationVersion = new Dictionary<VisualElement, int>();
    private int heartPopupAnimationVersion;

    /// <summary>현재 스테이지 시작 시 전체 타일 카운트(합).</summary>
    private int initialTileCount;

    private void Awake()
    {
        isDebugBuildCached = Debug.isDebugBuild;
        LoadSavedSettings();
        IsVibrationEnabled = isVibrationOn;

        // UIDocument 자동 캐싱
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogWarning("[GameMainUIController] UIDocument를 찾을 수 없습니다.");
            return;
        }

        VisualElement root = uiDocument.rootVisualElement;
        StyleSheet mainStyleSheet = Resources.Load<StyleSheet>("GameMainUI");
        if (mainStyleSheet != null && !root.styleSheets.Contains(mainStyleSheet))
            root.styleSheets.Add(mainStyleSheet);

        stageTitleLabel = root.Q<Label>("StageTitle");
        stageNumberLabel = root.Q<Label>("StageNumber");
        gameProgressBar = root.Q<ProgressBar>("GameProgress");
        settingButton = root.Q<Button>("SettingButton");
        settingIcon = root.Q<Image>("SettingIcon");

        skipButton = root.Q<Button>("SkipButton");
        skipIcon = root.Q<Image>("SkipIcon");
        resetButton = root.Q<Button>("ResetButton");
        resetIcon = root.Q<Image>("ResetIcon");
        blockAdsButton = root.Q<Button>("BlockAdsButton");
        blockIcon = root.Q<Image>("BlockIcon");
        settingPopupOverlay = root.Q<VisualElement>("SettingPopupOverlay");
        settingCloseButton = root.Q<Button>("SettingCloseButton");
        settingCloseIcon = root.Q<Image>("SettingCloseIcon");
        resetConfirmOverlay = root.Q<VisualElement>("ResetConfirmOverlay");
        resetConfirmCancelButton = root.Q<Button>("ResetConfirmCancelButton");
        resetConfirmOkButton = root.Q<Button>("ResetConfirmOkButton");
        soundSwitchButton = root.Q<Button>("SoundSwitchButton");
        soundSwitchLabel = root.Q<Label>("SoundSwitchLabel");
        vibrationSwitchButton = root.Q<Button>("VibrationSwitchButton");
        vibrationSwitchLabel = root.Q<Label>("VibrationSwitchLabel");
        soundIcon = root.Q<Image>("SoundIcon");
        vibrationIcon = root.Q<Image>("VibrationIcon");
        helpIcon = root.Q<Image>("HelpIcon");
        languageIcon = root.Q<Image>("LanguageIcon");
        helpButton = root.Q<Button>("HelpButton");
        languageButton = root.Q<Button>("LanguageButton");
        rateButton = root.Q<Button>("RateButton");
        removeAdsButton = root.Q<Button>("RemoveAdsButton");
        emailButton = root.Q<Button>("EmailButton");
        resetDataButton = root.Q<Button>("ResetDataButton");
        privacyPolicyButton = root.Q<Button>("PrivacyPolicyButton");
        termsButton = root.Q<Button>("TermsButton");
        tutorialOverlay = root.Q<VisualElement>("TutorialOverlay");
        tutorialDialog = root.Q<VisualElement>("TutorialDialog");
        tutorialTitleLabel = root.Q<Label>("TutorialTitleLabel");
        tutorialDescriptionLabel = root.Q<Label>("TutorialDescriptionLabel");
        tutorialStepHintLabel = root.Q<Label>("TutorialStepHintLabel");
        tutorialCloseButton = root.Q<Button>("TutorialCloseButton");
        tutorialCloseIcon = root.Q<Image>("TutorialCloseIcon");
        tutorialConfirmButton = root.Q<Button>("TutorialConfirmButton");
        tutorialTileLeft = root.Q<VisualElement>("TutorialTileLeft");
        tutorialTileCenter = root.Q<VisualElement>("TutorialTileCenter");
        tutorialTileRight = root.Q<VisualElement>("TutorialTileRight");
        tutorialTileLeftCount = root.Q<Label>("TutorialTileLeftCount");
        tutorialTileCenterCount = root.Q<Label>("TutorialTileCenterCount");
        tutorialTileRightCount = root.Q<Label>("TutorialTileRightCount");
        tutorialHandImage = root.Q<Image>("TutorialHandImage");
        heart1Image = root.Q<Image>("Heart1Image");
        heart2Image = root.Q<Image>("Heart2Image");
        heart3Image = root.Q<Image>("Heart3Image");
        heartDepletedOverlay = root.Q<VisualElement>("HeartDepletedOverlay");
        heartDepletedDialog = root.Q<VisualElement>("HeartDepletedDialog");
        heartDepletedTitleLabel = root.Q<Label>("HeartDepletedTitle");
        heartDepletedMessageLabel = root.Q<Label>("HeartDepletedMessage");
        heartRefillRewardHintLabel = root.Q<Label>("HeartRefillRewardHint");
        heartRefillAdButton = root.Q<Button>("HeartRefillAdButton");
        heartRefillStatusLabel = root.Q<Label>("HeartRefillStatusLabel");
        bottomBar = root.Q<VisualElement>("BottomBar");
        stageSnackbar = root.Q<VisualElement>("StageSnackbar");
        stageSnackbarLabel = root.Q<Label>("StageSnackbarLabel");
        bannerAdContainer = root.Q<VisualElement>("BannerAdContainer");
        bannerAdPlaceholder = root.Q<Label>("BannerAdPlaceholder");

        if (settingPopupOverlay != null)
            settingPopupOverlay.style.display = DisplayStyle.None;
        if (resetConfirmOverlay != null)
            resetConfirmOverlay.style.display = DisplayStyle.None;
        if (heartDepletedOverlay != null)
            heartDepletedOverlay.style.display = DisplayStyle.None;
        if (tutorialOverlay != null)
            tutorialOverlay.style.display = DisplayStyle.None;
        if (stageSnackbar != null)
        {
            stageSnackbar.style.display = DisplayStyle.None;
            stageSnackbar.style.opacity = 0f;
            stageSnackbar.style.scale = new StyleScale(new Scale(new Vector3(0.96f, 0.96f, 1f)));
        }

        if (gameProgressBar != null)
        {
            // 기본값 초기화
            gameProgressBar.lowValue = 0f;
            gameProgressBar.highValue = 1f;
            gameProgressBar.value = 0f;
            displayedProgressValue = 0f;
            gameProgressBar.title = string.Empty;
            gameProgressBar.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            gameProgressBar.style.borderLeftColor = Color.white;
            gameProgressBar.style.borderRightColor = Color.white;
            gameProgressBar.style.borderTopColor = Color.white;
            gameProgressBar.style.borderBottomColor = Color.white;
            gameProgressBar.style.borderLeftWidth = 3f;
            gameProgressBar.style.borderRightWidth = 3f;
            gameProgressBar.style.borderTopWidth = 3f;
            gameProgressBar.style.borderBottomWidth = 3f;

            VisualElement progressBackground = gameProgressBar.Q(className: ProgressBar.backgroundUssClassName);
            if (progressBackground != null)
            {
                progressBackground.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
                progressBackground.style.marginTop = 0f;
                progressBackground.style.marginBottom = 0f;
                progressBackground.style.marginLeft = 0f;
                progressBackground.style.marginRight = 0f;
                progressBackground.style.paddingTop = 0f;
                progressBackground.style.paddingBottom = 0f;
                progressBackground.style.paddingLeft = 0f;
                progressBackground.style.paddingRight = 0f;
                progressBackground.style.height = Length.Percent(100);
            }

            VisualElement progressFill = gameProgressBar.Q(className: ProgressBar.progressUssClassName);
            if (progressFill != null)
            {
                progressFill.style.backgroundColor = new StyleColor(Color.white);
                progressFill.style.marginTop = 0f;
                progressFill.style.marginBottom = 0f;
                progressFill.style.marginLeft = 0f;
                progressFill.style.marginRight = 0f;
                progressFill.style.paddingTop = 0f;
                progressFill.style.paddingBottom = 0f;
                progressFill.style.height = Length.Percent(100);
            }

            Label progressTitle = gameProgressBar.Q<Label>(className: ProgressBar.titleUssClassName);
            if (progressTitle != null)
                progressTitle.style.display = DisplayStyle.None;
        }

        // 설정 버튼: 아이콘 스프라이트 로드 + 클릭 이벤트
        if (settingIcon != null)
        {
            // Resources/Sprites/setting.png 로드
            Sprite sprite = Resources.Load<Sprite>("Sprites/setting");
            if (sprite != null)
            {
                settingIcon.sprite = sprite;
            }
            else
            {
                Debug.LogWarning("[GameMainUIController] Resources/Sprites/setting.png 스프라이트를 찾을 수 없습니다.");
            }
        }

        if (settingButton != null)
        {
            settingButton.clicked += () =>
            {
                ShowSettingPopup();
            };
        }

        if (settingCloseButton != null)
            settingCloseButton.clicked += HideSettingPopup;
        if (resetConfirmCancelButton != null)
            resetConfirmCancelButton.clicked += HideResetDataConfirmPopup;
        if (resetConfirmOkButton != null)
            resetConfirmOkButton.clicked += ConfirmResetData;

        AssignSprite(settingCloseIcon, "Sprites/close", "close.png");
        AssignSprite(tutorialCloseIcon, "Sprites/close", "close.png");
        AssignSprite(tutorialHandImage, "Sprites/hand", "hand.png");
        AssignSprite(soundIcon, "Sprites/sound", "sound.png");
        AssignSprite(vibrationIcon, "Sprites/vibrate", "vibrate.png");
        AssignSprite(helpIcon, "Sprites/help", "help.png");
        AssignSprite(languageIcon, "Sprites/global", "global.png");
        heartFilledSprite = Resources.Load<Sprite>("Sprites/heart");
        heartEmptySprite = Resources.Load<Sprite>("Sprites/heart_empty");
        LoadHelpTutorialSchedule();
        LoadStageSnackbarSchedule();

        RefreshSoundSwitchVisual();
        RefreshVibrationSwitchVisual();
        ApplySoundSwitchToAudioListener();

        if (soundSwitchButton != null)
            soundSwitchButton.clicked += ToggleSoundSwitch;
        if (vibrationSwitchButton != null)
            vibrationSwitchButton.clicked += ToggleVibrationSwitch;

        if (helpButton != null)
            helpButton.clicked += OpenHelpTutorialFromSettings;
        if (languageButton != null)
            languageButton.clicked += () => Debug.Log("언어 변경 열기");
        if (rateButton != null)
            rateButton.clicked += () => Debug.Log("평가하기 클릭");
        if (removeAdsButton != null)
            removeAdsButton.clicked += () => Debug.Log("광고 제거 클릭");
        if (emailButton != null)
            emailButton.clicked += OpenSupportEmail;
        if (resetDataButton != null)
            resetDataButton.clicked += ShowResetDataConfirmPopup;
        if (privacyPolicyButton != null)
            privacyPolicyButton.clicked += OpenPrivacyPolicy;
        if (termsButton != null)
            termsButton.clicked += OpenTerms;
        if (tutorialCloseButton != null)
            tutorialCloseButton.clicked += CloseTutorialPopup;
        if (tutorialConfirmButton != null)
            tutorialConfirmButton.clicked += CloseTutorialPopup;
        if (heartRefillAdButton != null)
            heartRefillAdButton.clicked += OnHeartRefillAdButtonClicked;

        // 하단 스킵/리셋/광고제거 버튼 아이콘 및 클릭 로그
        if (skipIcon != null)
        {
            Sprite sprite = Resources.Load<Sprite>("Sprites/skip");
            if (sprite != null)
                skipIcon.sprite = sprite;
            else
                Debug.LogWarning("[GameMainUIController] Resources/Sprites/skip.png 스프라이트를 찾을 수 없습니다.");
        }
        if (resetIcon != null)
        {
            Sprite sprite = Resources.Load<Sprite>("Sprites/reset");
            if (sprite != null)
                resetIcon.sprite = sprite;
            else
                Debug.LogWarning("[GameMainUIController] Resources/Sprites/reset.png 스프라이트를 찾을 수 없습니다.");
        }
        if (blockIcon != null)
        {
            Sprite sprite = Resources.Load<Sprite>("Sprites/block");
            if (sprite != null)
                blockIcon.sprite = sprite;
            else
                Debug.LogWarning("[GameMainUIController] Resources/Sprites/block.png 스프라이트를 찾을 수 없습니다.");
        }

        if (skipButton != null)
            skipButton.clicked += OnSkipClicked;
        if (resetButton != null)
            resetButton.clicked += OnResetClicked;
        if (blockAdsButton != null)
            blockAdsButton.clicked += () => Debug.Log("광고 제거");

        SetupButtonClickAnimations();
        ConfigureHeartDepletedPopupForRewardedAd();
        RefreshHeartVisuals();
        reservedBannerHeightPx = EstimateInitialBannerHeightPx();
        RefreshBottomLayout(force: true);
        InitializeBannerAd();
    }

    private void Update()
    {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        if (pendingBannerLoadFromInitialize)
        {
            pendingBannerLoadFromInitialize = false;
            LoadBannerAd();
        }
        if (pendingRewardedAdLoadFromInitialize)
        {
            pendingRewardedAdLoadFromInitialize = false;
            LoadRewardedAd();
        }
        if (pendingShowRewardedAd)
        {
            pendingShowRewardedAd = false;
            ShowRewardedAdInternal();
        }
#endif
        RefreshBottomLayout(force: false);
    }

    private void OnDestroy()
    {
        StopStageSnackbarPlayback();
        StopTutorialAnimation();
        DestroyBannerAd();
        DestroyRewardedAd();
    }

    private void SetupButtonClickAnimations()
    {
        RegisterButtonClickAnimation(settingButton);
        RegisterButtonClickAnimation(skipButton);
        RegisterButtonClickAnimation(resetButton);
        RegisterButtonClickAnimation(blockAdsButton, useWarmPulse: true);

        RegisterButtonClickAnimation(soundSwitchButton);
        RegisterButtonClickAnimation(vibrationSwitchButton);
        RegisterButtonClickAnimation(helpButton);
        RegisterButtonClickAnimation(languageButton);
        RegisterButtonClickAnimation(rateButton);
        RegisterButtonClickAnimation(removeAdsButton, useWarmPulse: true);
        RegisterButtonClickAnimation(emailButton);
        RegisterButtonClickAnimation(resetDataButton, useWarmPulse: true);
        RegisterButtonClickAnimation(privacyPolicyButton);
        RegisterButtonClickAnimation(termsButton);
        RegisterButtonClickAnimation(tutorialCloseButton);
        RegisterButtonClickAnimation(tutorialConfirmButton, useWarmPulse: true);
        RegisterButtonClickAnimation(resetConfirmCancelButton);
        RegisterButtonClickAnimation(resetConfirmOkButton, useWarmPulse: true);
        RegisterButtonClickAnimation(heartRefillAdButton, useWarmPulse: true);
    }

    private void RegisterButtonClickAnimation(Button button, bool useWarmPulse = false)
    {
        if (button == null)
            return;

        button.AddToClassList(NeonPressBaseClass);
        if (useWarmPulse)
            button.AddToClassList(NeonPressWarmClass);

        button.clicked += () => PlayButtonClickAnimation(button);
    }

    private void PlayButtonClickAnimation(VisualElement button)
    {
        if (button == null)
            return;

        int version = 1;
        if (buttonPressAnimationVersion.TryGetValue(button, out int previousVersion))
            version = previousVersion + 1;
        buttonPressAnimationVersion[button] = version;

        button.RemoveFromClassList(NeonPressActiveClass);
        button.style.scale = new StyleScale(new Scale(new Vector3(0.9f, 0.9f, 1f)));
        button.AddToClassList(NeonPressActiveClass);

        button.schedule.Execute(() =>
        {
            if (!IsCurrentButtonAnimationVersion(button, version))
                return;
            button.style.scale = new StyleScale(new Scale(new Vector3(1.05f, 1.05f, 1f)));
        }).StartingIn(55);

        button.schedule.Execute(() =>
        {
            if (!IsCurrentButtonAnimationVersion(button, version))
                return;
            button.style.scale = new StyleScale(new Scale(Vector3.one));
            button.RemoveFromClassList(NeonPressActiveClass);
        }).StartingIn(140);
    }

    private bool IsCurrentButtonAnimationVersion(VisualElement button, int expectedVersion)
    {
        return buttonPressAnimationVersion.TryGetValue(button, out int currentVersion) && currentVersion == expectedVersion;
    }

    /// <summary>스킵 버튼 클릭: 현재 스테이지를 건너뛰고 즉시 다음 스테이지 로드.</summary>
    private void OnSkipClicked()
    {
        FirebaseBootstrap.LogEvent("ui_button_click", new Dictionary<string, object>
        {
            { "button_name", "skip" }
        });
        var gm = FindFirstObjectByType<GameManager>();
        if (gm != null)
            gm.LoadNextStageImmediate();
        else
            Debug.Log("스테이지 스킵됨 (GameManager 없음)");
    }

    /// <summary>리셋 버튼 클릭: 현재 스테이지를 초기 상태로 복원.</summary>
    private void OnResetClicked()
    {
        FirebaseBootstrap.LogEvent("ui_button_click", new Dictionary<string, object>
        {
            { "button_name", "reset_stage" }
        });
        var gm = FindFirstObjectByType<GameManager>();
        if (gm != null)
            gm.ResetCurrentStage();
        else
            Debug.Log("스테이지 리셋됨 (GameManager 없음)");
    }

    private void ShowSettingPopup()
    {
        if (settingPopupOverlay != null)
        {
            settingPopupOverlay.style.display = DisplayStyle.Flex;
            isSettingPopupOpen = true;
            FirebaseBootstrap.LogEvent("settings_popup_open");
            FirebaseBootstrap.LogBreadcrumb("settings_popup_open");
        }
    }

    private void HideSettingPopup()
    {
        HideResetDataConfirmPopup();
        if (settingPopupOverlay != null)
        {
            settingPopupOverlay.style.display = DisplayStyle.None;
            isSettingPopupOpen = false;
            FirebaseBootstrap.LogEvent("settings_popup_close");
        }
    }

    /// <summary>설정 팝업이 열려 있으면 게임 입력 차단에 사용.</summary>
    public bool IsSettingPopupOpen => isSettingPopupOpen;
    public bool IsTutorialPopupOpen => isTutorialPopupOpen;
    public bool IsWaitingForHeartRefill => isWaitingForHeartRefill;

    public void ResetHeartsForNewStage()
    {
        SetHeartCount(MaxHearts, animated: false);
        isWaitingForHeartRefill = false;
        ConfigureHeartDepletedPopupForRewardedAd();
        HideHeartDepletedPopup();
    }

    public bool ConsumeHeartOnGameOver()
    {
        int nextHeartCount = Mathf.Max(0, currentHearts - 1);
        SetHeartCount(nextHeartCount, animated: true);
        FirebaseBootstrap.LogEvent("heart_consumed", new Dictionary<string, object>
        {
            { "remaining_hearts", currentHearts }
        });

        if (currentHearts > 0)
            return true;

        isWaitingForHeartRefill = true;
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null && gm.TryPeekSessionFreeHeartRefill(out int thresholdMinutes))
        {
            ConfigureHeartDepletedPopupForSessionReward(thresholdMinutes);
            FirebaseBootstrap.LogEvent("heart_refill_offer", new Dictionary<string, object>
            {
                { "type", "session_play_reward" },
                { "threshold_minutes", thresholdMinutes },
                { "pending_free_refills", gm.PendingSessionFreeHeartRefillCount }
            });
        }
        else
        {
            ConfigureHeartDepletedPopupForRewardedAd();
            FirebaseBootstrap.LogEvent("heart_refill_offer", new Dictionary<string, object>
            {
                { "type", "rewarded_ad" }
            });

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            if (rewardedAd == null || !rewardedAd.CanShowAd())
                LoadRewardedAd();
#endif
        }

        ShowHeartDepletedPopup();
        return false;
    }

    private void SetHeartCount(int newHeartCount, bool animated)
    {
        int clampedHeartCount = Mathf.Clamp(newHeartCount, 0, MaxHearts);
        int previousHeartCount = currentHearts;
        currentHearts = clampedHeartCount;

        if (!animated || previousHeartCount == clampedHeartCount)
        {
            RefreshHeartVisuals();
            return;
        }

        for (int i = 0; i < MaxHearts; i++)
        {
            bool wasFilled = previousHeartCount >= i + 1;
            bool isFilled = clampedHeartCount >= i + 1;
            Image heartImage = GetHeartImageByIndex(i);
            if (wasFilled != isFilled)
                AnimateHeartStateChange(heartImage, isFilled);
            else
                ApplyHeartVisual(heartImage, isFilled);
        }
    }

    private Image GetHeartImageByIndex(int index)
    {
        switch (index)
        {
            case 0: return heart1Image;
            case 1: return heart2Image;
            case 2: return heart3Image;
            default: return null;
        }
    }

    private void RefreshHeartVisuals()
    {
        ApplyHeartVisual(heart1Image, currentHearts >= 1);
        ApplyHeartVisual(heart2Image, currentHearts >= 2);
        ApplyHeartVisual(heart3Image, currentHearts >= 3);
    }

    private void ApplyHeartVisual(Image targetImage, bool isFilled)
    {
        ApplyHeartSprite(targetImage, isFilled ? heartFilledSprite : heartEmptySprite);
        if (targetImage == null)
            return;

        targetImage.style.opacity = isFilled ? 1f : 0.58f;
        targetImage.style.scale = new StyleScale(new Scale(Vector3.one));
    }

    private void AnimateHeartStateChange(Image heartImage, bool isFilled)
    {
        if (heartImage == null)
            return;

        int nextVersion = 1;
        if (heartAnimationVersion.TryGetValue(heartImage, out int previousVersion))
            nextVersion = previousVersion + 1;
        heartAnimationVersion[heartImage] = nextVersion;

        heartImage.style.opacity = 0.34f;
        heartImage.style.scale = new StyleScale(new Scale(new Vector3(0.78f, 0.78f, 1f)));

        heartImage.schedule.Execute(() =>
        {
            if (!IsCurrentHeartAnimationVersion(heartImage, nextVersion))
                return;

            ApplyHeartSprite(heartImage, isFilled ? heartFilledSprite : heartEmptySprite);
            heartImage.style.opacity = isFilled ? 1f : 0.58f;
            float popScale = isFilled ? 1.22f : 0.92f;
            heartImage.style.scale = new StyleScale(new Scale(new Vector3(popScale, popScale, 1f)));
        }).StartingIn(72);

        heartImage.schedule.Execute(() =>
        {
            if (!IsCurrentHeartAnimationVersion(heartImage, nextVersion))
                return;

            heartImage.style.scale = new StyleScale(new Scale(Vector3.one));
        }).StartingIn(182);
    }

    private bool IsCurrentHeartAnimationVersion(VisualElement heartElement, int expectedVersion)
    {
        return heartAnimationVersion.TryGetValue(heartElement, out int currentVersion) && currentVersion == expectedVersion;
    }

    private static void ApplyHeartSprite(Image targetImage, Sprite sprite)
    {
        if (targetImage == null || sprite == null)
            return;

        targetImage.sprite = null;
        targetImage.image = sprite.texture;
        targetImage.scaleMode = ScaleMode.ScaleToFit;
        targetImage.style.overflow = Overflow.Visible;
        targetImage.uv = new Rect(0f, 0f, 1f, 1f);
    }

    private void ShowHeartDepletedPopup()
    {
        if (heartDepletedOverlay != null)
            heartDepletedOverlay.style.display = DisplayStyle.Flex;

        heartPopupAnimationVersion++;
        int popupVersion = heartPopupAnimationVersion;
        if (heartDepletedDialog != null)
        {
            heartDepletedDialog.style.opacity = 0f;
            heartDepletedDialog.style.scale = new StyleScale(new Scale(new Vector3(0.92f, 0.92f, 1f)));
            heartDepletedDialog.schedule.Execute(() =>
            {
                if (popupVersion != heartPopupAnimationVersion)
                    return;
                heartDepletedDialog.style.opacity = 1f;
                heartDepletedDialog.style.scale = new StyleScale(new Scale(Vector3.one));
            }).StartingIn(12);
        }

        UpdateHeartRefillButtonState();
    }

    private void HideHeartDepletedPopup()
    {
        heartPopupAnimationVersion++;
        currentHeartRefillMode = HeartRefillMode.RewardedAd;
        currentSessionPlayRewardMinutes = 0;

        if (heartDepletedDialog != null)
        {
            heartDepletedDialog.style.opacity = 1f;
            heartDepletedDialog.style.scale = new StyleScale(new Scale(Vector3.one));
        }

        if (heartDepletedOverlay != null)
            heartDepletedOverlay.style.display = DisplayStyle.None;
        SetHeartRefillStatus(string.Empty);
    }

    private void SetHeartRefillStatus(string message)
    {
        if (heartRefillStatusLabel != null)
            heartRefillStatusLabel.text = message;
    }

    private void UpdateHeartRefillButtonState()
    {
        if (heartRefillAdButton == null)
            return;

        if (currentHeartRefillMode == HeartRefillMode.SessionPlayReward)
        {
            heartRefillAdButton.SetEnabled(true);
            int rewardMinutes = Mathf.Max(1, currentSessionPlayRewardMinutes);
            SetHeartRefillStatus($"{rewardMinutes}분 플레이 보상으로 무료 충전 가능합니다.");
            return;
        }

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        bool canShow = rewardedAd != null && rewardedAd.CanShowAd();
        heartRefillAdButton.SetEnabled(canShow);
        if (canShow)
            SetHeartRefillStatus("광고 시청 후 하트 3개 충전");
        else
            SetHeartRefillStatus("광고를 불러오는 중입니다...");
#else
        heartRefillAdButton.SetEnabled(true);
        SetHeartRefillStatus("에디터에서는 즉시 충전됩니다.");
#endif
    }

    private void OnHeartRefillAdButtonClicked()
    {
        if (!isWaitingForHeartRefill)
            return;

        FirebaseBootstrap.LogEvent("heart_refill_button_click", new Dictionary<string, object>
        {
            { "mode", currentHeartRefillMode == HeartRefillMode.SessionPlayReward ? "session_play_reward" : "rewarded_ad" }
        });

        if (currentHeartRefillMode == HeartRefillMode.SessionPlayReward)
        {
            TryCompleteSessionPlayRewardHeartRefill();
            return;
        }

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        if (rewardedAd != null && rewardedAd.CanShowAd())
        {
            heartRefillAdButton?.SetEnabled(false);
            SetHeartRefillStatus("광고를 여는 중입니다...");
            pendingShowRewardedAd = true;
            return;
        }

        SetHeartRefillStatus("광고를 준비 중입니다. 잠시 후 다시 시도해 주세요.");
        LoadRewardedAd();
#else
        CompleteHeartRefillAfterReward();
#endif
    }

    private void TryCompleteSessionPlayRewardHeartRefill()
    {
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm == null || !gm.TryConsumeSessionFreeHeartRefill(out int thresholdMinutes))
        {
            ConfigureHeartDepletedPopupForRewardedAd();
            UpdateHeartRefillButtonState();
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            if (rewardedAd == null || !rewardedAd.CanShowAd())
                LoadRewardedAd();
#endif
            return;
        }

        heartRefillAdButton?.SetEnabled(false);
        CompleteHeartRefill("session_play_reward", thresholdMinutes);
    }

    private void CompleteHeartRefillAfterReward()
    {
        CompleteHeartRefill("rewarded_ad");
    }

    private void CompleteHeartRefill(string source, int sessionRewardMinutes = 0)
    {
        SetHeartCount(MaxHearts, animated: true);
        isWaitingForHeartRefill = false;
        HideHeartDepletedPopup();
        Dictionary<string, object> eventData = new Dictionary<string, object>
        {
            { "source", source }
        };
        if (sessionRewardMinutes > 0)
            eventData["session_reward_minutes"] = sessionRewardMinutes;

        FirebaseBootstrap.LogEvent("heart_refilled", eventData);
    }

    private void ConfigureHeartDepletedPopupForRewardedAd()
    {
        currentHeartRefillMode = HeartRefillMode.RewardedAd;
        currentSessionPlayRewardMinutes = 0;
        SetHeartDepletedPopupCopy(
            "하트가 모두 소진됐어요",
            "광고를 시청하면 하트 3개가 즉시 충전되고 현재 스테이지가 다시 시작됩니다.",
            "보상: 하트 3개 + 즉시 재시작",
            "광고 보고 하트 3개 충전");
    }

    private void ConfigureHeartDepletedPopupForSessionReward(int thresholdMinutes)
    {
        currentHeartRefillMode = HeartRefillMode.SessionPlayReward;
        currentSessionPlayRewardMinutes = Mathf.Max(1, thresholdMinutes);
        SetHeartDepletedPopupCopy(
            "무료 하트 충전 기회",
            $"{currentSessionPlayRewardMinutes}분 이상 플레이했기 때문에 하트 3개를 무료로 충전해드립니다.",
            "보상: 하트 3개 + 즉시 재시작 (광고 없음)",
            "무료 충전 확인");
    }

    private void SetHeartDepletedPopupCopy(string title, string message, string rewardHint, string buttonText)
    {
        if (heartDepletedTitleLabel != null)
            heartDepletedTitleLabel.text = title;
        if (heartDepletedMessageLabel != null)
            heartDepletedMessageLabel.text = message;
        if (heartRefillRewardHintLabel != null)
            heartRefillRewardHintLabel.text = rewardHint;
        if (heartRefillAdButton != null)
            heartRefillAdButton.text = buttonText;
    }

    private void LoadHelpTutorialSchedule()
    {
        helpTutorialEntries.Clear();
        helpTutorialEntriesByStage.Clear();

        TextAsset scheduleAsset = Resources.Load<TextAsset>(TutorialScheduleResourcePath);
        if (scheduleAsset == null)
        {
            Debug.LogWarning($"[GameMainUIController] 도움말 스케줄 JSON 없음: Resources/{TutorialScheduleResourcePath}.json");
            AddFallbackHelpTutorialEntry();
            return;
        }

        HelpTutorialScheduleData scheduleData = null;
        try
        {
            scheduleData = JsonUtility.FromJson<HelpTutorialScheduleData>(scheduleAsset.text);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameMainUIController] 도움말 스케줄 파싱 실패: {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, "Help tutorial schedule parse failed");
        }

        if (scheduleData == null || scheduleData.entries == null || scheduleData.entries.Length == 0)
        {
            AddFallbackHelpTutorialEntry();
            return;
        }

        for (int i = 0; i < scheduleData.entries.Length; i++)
        {
            HelpTutorialEntryData entry = scheduleData.entries[i];
            if (entry == null)
                continue;

            if (entry.stageIndex <= 0)
                continue;

            if (string.IsNullOrWhiteSpace(entry.id))
                entry.id = $"tutorial_stage_{entry.stageIndex}_{i + 1}";

            if (string.IsNullOrWhiteSpace(entry.tutorialType))
                entry.tutorialType = TutorialTypeBasicPath;

            if (string.IsNullOrWhiteSpace(entry.title))
                entry.title = "도움말";

            if (string.IsNullOrWhiteSpace(entry.description))
                entry.description = "타일을 연결해 카운트를 0으로 만드세요.";

            if (string.IsNullOrWhiteSpace(entry.closeButtonText))
                entry.closeButtonText = "확인";

            helpTutorialEntries.Add(entry);
            if (!helpTutorialEntriesByStage.TryGetValue(entry.stageIndex, out List<HelpTutorialEntryData> list))
            {
                list = new List<HelpTutorialEntryData>();
                helpTutorialEntriesByStage[entry.stageIndex] = list;
            }

            list.Add(entry);
        }

        if (helpTutorialEntries.Count == 0)
            AddFallbackHelpTutorialEntry();
    }

    private void AddFallbackHelpTutorialEntry()
    {
        HelpTutorialEntryData fallback = new HelpTutorialEntryData
        {
            id = "basic_stage_1",
            stageIndex = 1,
            tutorialType = TutorialTypeBasicPath,
            title = "기본 플레이 방법",
            description = "왼쪽(1) → 중앙(2) → 오른쪽(1) → 중앙으로 이동하면 카운트가 줄어들며 클리어됩니다.",
            closeButtonText = "확인"
        };
        helpTutorialEntries.Add(fallback);
        helpTutorialEntriesByStage[1] = new List<HelpTutorialEntryData> { fallback };
    }

    private void LoadStageSnackbarSchedule()
    {
        stageSnackbarEntriesByStage.Clear();
        shownStageSnackbarIdsThisSession.Clear();

        TextAsset scheduleAsset = Resources.Load<TextAsset>(StageSnackbarScheduleResourcePath);
        if (scheduleAsset == null)
        {
            Debug.LogWarning($"[GameMainUIController] 스낵바 스케줄 JSON 없음: Resources/{StageSnackbarScheduleResourcePath}.json");
            return;
        }

        StageSnackbarScheduleData scheduleData = null;
        try
        {
            scheduleData = JsonUtility.FromJson<StageSnackbarScheduleData>(scheduleAsset.text);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameMainUIController] 스낵바 스케줄 파싱 실패: {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, "Stage snackbar schedule parse failed");
        }

        if (scheduleData == null || scheduleData.entries == null || scheduleData.entries.Length == 0)
            return;

        for (int i = 0; i < scheduleData.entries.Length; i++)
        {
            StageSnackbarEntryData entry = scheduleData.entries[i];
            if (entry == null || entry.stageIndex <= 0)
                continue;

            if (string.IsNullOrWhiteSpace(entry.id))
                entry.id = $"stage_snackbar_{entry.stageIndex}_{i + 1}";
            if (string.IsNullOrWhiteSpace(entry.message))
                entry.message = "새로운 타입의 타일이 열립니다! {remainingStages}스테이지 남았습니다.";
            if (entry.duration <= 0f)
                entry.duration = stageSnackbarDefaultDuration;

            if (!stageSnackbarEntriesByStage.TryGetValue(entry.stageIndex, out List<StageSnackbarEntryData> list))
            {
                list = new List<StageSnackbarEntryData>();
                stageSnackbarEntriesByStage[entry.stageIndex] = list;
            }

            list.Add(entry);
        }
    }

    private void TryShowScheduledSnackbarForStage(int stageIndex)
    {
        if (isTutorialPopupOpen || isSettingPopupOpen || isWaitingForHeartRefill)
            return;
        if (!stageSnackbarEntriesByStage.TryGetValue(stageIndex, out List<StageSnackbarEntryData> entries) || entries == null || entries.Count == 0)
            return;

        List<StageSnackbarEntryData> pendingEntries = new List<StageSnackbarEntryData>();
        for (int i = 0; i < entries.Count; i++)
        {
            StageSnackbarEntryData entry = entries[i];
            if (entry == null)
                continue;

            if (!string.IsNullOrEmpty(entry.id) && shownStageSnackbarIdsThisSession.Contains(entry.id))
                continue;

            pendingEntries.Add(entry);
        }

        if (pendingEntries.Count == 0)
            return;

        for (int i = 0; i < pendingEntries.Count; i++)
        {
            StageSnackbarEntryData entry = pendingEntries[i];
            if (entry != null && !string.IsNullOrEmpty(entry.id))
                shownStageSnackbarIdsThisSession.Add(entry.id);
        }

        StartStageSnackbarPlayback(pendingEntries, stageIndex);
    }

    private void StartStageSnackbarPlayback(List<StageSnackbarEntryData> entries, int currentStageIndex)
    {
        if (entries == null || entries.Count == 0)
            return;

        StopStageSnackbarPlayback();
        stageSnackbarRoutine = StartCoroutine(PlayStageSnackbarSequence(entries, currentStageIndex));
    }

    private IEnumerator PlayStageSnackbarSequence(List<StageSnackbarEntryData> entries, int currentStageIndex)
    {
        if (stageSnackbar == null || stageSnackbarLabel == null || entries == null || entries.Count == 0)
            yield break;

        stageSnackbarAnimationVersion++;
        int animationVersion = stageSnackbarAnimationVersion;

        for (int i = 0; i < entries.Count; i++)
        {
            if (!IsCurrentStageSnackbarAnimationVersion(animationVersion))
                yield break;

            StageSnackbarEntryData entry = entries[i];
            string message = BuildStageSnackbarMessage(entry, currentStageIndex);
            if (string.IsNullOrWhiteSpace(message))
                continue;

            ShowStageSnackbar(message, animationVersion);
            float duration = Mathf.Max(1f, entry != null && entry.duration > 0f ? entry.duration : stageSnackbarDefaultDuration);
            yield return new WaitForSecondsRealtime(duration);

            if (!IsCurrentStageSnackbarAnimationVersion(animationVersion))
                yield break;

            HideStageSnackbar(animationVersion);
            yield return new WaitForSecondsRealtime(0.2f);
        }

        if (IsCurrentStageSnackbarAnimationVersion(animationVersion))
            stageSnackbarRoutine = null;
    }

    private string BuildStageSnackbarMessage(StageSnackbarEntryData entry, int currentStageIndex)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.message))
            return string.Empty;

        int targetStageIndex = entry.targetStageIndex > 0 ? entry.targetStageIndex : currentStageIndex;
        int remainingStages = Mathf.Max(0, targetStageIndex - currentStageIndex);

        string message = entry.message;
        message = message.Replace("{currentStage}", currentStageIndex.ToString());
        message = message.Replace("{targetStage}", targetStageIndex.ToString());
        message = message.Replace("{remainingStages}", remainingStages.ToString());
        return message.Trim();
    }

    private void ShowStageSnackbar(string message, int animationVersion)
    {
        if (stageSnackbar == null || stageSnackbarLabel == null)
            return;

        stageSnackbarLabel.text = message;
        stageSnackbar.style.display = DisplayStyle.Flex;
        stageSnackbar.style.opacity = 0f;
        stageSnackbar.style.scale = new StyleScale(new Scale(new Vector3(0.96f, 0.96f, 1f)));
        stageSnackbar.schedule.Execute(() =>
        {
            if (!IsCurrentStageSnackbarAnimationVersion(animationVersion))
                return;

            stageSnackbar.style.opacity = 1f;
            stageSnackbar.style.scale = new StyleScale(new Scale(Vector3.one));
        }).StartingIn(12);

        FirebaseBootstrap.LogEvent("stage_snackbar_show", new Dictionary<string, object>
        {
            { "stage_index", currentStageIndexForUI },
            { "message", message }
        });
    }

    private void HideStageSnackbar(int animationVersion)
    {
        if (stageSnackbar == null)
            return;

        stageSnackbar.style.opacity = 0f;
        stageSnackbar.style.scale = new StyleScale(new Scale(new Vector3(0.97f, 0.97f, 1f)));
        stageSnackbar.schedule.Execute(() =>
        {
            if (!IsCurrentStageSnackbarAnimationVersion(animationVersion))
                return;

            stageSnackbar.style.display = DisplayStyle.None;
        }).StartingIn(160);
    }

    private void StopStageSnackbarPlayback()
    {
        stageSnackbarAnimationVersion++;
        if (stageSnackbarRoutine != null)
        {
            StopCoroutine(stageSnackbarRoutine);
            stageSnackbarRoutine = null;
        }

        if (stageSnackbar != null)
        {
            stageSnackbar.style.display = DisplayStyle.None;
            stageSnackbar.style.opacity = 0f;
            stageSnackbar.style.scale = new StyleScale(new Scale(new Vector3(0.96f, 0.96f, 1f)));
        }
    }

    private bool IsCurrentStageSnackbarAnimationVersion(int expectedVersion)
    {
        return stageSnackbarAnimationVersion == expectedVersion;
    }

    private void TryShowScheduledTutorialForStage(int stageIndex)
    {
        if (isTutorialPopupOpen || isSettingPopupOpen || isWaitingForHeartRefill)
            return;

        HelpTutorialEntryData entry = GetNextAutoTutorialEntryForStage(stageIndex);
        if (entry == null)
            return;

        ShowTutorialPopup(entry, ignoreDismissed: false, openedFromSettings: false);
    }

    private HelpTutorialEntryData GetNextAutoTutorialEntryForStage(int stageIndex)
    {
        if (!helpTutorialEntriesByStage.TryGetValue(stageIndex, out List<HelpTutorialEntryData> entries) || entries == null)
            return null;

        for (int i = 0; i < entries.Count; i++)
        {
            HelpTutorialEntryData entry = entries[i];
            if (entry == null || string.IsNullOrEmpty(entry.id))
                continue;

            if (!IsTutorialDismissed(entry.id))
                return entry;
        }

        return null;
    }

    private HelpTutorialEntryData GetHelpTutorialForSettings()
    {
        HelpTutorialEntryData stageEntry = GetFirstTutorialEntryForStage(currentStageIndexForUI);
        if (stageEntry != null)
            return stageEntry;
        if (helpTutorialEntries.Count > 0)
            return helpTutorialEntries[0];
        return null;
    }

    private HelpTutorialEntryData GetFirstTutorialEntryForStage(int stageIndex)
    {
        if (!helpTutorialEntriesByStage.TryGetValue(stageIndex, out List<HelpTutorialEntryData> entries) || entries == null)
            return null;
        return entries.Count > 0 ? entries[0] : null;
    }

    private static string GetTutorialDismissedKey(string tutorialId)
    {
        return TutorialDismissedKeyPrefix + tutorialId;
    }

    private static bool IsTutorialDismissed(string tutorialId)
    {
        if (string.IsNullOrEmpty(tutorialId))
            return false;
        return LoadSettingBool(GetTutorialDismissedKey(tutorialId), false);
    }

    private static void MarkTutorialDismissed(string tutorialId)
    {
        if (string.IsNullOrEmpty(tutorialId))
            return;
        SaveSettingBool(GetTutorialDismissedKey(tutorialId), true);
    }

    private void OpenHelpTutorialFromSettings()
    {
        HelpTutorialEntryData entry = GetHelpTutorialForSettings();
        if (entry == null)
        {
            Debug.LogWarning("[GameMainUIController] 표시할 도움말 항목이 없습니다.");
            return;
        }

        HideSettingPopup();
        ShowTutorialPopup(entry, ignoreDismissed: true, openedFromSettings: true);
    }

    private void ShowTutorialPopup(HelpTutorialEntryData entry, bool ignoreDismissed, bool openedFromSettings)
    {
        if (entry == null)
            return;

        if (!ignoreDismissed && IsTutorialDismissed(entry.id))
            return;

        activeTutorialEntry = entry;
        isTutorialPopupOpen = true;

        if (tutorialOverlay != null)
            tutorialOverlay.style.display = DisplayStyle.Flex;

        if (tutorialDialog != null)
        {
            tutorialDialog.style.opacity = 0f;
            tutorialDialog.style.scale = new StyleScale(new Scale(new Vector3(0.94f, 0.94f, 1f)));
            tutorialDialog.schedule.Execute(() =>
            {
                if (!isTutorialPopupOpen)
                    return;
                tutorialDialog.style.opacity = 1f;
                tutorialDialog.style.scale = new StyleScale(new Scale(Vector3.one));
            }).StartingIn(14);
        }

        if (tutorialTitleLabel != null)
            tutorialTitleLabel.text = entry.title;
        if (tutorialDescriptionLabel != null)
            tutorialDescriptionLabel.text = entry.description;
        if (tutorialConfirmButton != null)
            tutorialConfirmButton.text = entry.closeButtonText;

        ApplyTutorialStepState(1, 2, 1, "왼쪽 타일에서 시작해 경로를 연결해보세요.");
        SetTutorialHandPosition(0, instant: true);
        StartTutorialAnimation(entry);

        FirebaseBootstrap.LogEvent("help_tutorial_open", new Dictionary<string, object>
        {
            { "tutorial_id", entry.id },
            { "stage_index", entry.stageIndex },
            { "open_type", openedFromSettings ? "settings_button" : "stage_auto" }
        });
    }

    private void CloseTutorialPopup()
    {
        if (!isTutorialPopupOpen)
            return;

        string tutorialId = activeTutorialEntry != null ? activeTutorialEntry.id : string.Empty;
        if (!string.IsNullOrEmpty(tutorialId))
            MarkTutorialDismissed(tutorialId);

        StopTutorialAnimation();
        isTutorialPopupOpen = false;

        if (tutorialDialog != null)
        {
            tutorialDialog.style.opacity = 1f;
            tutorialDialog.style.scale = new StyleScale(new Scale(Vector3.one));
        }

        if (tutorialOverlay != null)
            tutorialOverlay.style.display = DisplayStyle.None;

        activeTutorialEntry = null;
        if (tutorialStepHintLabel != null)
            tutorialStepHintLabel.text = string.Empty;

        FirebaseBootstrap.LogEvent("help_tutorial_close", new Dictionary<string, object>
        {
            { "tutorial_id", string.IsNullOrEmpty(tutorialId) ? "unknown" : tutorialId }
        });
    }

    private void StartTutorialAnimation(HelpTutorialEntryData entry)
    {
        StopTutorialAnimation();
        if (entry == null)
            return;

        if (string.Equals(entry.tutorialType, TutorialTypeBasicPath, StringComparison.OrdinalIgnoreCase))
        {
            tutorialAnimationVersion++;
            tutorialAnimationRoutine = StartCoroutine(PlayBasicTutorialAnimationLoop(tutorialAnimationVersion));
        }
    }

    private void StopTutorialAnimation()
    {
        tutorialAnimationVersion++;
        if (tutorialAnimationRoutine != null)
        {
            StopCoroutine(tutorialAnimationRoutine);
            tutorialAnimationRoutine = null;
        }
    }

    private IEnumerator PlayBasicTutorialAnimationLoop(int animationVersion)
    {
        WaitForSecondsRealtime stepWait = new WaitForSecondsRealtime(0.58f);
        WaitForSecondsRealtime cycleWait = new WaitForSecondsRealtime(0.82f);

        while (isTutorialPopupOpen && animationVersion == tutorialAnimationVersion)
        {
            ApplyTutorialStepState(1, 2, 1, "왼쪽에서 시작");
            SetTutorialHandPosition(0, instant: true);
            yield return new WaitForSecondsRealtime(0.42f);

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            SetTutorialHandPosition(1, instant: false);
            ApplyTutorialStepState(0, 2, 1, "왼쪽 타일 카운트 -1", pulseLeft: true);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            SetTutorialHandPosition(2, instant: false);
            ApplyTutorialStepState(0, 1, 1, "중앙 타일 카운트 -1", pulseCenter: true);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            SetTutorialHandPosition(1, instant: false);
            ApplyTutorialStepState(0, 1, 0, "오른쪽 타일 카운트 -1", pulseRight: true);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyTutorialStepState(0, 0, 0, "남은 카운트 0: 스테이지 클리어!", pulseCenter: true);
            yield return cycleWait;
        }

        tutorialAnimationRoutine = null;
    }

    private bool IsTutorialAnimationActive(int animationVersion)
    {
        return isTutorialPopupOpen && animationVersion == tutorialAnimationVersion;
    }

    private void ApplyTutorialStepState(int leftCount, int centerCount, int rightCount, string hint, bool pulseLeft = false, bool pulseCenter = false, bool pulseRight = false)
    {
        ApplyTutorialTileState(tutorialTileLeft, tutorialTileLeftCount, leftCount, pulseLeft);
        ApplyTutorialTileState(tutorialTileCenter, tutorialTileCenterCount, centerCount, pulseCenter);
        ApplyTutorialTileState(tutorialTileRight, tutorialTileRightCount, rightCount, pulseRight);

        if (tutorialStepHintLabel != null)
            tutorialStepHintLabel.text = hint;
    }

    private static void ApplyTutorialTileState(VisualElement tile, Label countLabel, int count, bool pulse)
    {
        if (countLabel != null)
            countLabel.text = Mathf.Max(0, count).ToString();
        if (tile == null)
            return;

        bool active = count > 0;
        tile.style.opacity = active ? 1f : 0.3f;
        tile.style.backgroundColor = active ? new StyleColor(new Color(0.24f, 0.72f, 0.99f, 0.23f)) : new StyleColor(new Color(0.08f, 0.13f, 0.2f, 0.46f));
        Color borderColor = active ? new Color(0.73f, 0.93f, 1f, 0.95f) : new Color(0.45f, 0.61f, 0.72f, 0.58f);
        tile.style.borderLeftColor = borderColor;
        tile.style.borderRightColor = borderColor;
        tile.style.borderTopColor = borderColor;
        tile.style.borderBottomColor = borderColor;

        if (pulse)
        {
            tile.style.scale = new StyleScale(new Scale(new Vector3(1.16f, 1.16f, 1f)));
            tile.schedule.Execute(() =>
            {
                tile.style.scale = new StyleScale(new Scale(Vector3.one));
            }).StartingIn(120);
        }
        else
        {
            tile.style.scale = new StyleScale(new Scale(Vector3.one));
        }
    }

    private void SetTutorialHandPosition(int laneIndex, bool instant)
    {
        if (tutorialHandImage == null)
            return;

        float leftPercent;
        switch (laneIndex)
        {
            case 0:
                leftPercent = 19f;
                break;
            case 1:
                leftPercent = 44f;
                break;
            case 2:
                leftPercent = 69f;
                break;
            default:
                leftPercent = 44f;
                break;
        }

        tutorialHandImage.style.left = Length.Percent(leftPercent);
        tutorialHandImage.style.top = 226f;
        tutorialHandImage.style.opacity = 1f;
        tutorialHandImage.style.scale = new StyleScale(new Scale(instant ? Vector3.one : new Vector3(1.04f, 1.04f, 1f)));
    }

    private void ToggleSoundSwitch()
    {
        isSoundOn = !isSoundOn;
        RefreshSoundSwitchVisual();
        ApplySoundSwitchToAudioListener();
        SaveSettingBool(SaveKeySoundOn, isSoundOn);
        FirebaseBootstrap.LogEvent("sound_toggle", new Dictionary<string, object>
        {
            { "enabled", isSoundOn ? 1L : 0L }
        });
        Debug.Log(isSoundOn ? "소리 ON" : "소리 OFF");
    }

    private void ToggleVibrationSwitch()
    {
        isVibrationOn = !isVibrationOn;
        IsVibrationEnabled = isVibrationOn;
        RefreshVibrationSwitchVisual();
        SaveSettingBool(SaveKeyVibrationOn, isVibrationOn);
        FirebaseBootstrap.LogEvent("vibration_toggle", new Dictionary<string, object>
        {
            { "enabled", isVibrationOn ? 1L : 0L }
        });
        Debug.Log(isVibrationOn ? "진동 ON" : "진동 OFF");
    }

    private void RefreshSoundSwitchVisual()
    {
        if (soundSwitchButton == null)
            return;
        soundSwitchButton.EnableInClassList("settings-switch-on", isSoundOn);
        soundSwitchButton.EnableInClassList("settings-switch-off", !isSoundOn);
        if (soundSwitchLabel != null)
            soundSwitchLabel.text = isSoundOn ? "ON" : "OFF";
    }

    private void RefreshVibrationSwitchVisual()
    {
        if (vibrationSwitchButton == null)
            return;
        vibrationSwitchButton.EnableInClassList("settings-switch-on", isVibrationOn);
        vibrationSwitchButton.EnableInClassList("settings-switch-off", !isVibrationOn);
        if (vibrationSwitchLabel != null)
            vibrationSwitchLabel.text = isVibrationOn ? "ON" : "OFF";
    }

    private void ApplySoundSwitchToAudioListener()
    {
        AudioListener.volume = isSoundOn ? 1f : 0f;
    }

    private void ShowResetDataConfirmPopup()
    {
        FirebaseBootstrap.LogEvent("reset_data_warning_popup_open");
        if (resetConfirmOverlay != null)
            resetConfirmOverlay.style.display = DisplayStyle.Flex;
    }

    private void HideResetDataConfirmPopup()
    {
        if (resetConfirmOverlay != null)
            resetConfirmOverlay.style.display = DisplayStyle.None;
    }

    private void ConfirmResetData()
    {
        FirebaseBootstrap.LogEvent("reset_data_confirmed");
        FirebaseBootstrap.LogBreadcrumb("reset_data_confirmed");

        try
        {
            if (ES3.KeyExists(SaveKeySoundOn)) ES3.DeleteKey(SaveKeySoundOn);
            if (ES3.KeyExists(SaveKeyVibrationOn)) ES3.DeleteKey(SaveKeyVibrationOn);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameMainUIController] 설정 초기화(ES3) 실패: {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, "ConfirmResetData ES3 delete failed");
        }

        PlayerPrefs.DeleteKey(SaveKeySoundOn);
        PlayerPrefs.DeleteKey(SaveKeyVibrationOn);
        PlayerPrefs.Save();

        GameManager.ClearSavedStageProgress();

        isSoundOn = true;
        isVibrationOn = true;
        IsVibrationEnabled = true;
        RefreshSoundSwitchVisual();
        RefreshVibrationSwitchVisual();
        ApplySoundSwitchToAudioListener();

        HideResetDataConfirmPopup();
        HideSettingPopup();

        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null)
            gm.ResetProgressAndRestartToFirstStage();
    }

    private void OpenPrivacyPolicy()
    {
        FirebaseBootstrap.LogEvent("open_external_link", new Dictionary<string, object>
        {
            { "link_type", "privacy_policy" }
        });
        Application.OpenURL(PrivacyUrl);
    }

    private void OpenTerms()
    {
        FirebaseBootstrap.LogEvent("open_external_link", new Dictionary<string, object>
        {
            { "link_type", "terms" }
        });
        Application.OpenURL(PrivacyUrl);
    }

    private void OpenSupportEmail()
    {
        FirebaseBootstrap.LogEvent("open_support_email", new Dictionary<string, object>
        {
            { "channel", "mailto" }
        });
        string subject = Uri.EscapeDataString("ZeroStep Support Request");
        string body = Uri.EscapeDataString(BuildSupportEmailBody());
        Application.OpenURL($"mailto:{SupportEmailAddress}?subject={subject}&body={body}");
    }

    private static string BuildSupportEmailBody()
    {
        return
            "Hello ZeroStep Team,\n\n" +
            "Current Device Information:\n" +
            $"- Device Model: {SystemInfo.deviceModel}\n" +
            $"- Device Name: {SystemInfo.deviceName}\n" +
            $"- Operating System: {SystemInfo.operatingSystem}\n" +
            $"- System Language: {Application.systemLanguage}\n\n" +
            "Game Version Information:\n" +
            $"- App Version: {Application.version}\n" +
            $"- Unity Version: {Application.unityVersion}\n\n" +
            "Message:\n" +
            "(Please write your message here.)\n";
    }

    private void LoadSavedSettings()
    {
        isSoundOn = LoadSettingBool(SaveKeySoundOn, true);
        isVibrationOn = LoadSettingBool(SaveKeyVibrationOn, true);
    }

    private static bool LoadSettingBool(string key, bool defaultValue)
    {
        try
        {
            if (ES3.KeyExists(key))
                return ES3.Load<bool>(key);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameMainUIController] 설정 로드 실패({key}): {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, $"LoadSettingBool failed: {key}");
        }

        if (PlayerPrefs.HasKey(key))
            return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
        return defaultValue;
    }

    private static void SaveSettingBool(string key, bool value)
    {
        try
        {
            ES3.Save(key, value);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameMainUIController] 설정 저장 실패({key}): {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, $"SaveSettingBool failed: {key}");
        }

        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void AssignSprite(Image targetImage, string resourcePath, string fileName)
    {
        if (targetImage == null)
            return;
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
        {
            targetImage.sprite = null;
            targetImage.image = sprite.texture;
            targetImage.scaleMode = ScaleMode.ScaleToFit;
            targetImage.style.overflow = Overflow.Visible;
            targetImage.uv = new Rect(0f, 0f, 1f, 1f);
        }
        else
            Debug.LogWarning($"[GameMainUIController] Resources/{resourcePath} ({fileName}) 스프라이트를 찾을 수 없습니다.");
    }

    private void InitializeBannerAd()
    {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        SetBannerPlaceholderText(isDebugBuildCached ? "TEST AD LOADING..." : "AD LOADING...");
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        MobileAds.Initialize(_ =>
        {
            pendingBannerLoadFromInitialize = true;
            pendingRewardedAdLoadFromInitialize = true;
        });
#else
        SetBannerPlaceholderText("BANNER");
#endif
    }

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
    private string ResolveRewardedAdUnitId()
    {
        bool useTestAdUnit = isDebugBuildCached;
#if UNITY_ANDROID
        return useTestAdUnit ? AndroidTestRewardedAdUnitId : AndroidReleaseRewardedAdUnitId;
#elif UNITY_IOS
        return useTestAdUnit ? IOSTestRewardedAdUnitId : IOSReleaseRewardedAdUnitId;
#else
        return string.Empty;
#endif
    }

    private void LoadRewardedAd()
    {
        if (isRewardedAdLoading)
            return;

        if (rewardedAd != null && rewardedAd.CanShowAd())
        {
            UpdateHeartRefillButtonState();
            return;
        }

        string adUnitId = ResolveRewardedAdUnitId();
        if (string.IsNullOrEmpty(adUnitId))
            return;

        isRewardedAdLoading = true;
        RewardedAd.Load(adUnitId, new AdRequest(), (RewardedAd ad, LoadAdError loadError) =>
        {
            isRewardedAdLoading = false;
            if (loadError != null || ad == null)
            {
                string errorMessage = loadError != null ? loadError.GetMessage() : "unknown";
                Debug.LogWarning($"[GameMainUIController] Rewarded ad load failed: {errorMessage}");
                SetHeartRefillStatus("광고 준비에 실패했습니다. 잠시 후 다시 시도해 주세요.");
                UpdateHeartRefillButtonState();
                return;
            }

            DestroyRewardedAd();
            rewardedAd = ad;
            rewardEarnedThisShow = false;

            rewardedAd.OnAdFullScreenContentClosed += HandleRewardedAdFullScreenClosed;
            rewardedAd.OnAdFullScreenContentFailed += HandleRewardedAdFullScreenFailed;

            Debug.Log($"[GameMainUIController] Rewarded ad loaded. unitId={adUnitId}, testMode={(isDebugBuildCached ? 1 : 0)}");
            UpdateHeartRefillButtonState();
        });
    }

    private void ShowRewardedAdInternal()
    {
        if (rewardedAd == null || !rewardedAd.CanShowAd())
        {
            SetHeartRefillStatus("광고를 준비 중입니다. 잠시 후 다시 시도해 주세요.");
            UpdateHeartRefillButtonState();
            LoadRewardedAd();
            return;
        }

        rewardEarnedThisShow = false;
        rewardedAd.Show(reward =>
        {
            rewardEarnedThisShow = true;
            CompleteHeartRefillAfterReward();
        });
    }

    private void HandleRewardedAdFullScreenClosed()
    {
        if (isWaitingForHeartRefill && !rewardEarnedThisShow)
            SetHeartRefillStatus("보상을 받지 못했습니다. 다시 시도해 주세요.");

        DestroyRewardedAd();
        LoadRewardedAd();
        UpdateHeartRefillButtonState();
    }

    private void HandleRewardedAdFullScreenFailed(AdError adError)
    {
        string errorMessage = adError != null ? adError.GetMessage() : "unknown";
        Debug.LogWarning($"[GameMainUIController] Rewarded ad failed to show: {errorMessage}");
        SetHeartRefillStatus("광고 표시 실패. 다시 시도해 주세요.");
        DestroyRewardedAd();
        LoadRewardedAd();
        UpdateHeartRefillButtonState();
    }

    private void LoadBannerAd()
    {
        DestroyBannerAd();

        string adUnitId = ResolveBannerAdUnitId();
        if (string.IsNullOrEmpty(adUnitId))
        {
            Debug.LogWarning("[GameMainUIController] 현재 플랫폼에서 Banner Ad Unit ID를 찾을 수 없습니다.");
            return;
        }

        AdSize adSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
        reservedBannerHeightPx = Mathf.Max(reservedBannerHeightPx, ConvertDpToPixels(adSize.Height));
        RefreshBottomLayout(force: true);
        bannerView = new BannerView(adUnitId, adSize, AdPosition.Bottom);
        bannerView.OnBannerAdLoaded += HandleBannerAdLoaded;
        bannerView.OnBannerAdLoadFailed += HandleBannerAdLoadFailed;
        bannerView.LoadAd(new AdRequest());

        Debug.Log($"[GameMainUIController] Banner ad load requested. unitId={adUnitId}, testMode={(isDebugBuildCached ? 1 : 0)}");
    }

    private string ResolveBannerAdUnitId()
    {
        bool useTestAdUnit = isDebugBuildCached;
#if UNITY_ANDROID
        return useTestAdUnit ? AndroidTestBannerAdUnitId : AndroidReleaseBannerAdUnitId;
#elif UNITY_IOS
        return useTestAdUnit ? IOSTestBannerAdUnitId : IOSReleaseBannerAdUnitId;
#else
        return string.Empty;
#endif
    }

    private void DestroyRewardedAd()
    {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        if (rewardedAd == null)
            return;

        rewardedAd.OnAdFullScreenContentClosed -= HandleRewardedAdFullScreenClosed;
        rewardedAd.OnAdFullScreenContentFailed -= HandleRewardedAdFullScreenFailed;
        rewardedAd.Destroy();
        rewardedAd = null;
#endif
    }

    private void HandleBannerAdLoaded()
    {
        if (bannerView != null)
            bannerView.Show();

        if (bannerView != null)
        {
            float loadedBannerHeight = bannerView.GetHeightInPixels();
            if (loadedBannerHeight > 0f)
            {
                reservedBannerHeightPx = loadedBannerHeight;
                nextBannerHeightPollTime = 0f;
                RefreshBottomLayout(force: true);
            }
        }

        if (bannerAdContainer != null)
            bannerAdContainer.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));

        if (bannerAdPlaceholder != null)
            bannerAdPlaceholder.style.display = DisplayStyle.None;
    }

    private void HandleBannerAdLoadFailed(LoadAdError loadError)
    {
        string errorMessage = loadError != null ? loadError.GetMessage() : "unknown";
        Debug.LogWarning($"[GameMainUIController] Banner ad load failed: {errorMessage}");
        reservedBannerHeightPx = 0f;
        RefreshBottomLayout(force: true);
        SetBannerPlaceholderText("AD LOAD FAILED");
    }
#endif

#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)
    private void DestroyRewardedAd()
    {
    }
#endif

    private void SetBannerPlaceholderText(string text)
    {
        if (bannerAdPlaceholder == null)
            return;
        bannerAdPlaceholder.style.display = DisplayStyle.Flex;
        bannerAdPlaceholder.text = text;
    }

    private void DestroyBannerAd()
    {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        if (bannerView == null)
            return;
        bannerView.Destroy();
        bannerView = null;
        reservedBannerHeightPx = 0f;
        nextBannerHeightPollTime = 0f;
        RefreshBottomLayout(force: true);
#endif
    }

    private float EstimateInitialBannerHeightPx()
    {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        AdSize estimatedSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
        if (estimatedSize != null && estimatedSize.Height > 0)
            return ConvertDpToPixels(estimatedSize.Height);
#endif
        return 80f;
    }

    private static float ConvertDpToPixels(float dp)
    {
        float dpi = Screen.dpi;
        if (dpi <= 0f)
            dpi = 160f;
        return Mathf.Ceil(dp * (dpi / 160f));
    }

    private void RefreshBottomLayout(bool force)
    {
        Rect currentSafeArea = Screen.safeArea;
        bool screenChanged = cachedScreenWidth != Screen.width || cachedScreenHeight != Screen.height;
        bool safeAreaChanged = !IsSameRect(cachedSafeArea, currentSafeArea);

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        if (bannerView != null)
        {
            if (force || Time.unscaledTime >= nextBannerHeightPollTime)
            {
                nextBannerHeightPollTime = Time.unscaledTime + Mathf.Max(0.1f, bannerHeightPollInterval);
                float liveBannerHeight = bannerView.GetHeightInPixels();
                if (liveBannerHeight > 0f && Mathf.Abs(liveBannerHeight - reservedBannerHeightPx) > 0.5f)
                {
                    reservedBannerHeightPx = liveBannerHeight;
                    force = true;
                }
            }
        }
#endif

        if (!force && !screenChanged && !safeAreaChanged)
            return;

        cachedScreenWidth = Screen.width;
        cachedScreenHeight = Screen.height;
        cachedSafeArea = currentSafeArea;

        float safeBottomPx = Mathf.Max(0f, currentSafeArea.yMin);
        float totalReservedBottomPx = safeBottomPx + Mathf.Max(0f, reservedBannerHeightPx);

        if (bannerAdContainer != null)
        {
            bannerAdContainer.style.height = totalReservedBottomPx;
            bannerAdContainer.style.paddingBottom = safeBottomPx;
        }

        if (bottomBar != null)
        {
            bottomBar.style.marginBottom = 0f;
            bottomBar.style.bottom = totalReservedBottomPx + Mathf.Max(0f, bottomBarExtraSpacing);
        }

        if (stageSnackbar != null)
            stageSnackbar.style.bottom = totalReservedBottomPx + Mathf.Max(0f, stageSnackbarExtraSpacing);
    }

    private static bool IsSameRect(Rect a, Rect b)
    {
        return Mathf.Abs(a.x - b.x) < 0.5f &&
               Mathf.Abs(a.y - b.y) < 0.5f &&
               Mathf.Abs(a.width - b.width) < 0.5f &&
               Mathf.Abs(a.height - b.height) < 0.5f;
    }

    /// <summary>스테이지 번호 및 전체 카운트로 상단 UI 초기화.</summary>
    public void SetupStage(int stageIndex, int totalCount, int remainingCount)
    {
        StopStageSnackbarPlayback();
        currentStageIndexForUI = Mathf.Max(1, stageIndex);

        // STAGE / 스테이지 번호 텍스트 갱신
        if (stageTitleLabel != null)
            stageTitleLabel.text = "STAGE";

        if (stageNumberLabel != null)
            stageNumberLabel.text = stageIndex.ToString("D2");

        initialTileCount = Mathf.Max(1, totalCount);
        UpdateProgress(remainingCount);
        TryShowScheduledTutorialForStage(currentStageIndexForUI);
        TryShowScheduledSnackbarForStage(currentStageIndexForUI);
    }

    /// <summary>남은 타일 카운트 기준으로 ProgressBar 갱신.</summary>
    public void UpdateProgress(int remainingCount)
    {
        if (gameProgressBar == null || initialTileCount <= 0)
            return;

        float used = Mathf.Clamp(initialTileCount - remainingCount, 0, initialTileCount);
        gameProgressBar.lowValue = 0f;
        gameProgressBar.highValue = initialTileCount;
        gameProgressBar.title = string.Empty;

        if (progressAnimRoutine != null)
            StopCoroutine(progressAnimRoutine);
        progressAnimRoutine = StartCoroutine(AnimateProgressTo(used));
    }

    private System.Collections.IEnumerator AnimateProgressTo(float targetValue)
    {
        float startValue = displayedProgressValue;
        float duration = Mathf.Max(0.01f, progressAnimDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            displayedProgressValue = Mathf.Lerp(startValue, targetValue, t);
            gameProgressBar.value = displayedProgressValue;
            yield return null;
        }

        displayedProgressValue = targetValue;
        gameProgressBar.value = displayedProgressValue;
        progressAnimRoutine = null;
    }
}
