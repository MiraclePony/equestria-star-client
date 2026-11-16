using System;
using Godot;
using EquestriaStar.Api;

namespace EquestriaStar.Game.UI;

/// <summary>
/// 左侧行动顺序列表中的一块玩家名牌：角色头像横幅（抠图 + 主题底色）上直接压无边框文字：玩家名、角色名、声望、金币。
/// 正在行动的玩家名牌放大并换金边；已行动、下一位用描边与角标区分（不只靠颜色）。
/// </summary>
public partial class OtherPlayerHudEntry : PanelContainer
{
    public enum TurnState
    {
        Waiting,
        Acted,
        Current,
        Next
    }

    // 横幅素材 760×220（3.45:1），名牌按同比例显示。
    public const float NormalWidth = 345f;
    public const float NormalHeight = 100f;
    public const float CurrentWidth = 386f;
    public const float CurrentHeight = 112f;

    [Export]
    public StyleBox? NormalStyle { get; set; }

    [Export]
    public StyleBox? HoverStyle { get; set; }

    private TextureRect _banner = null!;
    private Panel _frame = null!;
    private Control _names = null!;
    private Label _orderLabel = null!;
    private Label _nicknameLabel = null!;
    private Label _characterLabel = null!;
    private Label _prestigeValue = null!;
    private Label _coinsValue = null!;
    private TextureRect _stateIcon = null!;
    private bool _hovered;

    public event Action<OtherPlayerHudEntry, bool>? HoverChanged;

    public PlayerViewPlayerDto? Player { get; private set; }
    public TurnState State { get; private set; } = TurnState.Waiting;
    public bool IsSelf { get; private set; }
    public string NicknameText => IsNodeReady() ? _nicknameLabel.Text : "";

    public override void _Ready()
    {
        MouseDefaultCursorShape = CursorShape.PointingHand;
        BuildLayout();
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        ApplyState();

        if (Player != null)
        {
            ApplyPlayer();
        }
    }

    public override void _ExitTree()
    {
        MouseEntered -= OnMouseEntered;
        MouseExited -= OnMouseExited;
    }

    public void BindPlayer(PlayerViewPlayerDto player)
    {
        Player = player;
        if (IsNodeReady())
        {
            ApplyPlayer();
        }
    }

    public void SetTurnState(TurnState state, bool isSelf)
    {
        State = state;
        IsSelf = isSelf;
        if (IsNodeReady())
        {
            ApplyState();
        }
    }

    private void BuildLayout()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        PaperHudArt.AddDropShadow(this, 14, 6);

