using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 앱 시작 인트로 모션그래픽.
/// 어둠 속 수평선 점등 → 게임 타일이 3·2·1·0으로 줄어듦(게임 규칙) → 0 타일이 로고의 "O" 자리로 날아가 착지(플래시·충격파)
/// → 타일에서 번진 빛으로 R·E·Z 순서로 네온 점등 → STEP이 한 글자씩 솟아오름 → 부제 → 로고 위 광택 스윕.
/// 로고 글자와 배경은 Resources/Intro 이미지, 나머지(수평선·타일·링·입자)는 UI Toolkit 벡터 드로잉이다.
/// SetTime으로 시간을 넣어 구동하며, 같은 시간에는 항상 같은 화면을 그린다(테스트·캡처 재현성).
/// </summary>
public sealed class NeonIntroElement : VisualElement
{
    public const float ContentDuration = 2.5f;

    // 타이밍(초)
    private const float StreakStart = 0.05f;
    private const float CountStart = 0.22f;
    private const float CountStep = 0.24f;
    private const float FlyStart = CountStart + CountStep * 3f;
    private const float FlyDuration = 0.2f;
    private const float LandTime = FlyStart + FlyDuration;
    private const float ShineStart = 1.85f;

    // 로고 원본(1060×580) 기준 레이어 위치
    private const float LogoWidth = 1060f;
    private const float LogoHeight = 580f;
    private static readonly string[] ZerNames = { "intro_z", "intro_e1", "intro_r" };
    private static readonly Rect[] ZerRects = { new Rect(0f, 0f, 274f, 318f), new Rect(274f, 0f, 229f, 318f), new Rect(503f, 0f, 237f, 318f) };
    private static readonly string[] StepNames = { "intro_s", "intro_t", "intro_e2", "intro_p" };
    private static readonly Rect[] StepRects = { new Rect(0f, 318f, 318f, 262f), new Rect(318f, 318f, 219f, 262f), new Rect(537f, 318f, 216f, 262f), new Rect(753f, 318f, 307f, 262f) };
    private static readonly Rect TileRect = new Rect(740f, 0f, 320f, 318f);
    // 타일 이미지 안에서 실제 둥근 사각형 테두리가 차지하는 영역(비율)
    private static readonly Rect TileInner = new Rect(0.05f, 0.12f, 0.83f, 0.8f);

    // 배경 이미지(1024×1536)에서 수평선 높이
    private const float BackgroundAspect = 1024f / 1536f;
    private const float BackgroundHorizon = 945f / 1536f;

    private static readonly Color Mint = new Color(0.45f, 1f, 0.85f, 1f);
    private static readonly Color Cyan = new Color(0.27f, 0.86f, 1f, 1f);
    private static readonly Color Pink = new Color(1f, 0.3f, 0.78f, 1f);
    // 게임 숫자 색(테두리, 숫자): 3 노랑/주황, 2 청록/초록, 1 흰색/하늘
    private static readonly Color[] CountBorder = { new Color(0.92f, 0.98f, 1f, 1f), new Color(0.35f, 0.95f, 1f, 1f), new Color(1f, 0.86f, 0.45f, 1f) };
    private static readonly Color[] CountDigit = { new Color(0.4f, 0.88f, 1f, 1f), new Color(0.35f, 1f, 0.55f, 1f), new Color(1f, 0.58f, 0.22f, 1f) };

    private readonly VisualElement background;
    private readonly VisualElement fxLayer;
    private readonly VisualElement logoRoot;
    private readonly VisualElement[] zerLetters = new VisualElement[3];
    private readonly VisualElement[] stepLetters = new VisualElement[4];
    private readonly VisualElement tileImage;
    private readonly VisualElement[] shineBands = new VisualElement[3];
    private readonly VisualElement[] shineImages = new VisualElement[3];
    private readonly Label countLabel;
    private readonly Label subtitleLabel;
    private readonly VisualElement flash;
    private readonly List<Vector3> particleSeeds = new List<Vector3>();
    private float time;
    private int lastCountShown = -1;

