using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// Blackout 타일: 숫자 대신 "?" 표시, 배경은 count 네온 컬러 유지. 정적 노이즈·플리커·물음표 맥동·밟을 때 피드백.
/// </summary>
[RequireComponent(typeof(Tile))]
public class BlackoutTile : MonoBehaviour, IGameOverBlackoutVisual
{
    [Header("스프라이트")]
    [SerializeField] private string tileSpritePath = "Sprites/black_out_tile";
    [SerializeField] private float coverScale = 0.7f;

    [Header("노이즈·플리커")]
    [Tooltip("정적 노이즈 밝기 (0~1)")]
    [SerializeField] private float noiseAlpha = 0.15f;
    [Tooltip("플리커/글리치 간격(초) 3~5초 랜덤")]
    [SerializeField] private float glitchIntervalMin = 3f;
    [SerializeField] private float glitchIntervalMax = 5f;
    [Tooltip("플리커 시 밝기 감소 비율")]
    [SerializeField] private float flickerDim = 0.75f;
    [Tooltip("글리치 지속 시간(초)")]
    [SerializeField] private float glitchDuration = 0.08f;

    [Header("물음표 맥동 (DOTween)")]
    [Tooltip("맥동 스케일 최소~최대")]
    [SerializeField] private float pulseScaleMin = 1f;
    [SerializeField] private float pulseScaleMax = 1.12f;
    [Tooltip("한 번 맥동 시간(초)")]
    [SerializeField] private float pulseDuration = 1.2f;

    [Header("밟을 때 피드백")]
    [Tooltip("Punch Scale 강도")]
    [SerializeField] private float punchScaleStrength = 0.3f;
    [Tooltip("일시 탁해짐 비율 (1=원색)")]
    [SerializeField] private float dullColorMult = 0.6f;
    [SerializeField] private float dullDuration = 0.15f;

    private Tile tile;
    private TMP_Text questionText;
    private SpriteRenderer tileSpriteRenderer;
    private SpriteRenderer coverRenderer;
    private SpriteRenderer noiseOverlay;
    private GameObject coverObject;
    private Color baseNoiseColor;
    private Coroutine glitchRoutine;
    private Tween pulseTween;
    private bool isStepped;
    private bool gameOverBlackoutVisualActive;
    private Texture2D noiseTexture;
    private Sprite noiseSprite;

    private void Awake()
    {
        tile = GetComponent<Tile>();
        questionText = tile != null ? tile.GetNumberText() : GetComponentInChildren<TMP_Text>(true);
        tileSpriteRenderer = GetComponent<SpriteRenderer>();
        CreateCoverSprite();
        CreateNoiseOverlay();
        EnsureOverlaySorting();
        RefreshVisualState();
    }

    private void Start()
    {
        HideQuestionText();
        StartPulseTween();
        glitchRoutine = StartCoroutine(GlitchFlickerRoutine());
    }

    private void OnDestroy()
    {
        pulseTween?.Kill();
        if (glitchRoutine != null)
            StopCoroutine(glitchRoutine);
        if (coverObject != null)
            Destroy(coverObject);
        // 스테이지마다 새로 만드는 노이즈 텍스처/스프라이트는 직접 해제해야 누수가 없다.
        if (noiseSprite != null)
            Destroy(noiseSprite);
        if (noiseTexture != null)
            Destroy(noiseTexture);
    }

