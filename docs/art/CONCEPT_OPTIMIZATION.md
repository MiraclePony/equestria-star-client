> 历史报告：最新为 [纸艺审核v3](PAPER_LOOKDEV_REVIEW.md)，实际对照页在 artifacts/paper-v3-review.html；旧数值与旧清理方法不能覆盖v3。

# 概念图核对与优化记录 · 2026-10-24

最新桌面方案已切换Forward+，无尽森林全部使用专属树；使用一套完整模型贯穿缩放，详见 [FOREST_LIGHTING_REVIEW.md](FOREST_LIGHTING_REVIEW.md)。下方Mobile默认及LOD建议是历史选型，最新授权与策略以上述说明为准。

2026-10-25 更新：在本轮持续工作期间发现 v005 新增九项天空素材，已增量导出 49 分件，星纹气球、立体卷云和纸鸟完成大陆试排；环境库现为 60 项／306 GLB。连续缩放、主树不变、异步细节与独立信息层已完成第一版本地验收，详见 [ZOOM_ACCEPTANCE.md](ZOOM_ACCEPTANCE.md)。以下「无新模型」及性能数字是上一轮记录，不是当前状态；引擎比较仍是当时的选型判断。

本轮仅本地修改，没有提交、上传或更新远端PR。参考用户重发的最早概念图；当前实际画面见本机 `artifacts/background-mobile.png`，对照页为 `artifacts/local-art-review.html`。

## 素材检查

重新扫描工作区的blend/glb/fbx/obj源文件，并读取交付索引。最新制作文件仍为19:20的WAT011分件，环境索引仍为v004：51项可用、9项待补。没有发现未收录的独立“模型.blend”，也没有索引内漏装的可用资产；`check_sync.ps1`检查原地标和环境源文件哈希均未变化。

因此没有重复导出已有模型，继续复用43组115个地标GLB与51项257个环境GLB。upstream/origin读取成功，远端分支未变。

## 对照概念图后执行的改动

| 原问题 | 本轮实际处理 | 尚需继续 |
| --- | --- | --- |
| 热气球偏少，在近景被裁掉 | 复用BG004，改为左侧主气球、右侧小气球、后方远景气球，共三只，加入轻微漂浮 | 可再做配色变体，避免同源造型重复 |
| 周边天空单调 | 增加五个独立BG003纸云，校正薄纸云的朝向，避免呈现成窄条；不向棋盘投影 | 更自然的云层远近与风向组合 |
| 海岸像等宽台阶 | 沿实际96点轮廓生成宽窄变化的沙坡、薄白岸线、浅水折面，复用DEC103岸边散石 | 海湾轮廓、河口、岩岸与湖岸仍需连贯设计 |
| 画面颗粒与阴影不够舒服 | 纸纹颗粒减弱，左上暖光增强，少量高光Glow；提高环境补光，软化常规阴影 | 有方向的真实纸纤维、折痕和建筑AO仍值得烘焙 |
| 建筑接地较弱 | 增加可选Forward+桌面档的接触AO；复制环境资源，避免不同场景设置互相污染 | 逐建筑校准AO半径，实机性能评估 |
| 小物阴影成本与噪声 | 85个低矮小装饰不再投影；保留网格、材质、受光和三角面净空检查，树木与建筑仍投影 | 重复植被批处理、LOD和材质整合 |
| 缺少微缩摄影感 | 新增可关闭的拍照景深，中心棋盘清楚，远近边缘柔化 | 景深不默认用于对局，避免妨碍格子辨认 |

PCSS可变半影实测在小纸树上出现点状噪声，最终未启用；采用固定阴影过滤。没有为了堆效果开启SDFGI、反射或体积雾。

## 引擎与渲染选择

以下功能依据官方文档，迁移成本和当前选择是本项目的判断，没有实际迁移或声称测过Unity/Unreal性能。

