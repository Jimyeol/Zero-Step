using System.Collections;
using UnityEngine;
using DG.Tweening;
using TMPro;

/// <summary>
/// 십자 폭발 타일: 밟을 때만 0.1초 발광(1.2배) 후 상하좌우 인접 타일만 -1. 대기 중에는 펄스 없음.
/// </summary>
[RequireComponent(typeof(Tile))]
public class CrossBlastTile : MonoBehaviour
{
    [Header("터치 발광 연출")]
    [Tooltip("밟았을 때 발광 지속 시간(초)")]
    [SerializeField] private float flashDuration = 0.1f;
    [Tooltip("발광 시 스케일 배율 (타일 기준 1.2배)")]
    [SerializeField] private float flashScaleMult = 1.2f;
    [Tooltip("발광 색 (JSON properties beamColor로 덮어쓸 수 있음)")]
    [SerializeField] private string beamColorHex = "#00FFFF";
    [SerializeField] private string tileSpritePath = "Sprites/corss_blast_tile";
    [SerializeField] [Range(0.1f, 2f)] private float numberScaleMultiplier = 0.8f;

    private Tile tile;
    private SpriteRenderer spriteRenderer;
    private Vector3 baseScale;
    private Color baseBeamColor;
    private bool isExploding;
    private Coroutine explosionRoutine;

    private void Awake()
    {
        tile = GetComponent<Tile>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            baseScale = transform.localScale;
        ApplyTileSprite();
        ApplyNumberScale();
        ParseBeamColor(beamColorHex);
    }

    /// <summary>
    /// JSON properties 또는 Inspector 값으로 발광 색 설정.
    /// </summary>
    public void SetProperties(float speed, float range, string beamHex)
    {
        if (!string.IsNullOrEmpty(beamHex))
        {
            beamColorHex = beamHex;
            ParseBeamColor(beamHex);
        }
    }

    /// <summary>
    /// 터치 시 0.1초 발광(1.2배) 후 상하좌우 인접 타일만 -1. nextTile은 밟고 이동한 다음 타일이라 효과 제외.
    /// </summary>
    public void TriggerExplosion(GameManager gameManager, Tile nextTile)
    {
        if (isExploding || gameManager == null) return;
        explosionRoutine = StartCoroutine(FlashAndDecreaseAdjacentRoutine(gameManager, nextTile));
    }

    public void ResetTransientVisualState()
    {
        if (explosionRoutine != null)
        {
            StopCoroutine(explosionRoutine);
            explosionRoutine = null;
        }

        isExploding = false;
        DOTween.Kill(transform);
        if (spriteRenderer != null)
        {
            DOTween.Kill(spriteRenderer);
            spriteRenderer.color = Color.white;
        }
        if (baseScale != Vector3.zero)
            transform.localScale = baseScale;
        if (tile != null)
            tile.RestoreNeonColor();
    }

    private void ParseBeamColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color c))
            baseBeamColor = c;
        else
            baseBeamColor = new Color(0f, 1f, 1f, 1f); // 시안 기본
    }

    private void ApplyTileSprite()
    {
        if (spriteRenderer == null)
            return;

        Sprite tileSprite = Resources.Load<Sprite>(tileSpritePath);
        if (tileSprite == null)
        {
            Debug.LogWarning($"[CrossBlastTile] Resources/{tileSpritePath} 을(를) 찾을 수 없습니다.");
            return;
        }
        spriteRenderer.sprite = tileSprite;
    }

    private void ApplyNumberScale()
    {
        TMP_Text numberText = tile != null ? tile.GetNumberText() : null;
        if (numberText == null)
            return;

        numberText.transform.localScale *= numberScaleMultiplier;
    }

    private IEnumerator FlashAndDecreaseAdjacentRoutine(GameManager gameManager, Tile nextTile)
    {
        isExploding = true;

        Color brightColor = baseBeamColor * 2f;
        Vector3 flashScale = baseScale * flashScaleMult;

        // 0.1초 동안 타일 1.2배 + 발광
        if (spriteRenderer != null)
            spriteRenderer.color = brightColor;
        transform.DOScale(flashScale, flashDuration).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(flashDuration);

        // 상하좌우 인접 타일만 -1 (밟고 이동한 다음 타일은 제외)
        int exX = (nextTile != null) ? nextTile.X : -999;
        int exY = (nextTile != null) ? nextTile.Y : -999;
        gameManager.DecreaseAdjacentTiles(tile.X, tile.Y, exX, exY);

        // 스케일·색상 복구
        transform.DOScale(baseScale, 0.05f).SetEase(Ease.OutQuad);
        if (tile != null)
            tile.RestoreNeonColor();
        yield return new WaitForSeconds(0.05f);

        isExploding = false;
        explosionRoutine = null;
    }
}
