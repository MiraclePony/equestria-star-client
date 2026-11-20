using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using EquestriaStar.Api;
using EquestriaStar.Game.Dice;
using EquestriaStar.Game.Map;
using EquestriaStar.Game.UI;
using Godot;

/// <summary>
/// 游戏内二级 / 三级 UI 纸艺换皮出图（不连服务端，只用本地模拟数据）。
/// 依次导出个人任务、放弃确认、伙伴招募候选、招募确认、背包、打工结果到 artifacts/ingame-ui/&lt;标签&gt;/。
/// 用法：godot --path . --rendering-method forward_plus --resolution 1920x1080 --script res://tests/RunInGameUiLookDev.cs -- --tag=after
/// 截图只是视觉证据，不代替真实 GUI 输入测试。
/// </summary>
public partial class RunInGameUiLookDev : SceneTree
{
    public override void _Initialize()
    {
        Callable.From(() => { _ = RunAsync(); }).CallDeferred();
    }

    private async Task RunAsync()
    {
        try
        {
            var tag = "after";
            foreach (var arg in OS.GetCmdlineUserArgs())
            {
                if (arg.StartsWith("--tag=", StringComparison.Ordinal))
                {
                    tag = arg["--tag=".Length..];
                }
            }

            var small = DisplayServer.WindowGetSize().X < 1500 ? "_960" : "";
            var outDir = ProjectSettings.GlobalizePath($"res://artifacts/ingame-ui/{tag}");
            System.IO.Directory.CreateDirectory(outDir);
            Root.GetNode<ApiClient>("ApiClient").SetTransportForTests(new LookDevTransport());

            var map = GD.Load<PackedScene>("res://scenes/game/map/GameMapScene.tscn").Instantiate<GameMapScene>();
            map.AutoLoadOnReady = false;
            Root.AddChild(map);
            await Frames(2);
            await map.LoadMapAsync();
            if (map.GetNodeOrNull<Control>("CanvasLayer/MapInfoMargin") is { } mapInfo)
            {
                mapInfo.Visible = false;
            }

            // D01 / D02：个人任务列表与放弃确认。
            var journal = GD.Load<PackedScene>("res://scenes/game/ui/QuestJournalOverlay.tscn").Instantiate<QuestJournalOverlay>();
            Root.AddChild(journal);
            await Frames(2);
            journal.ShowTasks(
            [
                new QuestTaskDisplay(501, 14, "送信到小马镇",
                [
                    new QuestObjectiveDisplay("前往小马镇邮局", true, "（1/1）", true),
                    new QuestObjectiveDisplay("交付 1 件杂物", false, "（0/1）", true)
                ], false, "目标尚未全部完成", true, ""),
                new QuestTaskDisplay(502, 38, "蛋糕谜案",
                [
                    new QuestObjectiveDisplay("速度判定", true, "（1/1）", true),
                    new QuestObjectiveDisplay("社交判定", true, "（1/1）", true),
                    new QuestObjectiveDisplay("魔法判定", true, "（1/1）", true)
                ], true, "", true, ""),
                new QuestTaskDisplay(503, 52, "森林探险",
                [
                    new QuestObjectiveDisplay("抵达永恒自由森林", false, "", false)
                ], false, "目标尚未全部完成", false, "声望不足，无法放弃")
            ]);
            await Capture($"{outDir}/D01_quest_list{small}.png");
            if (journal.FindChild("AbandonButton", true, false) is Button abandon)
            {
                abandon.EmitSignal(BaseButton.SignalName.Pressed);
                await Capture($"{outDir}/D02_abandon_confirm{small}.png");
            }

            journal.ForceClose();

            // G03 / G04：伙伴招募候选与确认。
            var partner = GD.Load<PackedScene>("res://scenes/game/ui/PartnerOverlay.tscn").Instantiate<PartnerOverlay>();
            Root.AddChild(partner);
            await Frames(2);
            partner.ShowReward(NewReward(), 11, true, false);
            await Capture($"{outDir}/G03_partner_candidates{small}.png");
            if (partner.FindChild("Condition_704_1", true, false) is Button condition)
            {
                condition.EmitSignal(BaseButton.SignalName.Pressed);
                await Capture($"{outDir}/G04_partner_confirm{small}.png");
            }

            partner.ForceClose();

            // E01：判定任务列表（可用 / 已完成 / 不可用三种行）。
            var check = GD.Load<PackedScene>("res://scenes/game/ui/QuestCheckOverlay.tscn").Instantiate<QuestCheckOverlay>();
            Root.AddChild(check);
            await Frames(2);
            check.ShowContent(
            [
                new QuestCheckTaskView(149, 403, "蛋糕谜案", "QUEST_CHECK", QuestCheckModel.SocialCheck, false,
                [
                    new QuestCheckRowView("SPEED", false, [], 6, true, false, "该属性已完成"),
                    new QuestCheckRowView("SOCIAL", false, [], 7, false, true, ""),
                    new QuestCheckRowView("MAGIC", false, [], 8, false, false, "当前位置不可用")
                ]),
                new QuestCheckTaskView(138, 401, "任选属性判定", "QUEST_CHECK", QuestCheckModel.SocialCheck, false,
                [
                    new QuestCheckRowView("ANY", true, ["STRENGTH", "MAGIC", "MIND"], 7, false, true, "")
                ])
            ], "");
            await Capture($"{outDir}/E01_quest_check{small}.png");
            check.CloseOverlay(false);

            // D06：行为“提交”任务列表。
            var target = GD.Load<PackedScene>("res://scenes/game/ui/QuestTargetSubmissionOverlay.tscn").Instantiate<QuestTargetSubmissionOverlay>();
            Root.AddChild(target);
            await Frames(2);
            target.ShowTasks(
            [
                new QuestTargetSubmissionTaskView(14, 501, "送信到小马镇", QuestTargetSubmissionModel.SubmitItemTrigger, 1005, null, null, null, null, "交付 1 件「花束」", true, ""),
                new QuestTargetSubmissionTaskView(52, 503, "修缮市政厅", "PAY_RESOURCE", null, null, 6, null, 1, "支付 6 金币 + 1 社交", false, "金币不足")
            ], new PlayerItemsResponseDto { Items = [] }, "");
            await Capture($"{outDir}/D06_target_submission{small}.png");
            target.CloseOverlay(false);

            // C01：背包。
            var viewer = GD.Load<PackedScene>("res://scenes/game/ui/CardViewerOverlay.tscn").Instantiate<CardViewerOverlay>();
            Root.AddChild(viewer);
            await Frames(2);
            var cards = new List<CardViewerCard>();
            foreach (var (playerItemId, itemId, name, type) in new[] { (1L, 1017L, "药剂", "CONSUMABLE"), (2L, 1018L, "蹄铁", "EQUIPMENT"), (3L, 1005L, "花束", "MISC") })
            {
                if (CardArtCatalog.TryGetItemTexture(itemId, out var texture, out _) && texture != null)
                {
                    cards.Add(new CardViewerCard($"P{playerItemId}", texture, ItemId: itemId, PlayerItemId: playerItemId, ItemName: name, ItemType: type, Location: "HAND",
                        ActionAvailability: type == "MISC" ? new CardActionAvailability(false, "杂物不能直接使用") : new CardActionAvailability(true, "")));
                }
            }

            viewer.OpenLoading(CardViewerKind.Inventory);
            viewer.ShowCards(CardViewerKind.Inventory, cards);
            await Capture($"{outDir}/C01_inventory{small}.png");
            viewer.CloseOverlay();

            // F05：打工结果。
            var dice = GD.Load<PackedScene>("res://scenes/game/dice/WorkDiceOverlay.tscn").Instantiate<WorkDiceOverlay>();
            Root.AddChild(dice);
            await Frames(2);
            var response = JsonSerializer.Deserialize<CheckRollResponseDto>("""
            {"submittedDice":5,"effectiveDice":5,
             "checkResult":{"dice":5,"attributeType":"SOCIAL","attributeTypeText":"社交","attributeValue":3,"modifier":0,"finalValue":8,"difficulty":7,"success":true},
             "workResult":{"socialCost":1,"remainingSocial":2,"rewardCoins":4,"coinsAfter":16}}
            """)!;
            // C02：消耗品使用确认（背包里点“使用”）。
            viewer.OpenLoading(CardViewerKind.Inventory);
            viewer.ShowCards(CardViewerKind.Inventory, cards);
            await Frames(2);
            if (viewer.ItemCards.Count > 0 && viewer.ItemCards[0].FindChild("ActionButton", true, false) is Button use)
            {
                use.EmitSignal(BaseButton.SignalName.Pressed);
                await Capture($"{outDir}/C02_use_confirm{small}.png");
            }

            viewer.CloseOverlay();

            // B03 / B04：货架抽屉（出售 / 刷新入口）与刷新确认。
            var shopCards = new List<CardViewerCard>();
            foreach (var id in CardArtCatalog.ItemArtPaths.Keys)
            {
                if (shopCards.Count < 3 && CardArtCatalog.TryGetItemTexture(id, out var texture, out _) && texture != null)
                {
                    shopCards.Add(new CardViewerCard($"S{id}", texture, ShelfSlotId: shopCards.Count + 1, ItemId: id));
                }
            }

            viewer.OpenLoading(CardViewerKind.ShopShelf);
            viewer.ShowCards(CardViewerKind.ShopShelf, shopCards, new CardActionAvailability(true, ""));
            viewer.UpdateShopActions(new CardActionAvailability(true, ""), new CardActionAvailability(true, ""));
            await Capture($"{outDir}/B03_shop_drawer{small}.png");
            if (viewer.FindChild("RefreshButton", true, false) is Button refresh)
            {
                refresh.EmitSignal(BaseButton.SignalName.Pressed);
                await Capture($"{outDir}/B04_shop_refresh_confirm{small}.png");
            }

            viewer.CloseOverlay();

            // B05：出售列表。
            var sellCards = new List<CardViewerCard>();
            foreach (var (playerItemId, itemId, name) in new[] { (11L, 1017L, "药剂"), (12L, 1005L, "花束") })
            {
                if (CardArtCatalog.TryGetItemTexture(itemId, out var texture, out _) && texture != null)
                {
                    sellCards.Add(new CardViewerCard($"SELL{playerItemId}", texture, ItemId: itemId, PlayerItemId: playerItemId, ItemName: name, ItemType: "CONSUMABLE", Location: "HAND", SellPrice: 2,
                        ActionAvailability: new CardActionAvailability(true, "")));
                }
            }

            viewer.OpenLoading(CardViewerKind.ShopSell);
            viewer.ShowCards(CardViewerKind.ShopSell, sellCards);
            await Capture($"{outDir}/B05_shop_sell{small}.png");
            viewer.CloseOverlay();

            // D03 / D04：任务结算摘要 + ITEM 奖励领取。
            var journal2 = GD.Load<PackedScene>("res://scenes/game/ui/QuestJournalOverlay.tscn").Instantiate<QuestJournalOverlay>();
            Root.AddChild(journal2);
            await Frames(2);
            var settlement = JsonSerializer.Deserialize<QuestSettlementDto>("""
            {"playerQuestId":502,"questId":38,"coinsGranted":5,"prestigeGranted":2,"partnerBonusCoins":1,"coinsAfter":17,"prestigeAfter":10,"pendingItemRewardCount":1}
            """)!;
            var itemReward = JsonSerializer.Deserialize<PendingRewardDto>("""
            {"rewardId":81,"kind":"ITEM","playerId":11,"sourceType":"QUEST_SUBMIT","canResolve":true,"hasAuthority":true,"capacitySatisfied":true,"canExecute":true,"canAbandon":true,
             "candidates":[{"cardId":901,"itemId":1003,"canExecute":true,"capacitySatisfied":true,"configuration":{"id":1003,"item_name":"苹果","item_type":"CONSUMABLE"}},
                           {"cardId":902,"itemId":1013,"canExecute":true,"capacitySatisfied":true,"configuration":{"id":1013,"item_name":"魔法书","item_type":"EQUIPMENT"}}]}
            """)!;
            journal2.ShowSettlement(settlement, itemReward, 11, null);
            await Capture($"{outDir}/D03_settlement_reward{small}.png");

            // C05：领奖期间查看 / 整理背包。
            journal2.ShowInventory(new PlayerItemsResponseDto
            {
                Items =
                [
                    new PlayerItemDto { PlayerItemId = 21, ItemId = 1017, ItemName = "药剂", ItemType = "CONSUMABLE", Location = "HAND" },
                    new PlayerItemDto { PlayerItemId = 22, ItemId = 1018, ItemName = "蹄铁", ItemType = "EQUIPMENT", Location = "EQUIPPED" }
                ]
            }, true, "");
            await Capture($"{outDir}/C05_reward_inventory{small}.png");
            journal2.ForceClose();

            // G01 / G02：伙伴队伍与独立遣散确认。
            var team = GD.Load<PackedScene>("res://scenes/game/ui/PartnerOverlay.tscn").Instantiate<PartnerOverlay>();
            Root.AddChild(team);
            await Frames(2);
            var snapshot = JsonSerializer.Deserialize<PlayerPartnerListResponseDto>("""
            {"gameId":900,"playerId":11,"stateVersion":58,"dismissal":{"canExecute":true,"socialCost":2},
             "partners":[{"playerPartnerId":301,"partnerId":24,"partnerCode":"BIG_MAC","partnerName":"大麦","status":"ACTIVE","statusText":"已生效","dismissal":{"canExecute":true,"socialCost":2}},
                         {"playerPartnerId":302,"partnerId":25,"partnerCode":"PRINCESS_LUNA","partnerName":"露娜","status":"PENDING_ACTIVE","statusText":"待生效","dismissal":{"canExecute":false,"unavailableReason":"社交不足","socialCost":2}}]}
            """)!;
            team.ShowTeam(snapshot, true, false);
            await Capture($"{outDir}/G01_partner_team{small}.png");
            if (team.FindChild("Dismiss_301", true, false) is Button dismiss)
            {
                dismiss.EmitSignal(BaseButton.SignalName.Pressed);
                await Capture($"{outDir}/G02_partner_dismiss_confirm{small}.png");
            }

            team.ForceClose();

            // F01：任选属性。
            dice.OpenAttributeSelection(JsonSerializer.Deserialize<CurrentWorkResponseDto>("""
            {"cellName":"苹果园","cellNameText":"苹果园","attributeType":"ANY","difficulty":7,"socialCost":1,"rewardCoins":4}
            """)!);
            await Capture($"{outDir}/F01_work_attribute{small}.png");

            // F02 / F03：待投掷与停骰决策。
            var pending = JsonSerializer.Deserialize<PendingCheckDto>("""
            {"checkId":9001,"businessType":"WORK","attributeType":"SOCIAL","attributeValue":3,"modifier":0,"difficulty":7,
             "canConfirm":true,"canReroll":true,"nextRerollSocialCost":1}
            """)!;
            dice.OpenPending(pending, false);
            await Capture($"{outDir}/F02_work_pending{small}.png");
            dice.Dice.EmitSignal(Dice3D.SignalName.RollCompleted, 5);
            await Capture($"{outDir}/F03_work_decision{small}.png");

            // F05：打工结果。
            dice.ShowWorkResult(response);
            await Capture($"{outDir}/F05_work_result{small}.png");
            await Frames(30);
            await Capture($"{outDir}/F05_work_result_settled{small}.png");

            GD.Print("INGAME_UI_LOOKDEV_DONE");
        }
        catch (Exception exception)
        {
            GD.PushError($"INGAME_UI_LOOKDEV_FAILED {exception}");
        }

        Quit();
    }

