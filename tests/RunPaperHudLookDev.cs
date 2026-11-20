using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EquestriaStar.Api;
using EquestriaStar.Game.UI;
using Godot;

/// <summary>
/// 折纸 HUD 实机出图（不连服务端）：可控地图色背景 + 新 HUD + 模拟数据，
/// 依次导出 收起 / 行为菜单展开 / 历史抽屉 / 聊天抽屉 四张图到 artifacts/paper-hud/。
/// 用法：godot --path . --rendering-method forward_plus --resolution 1920x1080 --script res://tests/RunPaperHudLookDev.cs
/// </summary>
public partial class RunPaperHudLookDev : SceneTree
{
    private const long SelfUserId = 20;

    public override void _Initialize()
    {
        Callable.From(() => { _ = RunAsync(); }).CallDeferred();
    }

    private async Task RunAsync()
    {
        try
        {
            GD.Print("PAPER_HUD_LOOKDEV_BEGIN");
            var outDir = ProjectSettings.GlobalizePath("res://artifacts/paper-hud");
            System.IO.Directory.CreateDirectory(outDir);
            // 视觉夹具不加载正式 PCK，避免把地图加载性能混入 HUD 出图。
            // 正式 GameScreen 与地图组合由 GameScreen 回归和窗口验收单独覆盖。
            var background = new ColorRect
            {
                Color = new Color("9fc89b"),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            Root.AddChild(background);

            GD.Print("PAPER_HUD_LOOKDEV_ADD_HUD");
            var hud = GD.Load<PackedScene>("res://scenes/game/ui/GameHud.tscn").Instantiate<GameHud>();
            Root.AddChild(hud);
            await Frames(2);
            GD.Print("PAPER_HUD_LOOKDEV_HUD_READY");

            var view = CreateView();
            hud.BindPlayerView(view, SelfUserId);
            hud.SetConnectionReady(true);
            hud.SetMovementRulesVersion(2);
            hud.SetAvailableActions(
            [
                new AvailableActionDto { ActionType = "END_TURN", Available = true },
                new AvailableActionDto { ActionType = "WORK", Available = true },
                new AvailableActionDto { ActionType = "USE_RAILWAY", Available = false, ReasonText = "当前格没有车站" }
            ], view.StateVersion);
            hud.SetInventoryPreview(CreateItems());
            hud.SetPartnerPreview(CreatePartners());
            hud.SetPersonalQuestCount(2);

            if (Array.Exists(OS.GetCmdlineUserArgs(), value => value == "--paper-hud-visual-check"))
            {
                GD.Print("PAPER_HUD_VISUAL_CHECK_READY");
                return;
            }

            // canvas_items 拉伸下逻辑尺寸恒为 1920，按真实窗口尺寸区分文件名。
            var scale = DisplayServer.WindowGetSize().X < 1500 ? "_960" : "";
            await Capture($"{outDir}/hud_closed{scale}.png");

            hud.ActionMenuButton.EmitSignal(BaseButton.SignalName.Pressed);
            await Capture($"{outDir}/hud_action_menu{scale}.png");
            hud.CloseActionMenu();

            hud.HistoryHandle.EmitSignal(BaseButton.SignalName.Pressed);
            hud.SetActionLogs(CreateLogs());
            hud.ChatHandle.EmitSignal(BaseButton.SignalName.Pressed);
            await Capture($"{outDir}/hud_drawers{scale}.png");

            hud.HistoryHandle.EmitSignal(BaseButton.SignalName.Pressed);
            hud.ChatHandle.EmitSignal(BaseButton.SignalName.Pressed);

            // 右侧拉框抽屉：使用真实任务卡 / 道具卡图。
            var viewer = GD.Load<PackedScene>("res://scenes/game/ui/CardViewerOverlay.tscn").Instantiate<CardViewerOverlay>();
            Root.AddChild(viewer);
            await Frames(2);
            var quests = new List<CardViewerCard>();
            foreach (var code in CardArtCatalog.QuestArtPaths.Keys)
            {
                if (quests.Count < 3 && CardArtCatalog.TryGetQuestTexture(code, out var texture, out _) && texture != null)
                {
                    quests.Add(new CardViewerCard(code, texture, QuestId: quests.Count + 1, OfferId: quests.Count + 1));
                }
            }

            viewer.OpenLoading(CardViewerKind.QuestBoard);
            viewer.ShowCards(CardViewerKind.QuestBoard, quests, new CardActionAvailability(true, ""));
            await Capture($"{outDir}/hud_quest_drawer{scale}.png");
            viewer.UpdateActionAvailability(new CardActionAvailability(false, "当前不是你的回合"));
            await Capture($"{outDir}/hud_quest_drawer_disabled{scale}.png");

            var items = new List<CardViewerCard>();
            foreach (var id in CardArtCatalog.ItemArtPaths.Keys)
            {
                if (items.Count < 3 && CardArtCatalog.TryGetItemTexture(id, out var texture, out _) && texture != null)
                {
                    items.Add(new CardViewerCard($"I{id}", texture, ShelfSlotId: items.Count + 1, ItemId: id));
                }
            }

            viewer.CloseOverlay();
            viewer.OpenLoading(CardViewerKind.ShopShelf);
            viewer.ShowCards(CardViewerKind.ShopShelf, items, new CardActionAvailability(false, "金币不足"));
            await Capture($"{outDir}/hud_shop_drawer{scale}.png");

            GD.Print("PAPER_HUD_LOOKDEV_DONE");
        }
        catch (Exception exception)
        {
            GD.PushError($"PAPER_HUD_LOOKDEV_FAILED {exception}");
        }

        Quit();
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await ToSignal(this, SignalName.ProcessFrame);
        }
    }

