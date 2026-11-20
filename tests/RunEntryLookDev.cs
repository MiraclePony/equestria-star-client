using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EquestriaStar.Api;
using EquestriaStar.State;
using EquestriaStar.UI;
using Godot;

/// <summary>
/// 入口与等待大厅现状出图（不连服务端）：登录、注册、大厅、创建房间、等待房间（房主 / 普通玩家）、离房确认。
/// 输出到 artifacts/entry-lookdev/。用法：godot --path . --resolution 1920x1080 --script res://tests/RunEntryLookDev.cs
/// </summary>
public partial class RunEntryLookDev : SceneTree
{
    public override void _Initialize()
    {
        Callable.From(() => { _ = RunAsync(); }).CallDeferred();
    }

    private async Task RunAsync()
    {
        try
        {
            var outDir = ProjectSettings.GlobalizePath("res://artifacts/entry-lookdev");
            System.IO.Directory.CreateDirectory(outDir);
            var api = Root.GetNode<ApiClient>("ApiClient");
            var session = Root.GetNode<Session>("Session");
            var transport = new LookDevTransport();
            BookParallax.Enabled = false; // 截图要确定，不跟随鼠标
            PageTurn.Enabled = false;     // 常规截图不播放翻页 / 封面
            api.SetTransportForTests(transport);

            var app = GD.Load<PackedScene>("res://scenes/app/App.tscn").Instantiate<App>();
            Root.AddChild(app);
            await Capture($"{outDir}/01_login.png");

            app.ShowRegister("pony_star");
            await Capture($"{outDir}/02_register.png");

            session.SetAuth(new LoginResponseDto
            {
                AccessToken = "lookdev",
                User = new UserDto { UserId = 1, Username = "pony_star", Nickname = "小星" }
            });
            app.ShowLobby();
            await Capture($"{outDir}/03_lobby.png", 30);
            var dialog = FindChild<CreateRoomDialog>(app);
            dialog?.OpenDialog();
            await Capture($"{outDir}/04_create_room.png");

            session.SetCurrentRoom(10, "小马镇周末局");
            transport.Detail = Room(ownerSelf: true);
            app.ShowRoom();
            await Capture($"{outDir}/05_room_owner.png", 30);

            session.SetAuth(new LoginResponseDto
            {
                AccessToken = "lookdev",
                User = new UserDto { UserId = 2, Username = "yun", Nickname = "云卷" }
            });
            session.SetCurrentRoom(10, "小马镇周末局");
            app.ShowRoom();
            await Capture($"{outDir}/06_room_member.png", 30);

            // 视差：固定把“视线”推到左上 / 右下各拍一张，对比深层纸片的位移。
            app.ShowLobby();
            BookParallax.Enabled = true;
            BookParallax.ForcedDirection = new Vector2(-1, -1);
            await Capture($"{outDir}/07_parallax_left.png", 20);
            BookParallax.ForcedDirection = new Vector2(1, 1);
            await Capture($"{outDir}/08_parallax_right.png", 20);
            BookParallax.ForcedDirection = null;
            BookParallax.Enabled = false;

            // 选角页：房间进入 CHARACTER_SELECTING，其他玩家已选“大布莱恩”。
            transport.Detail = Room(ownerSelf: false);
            transport.Detail.Status = "CHARACTER_SELECTING";
            transport.Detail.CharacterSelectionDeadline = DateTime.Now.AddSeconds(45).ToString("yyyy-MM-ddTHH:mm:ss.ffffff");
            transport.Detail.Members![0].SelectedCharacterId = 102;
            app.ShowCharacterSelect();
            await Capture($"{outDir}/09_character_select.png", 45);

            // 翻书：定格几个角度出图。
            app.ShowLogin("yun");
            await Capture($"{outDir}/_tmp.png", 10);
            PageTurn.Enabled = true;
            foreach (var (name, angle) in new[] { ("10_open_cover", 0f), ("11_open_turning", 1.25f), ("12_open_landing", 2.45f), ("12b_open_end", Mathf.Pi) })
            {
                PageTurn.FreezeAngle = angle;
                await PageTurn.PlayOpening(app);
                await Capture($"{outDir}/{name}.png", 4);
                PageTurn.ClearFrozen();
            }

            session.SetAuth(new LoginResponseDto
            {
                AccessToken = "lookdev",
                User = new UserDto { UserId = 1, Username = "pony_star", Nickname = "小星" }
            });
            foreach (var (name, angle, forward) in new[] { ("13_turn_forward_a", 0.95f, true), ("14_turn_forward_b", 2.2f, true), ("14b_forward_end", Mathf.Pi, true), ("15_turn_back", 1.4f, false), ("15b_back_end", Mathf.Pi, false) })
            {
                PageTurn.Enabled = false;
                if (forward)
                {
                    app.ShowLogin("yun");
                }
                else
                {
                    app.ShowLobby();
                }

                await Capture($"{outDir}/_tmp.png", 20);
                PageTurn.Enabled = true;
                PageTurn.FreezeAngle = angle;
                if (forward)
                {
                    app.ShowLobby();
                }
                else
                {
                    app.ShowLogin("yun");
                }

                await Capture($"{outDir}/{name}.png", 20);
                PageTurn.ClearFrozen();
            }

            PageTurn.FreezeAngle = null;
            PageTurn.Enabled = false;
            app.ShowLobby();
            await Capture($"{outDir}/16_ref_lobby.png", 20);
            app.ShowLogin("yun");
            await Capture($"{outDir}/16_ref_login.png", 20);
            System.IO.File.Delete($"{outDir}/_tmp.png");
            GD.Print("ENTRY_LOOKDEV_DONE");
        }
        catch (Exception exception)
        {
            GD.PushError($"ENTRY_LOOKDEV_FAILED {exception}");
        }

        Quit();
    }

