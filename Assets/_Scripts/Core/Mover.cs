using System;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections.Specialized;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.AI;
using Rambler.Core;
using Rambler.Attributes;
using Rambler.Control;
using Rambler.Saving;
using Rambler.Combat;
using UnityEngine.Animations.Rigging;
using System.Collections;

namespace Rambler.Movement
{
    public class Mover : MonoBehaviour, IAction
    {  
        [SerializeField] WeaponIK weaponIK;         
        [SerializeField] Rig mainRig;
        
        public bool rigWeaponEquipped;
        public bool timerRunning;  //Disabled in Attack & When Patrolling
        public float maxSpeed;

        [Header("Foot sliding")]
        [Tooltip("Feed the animator speed in the clips' own scale (speed / avatar size) and speed the run up when the agent " +
                 "outruns it, so planted feet stay put. The locomotion blend thresholds must be the clips' natural speeds.")]
        [SerializeField] bool matchAnimToSpeed;
        [Tooltip("Natural speed of the fastest locomotion clip, in the clips' scale (Unarmed_Sprint_F_RM = 3.26).")]
        [SerializeField] float animTopSpeed = 3.26f;
        [Tooltip("Most the run animation may be sped up; beyond this the feet slide a little rather than whirl.")]
        [SerializeField] float maxAnimSpeedUp = 1.6f;
        [Tooltip("Natural speed of the walk clip, in the clips' scale (unarmed walk 0.92, pistol walk 1.45). Below it the walk " +
                 "is played slower rather than blended with the idle, whose feet don't move.")]
        [SerializeField] float animWalkSpeed = 0.92f;
        [Tooltip("Slowest the walk is played (fraction of normal); slower than this it eases into the idle.")]
        [SerializeField] float minWalkPlayback = 0.4f;
        [Tooltip("No jog clip between walk and sprint: play the walk sped up (to Max Walk Speed Up) or the sprint slowed " +
                 "(down to Min Run Playback) instead of mixing the two, whose different strides made the feet skid.")]
        [SerializeField] bool bridgeWalkRunGap;
        [SerializeField] float maxWalkSpeedUp = 1.5f;
        [SerializeField] float minRunPlayback = 0.5f;
        [Tooltip("Where the blend tree's sprint clip sits (its threshold); sent at least this for a pure run, whatever the run's measured speed.")]
        [SerializeField] float runBlendThreshold = 3.26f;
        [Tooltip("Locomotion changes with the weapon held (guards: unarmed / pistol / rifle blends). Uses the speeds below " +
                 "for the current weapon instead of Anim Walk Speed / Anim Top Speed.")]
        [SerializeField] bool speedsByWeapon;
        [SerializeField] Vector2 unarmedWalkTop = new Vector2(0.92f, 3.26f);
        [SerializeField] Vector2 pistolWalkTop = new Vector2(1.45f, 3.84f);
        [SerializeField] Vector2 rifleWalkTop = new Vector2(1.93f, 4.34f);
        Rambler.Combat.Fighter weaponOwner;

        //Walk and top natural speeds for what the character is carrying now
        float GroundScale()
        {
            var an = GetComponent<Animator>();
            return an != null && an.isHuman ? an.humanScale * transform.lossyScale.y : 1f;
        }

        /// <summary>A comfortable walking pace in m/s: the walk clip played a little brisk.</summary>
        public float WalkPace => LocomotionSpeeds().x * GroundScale() * 1.2f;

        /// <summary>The fastest ground speed in m/s the run animation can match without the feet skating.</summary>
        public float MaxCleanSpeed => LocomotionSpeeds().y * GroundScale() * maxAnimSpeedUp;

        Vector2 LocomotionSpeeds()
        {
            if (!speedsByWeapon) return new Vector2(animWalkSpeed, animTopSpeed);
            if (weaponOwner == null) weaponOwner = GetComponent<Rambler.Combat.Fighter>();
            var w = weaponOwner != null ? weaponOwner.weaponConfig : null;
            if (w == null || w.isMelee) return unarmedWalkTop;
            return w.isPistol ? pistolWalkTop : rifleWalkTop;
        }

