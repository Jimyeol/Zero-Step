using UnityEngine;

/// <summary>
/// 충전(Charge) 타일: 떠날 때 자기 숫자는 1 줄고, 상하좌우의 남아 있는 이웃(들어가는 칸 제외)을 1씩 늘린다.
/// FixedKnot·Igniter·Hidden 타일은 영향을 받지 않는다. 숫자를 늘리는 유일한 장치라 "다시 와야 하는" 퍼즐을 만든다.
/// 규칙 판정은 GameManager가 하고, 이 컴포넌트는 "+" 표시와 충전 연출만 담당한다.
/// </summary>
[RequireComponent(typeof(Tile))]
public class ChargeTile : MonoBehaviour
{
    private static readonly Color ChargeColor = new Color(0.45f, 1.45f, 0.7f, 1f);
    private const float GlyphHalfLength = 0.17f;
    private const float GlyphOffset = 0.3f;

    private Tile tile;
    private SpriteRenderer tileSpriteRenderer;
    private LineRenderer glyphHorizontal;
    private LineRenderer glyphVertical;
    private float pulseUntil;

    private void Awake()
    {
        tile = GetComponent<Tile>();
        tileSpriteRenderer = GetComponent<SpriteRenderer>();
        if (VisualTheme.IsSoftClay)
        {
            // 점토 테마: 선 대신 모서리 잉크색 "+" 아이콘.
            clayIcon = VisualTheme.AttachCornerIcon(transform, VisualTheme.Icon.Plus);
            if (clayIcon != null)
                clayIconBaseScale = clayIcon.transform.localScale;
            return;
        }
        glyphHorizontal = CreateGlyphLine("ChargeGlyphH");
        glyphVertical = CreateGlyphLine("ChargeGlyphV");
    }

    private SpriteRenderer clayIcon;
    private Vector3 clayIconBaseScale;

    private LineRenderer CreateGlyphLine(string objectName)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.numCapVertices = 4;
        line.alignment = LineAlignment.TransformZ;
        if (tileSpriteRenderer != null)
        {
            if (tileSpriteRenderer.sharedMaterial != null)
                line.sharedMaterial = tileSpriteRenderer.sharedMaterial;
            line.sortingLayerID = tileSpriteRenderer.sortingLayerID;
            line.sortingOrder = tileSpriteRenderer.sortingOrder + 5;
        }
        return line;
    }

    /// <summary>떠날 때 충전이 일어나면 GameManager가 호출한다.</summary>
    public void PlayChargeFeedback(int chargedCount)
    {
        if (chargedCount <= 0)
            return;
        pulseUntil = Time.unscaledTime + 0.25f * Mathf.Max(0.2f, GameplayFeedback.Intensity);
    }

    private void LateUpdate()
    {
        bool visible = tile != null && tile.IsActive && !tile.IsGameOverBlackoutActive && transform.localScale.sqrMagnitude > 0.0001f;
        if (clayIcon != null)
        {
            clayIcon.enabled = visible;
            clayIcon.transform.localScale = clayIconBaseScale * (Time.unscaledTime < pulseUntil ? 1.3f : 1f);
            return;
        }
        if (glyphHorizontal == null || glyphVertical == null)
            return;
        glyphHorizontal.enabled = visible;
        glyphVertical.enabled = visible;
        if (!visible)
            return;

        // 타일 오른쪽 위 모서리에 "+" 표시(숫자를 가리지 않게). 충전 직후 잠깐 커진다.
        float half = tileSpriteRenderer != null && tileSpriteRenderer.sprite != null
            ? tileSpriteRenderer.sprite.bounds.extents.x
            : 0.5f;
        float pulse = Time.unscaledTime < pulseUntil ? 1.35f : 1f;
        float len = half * GlyphHalfLength * pulse;
        Vector3 center = new Vector3(half * GlyphOffset * 2f, half * GlyphOffset * 2f, -0.03f);
        glyphHorizontal.SetPosition(0, center + Vector3.left * len);
        glyphHorizontal.SetPosition(1, center + Vector3.right * len);
        glyphVertical.SetPosition(0, center + Vector3.down * len);
        glyphVertical.SetPosition(1, center + Vector3.up * len);
        float width = half * 0.11f;
        glyphHorizontal.widthMultiplier = width;
        glyphVertical.widthMultiplier = width;
        glyphHorizontal.startColor = glyphHorizontal.endColor = ChargeColor;
        glyphVertical.startColor = glyphVertical.endColor = ChargeColor;
    }

    /// <summary>충전의 영향을 받을 수 있는 타일인지(FixedKnot·Igniter·Hidden 제외).</summary>
    public static bool CanBeCharged(Tile target)
    {
        if (target == null)
            return false;
        return target.GetComponent<FixedKnotTile>() == null &&
               target.GetComponent<IgniterTile>() == null &&
               target.GetComponent<HiddenTile>() == null;
    }
}
