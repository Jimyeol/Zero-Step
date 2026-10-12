using UnityEngine;
using TMPro;

/// <summary>
/// BlindCurtain 타일: 게임 규칙은 일반 타일과 같고, 숫자 대신 물음표만 표시한다.
/// 튜토리얼과 같은 "가림(눈 가림)" 아이콘을 숫자 뒤에 흐리게 깔아 Blackout과 구분한다.
/// 숫자 텍스트는 Tile.UpdateNumberDisplay가 "?"로 고정하므로 매 프레임 갱신하지 않는다.
/// </summary>
[RequireComponent(typeof(Tile))]
public class BlindCurtainTile : MonoBehaviour
{
    private const string IconSpritePath = "Sprites/blind_curtain_tile";
    private const float IconScale = 0.62f;
    private const float IconAlpha = 0.32f;

    private Tile tile;
    private TMP_Text numberText;
    private SpriteRenderer tileSpriteRenderer;
    private GameObject iconObject;
    private SpriteRenderer iconRenderer;
    private bool lastActive = true;

    private void Awake()
    {
        tile = GetComponent<Tile>();
        numberText = tile != null ? tile.GetNumberText() : GetComponentInChildren<TMP_Text>(true);
        tileSpriteRenderer = GetComponent<SpriteRenderer>();
        CreateIcon();
        RefreshVisualState();
    }

    private void Start()
    {
        RefreshVisualState();
    }

    /// <summary>초기화·리셋·전역 ? 연출 직후 GameManager가 호출해 "?" 표시를 복원한다.</summary>
    public void RefreshVisualState()
    {
        if (numberText == null && tile != null)
            numberText = tile.GetNumberText();

        lastActive = tile == null || tile.IsActive;
        if (numberText != null)
        {
            numberText.text = "?";
            if (numberText.gameObject.activeSelf != lastActive)
                numberText.gameObject.SetActive(lastActive);
            if (lastActive)
                numberText.ForceMeshUpdate(true, true);
        }

        if (iconObject != null && iconObject.activeSelf != lastActive)
            iconObject.SetActive(lastActive);
    }

    private void LateUpdate()
    {
        bool active = tile == null || tile.IsActive;
        bool gameOverBlackout = tile != null && tile.IsGameOverBlackoutActive;
        bool showIcon = active && !gameOverBlackout;
        if (iconObject != null && iconObject.activeSelf != showIcon)
            iconObject.SetActive(showIcon);
        if (showIcon && iconRenderer != null && tileSpriteRenderer != null)
        {
            Color c = tileSpriteRenderer.color;
            iconRenderer.color = new Color(c.r, c.g, c.b, IconAlpha);
        }
        lastActive = active;
    }

    private void CreateIcon()
    {
        if (tileSpriteRenderer == null || iconObject != null)
            return;

        Sprite sprite = Resources.Load<Sprite>(IconSpritePath);
        if (sprite == null)
            return;

        iconObject = new GameObject("BlindCurtainIcon");
        iconObject.transform.SetParent(transform, false);
        iconObject.transform.localPosition = new Vector3(0f, 0f, -0.005f);
        iconObject.transform.localScale = Vector3.one * IconScale;
        iconRenderer = iconObject.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = sprite;
        iconRenderer.sortingOrder = tileSpriteRenderer.sortingOrder + 1;
        if (tileSpriteRenderer.sharedMaterial != null)
            iconRenderer.sharedMaterial = tileSpriteRenderer.sharedMaterial;
        iconRenderer.color = new Color(1f, 1f, 1f, IconAlpha);
    }

    private void OnDestroy()
    {
        if (iconObject != null)
            Destroy(iconObject);
    }
}
