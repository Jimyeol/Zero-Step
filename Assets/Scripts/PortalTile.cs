using UnityEngine;

/// <summary>
/// 포탈(Portal) 타일: 다른 칸을 모두 지운 뒤 마지막 이동으로만 들어갈 수 있고, 들어가면 2페이즈 보드가 나온다.
/// 규칙 판정과 페이즈 전환은 GameManager가 하고, 이 컴포넌트는 소용돌이 표시와 빨아들이는 연출만 담당한다.
/// 잠겨 있을 때는 흐린 소용돌이가 천천히 돌고, 들어갈 수 있게 되면 진하게 빨리 돈다.
/// </summary>
[RequireComponent(typeof(Tile))]
public class PortalTile : MonoBehaviour
{
    public static readonly Color PortalColor = new Color32(0x7A, 0x5C, 0xC9, 0xFF);
    private const int ArmCount = 2;
    private const int PointsPerArm = 22;
    private const float SuckDuration = 0.6f;

    private Tile tile;
    private SpriteRenderer tileSpriteRenderer;
    private LineRenderer[] arms;
    private float angle;
    private float open;          // 0 = 잠김, 1 = 열림 (부드럽게 따라간다)
    private float suckStart = -10f;

    /// <summary>지금 들어갈 수 있는지(GameManager가 알려 준다).</summary>
    public bool IsOpen { get; set; }

    public bool IsSucking => Time.unscaledTime - suckStart < SuckDuration;
    public static float SuckSeconds => SuckDuration;

    private void Awake()
    {
        tile = GetComponent<Tile>();
        tileSpriteRenderer = GetComponent<SpriteRenderer>();
        arms = new LineRenderer[ArmCount];
        for (int i = 0; i < ArmCount; i++)
            arms[i] = CreateArm($"PortalArm{i}");
    }

    private LineRenderer CreateArm(string objectName)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = PointsPerArm;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.TransformZ;
        line.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.35f);
        if (tileSpriteRenderer != null)
        {
            if (tileSpriteRenderer.sharedMaterial != null)
                line.sharedMaterial = tileSpriteRenderer.sharedMaterial;
            line.sortingLayerID = tileSpriteRenderer.sortingLayerID;
            line.sortingOrder = tileSpriteRenderer.sortingOrder + 1;
        }
        return line;
    }

    /// <summary>타일 스프라이트 반 폭(월드 단위, 현재 크기 기준).</summary>
    public float WorldHalfSize
    {
        get
        {
            float half = tileSpriteRenderer != null && tileSpriteRenderer.sprite != null ? tileSpriteRenderer.sprite.bounds.extents.x : 0.5f;
            return half * Mathf.Abs(transform.lossyScale.x);
        }
    }

    /// <summary>
    /// 2페이즈 시작 칸 위에 남는 소용돌이 잔상: 포탈이 사라진 자리에서 천천히 멈추며 옅어진다(0.55초 뒤 스스로 없어진다).
    /// </summary>
    public static void SpawnAfterglow(Vector3 worldPosition, float worldHalfSize, SpriteRenderer sortingReference)
    {
        var go = new GameObject("PortalAfterglow");
        go.transform.position = worldPosition + new Vector3(0f, 0f, -0.03f);
        var glow = go.AddComponent<PortalAfterglow>();
        glow.Init(worldHalfSize, sortingReference);
    }

    /// <summary>포탈로 들어갔을 때: 소용돌이가 빠르게 돌며 커진다.</summary>
    public void PlaySuck()
    {
        suckStart = Time.unscaledTime;
    }

    private void LateUpdate()
    {
        bool visible = tile != null && tile.IsActive && !tile.IsGameOverBlackoutActive && transform.localScale.sqrMagnitude > 0.0001f;
        for (int i = 0; i < arms.Length; i++)
            arms[i].enabled = visible;
        if (!visible)
            return;

        float dt = Time.unscaledDeltaTime;
        open = Mathf.MoveTowards(open, IsOpen ? 1f : 0f, dt * 4f);
        float suck = Mathf.Clamp01((Time.unscaledTime - suckStart) / SuckDuration);
        bool sucking = suck < 1f;
        float speed = Mathf.Lerp(50f, 170f, open) + (sucking ? 900f * suck : 0f);
        angle -= speed * dt;

        float half = tileSpriteRenderer != null && tileSpriteRenderer.sprite != null
            ? tileSpriteRenderer.sprite.bounds.extents.x
            : 0.5f;
        float pulse = 1f + (IsOpen ? Mathf.Sin(Time.unscaledTime * 6f) * 0.08f : 0f);
        float radius = half * 0.62f * pulse * (sucking ? 1f + suck * 0.5f : 1f);
        Color color = Color.Lerp(new Color(PortalColor.r, PortalColor.g, PortalColor.b, 0.38f), PortalColor, open);
        if (sucking)
            color.a *= 1f - suck * 0.6f;
        float width = half * Mathf.Lerp(0.07f, 0.12f, open);

        for (int a = 0; a < arms.Length; a++)
        {
            LineRenderer line = arms[a];
            float baseAngle = angle + a * 360f / arms.Length;
            for (int k = 0; k < PointsPerArm; k++)
            {
                float t = k / (float)(PointsPerArm - 1);
                // 바깥에서 안으로 감겨 들어가는 나선 팔: 반지름은 줄고 각도는 늘어난다.
                float r = radius * Mathf.Lerp(1f, 0.28f, t);
                float deg = baseAngle + t * 200f;
                float rad = deg * Mathf.Deg2Rad;
                line.SetPosition(k, new Vector3(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r * 0.92f, -0.02f));
            }
            line.widthMultiplier = width;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, color.a * 0.4f);
        }
    }
}

