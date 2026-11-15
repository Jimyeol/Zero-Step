using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 피버 타임 화면 연출(UI Toolkit, 입력은 통과시킨다).
/// - 화면 테두리: 무지개색 네온관이 둘러싸고, 빛이 테두리를 따라 달리며 배경음악 박자(150BPM)에 맞춰 반짝인다.
/// - 상단 "FEVER TIME!": 글자가 화면 가운데에서 하나씩 튀어나온 뒤 상단 바 아래로 올라가 자리 잡고,
///   글자마다 무지개색으로 물결치며 빛난다. 남은 시간이 5초 이하면 초 숫자가 붙고 테두리가 두 배로 빨리 깜빡인다.
/// </summary>
public sealed class ClayFeverOverlay : VisualElement
{
    private const string Title = "FEVER TIME!";
    private const float Beat = 0.4f;            // bgm_fever 150BPM
    private const float IntroSeconds = 1.05f;
    private const float FadeOutSeconds = 0.45f;
    private const int FrameSegments = 120;

    private readonly VisualElement frame;
    private readonly VisualElement header;
    private readonly List<Label> letters = new List<Label>();
    private readonly Label secondsLabel;
    private readonly float headerTop;
    private readonly float startTime;
    private float endStart = -1f;
    private int secondsLeft = 99;
    private float secondsBumpTime = -10f;
    private IVisualElementScheduledItem tick;

    public ClayFeverOverlay(float headerTop)
    {
        this.headerTop = headerTop;
        startTime = Time.unscaledTime;
        pickingMode = PickingMode.Ignore;
        style.position = Position.Absolute;
        style.left = 0;
        style.top = 0;
        style.right = 0;
        style.bottom = 0;

        frame = new VisualElement { pickingMode = PickingMode.Ignore };
        frame.style.position = Position.Absolute;
        frame.style.left = 0;
        frame.style.top = 0;
        frame.style.right = 0;
        frame.style.bottom = 0;
        frame.generateVisualContent += DrawFrame;
        Add(frame);

        header = new VisualElement { pickingMode = PickingMode.Ignore };
        header.style.position = Position.Absolute;
        header.style.left = 0;
        header.style.right = 0;
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.Center;
        header.style.alignItems = Align.Center;
        foreach (char c in Title)
        {
            var label = new Label(c == ' ' ? " " : c.ToString()) { pickingMode = PickingMode.Ignore };
            StyleLetter(label, 58f);
            label.style.width = c == ' ' ? 18f : StyleKeyword.Auto;
            letters.Add(label);
            header.Add(label);
        }
        secondsLabel = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
        StyleLetter(secondsLabel, 66f);
        secondsLabel.style.marginLeft = 10f;
        secondsLabel.style.display = DisplayStyle.None;
        header.Add(secondsLabel);
        Add(header);

        tick = schedule.Execute(Tick).Every(16);
        Tick();
    }

    private static void StyleLetter(Label label, float size)
    {
        label.style.fontSize = size;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginLeft = 1f;
        label.style.marginRight = 1f;
        label.style.paddingLeft = 0;
        label.style.paddingRight = 0;
        // 밝은 종이 배경에서도 또렷하게: 진한 잉크 외곽선 + 같은 색 빛 번짐.
        label.style.unityTextOutlineWidth = 2.2f;
        label.style.unityTextOutlineColor = VisualTheme.Ink;
    }

    /// <summary>남은 초(5초 이하이면 제목 옆에 크게 보여 준다).</summary>
    public void SetSecondsLeft(int seconds)
    {
        if (seconds == secondsLeft)
            return;
        secondsLeft = seconds;
        if (seconds <= 5 && seconds >= 0)
        {
            secondsLabel.text = seconds.ToString();
            secondsLabel.style.display = DisplayStyle.Flex;
            secondsBumpTime = Time.unscaledTime;
        }
    }