    private static T? FindChild<T>(Node node) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T match)
            {
                return match;
            }

            var nested = FindChild<T>(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private async Task Capture(string path, int frames = 12)
    {
        for (var i = 0; i < frames; i++)
        {
            await ToSignal(this, SignalName.ProcessFrame);
        }

        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = Root.GetViewport().GetTexture().GetImage();
        image.SavePng(path);
        GD.Print($"ENTRY_CAPTURE {path}");
    }

    private static RoomDetailDto Room(bool ownerSelf) => new()
    {
        RoomId = 10,
        RoomName = "小马镇周末局",
        OwnerUserId = 1,
        OwnerNickname = "小星",
        Status = "WAITING",
        CurrentPlayers = 4,
        MaxPlayers = 5,
        Members =
        [
            new() { UserId = 1, Nickname = "小星", SeatNo = 1, MemberRole = "OWNER", ReadyStatus = "NOT_READY" },
            new() { UserId = 2, Nickname = "云卷", SeatNo = 2, MemberRole = "MEMBER", ReadyStatus = "READY" },
            new() { UserId = 3, Nickname = "风蹄", SeatNo = 3, MemberRole = "MEMBER", ReadyStatus = "READY" },
            new() { UserId = 4, Nickname = "莓露的超长昵称示例", SeatNo = 4, MemberRole = "MEMBER", ReadyStatus = "NOT_READY" }
        ]
    };

    private sealed class LookDevTransport : IApiTransport
    {
        public RoomDetailDto Detail { get; set; } = new();

        public Task<ApiResult<T>> RequestJsonAsync<T>(HttpClient.Method method, string path, object? body, bool authorized, IReadOnlyDictionary<string, string?>? query)
        {
            if (typeof(T) == typeof(RoomDetailDto))
            {
                return Task.FromResult((ApiResult<T>)(object)ApiResult<RoomDetailDto>.Success(Detail, "success", 200));
            }

            if (typeof(T) == typeof(List<CharacterDto>))
            {
                var characters = new List<CharacterDto>
                {
                    new() { CharacterId = 101, Code = "INITIAL_ORANGE_FEATHER", Name = "橙羽", CharacterType = "INITIAL" },
                    new() { CharacterId = 102, Code = "INITIAL_BIG_BRIAN", Name = "大布莱恩", CharacterType = "INITIAL" },
                    new() { CharacterId = 103, Code = "INITIAL_XUN_YU", Name = "循雨", CharacterType = "INITIAL" },
                    new() { CharacterId = 104, Code = "INITIAL_YE_LING", Name = "夜灵", CharacterType = "INITIAL" },
                    new() { CharacterId = 105, Code = "INITIAL_DAN_QING", Name = "丹青", CharacterType = "INITIAL" }
                };
                return Task.FromResult((ApiResult<T>)(object)ApiResult<List<CharacterDto>>.Success(characters, "success", 200));
            }

            if (typeof(T) == typeof(RoomPageDto))
            {
                var page = new RoomPageDto
                {
                    Page = 1,
                    PageSize = 50,
                    Total = 5,
                    Records =
                    [
                        new() { RoomId = 10, RoomName = "小马镇周末局", OwnerNickname = "小星", Status = "WAITING", CurrentPlayers = 4, MaxPlayers = 5 },
                        new() { RoomId = 11, RoomName = "新手教学房", OwnerNickname = "云卷", Status = "WAITING", CurrentPlayers = 2, MaxPlayers = 4 },
                        new() { RoomId = 12, RoomName = "永恒自由森林探险", OwnerNickname = "风蹄", Status = "PLAYING", CurrentPlayers = 6, MaxPlayers = 6 },
                        new() { RoomId = 13, RoomName = "满员测试", OwnerNickname = "墨铃", Status = "WAITING", CurrentPlayers = 3, MaxPlayers = 3 },
                        new() { RoomId = 14, RoomName = "坎特洛特下午茶", OwnerNickname = "莓露", Status = "WAITING", CurrentPlayers = 1, MaxPlayers = 6 }
                    ]
                };
                return Task.FromResult((ApiResult<T>)(object)ApiResult<RoomPageDto>.Success(page, "success", 200));
            }

            return Task.FromResult(ApiResult<T>.Failure("出图模式不连服务端", "LOOKDEV"));
        }
    }
}
