using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A2 소프트 클레이 테마의 스테이지 클리어 카드: 화면 가운데 흰 카드가 튀어나오고(눌렸다 펴짐),
/// 별 3개가 하나씩 "펑" 하고 채워진다. 터치는 막지 않아(탭하면 바로 다음 스테이지) 게임 흐름은 그대로다.
/// 스스로 애니메이션하고, 시간이 지나거나 Dismiss()가 불리면 사라진다.
/// </summary>
public sealed class ClayClearCard : VisualElement
{
    private const float PopDuration = 0.28f;
    private const float StarStart = 0.16f;
    private const float StarStep = 0.13f;
    private const float StarPopDuration = 0.22f;
    private const float AutoHideAfter = 1.9f;
    private const float FadeDuration = 0.18f;

    private readonly VisualElement card;
    private readonly VisualElement starRow;
    private readonly Label titleLabel;
    private readonly Label subtitleLabel;
    private readonly Label tapLabel;
    private readonly int stars;
    private readonly float startTime;
    private float dismissTime = -1f;
    private IVisualElementScheduledItem ticker;

    public ClayClearCard(string title, string subtitle, string tapHint, int stars)
    {
        this.stars = Mathf.Clamp(stars, 0, 3);
        startTime = Time.unscaledTime;
        pickingMode = PickingMode.Ignore;
        style.position = Position.Absolute;
        style.left = 0f;
        style.right = 0f;
        style.top = 0f;
        style.bottom = 0f;
        style.alignItems = Align.Center;
        style.justifyContent = Justify.FlexStart;
        style.paddingTop = Length.Percent(24);

        card = new VisualElement { pickingMode = PickingMode.Ignore };
        card.style.width = Length.Percent(66);
        card.style.backgroundColor = Color.white;
        card.style.borderTopLeftRadius = 44f;
        card.style.borderTopRightRadius = 44f;
        card.style.borderBottomLeftRadius = 44f;
        card.style.borderBottomRightRadius = 44f;
        card.style.paddingTop = 44f;
        card.style.paddingBottom = 40f;
        card.style.alignItems = Align.Center;
        Add(card);

        titleLabel = MakeLabel(title, 72f, VisualTheme.Ink);
        card.Add(titleLabel);

        starRow = new VisualElement { pickingMode = PickingMode.Ignore };
        starRow.style.width = 420f;
        starRow.style.height = 130f;
        starRow.style.marginTop = 18f;
        starRow.generateVisualContent += DrawStars;
        card.Add(starRow);

        subtitleLabel = MakeLabel(subtitle, 32f, VisualTheme.Accent);
        subtitleLabel.style.marginTop = 10f;
        subtitleLabel.style.display = string.IsNullOrEmpty(subtitle) ? DisplayStyle.None : DisplayStyle.Flex;
        card.Add(subtitleLabel);

        tapLabel = MakeLabel(tapHint, 26f, VisualTheme.InkSoft);
        tapLabel.style.marginTop = 18f;
        tapLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
        card.Add(tapLabel);

        Animate();
        ticker = schedule.Execute(Animate).Every(16);
    }

    /// <summary>흰 카드 본체의 최종 영역(튀어나오는 크기 변화 제외, target 좌표계). 별 날아가는 연출이 피해 간다.</summary>
    public Rect CardRectIn(VisualElement target) => this.ChangeCoordinatesTo(target, card.layout);

    public void Dismiss()
    {
        if (dismissTime < 0f)
            dismissTime = Time.unscaledTime;
    }

    private static Label MakeLabel(string text, float size, Color color)
    {
        var label = new Label(text) { pickingMode = PickingMode.Ignore };
        label.AddToClassList("clay-intro-text");
        label.style.fontSize = size;
        label.style.color = color;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        return label;
    }

    private void Animate()
    {
        float t = Time.unscaledTime - startTime;
        if (dismissTime < 0f && t > AutoHideAfter)
            dismissTime = Time.unscaledTime;

        // 카드: 작게 눌린 채 튀어나와 살짝 넘쳤다가 자리잡는다.
        float p = Mathf.Clamp01(t / PopDuration);
        float s = EaseOutBack(p);
        float squash = p < 1f ? Mathf.Sin(p * Mathf.PI) * 0.06f : 0f;
        card.style.scale = new Scale(new Vector3(Mathf.Max(0.01f, s * (1f + squash)), Mathf.Max(0.01f, s * (1f - squash)), 1f));
        float alpha = Mathf.Clamp01(p * 3f);
        tapLabel.style.opacity = Mathf.Clamp01((t - 0.55f) / 0.25f);

        if (dismissTime >= 0f)
        {
            float f = Mathf.Clamp01((Time.unscaledTime - dismissTime) / FadeDuration);
            alpha *= 1f - f;
            card.style.scale = new Scale(new Vector3(1f - f * 0.08f, 1f - f * 0.08f, 1f));
            if (f >= 1f)
            {
                ticker?.Pause();
                RemoveFromHierarchy();
                return;
            }
        }
        style.opacity = alpha;
        starRow.MarkDirtyRepaint();
    }

    private void DrawStars(MeshGenerationContext context)
    {
        Rect r = starRow.contentRect;
        if (r.width <= 1f)
            return;
        Painter2D painter = context.painter2D;
        float t = Time.unscaledTime - startTime;
        float radius = r.height * 0.42f;
        for (int i = 0; i < 3; i++)
        {
            Vector2 center = new Vector2(r.width * (0.18f + i * 0.32f), r.height * (i == 1 ? 0.44f : 0.56f));
            bool earned = i < stars;
            float local = Mathf.Clamp01((t - (StarStart + i * StarStep)) / StarPopDuration);
            // 빈 별 자리(연한 종이색)는 처음부터 보인다.
            DrawStar(painter, center, radius * (i == 1 ? 1.1f : 1f), VisualTheme.PaperShade);
            if (!earned || local <= 0f)
                continue;
            float pop = local < 0.6f ? Mathf.Lerp(0.2f, 1.28f, local / 0.6f) : Mathf.Lerp(1.28f, 1f, (local - 0.6f) / 0.4f);
            DrawStar(painter, center, radius * (i == 1 ? 1.1f : 1f) * pop, VisualTheme.Accent);
        }
    }

    private static void DrawStar(Painter2D painter, Vector2 center, float radius, Color color)
    {
        painter.fillColor = color;
        painter.lineJoin = LineJoin.Round;
        painter.strokeColor = color;
        painter.lineWidth = radius * 0.12f;
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
        painter.Stroke();
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
