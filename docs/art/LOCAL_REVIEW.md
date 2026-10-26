> 历史报告：最新为 [纸艺审核v3](PAPER_LOOKDEV_REVIEW.md)，实际对照页在 artifacts/paper-v3-review.html；旧数值与旧清理方法不能覆盖v3。

# 本地概念图适配 · 2026-10-24

最新专属森林、单套完整模型和Forward+桌面光影见 [FOREST_LIGHTING_REVIEW.md](FOREST_LIGHTING_REVIEW.md)，截图页 `artifacts/forest-lighting-review.html`。下方保留此前静态适配过程。

2026-10-25 更新：已完成连续缩放第一版及 BG101—BG109 新素材接入，环境现为 60 项／306 GLB，总计 421 GLB。入口 `验收缩放版.ps1`，结果与局限见 [ZOOM_ACCEPTANCE.md](ZOOM_ACCEPTANCE.md)，新截图页 `artifacts/zoom-review.html`。以下保留前一轮静态适配记录；51 项／372 GLB 和「尚未做细节切换」不代表最新状态。未提交、未上传。

最新背景与渲染优化已追加，详见[CONCEPT_OPTIMIZATION.md](CONCEPT_OPTIMIZATION.md)。当前实图为`artifacts/background-mobile.png`；新增三只热气球、五个独立纸云、变化海岸及两档渲染，拍照景深可关闭。下方保留首轮概念适配过程与棋子参数。

本轮未提交、未上传。远端草稿仍为首轮 d6800bd；成功读取upstream/origin，无新增变更。源文件哈希未变，本轮没有重复导出。保留原43组115个地标GLB，以及51项257个环境GLB。

## 查看

运行项目根目录 `打开美术预览.ps1`，默认进入50号概念构图。`-View 1`查看完整地图；45/46/47为小马谷/森林/雪域，48为铁路样件，49为水域端口试拼。可缩放、拖动、点击地标试转60°并保存本地朝向。

本机对照页：`artifacts/local-art-review.html`。`artifacts/concept-final.png`是Godot 4.6.2 Mobile/Vulkan实际渲染，不是生成图；截图没有后期拼接。artifacts目录忽略Git和Godot导入。

## 已改内容

- 按用户原概念图调整12区颜色：橄榄草地、深绿森林、淡紫雪域、赭黄荒漠、灰色城市、青蓝区域，增加纸白/金色边框和分层纸芯。
- 世界空间纸纤维、微法线和粗糙度保留原折面及透明印花；不改写GLB或贴纸UV，不冒称已烘焙Blender全部材质。
- 镜头降至约40.5°并增加轻微侧角，暖侧光搭配冷环境补光；减少调色泛白，HUD仍在滤镜之后。
- 105格地区植被/散件。森林每格1—3棵，以深绿松树为主，少量原森林树形；保留原低矮装饰，减少花朵，雪域使用独立雪松。
- 山群大小、转角、高度错落，降低丘陵荧光绿；复用雪山、紫山、台地、断崖、湖泊WAT009、瀑布WAT008、林缘、帆船、云、纸鸟及热气球。区域实际间隙补水面，没有移动玩法格子。

## 棋子适配

`assets/art/token-layout.json`保存138格候选落点、33组地标指纹和变换。源GLB更新后旧地标配置不会直接套用。21组保持原比例，12组缩放Building与Decoration至0.52—0.94，必要时平移；Terrain、原制作源与分件层级保持不变。预览开关可恢复原版比例。

身体半宽0.22r、高0.72r，从地面上0.12r开始检测。使用实际三角面/包围盒SAT和向上屋顶射线。此前33处整组AABB预警，精查为31处原格心相交，两处空院落属于误报。适配后138落点均未触发当前检查。

A1→A2是实际NORMAL边，示例采用局部网格绕行、连续线段采样和平滑播放；修复落点取整到障碍格造成路径失败。25个播放采样通过，规划线段采样间隔不超过0.09r。报告：`artifacts/token-clearance.json`。

这只是美术体积和示例移动检查，尚未覆盖全部连边、地面支撑、河沟跨越、实际角色物理或多人站位，不是生产移动系统。

## 铁路和水系

已有13条STATION_ROUTE端点证据，原铁路包仍缺最终模型。新增原纸质棋盘白线的人工描线草稿，奶油双轨/粉金枕木只用于离线构图审阅，可关闭；不会进入实时GameMapBuilder或改变服务器路线。站口、转角半径、列车净空、桥隧和接缝仍待核对。

49号视图按真实WAT006→WAT005→WAT010端口位置、方向及水位试拼；湖泊/瀑布仍是背景提案，大陆正式水系尚未完成。

## 验证与差距

C#构建0警告0错误；Godot导入、静态检查、HexCell、GameMap、OrigamiArt、GameScreen和EnvironmentArt测试通过。覆盖真实资源、坐标/点击身份、候选回退、确定性布置、水域端口、开放院落/实体墙/屋顶、绕行及138落点。日志中的模拟网络失败和双视觉源警告为既有测试故意触发的分支。

372个GLB的容器、内嵌依赖、有限坐标、产物/源文件SHA256校验通过。原地标1,000,979三角形，环境52,551三角形。实际查看了概念构图、小马谷、森林、雪域、棋子和滤镜对照，无Shader错误。

部分建筑的轮廓和细节仍比原概念简化；山群、岸线尚需自然化，完整铁路、水系、实际棋子、手机性能及发布包尚未完成。技术通过不代表美术定稿。继续待办见[INTEGRATION.md](INTEGRATION.md)。

## 复现

执行 `tools/art/check_sync.ps1 -SourceRoot 'I:/blender文件/小马国之心'` 与 `dotnet build --nologo`。用本机Godot .NET截图参数：

```text
--path . --resolution 1760x1000 res://scenes/art/OrigamiGallery.tscn -- --art-view 50 --art-clean --art-capture artifacts/concept-final.png
```

`--art-source-layout`关闭落点/比例适配；`--art-baseline`关闭环境及调色；`--art-no-grade`只关闭调色；`--art-clearance`显示检查体。50号视图默认显示铁路草稿，可用开关关闭；其余视图默认不显示。

制作方法参考：[Godot后处理](https://docs.godotengine.org/en/4.6/tutorials/shaders/custom_postprocessing.html)、[屏幕纹理](https://docs.godotengine.org/en/4.6/tutorials/shaders/screen-reading_shaders.html)。此前研究了作者的[水彩Shader](https://godotshaders.com/shader/watercolor-light-shader/)，当前纸材质与调色均为原创实现，没有下载未知许可证滤镜。
