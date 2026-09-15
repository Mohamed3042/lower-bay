"""Read-only inspection before selecting or changing a Blender scene."""
import bpy
import json
print(json.dumps({'file': bpy.data.filepath, 'active_scene': bpy.context.scene.name,
                  'dirty': bpy.data.is_dirty,
                  'scenes': [{'name': s.name, 'objects': len(s.objects)} for s in bpy.data.scenes],
                  'version': bpy.app.version_string}))
