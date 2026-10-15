using System.Collections.Generic;
using UnityEngine;
namespace HotelTogether
{
 /// <summary>
 /// World-space signs and floating labels. Latin text is upgraded to the game's Lilita One font at a high
 /// atlas resolution (crisp instead of blurry Arial) and gets a soft drop shadow so it reads on any wall.
 /// </summary>
 [RequireComponent(typeof(TextMesh))]
 public sealed class HotelWorldText : MonoBehaviour
 {
  public Shader shader;
  public float maximumDistance=12;
  [Tooltip("Swap to the bundled Lilita One font for Latin text.")] public bool cartoonFont=true;
  [Tooltip("Dark copy behind the text for depth and contrast.")] public bool dropShadow=true;
  public Color shadowColor=new Color(.10f,.06f,.04f,.6f);
  const int AtlasSize=96;
  static Font lilita;static bool lilitaLoaded;

  public static TextMesh Create(Transform parent,string text,Vector3 position,float size,Color color,bool positiveZ=false)
  {
   var go=new GameObject("Label / "+text.Replace('\n',' '));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=positiveZ?Quaternion.Euler(0,180,0):Quaternion.identity;
   var label=go.AddComponent<TextMesh>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.text=text;label.characterSize=size;label.fontSize=48;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=color;go.AddComponent<HotelWorldText>();return label;
  }
  sealed class FontMaterial {public Material material;public int users;}
  static readonly Dictionary<Font,FontMaterial> materials=new();
  Font font;FontMaterial entry;TextMesh label,shadow;MeshRenderer body,shadowRenderer;

  static Font Lilita(){if(!lilitaLoaded){lilitaLoaded=true;lilita=Resources.Load<Font>("HotelTogetherUI/LilitaOne-Regular");}return lilita;}
  static bool IsLatin(string text){if(text==null)return true;foreach(char c in text)if(c>127)return false;return true;}

  void UpgradeFont()
  {
   if(!cartoonFont||!IsLatin(label.text))return;
   var target=Lilita();if(!target||label.font==target)return;
   int size=label.fontSize<=0?13:label.fontSize;
   // Same on-screen height: a bigger atlas size with a proportionally smaller character size. Lilita is a
   // little wider than Arial, so it is scaled down slightly to keep text inside its sign.
   label.characterSize*=size/(float)AtlasSize*.9f;label.fontSize=AtlasSize;label.font=target;
  }

  void OnEnable()
  {
   label=GetComponent<TextMesh>();body=GetComponent<MeshRenderer>();
   UpgradeFont();
   font=label.font;if(!shader)shader=Shader.Find("HotelTogether/WorldText");if(!font||!shader)return;
   if(!materials.TryGetValue(font,out entry)){entry=new FontMaterial{material=new Material(shader){name="Depth tested hotel font",hideFlags=HideFlags.HideAndDontSave}};materials.Add(font,entry);}
   entry.users++;entry.material.mainTexture=font.material.mainTexture;body.sharedMaterial=entry.material;Font.textureRebuilt+=RefreshAtlas;
   if(dropShadow)EnsureShadow();
  }

  void EnsureShadow()
  {
   if(!shadow)
   {
    var go=new GameObject("Shadow"){hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);
    go.AddComponent<HotelWorldTextShadow>();shadow=go.AddComponent<TextMesh>();shadowRenderer=go.GetComponent<MeshRenderer>();
    shadowRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;shadowRenderer.receiveShadows=false;
   }
   SyncShadow();
  }

  void SyncShadow()
  {
   if(!shadow)return;
   if(shadow.text!=label.text)shadow.text=label.text;
   shadow.font=label.font;shadow.fontSize=label.fontSize;shadow.characterSize=label.characterSize;shadow.anchor=label.anchor;shadow.alignment=label.alignment;shadow.lineSpacing=label.lineSpacing;shadow.fontStyle=label.fontStyle;shadow.richText=label.richText;
   var c=shadowColor;c.a*=label.color.a;shadow.color=c;
   // Offset down-right and slightly behind the text (text faces -Z), scaled with the letter size.
   float h=label.characterSize*label.fontSize*.1f;
   shadow.transform.localPosition=new Vector3(h*.05f,-h*.06f,h*.03f);
   if(entry!=null)shadowRenderer.sharedMaterial=entry.material;
  }

  void RefreshAtlas(Font changed){if(changed==font&&entry!=null)entry.material.mainTexture=font.material.mainTexture;}
  void LateUpdate()
  {
   var camera=Camera.main;bool visible=true;
   if(camera&&maximumDistance>0)visible=(camera.transform.position-transform.position).sqrMagnitude<maximumDistance*maximumDistance;
   body.enabled=visible;
   if(shadow){shadowRenderer.enabled=visible&&dropShadow;if(visible&&(shadow.text!=label.text||shadow.font!=label.font))SyncShadow();}
   else if(dropShadow&&visible&&entry!=null)EnsureShadow();
  }
  void OnDisable(){if(shadowRenderer)shadowRenderer.enabled=false;Font.textureRebuilt-=RefreshAtlas;if(entry==null)return;entry.users--;if(entry.users==0){materials.Remove(font);Destroy(entry.material);}entry=null;font=null;}
  void OnDestroy(){if(shadow)Destroy(shadow.gameObject);}
 }

 /// <summary>Marks the drop-shadow copy so it is never upgraded or shadowed itself.</summary>
 public sealed class HotelWorldTextShadow:MonoBehaviour{}
}
