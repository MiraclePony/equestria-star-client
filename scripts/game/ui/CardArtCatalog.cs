using System;
using System.Collections.Generic;
using Godot;

namespace EquestriaStar.Game.UI;

/// <summary>
/// CORE_9 卡牌原图的稳定映射。业务 ID 与资源路径在此集中维护，不根据名称或文件名猜测。
/// </summary>
public static class CardArtCatalog
{
    public static IReadOnlyDictionary<string, string> QuestArtPaths { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OFFICIAL_QUEST_006"] = "res://assets/cards/quests/6.jpg",
            ["OFFICIAL_QUEST_007"] = "res://assets/cards/quests/7.jpg",
            ["OFFICIAL_QUEST_010"] = "res://assets/cards/quests/10.jpg",
            ["OFFICIAL_QUEST_013"] = "res://assets/cards/quests/13.jpg",
            ["OFFICIAL_QUEST_014"] = "res://assets/cards/quests/14.jpg",
            ["OFFICIAL_QUEST_015"] = "res://assets/cards/quests/15.jpg",
            ["OFFICIAL_QUEST_016"] = "res://assets/cards/quests/16.jpg",
            ["OFFICIAL_QUEST_017"] = "res://assets/cards/quests/17.jpg",
            ["OFFICIAL_QUEST_018"] = "res://assets/cards/quests/18.jpg",
            ["OFFICIAL_QUEST_021"] = "res://assets/cards/quests/21.jpg",
            ["OFFICIAL_QUEST_022"] = "res://assets/cards/quests/22.jpg",
            ["OFFICIAL_QUEST_023"] = "res://assets/cards/quests/24.jpg",
            ["OFFICIAL_QUEST_025"] = "res://assets/cards/quests/26.jpg",
            ["OFFICIAL_QUEST_026"] = "res://assets/cards/quests/27.jpg",
            ["OFFICIAL_QUEST_038"] = "res://assets/cards/quests/32.jpg",
            ["OFFICIAL_QUEST_042"] = "res://assets/cards/quests/36.jpg",
            ["OFFICIAL_QUEST_045"] = "res://assets/cards/quests/38.jpg",
            ["OFFICIAL_QUEST_046"] = "res://assets/cards/quests/40.jpg",
            ["OFFICIAL_QUEST_047"] = "res://assets/cards/quests/41.jpg",
            ["OFFICIAL_QUEST_049"] = "res://assets/cards/quests/43.jpg",
            ["OFFICIAL_QUEST_050"] = "res://assets/cards/quests/44.jpg",
            ["OFFICIAL_QUEST_051"] = "res://assets/cards/quests/45.jpg",
            ["OFFICIAL_QUEST_052"] = "res://assets/cards/quests/46.jpg",
            ["OFFICIAL_QUEST_066"] = "res://assets/cards/quests/60.jpg"
        };

    public static IReadOnlyDictionary<long, string> ItemArtPaths { get; } =
        new Dictionary<long, string>
        {
            [1003] = "res://assets/cards/items/苹果新.jpg",
            [1005] = "res://assets/cards/items/花束.jpg",
            [1013] = "res://assets/cards/items/魔法书.jpg",
            [1017] = "res://assets/cards/items/药剂.jpg",
            [1018] = "res://assets/cards/items/蹄铁新.jpg"
        };

    public const string PartnerCardBackPath = "res://assets/cards/partners/伙伴卡背.jpg";

    /// <summary>
    /// 伙伴卡按服务端 partnerCode 绑定。代码同时接受正式 P_ 前缀和同值短码，
    /// 仅用于同一张稳定资源映射，不按中文名称猜测业务身份。
    /// </summary>
    public static IReadOnlyDictionary<string, string> PartnerCodeArtPaths { get; } = BuildPartnerCodeMap();

    private static Dictionary<string, string> BuildPartnerCodeMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(string code, string file)
        {
            var path = $"res://assets/cards/partners/{file}";
            map[code] = path;
            map[$"P_{code}"] = path;
        }

