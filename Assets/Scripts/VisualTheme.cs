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
    /// 타일 오른쪽 위 모서리에 잉크색 아이콘을 붙인다(이미 있으면 갱신). 특수 타일 표시는 이 아이콘 하나로만 한다.
    /// rotationDegrees: 화살표 방향(0 = 오른쪽, 90 = 위).
    /// </summary>
    public static SpriteRenderer AttachCornerIcon(Transform tile, Icon icon, float rotationDegrees = 0f)
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
