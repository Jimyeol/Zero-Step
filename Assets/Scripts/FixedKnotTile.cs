using System.Collections;
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
    [Tooltip("잠금 해제 시 흔들림 강도")]
    [SerializeField] private float unlockShakeStrength = 0.2f;
    [Tooltip("잠금 해제 시 페이드아웃 시간(초)")]
    [SerializeField] private float unlockFadeDuration = 0.2f;

    [Header("순서 숫자")]
    [Tooltip("타일 중앙 순서 숫자 폰트 크기")]
    [SerializeField] private float orderFontSize = 14f;
    [Tooltip("타일 중앙 순서 숫자 로컬 스케일")]
    [SerializeField] private float orderTextScale = 1.2f;

    private Tile tile;
    private SpriteRenderer tileSpriteRenderer;
    private TMP_Text orderText;
    private int targetOrderValue;
    private bool isAbsoluteValue;
    private Tween fadeTween;
    private int displayRemaining = 99;
    private Sprite defaultSprite;
    private Sprite lockedSprite;

    /// <summary>반드시 이 스텝 수에만 진입 가능 (1-based).</summary>
    public int TargetOrder => targetOrderValue;
    /// <summary>순서가 틀리면 진입 불가 후 게임오버(암전·리셋).</summary>
    public bool IsAbsolute => isAbsoluteValue;

    /// <summary>스테이지 데이터 기반 초기화. 그리드 생성 시 GameManager가 호출.</summary>
    public void Setup(int targetOrder, bool isAbsolute)
    {
        targetOrderValue = Mathf.Max(1, targetOrder);
        isAbsoluteValue = isAbsolute;
        EnsureOrderText();
        ApplyLockedVisual();
        if (tile != null && tile.GetNumberText() != null)
            tile.GetNumberText().gameObject.SetActive(false);
        UpdateVisual(0);
    }

    private void Awake()
    {
        tile = GetComponent<Tile>();
        tileSpriteRenderer = GetComponent<SpriteRenderer>();
        defaultSprite = tileSpriteRenderer != null ? tileSpriteRenderer.sprite : null;
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

        if (tile != null && tile.GetNumberText() != null && tile.GetNumberText().font != null)
            orderText.font = tile.GetNumberText().font;
        else
        {
            var defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (defaultFont != null)
                orderText.font = defaultFont;
        }
    }

    private void ApplyLockedSprite()
    {
        if (tileSpriteRenderer == null)
            return;

        if (lockedSprite == null)
            lockedSprite = Resources.Load<Sprite>(lockedSpritePath);

        if (lockedSprite == null)
        {
            Debug.LogWarning($"[FixedKnotTile] Resources/{lockedSpritePath} 을(를) 찾을 수 없습니다.");
            return;
        }

        tileSpriteRenderer.sprite = lockedSprite;
    }

    private void RestoreDefaultSprite()
    {
        if (tileSpriteRenderer != null && defaultSprite != null)
            tileSpriteRenderer.sprite = defaultSprite;
    }

    private void ApplyLockedVisual()
    {
        ApplyLockedSprite();
        if (tileSpriteRenderer != null)
        {
            tileSpriteRenderer.enabled = true;
            Color spriteColor = tileSpriteRenderer.color;
            spriteColor.a = 1f;
            tileSpriteRenderer.color = spriteColor;
        }

        if (orderText != null)
        {
            orderText.gameObject.SetActive(true);
            orderText.text = targetOrderValue.ToString();
            ApplyOrderTextColor(new Color(1f, 0.35f, 0.35f, 1f) * 1.2f);
            orderText.alpha = 1f;
            orderText.transform.localScale = Vector3.one * orderTextScale;
        }
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

    /// <summary>
    /// GameManager 단일 경로 리스트 Count만 참조. 표시 = targetOrder - totalPathCount.
    /// 1 = 다음에 진입 가능, >1 = 아직, 0 = 이미 지남.
    /// </summary>
    public void UpdateVisual(int totalPathCount)
    {
        int remaining = Mathf.Max(0, targetOrderValue - totalPathCount);
        displayRemaining = remaining;

        if (orderText != null)
        {
            orderText.text = remaining.ToString();
            orderText.gameObject.SetActive(!IsSolvedState() && remaining > 0);
        }
    }

    /// <summary>totalPathCount가 targetOrder에 도달했거나 넘었는데 아직 밟지 않았으면 건너뛴 것.</summary>
    public bool IsMissedAtStepCount(int totalPathCount)
    {
        if (tile == null || !tile.IsActive)
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
        transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 4, 0.5f).SetUpdate(true);
    }

    /// <summary>타일을 밟은 뒤 다음 타일로 떠날 때 GameManager가 호출.</summary>
    public void OnLeftByPlayer()
    {
        if (tile == null)
            return;

        if (tile.CurrentNumber == 0)
            StartCoroutine(FadeOutLockedVisualRoutine());
    }

    private IEnumerator FadeOutLockedVisualRoutine()
    {
        if (fadeTween != null && fadeTween.IsActive())
            fadeTween.Kill();

        transform.DOShakePosition(0.15f, unlockShakeStrength, 12, 90f, false, true).SetUpdate(true);
        yield return new WaitForSeconds(0.15f);

        if (tileSpriteRenderer != null)
            fadeTween = tileSpriteRenderer.DOFade(0f, unlockFadeDuration).SetEase(Ease.Linear).SetUpdate(true);
        if (orderText != null)
            orderText.DOFade(0f, unlockFadeDuration).SetEase(Ease.Linear).SetUpdate(true);
        yield return new WaitForSeconds(unlockFadeDuration);

        RestoreDefaultSprite();
        if (tileSpriteRenderer != null)
            tileSpriteRenderer.color = Color.white;
        if (orderText != null)
            orderText.gameObject.SetActive(false);
        fadeTween = null;

        transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 4, 0.5f).SetUpdate(true);
    }

    /// <summary>잘못된 순서로 진입 시도 시 타일을 붉게 흔들어 피드백.</summary>
    public void PlayWrongOrderShake()
    {
        float duration = 0.35f;
        float strength = 0.14f;
        transform.DOShakePosition(duration, strength, 18, 90f, false, true).SetUpdate(true);
        if (tileSpriteRenderer != null)
        {
            Color orig = tileSpriteRenderer.color;
            tileSpriteRenderer.DOColor(new Color(1f, 0.25f, 0.25f, orig.a), 0.04f).SetUpdate(true).OnComplete(() =>
            {
                if (tileSpriteRenderer != null)
                    tileSpriteRenderer.DOColor(orig, 0.3f).SetUpdate(true);
            });
        }
    }

    /// <summary>게임오버 리셋 시 잠금 타일과 순서 숫자를 다시 표시.</summary>
    public void ResetGearVisibility()
    {
        displayRemaining = targetOrderValue;
        if (fadeTween != null && fadeTween.IsActive())
            fadeTween.Kill();

        ApplyLockedVisual();

        if (orderText != null)
        {
            orderText.gameObject.SetActive(true);
            orderText.text = targetOrderValue.ToString();
        }
        if (tile != null && tile.GetNumberText() != null)
            tile.GetNumberText().gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (tile != null && tile.GetNumberText() != null)
            tile.GetNumberText().gameObject.SetActive(false);

        if (tileSpriteRenderer == null)
            return;

        if (IsSolvedState())
        {
            RestoreDefaultSprite();
            tileSpriteRenderer.color = Color.white;
            if (orderText != null)
                orderText.gameObject.SetActive(false);
            return;
        }

        ApplyLockedSprite();

        Color lockedColor;
        if (displayRemaining == 1)
            lockedColor = new Color(0.35f, 1f, 0.45f, 1f) * 1.4f;
        else if (displayRemaining > 1)
            lockedColor = new Color(1f, 0.35f, 0.35f, 1f) * 1.2f;
        else
            lockedColor = Color.white;

        tileSpriteRenderer.color = lockedColor;

        if (orderText != null)
        {
            bool shouldShow = tile != null && tile.IsActive && displayRemaining > 0;
            if (orderText.gameObject.activeSelf != shouldShow)
                orderText.gameObject.SetActive(shouldShow);
            if (shouldShow)
                ApplyOrderTextColor(lockedColor);
        }
    }

    private void OnDestroy()
    {
        if (fadeTween != null && fadeTween.IsActive())
            fadeTween.Kill();
        if (orderText != null)
            Destroy(orderText.gameObject);
    }
}
