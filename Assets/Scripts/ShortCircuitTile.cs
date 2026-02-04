using UnityEngine;

/// <summary>
/// 네온 합선(ShortCircuit) 타일: 화살표 방향으로만 이동 가능.
/// 화살표 방향 셀로만 나갈 수 있고, 반대 방향 셀에서만 들어올 수 있음.
/// 타일과 이동 가능 셀 사이에 화살표(arrow.png) 배치.
/// </summary>
[RequireComponent(typeof(Tile))]
public class ShortCircuitTile : MonoBehaviour
{
    [Header("화살표")]
    [Tooltip("Resources 경로 (Assets/Resources/Sprites/arrow.png → Sprites/arrow). 없으면 인스펙터에서 할당")]
    [SerializeField] private string arrowSpritePath = "Sprites/arrow";
    [Tooltip("화살표 스케일 (타일 간격에 맞게)")]
    [SerializeField] private float arrowScale = 0.35f;
    [Tooltip("화살표 기본 방향이 위(Up)일 때 0. 오른쪽이면 -90 등")]
    [SerializeField] private float arrowRotationOffset = 0f;

    private Tile tile;
    /// <summary>방향: Up(0,-1), Down(0,1), Right(1,0), Left(-1,0) — 그리드 (col,row) 기준.</summary>
    private int dirX, dirY;
    private int exitX, exitY;
    private int entryX, entryY;
    private GameObject arrowObject;

    /// <summary>이동 가능한 셀 (나갈 수 있는 유일한 셀).</summary>
    public (int x, int y) ExitCell => (exitX, exitY);
    /// <summary>이 타일로 들어올 수 있는 유일한 셀 (화살표 반대 방향).</summary>
    public (int x, int y) EntryCell => (entryX, entryY);

    private void Awake()
    {
        tile = GetComponent<Tile>();
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
        entryX = cx - dirX;
        entryY = cy - dirY;

        bool exitInBounds = exitX >= 0 && exitX < stageWidth && exitY >= 0 && exitY < stageHeight;
        if (exitInBounds)
        {
            float exW = startX + exitX * (tileW + pad);
            float eyW = startY + exitY * (tileH + pad);
            Vector3 tilePos = transform.position;
            Vector3 exitPos = new Vector3(exW, eyW, 0f);
            CreateArrowBetween(tilePos, exitPos);
        }
    }

    private void ParseDirection(string dir)
    {
        switch (dir.ToUpperInvariant())
        {
            case "UP":   dirX = 0;  dirY = -1; break;
            case "DOWN": dirX = 0;  dirY = 1;  break;
            case "RIGHT": dirX = 1;  dirY = 0;  break;
            case "LEFT":  dirX = -1; dirY = 0;  break;
            default:     dirX = 0;  dirY = 1;  break; // Down
        }
    }

    /// <summary> (toX, toY)가 이 타일에서 나갈 수 있는 유일한 셀인지.</summary>
    public bool IsExitCell(int toX, int toY)
    {
        return toX == exitX && toY == exitY;
    }

    /// <summary> (fromX, fromY)에서 이 타일로 들어올 수 있는지 (반대 방향에서만 진입 가능).</summary>
    public bool IsValidEntryFrom(int fromX, int fromY)
    {
        return fromX == entryX && fromY == entryY;
    }

    private void CreateArrowBetween(Vector3 posA, Vector3 posB)
    {
        Sprite arrowSprite = Resources.Load<Sprite>(arrowSpritePath);
        if (arrowSprite == null)
            arrowSprite = Resources.Load<Sprite>("Sprites/arrrow"); // 오타 대체
        if (arrowSprite == null)
        {
            Debug.LogWarning("[ShortCircuitTile] Resources/Sprites/arrow.png (또는 arrrow.png)를 넣어주세요.");
            return;
        }

        Vector3 mid = (posA + posB) * 0.5f;
        mid.z = -0.35f;
        Vector2 dir = ((Vector2)(posB - posA)).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + arrowRotationOffset;

        arrowObject = new GameObject("ShortCircuitArrow");
        arrowObject.transform.SetParent(transform);
        arrowObject.transform.position = mid;
        arrowObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        arrowObject.transform.localScale = Vector3.one * arrowScale;

        var sr = arrowObject.AddComponent<SpriteRenderer>();
        sr.sprite = arrowSprite;
        sr.sortingOrder = 1;
    }

    /// <summary>
    /// 타일이 비활성(count 0)이면 화살표 숨김, 리셋 후 다시 활성화되면 화살표 표시.
    /// 타일 GameObject가 Destroy되면 화살표도 자식이라 함께 제거됨.
    /// </summary>
    private void LateUpdate()
    {
        if (arrowObject == null) return;
        bool shouldShow = tile != null && tile.IsActive;
        if (arrowObject.activeSelf != shouldShow)
            arrowObject.SetActive(shouldShow);
    }

    private void OnDestroy()
    {
        if (arrowObject != null)
            Destroy(arrowObject);
    }
}
