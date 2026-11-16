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
    /// 伙伴编码（后端 partnerCode）→ 卡图。正式名单尚未从后端拿到，确认后在这里补全；
    /// 补全前按下面的名字对照表兜底。
    /// </summary>
    public static IReadOnlyDictionary<string, string> PartnerCodeArtPaths { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 伙伴名字（卡面英文名与常见中文译名）→ 卡图（来自 2.0 内测版“伙伴2.0”卡图）。
    /// 中文译名为客户端推测，后端正式名单下发后以 PartnerCodeArtPaths 为准。
    /// </summary>
    public static IReadOnlyDictionary<string, string> PartnerNameArtPaths { get; } = BuildPartnerNameMap();

    private static Dictionary<string, string> BuildPartnerNameMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(string file, params string[] names)
        {
            foreach (var name in names)
            {
                map[name] = $"res://assets/cards/partners/{file}";
            }
        }

        Add("AB.jpg", "Apple Bloom", "小苹花");
        Add("AJ新.jpg", "Applejack", "Apple Jack", "苹果嘉儿");
        Add("BB新.jpg", "Bon Bon", "糖糖");
        Add("BM.jpg", "Big Macintosh", "Big Mac", "大麦克");
        Add("CC.jpg", "Coco Pommel", "可可");
        Add("CG.jpg", "Cozy Glow", "柯西光辉");
        Add("CS新.jpg", "Cheese Sandwich", "奶酪三明治");
        Add("DD新.jpg", "Daring Do", "天马无畏");
        Add("DH新.jpg", "Derpy Hooves", "Derpy", "小呆");
        Add("DJ.jpg", "Vinyl Scratch", "DJ PON-3", "维尼尔");
        Add("DOC.jpg", "Doctor Whooves", "神秘博士");
        Add("DT.jpg", "Diamond Tiara", "Silver Spoon", "钻石冠", "银勺");
        Add("FL.jpg", "Flash Sentry", "闪光哨兵");
        Add("FS新.jpg", "Fluttershy", "柔柔", "小蝶");
        Add("GS新.jpg", "Granny Smith", "史密斯奶奶");
        Add("LH新.jpg", "Lyra Heartstrings", "Lyra Heartstring", "天琴心弦");
        Add("MP新.jpg", "Maud Pie", "莫德派");
        Add("MR.jpg", "Mayor", "Mayor Mare", "镇长");
        Add("OCT.jpg", "Octavia Melody", "奥克塔维亚");
        Add("PCD.jpg", "Princess Cadance", "韵律公主");
        Add("PC新.jpg", "Princess Celestia", "宇宙公主");
        Add("PL.jpg", "Princess Luna", "月亮公主");
        Add("PP新.jpg", "Pinkie Pie", "萍琪", "碧琪");
        Add("RD新.jpg", "Rainbow Dash", "云宝");
        Add("RR新.jpg", "Rarity", "珍奇");
        Add("SA.jpg", "Shining Armor", "闪耀盔甲");
        Add("SB.jpg", "Sweetie Belle", "甜贝儿");
        Add("SL.jpg", "Scootaloo", "飞板璐");
        Add("SP.jpg", "Spike", "穗龙");
        Add("SS新.jpg", "Sunset Shimmer", "日光烁烁");
        Add("TS新.jpg", "Twilight Sparkle", "暮光闪闪", "紫悦");
        Add("TX新.jpg", "Trixie", "崔克茜");
        Add("ZC.jpg", "Zecora", "泽科拉");
        Add("sg没改.jpg", "Starlight Glimmer", "星光熠熠");
        return map;
    }

    /// <summary>伙伴卡图。按编码优先、名字兜底；都找不到时给出卡背并返回 false。</summary>
    public static bool TryGetPartnerTexture(string? partnerCode, string? partnerName, out Texture2D? texture, out string error)
    {
        texture = null;
        if (!string.IsNullOrWhiteSpace(partnerCode)
            && PartnerCodeArtPaths.TryGetValue(partnerCode.Trim(), out var codePath))
        {
            return TryLoad(codePath, out texture, out error);
        }

        if (!string.IsNullOrWhiteSpace(partnerName)
            && PartnerNameArtPaths.TryGetValue(partnerName.Trim(), out var namePath))
        {
            return TryLoad(namePath, out texture, out error);
        }

        TryLoad(PartnerCardBackPath, out texture, out _);
        error = $"伙伴 {partnerCode ?? partnerName ?? "<空>"} 没有本地卡图映射，暂用卡背。";
        return false;
    }

    /// <summary>开局角色卡图（名牌与底栏头像使用，复用选角的编码映射）。</summary>
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
