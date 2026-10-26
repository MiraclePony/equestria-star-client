"""Build the local v3 evidence index from actual Godot reports; no Git mutation/network."""
from pathlib import Path
from datetime import datetime, timezone, timedelta
from collections import Counter
import hashlib
import html
import json
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'artifacts'
HUB = ROOT.parent / 'AI协作_前端美术'
NOW = datetime.now(timezone(timedelta(hours=8))).isoformat(timespec='seconds')

def read(path):
    return json.loads((ROOT/path).read_text(encoding='utf-8-sig'))

def write(path, value):
    path.write_text(value, encoding='utf-8')

def dump(path, value):
    write(path, json.dumps(value, ensure_ascii=False, indent=2)+'\n')

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT).decode('utf-8').strip()

reports = {r:read(f'artifacts/paper-v3-{r}-acceptance.json') for r in ['forward_plus','mobile','gl_compatibility']}
assert all(r['status']=='passed' for r in reports.values())
zoom = read('artifacts/zoom-acceptance.json')
assert zoom['status']=='passed'
baseline = read('artifacts/paper-v3-baseline.json')
for report in reports.values():
    for before, after in zip(baseline['cases'], report['cases']):
        assert before['cameraTransform']==after['cameraTransform'] and before['cameraSize']==after['cameraSize']
    assert all(p['differentBytes']==0 for p in report['pixelChecks'] if 'differentBytes' in p)

snapshot=read('.art-tools/lookdev-v3-baseline/file-hashes.json')
changed=[p for p,h in snapshot.items() if (ROOT/p).exists() and sha(ROOT/p)!=h]
new_scope=[
    'scripts/art/PaperFoldGeometry.cs','scripts/art/PaperGroundContact.cs','scripts/art/PaperSceneAudit.cs',
    'scripts/game/map/BoardSeamFoundations.cs','assets/shaders/paper_grade.gdshader',
    'assets/shaders/legacy_physical_paper.gdshader','assets/shaders/legacy_warm_paper.gdshader',
    'tests/CapturePaperBaseline.cs','tests/RunPaperLookAcceptance.cs',
    'tests/art/lookdev_reference','tools/art/measure_townhall_reference.py',
    'tools/art/build_paper_review.py','验收纸艺审核版.ps1','docs/art/PAPER_LOOKDEV_REVIEW.md',
    '../AI协作_前端美术/审核实施计划_v3.md',
]
manifest={'createdAtShanghai':NOW,'head':git('rev-parse','HEAD'),'workingBranch':git('branch','--show-current'),
    'baselineSnapshot':'.art-tools/lookdev-v3-baseline','snapshotFiles':len(snapshot),
    'changedSinceV2Snapshot':changed,'v3NewOrEvidenceFiles':new_scope,
    'sourcePolicy':'Original blend and all 421 GLB binaries unchanged. New fold geometry is a runtime presentation adaptation, one mesh at every zoom.',
    'gitPolicy':'No commit, push, PR update or merge. Existing uncommitted work preserved.'}
dump(OUT/'paper-v3-change-manifest.json',manifest)

map_path='tests/fixtures/main-map-20261024.json'
map_data=read(map_path)['data']
cells={c['cellId']:c for c in map_data['cells']}
inv=reports['forward_plus']['inventory']
trace_path='assets/art/reference-rail-draft.json'
trace=read(trace_path)
assert sha(ROOT/trace_path)==snapshot[trace_path]
ports=[]
for edge in map_data['edges']:
    if edge['edgeType']!='STATION_ROUTE':continue
    a,b=cells[edge['fromCellId']],cells[edge['toCellId']]
    ports.append({'from':a['cellCode'],'fromName':a['cellName'],'to':b['cellCode'],'toName':b['cellName'],
        'status':'logical-edge-preserved; physical-station-ports-not-authored',
        'required':'Survey station assembly entry/exit position, tangent, height and gauge; verify switch/bridge/tunnel modules against the traced route.'})
