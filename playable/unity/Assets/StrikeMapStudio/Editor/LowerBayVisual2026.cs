using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace StrikeMapStudio.Editor
{
    // This is a visual layer over the proven map contract, not a second layout.
    public static class LowerBayVisual2026
    {
        private const string Assets = "Assets/LowerBay2026";
        private static string folder;
        private static int serial;
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private sealed class DetailBatch
        {
            public Material material;
            public readonly List<CombineInstance> meshes = new List<CombineInstance>();
        }
        private static readonly Dictionary<string, DetailBatch> details = new Dictionary<string, DetailBatch>();

        public static void Apply(GameObject root, string sceneFolder)
        {
            folder = sceneFolder + "/Realism2026";
            AssetDatabase.CreateFolder(sceneFolder, "Realism2026");
            serial = 0; materials.Clear(); details.Clear();
            ConfigureImports();
            Material tile = Surface("ivory glazed ceramic", "Tiles008", "#e1decb", .66f, .18f);
            Material dado = Surface("forest green glazed dado", "Tiles008", "#27483c", .7f, .12f);
            Material floor = Surface("worn station concrete", "Concrete033", "#aaaead", .7f, .15f, .68f);
            Material ceiling = Surface("stained concrete structure", "Concrete033", "#b3b6b0", 1, .2f);
            Material trim = Surface("worn concrete capitals", "Concrete048", "#919995", .95f, .15f);
            Material steel = Solid("galvanized structural steel", "#454e4b", .78f, .48f);
            Material brightSteel = Solid("polished rail heads", "#a2aaa9", .92f, .28f);
            Material yellow = Solid("ochre tactile safety surface", "#b89a41", .05f, .66f);
            Material stainless = Solid("brushed stainless equipment", "#858f8b", .82f, .42f);
            Material lcd = Solid("ticket display glass", "#102b30", .05f, .24f);
            lcd.EnableKeyword("_EMISSION"); lcd.SetColor("_EmissionColor", Hex("#163e46")*.18f);
            Material ticketHeader = Solid("ticket header enamel", "#562931", .1f, .4f);
            var entities = root.GetComponentsInChildren<StrikeMapEntity>();
            foreach (var entity in entities)
            {
                string id = entity.sourceId;
                var renderer = entity.GetComponent<MeshRenderer>();
                var filter = entity.GetComponent<MeshFilter>();
                if (id.StartsWith("train-") || id.StartsWith("bench-") || id.StartsWith("vending-") || id=="red-catwalk" || id=="blue-catwalk")
                {
                    renderer.enabled = false; renderer.forceRenderingOff = true;
                    continue;
                }
                string texture = renderer.sharedMaterial.mainTexture != null ? renderer.sharedMaterial.mainTexture.name : "";
                bool surface = false;
                if (id.StartsWith("ticket-machine-") && id.EndsWith("screen"))
                { renderer.sharedMaterial = lcd; surface = true; }
                else if (id.StartsWith("ticket-machine-") && id.EndsWith("header"))
                { renderer.sharedMaterial = ticketHeader; surface = true; }
                else if ((id.StartsWith("fare-gate-") && !id.EndsWith("reader")) || id.StartsWith("fare-end-post") || (id.StartsWith("ticket-machine-") && id.EndsWith("body")))
                { renderer.sharedMaterial = stainless; surface = true; }
                else if (entity.roof) { renderer.sharedMaterial = ceiling; surface = true; }
                else if (entity.kind == "floor" || entity.kind == "ramp") { renderer.sharedMaterial = floor; surface = true; }
                else if (id.EndsWith("capital")) { renderer.sharedMaterial = trim; surface = true; }
                else if (id.Contains("dado") || id.EndsWith("plinth")) { renderer.sharedMaterial = dado; surface = true; }
                else if (texture.Contains("ivory")) { renderer.sharedMaterial = tile; surface = true; }
                else if (id.StartsWith("rail-")) { renderer.sharedMaterial = brightSteel; surface = true; }
                else if (id.Contains("catwalk") || id.Contains("glass-rail") || id.Contains("glass-door-post") || id.Contains("light-backing"))
                { renderer.sharedMaterial = steel; surface = true; }
                else if (id.StartsWith("platform-safety")) { renderer.sharedMaterial = yellow; surface = true; }
                else if (renderer.sharedMaterial.mainTexture == null && renderer.sharedMaterial.renderQueue < 3000
                    && renderer.sharedMaterial.GetColor("_EmissionColor").maxColorComponent < .01f) surface = true;
                if (surface && entity.kind != "ramp" && renderer.sharedMaterial.renderQueue < 3000)
                {
                    // Keep the authored transform and BoxCollider exactly where they were.
                    Vector3 size = entity.transform.localScale;
                    var mesh = BevelBox(size, true, entity.roof ? 3 : 2);
                    AssetDatabase.CreateAsset(mesh, folder + "/bevel-" + serial++ + ".asset");
                    filter.sharedMesh = mesh;
                }
                else if (entity.kind == "ramp")
                {
                    var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                    mesh.uv = mesh.vertices.Select(v => new Vector2(v.x, v.z) / 2).ToArray();
                    mesh.RecalculateTangents();
                    AssetDatabase.CreateAsset(mesh, folder + "/ramp-" + serial++ + ".asset");
                    filter.sharedMesh = mesh;
                }
            }

            var train = root.GetComponentInChildren<StrikeMapTrain>();
            foreach (float x in new[] { -9f, 9f })
                Prop("subway-car", train.transform, new Vector3(x, -.99f, 0), 0, new Vector3(17.8f, 3.19f, 2.9f));
            foreach (float x in new[] { -20f, 20f })
            {
                float placedX = x > 0 ? 26 : x; // Keep the east bench clear of the ticket machines.
                Prop("station-bench", root.transform, new Vector3(placedX, 0, 15.25f), 180, new Vector3(2.2f, .78f, .71f));
                FitOriginalCollider(entities.Single(e => e.sourceId == "bench-" + x), new Vector3(placedX, .39f, 15.25f), new Vector3(2.2f, .78f, .71f));
            }
            Prop("drinks-vending", root.transform, new Vector3(-6.2f, 0, -10.8f), 0, new Vector3(.95f, 1.95f, .8f));
            FitOriginalCollider(entities.Single(e => e.sourceId == "vending-case"), new Vector3(-6.2f, .975f, -10.8f), new Vector3(.95f, 1.95f, .8f));
            foreach (var position in new[] { new Vector3(-17.5f,0,15.35f), new Vector3(28,0,15.35f), new Vector3(-4.8f,0,-11.4f), new Vector3(9,0,15.35f) })
            {
                var prop = Prop("station-bin", root.transform, position, 180, new Vector3(.51f,.95f,.51f));
                var collider = prop.AddComponent<BoxCollider>(); collider.center = Vector3.up * .475f; collider.size = new Vector3(.51f,.95f,.51f);
            }
            Architecture(root, steel, brightSteel, yellow);
            Equipment(root, entities, stainless, steel, brightSteel);
            StationSigns(root, entities);
            PickupCases(root);
            FlushDetails(root);
            root.name = "Lower Bay / 2026";
        }

        private static void ConfigureImports()
        {
            foreach (string full in Directory.GetFiles(Assets, "*", SearchOption.AllDirectories))
            {
                string path = full.Replace('\\','/');
                if (path.EndsWith(".png") || path.EndsWith(".jpg"))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    bool normal = Path.GetFileNameWithoutExtension(path).ToLowerInvariant().Contains("normal");
                    bool color = path.EndsWith("basecolor.png") || path.EndsWith("Color.jpg");
                    if (importer.textureType == (normal ? TextureImporterType.NormalMap : TextureImporterType.Default)
                        && importer.sRGBTexture == color && importer.maxTextureSize == 4096 && importer.mipmapEnabled
                        && importer.anisoLevel == 8 && importer.textureCompression == TextureImporterCompression.CompressedHQ && !importer.isReadable) continue;
                    importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    importer.sRGBTexture = color; importer.maxTextureSize = 4096;
                    importer.mipmapEnabled = true; importer.anisoLevel = 8;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.isReadable = false; importer.SaveAndReimport();
                }
                else if (path.EndsWith(".fbx"))
                {
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    if (importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importAnimation
                        && !importer.isReadable && importer.importNormals == ModelImporterNormals.Import
                        && importer.importTangents == ModelImporterTangents.CalculateMikk && importer.meshCompression == ModelImporterMeshCompression.Off) continue;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.importAnimation = false; importer.isReadable = false;
                    importer.importNormals = ModelImporterNormals.Import;
                    importer.importTangents = ModelImporterTangents.CalculateMikk;
                    importer.meshCompression = ModelImporterMeshCompression.Off;
                    importer.SaveAndReimport();
                }
            }
        }

        private static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var color); return color; }
        private static Texture2D Texture(string path)
        {
            var result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (result == null) throw new FileNotFoundException("Required native 4K texture missing",path);
            if (result.width != 4096 || result.height != 4096) throw new InvalidDataException("Texture was resized below 4K: " + path);
            return result;
        }
        private static Material Surface(string name, string set, string tint, float roughness, float weather, float wetness = 0)
        {
            var material = new Material(Shader.Find("LowerBay/PBR 2026")) { name = name, color = Hex(tint) };
            string path = Assets + "/Surfaces/" + set + "/";
            material.mainTexture = Texture(path + "Color.jpg");
            material.SetTexture("_BumpMap", Texture(path + "NormalGL.jpg"));
            material.SetTexture("_SurfaceMap", Texture(path + "Roughness.jpg"));
            material.SetTexture("_OcclusionMap", Texture(path + "AmbientOcclusion.jpg"));
            material.SetFloat("_RoughnessScale", roughness); material.SetFloat("_Weather", weather); material.SetFloat("_Wetness", wetness);
            AssetDatabase.CreateAsset(material, folder + "/" + name + ".mat");
            return material;
        }
        private static Material Solid(string name, string color, float metal, float roughness)
        {
            var material = new Material(Shader.Find("Standard")) { name = name, color = Hex(color) };
            material.SetFloat("_Metallic",metal); material.SetFloat("_Glossiness",1-roughness);
            AssetDatabase.CreateAsset(material, folder + "/" + name + ".mat");
            return material;
        }
        private static Material PropMaterial(string id)
        {
            if (materials.TryGetValue(id,out var material)) return material;
            material = new Material(Shader.Find("LowerBay/PBR 2026")) { name = id + " original 4K PBR" };
            string path = Assets + "/Props/" + id + "/";
            material.mainTexture = Texture(path + "basecolor.png");
            material.SetTexture("_BumpMap",Texture(path + "normal.png"));
            material.SetTexture("_SurfaceMap",Texture(path + "metalrough.png"));
            material.SetFloat("_Packed",1); material.SetFloat("_Metallic",1);
            material.SetFloat("_Cull",0); // All four supplied glTF materials declare doubleSided=true.
            if (id == "subway-car") { material.SetFloat("_RoughnessScale",1.45f); material.SetFloat("_BumpScale",.6f); }
            AssetDatabase.CreateAsset(material, folder + "/" + id + ".mat"); materials.Add(id,material);
            return material;
        }
        private static GameObject Prop(string id, Transform parent, Vector3 position, float yaw, Vector3 dimensions)
        {
            var host = new GameObject(id); host.transform.SetParent(parent,false);
            host.transform.localPosition = position; host.transform.localRotation = Quaternion.Euler(0,yaw,0);
            var levels = new LOD[2];
            for (int index=0;index<2;index++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Assets + "/Props/" + id + "/LOD" + index + ".fbx");
                if (prefab == null) throw new FileNotFoundException("Prepared prop missing: " + id);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var calibration=new GameObject("Metre-space LOD"+index);calibration.transform.SetParent(host.transform,false);
                instance.transform.SetParent(calibration.transform,false);
                var renderers = instance.GetComponentsInChildren<Renderer>();
                // Imported FBX unit conversion is measured, not assumed.
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                var localSize = host.transform.InverseTransformVector(bounds.size);
                localSize = new Vector3(Mathf.Abs(localSize.x),Mathf.Abs(localSize.y),Mathf.Abs(localSize.z));
                // Scale in the host axes; an FBX root can carry a 90-degree axis conversion.
                calibration.transform.localScale = new Vector3(dimensions.x/localSize.x,dimensions.y/localSize.y,dimensions.z/localSize.z);
                var fitted = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) fitted.Encapsulate(renderer.bounds);
                var fittedSize = host.transform.InverseTransformVector(fitted.size);
                fittedSize = new Vector3(Mathf.Abs(fittedSize.x),Mathf.Abs(fittedSize.y),Mathf.Abs(fittedSize.z));
                if ((fittedSize-dimensions).magnitude > .03f) throw new InvalidOperationException("FBX instance scale mismatch: " + id + " " + fittedSize);
                Vector3 localCenter=host.transform.InverseTransformPoint(fitted.center);
                calibration.transform.localPosition-=new Vector3(localCenter.x,localCenter.y-fittedSize.y*.5f,localCenter.z);
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterial = PropMaterial(id); renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }
                levels[index] = new LOD(index == 0 ? .12f : .004f,renderers);
            }
            var group = host.AddComponent<LODGroup>(); group.SetLODs(levels); group.RecalculateBounds();
            return host;
        }
        private static void FitOriginalCollider(StrikeMapEntity entity, Vector3 center, Vector3 size)
        {
            var box = entity.GetComponent<BoxCollider>();
            box.center = entity.transform.InverseTransformPoint(center);
            Vector3 scale = entity.transform.lossyScale;
            box.size = new Vector3(size.x/scale.x,size.y/scale.y,size.z/scale.z);
        }

        private static Mesh BevelBox(Vector3 size, bool normalized, float metresPerRepeat = 2)
        {
            float radius = Mathf.Min(.028f,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.24f);
            Vector3 half = size*.5f, inner = half - Vector3.one*radius;
            var vertices=new List<Vector3>(); var normals=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
            for (int axis=0;axis<3;axis++) for (int sign=-1;sign<=1;sign+=2)
            {
                int u=(axis+1)%3,v=(axis+2)%3,start=vertices.Count;
                float[] us={-half[u],-inner[u],inner[u],half[u]}, vs={-half[v],-inner[v],inner[v],half[v]};
                for(int j=0;j<4;j++) for(int i=0;i<4;i++)
                {
                    Vector3 p=Vector3.zero; p[axis]=half[axis]*sign;p[u]=us[i];p[v]=vs[j];
                    var closest=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));
                    Vector3 normal=(p-closest).normalized, point=closest+normal*radius;
                    uv.Add((axis == 1 ? new Vector2(p.x,p.z) : new Vector2(p[axis==0?2:0],p.y))/metresPerRepeat);
                    vertices.Add(normalized ? new Vector3(point.x/size.x,point.y/size.y,point.z/size.z) : point);
                    normals.Add(normalized ? Vector3.Scale(normal,size).normalized : normal);
                }
                for(int j=0;j<3;j++) for(int i=0;i<3;i++)
                {
                    int a=start+j*4+i,b=a+1,c=a+4,d=c+1;
                    triangles.AddRange(sign>0 ? new[]{a,b,d,a,d,c} : new[]{a,d,b,a,c,d});
                }
            }
            var mesh=new Mesh { name="Chamfered architectural module" };
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
            mesh.RecalculateTangents();mesh.RecalculateBounds(); return mesh;
        }
        private static void Detail(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            string key=material.name+":"+Mathf.FloorToInt(position.x/16);
            if(!details.TryGetValue(key,out var batch)) { batch=new DetailBatch{material=material}; details.Add(key,batch); }
            batch.meshes.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(position,rotation,scale)});
        }
        private static void Box(Vector3 p,Vector3 size,Material material) { Detail(BevelBox(size,false),p,Quaternion.identity,Vector3.one,material); }
        private static void Pipe(Vector3 from,Vector3 to,float radius,Material material)
        {
            Vector3 delta=to-from;
            var temporary=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Mesh mesh=temporary.GetComponent<MeshFilter>().sharedMesh;
            Detail(mesh,(from+to)*.5f,Quaternion.FromToRotation(Vector3.up,delta),new Vector3(radius*2,delta.magnitude*.5f,radius*2),material);
            UnityEngine.Object.DestroyImmediate(temporary);
        }
        private static void Architecture(GameObject root,Material steel,Material rail,Material yellow)
        {
            for(float x=-34;x<=34;x+=4)
            {
                // Three plates form an I-section; fittings meet the column grid.
                if (Mathf.Abs(x) <= 30) // Upper-room portals occupy the end bays.
                {
                    Box(new Vector3(x,5.22f,0),new Vector3(.13f,.33f,10.15f),steel);
                    foreach(float y in new[]{5.035f,5.405f}) Box(new Vector3(x,y,0),new Vector3(.38f,.055f,10.15f),steel);
                }
                foreach(float z in new[]{-4.9f,4.9f})
                {
                    Box(new Vector3(x,4.92f,z),new Vector3(.52f,.07f,.52f),steel);
                    foreach(float dx in new[]{-.19f,.19f}) foreach(float dz in new[]{-.19f,.19f})
                        Pipe(new Vector3(x+dx,4.85f,z+dz),new Vector3(x+dx,4.96f,z+dz),.037f,rail);
                }
            }
            foreach(float side in new[]{-1f,1f})
            {
                for(int line=0;line<3;line++) Pipe(new Vector3(-38,4.62f+line*.13f,side*5.56f),new Vector3(38,4.62f+line*.13f,side*5.56f),.032f,steel);
                for(float x=-34;x<=34;x+=4)
                    Box(new Vector3(x,4.8f,side*5.49f),new Vector3(.06f,.64f,.25f),rail);
                // Safety strip and drainage are separate real geometry, with clear walk space.
                for(float x=-37.8f;x<38;x+=.16f) for(int row=0;row<3;row++)
                {
                    float z=side*(2.08f+row*.1f);
                    Pipe(new Vector3(x,.028f,z),new Vector3(x,.036f,z),.022f,yellow);
                }
                for(float x=-37.5f;x<38;x+=1.25f)
                {
                    Box(new Vector3(x,.007f,side*5.63f),new Vector3(.8f,.012f,.21f),steel);
                    for(int slit=0;slit<9;slit++) Box(new Vector3(x-.35f+slit*.085f,.016f,side*5.63f),new Vector3(.02f,.008f,.18f),rail);
                }
            }
            // Fine steel infill within the existing upper walkway barriers.
            foreach(float side in new[]{-1f,1f}) foreach(float z in new[]{-1.3f,1.3f})
            {
                for(float distance=18.4f;distance<29.8f;distance+=.32f)
                    Pipe(new Vector3(side*distance,3.08f,z),new Vector3(side*distance,3.87f,z),.014f,steel);
            }
            foreach(float side in new[]{-1f,1f})
            {
                foreach(float z in new[]{-1.24f,1.24f})
                {
                    Box(new Vector3(side*24,2.85f,z),new Vector3(12,.3f,.1f),steel);
                    foreach(float x in new[]{22f,30f}) Pipe(new Vector3(side*x,3.04f,z),new Vector3(side*x,5.2f,z),.026f,steel);
                }
                for(float x=18.05f;x<30;x+=.12f) Box(new Vector3(side*x,2.985f,0),new Vector3(.022f,.035f,2.48f),steel);
                for(float z=-1.2f;z<=1.21f;z+=.3f) Box(new Vector3(side*24,2.965f,z),new Vector3(11.9f,.018f,.018f),steel);
            }
        }
        private static void Equipment(GameObject root, StrikeMapEntity[] entities, Material stainless, Material steel, Material rail)
        {
            Material recess=Solid("equipment rubber seals","#141d1c",0,.85f);
            Material indicator=Solid("reader status lamps","#4e9678",.05f,.3f);
            indicator.EnableKeyword("_EMISSION");indicator.SetColor("_EmissionColor",Hex("#2a785a")*.55f);
            foreach(var gate in entities.Where(e=>e.sourceId.StartsWith("fare-gate-") && e.sourceId.EndsWith("body")))
            {
                float x=gate.transform.position.x;
                Box(new Vector3(x,.54f,11.639f),new Vector3(.265f,.76f,.018f),steel);
                foreach(float y in new[]{.18f,.88f}) foreach(float dx in new[]{-.105f,.105f})
                    Pipe(new Vector3(x+dx,y,11.615f),new Vector3(x+dx,y,11.65f),.012f,rail);
                Box(new Vector3(x,1.157f,11.86f),new Vector3(.16f,.014f,.12f),recess);
                Box(new Vector3(x,1.166f,11.83f),new Vector3(.115f,.006f,.012f),indicator);
                // Short tripod arms leave over a metre of clear walking width.
                var hub=new Vector3(x+.19f,.73f,12.5f);
                Pipe(hub-Vector3.right*.05f,hub+Vector3.right*.08f,.11f,stainless);
                foreach(float angle in new[]{0f,120f,240f})
                {
                    float a=angle*Mathf.Deg2Rad;
                    Pipe(hub,hub+new Vector3(.36f,Mathf.Sin(a)*.24f,Mathf.Cos(a)*.24f),.021f,rail);
                }
            }
            foreach(var machine in entities.Where(e=>e.sourceId.StartsWith("ticket-machine-") && e.sourceId.EndsWith("body")))
            {
                float x=machine.transform.position.x;
                Box(new Vector3(x,.39f,14.82f),new Vector3(.77f,.47f,.018f),steel);
                for(int line=0;line<7;line++) Box(new Vector3(x,.25f+line*.045f,14.805f),new Vector3(.59f,.011f,.018f),recess);
                for(int row=0;row<3;row++) for(int col=0;col<3;col++)
                    Box(new Vector3(x-.095f+col*.095f,.92f+row*.075f,14.787f),new Vector3(.063f,.049f,.035f),rail);
                Box(new Vector3(x+.27f,1.05f,14.779f),new Vector3(.045f,.21f,.016f),recess);
                Label(root,"TICKETS",new Vector3(x,2.09f,14.805f),0,.6f,.09f);
                Label(root,"SELECT FARE",new Vector3(x,1.53f,14.812f),0,.52f,.065f);
            }
        }
        private static void FlushDetails(GameObject root)
        {
            var holder=new GameObject("2026 architectural details"); holder.transform.SetParent(root.transform,false);
            foreach(var batch in details.Values)
            {
                var mesh=new Mesh{name="Station fittings",indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(batch.meshes.ToArray(),true,true);mesh.RecalculateTangents();
                bool tinyFittings = batch.material.name != "galvanized structural steel";
                if (!tinyFittings) Unwrapping.GenerateSecondaryUVSet(mesh);
                AssetDatabase.CreateAsset(mesh,folder+"/fittings-"+serial++ +".asset");
                var host=new GameObject("Fittings / "+batch.material.name);host.transform.SetParent(holder.transform,false);
                host.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=host.AddComponent<MeshRenderer>();renderer.sharedMaterial=batch.material;
                if (tinyFittings) { renderer.receiveGI=ReceiveGI.LightProbes; renderer.lightProbeUsage=LightProbeUsage.BlendProbes; }
                GameObjectUtility.SetStaticEditorFlags(host,StaticEditorFlags.ContributeGI|StaticEditorFlags.OccludeeStatic);
            }
        }
        private static void StationSigns(GameObject root,StrikeMapEntity[] entities)
        {
            Material enamel=Solid("maroon porcelain station signs","#562931",.15f,.3f);
            foreach(var entity in entities.Where(e=>e.sourceId.StartsWith("sign-")))
            {
                entity.GetComponent<Renderer>().enabled=false;entity.GetComponent<Renderer>().forceRenderingOff=true;
                Vector3 p=entity.transform.position;
                Box(p,new Vector3(2.7f,.65f,.08f),enamel);
                float facing=p.z>0?0:180;
                Label(root,"LOWER BAY",p+new Vector3(0,.085f,p.z>0?-.051f:.051f),facing,2.35f,.26f);
                Label(root,"GIDEONS TOWER   /   FORT WINTER",p+new Vector3(0,-.18f,p.z>0?-.052f:.052f),facing,2.45f,.085f);
            }
        }
        private static void Label(GameObject root,string text,Vector3 p,float yaw,float width,float height)
        {
            var host=new GameObject("Station lettering");host.transform.SetParent(root.transform,false);
            host.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));
            var label=host.AddComponent<TextMesh>();label.text=text;label.anchor=TextAnchor.MiddleCenter;
            label.alignment=TextAlignment.Center;label.fontSize=96;label.characterSize=1;
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.color=Hex("#e8e0ca");
            var renderer=host.GetComponent<MeshRenderer>();renderer.sharedMaterial=label.font.material;
            Vector3 size=renderer.bounds.size;
            host.transform.localScale=Vector3.one*Mathf.Min(width/Mathf.Max(.001f,size.x),height/Mathf.Max(.001f,size.y));
            var depth=host.AddComponent<LowerBayStationLabel>();depth.shader=Shader.Find("LowerBay/World Text");depth.Refresh();
        }

        private static void PickupCases(GameObject root)
        {
            Material shell=Solid("pickup case powder coated steel","#394843",.5f,.48f);
            Material latch=Solid("pickup case steel latches","#8b9694",.8f,.32f);
            Physics.SyncTransforms();
            foreach(var marker in root.GetComponentsInChildren<StrikeMapMarker>().Where(m=>m.kind!="spawn"))
            {
                var visual=marker.transform.Find("Visual");
                if(visual==null || visual.GetComponent<MeshFilter>()==null) continue;
                Vector3 size=marker.kind=="sniper"?new Vector3(1.08f,.22f,.35f):new Vector3(.52f,.28f,.35f);
                float ground=-.8f;
                if(Physics.Raycast(marker.transform.position,Vector3.down,out var hit,2,~0,QueryTriggerInteraction.Ignore)) ground=hit.point.y-marker.transform.position.y;
                visual.localScale=Vector3.one;visual.localPosition=new Vector3(0,ground+size.y*.5f+.025f,0);
                Mesh mesh=BevelBox(size,false);AssetDatabase.CreateAsset(mesh,folder+"/pickup-case-"+serial++ +".asset");
                visual.GetComponent<MeshFilter>().sharedMesh=mesh;visual.GetComponent<Renderer>().sharedMaterial=shell;
                var lid=new GameObject("Colored case lid");lid.transform.SetParent(visual,false);lid.transform.localPosition=Vector3.up*(size.y*.5f-.015f);
                var lidMesh=BevelBox(new Vector3(size.x*.93f,.035f,size.z*.94f),false);
                AssetDatabase.CreateAsset(lidMesh,folder+"/pickup-lid-"+serial++ +".asset");
                lid.AddComponent<MeshFilter>().sharedMesh=lidMesh;
                Color color=marker.kind=="health"?Hex("#803a36"):marker.kind=="ammo"?Hex("#9e8950"):marker.kind=="sniper"?Hex("#3e4248"):Hex("#537480");
                var paint=new Material(shell){name="Pickup identification",color=color};paint.SetFloat("_Metallic",.15f);
                AssetDatabase.CreateAsset(paint,folder+"/pickup-paint-"+serial++ +".mat");lid.AddComponent<MeshRenderer>().sharedMaterial=paint;
                foreach(float side in new[]{-1f,1f})
                {
                    var clip=new GameObject("Latch");clip.transform.SetParent(visual,false);clip.transform.localPosition=new Vector3(side*size.x*.32f,size.y*.12f,-size.z*.5f-.007f);
                    var clipMesh=BevelBox(new Vector3(.045f,.095f,.025f),false);AssetDatabase.CreateAsset(clipMesh,folder+"/pickup-latch-"+serial++ +".asset");
                    clip.AddComponent<MeshFilter>().sharedMesh=clipMesh;clip.AddComponent<MeshRenderer>().sharedMaterial=latch;
                }
            }
        }

        public static void ConfigureLighting(GameObject root)
        {
            PlayerSettings.colorSpace=ColorSpace.Linear;
            QualitySettings.SetQualityLevel(QualitySettings.names.Length-1,true);
            QualitySettings.globalTextureMipmapLimit=0;
            QualitySettings.antiAliasing=4;QualitySettings.anisotropicFiltering=AnisotropicFiltering.ForceEnable;
            QualitySettings.shadowDistance=70;QualitySettings.shadows=ShadowQuality.All;
            RenderSettings.ambientSkyColor=new Color(.2f,.25f,.3f);
            RenderSettings.ambientEquatorColor=new Color(.12f,.16f,.18f);
            RenderSettings.ambientGroundColor=new Color(.075f,.085f,.075f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;
            RenderSettings.fogDensity=.008f;RenderSettings.fogColor=new Color(.085f,.11f,.12f);
            foreach(var light in root.GetComponentsInChildren<Light>())
            {
                if(light.type==LightType.Directional){light.intensity=.12f;continue;}
                light.color=light.transform.position.z>6?new Color(.8f,.9f,1):new Color(.96f,.91f,.8f);
                light.intensity*=1.12f;light.range*=.95f;
            }
            // A local probe cage keeps furniture from interpolating light through
            // the alcove wall or the concourse's tiled back wall.
            var probeGroup=root.GetComponentInChildren<LightProbeGroup>();
            var lightProbes=probeGroup.probePositions.ToList();
            foreach(float x in new[]{-6.4f,-4.5f,0f,4.5f,6.4f}) foreach(float z in new[]{-11.45f,-9.6f})
                foreach(float y in new[]{.3f,1.2f,2.5f}) lightProbes.Add(new Vector3(x,y,z));
            foreach(float x in new[]{-21.5f,-18.5f,-16.5f,8f,10f,24.5f,27.5f,29f}) foreach(float z in new[]{14.2f,15.65f})
                foreach(float y in new[]{.3f,1.2f,2.6f}) lightProbes.Add(new Vector3(x,y,z));
            probeGroup.probePositions=lightProbes.ToArray();
            foreach(var pair in new[]{new[]{new Vector3(-6.2f,2.9f,-9.3f),new Vector3(-6.2f,1,-10.8f)},
                new[]{new Vector3(-20,3.4f,13.5f),new Vector3(-20,.5f,15.25f)},
                new[]{new Vector3(26,3.4f,13.5f),new Vector3(26,.5f,15.25f)}})
            {
                var host=new GameObject("Furniture fixture");host.transform.SetParent(root.transform,false);
                host.transform.position=pair[0];host.transform.LookAt(pair[1]);
                var light=host.AddComponent<Light>();light.type=LightType.Spot;light.range=6;light.spotAngle=95;
                light.intensity=2.2f;light.color=new Color(.95f,.94f,.87f);light.shadows=LightShadows.Soft;
                light.shadowBias=.025f;light.shadowNormalBias=.05f;light.lightmapBakeType=LightmapBakeType.Realtime;
            }
            var camera=root.GetComponentsInChildren<Camera>().Single(c=>c.gameObject.activeInHierarchy);
            camera.fieldOfView=68;camera.allowHDR=true;camera.allowMSAA=true;
            var film=camera.gameObject.AddComponent<LowerBayFilm>();
            film.shader=Shader.Find("Hidden/LowerBay/Film");film.exposure=1.0f;
            foreach(float x in new[]{-27f,-9f,9f,27f})
            {
                var host=new GameObject("Station reflection probe");host.transform.SetParent(root.transform,false);
                host.transform.position=new Vector3(x,2.1f,1);
                var probe=host.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Baked;
                probe.resolution=256;probe.hdr=true;probe.boxProjection=true;
                probe.size=new Vector3(25,8,25);probe.blendDistance=4;probe.nearClipPlane=.12f;probe.farClipPlane=100;
                probe.clearFlags=ReflectionProbeClearFlags.SolidColor;probe.backgroundColor=RenderSettings.fogColor;
            }
            foreach(var point in new[]{new Vector3(-3,1.7f,-10.5f),new Vector3(-20,1.8f,13),new Vector3(24,1.8f,13)})
            {
                var host=new GameObject("Room reflection probe");host.transform.SetParent(root.transform,false);host.transform.position=point;
                var probe=host.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Baked;probe.resolution=256;probe.hdr=true;
                probe.boxProjection=true;probe.size=point.z<0?new Vector3(15,3.5f,3.5f):new Vector3(24,4,8);
                probe.blendDistance=.6f;probe.importance=2;probe.nearClipPlane=.1f;probe.farClipPlane=70;
                probe.clearFlags=ReflectionProbeClearFlags.SolidColor;probe.backgroundColor=RenderSettings.fogColor;
            }
        }
        public static void BakeReflections(GameObject root)
        {
            int index=0;
            foreach(var probe in root.GetComponentsInChildren<ReflectionProbe>())
            {
                string path=folder+"/reflection-"+index++ +".exr";
                if(!Lightmapping.BakeReflectionProbe(probe,path)) throw new InvalidOperationException("Reflection probe bake failed.");
            }
        }
    }
}
