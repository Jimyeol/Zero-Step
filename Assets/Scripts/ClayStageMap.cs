using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A2 소프트 클레이 테마의 챕터 지도(전체 화면). 상단 STAGE 글자를 누르면 열린다.
/// 20판 = 1챕터. 4×5 칸이 그대로 모자이크라서, 깬 판이 채워질수록 그 챕터의 숫자 그림(0~9 픽셀 글자)이 드러난다.
/// 깬 판은 다시 할 수 있고, 아직 안 깬 판은 맨 앞(frontier)부터 세 판까지 골라 할 수 있다.
/// </summary>
public sealed class ClayStageMap : VisualElement
{
    public const string ElementName = "ClayStageMap";
    public const int StagesPerChapter = 20;
    private const int Columns = 4;
    private const int Rows = 5;
    private const float Gap = 20f;

    // 3×5 픽셀 숫자(왼쪽 세 칸에 그리고 오른쪽 한 칸은 비운다). 챕터 번호의 끝자리 숫자가 그 챕터의 그림이다.
    private static readonly string[] DigitRows =
    {
        "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
        "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111"
    };

    public struct Texts
    {
        public string chapter;      // "챕터 {n}"
        public string complete;     // "완성!"
        public string continuePlay; // "이어하기"
        public string latest;       // "최신 판으로"
        public string lab;          // "연구소"
    }

    private readonly Texts texts;
    private readonly int finalStage;
    private readonly int currentStage;
    private readonly int frontier;
    private readonly Func<int, bool> isUnlocked;
    private readonly Action<int> onSelectStage;
    private readonly Action onClose;
    private readonly float cellSize;

    private readonly Label chapterLabel;
    private readonly Label rangeLabel;
    private readonly Label progressLabel;
    private readonly Label totalStarsLabel;
    private readonly VisualElement grid;
    private readonly VisualElement previousButton;
    private readonly VisualElement nextButton;
    private int chapter;

