using System.Collections;
using UnityEngine;
using DG.Tweening;
using TMPro;

/// <summary>
/// Hidden 타일: 초기엔 투명·콜라이더 꺼짐. Igniter 트리거 시 릴레이 순차 점등 후 밟을 수 있음.
/// </summary>
[RequireComponent(typeof(Tile))]
public class HiddenTile : MonoBehaviour
{
    [Header("점등 연출")]
    [Tooltip("한 타일씩 점등 간격(초). Igniter 트리거 시 GameManager에서 참조")]
    [SerializeField] private float relayInterval = 0.08f;
    /// <summary>한 타일씩 점등 간격. GameManager/IgniterTile에서 사용.</summary>
    public float RelayInterval => relayInterval;
    [Tooltip("페이드인 + 스케일 등장 시간")]
    [SerializeField] private float appearDuration = 0.2f;

    private Tile tile;
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider2D;
    private TMP_Text numberText;
    private bool isActivated;
    private Color targetColor;
    private Color targetNumberColor;

    private void Awake()
    {
        tile = GetComponent<Tile>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        boxCollider2D = GetComponent<BoxCollider2D>();
        numberText = tile != null ? tile.GetNumberText() : GetComponentInChildren<TMP_Text>(true);
    }

    /// <summary>
    /// 그리드 생성 후 GameManager가 호출. 초기 상태: 테두리·숫자 없이 완전 빈 공간처럼.
    /// </summary>
    public void Setup()
    {
        ResetToHiddenState();
    }

    /// <summary>
    /// 리셋/게임오버 시 GameManager가 호출. 다시 비활성 상태로 (테두리·숫자 없음).
    /// </summary>
    public void ResetToHiddenState()
    {
        StopAllCoroutines();
        isActivated = false;
        DOTween.Kill(transform);
        if (spriteRenderer != null) DOTween.Kill(spriteRenderer);
        if (numberText != null) DOTween.Kill(numberText.transform);
        CacheTargetVisualState();
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }
        if (boxCollider2D != null)
            boxCollider2D.enabled = false;
        if (numberText != null)
            numberText.gameObject.SetActive(false);
        transform.localScale = Vector3.one;
        if (GameManager.VerboseStage6DebugEnabled)
            Debug.Log($"[Stage6 Hidden 리셋] tile={GameManager.DescribeTileForDebug(tile)} collider={IsColliderEnabled} sprite={(spriteRenderer != null && spriteRenderer.enabled)} activated={isActivated}");
    }

    /// <summary>
    /// Igniter가 트리거 시 호출. delay 후 릴레이 점등 연출.
    /// </summary>
    public void ActivateWithDelay(float delay)
    {
        if (isActivated) return;
        isActivated = true;
        if (GameManager.VerboseStage6DebugEnabled)
            Debug.Log($"[Stage6 Hidden 예약] tile={GameManager.DescribeTileForDebug(tile)} delay={delay:F3} colliderBefore={IsColliderEnabled}");
        StartCoroutine(ActivateAfterDelayRoutine(delay));
    }

    private IEnumerator ActivateAfterDelayRoutine(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        CacheTargetVisualState();

        if (boxCollider2D != null)
            boxCollider2D.enabled = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.color = new Color(targetColor.r, targetColor.g, targetColor.b, 0f);
            spriteRenderer.DOFade(targetColor.a, appearDuration).SetEase(Ease.OutQuad);
            transform.DOScale(transform.localScale * 1.15f, appearDuration * 0.5f).SetEase(Ease.OutQuad)
                .OnComplete(() => transform.DOScale(transform.localScale / 1.15f, appearDuration * 0.5f).SetEase(Ease.InQuad));
        }
        if (numberText != null)
        {
            numberText.gameObject.SetActive(true);
            StartCoroutine(FadeInNumberTextRoutine());
        }
        if (GameManager.VerboseStage6DebugEnabled)
            Debug.Log($"[Stage6 Hidden 활성화] tile={GameManager.DescribeTileForDebug(tile)} delay={delay:F3} collider={IsColliderEnabled} sprite={(spriteRenderer != null && spriteRenderer.enabled)} activated={isActivated}");
    }

    private void CacheTargetVisualState()
    {
        if (tile != null)
            tile.RestoreNeonColor();

        if (spriteRenderer != null)
            targetColor = spriteRenderer.color;
        else
            targetColor = Color.white;

        if (numberText != null)
            targetNumberColor = numberText.color;
        else
            targetNumberColor = Color.white;
    }

    private IEnumerator FadeInNumberTextRoutine()
    {
        if (numberText == null) yield break;
        Color c = targetNumberColor;
        c.a = 0f;
        numberText.color = c;
        float elapsed = 0f;
        while (elapsed < appearDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Clamp01(elapsed / appearDuration);
            numberText.color = c;
            yield return null;
        }
        c.a = 1f;
        numberText.color = c;
    }

    public bool IsActivated => isActivated;
    public bool IsColliderEnabled => boxCollider2D != null && boxCollider2D.enabled;
}
