using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using EquestriaStar.Api;
using EquestriaStar.Game.UI;
using EquestriaStar.Game.Map;
using EquestriaStar.State;

namespace EquestriaStar.UI;

/// <summary>CORE_9 伙伴队伍、招募候选和独立遣散流程。</summary>
public partial class GameScreen
{
    private enum PartnerPendingRecoveryState
    {
        Recovered,
        None,
        Conflict,
        Failed
    }

    private PartnerOverlay _partnerOverlay = null!;
    private bool _partnerSubmitting;
    private bool _partnerRecoveryInProgress;
    private int _partnerRequestSerial;
    private GameActionRequestDto? _unresolvedPartnerRequest;
    private bool _returnToPartnerRewardAfterResult;

    public PartnerOverlay PartnerOverlay => _partnerOverlay;
    public PlayerPartnerListResponseDto? PartnerSnapshot => _partnerSnapshot;
    public bool IsPartnerSubmitting => _partnerSubmitting;
    public bool IsPartnerRecoveryInProgress => _partnerRecoveryInProgress;
    public GameActionRequestDto? UnresolvedPartnerRequest => _unresolvedPartnerRequest;
    public bool SupportsPartnerRecruitment => PartnerSystemModel.SupportsWrites(_latestPlayerView);

    private void InitializePartnerSystem()
    {
        _partnerOverlay = GetNode<PartnerOverlay>("PartnerOverlay");
        _hud.PartnersRequested += OnPartnersRequested;
        _hud.PartnerRecruitRequested += OnPartnerRecruitRequested;
        _partnerOverlay.RetryRequested += OnPartnerRetryRequested;
        _partnerOverlay.DismissRequested += OnPartnerDismissRequested;
        _partnerOverlay.RecruitRequested += OnPartnerRecruitConditionRequested;
        _partnerOverlay.AbandonRequested += OnPartnerAbandonRequested;
    }

    private void DisposePartnerSystem()
    {
        if (GodotObject.IsInstanceValid(_hud))
        {
            _hud.PartnersRequested -= OnPartnersRequested;
            _hud.PartnerRecruitRequested -= OnPartnerRecruitRequested;
        }
        if (!GodotObject.IsInstanceValid(_partnerOverlay)) return;
        _partnerOverlay.RetryRequested -= OnPartnerRetryRequested;
        _partnerOverlay.DismissRequested -= OnPartnerDismissRequested;
        _partnerOverlay.RecruitRequested -= OnPartnerRecruitConditionRequested;
        _partnerOverlay.AbandonRequested -= OnPartnerAbandonRequested;
    }

    private void ResetPartnerSystemForStart()
    {
        _partnerSubmitting = false;
        _partnerRecoveryInProgress = false;
        _partnerRequestSerial++;
        _unresolvedPartnerRequest = null;
        _returnToPartnerRewardAfterResult = false;
        if (GodotObject.IsInstanceValid(_partnerOverlay)) _partnerOverlay.ForceClose();
    }

    private void StopPartnerSystem()
    {
        _partnerSubmitting = false;
        _partnerRecoveryInProgress = false;
        _partnerRequestSerial++;
        _unresolvedPartnerRequest = null;
        _returnToPartnerRewardAfterResult = false;
        if (GodotObject.IsInstanceValid(_partnerOverlay)) _partnerOverlay.ForceClose();
    }

    public Task OpenPartnersAsync()
    {
        if (!_active || !TryGetCurrentPlayer(out var player)) return Task.CompletedTask;
        if (TryShowUnresolvedPartnerOperation()) return Task.CompletedTask;
        _partnerOverlay.OpenLoading(true);
        return RefreshPartnerSnapshotForOverlayAsync(player.PlayerId, _generation);
    }

