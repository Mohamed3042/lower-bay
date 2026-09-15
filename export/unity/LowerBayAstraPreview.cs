#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class LowerBayAstraPreview {
 const string Root="Assets/LowerBayAstra";
 [Serializable] public class MeshInfo { public string name,sourceTag; public Vector3[] vertices,normals; public Vector2[] uv; public int[] triangles,triMaterials; public string[] materials; }
 [Serializable] public class MaterialInfo { public string name,color,emission; public float metallic,smoothness,emissionIntensity; }
 [Serializable] public class Lamp { public Vector3 position; public string color; public float intensity,range; }
 [Serializable] public class POV { public int index; public Vector3 eye,target; public float fov; }
 [Serializable] public class CollisionInfo { public string name,tag; public Vector3[] vertices; public int[] triangles; public int layer; }
 [Serializable] public class Route { public string name; public Vector3[] points; }
 [Serializable] public class Payload { public MeshInfo[] meshes; public MaterialInfo[] materials; public Lamp[] lamps; public POV[] povs; public CollisionInfo[] collisions; public Route[] routes; }
 static void ConfigureAuthoringLayers() {
  var path="ProjectSettings/TagManager.asset";
  var backup="_astra_backups/tags_"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssffff");
  Directory.CreateDirectory(backup);File.Copy(path,backup+"/TagManager.asset");
  var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(path)[0]);
  var layer=settings.FindProperty("layers").GetArrayElementAtIndex(8);
  if(!string.IsNullOrEmpty(layer.stringValue)&&layer.stringValue!="GloballyLit")
   throw new Exception("Layer 8 is occupied by "+layer.stringValue+"; will not overwrite");
  layer.stringValue="GloballyLit";
  var tags=settings.FindProperty("tags");
  foreach(var name in new[]{"Metal","Cement"}) {
   bool exists=false;for(int i=0;i<tags.arraySize;i++)if(tags.GetArrayElementAtIndex(i).stringValue==name)exists=true;
   if(!exists){int i=tags.arraySize;tags.InsertArrayElementAtIndex(i);tags.GetArrayElementAtIndex(i).stringValue=name;}
  }
  settings.ApplyModifiedProperties();AssetDatabase.SaveAssets();
 }
 static Color Colour(string text) { Color c; return ColorUtility.TryParseHtmlString(text,out c)?c:Color.gray; }
 static T Store<T>(T value,string path) where T:UnityEngine.Object {
  var existing=AssetDatabase.LoadAssetAtPath<T>(path);
  if(existing!=null) { EditorUtility.CopySerialized(value,existing);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(existing);return existing; }
  AssetDatabase.CreateAsset(value,path);return value;
 }
 public static Vector2[] ConnectedLightmapUVs(MeshInfo input) {
  // UV0/normal seams must not make every triangle an isolated lightmap chart.
  // Build a position-welded topology proxy, unwrap its triangle corners, then map
  // those corners back onto the deliberately split rendering vertices.
  var positions=new List<Vector3>();var lookup=new Dictionary<Vector3,int>();
  var remap=new int[input.vertices.Length];
  for(int i=0;i<input.vertices.Length;i++) {
   var p=input.vertices[i];int index;
   if(!lookup.TryGetValue(p,out index)) {index=positions.Count;lookup.Add(p,index);positions.Add(p);}
   remap[i]=index;
  }
  var proxy=new Mesh {indexFormat=IndexFormat.UInt32};
  proxy.SetVertices(positions);proxy.triangles=input.triangles.Select(i=>remap[i]).ToArray();
  proxy.RecalculateNormals();proxy.RecalculateBounds();
  UnwrapParam settings;UnwrapParam.SetDefaults(out settings);
  settings.hardAngle=88;settings.packMargin=NeedsExtraPadding(input.name)?.03f:.012f;
  var corners=Unwrapping.GeneratePerTriangleUV(proxy,settings);
  UnityEngine.Object.DestroyImmediate(proxy);
  if(corners==null||corners.Length!=input.triangles.Length)throw new Exception("UV2 generation failed: "+input.name);
  var uv2=new Vector2[input.vertices.Length];
  for(int i=0;i<corners.Length;i++) {
   var uv=corners[i];
   if(float.IsNaN(uv.x)||float.IsNaN(uv.y)||uv.x<-.0001f||uv.x>1.0001f||uv.y<-.0001f||uv.y>1.0001f)
    throw new Exception("Invalid UV2: "+input.name);
   uv2[input.triangles[i]]=uv;
  }
  return uv2;
 }
 static bool NeedsExtraPadding(string name) {
  return new[]{"LB_SEED_deck_grate","LB_SEED_stair_concrete","LB_SEED_glasswall_conc_d","LB_SEED_shell_conc_d","LB_CONCOURSE_PIERS_22"}.Contains(name);
 }
 static bool ProbeDetail(MeshInfo input) {
  return new[]{"ad","graf","sign","prop","lamp","glass","rail","gate","portal","vend"}.Contains(input.sourceTag)
   ||input.name.StartsWith("LB_SEED_deck_steel")||input.name.StartsWith("LB_SEED_booth_sign_lt");
 }
 static IEnumerable<MeshInfo> SpatialDetails(MeshInfo input) {
  if(!ProbeDetail(input)){yield return input;yield break;}
  // The source groups disconnected fixtures by material across the entire station.
  // Split only rendering batches into local cells; never change geometry or collision.
  var cells=new SortedDictionary<string,List<int>>();
  for(int t=0;t<input.triMaterials.Length;t++) {
   var c=(input.vertices[input.triangles[t*3]]+input.vertices[input.triangles[t*3+1]]+input.vertices[input.triangles[t*3+2]])/3;
   string key=Mathf.FloorToInt(c.x/6)+"_"+Mathf.FloorToInt(c.y/3)+"_"+Mathf.FloorToInt(c.z/6);
   if(!cells.ContainsKey(key))cells[key]=new List<int>();cells[key].Add(t);
  }
  foreach(var cell in cells) {
   var indices=cell.Value.SelectMany(t=>new[]{input.triangles[t*3],input.triangles[t*3+1],input.triangles[t*3+2]}).ToArray();
   yield return new MeshInfo{name=input.name+"_CELL_"+cell.Key,sourceTag=input.sourceTag,
    vertices=indices.Select(i=>input.vertices[i]).ToArray(),normals=indices.Select(i=>input.normals[i]).ToArray(),uv=indices.Select(i=>input.uv[i]).ToArray(),
    triangles=Enumerable.Range(0,indices.Length).ToArray(),triMaterials=cell.Value.Select(t=>input.triMaterials[t]).ToArray(),materials=input.materials};
  }
 }
 static string Tex(string key) {
  switch(key) {
   case "tile": case "tile_d": case "skirt":return "Floor_B_DM";
   case "concrete":return "Floor_A";
   case "conc_d":case "ceil":case "canopy":return "Floor_B_DM";
   case "ballast":return "Rock_3";
   case "steel":case "beam":return "Metal02";
   case "rail":case "car":case "vend":return "Metal01";
   case "grate":return "Metal03";
   case "car_trim":case "vend_bd":return "BlackMetal";
   default:return null;
  }
 }
 public static void Build() {
  ConfigureAuthoringLayers();
  Directory.CreateDirectory(Root+"/Generated"); Directory.CreateDirectory(Root+"/Captures");
  var data=JsonUtility.FromJson<Payload>(File.ReadAllText(Root+"/lowerbay_preview.json"));
  if(data.meshes.Length<40)throw new Exception("Incomplete mesh payload");
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var root=new GameObject("LevelLowerBay_PREVIEW_NOT_RUNTIME_READY");
  var mats=new Dictionary<string,Material>();
  foreach(var source in data.materials) {
   bool tile=source.name=="tile"||source.name=="tile_d"||source.name=="skirt";
   bool worn=!tile&&Tex(source.name)!=null&&source.name!="vend";
   var shader=Shader.Find(tile?"LowerBay/FootageSquareTile":worn?"LowerBay/WornSurface":"Standard");
   if(shader==null||!shader.isSupported)throw new Exception("Missing shader for "+source.name);
   var material=new Material(shader); material.name="LB_"+source.name;
   var colour=Colour(source.color);
   if(source.name=="tile")colour=new Color(.76f,.74f,.68f);
   if(source.name=="skirt"||source.name=="tile_d")colour=new Color(.20f,.23f,.22f);
   material.color=colour;
   material.SetFloat("_Glossiness",tile?.2f:Mathf.Min(source.smoothness,.45f));
   if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",source.metallic*.6f);
   if(material.HasProperty("_NeutralMetal"))material.SetFloat("_NeutralMetal",new[]{"car","rail","steel","beam"}.Contains(source.name)?1:0);
   var texture=Tex(source.name);
   if(texture!=null) {
    var t=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ForgeTextures/"+texture+".png");
    if(t==null)throw new Exception("Missing texture "+texture);
    material.mainTexture=t; material.mainTextureScale=tile?Vector2.one:Vector2.one*.4f;
   }
   if(source.name=="glass") {
    material.SetFloat("_Mode",3); material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);
    material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetInt("_ZWrite",0);
    material.EnableKeyword("_ALPHABLEND_ON");material.renderQueue=3000;
    material.color=new Color(.50f,.65f,.68f,.17f);
   }
   bool glow=source.name=="winlt"||source.name=="lamp"||source.name=="daylight";
   if(glow||source.emissionIntensity>0) {
    material.EnableKeyword("_EMISSION");
    Color c=source.name=="winlt"?new Color(1,.89f,.64f):source.name=="lamp"?new Color(1,.92f,.76f):Colour(source.emission);
    material.SetColor("_EmissionColor",c*(glow?.75f:Mathf.Min(source.emissionIntensity,.4f)));
    material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
   }
   if(source.name=="winlt") {material.color=new Color(.4f,.42f,.38f);material.SetColor("_EmissionColor",new Color(1,.94f,.82f)*.35f);}
   if(source.name=="sign_lt") {material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(.14f,.14f,.13f));}
   material=Store(material,Root+"/Generated/"+material.name+".mat");mats.Add(source.name,material);
  }
  int triangles=0;
  foreach(var input in data.meshes.SelectMany(SpatialDetails)) {
   var mesh=new Mesh {name=input.name,indexFormat=IndexFormat.UInt32};
   mesh.vertices=input.vertices;mesh.normals=input.normals;mesh.uv=input.uv;mesh.subMeshCount=input.materials.Length;
   for(int slot=0;slot<input.materials.Length;slot++) {
    var indices=new List<int>();
    for(int tri=0;tri<input.triMaterials.Length;tri++)if(input.triMaterials[tri]==slot) {
     indices.Add(input.triangles[tri*3]);indices.Add(input.triangles[tri*3+1]);indices.Add(input.triangles[tri*3+2]);
    }
    mesh.SetTriangles(indices,slot,false);
   }
   mesh.RecalculateBounds();mesh.RecalculateTangents();
   if(!ProbeDetail(input))mesh.uv2=ConnectedLightmapUVs(input);
   mesh=Store(mesh,Root+"/Generated/"+input.name+".asset");
   var go=new GameObject(input.name);go.layer=8;go.transform.SetParent(root.transform,false);
   go.AddComponent<MeshFilter>().sharedMesh=mesh;
   var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=input.materials.Select(n=>mats[n]).ToArray();
   // Thin decorative meshes have too little atlas area for safe chart margins.
   // Keep architecture lightmapped; use the baked probe volume for small details.
   bool detail=ProbeDetail(input);
   renderer.receiveGI=detail?ReceiveGI.LightProbes:ReceiveGI.Lightmaps;
   renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
   renderer.scaleInLightmap=input.name=="LB_SEED_booth_conc_d"?3:NeedsExtraPadding(input.name)?1.5f:1;
   if(input.name.StartsWith("LB_STATION_LABEL_"))renderer.shadowCastingMode=ShadowCastingMode.Off;
   GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|(detail?0:StaticEditorFlags.ContributeGI));
   triangles+=input.triMaterials.Length;
  }
  RenderSettings.ambientMode=AmbientMode.Flat;
  RenderSettings.ambientLight=new Color(.38f,.40f,.43f);
  // In a synchronous batch capture the editor has not refreshed the ambient SH yet.
  // Supply the preview ambient probe explicitly; this is not a baked-lighting claim.
  var ambientProbe=new SphericalHarmonicsL2();
  ambientProbe.AddAmbientLight(new Color(.28f,.30f,.33f));
  RenderSettings.ambientProbe=ambientProbe;
  RenderSettings.ambientSkyColor=new Color(.27f,.31f,.36f);RenderSettings.ambientEquatorColor=new Color(.17f,.18f,.19f);
  RenderSettings.ambientGroundColor=new Color(.09f,.09f,.08f);
  RenderSettings.fog=false;
  foreach(var source in data.lamps) {
   var go=new GameObject("LB_PREVIEW_LIGHT");go.transform.SetParent(root.transform,false);go.transform.position=source.position;
   var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=Colour(source.color);
   light.intensity=source.intensity;light.range=source.range;light.shadows=LightShadows.Soft;
   light.lightmapBakeType=LightmapBakeType.Baked;
  }
  QualitySettings.pixelLightCount=8;QualitySettings.shadowDistance=80;
  var probes=new GameObject("LightProbes");probes.transform.SetParent(root.transform,false);
  var probePositions=new List<Vector3>();
  foreach(float y in new[]{.5f,1.6f})foreach(float z in new[]{-4f,0f,4f})
   for(int x=-36;x<=36;x+=6)probePositions.Add(new Vector3(x,y,z));
  foreach(float y in new[]{3.7f,4.7f})for(int x=-36;x<=36;x+=6)
   if(Mathf.Abs(x)>=18)probePositions.Add(new Vector3(x,y,0));
  foreach(float y in new[]{.5f,1.6f,3.1f})foreach(float z in new[]{8f,11f,14f})
   for(int x=-30;x<=30;x+=6)probePositions.Add(new Vector3(x,y,z));
  foreach(float y in new[]{.5f,1.6f,2.8f})for(int x=-30;x<=30;x+=6)probePositions.Add(new Vector3(x,y,-7.2f));
  foreach(float y in new[]{.5f,1.6f,2.8f})foreach(float x in new[]{-5f,0f,5f})probePositions.Add(new Vector3(x,y,-10));
  probes.AddComponent<LightProbeGroup>().probePositions=probePositions.ToArray();
  var anchors=new GameObject("LocalDetailProbeAnchors");anchors.transform.SetParent(root.transform,false);
  // Exclude samples inside the train body. Anchors use authored interior samples,
  // not mesh centres inside metal, flooring, walls or the train.
  var safeSamples=probePositions.Where(p=>!(Mathf.Abs(p.x)<18&&Mathf.Abs(p.z)<2&&p.y<2.3f)).ToArray();
  foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())if(renderer.receiveGI==ReceiveGI.LightProbes) {
   var anchor=new GameObject(renderer.name+"_Probe");anchor.transform.SetParent(anchors.transform,false);
   anchor.transform.position=safeSamples.OrderBy(p=>(p-renderer.bounds.center).sqrMagnitude).First();
   renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.probeAnchor=anchor.transform;
  }
  // Local baked reflections give metals a station interior instead of a black default cube.
  var reflectionCentres=new[]{new Vector3(0,2.7f,0),new Vector3(0,1.95f,11),new Vector3(0,1.75f,-8)};
  var reflectionSizes=new[]{new Vector3(76,5.4f,12),new Vector3(76,3.9f,10),new Vector3(76,3.5f,4)};
  for(int i=0;i<reflectionCentres.Length;i++) {
   var go=new GameObject("BakedReflection_"+i);go.transform.SetParent(root.transform,false);go.transform.position=reflectionCentres[i];
   var probe=go.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Baked;
   probe.size=reflectionSizes[i];probe.boxProjection=true;probe.resolution=128;probe.nearClipPlane=.1f;probe.farClipPlane=100;
   probe.clearFlags=ReflectionProbeClearFlags.SolidColor;probe.backgroundColor=new Color(.04f,.05f,.06f);
  }
  var collisionRoot=new GameObject("Collision");collisionRoot.transform.SetParent(root.transform,false);
  if(data.collisions!=null)foreach(var input in data.collisions) {
   var mesh=new Mesh {name=input.name,indexFormat=IndexFormat.UInt32};mesh.vertices=input.vertices;mesh.triangles=input.triangles;mesh.RecalculateBounds();
   mesh=Store(mesh,Root+"/Generated/"+input.name+".asset");
   var go=new GameObject(input.name);go.layer=input.layer;go.transform.SetParent(collisionRoot.transform,false);
   if(UnityEditorInternal.InternalEditorUtility.tags.Contains(input.tag))go.tag=input.tag;
   else Debug.LogWarning("Runtime surface tag unavailable in authoring project: "+input.tag);
   var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=false;collider.isTrigger=false;
  }
  LowerBayRoomLighting.Apply();
  LowerBayLegacyMaterials.Apply();
  var camgo=new GameObject("PreviewCamera");var camera=camgo.AddComponent<Camera>();
  camera.nearClipPlane=.05f;camera.farClipPlane=200;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.05f,.06f);
  camera.allowHDR=true;
  AssetDatabase.SaveAssets();
  EditorSceneManager.SaveScene(scene,Root+"/LevelLowerBay_Preview.unity");
  foreach(var pov in data.povs.Where(p=>p.index!=3)) {
   camera.transform.position=pov.eye;camera.transform.LookAt(pov.target);camera.fieldOfView=pov.fov;
   var rt=new RenderTexture(1280,720,24){antiAliasing=4};camera.targetTexture=rt;camera.Render();
   var prior=RenderTexture.active;RenderTexture.active=rt;
   var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
   File.WriteAllBytes(Root+"/Captures/pov_"+pov.index+".png",image.EncodeToPNG());
   RenderTexture.active=prior;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
  }
  camera.transform.position=data.povs[0].eye;camera.transform.LookAt(data.povs[0].target);camera.fieldOfView=data.povs[0].fov;
  EditorSceneManager.SaveScene(scene);
  ValidateCollisionRoutes(data);
  File.WriteAllText(Root+"/preview_report.json","{\"status\":\"visual preview only; bake and gameplay wiring pending\",\"sourceGroups\":"+data.meshes.Length+",\"renderers\":"+root.GetComponentsInChildren<MeshRenderer>().Length+",\"triangles\":"+triangles+"}");
  Debug.Log("LOWERBAY_ASTRA_PREVIEW_SUCCESS triangles="+triangles);
 }
 [Serializable] public class Probe {public string route,status,surface,obstacle;public Vector3 requested,floor; public float headroom;}
 [Serializable] public class ProbeReport {public string status="Point samples only; not full player-controller certification";public int colliders;public Probe[] probes;}
 static void ValidateCollisionRoutes(Payload data) {
  Physics.SyncTransforms();var results=new List<Probe>();
  if(data.routes!=null)foreach(var route in data.routes)foreach(var p in route.points) {
   var result=new Probe{route=route.name,requested=p,status="NO_FLOOR",headroom=-1};
   RaycastHit floor;
   // 1.8 m includes the authored catwalk-to-train drop; never fills it with invented floor.
   if(Physics.Raycast(p+Vector3.up*.6f,Vector3.down,out floor,1.8f,1<<8,QueryTriggerInteraction.Ignore)) {
    result.floor=floor.point;result.surface=floor.collider.name;
    var start=floor.point+Vector3.up*.05f;RaycastHit ceiling;
    result.headroom=Physics.Raycast(start,Vector3.up,out ceiling,8,1<<8)?ceiling.distance+.05f:8;
    // 1 m diameter / 2 m standing capsule, with 2 cm contact tolerance at the floor.
    var hits=Physics.OverlapCapsule(floor.point+Vector3.up*.52f,floor.point+Vector3.up*1.52f,.48f,1<<8,QueryTriggerInteraction.Ignore);
    result.obstacle=string.Join(";",hits.Select(c=>c.name));
    result.status=result.headroom<1.999f?"LOW_HEADROOM":hits.Length>0?"CAPSULE_OVERLAP":"POINT_CLEAR";
   }
   results.Add(result);
  }
  File.WriteAllText(Root+"/collision_route_report.json",JsonUtility.ToJson(new ProbeReport{colliders=UnityEngine.Object.FindObjectsOfType<MeshCollider>().Length,probes=results.ToArray()},true));
 }
 public static void BakePreview() {
  var path=Root+"/LevelLowerBay_Preview.unity";
  if(!File.Exists(path))throw new Exception("Preview scene missing");
  var backup=Path.Combine(Application.dataPath,"../_astra_backups/diagnostic_bake_"+DateTime.UtcNow.ToString("yyyyMMddTHHmmss"));
  Directory.CreateDirectory(backup);File.Copy(path,Path.Combine(backup,"before_bake.unity"));
  var scene=EditorSceneManager.OpenScene(path);
  var settings=new LightingSettings();settings.name="LowerBay_Diagnostic_NotFinal";
  settings.lightmapper=LightingSettings.Lightmapper.ProgressiveCPU;
  settings.lightmapResolution=8;settings.lightmapMaxSize=2048;
  settings.directSampleCount=64;settings.indirectSampleCount=128;settings.environmentSampleCount=64;
  settings.maxBounces=2;settings.ao=true;settings.aoMaxDistance=.8f;
  settings=Store(settings,Root+"/Generated/DiagnosticLighting.lighting");
  Lightmapping.lightingSettings=settings;
  EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  if(!Lightmapping.Bake())throw new Exception("Diagnostic bake returned false");
  if(LightmapSettings.lightmaps.Length==0)throw new Exception("Diagnostic bake produced no lightmaps");
  if(LowerBayRoomLighting.RepairDarkAnchors()>0)EditorSceneManager.MarkSceneDirty(scene);
  EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  var data=JsonUtility.FromJson<Payload>(File.ReadAllText(Root+"/lowerbay_preview.json"));
  var renderers=UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
  var validation=new BakeValidation {
   renderers=renderers.Length,colliders=UnityEngine.Object.FindObjectsOfType<MeshCollider>().Length,
   lightmaps=LightmapSettings.lightmaps.Length,
   missingMaterials=renderers.Sum(r=>r.sharedMaterials.Count(m=>m==null||m.shader==null||!m.shader.isSupported)),
   wrongRenderLayers=renderers.Count(r=>r.gameObject.layer!=8),
   lightmappedRenderers=renderers.Count(r=>r.receiveGI==ReceiveGI.Lightmaps),
   probeLitRenderers=renderers.Count(r=>r.receiveGI==ReceiveGI.LightProbes),
   unbakedArchitecture=renderers.Where(r=>r.receiveGI==ReceiveGI.Lightmaps&&(r.lightmapIndex<0||r.lightmapIndex>=LightmapSettings.lightmaps.Length)).Select(r=>r.name).ToArray(),
   realtimeLights=UnityEngine.Object.FindObjectsOfType<Light>().Count(l=>l.lightmapBakeType!=LightmapBakeType.Baked),
   lightProbes=LightmapSettings.lightProbes==null?0:LightmapSettings.lightProbes.count
  };
  File.WriteAllText(Root+"/bake_validation.json",JsonUtility.ToJson(validation,true));
  if(validation.missingMaterials>0||validation.wrongRenderLayers>0||validation.unbakedArchitecture.Length>0)
   throw new Exception("Bake validation failed; inspect bake_validation.json");
  var camera=UnityEngine.Object.FindObjectOfType<Camera>();
  foreach(var volume in UnityEngine.Object.FindObjectsOfType<LightProbeProxyVolume>())volume.Update();
  foreach(var pov in data.povs.Where(p=>p.index!=3)) {
   camera.transform.position=pov.eye;camera.transform.LookAt(pov.target);camera.fieldOfView=pov.fov;
   var rt=new RenderTexture(1280,720,24){antiAliasing=4};camera.targetTexture=rt;camera.Render();
   var prior=RenderTexture.active;RenderTexture.active=rt;
   var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
   File.WriteAllBytes(Root+"/Captures/diagnostic_baked_"+pov.index+".png",image.EncodeToPNG());
   RenderTexture.active=prior;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
  }
  File.WriteAllText(Root+"/diagnostic_bake_report.json","{\"status\":\"diagnostic bake, not production\",\"lightmaps\":"+LightmapSettings.lightmaps.Length+"}");
  Debug.Log("LOWERBAY_DIAGNOSTIC_BAKE_SUCCESS lightmaps="+LightmapSettings.lightmaps.Length);
 }
 public static void CheckProbeCapture() {
  EditorSceneManager.OpenScene(Root+"/LevelLowerBay_Preview.unity");
  var camera=UnityEngine.Object.FindObjectOfType<Camera>();
  camera.transform.position=new Vector3(34,1.65f,4);camera.transform.LookAt(new Vector3(-34,2,4));camera.fieldOfView=58;
  foreach(var volume in UnityEngine.Object.FindObjectsOfType<LightProbeProxyVolume>())volume.Update();
  var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;camera.Render();camera.Render();
  var prior=RenderTexture.active;RenderTexture.active=rt;
  var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
  File.WriteAllBytes(Root+"/Captures/probe_check.png",image.EncodeToPNG());
  foreach(var renderer in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())if(renderer.receiveGI==ReceiveGI.LightProbes) {
   renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.lightProbeProxyVolumeOverride=null;
  }
  camera.Render();camera.Render();
  image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
  File.WriteAllBytes(Root+"/Captures/probe_check_blend.png",image.EncodeToPNG());
  LightProbes.Tetrahedralize();
  var probeNotes=new List<string>();
  foreach(var pos in new[]{new Vector3(0,1.6f,4),new Vector3(30,1.6f,4),new Vector3(0,1.6f,11)}) {
   SphericalHarmonicsL2 sh;LightProbes.GetInterpolatedProbe(pos,null,out sh);
   probeNotes.Add(pos+" L0="+sh[0,0]+","+sh[1,0]+","+sh[2,0]);
  }
  foreach(var renderer in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())if(renderer.receiveGI==ReceiveGI.LightProbes) {
   probeNotes.Add(renderer.name+" center="+renderer.bounds.center+" lm="+renderer.lightmapIndex);
   renderer.lightmapIndex=-1;
  }
  File.WriteAllLines(Root+"/probe_diagnostic.txt",probeNotes);
  camera.Render();camera.Render();
  image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
  File.WriteAllBytes(Root+"/Captures/probe_check_tetra.png",image.EncodeToPNG());
  var diagnosticMaterials=new List<Material>();
  foreach(var renderer in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())if(renderer.receiveGI==ReceiveGI.LightProbes) {
   renderer.sharedMaterials=renderer.sharedMaterials.Select(original=>{
    var material=new Material(Shader.Find("Standard"));material.color=Color.gray;
    diagnosticMaterials.Add(material);return material;
   }).ToArray();
  }
  camera.Render();camera.Render();image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
  File.WriteAllBytes(Root+"/Captures/probe_check_neutral.png",image.EncodeToPNG());
  var diagnosticAnchor=new GameObject("UnsavedProbeDiagnosticAnchor");diagnosticAnchor.transform.position=new Vector3(30,1.6f,4);
  foreach(var renderer in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())if(renderer.receiveGI==ReceiveGI.LightProbes)renderer.probeAnchor=diagnosticAnchor.transform;
  camera.Render();camera.Render();image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
  File.WriteAllBytes(Root+"/Captures/probe_check_anchor.png",image.EncodeToPNG());
  foreach(var material in diagnosticMaterials)UnityEngine.Object.DestroyImmediate(material);
  UnityEngine.Object.DestroyImmediate(diagnosticAnchor);
  RenderTexture.active=prior;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
  Debug.Log("LOWERBAY_PROBE_CAPTURE_SUCCESS");
 }
 public static void PackageVisualCandidate() {
  // A portable art/collision handoff, never a falsely certified runtime bundle.
  var scene=Root+"/LevelLowerBay_Preview.unity";
  var paths=AssetDatabase.GetDependencies(scene,true)
   .Where(p=>p.StartsWith(Root+"/")||p.StartsWith("Assets/ForgeTextures/")).Distinct().ToArray();
  if(!paths.Any(p=>p.EndsWith(".unity"))||!paths.Any(p=>p.EndsWith("LightingData.asset")))
   throw new Exception("Scene/bake dependency missing; package refused");
  Directory.CreateDirectory("Builds/LowerBayAstra");
  var path="Builds/LowerBayAstra/LowerBay_VisualCandidate_"+DateTime.UtcNow.ToString("yyyyMMddTHHmmss")+".unitypackage";
  AssetDatabase.ExportPackage(paths,path,ExportPackageOptions.Default);
  File.WriteAllText("Builds/LowerBayAstra/package_report.json",JsonUtility.ToJson(new PackageReport{package=path,assets=paths},true));
  Debug.Log("LOWERBAY_VISUAL_PACKAGE_SUCCESS "+path);
 }
 [Serializable] public class PackageReport {public string status="Art/collision handoff only: real client components, catalog MapId and gameplay certification pending";public string package;public string[] assets;}
 [Serializable] public class BakeValidation {
  public string status="Measured rendering/bake checks only, not runtime certification";
  public int renderers,colliders,lightmaps,missingMaterials,wrongRenderLayers,lightmappedRenderers,probeLitRenderers,realtimeLights,lightProbes;
  public string[] unbakedArchitecture;
 }
}
#endif
