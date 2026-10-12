using System.Collections.Generic;
using UnityEngine;

/// <summary>Cosmetic feedback only. Never owns tile state, colliders, or game time.</summary>
[DefaultExecutionOrder(100)]
public sealed class GameplayFeedback : MonoBehaviour
{
    public enum EffectLevel { Full, Reduced, Minimal }
    private const string PreferenceKey = "ZeroStep.EffectLevel";
    private const int PoolSize = 64;
    private const int RingSegments = 32;
    private static int cachedLevel = -1;
    public static GameplayFeedback Instance { get; private set; }
    public static EffectLevel Level
    {
        get
        {
            if (cachedLevel < 0)
            {
                // 직접 고른 적이 없으면 OS의 "동작 줄이기" 설정을 따라 기본값을 Reduced로 둔다(광과민·멀미 배려).
                int defaultLevel = IsSystemReduceMotionEnabled() ? (int)EffectLevel.Reduced : (int)EffectLevel.Full;
                cachedLevel = Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, defaultLevel), 0, 2);
            }
            return (EffectLevel)cachedLevel;
        }
        set
        {
            cachedLevel = Mathf.Clamp((int)value, 0, 2);
            PlayerPrefs.SetInt(PreferenceKey, cachedLevel);
            PlayerPrefs.Save();
            if (Instance != null)
                Instance.ResetFeedback();
        }
    }
