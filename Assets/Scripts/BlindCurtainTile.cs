using UnityEngine;

/// <summary>
/// 암전 커튼(BlindCurtain) 타일: count는 항상 1, 밟는 순간 모든 타일 숫자가 ?로 변함.
/// UI에는 숫자 대신 hidden.png 아이콘 표시, 광원(발광) 효과 적용.
/// </summary>
[RequireComponent(typeof(Tile))]
public class BlindCurtainTile : MonoBehaviour
{
    [Header("아이콘")]
    [Tooltip("Resources 경로 (Assets/Resources/Sprites/hidden.png → Sprites/hidden)")]
    [SerializeField] private string iconSpritePath = "Sprites/hidden";
    [Tooltip("아이콘 스케일 (타일 중앙에 맞게)")]
    [SerializeField] private float iconScale = 0.5f;
    [Tooltip("발광(HDR) 강도 배율. 1.5~2.0 권장 — 타일과 동일 머티리얼 사용 시 Emission으로 빛남")]
    [SerializeField] private float glowMult = 1.8f;

    private Tile tile;
    private SpriteRenderer tileSpriteRenderer;
    private SpriteRenderer iconRenderer;
    private GameObject iconObject;

    private void Awake()
    {
        tile = GetComponent<Tile>();
        tileSpriteRenderer = GetComponent<SpriteRenderer>();
        if (tile != null && tile.GetNumberText() != null)
            tile.GetNumberText().gameObject.SetActive(false);
        CreateIcon();
    }

    private void CreateIcon()
    {
        Sprite iconSprite = Resources.Load<Sprite>(iconSpritePath);
        if (iconSprite == null)
        {
            Debug.LogWarning($"[BlindCurtainTile] Resources/{iconSpritePath} 을(를) 찾을 수 없습니다.");
            return;
        }

        iconObject = new GameObject("BlindCurtainIcon");
        iconObject.transform.SetParent(transform);
        iconObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        iconObject.transform.localRotation = Quaternion.identity;
        iconObject.transform.localScale = Vector3.one * iconScale;

        iconRenderer = iconObject.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = iconSprite;
        iconRenderer.sortingOrder = 1;
        // 타일과 동일 머티리얼 사용 → HDR 색상이 Emission으로 발광
        if (tileSpriteRenderer != null && tileSpriteRenderer.sharedMaterial != null)
            iconRenderer.sharedMaterial = tileSpriteRenderer.sharedMaterial;
        iconRenderer.color = Color.white;
    }

    /// <summary>타일 배경 HDR 색상과 동기화해 아이콘에 발광(Emission) 효과 적용.</summary>
    private void LateUpdate()
    {
        if (iconRenderer == null || tileSpriteRenderer == null) return;
        Color tileColor = tileSpriteRenderer.color;
        // HDR 색상(1 초과)으로 설정 시 URP Sprite-Lit 머티리얼에서 발광
        Color hdrIcon = new Color(
            Mathf.Max(0f, tileColor.r * glowMult),
            Mathf.Max(0f, tileColor.g * glowMult),
            Mathf.Max(0f, tileColor.b * glowMult),
            tileColor.a);
        iconRenderer.color = hdrIcon;
    }

    private void OnDestroy()
    {
        if (iconObject != null)
            Destroy(iconObject);
    }
}
