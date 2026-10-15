using UnityEngine;
namespace HotelTogether
{
    public partial class HotelPlayer
    {
        HotelMaintenanceSystem Maintenance=>hudHotel?hudHotel.Maintenance:null;
        public bool IsMaintaining=>Maintenance&&Maintenance.CurrentAction!=MaintenanceAction.None;
        public MaintenanceAction MaintenanceActivity=>Maintenance?Maintenance.CurrentAction:MaintenanceAction.None;
        bool UseMaintenance(HotelItem item)
        {
            var incident=item.GetComponent<HotelMaintenanceIncident>();
            if(incident){incident.system.Inspect(incident,this);return true;}
            var rack=item.GetComponent<HotelMaintenanceRack>();if(rack){rack.Use(this);return true;}
            var fixture=item.GetComponent<HotelWaterFixture>();if(fixture&&fixture.MaintenanceIsolated){Notify(Maintenance&&!Maintenance.HasPower?"Pump power unavailable. Restore the fuse in G05.":Maintenance&&!Maintenance.HasWater?"Water supply unavailable. Repair the pump in G06.":"Water line isolated. Repair the burst pipe and reopen its valve.");return true;}
            return false;
        }
        Quaternion MaintenanceCarryOffset()
        {
            if(!IsMaintaining)return Quaternion.identity;
            float wave=Mathf.Sin(animationClock*7);
            return MaintenanceActivity switch
            {
                MaintenanceAction.Wrench=>Quaternion.Euler(0,0,wave*25),
                MaintenanceAction.Screwdriver=>Quaternion.Euler(0,0,Mathf.Sin(animationClock*10)*35),
                MaintenanceAction.Spray=>Quaternion.Euler(-8,Mathf.Sin(animationClock*3)*7,0),
                _=>Quaternion.identity
            };
        }
        bool MaintenanceHandPose(float side,out Vector3 target,out Quaternion rotation,out float curl)
        {
            target=Vector3.zero;rotation=Quaternion.identity;curl=.6f;if(!IsMaintaining||!view)return false;
            var action=MaintenanceActivity;var site=Maintenance.Working;
            var point=site?site.Target:view.transform.TransformPoint(new Vector3(0,-.2f,.7f));
            var local=view.transform.InverseTransformPoint(point);local=Vector3.ClampMagnitude(local,.80f);
            float wave=Mathf.Sin(animationClock*(action==MaintenanceAction.Screwdriver?10:7));
            local.x+=side*.10f;local.y+=action==MaintenanceAction.Spray?-.08f:wave*.016f;
            target=view.transform.TransformPoint(local);
            rotation=view.transform.rotation*Quaternion.Euler(70,side*20,action==MaintenanceAction.Valve?wave*55:action==MaintenanceAction.Wrench?wave*25:action==MaintenanceAction.Screwdriver?wave*35:0);
            curl=action==MaintenanceAction.Breaker?.45f:.85f;return true;
        }
        void DrawMaintenanceHud(Rect safe)
        {
            var system=Maintenance;if(!system||!system.Initialized)return;
            HotelMaintenanceIncident priority=system.Working;
            if(!priority)foreach(var incident in system.incidents)if(incident&&incident.Unresolved){priority=incident;break;}
            if(!priority)return;
            var rect=new Rect(safe.x+24,safe.y+462,Mathf.Min(360,safe.width-48),126);
            Bubble(rect,uiCream,uiCreamBottom,17,4);
            GUI.Label(new Rect(rect.x+15,rect.y+10,rect.width-30,24),priority.Title+" / "+system.ActiveCount+" active",hudTitle);
            GUI.Label(new Rect(rect.x+15,rect.y+36,rect.width-30,25),priority.location,hudCaption);
            string text=priority.State==MaintenanceIncidentState.Warning?"Warning: "+Mathf.CeilToInt(priority.WarningLeft)+"s  /  J details":priority.ActionLabel+"  /  "+Mathf.RoundToInt(priority.Progress*100)+"%";
            GUI.Label(new Rect(rect.x+15,rect.y+64,rect.width-30,22),text,hudCaption);
            Panel(new Rect(rect.x+15,rect.y+98,rect.width-30,7),new Color(.2f,.1f,.05f,.15f),3);
            Rule(new Rect(rect.x+15,rect.y+98,(rect.width-30)*priority.Progress,7),uiOrangeBottom);
        }
    }
}
