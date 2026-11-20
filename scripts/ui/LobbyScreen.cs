using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using EquestriaStar.Api;
using EquestriaStar.State;

namespace EquestriaStar.UI;

public partial class LobbyScreen : Control
{
    [Signal]
    public delegate void EnterRoomEventHandler();

    [Signal]
    public delegate void LoggedOutEventHandler();

    private readonly PackedScene _roomCardScene = GD.Load<PackedScene>("res://scenes/components/RoomCard.tscn");

    private Label _playerLabel = null!;
    private Button _logoutButton = null!;
    private Button _createRoomButton = null!;
    private Button _refreshButton = null!;
    private LineEdit _searchEdit = null!;
    private Label _bannerLabel = null!;
    private Label _stateLabel = null!;
    private VBoxContainer _roomList = null!;
    private ScrollContainer _scrollContainer = null!;
    private Timer _refreshTimer = null!;
    private CreateRoomDialog _createRoomDialog = null!;

    private readonly List<RoomDto> _allRooms = new();
    private bool _active;
    private bool _refreshing;
    private int _generation;
    private string _lastFilterKeyword = "";

    private ApiClient ApiClient => GetNode<ApiClient>("/root/ApiClient");
    private Session Session => GetNode<Session>("/root/Session");

    public override void _Ready()
    {
        _playerLabel = GetNode<Label>("RootMargin/RootLayout/Header/TitleBox/PlayerLabel");
        _logoutButton = GetNode<Button>("RootMargin/RootLayout/Header/LogoutButton");
        _createRoomButton = GetNode<Button>("RootMargin/RootLayout/Toolbar/CreateRoomButton");
        _refreshButton = GetNode<Button>("RootMargin/RootLayout/Toolbar/RefreshButton");
        _searchEdit = GetNode<LineEdit>("RootMargin/RootLayout/Toolbar/SearchEdit");
        _bannerLabel = GetNode<Label>("RootMargin/RootLayout/BannerLabel");
        _stateLabel = GetNode<Label>("RootMargin/RootLayout/RoomScroll/RoomList/StateLabel");
        _roomList = GetNode<VBoxContainer>("RootMargin/RootLayout/RoomScroll/RoomList");
        _scrollContainer = GetNode<ScrollContainer>("RootMargin/RootLayout/RoomScroll");
        _refreshTimer = GetNode<Timer>("RefreshTimer");
        _createRoomDialog = GetNode<CreateRoomDialog>("CreateRoomDialog");

        _playerLabel.Text = $"当前玩家：{Session.GetPlayerName()}";
        ApplyBookStyle();
        _logoutButton.Pressed += OnLogoutPressed;
        _createRoomButton.Pressed += _createRoomDialog.OpenDialog;
        _refreshButton.Pressed += () => RefreshRooms(true);
        _searchEdit.TextChanged += _ => ApplyFilter(true);
        _refreshTimer.Timeout += () => RefreshRooms(false);
        _createRoomDialog.RoomCreated += () => EmitSignal(SignalName.EnterRoom);
        _refreshTimer.WaitTime = 10.0;
        _refreshTimer.OneShot = false;
        ShowBanner("");
    }

    public void Start()
    {
        _active = true;
        _generation++;
        _refreshTimer.Start();
        RefreshRooms(false);
    }

    public void Stop()
    {
        _active = false;
        _generation++;
        _refreshTimer.Stop();
    }

    public void RefreshRooms(bool manual)
    {
        if (_refreshing)
        {
            if (manual)
            {
                ShowBanner("正在刷新房间列表，请稍候。");
            }

            return;
        }

        _ = RefreshRoomsAsync(_generation, manual);
    }

    private async Task RefreshRoomsAsync(int generation, bool manual)
    {
        _refreshing = true;
        _refreshButton.Disabled = true;
        var oldScroll = _scrollContainer.ScrollVertical;
        if (_allRooms.Count == 0)
        {
            SetState("正在获取房间列表...");
        }
        else if (manual)
        {
            ShowBanner("正在刷新房间列表...");
        }

        var fetch = await FetchAllRoomsWithRetryAsync();
        if (!IsInsideTree() || !_active || generation != _generation)
        {
            _refreshing = false;
            return;
        }

        _refreshing = false;
        _refreshButton.Disabled = false;
        if (!fetch.Ok)
        {
            if (_allRooms.Count == 0)
            {
                SetState(fetch.Message);
            }
            else
            {
                ShowBanner(fetch.Message);
                ApplyFilter(false);
                _scrollContainer.ScrollVertical = oldScroll;
            }

            return;
        }

        _allRooms.Clear();
        _allRooms.AddRange(fetch.Rooms);
        ShowBanner(fetch.Warning ?? "");
        ApplyFilter(false);
        if (_searchEdit.Text.Trim() == _lastFilterKeyword)
        {
            _scrollContainer.ScrollVertical = oldScroll;
        }
    }

