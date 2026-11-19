using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Rambler.Attributes;
using Rambler.Combat;
using Rambler.Movement;
using BDTree = Opsive.BehaviorDesigner.Runtime.BehaviorTree;

namespace Rambler.Control
{
    //The senses and hands of a squad member the player isn't controlling. The decisions are made by the
    //Behavior Designer tree on the same object (tasks in Control/Companion/Tasks):
    //    Start -> Repeater -> Selector
    //                          |- Sequence (Abort Type: Both) -> Companion Sees Enemy -> Companion Engage
    //                          '- Follow Leader
    //This component sees enemies, keeps the gun loaded, and starts the tree when the character becomes
    //a companion / stops it when the player takes control of it or it dies.
    //Replaces AIController + FieldOfView on companions (both are switched off while this runs).
    //On enemy robots that can be hacked it stays off until RobotHack switches it on.
    [RequireComponent(typeof(NavMeshAgent))]
    public class CompanionBrain : MonoBehaviour
    {
        [Header("Senses")]
        [Tooltip("How far it sees enemies, all round.")]
        [SerializeField] float sightRange = 22f;
        [SerializeField] float eyeHeight = 1.55f;
        [Tooltip("What blocks its view (walls, doors).")]
        [SerializeField] LayerMask sightBlockers = (1 << 0) | (1 << 11);
        [Tooltip("Keeps fighting this long after losing sight of the target.")]
        [SerializeField] float memory = 2.5f;

        [Header("Squad")]
        [Tooltip("Breaks off a fight to regroup when the player is further away than this.")]
        [SerializeField] float leash = 18f;
        [Tooltip("Only takes on enemies within this distance of the player.")]
        [SerializeField] float engageAroundLeader = 20f;

        [Header("Cover")]
        [SerializeField] float coverSearchRadius = 10f;

        [Header("Weapon")]
        [SerializeField] float reloadTime = 1.6f;

        NavMeshAgent agent;
        Fighter fighter;
        Mover mover;
        Health health;
        CoverUser cover;
        BDTree tree;
        AIController oldBrain;
        FieldOfView oldSight;
        PlayerSelector selector;

        readonly List<GameObject> candidates = new();
        float nextScan, nextCandidateRefresh;
        float reloadAt; //0 = not reloading
        bool treeRunning;

        public GameObject Leader { get; private set; }
        public GameObject Target { get; private set; }
        public bool TargetVisible { get; private set; }
        public float LastSeenTime { get; private set; } = -999f;
        //Under the player's control: the tree is stopped and PlayerController drives it
        public bool IsControlled => Leader == gameObject;
        public bool IsDead => health != null && health.IsDead();
        public CoverUser Cover => cover;
        public Fighter Fighter => fighter;
        public float CoverSearchRadius => coverSearchRadius;
        public float AttackRange => fighter != null && fighter.AttackRange > 0f ? fighter.AttackRange : 12f;
        public float MaxSpeed => mover != null && mover.maxSpeed > 0f ? mover.maxSpeed : agent.speed;
        public Vector3 Eye => transform.position + Vector3.up * eyeHeight;

        //Something to fight (or, without a gun, to hide from): seen recently, alive, and the player isn't running off without us
        public bool HasEnemy =>
            Target != null && IsAlive(Target) && Time.time - LastSeenTime <= memory && LeaderInReach;

        //Has a gun. Unarmed or melee companions hide instead of fighting: charging gunmen just gets them killed
        public bool CanShoot =>
            fighter != null && fighter.weaponConfig != null && !fighter.weaponConfig.isMelee && fighter.weaponConfig.projectile != null;

        public bool LeaderInReach =>
            Leader == null || Vector3.Distance(transform.position, Leader.transform.position) <= leash;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            fighter = GetComponent<Fighter>();
            mover = GetComponent<Mover>();
            health = GetComponent<Health>();
            cover = GetComponent<CoverUser>();
            tree = GetComponent<BDTree>();
            oldBrain = GetComponent<AIController>();
            oldSight = GetComponent<FieldOfView>();
            //This component decides when the tree runs
            if (tree != null)
            {
                tree.StartWhenEnabled = false;
                if (tree.IsRunning()) tree.StopBehavior();
            }
            else Debug.LogWarning("[CompanionBrain] " + name + " has no Behavior Tree component; it will only stand still.", this);
        }

        void OnEnable()
        {
            //Still an enemy (a hackable robot): the enemy brain is in charge until it is hacked
            if (CompareTag("Enemy"))
            {
                enabled = false;
                return;
            }
            if (oldBrain != null) oldBrain.enabled = false;
            if (oldSight != null) oldSight.enabled = false;
            if (cover != null) cover.AutoSeek = false; //Engage picks the cover
            if (mover != null) mover.timerRunning = false;
            PlayerSelector.OnPlayerObjectChanged += OnLeaderChanged;
            if (selector == null) selector = FindObjectOfType<PlayerSelector>();
            OnLeaderChanged(selector != null ? selector.activePlayer : null);
        }

        void OnDisable()
        {
            PlayerSelector.OnPlayerObjectChanged -= OnLeaderChanged;
            StopTree();
        }

        void OnLeaderChanged(GameObject newLeader)
        {
            Leader = newLeader;
            if (IsControlled)
            {
                StopTree();
                Target = null;
                TargetVisible = false;
            }
            else if (!IsDead) StartTree();
        }

