using UnityEngine;

namespace Rambler.Movement
{
    // Plants a humanoid's feet on the ground and lowers the hips so the lower foot can reach.
    // Must sit on the same GameObject as the Animator, with IK Pass ticked on the Base Layer.
    [RequireComponent(typeof(Animator))]
    public class FootIK : MonoBehaviour
    {
        [Header("Ground Check")]
        [SerializeField] LayerMask groundLayers = ~(1 << 2); //Everything except Ignore Raycast
        [SerializeField] float rayStartHeight = 0.5f;        //How far above the foot the ray starts
        [SerializeField] float rayDepth = 0.5f;              //How far below the foot it can reach

        [Header("Feet")]
        [SerializeField] float footOffset = 0f;     //Extra lift on top of the avatar's own ankle height
        [SerializeField] float plantRange = 0.15f;  //Foot lifted higher than this gets no IK (mid-step)
        [Range(0f, 1f)] [SerializeField] float footWeight = 1f;

        [Header("Hips")]
        [SerializeField] float hipSmoothing = 10f;
        [SerializeField] float maxHipDrop = 0.4f;

        Animator anim;
        float hipOffset;

        void Awake()
        {
            anim = GetComponent<Animator>();
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0) return;

            float leftDrop = PlaceFoot(AvatarIKGoal.LeftFoot);
            float rightDrop = PlaceFoot(AvatarIKGoal.RightFoot);

            //Lower the hips by whichever foot needs to reach furthest down
            float targetOffset = Mathf.Clamp(Mathf.Min(leftDrop, rightDrop, 0f), -maxHipDrop, 0f);
            hipOffset = Mathf.Lerp(hipOffset, targetOffset, hipSmoothing * Time.deltaTime);
            anim.bodyPosition += transform.up * hipOffset;
        }

        //Returns how far the ground sits below (negative) the animated foot
        float PlaceFoot(AvatarIKGoal foot)
        {
            Vector3 animPos = anim.GetIKPosition(foot);
            Quaternion animRot = anim.GetIKRotation(foot);
            Vector3 up = transform.up;

            if (!Physics.Raycast(animPos + up * rayStartHeight, -up, out RaycastHit hit,
                rayStartHeight + rayDepth, groundLayers, QueryTriggerInteraction.Ignore))
            {
                anim.SetIKPositionWeight(foot, 0f);
                anim.SetIKRotationWeight(foot, 0f);
                return 0f;
            }

            //The avatar knows how far its ankle sits above the sole
            float ankleHeight = foot == AvatarIKGoal.LeftFoot ? anim.leftFeetBottomHeight : anim.rightFeetBottomHeight;
            Vector3 plantedPos = hit.point + up * (ankleHeight + footOffset);

            //Fade IK out as the animation lifts the foot, so steps still clear the ground
            float lift = Vector3.Dot(animPos - plantedPos, up);
            float weight = footWeight * (1f - Mathf.Clamp01(lift / plantRange));

            anim.SetIKPositionWeight(foot, weight);
            anim.SetIKRotationWeight(foot, weight);
            anim.SetIKPosition(foot, plantedPos);
            //Keep the animated foot angle, tilted to match the slope
            anim.SetIKRotation(foot, Quaternion.FromToRotation(up, hit.normal) * animRot);

            //Feet mid-step pull the hips down less
            return -lift * weight;
        }
    }
}
