using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using HotelTogether;

// Editor-only authoring. All generated assets are saved, so players need no generator.
public static class HotelLobbyArtDirection {
 const string Art="Assets/HotelTogether/Art/Lobby/";
 static Transform root;
 static Material cream, teal, brass, glow, brick, wood;
 [MenuItem("HotelTogether/Apply warm colourful lobby")]
 public static void Apply(){
  if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before authoring the lobby.");
  Directory.CreateDirectory(Art);AssetDatabase.Refresh();
  var previous=GameObject.Find("Lobby colour and lighting");if(previous)Undo.DestroyObjectImmediate(previous);
  var go=new GameObject("Lobby colour and lighting");Undo.RegisterCreatedObjectUndo(go,"Warm colourful lobby");root=go.transform;
  cream=Mat("Honey cream",new Color(.94f,.83f,.65f),.18f);
  cream.EnableKeyword("_EMISSION");cream.SetColor("_EmissionColor",new Color(.94f,.68f,.36f)*.18f);
  teal=Mat("Deep teal panels",new Color(.13f,.34f,.34f),.22f);
  brass=Mat("Aged golden brass",new Color(.66f,.43f,.16f),.5f,.55f);
  brick=Mat("Terracotta accents",new Color(.66f,.24f,.13f),.16f);
  wood=Mat("Honey wood",new Color(.58f,.38f,.21f),.32f);
  glow=Mat("Warm diffusers",new Color(1,.86f,.58f),.25f);glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(1,.58f,.18f)*4.6f);EditorUtility.SetDirty(glow);
  var panelTexture=Texture("TealPanelPattern",512,(x,y)=>{
   int px=x%128;bool edge=(px>=9&&px<=12)||(px>=115&&px<=118)||(y>=20&&y<=23)||(y>=488&&y<=491);
   return edge?new Color(.08f,.23f,.24f):new Color(.20f,.43f,.42f);});
  teal.SetTexture("_BaseMap",panelTexture);
  FinishWalls();FinishFurniture();
  var rug=Mat("Teal mustard terracotta rug",Color.white,.02f);rug.SetTexture("_BaseMap",Texture("LobbyRugPattern",1024,(x,y)=>{
   float u=x/1023f,v=y/1023f;float edge=Mathf.Min(Mathf.Min(u,1-u),Mathf.Min(v,1-v));
   var dark=new Color(.14f,.31f,.32f);var gold=new Color(.66f,.47f,.25f);var red=new Color(.52f,.29f,.21f);
   if(edge<.018f)return dark;if(edge<.03f)return gold;if(edge<.045f)return dark;
   float a=u*5+v*5,b=u*5-v*5;float da=Mathf.Abs(a-Mathf.Round(a)),db=Mathf.Abs(b-Mathf.Round(b));
   var c=(da<.035f||db<.035f)?gold:(((Mathf.FloorToInt(a)+Mathf.FloorToInt(b))&1)==0?dark:red);
   float weave=1f+(((x+y)%4==0)?-.025f:.015f);return c*weave; }));
  Box("Main seating patterned rug",new Vector3(6.3f,.014f,6.4f),new Vector3(5.5f,.009f,2.35f),rug);
  Box("Waiting corner patterned rug",new Vector3(4.1f,.014f,2.5f),new Vector3(4.5f,.009f,-3.2f),rug);
  // Match the shallow existing ceiling coffers, without lowering the ceiling.
  foreach(float x in new[]{-4f,4f})foreach(float z in new[]{-3.4f,3.4f}){
   Frame(x,z,7.5f,5.8f,7.78f,brass,.09f);
   Frame(x,z,7.28f,5.58f,7.69f,glow,.12f);
   var light=LightAt("Coffer warm wash",new Vector3(x,7.45f,z),LightType.Spot,34f,11f);light.transform.rotation=Quaternion.Euler(90,0,0);light.spotAngle=105;light.innerSpotAngle=70;
  }
  // Balcony soffit and handrail receive subtle gold detail and continuous warm strips.
  Box("Balcony rear warm strip",new Vector3(17.5f,.035f,.055f),new Vector3(0,4.43f,-6.86f),glow);
  Box("Balcony side warm strip",new Vector3(.055f,.035f,13.4f),new Vector3(8.82f,4.43f,0),glow);
  Box("Balcony rear golden edge",new Vector3(17.65f,.07f,.065f),new Vector3(0,4.53f,-6.90f),brass);
  Box("Balcony side golden edge",new Vector3(.065f,.07f,13.55f),new Vector3(8.90f,4.53f,0),brass);
  Poster("Seaside poster",new Vector3(5,2.45f,-10.76f),0,0);
  Poster("Hotel poster",new Vector3(-7.1f,2.45f,-10.76f),0,1);
  Poster("Tropical hallway poster",new Vector3(-2.6f,2.45f,-10.76f),0,2);
  Poster("Sunset poster",new Vector3(-8.76f,2.45f,6),90,3);
  foreach(float x in new[]{-8.4f,-5.4f,-1.7f,1.6f,8.4f})Sconce(new Vector3(x,2.72f,-10.75f),0);
  foreach(float z in new[]{-6.82f,5.15f})Sconce(new Vector3(-8.75f,2.72f,z),90);
  // Existing palm assets fill balcony corners; decorative clones have no gameplay or collision.
  BalconyPalm("Balcony palm west",new Vector3(7.6f,4.54f,-8.65f));
  BalconyPalm("Balcony palm east",new Vector3(-7.6f,4.54f,-8.65f));
  Lighting();AmbientOcclusion();
  LocalAtmosphere();
  foreach(var r in root.GetComponentsInChildren<Renderer>())GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.BatchingStatic);
  Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  Debug.Log("LOBBY_ART_APPLIED: warm palette, two rugs, four posters, seven sconces, four ceiling washes.");
 }
 static Material Mat(string name,Color color,float smooth,float metal=0){
  string path=Art+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
  Undo.RecordObject(m,"Lobby material finish");m.name=name;m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metal);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
 }
 static Texture2D Texture(string name,int size,Func<int,int,Color> pixel){
  string path=Art+name+".png";var t=new Texture2D(size,size,TextureFormat.RGB24,false);var colors=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++)colors[y*size+x]=pixel(x,y);t.SetPixels(colors);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
  var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=true;importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=4;importer.mipmapEnabled=true;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
 }
 static GameObject Box(string name,Vector3 size,Vector3 position,Material material,Transform parent=null){
  var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent?parent:root,false);g.transform.localPosition=position;g.transform.localScale=size;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=material;return g;
 }
 static void Frame(float x,float z,float w,float d,float y,Material m,float thickness){
  Box("Coffer long edge",new Vector3(w,thickness,thickness),new Vector3(x,y,z-d/2),m);Box("Coffer long edge",new Vector3(w,thickness,thickness),new Vector3(x,y,z+d/2),m);
  Box("Coffer short edge",new Vector3(thickness,thickness,d),new Vector3(x-w/2,y,z),m);Box("Coffer short edge",new Vector3(thickness,thickness,d),new Vector3(x+w/2,y,z),m);
 }
 static Light LightAt(string name,Vector3 position,LightType type,float intensity,float range){
  var l=new GameObject(name).AddComponent<Light>();l.transform.SetParent(root,false);l.transform.position=position;l.type=type;l.color=new Color(1,.74f,.42f);l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;return l;
 }
 static void Sconce(Vector3 position,float angle){
  var group=new GameObject("Brass warm wall sconce").transform;group.SetParent(root,false);group.SetPositionAndRotation(position,Quaternion.Euler(0,angle,0));
  Box("Wall plate",new Vector3(.16f,.55f,.045f),Vector3.zero,brass,group);
  Box("Lower bracket",new Vector3(.24f,.045f,.19f),new Vector3(0,-.18f,.07f),brass,group);
  Box("Milk glass lamp",new Vector3(.22f,.32f,.13f),new Vector3(0,0,.11f),glow,group);
  Box("Upper golden cap",new Vector3(.24f,.035f,.15f),new Vector3(0,.18f,.11f),brass,group);
  var l=LightAt("Sconce warm pool",position+group.forward*.32f,LightType.Point,1.45f,3.6f);l.transform.SetParent(group,true);
 }
 static void Poster(string name,Vector3 position,float angle,int quadrant){
  var g=new GameObject(name).transform;g.SetParent(root,false);g.SetPositionAndRotation(position,Quaternion.Euler(0,angle,0));
  var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"TravelPosterAtlas.png");var m=Mat("Travel poster "+quadrant,Color.white,.08f);m.SetTexture("_BaseMap",atlas);m.SetTextureScale("_BaseMap",new Vector2(.5f,.5f));m.SetTextureOffset("_BaseMap",new Vector2((quadrant%2)*.5f,quadrant<2?.5f:0));
  Box("Frame backing",new Vector3(1.34f,1.64f,.07f),Vector3.zero,wood,g);
  var face=GameObject.CreatePrimitive(PrimitiveType.Quad);face.name="Original travel illustration";face.transform.SetParent(g,false);face.transform.localPosition=new Vector3(0,0,.042f);face.transform.localRotation=Quaternion.Euler(0,180,0);face.transform.localScale=new Vector3(1.18f,1.48f,1);UnityEngine.Object.DestroyImmediate(face.GetComponent<Collider>());face.GetComponent<Renderer>().sharedMaterial=m;
  Box("Frame gold top",new Vector3(1.34f,.055f,.035f),new Vector3(0,.795f,.052f),brass,g);Box("Frame gold bottom",new Vector3(1.34f,.055f,.035f),new Vector3(0,-.795f,.052f),brass,g);
  Box("Frame gold left",new Vector3(.055f,1.54f,.035f),new Vector3(-.642f,0,.052f),brass,g);Box("Frame gold right",new Vector3(.055f,1.54f,.035f),new Vector3(.642f,0,.052f),brass,g);
 }
 static void BalconyPalm(string name,Vector3 foot){
  var original=GameObject.Find("Prop_0050");if(!original)return;var g=UnityEngine.Object.Instantiate(original,root);g.name=name;var item=g.GetComponent<HotelItem>();float height=item.dimensions.y*.62f;foreach(var c in g.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);foreach(var r in g.GetComponentsInChildren<Rigidbody>())UnityEngine.Object.DestroyImmediate(r);UnityEngine.Object.DestroyImmediate(item);g.transform.localScale=original.transform.localScale*.62f;g.transform.position=foot+Vector3.up*height/2;
 }
 static void FinishFurniture(){
  var fabric=Mat("Lobby woven teal",new Color(.12f,.40f,.44f),.10f);
  foreach(var item in UnityEngine.Object.FindObjectsByType<HotelItem>()){
   if(item.home.x<-9||item.home.x>9||item.home.z<-7||item.home.z>7)continue;
   foreach(var r in item.GetComponentsInChildren<Renderer>()){
    var ms=r.sharedMaterials;bool changed=false;
    for(int i=0;i<ms.Length;i++){
     var source=ms[i];if(!source)continue;Color color=source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.HasProperty("baseColorFactor")?source.GetColor("baseColorFactor"):Color.white;
     bool green=color.g>color.r*1.2f&&color.g>color.b*.85f&&color.g<.75f;
     bool brown=color.r>color.g*1.15f&&color.g>color.b*1.15f&&color.r>.2f&&color.r<.85f;
     if(item.kind=="seat"&&green){ms[i]=fabric;changed=true;}
     if((item.sourceModel.Contains("Table")||item.name=="Prop_0041"||item.name=="Prop_0042")&&brown){ms[i]=wood;changed=true;}
    }
    if(changed){Undo.RecordObject(r,"Lobby furniture palette");r.sharedMaterials=ms;EditorUtility.SetDirty(r);}
   }
   if(item.kind=="lamp"&&item.lamp){Undo.RecordObject(item.lamp,"Warm local lamp");item.lamp.color=new Color(1,.72f,.38f);item.lamp.intensity=2.8f;item.lamp.range=4;EditorUtility.SetDirty(item.lamp);}
  }
  var ceiling=GameObject.Find("Node-321");if(ceiling){var m=Mat("Warm ceiling bounce",new Color(.94f,.84f,.68f),.12f);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(.85f,.62f,.35f));Undo.RecordObject(ceiling.GetComponent<Renderer>(),"Honey cream ceiling");ceiling.GetComponent<Renderer>().sharedMaterial=m;}
  var floor=GameObject.Find("Node-86");if(floor){var r=floor.GetComponent<Renderer>();var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HotelTogether/Models/HotelScene.glb").GetComponentsInChildren<Renderer>(true).First(a=>a.name=="Node-86").sharedMaterial;var m=Mat("Warm satin lobby tiles",new Color(.94f,.89f,.79f),.36f);m.SetTexture("_BaseMap",original.mainTexture);Undo.RecordObject(r,"Satin lobby floor");r.sharedMaterial=m;r.receiveShadows=true;EditorUtility.SetDirty(r);}
 }
 struct Vertex{public Vector3 p,n;public Vector2 uv;public Vector4 tangent;public Color color;public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),tangent=Vector4.Lerp(a.tangent,b.tangent,t),color=Color.Lerp(a.color,b.color,t)};}
 static List<Vertex> Clip(List<Vertex> polygon,int axis,float limit,bool greater){
  var output=new List<Vertex>();if(polygon.Count==0)return output;var a=polygon[polygon.Count-1];float da=(greater?1:-1)*(a.p[axis]-limit);foreach(var b in polygon){float db=(greater?1:-1)*(b.p[axis]-limit);if((da>=0)!=(db>=0))output.Add(Vertex.Lerp(a,b,da/(da-db)));if(db>=0)output.Add(b);a=b;da=db;}return output;
 }
 static void FinishWalls(){
  var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HotelTogether/Models/HotelScene.glb");var sources=model.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("Node-")).GroupBy(r=>r.name).ToDictionary(g=>g.Key,g=>g.First());
  var bounds=new Bounds(new Vector3(0,2.2f,-1.95f),new Vector3(18.04f,4.4f,17.94f));
  foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>()){
   if(r.GetComponentInParent<HotelItem>()||!sources.TryGetValue(r.name,out var source))continue;var original=source.sharedMaterial;string texture=original&&original.mainTexture?original.mainTexture.name:"";
   if(texture!="image_24"&&texture!="image_28"&&texture!="image_25")continue;
   // Source imported geometry is read-only; make a saved two-material partition for lobby faces.
   var src=source.GetComponent<MeshFilter>().sharedMesh;var positions=src.vertices;var normals=src.normals;var uvs=src.uv;var tangents=src.tangents;var colors=src.colors;var indices=src.triangles;
   var outside=new List<Vertex>();var inside=new List<Vertex>();
   for(int i=0;i<indices.Length;i+=3){var poly=new List<Vertex>();for(int j=0;j<3;j++){int k=indices[i+j];poly.Add(new Vertex{p=r.transform.TransformPoint(positions[k]),n=normals.Length>k?r.transform.TransformDirection(normals[k]):Vector3.up,uv=uvs.Length>k?uvs[k]:Vector2.zero,tangent=tangents.Length>k?tangents[k]:new Vector4(1,0,0,1),color=colors.Length>k?colors[k]:Color.white});}
    foreach(var cut in new[]{(0,bounds.min.x,true),(0,bounds.max.x,false),(2,bounds.min.z,true),(2,bounds.max.z,false)}){var rejected=Clip(poly,cut.Item1,cut.Item2,!cut.Item3);Append(outside,rejected);poly=Clip(poly,cut.Item1,cut.Item2,cut.Item3);}Append(inside,poly);
   }
   if(inside.Count==0)continue;var all=outside.Concat(inside).ToArray();var mesh=new Mesh{name="Lobby finish partition "+r.name};mesh.SetVertices(all.Select(v=>r.transform.InverseTransformPoint(v.p)).ToArray());mesh.SetNormals(all.Select(v=>r.transform.InverseTransformDirection(v.n)).ToArray());mesh.SetUVs(0,all.Select(v=>v.uv).ToArray());mesh.SetTangents(all.Select(v=>v.tangent).ToArray());mesh.SetColors(all.Select(v=>v.color).ToArray());mesh.subMeshCount=2;mesh.SetTriangles(Enumerable.Range(0,outside.Count).ToArray(),0);mesh.SetTriangles(Enumerable.Range(outside.Count,inside.Count).ToArray(),1);mesh.RecalculateBounds();
   string path=Art+"Finish-"+r.name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;}else AssetDatabase.CreateAsset(mesh,path);
   Undo.RecordObject(r.GetComponent<MeshFilter>(),"Lobby wall finish");Undo.RecordObject(r,"Lobby wall palette");r.GetComponent<MeshFilter>().sharedMesh=mesh;r.sharedMaterials=new[]{original,texture=="image_25"?cream:teal};EditorUtility.SetDirty(r);EditorUtility.SetDirty(r.GetComponent<MeshFilter>());
  }
  // Terracotta pilasters frame the two elevator bays, leaving their doors unobstructed.
  foreach(float x in new[]{1.7f,5f,8.5f})Box("Terracotta wall accent",new Vector3(.28f,3.04f,.055f),new Vector3(x,2.73f,-10.80f),brick);
 }
 static void Append(List<Vertex> triangles,List<Vertex> polygon){for(int i=1;i+1<polygon.Count;i++){triangles.Add(polygon[0]);triangles.Add(polygon[i]);triangles.Add(polygon[i+1]);}}
 static void Lighting(){
  var sun=RenderSettings.sun;if(sun){Undo.RecordObject(sun,"Golden afternoon daylight");sun.color=new Color(1,.85f,.61f);sun.intensity=1.75f;sun.shadowStrength=.82f;EditorUtility.SetDirty(sun);}
  RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.57f,.66f,.78f);RenderSettings.ambientEquatorColor=new Color(.40f,.42f,.45f);RenderSettings.ambientGroundColor=new Color(.42f,.34f,.25f);
  foreach(var light in UnityEngine.Object.FindObjectsByType<Light>().Where(l=>l.name=="Lobby warm fill")){Undo.RecordObject(light,"Balanced warm fill");light.intensity=7f;light.color=new Color(1,.76f,.45f);EditorUtility.SetDirty(light);}
  foreach(var p in UnityEngine.Object.FindObjectsByType<ReflectionProbe>().Where(p=>p.transform.position.x>-10&&p.transform.position.x<10)){p.backgroundColor=new Color(.56f,.68f,.78f);EditorUtility.SetDirty(p);}
  DynamicGI.UpdateEnvironment();
  var camera=GameObject.Find("Player camera").GetComponent<Camera>().GetUniversalAdditionalCameraData();camera.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;camera.antialiasingQuality=AntialiasingQuality.High;EditorUtility.SetDirty(camera);
 }
 static void LocalAtmosphere(){
  var g=new GameObject("Lobby warm colour grading");g.transform.SetParent(root,false);g.transform.position=new Vector3(0,3.5f,-1.8f);var box=g.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(18,10,17.5f);
  var volume=g.AddComponent<Volume>();volume.isGlobal=false;volume.blendDistance=2f;volume.priority=5;
  string path=Art+"LobbyAtmosphere.asset";var p=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);if(!p){p=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(p,path);}
  p.components.RemoveAll(c=>!c);
  if(!p.TryGet<ColorAdjustments>(out var color))color=p.Add<ColorAdjustments>();color.saturation.Override(10);color.contrast.Override(7);color.postExposure.Override(.15f);
  if(!p.TryGet<WhiteBalance>(out var white))white=p.Add<WhiteBalance>();white.temperature.Override(9);
  if(!p.TryGet<Bloom>(out var bloom))bloom=p.Add<Bloom>();bloom.intensity.Override(.18f);bloom.threshold.Override(1.2f);volume.sharedProfile=p;PersistProfile(p);
  var global=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/HotelTogether/Settings/Atmosphere.asset");if(global){global.components.RemoveAll(c=>!c);if(!global.TryGet<Tonemapping>(out var tone))tone=global.Add<Tonemapping>();tone.mode.Override(TonemappingMode.Neutral);if(!global.TryGet<Bloom>(out var globalBloom))globalBloom=global.Add<Bloom>();globalBloom.intensity.Override(.12f);PersistProfile(global);}
 }
 static void PersistProfile(VolumeProfile profile){foreach(var component in profile.components){if(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(component)))AssetDatabase.AddObjectToAsset(component,profile);EditorUtility.SetDirty(component);}EditorUtility.SetDirty(profile);}
 static void AmbientOcclusion(){
  var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;var so=new SerializedObject(pipeline);var renderer=(UniversalRendererData)so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue;
  var feature=renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();if(!feature){feature=ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();feature.name="Soft contact shadows";AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);}
  var settings=new SerializedObject(feature);settings.FindProperty("m_Settings.Downsample").boolValue=true;settings.FindProperty("m_Settings.AfterOpaque").boolValue=true;settings.FindProperty("m_Settings.Source").enumValueIndex=0;settings.FindProperty("m_Settings.Intensity").floatValue=.6f;settings.FindProperty("m_Settings.Radius").floatValue=.15f;settings.FindProperty("m_Settings.BlurQuality").enumValueIndex=1;settings.FindProperty("m_Settings.DirectLightingStrength").floatValue=.15f;settings.ApplyModifiedPropertiesWithoutUndo();feature.SetActive(true);feature.Create();renderer.SetDirty();EditorUtility.SetDirty(renderer);
 }
}