    private static PendingRewardDto NewReward() => new()
    {
        RewardId = 73,
        PlayerId = 11,
        Kind = "PARTNER",
        SourceType = "PARTNER_RECRUIT",
        CanAbandon = true,
        Candidates =
        [
            new RewardCandidateDto
            {
                CardId = 704,
                PartnerId = 36,
                Configuration = new ItemRewardConfigurationDto { Id = 36, PartnerCode = "CHEESE_SANDWICH", PartnerName = "起司派" },
                RecruitConditions =
                [
                    new PartnerRecruitConditionDto { ConditionIndex = 1, Method = "DEFAULT_PRESTIGE", DisplayText = "3 声望", CanExecute = true, Cost = new PartnerRecruitCostDto { Prestige = 3 } },
                    new PartnerRecruitConditionDto
                    {
                        ConditionIndex = 2, Method = "ITEM_CATEGORY_SPEND", DisplayText = "消耗 1 件杂物", CanExecute = true,
                        EligibleItems = [new PartnerRecruitItemDto { PlayerItemId = 800, ItemId = 1005, ItemName = "花束", Location = "HAND" }],
                        IneligibleItems = [new PartnerRecruitItemDto { PlayerItemId = 801, ItemId = 1013, ItemName = "魔法书", Location = "EQUIPPED", UnavailableReason = "已装备" }]
                    },
                    new PartnerRecruitConditionDto { ConditionIndex = 3, Method = "DEPENDENCY", DisplayText = "故事书", CanExecute = false, UnavailableReason = "尚未开放" }
                ]
            }
        ]
    };

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
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = Root.GetViewport().GetTexture().GetImage();
        image.SavePng(path);
        GD.Print($"INGAME_UI_CAPTURE {path}");
    }

    private sealed class LookDevTransport : IApiTransport
    {
        public Task<ApiResult<T>> RequestJsonAsync<T>(HttpClient.Method method, string path, object? body, bool authorized, IReadOnlyDictionary<string, string?>? query)
        {
            if (typeof(T) == typeof(GameMapDto))
            {
                var json = FileAccess.GetFileAsString("res://tests/fixtures/main-map-20261024.json");
                var envelope = JsonSerializer.Deserialize<ApiEnvelope<GameMapDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return Task.FromResult(ApiResult<T>.Success((T)(object)envelope!.Data!, "ok", 200));
            }

            return Task.FromResult(ApiResult<T>.Failure("出图模式不连服务端", "LOOKDEV"));
        }
    }
}
