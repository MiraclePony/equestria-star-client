using Godot;
using EquestriaStar.Api;
using EquestriaStar.State;

namespace EquestriaStar.UI;

public partial class CreateRoomDialog : Control
{
    [Signal]
    public delegate void RoomCreatedEventHandler();

    private LineEdit _roomNameEdit = null!;
    private SpinBox _maxPlayersSpin = null!;
    private Button _createButton = null!;
    private Button _cancelButton = null!;
    private Button _closeButton = null!;
    private Label _statusLabel = null!;
    private bool _submitting;

    private ApiClient ApiClient => GetNode<ApiClient>("/root/ApiClient");
    private Session Session => GetNode<Session>("/root/Session");

    public override void _Ready()
    {
        _roomNameEdit = GetNode<LineEdit>("Center/Panel/Margin/Form/Fields/RoomNameEdit");
        _maxPlayersSpin = GetNode<SpinBox>("Center/Panel/Margin/Form/Fields/PlayerRow/MaxPlayersSpin");
        _createButton = GetNode<Button>("Center/Panel/Margin/Form/ButtonRow/CreateButton");
        _cancelButton = GetNode<Button>("Center/Panel/Margin/Form/ButtonRow/CancelButton");
        _closeButton = GetNode<Button>("Center/Panel/Margin/Form/Header/CloseButton");
        _statusLabel = GetNode<Label>("Center/Panel/Margin/Form/StatusLabel");

        _createButton.Pressed += OnCreatePressed;
        _cancelButton.Pressed += CloseDialog;
        _closeButton.Pressed += CloseDialog;
        _roomNameEdit.TextSubmitted += _ => OnCreatePressed();
        _maxPlayersSpin.MinValue = 2;
        _maxPlayersSpin.MaxValue = 6;
        _maxPlayersSpin.Step = 1;
        _maxPlayersSpin.Value = 4;
        _statusLabel.Text = "";
        ApplyBookStyle();
        Hide();
    }

