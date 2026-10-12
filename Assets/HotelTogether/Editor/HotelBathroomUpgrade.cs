using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HotelTogether;

// Reuses the supplied GLBs and the hotel's existing working door/toilet geometry.
public static class HotelBathroomUpgrade
{
    const string Root = "Assets/HotelTogether/";
    const string Art = Root + "Art/Bathrooms/";
    const string Prefabs = Root + "Prefabs/Bathrooms/";
    static Material tile, grout, accent, bronze, chrome, glass, porcelain, dark, wood;
    static GameObject sinkPrefab, showerPrefab, soapPrefab, shampooPrefab, conditionerPrefab, cupPrefab;
    static GameObject doorSource, doorFrame;
    static Material waterStream, waterDrops;
    static AudioClip tapSound, showerSound;

    [MenuItem("Hotel Together/Rooms/Upgrade all bathrooms")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before upgrading bathrooms.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "HotelLobby") throw new InvalidOperationException("Open HotelLobby first.");
        var baths = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.gameObject.scene == scene && t.name == "Guest en-suite bathroom").OrderBy(t => t.parent.name).ToArray();
        if (baths.Length != 16) throw new InvalidOperationException("Expected 16 guest bathrooms, found " + baths.Length);
        doorSource = AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/Bathroom/DoorLeafPrepared.glb");
        doorFrame = AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/Bathroom/DoorFramePrepared.glb");
        if (!doorSource || !doorFrame) throw new InvalidOperationException("Import the prepared supplied door and frame first.");
        foreach (var file in new[] { "bathroom_stuff", "bathroom_shower", "wellworth_washbasinsink" })
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/Bathroom/" + file + ".glb"))
                throw new InvalidOperationException("Model has not imported: " + file);
        Directory.CreateDirectory(Art); Directory.CreateDirectory(Prefabs);
        var backup=Path.GetFullPath(Path.Combine(Application.dataPath, "../../tmp/bathrooms/backup/HotelLobby-before-water-and-newdoors.unity"));
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Upgrade hotel bathrooms");
        tile = Mat("Ivory porcelain", new Color(.82f,.79f,.70f),.55f);
        grout = Mat("Warm stone grout", new Color(.48f,.48f,.43f),.2f);
        accent = Mat("Sage shower tile", new Color(.25f,.39f,.36f),.52f);
        bronze = Mat("Brushed champagne brass", new Color(.65f,.48f,.25f),.66f,.8f);
        chrome = Mat("Chrome sanitary fittings", new Color(.7f,.73f,.74f),.85f,.94f);
        porcelain = Mat("White glazed ceramic", new Color(.94f,.94f,.89f),.7f);
        dark = Mat("Drain and rubber", new Color(.10f,.13f,.12f),.42f,.2f);
        wood = Mat("Bathroom walnut", new Color(.28f,.17f,.09f),.34f);
        glass = Mat("Clear shower screen", new Color(.67f,.85f,.81f,.12f),.25f);
        glass.SetFloat("_Surface",1); glass.SetFloat("_Blend",0); glass.SetFloat("_SrcBlend",5); glass.SetFloat("_DstBlend",10);
        glass.SetFloat("_ZWrite",0); glass.SetFloat("_Cull",0); glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        glass.DisableKeyword("_ALPHATEST_ON"); glass.renderQueue=3000;
        glass.SetFloat("_SpecularHighlights",0);glass.SetFloat("_EnvironmentReflections",0);
        glass.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");glass.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        sinkPrefab = ModelPrefab("wellworth_washbasinsink", null, "Wellworth pedestal basin",1.01f,0);
        showerPrefab = ModelPrefab("bathroom_shower", null, "Supplied rainfall shower",1.75f,0);
        soapPrefab = ModelPrefab("bathroom_stuff", "Cylinder.002", "Amenity soap bottle",.18f,0);
        shampooPrefab = ModelPrefab("bathroom_stuff", "Cylinder.008", "Amenity shampoo bottle",.23f,0);
        conditionerPrefab = ModelPrefab("bathroom_stuff", "Cylinder.001", "Amenity conditioner bottle",.21f,0);
        cupPrefab = ModelPrefab("bathroom_stuff", "Cube.001", "Supplied toothbrush tumbler",.18f,0);
        PrepareWaterAssets();
        foreach(var b in baths) UpgradeGuest(b);
        int publicSinks=UpgradePublicSinks();
        // Existing downstairs toilets retain their cubicle placement and interaction identity.
        foreach(var item in UnityEngine.Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None).Where(i=>i.sourceModel=="BathroomToilet" && i.transform.position.y<2))
        {
            if(item.transform.Find("Bathroom resize marker")) continue;
            Undo.RecordObject(item.transform,"Slightly enlarge public toilet"); var before=BoundsOf(item.gameObject);
            item.transform.localScale*=1.10f; var after=BoundsOf(item.gameObject); item.transform.position+=Vector3.up*(before.min.y-after.min.y);
            var marker=new GameObject("Bathroom resize marker"); marker.transform.SetParent(item.transform,false); Undo.RegisterCreatedObjectUndo(marker,"Toilet marker");
        }
        Physics.SyncTransforms(); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Undo.CollapseUndoOperations(undo);
        Debug.Log("BATHROOM_UPGRADE_COMPLETE guestBathrooms="+baths.Length+" publicBasins="+publicSinks);
    }

    static Material Mat(string name, Color color, float smooth, float metal=0)
    {
        string path=Art+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.color=color;m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metal);EditorUtility.SetDirty(m);return m;
    }
    static Bounds BoundsOf(GameObject g)
    {
        var rs=g.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
        if(rs.Length==0)throw new InvalidOperationException("Missing visible geometry: "+g.name);
        var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
    }
    static GameObject ModelPrefab(string model,string node,string name,float height,float yaw)
    {
        string path=Prefabs+name+".prefab"; var full=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/Bathroom/"+model+".glb"));
        GameObject visual=full;
        if(node!=null)
        {
            var source=full.GetComponentsInChildren<Transform>().First(t=>t.name==node);
            visual=UnityEngine.Object.Instantiate(source.gameObject);visual.transform.SetPositionAndRotation(source.position,source.rotation);visual.transform.localScale=source.lossyScale;
            UnityEngine.Object.DestroyImmediate(full);
        }
        var wrapper=new GameObject(name);visual.transform.SetParent(wrapper.transform,true);visual.name="Supplied model";
        var b=BoundsOf(wrapper);float scale=height/b.size.y;visual.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);visual.transform.localPosition*=scale;visual.transform.localScale*=scale;
        wrapper.transform.rotation=Quaternion.Euler(0,yaw,0);
        if(model=="wellworth_washbasinsink")foreach(var r in wrapper.GetComponentsInChildren<Renderer>())r.sharedMaterial=r.name=="Object_2"?porcelain:chrome;
        if(model=="bathroom_shower")foreach(var r in wrapper.GetComponentsInChildren<Renderer>())r.sharedMaterial=chrome;
        var prefab=PrefabUtility.SaveAsPrefabAsset(wrapper,path);UnityEngine.Object.DestroyImmediate(wrapper);return prefab;
    }
    static GameObject Group(Transform parent,string name)
    {
        var g=new GameObject(name);g.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(g,name);return g;
    }
    static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material,bool collision=true)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=size;
        g.GetComponent<Renderer>().sharedMaterial=material;if(!collision)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        Undo.RegisterCreatedObjectUndo(g,name);return g;
    }
    static GameObject Place(GameObject prefab,Transform parent,string name,Vector3 pos,float yaw=0)
    {
        var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localRotation=Quaternion.Euler(0,yaw,0);Undo.RegisterCreatedObjectUndo(g,name);return g;
    }
    static void DisableLegacy(Transform b)
    {
        foreach(var child in b.Cast<Transform>().ToArray())
        {
            if(child.name=="Bathroom upgrade - complete en-suite") {Undo.DestroyObjectImmediate(child.gameObject);continue;}
            if(child.name=="BathroomToilet - upstairs" || child.name=="Bathroom warm fill" || child.name=="Bathroom brass mirror frame" || child.name=="Smoked mirror")continue;
            Undo.RecordObject(child.gameObject,"Replace bathroom fixture or wall");child.gameObject.SetActive(false);
        }
    }
    static void UpgradeGuest(Transform b)
    {
        DisableLegacy(b);var root=Group(b,"Bathroom upgrade - complete en-suite").transform;
        // 2.30 x 2.56 m footprint, 0.96 m clear door, continuously closed at every corner.
        Box(root,"Continuous rear wall",new Vector3(0,2.15f,-1.31f),new Vector3(2.50f,4.30f,.12f),tile);
        Box(root,"Continuous front wall",new Vector3(0,2.15f,1.31f),new Vector3(2.50f,4.30f,.12f),tile);
        Box(root,"Continuous basin wall",new Vector3(1.19f,2.15f,0),new Vector3(.12f,4.30f,2.62f),tile);
        Box(root,"Entry wall to shower",new Vector3(-1.19f,2.15f,-.59f),new Vector3(.12f,4.30f,1.44f),tile);
        Box(root,"Entry wall return",new Vector3(-1.19f,2.15f,1.27f),new Vector3(.12f,4.30f,.20f),tile);
        Box(root,"Door lintel closes wall",new Vector3(-1.19f,3.33f,.65f),new Vector3(.12f,1.94f,1.08f),tile);
        Box(root,"Bathroom finished ceiling",new Vector3(0,4.33f,0),new Vector3(2.50f,.06f,2.74f),tile);
        Box(root,"Stone grout floor",new Vector3(0,.013f,0),new Vector3(2.26f,.025f,2.50f),grout,false);
        for(int x=0;x<4;x++)for(int z=0;z<4;z++)
            Box(root,"Large format porcelain tile",new Vector3(-.8475f+x*.565f,.027f,-.9375f+z*.625f),new Vector3(.558f,.007f,.618f),tile,false);
        // Calm sage accent wall and fine grout joints in the wet area.
        Box(root,"Shower sage tiled feature",new Vector3(-.63f,1.48f,-1.239f),new Vector3(.98f,2.89f,.016f),accent,false);
        for(int row=1;row<8;row++)Box(root,"Shower horizontal tile joint",new Vector3(-.63f,row*.36f,-1.227f),new Vector3(.98f,.005f,.004f),grout,false);
        Box(root,"Shower vertical tile joint",new Vector3(-.63f,1.48f,-1.225f),new Vector3(.005f,2.89f,.004f),grout,false);
        Box(root,"Low walk-in shower tray",new Vector3(-.63f,.045f,-.70f),new Vector3(1.00f,.035f,1.08f),porcelain,false);
        Box(root,"Linear shower drain",new Vector3(-.63f,.064f,-1.10f),new Vector3(.64f,.004f,.055f),dark,false);
        for(int i=0;i<11;i++)Box(root,"Drain slotted grate",new Vector3(-.92f+i*.058f,.068f,-1.10f),new Vector3(.012f,.004f,.05f),chrome,false);
        var shower=Place(showerPrefab,root,"Rain shower - supplied bathroom_shower",new Vector3(-.63f,.49f,-.925f));
        AddWater(shower,true);
        Box(root,"Fixed walk-in glass screen",new Vector3(-.09f,1.14f,-.68f),new Vector3(.018f,2.16f,1.12f),glass);
        Box(root,"Screen brass wall channel",new Vector3(-.09f,1.14f,-1.225f),new Vector3(.029f,2.18f,.035f),bronze,false);
        Box(root,"Screen brass edge",new Vector3(-.09f,1.14f,-.13f),new Vector3(.025f,2.18f,.025f),bronze,false);
        Box(root,"Screen stabilizer",new Vector3(-.60f,2.25f,-.16f),new Vector3(1.05f,.018f,.018f),bronze,false);
        Box(root,"Shower amenity shelf",new Vector3(-.87f,1.22f,-1.11f),new Vector3(.40f,.035f,.23f),bronze,false);
        Place(shampooPrefab,root,"Supplied shampoo",new Vector3(-.97f,1.24f,-1.10f));
        Place(conditionerPrefab,root,"Supplied conditioner",new Vector3(-.79f,1.24f,-1.10f));
        var sink=Place(sinkPrefab,root,"Wellworth washbasin - supplied model",new Vector3(.825f,.032f,.58f),270);
        AddWater(sink,false);
        var sinkCol=sink.AddComponent<BoxCollider>();sinkCol.center=new Vector3(0,.47f,0);sinkCol.size=new Vector3(.58f,.90f,.48f);
        // Preserve the original toilet model and its HotelItem, set size once (idempotent).
        var toilet=b.Find("BathroomToilet - upstairs");Undo.RecordObject(toilet,"Resize and position toilet");
        toilet.localScale=Vector3.one;toilet.localRotation=Quaternion.Euler(0,180,0);toilet.localPosition=new Vector3(.61f,.5f,-.78f);
        var bounds=BoundsOf(toilet.gameObject);toilet.position+=Vector3.up*(b.position.y+.032f-bounds.min.y);
        var item=toilet.GetComponent<HotelItem>();if(item){Undo.RecordObject(item,"Toilet home position");item.home=toilet.position;item.homeRotation=toilet.rotation;}
        Box(root,"Slim basin amenity shelf",new Vector3(.99f,1.13f,.60f),new Vector3(.24f,.035f,.76f),wood,false);
        Place(soapPrefab,root,"Supplied hand soap",new Vector3(.98f,1.15f,.34f));
        Place(cupPrefab,root,"Supplied toothbrush cup",new Vector3(.98f,1.15f,.83f));
        Box(root,"Walnut towel shelf",new Vector3(.1f,.62f,1.09f),new Vector3(.65f,.07f,.29f),wood,false);
        var towels=b.Find("LaundryLinen - upstairs");if(towels)
        {
            var copy=UnityEngine.Object.Instantiate(towels.gameObject,root);copy.name="Folded guest towels";copy.SetActive(true);copy.transform.localRotation=Quaternion.identity;
            copy.transform.localPosition=new Vector3(.1f,.70f,1.07f);var tb=BoundsOf(copy);copy.transform.localScale*=.28f/Mathf.Max(tb.size.x,tb.size.z);
            tb=BoundsOf(copy);copy.transform.position+=root.TransformPoint(new Vector3(.1f,.66f,1.07f))-new Vector3(tb.center.x,tb.min.y,tb.center.z);
            foreach(var c in copy.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);foreach(var rb in copy.GetComponentsInChildren<Rigidbody>())UnityEngine.Object.DestroyImmediate(rb);
            foreach(var hi in copy.GetComponentsInChildren<HotelItem>())UnityEngine.Object.DestroyImmediate(hi);Undo.RegisterCreatedObjectUndo(copy,"Guest towels");
        }
        Box(root,"Towel rail",new Vector3(.17f,1.05f,1.09f),new Vector3(.54f,.026f,.026f),bronze,false);
        for(int i=0;i<2;i++)Box(root,"Folded hand towel",new Vector3(.05f+i*.20f,.96f,1.08f),new Vector3(.16f,.20f,.045f),porcelain,false);
        Box(root,"Bath mat outside wet zone",new Vector3(-.60f,.035f,.43f),new Vector3(.72f,.007f,.56f),tile,false);
        Box(root,"Toilet paper holder",new Vector3(1.09f,.65f,-.65f),new Vector3(.075f,.035f,.18f),bronze,false);
        var roll=GameObject.CreatePrimitive(PrimitiveType.Cylinder);roll.name="Toilet paper roll";roll.transform.SetParent(root,false);roll.transform.localPosition=new Vector3(1.055f,.64f,-.65f);roll.transform.localRotation=Quaternion.Euler(90,0,0);roll.transform.localScale=new Vector3(.11f,.09f,.11f);roll.GetComponent<Renderer>().sharedMaterial=porcelain;UnityEngine.Object.DestroyImmediate(roll.GetComponent<Collider>());Undo.RegisterCreatedObjectUndo(roll,"Toilet paper");
        var fill=b.Find("Bathroom warm fill")?.GetComponent<Light>();if(fill){Undo.RecordObject(fill,"Bathroom fill");fill.range=3.8f;fill.intensity=2.0f;fill.color=new Color(1,.89f,.74f);fill.shadows=LightShadows.None;fill.transform.localPosition=new Vector3(0,2.82f,.1f);}
        for(int i=0;i<2;i++)Box(root,"Mirror brass sconce",new Vector3(1.095f,1.85f,.15f+i*.91f),new Vector3(.04f,.44f,.035f),bronze,false);
        BuildDoor(root);
    }
    static void BuildDoor(Transform parent)
    {
        var root=Group(parent,"Bathroom privacy door - E to open");root.transform.localPosition=new Vector3(-1.19f,1.19f,.65f);root.transform.localRotation=Quaternion.Euler(0,90,0);
        var leaf=(GameObject)PrefabUtility.InstantiatePrefab(doorSource);leaf.transform.SetParent(root.transform,false);leaf.name="Supplied door.glb - moving leaf";
        leaf.transform.localPosition=Vector3.zero;leaf.transform.localRotation=Quaternion.identity;
        var leafCollider=leaf.AddComponent<BoxCollider>();leafCollider.size=new Vector3(.94f,2.30f,.055f);
        var frame=(GameObject)PrefabUtility.InstantiatePrefab(doorFrame);frame.transform.SetParent(root.transform,false);frame.name="Supplied door.glb - fixed frame";
        var hi=root.AddComponent<HotelItem>();hi.kind="door";hi.displayName="Bathroom privacy door";hi.sourceModel="Supplied door.glb";hi.dimensions=new Vector3(.96f,2.32f,.08f);
        float angle=-root.transform.eulerAngles.y*Mathf.Deg2Rad;
        hi.door=new DoorData{x=-root.transform.position.x,z=root.transform.position.z,w=.96f,height=2.32f,@base=angle,outside=1,slide=false,
            leaves=new[]{new LeafData{id=leaf.name,side=1,width=.96f,hinge=.48f,closed=0}}};
        hi.leaves=new[]{leaf.transform};hi.home=root.transform.position;hi.homeRotation=root.transform.rotation;
        var dl=leaf.GetComponent<DoorLeaf>();if(!dl)dl=leaf.AddComponent<DoorLeaf>();dl.owner=hi;
        var proxy=root.AddComponent<BoxCollider>();proxy.isTrigger=true;proxy.size=new Vector3(.96f,2.32f,.12f);
        foreach(float x in new[]{-.52f,.52f}){var jamb=Group(root.transform,"Frame jamb collision");jamb.transform.localPosition=new Vector3(x,0,0);jamb.AddComponent<BoxCollider>().size=new Vector3(.05f,2.38f,.15f);}
        Undo.RegisterCreatedObjectUndo(frame,"Supplied door frame");
        Undo.RegisterCreatedObjectUndo(leaf,"Bathroom door leaf");
    }
    static void PrepareWaterAssets()
    {
        var path=Art+"Soft water droplet.asset";
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(!texture)
        {
            texture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Soft water droplet",wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){float radius=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;float alpha=Mathf.Pow(Mathf.Clamp01(1-radius),1.5f);texture.SetPixel(x,y,new Color(1,1,1,alpha));}
            texture.Apply();AssetDatabase.CreateAsset(texture,path);
        }
        waterDrops=WaterMaterial("Water droplets",texture);
        waterStream=WaterMaterial("Clear running water",null);
        tapSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/TapRunningWater.wav");
        showerSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/ShowerRunningWater.wav");
        if(!tapSound||!showerSound)throw new InvalidOperationException("Import the water audio before applying.");
    }
    static Material WaterMaterial(string name,Texture texture)
    {
        var path=Art+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",new Color(.85f,.95f,1,.70f));if(texture)m.SetTexture("_BaseMap",texture);
        m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.DisableKeyword("_ALPHATEST_ON");m.DisableKeyword("_ALPHAPREMULTIPLY_ON");m.renderQueue=3000;EditorUtility.SetDirty(m);return m;
    }
    static void AddWater(GameObject g,bool shower)
    {
        var item=g.AddComponent<HotelItem>();item.kind="water";item.displayName=shower?"Rain shower":"Washbasin tap";item.sourceModel=shower?"BathroomShower":"WellworthBasin";
        var fixture=g.AddComponent<HotelWaterFixture>();item.waterFixture=fixture;fixture.shower=shower;
        fixture.outlet=shower?new Vector3(0,1.50f,.21f):new Vector3(0,.925f,.01f);
        fixture.impact=shower?new Vector3(0,-.42f,.21f):new Vector3(0,.765f,.01f);
        fixture.streamMaterial=waterStream;fixture.dropletMaterial=waterDrops;fixture.waterSound=shower?showerSound:tapSound;fixture.volume=shower?.50f:.38f;
        var trigger=g.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=shower?new Vector3(0,.55f,.08f):new Vector3(0,.94f,.01f);trigger.size=shower?new Vector3(.36f,.40f,.27f):new Vector3(.25f,.22f,.25f);
    }
    static int UpgradePublicSinks()
    {
        var old=GameObject.Find("Prop_0268");if(!old)return 0;
        var existing=old.transform.parent.Find("Public bathroom - supplied Wellworth basins");if(existing)Undo.DestroyObjectImmediate(existing.gameObject);
        foreach(var r in old.GetComponentsInChildren<Renderer>()){Undo.RecordObject(r,"Replace public sink");r.enabled=false;}
        foreach(var c in old.GetComponentsInChildren<Collider>()){Undo.RecordObject(c,"Replace public sink collision");c.enabled=false;}
        var root=Group(old.transform.parent,"Public bathroom - supplied Wellworth basins").transform;
        for(int i=0;i<2;i++)
        {
            var g=Place(sinkPrefab,root,"Public Wellworth washbasin "+(i+1),new Vector3(13.91f+i*.88f,.028f,-11.50f),180);
            AddWater(g,false);
            var c=g.AddComponent<BoxCollider>();c.center=new Vector3(0,.47f,0);c.size=new Vector3(.58f,.90f,.48f);
            Box(root,"Public basin amenity shelf",new Vector3(13.91f+i*.88f,1.10f,-11.18f),new Vector3(.64f,.035f,.18f),wood,false);
            Place(soapPrefab,root,"Public supplied soap",new Vector3(13.70f+i*.88f,1.12f,-11.18f));
        }
        // The original soap stood on the old countertop; move it onto the new shelf.
        var soap=GameObject.Find("Prop_0269");if(soap){Undo.RecordObject(soap.transform,"Public soap shelf");soap.transform.position=new Vector3(14.14f,1.17f,-11.18f);}
        return 2;
    }
}