| 方案 | 适用性与选择 |
| --- | --- |
| Godot Mobile | 继续作为默认。支持当前纸材质、MSAA、Glow和拍照景深；不支持SSAO。适合先控制材质、模型与阴影开销。[Godot渲染器](https://docs.godotengine.org/en/4.6/tutorials/rendering/renderers.html)、[SSAO说明](https://docs.godotengine.org/en/4.6/classes/class_environment.html#class-environment-property-ssao-enabled) |
| Godot Forward+ | 已提供可选桌面档。可用SSAO增强缝隙和接地，代价是更多渲染工作与显存。当前没有充分依据把所有平台切到此档。[官方说明](https://docs.godotengine.org/en/4.6/tutorials/rendering/renderers.html) |
| Godot Compatibility | 可作为低端硬件的后续回退候选；颜色和后处理实现需单独复核，本轮没有新增这一路的验收。 |
| Unity URP / HDRP | 官方表中URP覆盖移动端，HDRP不支持Android/iOS；本项目迁移还要重接C#界面、地图、Shader与资源管线。现有问题以构图、模型细节、贴图与性能预算为主，暂不选择迁移。[Unity 6管线对照](https://docs.unity3d.com/cn/6000.0/Manual/render-pipelines-feature-comparison.html) |
| Unreal / Lumen | 可作为单独高规格展示场景的候选。高端Android的Lumen仍标为实验性；它不能代替纸艺模型细节，也不适合作为当前移动端优化捷径。[Epic移动端Lumen](https://dev.epicgames.com/documentation/en-us/unreal-engine/using-lumen-global-illumination-on-mobile-in-unreal-engine) |

本机RTX 5000 Ada、1760×1000、同一概念构图，最终场景统计：Mobile约230MiB、1064次绘制；Forward+约353MiB、5862次绘制。两档包含的Pass不同，不能把绘制数比例等同于GPU耗时比例。移除微小阴影后，Forward+引擎统计绘制数从6070降至5862；同时移除了气球投影，因此此差值属于组合优化结果。

统计经过120帧预热、60帧CPU采样，开启VSync；CPU数据有本机调度波动，不据此宣传帧率提升，也不代表手机结果。原始记录在 `artifacts/background-*-stats.json`。

## 按优先级继续优化

1. **地标细节与比例**：部分现有城堡、车站和城市仍比概念简化；12组缩放适配应结合真实棋子重新审阅，避免小城堡失去视觉中心。优先保留主体轮廓，必要时另存前庭/入口适配副本。
2. **真实大陆水系**：利用已交付河段、瀑布、河口建立连贯水位与岸高。WAT011没有无缝吸附验收，不能将自由摆放当成完整岸线拼接。当前南侧池塘仍为背景提案。
3. **正式铁路与道路语言**：核对13条STATION_ROUTE、沿途格和站口，解决桥隧及跨区高差。现在的轨道仍仅为离线描线草稿。
4. **材质与暖光**：烘焙纸纤维、折痕和局部AO，保留透明贴纸；光照与材质应先于更强的滤镜。当前柔光和景深不能补回缺失的模型细节。
5. **性能**：按真机测量再做植被MultiMesh、细碎材质合批、GLB自动/手工LOD与低端画质档，避免为了少量远景细节维持全部高面数。
6. **棋子/UI**：扩大通道验证到其他边、地面支撑和多人站位；继续卡栏、纸艺图标、选中与移动反馈。

## 运行与检查

```powershell
./打开美术预览.ps1 -Quality Mobile
./打开美术预览.ps1 -Quality Desktop
./打开美术预览.ps1 -Quality Desktop -Photo
./打开美术预览.ps1 -View 1
```

默认不启用拍照景深。渲染器需要启动时选择；预览内可单独关闭调色、景深、铁路草稿和适配。

本轮C#构建0警告0错误；Godot导入、静态检查、EnvironmentArt、GameMap、GameScreen通过。138个当前候选落点及A1→A2示例绕行仍为零预警。已查看两档实际渲染、桌面近景、景深和完整大陆；没有Shader错误。原制作源和原GLB未改写，没有上传。
