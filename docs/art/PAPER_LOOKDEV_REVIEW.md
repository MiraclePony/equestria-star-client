# 纸艺光照与结构审核 · 本地验收 v3

生成时间：2026-10-25T04:32:39+08:00。本轮把用户外部审核整理为7阶段27项并实施，实际截图/数据见[验收页](../../artifacts/paper-v3-review.html)，逐项计划见[审核实施计划](../../../AI协作_前端美术/审核实施计划_v3.md)。没有提交、上传或更新PR。整体地图仍是本地美术验收版，下面明确列出需要人工继续制作的内容。

## 阅读审核后的判断

完整阅读了审核包的诊断、任务书、官方资料、参数说明及示例代码；49项文件清单哈希一致。原包解压在协作台外部审核目录。其AI示意图仅代表方向，中性demo没有替换真实项目。

真实工程为Godot 4.6.2 .NET，原包为4.5.1。本轮保持4.6.2、项目设置、C#装配与地图身份，复制示例rig到独立64×64子视口核验，再把方案适配到原工程。`paper_grade.gdshader`采用用户审核包的调色代码；测试用`tests/art/lookdev_reference/`保留原包脚本并调整preload。代码来源是用户交付包。

另核对4.6官方文档：[渲染器能力](https://docs.godotengine.org/en/4.6/tutorials/rendering/renderers.html)、[屏幕读取与拷贝顺序](https://docs.godotengine.org/en/4.6/tutorials/shaders/screen-reading_shaders.html)、[方向光角尺寸/PCSS](https://docs.godotengine.org/en/4.6/classes/class_light3d.html#class-light3d-property-light-angular-distance)。文档说明API和后端限制；颜色、阴影分割和偏移数值由本工程实图选择，不能由文档代替画面验收。

基线审计为1390个Mesh、1太阳、1环境、无相机Environment覆盖、无自动曝光、非物理灯光单位。**原颜色贴图已经正确标记source_color**，未发现普通纸面unshaded/自发光，不能把审核假设当已证实缺陷。实际更换的是旧全屏噪点/暗角/屏幕暖光和过强纸面纹理，并取消Glow/SSIL叠加；保留原多材质槽、UV、彩色贴纸、Alpha及源顶点色语义。数值纤维纹理不作颜色解码。

## 已安装的画面改动

- A纸艺晴天为默认，另有B清透、C午后。一个投影太阳和一盏无投影弱冷补光；固定曝光1，缩放不改太阳。Forward+保留短半径弱SSAO，Mobile与Compatibility不依赖AO/SSIL/SDFGI/体积雾。未迁移到其他引擎，使用Godot的不同渲染后端做实图校准。
- 唯一调色在CanvasLayer -10，先BackBufferCopy后一次LDR取样；HUD在其后。关闭HDR2D时运行，打开HDR2D自动旁路，鼠标Ignore。默认强度0.50、饱和度1.02、对比1.025，保护纸白，无屏幕噪点、暗角和假太阳。默认没有景深。
- 纸面使用哑光Lambert，无镜面；默认grain_strength=0。纸芯频率105→34、振幅减弱并按像素导数淡出。普通边框宽度0.020r→0.010r、地标0.038r→0.025r。
- 74棵VEG006—009专属森林树改为大折片树冠，保留树干/苔藓及主根变换；21个TER101—103山体改为少量主山脊，雪帽随新山脊重构。共95个适配节点，同一网格贯穿缩放，原GLB与blend不改。
- 依据真实地形三角面测高，98处很小的离地缝加入不透明纸根座；没有全图透明黑片，没有给云中悬浮物强加近地阴影。局部烘焙AO为后续源制作选择，本轮没有为AO全模型细分。
- 参考铁路降为低对比浅灰纸色，轨距0.045D、单轨宽0.010D、枕木宽0.18r/间隔0.24r，转折改斜接，14处可确认离开地块接收面的草稿段加下层桥面。中心线路径文件哈希不变。**仍仅离线预览，未宣称站口/分岔正式验收。**
- 检查385条原连接：188 NORMAL、86 EVERFREE、98 GAP、13 STATION_ROUTE。现有陆地邻块已接触，本地图新增连续地基为0；129条GAP/车站/云中关系保留。取消凭填空想象的区域水道，不把规则空隙自动填陆。地基工具仅为今后有证据的物理小缝准备。

纸艺光照默认：太阳#fff4e4/1.15，弱反向补光#c6cff4/0.11；阴影opacity0.90、bias0.035、normalbias0.65、blur1.6、2分割混合、最大距离42；相机near0.3/far65。2分割、4分割、Orthogonal和PCSS0.7均有同机位图，Forward+/Mobile采用2分割固定PCF，PCSS试图有颗粒感，未默认开启。Compatibility单独用Linear、太阳×0.45和常量补光0.22校准纸白；GL实际复核发现2分割有大面积斜纹自阴影，因此GL改为Orthogonal、bias0.10/normalbias2.0。修正前后图保留，不把Forward+数值照搬过去。

## 四类比例与保留范围

下面是运行时实测包围盒相对地块直径D；森林一行是B3的可见主树组，不能当成整片森林占地。继承已有33组棋子布局配置和12组0.52—0.94内容适配，没有为凑数字再整体缩放建筑。

| 参考 | 宽/D | 深/D | 高/D |
| --- | ---: | ---: | ---: |
| fashion-shop / A16 | 0.648 | 0.652 | 0.653 |
| everfree / B3 | 0.278 | 0.236 | 0.389 |
| crystal-castle / C5 | 0.382 | 0.670 | 0.995 |
| 市政厅未标准化原稿Cube.007 | 0.895* | 0.895* | 1.491* |

*市政厅以原稿母版直径2作量测参考，源包同时含另一个2×2×2物体和两个空网格，不能可靠视为已批准装配。已通过Blender只读量测并记录hide_render/viewport与modifier，未导出/安装未标准化市政厅。详情见[测量JSON](../../artifacts/paper-v3-townhall-measurement.json)。后续应由建模者确认制作物体、原点和地面后再接入。

## 原工程验收

三个机位均1760×1000、同一相机变换：总览A1六角占屏4%，区域B3占屏10%，近景时装店A16占屏23%。各有基线与新结果的无UI/有UI图。动画冻结；整体前后图包含多项改动，**不能仅凭整图声称是灯光改进**。验收页另给只改灯光、只开关调色、只开关太阳阴影、只改材质、只改折面、只改铁路权重的同机位对照。

Forward+/Mobile/Compatibility：实际4个HUD色块和一张真实角色卡共85500个通道字节，开关调色均0差异；地图平均字节变化约0.6—1，证明调色在作用但很轻。Forward+/Mobile还实测HDR2D旁路。鼠标透传、单一环境/投影太阳、无重效果依赖通过。白/灰/深蓝/紫色三预设校准图已保存，各朝向纸白仍有层次。

缩放回归通过：184个主植被根、138格位置、落点和点击身份保持；铁路范围/中心线保持；主轮廓没有随距离换模。冷/迟到/失败加载、预取、边界滞回、缓存回收、HUD占屏、宽屏、常驻玩法提示和装饰开关通过。测试胶囊为占位体，A1→A2示例移动与138格净空零预警；不能等同真实棋子、所有路线及在线多人验收。

构建0警告0错误；Godot导入、GameMap/HexCell/OrigamiArt/GameScreen/EnvironmentArt和静态检查通过；421个GLB的哈希、内嵌资源、结构与所有源哈希通过。源索引v005无变化，保留候选状态，本轮未重复导出素材。

## 本机帧时和资源

RTX 5000 Ada Laptop、Windows、同尺寸同机位，单独运行；每机位预热90帧，v3采样120帧，基线90帧。CPU/GPU为Godot视口渲染计时，帧间隔含脚本、抓图/断言和调度，不应相加。显存为引擎监测值而非操作系统整卡占用。表格记录实际数据，未承诺手机/浏览器性能或稳定60FPS。

| 渲染器 | 机位 | CPU P95/P99 ms | GPU P95/P99 ms | 帧间隔 P95/P99 ms | 显存MiB | 无UI draw |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| forward_plus | overview | 6.51 / 8.48 | 1.88 / 1.89 | 11.89 / 14.98 | 390.5 | 4328 |
| forward_plus | region | 10.50 / 13.95 | 2.99 / 3.01 | 21.77 / 23.69 | 403.1 | 3309 |
| forward_plus | close | 3.56 / 4.18 | 1.81 / 1.82 | 13.74 / 16.41 | 403.2 | 2272 |
| mobile | overview | 5.68 / 6.16 | 1.73 / 1.80 | 13.02 / 19.17 | 251.8 | 1942 |
| mobile | region | 5.90 / 6.88 | 1.70 / 1.89 | 14.88 / 21.63 | 264.4 | 1354 |
| mobile | close | 2.91 / 3.03 | 1.75 / 1.76 | 9.10 / 9.40 | 264.5 | 698 |
| gl_compatibility | overview | 16.80 / 22.68 | 11.55 / 14.66 | 19.14 / 26.91 | 150.0 | 9093 |
| gl_compatibility | region | 12.80 / 15.57 | 6.18 / 6.56 | 15.37 / 17.44 | 150.6 | 6342 |
| gl_compatibility | close | 7.46 / 8.95 | 1.51 / 2.14 | 10.36 / 11.83 | 150.7 | 3716 |

原v2同机位基线：

| 机位 | CPU P95/P99 ms | GPU P95/P99 ms | 帧间隔 P95/P99 ms | 显存MiB |
| --- | ---: | ---: | ---: | ---: |
| overview | 8.35 / 10.47 | 1.78 / 1.80 | 13.72 / 17.25 | 402.7 |
| region | 6.44 / 8.65 | 3.04 / 3.18 | 13.83 / 21.05 | 402.8 |
| close | 3.91 / 5.37 | 2.65 / 3.13 | 24.13 / 25.41 | 402.8 |

区域CPU尾部本轮高于单次基线，未把视觉修改包装成全面性能提升。基线draw采在UI之后，不能直接和v3无UI draw算降幅；保留原JSON便于复测。连续缩放整段CPU P95/P99为5.45/7.21ms、GPU为3.27/4.71ms、帧间隔为15.02/24.03ms，初始构建1095ms；这些仍提示后续需要实例与加载预算优化。

## 失败记录及修正

首次像素测试错误地采了色块间隙，修正为4个精确矩形后0差异；失败JSON/日志保留。旧共享材质显式Dispose与测试中原地切Shader曾引发退出期重复错误，改为隔离重建对照及退出期丢引用/回收，运行中不强制GC。失败大日志压缩保存，不覆盖事实。

Compatibility曾在退出报告每张349524字节纹理残留；禁止创建随后立即丢弃的天空辐射资源后，最新测试和实际Gallery均正常结束。修复前日志`paper-v3-gl-gallery-before-sky-fix.log`及`paper-v3-gl-exit-before-cleanup.log`保留。实图检查另发现GL大面积阴影斜纹，独立Orthogonal及偏移校准消除了平地/海面条纹；此问题单靠脚本passed不能检出。修正前`paper-v3-gl-shadow-acne-before.png`与修正后`paper-v3-gl-shadow-orthogonal-test.png`均保留。最终三渲染器验收、缩放与实际预览日志无脚本/Shader/退出资源错误；这不是跨设备无问题保证。

## 仍需人工美术继续的明确问题

1. 以下13条规则连接保留原样，但物理站台端口尚未制作/测定。先确认入口/出口位置、切线、高度和轨距，再验收转弯、分岔、桥隧模块；不能用站点直线宣布完成正式铁路。
2. 市政厅源装配须标准化。屋檐背面、门窗纸厚和局部AO需要逐件建模审核，当前运行时折面适配没有替代源建模工作。
3. 皇城后山平台、马哈顿港湾、骏鹰海岸和各区山脉走势仍是背景提案；外围基底轮廓可进一步自然化。原图地理关系不能由填空推断。
4. 真实棋子尺寸/动画、多棋子站位和所有连边移动待资源与玩法接入；当前胶囊只验证候选净空。
5. 默认阴影仍可在极近处看到PCF采样与地形交界；GL的细山面还有少量自阴影采样，整体软度弱于Forward+，值得在目标设备上继续调试。基础图同步构建、区域CPU尾部和数千draw需要后续批量实例/缓存优化；没有手机或发行包实测。

| 起点 | 终点 | 物理端口状态 |
| --- | --- | --- |
| A15 小马谷火车站 | F4 中心城火车站 | 未测物理端口 |
| A15 小马谷火车站 | K9 彩虹瀑布 | 未测物理端口 |
| F4 中心城火车站 | E8 马哈顿火车站 | 未测物理端口 |
| F4 中心城火车站 | G5 巴尔的马 | 未测物理端口 |
| E8 马哈顿火车站 | D1 d区普通格 | 未测物理端口 |
| E8 马哈顿火车站 | C10 水晶帝国 | 未测物理端口 |
| C10 水晶帝国 | L8 云中城 | 未测物理端口 |
| C10 水晶帝国 | C1 耗耗斯坦 | 未测物理端口 |
| L8 云中城 | K9 彩虹瀑布 | 未测物理端口 |
| K9 彩虹瀑布 | J4 苹果鲁萨 | 未测物理端口 |
| J4 苹果鲁萨 | I1 i区普通格 | 未测物理端口 |
| I1 i区普通格 | H4 h区普通格 | 未测物理端口 |
| H4 h区普通格 | G5 巴尔的马 | 未测物理端口 |

## 27项证据表

“完成”表示本轮可安全实施部分已落地且限制已记录，不能将未测端口或未批准市政厅解读为已完成制作。

| 计划项 | 实际结果/边界 | 原始证据 |
| --- | --- | --- |
| 1.1 | Godot 4.6.2 .NET、RTX 5000 Ada；非物理单位、HDR2D关闭 | [paper-v3-baseline-audit.json](../../artifacts/paper-v3-baseline-audit.json) |
| 1.2 | 113文件指纹/原改动快照；仅建分支引用，没有切换或覆盖 | [paper-v3-change-manifest.json](../../artifacts/paper-v3-change-manifest.json) |
| 1.3 | 只读材质、灯光、环境、相机审计 | [paper-v3-baseline-audit.json](../../artifacts/paper-v3-baseline-audit.json) |
| 1.4 | 三个固定机位，基线有/无UI各3张 | [paper-v3-baseline.json](../../artifacts/paper-v3-baseline.json) |
| 2.1 | 最终1环境、1投影太阳、1无阴影弱补光，无相机覆盖 | [paper-v3-forward_plus-audit.json](../../artifacts/paper-v3-forward_plus-audit.json) |
| 2.2 | 原albedo已有source_color；保留UV/多材质/Alpha，补原顶点色开关 | [paper-v3-forward_plus-material-new.png](../../artifacts/paper-v3-forward_plus-material-new.png) |
| 2.3 | 普通纸面不发光、不unshaded；未重复烘AO或全局乘暗 | [paper-v3-baseline-audit.json](../../artifacts/paper-v3-baseline-audit.json) |
| 2.4 | 单轻调色、显式拷屏、HDR2D旁路、鼠标透传 | [paper-v3-forward_plus-acceptance.json](../../artifacts/paper-v3-forward_plus-acceptance.json) |
| 3.1 | A/B/C预设及白灰/蓝紫校准 | [paper-v3-forward_plus-calibration-0.png](../../artifacts/paper-v3-forward_plus-calibration-0.png) |
| 3.2 | Forward+仅弱SSAO；Mobile/Compatibility无高级依赖 | [paper-v3-mobile-acceptance.json](../../artifacts/paper-v3-mobile-acceptance.json) |
| 3.3 | 实图比较2/4分割/Orthogonal/PCSS，选2分割固定PCF | [paper-v3-forward_plus-shadow-Parallel2Splits.png](../../artifacts/paper-v3-forward_plus-shadow-Parallel2Splits.png) |
| 3.4 | 曝光1、世界光固定；near0.3/far65；白纸不同朝向可读 | [paper-v3-forward_plus-calibration-0.png](../../artifacts/paper-v3-forward_plus-calibration-0.png) |
| 4.1 | 按地形三角面测接收高度，98个小树根纸座 | [paper-v3-forward_plus-acceptance.json](../../artifacts/paper-v3-forward_plus-acceptance.json) |
| 4.2 | 不透明小纸座和短半径AO，无整片透明黑影或全模型细分 | [paper-v3-forward_plus-close-clean.png](../../artifacts/paper-v3-forward_plus-close-clean.png) |
| 4.3 | 云中区域排除贴地纸座；保留透明贴纸 | [paper-v3-forward_plus-region-clean.png](../../artifacts/paper-v3-forward_plus-region-clean.png) |
| 5.1 | 铁路降权、斜接转弯、14草稿桥面；13端口连接列人工核对 | [paper-v3-rail-gap-audit.json](../../artifacts/paper-v3-rail-gap-audit.json) |
| 5.2 | 正常陆地相邻已相接，实际补0条；129条规则/云中关系保留 | [paper-v3-rail-gap-audit.json](../../artifacts/paper-v3-rail-gap-audit.json) |
| 5.3 | 时装店/森林/水晶实景尺寸；市政厅只读量测未批准原稿 | [paper-v3-townhall-measurement.json](../../artifacts/paper-v3-townhall-measurement.json) |
| 5.4 | 74专属树冠和21山体共95永久适配，184主植被根不动 | [paper-v3-forward_plus-geometry-folded-only.png](../../artifacts/paper-v3-forward_plus-geometry-folded-only.png) |
| 5.5 | 细边框、低对比纸芯、默认纸纹0、无屏幕噪点 | [paper-v3-forward_plus-overview-clean.png](../../artifacts/paper-v3-forward_plus-overview-clean.png) |
| 6.1 | 原工程灯光/材质/几何/轨道独立A/B，另有阴影/滤镜开关 | [paper-v3-review.html](../../artifacts/paper-v3-review.html) |
| 6.2 | 三渲染器4色块+真实卡面0字节差；HDR2D旁路通过 | [paper-v3-forward_plus-acceptance.json](../../artifacts/paper-v3-forward_plus-acceptance.json) |
| 6.3 | 138格/184主植被、落点/点击/提示/预取/缓存/滞回通过 | [zoom-acceptance.json](../../artifacts/zoom-acceptance.json) |
| 6.4 | 三渲染器同机位帧时P95/P99、显存、draw；不当成手机认证 | [paper-v3-forward_plus-acceptance.json](../../artifacts/paper-v3-forward_plus-acceptance.json) |
| 7.1 | 本报告、27项表、文件清单、参数和人工美术项 | [paper-v3-change-manifest.json](../../artifacts/paper-v3-change-manifest.json) |
| 7.2 | 三机位/UI、单变量对照、JSON、成功与失败日志、入口脚本 | [paper-v3-review.html](../../artifacts/paper-v3-review.html) |
| 7.3 | 协作台/交接和本地预览；最终引用检查无提交上传 | [paper-v3-git-refs.txt](../../artifacts/paper-v3-git-refs.txt) |

## 文件变更与复现

运行根目录`验收纸艺审核版.ps1`进入交互预览；`-PaperPreset 1/2`切B/C，`-Quality Mobile/Compatibility`切渲染后端；`-SkipBuild`复用现有构建。预览内可切光照/调色/背景/装饰/镜头并旋转地标。`-RunChecks`执行三后端完整验收及缩放，日志在artifacts。原始截图未裁切代替，页面可点开1760×1000原图。

v3改动相对本地v2快照（其中包含尚未提交的旧轮次成果，不应统称本轮新增）：

- `assets/shaders/physical_paper.gdshader`
- `docs/art/CONCEPT_OPTIMIZATION.md`
- `docs/art/FOREST_LIGHTING_REVIEW.md`
- `docs/art/INTEGRATION.md`
- `docs/art/LOCAL_REVIEW.md`
- `docs/art/ZOOM_ACCEPTANCE.md`
- `scenes/game/map/GameMapScene.tscn`
- `scripts/art/ArtRenderProfile.cs`
- `scripts/art/ConceptPaperStyle.cs`
- `scripts/art/OrigamiGallery.cs`
- `scripts/art/PaperAtmosphere.cs`
- `scripts/art/ReferenceRailDraft.cs`
- `scripts/game/map/ContinentalSceneryBuilder.cs`
- `scripts/game/map/DetailAssetCache.cs`
- `scripts/game/map/EnvironmentArtCatalog.cs`
- `scripts/game/map/GameMapBuilder.cs`
- `scripts/game/map/OrigamiArtCatalog.cs`
- `scripts/game/map/OrigamiMapPresentation.cs`
- `scripts/game/map/ZoomArtDetailController.cs`
- `打开美术预览.ps1`

新模块：`PaperFoldGeometry`、`PaperGroundContact`、`PaperSceneAudit`、`BoardSeamFoundations`，新调色Shader、保留的legacy对照Shader、两份审计/验收测试、用户参考rig测试目录、只读市政厅量测脚本、本报告生成器、审核版入口和本报告。完整列表见[变更清单](../../artifacts/paper-v3-change-manifest.json)。默认材质不会再上传无用的噪点纹理，退出时共享材质不作重复Dispose。

当前分支仍`art/origami-integration`，HEAD `d6800bd531c3b72e6194086a69a5855754f3fd7f`。仅新建`art/papercraft-lookdev-v3`引用指向原HEAD，未切分支。113文件基线与原有diff保存在`.art-tools/lookdev-v3-baseline/`；本地未提交改动不能靠该引用恢复，应使用文件快照，禁止reset/checkout覆盖现场。upstream/main与origin/main只读fetch后均dd708216，origin美术分支仍d6800bd。**没有本轮提交、push、PR更新或自动合并。**
