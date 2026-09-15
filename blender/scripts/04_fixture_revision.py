"""Replace emissive solid-box fixtures with recessed lenses and dimensional cages."""
import sys,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import bpy,bmesh
from lowerbay_common import BUILD,load,checkpoint,save
def main():
    assert Path(bpy.data.filepath).resolve()==(BUILD/'blender/map_master.blend').resolve()
    assert bpy.context.scene.name=='LevelLowerBay'
    s=load('module_catalog')['fixture_revision'];name='LB_FIXTURE_R'+str(s['revision'])
    if name in bpy.data.collections:
        print('Fixture revision already present; no-op.');return
    checkpoint('before_fixture_revision')
    old=bpy.data.objects['LB_SEED_lamp_lamp']
    archive=bpy.data.collections.new('LB_ARCHIVE_FIXTURES');bpy.context.scene.collection.children.link(archive)
    archive.objects.link(old)
    for c in list(old.users_collection):
        if c!=archive:c.objects.unlink(old)
    old.hide_render=True;old.hide_set(True);archive.hide_render=True;archive.hide_viewport=True
    out=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(out)
    parts=[p for p in json.loads((BUILD/'validation/measurements/seed_components.json').read_text()) if p['tag']=='lamp']
    for start in range(0,len(parts),s['batch_size']):
        vertices=[];faces=[];indices=[]
        def box(lo,hi,mat):
            a,b,c=lo;d,e,f=hi;n=len(vertices)
            vertices.extend([(a,-c,b),(d,-c,b),(d,-f,b),(a,-f,b),(a,-c,e),(d,-c,e),(d,-f,e),(a,-f,e)])
            faces.extend(tuple(n+i for i in p) for p in [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)])
            indices.extend([mat]*6)
        for part in parts[start:start+s['batch_size']]:
            x,y,z=part['min_obj'];X,Y,Z=part['max_obj'];cx=(x+X)/2;cz=(z+Z)/2
            if abs(cx)>32 and abs(cz)<1.1:z+=2-cz;Z+=2-cz
            w=s['frame_width_m'];back=Y-(Y-y)*s['back_fraction'];inset=s['lens_inset_m']
            box((x,back,z),(X,Y,Z),0)
            box((x,y,z),(X,back,z+w),0);box((x,y,Z-w),(X,back,Z),0)
            box((x,y,z+w),(x+w,back,Z-w),0);box((X-w,y,z+w),(X,back,Z-w),0)
            box((x+w,y+inset,z+w),(X-w,back-.006,Z-w),1)
            bars=s['cage_bars'] if abs((X-x)-(Z-z))<.1 else 2
            for i in range(1,bars+1):
                bx=x+(X-x)*i/(bars+1);b=s['bar_width_m']/2
                box((bx-b,y,z+w),(bx+b,y+inset*.7,Z-w),0)
        mesh=bpy.data.meshes.new(name+'_MESH_'+str(start));mesh.from_pydata(vertices,[],faces)
        for m in s['materials']:mesh.materials.append(bpy.data.materials['LB_'+m])
        for p,mi in zip(mesh.polygons,indices):p.material_index=mi
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
        obj=bpy.data.objects.new(name+'_BATCH_'+str(start),mesh);out.objects.link(obj)
        obj['owner_map']='11_lowerbay';obj['source_tag']='lamp'
        mod=obj.modifiers.new('LB_FIXTURE_EDGE','BEVEL');mod.width=s['bevel_m'];mod.segments=s['bevel_segments'];mod.limit_method='ANGLE'
    save('fixture_revision',{'gate':6,'status':'83 recessed/caged fixtures; unchanged envelopes, source archived',
     'fixtures':len(parts),'batches':len(out.objects),'clearance_lamp_offsets_preserved':True},False)

main()