    /// <summary>피버가 끝나면: 서서히 사라진 뒤 스스로 빠진다.</summary>
    public void End()
    {
        if (endStart < 0f)
            endStart = Time.unscaledTime;
    }

    private bool Urgent => secondsLeft <= 5;

    private void Tick()
    {
        float now = Time.unscaledTime;
        float age = now - startTime;
        float fade = 1f;
        if (endStart >= 0f)
        {
            fade = 1f - (now - endStart) / FadeOutSeconds;
            if (fade <= 0f)
            {
                tick?.Pause();
                RemoveFromHierarchy();
                return;
            }
        }
        float intensity = Mathf.Max(0.25f, GameplayFeedback.Intensity);
        style.opacity = fade;
        UpdateHeader(now, age, intensity);
        frame.MarkDirtyRepaint();
    }

    private void UpdateHeader(float now, float age, float intensity)
    {
        float height = layout.height;
        if (float.IsNaN(height) || height < 1f)
            height = Screen.height;
        // 처음: 화면 가운데서 크게 → 0.75초 뒤 상단 자리로 올라간다.
        float move = Mathf.Clamp01((age - 0.75f) / 0.3f);
        move = move * move * (3f - 2f * move);
        float centerTop = height * 0.4f;
        header.style.top = Mathf.Lerp(centerTop, headerTop, move);
        float headerScale = Mathf.Lerp(1.7f, 1f, move);
        header.style.scale = new Scale(new Vector3(headerScale, headerScale, 1f));

        for (int i = 0; i < letters.Count; i++)
        {
            Label label = letters[i];
            // 글자가 하나씩 튀어나온다(0 → 1.3 → 1, 살짝 회전).
            float appear = Mathf.Clamp01((age - i * 0.05f) / 0.22f);
            float pop = appear < 1f ? EaseOutBack(appear) : 1f;
            float spinIn = (1f - appear) * (i % 2 == 0 ? -40f : 40f);
            // 물결: 글자마다 박자에 맞춰 위아래로.
            float wave = Mathf.Sin(now * 7f - i * 0.55f) * 4f * intensity * (Urgent ? 1.5f : 1f);
            label.style.translate = new Translate(0, wave, 0);
            label.style.rotate = new Rotate(spinIn);
            label.style.scale = new Scale(new Vector3(pop, pop, 1f));
            Color c = Color.HSVToRGB(Mathf.Repeat(now * 0.45f + i * 0.08f, 1f), 0.85f, 1f);
            label.style.color = c;
            label.style.textShadow = new TextShadow { color = new Color(c.r, c.g, c.b, 1f), blurRadius = 18f, offset = Vector2.zero };
        }

        if (secondsLabel.style.display == DisplayStyle.Flex)
        {
            float b = Mathf.Clamp01((now - secondsBumpTime) / 0.3f);
            float s = 1f + 0.5f * (1f - b) * (1f - b);
            secondsLabel.style.scale = new Scale(new Vector3(s, s, 1f));
            Color red = Color.Lerp(new Color(1f, 0.25f, 0.3f), Color.white, 0.5f + 0.5f * Mathf.Sin(now * 18f));
            secondsLabel.style.color = red;
            secondsLabel.style.textShadow = new TextShadow { color = new Color(1f, 0.2f, 0.25f, 0.95f), blurRadius = 12f, offset = Vector2.zero };
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 2.2f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    /// <summary>둥근 사각 테두리를 따라 무지개 네온관을 그린다(바깥 은은한 빛 + 가운데 밝은 심).</summary>
    private void DrawFrame(MeshGenerationContext mgc)
    {
        Rect r = frame.contentRect;
        if (r.width < 4f || r.height < 4f)
            return;
        float now = Time.unscaledTime;
        float age = now - startTime;
        float intensity = Mathf.Max(0.25f, GameplayFeedback.Intensity);
        float inset = 12f;
        float radius = Mathf.Min(34f, Mathf.Min(r.width, r.height) * 0.08f);
        var points = RoundedRectPoints(new Rect(r.x + inset, r.y + inset, r.width - inset * 2f, r.height - inset * 2f), radius, FrameSegments);

        // 박자 반짝임(마지막 5초는 두 배 빠르게)과 처음에 한 바퀴 불이 들어오는 연출.
        float beat = Urgent ? Beat * 0.5f : Beat;
        float flash = Mathf.Exp(-Mathf.Repeat(age, beat) / beat * 5f);
        float lit = Mathf.Clamp01(age / 0.55f);
        float chaseSpeed = Urgent ? 2.6f : 1.4f;

        Painter2D p = mgc.painter2D;
        p.lineCap = LineCap.Round;
        int count = points.Count;
        for (int layer = 0; layer < 3; layer++)
        {
            float width = layer == 0 ? 34f : layer == 1 ? 16f : 7f;
            float alphaBase = layer == 0 ? 0.22f : layer == 1 ? 0.5f : 1f;
            p.lineWidth = width;
            for (int i = 0; i < count; i++)
            {
                float u = i / (float)count;
                if (u > lit)
                    break;
                float chase = 0.6f + 0.4f * Mathf.Max(0f, Mathf.Sin((u * 6f - age * chaseSpeed) * Mathf.PI * 2f));
                float twinkle = (i % 2 == 0) == (Mathf.Repeat(age, beat * 2f) < beat) ? 1f : 0.75f;
                float a = alphaBase * chase * twinkle * (0.7f + 0.3f * flash) * intensity;
                Color c = Color.HSVToRGB(Mathf.Repeat(u * 2f - age * 0.35f, 1f), layer == 2 ? 0.7f : 0.95f, 1f);
                c.a = Mathf.Clamp01(a);
                p.strokeColor = c;
                p.BeginPath();
                p.MoveTo(points[i]);
                p.LineTo(points[(i + 1) % count]);
                p.Stroke();
            }
        }
    }

    private static List<Vector2> RoundedRectPoints(Rect r, float radius, int segments)
    {
        // 둘레를 따라 같은 간격으로 점을 찍는다(왼쪽 위 모서리에서 시계 방향).
        float straightW = r.width - 2f * radius;
        float straightH = r.height - 2f * radius;
        float arc = Mathf.PI * 0.5f * radius;
        float perimeter = 2f * (straightW + straightH) + 4f * arc;
        var pts = new List<Vector2>(segments);
        for (int k = 0; k < segments; k++)
        {
            float d = perimeter * k / segments;
            pts.Add(PointAt(r, radius, straightW, straightH, arc, d));
        }
        return pts;
    }

    private static Vector2 PointAt(Rect r, float radius, float w, float h, float arc, float d)
    {
        // 위쪽 직선 → 오른쪽 위 모서리 → 오른쪽 직선 → ... 순서.
        float x0 = r.xMin, y0 = r.yMin, x1 = r.xMax, y1 = r.yMax;
        if (d < w) return new Vector2(x0 + radius + d, y0);
        d -= w;
        if (d < arc) return Corner(new Vector2(x1 - radius, y0 + radius), radius, -90f + d / arc * 90f);
        d -= arc;
        if (d < h) return new Vector2(x1, y0 + radius + d);
        d -= h;
        if (d < arc) return Corner(new Vector2(x1 - radius, y1 - radius), radius, d / arc * 90f);
        d -= arc;
        if (d < w) return new Vector2(x1 - radius - d, y1);
        d -= w;
        if (d < arc) return Corner(new Vector2(x0 + radius, y1 - radius), radius, 90f + d / arc * 90f);
        d -= arc;
        if (d < h) return new Vector2(x0, y1 - radius - d);
        d -= h;
        return Corner(new Vector2(x0 + radius, y0 + radius), radius, 180f + Mathf.Min(d, arc) / arc * 90f);
    }

    private static Vector2 Corner(Vector2 center, float radius, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        // UI 좌표는 y가 아래로 커진다: -90도가 위쪽.
        return center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
    }
}
