# 游戏内纸艺换皮规范（ui/ingame-paper-polish）

只改视觉，不改节点路径、脚本、信号、业务锁、确认步骤和关闭方式。入口页（登录 / 大厅 / 房间 / 选角）继续用 `client_theme.tres`，不受影响。

## 主题：`assets/theme/ingame_paper_theme.tres`

挂在游戏内各覆盖层的根节点上，C# 动态创建的按钮、标签、滚动条、提示框都会自动继承。

| 类型 / 变体 | 用途 | 外观 |
| --- | --- | --- |
| `Label` | 正文 | 深可可棕 `#5a3a28`，雅黑 600，18px |
| `PaperTitleLabel` | 页面 / 弹层标题 | 圆润粗体（站酷快乐体 → 雅黑），30px |
| `PaperMutedLabel` | 状态、提示、期限 | 暖灰棕 `#8a6e58`，16px |
| `PaperErrorLabel` | 错误、失败原因 | 砖红 `#9a3b2a` |
| `Button` | 主操作（接取 / 购买 / 提交 / 确认 / 领取） | 金色纸胶囊，底边加厚；悬停提亮、按下下沉；禁用为中性灰 + 灰字；焦点为金色外圈 |
| `PaperSecondaryButton` | 关闭、取消、返回、否 | 奶油纸胶囊 |
| `PaperDangerButton` | 放弃、丢弃、遣散 | 玫瑰色纸胶囊、砖红字 |
| `PanelContainer` | 主面板、确认框 | 奶油手撕纸九宫格（`paper_hud/panels/drawer_cream.png`） |
| `PaperCardPanel` | 列表里的信息卡（任务卡等） | 浅奶油圆角卡 + 细米色边 + 轻投影 |
| `PaperNoticePanel` | 行内提示 / 错误条 | 淡玫瑰底 + 砖红细边 |
| `PaperBarePanel` | 只用来接收输入的按钮外层容器 | 透明 |
| 滚动条、输入框、悬停提示 | 通用 | 暖米色轨道 + 驼色滑块；暖纸输入框；奶油提示框 |

遮罩：暖棕半透明 `Color(0.16, 0.1, 0.05, 0.55)`，不再用近黑色。

## 场景换皮脚本：`tools/ui/repaper_scene.py`

1. 主题由 `client_theme.tres` 换成 `ingame_paper_theme.tres`；
2. 删除为深色底准备的字色覆盖、深色面板 / 按钮样式覆盖；
3. 按节点名加变体：`Title*` → 标题，`Status*/Hint*/Deadline*` → 弱化，`ErrorLabel` → 错误，`Close/Cancel/No/Back*Button` → 次要，`Abandon/Discard/Dismiss*Button` → 危险；
4. 遮罩 ColorRect 换暖棕。

脚本只动视觉属性行。C# 里写死的深色底配色（例如 `e6ecea` 浅字）需要逐处改成 `PaperHudArt.Ink` 等纸艺色。

## 出图：`tests/RunInGameUiLookDev.cs`

```
godot --path . --rendering-method forward_plus --resolution 1920x1080 --script res://tests/RunInGameUiLookDev.cs -- --tag=after
```

输出到 `artifacts/ingame-ui/<tag>/`（不提交）。截图只是视觉证据，不代替真实 GUI 输入测试。
