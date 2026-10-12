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
        Vector3 hipBase, headBase, rightElbowBase;
        float phase, blinkOffset;
        bool initialized;
        public void Pose(float speed, float elapsed, bool seated, bool sleeping, bool hungry, float happiness, bool arriving)
        {
            if (!initialized) { hipBase=hips.localPosition;headBase=head.localPosition;rightElbowBase=rightForearm.localPosition;blinkOffset=Random.Range(0,4.7f);initialized=true; }
            float motion=Mathf.Clamp01(speed/1.7f);phase+=speed*elapsed*8;
            float stride=Mathf.Sin(phase)*24*motion;
            hips.localPosition=hipBase+Vector3.up*(Mathf.Cos(phase*2)*.012f*motion-(seated?.36f:0));
            head.localPosition=headBase;
            rightForearm.localPosition=rightElbowBase;
            leftLeg.localRotation=Quaternion.Euler(seated?-78:stride,0,0);
            rightLeg.localRotation=Quaternion.Euler(seated?-78:-stride,0,0);
            leftShin.localRotation=Quaternion.Euler(seated?78:Mathf.Max(0,-stride)*.7f,0,0);
            rightShin.localRotation=Quaternion.Euler(seated?78:Mathf.Max(0,stride)*.7f,0,0);
            leftArm.localRotation=Quaternion.Euler(-stride*.65f,0,8);
            rightArm.localRotation=Quaternion.Euler(arriving?12:stride*.65f,0,-8);
            leftForearm.localRotation=Quaternion.Euler(seated?-42:-8,0,0);
            rightForearm.localRotation=Quaternion.Euler(seated?-42:arriving?-22:-8,0,0);
            if(hungry&&seated) rightForearm.localRotation=Quaternion.Euler(-55+Mathf.Sin(Time.time*3)*18,0,0);
            float blink=sleeping?.04f:(Mathf.Repeat(Time.time+blinkOffset,4.7f)<.13f?.12f:1);
            leftEye.localScale=new Vector3(1,blink,1);rightEye.localScale=new Vector3(1,blink,1);
            leftBrow.localRotation=Quaternion.Euler(0,0,happiness<.35f?-17:6);
            rightBrow.localRotation=Quaternion.Euler(0,0,happiness<.35f?17:-6);
            smile.localScale=new Vector3(1,happiness<.35f?.4f:1,1);
            if(luggage) luggage.SetActive(arriving);
            if(arriving&&luggage)
            {
                Vector3 start=rightArm.position,target=luggageHandle?luggageHandle.position:luggage.transform.TransformPoint(new Vector3(0,.49f,0));var direction=(target-start).normalized;
                float distance=Mathf.Clamp(Vector3.Distance(target,start),.02f,.465f),upper=.235f,lower=.235f;
                float along=(upper*upper-lower*lower+distance*distance)/(2*distance);float height=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
                var bend=Vector3.ProjectOnPlane(transform.right+transform.forward*.2f,direction).normalized;var elbow=start+direction*along+bend*height;
                rightArm.rotation=Quaternion.FromToRotation(transform.TransformDirection(rightElbowBase),elbow-start)*transform.rotation;
                rightForearm.position=elbow;rightForearm.rotation=Quaternion.FromToRotation(-transform.up,target-elbow)*transform.rotation;
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
