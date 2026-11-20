using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using EquestriaStar.Api;
using EquestriaStar.Game.UI;
using EquestriaStar.State;

public partial class RunPartnerSystemTests : SceneTree
{
    private readonly List<string> _failures = [];

    public override void _Initialize() => Callable.From(Begin).CallDeferred();
    private void Begin() => _ = RunAsync();

    private async Task RunAsync()
    {
        try
        {
            var visualCheck = OS.GetCmdlineUserArgs().Contains("--visual-check");
            var compact = OS.GetCmdlineUserArgs().Contains("--compact");
            if (visualCheck)
            {
                DisplayServer.WindowSetSize(compact ? new Vector2I(960, 540) : new Vector2I(1920, 1080));
                DisplayServer.WindowSetTitle(compact ? "伙伴系统视觉验收 - 960x540" : "伙伴系统视觉验收 - 1920x1080");
            }
            VerifyFormalArtMappings();
            VerifyContractParsingAndModel();
            VerifyCandidateIdentityContract();

            var overlay = GD.Load<PackedScene>("res://scenes/game/ui/PartnerOverlay.tscn").Instantiate<PartnerOverlay>();
            Root.AddChild(overlay);
            await ToSignal(this, SignalName.ProcessFrame);
            await VerifyRealGuiPathAsync(overlay);
            if (visualCheck)
            {
                GD.Print($"PARTNER_VISUAL_CHECK_READY size={DisplayServer.WindowGetSize()}");
                return;
            }
            overlay.QueueFree();
            await ToSignal(this, SignalName.ProcessFrame);
            Finish();
        }
        catch (Exception exception)
        {
            GD.PushError($"伙伴测试异常：{exception}");
            Quit(1);
        }
    }

    private void VerifyFormalArtMappings()
    {
        var expected = new Dictionary<string, string>
        {
            ["BIG_MAC"] = "BM.jpg",
            ["PRINCESS_LUNA"] = "PL.jpg",
            ["SWEETIE_BELLE"] = "SB.jpg",
            ["LYRA"] = "LH新.jpg",
            ["CHEESE_SANDWICH"] = "CS新.jpg",
            ["SCOOTALOO"] = "SL.jpg"
        };
        foreach (var pair in expected)
        {
            AssertTrue(CardArtCatalog.PartnerCodeArtPaths.TryGetValue(pair.Key, out var path), $"缺少正式伙伴编码 {pair.Key}");
            AssertTrue(path?.EndsWith(pair.Value, StringComparison.Ordinal) == true, $"{pair.Key} 必须映射到 {pair.Value}");
            AssertTrue(FileAccess.FileExists(path), $"伙伴卡图不存在：{path}");
        }
        AssertTrue(FileAccess.FileExists(CardArtCatalog.PartnerCardBackPath), "未知伙伴卡背必须存在");
    }

