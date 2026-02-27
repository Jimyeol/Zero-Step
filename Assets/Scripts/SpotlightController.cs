using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Spotlight 모드: 화면 전체 Fog of War + 스포트라이트(밝은 영역)만 보이게 함.
/// Normal = 밟은 타일 영구 밝힘, Hard = 드래그 중인 위치만 밝음. 스타트 주변은 항상 밝힘.
/// 게임오버 시 실패 지점에서 Radar Pulse(원형 파동) 연출.
/// </summary>
public class SpotlightController : MonoBehaviour
{
    private const int MaxRevealed = 64;

    [Header("참조")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private GameManager gameManager;

    [Header("게임오버 Radar Pulse")]
    [Tooltip("펄스가 퍼져 나가는 최대 반경 (월드 단위)")]
    [SerializeField] private float pulseMaxRadius = 25f;
    [Tooltip("펄스 링 두께 (이 구간만 보였다가 어두워짐, 약 0.2초 노출)")]
    [SerializeField] private float pulseRingWidth = 4f;
    [Tooltip("펄스 한 바퀴 퍼지는 시간(초). 0.02 = 매우 빠르게")]
    [SerializeField] private float pulseDuration = 0.5f;

    private StageConfig config;
    private GameObject fogQuad;
    private Material fogMaterial;
    private MeshRenderer fogRenderer;
    /// <summary>영구 밝힌 영역 (스타트 주변). Hard일 때만 이 영역 + 현재 스포트라이트.</summary>
    private readonly List<Vector2> startRevealPositions = new List<Vector2>();
    /// <summary>Normal 모드: 밟은 타일 월드 위치.</summary>
    private readonly List<Vector2> revealedPositions = new List<Vector2>();
    private readonly Vector4[] revealedPositionBuffer = new Vector4[MaxRevealed];
    private bool isHardMode;
    private float radius;
    private float softness;
    /// <summary>게임오버 펄스 재생 중에는 일반 스포트라이트/밝힌 영역 숨김.</summary>
    private bool isPulsePlaying;
    private Tween pulseTween;
    private float cachedFogOrthoSize = -1f;
    private float cachedFogAspect = -1f;

    /// <summary>
    /// 스테이지 로드 시 GameManager가 호출. config와 스타트 타일 월드 위치 전달.
    /// </summary>
    public void Setup(StageConfig stageConfig, Vector2 startTileWorldPos, float startRevealRadius)
    {
        config = stageConfig;
        if (config == null || string.IsNullOrEmpty(config.mode) || !config.mode.Equals("Spotlight", System.StringComparison.OrdinalIgnoreCase))
            return;

        isHardMode = config.difficulty != null && config.difficulty.Equals("Hard", System.StringComparison.OrdinalIgnoreCase);
        radius = config.spotlightRadius > 0f ? config.spotlightRadius : 2.5f;
        softness = radius * 0.15f;

        startRevealPositions.Clear();
        startRevealPositions.Add(startTileWorldPos);
        revealedPositions.Clear();
        if (isHardMode)
            revealedPositions.AddRange(startRevealPositions);
        else
            revealedPositions.AddRange(startRevealPositions);

        EnsureFogQuad();
        if (fogQuad != null)
            fogQuad.SetActive(true);
    }

    /// <summary>카메라 참조 (스테이지 로드 후 GameManager가 설정).</summary>
    public void SetCamera(Camera cam) { targetCamera = cam; }
    /// <summary>GameManager 참조 (스테이지 로드 후 설정).</summary>
    public void SetGameManager(GameManager gm) { gameManager = gm; }

    /// <summary>
    /// Spotlight 모드가 아닐 때 또는 스테이지 전환 시 포그 비활성화.
    /// </summary>
    public void Disable()
    {
        if (fogQuad != null)
            fogQuad.SetActive(false);
    }

    /// <summary>
    /// 게임오버·리셋 시 호출. 밝혀진 영역을 초기 시작점만 남기고 초기화.
    /// </summary>
    public void ResetRevealedToStartOnly(Vector2 startTileWorldPos)
    {
        if (config == null || !config.mode.Equals("Spotlight", System.StringComparison.OrdinalIgnoreCase))
            return;
        startRevealPositions.Clear();
        startRevealPositions.Add(startTileWorldPos);
        revealedPositions.Clear();
        revealedPositions.AddRange(startRevealPositions);
    }

    /// <summary>
    /// 리셋 시 완전 암흑: 밝힌 영역 전부 제거 (Partial Reset).
    /// </summary>
    public void ClearAllRevealed()
    {
        if (config == null || !config.mode.Equals("Spotlight", System.StringComparison.OrdinalIgnoreCase))
            return;
        startRevealPositions.Clear();
        revealedPositions.Clear();
        StopPulse();
    }

    /// <summary>
    /// 게임오버 시 실패 지점(CurrentPosition)에서 전방향 원형 파동. 파동이 닿는 타일만 잠깐 보였다가 다시 어두워짐.
    /// </summary>
    public void TriggerGameOverPulse(Vector2 failWorldPos, Action onComplete)
    {
        if (fogMaterial == null || config == null || !config.mode.Equals("Spotlight", System.StringComparison.OrdinalIgnoreCase))
        {
            onComplete?.Invoke();
            return;
        }
        if (pulseTween != null && pulseTween.IsActive())
            pulseTween.Kill();
        isPulsePlaying = true;
        fogMaterial.SetVector("_PulseCenter", new Vector4(failWorldPos.x, failWorldPos.y, 1f, 0f));
        fogMaterial.SetFloat("_PulseRadius", 0f);
        fogMaterial.SetFloat("_PulseWidth", pulseRingWidth);

        float fromRadius = 0f;
        pulseTween = DOTween.To(() => fromRadius, x => fromRadius = x, pulseMaxRadius, pulseDuration)
            .SetEase(Ease.Linear)
            .OnUpdate(() =>
            {
                if (fogMaterial != null)
                    fogMaterial.SetFloat("_PulseRadius", fromRadius);
            })
            .OnComplete(() =>
            {
                isPulsePlaying = false;
                StopPulse();
                onComplete?.Invoke();
            });
    }

    /// <summary>
    /// 펄스 비활성화 (리셋 후 등).
    /// </summary>
    public void StopPulse()
    {
        if (pulseTween != null && pulseTween.IsActive())
            pulseTween.Kill();
        pulseTween = null;
        isPulsePlaying = false;
        if (fogMaterial != null)
        {
            fogMaterial.SetVector("_PulseCenter", new Vector4(0f, 0f, 0f, 0f));
            fogMaterial.SetFloat("_PulseRadius", -1f);
        }
    }

    /// <summary>Spotlight 모드가 켜져 있는지.</summary>
    public bool IsSpotlightActive()
    {
        return config != null && config.mode != null && config.mode.Equals("Spotlight", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normal 모드: 타일을 밟았을 때 해당 월드 위치를 영구 밝힘 목록에 추가.
    /// </summary>
    public void AddRevealedPosition(Vector2 worldPos)
    {
        if (isHardMode) return;
        if (revealedPositions.Count >= MaxRevealed) return;
        revealedPositions.Add(worldPos);
    }

    /// <summary>
    /// 손을 뗀 뒤 새 시작점(하트비트)이 되는 타일 위치. Hard 모드에서는 이전에 손 뗐던 곳은 모두 끄고, 방금 손 뗀 곳 하나만 밝힘.
    /// </summary>
    public void AddRevealedPositionForNewStart(Vector2 worldPos)
    {
        if (isHardMode)
        {
            // Hard: 손 뗀 곳이 계속 쌓이지 않도록, 현재 시작점 하나만 남기고 갱신
            revealedPositions.Clear();
            revealedPositions.Add(worldPos);
        }
        else
        {
            if (revealedPositions.Count >= MaxRevealed) return;
            revealedPositions.Add(worldPos);
        }
    }

    private void EnsureFogQuad()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        if (fogQuad == null)
        {
            // 모바일 빌드에서 Shader.Find가 null이 되는 것 방지: Always Included Shaders에 등록 + Resources 폴백
            Shader shader = Shader.Find("Custom/SpotlightFog");
            if (shader == null)
                shader = Resources.Load<Shader>("Shaders/SpotlightFog");
            if (shader == null)
            {
                Debug.LogWarning("[SpotlightController] SpotlightFog 셰이더를 찾을 수 없습니다. Project Settings > Graphics > Always Included Shaders에 추가했는지 확인하세요.");
                return;
            }

            fogQuad = new GameObject("SpotlightFog");
            fogQuad.transform.SetParent(targetCamera.transform, false);
            fogQuad.transform.localPosition = Vector3.forward * 10f;
            fogQuad.transform.localRotation = Quaternion.identity;
            fogQuad.transform.localScale = Vector3.one;

            MeshFilter mf = fogQuad.AddComponent<MeshFilter>();
            Mesh mesh = new Mesh();
            mesh.name = "FogQuad";
            float h = 1f;
            float w = 1f;
            mesh.vertices = new Vector3[]
            {
                new Vector3(-w * 0.5f, -h * 0.5f, 0),
                new Vector3( w * 0.5f, -h * 0.5f, 0),
                new Vector3(-w * 0.5f,  w * 0.5f, 0),
                new Vector3( w * 0.5f,  w * 0.5f, 0)
            };
            mesh.uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new int[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            fogMaterial = new Material(shader);
            fogRenderer = fogQuad.AddComponent<MeshRenderer>();
            fogRenderer.sharedMaterial = fogMaterial;
            fogRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fogRenderer.receiveShadows = false;
            fogRenderer.sortingOrder = 32767;
        }

        UpdateFogQuadScale();
    }

    private void UpdateFogQuadScale()
    {
        if (fogQuad == null || targetCamera == null) return;
        if (!targetCamera.orthographic)
            return;
        if (Mathf.Abs(cachedFogOrthoSize - targetCamera.orthographicSize) < 0.0001f &&
            Mathf.Abs(cachedFogAspect - targetCamera.aspect) < 0.0001f)
            return;

        float ortho = targetCamera.orthographicSize * 2f;
        float aspect = targetCamera.aspect;
        fogQuad.transform.localScale = new Vector3(ortho * aspect, ortho, 1f);
        cachedFogOrthoSize = targetCamera.orthographicSize;
        cachedFogAspect = aspect;
    }

    private void Update()
    {
        if (GameManager.IsPerformanceOverlayOpen)
            return;

        if (fogMaterial == null || config == null || !config.mode.Equals("Spotlight", System.StringComparison.OrdinalIgnoreCase))
            return;

        UpdateFogQuadScale();

        // 펄스 재생 중에는 일반 스포트라이트/밝힌 영역 숨김 (전체 맵 밝히지 않음)
        if (isPulsePlaying)
        {
            fogMaterial.SetVector("_Center", new Vector4(0f, 0f, 0f, 0f));
            fogMaterial.SetInt("_RevealedCount", 0);
            return;
        }

        if (gameManager == null) return;
        Vector2 pointerWorld = gameManager.GetPointerWorldPosition();
        bool dragging = gameManager.IsDragging;

        fogMaterial.SetFloat("_Radius", radius);
        fogMaterial.SetFloat("_Softness", softness);

        if (dragging)
        {
            fogMaterial.SetVector("_Center", new Vector4(pointerWorld.x, pointerWorld.y, 1f, 0f));
            // Hard: 드래그 중에는 손가락만 따라가게, 마지막으로 밟았던 타일 밝기는 끔
            if (isHardMode)
            {
                fogMaterial.SetInt("_RevealedCount", 0);
            }
            else
            {
                ApplyRevealedPositionsToMaterial();
            }
        }
        else
        {
            fogMaterial.SetVector("_Center", new Vector4(0f, 0f, 0f, 0f));
            ApplyRevealedPositionsToMaterial();
        }
    }

    private void ApplyRevealedPositionsToMaterial()
    {
        if (fogMaterial == null)
            return;

        int count = Mathf.Min(revealedPositions.Count, MaxRevealed);
        fogMaterial.SetInt("_RevealedCount", count);
        for (int i = 0; i < count; i++)
            revealedPositionBuffer[i] = new Vector4(revealedPositions[i].x, revealedPositions[i].y, 0f, 0f);
        fogMaterial.SetVectorArray("_RevealedPositions", revealedPositionBuffer);
    }

    private void OnDestroy()
    {
        if (pulseTween != null && pulseTween.IsActive())
            pulseTween.Kill();
        if (fogMaterial != null)
            Destroy(fogMaterial);
    }
}
