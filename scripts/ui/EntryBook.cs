using System;
using System.Collections.Generic;
using Godot;

namespace EquestriaStar.UI;

/// <summary>
/// 入口界面（登录 / 注册 / 大厅 / 等待房间）的“折纸立体书”外观。
/// 每个界面是同一本书的一页：整屏铺一张书本背景（左页隧道书插图 + 空白右页），
/// 控件全部由引擎绘制在右页上。素材在 res://assets/art/ui/entry_book/（不含文字）。
/// 坐标统一用 1920×1080 的“书本舞台”坐标，舞台在屏幕中央，窗口更宽 / 更高时四周露出压暗的桌面。
/// </summary>
public static partial class EntryBook
{
    public const float StageWidth = 1920f;
    public const float StageHeight = 1080f;
    private const string Root = "res://assets/art/ui/entry_book/";

    // 克制的低饱和暖调色板（与概念图定稿一致）。
    public static readonly Color Parchment = new("efe6d6");
    public static readonly Color PaperLight = new("f7f1e6");
    public static readonly Color Ink = new("4a3426");
    public static readonly Color InkSoft = new("8a7562");
    public static readonly Color InkFaint = new("b7a58f");
    public static readonly Color Lilac = new("b6aec4");
    public static readonly Color LilacWash = new("e4dde9");
    public static readonly Color Mist = new("a3b4c2");
    public static readonly Color Sage = new("a8b89f");
    public static readonly Color Rose = new("cfa79e");
    public static readonly Color Gold = new("c99a45");
    public static readonly Color Disabled = new("cbc5bc");
    public static readonly Color ErrorInk = new("a0473a");
    public static readonly Color InfoInk = new("5d6f7c");

    /// <summary>各页右页的可用版心（舞台坐标）。由背景图实测得到。</summary>
    public static readonly Dictionary<string, Rect2> RightPage = new(StringComparer.Ordinal)
    {
        ["login"] = new Rect2(880, 58, 925, 920),
        ["lobby"] = new Rect2(785, 50, 1015, 912),
        ["room"] = new Rect2(785, 50, 1015, 912)
    };

    private static readonly Dictionary<string, Texture2D?> Cache = new(StringComparer.Ordinal);
    private static Font? _titleFont;
    private static Font? _bodyFont;
    private static Font? _boldFont;

    public enum CapsuleKind
    {
        Primary,
        Secondary,
        Warm,
        Quiet
    }

    public enum TagKind
    {
        Waiting,
        Playing,
        Ready,
        NotReady,
        Owner,
        Empty
    }

    public static Texture2D? Tex(string name)
    {
        var path = Root + name + ".png";
        if (Cache.TryGetValue(path, out var cached) && (cached == null || GodotObject.IsInstanceValid(cached)))
        {
            return cached;
        }

        var texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        if (texture == null)
        {
            GD.PushWarning($"立体书素材缺失：{path}");
        }

        Cache[path] = texture;
        return texture;
    }

    public static Texture2D? Icon(string name) => Tex("icons/" + name);

    // ---------------------------------------------------------------- 字体

    /// <summary>标题：书卷气的粗宋（思源 / Noto 宋体），找不到退回雅黑粗体。</summary>
    public static Font TitleFont => _titleFont ??= new FontVariation
    {
        BaseFont = new SystemFont
        {
            FontNames = ["Noto Serif SC", "Source Han Serif SC", "思源宋体", "Songti SC", "STZhongsong", "Microsoft YaHei UI"],
            FontWeight = 900,
            Antialiasing = TextServer.FontAntialiasing.Gray
        }
    };

    public static Font BodyFont => _bodyFont ??= new SystemFont
    {
        FontNames = ["HarmonyOS Sans SC", "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Noto Sans SC"],
        FontWeight = 500
    };

    public static Font BoldFont => _boldFont ??= new SystemFont
    {
        FontNames = ["HarmonyOS Sans SC", "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Noto Sans SC"],
        FontWeight = 700
    };

