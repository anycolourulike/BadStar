using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Rambler.Attributes;
using Rambler.Combat;
using Rambler.Movement;

namespace Rambler.Control
{
    //The brain for enemy soldiers. Six states:
    //Idle    - stands at its post (walks back to it if moved), then patrols if it has a route.
    //Patrol  - walks its PatrolPath, pausing at each waypoint.
    //Attack  - takes cover within range of the target and fights from it. Before every push to closer
    //          cover it clears the way with a grenade (while it has any), waits for the blast, then moves up.
    //Defend  - holds cover and never advances: when badly hurt, or when guarding a post. Grenades only
    //          if the target gets close.
    //Search  - goes to where the target was last seen or heard and sweeps around it, then gives up.
    //Dead    - stops everything.
    //Enemies that spot someone, or get shot, pass the word to squadmates nearby.
    //Replaces AIController + FieldOfView on enemies (both are switched off while this runs; a hacked
    //robot switches this off and goes back to them as a companion).
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAI : MonoBehaviour
    {
        static readonly List<EnemyAI> all = new();
        public static IReadOnlyList<EnemyAI> All => all;

        [Header("Senses")]
        [Tooltip("How far it can see while calm (idle, patrolling, searching).")]
        [SerializeField] float sightRange = 14f;
        [Tooltip("Width of its view cone while calm, in degrees.")]
        [Range(0f, 360f)] [SerializeField] float sightAngle = 140f;
        [Tooltip("Once fighting it watches all round, this far.")]
        [SerializeField] float combatSightRange = 25f;
        [Tooltip("Notices anyone this close, whichever way it faces.")]
        [SerializeField] float closeSense = 3f;
        [SerializeField] float eyeHeight = 1.55f;
        [Tooltip("What blocks its view (walls, doors).")]
        [SerializeField] LayerMask sightBlockers = (1 << 0) | (1 << 11);
        [Tooltip("Squadmates within this distance hear when it spots someone or gets shot.")]
        [SerializeField] float squadAlertRadius = 18f;

        [Header("Post and Patrol")]
        [Tooltip("Optional. Its post: it guards here (Defend instead of Attack) and returns here after a search.")]
        [SerializeField] Transform guardPoint;
        [Tooltip("Optional. Falls back to the AIController's patrol path.")]
        [SerializeField] PatrolPath patrolPath;
        [SerializeField] float idleTime = 3f;
        [Range(0f, 1f)] [SerializeField] float patrolSpeed = 0.25f;
        [SerializeField] float waypointTolerance = 1f;
        [SerializeField] float waypointDwell = 2f;

        [Header("Attack")]
        [Tooltip("How far from itself it looks for cover.")]
        [SerializeField] float coverSearchRadius = 12f;
        [Tooltip("Seconds of shooting from a cover point before it clears and pushes (random in this range).")]
        [SerializeField] Vector2 engageTime = new Vector2(4f, 7f);
        [Tooltip("A push must end at least this much closer to the target.")]
        [SerializeField] float pushStep = 3f;
        [Tooltip("Stops pushing once this close; just fights from where it is.")]
        [SerializeField] float closestPush = 5f;
        [Tooltip("Seconds after the grenade leaves the hand before it moves up.")]
        [SerializeField] float pushAfterBlast = 0.4f;

        [Header("Defend")]
        [Tooltip("Defends instead of attacking below this share of health.")]
        [Range(0f, 1f)] [SerializeField] float defendBelowHealth = 0.35f;
        [Tooltip("While defending, grenades anyone who gets this close.")]
        [SerializeField] float defendGrenadeRange = 8f;
        [Tooltip("How far from its post a guard will take cover.")]
        [SerializeField] float guardRadius = 6f;

        [Header("Search")]
        [Tooltip("Seconds without seeing the target before Attack becomes Search.")]
        [SerializeField] float loseSightTime = 5f;
        [SerializeField] float searchRadius = 7f;
        [SerializeField] float searchTime = 18f;
        [SerializeField] int searchSpots = 3;

        NavMeshAgent agent;
        Animator anim;
        Fighter fighter;
        Mover mover;
        Health health;
        CoverUser cover;
        GrenadeThrower thrower;
        AIController oldBrain;
        FieldOfView oldSight;
        StateMachine machine;
        IState idle, patrol, attack, defend, search, dead;

        [Header("Weapon")]
        [Tooltip("Seconds to reload once the magazine runs dry.")]
        [SerializeField] float reloadTime = 1.6f;

        static readonly int WeaponPoseHash = Animator.StringToHash("WeaponPose");

        readonly List<GameObject> candidates = new();
        bool hasWeaponPose;
        float reloadedAt; //0 = not reloading
        bool firstMagazine;
        float nextCandidateRefresh;
        float nextScan;
        float maxHealth;
        bool engaged; //Has fought (or been alerted) since it last calmed down

        public GameObject Target { get; private set; }
        public bool TargetVisible { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public float LastSeenTime { get; private set; } = -999f;
        //A position to investigate (heard, shot at, told by a squadmate) that hasn't been searched yet
        public bool HasLead { get; private set; }
        public string StateName => machine?._currentState?.GetType().Name.Replace("Enemy", "") ?? "-";

        public Vector3 Home { get; private set; }
        public Quaternion HomeRotation { get; private set; }
        public bool IsGuard => guardPoint != null;
        public PatrolPath Route => patrolPath;
        public NavMeshAgent Agent => agent;
        public Fighter Fighter => fighter;
        public Mover Mover => mover;
        public CoverUser Cover => cover;
        public GrenadeThrower Thrower => thrower;
        public float IdleTime => idleTime;
        public float PatrolSpeed => patrolSpeed;
        public float WaypointTolerance => waypointTolerance;
        public float WaypointDwell => waypointDwell;
        public float CoverSearchRadius => coverSearchRadius;
        public Vector2 EngageTime => engageTime;
        public float PushStep => pushStep;
        public float ClosestPush => closestPush;
        public float PushAfterBlast => pushAfterBlast;
        public float DefendGrenadeRange => defendGrenadeRange;
        public float GuardRadius => guardRadius;
        public float SearchRadius => searchRadius;
        public float SearchTime => searchTime;
        public int SearchSpots => searchSpots;
        public float AttackRange => fighter != null && fighter.AttackRange > 0f ? fighter.AttackRange : 12f;
        public bool IsDead => health != null && health.IsDead();
        public float HealthFraction => maxHealth > 0f ? health.healthPoints.value / maxHealth : 1f;
        public bool ShouldDefend => IsGuard || HealthFraction < defendBelowHealth;
        public float TimeSinceSeen => Time.time - LastSeenTime;
        public Vector3 Eye => transform.position + Vector3.up * eyeHeight;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            anim = GetComponent<Animator>();
            fighter = GetComponent<Fighter>();
            mover = GetComponent<Mover>();
            health = GetComponent<Health>();
            cover = GetComponent<CoverUser>();
            thrower = GetComponent<GrenadeThrower>();
            oldBrain = GetComponent<AIController>();
            oldSight = GetComponent<FieldOfView>();
            if (patrolPath == null && oldBrain != null) patrolPath = oldBrain.PatrolRoute;
            SetHome(guardPoint != null ? guardPoint : transform);

            idle = new EnemyIdle(this);
            patrol = new EnemyPatrol(this);
            attack = new EnemyAttack(this);
            defend = new EnemyDefend(this);
            search = new EnemySearch(this);
            dead = new EnemyDead(this);

            machine = new StateMachine();
            machine.AddAnyTransition(dead, () => IsDead);
            foreach (var calm in new[] { idle, patrol, search })
            {
                machine.AddTransition(attack, calm, () => TargetVisible && !ShouldDefend);
                machine.AddTransition(defend, calm, () => TargetVisible && ShouldDefend);
            }
            machine.AddTransition(patrol, idle, () => patrolPath != null && ((EnemyIdle)idle).Rested);
            machine.AddTransition(search, idle, () => HasLead);
            machine.AddTransition(search, patrol, () => HasLead);
            machine.AddTransition(defend, attack, () => ShouldDefend);
            machine.AddTransition(search, attack, () => !TargetVisible && TimeSinceSeen > loseSightTime);
            machine.AddTransition(search, defend, () => !TargetVisible && TimeSinceSeen > loseSightTime * 2f && !IsGuard);
            machine.AddTransition(idle, defend, () => !TargetVisible && TimeSinceSeen > loseSightTime * 2f && IsGuard);
            machine.AddTransition(patrol, search, () => ((EnemySearch)search).Finished && patrolPath != null);
            machine.AddTransition(idle, search, () => ((EnemySearch)search).Finished);
        }

        void Start()
        {
            maxHealth = health != null ? health.healthPoints.value : 0f;
            if (anim != null)
            {
                foreach (var p in anim.parameters) if (p.nameHash == WeaponPoseHash) hasWeaponPose = true;
            }
        }

        void OnEnable()
        {
            all.Add(this);
            //This brain drives the enemy now; the old AI and field of view would fight it for the Fighter
            if (oldBrain != null) oldBrain.enabled = false;
            if (oldSight != null) oldSight.enabled = false;
            if (cover != null) cover.AutoSeek = false;
            if (health != null) health.Damaged += OnDamaged;
            machine.SetState(idle);
        }

        void OnDisable()
        {
            all.Remove(this);
            if (health != null) health.Damaged -= OnDamaged;
            if (fighter != null) fighter.CancelTarget();
            if (cover != null) cover.Leave();
        }

        void Update()
        {
            if (!IsDead && Time.time >= nextScan)
            {
                nextScan = Time.time + 0.2f;
                Look();
            }
            if (!IsDead) UpdateWeapon();
            machine.Tick();
        }

        //Picks the armed or unarmed locomotion, and reloads from the unlimited NPC reserve when dry
        void UpdateWeapon()
        {
            var config = fighter.weaponConfig;
            if (hasWeaponPose) anim.SetFloat(WeaponPoseHash, config == null || config.isMelee ? 0f : config.isPistol ? 1f : 2f);

            var weapon = fighter.activeWeapon;
            if (weapon == null || config == null || config.isMelee || config.projectile == null) return;
            if (!firstMagazine)
            {
                firstMagazine = true;
                if (weapon.CurClip <= 0) weapon.RefillMagazine();
            }
            if (reloadedAt == 0f && weapon.CurClip <= 0) reloadedAt = Time.time + reloadTime;
            if (reloadedAt > 0f && Time.time >= reloadedAt)
            {
                weapon.RefillMagazine();
                reloadedAt = 0f;
            }
        }

        public bool IsReloading => reloadedAt > 0f;

        //Where it returns to after a search (a guard's post, or where it stood at the start)
        public void SetHome(Transform post)
        {
            Home = post.position;
            HomeRotation = Quaternion.Euler(0f, post.eulerAngles.y, 0f);
        }

        // ---------- Senses ----------

        void Look()
        {
            if (Time.time >= nextCandidateRefresh)
            {
                nextCandidateRefresh = Time.time + 1f;
                candidates.Clear();
                candidates.AddRange(GameObject.FindGameObjectsWithTag("Player"));
                candidates.AddRange(GameObject.FindGameObjectsWithTag("Companion"));
            }

            GameObject best = null;
            float bestDist = float.MaxValue;
            foreach (var c in candidates)
            {
                if (c == null || !c.activeInHierarchy || !IsAlive(c)) continue;
                float dist = Vector3.Distance(transform.position, c.transform.position);
                if (dist < bestDist && CanSee(c, dist))
                {
                    best = c;
                    bestDist = dist;
                }
            }

            bool firstSighting = best != null && !engaged;
            TargetVisible = best != null;
            if (best != null)
            {
                Target = best;
                LastKnownPosition = best.transform.position;
                LastSeenTime = Time.time;
                HasLead = false;
                engaged = true;
                if (firstSighting) AlertSquad(LastKnownPosition);
            }
            else if (Target != null && !IsAlive(Target))
            {
                Target = null;
            }
        }

        bool CanSee(GameObject other, float dist)
        {
            bool alert = engaged;
            if (dist > (alert ? combatSightRange : sightRange)) return false;
            if (!alert && dist > closeSense)
            {
                Vector3 to = other.transform.position - transform.position;
                to.y = 0f;
                if (Vector3.Angle(transform.forward, to) > sightAngle * 0.5f) return false;
            }
            return HasLineOfSight(other);
        }

        public bool HasLineOfSight(GameObject other) => HasLineOfSight(Eye, other.transform.position + Vector3.up * 1.2f);

        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            return !Physics.Linecast(from, to, sightBlockers, QueryTriggerInteraction.Ignore);
        }

