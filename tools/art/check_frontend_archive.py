"""Check archive compatibility against the original client documents; no code formatting."""
from pathlib import Path
import json, re, subprocess, hashlib, fnmatch, xml.etree.ElementTree as ET
from datetime import datetime, timezone

R=Path(__file__).resolve().parents[2]
checks=[]; failures=[]
def check(name, ok, detail):
    checks.append({'name':name,'passed':bool(ok),'detail':detail})
    if not ok:failures.append(name)
def text(name):return (R/name).read_text(encoding='utf-8-sig')
def git(*args):return subprocess.check_output(['git',*args],cwd=R).decode('utf-8')
docs=['小马国之星前端重构需求与后端接口交接文档.md','小马国之星_前端接口文档.md',
      '小马国之星-前端地图中文映射交接文档.md','小马国之星-前端中文映射交接文档.md',
      '小马国之星行动结果与玩家可读日志接口交接说明.md','前端接口同步说明_20260708.md',
      'API接口清单.md','ES完整API文档.md']
doc_hashes=[]
for name in docs:
    raw=(R/name).read_bytes();raw.decode('utf-8-sig')
    same=not git('diff','HEAD','--',name).strip()
    check('Original document: '+name,same,'Original interface/handoff document preserved unchanged.')
    doc_hashes.append({'path':name,'sha256':hashlib.sha256(raw).hexdigest()})
project=text('project.godot');csproj=ET.fromstring(text('EquestriaStar.csproj'))
check('Engine and runtime',csproj.attrib.get('Sdk')=='Godot.NET.Sdk/4.6.2' and csproj.find('.//TargetFramework').text=='net8.0','Godot 4.6.2 .NET / C# / .NET 8.')
check('Normal application entry','run/main_scene="res://scenes/app/App.tscn"' in project,'Offline OrigamiGallery does not replace login/application entry.')
check('Existing API/state boundary unchanged',not git('diff','--name-only','HEAD','--','scripts/api','scripts/state').strip(),'No art change to protocol DTOs, HTTP request serialization, session state or credentials handling.')
api=text('scripts/api/ApiClient.cs')
check('UTF8 and envelope conventions','Encoding.UTF8.GetString(body)' in api and 'PropertyNameCaseInsensitive = true' in api and 'envelope.Code != 1' in api,'Existing UTF8 JSON, case-insensitive response binding and code/data/msg/errorCode handling retained; covered by RunUnitTests.')
check('Existing request field names','new { username, password }' in api and 'new { roomName, maxPlayers }' in api and 'new { selectedCharacterId }' in api,'Requests already use explicitly named lower-camel anonymous properties; no global naming policy is required by the original documents.')
footer=text('scenes/app/App.tscn');app=text('scripts/ui/App.cs')
check('Non-game footer and player text','custom_minimum_size = Vector2(0, 52)' in footer and '当前玩家：' in app and '未登录' in app,'52 px footer falls in original suggested 48–64 range. Game-page exception is recorded separately.')
check('Map Chinese display precedence','string.IsNullOrWhiteSpace(CellNameText) ? CellName : CellNameText' in text('scripts/game/map/HexCellData.cs'),'Display name prefers cellNameText; protocol identity/coordinates remain separate.')
for directory in ['scripts/api','scripts/state','scripts/ui','scripts/game/map','scenes/app','scenes/screens','scenes/game/map','assets/art']:
    check('Directory: '+directory,(R/directory).is_dir(),'Existing Godot project directory organization retained.')

paths=sorted(set(filter(None,git('ls-files','--cached','--others','--exclude-standard','-z').split('\0'))))
resource_refs=0;json_files=0;partial_classes=0;utf8_files=0;case_tables={}
def exact_file(rel):
    current=R
    for part in Path(rel).parts:
        if part in {'.','..'}:return False
        if current not in case_tables:
            case_tables[current]={p.name for p in current.iterdir()} if current.is_dir() else set()
        if part not in case_tables[current]:return False
        current=current/part
    return current.is_file()