    private void VerifyContractParsingAndModel()
    {
        const string json = """
        {
          "stateVersion":63,
          "engineState":{"partnerRecruitmentVersion":1,"pendingReward":{
            "rewardId":73,"playerId":11,"kind":"PARTNER","sourceType":"PARTNER_RECRUIT","canAbandon":true,
            "candidates":[{"cardId":704,"partnerId":36,"configuration":{"id":36,"partner_code":"CHEESE_SANDWICH","partner_name":"起司派"},
              "recruitConditions":[
                {"conditionIndex":1,"method":"DEFAULT_PRESTIGE","displayText":"3 声望","canExecute":true,"cost":{"prestige":3,"coins":0,"social":0,"itemQuantity":0},"eligibleItems":[],"ineligibleItems":[]},
                {"conditionIndex":2,"method":"ITEM_CATEGORY_SPEND","displayText":"消耗 1 件杂物","canExecute":true,"cost":{"prestige":0,"coins":0,"social":0,"itemQuantity":1},
                 "requirements":{"lowestEquivalentPrestige":false,"item":{"itemId":null,"itemCategory":"杂物","quantity":1,"consumed":true}},
                 "eligibleItems":[{"playerItemId":800,"itemId":1005,"gameItemCardId":990,"itemName":"花束","location":"HAND"}],
                 "ineligibleItems":[{"playerItemId":801,"itemId":1013,"gameItemCardId":991,"itemName":"魔法书","location":"EQUIPPED","unavailableReasonCode":"ITEM_EQUIPPED","unavailableReason":"已装备"}]}
              ]}]}}
        }
        """;
        var view = JsonSerializer.Deserialize<PlayerViewResponseDto>(json)!;
        AssertTrue(PartnerSystemModel.SupportsWrites(view), "partnerRecruitmentVersion=1 必须启用新版写操作");
        var reward = view.EngineState!.PendingReward!;
        AssertTrue(PartnerSystemModel.IsOwnPartnerReward(reward, 11), "本人 PARTNER_RECRUIT 队首必须可识别");
        AssertTrue(PartnerSystemModel.TryFindRecruitCondition(reward, 11, 73, 704, 2, out _, out var itemCondition, out _),
            "必须按服务端 conditionIndex 找到杂物方式");
        AssertTrue(PartnerSystemModel.TryBuildRecruitPayload(itemCondition, 73, 704, 800, out var payload, out _),
            "合法 HAND 实例必须可构建招募 payload");
        AssertEqual(payload.Count, 4, "杂物招募 payload 只能包含四个契约字段");
        AssertEqual(payload["playerItemId"]!.GetValue<long>(), 800L, "必须提交所选 playerItemId");
        AssertFalse(PartnerSystemModel.TryBuildRecruitPayload(itemCondition, 73, 704, 801, out _, out _),
            "EQUIPPED 实例不得提交");
        AssertTrue(PartnerSystemModel.TryFindRecruitCondition(reward, 11, 73, 704, 1, out _, out var prestige, out _),
            "大麦式独立方式必须逐行保留");
        AssertTrue(PartnerSystemModel.TryBuildRecruitPayload(prestige, 73, 704, 0, out var prestigePayload, out _),
            "非道具方式不得要求 playerItemId");
        AssertEqual(prestigePayload.Count, 3, "非道具招募不得携带 playerItemId 或原子替换字段");
        AssertFalse(prestigePayload.ContainsKey("dismissPlayerPartnerId"), "新客户端不得发送旧原子替换字段");

        var snapshot = JsonSerializer.Deserialize<PlayerPartnerListResponseDto>("""
        {"gameId":900,"playerId":11,"stateVersion":58,"dismissal":{"canExecute":true,"socialCost":2},
         "partners":[{"playerPartnerId":301,"partnerId":24,"partnerCode":"BIG_MAC","partnerName":"大麦","status":"ACTIVE","dismissal":{"canExecute":true,"socialCost":2}},
                     {"playerPartnerId":302,"partnerId":25,"partnerCode":"PRINCESS_LUNA","partnerName":"露娜","status":"PENDING_ACTIVE","dismissal":{"canExecute":false,"unavailableReason":"社交不足","socialCost":2}}]}
        """)!;
        AssertTrue(PartnerSystemModel.TryValidateSnapshot(snapshot, 900, 11, out _), "伙伴列表必须使用响应 stateVersion 与实例资格");
        AssertTrue(PartnerSystemModel.CanDismiss(snapshot.Partners![0], out _), "ACTIVE 可按服务端资格遣散");
        AssertFalse(PartnerSystemModel.CanDismiss(snapshot.Partners[1], out var reason) || reason != "社交不足",
            "PENDING_ACTIVE 也必须只按逐实例服务端资格，不按状态推算");
    }

    private void VerifyCandidateIdentityContract()
    {
        var correct = NewReward();
        AssertTrue(PartnerSystemModel.TryFindRecruitCondition(correct, 11, 73, 704, 1, out _, out _, out _),
            "candidate.partnerId 必须与 configuration.id 一致");

        var missing = NewReward();
        missing.Candidates![0].Configuration!.Id = null;
        AssertFalse(PartnerSystemModel.TryFindRecruitCondition(missing, 11, 73, 704, 1, out _, out _, out _),
            "configuration.id 缺失必须拒绝");

        var conflict = NewReward();
        conflict.Candidates![0].Configuration!.Id = 37;
        AssertFalse(PartnerSystemModel.TryFindRecruitCondition(conflict, 11, 73, 704, 1, out _, out _, out _),
            "candidate.partnerId 与 configuration.id 冲突必须拒绝");

        const string wrongFieldJson = """
        {"rewardId":73,"playerId":11,"kind":"PARTNER","sourceType":"PARTNER_RECRUIT","candidates":[
          {"cardId":704,"partnerId":36,"configuration":{"partner_id":36,"partner_code":"CHEESE_SANDWICH","partner_name":"起司派"},
           "recruitConditions":[{"conditionIndex":1,"method":"DEFAULT_PRESTIGE","canExecute":true}]}
        ]}
        """;
        var wrongField = JsonSerializer.Deserialize<PendingRewardDto>(wrongFieldJson)!;
        AssertFalse(PartnerSystemModel.TryFindRecruitCondition(wrongField, 11, 73, 704, 1, out _, out _, out _),
            "仅有错误 configuration.partner_id 时不得绕过准确 id 校验");
    }

