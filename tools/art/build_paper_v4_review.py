"""Local V4 evidence assembly. Reads real engine output; never uploads or changes Git."""
from pathlib import Path
from collections import Counter
from datetime import datetime, timezone, timedelta
import hashlib
import html
import json
import shutil
import subprocess
from PIL import Image
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'artifacts'
HUB=ROOT.parent/'AI协作_前端美术'
NOW=datetime.now(timezone(timedelta(hours=8))).isoformat(timespec='seconds')
METHODS=['forward_plus','mobile','gl_compatibility']

def read(path):return json.loads((ROOT/path).read_text(encoding='utf-8-sig'))
def write(path,value):path.write_text(value,encoding='utf-8')
def dump(path,value):write(path,json.dumps(value,ensure_ascii=False,indent=2)+'\n')
def git(*args):return subprocess.check_output(['git',*args],cwd=ROOT).decode('utf-8').strip()
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

reports={m:read(f'artifacts/paper-v4-{m}-acceptance.json') for m in METHODS}
assert all(r['status']=='passed' for r in reports.values())
invariants={m:next(x for x in r['evidence'] if x.get('name')=='dynamic-invariants') for m,r in reports.items()}
reduced={m:next(x for x in r['evidence'] if x.get('name')=='reduced-mode') for m,r in reports.items()}
main=invariants['forward_plus']
sites=json.loads(main['siteJson'])
benchmarks=[]
for path in sorted(OUT.glob('paper-v4-*-benchmark-*.json')):
    assert path.stat().st_mtime>(OUT/'paper-v4-build.log').stat().st_mtime,'Stale sampling from an earlier build: '+path.name
    report=json.loads(path.read_text(encoding='utf-8-sig'))
    if report['status']!='passed':continue
    for row in report['evidence']:
        if row.get('durationSeconds',0)>=30:benchmarks.append({'renderer':report['renderer'],**row})
required={('forward_plus',look,view,False) for look in [0,3,4] for view in ['overview','region','close']}
required|={(m,3,view,False) for m in ['mobile','gl_compatibility'] for view in ['overview','region','close']}
required|={('forward_plus',3,'region',True)}
assert required<={(r['renderer'],r['look'],r['view'],r['reduced']) for r in benchmarks},'Clean 30-second sampling incomplete'
assert all((r['renderTargetWidth'],r['renderTargetHeight'])==(1760,1000) for r in benchmarks),'Actual benchmark resolution differs'
for method,r in reports.items():
    for view in ['overview','region','close']:
        cases=[x for x in r['evidence'] if x.get('view')==view]
        assert len(cases)==3 and len({x['cameraTransform'] for x in cases})==1
    assert invariants[method]['frozenMeanByteDelta']==0

stats={}
for method in METHODS:
    pictures={k:np.asarray(Image.open(OUT/f'paper-v4-{method}-{k}-region-clean.png').convert('RGB')).astype(float) for k in ['v3','D','D-vivid']}
    grade=[np.asarray(Image.open(OUT/f'paper-v4-{method}-grade-{k}-only.png').convert('RGB')).astype(float) for k in ['v3','D']]
    stats[method]={'D_vs_V3_meanByteDifference':float(np.abs(pictures['D']-pictures['v3']).mean()),
        'vivid_vs_standard_meanByteDifference':float(np.abs(pictures['D-vivid']-pictures['D']).mean()),
        'gradeOnlyMeanByteDifference':float(np.abs(grade[1]-grade[0]).mean()),
        'wholeImageWhiteFractions':{k:{'allChannelsAbove245':float((v.min(axis=2)>245).mean()),'allChannels255':float((v.min(axis=2)==255).mean()),'allChannelsBelow15':float((v.max(axis=2)<15).mean())} for k,v in pictures.items()}}
dump(OUT/'paper-v4-image-statistics.json',stats)

snapshot=read('.art-tools/lookdev-v4-baseline/file-hashes.json')
changed=[p for p,h in snapshot.items() if (ROOT/p).is_file() and sha(ROOT/p)!=h]
new_files=['scripts/art/PaperPlaceComposition.cs','scripts/art/PaperEnvironmentMotion.cs','scripts/art/PaperFlagMotion.cs',
    'assets/shaders/paper_ambient.gdshader','assets/shaders/paper_local_halo.gdshader','tests/RunPaperV4Acceptance.cs',
    'tools/art/build_paper_v4_review.py','验收纸艺童话V4.ps1','docs/art/PAPER_LOOKDEV_V4_REVIEW.md']
