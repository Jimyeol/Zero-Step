using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;
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
    private const string SaveKeyLanguageSelection = "SettingLanguageSelection";
    private const string PrivacyUrl = "https://www.naver.com";
    private const string SupportEmailAddress = "crewoongcrewoong@gmail.com";
    private const string NeonPressBaseClass = "neon-press-button";
    private const string NeonPressWarmClass = "neon-press-button-warm";
    private const string NeonPressActiveClass = "neon-press-active";
    private const string AndroidReleaseBannerAdUnitId = "ca-app-pub-1863948941169747/1159516189";
    private const string IOSReleaseBannerAdUnitId = "ca-app-pub-1863948941169747/3645749158";
    private const string AndroidTestBannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";
    private const string IOSTestBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
    private const string AndroidReleaseInterstitialAdUnitId = "ca-app-pub-1863948941169747/3047278507";
    private const string IOSReleaseInterstitialAdUnitId = "ca-app-pub-1863948941169747/9983863158";
    private const string AndroidTestInterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";
    private const string IOSTestInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";
    private const string AndroidReleaseRewardedAdUnitId = "ca-app-pub-1863948941169747/7021684124";
    private const string IOSReleaseRewardedAdUnitId = "ca-app-pub-1863948941169747/6383389878";
    private const string AndroidTestRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
    private const string IOSTestRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";
    private const string AndroidReleaseStageSkipRewardedAdUnitId = "ca-app-pub-1863948941169747/4356131957";
    private const string IOSReleaseStageSkipRewardedAdUnitId = "ca-app-pub-1863948941169747/6100458466";
    private const string AndroidTestStageSkipRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
    private const string IOSTestStageSkipRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";
    private const string StageSkipRewardName = "Stage Skip";
    private const int StageSkipRewardAmount = 1;
    private const int StageTransitionInterstitialInterval = 15;
    private const int MaxHearts = 3;
    private const string TutorialScheduleResourcePath = "Tutorials/help_tutorial_schedule";
    private const string StageSnackbarScheduleResourcePath = "Tutorials/stage_snackbar_schedule";
    private const string TutorialDismissedKeyPrefix = "TutorialDismissed_";
    private const string TutorialTypeBasicPath = "BasicPath";
    private const string TutorialTypeShortCircuit = "ShortCircuit";
    private const string DefaultUIButtonSfxResourcePath = "Sounds/ui_button";
    private const string DefaultSplashSpriteResourcePath = "Sprites/splash";
    private const string DefaultSplashVideoResourcePath = "Sprites/splash_video";

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
        public string titleKey;
        public string title = "기본 플레이 방법";
        public string descriptionKey;
        public string description = "왼쪽(1) → 중앙(2) → 오른쪽(1) → 중앙으로 이동하면 카운트가 줄어들며 클리어됩니다.";
        public string closeButtonTextKey;
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
        public string messageKey;
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
    [Header("UI 버튼 사운드")]
    [SerializeField] private string uiButtonSfxResourcePath = DefaultUIButtonSfxResourcePath;
    [SerializeField] [Range(0f, 1f)] private float uiButtonSfxVolume = 1f;
    [Header("스플래시")]
    [SerializeField] private string splashSpriteResourcePath = DefaultSplashSpriteResourcePath;
    [SerializeField] private string splashVideoResourcePath = DefaultSplashVideoResourcePath;
    [SerializeField] [Range(0.5f, 8f)] private float splashMinimumDuration = 1.6f;
    [SerializeField] [Range(2f, 20f)] private float splashMaximumWait = 8f;
    [SerializeField] [Range(0.05f, 1f)] private float splashFadeOutDuration = 0.35f;
    [SerializeField] [Range(0.05f, 2f)] private float splashVideoFadeOutDuration = 0.5f;
    [SerializeField] [Range(0f, 0.08f)] private float splashPulseScale = 0.028f;
    [SerializeField] [Range(0.1f, 4f)] private float splashPulseSpeed = 1.2f;
    [SerializeField] [Range(0f, 30f)] private float splashFloatDistancePx = 10f;
    [SerializeField] [Range(0.1f, 4f)] private float splashFloatSpeed = 0.8f;

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
    private Label settingTitleTextLabel;
    private Label gameSettingSectionLabel;
    private Label helpLanguageSectionLabel;
    private Label recommendServiceSectionLabel;
    private Label dataPolicySectionLabel;
    private Label soundRowLabel;
    private Label vibrationRowLabel;
    private Label helpMenuLabel;
    private Label languageMenuLabel;
    private Label resetConfirmTitleLabel;
    private Label resetConfirmMessageLabel;
    private VisualElement languageSelectOverlay;
    private VisualElement languageSelectDialog;
    private Label languageSelectTitleLabel;
    private Button languageSelectCloseButton;
    private Image languageSelectCloseIcon;
    private VisualElement languageOptionList;
    private VisualElement tutorialOverlay;
    private VisualElement tutorialDialog;
    private VisualElement tutorialBasicDemoBoard;
    private VisualElement tutorialShortCircuitDemoBoard;
    private Label tutorialTitleLabel;
    private Label tutorialDescriptionLabel;
    private Label tutorialStepHintLabel;
    private Button tutorialCloseButton;
    private Image tutorialCloseIcon;
    private Button tutorialConfirmButton;
    private VisualElement tutorialTileLeft;
    private VisualElement tutorialTileCenter;
    private VisualElement tutorialTileRight;
    private VisualElement tutorialShortTileTopLeft;
    private VisualElement tutorialShortTileTopRight;
    private VisualElement tutorialShortTileBottomLeft;
    private VisualElement tutorialShortTileBottomRight;
    private Label tutorialTileLeftCount;
    private Label tutorialTileCenterCount;
    private Label tutorialTileRightCount;
    private Label tutorialShortTileTopLeftCount;
    private Label tutorialShortTileTopRightCount;
    private Label tutorialShortTileBottomLeftCount;
    private Label tutorialShortTileBottomRightCount;
    private Label tutorialShortTileBottomLeftArrow;
    private Image tutorialHandImage;
    private Image tutorialShortCircuitHandImage;
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
    private AudioSource uiButtonSfxAudioSource;
    private AudioClip uiButtonSfxClip;
    private bool uiButtonSfxMissingLogged;
    private Sprite heartFilledSprite;
    private Sprite heartEmptySprite;
    private bool isSoundOn = true;
    private bool isVibrationOn = true;
    private string selectedLanguageCode = GameLocalization.LanguageAuto;
    private string activeLanguageCode = "en";
    public string ActiveLanguageCode => activeLanguageCode;
    private bool isDebugBuildCached;
    private volatile bool pendingBannerLoadFromInitialize;
    private volatile bool pendingRewardedAdLoadFromInitialize;
    private volatile bool pendingStageSkipRewardedAdLoadFromInitialize;
    private volatile bool pendingInterstitialLoadFromInitialize;
    private volatile bool pendingShowRewardedAd;
    private float reservedBannerHeightPx;
    private float nextBannerHeightPollTime;
    private int cachedScreenWidth = -1;
    private int cachedScreenHeight = -1;
    private Rect cachedSafeArea;
    private int currentHearts = MaxHearts;
    private bool isWaitingForHeartRefill;
    private bool isLanguageSelectionPopupOpen;
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
    private Coroutine splashFadeRoutine;
    private bool isSplashActive;
    private bool isSplashClosing;
    private float splashStartTime;
    private bool splashStageReady;
    private bool splashBannerReady;
    private bool splashHeartRewardedReady;
    private bool splashStageSkipRewardedReady;
    private bool splashInterstitialReady;
    private bool splashTimerStarted;
    private bool splashVideoLoaded;
    private bool splashVideoEnded;
    private bool splashVideoFadeStarted;
    private float splashVideoFadeStartTime;
    private VisualElement splashOverlay;
    private Image splashImage;
    private Label splashLoadingLabel;
    private VisualElement splashLoadingProgressFill;
    private VideoPlayer splashVideoPlayer;
    private RenderTexture splashVideoRenderTexture;
    private HeartRefillMode currentHeartRefillMode = HeartRefillMode.RewardedAd;
    private int currentSessionPlayRewardMinutes;
    private Coroutine stageSnackbarRoutine;
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
    private BannerView bannerView;
    private RewardedAd rewardedAd;
    private bool isRewardedAdLoading;
    private bool rewardEarnedThisShow;
    private RewardedAd stageSkipRewardedAd;
    private bool isStageSkipRewardedAdLoading;
    private bool stageSkipRewardEarnedThisShow;
    private Action pendingStageSkipRewardCompletionAction;
    private InterstitialAd stageTransitionInterstitialAd;
    private bool isStageTransitionInterstitialAdLoading;
    private bool isStageTransitionInterstitialShowing;
    private Action pendingStageTransitionInterstitialCompletionAction;
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
        InitializeSplashState();

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
        resetConfirmTitleLabel = root.Q<Label>("ResetConfirmTitle");
        resetConfirmMessageLabel = root.Q<Label>("ResetConfirmMessage");
        languageSelectOverlay = root.Q<VisualElement>("LanguageSelectOverlay");
        languageSelectDialog = root.Q<VisualElement>("LanguageSelectDialog");
        languageSelectTitleLabel = root.Q<Label>("LanguageSelectTitleLabel");
        languageSelectCloseButton = root.Q<Button>("LanguageSelectCloseButton");
        languageSelectCloseIcon = root.Q<Image>("LanguageSelectCloseIcon");
        languageOptionList = root.Q<VisualElement>("LanguageOptionList");
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
        settingTitleTextLabel = root.Q<Label>("SettingTitleLabel");
        gameSettingSectionLabel = root.Q<Label>("GameSettingSectionLabel");
        helpLanguageSectionLabel = root.Q<Label>("HelpLanguageSectionLabel");
        recommendServiceSectionLabel = root.Q<Label>("RecommendServiceSectionLabel");
        dataPolicySectionLabel = root.Q<Label>("DataPolicySectionLabel");
        soundRowLabel = root.Q<Label>("SoundRowLabel");
        vibrationRowLabel = root.Q<Label>("VibrationRowLabel");
        helpMenuLabel = root.Q<Label>("HelpMenuLabel");
        languageMenuLabel = root.Q<Label>("LanguageMenuLabel");
        tutorialOverlay = root.Q<VisualElement>("TutorialOverlay");
        tutorialDialog = root.Q<VisualElement>("TutorialDialog");
        tutorialBasicDemoBoard = root.Q<VisualElement>("TutorialBasicDemoBoard");
        tutorialShortCircuitDemoBoard = root.Q<VisualElement>("TutorialShortCircuitDemoBoard");
        tutorialTitleLabel = root.Q<Label>("TutorialTitleLabel");
        tutorialDescriptionLabel = root.Q<Label>("TutorialDescriptionLabel");
        tutorialStepHintLabel = root.Q<Label>("TutorialStepHintLabel");
        tutorialCloseButton = root.Q<Button>("TutorialCloseButton");
        tutorialCloseIcon = root.Q<Image>("TutorialCloseIcon");
        tutorialConfirmButton = root.Q<Button>("TutorialConfirmButton");
        tutorialTileLeft = root.Q<VisualElement>("TutorialTileLeft");
        tutorialTileCenter = root.Q<VisualElement>("TutorialTileCenter");
        tutorialTileRight = root.Q<VisualElement>("TutorialTileRight");
        tutorialShortTileTopLeft = root.Q<VisualElement>("TutorialShortTileTopLeft");
        tutorialShortTileTopRight = root.Q<VisualElement>("TutorialShortTileTopRight");
        tutorialShortTileBottomLeft = root.Q<VisualElement>("TutorialShortTileBottomLeft");
        tutorialShortTileBottomRight = root.Q<VisualElement>("TutorialShortTileBottomRight");
        tutorialTileLeftCount = root.Q<Label>("TutorialTileLeftCount");
        tutorialTileCenterCount = root.Q<Label>("TutorialTileCenterCount");
        tutorialTileRightCount = root.Q<Label>("TutorialTileRightCount");
        tutorialShortTileTopLeftCount = root.Q<Label>("TutorialShortTileTopLeftCount");
        tutorialShortTileTopRightCount = root.Q<Label>("TutorialShortTileTopRightCount");
        tutorialShortTileBottomLeftCount = root.Q<Label>("TutorialShortTileBottomLeftCount");
        tutorialShortTileBottomRightCount = root.Q<Label>("TutorialShortTileBottomRightCount");
        tutorialShortTileBottomLeftArrow = root.Q<Label>("TutorialShortTileBottomLeftArrow");
        tutorialHandImage = root.Q<Image>("TutorialHandImage");
        tutorialShortCircuitHandImage = root.Q<Image>("TutorialShortCircuitHandImage");
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
        if (languageSelectOverlay != null)
            languageSelectOverlay.style.display = DisplayStyle.None;
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
        if (languageSelectDialog != null)
        {
            languageSelectDialog.style.opacity = 1f;
            languageSelectDialog.style.scale = new StyleScale(new Scale(Vector3.one));
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
        if (languageSelectCloseButton != null)
            languageSelectCloseButton.clicked += HideLanguageSelectionPopup;

        AssignSprite(settingCloseIcon, "Sprites/close", "close.png");
        AssignSprite(languageSelectCloseIcon, "Sprites/close", "close.png");
        AssignSprite(tutorialCloseIcon, "Sprites/close", "close.png");
        AssignSprite(tutorialHandImage, "Sprites/hand", "hand.png");
        AssignSprite(tutorialShortCircuitHandImage, "Sprites/hand", "hand.png");
        AssignSprite(soundIcon, "Sprites/sound", "sound.png");
        AssignSprite(vibrationIcon, "Sprites/vibrate", "vibrate.png");
        AssignSprite(helpIcon, "Sprites/help", "help.png");
        AssignSprite(languageIcon, "Sprites/global", "global.png");
        heartFilledSprite = Resources.Load<Sprite>("Sprites/heart");
        heartEmptySprite = Resources.Load<Sprite>("Sprites/heart_empty");
        LoadHelpTutorialSchedule();
        LoadStageSnackbarSchedule();
        InitializeUIButtonSfx();

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
            languageButton.clicked += OpenLanguageSelectionPopup;
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

        ApplyLocalizationForCurrentLanguage();
        SetupButtonClickAnimations();
        ConfigureHeartDepletedPopupForRewardedAd();
        RefreshHeartVisuals();
        reservedBannerHeightPx = EstimateInitialBannerHeightPx();
        RefreshBottomLayout(force: true);
        InitializeBannerAd();
    }

    private void Start()
    {
        EnsureSplashOverlayAttached();
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
        if (pendingStageSkipRewardedAdLoadFromInitialize)
        {
            pendingStageSkipRewardedAdLoadFromInitialize = false;
            LoadStageSkipRewardedAd();
        }
        if (pendingInterstitialLoadFromInitialize)
        {
            pendingInterstitialLoadFromInitialize = false;
            LoadStageTransitionInterstitialAd();
        }
        if (pendingShowRewardedAd)
        {
            pendingShowRewardedAd = false;
            ShowRewardedAdInternal();
        }
#endif
        RefreshBottomLayout(force: false);
        EnsureSplashOverlayAttached();
        UpdateSplashVisual();
        TryCloseSplashIfReady();
    }

    private void OnDestroy()
    {
        StopStageSnackbarPlayback();
        StopTutorialAnimation();
        if (splashFadeRoutine != null)
            StopCoroutine(splashFadeRoutine);
        splashFadeRoutine = null;
        CleanupSplashVideo();
        DestroyBannerAd();
        DestroyRewardedAd();
        DestroyStageSkipRewardedAd();
        DestroyStageTransitionInterstitialAd();
        isSplashActive = false;
        isSplashClosing = false;
        splashOverlay = null;
        splashImage = null;
        splashLoadingLabel = null;
        splashLoadingProgressFill = null;
    }

    private void InitializeUIButtonSfx()
    {
        if (uiButtonSfxAudioSource == null)
        {
            Transform existing = transform.Find("UIButtonSfxAudioSource");
            GameObject child = existing != null ? existing.gameObject : new GameObject("UIButtonSfxAudioSource");
            if (existing == null)
                child.transform.SetParent(transform, false);
            uiButtonSfxAudioSource = child.GetComponent<AudioSource>();
            if (uiButtonSfxAudioSource == null)
                uiButtonSfxAudioSource = child.AddComponent<AudioSource>();
        }

        uiButtonSfxAudioSource.playOnAwake = false;
        uiButtonSfxAudioSource.loop = false;
        uiButtonSfxAudioSource.spatialBlend = 0f;
        uiButtonSfxAudioSource.dopplerLevel = 0f;
        uiButtonSfxAudioSource.rolloffMode = AudioRolloffMode.Linear;

        if (uiButtonSfxClip == null)
        {
            string safePath = string.IsNullOrWhiteSpace(uiButtonSfxResourcePath)
                ? DefaultUIButtonSfxResourcePath
                : uiButtonSfxResourcePath;
            uiButtonSfxClip = Resources.Load<AudioClip>(safePath);
            if (uiButtonSfxClip == null && !uiButtonSfxMissingLogged)
            {
                uiButtonSfxMissingLogged = true;
                Debug.LogWarning($"[GameMainUIController] UI 버튼 사운드를 찾을 수 없습니다: Resources/{safePath}.wav");
            }
        }
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
        RegisterButtonClickAnimation(languageSelectCloseButton);
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

        PlayUIButtonSfx();

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

    private void PlayUIButtonSfx()
    {
        if (uiButtonSfxAudioSource == null || uiButtonSfxClip == null)
            return;
        uiButtonSfxAudioSource.PlayOneShot(uiButtonSfxClip, Mathf.Clamp01(uiButtonSfxVolume));
    }

    /// <summary>스킵 버튼 클릭: 보상형 광고를 시청하면 현재 스테이지를 건너뛴다.</summary>
    private void OnSkipClicked()
    {
        FirebaseBootstrap.LogEvent("ui_button_click", new Dictionary<string, object>
        {
            { "button_name", "skip" }
        });

        var gm = FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.Log("스테이지 스킵됨 (GameManager 없음)");
            return;
        }

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        TryShowStageSkipRewardedAd(() =>
        {
            if (gm != null)
                gm.LoadNextStageImmediate();
        });
#else
        // 에디터에서는 광고 없이 즉시 동작.
        if (gm != null)
            gm.LoadNextStageImmediate();
#endif
    }

    public void ShowStageTransitionInterstitialIfNeeded(int completedStageIndex, Action onCompleted)
    {
        Action completion = onCompleted ?? (() => { });
        if (completedStageIndex <= 0 || completedStageIndex % StageTransitionInterstitialInterval != 0)
        {
            completion.Invoke();
            return;
        }

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        if (TryShowStageTransitionInterstitial(completion))
            return;
#endif
        completion.Invoke();
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
        HideLanguageSelectionPopup();
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
    public bool IsSplashActive => isSplashActive;

    public void NotifyStageBootstrapCompleted()
    {
        splashStageReady = true;
    }

    private void InitializeSplashState()
    {
        splashStartTime = 0f;
        splashTimerStarted = false;
        isSplashActive = true;
        isSplashClosing = false;
        splashStageReady = false;
        splashVideoLoaded = false;
        splashVideoEnded = false;
        splashVideoFadeStarted = false;
        splashVideoFadeStartTime = 0f;

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        splashBannerReady = false;
        splashHeartRewardedReady = false;
        splashStageSkipRewardedReady = false;
        splashInterstitialReady = false;
#else
        splashBannerReady = true;
        splashHeartRewardedReady = true;
        splashStageSkipRewardedReady = true;
        splashInterstitialReady = true;
#endif
    }

    private void EnsureSplashOverlayAttached()
    {
        if (!isSplashActive || uiDocument == null)
            return;

        if (splashOverlay != null && splashOverlay.parent != null)
        {
            splashOverlay.BringToFront();
            return;
        }

        VisualElement root = uiDocument.rootVisualElement;
        if (root == null)
            return;

        CreateSplashOverlay(root);
        splashOverlay?.BringToFront();
    }

    private void CreateSplashOverlay(VisualElement root)
    {
        if (root == null)
            return;

        if (splashOverlay != null)
            splashOverlay.RemoveFromHierarchy();

        splashOverlay = new VisualElement
        {
            name = "RuntimeSplashOverlay",
            pickingMode = PickingMode.Position
        };
        splashOverlay.style.position = Position.Absolute;
        splashOverlay.style.left = 0f;
        splashOverlay.style.top = 0f;
        splashOverlay.style.right = 0f;
        splashOverlay.style.bottom = 0f;
        splashOverlay.style.justifyContent = Justify.Center;
        splashOverlay.style.alignItems = Align.Center;
        splashOverlay.style.backgroundColor = new StyleColor(new Color(0.01f, 0.02f, 0.04f, 1f));
        splashOverlay.style.opacity = 1f;

        VisualElement logoWrap = new VisualElement();
        logoWrap.style.position = Position.Absolute;
        logoWrap.style.left = 0f;
        logoWrap.style.top = 0f;
        logoWrap.style.right = 0f;
        logoWrap.style.bottom = 0f;
        logoWrap.style.width = Length.Percent(100f);
        logoWrap.style.height = Length.Percent(100f);
        logoWrap.style.alignItems = Align.Center;
        logoWrap.style.justifyContent = Justify.Center;
        logoWrap.style.flexDirection = FlexDirection.Column;

        splashImage = new Image();
        splashImage.style.width = Length.Percent(100f);
        splashImage.style.height = Length.Percent(100f);
        splashImage.scaleMode = ScaleMode.ScaleAndCrop;
        logoWrap.Add(splashImage);

        string safePath = string.IsNullOrWhiteSpace(splashSpriteResourcePath)
            ? DefaultSplashSpriteResourcePath
            : splashSpriteResourcePath;
        Texture splashTexture = TryLoadSplashTexture(safePath);
        if (splashTexture != null)
            splashImage.image = splashTexture;

        bool videoLoadedNow = TrySetupSplashVideo();
        if (splashTexture == null && !videoLoadedNow)
        {
            logoWrap.Remove(splashImage);
            splashImage = null;
            Label fallbackLabel = new Label("ZERO STEP");
            fallbackLabel.style.fontSize = 92f;
            fallbackLabel.style.color = new StyleColor(Color.white);
            fallbackLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            logoWrap.Add(fallbackLabel);
            Debug.LogWarning($"[GameMainUIController] 스플래시 이미지 리소스를 찾을 수 없습니다: Resources/{safePath}.png");
        }

        VisualElement loadingWrap = new VisualElement();
        loadingWrap.style.position = Position.Absolute;
        loadingWrap.style.left = 0f;
        loadingWrap.style.right = 0f;
        loadingWrap.style.bottom = 80f;
        loadingWrap.style.alignItems = Align.Center;
        loadingWrap.style.justifyContent = Justify.Center;
        loadingWrap.style.flexDirection = FlexDirection.Column;

        splashLoadingLabel = new Label("Loading");
        splashLoadingLabel.style.fontSize = 28f;
        splashLoadingLabel.style.color = new StyleColor(new Color(0.82f, 0.92f, 1f, 1f));
        splashLoadingLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        splashLoadingLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        splashLoadingLabel.style.letterSpacing = 0.8f;
        loadingWrap.Add(splashLoadingLabel);

        VisualElement progressTrack = new VisualElement();
        progressTrack.style.width = 340f;
        progressTrack.style.height = 10f;
        progressTrack.style.marginTop = 16f;
        progressTrack.style.backgroundColor = new StyleColor(new Color(0.2f, 0.28f, 0.36f, 0.65f));
        progressTrack.style.borderTopLeftRadius = 999f;
        progressTrack.style.borderTopRightRadius = 999f;
        progressTrack.style.borderBottomLeftRadius = 999f;
        progressTrack.style.borderBottomRightRadius = 999f;
        progressTrack.style.overflow = Overflow.Hidden;

        splashLoadingProgressFill = new VisualElement();
        splashLoadingProgressFill.style.width = Length.Percent(0f);
        splashLoadingProgressFill.style.height = Length.Percent(100f);
        splashLoadingProgressFill.style.backgroundColor = new StyleColor(new Color(0.46f, 0.82f, 1f, 0.95f));
        progressTrack.Add(splashLoadingProgressFill);
        loadingWrap.Add(progressTrack);

        splashOverlay.Add(logoWrap);
        splashOverlay.Add(loadingWrap);
        root.Add(splashOverlay);
        Debug.Log($"[Splash] Overlay attached. root={root.name}, imageLoaded={(splashTexture != null ? 1 : 0)}, videoLoaded={(videoLoadedNow ? 1 : 0)}, resourcePath={safePath}");
        if (!splashTimerStarted)
        {
            splashStartTime = Time.unscaledTime;
            splashTimerStarted = true;
        }
    }

    private Texture TryLoadSplashTexture(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        Sprite singleSprite = Resources.Load<Sprite>(resourcePath);
        if (singleSprite != null)
            return singleSprite.texture;

        Sprite[] slicedSprites = Resources.LoadAll<Sprite>(resourcePath);
        if (slicedSprites != null && slicedSprites.Length > 0 && slicedSprites[0] != null)
            return slicedSprites[0].texture;

        return Resources.Load<Texture2D>(resourcePath);
    }

    private bool TrySetupSplashVideo()
    {
        if (splashImage == null)
            return false;

        string safeVideoPath = string.IsNullOrWhiteSpace(splashVideoResourcePath)
            ? DefaultSplashVideoResourcePath
            : splashVideoResourcePath;
        VideoClip clip = Resources.Load<VideoClip>(safeVideoPath);
        if (clip == null)
        {
            splashVideoLoaded = false;
            splashVideoEnded = false;
            splashVideoFadeStarted = false;
            return false;
        }

        if (splashVideoPlayer == null)
            splashVideoPlayer = GetComponent<VideoPlayer>();
        if (splashVideoPlayer == null)
            splashVideoPlayer = gameObject.AddComponent<VideoPlayer>();
        if (splashVideoPlayer.isPlaying)
            splashVideoPlayer.Stop();

        ReleaseSplashVideoRenderTexture();
        int textureWidth = Mathf.Max(64, (int)(clip.width > 0 ? clip.width : 1080u));
        int textureHeight = Mathf.Max(64, (int)(clip.height > 0 ? clip.height : 1920u));
        splashVideoRenderTexture = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGB32);
        splashVideoRenderTexture.Create();

        splashVideoPlayer.errorReceived -= HandleSplashVideoError;
        splashVideoPlayer.loopPointReached -= HandleSplashVideoLoopPointReached;
        splashVideoPlayer.prepareCompleted -= HandleSplashVideoPrepared;

        splashVideoPlayer.playOnAwake = false;
        splashVideoPlayer.source = VideoSource.VideoClip;
        splashVideoPlayer.clip = clip;
        splashVideoPlayer.isLooping = false;
        splashVideoPlayer.skipOnDrop = true;
        splashVideoPlayer.waitForFirstFrame = true;
        splashVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        splashVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
        splashVideoPlayer.targetTexture = splashVideoRenderTexture;

        splashVideoPlayer.errorReceived += HandleSplashVideoError;
        splashVideoPlayer.loopPointReached += HandleSplashVideoLoopPointReached;
        splashVideoPlayer.prepareCompleted += HandleSplashVideoPrepared;

        splashImage.image = splashVideoRenderTexture;
        splashImage.scaleMode = ScaleMode.ScaleAndCrop;
        splashImage.style.opacity = 1f;
        splashImage.style.display = DisplayStyle.Flex;

        splashVideoLoaded = true;
        splashVideoEnded = false;
        splashVideoFadeStarted = false;
        splashVideoFadeStartTime = 0f;
        splashVideoPlayer.Prepare();
        return true;
    }

    private void HandleSplashVideoPrepared(VideoPlayer source)
    {
        if (!isSplashActive || source == null)
            return;
        source.Play();
    }

    private void HandleSplashVideoLoopPointReached(VideoPlayer source)
    {
        splashVideoEnded = true;
    }

    private void HandleSplashVideoError(VideoPlayer source, string message)
    {
        Debug.LogWarning($"[GameMainUIController] Splash video playback failed: {message}");
        splashVideoEnded = true;
        StartSplashVideoFadeOut();
    }

    private void StartSplashVideoFadeOut()
    {
        if (!splashVideoLoaded || splashVideoFadeStarted || splashImage == null)
            return;

        splashVideoFadeStarted = true;
        splashVideoFadeStartTime = Time.unscaledTime;
    }

    private void CleanupSplashVideo()
    {
        if (splashVideoPlayer != null)
        {
            splashVideoPlayer.errorReceived -= HandleSplashVideoError;
            splashVideoPlayer.loopPointReached -= HandleSplashVideoLoopPointReached;
            splashVideoPlayer.prepareCompleted -= HandleSplashVideoPrepared;
            if (splashVideoPlayer.isPlaying)
                splashVideoPlayer.Stop();
            splashVideoPlayer.targetTexture = null;
            splashVideoPlayer.clip = null;
        }

        ReleaseSplashVideoRenderTexture();
        splashVideoLoaded = false;
        splashVideoEnded = false;
        splashVideoFadeStarted = false;
    }

    private void ReleaseSplashVideoRenderTexture()
    {
        if (splashVideoRenderTexture == null)
            return;

        if (splashVideoRenderTexture.IsCreated())
            splashVideoRenderTexture.Release();
        Destroy(splashVideoRenderTexture);
        splashVideoRenderTexture = null;
    }

    private void UpdateSplashVisual()
    {
        if (!isSplashActive || isSplashClosing || splashOverlay == null)
            return;

        float elapsed = Time.unscaledTime - splashStartTime;
        if (splashImage != null && !splashVideoLoaded)
        {
            float pulse = 1f + splashPulseScale * Mathf.Sin(elapsed * splashPulseSpeed * Mathf.PI * 2f);
            float yOffset = splashFloatDistancePx * Mathf.Sin(elapsed * splashFloatSpeed * Mathf.PI * 2f);
            splashImage.style.scale = new StyleScale(new Scale(new Vector3(pulse, pulse, 1f)));
            splashImage.style.translate = new StyleTranslate(new Translate(
                new Length(0f, LengthUnit.Pixel),
                new Length(yOffset, LengthUnit.Pixel),
                0f));
        }

        if (splashVideoFadeStarted && splashImage != null)
        {
            float fadeDuration = Mathf.Max(0.05f, splashVideoFadeOutDuration);
            float fadeT = Mathf.Clamp01((Time.unscaledTime - splashVideoFadeStartTime) / fadeDuration);
            splashImage.style.opacity = 1f - fadeT;
            if (fadeT >= 1f)
                splashImage.style.display = DisplayStyle.None;
        }

        if (splashLoadingLabel != null)
        {
            int dotCount = Mathf.FloorToInt((Time.unscaledTime * 2.4f) % 4f);
            splashLoadingLabel.text = $"Loading{new string('.', dotCount)}";
        }

        if (splashLoadingProgressFill != null)
            splashLoadingProgressFill.style.width = Length.Percent(GetSplashReadinessProgress01() * 100f);
    }

    private float GetSplashReadinessProgress01()
    {
        int readyCount = 0;
        if (splashStageReady) readyCount++;
        if (splashBannerReady) readyCount++;
        if (splashHeartRewardedReady) readyCount++;
        if (splashStageSkipRewardedReady) readyCount++;
        if (splashInterstitialReady) readyCount++;
        return readyCount / 5f;
    }

    private bool IsSplashReady()
    {
        return splashStageReady &&
               splashBannerReady &&
               splashHeartRewardedReady &&
               splashStageSkipRewardedReady &&
               splashInterstitialReady;
    }

    private void TryCloseSplashIfReady()
    {
        if (!isSplashActive || isSplashClosing)
            return;
        if (!splashTimerStarted)
            return;

        float elapsed = Time.unscaledTime - splashStartTime;
        bool isReady = IsSplashReady();
        bool minDurationPassed = elapsed >= Mathf.Max(0.1f, splashMinimumDuration);
        bool readyToClose = splashVideoLoaded
            ? (isReady && splashVideoEnded)
            : (isReady && minDurationPassed);
        bool timeout = elapsed >= Mathf.Max(splashMinimumDuration, splashMaximumWait);
        if (!isReady && splashVideoLoaded && splashVideoEnded)
            StartSplashVideoFadeOut();

        if (readyToClose || timeout)
        {
            if (timeout && !isReady)
                Debug.LogWarning("[GameMainUIController] Splash timeout reached before full preload; continue to gameplay.");

            isSplashClosing = true;
            splashFadeRoutine = StartCoroutine(CloseSplashRoutine());
        }
    }

    private IEnumerator CloseSplashRoutine()
    {
        float duration = Mathf.Max(0.01f, splashFadeOutDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (splashOverlay != null)
                splashOverlay.style.opacity = 1f - t;
            yield return null;
        }

        if (splashOverlay != null)
            splashOverlay.RemoveFromHierarchy();

        splashOverlay = null;
        splashImage = null;
        splashLoadingLabel = null;
        splashLoadingProgressFill = null;
        splashFadeRoutine = null;
        isSplashActive = false;
        isSplashClosing = false;
        splashTimerStarted = false;
        CleanupSplashVideo();
    }

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
            SetHeartRefillStatus(T("heart_status_session_reward", ("minutes", rewardMinutes.ToString())));
            return;
        }

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        bool canShow = rewardedAd != null && rewardedAd.CanShowAd();
        heartRefillAdButton.SetEnabled(canShow);
        if (canShow)
            SetHeartRefillStatus(T("heart_status_reward_ready"));
        else
            SetHeartRefillStatus(T("heart_status_loading_ad"));
