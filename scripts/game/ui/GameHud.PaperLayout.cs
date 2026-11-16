using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using EquestriaStar.Api;

namespace EquestriaStar.Game.UI;

/// <summary>
/// 折纸风第一层 HUD 的版式（按 2026-11 定稿 m1–m3）：
/// 左侧玩家名牌（行动顺序）、底部玩家状态栏（角色卡头像、六维、伙伴、道具、装备、操作簇）、
/// 右侧公共任务 / 集市拉手、顶部聊天 / 历史抽屉。业务资格判断仍在 GameHud.cs。
/// </summary>
public partial class GameHud
{
    public const int PartnerSlotCount = 5;
    public const int EquipmentSlotCount = 2;
    private const int VisibleItemCards = 3;
    private static readonly Vector2 CardSize = new(96, 144);

    [Signal]
    public delegate void HistoryRequestedEventHandler();

    private Label _roundLabel = null!;
    private TextureRect _selfAvatar = null!;
    private TextureRect _syncIcon = null!;
    private Label _partnerCountLabel = null!;
    private readonly List<(TextureRect Art, TextureRect Empty)> _partnerSlots = [];
    private Label _itemCountLabel = null!;
    private readonly List<TextureRect> _itemCards = [];
    private Control _itemStack = null!;
    private Label _itemStackLabel = null!;
    private Label _itemEmptyLabel = null!;
    private Label _equipCountLabel = null!;
    private readonly List<(TextureRect Art, TextureRect Empty)> _equipSlots = [];
    private Label _questsBadgeLabel = null!;
    private Control _questsBadge = null!;
    private PanelContainer _chatPanel = null!;
    private PanelContainer _historyPanel = null!;
    private VBoxContainer _historyList = null!;
    private Label _historyStatusLabel = null!;
    private Button _chatHandle = null!;
    private Button _historyHandle = null!;
    private bool _paperLayoutBuilt;

    public Button ChatHandle => _chatHandle;
    public Button HistoryHandle => _historyHandle;
    public bool IsHistoryOpen => GodotObject.IsInstanceValid(_historyPanel) && _historyPanel.Visible;
    public bool IsChatOpen => GodotObject.IsInstanceValid(_chatPanel) && _chatPanel.Visible;
    public int VisibleItemCardCount => _itemCards.Count(card => card.Visible);
    public int PartnerCardCount => _partnerSlots.Count(slot => slot.Art.Visible);
    public int EquippedCardCount => _equipSlots.Count(slot => slot.Art.Visible);
    public string CurrentCoinsText => GodotObject.IsInstanceValid(_coinsValueLabel) ? _coinsValueLabel.Text : "";
    public string RoundText => GodotObject.IsInstanceValid(_roundLabel) ? _roundLabel.Text : "";

    private void BuildPaperLayout()
    {
        // 回合提示（需求 B01）：缩成小纸签，放在两个顶部抽屉之间、层级低于抽屉面板，不再占据顶部中央。
        var banner = _hudRoot.GetNode<TextureRect>("TurnNoticeAnchor/TurnNoticePanel/BannerTexture");
        banner.StretchMode = TextureRect.StretchModeEnum.Scale;
        _hudRoot.GetNode<Control>("TurnNoticeAnchor/TurnNoticePanel").CustomMinimumSize = new Vector2(270, 44);
        var turnLabel = _hudRoot.GetNode<Label>("TurnNoticeAnchor/TurnNoticePanel/TurnNoticeLabel");
        turnLabel.OffsetLeft = 22;
        turnLabel.OffsetRight = -22;
        turnLabel.OffsetTop = 3;
        turnLabel.OffsetBottom = -7;
        turnLabel.AddThemeFontSizeOverride("font_size", 17);
        turnLabel.AddThemeColorOverride("font_color", PaperHudArt.Ink);
        turnLabel.AddThemeFontOverride("font", PaperHudArt.BoldFont);
        var turnAnchor = _hudRoot.GetNode<Control>("TurnNoticeAnchor");
        turnAnchor.OffsetTop = 92;
        turnAnchor.OffsetBottom = 140;
        var connection = _hudRoot.GetNode<Control>("ConnectionNoticeAnchor");
        connection.OffsetTop = 142;
        connection.OffsetBottom = 178;

        var detail = _hudRoot.GetNode<PanelContainer>("PlayerDetailPanel");
        detail.AddThemeStyleboxOverride("panel", PaperHudArt.NinePatch(PaperHudArt.Panel("drawer_cream"), 40, 18));

        BuildLeftRail();
        BuildTopRight();
        BuildRightDrawers();
        BuildTopDrawers();
        BuildBottomBar();
        BuildActionMenu();

        _paperLayoutBuilt = true;
        SetInventoryPreview(null);
        SetPartnerPreview(null);

        // 名牌悬停详情要在所有新节点之上。
        _hudRoot.MoveChild(detail, _hudRoot.GetChildCount() - 1);
    }

    // ───────────── 左侧：轮次 + 玩家名牌 ─────────────