        [Header("Aiming locomotion")]
        [Tooltip("While the body is turned onto a target (Fighter aiming), play the 8-way strafe blend (controller needs Aiming/MoveX/MoveZ).")]
        [SerializeField] bool aimLocomotion = true;
        [Tooltip("Top speed while aiming (m/s): a jog, which the strafe run clips cover.")]
        [SerializeField] float maxAimMoveSpeed = 2.4f;
        [Tooltip("Top speed backing away while aiming (m/s).")]
        [SerializeField] float maxAimBackSpeed = 1.0f;
        [Tooltip("Top speed side-stepping while aiming (m/s): nobody walks sideways quickly.")]
        [SerializeField] float maxAimSideSpeed = 0.75f;
        static readonly int MoveXHash = Animator.StringToHash("MoveX");
        static readonly int MoveZHash = Animator.StringToHash("MoveZ");
        static readonly int AimingHash = Animator.StringToHash("Aiming");
        static readonly int AimSetHash = Animator.StringToHash("AimSet");
        static readonly int AimPlaybackHash = Animator.StringToHash("AimPlayback");
        bool hasAimPlayback;

        //TacticalMoves aim-movement clip speeds (normalized units/s) by direction: F, FR, R, BR, B, BL, L, FL
        static readonly float[] PistolWalk = { 1.01f, 0.89f, 0.51f, 0.89f, 0.96f, 0.93f, 0.41f, 0.92f };
        static readonly float[] PistolRun = { 2.70f, 2.31f, 2.73f, 3.00f, 2.71f, 2.31f, 2.73f, 3.00f };
        static readonly float[] SmgWalk = { 1.07f, 1.12f, 0.48f, 1.12f, 1.12f, 1.13f, 0.48f, 1.12f };
        static readonly float[] SmgRun = { 2.67f, 3.57f, 2.70f, 3.57f, 2.67f, 3.57f, 2.70f, 3.57f };

        static float ByDirection(float[] table, float angle)
        {
            float a = Mathf.Repeat(angle, 360f) / 45f; int i = Mathf.FloorToInt(a) % 8;
            return Mathf.Lerp(table[i], table[(i + 1) % 8], a - Mathf.Floor(a));
        }

        /// <summary>For the pack aim sets: the blend input (a pure walk or a pure run in that direction, at the
        /// clip's own speed) and the playback rate that makes its feet match the actual speed. Mixing the walk
        /// and run of one direction skates the feet, so this never does.</summary>
        public static void AimFeed(int set, Vector2 velocity, out Vector2 feed, out float playback)
        {
            float speed = velocity.magnitude;
            feed = Vector2.zero; playback = 1f;
            if (set <= 0 || speed < 0.05f) return;
            float angle = Mathf.Atan2(velocity.x, velocity.y) * Mathf.Rad2Deg;
            float walk = ByDirection(set == 2 ? SmgWalk : PistolWalk, angle);
            float run = ByDirection(set == 2 ? SmgRun : PistolRun, angle);
            //Pick the gait whose playback rate is closer to 1 (as a ratio), within sensible limits
            float rw = speed / walk, rr = speed / run;
            bool useWalk = rw <= 1.7f && (rr < 0.55f || Mathf.Abs(Mathf.Log(rw)) <= Mathf.Abs(Mathf.Log(rr)));
            float native = useWalk ? walk : run;
            feed = velocity / speed * native;
            playback = Mathf.Clamp(speed / native, 0.4f, 1.8f);
        }
        bool hasAimSet;
        Combat.Fighter aimFighter;
        bool hasAimParams;

        static readonly int ForwardSpeedHash = Animator.StringToHash("forwardSpeed");
        static readonly int LocoSpeedHash = Animator.StringToHash("LocoSpeed");
        Animator animator;
        bool hasLocoSpeed;

        NavMeshAgent navMeshAgent;
        float timerToMove = 10f;        
        Rigidbody rb;
        

        public void Start()
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            rb = GetComponent<Rigidbody>();
            timerRunning = true;

            //Characters placed in scenes can be saved slightly tilted; keep them upright
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        public void FixedUpdate()
        {
            if(timerRunning)
            {
                if(timerToMove <= 0)
                {
                    MoveTo(RandomNavmeshLocation(3f, this.gameObject.transform), maxSpeed);
                    timerToMove = 10f;
                }
            }
        }