manifest={'createdAtShanghai':NOW,'head':git('rev-parse','HEAD'),'branch':git('branch','--show-current'),
    'baselineSnapshotFiles':len(snapshot),'changedFromV3Snapshot':changed,'newV4Files':new_files,
    'sourcePolicy':'421 original GLBs and original blend unchanged; source status retained; only runtime adaptation',
    'uploadPolicy':'No commit/push/deployment/PR/merge; explicit new user request required.'}
dump(OUT/'paper-v4-change-manifest.json',manifest)

resource_list=[
    {'asset':'twilight-castle','source':'交付/地块/暮光城堡/02_建筑.blend','status':'installed','detail':'Building_Twilight_FoldedBanners / TW7_Paper_FlagLight and FlagPaper split into 3 connected cloth pieces; original UV/materials and fixed structure retained. Explicit authored hinge markers would improve the provisional upper-edge hinges.'},
    {'asset':'weather-factory','source':'交付/地块/天气工厂/02_建筑.blend','status':'installed','detail':'2 vapor sources on reviewed upper horizontal caps of 11_R3B_Factory_DiamondDomesAndSteppedChimneys; exact local/world positions in acceptance JSON. Suggest authoring chimney_outlet_01/02 markers.'},
    {'asset':'hotel-candidate','source':'交付/地块/高档场所_酒店候选方案/03_装饰.blend','status':'future-source-split','detail':'16_R2C_Hotel_HorseBannersAndTrolley combines cloth and trolley; separate cloth/hinges before adding wind. Candidate status remains.'},
    {'asset':'baltimare','source':'交付/地块/巴尔的马/03_装饰.blend','status':'future-source-split','detail':'25_Klugetown_CargoStairsAndBanners combines banners/cargo/stairs; separate cloth before animation.'},
    {'asset':'13-station-connections','status':'not-surveyed','detail':'V3 physical station ports remain unverified. No moving train on draft rails.'},
    {'asset':'city-hall','source':'折纸市政厅2.blend','status':'not-standardized','detail':'V3 authoring assembly/ground origin still require confirmation; not silently installed.'},
    {'asset':'all-site-entrances','status':'visual-proposal','detail':'New low approaches follow current source-assembly fronts. Explicit door/front markers improve future reorientation; these surfaces add no gameplay edges.'},
]
dump(OUT/'paper-v4-source-handoff.json',resource_list)

def fmt(v):return f'{v:.2f}' if v>0 else '不可用'
perf_rows='\n'.join(f"| {r['renderer']} | {r['look']}{'减少' if r['reduced'] else ''} / {r['view']} | {fmt(r['cpuP95'])}/{fmt(r['cpuP99'])} | {fmt(r['gpuP95'])}/{fmt(r['gpuP99'])} | {fmt(r['frameP95'])}/{fmt(r['frameP99'])} | {fmt(r['processP95'])}/{fmt(r['processP99'])} | {r['motionP95']:.3f} | {r['drawsPeak']:.0f} | {r['memoryPeakBytes']/1048576:.1f} |" for r in benchmarks)
def case(look,view,reduced=False):return next(r for r in benchmarks if r['renderer']=='forward_plus' and r['look']==look and r['view']==view and r['reduced']==reduced)
deltas=[]
for view in ['overview','region','close']:
    a,b=case(0,view),case(3,view)
    deltas.append({'view':view,'cpuRenderP95Delta':b['cpuP95']-a['cpuP95'],'gpuP95Delta':b['gpuP95']-a['gpuP95'],'processP95Delta':b['processP95']-a['processP95'],'motionP95Delta':b['motionP95']-a['motionP95'],'drawsDelta':b['drawsPeak']-a['drawsPeak'],'memoryMiBDelta':(b['memoryPeakBytes']-a['memoryPeakBytes'])/1048576})
