using RootMotion.FinalIK;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Rambler.Movement;

namespace Rambler.Combat
{
    //Kneeling pistol fire behind low cover (animator state "Cover Low Fire", Kneel_Pistol_Fire).
    //While it plays, the weapon rigs let go of the hands, and (Final IK, run here in a fixed order after the animation):
    //  1. the right hand turns so this character's own gun points straight down the line of sight
    //     (the clip was keyed on the guard's pistol; other guns sit in the hand differently),
    //  2. an aim solver bends the spine so the barrel points at the target,
    //  3. a limb solver puts the left hand on the gun's LH_Pos marker.
    [DefaultExecutionOrder(100)] //After Mover, which sets the rig weights each Update
    public class CoverKneelAim : MonoBehaviour
    {
        [SerializeField] string stateName = "Cover Low Fire";
        [Tooltip("Seconds to blend the IK in and out.")]
        [SerializeField] float blendTime = 0.2f;
        [Tooltip("Aim at this height on the target.")]
        [SerializeField] float targetHeight = 1.2f;
        [Range(0f, 1f)]
        [SerializeField] float spineAimWeight = 1f;
        [Range(0f, 1f)]
        [SerializeField] float supportHandWeight = 1f;

        Animator anim;
        Fighter fighter;
        Mover mover;
        WeaponIK weaponIK;
        Rig mainRig;
        RigBuilder rigBuilder;
        float[] rigWeights; //Each rig layer's weight before kneeling, restored after
        readonly IKSolverAim aim = new IKSolverAim();
        readonly IKSolverLimb leftArm = new IKSolverLimb();
        bool ready;
        float weight;
        int stateHash;

        //The gun's barrel and "up" (out of the grip) in its own rotation frame, worked out once per gun
        ActiveWeapon gunFor;
        Vector3 barrelLocal = Vector3.forward, upLocal = Vector3.up;
        Transform lhMarker;

        public bool Active => weight > 0.001f;

        void Awake()
        {
            anim = GetComponent<Animator>();
            fighter = GetComponent<Fighter>();
            mover = GetComponent<Mover>();
            weaponIK = GetComponent<WeaponIK>();
            if (mover != null)
            {
                var f = typeof(Mover).GetField("mainRig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                mainRig = f != null ? f.GetValue(mover) as Rig : null;
            }
            stateHash = Animator.StringToHash(stateName);
            rigBuilder = GetComponent<RigBuilder>();
        }

        void Start()
        {
            if (anim == null || !anim.isHuman) { enabled = false; return; }
            var spine = anim.GetBoneTransform(HumanBodyBones.Spine);
            var chest = anim.GetBoneTransform(HumanBodyBones.Chest);
            var upper = anim.GetBoneTransform(HumanBodyBones.UpperChest);
            var chain = new System.Collections.Generic.List<Transform>();
            foreach (var b in new[] { spine, chest, upper }) if (b != null && !chain.Contains(b)) chain.Add(b);
            var la = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var lf = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var lh = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            if (chain.Count == 0 || la == null || lf == null || lh == null) { enabled = false; return; }
            aim.SetChain(chain.ToArray(), transform);
            aim.clampWeight = 0.3f;
            aim.maxIterations = 2;
            leftArm.SetChain(la, lf, lh, transform);
            leftArm.goal = AvatarIKGoal.LeftHand;
            leftArm.bendModifier = IKSolverLimb.BendModifier.Animation;
            ready = true;
        }

        bool InKneelState()
        {
            var cur = anim.GetCurrentAnimatorStateInfo(0);
            if (cur.shortNameHash == stateHash) return !anim.IsInTransition(0) || anim.GetNextAnimatorStateInfo(0).shortNameHash == stateHash;
            return anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).shortNameHash == stateHash;
        }

        void Update()
        {
            if (!ready) return;
            bool want = InKneelState() && fighter != null && fighter.activeWeapon != null;
            weight = Mathf.MoveTowards(weight, want ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, blendTime));
            //The kneel's own arms hold the gun: let go of the standing weapon rigs (and give them back afterwards)
            if (rigBuilder != null)
            {
                var layers = rigBuilder.layers;
                if (Active && rigWeights == null)
                {
                    rigWeights = new float[layers.Count];
                    for (int i = 0; i < layers.Count; i++) rigWeights[i] = layers[i].rig != null ? layers[i].rig.weight : 0f;
                }
                if (rigWeights != null)
                {
                    for (int i = 0; i < layers.Count && i < rigWeights.Length; i++)
                    {
                        var rig = layers[i].rig;
                        if (rig == null) continue;
                        rig.weight = rigWeights[i] * (1f - weight); //Back to the stored weight once the blend is out
                    }
                    if (!Active) rigWeights = null;
                }
            }
            if (!Active) return;
            if (weaponIK != null) weaponIK.AimWeight = Mathf.Min(weaponIK.AimWeight, 1f - weight);
        }

        void LateUpdate()
        {
            if (!ready || !Active || fighter.activeWeapon == null) return;
            var gun = fighter.activeWeapon;
            if (gun != gunFor) MeasureGun(gun);
            var g = gun.transform;
            var rh = anim.GetBoneTransform(HumanBodyBones.RightHand);

            //1. Right hand: barrel along the body's facing, grip upright
            Vector3 fwd = transform.forward;
            Quaternion want = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(barrelLocal, upLocal));
            Quaternion delta = want * Quaternion.Inverse(g.rotation);
            rh.rotation = Quaternion.Slerp(Quaternion.identity, delta, weight) * rh.rotation;

            //2. Spine: barrel onto the target
            Transform target = fighter.EnemyPos;
            if (target != null && spineAimWeight > 0f)
            {
                aim.transform = g;
                aim.axis = barrelLocal;
                aim.IKPosition = target.position + Vector3.up * targetHeight;
                aim.IKPositionWeight = weight * spineAimWeight;
                aim.Update();
            }

            //3. Left hand onto the gun
            if (lhMarker != null && supportHandWeight > 0f)
            {
                leftArm.IKPosition = lhMarker.position;
                leftArm.IKRotation = lhMarker.rotation;
                leftArm.IKPositionWeight = weight * supportHandWeight;
                leftArm.IKRotationWeight = weight * supportHandWeight;
                leftArm.Update();
            }
        }

        //Barrel = the muzzle's forward (where shots go); up = the gun's up, square to the barrel
        void MeasureGun(ActiveWeapon gun)
        {
            gunFor = gun;
            lhMarker = FindDeep(gun.transform, "LH_Pos");
            if (!aim.initiated) aim.Initiate(transform);
            if (!leftArm.initiated) leftArm.Initiate(transform);
            Quaternion inv = Quaternion.Inverse(gun.transform.rotation);
            Transform muzzle = gun.MuzPos();
            Vector3 barrel = muzzle != null ? muzzle.forward : gun.transform.forward;
            //Some muzzle markers face backwards: the barrel must point away from the middle of the gun
            if (muzzle != null)
            {
                var r = gun.GetComponentInChildren<Renderer>();
                Vector3 centre = r != null ? r.bounds.center : gun.transform.position;
                if (Vector3.Dot(barrel, muzzle.position - centre) < 0f) barrel = -barrel;
            }
            Vector3 up = Vector3.ProjectOnPlane(gun.transform.up, barrel);
            if (up.sqrMagnitude < 0.01f) up = Vector3.ProjectOnPlane(muzzle != null ? muzzle.up : Vector3.up, barrel);
            barrelLocal = (inv * barrel).normalized;
            upLocal = (inv * up).normalized;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }
    }
}
