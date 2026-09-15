#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class LowerBayRoomLighting {
 const string Root="Assets/LowerBayAstra";
 [Serializable] class Spec {public string revision,colour;public float wallInteriorZ,fixtureY,intensity,range;public float[] positionsX;}
 static void Part(Transform parent,string name,PrimitiveType shape,Vector3 position,Vector3 scale,Material material,Transform anchor) {
  var go=GameObject.CreatePrimitive(shape);go.name="LB_ROOM_LIGHTING_"+name;
  // Only the automatically-created temporary primitive collider is removed.
  UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
  go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=scale;go.layer=8;
  var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.receiveGI=ReceiveGI.LightProbes;
  renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.probeAnchor=anchor;
  GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic);
 }
 public static void Apply() {
  var spec=JsonUtility.FromJson<Spec>(File.ReadAllText(Root+"/room_lighting_revision.json"));
  var sceneRoot=GameObject.Find("LevelLowerBay_PREVIEW_NOT_RUNTIME_READY");
  if(sceneRoot==null)throw new Exception("Not the assigned Lower Bay scene");
  if(GameObject.Find("LB_ROOM_LIGHTING_"+spec.revision)!=null)return;
  var root=new GameObject("LB_ROOM_LIGHTING_"+spec.revision);root.transform.SetParent(sceneRoot.transform,false);
  var steel=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Generated/LB_steel.mat");
  var glow=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Generated/LB_lamp.mat");
  if(steel==null||glow==null)throw new Exception("Missing fixture materials");
  Color colour;if(!ColorUtility.TryParseHtmlString(spec.colour,out colour))throw new Exception("Invalid fixture colour");
  for(int i=0;i<spec.positionsX.Length;i++) {
   float x=spec.positionsX[i],y=spec.fixtureY,z=spec.wallInteriorZ;
   var anchor=new GameObject("WallCageProbe_"+i);anchor.transform.SetParent(root.transform,false);anchor.transform.position=new Vector3(x,1.6f,z+1.5f);
   Part(root.transform,i+"_BACK",PrimitiveType.Cube,new Vector3(x,y,z+.04f),new Vector3(.34f,.50f,.08f),steel,anchor.transform);
   Part(root.transform,i+"_LENS",PrimitiveType.Sphere,new Vector3(x,y,z+.135f),new Vector3(.25f,.38f,.14f),glow,anchor.transform);
   foreach(float offset in new[]{-.14f,0,.14f})
    Part(root.transform,i+"_CAGE_H_"+offset,PrimitiveType.Cube,new Vector3(x,y+offset,z+.215f),new Vector3(.29f,.022f,.026f),steel,anchor.transform);
   foreach(float offset in new[]{-.13f,.13f})
    Part(root.transform,i+"_CAGE_V_"+offset,PrimitiveType.Cube,new Vector3(x+offset,y,z+.215f),new Vector3(.022f,.40f,.026f),steel,anchor.transform);
   var lamp=new GameObject("LB_ROOM_LIGHTING_EMITTER_"+i);lamp.transform.SetParent(root.transform,false);lamp.transform.position=new Vector3(x,y,z+.42f);
   var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=colour;light.intensity=spec.intensity;
   light.range=spec.range;light.shadows=LightShadows.Soft;light.lightmapBakeType=LightmapBakeType.Baked;
  }
 }
 static int Zone(Vector3 p) {return p.z<-8.5f?0:p.z<-6?1:p.z>6?2:p.y>3.3f?3:4;}
 public static int RepairDarkAnchors() {
  if(LightmapSettings.lightProbes==null)return 0;
  LightProbes.Tetrahedralize();int count=0;
  var samples=LightmapSettings.lightProbes.positions.Where(p=>{
   SphericalHarmonicsL2 sh;LightProbes.GetInterpolatedProbe(p,null,out sh);return (sh[0,0]+sh[1,0]+sh[2,0])/3>.01f;
  }).ToArray();
  foreach(var renderer in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())if(renderer.receiveGI==ReceiveGI.LightProbes&&renderer.probeAnchor!=null) {
   SphericalHarmonicsL2 sh;LightProbes.GetInterpolatedProbe(renderer.probeAnchor.position,renderer,out sh);
   if((sh[0,0]+sh[1,0]+sh[2,0])/3>=.01f)continue;
   var candidates=samples.Where(p=>Zone(p)==Zone(renderer.probeAnchor.position)).OrderBy(p=>(p-renderer.probeAnchor.position).sqrMagnitude).ToArray();
   if(candidates.Length==0||Vector3.Distance(candidates[0],renderer.probeAnchor.position)>6)continue;
   renderer.probeAnchor.position=candidates[0];EditorUtility.SetDirty(renderer.probeAnchor);count++;
  }
  Debug.Log("LOWERBAY_LOCAL_ANCHORS_REPAIRED="+count);return count;
 }
 public static void ApplyAndBake() {
  var scene=EditorSceneManager.OpenScene(Root+"/LevelLowerBay_Preview.unity");Apply();
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  LowerBayAstraPreview.BakePreview();
 }
}
#endif
