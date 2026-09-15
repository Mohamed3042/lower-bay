import sys,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import bpy,bmesh
from lowerbay_common import BUILD,load,checkpoint,save
assert Path(bpy.data.filepath).resolve()==(BUILD/'blender/map_master.blend').resolve()
assert bpy.context.scene.name=='LevelLowerBay'
s=load('module_catalog')['clearance_revision']
assert 'LB_CLEARANCE_R1' not in bpy.data.collections,'Revision already applied; do not duplicate'
checkpoint('before_clearance_revision')
archive=bpy.data.collections.new('LB_ARCHIVE_CLEARANCE_R1');bpy.context.scene.collection.children.link(archive)
for name in s['archive_names']:
    o=bpy.data.objects[name];archive.objects.link(o)
    for c in list(o.users_collection):
        if c!=archive:c.objects.unlink(o)
    o.hide_render=True;o.hide_set(True)
archive.hide_render=True;archive.hide_viewport=True
out=bpy.data.collections.new('LB_CLEARANCE_R1');bpy.context.scene.collection.children.link(out)
def batch(name,boxes,materials):
    vs=[];fs=[];ms=[]
    for low,high,mi in boxes:
        x0,y0,z0=low;x1,y1,z1=high;n=len(vs)
        vs.extend([(x0,-z0,y0),(x1,-z0,y0),(x1,-z1,y0),(x0,-z1,y0),(x0,-z0,y1),(x1,-z0,y1),(x1,-z1,y1),(x0,-z1,y1)])
        fs.extend(tuple(n+i for i in f) for f in [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)])
        ms.extend([mi]*6)
    mesh=bpy.data.meshes.new(name+'_MESH');mesh.from_pydata(vs,[],fs)
    for m in materials:mesh.materials.append(bpy.data.materials['LB_'+m])
    for face,mi in zip(mesh.polygons,ms):face.material_index=mi
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    o=bpy.data.objects.new(name,mesh);o['owner_map']='11_lowerbay';out.objects.link(o)
    mod=o.modifiers.new('LB_EDGE_FINISH','BEVEL');mod.width=s['bevel_m'];mod.segments=s['bevel_segments'];mod.limit_method='ANGLE';mod.use_clamp_overlap=True
    return o
boxes=[]
for x,z in s['concourse_centres_obj_xz']:
    w=s['concourse_width']/2
    for low,high,mi in [(0,s['dado_top'],1),(s['dado_top'],s['capital_bottom'],0),(s['capital_bottom'],s['concourse_top'],2)]:
        boxes.append(([x-w,low,z-w],[x+w,high,z+w],mi))
batch('LB_CONCOURSE_PIERS_22',boxes,['tile','skirt','conc_d'])
boxes=[]
for x in s['beam_centres_x']:
    for loz,hiz in [(s['beam_span_z'][0],s['passage_z'][0]),(s['passage_z'][1],s['beam_span_z'][1])]:
        w=s['beam_width']/2;web=s['web_width']/2;low=s['beam_bottom_y'];high=s['beam_top_y'];th=s['flange_thickness']
        boxes.extend([([x-w,low,loz],[x+w,low+th,hiz],0),([x-web,low+th,loz],[x+web,high-th,hiz],0),([x-w,high-th,loz],[x+w,high,hiz],0)])
    low=s['end_clearance_bottom_y'] if abs(x)>s['end_threshold_x'] else s['central_clearance_bottom_y']
    boxes.append(([x-s['beam_width']/2,low,s['passage_z'][0]],[x+s['beam_width']/2,s['beam_top_y'],s['passage_z'][1]],0))
batch('LB_HAUNCHED_CLEARANCE_BEAMS_15',boxes,['beam'])
parts=json.loads((BUILD/'validation/measurements/seed_components.json').read_text())
shifted=[]
for p in parts:
    lo,hi=p['min_obj'],p['max_obj'];cx=(lo[0]+hi[0])/2;cz=(lo[2]+hi[2])/2
    dx=dy=dz=0
    if p['tag']=='sign' and abs(cz)<1.1 and hi[1]>3:
        dx=(s['sign_gallery_x'] if cx>=0 else -s['sign_gallery_x'])-cx
        dz=(s['sign_side_z'] if cx>=0 else -s['sign_side_z'])-cz
        dy=s['sign_bottom_y']-lo[1]
    elif p['tag']=='lamp' and abs(cx)>s['end_threshold_x'] and abs(cz)<1.1:
        dz=(s['end_lamp_side_z'] if cz>=0 else -s['end_lamp_side_z'])-cz
    else:continue
    obj=bpy.data.objects.get('LB_SEED_'+p['tag']+'_'+p['material'])
    if not obj:continue
    count=0
    for v in obj.data.vertices:
        q=(v.co.x,v.co.z,-v.co.y)
        if all(lo[i]-.0001<=q[i]<=hi[i]+.0001 for i in range(3)):
            v.co.x+=dx;v.co.z+=dy;v.co.y-=dz;count+=1
    obj.data.update();shifted.append({'object':obj.name,'vertices':count,'delta_obj':[dx,dy,dz]})
bpy.context.view_layer.update()
save('geometry_clearance_revision',{'gate':4,'status':'Construction corrections saved; capsule sweep still required',
 'concourse_piers':22,'haunched_beams':15,'floor_or_route_layout_changes':0,'shifted_components':shifted,
 'end_beam_headroom_m':s['end_clearance_bottom_y']-3.4},False)
