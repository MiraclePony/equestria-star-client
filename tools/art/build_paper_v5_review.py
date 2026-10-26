"""Assemble genuine V5 Gallery evidence, never uploads or modifies Git."""
from pathlib import Path
from datetime import datetime,timezone,timedelta
import json,hashlib,shutil,subprocess,re,html
from PIL import Image
import numpy as np

R=Path(__file__).resolve().parents[2];O=R/'artifacts';H=R.parent/'AI协作_前端美术'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,s):p.write_text(s,encoding='utf-8')
def dump(p,d):write(p,json.dumps(d,ensure_ascii=False,indent=2)+'\n')
now=datetime.now(timezone(timedelta(hours=8))).isoformat(timespec='seconds')
methods=['forward_plus','mobile','gl_compatibility'];reports={m:read(O/f'paper-v5-{m}-acceptance.json') for m in methods}
assert all(d['status']=='passed' for d in reports.values())
before=read(O/'paper-v5-before-diagnostic.json');movie=read(O/'paper-v5-video-metadata.json');assert 19.95<=movie['durationSeconds']<=20.05
assert movie['sha256']==hashlib.sha256((O/'paper-v5-actual-runtime.mp4').read_bytes()).hexdigest()
rows=[]
for p in sorted(O.glob('paper-v5-*-benchmark-*.json')):
    assert p.stat().st_mtime>(O/'paper-v5-build.log').stat().st_mtime,p
    d=read(p);assert d['status']=='passed'
    rows.extend({'renderer':d['renderer'],**e} for e in d['evidence'])
assert len(rows)==15 and all(e['durationSeconds']>=30 and e['width']==1760 and e['height']==1000 for e in rows)
main=reports['forward_plus'];invariants=next(e for e in main['evidence'] if e.get('name')=='invariants')
def cycle(d,name):return next(e for e in d['evidence'] if e.get('name')==name)
normal=cycle(main,'normal-main-gallery-cycle')
old_ranges=[]
for path in sorted({e['path'] for e in before['samples']}):
    a=[e for e in before['samples'] if e['path']==path]
    old_ranges.append({'path':path,'worldYPeakToPeak':max(e['position'][1] for e in a)-min(e['position'][1] for e in a),'screenYPeakToPeak':max(e['screen'][1] for e in a)-min(e['screen'][1] for e in a)})
stats={}
for view in ['overview','region','close']:
    images={key:np.asarray(Image.open(O/name).convert('RGB')).astype(float) for key,name in [('V4',f'paper-v5-before-{view}-clean.png'),('D',f'paper-v5-forward_plus-5-{view}-clean.png'),('D+',f'paper-v5-forward_plus-6-{view}-clean.png')]}
    stats[view]={'V4_to_V5_meanByteDifference':float(np.abs(images['D']-images['V4']).mean()),'D_to_Dplus_meanByteDifference':float(np.abs(images['D+']-images['D']).mean()),'whiteFractions':{k:float((im.min(axis=2)==255).mean()) for k,im in images.items()}}
dump(O/'paper-v5-image-statistics.json',stats)
def perf_case(version,view):return next(e for e in rows if e['renderer']=='forward_plus' and e['baseline']==(version=='v4') and not e['reduced'] and e['view']==view)
deltas=[]
for view in ['overview','region','close']:
    a,b=perf_case('v4',view),perf_case('v5',view)
    deltas.append({'view':view,'viewportCpuP95Delta':b['cpuP95']-a['cpuP95'],'gpuP95Delta':b['gpuP95']-a['gpuP95'],'setupP95Delta':b['setupP95']-a['setupP95'],'motionP95Delta':b['motionP95']-a['motionP95'],'drawDelta':b['draws']-a['draws'],'memoryMiBDelta':(b['memoryBytes']-a['memoryBytes'])/1048576})