rail={'createdAtShanghai':NOW,'mapCells':len(cells),'mapEdges':len(map_data['edges']),
    'edgeTypes':dict(Counter(e['edgeType'] for e in map_data['edges'])),
    'fixtureSha256':sha(ROOT/map_path),'traceSha256':sha(ROOT/trace_path),
    'traceUnchangedFromV2Snapshot':True,'traceRoutes':len(trace['routes']),
    'visibleSegmentPairs':len(inv['railCentreline'])//2,'draftGapDeckSpans':inv['railDeckSpans'],
    'filledLandEdges':inv['filledEdges'],'reservedEdges':inv['reservedEdges'],
    'reservedTypes':dict(Counter(x.split(':')[-1] for x in inv['reservedEdges'])),
    'stationConnectionsToSurvey':ports,
    'note':'13 gameplay station edges are not physical railway splines. The gallery trace is offline only. No formal ports, new routes, coastline or land connection is inferred.'}
dump(OUT/'paper-v3-rail-gap-audit.json',rail)

def performance_rows():
    rows=[]
    for method,report in reports.items():
        for c in report['cases']:
            rows.append(f"| {method} | {c['name']} | {c['cpuP95']:.2f} / {c['cpuP99']:.2f} | {c['gpuP95']:.2f} / {c['gpuP99']:.2f} | {c['frameP95']:.2f} / {c['frameP99']:.2f} | {c['memory']/1048576:.1f} | {c['drawsClean']} |")
    return '\n'.join(rows)

def baseline_rows():
    return '\n'.join(f"| {b['name']} | {b['cpuP95']:.2f} / {b['cpuP99']:.2f} | {b['gpuP95']:.2f} / {b['gpuP99']:.2f} | {b['frameP95']:.2f} / {b['frameP99']:.2f} | {b['memory']/1048576:.1f} |" for b in baseline['cases'])

evidence=[
('1.1','Godot 4.6.2 .NET、RTX 5000 Ada；非物理单位、HDR2D关闭','paper-v3-baseline-audit.json'),
('1.2','113文件指纹/原改动快照；仅建分支引用，没有切换或覆盖','paper-v3-change-manifest.json'),
('1.3','只读材质、灯光、环境、相机审计','paper-v3-baseline-audit.json'),
('1.4','三个固定机位，基线有/无UI各3张','paper-v3-baseline.json'),
('2.1','最终1环境、1投影太阳、1无阴影弱补光，无相机覆盖','paper-v3-forward_plus-audit.json'),
('2.2','原albedo已有source_color；保留UV/多材质/Alpha，补原顶点色开关','paper-v3-forward_plus-material-new.png'),
('2.3','普通纸面不发光、不unshaded；未重复烘AO或全局乘暗','paper-v3-baseline-audit.json'),
('2.4','单轻调色、显式拷屏、HDR2D旁路、鼠标透传','paper-v3-forward_plus-acceptance.json'),
('3.1','A/B/C预设及白灰/蓝紫校准','paper-v3-forward_plus-calibration-0.png'),
('3.2','Forward+仅弱SSAO；Mobile/Compatibility无高级依赖','paper-v3-mobile-acceptance.json'),
('3.3','实图比较2/4分割/Orthogonal/PCSS，选2分割固定PCF','paper-v3-forward_plus-shadow-Parallel2Splits.png'),
('3.4','曝光1、世界光固定；near0.3/far65；白纸不同朝向可读','paper-v3-forward_plus-calibration-0.png'),
('4.1','按地形三角面测接收高度，98个小树根纸座','paper-v3-forward_plus-acceptance.json'),
('4.2','不透明小纸座和短半径AO，无整片透明黑影或全模型细分','paper-v3-forward_plus-close-clean.png'),
('4.3','云中区域排除贴地纸座；保留透明贴纸','paper-v3-forward_plus-region-clean.png'),
('5.1','铁路降权、斜接转弯、14草稿桥面；13端口连接列人工核对','paper-v3-rail-gap-audit.json'),
('5.2','正常陆地相邻已相接，实际补0条；129条规则/云中关系保留','paper-v3-rail-gap-audit.json'),
('5.3','时装店/森林/水晶实景尺寸；市政厅只读量测未批准原稿','paper-v3-townhall-measurement.json'),
('5.4','74专属树冠和21山体共95永久适配，184主植被根不动','paper-v3-forward_plus-geometry-folded-only.png'),
('5.5','细边框、低对比纸芯、默认纸纹0、无屏幕噪点','paper-v3-forward_plus-overview-clean.png'),
('6.1','原工程灯光/材质/几何/轨道独立A/B，另有阴影/滤镜开关','paper-v3-review.html'),
('6.2','三渲染器4色块+真实卡面0字节差；HDR2D旁路通过','paper-v3-forward_plus-acceptance.json'),
('6.3','138格/184主植被、落点/点击/提示/预取/缓存/滞回通过','zoom-acceptance.json'),
('6.4','三渲染器同机位帧时P95/P99、显存、draw；不当成手机认证','paper-v3-forward_plus-acceptance.json'),
('7.1','本报告、27项表、文件清单、参数和人工美术项','paper-v3-change-manifest.json'),
('7.2','三机位/UI、单变量对照、JSON、成功与失败日志、入口脚本','paper-v3-review.html'),
('7.3','协作台/交接和本地预览；最终引用检查无提交上传','paper-v3-git-refs.txt'),
]
table='\n'.join(f'| {n} | {desc} | [{file}](../../artifacts/{file}) |' for n,desc,file in evidence)
port_table='\n'.join(f"| {p['from']} {p['fromName']} | {p['to']} {p['toName']} | 未测物理端口 |" for p in ports)
proportions='\n'.join(f"| {c['id']} / {c['cell']} | {c['widthOverDiameter']:.3f} | {c['depthOverDiameter']:.3f} | {c['heightOverDiameter']:.3f} |" for c in inv['proportions'])
change_list='\n'.join('- `'+p+'`' for p in sorted(changed))

