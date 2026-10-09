using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using HotelTogether;
public static class UpperFloorsQA {
 static HotelPlayer player;static CharacterController cc;static List<string> report=new();
 static Vector3 P(float x,float y,float z)=>new Vector3(29-x,y,z-33);
 static void Check(string label,bool ok,string detail=""){report.Add((ok?"PASS ":"FAIL ")+label+(detail==""?"":" : "+detail));}
 static void Pose(float x,float y,float z){cc.enabled=false;player.transform.position=P(x,y+.025f,z);cc.enabled=true;Physics.SyncTransforms();}
 static bool Walk(float x,float z){var target=P(x,0,z);for(int i=0;i<1500;i++){var delta=target-player.transform.position;delta.y=0;if(delta.magnitude<.035f)return true;var old=player.transform.position;cc.Move(Vector3.ClampMagnitude(delta,.04f)+Vector3.down*.04f);if(Vector3.Distance(old,player.transform.position)<.0001f)return false;}return false;}
 static string At()=>player.transform.position.ToString("F2");
 public static async Task<string> Main(){
  report.Clear();player=UnityEngine.Object.FindAnyObjectByType<HotelPlayer>();cc=player.GetComponent<CharacterController>();player.enabled=false;
  foreach(var d in UnityEngine.Object.FindObjectsByType<HotelItem>().Where(i=>i.kind=="door"))d.open=true;await Task.Delay(1500);Physics.SyncTransforms();
  foreach(string id in new[]{"A","B"}){
   float x=id=="A"?12:38,z=id=="A"?22:16;bool rev=id=="B";
   float front=rev?z+1.2f:z+4.8f,back=rev?z+4.8f:z+1.2f,fc=rev?z+.65f:z+5.35f,bc=rev?z+5.35f:z+.65f;
   Pose(x+4.7f,0,z+3);Check(id+" ground doorway",Walk(x+3.53f,z+3),At());
   for(int level=0;level<2;level++){
    bool ok=Walk(x+3.53f,fc)&&Walk(x+2.35f,fc)&&Walk(x+2.35f,front)&&Walk(x+2.35f,back)&&Walk(x+2.35f,bc)&&Walk(x+.85f,bc)&&Walk(x+.85f,back)&&Walk(x+.85f,front)&&Walk(x+.85f,fc)&&Walk(x+3.53f,fc)&&Walk(x+3.53f,z+3);
    Check(id+" ascend to level "+(level+2),ok&&Mathf.Abs(player.transform.position.y-(level+1)*4.5f)<.13f,At());
    Check(id+" exit to level "+(level+2)+" corridor",Walk(x+4.7f,z+3),At());Walk(x+3.53f,z+3);
   }
   for(int level=2;level>0;level--){
    bool ok=Walk(x+3.53f,fc)&&Walk(x+.85f,fc)&&Walk(x+.85f,front)&&Walk(x+.85f,back)&&Walk(x+.85f,bc)&&Walk(x+2.35f,bc)&&Walk(x+2.35f,back)&&Walk(x+2.35f,front)&&Walk(x+2.35f,fc)&&Walk(x+3.53f,fc)&&Walk(x+3.53f,z+3);
    Check(id+" descend to level "+level,ok&&Mathf.Abs(player.transform.position.y-(level-1)*4.5f)<.13f,At());
   }
  }
  var plan=JsonUtility.FromJson<HotelUpperFloors.Plan>(File.ReadAllText("Assets/HotelTogether/UpperPlan.json"));
  foreach(var room in plan.upper.Where(r=>r.cat!="stair"&&r.cat!="lift"))foreach(var d in room.doors){
   float x=d.side=="N"||d.side=="S"?d.p:d.side=="W"?room.x:room.x+room.w,z=d.side=="N"?room.y:d.side=="S"?room.y+room.h:d.p;
   float dx=d.side=="W"?-1:d.side=="E"?1:0,dz=d.side=="N"?-1:d.side=="S"?1:0;Pose(x+dx*.8f,4.5f,z+dz*.8f);Check(room.id+" doorway and landing",Walk(x-dx*.8f,z-dz*.8f)&&Mathf.Abs(player.transform.position.y-4.5f)<.12f,At());
  }
  Pose(29,4.5f,24);cc.Move(Vector3.down*.1f);Check("Upper gallery floor solid",Mathf.Abs(player.transform.position.y-4.5f)<.12f);Check("Gallery guard blocks fall",!Walk(29,28),At());
  Check("Lobby remains open vertically",!Physics.Raycast(P(29,.2f,33),Vector3.up,out var h,8.5f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore));
  Check("No furnishings added above",GameObject.Find("Upper floors and rooftop").GetComponentsInChildren<HotelItem>(true).Length==0);
  foreach(var room in plan.roof.Where(r=>r.cat=="service"))foreach(var d in room.doors){float x=d.side=="N"||d.side=="S"?d.p:d.side=="W"?room.x:room.x+room.w,z=d.side=="N"?room.y:d.side=="S"?room.y+room.h:d.p;float dx=d.side=="W"?-1:d.side=="E"?1:0,dz=d.side=="N"?-1:d.side=="S"?1:0;Pose(x+dx*.8f,9,z+dz*.8f);Check(room.id+" roof service doorway",Walk(x-dx*.8f,z-dz*.8f)&&Mathf.Abs(player.transform.position.y-9)<.12f);}
  foreach(float y in new[]{4.5f,9f}){Pose(18,y,25);bool route=Walk(18,24)&&Walk(40,24)&&Walk(44,24)&&Walk(44,19);Check("A to B corridor connection at "+y,route&&Mathf.Abs(player.transform.position.y-y)<.12f,At());}
  var colliders=GameObject.Find("Upper floors and rooftop").GetComponentsInChildren<Collider>();int overlaps=0;var slabs=colliders.OfType<BoxCollider>().Where(c=>c.name.Contains("slab")||c.name.Contains("landing")||c.name.Contains("access passage")).ToArray();for(int i=0;i<slabs.Length;i++)for(int j=i+1;j<slabs.Length;j++)if(Physics.ComputePenetration(slabs[i],slabs[i].transform.position,slabs[i].transform.rotation,slabs[j],slabs[j].transform.position,slabs[j].transform.rotation,out var dir,out float distance)&&distance>.004f)overlaps++;
  Check("Floor and stair slabs have no overlapping volumes",overlaps==0,"overlaps="+overlaps);
  player.enabled=true;player.ResetWorld();var result=string.Join("\n",report);File.WriteAllText("../HotelTogether-Upper-Floors-QA.txt",result);return result;
 }
}