dump(O/'paper-v5-performance-summary.json',{'rows':rows,'V5minusV4Fallback':deltas,'conditions':'Same executable/RTX 5000 Ada Laptop/Windows/1760x1000. V4 geometry and grade fallback vs V5. 240 warmup frames then >=30 real seconds per interval. Actual automatic animation on. No screenshots, recording, assertions or log IO inside intervals.','semantics':'Viewport CPU excludes scripts/global setup. Setup and controller ticks separately measured. Wall is per ProcessFrame interval including scheduling. GPU 0 unavailable, not zero cost. Not target phone certification. Unlike old V4 TimeProcess second-max monitor, this run records direct frame/setup/tick timings.','comparisonLimit':'Fallback is current executable with V4 scenery/colour disabled branches; not a byte-for-byte old binary. Preserved old PNGs are genuine pre-edit V4 output. No percentages against old capture-heavy timing.'})
source_hashes=read(R/'.art-tools/lookdev-v5-baseline/file-hashes.json')
manifest={'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=R).decode().strip(),'baselineFiles':len(source_hashes),'changedSinceSnapshot':[p for p,digest in source_hashes.items() if (R/p).is_file() and hashlib.sha256((R/p).read_bytes()).hexdigest()!=digest],'newModules':['scripts/game/map/PaperWorldBackground.cs','scripts/art/PaperMaterialLifetime.cs','assets/shaders/paper_ocean.gdshader','tests/RunPaperV5Probe.cs','tests/RunPaperV5Acceptance.cs','tools/art/build_paper_v5_review.py','验收纸艺世界V5.ps1'],'noUpload':True,'sourceAssets':'421 GLBs and original blend/catalog candidate flags unchanged'}
assert manifest['head']=='d6800bd531c3b72e6194086a69a5855754f3fd7f'
manifest['newModules']+=['tools/art/finalize_paper_v5_handoff.py','tools/art/verify_paper_v5_review.py']
assert all((R/p).is_file() for p in manifest['newModules'])
dump(O/'paper-v5-change-manifest.json',manifest)
dump(O/'paper-v5-motion-summary.json',{'before':{'registered':before['registeredDrifts'],'clockAdvanced':True,'cameraStable':before['cameraStable'],'ranges':old_ranges,'conclusion':'Registration and automatic process were active. Screen bob was only 5.94–6.88px; expanded left inspector also occluded west balloon in region view. No zero-clock or missing-registration bug reproduced. V4 global image mean delta did not validate balloon-specific visible movement.'},'after':{m:{'normal':cycle(d,'normal-main-gallery-cycle')['ranges'],'eastVisible':cycle(d,'visible-camera-BalloonEast')['ranges'],'distantVisible':cycle(d,'visible-camera-BalloonDistant')['ranges'],'reduced':cycle(d,'reduced-main-gallery-cycle')['ranges']} for m,d in reports.items()},'policy':'Static anchor -> MotionPivot -> original complete mesh. Source scale/yaw inverted for world-sized offsets. 0.045D bob, 6.5s; freeze preserves pose, disable returns to rest over 0.5s, rebuild retains clock. Actual scene IDs/viewport/mesh centres/frame counters and full samples in per-backend JSON. Final interactive window emits separate live audit.'})
def fmt(v):return f'{v:.2f}' if v>0 else '不可用'
perf_table='\n'.join(f"| {e['renderer']} | {'V4回退' if e['baseline'] else 'V5'}{'减少' if e['reduced'] else ''}/{e['view']} | {fmt(e['cpuP95'])}/{fmt(e['cpuP99'])} | {fmt(e['gpuP95'])}/{fmt(e['gpuP99'])} | {fmt(e['setupP95'])}/{fmt(e['setupP99'])} | {fmt(e['wallP95'])}/{fmt(e['wallP99'])} | {e['motionP95']:.3f} | {e['draws']:.0f} | {e['memoryBytes']/1048576:.1f} |" for e in rows)
delta_table='\n'.join(f"| {e['view']} | {e['viewportCpuP95Delta']:+.2f} | {e['gpuP95Delta']:+.2f} | {e['setupP95Delta']:+.2f} | {e['motionP95Delta']:+.3f} | {e['drawDelta']:+.0f} | {e['memoryMiBDelta']:+.1f} |" for e in deltas)
balloon_table='\n'.join(f"| {e['name']} | {e['peakToPeakY']:.2f}px | {e['anchorStable']} |" for e in normal['ranges'])
groups_table='\n'.join(f"| {g['board']} | {g['kind']} | {g['near']} | {g['far']} |" for g in invariants['background'])
report=f'''# V5 · 主预览动态与外围纸艺世界整改

{now}。原Godot 4.6.2 .NET / OrigamiGallery主预览集成；没有单独演示场景替代主预览。入口`验收纸艺世界V5.ps1`，默认V5 D（5），D+（6）；保留V4 D/鲜明（3/4）和旧背景对照。未提交、上传、改远端PR或部署。

## 先看可验收结果

[真实对照与20秒录像](../../artifacts/paper-v5-review.html)。视频0–10秒固定区域观察，10–12秒真实UI冻结，12–14秒恢复，14–20秒慢缩放。全部运动由主Gallery自动_Process推进，没有手动Tick/SetClock替代。MovieWriter固定步长说明实际场景运行，不能用作实时FPS；600帧/30fps/20秒，1920×1080，只裁去原片预热与退出，原AVI、原始起始帧号和编码日志保留。

## 气球此前为何看起来不动

真正的V4主Gallery自动诊断发现11个SceneryDrift全已注册、时钟推进、相机不动；三气球完整周期世界Y峰峰约0.0495m，屏幕只有5.94–6.88px。未复现“没有注册/时钟为零”，不能将任务书潜在原因当事实。区域实图同时发现左侧展开的美术设置覆盖西侧气球；其余两只在该区域机位屏外。V4只检查整体图像推进2秒均差，不能证明某只可见气球足够明显。

本轮将布局根固定，原气囊/吊篮/吊带整体放在MotionPivot；只对支点写变换。世界0.045D幅度、6.5秒周期，转换父级缩放与朝向，所有气球保留确定错相。默认收起设置，可用左上角按钮或Tab展开；实际开关、冻结与减少连到主控制器，关闭0.5秒平滑回静态。重建保留Clock/权重/开关状态，缩放只影响独立小纸饰，没有主体高低模替换。

| 主区域机位实例 | 完整周期屏幕Y峰峰 | 静态根保持 |
| --- | ---: | --- |
{balloon_table}

主区域只直接看到西侧，另给东侧/远处真实实例的专用可见机位和完整周期，三后端均验证8–16px范围。实例路径/ID、Viewport、实际可见网格中心、世界/屏幕轨迹、更新时间在[原始验收JSON](../../artifacts/paper-v5-forward_plus-acceptance.json)及[轨迹汇总](../../artifacts/paper-v5-motion-summary.json)。冻结2秒像素均差{invariants['frozenPixelDelta']}，恢复继续原相位；减少模式保留相位并减幅，17个小动态容量可见数，非清空整个GPU分配。

## 背景的实际来源与修改

原大块青色来自`GeneratedCells/ContinentalScenery/Sea`平面，Y={before['seaY']}，不是清屏/天空；Floor Y={before['floorY']}更低。大陆纸层与岸线顶面绕序和上法线不一致，双面材质把上表面当背面着色，导致纸层/浅滩发暗。本轮修正真实顶面，纸层核心/折岸/浅滩才正常显现；旧版对照保留原绕序。没有只换Environment背景色充数。

海面换成一张不透明世界坐标Shader：浅青、蓝绿、深水大色区与宽纸浪，慢长波用统一paper_clock；不使用高频噪点、写实反射/折射或叠加大透明片。关闭动态时淡去波带而非把长时间相位倒卷。普通地块加入全局连续的低频浅深纸片，保留地区识别与六角边界。

外围低纸裙/浅滩连接原纸层，分区近景有共享脚部与主次形，外缘落到水位以下；远山降饱和/对比并后退，前侧低、后侧高。实图发现首轮新增山群像悬空孤石，已下沉远景脚部、加连贯低陆架并减少无意义重复圈；保留真实交付山体/台地，背景森林使用VEG006专属树，雪区使用VEG103雪松群。新增4个全三维纸云空域，原气球/云/鸟动效纳入同一管理器。新造地形合并为一个无阴影不透明网格；所有地貌/岸线标记为艺术方案，不声称新正剧地理。

| 区域锚点 | 背景身份 | 近形中心 | 远形中心 |
| --- | --- | --- | --- |
{groups_table}

保留138格/385边与规则空隙；没有新增碰撞、玩法连边、站口或正式桥。间隔仍是低处不可玩纸衬，固定边界没有填平为新地块。新景物不把所有地区接成共享河/海峡。纯背景图隐藏棋盘、规则提示、地标魔法效果后，单看山形、海面、岸线、林带与云；另给90/180/270度，验证背侧为实际几何。

## D与D+

曝光恒1，太阳方向恒(-50,-42,0)。V5 D主光1.36，D+1.40，未把太阳与曝光一起大幅拉高；同一条LDR调色，强度1.0，饱和度1.18/1.24，对比1.10/1.14，暖高光/蓝紫阴影分别加深，保留中性纸白保护。三处局部光晕0.68/0.95，无全屏白墙泛光、重暗角、颗粒或默认景深。远景空气层次由三维几何的距离/色值组织，未用全图雾洗白棋盘。

HUD实际可见色块/真实角色卡开关世界效果0差，HDR2D旁路恢复通过。保留单调色/屏幕拷贝位于HUD之下。原树根{invariants['roots']}、格子{invariants['cells']}、建筑/棋子/点击锚点不受装饰运动改变；原净空、交互、单套完整模型和缩放缓存回归另有日志。

## 干净性能与实际限制

240帧预热后每条件至少30秒，动画开启，1760×1000，无录屏/截图/断言/密集IO；共15个区间。RTX 5000 Ada Laptop/Windows，不代表手机。视口渲染CPU、全局帧准备、控制器直接计时和墙钟分别记录，不能相加推FPS；GPU缺失写不可用。V4对照是同一新程序集中的旧布景/调色回退；真正旧版静态截图单独保存，未拿旧含抓图样本算提升百分比。

| 后端 | 状态/机位 | 视口CPU P95/P99 ms | GPU P95/P99 ms | 准备CPU P95/P99 ms | 帧间隔P95/P99 ms | 动态P95 ms | draw峰值 | 显存MiB |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
{perf_table}

| 机位 | 视口CPU增量ms | GPU增量ms | 准备增量ms | 动态增量ms | draw差 | 显存差MiB |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
{delta_table}

保留原地区CPU长尾与本次新增开销；纸面主渲染和阴影仍有优化空间。上述是顺序单次样本，笔记本功耗/频率未锁定，不作稳定帧率承诺。主要新成本是背景树组与原模型表面数；新海面没有增加透明层，新地形只有一个合批，星点/纸叶/烟预算仍38，水纹17，减少后小物17。汇总尺寸校验曾拦下7756×4076的减少模式样本，已原样另存rejected-reduced-resolution，不参与表格；只锁定性能测试窗口为1760×1000并重测，普通预览仍可自由调整。后端/减少/冻结/UI/重建的原始JSON与成功/失败日志保留。

## 修复记录、源与交接

完整验收补测曾复现退出material_set_shader循环：仅清缓存/GC并不足以释放测试/旧场景残留的材质句柄。连续缩放又暴露了已淘汰近景副本及净空拒绝的临时资源未纳入记录，已在导入实例化和渐隐材质创建时登记。本轮只在Gallery会话跟踪这些句柄；场景QueueFree和渲染完成、缓存清空后，按实例ID去重一次释放，再退出同步。Mobile首次长采样另出现退出挂起，原日志/数据保留；改为异步等待终结器、让引擎主线程继续运行后，完整重测正常退出，逐步追踪记录显示释放与终结器均完成。不把首次被终止进程算作成功退出。没有运行中重复Dispose共享材质，也没有让普通在线场景新增永久引用；三后端/原片/连续缩放/全部最终长采样正常退出。另修正了专用观察机位使用控制器旧目标导致远处气球没进入画面的测试错误，未降低8–16px阈值。失败日志保留。

源索引v005没有更新，421个GLB/原blend/候选状态未改。没有让独立AI重做高低模。后续建模建议：近岸陆架继续精修为更自然的切纸轮廓；区域山形进一步使用手工大折面/准确平台，新增地形仍为程序布景提案。13铁路物理端口、市政厅源标准化、真实棋子全路线、目标手机与发行包属于后续，不伪称本轮已测。

源码快照`.art-tools/lookdev-v5-baseline/`；[变更清单](../../artifacts/paper-v5-change-manifest.json)；[6阶段28项计划](../../../AI协作_前端美术/审核实施计划_v5.md)。协作台当前进度/主控状态/原始日志继续由本聊天主控维护。HEAD保持{manifest['head']}，没有commit/push/改PR/部署。

官方依据：[逐帧处理](https://docs.godotengine.org/en/4.6/tutorials/scripting/idle_and_physics_processing.html)、[Camera3D与屏幕投影](https://docs.godotengine.org/en/4.6/classes/class_camera3d.html)、[Node搜索/处理](https://docs.godotengine.org/en/4.6/classes/class_node.html)、[环境/后处理](https://docs.godotengine.org/en/4.6/tutorials/3d/environment_and_post_processing.html)。具体颜色、8–16px和6.5秒是本工程实测校准。
'''
write(R/'docs/art/PAPER_LOOKDEV_V5_REVIEW.md',report)
pairs=[]
for view in ['overview','region','close']:
    pairs.append((f'{view} · 编辑前V4/现在V5',f'paper-v5-before-{view}-clean.png',f'paper-v5-forward_plus-5-{view}-clean.png'))
    pairs.append((f'{view} · D/D+',f'paper-v5-forward_plus-5-{view}-clean.png',f'paper-v5-forward_plus-6-{view}-clean.png'))
pairs+=[('纯背景：棋盘/提示/魔法地标隐藏','paper-v5-before-background-only.png','paper-v5-forward_plus-background-only.png'),('同一真实自动运行实例，0/3秒','paper-v5-forward_plus-automatic-balloon-0.png','paper-v5-forward_plus-automatic-balloon-90.png'),('世界90/270度','paper-v5-forward_plus-angle-90.png','paper-v5-forward_plus-angle-270.png'),('主场景东/远气球专用机位','paper-v5-forward_plus-visible-BalloonEast.png','paper-v5-forward_plus-visible-BalloonDistant.png'),('Forward+/Mobile','paper-v5-forward_plus-5-region-clean.png','paper-v5-mobile-5-region-clean.png'),('Forward+/Compatibility','paper-v5-forward_plus-5-region-clean.png','paper-v5-gl_compatibility-5-region-clean.png'),('实际HUD/卡片开关世界效果','paper-v5-forward_plus-ui-world-on.png','paper-v5-forward_plus-ui-world-off.png')]
cards=''.join(f'<article><h2>{html.escape(title)}</h2><div class="pair"><a href="{a}" target="_blank"><img loading="lazy" src="{a}"></a><a href="{b}" target="_blank"><img loading="lazy" src="{b}"></a></div></article>' for title,a,b in pairs)
page=f'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>小马国之星 V5 · 主预览动态与纸艺世界</title><style>body{{margin:0;background:#edf1e5;color:#294a4d;font:16px/1.7 system-ui,"Microsoft YaHei",sans-serif}}main{{max-width:1440px;margin:auto;padding:28px}}h1{{font-size:34px}}h2{{font-size:21px}}a{{color:#376b86}}video{{width:100%;background:#2d626a;border-radius:10px}}.note,article{{padding:20px;background:#fffcf0;border:1px solid #d2dfcf;border-radius:10px;margin:20px 0}}.pair{{display:grid;grid-template-columns:1fr 1fr;gap:15px}}img{{width:100%;display:block}}.frames{{display:grid;grid-template-columns:repeat(5,1fr);gap:8px}}@media(max-width:800px){{main{{padding:15px}}.pair{{grid-template-columns:1fr}}.frames{{grid-template-columns:1fr 1fr}}}}</style><main>
<p>V5本地验收 · {now} · 未提交/未上传</p><h1>会轻轻浮动的气球，延伸到棋盘外的纸艺世界</h1><p>主Gallery实际自动更新。海面、浅滩、纸层、分区近山/远山、林带和云共同组成背景；D/D+增强冷暖和局部魔法。</p><p><a href="../docs/art/PAPER_LOOKDEV_V5_REVIEW.md">完整报告/参数/性能</a> · <a href="paper-v5-motion-summary.json">此前原因与完整轨迹</a> · <a href="paper-v5-performance-summary.json">15组30秒性能</a> · <a href="paper-v5-change-manifest.json">文件变更</a></p>
<div class="note">实时游戏默认播放环境动态，左上角按钮/Tab展开设置。下方对照图是静态截图；上方录像可直接播放，先固定10秒，再冻结2秒、恢复、拉近。程序布景/岸线是艺术方案，规则空隙与铁路保持；没有新增正式站口。</div>
<h2>20秒实际主预览运行录像</h2><video controls loop preload="metadata" poster="paper-v5-video-frame-2.png"><source src="paper-v5-actual-runtime.mp4" type="video/mp4"></video><p><a href="paper-v5-actual-runtime.mp4" download>下载20秒MP4</a> · <a href="paper-v5-video-metadata.json">原片起始帧/编码元数据</a>。固定步长原工程录像，性能另采。</p><div class="frames">{''.join(f'<a href="paper-v5-video-frame-{t}.png" target="_blank"><img loading="lazy" src="paper-v5-video-frame-{t}.png" title="录像{t}秒实际抽帧"></a>' for t in ['2','10-5','11-5','13','19'])}</div>
{cards}<h2>实际运动与结构</h2><p>V4主预览有11个注册动态对象，时钟在推进，气球位移仅约6–7px；展开设置还会覆盖西侧气球。V5三只真实实例完整周期约10–12px，静态锚点不动；东侧和远处逐一用可见机位验证，不只测试独立对象。</p><p>三后端、冻结/恢复、平滑关闭、减少、重建保时、HUD/角色卡、缩放/净空/源校验有实际证据。原始JSON：{' · '.join(f'<a href="paper-v5-{m}-acceptance.json">{m}</a>' for m in methods)}。</p><p>本机15组30秒性能与新增开销见完整报告，保留CPU长尾；不代表手机。正式铁路端口、真实棋子全路线与手工地形精修列为后续。<a href="../../AI协作_前端美术/审核实施计划_v5.md">6阶段28项实施计划</a></p></main></html>'''
write(O/'paper-v5-review.html',page)
bundle=O/'paper-v5-review-bundle';bundle.mkdir(exist_ok=True)
files={a for _,a,b in pairs}|{b for _,a,b in pairs}|{f'paper-v5-video-frame-{t}.png' for t in ['2','10-5','11-5','13','19']}|{'paper-v5-actual-runtime.mp4','paper-v5-video-metadata.json','paper-v5-motion-summary.json','paper-v5-performance-summary.json','paper-v5-change-manifest.json','paper-v5-image-statistics.json'}|{f'paper-v5-{m}-acceptance.json' for m in methods}
files|={p.name for p in O.glob('paper-v5-*-benchmark-*.json')}
files|={f'paper-v5-{m}-{look}-{view}-{ui}.png' for m in methods for look in [5,6] for view in ['overview','region','close'] for ui in ['clean','ui']}
files|={f'paper-v5-forward_plus-v4-fallback-angle-{a}.png' for a in [0,90,180,270]}
for name in files:
    src=O/name;assert src.is_file() and src.suffix in {'.png','.json','.mp4'};shutil.copy2(src,bundle/name)
for src in [R/'docs/art/PAPER_LOOKDEV_V5_REVIEW.md',H/'审核实施计划_v5.md',H/'V5逐项验收证据.md']:
    if src.exists():write(bundle/src.name,src.read_text(encoding='utf-8').replace('../../artifacts/','').replace('paper-v5-review.html','index.html').replace('../../../AI协作_前端美术/审核实施计划_v5.md','审核实施计划_v5.md'))
write(bundle/'index.html',page.replace('../docs/art/PAPER_LOOKDEV_V5_REVIEW.md','PAPER_LOOKDEV_V5_REVIEW.md').replace('../../AI协作_前端美术/审核实施计划_v5.md','审核实施计划_v5.md'))
print('V5_REVIEW_ASSEMBLED',len(rows),'long intervals,',len(files),'curated evidence files')
