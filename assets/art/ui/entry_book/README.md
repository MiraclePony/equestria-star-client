# 入口界面“折纸立体书”素材（entry_book）

2026-11 定稿的隧道书概念图（`Claude outputs/entry_concepts/book/tunnel/`）落地用的素材。登录 / 注册 / 大厅 / 建房间 / 等待房间都是同一本书的一页。
素材里**不含任何控件文字**，文案、昵称、数字全部由 Godot 的 Label / Button 绘制。

代码入口：`scripts/ui/EntryBook.cs`（舞台、色板、胶囊按钮、输入框、状态纸签、页眉、玩家书签）；各页在自己的 `ApplyBookStyle()` 里排版（房间页在 `RoomScreen.Book.cs`）。

| 目录 | 内容 | 来源 |
| --- | --- | --- |
| `bg/` login、lobby、room、select（1920×1080） | 整本书背景：左页隧道书插图，右页空白。room 的舞台是空的，座位立牌由引擎按人数摆放 | GPT 在审定概念图上擦掉右页控件，Lanczos 放大到 1920×1080 |
| `parts/` 15 张 | login_card 借阅卡、pocket 书袋、tag_card 书签吊牌（已 2× 放大，九宫格用；穿孔已填暗色）、ribbon 丝带、seal 圆章、standee / standee_empty 座位立牌、crest 标志底牌、section_tab 分节标签、horseshoe_gold / horseshoe_empty 座位马蹄铁、crown 皇冠、seat_rose / seat_gold 号码圆章、close_round | GPT 素材表（品红底）→ `Claude outputs/entry_concepts/impl/slice_entry.py` 抠图切块 |
| `depth/` login、lobby、room（960×540 灰度） | 左页视差用的深度图：白 = 靠前不动，黑 = 深远（`BookParallax` + `assets/shaders/book_parallax.gdshader`） | GPT 按背景生成分层深度图，缩小并轻微模糊 |
| `icons/` 16 张 | horseshoe、lock、eye、eye_closed、star、search、refresh、plus、logout、back、undo、signal、pencil、crown、check、close | 同上；切完后统一转成白色线稿，在引擎里用 Modulate 上色（默认墨棕 #4A3426） |

- 坐标：所有页面按 1920×1080 的“书本舞台”排版（`EntryBook.RightPage` 是各页右页版心），舞台居中，窗口更宽时四周露出压暗的桌面。
- 提示词与原始素材表在 `Claude outputs/entry_concepts/impl/`（`bg_*.md`、`e1_parts.md`、`e2_icons.md`，运行 `impl.ps1`）。
- select 是选角页的两页空白跨页（GPT 在大厅背景上擦掉左页插图）。
- 翻页效果（`scripts/ui/PageTurn.cs` + `assets/shaders/page_turn.gdshader`）不需要额外素材：旧画面实时截图，背面直接采样 `bg/` 里的新一页背景；启动封面是程序生成的。
