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

    [Header("Shaders")]
    [Tooltip("Optional override. Leave empty to use the built-in Sprites/Default shader, which is compatible with the 2D renderer.")]
    [SerializeField] private Shader baseShader;
    [Tooltip("Optional override. Leave empty to use the built-in Sprites/Default shader, which supports texture offset animation.")]
    [SerializeField] private Shader gridShader;
    [Tooltip("Optional override. Leave empty to use the first supported additive/particle shader.")]
    [SerializeField] private Shader glowShader;

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
    [Tooltip("When enabled, the visible grid density stays tied to a reference camera size instead of growing with larger stages.")]
    [SerializeField] private bool lockGridDensityToReferenceCameraSize = true;
    [Tooltip("Fallback reference orthographic size. 0 means use the current camera size until GameManager supplies the 2x2 reference.")]
    [SerializeField] [Min(0f)] private float referenceOrthographicSize;

    [Header("Grid Motion")]
    [SerializeField] private bool gridMotionEnabled = true;
    [SerializeField] [Min(0f)] private float gridScrollSpeed = 0.035f;

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
    private const int DefaultGridTextureSize = 128;
    private const int DefaultGridCellPixels = 16;
    private const int DefaultGridLinePixels = 1;
    private const float DefaultGridWorldCellSize = 1.1f;
    private const float DefaultGridScrollSpeed = 0.035f;
    private const int DefaultGlowTextureSize = 256;
    private const float DefaultGlowIntensity = 0.135f;
    private const float DefaultGlowWidthScale = 1.45f;
    private const float DefaultGlowHeightScale = 0.72f;
    private const float DefaultCoreGlowWidthScale = 0.55f;
    private const float DefaultCoreGlowHeightScale = 0.32f;
    private const float DefaultAspect = 9f / 16f;
    private static readonly Vector2[] GridFlowDirections =
    {
        Vector2.right,
        Vector2.left,
        new Vector2(1f, 1f).normalized,
        new Vector2(-1f, 1f).normalized
    };

    private Transform basePlane;
    private Transform gridPlane;
    private Transform glowPlane;
    private Transform coreGlowPlane;

    private Mesh quadMesh;
    private Mesh gridMesh;
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
    private readonly System.Random gridFlowRandom = new System.Random();
    private readonly Vector2[] gridUvBuffer = new Vector2[4];
    private Vector2 gridScrollDirection = Vector2.right;
    private Vector2 gridScrollOffset;
    private Vector2 currentGridTiling = Vector2.one;
    private int lastGridFlowDirectionIndex = -1;
    private float runtimeReferenceOrthographicSize = -1f;
    private bool runtimeCorrectionLogged;

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
        AdvanceGridMotion();
    }

    private void OnDestroy()
    {
        ReleaseGeneratedResources();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SanitizeSettings(false);
    }