        // 底图：角色头像横幅（抠图 + 头部对焦 + 主题底色）；缺素材时退回卡面裁切。
        var holder = new Control { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(holder);
        var paper = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        paper.SetAnchorsPreset(LayoutPreset.FullRect);
        paper.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("f3e2c4"),
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12
        });
        holder.AddChild(paper);
        _banner = new TextureRect
        {
            Name = "Banner",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _banner.SetAnchorsPreset(LayoutPreset.FullRect);
        holder.AddChild(_banner);
        _frame = new Panel { Name = "Frame", MouseFilter = MouseFilterEnum.Ignore };
        _frame.SetAnchorsPreset(LayoutPreset.FullRect);
        holder.AddChild(_frame);

        // 玩家信息：无边框文字直接压在横幅右侧（白字深棕描边）。
        _names = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        _names.AnchorLeft = 0.4f;
        _names.AnchorRight = 1f;
        _names.AnchorBottom = 1f;
        _names.OffsetRight = -84;
        _names.AddThemeConstantOverride("separation", -4);
        holder.AddChild(_names);

        var nameRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        nameRow.AddThemeConstantOverride("separation", 4);
        _names.AddChild(nameRow);
        _nicknameLabel = PaperHudArt.MakeOverlayLabel("玩家昵称", 26, 8, font: PaperHudArt.TitleFont);
        _nicknameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _nicknameLabel.ClipText = true;
        _nicknameLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        nameRow.AddChild(_nicknameLabel);
        _stateIcon = PaperHudArt.MakeIcon("check", 26);
        _stateIcon.MouseFilter = MouseFilterEnum.Pass;
        nameRow.AddChild(_stateIcon);

        _characterLabel = PaperHudArt.MakeOverlayLabel("", 17, 6);
        _characterLabel.ClipText = true;
        _characterLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _names.AddChild(_characterLabel);

        // 真实回合序号（服务端 TurnOrder）：左上角奶油色小圆章。
        var orderBadge = new Control { Name = "OrderBadge", MouseFilter = MouseFilterEnum.Pass, TooltipText = "回合顺序", Position = new Vector2(-8, -8), Size = new Vector2(30, 30) };
        var orderArt = new TextureRect { Texture = PaperHudArt.ButtonArt("badge_cream"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        orderArt.SetAnchorsPreset(LayoutPreset.FullRect);
        orderBadge.AddChild(orderArt);
        _orderLabel = PaperHudArt.Glyph("-", 16);
        _orderLabel.Name = "OrderLabel";
        _orderLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        orderBadge.AddChild(_orderLabel);
        holder.AddChild(orderBadge);

        var resources = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        resources.AnchorLeft = 1f;
        resources.AnchorRight = 1f;
        resources.AnchorBottom = 1f;
        resources.OffsetLeft = -80;
        resources.OffsetRight = -10;
        resources.AddThemeConstantOverride("separation", 0);
        holder.AddChild(resources);
        _prestigeValue = AddStat(resources, "reputation", "声望");
        _coinsValue = AddStat(resources, "coin", "金币");
    }

    private static Label AddStat(Container parent, string icon, string tooltip)
    {
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass, TooltipText = tooltip };
        row.AddThemeConstantOverride("separation", 4);
        parent.AddChild(row);
        row.AddChild(PaperHudArt.MakeIcon(icon, 28));
        var value = PaperHudArt.MakeOverlayLabel("-", 24, 7);
        row.AddChild(value);
        return value;
    }

    private void ApplyPlayer()
    {
        if (Player == null)
        {
            return;
        }

        _nicknameLabel.Text = DisplayName(Player);
        _nicknameLabel.TooltipText = $"{_nicknameLabel.Text}（回合顺序 {Player.TurnOrder}）";
        // 副标题只写角色名，座位顺序由列表位置表达。
        _characterLabel.Text = string.IsNullOrWhiteSpace(Player.InitialCharacterName)
            ? ""
            : Player.InitialCharacterName!.Trim();
        _prestigeValue.Text = Player.Prestige.ToString();
        _coinsValue.Text = Player.Coins.ToString();
        _orderLabel.Text = Player.TurnOrder > 0 ? Player.TurnOrder.ToString() : "-";
        var banner = PaperHudArt.CharacterPortrait(Player.InitialCharacterCode, "banner");
        if (banner == null)
        {
            CardArtCatalog.TryGetCharacterTexture(Player.InitialCharacterCode, out var texture, out _);
            banner = PaperHudArt.Portrait(texture, 0.08f, 0.3f, 0.12f);
        }

        _banner.Texture = banner;
    }

    private void ApplyState()
    {
        var current = State == TurnState.Current;
        CustomMinimumSize = current
            ? new Vector2(CurrentWidth, CurrentHeight)
            : new Vector2(NormalWidth, NormalHeight);

        // 纸框：行动中 = 蜂蜜金粗边 + 金色光晕；下一位 = 天蓝边；其余 = 浅纸边。悬停提亮。
        var (border, width) = State switch
        {
            TurnState.Current => ("f2b33d", 5),
            TurnState.Next => ("8fc3e8", 4),
            _ => (_hovered ? "ffe39a" : "fff3dc", 4)
        };
        var frame = PaperHudArt.PortraitFrame(border, width, 12);
        if (current)
        {
            frame.ShadowColor = new Color(0.98f, 0.78f, 0.3f, 0.55f);
            frame.ShadowSize = 12;
        }

        _frame.AddThemeStyleboxOverride("panel", frame);
        Modulate = State == TurnState.Acted ? new Color(0.86f, 0.84f, 0.82f, 0.92f) : Colors.White;
        _nicknameLabel.AddThemeFontSizeOverride("font_size", current ? 30 : 26);

        // 状态角标：已行动 = 对勾，下一位 = 箭头，行动中 = 沙漏（不只靠颜色）。
        var iconName = State switch
        {
            TurnState.Acted => "check",
            TurnState.Next => "arrow_right",
            TurnState.Current => "round",
            _ => ""
        };
        _stateIcon.Visible = iconName.Length > 0;
        if (iconName.Length > 0)
        {
            _stateIcon.Texture = PaperHudArt.Icon(iconName);
        }

        _stateIcon.TooltipText = State switch
        {
            TurnState.Acted => "本轮已行动",
            TurnState.Next => "下一位行动",
            TurnState.Current => IsSelf ? "正在行动（你）" : "正在行动",
            _ => ""
        };
    }

    private void OnMouseEntered()
    {
        _hovered = true;
        ApplyState();
        HoverChanged?.Invoke(this, true);
    }

    private void OnMouseExited()
    {
        _hovered = false;
        ApplyState();
        HoverChanged?.Invoke(this, false);
    }

    private static string DisplayName(PlayerViewPlayerDto player)
    {
        return string.IsNullOrWhiteSpace(player.Nickname) ? $"玩家 {player.UserId}" : player.Nickname.Trim();
    }
}
