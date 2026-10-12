using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A2 소프트 클레이 테마 인트로(네온 인트로와 같은 타이밍·같은 SetTime 방식).
/// 종이 배경 → 점토 타일이 3·2·1·0으로 줄어듦(게임 규칙, 숫자 색 = 남은 횟수) → 로고의 "O" 자리로 옮겨가 툭 내려앉음
/// → Z·E·R가 위에서 떨어져 찰흙처럼 눌렸다 펴짐 → STEP이 아래에서 튀어 오름 → 부제.
/// 로고 글자는 Resources/Intro/intro_clay_* 이미지, 타일·먼지는 UI Toolkit 요소와 벡터 드로잉이다.
/// </summary>
public sealed class ClayIntroElement : VisualElement
{
    public const float ContentDuration = NeonIntroElement.ContentDuration;

    private const float CountStart = 0.22f;
    private const float CountStep = 0.24f;
    private const float FlyStart = CountStart + CountStep * 3f;
    private const float FlyDuration = 0.22f;
    private const float LandTime = FlyStart + FlyDuration;

    // 로고 원본(1145×722) 기준 레이어 위치
    private const float LogoWidth = 1145f;
    private const float LogoHeight = 722f;
    private static readonly string[] ZerNames = { "intro_clay_z", "intro_clay_e1", "intro_clay_r" };
    private static readonly Rect[] ZerRects = { new Rect(0f, 0f, 300f, 352f), new Rect(300f, 0f, 245f, 352f), new Rect(545f, 0f, 269f, 352f) };
    private static readonly string[] StepNames = { "intro_clay_s", "intro_clay_t", "intro_clay_e2", "intro_clay_p" };
    private static readonly Rect[] StepRects = { new Rect(0f, 352f, 298f, 370f), new Rect(298f, 352f, 287f, 370f), new Rect(585f, 352f, 259f, 370f), new Rect(844f, 352f, 301f, 370f) };
    private static readonly Rect TileRect = new Rect(814f, 0f, 331f, 352f);
    // 타일 이미지 안에서 점토 사각형이 차지하는 영역(비율)
    private static readonly Rect TileInner = new Rect(0.03f, 0.06f, 0.94f, 0.9f);
    private static readonly Color LogoTileSage = new Color32(0x8F, 0xAE, 0x7A, 0xFF);

    private readonly VisualElement fxLayer;
    private readonly VisualElement logoRoot;
    private readonly VisualElement[] zerLetters = new VisualElement[3];
    private readonly VisualElement[] stepLetters = new VisualElement[4];
    private readonly VisualElement logoTile;
    private readonly VisualElement countTile;
    private readonly Label countLabel;
    private readonly Label subtitleLabel;
    private readonly List<Vector3> dustSeeds = new List<Vector3>();
    private float time;
    private int lastCount = -1;

    public ClayIntroElement()
    {
        name = "ClayIntro";
        pickingMode = PickingMode.Ignore;
        Stretch(this);
        style.backgroundColor = VisualTheme.Paper;

        fxLayer = NewLayer();
        Stretch(fxLayer);
        fxLayer.generateVisualContent += DrawEffects;
        Add(fxLayer);

        logoRoot = NewLayer();
        logoRoot.style.position = Position.Absolute;
        Add(logoRoot);
        for (int i = 0; i < zerLetters.Length; i++)
            zerLetters[i] = AddLogoLayer(ZerNames[i], ZerRects[i]);
        logoTile = AddLogoLayer("intro_clay_tile", TileRect);
        for (int i = 0; i < stepLetters.Length; i++)
            stepLetters[i] = AddLogoLayer(StepNames[i], StepRects[i]);

        // 카운트다운 점토 타일(게임 타일과 같은 그림, 남은 횟수 색으로 칠함)
        countTile = NewLayer();
        countTile.style.position = Position.Absolute;
        Texture2D clayTile = Resources.Load<Texture2D>("Sprites/Clay/clay_tile");
        if (clayTile != null)
            countTile.style.backgroundImage = new StyleBackground(clayTile);
        countTile.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(92));
        countLabel = new Label("3") { pickingMode = PickingMode.Ignore };
        countLabel.AddToClassList("clay-intro-text");
        Stretch(countLabel);
        countLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        countLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        countLabel.style.color = VisualTheme.Ink;
        countLabel.style.paddingBottom = Length.Percent(6);
        countTile.Add(countLabel);
        Add(countTile);

        subtitleLabel = new Label("A CALM NUMBER PUZZLE") { pickingMode = PickingMode.Ignore };
        subtitleLabel.AddToClassList("clay-intro-text");
        subtitleLabel.style.position = Position.Absolute;
        subtitleLabel.style.left = 0f;
        subtitleLabel.style.right = 0f;
        subtitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        subtitleLabel.style.color = VisualTheme.InkSoft;
        Add(subtitleLabel);

