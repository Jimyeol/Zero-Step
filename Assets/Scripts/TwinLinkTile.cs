using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using DigitalRuby.LightningBolt;

/// <summary>
/// TwinLink 타일: 같은 linkID끼리 연결된 짝 타일을 관리하고, 전기 테두리와 번쩍임 연출을 담당한다.
/// GameManager가 지정한 전용 색으로 전기·숫자 발광색을 적용한다.
/// </summary>
[RequireComponent(typeof(Tile))]
public class TwinLinkTile : MonoBehaviour
{
    /// <summary>GameManager Inspector에서 조정한 값을 전달할 때 사용. 값이 0이면 스크립트 기본값 사용.</summary>
    public struct TwinLinkSettings
    {
        public float borderOffset;
        public float boltInterval;
        public float chaosFactor;
        public int boltGenerations;
        public float boltWidthScale;
        public float flashDuration;
        public float shakeStrength;
    }

    [Header("LightningBolt 전기 효과 (기본값만 사용, 실제 조정은 GameManager에서)")]
    [SerializeField] private GameObject lightningBoltPrefab;
    private const float MinSmoothBoltInterval = 0.015f;
    private const float MaxSmoothBoltInterval = 0.06f;
    private float borderOffset = 0.98f;
    private float boltInterval = 0.03f;
    private float chaosFactor = 0.025f;
    private int boltGenerations = 4;
    private float boltWidthScale = 0.25f;
    private float flashDuration = 0.2f;
    private float flashIntensityMult = 2.2f;
    private float shakeStrength = 0.08f;
    private float shakeDuration = 0.2f;

    private Tile tile;
    private int linkID;
    private Color linkColor;
    private List<TwinLinkTile> partners = new List<TwinLinkTile>();
    private Color normalLineColor;

    /// <summary>테두리 4변: 각 변의 (시작 월드좌표, 끝 월드좌표).</summary>
    private readonly Vector3[] edgeStartLocal = new Vector3[4];
    private readonly Vector3[] edgeEndLocal = new Vector3[4];
    private LightningBoltScript[] boltScripts = new LightningBoltScript[4];
    private LineRenderer[] boltLineRenderers = new LineRenderer[4];
    private float boltTimer;
    private Tween flashResetTween;

    private void Awake()
    {
        tile = GetComponent<Tile>();
    }

    /// <summary>
    /// GameManager가 지정한 linkID와 색으로 초기화. 같은 linkID끼리는 같은 색을 공유한다.
    /// boltPrefabOverride: GameManager에서 넘기면 이걸 사용.
    /// settings: GameManager Inspector 값. 값이 0이면 기본값 유지.
    /// </summary>
    public void Setup(int id, Color assignedColor, GameObject boltPrefabOverride = null, TwinLinkSettings? settings = null)
    {
        linkID = id;
        linkColor = assignedColor;

        normalLineColor = linkColor;
        if (tile != null)
            tile.SetNumberColorOverride(linkColor);

        if (settings.HasValue)
        {
            var s = settings.Value;
            borderOffset = s.borderOffset > 0f ? s.borderOffset : borderOffset;
            boltInterval = s.boltInterval > 0f ? Mathf.Clamp(s.boltInterval, MinSmoothBoltInterval, MaxSmoothBoltInterval) : boltInterval;
            chaosFactor = Mathf.Clamp01(s.chaosFactor);
            boltGenerations = Mathf.Clamp(s.boltGenerations, 2, 6);
            boltWidthScale = s.boltWidthScale > 0f ? s.boltWidthScale : boltWidthScale;
            flashDuration = s.flashDuration >= 0f ? s.flashDuration : flashDuration;
            shakeStrength = s.shakeStrength >= 0f ? s.shakeStrength : shakeStrength;
        }

        if (boltPrefabOverride != null)
            lightningBoltPrefab = boltPrefabOverride;
        if (lightningBoltPrefab == null)
            lightningBoltPrefab = Resources.Load<GameObject>("SimpleLightningBoltAnimatedPrefab");
        if (lightningBoltPrefab == null)
            lightningBoltPrefab = Resources.Load<GameObject>("SimpleLightningBoltPrefab");
        if (lightningBoltPrefab == null)
        {
            Debug.LogWarning("[TwinLinkTile] LightningBolt 프리팹이 없습니다. GameManager에 TwinLink Lightning Prefab을 할당하거나, 해당 프리팹을 Assets/Resources/에 복사해 두세요.");
            return;
        }

        BuildEdgePositions();
        CreateBoltInstances();
        ApplyBoltColor(normalLineColor);
    }

