import sys,runpy,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import bpy
from lowerbay_common import BUILD
assert Path(bpy.data.filepath).resolve()==(BUILD/'blender/map_master.blend').resolve()
before=(len(bpy.data.objects),len(bpy.data.meshes),len(bpy.data.collections))
for script in ('04_train_revision.py','04_fixture_revision.py','04_label_revision.py','07_collision.py'):
    runpy.run_path(str(BUILD/'blender/scripts'/script),run_name='__main__')
after=(len(bpy.data.objects),len(bpy.data.meshes),len(bpy.data.collections))
assert before==after,(before,after)
report={'status':'PASS four revision replays create no duplicate objects, meshes or collections',
 'before':before,'after':after}
(BUILD/'validation/measurements/revision_replay.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
