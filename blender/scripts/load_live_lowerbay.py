"""Preserve the current interactive file before loading the owned map master."""
import bpy
import datetime
from pathlib import Path
BUILD = Path(__file__).resolve().parents[2]
target = BUILD / 'blender/map_master.blend'
assert target.is_file()
stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S_%f')
backup = BUILD / 'blender/checkpoints' / ('interactive_before_load_' + stamp + '.blend')
bpy.ops.wm.save_as_mainfile(filepath=str(backup), copy=True)
with (BUILD / 'validation/BUILD_STATE.md').open('a', encoding='utf-8') as f:
    f.write('\n- Interactive scene preserved before load: `' + str(backup.relative_to(BUILD)) + '`.\n')
bpy.ops.wm.open_mainfile(filepath=str(target))
if bpy.context.window:
    bpy.context.window.scene = bpy.data.scenes['LevelLowerBay']
print('Loaded owned Lower Bay master; prior scene preserved.')
