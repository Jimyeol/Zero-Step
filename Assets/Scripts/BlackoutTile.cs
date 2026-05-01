using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// Blackout 타일: 숫자 대신 "?" 표시, 배경은 count 네온 컬러 유지. 정적 노이즈·플리커·물음표 맥동·밟을 때 피드백.
/// </summary>
[RequireComponent(typeof(Tile))]
public class BlackoutTile : MonoBehaviour
{
    [Header("스프라이트")]
    [SerializeField] private string tileSpritePath = "Sprites/blind_curtain_tile";
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
    }

    /// <summary>
    /// 타일을 밟았을 때 호출 (GameManager). Punch Scale + 일시 탁해짐 피드백.
    /// </summary>
    public void OnStepped()
    {
        if (questionText != null && questionText.gameObject.activeInHierarchy)
        {
            questionText.transform.DOKill();
            questionText.transform.localScale = Vector3.one;
            questionText.transform.DOPunchScale(Vector3.one * punchScaleStrength, 0.25f, 4, 0.5f).SetEase(Ease.OutQuad);
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

    private void CreateNoiseOverlay()
    {
        // 데이터 손상 느낌의 정적 노이즈 텍스처 생성
        int size = 64;
        Texture2D noiseTex = new Texture2D(size, size);
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
        Sprite noiseSprite = Sprite.Create(noiseTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
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

        questionText.text = string.Empty;
        if (questionText.gameObject.activeSelf)
            questionText.gameObject.SetActive(false);
    }

    private void RefreshVisualState()
    {
        bool shouldShow = tile == null || tile.IsActive;

        if (coverObject != null && coverObject.activeSelf != shouldShow)
            coverObject.SetActive(shouldShow);
        if (noiseOverlay != null)
            noiseOverlay.enabled = shouldShow;
    }

    private void StartPulseTween()
    {
        if (questionText == null || !questionText.gameObject.activeInHierarchy) return;
        questionText.transform.localScale = Vector3.one * pulseScaleMin;
        pulseTween = questionText.transform
            .DOScale(pulseScaleMax, pulseDuration * 0.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private IEnumerator GlitchFlickerRoutine()
    {
        while (true)
        {
            float wait = Random.Range(glitchIntervalMin, glitchIntervalMax);
            yield return new WaitForSeconds(wait);

            // 50% 확률: 타일 밝기 플리커, 50%: 물음표 글리치(짧은 지직)
            if (Random.value > 0.5f)
            {
                // 타일 밝기 미세 떨림
                if (tileSpriteRenderer != null)
                {
                    Color orig = tileSpriteRenderer.color;
                    Color dim = new Color(orig.r * flickerDim, orig.g * flickerDim, orig.b * flickerDim, orig.a);
                    tileSpriteRenderer.color = dim;
                    yield return new WaitForSeconds(glitchDuration);
                    tileSpriteRenderer.color = orig;
                }
            }
            else
            {
                // 물음표 짧은 글리치 (위치/스케일 미세 지직, 숫자 노출 없음)
                if (questionText != null && questionText.gameObject.activeInHierarchy)
                {
                    Vector3 origPos = questionText.transform.localPosition;
                    Vector3 origScale = questionText.transform.localScale;
                    float elapsed = 0f;
                    while (elapsed < glitchDuration)
                    {
                        questionText.transform.localPosition = origPos + new Vector3(Random.Range(-0.02f, 0.02f), Random.Range(-0.02f, 0.02f), 0f);
                        questionText.transform.localScale = origScale * Random.Range(0.95f, 1.05f);
                        elapsed += Time.deltaTime;
                        yield return null;
                    }
                    questionText.transform.localPosition = origPos;
                    questionText.transform.localScale = origScale;
                }
            }
        }
    }

    private void LateUpdate()
    {
        HideQuestionText();
        RefreshVisualState();
        if (coverRenderer != null && tileSpriteRenderer != null)
            coverRenderer.color = tileSpriteRenderer.color;
    }
}