doc=f'''# 纸艺光照与结构审核 · 本地验收 v3

生成时间：{NOW}。本轮把用户外部审核整理为7阶段27项并实施，实际截图/数据见[验收页](../../artifacts/paper-v3-review.html)，逐项计划见[审核实施计划](../../../AI协作_前端美术/审核实施计划_v3.md)。没有提交、上传或更新PR。整体地图仍是本地美术验收版，下面明确列出需要人工继续制作的内容。

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
{proportions}
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
{performance_rows()}

原v2同机位基线：

| 机位 | CPU P95/P99 ms | GPU P95/P99 ms | 帧间隔 P95/P99 ms | 显存MiB |
| --- | ---: | ---: | ---: | ---: |
{baseline_rows()}

区域CPU尾部本轮高于单次基线，未把视觉修改包装成全面性能提升。基线draw采在UI之后，不能直接和v3无UI draw算降幅；保留原JSON便于复测。连续缩放整段CPU P95/P99为{zoom['cpuRenderP95']:.2f}/{zoom['cpuRenderP99']:.2f}ms、GPU为{zoom['gpuP95']:.2f}/{zoom['gpuP99']:.2f}ms、帧间隔为{zoom['wallFrameP95']:.2f}/{zoom['wallFrameP99']:.2f}ms，初始构建{zoom['initialResidentMapBuildMs']:.0f}ms；这些仍提示后续需要实例与加载预算优化。

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
{port_table}

## 27项证据表

“完成”表示本轮可安全实施部分已落地且限制已记录，不能将未测端口或未批准市政厅解读为已完成制作。

| 计划项 | 实际结果/边界 | 原始证据 |
| --- | --- | --- |
{table}

## 文件变更与复现

运行根目录`验收纸艺审核版.ps1`进入交互预览；`-PaperPreset 1/2`切B/C，`-Quality Mobile/Compatibility`切渲染后端；`-SkipBuild`复用现有构建。预览内可切光照/调色/背景/装饰/镜头并旋转地标。`-RunChecks`执行三后端完整验收及缩放，日志在artifacts。原始截图未裁切代替，页面可点开1760×1000原图。

v3改动相对本地v2快照（其中包含尚未提交的旧轮次成果，不应统称本轮新增）：

{change_list}

新模块：`PaperFoldGeometry`、`PaperGroundContact`、`PaperSceneAudit`、`BoardSeamFoundations`，新调色Shader、保留的legacy对照Shader、两份审计/验收测试、用户参考rig测试目录、只读市政厅量测脚本、本报告生成器、审核版入口和本报告。完整列表见[变更清单](../../artifacts/paper-v3-change-manifest.json)。默认材质不会再上传无用的噪点纹理，退出时共享材质不作重复Dispose。

当前分支仍`art/origami-integration`，HEAD `{manifest['head']}`。仅新建`art/papercraft-lookdev-v3`引用指向原HEAD，未切分支。113文件基线与原有diff保存在`.art-tools/lookdev-v3-baseline/`；本地未提交改动不能靠该引用恢复，应使用文件快照，禁止reset/checkout覆盖现场。upstream/main与origin/main只读fetch后均dd708216，origin美术分支仍d6800bd。**没有本轮提交、push、PR更新或自动合并。**
'''
write(ROOT/'docs/art/PAPER_LOOKDEV_REVIEW.md',doc)