    private void BuildLeftRail()
    {
        var margin = new MarginContainer { Name = "LeftMargin", MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        margin.OffsetLeft = 14;
        margin.OffsetTop = 10;
        margin.OffsetRight = 410;
        margin.OffsetBottom = -260;
        _hudRoot.AddChild(margin);

        var rail = new VBoxContainer { Name = "LeftRail", MouseFilter = Control.MouseFilterEnum.Ignore };
        rail.AddThemeConstantOverride("separation", 22);
        margin.AddChild(rail);

        // 轮次：沙漏 + 数字压在一枚纸片上。
        var roundTag = new PanelContainer
        {
            Name = "RoundTag",
            MouseFilter = Control.MouseFilterEnum.Pass,
            TooltipText = "当前轮次",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(115, 56)
        };
        roundTag.AddThemeStyleboxOverride("panel", PaperHudArt.NinePatch(PaperHudArt.Panel("tag"), 30, 8));
        roundTag.AddToGroup("blocks_map_input");
        PaperHudArt.AddDropShadow(roundTag, 12, 8);
        var roundRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        roundRow.AddThemeConstantOverride("separation", 6);
        roundTag.AddChild(roundRow);
        roundRow.AddChild(PaperHudArt.MakeIcon("round", 36));
        _roundLabel = PaperHudArt.MakeLabel("-", 30);
        roundRow.AddChild(_roundLabel);
        rail.AddChild(roundTag);

        var panel = new Control { Name = "OtherPlayersPanel", MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        rail.AddChild(panel);
        _otherPlayersScroll = new ScrollContainer
        {
            Name = "OtherPlayersScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            // 可滚动但不显示滚动条（名牌列超过可用高度时用滚轮浏览）。
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever
        };
        _otherPlayersScroll.AddToGroup("blocks_map_input");
        // 顶部留出胸像越框的空间；滚动区不裁掉越框的头像。
        _otherPlayersScroll.Position = new Vector2(0, 0);
        _otherPlayersScroll.Size = new Vector2(392, 120);
        _otherPlayersScroll.ClipContents = false;
        panel.AddChild(_otherPlayersScroll);
        _otherPlayersList = new VBoxContainer { Name = "OtherPlayersList", MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _otherPlayersList.AddThemeConstantOverride("separation", 22);
        _otherPlayersScroll.AddChild(_otherPlayersList);
        _emptyOtherPlayersLabel = PaperHudArt.MakeLabel("暂无其他玩家", 15, PaperHudArt.InkSoft);
        _emptyOtherPlayersLabel.Name = "EmptyLabel";
        _emptyOtherPlayersLabel.CustomMinimumSize = new Vector2(300, 60);
        _emptyOtherPlayersLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _otherPlayersList.AddChild(_emptyOtherPlayersLabel);
    }

    // ───────────── 右上：地图标识 ─────────────

    private void BuildTopRight()
    {
        var anchor = new MarginContainer { Name = "TopRightAnchor", MouseFilter = Control.MouseFilterEnum.Ignore };
        anchor.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        // 地图标识：80×64 方形纸片 + 地图图标；地图名放在悬停提示里（标识仅此一处，需求 B05）。
        anchor.OffsetLeft = -100;
        anchor.OffsetTop = 6;
        anchor.OffsetRight = -16;
        anchor.OffsetBottom = 72;
        anchor.GrowHorizontal = Control.GrowDirection.Begin;
        _hudRoot.AddChild(anchor);
        var column = new VBoxContainer { Name = "TopRight", MouseFilter = Control.MouseFilterEnum.Ignore };
        anchor.AddChild(column);
        var indicator = new Control { Name = "MapIndicator", CustomMinimumSize = new Vector2(80, 64), MouseFilter = Control.MouseFilterEnum.Pass, TooltipText = "正式地图" };
        indicator.AddToGroup("blocks_map_input");
        column.AddChild(indicator);
        PaperHudArt.AddDropShadow(indicator, 12, 6);
        var texture = new TextureRect
        {
            Name = "Texture",
            Texture = PaperHudArt.Panel("drawer_cream"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        texture.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        indicator.AddChild(texture);
        var icon = PaperHudArt.MakeIcon("map", 46);
        icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        icon.OffsetLeft = 12;
        icon.OffsetTop = 8;
        icon.OffsetRight = -12;
        icon.OffsetBottom = -10;
        indicator.AddChild(icon);
        var label = PaperHudArt.MakeLabel("正式地图", 14);
        label.Name = "Label";
        label.Visible = false;
        indicator.AddChild(label);
    }

    // ───────────── 右侧：公共任务 / 集市拉手 ─────────────

    private void BuildRightDrawers()
    {
        var drawers = new VBoxContainer { Name = "RightDrawers", MouseFilter = Control.MouseFilterEnum.Ignore };
        drawers.SetAnchorsPreset(Control.LayoutPreset.CenterRight);
        drawers.OffsetLeft = -80;
        drawers.OffsetRight = 0;
        drawers.OffsetTop = -360;
        drawers.OffsetBottom = 140;
        drawers.GrowHorizontal = Control.GrowDirection.Begin;
        drawers.AddThemeConstantOverride("separation", 36);
        _hudRoot.AddChild(drawers);

        _questBoardButton = MakeDrawerHandle("QuestBoardButton", "handle_lav", "quest", "展开公共任务板");
        _shopShelfButton = MakeDrawerHandle("ShopShelfButton", "handle_mint", "market", "展开公共集市");
        drawers.AddChild(_questBoardButton);
        drawers.AddChild(_shopShelfButton);
    }

    private static Button MakeDrawerHandle(string name, string art, string icon, string tooltip)
    {
        var button = new Button
        {
            Name = name,
            CustomMinimumSize = new Vector2(80, 220),
            TooltipText = tooltip,
            Flat = false,
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        button.AddToGroup("blocks_map_input");
        ApplyTextureStates(button, PaperHudArt.Panel(art), 24);
        PaperHudArt.AddDropShadow(button, 14, 6);

        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        column.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        column.OffsetLeft = 8;
        column.AddThemeConstantOverride("separation", 14);
        button.AddChild(column);
        column.AddChild(PaperHudArt.MakeIcon(icon, 56));
        // 深棕 ◀ 小三角：向屏幕内拉出。
        column.AddChild(PaperHudArt.Glyph("◀", 20));
        return button;
    }

    // ───────────── 顶部：聊天 / 历史抽屉 ─────────────

    private const float TopHandleWidth = 210f;
    private const float TopHandleHeight = 62f;
    private const float DrawerWidth = 520f;

    private ScrollContainer _historyScroll = null!;

    private void BuildTopDrawers()
    {
        var drawers = new Control { Name = "TopDrawers", MouseFilter = Control.MouseFilterEnum.Ignore };
        drawers.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        drawers.OffsetBottom = 70;
        _hudRoot.AddChild(drawers);

        // 聊天：拉手挂在左上（名牌列右侧），面板从拉手下沿垂下，两者接成一体。
        _chatPanel = MakeDrawerPanel("ChatPanel");
        _chatPanel.Position = new Vector2(430, TopHandleHeight - 10);
        drawers.AddChild(_chatPanel);
        _chatHandle = MakeTopHandle("ChatHandle", "chat", "聊天");
        _chatHandle.Position = new Vector2(430 + (DrawerWidth - TopHandleWidth) / 2f, 0);
        drawers.AddChild(_chatHandle);
        var chatContent = (VBoxContainer)_chatPanel.GetChild(1);
        var chatNotice = PaperHudArt.MakeLabel("聊天功能暂未开放", 20, PaperHudArt.InkSoft, align: HorizontalAlignment.Center);
        chatNotice.CustomMinimumSize = new Vector2(0, 150);
        chatContent.AddChild(chatNotice);
        var inputRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        inputRow.AddThemeConstantOverride("separation", 10);
        chatContent.AddChild(inputRow);
        var input = new LineEdit { Editable = false, PlaceholderText = "暂未开放", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 48) };
        var inputBox = PaperHudArt.PaperStrip("eadbc4", "d2bb9a", 14);
        input.AddThemeStyleboxOverride("normal", inputBox);
        input.AddThemeStyleboxOverride("read_only", inputBox);
        input.AddThemeColorOverride("font_placeholder_color", new Color("a08a70"));
        input.AddThemeFontSizeOverride("font_size", 18);
        inputRow.AddChild(input);
        var send = MakeRoundButton("ChatSendButton", "end_turn_normal", "end_turn_hover", "send", "聊天功能暂未开放", 52);
        send.Disabled = true;
        inputRow.AddChild(send);
        AddCollapseTab(_chatPanel, OnChatHandlePressed);

        // 历史：拉手挂在右上（地图标识左侧）。
        _historyPanel = MakeDrawerPanel("HistoryPanel");
        _historyPanel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _historyPanel.OffsetLeft = -120 - DrawerWidth;
        _historyPanel.OffsetRight = -120;
        _historyPanel.OffsetTop = TopHandleHeight - 10;
        drawers.AddChild(_historyPanel);
        _historyHandle = MakeTopHandle("HistoryHandle", "history", "历史记录");
        _historyHandle.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _historyHandle.OffsetLeft = -120 - (DrawerWidth + TopHandleWidth) / 2f;
        _historyHandle.OffsetRight = _historyHandle.OffsetLeft + TopHandleWidth;
        _historyHandle.OffsetTop = 0;
        _historyHandle.OffsetBottom = TopHandleHeight;
        drawers.AddChild(_historyHandle);
        var historyContent = (VBoxContainer)_historyPanel.GetChild(1);
        _historyStatusLabel = PaperHudArt.MakeLabel("展开后读取最近行动", 18, PaperHudArt.InkSoft, align: HorizontalAlignment.Center);
        _historyStatusLabel.CustomMinimumSize = new Vector2(0, 60);
        historyContent.AddChild(_historyStatusLabel);
        _historyScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        historyContent.AddChild(_historyScroll);
        _historyList = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _historyList.AddThemeConstantOverride("separation", 8);
        _historyScroll.AddChild(_historyList);
        AddCollapseTab(_historyPanel, OnHistoryHandlePressed);

        _chatHandle.Pressed += OnChatHandlePressed;
        _historyHandle.Pressed += OnHistoryHandlePressed;
    }

    private static Button MakeTopHandle(string name, string icon, string tooltip)
    {
        var button = new Button
        {
            Name = name,
            CustomMinimumSize = new Vector2(TopHandleWidth, TopHandleHeight),
            Size = new Vector2(TopHandleWidth, TopHandleHeight),
            TooltipText = tooltip,
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            ZIndex = 41
        };
        button.AddToGroup("blocks_map_input");
        ApplyTextureStates(button, PaperHudArt.Panel("handle_top"), 28);
        PaperHudArt.AddDropShadow(button, 14, 6);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        row.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        row.OffsetBottom = -4;
        row.AddThemeConstantOverride("separation", 12);
        button.AddChild(row);
        row.AddChild(PaperHudArt.MakeIcon(icon, 42));
        row.AddChild(PaperHudArt.Glyph("▼", 18));
        return button;
    }

    private static PanelContainer MakeDrawerPanel(string name)
    {
        var panel = new PanelContainer
        {
            Name = name,
            Visible = false,
            CustomMinimumSize = new Vector2(DrawerWidth, 0),
            ZIndex = 40
        };
        panel.AddToGroup("blocks_map_input");
        panel.AddThemeStyleboxOverride("panel", PaperHudArt.NinePatch(PaperHudArt.Panel("drawer_cream"), 40, 22));
        PaperHudArt.AddDropShadow(panel, 18, 10);
        var content = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 10);
        // 顶部给拉手留出空间（拉手压在面板上沿）。
        content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 14), MouseFilter = Control.MouseFilterEnum.Ignore });
        panel.AddChild(content);
        return panel;
    }

    /// <summary>抽屉底边中间的 ^ 收起舌片。</summary>
    private static void AddCollapseTab(PanelContainer panel, Action onPressed)
    {
        var content = (VBoxContainer)panel.GetChild(1);
        var holder = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.AddChild(holder);
        var tab = new Button
        {
            Name = "CollapseTab",
            CustomMinimumSize = new Vector2(90, 28),
            TooltipText = "收起",
            FocusMode = Control.FocusModeEnum.None,
            Text = "▲",
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        tab.AddToGroup("blocks_map_input");
        tab.AddThemeFontSizeOverride("font_size", 14);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color" })
        {
            tab.AddThemeColorOverride(state, PaperHudArt.Ink);
        }

        var strip = PaperHudArt.PaperStrip("f5d6a8", "b98a5a", 12);
        tab.AddThemeStyleboxOverride("normal", strip);
        tab.AddThemeStyleboxOverride("hover", PaperHudArt.PaperStrip("f9dc85", "b98a5a", 12));
        tab.AddThemeStyleboxOverride("pressed", strip);
        tab.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        tab.Pressed += onPressed;
        holder.AddChild(tab);
    }

    private void OnChatHandlePressed()
    {
        _chatPanel.Visible = !_chatPanel.Visible;
        _chatHandle.TooltipText = _chatPanel.Visible ? "收起聊天" : "聊天";
    }

    private void OnHistoryHandlePressed()
    {
        _historyPanel.Visible = !_historyPanel.Visible;
        _historyHandle.TooltipText = _historyPanel.Visible ? "收起历史记录" : "历史记录";
        if (_historyPanel.Visible)
        {
            EmitSignal(SignalName.HistoryRequested);
        }
    }

    /// <summary>历史记录抽屉的数据（服务端行动日志，只读）。loading / error / 空列表分别呈现。</summary>
    public void SetActionLogs(IReadOnlyList<ActionLogDto>? logs, string? error = null, bool loading = false)
    {
        if (!_paperLayoutBuilt)
        {
            return;
        }

        foreach (var child in _historyList.GetChildren())
        {
            _historyList.RemoveChild(child);
            child.QueueFree();
        }

        _historyScroll.CustomMinimumSize = Vector2.Zero;
        if (loading || !string.IsNullOrWhiteSpace(error) || logs == null || logs.Count == 0)
        {
            _historyStatusLabel.Text = loading
                ? "正在读取最近行动……"
                : !string.IsNullOrWhiteSpace(error) ? $"读取失败：{error!.Trim()}" : "本局还没有行动记录";
            _historyStatusLabel.Visible = true;
            return;
        }

        _historyStatusLabel.Visible = false;
        foreach (var log in logs)
        {
            _historyList.AddChild(MakeLogStrip(log));
        }

        // 高度按内容自适应：每行约 64px，最多显示 6 行后滚动。
        _historyScroll.CustomMinimumSize = new Vector2(0, Math.Min(logs.Count, 6) * 72f);
    }

    private Control MakeLogStrip(ActionLogDto log)
    {
        var strip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, 64) };
        strip.AddThemeStyleboxOverride("panel", PaperHudArt.PaperStrip());
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        strip.AddChild(row);

        var player = _pendingPlayerView?.Players?.FirstOrDefault(p => log.PlayerId is > 0 && p.PlayerId == log.PlayerId);
        CardArtCatalog.TryGetCharacterTexture(player?.InitialCharacterCode, out var card, out _);
        var avatarClip = new Control { CustomMinimumSize = new Vector2(48, 48), ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var avatar = new TextureRect
        {
            Texture = PaperHudArt.Portrait(card),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        avatar.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        avatarClip.AddChild(avatar);
        row.AddChild(avatarClip);

        var who = PaperHudArt.MakeLabel(string.IsNullOrWhiteSpace(log.PlayerName) ? "玩家" : log.PlayerName!.Trim(), 19);
        who.CustomMinimumSize = new Vector2(78, 0);
        who.ClipText = true;
        who.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(who);
        row.AddChild(PaperHudArt.MakeIcon(LogIcon(log.ActionType), 32));
        var text = PaperHudArt.MakeLabel(FirstNonEmpty(log.Message, log.ActionTypeText, log.ActionType ?? ""), 19, PaperHudArt.InkSoft, bold: false);
        text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        row.AddChild(text);
        return strip;
    }

    private static string LogIcon(string? actionType)
    {
        var type = actionType?.ToUpperInvariant() ?? "";
        if (type.Contains("WORK")) return "coin";
        if (type.Contains("SHOP") || type.Contains("BUY")) return "market";
        if (type.Contains("QUEST")) return "quest";
        if (type.Contains("ITEM") || type.Contains("EQUIP")) return "item";
        if (type.Contains("MOVE") || type.Contains("RAIL")) return "arrow_right";
        if (type.Contains("END_TURN")) return "end_turn";
        return "history";
    }

    // ───────────── 底部玩家状态栏 ─────────────

    private void BuildBottomBar()
    {
        var bar = new Control { Name = "BottomBar", MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        bar.OffsetLeft = 10;
        bar.OffsetRight = -10;
        bar.OffsetTop = -244;
        bar.OffsetBottom = -8;
        bar.GrowVertical = Control.GrowDirection.Begin;
        _hudRoot.AddChild(bar);

        var tray = new NinePatchRect
        {
            Name = "Tray",
            Texture = PaperHudArt.Panel("bottom_tray"),
            PatchMarginLeft = 70,
            PatchMarginRight = 70,
            PatchMarginTop = 50,
            PatchMarginBottom = 40,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        tray.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        // 托盘本身挡住地图点击（实体面板）；先挂投影，托盘压在投影上。
        tray.AddToGroup("blocks_map_input");
        PaperHudArt.AddDropShadow(bar, 26, 14);
        bar.AddChild(tray);

        var content = new HBoxContainer { Name = "BarContent", MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Begin };
        content.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        content.OffsetLeft = 22;
        content.OffsetRight = -14;
        content.OffsetTop = 18;
        content.OffsetBottom = -16;
        content.AddThemeConstantOverride("separation", 16);
        bar.AddChild(content);

        content.AddChild(BuildSelfSection());
        content.AddChild(MakeDivider());
        content.AddChild(BuildPartnerSection());
        content.AddChild(MakeDivider());
        content.AddChild(BuildItemSection());
        content.AddChild(MakeDivider());
        content.AddChild(BuildEquipmentSection());
        content.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        content.AddChild(BuildActionCluster());
    }

    private static Control MakeDivider()
    {
        // 细虚线分隔：用一串小点 Label 实现，避免额外素材。
        var line = new ColorRect
        {
            Color = new Color(0.55f, 0.4f, 0.28f, 0.28f),
            CustomMinimumSize = new Vector2(2, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        return line;
    }

    private Control BuildSelfSection()
    {
        var panel = new HBoxContainer { Name = "CurrentPlayerPanel", MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeConstantOverride("separation", 12);

        // 方形头像：角色抠图 + 头部对焦 + 主题底色（400×400 素材），昵称用无边框描边字压在左上角。
        var avatarHolder = new Control { Name = "AvatarPlaceholder", CustomMinimumSize = new Vector2(192, 192), MouseFilter = Control.MouseFilterEnum.Pass, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        avatarHolder.AddToGroup("blocks_map_input");
        panel.AddChild(avatarHolder);
        PaperHudArt.AddDropShadow(avatarHolder, 14, 6);
        var paper = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        paper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        paper.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("f3e2c4"),
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14
        });
        avatarHolder.AddChild(paper);
        _selfAvatar = new TextureRect
        {
            Name = "AvatarCard",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _selfAvatar.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        avatarHolder.AddChild(_selfAvatar);
        var frame = new Panel { Name = "Frame", MouseFilter = Control.MouseFilterEnum.Ignore };
        frame.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        frame.AddThemeStyleboxOverride("panel", PaperHudArt.PortraitFrame("fff3dc", 5, 14));
        avatarHolder.AddChild(frame);

        var nameStrip = new VBoxContainer { Name = "NameRibbon", MouseFilter = Control.MouseFilterEnum.Ignore };
        nameStrip.AddThemeConstantOverride("separation", -6);
        nameStrip.Position = new Vector2(12, 6);
        nameStrip.Size = new Vector2(170, 60);
        avatarHolder.AddChild(nameStrip);
        _currentNicknameLabel = PaperHudArt.MakeOverlayLabel("尚未绑定玩家", 24, 8, font: PaperHudArt.TitleFont);
        _currentNicknameLabel.Name = "NicknameLabel";
        _currentNicknameLabel.CustomMinimumSize = new Vector2(170, 0);
        _currentNicknameLabel.ClipText = true;
        _currentNicknameLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        nameStrip.AddChild(_currentNicknameLabel);
        _currentAvatarNameLabel = PaperHudArt.MakeOverlayLabel("玩家", 15, 6);
        _currentAvatarNameLabel.Name = "AvatarNameLabel";
        nameStrip.AddChild(_currentAvatarNameLabel);
        _syncIcon = PaperHudArt.MakeIcon("sync_ok", 32);
        _syncIcon.MouseFilter = Control.MouseFilterEnum.Pass;
        _syncIcon.TooltipText = "与服务端同步正常";
        _syncIcon.Position = new Vector2(150, 150);
        avatarHolder.AddChild(_syncIcon);

        var status = new VBoxContainer { Name = "StatusContent", MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        status.AddThemeConstantOverride("separation", 6);
        panel.AddChild(status);
        var (coins, coinsValue) = PaperHudArt.MakePill("coin", "pill_gold", new Vector2(150, 40), 20, 28, "金币");
        coins.Name = "CoinsBox";
        coinsValue.Name = "CoinsValue";
        _coinsValueLabel = coinsValue;
        var (prestige, prestigeValue) = PaperHudArt.MakePill("reputation", "pill_gold", new Vector2(150, 40), 20, 28, "声望");
        prestige.Name = "PrestigeBox";
        prestigeValue.Name = "PrestigeValue";
        _prestigeValueLabel = prestigeValue;
        status.AddChild(coins);
        status.AddChild(prestige);

        var grid = new GridContainer { Name = "AttributesGrid", Columns = 2, MouseFilter = Control.MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 4);
        status.AddChild(grid);
        foreach (var (key, icon, text) in AttributeIcons)
        {
            var (pill, value) = PaperHudArt.MakePill(icon, "pill_cream", new Vector2(72, 34), 17, 24, text);
            pill.Name = $"{key}Pill";
            value.Name = $"{key}Value";
            grid.AddChild(pill);
            _currentAttributeLabels[key.ToLowerInvariant()] = value;
        }

        return panel;
    }

    private static readonly (string Key, string Icon, string Text)[] AttributeIcons =
    [
        ("Strength", "strength", "力量"),
        ("Speed", "speed", "速度"),
        ("Social", "social", "社交"),
        ("Charm", "charm", "魅力"),
        ("Magic", "magic", "魔法"),
        ("Mind", "mind", "心智")
    ];

    private Control BuildPartnerSection()
    {
        var (section, header, row) = MakeSection("PartnerSection", "partner");
        _partnerCountLabel = header;
        _partnersButton = MakeSectionButton("PartnersButton", "伙伴（仅展示，招募与技能暂未开放）");
        _partnersButton.Disabled = true;
        section.AddChild(_partnersButton);
        for (var i = 0; i < PartnerSlotCount; i++)
        {
            _partnerSlots.Add(MakeCardSlot(row, $"PartnerSlot{i + 1}"));
        }

        return section;
    }

    private Control BuildItemSection()
    {
        var (section, header, row) = MakeSection("ItemSection", "item");
        _itemCountLabel = header;
        _inventoryButton = MakeSectionButton("ItemsButton", "查看背包中的道具");
        section.AddChild(_inventoryButton);
        for (var i = 0; i < VisibleItemCards; i++)
        {
            var card = new TextureRect
            {
                Name = $"ItemCard{i + 1}",
                CustomMinimumSize = CardSize,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false
            };
            row.AddChild(card);
            _itemCards.Add(card);
        }

        _itemEmptyLabel = PaperHudArt.MakeLabel("背包是空的", 15, PaperHudArt.InkSoft);
        _itemEmptyLabel.CustomMinimumSize = new Vector2(CardSize.X * 2, CardSize.Y);
        _itemEmptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
        row.AddChild(_itemEmptyLabel);

        _itemStack = new Control { Name = "ItemStack", CustomMinimumSize = new Vector2(52, CardSize.Y), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        var stackArt = new TextureRect
        {
            Texture = PaperHudArt.CardBit("stack_mint"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        stackArt.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _itemStack.AddChild(stackArt);
        var badge = new TextureRect
        {
            Texture = PaperHudArt.ButtonArt("badge_cream"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = new Vector2(2, CardSize.Y / 2 - 24),
            Size = new Vector2(48, 48)
        };
        _itemStack.AddChild(badge);
        _itemStackLabel = PaperHudArt.MakeLabel("+0", 17, align: HorizontalAlignment.Center);
        _itemStackLabel.Position = badge.Position;
        _itemStackLabel.Size = badge.Size;
        _itemStack.AddChild(_itemStackLabel);
        row.AddChild(_itemStack);

        return section;
    }

    private Control BuildEquipmentSection()
    {
        var (section, header, row) = MakeSection("EquipmentSection", "equip");
        _equipCountLabel = header;
        _equipmentButton = MakeSectionButton("EquipmentButton", "查看当前装备");
        section.AddChild(_equipmentButton);
        for (var i = 0; i < EquipmentSlotCount; i++)
        {
            _equipSlots.Add(MakeCardSlot(row, $"EquipSlot{i + 1}"));
        }

        return section;
    }

    /// <summary>区块：顶部“图标 + 数字”小纸签，下面一排卡；整个区块是一个透明按钮。</summary>
    private static (Control Section, Label Header, HBoxContainer Row) MakeSection(string name, string icon)
    {
        var section = new Control { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore };
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 4);
        section.AddChild(column);

        var tag = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        tag.AddThemeStyleboxOverride("panel", PaperHudArt.NinePatch(PaperHudArt.Panel("tag"), 30, 3));
        tag.CustomMinimumSize = new Vector2(95, 36);
        PaperHudArt.AddDropShadow(tag, 10, 6);
        var tagRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        tagRow.AddThemeConstantOverride("separation", 4);
        tag.AddChild(tagRow);
        tagRow.AddChild(PaperHudArt.MakeIcon(icon, 28));
        var header = PaperHudArt.MakeLabel("-", 20);
        header.CustomMinimumSize = new Vector2(30, 0);
        tagRow.AddChild(header);
        column.AddChild(tag);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        column.AddChild(row);

        // 区块尺寸跟随内容。
        column.Resized += () => section.CustomMinimumSize = column.GetCombinedMinimumSize();
        section.CustomMinimumSize = column.GetCombinedMinimumSize();
        return (section, header, row);
    }

    private static Button MakeSectionButton(string name, string tooltip)
    {
        var button = new Button
        {
            Name = name,
            TooltipText = tooltip,
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        button.AddToGroup("blocks_map_input");
        var empty = new StyleBoxEmpty();
        button.AddThemeStyleboxOverride("normal", empty);
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat
        {
            BgColor = new Color(1f, 0.85f, 0.4f, 0.12f),
            BorderColor = new Color("f6cc5a"),
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthTop = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12
        });
        button.AddThemeStyleboxOverride("pressed", empty);
        button.AddThemeStyleboxOverride("disabled", empty);
        button.AddThemeStyleboxOverride("focus", empty);
        button.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        button.OffsetLeft = -6;
        button.OffsetRight = 6;
        button.OffsetTop = -4;
        button.OffsetBottom = 4;
        return button;
    }

    private static (TextureRect Art, TextureRect Empty) MakeCardSlot(HBoxContainer row, string name)
    {
        var slot = new Control { Name = name, CustomMinimumSize = CardSize, MouseFilter = Control.MouseFilterEnum.Ignore };
        var empty = new TextureRect
        {
            Texture = PaperHudArt.Panel("slot_empty"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        empty.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var plus = PaperHudArt.Glyph("+", 34, new Color("c9a77e"));
        plus.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        empty.AddChild(plus);
        slot.AddChild(empty);
        var art = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        art.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        // 缺卡图（显示卡背 / 卡框）时的“?”角标，避免裸卡背像占位。
        var badge = new Control { Name = "MissingBadge", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(CardSize.X - 34, 4), Size = new Vector2(30, 30) };
        var badgeArt = new TextureRect { Texture = PaperHudArt.ButtonArt("badge_cream"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
        badgeArt.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        badge.AddChild(badgeArt);
        var mark = PaperHudArt.Glyph("?", 17);
        mark.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        badge.AddChild(mark);
        art.AddChild(badge);
        slot.AddChild(art);
        row.AddChild(slot);
        return (art, empty);
    }

    // ───────────── 操作簇：行为魔杖、我的任务、结束回合 ─────────────

    private Control BuildActionCluster()
    {
        var cluster = new Control { Name = "ActionCluster", CustomMinimumSize = new Vector2(290, 200), MouseFilter = Control.MouseFilterEnum.Ignore };

        _actionMenuButton = MakeRoundButton("ActionMenuButton", "round_peach_normal", "round_peach_hover", "magic", "展开行为菜单", 84);
        _actionMenuButton.Position = new Vector2(34, 4);
        cluster.AddChild(_actionMenuButton);

        _personalQuestsButton = MakeRoundButton("QuestsButton", "round_lav_normal", "round_lav_hover", "quest", "查看当前已接任务", 84);
        _personalQuestsButton.Position = new Vector2(10, 106);
        cluster.AddChild(_personalQuestsButton);
        _questsBadge = new Control { Name = "QuestsBadge", MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(58, -4), Size = new Vector2(34, 34), Visible = false };
        var badgeArt = new TextureRect { Texture = PaperHudArt.ButtonArt("badge_red"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
        badgeArt.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _questsBadge.AddChild(badgeArt);
        _questsBadgeLabel = PaperHudArt.MakeLabel("0", 15, Colors.White, align: HorizontalAlignment.Center);
        _questsBadgeLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _questsBadge.AddChild(_questsBadgeLabel);
        _personalQuestsButton.AddChild(_questsBadge);

        // 结束回合：唯一入口，独立大圆章（原魔杖菜单内的入口已移除）。
        var endTurnSlot = new Control { Name = "EndTurnSlot", MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(112, 12), Size = new Vector2(176, 176), Visible = false };
        cluster.AddChild(endTurnSlot);
        _endTurnAnchor = endTurnSlot;
        _endTurnButton = new Button
        {
            Name = "EndTurnButton",
            Text = "结束回合",
            TooltipText = "",
            Disabled = true,
            FocusMode = Control.FocusModeEnum.None,
            Icon = PaperHudArt.Icon("end_turn"),
            ExpandIcon = false,
            IconAlignment = HorizontalAlignment.Center,
            VerticalIconAlignment = VerticalAlignment.Top,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        _endTurnButton.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _endTurnButton.AddToGroup("blocks_map_input");
        ApplyRoundStates(_endTurnButton, "end_turn_normal", "end_turn_hover", "end_turn_pressed", "end_turn_disabled");
        _endTurnButton.AddThemeConstantOverride("icon_max_width", 64);
        _endTurnButton.AddThemeFontOverride("font", PaperHudArt.TitleFont);
        _endTurnButton.AddThemeFontSizeOverride("font_size", 25);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
        {
            _endTurnButton.AddThemeColorOverride(state, PaperHudArt.Ink);
        }

        _endTurnButton.AddThemeColorOverride("font_disabled_color", new Color(0.45f, 0.4f, 0.36f, 1f));
        _endTurnButton.AddThemeColorOverride("font_outline_color", new Color("fff3d6"));
        _endTurnButton.AddThemeConstantOverride("outline_size", 6);
        endTurnSlot.AddChild(_endTurnButton);
        return cluster;
    }

    private static Button MakeRoundButton(string name, string normal, string hover, string icon, string tooltip, float size)
    {
        var button = new Button
        {
            Name = name,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(size, size),
            Size = new Vector2(size, size),
            FocusMode = Control.FocusModeEnum.None,
            Icon = PaperHudArt.Icon(icon),
            ExpandIcon = true,
            IconAlignment = HorizontalAlignment.Center,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        button.AddToGroup("blocks_map_input");
        ApplyRoundStates(button, normal, hover, normal, "round_gray_disabled");
        button.AddThemeColorOverride("icon_disabled_color", new Color(0.7f, 0.7f, 0.7f, 0.7f));
        return button;
    }

    private static void ApplyRoundStates(Button button, string normal, string hover, string pressed, string disabled)
    {
        StyleBox Box(string art, float inset) => new StyleBoxTexture
        {
            Texture = PaperHudArt.ButtonArt(art),
            ContentMarginLeft = inset,
            ContentMarginRight = inset,
            ContentMarginTop = inset,
            ContentMarginBottom = inset
        };
        var inset = button.CustomMinimumSize.X > 0 ? button.CustomMinimumSize.X * 0.2f : 30f;
        button.AddThemeStyleboxOverride("normal", Box(normal, inset));
        button.AddThemeStyleboxOverride("hover", Box(hover, inset));
        button.AddThemeStyleboxOverride("pressed", Box(pressed, inset));
        button.AddThemeStyleboxOverride("disabled", Box(disabled, inset));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private static void ApplyTextureStates(Button button, Texture2D? texture, int margin)
    {
        var normal = PaperHudArt.NinePatch(texture, margin, 4);
        var hover = PaperHudArt.NinePatch(texture, margin, 4);
        if (hover is StyleBoxTexture hoverTexture)
        {
            hoverTexture.ModulateColor = new Color(1.08f, 1.05f, 0.95f);
        }

        var pressed = PaperHudArt.NinePatch(texture, margin, 4);
        if (pressed is StyleBoxTexture pressedTexture)
        {
            pressedTexture.ModulateColor = new Color(0.9f, 0.88f, 0.85f);
        }

        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("disabled", normal);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    // ───────────── 行为菜单（从魔杖向上展开） ─────────────

    private void BuildActionMenu()
    {
        var anchor = new MarginContainer { Name = "ActionMenuAnchor", MouseFilter = Control.MouseFilterEnum.Ignore };
        anchor.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        // 面板底边贴着魔杖圆章上沿（约 10px）。
        anchor.OffsetLeft = -400;
        anchor.OffsetRight = -150;
        anchor.OffsetTop = -590;
        anchor.OffsetBottom = -228;
        anchor.GrowHorizontal = Control.GrowDirection.Begin;
        anchor.GrowVertical = Control.GrowDirection.Begin;
        _hudRoot.AddChild(anchor);
        var menu = new VBoxContainer { Name = "ActionMenu", MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
        anchor.AddChild(menu);
        _actionMenuPanel = new PanelContainer { Name = "MenuPanel", Visible = false, CustomMinimumSize = new Vector2(220, 0) };
        _actionMenuPanel.AddToGroup("blocks_map_input");
        ((PanelContainer)_actionMenuPanel).AddThemeStyleboxOverride("panel", PaperHudArt.NinePatch(PaperHudArt.Panel("drawer_cream"), 40, 14));
        menu.AddChild(_actionMenuPanel);
        var margin = new MarginContainer { Name = "MenuMargin", MouseFilter = Control.MouseFilterEnum.Ignore };
        _actionMenuPanel.AddChild(margin);
        var list = new VBoxContainer { Name = "Menu", MouseFilter = Control.MouseFilterEnum.Ignore };
        list.AddThemeConstantOverride("separation", 8);
        margin.AddChild(list);

        _workButton = MakeMenuButton("WorkButton", "打工", "coin");
        _railwayButton = MakeMenuButton("RailwayButton", "铁路", "arrow_right");
        _questCheckButton = MakeMenuButton("QuestCheckButton", "判定", "check");
        _questSubmissionButton = MakeMenuButton("QuestSubmissionButton", "提交", "accept");
        _workButton.Disabled = true;
        _railwayButton.Disabled = true;
        list.AddChild(_workButton);
        list.AddChild(_railwayButton);
        list.AddChild(_questCheckButton);
        list.AddChild(_questSubmissionButton);
    }

    private static Button MakeMenuButton(string name, string text, string icon)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            Icon = PaperHudArt.Icon(icon),
            ExpandIcon = false,
            CustomMinimumSize = new Vector2(196, 48),
            Alignment = HorizontalAlignment.Left,
            FocusMode = Control.FocusModeEnum.None
        };
        button.AddThemeConstantOverride("icon_max_width", 30);
        button.AddThemeConstantOverride("h_separation", 10);
        button.AddThemeFontOverride("font", PaperHudArt.BoldFont);
        button.AddThemeFontSizeOverride("font_size", 18);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
        {
            button.AddThemeColorOverride(state, PaperHudArt.Ink);
        }

        button.AddThemeColorOverride("font_disabled_color", new Color("a8957e"));
        // 菜单按钮较矮，用代码绘制的纸胶囊（奶油底 + 厚米边），避免九宫格大圆角被压扁。
        static StyleBoxFlat Pill(string fill, string border) => new()
        {
            BgColor = new Color(fill),
            BorderColor = new Color(border),
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 4,
            CornerRadiusTopLeft = 22,
            CornerRadiusTopRight = 22,
            CornerRadiusBottomLeft = 22,
            CornerRadiusBottomRight = 22,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ShadowColor = new Color(0.35f, 0.22f, 0.12f, 0.18f),
            ShadowSize = 3,
            ShadowOffset = new Vector2(1, 2)
        };
        var normal = Pill("fbf3e2", "ebd9b8");
        var hover = Pill("f9dc85", "f6cc5a");
        var disabled = Pill("e6d9c6", "d6c3a6");

        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);
        button.AddThemeStyleboxOverride("disabled", disabled);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        return button;
    }

    // ───────────── 底栏数据 ─────────────

    /// <summary>
    /// 底栏道具 / 装备预览。只读展示服务端背包快照：HAND 进道具区（最多露 3 张，其余叠成牌堆），
    /// EQUIPPED 进装备区（2 槽）。传 null 表示尚未加载。
    /// </summary>
    public void SetInventoryPreview(IEnumerable<PlayerItemDto>? items, string? error = null)
    {
        if (!_paperLayoutBuilt)
        {
            return;
        }

        var list = items?.ToList();
        var hand = list?.Where(item => ItemSystemModel.IsAt(item, ItemSystemModel.Hand)).ToList() ?? [];
        var equipped = list?.Where(item => ItemSystemModel.IsAt(item, ItemSystemModel.Equipped)).ToList() ?? [];

        _itemCountLabel.Text = list == null ? "-" : hand.Count.ToString();
        for (var i = 0; i < _itemCards.Count; i++)
        {
            var card = _itemCards[i];
            if (i < hand.Count)
            {
                card.Visible = true;
                card.Texture = ItemTexture(hand[i]);
                card.TooltipText = hand[i].ItemName ?? "";
            }
            else
            {
                card.Visible = false;
            }
        }

        var overflow = Math.Max(0, hand.Count - _itemCards.Count);
        _itemStack.Visible = overflow > 0;
        _itemStackLabel.Text = $"+{overflow}";
        _itemEmptyLabel.Visible = hand.Count == 0;
        _itemEmptyLabel.Text = list == null
            ? (string.IsNullOrWhiteSpace(error) ? "正在读取背包……" : "背包读取失败")
            : "背包是空的";
        _inventoryButton.TooltipText = string.IsNullOrWhiteSpace(error)
            ? $"查看背包中的道具（{hand.Count} 件）"
            : $"背包读取失败：{error.Trim()}";

        _equipCountLabel.Text = list == null ? $"-/{EquipmentSlotCount}" : $"{equipped.Count}/{EquipmentSlotCount}";
        for (var i = 0; i < _equipSlots.Count; i++)
        {
            var (art, empty) = _equipSlots[i];
            var has = i < equipped.Count;
            art.Visible = has;
            empty.Visible = !has;
            if (has)
            {
                art.Texture = ItemTexture(equipped[i], "frame_equip", out var mapped);
                art.GetNode<Control>("MissingBadge").Visible = !mapped;
                art.TooltipText = equipped[i].ItemName ?? "";
            }
        }
    }

    private static Texture2D? ItemTexture(PlayerItemDto item) => ItemTexture(item, "frame_item", out _);

    private static Texture2D? ItemTexture(PlayerItemDto item, string fallbackFrame, out bool mapped)
    {
        if (item.ItemId is long id && CardArtCatalog.TryGetItemTexture(id, out var texture, out _))
        {
            mapped = true;
            return texture;
        }

        // 没有本地卡图的道具：显示对应颜色的空卡框 + “?”角标，名字在悬停提示里（不自绘卡面）。
        mapped = false;
        return PaperHudArt.CardBit(fallbackFrame);
    }

    /// <summary>底栏伙伴预览（只读）。null = 尚未加载或读取失败。</summary>
    public void SetPartnerPreview(IReadOnlyList<PlayerPartnerDto>? partners, string? error = null)
    {
        if (!_paperLayoutBuilt)
        {
            return;
        }

        var active = partners?
            .Where(partner => !string.Equals(partner.Status, "DISMISSED", StringComparison.OrdinalIgnoreCase))
            .ToList();
        _partnerCountLabel.Text = active == null ? $"-/{PartnerSlotCount}" : $"{active.Count}/{PartnerSlotCount}";
        for (var i = 0; i < _partnerSlots.Count; i++)
        {
            var (art, empty) = _partnerSlots[i];
            var has = active != null && i < active.Count;
            art.Visible = has;
            empty.Visible = !has;
            if (has)
            {
                var partner = active![i];
                var mapped = CardArtCatalog.TryGetPartnerTexture(partner.PartnerCode, partner.PartnerName, out var texture, out _);
                art.Texture = texture;
                art.GetNode<Control>("MissingBadge").Visible = !mapped;
                var pending = !string.Equals(partner.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(partner.Status);
                art.Modulate = pending ? new Color(1, 1, 1, 0.6f) : Colors.White;
                art.TooltipText = $"{partner.PartnerName ?? partner.PartnerCode ?? "伙伴"}"
                    + (string.IsNullOrWhiteSpace(partner.StatusText) ? "" : $"（{partner.StatusText}）");
            }
        }

        _partnersButton.TooltipText = !string.IsNullOrWhiteSpace(error)
            ? $"伙伴读取失败：{error.Trim()}"
            : "伙伴（仅展示，招募与技能暂未开放）";
    }

    /// <summary>“我的任务”圆章上的数量角标；null = 未知时不显示。</summary>
    public void SetPersonalQuestCount(int? count)
    {
        if (!_paperLayoutBuilt)
        {
            return;
        }

        _questsBadge.Visible = count is > 0;
        _questsBadgeLabel.Text = count?.ToString() ?? "";
    }

    private void ApplySelfAvatar(PlayerViewPlayerDto? player)
    {
        var square = PaperHudArt.CharacterPortrait(player?.InitialCharacterCode, "square");
        if (square == null)
        {
            CardArtCatalog.TryGetCharacterTexture(player?.InitialCharacterCode, out var texture, out _);
            square = PaperHudArt.Portrait(texture, 0.05f, 0.5f, 0.12f);
        }

        _selfAvatar.Texture = square;
        // 头像下方小字只写角色名。
        _currentAvatarNameLabel.Text = string.IsNullOrWhiteSpace(player?.InitialCharacterName) ? "" : player!.InitialCharacterName!.Trim();
    }

    private void ApplyRound(PlayerViewResponseDto? view)
    {
        _roundLabel.Text = view == null || view.CurrentRound <= 0 ? "-" : view.CurrentRound.ToString();
    }

    private void ApplySyncIndicator(bool ready)
    {
        _syncIcon.Texture = PaperHudArt.Icon(ready ? "sync_ok" : "round");
        _syncIcon.TooltipText = ready ? "与服务端同步正常" : "正在建立对局连接……";
    }

    /// <summary>按回合顺序给每块名牌定状态：当前、下一位、本轮已行动、等待。</summary>
    private void ApplyTurnStates(IReadOnlyList<PlayerViewPlayerDto> ordered, PlayerViewResponseDto view, long currentUserId)
    {
        var currentIndex = -1;
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].UserId == view.CurrentTurnUserId)
            {
                currentIndex = i;
                break;
            }
        }

        var running = string.Equals(view.GameStatus, "RUNNING", StringComparison.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            var player = ordered[i];
            if (!_otherPlayerEntries.TryGetValue(OtherPlayerKey(player), out var entry) || !GodotObject.IsInstanceValid(entry))
            {
                continue;
            }

            var state = OtherPlayerHudEntry.TurnState.Waiting;
            if (running && currentIndex >= 0)
            {
                if (i == currentIndex)
                {
                    state = OtherPlayerHudEntry.TurnState.Current;
                }
                else if (ordered.Count > 1 && i == (currentIndex + 1) % ordered.Count)
                {
                    state = OtherPlayerHudEntry.TurnState.Next;
                }
                else if (i < currentIndex)
                {
                    state = OtherPlayerHudEntry.TurnState.Acted;
                }
            }

            entry.SetTurnState(state, player.UserId == currentUserId);
        }
    }
}
