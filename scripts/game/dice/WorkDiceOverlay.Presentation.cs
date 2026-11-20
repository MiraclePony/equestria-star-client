using System;
using System.Collections.Generic;
using Godot;
using EquestriaStar.Api;
using EquestriaStar.Game.UI;

namespace EquestriaStar.Game.Dice;

/// <summary>
/// 骰子层的二维演出（“大骰判定”）：四周压暗、顶部难度徽章 + 属性签、停骰点数气泡、结果大横幅与算式。
/// 只做表现：所有新节点不接收鼠标、不加入 blocks_map_input，不改变投掷区、按钮命中、状态机、点数或服务端结果；
/// 文字全部来自待判定 / 结算回执里已有的字段，缺字段就不显示对应部分。动画可随时被关闭打断，不留 Tween。
/// </summary>
public partial class WorkDiceOverlay
{
    private const string DiceArtRoot = "res://assets/art/ui/paper_hud/dice/";

    private static readonly Dictionary<string, (string Text, string Icon)> AttributeLook = new(StringComparer.OrdinalIgnoreCase)
    {
        ["STRENGTH"] = ("力量", "strength"),
        ["SPEED"] = ("速度", "speed"),
        ["SOCIAL"] = ("社交", "social"),
        ["CHARM"] = ("魅力", "charm"),
        ["MAGIC"] = ("魔法", "magic"),
        ["MIND"] = ("心智", "mind")
    };

    private TextureRect? _vignette;
    private Control? _difficultyBadge;
    private Label? _difficultyValue;
    private Label? _difficultyCaption;
    private Control? _attributeTag;
    private TextureRect? _attributeTagIcon;
    private Label? _attributeTagLabel;
    private Control? _rollBubble;
    private Label? _rollValue;
    private Control? _resultBanner;
    private TextureRect? _resultBannerArt;
    private Label? _resultBannerTitle;
    private Label? _resultEquation;
    private Tween? _rollTween;
    private Tween? _bannerTween;

    private bool _presentationBuilt;

    /// <summary>_Ready 时只给属性按钮配图标；演出节点等第一次真正需要时再创建，不拖慢骰子层载入。</summary>
    private void SetupPresentation()
    {
        foreach (var (attribute, look) in AttributeLook)
        {
            if (_attributeButtons.TryGetValue(attribute, out var button))
            {
                button.Icon = PaperHudArt.Icon(look.Icon);
                button.ExpandIcon = false;
                button.AddThemeConstantOverride("icon_max_width", 34);
                button.AddThemeConstantOverride("h_separation", 10);
                button.AddThemeFontSizeOverride("font_size", 22);
            }
        }
    }

