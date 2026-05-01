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
    }

    private void SyncDirectionVisualRenderer()
    {
        if (directionVisualRenderer == null || spriteRenderer == null)
            return;

        directionVisualRenderer.sprite = tileSprite;
        directionVisualRenderer.color = spriteRenderer.color;
        directionVisualRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        directionVisualRenderer.sortingOrder = spriteRenderer.sortingOrder;
        directionVisualRenderer.maskInteraction = spriteRenderer.maskInteraction;
        directionVisualRenderer.enabled = tile == null || tile.IsActive;

        if (spriteRenderer.enabled)
            spriteRenderer.enabled = false;
    }

    private void ApplyDirectionVisualRotation()
    {
        if (directionVisualTransform == null)
            return;

        directionVisualTransform.localEulerAngles = new Vector3(0f, 0f, GetSpriteRotationZ());
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
