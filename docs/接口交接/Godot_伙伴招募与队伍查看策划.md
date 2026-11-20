# Godot 伙伴招募、队伍查看与独立遣散接入记录

日期：2026-11-20。状态：客户端本地接入并使用严格模拟契约验证；后端专项仍标记为“本地实现、隔离验收、尚未部署”，但另有 2026-11-17 的部署记录／用户环境报告。两者状态冲突，本轮未登录服务器独立核验，因此不把任一历史记录直接当作当前线上结论，也未进行真实服务联调。

依据：本目录 [ES 完整 API 文档](ES完整API文档.md)、[主动掷骰接入说明](Godot_任务主动掷骰接入说明.md)，以及后端最新专项 `D:/ES/docs/CORE_9伙伴招募与遣散_接口交接.md`。专项对伙伴逐方式资格、杂物选物和独立遣散具有更高优先级；本记录不再沿用旧版原子替换方案。

## 已接入范围

1. 纸艺 HUD 的“伙伴”入口启用，只读查询 `GET /games/{gameId}/partners?playerId=...`，按 `playerPartnerId` 展示实例、状态、六维文本和逐实例遣散资格。底部三个伙伴槽与队伍面板共用同一成功快照。
2. 行为菜单增加“招募伙伴”。只有新鲜 `player-view.engineState.partnerRecruitmentVersion=1`、连接就绪且 `DRAW_PARTNER_OFFER.available=true` 时启用；否则显示服务端原因。读取队伍不受该能力门槛限制。
3. 招募准备提交 `DRAW_PARTNER_OFFER {}`。只有本人 `PARTNER_RECRUIT`、`WAITING_ROLL`、固定 `SOCIAL` 的有效 `pendingCheck` 才进入现有物理骰子层；复用现有 `/checks/pending`、重投、最终确认和 requestId 恢复流程。
4. 最终判定先显示骰点结果，再按最新本人 FIFO 队首路由 `kind=PARTNER`、`sourceType=PARTNER_RECRUIT` 的候选。实际候选和少抽以 `deckDraws`、队首候选为准；零候选不伪造卡牌。
5. 每张候选按 `configuration.partner_name` 和 `configuration.partner_code` 显示。招募方式逐行读取 `recruitConditions[]`，按钮只看本行 `canExecute`；提交服务端给出的 `conditionIndex`，不使用数组下标、不使用旧候选级资格。
6. `ITEM_CATEGORY_SPEND` 必须打开选物页。`eligibleItems` 中 `HAND` 实例可选，`ineligibleItems` 中 `EQUIPPED` 实例只读；即使只有一件可用手牌也由玩家选择并确认。提交 `RECRUIT_PARTNER_FROM_OFFER {rewardId,cardId,conditionIndex,playerItemId}`，其他方式不携带 `playerItemId`。
7. 独立遣散读取 `/partners` 的整体 `dismissal` 和 `partners[i].dismissal`，不按数量、余额或 ACTIVE/PENDING_ACTIVE 本地推算。确认后提交 `DISMISS_PARTNER {playerPartnerId}`，费用和结果读取 `partnerDismissal`。
8. 放弃整批只在当前本人伙伴队首 `canAbandon=true` 时二次确认，提交 `RESOLVE_TURN_START_EFFECT {rewardId,abandon:true}`。遣散后仍刷新同一队首，不自动招募；放弃或后续招募失败不会恢复已成功遣散的伙伴。

## 准确请求和状态边界

- 所有 `/actions` 带本人局内 `playerId`、最新 `stateVersion` 和新 UUID `requestId`。结果未知时保留原路径、原 ID 和完整原请求体；版本过期刷新后由玩家重新确认，不能用新 ID 自动重复扣费。
- 招募 payload 不传 `dismissPlayerPartnerId`。独立遣散不套用丢弃道具次数，也不要求候选存在或伙伴栏已满。
- 招募实际支付读取 `partnerRecruit.payment`；遣散实际费用读取 `partnerDismissal`。客户端不预扣金币、声望、社交或道具，不用余额差推算费用。
- 成功后应用不旧于当前状态的 `playerViewState`，再刷新玩家视图、可用行动、伙伴、背包和任务。后续 GET 失败只重查，不重新执行已成功写操作。
- `/partners.data.stateVersion` 是伙伴列表快照版本。查询失败、身份冲突、结构异常和旧响应均不能推进成功版本或覆盖最后一次成功快照；首次背包和任务预览只能填充空快照，不能覆盖后续用户操作取得的数据。
- ITEM 与 PARTNER 使用同一 FIFO 路由。自己的伙伴队首进入伙伴层，其他玩家队首只显示权威阻塞；不同时打开 ITEM 与 PARTNER 两套奖励弹层。
- PENDING_ACTIVE 占伙伴栏，但属性和技能是否生效只认权威状态。END 同响应可能完成生效，客户端不自行切换状态或补行动点。

## 素材映射

正式编码保留旧别名和未知卡背：

| partnerCode | 卡图 |
|---|---|
| BIG_MAC | BM.jpg |
| PRINCESS_LUNA | PL.jpg |
| SWEETIE_BELLE | SB.jpg |
| LYRA | LH新.jpg |
| CHEESE_SANDWICH | CS新.jpg |
| SCOOTALOO | SL.jpg |

