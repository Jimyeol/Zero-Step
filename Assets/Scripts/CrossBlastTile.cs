using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 십자 폭발 타일: 떠날 때 짧은 중앙 폭발 cue 후 GameManager가 선택한 상하좌우 타일만 -1.
/// </summary>
[RequireComponent(typeof(Tile))]
public class CrossBlastTile : MonoBehaviour
{
    [Header("떠남 폭발 연출")]
    [Tooltip("발광 충전 지속 시간(초)")]
    [SerializeField] private float chargeDuration = 0.07f;
    [Tooltip("십자 cue가 바깥으로 퍼지는 지속 시간(초)")]
    [SerializeField] private float releaseDuration = 0.14f;
    [Tooltip("십자 cue가 사라지는 지속 시간(초)")]
    [SerializeField] private float fadeDuration = 0.08f;
    [Tooltip("중앙 발광 강도 배율")]
    [SerializeField] private float flashIntensityMultiplier = 1.65f;
    [Tooltip("십자 cue 선 두께")]
    [SerializeField] private float burstLineWidth = 0.12f;
    [Tooltip("발광 색 (JSON properties beamColor로 덮어쓸 수 있음)")]
    [SerializeField] private string beamColorHex = "#00FFFF";
    [SerializeField] private string tileSpritePath = "Sprites/corss_blast_tile";
    [SerializeField] [Range(0.1f, 2f)] private float numberScaleMultiplier = 0.8f;

    private Tile tile;
    private SpriteRenderer spriteRenderer;
    private Vector3 baseScale;
    private Color baseBeamColor;
    private Coroutine explosionRoutine;
    private readonly Tile[] affectedTilesBuffer = new Tile[4];
    private readonly LineRenderer[] burstLines = new LineRenderer[4];
    private GameManager pendingMutationOwner;
    private Tile pendingMutationClearReferenceTile;
    private bool hasPendingBoardMutation;
    private bool burstLineCreationFailed;
    private static Material fallbackBurstLineMaterial;

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

    /// <summary>떠날 때 GameManager가 선택한 타일만 감소시키고 짧은 십자 cue를 보여준다.</summary>
    public void TriggerExplosion(GameManager gameManager, Tile nextTile)
    {
        if (gameManager == null) return;
        if (explosionRoutine != null)
        {
            CompletePendingBoardMutation(false);
            StopCoroutine(explosionRoutine);
            explosionRoutine = null;
            HideBurstLines();
        }

        // 연출 준비(실패할 수 있음)를 먼저 하고, 그 다음에 규칙상 감소 대상을 고른다.
        EnsureBurstLines();
        int affectedCount = gameManager.GetCrossBlastAffectedTiles(tile, nextTile, affectedTilesBuffer);
        BeginPendingBoardMutation(gameManager, null, affectedCount);
        explosionRoutine = StartCoroutine(FlashAndDecreaseAdjacentRoutine(gameManager, affectedCount));
    }

    public void ResetTransientVisualState()
    {
        if (explosionRoutine != null)
        {
            CompletePendingBoardMutation(false);
            StopCoroutine(explosionRoutine);
            explosionRoutine = null;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }
        if (baseScale != Vector3.zero)
            transform.localScale = baseScale;
        if (tile != null)
            tile.RestoreNeonColor();
        HideBurstLines();
        ClearAffectedTiles();
    }

    private void ParseBeamColor(string hex)
    {
        if (VisualTheme.IsSoftClay)
        {
            // 점토 테마: 십자 빔은 테라코타 잉크색(발광 없음).
            baseBeamColor = VisualTheme.Accent;
            return;
        }
        if (ColorUtility.TryParseHtmlString(hex, out Color c))
            baseBeamColor = c;
        else
            baseBeamColor = new Color(0f, 1f, 1f, 1f); // 시안 기본
    }

