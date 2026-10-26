"""Read the current environment delivery index; export changed split components only."""
import argparse
import json
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from export_origami import PROJECT, export_component, sha

parser = argparse.ArgumentParser()
parser.add_argument('--source-root', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
source_root = args.source_root.resolve()
index_path = source_root/'交付/环境资产/环境资产索引.json'
index_hash = sha(index_path)
index = json.loads(index_path.read_text(encoding='utf-8-sig'))
output = PROJECT/'assets/art/environment'
output.mkdir(parents=True, exist_ok=True)
catalog_path = output/'catalog.json'
previous = json.loads(catalog_path.read_text(encoding='utf-8')) if catalog_path.exists() else {}
previous = {e['id']: e for e in previous.get('assets', [])}
entries = []
for asset in index['assets']:
    if not asset.get('model'):
        continue
    # Shared assembly coordinates are intentionally preserved. No recentering of parts.
    sources = asset.get('parts') or [asset['model']]
    components = {}
    for i, relative in enumerate(sources):
        source = source_root/relative
        if not source.is_file():
            raise RuntimeError(f'Indexed source missing: {source}')
        key = f'part{i+1:02d}'
        dest = output/asset['id']/(key+'.glb')
        old = previous.get(asset['id'], {}).get('components', {}).get(key)
        if old and old['source'] == relative and old['sourceSha256'] == sha(source) and dest.exists() and old['sha256'] == sha(dest):
            components[key] = old
        else:
            components[key] = export_component(source, dest, source_root)
            print('EXPORTED', asset['id'], key, flush=True)
    entry = {'id':asset['id'], 'name':asset['name'], 'aliases':[], 'status':'local-placement-review',
        'sourceStatus':asset['status'], 'sourceReadme':asset['readme'], 'components':components}
    ports_path = (source_root/asset['model']).parent/'ports.json'
    if ports_path.exists():
        entry['ports'] = json.loads(ports_path.read_text(encoding='utf-8-sig'))['ports']
        entry['dependencies'] = [{'source':ports_path.relative_to(source_root).as_posix(),'sourceSha256':sha(ports_path)}]
    entries.append(entry)
    lines = [f'[gd_scene load_steps={len(components)+1} format=3]', '']
    for key, part in components.items():
        lines.append(f'[ext_resource type="PackedScene" path="{part["path"]}" id="{key}"]')
    lines += ['', '[node name="EnvironmentAsset" type="Node3D"]']
    for key in components:
        lines += ['', f'[node name="{key}" parent="." instance=ExtResource("{key}")]']
    (output/asset['id']/'visual.tscn').write_text('\n'.join(lines)+'\n', encoding='utf-8')
if sha(index_path) != index_hash:
    raise RuntimeError('Delivery index changed during export; rerun once stable.')
catalog = {'schemaVersion':1, 'sourceIndex':'交付/环境资产/环境资产索引.json', 'sourceIndexSha256':index_hash,
    'sourceUp':'Z', 'exportUp':'Y', 'visualApproval':False, 'assets':entries,
    'unavailable':[{'id':a['id'],'name':a['name']} for a in index['assets'] if not a.get('model')]}
catalog_path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print('ENVIRONMENT_CATALOG', len(entries), sum(len(e['components']) for e in entries), flush=True)
