using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 점토 테마 설정 화면. 딱딱한 줄 목록 대신 말랑한 점토 타일로 구성한다.
/// - 맨 위: 제로가 고개를 내민 머리줄 + 둥근 닫기 버튼
/// - 소리·진동: 큰 타일 두 개(켜지면 초록 점토, 꺼지면 회색), 알약 스위치가 미끄러진다
/// - 효과: 세 칸짜리 알약 고르기
/// - 도움말·언어·평가·문의: 2×2 아이콘 타일
/// - 맨 아래: 광고 제거·데이터 초기화·개인정보처리방침·이용약관 작은 칩과 버전
/// 동작은 기존 설정 화면의 함수를 그대로 부른다(Actions). 누르면 눌렸다 튀어 오르고, 열릴 때 타일이 차례로 톡톡 나타난다.
/// </summary>
public sealed class ClaySettingsSheet : VisualElement
{
    public const string ElementName = "ClaySettingsSheet";

    public sealed class Actions
    {
        public Func<string, string> T;
        public Func<bool> SoundOn;
        public Func<bool> VibrationOn;
        public Func<int> EffectLevel;          // 0 전체, 1 줄임, 2 최소
        public Func<string> LanguageName;
        public Action ToggleSound;
        public Action ToggleVibration;
        public Action<int> SetEffectLevel;
        public Action Help;
        public Action Language;
        public Action Rate;
        public Action Email;
        public Action RemoveAds;
        public Action ResetData;
        public Action Privacy;
        public Action Terms;
        public Action Close;
        public Action Click;
        public string Version;
    }

    public enum IconKind { Sound, SoundOff, Vibrate, Sparkle, Help, Globe, Star, Mail, NoAds, Trash, Doc, Close }

    private static readonly Color Paper = VisualTheme.Paper;
    private static readonly Color Card = new Color32(0xFB, 0xF8, 0xF3, 0xFF);
    private static readonly Color Ink = VisualTheme.Ink;
    private static readonly Color InkSoft = VisualTheme.InkSoft;
    private static readonly Color Shade = VisualTheme.PaperShade;
    private static readonly Color OnGreen = new Color32(0xB9, 0xD6, 0xA8, 0xFF);
    private static readonly Color Lavender = new Color32(0xD4, 0xCB, 0xEA, 0xFF);
    private static readonly Color Peach = new Color32(0xF2, 0xC9, 0xA8, 0xFF);
    private static readonly Color Butter = new Color32(0xF1, 0xDE, 0xA2, 0xFF);
    private static readonly Color Sky = new Color32(0xBF, 0xD9, 0xEC, 0xFF);

    private readonly Actions actions;
    private readonly List<VisualElement> popIns = new List<VisualElement>();
    private ToggleTile soundTile;
    private ToggleTile vibrationTile;
    private readonly List<Button> effectChips = new List<Button>();

    public ClaySettingsSheet(Actions actions)
    {
        this.actions = actions;
        name = ElementName;
        style.flexGrow = 0;
        style.flexShrink = 1;
        style.backgroundColor = Card;
        style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 56;
        style.borderBottomWidth = 12;
        style.borderBottomColor = Shade;
        style.paddingLeft = style.paddingRight = 40;
        style.paddingTop = 30;
        style.paddingBottom = 26;

        Add(BuildHeader());
        var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden };
        scroll.style.flexGrow = 0;
        scroll.style.flexShrink = 1;
        scroll.style.marginTop = 10;
        Add(scroll);

        scroll.Add(SectionLabel(actions.T("section_game_settings")));
        var toggles = Row();
        soundTile = new ToggleTile(this, actions.T("label_sound"), IconKind.Sound, IconKind.SoundOff, actions.SoundOn, actions.ToggleSound);
        vibrationTile = new ToggleTile(this, actions.T("label_vibration"), IconKind.Vibrate, IconKind.Vibrate, actions.VibrationOn, actions.ToggleVibration);
        soundTile.Root.style.marginRight = 22;
        toggles.Add(soundTile.Root);
        toggles.Add(vibrationTile.Root);
        scroll.Add(toggles);
        scroll.Add(BuildEffectRow());

