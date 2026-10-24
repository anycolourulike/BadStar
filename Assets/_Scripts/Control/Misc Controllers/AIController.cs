using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Core;
using Rambler.Movement;
using Rambler.Attributes;
using System;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.Animations.Rigging;

namespace Rambler.Control
{
    public class AIController : MonoBehaviour
    {
        [SerializeField] GameObject closestTarget = null;
        [SerializeField] GameObject footFX1;
        [SerializeField] GameObject footFX2;
        [SerializeField] PatrolPath patrolPath;
        [SerializeField] float suspicionTime = 3f;
        [SerializeField] float waypointTolerence = 1f;
        [SerializeField] float waypointDwellTime = 1.7f;
        [SerializeField] Mover mover;
        [SerializeField] FieldOfView FOV;
        public Transform patrolToTarget;
        [Range(0, 1)]
        [SerializeField] float patrolSpeedFraction = 0.2f;
        float timeSinceArrivedAtWaypoint
         = Mathf.Infinity;
        float timeSinceLastSawPlayer
         = Mathf.Infinity;
        public List<GameObject> targetList
        = new List<GameObject>();
        public PlayerSelector playerSelector;

        public bool isPatrollingToLocation;
        public bool isFollowingPlayer;
        public bool isPatrolling;
        public bool isReloading;

        public bool isIdle;
        public bool isDead;

        public bool canCrouch;
        public bool isCrouching;
        public bool isIdleCrouching;
        public bool isAtCrouchPoint;
        public bool timeToCrouchIsTrue;

        /// Rocker ///
        public bool rocksOnGround;
        public bool caveIn;
        public bool isRocker;
        public bool standUp;
        public bool hasRock;
        public bool readyToThrow;

        BoulderGennie boulderGennie;
        public string command;
        public Animator animator;
        StateMachine stateMachine;
        CapsuleCollider capsuleCol;
        CombatTarget otherCombatTarget;
        int currentWaypointIndex = 0;
        float TimerForNextAttack;
        public GameObject CrouchTransform;

        public Vector3 nextPosition;
        public GameObject activePlayer;
        CombatTarget combatTarget;
        ICauseCaveIn causeCaveIn;

        ISitting sitting;
        Fighter fighter;
        Boulder boulder;
        float coolDown;
        Health health;

        //States
        FollowPlayer followPlayer;
        Patrol patrol;
        Idle idle;
        Dead dead;

        private void Start()
        {
            FindPlayerSelector();
            if (playerSelector != null) activePlayer = playerSelector.activePlayer;
        }

        //Awake/OnEnable run before Start, so look this up as early as possible
        void FindPlayerSelector()
        {
            if (playerSelector != null) return;
            GameObject playerCore = GameObject.Find("/PlayerCore");
            if (playerCore != null) playerSelector = playerCore.GetComponent<PlayerSelector>();
        }