    private float TileHalfSize()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col != null && col.size.x > 0.01f)
            return col.size.x * 0.5f * transform.lossyScale.x;
        return 0.5f;
    }

    /// <summary>테두리 4변의 로컬 좌표 (시작, 끝) 쌍 설정.</summary>
    private void BuildEdgePositions()
    {
        float h = TileHalfSize() * borderOffset;
        // 아래 → 오른쪽 → 위 → 왼쪽
        edgeStartLocal[0] = new Vector3(-h, -h, 0f);
        edgeEndLocal[0] = new Vector3(h, -h, 0f);
        edgeStartLocal[1] = new Vector3(h, -h, 0f);
        edgeEndLocal[1] = new Vector3(h, h, 0f);
        edgeStartLocal[2] = new Vector3(h, h, 0f);
        edgeEndLocal[2] = new Vector3(-h, h, 0f);
        edgeStartLocal[3] = new Vector3(-h, h, 0f);
        edgeEndLocal[3] = new Vector3(-h, -h, 0f);
    }

    private void CreateBoltInstances()
    {
        float halfSize = TileHalfSize();
        float widthMult = Mathf.Max(0.05f, halfSize * 2f * boltWidthScale);

        for (int i = 0; i < 4; i++)
        {
            GameObject go = Instantiate(lightningBoltPrefab, transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.name = $"TwinLinkBolt_{i}";

            var script = go.GetComponent<LightningBoltScript>();
            if (script == null)
            {
                Debug.LogWarning("[TwinLinkTile] LightningBolt 프리팹에 LightningBoltScript가 없습니다.");
                Destroy(go);
                continue;
            }

            script.StartObject = null;
            script.EndObject = null;
            script.ManualMode = true;
            // 번개가 사라지기 전에 여러 번 겹쳐 갱신되도록 유지 시간을 충분히 둔다.
            script.Duration = Mathf.Max(boltInterval * 6f, 0.18f);
            script.Generations = boltGenerations;
            script.ChaosFactor = Mathf.Clamp01(chaosFactor);

            var lr = go.GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.sortingOrder = 10;
                lr.useWorldSpace = true;
                lr.widthMultiplier = widthMult;
                lr.numCapVertices = 4;
                lr.numCornerVertices = 4;
            }

            boltScripts[i] = script;
            boltLineRenderers[i] = lr;
        }
    }

    /// <summary>타일이 사라지면(count 0) 전기 효과도 함께 숨김.</summary>
    private void SetBoltsActive(bool active)
    {
        for (int i = 0; i < 4; i++)
        {
            if (boltScripts[i] != null && boltScripts[i].gameObject != null)
                boltScripts[i].gameObject.SetActive(active);
        }
    }

    private void ApplyBoltColor(Color c)
    {
        for (int i = 0; i < 4; i++)
        {
            if (boltLineRenderers[i] == null) continue;
            boltLineRenderers[i].startColor = c;
            boltLineRenderers[i].endColor = new Color(c.r, c.g, c.b, c.a * 0.85f);
        }
    }

    private void Update()
    {
        if (GameManager.IsPerformanceOverlayOpen)
            return;

        if (tile != null && (!tile.IsActive || transform.localScale.sqrMagnitude <= 0.0001f))
        {
            SetBoltsActive(false);
            return;
        }
        SetBoltsActive(true);

        if (lightningBoltPrefab == null || boltScripts[0] == null) return;

        boltTimer -= Time.deltaTime;
        if (boltTimer <= 0f)
        {
            boltTimer = boltInterval;
            for (int i = 0; i < 4; i++)
            {
                if (boltScripts[i] == null) continue;
                Vector3 wStart = transform.TransformPoint(edgeStartLocal[i]);
                Vector3 wEnd = transform.TransformPoint(edgeEndLocal[i]);
                boltScripts[i].StartPosition = wStart;
                boltScripts[i].EndPosition = wEnd;
                boltScripts[i].Trigger();
            }
            ApplyBoltColor(normalLineColor);
        }
    }

    /// <summary>
    /// 같은 linkID 타일 목록 등록. GameManager가 그리드 생성 후 호출.
    /// </summary>
    public void SetPartners(List<TwinLinkTile> list)
    {
        partners.Clear();
        if (list != null)
        {
            foreach (var p in list)
                if (p != null && p != this)
                    partners.Add(p);
        }
    }

    /// <summary>짝 타일들이 이번 스텝에 함께 감소 가능한지 검사.</summary>
    public bool CanConsumePartners(System.Predicate<Tile> shouldExcludePartner = null)
    {
        foreach (var p in partners)
        {
            if (p == null || p.tile == null) continue;
            if (shouldExcludePartner != null && shouldExcludePartner(p.tile))
                continue;
            if (p.tile.CurrentNumber <= 0)
                return false;
        }

        return true;
    }

    public bool AreAllPartnersAtCount(int expectedCount)
    {
        if (partners.Count == 0)
            return false;

        foreach (var p in partners)
        {
            if (p == null || p.tile == null)
                return false;
            if (p.tile.CurrentNumber != expectedCount)
                return false;
        }

        return true;
    }

    public int GetPartnerRemainingCount()
    {
        int sum = 0;
        foreach (var p in partners)
        {
            if (p == null || p.tile == null) continue;
            sum += p.tile.CurrentNumber;
        }
        return sum;
    }

    public bool HasPartner(Tile candidate)
    {
        if (candidate == null)
            return false;

        foreach (var p in partners)
        {
            if (p != null && p.tile == candidate)
                return true;
        }

        return false;
    }

    /// <summary>짝 타일들을 직접 1 감소시키고 전기 연출을 재생한다.</summary>
    public void ConsumePartners(System.Action<Tile> consumeTile, System.Predicate<Tile> shouldExcludePartner = null)
    {
        if (consumeTile == null)
            return;

        foreach (var p in partners)
        {
            if (p == null || p.tile == null) continue;
            if (shouldExcludePartner != null && shouldExcludePartner(p.tile))
                continue;
            consumeTile(p.tile);
            p.FlashBolt();
            p.Shake();
        }

        FlashBolt();
        Shake();
    }

    public void ResetTransientVisualState()
    {
        DOTween.Kill(transform);
        if (flashResetTween != null && flashResetTween.IsActive())
            flashResetTween.Kill();
        flashResetTween = null;
        ApplyBoltColor(normalLineColor);
        ClearBoltLines();
        SetBoltsActive(false);
    }

    private void ClearBoltLines()
    {
        for (int i = 0; i < boltLineRenderers.Length; i++)
        {
            if (boltLineRenderers[i] != null)
                boltLineRenderers[i].positionCount = 0;
        }
    }

    private void FlashBolt()
    {
        Color bright = linkColor * flashIntensityMult;
        bright.a = 1f;
        ApplyBoltColor(bright);
        if (flashResetTween != null && flashResetTween.IsActive())
            flashResetTween.Kill();
        flashResetTween = DOVirtual.DelayedCall(flashDuration, () =>
        {
            if (this != null)
                ApplyBoltColor(normalLineColor);
            flashResetTween = null;
        });
    }

    private void Shake()
    {
        transform.DOShakePosition(shakeDuration, shakeStrength, 14, 90f, false, true).SetUpdate(true);
    }

    public int LinkID => linkID;

    /// <summary>트레일 등 연출용 대표 컬러 (JSON/linkID 기반).</summary>
    public Color GetLinkColor() => linkColor;
}
