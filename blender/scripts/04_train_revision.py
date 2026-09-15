import sys
import math
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import bpy
from lowerbay_common import BUILD,load,checkpoint,save

def main():
    assert Path(bpy.data.filepath).resolve() == (BUILD/'blender/map_master.blend').resolve()
    assert bpy.context.scene.name == 'LevelLowerBay'
    s=load('module_catalog')['train_revision']
    revision='LB_TRAIN_R'+str(s['revision'])
    if revision in bpy.data.collections:
        print('Train revision already present; no-op.')
        return
    checkpoint('geometry_before_train_revision')
    archive=bpy.data.collections.new('LB_ARCHIVE_BEFORE_TRAIN_R'+str(s['revision']))
    bpy.context.scene.collection.children.link(archive)
    old=[o for o in bpy.context.scene.objects if not o.hide_render and o.get('source_tag') in (s['source_archive_tag'],'hero_train')]
    assert len(old) in (1,4)
    for o in old:
        archive.objects.link(o)
        for c in list(o.users_collection):
            if c!=archive:c.objects.unlink(o)
        o.hide_render=True
        o.hide_set(True)
    archive.hide_render=True
    archive.hide_viewport=True
    out=bpy.data.collections.new(revision)
    bpy.context.scene.collection.children.link(out)
    verts=[]; faces=[]; mats=[]
    def poly(points, polygons, mat):
        start=len(verts)
        verts.extend((x,-z,y) for x,y,z in points)
        faces.extend(tuple(start+i for i in f) for f in polygons)
        mats.extend([mat]*len(polygons))
    def box(x,y,z,dx,dy,dz,mat):
        points=[(x+a*dx/2,y+b*dy/2,z+c*dz/2) for a,b,c in [(-1,-1,-1),(1,-1,-1),(1,-1,1),(-1,-1,1),(-1,1,-1),(1,1,-1),(1,1,1),(-1,1,1)]]
        # Source Y-up, outward faces; transform to Blender is a proper rotation.
        poly(points,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],mat)
    def cylinder(x,y,z,r,depth,segments,axis,mat):
        points=[]
        for a in [-depth/2,depth/2]:
            for i in range(segments):
                t=2*math.pi*i/segments
                if axis=='z':points.append((x+r*math.cos(t),y+r*math.sin(t),z+a))
                else:points.append((x+a,y+r*math.cos(t),z+r*math.sin(t)))
        fs=[tuple(reversed(range(segments))),tuple(range(segments,segments*2))]
        fs.extend((i,(i+1)%segments,(i+1)%segments+segments,i+segments) for i in range(segments))
        poly(points,fs,mat)
    for cx in s['centres_obj_x']:
        half=s['car_length_m']/2
        # Rounded roof: closed elliptical extrusion with an actual curved silhouette.
        section=[]
        for i in range(s['roof_segments']+1):
            t=math.pi*i/s['roof_segments']
            section.append((s['roof_spring_y']+(s['roof_crown_y']-s['roof_spring_y'])*math.sin(t),s['half_width']*math.cos(t)))
        n=len(section)
        points=[(x,y,z) for x in [cx-half,cx+half] for y,z in section]
        fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]
        fs.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
        poly(points,fs,0)
        box(cx,s['floor_y']+s['floor_thickness']/2,0,s['car_length_m'],s['floor_thickness'],2*s['half_width']-.1,1)
        step=(s['car_length_m']-2*s['bay_margin'])/s['bay_count']
        for side in [-1,1]:
            z=side*(s['half_width']-s['side_thickness']/2)
            box(cx,(s['side_bottom_y']+s['sill_y'])/2,z,s['car_length_m'],s['sill_y']-s['side_bottom_y'],s['side_thickness'],0)
            box(cx,(s['window_top_y']+s['roof_spring_y'])/2,z,s['car_length_m'],s['roof_spring_y']-s['window_top_y'],s['side_thickness'],0)
            for y in s['rib_heights_y']:
                box(cx,y,side*(s['half_width']+s['rib_depth']/2),s['car_length_m'],s['rib_height'],s['rib_depth'],0)
            for i in range(s['bay_count']+1):
                x=cx-half+s['bay_margin']+i*step
                box(x,(s['sill_y']+s['window_top_y'])/2,z,s['post_width'],s['window_top_y']-s['sill_y'],s['side_thickness'],0)
            for i in range(s['bay_count']):
                x=cx-half+s['bay_margin']+(i+.5)*step
                width=step-s['post_width']
                if i in s['door_bays']:
                    front=side*(s['half_width']+.01);frame=s['door_frame_width']
                    bottom=s['side_bottom_y'];top=s['window_top_y'];sill=s['sill_y']
                    box(x,(bottom+sill)/2,front,width,sill-bottom,s['window_thickness'],1)
                    for edge in [-1,1]:box(x+edge*(width-frame)/2,(sill+top)/2,front,frame,top-sill,s['window_thickness'],1)
                    box(x,top-frame/2,front,width,frame,s['window_thickness'],1)
                    box(x,(sill+top)/2,front,frame*.65,top-sill,s['window_thickness'],1)
                    for edge in [-1,1]:
                        box(x+edge*width*.25,(sill+top-frame)/2,front-side*s['door_glass_inset'],width*.5-frame*1.4,top-sill-frame,s['window_thickness'],3)
                else:
                    box(x,(s['sill_y']+s['window_top_y'])/2,side*(s['half_width']-s['window_recess']),width,s['window_top_y']-s['sill_y']-.08,s['window_thickness'],3)
        for off in s['bogie_offsets_x']:
            box(cx+off,s['underframe_y'],0,*s['underframe_size'],2)
            for axle in s['axle_offsets_x']:
                for side in [-1,1]:
                    cylinder(cx+off+axle,s['wheel_y'],side*s['wheel_lateral'],s['wheel_radius'],s['wheel_width'],s['wheel_segments'],'z',2)
        for end in [-1,1]:
            x=cx+end*half
            box(x,(s['side_bottom_y']+s['roof_spring_y'])/2,0,s['cab_plate_thickness'],s['roof_spring_y']-s['side_bottom_y'],2*s['half_width'],0)
            for lateral in s['cab_window_centres_z']:
                box(x+end*s['cab_front_extra'],(s['cab_window_bottom']+s['cab_window_top'])/2,lateral,s['cab_plate_thickness'],s['cab_window_top']-s['cab_window_bottom'],s['cab_window_width'],3)
            for lateral in s['headlight_z']:
                cylinder(x+end*s['cab_front_extra'],s['headlight_y'],lateral,s['headlight_radius'],s['headlight_depth'],s['headlight_segments'],'x',4)
    mesh=bpy.data.meshes.new('LB_TRAIN_TWO_CAR_MESH')
    mesh.from_pydata(verts,[],faces)
    for mat in s['materials']:mesh.materials.append(bpy.data.materials['LB_'+mat])
    for face,mat in zip(mesh.polygons,mats):face.material_index=mat
    # Recalculate outward normals per closed primitive before the bevel/export.
    import bmesh
    bm=bmesh.new();bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(mesh);bm.free();mesh.update()
    obj=bpy.data.objects.new('LB_HERO_TRAIN_TWO_CAR_R'+str(s['revision']),mesh)
    obj['owner_map']='11_lowerbay';obj['source_tag']='hero_train'
    out.objects.link(obj)
    bevel=obj.modifiers.new('LB_TRAIN_EDGE_FINISH','BEVEL')
    bevel.width=s['bevel_m'];bevel.segments=s['bevel_segments'];bevel.limit_method='ANGLE';bevel.use_clamp_overlap=True
    bpy.context.view_layer.update()
    lo,hi=s['locked_envelope_obj']
    assert all(lo[0]-.0001<=x<=hi[0]+.0001 and lo[1]-.0001<=y<=hi[1]+.0001 and lo[2]-.0001<=z<=hi[2]+.0001 for x,y,z in ((v.co.x,v.co.z,-v.co.y) for v in mesh.vertices))
    save('geometry_train_revision',{'gate':'4-6 preview refinement','status':'Train geometry checkpoint; full structure clearance still pending',
        'car_count':2,'source_envelope_preserved':True,'mesh_vertices':len(mesh.vertices),
        'rounded_roof':True,'modeled_wheels':16,'original_train_archived':True},False)
    print('New two-car train saved; original seed train archived.')

main()
