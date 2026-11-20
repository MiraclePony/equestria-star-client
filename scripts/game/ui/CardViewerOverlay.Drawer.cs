using Godot;

namespace EquestriaStar.Game.UI;

/// <summary>
/// 公共任务板 / 公共货架的“右侧拉框抽屉”呈现（m2 定稿）。
/// 只改变摆放与纸面样式：节点路径、按钮信号、加载 / 空 / 错误 / 重试状态、点击其他区域关闭等行为与原查看层完全一致。
/// 背包、装备、个人任务仍使用居中的原呈现。
/// </summary>
public partial class CardViewerOverlay
{
    // 抽屉贴着右侧拉手（拉手 80px 宽，屏幕右缘）。
    private const float DrawerRightInset = 92f;
    private const float DrawerPadding = 22f;
    private const float QuestDrawerCardWidth = 310f;
    private const float ShopDrawerCardWidth = 176f;

    private Control _centerContainer = null!;
    private ColorRect _backdrop = null!;
    private Panel _drawerPaper = null!;
    private Control _errorPanel = null!;
    private Label _loadingLabel = null!;
    private bool _drawerPresentation;

    // 原呈现的默认值，切回居中模式时还原。
    private Color _defaultBackdrop;
    private Color _defaultTitleColor;
    private Color _defaultEmptyColor;
    private Color _defaultErrorColor;
    private Vector2 _defaultEmptyMin;
    private Vector2 _defaultLoadingMin;
    private Vector2 _defaultErrorStateMin;
    private Vector2 _defaultErrorPanelMin;
    private StyleBox? _defaultErrorStyle;

    public bool IsDrawerPresentation => _drawerPresentation;

    private static bool IsDrawerKind(CardViewerKind kind) => kind is CardViewerKind.QuestBoard or CardViewerKind.ShopShelf;

