"""Replace the misplaced/fluted seed columns with footage-led tiled piers.

Source meshes are archived, never deleted. Every geometric value comes from spec.
"""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
import math
from lowerbay_common import BUILD, load, checkpoint, save

assert Path(bpy.data.filepath).resolve() == (BUILD / 'blender/map_master.blend').resolve(), 'Wrong project open'
assert bpy.context.scene.name == 'LevelLowerBay', 'Wrong active scene'
spec = load('module_catalog')['column_revision']
output_name = 'LB_FOOTAGE_COLUMNS_R1'
if output_name in bpy.data.collections:
    print('Column revision already applied; no-op')
else:
    checkpoint('gate04_before_column_revision')
    archive = bpy.data.collections.new('LB_ARCHIVE_SEED_COLUMNS')
    bpy.context.scene.collection.children.link(archive)
    for name in spec['archive_source_names']:
        obj = bpy.data.objects[name]
        assert obj.get('map_owner') == '11_lowerbay'
        archive.objects.link(obj)
        for coll in list(obj.users_collection):
            if coll != archive:
                coll.objects.unlink(obj)
        obj.hide_render = True
        obj.hide_set(True)
    archive.hide_render = True
    archive.hide_viewport = True
    output = bpy.data.collections.new(output_name)
    bpy.context.scene.collection.children.link(output)
    vertices, faces, face_materials = [], [], []
    def box(cx, cz, width, bottom, top, material):
        n = len(vertices)
        h = width/2
        # Construction is Blender Z-up; the input centres use source OBJ X/Z.
        vertices.extend([(cx-h,-cz-h,bottom),(cx+h,-cz-h,bottom),(cx+h,-cz+h,bottom),(cx-h,-cz+h,bottom),
                         (cx-h,-cz-h,top),(cx+h,-cz-h,top),(cx+h,-cz+h,top),(cx-h,-cz+h,top)])
        faces.extend([tuple(n+i for i in f) for f in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]])
        face_materials.extend([material]*6)
    for x,z in spec['centres_obj_xz']:
        box(x,z,spec['foot_width_m'],spec['floor_y_m'],spec['foot_height_m'],2)
        box(x,z,spec['dado_width_m'],spec['foot_height_m'],spec['dado_top_y_m'],1)
        box(x,z,spec['shaft_width_m'],spec['dado_top_y_m'],spec['collar_bottom_y_m'],0)
        box(x,z,spec['collar_width_m'],spec['collar_bottom_y_m'],spec['capital_bottom_y_m'],0)
        box(x,z,spec['capital_width_m'],spec['capital_bottom_y_m'],spec['ceiling_y_m'],2)
    mesh = bpy.data.meshes.new('LB_SQUARE_TILE_PIERS_MESH')
    mesh.from_pydata(vertices, [], faces)
    for material in spec['material_slots']:
        mesh.materials.append(bpy.data.materials['LB_'+material])
    for face, material in zip(mesh.polygons, face_materials):
        face.material_index = material
    mesh.update()
    obj = bpy.data.objects.new('LB_SQUARE_TILE_PIERS_36', mesh)
    obj['owner_map'] = '11_lowerbay'
    obj['spec_revision'] = spec['revision']
    output.objects.link(obj)
    bevel = obj.modifiers.new('LB_INWARD_EDGE_FINISH', 'BEVEL')
    bevel.width = spec['bevel_width_m']
    bevel.segments = spec['bevel_segments']
    bevel.limit_method = 'ANGLE'
    bevel.use_clamp_overlap = True
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(dg)
    evaluated_mesh = evaluated.to_mesh()
    assert min(v.co.z for v in evaluated_mesh.vertices) >= spec['floor_y_m']-.0002
    assert abs(max(v.co.z for v in evaluated_mesh.vertices)-spec['ceiling_y_m']) < .0002
    for v in evaluated_mesh.vertices:
        assert any(abs(v.co.x-x) <= spec['plan_envelope_width_max_m']/2+.0002 and
                   abs(v.co.y+z) <= spec['plan_envelope_width_max_m']/2+.0002 for x,z in spec['centres_obj_xz'])
    evaluated_mesh.calc_loop_triangles()
    column_triangles = len(evaluated_mesh.loop_triangles)
    evaluated.to_mesh_clear()
    total = 0
    for item in bpy.context.scene.objects:
        if item.type == 'MESH' and not item.hide_render:
            ev = item.evaluated_get(dg)
            me = ev.to_mesh()
            me.calc_loop_triangles()
            total += len(me.loop_triangles)
            ev.to_mesh_clear()
    for area in bpy.context.screen.areas if bpy.context.screen else []:
        if area.type == 'VIEW_3D':
            area.spaces.active.region_3d.view_perspective = 'CAMERA'
            area.spaces.active.shading.type = 'SOLID'
            area.spaces.active.shading.color_type = 'MATERIAL'
            area.spaces.active.shading.show_cavity = True
    save('gate04_column_revision', {'gate': 4, 'status': 'PARTIAL: full-height square piers corrected; other structure issues pending',
         'column_count': len(spec['centres_obj_xz']), 'unchanged_concourse_piers': 22,
         'height_m': spec['ceiling_y_m']-spec['floor_y_m'], 'column_triangles': column_triangles,
         'visible_evaluated_triangles': total, 'plan_envelopes_pass': True,
         'original_meshes_archived_not_deleted': spec['archive_source_names'],
         'route_and_anchor_changes': 0}, render=False)