        static bool IsAlive(GameObject character)
        {
            var h = character.GetComponent<Health>();
            return h == null || !h.IsDead();
        }

        //Shot by someone it can't see: it knows roughly where the shooting came from
        void OnDamaged(float amount)
        {
            if (TargetVisible || IsDead) return;
            GameObject nearest = null;
            float bestDist = float.MaxValue;
            foreach (var c in candidates)
            {
                if (c == null || !IsAlive(c)) continue;
                float d = Vector3.Distance(transform.position, c.transform.position);
                if (d < bestDist) { bestDist = d; nearest = c; }
            }
            if (nearest == null) return;
            var guess = nearest.transform.position + UnityEngine.Random.insideUnitSphere * 3f;
            guess.y = nearest.transform.position.y;
            Target = nearest;
            HearOf(guess);
            AlertSquad(guess);
        }

        //Something worth investigating at a position (gunfire, a squadmate's call, the ship)
        public void HearOf(Vector3 position)
        {
            if (IsDead || TargetVisible) return;
            LastKnownPosition = position;
            HasLead = true;
            engaged = true;
            ((EnemySearch)search).Restart();
        }

        void AlertSquad(Vector3 position)
        {
            foreach (var mate in all)
            {
                if (mate == this || mate.IsDead || mate.TargetVisible) continue;
                if (Vector3.Distance(mate.transform.position, transform.position) > squadAlertRadius) continue;
                mate.HearOf(position);
            }
        }

