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
    /// <summary>BlindCurtain 밟은 후: 모든 타일 숫자를 ?로만 표시. 리셋 시 false로 복원.</summary>
    private bool displayAsQuestion;
    /// <summary>TwinLink 타일용. 설정 시 숫자 색상/발광을 이 색으로 고정.</summary>
    private Color? numberColorOverride;

    /// <summary>타일 -1 시 파티클 개수 배율 (1.3 = 30% 증가).</summary>
    private const float HitEffectParticleCountScale = 1.3f;

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

        // hitEffect 파티클 개수를 1.3배로 적용 (burst 한 번만 스케일)
        ScaleHitEffectEmission(HitEffectParticleCountScale);
    }

    /// <summary>hitEffect의 emission burst 개수를 지정 배율로 한 번 스케일.</summary>
    private void ScaleHitEffectEmission(float scale)
    {
        if (hitEffect == null || scale <= 0f) return;
        var emission = hitEffect.emission;
        if (!emission.enabled) return;
        int burstCount = emission.burstCount;
        if (burstCount == 0) return;
        var bursts = new ParticleSystem.Burst[burstCount];
        emission.GetBursts(bursts);
        for (int i = 0; i < burstCount; i++)
        {
            ParticleSystem.Burst b = bursts[i];
            var countCurve = b.count;
            float minC = countCurve.constantMin;
            float maxC = countCurve.constantMax;
            b.count = new ParticleSystem.MinMaxCurve(Mathf.Max(1f, minC * scale), Mathf.Max(1f, maxC * scale));
            bursts[i] = b;
        }
        emission.SetBursts(bursts);
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
    /// 게임오버 암전: 색상을 검정으로, 숫자 텍스트 비활성화.
    /// </summary>
    public void SetBlackout(bool blackout)
    {
        if (blackout)
        {
            if (spriteRenderer != null) spriteRenderer.color = Color.black;
            if (tileImage != null && tileImage != spriteRenderer) tileImage.color = Color.black;
            if (numberText != null) numberText.gameObject.SetActive(false);
            if (startLabel != null) startLabel.gameObject.SetActive(false);
        }
        else
        {
            ApplyNumberColor();
            if (numberText != null && initialNumber > 0) numberText.gameObject.SetActive(true);
            if (startLabel != null && isStartPoint) startLabel.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 게임오버 연출 후 네온 색 복구 (깜빡임 사이).
    /// </summary>
    public void RestoreNeonColor()
    {
        ApplyNumberColor();
    }

    /// <summary>
    /// 게임오버 연출: 지정 색으로 변경 (깜빡임·암전용).
    /// </summary>
    public void SetGlitchColor(Color c)
    {
        if (spriteRenderer != null) spriteRenderer.color = c;
        if (tileImage != null && tileImage != spriteRenderer) tileImage.color = c;
        if (numberText != null) { numberText.color = c; ApplyTMPGlow(numberText, c); }
    }

    /// <summary>
    /// 리셋 연출: 스케일을 0으로 (순차 등장 전).
    /// </summary>
    public void SetScaleZero()
    {
        if (startPointPulseRoutine != null) { StopCoroutine(startPointPulseRoutine); startPointPulseRoutine = null; }
        if (scaleRoutine != null) { StopCoroutine(scaleRoutine); scaleRoutine = null; }
        transform.localScale = Vector3.zero;
    }

    /// <summary>
    /// 리셋 연출: 나타나는 순간 네온 컬러·텍스트 복구 후 0 → 1.2 → 1.0 Bounce 코루틴.
    /// </summary>
    public void PlayBounceAppearance()
    {
        if (scaleRoutine != null) StopCoroutine(scaleRoutine);
        if (startPointPulseRoutine != null) { StopCoroutine(startPointPulseRoutine); startPointPulseRoutine = null; }
        scaleOverride = 1f;
        ApplyNumberColor();
        if (initialNumber > 0) SetActiveState(true);
        if (numberText != null) numberText.gameObject.SetActive(true);
        if (startLabel != null && isStartPoint) startLabel.gameObject.SetActive(true);
        scaleRoutine = StartCoroutine(BounceAppearanceRoutine());
    }

    private IEnumerator BounceAppearanceRoutine()
    {
        Vector3 zero = Vector3.zero;
        Vector3 peak = baseScale * 1.2f;
        float expandDur = 0.18f;
        float contractDur = 0.12f;
        float elapsed = 0f;
        while (elapsed < expandDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / expandDur;
            t = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.Lerp(zero, peak, t);
            yield return null;
        }
        transform.localScale = peak;
        elapsed = 0f;
        while (elapsed < contractDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / contractDur;
            t = t * t;
            transform.localScale = Vector3.Lerp(peak, baseScale, t);
            yield return null;
        }
        transform.localScale = baseScale;
        scaleRoutine = null;
    }

    /// <summary>
    /// 하트비트: restMult ~ peakMult 구간에서 살짝 커졌다 줄었다 반복. (시작점·현재 위치 공용)
    /// </summary>
    private IEnumerator HeartbeatPulseRoutine(float restMult, float peakMult)
    {
        Vector3 restScale = baseScale * restMult;
        Vector3 peakScale = baseScale * Mathf.Max(restMult, peakMult);
        WaitForSeconds pulseWait = new WaitForSeconds(pulseInterval);

        while (true)
        {
            float elapsed = 0f;
            while (elapsed < pulseExpandDuration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(restScale, peakScale, elapsed / pulseExpandDuration);
                yield return null;
            }
            transform.localScale = peakScale;
            elapsed = 0f;
            while (elapsed < pulseContractDuration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(peakScale, restScale, elapsed / pulseContractDuration);
                yield return null;
            }
            transform.localScale = restScale;
            elapsed = 0f;
            while (elapsed < pulseExpandDuration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(restScale, peakScale, elapsed / pulseExpandDuration);
                yield return null;
            }
            transform.localScale = peakScale;
            elapsed = 0f;
            while (elapsed < pulseContractDuration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(peakScale, restScale, elapsed / pulseContractDuration);
                yield return null;
            }
            transform.localScale = restScale;
            yield return pulseWait;
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
        displayAsQuestion = false;
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

        // 1→0일 때도 파티클이 2→1처럼 선명하게: SetNumber(0) 후에는 타일 색이 어두워지므로,
        // 감소 전 밝은 색을 미리 저장해 두고 파티클에 적용.
        Color particleColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

        SetNumber(currentNumber - 1);
        PlayTensionAnimation();

        if (hitEffect != null)
        {
            var main = hitEffect.main;
            main.startColor = new ParticleSystem.MinMaxGradient(particleColor);
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
        if (numberText == null) return;
        // Blackout 타일 또는 BlindCurtain으로 인한 전체 ? 모드: 숫자 노출 금지
        if (displayAsQuestion || GetComponent<BlackoutTile>() != null)
        {
            numberText.text = "?";
        }
        else
        {
            numberText.text = currentNumber.ToString();
        }
        numberText.ForceMeshUpdate(true, true);
    }

    /// <summary>BlindCurtain 밟을 때: 모든 타일 숫자를 ?로 표시. 리셋 시 GameManager가 false로 복원.</summary>
    public void SetDisplayAsQuestion(bool showAsQuestion)
    {
        if (displayAsQuestion == showAsQuestion) return;
        displayAsQuestion = showAsQuestion;
        UpdateNumberDisplay();
    }

    /// <summary>BlackoutTile 등에서 물음표 텍스트 참조용.</summary>
    public TMP_Text GetNumberText() => numberText;

    /// <summary>TwinLink 타일용. 숫자/발광 색상을 지정 색으로 고정. null이면 숫자별 기본 색상 사용.</summary>
    public void SetNumberColorOverride(Color? color)
    {
        numberColorOverride = color;
    }

    /// <summary>
    /// 숫자별 색상 적용: 4+ 핑크, 2~3 민트, 1 하늘색. HDR 발광(Color * 2.0). 0이면 어두운 회색 후 꺼짐.
    /// </summary>
    private void ApplyNumberColor()
    {
        Color hdrColor;
        if (numberColorOverride.HasValue && currentNumber > 0)
        {
            hdrColor = numberColorOverride.Value * hdrIntensity;
            if (isStartPoint) hdrColor *= 1.3f * StartPointTint;
        }
        else
        {
            Color baseColor = GetBaseColorForNumber(currentNumber);
            float emissionMult = hdrIntensity;
            if (isStartPoint && currentNumber > 0)
                emissionMult *= 1.3f; // 시작 타일 Emission 강화
            hdrColor = currentNumber > 0 ? baseColor * emissionMult : baseColor;
            if (isStartPoint && currentNumber > 0)
                hdrColor *= StartPointTint;
        }

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
        Color hdrColor;
        if (numberColorOverride.HasValue && currentNumber > 0)
        {
            hdrColor = numberColorOverride.Value * hdrIntensity;
            if (spriteRenderer != null) spriteRenderer.color = hdrColor;
            if (tileImage != null && tileImage != spriteRenderer) tileImage.color = hdrColor;
            if (numberText != null) { numberText.color = hdrColor; ApplyTMPGlow(numberText, hdrColor); }
            return;
        }
        Color baseColor = GetBaseColorForNumber(currentNumber);
        hdrColor = baseColor * hdrIntensity;

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
