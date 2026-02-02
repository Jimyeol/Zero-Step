using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 네온 퍼즐 타일. 그리드 좌표(x,y), 숫자별 색상(4+ 핑크, 2~3 민트, 1 하늘색), HDR 발광, 0 시 꺼짐.
/// 숫자 감소 시 코루틴으로 0.9x → 1.0x 텐션 애니메이션.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class Tile : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private SpriteRenderer tileImage;
    [Tooltip("시작점일 때 표시할 텍스트(선택). 없으면 테두리 색으로만 표시")]
    [SerializeField] private TMP_Text startLabel;

    [Header("발광")]
    [Tooltip("색상에 곱해 HDR 발광(Emission) 강도")]
    [SerializeField] private float hdrIntensity = 2f;

    [Header("텐션 애니메이션")]
    [SerializeField] private float shrinkScale = 0.9f;
    [SerializeField] private float shrinkDuration = 0.05f;
    [SerializeField] private float restoreDuration = 0.08f;

    // 그리드 좌표 (GameManager가 설정)
    private int gridX;
    private int gridY;
    private int currentNumber;
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider2D;
    private Vector3 baseScale;
    private Coroutine scaleRoutine;

    // 요구 색상: 4+ 핑크(#FF00FF), 2~3 민트(#00FFCC), 1 하늘색(#87CEFA), 0 어두운 회색
    private static readonly Color Pink = new Color(1f, 0f, 1f, 1f);           // #FF00FF
    private static readonly Color Mint = new Color(0f, 1f, 0.8f, 1f);         // #00FFCC
    private static readonly Color SkyBlue = new Color(0.53f, 0.81f, 0.98f, 1f); // #87CEFA
    private static readonly Color DarkGrayOff = new Color(0.2f, 0.2f, 0.2f, 1f);

    public int X => gridX;
    public int Y => gridY;
    public int CurrentNumber => currentNumber;
    public bool IsActive => currentNumber > 0;

    private bool isStartPoint;
    private static readonly Color StartPointTint = new Color(0.9f, 1f, 0.9f, 1f);

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (tileImage == null)
            tileImage = spriteRenderer;
        boxCollider2D = GetComponent<BoxCollider2D>();
        baseScale = transform.localScale;

        if (numberText == null)
            numberText = GetComponentInChildren<TMP_Text>(true);
    }

    /// <summary>
    /// 그리드 좌표 설정 (GameManager가 생성 시 호출).
    /// </summary>
    public void SetGridPosition(int x, int y)
    {
        gridX = x;
        gridY = y;
    }

    /// <summary>
    /// 시작점 표시: 'Start' 텍스트 표시 또는 테두리(스프라이트 틴트) 적용.
    /// </summary>
    public void SetAsStartPoint(bool isStart)
    {
        isStartPoint = isStart;
        if (startLabel != null)
            startLabel.gameObject.SetActive(isStart);
    }

    /// <summary>
    /// 숫자 설정. 0이면 어두운 회색으로 꺼짐. 색상은 숫자별 + HDR 발광.
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
    /// 숫자 1 감소 (드래그 경로 적용 시 GameManager가 호출). 텐션 애니메이션 재생.
    /// </summary>
    public void DecreaseNumber()
    {
        if (currentNumber <= 0)
            return;
        SetNumber(currentNumber - 1);
        PlayTensionAnimation();
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
    /// 숫자별 색상 적용: 4+ 핑크, 2~3 민트, 1 하늘색. HDR 발광(Color * 2.0). 0이면 어두운 회색 후 꺼짐.
    /// </summary>
    private void ApplyNumberColor()
    {
        Color baseColor = GetBaseColorForNumber(currentNumber);
        Color hdrColor = currentNumber > 0 ? baseColor * hdrIntensity : baseColor;
        if (isStartPoint && currentNumber > 0)
            hdrColor *= StartPointTint;

        if (spriteRenderer != null)
            spriteRenderer.color = hdrColor;
        if (tileImage != null && tileImage != spriteRenderer)
            tileImage.color = hdrColor;

        if (numberText != null)
        {
            numberText.color = hdrColor;
            ApplyTMPGlow(numberText, hdrColor);
        }
    }

    private static Color GetBaseColorForNumber(int n)
    {
        if (n >= 4) return Pink;
        if (n >= 2) return Mint;
        if (n >= 1) return SkyBlue;
        return DarkGrayOff;
    }

    /// <summary>
    /// TMP Glow Color를 타일 색상과 동기화 (Orbitron SDF 등).
    /// </summary>
    private static void ApplyTMPGlow(TMP_Text tmp, Color hdrColor)
    {
        if (tmp == null) return;
        Material mat = tmp.fontSharedMaterial;
        if (mat == null || !mat.HasProperty(ShaderUtilities.ID_GlowColor)) return;

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
    /// 코루틴: 0.9x 수축 후 1.0x로 빠르게 복구 (텐션 애니메이션). 외부 라이브러리 없음.
    /// </summary>
    private void PlayTensionAnimation()
    {
        if (scaleRoutine != null)
            StopCoroutine(scaleRoutine);
        scaleRoutine = StartCoroutine(ScaleTensionRoutine());
    }

    private IEnumerator ScaleTensionRoutine()
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
            t = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.Lerp(small, baseScale, t);
            yield return null;
        }
        transform.localScale = baseScale;
        scaleRoutine = null;
    }
}
