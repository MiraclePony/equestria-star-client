using System;
using Godot;
using EquestriaStar.Api;
using EquestriaStar.State;

namespace EquestriaStar.UI;

/// <summary>
/// 等待房间“角色表”的一行：座位号圆章 + 昵称 + 引导点线 + 状态纸签（房主 / 已准备 / 未准备）。
/// 空座位不走这个组件（由 RoomScreen 另画“等待加入”行），保证成员列表只含真实成员。
/// </summary>
public partial class RoomMemberRow : PanelContainer
{
    private Label _seatLabel = null!;
    private Label _nameLabel = null!;
    private Label _roleLabel = null!;
    private Label _readyLabel = null!;
    private EntryBook.Tag _tag = null!;
    private TextureRect _seatPlate = null!;
    private bool _highlighted;

    public long UserId { get; private set; }

    public override void _Ready()
    {
        _seatLabel = GetNode<Label>("Margin/Row/SeatLabel");
        _nameLabel = GetNode<Label>("Margin/Row/NameLabel");
        _roleLabel = GetNode<Label>("Margin/Row/RoleLabel");
        _readyLabel = GetNode<Label>("Margin/Row/ReadyLabel");
        ApplyBookStyle();
    }

    public void UpdateMember(RoomMemberDto member)
    {
        UserId = member.UserId;
        _seatLabel.Text = member.SeatNo.ToString();
        _nameLabel.Text = PlainName(member);
        EntryBook.FitWidth(_nameLabel, 330);

        var isOwner = string.Equals(member.MemberRole, "OWNER", StringComparison.OrdinalIgnoreCase);
        var isReady = string.Equals(member.ReadyStatus, "READY", StringComparison.OrdinalIgnoreCase);
        _roleLabel.Text = isOwner ? "房主" : "";
        _roleLabel.Visible = false;
        _readyLabel.Text = isOwner ? "无需准备" : isReady ? "已准备" : "未准备";
        _readyLabel.Visible = false;
        if (isOwner)
        {
            _tag.Set(EntryBook.TagKind.Owner, "房主");
        }
        else
        {
            _tag.Set(isReady ? EntryBook.TagKind.Ready : EntryBook.TagKind.NotReady, isReady ? "已准备" : "未准备");
        }
    }

    /// <summary>高亮“我自己”这一行（雾紫底 + 金色座位章）。</summary>
    public void SetHighlighted(bool highlighted)
    {
        _highlighted = highlighted;
        AddThemeStyleboxOverride("panel", RowBox(highlighted));
        _seatPlate.Texture = EntryBook.Tex(highlighted ? "parts/seat_gold" : "parts/seat_rose");
    }

    /// <summary>昵称（不带“✓”，准备状态由右侧纸签表达）。</summary>
    internal static string PlainName(RoomMemberDto member)
    {
        return string.IsNullOrWhiteSpace(member.Nickname) ? member.Username : member.Nickname;
    }

    internal static StyleBoxFlat RowBox(bool highlighted) => highlighted
        ? new StyleBoxFlat
        {
            BgColor = EntryBook.LilacWash,
            CornerRadiusTopLeft = 24,
            CornerRadiusTopRight = 24,
            CornerRadiusBottomLeft = 24,
            CornerRadiusBottomRight = 24,
            AntiAliasing = true
        }
        : new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), BorderColor = new Color("e0d3bf"), BorderWidthBottom = 1 };

    /// <summary>座位号：圆章底图 + 号码。</summary>
    internal static Control SeatBadge(Label number, out TextureRect plate, float size = 40f)
    {
        var badge = new Control { CustomMinimumSize = new Vector2(size, size), MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        plate = EntryBook.MakeImage("parts/seat_rose", Vector2.Zero, TextureRect.StretchModeEnum.KeepAspectCentered);
        plate.SetAnchorsPreset(LayoutPreset.FullRect);
        badge.AddChild(plate);
        number.GetParent()?.RemoveChild(number);
        badge.AddChild(number);
        number.SetAnchorsPreset(LayoutPreset.FullRect);
        number.HorizontalAlignment = HorizontalAlignment.Center;
        number.VerticalAlignment = VerticalAlignment.Center;
        EntryBook.StyleLabel(number, 19, new Color("fbf4e8"), EntryBook.BoldFont);
        return badge;
    }

    internal static Label Leader()
    {
        var leader = EntryBook.MakeLabel(new string('·', 80), 16, EntryBook.InkFaint);
        leader.ClipText = true;
        leader.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leader.CustomMinimumSize = new Vector2(30, 0);
        return leader;
    }

    private void ApplyBookStyle()
    {
        CustomMinimumSize = new Vector2(0, 54);
        var margin = GetNode<MarginContainer>("Margin");
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        var row = GetNode<HBoxContainer>("Margin/Row");
        row.AddThemeConstantOverride("separation", 16);

        var badge = SeatBadge(_seatLabel, out _seatPlate);
        row.AddChild(badge);
        row.MoveChild(badge, 0);

        EntryBook.StyleLabel(_nameLabel, 21, EntryBook.Ink, EntryBook.BoldFont);
        _nameLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _nameLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _nameLabel.MaxLinesVisible = 1;
        var leader = Leader();
        row.AddChild(leader);
        row.MoveChild(leader, 2);

        _tag = new EntryBook.Tag(36, 17, 136);
        row.AddChild(_tag);
        SetHighlighted(_highlighted);
    }
}
