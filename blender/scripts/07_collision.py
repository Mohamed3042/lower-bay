"""Separate unbeveled static collision proxies; never use high-poly render modifiers."""
import sys,hashlib,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import bpy
from lowerbay_common import BUILD,load,checkpoint,save
def main():
    assert Path(bpy.data.filepath).resolve()==(BUILD/'blender/map_master.blend').resolve()
    assert bpy.context.scene.name=='LevelLowerBay'
    coll=bpy.data.collections['MAP_COLLISION']
    excluded={'ad','graf','sign','lamp','glass'}
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and not o.hide_render and o.get('source_tag') not in excluded]
    signature=hashlib.sha256(json.dumps([(o.name,[list(v.co) for v in o.data.vertices],[list(p.vertices) for p in o.data.polygons]) for o in sorted(objects,key=lambda o:o.name)]).encode()).hexdigest()
    if coll.get('source_signature')==signature:
        print('Collision sources unchanged; no-op.')
        return
    checkpoint('before_collision_proxies')
    if coll.objects:
        archive=bpy.data.collections.new('LB_ARCHIVE_COLLISION');bpy.context.scene.collection.children.link(archive)
        archive.hide_render=True;archive.hide_viewport=True
        for o in list(coll.objects):
            archive.objects.link(o);coll.objects.unlink(o);o.name='ARCHIVE_'+o.name
    coll['source_signature']=signature
    count=0;triangles=0
    for src in objects:
        mesh=src.data.copy();mesh.name='COL_'+src.data.name
        obj=bpy.data.objects.new('COL_'+src.name,mesh);coll.objects.link(obj);obj.matrix_world=src.matrix_world.copy()
        obj['owner_map']='11_lowerbay';obj['unity_layer']=8
        obj['unity_tag']='Metal' if any(k in src.name.lower() for k in ('rail','beam','train','steel','grate')) else 'Cement'
        obj['is_trigger']=False;obj['collision_source']=src.name
        obj.hide_render=True;obj.hide_set(True);obj.display_type='WIRE'
        mesh.calc_loop_triangles();triangles+=len(mesh.loop_triangles);count+=1
    coll.hide_render=True;coll.hide_viewport=True
    save('collision_proxies',{'gate':9,'status':'Proxy meshes generated; runtime capsule/route tests still required',
     'collision_objects':count,'triangles':triangles,'high_poly_modifiers_excluded':True,
     'layer':8,'triggers':0},False)

main()
