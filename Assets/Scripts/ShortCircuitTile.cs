using UnityEngine;

/// <summary>
/// 네온 합선(ShortCircuit) 타일: 화살표 방향으로만 이동 가능.
/// 화살표 방향 셀로만 나갈 수 있고, 화살표 방향 쪽에서의 진입만 금지한다.
/// 시각적으로는 전용 타일 스프라이트를 사용한다.
/// </summary>
[RequireComponent(typeof(Tile))]
public class ShortCircuitTile : MonoBehaviour
{
    [Header("스프라이트")]
    [Tooltip("Resources 경로 (Assets/Resources/Sprites/short_circuit_tile.png → Sprites/short_circuit_tile)")]
    [SerializeField] private string tileSpritePath = "Sprites/short_circuit_tile";

    private Tile tile;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer directionVisualRenderer;
    private Transform directionVisualTransform;
    private Sprite tileSprite;
    /// <summary>방향: Up(0,1), Down(0,-1), Right(1,0), Left(-1,0) — 화면 배치 기준.</summary>
    private int dirX, dirY;
    private int exitX, exitY;

    /// <summary>이동 가능한 셀 (나갈 수 있는 유일한 셀).</summary>
    public (int x, int y) ExitCell => (exitX, exitY);
    /// <summary>이 타일로 들어오면 안 되는 셀 (화살표가 향한 방향 쪽).</summary>
    public (int x, int y) BlockedEntryCell => (exitX, exitY);
    public string DirectionLocalizationKey
    {
        get
        {
            if (dirX < 0)
                return "direction_left";
            if (dirX > 0)
                return "direction_right";
            if (dirY > 0)
                return "direction_up";
            return "direction_down";
        }
    }

    private void Awake()
    {
        tile = GetComponent<Tile>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        ApplyTileSprite();
    }

    private void LateUpdate()
    {
        SyncDirectionVisualRenderer();
        UpdateFlowChevron();
    }

    // 출구 쪽으로 흘러가는 작은 쉐브론: 밟기 전에 "이쪽으로만 나간다"를 보여준다(효과 강도 반영, Minimal은 정지 표시).
    private const float ChevronCycleSeconds = 0.8f;
    private LineRenderer flowChevron;

    private void EnsureFlowChevron()
    {
        if (flowChevron != null || spriteRenderer == null)
            return;

        var go = new GameObject("ShortCircuitFlowChevron");
        go.transform.SetParent(transform, false);
        flowChevron = go.AddComponent<LineRenderer>();
        flowChevron.useWorldSpace = false;
        flowChevron.positionCount = 3;
        flowChevron.numCapVertices = 3;
        flowChevron.numCornerVertices = 3;
        flowChevron.alignment = LineAlignment.TransformZ;
        if (spriteRenderer.sharedMaterial != null)
            flowChevron.sharedMaterial = spriteRenderer.sharedMaterial;
        flowChevron.sortingLayerID = spriteRenderer.sortingLayerID;
        flowChevron.sortingOrder = spriteRenderer.sortingOrder + 4;
        flowChevron.enabled = false;
    }

    private void UpdateFlowChevron()
    {
        if (VisualTheme.IsSoftClay)
            return;
        EnsureFlowChevron();
        if (flowChevron == null)
            return;

        bool visible = tile != null && tile.IsActive && !tile.IsGameOverBlackoutActive && transform.localScale.sqrMagnitude > 0.0001f;
        flowChevron.enabled = visible;
        if (!visible)
            return;

        // 로컬 좌표 기준 타일 반 크기(스프라이트 bounds는 월드 단위이므로 스케일로 나눈다).
        float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        float half = directionVisualRenderer != null && directionVisualRenderer.sprite != null
            ? directionVisualRenderer.sprite.bounds.extents.x
            : 0.5f;
        float intensity = GameplayFeedback.Intensity;
        float t = intensity > 0f ? Mathf.Repeat(Time.time / ChevronCycleSeconds, 1f) : 0.55f;
        Vector3 dir = new Vector3(dirX, dirY, 0f);
        Vector3 side = new Vector3(-dirY, dirX, 0f);
        float travel = Mathf.Lerp(0.1f, 0.62f, t) * half;
        float size = 0.2f * half;
        Vector3 tip = dir * (travel + size * 0.5f);
        Vector3 back = dir * (travel - size * 0.5f);
        flowChevron.SetPosition(0, back + side * size);
        flowChevron.SetPosition(1, tip);
        flowChevron.SetPosition(2, back - side * size);
        flowChevron.widthMultiplier = half * 0.09f;
        float alpha = intensity > 0f ? Mathf.Sin(t * Mathf.PI) * 0.85f : 0.6f;
        Color c = new Color(1f, 1f, 1f, alpha);
        flowChevron.startColor = c;
        flowChevron.endColor = c;
        flowChevron.transform.localPosition = new Vector3(0f, 0f, -0.02f / scale);
    }