        void Update()
        {           
            UpdateAnimator();
            if(rigWeaponEquipped == true)
            { 
                if(navMeshAgent.velocity.magnitude > 0.15f)
                {
                    if (mainRig != null)
                    {
                        mainRig.weight = 0.7f;
                    }
                } 
                else
                {
                    if (mainRig != null)
                    {
                        mainRig.weight = 1f;
                    }
                }               
            } 
            WeaponIKWeight();                     
        }

        void WeaponIKWeight()
        {           
            if (weaponIK == null) return; //Unarmed characters (e.g. the scientist) have no aim rig
            if(navMeshAgent.velocity.magnitude > 0.15f)
            {                   
                weaponIK.AimWeight = 0f;
            } 
            else
            {   
                weaponIK.AimWeight = 1f;                  
            }    
        }

        public float MaxSpeed()
        {
            return maxSpeed;
        }

        public void RigWeaponEquipped()
        {            
            rigWeaponEquipped = true;
        }   

        public void RigWeaponUnequipped()
        {            
            rigWeaponEquipped = false;
        } 

        public void RigWeightToZero()
        {
            if (mainRig != null) mainRig.weight = 0;
            RigWeaponUnequipped();
        }   

        public void RotateTowards(Transform target)
        {
            int rotSpeed = 160;
            Vector3 dir = FlatDirection(target.position);
            if (dir == Vector3.zero) return;
            var targetToLook = Quaternion.LookRotation(dir);
            this.transform.rotation = Quaternion.Slerp(this.transform.rotation, targetToLook, rotSpeed * Time.deltaTime);
        }

        //Turn to face a point on the horizontal plane only, so height differences never tilt the body
        //Turns towards a point at a set rate instead of snapping; returns how far off it still is (degrees)
        public float TurnTowards(Vector3 point, float degreesPerSecond)
        {
            Vector3 dir = FlatDirection(point);
            if (dir == Vector3.zero) return 0f;
            var want = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, degreesPerSecond * Time.deltaTime);
            return Quaternion.Angle(transform.rotation, want);
        }

        public void FaceFlat(Vector3 point)
        {
            Vector3 dir = FlatDirection(point);
            if (dir == Vector3.zero) return;
            transform.rotation = Quaternion.LookRotation(dir);
        }