    public Task BeginPartnerRecruitmentAsync()
    {
        if (TryShowUnresolvedPartnerOperation()) return Task.CompletedTask;
        if (!CanStartPartnerWrite() || !TryGetCurrentPlayer(out var player)) return Task.CompletedTask;
        if (!SupportsPartnerRecruitment)
        {
            ShowGameStatusError("当前对局未通过新鲜 player-view 启用伙伴招募。");
            return Task.CompletedTask;
        }
        var action = CurrentPartnerAction("DRAW_PARTNER_OFFER");
        if (action?.Available != true)
        {
            ShowGameStatusError(FirstNonEmpty(action?.ReasonText, action?.Reason, "当前不能招募伙伴。"));
            return Task.CompletedTask;
        }
        var request = NewPartnerRequest(player.PlayerId, "DRAW_PARTNER_OFFER", new JsonObject());
        return SubmitPartnerActionAsync(request, PartnerWriteKind.DrawOffer);
    }

    public Task DismissPartnerAsync(long playerPartnerId)
    {
        if (TryShowUnresolvedPartnerOperation()) return Task.CompletedTask;
        if (!CanStartPartnerWrite() || !TryGetCurrentPlayer(out var player) || _partnerSnapshot == null)
            return Task.CompletedTask;
        var partner = _partnerSnapshot.Partners?.FirstOrDefault(value => value.PlayerPartnerId == playerPartnerId);
        if (!PartnerSystemModel.CanDismiss(partner, out var reason))
        {
            _partnerOverlay.ShowTeam(_partnerSnapshot, SupportsPartnerRecruitment, false, reason);
            return Task.CompletedTask;
        }
        var request = NewPartnerRequest(player.PlayerId, "DISMISS_PARTNER", new JsonObject
        {
            ["playerPartnerId"] = playerPartnerId
        });
        return SubmitPartnerActionAsync(request, PartnerWriteKind.Dismiss);
    }

    public Task RecruitPartnerAsync(long rewardId, long cardId, int conditionIndex, long playerItemId)
    {
        if (TryShowUnresolvedPartnerOperation()) return Task.CompletedTask;
        if (!CanStartPartnerWrite() || !TryGetCurrentPlayer(out var player)) return Task.CompletedTask;
        var reward = _latestPlayerView?.EngineState?.PendingReward;
        if (!PartnerSystemModel.TryFindRecruitCondition(
                reward, player.PlayerId, rewardId, cardId, conditionIndex,
                out _, out var condition, out var error)
            || !PartnerSystemModel.TryBuildRecruitPayload(
                condition, rewardId, cardId, playerItemId, out var payload, out error))
        {
            _partnerOverlay.ShowError(error);
            return Task.CompletedTask;
        }
        var request = NewPartnerRequest(player.PlayerId, "RECRUIT_PARTNER_FROM_OFFER", payload);
        return SubmitPartnerActionAsync(request, PartnerWriteKind.Recruit);
    }

    public Task AbandonPartnerRewardAsync(long rewardId)
    {
        if (TryShowUnresolvedPartnerOperation()) return Task.CompletedTask;
        if (!CanStartPartnerWrite() || !TryGetCurrentPlayer(out var player)) return Task.CompletedTask;
        var reward = _latestPlayerView?.EngineState?.PendingReward;
        if (!PartnerSystemModel.IsOwnPartnerReward(reward, player.PlayerId)
            || reward!.RewardId != rewardId || reward.CanAbandon != true)
        {
            _partnerOverlay.ShowError(FirstNonEmpty(reward?.AbandonUnavailableReason, null, "当前不能放弃该伙伴奖励队首。"));
            return Task.CompletedTask;
        }
        var request = NewPartnerRequest(player.PlayerId, "RESOLVE_TURN_START_EFFECT", new JsonObject
        {
            ["rewardId"] = rewardId,
            ["abandon"] = true
        });
        return SubmitPartnerActionAsync(request, PartnerWriteKind.Abandon);
    }