    private async Task Capture(string path)
    {
        await Frames(12);
        if (DisplayServer.GetName().Equals("headless", StringComparison.OrdinalIgnoreCase))
        {
            GD.Print($"PAPER_HUD_CAPTURE_SKIPPED_HEADLESS {path}");
            return;
        }

        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = Root.GetViewport().GetTexture().GetImage();
        image.SavePng(path);
        GD.Print($"PAPER_HUD_CAPTURE {path} {image.GetWidth()}x{image.GetHeight()}");
    }

    private static PlayerViewResponseDto CreateView() => new()
    {
        GameId = 100,
        GameStatus = "RUNNING",
        CurrentRound = 6,
        CurrentTurnUserId = SelfUserId,
        StateVersion = 42,
        EngineState = new EngineStateDto { Phase = "MIDDLE" },
        Players =
        [
            Player(1001, 10, "云卷", 1, "INITIAL_HONG_FANG", "红方", 9, 5),
            Player(1002, SelfUserId, "小星", 2, "INITIAL_CHANCE_DICE", "机遇骰子", 12, 8),
            Player(1003, 30, "风蹄", 3, "INITIAL_YE_LING", "夜灵", 7, 4),
            Player(1004, 40, "莓露的超长昵称示例", 4, "INITIAL_DAN_QING", "丹青", 5, 6),
            Player(1005, 50, "墨铃", 5, "INITIAL_TURBID_STAR", "浊星", 10, 3)
        ]
    };

    private static PlayerViewPlayerDto Player(long playerId, long userId, string nick, int order, string code, string character, int coins, int prestige) => new()
    {
        PlayerId = playerId,
        UserId = userId,
        Nickname = nick,
        TurnOrder = order,
        InitialCharacterCode = code,
        InitialCharacterName = character,
        Coins = coins,
        Prestige = prestige,
        EffectiveAttributes = new EffectiveAttributesDto { Strength = 3, Speed = 2, Social = 4, Charm = 1, Magic = 2, Mind = 3 }
    };

    private static List<PlayerItemDto> CreateItems() =>
    [
        Item(1, 1017, "药剂", "HAND"),
        Item(2, 1003, "苹果", "HAND"),
        Item(3, 1013, "魔法书", "HAND"),
        Item(4, 1005, "花束", "HAND"),
        Item(5, 1017, "药剂", "HAND"),
        Item(6, 1018, "蹄铁", "EQUIPPED"),
        Item(7, 9999, "未映射的道具", "EQUIPPED")
    ];

    private static PlayerItemDto Item(long id, long itemId, string name, string location) => new()
    {
        PlayerItemId = id,
        ItemId = itemId,
        ItemName = name,
        Location = location
    };

    private static List<PlayerPartnerDto> CreatePartners() =>
    [
        new() { PlayerPartnerId = 1, PartnerCode = "P_RD", PartnerName = "云宝", Status = "ACTIVE" },
        new() { PlayerPartnerId = 2, PartnerCode = "P_FS", PartnerName = "Fluttershy", Status = "ACTIVE" },
        new() { PlayerPartnerId = 3, PartnerCode = "P_UNKNOWN", PartnerName = "未映射伙伴", Status = "PENDING_ACTIVE", StatusText = "待生效" }
    ];

    private static List<ActionLogDto> CreateLogs() =>
    [
        new() { PlayerId = 1001, ActionType = "WORK", PlayerName = "云卷", Message = "打工成功，获得 3 金币。" },
        new() { PlayerId = 1002, ActionType = "ACCEPT_QUEST", PlayerName = "小星", Message = "接取了任务「送信」。" },
        new() { PlayerId = 1003, ActionType = "MOVE", PlayerName = "风蹄", Message = "移动到小马镇。" },
        new() { PlayerId = 1005, ActionType = "SUBMIT_QUEST", PlayerName = "墨铃", Message = "提交任务，声望 +2。" }
    ];

}
