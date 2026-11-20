namespace EquestriaStar.Game.UI;

/// <summary>
/// 纯显示用的枚举中文名（服务端已给中文文本时由调用方优先使用服务端文本）。
/// 不参与任何判断或提交，未知枚举原样显示。
/// </summary>
public static class PaperDisplayText
{
    public static string ItemType(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "CONSUMABLE" => "消耗品",
        "EQUIPMENT" => "装备",
        "MISC" => "杂物",
        null or "" => "未知类型",
        _ => value!.Trim()
    };

    public static string Location(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "HAND" => "手牌",
        "EQUIPPED" => "已装备",
        null or "" => "未知",
        _ => value!.Trim()
    };
}