    private void ApplyTileSprite()
    {
        if (spriteRenderer == null)
            return;
        if (VisualTheme.IsSoftClay)
        {
            VisualTheme.AttachCornerIcon(transform, VisualTheme.Icon.Burst);
            return;
        }

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
        if (VisualTheme.IsSoftClay)
            return;
        TMP_Text numberText = tile != null ? tile.GetNumberText() : null;
        if (numberText == null)
            return;

        numberText.transform.localScale *= numberScaleMultiplier;
    }

    private void EnsureBurstLines()
    {
        if (burstLineCreationFailed || HasBurstLines())
            return;

        try
        {
            CreateBurstLines();
        }
        catch (System.Exception ex)
        {
            burstLineCreationFailed = true;
            HideBurstLines();
            Debug.LogWarning($"[CrossBlastTile] Burst line visual setup failed. CrossBlast gameplay will continue without burst lines. {ex.GetType().Name}: {ex.Message}");
            FirebaseBootstrap.LogNonFatalException(ex, "CrossBlast burst line visual setup failed");
        }
    }

    private bool HasBurstLines()
    {
        for (int i = 0; i < burstLines.Length; i++)
        {
            if (burstLines[i] == null)
                return false;
        }

        return true;
    }

    private void CreateBurstLines()
    {
        Material lineMaterial = ResolveLineMaterial(spriteRenderer);
        for (int i = 0; i < burstLines.Length; i++)
        {
            if (burstLines[i] != null)
                continue;

            // 한 GameObject에는 Renderer를 하나만 붙일 수 있다(타일에는 이미 SpriteRenderer가 있음).
            // 그래서 빔 선은 자식 오브젝트에 만든다.
            var lineObject = new GameObject($"CrossBlastBurstLine{i}");
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.enabled = false;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = burstLineWidth;
            line.sharedMaterial = lineMaterial;
            if (spriteRenderer != null)
            {
                line.sortingLayerID = spriteRenderer.sortingLayerID;
                line.sortingOrder = spriteRenderer.sortingOrder + 20;
            }
            burstLines[i] = line;
        }
    }

    private static Material ResolveLineMaterial(SpriteRenderer renderer)
    {
        if (renderer != null && renderer.sharedMaterial != null)
            return renderer.sharedMaterial;
        if (fallbackBurstLineMaterial != null)
            return fallbackBurstLineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            return null;

        fallbackBurstLineMaterial = new Material(shader)
        {
            name = "CrossBlast Burst Line (Runtime)",
            hideFlags = HideFlags.DontSave
        };
        return fallbackBurstLineMaterial;
    }

    private IEnumerator FlashAndDecreaseAdjacentRoutine(GameManager gameManager, int affectedCount)
    {
        float feedbackIntensity = FeedbackIntensity();

        if (spriteRenderer != null && feedbackIntensity > 0f && !VisualTheme.IsSoftClay)
        {
            Color centerColor = WithAlpha(baseBeamColor, 1f) * Mathf.Lerp(1f, flashIntensityMultiplier, feedbackIntensity);
            centerColor.a = 1f;
            spriteRenderer.color = centerColor;
        }

        // 빔이 이웃에 닿는 순간 숫자가 줄도록, 충전 시간 동안 빔을 이웃까지 뻗게 한다(원인 → 결과 순서).
        yield return ExtendBurstLines(affectedCount, chargeDuration);

        gameManager.DecreaseCrossBlastAffectedTiles(affectedTilesBuffer, affectedCount);
        GameplayFeedback.Instance?.CrossBlast(tile, affectedTilesBuffer, affectedCount);
        CompletePendingBoardMutation(true);
        yield return FadeBurstLines(affectedCount);

        if (tile != null)
            tile.RestoreNeonColor();

        HideBurstLines();
        ClearAffectedTiles();
        explosionRoutine = null;
    }

    private void BeginPendingBoardMutation(GameManager owner, Tile clearReferenceTile, int affectedCount)
    {
        CompletePendingBoardMutation(false);
        if (owner == null || affectedCount <= 0)
            return;

        pendingMutationOwner = owner;
        pendingMutationClearReferenceTile = clearReferenceTile;
        hasPendingBoardMutation = true;
        pendingMutationOwner.RegisterPendingBoardMutation("cross_blast");
    }

