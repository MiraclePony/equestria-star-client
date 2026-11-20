using Godot;

namespace EquestriaStar.UI;

/// <summary>
/// 选角页：翻到两页都空白的跨页，角色卡横向铺在整本书上。
/// 只改排版与上色，测试依赖的节点路径（CarouselScroll / ConfirmButton / CardNameLabel 等）保持不变。
/// </summary>
public partial class CharacterSelectScreen
{
    /// <summary>选角跨页的版心（舞台坐标，两页合起来）。</summary>
    private static readonly Rect2 SpreadPage = new(120, 50, 1690, 912);

    private void ApplyBookStyle()
    {
        var hasSelectBg = ResourceLoader.Exists("res://assets/art/ui/entry_book/bg/select.png");
        var stage = EntryBook.BuildStage(this, hasSelectBg ? "select" : "lobby");
        EntryBook.Footer(stage, SpreadPage);

        // 页眉：小标志 | 选择角色 …… 书签
        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 18);
        var crest = EntryBook.Crest(196, 22);
        crest.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(crest);
        head.AddChild(EntryBook.MakeLabel("|", 34, EntryBook.InkSoft));
        head.AddChild(EntryBook.MakeLabel("选择角色", 38, EntryBook.Ink, EntryBook.TitleFont));
        EntryBook.AddAt(stage, head, new Rect2(SpreadPage.Position.X + 30, SpreadPage.Position.Y + 6, 900, 84));
        EntryBook.AddAt(stage, EntryBook.Rule(1.5f, new Color(EntryBook.InkSoft, 0.35f)),
            new Rect2(SpreadPage.Position.X + 30, SpreadPage.Position.Y + 98, SpreadPage.Size.X - 60, 1.5f));

        var rootMargin = GetNode<MarginContainer>("RootMargin");
        EntryBook.PlaceOnStage(rootMargin, new Rect2(SpreadPage.Position.X, SpreadPage.Position.Y + 110, SpreadPage.Size.X, SpreadPage.Size.Y - 170));
        rootMargin.AddThemeConstantOverride("margin_left", 40);
        rootMargin.AddThemeConstantOverride("margin_right", 40);
        rootMargin.AddThemeConstantOverride("margin_top", 6);
        rootMargin.AddThemeConstantOverride("margin_bottom", 6);
        var layout = GetNode<VBoxContainer>("RootMargin/Layout");
        layout.AddThemeConstantOverride("separation", 10);

        EntryBook.StyleLabel(_countdownLabel, 26, EntryBook.Ink, EntryBook.TitleFont);
        _countdownLabel.HorizontalAlignment = HorizontalAlignment.Center;
        EntryBook.StyleLabel(_selectionStateLabel, 18, EntryBook.InkSoft);
        _selectionStateLabel.HorizontalAlignment = HorizontalAlignment.Center;

        foreach (var arrow in new[] { _leftButton, _rightButton })
        {
            EntryBook.StyleCapsule(arrow, EntryBook.CapsuleKind.Warm, 64, 34);
            arrow.CustomMinimumSize = new Vector2(64, 64);
            arrow.OffsetTop = -32;
            arrow.OffsetBottom = 32;
            if (arrow.GetThemeStylebox("normal") is StyleBoxFlat)
            {
                foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
                {
                    if (arrow.GetThemeStylebox(state) is StyleBox box)
                    {
                        box.ContentMarginLeft = 0;
                        box.ContentMarginRight = 0;
                    }
                }
            }
        }

        _leftButton.OffsetLeft = 20;
        _leftButton.OffsetRight = 84;
        _rightButton.OffsetLeft = -84;
        _rightButton.OffsetRight = -20;

        EntryBook.StyleLabel(_cardNameLabel, 34, EntryBook.Ink, EntryBook.TitleFont);
        _cardNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        var confirmRow = GetNode<HBoxContainer>("RootMargin/Layout/ConfirmRow");
        confirmRow.Alignment = BoxContainer.AlignmentMode.Center;
        EntryBook.StyleCapsule(_confirmButton, EntryBook.CapsuleKind.Primary, 58, 26);
        _confirmButton.CustomMinimumSize = new Vector2(340, 58);
        EntryBook.StyleLabel(_networkStatusLabel, 16, EntryBook.InfoInk);
        _networkStatusLabel.HorizontalAlignment = HorizontalAlignment.Center;

        var bookmark = EntryBook.PlayerBookmark(Session.GetPlayerName(), null);
        AddChild(bookmark);
        EntryBook.PlaceOnStage(bookmark, new Rect2(SpreadPage.End.X - 330, 0, 300, 140));
    }
}
