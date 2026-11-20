using System;
using System.Collections.Generic;
using Godot;

namespace EquestriaStar.Game.UI;

/// <summary>
/// 折纸风 HUD 素材的集中入口。素材位于 res://assets/art/ui/paper_hud/，
/// 只包含纸框、按钮底、图标；卡面一律来自现有卡图（CardArtCatalog），不在这里生成。
/// </summary>
public static class PaperHudArt
{
    private const string Root = "res://assets/art/ui/paper_hud/";

    // 文字与描边统一使用深可可棕。
    public static readonly Color Ink = new("5a3a28");
    public static readonly Color InkSoft = new("7a5a44");
    public static readonly Color Disabled = new(0.62f, 0.6f, 0.58f, 1f);

    private static readonly Dictionary<string, Texture2D?> Cache = new(StringComparer.Ordinal);
    private static FontVariation? _titleFont;
    private static FontVariation? _boldFont;

    public static Texture2D? Icon(string name) => Load($"icons/{name}.png");
    public static Texture2D? ButtonArt(string name) => Load($"buttons/{name}.png");
    public static Texture2D? Panel(string name) => Load($"panels/{name}.png");
    public static Texture2D? CardBit(string name) => Load($"cards/{name}.png");

    public static Texture2D? Load(string relativePath)
    {
        var path = Root + relativePath;
        if (Cache.TryGetValue(path, out var cached) && (cached == null || GodotObject.IsInstanceValid(cached)))
        {
            return cached;
        }

        var texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        if (texture == null)
        {
            GD.PushWarning($"折纸 HUD 素材缺失：{path}");
        }

        Cache[path] = texture;
        return texture;
    }

    /// <summary>九宫格纸框。margin 为素材像素，contentMargin 为内容内边距。</summary>
    public static StyleBox NinePatch(Texture2D? texture, int margin, float contentMargin = -1f, float scale = 1f)
    {
        if (texture == null)
        {
            return new StyleBoxFlat
            {
                BgColor = new Color("fbf3e2"),
                CornerRadiusTopLeft = 12,
                CornerRadiusTopRight = 12,
                CornerRadiusBottomLeft = 12,
                CornerRadiusBottomRight = 12
            };
        }

        var box = new StyleBoxTexture
        {
            Texture = texture,
            TextureMarginLeft = margin,
            TextureMarginTop = margin,
            TextureMarginRight = margin,
            TextureMarginBottom = margin,
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Stretch,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Stretch
        };
        var content = contentMargin >= 0 ? contentMargin : margin * scale * 0.6f;
        box.ContentMarginLeft = content;
        box.ContentMarginTop = content;
        box.ContentMarginRight = content;
        box.ContentMarginBottom = content;
        return box;
    }

    /// <summary>标题用的圆润粗体（结束回合等）。优先系统里的可爱圆体，找不到就用雅黑粗体。</summary>
    public static Font TitleFont
    {
        get
        {
            if (_titleFont != null)
            {
                return _titleFont;
            }

            var system = new SystemFont
            {
                FontNames = ["ZCOOL KuaiLe", "站酷快乐体", "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC"],
                FontWeight = 800,
                Antialiasing = TextServer.FontAntialiasing.Gray
            };
            _titleFont = new FontVariation { BaseFont = system, VariationEmbolden = 0.35f };
            return _titleFont;
        }
    }

    /// <summary>正文粗体：名牌昵称、数值。</summary>
    public static Font BoldFont
    {
        get
        {
            if (_boldFont != null)
            {
                return _boldFont;
            }

            var system = new SystemFont
            {
                FontNames = ["Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC"],
                FontWeight = 700
            };
            _boldFont = new FontVariation { BaseFont = system };
            return _boldFont;
        }
    }

    public static Label MakeLabel(string text, int size, Color? color = null, bool bold = true, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = align,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            // 默认不截断：数值标签需要按文字宽度占位；需要截断的长文本由调用方单独开启。
            TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming,
            ClipText = false
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Ink);
        if (bold)
        {
            label.AddThemeFontOverride("font", BoldFont);
        }

