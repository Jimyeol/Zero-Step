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
    private const float TopHudBasePaddingPx = 64f;
    private const float TopHudBackdropBaseTopPx = 26f;
    private const string TutorialScheduleResourcePath = "Tutorials/help_tutorial_schedule";
    private const string StageSnackbarScheduleResourcePath = "Tutorials/stage_snackbar_schedule";
    private const string TutorialDismissedKeyPrefix = "TutorialDismissed_";
    private const string TutorialTypeBasicPath = "BasicPath";
    private const string TutorialTypeShortCircuit = "ShortCircuit";
    private const string TutorialTypeCrossBlast = "CrossBlast";
    private const string TutorialTypeFixedKnot = "FixedKnot";
    private const string TutorialTypeTwinLink = "TwinLink";
    private const string TutorialTypeIgniter = "Igniter";
    private const string TutorialTypeBlindCurtain = "BlindCurtain";
    private const string TutorialTypeBlackout = "Blackout";
    private const string TutorialTypeBlackOutAlias = "BlackOut";
    private const string ShortCircuitTutorialDemoDirection = "Right";
    private const float TutorialHandFallbackSize = 100f;
    private const float TutorialTrailHeight = 14f;
    private const float TutorialBlockedMarkerSize = 58f;
    private const int TutorialSpecialCellCount = 9;
    private const float TutorialSpecialLineThickness = 14f;
    private const float TutorialSpecialMarkerSize = 58f;
    private const float TutorialSpecialPulseSize = 72f;
    private const string CrossBlastSpriteResourcePath = "Sprites/corss_blast_tile";
    private const string FixedKnotSpriteResourcePath = "Sprites/fixed_knot_tile";
    private const string IgniterSpriteResourcePath = "Sprites/igniter_tile";
    private const string BlindCurtainSpriteResourcePath = "Sprites/blind_curtain_tile";
    private const string DefaultUIButtonSfxResourcePath = "Sounds/ui_button";
    private const string DefaultSplashSpriteResourcePath = "Sprites/splash";
    private const string DefaultSplashVideoResourcePath = "Sprites/splash_video";

    private static readonly Color[] TutorialNumberPalette =
    {
        new Color32(0x70, 0xDF, 0xF8, 0xFF),
        new Color32(0x54, 0xF5, 0xA4, 0xFF),
        new Color32(0xFA, 0xA1, 0x66, 0xFF),
        new Color32(0xF5, 0x5F, 0xD5, 0xFF),
        new Color32(0xA7, 0x7C, 0xFF, 0xFF),
        new Color32(0xF7, 0xE7, 0x5C, 0xFF),
        new Color32(0xFF, 0x5D, 0x73, 0xFF),
        new Color32(0xB6, 0xF8, 0x5C, 0xFF),
        new Color32(0x5B, 0x8C, 0xFF, 0xFF),
        new Color32(0xF4, 0xF0, 0xFF, 0xFF)
    };

    private static readonly string[] TutorialSpecialCellThemeClasses =
    {
        "tutorial-special-cell-cross",
        "tutorial-special-cell-fixed",
        "tutorial-special-cell-twin",
        "tutorial-special-cell-igniter",
        "tutorial-special-cell-blind",
        "tutorial-special-cell-blackout",
        "tutorial-special-cell-hidden"
    };

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
        public string instructionTextKey;
        public string instructionText;
        public string closeButtonTextKey;
        public string closeButtonText = "확인";
    }

    private class SpecialTutorialPreset
    {
        public readonly string tutorialType;
        public readonly string themeClass;
        public readonly string spritePath;
        public readonly string initialHintKey;
        public readonly int focusCell;

        public SpecialTutorialPreset(string tutorialType, string themeClass, string spritePath, string initialHintKey, int focusCell)
        {
            this.tutorialType = tutorialType;
            this.themeClass = themeClass;
            this.spritePath = spritePath;
            this.initialHintKey = initialHintKey;
            this.focusCell = focusCell;
        }
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
    private VisualElement topBar;
    private VisualElement topHudBackdrop;

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
    private VisualElement tutorialBasicTrailLeftCenter;
    private VisualElement tutorialBasicTrailCenterRight;
    private VisualElement tutorialShortCircuitDemoBoard;
    private VisualElement tutorialShortCircuitExitTrail;
    private VisualElement tutorialShortCircuitBlockedEntry;
    private VisualElement tutorialSpecialDemoBoard;
    private VisualElement tutorialSpecialTrailA;
    private VisualElement tutorialSpecialTrailB;
    private VisualElement tutorialSpecialTrailC;
    private VisualElement tutorialSpecialBeamHorizontal;
    private VisualElement tutorialSpecialBeamVertical;
    private VisualElement tutorialSpecialPairLine;
    private VisualElement tutorialSpecialPulse;
    private VisualElement tutorialSpecialRevealPulseA;
    private VisualElement tutorialSpecialRevealPulseB;
    private VisualElement tutorialSpecialBlockedMarker;
    private Label tutorialTitleLabel;
    private Label tutorialDescriptionLabel;
    private Label tutorialStepHintLabel;
    private Button tutorialCloseButton;
    private Image tutorialCloseIcon;
    private Button tutorialConfirmButton;
    private Button tutorialPreviousButton;
    private Label tutorialPreviousButtonLabel;
    private Button tutorialNextButton;
    private Label tutorialNextButtonLabel;
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
    private Label tutorialShortCircuitBlockedEntryLabel;
    private Label tutorialSpecialBlockedLabel;
    private Image tutorialHandImage;
    private Image tutorialShortCircuitTileImage;
    private Image tutorialShortCircuitHandImage;
    private Image tutorialSpecialHandImage;
    private readonly VisualElement[] tutorialSpecialCells = new VisualElement[TutorialSpecialCellCount];
    private readonly Image[] tutorialSpecialSprites = new Image[TutorialSpecialCellCount];
    private readonly Label[] tutorialSpecialCounts = new Label[TutorialSpecialCellCount];
    private readonly Label[] tutorialSpecialBadges = new Label[TutorialSpecialCellCount];
    private readonly Dictionary<string, Sprite> tutorialSpriteCache = new Dictionary<string, Sprite>();
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
    private bool activeTutorialOpenedFromSettings;
    private bool hasStaticTutorialInstructionText;
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

        topBar = root.Q<VisualElement>("TopBar");
        topHudBackdrop = root.Q<VisualElement>("TopHudBackdrop");
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
        tutorialBasicTrailLeftCenter = root.Q<VisualElement>("TutorialBasicTrailLeftCenter");
        tutorialBasicTrailCenterRight = root.Q<VisualElement>("TutorialBasicTrailCenterRight");
        tutorialShortCircuitDemoBoard = root.Q<VisualElement>("TutorialShortCircuitDemoBoard");
        tutorialShortCircuitExitTrail = root.Q<VisualElement>("TutorialShortCircuitExitTrail");
        tutorialShortCircuitBlockedEntry = root.Q<VisualElement>("TutorialShortCircuitBlockedEntry");
        tutorialSpecialDemoBoard = root.Q<VisualElement>("TutorialSpecialDemoBoard");
        tutorialSpecialTrailA = root.Q<VisualElement>("TutorialSpecialTrailA");
        tutorialSpecialTrailB = root.Q<VisualElement>("TutorialSpecialTrailB");
        tutorialSpecialTrailC = root.Q<VisualElement>("TutorialSpecialTrailC");
        tutorialSpecialBeamHorizontal = root.Q<VisualElement>("TutorialSpecialBeamHorizontal");
        tutorialSpecialBeamVertical = root.Q<VisualElement>("TutorialSpecialBeamVertical");
        tutorialSpecialPairLine = root.Q<VisualElement>("TutorialSpecialPairLine");
        tutorialSpecialPulse = root.Q<VisualElement>("TutorialSpecialPulse");
        tutorialSpecialRevealPulseA = root.Q<VisualElement>("TutorialSpecialRevealPulseA");
        tutorialSpecialRevealPulseB = root.Q<VisualElement>("TutorialSpecialRevealPulseB");
        tutorialSpecialBlockedMarker = root.Q<VisualElement>("TutorialSpecialBlockedMarker");
        tutorialTitleLabel = root.Q<Label>("TutorialTitleLabel");
        tutorialDescriptionLabel = root.Q<Label>("TutorialDescriptionLabel");
        tutorialStepHintLabel = root.Q<Label>("TutorialStepHintLabel");
        tutorialCloseButton = root.Q<Button>("TutorialCloseButton");
        tutorialCloseIcon = root.Q<Image>("TutorialCloseIcon");
        tutorialConfirmButton = root.Q<Button>("TutorialConfirmButton");
        tutorialPreviousButton = root.Q<Button>("TutorialPreviousButton");
        tutorialPreviousButtonLabel = root.Q<Label>("TutorialPreviousButtonLabel");
        tutorialNextButton = root.Q<Button>("TutorialNextButton");
        tutorialNextButtonLabel = root.Q<Label>("TutorialNextButtonLabel");
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
        tutorialShortCircuitBlockedEntryLabel = root.Q<Label>("TutorialShortCircuitBlockedEntryLabel");
        tutorialSpecialBlockedLabel = root.Q<Label>("TutorialSpecialBlockedLabel");
        tutorialHandImage = root.Q<Image>("TutorialHandImage");
        tutorialShortCircuitTileImage = root.Q<Image>("TutorialShortCircuitTileImage");
        tutorialShortCircuitHandImage = root.Q<Image>("TutorialShortCircuitHandImage");
        tutorialSpecialHandImage = root.Q<Image>("TutorialSpecialHandImage");
        for (int i = 0; i < TutorialSpecialCellCount; i++)
        {
            tutorialSpecialCells[i] = root.Q<VisualElement>($"TutorialSpecialCell{i}");
            tutorialSpecialSprites[i] = root.Q<Image>($"TutorialSpecialSprite{i}");
            tutorialSpecialCounts[i] = root.Q<Label>($"TutorialSpecialCount{i}");
            tutorialSpecialBadges[i] = root.Q<Label>($"TutorialSpecialBadge{i}");
        }
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
            gameProgressBar.style.borderLeftColor = new StyleColor(new Color32(0x00, 0xE5, 0xFF, 0x00));
            gameProgressBar.style.borderRightColor = new StyleColor(new Color32(0x00, 0xE5, 0xFF, 0x00));
            gameProgressBar.style.borderTopColor = new StyleColor(new Color32(0x00, 0xE5, 0xFF, 0x00));
            gameProgressBar.style.borderBottomColor = new StyleColor(new Color32(0x00, 0xE5, 0xFF, 0x00));
            gameProgressBar.style.borderLeftWidth = 0f;
            gameProgressBar.style.borderRightWidth = 0f;
            gameProgressBar.style.borderTopWidth = 0f;
            gameProgressBar.style.borderBottomWidth = 0f;

            VisualElement progressBackground = gameProgressBar.Q(className: ProgressBar.backgroundUssClassName);
            if (progressBackground != null)
            {
                progressBackground.style.backgroundColor = new StyleColor(new Color32(0x02, 0x13, 0x1F, 0xD9));
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
                progressFill.style.backgroundColor = new StyleColor(new Color32(0x00, 0xEF, 0xE6, 0xFF));
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
        AssignSprite(tutorialShortCircuitTileImage, "Sprites/short_circuit_tile", "short_circuit_tile.png");
        ApplyShortCircuitTutorialSpriteRotation();
        AssignSprite(tutorialShortCircuitHandImage, "Sprites/hand", "hand.png");
        AssignSprite(tutorialSpecialHandImage, "Sprites/hand", "hand.png");
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
        if (tutorialPreviousButton != null)
            tutorialPreviousButton.clicked += ShowPreviousSettingsTutorial;
        if (tutorialNextButton != null)
            tutorialNextButton.clicked += ShowNextSettingsTutorial;
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
        RegisterButtonClickAnimation(tutorialPreviousButton);
        RegisterButtonClickAnimation(tutorialNextButton);
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
        ConsumeHeartAndHandleDepletion("game_over", -1, out bool hasHeartsRemaining);
        return hasHeartsRemaining;
    }

    public bool ConsumeHeartOnManualRetry(int moveCount)
    {
        return ConsumeHeartAndHandleDepletion("manual_retry", moveCount, out _);
    }

    private bool ConsumeHeartAndHandleDepletion(string consumeReason, int moveCount, out bool hasHeartsRemaining)
    {
        hasHeartsRemaining = false;
        if (currentHearts <= 0)
        {
            PrepareHeartRefillOffer(consumeReason);
            ShowHeartDepletedPopup();
            return false;
        }

        int nextHeartCount = Mathf.Max(0, currentHearts - 1);
        SetHeartCount(nextHeartCount, animated: true);
        var eventData = new Dictionary<string, object>
        {
            { "reason", consumeReason },
            { "remaining_hearts", currentHearts }
        };
        if (moveCount >= 0)
            eventData["move_count"] = moveCount;
        FirebaseBootstrap.LogEvent("heart_consumed", eventData);

        hasHeartsRemaining = currentHearts > 0;
        if (hasHeartsRemaining)
            return true;

        PrepareHeartRefillOffer(consumeReason);
        ShowHeartDepletedPopup();
        return true;
    }

    private void PrepareHeartRefillOffer(string offerContext)
    {
        isWaitingForHeartRefill = true;
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null && gm.TryPeekSessionFreeHeartRefill(out int thresholdMinutes))
        {
            ConfigureHeartDepletedPopupForSessionReward(thresholdMinutes);
            FirebaseBootstrap.LogEvent("heart_refill_offer", new Dictionary<string, object>
            {
                { "context", offerContext },
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
                { "context", offerContext },
                { "type", "rewarded_ad" }
            });

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            if (rewardedAd == null || !rewardedAd.CanShowAd())
                LoadRewardedAd();
#endif
        }
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
            instructionTextKey = "tutorial_basic_instructions",
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

    private int GetHelpTutorialEntryIndex(HelpTutorialEntryData targetEntry)
    {
        if (targetEntry == null)
            return -1;

        for (int i = 0; i < helpTutorialEntries.Count; i++)
        {
            HelpTutorialEntryData entry = helpTutorialEntries[i];
            if (ReferenceEquals(entry, targetEntry))
                return i;
        }

        if (string.IsNullOrEmpty(targetEntry.id))
            return -1;

        for (int i = 0; i < helpTutorialEntries.Count; i++)
        {
            HelpTutorialEntryData entry = helpTutorialEntries[i];
            if (entry != null && string.Equals(entry.id, targetEntry.id, StringComparison.Ordinal))
                return i;
        }

        return -1;
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

        isTutorialPopupOpen = true;
        activeTutorialOpenedFromSettings = openedFromSettings;

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

        ApplyTutorialEntryToOpenPopup(entry);

        FirebaseBootstrap.LogEvent("help_tutorial_open", new Dictionary<string, object>
        {
            { "tutorial_id", entry.id },
            { "stage_index", entry.stageIndex },
            { "open_type", openedFromSettings ? "settings_button" : "stage_auto" }
        });
    }

    private void ApplyTutorialEntryToOpenPopup(HelpTutorialEntryData entry)
    {
        if (entry == null)
            return;

        activeTutorialEntry = entry;
        if (tutorialTitleLabel != null)
            tutorialTitleLabel.text = ResolveTutorialTitle(entry);
        ApplyTutorialDescriptionText(entry);
        if (tutorialConfirmButton != null)
            tutorialConfirmButton.text = ResolveTutorialCloseButtonText(entry);
        ApplyTutorialInstructionText(entry);
        ConfigureTutorialDemo(entry);
        StartTutorialAnimation(entry);
        RefreshTutorialNavigation();
    }

    private void ShowNextSettingsTutorial()
    {
        ShowSettingsTutorialByOffset(1, "help_tutorial_next");
    }

    private void ShowPreviousSettingsTutorial()
    {
        ShowSettingsTutorialByOffset(-1, "help_tutorial_previous");
    }

    private void ShowSettingsTutorialByOffset(int offset, string eventName)
    {
        if (!isTutorialPopupOpen || !activeTutorialOpenedFromSettings || helpTutorialEntries.Count <= 1)
            return;

        int currentIndex = GetHelpTutorialEntryIndex(activeTutorialEntry);
        int nextIndex = currentIndex < 0 ? 0 : (currentIndex + offset + helpTutorialEntries.Count) % helpTutorialEntries.Count;
        HelpTutorialEntryData nextEntry = helpTutorialEntries[nextIndex];
        if (nextEntry == null)
            return;

        ApplyTutorialEntryToOpenPopup(nextEntry);

        FirebaseBootstrap.LogEvent(eventName, new Dictionary<string, object>
        {
            { "tutorial_id", nextEntry.id },
            { "stage_index", nextEntry.stageIndex },
            { "tutorial_index", nextIndex + 1 },
            { "tutorial_count", helpTutorialEntries.Count }
        });
    }

    private void RefreshTutorialNavigation()
    {
        bool showNavigation = isTutorialPopupOpen && activeTutorialOpenedFromSettings && helpTutorialEntries.Count > 1;
        if (tutorialPreviousButton != null)
            tutorialPreviousButton.style.display = showNavigation ? DisplayStyle.Flex : DisplayStyle.None;
        if (tutorialPreviousButtonLabel != null)
            tutorialPreviousButtonLabel.text = "‹";
        if (tutorialNextButton != null)
            tutorialNextButton.style.display = showNavigation ? DisplayStyle.Flex : DisplayStyle.None;
        if (tutorialNextButtonLabel != null)
            tutorialNextButtonLabel.text = "›";
    }

    private void CloseTutorialPopup()
    {
        if (!isTutorialPopupOpen)
            return;

        string tutorialId = activeTutorialEntry != null ? activeTutorialEntry.id : string.Empty;
        bool openedFromSettings = activeTutorialOpenedFromSettings;
        if (!openedFromSettings && !string.IsNullOrEmpty(tutorialId))
            MarkTutorialDismissed(tutorialId);

        StopTutorialAnimation();
        isTutorialPopupOpen = false;
        activeTutorialOpenedFromSettings = false;
        RefreshTutorialNavigation();

        if (tutorialDialog != null)
        {
            tutorialDialog.style.opacity = 1f;
            tutorialDialog.style.scale = new StyleScale(new Scale(Vector3.one));
        }

        if (tutorialOverlay != null)
            tutorialOverlay.style.display = DisplayStyle.None;

        activeTutorialEntry = null;
        hasStaticTutorialInstructionText = false;
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

        string tutorialType = NormalizeTutorialType(entry.tutorialType);
        if (string.Equals(tutorialType, TutorialTypeBasicPath, StringComparison.OrdinalIgnoreCase))
        {
            tutorialAnimationVersion++;
            tutorialAnimationRoutine = StartCoroutine(PlayBasicTutorialAnimationLoop(tutorialAnimationVersion));
        }
        else if (string.Equals(tutorialType, TutorialTypeShortCircuit, StringComparison.OrdinalIgnoreCase))
        {
            tutorialAnimationVersion++;
            tutorialAnimationRoutine = StartCoroutine(PlayShortCircuitTutorialAnimationLoop(tutorialAnimationVersion));
        }
        else if (string.Equals(tutorialType, TutorialTypeCrossBlast, StringComparison.OrdinalIgnoreCase))
        {
            tutorialAnimationVersion++;
            tutorialAnimationRoutine = StartCoroutine(PlayCrossBlastTutorialAnimationLoop(tutorialAnimationVersion));
        }
        else if (string.Equals(tutorialType, TutorialTypeFixedKnot, StringComparison.OrdinalIgnoreCase))
        {
            tutorialAnimationVersion++;
            tutorialAnimationRoutine = StartCoroutine(PlayFixedKnotTutorialAnimationLoop(tutorialAnimationVersion));
        }
        else if (string.Equals(tutorialType, TutorialTypeTwinLink, StringComparison.OrdinalIgnoreCase))
        {
            tutorialAnimationVersion++;
            tutorialAnimationRoutine = StartCoroutine(PlayTwinLinkTutorialAnimationLoop(tutorialAnimationVersion));
        }
        else if (string.Equals(tutorialType, TutorialTypeIgniter, StringComparison.OrdinalIgnoreCase))
        {
            tutorialAnimationVersion++;
            tutorialAnimationRoutine = StartCoroutine(PlayIgniterTutorialAnimationLoop(tutorialAnimationVersion));
        }
        else if (string.Equals(tutorialType, TutorialTypeBlindCurtain, StringComparison.OrdinalIgnoreCase))
        {
            tutorialAnimationVersion++;
            tutorialAnimationRoutine = StartCoroutine(PlayBlindCurtainTutorialAnimationLoop(tutorialAnimationVersion));
        }
        else if (string.Equals(tutorialType, TutorialTypeBlackout, StringComparison.OrdinalIgnoreCase))
        {
            tutorialAnimationVersion++;
            tutorialAnimationRoutine = StartCoroutine(PlayBlackoutTutorialAnimationLoop(tutorialAnimationVersion));
        }
        else
        {
            tutorialAnimationVersion++;
            tutorialAnimationRoutine = StartCoroutine(PlayBasicTutorialAnimationLoop(tutorialAnimationVersion));
        }
    }

    private static string NormalizeTutorialType(string tutorialType)
    {
        if (string.IsNullOrWhiteSpace(tutorialType))
            return TutorialTypeBasicPath;

        if (string.Equals(tutorialType, TutorialTypeBlackOutAlias, StringComparison.OrdinalIgnoreCase))
            return TutorialTypeBlackout;
        if (string.Equals(tutorialType, TutorialTypeBlackout, StringComparison.OrdinalIgnoreCase))
            return TutorialTypeBlackout;
        if (string.Equals(tutorialType, TutorialTypeShortCircuit, StringComparison.OrdinalIgnoreCase))
            return TutorialTypeShortCircuit;
        if (string.Equals(tutorialType, TutorialTypeCrossBlast, StringComparison.OrdinalIgnoreCase))
            return TutorialTypeCrossBlast;
        if (string.Equals(tutorialType, TutorialTypeFixedKnot, StringComparison.OrdinalIgnoreCase))
            return TutorialTypeFixedKnot;
        if (string.Equals(tutorialType, TutorialTypeTwinLink, StringComparison.OrdinalIgnoreCase))
            return TutorialTypeTwinLink;
        if (string.Equals(tutorialType, TutorialTypeIgniter, StringComparison.OrdinalIgnoreCase))
            return TutorialTypeIgniter;
        if (string.Equals(tutorialType, TutorialTypeBlindCurtain, StringComparison.OrdinalIgnoreCase))
            return TutorialTypeBlindCurtain;
        if (string.Equals(tutorialType, TutorialTypeBasicPath, StringComparison.OrdinalIgnoreCase))
            return TutorialTypeBasicPath;

        return TutorialTypeBasicPath;
    }

    private static bool IsSpecialTutorialType(string tutorialType)
    {
        string normalized = NormalizeTutorialType(tutorialType);
        return string.Equals(normalized, TutorialTypeCrossBlast, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, TutorialTypeFixedKnot, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, TutorialTypeTwinLink, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, TutorialTypeIgniter, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, TutorialTypeBlindCurtain, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, TutorialTypeBlackout, StringComparison.OrdinalIgnoreCase);
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
        WaitForSecondsRealtime introWait = new WaitForSecondsRealtime(0.55f);
        WaitForSecondsRealtime stepWait = new WaitForSecondsRealtime(0.72f);
        WaitForSecondsRealtime cycleWait = new WaitForSecondsRealtime(1f);

        while (isTutorialPopupOpen && animationVersion == tutorialAnimationVersion)
        {
            ApplyBasicTutorialStepState(1, 2, 1, T("tutorial_step_start"), trailPhase: 0);
            SetTutorialHandPosition(0, instant: true);
            yield return introWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            SetTutorialHandPosition(1, instant: false);
            ApplyBasicTutorialStepState(0, 2, 1, T("tutorial_step_left"), pulseLeft: true, trailPhase: 1);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            SetTutorialHandPosition(2, instant: false);
            ApplyBasicTutorialStepState(0, 1, 1, T("tutorial_step_center"), pulseCenter: true, trailPhase: 2);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            SetTutorialHandPosition(1, instant: false);
            ApplyBasicTutorialStepState(0, 1, 0, T("tutorial_step_right"), pulseRight: true, trailPhase: 3);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyBasicTutorialStepState(0, 0, 0, T("tutorial_step_clear"), pulseCenter: true, trailPhase: 4);
            yield return cycleWait;
        }

        tutorialAnimationRoutine = null;
    }

    private IEnumerator PlayShortCircuitTutorialAnimationLoop(int animationVersion)
    {
        WaitForSecondsRealtime introWait = new WaitForSecondsRealtime(0.62f);
        WaitForSecondsRealtime stepWait = new WaitForSecondsRealtime(0.76f);
        WaitForSecondsRealtime cycleWait = new WaitForSecondsRealtime(1.05f);

        while (isTutorialPopupOpen && animationVersion == tutorialAnimationVersion)
        {
            ApplyShortCircuitTutorialStepState(
                1, 1, 1, 1,
                T("tutorial_short_circuit_hint_intro"),
                pulseBottomLeft: true,
                pulseArrow: true,
                pathPhase: 0);
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
                pulseArrow: true,
                pathPhase: 1);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            SetShortCircuitTutorialHandPosition(3, instant: false);
            ApplyShortCircuitTutorialStepState(
                1, 1, 0, 1,
                T("tutorial_short_circuit_step_blocked_entry"),
                pulseBottomRight: true,
                pulseArrow: true,
                pathPhase: 2);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            SetShortCircuitTutorialHandPosition(2, instant: false);
            ApplyShortCircuitTutorialStepState(
                1, 1, 0, 0,
                T("tutorial_short_circuit_step_follow"),
                pulseTopRight: true,
                pathPhase: 2);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyShortCircuitTutorialStepState(
                1, 1, 0, 0,
                T("tutorial_short_circuit_step_remember"),
                pulseTopRight: true,
                pulseArrow: true,
                pathPhase: 2);
            yield return cycleWait;
        }

        tutorialAnimationRoutine = null;
    }

    private IEnumerator PlayCrossBlastTutorialAnimationLoop(int animationVersion)
    {
        WaitForSecondsRealtime stepWait = new WaitForSecondsRealtime(0.76f);
        WaitForSecondsRealtime cycleWait = new WaitForSecondsRealtime(1.05f);

        while (isTutorialPopupOpen && animationVersion == tutorialAnimationVersion)
        {
            ApplyCrossBlastTutorialState(0);
            SetSpecialTutorialHandToCell(4, instant: true);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyCrossBlastTutorialState(1);
            SetSpecialTutorialHandToCell(4, instant: false);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyCrossBlastTutorialState(2);
            SetSpecialTutorialHandToCell(5, instant: false);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyCrossBlastTutorialState(3);
            yield return cycleWait;
        }

        tutorialAnimationRoutine = null;
    }

    private IEnumerator PlayFixedKnotTutorialAnimationLoop(int animationVersion)
    {
        WaitForSecondsRealtime stepWait = new WaitForSecondsRealtime(0.76f);
        WaitForSecondsRealtime cycleWait = new WaitForSecondsRealtime(1.05f);

        while (isTutorialPopupOpen && animationVersion == tutorialAnimationVersion)
        {
            ApplyFixedKnotTutorialState(0);
            SetSpecialTutorialHandToCell(3, instant: true);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyFixedKnotTutorialState(1);
            SetSpecialTutorialHandToCell(4, instant: false);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyFixedKnotTutorialState(2);
            SetSpecialTutorialHandToCell(5, instant: false);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyFixedKnotTutorialState(3);
            yield return cycleWait;
        }

        tutorialAnimationRoutine = null;
    }

    private IEnumerator PlayTwinLinkTutorialAnimationLoop(int animationVersion)
    {
        WaitForSecondsRealtime stepWait = new WaitForSecondsRealtime(0.76f);
        WaitForSecondsRealtime cycleWait = new WaitForSecondsRealtime(1.05f);

        while (isTutorialPopupOpen && animationVersion == tutorialAnimationVersion)
        {
            ApplyTwinLinkTutorialState(0);
            SetSpecialTutorialHandToCell(3, instant: true);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyTwinLinkTutorialState(1);
            SetSpecialTutorialHandToCell(4, instant: false);
            yield return cycleWait;
        }

        tutorialAnimationRoutine = null;
    }

    private IEnumerator PlayIgniterTutorialAnimationLoop(int animationVersion)
    {
        WaitForSecondsRealtime stepWait = new WaitForSecondsRealtime(0.76f);
        WaitForSecondsRealtime cycleWait = new WaitForSecondsRealtime(1.05f);

        while (isTutorialPopupOpen && animationVersion == tutorialAnimationVersion)
        {
            ApplyIgniterTutorialState(0);
            SetSpecialTutorialHandToCell(3, instant: true);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyIgniterTutorialState(1);
            SetSpecialTutorialHandToCell(4, instant: false);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyIgniterTutorialState(2);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyIgniterTutorialState(3);
            SetSpecialTutorialHandToCell(5, instant: false);
            yield return cycleWait;
        }

        tutorialAnimationRoutine = null;
    }

    private IEnumerator PlayBlindCurtainTutorialAnimationLoop(int animationVersion)
    {
        WaitForSecondsRealtime stepWait = new WaitForSecondsRealtime(0.76f);
        WaitForSecondsRealtime cycleWait = new WaitForSecondsRealtime(1.05f);

        while (isTutorialPopupOpen && animationVersion == tutorialAnimationVersion)
        {
            ApplyBlindCurtainTutorialState(0);
            SetSpecialTutorialHandToCell(3, instant: true);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyBlindCurtainTutorialState(1);
            SetSpecialTutorialHandToCell(4, instant: false);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyBlindCurtainTutorialState(2);
            SetSpecialTutorialHandToCell(5, instant: false);
            yield return cycleWait;
        }

        tutorialAnimationRoutine = null;
    }

    private IEnumerator PlayBlackoutTutorialAnimationLoop(int animationVersion)
    {
        WaitForSecondsRealtime stepWait = new WaitForSecondsRealtime(0.76f);
        WaitForSecondsRealtime cycleWait = new WaitForSecondsRealtime(1.05f);

        while (isTutorialPopupOpen && animationVersion == tutorialAnimationVersion)
        {
            ApplyBlackoutTutorialState(0);
            SetSpecialTutorialHandToCell(3, instant: true);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyBlackoutTutorialState(1);
            SetSpecialTutorialHandToCell(4, instant: false);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyBlackoutTutorialState(2);
            yield return stepWait;

            if (!IsTutorialAnimationActive(animationVersion))
                yield break;
            ApplyBlackoutTutorialState(3);
            yield return cycleWait;
        }

        tutorialAnimationRoutine = null;
    }

    private bool IsTutorialAnimationActive(int animationVersion)
    {
        return isTutorialPopupOpen && animationVersion == tutorialAnimationVersion;
    }

    private void ApplyBasicTutorialStepState(
        int leftCount,
        int centerCount,
        int rightCount,
        string hint,
        bool pulseLeft = false,
        bool pulseCenter = false,
        bool pulseRight = false,
        int trailPhase = 0)
    {
        ApplyBasicTutorialTileState(tutorialTileLeft, tutorialTileLeftCount, leftCount, pulseLeft);
        ApplyBasicTutorialTileState(tutorialTileCenter, tutorialTileCenterCount, centerCount, pulseCenter);
        ApplyBasicTutorialTileState(tutorialTileRight, tutorialTileRightCount, rightCount, pulseRight);
        ApplyBasicTutorialTrailState(trailPhase);

        SetTutorialStepHint(hint);
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
        bool pulseArrow = false,
        int pathPhase = 0,
        bool showBlockedEntry = false)
    {
        ApplyTutorialTileState(tutorialShortTileTopLeft, tutorialShortTileTopLeftCount, topLeftCount, pulseTopLeft);
        ApplyTutorialTileState(tutorialShortTileTopRight, tutorialShortTileTopRightCount, topRightCount, pulseTopRight);
        ApplyShortCircuitTileState(tutorialShortTileBottomLeft, tutorialShortTileBottomLeftCount, bottomLeftCount, pulseBottomLeft, pulseArrow);
        ApplyTutorialTileState(tutorialShortTileBottomRight, tutorialShortTileBottomRightCount, bottomRightCount, pulseBottomRight);
        ApplyShortCircuitTutorialPathState(pathPhase, showBlockedEntry);

        SetTutorialStepHint(hint);
    }

    private void ApplyShortCircuitTutorialPathState(int pathPhase, bool showBlockedEntry)
    {
        UpdateShortCircuitTutorialGeometry();

        bool exitActive = pathPhase >= 1;
        ApplyShortCircuitExitTrail(tutorialShortCircuitExitTrail, exitActive, pathPhase == 1);
        ApplyShortCircuitBlockedEntry(showBlockedEntry);
    }

    private void ApplyShortCircuitBlockedEntry(bool showBlockedEntry)
    {
        if (tutorialShortCircuitBlockedEntry == null)
            return;

        tutorialShortCircuitBlockedEntry.style.opacity = showBlockedEntry ? 1f : 0f;
        tutorialShortCircuitBlockedEntry.style.scale = new StyleScale(
            new Scale(showBlockedEntry ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one));

        if (tutorialShortCircuitBlockedEntryLabel != null)
            tutorialShortCircuitBlockedEntryLabel.style.opacity = showBlockedEntry ? 1f : 0f;
    }

    private static void ApplyShortCircuitExitTrail(VisualElement trail, bool active, bool pulse)
    {
        if (trail == null)
            return;

        trail.style.opacity = active ? 0.9f : 0.16f;
        trail.style.backgroundColor = active
            ? new StyleColor(new Color(1f, 0.72f, 0.31f, 0.32f))
            : new StyleColor(new Color(0.45f, 0.32f, 0.18f, 0.12f));
        SetElementBorderColor(trail, active
            ? new Color(1f, 0.92f, 0.64f, 0.92f)
            : new Color(0.68f, 0.54f, 0.36f, 0.36f));

        if (pulse)
        {
            trail.style.scale = new StyleScale(new Scale(new Vector3(1f, 1.45f, 1f)));
            trail.schedule.Execute(() =>
            {
                trail.style.scale = new StyleScale(new Scale(Vector3.one));
            }).StartingIn(150);
        }
        else
        {
            trail.style.scale = new StyleScale(new Scale(Vector3.one));
        }
    }

    private void ApplyCrossBlastTutorialState(int phase)
    {
        ResetSpecialTutorialBoard();

        bool exploded = phase >= 2;
        SetSpecialCell(1, exploded ? "1" : "2", null, string.Empty, active: true);
        SetSpecialCell(3, exploded ? "1" : "2", null, string.Empty, active: true);
        SetSpecialCell(4, exploded ? "0" : "1", CrossBlastSpriteResourcePath, "tutorial-special-cell-cross", active: true, pulse: phase == 1);
        SetSpecialCell(5, "2", null, string.Empty, active: true, pulse: phase == 2);
        SetSpecialCell(7, exploded ? "1" : "2", null, string.Empty, active: true);

        if (phase >= 1)
        {
            ShowSpecialLineBetween(tutorialSpecialBeamHorizontal, 3, 5, new Color(0.55f, 1f, 1f, 0.95f), pulse: phase == 1);
            ShowSpecialLineBetween(tutorialSpecialBeamVertical, 1, 7, new Color(0.55f, 1f, 1f, 0.95f), pulse: phase == 1);
            ShowSpecialPulseAtCell(tutorialSpecialPulse, 4, new Color(0.55f, 1f, 1f, 0.92f), phase == 1);
        }

        if (phase >= 2)
            ShowSpecialLineBetween(tutorialSpecialTrailA, 4, 5, new Color(0.94f, 1f, 0.64f, 0.92f), pulse: phase == 2);

        string hintKey = phase == 0 ? "tutorial_cross_blast_step_intro" :
            phase == 1 ? "tutorial_cross_blast_step_blast" :
            phase == 2 ? "tutorial_cross_blast_step_adjacent" :
            "tutorial_cross_blast_step_exclude";
        SetSpecialHint(T(hintKey));
    }

    private void ApplyFixedKnotTutorialState(int phase)
    {
        ResetSpecialTutorialBoard();

        SetSpecialCell(3, phase >= 1 ? "0" : "1", null, string.Empty, active: true, pulse: phase == 0);
        SetSpecialCell(4, phase >= 2 ? "0" : "1", null, string.Empty, active: true, pulse: phase == 1);

        string fixedCount = phase == 0 ? "3" : phase == 1 ? "2" : phase == 2 ? "1" : "0";
        SetSpecialCell(5, fixedCount, FixedKnotSpriteResourcePath, "tutorial-special-cell-fixed", active: true, pulse: phase == 2, badge: "3");

        if (phase >= 1)
            ShowSpecialLineBetween(tutorialSpecialTrailA, 3, 4, new Color(1f, 0.82f, 0.48f, 0.92f), pulse: phase == 1);
        if (phase >= 2)
            ShowSpecialLineBetween(tutorialSpecialTrailB, 4, 5, new Color(1f, 0.82f, 0.48f, 0.92f), pulse: phase == 2);
        string hintKey = phase == 0 ? "tutorial_fixed_knot_step_intro" :
            phase == 1 ? "tutorial_fixed_knot_step_countdown" :
            phase == 2 ? "tutorial_fixed_knot_step_exact" :
            "tutorial_fixed_knot_step_missed";
        SetSpecialHint(T(hintKey));
    }

    private void ApplyTwinLinkTutorialState(int phase)
    {
        ResetSpecialTutorialBoard();

        string twinCount = phase >= 1 ? "1" : "2";

        SetSpecialCell(3, twinCount, null, "tutorial-special-cell-twin", active: true, pulse: phase == 1, badge: "A");
        SetSpecialCell(5, twinCount, null, "tutorial-special-cell-twin", active: true, pulse: phase == 1, badge: "A");
        SetSpecialCell(4, "1", null, string.Empty, active: true, pulse: phase == 1);

        ShowSpecialLineBetween(tutorialSpecialPairLine, 3, 5, new Color(0.72f, 0.48f, 1f, 0.96f), pulse: phase == 1);
        if (phase >= 1)
            ShowSpecialLineBetween(tutorialSpecialTrailA, 3, 4, new Color(0.72f, 0.48f, 1f, 0.76f), pulse: phase == 1);

        string hintKey = phase == 0 ? "tutorial_twin_link_step_intro" :
            "tutorial_twin_link_step_pair";
        SetSpecialHint(T(hintKey));
    }

    private void ApplyIgniterTutorialState(int phase)
    {
        ResetSpecialTutorialBoard();

        SetSpecialCell(3, phase >= 1 ? "0" : "1", null, string.Empty, active: true);
        SetSpecialCell(4, "1", IgniterSpriteResourcePath, "tutorial-special-cell-igniter", active: true, pulse: phase == 1);
        SetSpecialCell(1, phase >= 2 ? "1" : string.Empty, null, string.Empty, active: phase >= 2, pulse: phase == 2, hidden: phase < 2);
        SetSpecialCell(5, phase >= 2 ? "1" : string.Empty, null, string.Empty, active: phase >= 2, pulse: phase == 2, hidden: phase < 2);

        if (phase >= 1)
            ShowSpecialPulseAtCell(tutorialSpecialPulse, 4, new Color(1f, 0.66f, 0.26f, 0.94f), phase == 1);
        if (phase >= 2)
        {
            ShowSpecialLineBetween(tutorialSpecialTrailA, 4, 1, new Color(1f, 0.66f, 0.26f, 0.82f), pulse: phase == 2);
            ShowSpecialLineBetween(tutorialSpecialTrailB, 4, 5, new Color(1f, 0.66f, 0.26f, 0.82f), pulse: phase == 2);
            ShowSpecialPulseAtCell(tutorialSpecialRevealPulseA, 1, new Color(1f, 0.72f, 0.34f, 0.86f), phase == 2);
            ShowSpecialPulseAtCell(tutorialSpecialRevealPulseB, 5, new Color(1f, 0.72f, 0.34f, 0.86f), phase == 2);
        }

        string hintKey = phase == 0 ? "tutorial_igniter_step_intro" :
            phase == 1 ? "tutorial_igniter_step_trigger" :
            phase == 2 ? "tutorial_igniter_step_reveal" :
            "tutorial_igniter_step_continue";
        SetSpecialHint(T(hintKey));
    }

    private void ApplyBlindCurtainTutorialState(int phase)
    {
        ResetSpecialTutorialBoard();

        SetSpecialCell(3, phase >= 1 ? "0" : "1", null, string.Empty, active: true);
        SetSpecialCell(4, "?", BlindCurtainSpriteResourcePath, "tutorial-special-cell-blind", active: phase < 2, pulse: phase == 1);
        SetSpecialCell(5, "1", null, string.Empty, active: true, pulse: phase == 2);

        if (phase >= 1)
            ShowSpecialLineBetween(tutorialSpecialTrailA, 3, 4, new Color(0.75f, 0.9f, 1f, 0.82f), pulse: phase == 1);
        if (phase >= 2)
            ShowSpecialLineBetween(tutorialSpecialTrailB, 4, 5, new Color(0.75f, 0.9f, 1f, 0.82f), pulse: phase == 2);

        string hintKey = phase == 0 ? "tutorial_blind_curtain_step_intro" :
            phase == 1 ? "tutorial_blind_curtain_step_unknown" :
            "tutorial_blind_curtain_step_normal";
        SetSpecialHint(T(hintKey));
    }

    private void ApplyBlackoutTutorialState(int phase)
    {
        ResetSpecialTutorialBoard();

        for (int i = 0; i < TutorialSpecialCellCount; i++)
        {
            bool center = i == 4;
            bool question = phase >= 2 || center;
            string text = question ? "?" : ((i % 3) + 1).ToString();
            string theme = center || phase >= 2 ? "tutorial-special-cell-blackout" : string.Empty;
            string sprite = null;
            SetSpecialCell(i, text, sprite, theme, active: true, pulse: center && phase == 1);
        }

        string hintKey = phase == 0 ? "tutorial_blackout_step_intro" :
            phase == 1 ? "tutorial_blackout_step_trigger" :
            phase == 2 ? "tutorial_blackout_step_flip" :
            "tutorial_blackout_step_memory";
        SetSpecialHint(T(hintKey));
    }

    private void ResetSpecialTutorialBoard()
    {
        for (int i = 0; i < TutorialSpecialCellCount; i++)
        {
            VisualElement cell = tutorialSpecialCells[i];
            if (cell != null)
            {
                foreach (string themeClass in TutorialSpecialCellThemeClasses)
                    cell.RemoveFromClassList(themeClass);
                cell.style.opacity = 0.16f;
                cell.style.scale = new StyleScale(new Scale(Vector3.one));
                cell.style.backgroundColor = new StyleColor(new Color(0.04f, 0.1f, 0.14f, 0.5f));
                SetElementBorderColor(cell, new Color(0.38f, 0.58f, 0.68f, 0.32f));
            }

            if (tutorialSpecialSprites[i] != null)
            {
                tutorialSpecialSprites[i].image = null;
                tutorialSpecialSprites[i].style.opacity = 0f;
                tutorialSpecialSprites[i].style.scale = new StyleScale(new Scale(Vector3.one));
                tutorialSpecialSprites[i].style.rotate = new StyleRotate(new Rotate(0f));
            }

            if (tutorialSpecialCounts[i] != null)
            {
                tutorialSpecialCounts[i].text = string.Empty;
                tutorialSpecialCounts[i].style.opacity = 1f;
                tutorialSpecialCounts[i].style.color = new StyleColor(new Color(0.86f, 0.98f, 1f, 0.34f));
            }

            if (tutorialSpecialBadges[i] != null)
            {
                tutorialSpecialBadges[i].text = string.Empty;
                tutorialSpecialBadges[i].style.opacity = 0f;
            }
        }

        ResetSpecialEffect(tutorialSpecialTrailA);
        ResetSpecialEffect(tutorialSpecialTrailB);
        ResetSpecialEffect(tutorialSpecialTrailC);
        ResetSpecialEffect(tutorialSpecialBeamHorizontal);
        ResetSpecialEffect(tutorialSpecialBeamVertical);
        ResetSpecialEffect(tutorialSpecialPairLine);
        ResetSpecialEffect(tutorialSpecialPulse);
        ResetSpecialEffect(tutorialSpecialRevealPulseA);
        ResetSpecialEffect(tutorialSpecialRevealPulseB);
        ResetSpecialEffect(tutorialSpecialBlockedMarker);

        if (tutorialSpecialBlockedLabel != null)
            tutorialSpecialBlockedLabel.style.opacity = 0f;
        if (tutorialSpecialHandImage != null)
        {
            tutorialSpecialHandImage.style.opacity = 0f;
            tutorialSpecialHandImage.style.scale = new StyleScale(new Scale(Vector3.one));
        }
    }

    private static void ResetSpecialEffect(VisualElement element)
    {
        if (element == null)
            return;

        element.style.opacity = 0f;
        element.style.scale = new StyleScale(new Scale(Vector3.one));
        element.style.rotate = new StyleRotate(new Rotate(0f));
    }

    private void SetSpecialCell(
        int index,
        string countText,
        string spritePath,
        string themeClass,
        bool active,
        bool pulse = false,
        string badge = "",
        bool hidden = false)
    {
        if (index < 0 || index >= TutorialSpecialCellCount)
            return;

        VisualElement cell = tutorialSpecialCells[index];
        if (cell != null)
        {
            if (!string.IsNullOrEmpty(themeClass))
                cell.AddToClassList(themeClass);
            if (hidden)
                cell.AddToClassList("tutorial-special-cell-hidden");

            ApplySpecialCellTheme(cell, themeClass, active, hidden);
            cell.style.opacity = hidden ? 0.24f : active ? 1f : 0.36f;
            if (pulse)
            {
                cell.style.scale = new StyleScale(new Scale(new Vector3(1.13f, 1.13f, 1f)));
                cell.schedule.Execute(() =>
                {
                    cell.style.scale = new StyleScale(new Scale(Vector3.one));
                }).StartingIn(140);
            }
        }

        Label countLabel = tutorialSpecialCounts[index];
        if (countLabel != null)
        {
            countLabel.text = countText ?? string.Empty;
            countLabel.style.opacity = string.IsNullOrEmpty(countLabel.text) ? 0f : 1f;
            countLabel.style.color = new StyleColor(countLabel.text == "?"
                ? new Color(0.92f, 0.97f, 1f, 0.72f)
                : new Color(0.86f, 0.98f, 1f, active ? 0.36f : 0.18f));
        }

        Label badgeLabel = tutorialSpecialBadges[index];
        if (badgeLabel != null)
        {
            badgeLabel.text = badge ?? string.Empty;
            badgeLabel.style.opacity = string.IsNullOrEmpty(badgeLabel.text) ? 0f : 1f;
        }

        Image spriteImage = tutorialSpecialSprites[index];
        if (spriteImage != null)
        {
            Sprite sprite = LoadTutorialSprite(spritePath);
            if (sprite != null)
            {
                spriteImage.image = sprite.texture;
                spriteImage.scaleMode = ScaleMode.ScaleToFit;
                spriteImage.style.overflow = Overflow.Visible;
                spriteImage.uv = new Rect(0f, 0f, 1f, 1f);
                spriteImage.style.opacity = active ? 0.94f : 0.28f;
            }
            else
            {
                spriteImage.image = null;
                spriteImage.style.opacity = 0f;
            }
        }
    }

    private static void ApplySpecialCellTheme(VisualElement cell, string themeClass, bool active, bool hidden)
    {
        if (cell == null)
            return;

        Color backgroundColor = new Color(0.1f, 0.22f, 0.29f, 0.8f);
        Color borderColor = new Color(0.6f, 0.9f, 1f, 0.84f);

        if (hidden)
        {
            backgroundColor = new Color(0.02f, 0.04f, 0.07f, 0.88f);
            borderColor = new Color(0.16f, 0.22f, 0.3f, 0.72f);
        }
        else
        {
            switch (themeClass)
            {
                case "tutorial-special-cell-cross":
                    backgroundColor = new Color(0.05f, 0.29f, 0.34f, 0.82f);
                    borderColor = new Color(0.36f, 0.96f, 1f, 0.96f);
                    break;
                case "tutorial-special-cell-fixed":
                    backgroundColor = new Color(0.32f, 0.09f, 0.12f, 0.82f);
                    borderColor = new Color(1f, 0.48f, 0.48f, 0.96f);
                    break;
                case "tutorial-special-cell-twin":
                    backgroundColor = new Color(0.22f, 0.11f, 0.4f, 0.82f);
                    borderColor = new Color(0.72f, 0.52f, 1f, 0.98f);
                    break;
                case "tutorial-special-cell-igniter":
                    backgroundColor = new Color(0.36f, 0.18f, 0.04f, 0.84f);
                    borderColor = new Color(1f, 0.78f, 0.22f, 0.98f);
                    break;
                case "tutorial-special-cell-blind":
                    backgroundColor = new Color(0.03f, 0.09f, 0.14f, 0.9f);
                    borderColor = new Color(0.6f, 0.78f, 0.92f, 0.92f);
                    break;
                case "tutorial-special-cell-blackout":
                    backgroundColor = new Color(0.01f, 0.015f, 0.025f, 0.94f);
                    borderColor = new Color(0.8f, 0.85f, 1f, 0.8f);
                    break;
            }

            if (!active)
            {
                backgroundColor.a *= 0.55f;
                borderColor.a *= 0.55f;
            }
        }

        cell.style.backgroundColor = new StyleColor(backgroundColor);
        SetElementBorderColor(cell, borderColor);
    }

    private Sprite LoadTutorialSprite(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return null;

        if (tutorialSpriteCache.TryGetValue(resourcePath, out Sprite cachedSprite))
            return cachedSprite;

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
            Debug.LogWarning($"[GameMainUIController] Resources/{resourcePath} 도움말 스프라이트를 찾을 수 없습니다.");

        tutorialSpriteCache[resourcePath] = sprite;
        return sprite;
    }

    private void SetSpecialHint(string hint)
    {
        SetTutorialStepHint(hint);
    }

    private void SetTutorialStepHint(string hint)
    {
        if (tutorialStepHintLabel != null && !hasStaticTutorialInstructionText)
            tutorialStepHintLabel.text = hint;
    }

    private void ShowSpecialBlockedMarkerAtCell(int cellIndex)
    {
        PositionSpecialElementAtCell(tutorialSpecialBlockedMarker, cellIndex, TutorialSpecialMarkerSize, TutorialSpecialMarkerSize);
        if (tutorialSpecialBlockedMarker != null)
        {
            tutorialSpecialBlockedMarker.style.opacity = 1f;
            tutorialSpecialBlockedMarker.style.scale = new StyleScale(new Scale(new Vector3(1.08f, 1.08f, 1f)));
        }
        if (tutorialSpecialBlockedLabel != null)
            tutorialSpecialBlockedLabel.style.opacity = 1f;
    }

    private void ShowSpecialPulseAtCell(VisualElement pulseElement, int cellIndex, Color color, bool pulse)
    {
        PositionSpecialElementAtCell(pulseElement, cellIndex, TutorialSpecialPulseSize, TutorialSpecialPulseSize);
        if (pulseElement == null)
            return;

        pulseElement.style.opacity = 0.9f;
        pulseElement.style.backgroundColor = new StyleColor(new Color(color.r, color.g, color.b, 0.14f));
        SetElementBorderColor(pulseElement, color);
        pulseElement.style.scale = new StyleScale(new Scale(pulse ? new Vector3(1.24f, 1.24f, 1f) : Vector3.one));
    }

    private void ShowSpecialLineBetween(VisualElement line, int fromCellIndex, int toCellIndex, Color color, bool pulse)
    {
        PositionSpecialLineBetween(line, fromCellIndex, toCellIndex);
        if (line == null)
            return;

        line.style.opacity = 0.88f;
        line.style.backgroundColor = new StyleColor(new Color(color.r, color.g, color.b, 0.22f));
        SetElementBorderColor(line, color);
        line.style.scale = new StyleScale(new Scale(pulse ? new Vector3(1f, 1.35f, 1f) : Vector3.one));
    }

    private void PositionSpecialLineBetween(VisualElement line, int fromCellIndex, int toCellIndex)
    {
        if (line == null || !TryGetSpecialCellBounds(fromCellIndex, out Rect fromBounds) || !TryGetSpecialCellBounds(toCellIndex, out Rect toBounds) || tutorialSpecialDemoBoard == null)
            return;

        Rect boardBounds = tutorialSpecialDemoBoard.worldBound;
        if (boardBounds.width <= 0f || boardBounds.height <= 0f)
            return;

        Vector2 fromCenter = fromBounds.center;
        Vector2 toCenter = toBounds.center;
        bool horizontal = Mathf.Abs(toCenter.x - fromCenter.x) >= Mathf.Abs(toCenter.y - fromCenter.y);

        if (horizontal)
        {
            float left = Mathf.Min(fromCenter.x, toCenter.x) - boardBounds.x;
            float top = ((fromCenter.y + toCenter.y) * 0.5f) - boardBounds.y - TutorialSpecialLineThickness * 0.5f;
            line.style.left = Mathf.Clamp(left, 0f, Mathf.Max(0f, boardBounds.width));
            line.style.top = Mathf.Clamp(top, 0f, Mathf.Max(0f, boardBounds.height - TutorialSpecialLineThickness));
            line.style.width = Mathf.Abs(toCenter.x - fromCenter.x);
            line.style.height = TutorialSpecialLineThickness;
        }
        else
        {
            float left = ((fromCenter.x + toCenter.x) * 0.5f) - boardBounds.x - TutorialSpecialLineThickness * 0.5f;
            float top = Mathf.Min(fromCenter.y, toCenter.y) - boardBounds.y;
            line.style.left = Mathf.Clamp(left, 0f, Mathf.Max(0f, boardBounds.width - TutorialSpecialLineThickness));
            line.style.top = Mathf.Clamp(top, 0f, Mathf.Max(0f, boardBounds.height));
            line.style.width = TutorialSpecialLineThickness;
            line.style.height = Mathf.Abs(toCenter.y - fromCenter.y);
        }
    }

    private void PositionSpecialElementAtCell(VisualElement element, int cellIndex, float width, float height)
    {
        if (element == null || !TryGetSpecialCellBounds(cellIndex, out Rect cellBounds) || tutorialSpecialDemoBoard == null)
            return;

        Rect boardBounds = tutorialSpecialDemoBoard.worldBound;
        if (boardBounds.width <= 0f || boardBounds.height <= 0f)
            return;

        element.style.left = Mathf.Clamp(cellBounds.center.x - boardBounds.x - width * 0.5f, 0f, Mathf.Max(0f, boardBounds.width - width));
        element.style.top = Mathf.Clamp(cellBounds.center.y - boardBounds.y - height * 0.5f, 0f, Mathf.Max(0f, boardBounds.height - height));
        element.style.width = width;
        element.style.height = height;
    }

    private bool TryGetSpecialCellBounds(int cellIndex, out Rect bounds)
    {
        bounds = default(Rect);
        if (cellIndex < 0 || cellIndex >= TutorialSpecialCellCount)
            return false;

        VisualElement cell = tutorialSpecialCells[cellIndex];
        if (cell == null || cell.panel == null)
            return false;

        bounds = cell.worldBound;
        return bounds.width > 0f && bounds.height > 0f;
    }

    private void SetSpecialTutorialHandToCell(int cellIndex, bool instant)
    {
        if (tutorialSpecialHandImage == null)
            return;

        if (TrySetSpecialTutorialHandToCell(cellIndex, instant))
            return;

        int col = Mathf.Clamp(cellIndex % 3, 0, 2);
        int row = Mathf.Clamp(cellIndex / 3, 0, 2);
        tutorialSpecialHandImage.style.left = Length.Percent(31f + col * 17f);
        tutorialSpecialHandImage.style.top = 38f + row * 96f;
        tutorialSpecialHandImage.style.opacity = 1f;
        tutorialSpecialHandImage.style.scale = new StyleScale(new Scale(instant ? Vector3.one : new Vector3(1.04f, 1.04f, 1f)));

        if (tutorialSpecialDemoBoard != null)
        {
            tutorialSpecialDemoBoard.schedule.Execute(() =>
            {
                TrySetSpecialTutorialHandToCell(cellIndex, instant);
            }).StartingIn(32);
        }
    }

    private bool TrySetSpecialTutorialHandToCell(int cellIndex, bool instant)
    {
        if (tutorialSpecialDemoBoard == null || tutorialSpecialHandImage == null || !TryGetSpecialCellBounds(cellIndex, out Rect cellBounds))
            return false;
        if (tutorialSpecialDemoBoard.panel == null)
            return false;

        Rect boardBounds = tutorialSpecialDemoBoard.worldBound;
        if (boardBounds.width <= 0f || boardBounds.height <= 0f)
            return false;

        float handWidth = GetResolvedDimension(tutorialSpecialHandImage, useWidth: true, 88f);
        float handHeight = GetResolvedDimension(tutorialSpecialHandImage, useWidth: false, 88f);
        float left = cellBounds.center.x - boardBounds.x - handWidth * 0.5f;
        float top = cellBounds.yMax - boardBounds.y - handHeight * 0.34f;

        tutorialSpecialHandImage.style.left = Mathf.Clamp(left, 0f, Mathf.Max(0f, boardBounds.width - handWidth));
        tutorialSpecialHandImage.style.top = Mathf.Clamp(top, 0f, Mathf.Max(0f, boardBounds.height - handHeight));
        tutorialSpecialHandImage.style.opacity = 1f;
        tutorialSpecialHandImage.style.scale = new StyleScale(new Scale(instant ? Vector3.one : new Vector3(1.04f, 1.04f, 1f)));
        return true;
    }

    private SpecialTutorialPreset GetSpecialTutorialPreset(string tutorialType)
    {
        string normalized = NormalizeTutorialType(tutorialType);
        switch (normalized)
        {
            case TutorialTypeCrossBlast:
                return new SpecialTutorialPreset(normalized, "tutorial-special-cell-cross", CrossBlastSpriteResourcePath, "tutorial_cross_blast_step_intro", 4);
            case TutorialTypeFixedKnot:
                return new SpecialTutorialPreset(normalized, "tutorial-special-cell-fixed", FixedKnotSpriteResourcePath, "tutorial_fixed_knot_step_intro", 5);
            case TutorialTypeTwinLink:
                return new SpecialTutorialPreset(normalized, "tutorial-special-cell-twin", null, "tutorial_twin_link_step_intro", 3);
            case TutorialTypeIgniter:
                return new SpecialTutorialPreset(normalized, "tutorial-special-cell-igniter", IgniterSpriteResourcePath, "tutorial_igniter_step_intro", 4);
            case TutorialTypeBlindCurtain:
                return new SpecialTutorialPreset(normalized, "tutorial-special-cell-blind", null, "tutorial_blind_curtain_step_intro", 4);
            case TutorialTypeBlackout:
                return new SpecialTutorialPreset(normalized, "tutorial-special-cell-blackout", null, "tutorial_blackout_step_intro", 4);
            default:
                return null;
        }
    }

    private void ConfigureSpecialTutorialDemo(string tutorialType)
    {
        SpecialTutorialPreset preset = GetSpecialTutorialPreset(tutorialType);
        ResetSpecialTutorialBoard();
        if (preset == null)
            return;

        SetSpecialHint(T(preset.initialHintKey));
        switch (preset.tutorialType)
        {
            case TutorialTypeCrossBlast:
                ApplyCrossBlastTutorialState(0);
                break;
            case TutorialTypeFixedKnot:
                ApplyFixedKnotTutorialState(0);
                break;
            case TutorialTypeTwinLink:
                ApplyTwinLinkTutorialState(0);
                break;
            case TutorialTypeIgniter:
                ApplyIgniterTutorialState(0);
                break;
            case TutorialTypeBlindCurtain:
                ApplyBlindCurtainTutorialState(0);
                break;
            case TutorialTypeBlackout:
                ApplyBlackoutTutorialState(0);
                break;
        }

        SetSpecialTutorialHandToCell(preset.focusCell, instant: true);
    }

    private void ApplyBasicTutorialTrailState(int trailPhase)
    {
        UpdateBasicTutorialTrailGeometry();

        bool leftActive = trailPhase >= 1;
        bool rightActive = trailPhase >= 2;
        bool complete = trailPhase >= 4;

        ApplyBasicTrailElement(tutorialBasicTrailLeftCenter, leftActive, complete, trailPhase == 1);
        ApplyBasicTrailElement(tutorialBasicTrailCenterRight, rightActive, complete, trailPhase == 2 || trailPhase == 3);
    }

    private static void ApplyBasicTutorialTileState(VisualElement tile, Label countLabel, int count, bool pulse)
    {
        int shownCount = Mathf.Max(0, count);
        if (countLabel != null)
        {
            countLabel.text = shownCount.ToString();

            Color countColor = GetTutorialNumberColor(shownCount);
            countLabel.style.color = new StyleColor(count > 0
                ? new Color(countColor.r, countColor.g, countColor.b, 0.34f)
                : new Color(0.62f, 0.72f, 0.78f, 0.18f));
        }

        if (tile == null)
            return;

        bool active = count > 0;
        Color displayColor = GetTutorialNumberColor(shownCount);
        tile.style.opacity = active ? 1f : 0.42f;
        tile.style.backgroundColor = active
            ? new StyleColor(new Color(displayColor.r, displayColor.g, displayColor.b, 0.14f))
            : new StyleColor(new Color(0.05f, 0.08f, 0.11f, 0.72f));

        Color borderColor = active
            ? new Color(displayColor.r, displayColor.g, displayColor.b, 0.94f)
            : new Color(0.34f, 0.47f, 0.55f, 0.58f);
        SetElementBorderColor(tile, borderColor);

        if (pulse)
        {
            tile.style.scale = new StyleScale(new Scale(new Vector3(1.14f, 1.14f, 1f)));
            tile.schedule.Execute(() =>
            {
                tile.style.scale = new StyleScale(new Scale(Vector3.one));
            }).StartingIn(130);
        }
        else
        {
            tile.style.scale = new StyleScale(new Scale(Vector3.one));
        }
    }

    private static void ApplyBasicTrailElement(VisualElement trail, bool active, bool complete, bool pulse)
    {
        if (trail == null)
            return;

        trail.style.opacity = complete ? 0.62f : active ? 0.92f : 0.16f;
        trail.style.backgroundColor = active || complete
            ? new StyleColor(new Color(0.44f, 0.92f, 1f, complete ? 0.22f : 0.32f))
            : new StyleColor(new Color(0.24f, 0.48f, 0.56f, 0.12f));
        SetElementBorderColor(trail, active || complete
            ? new Color(0.74f, 0.97f, 1f, complete ? 0.62f : 0.92f)
            : new Color(0.46f, 0.68f, 0.76f, 0.36f));

        if (pulse)
        {
            trail.style.scale = new StyleScale(new Scale(new Vector3(1f, 1.45f, 1f)));
            trail.schedule.Execute(() =>
            {
                trail.style.scale = new StyleScale(new Scale(Vector3.one));
            }).StartingIn(150);
        }
        else
        {
            trail.style.scale = new StyleScale(new Scale(Vector3.one));
        }
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
        SetElementBorderColor(tile, borderColor);

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

    private static Color GetTutorialNumberColor(int count)
    {
        if (count <= 0)
            return new Color(0.42f, 0.5f, 0.56f, 1f);

        int paletteIndex = Mathf.Clamp(count, 1, TutorialNumberPalette.Length) - 1;
        return TutorialNumberPalette[paletteIndex];
    }

    private static void SetElementBorderColor(VisualElement element, Color color)
    {
        if (element == null)
            return;

        element.style.borderLeftColor = color;
        element.style.borderRightColor = color;
        element.style.borderTopColor = color;
        element.style.borderBottomColor = color;
    }

    private void ApplyShortCircuitTutorialSpriteRotation()
    {
        if (tutorialShortCircuitTileImage == null)
            return;

        float rotationZ = GetShortCircuitSpriteRotationZ(ShortCircuitTutorialDemoDirection);
        tutorialShortCircuitTileImage.style.rotate = new StyleRotate(new Rotate(rotationZ));
    }

    private static float GetShortCircuitSpriteRotationZ(string direction)
    {
        string normalized = string.IsNullOrEmpty(direction) ? "LEFT" : direction.ToUpperInvariant();
        switch (normalized)
        {
            case "RIGHT":
                return 180f;
            case "UP":
                return -90f;
            case "DOWN":
                return 90f;
            case "LEFT":
            default:
                return 0f;
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
            SetElementBorderColor(tile, borderColor);
        }

        bool showArrow = count > 0;
        ApplyShortCircuitTileImageState(showArrow, pulseArrow);

        if (tutorialShortTileBottomLeftArrow == null)
            return;

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

    private void ApplyShortCircuitTileImageState(bool active, bool pulse)
    {
        if (tutorialShortCircuitTileImage == null)
            return;

        tutorialShortCircuitTileImage.style.opacity = active ? 0.98f : 0.34f;
        tutorialShortCircuitTileImage.style.scale = new StyleScale(
            new Scale(pulse && active ? new Vector3(1.12f, 1.12f, 1f) : Vector3.one));

        if (pulse && active)
        {
            tutorialShortCircuitTileImage.schedule.Execute(() =>
            {
                tutorialShortCircuitTileImage.style.scale = new StyleScale(new Scale(Vector3.one));
            }).StartingIn(140);
        }
    }

    private void SetTutorialHandPosition(int laneIndex, bool instant)
    {
        if (tutorialHandImage == null)
            return;

        UpdateBasicTutorialTrailGeometry();
        if (TrySetTutorialHandPositionFromTile(laneIndex, instant))
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

        if (tutorialBasicDemoBoard != null)
        {
            tutorialBasicDemoBoard.schedule.Execute(() =>
            {
                UpdateBasicTutorialTrailGeometry();
                TrySetTutorialHandPositionFromTile(laneIndex, instant);
            }).StartingIn(32);
        }
    }

    private bool TrySetTutorialHandPositionFromTile(int laneIndex, bool instant)
    {
        VisualElement targetTile = GetBasicTutorialTileByLane(laneIndex);
        if (targetTile == null || tutorialBasicDemoBoard == null || tutorialHandImage == null)
            return false;
        if (targetTile.panel == null || tutorialBasicDemoBoard.panel == null)
            return false;

        Rect boardBounds = tutorialBasicDemoBoard.worldBound;
        Rect tileBounds = targetTile.worldBound;
        if (boardBounds.width <= 0f || boardBounds.height <= 0f || tileBounds.width <= 0f || tileBounds.height <= 0f)
            return false;

        float handWidth = GetResolvedDimension(tutorialHandImage, useWidth: true, TutorialHandFallbackSize);
        float handHeight = GetResolvedDimension(tutorialHandImage, useWidth: false, TutorialHandFallbackSize);
        float left = tileBounds.center.x - boardBounds.x - handWidth * 0.5f;
        float top = tileBounds.yMax - boardBounds.y - handHeight * 0.36f;

        tutorialHandImage.style.left = Mathf.Clamp(left, 0f, Mathf.Max(0f, boardBounds.width - handWidth));
        tutorialHandImage.style.top = Mathf.Clamp(top, 0f, Mathf.Max(0f, boardBounds.height - handHeight));
        tutorialHandImage.style.opacity = 1f;
        tutorialHandImage.style.scale = new StyleScale(new Scale(instant ? Vector3.one : new Vector3(1.04f, 1.04f, 1f)));
        return true;
    }

    private VisualElement GetBasicTutorialTileByLane(int laneIndex)
    {
        switch (laneIndex)
        {
            case 0:
                return tutorialTileLeft;
            case 1:
                return tutorialTileCenter;
            case 2:
                return tutorialTileRight;
            default:
                return tutorialTileCenter;
        }
    }

    private void UpdateBasicTutorialTrailGeometry()
    {
        PositionBasicTutorialTrail(tutorialBasicTrailLeftCenter, tutorialTileLeft, tutorialTileCenter);
        PositionBasicTutorialTrail(tutorialBasicTrailCenterRight, tutorialTileCenter, tutorialTileRight);
    }

    private void PositionBasicTutorialTrail(VisualElement trail, VisualElement fromTile, VisualElement toTile)
    {
        if (trail == null || fromTile == null || toTile == null || tutorialBasicDemoBoard == null)
            return;
        if (trail.panel == null || fromTile.panel == null || toTile.panel == null || tutorialBasicDemoBoard.panel == null)
            return;

        Rect boardBounds = tutorialBasicDemoBoard.worldBound;
        Rect fromBounds = fromTile.worldBound;
        Rect toBounds = toTile.worldBound;
        if (boardBounds.width <= 0f || fromBounds.width <= 0f || toBounds.width <= 0f)
            return;

        float startX = fromBounds.center.x;
        float endX = toBounds.center.x;
        float left = Mathf.Min(startX, endX) - boardBounds.x;
        float width = Mathf.Abs(endX - startX);
        float top = ((fromBounds.center.y + toBounds.center.y) * 0.5f) - boardBounds.y - TutorialTrailHeight * 0.5f;

        trail.style.left = Mathf.Clamp(left, 0f, Mathf.Max(0f, boardBounds.width));
        trail.style.top = Mathf.Clamp(top, 0f, Mathf.Max(0f, boardBounds.height - TutorialTrailHeight));
        trail.style.width = Mathf.Max(0f, width);
        trail.style.height = TutorialTrailHeight;
    }

    private static float GetResolvedDimension(VisualElement element, bool useWidth, float fallback)
    {
        if (element == null)
            return fallback;

        float value = useWidth ? element.resolvedStyle.width : element.resolvedStyle.height;
        return float.IsNaN(value) || value <= 0f ? fallback : value;
    }

    private void SetShortCircuitTutorialHandPosition(int pointIndex, bool instant)
    {
        if (tutorialShortCircuitHandImage == null)
            return;

        UpdateShortCircuitTutorialGeometry();
        if (TrySetShortCircuitTutorialHandPositionFromTile(pointIndex, instant))
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
                leftPercent = 56f;
                top = 206f;
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

        if (tutorialShortCircuitDemoBoard != null)
        {
            tutorialShortCircuitDemoBoard.schedule.Execute(() =>
            {
                UpdateShortCircuitTutorialGeometry();
                TrySetShortCircuitTutorialHandPositionFromTile(pointIndex, instant);
            }).StartingIn(32);
        }
    }

    private bool TrySetShortCircuitTutorialHandPositionFromTile(int pointIndex, bool instant)
    {
        VisualElement targetTile = GetShortCircuitTutorialTileByPoint(pointIndex);
        if (targetTile == null || tutorialShortCircuitDemoBoard == null || tutorialShortCircuitHandImage == null)
            return false;
        if (targetTile.panel == null || tutorialShortCircuitDemoBoard.panel == null)
            return false;

        Rect boardBounds = tutorialShortCircuitDemoBoard.worldBound;
        Rect tileBounds = targetTile.worldBound;
        if (boardBounds.width <= 0f || boardBounds.height <= 0f || tileBounds.width <= 0f || tileBounds.height <= 0f)
            return false;

        float handWidth = GetResolvedDimension(tutorialShortCircuitHandImage, useWidth: true, TutorialHandFallbackSize);
        float handHeight = GetResolvedDimension(tutorialShortCircuitHandImage, useWidth: false, TutorialHandFallbackSize);
        float left = tileBounds.center.x - boardBounds.x - handWidth * 0.5f;
        float top = tileBounds.yMax - boardBounds.y - handHeight * 0.36f;

        tutorialShortCircuitHandImage.style.left = Mathf.Clamp(left, 0f, Mathf.Max(0f, boardBounds.width - handWidth));
        tutorialShortCircuitHandImage.style.top = Mathf.Clamp(top, 0f, Mathf.Max(0f, boardBounds.height - handHeight));
        tutorialShortCircuitHandImage.style.opacity = 1f;
        tutorialShortCircuitHandImage.style.scale = new StyleScale(new Scale(instant ? Vector3.one : new Vector3(1.04f, 1.04f, 1f)));
        return true;
    }

    private VisualElement GetShortCircuitTutorialTileByPoint(int pointIndex)
    {
        switch (pointIndex)
        {
            case 0:
                return tutorialShortTileBottomLeft;
            case 1:
            case 3:
                return tutorialShortTileBottomRight;
            case 2:
                return tutorialShortTileTopRight;
            default:
                return tutorialShortTileBottomLeft;
        }
    }

    private void UpdateShortCircuitTutorialGeometry()
    {
        PositionShortCircuitExitTrail();
        PositionShortCircuitBlockedEntry();
    }

    private void PositionShortCircuitExitTrail()
    {
        if (tutorialShortCircuitExitTrail == null || tutorialShortTileBottomLeft == null || tutorialShortTileBottomRight == null || tutorialShortCircuitDemoBoard == null)
            return;
        if (tutorialShortCircuitExitTrail.panel == null || tutorialShortTileBottomLeft.panel == null || tutorialShortTileBottomRight.panel == null || tutorialShortCircuitDemoBoard.panel == null)
            return;

        Rect boardBounds = tutorialShortCircuitDemoBoard.worldBound;
        Rect fromBounds = tutorialShortTileBottomLeft.worldBound;
        Rect toBounds = tutorialShortTileBottomRight.worldBound;
        if (boardBounds.width <= 0f || fromBounds.width <= 0f || toBounds.width <= 0f)
            return;

        float left = fromBounds.center.x - boardBounds.x;
        float width = toBounds.center.x - fromBounds.center.x;
        float top = ((fromBounds.center.y + toBounds.center.y) * 0.5f) - boardBounds.y - TutorialTrailHeight * 0.5f;

        tutorialShortCircuitExitTrail.style.left = Mathf.Clamp(left, 0f, Mathf.Max(0f, boardBounds.width));
        tutorialShortCircuitExitTrail.style.top = Mathf.Clamp(top, 0f, Mathf.Max(0f, boardBounds.height - TutorialTrailHeight));
        tutorialShortCircuitExitTrail.style.width = Mathf.Max(0f, width);
        tutorialShortCircuitExitTrail.style.height = TutorialTrailHeight;
    }

    private void PositionShortCircuitBlockedEntry()
    {
        if (tutorialShortCircuitBlockedEntry == null || tutorialShortTileBottomLeft == null || tutorialShortTileBottomRight == null || tutorialShortCircuitDemoBoard == null)
            return;
        if (tutorialShortCircuitBlockedEntry.panel == null || tutorialShortTileBottomLeft.panel == null || tutorialShortTileBottomRight.panel == null || tutorialShortCircuitDemoBoard.panel == null)
            return;

        Rect boardBounds = tutorialShortCircuitDemoBoard.worldBound;
        Rect fromBounds = tutorialShortTileBottomRight.worldBound;
        Rect toBounds = tutorialShortTileBottomLeft.worldBound;
        if (boardBounds.width <= 0f || fromBounds.width <= 0f || toBounds.width <= 0f)
            return;

        float centerX = (fromBounds.center.x + toBounds.center.x) * 0.5f;
        float centerY = (fromBounds.center.y + toBounds.center.y) * 0.5f;
        float left = centerX - boardBounds.x - TutorialBlockedMarkerSize * 0.5f;
        float top = centerY - boardBounds.y - TutorialBlockedMarkerSize * 0.5f;

        tutorialShortCircuitBlockedEntry.style.left = Mathf.Clamp(left, 0f, Mathf.Max(0f, boardBounds.width - TutorialBlockedMarkerSize));
        tutorialShortCircuitBlockedEntry.style.top = Mathf.Clamp(top, 0f, Mathf.Max(0f, boardBounds.height - TutorialBlockedMarkerSize));
        tutorialShortCircuitBlockedEntry.style.width = TutorialBlockedMarkerSize;
        tutorialShortCircuitBlockedEntry.style.height = TutorialBlockedMarkerSize;
    }

    private void ConfigureTutorialDemo(HelpTutorialEntryData entry)
    {
        string tutorialType = NormalizeTutorialType(entry != null ? entry.tutorialType : TutorialTypeBasicPath);
        bool isShortCircuit = string.Equals(tutorialType, TutorialTypeShortCircuit, StringComparison.OrdinalIgnoreCase);
        bool isSpecial = IsSpecialTutorialType(tutorialType);
        bool isBasic = !isShortCircuit && !isSpecial;

        if (tutorialBasicDemoBoard != null)
            tutorialBasicDemoBoard.style.display = isBasic ? DisplayStyle.Flex : DisplayStyle.None;
        if (tutorialShortCircuitDemoBoard != null)
            tutorialShortCircuitDemoBoard.style.display = isShortCircuit ? DisplayStyle.Flex : DisplayStyle.None;
        if (tutorialSpecialDemoBoard != null)
            tutorialSpecialDemoBoard.style.display = isSpecial ? DisplayStyle.Flex : DisplayStyle.None;

        if (tutorialHandImage != null && isBasic)
        {
            ApplyBasicTutorialStepState(1, 2, 1, T("tutorial_hint_connect"));
            SetTutorialHandPosition(0, instant: true);
        }

        if (tutorialShortCircuitHandImage != null)
            tutorialShortCircuitHandImage.style.opacity = isShortCircuit ? 1f : 0f;
        if (tutorialSpecialHandImage != null)
            tutorialSpecialHandImage.style.opacity = isSpecial ? 1f : 0f;
        if (tutorialHandImage != null && !isBasic)
            tutorialHandImage.style.opacity = 0f;

        if (isSpecial)
        {
            ConfigureSpecialTutorialDemo(tutorialType);
            return;
        }

        if (!isShortCircuit)
        {
            ResetSpecialTutorialBoard();
            return;
        }

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
        ApplyTutorialDescriptionText(activeTutorialEntry);
        if (tutorialConfirmButton != null)
            tutorialConfirmButton.text = ResolveTutorialCloseButtonText(activeTutorialEntry);
        ApplyTutorialInstructionText(activeTutorialEntry);
        RefreshTutorialNavigation();
    }

    private void ApplyTutorialDescriptionText(HelpTutorialEntryData entry)
    {
        string description = ResolveTutorialDescription(entry);
        if (tutorialDescriptionLabel == null)
            return;

        bool hasDescription = !string.IsNullOrWhiteSpace(description);
        tutorialDescriptionLabel.text = hasDescription ? description : string.Empty;
        tutorialDescriptionLabel.style.display = hasDescription ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void ApplyTutorialInstructionText(HelpTutorialEntryData entry)
    {
        string instructionText = ResolveTutorialInstructionText(entry);
        hasStaticTutorialInstructionText = !string.IsNullOrWhiteSpace(instructionText);

        if (tutorialStepHintLabel != null)
            tutorialStepHintLabel.text = hasStaticTutorialInstructionText ? instructionText : string.Empty;
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
        {
            if (string.Equals(entry.descriptionKey, "tutorial_basic_description", StringComparison.Ordinal))
                return string.Empty;
            return T(entry.descriptionKey);
        }
        if (!string.IsNullOrWhiteSpace(entry.description))
            return entry.description;
        if (string.Equals(NormalizeTutorialType(entry.tutorialType), TutorialTypeBasicPath, StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return T("help_generic_description");
    }

    private string ResolveTutorialInstructionText(HelpTutorialEntryData entry)
    {
        if (entry == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(entry.instructionTextKey))
            return T(entry.instructionTextKey);
        if (!string.IsNullOrWhiteSpace(entry.instructionText))
            return entry.instructionText;
        return string.Empty;
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
        float safeTopPx = Mathf.Max(0f, Screen.height - currentSafeArea.yMax);
        float totalReservedBottomPx = safeBottomPx + Mathf.Max(0f, reservedBannerHeightPx);

        if (topBar != null)
            topBar.style.paddingTop = TopHudBasePaddingPx + safeTopPx;
        if (topHudBackdrop != null)
            topHudBackdrop.style.top = TopHudBackdropBaseTopPx + safeTopPx;

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
