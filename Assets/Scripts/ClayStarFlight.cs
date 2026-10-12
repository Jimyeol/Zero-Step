using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A2 소프트 클레이 테마의 클리어 연출: 부서진 칸 자리에서 작은 별이 튀어나와 꼬리를 끌며 상단 하트로 "도로로록" 날아가고,
/// 하트마다 첫 별이 닿는 순간 콜백(하트 "뿅")과 작은 링 반짝임을 낸다. 전체 약 0.8초. 터치는 막지 않는다.
/// </summary>
public sealed class ClayStarFlight : VisualElement
{
    public const float HeartStep = 0.07f;
    private const float FlightDuration = 0.36f;
    private const float ExtraStarDelay = 0.045f;
    private const float BurstDuration = 0.24f;
    private const float StarRadius = 24f;

    private struct Flight
    {
        public Vector2 from;
        public Vector2 control;
        public Vector2 to;
        public float launch;
        public int heart;
        public float spin;
        public bool arrived;
    }

    private readonly List<Flight> flights = new List<Flight>();
    private readonly List<Vector2> sources;
    private readonly Vector2[] heartTargets;
    private readonly float[] burstStart;
    private readonly Action<int> onHeartReached;
    private readonly Func<Rect> avoidRectProvider;
    private float startTime;
    private float endTime;
    private bool built;
    private int buildAttempts;
    private IVisualElementScheduledItem ticker;
    private bool finished;

    /// <param name="sources">별이 출발할 위치(이 요소 좌표계).</param>
    /// <param name="heartTargets">하트 중심(이 요소 좌표계). 왼쪽부터 차례로 채워진다.</param>
    /// <param name="onHeartReached">하트 i에 첫 별이 닿을 때 한 번 불린다.</param>
    /// <param name="avoidRectProvider">별이 피해 돌아갈 영역(클리어 카드, 이 요소 좌표계). 첫 프레임 레이아웃 뒤에 읽는다.</param>
    public ClayStarFlight(IReadOnlyList<Vector2> sources, Vector2[] heartTargets, Action<int> onHeartReached, Func<Rect> avoidRectProvider = null)
    {
        this.sources = new List<Vector2>(sources);
        this.heartTargets = heartTargets;
        this.onHeartReached = onHeartReached;
        this.avoidRectProvider = avoidRectProvider;
        burstStart = new float[heartTargets.Length];
        for (int i = 0; i < burstStart.Length; i++)
            burstStart[i] = -1f;

        pickingMode = PickingMode.Ignore;
        style.position = Position.Absolute;
        style.left = 0f;
        style.right = 0f;
        style.top = 0f;
        style.bottom = 0f;
        generateVisualContent += Draw;

        ticker = schedule.Execute(Tick).Every(16);
    }

    /// <summary>피해 갈 카드의 레이아웃이 잡힐 때까지(최대 몇 프레임) 기다렸다가 경로를 만든다.</summary>
    private bool TryBuildFlights()
    {
        Rect avoid = avoidRectProvider != null ? avoidRectProvider() : Rect.zero;
        bool hasAvoid = avoid.width > 1f && avoid.height > 1f && !float.IsNaN(avoid.width) && !float.IsNaN(avoid.y);
        if (avoidRectProvider != null && !hasAvoid && buildAttempts++ < 6)
            return false;
        BuildFlights(hasAvoid ? avoid : Rect.zero);
        return true;
    }

    private void BuildFlights(Rect avoid)
    {
        built = true;
        startTime = Time.unscaledTime;
        bool hasAvoid = avoid.width > 1f;

        int hearts = heartTargets.Length;
        int count = Mathf.Max(hearts, sources.Count);
        var random = new System.Random(sources.Count * 7919 + hearts);
        float lastArrival = 0f;
        for (int j = 0; j < count; j++)
        {
            int heart = j % hearts;
            int round = j / hearts;
            Vector2 from = sources[j % sources.Count];
            Vector2 to = heartTargets[heart];
            float jitter = (float)(random.NextDouble() * 2.0 - 1.0);
            Vector2 control;
            if (hasAvoid)
            {
                // 클리어 카드 옆으로 크게 돌아 올라간다: 곡선 가운데(t=0.5)가 카드 바깥에 오도록 조절점을 잡는다.
                bool goLeft = from.x < avoid.center.x;
                float edge = goLeft ? avoid.xMin - 36f : avoid.xMax + 36f;
                float cx = 2f * edge - 0.5f * (from.x + to.x) + jitter * 40f;
                float cy = avoid.center.y + jitter * avoid.height * 0.2f;
                control = new Vector2(cx, cy);
            }
            else
            {
                // 옆으로 살짝 휘어 올라가는 곡선(별마다 방향이 달라 흩날리는 느낌).
                Vector2 mid = (from + to) * 0.5f;
                Vector2 delta = to - from;
                Vector2 side = new Vector2(-delta.y, delta.x).normalized;
                float bend = jitter * Mathf.Min(220f, delta.magnitude * 0.35f);
                control = mid + side * bend + new Vector2(0f, -Mathf.Min(160f, delta.magnitude * 0.18f));
            }
            float launch = heart * HeartStep + round * ExtraStarDelay;
            flights.Add(new Flight
            {
                from = from,
                control = control,
                to = to,
                launch = launch,
                heart = heart,
                spin = (float)(random.NextDouble() * 2.0 - 1.0) * 540f
            });
            lastArrival = Mathf.Max(lastArrival, launch + FlightDuration);
        }
        endTime = lastArrival + BurstDuration;
    }

