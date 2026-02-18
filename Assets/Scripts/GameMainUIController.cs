using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit 기반 게임 상단 UI 컨트롤러.
/// STAGE 텍스트와 진행도 ProgressBar를 GameManager와 연동한다.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameMainUIController : MonoBehaviour
{
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
    private bool isSettingPopupOpen;

    /// <summary>현재 스테이지 시작 시 전체 타일 카운트(합).</summary>
    private int initialTileCount;

    private void Awake()
    {
        // UIDocument 자동 캐싱
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogWarning("[GameMainUIController] UIDocument를 찾을 수 없습니다.");
            return;
        }

        VisualElement root = uiDocument.rootVisualElement;
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

        if (settingPopupOverlay != null)
            settingPopupOverlay.style.display = DisplayStyle.None;

        if (gameProgressBar != null)
        {
            // 기본값 초기화
            gameProgressBar.lowValue = 0f;
            gameProgressBar.highValue = 1f;
            gameProgressBar.value = 0f;
            gameProgressBar.title = string.Empty;
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
            skipButton.clicked += () => Debug.Log("스테이지 스킵됨");
        if (resetButton != null)
            resetButton.clicked += OnResetClicked;
        if (blockAdsButton != null)
            blockAdsButton.clicked += () => Debug.Log("광고 제거");
    }

    /// <summary>리셋 버튼 클릭: 현재 스테이지를 초기 상태로 복원.</summary>
    private void OnResetClicked()
    {
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
        }
    }

    private void HideSettingPopup()
    {
        if (settingPopupOverlay != null)
        {
            settingPopupOverlay.style.display = DisplayStyle.None;
            isSettingPopupOpen = false;
        }
    }

    /// <summary>설정 팝업이 열려 있으면 게임 입력 차단에 사용.</summary>
    public bool IsSettingPopupOpen => isSettingPopupOpen;

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

        int used = Mathf.Clamp(initialTileCount - remainingCount, 0, initialTileCount);
        gameProgressBar.lowValue = 0f;
        gameProgressBar.highValue = initialTileCount;
        gameProgressBar.value = used;

        // 제목에는 사용 수/전체 수와 퍼센트 표시
        float percent = initialTileCount > 0 ? (float)used / initialTileCount * 100f : 0f;
        gameProgressBar.title = $"{used}/{initialTileCount} ({percent:0}%)";
    }
}
