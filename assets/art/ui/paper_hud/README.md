# 折纸风游戏 HUD 素材（paper_hud）

2026-11 定稿（m1–m3 概念图）对应的可拆分素材，共 64 张透明 PNG。由 GPT 生成素材表后按品红底抠图、按连通块切分（脚本：仓库外 `Claude outputs/ui_assets/tools/slice_sheets.py`）。
素材里**不含任何文字和数字**，数值、名字、按钮文字全部由 Godot 的 `Label` / `Button` 渲染。卡面一律来自现有卡图（`CardArtCatalog`），本目录只有纸框、按钮底和图标。

代码入口：`scripts/game/ui/PaperHudArt.cs`（加载、九宫格、胶囊、圆章四态）；版式：`scripts/game/ui/GameHud.PaperLayout.cs`。

| 目录 | 内容 | 用法 |
| --- | --- | --- |
| `icons/`（28） | coin 金币、reputation 声望、strength 力量、speed 速度、social 社交、charm 魅力、magic 魔法、mind 心智、partner 伙伴、item 道具、equip 装备、quest 任务、market 集市、chat 聊天、history 历史、map 地图、sync_ok 同步正常、round 轮次、end_turn 结束回合（马蹄铁）、unequip 卸下、accept 接取、buy 购买、lock、check、arrow_right、send、expand、close | `TextureRect` / `Button.Icon`，按 24–64 px 显示 |
| `buttons/`（14） | end_turn_normal/hover/pressed/disabled（大圆章四态）、round_peach/lav/sky 常态与悬停、round_gray_disabled、badge_red/badge_cream 角标、pill_gold/pill_cream 胶囊 | 圆章作为 `StyleBoxTexture` 整体拉伸；胶囊作为背景整体缩放 |
| `panels/`（13） | bottom_tray 底栏托盘、nameplate_normal/current/next/acted 名牌四态、drawer_lav/mint/cream 抽屉面板、handle_lav/mint 右侧竖拉手、handle_top 顶部拉手、tag 小纸签、slot_empty 空卡槽 | 九宫格（`NinePatchRect` / `StyleBoxTexture`），角边距 20–70 px |
| `cards/`（9） | frame_partner/item/equip/quest 竖版卡框、frame_quest_wide 横版任务卡框、stack_mint/stack_sky 牌堆、frame_highlight 选中描边、frame_disabled 禁用框 | 卡框中间是空白窗口，只在缺卡图时兜底；牌堆用于“+N 更多” |

- 伙伴卡图（2.0 内测版“伙伴2.0”，34 张 + 卡背）在 `assets/cards/partners/`，映射在 `CardArtCatalog.PartnerNameArtPaths`（按卡面英文名与常见中文译名；后端正式 partnerCode 名单下发后补 `PartnerCodeArtPaths`）。
- “结束回合”文字使用 `PaperHudArt.TitleFont`（系统字体回退链：站酷快乐体 → 微软雅黑粗体）。若要嵌入正式字体，把 OFL 授权的字体文件放进 `assets/fonts/` 并改 `TitleFont` 为 `FontFile`。

## portraits/（角色头像）

- `banner_<角色>.png` 760×220：左侧玩家名牌横幅；`square_<角色>.png` 400×400：底栏本人头像。
- 文件名 = 角色编码去掉 `INITIAL_` 后小写（如 `INITIAL_HONG_FANG` → `hong_fang`），由 `PaperHudArt.CharacterPortrait(code, kind)` 读取；缺素材时退回卡面裁切。
- 来源：现有角色卡由本机 GPT 抠出角色并补全被遮挡部分（纯品红底），再用 `Claude outputs/ui_assets/portraits/compose.py` 抠底、加白色贴纸描边、按 `heads.json` 头部框对焦、铺主题底色、烘焙圆角。
- 玩家名、角色名、声望、金币不烘焙进图，在引擎里用白字深棕描边直接压在头像上。