groups={
    '三机位与UI':[(f'{view} · '+('有UI' if ui=='ui' else '无UI'),f'paper-v3-forward_plus-{view}-{ui}.png',f'paper-v3-baseline-{view}-{ui}.png','v3全部改动','v2基线（非单变量）') for view in ['overview','region','close'] for ui in ['clean','ui']],
    '单变量A/B':[
        ('仅光照','paper-v3-forward_plus-lighting-new.png','paper-v3-forward_plus-lighting-v2-only.png','v3光照','旧光照；材质/几何不变'),
        ('仅材质','paper-v3-forward_plus-material-new.png','paper-v3-forward_plus-material-v2-only.png','干净哑光材质','旧纸纹；镜头/光照不变'),
        ('仅折面','paper-v3-forward_plus-geometry-folded-only.png','paper-v3-forward_plus-geometry-source-only.png','永久大折片适配','源网格；主根不变'),
        ('仅铁路权重','paper-v3-forward_plus-rail-quiet-only.png','paper-v3-forward_plus-rail-v2-weight-only.png','低对比浅纸轨','旧亮白轨；中心线不变'),
        ('仅调色','paper-v3-forward_plus-grade-on.png','paper-v3-forward_plus-grade-off.png','单次轻调色开','调色关；HUD像素保持'),
        ('仅太阳阴影','paper-v3-forward_plus-lighting-new.png','paper-v3-forward_plus-sun-shadow-off.png','太阳阴影开','太阳阴影关；环境AO仍保持'),
    ],
    '阴影与校准':[
        ('2与4分割','paper-v3-forward_plus-shadow-Parallel2Splits.png','paper-v3-forward_plus-shadow-Parallel4Splits.png','默认2分割','4分割'),
        ('2分割与Orthogonal','paper-v3-forward_plus-shadow-Parallel2Splits.png','paper-v3-forward_plus-shadow-Orthogonal.png','默认2分割','Orthogonal'),
        ('PCF与PCSS','paper-v3-forward_plus-lighting-new.png','paper-v3-forward_plus-pcss-test.png','默认固定PCF','PCSS试用：未启用'),
        ('光照A/B','paper-v3-forward_plus-calibration-0.png','paper-v3-forward_plus-calibration-1.png','A纸艺晴天校准','B清透校准'),
        ('光照A/C','paper-v3-forward_plus-calibration-0.png','paper-v3-forward_plus-calibration-2.png','A校准','C午后校准（改变太阳高度）'),
        ('A/C实际地图','paper-v3-forward_plus-preset-0.png','paper-v3-forward_plus-preset-2.png','A实际图','C实际图'),
    ],
    '渲染器与缩放':[
        ('Forward+/Mobile','paper-v3-forward_plus-region-clean.png','paper-v3-mobile-region-clean.png','Forward+','Mobile（同一Windows显卡）'),
        ('Forward+/Compatibility','paper-v3-forward_plus-region-clean.png','paper-v3-gl_compatibility-region-clean.png','Forward+','Compatibility（单独校准）'),
        ('GL阴影修正','paper-v3-gl-shadow-orthogonal-test.png','paper-v3-gl-shadow-acne-before.png','GL独立Orthogonal/偏移校准','修正前：不作为最终画面'),
        ('森林区域/近景','zoom-forest-region.png','zoom-forest-near.png','区域：主树不动','近景：增加低矮装饰'),
        ('森林/展示','zoom-forest-near.png','zoom-forest-showcase.png','近景','展示距离：同一完整网格'),
        ('皇城/海岸','zoom-palace-near.png','zoom-coast-near.png','皇城近景','海岸近景（提案）'),
    ],
}
sections=[]
for i,(name,pairs) in enumerate(groups.items()):
    cards=[]
    for title,a,b,la,lb in pairs:
        assert (OUT/a).exists() and (OUT/b).exists(),(a,b)
        cards.append(f'<article><h3>{title}</h3><div class="pair"><figure><a href="{a}" target="_blank"><img loading="lazy" src="{a}" alt="{html.escape(la)}"></a><figcaption>{la}</figcaption></figure><figure><a href="{b}" target="_blank"><img loading="lazy" src="{b}" alt="{html.escape(lb)}"></a><figcaption>{lb}</figcaption></figure></div></article>')
    sections.append(f'<section id="g{i}" class="group"'+(' hidden' if i else '')+'>'+''.join(cards)+'</section>')