    public ClayStageMap(
        Texts texts, Rect panel, float topInset, float bottomInset,
        int currentStage, int frontier, int finalStage,
        Func<int, bool> isUnlocked, Action<int> onSelectStage, Action onClose, Action onLab, Action onDebug)
    {
        this.texts = texts;
        this.finalStage = Mathf.Max(1, finalStage);
        this.currentStage = currentStage;
        this.frontier = frontier;
        this.isUnlocked = isUnlocked;
        this.onSelectStage = onSelectStage;
        this.onClose = onClose;

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
        style.paddingTop = Mathf.Max(40f, topInset + 24f);
        style.paddingBottom = Mathf.Max(40f, bottomInset + 24f);
        // 지도 아래의 게임 화면으로 터치가 새지 않게 한다.
        RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
        RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

        const float headerHeight = 110f + 150f + 56f;
        const float footerHeight = 150f;
        float availableHeight = panel.height - Mathf.Max(40f, topInset + 24f) - Mathf.Max(40f, bottomInset + 24f) - headerHeight - footerHeight - 40f;
        float byWidth = (panel.width - 140f - Gap * (Columns - 1)) / Columns;
        float byHeight = (availableHeight - Gap * (Rows - 1)) / Rows;
        cellSize = Mathf.Clamp(Mathf.Min(byWidth, byHeight), 90f, 230f);
        float gridWidth = cellSize * Columns + Gap * (Columns - 1);

        // 윗줄: 모은 별 합계 · 닫기
        var topRow = Row(110f);
        topRow.style.width = gridWidth;
        topRow.style.justifyContent = Justify.SpaceBetween;
        var starTotal = Row(72f);
        var starIcon = new VisualElement { pickingMode = PickingMode.Ignore };
        starIcon.style.width = 52f;
        starIcon.style.height = 52f;
        starIcon.style.marginRight = 12f;
        starIcon.generateVisualContent += ctx => DrawStar(ctx.painter2D, ctx.visualElement.contentRect.center, 26f, VisualTheme.Accent);
        starTotal.Add(starIcon);
        totalStarsLabel = MakeLabel(string.Empty, 44f, VisualTheme.Ink);
        starTotal.Add(totalStarsLabel);
        topRow.Add(starTotal);
        var rightButtons = Row(96f);
        if (onDebug != null)
        {
            var debugButton = RoundButton(96f, () => onDebug());
            debugButton.Add(MakeLabel("DBG", 26f, VisualTheme.InkSoft));
            debugButton.style.marginRight = 16f;
            rightButtons.Add(debugButton);
        }
        var closeButton = RoundButton(96f, Close);
        closeButton.generateVisualContent += ctx => DrawCross(ctx.painter2D, ctx.visualElement.contentRect);
        rightButtons.Add(closeButton);
        topRow.Add(rightButtons);
        Add(topRow);

        // 챕터 줄: ‹ 챕터 3 (41–60) ›
        var chapterRow = Row(150f);
        chapterRow.style.width = gridWidth;
        chapterRow.style.justifyContent = Justify.SpaceBetween;
        previousButton = RoundButton(104f, () => ShowChapter(chapter - 1));
        previousButton.generateVisualContent += ctx => DrawChevron(ctx.painter2D, ctx.visualElement.contentRect, -1f);
        chapterRow.Add(previousButton);
        var titleBlock = new VisualElement { pickingMode = PickingMode.Ignore };
        titleBlock.style.alignItems = Align.Center;
        chapterLabel = MakeLabel(string.Empty, 64f, VisualTheme.Ink);
        rangeLabel = MakeLabel(string.Empty, 30f, VisualTheme.InkSoft);
        rangeLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
        titleBlock.Add(chapterLabel);
        titleBlock.Add(rangeLabel);
        chapterRow.Add(titleBlock);
        nextButton = RoundButton(104f, () => ShowChapter(chapter + 1));
        nextButton.generateVisualContent += ctx => DrawChevron(ctx.painter2D, ctx.visualElement.contentRect, 1f);
        chapterRow.Add(nextButton);
        Add(chapterRow);

        progressLabel = MakeLabel(string.Empty, 30f, VisualTheme.InkSoft);
        progressLabel.style.height = 56f;
        Add(progressLabel);

        grid = new VisualElement { pickingMode = PickingMode.Ignore };
        grid.style.width = gridWidth;
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.marginTop = 12f;
        Add(grid);

        // 아랫줄: 연구소 · 이어하기(또는 최신 판으로)
        var footer = Row(footerHeight);
        footer.style.width = gridWidth;
        footer.style.justifyContent = Justify.SpaceBetween;
        footer.style.marginTop = 28f;
        if (onLab != null)
            footer.Add(PillButton(texts.lab, false, gridWidth * 0.36f, () => onLab()));
        bool atFrontier = currentStage == frontier;
        string primaryText = atFrontier ? texts.continuePlay : texts.latest + " · " + frontier.ToString("D2");
        footer.Add(PillButton(primaryText, true, onLab != null ? gridWidth * 0.6f : gridWidth, () =>
        {
            if (atFrontier)
                Close();
            else
                this.onSelectStage?.Invoke(frontier);
        }));
        Add(footer);

        ShowChapter(ChapterOf(currentStage));
    }

    public static int ChapterOf(int stage) => (Mathf.Max(1, stage) - 1) / StagesPerChapter;

    private int ChapterCount => (finalStage - 1) / StagesPerChapter + 1;

    private void Close() => onClose?.Invoke();