    private async Task<RoomFetchResult> FetchAllRoomsWithRetryAsync()
    {
        var firstTry = await FetchAllRoomsOnceAsync();
        if (firstTry.Ok)
        {
            return firstTry;
        }

        if (firstTry.RetryReason == "DEDUP_MISMATCH")
        {
            var secondTry = await FetchAllRoomsOnceAsync();
            return secondTry.Ok ? secondTry : RoomFetchResult.Failure("房间列表不完整，已保留旧列表。");
        }

        return firstTry;
    }

    private async Task<RoomFetchResult> FetchAllRoomsOnceAsync()
    {
        var first = await ApiClient.RoomsPageAsync(1, ApiConfig.PageSize);
        if (!first.Ok || first.Data == null)
        {
            return RoomFetchResult.Failure(first.Message);
        }

        var total = first.Data.Total;
        if (RoomListModel.TotalExceedsPageLimit(total, ApiConfig.PageSize, ApiConfig.MaxRoomPages))
        {
            return RoomFetchResult.Failure("房间数量超过客户端保护上限，已保留旧列表。");
        }

        var pages = new List<IEnumerable<RoomDto>> { first.Data.Records ?? [] };
        var pageCount = RoomListModel.PageCountFromTotal(total, ApiConfig.PageSize, ApiConfig.MaxRoomPages);
        for (var page = 2; page <= pageCount; page++)
        {
            var response = await ApiClient.RoomsPageAsync(page, ApiConfig.PageSize);
            if (!response.Ok || response.Data == null)
            {
                return RoomFetchResult.Failure(response.Message);
            }

            pages.Add(response.Data.Records ?? []);
        }

        var merged = RoomListModel.MergeUniqueByRoomId(pages);
        if (merged.Count < total)
        {
            return RoomFetchResult.Failure("房间列表不完整，正在重试。", "DEDUP_MISMATCH");
        }

        return RoomFetchResult.Success(merged);
    }

    private void ApplyFilter(bool scrollToTop)
    {
        foreach (var child in _roomList.GetChildren())
        {
            if (child != _stateLabel)
            {
                child.QueueFree();
            }
        }

        var keyword = _searchEdit.Text.Trim();
        _lastFilterKeyword = keyword;
        var rooms = RoomListModel.FilterRooms(_allRooms, keyword);
        if (rooms.Count == 0)
        {
            SetState(_allRooms.Count == 0 ? "当前没有房间，可以创建一个新房间。" : "没有找到匹配的房间。");
        }
        else
        {
            _stateLabel.Visible = false;
            foreach (var room in rooms)
            {
                var card = _roomCardScene.Instantiate<RoomCard>();
                _roomList.AddChild(card);
                card.JoinRequested += OnJoinRequested;
                card.Setup(room);
            }
        }

        if (scrollToTop)
        {
            _scrollContainer.ScrollVertical = 0;
        }
    }

    private async void OnJoinRequested(RoomCard card)
    {
        var room = card.Room;
        if (!RoomListModel.CanJoin(room))
        {
            ShowBanner("这个房间当前不可加入。");
            return;
        }

        card.SetJoinLoading(true);
        var result = await ApiClient.JoinRoomAsync(room.RoomId);
        if (!IsInsideTree())
        {
            return;
        }

        if (!result.Ok)
        {
            card.SetJoinLoading(false);
            ShowBanner(result.Message);
            return;
        }

        Session.SetCurrentRoom(room.RoomId, room.RoomName);
        EmitSignal(SignalName.EnterRoom);
    }

    private async void OnLogoutPressed()
    {
        _logoutButton.Disabled = true;
        await ApiClient.LogoutAsync();
        Session.ClearAuth();
        if (IsInsideTree())
        {
            EmitSignal(SignalName.LoggedOut);
        }
    }

