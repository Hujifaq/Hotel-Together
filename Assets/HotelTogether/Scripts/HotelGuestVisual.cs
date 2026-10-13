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
        float phase, blinkOffset;
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
            float motion=Mathf.Clamp01(speed/1.7f);phase+=speed*elapsed*8;
            float stride=Mathf.Sin(phase)*24*motion;
            bool tallLuggage = arriving && luggageHandle && luggageHandle.localPosition.z < -.1f;
            hips.localPosition=hipBase+Vector3.up*(Mathf.Cos(phase*2)*.012f*motion-(seated?.36f:0));
            head.localPosition=headBase;
            rightForearm.localPosition=rightElbowBase;
            leftLeg.localRotation=Quaternion.Euler(seated?-78:stride,0,0);
            rightLeg.localRotation=Quaternion.Euler(seated?-78:-stride,0,0);
            leftShin.localRotation=Quaternion.Euler(seated?78:Mathf.Max(0,-stride)*.7f,0,0);
            rightShin.localRotation=Quaternion.Euler(seated?78:Mathf.Max(0,stride)*.7f,0,0);
            leftArm.localRotation=Quaternion.Euler(-stride*.65f,0,8);
            rightArm.localRotation=Quaternion.Euler(arriving?35:stride*.65f,0,-8);
            leftForearm.localRotation=Quaternion.Euler(seated?-42:-8,0,0);
            rightForearm.localRotation=arriving?Quaternion.identity:Quaternion.Euler(seated?-42:-8,0,0);
            if(hungry&&seated) rightForearm.localRotation=Quaternion.Euler(-55+Mathf.Sin(Time.time*3)*18,0,0);
            float blink=sleeping?.04f:(Mathf.Repeat(Time.time+blinkOffset,4.7f)<.13f?.12f:1);
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
        public void HoldCup(Vector3 target)
        {
            var start=rightArm.position;var direction=(target-start).normalized;
            float distance=Mathf.Clamp(Vector3.Distance(target,start),.02f,.465f),along=distance*.5f;
            var elbow=start+direction*along+Vector3.ProjectOnPlane(transform.right,direction).normalized*Mathf.Sqrt(Mathf.Max(0,.235f*.235f-along*along));
            rightArm.rotation=Quaternion.FromToRotation(transform.TransformDirection(rightElbowBase),elbow-start)*transform.rotation;
            rightForearm.position=elbow;rightForearm.rotation=Quaternion.FromToRotation(-transform.up,target-elbow)*transform.rotation;
        }
    }
}
