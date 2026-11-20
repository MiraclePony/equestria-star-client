using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using EquestriaStar.Api;

namespace EquestriaStar.UI;

/// <summary>
/// 纸艺 HUD 的只读数据桥接。复用现有背包、任务快照；伙伴按权威状态变化刷新，
/// 行动历史只在玩家展开抽屉时查询，不建立新的业务轮询。
/// </summary>
public partial class GameScreen
{
    private long _paperHudPlayerId;
    private long _paperHudPartnerVersion = -1;
    private int _paperHudGeneration;
    private bool _paperHudPartnerLoading;
    private bool _paperHudPartnerRefreshPending;
    private bool _paperHudHistoryLoading;
    private int _paperHudPartnerRequestSerial;
    private int _paperHudHistoryRequestSerial;
    private PlayerPartnerListResponseDto? _partnerSnapshot;

    private void ConnectPaperHudSignals()
    {
        _hud.HistoryRequested += OnPaperHudHistoryRequested;
    }

    private void DisconnectPaperHudSignals()
    {
        if (GodotObject.IsInstanceValid(_hud))
        {
            _hud.HistoryRequested -= OnPaperHudHistoryRequested;
        }
    }

    private void ResetPaperHudPreviews()
    {
        _paperHudGeneration++;
        _paperHudPlayerId = 0;
        _paperHudPartnerVersion = -1;
        _paperHudPartnerRefreshPending = false;
        _paperHudPartnerRequestSerial++;
        _paperHudHistoryRequestSerial++;
        _paperHudPartnerLoading = false;
        _paperHudHistoryLoading = false;
        _partnerSnapshot = null;
        if (GodotObject.IsInstanceValid(_hud) && _hud.IsNodeReady())
        {
            _hud.SetInventoryPreview(null);
            _hud.SetPartnerPreview(null);
            _hud.SetPersonalQuestCount(null);
            _hud.SetActionLogs(null);
        }
    }

    private void OnPaperInventorySnapshotChanged(PlayerItemsResponseDto? snapshot)
    {
        if (snapshot != null && GodotObject.IsInstanceValid(_hud) && _hud.IsNodeReady())
        {
            _hud.SetInventoryPreview(snapshot.Items ?? []);
        }
    }

    private void OnPaperQuestSnapshotChanged(PlayerQuestListResponseDto? snapshot)
    {
        if (snapshot != null && GodotObject.IsInstanceValid(_hud) && _hud.IsNodeReady())
        {
            _hud.SetPersonalQuestCount(snapshot.Quests?.Count ?? 0);
        }
    }

    private void HandlePlayerViewForPaperHud(PlayerViewResponseDto view)
    {
        var self = view.Players?.FirstOrDefault(player => player.UserId == _currentUserId);
        if (self?.PlayerId is not > 0 || _gameId <= 0)
        {
            return;
        }

        if (_paperHudPlayerId != self.PlayerId)
        {
            _paperHudPlayerId = self.PlayerId;
            _ = LoadInitialPaperHudPreviewAsync(_gameId, self.PlayerId, _paperHudGeneration);
        }

        if (view.StateVersion <= _paperHudPartnerVersion)
        {
            return;
        }

        if (_paperHudPartnerLoading)
        {
            _paperHudPartnerRefreshPending = true;
            return;
        }

        _ = RefreshPaperHudPartnersAsync(_gameId, self.PlayerId, view.StateVersion, _paperHudGeneration);
    }

