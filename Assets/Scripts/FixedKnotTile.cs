using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// 고정 매듭(FixedKnot) 타일: 반드시 targetOrder 번째 스텝에만 진입 가능.
/// 전용 타일 스프라이트와 순서 숫자만 표시한다.
/// </summary>
[RequireComponent(typeof(Tile))]
public class FixedKnotTile : MonoBehaviour
{
    [Header("스프라이트")]
    [Tooltip("Resources 경로 (Assets/Resources/Sprites/fixed_knot_tile.png → Sprites/fixed_knot_tile)")]
    [SerializeField] private string lockedSpritePath = "Sprites/fixed_knot_tile";
    [Tooltip("잠금 해제 시 페이드아웃 시간(초)")]
    [SerializeField] private float unlockFadeDuration = 0.2f;

    [Header("순서 숫자")]
    [Tooltip("타일 중앙 순서 숫자 폰트 크기")]
    [SerializeField] private float orderFontSize = 14f;
    [Tooltip("타일 중앙 순서 숫자 로컬 스케일")]
    [SerializeField] private float orderTextScale = 1.2f;

    [Header("잠금 연출")]
    [Tooltip("대기 잠금 pulse 시간(초)")]
    [SerializeField] private float lockedPulseDuration = 1.15f;
    [Tooltip("차례 직전 ready pulse 시간(초)")]
    [SerializeField] private float readyPulseDuration = 0.65f;
    [Tooltip("잠금 해제 burst 시간(초)")]
    [SerializeField] private float unlockBurstDuration = 0.24f;

    private Tile tile;
    private SpriteRenderer tileSpriteRenderer;
    private SpriteRenderer accentRenderer;
    private TMP_Text orderText;
    private int targetOrderValue;
    private bool isAbsoluteValue;
    private Sequence statePulseSequence;
    private Sequence correctEntrySequence;
    private Sequence solvedLeaveSequence;
    private Sequence deniedColorSequence;
    private Tween positionShakeTween;
    private int displayRemaining = 99;
    private Sprite defaultSprite;
    private Sprite lockedSprite;
    private Vector3 baseLocalPosition;
    private bool hasBaseLocalPosition;
    private bool hasBeenSteppedCorrectly;
    private FixedKnotVisualState visualState = FixedKnotVisualState.None;

    private static readonly Color LockedFutureColor = new Color(1.2f, 0.42f, 0.42f, 1f);
    private static readonly Color ReadyNowColor = new Color(0.49f, 1.4f, 0.63f, 1f);
    private static readonly Color EnteredCorrectColor = Color.white;
    private static readonly Color DeniedColor = new Color(1f, 0.25f, 0.25f, 1f);

    private enum FixedKnotVisualState
    {
        None,
        LockedFuture,
        ReadyNow,
        EnteredCorrect,
        SolvedWaitingForLeave,
        Denied,
        SolvedLeaving
    }

    /// <summary>반드시 이 스텝 수에만 진입 가능 (1-based).</summary>
    public int TargetOrder => targetOrderValue;
    /// <summary>순서가 틀리면 진입 불가 후 게임오버(암전·리셋).</summary>
    public bool IsAbsolute => isAbsoluteValue;
    public int CurrentRequiredOrder => Mathf.Max(1, displayRemaining);

    /// <summary>스테이지 데이터 기반 초기화. 그리드 생성 시 GameManager가 호출.</summary>
    public void Setup(int targetOrder, bool isAbsolute)
    {
        targetOrderValue = Mathf.Max(1, targetOrder);
        isAbsoluteValue = isAbsolute;
        hasBeenSteppedCorrectly = false;
        EnsureOrderText();
        ApplyLockedVisual();
        HideTileNumberText();
        UpdateVisual(0);
    }

    private void Awake()
    {
        tile = GetComponent<Tile>();
        tileSpriteRenderer = GetComponent<SpriteRenderer>();
        defaultSprite = tileSpriteRenderer != null ? tileSpriteRenderer.sprite : null;
    }

