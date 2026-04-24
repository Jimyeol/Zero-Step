using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Creates a camera-fitted procedural grid background with a bottom cyan glow.
/// Textures and materials are generated at runtime, so no user-supplied sprite files are required.
/// </summary>
public class ProceduralGridBackground : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool followCameraPosition = true;
    [SerializeField] private float backgroundZ = 2f;

    [Header("Base Plane")]
    [SerializeField] private Color basePlaneColor = new Color(0.008f, 0.031f, 0.051f, 1f);
    [SerializeField] private int baseSortingOrder = -120;

    [Header("Grid")]
    [SerializeField] [Min(16)] private int gridTextureSize = 128;
    [SerializeField] [Min(4)] private int gridCellPixels = 16;
    [SerializeField] [Min(1)] private int gridLinePixels = 1;
    [SerializeField] [Min(0.05f)] private float gridWorldCellSize = 1.1f;
    [SerializeField] private Color gridLineColor = new Color(0.03f, 0.34f, 0.42f, 0.34f);
    [SerializeField] private FilterMode gridFilterMode = FilterMode.Bilinear;
    [SerializeField] private int gridSortingOrder = -110;

    [Header("Glow")]
    [SerializeField] [Min(32)] private int glowTextureSize = 256;
    [SerializeField] private Color glowColor = new Color(0f, 0.88f, 1f, 0.72f);
    [SerializeField] [Min(0f)] private float glowIntensity = 0.135f;
    [SerializeField] [Min(0.1f)] private float glowWidthScale = 1.45f;
    [SerializeField] [Min(0.1f)] private float glowHeightScale = 0.72f;
    [SerializeField] private float glowYOffset = -0.65f;
    [SerializeField] private int glowSortingOrder = -100;

    [Header("Optional Core Glow")]
    [SerializeField] private bool enableCoreGlow;
    [SerializeField] [Min(0.1f)] private float coreGlowWidthScale = 0.55f;
    [SerializeField] [Min(0.1f)] private float coreGlowHeightScale = 0.32f;
    [SerializeField] private float coreGlowYOffset = -0.78f;
    [SerializeField] private int coreGlowSortingOrder = -99;

    [Header("Diagnostics")]
    [SerializeField] private bool logRebuilds;

    public int TextureRebuildCount { get; private set; }
    public int MaterialRebuildCount { get; private set; }

    private const string BasePlaneName = "BasePlane";
    private const string GridPlaneName = "GridPlane";
    private const string BottomGlowName = "BottomGlow";
    private const string BottomGlowCoreName = "BottomGlowCore";
    private const float MetricEpsilon = 0.0001f;

    private Transform basePlane;
    private Transform gridPlane;
    private Transform glowPlane;
    private Transform coreGlowPlane;

    private Mesh quadMesh;
    private Texture2D gridTexture;
    private Texture2D glowTexture;
    private Material baseMaterial;
    private Material gridMaterial;
    private Material glowMaterial;
    private Material coreGlowMaterial;

    private float cachedOrthoSize = -1f;
    private float cachedAspect = -1f;
    private int cachedScreenWidth = -1;
    private int cachedScreenHeight = -1;
    private Vector3 cachedCameraPosition = new Vector3(float.NaN, float.NaN, float.NaN);

    private void Awake()
    {
        EnsureSetup();
    }

    private void OnEnable()
    {
        EnsureSetup();
        ForceRefit();
    }

    private void LateUpdate()
    {
        EnsureSetup();
        RefitIfNeeded();
    }

    private void OnDestroy()
    {
        ReleaseGeneratedResources();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        gridTextureSize = Mathf.Max(16, gridTextureSize);
        gridCellPixels = Mathf.Clamp(gridCellPixels, 4, gridTextureSize);
        gridTextureSize = SnapTextureSizeToWholeCells(gridTextureSize, gridCellPixels);
        gridLinePixels = Mathf.Clamp(gridLinePixels, 1, gridCellPixels);
        gridWorldCellSize = Mathf.Max(0.05f, gridWorldCellSize);
        glowTextureSize = Mathf.Max(32, glowTextureSize);
        glowIntensity = Mathf.Max(0f, glowIntensity);
        glowWidthScale = Mathf.Max(0.1f, glowWidthScale);
        glowHeightScale = Mathf.Max(0.1f, glowHeightScale);
        coreGlowWidthScale = Mathf.Max(0.1f, coreGlowWidthScale);
        coreGlowHeightScale = Mathf.Max(0.1f, coreGlowHeightScale);
    }