    private void CompletePendingBoardMutation(bool requestFailureCheck)
    {
        if (!hasPendingBoardMutation)
            return;

        GameManager owner = pendingMutationOwner;
        Tile clearReferenceTile = pendingMutationClearReferenceTile;
        pendingMutationOwner = null;
        pendingMutationClearReferenceTile = null;
        hasPendingBoardMutation = false;

        if (owner != null)
            owner.CompletePendingBoardMutation("cross_blast", clearReferenceTile, requestFailureCheck);
    }

    private IEnumerator ExtendBurstLines(int affectedCount, float duration)
    {
        float feedbackIntensity = FeedbackIntensity();
        int count = Mathf.Clamp(affectedCount, 0, affectedTilesBuffer.Length);
        if (count <= 0 || feedbackIntensity <= 0f || !HasBurstLines())
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Vector3 center = GetCuePosition(transform.position);
        for (int i = 0; i < burstLines.Length; i++)
        {
            LineRenderer line = burstLines[i];
            bool active = i < count && affectedTilesBuffer[i] != null;
            if (line == null) continue;
            line.enabled = active;
            if (!active) continue;
            line.SetPosition(0, center);
            line.SetPosition(1, center);
        }

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = 1f - (1f - t) * (1f - t);
            for (int i = 0; i < count; i++)
            {
                Tile target = affectedTilesBuffer[i];
                LineRenderer line = burstLines[i];
                if (target == null || line == null || !line.enabled) continue;
                Vector3 targetPosition = GetCuePosition(target.transform.position);
                line.SetPosition(0, center);
                line.SetPosition(1, Vector3.Lerp(center, targetPosition, eased));
                float alpha = 0.95f * feedbackIntensity;
                line.startColor = WithAlpha(baseBeamColor, alpha);
                line.endColor = WithAlpha(baseBeamColor, alpha * 0.6f);
                line.widthMultiplier = burstLineWidth * 1.2f * Mathf.Lerp(0.65f, 1f, feedbackIntensity);
            }
            yield return null;
        }
    }

    private IEnumerator FadeBurstLines(int affectedCount)
    {
        float feedbackIntensity = FeedbackIntensity();
        int count = Mathf.Clamp(affectedCount, 0, affectedTilesBuffer.Length);
        float duration = Mathf.Max(0.01f, releaseDuration * 0.5f + fadeDuration);
        if (count <= 0 || feedbackIntensity <= 0f || !HasBurstLines())
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(0.8f, 0f, t) * feedbackIntensity;
            for (int i = 0; i < count; i++)
            {
                LineRenderer line = burstLines[i];
                if (line == null || !line.enabled) continue;
                line.startColor = WithAlpha(baseBeamColor, alpha);
                line.endColor = WithAlpha(baseBeamColor, alpha * 0.35f);
                line.widthMultiplier = Mathf.Lerp(burstLineWidth * 1.2f, burstLineWidth * 0.25f, t);
            }
            yield return null;
        }
    }

    private void HideBurstLines()
    {
        for (int i = 0; i < burstLines.Length; i++)
        {
            if (burstLines[i] != null)
                burstLines[i].enabled = false;
        }
    }

    /// <summary>
    /// 감소 대상 목록은 게임 규칙 데이터이므로 연출 정리(HideBurstLines)와 분리해서 비운다.
    /// (예전에는 빔 생성 실패 처리에서 이 목록까지 비워 첫 폭발이 이웃 숫자를 줄이지 못했다.)
    /// </summary>
    private void ClearAffectedTiles()
    {
        for (int i = 0; i < affectedTilesBuffer.Length; i++)
            affectedTilesBuffer[i] = null;
    }

    private static Vector3 GetCuePosition(Vector3 source)
    {
        return new Vector3(source.x, source.y, source.z - 0.05f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private static float FeedbackIntensity()
    {
        return Mathf.Clamp01(GameplayFeedback.Intensity);
    }
}