    private async Task LoadInitialPaperHudPreviewAsync(long gameId, long playerId, int generation)
    {
        var itemsTask = SafePaperHudGet(() => ApiClient.GetPlayerItemsAsync(gameId, playerId), "道具列表");
        var questsTask = SafePaperHudGet(() => ApiClient.GetPlayerQuestsAsync(gameId, playerId), "个人任务");
        await Task.WhenAll(itemsTask, questsTask);
        if (!IsPaperHudCurrent(gameId, generation))
        {
            return;
        }

        // 首次并行预览只能填充空快照，不能覆盖打开背包后得到的更新数据。
        if (_inventorySnapshot == null && itemsTask.Result.Ok && itemsTask.Result.Data != null
            && ValidatePlayerItems(itemsTask.Result.Data, gameId, playerId, out _))
        {
            _inventorySnapshot = itemsTask.Result.Data;
        }
        else if (_inventorySnapshot == null)
        {
            _hud.SetInventoryPreview(null, FirstNonEmpty(itemsTask.Result.Message, null, "道具列表读取失败"));
        }

        if (_questJournalSnapshot == null && questsTask.Result.Ok && questsTask.Result.Data != null
            && ValidatePlayerQuests(questsTask.Result.Data, gameId, playerId, out _))
        {
            _questJournalSnapshot = questsTask.Result.Data;
            OnPaperQuestSnapshotChanged(_questJournalSnapshot);
        }
    }

    private async Task RefreshPaperHudPartnersAsync(long gameId, long playerId, long stateVersion, int generation)
    {
        var requestSerial = ++_paperHudPartnerRequestSerial;
        _paperHudPartnerLoading = true;
        try
        {
            var result = await SafePaperHudGet(() => ApiClient.GetPlayerPartnersAsync(gameId, playerId), "伙伴列表");
            if (!IsPaperHudCurrent(gameId, generation))
            {
                return;
            }

            if (result.Ok && result.Data != null
                && result.Data.GameId == gameId
                && result.Data.PlayerId == playerId
                && result.Data.StateVersion.HasValue
                && result.Data.StateVersion.Value >= _paperHudPartnerVersion)
            {
                _partnerSnapshot = result.Data;
                _paperHudPartnerVersion = result.Data.StateVersion.Value;
                _hud.SetPartnerPreview(_partnerSnapshot.Partners ?? []);
                OnPartnerSnapshotChanged(_partnerSnapshot);
            }
            else
            {
                // 查询失败或旧快照不能推进成功版本；保留最后一次成功数据显示非阻塞错误。
                var error = result.Ok
                    ? "伙伴列表身份、版本或数据结构无效。"
                    : FirstNonEmpty(result.Message, null, "伙伴列表读取失败");
                _hud.SetPartnerPreview(_partnerSnapshot?.Partners, error);
            }
        }
        finally
        {
            if (requestSerial == _paperHudPartnerRequestSerial)
            {
                _paperHudPartnerLoading = false;
            }
        }

        if (_paperHudPartnerRefreshPending && _latestPlayerView != null && IsPaperHudCurrent(gameId, generation))
        {
            _paperHudPartnerRefreshPending = false;
            HandlePlayerViewForPaperHud(_latestPlayerView);
        }
    }

    private async void OnPaperHudHistoryRequested()
    {
        if (_paperHudHistoryLoading || _gameId <= 0)
        {
            return;
        }

        var gameId = _gameId;
        var generation = _paperHudGeneration;
        var requestSerial = ++_paperHudHistoryRequestSerial;
        _paperHudHistoryLoading = true;
        _hud.SetActionLogs(null, loading: true);
        try
        {
            var result = await SafePaperHudGet(() => ApiClient.GetActionLogsAsync(gameId, 20), "行动历史");
            if (!IsPaperHudCurrent(gameId, generation))
            {
                return;
            }

            _hud.SetActionLogs(
                result.Ok ? result.Data ?? [] : null,
                result.Ok ? null : FirstNonEmpty(result.Message, null, "行动历史读取失败")
            );
        }
        finally
        {
            if (requestSerial == _paperHudHistoryRequestSerial)
            {
                _paperHudHistoryLoading = false;
            }
        }
    }

    private bool IsPaperHudCurrent(long gameId, int generation)
    {
        return GodotObject.IsInstanceValid(this)
            && IsInsideTree()
            && _active
            && _gameId == gameId
            && _paperHudGeneration == generation
            && GodotObject.IsInstanceValid(_hud);
    }

    private static async Task<ApiResult<T>> SafePaperHudGet<T>(Func<Task<ApiResult<T>>> request, string operation)
    {
        try
        {
            return await request();
        }
        catch (Exception exception)
        {
            return ApiResult<T>.Failure($"{operation}请求异常：{exception.Message}", "PAPER_HUD_READ_EXCEPTION");
        }
    }
}
