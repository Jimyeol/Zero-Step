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
    [Tooltip("색상에 곱해 HDR 발광(Emission) 강도 (기본 1.4 = 기존 대비 30% 감소)")]
    [SerializeField] private float hdrIntensity = 1.4f;

    [Header("텐션 애니메이션")]
    [SerializeField] private float shrinkScale = 0.9f;
    [SerializeField] private float shrinkDuration = 0.05f;
    [SerializeField] private float restoreDuration = 0.08f;

    [Header("이펙트")]
    [Tooltip("숫자 감소 시 재생할 네온 스파크 파티클 (타일 색상과 동기화)")]
    public ParticleSystem hitEffect;

    [Header("시작점 하트비트")]
    [Tooltip("펄스 시 최대 스케일 배율 (기본 1.2에서 이 값까지 커졌다 줄었다)")]
    [SerializeField] private float pulsePeakScale = 1.26f;
    [SerializeField] private float pulseExpandDuration = 0.1f;
    [SerializeField] private float pulseContractDuration = 0.15f;
    [SerializeField] private float pulseInterval = 0.65f;

    // 그리드 좌표 (GameManager가 설정)
    private int gridX;
    private int gridY;
    private int currentNumber;
    /// <summary>게임오버 리셋 시 복원할 초기 숫자 (그리드 생성 시 GameManager가 설정).</summary>
    private int initialNumber;
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider2D;
    private Vector3 baseScale;
    private Coroutine scaleRoutine;
    /// <summary>시작점 하트비트(펄스) 애니메이션 코루틴.</summary>
    private Coroutine startPointPulseRoutine;

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

    /// <summary>스케일 배율: 1=기본, 1.2=초기 시작 타일, 1.1=현재 위치(멈춘 지점).</summary>
    private float scaleOverride = 1f;
    private const float InitialStartScale = 1.2f;
    private const float CurrentPositionScale = 1.1f;

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
    /// JSON 시작 타일: Scale 1.2배 + 하트비트 펄스로 '여기서 시작' 시각적 힌트.
    /// </summary>
    public void SetInitialStartTile(bool isInitial)
    {
        if (startPointPulseRoutine != null)
        {
            StopCoroutine(startPointPulseRoutine);
            startPointPulseRoutine = null;
        }
        scaleOverride = isInitial ? InitialStartScale : 1f;
        ApplyScaleOverride();
        if (isInitial)
            startPointPulseRoutine = StartCoroutine(HeartbeatPulseRoutine(InitialStartScale, pulsePeakScale));
    }

    /// <summary>
    /// 멈춘 지점 = 다음 드래그의 시작점. 1.1x 스케일 + 하트비트로 현재 위치 표시.
    /// </summary>
    public void SetCurrentPositionMarker(bool isCurrent)
    {
        if (startPointPulseRoutine != null)
        {
            StopCoroutine(startPointPulseRoutine);
            startPointPulseRoutine = null;
        }
        scaleOverride = isCurrent ? CurrentPositionScale : 1f;
        ApplyScaleOverride();
        if (isCurrent)
        {
            float peakMult = CurrentPositionScale * (pulsePeakScale / InitialStartScale);
            startPointPulseRoutine = StartCoroutine(HeartbeatPulseRoutine(CurrentPositionScale, peakMult));
        }
    }

    /// <summary>
    /// 드래그 시작 시 호출: 스케일 오버라이드 해제(1.0으로 복귀). 시작점 펄스도 중단.
    /// </summary>
    public void ClearScaleOverride()
    {
        if (startPointPulseRoutine != null)
        {
            StopCoroutine(startPointPulseRoutine);
            startPointPulseRoutine = null;
        }
        scaleOverride = 1f;
        ApplyScaleOverride();
    }

    private void ApplyScaleOverride()
    {
        transform.localScale = baseScale * scaleOverride;
    }

    /// <summary>
    /// 하트비트: restMult ~ peakMult 구간에서 살짝 커졌다 줄었다 반복. (시작점·현재 위치 공용)
    /// </summary>
    private IEnumerator HeartbeatPulseRoutine(float restMult, float peakMult)
    {
        Vector3 restScale = baseScale * restMult;
        Vector3 peakScale = baseScale * Mathf.Max(restMult, peakMult);
        WaitForEndOfFrame w = new WaitForEndOfFrame();

        while (true)
        {
            // 첫 번째 박동: 커졌다 줄었다
            float elapsed = 0f;
            while (elapsed < pulseExpandDuration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(restScale, peakScale, elapsed / pulseExpandDuration);
                yield return w;
            }
            transform.localScale = peakScale;
            elapsed = 0f;
            while (elapsed < pulseContractDuration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(peakScale, restScale, elapsed / pulseContractDuration);
                yield return w;
            }
            transform.localScale = restScale;
            // 두 번째 박동 (하트 느낌)
            elapsed = 0f;
            while (elapsed < pulseExpandDuration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(restScale, peakScale, elapsed / pulseExpandDuration);
                yield return w;
            }
            transform.localScale = peakScale;
            elapsed = 0f;
            while (elapsed < pulseContractDuration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(peakScale, restScale, elapsed / pulseContractDuration);
                yield return w;
            }
            transform.localScale = restScale;
            yield return new WaitForSeconds(pulseInterval);
        }
    }

    /// <summary>
    /// 리셋 시 복원할 초기 숫자 설정 (그리드 생성 시 GameManager가 호출).
    /// </summary>
    public void SetInitialNumber(int value)
    {
        initialNumber = Mathf.Max(0, value);
    }

    /// <summary>
    /// 게임오버 리셋: 초기 숫자로 복원하고 표시/컬라이더 재활성화.
    /// 리셋 시 색상이 쌓이지 않도록 렌더러를 먼저 초기화한 뒤 적용.
    /// 시작점은 첫 플레이와 동일한 발광만 적용(리셋 후 1.3x 중복 적용 방지).
    /// </summary>
    public void ResetToInitial()
    {
        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;
        if (tileImage != null && tileImage != spriteRenderer)
            tileImage.color = Color.white;
        if (numberText != null)
            numberText.color = Color.white;

        SetNumber(initialNumber);
        if (initialNumber > 0)
            SetActiveState(true);

        if (isStartPoint && initialNumber > 0)
            ApplyNumberColorWithoutStartBoost();
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
    /// 타일을 밟았을 때(숫자 감소) 호출. 숫자 감소 + 텐션 애니메이션 + 네온 스파크 이펙트.
    /// </summary>
    public void OnStep()
    {
        if (currentNumber <= 0)
            return;
        SetNumber(currentNumber - 1);
        PlayTensionAnimation();

        if (hitEffect != null)
        {
            var main = hitEffect.main;
            main.startColor = spriteRenderer != null ? new ParticleSystem.MinMaxGradient(spriteRenderer.color) : new ParticleSystem.MinMaxGradient(Color.white);
            hitEffect.Play();
        }
    }

    /// <summary>
    /// 숫자 1 감소 (GameManager가 호출). OnStep()으로 처리.
    /// </summary>
    public void DecreaseNumber()
    {
        OnStep();
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
        float emissionMult = hdrIntensity;
        if (isStartPoint && currentNumber > 0)
            emissionMult *= 1.3f; // 시작 타일 Emission 강화
        Color hdrColor = currentNumber > 0 ? baseColor * emissionMult : baseColor;
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

    /// <summary>
    /// 시작점 부스트(1.3x) 없이 색상만 적용. 리셋 시 첫 플레이와 동일한 발광으로 맞출 때 사용.
    /// </summary>
    private void ApplyNumberColorWithoutStartBoost()
    {
        Color baseColor = GetBaseColorForNumber(currentNumber);
        Color hdrColor = baseColor * hdrIntensity;

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
        Vector3 targetScale = baseScale * scaleOverride;
        Vector3 small = targetScale * shrinkScale;
        float elapsed = 0f;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(targetScale, small, elapsed / shrinkDuration);
            yield return null;
        }
        transform.localScale = small;

        elapsed = 0f;
        while (elapsed < restoreDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / restoreDuration;
            t = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.Lerp(small, targetScale, t);
            yield return null;
        }
        transform.localScale = baseScale * scaleOverride;
        scaleRoutine = null;
    }
}
