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
        float pulse = 1f + (IsOpen ? Mathf.Sin(Time.unscaledTime * 6f) * 0.05f : 0f);
        float radius = half * 0.62f * pulse * (sucking ? 1f + suck * 0.5f : 1f);
        Color color = Color.Lerp(new Color(PortalColor.r, PortalColor.g, PortalColor.b, 0.38f), PortalColor, open);
        if (sucking)
            color.a *= 1f - suck * 0.6f;
        float width = half * Mathf.Lerp(0.07f, 0.1f, open);

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