        scroll.Add(SectionLabel(actions.T("section_help_language")));
        var grid1 = Row();
        grid1.Add(IconTile(actions.T("menu_help"), IconKind.Help, Sky, actions.Help, true));
        grid1.Add(IconTile(actions.LanguageName?.Invoke() ?? "", IconKind.Globe, Lavender, actions.Language, false));
        scroll.Add(grid1);
        var grid2 = Row();
        grid2.style.marginTop = 22;
        grid2.Add(IconTile(actions.T("button_rate"), IconKind.Star, Butter, actions.Rate, true));
        grid2.Add(IconTile(ShortEmailLabel(), IconKind.Mail, Peach, actions.Email, false));
        scroll.Add(grid2);

        scroll.Add(SectionLabel(actions.T("section_data_policy")));
        var chips = new VisualElement();
        chips.style.flexDirection = FlexDirection.Row;
        chips.style.flexWrap = Wrap.Wrap;
        chips.Add(Chip(actions.T("button_remove_ads"), IconKind.NoAds, actions.RemoveAds, false));
        chips.Add(Chip(actions.T("button_reset_data"), IconKind.Trash, actions.ResetData, true));
        chips.Add(Chip(actions.T("button_privacy_policy"), IconKind.Doc, actions.Privacy, false));
        chips.Add(Chip(actions.T("button_terms"), IconKind.Doc, actions.Terms, false));
        scroll.Add(chips);

        if (!string.IsNullOrEmpty(actions.Version))
        {
            var version = new Label("Zero Step  v" + actions.Version);
            version.style.fontSize = 26;
            version.style.color = InkSoft;
            version.style.unityTextAlign = TextAnchor.MiddleCenter;
            version.style.marginTop = 26;
            version.style.marginBottom = 6;
            scroll.Add(version);
        }