    private async Task SubmitPartnerActionAsync(GameActionRequestDto request, PartnerWriteKind kind, bool replay = false)
    {
        if (_partnerSubmitting || !_active) return;
        _partnerSubmitting = true;
        _hud.SetActionControlsLocked(true);
        _partnerOverlay.SetSubmitting(true, replay ? "正在恢复原伙伴操作……" : "正在提交伙伴操作……");
        var generation = _generation;
        ApiResult<GameActionResponseDto> result;
        try
        {
            result = await ApiClient.SubmitGameActionAsync(_gameId, request);
        }
        catch (Exception exception)
        {
            result = ApiResult<GameActionResponseDto>.Failure($"伙伴操作请求异常：{exception.Message}", "PARTNER_ACTION_EXCEPTION");
        }

        try
        {
            if (!IsResponseCurrent(generation)) return;
            if (!result.Ok || result.Data == null)
            {
                if (IsRecoverableUnknownResult(result, "PARTNER_ACTION_EXCEPTION"))
                {
                    _unresolvedPartnerRequest = request;
                    if (kind == PartnerWriteKind.DrawOffer
                        && await QueryPartnerPendingCheckAsync(generation) == PartnerPendingRecoveryState.Recovered) return;
                    _partnerOverlay.ShowError($"伙伴操作结果未知，已保留原请求身份。{FirstNonEmpty(result.Message, null, "可点击重试恢复原操作。")}");
                }
                else
                {
                    _unresolvedPartnerRequest = null;
                    await RefreshPartnerAuthorityAsync(generation);
                    _partnerOverlay.ShowError(FirstNonEmpty(result.Message, null, "伙伴操作未完成。"));
                }
                return;
            }

            _stateVersionFloor = Math.Max(_stateVersionFloor, result.Data.NewStateVersion);
            if (result.Data.PlayerViewState != null
                && ValidatePlayerView(result.Data.PlayerViewState, _gameId, _currentUserId, out _)
                && !IsOlderPlayerView(result.Data.PlayerViewState))
            {
                ApplyPlayerView(result.Data.PlayerViewState, false);
            }

            if (kind == PartnerWriteKind.DrawOffer)
            {
                if (!string.Equals(result.Data.ResultType, "CHECK_ROLL_PENDING", StringComparison.OrdinalIgnoreCase))
                {
                    _partnerOverlay.ShowError("招募准备成功回执不是 CHECK_ROLL_PENDING。" );
                    return;
                }
                if (replay)
                {
                    // 幂等重放携带的是首次历史快照，必须重新查询当前待判定，不能直接打开旧骰子。
                    var recovery = await QueryPartnerPendingCheckAsync(generation);
                    if (recovery == PartnerPendingRecoveryState.Recovered) return;
                    if (recovery == PartnerPendingRecoveryState.None)
                    {
                        _unresolvedPartnerRequest = null;
                        await RefreshPartnerAuthorityAsync(generation);
                        RoutePartnerRewardOrTeam("原伙伴招募判定已不再待处理，未恢复过期骰子。" );
                    }
                    return;
                }
                if (!TryValidatePendingPartnerRecruit(result.Data.PendingCheck, _gameId, request.PlayerId, out var pending, out var pendingError))
                {
                    _partnerOverlay.ShowError(pendingError);
                    return;
                }
                _unresolvedPartnerRequest = null;
                OpenPendingWork(pending, false);
                return;
            }

            _unresolvedPartnerRequest = null;

            if (kind == PartnerWriteKind.Recruit && result.Data.PartnerRecruit == null)
            {
                _partnerOverlay.ShowError("招募已返回成功，但缺少 partnerRecruit 实际支付结果。");
                return;
            }
            if (kind == PartnerWriteKind.Dismiss && result.Data.PartnerDismissal == null)
            {
                _partnerOverlay.ShowError("遣散已返回成功，但缺少 partnerDismissal 结果。");
                return;
            }

            var success = kind switch
            {
                PartnerWriteKind.Recruit => FormatRecruitResult(result.Data.PartnerRecruit!),
                PartnerWriteKind.Dismiss => $"伙伴已遣散；实际消耗 {result.Data.PartnerDismissal!.SocialCost} 社交点。",
                PartnerWriteKind.Abandon => "伙伴候选已按服务端结果放弃。",
                _ => "伙伴操作已完成。"
            };
            await RefreshPartnerAuthorityAsync(generation);
            RoutePartnerRewardOrTeam(success);
        }
        finally
        {
            if (IsResponseCurrent(generation))
            {
                _partnerSubmitting = false;
                if (!IsWorkOverlayBlocking) _hud.SetActionControlsLocked(false);
            }
        }
    }

