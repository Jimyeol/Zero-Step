using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A2 소프트 클레이 테마의 시작 홈 화면. 1000판 엔딩을 본 유저에게만 인트로 뒤에 뜬다
/// (아직 다 깨지 못한 유저는 홈 화면 없이 바로 게임이 시작된다).
/// 마스코트, 제목, 안내 한 줄, 버튼 두 개: 무한 연구소 도전하기 / 스테이지 다시 하기.
/// </summary>
public sealed class ClayHomeScreen : VisualElement
{
    public const string ElementName = "ClayHomeScreen";

    public struct Texts
    {
        public string title;       // "무한 연구소"
        public string subtitle;    // 안내 한 줄
        public string labButton;   // "무한 연구소 도전하기"
        public string labProgress; // "LAB 12"
        public string stageButton; // "스테이지 다시 하기"
    }

    public ClayHomeScreen(Texts texts, Rect panel, Action onLab, Action onStages)
    {
        name = ElementName;
        pickingMode = PickingMode.Position;
        style.position = Position.Absolute;
        style.left = 0f;
        style.right = 0f;
        style.top = 0f;
        style.bottom = 0f;
        style.backgroundColor = VisualTheme.Paper;
        style.alignItems = Align.Center;
        style.justifyContent = Justify.Center;
        // 아래의 게임 화면으로 터치가 새지 않게 한다.
        RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
        RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

        float width = Mathf.Floor(Mathf.Min(panel.width - 160f, 820f));

        // 마스코트(몸통에 테라코타 색을 입히고 얼굴을 겹친다)
        const float mascot = 300f;
        var mascotBox = new VisualElement { pickingMode = PickingMode.Ignore };
        mascotBox.style.width = mascot;
        mascotBox.style.height = mascot;
        mascotBox.style.flexShrink = 0f;
        mascotBox.Add(MakeSprite(VisualTheme.ZeroBodySprite, VisualTheme.Accent));
        mascotBox.Add(MakeSprite(VisualTheme.ZeroFaceSprite, Color.white));
        Add(mascotBox);

        Label title = MakeLabel(texts.title, 84f, VisualTheme.Ink, true);
        title.style.marginTop = 8f;
        Add(title);

        Label subtitle = MakeLabel(texts.subtitle, 36f, VisualTheme.InkSoft, false);
        subtitle.style.width = width;
        subtitle.style.whiteSpace = WhiteSpace.Normal;
        subtitle.style.marginTop = 18f;
        subtitle.style.marginBottom = 64f;
        Add(subtitle);

        VisualElement labButton = MakeButton(width, 148f, VisualTheme.Accent, onLab);
        labButton.Add(MakeLabel(texts.labButton, 44f, Color.white, true));
        if (!string.IsNullOrEmpty(texts.labProgress))
        {
            Label progress = MakeLabel(texts.labProgress, 28f, new Color(1f, 1f, 1f, 0.85f), true);
            progress.style.marginTop = 2f;
            labButton.Add(progress);
        }
        Add(labButton);

        VisualElement stageButton = MakeButton(width, 120f, Color.white, onStages);
        stageButton.style.marginTop = 28f;
        stageButton.Add(MakeLabel(texts.stageButton, 40f, VisualTheme.Ink, true));
        Add(stageButton);
    }

    private static Image MakeSprite(Sprite sprite, Color tint)
    {
        var image = new Image { sprite = sprite, tintColor = tint, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
        image.style.position = Position.Absolute;
        image.style.left = 0f;
        image.style.right = 0f;
        image.style.top = 0f;
        image.style.bottom = 0f;
        return image;
    }

    private static Label MakeLabel(string text, float size, Color color, bool bold)
    {
        var label = new Label(text) { pickingMode = PickingMode.Ignore };
        label.AddToClassList("clay-intro-text");
        label.style.fontSize = size;
        label.style.color = color;
        label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        label.style.marginLeft = 0f;
        label.style.marginRight = 0f;
        label.style.marginTop = 0f;
        label.style.marginBottom = 0f;
        label.style.paddingLeft = 0f;
        label.style.paddingRight = 0f;
        label.style.paddingTop = 0f;
        label.style.paddingBottom = 0f;
        return label;
    }

    private static VisualElement MakeButton(float width, float height, Color background, Action onClick)
    {
        var button = new VisualElement { pickingMode = PickingMode.Position };
        button.style.width = width;
        button.style.height = height;
        button.style.flexShrink = 0f;
        float radius = height * 0.5f;
        button.style.borderTopLeftRadius = radius;
        button.style.borderTopRightRadius = radius;
        button.style.borderBottomLeftRadius = radius;
        button.style.borderBottomRightRadius = radius;
        button.style.backgroundColor = background;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.Center;
        button.RegisterCallback<ClickEvent>(evt =>
        {
            evt.StopPropagation();
            onClick?.Invoke();
        });
        return button;
    }
}