        RefreshAll();
        PlayPopIn();
    }

    private string ShortEmailLabel()
    {
        string full = actions.T("button_email");
        // "개발자에게 이메일 보내기"처럼 긴 문구는 타일에 맞게 줄인다.
        string shortText = actions.T("button_email_short");
        return string.IsNullOrEmpty(shortText) || shortText == "button_email_short" ? full : shortText;
    }

    // ------------------------------------------------------------------------------------------- header
    private VisualElement BuildHeader()
    {
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.height = 120;

        var zero = new VisualElement { pickingMode = PickingMode.Ignore };
        zero.style.width = 96;
        zero.style.height = 96;
        zero.style.marginRight = 18;
        var body = new Image { sprite = VisualTheme.ZeroBodySprite, tintColor = VisualTheme.Accent, pickingMode = PickingMode.Ignore };
        var face = new Image { sprite = VisualTheme.ZeroFaceSprite, pickingMode = PickingMode.Ignore };
        foreach (var img in new[] { body, face })
        {
            img.scaleMode = ScaleMode.ScaleToFit;
            img.style.position = Position.Absolute;
            img.style.left = img.style.top = img.style.right = img.style.bottom = 0;
            zero.Add(img);
        }
        header.Add(zero);
        // 제로가 숨 쉬듯 살짝 들썩인다.
        zero.schedule.Execute(() =>
        {
            float t = Time.unscaledTime;
            float s = 1f + Mathf.Sin(t * 2.6f) * 0.04f;
            zero.style.scale = new Scale(new Vector3(1f / s, s, 1f));
            zero.style.rotate = new Rotate(Mathf.Sin(t * 1.3f) * 4f);
        }).Every(33);

        var title = new Label(actions.T("settings_title"));
        title.style.fontSize = 62;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = Ink;
        title.style.flexGrow = 1;
        header.Add(title);

        var close = new Button(() => { actions.Click?.Invoke(); actions.Close?.Invoke(); }) { text = string.Empty };
        StyleClayButton(close, Shade, 48);
        close.style.width = 96;
        close.style.height = 96;
        close.Add(new IconView(IconKind.Close, Ink, 44));
        AddPress(close);
        header.Add(close);
        return header;
    }

    // ------------------------------------------------------------------------------------------- pieces
    private static VisualElement Row()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        return row;
    }

    private static Label SectionLabel(string text)
    {
        var label = new Label(text);
        label.style.fontSize = 32;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.color = InkSoft;
        label.style.marginTop = 34;
        label.style.marginBottom = 16;
        label.style.marginLeft = 8;
        return label;
    }

    private static void StyleClayButton(VisualElement b, Color color, float radius)
    {
        b.style.backgroundColor = color;
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = radius;
        b.style.borderLeftWidth = b.style.borderRightWidth = b.style.borderTopWidth = 0;
        b.style.borderBottomWidth = 8;
        b.style.borderBottomColor = Darker(color, 0.82f);
        b.style.paddingLeft = b.style.paddingRight = b.style.paddingTop = b.style.paddingBottom = 0;
        b.style.marginLeft = b.style.marginRight = b.style.marginTop = b.style.marginBottom = 0;
        b.style.alignItems = Align.Center;
        b.style.justifyContent = Justify.Center;
    }

    private static Color Darker(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

    /// <summary>누르면 쏙 눌리고(아래 두께가 줄며 내려감) 떼면 통 튀어 오른다.</summary>
    private static void AddPress(VisualElement b)
    {
        b.RegisterCallback<PointerDownEvent>(_ =>
        {
            b.style.scale = new Scale(new Vector3(0.94f, 0.94f, 1f));
            b.style.translate = new Translate(0, 4, 0);
        }, TrickleDown.TrickleDown);
        void Release()
        {
            b.style.translate = new Translate(0, 0, 0);
            b.style.scale = new Scale(new Vector3(1.05f, 1.05f, 1f));
            b.schedule.Execute(() => b.style.scale = new Scale(Vector3.one)).StartingIn(110);
        }
        b.RegisterCallback<PointerUpEvent>(_ => Release());
        b.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            b.style.translate = new Translate(0, 0, 0);
            b.style.scale = new Scale(Vector3.one);
        });
    }

    private VisualElement IconTile(string text, IconKind icon, Color color, Action onClick, bool leftOfPair)
    {
        var tile = new Button(() => { actions.Click?.Invoke(); onClick?.Invoke(); }) { text = string.Empty };
        StyleClayButton(tile, color, 40);
        tile.style.flexGrow = 1;
        tile.style.flexBasis = 0;
        tile.style.height = 210;
        if (leftOfPair)
            tile.style.marginRight = 22;
        var bubble = new VisualElement { pickingMode = PickingMode.Ignore };
        bubble.style.width = 92;
        bubble.style.height = 92;
        bubble.style.borderTopLeftRadius = bubble.style.borderTopRightRadius = bubble.style.borderBottomLeftRadius = bubble.style.borderBottomRightRadius = 46;
        bubble.style.backgroundColor = new Color(1f, 1f, 1f, 0.55f);
        bubble.style.alignItems = Align.Center;
        bubble.style.justifyContent = Justify.Center;
        bubble.Add(new IconView(icon, Ink, 56));
        tile.Add(bubble);
        var label = new Label(text) { pickingMode = PickingMode.Ignore };
        label.style.fontSize = 36;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.color = Ink;
        label.style.marginTop = 14;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.paddingLeft = label.style.paddingRight = 10;
        tile.Add(label);
        AddPress(tile);
        popIns.Add(tile);
        return tile;
    }

    private VisualElement Chip(string text, IconKind icon, Action onClick, bool warn)
    {
        var chip = new Button(() => { actions.Click?.Invoke(); onClick?.Invoke(); }) { text = string.Empty };
        StyleClayButton(chip, warn ? new Color32(0xF4, 0xD2, 0xC6, 0xFF) : Paper, 34);
        chip.style.borderBottomWidth = 6;
        chip.style.flexDirection = FlexDirection.Row;
        chip.style.height = 84;
        chip.style.paddingLeft = 26;
        chip.style.paddingRight = 30;
        chip.style.marginRight = 16;
        chip.style.marginBottom = 16;
        chip.Add(new IconView(icon, warn ? VisualTheme.Accent : InkSoft, 38) { style = { marginRight = 12 } });
        var label = new Label(text) { pickingMode = PickingMode.Ignore };
        label.style.fontSize = 30;
        label.style.color = warn ? (Color)new Color32(0xB0, 0x4A, 0x2A, 0xFF) : Ink;
        chip.Add(label);
        AddPress(chip);
        popIns.Add(chip);
        return chip;
    }

    private VisualElement BuildEffectRow()
    {
        var row = Row();
        row.style.alignItems = Align.Center;
        row.style.marginTop = 26;
        row.style.backgroundColor = Paper;
        row.style.borderTopLeftRadius = row.style.borderTopRightRadius = row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 40;
        row.style.paddingLeft = 26;
        row.style.paddingRight = 12;
        row.style.height = 116;
        row.Add(new IconView(IconKind.Sparkle, Ink, 48));
        var label = new Label(actions.T("label_effect_level"));
        label.style.fontSize = 36;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.color = Ink;
        label.style.marginLeft = 14;
        label.style.flexGrow = 1;
        row.Add(label);
        string[] keys = { "effect_level_full", "effect_level_reduced", "effect_level_minimal" };
        var group = Row();
        group.style.backgroundColor = Shade;
        group.style.borderTopLeftRadius = group.style.borderTopRightRadius = group.style.borderBottomLeftRadius = group.style.borderBottomRightRadius = 34;
        group.style.paddingLeft = group.style.paddingRight = group.style.paddingTop = group.style.paddingBottom = 6;
        for (int i = 0; i < keys.Length; i++)
        {
            int level = i;
            var chip = new Button(() =>
            {
                actions.Click?.Invoke();
                actions.SetEffectLevel?.Invoke(level);
                RefreshAll();
            }) { text = actions.T(keys[i]) };
            chip.style.fontSize = 30;
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.style.height = 80;
            chip.style.minWidth = 104;
            chip.style.paddingLeft = chip.style.paddingRight = 18;
            chip.style.marginLeft = chip.style.marginRight = chip.style.marginTop = chip.style.marginBottom = 0;
            chip.style.borderLeftWidth = chip.style.borderRightWidth = chip.style.borderTopWidth = chip.style.borderBottomWidth = 0;
            chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius = chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = 28;
            AddPress(chip);
            effectChips.Add(chip);
            group.Add(chip);
        }
        row.Add(group);
        return row;
    }

    /// <summary>켜고 끄는 큰 타일(소리·진동). 켜지면 초록 점토, 알약 스위치 손잡이가 오른쪽으로 미끄러진다.</summary>
    private sealed class ToggleTile
    {
        public readonly Button Root;
        private readonly Func<bool> state;
        private readonly IconView icon;
        private readonly IconKind onIcon;
        private readonly IconKind offIcon;
        private readonly VisualElement track;
        private readonly VisualElement knob;
        private readonly Label stateLabel;
        private float knobX;

        public ToggleTile(ClaySettingsSheet sheet, string text, IconKind onIcon, IconKind offIcon, Func<bool> state, Action toggle)
        {
            this.state = state;
            this.onIcon = onIcon;
            this.offIcon = offIcon;
            Root = new Button(() =>
            {
                sheet.actions.Click?.Invoke();
                toggle?.Invoke();
                Refresh(animate: true);
            }) { text = string.Empty };
            StyleClayButton(Root, OnGreen, 44);
            Root.style.flexGrow = 1;
            Root.style.flexBasis = 0;
            Root.style.height = 250;
            Root.style.justifyContent = Justify.SpaceAround;
            Root.style.paddingTop = 18;
            Root.style.paddingBottom = 18;

            var top = Row();
            top.pickingMode = PickingMode.Ignore;
            top.style.alignItems = Align.Center;
            icon = new IconView(onIcon, Ink, 60);
            top.Add(icon);
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = 40;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = Ink;
            label.style.marginLeft = 12;
            top.Add(label);
            Root.Add(top);

            var bottom = Row();
            bottom.pickingMode = PickingMode.Ignore;
            bottom.style.alignItems = Align.Center;
            track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.style.width = 132;
            track.style.height = 70;
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius = track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 35;
            knob = new VisualElement { pickingMode = PickingMode.Ignore };
            knob.style.position = Position.Absolute;
            knob.style.top = 7;
            knob.style.left = 7;
            knob.style.width = 56;
            knob.style.height = 56;
            knob.style.borderTopLeftRadius = knob.style.borderTopRightRadius = knob.style.borderBottomLeftRadius = knob.style.borderBottomRightRadius = 28;
            knob.style.backgroundColor = Color.white;
            knob.style.borderBottomWidth = 5;
            knob.style.borderBottomColor = new Color(0f, 0f, 0f, 0.12f);
            track.Add(knob);
            bottom.Add(track);
            stateLabel = new Label { pickingMode = PickingMode.Ignore };
            stateLabel.style.fontSize = 30;
            stateLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            stateLabel.style.marginLeft = 14;
            bottom.Add(stateLabel);
            Root.Add(bottom);
            AddPress(Root);
            sheet.popIns.Add(Root);
        }

        public void Refresh(bool animate)
        {
            bool on = state != null && state();
            Color tileColor = on ? OnGreen : Shade;
            Root.style.backgroundColor = tileColor;
            Root.style.borderBottomColor = Darker(tileColor, 0.82f);
            track.style.backgroundColor = on ? (Color)new Color32(0x5E, 0x9A, 0x55, 0xFF) : (Color)new Color32(0xB9, 0xB0, 0xA3, 0xFF);
            icon.Kind = on ? onIcon : offIcon;
            icon.Tint = on ? Ink : InkSoft;
            stateLabel.text = on ? "ON" : "OFF";
            stateLabel.style.color = on ? (Color)new Color32(0x3E, 0x6E, 0x37, 0xFF) : InkSoft;
            float target = on ? 69f : 7f;
            if (!animate)
            {
                knobX = target;
                knob.style.left = knobX;
                return;
            }
            float from = knobX;
            float start = Time.unscaledTime;
            IVisualElementScheduledItem item = null;
            item = knob.schedule.Execute(() =>
            {
                float u = Mathf.Clamp01((Time.unscaledTime - start) / 0.18f);
                float eased = 1f + 2.2f * Mathf.Pow(u - 1f, 3f) + 1.2f * Mathf.Pow(u - 1f, 2f);
                knobX = Mathf.LerpUnclamped(from, target, eased);
                knob.style.left = knobX;
                if (u >= 1f)
                    item?.Pause();
            }).Every(16);
        }
    }

    private void RefreshAll()
    {
        soundTile?.Refresh(animate: false);
        vibrationTile?.Refresh(animate: false);
        int level = actions.EffectLevel != null ? actions.EffectLevel() : 0;
        for (int i = 0; i < effectChips.Count; i++)
        {
            bool selected = i == level;
            effectChips[i].style.backgroundColor = selected ? Card : new Color(0, 0, 0, 0);
            effectChips[i].style.color = selected ? Ink : InkSoft;
        }
    }

    /// <summary>열릴 때 타일들이 위에서부터 차례로 톡톡 커지며 나타난다.</summary>
    private void PlayPopIn()
    {
        float start = Time.unscaledTime;
        for (int i = 0; i < popIns.Count; i++)
        {
            popIns[i].style.scale = new Scale(new Vector3(0.6f, 0.6f, 1f));
            popIns[i].style.opacity = 0f;
        }
        IVisualElementScheduledItem item = null;
        item = schedule.Execute(() =>
        {
            float age = Time.unscaledTime - start;
            bool done = true;
            for (int i = 0; i < popIns.Count; i++)
            {
                float u = Mathf.Clamp01((age - 0.04f * i) / 0.22f);
                if (u < 1f)
                    done = false;
                float s = u <= 0f ? 0.6f : 0.6f + 0.4f * (1f + 2.2f * Mathf.Pow(u - 1f, 3f) + 1.2f * Mathf.Pow(u - 1f, 2f));
                popIns[i].style.scale = new Scale(new Vector3(s, s, 1f));
                popIns[i].style.opacity = Mathf.Clamp01(u * 3f);
            }
            if (done)
            {
                foreach (var e in popIns)
                    e.style.scale = new Scale(Vector3.one);
                item?.Pause();
            }
        }).Every(16);
    }

    // ------------------------------------------------------------------------------------------- vector icons
    /// <summary>둥근 선으로 그린 아이콘(점토 테마의 잉크 아이콘과 같은 느낌).</summary>
    public sealed class IconView : VisualElement
    {
        private IconKind kind;
        private Color tint;

        public IconKind Kind { get => kind; set { if (kind != value) { kind = value; MarkDirtyRepaint(); } } }
        public Color Tint { get => tint; set { tint = value; MarkDirtyRepaint(); } }

        public IconView(IconKind kind, Color tint, float size)
        {
            this.kind = kind;
            this.tint = tint;
            pickingMode = PickingMode.Ignore;
            style.width = size;
            style.height = size;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext mgc)
        {
            Rect r = contentRect;
            float s = Mathf.Min(r.width, r.height) / 24f;
            Vector2 o = new Vector2(r.x + (r.width - 24f * s) * 0.5f, r.y + (r.height - 24f * s) * 0.5f);
            Vector2 P(float x, float y) => o + new Vector2(x * s, y * s);
            Painter2D p = mgc.painter2D;
            p.strokeColor = tint;
            p.fillColor = tint;
            p.lineWidth = 2.2f * s;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            void Line(float x0, float y0, float x1, float y1)
            {
                p.BeginPath(); p.MoveTo(P(x0, y0)); p.LineTo(P(x1, y1)); p.Stroke();
            }
            void Poly(bool close, bool fill, params float[] xy)
            {
                p.BeginPath();
                p.MoveTo(P(xy[0], xy[1]));
                for (int i = 2; i < xy.Length; i += 2)
                    p.LineTo(P(xy[i], xy[i + 1]));
                if (close) p.ClosePath();
                if (fill) p.Fill(); else p.Stroke();
            }
            void Circle(float cx, float cy, float rad, bool fill)
            {
                p.BeginPath(); p.Arc(P(cx, cy), rad * s, 0f, 360f); p.ClosePath();
                if (fill) p.Fill(); else p.Stroke();
            }
            void ArcPath(float cx, float cy, float rad, float a0, float a1)
            {
                p.BeginPath(); p.Arc(P(cx, cy), rad * s, a0, a1); p.Stroke();
            }

            switch (kind)
            {
                case IconKind.Sound:
                case IconKind.SoundOff:
                    Poly(true, true, 3, 9.5f, 7, 9.5f, 12, 5, 12, 19, 7, 14.5f, 3, 14.5f);
                    if (kind == IconKind.Sound)
                    {
                        ArcPath(12.5f, 12, 4.5f, -45f, 45f);
                        ArcPath(12.5f, 12, 8f, -50f, 50f);
                    }
                    else
                    {
                        Line(15.5f, 9, 21, 15);
                        Line(21, 9, 15.5f, 15);
                    }
                    break;
                case IconKind.Vibrate:
                    Poly(true, false, 8, 3.5f, 16, 3.5f, 16, 20.5f, 8, 20.5f);
                    Line(11, 17.5f, 13, 17.5f);
                    Poly(false, false, 4.5f, 8, 2.5f, 10, 4.5f, 12, 2.5f, 14, 4.5f, 16);
                    Poly(false, false, 19.5f, 8, 21.5f, 10, 19.5f, 12, 21.5f, 14, 19.5f, 16);
                    break;
                case IconKind.Sparkle:
                    Poly(true, true, 11, 2, 13, 9, 20, 11, 13, 13, 11, 20, 9, 13, 2, 11, 9, 9);
                    Poly(true, true, 19, 15.5f, 19.8f, 18.2f, 22.5f, 19, 19.8f, 19.8f, 19, 22.5f, 18.2f, 19.8f, 15.5f, 19, 18.2f, 18.2f);
                    break;
                case IconKind.Help:
                    Circle(12, 12, 9.5f, false);
                    ArcPath(12, 9.5f, 3f, 180f, 360f);
                    p.BeginPath(); p.MoveTo(P(15, 9.5f)); p.BezierCurveTo(P(15, 11.5f), P(12, 11.8f), P(12, 14)); p.Stroke();
                    Circle(12, 17.3f, 1.2f, true);
                    break;
                case IconKind.Globe:
                    Circle(12, 12, 9.5f, false);
                    Line(2.5f, 12, 21.5f, 12);
                    p.BeginPath(); p.MoveTo(P(12, 2.5f)); p.BezierCurveTo(P(7, 7), P(7, 17), P(12, 21.5f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(12, 2.5f)); p.BezierCurveTo(P(17, 7), P(17, 17), P(12, 21.5f)); p.Stroke();
                    break;
                case IconKind.Star:
                {
                    var pts = new List<float>();
                    for (int i = 0; i < 10; i++)
                    {
                        float a = (-90f + i * 36f) * Mathf.Deg2Rad;
                        float rad = i % 2 == 0 ? 10f : 4.3f;
                        pts.Add(12 + Mathf.Cos(a) * rad);
                        pts.Add(12.6f + Mathf.Sin(a) * rad);
                    }
                    Poly(true, false, pts.ToArray());
                    break;
                }
                case IconKind.Mail:
                    Poly(true, false, 3, 6, 21, 6, 21, 18, 3, 18);
                    Poly(false, false, 3.5f, 6.5f, 12, 13, 20.5f, 6.5f);
                    break;
                case IconKind.NoAds:
                    Poly(true, false, 3, 7, 21, 7, 21, 17, 3, 17);
                    Line(4, 20, 20, 4);
                    break;
                case IconKind.Trash:
                    Line(4, 6.5f, 20, 6.5f);
                    Poly(false, false, 9, 6.5f, 9, 4, 15, 4, 15, 6.5f);
                    Poly(false, false, 6, 6.5f, 7, 20, 17, 20, 18, 6.5f);
                    Line(10.5f, 10, 10.5f, 16.5f);
                    Line(13.5f, 10, 13.5f, 16.5f);
                    break;
                case IconKind.Doc:
                    Poly(true, false, 6, 3, 14, 3, 18, 7, 18, 21, 6, 21);
                    Line(9, 11, 15, 11);
                    Line(9, 14.5f, 15, 14.5f);
                    Line(9, 18, 13, 18);
                    break;
                case IconKind.Close:
                    p.lineWidth = 2.8f * s;
                    Line(6, 6, 18, 18);
                    Line(18, 6, 6, 18);
                    break;
            }
        }
    }
}
