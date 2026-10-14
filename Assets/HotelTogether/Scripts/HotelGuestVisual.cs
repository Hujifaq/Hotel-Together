using UnityEngine;
namespace HotelTogether
{
    public sealed class HotelGuestVisual : MonoBehaviour
    {
        public Transform hips, head, leftArm, rightArm, leftForearm, rightForearm;
        public Transform leftLeg, rightLeg, leftShin, rightShin, leftBrow, rightBrow;
        public Transform leftEye, rightEye, smile;
        public GameObject luggage;
        public HotelGuestDelivery arrivalLuggagePrefab;
        public Transform luggageHandle;
        Vector3 hipBase, headBase, rightElbowBase, luggageBase, luggageScaleBase;
        Quaternion luggageRotationBase;
        float phase, blinkOffset, animationClock, seatedWeight, movingWeight, eatingWeight;
        public float AnimationClock => animationClock;
        public Vector3 PrankRestPosition(Transform bone){if(!initialized)Pose(0,0,false,false,false,1,false);return bone==hips?hipBase:bone==head?headBase:bone==rightForearm?rightElbowBase:bone.localPosition;}
        public void ResetStandingPose(){seatedWeight=movingWeight=eatingWeight=0;Pose(0,.001f,false,false,false,1,false);}
        bool initialized, hasLuggageGrip;
        Vector3 luggageGripLocal;
        SkinnedMeshRenderer[] gripRenderers;
        public void SetLuggageGrip(Vector3 worldPoint)
        {
            luggageGripLocal = rightForearm.InverseTransformPoint(worldPoint);
            hasLuggageGrip = true;
            gripRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        }
        void Awake()
        {
            if (!Application.isPlaying) return;
            try { HotelCorrectedCharacter.Install(this); }
            catch (System.Exception error) { Debug.LogError("Could not skin corrected hotel character: " + error); }
        }
        public void Pose(float speed, float elapsed, bool seated, bool sleeping, bool hungry, float happiness, bool arriving)
        {
            if (!initialized) { hipBase=hips.localPosition;headBase=head.localPosition;rightElbowBase=rightForearm.localPosition;if(luggage){luggageBase=luggage.transform.localPosition;luggageRotationBase=luggage.transform.localRotation;luggageScaleBase=luggage.transform.localScale;}blinkOffset=Random.Range(0,4.7f);initialized=true; }
            if(elapsed<=0)return;
            animationClock+=elapsed;
            movingWeight=Mathf.MoveTowards(movingWeight,Mathf.Clamp01(speed/1.7f),elapsed*6);
            seatedWeight=Mathf.MoveTowards(seatedWeight,seated?1:0,elapsed*3.5f);
            eatingWeight=Mathf.MoveTowards(eatingWeight,hungry&&seated?1:0,elapsed*4);
            float motion=movingWeight;phase+=speed*elapsed*8;
            float stride=Mathf.Sin(phase)*24*motion;
            bool tallLuggage = arriving && luggageHandle && luggageHandle.localPosition.z < -.1f;
            hips.localPosition=hipBase+Vector3.up*(Mathf.Cos(phase*2)*.012f*motion-.36f*seatedWeight+Mathf.Sin(animationClock*2)*.004f*(1-motion));
            head.localPosition=headBase;hips.localRotation=Quaternion.identity;
            head.localRotation=Quaternion.Euler(sleeping?0:Mathf.Sin(animationClock*1.1f)*1.5f*(1-motion),Mathf.Sin(animationClock*.7f)*2*(1-motion),0);
            rightForearm.localPosition=rightElbowBase;
            leftLeg.localRotation=Quaternion.Euler(Mathf.Lerp(stride,-78,seatedWeight),0,0);
            rightLeg.localRotation=Quaternion.Euler(Mathf.Lerp(-stride,-78,seatedWeight),0,0);
            leftShin.localRotation=Quaternion.Euler(Mathf.Lerp(Mathf.Max(0,-stride)*.7f,78,seatedWeight),0,0);
            rightShin.localRotation=Quaternion.Euler(Mathf.Lerp(Mathf.Max(0,stride)*.7f,78,seatedWeight),0,0);
            leftArm.localRotation=Quaternion.Euler(-stride*.65f,0,8);
            rightArm.localRotation=Quaternion.Euler(arriving?35:stride*.65f,0,-8);
            leftForearm.localRotation=Quaternion.Euler(Mathf.Lerp(-8,-42,seatedWeight),0,0);
            rightForearm.localRotation=arriving?Quaternion.identity:Quaternion.Euler(Mathf.Lerp(-8,-42,seatedWeight),0,0);
            rightForearm.localRotation=Quaternion.Slerp(rightForearm.localRotation,Quaternion.Euler(-55+Mathf.Sin(animationClock*3)*18,0,0),eatingWeight);
            float blink=sleeping?.04f:(Mathf.Repeat(animationClock+blinkOffset,4.7f)<.13f?.12f:1);
            leftEye.localScale=new Vector3(1,blink,1);rightEye.localScale=new Vector3(1,blink,1);
            leftBrow.localRotation=Quaternion.Euler(0,0,happiness<.35f?-17:6);
            rightBrow.localRotation=Quaternion.Euler(0,0,happiness<.35f?17:-6);
            smile.localScale=new Vector3(1,happiness<.35f?.4f:1,1);
            if (gripRenderers != null)
                foreach (var skin in gripRenderers)
                    if (skin && skin.enabled && skin.sharedMesh && skin.sharedMesh.blendShapeCount > 0)
                        skin.SetBlendShapeWeight(0, arriving && !seated ? 100 : 0);
            if(luggage)
            {
                // Pull with a straight arm behind the body. The two case meshes
                // differ in height, so each needs its own grounded lean.
                luggage.transform.localPosition = luggageBase + (arriving
                    ? tallLuggage ? new Vector3(-.12f, -.075f, -.22f) : new Vector3(-.12f, -.075f, -.38f)
                    : Vector3.zero);
                luggage.transform.localRotation = luggageRotationBase * (arriving ? Quaternion.Euler(45, 0, 0) : Quaternion.identity);
                luggage.transform.localScale = Vector3.Scale(luggageScaleBase,
                    arriving ? new Vector3(1, tallLuggage ? .65f : .8f, 1) : Vector3.one);
                // Anchor the handle to the animated palm, including the walking bob.
                if (arriving && hasLuggageGrip && luggageHandle)
                    luggage.transform.position += rightForearm.TransformPoint(luggageGripLocal) - luggageHandle.position;
                luggage.SetActive(arriving);
            }
        }
        public void PoseComplaint(float weight,bool speaking)
        {
            head.localRotation*=Quaternion.Euler(-12*weight,Mathf.Sin(animationClock*13)*12*weight,8*weight);
            leftArm.localRotation=Quaternion.Slerp(leftArm.localRotation,Quaternion.Euler(-65,0,30),weight);
            rightArm.localRotation=Quaternion.Slerp(rightArm.localRotation,Quaternion.Euler(-48,0,-35),weight);
            leftForearm.localRotation=Quaternion.Slerp(leftForearm.localRotation,Quaternion.Euler(-80,0,0),weight);
            rightForearm.localRotation=Quaternion.Slerp(rightForearm.localRotation,Quaternion.Euler(-70,0,0),weight);
            leftBrow.localRotation=Quaternion.Euler(0,0,-20);rightBrow.localRotation=Quaternion.Euler(0,0,20);
            if(speaking)smile.localScale=new Vector3(1,.35f+Mathf.Abs(Mathf.Sin(animationClock*17))*.7f,1);
        }
        public void HoldCup(Vector3 target)
        {
            var start=rightArm.position;var direction=(target-start).normalized;
            float distance=Mathf.Clamp(Vector3.Distance(target,start),.02f,.465f),along=distance*.5f;
            var elbow=start+direction*along+Vector3.ProjectOnPlane(transform.right,direction).normalized*Mathf.Sqrt(Mathf.Max(0,.235f*.235f-along*along));
            rightArm.rotation=Quaternion.FromToRotation(transform.TransformDirection(rightElbowBase),elbow-start)*transform.rotation;
            rightForearm.position=elbow;rightForearm.rotation=Quaternion.FromToRotation(-transform.up,target-elbow)*transform.rotation;
        }
        public void PoseActivity(GuestActivity activity,float dt)
        {
            if(dt<=0)return;
            if(activity==GuestActivity.Unpacking)
            {
                hips.localPosition+=Vector3.down*.05f;
                head.localRotation*=Quaternion.Euler(18,0,0);
                leftArm.localRotation=Quaternion.Euler(-28,0,8);rightArm.localRotation=Quaternion.Euler(-32,0,-8);
                leftForearm.localRotation=Quaternion.Euler(-35+Mathf.Sin(animationClock*3)*12,0,0);
                rightForearm.localRotation=Quaternion.Euler(-35-Mathf.Sin(animationClock*3)*12,0,0);
            }
            else if(activity==GuestActivity.WaitingMeal||activity==GuestActivity.WaitingService||activity==GuestActivity.RouteBlocked)
            {
                // A short request gesture every few seconds; the hand rests between requests.
                float request=Mathf.Max(0,Mathf.Sin(animationClock*1.2f));request*=request;
                leftArm.localRotation*=Quaternion.Euler(-30*request,0,0);
                leftForearm.localRotation*=Quaternion.Euler(-25*request,0,0);
            }
            else if(activity==GuestActivity.Hiding)
            {
                leftArm.localRotation=Quaternion.Euler(-65,0,15);rightArm.localRotation=Quaternion.Euler(-65,0,-15);
                leftForearm.localRotation=Quaternion.Euler(-75,0,0);rightForearm.localRotation=Quaternion.Euler(-75,0,0);
                head.localRotation*=Quaternion.Euler(16,0,0);
            }
        }
    }
}
