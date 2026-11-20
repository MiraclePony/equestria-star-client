using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using EquestriaStar.Api;

namespace EquestriaStar.Game.UI;

public enum PartnerOverlayMode
{
    Closed,
    LoadingTeam,
    Team,
    LoadingReward,
    Reward,
    ItemSelection,
    Error
}

/// <summary>伙伴队伍、候选方式和杂物选物的纸艺覆盖层；不读取 Session，也不直接请求接口。</summary>
public partial class PartnerOverlay : CanvasLayer
{
    [Signal] public delegate void ClosedEventHandler();
    [Signal] public delegate void RetryRequestedEventHandler();
    [Signal] public delegate void DismissRequestedEventHandler(long playerPartnerId);
    [Signal] public delegate void RecruitRequestedEventHandler(long rewardId, long cardId, int conditionIndex, long playerItemId);
    [Signal] public delegate void AbandonRequestedEventHandler(long rewardId);

    private Control _root = null!;
    private Label _title = null!;
    private Label _status = null!;
    private VBoxContainer _content = null!;
    private Button _closeButton = null!;
    private Button _backButton = null!;
    private Button _retryButton = null!;
    private Button _abandonButton = null!;
    private Control _confirmLayer = null!;
    private Label _confirmMessage = null!;
    private Button _confirmButton = null!;
    private Button _confirmCancelButton = null!;
    private PendingRewardDto? _reward;
    private RewardCandidateDto? _itemCandidate;
    private PartnerRecruitConditionDto? _itemCondition;
    private long _selectedPlayerItemId;
    private Action? _confirmedAction;

    public PartnerOverlayMode Mode { get; private set; } = PartnerOverlayMode.Closed;
    public bool IsOpen => Mode != PartnerOverlayMode.Closed;
    public bool IsConfirmationOpen => GodotObject.IsInstanceValid(_confirmLayer) && _confirmLayer.Visible;
    public long SelectedPlayerItemId => _selectedPlayerItemId;
    public long CurrentRewardId => _reward?.RewardId ?? 0;
    public long CurrentCandidateCardId => _itemCandidate?.CardId ?? 0;
    public int CurrentConditionIndex => _itemCondition?.ConditionIndex ?? 0;

    public void SetStatusMessage(string message) => _status.Text = message;