    private void Start()
    {
        CacheBaseLocalPosition();
    }

    private TMP_Text GetTileNumberText()
    {
        return tile != null ? tile.GetNumberText() : null;
    }

    private void HideTileNumberText()
    {
        TMP_Text tileNumberText = GetTileNumberText();
        if (tileNumberText != null)
            tileNumberText.gameObject.SetActive(false);
    }

    private void EnsureOrderText()
    {
        if (orderText != null)
            return;

        GameObject numberObj = new GameObject("OrderNumber");
        numberObj.transform.SetParent(transform);
        numberObj.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        numberObj.transform.localRotation = Quaternion.identity;
        numberObj.transform.localScale = Vector3.one * orderTextScale;

        orderText = numberObj.AddComponent<TextMeshPro>();
        orderText.text = targetOrderValue.ToString();
        orderText.fontSize = orderFontSize;
        orderText.alignment = TextAlignmentOptions.Center;

        var numberRenderer = numberObj.GetComponent<Renderer>();
        if (numberRenderer != null)
            numberRenderer.sortingOrder = 2;

        TMP_Text tileNumberText = GetTileNumberText();
        if (tileNumberText != null && tileNumberText.font != null)
            orderText.font = tileNumberText.font;
        else
        {
            var defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (defaultFont != null)
                orderText.font = defaultFont;
        }
    }

    private bool EnsureLockedSprite()
    {
        if (lockedSprite == null)
            lockedSprite = Resources.Load<Sprite>(lockedSpritePath);

        if (lockedSprite == null)
        {
            Debug.LogWarning($"[FixedKnotTile] Resources/{lockedSpritePath} 을(를) 찾을 수 없습니다.");
            return false;
        }

        return true;
    }

    private void ApplyLockedSprite()
    {
        if (tileSpriteRenderer == null || !EnsureLockedSprite())
            return;

        tileSpriteRenderer.sprite = lockedSprite;
        if (accentRenderer != null)
            accentRenderer.sprite = lockedSprite;
    }

    private void RestoreDefaultSprite()
    {
        if (tileSpriteRenderer != null && defaultSprite != null)
            tileSpriteRenderer.sprite = defaultSprite;
    }

    private void ApplyLockedVisual()
    {
        StopVisualTweens(false);
        ApplyLockedSprite();
        if (tileSpriteRenderer != null)
        {
            tileSpriteRenderer.enabled = true;
            tileSpriteRenderer.color = LockedFutureColor;
        }

        if (orderText != null)
        {
            orderText.gameObject.SetActive(true);
            orderText.text = targetOrderValue.ToString();
            ApplyOrderTextColor(LockedFutureColor);
            orderText.alpha = 1f;
            orderText.transform.localScale = Vector3.one * orderTextScale;
        }

        HideAccentVisual();
        visualState = FixedKnotVisualState.None;
    }

    private void ApplyOrderTextColor(Color color)
    {
        if (orderText == null)
            return;

        orderText.color = color;

        Material instanceMat = orderText.fontMaterial;
        if (instanceMat == null)
            return;

        if (instanceMat.HasProperty(ShaderUtilities.ID_FaceColor))
            instanceMat.SetColor(ShaderUtilities.ID_FaceColor, color);
        if (instanceMat.HasProperty(ShaderUtilities.ID_OutlineColor))
            instanceMat.SetColor(ShaderUtilities.ID_OutlineColor, color);
        if (instanceMat.HasProperty(ShaderUtilities.ID_UnderlayColor))
            instanceMat.SetColor(ShaderUtilities.ID_UnderlayColor, color);
        if (instanceMat.HasProperty(ShaderUtilities.ID_GlowColor))
            instanceMat.SetColor(ShaderUtilities.ID_GlowColor, color);
    }