    private async Task<PartnerPendingRecoveryState> QueryPartnerPendingCheckAsync(int generation)
    {
        var result = await ApiClient.GetPendingCheckAsync(_gameId);
        if (!IsResponseCurrent(generation)) return PartnerPendingRecoveryState.Failed;
        if (!result.Ok)
        {
            _partnerOverlay.ShowError($"待判定查询失败，已保留原伙伴请求：{FirstNonEmpty(result.Message, null, "请重试恢复。")}");
            return PartnerPendingRecoveryState.Failed;
        }
        if (result.Data == null) return PartnerPendingRecoveryState.None;
        if (TryGetCurrentPlayer(out var player)
            && TryValidatePendingPartnerRecruit(result.Data, _gameId, player.PlayerId, out var pending, out _))
        {
            if (_unresolvedPartnerRequest is { } unresolved
                && (!string.Equals(unresolved.ActionType, "DRAW_PARTNER_OFFER", StringComparison.OrdinalIgnoreCase)
                    || unresolved.PlayerId != player.PlayerId))
            {
                _partnerOverlay.ShowError("当前伙伴未决请求与服务端待判定身份不一致，已停止自动恢复。" );
                return PartnerPendingRecoveryState.Conflict;
            }
            _unresolvedPartnerRequest = null;
            OpenPendingWork(pending, true);
            return PartnerPendingRecoveryState.Recovered;
        }
        _partnerOverlay.ShowError("服务端存在其他待判定，未重放原伙伴招募准备。请先同步并处理当前权威待办。");
        return PartnerPendingRecoveryState.Conflict;
    }

    private async Task RefreshPartnerAuthorityAsync(int generation)
    {
        InvalidateAvailableActions();
        await RefreshPlayerViewAsync();
        await RefreshAvailableActionsAsync(true);
        if (IsResponseCurrent(generation) && TryGetCurrentPlayer(out var player))
        {
            await RefreshPartnerSnapshotForOverlayAsync(player.PlayerId, generation, false);
            await RefreshInventorySnapshotAfterPartnerAsync(player.PlayerId, generation);
            await RefreshQuestSnapshotAfterPartnerAsync(player.PlayerId, generation);
        }
    }

    private async Task RefreshPartnerSnapshotForOverlayAsync(long playerId, int generation, bool showLoading = true)
    {
        var serial = ++_partnerRequestSerial;
        if (showLoading) _partnerOverlay.OpenLoading(true);
        var result = await SafePaperHudGet(() => ApiClient.GetPlayerPartnersAsync(_gameId, playerId), "伙伴列表");
        if (!IsResponseCurrent(generation) || serial != _partnerRequestSerial) return;
        if (!result.Ok)
        {
            _partnerOverlay.ShowError(FirstNonEmpty(result.Message, null, "伙伴列表读取失败。"));
            return;
        }
        if (!PartnerSystemModel.TryValidateSnapshot(result.Data, _gameId, playerId, out var error))
        {
            _partnerOverlay.ShowError(error);
            return;
        }
        if (result.Data!.StateVersion < _paperHudPartnerVersion) return;
        _partnerSnapshot = result.Data;
        _paperHudPartnerVersion = result.Data.StateVersion!.Value;
        _hud.SetPartnerPreview(result.Data.Partners ?? []);
        if (_partnerOverlay.Mode is PartnerOverlayMode.LoadingTeam or PartnerOverlayMode.Team)
        {
            _partnerOverlay.ShowTeam(result.Data, SupportsPartnerRecruitment, _partnerSubmitting);
        }
    }

