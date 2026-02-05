using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// 고정 매듭(FixedKnot) 타일: 반드시 targetOrder 번째 스텝에만 진입 가능.
/// 표시·진입 판정은 GameManager의 단일 경로 리스트(CurrentPathList) Count만 참조. 별도 카운터 없음.
/// </summary>
[RequireComponent(typeof(Tile))]
public class FixedKnotTile : MonoBehaviour
{
    [Header("기어 아이콘")]
    [Tooltip("Resources 경로 (Sprites/gear)")]
    [SerializeField] private string gearSpritePath = "Sprites/gear";
    [Tooltip("기어 스케일 (타일 중앙에 맞게, 크게 보이도록)")]
    [SerializeField] private float gearScale = 0.85f;
    [Tooltip("기어 안 숫자 폰트 크기 (처음부터 크게 보이도록)")]
    [SerializeField] private float orderFontSize = 14f;
    [Tooltip("기어 안 숫자 로컬 스케일 (처음부터 동일 크기 유지)")]
    [SerializeField] private float orderTextScale = 1.2f;
    [Tooltip("경로 타일 수가 늘어날 때마다 기어 Z축 회전 각도(도, 시계방향)")]
    [SerializeField] private float gearZRotationPerStep = 36f;
    [Tooltip("count 0 될 때 기어 흔들림 강도")]
    [SerializeField] private float unlockShakeStrength = 0.2f;
    [Tooltip("기어 페이드아웃 시간(초)")]
    [SerializeField] private float gearFadeDuration = 0.2f;

    private Tile tile;
    private SpriteRenderer tileSpriteRenderer;
    private GameObject gearObject;
    private SpriteRenderer gearRenderer;
    private TMP_Text orderText;
    private int targetOrderValue;
    private bool isAbsoluteValue;
    private float currentGearRotationZ;
    private Tween fadeTween;
    /// <summary>화면 표시용. targetOrder - totalPathCount. 1=진입 가능(초록), &gt;1=빨강, 0=이미 지남.</summary>
    private int displayRemaining = 99;
    /// <summary>기어 회전용. 직전 totalPathCount.</summary>
    private int previousTotalPathCount = -1;

    /// <summary>반드시 이 스텝 수에만 진입 가능 (1-based).</summary>
    public int TargetOrder => targetOrderValue;
    /// <summary>순서가 틀리면 진입 불가 후 게임오버(암전·리셋).</summary>
    public bool IsAbsolute => isAbsoluteValue;

    /// <summary>스테이지 데이터 기반 초기화. 그리드 생성 시 GameManager가 호출.</summary>
    public void Setup(int targetOrder, bool isAbsolute)
    {
        targetOrderValue = Mathf.Max(1, targetOrder);
        isAbsoluteValue = isAbsolute;
        currentGearRotationZ = 0f;
        Debug.Log($"[FixedKnot] Setup targetOrder={targetOrderValue} (기어 초기화)");
        CreateGearAndNumber();
        if (tile != null && tile.GetNumberText() != null)
            tile.GetNumberText().gameObject.SetActive(false);
        // 기어만 보이도록 타일 배경(스프라이트) 숨김
        if (tileSpriteRenderer != null)
            tileSpriteRenderer.enabled = false;
        UpdateVisual(0);
    }