    private async Task VerifyRealGuiPathAsync(PartnerOverlay overlay)
    {
        var writes = 0;
        overlay.RecruitRequested += (_, _, _, _) => writes++;
        var reward = NewReward();
        overlay.ShowReward(reward, 11, true, false);
        await ToSignal(this, SignalName.ProcessFrame);

        var enabled = overlay.FindChild("Condition_704_1", true, false) as Button;
        var disabled = overlay.FindChild("Condition_704_3", true, false) as Button;
        AssertTrue(enabled != null && !enabled.Disabled, "可用逐方式按钮必须亮起");
        AssertTrue(disabled != null && disabled.Disabled, "未开放逐方式按钮必须灰色");
        await ClickAsync(disabled!);
        AssertEqual(writes, 0, "灰色按钮真实 GUI 点击不得提交");
        await ClickAsync(enabled!);
        var dialog = overlay.GetNode<Control>("Root/ConfirmLayer");
        AssertTrue(dialog.Visible, "可用方式真实 GUI 点击必须打开确认框");
        await ClickAsync(overlay.GetNode<Button>("Root/ConfirmLayer/Panel/Margin/Content/Buttons/ConfirmButton"));
        AssertEqual(writes, 1, "确认后必须恰好产生一次招募信号");

        overlay.ShowReward(reward, 11, true, false);
        await ToSignal(this, SignalName.ProcessFrame);
        var itemCondition = overlay.FindChild("Condition_704_2", true, false) as Button;
        await ClickAsync(itemCondition!);
        AssertEqual(overlay.Mode, PartnerOverlayMode.ItemSelection, "即使只有一件手牌也必须打开选物页");
        var equipped = overlay.FindChild("Item_801", true, false) as Button;
        var hand = overlay.FindChild("Item_800", true, false) as Button;
        AssertTrue(equipped?.Disabled == true, "已装备杂物必须只读");
        AssertTrue(hand?.Disabled == false, "eligibleItems 中的 HAND 必须可选");

        await ClickAsync(hand!);
        var itemConfirm = overlay.GetNode<Control>("Root/ConfirmLayer");
        AssertTrue(itemConfirm.Visible, "选择手牌后必须打开确认框");
        overlay.SyncRewardAuthority(NewReward(), 11, true, false);
        overlay.SyncRewardAuthority(NewReward(), 11, true, false);
        AssertEqual(overlay.Mode, PartnerOverlayMode.ItemSelection, "同一候选跨多次轮询必须保留选物页");
        AssertEqual(overlay.SelectedPlayerItemId, 800L, "同一候选轮询不得清空已选实例");
        AssertTrue(overlay.IsConfirmationOpen, "同一候选轮询不得打断确认框");

        await ClickAsync(overlay.GetNode<Button>("Root/ConfirmLayer/Panel/Margin/Content/Buttons/CancelButton"));
        AssertFalse(overlay.IsConfirmationOpen, "取消后不得遗留确认遮罩");
        AssertEqual(writes, 1, "取消杂物确认必须零写入");

        await ClickAsync(overlay.FindChild("Item_800", true, false) as Button ?? throw new InvalidOperationException("缺少手牌按钮"));
        var changed = NewReward();
        changed.Candidates![0].RecruitConditions![1].EligibleItems = [];
        changed.Candidates![0].RecruitConditions![1].CanExecute = false;
        overlay.SyncRewardAuthority(changed, 11, true, false);
        AssertFalse(overlay.IsConfirmationOpen, "所选实例或资格失效时必须关闭旧确认框");
        AssertEqual(overlay.Mode, PartnerOverlayMode.Reward, "资格失效后必须返回最新候选页");
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

    private async Task ClickAsync(Control control)
    {
        await ToSignal(this, SignalName.ProcessFrame);
        var position = control.GetGlobalRect().GetCenter();
        var viewport = control.GetViewport();
        viewport.PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true, Position = position, GlobalPosition = position }, true);
        await ToSignal(this, SignalName.ProcessFrame);
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = position, GlobalPosition = position }, true);
        await ToSignal(this, SignalName.ProcessFrame);
    }

    private void Finish()
    {
        if (_failures.Count == 0) { GD.Print("PARTNER_SYSTEM_TESTS_PASSED"); Quit(0); return; }
        foreach (var failure in _failures) GD.PushError(failure);
        Quit(1);
    }
    private void AssertTrue(bool value, string message) { if (!value) _failures.Add(message); }
    private void AssertFalse(bool value, string message) => AssertTrue(!value, message);
    private void AssertEqual<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected)) _failures.Add($"{message}；期望={expected}，实际={actual}");
    }
}