        void Awake()
        {
            FindPlayerSelector();
            if (this.transform.name == "Rocker")
            {
                isRocker = true;
                rocksOnGround = false;
                boulderGennie = GetComponent<BoulderGennie>();
                animator = GetComponent<Animator>();
            }
            if (this.gameObject.tag == "Player" && playerSelector != null)
            {
                activePlayer = playerSelector.activePlayer;
            }
            else if (this.gameObject.tag == "Enemy")
            {
                activePlayer = null;
            }
            stateMachine = new StateMachine();
            fighter = GetComponent<Fighter>();
            health = GetComponent<Health>();
            mover = GetComponent<Mover>();
            FOV = GetComponent<FieldOfView>();
            combatTarget = GetComponent<CombatTarget>();
            AssignTargetList();
            if (patrolPath != null && patrolPath.transform.childCount > 0)
            {
                nextPosition = patrolPath.GetWaypoint(patrolPath.GetNextIndex(0));
            }
            coolDown = 2.5f;
            TimerForNextAttack = coolDown;

            //States            
            patrol = new Patrol(this, patrolPath, mover, waypointTolerence, waypointDwellTime,
                                    patrolSpeedFraction, timeSinceArrivedAtWaypoint, currentWaypointIndex, nextPosition);
            followPlayer = new FollowPlayer(this, mover);
            idle = new Idle(this, fighter);
            causeCaveIn = new ICauseCaveIn(this, mover, animator, boulderGennie);
            sitting = new ISitting(this, animator);
            var collectRocks = new IFindRocks(this, boulderGennie, mover);
            var hasTarget = new IThrowRock(this, boulderGennie);
            var dead = new Dead(this, combatTarget, mover, fighter);

            //Transitions       
            At(idle, patrol, CompanionIdle());
            At(idle, patrol, enemyIdle());
            At(idle, followPlayer, follow());

            At(patrol, idle, HasPath());
            At(followPlayer, idle, follow());

            At(dead, followPlayer, IsDead());
            At(dead, patrol, IsDead());
            At(dead, idle, IsDead());     

            /////Rocker/////
            At(idle, sitting, StandUp());
            At(causeCaveIn, idle, CanCauseCaveIn());
            At(hasTarget, collectRocks, throwRocks());
            At(collectRocks, hasTarget, nextRock());
            At(causeCaveIn, collectRocks, CanCauseCaveIn());
            At(collectRocks, causeCaveIn, nextRock());

            /////Transitions/////  Change State by setting the relevant bool to true and the others to false
            void At(IState to, IState from, Func<bool> condition) => stateMachine.AddTransition(to, from, condition);

            Func<bool> IsDead() => () => isDead == true; 
            Func<bool> HasPath() => () => isPatrolling == true && isDead == false; 
            Func<bool> follow() => () => isFollowingPlayer == true && isDead == false; 
            Func<bool> enemyIdle() => () => this.gameObject.CompareTag("Enemy") && isDead == false &&
                                            (FOV == null || FOV.canSeePlayer == false) && isIdle == true;
            Func<bool> CompanionIdle() => () => this.gameObject.CompareTag("Companion") && isDead == false &&
                                             isIdle == true; 

            ////ROCKER////
            Func<bool> StandUp() => () => isRocker == true && standUp == true && caveIn == false;
            Func<bool> CanCauseCaveIn() => () => isRocker == true && rocksOnGround == false && caveIn == true; //no rocks on ground, no rocks in hand, starts jump / cave in sequence

            Func<bool> throwRocks() => () => isRocker == true && hasRock == true && caveIn == false && readyToThrow == true;
            Func<bool> nextRock() => () => isRocker == true && hasRock == false && rocksOnGround == true && caveIn == false && readyToThrow == false;
        }

        void OnEnable()
        {
            PlayerSelector.OnPlayerObjectChanged += UpdatePlayer;
            if (FOV != null) FOV.enabled = true;
            //Initial State
            if (this.gameObject.CompareTag("Enemy"))
            {
                stateMachine.SetState(idle);
            }
            if (this.gameObject.CompareTag("Companion"))
            {
                stateMachine.SetState(followPlayer);
            }
            if ((this.transform.name == "Rocker") && this.gameObject.CompareTag("Enemy"))
            {
                stateMachine.SetState(sitting);
                if (playerSelector != null) closestTarget = playerSelector.activePlayer;
            }           
            
        }

        void OnDisable()
        {
            PlayerSelector.OnPlayerObjectChanged -= UpdatePlayer;
            if (FOV != null) FOV.enabled = false;
        }

        void Update()
        {
            stateMachine.Tick();
            if (this.gameObject.CompareTag("Player"))
            {
                if (FOV != null && FOV.canSeePlayer == false)
                {
                    fighter.TargetCap = null;
                    UpdateTarget();
                }
            }
        }

        public void TargetHealthCheck()
        {
            if (closestTarget == null) return;
            var targetIsDead = closestTarget.GetComponent<Health>().isDead;
            if (targetIsDead == true) { UpdateTarget(); };
        }

        public void AssignTarget()
        {
            fighter.TargetCap = capsuleCol;
            fighter.CombatTarget = otherCombatTarget;
            fighter.Attack(combatTarget: closestTarget);
        }

        public void AtWayPoint()
        {
            string _command = command;
            animator.SetTrigger(command);
        }