#endif

    private void EnsureSetup()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        EnsureQuadMesh();
        EnsureGeneratedTextures();
        EnsureMaterials();
        EnsurePlanes();
    }

    private void EnsureQuadMesh()
    {
        if (quadMesh != null)
            return;

        quadMesh = new Mesh
        {
            name = "ProceduralBackgroundQuad"
        };
        quadMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        quadMesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        quadMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        quadMesh.RecalculateBounds();
        quadMesh.hideFlags = HideFlags.DontSave;
    }

    private void EnsureGeneratedTextures()
    {
        if (gridTexture == null)
            gridTexture = CreateGridTexture();
        if (glowTexture == null)
            glowTexture = CreateGlowTexture();
    }

    private Texture2D CreateGridTexture()
    {
        int cell = Mathf.Clamp(gridCellPixels, 4, Mathf.Max(16, gridTextureSize));
        int size = SnapTextureSizeToWholeCells(gridTextureSize, cell);
        int line = Mathf.Clamp(gridLinePixels, 1, cell);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "GeneratedGridTexture",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = gridFilterMode,
            hideFlags = HideFlags.DontSave
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;

        for (int y = 0; y < size; y++)
        {
            bool horizontalLine = (y % cell) < line;
            for (int x = 0; x < size; x++)
            {
                bool verticalLine = (x % cell) < line;
                if (horizontalLine || verticalLine)
                    pixels[y * size + x] = gridLineColor;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        TextureRebuildCount++;
        LogRebuild("grid texture", TextureRebuildCount);
        return texture;
    }

    private Texture2D CreateGlowTexture()
    {
        int size = Mathf.Max(32, glowTextureSize);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "GeneratedBottomGlowTexture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(0.5f, 0.02f);
        for (int y = 0; y < size; y++)
        {
            float v = size <= 1 ? 0f : (float)y / (size - 1);
            for (int x = 0; x < size; x++)
            {
                float u = size <= 1 ? 0f : (float)x / (size - 1);
                float dx = (u - center.x) / 0.58f;
                float dy = (v - center.y) / 0.78f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float radial = Mathf.Clamp01(1f - distance);
                float verticalFade = Mathf.Clamp01(1f - v * 0.82f);
                float alpha = Mathf.SmoothStep(0f, 1f, radial) * verticalFade * glowColor.a;
                Color color = new Color(
                    glowColor.r * glowIntensity,
                    glowColor.g * glowIntensity,
                    glowColor.b * glowIntensity,
                    alpha);
                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        TextureRebuildCount++;
        LogRebuild("glow texture", TextureRebuildCount);
        return texture;
    }

    private void EnsureMaterials()
    {
        if (baseMaterial == null)
            baseMaterial = CreateBaseMaterial();
        if (gridMaterial == null)
            gridMaterial = CreateGridMaterial();
        if (glowMaterial == null)
            glowMaterial = CreateGlowMaterial("GeneratedBottomGlowMaterial");
        if (coreGlowMaterial == null)
            coreGlowMaterial = CreateGlowMaterial("GeneratedBottomGlowCoreMaterial");
    }

    private Material CreateBaseMaterial()
    {
        Shader shader = FindFirstShader("Universal Render Pipeline/Unlit", "Unlit/Color", "Sprites/Default");
        Material material = new Material(shader)
        {
            name = "GeneratedBackgroundBaseMaterial",
            hideFlags = HideFlags.DontSave
        };
        SetMaterialColor(material, basePlaneColor);
        material.renderQueue = (int)RenderQueue.Geometry;
        MaterialRebuildCount++;
        LogRebuild("base material", MaterialRebuildCount);
        return material;
    }

    private Material CreateGridMaterial()
    {
        Shader shader = FindFirstShader("Universal Render Pipeline/Unlit", "Unlit/Transparent", "Sprites/Default");
        Material material = new Material(shader)
        {
            name = "GeneratedBackgroundGridMaterial",
            hideFlags = HideFlags.DontSave
        };
        ConfigureTransparentMaterial(material);
        SetMaterialColor(material, Color.white);
        SetMaterialTexture(material, gridTexture);
        material.renderQueue = (int)RenderQueue.Transparent;
        MaterialRebuildCount++;
        LogRebuild("grid material", MaterialRebuildCount);
        return material;
    }

    private Material CreateGlowMaterial(string materialName)
    {
        Shader shader = FindFirstShader("Legacy Shaders/Particles/Additive", "Particles/Standard Unlit", "Sprites/Default");
        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.DontSave
        };
        ConfigureTransparentMaterial(material);
        SetMaterialColor(material, Color.white);
        SetMaterialTexture(material, glowTexture);
        material.renderQueue = (int)RenderQueue.Transparent;
        MaterialRebuildCount++;
        LogRebuild(materialName, MaterialRebuildCount);
        return material;
    }

    private static Shader FindFirstShader(params string[] shaderNames)
    {
        for (int i = 0; i < shaderNames.Length; i++)
        {
            Shader shader = Shader.Find(shaderNames[i]);
            if (shader != null)
                return shader;
        }

        Shader fallback = Shader.Find("Sprites/Default");
        if (fallback != null)
            return fallback;

        return Shader.Find("Unlit/Color");
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", color);
        material.color = color;
    }

    private static void SetMaterialTexture(Material material, Texture texture)
    {
        if (material == null || texture == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
    }

    private static void SetMaterialTextureScale(Material material, Vector2 scale)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTextureScale("_BaseMap", scale);
        if (material.HasProperty("_MainTex"))
            material.SetTextureScale("_MainTex", scale);
    }

    private void EnsurePlanes()
    {
        basePlane = EnsurePlane(BasePlaneName, baseMaterial, baseSortingOrder);
        gridPlane = EnsurePlane(GridPlaneName, gridMaterial, gridSortingOrder);
        glowPlane = EnsurePlane(BottomGlowName, glowMaterial, glowSortingOrder);
        coreGlowPlane = EnsurePlane(BottomGlowCoreName, coreGlowMaterial, coreGlowSortingOrder);
        if (coreGlowPlane != null)
            coreGlowPlane.gameObject.SetActive(enableCoreGlow);
    }

    private Transform EnsurePlane(string planeName, Material material, int sortingOrder)
    {
        Transform child = transform.Find(planeName);
        if (child == null)
        {
            GameObject plane = new GameObject(planeName);
            plane.hideFlags = HideFlags.DontSave;
            child = plane.transform;
            child.SetParent(transform, false);
        }
        else
        {
            child.gameObject.hideFlags = HideFlags.DontSave;
        }

        MeshFilter meshFilter = child.GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = child.gameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = quadMesh;

        MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = child.gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.sortingLayerName = "Default";
        meshRenderer.sortingOrder = sortingOrder;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        Collider collider = child.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
                Destroy(collider);
            else
                DestroyImmediate(collider);
        }

        return child;
    }

    private void RefitIfNeeded()
    {
        if (targetCamera == null || !targetCamera.orthographic)
            return;

        bool metricsChanged =
            Mathf.Abs(cachedOrthoSize - targetCamera.orthographicSize) > MetricEpsilon ||
            Mathf.Abs(cachedAspect - targetCamera.aspect) > MetricEpsilon ||
            cachedScreenWidth != Screen.width ||
            cachedScreenHeight != Screen.height;

        bool cameraMoved = followCameraPosition && (targetCamera.transform.position - cachedCameraPosition).sqrMagnitude > MetricEpsilon;
        if (!metricsChanged && !cameraMoved)
            return;

        RefitPlanes();
    }

    private void ForceRefit()
    {
        cachedOrthoSize = -1f;
        cachedAspect = -1f;
        cachedScreenWidth = -1;
        cachedScreenHeight = -1;
        cachedCameraPosition = new Vector3(float.NaN, float.NaN, float.NaN);
        RefitIfNeeded();
    }

    private void RefitPlanes()
    {
        if (targetCamera == null || !targetCamera.orthographic)
            return;

        float height = targetCamera.orthographicSize * 2f;
        float width = height * targetCamera.aspect;
        Vector3 cameraPosition = targetCamera.transform.position;
        if (followCameraPosition)
            transform.position = new Vector3(cameraPosition.x, cameraPosition.y, backgroundZ);

        SetPlaneTransform(basePlane, Vector3.zero, width, height);
        SetPlaneTransform(gridPlane, Vector3.back * 0.01f, width, height);

        float glowWidth = width * glowWidthScale;
        float glowHeight = height * glowHeightScale;
        float glowCenterY = -height * 0.5f + glowHeight * 0.5f + glowYOffset;
        SetPlaneTransform(glowPlane, new Vector3(0f, glowCenterY, -0.02f), glowWidth, glowHeight);

        float coreWidth = width * coreGlowWidthScale;
        float coreHeight = height * coreGlowHeightScale;
        float coreCenterY = -height * 0.5f + coreHeight * 0.5f + coreGlowYOffset;
        SetPlaneTransform(coreGlowPlane, new Vector3(0f, coreCenterY, -0.03f), coreWidth, coreHeight);

        float cellsPerTexture = GetGridCellsPerTexture();
        Vector2 tiling = new Vector2(
            Mathf.Max(0.001f, width / Mathf.Max(0.05f, gridWorldCellSize) / cellsPerTexture),
            Mathf.Max(0.001f, height / Mathf.Max(0.05f, gridWorldCellSize) / cellsPerTexture));
        SetMaterialTextureScale(gridMaterial, tiling);

        cachedOrthoSize = targetCamera.orthographicSize;
        cachedAspect = targetCamera.aspect;
        cachedScreenWidth = Screen.width;
        cachedScreenHeight = Screen.height;
        cachedCameraPosition = cameraPosition;
    }

    private float GetGridCellsPerTexture()
    {
        int cell = Mathf.Clamp(gridCellPixels, 4, Mathf.Max(16, gridTextureSize));
        int size = SnapTextureSizeToWholeCells(gridTextureSize, cell);
        return Mathf.Max(1f, size / (float)cell);
    }

    private static int SnapTextureSizeToWholeCells(int requestedSize, int cellPixels)
    {
        int cell = Mathf.Max(1, cellPixels);
        int minimumCells = Mathf.CeilToInt(16f / cell);
        int requestedCells = Mathf.RoundToInt(Mathf.Max(16, requestedSize) / (float)cell);
        int cells = Mathf.Max(1, minimumCells, requestedCells);
        return cells * cell;
    }

    private static void SetPlaneTransform(Transform plane, Vector3 localPosition, float width, float height)
    {
        if (plane == null)
            return;

        plane.localPosition = localPosition;
        plane.localRotation = Quaternion.identity;
        plane.localScale = new Vector3(width, height, 1f);
    }

    private void ReleaseGeneratedResources()
    {
        DestroyGeneratedMaterial(ref baseMaterial);
        DestroyGeneratedMaterial(ref gridMaterial);
        DestroyGeneratedMaterial(ref glowMaterial);
        DestroyGeneratedMaterial(ref coreGlowMaterial);
        DestroyGeneratedTexture(ref gridTexture);
        DestroyGeneratedTexture(ref glowTexture);
        DestroyGeneratedMesh(ref quadMesh);
    }

    private static void DestroyGeneratedMaterial(ref Material material)
    {
        if (material == null)
            return;

        if (Application.isPlaying)
            Destroy(material);
        else
            DestroyImmediate(material);
        material = null;
    }

    private static void DestroyGeneratedTexture(ref Texture2D texture)
    {
        if (texture == null)
            return;

        if (Application.isPlaying)
            Destroy(texture);
        else
            DestroyImmediate(texture);
        texture = null;
    }

    private static void DestroyGeneratedMesh(ref Mesh mesh)
    {
        if (mesh == null)
            return;

        if (Application.isPlaying)
            Destroy(mesh);
        else
            DestroyImmediate(mesh);
        mesh = null;
    }

    private void LogRebuild(string label, int count)
    {
        if (!logRebuilds)
            return;

        Debug.LogFormat(this, "[ProceduralGridBackground] Rebuilt {0}. count={1}", label, count);
    }
}
