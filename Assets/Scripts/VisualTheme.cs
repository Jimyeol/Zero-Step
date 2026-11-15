using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화면 테마(A2 "소프트 클레이"). 따뜻한 종이색 배경, 점토 블록 타일, 진한 잉크색 숫자, 특수 타일은 모서리 아이콘으로만 표시한다.
/// 색은 숫자(남은 횟수)를 보조로만 나타내고, 실제 정보는 크고 진한 숫자가 전달한다(색약·작은 화면 대비).
/// 네온 테마 코드는 남겨 두고, 각 연출은 IsSoftClay일 때 이 팔레트를 쓴다.
/// </summary>
public static class VisualTheme
{
    /// <summary>A2 소프트 클레이 테마 사용 여부.</summary>
    public static bool IsSoftClay = true;

    // 기본 색
    public static readonly Color Paper = new Color32(0xF3, 0xEE, 0xE6, 0xFF);
    public static readonly Color PaperShade = new Color32(0xE6, 0xDF, 0xD3, 0xFF);
    public static readonly Color Ink = new Color32(0x2B, 0x2A, 0x28, 0xFF);
    public static readonly Color InkSoft = new Color32(0x6E, 0x67, 0x5E, 0xFF);
    public static readonly Color Accent = new Color32(0xD9, 0x73, 0x4E, 0xFF);   // 테라코타: 하트, 진행 바, 배지
    public static readonly Color SlotTint = new Color32(0x8C, 0x83, 0x78, 0xFF);  // 0이 된 칸(음각 자리)
    public static readonly Color PathInk = new Color32(0x3A, 0x36, 0x31, 0xFF);   // 손가락으로 그리는 경로

    /// <summary>남은 횟수별 점토 색(채도를 낮춘 보조 신호). 1 모래, 2 세이지, 3 살구, 4 라벤더, 5 더스티 블루, 6 로즈, 7 머스터드, 8+ 스톤.</summary>
    private static readonly Color[] CountColors =
    {
        new Color32(0xEA, 0xDC, 0xC2, 0xFF),
        new Color32(0xBC, 0xD2, 0xB3, 0xFF),
        new Color32(0xF2, 0xB8, 0x92, 0xFF),
        new Color32(0xCD, 0xC2, 0xE3, 0xFF),
        new Color32(0xAE, 0xCB, 0xDC, 0xFF),
        new Color32(0xE7, 0xAF, 0xA9, 0xFF),
        new Color32(0xDC, 0xCB, 0x8E, 0xFF),
        new Color32(0xBD, 0xB5, 0xAB, 0xFF),
    };

    public static Color ClayColorForCount(int count)
    {
        if (count <= 0)
            return SlotTint;
        return CountColors[Mathf.Clamp(count, 1, CountColors.Length) - 1];
    }

    public enum Icon { Arrow, Burst, Lock, Link, Bolt, EyeOff, Moon, Plus, Dot }

    private const string SpriteFolder = "Sprites/Clay/";
    private static Sprite tileSprite;
    private static Sprite slotSprite;
    private static readonly Dictionary<Icon, Sprite> iconSprites = new Dictionary<Icon, Sprite>();

    public static Sprite TileSprite => tileSprite != null ? tileSprite : (tileSprite = Resources.Load<Sprite>(SpriteFolder + "clay_tile"));
    public static Sprite SlotSprite => slotSprite != null ? slotSprite : (slotSprite = Resources.Load<Sprite>(SpriteFolder + "clay_slot"));
    private static Sprite ringSprite;
    /// <summary>점토 타일 윗면 둘레에 맞춘 흰 외곽선(색을 입혀 시작점·짝꿍 그룹 표시에 쓴다).</summary>
    public static Sprite RingSprite => ringSprite != null ? ringSprite : (ringSprite = Resources.Load<Sprite>(SpriteFolder + "clay_ring"));

    private static Sprite zeroBodySprite;
    private static Sprite zeroFaceSprite;
    /// <summary>마스코트 "제로"의 몸통(회색조, 색을 입혀 쓴다)과 얼굴.</summary>
    public static Sprite ZeroBodySprite => zeroBodySprite != null ? zeroBodySprite : (zeroBodySprite = Resources.Load<Sprite>(SpriteFolder + "zero_body"));
    public static Sprite ZeroFaceSprite => zeroFaceSprite != null ? zeroFaceSprite : (zeroFaceSprite = Resources.Load<Sprite>(SpriteFolder + "zero_face"));