    private async Task RefreshInventorySnapshotAfterPartnerAsync(long playerId, int generation)
    {
        var result = await ApiClient.GetPlayerItemsAsync(_gameId, playerId);
        if (IsResponseCurrent(generation) && result.Ok && result.Data != null
            && ValidatePlayerItems(result.Data, _gameId, playerId, out _)) _inventorySnapshot = result.Data;
    }

    private async Task RefreshQuestSnapshotAfterPartnerAsync(long playerId, int generation)
    {
        var result = await ApiClient.GetPlayerQuestsAsync(_gameId, playerId);
        if (IsResponseCurrent(generation) && result.Ok && result.Data != null
            && ValidatePlayerQuests(result.Data, _gameId, playerId, out _)) _questJournalSnapshot = result.Data;
    }

    private void HandlePlayerViewForPartnerSystem(PlayerViewResponseDto view)
    {
        if (!GodotObject.IsInstanceValid(_partnerOverlay) || !TryGetCurrentPlayer(out var player)) return;
        if (_unresolvedPartnerRequest is { } unresolved
            && string.Equals(unresolved.ActionType, "DRAW_PARTNER_OFFER", StringComparison.OrdinalIgnoreCase)
            && unresolved.PlayerId == player.PlayerId
            && TryValidatePendingPartnerRecruit(view.EngineState?.PendingCheck, _gameId, player.PlayerId, out _, out _))
        {
            // 新鲜玩家视图已经证明准备成功，结束原未决上下文，后续由待判定界面接管。
            _unresolvedPartnerRequest = null;
        }

        var reward = view.EngineState?.PendingReward;
        var rewardUiOpen = _partnerOverlay.Mode is PartnerOverlayMode.LoadingReward
                or PartnerOverlayMode.Reward or PartnerOverlayMode.ItemSelection
            || (_partnerOverlay.Mode == PartnerOverlayMode.Error && _partnerOverlay.CurrentRewardId > 0)
            || _questJournal.State == QuestJournalState.Reward;
        var shouldAutoOpen = !IsWorkOverlayBlocking
            && reward != null
            && (rewardUiOpen
                || (!_partnerOverlay.IsOpen && !_questJournal.IsOpen)
                || _partnerOverlay.Mode == PartnerOverlayMode.Team);
        if (rewardUiOpen || shouldAutoOpen)
        {
            RouteAuthoritativeReward(reward, player.PlayerId);
        }
        else if (reward == null && rewardUiOpen)
        {
            RouteAuthoritativeReward(null, player.PlayerId);
        }
    }

    private void OnPartnerSnapshotChanged(PlayerPartnerListResponseDto snapshot)
    {
        if (!GodotObject.IsInstanceValid(_partnerOverlay)) return;
        if (_partnerOverlay.Mode is PartnerOverlayMode.Team or PartnerOverlayMode.LoadingTeam)
            _partnerOverlay.ShowTeam(snapshot, SupportsPartnerRecruitment, _partnerSubmitting);
    }

    private void RoutePartnerRewardOrTeam(string? message = null)
    {
        if (!TryGetCurrentPlayer(out var player)) return;
        var reward = _latestPlayerView?.EngineState?.PendingReward;
        if (reward != null)
        {
            RouteAuthoritativeReward(reward, player.PlayerId, message, fallbackToTeam: true);
        }
        else if (_partnerSnapshot != null)
        {
            _questJournal.ForceClose();
            _partnerOverlay.ShowTeam(_partnerSnapshot, SupportsPartnerRecruitment, _partnerSubmitting, message);
        }
        else
        {
            _partnerOverlay.ShowError(message ?? "伙伴状态正在同步，请重试。" );
        }
    }