    /// <summary>目录页：左页小镇隧道书，右页是房间目录；玩家书签从书顶垂下。</summary>
    private void ApplyBookStyle()
    {
        var stage = EntryBook.BuildStage(this, "lobby");
        var page = EntryBook.RightPage["lobby"];
        EntryBook.Footer(stage, page);

        var rootMargin = GetNode<MarginContainer>("RootMargin");
        EntryBook.PlaceOnStage(rootMargin, page);
        rootMargin.AddThemeConstantOverride("margin_left", 30);
        rootMargin.AddThemeConstantOverride("margin_right", 30);
        rootMargin.AddThemeConstantOverride("margin_top", 6);
        rootMargin.AddThemeConstantOverride("margin_bottom", 60);
        var layout = GetNode<VBoxContainer>("RootMargin/RootLayout");
        layout.AddThemeConstantOverride("separation", 14);

        // 页眉：小标志 | 游戏大厅 …… 右侧留给书签
        var header = GetNode<HBoxContainer>("RootMargin/RootLayout/Header");
        header.AddThemeConstantOverride("separation", 18);
        header.CustomMinimumSize = new Vector2(0, 84);
        var crest = EntryBook.Crest(196, 22);
        crest.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(crest);
        header.MoveChild(crest, 0);
        var bar = EntryBook.MakeLabel("|", 34, EntryBook.InkSoft);
        header.AddChild(bar);
        header.MoveChild(bar, 1);
        var titleBox = GetNode<VBoxContainer>("RootMargin/RootLayout/Header/TitleBox");
        titleBox.Alignment = BoxContainer.AlignmentMode.Center;
        EntryBook.StyleLabel(titleBox.GetNode<Label>("Title"), 38, EntryBook.Ink, EntryBook.TitleFont);
        _playerLabel.Visible = false;
        var headRule = EntryBook.Rule(1.5f, new Color(EntryBook.InkSoft, 0.35f));
        layout.AddChild(headRule);
        layout.MoveChild(headRule, 1);

        var bookmark = EntryBook.PlayerBookmark(Session.GetPlayerName(), _logoutButton);
        AddChild(bookmark);
        MoveChild(bookmark, _createRoomDialog.GetIndex());
        EntryBook.PlaceOnStage(bookmark, new Rect2(page.End.X - 330, 0, 300, 140));

        // 工具栏：搜索 / 刷新 / 创建房间（本页唯一主按钮）
        var toolbar = GetNode<HBoxContainer>("RootMargin/RootLayout/Toolbar");
        toolbar.AddThemeConstantOverride("separation", 16);
        EntryBook.StyleInput(_searchEdit, "search", 52, 19);
        _searchEdit.PlaceholderText = "搜索房间名称";
        EntryBook.StyleCapsule(_refreshButton, EntryBook.CapsuleKind.Warm, 52, 20, "refresh");
        _refreshButton.CustomMinimumSize = new Vector2(132, 52);
        EntryBook.StyleCapsule(_createRoomButton, EntryBook.CapsuleKind.Primary, 56, 24, "plus");
        _createRoomButton.CustomMinimumSize = new Vector2(236, 56);

        var section = EntryBook.SectionHeader("目录");
        layout.AddChild(section);
        layout.MoveChild(section, toolbar.GetIndex() + 1);

        EntryBook.StyleLabel(_bannerLabel, 16, EntryBook.InfoInk);
        _bannerLabel.HorizontalAlignment = HorizontalAlignment.Center;
        EntryBook.StyleLabel(_stateLabel, 19, EntryBook.InkSoft);
        _stateLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _stateLabel.CustomMinimumSize = new Vector2(0, 120);
        _roomList.AddThemeConstantOverride("separation", 4);
        _scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
    }

    private void SetState(string message)
    {
        _stateLabel.Text = message;
        _stateLabel.Visible = true;
    }

    private void ShowBanner(string message)
    {
        _bannerLabel.Text = message;
        _bannerLabel.Visible = !string.IsNullOrWhiteSpace(message);
    }

    private sealed class RoomFetchResult
    {
        public bool Ok { get; private init; }
        public string Message { get; private init; } = "";
        public string? RetryReason { get; private init; }
        public string? Warning { get; private init; }
        public List<RoomDto> Rooms { get; private init; } = [];

        public static RoomFetchResult Success(List<RoomDto> rooms, string? warning = null)
        {
            return new RoomFetchResult { Ok = true, Rooms = rooms, Warning = warning };
        }

        public static RoomFetchResult Failure(string message, string? retryReason = null)
        {
            return new RoomFetchResult { Ok = false, Message = message, RetryReason = retryReason };
        }
    }
}
