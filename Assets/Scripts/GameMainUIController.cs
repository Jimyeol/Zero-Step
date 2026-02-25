using System;
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

    [Header("UI Toolkit 참조 (자동 캐싱)")]
    [SerializeField] private UIDocument uiDocument;

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
    private VisualElement bannerAdContainer;
    private Label bannerAdPlaceholder;
    private bool isSoundOn = true;
    private bool isVibrationOn = true;
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
    private BannerView bannerView;
#endif
    [Header("ProgressBar Animation")]
    [SerializeField] private float progressAnimDuration = 0.25f;
    private float displayedProgressValue;
    private Coroutine progressAnimRoutine;
    private readonly Dictionary<VisualElement, int> buttonPressAnimationVersion = new Dictionary<VisualElement, int>();

    /// <summary>현재 스테이지 시작 시 전체 타일 카운트(합).</summary>
    private int initialTileCount;

    private void Awake()
    {
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
        bannerAdContainer = root.Q<VisualElement>("BannerAdContainer");
        bannerAdPlaceholder = root.Q<Label>("BannerAdPlaceholder");

        if (settingPopupOverlay != null)
            settingPopupOverlay.style.display = DisplayStyle.None;
        if (resetConfirmOverlay != null)
            resetConfirmOverlay.style.display = DisplayStyle.None;

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
        AssignSprite(soundIcon, "Sprites/sound", "sound.png");
        AssignSprite(vibrationIcon, "Sprites/vibrate", "vibrate.png");
        AssignSprite(helpIcon, "Sprites/help", "help.png");
        AssignSprite(languageIcon, "Sprites/global", "global.png");

        RefreshSoundSwitchVisual();
        RefreshVibrationSwitchVisual();
        ApplySoundSwitchToAudioListener();

        if (soundSwitchButton != null)
            soundSwitchButton.clicked += ToggleSoundSwitch;
        if (vibrationSwitchButton != null)
            vibrationSwitchButton.clicked += ToggleVibrationSwitch;

        if (helpButton != null)
            helpButton.clicked += () => Debug.Log("도움말 열기");
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
        InitializeBannerAd();
    }

    private void OnDestroy()
    {
        DestroyBannerAd();
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
        RegisterButtonClickAnimation(resetConfirmCancelButton);
        RegisterButtonClickAnimation(resetConfirmOkButton, useWarmPulse: true);
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
        SetBannerPlaceholderText(Debug.isDebugBuild ? "TEST AD LOADING..." : "AD LOADING...");
        MobileAds.Initialize(_ => LoadBannerAd());
#else
        SetBannerPlaceholderText("BANNER");
#endif
    }

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
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
        bannerView = new BannerView(adUnitId, adSize, AdPosition.Bottom);
        bannerView.OnBannerAdLoaded += HandleBannerAdLoaded;
        bannerView.OnBannerAdLoadFailed += HandleBannerAdLoadFailed;
        bannerView.LoadAd(new AdRequest());

        Debug.Log($"[GameMainUIController] Banner ad load requested. unitId={adUnitId}, testMode={(Debug.isDebugBuild ? 1 : 0)}");
    }

    private static string ResolveBannerAdUnitId()
    {
        bool useTestAdUnit = Debug.isDebugBuild;
#if UNITY_ANDROID
        return useTestAdUnit ? AndroidTestBannerAdUnitId : AndroidReleaseBannerAdUnitId;
#elif UNITY_IOS
        return useTestAdUnit ? IOSTestBannerAdUnitId : IOSReleaseBannerAdUnitId;
#else
        return string.Empty;
#endif
    }

    private void HandleBannerAdLoaded()
    {
        if (bannerView != null)
            bannerView.Show();

        if (bannerAdContainer != null)
            bannerAdContainer.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));

        if (bannerAdPlaceholder != null)
            bannerAdPlaceholder.style.display = DisplayStyle.None;
    }

    private void HandleBannerAdLoadFailed(LoadAdError loadError)
    {
        string errorMessage = loadError != null ? loadError.GetMessage() : "unknown";
        Debug.LogWarning($"[GameMainUIController] Banner ad load failed: {errorMessage}");
        SetBannerPlaceholderText("AD LOAD FAILED");
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
#endif
    }

    /// <summary>스테이지 번호 및 전체 카운트로 상단 UI 초기화.</summary>
    public void SetupStage(int stageIndex, int totalCount, int remainingCount)
    {
        // STAGE / 스테이지 번호 텍스트 갱신
        if (stageTitleLabel != null)
            stageTitleLabel.text = "STAGE";

        if (stageNumberLabel != null)
            stageNumberLabel.text = stageIndex.ToString("D2");

        initialTileCount = Mathf.Max(1, totalCount);
        UpdateProgress(remainingCount);
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
