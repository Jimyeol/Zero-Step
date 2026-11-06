using UnityEngine;

/// <summary>
/// A2 소프트 클레이 테마의 마스코트 "제로": 지금 있는 칸의 윗변에 걸터앉은 작은 점토 캐릭터.
/// 길을 그리면 칸에서 칸으로 폴짝폴짝 따라오고, 가만히 있으면 숨 쉬듯 흔들리며 눈을 깜빡인다.
/// 판을 깨면 뛰어오르며 한 바퀴 돌고, 실패하면 고개를 젓는다. 숫자를 가리지 않도록 칸 위쪽 틈에 앉는다.
/// </summary>
public sealed class ClayZero : MonoBehaviour
{
    private const float SizeRatio = 0.5f;       // 타일 폭 대비 캐릭터 그림 폭(몸통은 그 약 70%)
    private const float HopDuration = 0.15f;
    private const float FadeDuration = 0.14f;
    private const int SortingAboveTile = 6;

    private SpriteRenderer body;
    private SpriteRenderer face;
    private Tile target;
    private Vector3 hopFrom;
    private Vector3 position;
    private float hopStart = -10f;
    private float alpha;
    private float nextBlink;
    private float blinkStart = -10f;
    private float cheerStart = -10f;
    private float shakeStart = -10f;
    private bool placed;

    public static ClayZero Create(Transform parent)
    {
        var go = new GameObject("ClayZero");
        go.transform.SetParent(parent, false);
        return go.AddComponent<ClayZero>();
    }

    private void Awake()
    {
        body = MakeRenderer("Body", VisualTheme.ZeroBodySprite, VisualTheme.Accent, 0);
        face = MakeRenderer("Face", VisualTheme.ZeroFaceSprite, Color.white, 1);
        nextBlink = Time.unscaledTime + 2f;
        Apply(0f, Vector3.zero, 1f, 1f, 0f, 1f, 0);
    }

    private SpriteRenderer MakeRenderer(string objectName, Sprite sprite, Color color, int order)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.001f * order);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        return sr;
    }

    /// <summary>앉을 칸(없으면 사라진다). 칸이 바뀌면 그쪽으로 폴짝 뛴다.</summary>
    public void SetTarget(Tile tile)
    {
        if (tile == target)
            return;
        target = tile;
        if (tile == null)
            return;
        hopFrom = position;
        hopStart = placed && alpha > 0.5f ? Time.unscaledTime : -10f;
    }

    /// <summary>판을 깼을 때: 두 번 뛰어오르며 한 바퀴 돈다.</summary>
    public void Cheer() => cheerStart = Time.unscaledTime;

    /// <summary>실패했을 때: 좌우로 고개를 젓는다.</summary>
    public void Shake() => shakeStart = Time.unscaledTime;

    private void LateUpdate()
    {
        float now = Time.unscaledTime;
        SpriteRenderer tileRenderer = target != null ? target.GetComponent<SpriteRenderer>() : null;
        bool show = target != null && tileRenderer != null && tileRenderer.enabled && target.gameObject.activeInHierarchy && GameplayFeedback.Intensity > 0f;
        alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, Time.unscaledDeltaTime / FadeDuration);
        if (!show)
        {
            if (alpha <= 0f)
                placed = false;
            Apply(alpha, position, 1f, 1f, 0f, 1f, body.sortingOrder);
            return;
        }

        // 칸의 평소 크기 기준(하트비트로 커졌다 작아지는 것은 따라가지 않아 자리와 크기가 흔들리지 않는다).
        Vector3 tileScale = target.transform.lossyScale;
        Sprite tileSprite = tileRenderer.sprite;
        float tileWidth = tileSprite != null ? tileSprite.bounds.size.x * Mathf.Abs(tileScale.x) : 1f;
        float tileHeight = tileSprite != null ? tileSprite.bounds.size.y * Mathf.Abs(tileScale.y) : 1f;
        float size = tileWidth * SizeRatio;
        Vector3 anchor = target.transform.position + new Vector3(0f, tileHeight * 0.4f + size * 0.2f, -0.06f);

        if (!placed)
        {
            placed = true;
            position = anchor;
            hopStart = -10f;
        }

        float squashX = 1f;
        float squashY = 1f;
        float hop = (now - hopStart) / HopDuration;
        if (hop < 1f)
        {
            // 폴짝: 포물선으로 넘어가며 공중에서는 길쭉해지고 내려앉을 때 납작해진다.
            float eased = hop * hop * (3f - 2f * hop);
            position = Vector3.Lerp(hopFrom, anchor, eased);
            position.y += Mathf.Sin(hop * Mathf.PI) * tileHeight * 0.32f;
            float stretch = Mathf.Sin(hop * Mathf.PI) * 0.18f;
            squashX = 1f - stretch;
            squashY = 1f + stretch;
        }
        else
        {
            float landing = Mathf.Clamp01((now - hopStart - HopDuration) / 0.12f);
            float squash = hopStart > 0f ? (1f - landing) * 0.16f : 0f;
            // 가만히 있을 때: 숨 쉬듯 살짝 오르내린다.
            float breathe = Mathf.Sin(now * 2.6f) * 0.03f;
            squashX = 1f + squash - breathe * 0.5f;
            squashY = 1f - squash + breathe;
            position = anchor;
        }

        float spin = 0f;
        float cheer = now - cheerStart;
        if (cheer >= 0f && cheer < 0.9f)
        {
            float jump = Mathf.Abs(Mathf.Sin(cheer / 0.45f * Mathf.PI));
            position.y += jump * tileHeight * 0.55f;
            spin = Mathf.Clamp01(cheer / 0.45f) * 360f;
            squashY *= 1f + jump * 0.12f;
        }
        float shake = now - shakeStart;
        if (shake >= 0f && shake < 0.45f)
            position.x += Mathf.Sin(shake * 46f) * tileWidth * 0.05f * (1f - shake / 0.45f);

        // 눈 깜빡임
        if (now >= nextBlink)
        {
            blinkStart = now;
            nextBlink = now + Random.Range(2.4f, 4.6f);
        }
        float blink = (now - blinkStart) / 0.12f;
        float eye = blink >= 0f && blink < 1f ? Mathf.Abs(blink * 2f - 1f) * 0.85f + 0.15f : 1f;

        float spriteWidth = body.sprite != null ? Mathf.Max(0.0001f, body.sprite.bounds.size.x) : 1f;
        Apply(alpha, position, size / spriteWidth * squashX, size / spriteWidth * squashY, spin, eye, tileRenderer.sortingOrder + SortingAboveTile);
        body.sortingLayerID = tileRenderer.sortingLayerID;
        face.sortingLayerID = tileRenderer.sortingLayerID;
    }

    private void Apply(float a, Vector3 worldPosition, float scaleX, float scaleY, float spinDegrees, float eyeOpen, int order)
    {
        transform.position = worldPosition;
        transform.rotation = Quaternion.Euler(0f, 0f, spinDegrees);
        transform.localScale = new Vector3(scaleX, scaleY, 1f);
        bool visible = a > 0.001f;
        body.enabled = visible;
        face.enabled = visible;
        Color c = VisualTheme.Accent;
        c.a = a;
        body.color = c;
        face.color = new Color(1f, 1f, 1f, a);
        face.transform.localScale = new Vector3(1f, eyeOpen, 1f);
        body.sortingOrder = order;
        face.sortingOrder = order + 1;
    }
}