    /// <summary>짝꿍(TwinLink) 그룹 색: 타일 점토색·시작점 테라코타와 겹치지 않는 진한 색(청록, 자두, 황토, 남색, 올리브, 숲초록).</summary>
    private static readonly Color[] LinkGroupColors =
    {
        new Color32(0x2E, 0x86, 0x8C, 0xFF),
        new Color32(0x8E, 0x4C, 0x82, 0xFF),
        new Color32(0xB5, 0x7E, 0x1E, 0xFF),
        new Color32(0x45, 0x5F, 0xA8, 0xFF),
        new Color32(0x6B, 0x7F, 0x2A, 0xFF),
        new Color32(0x2F, 0x6B, 0x4F, 0xFF),
    };

    /// <summary>숫자가 "?"로 가려진 타일의 색(어떤 숫자 색과도 다른 중립 회갈색).</summary>
    public static readonly Color ClayHiddenColor = new Color32(0xCF, 0xC8, 0xBC, 0xFF);

    /// <summary>짝꿍 글자별 고정 색: A 파랑, B 분홍, C 노랑, D 청록, E 보라, F 연두. 서로 잘 구분되고 칸 숫자 색과도 겹치지 않는다.</summary>
    private static readonly Color[] LinkLabelColors =
    {
        new Color32(0x2F, 0x6F, 0xD0, 0xFF),
        new Color32(0xD0, 0x45, 0x7F, 0xFF),
        new Color32(0xD0, 0x8A, 0x14, 0xFF),
        new Color32(0x1F, 0x9A, 0x8A, 0xFF),
        new Color32(0x7A, 0x4F, 0xC9, 0xFF),
        new Color32(0x5E, 0x8F, 0x1F, 0xFF),
    };

    public static Color LinkGroupColorForLabel(string label)
    {
        int index = 0;
        if (!string.IsNullOrEmpty(label))
        {
            char c = char.ToUpperInvariant(label[0]);
            index = c >= 'A' && c <= 'Z' ? c - 'A' : 0;
        }
        return LinkLabelColors[index % LinkLabelColors.Length];
    }

    public static Color LinkGroupColor(int linkId)
    {
        int index = ((linkId % LinkGroupColors.Length) + LinkGroupColors.Length) % LinkGroupColors.Length;
        return LinkGroupColors[index];
    }

    /// <summary>현재 플레이어가 서 있거나(대기 중) 드래그로 머리가 있는 타일. 짝꿍 연결 표시 등에 쓴다.</summary>
    public static Tile FocusTile;

    public static Sprite IconSprite(Icon icon)
    {
        if (iconSprites.TryGetValue(icon, out Sprite sprite) && sprite != null)
            return sprite;
        string name = icon switch
        {
            Icon.Arrow => "icon_arrow",
            Icon.Burst => "icon_burst",
            Icon.Lock => "icon_lock",
            Icon.Link => "icon_link",
            Icon.Bolt => "icon_bolt",
            Icon.EyeOff => "icon_eyeoff",
            Icon.Moon => "icon_moon",
            Icon.Plus => "icon_plus",
            _ => "icon_dot",
        };
        sprite = Resources.Load<Sprite>(SpriteFolder + name);
        iconSprites[icon] = sprite;
        return sprite;
    }

    private const string CornerIconName = "ClayCornerIcon";

    /// <summary>
    /// 도전 목표 "별 칸에서 끝내기"의 별 표시를 타일 왼쪽 아래 모서리에 붙인다(특수 타일 아이콘·짝꿍 배지와 겹치지 않는 자리).
    /// </summary>
    public static void AttachGoalStar(Transform tile)
    {
        if (tile == null || tile.Find(GoalStarName) != null)
            return;
        var go = new GameObject(GoalStarName);
        go.transform.SetParent(tile, false);
        SpriteRenderer tileRenderer = tile.GetComponent<SpriteRenderer>();
        Vector2 tileSize = tileRenderer != null && tileRenderer.sprite != null ? (Vector2)tileRenderer.sprite.bounds.size : new Vector2(5.12f, 5.12f);
        go.transform.localPosition = new Vector3(-tileSize.x * 0.25f, -tileSize.y * 0.185f, -0.01f);
        Sprite sprite = StarSprite;
        float iconWorld = tileSize.x * 0.22f;
        go.transform.localScale = Vector3.one * (iconWorld / Mathf.Max(0.0001f, sprite.bounds.size.x));
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Accent;
        go.AddComponent<ClayCornerIcon>();
        if (tileRenderer != null)
        {
            sr.sortingLayerID = tileRenderer.sortingLayerID;
            sr.sortingOrder = tileRenderer.sortingOrder + 2;
        }
    }

