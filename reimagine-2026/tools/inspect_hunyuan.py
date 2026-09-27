"""Render owner GLBs without altering or copying the originals (Blender 5.1)."""
import hashlib
import json
import math
import pathlib
import bpy
from mathutils import Vector

ROOT = pathlib.Path(__file__).resolve().parents[1]
OUT = ROOT / 'evidence' / 'hunyuan-intake'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE = pathlib.Path.home() / 'Downloads'
FILES = ['44b8e503af2bdba29d2f99540ba91a05.glb',
         '7359061a51599b9f4b2a290feff846b9.glb',
         '6dab80e603ca593a1e83afec52493a4a.glb',
         'ba47377b1d8544136f0ff4247b0f4b1a.glb']

def aim(obj, target):
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat('-Z', 'Y').to_euler()

records = []
for index, name in enumerate(FILES, 1):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 16
    scene.cycles.use_denoising = True
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = 3
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 820
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.world = bpy.data.worlds.new('Neutral studio')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.35,.4,.48,1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .5
    path = SOURCE/name
    digest = hashlib.file_digest(path.open('rb'), 'sha256').hexdigest()
    bpy.ops.import_scene.gltf(filepath=str(path))
    meshes = [o for o in scene.objects if o.type == 'MESH']
    points = [o.matrix_world @ Vector(v) for o in meshes for v in o.bound_box]
    lo = Vector([min(v[a] for v in points) for a in range(3)])
    hi = Vector([max(v[a] for v in points) for a in range(3)])
    record = dict(file=name, sha256=digest, meshes=len(meshes),
                  sourceBounds={'min':list(lo),'max':list(hi)},
                  sourceTriangles=sum(len(o.data.polygons) for o in meshes),
                  identity='UNASSIGNED_PENDING_VISUAL_REVIEW', previews=[])
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    center = Vector(((lo.x+hi.x)/2, (lo.y+hi.y)/2, lo.z))
    factor = 4.0/max(hi-lo)
    for v in obj.data.vertices:
        v.co = (v.co-center)*factor
    if (hi.y-lo.y) > (hi.x-lo.x)*1.6:
        obj.rotation_euler.z = math.pi/2
    height = (hi.z-lo.z)*factor
    bpy.ops.mesh.primitive_plane_add(size=200)
    floor = bpy.context.object
    floor.name='Preview floor only'
    mat = bpy.data.materials.new('Studio neutral')
    mat.diffuse_color=(.14,.17,.2,1)
    floor.data.materials.append(mat)
    for location, power, size in [((1,-4,7),1250,5),((-4,-1,3),700,4),((2,4,6),1300,4)]:
        bpy.ops.object.light_add(type='AREA', location=location)
        light=bpy.context.object
        light.data.energy=power
        light.data.shape='DISK'
        light.data.size=size
        aim(light,(0,0,height*.45))
    bpy.ops.object.camera_add(location=(6,-8,5))
    camera=bpy.context.object
    camera.data.type='ORTHO'
    camera.data.ortho_scale=6.3
    scene.camera=camera
    target=(0,0,height*.47)
    for label, direction in [('front',(6,-8,5)),('rear',(-6,8,5))]:
        camera.location=direction
        aim(camera,target)
        image=OUT/(str(index)+'-'+label+'.png')
        scene.render.filepath=str(image)
        bpy.ops.render.render(write_still=True)
        record['previews'].append(image.relative_to(ROOT).as_posix())
    records.append(record)
    (OUT/'receipt.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
    print('INTAKE_RENDERED',name,flush=True)