    public NeonIntroElement()
    {
        name = "NeonIntro";
        pickingMode = PickingMode.Ignore;
        Stretch(this);
        style.backgroundColor = new Color(0.004f, 0.006f, 0.014f, 1f);

        background = NewLayer();
        Stretch(background);
        SetImage(background, "intro_bg");
        background.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
        background.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
        background.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
        Add(background);

        fxLayer = NewLayer();
        Stretch(fxLayer);
        fxLayer.generateVisualContent += DrawEffects;
        Add(fxLayer);

        logoRoot = NewLayer();
        logoRoot.style.position = Position.Absolute;
        Add(logoRoot);
        for (int i = 0; i < zerLetters.Length; i++)
            zerLetters[i] = AddLogoLayer(ZerNames[i], ZerRects[i]);
        tileImage = AddLogoLayer("intro_tile", TileRect);
        for (int i = 0; i < stepLetters.Length; i++)
            stepLetters[i] = AddLogoLayer(StepNames[i], StepRects[i]);

        // 광택: 흰 마스크 이미지를 좁은 띠(overflow hidden) 안에서만 보여 주고 띠를 가로로 이동시킨다.
        // 폭이 다른 띠 3개를 겹쳐 가장자리를 부드럽게 만든다.
        for (int i = 0; i < shineBands.Length; i++)
        {
            var band = NewLayer();
            band.style.position = Position.Absolute;
            band.style.top = 0f;
            band.style.bottom = 0f;
            band.style.overflow = Overflow.Hidden;
            var image = NewLayer();
            image.style.position = Position.Absolute;
            image.style.top = 0f;
            SetImage(image, "intro_shine");
            band.Add(image);
            logoRoot.Add(band);
            shineBands[i] = band;
            shineImages[i] = image;
        }

        countLabel = new Label("3") { pickingMode = PickingMode.Ignore };
        countLabel.AddToClassList("neon-intro-text");
        countLabel.style.position = Position.Absolute;
        countLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        countLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        countLabel.style.paddingLeft = 0f;
        countLabel.style.paddingRight = 0f;
        countLabel.style.paddingTop = 0f;
        countLabel.style.paddingBottom = 0f;
        countLabel.style.marginLeft = 0f;
        countLabel.style.marginTop = 0f;
        Add(countLabel);

        subtitleLabel = new Label("A NEON PUZZLE GAME") { pickingMode = PickingMode.Ignore };
        subtitleLabel.AddToClassList("neon-intro-text");
        subtitleLabel.style.position = Position.Absolute;
        subtitleLabel.style.left = 0f;
        subtitleLabel.style.right = 0f;
        subtitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        subtitleLabel.style.color = new Color(0.86f, 0.94f, 1f, 1f);
        subtitleLabel.style.textShadow = new TextShadow { offset = Vector2.zero, blurRadius = 10f, color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f) };
        Add(subtitleLabel);

        flash = NewLayer();
        Stretch(flash);
        flash.style.backgroundColor = new Color(0.75f, 0.95f, 1f, 1f);
        Add(flash);