    private void OnDestroy()
    {
        if (tile != null && directionVisualRenderer != null)
            tile.UnregisterLinkedTileRenderer(directionVisualRenderer);
    }

    /// <summary>
    /// GameManager가 생성 시 호출. direction 문자열과 그리드 범위로 출구/입구 셀 계산.
    /// </summary>
    public void Setup(string direction, int stageWidth, int stageHeight, float startX, float startY, float tileW, float tileH, float pad)
    {
        ParseDirection(direction ?? "Down");
        int cx = tile.X;
        int cy = tile.Y;
        exitX = cx + dirX;
        exitY = cy + dirY;
        ApplyDirectionVisualRotation();
        if (VisualTheme.IsSoftClay)
            VisualTheme.AttachCornerIcon(transform, VisualTheme.Icon.Arrow, GetClayArrowRotation());
    }

    private void ParseDirection(string dir)
    {
        switch (dir.ToUpperInvariant())
        {
            case "UP":   dirX = 0;  dirY = 1;  break;
            case "DOWN": dirX = 0;  dirY = -1; break;
            case "RIGHT": dirX = 1;  dirY = 0;  break;
            case "LEFT":  dirX = -1; dirY = 0;  break;
            default:     dirX = 0;  dirY = -1; break; // Down
        }
    }

    /// <summary> (toX, toY)가 이 타일에서 나갈 수 있는 유일한 셀인지.</summary>
    public bool IsExitCell(int toX, int toY)
    {
        return toX == exitX && toY == exitY;
    }

    /// <summary>(fromX, fromY)에서 이 타일로 진입이 금지되는지.</summary>
    public bool IsBlockedEntryFrom(int fromX, int fromY)
    {
        return fromX == exitX && fromY == exitY;
    }

    private void ApplyTileSprite()
    {
        if (spriteRenderer == null)
            return;
        // 점토 테마: 전용 타일 그림 대신 기본 점토 타일 + 모서리 화살표(Setup에서 방향 확정 후 붙임).
        if (VisualTheme.IsSoftClay)
            return;

        tileSprite = Resources.Load<Sprite>(tileSpritePath);
        if (tileSprite == null)
        {
            Debug.LogWarning($"[ShortCircuitTile] Resources/{tileSpritePath} 을(를) 찾을 수 없습니다.");
            return;
        }

        EnsureDirectionVisualRenderer();
        SyncDirectionVisualRenderer();
        ApplyDirectionVisualRotation();
    }

    private void EnsureDirectionVisualRenderer()
    {
        if (directionVisualRenderer != null)
            return;

        GameObject visualObject = new GameObject("ShortCircuitDirectionVisual");
        directionVisualTransform = visualObject.transform;
        directionVisualTransform.SetParent(transform);
        directionVisualTransform.localPosition = Vector3.zero;
        directionVisualTransform.localRotation = Quaternion.identity;
        directionVisualTransform.localScale = Vector3.one;

        directionVisualRenderer = visualObject.AddComponent<SpriteRenderer>();
        directionVisualRenderer.sprite = tileSprite;
        if (spriteRenderer.sharedMaterial != null)
            directionVisualRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
        if (tile != null)
            tile.RegisterLinkedTileRenderer(directionVisualRenderer);
    }

    private void SyncDirectionVisualRenderer()
    {
        if (directionVisualRenderer == null || spriteRenderer == null)
            return;

        directionVisualRenderer.sprite = tileSprite;
        directionVisualRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        directionVisualRenderer.maskInteraction = spriteRenderer.maskInteraction;
        if (tile == null)
        {
            directionVisualRenderer.color = spriteRenderer.color;
            directionVisualRenderer.sortingOrder = spriteRenderer.sortingOrder;
            directionVisualRenderer.enabled = true;
        }

        if (spriteRenderer.enabled)
            spriteRenderer.enabled = false;
    }

    private void ApplyDirectionVisualRotation()
    {
        if (directionVisualTransform == null)
            return;

        directionVisualTransform.localEulerAngles = new Vector3(0f, 0f, GetSpriteRotationZ());
    }

    /// <summary>모서리 화살표 회전(아이콘은 오른쪽을 가리킨다).</summary>
    private float GetClayArrowRotation()
    {
        if (dirX > 0) return 0f;
        if (dirX < 0) return 180f;
        return dirY > 0 ? 90f : -90f;
    }

    private float GetSpriteRotationZ()
    {
        if (dirX > 0)
            return 180f;
        if (dirY > 0)
            return -90f;
        if (dirY < 0)
            return 90f;

        return 0f;
    }
}
