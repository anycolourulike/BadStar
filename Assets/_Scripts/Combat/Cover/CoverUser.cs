using UnityEngine;
using UnityEngine.AI;
using Rambler.Attributes;
using Rambler.Movement;

namespace Rambler.Combat
{
    //Lets a character use CoverPoints. When it comes to a stop inside a cover zone it snaps to the point,
    //settles into the cover pose; moving away stands it back up.
    //Low cover: crouched facing the cover, popping up while it has a target to shoot.
    //High cover: back to the wall looking past the open edge, stepping out while it has a target to shoot.
    //Enemies (seekCover) pick a free point between themselves and their target, within weapon range, and run to it.
    //While in cover the character takes the point's share of damage (see Health.TakeDamage).
    [RequireComponent(typeof(NavMeshAgent))]
    public class CoverUser : MonoBehaviour
    {
        [Tooltip("Agent speed below which the character counts as stopped.")]
        [SerializeField] float stoppedSpeed = 0.15f;
        [Tooltip("How quickly the character slides onto the cover point.")]
        [SerializeField] float snapSpeed = 5f;
        [SerializeField] float turnSpeed = 540f;
        [Tooltip("High cover: how far the character steps past the edge to shoot.")]
        [SerializeField] float stepOut = 0.6f;

        [Header("Cover fire rhythm (guards and companions, not the player)")]
        [Tooltip("In cover with a target: hide, pop out and fire a burst, duck back, repeat. Off: fires the whole time.")]
        [SerializeField] bool useFireRhythm = true;
        [Tooltip("Seconds spent hidden before each burst; one is picked at random each time, so players can learn the rhythm.")]
        [SerializeField] float[] hideTimes = { 1.5f, 2.5f, 4f };
        [Tooltip("Seconds spent out of cover firing each burst.")]
        [SerializeField] float burstTime = 2f;

        [Header("AI")]
        [Tooltip("Run to nearby cover when fighting. On by default for enemies.")]
        [SerializeField] bool seekCover;
        [SerializeField] float searchRadius = 10f;
        [Tooltip("Seconds between looking for better cover.")]
        [SerializeField] float searchInterval = 4f;
        [Tooltip("Give up running to a cover point after this long.")]
        [SerializeField] float giveUpAfter = 6f;

        static readonly int InCoverLow = Animator.StringToHash("InCoverLow");
        static readonly int StandCoverLeft = Animator.StringToHash("StandCoverLeft");
        static readonly int StandCoverRight = Animator.StringToHash("StandCoverRight");
        static readonly int CoverPeekLeft = Animator.StringToHash("CoverPeekLeft");
        static readonly int CoverPeekRight = Animator.StringToHash("CoverPeekRight");
        static readonly int CoverPistol = Animator.StringToHash("CoverPistol");
        static readonly int CoverVariant = Animator.StringToHash("CoverVariant");
        static readonly int CoverLowFire = Animator.StringToHash("CoverLowFire");
        bool hasLowFire;

        [Tooltip("How many versions of each cover pose the animator has (CoverVariant 0, 1, 2...). One is picked at random per cover spot.")]
        [SerializeField] int poseVariants = 3;

        NavMeshAgent agent;
        Animator anim;
        Fighter fighter;
        Mover mover;
        Health health;
        CoverPoint current;
        CoverPoint heading;
        float nextSearch;
        float headingSince;
        bool hasLow, hasLeft, hasRight, hasPeekLeft, hasPeekRight, hasPistol, hasVariant;
        int variant; //Which version of the cover pose plays at the current spot
        CoverPoint lastPoint; //Where it last took cover, and when it left: dropping straight back in keeps the same pose
        float lastLeft = -99f;
        bool exposed;    //Fire rhythm: out of cover firing (true) or hidden (false)
        float phaseEnds; //Fire rhythm: when the current hide/burst ends (0 = not started)

        public bool InCover => current != null;
        public bool IsMovingToCover => heading != null && current == null;
        public CoverPoint Current => current;
        public float DamageMultiplier => current != null ? current.DamageTaken : 1f;
        //Off when something else (EnemyAI) decides where to take cover
        public bool AutoSeek { get => seekCover; set => seekCover = value; }
        //Stands the character out of its cover pose while it does something else (e.g. throws a grenade)
        public bool PoseSuspended { get; set; }
        //The fire rhythm runs for AI only; the character the player controls fires when told to
        bool RhythmActive => useFireRhythm && !CompareTag("Player") && !Suppressing;
        //Laying down covering fire for a squadmate who is moving: stays out and keeps shooting instead of hiding
        public bool Suppressing { get; set; }
        //In cover with a target but in the hidden part of the fire rhythm: Fighter neither aims nor fires
        public bool HoldingFire => current != null && RhythmActive && fighter != null && fighter.EnemyPos != null && !exposed;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            anim = GetComponent<Animator>();
            fighter = GetComponent<Fighter>();
            mover = GetComponent<Mover>();
            health = GetComponent<Health>();
            if (CompareTag("Enemy")) seekCover = true;
            if (anim != null)
            {
                foreach (var p in anim.parameters)
                {
                    if (p.nameHash == InCoverLow) hasLow = true;
                    else if (p.nameHash == StandCoverLeft) hasLeft = true;
                    else if (p.nameHash == StandCoverRight) hasRight = true;
                    else if (p.nameHash == CoverPeekLeft) hasPeekLeft = true;
                    else if (p.nameHash == CoverPeekRight) hasPeekRight = true;
                    else if (p.nameHash == CoverPistol) hasPistol = true;
                    else if (p.nameHash == CoverVariant) hasVariant = true;
                    else if (p.nameHash == CoverLowFire) hasLowFire = true;
                }
            }
        }

