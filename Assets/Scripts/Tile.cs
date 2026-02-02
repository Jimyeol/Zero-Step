using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 퍼즐 타일. 터치 시 숫자가 1씩 줄어들고 0이 되면 비활성화됨.
/// 숫자별 HDR 색상(5~4 핑크, 3~2 민트, 1 하늘색) 및 터치 시 스케일 애니메이션.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class Tile : MonoBehaviour, IPointerDownHandler
{
    [Header("참조")]
    [SerializeField] private TMP_Text numberText;

    [Header("터치 애니메이션")]
    [SerializeField] private float shrinkScale = 0.9f;
    [SerializeField] private float shrinkDuration = 0.05f;
    [SerializeField] private float restoreDuration = 0.08f;

    private int currentNumber;
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider2D;
    private Vector3 baseScale;
    private Coroutine scaleRoutine;
    private float lastDecreaseTime = -1f;
    private const float DecreaseCooldown = 0.2f;

    // 숫자별 HDR 색상 (Intensity > 1 로 발광 느낌): 5~4 핑크, 3~2 민트, 1 하늘색
    private static readonly Color Color5to4 = new Color(2f, 0.4f, 0.8f, 1f);   // 핑크
    private static readonly Color Color3to2 = new Color(0.2f, 2f, 1.2f, 1f);   // 민트
    private static readonly Color Color1 = new Color(0.3f, 0.8f, 2f, 1f);      // 하늘색

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        boxCollider2D = GetComponent<BoxCollider2D>();
        baseScale = transform.localScale;

        if (numberText == null)
            numberText = GetComponentInChildren<TMP_Text>(true);
    }

    /// <summary>
    /// 타일 숫자 설정. 0이면 비활성화. 숫자에 따라 HDR 색상 자동 적용.
    /// </summary>
    public void SetNumber(int value)
    {
        currentNumber = Mathf.Max(0, value);
        UpdateNumberDisplay();
        ApplyNumberColor();

        if (currentNumber <= 0)
            SetActiveState(false);
    }

    /// <summary>
    /// 현재 숫자 반환.
    /// </summary>
    public int GetNumber() => currentNumber;

    /// <summary>
    /// 숫자가 0보다 큰지(활성 상태인지) 반환.
    /// </summary>
    public bool IsActive() => currentNumber > 0;

    /// <summary>
    /// 터치/클릭 시 숫자 1 감소 + 스케일 애니메이션. (EventSystem 또는 GridManager 폴백에서 호출)
    /// </summary>
    public void TryDecreaseNumber()
    {
        if (currentNumber <= 0)
            return;
        if (Time.time - lastDecreaseTime < DecreaseCooldown)
            return;

        lastDecreaseTime = Time.time;
        SetNumber(currentNumber - 1);
        PlayTapAnimation();
    }

    private void UpdateNumberDisplay()
    {
        if (numberText != null)
        {
            numberText.text = currentNumber.ToString();
            numberText.ForceMeshUpdate(true, true);
        }
    }

    /// <summary>
    /// 숫자 구간별 HDR 색상을 타일(SpriteRenderer)과 텍스트(TMP face + glow)에 적용.
    /// </summary>
    private void ApplyNumberColor()
    {
        Color hdr = GetColorForNumber(currentNumber);

        if (spriteRenderer != null)
            spriteRenderer.color = hdr;

        if (numberText != null)
        {
            numberText.color = hdr;
            ApplyTMPGlow(numberText, hdr);
        }
    }

    private static Color GetColorForNumber(int n)
    {
        if (n >= 4) return Color5to4;
        if (n >= 2) return Color3to2;
        if (n >= 1) return Color1;
        return Color.gray;
    }

    /// <summary>
    /// TMP Glow 활성화 및 Glow 색상을 타일과 동일한 HDR로 설정.
    /// </summary>
    private static void ApplyTMPGlow(TMP_Text tmp, Color hdrColor)
    {
        if (tmp == null)
            return;

        Material mat = tmp.fontSharedMaterial;
        if (mat == null || !mat.HasProperty(ShaderUtilities.ID_GlowColor))
            return;

        Material instanceMat = tmp.fontMaterial;
        if (instanceMat != null)
        {
            instanceMat.EnableKeyword(ShaderUtilities.Keyword_Glow);
            instanceMat.SetColor(ShaderUtilities.ID_GlowColor, hdrColor);
            instanceMat.SetFloat(ShaderUtilities.ID_GlowOffset, 0f);
            instanceMat.SetFloat(ShaderUtilities.ID_GlowPower, 0.5f);
            instanceMat.SetFloat(ShaderUtilities.ID_GlowOuter, 0.4f);
            instanceMat.SetFloat(ShaderUtilities.ID_GlowInner, 0.05f);
        }
    }

    private void SetActiveState(bool active)
    {
        if (spriteRenderer != null)
            spriteRenderer.enabled = active;
        if (boxCollider2D != null)
            boxCollider2D.enabled = active;
        if (numberText != null)
            numberText.gameObject.SetActive(active);
    }

    /// <summary>
    /// 코루틴: 0.9x 수축 후 1.0x로 복귀 (LeanTween/DOTween 없이).
    /// </summary>
    private void PlayTapAnimation()
    {
        if (scaleRoutine != null)
            StopCoroutine(scaleRoutine);
        scaleRoutine = StartCoroutine(ScaleBounceRoutine());
    }

    private IEnumerator ScaleBounceRoutine()
    {
        Vector3 small = baseScale * shrinkScale;
        float elapsed = 0f;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(baseScale, small, elapsed / shrinkDuration);
            yield return null;
        }
        transform.localScale = small;

        elapsed = 0f;
        while (elapsed < restoreDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / restoreDuration;
            t = 1f - (1f - t) * (1f - t); // EaseOutQuad
            transform.localScale = Vector3.Lerp(small, baseScale, t);
            yield return null;
        }
        transform.localScale = baseScale;
        scaleRoutine = null;
    }

    /// <summary>
    /// 터치/클릭 시 숫자 1 감소 (IPointerDownHandler).
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        TryDecreaseNumber();
    }
}