    public override void _Ready()
    {
        _root = GetNode<Control>("Root");
        _title = GetNode<Label>("Root/Center/Panel/Margin/Layout/Header/Title");
        _status = GetNode<Label>("Root/Center/Panel/Margin/Layout/Status");
        _content = GetNode<VBoxContainer>("Root/Center/Panel/Margin/Layout/Scroll/Content");
        _closeButton = GetNode<Button>("Root/Center/Panel/Margin/Layout/Header/CloseButton");
        _backButton = GetNode<Button>("Root/Center/Panel/Margin/Layout/Footer/BackButton");
        _retryButton = GetNode<Button>("Root/Center/Panel/Margin/Layout/Footer/RetryButton");
        _abandonButton = GetNode<Button>("Root/Center/Panel/Margin/Layout/Footer/AbandonButton");
        _confirmLayer = GetNode<Control>("Root/ConfirmLayer");
        _confirmMessage = GetNode<Label>("Root/ConfirmLayer/Panel/Margin/Content/Message");
        _confirmButton = GetNode<Button>("Root/ConfirmLayer/Panel/Margin/Content/Buttons/ConfirmButton");
        _confirmCancelButton = GetNode<Button>("Root/ConfirmLayer/Panel/Margin/Content/Buttons/CancelButton");
        _closeButton.Pressed += CloseOverlay;
        _backButton.Pressed += ReturnToReward;
        _retryButton.Pressed += () => EmitSignal(SignalName.RetryRequested);
        _abandonButton.Pressed += ConfirmAbandon;
        _confirmButton.Pressed += OnConfirmed;
        _confirmCancelButton.Pressed += OnConfirmCancelled;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_closeButton)) _closeButton.Pressed -= CloseOverlay;
        if (GodotObject.IsInstanceValid(_backButton)) _backButton.Pressed -= ReturnToReward;
        if (GodotObject.IsInstanceValid(_abandonButton)) _abandonButton.Pressed -= ConfirmAbandon;
        if (GodotObject.IsInstanceValid(_confirmButton)) _confirmButton.Pressed -= OnConfirmed;
        if (GodotObject.IsInstanceValid(_confirmCancelButton)) _confirmCancelButton.Pressed -= OnConfirmCancelled;
    }

    public void OpenLoading(bool team)
    {
        if (team)
        {
            _reward = null;
            _itemCandidate = null;
            _itemCondition = null;
            _selectedPlayerItemId = 0;
            CloseConfirm();
        }
        Mode = team ? PartnerOverlayMode.LoadingTeam : PartnerOverlayMode.LoadingReward;
        _root.Visible = true;
        _title.Text = team ? "我的伙伴" : "伙伴候选";
        _status.Text = "正在同步服务端伙伴状态……";
        ClearContent();
        ConfigureFooter(false, false, false);
    }

    public void ShowError(string message)
    {
        Mode = PartnerOverlayMode.Error;
        _root.Visible = true;
        _status.Text = message;
        ClearContent();
        ConfigureFooter(false, true, false);
    }

    public void ShowTeam(PlayerPartnerListResponseDto snapshot, bool writesSupported, bool submitting, string? message = null)
    {
        _reward = null;
        _itemCandidate = null;
        _itemCondition = null;
        _selectedPlayerItemId = 0;
        CloseConfirm();
        Mode = PartnerOverlayMode.Team;
        _root.Visible = true;
        _title.Text = "我的伙伴";
        _status.Text = message ?? (writesSupported ? "伙伴状态与遣散资格均来自服务端。" : "当前对局未启用新版伙伴写操作，仅可查看。" );
        ClearContent();
        ConfigureFooter(false, false, false);

        var partners = snapshot.Partners ?? [];
        if (partners.Count == 0)
        {
            AddText("当前队伍没有伙伴。", 20);
            return;
        }

        foreach (var partner in partners)
        {
            var row = MakeCardRow();
            row.AddChild(MakePartnerArt(partner.PartnerCode, partner.PartnerName));
            var details = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            details.AddChild(MakeLabel(partner.PartnerName ?? partner.PartnerCode ?? "未知伙伴", 22));
            details.AddChild(MakeLabel($"状态：{partner.StatusText ?? partner.Status ?? "未知"}\n力量 {partner.AttrStrength ?? "-"}　速度 {partner.AttrSpeed ?? "-"}　社交 {partner.AttrSocial ?? "-"}\n魅力 {partner.AttrCharm ?? "-"}　魔法 {partner.AttrMagic ?? "-"}　心智 {partner.AttrMind ?? "-"}", 16));
            if (!string.IsNullOrWhiteSpace(partner.Description)) details.AddChild(MakeLabel(partner.Description!, 15));
            row.AddChild(details);

            var dismissal = partner.Dismissal;
            var button = new Button { Name = $"Dismiss_{partner.PlayerPartnerId}", Text = submitting ? "处理中……" : "遣散", CustomMinimumSize = new Vector2(110, 44) };
            button.Disabled = submitting || !writesSupported || dismissal?.CanExecute != true || partner.PlayerPartnerId is not > 0;
            button.TooltipText = button.Disabled
                ? FirstNonEmpty(dismissal?.UnavailableReason, writesSupported ? "服务端未允许遣散该伙伴。" : "当前对局未启用新版伙伴写操作。")
                : $"遣散将消耗 {dismissal!.SocialCost ?? 0} 社交点。";
            var id = partner.PlayerPartnerId ?? 0;
            var name = partner.PartnerName ?? partner.PartnerCode ?? "该伙伴";
            var cost = dismissal?.SocialCost ?? snapshot.Dismissal?.SocialCost ?? 0;
            button.Pressed += () => ConfirmDismiss(id, name, cost);
            row.AddChild(button);
            _content.AddChild(row);
        }
    }

    public void ShowReward(PendingRewardDto reward, long currentPlayerId, bool writesSupported, bool submitting, string? message = null)
    {
        _reward = reward;
        _itemCandidate = null;
        _itemCondition = null;
        Mode = PartnerOverlayMode.Reward;
        _root.Visible = true;
        _title.Text = "伙伴招募候选";
        _status.Text = message ?? "请选择伙伴及服务端允许的招募方式。";
        ClearContent();
        var own = reward.PlayerId == currentPlayerId;
        ConfigureFooter(false, false, own && reward.CanAbandon == true);
        _abandonButton.Disabled = submitting || !writesSupported || !own || reward.CanAbandon != true;
        _abandonButton.TooltipText = _abandonButton.Disabled
            ? FirstNonEmpty(reward.AbandonUnavailableReason, "当前不能放弃本批候选。") : "放弃整批候选。";

        if (!own)
        {
            AddText("当前 FIFO 队首属于其他玩家，正在等待其处理。", 19);
            return;
        }
        if (!string.Equals(reward.Kind, "PARTNER", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(reward.SourceType, "PARTNER_RECRUIT", StringComparison.OrdinalIgnoreCase))
        {
            AddText("当前奖励队首不是伙伴招募候选。", 19);
            return;
        }

        var candidates = reward.Candidates ?? [];
        if (candidates.Count == 0)
        {
            var draw = reward.DrawResult;
            AddText(draw?.Message ?? "本次没有抽到可展示的伙伴候选。", 19);
            return;
        }

        foreach (var candidate in candidates)
        {
            var config = candidate.Configuration;
            var row = MakeCardRow();
            row.AddChild(MakePartnerArt(config?.PartnerCode, config?.PartnerName));
            var conditions = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            conditions.AddChild(MakeLabel(config?.PartnerName ?? config?.PartnerCode ?? "未知伙伴", 22));
            foreach (var condition in candidate.RecruitConditions ?? [])
            {
                var button = new Button
                {
                    Name = $"Condition_{candidate.CardId}_{condition.ConditionIndex}",
                    Text = condition.DisplayText ?? condition.Method ?? "未命名方式",
                    CustomMinimumSize = new Vector2(360, 42),
                    Disabled = submitting || !writesSupported || condition.CanExecute != true || condition.ConditionIndex is not > 0
                };
                button.TooltipText = button.Disabled
                    ? FirstNonEmpty(condition.UnavailableReason, condition.ConditionIndex is null ? "该方式尚未开放。" : "当前不能使用该方式。")
                    : BuildCostText(condition.Cost);
                button.Pressed += () => OnConditionPressed(reward, candidate, condition);
                conditions.AddChild(button);
            }
            row.AddChild(conditions);
            _content.AddChild(row);
        }
    }

    /// <summary>用新鲜权威队首刷新候选；同一有效杂物方式保持选物页和确认框。</summary>
    public bool SyncRewardAuthority(
        PendingRewardDto reward,
        long currentPlayerId,
        bool writesSupported,
        bool submitting,
        string? message = null)
    {
        if (Mode == PartnerOverlayMode.ItemSelection
            && _reward?.RewardId is > 0
            && _itemCandidate?.CardId is > 0
            && _itemCondition?.ConditionIndex is > 0
            && reward.RewardId == _reward.RewardId)
        {
            var candidate = reward.Candidates?.FirstOrDefault(value => value.CardId == _itemCandidate.CardId);
            var condition = candidate?.RecruitConditions?.FirstOrDefault(value => value.ConditionIndex == _itemCondition.ConditionIndex);
            var valid = reward.PlayerId == currentPlayerId
                && string.Equals(reward.Kind, "PARTNER", StringComparison.OrdinalIgnoreCase)
                && string.Equals(reward.SourceType, "PARTNER_RECRUIT", StringComparison.OrdinalIgnoreCase)
                && condition?.CanExecute == true
                && string.Equals(condition.Method, "ITEM_CATEGORY_SPEND", StringComparison.OrdinalIgnoreCase);
            if (valid)
            {
                var selectedStillValid = _selectedPlayerItemId <= 0
                    || condition!.EligibleItems?.Any(item => item.PlayerItemId == _selectedPlayerItemId
                        && string.Equals(item.Location, "HAND", StringComparison.OrdinalIgnoreCase)) == true;
                if (selectedStillValid)
                {
                    ShowItemSelection(reward, candidate!, condition!, preserveSelection: true);
                    if (!string.IsNullOrWhiteSpace(message)) _status.Text = message;
                    return true;
                }
            }

            CloseConfirm();
            ShowReward(reward, currentPlayerId, writesSupported, submitting,
                "原选物身份或资格已经变化，请按最新候选重新选择；客户端不会自动换扣。" );
            return false;
        }

        ShowReward(reward, currentPlayerId, writesSupported, submitting, message);
        return true;
    }

    public void SetSubmitting(bool submitting, string message)
    {
        _status.Text = message;
        foreach (var child in EnumerateButtons(_content)) child.Disabled = submitting || child.Disabled;
        _closeButton.Disabled = submitting;
        _backButton.Disabled = submitting;
        _abandonButton.Disabled = submitting || _abandonButton.Disabled;
    }

    public void CloseOverlay()
    {
        if (!_root.Visible || _closeButton.Disabled) return;
        ForceClose(true);
    }

    public void ForceClose(bool emitClosed = false)
    {
        if (!GodotObject.IsInstanceValid(_root)) return;
        _root.Visible = false;
        Mode = PartnerOverlayMode.Closed;
        _reward = null;
        _itemCandidate = null;
        _itemCondition = null;
        _selectedPlayerItemId = 0;
        _confirmedAction = null;
        _confirmLayer.Visible = false;
        ClearContent();
        if (emitClosed) EmitSignal(SignalName.Closed);
    }

    private void OnConditionPressed(PendingRewardDto reward, RewardCandidateDto candidate, PartnerRecruitConditionDto condition)
    {
        if (reward.RewardId is not > 0 || candidate.CardId is not > 0 || condition.ConditionIndex is not > 0 || condition.CanExecute != true) return;
        if (string.Equals(condition.Method, "ITEM_CATEGORY_SPEND", StringComparison.OrdinalIgnoreCase))
        {
            ShowItemSelection(reward, candidate, condition);
            return;
        }
        var rewardId = reward.RewardId.Value;
        var cardId = candidate.CardId.Value;
        var conditionIndex = condition.ConditionIndex.Value;
        var text = $"确定使用“{condition.DisplayText ?? condition.Method}”招募「{candidate.Configuration?.PartnerName ?? "该伙伴"}」吗？\n{BuildCostText(condition.Cost)}";
        Ask(text, () => EmitSignal(SignalName.RecruitRequested, rewardId, cardId, conditionIndex, 0L));
    }

    private void ShowItemSelection(
        PendingRewardDto reward,
        RewardCandidateDto candidate,
        PartnerRecruitConditionDto condition,
        bool preserveSelection = false)
    {
        var previousSelection = preserveSelection ? _selectedPlayerItemId : 0;
        var confirmationWasOpen = preserveSelection && _confirmLayer.Visible;
        _reward = reward;
        _itemCandidate = candidate;
        _itemCondition = condition;
        _selectedPlayerItemId = previousSelection;
        Mode = PartnerOverlayMode.ItemSelection;
        _title.Text = "选择招募支付道具";
        _status.Text = "请选择一件服务端列出的手牌杂物；已装备实例仅供查看。";
        ClearContent();
        ConfigureFooter(true, false, false);

        foreach (var item in condition.EligibleItems ?? []) AddItemChoice(item, true);
        foreach (var item in condition.IneligibleItems ?? []) AddItemChoice(item, false);
        if ((condition.EligibleItems?.Count ?? 0) == 0 && (condition.IneligibleItems?.Count ?? 0) == 0)
        {
            AddText("服务端没有返回可选或只读道具实例。", 18);
        }
        _confirmLayer.Visible = confirmationWasOpen;
    }

    private void AddItemChoice(PartnerRecruitItemDto item, bool eligible)
    {
        var button = new Button
        {
            Name = $"Item_{item.PlayerItemId}",
            Text = eligible ? $"{item.ItemName ?? $"道具 {item.ItemId}"}（手牌）" : $"{item.ItemName ?? $"道具 {item.ItemId}"}（已装备，不可选）",
            CustomMinimumSize = new Vector2(0, 54),
            Disabled = !eligible || item.PlayerItemId is not > 0,
            TooltipText = eligible ? "选择后仍需确认。" : FirstNonEmpty(item.UnavailableReason, "已装备，不能作为招募支付。")
        };
        if (eligible)
        {
            var id = item.PlayerItemId!.Value;
            button.Pressed += () => ConfirmItem(id, item.ItemName ?? "该道具");
        }
        _content.AddChild(button);
    }

    private void ConfirmItem(long playerItemId, string itemName)
    {
        if (_reward?.RewardId is not > 0 || _itemCandidate?.CardId is not > 0 || _itemCondition?.ConditionIndex is not > 0) return;
        var rewardId = _reward.RewardId.Value;
        var cardId = _itemCandidate.CardId.Value;
        var conditionIndex = _itemCondition.ConditionIndex.Value;
        var partnerName = _itemCandidate.Configuration?.PartnerName ?? "该伙伴";
        _selectedPlayerItemId = playerItemId;
        Ask($"确定消耗手牌「{itemName}」招募「{partnerName}」吗？", () =>
            EmitSignal(SignalName.RecruitRequested, rewardId, cardId, conditionIndex, playerItemId));
    }

    private void ConfirmDismiss(long playerPartnerId, string partnerName, int socialCost)
    {
        if (playerPartnerId <= 0) return;
        Ask($"确定遣散「{partnerName}」吗？\n服务端当前费用：{socialCost} 社交点。", () => EmitSignal(SignalName.DismissRequested, playerPartnerId));
    }

    private void ConfirmAbandon()
    {
        if (_reward?.RewardId is not > 0 || _reward.CanAbandon != true) return;
        var rewardId = _reward.RewardId.Value;
        Ask("确定放弃整批伙伴候选吗？已成功遣散的伙伴不会恢复。", () => EmitSignal(SignalName.AbandonRequested, rewardId));
    }

    private void ReturnToReward()
    {
        if (_reward != null) ShowReward(_reward, _reward.PlayerId ?? 0, true, false);
    }

    private void Ask(string text, Action confirmed)
    {
        _confirmedAction = confirmed;
        _confirmMessage.Text = text;
        _confirmLayer.Visible = true;
    }

    private void OnConfirmed()
    {
        var action = _confirmedAction;
        _confirmedAction = null;
        _confirmLayer.Visible = false;
        action?.Invoke();
    }

    private void OnConfirmCancelled()
    {
        CloseConfirm();
    }

    private void CloseConfirm()
    {
        _confirmedAction = null;
        _confirmLayer.Visible = false;
    }

    private void ConfigureFooter(bool back, bool retry, bool abandon)
    {
        _backButton.Visible = back;
        _retryButton.Visible = retry;
        _abandonButton.Visible = abandon;
        _closeButton.Disabled = false;
    }

    private void ClearContent()
    {
        foreach (var child in _content.GetChildren())
        {
            // 轮询刷新时先从容器立即移除，避免同名旧按钮在等待释放期间继续参与命中。
            _content.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static HBoxContainer MakeCardRow()
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 190),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 18);
        return row;
    }

    private static TextureRect MakePartnerArt(string? code, string? name)
    {
        CardArtCatalog.TryGetPartnerTexture(code, name, out var texture, out _);
        return new TextureRect
        {
            Texture = texture,
            CustomMinimumSize = new Vector2(132, 184),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    private void AddText(string text, int size) => _content.AddChild(MakeLabel(text, size));

    private static Label MakeLabel(string text, int size)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    private static IEnumerable<Button> EnumerateButtons(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Button button) yield return button;
            foreach (var nested in EnumerateButtons(child)) yield return nested;
        }
    }

    private static string BuildCostText(PartnerRecruitCostDto? cost)
    {
        if (cost == null) return "费用由服务端结算。";
        var parts = new List<string>();
        if (cost.Prestige is > 0) parts.Add($"{cost.Prestige} 声望");
        if (cost.Coins is > 0) parts.Add($"{cost.Coins} 金币");
        if (cost.Social is > 0) parts.Add($"{cost.Social} 社交");
        if (cost.ItemQuantity is > 0) parts.Add($"{cost.ItemQuantity} 件道具");
        return parts.Count == 0 ? "无资源费用" : $"实际费用：{string.Join("、", parts)}";
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
}
