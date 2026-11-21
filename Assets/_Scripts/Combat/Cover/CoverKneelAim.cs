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
        [Tooltip("States that fire from cover: hands on the gun and the spine aims at the target.")]
        [SerializeField] string[] fireStates = { "Cover Low Fire", "Corner Shoot L", "Corner Shoot R" };
        [Tooltip("Fire states whose own clips (not the pack's) are made for this IK. Others only use it with pack clips (rifles keep their rigs).")]
        [SerializeField] string[] ownClipFireStates = { "Cover Low Fire" };
        [Tooltip("Seconds to blend the IK in and out.")]
        [SerializeField] float blendTime = 0.2f;
        [Tooltip("Aim at this height on the target.")]
        [SerializeField] float targetHeight = 1.2f;
        [Range(0f, 1f)]
        [SerializeField] float spineAimWeight = 1f;
        [Range(0f, 1f)]
        [SerializeField] float supportHandWeight = 1f;
        [Tooltip("Hiding states: the gun is held this way only while they play a TacticalMoves pack clip.")]
        [SerializeField] string[] hideStates = { "Cover Low", "Stand Corner Hide L", "Stand Corner Hide R" };
        [Header("TacticalMoves pack clips (set by the setup script)")]
        [Tooltip("The pack gun's barrel and up in this character's right-hand bone space: in pack clips our gun is turned to match.")]
        [SerializeField] Vector3 packBarrelInHand = Vector3.forward;
        [SerializeField] Vector3 packUpInHand = Vector3.up;
        [Tooltip("The same for the pack's SMG clips (its SMG sits in the hand differently).")]
        [SerializeField] Vector3 packSmgBarrelInHand = Vector3.forward;
        [SerializeField] Vector3 packSmgUpInHand = Vector3.up;

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
        int[] fireHashes, hideHashes, ownHashes;
        bool firing, packClip, packSmg;

        //The gun's barrel and "up" (out of the grip) in its own rotation frame, worked out once per gun
        ActiveWeapon gunFor;
        Vector3 barrelLocal = Vector3.forward, upLocal = Vector3.up;
        Transform lhMarker;

        public bool Active => weight > 0.001f;

        //Pack shoot / reload overlay on the "Pack Upper" layer
        static readonly int PackShootHash = Animator.StringToHash("PackShoot");
        static readonly int PackReloadHash = Animator.StringToHash("PackReload");
        static readonly int PackShootState = Animator.StringToHash("Pack Shoot");
        static readonly int PackReloadState = Animator.StringToHash("Pack Reload");
        [Tooltip("Seconds the pack reload takes (the left hand leaves the gun meanwhile).")]
        [SerializeField] float packReloadTime = 1.9f;
        int upperLayer = -1;
        float reloadUntil;
        ActiveWeapon hooked;
        bool Reloading => Time.time < reloadUntil;

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
            fireHashes = System.Array.ConvertAll(fireStates, Animator.StringToHash);
            hideHashes = System.Array.ConvertAll(hideStates, Animator.StringToHash);
            ownHashes = System.Array.ConvertAll(ownClipFireStates, Animator.StringToHash);
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
            aim.clampWeight = 0.1f;
            aim.maxIterations = 4;
            leftArm.SetChain(la, lf, lh, transform);
            leftArm.goal = AvatarIKGoal.LeftHand;
            leftArm.bendModifier = IKSolverLimb.BendModifier.Animation;
            upperLayer = anim.GetLayerIndex("Pack Upper");
            ready = true;
        }

        //The state that is (or is becoming) current
        int TargetState()
        {
            return anim.IsInTransition(0) ? anim.GetNextAnimatorStateInfo(0).shortNameHash : anim.GetCurrentAnimatorStateInfo(0).shortNameHash;
        }

        //Is the heaviest clip playing in that state one of the TacticalMoves pack's (named Anim_...)?
        bool PackClipPlaying()
        {
            var infos = anim.IsInTransition(0) ? anim.GetNextAnimatorClipInfo(0) : anim.GetCurrentAnimatorClipInfo(0);
            float best = 0f; string name = null;
            foreach (var ci in infos) if (ci.weight > best && ci.clip != null) { best = ci.weight; name = ci.clip.name; }
            packSmg = name != null && name.StartsWith("Anim_SMG");
            return name != null && name.StartsWith("Anim_");
        }

        bool InKneelState()
        {
            int s = TargetState();
            firing = System.Array.IndexOf(fireHashes, s) >= 0;
            bool hiding = System.Array.IndexOf(hideHashes, s) >= 0;
            packClip = (firing || hiding) && PackClipPlaying();
            if (firing && !packClip && System.Array.IndexOf(ownHashes, s) < 0) firing = false; //e.g. a rifle leaning out on its own clip and rigs
            return firing || (hiding && packClip);
        }

        void OnDisable() { Hook(null); }

        void Hook(ActiveWeapon gun)
        {
            if (hooked == gun) return;
            if (hooked != null) { hooked.Fired -= OnFired; hooked.Reloading -= OnReloading; }
            hooked = gun;
            if (hooked != null) { hooked.Fired += OnFired; hooked.Reloading += OnReloading; }
        }

        bool PackGun => fighter != null && fighter.weaponConfig != null && fighter.weaponConfig.CoverAnimSet > 0;

        void OnFired()
        {
            if (upperLayer >= 0 && Active && packClip && !Reloading) anim.SetTrigger(PackShootHash);
        }

        void OnReloading()
        {
            if (upperLayer < 0 || !PackGun || Reloading) return;
            reloadUntil = Time.time + packReloadTime;
            anim.ResetTrigger(PackShootHash);
            anim.SetTrigger(PackReloadHash);
        }

        void Update()
        {
            if (!ready) return;
            Hook(fighter != null ? fighter.activeWeapon : null);
            //NPCs wait out an empty magazine before refilling: show the reload while they wait
            if (hooked != null && hooked.CurClip <= 0 && !Reloading && PackGun && Active) OnReloading();
            if (upperLayer >= 0)
            {
                //The layer's resting state is empty (a default arms-down pose), so the layer fades out before the clip ends
                var st = anim.GetCurrentAnimatorStateInfo(upperLayer);
                bool overlay = st.shortNameHash == PackShootState || st.shortNameHash == PackReloadState;
                float t = st.normalizedTime;
                float target = !overlay ? 0f : t < 0.78f ? 1f : Mathf.Clamp01(1f - (t - 0.78f) / 0.12f);
                anim.SetLayerWeight(upperLayer, Mathf.MoveTowards(anim.GetLayerWeight(upperLayer), target, Time.deltaTime / 0.06f));
            }
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

            //1. Right hand: in a pack clip, the gun sits the way the pack's own gun sits in that hand;
            //   in our own kneel clip, barrel along the body's facing, grip upright
            Quaternion gunAim = packClip
                ? (packSmg ? Quaternion.LookRotation(rh.rotation * packSmgBarrelInHand, rh.rotation * packSmgUpInHand)
                           : Quaternion.LookRotation(rh.rotation * packBarrelInHand, rh.rotation * packUpInHand))
                : Quaternion.LookRotation(transform.forward, Vector3.up);
            Quaternion want = gunAim * Quaternion.Inverse(Quaternion.LookRotation(barrelLocal, upLocal));
            Quaternion delta = want * Quaternion.Inverse(g.rotation);
            rh.rotation = Quaternion.Slerp(Quaternion.identity, delta, weight) * rh.rotation;

            //2. Spine: barrel onto the target
            Transform target = fighter.EnemyPos;
            if (firing && target != null && spineAimWeight > 0f && !Reloading)
            {
                aim.transform = g;
                aim.axis = barrelLocal;
                aim.IKPosition = target.position + Vector3.up * targetHeight;
                aim.IKPositionWeight = weight * spineAimWeight;
                aim.Update();
            }

            //3. Left hand onto the gun
            if (lhMarker != null && supportHandWeight > 0f && !Reloading) //The left hand fetches the magazine
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