    public static void RemoveGoalStar(Transform tile)
    {
        Transform existing = tile != null ? tile.Find(GoalStarName) : null;
        if (existing != null)
            Object.Destroy(existing.gameObject);
    }

    private const string GoalStarName = "ClayGoalStar";
    private static Sprite starSprite;

    /// <summary>흰 별 스프라이트(색은 SpriteRenderer.color로 입힌다). 코드로 그려 한 번만 만든다.</summary>
    public static Sprite StarSprite
    {
        get
        {
            if (starSprite != null)
                return starSprite;
            const int size = 128;
            const int samples = 4;
            var points = new Vector2[10];
            for (int k = 0; k < 10; k++)
            {
                float angle = Mathf.PI * 0.5f + k * Mathf.PI / 5f;
                float radius = (k % 2 == 0 ? 0.47f : 0.22f) * size;
                points[k] = new Vector2(size * 0.5f + Mathf.Cos(angle) * radius, size * 0.48f + Mathf.Sin(angle) * radius);
            }
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            if (PointInPolygon(points, x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples))
                                inside++;
                        }
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * inside / (samples * samples)));
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ClayStar", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            starSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            starSprite.name = "ClayStar";
            return starSprite;
        }
    }

    private static bool PointInPolygon(Vector2[] polygon, float px, float py)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];
            if ((a.y > py) != (b.y > py) && px < (b.x - a.x) * (py - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>
    /// 타일 오른쪽 위 모서리에 잉크색 아이콘을 붙인다(이미 있으면 갱신). 특수 타일 표시는 이 아이콘 하나로만 한다.
    /// rotationDegrees: 화살표 방향(0 = 오른쪽, 90 = 위). motion: 아이콘의 살아 있는 움직임(ClayIconMotion), flow는 Flow 방향.
    /// </summary>
    public static SpriteRenderer AttachCornerIcon(Transform tile, Icon icon, float rotationDegrees = 0f,
        ClayIconMotion.Motion motion = ClayIconMotion.Motion.None, Vector2 flow = default)
    {
        if (tile == null)
            return null;
        Transform existing = tile.Find(CornerIconName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(CornerIconName);
        if (existing == null)
            go.transform.SetParent(tile, false);

        SpriteRenderer tileRenderer = tile.GetComponent<SpriteRenderer>();
        Vector2 tileSize = tileRenderer != null && tileRenderer.sprite != null ? (Vector2)tileRenderer.sprite.bounds.size : new Vector2(5.12f, 5.12f);
        // 점토 타일 윗면 기준 오른쪽 위(스프라이트 512px 중 윗면은 y 20~466). 아이콘은 타일 폭의 약 22%.
        go.transform.localPosition = new Vector3(tileSize.x * 0.26f, tileSize.y * 0.27f, -0.01f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        float iconWorld = tileSize.x * 0.25f;
        Sprite sprite = IconSprite(icon);
        float spriteWorld = sprite != null ? Mathf.Max(0.0001f, sprite.bounds.size.x) : 1f;
        go.transform.localScale = Vector3.one * (iconWorld / spriteWorld);

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        Color ink = Ink;
        ink.a = 0.86f;
        sr.color = ink;
        if (go.GetComponent<ClayCornerIcon>() == null)
            go.AddComponent<ClayCornerIcon>();
        if (tileRenderer != null)
        {
            sr.sortingLayerID = tileRenderer.sortingLayerID;
            sr.sortingOrder = tileRenderer.sortingOrder + 2;
        }
        if (motion != ClayIconMotion.Motion.None)
            ClayIconMotion.Attach(sr, motion, flow);
        return sr;
    }

    public static void RemoveCornerIcon(Transform tile)
    {
        if (tile == null)
            return;
        Transform existing = tile.Find(CornerIconName);
        if (existing != null)
            Object.Destroy(existing.gameObject);
    }
}