dump(OUT/'paper-v4-performance-summary.json',{'conditions':'1760x1000; warmed 180 frames; each interval >=30 actual seconds; animated; no screenshots/recording/assertions/log IO in sampled loops; same Windows RTX 5000 Ada Laptop','rows':benchmarks,'DminusV3':deltas,'metricSemantics':{'cpu':'Measured viewport rendering CPU only; excludes scripts and global frame setup. Not total CPU frame time.','process':'Performance.TimeProcess: engine main.cpp publishes process_max once per second. These percentiles describe the sampled second-peak monitor, not per-frame script execution.','motion':'Direct controller tick duration in microsecond clock, reported as milliseconds.','frame':'Consecutive SceneTree.ProcessFrame wall-clock intervals; includes scheduling.'},'note':'CPU/GPU are separate engine metrics, not additive. Zero metrics are unavailable, not free. First long pass retained; fully transparent distant leaf/star batches are now culled after fade, phases retained. V3 old capture-heavy 90/120-frame values are not comparison data.'})
over=[r['view'] for r in deltas if r['gpuP95Delta']>2 or r['cpuRenderP95Delta']>1]
assessment=('当前视口渲染CPU/GPU增量在约1/2ms起点内。' if not over else '当前仍超出视口渲染CPU约1ms/GPU约2ms起点的机位：'+', '.join(over)+'。')+'这不等于总CPU已经认证：引擎TimeProcess的每秒峰值和墙钟长尾仍需单独评估，完整监测保留。'
delta_table='\n'.join(f"| {r['view']} | {r['cpuRenderP95Delta']:+.2f} | {r['gpuP95Delta']:+.2f} | {r['processP95Delta']:+.2f} | {r['drawsDelta']:+.0f} | {r['memoryMiBDelta']:+.1f} |" for r in deltas)
site_table='\n'.join(f"| {s['Cell']} | {s['Asset']} | {s['Relation']} | {s['Flowers']} | {s['MaxHeight']:.3f} |" for s in sites)
motion=main['motion'];reduced_motion=reduced['forward_plus']['motion']
movie=read('artifacts/paper-v4-video-metadata.json')
assert 15<=movie['durationSeconds']<=20
stats_table='\n'.join(f"| {m} | {s['D_vs_V3_meanByteDifference']:.2f} | {s['gradeOnlyMeanByteDifference']:.2f} | {s['vivid_vs_standard_meanByteDifference']:.2f} | {s['wholeImageWhiteFractions']['D']['allChannels255']*100:.3f}% |" for m,s in stats.items())
report=f'''# V4 · 纸艺童话场所、光影与动态验收

{NOW}。本轮依据用户V4任务书，按[6阶段30项计划](../../../AI协作_前端美术/审核实施计划_v4.md)实施到可验收。入口 `验收纸艺童话V4.ps1`，默认D标准；[真实对照与18秒录像](../../artifacts/paper-v4-review.html)。没有提交、上传、部署或合并。源v005没有新增模型，421GLB/blend/候选状态保留。

## 这轮可直接看到的变化

在同一张原棋盘中，D比V3有更亮的暖日照、更明确的蓝紫暗面、饱满但有保护的颜色。时装店门前铺地/花坛/低边缘与森林根部纸坡相联系；城镇、森林遗迹、水晶核心、街区与皇城平台有不同的地面语言。独立大纸片树冠、旗帜、气球/云、水面/瀑布高光、局部星点/纸叶与工厂纸状蒸气使用不同节奏。

这里都是原Godot 4.6.2 .NET工程实际画面，没有用另建demo或AI图替换。V3/A、B、C仍可切；V3场景不创建V4铺地、旗帜拆分、树冠扩展或额外特效，原灯光/调色算法保持。保存了197文件V3指纹与源码快照、原diff和原三机位；新对照统一冻结时钟到2.25秒，不能把天空动画时相的小差异说成原源模型被改动。

## 样板及地区组织

新增场所由一个批量Mesh表达，顶点颜色控制各区纸色；没有逐朵花新增脚本或材质。地面大多起伏低于0.044D，位于棋子身体检查起点之下；皇城后侧纸山壁单独约0.40D，并通过落点/入口检查。铺地跟随现有装配前方，是视觉提案，不增加行走连边。云中L区以及A7云宝家明确排除新贴地铺地/支撑。

森林沿原根节点添低纸坡，按稳定种子把可安全的树冠扩大为1.08/1.155/1.23；候选净空不通过时退回原树冠尺度，不移动根或在通道中增高树。泽可拉/旧城堡/谐律之树分别使用曲折入口、残垣坡与开阔根地。森林遗迹、西部干燥区、龙地/塔塔罗斯与雪区使用各自冷暖纸坡，不套用鲜花花坛；云宝家保持悬浮，不生成地面入口。原33组棋子配置和所有规则空隙保留。

原环境回归最初发现树冠扩大侵入旧0.30r中心保留框，尽管较小的棋子三角面代理仍通过，已据此回退受影响树冠的尺寸，并逐支点比较正负极限摆动；{motion['swayLimitedHinges']}个支点限制摆幅。保留原0.30r检查原样，没有缩小保留框或删断言。最终环境回归和8时相包络同时通过，失败日志留存。

| 格 | 源地标 | 场所关系 | 小花数 | 记录最大高差m |
| --- | --- | --- | ---: | ---: |
{site_table}

铁路D档改为浅米灰哑光厚纸带，单轨宽0.014D，轨距0.045D，截面深0.035r，枕木间隔0.38r；修正D几何正面绕序，避免纸带顶面被当背面而显暗金属色。V3原样对照保留。原中心线路径/13条STATION_ROUTE不变，14处V3草稿桥面仍为离线草稿，未增加列车运行或声称正式端口完成。

## D标准、D强表现与单变量

保留Filmic、曝光1、固定世界太阳：D太阳高度50°、暖白#fff1dc，标准能量1.40，强表现1.50；补光#A8B8F4/0.09，天空/地面补光更克制。1.30/1.40/1.50有同机位独立图，未把色调映射切换混入能量比较。Forward+/Mobile延续2分割固定PCF；GL保留独立Orthogonal与偏移，D太阳再×0.43、常量补光0.22。没有默认PCSS、SSIL、全局雾、自动曝光或大范围Bloom。

一次LDR调色，强度标准0.88/强表现1.00，饱和度1.14/1.20、对比1.085/1.12。新算法对已有高饱和颜色减小增幅，用端点保护的柔和明暗曲线，暗部偏蓝紫、高光稍暖，中性高亮纸白额外保护。不恢复屏幕噪点、暗角、黑描边。三个局部地标光晕片代替整屏Glow，标准强度0.48、强表现0.80，只有少量自身透明覆盖，不额外加OmniLight；HUD在全部世界效果之后。

| 后端 | D与V3整图均差/255 | 仅调色均差/255 | 强与标准均差/255 | D整图全通道255占比 |
| --- | ---: | ---: | ---: | ---: |
{stats_table}

数值只作辅助；页面提供原分辨率实图检查。白色占比按全图计算，不是“白纸材质分割”或每面均无高光的证明。整体D/V3图包含多项改动；灯光、调色、光晕、冻结/推进时间另有单变量图。

## 六类动态与真实来源

| 类别 | 实际实现 | 稳定边界 |
| --- | --- | --- |
| 树冠/旗帜 | {motion['crownHinges']}树冠支点、{motion['flagHinges']}原暮光折纸旗布连通组；树冠约2.1°/4.8秒、旗布3.2°/3.4秒 | 原树干、主根、杆/建筑固定，UV和槽保留；共同世界风向、TileID/组件序号错相 |
| 气球/云/纸鸟 | 既有模型节点由统一时钟驱动；气球约0.0225D/7.6秒，云慢漂移 | 原天空范围内有限往复，没有在可见区域突然回绕 |
| 水面/瀑布 | {motion['waterInstances']}小纸波纹，原Water/Pond及ContinuousRainbowAndPool分组有宽纸带高光流动 | 岸线、水位与原几何不移，非玻璃水和密集顶点波 |
| 局部魔法 | 三地标18个缓升星点、3个局部光晕 | 原建筑不缩放/整体闪烁，不照亮全图普通纸面 |
| 林缘小事件 | 4个空间小组共12片折纸叶，稳定种子与偶发节奏；既有BG纸鸟保留 | 远景渐隐，沿用缩放滞回，不在边界重发同批物体 |
| 已有烟囱 | 天气工厂L7两个真实阶梯烟囱上方各4片纸状蒸气 | 根据已查看的源模型与高处水平盖面定位；不凭空给住宅添加烟囱 |

首次只扫描部分地标没有发现独立旗帜/烟口，随后全421分件扫描找到暮光旗布与天气工厂阶梯烟囱，已继续接入，不能再用早期“没有模型”结论停工。具体烟口坐标、旗布状态见原始验收JSON与[源建模交接](../../artifacts/paper-v4-source-handoff.json)。酒店候选和巴尔的马仍把旗布/货物/手推车合在同一网格，未让整个装配摇晃。

全部特效是{motion['batches']}个空间小批次、最多{motion['particleBudget']}个小动态实例，加{motion['waterInstances']}条水纹；没有每树逐帧脚本或每帧新建材质/上传整套实例矩阵。减少模式把小动态实际可见数降到{reduced_motion['activeSmallInstances']}并降低幅度，分配的MultiMesh容量仍保留，不宣称显存同比下降。主树保留投影，小纸片/星点/烟不投影。新增批次有显式包围盒，树冠/旗布有摆动余量。首轮30秒采样发现总览GPU增量略过2ms起点后，增加了零透明度叶片/星点批次在淡出终点的真正隐藏，避免仍提交透明draw；时钟相位继续，不在回到近景时重发。首轮全部数据保留，优化后重新采集最终16条件。

统一paper_clock控制CPU支点和Shader，不使用活跃TIME偷跑。关闭/冻结时保留当前时相，图像冻结均差{main['frozenMeanByteDelta']}；同机位推进2秒的变化为{main['motionMeanByteDelta']:.3f}/255。树冠密度/幅度按既有缩放档渐变，减少模式不重置相位。没有烘死的太阳长影或全图透明黑片。

## 实际录像与验收

[18秒MP4](../../artifacts/paper-v4-actual-runtime.mp4)，{movie['width']}×{movie['height']}，{movie['fps']}fps，编码时长{movie['durationSeconds']:.2f}秒。由原Gallery的Godot MovieWriter实际逐帧运行生成，先固定区域看动态，再缓慢移近时装店；固定步长录像并非实时屏幕性能证据。保留原AVI与编码/探测日志，无AI插帧或静态图拼接替代。页面提供2/8/14/17秒抽帧。

三后端完整验收通过，V3/D/强表现各三机位、各有/无UI。4个HUD色块及真实角色卡共85500通道字节在全部世界效果/调色开关时0差异；Forward+/Mobile实测HDR2D旁路并恢复调色；鼠标Ignore。{main['roots']}个主植被根、{main['cells']}格、{main['buildings']}建筑根、落点/点击身份、曝光与太阳保持；八个时相的森林风动包络和138格净空/示例移动复核通过。完整缩放/缓存回归、地图/地块/界面/美术测试另留日志；这些不等同于真实棋子全路线或在线多人验收。

## 30秒干净性能样本

每条件预热180帧后至少30秒实际运行，动画开启，原分辨率同机位，无截图/录屏/断言/密集日志。RTX 5000 Ada Laptop、Windows；这是同机设备数据，不是手机/网页认证。视口CPU渲染不含脚本与全局帧准备；动态控制器另直接计时。TimeProcess在4.6.2的main.cpp中每秒发布process_max，因此下表是每秒峰值监测的采样分位，不能称作逐帧脚本P95/P99。墙钟是连续ProcessFrame间隔。CPU与GPU不能相加推FPS。0计时写“不可用”；显存是引擎监测。

| 后端 | 状态/机位 | 视口CPU P95/P99 ms | GPU P95/P99 ms | 帧间隔 P95/P99 ms | 引擎秒峰监测 P95/P99 ms | 动态P95 ms | draw峰值 | 显存MiB |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
{perf_rows}

D相对本轮重新采样的V3：

| 机位 | CPU渲染P95差ms | GPU P95差ms | 进程P95差ms | draw差 | 显存差MiB |
| --- | ---: | ---: | ---: | ---: | ---: |
{delta_table}

{assessment} 目标起点为GPU P95新增约≤2ms、CPU约≤1ms；已先做远景透明批次剔除，仍保留地区CPU长尾。单次顺序样本没有锁定笔记本功耗/频率，不将细小差值当作稳定硬件承诺；这也不是忽略实际超出数值的理由。没有用上一轮带抓图的90/120帧值计算提升百分比。

## 实图发现并修复的问题

新增纸坡/铺地最初背面绕序错误，修正为Godot正面；新的十六进制顶点色明确转线性，避免绿纸坡发白，原资源的顶点色语义不动。D铁路相同的顶面绕序问题也单独修正。纹理/调色/太阳阴影逐项关闭的原图已保存：残留斜纹主要随阴影消失，不能用grain=0否认PCF/自阴影采样。Forward+保持局部稳定偏移，GL用独立Orthogonal；极近处细面仍有少量采样，是后续目标设备细调项。

退出期三次出现重复material_set_shader错误，压缩保留失败日志。没有把“渲染pass但进程卡住”算最终通过；异步Clear/QueueFree修复了主要场景持有，但完整录像补测又复现。核对4.6.2官方源码后，进一步在退出前清掉HexCellMeshFactory/MapLayoutConfig的静态标准材质/几何引用，默认材质改为可重新生成的惰性缓存，并在退出期回收后ForceSync。连续三次短测、完整录像和最终全部采样正常退出。此处只释放引用，不提前对仍共享材质反复Dispose或原地换Shader；运行中不强制GC、不逐帧ForceSync。该故障来自退出清理顺序的推断与本地复测，不冒称上游已确认缺陷。

## 后续源制作与范围

酒店候选/巴尔的马需要分离旗布与刚体，暮光旗布及工厂烟口建议加作者标记；入口铺地目前按装配前向提案，可补准确door/front标记。皇城后山与街区仍是局部纸艺布景，需要艺术家继续精修形体；没有重画大陆地理。13正式铁路端口、市政厅源标准化、真实棋子/全部路线、手机和发行包仍属后续。缺件清单给出确切文件/网格，没有伪称都已定稿。

原始源码快照`.art-tools/lookdev-v4-baseline/`，变更清单[JSON](../../artifacts/paper-v4-change-manifest.json)。新增模块为PaperPlaceComposition、PaperEnvironmentMotion、PaperFlagMotion及两份局部Shader，扩展既有光照/单调色/树冠分件/铁路/缩放控制/预览；原源稿和二进制产物未重写。当前分支{manifest['branch']}，HEAD {manifest['head']}；只读fetch两远端未变，未commit/push/改PR/部署/合并。

官方依据：[Godot 4.6 MovieWriter录制](https://docs.godotengine.org/en/4.6/tutorials/animation/creating_movies.html)、[MultiMesh批量与包围盒](https://docs.godotengine.org/en/4.6/classes/class_multimesh.html)、[Spatial shader颜色与法线](https://docs.godotengine.org/en/4.6/tutorials/shaders/shader_reference/spatial_shader.html)、[环境/后处理](https://docs.godotengine.org/en/4.6/tutorials/3d/environment_and_post_processing.html)。具体颜色/节奏由本工程实图选择。视频编码器只装在忽略目录，来自PyPI imageio-ffmpeg 0.6.0 Windows wheel，已核对官方SHA256，未装入系统或提交仓库。

性能语义依据：[Performance监测更新说明](https://docs.godotengine.org/en/4.6/classes/class_performance.html)、[视口渲染计时范围](https://docs.godotengine.org/en/4.6/classes/class_renderingserver.html#class-renderingserver-method-viewport-get-measured-render-time-cpu)、[当前4.6.2 main.cpp每秒峰值实现](https://github.com/godotengine/godot/blob/4.6.2-stable/main/main.cpp#L4640)。
'''
write(ROOT/'docs/art/PAPER_LOOKDEV_V4_REVIEW.md',report)