    private void ShowChapter(int index)
    {
        chapter = Mathf.Clamp(index, 0, ChapterCount - 1);
        int first = chapter * StagesPerChapter + 1;
        int last = Mathf.Min(finalStage, first + StagesPerChapter - 1);
        Color chapterColor = VisualTheme.LinkGroupColor(chapter);
        string digit = DigitRows[(chapter + 1) % 10];

        grid.Clear();
        int cleared = 0;
        int stars = 0;
        for (int i = 0; i < StagesPerChapter; i++)
        {
            int stage = first + i;
            int row = i / Columns;
            int column = i % Columns;
            var cell = new VisualElement();
            cell.style.width = cellSize;
            cell.style.height = cellSize;
            cell.style.marginRight = column == Columns - 1 ? 0f : Gap;
            cell.style.marginBottom = row == Rows - 1 ? 0f : Gap;
            float radius = cellSize * 0.2f;
            cell.style.borderTopLeftRadius = radius;
            cell.style.borderTopRightRadius = radius;
            cell.style.borderBottomLeftRadius = radius;
            cell.style.borderBottomRightRadius = radius;
            cell.style.alignItems = Align.Center;
            cell.style.justifyContent = Justify.Center;
            if (stage > last)
            {
                cell.style.visibility = Visibility.Hidden;
                grid.Add(cell);
                continue;
            }

            int stageStars = StageStarsStore.GetStars(stage);
            bool isCleared = stageStars > 0;
            bool unlocked = isUnlocked != null && isUnlocked(stage);
            bool motifOn = column < 3 && digit[row * 3 + column] == '1';
            if (isCleared)
            {
                cleared++;
                stars += stageStars;
            }

            Color background;
            Color textColor;
            if (isCleared)
            {
                background = motifOn ? chapterColor : Color.Lerp(chapterColor, Color.white, 0.74f);
                textColor = motifOn ? Color.white : VisualTheme.Ink;
            }
            else if (unlocked)
            {
                background = Color.white;
                textColor = VisualTheme.Ink;
            }
            else
            {
                background = VisualTheme.PaperShade;
                textColor = new Color(VisualTheme.InkSoft.r, VisualTheme.InkSoft.g, VisualTheme.InkSoft.b, 0.45f);
            }
            cell.style.backgroundColor = background;

            bool isCurrent = stage == currentStage;
            float border = isCurrent ? Mathf.Max(5f, cellSize * 0.035f) : (unlocked && !isCleared ? 2f : 0f);
            Color borderColor = isCurrent ? VisualTheme.Accent : new Color32(0xDC, 0xD3, 0xC6, 0xFF);
            cell.style.borderLeftWidth = border;
            cell.style.borderRightWidth = border;
            cell.style.borderTopWidth = border;
            cell.style.borderBottomWidth = border;
            cell.style.borderLeftColor = borderColor;
            cell.style.borderRightColor = borderColor;
            cell.style.borderTopColor = borderColor;
            cell.style.borderBottomColor = borderColor;

            var number = MakeLabel(stage.ToString(), cellSize * 0.3f, textColor);
            cell.Add(number);
            if (isCleared)
            {
                var starRow = new VisualElement { pickingMode = PickingMode.Ignore };
                starRow.style.width = cellSize * 0.62f;
                starRow.style.height = cellSize * 0.2f;
                int earned = stageStars;
                Color on = motifOn ? Color.white : VisualTheme.Accent;
                Color off = motifOn ? new Color(1f, 1f, 1f, 0.3f) : new Color(0f, 0f, 0f, 0.1f);
                starRow.generateVisualContent += ctx =>
                {
                    Rect r = ctx.visualElement.contentRect;
                    for (int k = 0; k < 3; k++)
                        DrawStar(ctx.painter2D, new Vector2(r.width * (0.18f + k * 0.32f), r.height * 0.5f), r.height * 0.48f, k < earned ? on : off);
                };
                cell.Add(starRow);
            }

            if (unlocked)
            {
                int target = stage;
                cell.pickingMode = PickingMode.Position;
                cell.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    if (target == currentStage)
                        Close();
                    else
                        onSelectStage?.Invoke(target);
                });
            }
            else
            {
                cell.pickingMode = PickingMode.Ignore;
            }
            grid.Add(cell);
        }

        int count = last - first + 1;
        chapterLabel.text = (texts.chapter ?? "{n}").Replace("{n}", (chapter + 1).ToString());
        rangeLabel.text = first + " – " + last;
        bool complete = cleared >= count;
        progressLabel.text = complete ? texts.complete + "  ·  " + stars + " / " + count * 3 : cleared + " / " + count + "  ·  " + stars + " / " + count * 3;
        progressLabel.style.color = complete ? VisualTheme.Accent : VisualTheme.InkSoft;
        totalStarsLabel.text = StageStarsStore.TotalStars().ToString();
        previousButton.style.visibility = chapter > 0 ? Visibility.Visible : Visibility.Hidden;
        nextButton.style.visibility = chapter < ChapterCount - 1 ? Visibility.Visible : Visibility.Hidden;
    }

    private static VisualElement Row(float height)
    {
        var row = new VisualElement { pickingMode = PickingMode.Ignore };
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.height = height;
        row.style.flexShrink = 0f;
        return row;
    }

    private static Label MakeLabel(string text, float size, Color color)
    {
        var label = new Label(text) { pickingMode = PickingMode.Ignore };
        label.AddToClassList("clay-intro-text");
        label.style.fontSize = size;
        label.style.color = color;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
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

    private static VisualElement RoundButton(float size, Action onClick)
    {
        var button = new VisualElement { pickingMode = PickingMode.Position };
        button.style.width = size;
        button.style.height = size;
        button.style.flexShrink = 0f;
        float radius = size * 0.5f;
        button.style.borderTopLeftRadius = radius;
        button.style.borderTopRightRadius = radius;
        button.style.borderBottomLeftRadius = radius;
        button.style.borderBottomRightRadius = radius;
        button.style.backgroundColor = Color.white;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.Center;
        button.RegisterCallback<ClickEvent>(evt =>
        {
            evt.StopPropagation();
            onClick?.Invoke();
        });
        return button;
    }

    private static VisualElement PillButton(string text, bool primary, float width, Action onClick)
    {
        var button = new VisualElement { pickingMode = PickingMode.Position };
        button.style.width = width;
        button.style.height = 116f;
        const float radius = 58f;
        button.style.borderTopLeftRadius = radius;
        button.style.borderTopRightRadius = radius;
        button.style.borderBottomLeftRadius = radius;
        button.style.borderBottomRightRadius = radius;
        button.style.backgroundColor = primary ? VisualTheme.Accent : Color.white;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.Center;
        button.Add(MakeLabel(text, 40f, primary ? Color.white : VisualTheme.Ink));
        button.RegisterCallback<ClickEvent>(evt =>
        {
            evt.StopPropagation();
            onClick?.Invoke();
        });
        return button;
    }

    private static void DrawStar(Painter2D painter, Vector2 center, float radius, Color color)
    {
        painter.fillColor = color;
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

    private static void DrawCross(Painter2D painter, Rect rect)
    {
        float half = rect.width * 0.17f;
        Vector2 c = rect.center;
        painter.strokeColor = VisualTheme.Ink;
        painter.lineWidth = rect.width * 0.075f;
        painter.lineCap = LineCap.Round;
        painter.BeginPath();
        painter.MoveTo(c + new Vector2(-half, -half));
        painter.LineTo(c + new Vector2(half, half));
        painter.MoveTo(c + new Vector2(half, -half));
        painter.LineTo(c + new Vector2(-half, half));
        painter.Stroke();
    }

    private static void DrawChevron(Painter2D painter, Rect rect, float direction)
    {
        float size = rect.width * 0.16f;
        Vector2 c = rect.center + new Vector2(direction * size * 0.25f, 0f);
        painter.strokeColor = VisualTheme.Ink;
        painter.lineWidth = rect.width * 0.075f;
        painter.lineCap = LineCap.Round;
        painter.lineJoin = LineJoin.Round;
        painter.BeginPath();
        painter.MoveTo(c + new Vector2(-direction * size, -size * 1.3f));
        painter.LineTo(c + new Vector2(direction * size * 0.4f, 0f));
        painter.LineTo(c + new Vector2(-direction * size, size * 1.3f));
        painter.Stroke();
    }
}