#else
        heartRefillAdButton.SetEnabled(true);
        SetHeartRefillStatus(T("heart_status_editor"));
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
            SetHeartRefillStatus(T("heart_status_opening_ad"));
            pendingShowRewardedAd = true;
            return;
        }

        SetHeartRefillStatus(T("heart_status_prepare_retry"));
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
            T("heart_rewarded_title"),
            T("heart_rewarded_message"),
            T("heart_rewarded_hint"),
            T("heart_rewarded_button"));
    }

    private void ConfigureHeartDepletedPopupForSessionReward(int thresholdMinutes)
    {
        currentHeartRefillMode = HeartRefillMode.SessionPlayReward;
        currentSessionPlayRewardMinutes = Mathf.Max(1, thresholdMinutes);
        SetHeartDepletedPopupCopy(
            T("heart_session_title"),
            T("heart_session_message", ("minutes", currentSessionPlayRewardMinutes.ToString())),
            T("heart_session_hint"),
            T("heart_session_button"));
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

            if (string.IsNullOrWhiteSpace(entry.titleKey) && string.IsNullOrWhiteSpace(entry.title))
                entry.titleKey = "help_generic_title";

            if (string.IsNullOrWhiteSpace(entry.descriptionKey) && string.IsNullOrWhiteSpace(entry.description))
                entry.descriptionKey = "help_generic_description";

            if (string.IsNullOrWhiteSpace(entry.closeButtonTextKey) && string.IsNullOrWhiteSpace(entry.closeButtonText))
                entry.closeButtonTextKey = "help_close_button";

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
            titleKey = "tutorial_basic_title",
            descriptionKey = "tutorial_basic_description",
            closeButtonTextKey = "help_close_button"
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
            if (string.IsNullOrWhiteSpace(entry.messageKey) && string.IsNullOrWhiteSpace(entry.message))
                entry.messageKey = "snackbar_default_new_tile_unlock";
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
        string messageTemplate = ResolveStageSnackbarTemplate(entry);
        if (string.IsNullOrWhiteSpace(messageTemplate))
            return string.Empty;

        int targetStageIndex = entry.targetStageIndex > 0 ? entry.targetStageIndex : currentStageIndex;
        int remainingStages = Mathf.Max(0, targetStageIndex - currentStageIndex);

        string message = messageTemplate;
        message = message.Replace("{currentStage}", currentStageIndex.ToString());
        message = message.Replace("{targetStage}", targetStageIndex.ToString());
        message = message.Replace("{remainingStages}", remainingStages.ToString());
        return message.Trim();
    }

    public void ShowGameplaySnackbar(string localizationKey, params (string key, string value)[] replacements)
    {
        ShowGameplaySnackbar(localizationKey, 1.6f, replacements);
    }

    public void ShowGameplaySnackbar(string localizationKey, float durationSeconds, params (string key, string value)[] replacements)
    {
        if (string.IsNullOrWhiteSpace(localizationKey))
            return;

        string message = replacements != null && replacements.Length > 0
            ? T(localizationKey, replacements)
            : T(localizationKey);
        if (string.IsNullOrWhiteSpace(message))
            return;

        StopStageSnackbarPlayback();
        stageSnackbarRoutine = StartCoroutine(PlayGameplaySnackbar(message.Trim(), durationSeconds));
    }

    private IEnumerator PlayGameplaySnackbar(string message, float durationSeconds)
    {
        if (stageSnackbar == null || stageSnackbarLabel == null)
            yield break;

        stageSnackbarAnimationVersion++;
        int animationVersion = stageSnackbarAnimationVersion;

        ShowStageSnackbar(message, animationVersion, logAnalytics: false);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.8f, durationSeconds));

        if (!IsCurrentStageSnackbarAnimationVersion(animationVersion))
            yield break;

        HideStageSnackbar(animationVersion);
        yield return new WaitForSecondsRealtime(0.2f);

        if (IsCurrentStageSnackbarAnimationVersion(animationVersion))
            stageSnackbarRoutine = null;
    }

    private void ShowStageSnackbar(string message, int animationVersion, bool logAnalytics = true)
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

        if (logAnalytics)
        {
            FirebaseBootstrap.LogEvent("stage_snackbar_show", new Dictionary<string, object>
            {
                { "stage_index", currentStageIndexForUI },
                { "message", message }
            });
        }
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
            tutorialTitleLabel.text = ResolveTutorialTitle(entry);
        if (tutorialDescriptionLabel != null)
            tutorialDescriptionLabel.text = ResolveTutorialDescription(entry);
        if (tutorialConfirmButton != null)
            tutorialConfirmButton.text = ResolveTutorialCloseButtonText(entry);
        ConfigureTutorialDemo(entry);
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
        else if (string.Equals(entry.tutorialType, TutorialTypeShortCircuit, StringComparison.OrdinalIgnoreCase))
        {
            tutorialAnimationVersion++;
            tutorialAnimationRoutine = StartCoroutine(PlayShortCircuitTutorialAnimationLoop(tutorialAnimationVersion));
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
            ApplyTutorialStepState(1, 2, 1, T("tutorial_step_start"));
            SetTutorialHandPosition(0, instant: true);
            yield return new WaitForSecondsRealtime(0.42f);

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            SetTutorialHandPosition(1, instant: false);
            ApplyTutorialStepState(0, 2, 1, T("tutorial_step_left"), pulseLeft: true);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            SetTutorialHandPosition(2, instant: false);
            ApplyTutorialStepState(0, 1, 1, T("tutorial_step_center"), pulseCenter: true);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            SetTutorialHandPosition(1, instant: false);
            ApplyTutorialStepState(0, 1, 0, T("tutorial_step_right"), pulseRight: true);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyTutorialStepState(0, 0, 0, T("tutorial_step_clear"), pulseCenter: true);
            yield return cycleWait;
        }

        tutorialAnimationRoutine = null;
    }

    private IEnumerator PlayShortCircuitTutorialAnimationLoop(int animationVersion)
    {
        WaitForSecondsRealtime introWait = new WaitForSecondsRealtime(0.5f);
        WaitForSecondsRealtime stepWait = new WaitForSecondsRealtime(0.62f);
        WaitForSecondsRealtime cycleWait = new WaitForSecondsRealtime(0.86f);

        while (isTutorialPopupOpen && animationVersion == tutorialAnimationVersion)
        {
            ApplyShortCircuitTutorialStepState(
                1, 1, 1, 1,
                T("tutorial_short_circuit_hint_intro"),
                pulseBottomLeft: true,
                pulseArrow: true);
            SetShortCircuitTutorialHandPosition(0, instant: true);
            yield return introWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            SetShortCircuitTutorialHandPosition(1, instant: false);
            ApplyShortCircuitTutorialStepState(
                1, 1, 0, 1,
                T("tutorial_short_circuit_step_exit"),
                pulseBottomLeft: true,
                pulseBottomRight: true,
                pulseArrow: true);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            SetShortCircuitTutorialHandPosition(2, instant: false);
            ApplyShortCircuitTutorialStepState(
                1, 1, 0, 0,
                T("tutorial_short_circuit_step_follow"),
                pulseTopRight: true);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyShortCircuitTutorialStepState(
                1, 1, 0, 0,
                T("tutorial_short_circuit_step_remember"),
                pulseTopRight: true,
                pulseArrow: true);
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

    private void ApplyShortCircuitTutorialStepState(
        int topLeftCount,
        int topRightCount,
        int bottomLeftCount,
        int bottomRightCount,
        string hint,
        bool pulseTopLeft = false,
        bool pulseTopRight = false,
        bool pulseBottomLeft = false,
        bool pulseBottomRight = false,
        bool pulseArrow = false)
    {
        ApplyTutorialTileState(tutorialShortTileTopLeft, tutorialShortTileTopLeftCount, topLeftCount, pulseTopLeft);
        ApplyTutorialTileState(tutorialShortTileTopRight, tutorialShortTileTopRightCount, topRightCount, pulseTopRight);
        ApplyShortCircuitTileState(tutorialShortTileBottomLeft, tutorialShortTileBottomLeftCount, bottomLeftCount, pulseBottomLeft, pulseArrow);
        ApplyTutorialTileState(tutorialShortTileBottomRight, tutorialShortTileBottomRightCount, bottomRightCount, pulseBottomRight);

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

    private void ApplyShortCircuitTileState(VisualElement tile, Label countLabel, int count, bool pulseTile, bool pulseArrow)
    {
        ApplyTutorialTileState(tile, countLabel, count, pulseTile);

        if (tile != null)
        {
            bool active = count > 0;
            tile.style.backgroundColor = active
                ? new StyleColor(new Color(0.99f, 0.68f, 0.24f, 0.28f))
                : new StyleColor(new Color(0.18f, 0.14f, 0.08f, 0.52f));

            Color borderColor = active
                ? new Color(1f, 0.89f, 0.63f, 0.96f)
                : new Color(0.69f, 0.58f, 0.42f, 0.62f);
            tile.style.borderLeftColor = borderColor;
            tile.style.borderRightColor = borderColor;
            tile.style.borderTopColor = borderColor;
            tile.style.borderBottomColor = borderColor;
        }

        if (tutorialShortTileBottomLeftArrow == null)
            return;

        bool showArrow = count > 0;
        tutorialShortTileBottomLeftArrow.style.opacity = showArrow ? 1f : 0.42f;
        tutorialShortTileBottomLeftArrow.style.scale = new StyleScale(
            new Scale(pulseArrow && showArrow ? new Vector3(1.18f, 1.18f, 1f) : Vector3.one));
        tutorialShortTileBottomLeftArrow.style.color = new StyleColor(
            showArrow ? new Color(1f, 0.96f, 0.84f, 0.99f) : new Color(0.77f, 0.67f, 0.55f, 0.62f));

        if (pulseArrow && showArrow)
        {
            tutorialShortTileBottomLeftArrow.schedule.Execute(() =>
            {
                tutorialShortTileBottomLeftArrow.style.scale = new StyleScale(new Scale(Vector3.one));
            }).StartingIn(140);
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

    private void SetShortCircuitTutorialHandPosition(int pointIndex, bool instant)
    {
        if (tutorialShortCircuitHandImage == null)
            return;

        float leftPercent;
        float top;
        switch (pointIndex)
        {
            case 0:
                leftPercent = 26f;
                top = 206f;
                break;
            case 1:
                leftPercent = 56f;
                top = 206f;
                break;
            case 2:
                leftPercent = 56f;
                top = 48f;
                break;
            case 3:
                leftPercent = 26f;
                top = 48f;
                break;
            default:
                leftPercent = 26f;
                top = 206f;
                break;
        }

        tutorialShortCircuitHandImage.style.left = Length.Percent(leftPercent);
        tutorialShortCircuitHandImage.style.top = top;
        tutorialShortCircuitHandImage.style.opacity = 1f;
        tutorialShortCircuitHandImage.style.scale = new StyleScale(new Scale(instant ? Vector3.one : new Vector3(1.04f, 1.04f, 1f)));
    }

    private void ConfigureTutorialDemo(HelpTutorialEntryData entry)
    {
        bool isShortCircuit = entry != null &&
            string.Equals(entry.tutorialType, TutorialTypeShortCircuit, StringComparison.OrdinalIgnoreCase);

        if (tutorialBasicDemoBoard != null)
            tutorialBasicDemoBoard.style.display = isShortCircuit ? DisplayStyle.None : DisplayStyle.Flex;
        if (tutorialShortCircuitDemoBoard != null)
            tutorialShortCircuitDemoBoard.style.display = isShortCircuit ? DisplayStyle.Flex : DisplayStyle.None;

        if (tutorialHandImage != null && !isShortCircuit)
        {
            ApplyTutorialStepState(1, 2, 1, T("tutorial_hint_connect"));
            SetTutorialHandPosition(0, instant: true);
        }

        if (tutorialShortCircuitHandImage != null)
            tutorialShortCircuitHandImage.style.opacity = isShortCircuit ? 1f : 0f;
        if (tutorialHandImage != null && isShortCircuit)
            tutorialHandImage.style.opacity = 0f;

        if (!isShortCircuit)
            return;

        ApplyShortCircuitTutorialStepState(
            1, 1, 1, 1,
            T("tutorial_short_circuit_hint_intro"),
            pulseBottomLeft: true,
            pulseArrow: true);
        SetShortCircuitTutorialHandPosition(0, instant: true);
    }

    private string T(string key)
    {
        return GameLocalization.Get(key, activeLanguageCode);
    }

    private string T(string key, params (string key, string value)[] replacements)
    {
        return GameLocalization.Get(key, activeLanguageCode, replacements);
    }

    private void ApplyLocalizationForCurrentLanguage()
    {
        if (stageTitleLabel != null)
            stageTitleLabel.text = T("stage_title");

        if (settingTitleTextLabel != null)
            settingTitleTextLabel.text = T("settings_title");
        if (gameSettingSectionLabel != null)
            gameSettingSectionLabel.text = T("section_game_settings");
        if (helpLanguageSectionLabel != null)
            helpLanguageSectionLabel.text = T("section_help_language");
        if (recommendServiceSectionLabel != null)
            recommendServiceSectionLabel.text = T("section_recommend_service");
        if (dataPolicySectionLabel != null)
            dataPolicySectionLabel.text = T("section_data_policy");
        if (soundRowLabel != null)
            soundRowLabel.text = T("label_sound");
        if (vibrationRowLabel != null)
            vibrationRowLabel.text = T("label_vibration");
        if (helpMenuLabel != null)
            helpMenuLabel.text = T("menu_help");

        if (rateButton != null)
            rateButton.text = T("button_rate");
        if (removeAdsButton != null)
            removeAdsButton.text = T("button_remove_ads");
        if (emailButton != null)
            emailButton.text = T("button_email");
        if (resetDataButton != null)
            resetDataButton.text = T("button_reset_data");
        if (privacyPolicyButton != null)
            privacyPolicyButton.text = T("button_privacy_policy");
        if (termsButton != null)
            termsButton.text = T("button_terms");

        if (resetConfirmTitleLabel != null)
            resetConfirmTitleLabel.text = T("reset_confirm_title");
        if (resetConfirmMessageLabel != null)
            resetConfirmMessageLabel.text = T("reset_confirm_message");
        if (resetConfirmCancelButton != null)
            resetConfirmCancelButton.text = T("button_cancel");
        if (resetConfirmOkButton != null)
            resetConfirmOkButton.text = T("button_reset");
        if (languageSelectTitleLabel != null)
            languageSelectTitleLabel.text = T("language_select_title");

        UpdateLanguageMenuLabel();
        if (isLanguageSelectionPopupOpen)
            BuildLanguageSelectionListUI();
        UpdateActiveTutorialCopyIfOpen();

        if (currentHeartRefillMode == HeartRefillMode.SessionPlayReward)
            ConfigureHeartDepletedPopupForSessionReward(currentSessionPlayRewardMinutes);
        else
            ConfigureHeartDepletedPopupForRewardedAd();

        UpdateHeartRefillButtonState();
    }

    private void UpdateLanguageMenuLabel()
    {
        if (languageMenuLabel == null)
            return;

        string languageDisplayName = BuildLanguageOptionDisplayName(selectedLanguageCode);

        languageMenuLabel.text = T("menu_language", ("language", languageDisplayName));
    }

    private void OpenLanguageSelectionPopup()
    {
        if (languageSelectOverlay == null)
        {
            string nextSelection = GameLocalization.GetNextSelectionCode(selectedLanguageCode);
            ApplyLanguageSelection(nextSelection, persist: true);
            return;
        }

        if (languageSelectTitleLabel != null)
            languageSelectTitleLabel.text = T("language_select_title");

        BuildLanguageSelectionListUI();
        languageSelectOverlay.style.display = DisplayStyle.Flex;
        isLanguageSelectionPopupOpen = true;

        if (languageSelectDialog != null)
        {
            languageSelectDialog.style.opacity = 0f;
            languageSelectDialog.style.scale = new StyleScale(new Scale(new Vector3(0.95f, 0.95f, 1f)));
            languageSelectDialog.schedule.Execute(() =>
            {
                if (!isLanguageSelectionPopupOpen)
                    return;
                languageSelectDialog.style.opacity = 1f;
                languageSelectDialog.style.scale = new StyleScale(new Scale(Vector3.one));
            }).StartingIn(12);
        }

        FirebaseBootstrap.LogEvent("language_popup_open", new Dictionary<string, object>
        {
            { "selection", selectedLanguageCode },
            { "active", activeLanguageCode }
        });
    }

    private void HideLanguageSelectionPopup()
    {
        isLanguageSelectionPopupOpen = false;
        if (languageSelectOverlay != null)
            languageSelectOverlay.style.display = DisplayStyle.None;
        if (languageSelectDialog != null)
        {
            languageSelectDialog.style.opacity = 1f;
            languageSelectDialog.style.scale = new StyleScale(new Scale(Vector3.one));
        }
    }

    private void BuildLanguageSelectionListUI()
    {
        if (languageOptionList == null)
            return;

        languageOptionList.Clear();
        string[] languageSelections = GameLocalization.GetSelectionOrder();
        for (int i = 0; i < languageSelections.Length; i++)
        {
            string selectionCode = languageSelections[i];
            string optionLabel = BuildLanguageOptionDisplayName(selectionCode);
            bool isSelectedOption = string.Equals(selectedLanguageCode, selectionCode, StringComparison.OrdinalIgnoreCase);
            if (isSelectedOption)
                optionLabel = "✓ " + optionLabel;

            Button optionButton = new Button
            {
                text = optionLabel
            };
            optionButton.AddToClassList("language-option-button");
            if (isSelectedOption)
                optionButton.AddToClassList("language-option-selected");

            string capturedSelectionCode = selectionCode;
            optionButton.clicked += () => OnLanguageSelectionOptionClicked(capturedSelectionCode);
            RegisterButtonClickAnimation(optionButton);

            languageOptionList.Add(optionButton);
        }
    }

    private void OnLanguageSelectionOptionClicked(string selectionCode)
    {
        ApplyLanguageSelection(selectionCode, persist: true);
        HideLanguageSelectionPopup();

        FirebaseBootstrap.LogEvent("language_changed", new Dictionary<string, object>
        {
            { "selection", selectedLanguageCode },
            { "active", activeLanguageCode }
        });
    }

    private string BuildLanguageOptionDisplayName(string selectionCode)
    {
        string normalizedSelection = GameLocalization.NormalizeSelectionCode(selectionCode);
        if (string.Equals(normalizedSelection, GameLocalization.LanguageAuto, StringComparison.OrdinalIgnoreCase))
        {
            string resolvedLanguageCode = GameLocalization.ResolveSystemLanguageCode();
            string resolvedLanguageName = GameLocalization.GetLanguageDisplayName(resolvedLanguageCode);
            return $"{T("language_system")} ({resolvedLanguageName})";
        }

        return GameLocalization.GetLanguageDisplayName(normalizedSelection);
    }

    private void ApplyLanguageSelection(string selectionCode, bool persist)
    {
        selectedLanguageCode = GameLocalization.NormalizeSelectionCode(selectionCode);
        activeLanguageCode = GameLocalization.ResolveActiveLanguageCode(selectedLanguageCode);

        if (persist)
            SaveSettingString(SaveKeyLanguageSelection, selectedLanguageCode);

        StopStageSnackbarPlayback();
        ApplyLocalizationForCurrentLanguage();
    }

    private void UpdateActiveTutorialCopyIfOpen()
    {
        if (!isTutorialPopupOpen || activeTutorialEntry == null)
            return;

        if (tutorialTitleLabel != null)
            tutorialTitleLabel.text = ResolveTutorialTitle(activeTutorialEntry);
        if (tutorialDescriptionLabel != null)
            tutorialDescriptionLabel.text = ResolveTutorialDescription(activeTutorialEntry);
        if (tutorialConfirmButton != null)
            tutorialConfirmButton.text = ResolveTutorialCloseButtonText(activeTutorialEntry);
    }

    private string ResolveTutorialTitle(HelpTutorialEntryData entry)
    {
        if (entry == null)
            return T("help_generic_title");
        if (!string.IsNullOrWhiteSpace(entry.titleKey))
            return T(entry.titleKey);
        if (!string.IsNullOrWhiteSpace(entry.title))
            return entry.title;
        return T("help_generic_title");
    }

    private string ResolveTutorialDescription(HelpTutorialEntryData entry)
    {
        if (entry == null)
            return T("help_generic_description");
        if (!string.IsNullOrWhiteSpace(entry.descriptionKey))
            return T(entry.descriptionKey);
        if (!string.IsNullOrWhiteSpace(entry.description))
            return entry.description;
        return T("help_generic_description");
    }

    private string ResolveTutorialCloseButtonText(HelpTutorialEntryData entry)
    {
        if (entry == null)
            return T("help_close_button");
        if (!string.IsNullOrWhiteSpace(entry.closeButtonTextKey))
            return T(entry.closeButtonTextKey);
        if (!string.IsNullOrWhiteSpace(entry.closeButtonText))
            return entry.closeButtonText;
        return T("help_close_button");
    }

    private string ResolveStageSnackbarTemplate(StageSnackbarEntryData entry)
    {
        if (entry == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(entry.messageKey))
            return T(entry.messageKey);
        if (!string.IsNullOrWhiteSpace(entry.message))
            return entry.message;
        return T("snackbar_default_new_tile_unlock");
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
            if (ES3.KeyExists(SaveKeyLanguageSelection)) ES3.DeleteKey(SaveKeyLanguageSelection);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameMainUIController] 설정 초기화(ES3) 실패: {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, "ConfirmResetData ES3 delete failed");
        }

        PlayerPrefs.DeleteKey(SaveKeySoundOn);
        PlayerPrefs.DeleteKey(SaveKeyVibrationOn);
        PlayerPrefs.DeleteKey(SaveKeyLanguageSelection);
        PlayerPrefs.Save();

        GameManager.ClearSavedStageProgress();

        isSoundOn = true;
        isVibrationOn = true;
        IsVibrationEnabled = true;
        selectedLanguageCode = GameLocalization.LanguageAuto;
        activeLanguageCode = GameLocalization.ResolveSystemLanguageCode();
        RefreshSoundSwitchVisual();
        RefreshVibrationSwitchVisual();
        ApplySoundSwitchToAudioListener();
        ApplyLocalizationForCurrentLanguage();

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
        selectedLanguageCode = GameLocalization.NormalizeSelectionCode(
            LoadSettingString(SaveKeyLanguageSelection, GameLocalization.LanguageAuto));
        activeLanguageCode = GameLocalization.ResolveActiveLanguageCode(selectedLanguageCode);
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

    private static string LoadSettingString(string key, string defaultValue)
    {
        try
        {
            if (ES3.KeyExists(key))
                return ES3.Load<string>(key);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameMainUIController] 문자열 설정 로드 실패({key}): {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, $"LoadSettingString failed: {key}");
        }

        return PlayerPrefs.GetString(key, defaultValue);
    }

    private static void SaveSettingString(string key, string value)
    {
        string safeValue = value ?? string.Empty;
        try
        {
            ES3.Save(key, safeValue);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameMainUIController] 문자열 설정 저장 실패({key}): {e.Message}");
            FirebaseBootstrap.LogNonFatalException(e, $"SaveSettingString failed: {key}");
        }

        PlayerPrefs.SetString(key, safeValue);
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
        SetBannerPlaceholderText(isDebugBuildCached ? T("banner_loading_test") : T("banner_loading"));
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        MobileAds.Initialize(_ =>
        {
            pendingBannerLoadFromInitialize = true;
            pendingRewardedAdLoadFromInitialize = true;
            pendingStageSkipRewardedAdLoadFromInitialize = true;
            pendingInterstitialLoadFromInitialize = true;
        });
#else
        splashBannerReady = true;
        splashHeartRewardedReady = true;
        splashStageSkipRewardedReady = true;
        splashInterstitialReady = true;
        SetBannerPlaceholderText(T("banner_default"));
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

    private string ResolveStageSkipRewardedAdUnitId()
    {
        bool useTestAdUnit = isDebugBuildCached;
#if UNITY_ANDROID
        return useTestAdUnit ? AndroidTestStageSkipRewardedAdUnitId : AndroidReleaseStageSkipRewardedAdUnitId;
#elif UNITY_IOS
        return useTestAdUnit ? IOSTestStageSkipRewardedAdUnitId : IOSReleaseStageSkipRewardedAdUnitId;
#else
        return string.Empty;
#endif
    }

    private string ResolveStageTransitionInterstitialAdUnitId()
    {
        bool useTestAdUnit = isDebugBuildCached;
#if UNITY_ANDROID
        return useTestAdUnit ? AndroidTestInterstitialAdUnitId : AndroidReleaseInterstitialAdUnitId;
#elif UNITY_IOS
        return useTestAdUnit ? IOSTestInterstitialAdUnitId : IOSReleaseInterstitialAdUnitId;
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
            splashHeartRewardedReady = true;
            UpdateHeartRefillButtonState();
            return;
        }

        string adUnitId = ResolveRewardedAdUnitId();
        if (string.IsNullOrEmpty(adUnitId))
        {
            splashHeartRewardedReady = true;
            return;
        }

        isRewardedAdLoading = true;
        RewardedAd.Load(adUnitId, new AdRequest(), (RewardedAd ad, LoadAdError loadError) =>
        {
            isRewardedAdLoading = false;
            if (loadError != null || ad == null)
            {
                splashHeartRewardedReady = true;
                string errorMessage = loadError != null ? loadError.GetMessage() : "unknown";
                Debug.LogWarning($"[GameMainUIController] Rewarded ad load failed: {errorMessage}");
                SetHeartRefillStatus(T("heart_status_load_failed"));
                UpdateHeartRefillButtonState();
                return;
            }

            splashHeartRewardedReady = true;
            DestroyRewardedAd();
            rewardedAd = ad;
            rewardEarnedThisShow = false;

            rewardedAd.OnAdFullScreenContentClosed += HandleRewardedAdFullScreenClosed;
            rewardedAd.OnAdFullScreenContentFailed += HandleRewardedAdFullScreenFailed;

            Debug.Log($"[GameMainUIController] Rewarded ad loaded. unitId={adUnitId}, testMode={(isDebugBuildCached ? 1 : 0)}");
            UpdateHeartRefillButtonState();
        });
    }

    private void LoadStageSkipRewardedAd()
    {
        if (isStageSkipRewardedAdLoading)
            return;

        if (stageSkipRewardedAd != null && stageSkipRewardedAd.CanShowAd())
        {
            splashStageSkipRewardedReady = true;
            return;
        }

        string adUnitId = ResolveStageSkipRewardedAdUnitId();
        if (string.IsNullOrEmpty(adUnitId))
        {
            splashStageSkipRewardedReady = true;
            return;
        }

        isStageSkipRewardedAdLoading = true;
        RewardedAd.Load(adUnitId, new AdRequest(), (RewardedAd ad, LoadAdError loadError) =>
        {
            isStageSkipRewardedAdLoading = false;
            if (loadError != null || ad == null)
            {
                splashStageSkipRewardedReady = true;
                string errorMessage = loadError != null ? loadError.GetMessage() : "unknown";
                Debug.LogWarning($"[GameMainUIController] Stage skip rewarded ad load failed: {errorMessage}");
                return;
            }

            splashStageSkipRewardedReady = true;
            DestroyStageSkipRewardedAd();
            stageSkipRewardedAd = ad;
            stageSkipRewardEarnedThisShow = false;
            stageSkipRewardedAd.OnAdFullScreenContentClosed += HandleStageSkipRewardedAdFullScreenClosed;
            stageSkipRewardedAd.OnAdFullScreenContentFailed += HandleStageSkipRewardedAdFullScreenFailed;

            Debug.Log($"[GameMainUIController] Stage skip rewarded ad loaded. unitId={adUnitId}, testMode={(isDebugBuildCached ? 1 : 0)}");
        });
    }

    private void LoadStageTransitionInterstitialAd()
    {
        if (isStageTransitionInterstitialAdLoading)
            return;

        if (stageTransitionInterstitialAd != null && stageTransitionInterstitialAd.CanShowAd())
        {
            splashInterstitialReady = true;
            return;
        }

        string adUnitId = ResolveStageTransitionInterstitialAdUnitId();
        if (string.IsNullOrEmpty(adUnitId))
        {
            splashInterstitialReady = true;
            return;
        }

        isStageTransitionInterstitialAdLoading = true;
        InterstitialAd.Load(adUnitId, new AdRequest(), (InterstitialAd ad, LoadAdError loadError) =>
        {
            isStageTransitionInterstitialAdLoading = false;
            if (loadError != null || ad == null)
            {
                splashInterstitialReady = true;
                string errorMessage = loadError != null ? loadError.GetMessage() : "unknown";
                Debug.LogWarning($"[GameMainUIController] Stage transition interstitial load failed: {errorMessage}");
                return;
            }

            splashInterstitialReady = true;
            DestroyStageTransitionInterstitialAd();
            stageTransitionInterstitialAd = ad;
            stageTransitionInterstitialAd.OnAdFullScreenContentClosed += HandleStageTransitionInterstitialClosed;
            stageTransitionInterstitialAd.OnAdFullScreenContentFailed += HandleStageTransitionInterstitialFailed;

            Debug.Log($"[GameMainUIController] Stage transition interstitial loaded. unitId={adUnitId}, testMode={(isDebugBuildCached ? 1 : 0)}");
        });
    }

    private void ShowRewardedAdInternal()
    {
        if (rewardedAd == null || !rewardedAd.CanShowAd())
        {
            SetHeartRefillStatus(T("heart_status_prepare_retry"));
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

    private void TryShowStageSkipRewardedAd(Action onRewardEarned)
    {
        if (stageSkipRewardedAd == null || !stageSkipRewardedAd.CanShowAd())
        {
            FirebaseBootstrap.LogEvent("stage_skip_reward_ad_not_ready");
            LoadStageSkipRewardedAd();
            return;
        }

        pendingStageSkipRewardCompletionAction = onRewardEarned;
        stageSkipRewardEarnedThisShow = false;
        FirebaseBootstrap.LogEvent("stage_skip_reward_ad_show");
        stageSkipRewardedAd.Show(_ =>
        {
            stageSkipRewardEarnedThisShow = true;
            FirebaseBootstrap.LogEvent("stage_skip_reward_earned", new Dictionary<string, object>
            {
                { "reward_name", StageSkipRewardName },
                { "reward_amount", StageSkipRewardAmount }
            });
        });
    }

    private bool TryShowStageTransitionInterstitial(Action onCompleted)
    {
        if (onCompleted == null)
            return false;

        if (isStageTransitionInterstitialShowing)
            return true;

        if (stageTransitionInterstitialAd == null || !stageTransitionInterstitialAd.CanShowAd())
        {
            LoadStageTransitionInterstitialAd();
            return false;
        }

        pendingStageTransitionInterstitialCompletionAction = onCompleted;
        isStageTransitionInterstitialShowing = true;
        FirebaseBootstrap.LogEvent("stage_transition_interstitial_show", new Dictionary<string, object>
        {
            { "stage_index", currentStageIndexForUI },
            { "interval", StageTransitionInterstitialInterval }
        });
        stageTransitionInterstitialAd.Show();
        return true;
    }

    private void HandleRewardedAdFullScreenClosed()
    {
        if (isWaitingForHeartRefill && !rewardEarnedThisShow)
            SetHeartRefillStatus(T("heart_status_no_reward"));

        DestroyRewardedAd();
        LoadRewardedAd();
        UpdateHeartRefillButtonState();
    }

    private void HandleRewardedAdFullScreenFailed(AdError adError)
    {
        string errorMessage = adError != null ? adError.GetMessage() : "unknown";
        Debug.LogWarning($"[GameMainUIController] Rewarded ad failed to show: {errorMessage}");
        SetHeartRefillStatus(T("heart_status_show_failed"));
        DestroyRewardedAd();
        LoadRewardedAd();
        UpdateHeartRefillButtonState();
    }

    private void HandleStageSkipRewardedAdFullScreenClosed()
    {
        bool shouldSkip = stageSkipRewardEarnedThisShow;
        Action completion = shouldSkip ? pendingStageSkipRewardCompletionAction : null;
        pendingStageSkipRewardCompletionAction = null;

        if (!shouldSkip)
            FirebaseBootstrap.LogEvent("stage_skip_reward_not_earned");

        DestroyStageSkipRewardedAd();
        LoadStageSkipRewardedAd();

        if (shouldSkip)
            completion?.Invoke();
    }

    private void HandleStageSkipRewardedAdFullScreenFailed(AdError adError)
    {
        string errorMessage = adError != null ? adError.GetMessage() : "unknown";
        Debug.LogWarning($"[GameMainUIController] Stage skip rewarded ad failed to show: {errorMessage}");
        FirebaseBootstrap.LogEvent("stage_skip_reward_show_failed");
        pendingStageSkipRewardCompletionAction = null;
        DestroyStageSkipRewardedAd();
        LoadStageSkipRewardedAd();
    }

    private void HandleStageTransitionInterstitialClosed()
    {
        CompleteStageTransitionInterstitialFlow("closed");
    }

    private void HandleStageTransitionInterstitialFailed(AdError adError)
    {
        string errorMessage = adError != null ? adError.GetMessage() : "unknown";
        Debug.LogWarning($"[GameMainUIController] Stage transition interstitial failed to show: {errorMessage}");
        CompleteStageTransitionInterstitialFlow("failed");
    }

    private void CompleteStageTransitionInterstitialFlow(string resultType)
    {
        Action completion = pendingStageTransitionInterstitialCompletionAction;
        pendingStageTransitionInterstitialCompletionAction = null;
        isStageTransitionInterstitialShowing = false;

        DestroyStageTransitionInterstitialAd();
        LoadStageTransitionInterstitialAd();

        FirebaseBootstrap.LogEvent("stage_transition_interstitial_complete", new Dictionary<string, object>
        {
            { "stage_index", currentStageIndexForUI },
            { "result", resultType }
        });
        completion?.Invoke();
    }

    private void LoadBannerAd()
    {
        DestroyBannerAd();

        string adUnitId = ResolveBannerAdUnitId();
        if (string.IsNullOrEmpty(adUnitId))
        {
            splashBannerReady = true;
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
        if (rewardedAd == null)
            return;

        rewardedAd.OnAdFullScreenContentClosed -= HandleRewardedAdFullScreenClosed;
        rewardedAd.OnAdFullScreenContentFailed -= HandleRewardedAdFullScreenFailed;
        rewardedAd.Destroy();
        rewardedAd = null;
    }

    private void DestroyStageSkipRewardedAd()
    {
        if (stageSkipRewardedAd == null)
            return;

        stageSkipRewardedAd.OnAdFullScreenContentClosed -= HandleStageSkipRewardedAdFullScreenClosed;
        stageSkipRewardedAd.OnAdFullScreenContentFailed -= HandleStageSkipRewardedAdFullScreenFailed;
        stageSkipRewardedAd.Destroy();
        stageSkipRewardedAd = null;
    }

    private void DestroyStageTransitionInterstitialAd()
    {
        if (stageTransitionInterstitialAd == null)
            return;

        stageTransitionInterstitialAd.OnAdFullScreenContentClosed -= HandleStageTransitionInterstitialClosed;
        stageTransitionInterstitialAd.OnAdFullScreenContentFailed -= HandleStageTransitionInterstitialFailed;
        stageTransitionInterstitialAd.Destroy();
        stageTransitionInterstitialAd = null;
        isStageTransitionInterstitialShowing = false;
    }

    private void HandleBannerAdLoaded()
    {
        splashBannerReady = true;
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
        splashBannerReady = true;
        string errorMessage = loadError != null ? loadError.GetMessage() : "unknown";
        Debug.LogWarning($"[GameMainUIController] Banner ad load failed: {errorMessage}");
        reservedBannerHeightPx = 0f;
        RefreshBottomLayout(force: true);
        SetBannerPlaceholderText(T("banner_load_failed"));
    }
#endif

#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)
    private void DestroyRewardedAd()
    {
    }

    private void DestroyStageSkipRewardedAd()
    {
    }

    private void DestroyStageTransitionInterstitialAd()
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
            stageTitleLabel.text = T("stage_title");

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
