using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace HotelTogether {
 [RequireComponent(typeof(CharacterController))] public class HotelPlayer:MonoBehaviour {
  public Camera view;public float speed=2.7f,sprint=4.4f,sensitivity=.12f;public HotelItem held,cart,seated;public string message;public int bellCount;
  CharacterController cc;Rigidbody anchor;FixedJoint grip;float yaw=180,pitch,vertical;Quaternion holdOffset;HotelItem focused;float messageUntil;Vector3 spawn;Quaternion startRotation;readonly List<Rigidbody> cargo=new();Transform hands;Font font;float fps;
  public HotelEmployeePlayer employee;public HotelCombat combat;public bool transportLocked;HotelLuggageTrolley activeTrolley;public HotelItem FocusedItem=>focused;
  Coroutine pickupReach;HotelItem pendingPickup;
  public bool IsReachingForItem=>pendingPickup!=null;
  public float ViewPitch=>pitch;
  void Start(){combat=GetComponent<HotelCombat>();cc=GetComponent<CharacterController>();Physics.IgnoreLayerCollision(8,9,true);spawn=transform.position;startRotation=transform.rotation;yaw=transform.eulerAngles.y;var hand=new GameObject("Physics grip anchor");anchor=hand.AddComponent<Rigidbody>();anchor.isKinematic=true;anchor.useGravity=false;eyeBase=standingEyeBase=view.transform.localPosition;if(employee)employee.Initialize(this);CreateHands();}
  public void Notify(string text){message=text;messageUntil=Time.time+3;}
  public void CaptureMouse(){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
  public void Face(float heading){yaw=heading;pitch=0;transform.rotation=Quaternion.Euler(0,yaw,0);view.transform.localRotation=Quaternion.identity;}
  void Update(){fps=Mathf.Lerp(fps,1/Mathf.Max(Time.unscaledDeltaTime,.001f),.04f);var k=Keyboard.current;var m=Mouse.current;if(k==null||m==null)return;if(k.escapeKey.wasPressedThisFrame){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}if(Cursor.lockState!=CursorLockMode.Locked)return;if(transportLocked||combat&&combat.IsDown)return;
   var delta=m.delta.ReadValue()*sensitivity;float newYaw=yaw+delta.x;if(cart)newYaw=Mathf.MoveTowardsAngle(yaw,newYaw,90*Time.deltaTime);pitch=Mathf.Clamp(pitch-delta.y,-80,88);
   float h=(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),v=(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0);Vector3 move=Quaternion.Euler(0,newYaw,0)*new Vector3(h,0,v).normalized*(k.leftShiftKey.isPressed?sprint:speed)*Time.deltaTime;
   if(cart&&!ClearCart(transform.position+move,newYaw)){move=Vector3.zero;newYaw=yaw;Notify("The cart is blocked.");}yaw=newYaw;transform.rotation=Quaternion.Euler(0,yaw,0);view.transform.localRotation=Quaternion.Euler(pitch,0,0);
   if(!seated){if(cc.isGrounded&&vertical<0)vertical=-2;vertical+=Physics.gravity.y*Time.deltaTime;cc.Move(move+Vector3.up*vertical*Time.deltaTime);}
   focused=null;if(Physics.Raycast(view.transform.position,view.transform.forward,out var hit,3.1f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Collide)){focused=hit.collider.GetComponentInParent<HotelItem>();if(!focused)focused=hit.collider.GetComponentInParent<DoorLeaf>()?.owner;}
   if(k.eKey.wasPressedThisFrame)Use();if(k.qKey.wasPressedThisFrame)Drop();if(k.spaceKey.wasPressedThisFrame&&held)Drop(true);if(k.rKey.wasPressedThisFrame)ResetWorld();

  }

    void Use(){
   if(IsReachingForItem||transportLocked)return;if(seated){Stand();return;}
   if(focused){
    var elevatorButton=focused.GetComponent<HotelElevatorButton>();if(elevatorButton){elevatorButton.Use(this);return;}
    var receiver=focused.GetComponent<HotelRoomDelivery>();if(receiver){receiver.Deliver(this);return;}
    var lift=focused.GetComponent<HotelServiceLift>();if(lift){lift.Use(this);return;}
    var washer=focused.GetComponent<HotelLaundryMachine>();if(washer){washer.Use(this);return;}
    var trolley=focused.GetComponent<HotelLuggageTrolley>();if(trolley&&held){trolley.Load(this);return;}
    var tray=held?held.GetComponent<HotelServiceTray>():null;var supply=focused.GetComponent<HotelGuestSupply>();if(tray&&supply){tray.Load(supply,this);return;}
   }
   if(focused&&focused.kind=="door"){var defense=focused.GetComponent<HotelDefenseOpening>();if(defense&&defense.BoardCount>0){Notify("N to remove planks before opening this door.");return;}if(defense&&defense.Broken){Notify("T to repair this broken opening.");return;}var entrance=focused.GetComponent<HotelAutomaticEntrance>();if(entrance){entrance.RequestOpen(true);Notify("Opening "+ItemName(focused));}else{focused.open=!focused.open;Notify((focused.open?"Opened ":"Closed ")+ItemName(focused));}return;}
   if(cart){EndCart();return;}
   if(focused&&focused.kind=="barricade"){focused.GetComponent<HotelDefenseOpening>()?.AddBoard(combat);return;}
   if(focused&&focused.kind=="defenseStock"){focused.GetComponent<HotelDefenseStock>()?.Collect(combat);return;}
   if(focused&&focused.kind=="guest"){focused.GetComponent<HotelGuestBot>()?.Serve(this);return;}
   if(held){Drop();return;}if(!focused)return;
   switch(focused.kind){case "pickup":case "mop":RequestPickup(focused);break;case "cart":BeginCart(focused);break;case "seat":Sit(focused);break;case "lamp":focused.lampOn=!focused.lampOn;if(focused.lamp)focused.lamp.enabled=focused.lampOn;Notify(ItemName(focused)+(focused.lampOn?" is on!":" is off!"));break;case "bell":Bell();break;case "water":if(focused.waterFixture){focused.waterFixture.Toggle();Notify(ItemName(focused)+(focused.waterFixture.IsRunning?" is running.":" is off."));}break;default:Notify("This item is decorative.");break;}
  }
public bool RequestPickup(HotelItem item){
   if(!item||held||cart||seated||pendingPickup||(item.kind!="pickup"&&item.kind!="mop"))return false;
   if(!employee||!employee.Ready){PickUp(item);return true;}
   Vector3 point=item.transform.position;var renderers=item.GetComponentsInChildren<Renderer>();if(renderers.Length>0)point=renderers[0].bounds.ClosestPoint(view.transform.position);
   if(Vector3.Distance(view.transform.position,point)>3.1f)return false;
   pendingPickup=item;employee.Avatar.BeginReach(item.transform,point);pickupReach=StartCoroutine(ReachAndPickUp(item));return true;
  }
  System.Collections.IEnumerator ReachAndPickUp(HotelItem item){
   yield return new WaitForSeconds(.24f);
   if(item&&Cursor.lockState==CursorLockMode.Locked&&!held&&!cart&&!seated&&Vector3.Distance(view.transform.position,item.transform.position)<3.4f)PickUp(item);
   pendingPickup=null;pickupReach=null;if(employee&&employee.Avatar)employee.Avatar.EndReach();
  }
  void CancelPickupReach(){if(pickupReach!=null)StopCoroutine(pickupReach);pickupReach=null;pendingPickup=null;if(employee&&employee.Avatar)employee.Avatar.EndReach();}
  [Header("Pickup and throw physics")]
  [Range(3,12)]public float throwSpeed=6.8f;
  [Range(60,250)]public float carrySpring=150;
  [Range(12,35)]public float carryDamping=24;
  Rigidbody heldBody;bool restoreGravity;float restoreDamping,restoreAngularDamping;Vector3 carryCenter;float carryRadius;Renderer[] carryRenderers;int[] heldLayers;Transform[] heldTransforms;float throwGesture;
  Quaternion CarryRotation()=>Quaternion.Euler(held&&held.GetComponent<HotelServiceTray>()?0:Mathf.Clamp(pitch*.38f,-24,24),yaw,0)*(combat?combat.SwingOffset:Quaternion.identity);
  Vector3 CarryCenterTarget(){
   var tray=held&&held.GetComponent<HotelServiceTray>();var local=tray?new Vector3(0,-.43f,.78f):new Vector3(0,-.23f,itemHoldDistance);var target=view.transform.TransformPoint(local);var direction=(target-view.transform.position).normalized;float distance=local.magnitude;
   if(Physics.SphereCast(view.transform.position,carryRadius,direction,out var hit,distance,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore))target=view.transform.position+direction*Mathf.Max(.38f,hit.distance-.04f);
   return target;
  }
  public void ApplyElevatorMotion(Vector3 delta){if(delta.sqrMagnitude==0)return;var controller=GetComponent<CharacterController>();bool wasEnabled=controller.enabled;controller.enabled=false;transform.position+=delta;controller.enabled=wasEnabled;if(held){var body=held.GetComponent<Rigidbody>();body.position+=delta;held.transform.position=body.position;}if(cart){var body=cart.GetComponent<Rigidbody>();body.position+=delta;cart.transform.position=body.position;}if(anchor)anchor.position+=delta;}
  void FixedUpdate(){if(transportLocked)return;
   if(held&&heldBody){
    var rotation=CarryRotation();var target=CarryCenterTarget()-rotation*carryCenter;
    var error=target-heldBody.position;if(error.sqrMagnitude>9){Drop();return;}
    var acceleration=error*carrySpring-heldBody.linearVelocity*carryDamping;
    heldBody.AddForce(Vector3.ClampMagnitude(acceleration,130),ForceMode.Acceleration);
    var delta=rotation*Quaternion.Inverse(heldBody.rotation);delta.ToAngleAxis(out float angle,out var axis);if(angle>180)angle-=360;
    if(float.IsFinite(axis.x)&&axis.sqrMagnitude>.01f){var desired=axis.normalized*Mathf.Clamp(angle*Mathf.Deg2Rad*12,-12,12);heldBody.AddTorque((desired-heldBody.angularVelocity)*18,ForceMode.Acceleration);}
    anchor.position=target;anchor.rotation=rotation;
   }
   if(cart){var rb=cart.GetComponent<Rigidbody>();rb.MovePosition(CartPosition(transform.position,yaw));rb.MoveRotation(Quaternion.Euler(0,yaw-90,0));}
  }
  void PickUp(HotelItem item){
   item.GetComponent<HotelGuestDelivery>()?.Detach();if(held)Drop();held=item;heldBody=item.GetComponent<Rigidbody>();if(!heldBody)heldBody=item.gameObject.AddComponent<Rigidbody>();
   var rb=heldBody;restoreGravity=rb.useGravity;restoreDamping=rb.linearDamping;restoreAngularDamping=rb.angularDamping;
   rb.isKinematic=false;rb.useGravity=false;rb.mass=Mathf.Max(.15f,rb.mass);rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.maxAngularVelocity=14;rb.linearDamping=.08f;rb.angularDamping=.1f;rb.linearVelocity=rb.angularVelocity=Vector3.zero;
   heldTransforms=item.GetComponentsInChildren<Transform>();heldLayers=new int[heldTransforms.Length];for(int i=0;i<heldTransforms.Length;i++){heldLayers[i]=heldTransforms[i].gameObject.layer;heldTransforms[i].gameObject.layer=9;}
   var rotation=CarryRotation();var position=rb.position;rb.rotation=rotation;item.transform.SetPositionAndRotation(position,rotation);Physics.SyncTransforms();
   carryRenderers=item.GetComponentsInChildren<Renderer>();bool found=false;var bounds=new Bounds(item.transform.position,item.dimensions);
   foreach(var renderer in carryRenderers){if(!renderer.enabled)continue;var local=renderer.localBounds;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var point=renderer.transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z)));if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);}}
   carryCenter=Quaternion.Inverse(rotation)*(bounds.center-rb.position);carryRadius=Mathf.Clamp(Mathf.Min(bounds.extents.x,bounds.extents.y),.035f,.22f);
   var target=CarryCenterTarget()-rotation*carryCenter;rb.position=target;item.transform.SetPositionAndRotation(target,rotation);anchor.position=target;anchor.rotation=rotation;holdOffset=Quaternion.identity;rb.WakeUp();Physics.SyncTransforms();
   handBoundsOwner=null;Notify("Q  Put down | SPACE  Throw");
  }
  public void Drop(bool throwIt=false){
   CancelPickupReach();if(!held)return;var rb=heldBody?heldBody:held.GetComponent<Rigidbody>();
   if(grip){Destroy(grip);grip=null;}
   if(heldTransforms!=null){for(int i=0;i<heldTransforms.Length;i++)if(heldTransforms[i])heldTransforms[i].gameObject.layer=heldLayers[i];}else SetLayer(held.transform,0);
   rb.isKinematic=false;rb.useGravity=restoreGravity;rb.linearDamping=restoreDamping;rb.angularDamping=restoreAngularDamping;rb.WakeUp();
   if(throwIt){var movement=cc&&cc.enabled?new Vector3(cc.velocity.x,0,cc.velocity.z):Vector3.zero;rb.linearVelocity=movement*.55f;rb.AddForce(view.transform.forward*throwSpeed+Vector3.up*.9f,ForceMode.VelocityChange);rb.AddTorque(view.transform.right*3.2f+view.transform.up*1.1f,ForceMode.VelocityChange);throwGesture=1;if(employee&&employee.Avatar)employee.Avatar.ThrowGesture();}
   held=null;heldBody=null;heldTransforms=null;heldLayers=null;
  }

  static void SetLayer(Transform t,int layer){t.gameObject.layer=layer;foreach(Transform c in t)SetLayer(c,layer);}
  Vector3 CartPosition(Vector3 player,float heading)=>activeTrolley?activeTrolley.PositionFor(player,heading):player+Quaternion.Euler(0,heading,0)*Vector3.forward*1.12f+Vector3.up*(cart.dimensions.y*.5f);
  bool ClearCart(Vector3 pos,float heading){if(!cart)return true;var desired=CartPosition(pos,heading);var rot=Quaternion.Euler(0,heading-90,0);int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(cart.transform.position,desired)/.1f),Mathf.CeilToInt(Quaternion.Angle(cart.transform.rotation,rot)/8));var colliders=cart.GetComponentsInChildren<BoxCollider>();for(int i=1;i<=steps;i++){var p=Vector3.Lerp(cart.transform.position,desired,(float)i/steps);var q=Quaternion.Slerp(cart.transform.rotation,rot,(float)i/steps);foreach(var box in colliders){if(box.transform.GetComponentInParent<HotelItem>()!=cart)continue;var local=cart.transform.InverseTransformPoint(box.transform.TransformPoint(box.center));var half=Vector3.Scale(box.size,box.transform.lossyScale)*.5f-Vector3.one*.018f;half=Vector3.Max(half,Vector3.one*.005f);var relative=Quaternion.Inverse(cart.transform.rotation)*box.transform.rotation;foreach(var c in Physics.OverlapBox(p+q*local,half,q*relative,~(1<<8),QueryTriggerInteraction.Ignore)){if(c.transform.IsChildOf(cart.transform)||c.transform.IsChildOf(transform))continue;return false;}}}return true;}
  public void BeginCart(HotelItem o){cart=o;activeTrolley=o.GetComponent<HotelLuggageTrolley>();if(activeTrolley)activeTrolley.SetPushing(true);var rb=o.GetComponent<Rigidbody>();if(!rb)rb=o.gameObject.AddComponent<Rigidbody>();foreach(var a in FindObjectsByType<HotelItem>(FindObjectsSortMode.None)){var r=a.GetComponent<Rigidbody>();if(!r||r==rb||r.isKinematic)continue;float bottom=a.transform.position.y-a.dimensions.y/2;float baseY=o.transform.position.y-o.dimensions.y/2;if(Vector2.Distance(new Vector2(a.transform.position.x,a.transform.position.z),new Vector2(o.transform.position.x,o.transform.position.z))<.5f&&bottom>baseY+.27f&&bottom<baseY+.5f){r.isKinematic=true;a.transform.SetParent(o.transform,true);cargo.Add(r);}}
   if(!ClearCart(transform.position,yaw)){EndCart();Notify("Not enough room. Move back and try again!");return;}rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;rb.isKinematic=true;rb.position=CartPosition(transform.position,yaw);rb.rotation=Quaternion.Euler(0,yaw-90,0);if(hands)hands.gameObject.SetActive(true);UpdateHands();Notify("E  Release cart");}
  public void EndCart(){if(!cart)return;if(activeTrolley)activeTrolley.SetPushing(false);var rb=cart.GetComponent<Rigidbody>();rb.isKinematic=false;rb.linearVelocity=Vector3.zero;foreach(var r in cargo){r.transform.SetParent(cart.transform.parent,true);r.isKinematic=false;}cargo.Clear();cart=null;activeTrolley=null;if(hands)hands.gameObject.SetActive(false);}
  void Sit(HotelItem o){var guests=FindFirstObjectByType<HotelGuestSimulation>();if(guests&&guests.IsSeatOccupied(o)){Notify("This seat is reserved for a guest.");return;}seated=o;cc.enabled=false;transform.position=new Vector3(o.transform.position.x,o.home.y-o.dimensions.y/2+.025f,o.transform.position.z);eyeBase=new Vector3(standingEyeBase.x,o.seatEyeHeight,standingEyeBase.z);view.transform.localPosition=eyeBase;smoothedBob=Vector3.zero;bobWeight=0;Notify("E  Stand up");}
  void Stand(){var s=seated;if(!s)return;Vector3 target=s.transform.position+s.transform.forward*(s.dimensions.z/2+.75f);target.y=transform.position.y;if(Physics.CheckCapsule(target+Vector3.up*.35f,target+Vector3.up*1.35f,.28f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore)){target=s.transform.position-s.transform.forward*(s.dimensions.z/2+.75f);target.y=transform.position.y;}transform.position=target;eyeBase=standingEyeBase;view.transform.localPosition=eyeBase;smoothedBob=Vector3.zero;bobWeight=0;cc.enabled=true;vertical=0;seated=null;}
  public void ResetWorld(){transportLocked=false;foreach(var elevator in FindObjectsByType<HotelElevator>(FindObjectsSortMode.None))elevator.ResetElevator();foreach(var lift in FindObjectsByType<HotelServiceLift>(FindObjectsSortMode.None))lift.ResetLift();Drop();EndCart();seated=null;cc.enabled=false;transform.SetPositionAndRotation(spawn,startRotation);eyeBase=standingEyeBase;view.transform.localPosition=eyeBase;smoothedBob=Vector3.zero;bobWeight=0;pitch=0;yaw=startRotation.eulerAngles.y;cc.enabled=true;foreach(var o in FindObjectsByType<HotelItem>(FindObjectsSortMode.None))o.ResetItem();Physics.SyncTransforms();FindFirstObjectByType<HotelGuestSimulation>()?.ResetSimulation();FindFirstObjectByType<HotelZombieDirector>()?.ResetDefense();Notify("Fresh start!");}
  void Bell(){bellCount++;var clip=AudioClip.Create("Reception bell",24000,1,24000,false);var samples=new float[24000];for(int i=0;i<samples.Length;i++){float t=i/24000f;samples[i]=Mathf.Sin(2*Mathf.PI*1568*t)*.15f*Mathf.Exp(-t*7);}clip.SetData(samples,0);AudioSource.PlayClipAtPoint(clip,focused.transform.position,.7f);Destroy(clip,2);Notify("Ding! Someone will be right with you.");}
  [Header("First-person feel")]
  public bool walkingCameraBob=true;
  [Range(0,.04f)]public float walkingBobAmount=.014f;
  [Range(.8f,2.5f)]public float walkingBobFrequency=1.65f;
  [Range(.45f,1.4f)]public float itemHoldDistance=.85f;
  public Color handSkinColor=new Color(.89f,.64f,.44f);
  Vector3 eyeBase,standingEyeBase;float bobPhase,bobWeight;Vector3 smoothedBob;
  Material handSkin,handSleeve;readonly List<Mesh> handMeshes=new();
  HandRig[] handRigs;HotelItem handBoundsOwner;Renderer[] handBoundsRenderers=System.Array.Empty<Renderer>();Collider[] handGripColliders=System.Array.Empty<Collider>();Renderer handShaftRenderer;
  sealed class HandRig {public Transform palm,model;public Mesh mesh;public Vector3[] vertices,normals;public float morph;public float weight;public Vector3 target;public Quaternion rotation;public float side;}

  void LateUpdate(){if(!view||!cc)return;UpdateCameraMotion(Time.deltaTime);if(employee&&employee.Ready)employee.UpdatePresentation(Time.deltaTime);UpdateHands();if(employee&&employee.Avatar)employee.Avatar.SetInteractionArmsHidden(hands&&hands.gameObject.activeSelf);}
  void UpdateCameraMotion(float dt){
   dt=Mathf.Min(dt,.05f);var velocity=cc.enabled?cc.velocity:Vector3.zero;float horizontal=new Vector2(velocity.x,velocity.z).magnitude;
   bool moving=walkingCameraBob&&cc.enabled&&cc.isGrounded&&!seated&&Cursor.lockState==CursorLockMode.Locked&&horizontal>.12f;
   bobWeight=Mathf.MoveTowards(bobWeight,moving?Mathf.Clamp01(horizontal/Mathf.Max(speed,.1f)):0,dt*5);
   if(moving)bobPhase+=dt*walkingBobFrequency*Mathf.PI*2*Mathf.Clamp(horizontal/Mathf.Max(speed,.1f),.4f,1.45f);
   float carry=held ? .75f : cart ? .65f : 1;float amount=walkingBobAmount*bobWeight*carry;
   var target=new Vector3(Mathf.Sin(bobPhase*.5f)*amount*.4f,Mathf.Sin(bobPhase)*amount,0);
   smoothedBob=Vector3.Lerp(smoothedBob,target,1-Mathf.Exp(-dt*14));
   if(bobWeight<.001f&&smoothedBob.sqrMagnitude<.00000001f)smoothedBob=Vector3.zero;
   view.transform.localPosition=eyeBase+smoothedBob;
   view.transform.localRotation=Quaternion.Euler(pitch,0,Mathf.Sin(bobPhase*.5f)*.18f*bobWeight*carry);
  }
  Material HandMaterial(string name,Color color){
   var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)shader=Shader.Find("Standard");
   var material=new Material(shader){name=name,color=color};if(material.HasProperty("_ReceiveShadows")){material.SetFloat("_ReceiveShadows",0);material.EnableKeyword("_RECEIVE_SHADOWS_OFF");}if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.23f);return material;
  }
  Transform HandPrimitive(string name,PrimitiveType shape,Transform parent,Vector3 position,Vector3 scale,Material material){
   var go=GameObject.CreatePrimitive(shape);go.name=name;go.layer=8;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
   var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
   var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
   return go.transform;
  }
  Mesh referenceHandMesh;Vector3[] openHandVertices,closedHandVertices,openHandNormals,closedHandNormals;Bounds handBounds;int[] wristSeam;Vector3 wristCenter;const int armRings=14;
  bool LoadReferenceHand(){
   var asset=Resources.Load<TextAsset>("HotelTogetherHands/HotelBulkyHand");if(!asset)return false;
   using(var reader=new System.IO.BinaryReader(new System.IO.MemoryStream(asset.bytes))){
    if(reader.ReadInt32()!=0x48544C48||reader.ReadInt32()!=3)return false;int count=reader.ReadInt32();
    Vector3[] Read(){var data=new Vector3[count];for(int i=0;i<count;i++)data[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());return data;}
    openHandVertices=Read();closedHandVertices=Read();openHandNormals=Read();closedHandNormals=Read();var triangles=new int[reader.ReadInt32()];for(int i=0;i<triangles.Length;i++)triangles[i]=reader.ReadInt32();
    wristSeam=new int[reader.ReadInt32()];for(int i=0;i<wristSeam.Length;i++)wristSeam[i]=reader.ReadInt32();wristCenter=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
    referenceHandMesh=new Mesh{name="Bulky cartoon hand silhouette"};referenceHandMesh.vertices=openHandVertices;referenceHandMesh.normals=openHandNormals;referenceHandMesh.triangles=triangles;referenceHandMesh.RecalculateBounds();handBounds=referenceHandMesh.bounds;foreach(var point in closedHandVertices)handBounds.Encapsulate(point);referenceHandMesh.bounds=handBounds;
   }return true;
  }
  void CreateHands(){
   if(!LoadReferenceHand()){Debug.LogError("Install the HotelBulkyHand.bytes hand asset in Resources/HotelTogetherHands.");return;}
   hands=new GameObject("Continuous cartoon hands and arms").transform;hands.SetParent(transform,false);hands.gameObject.layer=8;
   handSkin=HandMaterial("Warm cartoon skin",handSkinColor);handRigs=new HandRig[2];
   for(int i=0;i<2;i++){
    float side=i==0?-1:1;var palm=new GameObject(i==0?"Left bulky hand":"Right bulky hand").transform;palm.SetParent(hands,false);palm.gameObject.layer=8;
    var rig=new HandRig{side=side,palm=palm,rotation=Quaternion.identity,morph=-1};handRigs[i]=rig;
    var model=new GameObject("One seamless hand and arm",typeof(MeshFilter),typeof(MeshRenderer)).transform;model.SetParent(palm,false);model.localScale=new Vector3(side,1,1);model.gameObject.layer=8;rig.model=model;
    rig.mesh=new Mesh{name="Continuous skin mesh "+i};rig.mesh.MarkDynamic();handMeshes.Add(rig.mesh);int total=openHandVertices.Length+armRings*wristSeam.Length+1;rig.vertices=new Vector3[total];rig.normals=new Vector3[total];model.GetComponent<MeshFilter>().sharedMesh=rig.mesh;
    var triangles=new List<int>(referenceHandMesh.triangles);int RingIndex(int ring,int s)=>ring==0?wristSeam[s]:openHandVertices.Length+(ring-1)*wristSeam.Length+s;
    for(int ring=0;ring<armRings;ring++)for(int s=0;s<wristSeam.Length;s++){int a=RingIndex(ring,s),b=RingIndex(ring,(s+1)%wristSeam.Length),c=RingIndex(ring+1,s),d=RingIndex(ring+1,(s+1)%wristSeam.Length);triangles.AddRange(new[]{a,c,b,b,c,d});}
    for(int s=0;s<wristSeam.Length;s++)triangles.AddRange(new[]{total-1,RingIndex(armRings,s),RingIndex(armRings,(s+1)%wristSeam.Length)});
    rig.mesh.vertices=rig.vertices;rig.mesh.SetTriangles(triangles,0);
    var renderer=model.GetComponent<MeshRenderer>();renderer.sharedMaterial=handSkin;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
    AnimateFingers(rig,0);UpdateContinuousArm(rig);palm.gameObject.SetActive(false);
   }if(hands)hands.gameObject.SetActive(false);
  }
  void AnimateFingers(HandRig rig,float curl){
   curl=Mathf.Clamp01(curl);if(Mathf.Abs(rig.morph-curl)<.002f)return;rig.morph=curl;
   for(int i=0;i<openHandVertices.Length;i++){rig.vertices[i]=Vector3.Lerp(openHandVertices[i],closedHandVertices[i],curl);rig.normals[i]=Vector3.Lerp(openHandNormals[i],closedHandNormals[i],curl).normalized;}
  }
  void UpdateContinuousArm(HandRig rig){
   var body=view.transform.TransformPoint(new Vector3(rig.side*.36f,-.90f,.12f));var end=rig.model.InverseTransformPoint(body);
   var a=wristCenter;var b=a+Vector3.back*.12f;var c=end+rig.model.InverseTransformVector(view.transform.up*.27f+view.transform.forward*.08f);var d=end;
   Vector3 Point(float t){float u=1-t;return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;}
   for(int ring=1;ring<=armRings;ring++){
    float t=(float)ring/armRings;var center=Point(t);var tangent=(Point(Mathf.Min(1,t+.003f))-Point(Mathf.Max(0,t-.003f))).normalized;var rotation=Quaternion.FromToRotation(Vector3.back,tangent);float thickness=Mathf.Lerp(1,1.72f,Mathf.SmoothStep(0,1,t));
    for(int s=0;s<wristSeam.Length;s++){var radial=closedHandVertices[wristSeam[s]]-wristCenter;radial.z=0;int index=openHandVertices.Length+(ring-1)*wristSeam.Length+s;rig.vertices[index]=center+rotation*radial*thickness;rig.normals[index]=rotation*radial.normalized;}
   }
   rig.vertices[rig.vertices.Length-1]=end;rig.normals[rig.normals.Length-1]=(end-c).normalized;
   rig.mesh.vertices=rig.vertices;rig.mesh.normals=rig.normals;rig.mesh.RecalculateBounds();
  }
  void Segment(Transform t,Vector3 a,Vector3 b,float radius){t.position=(a+b)*.5f;t.rotation=Quaternion.FromToRotation(Vector3.up,b-a);t.localScale=new Vector3(radius*2,Mathf.Max(.01f,(b-a).magnitude)*.5f,radius*2);}
  Bounds HeldViewBounds(){
   bool found=false;var result=new Bounds();
   foreach(var renderer in handBoundsRenderers){
    if(!renderer.enabled)continue;var b=renderer.localBounds;
    for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var p=view.transform.InverseTransformPoint(renderer.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));if(!found){result=new Bounds(p,Vector3.zero);found=true;}else result.Encapsulate(p);}
   }
   if(!found)result=new Bounds(view.transform.InverseTransformPoint(held.transform.position),held.dimensions);return result;
  }
  Vector3 GripPoint(float side,Bounds bounds){
   if(handShaftRenderer){var center=handShaftRenderer.transform.TransformPoint(handShaftRenderer.localBounds.center);return center+view.transform.up*(side<0 ? .09f : -.09f)+view.transform.right*side*.022f-view.transform.forward*.016f;}
   var local=new Vector3((side<0?bounds.min.x:bounds.max.x)-side*.006f,bounds.center.y-Mathf.Min(.10f,bounds.size.y*.17f),bounds.min.z+.016f);
   var surface=view.transform.TransformPoint(local);var outward=(view.transform.right*side*.72f-view.transform.forward*.69f).normalized;var query=surface+outward*.35f;float distance=float.PositiveInfinity;
   foreach(var collider in handGripColliders){
    if(!collider.enabled||collider.isTrigger)continue;
    if(collider is MeshCollider mesh&&!mesh.convex)continue;
    var point=collider.ClosestPoint(query);float d=(point-query).sqrMagnitude;if(d<distance){distance=d;surface=point;}
   }
   return surface+outward*.012f;
  }
  void UpdateHands(){
   if(handRigs==null||!view)return;
   if(handSkin)handSkin.color=handSkinColor;
   if(held!=handBoundsOwner){handBoundsOwner=held;handBoundsRenderers=held?held.GetComponentsInChildren<Renderer>():System.Array.Empty<Renderer>();handGripColliders=held?held.GetComponentsInChildren<Collider>():System.Array.Empty<Collider>();handShaftRenderer=System.Array.Find(handBoundsRenderers,r=>r.name.Contains("Mop_Shaft"));}
   float dt=Mathf.Clamp(Time.deltaTime,1f/120,.05f);throwGesture=Mathf.MoveTowards(throwGesture,0,dt*5);var bounds=held?HeldViewBounds():new Bounds();
   bool twoHands=held&&(bounds.size.x>.27f||bounds.size.y>.43f||held.kind=="mop");bool showing=false;
   foreach(var rig in handRigs){
    bool active=cart||(pendingPickup&&rig.side>0)||(held&&bounds.center.magnitude<1.8f&&(rig.side>0||twoHands));rig.weight=Mathf.MoveTowards(rig.weight,active?1:0,dt*(active?6:5));
    bool visible=rig.weight>.001f;showing|=visible;rig.palm.gameObject.SetActive(visible);if(!visible)continue;
    if(cart){
     var handle=activeTrolley?(rig.side<0?activeTrolley.leftHandle.position:activeTrolley.rightHandle.position):cart.transform.TransformPoint(new Vector3(-.46f,-cart.dimensions.y/2+1.08f,-rig.side*.16f));rig.target=handle-cart.transform.right*.04f-cart.transform.up*.025f;rig.rotation=Quaternion.LookRotation(cart.transform.up,cart.transform.right);
    }else if(held){
     rig.target=GripPoint(rig.side,bounds);var inward=(-view.transform.right*rig.side*.72f+view.transform.forward*.69f).normalized;rig.rotation=Quaternion.LookRotation(view.transform.up,inward);
    }else if(pendingPickup){
     var surface=pendingPickup.GetComponentInChildren<Renderer>();rig.target=surface?surface.bounds.ClosestPoint(view.transform.position):pendingPickup.transform.position;rig.rotation=view.transform.rotation*Quaternion.Euler(55,rig.side*15,0);
    }
    var rest=view.transform.TransformPoint(new Vector3(rig.side*.32f,-.54f,.14f));float reach=Mathf.SmoothStep(0,1,rig.weight);
    var gesture=view.transform.forward*(Mathf.Sin(throwGesture*Mathf.PI)*.13f);rig.palm.position=Vector3.Lerp(rest,rig.target+gesture,reach);rig.palm.rotation=Quaternion.Slerp(view.transform.rotation*Quaternion.Euler(55,rig.side*15,0),rig.rotation,reach);
    rig.palm.localScale=Vector3.one;float curl=cart||handShaftRenderer||(held&&!twoHands)?1:.47f;AnimateFingers(rig,curl*Mathf.SmoothStep(0,1,Mathf.Clamp01((rig.weight-.15f)/.85f)));
    UpdateContinuousArm(rig);
   }
   hands.gameObject.SetActive(showing);
  }

  public bool TryGetEmployeeGripPoints(out Vector3 left,out Vector3 right,out bool twoHands){
   left=right=Vector3.zero;twoHands=false;
   if(cart){left=activeTrolley?activeTrolley.leftHandle.position:cart.transform.TransformPoint(new Vector3(-.46f,-cart.dimensions.y/2+1.08f,.16f))-cart.transform.right*.04f-cart.transform.up*.025f;right=activeTrolley?activeTrolley.rightHandle.position:cart.transform.TransformPoint(new Vector3(-.46f,-cart.dimensions.y/2+1.08f,-.16f))-cart.transform.right*.04f-cart.transform.up*.025f;twoHands=true;return true;}
   if(!held)return false;
   if(held!=handBoundsOwner){handBoundsOwner=held;handBoundsRenderers=held.GetComponentsInChildren<Renderer>();handGripColliders=held.GetComponentsInChildren<Collider>();handShaftRenderer=System.Array.Find(handBoundsRenderers,r=>r.name.Contains("Mop_Shaft"));}
   var b=HeldViewBounds();twoHands=b.size.x>.27f||b.size.y>.43f||held.kind=="mop";left=GripPoint(-1,b);right=GripPoint(1,b);return true;
  }

  // Compact, contextual UI inspired by the game's bright rounded icon style.
  readonly Color uiBrown=new Color(.36f,.20f,.13f),uiCream=new Color(1,.94f,.79f),uiCreamBottom=new Color(1,.84f,.59f),uiBlue=new Color(.22f,.84f,.97f),uiBlueBottom=new Color(.02f,.52f,.82f),uiOrange=new Color(1,.66f,.28f),uiOrangeBottom=new Color(.98f,.37f,.12f);
  readonly Dictionary<string,Texture2D> uiSurfaces=new();
  GUIStyle uiTitle,uiAction,uiSmall,uiKey,uiToast,uiBrand,uiHeading,uiButton,uiSecondary;
  float uiOpacity;string uiLastName,uiLastAction,uiLastDoor;bool uiLastHolding;bool uiOwnFont;
  readonly GUIContent uiNameContent=new(),uiActionContent=new(),uiKeyContent=new();

  void PrepareInterface(){
   if(uiTitle!=null&&font){RefreshUiStyles();return;}
   var bundled=Resources.Load<Font>("HotelTogetherUI/LilitaOne-Regular");
   if(uiOwnFont&&font)Destroy(font);
   font=bundled?bundled:Font.CreateDynamicFontFromOSFont(new[]{"Comic Sans MS","Arial Rounded MT Bold","Arial"},30);uiOwnFont=!bundled;
   GUIStyle Style(int size,Color color,TextAnchor alignment=TextAnchor.MiddleLeft){return new GUIStyle{font=font,fontSize=size,fontStyle=bundled?FontStyle.Normal:FontStyle.Bold,alignment=alignment,clipping=TextClipping.Clip,normal={textColor=color},hover={textColor=color},active={textColor=color},focused={textColor=color},padding=new RectOffset(0,0,0,0)};}
   uiTitle=Style(30,uiBrown);uiAction=Style(21,Color.white,TextAnchor.MiddleCenter);
   uiSmall=Style(18,uiBrown,TextAnchor.MiddleCenter);uiKey=Style(35,Color.white,TextAnchor.MiddleCenter);
   uiToast=Style(22,uiBrown,TextAnchor.MiddleCenter);uiToast.wordWrap=true;
   uiBrand=Style(18,Color.white,TextAnchor.MiddleCenter);uiHeading=Style(50,Color.white,TextAnchor.MiddleCenter);
   uiButton=Style(30,Color.white,TextAnchor.MiddleCenter);uiSecondary=Style(26,Color.white,TextAnchor.MiddleCenter);
   RefreshUiStyles();
  }
  void RefreshUiStyles(){
   void Apply(GUIStyle s,int size,Color c,TextAnchor align){s.font=font;s.fontSize=size;s.fontStyle=uiOwnFont?FontStyle.Bold:FontStyle.Normal;s.alignment=align;s.normal.textColor=c;s.hover.textColor=c;s.active.textColor=c;s.focused.textColor=c;}
   Apply(uiTitle,30,uiBrown,TextAnchor.MiddleLeft);Apply(uiAction,21,Color.white,TextAnchor.MiddleCenter);Apply(uiSmall,18,uiBrown,TextAnchor.MiddleCenter);Apply(uiKey,35,Color.white,TextAnchor.MiddleCenter);Apply(uiToast,22,uiBrown,TextAnchor.MiddleCenter);Apply(uiBrand,18,Color.white,TextAnchor.MiddleCenter);Apply(uiHeading,50,Color.white,TextAnchor.MiddleCenter);Apply(uiButton,30,Color.white,TextAnchor.MiddleCenter);Apply(uiSecondary,26,Color.white,TextAnchor.MiddleCenter);
  }
  static Rect Inset(Rect r,float n)=>new Rect(r.x+n,r.y+n,r.width-n*2,r.height-n*2);
  void Panel(Rect r,Color top,Color bottom,float radius=17){
   int w=Mathf.Max(1,Mathf.CeilToInt(r.width)),h=Mathf.Max(1,Mathf.CeilToInt(r.height));radius=Mathf.Min(radius,Mathf.Min(w,h)*.5f);
   string id=w+":"+h+":"+((Color32)top).GetHashCode()+":"+((Color32)bottom).GetHashCode()+":"+radius;
   if(!uiSurfaces.TryGetValue(id,out var texture)){
    texture=new Texture2D(w,h,TextureFormat.RGBA32,false){name="Hotel cartoon UI surface",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
    var pixels=new Color32[w*h];
    for(int y=0;y<h;y++){var c=Color.Lerp(bottom,top,(float)y/Mathf.Max(1,h-1));for(int x=0;x<w;x++){
     float dx=Mathf.Max(Mathf.Abs(x+.5f-w*.5f)-(w*.5f-radius),0),dy=Mathf.Max(Mathf.Abs(y+.5f-h*.5f)-(h*.5f-radius),0);
     var p=c;p.a*=Mathf.Clamp01(radius+.5f-Mathf.Sqrt(dx*dx+dy*dy));pixels[y*w+x]=p;
    }}texture.SetPixels32(pixels);texture.Apply(false,true);uiSurfaces[id]=texture;
   }
   var old=GUI.color;GUI.color=new Color(1,1,1,GUI.contentColor.a);GUI.DrawTexture(r,texture);GUI.color=old;
  }
  void Panel(Rect r,Color color,float radius=17)=>Panel(r,color,color,radius);
  void Rule(Rect r,Color color){var old=GUI.color;color.a*=GUI.contentColor.a;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
  void Bubble(Rect r,Color top,Color bottom,float radius=18,float depth=5){
   Panel(new Rect(r.x,r.y+depth,r.width,r.height),new Color(uiBrown.r*.75f,uiBrown.g*.75f,uiBrown.b*.75f,.8f),radius);
   Panel(r,uiBrown,radius);Panel(Inset(r,3),top,bottom,Mathf.Max(3,radius-3));
   Panel(new Rect(r.x+12,r.y+5,Mathf.Max(1,r.width-24),3),new Color(1,1,1,.55f),1.5f);
  }
  void Text(Rect r,string text,GUIStyle style,Color outline,float stroke=1.3f){
   // Distinct GUIContent objects keep changing labels and key caps independent.
   var content=new GUIContent(text);var old=GUI.contentColor;
   GUI.contentColor=new Color(outline.r,outline.g,outline.b,old.a*outline.a);
   GUI.Label(new Rect(r.x-stroke,r.y,r.width,r.height),content,style);GUI.Label(new Rect(r.x+stroke,r.y,r.width,r.height),content,style);
   GUI.Label(new Rect(r.x,r.y-stroke,r.width,r.height),content,style);GUI.Label(new Rect(r.x,r.y+stroke,r.width,r.height),content,style);
   GUI.contentColor=old;GUI.Label(r,content,style);
  }
  static bool HasInteraction(HotelItem item){if(!item)return false;switch(item.kind){case "door":return item.leaves!=null&&item.leaves.Length>0&&item.door!=null;case "lamp":return item.lamp!=null;case "water":return item.waterFixture!=null;case "pickup":case "mop":case "cart":case "seat":case "bell":return true;case "guest":return item.GetComponent<HotelGuestBot>()!=null;case "roomDelivery":case "laundry":case "serviceLift":case "barricade":case "defenseStock":return true;default:return false;}}
  string ItemAction(HotelItem item){var elevatorButton=item.GetComponent<HotelElevatorButton>();if(elevatorButton)return elevatorButton.ActionLabel;switch(item.kind){case "roomDelivery":return "DELIVER TAGGED ITEMS";case "laundry":return item.GetComponent<HotelLaundryMachine>().Ready?"COLLECT CLEAN LINEN":"LOAD DIRTY LINEN";case "serviceLift":return "USE SERVICE LIFT";case "barricade":return "BUILD BARRICADE";case "defenseStock":return "COLLECT SUPPLIES";case "guest":return item.GetComponent<HotelGuestBot>()?.ActionLabel??"TALK";case "door":return item.GetComponent<HotelAutomaticEntrance>()?"OPEN ENTRANCE":item.open?"CLOSE DOOR":"OPEN DOOR";case "lamp":return item.lampOn?"SWITCH OFF":"SWITCH ON";case "water":return item.waterFixture&&item.waterFixture.IsRunning?"TURN WATER OFF":"TURN WATER ON";case "seat":return "TAKE A SEAT";case "cart":return "PUSH CART";case "bell":return "RING BELL";default:return "PICK UP";}}
  static readonly Dictionary<string,string> uiModelNames=new(){
   {"BathroomBucket","Cleaning Bucket"},{"BathroomBuilt","Hand Soap"},{"BathroomTool","Dustpan"},{"Blue-Suitcase","Blue Suitcase"},
   {"CheckInBell","Reception Bell"},{"CreamSuitcase","Cream Suitcase"},{"CutlerySet","Cutlery"},{"DiningChair","Dining Chair"},
   {"DishCart","Dish Cart"},{"DuffelBag","Travel Bag"},{"FoodBox","Food Box"},{"FryingPan","Frying Pan"},{"Gold-Trolley","Luggage Trolley"},
   {"HotelParcel","Parcel"},{"HousekeepingCart","Housekeeping Cart"},{"Imported chair","Cozy Armchair"},{"Imported S_floorlamp_A_EHH_01","Reading Lamp"},
   {"Imported StaticMesh","Lounge Sofa"},{"LaundryChair","Laundry Chair"},{"LaundryCleaner","Cleaning Liquid"},{"LaundryDetergent","Laundry Detergent"},
   {"LaundryLamp","Laundry Lamp"},{"LaundryLinen","Laundry Bundle"},{"LaundrySofa","Laundry Sofa"},{"LaundrySpray","Spray Bottle"},
   {"LobbyBench3","Cozy Sofa"},{"LobbyChair","Lounge Chair"},{"MealPlate","Dinner Plate"},{"MixingBowl","Mixing Bowl"},{"Mop","Mop"},
   {"MopBucket","Mop Bucket"},{"OilBottle","Oil Bottle"},{"PlateStack","Plate Stack"},{"PrepBoardSet","Chopping Board"},{"RedSuitcase","Red Suitcase"},
   {"RiceSack","Rice Sack"},{"SeasoningSet","Seasoning Set"},{"ServingTray","Serving Tray"},{"SoupBowl","Soup Bowl"},{"StockPot","Cooking Pot"},
   {"TableLamp","Table Lamp"},{"UtilityChair","Tea Room Chair"},{"UtilityTeaCup","Teacup"},{"UtilityTeapot","Teapot"},{"Vacuum","Vacuum Cleaner"},
   {"VegCrate","Vegetable Crate"},{"WallExtinguisher","Fire Extinguisher"},{"WaterCup","Water Glass"},{"WetFloorSign","Wet Floor Sign"}
  };
  static readonly Dictionary<string,string> uiDoorNames=new(){
   {"Prop_0000","Laundry Room"},{"Prop_0001","Storage Room"},{"Prop_0002","Receiving Room"},{"Prop_0003","Delivery Entrance"},
   {"Prop_0004","Kitchen"},{"Prop_0005","Electrical Room"},{"Prop_0006","Utility Room"},{"Prop_0007","Common Room"},
   {"Prop_0008","Restroom"},{"Prop_0009","Stairway A"},{"Prop_0010","Service Lift"},{"Prop_0011","Guest Lift"},
   {"Prop_0012","Staff Room"},{"Prop_0013","Stairway B"},{"Prop_0014","Stairway B"},{"Prop_0016","Guest Lounge"},
   {"Prop_0017","Dining Room"},{"Prop_0262","Restroom Stall 1"},{"Prop_0263","Restroom Stall 2"}
  };
  static string ItemName(HotelItem item){
   if(!item)return "Item";
   if(item.kind=="door"&&uiDoorNames.TryGetValue(item.name,out var doorName))return doorName;
   if(uiModelNames.TryGetValue(item.sourceModel??"",out var modelName))return modelName;
   string label=item.displayName??"";bool english=label.Length>0;foreach(char c in label)if(c>127){english=false;break;}
   if(english)return label;
   switch(item.kind){case "door":return "Door";case "lamp":return "Lamp";case "seat":return "Comfy Seat";case "cart":return "Trolley";case "bell":return "Reception Bell";case "mop":return "Mop";default:return "Item";}
  }
  bool Interaction(out string name,out string action,out bool holding,out string doorAction){
   name=action=doorAction=null;holding=false;
   if(seated){name=ItemName(seated);action="STAND UP";return true;}
   if(cart){if(focused&&(focused.kind=="door"||focused.GetComponent<HotelRoomDelivery>()||focused.GetComponent<HotelServiceLift>()||focused.GetComponent<HotelElevatorButton>())){name=ItemName(focused);action=ItemAction(focused);return true;}name=ItemName(cart);action="RELEASE CART";return true;}
   if(held&&focused&&(focused.kind=="guest"||focused.GetComponent<HotelRoomDelivery>()||focused.GetComponent<HotelLaundryMachine>()||focused.GetComponent<HotelServiceLift>()||focused.GetComponent<HotelElevatorButton>()||focused.GetComponent<HotelLuggageTrolley>()||held.GetComponent<HotelServiceTray>()&&focused.GetComponent<HotelGuestSupply>())){name=ItemName(focused);action=focused.GetComponent<HotelLuggageTrolley>()?"LOAD SUITCASE":held.GetComponent<HotelServiceTray>()&&focused.GetComponent<HotelGuestSupply>()?"LOAD TRAY":ItemAction(focused);holding=true;return true;}if(held){name=ItemName(held);action="PUT DOWN";holding=true;if(HasInteraction(focused)&&focused.kind=="door")doorAction=ItemAction(focused);return true;}
   if(!HasInteraction(focused))return false;name=ItemName(focused);action=ItemAction(focused);return true;
  }
  void DrawInteraction(Rect safe){
   bool visible=Interaction(out var name,out var action,out var holding,out var doorAction);
   if(visible){uiLastName=name;uiLastAction=action;uiLastHolding=holding;uiLastDoor=doorAction;}
   if(Event.current.type==EventType.Repaint)uiOpacity=Mathf.MoveTowards(uiOpacity,visible?1:0,Time.unscaledDeltaTime*10);
   if(uiOpacity<.001f)return;
   float width=Mathf.Min(468,safe.width-40),x=safe.center.x-width/2,y=safe.yMax-131+(1-uiOpacity)*12;
   if(held&&held.GetComponent<HotelServiceTray>()){width=Mathf.Min(390,safe.width-40);x=safe.x+24;y=safe.yMax-390+(1-uiOpacity)*12;}
   using(new UiAlphaScope(uiOpacity)){
    Bubble(new Rect(x,y,width,98),uiCream,uiCreamBottom,21,6);
    // Cyan tab, cream body, and a blue keyboard badge echo the supplied button kit.
    Bubble(new Rect(x+98,y-18,width-120,35),uiBlue,uiBlueBottom,13,3);
    Text(new Rect(x+104,y-19,width-132,35),uiLastAction,uiAction,uiBrown,1);
    Bubble(new Rect(x+17,y+15,66,66),uiBlue,uiBlueBottom,16,4);
    uiKeyContent.text=uiLastHolding&&uiLastAction=="PUT DOWN"?"Q":"E";Text(new Rect(x+17,y+12,66,66),uiKeyContent.text,uiKey,uiBrown,1.5f);
    uiNameContent.text=uiLastName;uiTitle.fontSize=30;
    while(uiTitle.fontSize>19&&uiTitle.CalcSize(uiNameContent).x>width-126)uiTitle.fontSize--;
    GUI.Label(new Rect(x+101,y+26,width-122,49),uiNameContent,uiTitle);
    if(uiLastHolding){
     float fw=uiLastDoor==null?174:350;var f=new Rect(x+width/2-fw/2,y-68,fw,34);
     Bubble(f,uiOrange,uiOrangeBottom,13,3);
     Text(f,uiLastDoor==null?"SPACE  Throw":"SPACE  Throw    E  "+(uiLastDoor=="OPEN DOOR"?"Open door":"Close door"),uiBrand,uiBrown,1);
    }
   }
   if(visible&&HasInteraction(focused)&&!seated&&!cart){
    float s=held?4:6;Panel(new Rect(safe.center.x-s-2,safe.center.y-s-2,s*2+4,s*2+4),uiBrown,8);
    Panel(new Rect(safe.center.x-s,safe.center.y-s,s*2,s*2),uiBlue,uiBlueBottom,6);
   }
  }
  struct UiAlphaScope:System.IDisposable{Color old;public UiAlphaScope(float alpha){old=GUI.contentColor;GUI.contentColor=new Color(1,1,1,alpha);}public void Dispose(){GUI.contentColor=old;}}
  void DrawNotice(Rect safe){
   if(Time.time>=messageUntil||string.IsNullOrWhiteSpace(message)||message=="Q  Put down | SPACE  Throw"||message=="E  Stand up"||message=="E  Release cart")return;
   float width=Mathf.Min(460,safe.width-40);var r=new Rect(safe.center.x-width/2,safe.y+28,width,58);
   using(new UiAlphaScope(Mathf.Min(1,(messageUntil-Time.time)*3))){Bubble(r,uiCream,uiCreamBottom,17,4);GUI.Label(Inset(r,9),message,uiToast);}
  }
  void Star(Vector2 center,float radius){
   // A tiny four-point sparkle that does not depend on a symbol font.
   var matrix=GUI.matrix;GUIUtility.RotateAroundPivot(45,center);
   Panel(new Rect(center.x-radius,center.y-3,radius*2,6),uiCream,3);Panel(new Rect(center.x-3,center.y-radius,6,radius*2),uiCream,3);GUI.matrix=matrix;
  }
  void DrawMenu(Rect safe){
   if(employee&&employee.Ready){DrawEmployeeMenu(safe);return;}
   Rule(safe,new Color(.05f,.15f,.20f,.47f));
   float width=Mathf.Min(466,safe.width-40);var r=new Rect(safe.center.x-width/2,safe.center.y-239,width,478);
   Bubble(r,uiCream,uiCreamBottom,32,9);
   // A blue toy-like header with an orange backing ribbon.
   Bubble(new Rect(r.x-8,r.y+34,r.width+16,143),uiOrange,uiOrangeBottom,25,5);
   Bubble(new Rect(r.x-14,r.y+23,r.width+28,139),uiBlue,uiBlueBottom,25,6);
   uiHeading.fontSize=50;
   Text(new Rect(r.x+18,r.y+33,r.width-36,57),"HOTEL",uiHeading,uiBrown,2.5f);
   uiHeading.fontSize=46;
   Text(new Rect(r.x+18,r.y+86,r.width-36,58),"TOGETHER",uiHeading,uiBrown,2.5f);
   Star(new Vector2(r.x+31,r.y+74),9);Star(new Vector2(r.xMax-31,r.y+115),7);
   GUI.Label(new Rect(r.x+24,r.y+188,r.width-48,35),"Make yourself at home!",uiToast);
   float bx=r.x+37,bw=r.width-74;
   if(MenuButton(new Rect(bx,r.y+246,bw,66),"LET'S PLAY",true))CaptureMouse();
   if(MenuButton(new Rect(bx,r.y+329,bw,58),"START OVER",false)){ResetWorld();CaptureMouse();}
   GUI.Label(new Rect(r.x+25,r.y+414,r.width-50,28),"ESC  Menu  |  E  Interact",uiSmall);
  }
  void DrawEmployeeMenu(Rect safe){
   Rule(safe,new Color(.05f,.15f,.20f,.63f));
   float width=Mathf.Min(750,safe.width-36),height=650;var r=new Rect(safe.center.x-width/2,safe.center.y-height/2,width,height);
   Bubble(r,uiCream,uiCreamBottom,28,7);
   Bubble(new Rect(r.x+20,r.y+18,width-40,77),uiBlue,uiBlueBottom,20,4);
   uiHeading.fontSize=36;Text(new Rect(r.x+20,r.y+20,width-40,70),"HOTEL TOGETHER",uiHeading,uiBrown,2);
   GUI.Label(new Rect(r.x+20,r.y+109,width-40,35),"CHOOSE YOUR EMPLOYEE",uiToast);
   float cardWidth=(width-74)/2,cardHeight=304;
   for(int i=0;i<employee.employees.Length&&i<2;i++){
    var entry=employee.employees[i];var card=new Rect(r.x+26+i*(cardWidth+22),r.y+156,cardWidth,cardHeight);bool selected=employee.SelectedIndex==i;
    Panel(new Rect(card.x-4,card.y-4,card.width+8,card.height+8),selected?entry.accent:uiBrown,20);
    Panel(card,selected?Color.white:uiCream,17);
    if(entry.portrait)GUI.DrawTexture(new Rect(card.x+12,card.y+10,card.width-24,214),entry.portrait,ScaleMode.ScaleToFit,true);
    uiTitle.fontSize=26;uiTitle.alignment=TextAnchor.MiddleCenter;GUI.Label(new Rect(card.x+8,card.y+224,card.width-16,34),entry.title,uiTitle);uiTitle.alignment=TextAnchor.MiddleLeft;
    GUI.Label(new Rect(card.x+8,card.y+258,card.width-16,27),selected?"SELECTED":"SELECT",uiSmall);
    if(GUI.Button(card,GUIContent.none,GUIStyle.none))employee.SelectEmployee(i);
   }
   GUI.Label(new Rect(r.x+20,r.y+476,width-40,28),"Look down to see your feet.  E to reach and grab.",uiSmall);
   float bw=(width-74)/2;
   if(MenuButton(new Rect(r.x+26,r.y+525,bw,61),"LET'S PLAY",true))CaptureMouse();
   if(MenuButton(new Rect(r.x+48+bw,r.y+525,bw,61),"START OVER",false)){ResetWorld();CaptureMouse();}
   GUI.Label(new Rect(r.x+20,r.y+608,width-40,25),"LEFT / RIGHT  Choose  |  ESC  Menu  |  E  Interact",uiSmall);
   if(Time.time<messageUntil)GUI.Label(new Rect(r.x+20,r.yMax+16,width-40,35),message,uiToast);
  }
  bool MenuButton(Rect r,string label,bool primary){
   bool hover=r.Contains(Event.current.mousePosition),press=hover&&GUIUtility.hotControl!=0;var draw=r;if(press)draw.y+=3;
   Color top=primary?uiBlue:uiOrange,bottom=primary?uiBlueBottom:uiOrangeBottom;if(hover)top=Color.Lerp(top,Color.white,.22f);
   Bubble(draw,top,bottom,18,press?2:6);Text(draw,label,primary?uiButton:uiSecondary,uiBrown,1.7f);
   // Stable IDs and dedicated contents prevent stale label/key geometry as the target changes.
   return GUI.Button(r,GUIContent.none,GUIStyle.none);
  }
  void OnGUI(){
   PrepareInterface();var matrix=GUI.matrix;var color=GUI.color;var content=GUI.contentColor;
   float scale=Mathf.Clamp(Mathf.Min(Screen.width/1440f,Screen.height/900f),.45f,2f);
   GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one*scale);GUI.color=GUI.contentColor=Color.white;
   var a=Screen.safeArea;var safe=new Rect(a.x/scale,(Screen.height-a.yMax)/scale,a.width/scale,a.height/scale);
   if(Cursor.lockState==CursorLockMode.Locked){DrawInteraction(safe);DrawNotice(safe);}else{uiOpacity=0;DrawMenu(safe);}
   GUI.matrix=matrix;GUI.color=color;GUI.contentColor=content;
  }
  public void PlayStrikeGesture(){throwGesture=1;if(employee&&employee.Avatar)employee.Avatar.ThrowGesture();}public void Incapacitate(){Drop();EndCart();if(seated)Stand();eyeBase=new Vector3(standingEyeBase.x,standingEyeBase.y*.55f,standingEyeBase.z);}public void Recover(){if(view){eyeBase=standingEyeBase;view.transform.localPosition=eyeBase;}}
  void OnDestroy(){foreach(var surface in uiSurfaces.Values)if(surface)Destroy(surface);uiSurfaces.Clear();if(uiOwnFont&&font)Destroy(font);foreach(var mesh in handMeshes)if(mesh)Destroy(mesh);if(handSkin)Destroy(handSkin);if(handSleeve)Destroy(handSleeve);if(referenceHandMesh)Destroy(referenceHandMesh);if(anchor)Destroy(anchor.gameObject);if(hands)Destroy(hands.gameObject);}

 }
}
