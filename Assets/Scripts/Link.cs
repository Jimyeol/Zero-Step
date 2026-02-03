using System.Collections;
using UnityEngine;

/// <summary>
/// 인접 타일 사이의 연결선. 평소 어두움. 드래그 경로/체인 시 밝게 점등.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Link : MonoBehaviour
{
    [Header("점등")]
    [Tooltip("체인/펄스 시 HDR 강도 (흰색)")]
    [SerializeField] private float chainIntensity = 3f;
    [Tooltip("펄스 파동 한 번 깜빡임 간격(초)")]
    [SerializeField] private float pulseFlashInterval = 0.05f;

    private SpriteRenderer spriteRenderer;
    private Color defaultColor;
    private Coroutine chainRoutine;
    private Coroutine pulseRoutine;
    private bool isPathLit;

    /// <summary>그리드 좌표 (타일 A).</summary>
    public int Ax { get; private set; }
    /// <summary>그리드 좌표 (타일 A).</summary>
    public int Ay { get; private set; }
    /// <summary>그리드 좌표 (타일 B).</summary>
    public int Bx { get; private set; }
    /// <summary>그리드 좌표 (타일 B).</summary>
    public int By { get; private set; }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            defaultColor = spriteRenderer.color;
    }

    /// <summary>
    /// 링크가 연결하는 두 타일의 그리드 좌표 설정.
    /// </summary>
    public void SetCells(int ax, int ay, int bx, int by)
    {
        Ax = ax;
        Ay = ay;
        Bx = bx;
        By = by;
    }

    /// <summary>
    /// 평소 어두운 색 설정 (LinkSystem이 배치 시 호출).
    /// </summary>
    public void SetDefaultColor(Color color)
    {
        defaultColor = color;
        if (spriteRenderer != null && !isPathLit)
            spriteRenderer.color = color;
    }

    /// <summary>
    /// 드래그 경로 점등: 네온 컬러로 밝게.
    /// </summary>
    public void SetPathLit(Color neonColor)
    {
        isPathLit = true;
        if (spriteRenderer != null)
            spriteRenderer.color = neonColor * 1.5f;
    }

    /// <summary>
    /// 경로 점등 해제: 기본 어두운 색으로.
    /// </summary>
    public void ClearPathLit()
    {
        isPathLit = false;
        if (spriteRenderer != null)
            spriteRenderer.color = defaultColor;
    }

    /// <summary>
    /// CrossBlast 체인: 흰색 HDR로 duration간 점등 후 복구.
    /// </summary>
    public void SetChainLit(float duration)
    {
        if (chainRoutine != null)
            StopCoroutine(chainRoutine);
        chainRoutine = StartCoroutine(ChainLitRoutine(duration));
    }

    private IEnumerator ChainLitRoutine(float duration)
    {
        if (spriteRenderer != null)
            spriteRenderer.color = Color.white * chainIntensity;
        yield return new WaitForSeconds(duration);
        if (spriteRenderer != null)
            spriteRenderer.color = defaultColor;
        chainRoutine = null;
    }

    /// <summary>
    /// 펄스 파동: totalDuration 동안 짧은 간격으로 흰색 HDR ↔ 기본색 반복.
    /// </summary>
    public void SetPulseFlash(float totalDuration)
    {
        if (pulseRoutine != null)
            StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(PulseFlashRoutine(totalDuration));
    }

    private IEnumerator PulseFlashRoutine(float totalDuration)
    {
        float elapsed = 0f;
        bool lit = false;
        Color whiteHdr = Color.white * chainIntensity;
        while (elapsed < totalDuration)
        {
            if (spriteRenderer != null)
                spriteRenderer.color = lit ? whiteHdr : defaultColor;
            lit = !lit;
            elapsed += pulseFlashInterval;
            yield return new WaitForSeconds(pulseFlashInterval);
        }
        if (spriteRenderer != null)
            spriteRenderer.color = defaultColor;
        pulseRoutine = null;
    }
}