for name in paths:
    p=R/name
    if not p.is_file():continue
    if p.suffix in {'.cs','.tscn','.tres','.gdshader','.gd','.uid','.import','.json','.md','.ps1','.py','.cfg','.cmd'} or name=='project.godot':
        try:s=text(name);utf8_files+=1
        except UnicodeDecodeError:check('UTF8: '+name,False,'Invalid UTF8 text');continue
    else:continue
    if p.suffix=='.json':
        try:json.loads(s,parse_constant=lambda value:(_ for _ in ()).throw(ValueError(value)));json_files+=1
        except (ValueError,json.JSONDecodeError) as exc:check('JSON: '+name,False,str(exc))
    if p.suffix in {'.tscn','.tres'}:
        for rel in re.findall(r'path="res://([^"\n]+)"',s):
            resource_refs+=1
            if not exact_file(rel):check('Resource reference: '+name,False,rel+' missing or case mismatch')
    if p.suffix=='.cs':
        for classname in re.findall(r'public\s+partial\s+class\s+(\w+)',s):
            partial_classes+=1
            if classname!=p.stem:check('Script name: '+name,False,classname+' does not match filename')
check('UTF8 resource/source texts',True,f'{utf8_files} text files decoded strictly as UTF8.')
check('Strict JSON resource files',True,f'{json_files} JSON documents parsed; NaN/Infinity rejected.')
check('Scene/resource paths and case',True,f'{resource_refs} explicit scene/tres res:// paths checked, including exact case.')
check('Godot partial class/filename convention',True,f'{partial_classes} public partial classes checked.')
include=re.search(r'^include_filter="([^"]*)"',text('export_presets.cfg'),re.M).group(1).split(',')
runtime_json=[p.relative_to(R).as_posix() for p in (R/'assets/art').rglob('*.json')]
check('Runtime catalog export inclusion',all(any(fnmatch.fnmatchcase(p,pattern) for pattern in include) for p in runtime_json),runtime_json)

gaps=[
 {'id':'game-footer','original':'Original reconstruction document §2 suggests one fixed bottom player footer on all pages.','current':'App.ShowGame hides the global 52px footer; in-game GameHud displays player information.','baselineUnchanged':not git('diff','HEAD','--','scripts/ui/App.cs','scenes/app/App.tscn').strip()},
 {'id':'complete-action-flow','original':'Original reconstruction document §§15–17 describes the complete action/dialog/player-facing log/settlement experience.','current':'Current normal GameScreen mainly integrates server map, player HUD and refresh/retry. Full action/log/settlement UI is not comprehensively implemented or accepted by this art archive.','scope':'Existing frontend feature scope gap; no claim of complete online gameplay acceptance.'},
 {'id':'historical-endpoints','original':'Some early documents describe owner-start and an older room flow.','current':'README explicitly makes ES完整API文档.md, API接口清单.md and current protocol implementation authoritative for ready/selection/automatic start; do not restore a removed endpoint to satisfy an older example.'},
]
out={'status':'failed' if failures else 'compatibility-check-passed-with-existing-gaps','atUtc':datetime.now(timezone.utc).isoformat(),'checks':checks,'failures':failures,'originalDocuments':doc_hashes,'existingGaps':gaps,'scope':'Archive format/resources and art-delta compatibility review, not complete frontend functional/online acceptance. No repository-wide formatter/editorconfig is defined; no invented style rule or bulk code rewrite applied.'}
(R/'artifacts').mkdir(exist_ok=True)
encoded=json.dumps(out,ensure_ascii=False,indent=2)+'\n'
(R/'artifacts/archive-2026-10-26-frontend-format.json').write_text(encoded,encoding='utf-8')
(R/'docs/art/archive-2026-10-26-format-check.json').write_text(encoded,encoding='utf-8')
print(json.dumps({'status':out['status'],'checks':len(checks),'utf8Files':utf8_files,'jsonFiles':json_files,'sceneReferences':resource_refs,'partialClasses':partial_classes,'failures':failures,'existingGaps':len(gaps)},ensure_ascii=False))
raise SystemExit(1 if failures else 0)
