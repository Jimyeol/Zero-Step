using UnityEngine;
using TMPro;

/// <summary>
/// BlindCurtain 타일: 게임 규칙은 일반 타일과 같고, 숫자 위를 가리는 커버 타일로 시각적 구분을 준다.
/// </summary>
[RequireComponent(typeof(Tile))]
public class BlindCurtainTile : MonoBehaviour
{
    [Header("스프라이트")]
    [Tooltip("Resources 경로 (Assets/Resources/Sprites/blind_curtain_tile.png → Sprites/blind_curtain_tile)")]
    [SerializeField] private string tileSpritePath = "Sprites/black_out_tile";
    [SerializeField] private float coverScale = 1f;

    private Tile tile;
    private SpriteRenderer tileSpriteRenderer;
    private TMP_Text numberText;
    private SpriteRenderer coverRenderer;
    private GameObject coverObject;

    private void Awake()
    {
        tile = GetComponent<Tile>();
        tileSpriteRenderer = GetComponent<SpriteRenderer>();
        numberText = tile != null ? tile.GetNumberText() : GetComponentInChildren<TMP_Text>(true);
        CreateCoverSprite();
        RefreshVisualState();
    }

    public void RefreshVisualState()
    {
        if (numberText != null)
            numberText.gameObject.SetActive(false);

        if (coverObject != null && tile != null)
        {
            bool shouldShow = tile.IsActive;
            if (coverObject.activeSelf != shouldShow)
                coverObject.SetActive(shouldShow);
        }
    }

    private void CreateCoverSprite()
    {
        if (coverObject != null || tileSpriteRenderer == null)
            return;

        Sprite tileSprite = Resources.Load<Sprite>(tileSpritePath);
        if (tileSprite == null)
        {
            Debug.LogWarning($"[BlindCurtainTile] Resources/{tileSpritePath} 을(를) 찾을 수 없습니다.");
            return;
        }

        coverObject = new GameObject("BlindCurtainCover");
        coverObject.transform.SetParent(transform);
        coverObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        coverObject.transform.localRotation = Quaternion.identity;
        coverObject.transform.localScale = Vector3.one * coverScale;

        coverRenderer = coverObject.AddComponent<SpriteRenderer>();
        coverRenderer.sprite = tileSprite;
        coverRenderer.sortingOrder = tileSpriteRenderer.sortingOrder + 2;
        if (tileSpriteRenderer.sharedMaterial != null)
            coverRenderer.sharedMaterial = tileSpriteRenderer.sharedMaterial;
        coverRenderer.color = tileSpriteRenderer.color;
    }

    private void LateUpdate()
    {
        RefreshVisualState();

        if (coverRenderer != null && tileSpriteRenderer != null)
            coverRenderer.color = tileSpriteRenderer.color;
    }

    private void OnDestroy()
    {
        if (coverObject != null)
            Destroy(coverObject);
    }
}
