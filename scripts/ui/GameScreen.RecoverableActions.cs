using System;
using System.Threading.Tasks;
using Godot;
using EquestriaStar.Api;

namespace EquestriaStar.UI;

public partial class GameScreen
{
    private const double IdempotentReplayDelaySeconds = 0.35;

    /// <summary>
    /// 仅判定“服务端是否已经执行仍未知”的传输或回执问题。
    /// 明确业务拒绝、版本过期和请求身份冲突不得自动重发。
    /// </summary>
    private static bool IsRecoverableUnknownResult(
        ApiResult<GameActionResponseDto> result,
        params string[] requestExceptionCodes)
    {
        if (result.IsAuthError)
        {
            return false;
        }

        var code = result.ErrorCode ?? "";
        if (code is "REQUEST_TIMEOUT" or "NETWORK_TIMEOUT" or "NETWORK_ERROR"
            or "EMPTY_RESPONSE" or "INVALID_JSON" or "RESPONSE_TYPE_MISMATCH"
            or "REQUEST_IN_PROGRESS")
        {
            return true;
        }

        if (string.Equals(code, "HTTP_ERROR", StringComparison.OrdinalIgnoreCase)
            && result.HttpStatus >= 500)
        {
            return true;
        }

        return Array.Exists(requestExceptionCodes, candidate =>
            string.Equals(candidate, code, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> WaitForIdempotentReplayAsync(int generation)
    {
        if (!IsResponseCurrent(generation))
        {
            return false;
        }

        await ToSignal(GetTree().CreateTimer(IdempotentReplayDelaySeconds), Timer.SignalName.Timeout);
        return IsResponseCurrent(generation);
    }
}
