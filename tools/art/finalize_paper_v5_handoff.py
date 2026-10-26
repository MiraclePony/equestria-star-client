"""Finalize local collaboration records only after the real V5 evidence exists."""
from pathlib import Path
from datetime import datetime, timezone, timedelta
import json, re, shutil

R=Path(__file__).resolve().parents[2]; O=R/'artifacts'; H=R.parent/'AI协作_前端美术'
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,text): p.write_text(text,encoding='utf-8')
def dump(p,obj): write(p,json.dumps(obj,ensure_ascii=False,indent=2)+'\n')
now=datetime.now(timezone(timedelta(hours=8))).isoformat(timespec='seconds')
verification=read(O/'paper-v5-review-verification.json')
assert verification['status']=='passed'
live=read(O/'paper-v5-live-preview.json'); audit=read(O/'paper-v5-live-motion-summary.json')
assert audit['status']=='passed' and live['windowHandle']!=0
perf=read(O/'paper-v5-performance-summary.json'); assert len(perf['rows'])==15
manifest=read(O/'paper-v5-change-manifest.json')
rows=[
 ('1.1','115文件V4现场、源v005与任务原文保留','.art-tools/lookdev-v5-baseline/；paper-v5-source-sync.log；外部审核/2026-10-25_V5/'),
 ('1.2','真实Gallery自动更新、注册、实例与Viewport','paper-v5-before-diagnostic.json；before-probe.log'),
 ('1.3','未复现缺注册/零时钟；约6–7px摆幅及面板遮挡','before-diagnostic.json完整周期；before-region-ui.png；motion-summary.json'),
 ('1.4','真正旧版三机位/纯背景，旧布景回退多角度；定位Sea/Floor','before-*-clean/ui.png；v4-fallback-angle-*.png；before-diagnostic.json'),
 ('2.1','静态Anchor→MotionPivot→原完整气球，世界幅度换算','SceneryDrift.cs；三后端验收JSON path/ID/Viewport/Update字段'),
 ('2.2','6.5秒完整周期；主区域及东/远专用可见机位均8–16px','三后端 normal-main-gallery-cycle / visible-camera-BalloonEast/Distant'),
 ('2.3','实际UI信号、2秒冻结0差、连续恢复、0.5秒平滑回静态、减少','三后端验收JSON/assertions；freeze-start/after-two-seconds.png'),
 ('2.4','重建保时保状态、近景只切小装饰、同一动画支点','OrigamiGallery.cs；ZoomArtDetailController.cs；三后端/zoom日志'),
 ('2.5','自动Gallery完整轨迹与普通交互窗口二次记录','paper-v5-*-acceptance.json；live-motion.json/summary.json/png'),
 ('3.1','真正Sea宽色区/长纸浪，岸线顶面绕序修复','paper_ocean.gdshader；CoastalShoreBuilder.cs；background-only.png'),
 ('3.2','低草坡/折岸/浅滩/共享陆架，原规则空隙不填平','PaperWorldBackground.cs；三机位及纯背景图；138格385边回归'),
 ('3.3','9组地区身份、近景主次形/专属林带','invariants.background；PaperWorldBackground.cs；angle-90/180/270.png'),
 ('3.4','低对比远山、4个新增三维云空域，多方向观察','world-overview.png；angle-*.png；background-only.png'),
 ('3.5','普通地块连续低频浅深纸片、低纸衬，保留边界','physical_paper.gdshader；V4/V5三机位同镜头截图'),
 ('4.1','V5 D/D+真曲线差异、白纸保护、固定曝光/方向','ArtRenderProfile.cs；PaperAtmosphere.cs；image-statistics.json'),
 ('4.2','三处局部光晕与几何空气层次，单调色/HUD/HDR2D保护','三后端uiColorDelta=0；ui-world-on/off.png；HDR2D断言'),
 ('4.3','云/气球/海面/74树冠/3旗布/星点纸叶统一时钟','PaperEnvironmentMotion.cs；三后端冻结像素0差；电影冻结区间'),
 ('4.4','38小物/17水纹/16批次与减少模式；新地形1批/海洋0透明层','invariants.motion；性能JSON；减少模式完整周期'),
 ('5.1','同机位V4/V5/D+清图/UI，纯背景、多角度、36主状态图','paper-v5-review.html；36张backend/preset/view/UI图片'),
 ('5.2','三后端自动运动及真实UI/重建/减少/裁剪/HDR','paper-v5-{forward_plus,mobile,gl_compatibility}-acceptance.json/log'),
 ('5.3','184根/138格/385边、落点/点击/净空/铁路及连续缩放缓存','RunEnvironmentArtTests/RunGameMapTests/RunHexCellTests/RunOrigamiArtTests/zoom日志'),
 ('5.4','实际主Gallery自动运行600帧/20秒；先固定10秒再冻结恢复/慢缩放','actual-runtime.mp4/avi；film.json/log；video-metadata.json；实际抽帧'),
 ('5.5','15区间各≥30秒独立性能，成本/CPU长尾如实保留','performance-summary.json；5份benchmark JSON；完整报告性能表'),
 ('5.6','421GLB/源哈希、导入/构建/静态/相关测试及正常退出','source-sync/build/static/landmark/environment/三后端/zoom日志'),
 ('6.1','真实原因、参数、变更归因、轨迹/图片/电影/性能','PAPER_LOOKDEV_V5_REVIEW.md；change-manifest/motion/performance JSON'),
 ('6.2','协作台/逐项证据/成功和失败日志，明确后续源制作边界','本文件；当前进度/目标与验收/待办与分工/进度流水；失败日志.gz'),
 ('6.3','限定证据目录本地页、视频、普通交互预览与实际窗口','review-verification.json；live-preview.json；live-motion-summary.json'),
 ('6.4','本轮可执行范围完成，无commit/push/PR/部署','HEAD d6800bd；只读upstream/origin同步；工具目标完成记录由主控最后补存'),
]
assert len(rows)==28
plan=H/'审核实施计划_v5.md'; text=plan.read_text(encoding='utf-8')
assert len(re.findall(r'^- \[[ x]\] ',text,re.M))==28
write(plan,text.replace('- [ ] ','- [x] ')+f'\n验收核对：{now}。本轮28项有实际证据，见[V5逐项验收证据](V5逐项验收证据.md)；长期手工地形精修、正式铁路端口与目标手机未冒称完成。\n')
evidence=f'''# V5逐项验收证据

{now}。6阶段28项，本轮实施范围已交付。表内不带目录的日志/JSON/图片位于客户端artifacts；旧版同机位图为编辑前真实V4，旧布景多角度/性能是当前程序集回退，二者明确区分。没有以手动Tick替代主Gallery自动运动。

| 项 | 已完成内容 | 主要证据 |
| --- | --- | --- |
'''+''.join(f'| {i} | {result} | {proof} |\n' for i,result,proof in rows)+'''
气球结论：原注册和时钟有效，旧摆幅约6–7px且西侧被展开的设置覆盖；未复现缺注册/时钟停走。现在三只实际模型全周期约10–12px，主区域只看到西侧，其余使用专用可见机位验证。正常主窗口另有实时自动审计；不把屏外对象说成当前可见。

性能是RTX 5000 Ada Laptop上的顺序独立采样，未锁定笔记本功耗/频率；CPU长尾与新增成本保留，不代表手机或发行包。GPU/视口CPU/准备CPU/墙钟分别记录，不相加推FPS。源码共享材质的退出错误已在三后端、连续缩放与长采样正常退出复验；原始失败输出压缩保留。

保留限制：新岸线/陆架/山形仍是艺术布景方案，外侧低山与轮廓可继续手工精修；13铁路物理端口、市政厅源标准化、真实棋子全路线、目标手机和发行包仍需后续，不因本轮目标完成而改变其状态。单套完整网格、原blend、候选、421GLB、138格385边和棋子/点击锚点保留。
'''
write(H/'V5逐项验收证据.md',evidence)
progress=f'''# V5本地验收就绪

{now}。本聊天Codex主控。已按[6阶段28项计划](审核实施计划_v5.md)完成，逐项见[证据表](V5逐项验收证据.md)，参数、原因与性能见[客户端报告](../equestria-star-client/docs/art/PAPER_LOOKDEV_V5_REVIEW.md)。没有提交、上传、改PR或部署。

先运行客户端根目录`验收纸艺世界V5.ps1`，默认Forward+ / V5 D（5）、区域B3、动态自动播放、设置收起。左上角按钮或Tab展开设置，可切D+（6）、冻结、减少、旧背景对照；滚轮连续缩放。普通交互主窗口已打开，独立审计记录了真实实例路径/ID/Viewport、屏幕轨迹与相机/锚点稳定。

本地对照页：[V5画面与20秒实机录像](http://127.0.0.1:8769/)。证据仅从`artifacts/paper-v5-review-bundle`提供，未开放整个制作目录。程序关闭后可重新运行入口，证据页服务器是独立本地进程。

本轮实质修改：气球原约6–7px摆幅/面板遮挡已改为MotionPivot、6.5秒、约10–12px；关闭0.5秒平滑回静态，冻结2秒0差，重建保时。海面大色区/慢纸浪、岸线顶面绕序、近岸低陆架、9组分区主次山形/低对比远山、专属林带/4云，以及真实有效D/D+。184主根/138格/385边和421GLB/源v005保持。

验收：三后端×两风格×三机位×有无UI共36主图；纯背景与多角度；自动完整周期/真实开关/冻结/减少/重建/HDR2D/HUD；20秒实际Gallery录像；15条件各≥30秒独立性能；C#构建0警告0错误、源哈希/GLB/缩放/布局/交互回归、正常退出。CPU长尾/新增成本原样报告，不宣称手机达标。

仍需后续：外围轮廓与手工折面精修、13正式铁路端口、市政厅制作源标准化、真实棋子全路线和目标手机/发行包。新地貌与岸线为方案，不新增玩法关系。V4历史见[归档](历史_验收v4.md)，不能用旧“约18秒录像”覆盖当前20秒证据。
'''
write(H/'当前进度.md',progress)
write(H/'目标与验收.md',f'''# 本轮目标与验收 · V5

{now}。用户V5任务全文已读、先计划后执行；[6阶段28项](审核实施计划_v5.md)本轮范围已完成，见[逐项证据](V5逐项验收证据.md)和[主报告](../equestria-star-client/docs/art/PAPER_LOOKDEV_V5_REVIEW.md)。工具目标由主控在最后确认窗口与文件后关闭。

已交付真实主Gallery动态诊断/修复与正常窗口审计、外围近中远/海岸海面组织、D/D+、统一动态与平滑开关、三后端组合回归、600帧20秒实机录像、15条件各30秒性能、421GLB/源哈希/布局与连续缩放、协作台和完整成功/失败日志。

保护：单套完整网格/原源稿/候选/树根/棋子/138格385边/铁路中心线；没有提交上传。旧版回退性能明确注明是同一当前程序集，真实编辑前图片另存。长期人工地理精修、13端口、真实棋子全路线及手机实测仍列后续，未被本轮完成标记掩盖。
''')
readme=H/'README.md'; text=readme.read_text(encoding='utf-8')
text=text.replace('当前已到D纸艺童话本地验收V4。先读 [6阶段30项计划](审核实施计划_v4.md) 与 [完整证据报告](../equestria-star-client/docs/art/PAPER_LOOKDEV_V4_REVIEW.md)','当前已到V5纸艺世界与主预览动态本地验收。先读 [6阶段28项计划](审核实施计划_v5.md)、[逐项证据](V5逐项验收证据.md) 与 [完整报告](../equestria-star-client/docs/art/PAPER_LOOKDEV_V5_REVIEW.md)')
write(readme,text)
ownership=H/'待办与分工.md';text=ownership.read_text(encoding='utf-8')
addition='''## V5新增主控文件与接手入口

主控已完成本轮集成；协作者在下一轮改这些文件前先登记独立提案，勿覆盖未提交现场。

| 工作 | 文件 | 边界 |
| --- | --- | --- |
| 实际动态/开关/重建 | SceneryDrift.cs、PaperEnvironmentMotion.cs、OrigamiGallery.cs、ZoomArtDetailController.cs | 静态锚点不动、统一时间、同一MotionPivot；勿加第二个逐帧驱动 |
| 外围分区/海岸/海洋 | PaperWorldBackground.cs、ContinentalSceneryBuilder.cs、CoastalShoreBuilder.cs、paper_ocean.gdshader | 不新增碰撞/连边；岸线和新山形保留提案标记 |
| D/D+与纸面 | ArtRenderProfile.cs、PaperAtmosphere.cs、ConceptPaperStyle.cs、physical_paper.gdshader | 单调色、白纸/HUD保护、曝光固定 |
| Gallery材质退出 | PaperMaterialLifetime.cs、EnvironmentArtCatalog.cs、ZoomArtDetailController.cs、Gallery退出函数 | 只登记Gallery；场景脱离后去重释放。禁止运行中重复Dispose或Shader=null |
| 证据与协作 | RunPaperV5Probe/Acceptance.cs、build_paper_v5_review.py、V5入口、报告/协作台 | 主预览自动运行的轨迹/电影为事实；不得用手动Tick替代 |

手工精修可优先优化外围低山的形状重复、陆架岸线转折、皇城平台与马哈顿城市边缘；单独保存提案和多角度实图，保留现有实际锚点。当前程序布景没有升级为原作地理断言。

'''
if '## V5新增主控文件与接手入口' not in text:write(ownership,text.replace('# 待办与分工\n','# 待办与分工\n\n'+addition,1))
integration=R/'docs/art/INTEGRATION.md';text=integration.read_text(encoding='utf-8')
intro='''## 当前主线：V5本地验收就绪（优先于下方V4历史）

用户V5任务按[6阶段28项计划](../../../AI协作_前端美术/审核实施计划_v5.md)本地完成；[逐项证据](../../../AI协作_前端美术/V5逐项验收证据.md)、[完整报告](PAPER_LOOKDEV_V5_REVIEW.md)与协作台当前进度为交接入口。运行`验收纸艺世界V5.ps1`，默认Forward+ / D（5），D+（6）及V4对照保留；设置默认收起，左上角或Tab展开。真实Gallery自动气球完整周期约10–12px/6.5秒，冻结/平滑关闭/减少/重建保时；原约6–7px摆幅和面板遮挡已诊断，未复现缺注册/零时钟。

近岸陆架/真实岸线顶面、9组地区近远山/专属林带/4云、大色面海洋/慢纸浪与有效D/D+已接入。36主状态图、纯背景/多角度、三后端动态/UI、600帧20秒实机录像、15条件各30秒独立性能、源哈希/421GLB/布局/交互/连续缩放和正常退出均有原始证据；纸裙/山形/岸线仍是艺术方案。新材质退出登记覆盖临时近景和净空拒绝件，只在Gallery脱离全部场景后去重释放；旧的仅GC结论不再适用。

138格/385边、184主根、原blend/候选/单套完整网格/铁路中心线/棋子点击锚点保留。13站口、市政厅源标准化、真实棋子全路线、手工地形精修与目标手机/发行包仍是后续。未提交、推送、更新远端PR或部署。旧版图片为真实预修改输出；性能/多角度的V4回退为同一当前程序集，不伪称旧二进制实测。

'''
if '## 当前主线：V5本地验收就绪' not in text:write(integration,text.replace('\n\n','\n\n'+intro,1))
for src in (H/'V5逐项验收证据.md',H/'审核实施计划_v5.md'):
    shutil.copy2(src,O/'paper-v5-review-bundle'/src.name)
print('V5_HANDOFF_COMPLETE',len(rows),'evidence rows',now)
