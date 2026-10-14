using UnityEngine;
using UnityEngine.AI;
namespace HotelTogether
{
    // Metre-scaled, reusable shapes. Existing decorative extinguishers are made usable too.
    public static class HotelMaintenanceModels
    {
        static GameObject Shape(Transform parent,string name,PrimitiveType primitive,Vector3 position,Vector3 size,Material material,Quaternion rotation=default)
        {
            var obj=GameObject.CreatePrimitive(primitive);obj.name=name;obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position;obj.transform.localScale=size;obj.transform.localRotation=rotation==default?Quaternion.identity:rotation;
            obj.GetComponent<Renderer>().sharedMaterial=material;var collider=obj.GetComponent<Collider>();collider.enabled=false;if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);return obj;
        }
        static void Label(Transform parent,string text,Vector3 local,float size,Color color)
        {
            HotelWorldText.Create(parent,text,local,size*.42f,color).GetComponent<HotelWorldText>().maximumDistance=8;
        }
        public static ParticleSystem Particles(Transform parent,Material material,Color color,float rate,float size,float lifetime,float speed,float angle)
        {
            var obj=new GameObject("Maintenance particles");obj.transform.SetParent(parent,false);
            var particles=obj.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.loop=true;main.maxParticles=140;main.startLifetime=lifetime;main.startSpeed=speed;main.startSize=new ParticleSystem.MinMaxCurve(size*.6f,size);main.startColor=color;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=particles.emission;emission.rateOverTime=rate;
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=angle;shape.radius=.14f;
            var fade=particles.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return particles;
        }
        public static HotelMaintenanceTool Tool(HotelMaintenanceSystem system,MaintenanceToolKind kind,Vector3 point,Transform parent)
        {
            var root=new GameObject(kind.ToString());root.transform.SetParent(parent,false);root.transform.position=point;
            var metal=system.CreateMaterial("Tool brushed metal",new Color(.55f,.62f,.67f));metal.SetFloat("_Metallic",.8f);metal.SetFloat("_Smoothness",.65f);
            var black=system.CreateMaterial("Tool rubber",new Color(.025f,.035f,.04f));
            var red=system.CreateMaterial("Extinguisher safety red",new Color(.82f,.045f,.025f));
            var orange=system.CreateMaterial("Insulated tool grip",new Color(1,.38f,.045f));
            var item=root.AddComponent<HotelItem>();item.dimensions=kind==MaintenanceToolKind.Extinguisher?new Vector3(.28f,.62f,.26f):new Vector3(.14f,.075f,.48f);
            var box=root.AddComponent<BoxCollider>();box.size=item.dimensions;
            var tool=root.AddComponent<HotelMaintenanceTool>();
            if(kind==MaintenanceToolKind.Extinguisher)
            {
                Shape(root.transform,"Pressure vessel",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.21f,.22f,.21f),red);
                Shape(root.transform,"Rounded shoulder",PrimitiveType.Sphere,new Vector3(0,.21f,0),new Vector3(.21f,.13f,.21f),red);
                Shape(root.transform,"Bottom collar",PrimitiveType.Cylinder,new Vector3(0,-.22f,0),new Vector3(.22f,.015f,.22f),black);
                Shape(root.transform,"Valve stem",PrimitiveType.Cylinder,new Vector3(0,.27f,0),new Vector3(.025f,.035f,.025f),metal);
                Shape(root.transform,"Carry handle",PrimitiveType.Cube,new Vector3(0,.305f,-.015f),new Vector3(.14f,.025f,.055f),black);
                tool.squeezeLever=Shape(root.transform,"Squeeze lever",PrimitiveType.Cube,new Vector3(0,.337f,-.015f),new Vector3(.13f,.018f,.05f),red,Quaternion.Euler(0,0,-12)).transform;
                Shape(root.transform,"Pressure gauge",PrimitiveType.Cylinder,new Vector3(-.04f,.26f,.035f),new Vector3(.025f,.012f,.025f),metal,Quaternion.Euler(90,0,0));
                var hoseObj=new GameObject("Flexible black hose");hoseObj.transform.SetParent(root.transform,false);var hose=hoseObj.AddComponent<LineRenderer>();hose.useWorldSpace=false;hose.sharedMaterial=black;hose.widthMultiplier=.018f;hose.numCornerVertices=5;hose.positionCount=5;hose.SetPositions(new[]{new Vector3(.02f,.27f,0),new Vector3(.12f,.24f,0),new Vector3(.14f,-.14f,0),new Vector3(.12f,.12f,.13f),new Vector3(.08f,.15f,.16f)});
                tool.nozzle=new GameObject("Aimable discharge nozzle").transform;tool.nozzle.SetParent(root.transform,false);tool.nozzle.localPosition=new Vector3(.08f,.15f,.16f);
                Shape(tool.nozzle,"Nozzle body",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.018f,.055f,.018f),black,Quaternion.Euler(90,0,0));
                Shape(root.transform,"Instruction plate",PrimitiveType.Cube,new Vector3(0,0,-.106f),new Vector3(.13f,.16f,.008f),system.CreateMaterial("Extinguisher label",Color.white));
                Label(root.transform,"FIRE\nABC",new Vector3(0,0,-.114f),.026f,Color.black);
            }
            else if(kind==MaintenanceToolKind.Wrench)
            {
                Shape(root.transform,"Steel handle",PrimitiveType.Cube,new Vector3(0,0,-.025f),new Vector3(.046f,.025f,.32f),metal);
                Shape(root.transform,"Handle inset",PrimitiveType.Cube,new Vector3(0,.014f,-.025f),new Vector3(.025f,.006f,.23f),black);
                Shape(root.transform,"Jaw shoulder",PrimitiveType.Cube,new Vector3(0,0,.15f),new Vector3(.13f,.03f,.055f),metal);
                Shape(root.transform,"Fixed jaw",PrimitiveType.Cube,new Vector3(-.055f,0,.20f),new Vector3(.032f,.03f,.09f),metal);
                Shape(root.transform,"Adjustable jaw",PrimitiveType.Cube,new Vector3(.045f,0,.20f),new Vector3(.034f,.03f,.07f),metal);
                Shape(root.transform,"Adjustment screw",PrimitiveType.Cylinder,new Vector3(0,0,.15f),new Vector3(.018f,.048f,.018f),black,Quaternion.Euler(0,0,90));
            }
            else
            {
                Shape(root.transform,"Insulated orange grip",PrimitiveType.Cylinder,new Vector3(0,0,-.12f),new Vector3(.037f,.08f,.037f),orange,Quaternion.Euler(90,0,0));
                Shape(root.transform,"Rubber grip band",PrimitiveType.Cylinder,new Vector3(0,0,-.10f),new Vector3(.039f,.042f,.039f),black,Quaternion.Euler(90,0,0));
                Shape(root.transform,"Insulated shaft",PrimitiveType.Cylinder,new Vector3(0,0,.06f),new Vector3(.009f,.10f,.009f),orange,Quaternion.Euler(90,0,0));
                Shape(root.transform,"Driver bit",PrimitiveType.Cube,new Vector3(0,0,.18f),new Vector3(.016f,.005f,.045f),metal);
            }
            tool.Configure(system,kind);system.tools.Add(tool);return tool;
        }
        static bool FindSpace(HotelMaintenanceSystem system,Vector3 preferred,Vector3 half,out Vector3 result)
        {
            Physics.SyncTransforms();
            float floor=system.FloorHeight(preferred.y);preferred.y=floor;
            int type=system.hotel.guestPrefabs!=null&&system.hotel.guestPrefabs.Length>0?system.hotel.guestPrefabs[0].GetComponent<NavMeshAgent>().agentTypeID:0;
            var filter=new NavMeshQueryFilter{agentTypeID=type,areaMask=NavMesh.AllAreas};
            for(int i=0;i<65;i++)
            {
                float angle=i*Mathf.PI/4,radius=i==0?0:.45f*Mathf.Ceil(i/8f);
                var point=preferred+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                if(!NavMesh.SamplePosition(point,out var hit,.5f,filter)||Mathf.Abs(hit.position.y-floor)>.35f)continue;
                if(Physics.CheckBox(hit.position+Vector3.up*(half.y+.08f),half,Quaternion.identity,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore))continue;
                result=hit.position;return true;
            }
            result=preferred;return false;
        }
        public static void Build(HotelMaintenanceSystem system)
        {
            var root=new GameObject("Hotel maintenance equipment and incident sites");root.transform.SetParent(system.transform,false);
            var grey=system.CreateMaterial("Maintenance cabinet graphite",new Color(.10f,.18f,.21f));
            var steel=system.CreateMaterial("Maintenance galvanized metal",new Color(.48f,.56f,.60f));steel.SetFloat("_Metallic",.65f);
            var amber=system.CreateMaterial("Maintenance safety amber",new Color(1,.49f,.05f));
            var white=system.CreateMaterial("Maintenance white labels",new Color(.9f,.92f,.88f));
            Vector3 rackPreferred=new Vector3(-4.5f,.025f,-10.5f);
            foreach(var item in Object.FindObjectsByType<HotelItem>(FindObjectsSortMode.None))
            {
                if(item.name=="Prop_0012")rackPreferred=item.transform.position+item.transform.right*1.4f+item.transform.forward*.7f;
                if(item.sourceModel=="WallExtinguisher")
                {
                    HotelEquipmentPlacement.AttachExisting(item);
                    var tool=item.GetComponent<HotelMaintenanceTool>();if(!tool)tool=item.gameObject.AddComponent<HotelMaintenanceTool>();tool.Configure(system,MaintenanceToolKind.Extinguisher);system.tools.Add(tool);
                }
            }
            if(FindSpace(system,rackPreferred,new Vector3(.6f,.48f,.2f),out var rackPoint))
            {
                var rackRoot=new GameObject("Staff maintenance rack");rackRoot.transform.SetParent(root.transform,false);
                bool mounted=HotelEquipmentPlacement.Wall(system,rackPoint,1.18f,.175f,out var mount,out var rotation);
                rackRoot.transform.SetPositionAndRotation(mounted?mount:rackPoint,mounted?rotation:HotelEquipmentPlacement.FaceOpenArea(system,rackPoint));rackPoint=rackRoot.transform.position;
                Shape(rackRoot.transform,"Equipment shelf",PrimitiveType.Cube,new Vector3(0,.70f,0),new Vector3(1.18f,.05f,.34f),steel);
                Shape(rackRoot.transform,"Back board",PrimitiveType.Cube,new Vector3(0,.5f,.15f),new Vector3(1.18f,.95f,.05f),grey);
                var item=rackRoot.AddComponent<HotelItem>();item.kind="maintenanceRack";item.displayName="Staff maintenance / tool refill";item.dimensions=new Vector3(1.18f,.95f,.34f);
                if(!mounted){Shape(rackRoot.transform,"Left rack foot",PrimitiveType.Cube,new Vector3(-.49f,.27f,.1f),new Vector3(.06f,.54f,.1f),steel);Shape(rackRoot.transform,"Right rack foot",PrimitiveType.Cube,new Vector3(.49f,.27f,.1f),new Vector3(.06f,.54f,.1f),steel);}
                var collider=rackRoot.AddComponent<BoxCollider>();collider.center=new Vector3(0,.5f,-.055f);collider.size=new Vector3(1.18f,.95f,.46f);
                Obstacle(rackRoot,collider);
                system.rack=rackRoot.AddComponent<HotelMaintenanceRack>();system.rack.system=system;
                Label(rackRoot.transform,"MAINTENANCE\nTOOLS / REFILL",new Vector3(.20f,.90f,.118f),.033f,Color.white);
                foreach(var entry in new[]{(MaintenanceToolKind.Extinguisher,new Vector3(-.36f,1.06f,-.10f)),(MaintenanceToolKind.Wrench,new Vector3(.08f,.77f,-.08f)),(MaintenanceToolKind.Screwdriver,new Vector3(.40f,.77f,-.08f))}){var tool=Tool(system,entry.Item1,rackRoot.transform.TransformPoint(entry.Item2),rackRoot.transform);tool.transform.rotation=rackRoot.transform.rotation;var toolItem=tool.GetComponent<HotelItem>();toolItem.home=tool.transform.position;toolItem.homeRotation=tool.transform.rotation;}
            }
            else Debug.LogWarning("No clear NavMesh position for maintenance rack. Tools were not placed.");
            // Fire sites occupy free floor patches near equipment, without replacing the original scene.
            AddGroundSite(system,root.transform,MaintenanceIncidentKind.Fire,"Kitchen",new Vector3(-11,.025f,-23.5f),grey,steel,amber);
            AddGroundSite(system,root.transform,MaintenanceIncidentKind.Fire,"Laundry",new Vector3(23,.025f,-24.5f),grey,steel,amber);
            int pipes=0;
            foreach(var fixture in Object.FindObjectsByType<HotelWaterFixture>(FindObjectsSortMode.None))
            {
                if(fixture.shower||fixture.transform.position.y>2)continue;
                var site=AddGroundSite(system,root.transform,MaintenanceIncidentKind.PipeBurst,"Lobby water line "+(pipes+1),fixture.transform.position+fixture.transform.forward*.75f,grey,steel,amber);
                if(site){site.fixture=fixture;if(++pipes>=2)break;}
            }
            if(pipes==0)AddGroundSite(system,root.transform,MaintenanceIncidentKind.PipeBurst,"Utility water line",new Vector3(-23,.025f,-24),grey,steel,amber);
            if(system.hotel.elevators!=null)foreach(var lift in system.hotel.elevators)if(lift)
            {
                var site=AddGroundSite(system,root.transform,MaintenanceIncidentKind.ElevatorFault,lift.service?"Service lift":"Guest lift",lift.ExitPoint(0)+Vector3.right*1.65f,grey,steel,amber);
                if(site)site.elevator=lift;
            }
            AddGroundSite(system,root.transform,MaintenanceIncidentKind.PowerOutage,"Electrical room",new Vector3(-19.4f,.025f,-23.3f),grey,steel,amber);
            foreach(var point in new[]{rackPoint+Vector3.up*1.6f,new Vector3(0,2,-9),new Vector3(0,6.5f,-18),new Vector3(0,11,-18)})
            {
                var foot=point;foot.y=system.FloorHeight(point.y);
                Vector3 lampPosition=point;Quaternion lampRotation=Quaternion.identity;
                if(HotelEquipmentPlacement.Wall(system,foot,.24f,.04f,out var wall,out var facing)){lampPosition=wall+Vector3.up*2.05f;lampRotation=facing;}
                else if(Physics.Raycast(point,Vector3.up,out var ceiling,4,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&ceiling.normal.y<-.9f){lampPosition=ceiling.point+Vector3.down*.045f;}
                else {Shape(root.transform,"Emergency lamp support",PrimitiveType.Cylinder,new Vector3(point.x,(point.y+foot.y)*.5f,point.z),new Vector3(.025f,(point.y-foot.y)*.5f,.025f),steel);Shape(root.transform,"Emergency lamp floor base",PrimitiveType.Cube,foot+Vector3.up*.025f,new Vector3(.18f,.05f,.18f),steel);}
                var lamp=Shape(root.transform,"Emergency route lamp",PrimitiveType.Cube,lampPosition,new Vector3(.24f,.08f,.08f),white,lampRotation);lamp.transform.position=lampPosition;
                var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(.3f,.85f,1);light.intensity=1.6f;light.range=9;system.RegisterEmergency(light);
            }
        }
        static HotelMaintenanceIncident AddGroundSite(HotelMaintenanceSystem system,Transform parent,MaintenanceIncidentKind kind,string label,Vector3 preferred,Material grey,Material metal,Material amber)
        {
            if(!FindSpace(system,preferred,new Vector3(.27f,kind==MaintenanceIncidentKind.Fire?.42f:.70f,.23f),out var point)){Debug.LogWarning("No reachable maintenance site: "+label);return null;}
            var root=new GameObject(label+" / "+kind);root.transform.SetParent(parent,false);
            Vector3 wallPoint=point;Quaternion wallRotation=Quaternion.identity;
            bool mounted=kind!=MaintenanceIncidentKind.Fire&&HotelEquipmentPlacement.Wall(system,point,kind==MaintenanceIncidentKind.PipeBurst?.3f:.54f,kind==MaintenanceIncidentKind.PipeBurst?.025f:.09f,out wallPoint,out wallRotation);
            root.transform.SetPositionAndRotation(mounted?wallPoint:point,mounted?wallRotation:HotelEquipmentPlacement.FaceOpenArea(system,point));
            var item=root.AddComponent<HotelItem>();item.kind="maintenanceIncident";item.displayName=label;
            var incident=root.AddComponent<HotelMaintenanceIncident>();incident.system=system;incident.kind=kind;incident.location=label;incident.workPoint=new Vector3(0,kind==MaintenanceIncidentKind.Fire?.20f:.8f,-.20f);
            var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,kind==MaintenanceIncidentKind.Fire?.5f:.75f,kind==MaintenanceIncidentKind.Fire?0:-.035f);collider.size=new Vector3(.54f,kind==MaintenanceIncidentKind.Fire?1:1.5f,kind==MaintenanceIncidentKind.Fire?.40f:.25f);collider.isTrigger=kind==MaintenanceIncidentKind.Fire;
            if(kind!=MaintenanceIncidentKind.Fire)Obstacle(root,collider);
            if(kind==MaintenanceIncidentKind.Fire)
            {
                incident.selectionCollider=collider;collider.enabled=false;
                var flameMaterial=system.CreateMaterial("Soft amber flames",new Color(1,.25f,.015f,.85f),true);
                incident.effect=Particles(root.transform,flameMaterial,new Color(1,.54f,.06f,.9f),48,.36f,.8f,1.15f,16);incident.effect.transform.localPosition=Vector3.up*.15f;incident.effect.transform.localRotation=Quaternion.Euler(-90,0,0);
                incident.smoke=Particles(root.transform,system.CreateMaterial("Soft smoke",new Color(.18f,.20f,.21f,.45f),true),new Color(.28f,.30f,.31f,.45f),15,.5f,2,1,22);incident.smoke.transform.localRotation=Quaternion.Euler(-90,0,0);
                var light=root.AddComponent<Light>();light.color=new Color(1,.28f,.025f);light.intensity=1.8f;light.range=4;light.enabled=false;incident.effectLight=light;
            }
            else if(kind==MaintenanceIncidentKind.PipeBurst)
            {
                if(mounted){foreach(float height in new[]{.25f,.88f})Shape(root.transform,"Wall pipe clamp",PrimitiveType.Cube,new Vector3(0,height,.018f),new Vector3(.10f,.035f,.022f),metal);}else Shape(root.transform,"Pipe floor support",PrimitiveType.Cylinder,new Vector3(0,.025f,0),new Vector3(.12f,.025f,.12f),metal);
                Shape(root.transform,"Pipe riser",PrimitiveType.Cylinder,new Vector3(0,.5f,0),new Vector3(.04f,.46f,.04f),metal);
                Shape(root.transform,"Pipe coupling",PrimitiveType.Cylinder,new Vector3(0,.7f,0),new Vector3(.064f,.045f,.064f),metal);
                Shape(root.transform,"Valve boss",PrimitiveType.Cylinder,new Vector3(0,.9f,-.06f),new Vector3(.045f,.045f,.045f),amber,Quaternion.Euler(90,0,0));
                var wheel=new GameObject("Water shutoff handwheel");wheel.transform.SetParent(root.transform,false);wheel.transform.localPosition=new Vector3(0,.9f,-.13f);incident.valve=wheel.transform;
                Shape(wheel.transform,"Valve horizontal spoke",PrimitiveType.Cube,Vector3.zero,new Vector3(.23f,.025f,.025f),amber);Shape(wheel.transform,"Valve vertical spoke",PrimitiveType.Cube,Vector3.zero,new Vector3(.025f,.23f,.025f),amber);
                incident.effect=Particles(root.transform,system.CreateMaterial("Pipe spray water",new Color(.25f,.7f,1,.65f),true),new Color(.45f,.82f,1,.7f),70,.026f,.65f,3.8f,18);incident.effect.transform.localPosition=new Vector3(0,.7f,-.045f);incident.effect.transform.localRotation=Quaternion.Euler(0,180,0);
                var gravity=incident.effect.main;gravity.gravityModifier=.7f;Label(root.transform,"WATER\nSHUTOFF",new Vector3(0,.35f,-.07f),.03f,Color.white);
            }
            else
            {
                if(!mounted){Shape(root.transform,"Service cabinet pedestal",PrimitiveType.Cube,new Vector3(0,.3f,.05f),new Vector3(.12f,.60f,.12f),metal);Shape(root.transform,"Service cabinet base",PrimitiveType.Cube,new Vector3(0,.035f,.05f),new Vector3(.38f,.07f,.30f),metal);}
                Shape(root.transform,"Service cabinet",PrimitiveType.Cube,new Vector3(0,.9f,0),new Vector3(.46f,.58f,.18f),grey);
                var hinge=new GameObject("Hinged cabinet cover");hinge.transform.SetParent(root.transform,false);hinge.transform.localPosition=new Vector3(-.23f,.9f,-.10f);incident.cover=hinge.transform;
                Shape(hinge.transform,"Cabinet door",PrimitiveType.Cube,new Vector3(.23f,0,0),new Vector3(.45f,.57f,.025f),metal);
                var switchRoot=new GameObject("Restart breaker");switchRoot.transform.SetParent(root.transform,false);switchRoot.transform.localPosition=new Vector3(.11f,.82f,-.13f);incident.breaker=switchRoot.transform;
                Shape(switchRoot.transform,"Breaker lever",PrimitiveType.Cube,Vector3.zero,new Vector3(.07f,.13f,.07f),amber);
                incident.indicator=Shape(root.transform,"Fault status LED",PrimitiveType.Sphere,new Vector3(-.10f,1.08f,-.12f),Vector3.one*.04f,amber).GetComponent<Renderer>();
                Shape(root.transform,"Cabinet identification plate",PrimitiveType.Cube,new Vector3(0,1.28f,-.095f),new Vector3(.54f,.17f,.025f),grey);
                Label(root.transform,kind==MaintenanceIncidentKind.PowerOutage?"POWER\nFUSE / RESET":"LIFT\nSERVICE / TEST",new Vector3(0,1.28f,-.110f),.032f,Color.white);
                incident.effect=Particles(root.transform,system.CreateMaterial("Fault sparks",new Color(1,.6f,.05f,.8f),true),new Color(1,.75f,.15f,.9f),7,.028f,.28f,1.8f,40);incident.effect.transform.localPosition=new Vector3(0,.9f,-.11f);incident.effect.transform.localRotation=Quaternion.Euler(0,180,0);
            }
            incident.ambientSource=root.AddComponent<AudioSource>();incident.ambientSource.loop=true;incident.ambientSource.playOnAwake=false;incident.ambientSource.spatialBlend=1;incident.ambientSource.minDistance=1;incident.ambientSource.maxDistance=14;incident.ambientSource.volume=.16f;
            incident.ambientSource.clip=kind==MaintenanceIncidentKind.Fire||kind==MaintenanceIncidentKind.PipeBurst?system.Hiss:system.FaultTone;incident.ambientSource.pitch=kind==MaintenanceIncidentKind.Fire?.55f:1;
            system.incidents.Add(incident);return incident;
        }
        static void Obstacle(GameObject root,BoxCollider collider)
        {
            var obstacle=root.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=collider.center;obstacle.size=collider.size;obstacle.carving=true;obstacle.carveOnlyStationary=true;
        }
    }
}