groups={
 '三个状态/三机位':[(f'{view} · V3 → D',f'paper-v4-forward_plus-v3-{view}-clean.png',f'paper-v4-forward_plus-D-{view}-clean.png','V3/A原状态','D标准') for view in ['overview','region','close']]+[(f'{view} · D → 鲜明',f'paper-v4-forward_plus-D-{view}-clean.png',f'paper-v4-forward_plus-D-vivid-{view}-clean.png','D标准','D强表现') for view in ['overview','region','close']],
 '单变量/诊断':[
 ('仅光照','paper-v4-forward_plus-lighting-v3-only.png','paper-v4-forward_plus-lighting-D-only.png','V3光照','D光照；其他保持'),
 ('仅调色','paper-v4-forward_plus-grade-v3-only.png','paper-v4-forward_plus-grade-D-only.png','V3轻调色','D饱和度感知调色'),
 ('仅光晕','paper-v4-forward_plus-halo-off.png','paper-v4-forward_plus-halo-on.png','局部光晕关','局部光晕开'),
 ('冻结与推进2秒','paper-v4-forward_plus-motion-frozen.png','paper-v4-forward_plus-motion-after-two-seconds.png','冻结','同参数推进2秒'),
 ('纹理/阴影排查','paper-v4-forward_plus-diagnostic-no-albedo-texture.png','paper-v4-forward_plus-diagnostic-no-shadow.png','原颜色贴图关闭','太阳阴影关闭'),
 ('能量1.30/1.40','paper-v4-forward_plus-sun-1.30.png','paper-v4-forward_plus-sun-1.40.png','只改主光1.30','只改主光1.40（采用）'),
 ('1.40/1.50','paper-v4-forward_plus-sun-1.40.png','paper-v4-forward_plus-sun-1.50.png','1.40','1.50'),
 ],
 '动态来源/后端/UI':[
 ('真实烟囱/旗布','paper-v4-forward_plus-factory-smoke-close.png','paper-v4-forward_plus-twilight-flags-close.png','天气工厂原烟囱','暮光原旗布分组'),
 ('减少动态','paper-v4-forward_plus-motion-after-two-seconds.png','paper-v4-forward_plus-reduced-motion.png','完整动态38小物','减少模式17小物，时相保持'),
 ('Forward+/Mobile','paper-v4-forward_plus-D-region-clean.png','paper-v4-mobile-D-region-clean.png','Forward+','Mobile：同一Windows设备'),
 ('Forward+/GL','paper-v4-forward_plus-D-region-clean.png','paper-v4-gl_compatibility-D-region-clean.png','Forward+','GL独立颜色/阴影校准'),
 ('实际UI/卡面','paper-v4-forward_plus-world-effects-ui-on.png','paper-v4-forward_plus-world-effects-ui-off.png','世界效果开','世界效果关；HUD/卡面0字节差'),
 ],
}
sections=[]
for i,(title,pairs) in enumerate(groups.items()):
    cards=[]
    for name,a,b,la,lb in pairs:
        assert (OUT/a).is_file() and (OUT/b).is_file(),(a,b)
        cards.append(f'<article><h3>{name}</h3><div class="pair"><figure><a href="{a}" target="_blank"><img loading="lazy" src="{a}" alt="{html.escape(la)}"></a><figcaption>{la}</figcaption></figure><figure><a href="{b}" target="_blank"><img loading="lazy" src="{b}" alt="{html.escape(lb)}"></a><figcaption>{lb}</figcaption></figure></div></article>')
    sections.append(f'<section class="group" id="g{i}"'+(' hidden' if i else '')+'>'+''.join(cards)+'</section>')