        return label;
    }

    public static TextureRect MakeIcon(string name, float size)
    {
        return new TextureRect
        {
            Texture = Icon(name),
            CustomMinimumSize = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    /// <summary>图标 + 数字的纸胶囊（金币、声望、六维、区名）。返回胶囊与数值 Label。</summary>
    public static (PanelContainer Pill, Label Value) MakePill(string iconName, string panelArt, Vector2 minSize, int fontSize, float iconSize, string tooltip = "")
    {
        var pill = new PanelContainer
        {
            CustomMinimumSize = minSize,
            MouseFilter = string.IsNullOrEmpty(tooltip) ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Pass,
            TooltipText = tooltip
        };
        // 小胶囊整体缩放纸面贴图（九宫格角太大，会挤满小控件）。
        pill.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var art = panelArt.StartsWith("pill_", StringComparison.Ordinal) ? ButtonArt(panelArt) : Panel(panelArt);
        pill.AddChild(new TextureRect
        {
            Texture = art,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 4);
        pill.AddChild(row);
        row.AddChild(MakeIcon(iconName, iconSize));
        var value = MakeLabel("-", fontSize);
        row.AddChild(value);
        return (pill, value);
    }

    /// <summary>纸质圆章按钮的四态贴图。</summary>
    public static void ApplyRoundButton(TextureButton button, string normal, string? hover, string? pressed, string? disabled)
    {
        button.TextureNormal = ButtonArt(normal);
        button.TextureHover = hover == null ? null : ButtonArt(hover);
        button.TexturePressed = pressed == null ? null : ButtonArt(pressed);
        button.TextureDisabled = disabled == null ? null : ButtonArt(disabled);
        button.IgnoreTextureSize = true;
        button.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
    }

    /// <summary>
    /// 统一纸片投影：在目标控件下方画一层柔和投影（偏移 0,6、模糊 10、暖棕 30%），
    /// 让纸片“立在地图上”。投影节点不接收鼠标。
    /// </summary>
    public static void AddDropShadow(Control target, int radius = 14, float inset = 6f)
    {
        var shadow = new Panel
        {
            Name = "PaperShadow",
            ShowBehindParent = true,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        shadow.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        shadow.OffsetLeft = inset;
        shadow.OffsetRight = -inset;
        shadow.OffsetTop = inset;
        shadow.OffsetBottom = -inset;
        shadow.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            // 只画投影，不画底色（避免从纸片手撕边的透明处透出深色带）。
            BgColor = new Color(0.35f, 0.22f, 0.1f, 0f),
            ShadowColor = new Color(0.35f, 0.22f, 0.1f, 0.30f),
            ShadowSize = 10,
            ShadowOffset = new Vector2(0, 6),
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius
        });
        target.AddChild(shadow);
    }

    /// <summary>从角色 / 伙伴卡面裁出胸像区域（卡面上部立绘）。</summary>
    public static Texture2D? Portrait(Texture2D? card, float top = 0.07f, float bottom = 0.52f, float side = 0.1f)
    {
        if (card == null)
        {
            return null;
        }

        var size = card.GetSize();
        return new AtlasTexture
        {
            Atlas = card,
            Region = new Rect2(size.X * side, size.Y * top, size.X * (1f - side * 2f), size.Y * (bottom - top))
        };
    }

    /// <summary>
    /// 角色头像（GPT 从角色卡抠出并补全、头部对焦、主题底色）：kind = "banner"（760×220 名牌横幅）或 "square"（400×400 底栏）。
    /// 角色编码去掉 INITIAL_ 前缀后小写即文件名；没有对应素材时返回 null，由调用方退回卡面裁切。
    /// </summary>
    public static Texture2D? CharacterPortrait(string? characterCode, string kind)
    {
        if (string.IsNullOrWhiteSpace(characterCode))
        {
            return null;
        }

        var slug = characterCode.Trim().ToLowerInvariant();
        if (slug.StartsWith("initial_", StringComparison.Ordinal))
        {
            slug = slug["initial_".Length..];
        }

        var path = $"{Root}portraits/{kind}_{slug}.png";
        if (Cache.TryGetValue(path, out var cached) && (cached == null || GodotObject.IsInstanceValid(cached)))
        {
            return cached;
        }

        var texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        Cache[path] = texture;
        return texture;
    }

    /// <summary>直接压在头像图上的无边框文字：白字 + 深可可棕描边。</summary>
    public static Label MakeOverlayLabel(string text, int size, int outline = 8, Color? color = null, Font? font = null)
    {
        var label = MakeLabel(text, size, color ?? new Color("fffaf0"));
        label.AddThemeConstantOverride("outline_size", outline);
        label.AddThemeColorOverride("font_outline_color", Ink);
        label.AddThemeConstantOverride("shadow_offset_x", 0);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        label.AddThemeConstantOverride("shadow_outline_size", outline);
        label.AddThemeColorOverride("font_shadow_color", new Color(0.2f, 0.12f, 0.06f, 0.35f));
        if (font != null)
        {
            label.AddThemeFontOverride("font", font);
        }

        return label;
    }

    /// <summary>头像外的纸框：浅纸边 + 外圈暖棕细线（圆角与烘焙圆角一致）。</summary>
    public static StyleBoxFlat PortraitFrame(string border, int width, int radius) => new()
    {
        DrawCenter = false,
        BorderColor = new Color(border),
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        AntiAliasing = true
    };

    /// <summary>深棕色小三角 / 加号等字形标签（替代彩色箭头图标）。</summary>
    public static Label Glyph(string text, int size, Color? color = null)
    {
        var label = MakeLabel(text, size, color ?? Ink, align: HorizontalAlignment.Center);
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }

    /// <summary>纸条行：奶油纸底、细米色描边、圆角 10（抽屉列表行、菜单行共用）。</summary>
    public static StyleBoxFlat PaperStrip(string fill = "fbead0", string border = "d9b98e", int radius = 10) => new()
    {
        BgColor = new Color(fill),
        BorderColor = new Color(border),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 2,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        ContentMarginLeft = 10,
        ContentMarginRight = 10,
        ContentMarginTop = 6,
        ContentMarginBottom = 6
    };
}
