"""Verify the curated review, genuine film and ordinary interactive Gallery audit."""
from pathlib import Path
import json, re, urllib.request, hashlib, subprocess
from datetime import datetime, timezone
from PIL import Image
import numpy as np

R=Path(__file__).resolve().parents[2]; O=R/'artifacts'; B=O/'paper-v5-review-bundle'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def dump(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
data=read(O/'paper-v5-live-motion.json'); snapshots=data['snapshots']
# Launch script selects B3 region directly. Allow first settling frame, not camera drift
# during the actual cycle; live samples include it for transparency.
assert len(snapshots)>=40 and snapshots[-1]['seconds']-snapshots[0]['seconds']>=8
assert all(s['enabled'] and not s['frozen'] and not s['settingsVisible'] for s in snapshots)
assert len({s['camera'] for s in snapshots})==1
assert snapshots[-1]['clock']-snapshots[0]['clock']>=8
balloons=[]
for name in sorted({b['name'] for s in snapshots for b in s['balloons']}):
    a=[b for s in snapshots for b in s['balloons'] if b['name']==name]
    assert len({b['anchorId'] for b in a})==1 and len({tuple(b['anchor']) for b in a})==1
    assert a[-1]['Updates']>a[0]['Updates'] and a[-1]['LastFrame']>a[0]['LastFrame']
    seen=all(b['visible'] and b['visibleMeshes']>0 and b['inViewport'] for b in a)
    amplitude=max(b['screen'][1] for b in a)-min(b['screen'][1] for b in a)
    if seen:assert 8<=amplitude<=16,(name,amplitude)
    balloons.append({'name':name,'anchorPath':a[0]['anchorPath'],'anchorId':a[0]['anchorId'],'pivotPath':a[0]['pivotPath'],'pivotId':a[0]['pivotId'],'viewport':a[0]['viewport'],'visibleMeshes':a[0]['visibleMeshes'],'inActualWindowThroughout':seen,'screenYPeakToPeak':amplitude,'worldYPeakToPeak':max(b['world'][1] for b in a)-min(b['world'][1] for b in a),'anchorStable':True,'firstUpdate':a[0]['Updates'],'lastUpdate':a[-1]['Updates'],'firstFrame':a[0]['LastFrame'],'lastFrame':a[-1]['LastFrame']})
assert any(b['name']=='BalloonWest' and b['inActualWindowThroughout'] for b in balloons)
summary={'status':'passed','source':data['source'],'renderer':data['renderer'],'snapshots':len(snapshots),'seconds':snapshots[-1]['seconds']-snapshots[0]['seconds'],'clockAdvance':snapshots[-1]['clock']-snapshots[0]['clock'],'cameraStable':True,'settingsCollapsed':True,'balloons':balloons,'offscreenPolicy':'East/Distant not claimed visible in this window; dedicated actual cameras tested in three-backend acceptance JSON.'}
dump(O/'paper-v5-live-motion-summary.json',summary)
for p in ['paper-v5-live-motion.json','paper-v5-live-motion-summary.json','paper-v5-live-motion.png','paper-v5-live-preview.json']:
    src=O/p; assert src.exists(); (B/p).write_bytes(src.read_bytes())

movie=read(O/'paper-v5-video-metadata.json');assert movie['durationSeconds']==20 and movie['fps']==30
assert movie['sha256']==hashlib.sha256((B/'paper-v5-actual-runtime.mp4').read_bytes()).hexdigest()
freeze1=np.asarray(Image.open(O/'paper-v5-video-frame-10-5.png').convert('RGB')).astype(int)
freeze2=np.asarray(Image.open(O/'paper-v5-video-frame-11-5.png').convert('RGB')).astype(int)
freeze_diff=float(np.abs(freeze1-freeze2).mean())
# Lossy H264 frames may differ slightly even when the original frozen viewport is identical.
assert freeze_diff<1,freeze_diff
assert read(O/'paper-v5-performance-summary.json')['rows'].__len__()==15
main_pngs=[]
for method in ['forward_plus','mobile','gl_compatibility']:
    d=read(O/f'paper-v5-{method}-acceptance.json');assert d['status']=='passed' and not d['failures']
    for look in [5,6]:
        for view in ['overview','region','close']:
            for ui in ['clean','ui']:
                p=B/f'paper-v5-{method}-{look}-{view}-{ui}.png';assert Image.open(p).size==(1760,1000);main_pngs.append(p.name)
native_logs=[]
for p in sorted(O.glob('paper-v5-*.log')):
    if 'failure' in p.name:continue
    assert p.stat().st_size<100000,p
    text=p.read_text(encoding='utf-8-sig',errors='replace')
    assert 'material_set_shader' not in text,p
    if '-acceptance.log' in p.name or '-benchmark-' in p.name or p.name in {'paper-v5-film.log','paper-v5-baseline-angles.log'}:
        assert 'PAPER_V5_PASS' in text and 'ART_DETACHED_MATERIAL_HANDLES_RELEASED' in text,p
        native_logs.append(p.name)
    assert 'ERROR:' not in text or p.name=='paper-v5-RunGameMapTests.log',p
for name in ['index.html','PAPER_LOOKDEV_V5_REVIEW.md']:
    text=(B/name).read_text(encoding='utf-8')
    links=re.findall(r'(?:href|src)="([^"]+)"',text) if name.endswith('.html') else re.findall(r'\]\(([^)]+)\)',text)
    for link in links:
        if link.startswith(('http:','https:','#','mailto:')):continue
        target=(B/link.split('#')[0]).resolve()
        assert target.is_relative_to(B.resolve()) and target.exists(),(name,link)
base='http://127.0.0.1:8769/'
with urllib.request.urlopen(base) as response:assert response.status==200
with urllib.request.urlopen(urllib.request.Request(base+'paper-v5-actual-runtime.mp4',method='HEAD')) as response:
    assert response.status==200 and response.headers['Content-Type']=='video/mp4'
head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=R).decode().strip()
assert head=='d6800bd531c3b72e6194086a69a5855754f3fd7f'
result={'status':'passed','at':datetime.now(timezone.utc).isoformat(),'mainScreenshots':len(main_pngs),'film':movie,'encodedFrozenMeanByteDifference':freeze_diff,'nativeCleanExitLogs':native_logs,'liveAudit':'paper-v5-live-motion-summary.json','localReview':base,'serverScope':str(B),'noUpload':True,'head':head,'linkTargetsInsideCuratedBundle':True}
dump(O/'paper-v5-review-verification.json',result);dump(B/'paper-v5-review-verification.json',result)
print('V5_CURATED_REVIEW_AND_REAL_INTERACTIVE_AUDIT_PASS',len(main_pngs),'main images',len(balloons),'actual balloon IDs','freeze MP4 difference',freeze_diff)