    // ---------------------------------------------------------------- 舞台

    /// <summary>
    /// 在界面最底层铺书本背景，返回 1920×1080 的舞台节点（居中）。
    /// 已有的旧背景节点（名为 Background 的 ColorRect）会被隐藏。
    /// </summary>
    public static Control BuildStage(Control screen, string background)
    {
        if (screen.GetNodeOrNull<CanvasItem>("Background") is { } legacy)
        {
            legacy.Visible = false;
        }

        var texture = Tex("bg/" + background);
        var backdrop = new TextureRect
        {
            Name = "BookBackdrop",
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Modulate = new Color(0.5f, 0.42f, 0.38f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        screen.AddChild(backdrop);
        screen.MoveChild(backdrop, 0);
        // 窗口比书本更宽 / 更高时露出的桌面：压一层木色，只隐约透出放大的背景，避免看出“第二本书”。
        var wood = new ColorRect { Name = "BookBackdropTint", Color = new Color(0.27f, 0.17f, 0.12f, 0.82f), MouseFilter = Control.MouseFilterEnum.Ignore };
        wood.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        screen.AddChild(wood);
        screen.MoveChild(wood, 1);

        var stage = new Control { Name = "BookStage", MouseFilter = Control.MouseFilterEnum.Ignore };
        PlaceOnStage(stage, new Rect2(0, 0, StageWidth, StageHeight));
        screen.AddChild(stage);
        screen.MoveChild(stage, 2);

        var book = new BookParallax
        {
            Name = "Book",
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        book.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var depthPath = Root + "depth/" + background + ".png";
        book.Setup(ResourceLoader.Exists(depthPath) ? GD.Load<Texture2D>(depthPath) : null);
        stage.AddChild(book);
        return stage;
    }

    /// <summary>把屏幕的直接子节点放到舞台坐标 rect 上（锚点居中，随窗口居中）。</summary>
    public static void PlaceOnStage(Control node, Rect2 rect)
    {
        node.AnchorLeft = 0.5f;
        node.AnchorRight = 0.5f;
        node.AnchorTop = 0.5f;
        node.AnchorBottom = 0.5f;
        node.OffsetLeft = rect.Position.X - StageWidth / 2f;
        node.OffsetTop = rect.Position.Y - StageHeight / 2f;
        node.OffsetRight = rect.End.X - StageWidth / 2f;
        node.OffsetBottom = rect.End.Y - StageHeight / 2f;
        node.GrowHorizontal = Control.GrowDirection.Both;
        node.GrowVertical = Control.GrowDirection.Both;
    }

    /// <summary>在舞台节点内部按舞台坐标放置（stage 自身就是 1920×1080）。</summary>
    public static T AddAt<T>(Control stage, T node, Rect2 rect) where T : Control
    {
        node.Position = rect.Position;
        node.Size = rect.Size;
        stage.AddChild(node);
        return node;
    }

    // ---------------------------------------------------------------- 页面骨架

    /// <summary>页眉：左侧小标志、右侧页名，下方一道细线。</summary>
    public static void RunningHead(Control stage, Rect2 page, string pageName)
    {
        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 16);
        head.AddChild(Crest(200, 23));
        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        head.AddChild(spacer);
        var name = MakeLabel(pageName, 24, Ink, TitleFont, HorizontalAlignment.Right);
        name.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        head.AddChild(name);
        AddAt(stage, head, new Rect2(page.Position.X + 24, page.Position.Y + 4, page.Size.X - 48, 76));
        var rule = Rule(1.5f, new Color(InkSoft, 0.4f));
        AddAt(stage, rule, new Rect2(page.Position.X + 24, page.Position.Y + 90, page.Size.X - 48, 1.5f));
    }

    public static void Footer(Control stage, Rect2 page)
    {
        AddAt(stage, FooterOrnament(320), new Rect2(page.Position.X, page.End.Y - 46, page.Size.X, 30));
    }

    /// <summary>借阅卡：浅纸卡底（九宫格）+ 卡片下半部插进雾紫书袋。</summary>
    public static void DecorateCard(PanelContainer panel, float width, float pocketHeight = 168f)
    {
        panel.CustomMinimumSize = new Vector2(width, 0);
        var card = Tex("parts/login_card");
        if (card != null)
        {
            panel.AddThemeStyleboxOverride("panel", new StyleBoxTexture
            {
                Texture = card,
                TextureMarginLeft = 48,
                TextureMarginTop = 48,
                TextureMarginRight = 48,
                TextureMarginBottom = 48,
                ContentMarginLeft = 0,
                ContentMarginTop = 0,
                ContentMarginRight = 0,
                ContentMarginBottom = 0
            });
        }

        var layer = new Control { Name = "PocketLayer", MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(layer);
        panel.MoveChild(layer, 0);
        var pocket = MakeImage("parts/pocket", Vector2.Zero, TextureRect.StretchModeEnum.Scale);
        pocket.AnchorLeft = 0;
        pocket.AnchorRight = 1;
        pocket.AnchorTop = 1;
        pocket.AnchorBottom = 1;
        pocket.OffsetLeft = -30;
        pocket.OffsetRight = 30;
        pocket.OffsetTop = -pocketHeight;
        pocket.OffsetBottom = 26;
        layer.AddChild(pocket);

        // 投影：作为卡片的子节点但画在卡片后面。
        var shadow = new Panel { Name = "CardShadow", MouseFilter = Control.MouseFilterEnum.Ignore, ShowBehindParent = true };
        shadow.AddThemeStyleboxOverride("panel", SoftShadow(36, 0.22f, 20, 10));
        panel.AddChild(shadow);
        panel.MoveChild(shadow, 0);
    }

    /// <summary>卡片标题：两侧卷草细线 + 粗宋标题。</summary>
    public static void StyleCardTitle(Label title, int size = 40)
    {
        StyleLabel(title, size, Ink, TitleFont);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        if (title.GetNodeOrNull("FlourishLeft") != null)
        {
            return;
        }

        // 标题两侧的小花饰：灰玫细线 + 圆点（概念图里“～ 登录 ～”的卷草线简化版）。
        var left = Flourish(true);
        left.Name = "FlourishLeft";
        var right = Flourish(false);
        right.Name = "FlourishRight";
        title.AddChild(left);
        title.AddChild(right);
        void Place()
        {
            var font = title.GetThemeFont("font");
            var textWidth = font.GetStringSize(title.Text, HorizontalAlignment.Left, -1, title.GetThemeFontSize("font_size")).X;
            var cx = title.Size.X / 2f;
            var cy = title.Size.Y / 2f;
            left.Position = new Vector2(cx - textWidth / 2f - 18f - 64f, cy - 5f);
            right.Position = new Vector2(cx + textWidth / 2f + 18f, cy - 5f);
        }

        title.Resized += Place;
        Place();
    }

    private static Control Flourish(bool pointLeft)
    {
        var box = new Control { Size = new Vector2(64, 10), MouseFilter = Control.MouseFilterEnum.Ignore };
        var line = new ColorRect { Color = new Color(Rose, 0.85f), MouseFilter = Control.MouseFilterEnum.Ignore };
        line.Position = new Vector2(pointLeft ? 0 : 14, 4);
        line.Size = new Vector2(50, 2);
        box.AddChild(line);
        var dot = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(8, 8) };
        dot.Position = new Vector2(pointLeft ? 54 : 2, 1);
        dot.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Rose,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            AntiAliasing = true
        });
        box.AddChild(dot);
        return box;
    }

