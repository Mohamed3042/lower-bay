#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;
using UnityEditor;

// Read-only acceptance measurements of the saved candidate. No scene/asset save.
public static class LowerBayFinalChecks {
 const string Root="Assets/LowerBayAstra";
 static void RefineMetals(bool save) {
  foreach(string key in new[]{"car","rail","steel","beam"}) {
   var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Generated/LB_"+key+".mat");
   if(material==null||!material.HasProperty("_NeutralMetal"))throw new Exception("Missing neutral-metal property: "+key);
   material.SetFloat("_NeutralMetal",1);
   if(save)EditorUtility.SetDirty(material);
  }
  var window=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Generated/LB_winlt.mat");
  window.color=new Color(.4f,.42f,.38f);window.SetColor("_EmissionColor",new Color(1,.94f,.82f)*.35f);
  var lettering=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Generated/LB_sign_lt.mat");
  lettering.EnableKeyword("_EMISSION");lettering.SetColor("_EmissionColor",new Color(.14f,.14f,.13f));
  if(save){EditorUtility.SetDirty(window);EditorUtility.SetDirty(lettering);}
  if(save)AssetDatabase.SaveAssets();
 }
 public static void MetalComparison() {
  EditorSceneManager.OpenScene(Root+"/LevelLowerBay_Preview.unity");
  // Unsaved material changes; this process exits without persisting them.
  RefineMetals(false);
  var data=JsonUtility.FromJson<LowerBayAstraPreview.Payload>(File.ReadAllText(Root+"/lowerbay_preview.json"));
  var camera=UnityEngine.Object.FindObjectOfType<Camera>();
  foreach(var pov in data.povs.Where(p=>p.index==1||p.index==9||p.index==10)) {
   camera.transform.position=pov.eye;camera.transform.LookAt(pov.target);camera.fieldOfView=pov.fov;
   var rt=new RenderTexture(1280,720,24){antiAliasing=4};camera.targetTexture=rt;camera.Render();camera.Render();
   var previous=RenderTexture.active;RenderTexture.active=rt;
   var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
   File.WriteAllBytes(Root+"/Captures/metal_comparison_"+pov.index+".png",texture.EncodeToPNG());
   RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
  }
  Debug.Log("LOWERBAY_METAL_COMPARISON_SUCCESS");
 }
 public static void RefineMetalsAndBake() {
  // deploy_unity_preview.ps1 preserves materials and the prior scene/bake first.
  RefineMetals(true);LowerBayAstraPreview.BakePreview();
 }
 static void CaptureInterior(string name,Vector3 eye,Vector3 target) {
  Physics.SyncTransforms();RaycastHit floor;
  if(!Physics.Raycast(eye,Vector3.down,out floor,2,1<<8,QueryTriggerInteraction.Ignore))throw new Exception(name+": QA camera has no nearby floor");
  var camera=UnityEngine.Object.FindObjectOfType<Camera>();camera.transform.position=eye;camera.transform.LookAt(target);camera.fieldOfView=66;
  var rt=new RenderTexture(1280,720,24){antiAliasing=4};camera.targetTexture=rt;camera.Render();camera.Render();
  var previous=RenderTexture.active;RenderTexture.active=rt;
  var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
  File.WriteAllBytes(Root+"/Captures/qa_"+name+".png",texture.EncodeToPNG());
  RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
 }
 [Serializable] public class Detail {
  public string name; public Vector3 centre,anchor; public float anchorDistance,probeL0;
 }
 [Serializable] public class Report {
  public string status="Art/collision candidate only; no runtime certification";
  public int renderers,renderTriangles,roomFixtureTriangles,colliders,collisionTriangles,lightmaps,probes,labelParts;
  public string[] failures,warnings;
  public Detail[] detailLighting;
 }
 public static void Run() {
  EditorSceneManager.OpenScene(Root+"/LevelLowerBay_Preview.unity");
  LowerBaySurfaceRepair.TraceRoutes();
  var failures=new System.Collections.Generic.List<string>();
  var warnings=new System.Collections.Generic.List<string>();
  var renderers=UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
  var colliders=UnityEngine.Object.FindObjectsOfType<MeshCollider>();
  var details=new System.Collections.Generic.List<Detail>();
  LightProbes.Tetrahedralize();
  int triangles=0;
  foreach(var renderer in renderers) {
   var filter=renderer.GetComponent<MeshFilter>();
   if(filter==null||filter.sharedMesh==null){failures.Add(renderer.name+": missing mesh");continue;}
   var mesh=filter.sharedMesh;triangles+=mesh.triangles.Length/3;
   if(renderer.gameObject.layer!=8)failures.Add(renderer.name+": wrong render layer");
   if(renderer.sharedMaterials.Length!=mesh.subMeshCount)failures.Add(renderer.name+": material slot mismatch");
   if(renderer.sharedMaterials.Any(m=>m==null||m.shader==null||!m.shader.isSupported))failures.Add(renderer.name+": invalid material/shader");
   if(renderer.sharedMaterials.Any(m=>m!=null&&m.shader!=null&&(m.shader.name=="Standard"||m.shader.name.StartsWith("LowerBay/"))))failures.Add(renderer.name+": non-legacy preview material remains");
   if(renderer.receiveGI==ReceiveGI.Lightmaps) {
    if(renderer.lightmapIndex<0||renderer.lightmapIndex>=LightmapSettings.lightmaps.Length)failures.Add(renderer.name+": architecture unbaked");
   } else {
    if(renderer.probeAnchor==null){failures.Add(renderer.name+": missing local probe anchor");continue;}
    SphericalHarmonicsL2 sh;LightProbes.GetInterpolatedProbe(renderer.probeAnchor.position,renderer,out sh);
    var item=new Detail{name=renderer.name,centre=renderer.bounds.center,anchor=renderer.probeAnchor.position,
     anchorDistance=Vector3.Distance(renderer.bounds.center,renderer.probeAnchor.position),probeL0=(sh[0,0]+sh[1,0]+sh[2,0])/3};
    details.Add(item);
    if(float.IsNaN(item.probeL0)||float.IsInfinity(item.probeL0))failures.Add(renderer.name+": invalid probe coefficients");
    else if(item.probeL0<.01f)warnings.Add(renderer.name+": nearly black probe sample");
   }
  }
  foreach(var collider in colliders) {
   if(collider.sharedMesh==null||collider.isTrigger||collider.convex||collider.gameObject.layer!=8||
     (collider.tag!="Metal"&&collider.tag!="Cement"))failures.Add(collider.name+": collision contract mismatch");
  }
  var payload=JsonUtility.FromJson<LowerBayAstraPreview.Payload>(File.ReadAllText(Root+"/lowerbay_preview.json"));
  int roomTriangles=renderers.Where(r=>r.name.StartsWith("LB_ROOM_LIGHTING_")).Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);
  if(triangles-roomTriangles!=payload.meshes.Sum(m=>m.triangles.Length/3))failures.Add("Spatial batching changed original render triangle total");
  if(roomTriangles>5000)failures.Add("Wall fixture geometry exceeds bounded detail allowance");
  if(colliders.Length!=payload.collisions.Length)failures.Add("Collision count changed");
  var report=new Report{renderers=renderers.Length,renderTriangles=triangles,roomFixtureTriangles=roomTriangles,colliders=colliders.Length,
   collisionTriangles=colliders.Where(c=>c.sharedMesh!=null).Sum(c=>c.sharedMesh.triangles.Length/3),
   lightmaps=LightmapSettings.lightmaps.Length,probes=LightmapSettings.lightProbes==null?0:LightmapSettings.lightProbes.count,
   labelParts=renderers.Count(r=>r.name.StartsWith("LB_STATION_LABEL_")),
   failures=failures.ToArray(),warnings=warnings.ToArray(),detailLighting=details.ToArray()};
  if(report.lightmaps==0)failures.Add("No saved lightmap atlas");
  report.failures=failures.ToArray();
  File.WriteAllText(Root+"/final_candidate_checks.json",JsonUtility.ToJson(report,true));
  if(failures.Count>0)throw new Exception("Lower Bay final candidate check failed; inspect report");
  // Additional QA positions inside the canonical room envelopes. Original seed
  // POV4 is pillar-occluded and POV5 produces black; preserve them as evidence.
  CaptureInterior("ad_room",new Vector3(5.8f,1.65f,-10.8f),new Vector3(-5,1.8f,-10.8f));
  CaptureInterior("opposite_upper",new Vector3(-34,4.95f,0),new Vector3(0,3.7f,0));
  for(int frame=0;frame<3;frame++) {
   CaptureInterior("rail_sweep_"+frame,new Vector3(27+frame*.18f,.5f,-1.75f),new Vector3(24,-1.02f,.8f));
   CaptureInterior("dado_sweep_"+frame,new Vector3(2+frame*.18f,1.65f,-10.7f),new Vector3(-2,.8f,-12));
  }
  Debug.Log("LOWERBAY_FINAL_CHECKS_SUCCESS renderers="+report.renderers+" triangles="+triangles+" warnings="+warnings.Count);
 }
}
#endif