/// <summary>포탈이 사라진 자리의 소용돌이 잔상(PortalTile.SpawnAfterglow).</summary>
public class PortalAfterglow : MonoBehaviour
{
    private const float Duration = 0.55f;
    private const int Points = 22;
    private LineRenderer[] arms;
    private float half;
    private float start;
    private float angle;

    public void Init(float worldHalfSize, SpriteRenderer sortingReference)
    {
        half = worldHalfSize;
        start = Time.unscaledTime;
        arms = new LineRenderer[2];
        for (int a = 0; a < arms.Length; a++)
        {
            var child = new GameObject($"Arm{a}");
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = Points;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.TransformZ;
            line.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.35f);
            if (sortingReference != null)
            {
                if (sortingReference.sharedMaterial != null)
                    line.sharedMaterial = sortingReference.sharedMaterial;
                line.sortingLayerID = sortingReference.sortingLayerID;
                line.sortingOrder = sortingReference.sortingOrder + 3;
            }
            arms[a] = line;
        }
    }

    private void LateUpdate()
    {
        float t = (Time.unscaledTime - start) / Duration;
        if (t >= 1f || arms == null)
        {
            Destroy(gameObject);
            return;
        }
        // 빠르게 돌던 소용돌이가 멈추며 바깥으로 퍼지고 옅어진다.
        angle -= Mathf.Lerp(900f, 60f, t) * Time.unscaledDeltaTime;
        float radius = half * Mathf.Lerp(0.9f, 1.25f, t);
        Color c = PortalTile.PortalColor;
        c.a = (1f - t) * 0.9f;
        for (int a = 0; a < arms.Length; a++)
        {
            float baseAngle = angle + a * 180f;
            for (int k = 0; k < Points; k++)
            {
                float u = k / (float)(Points - 1);
                float r = radius * Mathf.Lerp(1f, 0.3f, u);
                float rad = (baseAngle + u * 200f) * Mathf.Deg2Rad;
                arms[a].SetPosition(k, new Vector3(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r * 0.92f, 0f));
            }
            arms[a].widthMultiplier = half * 0.1f * (1f - t * 0.5f);
            arms[a].startColor = c;
            arms[a].endColor = new Color(c.r, c.g, c.b, c.a * 0.4f);
        }
    }
}