buttons=''.join(f'<button type="button" data-group="g{i}" aria-pressed="{str(i==0).lower()}">{title}</button>' for i,title in enumerate(groups))
perf_table=''.join(f"<tr><td>{r['renderer']}</td><td>{r['look']}{'减少' if r['reduced'] else ''}/{r['view']}</td><td>{fmt(r['cpuP95'])}/{fmt(r['cpuP99'])}</td><td>{fmt(r['gpuP95'])}/{fmt(r['gpuP99'])}</td><td>{fmt(r['frameP95'])}/{fmt(r['frameP99'])}</td><td>{r['motionP95']:.3f}</td><td>{r['drawsPeak']:.0f}</td><td>{r['memoryPeakBytes']/1048576:.1f}</td></tr>" for r in benchmarks)
page=f'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>小马国之星 · D纸艺童话V4</title>
<style>body{{margin:0;background:#eaf0e8;color:#283e40;font:16px/1.7 system-ui,"Microsoft YaHei",sans-serif}}header,main{{max-width:1480px;margin:auto;padding:26px}}h1{{font-size:32px}}h2{{font-size:25px}}h3{{font-size:18px;margin:0 0 12px}}p{{max-width:1150px}}a{{color:#37648b}}.note{{padding:14px 20px;background:#fff8e7;border-left:5px solid #bb9755}}video{{width:100%;max-height:760px;background:#273f43;border-radius:10px}}nav{{display:flex;gap:9px;flex-wrap:wrap;position:sticky;top:0;background:#eaf0e8;padding:12px 0;z-index:1}}button{{font:inherit;padding:8px 18px;border:1px solid #8eaca3;border-radius:24px;background:#f9fbf5;color:#314c43;cursor:pointer}}button[aria-pressed=true]{{color:white;background:#3e675b}}article{{margin:20px 0;padding:20px;background:#fcfcf7;border:1px solid #d4dfd3;border-radius:12px}}.pair{{display:grid;grid-template-columns:1fr 1fr;gap:15px}}figure{{margin:0}}img{{width:100%;display:block;border-radius:5px}}figcaption{{padding:7px 0}}.table{{overflow:auto}}table{{border-collapse:collapse;font-size:14px;width:100%}}td,th{{padding:8px;border-bottom:1px solid #ccd8d1;text-align:left;white-space:nowrap}}.frames{{display:grid;grid-template-columns:repeat(4,1fr);gap:10px}}@media(max-width:800px){{.pair{{grid-template-columns:1fr}}.frames{{grid-template-columns:1fr 1fr}}header,main{{padding:16px}}}}[hidden]{{display:none!important}}</style>
<header><p>V4本地验收 · {NOW} · 未提交/未上传</p><h1>阳光下可以探索的折纸童话世界</h1><p>保留原棋盘，在地标门前、林缘与纸坡之间建立联系；D标准有可见冷暖与饱满颜色，六类环境动态共享可冻结时间。V3/A/B/C仍保留，默认D标准。</p><p><a href="../docs/art/PAPER_LOOKDEV_V4_REVIEW.md">完整报告与参数</a> · <a href="../../AI协作_前端美术/审核实施计划_v4.md">6阶段30项计划</a> · <a href="paper-v4-source-handoff.json">源建模清单</a> · <a href="paper-v4-change-manifest.json">文件变更</a></p>
<div class="note">实际Godot工程画面。铁路仍为离线草稿，13站口未测；没有列车演示掩盖连接。云中建筑不加近地铺地。烟来自已查看的天气工厂阶梯烟囱，旗布来自原暮光组件；候选和原blend保留。</div></header>
<main><h2>18秒实际运行录像</h2><video controls preload="metadata" poster="paper-v4-video-frame-02.png"><source src="paper-v4-actual-runtime.mp4" type="video/mp4"></video><p>Godot原工程固定步长逐帧录制，先区域观察，再缓慢拉近。不是实测FPS证据；性能另做30秒干净采样。<a href="paper-v4-actual-runtime.mp4" download>下载MP4</a></p>
<div class="frames">{''.join(f'<a href="paper-v4-video-frame-{t:02}.png" target="_blank"><img loading="lazy" src="paper-v4-video-frame-{t:02}.png" alt="录像{t}秒实际抽帧"></a>' for t in [2,8,14,17])}</div>
<nav aria-label="画面对照">{buttons}</nav>{''.join(sections)}
<h2>30秒稳定运行数据</h2><p>同一RTX 5000 Ada Laptop / Windows / 1760×1000，每条件预热180帧，采样期间无截图/录屏/断言/密集日志。CPU为视口渲染计时，动态另直接计时；TimeProcess每秒峰值及帧间隔完整保留，CPU与GPU不相加，不可用计时不写成零成本。{assessment}</p><div class="table"><table><thead><tr><th>后端</th><th>状态/机位</th><th>视口CPU P95/P99</th><th>GPU P95/P99</th><th>帧间隔P95/P99</th><th>动态P95</th><th>draw峰值</th><th>显存MiB</th></tr></thead><tbody>{perf_table}</tbody></table></div>
<p><a href="paper-v4-performance-summary.json">性能原始汇总/新增成本</a> · <a href="paper-v4-image-statistics.json">图像辅助统计</a> · <a href="paper-v4-forward_plus-acceptance.json">Forward+验收</a> · <a href="paper-v4-mobile-acceptance.json">Mobile验收</a> · <a href="paper-v4-gl_compatibility-acceptance.json">GL验收</a> · <a href="paper-v4-video-metadata.json">录像元数据</a></p><p>本地入口：<code>验收纸艺童话V4.ps1</code>。可切V3/A、B、C、D标准/鲜明，调色、局部光晕、减少动态、关闭动态或冻结拍照。实际棋子全路线、正式站口与目标手机仍待后续验收。</p></main>
<script>document.querySelectorAll('nav button').forEach(b=>b.addEventListener('click',()=>{{document.querySelectorAll('.group').forEach(s=>s.hidden=s.id!==b.dataset.group);document.querySelectorAll('nav button').forEach(x=>x.setAttribute('aria-pressed',String(x===b)));}}));</script></html>'''
write(OUT/'paper-v4-review.html',page)
bundle=OUT/'paper-v4-review-bundle';bundle.mkdir(exist_ok=True)
images={f for pairs in groups.values() for _,a,b,_,_ in pairs for f in [a,b]}
images|={f'paper-v4-video-frame-{t:02}.png' for t in [2,8,14,17]}
images|={f'paper-v4-{m}-{look}-{view}-{ui}.png' for m in METHODS for look in ['v3','D','D-vivid'] for view in ['overview','region','close'] for ui in ['clean','ui']}
files=images|{'paper-v4-actual-runtime.mp4','paper-v4-video-metadata.json','paper-v4-performance-summary.json','paper-v4-image-statistics.json','paper-v4-source-handoff.json','paper-v4-change-manifest.json'}|{f'paper-v4-{m}-acceptance.json' for m in METHODS}
files|={p.name for p in OUT.glob('paper-v4-*-benchmark-*.json')}
for name in files:
    source=OUT/name;assert source.is_file() and source.parent==OUT and source.suffix in {'.png','.json','.mp4'}
    shutil.copy2(source,bundle/name)
for source in [ROOT/'docs/art/PAPER_LOOKDEV_V4_REVIEW.md',HUB/'审核实施计划_v4.md',HUB/'V4逐项验收证据.md']:
    if not source.exists():continue
    portable_text=source.read_text(encoding='utf-8').replace('../../artifacts/','').replace('../../../AI协作_前端美术/审核实施计划_v4.md','审核实施计划_v4.md').replace('../equestria-star-client/docs/art/PAPER_LOOKDEV_V4_REVIEW.md','PAPER_LOOKDEV_V4_REVIEW.md')
    write(bundle/source.name,portable_text)
portable=page.replace('../docs/art/PAPER_LOOKDEV_V4_REVIEW.md','PAPER_LOOKDEV_V4_REVIEW.md').replace('../../AI协作_前端美术/审核实施计划_v4.md','审核实施计划_v4.md')
write(bundle/'index.html',portable)
print('V4_REVIEW_BUILT',len(sites),'sites;',len(benchmarks),'clean intervals;',len(files),'curated evidence files')