    private void EnsureAccentVisual()
    {
        if (accentRenderer != null)
            return;

        if (!EnsureLockedSprite())
            return;

        GameObject accentObj = new GameObject("FixedKnotAccent");
        accentObj.transform.SetParent(transform);
        accentObj.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        accentObj.transform.localRotation = Quaternion.identity;
        accentObj.transform.localScale = Vector3.one * 1.08f;

        accentRenderer = accentObj.AddComponent<SpriteRenderer>();
        accentRenderer.sprite = lockedSprite;
        accentRenderer.enabled = false;
        accentRenderer.color = WithAlpha(LockedFutureColor, 0f);

        if (tileSpriteRenderer != null)
        {
            accentRenderer.sortingLayerID = tileSpriteRenderer.sortingLayerID;
            accentRenderer.sortingOrder = tileSpriteRenderer.sortingOrder - 1;
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private void ApplySpriteColor(Color color)
    {
        if (tileSpriteRenderer != null)
            tileSpriteRenderer.color = color;
    }

    private void ApplyOrderBaseline(Color color, bool shouldShow)
    {
        if (orderText == null)
            return;

        orderText.gameObject.SetActive(shouldShow);
        orderText.alpha = shouldShow ? 1f : 0f;
        orderText.transform.localScale = Vector3.one * orderTextScale;
        if (shouldShow)
            ApplyOrderTextColor(color);
    }

    private void SyncOrderVisibilityFromTileState()
    {
        if (orderText == null)
            return;

        bool shouldShow = ShouldShowOrderTextForCurrentState();
        if (orderText.gameObject.activeSelf != shouldShow)
            orderText.gameObject.SetActive(shouldShow);
    }

    private bool ShouldShowOrderTextForCurrentState()
    {
        if (tile == null || !tile.IsActive || displayRemaining <= 0 || IsSolvedState())
            return false;

        return visualState == FixedKnotVisualState.LockedFuture ||
               visualState == FixedKnotVisualState.ReadyNow ||
               visualState == FixedKnotVisualState.Denied;
    }

    private void ApplyAccentBaseline(Color color, float alpha, float scale)
    {
        EnsureAccentVisual();
        if (accentRenderer == null)
            return;

        accentRenderer.sprite = lockedSprite;
        accentRenderer.enabled = alpha > 0f;
        accentRenderer.color = WithAlpha(color, alpha);
        accentRenderer.transform.localScale = Vector3.one * scale;
    }

    private void HideAccentVisual()
    {
        if (accentRenderer == null)
            return;

        accentRenderer.enabled = false;
        accentRenderer.color = WithAlpha(accentRenderer.color, 0f);
        accentRenderer.transform.localScale = Vector3.one * 1.08f;
    }

    private void KillSequence(ref Sequence sequence)
    {
        if (sequence != null && sequence.IsActive())
            sequence.Kill();
        sequence = null;
    }

    private void StopVisualTweens(bool includeSolvedLeave)
    {
        bool preserveSolvedLeave = !includeSolvedLeave && solvedLeaveSequence != null && solvedLeaveSequence.IsActive();

        KillSequence(ref statePulseSequence);
        KillSequence(ref correctEntrySequence);
        KillSequence(ref deniedColorSequence);
        if (includeSolvedLeave)
            KillSequence(ref solvedLeaveSequence);

        if (preserveSolvedLeave)
            return;

        if (tileSpriteRenderer != null)
            tileSpriteRenderer.DOKill();
        if (orderText != null)
        {
            orderText.DOKill();
            orderText.transform.DOKill();
        }
        if (accentRenderer != null)
        {
            accentRenderer.DOKill();
            accentRenderer.transform.DOKill();
        }
    }

    private void ApplyVisualForCurrentState(bool force = false)
    {
        if (!force && visualState == FixedKnotVisualState.SolvedLeaving && solvedLeaveSequence != null && solvedLeaveSequence.IsActive())
            return;

        if (IsSolvedState())
        {
            ApplySolvedWaitingVisual(force);
            return;
        }

        if (hasBeenSteppedCorrectly || displayRemaining <= 0)
        {
            ApplyEnteredCorrectVisual(force);
            return;
        }

        if (displayRemaining == 1)
        {
            ApplyReadyNowVisual(force);
            return;
        }

        ApplyLockedFutureVisual(force);
    }

    private void ApplyLockedFutureVisual(bool force)
    {
        if (!force && visualState == FixedKnotVisualState.LockedFuture)
            return;

        StopVisualTweens(false);
        visualState = FixedKnotVisualState.LockedFuture;
        ApplyLockedSprite();
        ApplySpriteColor(LockedFutureColor);
        ApplyOrderBaseline(LockedFutureColor, tile != null && tile.IsActive && displayRemaining > 0);
        ApplyAccentBaseline(LockedFutureColor, 0.08f, 1.04f);
        StartPulseLoop(LockedFutureColor, 0.05f, 0.12f, 1.04f, 1.1f, lockedPulseDuration, false);
    }

    private void ApplyReadyNowVisual(bool force)
    {
        if (!force && visualState == FixedKnotVisualState.ReadyNow)
            return;

        StopVisualTweens(false);
        visualState = FixedKnotVisualState.ReadyNow;
        ApplyLockedSprite();
        ApplySpriteColor(ReadyNowColor);
        ApplyOrderBaseline(ReadyNowColor, tile != null && tile.IsActive && displayRemaining > 0);
        ApplyAccentBaseline(ReadyNowColor, 0.16f, 1.08f);
        StartPulseLoop(ReadyNowColor, 0.14f, 0.34f, 1.08f, 1.2f, readyPulseDuration, true);
    }

    private void ApplyEnteredCorrectVisual(bool force)
    {
        if (!force && visualState == FixedKnotVisualState.EnteredCorrect)
            return;

        StopVisualTweens(false);
        visualState = FixedKnotVisualState.EnteredCorrect;
        ApplyEnteredCorrectBaseline();
        HideAccentVisual();
    }

    private void ApplySolvedWaitingVisual(bool force)
    {
        if (!force && visualState == FixedKnotVisualState.SolvedWaitingForLeave)
            return;

        StopVisualTweens(false);
        visualState = FixedKnotVisualState.SolvedWaitingForLeave;
        RestoreDefaultSprite();
        ApplySpriteColor(Color.white);
        if (tileSpriteRenderer != null && tile != null && !tile.IsActive)
            tileSpriteRenderer.enabled = false;
        ApplyOrderBaseline(Color.white, false);
        HideAccentVisual();
    }

    private void StartPulseLoop(Color color, float minAlpha, float maxAlpha, float baseScale, float targetScale, float duration, bool pulseText)
    {
        EnsureAccentVisual();
        if (accentRenderer == null)
            return;

        KillSequence(ref statePulseSequence);
        accentRenderer.enabled = true;
        accentRenderer.color = WithAlpha(color, minAlpha);
        accentRenderer.transform.localScale = Vector3.one * baseScale;

        statePulseSequence = DOTween.Sequence().SetUpdate(true).SetLoops(-1, LoopType.Yoyo);
        statePulseSequence.Join(accentRenderer.DOFade(maxAlpha, duration).SetEase(Ease.InOutSine));
        statePulseSequence.Join(accentRenderer.transform.DOScale(Vector3.one * targetScale, duration).SetEase(Ease.InOutSine));

        if (pulseText && orderText != null && orderText.gameObject.activeSelf)
            statePulseSequence.Join(orderText.transform.DOScale(Vector3.one * orderTextScale * 1.08f, duration).SetEase(Ease.InOutSine));
    }

    private void PlayCorrectEntryFeedback()
    {
        StopVisualTweens(false);
        visualState = FixedKnotVisualState.EnteredCorrect;
        ApplyEnteredCorrectBaseline();
        ApplyAccentBaseline(ReadyNowColor, 0.32f, 1.08f);

        if (accentRenderer == null)
            return;

        correctEntrySequence = DOTween.Sequence().SetUpdate(true);
        correctEntrySequence.Join(accentRenderer.DOFade(0f, 0.18f).SetEase(Ease.OutQuad));
        correctEntrySequence.Join(accentRenderer.transform.DOScale(Vector3.one * 1.24f, 0.18f).SetEase(Ease.OutQuad));
        correctEntrySequence.OnComplete(() =>
        {
            HideAccentVisual();
            correctEntrySequence = null;
        });
    }

    /// <summary>
    /// GameManager 단일 경로 리스트 Count만 참조. 표시 = targetOrder - totalPathCount.
    /// 1 = 다음에 진입 가능, >1 = 아직, 0 = 이미 지남.
    /// </summary>
    public void UpdateVisual(int totalPathCount)
    {
        int remaining = Mathf.Max(0, targetOrderValue - totalPathCount);
        displayRemaining = remaining;

        if (orderText != null)
            orderText.text = remaining.ToString();

        ApplyVisualForCurrentState();
    }

    public void RefreshVisualState()
    {
        HideTileNumberText();
        ApplyVisualForCurrentState(true);
    }

    /// <summary>totalPathCount가 targetOrder에 도달했거나 넘었는데 아직 밟지 않았으면 건너뛴 것.</summary>
    public bool IsMissedAtStepCount(int totalPathCount)
    {
        if (tile == null || !tile.IsActive)
            return false;
        if (hasBeenSteppedCorrectly)
            return false;
        return totalPathCount >= targetOrderValue;
    }

    /// <summary>진입 허용: (targetOrder - totalPathCount) == 1 일 때만.</summary>
    public bool CanEnterTile(int totalPathCount)
    {
        return (targetOrderValue - totalPathCount) == 1;
    }

    /// <summary>다음 스텝 번호(1-based)가 targetOrder와 일치할 때만 진입 허용.</summary>
    public bool CanEnter(int nextStepNumber)
    {
        return nextStepNumber == targetOrderValue;
    }

    /// <summary>풀린 상태: count 0.</summary>
    private bool IsSolvedState()
    {
        return tile != null && tile.CurrentNumber == 0;
    }

    /// <summary>정확한 순서에 밟았을 때 호출.</summary>
    public void OnSteppedCorrectly()
    {
        hasBeenSteppedCorrectly = true;
        PlayCorrectEntryFeedback();
    }

    /// <summary>타일을 밟은 뒤 다음 타일로 떠날 때 GameManager가 호출.</summary>
    public void OnLeftByPlayer()
    {
        if (tile == null)
            return;

        if (tile.CurrentNumber == 0)
            PlaySolvedLeavingFeedback();
    }

    private void PlaySolvedLeavingFeedback()
    {
        if (visualState == FixedKnotVisualState.SolvedLeaving && solvedLeaveSequence != null && solvedLeaveSequence.IsActive())
            return;

        StopVisualTweens(true);
        visualState = FixedKnotVisualState.SolvedLeaving;
        ApplyEnteredCorrectBaseline();
        if (tileSpriteRenderer != null)
            tileSpriteRenderer.enabled = true;
        ApplyAccentBaseline(ReadyNowColor, 0.28f, 1.08f);

        solvedLeaveSequence = DOTween.Sequence().SetUpdate(true);
        if (tileSpriteRenderer != null)
            solvedLeaveSequence.Join(tileSpriteRenderer.DOFade(0f, unlockFadeDuration).SetEase(Ease.OutQuad));
        if (orderText != null)
            solvedLeaveSequence.Join(orderText.DOFade(0f, unlockFadeDuration).SetEase(Ease.OutQuad));
        if (accentRenderer != null)
        {
            solvedLeaveSequence.Join(accentRenderer.DOFade(0f, unlockBurstDuration).SetEase(Ease.OutQuad));
            solvedLeaveSequence.Join(accentRenderer.transform.DOScale(Vector3.one * 1.34f, unlockBurstDuration).SetEase(Ease.OutQuad));
        }

        solvedLeaveSequence.OnComplete(() =>
        {
            RestoreDefaultSprite();
            ApplySpriteColor(Color.white);
            if (tileSpriteRenderer != null && tile != null && !tile.IsActive)
                tileSpriteRenderer.enabled = false;
            ApplyOrderBaseline(Color.white, false);
            HideAccentVisual();
            solvedLeaveSequence = null;
        });
    }

    /// <summary>잘못된 순서로 진입 시도 시 타일을 붉게 흔들어 피드백.</summary>
    public void PlayWrongOrderShake()
    {
        if (positionShakeTween != null && positionShakeTween.IsActive())
            return;

        StopVisualTweens(false);
        visualState = FixedKnotVisualState.Denied;

        float duration = 0.35f;
        float strength = 0.14f;
        PlayPositionShake(duration, strength, 18);
        if (tileSpriteRenderer != null)
        {
            Color baseline = GetBaselineColorForCurrentState();
            Color flash = WithAlpha(DeniedColor, baseline.a);
            ApplySpriteColor(flash);
            if (orderText != null && orderText.gameObject.activeSelf)
                ApplyOrderTextColor(flash);

            deniedColorSequence = DOTween.Sequence().SetUpdate(true);
            deniedColorSequence.AppendInterval(0.04f);
            deniedColorSequence.Append(DOVirtual.Color(flash, baseline, 0.24f, color =>
            {
                ApplySpriteColor(color);
                if (orderText != null && orderText.gameObject.activeSelf)
                    ApplyOrderTextColor(color);
            }).SetEase(Ease.OutQuad));
            deniedColorSequence.OnComplete(() =>
            {
                deniedColorSequence = null;
                ApplyVisualForCurrentState(true);
            });
        }
    }

    private Color GetBaselineColorForCurrentState()
    {
        if (hasBeenSteppedCorrectly || displayRemaining <= 0)
            return EnteredCorrectColor;
        if (displayRemaining == 1)
            return ReadyNowColor;
        return LockedFutureColor;
    }

    private void ApplyEnteredCorrectBaseline()
    {
        ApplyLockedSprite();
        ApplySpriteColor(EnteredCorrectColor);
        ApplyOrderBaseline(EnteredCorrectColor, false);
    }

    private void CacheBaseLocalPosition()
    {
        if (hasBaseLocalPosition)
            return;

        baseLocalPosition = transform.localPosition;
        hasBaseLocalPosition = true;
    }

    private void PlayPositionShake(float duration, float strength, int vibrato)
    {
        CacheBaseLocalPosition();

        if (positionShakeTween != null && positionShakeTween.IsActive())
            positionShakeTween.Kill();

        transform.localPosition = baseLocalPosition;
        positionShakeTween = transform
            .DOShakePosition(duration, strength, vibrato, 90f, false, true)
            .SetUpdate(true)
            .OnKill(() =>
            {
                transform.localPosition = baseLocalPosition;
                positionShakeTween = null;
            })
            .OnComplete(() =>
            {
                transform.localPosition = baseLocalPosition;
                positionShakeTween = null;
            });
    }

    /// <summary>게임오버 리셋 시 잠금 타일과 순서 숫자를 다시 표시.</summary>
    public void ResetGearVisibility()
    {
        hasBeenSteppedCorrectly = false;
        displayRemaining = targetOrderValue;
        StopVisualTweens(true);
        if (positionShakeTween != null && positionShakeTween.IsActive())
            positionShakeTween.Kill();

        ApplyLockedVisual();

        if (orderText != null)
        {
            orderText.gameObject.SetActive(true);
            orderText.text = targetOrderValue.ToString();
        }
        HideTileNumberText();

        ApplyVisualForCurrentState(true);
    }

    private void LateUpdate()
    {
        HideTileNumberText();

        SyncOrderVisibilityFromTileState();

        if (visualState == FixedKnotVisualState.None)
            ApplyVisualForCurrentState(true);
    }

    private void OnDestroy()
    {
        StopVisualTweens(true);
        if (positionShakeTween != null && positionShakeTween.IsActive())
            positionShakeTween.Kill();
        if (orderText != null)
            Destroy(orderText.gameObject);
        if (accentRenderer != null)
            Destroy(accentRenderer.gameObject);
    }
}
