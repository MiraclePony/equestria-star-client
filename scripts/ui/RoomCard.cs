using Godot;
using EquestriaStar.Api;
using EquestriaStar.State;

namespace EquestriaStar.UI;

/// <summary>
/// 大厅“目录页”的一行：房间名 + 引导点线 + 马蹄铁座位 + 人数 + 状态纸签 + 加入胶囊。
/// 场景里原有的节点（名称 / 房主 / 人数 / 状态 / 加入）保留，只在 _Ready 里重新排版与上色。
/// </summary>
public partial class RoomCard : PanelContainer
{
    [Signal]
    public delegate void JoinRequestedEventHandler(RoomCard card);

    private const float RowHeight = 84f;

    private Label _roomNameLabel = null!;
    private Label _ownerLabel = null!;
    private Label _playersLabel = null!;
    private Label _statusLabel = null!;
    private Button _joinButton = null!;
    private HBoxContainer _seats = null!;
    private EntryBook.Tag _statusTag = null!;
    private StyleBoxFlat _normalBox = null!;
    private StyleBoxFlat _hoverBox = null!;
    private RoomDto _room = new();

    public RoomDto Room => _room;

    public override void _Ready()
    {
        _roomNameLabel = GetNode<Label>("Margin/Row/MainInfo/RoomNameLabel");
        _ownerLabel = GetNode<Label>("Margin/Row/MainInfo/MetaRow/OwnerLabel");
        _playersLabel = GetNode<Label>("Margin/Row/MainInfo/MetaRow/PlayersLabel");
        _statusLabel = GetNode<Label>("Margin/Row/MainInfo/MetaRow/StatusLabel");
        _joinButton = GetNode<Button>("Margin/Row/JoinButton");
        _joinButton.Pressed += () => EmitSignal(SignalName.JoinRequested, this);
        ApplyBookStyle();
    }

    public void Setup(RoomDto room)
    {
        _room = room;
        _roomNameLabel.Text = string.IsNullOrWhiteSpace(room.RoomName) ? "未命名房间" : room.RoomName;
        EntryBook.FitWidth(_roomNameLabel, 300);
        _ownerLabel.Text = $"房主  {(string.IsNullOrWhiteSpace(room.OwnerNickname) ? "未知" : room.OwnerNickname)}";
        _playersLabel.Text = $"{room.CurrentPlayers}/{room.MaxPlayers}";
        _statusLabel.Text = RoomListModel.StatusLabel(room);
        var playing = !string.Equals(room.Status, "WAITING", System.StringComparison.OrdinalIgnoreCase);
        _statusTag.Set(playing ? EntryBook.TagKind.Playing : EntryBook.TagKind.Waiting, RoomListModel.StatusLabel(room));
        BuildSeats(room.CurrentPlayers, room.MaxPlayers);
        var joinable = RoomListModel.CanJoin(room);
        _joinButton.Disabled = !joinable;
        _joinButton.Text = joinable ? "加入" : "不可加入";
    }

    public void SetJoinLoading(bool loading)
    {
        var joinable = RoomListModel.CanJoin(_room);
        _joinButton.Disabled = loading || !joinable;
        _joinButton.Text = loading ? "加入中…" : joinable ? "加入" : "不可加入";
    }

    private void BuildSeats(int current, int max)
    {
        foreach (var child in _seats.GetChildren())
        {
            child.QueueFree();
        }

        max = Mathf.Clamp(max, 0, 8);
        for (var i = 0; i < max; i++)
        {
            var filled = i < current;
            var shoe = EntryBook.MakeImage(filled ? "parts/horseshoe_gold" : "parts/horseshoe_empty", new Vector2(22, 23));
            if (!filled)
            {
                shoe.Modulate = new Color("a89784");
            }

            _seats.AddChild(shoe);
        }
    }

    private void ApplyBookStyle()
    {
        _normalBox = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0),
            BorderColor = new Color("dccdb5"),
            BorderWidthBottom = 1
        };
        _hoverBox = new StyleBoxFlat
        {
            BgColor = EntryBook.LilacWash,
            CornerRadiusTopLeft = 22,
            CornerRadiusTopRight = 22,
            CornerRadiusBottomLeft = 22,
            CornerRadiusBottomRight = 22,
            AntiAliasing = true
        };
        AddThemeStyleboxOverride("panel", _normalBox);
        CustomMinimumSize = new Vector2(0, RowHeight);
        MouseFilter = MouseFilterEnum.Pass;
        MouseEntered += () => AddThemeStyleboxOverride("panel", _hoverBox);
        MouseExited += () => AddThemeStyleboxOverride("panel", _normalBox);

        var margin = GetNode<MarginContainer>("Margin");
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        var row = GetNode<HBoxContainer>("Margin/Row");
        row.AddThemeConstantOverride("separation", 18);
        var main = GetNode<VBoxContainer>("Margin/Row/MainInfo");
        main.AddThemeConstantOverride("separation", 2);
        main.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        main.Alignment = BoxContainer.AlignmentMode.Center;

        // 名称 + 引导点线
        var nameRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        nameRow.AddThemeConstantOverride("separation", 10);
        main.AddChild(nameRow);
        main.MoveChild(nameRow, 0);
        _roomNameLabel.Reparent(nameRow);
        EntryBook.StyleLabel(_roomNameLabel, 23, EntryBook.Ink, EntryBook.BoldFont);
        _roomNameLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _roomNameLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        nameRow.AddChild(RoomMemberRow.Leader());

        EntryBook.StyleLabel(_ownerLabel, 16, EntryBook.InkSoft);
        _statusLabel.Visible = false;

        // 座位：马蹄铁一排 + 人数
        var seatBox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(176, 0), Alignment = BoxContainer.AlignmentMode.Center };
        seatBox.AddThemeConstantOverride("separation", 0);
        row.AddChild(seatBox);
        row.MoveChild(seatBox, 1);
        _seats = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        _seats.AddThemeConstantOverride("separation", 4);
        seatBox.AddChild(_seats);
        _playersLabel.Reparent(seatBox);
        EntryBook.StyleLabel(_playersLabel, 17, EntryBook.Ink, EntryBook.BoldFont);
        _playersLabel.HorizontalAlignment = HorizontalAlignment.Center;

        _statusTag = new EntryBook.Tag(34, 16, 100);
        row.AddChild(_statusTag);
        row.MoveChild(_statusTag, 2);

        EntryBook.StyleCapsule(_joinButton, EntryBook.CapsuleKind.Warm, 46, 19);
        _joinButton.CustomMinimumSize = new Vector2(132, 46);
        _joinButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }
}
