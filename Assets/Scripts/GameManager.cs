using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>
/// 네온 퍼즐 게임 매니저: JSON 스테이지 로드, 그리드 생성(count==0 스킵), 드래그 경로, Line Renderer, Stage Clear 시 다음 스테이지.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("그리드 설정")]
    [SerializeField] private float padding = 0.2f;
    [SerializeField] private float fitMargin = 1.05f;
    [Tooltip("스테이지 JSON 없을 때 폴백용")]
    [SerializeField] private int fallbackRows = 3;
    [SerializeField] private int fallbackCols = 3;
    [SerializeField] private int fallbackInitialNumber = 3;

    [Header("참조")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private Camera mainCamera;

    [Header("라인 렌더러")]
    [SerializeField] private float lineWidth = 0.15f;
    [SerializeField] private Color lineColor = new Color(1f, 0.5f, 1f, 1f);

    [Header("스테이지")]
    [SerializeField] private int startStageIndex = 1;
    [SerializeField] private float nextStageDelay = 1.5f;

    private int currentStageIndex;
    private float tileWidth;
    private float tileHeight;
    private float totalGridWidth;
    private float totalGridHeight;
    private int stageWidth;
    private int stageHeight;
    private Tile[,] tiles;
    private List<Tile> currentPath = new List<Tile>();
    private bool isDragging;
    private LineRenderer lineRenderer;
    private bool stageCleared;

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (tilePrefab == null || mainCamera == null)
        {
            Debug.LogError("[GameManager] Tile 프리팹 또는 Main Camera가 할당되지 않았습니다.");
            return;
        }

        EnsureInputAndRaycaster();
        EnsureCameraPostProcessingAndHDR();
        CreateLineRenderer();
        CacheTileSizeFromPrefab();

        currentStageIndex = startStageIndex;
        StageData data = StageManager.LoadStage(currentStageIndex);
        if (data != null)
            CreateGridFromStageData(data);
        else
            CreateGridFallback();
        AdjustCameraToFitGrid();
    }

    private void LateUpdate()
    {
        if (mainCamera != null && totalGridWidth > 0f && totalGridHeight > 0f)
            AdjustCameraToFitGrid();
    }

    private void Update()
    {
        if (stageCleared)
            return;

        UpdateDragAndPath();
    }

    /// <summary>
    /// 드래그 입력 처리: 누르면 경로 시작, 이동 시 인접 타일만 추가, 떼면 경로 적용 후 라인 갱신 및 승리 체크.
    /// </summary>
    private void UpdateDragAndPath()
    {
        Vector2 screenPoint = GetPointerScreenPosition();
        bool pointerDown = IsPointerDown();
        bool pointerUp = IsPointerUp();
        bool pointerHeld = IsPointerHeld();

        if (pointerDown)
        {
            Tile hit = GetTileAtScreen(screenPoint);
            if (hit != null && hit.IsActive)
            {
                isDragging = true;
                currentPath.Clear();
                currentPath.Add(hit);
            }
        }
        else if (isDragging && (pointerHeld || pointerUp))
        {
            Tile hit = GetTileAtScreen(screenPoint);
            if (hit != null && hit.IsActive && !currentPath.Contains(hit))
            {
                Tile last = currentPath[currentPath.Count - 1];
                if (IsAdjacent(last, hit))
                    currentPath.Add(hit);
            }

            if (pointerUp)
            {
                isDragging = false;
                ApplyPathAndUpdateLine();
                CheckStageClear();
            }
        }

        if (isDragging)
            UpdateLineRendererPositions();
    }

    private Vector2 GetPointerScreenPosition()
    {
        if (Mouse.current != null && Mouse.current.position.IsActuated())
            return Mouse.current.position.ReadValue();
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return Touchscreen.current.primaryTouch.position.ReadValue();
        return Vector2.zero;
    }

    private bool IsPointerDown()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;
        return false;
    }

    private bool IsPointerUp()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
            return true;
        return false;
    }

    private bool IsPointerHeld()
    {
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return true;
        return false;
    }

    private Tile GetTileAtScreen(Vector2 screenPoint)
    {
        if (mainCamera == null) return null;
        float camZ = mainCamera.transform.position.z;
        Vector3 world3 = mainCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, Mathf.Abs(camZ)));
        Vector2 worldPoint = new Vector2(world3.x, world3.y);
        Collider2D col = Physics2D.OverlapPoint(worldPoint);
        return col != null ? col.GetComponent<Tile>() : null;
    }

    /// <summary>
    /// 상하좌우 인접 여부 (대각선 불가).
    /// </summary>
    private bool IsAdjacent(Tile a, Tile b)
    {
        int dx = Mathf.Abs(a.X - b.X);
        int dy = Mathf.Abs(a.Y - b.Y);
        return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
    }

    /// <summary>
    /// 경로에 있는 타일마다 숫자 1 감소, 라인 초기화 후 경로 비우기.
    /// </summary>
    private void ApplyPathAndUpdateLine()
    {
        foreach (Tile t in currentPath)
            t.DecreaseNumber();
        currentPath.Clear();
        UpdateLineRendererPositions();
    }

    private void UpdateLineRendererPositions()
    {
        if (lineRenderer == null) return;

        if (currentPath.Count == 0)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        lineRenderer.positionCount = currentPath.Count;
        for (int i = 0; i < currentPath.Count; i++)
        {
            Vector3 p = currentPath[i].transform.position;
            p.z = -0.5f;
            lineRenderer.SetPosition(i, p);
        }
    }

    /// <summary>
    /// 모든 타일 숫자가 0이면 Stage Clear 로그.
    /// </summary>
    private void CheckStageClear()
    {
        if (tiles == null) return;
        for (int row = 0; row < stageHeight; row++)
            for (int col = 0; col < stageWidth; col++)
                if (tiles[row, col] != null && tiles[row, col].IsActive)
                    return;
        stageCleared = true;
        Debug.Log("Stage Clear");
        StartCoroutine(LoadNextStageAfterDelay());
    }

    private IEnumerator LoadNextStageAfterDelay()
    {
        yield return new WaitForSeconds(nextStageDelay);
        currentStageIndex++;
        StageData data = StageManager.LoadStage(currentStageIndex);
        if (data == null)
        {
            Debug.Log("All stages clear. 처음 스테이지부터 다시.");
            currentStageIndex = 1;
            data = StageManager.LoadStage(1);
            if (data == null)
            {
                stageCleared = false;
                yield break;
            }
        }
        ClearTiles();
        CreateGridFromStageData(data);
        stageCleared = false;
    }

    private void ClearTiles()
    {
        if (tiles != null)
        {
            for (int row = 0; row < stageHeight; row++)
                for (int col = 0; col < stageWidth; col++)
                    if (tiles[row, col] != null)
                    {
                        Destroy(tiles[row, col].gameObject);
                        tiles[row, col] = null;
                    }
        }
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponent<Tile>() != null)
                Destroy(child.gameObject);
        }
    }

    private void CreateLineRenderer()
    {
        GameObject lineGo = new GameObject("PathLine");
        lineGo.transform.SetParent(transform);

        lineRenderer = lineGo.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth * 0.6f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;
    }

    private void EnsureInputAndRaycaster()
    {
        EventSystem es = FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            var esGo = new GameObject("EventSystem");
            es = esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }
        else
        {
            var oldModule = es.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                Destroy(oldModule);
                if (es.GetComponent<InputSystemUIInputModule>() == null)
                    es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        if (mainCamera != null && mainCamera.GetComponent<Physics2DRaycaster>() == null)
            mainCamera.gameObject.AddComponent<Physics2DRaycaster>();
    }

    private void EnsureCameraPostProcessingAndHDR()
    {
        if (mainCamera == null) return;
        var camData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
        if (camData != null)
        {
            camData.renderPostProcessing = true;
            camData.allowHDROutput = true;
        }
    }

    private void CacheTileSizeFromPrefab()
    {
        var sr = tilePrefab.GetComponentInChildren<SpriteRenderer>(true);
        if (sr == null || sr.sprite == null)
        {
            tileWidth = 1f;
            tileHeight = 1f;
            return;
        }
        Bounds b = sr.sprite.bounds;
        Vector3 scale = tilePrefab.transform.lossyScale;
        tileWidth = b.size.x * scale.x;
        tileHeight = b.size.y * scale.y;
    }

    private void AdjustCameraToFitGrid()
    {
        if (mainCamera == null || !mainCamera.orthographic) return;
        float aspect = (float)Screen.width / Screen.height;
        float sizeByHeight = totalGridHeight * 0.5f * fitMargin;
        float sizeByWidth = (totalGridWidth * 0.5f) / aspect * fitMargin;
        mainCamera.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth);
        mainCamera.transform.position = new Vector3(0f, 0f, mainCamera.transform.position.z);
    }

    /// <summary>
    /// JSON 스테이지 데이터로 그리드 생성. count가 0인 셀은 인스턴스화 건너뛰고, startPoint 타일은 시작점 표시.
    /// </summary>
    private void CreateGridFromStageData(StageData data)
    {
        if (data.cells == null || data.startPoint == null) return;

        stageWidth = data.width;
        stageHeight = data.height;
        totalGridWidth = data.width * tileWidth + (data.width - 1) * padding;
        totalGridHeight = data.height * tileHeight + (data.height - 1) * padding;

        tiles = new Tile[data.height, data.width];
        float startX = -totalGridWidth * 0.5f + tileWidth * 0.5f;
        float startY = -totalGridHeight * 0.5f + tileHeight * 0.5f;

        foreach (CellData cell in data.cells)
        {
            if (cell.count <= 0) continue;

            float wx = startX + cell.x * (tileWidth + padding);
            float wy = startY + cell.y * (tileHeight + padding);

            GameObject tileObj = Instantiate(tilePrefab, transform);
            tileObj.transform.position = new Vector3(wx, wy, 0f);
            tileObj.name = $"Tile_{cell.y}_{cell.x}";

            Tile tile = tileObj.GetComponent<Tile>();
            if (tile != null)
            {
                tile.SetGridPosition(cell.x, cell.y);
                tile.SetNumber(cell.count);
                if (data.startPoint.x == cell.x && data.startPoint.y == cell.y)
                    tile.SetAsStartPoint(true);
                tiles[cell.y, cell.x] = tile;
            }
        }
    }

    /// <summary>
    /// 스테이지 JSON 없을 때 폴백: 동일 크기 그리드 전체 생성.
    /// </summary>
    private void CreateGridFallback()
    {
        stageWidth = fallbackCols;
        stageHeight = fallbackRows;
        totalGridWidth = fallbackCols * tileWidth + (fallbackCols - 1) * padding;
        totalGridHeight = fallbackRows * tileHeight + (fallbackRows - 1) * padding;

        tiles = new Tile[fallbackRows, fallbackCols];
        float startX = -totalGridWidth * 0.5f + tileWidth * 0.5f;
        float startY = -totalGridHeight * 0.5f + tileHeight * 0.5f;

        for (int row = 0; row < fallbackRows; row++)
        {
            for (int col = 0; col < fallbackCols; col++)
            {
                float x = startX + col * (tileWidth + padding);
                float y = startY + row * (tileHeight + padding);

                GameObject tileObj = Instantiate(tilePrefab, transform);
                tileObj.transform.position = new Vector3(x, y, 0f);
                tileObj.name = $"Tile_{row}_{col}";

                Tile tile = tileObj.GetComponent<Tile>();
                if (tile != null)
                {
                    tile.SetGridPosition(col, row);
                    tile.SetNumber(fallbackInitialNumber);
                    tiles[row, col] = tile;
                }
            }
        }
    }
}
