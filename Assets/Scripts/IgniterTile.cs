using UnityEngine;
using DG.Tweening;
using TMPro;

/// <summary>
/// Igniter 타일: 일회성 전원. 밟는 순간 targetID에 해당하는 Hidden 타일들을 활성화하고,
/// 다음 타일로 이동할 때 네온 소멸·먼지처럼 사라지는 연출 후 비활성화.
/// 숫자 대신 Resources/Sprites/switch.png 사용.
/// </summary>
[RequireComponent(typeof(Tile))]
public class IgniterTile : MonoBehaviour
{
    [Header("소멸 연출")]
    [Tooltip("네온 꺼짐 + 스케일 다운 시간")]
    [SerializeField] private float vanishDuration = 0.25f;
    [Tooltip("먼지처럼 흩어지는 파티클(선택). 없으면 스케일·페이드만")]
    [SerializeField] private ParticleSystem dustEffect;

    private Tile tile;
    private SpriteRenderer spriteRenderer;
    private TMP_Text numberText;
    private string targetID;
    private bool hasTriggered;

    private void Awake()
    {
        tile = GetComponent<Tile>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        numberText = tile != null ? tile.GetNumberText() : GetComponentInChildren<TMP_Text>(true);
    }

    /// <summary>
    /// GameManager가 그리드 생성 후 호출. targetID 설정, switch 스프라이트·숫자 숨김.
    /// </summary>
    public void Setup(string id)
    {
        targetID = id ?? "";
        hasTriggered = false;
        ApplySwitchSprite();
        EnsureNumberHidden();
    }

    private void ApplySwitchSprite()
    {
        Sprite switchSprite = Resources.Load<Sprite>("Sprites/switch");
        if (switchSprite != null && spriteRenderer != null)
            spriteRenderer.sprite = switchSprite;
    }

    /// <summary>
    /// 플레이어가 이 타일을 밟은 순간 호출. targetID에 해당하는 Hidden 그룹 활성화 (GameManager에서 호출).
    /// </summary>
    public void TriggerHiddenTiles(System.Collections.Generic.List<HiddenTile> hiddenTiles, Vector3 igniterWorldPos)
    {
        if (hasTriggered || hiddenTiles == null) return;
        hasTriggered = true;
        float delay = 0f;
        foreach (var h in hiddenTiles)
        {
            if (h != null && !h.IsActivated)
            {
                h.ActivateWithDelay(delay);
                delay += 0.08f;
            }
        }
    }

    /// <summary>
    /// 다음 타일로 이동하는 순간 호출. 네온 소멸·먼지 연출 후 count 0으로 비활성화.
    /// </summary>
    public void OnLeftThenVanish()
    {
        if (spriteRenderer != null)
            spriteRenderer.DOFade(0f, vanishDuration).SetEase(Ease.InQuad);
        transform.DOScale(transform.localScale * 0.3f, vanishDuration).SetEase(Ease.InBack);
        if (dustEffect != null)
            dustEffect.Play();
        DOVirtual.DelayedCall(vanishDuration, () =>
        {
            if (tile != null)
                tile.SetNumber(0);
        });
    }

    /// <summary>
    /// 리셋/게임오버 시 GameManager가 호출. 다시 밟을 수 있는 상태로 복구 (switch 스프라이트, count 1).
    /// </summary>
    public void ResetToInitialState()
    {
        hasTriggered = false;
        DOTween.Kill(transform);
        if (spriteRenderer != null) DOTween.Kill(spriteRenderer);
        ApplySwitchSprite();
        if (spriteRenderer != null)
            spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
        if (tile != null)
            tile.SetNumber(1);
        EnsureNumberHidden();
    }

    /// <summary>
    /// Igniter는 count=1 고정이라 숫자 미표시. 리셋 후 PlayBounceAppearance 등으로 숫자가 켜질 수 있으므로 강제로 숨김.
    /// </summary>
    public void EnsureNumberHidden()
    {
        if (numberText != null)
            numberText.gameObject.SetActive(false);
    }

    public string TargetID => targetID;
}
