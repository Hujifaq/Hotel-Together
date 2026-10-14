using UnityEngine;
namespace HotelTogether
{
    // Kept equipped after mouse release, like the hotel's other work tools.
    public sealed class HotelDefenseHammer : MonoBehaviour { }
    public sealed class HotelDefenseLatch : MonoBehaviour
    {
        public HotelDefenseOpening opening;
        public Transform bolt;
        public GameObject lockedIndicator,unlockedIndicator;
        public string ActionLabel=>!opening||opening.Broken?"REPAIR DOOR FIRST":opening.LockPending?"CANCEL LOCK":opening.Locked?"UNLOCK DOOR":"LOCK DOOR";
        void Update(){if(!opening)return;if(bolt)bolt.localPosition=Vector3.Lerp(bolt.localPosition,new Vector3(opening.Locked?.065f:0,0,-.04f),1-Mathf.Exp(-Time.deltaTime*12));if(lockedIndicator)lockedIndicator.SetActive(opening.Locked);if(unlockedIndicator)unlockedIndicator.SetActive(!opening.Locked);}
    }
    [DefaultExecutionOrder(70)]
    public sealed class HotelDefenseWork : MonoBehaviour
    {
        public DefenseWorkMode Mode {get;private set;}
        public HotelDefenseOpening Working {get;private set;}
        public float Progress {get;private set;}
        public bool Equipped=>player&&player.held&&player.held.GetComponent<HotelDefenseHammer>();
        public string ActionLabel=>"HOLD LMB: "+Mode.ToString().ToUpper();
        HotelPlayer player;HotelCombat combat;float nextGesture;
        void Awake(){player=GetComponent<HotelPlayer>();combat=GetComponent<HotelCombat>();}
        public void CycleMode(){Cancel();Mode=(DefenseWorkMode)(((int)Mode+1)%4);player.Notify("Hammer: "+Mode.ToString().ToUpper()+". Hold LMB at a window or door.");}
        public void SelectMode(DefenseWorkMode mode){Cancel();Mode=mode;}
        public bool Begin(HotelDefenseOpening opening)
        {
            if(!Equipped||!opening||!opening.CanWork(Mode,combat,true))return false;
            if(Working!=opening){Cancel();Working=opening;}return true;
        }
        public void Cancel(){Working=null;Progress=0;nextGesture=0;}
        public void ResetWork(){Cancel();Mode=DefenseWorkMode.Build;}
        public void AdvanceWork(float dt,bool inputHeld)
        {
            if(!Working)return;if(player.GameplayPaused||Time.timeScale<=0)return;
            if(!float.IsFinite(dt)||dt<=0)return;
            var focus=player.FocusedItem?player.FocusedItem.GetComponentInParent<HotelDefenseOpening>():null;
            if(!inputHeld||!Equipped||focus!=Working||!Working.CanWork(Mode,combat)){Cancel();return;}
            if(Time.time>=nextGesture){nextGesture=Time.time+.6f;player.PlayGesture(Mode==DefenseWorkMode.Remove?HotelPlayerGesture.Place:Mode==DefenseWorkMode.Repair?HotelPlayerGesture.Repair:HotelPlayerGesture.Build,Working.WorkPoint(player));player.PlayFeedback(HotelFeedback.Drop,.22f);}
            float seconds=Mode==DefenseWorkMode.Build?1.6f:Mode==DefenseWorkMode.Repair?2.2f:Mode==DefenseWorkMode.Reinforce?1.9f:.9f;
            Progress=Mathf.Min(1,Progress+dt/seconds);
            if(Progress<1)return;
            bool done=Mode switch {DefenseWorkMode.Build=>Working.AddBoard(combat),DefenseWorkMode.Remove=>Working.RemoveBoard(combat),DefenseWorkMode.Repair=>Working.Repair(combat),DefenseWorkMode.Reinforce=>Working.Reinforce(combat),_=>false};
            if(done)player.Notify(Working.label+": "+Mode.ToString().ToLower()+" complete. Wood "+combat.Wood+" / 30");Cancel();
        }
        void Update(){if(!player||!player.GameplayInputEnabled){if(!player||!player.GameplayPaused)Cancel();return;}AdvanceWork(Time.deltaTime,player.MaintenanceHeld);}
        void OnDisable(){Cancel();}
    }
}