        Add("AB", "AB.jpg");
        Add("AJ", "AJ新.jpg");
        Add("BB", "BB新.jpg");
        Add("BM", "BM.jpg");
        Add("CC", "CC.jpg");
        Add("CG", "CG.jpg");
        Add("CS", "CS新.jpg");
        Add("DD", "DD新.jpg");
        Add("DH", "DH新.jpg");
        Add("DJ", "DJ.jpg");
        Add("DOC", "DOC.jpg");
        Add("DT", "DT.jpg");
        Add("FL", "FL.jpg");
        Add("FS", "FS新.jpg");
        Add("GS", "GS新.jpg");
        Add("LH", "LH新.jpg");
        Add("MP", "MP新.jpg");
        Add("MR", "MR.jpg");
        Add("OCT", "OCT.jpg");
        Add("PCD", "PCD.jpg");
        Add("PC", "PC新.jpg");
        Add("PL", "PL.jpg");
        Add("PP", "PP新.jpg");
        Add("RD", "RD新.jpg");
        Add("RR", "RR新.jpg");
        Add("SA", "SA.jpg");
        Add("SB", "SB.jpg");
        Add("SL", "SL.jpg");
        Add("SP", "SP.jpg");
        Add("SS", "SS新.jpg");
        Add("TS", "TS新.jpg");
        Add("TX", "TX新.jpg");
        Add("ZC", "ZC.jpg");
        Add("SG", "sg没改.jpg");

        // CORE_9 正式 partnerCode。保留上方短码与 P_ 旧别名，避免旧快照丢图。
        map["BIG_MAC"] = "res://assets/cards/partners/BM.jpg";
        map["PRINCESS_LUNA"] = "res://assets/cards/partners/PL.jpg";
        map["SWEETIE_BELLE"] = "res://assets/cards/partners/SB.jpg";
        map["LYRA"] = "res://assets/cards/partners/LH新.jpg";
        map["CHEESE_SANDWICH"] = "res://assets/cards/partners/CS新.jpg";
        map["SCOOTALOO"] = "res://assets/cards/partners/SL.jpg";
        return map;
    }

    private static readonly Dictionary<string, Texture2D> TextureCache = new(StringComparer.Ordinal);

    public static bool TryGetQuestTexture(string? questCode, out Texture2D? texture, out string error)
    {
        texture = null;
        if (string.IsNullOrWhiteSpace(questCode) || !QuestArtPaths.TryGetValue(questCode.Trim(), out var path))
        {
            error = $"任务 {questCode ?? "<空>"} 没有本地卡图映射。";
            return false;
        }

        return TryLoad(path, out texture, out error);
    }

    public static bool TryGetItemTexture(long itemId, out Texture2D? texture, out string error)
    {
        texture = null;
        if (!ItemArtPaths.TryGetValue(itemId, out var path))
        {
            error = $"道具 {itemId} 没有本地卡图映射。";
            return false;
        }

        return TryLoad(path, out texture, out error);
    }

    public static bool TryGetPartnerTexture(string? partnerCode, string? partnerName, out Texture2D? texture, out string error)
    {
        texture = null;
        if (string.IsNullOrWhiteSpace(partnerCode)
            || !PartnerCodeArtPaths.TryGetValue(partnerCode.Trim(), out var path))
        {
            TryLoad(PartnerCardBackPath, out texture, out _);
            error = $"伙伴 {partnerCode ?? partnerName ?? "<空>"} 没有本地 partnerCode 卡图映射，暂用卡背。";
            return false;
        }

        return TryLoad(path, out texture, out error);
    }

    public static bool TryGetCharacterTexture(string? characterCode, out Texture2D? texture, out string error)
    {
        texture = null;
        if (string.IsNullOrWhiteSpace(characterCode)
            || !EquestriaStar.State.CharacterSelectionModel.KnownCardPaths.TryGetValue(characterCode.Trim(), out var path))
        {
            TryLoad(EquestriaStar.State.CharacterSelectionModel.CardBackPath, out texture, out _);
            error = $"角色 {characterCode ?? "<空>"} 没有本地卡图映射，暂用卡背。";
            return false;
        }

        return TryLoad(path, out texture, out error);
    }

    private static bool TryLoad(string path, out Texture2D? texture, out string error)
    {
        if (TextureCache.TryGetValue(path, out var cached) && GodotObject.IsInstanceValid(cached))
        {
            texture = cached;
            error = "";
            return true;
        }

        texture = GD.Load<Texture2D>(path);
        if (texture == null)
        {
            error = $"无法加载卡图资源：{path}";
            return false;
        }

        TextureCache[path] = texture;
        error = "";
        return true;
    }
}