        void OnDisable() => Leave();

        void Update()
        {
            if (health != null && health.IsDead())
            {
                Leave();
                heading = null;
                return;
            }
            if (!agent.enabled || !agent.isOnNavMesh) return;

            //Fighting hand to hand: no hiding, the punch has to land
            bool melee = fighter != null && fighter.EnemyPos != null && fighter.weaponConfig != null && fighter.weaponConfig.isMelee;
            if (melee)
            {
                if (current != null || heading != null) { Leave(); heading = null; }
                return;
            }

            if (current != null) HoldCover();
            else if (IsStopped()) TryEnter();
            if (IsMovingToCover && Time.time - headingSince > giveUpAfter) GiveUpHeading(); //Never got there

            if (seekCover) SeekCover();
        }

        bool IsStopped()
        {
            if (agent.pathPending) return false;
            bool arrived = !agent.hasPath || agent.isStopped || agent.remainingDistance <= agent.stoppingDistance + 0.05f;
            return arrived && agent.velocity.sqrMagnitude < stoppedSpeed * stoppedSpeed;
        }

        void TryEnter()
        {
            var point = CoverPoint.FindAt(transform.position, gameObject);
            if (point == null || !point.TryOccupy(gameObject)) return;
            if (heading != null && heading != point) heading.Release(gameObject); //Stopped short at other cover
            current = point;
            heading = null;
            //A fresh pose for each spot, kept until the character leaves it (a brief slip off the same spot keeps it)
            if (point != lastPoint || Time.time - lastLeft > 3f) variant = Random.Range(0, Mathf.Max(1, poseVariants));
            if (hasVariant) anim.SetFloat(CoverVariant, variant);
        }

