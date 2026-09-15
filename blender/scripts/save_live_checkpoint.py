import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
from lowerbay_common import BUILD, checkpoint, note
assert Path(bpy.data.filepath).resolve() == (BUILD / 'blender/map_master.blend').resolve()
assert bpy.context.scene.name == 'LevelLowerBay'
checkpoint('interactive_before_final_save')
bpy.ops.wm.save_as_mainfile(filepath=str(BUILD / 'blender/map_master.blend'))
note('Interactive Lower Bay master saved with corrected columns and camera framing; local build copy refreshed separately.')
print('Lower Bay master safely saved.')