    /// <summary>统一按最新权威 FIFO 队首选择唯一交互层。</summary>
    private void RouteAuthoritativeReward(
        PendingRewardDto? reward,
        long currentPlayerId,
        string? message = null,
        bool fallbackToTeam = false)
    {
        if (reward == null)
        {
            if (_partnerOverlay.Mode is PartnerOverlayMode.LoadingReward or PartnerOverlayMode.Reward or PartnerOverlayMode.ItemSelection)
                _partnerOverlay.ForceClose();
            if (_questJournal.State == QuestJournalState.Reward) _questJournal.ForceClose();
            if (fallbackToTeam && _partnerSnapshot != null)
                _partnerOverlay.ShowTeam(_partnerSnapshot, SupportsPartnerRecruitment, _partnerSubmitting, message);
            return;
        }

        var own = reward.PlayerId == currentPlayerId;
        var kind = reward.Kind?.Trim().ToUpperInvariant() ?? "";
        if (own && kind == "PARTNER"
            && string.Equals(reward.SourceType, "PARTNER_RECRUIT", StringComparison.OrdinalIgnoreCase))
        {
            if ((reward.Candidates?.Count ?? 0) == 0)
            {
                _questJournal.ForceClose();
                _partnerOverlay.ForceClose();
                var emptyMessage = FirstNonEmpty(reward.DrawResult?.Message, null, "本次没有实际伙伴候选，客户端不会补牌。" );
                if (fallbackToTeam && _partnerSnapshot != null)
                    _partnerOverlay.ShowTeam(_partnerSnapshot, SupportsPartnerRecruitment, _partnerSubmitting, emptyMessage);
                else
                    ShowGameStatusError(emptyMessage);
                return;
            }
            _questJournal.ForceClose();
            _partnerOverlay.SyncRewardAuthority(reward, currentPlayerId, SupportsPartnerRecruitment, _partnerSubmitting, message);
            return;
        }

        if (own && kind == "ITEM")
        {
            _partnerOverlay.ForceClose();
            _questJournal.ShowReward(reward, currentPlayerId);
            if (!string.IsNullOrWhiteSpace(message)) _questJournal.SetStatusMessage(message);
            return;
        }

        _partnerOverlay.ForceClose();
        _questJournal.ForceClose();
        ShowGameStatusError(!own
            ? "当前 FIFO 队首属于其他玩家，已隐藏其私有候选并等待处理。"
            : $"当前 FIFO 奖励类型「{FirstNonEmpty(reward.Kind, null, "未知")}」尚未接入，已保留权威阻塞状态。" );
    }

    private void SetActiveRewardStatus(string message)
    {
        if (_questJournal.State == QuestJournalState.Reward) _questJournal.SetStatusMessage(message);
        else if (_partnerOverlay.Mode is PartnerOverlayMode.Reward or PartnerOverlayMode.ItemSelection)
            _partnerOverlay.SetStatusMessage(message);
    }

    private GameActionRequestDto NewPartnerRequest(long playerId, string actionType, JsonObject payload) => new()
    {
        RequestId = Guid.NewGuid().ToString("D"),
        PlayerId = playerId,
        StateVersion = _latestPlayerView!.StateVersion,
        ActionType = actionType,
        Payload = payload
    };

    private AvailableActionDto? CurrentPartnerAction(string actionType) =>
        _latestPlayerView != null && _availableActionsStateVersion == _latestPlayerView.StateVersion
            ? _latestAvailableActions.FirstOrDefault(action => string.Equals(action.ActionType, actionType, StringComparison.OrdinalIgnoreCase))
            : null;

    private bool CanStartPartnerWrite() =>
        _active && IsGameConnectionReady && SupportsPartnerRecruitment
        && _latestPlayerView != null && _unresolvedPartnerRequest == null && !_partnerRecoveryInProgress
        && !_partnerSubmitting && !_itemSystemSubmitting
        && !_questSystemSubmitting && !_questAcceptSubmitting && !_questTargetSubmissionSubmitting
        && !IsQuestCheckPreparing && !_endTurnSubmitting && !IsWorkActionInProgress
        && _moveState != PlayerMoveState.SubmittingMove;