    private void EnsurePresentation()
    {
        if (_presentationBuilt)
        {
            return;
        }

        _presentationBuilt = true;
        // 压暗层：在 3D 骰盘之上、投掷输入区之下，只影响观感。
        _vignette = new TextureRect
        {
            Name = "PresentationVignette",
            Texture = LoadDiceArt("vignette"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _vignette.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_vignette);
        _root.MoveChild(_vignette, _diceViewportContainer.GetIndex() + 1);

        // 难度徽章：贴在信息纸板左侧。
        _difficultyBadge = MakeArtHolder("PresentationDifficulty", "dc_badge", new Vector2(208, 208), new Vector2(196, 18));
        _difficultyCaption = MakeCenteredLabel(_difficultyBadge, "难度", 22, PaperHudArt.InkSoft, new Vector2(0, 46), new Vector2(208, 32));
        _difficultyValue = MakeCenteredLabel(_difficultyBadge, "-", 76, PaperHudArt.Ink, new Vector2(0, 70), new Vector2(208, 90), title: true);
        _root.AddChild(_difficultyBadge);

        _attributeTag = MakeArtHolder("PresentationAttribute", "attr_tag", new Vector2(220, 74), new Vector2(190, 222));
        var tagRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        tagRow.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        tagRow.OffsetBottom = -4;
        tagRow.AddThemeConstantOverride("separation", 8);
        _attributeTagIcon = PaperHudArt.MakeIcon("social", 34);
        tagRow.AddChild(_attributeTagIcon);
        _attributeTagLabel = PaperHudArt.MakeLabel("-", 24);
        tagRow.AddChild(_attributeTagLabel);
        _attributeTag.AddChild(tagRow);
        _root.AddChild(_attributeTag);

        // 停骰点数气泡：贴在信息纸板右侧，骰子停稳时弹出。
        _rollBubble = MakeArtHolder("PresentationRoll", "roll_bubble", new Vector2(176, 176), new Vector2(1548, 34));
        MakeCenteredLabel(_rollBubble, "点数", 18, PaperHudArt.InkSoft, new Vector2(0, 34), new Vector2(176, 26));
        _rollValue = MakeCenteredLabel(_rollBubble, "-", 72, PaperHudArt.Ink, new Vector2(0, 54), new Vector2(176, 90), title: true);
        _rollBubble.PivotOffset = new Vector2(88, 88);
        _root.AddChild(_rollBubble);

        // 结果大横幅：结果纸板正上方。
        _resultBanner = new Control { Name = "PresentationResultBanner", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _resultBanner.AnchorLeft = 0.5f;
        _resultBanner.AnchorRight = 0.5f;
        _resultBanner.AnchorTop = 0.5f;
        _resultBanner.AnchorBottom = 0.5f;
        // 缎带素材 638×181（上方带星星），成功 / 失败共用同一画布尺寸。
        _resultBanner.OffsetLeft = -320;
        _resultBanner.OffsetRight = 320;
        _resultBanner.OffsetTop = -352;
        _resultBanner.OffsetBottom = -170;
        _resultBanner.PivotOffset = new Vector2(320, 105);
        _resultBannerArt = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _resultBannerArt.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _resultBanner.AddChild(_resultBannerArt);
        _resultBannerTitle = MakeCenteredLabel(_resultBanner, "", 50, PaperHudArt.Ink, new Vector2(0, 50), new Vector2(640, 72), title: true);
        // 算式写在缎带下半部（缎带下方紧贴结果纸板，放外面会被挡住）。
        _resultEquation = PaperHudArt.MakeLabel("", 21, PaperHudArt.Ink, align: HorizontalAlignment.Center);
        _resultEquation.Position = new Vector2(0, 118);
        _resultEquation.Size = new Vector2(640, 32);
        _resultBanner.AddChild(_resultEquation);
        _root.AddChild(_resultBanner);

        HidePresentation();
    }

    /// <summary>进入 / 恢复 / 重投授权后的待投掷状态：显示难度和属性，点数气泡清空。</summary>
    private void PresentPending(PendingCheckDto pending, bool resetRoll)
    {
        EnsurePresentation();

        var forest = IsForestMove(pending);
        SetPresentationVisible(true);
        _difficultyBadge!.Visible = !forest && pending.Difficulty.HasValue;
        _difficultyValue!.Text = pending.Difficulty?.ToString() ?? "-";
        var hasAttribute = !forest && !string.IsNullOrWhiteSpace(pending.AttributeType);
        _attributeTag!.Visible = hasAttribute;
        if (hasAttribute)
        {
            var key = pending.AttributeType!.Trim();
            var look = AttributeLook.TryGetValue(key, out var known) ? known : (Text: key, Icon: "round");
            _attributeTagIcon!.Texture = PaperHudArt.Icon(look.Icon);
            _attributeTagLabel!.Text = pending.AttributeValue.HasValue ? $"{look.Text} {pending.AttributeValue.Value}" : look.Text;
        }

        if (resetRoll)
        {
            KillTween(ref _rollTween);
            _rollBubble!.Visible = false;
            _rollBubble.Scale = Vector2.One;
        }

        HideResultBanner();
    }

    /// <summary>物理骰停稳：点数气泡弹出（只显示本地物理点数，确认前不上传）。</summary>
    private void PresentRoll(int value)
    {
        EnsurePresentation();

        _rollValue!.Text = value.ToString();
        _rollBubble!.Visible = true;
        KillTween(ref _rollTween);
        _rollBubble.Scale = new Vector2(0.4f, 0.4f);
        _rollTween = _rollBubble.CreateTween();
        _rollTween.TweenProperty(_rollBubble, "scale", Vector2.One, 0.32).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    /// <summary>打工 / 任务 / 招募判定结果：成功或失败横幅 + 服务端回执算式。</summary>
    private void PresentCheckResult(CheckRollResponseDto response, string successText, string failText)
    {
        var check = response.CheckResult;
        var success = check?.Success == true;
        ShowResultBanner(success, success ? successText : failText, BuildEquation(check, response.EffectiveDice));
    }

    /// <summary>森林方向骰结果：抵达或原地停留（不画成判定成败）。</summary>
    private void PresentForestResult(bool moved, int effectiveDice)
    {
        ShowResultBanner(moved, moved ? "已抵达" : "原地停留", $"方向骰 {effectiveDice}");
    }

    private void ShowResultBanner(bool positive, string title, string equation)
    {
        EnsurePresentation();

        _rollBubble!.Visible = false;
        _resultBannerArt!.Texture = LoadDiceArt(positive ? "ribbon_success" : "ribbon_fail");
        _resultBannerTitle!.Text = title;
        _resultBannerTitle.AddThemeColorOverride("font_color", positive ? PaperHudArt.Ink : new Color("6b5560"));
        _resultEquation!.Text = equation;
        _resultEquation.Visible = equation.Length > 0;
        _resultBanner!.Visible = true;
        KillTween(ref _bannerTween);
        _resultBanner.Scale = new Vector2(0.6f, 0.6f);
        _resultBanner.Modulate = new Color(1, 1, 1, 0);
        _bannerTween = _resultBanner.CreateTween().SetParallel();
        _bannerTween.TweenProperty(_resultBanner, "scale", Vector2.One, 0.38).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _bannerTween.TweenProperty(_resultBanner, "modulate:a", 1f, 0.2);
    }

    /// <summary>算式只用回执里实际给出的字段：骰点 + 属性值（+ 修正）= 总值 · 难度。</summary>
    private static string BuildEquation(CheckResultDto? check, int? effectiveDice)
    {
        var dice = check?.Dice ?? effectiveDice;
        if (check == null || !dice.HasValue)
        {
            return dice.HasValue ? $"骰点 {dice.Value}" : "";
        }

        var parts = $"骰点 {dice.Value}";
        if (check.AttributeValue.HasValue)
        {
            var name = FirstNonEmpty(check.AttributeTypeText,
                AttributeLook.TryGetValue(check.AttributeType ?? "", out var look) ? look.Text : "属性");
            parts += $" + {name} {check.AttributeValue.Value}";
        }

        if (check.Modifier is { } modifier && modifier != 0)
        {
            parts += modifier > 0 ? $" + 修正 {modifier}" : $" − 修正 {-modifier}";
        }

        if (check.FinalValue.HasValue)
        {
            parts += $" = {check.FinalValue.Value}";
        }

        if (check.Difficulty.HasValue)
        {
            parts += $"　·　难度 {check.Difficulty.Value}";
        }

        return parts;
    }

    private void HideResultBanner()
    {
        KillTween(ref _bannerTween);
        if (_resultBanner != null)
        {
            _resultBanner.Visible = false;
        }
    }

    private void HidePresentation()
    {
        KillTween(ref _rollTween);
        HideResultBanner();
        SetPresentationVisible(false);
        if (_rollBubble != null)
        {
            _rollBubble.Visible = false;
        }
    }

    private void SetPresentationVisible(bool visible)
    {
        if (_vignette != null) _vignette.Visible = visible;
        if (_difficultyBadge != null) _difficultyBadge.Visible = visible;
        if (_attributeTag != null) _attributeTag.Visible = visible;
    }

    private static void KillTween(ref Tween? tween)
    {
        if (tween != null && GodotObject.IsInstanceValid(tween))
        {
            tween.Kill();
        }

        tween = null;
    }

    private static Texture2D? LoadDiceArt(string name)
    {
        var path = $"{DiceArtRoot}{name}.png";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    private static Control MakeArtHolder(string name, string art, Vector2 size, Vector2 position)
    {
        var holder = new Control { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore, Position = position, Size = size, Visible = false };
        var texture = new TextureRect
        {
            Texture = LoadDiceArt(art),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        texture.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(texture);
        return holder;
    }

    private static Label MakeCenteredLabel(Control parent, string text, int size, Color color, Vector2 position, Vector2 box, bool title = false)
    {
        var label = PaperHudArt.MakeLabel(text, size, color, align: HorizontalAlignment.Center);
        if (title)
        {
            // 大号数字 / 横幅标题用粗体（与 HUD 共用同一字体资源，避免额外字体匹配）。
            label.AddThemeFontOverride("font", PaperHudArt.BoldFont);
        }

        label.Position = position;
        label.Size = box;
        parent.AddChild(label);
        return label;
    }
}
