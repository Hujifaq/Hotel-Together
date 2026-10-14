using UnityEngine;
namespace HotelTogether
{
    public partial class HotelPlayer
    {
        HotelDefenseWork defenseWork;
        internal static string DefenseName(HotelItem item)=>ItemName(item);
        public HotelDefenseWork DefenseWork {get{if(!defenseWork)defenseWork=GetComponent<HotelDefenseWork>();return defenseWork;}}
        bool DefenseHandPose(float side,out Vector3 target,out Quaternion rotation,out float curl)
        {
            target=Vector3.zero;rotation=Quaternion.identity;curl=.7f;
            if(!DefenseWork||!DefenseWork.Equipped||!DefenseWork.Working||!view)return false;
            var point=view.transform.InverseTransformPoint(DefenseWork.Working.WorkPoint(this));point=Vector3.ClampMagnitude(point,.8f);float strike=Mathf.Sin(animationClock*10);
            point.x+=side*.10f;point.y+=side>0?strike*.06f:0;
            target=view.transform.TransformPoint(point);rotation=view.transform.rotation*Quaternion.Euler(side>0?50+strike*30:70,side*15,0);curl=side>0?.85f:.4f;return true;
        }
        Quaternion DefenseCarryOffset()=>DefenseWork&&DefenseWork.Working?Quaternion.Euler(Mathf.Sin(animationClock*10)*35,0,0):Quaternion.identity;
        float defenseHudRefresh;
        string defenseSummary,defenseTask;
        void DrawDefenseHud(Rect safe)
        {
            var director=combat?combat.Director:null;if(!director||jobsOpen||!director.IsNight&&(!DefenseWork||!DefenseWork.Equipped))return;
            if(Time.unscaledTime>=defenseHudRefresh)
            {
                defenseHudRefresh=Time.unscaledTime+.5f;int secured=0,locked=0,damaged=0,breached=0;HotelDefenseOpening priority=null;float worst=0;
                foreach(var opening in director.openings)
                {
                    if(!opening)continue;if(opening.BoardCount>0)secured++;if(opening.Locked)locked++;if(opening.Broken)breached++;
                    float severity=(opening.Broken?2:1-opening.StructureHealth/Mathf.Max(1,opening.structureMax))+(opening.BoardMaximum>0?1-opening.BoardHealth/opening.BoardMaximum:0)+(opening.door?1-opening.LockHealth/65:0);
                    if(severity>.01f)damaged++;if(severity>worst){worst=severity;priority=opening;}
                }
                defenseSummary=secured+" boarded / "+locked+" locked / "+damaged+" damaged";
                defenseTask=priority?"REPAIR: "+priority.label:director.IsNight?"Hold the entrances. Rescue fallen guests.":"Prepare openings. Unlock doors at dawn.";
                if(breached>0)defenseTask=breached+" BREACHED / "+defenseTask;
            }
            var rect=new Rect(safe.xMax-Mathf.Min(360,safe.width-48)-24,safe.y+94,Mathf.Min(360,safe.width-48),105);
            Bubble(rect,uiCream,uiCreamBottom,17,3);
            GUI.Label(new Rect(rect.x+14,rect.y+8,rect.width-28,24),"HOTEL DEFENSE / WOOD "+combat.Wood,hudTitle);
            GUI.Label(new Rect(rect.x+14,rect.y+36,rect.width-28,22),defenseSummary,hudCaption);
            GUI.Label(new Rect(rect.x+14,rect.y+61,rect.width-28,35),defenseTask,hudCaption);
        }
    }
}