#if UNITY_IOS && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern bool ZeroStepIsReduceMotionEnabled();
#endif

    private static bool IsSystemReduceMotionEnabled()
    {
        try
        {
#if UNITY_IOS && !UNITY_EDITOR
            return ZeroStepIsReduceMotionEnabled();
#elif UNITY_ANDROID && !UNITY_EDITOR
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
            using (var globalSettings = new AndroidJavaClass("android.provider.Settings$Global"))
            {
                float scale = globalSettings.CallStatic<float>("getFloat", resolver, "animator_duration_scale", 1f);
                return scale <= 0.0001f;
            }
#else
            return false;
#endif
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    public static float Intensity => Level == EffectLevel.Full ? 1f : Level == EffectLevel.Reduced ? 0.45f : 0f;
    public float Energy { get; private set; }
    public float TrailWidthMultiplier => 1f + Energy * 0.32f * Intensity;
    public float TrailIntensityMultiplier => 1f + Energy * 0.55f * Intensity;
    public int ActiveEffectCount { get; private set; }
    public int EffectCapacity => PoolSize;

    private enum Shape { Ring, Beam, Spark }
    private sealed class Effect
    {
        public LineRenderer line;
        public Shape shape;
        public Vector3 from, to;
        public Color color;
        public float age, duration, delay, radius, width;
        public bool active;
    }

    private readonly Effect[] pool = new Effect[PoolSize];
    private static readonly Color Cyan = new Color(0.15f, 1.35f, 1.5f, 0.85f);
    private static readonly Color Gold = new Color(1.65f, 0.92f, 0.25f, 0.9f);
    private static readonly Color Coral = new Color(1.6f, 0.3f, 0.48f, 0.85f);
    private Material material;
    private Camera targetCamera;
    private GameMainUIController ui;
    private ProceduralGridBackground background;
    private System.Action<float> haptic;
    private float tileSize = 1f;
    private int cursor, moveCount;
    private bool dragging, nearCompletion;
    private Vector3 moveDirection = Vector3.up;
    private float lastMoveTime, lastDeniedTime = -1f, lastHapticTime = -1f;
    private float cameraTime, cameraDuration, cameraStrength;
    private Vector3 cameraOffset;
    private float cameraSizeOffset;
    private bool suspended;

    // 지나온 경로선(타일 중심을 잇는 선)과 현재 위치(머리 타일) 표시. 정보 전달용이라 Minimal에서도 유지한다.
    private const int PathCapacity = 96;
    private const float PathFadeSeconds = 0.3f;
    private static readonly Color PathColor = new Color(0.75f, 1.25f, 1.45f, 0.5f);
    private LineRenderer pathLine;
    private LineRenderer headRing;
    private readonly Vector3[] pathPoints = new Vector3[PathCapacity];
    private int pathPointCount;
    private float pathFadeStartTime = -1f;
    private Vector3 headPosition;
    private bool headVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        cachedLevel = -1;
    }

    private void Awake()
    {
        Instance = this;
        // 점토 테마는 밝은 종이 배경이라 더하기(발광) 블렌딩 대신 일반 알파 블렌딩을 쓴다.
        Shader shader = VisualTheme.IsSoftClay ? Shader.Find("Sprites/Default") : Resources.Load<Shader>("Shaders/GameplayFeedback");
        if (shader == null)
        {
            Debug.LogWarning("[GameplayFeedback] Feedback shader missing; cosmetic effects disabled.");
            return;
        }
        material = new Material(shader) { name = "GameplayFeedback (shared)" };
        for (int i = 0; i < pool.Length; i++)
        {
            var go = new GameObject("Feedback " + i);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.sortingOrder = 12;
            line.numCapVertices = 2;
            line.numCornerVertices = 1;
            line.enabled = false;
            pool[i] = new Effect { line = line };
        }

        pathLine = CreatePersistentLine("Feedback Path", 10, false);
        headRing = CreatePersistentLine("Feedback Head", 11, true);
        headRing.positionCount = RingSegments;
    }

    private LineRenderer CreatePersistentLine(string objectName, int sortingOrder, bool loop)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.sortingOrder = sortingOrder;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.loop = loop;
        line.enabled = false;
        return line;
    }

    /// <summary>현재 드래그에서 실제로 받아들여진 경로(타일 중심)를 그린다.</summary>
    public void UpdatePath(IReadOnlyList<Tile> path)
    {
        if (pathLine == null || path == null)
            return;

        pathPointCount = 0;
        for (int i = 0; i < path.Count && pathPointCount < PathCapacity; i++)
        {
            if (path[i] == null) continue;
            Vector3 p = path[i].transform.position;
            p.z -= 0.02f;
            pathPoints[pathPointCount++] = p;
        }

        pathFadeStartTime = -1f;
        headVisible = pathPointCount > 0 && dragging;
        if (pathPointCount > 0)
            headPosition = pathPoints[pathPointCount - 1];
        ApplyPathLine(1f);
    }

    /// <summary>손을 떼면 경로선을 짧게 남겼다가 사라지게 한다.</summary>
    public void ReleasePath()
    {
        headVisible = false;
        if (headRing != null)
            headRing.enabled = false;
        if (pathPointCount >= 2)
            pathFadeStartTime = Time.unscaledTime;
        else
            ClearPath();
    }

    public void ClearPath()
    {
        pathPointCount = 0;
        pathFadeStartTime = -1f;
        headVisible = false;
        if (pathLine != null)
            pathLine.enabled = false;
        if (headRing != null)
            headRing.enabled = false;
    }

    private void ApplyPathLine(float alphaScale)
    {
        if (pathLine == null)
            return;

        bool show = pathPointCount >= 2 && alphaScale > 0f && !suspended;
        pathLine.enabled = show;
        if (!show)
            return;

        pathLine.positionCount = pathPointCount;
        for (int i = 0; i < pathPointCount; i++)
            pathLine.SetPosition(i, pathPoints[i]);
        Color tint = VisualTheme.IsSoftClay ? WithAlphaOf(VisualTheme.PathInk, 0.82f) : PathColor;
        tint.a *= alphaScale;
        pathLine.startColor = tint;
        pathLine.endColor = tint;
        pathLine.widthMultiplier = tileSize * (VisualTheme.IsSoftClay ? 0.085f : 0.07f);
    }

    private void UpdatePathVisuals()
    {
        if (pathFadeStartTime >= 0f)
        {
            float t = (Time.unscaledTime - pathFadeStartTime) / PathFadeSeconds;
            if (t >= 1f)
                ClearPath();
            else
                ApplyPathLine(1f - t);
        }

        if (headRing == null)
            return;

        bool showHead = headVisible && dragging && !suspended;
        headRing.enabled = showHead;
        if (!showHead)
            return;

        float pulse = 1f + 0.06f * Intensity * Mathf.Sin(Time.unscaledTime * 9f);
        float radius = tileSize * 0.56f * pulse;
        for (int j = 0; j < RingSegments; j++)
        {
            float angle = j * Mathf.PI * 2f / RingSegments;
            headRing.SetPosition(j, headPosition + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
        }
        Color tint = VisualTheme.IsSoftClay ? WithAlphaOf(VisualTheme.PathInk, 0.7f) : new Color(0.9f, 1.3f, 1.5f, 0.75f);
        headRing.startColor = tint;
        headRing.endColor = tint;
        headRing.widthMultiplier = tileSize * 0.05f;
    }

    public void Configure(Camera camera, GameMainUIController mainUI, ProceduralGridBackground grid, System.Action<float> vibration)
    {
        targetCamera = camera;
        ui = mainUI;
        background = grid;
        haptic = vibration;
    }

    public void BeginStage(float size)
    {
        ResetFeedback();
        tileSize = Mathf.Max(0.01f, size);
        nearCompletion = false;
        suspended = false;
    }

    public void SetSuspended(bool value)
    {
        if (suspended == value) return;
        suspended = value;
        if (value) ResetFeedback();
    }

    public void DragStarted(Tile tile)
    {
        if (tile == null) return;
        dragging = true;
        moveCount = 0;
        lastMoveTime = Time.unscaledTime;
        Ring(tile.transform.position, Cyan, 0.64f, 0.2f);
    }

    public void BeginMove(Tile from, Tile to)
    {
        if (from != null && to != null)
            moveDirection = (to.transform.position - from.transform.position).normalized;
    }

    public void StepAccepted(Tile from, Tile to)
    {
        if (from == null || to == null) return;
        moveCount++;
        Energy = Mathf.Clamp01(Energy + 0.13f);
        lastMoveTime = Time.unscaledTime;
        Beam(from.transform.position, to.transform.position, Cyan, 0.16f);
        Ring(to.transform.position, Cyan, 0.56f, 0.16f);
        // 이동마다 가벼운 틱 햅틱(최소 간격 45ms). 4·8·12수 단위 "콤보" 펄스는 실제 규칙이 없어 제거했다.
        Vibrate(0.12f, 0.045f);
    }

    public void DragReleased(Tile tile)
    {
        if (dragging && moveCount > 0 && tile != null)
            Ring(tile.transform.position, Cyan, 0.7f, 0.26f);
        dragging = false;
    }

    public void TileDecreased(Tile tile, int previousCount)
    {
        if (tile == null || previousCount <= tile.CurrentNumber) return;
        if (tile.CurrentNumber == 0)
        {
            Burst(tile.transform.position, Cyan, Level == EffectLevel.Full ? 4 : 2, 0.8f, moveDirection);
            Ring(tile.transform.position, Cyan, 0.65f, 0.22f);
        }
        else if (tile.GetComponent<BlindCurtainTile>() != null)
        {
            // An edge pulse acknowledges consumption without revealing the hidden number.
            Ring(tile.transform.position, new Color(0.7f, 0.65f, 1.3f, 0.65f), 0.55f, 0.16f);
        }
    }

    public void Progress(int remaining, int initial, Tile reference)
    {
        if (nearCompletion || remaining <= 0 || initial < 5 || remaining > initial * 0.2f) return;
        nearCompletion = true;
        ui?.PlayGameplayFeedbackPulse(0.8f, true);
        if (reference != null) Ring(reference.transform.position, Gold, 0.85f, 0.32f);
    }

    public void MoveDenied(Tile tile)
    {
        if (tile == null || Time.unscaledTime - lastDeniedTime < 0.3f) return;
        lastDeniedTime = Time.unscaledTime;
        Ring(tile.transform.position, Coral, 0.58f, 0.16f);
        Vibrate(0.4f, 0.1f);
    }

    public void CrossBlast(Tile source, Tile[] targets, int count)
    {
        if (source == null) return;
        // 빔은 CrossBlastTile이 이미 그리므로 여기서는 폭발 링·불꽃만 같은 색 계열로 더한다(이중 빔 제거).
        Ring(source.transform.position, Cyan, 0.95f, 0.28f);
        Burst(source.transform.position, Cyan, Level == EffectLevel.Full ? 6 : 3, 1.05f);
        for (int i = 0; targets != null && i < Mathf.Min(count, targets.Length); i++)
            if (targets[i] != null)
                Ring(targets[i].transform.position, Cyan, 0.5f, 0.18f);
        Vibrate(0.5f);
    }

    public void TwinLink(Tile source, Tile target)
    {
        if (source == null || target == null) return;
        // 짝 타일 숫자는 즉시 줄어드므로 빔이 거의 즉시(약 0.05초) 닿게 해 원인과 결과가 어긋나 보이지 않게 한다.
        Beam(source.transform.position, target.transform.position, Cyan, 0.12f);
        Ring(target.transform.position, Cyan, 0.7f, 0.2f, 0.045f);
    }

    private static readonly Color ChargeGreen = new Color(0.45f, 1.45f, 0.7f, 0.85f);

    public void Charged(Tile source, Tile target)
    {
        if (source == null || target == null) return;
        Beam(source.transform.position, target.transform.position, ChargeGreen, 0.12f);
        Ring(target.transform.position, ChargeGreen, 0.62f, 0.2f, 0.04f);
    }

    public void RevealLink(Tile source, Tile target, float delay)
    {
        if (source == null || target == null) return;
        HiddenTile hidden = target.GetComponent<HiddenTile>();
        if (hidden != null && !hidden.IsColliderEnabled) return;
        Beam(source.transform.position, target.transform.position, Gold, 0.2f, Mathf.Max(0f, delay));
    }

    public void HiddenRevealed(Tile tile)
    {
        if (tile != null) Ring(tile.transform.position, Gold, 0.8f, 0.28f);
    }

    public void Blackout(Tile source)
    {
        if (source != null) Ring(source.transform.position, new Color(0.65f, 0.55f, 1.4f, 0.7f), 1.1f, 0.32f);
        if (Intensity > 0f && !suspended) background?.PulseFeedbackBlackout();
    }

    public void Cleared(Tile tile)
    {
        dragging = false;
        Vector3 center = tile != null ? tile.transform.position : Vector3.zero;
        Ring(center, Gold, 1.65f, 0.38f);
        Ring(center, Cyan, 1.25f, 0.34f, 0.06f);
        Burst(center, Gold, Level == EffectLevel.Full ? 12 : 4, 1.8f);
        StartCameraImpulse(0.26f, 0.035f);
        Energy = 1f;
        lastMoveTime = Time.unscaledTime;
    }

    public void Failed(Tile tile)
    {
        dragging = false;
        if (tile != null) Ring(tile.transform.position, Coral, 0.9f, 0.24f);
        StartCameraImpulse(0.18f, 0.022f);
        Vibrate(0.65f, 0f);
    }

    private void Vibrate(float strength, float minInterval = 0.14f)
    {
        // 햅틱은 설정의 진동 토글로 끄며, 효과 강도(시각 연출)와는 별개로 둔다.
        if (suspended || Time.unscaledTime - lastHapticTime < minInterval) return;
        lastHapticTime = Time.unscaledTime;
        haptic?.Invoke(strength);
    }

    private static Color WithAlphaOf(Color c, float a)
    {
        c.a = a;
        return c;
    }

    /// <summary>
    /// 점토 테마: 네온(HDR) 효과 색을 종이 위에서 보이는 잉크 계열로 바꾼다.
    /// 따뜻한 색(금색·산호)은 테라코타, 나머지(시안·보라·초록)는 부드러운 잉크색.
    /// </summary>
    private static Color ClayEffectColor(Color neon)
    {
        Color.RGBToHSV(new Color(Mathf.Clamp01(neon.r / 1.7f), Mathf.Clamp01(neon.g / 1.7f), Mathf.Clamp01(neon.b / 1.7f)), out float h, out _, out _);
        bool warm = h < 0.13f || h > 0.9f;
        Color c = warm ? VisualTheme.Accent : VisualTheme.InkSoft;
        c.a = Mathf.Clamp(neon.a, 0f, 0.8f);
        return c;
    }

    private Effect Rent(Shape shape, Vector3 from, Vector3 to, Color color, float duration, float delay = 0f)
    {
        if (Intensity <= 0f || suspended || material == null) return null;
        Effect effect = pool[cursor];
        cursor = (cursor + 1) % PoolSize;
        effect.shape = shape;
        effect.from = from;
        effect.to = to;
        effect.color = VisualTheme.IsSoftClay ? ClayEffectColor(color) : color;
        effect.color.a *= Intensity;
        effect.age = 0f;
        effect.duration = duration;
        effect.delay = delay;
        effect.active = true;
        effect.line.loop = shape == Shape.Ring;
        effect.line.positionCount = shape == Shape.Ring ? RingSegments : 2;
        effect.line.enabled = false;
        return effect;
    }

    private void Ring(Vector3 center, Color color, float radius, float duration, float delay = 0f)
    {
        Effect effect = Rent(Shape.Ring, center, center, color, duration, delay);
        if (effect == null) return;
        effect.radius = radius * tileSize;
        effect.width = tileSize * 0.045f;
    }

    private void Beam(Vector3 from, Vector3 to, Color color, float duration, float delay = 0f)
    {
        Effect effect = Rent(Shape.Beam, from, to, color, duration, delay);
        if (effect != null) effect.width = tileSize * 0.075f;
    }

    private void Burst(Vector3 center, Color color, int count, float distance, Vector3 direction = default)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count + 0.3f;
            Vector3 velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) + direction * 0.45f;
            Effect effect = Rent(Shape.Spark, center, center + velocity * distance * tileSize, color, 0.26f + (i % 3) * 0.025f);
            if (effect != null) effect.width = tileSize * 0.045f;
        }
    }

    private void LateUpdate()
    {
        UpdatePathVisuals();
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        if (Time.unscaledTime - lastMoveTime > 0.22f || !dragging)
            Energy = Mathf.MoveTowards(Energy, 0f, dt * 1.6f);
        background?.SetFeedbackEnergy(suspended ? 0f : Energy * Intensity);
        ActiveEffectCount = 0;
        float bottom = float.NegativeInfinity, top = float.PositiveInfinity;
        if (targetCamera != null && ui != null && ui.HasResolvedGameplayLayout)
        {
            bottom = targetCamera.ViewportToWorldPoint(new Vector3(0.5f, ui.BottomGameplayMarginNormalized, 0f)).y + tileSize * 0.06f;
            top = targetCamera.ViewportToWorldPoint(new Vector3(0.5f, 1f - ui.TopGameplayMarginNormalized, 0f)).y - tileSize * 0.06f;
        }
        for (int i = 0; i < pool.Length; i++)
        {
            Effect effect = pool[i];
            if (effect == null || !effect.active) continue;
            effect.age += dt;
            float t = (effect.age - effect.delay) / effect.duration;
            if (t >= 1f || Intensity <= 0f)
            {
                effect.active = false;
                effect.line.enabled = false;
                continue;
            }
            ActiveEffectCount++;
            if (t < 0f) continue;
            effect.line.enabled = true;
            float ease = 1f - (1f - t) * (1f - t);
            Color tint = effect.color;
            tint.a *= (1f - t) * (1f - t);
            effect.line.startColor = tint;
            effect.line.endColor = tint;
            effect.line.widthMultiplier = effect.width * (1f - t * 0.65f);
            if (effect.shape == Shape.Ring)
            {
                float radius = Mathf.Lerp(effect.radius * 0.55f, effect.radius, ease);
                // 링을 게임 영역에 맞춰 점마다 잘라내면 D자 모양이 되므로, 반지름 자체를 영역 안으로 줄여 원 모양을 유지한다.
                float room = Mathf.Min(effect.from.y - bottom, top - effect.from.y);
                if (room > 0f && radius > room)
                {
                    float overflow = Mathf.Clamp01((radius - room) / Mathf.Max(0.0001f, radius));
                    radius = room;
                    tint.a *= 1f - overflow;
                    effect.line.startColor = tint;
                    effect.line.endColor = tint;
                }
                for (int j = 0; j < RingSegments; j++)
                {
                    float angle = j * Mathf.PI * 2f / RingSegments;
                    Vector3 p = effect.from + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                    effect.line.SetPosition(j, p);
                }
            }
            else
            {
                float head = effect.shape == Shape.Beam ? Mathf.Clamp01(t * 2.5f) : ease;
                float tail = effect.shape == Shape.Beam ? Mathf.Clamp01((t - 0.18f) * 1.6f) : Mathf.Max(0f, ease - 0.16f);
                Vector3 start = Vector3.Lerp(effect.from, effect.to, tail);
                Vector3 end = Vector3.Lerp(effect.from, effect.to, head);
                start.y = Mathf.Clamp(start.y, bottom, top);
                end.y = Mathf.Clamp(end.y, bottom, top);
                effect.line.SetPosition(0, start);
                effect.line.SetPosition(1, end);
            }
        }
        UpdateCameraImpulse(dt);
    }

    private void StartCameraImpulse(float duration, float strength)
    {
        RestoreCameraPose();
        if (Level != EffectLevel.Full || suspended) return;
        cameraDuration = duration;
        cameraTime = duration;
        cameraStrength = strength * tileSize;
    }

    // Remove only our additive pose, so responsive camera fitting remains authoritative.
    public void RestoreCameraPose()
    {
        RemoveCameraOffset();
        cameraTime = 0f;
    }

    private void RemoveCameraOffset()
    {
        if (targetCamera != null)
        {
            targetCamera.transform.position -= cameraOffset;
            targetCamera.orthographicSize -= cameraSizeOffset;
        }
        cameraOffset = Vector3.zero;
        cameraSizeOffset = 0f;
    }

    private void UpdateCameraImpulse(float dt)
    {
        RemoveCameraOffset();
        if (targetCamera == null || cameraTime <= 0f || suspended) return;
        cameraTime = Mathf.Max(0f, cameraTime - dt);
        float t = 1f - cameraTime / cameraDuration;
        float envelope = (1f - t) * (1f - t);
        cameraOffset = new Vector3(Mathf.Sin(t * 37f), Mathf.Sin(t * 51f) * 0.55f, 0f) * cameraStrength * envelope;
        cameraSizeOffset = targetCamera.orthographicSize * 0.009f * Mathf.Sin(t * Mathf.PI);
        targetCamera.transform.position += cameraOffset;
        targetCamera.orthographicSize += cameraSizeOffset;
    }

    public void ResetFeedback()
    {
        ClearPath();
        RestoreCameraPose();
        Energy = 0f;
        dragging = false;
        moveCount = 0;
        ActiveEffectCount = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] == null) continue;
            pool[i].active = false;
            if (pool[i].line != null)
                pool[i].line.enabled = false;
        }
        background?.ResetFeedbackMotion();
    }

    private void OnDisable() => ResetFeedback();
    private void OnApplicationPause(bool paused) { if (paused) ResetFeedback(); }
    private void OnDestroy()
    {
        ResetFeedback();
        if (material != null) Destroy(material);
        if (Instance == this) Instance = null;
    }
}