    private static bool TryValidatePendingPartnerRecruit(
        PendingCheckDto? source,
        long gameId,
        long playerId,
        out PendingCheckDto pending,
        out string error)
    {
        pending = source!;
        if (source?.CheckId is not > 0 || source.StateVersion == null || source.AuthorizationRound == null
            || !string.Equals(source.BusinessType, "PARTNER_RECRUIT", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.Status, "WAITING_ROLL", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.AttributeType, "SOCIAL", StringComparison.OrdinalIgnoreCase))
        {
            error = "服务端伙伴招募待判定缺少身份、版本、轮次、状态或固定社交属性。";
            return false;
        }
        if (source.GameId is > 0 && source.GameId != gameId)
        {
            error = "服务端返回了其他对局的伙伴招募判定。";
            return false;
        }
        if (source.PlayerId is > 0 && source.PlayerId != playerId)
        {
            error = "服务端返回了其他玩家的伙伴招募判定。";
            return false;
        }
        error = "";
        return true;
    }

    private static string FormatRecruitResult(PartnerRecruitResultDto result)
    {
        var cost = result.Payment?.Cost;
        var parts = new[]
        {
            cost?.Prestige is > 0 ? $"{cost.Prestige} 声望" : null,
            cost?.Coins is > 0 ? $"{cost.Coins} 金币" : null,
            cost?.Social is > 0 ? $"{cost.Social} 社交" : null,
            cost?.ItemQuantity is > 0 ? $"{cost.ItemQuantity} 件道具" : null
        }.Where(value => value != null);
        return $"伙伴招募成功，状态：{result.Status ?? "未知"}；实际支付：{string.Join("、", parts.DefaultIfEmpty("无资源费用"))}。";
    }

    private void OnPartnersRequested() => _ = OpenPartnersAsync();
    private void OnPartnerRecruitRequested() => _ = BeginPartnerRecruitmentAsync();
    private void OnPartnerDismissRequested(long id) => _ = DismissPartnerAsync(id);
    private void OnPartnerRecruitConditionRequested(long rewardId, long cardId, int conditionIndex, long itemId) =>
        _ = RecruitPartnerAsync(rewardId, cardId, conditionIndex, itemId);
    private void OnPartnerAbandonRequested(long rewardId) => _ = AbandonPartnerRewardAsync(rewardId);
    private void OnPartnerRetryRequested()
    {
        if (_unresolvedPartnerRequest != null)
        {
            _ = RetryUnresolvedPartnerActionAsync();
        }
        else if (_partnerOverlay.Mode == PartnerOverlayMode.Error)
        {
            _ = OpenPartnersAsync();
        }
    }

    private bool TryShowUnresolvedPartnerOperation()
    {
        if (_unresolvedPartnerRequest == null) return false;
        _partnerOverlay.ShowError("上一次伙伴操作结果仍未知，只能使用原 requestId、原版本和原请求体恢复。" );
        return true;
    }

    private async Task RetryUnresolvedPartnerActionAsync()
    {
        var request = _unresolvedPartnerRequest;
        if (request == null || _partnerSubmitting || _partnerRecoveryInProgress || !_active) return;
        _partnerRecoveryInProgress = true;
        try
        {
        var kind = request.ActionType switch
        {
            "DRAW_PARTNER_OFFER" => PartnerWriteKind.DrawOffer,
            "RECRUIT_PARTNER_FROM_OFFER" => PartnerWriteKind.Recruit,
            "DISMISS_PARTNER" => PartnerWriteKind.Dismiss,
            _ => PartnerWriteKind.Abandon
        };
        if (kind == PartnerWriteKind.DrawOffer)
        {
            var state = await QueryPartnerPendingCheckAsync(_generation);
            if (state != PartnerPendingRecoveryState.None) return;
        }
        await SubmitPartnerActionAsync(request, kind, true);
        }
        finally
        {
            if (IsResponseCurrent(_generation)) _partnerRecoveryInProgress = false;
        }
    }

    private enum PartnerWriteKind { DrawOffer, Recruit, Dismiss, Abandon }
}