        //Back to calm once a search turns up nothing
        public void CalmDown()
        {
            engaged = false;
            HasLead = false;
            Target = null;
        }

        public void LeadFollowed() => HasLead = false;

        // ---------- Actions the states share ----------

        //Shoot at the target from where it stands (the Fighter aims; CoverUser pops it out of cover)
        public void Shoot(GameObject target)
        {
            if (target == null) { HoldFire(); return; }
            if (fighter.EnemyPos == target.transform) return;
            fighter.TargetCap = target.GetComponent<CapsuleCollider>();
            fighter.CombatTarget = target.GetComponent<CombatTarget>();
            fighter.Attack(target);
        }

        public void HoldFire()
        {
            if (fighter.EnemyPos != null) fighter.CancelTarget();
        }

        public bool InRange(GameObject target) =>
            target != null && Vector3.Distance(transform.position, target.transform.position) <= AttackRange;

        //Drives the agent directly (as Mover.MoveTo does): Mover isn't set up until its Start
        public void MoveTo(Vector3 destination, float speedFraction)
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            if (NavMesh.SamplePosition(destination, out var hit, 2f, NavMesh.AllAreas)) destination = hit.position;
            agent.isStopped = false;
            agent.destination = destination;
            agent.speed = mover.maxSpeed * Mathf.Clamp01(speedFraction);
        }

        public bool Arrived(float tolerance = 0.4f)
        {
            if (!agent.enabled || !agent.isOnNavMesh || agent.pathPending) return false;
            return !agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + tolerance;
        }

        public void Stop()
        {
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        }

        public void Face(Vector3 point) => mover.FaceFlat(point);

        //Squadmates take turns with grenades so the target isn't buried under five at once
        static readonly Dictionary<GameObject, float> grenadeTurn = new();
        public bool GrenadeTurnFree(GameObject target) =>
            target == null || !grenadeTurn.TryGetValue(target, out float until) || Time.time >= until;
        public void TakeGrenadeTurn(GameObject target, float seconds)
        {
            if (target != null) grenadeTurn[target] = Time.time + seconds;
        }

        //No grenades onto squadmates
        public bool SquadmateNear(Vector3 point, float radius)
        {
            foreach (var mate in all)
            {
                if (mate == this || mate.IsDead) continue;
                if (Vector3.Distance(mate.transform.position, point) < radius) return true;
            }
            return false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, sightRange);
            Vector3 l = Quaternion.Euler(0f, -sightAngle * 0.5f, 0f) * transform.forward, r = Quaternion.Euler(0f, sightAngle * 0.5f, 0f) * transform.forward;
            Gizmos.DrawLine(Eye, Eye + l * sightRange);
            Gizmos.DrawLine(Eye, Eye + r * sightRange);
            if (Application.isPlaying && (HasLead || Target != null))
            {
                Gizmos.color = TargetVisible ? Color.red : new Color(1f, 0.5f, 0f);
                Gizmos.DrawLine(Eye, LastKnownPosition + Vector3.up);
                Gizmos.DrawWireSphere(LastKnownPosition, 0.4f);
            }
        }
    }
}
