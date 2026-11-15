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
    private const int GuideSortingOrder = 640;

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

    private Tile guideFrom;
    private Tile guideTo;
    private float guideT;
    private bool guideActive;

    /// <summary>힌트 안내: from 칸에서 to 칸으로 t(0~1)만큼 걸어간 자리에 있게 한다. 매 프레임 불러 준다.</summary>
    public void SetGuide(Tile from, Tile to, float t)
    {
        guideFrom = from;
        guideTo = to;
        guideT = Mathf.Clamp01(t);
        guideActive = from != null && to != null;
    }

    /// <summary>힌트 안내를 끝내고 원래 앉던 칸으로 돌아간다.</summary>
    public void ClearGuide()
    {
        if (!guideActive)
            return;
        guideActive = false;
        guideFrom = null;
        guideTo = null;
        hopFrom = position;
        hopStart = placed && alpha > 0.5f ? Time.unscaledTime : -10f;
    }

    /// <summary>칸 윗변에 걸터앉는 자리와 캐릭터 크기. 칸의 평소 크기 기준이다.</summary>
    private static bool TryGetPerch(Tile tile, out Vector3 anchor, out float size, out float tileWidth, out float tileHeight, out SpriteRenderer tileRenderer)
    {
        anchor = default;
        size = tileWidth = tileHeight = 1f;
        tileRenderer = tile != null ? tile.GetComponent<SpriteRenderer>() : null;
        if (tileRenderer == null)
            return false;
        Vector3 tileScale = tile.transform.lossyScale;
        Sprite tileSprite = tileRenderer.sprite;
        tileWidth = tileSprite != null ? tileSprite.bounds.size.x * Mathf.Abs(tileScale.x) : 1f;
        tileHeight = tileSprite != null ? tileSprite.bounds.size.y * Mathf.Abs(tileScale.y) : 1f;
        size = tileWidth * SizeRatio;
        anchor = tile.transform.position + new Vector3(0f, tileHeight * 0.36f + size * 0.18f, -0.06f);
        return true;
    }

    /// <summary>힌트 안내 중: 두 칸의 앉는 자리 사이를 폴짝 넘어간다. 멀리 떨어진 자리로는 빠르게 미끄러져 간다.</summary>
    private bool UpdateGuide(float now)
    {
        if (!TryGetPerch(guideFrom, out Vector3 from, out float size, out float tileWidth, out float tileHeight, out _)
            || !TryGetPerch(guideTo, out Vector3 to, out _, out _, out _, out SpriteRenderer toRenderer)
            || GameplayFeedback.Intensity <= 0f)
            return false;

        alpha = Mathf.MoveTowards(alpha, 1f, Time.unscaledDeltaTime / FadeDuration);
        float eased = guideT * guideT * (3f - 2f * guideT);
        Vector3 desired = Vector3.Lerp(from, to, eased);
        float arc = Mathf.Sin(guideT * Mathf.PI);
        desired.y += arc * tileHeight * 0.3f;
        if (!placed)
        {
            placed = true;
            position = desired;
        }
        // 되풀이할 때 끝에서 처음으로 돌아가는 구간은 순간이동 대신 빠르게 이동한다.
        position = Vector3.MoveTowards(position, desired, tileWidth * 16f * Time.unscaledDeltaTime);
        float stretch = arc * 0.16f;
        float spriteWidth = body.sprite != null ? Mathf.Max(0.0001f, body.sprite.bounds.size.x) : 1f;
        // 안내 중에는 힌트 선과 (선 위로 올린) 숫자보다도 위에 그린다. 칸을 넘어갈 때 숫자가 얼굴 위로 비치지 않게.
        Apply(alpha, position, size / spriteWidth * (1f - stretch), size / spriteWidth * (1f + stretch), 0f, 1f, GuideSortingOrder);
        body.sortingLayerID = toRenderer.sortingLayerID;
        face.sortingLayerID = toRenderer.sortingLayerID;
        return true;
    }

    private Tile suckTile;
    private float suckStart = -10f;
    private Vector3 suckFrom;
    private float popStart = -10f;
    private float suckDuration = 0.6f;
    private bool sucking;   // 포탈로 빨려 든 뒤 EmergeAt까지 숨어 있다(포탈 칸이 없어져도).

    /// <summary>포탈로 들어갔을 때: 소용돌이를 그리며 포탈 가운데로 빨려 들어가 사라진다. EmergeAt 전까지 보이지 않는다.</summary>
    public void SuckInto(Tile portal, float duration = 0.6f)
    {
        suckDuration = Mathf.Max(0.05f, duration);
        sucking = true;
        suckTile = portal;
        suckStart = Time.unscaledTime;
        suckFrom = position;
        guideActive = false;
    }

    /// <summary>2페이즈 보드의 시작 칸에서 퐁 하고 다시 나타난다.</summary>
    public void EmergeAt(Tile tile)
    {
        sucking = false;
        suckTile = null;
        target = tile;
        placed = false;
        alpha = 0f;
        popStart = Time.unscaledTime;
    }

    /// <summary>포탈 전환이 취소됐을 때(다른 판으로 이동): 숨김을 풀고 평소처럼 칸을 따라간다.</summary>
    public void CancelSuck()
    {
        if (!sucking)
            return;
        sucking = false;
        suckTile = null;
        placed = false;
        alpha = 0f;
    }

    private bool UpdateSuck(float now)
    {
        if (!sucking)
            return false;
        float t = Mathf.Clamp01((now - suckStart) / suckDuration);
        if (t >= 1f || suckTile == null || !TryGetPerch(suckTile, out _, out float size, out _, out _, out SpriteRenderer tileRenderer))
        {
            Apply(0f, position, 1f, 1f, 0f, 1f, body.sortingOrder);
            return true;
        }
        Vector3 center = suckTile.transform.position + new Vector3(0f, 0f, -0.06f);
        Vector3 offset = suckFrom - center;
        float turn = t * t * 540f * Mathf.Deg2Rad;
        offset = new Vector3(offset.x * Mathf.Cos(turn) - offset.y * Mathf.Sin(turn), offset.x * Mathf.Sin(turn) + offset.y * Mathf.Cos(turn), offset.z);
        position = center + offset * (1f - t * t);
        float shrink = 1f - t;
        float spriteWidth = body.sprite != null ? Mathf.Max(0.0001f, body.sprite.bounds.size.x) : 1f;
        Apply(1f, position, size / spriteWidth * shrink, size / spriteWidth * shrink, t * 720f, 1f, tileRenderer.sortingOrder + SortingAboveTile);
        body.sortingLayerID = tileRenderer.sortingLayerID;
        face.sortingLayerID = tileRenderer.sortingLayerID;
        return true;
    }

    /// <summary>판을 깼을 때: 두 번 뛰어오르며 한 바퀴 돈다.</summary>
    public void Cheer() => cheerStart = Time.unscaledTime;

    /// <summary>실패했을 때: 좌우로 고개를 젓는다.</summary>
    public void Shake() => shakeStart = Time.unscaledTime;

    private void LateUpdate()
    {
        float now = Time.unscaledTime;
        if (UpdateSuck(now))
            return;
        if (guideActive && UpdateGuide(now))
            return;
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
        // 칸 윗변에 걸터앉는다. 위 칸의 숫자에 닿지 않도록 자기 칸 쪽으로 조금 내려 앉힌다(자기 숫자는 가리지 않는 높이).
        Vector3 anchor = target.transform.position + new Vector3(0f, tileHeight * 0.36f + size * 0.18f, -0.06f);

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

        // 포탈에서 나왔을 때: 0에서 살짝 넘치게 커졌다가 제 크기로.
        float pop = (now - popStart) / 0.32f;
        if (pop >= 0f && pop < 1f)
        {
            float back = 1f + 2.2f * Mathf.Pow(pop - 1f, 3f) + 1.2f * Mathf.Pow(pop - 1f, 2f);
            squashX *= back;
            squashY *= back;
        }

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
