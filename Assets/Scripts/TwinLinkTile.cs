using System.Collections;
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
        public float idleAlphaMultiplier;
        public int idleEdgesPerPulse;
        public float activationLineWidthScale;
        public float activationLineDuration;
        public float activationLineAlpha;
    }

    [Header("LightningBolt 전기 효과 (기본값만 사용, 실제 조정은 GameManager에서)")]
    [SerializeField] private GameObject lightningBoltPrefab;
    private float borderOffset = 0.98f;
    private float boltInterval = 0.04f;
    private float chaosFactor = 0.03f;
    private int boltGenerations = 3;
    private float boltWidthScale = 0.25f;
    private float flashDuration = 0.2f;
    private float flashIntensityMult = 2.2f;
    private float shakeStrength = 0.08f;
    private float shakeDuration = 0.2f;
    private float idleAlphaMultiplier = 0.34f;
    private int idleEdgesPerPulse = 2;
    private float activationLineWidthScale = 0.12f;
    private float activationLineDuration = 0.22f;
    private float activationLineAlpha = 0.9f;

    private Tile tile;
    private SpriteRenderer spriteRenderer;
    private int linkID;
    private Color linkColor;
    private List<TwinLinkTile> partners = new List<TwinLinkTile>();
    private Color normalLineColor;

    /// <summary>테두리 4변: 각 변의 (시작 월드좌표, 끝 월드좌표).</summary>
    private readonly Vector3[] edgeStartLocal = new Vector3[4];
    private readonly Vector3[] edgeEndLocal = new Vector3[4];
    private LightningBoltScript[] boltScripts = new LightningBoltScript[4];
    private LineRenderer[] boltLineRenderers = new LineRenderer[4];
    private LineRenderer[] activationLines = new LineRenderer[0];
    private readonly List<TwinLinkTile> activationTargets = new List<TwinLinkTile>();
    private float boltTimer;
    private int nextIdleEdgeIndex;
    private Tween flashResetTween;
    private Coroutine activationFadeRoutine;
    private static Material fallbackActivationLineMaterial;

    private void Awake()
    {
        tile = GetComponent<Tile>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnDestroy()
    {
        DOTween.Kill(transform);
        StopActivationFadeRoutine();
        if (flashResetTween != null && flashResetTween.IsActive())
            flashResetTween.Kill();
        flashResetTween = null;
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

        if (tile != null)
            tile.SetNumberColorOverride(linkColor);

        if (settings.HasValue)
        {
            var s = settings.Value;
            borderOffset = s.borderOffset > 0f ? s.borderOffset : borderOffset;
            boltInterval = s.boltInterval > 0f ? s.boltInterval : boltInterval;
            chaosFactor = Mathf.Clamp01(s.chaosFactor);
            boltGenerations = Mathf.Clamp(s.boltGenerations, 2, 6);
            boltWidthScale = s.boltWidthScale > 0f ? s.boltWidthScale : boltWidthScale;
            flashDuration = s.flashDuration >= 0f ? s.flashDuration : flashDuration;
            shakeStrength = s.shakeStrength >= 0f ? s.shakeStrength : shakeStrength;
            idleAlphaMultiplier = s.idleAlphaMultiplier > 0f ? Mathf.Clamp01(s.idleAlphaMultiplier) : idleAlphaMultiplier;
            idleEdgesPerPulse = s.idleEdgesPerPulse > 0 ? Mathf.Clamp(s.idleEdgesPerPulse, 1, 4) : idleEdgesPerPulse;
            activationLineWidthScale = s.activationLineWidthScale > 0f ? s.activationLineWidthScale : activationLineWidthScale;
            activationLineDuration = s.activationLineDuration > 0f ? Mathf.Clamp(s.activationLineDuration, 0.05f, 0.5f) : activationLineDuration;
            activationLineAlpha = s.activationLineAlpha > 0f ? Mathf.Clamp01(s.activationLineAlpha) : activationLineAlpha;
        }
        normalLineColor = WithAlpha(linkColor, idleAlphaMultiplier);

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
            // Idle 테두리는 조용하게 보이도록 이전보다 짧게 남긴다.
            script.Duration = Mathf.Max(boltInterval * 2.2f, 0.08f);
            script.Generations = boltGenerations;
            script.ChaosFactor = Mathf.Clamp01(chaosFactor);

            var lr = go.GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.sortingOrder = 10;
                lr.useWorldSpace = true;
                lr.widthMultiplier = widthMult;
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
            int triggerCount = Mathf.Clamp(idleEdgesPerPulse, 1, 4);
            for (int step = 0; step < triggerCount; step++)
            {
                int i = (nextIdleEdgeIndex + step) % boltScripts.Length;
                if (boltScripts[i] == null) continue;
                Vector3 wStart = transform.TransformPoint(edgeStartLocal[i]);
                Vector3 wEnd = transform.TransformPoint(edgeEndLocal[i]);
                boltScripts[i].StartPosition = wStart;
                boltScripts[i].EndPosition = wEnd;
                boltScripts[i].Trigger();
            }
            nextIdleEdgeIndex = (nextIdleEdgeIndex + triggerCount) % boltScripts.Length;
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

        EnsureActivationLines(partners.Count);
        ClearActivationLines();
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

        activationTargets.Clear();
        foreach (var p in partners)
        {
            if (p == null || p.tile == null) continue;
            if (shouldExcludePartner != null && shouldExcludePartner(p.tile))
                continue;
            consumeTile(p.tile);
            activationTargets.Add(p);
            p.FlashBolt();
            p.Shake();
        }

        PlayActivationNetwork(activationTargets);
        FlashBolt();
        Shake();
    }

    public void ResetTransientVisualState()
    {
        DOTween.Kill(transform);
        StopActivationFadeRoutine();
        if (flashResetTween != null && flashResetTween.IsActive())
            flashResetTween.Kill();
        flashResetTween = null;
        nextIdleEdgeIndex = 0;
        ApplyBoltColor(normalLineColor);
        ClearBoltLines();
        ClearActivationLines();
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

    private void EnsureActivationLines(int requiredCount)
    {
        if (requiredCount <= activationLines.Length)
            return;

        int oldLength = activationLines.Length;
        System.Array.Resize(ref activationLines, requiredCount);
        for (int i = oldLength; i < activationLines.Length; i++)
            activationLines[i] = CreateActivationLine();
    }

    private LineRenderer CreateActivationLine()
    {
        LineRenderer line = gameObject.AddComponent<LineRenderer>();
        line.enabled = false;
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.widthMultiplier = GetActivationLineWidth();
        line.sharedMaterial = ResolveLineMaterial(spriteRenderer);
        if (spriteRenderer != null)
        {
            line.sortingLayerID = spriteRenderer.sortingLayerID;
            line.sortingOrder = spriteRenderer.sortingOrder + 20;
        }
        return line;
    }

    private static Material ResolveLineMaterial(SpriteRenderer renderer)
    {
        if (renderer != null && renderer.sharedMaterial != null)
            return renderer.sharedMaterial;
        if (fallbackActivationLineMaterial != null)
            return fallbackActivationLineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            return null;

        fallbackActivationLineMaterial = new Material(shader)
        {
            name = "TwinLink Activation Line (Runtime)",
            hideFlags = HideFlags.DontSave
        };
        return fallbackActivationLineMaterial;
    }

    private void PlayActivationNetwork(List<TwinLinkTile> targets)
    {
        if (targets == null || targets.Count == 0)
            return;

        StopActivationFadeRoutine();
        EnsureActivationLines(targets.Count);
        ClearActivationLines();

        Vector3 sourcePosition = GetCuePosition(transform.position);
        int lineIndex = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            TwinLinkTile target = targets[i];
            if (target == null)
                continue;

            LineRenderer line = activationLines[lineIndex++];
            if (line == null)
                continue;

            line.enabled = true;
            line.positionCount = 2;
            line.widthMultiplier = GetActivationLineWidth();
            line.SetPosition(0, sourcePosition);
            line.SetPosition(1, GetCuePosition(target.transform.position));
            ApplyActivationLineColor(line, activationLineAlpha);
        }

        if (lineIndex > 0)
            activationFadeRoutine = StartCoroutine(FadeActivationLinesRoutine(lineIndex));
    }

    private float GetActivationLineWidth()
    {
        return Mathf.Max(0.035f, TileHalfSize() * 2f * activationLineWidthScale);
    }

    private void ApplyActivationLineColor(LineRenderer line, float alpha)
    {
        Color start = WithAlpha(linkColor, alpha);
        Color end = WithAlpha(linkColor, alpha * 0.55f);
        line.startColor = start;
        line.endColor = end;
    }

    private void HideActivationLine(LineRenderer line)
    {
        if (line == null)
            return;

        line.enabled = false;
        line.positionCount = 0;
    }

    private void ClearActivationLines()
    {
        for (int i = 0; i < activationLines.Length; i++)
            HideActivationLine(activationLines[i]);
    }

    private IEnumerator FadeActivationLinesRoutine(int activeLineCount)
    {
        float elapsed = 0f;
        while (elapsed < activationLineDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / activationLineDuration);
            float eased = 1f - (1f - t) * (1f - t);
            float alpha = Mathf.Lerp(activationLineAlpha, 0f, eased);
            for (int i = 0; i < activeLineCount && i < activationLines.Length; i++)
            {
                LineRenderer line = activationLines[i];
                if (line != null && line.enabled)
                    ApplyActivationLineColor(line, alpha);
            }
            yield return null;
        }

        for (int i = 0; i < activeLineCount && i < activationLines.Length; i++)
            HideActivationLine(activationLines[i]);
        activationFadeRoutine = null;
    }

    private void StopActivationFadeRoutine()
    {
        if (activationFadeRoutine != null)
        {
            StopCoroutine(activationFadeRoutine);
            activationFadeRoutine = null;
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

    private static Vector3 GetCuePosition(Vector3 source)
    {
        return new Vector3(source.x, source.y, source.z - 0.05f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    public int LinkID => linkID;
    public IReadOnlyList<TwinLinkTile> Partners => partners;

    /// <summary>트레일 등 연출용 대표 컬러 (JSON/linkID 기반).</summary>
    public Color GetLinkColor() => linkColor;
}