buttons=''.join(f'<button type="button" aria-pressed="{str(i==0).lower()}" data-group="g{i}">{name}</button>' for i,name in enumerate(groups))
perf_html=''.join(f"<tr><td>{method}</td><td>{c['name']}</td><td>{c['cpuP95']:.2f}/{c['cpuP99']:.2f}</td><td>{c['gpuP95']:.2f}/{c['gpuP99']:.2f}</td><td>{c['frameP95']:.2f}/{c['frameP99']:.2f}</td><td>{c['memory']/1048576:.1f}</td><td>{c['drawsClean']}</td></tr>" for method,r in reports.items() for c in r['cases'])
page=f'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>小马国之星 · 纸艺审核 v3</title>
<style>body{{margin:0;background:#e9ede6;color:#263f3b;font:16px/1.65 system-ui,"Microsoft YaHei",sans-serif}}header,main{{max-width:1450px;margin:auto;padding:26px}}header{{padding-bottom:8px}}h1{{font-size:30px;margin:8px 0}}h2{{font-size:24px}}h3{{font-size:18px;margin:0 0 12px}}p{{max-width:1100px}}.notice{{background:#fff8e7;border-left:5px solid #b99555;padding:15px 20px}}nav{{display:flex;gap:8px;flex-wrap:wrap;position:sticky;top:0;padding:12px 0;background:#e9ede6;z-index:1}}button{{font:inherit;border:1px solid #8ba8a1;border-radius:20px;padding:8px 19px;background:#f8faf5;color:#294c43;cursor:pointer}}button[aria-pressed=true]{{background:#355d52;color:white}}article{{padding:20px;margin:18px 0;background:#fafbf7;border:1px solid #d3ded5;border-radius:12px}}.pair{{display:grid;grid-template-columns:1fr 1fr;gap:16px}}figure{{margin:0}}img{{display:block;width:100%;background:#4c969f;border-radius:5px}}figcaption{{padding:8px 0}}a{{color:#315d87}}.table{{overflow:auto}}table{{border-collapse:collapse;width:100%;font-size:14px}}td,th{{text-align:left;white-space:nowrap;border-bottom:1px solid #c9d5ce;padding:9px}}.pass{{color:#345f46;font-weight:bold}}@media(max-width:780px){{.pair{{grid-template-columns:1fr}}header,main{{padding:16px}}}}[hidden]{{display:none!important}}</style>
<header><p>本地验收 v3 · {NOW} · 未提交 / 未上传</p><h1>让纸张的折面承接光，而不是叠加滤镜</h1><p>审核整理为7阶段27项。这里都是原Godot工程的实际渲染。保留地图与铁路逻辑、单套完整网格；大折片树冠、山脊、接地、轻调色与铁路降权已本地安装。</p>
<p><a href="../docs/art/PAPER_LOOKDEV_REVIEW.md">完整报告与27项证据</a> · <a href="../../AI协作_前端美术/审核实施计划_v3.md">实施计划</a> · <a href="../../AI协作_前端美术/当前进度.md">协作交接</a> · <a href="paper-v3-change-manifest.json">变更清单</a></p>
<div class="notice">铁路仍为离线描线草稿，13条逻辑连接的物理端口待制作；市政厅未标准化源仅量测。背景地理和真实棋子全路线未定稿。三机位整体前后包含多项修改，请用“单变量A/B”判断具体改动。</div></header>
<main><nav aria-label="对照类别">{buttons}</nav>{''.join(sections)}
<h2>真实本机数据</h2><p class="pass">三渲染器验收通过 · 4个HUD色块及真实卡面0字节差 · 184主植被/138格缩放保持 · 421GLB与源哈希通过</p>
<p>Windows / RTX 5000 Ada Laptop / 1760×1000。计时含测试和抓图调度；CPU/GPU不可相加，非发行FPS或手机认证。区域CPU尾部高于单次基线，仍需后续实例和加载优化。</p>
<div class="table"><table><thead><tr><th>渲染器</th><th>机位</th><th>CPU P95/P99 ms</th><th>GPU P95/P99 ms</th><th>帧间隔 P95/P99 ms</th><th>显存MiB</th><th>无UI draw</th></tr></thead><tbody>{perf_html}</tbody></table></div>
<p><a href="paper-v3-forward_plus-acceptance.json">Forward+原始JSON</a> · <a href="paper-v3-mobile-acceptance.json">Mobile JSON</a> · <a href="paper-v3-gl_compatibility-acceptance.json">Compatibility JSON</a> · <a href="zoom-acceptance.json">缩放JSON</a> · <a href="paper-v3-rail-gap-audit.json">铁路/间隙核对</a></p>
<p>运行客户端根目录 <code>验收纸艺审核版.ps1</code>，滚轮缩放，点击选地块；A/B/C按钮切换光照，“轻调色”开关单独对比。完整参数与失败记录见报告。点击截图可看完整原图。</p></main>
<script>document.querySelectorAll('nav button').forEach(b=>b.addEventListener('click',()=>{{document.querySelectorAll('.group').forEach(s=>s.hidden=s.id!==b.dataset.group);document.querySelectorAll('nav button').forEach(x=>x.setAttribute('aria-pressed',String(x===b)));}}));</script></html>'''
write(OUT/'paper-v3-review.html',page)
# Serve only a curated evidence folder, never the source workspace or repository.
bundle=OUT/'paper-v3-review-bundle'
bundle.mkdir(exist_ok=True)
images={f for pairs in groups.values() for _,a,b,_,_ in pairs for f in [a,b]}
for filename in images | {'paper-v3-change-manifest.json','paper-v3-forward_plus-acceptance.json',
        'paper-v3-mobile-acceptance.json','paper-v3-gl_compatibility-acceptance.json',
        'zoom-acceptance.json','paper-v3-rail-gap-audit.json'}:
    source=OUT/filename
    assert source.is_file() and source.parent==OUT and source.suffix in {'.png','.json'}
    shutil.copy2(source,bundle/filename)
for source in [ROOT/'docs/art/PAPER_LOOKDEV_REVIEW.md',HUB/'审核实施计划_v3.md',HUB/'当前进度.md']:
    shutil.copy2(source,bundle/source.name)
portable=page.replace('../docs/art/PAPER_LOOKDEV_REVIEW.md','PAPER_LOOKDEV_REVIEW.md').replace('../../AI协作_前端美术/审核实施计划_v3.md','审核实施计划_v3.md').replace('../../AI协作_前端美术/当前进度.md','当前进度.md')
write(bundle/'index.html',portable)
print('PAPER_V3_EVIDENCE_BUILT',len(evidence),'items;',len(changed),'baseline text files changed')