    private void SetupDrawerPresentation()
    {
        _centerContainer = GetNode<Control>("OverlayRoot/Center");
        _backdrop = GetNode<ColorRect>("OverlayRoot/Backdrop");
        _errorPanel = GetNode<Control>("OverlayRoot/Center/Layout/ErrorState/ErrorPanel");
        _loadingLabel = GetNode<Label>("OverlayRoot/Center/Layout/LoadingState/LoadingPanel/LoadingLabel");
        _defaultBackdrop = _backdrop.Color;
        _defaultTitleColor = _titleLabel.GetThemeColor("font_color");
        _defaultEmptyColor = _emptyLabel.GetThemeColor("font_color");
        _defaultErrorColor = _errorLabel.GetThemeColor("font_color");
        _defaultEmptyMin = _emptyLabel.CustomMinimumSize;
        _defaultLoadingMin = _loadingState.CustomMinimumSize;
        _defaultErrorStateMin = _errorState.CustomMinimumSize;
        _defaultErrorPanelMin = _errorPanel.CustomMinimumSize;
        _defaultErrorStyle = _errorPanel.GetThemeStylebox("panel");

        // 抽屉纸面：在 Center 之下，随 Center 的实际矩形同步，不接收鼠标（点击纸面空白处与原来一样按背景处理）。
        _drawerPaper = new Panel { Name = "DrawerPaper", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlayRoot.AddChild(_drawerPaper);
        _overlayRoot.MoveChild(_drawerPaper, _centerContainer.GetIndex());
        _centerContainer.ItemRectChanged += SyncDrawerPaper;

        foreach (var button in _actionButtons)
        {
            StylePaperActionButton(button);
        }

        // 本地已接入的出售与两种刷新继续使用原信号，只补纸艺按钮外观。
        foreach (var button in new[] { _shopSell, _shopRefresh, _questRefresh })
        {
            StylePaperActionButton(button);
            button.CustomMinimumSize = new Vector2(Mathf.Max(button.CustomMinimumSize.X, 152), 48);
        }
        _shopSell.Icon = PaperHudArt.Icon("item");
        _shopRefresh.Icon = PaperHudArt.Icon("sync_ok");
        _questRefresh.Icon = PaperHudArt.Icon("quest");
    }

    private void ApplyPresentation(CardViewerKind kind)
    {
        var drawer = IsDrawerKind(kind);
        _drawerPresentation = drawer;
        _drawerPaper.Visible = drawer;
        _hintLabel.Visible = !drawer;
        _backdrop.Color = drawer ? new Color(0.16f, 0.1f, 0.05f, 0.16f) : _defaultBackdrop;
        _titleLabel.AddThemeColorOverride("font_color", drawer ? PaperHudArt.Ink : _defaultTitleColor);
        _titleLabel.HorizontalAlignment = drawer ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        if (drawer)
        {
            _titleLabel.AddThemeFontOverride("font", PaperHudArt.TitleFont);
            _titleLabel.AddThemeFontSizeOverride("font_size", 26);
        }
        else
        {
            _titleLabel.RemoveThemeFontOverride("font");
            _titleLabel.AddThemeFontSizeOverride("font_size", 28);
        }

        _emptyLabel.AddThemeColorOverride("font_color", drawer ? PaperHudArt.InkSoft : _defaultEmptyColor);
        _emptyLabel.CustomMinimumSize = drawer ? new Vector2(DrawerContentWidth(kind), 200) : _defaultEmptyMin;
        _loadingState.CustomMinimumSize = drawer ? new Vector2(DrawerContentWidth(kind), 180) : _defaultLoadingMin;
        _errorState.CustomMinimumSize = drawer ? new Vector2(DrawerContentWidth(kind), 200) : _defaultErrorStateMin;
        _errorPanel.CustomMinimumSize = drawer ? new Vector2(DrawerContentWidth(kind) - 40, 140) : _defaultErrorPanelMin;
        _errorLabel.AddThemeColorOverride("font_color", drawer ? new Color("9a3b2a") : _defaultErrorColor);
        if (drawer)
        {
            _errorPanel.AddThemeStyleboxOverride("panel", PaperHudArt.PaperStrip("fbe3d6", "d29a82", 14));
            _loadingLabel.AddThemeColorOverride("font_color", PaperHudArt.Ink);
        }
        else if (_defaultErrorStyle != null)
        {
            _errorPanel.AddThemeStyleboxOverride("panel", _defaultErrorStyle);
        }

        var row = _cardRow;
        // 任务板：横版卡竖排，按钮在卡右侧；货架：纵版卡横排，按钮在卡下方。
        row.Vertical = drawer && kind == CardViewerKind.QuestBoard;
        row.AddThemeConstantOverride("separation", drawer ? 14 : 24);
        foreach (var slot in _cardSlots)
        {
            slot.Vertical = !(drawer && kind == CardViewerKind.QuestBoard);
            slot.AddThemeConstantOverride("separation", 10);
        }

        for (var index = 0; index < _actionButtons.Count; index++)
        {
            var button = _actionButtons[index];
            button.Icon = drawer ? PaperHudArt.Icon(kind == CardViewerKind.QuestBoard ? "accept" : "buy") : null;
            button.ExpandIcon = false;
            _actionAreas[index].SizeFlagsVertical = drawer && kind == CardViewerKind.QuestBoard
                ? Control.SizeFlags.ShrinkCenter
                : Control.SizeFlags.Fill;
            if (drawer)
            {
                _actionAreas[index].AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            }
            else
            {
                _actionAreas[index].RemoveThemeStyleboxOverride("panel");
            }
        }

        PlaceCenter(kind);
    }

    private static float DrawerContentWidth(CardViewerKind kind) =>
        kind == CardViewerKind.QuestBoard ? QuestDrawerCardWidth + 134f : ShopDrawerCardWidth * 3f + 28f;

    private void PlaceCenter(CardViewerKind kind)
    {
        var center = _centerContainer;
        if (!_drawerPresentation)
        {
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            center.OffsetLeft = 60;
            center.OffsetTop = 42;
            center.OffsetRight = -60;
            center.OffsetBottom = -42;
            center.GrowHorizontal = Control.GrowDirection.Both;
            center.GrowVertical = Control.GrowDirection.Both;
            return;
        }

        // 使用确定的右侧安全矩形，避免反复 OpenLoading/ShowCards 时 Size=Zero
        // 累计改写偏移，最终把抽屉按钮推到视口外。
        var contentWidth = DrawerContentWidth(kind);
        center.AnchorLeft = 1;
        center.AnchorRight = 1;
        center.OffsetRight = -DrawerRightInset - DrawerPadding;
        center.OffsetLeft = center.OffsetRight - contentWidth;
        center.GrowHorizontal = Control.GrowDirection.Both;
        center.AnchorTop = 0;
        center.AnchorBottom = 1;
        center.GrowVertical = Control.GrowDirection.Both;
        if (kind == CardViewerKind.QuestBoard)
        {
            center.OffsetTop = 96 + DrawerPadding;
            center.OffsetBottom = -180;
        }
        else
        {
            center.OffsetTop = 90;
            center.OffsetBottom = -338;
        }

        Callable.From(SyncDrawerPaper).CallDeferred();
    }

    private void SyncDrawerPaper()
    {
        if (!GodotObject.IsInstanceValid(_drawerPaper) || !_drawerPresentation)
        {
            return;
        }

        var rect = _centerContainer.GetRect().Grow(DrawerPadding);
        _drawerPaper.Position = rect.Position;
        _drawerPaper.Size = rect.Size;
        var art = Kind == CardViewerKind.QuestBoard ? "drawer_lav" : "drawer_mint";
        _drawerPaper.AddThemeStyleboxOverride("panel", PaperHudArt.NinePatch(PaperHudArt.Panel(art), 40, 0));
    }

    private void RefreshDrawerCardSizes()
    {
        var quest = Kind == CardViewerKind.QuestBoard;
        var width = quest ? QuestDrawerCardWidth : ShopDrawerCardWidth;
        var ratio = quest ? QuestAspectRatio : ItemAspectRatio;
        var height = width / ratio;
        for (var index = 0; index < _cardSlots.Count; index++)
        {
            _cardAspects[index].Ratio = ratio;
            _cardAspects[index].CustomMinimumSize = new Vector2(width, height);
            _cardSlots[index].CustomMinimumSize = quest ? new Vector2(width + 134f, height) : new Vector2(width, height + 58f);
            _actionButtons[index].CustomMinimumSize = quest ? new Vector2(124, 54) : new Vector2(0, 50);
        }

        Callable.From(SyncDrawerPaper).CallDeferred();
    }

    /// <summary>接取 / 购买按钮：金色纸胶囊 + 图标 + 深棕字，不可用时整体变灰。</summary>
    private static void StylePaperActionButton(Button button)
    {
        StyleBoxFlat Pill(string fill, string border) => PaperHudArt.PaperStrip(fill, border, 22);
        button.AddThemeStyleboxOverride("normal", Pill("f9dc85", "b98a5a"));
        button.AddThemeStyleboxOverride("hover", Pill("fde9a8", "b98a5a"));
        button.AddThemeStyleboxOverride("pressed", Pill("efc65c", "a07040"));
        // 不可接取 / 不可购买：中性灰底、灰字、灰图标（与可用的金色明显区分，原因在悬停提示里）。
        button.AddThemeStyleboxOverride("disabled", Pill("d3d0cc", "aeaaa5"));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
        {
            button.AddThemeColorOverride(state, PaperHudArt.Ink);
        }

        button.AddThemeColorOverride("font_disabled_color", new Color("8c8883"));
        button.AddThemeColorOverride("icon_disabled_color", new Color(0.55f, 0.54f, 0.53f, 0.75f));
        button.AddThemeFontOverride("font", PaperHudArt.BoldFont);
        button.AddThemeFontSizeOverride("font_size", 20);
        button.AddThemeConstantOverride("icon_max_width", 30);
        button.AddThemeConstantOverride("h_separation", 6);
    }
}