        public void AttackBehaviour()
        {
            if (closestTarget == null) { return; }
            var targetIsDead = closestTarget.GetComponent<Health>().isDead;
            if (targetIsDead == true) { return; }
            fighter.Attack(combatTarget: closestTarget);
        }

        ///ROCKER///
        public void PlayFootFX1() { footFX1.SetActive(true); }
        public void DisablePlayFootFX1() { footFX1.SetActive(false); }
        public void PlayFootFX2() { footFX2.SetActive(true); }
        public void DisablePlayFootFX2() { footFX2.SetActive(false); }

        

        public void IsPatrolling()
        {
            isIdle = false;
            isFollowingPlayer = false;
            isDead = false;
            isPatrolling = true;
        }        

        public void IsIdle() 
        {
            isFollowingPlayer = false;
            isPatrolling = false;
            isDead = false;
            isIdle = true;
        }        

        public void FolllowingPlayer() 
        {
            isPatrolling = false;
            isDead = false;
            isIdle = false;
            isFollowingPlayer = true;
        }

        public void IsDead()
        {
            isFollowingPlayer = false;
            isPatrolling = false;
            isIdle = false;
            isDead = true;            
        }



        public void AtCrouchingPoint() { isAtCrouchPoint = true; }
        public void NotAtCrouchingPoint() { isAtCrouchPoint = false; }

        public void CanCrouch() { canCrouch = true; }
        public void CanNotCrouch() { canCrouch = false; }

        ///Rocker///

        public void CaveInTrue() { caveIn = true; }
        public void CaveInFalse() { caveIn = false; }

        public void ReadyToThrow()
        {
            readyToThrow = true;
            if (closestTarget != null) mover.RotateTowards(closestTarget.transform);
        }
        public void NotReadyToThrow() { readyToThrow = false; }

        public void RockInHand() { hasRock = true; }
        public void EmptyHand() { hasRock = false; }

        public void StandingUp() { standUp = true; }
        public void NotStandingUp() { standUp = false; }

        public void RocksOnGround() { rocksOnGround = true; }
        public void NoRocksOnGround() { rocksOnGround = false; }

        public void CrouchAtTransform()
        {
            isAtCrouchPoint = true;
            isCrouching = true;
            isIdleCrouching = true;
        }

        public void CrouchAnim()
        {
            animator.Play("Crouch");
        }

        public void FacePlayer()
        {
            if (playerSelector != null) activePlayer = playerSelector.activePlayer;
            if (activePlayer == null) return;
            mover.RotateTowards(activePlayer.transform);
        }

        void UpdatePlayer(GameObject newPlayer)
        {
            activePlayer = newPlayer;
        }

        void PlayerDeath()
        {
            fighter.TargetCap = null;
            fighter.CancelTarget();
            UpdateTarget();
        }

        void UpdateTarget()
        {
            StartCoroutine("RefreshEnemiesList");
        }

        public void UpdatePatrolToTarget(Transform targetPatrol)
        {
            patrolToTarget = targetPatrol;
            PatrolToTarget();
        }

        void PatrolToTarget()
        {
            mover.MoveTo(patrolToTarget.position, 7f);
        }

        IEnumerator RefreshEnemiesList()
        {
            targetList.Clear();
            AssignTargetList();
            FindNearestTarget();
            yield return new WaitForSeconds(0.01f);
        }

        void AssignTargetList()
        {
            if (this.gameObject.tag == "Player")
            {
                targetList.AddRange(collection: GameObject.FindGameObjectsWithTag("Enemy"));
            }
            else if (this.gameObject.tag == "Enemy")
            {
                targetList.AddRange(collection: GameObject.FindGameObjectsWithTag("Player"));
            }
        }

        public void FindNearestTarget()
        {
            float distToClosestTarget = Mathf.Infinity;
            closestTarget = null;
            foreach (GameObject target in targetList)
            {
                float distanceToTarget = (target.transform.position - this.transform.position).sqrMagnitude;
                if (distanceToTarget < distToClosestTarget)
                {
                    distToClosestTarget = distanceToTarget;
                    closestTarget = target;
                    capsuleCol = closestTarget.GetComponent<CapsuleCollider>();
                    otherCombatTarget = closestTarget.GetComponent<CombatTarget>();
                }
            }
        }
    }
}
