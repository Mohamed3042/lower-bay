#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class LowerBaySurfaceRepair {
 const string Root="Assets/LowerBayAstra";
 [Serializable] class Rule {public string under,over;}
 [Serializable] class Spec {public string revision;public Rule[] rules;}
 [Serializable] class Sample {public string route;public Vector3 expected,floor;public bool floorFound;public string[] blockers;}
 [Serializable] class Report {public string status="Sampled corridor-clearance diagnostic, not continuous controller playtest";public Sample[] samples;}
 public static void TraceRoutes() {
  Physics.SyncTransforms();var results=new List<Sample>();
  foreach(float side in new[]{-1f,1f}) {
   for(int i=0;i<=50;i++) {
    float t=i/50f;Vector3 expected=new Vector3(side*33.6f,t*3.4f,Mathf.Lerp(4,12,t));
    Add(results,"concourse_to_spawn_"+side,expected);
   }
   for(int i=0;i<=40;i++)Add(results,"missing_upper_return_"+side,new Vector3(side*36.8f,3.4f,Mathf.Lerp(12,0,i/40f)));
   for(int i=0;i<=80;i++)Add(results,"upper_walk_"+side,new Vector3(side*Mathf.Lerp(33,18,i/80f),3.14f,0));
  }
  File.WriteAllText(Root+"/route_clearance_diagnostic.json",JsonUtility.ToJson(new Report{samples=results.ToArray()},true));
  foreach(var group in results.GroupBy(s=>s.route))Debug.Log("LOWERBAY_ROUTE "+group.Key+" noFloor="+group.Count(s=>!s.floorFound)+" blocked="+group.Count(s=>s.blockers.Length>0)+" names="+string.Join(",",group.SelectMany(s=>s.blockers).Distinct()));
 }
 static void Add(List<Sample> rows,string route,Vector3 expected) {
  RaycastHit hit;bool found=Physics.Raycast(expected+Vector3.up*.5f,Vector3.down,out hit,1f,1<<8,QueryTriggerInteraction.Ignore);
  var foot=found?hit.point:expected;
  var blockers=Physics.OverlapCapsule(foot+Vector3.up*.37f,foot+Vector3.up*1.85f,.35f,1<<8,QueryTriggerInteraction.Ignore);
  rows.Add(new Sample{route=route,expected=expected,floor=foot,floorFound=found,blockers=blockers.Where(c=>!found||c!=hit.collider).Select(c=>c.name).Distinct().ToArray()});
 }
 public static void Apply() {
  var scene=EditorSceneManager.OpenScene(Root+"/LevelLowerBay_Preview.unity");
  var spec=JsonUtility.FromJson<Spec>(File.ReadAllText(Root+"/surface_ownership_revision.json"));
  var data=JsonUtility.FromJson<LowerBayAstraPreview.Payload>(File.ReadAllText(Root+"/lowerbay_preview.json"));
  foreach(string name in spec.rules.Select(r=>r.under).Distinct()) {
   var input=data.meshes.Single(m=>m.name==name);var go=GameObject.Find(name);
   if(go==null||go.transform.root.name!="LevelLowerBay_PREVIEW_NOT_RUNTIME_READY")throw new Exception("Missing owned repair target "+name);
   var filter=go.GetComponent<MeshFilter>();string path=AssetDatabase.GetAssetPath(filter.sharedMesh);
   if(!path.StartsWith(Root+"/Generated/",StringComparison.Ordinal))throw new Exception("Repair target not owned "+path);
   var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.vertices=input.vertices;mesh.normals=input.normals;mesh.uv=input.uv;mesh.subMeshCount=input.materials.Length;
   for(int slot=0;slot<input.materials.Length;slot++)mesh.SetTriangles(Enumerable.Range(0,input.triMaterials.Length).Where(t=>input.triMaterials[t]==slot).SelectMany(t=>input.triangles.Skip(t*3).Take(3)).ToArray(),slot,false);
   mesh.RecalculateBounds();mesh.RecalculateTangents();mesh.uv2=LowerBayAstraPreview.ConnectedLightmapUVs(input);
   EditorUtility.CopySerialized(mesh,filter.sharedMesh);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(filter.sharedMesh);
   Debug.Log("LOWERBAY_SURFACE_REPAIRED "+name+" triangles="+input.triMaterials.Length);
  }
  TraceRoutes();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
 }
 public static void ApplyAndBake(){Apply();LowerBayAstraPreview.BakePreview();}
}
#endif