    /// <summary>
    /// 타일을 밟았을 때 호출 (GameManager). Punch Scale + 일시 탁해짐 피드백.
    /// </summary>
    public void OnStepped()
    {
        if (IsInGameOverBlackout())
            return;

        Transform icon = GetAnimatedIconTransform();
        if (icon != null && GameplayFeedback.Intensity > 0f && !VisualTheme.IsSoftClay)
        {
            icon.DOKill();
            icon.localScale = Vector3.one * coverScale;
            icon.DOPunchScale(Vector3.one * coverScale * punchScaleStrength * GameplayFeedback.Intensity, 0.25f, 4, 0.5f).SetEase(Ease.OutQuad);
        }
        if (tileSpriteRenderer != null && !isStepped)
        {
            isStepped = true;
            Color original = tileSpriteRenderer.color;
            Color dull = new Color(original.r * dullColorMult, original.g * dullColorMult, original.b * dullColorMult, original.a);
            tileSpriteRenderer.DOColor(dull, glitchDuration).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                tileSpriteRenderer.DOColor(original, dullDuration).SetEase(Ease.OutQuad).OnComplete(() => isStepped = false);
            });
        }
    }

    private void CreateCoverSprite()
    {
        if (tileSpriteRenderer == null || coverObject != null)
            return;
        if (VisualTheme.IsSoftClay)
        {
            CreateClayCover();
            return;
        }

        Sprite tileSprite = Resources.Load<Sprite>(tileSpritePath);
        if (tileSprite == null)
        {
            Debug.LogWarning($"[BlackoutTile] Resources/{tileSpritePath} 을(를) 찾을 수 없습니다.");
            return;
        }

        coverObject = new GameObject("BlackoutCover");
        coverObject.transform.SetParent(transform);
        coverObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        coverObject.transform.localRotation = Quaternion.identity;
        coverObject.transform.localScale = Vector3.one * coverScale;

        coverRenderer = coverObject.AddComponent<SpriteRenderer>();
        coverRenderer.sprite = tileSprite;
        coverRenderer.sortingOrder = tileSpriteRenderer.sortingOrder + 1;
        if (tileSpriteRenderer.sharedMaterial != null)
            coverRenderer.sharedMaterial = tileSpriteRenderer.sharedMaterial;
        coverRenderer.color = tileSpriteRenderer.color;
    }

    /// <summary>점토 테마: 숫자 대신 가운데 잉크색 달 아이콘(지직거림·노이즈 없음).</summary>
    private void CreateClayCover()
    {
        Sprite moon = VisualTheme.IconSprite(VisualTheme.Icon.Moon);
        if (moon == null)
            return;
        coverObject = new GameObject("BlackoutCover");
        coverObject.transform.SetParent(transform, false);
        coverObject.transform.localPosition = new Vector3(0f, 0.06f, -0.01f);
        float tileWorld = tileSpriteRenderer.sprite != null ? tileSpriteRenderer.sprite.bounds.size.x : 5.12f;
        coverObject.transform.localScale = Vector3.one * (tileWorld * 0.42f / Mathf.Max(0.0001f, moon.bounds.size.x));
        coverRenderer = coverObject.AddComponent<SpriteRenderer>();
        coverRenderer.sprite = moon;
        coverRenderer.sortingLayerID = tileSpriteRenderer.sortingLayerID;
        coverRenderer.sortingOrder = tileSpriteRenderer.sortingOrder + 1;
        Color ink = VisualTheme.Ink;
        ink.a = 0.82f;
        coverRenderer.color = ink;
        coverObject.AddComponent<ClayCornerIcon>();
    }

    private void CreateNoiseOverlay()
    {
        if (VisualTheme.IsSoftClay)
            return;
        // 데이터 손상 느낌의 정적 노이즈 텍스처 생성
        int size = 64;
        Texture2D noiseTex = new Texture2D(size, size);
        noiseTexture = noiseTex;
        noiseTex.filterMode = FilterMode.Point;
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            float g = Random.Range(0.3f, 0.7f);
            pixels[i] = new Color(g, g, g, noiseAlpha * Random.Range(0.5f, 1f));
        }
        noiseTex.SetPixels(pixels);
        noiseTex.Apply();

        GameObject overlayGo = new GameObject("BlackoutNoise");
        overlayGo.transform.SetParent(transform);
        overlayGo.transform.localPosition = Vector3.zero;
        overlayGo.transform.localScale = Vector3.one;
        overlayGo.transform.localRotation = Quaternion.identity;

        noiseOverlay = overlayGo.AddComponent<SpriteRenderer>();
        noiseSprite = Sprite.Create(noiseTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        noiseOverlay.sprite = noiseSprite;
        noiseOverlay.color = new Color(1f, 1f, 1f, noiseAlpha);
        noiseOverlay.sortingOrder = tileSpriteRenderer != null ? tileSpriteRenderer.sortingOrder + 2 : 2;
        baseNoiseColor = noiseOverlay.color;
    }

    private void EnsureOverlaySorting()
    {
        if (questionText == null)
            return;

        Renderer questionRenderer = questionText.GetComponent<Renderer>();
        if (questionRenderer != null && tileSpriteRenderer != null)
            questionRenderer.sortingOrder = tileSpriteRenderer.sortingOrder + 3;
    }

    private void HideQuestionText()
    {
        if (questionText == null)
            return;

        if (questionText.gameObject.activeSelf)
        {
            questionText.text = string.Empty;
            questionText.gameObject.SetActive(false);
        }
    }

    private void RefreshVisualState()
    {
        if (IsInGameOverBlackout())
        {
            HideGameOverBlackoutOwnedVisuals();
            return;
        }

        bool shouldShow = tile == null || tile.IsActive;

        if (coverObject != null && coverObject.activeSelf != shouldShow)
            coverObject.SetActive(shouldShow);
        if (noiseOverlay != null)
            noiseOverlay.enabled = shouldShow;
        if (shouldShow)
            EnsureGlitchRoutine();
    }

    private void StartPulseTween()
    {
        if (IsInGameOverBlackout())
            return;

        if (pulseTween != null && pulseTween.IsActive())
            return;

        Transform icon = GetAnimatedIconTransform();
        if (VisualTheme.IsSoftClay || icon == null || !icon.gameObject.activeInHierarchy || GameplayFeedback.Intensity <= 0f)
            return;
        float amplitude = (pulseScaleMax - pulseScaleMin) * GameplayFeedback.Intensity;
        icon.localScale = Vector3.one * coverScale * pulseScaleMin;
        pulseTween = icon
            .DOScale(coverScale * (pulseScaleMin + amplitude), pulseDuration * 0.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private IEnumerator GlitchFlickerRoutine()
    {
        while (true)
        {
            float wait = Random.Range(glitchIntervalMin, glitchIntervalMax);
            yield return new WaitForSeconds(wait);
            // 효과 강도 Minimal에서는 깜빡임을 완전히 끈다(광과민 대응).
            if (IsInGameOverBlackout() || GameplayFeedback.Intensity <= 0f || VisualTheme.IsSoftClay)
                continue;

            // 50% 확률: 타일 밝기 플리커, 50%: 물음표 글리치(짧은 지직)
            if (Random.value > 0.5f)
            {
                // 타일 밝기 미세 떨림
                if (tileSpriteRenderer != null)
                {
                    Color orig = tileSpriteRenderer.color;
                    float dimFactor = Mathf.Lerp(1f, flickerDim, GameplayFeedback.Intensity);
                    Color dim = new Color(orig.r * dimFactor, orig.g * dimFactor, orig.b * dimFactor, orig.a);
                    tileSpriteRenderer.color = dim;
                    yield return new WaitForSeconds(glitchDuration);
                    tileSpriteRenderer.color = orig;
                }
            }
            else
            {
                // 물음표 짧은 글리치 (위치/스케일 미세 지직, 숫자 노출 없음)
                Transform icon = GetAnimatedIconTransform();
                if (icon != null && icon.gameObject.activeInHierarchy)
                {
                    Vector3 origPos = icon.localPosition;
                    float jitter = 0.02f * GameplayFeedback.Intensity;
                    float elapsed = 0f;
                    while (elapsed < glitchDuration)
                    {
                        icon.localPosition = origPos + new Vector3(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter), 0f);
                        elapsed += Time.deltaTime;
                        yield return null;
                    }
                    icon.localPosition = origPos;
                }
            }
        }
    }

    private void LateUpdate()
    {
        HideQuestionText();
        if (IsInGameOverBlackout())
        {
            HideGameOverBlackoutOwnedVisuals();
            return;
        }

        RefreshVisualState();
        if (coverRenderer != null && tileSpriteRenderer != null && !VisualTheme.IsSoftClay)
            coverRenderer.color = tileSpriteRenderer.color;
    }

    public void SetGameOverBlackoutVisual(bool active)
    {
        gameOverBlackoutVisualActive = active;
        if (active)
        {
            StopBlackoutTransientVisuals();
            HideGameOverBlackoutOwnedVisuals();
            return;
        }

        RestoreGameOverBlackoutOwnedVisuals();
    }

    private void StopBlackoutTransientVisuals()
    {
        pulseTween?.Kill();
        pulseTween = null;
        StopGlitchRoutine();
        isStepped = false;

        if (coverObject != null)
        {
            coverObject.transform.DOKill();
            coverObject.transform.localScale = Vector3.one * coverScale;
        }

        if (tileSpriteRenderer != null)
            tileSpriteRenderer.DOKill();
    }

    private void HideGameOverBlackoutOwnedVisuals()
    {
        HideQuestionText();
        if (coverObject != null && coverObject.activeSelf)
            coverObject.SetActive(false);
        if (noiseOverlay != null)
            noiseOverlay.enabled = false;
    }

    private void RestoreGameOverBlackoutOwnedVisuals()
    {
        HideQuestionText();
        RefreshVisualState();
        StartPulseTween();
    }

    private void EnsureGlitchRoutine()
    {
        if (glitchRoutine == null && isActiveAndEnabled && !IsInGameOverBlackout())
            glitchRoutine = StartCoroutine(GlitchFlickerRoutine());
    }

    private void StopGlitchRoutine()
    {
        if (glitchRoutine != null)
        {
            StopCoroutine(glitchRoutine);
            glitchRoutine = null;
        }
    }

    /// <summary>"?" 아이콘(커버 스프라이트). 숫자 텍스트는 항상 숨기므로 연출은 아이콘에 적용한다.</summary>
    private Transform GetAnimatedIconTransform()
    {
        return coverObject != null ? coverObject.transform : null;
    }

    private bool IsInGameOverBlackout()
    {
        return gameOverBlackoutVisualActive || (tile != null && tile.IsGameOverBlackoutActive);
    }
}
