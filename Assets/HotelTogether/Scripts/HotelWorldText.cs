using System.Collections.Generic;
using UnityEngine;
namespace HotelTogether
{
 [RequireComponent(typeof(TextMesh))]
 public sealed class HotelWorldText : MonoBehaviour
 {
  public Shader shader;
  sealed class FontMaterial {public Material material;public int users;}
  static readonly Dictionary<Font,FontMaterial> materials=new();
  Font font;FontMaterial entry;
  void OnEnable()
  {
   var label=GetComponent<TextMesh>();font=label.font;if(!font||!shader)return;
   if(!materials.TryGetValue(font,out entry)){entry=new FontMaterial{material=new Material(shader){name="Depth tested hotel font",hideFlags=HideFlags.HideAndDontSave}};materials.Add(font,entry);}
   entry.users++;entry.material.mainTexture=font.material.mainTexture;GetComponent<MeshRenderer>().sharedMaterial=entry.material;Font.textureRebuilt+=RefreshAtlas;
  }
  void RefreshAtlas(Font changed){if(changed==font&&entry!=null)entry.material.mainTexture=font.material.mainTexture;}
  void OnDisable(){Font.textureRebuilt-=RefreshAtlas;if(entry==null)return;entry.users--;if(entry.users==0){materials.Remove(font);Destroy(entry.material);}entry=null;font=null;}
 }
}
