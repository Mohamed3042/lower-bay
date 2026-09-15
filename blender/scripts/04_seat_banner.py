import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import bpy
from lowerbay_common import BUILD,load,checkpoint,save
def main():
 assert Path(bpy.data.filepath).resolve()==(BUILD/'blender/map_master.blend').resolve()
 s=load('module_catalog')['label_revision'];obj=bpy.data.objects['LB_SEED_sign_sign_b']
 if obj.get('blue_banner_seated'):print('Blue banner already seated; no-op.');return
 checkpoint('before_blue_banner_seating')
 lo,hi=s['blue_banner_source_range'];count=0
 for v in obj.data.vertices:
  q=(v.co.x,v.co.z,-v.co.y)
  if all(lo[i]-.0001<=q[i]<=hi[i]+.0001 for i in range(3)):
   v.co.z-=s['blue_banner_lowering_m'];count+=1
 assert count==8,count
 obj.data.update();obj['blue_banner_seated']=True
 for item in bpy.context.scene.objects:
  if item.name.startswith('LB_STATION_LABEL_03_'):item.location.z=(lo[1]+hi[1])/2-s['blue_banner_lowering_m']
 save('blue_banner_seating',{'gate':6,'status':'Blue destination banner lowered beneath the hall ceiling; other banner already inside high spawn room',
  'vertices_adjusted':count,'old_y_range':[lo[1],hi[1]],'new_y_range':[lo[1]-1.7,hi[1]-1.7],'layout_changed':False},False)
main()
