"""Isolated, checkpointed Lower Bay seed pipeline. Run only in a background Blender."""
import collections
import datetime
import json
import math
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Vector

BUILD = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(BUILD / 'tools'))
from prepare_spec import read_obj


def load(name):
    return json.loads((BUILD / 'spec' / (name + '.json')).read_text())


def note(message):
    path = BUILD / 'validation/BUILD_STATE.md'
    with path.open('a', encoding='utf-8') as stream:
        stream.write('\n- ' + datetime.datetime.now(datetime.timezone.utc).isoformat() + ': ' + message + '\n')


def checkpoint(stage):
    folder = BUILD / 'blender/checkpoints'
    folder.mkdir(parents=True, exist_ok=True)
    target = folder / (stage + '_' + datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S_%f') + '.blend')
    bpy.ops.wm.save_as_mainfile(filepath=str(target), copy=True)
    note('Pre-change checkpoint: `' + str(target.relative_to(BUILD)) + '`.')


def require_isolated():
    if not bpy.app.background:
        raise RuntimeError('Use a dedicated background Blender; never replace an interactive user scene.')
    if bpy.data.filepath and not Path(bpy.data.filepath).resolve().is_relative_to((BUILD / 'blender').resolve()):
        raise RuntimeError('Refusing an unrelated input blend file')


def collection(name):
    scene = bpy.context.scene
    if name in bpy.data.collections:
        raise RuntimeError('Output already exists: reopen earlier checkpoint to re-run, not duplicate')
    coll = bpy.data.collections.new(name)
    scene.collection.children.link(coll)
    return coll


def rgb(hex_color):
    vals = [int(hex_color[i:i+2], 16)/255 for i in (1, 3, 5)]
    return tuple(v/12.92 if v <= 0.04045 else ((v+.055)/1.055)**2.4 for v in vals)


def save(stage, report, render=False):
    path = BUILD / 'validation/measurements' / (stage + '.json')
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(report, indent=2) + '\n')
    master = BUILD / 'blender/map_master.blend'
    if master.exists():
        backup = BUILD / 'blender/checkpoints' / (stage + '_prior_master_' + datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S_%f') + '.blend')
        shutil.copy2(master, backup)
        note('Prior master preserved as `' + str(backup.relative_to(BUILD)) + '`.')
    bpy.ops.wm.save_as_mainfile(filepath=str(master))
    bpy.ops.wm.save_as_mainfile(filepath=str(BUILD / 'blender/checkpoints' / (stage + '_saved.blend')), copy=True)
    note(stage + ': ' + report['status'] + '; master saved; measurements `' + path.name + '`.')
    if render:
        render_path = BUILD / 'validation/screenshots' / (stage + '.png')
        render_path.parent.mkdir(parents=True, exist_ok=True)
        bpy.context.scene.render.filepath = str(render_path)
        bpy.ops.render.render(write_still=True)
    print('LOWERBAY_RESULT=' + json.dumps(report))


def setup_camera(scene):
    pov = load('lighting')['povs'][0]
    def unity_to_blender(p):
        return Vector((-p['x'], -p['z'], p['y']))
    data = bpy.data.cameras.new('LB_PREVIEW_CAMERA')
    camera = bpy.data.objects.new('LB_PREVIEW_CAMERA', data)
    scene.collection.objects.link(camera)
    camera.location = unity_to_blender(pov['eye'])
    camera.rotation_euler = (unity_to_blender(pov['target'])-camera.location).to_track_quat('-Z', 'Y').to_euler()
    data.type = 'PERSP'
    data.angle = math.radians(pov['fov'])
    data.clip_start = .05
    data.clip_end = 250
    scene.camera = camera
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = 'BOTH'
    scene.display.shading.background_type = 'WORLD'
    scene.world = bpy.data.worlds.new('LB_PREVIEW_WORLD')
    scene.world.color = (.13, .15, .18)
    preview = load('map_spec')['preview']
    scene.render.resolution_x, scene.render.resolution_y = preview['resolution']
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = preview['cpu_threads']


def bootstrap():
    require_isolated()
    if bpy.context.scene.name == 'LevelLowerBay':
        assert bpy.context.scene.unit_settings.scale_length == 1
        print('Lower Bay bootstrap already present; no-op')
        return
    checkpoint('gate02_before_bootstrap')
    # New scene leaves startup scene data intact. It is never a donor/user scene.
    scene = bpy.data.scenes.new('LevelLowerBay')
    bpy.context.window.scene = scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = load('map_spec')['units']['unity_unit_in_meters']
    for name in ('MAP_BLOCKOUT', 'MAP_STRUCTURE', 'MAP_MODULES', 'MAP_DETAIL', 'MAP_COLLISION', 'MAP_GAMEPLAY', 'MAP_LIGHTS'):
        collection(name)
    setup_camera(scene)
    save('gate02_bootstrap', {'gate': 2, 'status': 'PASS isolated tooling/bootstrap; later scripts developed gate-forward',
                             'scale_length': scene.unit_settings.scale_length,
                             'mesh_objects_in_active_scene': sum(o.type == 'MESH' for o in scene.objects)})


def blockout():
    require_isolated()
    scene = bpy.context.scene
    assert scene.name == 'LevelLowerBay'
    existing = [o for o in scene.objects if o.type == 'MESH' and o.get('map_owner') == '11_lowerbay']
    if existing:
        # Existing completed output is validated and left untouched (idempotent no-op).
        assert sum(len(o.data.polygons) for o in existing) == load('validation_rules')['seed_triangles']
        print('Blockout already present; no duplicates created')
        return
    checkpoint('gate03_before_import')
    materials = {}
    for spec in load('materials')['families']:
        material = bpy.data.materials.new('LB_' + spec['name'])
        material.diffuse_color = (*rgb(spec['color']), 1)
        material.use_nodes = True
        bsdf = material.node_tree.nodes.get('Principled BSDF')
        bsdf.inputs['Base Color'].default_value = material.diffuse_color
        bsdf.inputs['Roughness'].default_value = 1 - spec['smoothness']
        bsdf.inputs['Metallic'].default_value = spec['metallic']
        material['source_material'] = spec['name']
        material['preview_only'] = True
        materials[spec['name']] = material
    verts, faces = read_obj(BUILD / 'reference/seed/11_LowerBay.obj')
    groups = collections.defaultdict(list)
    for tag, mat, ids in faces:
        groups[(tag, mat)].append(ids)
    for (tag, mat), tris in sorted(groups.items()):
        indices = sorted({i for tri in tris for i in tri})
        remap = {i: j for j, i in enumerate(indices)}
        mesh = bpy.data.meshes.new('LB_SEED_' + tag + '_' + mat)
        mesh.from_pydata([(verts[i][0], -verts[i][2], verts[i][1]) for i in indices], [],
                         [tuple(remap[i] for i in tri) for tri in tris])
        mesh.materials.append(materials[mat])
        mesh.update()
        obj = bpy.data.objects.new(mesh.name, mesh)
        bpy.data.collections['MAP_BLOCKOUT'].objects.link(obj)
        obj['source_tag'] = tag
        obj['source_material'] = mat
        obj['map_owner'] = '11_lowerbay'
    objects = list(bpy.data.collections['MAP_BLOCKOUT'].objects)
    count = sum(len(o.data.polygons) for o in objects)
    assert count == load('validation_rules')['seed_triangles']
    measured = [[min(v.co[a] for o in objects for v in o.data.vertices) for a in range(3)],
                [max(v.co[a] for o in objects for v in o.data.vertices) for a in range(3)]]
    source_min = [min(v[a] for v in verts) for a in range(3)]
    source_max = [max(v[a] for v in verts) for a in range(3)]
    expected = [[source_min[0], -source_max[2], source_min[1]], [source_max[0], -source_min[2], source_max[1]]]
    assert all(abs(a-b) < load('validation_rules')['tolerance_m'] for m, e in zip(measured, expected) for a,b in zip(m,e))
    save('gate03_blockout', {'gate': 3, 'status': 'PASS exact rev4 import; gameplay clearance not yet certified',
                            'mesh_objects': len(objects), 'triangles': count, 'bounds_blender': measured,
                            'render': 'Workbench shape inspection, NOT final materials or baked lighting'}, True)


def structure():
    require_isolated()
    settings = load('module_catalog')['structure_refinement']
    if not any(o.get('source_tag') in settings['tags'] for o in bpy.data.collections['MAP_BLOCKOUT'].objects):
        print('Structure edge finish already present; no-op')
        return
    checkpoint('gate04_before_structure')
    changed = []
    for obj in list(bpy.data.collections['MAP_BLOCKOUT'].objects):
        if obj.get('source_tag') not in settings['tags']:
            continue
        # Modifiers preserve the source mesh. Re-run detects existing collection instead of duplicating.
        bevel = obj.modifiers.new('LB_INWARD_EDGE_FINISH', 'BEVEL')
        bevel.width = settings['bevel_width_m']
        bevel.segments = settings['bevel_segments']
        bevel.limit_method = 'ANGLE'
        bevel.angle_limit = math.radians(settings['angle_degrees'])
        bevel.use_clamp_overlap = True
        bpy.data.collections['MAP_STRUCTURE'].objects.link(obj)
        bpy.data.collections['MAP_BLOCKOUT'].objects.unlink(obj)
        changed.append(obj.name)
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    tris = 0
    envelope_failures = []
    for obj in bpy.context.scene.objects:
        if obj.type != 'MESH':
            continue
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        tris += len(mesh.loop_triangles)
        if obj.name in changed:
            for axis in range(3):
                lo = min(v.co[axis] for v in obj.data.vertices) - .0002
                hi = max(v.co[axis] for v in obj.data.vertices) + .0002
                if any(v.co[axis] < lo or v.co[axis] > hi for v in mesh.vertices):
                    envelope_failures.append(obj.name)
        evaluated.to_mesh_clear()
    assert not envelope_failures, envelope_failures
    save('gate04_structure_edges', {'gate': 4, 'status': 'PARTIAL structure edge refinement; full passage checks pending',
                                  'objects_refined': changed, 'evaluated_triangles': tris,
                                  'envelope_failures': envelope_failures,
                                  'route_or_spawn_changes': 0}, True)