        void HoldCover()
        {
            //Heading somewhere else: give the cover up
            bool leaving = !agent.isStopped && agent.hasPath && !agent.pathPending &&
                           agent.remainingDistance > current.Radius + 0.3f && agent.velocity.sqrMagnitude > 0.09f;
            if (leaving)
            {
                Leave();
                return;
            }

            //Slide onto the point and settle into the pose, unless busy shooting (Fighter faces the target).
            //Low cover: crouch facing the cover. High cover: back to the wall, looking out past the edge;
            //to shoot, step out past the edge and aim round the corner.
            bool hasTarget = fighter != null && fighter.EnemyPos != null;
            bool shooting = UpdateFireRhythm(hasTarget);
            bool low = current.Type == CoverPoint.CoverType.Low;
            bool left = current.Side == CoverPoint.CoverSide.Left;
            Vector3 spot = current.transform.position;
            if (!low && shooting) spot += OpenSide(current) * stepOut;
            Vector3 offset = spot - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > 0.0004f) agent.Move(offset * Mathf.Min(1f, snapSpeed * Time.deltaTime));
            if (!shooting)
            {
                var face = Quaternion.LookRotation(Flat(low ? current.Facing : -current.Facing));
                transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turnSpeed * Time.deltaTime);
            }

            //Pop up over low cover / lean out round high cover to shoot
            if (PoseSuspended) { SetParams(false, false, false, false, false); return; }
            //Pistols fire from a kneel behind low cover (Cover Low Fire); other guns still stand up over it
            bool kneelFire = low && shooting && hasLowFire && fighter != null && fighter.weaponConfig != null && fighter.weaponConfig.isPistol;
            if (hasLowFire) anim.SetBool(CoverLowFire, kneelFire);
            SetParams(low && !shooting,
                      !low && !shooting && left, !low && !shooting && !left,
                      !low && shooting && left, !low && shooting && !left);
        }

        //Hide for one of the hide times, fire for burstTime, repeat. Returns true while it should be out firing.
        bool UpdateFireRhythm(bool hasTarget)
        {
            if (!hasTarget || !RhythmActive)
            {
                exposed = false;
                phaseEnds = 0f;
                return hasTarget; //No rhythm: fire the whole time it has a target
            }
            if (phaseEnds == 0f)
            {
                //A new fight from this cover starts hidden
                exposed = false;
                phaseEnds = Time.time + PickHideTime();
            }
            else if (Time.time >= phaseEnds)
            {
                exposed = !exposed;
                phaseEnds = Time.time + (exposed ? burstTime : PickHideTime());
            }
            return exposed;
        }

        float PickHideTime() =>
            hideTimes != null && hideTimes.Length > 0 ? Mathf.Max(0f, hideTimes[Random.Range(0, hideTimes.Length)]) : 2f;

        void SeekCover()
        {
            if (current != null || fighter == null || fighter.EnemyPos == null) { heading = null; return; }
            if (heading != null)
            {
                if (!heading.IsFree && heading.Occupant != gameObject || Time.time - headingSince > giveUpAfter) heading = null;
                else return;
            }
            if (Time.time < nextSearch) return;
            nextSearch = Time.time + searchInterval;

            var point = PickCover(fighter.EnemyPos.position);
            if (point == null || !point.TryOccupy(gameObject)) return;
            heading = point;
            headingSince = Time.time;
            if (mover != null) mover.MoveTo(point.transform.position, 1f);
            else agent.SetDestination(point.transform.position);
        }

        //Nearest free point with its cover between it and the target, and close enough to shoot from
        CoverPoint PickCover(Vector3 target)
        {
            float range = fighter.AttackRange > 0f ? fighter.AttackRange * 0.9f : 12f;
            return FindCover(target, transform.position, searchRadius, range, 0f, float.MaxValue);
        }

        //The free point nearest to searchFrom (within searchRadius of it) whose cover faces the threat,
        //between minRange and maxRange from it, and closer to it than closerThan
        public CoverPoint FindCover(Vector3 threat, Vector3 searchFrom, float searchRadius, float maxRange, float minRange, float closerThan)
        {
            CoverPoint best = null;
            float bestDist = searchRadius;
            foreach (var point in CoverPoint.All)
            {
                if (!point.IsFree && point.Occupant != gameObject) continue;
                Vector3 p = point.transform.position;
                if (Mathf.Abs(p.y - transform.position.y) > 2f) continue; //Another floor
                float dist = Vector3.Distance(p, searchFrom);
                if (dist >= bestDist) continue;
                Vector3 toTarget = Flat(threat - p);
                float range = toTarget.magnitude;
                if (range > maxRange || range < minRange || range >= closerThan) continue;
                if (Vector3.Dot(Flat(point.Facing).normalized, toTarget.normalized) < 0.35f) continue;
                best = point;
                bestDist = dist;
            }
            return best;
        }

        //Claims a point and runs to it; takes the cover on arrival like any other stop
        public bool MoveToCover(CoverPoint point, float speedFraction)
        {
            if (point == null) return false;
            if (point == current) return true;
            if (!point.IsFree && point.Occupant != gameObject) return false;
            Leave();
            if (!point.TryOccupy(gameObject)) return false;
            heading = point;
            headingSince = Time.time;
            if (mover != null) mover.MoveTo(point.transform.position, speedFraction);
            else agent.SetDestination(point.transform.position);
            return true;
        }

        public void GiveUpHeading()
        {
            if (heading != null && heading != current) heading.Release(gameObject);
            heading = null;
        }

        public void Leave()
        {
            if (current != null)
            {
                current.Release(gameObject);
                lastPoint = current;
                lastLeft = Time.time;
            }
            if (heading != null) heading.Release(gameObject);
            current = null;
            exposed = false;
            phaseEnds = 0f;
            SetParams(false, false, false, false, false);
        }

        void SetParams(bool low, bool left, bool right, bool peekLeft, bool peekRight)
        {
            if (anim == null) return;
            if (hasLowFire && !(current != null && current.Type == CoverPoint.CoverType.Low && !PoseSuspended)) anim.SetBool(CoverLowFire, false);
            if (hasLow) anim.SetBool(InCoverLow, low);
            if (hasLeft) anim.SetBool(StandCoverLeft, left);
            if (hasRight) anim.SetBool(StandCoverRight, right);
            if (hasPeekLeft) anim.SetBool(CoverPeekLeft, peekLeft);
            if (hasPeekRight) anim.SetBool(CoverPeekRight, peekRight);
            if (hasPistol) anim.SetFloat(CoverPistol, fighter != null && fighter.weaponConfig != null && fighter.weaponConfig.isPistol ? 1f : 0f);   //one-handed guns lean out with the pistol pose
        }

        //Direction of the open edge of high cover. Side is given as seen when facing the cover.
        static Vector3 OpenSide(CoverPoint point)
        {
            Vector3 right = Vector3.Cross(Vector3.up, Flat(point.Facing)).normalized;
            return point.Side == CoverPoint.CoverSide.Left ? -right : right;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v : Vector3.forward;
        }
    }
}
