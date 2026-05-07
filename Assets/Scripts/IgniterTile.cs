using UnityEngine;
using DG.Tweening;
using TMPro;

/// <summary>
/// Igniter 타일: 밟는 순간 targetID에 해당하는 Hidden 타일들을 활성화하는 스위치 타일.
/// 이동/감소 규칙은 일반 타일과 같고, 시각적 식별을 위해 전용 스프라이트를 사용한다.
/// </summary>
[RequireComponent(typeof(Tile))]
public class IgniterTile : MonoBehaviour
{
    [Header("대표 컬러 (트레일 등 연출용)")]
    [Tooltip("밟았을 때 트레일이 잠깐 이 색으로 변하는 데 사용")]
    [SerializeField] private Color accentColor = new Color(1f, 0.6f, 0.2f, 1f); // 네온 오렌지
    [SerializeField] private string igniterSpritePath = "Sprites/igniter_tile";
    [Header("전환 연출")]
    [SerializeField] private float consumePulseDuration = 0.22f;
    [SerializeField] private float consumeFlashDuration = 0.12f;
    [SerializeField] private float consumePunchScale = 0.18f;

    private Tile tile;
    private SpriteRenderer spriteRenderer;
    private TMP_Text numberText;
    private Sprite defaultSprite;
    private string targetID;
    private bool hasTriggered;
    private bool useIgniterVisual = true;
    private Sequence consumeSequence;

    private void Awake()
    {
        tile = GetComponent<Tile>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        numberText = tile != null ? tile.GetNumberText() : GetComponentInChildren<TMP_Text>(true);
        defaultSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    /// <summary>
    /// GameManager가 그리드 생성 후 호출. targetID와 igniter 스프라이트를 설정한다.
    /// </summary>
    public void Setup(string id)
    {
        targetID = id ?? "";
        hasTriggered = false;
        useIgniterVisual = true;
        RefreshVisualState();
    }

    private void ApplyIgniterSprite()
    {
        Sprite igniterSprite = Resources.Load<Sprite>(igniterSpritePath);
        if (igniterSprite != null && spriteRenderer != null)
            spriteRenderer.sprite = igniterSprite;
    }

    private void RestoreDefaultSprite()
    {
        if (spriteRenderer != null && defaultSprite != null)
            spriteRenderer.sprite = defaultSprite;
    }

    private void ApplyIgniterVisual()
    {
        ApplyIgniterSprite();
        if (numberText != null && tile != null && tile.CurrentNumber > 0)
            numberText.gameObject.SetActive(true);
    }

    private void ApplyNormalVisual()
    {
        RestoreDefaultSprite();
        if (tile != null)
            tile.RestoreNeonColor();
        if (numberText != null && tile != null && tile.CurrentNumber > 0)
            numberText.gameObject.SetActive(true);
    }

    public void RefreshVisualState()
    {
        if (useIgniterVisual && tile != null && tile.CurrentNumber > 0)
            ApplyIgniterVisual();
        else
            ApplyNormalVisual();
    }

    /// <summary>
    /// 플레이어가 이 타일을 밟은 순간 호출. targetID에 해당하는 Hidden 그룹 활성화 (GameManager에서 호출).
    /// </summary>
    public int TriggerHiddenTiles(System.Collections.Generic.List<HiddenTile> hiddenTiles, bool instant = false, float relayInterval = 0.08f, System.Action<HiddenTile> onHiddenLive = null)
    {
        if (hasTriggered || hiddenTiles == null) return 0;
        hasTriggered = true;
        if (GameManager.VerboseStage6DebugEnabled)
            Debug.Log($"[Stage6 Igniter 실행] source={GameManager.DescribeTileForDebug(tile)} targetID={targetID} instant={instant} relay={relayInterval:F3} hiddenCount={hiddenTiles.Count}");
        float delay = 0f;
        int scheduledCount = 0;
        foreach (var h in hiddenTiles)
        {
            if (h != null && !h.IsActivated)
            {
                if (GameManager.VerboseStage6DebugEnabled)
                    Debug.Log($"[Stage6 Igniter 예약] source={GameManager.DescribeTileForDebug(tile)} target={GameManager.DescribeTileForDebug(h.GetComponent<Tile>())} delay={(instant ? 0f : delay):F3}");
                h.ActivateWithDelay(instant ? 0f : delay, onHiddenLive);
                scheduledCount++;
                if (!instant)
                    delay += relayInterval;
            }
        }

        return scheduledCount;
    }

    /// <summary>트레일 등 연출용 대표 컬러.</summary>
    public Color GetAccentColor() => accentColor;

    public void OnConsumed()
    {
        if (!useIgniterVisual)
        {
            RefreshVisualState();
            return;
        }

        useIgniterVisual = false;

        if (consumeSequence != null && consumeSequence.IsActive())
            consumeSequence.Kill();

        Color baseColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        Color flashColor = new Color(
            Mathf.Max(baseColor.r, accentColor.r * 1.25f),
            Mathf.Max(baseColor.g, accentColor.g * 1.25f),
            Mathf.Max(baseColor.b, accentColor.b * 1.25f),
            baseColor.a);

        consumeSequence = DOTween.Sequence().SetUpdate(true);
        consumeSequence.Append(transform.DOPunchScale(Vector3.one * consumePunchScale, consumePulseDuration, 4, 0.6f).SetEase(Ease.OutQuad));
        if (spriteRenderer != null)
        {
            consumeSequence.Join(spriteRenderer.DOColor(flashColor, consumeFlashDuration).SetEase(Ease.OutQuad));
            consumeSequence.Append(spriteRenderer.DOColor(baseColor, consumeFlashDuration).SetEase(Ease.InQuad));
        }
        consumeSequence.OnComplete(() =>
        {
            consumeSequence = null;
            if (spriteRenderer != null)
                spriteRenderer.color = Color.white;
            RefreshVisualState();
        });
    }

    /// <summary>
    /// 리셋/게임오버 시 GameManager가 호출. 다시 밟을 수 있는 상태로 복구한다.
    /// </summary>
    public void ResetToInitialState()
    {
        hasTriggered = false;
        useIgniterVisual = true;
        DOTween.Kill(transform);
        if (spriteRenderer != null) DOTween.Kill(spriteRenderer);
        if (consumeSequence != null && consumeSequence.IsActive())
            consumeSequence.Kill();
        consumeSequence = null;
        if (spriteRenderer != null)
            spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
        RefreshVisualState();
    }

    public string TargetID => targetID;
    public bool HasTriggered => hasTriggered;
}