    // ---------------------------------------------------------------- 文字

    public static void StyleLabel(Label label, int size, Color? color = null, Font? font = null)
    {
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Ink);
        label.AddThemeFontOverride("font", font ?? BodyFont);
    }

    public static Label MakeLabel(string text, int size, Color? color = null, Font? font = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = align,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        StyleLabel(label, size, color, font);
        return label;
    }

    /// <summary>让单行文字按内容宽度占位、超过 maxWidth 时省略（引导点线填满剩余空间）。</summary>
    public static void FitWidth(Label label, float maxWidth)
    {
        var font = label.GetThemeFont("font");
        var size = label.GetThemeFontSize("font_size");
        var width = font.GetStringSize(label.Text, HorizontalAlignment.Left, -1, size).X + 6f;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.CustomMinimumSize = new Vector2(MathF.Min(width, maxWidth), label.CustomMinimumSize.Y);
    }

    public static void ShowStatus(Label label, string message, bool isError)
    {
        label.Text = message;
        label.AddThemeColorOverride("font_color", isError ? ErrorInk : InfoInk);
    }

    // ---------------------------------------------------------------- 胶囊按钮

    private static StyleBoxFlat Capsule(Color fill, Color border, float borderWidth, float bottom, float height, float shadow = 0.16f)
    {
        var radius = (int)MathF.Ceiling(height / 2f);
        return new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = border,
            BorderWidthLeft = (int)borderWidth,
            BorderWidthTop = (int)borderWidth,
            BorderWidthRight = (int)borderWidth,
            BorderWidthBottom = (int)bottom,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = height * 0.5f,
            ContentMarginRight = height * 0.5f,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            ShadowColor = new Color(0.29f, 0.2f, 0.15f, shadow),
            ShadowSize = shadow > 0 ? 4 : 0,
            ShadowOffset = new Vector2(0, 2),
            AntiAliasing = true
        };
    }

    public static void StyleCapsule(Button button, CapsuleKind kind, float height = 52f, int fontSize = 22, string? icon = null)
    {
        (Color fill, Color hover, Color pressed, Color border, Color font) palette = kind switch
        {
            CapsuleKind.Primary => (new Color("d2a65a"), new Color("dbb26a"), new Color("bf9447"), new Color("a77d36"), Ink),
            CapsuleKind.Warm => (new Color("f4e2c1"), new Color("f8ead0"), new Color("ead3ab"), new Color("c7a473"), Ink),
            CapsuleKind.Quiet => (new Color(0, 0, 0, 0), LilacWash, new Color("dcd3e2"), new Color(0, 0, 0, 0), Ink),
            _ => (PaperLight, new Color("fbf7ef"), Parchment, new Color("9b8670"), Ink)
        };
        var borderWidth = kind == CapsuleKind.Quiet ? 0f : 1.5f;
        var bottom = kind == CapsuleKind.Quiet ? 0f : 3f;
        var shadow = kind == CapsuleKind.Quiet ? 0f : 0.16f;
        button.AddThemeStyleboxOverride("normal", Capsule(palette.fill, palette.border, borderWidth, bottom, height, shadow));
        button.AddThemeStyleboxOverride("hover", Capsule(palette.hover, palette.border, borderWidth, bottom, height, shadow));
        button.AddThemeStyleboxOverride("pressed", Capsule(palette.pressed, palette.border, borderWidth, MathF.Max(borderWidth, 1f), height, shadow * 0.5f));
        button.AddThemeStyleboxOverride("hover_pressed", Capsule(palette.pressed, palette.border, borderWidth, MathF.Max(borderWidth, 1f), height, shadow * 0.5f));
        button.AddThemeStyleboxOverride("disabled", Capsule(Disabled, new Color("b5aea4"), borderWidth, bottom, height, 0.08f));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", palette.font);
        button.AddThemeColorOverride("font_hover_color", palette.font);
        button.AddThemeColorOverride("font_pressed_color", palette.font);
        button.AddThemeColorOverride("font_hover_pressed_color", palette.font);
        button.AddThemeColorOverride("font_focus_color", palette.font);
        button.AddThemeColorOverride("font_disabled_color", new Color("8f877f"));
        button.AddThemeColorOverride("icon_normal_color", palette.font);
        button.AddThemeColorOverride("icon_hover_color", palette.font);
        button.AddThemeColorOverride("icon_pressed_color", palette.font);
        button.AddThemeColorOverride("icon_disabled_color", new Color("8f877f"));
        button.AddThemeFontOverride("font", kind == CapsuleKind.Primary && fontSize >= 24 ? TitleFont : BoldFont);
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeConstantOverride("h_separation", 8);
        button.AddThemeConstantOverride("icon_max_width", (int)(fontSize * 1.15f));
        button.CustomMinimumSize = new Vector2(button.CustomMinimumSize.X, height);
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        button.FocusMode = Control.FocusModeEnum.None;
        if (icon != null)
        {
            button.Icon = Icon(icon);
            button.ExpandIcon = false;
        }
    }

    /// <summary>
    /// 宽按钮里让图标紧挨文字居中（Godot 默认把图标贴在左边缘）：按当前宽度动态调整左右内边距。
    /// </summary>
    public static void KeepIconBesideText(Button button)
    {
        if (button.HasMeta("icon_beside_text"))
        {
            RecenterIcon(button);
            return;
        }

        button.SetMeta("icon_beside_text", true);
        RecenterIcon(button);
    }

    public static void RecenterIcon(Button button)
    {
        if (!button.HasMeta("icon_beside_text"))
        {
            return;
        }

        // 以“目标宽度”（自定义最小宽度，或容器分到的宽度）为基准，不用实时 Size，避免内边距→最小尺寸→Size 的反馈循环。
        var target = button.CustomMinimumSize.X > 0 ? button.CustomMinimumSize.X : 0f;
        var height = button.CustomMinimumSize.Y > 0 ? button.CustomMinimumSize.Y : 48f;
        var pad = height * 0.5f;
        if (button.Icon != null && target > 0)
        {
            var font = button.GetThemeFont("font");
            var size = button.GetThemeFontSize("font_size");
            var textWidth = font.GetStringSize(button.Text, HorizontalAlignment.Left, -1, size).X;
            var iconWidth = MathF.Min(button.GetThemeConstant("icon_max_width"), button.Icon.GetWidth()) + button.GetThemeConstant("h_separation");
            pad = MathF.Max(pad, MathF.Floor((target - textWidth - iconWidth) / 2f) - 8f);
        }

        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
        {
            if (button.GetThemeStylebox(state) is StyleBox box && MathF.Abs(box.ContentMarginLeft - pad) > 0.5f)
            {
                box.ContentMarginLeft = pad;
                box.ContentMarginRight = pad;
            }
        }
    }

    /// <summary>无底的小图标按钮（退出、关闭、显示密码）。</summary>
    public static void StyleIconButton(Button button, string icon, float size = 36f, string tooltip = "")
    {
        button.Text = "";
        button.Icon = Icon(icon);
        button.ExpandIcon = true;
        button.IconAlignment = HorizontalAlignment.Center;
        button.TooltipText = tooltip;
        button.CustomMinimumSize = new Vector2(size, size);
        button.FocusMode = Control.FocusModeEnum.None;
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        var round = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0),
            CornerRadiusTopLeft = (int)size,
            CornerRadiusTopRight = (int)size,
            CornerRadiusBottomLeft = (int)size,
            CornerRadiusBottomRight = (int)size,
            ContentMarginLeft = size * 0.18f,
            ContentMarginRight = size * 0.18f,
            ContentMarginTop = size * 0.18f,
            ContentMarginBottom = size * 0.18f
        };
        var hover = (StyleBoxFlat)round.Duplicate();
        hover.BgColor = new Color(0.29f, 0.2f, 0.15f, 0.08f);
        button.AddThemeStyleboxOverride("normal", round);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);
        button.AddThemeStyleboxOverride("disabled", round);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("icon_normal_color", Ink);
        button.AddThemeColorOverride("icon_hover_color", Ink);
        button.AddThemeColorOverride("icon_pressed_color", Ink);
        button.AddThemeColorOverride("icon_disabled_color", InkFaint);
    }

    // ---------------------------------------------------------------- 输入框

    /// <summary>浅纸色内凹胶囊输入框，左侧线性图标。</summary>
    public static void StyleInput(LineEdit edit, string? icon, float height = 50f, int fontSize = 19, float rightPad = 18f)
    {
        var leftPad = icon == null ? 22f : 54f;
        StyleBoxFlat Box(Color border, float width) => new()
        {
            BgColor = new Color("f4eee3"),
            BorderColor = border,
            BorderWidthLeft = (int)width,
            BorderWidthTop = (int)MathF.Max(width, 2f),
            BorderWidthRight = (int)width,
            BorderWidthBottom = (int)width,
            CornerRadiusTopLeft = (int)(height / 2),
            CornerRadiusTopRight = (int)(height / 2),
            CornerRadiusBottomLeft = (int)(height / 2),
            CornerRadiusBottomRight = (int)(height / 2),
            ContentMarginLeft = leftPad,
            ContentMarginRight = rightPad,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
            AntiAliasing = true
        };
        edit.AddThemeStyleboxOverride("normal", Box(new Color("b9a68e"), 1.5f));
        edit.AddThemeStyleboxOverride("focus", Box(Gold, 2f));
        edit.AddThemeStyleboxOverride("read_only", Box(new Color("cfc4b4"), 1.5f));
        edit.AddThemeColorOverride("font_color", Ink);
        edit.AddThemeColorOverride("font_placeholder_color", new Color(InkSoft, 0.8f));
        edit.AddThemeColorOverride("font_uneditable_color", InkSoft);
        edit.AddThemeColorOverride("caret_color", Ink);
        edit.AddThemeColorOverride("selection_color", new Color(Gold, 0.35f));
        edit.AddThemeFontOverride("font", BodyFont);
        edit.AddThemeFontSizeOverride("font_size", fontSize);
        edit.CustomMinimumSize = new Vector2(edit.CustomMinimumSize.X, height);
        edit.ClearButtonEnabled = false;
        if (icon != null && edit.GetNodeOrNull("FieldIcon") == null)
        {
            var glyph = new TextureRect
            {
                Name = "FieldIcon",
                Texture = Icon(icon),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = InkSoft
            };
            glyph.AnchorTop = 0.5f;
            glyph.AnchorBottom = 0.5f;
            glyph.OffsetLeft = 18;
            glyph.OffsetRight = 18 + height * 0.46f;
            glyph.OffsetTop = -height * 0.23f;
            glyph.OffsetBottom = height * 0.23f;
            edit.AddChild(glyph);
        }
    }

    // ---------------------------------------------------------------- 状态纸签

    public static (Color Fill, Color Border, Color Text, string? Icon) TagPalette(TagKind kind) => kind switch
    {
        TagKind.Waiting => (new Color("c3cfb8"), new Color("9fb091"), new Color("3f4d36"), null),
        TagKind.Ready => (new Color("c3cfb8"), new Color("9fb091"), new Color("3f4d36"), "check"),
        TagKind.Playing => (new Color("e3c4bc"), new Color("c79d93"), new Color("7a4239"), null),
        TagKind.Owner => (new Color("f7efdf"), new Color("d8bd86"), new Color("6b4f2a"), "crown"),
        TagKind.Empty => (new Color("e9e3da"), new Color("d4cbbf"), new Color("9a9086"), null),
        _ => (new Color("dcd6cd"), new Color("c4bcb1"), new Color("7f776e"), null)
    };

    public static StyleBoxFlat TagBox(TagKind kind, float height = 34f)
    {
        var palette = TagPalette(kind);
        var radius = (int)MathF.Ceiling(height / 2f);
        return new StyleBoxFlat
        {
            BgColor = palette.Fill,
            BorderColor = palette.Border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
            AntiAliasing = true
        };
    }

    /// <summary>可复用的状态纸签：PanelContainer + 图标 + 文字，返回整体与文字 Label。</summary>
    public sealed partial class Tag : PanelContainer
    {
        private readonly Label _label;
        private readonly TextureRect _icon;
        private readonly float _height;

        public Tag() : this(34f) { }

        public Tag(float height, int fontSize = 16, float minWidth = 104f)
        {
            _height = height;
            MouseFilter = MouseFilterEnum.Ignore;
            CustomMinimumSize = new Vector2(minWidth, height);
            SizeFlagsVertical = SizeFlags.ShrinkCenter;
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 6);
            AddChild(row);
            _icon = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(height * 0.56f, height * 0.56f),
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Ignore
            };
            row.AddChild(_icon);
            _label = MakeLabel("", fontSize, null, BoldFont, HorizontalAlignment.Center);
            row.AddChild(_label);
        }

        public void Set(TagKind kind, string text)
        {
            var palette = TagPalette(kind);
            AddThemeStyleboxOverride("panel", TagBox(kind, _height));
            _label.Text = text;
            _label.AddThemeColorOverride("font_color", palette.Text);
            _icon.Texture = palette.Icon == null ? null : (palette.Icon == "crown" ? Tex("parts/crown") : Icon(palette.Icon));
            _icon.Visible = palette.Icon != null;
            _icon.Modulate = palette.Icon == "check" ? palette.Text : Colors.White;
        }
    }

    // ---------------------------------------------------------------- 书页装饰

    /// <summary>纸片投影（暖棕柔影）。</summary>
    public static StyleBoxFlat SoftShadow(int radius, float alpha = 0.22f, int size = 14, float offsetY = 6f) => new()
    {
        BgColor = new Color(0, 0, 0, 0),
        ShadowColor = new Color(0.29f, 0.19f, 0.12f, alpha),
        ShadowSize = size,
        ShadowOffset = new Vector2(0, offsetY),
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius
    };

    public static TextureRect MakeImage(string name, Vector2 minSize, TextureRect.StretchModeEnum stretch = TextureRect.StretchModeEnum.KeepAspectCentered)
    {
        return new TextureRect
        {
            Texture = Tex(name),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = stretch,
            CustomMinimumSize = minSize,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    public static TextureRect MakeIcon(string name, float size, Color? tint = null)
    {
        // 图标素材是白色线稿，靠 Modulate 上色（默认墨棕）。
        var icon = MakeImage("icons/" + name, new Vector2(size, size));
        icon.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        icon.Modulate = tint ?? Ink;

        return icon;
    }

    /// <summary>页眉左侧的小标志：卷边徽牌 + 「小马国之星」。</summary>
    public static Control Crest(float width = 214f, int fontSize = 25)
    {
        var height = width * 0.36f;
        var crest = new Control { CustomMinimumSize = new Vector2(width, height), MouseFilter = Control.MouseFilterEnum.Ignore };
        var plate = MakeImage("parts/crest", Vector2.Zero, TextureRect.StretchModeEnum.Scale);
        plate.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        crest.AddChild(plate);
        var label = MakeLabel("小马国之星", fontSize, Ink, TitleFont, HorizontalAlignment.Center);
        label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        label.OffsetTop = height * 0.2f;
        crest.AddChild(label);
        return crest;
    }

    /// <summary>细墨线（可带两端淡出）。</summary>
    public static Control Rule(float thickness = 1.5f, Color? color = null)
    {
        var line = new ColorRect
        {
            Color = color ?? new Color(InkSoft, 0.45f),
            CustomMinimumSize = new Vector2(0, thickness),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        return line;
    }

    /// <summary>分节标签：「目录」「角色表」+ 右侧细线。</summary>
    public static Control SectionHeader(string text, Control? trailing = null)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 14);
        var tab = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(132, 46) };
        tab.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var plate = MakeImage("parts/section_tab", Vector2.Zero, TextureRect.StretchModeEnum.Scale);
        tab.AddChild(plate);
        tab.AddChild(MakeLabel(text, 23, Ink, TitleFont, HorizontalAlignment.Center));
        row.AddChild(tab);
        var line = Rule(1.5f, new Color(Rose, 0.75f));
        line.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(line);
        if (trailing != null)
        {
            row.AddChild(trailing);
        }

        return row;
    }

    /// <summary>页脚居中的小马蹄铁装饰。</summary>
    public static Control FooterOrnament(float width = 300f)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 12);
        var left = Rule(1.5f, new Color(Rose, 0.7f));
        left.CustomMinimumSize = new Vector2(width * 0.38f, 1.5f);
        row.AddChild(left);
        row.AddChild(MakeIcon("horseshoe", 26, Rose));
        var right = Rule(1.5f, new Color(Rose, 0.7f));
        right.CustomMinimumSize = new Vector2(width * 0.38f, 1.5f);
        row.AddChild(right);
        return row;
    }

    /// <summary>昵称首字（座位圆章、立牌用）。</summary>
    public static string Initial(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var trimmed = name.Trim();
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(trimmed);
        return enumerator.MoveNext() ? enumerator.GetTextElement() : trimmed[..1];
    }

    /// <summary>
    /// 从书顶垂下的玩家书签：丝带 + 灰玫圆章（昵称首字）+ 昵称 + 退出按钮。
    /// 返回整体节点；退出按钮由调用方传入（复用场景里已有的按钮）。
    /// </summary>
    public static Control PlayerBookmark(string nickname, Button? logoutButton)
    {
        var root = new Control { Name = "PlayerBookmark", MouseFilter = Control.MouseFilterEnum.Ignore };

        // 纸条底：圆章右侧的浅纸胶囊，放昵称和退出。
        var strip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        strip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = PaperLight,
            BorderColor = new Color("d8c9b2"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 26,
            CornerRadiusTopRight = 26,
            CornerRadiusBottomLeft = 26,
            CornerRadiusBottomRight = 26,
            ContentMarginLeft = 66,
            ContentMarginRight = 10,
            ShadowColor = new Color(0.29f, 0.19f, 0.12f, 0.14f),
            ShadowSize = 5,
            ShadowOffset = new Vector2(0, 2),
            AntiAliasing = true
        });
        strip.Position = new Vector2(40, 64);
        strip.Size = new Vector2(250, 54);
        root.AddChild(strip);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        strip.AddChild(row);
        var name = MakeLabel(nickname, 19, Ink, BoldFont);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        name.ClipText = true;
        row.AddChild(name);
        if (logoutButton != null)
        {
            logoutButton.GetParent()?.RemoveChild(logoutButton);
            StyleIconButton(logoutButton, "logout", 40, "退出登录");
            logoutButton.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(logoutButton);
        }

        // 丝带从书顶（舞台 y≈0）垂到圆章后面。
        var ribbon = MakeImage("parts/ribbon", Vector2.Zero, TextureRect.StretchModeEnum.Scale);
        ribbon.Position = new Vector2(30, -70);
        ribbon.Size = new Vector2(70, 170);
        root.AddChild(ribbon);

        var seal = MakeImage("parts/seal", Vector2.Zero, TextureRect.StretchModeEnum.Scale);
        seal.Position = new Vector2(16, 50);
        seal.Size = new Vector2(84, 84);
        root.AddChild(seal);
        var initial = MakeLabel(Initial(nickname), 30, new Color("fbf4e8"), TitleFont, HorizontalAlignment.Center);
        initial.AddThemeConstantOverride("outline_size", 4);
        initial.AddThemeColorOverride("font_outline_color", new Color("8d5f57"));
        initial.Position = seal.Position;
        initial.Size = seal.Size;
        root.AddChild(initial);
        return root;
    }

    /// <summary>半透明压暗层（弹出书签卡 / 确认框时盖住整本书）。</summary>
    public static ColorRect Scrim(float alpha = 0.42f)
    {
        var scrim = new ColorRect { Color = new Color(0.16f, 0.1f, 0.07f, alpha), MouseFilter = Control.MouseFilterEnum.Stop };
        scrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return scrim;
    }
}
