using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A2 소프트 클레이 테마의 짧은 안내 칩: 흰 알약 안에 (선택) 별 아이콘과 한 줄 글자. 아래에서 살짝 올라와 잠깐 머물다 사라진다.
/// 스테이지 시작 때 도전 목표를 알려주는 데 쓴다. 터치는 막지 않는다.
/// </summary>
public sealed class ClayToastChip : VisualElement
{
    private const float InDuration = 0.18f;
    private const float OutDuration = 0.25f;

    private readonly VisualElement pill;
    private readonly float startTime;
    private readonly float holdSeconds;
    private readonly IVisualElementScheduledItem ticker;

    /// <param name="centerTop">칩 윗변 가운데 위치(부모 좌표).</param>
    /// <param name="delaySeconds">나타나기 전 대기(직전 화면의 클리어 카드가 사라질 시간).</param>
    public ClayToastChip(Vector2 centerTop, string text, bool showStar, float holdSeconds = 2.2f, float delaySeconds = 0f)
    {
        this.holdSeconds = Mathf.Max(0.3f, holdSeconds);
        startTime = Time.unscaledTime + Mathf.Max(0f, delaySeconds);
        pickingMode = PickingMode.Ignore;
        AddToClassList("clay-keep");
        style.position = Position.Absolute;
        style.left = centerTop.x;
        style.top = centerTop.y;
        style.width = 0f;
        style.height = 0f;
        style.overflow = Overflow.Visible;

        pill = new VisualElement { pickingMode = PickingMode.Ignore };
        pill.AddToClassList("clay-keep");
        pill.style.position = Position.Absolute;
        pill.style.translate = new Translate(Length.Percent(-50), 0f);
        pill.style.flexDirection = FlexDirection.Row;
        pill.style.alignItems = Align.Center;
        pill.style.backgroundColor = Color.white;
        pill.style.paddingLeft = 26f;
        pill.style.paddingRight = 30f;
        pill.style.paddingTop = 12f;
        pill.style.paddingBottom = 12f;
        const float radius = 36f;
        pill.style.borderTopLeftRadius = radius;
        pill.style.borderTopRightRadius = radius;
        pill.style.borderBottomLeftRadius = radius;
        pill.style.borderBottomRightRadius = radius;
        Color line = new Color32(0xDC, 0xD3, 0xC6, 0xFF);
        pill.style.borderLeftColor = line;
        pill.style.borderRightColor = line;
        pill.style.borderTopColor = line;
        pill.style.borderBottomColor = line;
        pill.style.borderLeftWidth = 2f;
        pill.style.borderRightWidth = 2f;
        pill.style.borderTopWidth = 2f;
        pill.style.borderBottomWidth = 2f;
        Add(pill);

        if (showStar)
        {
            var star = new VisualElement { pickingMode = PickingMode.Ignore };
            star.AddToClassList("clay-keep");
            star.style.width = 40f;
            star.style.height = 40f;
            star.style.marginRight = 12f;
            star.style.flexShrink = 0f;
            star.generateVisualContent += DrawStar;
            pill.Add(star);
        }

        var label = new Label(text) { pickingMode = PickingMode.Ignore };
        label.AddToClassList("clay-intro-text");
        label.AddToClassList("clay-keep");
        label.style.fontSize = 34f;
        label.style.color = VisualTheme.Ink;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        label.style.marginLeft = 0f;
        label.style.marginRight = 0f;
        label.style.marginTop = 0f;
        label.style.marginBottom = 0f;
        label.style.paddingLeft = 0f;
        label.style.paddingRight = 0f;
        label.style.paddingTop = 0f;
        label.style.paddingBottom = 0f;
        pill.Add(label);

        Animate();
        ticker = schedule.Execute(Animate).Every(16);
    }

    private void Animate()
    {
        float t = Time.unscaledTime - startTime;
        if (t < 0f)
        {
            style.opacity = 0f;
            return;
        }
        float total = InDuration + holdSeconds + OutDuration;
        if (t >= total)
        {
            ticker?.Pause();
            RemoveFromHierarchy();
            return;
        }
        float enter = Mathf.Clamp01(t / InDuration);
        float exit = Mathf.Clamp01((t - InDuration - holdSeconds) / OutDuration);
        float eased = 1f - (1f - enter) * (1f - enter);
        style.opacity = eased * (1f - exit);
        style.translate = new Translate(0f, (1f - eased) * 18f - exit * 10f);
    }

    private static void DrawStar(MeshGenerationContext context)
    {
        Rect r = context.visualElement.contentRect;
        if (r.width <= 1f)
            return;
        Painter2D painter = context.painter2D;
        painter.fillColor = VisualTheme.Accent;
        Vector2 center = r.center;
        float radius = r.width * 0.5f;
        painter.BeginPath();
        for (int k = 0; k < 10; k++)
        {
            float angle = -Mathf.PI * 0.5f + k * Mathf.PI / 5f;
            float rr = k % 2 == 0 ? radius : radius * 0.48f;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rr;
            if (k == 0)
                painter.MoveTo(point);
            else
                painter.LineTo(point);
        }
        painter.ClosePath();
        painter.Fill();
    }
}
