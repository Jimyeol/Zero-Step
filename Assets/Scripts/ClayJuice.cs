using UnityEngine;

/// <summary>
/// A2 소프트 클레이 테마의 "손맛" 연출: 타일이 터질 때 점토 조각이 사방으로 튀고(중력·회전), 작은 먼지가 퍼진다.
/// 조각은 미리 만든 SpriteRenderer 풀을 돌려 쓴다(할당 없음). 효과 강도(GameplayFeedback.Intensity)에 비례한다.
/// </summary>
public sealed class ClayJuice : MonoBehaviour
{
    public static ClayJuice Instance { get; private set; }

    private const int PoolSize = 96;
    private const int SortingOrder = 160;

    private struct Chunk
    {
        public SpriteRenderer renderer;
        public Vector3 velocity;
        public float spin;
        public float age;
        public float life;
        public float startScale;
        public float gravity;
        public Color color;
        public bool active;
    }

    private readonly Chunk[] chunks = new Chunk[PoolSize];
    private int cursor;

    public static ClayJuice Ensure(Transform parent)
    {
        if (Instance != null)
            return Instance;
        var go = new GameObject("ClayJuice");
        if (parent != null)
            go.transform.SetParent(parent, false);
        Instance = go.AddComponent<ClayJuice>();
        return Instance;
    }

    private void Awake()
    {
        Instance = this;
        Sprite tile = VisualTheme.TileSprite;
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("ClayChunk");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = tile;
            sr.sortingOrder = SortingOrder;
            sr.enabled = false;
            chunks[i] = new Chunk { renderer = sr };
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>타일이 0이 되어 터질 때: 점토 조각이 크게 튄다.</summary>
    public void Pop(Vector3 center, Color color, float tileWorld, float power = 1f)
    {
        float intensity = GameplayFeedback.Intensity;
        if (intensity <= 0f)
            return;
        int count = Mathf.RoundToInt(Mathf.Lerp(5f, 12f, intensity) * power);
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.35f, 0.35f);
            float speed = tileWorld * Random.Range(2.6f, 4.6f) * power;
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.8f + 0.45f, 0f);
            Spawn(center + dir * tileWorld * 0.18f, dir * speed, color, tileWorld * Random.Range(0.16f, 0.27f), Random.Range(0.38f, 0.6f), tileWorld * 9f);
        }
        // 가벼운 먼지(종이색)
        Color dust = VisualTheme.PaperShade;
        for (int i = 0; i < 4; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Spawn(center, dir * tileWorld * Random.Range(0.8f, 1.4f), dust, tileWorld * Random.Range(0.1f, 0.16f), 0.35f, 0f);
        }
    }

    /// <summary>숫자가 줄어들 때: 작은 부스러기 몇 개.</summary>
    public void Crumbs(Vector3 center, Color color, float tileWorld)
    {
        float intensity = GameplayFeedback.Intensity;
        if (intensity <= 0f)
            return;
        int count = intensity >= 1f ? 4 : 2;
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0.2f, Mathf.PI - 0.2f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Spawn(center + new Vector3(0f, tileWorld * 0.2f, 0f), dir * tileWorld * Random.Range(1.4f, 2.3f), color, tileWorld * Random.Range(0.09f, 0.14f), Random.Range(0.3f, 0.42f), tileWorld * 8f);
        }
    }

    /// <summary>스테이지 클리어: 여러 색 점토 조각이 위로 크게 터진다.</summary>
    public void Confetti(Vector3 center, float tileWorld)
    {
        float intensity = GameplayFeedback.Intensity;
        if (intensity <= 0f)
            return;
        int count = Mathf.RoundToInt(Mathf.Lerp(14f, 34f, intensity));
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0.15f, Mathf.PI - 0.15f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Color c = i % 5 == 0 ? VisualTheme.Accent : VisualTheme.ClayColorForCount(1 + i % 6);
            Spawn(center, dir * tileWorld * Random.Range(4f, 7.5f), c, tileWorld * Random.Range(0.14f, 0.26f), Random.Range(0.7f, 1.05f), tileWorld * 11f);
        }
    }

    private void Spawn(Vector3 position, Vector3 velocity, Color color, float size, float life, float gravity)
    {
        ref Chunk c = ref chunks[cursor];
        cursor = (cursor + 1) % PoolSize;
        if (c.renderer == null)
            return;
        Sprite sprite = c.renderer.sprite;
        float spriteWorld = sprite != null ? Mathf.Max(0.0001f, sprite.bounds.size.x) : 1f;
        c.startScale = size / spriteWorld;
        c.renderer.transform.position = new Vector3(position.x, position.y, -0.2f);
        c.renderer.transform.localScale = Vector3.one * c.startScale;
        c.renderer.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        c.renderer.color = color;
        c.renderer.enabled = true;
        c.velocity = velocity;
        c.spin = Random.Range(-540f, 540f);
        c.age = 0f;
        c.life = life;
        c.gravity = gravity;
        c.color = color;
        c.active = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < PoolSize; i++)
        {
            ref Chunk c = ref chunks[i];
            if (!c.active)
                continue;
            c.age += dt;
            float t = c.age / Mathf.Max(0.01f, c.life);
            if (t >= 1f)
            {
                c.active = false;
                c.renderer.enabled = false;
                continue;
            }
            c.velocity += Vector3.down * c.gravity * dt;
            c.velocity *= 1f - Mathf.Clamp01(2.2f * dt);
            Transform tr = c.renderer.transform;
            tr.position += c.velocity * dt;
            tr.Rotate(0f, 0f, c.spin * dt);
            // 처음엔 살짝 커졌다가 줄어들며 사라진다(찰흙 조각 느낌).
            float scale = t < 0.15f ? Mathf.Lerp(0.7f, 1.1f, t / 0.15f) : Mathf.Lerp(1.1f, 0.2f, (t - 0.15f) / 0.85f);
            tr.localScale = Vector3.one * c.startScale * scale;
            Color col = c.color;
            col.a = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
            c.renderer.color = col;
        }
    }

    public void ClearAll()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            chunks[i].active = false;
            if (chunks[i].renderer != null)
                chunks[i].renderer.enabled = false;
        }
    }
}
