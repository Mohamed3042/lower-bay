"""Make metre-scale static FBX LODs and preserve the owner's embedded 4K maps.

Run in a fresh Blender 5.1 background process. Set LOWER_BAY_INCOMING to override
the Downloads source folder. Originals are read only. Receipt hashes bind each
export; geometry and maps can be regenerated from the recorded source files.
"""
import hashlib
import json
import math
import os
from pathlib import Path
import struct
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(os.environ.get('LOWER_BAY_INCOMING', str(Path.home()/'Downloads')))
OUT = ROOT/'playable/unity/Assets/LowerBay2026/Props'
RECEIPT = ROOT/'reimagine-2026/evidence/hunyuan-preparation.json'
ASSETS = [
    ('station-bin', '44b8e503af2bdba29d2f99540ba91a05.glb', (.51,.51,.95), 0, (35000,10000)),
    ('drinks-vending', '7359061a51599b9f4b2a290feff846b9.glb', (.95,.8,1.95), 0, (70000,22000)),
    ('subway-car', '6dab80e603ca593a1e83afec52493a4a.glb', (17.8,2.9,3.135), 90, (140000,45000)),
    ('station-bench', 'ba47377b1d8544136f0ff4247b0f4b1a.glb', (2.2,.71,.78), 0, (80000,28000)),
]

def sha(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def extract_maps(path, output):
    raw = path.read_bytes()
    assert raw[:4] == b'glTF' and struct.unpack_from('<I',raw,8)[0] == len(raw)
    count = struct.unpack_from('<I',raw,12)[0]
    doc = json.loads(raw[20:20+count])
    buffer = memoryview(raw)[28+count:]
    mat = doc['materials'][0]
    slots = {'basecolor':mat['pbrMetallicRoughness']['baseColorTexture']['index'],
             'normal':mat['normalTexture']['index'],
             'metalrough':mat['pbrMetallicRoughness']['metallicRoughnessTexture']['index']}
    receipt=[]
    for role, texture in slots.items():
        image = doc['images'][doc['textures'][texture]['source']]
        view = doc['bufferViews'][image['bufferView']]
        start = view.get('byteOffset',0)
        data = buffer[start:start+view['byteLength']]
        assert image['mimeType'] == 'image/png' and bytes(data[:8]) == b'\x89PNG\r\n\x1a\n'
        size = struct.unpack_from('>II',data,16)
        assert size == (4096,4096), (role,size)
        target = output/(role+'.png')
        target.write_bytes(data)
        receipt.append(dict(role=role,file=target.relative_to(ROOT).as_posix(),size=list(size),sha256=sha(target)))
    return receipt

def bounds(mesh):
    return [Vector([min(v.co[a] for v in mesh.vertices) for a in range(3)]),
            Vector([max(v.co[a] for v in mesh.vertices) for a in range(3)])]

records=[]
for asset, file, dimensions, rotation, budgets in ASSETS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.context.scene.unit_settings.scale_length=1
    path = SOURCE/file
    output = OUT/asset
    output.mkdir(parents=True, exist_ok=True)
    source_hash=sha(path)
    maps=extract_maps(path, output)
    bpy.ops.import_scene.gltf(filepath=str(path))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes: obj.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    if len(meshes)>1: bpy.ops.object.join()
    source=bpy.context.object
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    source.rotation_euler.z=math.radians(rotation)
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=False)
    lo,hi=bounds(source.data)
    center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    scale=Vector([dimensions[a]/(hi[a]-lo[a]) for a in range(3)])
    for vertex in source.data.vertices:
        vertex.co-=center
    source.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    source.data.update()
    samples=[source.data.vertices[i].co.copy() for i in range(0,len(source.data.vertices),max(1,len(source.data.vertices)//3072))]
    record=dict(asset=asset,sourceFile=file,sourceSha256=source_hash,
                sourceTriangles=len(source.data.polygons),dimensionsBlender=list(dimensions),
                pivot='centre of footprint at floor; metres',maps=maps,lods=[])
    for level,budget in enumerate(budgets):
        obj=source.copy();obj.data=source.data.copy()
        bpy.context.collection.objects.link(obj)
        obj.name=asset+'_LOD'+str(level)
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True);bpy.context.view_layer.objects.active=obj
        decimate=obj.modifiers.new('Budgeted static LOD','DECIMATE')
        decimate.ratio=min(1,budget/len(source.data.polygons))
        decimate.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=decimate.name)
        obj.data.calc_loop_triangles()
        triangles=len(obj.data.loop_triangles)
        assert triangles<=budget*1.02 and obj.data.uv_layers.active is not None
        assert all(math.isfinite(c) for v in obj.data.vertices for c in v.co)
        bvh=BVHTree.FromPolygons([v.co for v in obj.data.vertices],
                                [p.vertices[:] for p in obj.data.polygons],all_triangles=True)
        errors=sorted(bvh.find_nearest(p)[3] for p in samples)
        max_error=errors[-1]
        # Bound measured loss in world metres; this complements visual review.
        limit=(.055 if asset=='subway-car' else .018)*(2 if level else 1)
        if max_error>limit:
            raise ValueError(f'{asset} LOD{level} maximum surface error {max_error:.4f}m exceeds {limit}m')
        # Unity binds the separately preserved PBR maps. Do not export Blender's
        # packed-image material references, which embed machine-local paths.
        obj.data.materials.clear()
        target=output/('LOD'+str(level)+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,
            object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
            bake_space_transform=False,use_mesh_modifiers=True,mesh_smooth_type='FACE',
            add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False)
        assert b'C:\\Users' not in target.read_bytes() and b'C:/Users' not in target.read_bytes()
        record['lods'].append(dict(level=level,triangles=triangles,
            sampledSurfaceErrorMaxMetres=max_error,
            sampledSurfaceErrorP95Metres=errors[int(len(errors)*.95)],sampleCount=len(errors),
            fbx=target.relative_to(ROOT).as_posix(),sha256=sha(target)))
        bpy.data.objects.remove(obj,do_unlink=True)
    assert sha(path)==source_hash, 'Original owner GLB changed during processing'
    records.append(record)
    RECEIPT.write_text(json.dumps(records,indent=2),encoding='utf-8')
    print('PREPARED',asset,record['lods'],flush=True)
