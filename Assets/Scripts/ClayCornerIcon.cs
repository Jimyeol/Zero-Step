using UnityEngine;

/// <summary>점토 테마 모서리 아이콘이 타일의 정렬 순서(시작점·현재 위치는 위로 올라감)를 항상 따라가게 한다.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ClayCornerIcon : MonoBehaviour
{
    private SpriteRenderer iconRenderer;
    private SpriteRenderer tileRenderer;

    private void Awake()
    {
        iconRenderer = GetComponent<SpriteRenderer>();
        tileRenderer = transform.parent != null ? transform.parent.GetComponent<SpriteRenderer>() : null;
    }

    private void LateUpdate()
    {
        if (iconRenderer == null || tileRenderer == null)
            return;
        int order = tileRenderer.sortingOrder + 2;
        if (iconRenderer.sortingOrder != order)
            iconRenderer.sortingOrder = order;
        if (iconRenderer.sortingLayerID != tileRenderer.sortingLayerID)
            iconRenderer.sortingLayerID = tileRenderer.sortingLayerID;
        // 타일이 꺼지면(0이 됨, 숨김) 아이콘도 함께 숨긴다.
        bool show = tileRenderer.enabled && tileRenderer.color.a > 0.01f;
        if (iconRenderer.enabled != show)
            iconRenderer.enabled = show;
    }
}
