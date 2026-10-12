using UnityEngine;

/// <summary>
/// A2 소프트 클레이 테마: 지금 시작할 칸(현재 위치)을 테라코타 외곽선과 주기적으로 퍼지는 물결로 강조한다.
/// 타일의 위치·크기(하트비트 포함)를 매 프레임 따라가며, 드래그 중이거나 표시할 칸이 없으면 숨는다.
/// </summary>
public sealed class ClayStartMarker : MonoBehaviour
{
    private const float RipplePeriod = 1.15f;
    private const float AppearDuration = 0.22f;

    private SpriteRenderer ring;
    private SpriteRenderer ripple;
    private SpriteRenderer ripple2;
    private Tile target;
    private float targetSince;

    public static ClayStartMarker Create(Transform parent)
    {
        var go = new GameObject("ClayStartMarker");
        go.transform.SetParent(parent, false);
        return go.AddComponent<ClayStartMarker>();
    }

    private void Awake()
    {
        ring = MakeRing("Ring");
        ripple = MakeRing("Ripple");
        ripple2 = MakeRing("Ripple2");
        SetVisible(false);
    }

    private SpriteRenderer MakeRing(string objectName)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = VisualTheme.RingSprite;
        sr.color = VisualTheme.Accent;
        return sr;
    }

    /// <summary>강조할 타일(없으면 null).</summary>
    public void SetTarget(Tile tile)
    {
        if (tile != target)
        {
            target = tile;
            targetSince = Time.unscaledTime;
        }
    }

    private void SetVisible(bool visible)
    {
        if (ring != null) ring.enabled = visible;
        if (ripple != null) ripple.enabled = visible;
        if (ripple2 != null) ripple2.enabled = visible;
    }

    private void LateUpdate()
    {
        SpriteRenderer tileRenderer = target != null ? target.GetComponent<SpriteRenderer>() : null;
        bool show = target != null && target.IsActive && tileRenderer != null && tileRenderer.enabled && target.gameObject.activeInHierarchy;
        SetVisible(show);
        if (!show)
            return;

        transform.position = target.transform.position + new Vector3(0f, 0f, -0.05f);
        transform.rotation = target.transform.rotation;
        Vector3 tileScale = target.transform.lossyScale;
        int order = tileRenderer.sortingOrder + 3;

        float now = Time.unscaledTime;
        float appear = Mathf.Clamp01((now - targetSince) / AppearDuration);
        float appearScale = Mathf.Lerp(1.25f, 1f, 1f - (1f - appear) * (1f - appear));
        float intensity = Mathf.Max(0.3f, GameplayFeedback.Intensity);

        // 고정 외곽선: 살짝 숨 쉬듯 진해졌다 옅어짐
        // 짝꿍 외곽선 등 타일 위 다른 테두리와 겹치지 않게 살짝 바깥에 그린다.
        ring.transform.localScale = tileScale * appearScale * 1.07f;
        Color c = VisualTheme.Accent;
        c.a = appear * (0.82f + 0.18f * Mathf.Sin(now * 5f));
        ring.color = c;
        ring.sortingOrder = order;

        // 바깥으로 퍼지며 사라지는 물결 2개(반 주기 차이)
        UpdateRipple(ripple, now, 0f, tileScale, order - 1, intensity, appear);
        UpdateRipple(ripple2, now, 0.5f, tileScale, order - 1, intensity, appear);
    }

    private static void UpdateRipple(SpriteRenderer sr, float now, float phaseOffset, Vector3 tileScale, int order, float intensity, float appear)
    {
        float phase = Mathf.Repeat(now / RipplePeriod + phaseOffset, 1f);
        float eased = 1f - (1f - phase) * (1f - phase);
        sr.transform.localScale = tileScale * (1.07f + 0.42f * intensity * eased);
        Color c = VisualTheme.Accent;
        c.a = 0.55f * (1f - phase) * appear;
        sr.color = c;
        sr.sortingOrder = order;
    }
}
