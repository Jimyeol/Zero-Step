using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>
/// N x M 그리드를 동적으로 생성하고, 화면 비율에 맞춰 카메라로 그리드가 꽉 차게(Fit) 보이도록 조절함.
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("그리드 설정")]
    [SerializeField] private int gridRows = 3;
    [SerializeField] private int gridColumns = 3;
    [Tooltip("타일 사이 간격 (월드 단위)")]
    [SerializeField] private float padding = 0.2f;
    [Tooltip("그리드가 화면에 Fit될 때 여유 비율 (1.05 = 5% 여백). Bloom 등으로 잘리면 올려보세요.")]
    [SerializeField] private float fitMargin = 1.05f;

    [Header("참조")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private Camera mainCamera;
    [Tooltip("타일 생성 시 초기 숫자")]
    [SerializeField] private int initialTileNumber = 3;

    private float tileWidth;
    private float tileHeight;
    private float totalGridWidth;
    private float totalGridHeight;

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (tilePrefab == null || mainCamera == null)
        {
            Debug.LogError("[GridManager] Tile 프리팹 또는 Main Camera가 할당되지 않았습니다.");
            return;
        }

        EnsurePhysics2DRaycaster();
        EnsureCameraPostProcessingAndHDR();

        CacheTileSize();
        AdjustCameraToFitGrid();
        CreateGrid();
    }

    private void LateUpdate()
    {
        // Bloom/Volume 등이 카메라를 바꿀 수 있으므로, 매 프레임 Fit 재적용해 그리드가 화면 밖으로 나가지 않도록 함.
        if (mainCamera != null && totalGridWidth > 0f && totalGridHeight > 0f)
            AdjustCameraToFitGrid();
    }

    private void Update()
    {
        // 터치/클릭 폴백: EventSystem 미동작 시에도 타일 감소 처리
        if (!DetectTileTap(out Tile hitTile))
            return;
        if (hitTile != null && hitTile.IsActive())
            hitTile.TryDecreaseNumber();
    }

    /// <summary>
    /// 이번 프레임에 터치/클릭이 있고, 그 위치에 타일이 있으면 해당 Tile 반환.
    /// </summary>
    private bool DetectTileTap(out Tile hitTile)
    {
        hitTile = null;
        if (mainCamera == null)
            return false;

        bool touched = false;
        Vector2 screenPoint = Vector2.zero;

        // 새 Input System만 사용 (Player Settings에서 Input System Package 사용 시)
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            touched = true;
            screenPoint = Mouse.current.position.ReadValue();
        }
        if (!touched && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            touched = true;
            screenPoint = Touchscreen.current.primaryTouch.position.ReadValue();
        }

        if (!touched)
            return false;

        // 2D: ScreenToWorldPoint 후 (x,y)만 사용. z는 타일이 있는 평면(0)까지의 거리.
        float camZ = mainCamera.transform.position.z;
        Vector3 world3 = mainCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, Mathf.Abs(camZ)));
        Vector2 worldPoint = new Vector2(world3.x, world3.y);

        // Raycast(방향 0)는 동작하지 않으므로 OverlapPoint로 해당 위치의 콜라이더 검사.
        Collider2D hitCol = Physics2D.OverlapPoint(worldPoint);
        if (hitCol != null)
            hitTile = hitCol.GetComponent<Tile>();
        return true;
    }

    /// <summary>
    /// Game 뷰에서 Bloom 등 HDR 포스트프로세싱이 적용되도록 Main Camera에 Post Processing + HDR 활성화.
    /// </summary>
    private void EnsureCameraPostProcessingAndHDR()
    {
        if (mainCamera == null)
            return;

        var camData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
        if (camData != null)
        {
            camData.renderPostProcessing = true;
            camData.allowHDROutput = true;
        }
    }

    /// <summary>
    /// 터치/클릭 입력을 위해 EventSystem(Input System용) 및 Main Camera의 Physics2DRaycaster를 보장함.
    /// </summary>
    private void EnsurePhysics2DRaycaster()
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
            // 기존 StandaloneInputModule(구 Input) 제거 후 Input System용 모듈 사용
            var oldModule = es.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                Destroy(oldModule);
                if (es.GetComponent<InputSystemUIInputModule>() == null)
                    es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        if (mainCamera.GetComponent<Physics2DRaycaster>() == null)
            mainCamera.gameObject.AddComponent<Physics2DRaycaster>();
    }

    /// <summary>
    /// 프리팹의 스프라이트 크기로 타일 월드 크기 계산 (스케일 1 기준).
    /// </summary>
    private void CacheTileSize()
    {
        var sr = tilePrefab.GetComponentInChildren<SpriteRenderer>(true);
        if (sr == null || sr.sprite == null)
        {
            Debug.LogError("[GridManager] Tile 프리팹에 SpriteRenderer와 Sprite가 필요합니다.");
            tileWidth = 1f;
            tileHeight = 1f;
            return;
        }

        Bounds b = sr.sprite.bounds;
        Vector3 scale = tilePrefab.transform.lossyScale;
        tileWidth = b.size.x * scale.x;
        tileHeight = b.size.y * scale.y;

        totalGridWidth = gridColumns * tileWidth + (gridColumns - 1) * padding;
        totalGridHeight = gridRows * tileHeight + (gridRows - 1) * padding;
    }

    /// <summary>
    /// iOS, AOS, 태블릿 등 세로 모드에서 그리드가 화면 정중앙에 꽉 차게(Fit) 보이도록
    /// Orthographic Size를 조절함.
    /// </summary>
    private void AdjustCameraToFitGrid()
    {
        if (!mainCamera.orthographic)
        {
            Debug.LogWarning("[GridManager] Orthographic 카메라가 아닙니다. Fit 로직은 Orthographic에 맞춰져 있습니다.");
            return;
        }

        float aspect = (float)Screen.width / Screen.height;
        float sizeByHeight = totalGridHeight * 0.5f * fitMargin;
        float sizeByWidth = (totalGridWidth * 0.5f) / aspect * fitMargin;

        mainCamera.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth);
        mainCamera.transform.position = new Vector3(0f, 0f, mainCamera.transform.position.z);
    }

    /// <summary>
    /// Tile 프리팹을 복제해 N x M 그리드로 배치하고, 초기 숫자 설정.
    /// </summary>
    private void CreateGrid()
    {
        float startX = -totalGridWidth * 0.5f + tileWidth * 0.5f;
        float startY = -totalGridHeight * 0.5f + tileHeight * 0.5f;

        for (int row = 0; row < gridRows; row++)
        {
            for (int col = 0; col < gridColumns; col++)
            {
                float x = startX + col * (tileWidth + padding);
                float y = startY + row * (tileHeight + padding);

                GameObject tileObj = Instantiate(tilePrefab, transform);
                tileObj.transform.position = new Vector3(x, y, 0f);
                tileObj.name = $"Tile_{row}_{col}";

                var tile = tileObj.GetComponent<Tile>();
                if (tile != null)
                    tile.SetNumber(initialTileNumber);
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 해상도 변경 시 에디터에서 다시 Fit 적용 (실행 중에만).
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying && mainCamera != null && tilePrefab != null && gridRows > 0 && gridColumns > 0)
        {
            CacheTileSize();
            AdjustCameraToFitGrid();
        }
    }
#endif
}