        var random = new System.Random(11);
        for (int i = 0; i < 36; i++)
            particleSeeds.Add(new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()));

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
        // 가로 86%, 단 로고 높이가 화면 높이의 26%를 넘지 않게(가로가 넓은 화면 대비)
        float logoWidth = Mathf.Min(r.width * 0.86f, r.height * 0.26f * LogoWidth / LogoHeight);
        float logoHeight = logoWidth * LogoHeight / LogoWidth;
        logoRoot.style.width = logoWidth;
        logoRoot.style.height = logoHeight;
        logoRoot.style.left = (r.width - logoWidth) * 0.5f;
        logoRoot.style.top = r.height * 0.33f - logoHeight * 0.5f;
        subtitleLabel.style.top = r.height * 0.33f + logoHeight * 0.5f + logoWidth * 0.04f;
        subtitleLabel.style.fontSize = Mathf.Max(18f, logoWidth * 0.036f);
        for (int i = 0; i < shineImages.Length; i++)
        {
            shineImages[i].style.width = logoWidth;
            shineImages[i].style.height = logoHeight;
        }
        Animate();
        fxLayer.MarkDirtyRepaint();
    }

    private void Animate()
    {
        Rect r = contentRect;
        Rect logo = new Rect(logoRoot.resolvedStyle.left, logoRoot.resolvedStyle.top, logoRoot.resolvedStyle.width, logoRoot.resolvedStyle.height);
        bool hasLayout = r.width > 1f && logo.width > 1f && !float.IsNaN(logo.width);

        // 배경: 카운트다운 동안 어둡게, 착지 순간 불이 켜지며 천천히 줌아웃
        float dim = Phase(0.1f, 0.5f) * 0.32f;
        float lit = EaseOutCubic(Phase(LandTime, 0.45f));
        background.style.opacity = Mathf.Lerp(dim, 1f, lit);
        float zoom = Mathf.Lerp(1.16f, 1f, EaseOutCubic(Phase(0f, ContentDuration)));
        background.style.scale = new Scale(new Vector3(zoom, zoom, 1f));

        // 착지 플래시와 흔들림
        float sinceLand = time - LandTime;
        // 선형 색 공간에서는 흰 오버레이가 훨씬 밝게 보여 약하고 짧게 준다.
        flash.style.opacity = sinceLand >= 0f ? 0.09f * Mathf.Exp(-sinceLand * 30f) : 0f;
        float shake = sinceLand >= 0f ? Mathf.Exp(-sinceLand * 12f) * 10f : 0f;
        logoRoot.style.translate = new Translate(Mathf.Sin(sinceLand * 90f) * shake, Mathf.Cos(sinceLand * 77f) * shake * 0.6f);

        // 타일 이미지: 날아오는 마지막 구간에 벡터 타일과 교차
        float tileIn = Phase(LandTime - 0.08f, 0.1f);
        tileImage.style.opacity = tileIn;

        // R → E → Z 순서로 네온 점등(타일에서 빛이 번지는 방향)
        for (int i = 0; i < zerLetters.Length; i++)
        {
            float start = LandTime + 0.03f + (zerLetters.Length - 1 - i) * 0.07f;
            float t = time - start;
            float alpha;
            if (t < 0f) alpha = 0f;
            else if (t < 0.035f) alpha = 0.85f;
            else if (t < 0.06f) alpha = 0.15f;
            else if (t < 0.09f) alpha = 0.7f;
            else if (t < 0.11f) alpha = 0.3f;
            else alpha = 1f;
            zerLetters[i].style.opacity = alpha;
            float s = t < 0f ? 1.1f : Mathf.Lerp(1.1f, 1f, EaseOutCubic(Mathf.Clamp01(t / 0.2f)));
            zerLetters[i].style.scale = new Scale(new Vector3(s, s, 1f));
        }

        // STEP: 한 글자씩 아래에서 솟아오른다
        for (int i = 0; i < stepLetters.Length; i++)
        {
            float p = Phase(LandTime + 0.24f + i * 0.06f, 0.3f);
            stepLetters[i].style.opacity = Mathf.Clamp01(p * 2.5f);
            float rise = EaseOutBack(p);
            stepLetters[i].style.translate = new Translate(0f, Length.Percent((1f - rise) * 28f));
        }

        // 부제
        float sub = EaseOutCubic(Phase(LandTime + 0.6f, 0.35f));
        subtitleLabel.style.opacity = sub * 0.9f;
        subtitleLabel.style.letterSpacing = Mathf.Lerp(2f, 9f, sub);

        // 광택 스윕
        float shine = Phase(ShineStart, 0.42f);
        float[] bandWidths = { 0.22f, 0.13f, 0.06f };
        float[] bandAlphas = { 0.16f, 0.22f, 0.45f };
        for (int i = 0; i < shineBands.Length; i++)
        {
            bool active = shine > 0f && shine < 1f && hasLayout;
            shineBands[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            if (!active)
                continue;
            float center = Mathf.Lerp(-0.15f, 1.15f, EaseInOutCubic(shine)) * logo.width;
            float width = bandWidths[i] * logo.width;
            float left = center - width * 0.5f;
            shineBands[i].style.left = left;
            shineBands[i].style.width = width;
            shineImages[i].style.left = -left;
            shineBands[i].style.opacity = bandAlphas[i] * Mathf.Sin(shine * Mathf.PI);
        }

        // 카운트다운 숫자
        int count = CountAt(time);
        if (count >= 0 && hasLayout)
        {
            Rect tile = CountdownTileRect(r, logo);
            countLabel.style.display = DisplayStyle.Flex;
            float pop = 1f + 0.35f * Mathf.Exp(-(time - CountChangeTime(count)) * 14f);
            countLabel.style.left = tile.x;
            countLabel.style.top = tile.y;
            countLabel.style.width = tile.width;
            countLabel.style.height = tile.height;
            countLabel.style.fontSize = tile.height * 0.56f;
            countLabel.style.scale = new Scale(new Vector3(pop, pop, 1f));
            Color digit = count > 0 ? CountDigit[count - 1] : new Color(0.75f, 0.8f, 0.9f, 1f);
            countLabel.style.color = digit;
            countLabel.style.textShadow = new TextShadow { offset = Vector2.zero, blurRadius = tile.height * 0.08f, color = new Color(digit.r, digit.g, digit.b, 0.9f) };
            countLabel.style.opacity = TileAppear() * (1f - Phase(FlyStart + 0.04f, 0.1f));
            if (count != lastCountShown)
            {
                countLabel.text = count.ToString();
                lastCountShown = count;
            }
        }
        else
        {
            countLabel.style.display = DisplayStyle.None;
        }
    }

    private void DrawEffects(MeshGenerationContext context)
    {
        Rect r = contentRect;
        if (r.width <= 1f || r.height <= 1f)
            return;
        Painter2D painter = context.painter2D;
        painter.lineCap = LineCap.Round;
        painter.lineJoin = LineJoin.Round;
        Rect logo = logoRoot.layout;
        float horizonY = HorizonY(r);

        DrawHorizonStreak(painter, r, horizonY);

        if (logo.width > 1f)
        {
            DrawCountdownTile(painter, r, logo);
            DrawShockwave(painter, r, logo);
        }
        DrawParticles(painter, r, horizonY);
    }

    private void DrawHorizonStreak(Painter2D painter, Rect r, float y)
    {
        float grow = EaseOutCubic(Phase(StreakStart, 0.4f));
        if (grow <= 0f)
            return;
        float half = r.width * 0.5f * grow;
        float settle = Mathf.Lerp(1f, 0.35f, Phase(LandTime, 0.6f));
        float landBoost = time >= LandTime ? 1f + 1.2f * Mathf.Exp(-(time - LandTime) * 6f) : 1f;
        float cx = r.width * 0.5f;
        float[] widths = { 30f, 12f, 3.5f };
        float[] alphas = { 0.09f, 0.25f, 0.95f };
        for (int i = 0; i < widths.Length; i++)
        {
            // 가운데는 하늘색, 양끝으로 갈수록 왼쪽 민트·오른쪽 분홍
            DrawLine(painter, new Vector2(cx - half, y), new Vector2(cx, y), widths[i] * landBoost, Mint, Cyan, alphas[i] * settle);
            DrawLine(painter, new Vector2(cx, y), new Vector2(cx + half, y), widths[i] * landBoost, Cyan, Pink, alphas[i] * settle);
        }
    }

    private void DrawCountdownTile(Painter2D painter, Rect r, Rect logo)
    {
        float appear = TileAppear();
        float fade = 1f - Phase(LandTime - 0.06f, 0.08f);
        if (appear <= 0f || fade <= 0f)
            return;
        Rect tile = CountdownTileRect(r, logo);
        int count = Mathf.Max(0, CountAt(time));
        Color border = count > 0 ? CountBorder[count - 1] : Color.Lerp(Cyan, Pink, 0.5f);
        float fly = Phase(FlyStart, FlyDuration);
        border = Color.Lerp(border, Color.Lerp(Cyan, Pink, 0.55f), fly);
        float alpha = appear * fade;
        float radius = tile.width * 0.2f;

        // 어두운 유리 안쪽
        painter.fillColor = new Color(0.02f, 0.04f, 0.07f, 0.85f * alpha);
        RoundedRectPath(painter, tile, radius);
        painter.Fill();

        float[] widths = { 0.2f, 0.1f, 0.045f, 0.018f };
        float[] alphas = { 0.07f, 0.18f, 0.6f, 1f };
        for (int i = 0; i < widths.Length; i++)
        {
            Color c = i == widths.Length - 1 ? Color.Lerp(border, Color.white, 0.55f) : border;
            c.a = alphas[i] * alpha;
            painter.strokeColor = c;
            painter.lineWidth = Mathf.Max(1.5f, tile.width * widths[i]);
            RoundedRectPath(painter, tile, radius);
            painter.Stroke();
        }

        // 숫자가 줄어들 때마다 바깥으로 퍼지는 고리
        for (int k = 3; k >= 0; k--)
        {
            float since = time - CountChangeTime(k);
            if (k == 3 || since < 0f || since > 0.2f)
                continue;
            float p = since / 0.2f;
            Rect ring = Inflate(tile, tile.width * 0.45f * EaseOutCubic(p));
            Color c = k > 0 ? CountBorder[k - 1] : Cyan;
            c.a = (1f - p) * (1f - p) * 0.6f * alpha;
            painter.strokeColor = c;
            painter.lineWidth = Mathf.Lerp(tile.width * 0.05f, 1.5f, p);
            RoundedRectPath(painter, ring, radius + tile.width * 0.2f * p);
            painter.Stroke();
        }
    }

    private void DrawShockwave(Painter2D painter, Rect r, Rect logo)
    {
        float since = time - LandTime;
        if (since < 0f || since > 0.6f)
            return;
        float p = since / 0.6f;
        Rect target = LogoTileTarget(logo);
        Vector2 center = target.center;
        float radius = Mathf.Lerp(target.width * 0.5f, r.width * 0.95f, EaseOutCubic(p));
        float[] widths = { 34f, 14f, 4f };
        float[] alphas = { 0.12f, 0.3f, 0.95f };
        for (int i = 0; i < widths.Length; i++)
        {
            Color c = Color.Lerp(Cyan, Pink, p);
            c.a = alphas[i] * (1f - p) * (1f - p);
            painter.strokeColor = c;
            painter.lineWidth = widths[i] * (1f - p * 0.7f);
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Stroke();
        }
    }

    private void DrawParticles(Painter2D painter, Rect r, float horizonY)
    {
        float appear = Phase(LandTime, 0.3f);
        if (appear <= 0f)
            return;
        for (int i = 0; i < particleSeeds.Count; i++)
        {
            Vector3 seed = particleSeeds[i];
            float speed = 0.22f + seed.z * 0.35f;
            float life = Mathf.Repeat((time - LandTime) * speed + seed.y, 1f);
            float x = r.width * seed.x + Mathf.Sin(time * 1.7f + i * 1.3f) * 8f;
            float y = Mathf.Lerp(horizonY + r.height * 0.03f, horizonY - r.height * 0.3f, life);
            float alpha = Mathf.Sin(life * Mathf.PI) * 0.7f * appear;
            Color c = seed.x < 0.5f ? Color.Lerp(Mint, Cyan, seed.z) : Color.Lerp(Cyan, Pink, seed.z);
            float size = 1.2f + seed.z * 1.8f;
            c.a = alpha * 0.18f;
            painter.fillColor = c;
            painter.BeginPath();
            painter.Arc(new Vector2(x, y), size * 2.2f, 0f, 360f);
            painter.Fill();
            c.a = alpha;
            painter.fillColor = c;
            painter.BeginPath();
            painter.Arc(new Vector2(x, y), size, 0f, 360f);
            painter.Fill();
        }
    }

    private Rect CountdownTileRect(Rect r, Rect logo)
    {
        float size = r.width * 0.3f;
        Rect start = new Rect(r.width * 0.5f - size * 0.5f, logo.center.y - size * 0.5f, size, size);
        Rect end = LogoTileTarget(logo);
        float fly = EaseInOutCubic(Phase(FlyStart, FlyDuration));
        float appearScale = Mathf.Lerp(0.55f, 1f, EaseOutBack(Phase(CountStart, 0.2f)));
        Rect rect = new Rect(
            Mathf.Lerp(start.x, end.x, fly),
            Mathf.Lerp(start.y, end.y, fly),
            Mathf.Lerp(start.width, end.width, fly),
            Mathf.Lerp(start.height, end.height, fly));
        if (appearScale < 1f)
            rect = Inflate(rect, -rect.width * (1f - appearScale) * 0.5f);
        return rect;
    }

    private static Rect LogoTileTarget(Rect logo)
    {
        float sx = logo.width / LogoWidth;
        float sy = logo.height / LogoHeight;
        Rect crop = new Rect(logo.x + TileRect.x * sx, logo.y + TileRect.y * sy, TileRect.width * sx, TileRect.height * sy);
        return new Rect(crop.x + crop.width * TileInner.x, crop.y + crop.height * TileInner.y, crop.width * TileInner.width, crop.height * TileInner.height);
    }

    private float HorizonY(Rect r)
    {
        // 배경은 cover로 그려지고 중앙 기준으로 줌된다.
        float scale = Mathf.Max(r.width / BackgroundAspect, r.height);
        float top = (r.height - scale) * 0.5f;
        float y = top + scale * BackgroundHorizon;
        float zoom = Mathf.Lerp(1.16f, 1f, EaseOutCubic(Phase(0f, ContentDuration)));
        return r.height * 0.5f + (y - r.height * 0.5f) * zoom;
    }

    /// <summary>현재 보여 줄 숫자(3·2·1·0). 타일이 나오기 전이면 -1.</summary>
    private static int CountAt(float t)
    {
        if (t < CountStart)
            return -1;
        int step = Mathf.FloorToInt((t - CountStart) / CountStep);
        return Mathf.Max(0, 3 - step);
    }

    private static float CountChangeTime(int count) => CountStart + (3 - count) * CountStep;

    private float TileAppear() => Mathf.Clamp01(Phase(CountStart, 0.12f));

    private VisualElement AddLogoLayer(string resourceName, Rect source)
    {
        var layer = NewLayer();
        layer.style.position = Position.Absolute;
        layer.style.left = Length.Percent(source.x / LogoWidth * 100f);
        layer.style.top = Length.Percent(source.y / LogoHeight * 100f);
        layer.style.width = Length.Percent(source.width / LogoWidth * 100f);
        layer.style.height = Length.Percent(source.height / LogoHeight * 100f);
        SetImage(layer, resourceName);
        logoRoot.Add(layer);
        return layer;
    }

    private static VisualElement NewLayer()
    {
        return new VisualElement { pickingMode = PickingMode.Ignore };
    }

    private static void Stretch(VisualElement element)
    {
        element.style.position = Position.Absolute;
        element.style.left = 0f;
        element.style.top = 0f;
        element.style.right = 0f;
        element.style.bottom = 0f;
    }

    private static void SetImage(VisualElement element, string resourceName)
    {
        Texture2D texture = Resources.Load<Texture2D>("Intro/" + resourceName);
        if (texture != null)
            element.style.backgroundImage = new StyleBackground(texture);
    }

    private static void DrawLine(Painter2D painter, Vector2 from, Vector2 to, float width, Color fromColor, Color toColor, float alpha)
    {
        const int segments = 8;
        painter.lineWidth = width;
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments;
            float b = (i + 1) / (float)segments;
            Color c = Color.Lerp(fromColor, toColor, (a + b) * 0.5f);
            c.a = alpha;
            painter.strokeColor = c;
            painter.BeginPath();
            painter.MoveTo(Vector2.Lerp(from, to, a));
            painter.LineTo(Vector2.Lerp(from, to, b));
            painter.Stroke();
        }
    }

    private static void RoundedRectPath(Painter2D painter, Rect rect, float radius)
    {
        radius = Mathf.Clamp(radius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
        float x = rect.x, y = rect.y, w = rect.width, h = rect.height;
        painter.BeginPath();
        painter.MoveTo(new Vector2(x + radius, y));
        painter.LineTo(new Vector2(x + w - radius, y));
        painter.ArcTo(new Vector2(x + w, y), new Vector2(x + w, y + radius), radius);
        painter.LineTo(new Vector2(x + w, y + h - radius));
        painter.ArcTo(new Vector2(x + w, y + h), new Vector2(x + w - radius, y + h), radius);
        painter.LineTo(new Vector2(x + radius, y + h));
        painter.ArcTo(new Vector2(x, y + h), new Vector2(x, y + h - radius), radius);
        painter.LineTo(new Vector2(x, y + radius));
        painter.ArcTo(new Vector2(x, y), new Vector2(x + radius, y), radius);
        painter.ClosePath();
    }

    private static Rect Inflate(Rect rect, float amount)
    {
        return new Rect(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);
    }

    private float Phase(float start, float duration)
    {
        return Mathf.Clamp01((time - start) / Mathf.Max(0.0001f, duration));
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        return 1f - u * u * u;
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
