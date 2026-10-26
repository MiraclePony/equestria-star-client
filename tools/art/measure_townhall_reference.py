"""Read-only measurement of the unstandardized city hall; never exports it into gameplay."""
import bpy
import json
from pathlib import Path
from mathutils import Vector

root=Path(__file__).resolve().parents[2]
source=root.parent/'折纸市政厅2.blend'
with bpy.data.libraries.load(str(source),link=False) as (available,loaded):
    loaded.objects=available.objects
objects=[]
for obj in loaded.objects:
    if obj is None or obj.type!='MESH': continue
    corners=[obj.matrix_world@Vector(v) for v in obj.bound_box]
    lo=[min(v[i] for v in corners) for i in range(3)]
    hi=[max(v[i] for v in corners) for i in range(3)]
    objects.append({'name':obj.name,'min':lo,'max':hi,'size':[hi[i]-lo[i] for i in range(3)],'faces':len(obj.data.polygons),'hide_render':obj.hide_render,'hide_viewport':obj.hide_viewport,'modifiers':[m.type for m in obj.modifiers]})
result={'source':str(source),'status':'unstandardized-reference-only-not-installed','objects':objects,'note':'Object bounds include authoring scene objects; do not infer a canonical gameplay scale without selecting the actual assembly.'}
(root/'artifacts/paper-v3-townhall-measurement.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('TOWNHALL_REFERENCE_MEASURED',len(objects))
