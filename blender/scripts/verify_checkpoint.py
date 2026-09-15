"""Read saved scene, verify idempotent no-op behavior and report route probes.

Ray samples are diagnostics, NOT a swept-capsule collision certification.
"""
import json
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
from mathutils import Vector
from lowerbay_common import BUILD, load, bootstrap, blockout, structure


def fingerprint():
    return sorted((o.name, o.type, len(o.data.vertices) if o.type == 'MESH' else 0,
                   tuple((m.name, m.type) for m in o.modifiers)) for o in bpy.context.scene.objects)


before = fingerprint()
bootstrap()
blockout()
structure()
assert fingerprint() == before, 'Non-idempotent gate replay'
scene = bpy.context.scene
depsgraph = bpy.context.evaluated_depsgraph_get()
minimum = load('validation_rules')['derived_map_metrics']['min_ceiling_clearance_standing_u']['value']
reports = []
for route in load('routes')['routes']:
    samples = []
    for p in route['points']:
        q = Vector((-p['x'], -p['z'], p['y']))
        hit, loc, normal, face, obj, matrix = scene.ray_cast(depsgraph, q + Vector((0, 0, .35)), Vector((0, 0, -1)), distance=1.0)
        sample = {'unity_route_marker': p, 'floor_detected_within_1m': bool(hit)}
        if hit:
            ceiling, overhead, _, _, top_obj, _ = scene.ray_cast(depsgraph, loc + Vector((0, 0, .04)), Vector((0, 0, 1)), distance=minimum-.04)
            sample.update({'floor_object': obj.name, 'floor_height_blender': round(loc.z, 4),
                           'vertical_ray_headroom_obstruction': top_obj.name if ceiling else None,
                           'headroom_m_if_obstructed': round(overhead.z-loc.z, 4) if ceiling else None})
        samples.append(sample)
    reports.append({'route': route['name'], 'markers': samples})
report = {'saved_scene': bpy.data.filepath, 'idempotent_noop_replay': True,
          'active_scene': scene.name, 'mesh_groups': sum(o.type == 'MESH' for o in scene.objects),
          'source_triangles': sum(len(o.data.polygons) for o in scene.objects if o.type == 'MESH'),
          'route_probe_limit': 'Markers only; render meshes; not a capsule sweep or runtime certification',
          'route_probes': reports}
path = BUILD / 'validation/measurements/checkpoint_verification.json'
path.write_text(json.dumps(report, indent=2) + '\n')
issues = sum(not p['floor_detected_within_1m'] or bool(p.get('vertical_ray_headroom_obstruction')) for r in reports for p in r['markers'])
print(json.dumps({'idempotent_noop_replay': True, 'mesh_groups': report['mesh_groups'], 'route_markers_to_review': issues,
                  'report': str(path)}))
