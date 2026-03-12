using UnityEngine;
using DG.Tweening;

/// <summary>
/// Igniter 타일: 밟는 순간 targetID에 해당하는 Hidden 타일들을 활성화하는 스위치 타일.
/// 이동/감소 규칙은 일반 타일과 같고, 시각적 식별을 위해 switch 스프라이트를 사용한다.
/// </summary>
[RequireComponent(typeof(Tile))]
public class IgniterTile : MonoBehaviour
{
    [Header("대표 컬러 (트레일 등 연출용)")]
    [Tooltip("밟았을 때 트레일이 잠깐 이 색으로 변하는 데 사용")]
    [SerializeField] private Color accentColor = new Color(1f, 0.6f, 0.2f, 1f); // 네온 오렌지

    private SpriteRenderer spriteRenderer;
    private string targetID;
    private bool hasTriggered;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// GameManager가 그리드 생성 후 호출. targetID와 switch 스프라이트를 설정한다.
    /// </summary>
    public void Setup(string id)
    {
        targetID = id ?? "";
        hasTriggered = false;
        ApplySwitchSprite();
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
    public void TriggerHiddenTiles(System.Collections.Generic.List<HiddenTile> hiddenTiles, bool instant = false, float relayInterval = 0.08f)
    {
        if (hasTriggered || hiddenTiles == null) return;
        hasTriggered = true;
        float delay = 0f;
        foreach (var h in hiddenTiles)
        {
            if (h != null && !h.IsActivated)
            {
                h.ActivateWithDelay(instant ? 0f : delay);
                if (!instant)
                    delay += relayInterval;
            }
        }
    }

    /// <summary>트레일 등 연출용 대표 컬러.</summary>
    public Color GetAccentColor() => accentColor;

    /// <summary>
    /// 리셋/게임오버 시 GameManager가 호출. 다시 밟을 수 있는 상태로 복구한다.
    /// </summary>
    public void ResetToInitialState()
    {
        hasTriggered = false;
        DOTween.Kill(transform);
        if (spriteRenderer != null) DOTween.Kill(spriteRenderer);
        ApplySwitchSprite();
        if (spriteRenderer != null)
            spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
    }

    public string TargetID => targetID;
}
