using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class HotelStairWayfinding
{
    const string Art="Assets/HotelTogether/Art/Stairways/";
    static TMP_FontAsset Font()
    {
        var path=Art+"Stair number font.asset";var asset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(asset)return asset;
        var source=AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/HotelTogether/Resources/HotelTogetherUI/LilitaOne-Regular.ttf");
        asset=TMP_FontAsset.CreateFontAsset(source);asset.name="Stair number font";asset.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ");asset.atlasPopulationMode=AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(asset,path);AssetDatabase.AddObjectToAsset(asset.material,asset);foreach(var texture in asset.atlasTextures)if(texture)AssetDatabase.AddObjectToAsset(texture,asset);EditorUtility.SetDirty(asset);return asset;
    }
    static Material Mat(string name,Color color)
    {
        var path=Art+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.12f);mat.SetFloat("_SpecularHighlights",0);mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");EditorUtility.SetDirty(mat);return mat;
    }
    static void Text(Transform parent,string name,string content,Vector3 position,float size,TMP_FontAsset font)
    {
        var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.position=position;g.transform.rotation=Quaternion.Euler(0,90,0);
        var text=g.AddComponent<TextMeshPro>();text.font=font;text.text=content;text.fontSize=size;text.alignment=TextAlignmentOptions.Center;text.color=new Color(.96f,.94f,.86f);text.rectTransform.sizeDelta=new Vector2(1.25f,1.2f);text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Overflow;text.ForceMeshUpdate();
    }
    public static void Decorate(Transform stairs)
    {
        var font=Font();var colors=new[]{new Color(.055f,.32f,.36f),new Color(.57f,.265f,.17f),new Color(.27f,.37f,.22f)};var labels=new[]{"GROUND FLOOR","GUEST FLOOR","ROOFTOP"};
        for(int floor=0;floor<3;floor++){
            var accent=Mat("Stair floor "+(floor+1)+" accent",colors[floor]);
            foreach(var config in new[]{new Vector3(13,-8,-1),new Vector3(-13,-14,1)}){
                string letter=config.x>0?"A":"B";var parent=stairs.Find("Stair "+letter+" compact switchback");
                if(floor==0){var old=parent.Find("Floor colors and large numbers");if(old)Object.DestroyImmediate(old.gameObject);var root=new GameObject("Floor colors and large numbers");root.transform.SetParent(parent,false);}
                var signs=parent.Find("Floor colors and large numbers");
                float y=floor*4.5f,front=config.y-config.z*2.2f;
                foreach(var r in parent.GetComponentsInChildren<Renderer>().Where(r=>r.name.Contains("teal finish "+floor)||r.name.Contains(" "+floor+" teal panel")))r.sharedMaterial=accent;
                var board=GameObject.CreatePrimitive(PrimitiveType.Cube);board.name="Floor "+(floor+1)+" color panel";board.transform.SetParent(signs,false);board.transform.position=new Vector3(config.x+3.815f,y+1.65f,front);board.transform.localScale=new Vector3(.02f,1.95f,1.25f);board.GetComponent<Renderer>().sharedMaterial=accent;Object.DestroyImmediate(board.GetComponent<Collider>());
                float face=config.x+3.795f;
                Text(signs,"Large floor number "+(floor+1),(floor+1).ToString("00"),new Vector3(face,y+1.72f,front),10,font);
                Text(signs,"Floor label "+(floor+1),labels[floor],new Vector3(face,y+.96f,front),1.6f,font);
                Text(signs,"Stair ID "+(floor+1),"STAIR "+letter,new Vector3(face,y+2.4f,front),1.6f,font);
            }
        }
    }
    [MenuItem("Hotel Together/Environment/Polish stair entries and floor signage")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Exit Play Mode first.");var root=GameObject.Find(HotelStairBuilder.RootName);if(!root)throw new System.InvalidOperationException("Stair root missing.");
        var scene=root.scene;var backup=Path.GetFullPath("../tmp/stairs/HotelLobby-before-stair-wayfinding.unity");if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
        Undo.RegisterFullObjectHierarchyUndo(root,"Repair overlap and add stair wayfinding");HotelStairEnclosureRepair.Repair(root.transform);
        foreach(var a in Object.FindObjectsByType<HotelTogether.HotelCityAtmosphere>(FindObjectsSortMode.None))a.SuspendForSceneSave();foreach(var q in Object.FindObjectsByType<HotelTogether.HotelGraphicsQuality>(FindObjectsSortMode.None))q.SuspendForSceneSave();
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
    }
}
