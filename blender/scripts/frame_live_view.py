import bpy
assert bpy.context.scene.name == 'LevelLowerBay'
camera = bpy.context.scene.camera
assert camera.name == 'LB_PREVIEW_CAMERA'
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            space = area.spaces.active
            space.use_local_camera = False
            space.camera = camera
            space.clip_start = .05
            space.clip_end = 250
            space.region_3d.view_camera_zoom = 0
            space.region_3d.view_camera_offset = (0, 0)
            space.region_3d.view_rotation = camera.rotation_euler.to_quaternion()
            space.region_3d.view_perspective = 'CAMERA'
            area.tag_redraw()
print('All Lower Bay viewports aligned to saved validation camera.')
