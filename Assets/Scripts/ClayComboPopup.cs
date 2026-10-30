using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A2 소프트 클레이 테마의 콤보 글자: 한 획 안에서 칸이 연달아 터질 때 터진 칸 위에 "x3"이 톡 튀어 오르고,
/// 위로 떠오르며 사라진다(약 0.75초). 콤보가 클수록 글자가 커지고, 이정표(5·10·15·20…)에서는 색 알약 위에 감탄사가 붙는다.
/// 터치는 막지 않는다.
/// </summary>
public sealed class ClayComboPopup : VisualElement
{
    public const int PerfectTier = 1000;

    private const float PopDuration = 0.16f;
    private const float HoldUntil = 0.42f;
    private const float Lifetime = 0.75f;

    private static readonly Color Gold = new Color32(0xE8, 0xA3, 0x3D, 0xFF);

    private readonly Label label;
    private readonly float startTime;
    private readonly float rise;
    private readonly float tilt;
    private readonly IVisualElementScheduledItem ticker;

    public static bool IsMilestone(int combo) => combo >= 5 && combo % 5 == 0;

    public static string TextFor(int combo)
    {
        if (!IsMilestone(combo))
            return "x" + combo;
        if (combo >= 20)
            return "x" + combo + " ZERO HERO!";
        if (combo >= 15)
            return "x" + combo + " WOW!";
        if (combo >= 10)
            return "x" + combo + " AMAZING!";
        return "x" + combo + " GREAT!";
    }

    public ClayComboPopup(Vector2 position, string text, int combo)
    {
        startTime = Time.unscaledTime;
        pickingMode = PickingMode.Ignore;
        AddToClassList("clay-keep");
        style.position = Position.Absolute;
        style.left = position.x;
        style.top = position.y;
        style.width = 0f;
        style.height = 0f;
        style.overflow = Overflow.Visible;

        bool perfect = combo >= PerfectTier;
        bool milestone = perfect || IsMilestone(combo);
        // 이정표 알약은 글자가 길어 크기를 조금 눌러 화면 안에 들어오게 한다.
        float size = perfect ? 88f : milestone ? 68f : Mathf.Min(54f + combo * 4f, 92f);
        rise = perfect ? 90f : 70f;
        tilt = milestone ? 0f : ((combo * 37) % 17 - 8);

        label = new Label(text) { pickingMode = PickingMode.Ignore };
        label.AddToClassList("clay-intro-text");
        label.AddToClassList("clay-keep");
        label.style.position = Position.Absolute;
        label.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
        label.style.fontSize = size;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        if (milestone)
        {
            // 이정표: 색 알약 위 흰 글자로 한눈에 띄게.
            Color pill = perfect || combo >= 10 ? Gold : VisualTheme.Accent;
            label.style.color = Color.white;
            label.style.backgroundColor = pill;
            label.style.paddingLeft = 28f;
            label.style.paddingRight = 28f;
            label.style.paddingTop = 8f;
            label.style.paddingBottom = 10f;
            // 반지름을 높이의 절반으로 둬야 타원이 아니라 알약 모양이 된다.
            float radius = (size * 1.2f + 18f) * 0.5f;
            label.style.borderTopLeftRadius = radius;
            label.style.borderTopRightRadius = radius;
            label.style.borderBottomLeftRadius = radius;
            label.style.borderBottomRightRadius = radius;
        }
        else
        {
            label.style.color = VisualTheme.Accent;
            label.style.unityTextOutlineColor = VisualTheme.Paper;
            label.style.unityTextOutlineWidth = 3f;
        }
        Add(label);

        Animate();
        ticker = schedule.Execute(Animate).Every(16);
    }

    private void Animate()
    {
        float t = Time.unscaledTime - startTime;
        if (t >= Lifetime)
        {
            ticker?.Pause();
            RemoveFromHierarchy();
            return;
        }

        float p = Mathf.Clamp01(t / PopDuration);
        float scale = p < 1f ? Mathf.Lerp(0.3f, 1f, EaseOutBack(p)) : 1f;
        float fade = t <= HoldUntil ? 1f : 1f - (t - HoldUntil) / (Lifetime - HoldUntil);
        float y = -rise * EaseOut(t / Lifetime);

        label.style.scale = new Scale(new Vector3(scale, scale, 1f));
        label.style.rotate = new Rotate(tilt * (1f - p * 0.6f));
        style.translate = new Translate(0f, y);
        style.opacity = Mathf.Clamp01(fade);
    }

    private static float EaseOut(float t)
    {
        float u = 1f - Mathf.Clamp01(t);
        return 1f - u * u;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = Mathf.Clamp01(t) - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}

/// <summary>콤보 이정표·PERFECT 순간에 화면 전체가 따뜻한 색으로 짧게 번쩍인다(약 0.22초).</summary>
public sealed class ClayScreenFlash : VisualElement
{
    private const float Duration = 0.22f;

    private readonly float startTime;
    private readonly float peak;
    private readonly IVisualElementScheduledItem ticker;

    public ClayScreenFlash(float peakAlpha)
    {
        peak = Mathf.Clamp01(peakAlpha);
        startTime = Time.unscaledTime;
        pickingMode = PickingMode.Ignore;
        AddToClassList("clay-keep");
        style.position = Position.Absolute;
        style.left = 0f;
        style.right = 0f;
        style.top = 0f;
        style.bottom = 0f;
        Color c = Color.Lerp(VisualTheme.Accent, Color.white, 0.55f);
        c.a = 1f;
        style.backgroundColor = c;
        style.opacity = peak;
        ticker = schedule.Execute(Animate).Every(16);
    }

    private void Animate()
    {
        float t = (Time.unscaledTime - startTime) / Duration;
        if (t >= 1f)
        {
            ticker?.Pause();
            RemoveFromHierarchy();
            return;
        }
        float u = 1f - t;
        style.opacity = peak * u * u;
    }
}