        Vector3 FlatDirection(Vector3 point)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? dir : Vector3.zero;
        }

        public void StartMoveAction(Vector3 destination, float speedFraction)
        {
            GetComponent<ActionScheduler>().StartAction(this);
            MoveTo(destination, speedFraction);
        }

        public void MoveTo(Vector3 destination, float speedFraction)
        {   
            navMeshAgent.isStopped = false;      
            navMeshAgent.destination = destination; 
            navMeshAgent.speed = maxSpeed * Mathf.Clamp01(speedFraction);
        }

        public void CancelNav()
        {            
            navMeshAgent.isStopped = true;
            if (rb != null && !rb.isKinematic) rb.velocity = Vector3.zero; //Kinematic bodies have no velocity to clear
        }
               
        private void UpdateAnimator()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null) return;
                foreach (var p in animator.parameters) { if (p.nameHash == LocoSpeedHash) hasLocoSpeed = true; if (p.nameHash == AimingHash) hasAimParams = true; if (p.nameHash == AimSetHash) hasAimSet = true; if (p.nameHash == AimPlaybackHash) hasAimPlayback = true; }
            }
            if (navMeshAgent == null) navMeshAgent = GetComponent<NavMeshAgent>();
            Vector3 localVelocity = transform.InverseTransformDirection(navMeshAgent.velocity);
            float speed = localVelocity.z;
            if (matchAnimToSpeed && animator.isHuman && animator.humanScale > 0.01f)
            {
                //Ground speed, not just the forward part: a squad member side-stepping while it turns still steps
                Vector3 flat = navMeshAgent.velocity; flat.y = 0f;
                speed = flat.magnitude;
                //Humanoid clips move at their natural speed times the avatar's size and the character's scale
                //(the guards are scaled to 0.55), so compare in the clips' own scale
                speed /= animator.humanScale * Mathf.Max(0.01f, transform.lossyScale.y);
                if (hasLocoSpeed)
                {
                    float playback;
                    Vector2 speeds = LocomotionSpeeds();
                    float walk = Mathf.Max(0.1f, speeds.x);
                    if (speed > walk * minWalkPlayback && speed < walk)
                    {
                        //Slower than a walk: the pure walk, played slower (idle mixed in would skid the feet)
                        playback = speed / walk;
                        speed = walk;
                    }
                    else if (bridgeWalkRunGap && speed >= walk && speed < speeds.y)
                    {
                        if (speed < walk * maxWalkSpeedUp) { playback = speed / walk; speed = walk; }                  //Brisk walk
                        else { playback = Mathf.Max(minRunPlayback, speed / speeds.y); speed = Mathf.Max(speeds.y, runBlendThreshold); } //Easy run
                    }
                    else
                    {
                        playback = Mathf.Clamp(speed / Mathf.Max(0.1f, speeds.y), 1f, maxAnimSpeedUp);
                        if (bridgeWalkRunGap && speed >= speeds.y) speed = Mathf.Max(speed, runBlendThreshold); //Pure run, no walk mixed in
                    }
                    animator.SetFloat(LocoSpeedHash, playback);
                }
            }
            //A short damp so switching between walk and run crossfades instead of popping
            animator.SetFloat(ForwardSpeedHash, speed, bridgeWalkRunGap ? 0.12f : 0f, Time.deltaTime);

            //Aiming: the Fighter is steering the body at a target, so movement can be any direction relative to the facing
            if (hasAimParams)
            {
                bool aiming = aimLocomotion && !navMeshAgent.updateRotation;
                //Which aim-movement set fits the gun in hand (TacticalMoves pistol / SMG, else the Fists set)
                if (hasAimSet)
                {
                    if (aimFighter == null) aimFighter = GetComponent<Combat.Fighter>();
                    animator.SetFloat(AimSetHash, aimFighter != null && aimFighter.weaponConfig != null ? aimFighter.weaponConfig.CoverAnimSet : 0);
                }
                Vector3 lv = transform.InverseTransformDirection(navMeshAgent.velocity); lv.y = 0f;
                float moveAngle = lv.sqrMagnitude > 0.04f ? Mathf.Abs(Vector3.SignedAngle(Vector3.forward, lv, Vector3.up)) : 0f;
                //Heading straight for the target is just walking forward: the regular (speed-matched) locomotion does that best
                bool strafing = aiming && lv.sqrMagnitude > 0.04f && moveAngle > 30f;
                animator.SetBool(AimingHash, strafing || (aiming && lv.sqrMagnitude <= 0.04f));
                if (aiming && moveAngle > 110f && navMeshAgent.speed > maxAimBackSpeed) navMeshAgent.speed = maxAimBackSpeed;
                else if (aiming && moveAngle > 60f && navMeshAgent.speed > maxAimSideSpeed) navMeshAgent.speed = maxAimSideSpeed;
                float k = animator.isHuman ? Mathf.Max(0.01f, animator.humanScale * transform.lossyScale.y) : 1f;
                int set = hasAimSet && aimFighter != null && aimFighter.weaponConfig != null ? aimFighter.weaponConfig.CoverAnimSet : 0;
                Vector2 feed = new Vector2(lv.x / k, lv.z / k); float aimPlayback = 1f;
                if (set > 0) AimFeed(set, feed, out feed, out aimPlayback);
                animator.SetFloat(MoveXHash, feed.x, 0.1f, Time.deltaTime);
                animator.SetFloat(MoveZHash, feed.y, 0.1f, Time.deltaTime);
                if (hasAimPlayback) animator.SetFloat(AimPlaybackHash, aimPlayback, 0.1f, Time.deltaTime);
                if (aiming && navMeshAgent.speed > maxAimMoveSpeed) navMeshAgent.speed = maxAimMoveSpeed;
            }
        }

        public Vector3 RandomNavmeshLocation(float radius, Transform thisTransform)
        {
            Vector3 randomDirection = new Vector3(3, 3, 3) + UnityEngine.Random.onUnitSphere.normalized * radius;
            randomDirection += thisTransform.position;
            NavMeshHit hit;
            Vector3 finalPosition = Vector3.zero;
            if (NavMesh.SamplePosition(randomDirection, out hit, radius, 1))
            {
                finalPosition = hit.position;
            }
            return finalPosition;
        }


        IEnumerator NavMeshDelay()
        {
            yield return new WaitForSeconds(1f);
            GetComponent<NavMeshAgent>().enabled = true;
        }        
    }
}