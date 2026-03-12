using UnityEngine;
using TMPro;

/// <summary>
/// BlindCurtain 타일: 게임 규칙은 일반 타일과 같고, hidden 아이콘으로만 시각적 구분을 준다.
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
    [SerializeField] private float glowMult = 0.9f;

    private Tile tile;
    private SpriteRenderer tileSpriteRenderer;
    private TMP_Text numberText;
    private SpriteRenderer iconRenderer;
    private GameObject iconObject;

    private void Awake()
    {
        tile = GetComponent<Tile>();
        tileSpriteRenderer = GetComponent<SpriteRenderer>();
        numberText = tile != null ? tile.GetNumberText() : GetComponentInChildren<TMP_Text>(true);
        CreateIcon();
    }

    public void RefreshVisualState()
    {
        if (numberText != null)
            numberText.gameObject.SetActive(false);
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

    /// <summary>타일 배경 HDR 색상과 동기화해 아이콘에 발광(Emission) 효과 적용. 타일 비활성(count 0)이면 아이콘도 숨김.</summary>
    private void LateUpdate()
    {
        RefreshVisualState();
        if (iconObject != null && tile != null)
        {
            bool shouldShow = tile.IsActive;
            if (iconObject.activeSelf != shouldShow)
                iconObject.SetActive(shouldShow);
        }
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
