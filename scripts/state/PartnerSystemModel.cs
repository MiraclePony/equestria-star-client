using System;
using System.Linq;
using System.Text.Json.Nodes;
using EquestriaStar.Api;

namespace EquestriaStar.State;

/// <summary>伙伴招募、FIFO 身份和逐方式资格的纯逻辑模型。</summary>
public static class PartnerSystemModel
{
    public static bool SupportsWrites(PlayerViewResponseDto? view) =>
        view?.EngineState?.PartnerRecruitmentVersion == 1;

    public static bool IsOwnPartnerReward(PendingRewardDto? reward, long playerId) =>
        reward?.RewardId is > 0
        && reward.PlayerId == playerId
        && string.Equals(reward.Kind, "PARTNER", StringComparison.OrdinalIgnoreCase)
        && string.Equals(reward.SourceType, "PARTNER_RECRUIT", StringComparison.OrdinalIgnoreCase);

    public static bool TryValidateSnapshot(
        PlayerPartnerListResponseDto? snapshot,
        long gameId,
        long playerId,
        out string error)
    {
        if (snapshot == null || snapshot.GameId != gameId || snapshot.PlayerId != playerId
            || snapshot.StateVersion is not >= 0 || snapshot.Partners == null)
        {
            error = "伙伴列表缺少有效对局、玩家、版本或列表字段。";
            return false;
        }
        if (snapshot.Partners.Any(partner => partner.PlayerPartnerId is not > 0
            || partner.PartnerId is not > 0 || string.IsNullOrWhiteSpace(partner.PartnerCode)
            || string.IsNullOrWhiteSpace(partner.Status)))
        {
            error = "伙伴列表包含缺少实例身份、配置身份、编码或状态的条目。";
            return false;
        }
        error = "";
        return true;
    }

    public static bool TryFindRecruitCondition(
        PendingRewardDto? reward,
        long playerId,
        long rewardId,
        long cardId,
        int conditionIndex,
        out RewardCandidateDto candidate,
        out PartnerRecruitConditionDto condition,
        out string error)
    {
        candidate = null!;
        condition = null!;
        if (!IsOwnPartnerReward(reward, playerId) || reward!.RewardId != rewardId)
        {
            error = "当前 FIFO 队首已变化或不属于本人伙伴招募。";
            return false;
        }
        candidate = reward.Candidates?.FirstOrDefault(value => value.CardId == cardId)!;
        condition = candidate?.RecruitConditions?.FirstOrDefault(value => value.ConditionIndex == conditionIndex)!;
        if (candidate == null || condition == null)
        {
            error = "伙伴候选或招募方式已失效。";
            return false;
        }
        if (candidate.PartnerId is not > 0 || candidate.Configuration?.Id is not > 0
            || candidate.PartnerId != candidate.Configuration.Id
            || string.IsNullOrWhiteSpace(candidate.Configuration.PartnerCode)
            || string.IsNullOrWhiteSpace(candidate.Configuration.PartnerName))
        {
            error = "伙伴候选配置身份不完整或冲突。";
            return false;
        }
        if (condition.ConditionIndex is not > 0 || condition.CanExecute != true)
        {
            error = FirstNonEmpty(condition.UnavailableReason, "服务端未允许使用该招募方式。");
            return false;
        }
        error = "";
        return true;
    }

    public static bool TryBuildRecruitPayload(
        PartnerRecruitConditionDto condition,
        long rewardId,
        long cardId,
        long playerItemId,
        out JsonObject payload,
        out string error)
    {
        payload = new JsonObject();
        if (rewardId <= 0 || cardId <= 0 || condition.ConditionIndex is not > 0)
        {
            error = "招募请求缺少奖励、卡牌或方式身份。";
            return false;
        }
        var itemMethod = string.Equals(condition.Method, "ITEM_CATEGORY_SPEND", StringComparison.OrdinalIgnoreCase);
        if (itemMethod)
        {
            var eligible = condition.EligibleItems?.Any(item => item.PlayerItemId == playerItemId
                && string.Equals(item.Location, "HAND", StringComparison.OrdinalIgnoreCase)) == true;
            if (!eligible)
            {
                error = "所选道具已不在服务端返回的可提交手牌中。";
                return false;
            }
        }
        else if (playerItemId > 0)
        {
            error = "非杂物招募方式不能携带 playerItemId。";
            return false;
        }

        payload["rewardId"] = rewardId;
        payload["cardId"] = cardId;
        payload["conditionIndex"] = condition.ConditionIndex.Value;
        if (itemMethod) payload["playerItemId"] = playerItemId;
        error = "";
        return true;
    }

    public static bool CanDismiss(PlayerPartnerDto? partner, out string reason)
    {
        if (partner?.PlayerPartnerId is not > 0 || partner.Dismissal?.CanExecute != true)
        {
            reason = FirstNonEmpty(partner?.Dismissal?.UnavailableReason, "服务端未允许遣散该伙伴。");
            return false;
        }
        reason = "";
        return true;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
}