## 客户端结构

- `scripts/state/PartnerSystemModel.cs`：能力门槛、队首身份、逐方式定位、精确 payload 和独立遣散资格的纯逻辑。
- `scenes/game/ui/PartnerOverlay.tscn`、`scripts/game/ui/PartnerOverlay.cs`：队伍、候选、杂物选物、确认、错误和重试界面。
- `scripts/ui/GameScreen.Partners.cs`：请求生命周期、写操作互斥、版本保护、待判定恢复和 FIFO 路由。
- `GameHud`：只发出队伍与招募入口信号；不直接访问 Session 或接口。
- `GameScreen.Work.cs`、`WorkDiceOverlay.cs`：在既有物理骰流程中增加 `PARTNER_RECRUIT` 身份和结果分支，不复制骰子系统。

所有操作区域继续使用 `blocks_map_input`；按钮保留 Godot 正常 Pressed 处理，不在自身 GuiInput 阶段提前消费事件。心跳和玩家视图同步不因伙伴弹层暂停。

## 2026-11-20 审查修复

1. 候选身份改为严格校验 `candidate.partnerId == candidate.configuration.id`，两者都必须是正整数。客户端不再读取不存在的 `configuration.partner_id`；缺失、冲突或仅返回旧错误字段时均阻止领取请求并提示刷新。判定准备费用和最终招募支付仍是两个独立阶段。
2. 任务奖励层解除忙碌后会按最新权威资格恢复按钮，而不是把全部按钮统一启用。奖励页始终允许只读打开背包，不再受容量、`DISCARD_ITEM`、本人回合或奖励待办限制；具体丢弃操作仍按服务端资格灰显。背包关闭后重新同步玩家视图并路由当前 FIFO，不恢复进入背包前缓存的旧队首。
3. 同一 `rewardId/cardId/conditionIndex` 的杂物选物页会跨玩家视图轮询保持，仍有效的 `playerItemId` 选择和确认框也保持。确认动作捕获固定身份，提交前再对最新权威候选校验；资格、队首、方式或所选 HAND 实例失效时关闭确认并提示重新选择，不自动换扣。轮询重建控件时旧按钮会立即从容器移除，避免等待释放期间参与真实鼠标命中。
4. FIFO 由 `GameScreen` 统一路由：本人 `PARTNER/PARTNER_RECRUIT` 进入伙伴候选，本人 `ITEM` 进入道具奖励；其他玩家、未知 kind 和空队首均关闭旧私有候选。跨类型、同类型连续队首及归属变化只保留一个可交互奖励层；伙伴实际候选为空时不弹空招募窗口。
5. 未知 `DRAW_PARTNER_OFFER` 结果恢复复用原路径、原 `requestId`、原 `stateVersion` 和完整空 payload。历史 `WAITING_ROLL` 回执之后必须再次读取 `/checks/pending`；只有当前仍存在且属于本人本局的 `PARTNER_RECRUIT` 才进入骰子。当前 pending 已消失时结束旧未决上下文并同步权威状态；二次查询失败则保留原上下文和恢复入口，不创建新判定。

## 验证边界

本地测试覆盖正式六卡映射、准确候选身份四种组合、逐方式亮灰、杂物单件仍选物、装备只读、准确 payload、队伍快照版本、查询失败保留旧快照、选物及确认跨多次轮询、资格与队首失效、跨类型 FIFO、只读背包返回最新队首、准备拒绝不弹骰、历史回执后二次 pending 核实、无 pending 与复查失败、写操作互斥和正常 Viewport 鼠标按下／释放。

本轮验证结果：

- `dotnet build`：0 warning / 0 error。
- 本轮重跑 `RunPartnerSystemTests`、`RunGameScreenTests`、`RunDiceTests`、`RunQuestSystemTests`、`RunCore9CardViewerTests`、`RunGameConnectionTests`：退出码 0；其中 GameScreen、任务和卡牌测试仍输出既有 dummy renderer 退出资源诊断，未作为本轮新增业务失败掩盖。
- Godot Mono headless 导入和主场景启动：退出码 0，无 C#、脚本或场景解析错误。
- 最大化窗口与约 960×540 窗口：使用真实鼠标检查纸艺 HUD、行为菜单、背包弹层、伙伴队伍和招募入口；未发现裁切或地图输入穿透。候选逐方式、单件杂物选物、确认取消、轮询保持及 FIFO 切换由正常 Viewport 鼠标按下／释放的严格模拟流程覆盖。
- `tests/static_check.ps1`：在既有 `project.godot` 缺少脚本要求的 Forward+／Mobile 显式键处失败；按任务边界未修改渲染配置。
- `RunWorkFlowTests`：复现既有挂起，停在 `WORK_TEST_PHASE=fixed_pending`；按任务边界未修改打工取消／流程。

未覆盖：未登录或操作真实对局，当前部署状态也未独立核验；天琴加抽、少抽／零抽、PENDING_ACTIVE 到 ACTIVE、010／018 任务联动及同回合多次遣散仍需获授权后的真实联调。

需要用户决策的点：本轮未发现。