    private void Awake()
    {
        tile = GetComponent<Tile>();
        tileSpriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void CreateGearAndNumber()
    {
        Sprite gearSprite = Resources.Load<Sprite>(gearSpritePath);
        if (gearSprite == null)
        {
            Debug.LogWarning($"[FixedKnotTile] Resources/{gearSpritePath} 을(를) 찾을 수 없습니다.");
            return;
        }

        gearObject = new GameObject("FixedKnotGear");
        gearObject.transform.SetParent(transform);
        gearObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        gearObject.transform.localRotation = Quaternion.identity;
        gearObject.transform.localScale = Vector3.one * gearScale;

        gearRenderer = gearObject.AddComponent<SpriteRenderer>();
        gearRenderer.sprite = gearSprite;
        gearRenderer.sortingOrder = 1;
        if (tileSpriteRenderer != null && tileSpriteRenderer.sharedMaterial != null)
            gearRenderer.sharedMaterial = tileSpriteRenderer.sharedMaterial;
        gearRenderer.color = new Color(1f, 0.35f, 0.35f, 1f) * 1.2f;

        // 숫자는 타일 자식으로 두어 기어만 돌아가고 숫자는 고정 (기어 위에 겹쳐 표시)
        GameObject numberObj = new GameObject("OrderNumber");
        numberObj.transform.SetParent(transform);
        numberObj.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        numberObj.transform.localRotation = Quaternion.identity;
        numberObj.transform.localScale = Vector3.one * orderTextScale;

        orderText = numberObj.AddComponent<TextMeshPro>();
        orderText.text = targetOrderValue.ToString();
        orderText.fontSize = orderFontSize;
        orderText.alignment = TextAlignmentOptions.Center;
        var numberRenderer = numberObj.GetComponent<Renderer>();
        if (numberRenderer != null) numberRenderer.sortingOrder = 2;
        if (tile != null && tile.GetNumberText() != null && tile.GetNumberText().font != null)
            orderText.font = tile.GetNumberText().font;
        else
        {
            var defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (defaultFont != null) orderText.font = defaultFont;
        }
        orderText.color = Color.white;
        orderText.ForceMeshUpdate(true, true);
    }

    /// <summary>
    /// GameManager 단일 경로 리스트 Count만 참조. 표시 = targetOrder - (옮긴 횟수). 옮긴 횟수 = totalPathCount - 1.
    /// 한 번 옮기면 5→4, 두 번 옮기면 4→3. 1 = 다음에 진입 가능(초록), &gt;1 = 아직(빨강), 0 = 이미 지남.
    /// </summary>
    public void UpdateVisual(int totalPathCount)
    {
        int moves = Mathf.Max(0, totalPathCount - 1);
        int remaining = Mathf.Max(0, Mathf.Min(targetOrderValue, targetOrderValue - moves));
        displayRemaining = remaining;

        Debug.Log($"[FixedKnot] targetOrder={targetOrderValue}, totalPathCount={totalPathCount}, 옮긴횟수(moves)={moves}, 표시(remaining)={remaining}");

        if (orderText != null)
        {
            orderText.text = remaining.ToString();
            orderText.gameObject.SetActive(remaining > 0);
        }

        if (IsSolvedState()) return;
        if (gearObject == null || !gearObject.activeSelf) return;

        if (totalPathCount > previousTotalPathCount)
        {
            currentGearRotationZ -= gearZRotationPerStep;
            gearObject.transform.DOLocalRotate(new Vector3(0f, 0f, currentGearRotationZ), 0.15f).SetEase(Ease.OutQuad).SetUpdate(true);
        }
        previousTotalPathCount = totalPathCount;
    }

    /// <summary>totalPathCount가 targetOrder를 넘어갔는데 아직 밟지 않았으면 건너뛴 것 → 게임오버.</summary>
    public bool IsMissedAtStepCount(int totalPathCount)
    {
        if (tile == null || !tile.IsActive) return false;
        return totalPathCount > targetOrderValue;
    }

    /// <summary>진입 허용: (targetOrder - totalPathCount) == 1 일 때만. 즉 다음에 밟을 타일이 이 FixedKnot일 때만.</summary>
    public bool CanEnterTile(int totalPathCount)
    {
        return (targetOrderValue - totalPathCount) == 1;
    }

    /// <summary>다음 스텝 번호(1-based)가 targetOrder와 일치할 때만 진입 허용. GameManager가 nextStepNumber = totalPathCount+1 로 호출.</summary>
    public bool CanEnter(int nextStepNumber)
    {
        return nextStepNumber == targetOrderValue;
    }

    /// <summary>풀린 상태: 기어 숨김, count 0.</summary>
    private bool IsSolvedState()
    {
        return tile != null && tile.CurrentNumber == 0 && gearObject != null && !gearObject.activeSelf;
    }

    /// <summary>정확한 순서에 밟았을 때 호출. count는 그대로 두고, 다음 타일을 밟을 때(떠날 때) 사라짐.</summary>
    public void OnSteppedCorrectly()
    {
        if (gearObject != null)
        {
            Vector3 baseScale = Vector3.one * gearScale;
            gearObject.transform.DOScale(baseScale * 1.15f, 0.08f).SetEase(Ease.OutQuad).SetUpdate(true).OnComplete(() =>
            {
                if (gearObject != null)
                    gearObject.transform.DOScale(baseScale, 0.12f).SetEase(Ease.InOutQuad).SetUpdate(true);
            });
        }
    }

    /// <summary>기어를 밟은 뒤 다음 타일로 떠날 때 GameManager가 호출. (GameManager가 이미 count 차감함) 기어 흔들림·페이드아웃.</summary>
    public void OnLeftByPlayer()
    {
        if (tile == null) return;
        if (tile.CurrentNumber == 0)
            StartCoroutine(ShakeAndFadeOutGear());
    }

    private IEnumerator ShakeAndFadeOutGear()
    {
        if (gearObject == null || gearRenderer == null) yield break;
        if (fadeTween != null && fadeTween.IsActive()) fadeTween.Kill();

        // 기어 살짝 흔들리는 연출 (targetOrder 1이 될 때)
        gearObject.transform.DOShakePosition(0.15f, unlockShakeStrength, 12, 90f, false, true).SetUpdate(true);
        yield return new WaitForSeconds(0.15f);

        // 0.2초 만에 서서히 사라짐
        fadeTween = gearRenderer.DOFade(0f, gearFadeDuration).SetEase(Ease.Linear).SetUpdate(true);
        if (orderText != null)
            orderText.DOFade(0f, gearFadeDuration).SetEase(Ease.Linear).SetUpdate(true);
        yield return new WaitForSeconds(gearFadeDuration);

        if (gearObject != null)
            gearObject.SetActive(false);
        if (orderText != null)
            orderText.gameObject.SetActive(false);
        fadeTween = null;

        // 타일로 뿅: 배경 표시 + 팝 연출
        if (tileSpriteRenderer != null)
            tileSpriteRenderer.enabled = true;
        Vector3 baseScale = transform.localScale;
        transform.DOPunchScale(baseScale * 0.15f, 0.2f, 4, 0.5f).SetUpdate(true);
    }

    /// <summary>잘못된 순서로 진입 시도 시 기어(타일) 붉게 진동. GameManager가 호출 후 암전(게임오버) 연출.</summary>
    public void PlayWrongOrderShake()
    {
        float duration = 0.35f;
        float strength = 0.14f;
        transform.DOShakePosition(duration, strength, 18, 90f, false, true).SetUpdate(true);
        if (gearRenderer != null)
        {
            Color orig = gearRenderer.color;
            gearRenderer.DOColor(new Color(1f, 0.25f, 0.25f, orig.a), 0.04f).SetUpdate(true).OnComplete(() =>
            {
                if (gearRenderer != null)
                    gearRenderer.DOColor(orig, 0.3f).SetUpdate(true);
            });
        }
    }

    /// <summary>게임오버 리셋 시 기어 다시 표시. 타일 숫자(count)는 항상 숨김.</summary>
    public void ResetGearVisibility()
    {
        currentGearRotationZ = 0f;
        previousTotalPathCount = -1;
        if (tile != null && tile.GetNumberText() != null)
            tile.GetNumberText().gameObject.SetActive(false); // 리셋 후에도 targetOrder 위에 count 안 나오게
        if (gearObject != null)
        {
            gearObject.SetActive(true);
            gearObject.transform.localRotation = Quaternion.identity;
            gearObject.transform.localScale = Vector3.one * gearScale;
        }
        if (gearRenderer != null)
            gearRenderer.DOFade(1f, 0f);
        if (orderText != null)
        {
            orderText.gameObject.SetActive(true);
            orderText.DOFade(1f, 0f);
            orderText.text = targetOrderValue.ToString();
            Transform numT = orderText.transform;
            if (numT != null)
                numT.localScale = Vector3.one * orderTextScale;
        }
        if (fadeTween != null && fadeTween.IsActive()) fadeTween.Kill();
        if (tileSpriteRenderer != null)
            tileSpriteRenderer.enabled = false;
    }

    private void LateUpdate()
    {
        // 타일 숫자(count)는 항상 숨김 — targetOrder만 보이도록
        if (tile != null && tile.GetNumberText() != null)
            tile.GetNumberText().gameObject.SetActive(false);
        // targetOrder 1이 되면 기어 사라지고 tile 배경만 보이도록: 풀린 상태면 타일 스프라이트 강제 표시
        if (IsSolvedState() && tileSpriteRenderer != null)
            tileSpriteRenderer.enabled = true;
        else if (!IsSolvedState() && tileSpriteRenderer != null)
            tileSpriteRenderer.enabled = false;
        if (gearRenderer == null) return;
        if (!tile.IsActive && gearObject != null && gearObject.activeSelf)
            gearObject.SetActive(false);
        // 2 = 다음에 진입 가능(초록), >2 = 아직(빨강), 0~1 = 이미 지남/밟음
        Color hdrGear;
        if (displayRemaining == 2)
            hdrGear = new Color(0.35f, 1f, 0.45f, 1f) * 1.4f;
        else if (displayRemaining > 2)
            hdrGear = new Color(1f, 0.35f, 0.35f, 1f) * 1.2f;
        else
            hdrGear = Color.white;
        gearRenderer.color = hdrGear;
        if (orderText != null && orderText.gameObject.activeSelf)
            orderText.color = hdrGear;
    }

    private void OnDestroy()
    {
        if (fadeTween != null && fadeTween.IsActive())
            fadeTween.Kill();
        if (gearObject != null)
            Destroy(gearObject);
        if (orderText != null)
            Destroy(orderText.gameObject);
    }
}