#endif

    public void RandomizeGridFlowDirection(int stageIndex)
    {
        int directionIndex = gridFlowRandom.Next(GridFlowDirections.Length);
        if (GridFlowDirections.Length > 1 && directionIndex == lastGridFlowDirectionIndex)
            directionIndex = (directionIndex + 1 + Mathf.Abs(stageIndex % (GridFlowDirections.Length - 1))) % GridFlowDirections.Length;

        lastGridFlowDirectionIndex = directionIndex;
        SetGridFlowDirection(GridFlowDirections[directionIndex]);
    }

    public void SetGridFlowDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= MetricEpsilon)
            direction = Vector2.right;

        gridScrollDirection = direction.normalized;
    }

    public void SetGridReferenceOrthographicSize(float orthographicSize)
    {
        float sanitizedSize = Mathf.Max(0f, orthographicSize);
        if (Mathf.Abs(runtimeReferenceOrthographicSize - sanitizedSize) <= MetricEpsilon)
            return;

        runtimeReferenceOrthographicSize = sanitizedSize;
        EnsureSetup();
        ForceRefit();
    }

    private void EnsureSetup()
    {
        SanitizeSettings(true);

        if (targetCamera == null)
            targetCamera = Camera.main;

        EnsureQuadMesh();
        EnsureGeneratedTextures();
        EnsureMaterials();
        EnsurePlanes();
    }

    private void EnsureQuadMesh()
    {
        if (quadMesh == null)
            quadMesh = CreateQuadMesh("ProceduralBackgroundQuad");
        if (gridMesh == null)
        {
            gridMesh = CreateQuadMesh("ProceduralBackgroundGridQuad");
            gridMesh.MarkDynamic();
        }
    }

    private static Mesh CreateQuadMesh(string meshName)
    {
        Mesh mesh = new Mesh
        {
            name = meshName
        };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateBounds();
        mesh.hideFlags = HideFlags.DontSave;
        return mesh;
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
        Shader shader = ResolveShader(baseShader, "Sprites/Default", "Universal Render Pipeline/Unlit", "Unlit/Color");
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
        Shader shader = ResolveShader(gridShader, "Sprites/Default", "Universal Render Pipeline/Unlit", "Unlit/Transparent");
        Material material = new Material(shader)
        {
            name = "GeneratedBackgroundGridMaterial",
            hideFlags = HideFlags.DontSave
        };
        ConfigureTransparentMaterial(material);
        SetMaterialColor(material, Color.white);
        SetMaterialTexture(material, gridTexture);
        ResetGridMaterialUvTransform(material);
        material.renderQueue = (int)RenderQueue.Transparent;
        MaterialRebuildCount++;
        LogRebuild("grid material", MaterialRebuildCount);
        return material;
    }

    private Material CreateGlowMaterial(string materialName)
    {
        Shader shader = ResolveShader(glowShader, "Legacy Shaders/Particles/Additive", "Particles/Standard Unlit", "Sprites/Default");
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

    private Shader ResolveShader(Shader preferredShader, params string[] shaderNames)
    {
        if (preferredShader != null && preferredShader.isSupported)
            return preferredShader;

        if (preferredShader != null)
            Debug.LogWarning($"[ProceduralGridBackground] Assigned shader is not supported on this platform: {preferredShader.name}", this);

        return FindFirstShader(shaderNames);
    }

    private static Shader FindFirstShader(params string[] shaderNames)
    {
        for (int i = 0; i < shaderNames.Length; i++)
        {
            Shader shader = Shader.Find(shaderNames[i]);
            if (shader != null && shader.isSupported)
                return shader;
        }

        Shader fallback = Shader.Find("Sprites/Default");
        if (fallback != null && fallback.isSupported)
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

    private static void SetMaterialTextureOffset(Material material, Vector2 offset)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTextureOffset("_BaseMap", offset);
        if (material.HasProperty("_MainTex"))
            material.SetTextureOffset("_MainTex", offset);
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
        meshFilter.sharedMesh = planeName == GridPlaneName ? gridMesh : quadMesh;

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

        float aspect = CalculateCameraAspect(targetCamera);
        bool metricsChanged =
            Mathf.Abs(cachedOrthoSize - targetCamera.orthographicSize) > MetricEpsilon ||
            Mathf.Abs(cachedAspect - aspect) > MetricEpsilon ||
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

        float aspect = CalculateCameraAspect(targetCamera);
        float height = targetCamera.orthographicSize * 2f;
        float width = height * aspect;
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

        float gridTilingHeight = height;
        float gridTilingWidth = width;
        float referenceSize = GetGridReferenceOrthographicSize();
        if (referenceSize > MetricEpsilon)
        {
            gridTilingHeight = referenceSize * 2f;
            gridTilingWidth = gridTilingHeight * aspect;
        }

        float cellsPerTexture = GetGridCellsPerTexture();
        Vector2 tiling = new Vector2(
            Mathf.Max(0.001f, gridTilingWidth / Mathf.Max(0.05f, gridWorldCellSize) / cellsPerTexture),
            Mathf.Max(0.001f, gridTilingHeight / Mathf.Max(0.05f, gridWorldCellSize) / cellsPerTexture));
        currentGridTiling = tiling;
        ResetGridMaterialUvTransform(gridMaterial);
        ApplyGridMeshUvs();

        cachedOrthoSize = targetCamera.orthographicSize;
        cachedAspect = aspect;
        cachedScreenWidth = Screen.width;
        cachedScreenHeight = Screen.height;
        cachedCameraPosition = cameraPosition;
    }

    public static float CalculateCameraAspect(Camera camera)
    {
        if (camera != null)
        {
            Rect pixelRect = camera.pixelRect;
            if (pixelRect.width > MetricEpsilon && pixelRect.height > MetricEpsilon)
                return pixelRect.width / pixelRect.height;
            if (camera.aspect > MetricEpsilon)
                return camera.aspect;
        }

        if (Screen.width > 0 && Screen.height > 0)
            return (float)Screen.width / Screen.height;

        return DefaultAspect;
    }

    private void AdvanceGridMotion()
    {
        if (!gridMotionEnabled || gridMaterial == null || gridScrollSpeed <= 0f)
            return;

        if (gridScrollDirection.sqrMagnitude <= MetricEpsilon)
            gridScrollDirection = Vector2.right;

        gridScrollOffset += gridScrollDirection.normalized * gridScrollSpeed * Time.deltaTime;
        gridScrollOffset.x = Mathf.Repeat(gridScrollOffset.x, 1f);
        gridScrollOffset.y = Mathf.Repeat(gridScrollOffset.y, 1f);
        ApplyGridMeshUvs();
    }

    private void ApplyGridMeshUvs()
    {
        if (gridMesh == null)
            return;

        Vector2 tiling = new Vector2(
            Mathf.Max(0.001f, currentGridTiling.x),
            Mathf.Max(0.001f, currentGridTiling.y));
        Vector2 offset = gridScrollOffset;
        gridUvBuffer[0] = offset;
        gridUvBuffer[1] = offset + new Vector2(tiling.x, 0f);
        gridUvBuffer[2] = offset + new Vector2(0f, tiling.y);
        gridUvBuffer[3] = offset + tiling;
        gridMesh.uv = gridUvBuffer;
    }

    private static void ResetGridMaterialUvTransform(Material material)
    {
        SetMaterialTextureScale(material, Vector2.one);
        SetMaterialTextureOffset(material, Vector2.zero);
    }

    private float GetGridCellsPerTexture()
    {
        int cell = Mathf.Clamp(gridCellPixels, 4, Mathf.Max(16, gridTextureSize));
        int size = SnapTextureSizeToWholeCells(gridTextureSize, cell);
        return Mathf.Max(1f, size / (float)cell);
    }

    private float GetGridReferenceOrthographicSize()
    {
        if (runtimeReferenceOrthographicSize > MetricEpsilon)
            return runtimeReferenceOrthographicSize;
        if (!lockGridDensityToReferenceCameraSize)
            return 0f;
        if (referenceOrthographicSize > MetricEpsilon)
            return referenceOrthographicSize;

        return 0f;
    }

    private void SanitizeSettings(bool restoreMotionDefaults)
    {
        bool corrected = false;

        if (gridTextureSize < 16)
        {
            gridTextureSize = DefaultGridTextureSize;
            corrected = true;
        }

        gridCellPixels = Mathf.Clamp(gridCellPixels <= 0 ? DefaultGridCellPixels : gridCellPixels, 4, gridTextureSize);
        int snappedTextureSize = SnapTextureSizeToWholeCells(gridTextureSize, gridCellPixels);
        if (snappedTextureSize != gridTextureSize)
        {
            gridTextureSize = snappedTextureSize;
            corrected = true;
        }

        int sanitizedLinePixels = Mathf.Clamp(gridLinePixels <= 0 ? DefaultGridLinePixels : gridLinePixels, 1, gridCellPixels);
        if (sanitizedLinePixels != gridLinePixels)
        {
            gridLinePixels = sanitizedLinePixels;
            corrected = true;
        }

        if (gridWorldCellSize <= 0.05f)
        {
            gridWorldCellSize = DefaultGridWorldCellSize;
            corrected = true;
        }

        if (restoreMotionDefaults && !gridMotionEnabled)
        {
            gridMotionEnabled = true;
            corrected = true;
        }

        if (gridScrollSpeed <= MetricEpsilon)
        {
            gridScrollSpeed = DefaultGridScrollSpeed;
            corrected = true;
        }

        if (referenceOrthographicSize < 0f)
        {
            referenceOrthographicSize = 0f;
            corrected = true;
        }

        if (glowTextureSize < 32)
        {
            glowTextureSize = DefaultGlowTextureSize;
            corrected = true;
        }

        if (glowIntensity < 0f)
        {
            glowIntensity = DefaultGlowIntensity;
            corrected = true;
        }

        if (glowWidthScale <= 0.1f)
        {
            glowWidthScale = DefaultGlowWidthScale;
            corrected = true;
        }

        if (glowHeightScale <= 0.1f)
        {
            glowHeightScale = DefaultGlowHeightScale;
            corrected = true;
        }

        if (coreGlowWidthScale <= 0.1f)
        {
            coreGlowWidthScale = DefaultCoreGlowWidthScale;
            corrected = true;
        }

        if (coreGlowHeightScale <= 0.1f)
        {
            coreGlowHeightScale = DefaultCoreGlowHeightScale;
            corrected = true;
        }

        if (gridScrollDirection.sqrMagnitude <= MetricEpsilon)
        {
            gridScrollDirection = Vector2.right;
            corrected = true;
        }

        if (corrected && !runtimeCorrectionLogged && Application.isPlaying)
        {
            runtimeCorrectionLogged = true;
            Debug.LogWarning("[ProceduralGridBackground] Corrected invalid serialized background settings at runtime.", this);
        }
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
        DestroyGeneratedMesh(ref gridMesh);
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
