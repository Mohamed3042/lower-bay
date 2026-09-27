"""Build a new Blender scene from a complete StrikeMap JSON. Run in factory background Blender."""
import argparse
import base64
import hashlib
import json
import math
import os
import pathlib
import re
import sys
import time

import bpy
from mathutils import Vector


def arguments():
    parser = argparse.ArgumentParser()
    parser.add_argument('--map', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--width', type=int, default=1280)
    parser.add_argument('--height', type=int, default=720)
    parser.add_argument('--samples', type=int, default=24)
    parser.add_argument('--threads', type=int, default=4)
    parser.add_argument('--views', default='', help='Comma-separated landmark IDs and/or overview; defaults to overview plus first two landmarks.')
    parser.add_argument('--no-render', action='store_true')
    return parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:])


def linear_color(value):
    values = [int(value[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return tuple(v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in values)


def to_blender(position):
    x, y, z = position
    return Vector((x, -z, y))


def to_map(position):
    return [position.x, position.z, -position.y]


def train_offset(dynamic, seconds):
    minimum, maximum = dynamic['min'], dynamic['max']
    speed, pause = max(0, dynamic['speed']), max(0, dynamic['pause'])
    if maximum <= minimum or not speed:
        return minimum
    travel = (maximum - minimum) / speed
    t = (seconds + dynamic.get('phase', 0)) % (2 * (travel + pause))
    if t < pause:
        return minimum
    if t < pause + travel:
        return minimum + (t - pause) * speed
    if t < 2 * pause + travel:
        return maximum
    return maximum - (t - (2 * pause + travel)) * speed


def local_mesh(entity):
    w, h, d = [v / 2 for v in entity['size']]
    if entity['kind'] == 'ramp':
        points = [(-w, -h, -d), (w, -h, -d), (w, -h, d), (-w, -h, d), (w, h, d), (-w, h, d)]
        faces = [(1, 2, 3, 0), (5, 4, 1, 0), (2, 4, 5, 3), (3, 5, 0), (4, 2, 1)]
    else:
        points = [(-w, -h, -d), (w, -h, -d), (w, h, -d), (-w, h, -d), (-w, -h, d), (w, -h, d), (w, h, d), (-w, h, d)]
        faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 4, 7, 3), (1, 2, 6, 5), (0, 1, 5, 4), (3, 7, 6, 2)]
    return points, faces


def world_vertices(entity, extra_offset=None):
    points, _ = local_mesh(entity)
    angle = math.radians(entity.get('rotation', [0, 0, 0])[1])
    c, s = math.cos(angle), math.sin(angle)
    px, py, pz = entity['position']
    offset = extra_offset or [0, 0, 0]
    return [[px + x * c + z * s + offset[0], py + y + offset[1], pz - x * s + z * c + offset[2]] for x, y, z in points]


def bounds(points):
    return {'min': [min(p[a] for p in points) for a in range(3)], 'max': [max(p[a] for p in points) for a in range(3)]}


def set_linear_keys(obj):
    action = obj.animation_data.action
    curves = getattr(action, 'fcurves', None)
    if curves is None:
        curves = []
        for layer in action.layers:
            for strip in layer.strips:
                bag = strip.channelbag(obj.animation_data.action_slot)
                if bag:
                    curves.extend(bag.fcurves)
    for curve in curves:
        for key in curve.keyframe_points:
            key.interpolation = 'LINEAR'


def main():
    args = arguments()
    if not bpy.app.background:
        raise RuntimeError('This builder requires a new background process; it will not replace a user scene.')
    started = time.monotonic()
    source = pathlib.Path(args.map).resolve()
    output = pathlib.Path(args.output).resolve()
    if output.exists() and any(output.iterdir()):
        raise FileExistsError('Output directory is not empty; choose a new directory to preserve all existing files.')
    output.mkdir(parents=True, exist_ok=True)
    source_bytes = source.read_bytes()
    scene_map = json.loads(source_bytes.decode('utf-8-sig'))
    if scene_map.get('version') not in ('1.0', '2.0') or not isinstance(scene_map.get('entities'), list):
        raise ValueError('Expected a complete StrikeMap v1 or v2 map JSON.')
    # An output folder is intentionally exclusive: never overwrite a previous authored scene.
    if (output / 'arena.blend').exists():
        raise FileExistsError('arena.blend already exists; choose a new output directory to preserve it.')
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.name = scene_map['name']
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = args.samples
    scene.cycles.use_denoising = True
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = max(1, args.threads)
    scene.render.resolution_x, scene.render.resolution_y = args.width, args.height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.fps = 24
    scene.view_settings.exposure = .45
    scene.world.use_nodes = True
    scene.world.node_tree.nodes.get('Background').inputs['Color'].default_value = (.45, .52, .62, 1)
    scene.world.node_tree.nodes.get('Background').inputs['Strength'].default_value = .32
    scene['strikemap_version'] = scene_map['version']
    scene['strikemap_source_id'] = scene_map['id']
    scene['coordinate_conversion'] = 'map [x,y,z] -> Blender [x,-z,y]; bundled glTF converts back to numeric [x,y,z]'
    scene['provenance_json'] = json.dumps(scene_map.get('analysis', {}).get('provenance', {}))

    visuals = bpy.data.collections.new('ARENA_VISUALS')
    scene.collection.children.link(visuals)
    metadata = bpy.data.collections.new('GAMEPLAY_METADATA')
    scene.collection.children.link(metadata)
    materials, images, objects, roofs, lights = {}, {}, {}, [], []
    entity_by_id = {entity['id']: entity for entity in scene_map['entities']}

    def image_for(key):
        if key in images:
            return images[key]
        if not re.fullmatch(r'textures/[A-Za-z0-9_./-]+\.(png|jpg|jpeg|webp)', key, re.IGNORECASE) or '..' in pathlib.PurePosixPath(key).parts:
            raise ValueError('Unsafe texture asset key: ' + key)
        data = scene_map.get('assets', {}).get(key)
        if not isinstance(data, str) or not re.match(r'^data:image/(png|jpeg|webp);base64,', data):
            raise ValueError('Texture must be embedded in map.assets: ' + key)
        target = output / key
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(base64.b64decode(data.split(',', 1)[1], validate=True))
        image = bpy.data.images.load(str(target), check_existing=True)
        image.pack()
        images[key] = image
        return image

    def material_for(entity):
        spec = entity.get('material', {})
        key = json.dumps([entity['color'], spec], sort_keys=True)
        if key in materials:
            return materials[key]
        material = bpy.data.materials.new('Material_' + str(len(materials) + 1))
        material.use_nodes = True
        material.diffuse_color = (*linear_color(entity['color']), spec.get('opacity', 1))
        shader = material.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Base Color'].default_value = material.diffuse_color
        shader.inputs['Roughness'].default_value = spec.get('roughness', .8)
        shader.inputs['Metallic'].default_value = spec.get('metalness', .025)
        shader.inputs['Alpha'].default_value = spec.get('opacity', 1)
        shader.inputs['Emission Color'].default_value = (*linear_color(spec.get('emissive', '#000000')), 1)
        shader.inputs['Emission Strength'].default_value = spec.get('emissiveIntensity', 0)
        if spec.get('opacity', 1) < 1:
            material.surface_render_method = 'DITHERED'
        if spec.get('texture'):
            node = material.node_tree.nodes.new('ShaderNodeTexImage')
            node.image = image_for(spec['texture'])
            tint = material.node_tree.nodes.new('ShaderNodeMixRGB')
            tint.blend_type = 'MULTIPLY'
            tint.inputs[0].default_value = 1
            tint.inputs[2].default_value = (*linear_color(entity['color']), 1)
            material.node_tree.links.new(node.outputs['Color'], tint.inputs[1])
            material.node_tree.links.new(tint.outputs[0], shader.inputs['Base Color'])
        materials[key] = material
        return material

    for entity in scene_map['entities']:
        points, faces = local_mesh(entity)
        mesh = bpy.data.meshes.new(entity['id'] + '_mesh')
        mesh.from_pydata([to_blender(point) for point in points], [], faces)
        mesh.update()
        uv = mesh.uv_layers.new(name='UVMap')
        scale_u, scale_v = entity.get('material', {}).get('uvScale', [1, 1])
        for polygon in mesh.polygons:
            # Match Three BoxGeometry face orientation, including the horizontal
            # top faces. A corner-index UV rotated floor repeats by ninety degrees.
            map_normal = Vector(to_map(polygon.normal))
            major = max(range(3), key=lambda axis: abs(map_normal[axis]))
            for loop in polygon.loop_indices:
                vertex = points[mesh.loops[loop].vertex_index]
                nx, ny, nz = [vertex[axis] / entity['size'][axis] + .5 for axis in range(3)]
                if major == 0:
                    value = (1 - nz if map_normal.x > 0 else nz, ny)
                elif major == 1:
                    value = (nx, 1 - nz if map_normal.y > 0 else nz)
                else:
                    value = (nx if map_normal.z > 0 else 1 - nx, ny)
                uv.data[loop].uv = (value[0] * scale_u, value[1] * scale_v)
        obj = bpy.data.objects.new(entity['id'], mesh)
        visuals.objects.link(obj)
        obj.location = to_blender(entity['position'])
        obj.rotation_euler[2] = math.radians(entity.get('rotation', [0, 0, 0])[1])
        mesh.materials.append(material_for(entity))
        obj['entity_id'] = entity['id']
        obj['kind'] = entity['kind']
        obj['collidable'] = entity.get('collidable', True)
        obj['walkable'] = entity.get('walkable', entity['kind'] in ('floor', 'platform', 'ramp'))
        obj['roof'] = entity.get('roof', False)
        obj['zone'] = entity.get('zone', '')
        obj['label'] = entity.get('label', '')
        if entity['kind'] in ('wall', 'cover', 'platform') and min(entity['size']) >= .15:
            bevel = obj.modifiers.new('Architectural_edge_finish', 'BEVEL')
            bevel.width = min(.02, min(entity['size']) * .045)
            bevel.segments = 2
            bevel.use_clamp_overlap = True
        if entity.get('roof'):
            roofs.append(obj)
        if entity.get('material', {}).get('emissiveIntensity', 0) >= .8 and entity['position'][1] > 2:
            lights.append(entity)
        objects[entity['id']] = obj

    dynamic_offsets, carriers = {}, []
    for dynamic in scene_map.get('dynamics', []):
        carrier = bpy.data.objects.new(dynamic['id'], None)
        visuals.objects.link(carrier)
        carrier['dynamic_json'] = json.dumps(dynamic)
        carriers.append((dynamic, carrier))
        for identifier in dynamic['entityIds']:
            if identifier in objects:
                objects[identifier].parent = carrier
        axis = 0 if dynamic['axis'] == 'x' else 1
        sign = 1 if dynamic['axis'] == 'x' else -1
        speed, span, pause = dynamic['speed'], dynamic['max'] - dynamic['min'], dynamic['pause']
        travel = span / speed if speed > 0 else 0
        period = 2 * (travel + pause)
        duration = min(max(period, 1), 180)
        times = {0, duration}
        if period > 0:
            for cycle in range(-2, math.ceil(duration / period) + 3):
                for event in (0, pause, pause + travel, 2 * pause + travel, period):
                    value = cycle * period + event - dynamic.get('phase', 0) % period
                    if 0 < value < duration:
                        times.add(value)
        for seconds in sorted(times):
            carrier.location[axis] = sign * train_offset(dynamic, seconds)
            carrier.keyframe_insert(data_path='location', frame=1 + seconds * scene.render.fps)
        set_linear_keys(carrier)
        scene.frame_end = max(scene.frame_end, math.ceil(duration * scene.render.fps) + 1)
        offset = [0, 0, 0]
        offset[0 if dynamic['axis'] == 'x' else 2] = train_offset(dynamic, 0)
        for identifier in dynamic['entityIds']:
            dynamic_offsets[identifier] = offset
    scene.frame_set(1)

    for role in ('spawns', 'pickups'):
        for actor in scene_map.get(role, []):
            obj = bpy.data.objects.new(actor['id'], None)
            metadata.objects.link(obj)
            obj.location = to_blender(actor['position'])
            obj['actor_json'] = json.dumps(actor)
            obj['actor_role'] = role
            for dynamic, carrier in carriers:
                if actor['id'] in dynamic.get('actorIds', []):
                    obj.parent = carrier
    # Real fixtures illuminate enclosed interiors; no unrelated environment assets are required.
    # Adding a 33rd fixture must not abruptly halve the illuminated fixtures.
    # First reserve one light per authored zone, then spread the remaining budget.
    chosen_lights = []
    lit_zones = set()
    for fixture in lights:
        zone = fixture.get('zone', '')
        if zone not in lit_zones and len(chosen_lights) < 32:
            chosen_lights.append(fixture)
            lit_zones.add(zone)
    while len(chosen_lights) < min(32, len(lights)):
        remaining = [fixture for fixture in lights if fixture not in chosen_lights]
        chosen_lights.append(max(remaining, key=lambda fixture: min(sum((a - b) ** 2 for a, b in zip(fixture['position'], chosen['position'])) for chosen in chosen_lights)))
    for index, entity in enumerate(chosen_lights):
        data = bpy.data.lights.new('Fixture_light_' + str(index), 'AREA')
        data.energy = min(650, 150 * entity['material'].get('emissiveIntensity', 1))
        data.color = linear_color(entity['material'].get('emissive', '#fff0dd'))
        data.shape = 'RECTANGLE'
        data.size = max(.3, entity['size'][0])
        data.size_y = max(.3, entity['size'][2])
        obj = bpy.data.objects.new(data.name, data)
        scene.collection.objects.link(obj)
        point = list(entity['position'])
        point[1] -= entity['size'][1] / 2 + .05
        obj.location = to_blender(point)
    all_points = [point for entity in scene_map['entities'] for point in world_vertices(entity, dynamic_offsets.get(entity['id']))]
    measured_bounds = bounds(all_points)
    center = Vector([(a + b) / 2 for a, b in zip(measured_bounds['min'], measured_bounds['max'])])
    extent = Vector([b - a for a, b in zip(measured_bounds['min'], measured_bounds['max'])])
    key_data = bpy.data.lights.new('Overview_key', 'AREA')
    key_data.energy = max(1800, extent.x * extent.z * 4)
    key_data.size = max(extent.x, extent.z) * .7
    key = bpy.data.objects.new(key_data.name, key_data)
    scene.collection.objects.link(key)
    key.location = to_blender([center.x + extent.x * .2, measured_bounds['max'][1] + max(extent.x, extent.z) * .5, center.z - extent.z * .2])
    key.rotation_euler = (to_blender(center) - key.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.view_layer.update()
    errors = []
    for entity in scene_map['entities']:
        obj = objects[entity['id']]
        actual = [to_map(obj.matrix_world @ vertex.co) for vertex in obj.data.vertices]
        expected = world_vertices(entity, dynamic_offsets.get(entity['id']))
        error = max(abs(a - b) for point_a, point_b in zip(actual, expected) for a, b in zip(point_a, point_b))
        if error > 0.0001:
            errors.append({'id': entity['id'], 'maximum_error_m': error})
    if errors:
        raise AssertionError('Coordinate conversion failed: ' + json.dumps(errors[:8]))

    camera_data = bpy.data.cameras.new('ReviewCamera')
    camera = bpy.data.objects.new(camera_data.name, camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera_data.clip_start, camera_data.clip_end = .05, 2000
    landmarks = {landmark['id']: landmark for landmark in scene_map.get('landmarks', [])}
    requested = [value for value in args.views.split(',') if value] or ['overview', *list(landmarks)[:2]]
    renders = []

    def setup_view(name):
        overview = name == 'overview'
        for roof in roofs:
            roof.hide_render = overview
        key.hide_render = not overview and bool(chosen_lights)
        if overview:
            camera_data.type = 'ORTHO'
            longest = max(extent.x, extent.z)
            # Keep the long arena axis across the image, rather than foreshortening
            # it into a narrow vertical strip in a landscape review frame.
            offset = Vector((longest * .16, longest * .72, longest * .9)) if extent.x >= extent.z else Vector((longest * .9, longest * .72, longest * .16))
            location = center + offset
            camera.location = to_blender(location)
            camera.rotation_euler = (to_blender(center) - camera.location).to_track_quat('-Z', 'Y').to_euler()
            rotation = camera.rotation_euler.to_matrix().transposed()
            projections = [rotation @ (to_blender(point) - to_blender(center)) for point in all_points]
            projected_w = max(p.x for p in projections) - min(p.x for p in projections)
            projected_h = max(p.y for p in projections) - min(p.y for p in projections)
            camera_data.ortho_scale = max(projected_w, projected_h * args.width / args.height) * 1.16
        else:
            if name not in landmarks:
                raise ValueError('Unknown landmark: ' + name)
            landmark = landmarks[name]
            camera_data.type = 'PERSP'
            camera_data.sensor_fit = 'VERTICAL'
            camera_data.sensor_height = 24
            camera_data.lens = 12 / math.tan(math.radians(60) / 2)
            camera.location = to_blender(landmark['position'])
            camera.rotation_euler = (to_blender(landmark['lookAt']) - camera.location).to_track_quat('-Z', 'Y').to_euler()
        bpy.context.view_layer.update()

    for name in requested:
        setup_view(name)
        if not args.no_render:
            safe = re.sub(r'[^a-zA-Z0-9_-]', '_', name)
            path = output / (safe + '.png')
            scene.render.filepath = str(path)
            bpy.ops.render.render(write_still=True)
            renders.append({'view': name, 'path': path.name, 'width': args.width, 'height': args.height, 'bytes': path.stat().st_size, 'roof_cutaway': name == 'overview'})
    # Save/export a full scene, independent of the roof-hidden presentation render.
    if landmarks:
        setup_view(next(iter(landmarks)))
    for roof in roofs:
        roof.hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=str(output / 'arena.blend'))
    bpy.ops.export_scene.gltf(filepath=str(output / 'arena.glb'), export_format='GLB', export_extras=True, export_animations=True)
    report = {'status': 'BUILT', 'map_id': scene_map['id'], 'source_map': os.path.relpath(source, output).replace('\\', '/'), 'source_sha256': hashlib.sha256(source_bytes).hexdigest(), 'blender': bpy.app.version_string,
              'entities': len(objects), 'materials': len(materials), 'embedded_textures': list(images), 'fixtures': len(chosen_lights),
              'bounds_map_at_time_zero': measured_bounds, 'coordinate_error_count': len(errors),
              'coordinates': 'map [x,y,z] -> Blender [x,-z,y]; GLB is numeric map [x,y,z]; native Unity JSON importer owns engine handedness',
              'dynamics': [{'id': item['id'], 'offset_at_zero': train_offset(item, 0)} for item in scene_map.get('dynamics', [])],
              'dynamic_actors': [{'id': obj.name, 'parent': obj.parent.name, 'position_map_at_time_zero': to_map(obj.matrix_world.translation)} for obj in metadata.objects if obj.parent],
              'renders': renders, 'outputs': {name: (output / name).stat().st_size for name in ('arena.blend', 'arena.glb')},
              'elapsed_seconds': round(time.monotonic() - started, 2), 'scope': 'Blender construction, render and export; game runtime acceptance remains separate.'}
    (output / 'build-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print('STRIKEMAP_BLENDER_BUILD=' + json.dumps(report), flush=True)


if __name__ == '__main__':
    main()
