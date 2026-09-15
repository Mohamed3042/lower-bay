"""Intermediate Unity-2022 visual handoff, NOT final Gate-10/runtime certification."""
import sys,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import bpy
from lowerbay_common import BUILD,load
assert bpy.context.scene.name=='LevelLowerBay'
assert Path(bpy.data.filepath).resolve()==(BUILD/'blender/map_master.blend').resolve()
dest=BUILD/'export/preview'
dest.mkdir(parents=True,exist_ok=True)
dg=bpy.context.evaluated_depsgraph_get()
def xyz(v):return {'x':round(-v.x,6),'y':round(v.z,6),'z':round(-v.y,6)}
result={'status':'INTERMEDIATE VISUAL PREVIEW; no final gameplay or bake certification','meshes':[],
        'materials':load('materials')['families'],'lamps':load('lighting').get('preview_fixture_lamps',load('lighting')['lamps']),
        'povs':load('lighting')['povs']+load('lighting').get('review_povs',[])}
result['spawns']=load('gameplay_objects')['spawns']
result['routes']=load('routes')['routes']
result['collisions']=[]
for obj in bpy.data.collections['MAP_COLLISION'].objects:
    mesh=obj.data;mesh.calc_loop_triangles()
    result['collisions'].append({'name':obj.name,'vertices':[xyz(obj.matrix_world@v.co) for v in mesh.vertices],
      'triangles':[i for tri in mesh.loop_triangles for i in (tri.vertices[0],tri.vertices[2],tri.vertices[1])],
      'layer':obj['unity_layer'],'tag':obj['unity_tag']})
for obj in sorted(bpy.context.scene.objects,key=lambda o:o.name):
    if obj.type!='MESH' or obj.hide_render:continue
    ev=obj.evaluated_get(dg);mesh=ev.to_mesh();mesh.calc_loop_triangles()
    item={'name':obj.name,'sourceTag':obj.get('source_tag','architecture'),'vertices':[],'normals':[],'uv':[],'triangles':[],
          'triMaterials':[],'materials':[m.get('source_material',m.name.removeprefix('LB_')) for m in mesh.materials]}
    normalmat=obj.matrix_world.to_3x3().inverted().transposed()
    for tri in mesh.loop_triangles:
        n=normalmat@tri.normal
        for idx in (tri.vertices[0],tri.vertices[2],tri.vertices[1]):
            p=obj.matrix_world@mesh.vertices[idx].co
            item['vertices'].append(xyz(p));item['normals'].append(xyz(n.normalized()))
            # Per-face duplicated vertices prevent UV projection seams overwriting shared corners.
            if abs(n.z)>=max(abs(n.x),abs(n.y)):u,v=p.x,p.y
            elif abs(n.x)>=abs(n.y):u,v=p.y,p.z
            else:u,v=p.x,p.z
            item['uv'].append({'x':round(u,6),'y':round(v,6)})
            item['triangles'].append(len(item['triangles']))
        item['triMaterials'].append(tri.material_index)
    result['meshes'].append(item);ev.to_mesh_clear()
path=dest/'lowerbay_preview.json'
path.write_text(json.dumps(result,separators=(',',':')))
print(json.dumps({'path':str(path),'meshes':len(result['meshes']),'triangles':sum(len(m['triMaterials']) for m in result['meshes']),
                  'bytes':path.stat().st_size,'coordinate_conversion':'explicit Blender->Unity reflection + reversed winding'}))
