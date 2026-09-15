#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Texture-bakes the existing procedural preview's surface roles into mipmapped
// legacy maps. Donor ShaderLab sources remain byte-for-byte unchanged.
public static class LowerBayLegacyMaterials {
 const string Root="Assets/LowerBayAstra", Out=Root+"/LegacyTextures";
 static int Size=512;
 [Serializable] class Spec {public int textureSize;public float tilesPerMetre,shininess,specularColour,metalGloss,railGloss;}
 static Spec settings;
 [Serializable] public class Row {public string material,shader,shaderPath;public bool supported;}
 [Serializable] public class Report {public string revision="legacy_materials_r1",status;public int materials;public Row[] assignments;}
 static Shader Source(string file) {
  var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/LegacyShaders/"+file+".shader");
  if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Invalid legacy shader: "+file);
  return shader;
 }
 static Texture2D Readable(Texture source) {
  if(source==null)return Texture2D.whiteTexture;
  var rt=RenderTexture.GetTemporary(Size,Size,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
  var previous=RenderTexture.active;Graphics.Blit(source,rt);RenderTexture.active=rt;
  var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,false,false);
  texture.ReadPixels(new Rect(0,0,Size,Size),0,0);texture.Apply();
  RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);return texture;
 }
 static Texture2D Save(string name,Color[] pixels,bool normal=false) {
  var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,false,normal);texture.SetPixels(pixels);texture.Apply();
  string path=Out+"/"+name+".png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
  AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
  var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
  importer.sRGBTexture=!normal;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;
  importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.maxTextureSize=Size;
  importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
  return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
 }
 static float Luma(Color c){return c.r*.299f+c.g*.587f+c.b*.114f;}
 static float Smooth(float a,float b,float x){float t=Mathf.Clamp01((x-a)/(b-a));return t*t*(3-2*t);}
 static float Edge(float t){return Smooth(.01f,.025f,t)-Smooth(.025f,.04f,t)-Smooth(.96f,.975f,t)+Smooth(.975f,.99f,t);}
 static void BakeSurface(Material m,string key,Texture2D source,bool tile,bool neutral) {
  var albedo=new Color[Size*Size];var normals=new Color[Size*Size];
  for(int y=0;y<Size;y++)for(int x=0;x<Size;x++) {
   float u=(x+.5f)/Size,v=(y+.5f)/Size;Color c=source.GetPixelBilinear(u,v);float value=Luma(c);float nx=0,ny=0;
   if(tile) {
    float cx=u*4,cy=v*4,fx=cx-Mathf.Floor(cx),fy=cy-Mathf.Floor(cy);
    float edge=Mathf.Min(Mathf.Min(fx,1-fx),Mathf.Min(fy,1-fy));
    float face=Smooth(.006f,.022f,edge);
    uint seed=(uint)((int)cx*1973+(int)cy*9277+89173);seed=(seed^61)^(seed>>16);seed*=9;seed^=seed>>4;
    float wear=.94f+.06f*((seed%1000)/999f);
    float shade=Mathf.Lerp(.30f,wear*Mathf.Lerp(.8f,1,value),face);
    c=new Color(shade,shade,shade,Mathf.Lerp(.025f,.20f,face));nx=-Edge(fx)*.12f;ny=-Edge(fy)*.12f;
   } else if(neutral) {
    // Periodic, lower-frequency brushed finish; normal/specular maps receive mipmaps.
    float brush=.5f+.5f*Mathf.Sin(v*Mathf.PI*2*64);
    float shade=Mathf.Lerp(.88f,.96f,brush);c=new Color(shade,shade,shade,key=="rail"?settings.railGloss:settings.metalGloss);
    ny=(brush-.5f)*.012f;
   } else {
    nx=(value-Luma(source.GetPixelBilinear(u+1f/Size,v)))*.35f;
    ny=(value-Luma(source.GetPixelBilinear(u,v+1f/Size)))*.35f;c.a=.12f;
   }
   var n=new Vector3(nx,ny,1).normalized;albedo[y*Size+x]=c;normals[y*Size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);
  }
  m.mainTexture=Save(key+"_albedo_gloss",albedo);m.SetTexture("_BumpMap",Save(key+"_normal",normals,true));
  var scale=tile?Vector2.one*(settings.tilesPerMetre/4):Vector2.one*.4f;m.mainTextureScale=scale;m.SetTextureScale("_BumpMap",scale);
 }
 public static void Apply() {
  settings=JsonUtility.FromJson<Spec>(File.ReadAllText(Root+"/legacy_material_revision.json"));Size=settings.textureSize;
  if(Size<128||Size>2048||!Mathf.IsPowerOfTwo(Size)||settings.tilesPerMetre<=0||settings.shininess<=0)throw new Exception("Invalid legacy material construction spec");
  Directory.CreateDirectory(Out);
  Shader bump=Source("Normal-BumpSpec"),illum=Source("Illumin-Diffuse"),glass=Source("Particle Alpha Blend Cull Back");
  var payload=JsonUtility.FromJson<LowerBayAstraPreview.Payload>(File.ReadAllText(Root+"/lowerbay_preview.json"));
  var rows=new List<Row>();
  foreach(var source in payload.materials) {
   string key=source.name,path=Root+"/Generated/LB_"+key+".mat";
   var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null)throw new Exception("Missing existing material "+path);
   // Use source donor textures, not previous generated output: reruns stay idempotent.
   string tex=key=="tile"||key=="tile_d"||key=="skirt"||key=="conc_d"||key=="ceil"||key=="canopy"?"Floor_B_DM":
    key=="concrete"?"Floor_A":key=="ballast"?"Rock_3":key=="steel"||key=="beam"?"Metal02":
    key=="rail"||key=="car"||key=="vend"?"Metal01":key=="grate"?"Metal03":key=="car_trim"||key=="vend_bd"?"BlackMetal":null;
   bool tile=key=="tile"||key=="tile_d"||key=="skirt";
   bool glow=key=="winlt"||key=="lamp"||key=="daylight"||key=="sign_lt"||source.emissionIntensity>0;
   var color=m.HasProperty("_Color")?m.GetColor("_Color"):Color.white;
   m.shaderKeywords=new string[0];m.renderQueue=-1;m.shader=key=="glass"?glass:glow?illum:bump;
   m.globalIlluminationFlags=glow?MaterialGlobalIlluminationFlags.BakedEmissive:MaterialGlobalIlluminationFlags.EmissiveIsBlack;
   if(key=="glass") {
    m.SetColor("_TintColor",new Color(.25f,.325f,.34f,.085f));m.mainTexture=Texture2D.whiteTexture;m.SetFloat("_InvFade",1);
   } else {
    m.SetColor("_Color",color);
    if(glow) {
     // Existing glow colors remain subdued; dedicated baked fixtures provide illumination.
     float amount=key=="sign_lt"?.35f:key=="winlt"?.85f:.75f;
     var emission=Enumerable.Repeat(new Color(1,1,1,amount),Size*Size).ToArray();
     m.mainTexture=Texture2D.whiteTexture;m.SetTexture("_Illum",Save(key+"_illum",emission));m.SetFloat("_EmissionLM",amount);
    } else {
     m.SetFloat("_Shininess",settings.shininess);m.SetColor("_SpecColor",new Color(settings.specularColour,settings.specularColour,settings.specularColour,1));
     var original=tex==null?null:AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ForgeTextures/"+tex+".png");
     if(tex!=null&&original==null)throw new Exception("Missing donor texture "+tex);
     var readable=Readable(original);BakeSurface(m,key,readable,tile,new[]{"car","rail","steel","beam"}.Contains(key));
     if(readable!=Texture2D.whiteTexture)UnityEngine.Object.DestroyImmediate(readable);
    }
   }
   EditorUtility.SetDirty(m);rows.Add(new Row{material=m.name,shader=m.shader.name,shaderPath=AssetDatabase.GetAssetPath(m.shader),supported=m.shader.isSupported});
  }
  AssetDatabase.SaveAssets();
  var report=new Report{status="Legacy material assignments applied; geometry unchanged; lighting requires rebake; not runtime/footage certification",materials=rows.Count,assignments=rows.ToArray()};
  File.WriteAllText(Root+"/legacy_material_report.json",JsonUtility.ToJson(report,true));Debug.Log("LOWERBAY_LEGACY_MATERIALS_SUCCESS="+rows.Count);
 }
 public static void ApplyAndCheck() {EditorSceneManager.OpenScene(Root+"/LevelLowerBay_Preview.unity");Apply();LowerBayFinalChecks.Run();}
 public static void ApplyAndBake(){Apply();LowerBayAstraPreview.BakePreview();}
}
#endif