    public bool IsRunning => !finished;

    /// <summary>남은 별은 바로 도착 처리하고 사라진다.</summary>
    public void Finish()
    {
        if (finished)
            return;
        if (!built)
            BuildFlights(Rect.zero);
        for (int i = 0; i < flights.Count; i++)
            Arrive(i);
        Remove();
    }

    private void Tick()
    {
        if (finished)
            return;
        if (!built && !TryBuildFlights())
            return;
        float t = Time.unscaledTime - startTime;
        for (int i = 0; i < flights.Count; i++)
        {
            if (!flights[i].arrived && t >= flights[i].launch + FlightDuration)
                Arrive(i);
        }
        if (t >= endTime)
        {
            Remove();
            return;
        }
        MarkDirtyRepaint();
    }

    private void Arrive(int index)
    {
        Flight f = flights[index];
        if (f.arrived)
            return;
        f.arrived = true;
        flights[index] = f;
        if (burstStart[f.heart] >= 0f)
            return;
        burstStart[f.heart] = Time.unscaledTime - startTime;
        onHeartReached?.Invoke(f.heart);
    }

    private void Remove()
    {
        finished = true;
        ticker?.Pause();
        RemoveFromHierarchy();
    }

    private void Draw(MeshGenerationContext context)
    {
        if (!built)
            return;
        Painter2D painter = context.painter2D;
        float t = Time.unscaledTime - startTime;

        // 하트에 닿은 자리: 테라코타 링이 퍼지며 사라진다("뾰로롱").
        for (int h = 0; h < heartTargets.Length; h++)
        {
            if (burstStart[h] < 0f)
                continue;
            float p = (t - burstStart[h]) / BurstDuration;
            if (p < 0f || p >= 1f)
                continue;
            Color ring = VisualTheme.Accent;
            ring.a = (1f - p) * 0.85f;
            painter.strokeColor = ring;
            painter.lineWidth = Mathf.Lerp(5f, 1f, p);
            painter.BeginPath();
            painter.Arc(heartTargets[h], Mathf.Lerp(14f, 38f, EaseOut(p)), 0f, 360f);
            painter.Stroke();
            for (int k = 0; k < 4; k++)
            {
                float angle = (k * 90f + 45f) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 dot = heartTargets[h] + dir * Mathf.Lerp(20f, 46f, EaseOut(p));
                DrawStar(painter, dot, Mathf.Lerp(6f, 1.5f, p), ring, 0f);
            }
        }

        for (int i = 0; i < flights.Count; i++)
        {
            Flight f = flights[i];
            if (f.arrived)
                continue;
            float local = (t - f.launch) / FlightDuration;
            if (local < 0f)
                continue;
            // 처음엔 툭 튀어나오고, 하트 쪽으로 갈수록 빨라진다.
            float travel = EaseIn(Mathf.Clamp01(local));
            float grow = Mathf.Clamp01(local / 0.18f);
            float size = StarRadius * (grow < 1f ? Mathf.Lerp(0.2f, 1.25f, grow) : Mathf.Lerp(1.25f, 0.7f, Mathf.Clamp01((local - 0.18f) / 0.82f)));
            float angle = f.spin * local;

            // 꼬리: 지나온 자리에 옅고 작은 별 두 개.
            for (int k = 2; k >= 1; k--)
            {
                float back = EaseIn(Mathf.Clamp01(local - k * 0.07f));
                if (local - k * 0.07f <= 0f)
                    continue;
                Color tail = VisualTheme.Accent;
                tail.a = 0.42f / k;
                DrawStar(painter, Bezier(f, back), size * (1f - k * 0.22f), tail, angle - k * 20f);
            }

            Vector2 position = Bezier(f, travel);
            DrawStar(painter, position, size * 1.28f, Color.white, angle);
            DrawStar(painter, position, size, VisualTheme.Accent, angle);
        }
    }

    private static Vector2 Bezier(Flight f, float t)
    {
        float u = 1f - t;
        return u * u * f.from + 2f * u * t * f.control + t * t * f.to;
    }

    private static void DrawStar(Painter2D painter, Vector2 center, float radius, Color color, float angleDegrees)
    {
        if (radius <= 0.3f)
            return;
        painter.fillColor = color;
        painter.BeginPath();
        float baseAngle = -Mathf.PI * 0.5f + angleDegrees * Mathf.Deg2Rad;
        for (int k = 0; k < 10; k++)
        {
            float a = baseAngle + k * Mathf.PI / 5f;
            float r = k % 2 == 0 ? radius : radius * 0.5f;
            Vector2 point = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            if (k == 0)
                painter.MoveTo(point);
            else
                painter.LineTo(point);
        }
        painter.ClosePath();
        painter.Fill();
    }

    private static float EaseIn(float t) => t * t * (1.4f - 0.4f * t);

    private static float EaseOut(float t)
    {
        float u = 1f - Mathf.Clamp01(t);
        return 1f - u * u;
    }
}