        void StartTree()
        {
            if (tree == null || treeRunning) return;
            //StartBehavior can report false while the tree did start, which made this retry every frame
            treeRunning = tree.StartBehavior() || tree.IsRunning();
        }

        void StopTree()
        {
            if (tree != null && treeRunning) tree.StopBehavior();
            treeRunning = false;
            //Drop the AI's fight and cover run; the player (or nobody, if dead) decides from here
            HoldFire();
            if (cover != null) cover.GiveUpHeading();
        }

        void Update()
        {
            if (IsDead)
            {
                StopTree();
                enabled = false;
                return;
            }
            if (IsControlled) return;
            //The tree can end on its own (e.g. a task fails out); a living companion always restarts it
            if (treeRunning && tree != null && !tree.IsRunning()) treeRunning = false;
            if (!treeRunning) StartTree(); //e.g. the tree wasn't ready the first time

            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.2f;
                Look();
            }
            UpdateWeapon();
        }

        // ---------- Senses ----------

        void Look()
        {
            if (Time.time >= nextCandidateRefresh)
            {
                //Refreshed each second, so waves and ship-deployed drones are picked up
                nextCandidateRefresh = Time.time + 1f;
                candidates.Clear();
                candidates.AddRange(GameObject.FindGameObjectsWithTag("Enemy"));
            }

            Vector3 around = Leader != null ? Leader.transform.position : transform.position;
            GameObject best = null;
            float bestDist = float.MaxValue;
            foreach (var c in candidates)
            {
                if (c == null || !c.activeInHierarchy || !c.CompareTag("Enemy") || !IsAlive(c)) continue;
                float dist = Vector3.Distance(transform.position, c.transform.position);
                if (dist > sightRange || dist >= bestDist) continue;
                if (Vector3.Distance(around, c.transform.position) > engageAroundLeader) continue;
                if (!HasLineOfSight(c)) continue;
                best = c;
                bestDist = dist;
            }

            TargetVisible = best != null;
            if (best != null)
            {
                //Stick with the current target while it is still in view, unless something is much closer
                bool keep = Target != null && Target != best && IsAlive(Target) && Target.CompareTag("Enemy") &&
                            HasLineOfSight(Target) &&
                            Vector3.Distance(transform.position, Target.transform.position) < bestDist + 4f;
                if (!keep) Target = best;
                LastSeenTime = Time.time;
            }
            else if (Target != null && (!IsAlive(Target) || !Target.CompareTag("Enemy")))
            {
                Target = null;
            }
        }

        public bool HasLineOfSight(GameObject other) =>
            !Physics.Linecast(Eye, other.transform.position + Vector3.up * 1.2f, sightBlockers, QueryTriggerInteraction.Ignore);

        static bool IsAlive(GameObject character)
        {
            var h = character.GetComponent<Health>();
            return h == null || !h.IsDead();
        }

        // ---------- Weapon ----------

        //Reloads when dry (NPC guns refill for free; player guns draw on their reserve)
        void UpdateWeapon()
        {
            if (fighter == null) return;
            var weapon = fighter.activeWeapon;
            var config = fighter.weaponConfig;
            if (weapon == null || config == null || config.isMelee || config.projectile == null) return;
            if (reloadAt == 0f && weapon.CurClip <= 0) reloadAt = Time.time + reloadTime;
            if (reloadAt > 0f && Time.time >= reloadAt)
            {
                weapon.Reload();
                reloadAt = 0f;
            }
        }

        // ---------- Actions for the tree ----------

        //Fight this target (the Fighter closes to range, aims and fires)
        public void Shoot(GameObject target)
        {
            if (target == null || fighter == null) { HoldFire(); return; }
            if (fighter.EnemyPos == target.transform) return;
            fighter.TargetCap = target.GetComponent<CapsuleCollider>();
            fighter.CombatTarget = target.GetComponent<CombatTarget>();
            fighter.Attack(target);
        }

        public void HoldFire()
        {
            if (fighter != null && fighter.EnemyPos != null) fighter.CancelTarget();
        }

        public void MoveTo(Vector3 destination, float speedFraction)
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            if (NavMesh.SamplePosition(destination, out var hit, 2f, NavMesh.AllAreas)) destination = hit.position;
            agent.isStopped = false;
            agent.destination = destination;
            agent.speed = MaxSpeed * Mathf.Clamp01(speedFraction);
        }

        public void Stop()
        {
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        }

        public bool Arrived(float tolerance = 0.4f)
        {
            if (!agent.enabled || !agent.isOnNavMesh || agent.pathPending) return false;
            return !agent.hasPath || agent.isStopped || agent.remainingDistance <= agent.stoppingDistance + tolerance;
        }

        //This companion's place in the squad (0 = first), counting the living squad members other than the leader
        public int SquadSlot
        {
            get
            {
                if (selector == null) return 0;
                int slot = 0;
                foreach (var member in selector.players)
                {
                    if (member == null || member == Leader || !IsAlive(member)) continue;
                    if (member == gameObject) return slot;
                    slot++;
                }
                return slot;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, sightRange);
            if (Application.isPlaying && Target != null)
            {
                Gizmos.color = TargetVisible ? Color.red : new Color(1f, 0.5f, 0f);
                Gizmos.DrawLine(Eye, Target.transform.position + Vector3.up);
            }
        }
    }
}