    public void OpenDialog()
    {
        _roomNameEdit.Text = "";
        _maxPlayersSpin.Value = 4;
        SyncSeatChoices();
        _statusLabel.Text = "";
        SetLoading(false);
        Show();
        _roomNameEdit.GrabFocus();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && !_submitting && @event.IsActionPressed("ui_cancel"))
        {
            CloseDialog();
            GetViewport().SetInputAsHandled();
        }
    }

    private async void OnCreatePressed()
    {
        if (_submitting)
        {
            return;
        }

        var roomName = _roomNameEdit.Text.Trim();
        if (string.IsNullOrWhiteSpace(roomName))
        {
            ShowStatus("请输入房间名称。", true);
            return;
        }

        SetLoading(true);
        var result = await ApiClient.CreateRoomAsync(roomName, (int)_maxPlayersSpin.Value);
        if (!IsInsideTree())
        {
            return;
        }

        SetLoading(false);
        if (!result.Ok || result.Data == null)
        {
            ShowStatus(result.Message, true);
            return;
        }

        Session.SetCurrentRoom(result.Data.RoomId, string.IsNullOrWhiteSpace(result.Data.RoomName) ? roomName : result.Data.RoomName);
        CloseDialog();
        EmitSignal(SignalName.RoomCreated);
    }

    private void CloseDialog()
    {
        if (_submitting)
        {
            return;
        }

        Hide();
    }

    private void SetLoading(bool loading)
    {
        _submitting = loading;
        _createButton.Disabled = loading;
        _cancelButton.Disabled = loading;
        _closeButton.Disabled = loading;
        _roomNameEdit.Editable = !loading;
        _maxPlayersSpin.Editable = !loading;
        foreach (var choice in _seatChoices)
        {
            choice.Disabled = loading;
        }
        ShowStatus(loading ? "正在创建房间..." : "", false);
    }

    private void ShowStatus(string message, bool isError)
    {
        EntryBook.ShowStatus(_statusLabel, message, isError);
    }

    private readonly System.Collections.Generic.List<Button> _seatChoices = new();

    /// <summary>书签卡：从书顶插入的吊牌，压暗整本书；人数用 2–6 的圆形纸片单选。</summary>
    private void ApplyBookStyle()
    {
        var scrim = GetNode<ColorRect>("Scrim");
        scrim.Color = new Color(0.16f, 0.1f, 0.07f, 0.46f);

        var panel = GetNode<PanelContainer>("Center/Panel");
        panel.CustomMinimumSize = new Vector2(428, 0);
        var tag = EntryBook.Tex("parts/tag_card");
        if (tag != null)
        {
            panel.AddThemeStyleboxOverride("panel", new StyleBoxTexture
            {
                Texture = tag,
                TextureMarginLeft = 70,
                TextureMarginRight = 70,
                TextureMarginTop = 300,
                TextureMarginBottom = 190,
                ContentMarginLeft = 0,
                ContentMarginTop = 0,
                ContentMarginRight = 0,
                ContentMarginBottom = 0
            });
        }


        var margin = GetNode<MarginContainer>("Center/Panel/Margin");
        margin.AddThemeConstantOverride("margin_left", 46);
        margin.AddThemeConstantOverride("margin_right", 40);
        margin.AddThemeConstantOverride("margin_top", 268);
        margin.AddThemeConstantOverride("margin_bottom", 170);
        var form = GetNode<VBoxContainer>("Center/Panel/Margin/Form");
        form.AddThemeConstantOverride("separation", 14);

        var title = GetNode<Label>("Center/Panel/Margin/Form/Header/TitleBox/Title");
        EntryBook.StyleLabel(title, 36, EntryBook.Ink, EntryBook.TitleFont);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        // 关闭钮占了标题行右侧，标题用左侧等宽占位保持视觉居中。
        var header = GetNode<HBoxContainer>("Center/Panel/Margin/Form/Header");
        var balance = new Control { CustomMinimumSize = new Vector2(40, 0), MouseFilter = MouseFilterEnum.Ignore };
        header.AddChild(balance);
        header.MoveChild(balance, 0);
        GetNode<Label>("Center/Panel/Margin/Form/Header/TitleBox/Subtitle").Visible = false;
        GetNode<Control>("Center/Panel/Margin/Form/Header/TitleBox").SizeFlagsHorizontal = SizeFlags.ExpandFill;
        EntryBook.StyleIconButton(_closeButton, "close", 40, "关闭");
        _closeButton.SizeFlagsVertical = SizeFlags.ShrinkBegin;

        var fields = GetNode<VBoxContainer>("Center/Panel/Margin/Form/Fields");
        fields.AddThemeConstantOverride("separation", 10);
        EntryBook.StyleLabel(GetNode<Label>("Center/Panel/Margin/Form/Fields/RoomNameLabel"), 18, EntryBook.Ink, EntryBook.BoldFont);
        EntryBook.StyleInput(_roomNameEdit, "pencil", 50, 18);
        _roomNameEdit.PlaceholderText = "输入房间名称";
        var maxLabel = GetNode<Label>("Center/Panel/Margin/Form/Fields/PlayerRow/MaxPlayersLabel");
        EntryBook.StyleLabel(maxLabel, 18, EntryBook.Ink, EntryBook.BoldFont);
        maxLabel.CustomMinimumSize = new Vector2(0, 40);
        maxLabel.VerticalAlignment = VerticalAlignment.Bottom;
        _maxPlayersSpin.Visible = false;

        var choices = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        choices.AddThemeConstantOverride("separation", 10);
        fields.AddChild(choices);
        var group = new ButtonGroup();
        for (var count = 2; count <= 6; count++)
        {
            var value = count;
            var choice = new Button
            {
                Text = count.ToString(),
                ToggleMode = true,
                ButtonGroup = group,
                CustomMinimumSize = new Vector2(52, 52),
                FocusMode = FocusModeEnum.None,
                MouseDefaultCursorShape = CursorShape.PointingHand
            };
            choice.AddThemeStyleboxOverride("normal", SeatCircle(EntryBook.PaperLight, new Color("b9a68e")));
            choice.AddThemeStyleboxOverride("hover", SeatCircle(new Color("fbf7ef"), EntryBook.Gold));
            choice.AddThemeStyleboxOverride("pressed", SeatCircle(new Color("9d93ad"), new Color("857a96")));
            choice.AddThemeStyleboxOverride("hover_pressed", SeatCircle(new Color("9d93ad"), new Color("857a96")));
            choice.AddThemeStyleboxOverride("disabled", SeatCircle(EntryBook.Disabled, new Color("b5aea4")));
            choice.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            choice.AddThemeFontOverride("font", EntryBook.BoldFont);
            choice.AddThemeFontSizeOverride("font_size", 21);
            choice.AddThemeColorOverride("font_color", EntryBook.Ink);
            choice.AddThemeColorOverride("font_hover_color", EntryBook.Ink);
            choice.AddThemeColorOverride("font_pressed_color", new Color("fbf4e8"));
            choice.AddThemeColorOverride("font_hover_pressed_color", new Color("fbf4e8"));
            choice.Toggled += pressed =>
            {
                if (pressed)
                {
                    _maxPlayersSpin.Value = value;
                }
            };
            choices.AddChild(choice);
            _seatChoices.Add(choice);
        }

        SyncSeatChoices();
        EntryBook.StyleLabel(_statusLabel, 16, EntryBook.InfoInk);
        _statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        var buttons = GetNode<HBoxContainer>("Center/Panel/Margin/Form/ButtonRow");
        buttons.AddThemeConstantOverride("separation", 14);
        EntryBook.StyleCapsule(_cancelButton, EntryBook.CapsuleKind.Secondary, 52, 20);
        EntryBook.StyleCapsule(_createButton, EntryBook.CapsuleKind.Primary, 52, 21);
        _cancelButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _createButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    private void SyncSeatChoices()
    {
        foreach (var choice in _seatChoices)
        {
            choice.SetPressedNoSignal(choice.Text == ((int)_maxPlayersSpin.Value).ToString());
        }
    }

    private static StyleBoxFlat SeatCircle(Color fill, Color border) => new()
    {
        BgColor = fill,
        BorderColor = border,
        BorderWidthLeft = 2,
        BorderWidthTop = 2,
        BorderWidthRight = 2,
        BorderWidthBottom = 3,
        CornerRadiusTopLeft = 26,
        CornerRadiusTopRight = 26,
        CornerRadiusBottomLeft = 26,
        CornerRadiusBottomRight = 26,
        AntiAliasing = true
    };
}
