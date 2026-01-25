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
    public class TimbertoesCon : MonoBehaviour
    {
        [SerializeField] PlayerSelector selector;
        [SerializeField] GameObject closestTarget = null;
        [SerializeField] PatrolPath patrolPath;
        [SerializeField] float turnTime = 3f;
        [SerializeField] float waypointTolerence = 1f;
        [SerializeField] float waypointDwellTime = 1.7f;
        [SerializeField] Mover mover;
        [Range(0, 1)]
        [SerializeField] float patrolSpeedFraction = 0.2f;
        float timeSinceArrivedAtWaypoint
         = Mathf.Infinity;
        float timeSinceLastSawPlayer
         = Mathf.Infinity;
        public List<GameObject> targetList
        = new List<GameObject>();

        public bool isAttacking;
        public bool isPatroling;
        public bool isReloading;
        public bool readyToLaunch;
        public bool isLaunched;
        public bool isIdle;
       
        public Animator animator;
        StateMachine stateMachine;
        CapsuleCollider capsuleCol;
        CombatTarget otherCombatTarget;
        int currentWaypointIndex = 0;
        float TimerForNextAttack;
        public Vector3 nextPosition;
        CombatTarget combatTarget;
        TimbertoesFOV FOV;
        GameObject player;
        NavMeshAgent nav;
        TFighter fighter;
        float coolDown;
        Health health;
        Drone drone;
             

        void OnEnable()
        {
            Health.targetDeath += UpdateTarget;
            Health.playerDeath += PlayerDeath;
            Health.aIHit += FindNearestTarget;
        }

        void OnDisable()
        {
            Health.targetDeath -= UpdateTarget;
            Health.playerDeath -= PlayerDeath;
            Health.aIHit -= FindNearestTarget;
        }

        void Awake()
        { 
            stateMachine = new StateMachine();
            fighter = GetComponent<TFighter>();
            health = GetComponent<Health>();
            mover = GetComponent<Mover>();
            FOV = GetComponent<TimbertoesFOV>();
            animator = GetComponent<Animator>();
            combatTarget = GetComponent<CombatTarget>();

            AssignTargetList();
            if (patrolPath != null) { nextPosition = patrolPath.GetWaypoint(1); }
            coolDown = 2.5f;
            TimerForNextAttack = coolDown;

            //States
            var patrol = new TPatrol(this, patrolPath, mover, waypointTolerence, waypointDwellTime, patrolSpeedFraction, timeSinceArrivedAtWaypoint, currentWaypointIndex, nextPosition);
            var attack = new TAttack(this, fighter, mover, FOV, TimerForNextAttack, timeSinceLastSawPlayer, turnTime, coolDown);
            var dead = new TDead(this, combatTarget, mover, fighter);
            var reloading = new TReload(this, fighter);
            var idle = new TIdle(this, fighter);
            var droneLaunch =  new TLaunch(this, fighter, animator, mover);

            //Transitions declaration          
            At(attack, patrol, HasTarget());
            At(patrol, attack, HasPath());
            At(attack, reloading, Reloaded());
            At(reloading, attack, IsReloading());
            At(droneLaunch, patrol, DroneLaunch());
            At(patrol, droneLaunch, DroneLaunched());

            //Initial State
            stateMachine.SetState(patrol);

            //Transitions initialization
            void At(IState to, IState from, Func<bool> condition) => stateMachine.AddTransition(to, from, condition);
            
            Func<bool> IsReloading() => () => isReloading == true;
            Func<bool> Reloaded() => () => isReloading == false;
            Func<bool> HasPath() => () => patrolPath != null && isPatroling == true && isAttacking == false;
            Func<bool> HasTarget() => () => isReloading == false && isAttacking == true && isPatroling == false;
            Func<bool> DroneLaunch() => () => readyToLaunch == true && timeSinceArrivedAtWaypoint > 2f && isPatroling == false;
            Func<bool> DroneLaunched() => () => isLaunched == true && isPatroling == true;
        }

       
        void Update()
        {
            stateMachine.Tick();
            Debug.Log(this.gameObject.name + " " + stateMachine._currentState);
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

        public void AttackBehaviour()
        {
            var targetAlive = closestTarget.GetComponent<Health>().isDead;
            if (targetAlive == false) { return; }
            fighter.Attack(combatTarget: closestTarget);
        }

        public void OnTriggerEnter(Collider other)
        {
            if(other.gameObject.CompareTag("TimbertoesPath"))
            {
                if (isLaunched == false)
                {
                    NotPatrolling();
                    readyToLaunch = true;
                    isLaunched = true;
                }
            }
        }

        public void OnTriggerExit(Collider other)
        {
            if (other.gameObject.CompareTag("TimbertoesPath"))
            {
                readyToLaunch = false;
                isLaunched = false;
            }
        }

        public void IsPatrolling() { isPatroling = true; }
        public void NotPatrolling() { isPatroling = false; }

        public void ReloadingTrue() { isReloading = true; }
        public void ReloadingFalse() { isReloading = false; }

        public void AttackingTrue() { isAttacking = true; }
        public void AttackingFalse() { isAttacking = false; }

        public void FacePlayer()
        {
            player = selector.activePlayer;
            mover.RotateTowards(player.transform);
        }

        public void StopAttacking()
        {
            AttackingFalse();
            fighter.TargetCap = null;
            FOV.radius = 0;
            IsPatrolling();
            Invoke("FOVReset", 5f);
        }

        public void FOVReset()
        {
            FOV.radius = 10f;
        }

        void PlayerDeath(GameObject player)
        {
            animator.SetTrigger("VictoryDance");
            AudioManager.PlayTimbertoesSound(AudioManager.TimbertoesSound.timbertoesWins, this.transform.position);
            fighter.TargetCap = null;
            fighter.CancelTarget();
            UpdateTarget();
        }

        void UpdateTarget()
        {
            StartCoroutine(nameof(RefreshEnemiesList));
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
            targetList.AddRange(collection: GameObject.FindGameObjectsWithTag("Player"));
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
