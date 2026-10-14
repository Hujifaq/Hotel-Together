using System.Collections.Generic;
using UnityEngine;
namespace HotelTogether
{
 [RequireComponent(typeof(TextMesh))]
 public sealed class HotelWorldText : MonoBehaviour
 {
  public Shader shader;
  public float maximumDistance=12;
  public static TextMesh Create(Transform parent,string text,Vector3 position,float size,Color color,bool positiveZ=false)
  {
   var go=new GameObject("Label / "+text.Replace('\n',' '));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=positiveZ?Quaternion.Euler(0,180,0):Quaternion.identity;
   var label=go.AddComponent<TextMesh>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.text=text;label.characterSize=size;label.fontSize=48;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=color;go.AddComponent<HotelWorldText>();return label;
  }
  sealed class FontMaterial {public Material material;public int users;}
  static readonly Dictionary<Font,FontMaterial> materials=new();
  Font font;FontMaterial entry;
  void OnEnable()
  {
   var label=GetComponent<TextMesh>();font=label.font;if(!shader)shader=Shader.Find("HotelTogether/WorldText");if(!font||!shader)return;
   if(!materials.TryGetValue(font,out entry)){entry=new FontMaterial{material=new Material(shader){name="Depth tested hotel font",hideFlags=HideFlags.HideAndDontSave}};materials.Add(font,entry);}
   entry.users++;entry.material.mainTexture=font.material.mainTexture;GetComponent<MeshRenderer>().sharedMaterial=entry.material;Font.textureRebuilt+=RefreshAtlas;
  }
  void RefreshAtlas(Font changed){if(changed==font&&entry!=null)entry.material.mainTexture=font.material.mainTexture;}
  void LateUpdate(){var camera=Camera.main;if(camera&&maximumDistance>0)GetComponent<MeshRenderer>().enabled=(camera.transform.position-transform.position).sqrMagnitude<maximumDistance*maximumDistance;}
  void OnDisable(){Font.textureRebuilt-=RefreshAtlas;if(entry==null)return;entry.users--;if(entry.users==0){materials.Remove(font);Destroy(entry.material);}entry=null;font=null;}
 }
}