        var random = new System.Random(5);
        for (int i = 0; i < 10; i++)
            dustSeeds.Add(new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()));

        RegisterCallback<GeometryChangedEvent>(_ => ApplyLayout());
        SetTime(0f);
    }

    public void SetTime(float seconds)
    {
        time = Mathf.Max(0f, seconds);
        Animate();
        fxLayer.MarkDirtyRepaint();
    }

    private void ApplyLayout()
    {
        Rect r = contentRect;
        if (r.width <= 1f || r.height <= 1f)
            return;
        float logoWidth = Mathf.Min(r.width * 0.84f, r.height * 0.3f * LogoWidth / LogoHeight);
        float logoHeight = logoWidth * LogoHeight / LogoWidth;
        logoRoot.style.width = logoWidth;
        logoRoot.style.height = logoHeight;
        logoRoot.style.left = (r.width - logoWidth) * 0.5f;
        logoRoot.style.top = r.height * 0.4f - logoHeight * 0.5f;
        subtitleLabel.style.top = r.height * 0.4f + logoHeight * 0.5f + logoWidth * 0.05f;
        subtitleLabel.style.fontSize = Mathf.Max(18f, logoWidth * 0.034f);
        subtitleLabel.style.letterSpacing = logoWidth * 0.006f;
        Animate();
        fxLayer.MarkDirtyRepaint();
    }

    private void Animate()
    {
        Rect r = contentRect;
        Rect logo = new Rect(logoRoot.resolvedStyle.left, logoRoot.resolvedStyle.top, logoRoot.resolvedStyle.width, logoRoot.resolvedStyle.height);
        bool hasLayout = r.width > 1f && logo.width > 1f && !float.IsNaN(logo.width);

        // 카운트다운 타일
        int count = CountAt(time);
        float fade = 1f - Phase(LandTime - 0.04f, 0.08f);
        if (count >= 0 && hasLayout && fade > 0f)
        {
            Rect tile = CountdownTileRect(r, logo);
            countTile.style.display = DisplayStyle.Flex;
            countTile.style.left = tile.x;
            countTile.style.top = tile.y;
            countTile.style.width = tile.width;
            countTile.style.height = tile.height;
            countTile.style.opacity = Mathf.Clamp01(Phase(CountStart, 0.1f)) * fade;
            float fly = Phase(FlyStart, FlyDuration);
            Color clay = count > 0 ? VisualTheme.ClayColorForCount(count) : LogoTileSage;
            if (count > 0 && fly <= 0f)
                clay = Color.Lerp(VisualTheme.ClayColorForCount(Mathf.Min(3, count + 1)), clay, Phase(CountChangeTime(count), 0.08f));
            countTile.style.unityBackgroundImageTintColor = clay;
            // 숫자가 줄 때마다 찰흙처럼 눌렸다 펴짐
            float since = time - CountChangeTime(count);
            float squash = since >= 0f && since < 0.22f ? Mathf.Sin(since / 0.22f * Mathf.PI) * 0.12f : 0f;
            countTile.style.scale = new Scale(new Vector3(1f + squash, 1f - squash, 1f));
            countLabel.style.fontSize = tile.height * 0.5f;
            countLabel.style.opacity = 1f - Phase(FlyStart + 0.05f, 0.1f);
            if (count != lastCount)
            {
                countLabel.text = count.ToString();
                lastCount = count;
            }
        }
        else
        {
            countTile.style.display = DisplayStyle.None;
        }

        // 로고 타일: 날아온 타일과 교차, 착지 순간 눌렸다 펴짐
        logoTile.style.opacity = Phase(LandTime - 0.06f, 0.08f);
        logoTile.style.scale = SquashScale(time - LandTime, 0.16f);

        // Z·E·R: 위에서 떨어져 바닥에 닿으며 눌림
        for (int i = 0; i < zerLetters.Length; i++)
        {
            float start = LandTime + 0.04f + i * 0.07f;
            float p = Phase(start, 0.2f);
            zerLetters[i].style.opacity = Mathf.Clamp01(p * 4f);
            float drop = 1f - EaseInQuad(p);
            zerLetters[i].style.translate = new Translate(0f, Length.Percent(-55f * drop));
            zerLetters[i].style.scale = SquashScale(time - (start + 0.2f), 0.13f);
        }

        // STEP: 아래에서 튀어 오름
        for (int i = 0; i < stepLetters.Length; i++)
        {
            float start = LandTime + 0.28f + i * 0.06f;
            float p = Phase(start, 0.3f);
            stepLetters[i].style.opacity = Mathf.Clamp01(p * 3f);
            float rise = EaseOutBack(p);
            stepLetters[i].style.translate = new Translate(0f, Length.Percent((1f - rise) * 30f));
        }

        float sub = EaseOutCubic(Phase(LandTime + 0.7f, 0.35f));
        subtitleLabel.style.opacity = sub;
    }

    private void DrawEffects(MeshGenerationContext context)
    {
        Rect r = contentRect;
        Rect logo = logoRoot.layout;
        if (r.width <= 1f || logo.width <= 1f)
            return;
        float since = time - LandTime;
        if (since < 0f || since > 0.5f)
            return;
        // 착지 순간 타일 아래에서 퍼지는 작은 먼지
        Rect target = LogoTileTarget(logo);
        Vector2 basePoint = new Vector2(target.center.x, target.yMax);
        Painter2D painter = context.painter2D;
        float p = since / 0.5f;
        for (int i = 0; i < dustSeeds.Count; i++)
        {
            Vector3 seed = dustSeeds[i];
            float side = i % 2 == 0 ? -1f : 1f;
            float dist = target.width * (0.35f + seed.x * 0.45f) * EaseOutCubic(p);
            Vector2 pos = basePoint + new Vector2(side * dist, -target.height * 0.08f * seed.y * Mathf.Sin(p * Mathf.PI));
            Color c = VisualTheme.SlotTint;
            c.a = 0.35f * (1f - p);
            painter.fillColor = c;
            painter.BeginPath();
            painter.Arc(pos, target.width * (0.02f + seed.z * 0.03f) * (1f - p * 0.5f), 0f, 360f);
            painter.Fill();
        }
    }

    private Rect CountdownTileRect(Rect r, Rect logo)
    {
        float size = r.width * 0.32f;
        Rect start = new Rect(r.width * 0.5f - size * 0.5f, logo.center.y - size * 0.5f, size, size);
        Rect end = LogoTileTarget(logo);
        // 점토 타일 스프라이트(512px)는 윗면이 4~91% 영역이라 로고 타일 크기에 맞춰 조금 키운다.
        end = new Rect(end.x - end.width * 0.05f, end.y - end.height * 0.04f, end.width * 1.1f, end.height * 1.1f);
        float fly = EaseInOutCubic(Phase(FlyStart, FlyDuration));
        float arc = Mathf.Sin(fly * Mathf.PI) * size * 0.25f;
        Rect rect = new Rect(
            Mathf.Lerp(start.x, end.x, fly),
            Mathf.Lerp(start.y, end.y, fly) - arc,
            Mathf.Lerp(start.width, end.width, fly),
            Mathf.Lerp(start.height, end.height, fly));
        float appear = Mathf.Lerp(0.6f, 1f, EaseOutBack(Phase(CountStart, 0.2f)));
        if (appear < 1f)
        {
            float shrink = rect.width * (1f - appear) * 0.5f;
            rect = new Rect(rect.x + shrink, rect.y + shrink, rect.width - shrink * 2f, rect.height - shrink * 2f);
        }
        return rect;
    }

    private static Rect LogoTileTarget(Rect logo)
    {
        float sx = logo.width / LogoWidth;
        float sy = logo.height / LogoHeight;
        Rect crop = new Rect(logo.x + TileRect.x * sx, logo.y + TileRect.y * sy, TileRect.width * sx, TileRect.height * sy);
        return new Rect(crop.x + crop.width * TileInner.x, crop.y + crop.height * TileInner.y, crop.width * TileInner.width, crop.height * TileInner.height);
    }

    /// <summary>착지 직후 찰흙처럼 눌렸다(가로로 퍼짐) 펴지는 크기. since &lt; 0이면 그대로.</summary>
    private static Scale SquashScale(float since, float duration)
    {
        if (since < 0f || since > duration)
            return new Scale(Vector3.one);
        float k = Mathf.Sin(since / duration * Mathf.PI) * 0.14f;
        return new Scale(new Vector3(1f + k, 1f - k, 1f));
    }

    private static int CountAt(float t)
    {
        if (t < CountStart)
            return -1;
        int step = Mathf.FloorToInt((t - CountStart) / CountStep);
        return Mathf.Max(0, 3 - step);
    }

    private static float CountChangeTime(int count) => CountStart + (3 - Mathf.Clamp(count, 0, 3)) * CountStep;

    private VisualElement AddLogoLayer(string resourceName, Rect source)
    {
        var layer = NewLayer();
        layer.style.position = Position.Absolute;
        layer.style.left = Length.Percent(source.x / LogoWidth * 100f);
        layer.style.top = Length.Percent(source.y / LogoHeight * 100f);
        layer.style.width = Length.Percent(source.width / LogoWidth * 100f);
        layer.style.height = Length.Percent(source.height / LogoHeight * 100f);
        // 눌림은 바닥 기준으로 일어나게 한다.
        layer.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(95));
        Texture2D texture = Resources.Load<Texture2D>("Intro/" + resourceName);
        if (texture != null)
            layer.style.backgroundImage = new StyleBackground(texture);
        logoRoot.Add(layer);
        return layer;
    }

    private static VisualElement NewLayer() => new VisualElement { pickingMode = PickingMode.Ignore };

    private static void Stretch(VisualElement element)
    {
        element.style.position = Position.Absolute;
        element.style.left = 0f;
        element.style.top = 0f;
        element.style.right = 0f;
        element.style.bottom = 0f;
    }

    private float Phase(float start, float duration) => Mathf.Clamp01((time - start) / Mathf.Max(0.0001f, duration));

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        return 1f - u * u * u;
    }

    private static float EaseInQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t;
    }

    private static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
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
