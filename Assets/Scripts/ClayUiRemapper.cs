using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A2 소프트 클레이 테마: 팝업·스낵바처럼 인라인 스타일로 어두운 네온 색을 쓰는 UI를 종이·점토 색으로 바꿔 칠한다.
/// 같은 요소에 여러 번 적용해도 결과가 같다(이미 바뀐 색은 다시 바뀌지 않음). 코드가 나중에 네온 색을 다시 넣어도
/// 주기적으로 다시 적용하면 맞춰진다. 상단 HUD·하단 버튼·인트로는 전용 스타일이 있으므로 건드리지 않는다.
/// </summary>
public static class ClayUiRemapper
{
    private static readonly Color Card = new Color32(0xFB, 0xF8, 0xF3, 0xFF);
    private static readonly Color Line = new Color32(0xDC, 0xD3, 0xC6, 0xFF);
    private static readonly Color AccentText = new Color32(0xC0, 0x60, 0x3E, 0xFF);
    private static readonly Color WarmDim = new Color(0.24f, 0.2f, 0.16f, 0.45f);

    private static readonly string[] SkippedNames = { "TopBar", "BottomBar", "RuntimeSplashOverlay", "NeonIntro" };

    public static void Apply(VisualElement root)
    {
        if (root == null || !VisualTheme.IsSoftClay)
            return;
        Visit(root);
    }

    private static void Visit(VisualElement element)
    {
        string name = element.name;
        if (!string.IsNullOrEmpty(name))
        {
            for (int i = 0; i < SkippedNames.Length; i++)
            {
                if (name == SkippedNames[i])
                    return;
            }
        }

        // 코드가 점토 색을 직접 칠한 요소(.clay-keep)는 그대로 두고 자식만 본다.
        if (!element.ClassListContains("clay-keep"))
            Remap(element);
        int count = element.hierarchy.childCount;
        for (int i = 0; i < count; i++)
            Visit(element.hierarchy[i]);
    }

    private static void Remap(VisualElement element)
    {
        IResolvedStyle resolved = element.resolvedStyle;

        Color bg = resolved.backgroundColor;
        if (bg.a > 0.02f)
        {
            Color mapped = MapBackground(bg, element);
            if (!Approximately(mapped, bg))
                element.style.backgroundColor = mapped;
        }

        RemapBorder(element, resolved.borderLeftColor, c => element.style.borderLeftColor = c);
        RemapBorder(element, resolved.borderRightColor, c => element.style.borderRightColor = c);
        RemapBorder(element, resolved.borderTopColor, c => element.style.borderTopColor = c);
        RemapBorder(element, resolved.borderBottomColor, c => element.style.borderBottomColor = c);

        if (element is TextElement)
        {
            Color text = resolved.color;
            // 테라코타(강조) 버튼 위 글자는 흰색 그대로 둔다.
            Color mappedText = IsOnAccent(element) ? text : MapText(text);
            if (!Approximately(mappedText, text))
                element.style.color = mappedText;
            // 네온 글로(인라인 그림자)는 종이 위에서 번져 보이므로 끈다.
            StyleTextShadow inlineShadow = element.style.textShadow;
            if (inlineShadow.keyword == StyleKeyword.Undefined && inlineShadow.value.color.a > 0.01f)
                element.style.textShadow = new TextShadow { offset = Vector2.zero, blurRadius = 0f, color = Color.clear };
            if (resolved.unityTextOutlineWidth > 0f)
                element.style.unityTextOutlineWidth = 0f;
        }

        if (element is Image image)
        {
            Color tint = image.tintColor;
            if (Luminance(tint) > 0.85f && tint.a > 0.5f)
                image.tintColor = VisualTheme.Ink;
        }
    }

    private static Color MapBackground(Color c, VisualElement element)
    {
        float lum = Luminance(c);
        Color.RGBToHSV(c, out _, out float sat, out _);
        if (lum < 0.3f)
        {
            bool isOverlay = !string.IsNullOrEmpty(element.name) && element.name.EndsWith("Overlay");
            if (isOverlay)
                return new Color(WarmDim.r, WarmDim.g, WarmDim.b, Mathf.Min(c.a, WarmDim.a));
            if (c.a >= 0.8f)
                return Card;
            Color shade = VisualTheme.PaperShade;
            shade.a = c.a;
            return shade;
        }
        if (sat > 0.35f)
        {
            Color accent = VisualTheme.Accent;
            accent.a = c.a;
            return accent;
        }
        return c;
    }

    private static void RemapBorder(VisualElement element, Color c, System.Action<Color> set)
    {
        if (c.a <= 0.02f)
            return;
        Color mapped = Line;
        mapped.a = Mathf.Min(1f, c.a);
        if (!Approximately(mapped, c))
            set(mapped);
    }

    private static Color MapText(Color c)
    {
        if (c.a <= 0.02f)
            return c;
        Color.RGBToHSV(c, out _, out float sat, out _);
        float lum = Luminance(c);
        if (sat > 0.35f && lum > 0.35f)
            return new Color(AccentText.r, AccentText.g, AccentText.b, c.a);
        if (lum > 0.6f)
            return new Color(VisualTheme.Ink.r, VisualTheme.Ink.g, VisualTheme.Ink.b, c.a);
        return c;
    }

    private static bool IsOnAccent(VisualElement element)
    {
        for (VisualElement e = element; e != null; e = e.hierarchy.parent)
        {
            Color bg = e.resolvedStyle.backgroundColor;
            if (bg.a > 0.5f)
            {
                Color mapped = MapBackground(bg, e);
                Color.RGBToHSV(mapped, out _, out float sat, out _);
                return sat > 0.35f;
            }
        }
        return false;
    }

    private static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

    private static bool Approximately(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f && Mathf.Abs(a.a - b.a) < 0.004f;
    }
}
