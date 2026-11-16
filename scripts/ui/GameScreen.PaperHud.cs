using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using EquestriaStar.Api;

namespace EquestriaStar.UI;

/// <summary>
/// 折纸 HUD 底栏与历史抽屉的只读数据。
/// - 背包 / 装备、个人任务数量：复用业务流程已有的快照（_inventorySnapshot、_latestPersonalQuests），
///   不在业务流程中追加请求；仅在进入对局（绑定到本人）时各读取一次作为初始预览。
/// - 伙伴：客户端原本没有伙伴流程，按服务端状态版本变化只读刷新。
/// - 历史记录：展开抽屉时读取 action-logs。
/// 全部为 GET，不发起任何写操作。
/// </summary>
public partial class GameScreen
{
    private long _hudPreviewPlayerId;
    private long _hudPartnerVersion = -1;
    private bool _hudPartnerLoading;
    private bool _hudPartnerPending;
    private bool _hudHistoryLoading;
    private int _hudPreviewGeneration;

    private void ConnectPaperHudSignals()
    {
        _hud.HistoryRequested += OnHudHistoryRequested;
    }

    private void DisconnectPaperHudSignals()
    {
        if (GodotObject.IsInstanceValid(_hud))
        {
            _hud.HistoryRequested -= OnHudHistoryRequested;
        }
    }

    private void ResetPaperHudPreviews()
    {
        _hudPreviewGeneration++;
        _hudPreviewPlayerId = 0;
        _hudPartnerVersion = -1;
        _hudPartnerPending = false;
        if (GodotObject.IsInstanceValid(_hud) && _hud.IsNodeReady())
        {
            _hud.SetInventoryPreview(null);
            _hud.SetPartnerPreview(null);
            _hud.SetPersonalQuestCount(null);
        }
    }

    private void OnInventorySnapshotChanged(PlayerItemsResponseDto? snapshot)
    {
        if (snapshot != null && GodotObject.IsInstanceValid(_hud) && _hud.IsNodeReady())
        {
            _hud.SetInventoryPreview(snapshot.Items ?? []);
        }
    }

    private void OnPersonalQuestSnapshotChanged(PlayerQuestListResponseDto? snapshot)
    {
        if (snapshot != null && GodotObject.IsInstanceValid(_hud) && _hud.IsNodeReady())
        {
            _hud.SetPersonalQuestCount(snapshot.Quests?.Count ?? 0);
        }
    }

    private void HandlePlayerViewForPaperHud(PlayerViewResponseDto playerView)
    {
        var self = playerView.Players?.FirstOrDefault(player => player.UserId == _currentUserId);
        if (self == null || self.PlayerId <= 0 || _gameId <= 0)
        {
            return;
        }

        if (self.PlayerId != _hudPreviewPlayerId)
        {
            // 进入对局 / 换绑玩家：背包与任务只读预览各一次。
            _hudPreviewPlayerId = self.PlayerId;
            _ = LoadInitialPaperHudPreviewAsync(_gameId, self.PlayerId, _hudPreviewGeneration);
        }

        if (playerView.StateVersion > _hudPartnerVersion)
        {
            if (_hudPartnerLoading)
            {
                _hudPartnerPending = true;
            }
            else
            {
                _ = RefreshPartnerPreviewAsync(_gameId, self.PlayerId, playerView.StateVersion, _hudPreviewGeneration);
            }
        }
    }

    private async Task LoadInitialPaperHudPreviewAsync(long gameId, long playerId, int generation)
    {
        var itemsTask = SafeHudGet(() => ApiClient.GetPlayerItemsAsync(gameId, playerId), "道具列表");
        var questsTask = SafeHudGet(() => ApiClient.GetPlayerQuestsAsync(gameId, playerId), "个人任务");
        await Task.WhenAll(itemsTask, questsTask);
        if (!IsHudPreviewCurrent(gameId, generation))
        {
            return;
        }

        var items = itemsTask.Result;
        if (items.Ok && items.Data != null)
        {
            // 只更新底栏，不写入业务快照，避免影响各业务流程自己的版本判断。
            _hud.SetInventoryPreview(items.Data.Items ?? []);
        }
        else if (_inventorySnapshot == null)
        {
            _hud.SetInventoryPreview(null, FirstNonEmpty(items.Message, null, "道具列表读取失败"));
        }

        var quests = questsTask.Result;
        if (quests.Ok && quests.Data != null && _latestPersonalQuests == null)
        {
            _hud.SetPersonalQuestCount(quests.Data.Quests?.Count ?? 0);
        }
    }

    private async Task RefreshPartnerPreviewAsync(long gameId, long playerId, long stateVersion, int generation)
    {
        _hudPartnerLoading = true;
        try
        {
            var partners = await SafeHudGet(() => ApiClient.GetPlayerPartnersAsync(gameId, playerId), "伙伴列表");
            if (!IsHudPreviewCurrent(gameId, generation))
            {
                return;
            }

            if (partners.Ok && partners.Data != null)
            {
                _hud.SetPartnerPreview(partners.Data.Partners ?? []);
                _hudPartnerVersion = Math.Max(_hudPartnerVersion, stateVersion);
            }
            else
            {
                _hud.SetPartnerPreview(null, FirstNonEmpty(partners.Message, null, "伙伴列表读取失败"));
                // 失败后不推进版本：下次状态变化时再试，不在此处循环重试。
                _hudPartnerVersion = Math.Max(_hudPartnerVersion, stateVersion);
            }
        }
        finally
        {
            _hudPartnerLoading = false;
        }

        if (_hudPartnerPending && _latestPlayerView != null && IsHudPreviewCurrent(gameId, generation))
        {
            _hudPartnerPending = false;
            HandlePlayerViewForPaperHud(_latestPlayerView);
        }
    }

    private bool IsHudPreviewCurrent(long gameId, int generation)
    {
        return GodotObject.IsInstanceValid(this)
            && IsInsideTree()
            && gameId == _gameId
            && generation == _hudPreviewGeneration
            && GodotObject.IsInstanceValid(_hud);
    }

    private static async Task<ApiResult<T>> SafeHudGet<T>(Func<Task<ApiResult<T>>> request, string what)
    {
        try
        {
            return await request();
        }
        catch (Exception exception)
        {
            return ApiResult<T>.Failure($"{what}请求异常：{exception.Message}", "HUD_PREVIEW_REQUEST_EXCEPTION");
        }
    }

    private async void OnHudHistoryRequested()
    {
        if (_hudHistoryLoading || _gameId <= 0)
        {
            return;
        }

        var gameId = _gameId;
        var generation = _hudPreviewGeneration;
        _hudHistoryLoading = true;
        _hud.SetActionLogs(null, loading: true);
        try
        {
            var result = await SafeHudGet(() => ApiClient.GetActionLogsAsync(gameId, 20), "行动日志");
            if (!IsHudPreviewCurrent(gameId, generation))
            {
                return;
            }

            if (result.Ok)
            {
                _hud.SetActionLogs(result.Data ?? []);
            }
            else
            {
                _hud.SetActionLogs(null, FirstNonEmpty(result.Message, null, "行动日志读取失败"));
            }
        }
        finally
        {
            _hudHistoryLoading = false;
        }
    }
}
