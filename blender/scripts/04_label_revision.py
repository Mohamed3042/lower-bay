import sys,math
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import bpy
from lowerbay_common import BUILD,load,checkpoint,save
def main():
 assert Path(bpy.data.filepath).resolve()==(BUILD/'blender/map_master.blend').resolve()
 assert bpy.context.scene.name=='LevelLowerBay'
 s=load('module_catalog')['label_revision'];name='LB_LABEL_R'+str(s['revision'])
 if name in bpy.data.collections:print('Label revision already present; no-op.');return
 checkpoint('before_station_labels')
 out=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(out)
 source=bpy.data.collections.new('LB_LABEL_CURVE_SOURCES');bpy.context.scene.collection.children.link(source)
 for i,item in enumerate(s['labels']):
  lo,hi=item['min_obj'],item['max_obj'];cx=(lo[0]+hi[0])/2;cy=(lo[1]+hi[1])/2
  for side in [-1,1]:
   label='LB_STATION_LABEL_%02d_%s'%(i,'F' if side>0 else 'B')
   curve=bpy.data.curves.new(label+'_CURVE','FONT');curve.body=item['text'];curve.align_x='CENTER';curve.align_y='CENTER'
   curve.size=s['font_size_m'];curve.extrude=s['extrude_m'];curve.resolution_u=3
   template=bpy.data.objects.new(label+'_AUTHORING',curve);out.objects.link(template)
   template.location=(cx,-(hi[2]+s['surface_gap_m'] if side>0 else lo[2]-s['surface_gap_m']),cy)
   template.rotation_euler=(math.pi/2,0,0 if side>0 else math.pi)
   bpy.context.view_layer.update()
   scale=min((hi[0]-lo[0])*s['width_fraction']/max(template.dimensions.x,.001),(hi[1]-lo[1])*s['height_fraction']/max(template.dimensions.z,.001))
   template.scale=(scale,scale,1);bpy.context.view_layer.update()
   mesh=bpy.data.meshes.new_from_object(template.evaluated_get(bpy.context.evaluated_depsgraph_get()))
   mesh.materials.append(bpy.data.materials['LB_'+s['material']])
   obj=bpy.data.objects.new(label,mesh);out.objects.link(obj);obj.matrix_world=template.matrix_world.copy()
   obj['owner_map']='11_lowerbay';obj['source_tag']='sign'
   source.objects.link(template);out.objects.unlink(template);template.hide_render=True;template.hide_set(True)
 source.hide_render=True;source.hide_viewport=True
 save('station_labels',{'gate':6,'status':'Dimensional station/destination lettering; non-colliding detail only',
  'label_meshes':len(out.objects),'panel_envelopes_preserved':True,'artwork_reconstruction_claim':False},False)
main()
